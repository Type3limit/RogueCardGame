namespace RogueCardGame.Core.Combat.Actions;

/// <summary>
/// Base class for all combat actions, modeled after STS's AbstractGameAction.
/// Actions are queued in ActionManager and executed one at a time.
/// During execution, an action can enqueue more actions (cascading effects).
/// </summary>
public abstract class GameAction
{
    /// <summary>
    /// Duration categories for timing animation coordination.
    /// </summary>
    public enum ActionType
    {
        Instant,    // Resolves immediately (damage calc, status apply)
        Fast,       // Short delay (card discard)
        Medium,     // Medium delay (attack animation)
        Slow        // Long delay (boss special)
    }

    public ActionType Duration { get; protected set; } = ActionType.Instant;

    /// <summary>True after Execute completes.</summary>
    public bool IsDone { get; set; }

    /// <summary>Reference to the manager running this action (set by ActionManager).</summary>
    public ActionManager? Manager { get; internal set; }

    /// <summary>
    /// Execute this action. Implementations should set IsDone = true when finished.
    /// May call Manager.AddToTop/AddToBottom to enqueue follow-up actions.
    /// </summary>
    public abstract void Execute();

    /// <summary>Convenience: add an action to execute NEXT (stack-like, interrupting).</summary>
    protected void AddToTop(GameAction action) => Manager?.AddToTop(action);

    /// <summary>Convenience: add an action to execute AFTER all currently queued actions.</summary>
    protected void AddToBottom(GameAction action) => Manager?.AddToBottom(action);
}
