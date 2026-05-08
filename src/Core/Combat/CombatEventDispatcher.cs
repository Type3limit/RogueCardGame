using RogueCardGame.Core.Cards;
using RogueCardGame.Core.Characters;

namespace RogueCardGame.Core.Combat;

/// <summary>
/// Canonical set of combat events that Behaviors / Powers / Implants can subscribe to.
/// The dispatcher is a single fan-out point so every trigger has a consistent
/// ordering contract: power priority → implant priority → environment.
/// </summary>
public enum TriggerKind
{
    AtCombatStart,
    AtCombatEnd,
    AtTurnStart,
    AtTurnEnd,
    OnCardPlayed,
    OnCardDrawn,
    OnCardDiscarded,
    OnDealDamage,
    OnTakeDamage,
    OnKill,
    OnDeath,
    OnHeal,
    OnOverchargeConsumed,
    OnResonanceConsumed,
    OnImplantEquipped,
    OnRowSwitched,
    OnStatusApplied,
}

/// <summary>
/// Event envelope passed to combat trigger handlers. Payload is an opaque object
/// that handlers downcast when they know a specific event shape.
/// Keeping a single generic envelope avoids 20 overload signatures.
/// </summary>
public sealed class CombatEvent
{
    public required TriggerKind Kind { get; init; }
    public Combatant? Actor { get; init; }
    public Combatant? Target { get; init; }
    public Card? Card { get; init; }
    public int Amount { get; init; }
    public object? Extra { get; init; }
}

/// <summary>
/// Thin process-wide dispatcher that scopes handlers to a single CombatManager.
/// PowerManager still owns per-combatant hook dispatch; this dispatcher layers
/// over-the-top hooks used by Implant-origin Behaviors, environment rules, and
/// future scripted content that need combat-scope events rather than per-combatant.
/// </summary>
public sealed class CombatEventDispatcher
{
    public delegate void Handler(CombatEvent evt);

    private readonly Dictionary<TriggerKind, List<(int Priority, Handler Handler)>> _handlers = new();

    public void Subscribe(TriggerKind kind, Handler handler, int priority = 0)
    {
        if (!_handlers.TryGetValue(kind, out var list))
        {
            list = new List<(int, Handler)>();
            _handlers[kind] = list;
        }
        list.Add((priority, handler));
        list.Sort((a, b) => a.Priority.CompareTo(b.Priority));
    }

    public void Unsubscribe(TriggerKind kind, Handler handler)
    {
        if (!_handlers.TryGetValue(kind, out var list)) return;
        list.RemoveAll(h => h.Handler == handler);
    }

    public void Publish(CombatEvent evt)
    {
        if (!_handlers.TryGetValue(evt.Kind, out var list)) return;
        // Snapshot so handlers can unsubscribe during iteration safely
        foreach (var (_, handler) in list.ToArray())
            handler(evt);
    }

    public void Clear() => _handlers.Clear();
}
