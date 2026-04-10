using RogueCardGame.Core.Characters;

namespace RogueCardGame.Core.Combat;

/// <summary>
/// Simple enemy AI that selects intents based on weighted patterns.
/// Foundation for the Adaptive AI system (Phase 4).
/// </summary>
public class EnemyAI
{
    private readonly Deck.SeededRandom _random;

    public EnemyAI(Deck.SeededRandom random)
    {
        _random = random;
    }

    /// <summary>
    /// Select the next intent for an enemy based on its patterns and weights.
    /// </summary>
    public EnemyIntent SelectIntent(Enemy enemy, CombatState state)
    {
        var patterns = enemy.Data.IntentPatterns;
        if (patterns.Count == 0)
        {
            return new EnemyIntent
            {
                Type = EnemyIntentType.Attack,
                Value = 6,
                Scope = TargetScope.SingleFront,
                Description = "攻击"
            };
        }

        // Weighted random selection
        float totalWeight = patterns.Sum(p => p.Weight);
        float roll = (float)(_random.NextDouble() * totalWeight);
        float cumulative = 0;

        EnemyIntentPattern? selected = null;
        foreach (var pattern in patterns)
        {
            cumulative += pattern.Weight;
            if (roll <= cumulative)
            {
                selected = pattern;
                break;
            }
        }

        selected ??= patterns[^1];

        return new EnemyIntent
        {
            Type = selected.Type,
            Value = selected.Value,
            HitCount = selected.HitCount,
            Scope = selected.Scope,
            Description = selected.Description ?? GetDefaultDescription(selected)
        };
    }

    /// <summary>
    /// Execute an enemy's current intent against players.
    /// </summary>
    public void ExecuteIntent(
        Enemy enemy,
        EnemyIntent intent,
        TargetingSystem targeting,
        List<PlayerCharacter> players,
        FormationSystem formation,
        AggroSystem aggro)
    {
        switch (intent.Type)
        {
            case EnemyIntentType.Attack:
                ExecuteAttack(enemy, intent, targeting, players, formation, aggro);
                break;

            case EnemyIntentType.AttackDefend:
                ExecuteAttack(enemy, intent, targeting, players, formation, aggro);
                enemy.GainBlock(intent.Value / 2);
                break;

            case EnemyIntentType.Defend:
                enemy.GainBlock(intent.Value);
                break;

            case EnemyIntentType.Buff:
                enemy.StatusEffects.Apply(StatusType.Strength, intent.Value);
                break;

            case EnemyIntentType.Debuff:
                ExecuteDebuff(enemy, intent, targeting, players);
                break;

            case EnemyIntentType.Heal:
                enemy.Heal(intent.Value);
                break;
        }
    }

    private void ExecuteAttack(
        Enemy enemy,
        EnemyIntent intent,
        TargetingSystem targeting,
        List<PlayerCharacter> players,
        FormationSystem formation,
        AggroSystem aggro)
    {
        switch (intent.Scope)
        {
            case TargetScope.All:
                foreach (var p in players.Where(p => p.IsAlive))
                {
                    for (int i = 0; i < intent.HitCount; i++)
                        p.TakeDamage(enemy.CalculateAttackDamage(intent.Value));
                }
                break;

            case TargetScope.AllFront:
                foreach (var p in players.Where(p =>
                    p.IsAlive && formation.GetPosition(p.Id) == FormationRow.Front))
                {
                    for (int i = 0; i < intent.HitCount; i++)
                        p.TakeDamage(enemy.CalculateAttackDamage(intent.Value));
                }
                break;

            case TargetScope.AllBack:
                foreach (var p in players.Where(p =>
                    p.IsAlive && formation.GetPosition(p.Id) == FormationRow.Back))
                {
                    for (int i = 0; i < intent.HitCount; i++)
                        p.TakeDamage(enemy.CalculateAttackDamage(intent.Value));
                }
                break;

            default:
                var target = targeting.AutoSelectTarget(
                    intent.Scope,
                    players.Where(p => p.IsAlive),
                    id => players.Any(p => p.Id == id && p.StatusEffects.Has(StatusType.Taunt)));

                if (target != null)
                {
                    for (int i = 0; i < intent.HitCount; i++)
                        target.TakeDamage(enemy.CalculateAttackDamage(intent.Value));
                }
                break;
        }
    }

    private void ExecuteDebuff(
        Enemy enemy,
        EnemyIntent intent,
        TargetingSystem targeting,
        List<PlayerCharacter> players)
    {
        var target = targeting.AutoSelectTarget(intent.Scope, players.Where(p => p.IsAlive));
        if (target != null)
        {
            target.StatusEffects.Apply(StatusType.Weak, intent.Value);
        }
    }

    private static string GetDefaultDescription(EnemyIntentPattern pattern)
    {
        return pattern.Type switch
        {
            EnemyIntentType.Attack => $"攻击 {pattern.Value}" +
                (pattern.HitCount > 1 ? $"×{pattern.HitCount}" : ""),
            EnemyIntentType.Defend => $"防御 {pattern.Value}",
            EnemyIntentType.Buff => "增益",
            EnemyIntentType.Debuff => "减益",
            EnemyIntentType.Heal => $"治疗 {pattern.Value}",
            _ => "未知"
        };
    }
}

/// <summary>
/// Snapshot of current combat state for AI decision-making.
/// </summary>
public class CombatState
{
    public int TurnNumber { get; init; }
    public List<PlayerCharacter> Players { get; init; } = [];
    public List<Enemy> Enemies { get; init; } = [];
    public FormationSystem Formation { get; init; } = new();
}
