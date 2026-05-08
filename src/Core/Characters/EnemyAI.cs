using RogueCardGame.Core.Characters;

namespace RogueCardGame.Core.Combat;

/// <summary>
/// Enemy AI that selects intents from weighted phase patterns and executes them.
/// Supports position keywords: lock (applies Rooted) and breakCover (back-row splash).
/// </summary>
public class EnemyAI
{
    private readonly Deck.SeededRandom _random;

    public EnemyAI(Deck.SeededRandom random)
    {
        _random = random;
    }

    /// <summary>
    /// Select the next intent for an enemy based on its current phase patterns and weights.
    /// </summary>
    public EnemyIntent SelectIntent(Enemy enemy, CombatState state)
    {
        var patterns = enemy.GetActivePatterns();
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
            StatusId = selected.StatusId,
            Keywords = selected.Keywords,
            SummonId = selected.SummonId,
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
        List<Enemy> enemies,
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
                ExecuteBuff(enemy, intent, enemies);
                break;

            case EnemyIntentType.Debuff:
                ExecuteDebuff(enemy, intent, targeting, players, formation);
                break;

            case EnemyIntentType.Heal:
                ExecuteHeal(enemy, intent, enemies);
                break;

            case EnemyIntentType.Summon:
                // Summon is handled at CombatManager level via EnemyFactory.
                // EnemyAI signals the intent; CombatManager calls the factory and AddEnemy.
                break;

            case EnemyIntentType.Disabled:
                // Melee enemy in back row — wastes turn, does nothing
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
        bool hasLock = intent.Keywords.Contains("lock", StringComparer.OrdinalIgnoreCase);
        bool hasBreakCover = intent.Keywords.Contains("breakCover", StringComparer.OrdinalIgnoreCase);

        switch (intent.Scope)
        {
            case TargetScope.All:
                foreach (var p in players.Where(p => p.IsAlive))
                {
                    for (int i = 0; i < intent.HitCount; i++)
                        p.TakeDamage(enemy.CalculateAttackDamage(intent.Value));
                    if (hasLock) p.StatusEffects.Apply(StatusType.Rooted, 1, 1);
                }
                break;

            case TargetScope.AllFront:
            {
                var frontPlayers = players
                    .Where(p => p.IsAlive && formation.GetPosition(p.Id) == FormationRow.Front)
                    .ToList();
                foreach (var p in frontPlayers)
                {
                    for (int i = 0; i < intent.HitCount; i++)
                        p.TakeDamage(enemy.CalculateAttackDamage(intent.Value));
                    if (hasLock) p.StatusEffects.Apply(StatusType.Rooted, 1, 1);
                }
                // BreakCover: splash half damage to back row
                if (hasBreakCover && frontPlayers.Count > 0)
                {
                    int splashDamage = Math.Max(1, enemy.CalculateAttackDamage(intent.Value) / 2);
                    foreach (var p in players.Where(p =>
                        p.IsAlive && formation.GetPosition(p.Id) == FormationRow.Back))
                    {
                        p.TakeDamage(splashDamage);
                    }
                }
                break;
            }

            case TargetScope.AllBack:
                foreach (var p in players.Where(p =>
                    p.IsAlive && formation.GetPosition(p.Id) == FormationRow.Back))
                {
                    for (int i = 0; i < intent.HitCount; i++)
                        p.TakeDamage(enemy.CalculateAttackDamage(intent.Value));
                    if (hasLock) p.StatusEffects.Apply(StatusType.Rooted, 1, 1);
                }
                break;

            default:
            {
                var target = targeting.AutoSelectTarget(
                    intent.Scope,
                    players.Where(p => p.IsAlive),
                    id => players.Any(p => p.Id == id && p.StatusEffects.Has(StatusType.Taunt)));

                if (target != null)
                {
                    for (int i = 0; i < intent.HitCount; i++)
                        target.TakeDamage(enemy.CalculateAttackDamage(intent.Value));
                    if (hasLock)
                        target.StatusEffects.Apply(StatusType.Rooted, 1, 1);

                    // BreakCover: splash to back row when hitting front row
                    if (hasBreakCover && formation.GetPosition(target.Id) == FormationRow.Front)
                    {
                        int splashDamage = Math.Max(1, enemy.CalculateAttackDamage(intent.Value) / 2);
                        foreach (var p in players.Where(p =>
                            p.IsAlive &&
                            p.Id != target.Id &&
                            formation.GetPosition(p.Id) == FormationRow.Back))
                        {
                            p.TakeDamage(splashDamage);
                        }
                    }
                }
                break;
            }
        }
    }

    private void ExecuteDebuff(
        Enemy enemy,
        EnemyIntent intent,
        TargetingSystem targeting,
        List<PlayerCharacter> players,
        FormationSystem formation)
    {
        var debuff = ResolveStatusType(intent.StatusId, StatusType.Weak);

        IEnumerable<PlayerCharacter> targets = intent.Scope switch
        {
            TargetScope.All => players.Where(p => p.IsAlive),
            TargetScope.AllFront => players.Where(p => p.IsAlive && formation.GetPosition(p.Id) == FormationRow.Front),
            TargetScope.AllBack => players.Where(p => p.IsAlive && formation.GetPosition(p.Id) == FormationRow.Back),
            _ => ResolveSingleTarget(targeting, intent.Scope, players).Take(1)
        };

        foreach (var target in targets)
            target.StatusEffects.Apply(debuff, intent.Value, 1);
    }

    private static void ExecuteBuff(Enemy enemy, EnemyIntent intent, List<Enemy> enemies)
    {
        var buff = ResolveStatusType(intent.StatusId, StatusType.Strength);
        IEnumerable<Enemy> targets = intent.Scope == TargetScope.AllEnemies
            ? enemies.Where(e => e.IsAlive)
            : [enemy];

        foreach (var target in targets)
            target.StatusEffects.Apply(buff, intent.Value);
    }

    private static void ExecuteHeal(Enemy enemy, EnemyIntent intent, List<Enemy> enemies)
    {
        IEnumerable<Enemy> targets = intent.Scope == TargetScope.AllEnemies
            ? enemies.Where(e => e.IsAlive)
            : [enemy];

        foreach (var target in targets)
            target.Heal(intent.Value);
    }

    private static IEnumerable<PlayerCharacter> ResolveSingleTarget(
        TargetingSystem targeting,
        TargetScope scope,
        List<PlayerCharacter> players)
    {
        var target = targeting.AutoSelectTarget(scope, players.Where(p => p.IsAlive));
        return target is PlayerCharacter player ? [player] : [];
    }

    private static StatusType ResolveStatusType(string? statusId, StatusType fallback)
    {
        if (!string.IsNullOrWhiteSpace(statusId)
            && Enum.TryParse<StatusType>(statusId, true, out var parsed))
            return parsed;

        return fallback;
    }

    private static string GetDefaultDescription(EnemyIntentPattern pattern)
    {
        string keywords = pattern.Keywords.Count > 0
            ? $" [{string.Join(",", pattern.Keywords)}]"
            : "";
        return pattern.Type switch
        {
            EnemyIntentType.Attack => $"攻击 {pattern.Value}" +
                (pattern.HitCount > 1 ? $"×{pattern.HitCount}" : "") + keywords,
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
