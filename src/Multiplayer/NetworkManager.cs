using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RogueCardGame.Multiplayer;

// ─────────────────────────────────────────────────────────────
// Network message types for all multiplayer communication
// ─────────────────────────────────────────────────────────────

public enum MessageType : byte
{
    // Lobby
    LobbyCreate,
    LobbyJoin,
    LobbyLeave,
    LobbyReady,
    LobbyStart,
    LobbyChat,
    LobbySyncSettings,

    // Game state
    GameSeed,
    GameStateSnapshot,
    GameStateDelta,

    // Turn flow
    TurnPlanSubmit,
    TurnPlanAck,
    TurnAllPlansReady,
    TurnResolveStep,
    TurnEnd,

    // Player actions
    ActionPlayCard,
    ActionSwitchRow,
    ActionUsePotion,
    ActionEndTurn,
    ActionTargetSelect,

    // Synergy link
    LinkCardPlayed,
    LinkResponseWindow,
    LinkResolved,

    // Revive
    PlayerDowned,
    PlayerRevived,
    PlayerEliminated,

    // System
    Ping,
    Pong,
    Disconnect,
    Reconnect,
    ReconnectState,
    Kick,
    Error
}

/// <summary>
/// Base network message. All multiplayer communication is serialized through this.
/// </summary>
public class NetMessage
{
    public MessageType Type { get; set; }
    public long Timestamp { get; set; }
    public int SenderId { get; set; }
    public string Payload { get; set; } = "";

    public NetMessage() { }

    public NetMessage(MessageType type, int senderId, object? payload = null)
    {
        Type = type;
        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        SenderId = senderId;
        Payload = payload != null ? JsonSerializer.Serialize(payload) : "";
    }

    public T? GetPayload<T>()
    {
        if (string.IsNullOrEmpty(Payload)) return default;
        return JsonSerializer.Deserialize<T>(Payload);
    }

    public byte[] Serialize()
    {
        return JsonSerializer.SerializeToUtf8Bytes(this);
    }

    public static NetMessage? Deserialize(byte[] data)
    {
        return JsonSerializer.Deserialize<NetMessage>(data);
    }
}

// ─────────────────────────────────────────────────────────────
// Network peer representation
// ─────────────────────────────────────────────────────────────

public enum PeerState
{
    Connected,
    Ready,
    InGame,
    Disconnected,
    Reconnecting
}

public class NetPeer
{
    public int PeerId { get; set; }
    public ulong SteamId { get; set; }
    public string DisplayName { get; set; } = "Player";
    public PeerState State { get; set; } = PeerState.Connected;
    public int PlayerSlot { get; set; } = -1;
    public string SelectedClass { get; set; } = "";
    public long LastPingTime { get; set; }
    public int Latency { get; set; }
}

// ─────────────────────────────────────────────────────────────
// NetworkManager: Host-authoritative P2P network layer
// ─────────────────────────────────────────────────────────────

/// <summary>
/// Core networking manager. Abstracts P2P communication with host-authoritative model.
/// In production, the transport layer uses GodotSteam P2P.
/// This implementation provides the logical layer that can work with any transport.
/// </summary>
public class NetworkManager
{
    public bool IsHost { get; private set; }
    public int LocalPeerId { get; private set; }
    public int HostPeerId { get; private set; }
    public bool IsConnected { get; private set; }

    private readonly Dictionary<int, NetPeer> _peers = new();
    private readonly Queue<NetMessage> _outgoingQueue = new();
    private readonly Queue<NetMessage> _incomingQueue = new();
    private readonly Dictionary<MessageType, List<Action<NetMessage>>> _handlers = new();

    // Events for higher-level systems
    public event Action<NetPeer>? OnPeerConnected;
    public event Action<NetPeer>? OnPeerDisconnected;
    public event Action<NetMessage>? OnMessageReceived;

    private int _nextPeerId = 1;

    /// <summary>
    /// Initialize as host (server).
    /// </summary>
    public void HostGame(ulong steamId, string displayName)
    {
        IsHost = true;
        LocalPeerId = 0;
        HostPeerId = 0;
        IsConnected = true;

        var hostPeer = new NetPeer
        {
            PeerId = 0,
            SteamId = steamId,
            DisplayName = displayName,
            State = PeerState.Connected,
            PlayerSlot = 0
        };
        _peers[0] = hostPeer;
    }

