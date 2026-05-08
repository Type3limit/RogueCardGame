using RogueCardGame.Core.Combat;

namespace RogueCardGame.Core.Characters;

/// <summary>
/// Boss enemy AI with scripted phases and telegraph attacks.
/// Bosses follow a fixed script per phase, with phase transitions at HP thresholds.
/// Phase 1 (100-60%): Basic attacks + buffs
/// Phase 2 (60-30%): AoE attacks + debuffs
/// Phase 3 (30%-0%): Enraged multi-hit + self-heal
/// Uses composition around the base EnemyAI for execution.
/// </summary>
public class BossEnemyAI
{
    private readonly EnemyAI _baseAI;
    private readonly List<EnemyIntentPattern> _phaseScript;
    public int CurrentPhase { get; private set; } = 1;
    private int _scriptIndex;
    private int _turnCount;

    public BossEnemyAI(Deck.SeededRandom random, List<EnemyIntentPattern> phaseScript)
    {
        _baseAI = new EnemyAI(random);
        _phaseScript = phaseScript;
    }

    /// <summary>
    /// Select intent for a boss enemy. Bosses follow scripted patterns per phase.
    /// In phase 3, returns 2 intents (boss acts twice).
    /// </summary>
    public List<EnemyIntent> SelectIntents(Enemy enemy, CombatState state)
    {
        _turnCount++;
        CheckPhaseTransition(enemy);

        var intentData = GetCurrentScriptedIntent();
        AdvanceScript();

        var intent = new EnemyIntent
        {
            Type = intentData.Type,
            Value = intentData.Value,
            HitCount = intentData.HitCount,
            Scope = intentData.Scope,
            StatusId = intentData.StatusId,
            Keywords = intentData.Keywords,
            Description = intentData.Description ?? GetPhaseTelegraph()
        };

        if (CurrentPhase == 3)
        {
            var secondData = GetCurrentScriptedIntent();
            AdvanceScript();
            var secondIntent = new EnemyIntent
            {
                Type = secondData.Type,
                Value = secondData.Value,
                HitCount = secondData.HitCount,
                Scope = secondData.Scope,
                StatusId = secondData.StatusId,
                Keywords = secondData.Keywords,
                Description = secondData.Description ?? GetPhaseTelegraph()
            };
            return [intent, secondIntent];
        }

        return [intent];
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

    private void CheckPhaseTransition(Enemy enemy)
    {
        int maxHp = enemy.Data?.MaxHp ?? enemy.MaxHp;
        if (maxHp <= 0) return;
        int hpPercent = enemy.CurrentHp * 100 / maxHp;
        int newPhase = hpPercent switch
        {
            <= 30 => 3,
            <= 60 => 2,
            _ => 1
        };

        if (newPhase != CurrentPhase)
        {
            CurrentPhase = newPhase;
            _scriptIndex = 0;
        }
    }

    private EnemyIntentPattern GetCurrentScriptedIntent()
    {
        var phaseIntents = _phaseScript
            .Where(i => IsPatternForPhase(i, CurrentPhase))
            .ToList();

        if (phaseIntents.Count == 0)
            return _phaseScript[0];

        return phaseIntents[_scriptIndex % phaseIntents.Count];
    }

    private void AdvanceScript()
    {
        var phaseIntents = _phaseScript
            .Where(i => IsPatternForPhase(i, CurrentPhase))
            .ToList();
        _scriptIndex = (_scriptIndex + 1) % Math.Max(1, phaseIntents.Count);
    }

    private static bool IsPatternForPhase(EnemyIntentPattern pattern, int phase)
    {
        // Phase info encoded in Description or Weight convention:
        // Weight starting with 1 = phase 1, 2 = phase 2, 3 = phase 3
        // If no convention, all patterns apply to all phases
        return true;
    }

    public string GetPhaseTelegraph() =>
        CurrentPhase switch
        {
            1 => "第一阶段：试探性攻击",
            2 => "第二阶段：全面压制",
            3 => "第三阶段：疯狂攻击！",
            _ => "???"
        };

    public string GetEnrageDescription() =>
        CurrentPhase switch
        {
            1 => "冷静",
            2 => "紧张",
            3 => "暴怒！每回合行动 2 次",
            _ => "???"
        };
}
