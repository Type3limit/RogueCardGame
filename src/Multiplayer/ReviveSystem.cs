using System;
using System.Collections.Generic;
using System.Linq;
using RogueCardGame.Core.Characters;

namespace RogueCardGame.Multiplayer;

// ─────────────────────────────────────────────────────────────
// Revive System — Downed state and teammate rescue
// ─────────────────────────────────────────────────────────────

public enum DownedState
{
    Active,
    Downed,     // HP reached 0, can be revived within N turns
    Eliminated  // Failed to revive in time, out for this combat
}

public class DownedPlayerInfo
{
    public int PeerId { get; set; }
    public int TurnsRemaining { get; set; }
    public int ReviveHpNeeded { get; set; } // Healing needed to revive
    public int ReviveHpReceived { get; set; }
    public DownedState State { get; set; } = DownedState.Active;
}

/// <summary>
/// Manages the downed and revive mechanic for multiplayer.
/// When a player's HP reaches 0:
/// 1. They enter "Downed" state for 3 turns
/// 2. Teammates can use healing cards on them to revive
/// 3. If not revived within 3 turns, they are eliminated for this combat
/// 4. After combat ends, eliminated players return with 1 HP
/// </summary>
public class ReviveSystem
{
    public int DownedTurnLimit { get; set; } = 3;
    public int ReviveHpThreshold { get; set; } = 15; // Total healing needed to revive

    private readonly Dictionary<int, DownedPlayerInfo> _downedPlayers = new();

    public event Action<DownedPlayerInfo>? OnPlayerDowned;
    public event Action<DownedPlayerInfo>? OnPlayerRevived;
    public event Action<DownedPlayerInfo>? OnPlayerEliminated;
    public event Action<DownedPlayerInfo>? OnReviveProgress;

    /// <summary>
    /// Called when a player's HP reaches 0.
    /// </summary>
    public DownedPlayerInfo DownPlayer(int peerId)
    {
        var info = new DownedPlayerInfo
        {
            PeerId = peerId,
            TurnsRemaining = DownedTurnLimit,
            ReviveHpNeeded = ReviveHpThreshold,
            ReviveHpReceived = 0,
            State = DownedState.Downed
        };

        _downedPlayers[peerId] = info;
        OnPlayerDowned?.Invoke(info);
        return info;
    }

    /// <summary>
    /// Apply healing toward a downed player's revival.
    /// Returns true if the player was successfully revived.
    /// </summary>
    public bool ApplyReviveHealing(int peerId, int healAmount)
    {
        if (!_downedPlayers.TryGetValue(peerId, out var info))
            return false;
        if (info.State != DownedState.Downed)
            return false;

        info.ReviveHpReceived += healAmount;
        OnReviveProgress?.Invoke(info);

        if (info.ReviveHpReceived >= info.ReviveHpNeeded)
        {
            info.State = DownedState.Active;
            _downedPlayers.Remove(peerId);
            OnPlayerRevived?.Invoke(info);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Called at end of each turn. Decrements downed timers.
    /// </summary>
    public List<DownedPlayerInfo> TickDownedTimers()
    {
        var eliminated = new List<DownedPlayerInfo>();

        foreach (var info in _downedPlayers.Values.ToList())
        {
            if (info.State != DownedState.Downed) continue;

            info.TurnsRemaining--;
            if (info.TurnsRemaining <= 0)
            {
                info.State = DownedState.Eliminated;
                eliminated.Add(info);
                OnPlayerEliminated?.Invoke(info);
            }
        }

        return eliminated;
    }

    /// <summary>
    /// Check if a player is downed.
    /// </summary>
    public bool IsPlayerDowned(int peerId) =>
        _downedPlayers.TryGetValue(peerId, out var info) && info.State == DownedState.Downed;

    /// <summary>
    /// Check if a player is eliminated.
    /// </summary>
    public bool IsPlayerEliminated(int peerId) =>
        _downedPlayers.TryGetValue(peerId, out var info) && info.State == DownedState.Eliminated;

    /// <summary>
    /// Get downed info for UI display.
    /// </summary>
    public DownedPlayerInfo? GetDownedInfo(int peerId) => _downedPlayers.GetValueOrDefault(peerId);

    /// <summary>
    /// Get all currently downed players.
    /// </summary>
    public IReadOnlyList<DownedPlayerInfo> GetAllDowned() =>
        _downedPlayers.Values.Where(d => d.State == DownedState.Downed).ToList();

    /// <summary>
    /// After combat ends, reset all downed/eliminated players.
    /// Returns peer IDs of players that need to be set to 1 HP.
    /// </summary>
    public List<int> ResetAfterCombat()
    {
        var needRevive = _downedPlayers.Keys.ToList();
        _downedPlayers.Clear();
        return needRevive;
    }
}