    /// <summary>
    /// Initialize as client (joining a host).
    /// </summary>
    public void JoinGame(ulong hostSteamId, ulong localSteamId, string displayName)
    {
        IsHost = false;
        HostPeerId = 0;
        IsConnected = true;
        // PeerId is assigned by host upon connection
        // For now use -1 until assigned
        LocalPeerId = -1;
    }

    /// <summary>
    /// Register a handler for a specific message type.
    /// </summary>
    public void RegisterHandler(MessageType type, Action<NetMessage> handler)
    {
        if (!_handlers.ContainsKey(type))
            _handlers[type] = new List<Action<NetMessage>>();
        _handlers[type].Add(handler);
    }

    /// <summary>
    /// Unregister all handlers for a message type.
    /// </summary>
    public void UnregisterHandler(MessageType type)
    {
        _handlers.Remove(type);
    }

    /// <summary>
    /// Send a message to a specific peer. If host, sends directly.
    /// If client, routes through host.
    /// </summary>
    public void SendTo(int targetPeerId, NetMessage message)
    {
        message.SenderId = LocalPeerId;
        if (IsHost)
        {
            // Host sends directly to target
            _outgoingQueue.Enqueue(message);
        }
        else
        {
            // Client routes through host
            _outgoingQueue.Enqueue(message);
        }
    }

    /// <summary>
    /// Broadcast a message to all peers. Only host should broadcast game state.
    /// </summary>
    public void Broadcast(NetMessage message)
    {
        message.SenderId = LocalPeerId;
        foreach (var peer in _peers.Values)
        {
            if (peer.PeerId != LocalPeerId)
            {
                _outgoingQueue.Enqueue(message);
            }
        }
    }

    /// <summary>
    /// Host-side: accept a new peer connection.
    /// </summary>
    public NetPeer AcceptPeer(ulong steamId, string displayName)
    {
        if (!IsHost)
            throw new InvalidOperationException("Only host can accept peers");

        int peerId = _nextPeerId++;
        var peer = new NetPeer
        {
            PeerId = peerId,
            SteamId = steamId,
            DisplayName = displayName,
            State = PeerState.Connected,
            PlayerSlot = peerId
        };
        _peers[peerId] = peer;
        OnPeerConnected?.Invoke(peer);
        return peer;
    }

    /// <summary>
    /// Remove a peer (disconnect or kick).
    /// </summary>
    public void RemovePeer(int peerId)
    {
        if (_peers.TryGetValue(peerId, out var peer))
        {
            peer.State = PeerState.Disconnected;
            _peers.Remove(peerId);
            OnPeerDisconnected?.Invoke(peer);
        }
    }

    /// <summary>
    /// Process incoming messages and dispatch to registered handlers.
    /// Called each frame/tick by the game loop.
    /// </summary>
    public void ProcessMessages()
    {
        while (_incomingQueue.Count > 0)
        {
            var message = _incomingQueue.Dequeue();
            OnMessageReceived?.Invoke(message);

            if (_handlers.TryGetValue(message.Type, out var handlers))
            {
                foreach (var handler in handlers)
                {
                    handler(message);
                }
            }
        }
    }

    /// <summary>
    /// Simulate receiving a message (used for testing or local host-to-self).
    /// </summary>
    public void EnqueueIncoming(NetMessage message)
    {
        _incomingQueue.Enqueue(message);
    }

    /// <summary>
    /// Get all queued outgoing messages and clear the queue.
    /// The transport layer should call this to actually send packets.
    /// </summary>
    public List<NetMessage> FlushOutgoing()
    {
        var messages = _outgoingQueue.ToList();
        _outgoingQueue.Clear();
        return messages;
    }

    public NetPeer? GetPeer(int peerId) => _peers.GetValueOrDefault(peerId);
    public IReadOnlyDictionary<int, NetPeer> GetAllPeers() => _peers;
    public int PeerCount => _peers.Count;

    /// <summary>
    /// Update ping for a peer.
    /// </summary>
    public void UpdatePeerLatency(int peerId, int latencyMs)
    {
        if (_peers.TryGetValue(peerId, out var peer))
        {
            peer.Latency = latencyMs;
            peer.LastPingTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }

    /// <summary>
    /// Clean up connections.
    /// </summary>
    public void Shutdown()
    {
        Broadcast(new NetMessage(MessageType.Disconnect, LocalPeerId));
        _peers.Clear();
        _outgoingQueue.Clear();
        _incomingQueue.Clear();
        IsConnected = false;
    }
}
