namespace RogueCardGame.Core.Characters;

/// <summary>
/// Status effect types that can be applied to combatants.
/// </summary>
public enum StatusType
{
    // Debuffs
    Vulnerable,   // Take 50% more damage from attacks
    Weak,         // Deal 25% less attack damage
    Frail,        // Gain 25% less block
    Poison,       // Take X damage at end of turn, reduce by 1
    HackProgress, // Invasion progress (Netrunner mechanic)

    // Buffs
    Strength,     // Deal X more attack damage
    Dexterity,    // Gain X more block
    Taunt,        // Force enemies to target this combatant
    Regeneration, // Heal X HP at end of turn, reduce by 1

    // Class-specific
    Overcharge,   // Vanguard: stored energy for burst
    Resonance,    // Psion: skill chain amplifier
    Firewall,     // Damage reduction shield (from Netrunner)
    SystemChaos,  // Applied after hack wears off: -ATK

    // Positional
    Rooted,       // Cannot switch rows
    Stealth,      // Cannot be targeted by single-target attacks
}

/// <summary>
/// A status effect instance on a combatant.
/// </summary>
public class StatusEffect
{
    public StatusType Type { get; }
    public int Stacks { get; set; }
    public int Duration { get; set; } // -1 = permanent until cleared
    public bool IsDebuff { get; }

    public StatusEffect(StatusType type, int stacks, int duration = -1)
    {
        Type = type;
        Stacks = stacks;
        Duration = duration;
        IsDebuff = type switch
        {
            StatusType.Vulnerable or StatusType.Weak or StatusType.Frail
                or StatusType.Poison or StatusType.HackProgress
                or StatusType.SystemChaos or StatusType.Rooted => true,
            _ => false
        };
    }

    /// <summary>
    /// Tick at end of turn. Returns true if the effect should be removed.
    /// </summary>
    public bool Tick()
    {
        if (Duration > 0)
        {
            Duration--;
            if (Duration == 0) return true;
        }

        // Poison and Regeneration reduce stacks each turn
        if (Type is StatusType.Poison or StatusType.Regeneration)
        {
            Stacks--;
            if (Stacks <= 0) return true;
        }

        return false;
    }
}

/// <summary>
/// Manages status effects for a single combatant.
/// </summary>
public class StatusEffectManager
{
    private readonly List<StatusEffect> _effects = [];

    public IReadOnlyList<StatusEffect> Effects => _effects;

    public void Apply(StatusType type, int stacks, int duration = -1)
    {
        var existing = _effects.FirstOrDefault(e => e.Type == type);
        if (existing != null)
        {
            existing.Stacks += stacks;
            if (duration > existing.Duration)
                existing.Duration = duration;
        }
        else
        {
            _effects.Add(new StatusEffect(type, stacks, duration));
        }
    }

    public int GetStacks(StatusType type)
    {
        return _effects.FirstOrDefault(e => e.Type == type)?.Stacks ?? 0;
    }

    public bool Has(StatusType type)
    {
        return _effects.Any(e => e.Type == type && e.Stacks > 0);
    }

    public void Remove(StatusType type)
    {
        _effects.RemoveAll(e => e.Type == type);
    }

    public void ConsumeStacks(StatusType type, int amount)
    {
        var effect = _effects.FirstOrDefault(e => e.Type == type);
        if (effect == null) return;

        effect.Stacks -= amount;
        if (effect.Stacks <= 0)
            _effects.Remove(effect);
    }

    /// <summary>
    /// Tick all effects. Returns list of expired effects.
    /// </summary>
    public List<StatusEffect> TickAll()
    {
        var expired = _effects.Where(e => e.Tick()).ToList();
        _effects.RemoveAll(e => expired.Contains(e));
        return expired;
    }

    public void Clear()
    {
        _effects.Clear();
    }
}
