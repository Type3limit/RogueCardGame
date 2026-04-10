using System;
using System.Collections.Generic;
using System.Linq;

namespace RogueCardGame.Multiplayer;

// ─────────────────────────────────────────────────────────────
// Lobby settings and state
// ─────────────────────────────────────────────────────────────

public enum LobbyState
{
    WaitingForPlayers,
    AllReady,
    Starting,
    InGame,
    Closed
}

public class LobbySettings
{
    public int MaxPlayers { get; set; } = 4;
    public int MinPlayers { get; set; } = 1;
    public bool IsPublic { get; set; } = true;
    public int Difficulty { get; set; } = 0; // 0 = normal, 1+ = ascension
    public bool AllowDuplicateClasses { get; set; } = false;
    public bool FriendlyFire { get; set; } = false;
    public string GameMode { get; set; } = "coop_pve"; // coop_pve, pvp_1v1, pvp_2v2
}

public class LobbySlot
{
    public int SlotIndex { get; set; }
    public int PeerId { get; set; } = -1;
    public string DisplayName { get; set; } = "";
    public string SelectedClass { get; set; } = "";
    public bool IsReady { get; set; }
    public bool IsHost { get; set; }

    public bool IsEmpty => PeerId == -1;
}

// ─────────────────────────────────────────────────────────────
// LobbyManager
// ─────────────────────────────────────────────────────────────

/// <summary>
/// Manages lobby state including player slots, readiness, class selection.
/// </summary>
public class LobbyManager
{
    public LobbyState State { get; private set; } = LobbyState.WaitingForPlayers;
    public LobbySettings Settings { get; private set; }
    public string LobbyCode { get; private set; } = "";

    private readonly LobbySlot[] _slots;
    private readonly NetworkManager _network;

    public event Action<LobbySlot>? OnPlayerJoined;
    public event Action<LobbySlot>? OnPlayerLeft;
    public event Action<LobbySlot>? OnPlayerReady;
    public event Action<LobbySlot>? OnPlayerClassChanged;
    public event Action? OnAllReady;
    public event Action? OnGameStarting;
    public event Action<string>? OnChatMessage;

    public LobbyManager(NetworkManager network, LobbySettings? settings = null)
    {
        _network = network;
        Settings = settings ?? new LobbySettings();
        _slots = new LobbySlot[Settings.MaxPlayers];
        for (int i = 0; i < _slots.Length; i++)
        {
            _slots[i] = new LobbySlot { SlotIndex = i };
        }

        RegisterHandlers();
    }

    private void RegisterHandlers()
    {
        _network.RegisterHandler(MessageType.LobbyJoin, HandleJoin);
        _network.RegisterHandler(MessageType.LobbyLeave, HandleLeave);
        _network.RegisterHandler(MessageType.LobbyReady, HandleReady);
        _network.RegisterHandler(MessageType.LobbyStart, HandleStart);
        _network.RegisterHandler(MessageType.LobbyChat, HandleChat);
        _network.RegisterHandler(MessageType.LobbySyncSettings, HandleSyncSettings);
    }

    /// <summary>
    /// Create a new lobby as host.
    /// </summary>
    public void CreateLobby(string hostName, string hostClass = "")
    {
        LobbyCode = GenerateLobbyCode();
        State = LobbyState.WaitingForPlayers;

        // Host occupies slot 0
        _slots[0].PeerId = 0;
        _slots[0].DisplayName = hostName;
        _slots[0].SelectedClass = hostClass;
        _slots[0].IsHost = true;
    }

    /// <summary>
    /// Add a player to the lobby (host-side).
    /// </summary>
    public bool TryAddPlayer(int peerId, string displayName)
    {
        if (State != LobbyState.WaitingForPlayers)
            return false;

        var emptySlot = _slots.FirstOrDefault(s => s.IsEmpty);
        if (emptySlot == null)
            return false;

        emptySlot.PeerId = peerId;
        emptySlot.DisplayName = displayName;
        emptySlot.IsReady = false;
        OnPlayerJoined?.Invoke(emptySlot);

        // Sync lobby state to new player
        BroadcastLobbyState();
        return true;
    }

    /// <summary>
    /// Remove a player from the lobby.
    /// </summary>
    public void RemovePlayer(int peerId)
    {
        var slot = _slots.FirstOrDefault(s => s.PeerId == peerId);
        if (slot == null) return;

        string name = slot.DisplayName;
        slot.PeerId = -1;
        slot.DisplayName = "";
        slot.SelectedClass = "";
        slot.IsReady = false;
        OnPlayerLeft?.Invoke(slot);

        CheckReadyState();
        BroadcastLobbyState();
    }

