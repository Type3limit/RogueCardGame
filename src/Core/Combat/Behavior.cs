using RogueCardGame.Core.Cards;
using RogueCardGame.Core.Characters;
using RogueCardGame.Core.Combat.Actions;
using RogueCardGame.Core.Deck;

namespace RogueCardGame.Core.Combat;

// =============================================================================
//  BehaviorCtx — execution context threaded through every Behavior
// =============================================================================

/// <summary>
/// Execution context for a Behavior.  Carries all state needed for effect execution
/// and condition evaluation, so every pipeline (Card / Power / Implant / Status)
/// shares the same call signature.
/// </summary>
public sealed class BehaviorCtx
{
    public required Combatant Actor { get; init; }
    public Combatant? Target { get; init; }
    public List<Combatant> AllTargets { get; init; } = [];
    public Card? Card { get; init; }
    public required ActionManager Actions { get; init; }
    /// <summary>The owning CombatManager (null in unit tests that skip the full manager).</summary>
    public CombatManager? Combat { get; init; }
    public DeckManager? Deck { get; init; }
    /// <summary>The CombatEvent that triggered this Behavior (null for card-play contexts).</summary>
    public CombatEvent? TriggeringEvent { get; init; }
}

// =============================================================================
//  Behavior — the unified runtime primitive
// =============================================================================

/// <summary>
/// The single runtime primitive shared by Card effects, Power hooks, Implant
/// effects, and Status effects.  A Card is a list of OnPlayCard Behaviors;
/// a Power is a list of Behaviors subscribed to Dispatcher triggers;
/// an Implant is assembled into a PersistentPower whose Behaviors encode its effects.
/// </summary>
public sealed class Behavior
{
    /// <summary>Which trigger causes this Behavior to fire.</summary>
    public required TriggerKind Trigger { get; init; }

    /// <summary>
    /// Optional guard evaluated against a CardEffectContext; Behavior only fires when true.
    /// Uses <see cref="Expr"/> from Expr.cs — evaluates against CardEffectContext.
    /// </summary>
    public Expr? Condition { get; init; }

    /// <summary>
    /// Optional target override; if non-null, re-resolves targets inside the effect context.
    /// Uses <see cref="TargetSelector"/> from TargetSelector.cs — selects from CardEffectContext.
    /// </summary>
    public TargetSelector? TargetSelector { get; init; }

    /// <summary>The atomic effects to execute.</summary>
    public required IReadOnlyList<ICardEffect> Effects { get; init; }

    /// <summary>Dispatcher subscription priority (lower = earlier).</summary>
    public int Priority { get; init; }
}

// =============================================================================
//  BehaviorExecutor — unified execution engine
// =============================================================================

/// <summary>
/// Executes a <see cref="Behavior"/> (or list thereof) against a <see cref="BehaviorCtx"/>.
/// Card plays, Power hooks, and Implant reactions all flow through here.
/// </summary>
public static class BehaviorExecutor
{
    public static void Execute(Behavior behavior, BehaviorCtx ctx)
    {
        // Build effect context using the default targets from BehaviorCtx
        var defaultTargets = ctx.AllTargets.Count > 0 ? ctx.AllTargets
            : ctx.Target != null ? [ctx.Target] : new List<Combatant>();
        var effectCtx = BuildEffectContext(ctx, defaultTargets);

        // Evaluate condition using the CardEffectContext-based Expr API
        if (behavior.Condition != null && !behavior.Condition.Evaluate(effectCtx)) return;

        // Re-resolve targets if a TargetSelector override is specified
        if (behavior.TargetSelector != null)
        {
            var overrideTargets = behavior.TargetSelector.Select(effectCtx);
            effectCtx = BuildEffectContext(ctx, overrideTargets);
        }

        foreach (var effect in behavior.Effects)
            effect.Execute(effectCtx);
    }

    public static void ExecuteAll(IEnumerable<Behavior> behaviors, BehaviorCtx ctx)
    {
        foreach (var b in behaviors)
            Execute(b, ctx);
    }

    private static CardEffectContext BuildEffectContext(BehaviorCtx ctx, List<Combatant> targets)
    {
        return new CardEffectContext
        {
            Source      = ctx.Actor,
            Target      = targets.FirstOrDefault(),
            AllTargets  = targets,
            Formation   = ctx.Combat?.Formation ?? new FormationSystem(),
            Aggro       = ctx.Combat?.Aggro ?? new AggroSystem(),
            Card        = ctx.Card,
            Actions     = ctx.Actions,
            Deck        = ctx.Deck,
            CardDb      = ctx.Combat?.CardDb,
            ImplantBonusProvider = ctx.Combat?.ImplantBonusProvider,
            AllEnemies = ctx.Combat?.Enemies.Where(e => e.IsAlive).Cast<Combatant>().ToList(),
        };
    }
}

// =============================================================================
//  BehaviorBuilder — wraps CardEffectFactory output in Behavior envelopes
// =============================================================================

/// <summary>
/// Constructs <see cref="Behavior"/> instances from <see cref="CardEffectData"/>.
/// This is the "Behavior builder" replacement for raw CardEffectFactory usage:
/// instead of creating ICardEffect and calling Execute directly, callers build
/// Behaviors and run them through BehaviorExecutor so that all four pipelines
/// (Card / Power / Implant / Status) share one execution path.
/// </summary>
public static class BehaviorBuilder
{
    /// <summary>
    /// Build a single Behavior from one CardEffectData JSON descriptor.
    /// The resulting Behavior has <paramref name="trigger"/> set and wraps
    /// the ICardEffect created by CardEffectFactory.
    /// </summary>
    public static Behavior Build(CardEffectData data, TriggerKind trigger = TriggerKind.OnCardPlayed)
    {
        var effect = CardEffectFactory.Create(data);
        return new Behavior
        {
            Trigger  = trigger,
            Effects  = [effect],
            Priority = 0,
        };
    }

    /// <summary>
    /// Build a list of Behaviors from a list of CardEffectData descriptors.
    /// </summary>
    public static IReadOnlyList<Behavior> BuildAll(
        IEnumerable<CardEffectData> effects,
        TriggerKind trigger = TriggerKind.OnCardPlayed)
    {
        return effects.Select(d => Build(d, trigger)).ToList();
    }

    /// <summary>
    /// Build a BehaviorCtx from a CardEffectContext (bridge from old API).
    /// </summary>
    public static BehaviorCtx ContextFromCardEffect(CardEffectContext effectCtx, CombatManager? combat)
    {
        return new BehaviorCtx
        {
            Actor      = effectCtx.Source,
            Target     = effectCtx.Target,
            AllTargets = effectCtx.AllTargets,
            Card       = effectCtx.Card,
            Actions    = effectCtx.Actions ?? combat?.Actions ?? throw new InvalidOperationException("No ActionManager"),
            Combat     = combat,
            Deck       = effectCtx.Deck,
        };
    }
}
