using RogueCardGame.Core.Combat;
using RogueCardGame.Core.Combat.Powers;

namespace RogueCardGame.Core.Characters;

/// <summary>
/// Base class for all combatants (players and enemies).
/// Powers drive all buff/debuff mechanics via lifecycle hooks (STS-style).
/// StatusEffects is a thin facade over Powers for backward compatibility.
/// </summary>
public abstract class Combatant
{
    private static int _nextId = 0;

    public int Id { get; }
    public string Name { get; set; }
    public int MaxHp { get; set; }
    public int CurrentHp { get; set; }
    public int Block { get; set; }

    /// <summary>New STS-style power system — drives all buff/debuff mechanics.</summary>
    public PowerManager Powers { get; } = new();

    /// <summary>Legacy facade — delegates to PowerManager under the hood.</summary>
    public StatusEffectManager StatusEffects { get; } = new();

    public bool IsAlive => CurrentHp > 0;

    protected Combatant(string name, int maxHp)
    {
        Id = Interlocked.Increment(ref _nextId);
        Name = name;
        MaxHp = maxHp;
        CurrentHp = maxHp;
        StatusEffects.SetOwner(this);
    }

    /// <summary>
    /// Take damage after power modifiers (Vulnerable, Firewall, etc.).
    /// Returns actual HP damage dealt (after block absorption).
    /// </summary>
    public int TakeDamage(int amount)
    {
        if (amount <= 0) return 0;

        // Power modifiers on incoming damage (Vulnerable +50%, Firewall reduction, etc.)
        amount = Math.Max(0, (int)Powers.ModifyDamageTaken(amount));

        // Block absorbs damage first
        int blocked = Math.Min(Block, amount);
        Block -= blocked;
        int hpDamage = amount - blocked;

        CurrentHp = Math.Max(0, CurrentHp - hpDamage);
        return hpDamage;
    }

    /// <summary>
    /// Gain block (armor). Modified by powers (Frail, Dexterity, etc.).
    /// </summary>
    public void GainBlock(int amount)
    {
        if (amount <= 0) return;

        // Power modifiers on block gain (Frail -25%, Dexterity +X, etc.)
        amount = Math.Max(0, (int)Powers.ModifyBlockGain(amount));

        Block += amount;
    }

    /// <summary>
    /// Heal HP. Cannot exceed max HP.
    /// </summary>
    public int Heal(int amount)
    {
        if (amount <= 0) return 0;
        int before = CurrentHp;
        CurrentHp = Math.Min(MaxHp, CurrentHp + amount);
        return CurrentHp - before;
    }

    /// <summary>
    /// Called at the start of each turn.
    /// </summary>
    public virtual void OnTurnStart()
    {
        Block = 0; // Block resets each turn by default
        Powers.TriggerAtTurnStart();
    }

    /// <summary>
    /// Called at the end of each turn. Powers handle poison, regen, tick-down, etc.
    /// </summary>
    public virtual void OnTurnEnd()
    {
        // Powers handle all end-of-turn effects:
        // - PoisonPower deals damage and ticks down
        // - RegenerationPower heals and ticks down
        // - Vulnerable/Weak/Frail tick down
        // - Custom powers can inject their own behavior
        Powers.TriggerAtTurnEnd();
    }

    /// <summary>
    /// Calculate outgoing attack damage with power modifiers (Strength, Weak, etc.).
    /// </summary>
    public int CalculateAttackDamage(int baseDamage, FormationSystem? formation = null)
    {
        float damage = baseDamage;

        // Strength adds flat damage
        damage += Powers.GetStacks(CommonPowerIds.Strength);

        // Multiplicative modifiers (Weak -25%, etc.)
        damage = Powers.ModifyAttackDamage(damage);

        return Math.Max(0, (int)damage);
    }
}
