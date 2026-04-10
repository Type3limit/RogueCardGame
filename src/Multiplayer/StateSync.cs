using System;
using System.Collections.Generic;
using System.Text.Json;
using RogueCardGame.Core.Characters;
using RogueCardGame.Core.Combat;

namespace RogueCardGame.Multiplayer;

// ─────────────────────────────────────────────────────────────
// State snapshot for synchronization
// ─────────────────────────────────────────────────────────────

/// <summary>
/// Complete combat state snapshot for network synchronization.
/// Serialized and sent from host to all clients after each resolution step.
/// </summary>
public class CombatSnapshot
{
    public int TurnNumber { get; set; }
    public int PhaseIndex { get; set; }
    public long SnapshotId { get; set; }
    public int GameSeed { get; set; }
    public int RandomState { get; set; }

    public List<PlayerSnapshot> Players { get; set; } = new();
    public List<EnemySnapshot> Enemies { get; set; } = new();
    public string EnvironmentId { get; set; } = "";
    public List<string> ActiveEffects { get; set; } = new();
}

public class PlayerSnapshot
{
    public int PeerId { get; set; }
    public string ClassName { get; set; } = "";
    public int CurrentHp { get; set; }
    public int MaxHp { get; set; }
    public int Block { get; set; }
    public int Energy { get; set; }
    public int MaxEnergy { get; set; }
    public bool IsFrontRow { get; set; }
    public bool IsDowned { get; set; }
    public int DownedTurnsLeft { get; set; }
    public int HandCount { get; set; }
    public int DrawPileCount { get; set; }
    public int DiscardPileCount { get; set; }
    public List<StatusSnapshot> Statuses { get; set; } = new();
    public Dictionary<string, int> ClassResources { get; set; } = new();
}

public class EnemySnapshot
{
    public string EnemyId { get; set; } = "";
    public string InstanceId { get; set; } = "";
    public int CurrentHp { get; set; }
    public int MaxHp { get; set; }
    public int Block { get; set; }
    public bool IsFrontRow { get; set; }
    public string IntentType { get; set; } = "";
    public int IntentValue { get; set; }
    public int HackProgress { get; set; }
    public int HackThreshold { get; set; }
    public List<StatusSnapshot> Statuses { get; set; } = new();
}

public class StatusSnapshot
{
    public string StatusId { get; set; } = "";
    public int Stacks { get; set; }
    public int Duration { get; set; }
}

// ─────────────────────────────────────────────────────────────
// State delta for efficient updates
// ─────────────────────────────────────────────────────────────

public enum DeltaType
{
    PlayerHpChanged,
    PlayerBlockChanged,
    PlayerEnergyChanged,
    PlayerRowChanged,
    PlayerStatusChanged,
    PlayerDowned,
    PlayerRevived,
    EnemyHpChanged,
    EnemyBlockChanged,
    EnemyRowChanged,
    EnemyStatusChanged,
    EnemyDefeated,
    EnemySpawned,
    CardPlayed,
    CardDrawn,
    EnvironmentChanged,
    LinkTriggered,
    TurnChanged
}

public class StateDelta
{
    public DeltaType Type { get; set; }
    public string TargetId { get; set; } = "";
    public int IntValue { get; set; }
    public string StringValue { get; set; } = "";
    public long Timestamp { get; set; }
}

// ─────────────────────────────────────────────────────────────
// StateSync: Manages state synchronization between host and clients
// ─────────────────────────────────────────────────────────────

/// <summary>
/// Handles combat state synchronization.
/// Host creates snapshots/deltas; clients consume them.
/// Supports full snapshot for reconnection and deltas for normal play.
/// </summary>
public class StateSync
{
    private readonly NetworkManager _network;
    private CombatSnapshot? _lastSnapshot;
    private readonly List<StateDelta> _pendingDeltas = new();
    private long _snapshotCounter;

    // Checksum for state validation
    private int _lastChecksum;

    public event Action<CombatSnapshot>? OnSnapshotReceived;
    public event Action<StateDelta>? OnDeltaReceived;
    public event Action? OnStateMismatch;

