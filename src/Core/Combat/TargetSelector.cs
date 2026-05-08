using RogueCardGame.Core.Characters;

namespace RogueCardGame.Core.Combat;

/// <summary>
/// Per-effect target resolution strategy.
/// Lets an individual CardEffectData override the card-level TargetType
/// (e.g. a whole-card attack whose one inner effect is "heal self").
/// </summary>
public abstract class TargetSelector
{
    public abstract List<Combatant> Select(CardEffectContext context);

    public static TargetSelector Parse(string? spec) => (spec ?? "default").ToLowerInvariant() switch
    {
        "self" => Self,
        "allenemies" or "all_enemies" => AllEnemies,
        "randomenemy" or "random_enemy" => RandomEnemy,
        "allallies" or "all_allies" => AllAllies,
        _ => Default,
    };

    public static readonly TargetSelector Default = new DefaultSelector();
    public static readonly TargetSelector Self = new SelfSelector();
    public static readonly TargetSelector AllEnemies = new AllEnemiesSelector();
    public static readonly TargetSelector AllAllies = new AllAlliesSelector();
    public static readonly TargetSelector RandomEnemy = new RandomEnemySelector();

    private sealed class DefaultSelector : TargetSelector
    {
        public override List<Combatant> Select(CardEffectContext context) => context.AllTargets;
    }

    private sealed class SelfSelector : TargetSelector
    {
        public override List<Combatant> Select(CardEffectContext context) => [context.Source];
    }

    private sealed class AllEnemiesSelector : TargetSelector
    {
        public override List<Combatant> Select(CardEffectContext context)
        {
            var combat = context.Actions?.Combat;
            if (combat == null) return context.AllTargets;
            return combat.Enemies.Where(e => e.IsAlive).Cast<Combatant>().ToList();
        }
    }

    private sealed class AllAlliesSelector : TargetSelector
    {
        public override List<Combatant> Select(CardEffectContext context)
        {
            var combat = context.Actions?.Combat;
            if (combat == null) return [context.Source];
            return combat.Players.Where(p => p.IsAlive).Cast<Combatant>().ToList();
        }
    }

    private sealed class RandomEnemySelector : TargetSelector
    {
        private readonly Random _rng = new();
        public override List<Combatant> Select(CardEffectContext context)
        {
            var combat = context.Actions?.Combat;
            var pool = combat?.Enemies.Where(e => e.IsAlive).ToList() ?? new List<Enemy>();
            if (pool.Count == 0) return [];
            return [pool[_rng.Next(pool.Count)]];
        }
    }
}
