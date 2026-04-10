using System;
using System.Collections.Generic;
using System.Linq;

namespace RogueCardGame.Multiplayer;

// ─────────────────────────────────────────────────────────────
// Multiplayer turn flow: Simultaneous planning → Speed-based resolution
// ─────────────────────────────────────────────────────────────

public enum MultiplayerPhase
{
    Planning,       // All players simultaneously plan their actions
    WaitingForPlans,// Some players haven't submitted yet
    Resolving,      // Actions resolve in speed order
    EnemyPhase,     // Enemies act
    EndOfTurn       // Cleanup
}

/// <summary>
/// A single planned action from a player for this turn.
/// </summary>
public class PlannedAction
{
    public int PeerId { get; set; }
    public PlannedActionType ActionType { get; set; }
    public string CardId { get; set; } = "";
    public string TargetId { get; set; } = "";
    public int Priority { get; set; } // Lower = faster, Quick cards get priority 0
    public bool IsQuick { get; set; } // Quick cards resolve first
    public bool IsLink { get; set; }  // Is this a synergy link card
}

public enum PlannedActionType
{
    PlayCard,
    SwitchRow,
    UsePotion,
    Pass
}

/// <summary>
/// A player's complete plan for one turn.
/// </summary>
public class TurnPlan
{
    public int PeerId { get; set; }
    public List<PlannedAction> Actions { get; set; } = new();
    public bool IsAutoConfirmed { get; set; }
    public long SubmittedAt { get; set; }
}

// ─────────────────────────────────────────────────────────────
// MultiplayerTurnManager
// ─────────────────────────────────────────────────────────────

/// <summary>
/// Manages the multiplayer turn flow.
/// 1) All players plan simultaneously (with optional timeout)
/// 2) Plans resolve in speed order (Quick first, then by player speed)
/// 3) Link cards check for responses during resolution
/// 4) Enemy phase
/// 5) End of turn cleanup
/// </summary>
public class MultiplayerTurnManager
{
    public MultiplayerPhase CurrentPhase { get; private set; } = MultiplayerPhase.Planning;
    public int TurnNumber { get; private set; }
    public float PlanningTimeLimit { get; set; } = 60f; // seconds
    public float PlanningTimeRemaining { get; private set; }

    private readonly NetworkManager _network;
    private readonly Dictionary<int, TurnPlan> _submittedPlans = new();
    private readonly List<int> _activePlayers = new();
    private List<PlannedAction> _resolvedActions = new();
    private int _resolveIndex;

    public event Action? OnPlanningPhaseStart;
    public event Action<int>? OnPlayerPlanReceived; // peerId
    public event Action? OnAllPlansReceived;
    public event Action<PlannedAction>? OnActionResolving;
    public event Action? OnResolutionComplete;
    public event Action? OnEnemyPhaseStart;
    public event Action? OnTurnEnd;
    public event Action<float>? OnTimerTick;

    public MultiplayerTurnManager(NetworkManager network)
    {
        _network = network;
        _network.RegisterHandler(MessageType.TurnPlanSubmit, HandlePlanSubmit);
        _network.RegisterHandler(MessageType.TurnPlanAck, HandlePlanAck);
        _network.RegisterHandler(MessageType.TurnAllPlansReady, HandleAllPlansReady);
        _network.RegisterHandler(MessageType.TurnResolveStep, HandleResolveStep);
    }

    /// <summary>
    /// Set which players are active this turn (not downed).
    /// </summary>
    public void SetActivePlayers(IEnumerable<int> peerIds)
    {
        _activePlayers.Clear();
        _activePlayers.AddRange(peerIds);
    }

    /// <summary>
    /// Start a new turn's planning phase.
    /// </summary>
    public void StartPlanningPhase()
    {
        TurnNumber++;
        CurrentPhase = MultiplayerPhase.Planning;
        _submittedPlans.Clear();
        _resolvedActions.Clear();
        _resolveIndex = 0;
        PlanningTimeRemaining = PlanningTimeLimit;

        OnPlanningPhaseStart?.Invoke();

        if (_network.IsHost)
        {
            _network.Broadcast(new NetMessage(MessageType.TurnEnd, 0,
                new { Turn = TurnNumber, Phase = "planning" }));
        }
    }

    /// <summary>
    /// Submit the local player's plan. Sends to host for validation.
    /// </summary>
    public void SubmitPlan(TurnPlan plan)
    {
        plan.SubmittedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        if (_network.IsHost)
        {
            // Host validates and stores directly
            AcceptPlan(plan);
        }
        else
        {
            // Client sends to host
            _network.SendTo(_network.HostPeerId,
                new NetMessage(MessageType.TurnPlanSubmit, _network.LocalPeerId, plan));
        }
    }