    public StateSync(NetworkManager network)
    {
        _network = network;
        _network.RegisterHandler(MessageType.GameStateSnapshot, HandleSnapshot);
        _network.RegisterHandler(MessageType.GameStateDelta, HandleDelta);
        _network.RegisterHandler(MessageType.ReconnectState, HandleReconnect);
    }

    // ─────── Host-side: Create and send state ───────

    /// <summary>
    /// Host creates a full snapshot of current combat state and broadcasts it.
    /// Used at start of combat and for reconnecting clients.
    /// </summary>
    public void BroadcastSnapshot(CombatSnapshot snapshot)
    {
        if (!_network.IsHost) return;

        snapshot.SnapshotId = ++_snapshotCounter;
        _lastSnapshot = snapshot;
        _lastChecksum = ComputeChecksum(snapshot);
        _pendingDeltas.Clear();

        var msg = new NetMessage(MessageType.GameStateSnapshot, 0, snapshot);
        _network.Broadcast(msg);
    }

    /// <summary>
    /// Host sends a state delta (incremental update).
    /// </summary>
    public void BroadcastDelta(StateDelta delta)
    {
        if (!_network.IsHost) return;

        delta.Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        _pendingDeltas.Add(delta);

        var msg = new NetMessage(MessageType.GameStateDelta, 0, delta);
        _network.Broadcast(msg);
    }

    /// <summary>
    /// Send full snapshot to a specific peer (reconnection).
    /// </summary>
    public void SendSnapshotTo(int peerId)
    {
        if (!_network.IsHost || _lastSnapshot == null) return;

        var msg = new NetMessage(MessageType.ReconnectState, 0, _lastSnapshot);
        _network.SendTo(peerId, msg);
    }

    // ─────── Client-side: Receive and apply state ───────

    private void HandleSnapshot(NetMessage msg)
    {
        var snapshot = msg.GetPayload<CombatSnapshot>();
        if (snapshot == null) return;

        _lastSnapshot = snapshot;
        _lastChecksum = ComputeChecksum(snapshot);
        OnSnapshotReceived?.Invoke(snapshot);
    }

    private void HandleDelta(NetMessage msg)
    {
        var delta = msg.GetPayload<StateDelta>();
        if (delta == null) return;

        OnDeltaReceived?.Invoke(delta);
    }

    private void HandleReconnect(NetMessage msg)
    {
        var snapshot = msg.GetPayload<CombatSnapshot>();
        if (snapshot == null) return;

        _lastSnapshot = snapshot;
        OnSnapshotReceived?.Invoke(snapshot);
    }

    // ─────── Validation ───────

    /// <summary>
    /// Verify local state matches the host's checksum.
    /// If mismatch, request full snapshot.
    /// </summary>
    public void ValidateState(CombatSnapshot localState)
    {
        int localChecksum = ComputeChecksum(localState);
        if (localChecksum != _lastChecksum)
        {
            OnStateMismatch?.Invoke();
            // Request full snapshot from host
            if (!_network.IsHost)
            {
                _network.SendTo(_network.HostPeerId,
                    new NetMessage(MessageType.Reconnect, _network.LocalPeerId));
            }
        }
    }

    /// <summary>
    /// Simple checksum for state validation.
    /// </summary>
    private static int ComputeChecksum(CombatSnapshot snapshot)
    {
        int hash = snapshot.TurnNumber * 31;
        foreach (var player in snapshot.Players)
        {
            hash = hash * 17 + player.CurrentHp;
            hash = hash * 17 + player.Block;
            hash = hash * 17 + player.Energy;
            hash = hash * 17 + (player.IsFrontRow ? 1 : 0);
        }
        foreach (var enemy in snapshot.Enemies)
        {
            hash = hash * 17 + enemy.CurrentHp;
            hash = hash * 17 + enemy.Block;
        }
        return hash;
    }

    public CombatSnapshot? GetLastSnapshot() => _lastSnapshot;
}
