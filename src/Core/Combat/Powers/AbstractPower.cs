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

    /// <summary>The combatant that applied this power (for link/parasite effects).</summary>
    public Combatant? Source { get; set; }

    /// <summary>Optional implant bonus provider injected by the owning PowerManager.</summary>
    public Func<string, int>? ImplantBonusProvider { get; set; }

    protected int GetImplantBonus(string effectType) => ImplantBonusProvider?.Invoke(effectType) ?? 0;

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

    /// <summary>Called after the owner takes damage (after block). Attacker is the combatant that dealt the hit, null for non-attack damage (poison, env).</summary>
    public virtual void OnTakeDamage(int amount, Combatant? attacker = null) { }

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

    /// <summary>Called when the owner consumes overcharge stacks.</summary>
    public virtual void OnOverchargeConsumed(int stacks) { }

    /// <summary>Called when the owner consumes resonance stacks. Can modify damage.</summary>
    public virtual float ModifyConsumeResonanceDamage(float damage, int stacks) => damage;

    /// <summary>Called when the owner consumes overcharge stacks. Can modify damage (e.g. FrontlineCommander +1/stack).</summary>
    public virtual float ModifyOverchargeConsumeDamage(float damage, int stacks) => damage;

    /// <summary>Modify the energy cost of a card. Returns modified cost.</summary>
    public virtual int ModifyCardCost(Card card, int baseCost, Combatant? target) => baseCost;

    // =====================================================================
    //  DISPATCHER SUBSCRIPTION — replaces the PowerManager "hook wall"
    // =====================================================================

    // Use a list (not dict) so multiple subs on the same TriggerKind are all tracked for clean unsubscription.
    private readonly List<(TriggerKind kind, CombatEventDispatcher.Handler handler)> _subscriptions = new();

    /// <summary>
    /// Subscribe this power's virtual hooks to the Dispatcher event bus.
    /// Called automatically by PowerManager when EventBus is set and a power is applied.
    /// Subclasses may override to customise which events they listen to.
    /// </summary>
    public virtual void SubscribeTo(CombatEventDispatcher bus)
    {
        AddSub(bus, TriggerKind.AtTurnStart,   evt => { if (evt.Actor == Owner) AtTurnStart(); });
        AddSub(bus, TriggerKind.AtTurnEnd,     evt => { if (evt.Actor == Owner) AtTurnEnd(); });
        AddSub(bus, TriggerKind.OnCardPlayed,  evt => { if (evt.Actor == Owner && evt.Card != null) OnCardPlayed(evt.Card); });
        AddSub(bus, TriggerKind.OnCardDrawn,   evt => { if (evt.Actor == Owner && evt.Card != null) OnCardDrawn(evt.Card); });
        AddSub(bus, TriggerKind.OnDealDamage,  evt => { if (evt.Actor == Owner && evt.Target != null) OnDealDamage(evt.Target, evt.Amount); });
        AddSub(bus, TriggerKind.OnTakeDamage,  evt => { if (evt.Target == Owner) OnTakeDamage(evt.Amount, evt.Actor); });
        AddSub(bus, TriggerKind.OnDeath,       evt => { if (evt.Actor == Owner) OnDeath(); });
        AddSub(bus, TriggerKind.OnKill,        evt => { if (evt.Actor == Owner && evt.Target != null) OnKill(evt.Target); });
        AddSub(bus, TriggerKind.OnHeal,        evt => { if (evt.Target == Owner) OnHeal(evt.Amount); });
        AddSub(bus, TriggerKind.OnOverchargeConsumed,
                                               evt => { if (evt.Actor == Owner) OnOverchargeConsumed(evt.Amount); });
    }

    /// <summary>Unsubscribe all subscriptions established by SubscribeTo.</summary>
    public virtual void UnsubscribeFrom(CombatEventDispatcher bus)
    {
        foreach (var (kind, handler) in _subscriptions)
            bus.Unsubscribe(kind, handler);
        _subscriptions.Clear();
    }

    private void AddSub(CombatEventDispatcher bus, TriggerKind kind, CombatEventDispatcher.Handler handler)
    {
        _subscriptions.Add((kind, handler));
        bus.Subscribe(kind, handler, Priority);
    }
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

    /// <summary>Optional implant bonus provider for powers that need implant-aware behavior.</summary>
    public Func<string, int>? ImplantBonusProvider { get; set; }

    /// <summary>
    /// When set, every power applied to this manager automatically subscribes its hooks
    /// to the event bus.  Set by CombatManager after Initialize().
    /// </summary>
    public CombatEventDispatcher? EventBus { get; set; }

    /// <summary>
    /// Apply a power to the combatant. If the same PowerId already exists, stacks are added.
    /// Optional <paramref name="source"/> records which combatant produced the power (for parasite/link effects).
    /// </summary>
    public void ApplyPower(AbstractPower power, Combatant owner, Combatant? source = null)
    {
        power.Owner = owner;
        power.ActionManager = ActionManager;
        power.ImplantBonusProvider = ImplantBonusProvider;
        if (source != null) power.Source = source;

        var existing = _powers.FirstOrDefault(p => p.PowerId == power.PowerId);
        if (existing != null)
        {
            existing.Amount += power.Amount;
            if (source != null && existing.Source == null) existing.Source = source;
            existing.OnStacksAdded(power.Amount);
        }
        else
        {
            _powers.Add(power);
            power.OnApply();
            // Wire to dispatcher AFTER OnApply so the power can inspect its own state first
            if (EventBus != null) power.SubscribeTo(EventBus);
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
            if (EventBus != null) power.UnsubscribeFrom(EventBus);
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

    public void TriggerOnTakeDamage(int amount, Combatant? attacker = null)
    {
        foreach (var p in GetPowersByPriority()) p.OnTakeDamage(amount, attacker);
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

    public void TriggerOnOverchargeConsumed(int stacks)
    {
        foreach (var p in GetPowersByPriority()) p.OnOverchargeConsumed(stacks);
    }

    public float ModifyConsumeResonanceDamage(float damage, int stacks)
    {
        float d = damage;
        foreach (var p in GetPowersByPriority())
            d = p.ModifyConsumeResonanceDamage(d, stacks);
        return d;
    }

    public float ModifyOverchargeConsumeDamage(float damage, int stacks)
    {
        float d = damage;
        foreach (var p in GetPowersByPriority())
            d = p.ModifyOverchargeConsumeDamage(d, stacks);
        return d;
    }

    public int ModifyCardCost(Card card, int baseCost, Combatant? target)
    {
        int cost = baseCost;
        foreach (var p in GetPowersByPriority())
            cost = p.ModifyCardCost(card, cost, target);
        return cost;
    }

    /// <summary>Clear all powers (end of combat cleanup).</summary>
    public void Clear()
    {
        foreach (var p in _powers)
        {
            if (EventBus != null) p.UnsubscribeFrom(EventBus);
            p.OnRemove();
        }
        _powers.Clear();
    }

    private IEnumerable<AbstractPower> GetPowersByPriority() =>
        _powers.OrderBy(p => p.Priority).ToList(); // ToList to avoid modification during iteration

    /// <summary>
    /// Tick down all TicksDown powers by one; remove those that reach 0.
    /// Called by CombatManager at the end of each turn, after AtTurnEnd events fire.
    /// </summary>
    public void TickDownPowers()
    {
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
}
