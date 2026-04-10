using RogueCardGame.Core.Combat;

namespace RogueCardGame.Core.Characters;

/// <summary>
/// Base class for all combatants (players and enemies).
/// </summary>
public abstract class Combatant
{
    private static int _nextId = 0;

    public int Id { get; }
    public string Name { get; set; }
    public int MaxHp { get; set; }
    public int CurrentHp { get; set; }
    public int Block { get; set; }
    public StatusEffectManager StatusEffects { get; } = new();
    public bool IsAlive => CurrentHp > 0;

    protected Combatant(string name, int maxHp)
    {
        Id = Interlocked.Increment(ref _nextId);
        Name = name;
        MaxHp = maxHp;
        CurrentHp = maxHp;
    }

    /// <summary>
    /// Take damage after all modifiers. Returns actual damage dealt.
    /// </summary>
    public int TakeDamage(int amount)
    {
        if (amount <= 0) return 0;

        // Vulnerable: +50% damage
        if (StatusEffects.Has(StatusType.Vulnerable))
            amount = (int)(amount * 1.5f);

        // Block absorbs damage first
        int blocked = Math.Min(Block, amount);
        Block -= blocked;
        int hpDamage = amount - blocked;

        CurrentHp = Math.Max(0, CurrentHp - hpDamage);
        return hpDamage;
    }

    /// <summary>
    /// Gain block (armor). Applies formation bonus externally.
    /// </summary>
    public void GainBlock(int amount)
    {
        if (amount <= 0) return;

        // Frail: -25% block gain
        if (StatusEffects.Has(StatusType.Frail))
            amount = (int)(amount * 0.75f);

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
    }

    /// <summary>
    /// Called at the end of each turn. Process status effects.
    /// </summary>
    public virtual void OnTurnEnd()
    {
        // Poison damage
        int poison = StatusEffects.GetStacks(StatusType.Poison);
        if (poison > 0)
            TakeDamage(poison);

        // Regeneration
        int regen = StatusEffects.GetStacks(StatusType.Regeneration);
        if (regen > 0)
            Heal(regen);

        // Tick all effects
        StatusEffects.TickAll();
    }

    /// <summary>
    /// Calculate outgoing attack damage with modifiers.
    /// </summary>
    public int CalculateAttackDamage(int baseDamage, FormationSystem? formation = null)
    {
        int damage = baseDamage;

        // Strength bonus
        damage += StatusEffects.GetStacks(StatusType.Strength);

        // Weak: -25%
        if (StatusEffects.Has(StatusType.Weak))
            damage = (int)(damage * 0.75f);

        return Math.Max(0, damage);
    }
}
