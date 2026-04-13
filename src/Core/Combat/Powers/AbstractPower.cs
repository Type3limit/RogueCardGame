using RogueCardGame.Core.Cards;
using RogueCardGame.Core.Characters;
using RogueCardGame.Core.Combat.Actions;

namespace RogueCardGame.Core.Combat.Powers;

/// <summary>
/// Base class for all powers (buffs/debuffs), modeled after STS's AbstractPower.
/// Powers live on combatants and have lifecycle hooks that respond to combat events.
/// This replaces the old StatusType enum with rich objects that can contain custom logic.
/// </summary>
public abstract class AbstractPower
{
    /// <summary>Unique string identifier for this power type (e.g., "Vulnerable", "Doom").</summary>
    public abstract string PowerId { get; }

    /// <summary>Display name.</summary>
    public abstract string Name { get; }

    /// <summary>Description template. May reference Amount.</summary>
    public abstract string GetDescription();

    /// <summary>Current stack count / amount.</summary>
    public int Amount { get; set; }

    /// <summary>The combatant this power is attached to.</summary>
    public Combatant? Owner { get; set; }

    /// <summary>Is this a debuff? (affects UI color, purge logic)</summary>
    public virtual bool IsDebuff => false;

    /// <summary>Priority for hook ordering. Lower = earlier. Default 0.</summary>
    public virtual int Priority => 0;

    /// <summary>Should this power reduce stacks each turn? (like Poison)</summary>
    public virtual bool TicksDown => false;

    /// <summary>Reference to action manager for enqueueing actions from hooks.</summary>
    public ActionManager? ActionManager { get; set; }

    // =====================================================================
    //  LIFECYCLE HOOKS — Override in subclasses to add behavior
    // =====================================================================

    /// <summary>Called when this power is first applied to a combatant.</summary>
    public virtual void OnApply() { }

    /// <summary>Called when this power is removed from a combatant.</summary>
    public virtual void OnRemove() { }

    /// <summary>Called when stacks are added to an existing instance.</summary>
    public virtual void OnStacksAdded(int addedAmount) { }

    // --- Turn hooks ---

    /// <summary>Called at the start of the owner's turn.</summary>
    public virtual void AtTurnStart() { }

    /// <summary>Called at the end of the owner's turn (before tick-down).</summary>
    public virtual void AtTurnEnd() { }

    // --- Damage hooks ---

    /// <summary>
    /// Modify outgoing attack damage from the owner. Return modified damage.
    /// Called during damage calculation (like Strength, Weak).
    /// </summary>
    public virtual float ModifyAttackDamage(float baseDamage) => baseDamage;

    /// <summary>
    /// Modify incoming damage to the owner. Return modified damage.
    /// Called before block absorption (like Vulnerable, damage reduction).
    /// </summary>
    public virtual float ModifyDamageTaken(float baseDamage) => baseDamage;

    /// <summary>Called after the owner deals damage to a target.</summary>
    public virtual void OnDealDamage(Combatant target, int amount) { }

    /// <summary>Called after the owner takes damage (after block).</summary>
    public virtual void OnTakeDamage(int amount) { }

    // --- Block hooks ---

    /// <summary>Modify block gained by the owner. Return modified amount.</summary>
    public virtual float ModifyBlockGain(float baseBlock) => baseBlock;

    // --- Card hooks ---

    /// <summary>Called when the owner plays a card (after effects resolve).</summary>
    public virtual void OnCardPlayed(Card card) { }

    /// <summary>Called when the owner draws a card.</summary>
    public virtual void OnCardDrawn(Card card) { }

    // --- Death hooks ---

    /// <summary>Called when the owner dies.</summary>
    public virtual void OnDeath() { }

    /// <summary>Called when the owner kills another combatant.</summary>
    public virtual void OnKill(Combatant victim) { }

    // --- Misc hooks ---

    /// <summary>Called when any status/power is applied to the owner.</summary>
    public virtual void OnPowerApplied(AbstractPower power) { }

    /// <summary>Called when the owner heals.</summary>
    public virtual void OnHeal(int amount) { }
}

/// <summary>
/// Manages all powers on a single combatant. Replaces StatusEffectManager
/// for new Power-based effects while maintaining backward compatibility.
/// </summary>
public class PowerManager
{
    private readonly List<AbstractPower> _powers = [];

    /// <summary>All currently active powers.</summary>
    public IReadOnlyList<AbstractPower> Powers => _powers;

