namespace RogueCardGame.Core;

/// <summary>
/// Simple type-safe event bus for decoupling game systems.
/// Core logic can fire events without knowing about Godot UI.
/// </summary>
public class EventBus
{
    private static readonly Lazy<EventBus> _instance = new(() => new EventBus());
    public static EventBus Instance => _instance.Value;

    private readonly Dictionary<Type, List<Delegate>> _handlers = new();

    public void Subscribe<T>(Action<T> handler)
    {
        var type = typeof(T);
        if (!_handlers.ContainsKey(type))
            _handlers[type] = [];
        _handlers[type].Add(handler);
    }

    public void Unsubscribe<T>(Action<T> handler)
    {
        var type = typeof(T);
        if (_handlers.TryGetValue(type, out var list))
            list.Remove(handler);
    }

    public void Publish<T>(T eventData)
    {
        var type = typeof(T);
        if (_handlers.TryGetValue(type, out var list))
        {
            foreach (var handler in list.ToList())
            {
                ((Action<T>)handler)(eventData);
            }
        }
    }

    public void Clear()
    {
        _handlers.Clear();
    }
}

// ──────────── Game Events ────────────

public record CardPlayedEvent(int PlayerId, string CardId, int[]? TargetIds);
public record CardDrawnEvent(int PlayerId, string CardId);
public record DamageDealtEvent(int SourceId, int TargetId, int Amount);
public record BlockGainedEvent(int CombatantId, int Amount);
public record StatusAppliedEvent(int TargetId, string StatusType, int Stacks);
public record TurnStartedEvent(int TurnNumber);
public record TurnEndedEvent(int TurnNumber);
public record PhaseChangedEvent(string Phase);
public record FormationChangedEvent(int CombatantId, string NewRow);
public record EnemyIntentRevealedEvent(int EnemyId, string IntentType, int Value);
public record CombatEndedEvent(bool Victory);
public record PlayerDownedEvent(int PlayerId);
public record PlayerRevivedEvent(int PlayerId);