    /// <summary>
    /// Update planning timer. Called each frame.
    /// </summary>
    public void UpdatePlanningTimer(float deltaTime)
    {
        if (CurrentPhase != MultiplayerPhase.Planning &&
            CurrentPhase != MultiplayerPhase.WaitingForPlans)
            return;

        PlanningTimeRemaining -= deltaTime;
        OnTimerTick?.Invoke(PlanningTimeRemaining);

        if (PlanningTimeRemaining <= 0)
        {
            // Auto-confirm all players who haven't submitted
            AutoConfirmMissingPlans();
        }
    }

    /// <summary>
    /// Resolve the next planned action in the queue.
    /// Returns true if there are more actions to resolve.
    /// </summary>
    public bool ResolveNextAction(out PlannedAction? action)
    {
        action = null;
        if (CurrentPhase != MultiplayerPhase.Resolving)
            return false;

        if (_resolveIndex >= _resolvedActions.Count)
        {
            // All actions resolved, move to enemy phase
            CurrentPhase = MultiplayerPhase.EnemyPhase;
            OnResolutionComplete?.Invoke();
            OnEnemyPhaseStart?.Invoke();
            return false;
        }

        action = _resolvedActions[_resolveIndex++];
        OnActionResolving?.Invoke(action);

        if (_network.IsHost)
        {
            _network.Broadcast(new NetMessage(MessageType.TurnResolveStep, 0, action));
        }

        return true;
    }

    /// <summary>
    /// Complete enemy phase and end the turn.
    /// </summary>
    public void EndTurn()
    {
        CurrentPhase = MultiplayerPhase.EndOfTurn;
        OnTurnEnd?.Invoke();
    }

    // ─────── Host-side plan handling ───────

    private void HandlePlanSubmit(NetMessage msg)
    {
        if (!_network.IsHost) return;

        var plan = msg.GetPayload<TurnPlan>();
        if (plan == null) return;

        plan.PeerId = msg.SenderId;
        AcceptPlan(plan);
    }

    private void AcceptPlan(TurnPlan plan)
    {
        _submittedPlans[plan.PeerId] = plan;
        OnPlayerPlanReceived?.Invoke(plan.PeerId);

        // Send acknowledgment
        _network.SendTo(plan.PeerId,
            new NetMessage(MessageType.TurnPlanAck, 0, new { PeerId = plan.PeerId }));

        // Check if all active players have submitted
        if (_activePlayers.All(p => _submittedPlans.ContainsKey(p)))
        {
            BeginResolution();
        }
        else
        {
            CurrentPhase = MultiplayerPhase.WaitingForPlans;
        }
    }

    private void AutoConfirmMissingPlans()
    {
        foreach (var peerId in _activePlayers)
        {
            if (!_submittedPlans.ContainsKey(peerId))
            {
                // Create an empty plan (pass turn)
                var autoPlan = new TurnPlan
                {
                    PeerId = peerId,
                    IsAutoConfirmed = true,
                    Actions = new List<PlannedAction>
                    {
                        new PlannedAction { PeerId = peerId, ActionType = PlannedActionType.Pass }
                    }
                };
                AcceptPlan(autoPlan);
            }
        }
    }

    private void BeginResolution()
    {
        OnAllPlansReceived?.Invoke();
        CurrentPhase = MultiplayerPhase.Resolving;

        // Flatten all actions and sort by priority
        _resolvedActions = _submittedPlans.Values
            .SelectMany(p => p.Actions)
            .OrderBy(a => a.IsQuick ? 0 : 1)   // Quick actions first
            .ThenBy(a => a.Priority)             // Then by speed
            .ThenBy(a => a.PeerId)               // Tiebreak by peer id
            .ToList();

        _resolveIndex = 0;

        // Notify clients that resolution begins
        if (_network.IsHost)
        {
            _network.Broadcast(new NetMessage(MessageType.TurnAllPlansReady, 0,
                new { ActionCount = _resolvedActions.Count }));
        }
    }

    // ─────── Client-side handlers ───────

    private void HandlePlanAck(NetMessage msg)
    {
        // Plan was accepted by host
    }

    private void HandleAllPlansReady(NetMessage msg)
    {
        if (_network.IsHost) return;
        CurrentPhase = MultiplayerPhase.Resolving;
        OnAllPlansReceived?.Invoke();
    }

    private void HandleResolveStep(NetMessage msg)
    {
        if (_network.IsHost) return;
        var action = msg.GetPayload<PlannedAction>();
        if (action != null)
        {
            OnActionResolving?.Invoke(action);
        }
    }

    // ─────── Queries ───────

    public bool HasPlayerSubmitted(int peerId) => _submittedPlans.ContainsKey(peerId);
    public int SubmittedCount => _submittedPlans.Count;
    public int TotalActivePlayers => _activePlayers.Count;
    public IReadOnlyList<PlannedAction> GetResolvedActions() => _resolvedActions;
}
