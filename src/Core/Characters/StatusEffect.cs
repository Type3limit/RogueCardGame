using RogueCardGame.Core.Combat.Powers;

namespace RogueCardGame.Core.Characters;

/// <summary>
/// Status effect types that can be applied to combatants.
/// Kept for backward compatibility — new code should use PowerManager directly.
/// </summary>
public enum StatusType
{
    // Debuffs
    Vulnerable,   // Take 50% more damage from attacks
    Weak,         // Deal 25% less attack damage
    Frail,        // Gain 25% less block
    Poison,       // Take X damage at end of turn, reduce by 1

    // Buffs
    Strength,     // Deal X more attack damage
    Dexterity,    // Gain X more block
    Taunt,        // Force enemies to target this combatant
    Regeneration, // Heal X HP at end of turn, reduce by 1

    // Class-specific
    Overcharge,   // Vanguard: stored energy for burst
    Resonance,    // Psion: skill chain amplifier
    Firewall,     // Damage reduction shield (from Netrunner)

    // Positional
    Rooted,       // Cannot switch rows
    Stealth,      // Cannot be targeted by single-target attacks

    // Netrunner-specific
    ProtocolStack, // Netrunner: stacked protocol layers
    Hacked,        // Netrunner: hack/intrusion progress on enemy
    Stunned,       // Skip next action

    // Symbiote-specific
    Thorns,        // Deal X damage to attackers
    PainThreshold, // Gain armor equal to HP lost this turn
    ParasiticBond, // Heal % of damage dealt to marked target
}

/// <summary>
/// A status effect instance on a combatant. Kept for backward compatibility.
/// </summary>
public class StatusEffect
{
    public StatusType Type { get; }
    public int Stacks { get; set; }
    public int Duration { get; set; }
    public bool IsDebuff { get; }

    public StatusEffect(StatusType type, int stacks, int duration = -1)
    {
        Type = type;
        Stacks = stacks;
        Duration = duration;
        IsDebuff = type switch
        {
            StatusType.Vulnerable or StatusType.Weak or StatusType.Frail
                or StatusType.Poison
                or StatusType.Rooted => true,
            _ => false
        };
    }

    public bool Tick()
    {
        if (Duration > 0)
        {
            Duration--;
            if (Duration == 0) return true;
        }
        if (Type is StatusType.Poison or StatusType.Regeneration)
        {
            Stacks--;
            if (Stacks <= 0) return true;
        }
        return false;
    }
}

/// <summary>
/// Thin facade over PowerManager for backward compatibility.
/// All operations delegate to the owning Combatant's PowerManager.
/// </summary>
public class StatusEffectManager
{
    private Combatant? _owner;

    /// <summary>Link this manager to a combatant's PowerManager.</summary>
    public void SetOwner(Combatant owner) => _owner = owner;

    /// <summary>Backward-compat: generate StatusEffect list from current Powers.</summary>
    public IReadOnlyList<StatusEffect> Effects
    {
        get
        {
            if (_owner == null) return [];
            var list = new List<StatusEffect>();
            foreach (var p in _owner.Powers.Powers)
            {
                var st = PowerFactory.ToStatusType(p.PowerId);
                if (st.HasValue)
                    list.Add(new StatusEffect(st.Value, p.Amount));
            }
            return list;
        }
    }

    public void Apply(StatusType type, int stacks, int duration = -1)
    {
        if (_owner == null) return;
        var power = PowerFactory.CreateFromStatusType(type, stacks);
        _owner.Powers.ApplyPower(power, _owner);
    }

    public int GetStacks(StatusType type)
    {
        var powerId = PowerFactory.ToPowerId(type);
        return _owner?.Powers.GetStacks(powerId) ?? 0;
    }

    public bool Has(StatusType type)
    {
        var powerId = PowerFactory.ToPowerId(type);
        return _owner?.Powers.HasPower(powerId) ?? false;
    }

    public void Remove(StatusType type)
    {
        var powerId = PowerFactory.ToPowerId(type);
        _owner?.Powers.RemovePower(powerId);
    }

    public void ConsumeStacks(StatusType type, int amount)
    {
        var powerId = PowerFactory.ToPowerId(type);
        _owner?.Powers.ConsumeStacks(powerId, amount);
    }

    /// <summary>No-op: Powers handle their own ticking via TriggerAtTurnEnd.</summary>
    public List<StatusEffect> TickAll() => [];

    public void Clear() => _owner?.Powers.Clear();
}