    /// <summary>
    /// Set a player's selected class.
    /// </summary>
    public bool SetPlayerClass(int peerId, string className)
    {
        var slot = _slots.FirstOrDefault(s => s.PeerId == peerId);
        if (slot == null) return false;

        // Check for duplicate classes if not allowed
        if (!Settings.AllowDuplicateClasses)
        {
            bool taken = _slots.Any(s => s.PeerId != peerId && !s.IsEmpty && s.SelectedClass == className);
            if (taken) return false;
        }

        slot.SelectedClass = className;
        slot.IsReady = false; // Changing class un-readies
        OnPlayerClassChanged?.Invoke(slot);
        BroadcastLobbyState();
        return true;
    }

    /// <summary>
    /// Toggle ready state for a player.
    /// </summary>
    public void SetPlayerReady(int peerId, bool ready)
    {
        var slot = _slots.FirstOrDefault(s => s.PeerId == peerId);
        if (slot == null) return;

        // Must have selected a class to be ready
        if (ready && string.IsNullOrEmpty(slot.SelectedClass)) return;

        slot.IsReady = ready;
        OnPlayerReady?.Invoke(slot);
        CheckReadyState();
        BroadcastLobbyState();
    }

    /// <summary>
    /// Start the game (host only).
    /// </summary>
    public bool TryStartGame()
    {
        if (!_network.IsHost) return false;
        if (State != LobbyState.AllReady) return false;

        int playerCount = _slots.Count(s => !s.IsEmpty);
        if (playerCount < Settings.MinPlayers) return false;

        State = LobbyState.Starting;
        OnGameStarting?.Invoke();

        // Broadcast game start with seed
        var seed = new Random().Next();
        _network.Broadcast(new NetMessage(MessageType.LobbyStart, 0, new { Seed = seed }));

        State = LobbyState.InGame;
        return true;
    }

    // ─────── Message handlers ───────

    private void HandleJoin(NetMessage msg)
    {
        if (!_network.IsHost) return;
        var data = msg.GetPayload<Dictionary<string, string>>();
        string name = data?.GetValueOrDefault("name") ?? "Player";
        TryAddPlayer(msg.SenderId, name);
    }

    private void HandleLeave(NetMessage msg)
    {
        RemovePlayer(msg.SenderId);
    }

    private void HandleReady(NetMessage msg)
    {
        var data = msg.GetPayload<Dictionary<string, bool>>();
        bool ready = data?.GetValueOrDefault("ready") ?? true;
        SetPlayerReady(msg.SenderId, ready);
    }

    private void HandleStart(NetMessage msg)
    {
        if (_network.IsHost) return; // Host handles start locally
        State = LobbyState.InGame;
        OnGameStarting?.Invoke();
    }

    private void HandleChat(NetMessage msg)
    {
        var data = msg.GetPayload<Dictionary<string, string>>();
        string text = data?.GetValueOrDefault("text") ?? "";
        var slot = _slots.FirstOrDefault(s => s.PeerId == msg.SenderId);
        OnChatMessage?.Invoke($"{slot?.DisplayName ?? "???"}: {text}");

        // Host rebroadcasts chat
        if (_network.IsHost)
            _network.Broadcast(msg);
    }

    private void HandleSyncSettings(NetMessage msg)
    {
        if (_network.IsHost) return;
        var settings = msg.GetPayload<LobbySettings>();
        if (settings != null) Settings = settings;
    }

    // ─────── Helpers ───────

    private void CheckReadyState()
    {
        var occupiedSlots = _slots.Where(s => !s.IsEmpty).ToList();
        if (occupiedSlots.Count >= Settings.MinPlayers && occupiedSlots.All(s => s.IsReady))
        {
            State = LobbyState.AllReady;
            OnAllReady?.Invoke();
        }
        else
        {
            State = LobbyState.WaitingForPlayers;
        }
    }

    private void BroadcastLobbyState()
    {
        if (!_network.IsHost) return;
        // Broadcast full lobby state to all clients
        _network.Broadcast(new NetMessage(MessageType.LobbySyncSettings, 0, Settings));
    }

    private static string GenerateLobbyCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var rng = new Random();
        return new string(Enumerable.Range(0, 6).Select(_ => chars[rng.Next(chars.Length)]).ToArray());
    }

    // ─────── Queries ───────

    public IReadOnlyList<LobbySlot> GetSlots() => _slots;
    public LobbySlot? GetSlot(int peerId) => _slots.FirstOrDefault(s => s.PeerId == peerId);
    public int PlayerCount => _slots.Count(s => !s.IsEmpty);
    public bool IsFull => PlayerCount >= Settings.MaxPlayers;

    public IEnumerable<(int PeerId, string ClassName)> GetPlayerClasses()
    {
        return _slots.Where(s => !s.IsEmpty)
                     .Select(s => (s.PeerId, s.SelectedClass));
    }
}