    /// <summary>Reference to the action manager for injecting into new powers.</summary>
    public ActionManager? ActionManager { get; set; }

    /// <summary>
    /// Apply a power to the combatant. If the same PowerId already exists, stacks are added.
    /// </summary>
    public void ApplyPower(AbstractPower power, Combatant owner)
    {
        power.Owner = owner;
        power.ActionManager = ActionManager;

        var existing = _powers.FirstOrDefault(p => p.PowerId == power.PowerId);
        if (existing != null)
        {
            existing.Amount += power.Amount;
            existing.OnStacksAdded(power.Amount);
        }
        else
        {
            _powers.Add(power);
            power.OnApply();
        }

        // Notify all powers on this owner that a power was applied
        foreach (var p in _powers)
            p.OnPowerApplied(power);
    }

    /// <summary>Remove a power by ID.</summary>
    public void RemovePower(string powerId)
    {
        var power = _powers.FirstOrDefault(p => p.PowerId == powerId);
        if (power != null)
        {
            power.OnRemove();
            _powers.Remove(power);
        }
    }

    /// <summary>Get a power by ID, or null.</summary>
    public AbstractPower? GetPower(string powerId) => _powers.FirstOrDefault(p => p.PowerId == powerId);

    /// <summary>Check if a power is present.</summary>
    public bool HasPower(string powerId) => _powers.Any(p => p.PowerId == powerId);

    /// <summary>Get the stack count for a power, or 0.</summary>
    public int GetStacks(string powerId) => GetPower(powerId)?.Amount ?? 0;

    /// <summary>Reduce stacks; remove if reaching 0.</summary>
    public void ConsumeStacks(string powerId, int amount)
    {
        var power = GetPower(powerId);
        if (power == null) return;
        power.Amount -= amount;
        if (power.Amount <= 0) RemovePower(powerId);
    }

    // --- Aggregate hooks: iterate all powers in priority order ---

    public float ModifyAttackDamage(float baseDamage)
    {
        float d = baseDamage;
        foreach (var p in GetPowersByPriority())
            d = p.ModifyAttackDamage(d);
        return d;
    }

    public float ModifyDamageTaken(float baseDamage)
    {
        float d = baseDamage;
        foreach (var p in GetPowersByPriority())
            d = p.ModifyDamageTaken(d);
        return d;
    }

    public float ModifyBlockGain(float baseBlock)
    {
        float b = baseBlock;
        foreach (var p in GetPowersByPriority())
            b = p.ModifyBlockGain(b);
        return b;
    }

    public void TriggerAtTurnStart()
    {
        foreach (var p in GetPowersByPriority()) p.AtTurnStart();
    }

    public void TriggerAtTurnEnd()
    {
        // Execute end-of-turn effects
        foreach (var p in GetPowersByPriority()) p.AtTurnEnd();

        // Tick down powers that reduce each turn
        var toRemove = new List<string>();
        foreach (var p in _powers)
        {
            if (p.TicksDown)
            {
                p.Amount--;
                if (p.Amount <= 0)
                    toRemove.Add(p.PowerId);
            }
        }
        foreach (var id in toRemove) RemovePower(id);
    }

    public void TriggerOnDealDamage(Combatant target, int amount)
    {
        foreach (var p in GetPowersByPriority()) p.OnDealDamage(target, amount);
    }

    public void TriggerOnTakeDamage(int amount)
    {
        foreach (var p in GetPowersByPriority()) p.OnTakeDamage(amount);
    }

    public void TriggerOnCardPlayed(Card card)
    {
        foreach (var p in GetPowersByPriority()) p.OnCardPlayed(card);
    }

    public void TriggerOnCardDrawn(Card card)
    {
        foreach (var p in GetPowersByPriority()) p.OnCardDrawn(card);
    }

    public void TriggerOnDeath()
    {
        foreach (var p in GetPowersByPriority()) p.OnDeath();
    }

    public void TriggerOnKill(Combatant victim)
    {
        foreach (var p in GetPowersByPriority()) p.OnKill(victim);
    }

    public void TriggerOnHeal(int amount)
    {
        foreach (var p in GetPowersByPriority()) p.OnHeal(amount);
    }

    /// <summary>Clear all powers (end of combat cleanup).</summary>
    public void Clear()
    {
        foreach (var p in _powers) p.OnRemove();
        _powers.Clear();
    }

    private IEnumerable<AbstractPower> GetPowersByPriority() =>
        _powers.OrderBy(p => p.Priority).ToList(); // ToList to avoid modification during iteration
}
