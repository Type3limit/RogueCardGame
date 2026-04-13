using RogueCardGame.Core.Characters;

namespace RogueCardGame.Core.Combat.Actions;

/// <summary>
/// Manages a queue of GameActions, processing them one at a time.
/// Modeled after STS's GameActionManager.
/// Supports AddToTop (interrupt/stack) and AddToBottom (enqueue).
/// </summary>
public class ActionManager
{
    private readonly LinkedList<GameAction> _queue = new();

    /// <summary>True when there are no actions pending or executing.</summary>
    public bool IsEmpty => _queue.Count == 0;

    /// <summary>The action currently being processed (front of queue).</summary>
    public GameAction? CurrentAction => _queue.First?.Value;

    /// <summary>Reference to the combat manager for context.</summary>
    public CombatManager? Combat { get; set; }

    /// <summary>
    /// Add an action to execute immediately after the current one finishes (interrupt).
    /// If called during another action's Execute(), this new action runs next.
    /// </summary>
    public void AddToTop(GameAction action)
    {
        action.Manager = this;
        if (_queue.Count > 0)
            _queue.AddAfter(_queue.First!, action);
        else
            _queue.AddFirst(action);
    }

    /// <summary>
    /// Add an action to the end of the queue.
    /// </summary>
    public void AddToBottom(GameAction action)
    {
        action.Manager = this;
        _queue.AddLast(action);
    }

    /// <summary>
    /// Process all queued actions until the queue is empty.
    /// This is a synchronous loop — call once per game "tick" to drain the queue.
    /// Actions may enqueue more actions during execution (cascading).
    /// </summary>
    public void ProcessAll()
    {
        int safetyCounter = 0;
        const int maxIterations = 1000; // prevent infinite loops

        while (_queue.Count > 0 && safetyCounter < maxIterations)
        {
            safetyCounter++;
            var action = _queue.First!.Value;

            if (!action.IsDone)
            {
                action.Execute();
                action.IsDone = true;
            }

            _queue.RemoveFirst();

            // After each action, check if combat ended
            if (Combat != null && !Combat.IsActive)
            {
                _queue.Clear();
                break;
            }
        }

        if (safetyCounter >= maxIterations)
            Godot.GD.PrintErr("[ActionManager] Hit safety limit — possible infinite loop in action queue");
    }

    /// <summary>
    /// Process a single action from the front of the queue.
    /// Returns true if an action was processed, false if queue is empty.
    /// Useful for step-by-step animation playback.
    /// </summary>
    public bool ProcessNext()
    {
        if (_queue.Count == 0) return false;

        var action = _queue.First!.Value;
        if (!action.IsDone)
        {
            action.Execute();
            action.IsDone = true;
        }

        _queue.RemoveFirst();
        return true;
    }

    /// <summary>Clear all pending actions.</summary>
    public void Clear() => _queue.Clear();

    /// <summary>Number of actions still in queue.</summary>
    public int Count => _queue.Count;
}
