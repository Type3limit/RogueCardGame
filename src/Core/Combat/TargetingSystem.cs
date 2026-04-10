using RogueCardGame.Core.Cards;
using RogueCardGame.Core.Characters;

namespace RogueCardGame.Core.Combat;

/// <summary>
/// Handles target selection for card plays, respecting formation and aggro rules.
/// </summary>
public class TargetingSystem
{
    private readonly FormationSystem _formation;
    private readonly AggroSystem _aggro;

    public TargetingSystem(FormationSystem formation, AggroSystem aggro)
    {
        _formation = formation;
        _aggro = aggro;
    }

    /// <summary>
    /// Get valid targets for a card played by the source combatant.
    /// </summary>
    public List<Combatant> GetValidTargets(
        Combatant source,
        TargetType targetType,
        CardRange range,
        IEnumerable<Combatant> enemies,
        IEnumerable<Combatant> allies)
    {
        return targetType switch
        {
            TargetType.SingleEnemy => GetSingleEnemyTargets(source, range, enemies),
            TargetType.AllEnemies => enemies.Where(e => e.IsAlive).ToList(),
            TargetType.FrontRowEnemies => enemies
                .Where(e => e.IsAlive && _formation.GetPosition(e.Id) == FormationRow.Front)
                .ToList(),
            TargetType.BackRowEnemies => enemies
                .Where(e => e.IsAlive && _formation.GetPosition(e.Id) == FormationRow.Back)
                .ToList(),
            TargetType.Self => [source],
            TargetType.SingleAlly => allies.Where(a => a.IsAlive && a.Id != source.Id).ToList(),
            TargetType.AllAllies => allies.Where(a => a.IsAlive).ToList(),
            TargetType.All => enemies.Concat(allies).Where(c => c.IsAlive).ToList(),
            _ => []
        };
    }

    /// <summary>
    /// Get valid single-enemy targets, respecting formation cover.
    /// Melee attacks from front row can hit any enemy.
    /// Melee attacks from back row still target any enemy but with penalty.
    /// Ranged attacks can always target any enemy.
    /// However, back-row enemies are "covered" by front-row enemies unless using penetrating attacks.
    /// </summary>
    private List<Combatant> GetSingleEnemyTargets(
        Combatant source,
        CardRange range,
        IEnumerable<Combatant> enemies)
    {
        var alive = enemies.Where(e => e.IsAlive).ToList();
        if (alive.Count == 0) return [];

        var enemyIds = alive.Select(e => e.Id).ToList();

        // Ranged attacks with "Penetrate" keyword could target anyone
        // For now, default behavior: can only target back-row enemies
        // if no front-row enemies remain
        bool hasFrontRow = alive.Any(e => _formation.GetPosition(e.Id) == FormationRow.Front);

        if (range == CardRange.Melee && hasFrontRow)
        {
            // Melee can only hit front row if enemies have front-row units
            return alive
                .Where(e => _formation.GetPosition(e.Id) == FormationRow.Front)
                .ToList();
        }

        // Ranged or no front row: can target any living enemy
        return alive;
    }

    /// <summary>
    /// Auto-select the best target based on aggro (for enemy AI).
    /// </summary>
    public Combatant? AutoSelectTarget(
        TargetScope scope,
        IEnumerable<Combatant> players,
        Func<int, bool>? hasTaunt = null)
    {
        var alive = players.Where(p => p.IsAlive).ToList();
        if (alive.Count == 0) return null;

        // Check taunt first
        if (hasTaunt != null)
        {
            var tauntTarget = _aggro.GetTauntTarget(alive.Select(p => p.Id), hasTaunt);
            if (tauntTarget.HasValue)
                return alive.First(p => p.Id == tauntTarget.Value);
        }

        return scope switch
        {
            TargetScope.SingleFront => SelectFromRow(alive, FormationRow.Front),
            TargetScope.SingleBack => SelectFromRow(alive, FormationRow.Back),
            TargetScope.SingleAny => alive
                .OrderByDescending(p => _aggro.GetAggro(p.Id))
                .First(),
            _ => alive.First()
        };
    }

    private Combatant SelectFromRow(List<Combatant> candidates, FormationRow preferredRow)
    {
        var inRow = candidates
            .Where(c => _formation.GetPosition(c.Id) == preferredRow)
            .ToList();

        var pool = inRow.Count > 0 ? inRow : candidates;
        var targetId = _aggro.SelectTarget(
            pool.Select(c => c.Id), _formation);
        return pool.First(c => c.Id == targetId);
    }
}
