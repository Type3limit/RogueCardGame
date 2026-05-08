using RogueCardGame.Core.Combat;

namespace RogueCardGame.Core.Characters;

/// <summary>
/// Elite enemy AI with multi-action turns and phase transitions.
/// Elites attack twice per turn and gain new moves at 50% HP.
/// Uses composition around the base EnemyAI for execution.
/// </summary>
public class EliteEnemyAI
{
    private readonly EnemyAI _baseAI;
    public bool HasEnraged { get; private set; }
    private int _turnCount;

    public EliteEnemyAI(Deck.SeededRandom random)
    {
        _baseAI = new EnemyAI(random);
    }

    /// <summary>
    /// Elite enemies act twice per turn.
    /// At 50% HP, they "enrage" gaining increased damage.
    /// </summary>
    public List<EnemyIntent> SelectIntents(Enemy enemy, CombatState state)
    {
        _turnCount++;
        var intents = new List<EnemyIntent>();

        // Check for enrage phase transition (50% HP)
        if (!HasEnraged && enemy.CurrentHp <= enemy.MaxHp / 2)
        {
            HasEnraged = true;
            // Apply a strength buff to the enemy when enraged
            enemy.StatusEffects?.Apply(StatusType.Strength, 2);
        }

        // Select two intents for this turn
        var first = _baseAI.SelectIntent(enemy, state);
        intents.Add(first);

        var second = _baseAI.SelectIntent(enemy, state);
        intents.Add(second);

        return intents;
    }

    public void ExecuteIntent(
        Enemy enemy,
        EnemyIntent intent,
        TargetingSystem targeting,
        List<PlayerCharacter> players,
        List<Enemy> enemies,
        FormationSystem formation,
        AggroSystem aggro)
    {
        _baseAI.ExecuteIntent(enemy, intent, targeting, players, enemies, formation, aggro);
    }

    public string GetEnrageDescription() =>
        HasEnraged ? "暴怒：每回合行动 2 次，攻击力提升" : "尚未暴怒";
}
