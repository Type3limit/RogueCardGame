using RogueCardGame.Core.Characters;
using RogueCardGame.Core.Combat;
using RogueCardGame.Core.Deck;

namespace RogueCardGame.Core.Environment;

/// <summary>
/// Types of environmental effects on the battlefield.
/// </summary>
public enum EnvironmentType
{
    None,
    EmpField,        // +1 cost to all skill cards
    RadiationZone,   // All combatants take 2 damage at turn end
    GravityAnomaly,  // Row switch costs 1 energy (normally free)
    NanoSwarm,       // All healing reduced by 50%
    DataStorm,       // Draw 1 extra card each turn, discard 1 extra at end
    PlasmaLeak,      // Melee attacks deal +3 damage, ranged deal -2
    ShieldDisruptor, // Block capped at 15 per turn
    OverclockField,  // First card played each turn costs 0
    MagneticFlux     // Front and back row bonuses/penalties doubled
}

/// <summary>
/// A single active environment effect on the battlefield.
/// </summary>
public class EnvironmentEffect
{
    public EnvironmentType Type { get; }
    public string Name { get; }
    public string Description { get; }
    public int Duration { get; set; } // -1 = entire battle
    public int Intensity { get; set; } = 1;

    public EnvironmentEffect(EnvironmentType type, int duration = -1)
    {
        Type = type;
        Duration = duration;
        Name = GetName(type);
        Description = GetDescription(type);
    }

    private static string GetName(EnvironmentType type) => type switch
    {
        EnvironmentType.EmpField => "电磁脉冲场",
        EnvironmentType.RadiationZone => "辐射区域",
        EnvironmentType.GravityAnomaly => "重力异常",
        EnvironmentType.NanoSwarm => "纳米虫群",
        EnvironmentType.DataStorm => "数据风暴",
        EnvironmentType.PlasmaLeak => "等离子泄漏",
        EnvironmentType.ShieldDisruptor => "护盾干扰器",
        EnvironmentType.OverclockField => "超频场",
        EnvironmentType.MagneticFlux => "磁通量异常",
        _ => "无"
    };

    private static string GetDescription(EnvironmentType type) => type switch
    {
        EnvironmentType.EmpField => "所有技能牌费用 +1",
        EnvironmentType.RadiationZone => "每回合结束时所有战斗者受到 2 点伤害",
        EnvironmentType.GravityAnomaly => "换排需要消耗 1 点能量",
        EnvironmentType.NanoSwarm => "所有治疗效果减半",
        EnvironmentType.DataStorm => "每回合多抽 1 张牌，回合结束多弃 1 张",
        EnvironmentType.PlasmaLeak => "近战攻击 +3 伤害，远程攻击 -2 伤害",
        EnvironmentType.ShieldDisruptor => "每回合护甲上限为 15",
        EnvironmentType.OverclockField => "每回合第一张牌费用为 0",
        EnvironmentType.MagneticFlux => "前后排加成/惩罚翻倍",
        _ => ""
    };
}

/// <summary>
/// Manages dynamic battlefield environments for a combat encounter.
/// 0-2 random effects per room.
/// </summary>
public class EnvironmentSystem
{
    private readonly List<EnvironmentEffect> _activeEffects = [];
    private readonly SeededRandom _random;
    private bool _firstCardThisTurn;

    public IReadOnlyList<EnvironmentEffect> ActiveEffects => _activeEffects;

    public event Action<EnvironmentEffect>? OnEffectAdded;
    public event Action<EnvironmentEffect>? OnEffectRemoved;

    public EnvironmentSystem(SeededRandom random)
    {
        _random = random;
    }

    /// <summary>
    /// Roll environment effects for a new room.
    /// </summary>
    public void GenerateForRoom(int difficulty = 1)
    {
        _activeEffects.Clear();

        // 40% chance of 0 effects, 40% of 1, 20% of 2
        float roll = (float)_random.NextDouble();
        int count = roll < 0.4f ? 0 : roll < 0.8f ? 1 : 2;

        var allTypes = Enum.GetValues<EnvironmentType>()
            .Where(t => t != EnvironmentType.None)
            .ToList();

        for (int i = 0; i < count; i++)
        {
            if (allTypes.Count == 0) break;
            int idx = _random.Next(allTypes.Count);
            var type = allTypes[idx];
            allTypes.RemoveAt(idx); // No duplicates

            var effect = new EnvironmentEffect(type);
            _activeEffects.Add(effect);
            OnEffectAdded?.Invoke(effect);
        }
    }

    public bool HasEffect(EnvironmentType type) =>
        _activeEffects.Any(e => e.Type == type);

    /// <summary>
    /// Get card cost modifier from environment effects.
    /// </summary>
    public int GetCostModifier(Cards.CardType cardType, bool isFirstCard)
    {
        int mod = 0;

        if (HasEffect(EnvironmentType.EmpField) && cardType == Cards.CardType.Skill)
            mod += 1;

        if (HasEffect(EnvironmentType.OverclockField) && isFirstCard)
            mod -= 99; // Make it free (clamped to 0 by card system)

        return mod;
    }

    /// <summary>
    /// Get damage modifier from environment effects.
    /// </summary>
    public int GetDamageModifier(Cards.CardRange range)
    {
        if (!HasEffect(EnvironmentType.PlasmaLeak)) return 0;
        return range switch
        {
            Cards.CardRange.Melee => 3,
            Cards.CardRange.Ranged => -2,
            _ => 0
        };
    }

    /// <summary>
    /// Get draw bonus from environment effects.
    /// </summary>
    public int GetDrawBonus() =>
        HasEffect(EnvironmentType.DataStorm) ? 1 : 0;

    /// <summary>
    /// Get block cap (or int.MaxValue if no cap).
    /// </summary>
    public int GetBlockCap() =>
        HasEffect(EnvironmentType.ShieldDisruptor) ? 15 : int.MaxValue;

    /// <summary>
    /// Get healing multiplier (1.0 = normal, 0.5 = NanoSwarm).
    /// </summary>
    public float GetHealingMultiplier() =>
        HasEffect(EnvironmentType.NanoSwarm) ? 0.5f : 1.0f;

    /// <summary>
    /// Get formation multiplier (2.0 if MagneticFlux active).
    /// </summary>
    public float GetFormationMultiplier() =>
        HasEffect(EnvironmentType.MagneticFlux) ? 2.0f : 1.0f;

    /// <summary>
    /// Apply end-of-turn environment effects.
    /// </summary>
    public void ApplyTurnEndEffects(IEnumerable<Combatant> combatants)
    {
        if (HasEffect(EnvironmentType.RadiationZone))
        {
            foreach (var c in combatants.Where(c => c.IsAlive))
                c.TakeDamage(2);
        }

        // Tick durations
        foreach (var effect in _activeEffects.ToList())
        {
            if (effect.Duration > 0)
            {
                effect.Duration--;
                if (effect.Duration == 0)
                {
                    _activeEffects.Remove(effect);
                    OnEffectRemoved?.Invoke(effect);
                }
            }
        }

        _firstCardThisTurn = true;
    }

    public void OnCardPlayed()
    {
        _firstCardThisTurn = false;
    }

    public void OnTurnStart()
    {
        _firstCardThisTurn = true;
    }

    public bool IsFirstCardThisTurn => _firstCardThisTurn;

    public void Clear() => _activeEffects.Clear();
}
