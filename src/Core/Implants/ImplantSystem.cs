using System.Text.Json;
using System.Text.Json.Serialization;
using RogueCardGame.Core.Cards;
using RogueCardGame.Core.Characters;
using RogueCardGame.Core.Combat;
using RogueCardGame.Core.Combat.Actions;
using RogueCardGame.Core.Combat.Powers;
using RogueCardGame.Core.Deck;

namespace RogueCardGame.Core.Implants;

/// <summary>
/// Implant slot types - each player has one Neural slot and one Core slot.
/// Design doc v3: 2 slots only. No Somatic slot.
/// </summary>
public enum ImplantSlot
{
    Neural,   // Affects card draw, energy, hand mechanics — "how you play cards"
    Core      // Changes class core mechanic + formation interaction — "what build you play"
}

/// <summary>
/// Implant rarity.
/// </summary>
public enum ImplantRarity
{
    Common,
    Uncommon,
    Rare,
    Legendary
}

/// <summary>
/// Data definition for an implant.
/// </summary>
public sealed class ImplantData
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string NameEn { get; init; } = "";
    public required string Description { get; init; }
    public required ImplantSlot Slot { get; init; }
    public required ImplantRarity Rarity { get; init; }
    public CardClass? ClassRestriction { get; init; }
    public int CraftCost { get; init; } = 0; // Parts cost
    public List<ImplantEffectData> Effects { get; init; } = [];
    public string? ArtPath { get; init; }
    public string? Lore { get; init; }
}

/// <summary>
/// Individual effect within an implant.
/// </summary>
public sealed class ImplantEffectData
{
    public required string Type { get; init; } // e.g., "maxHp", "drawPerTurn", "energy", "strength", etc.
    public int Value { get; init; }
    public string? Condition { get; init; }
}

/// <summary>
/// Runtime implant instance equipped by a player.
/// </summary>
public class Implant
{
    public ImplantData Data { get; }
    public bool IsActive { get; set; } = true;

    public Implant(ImplantData data) => Data = data;
}

/// <summary>
/// Manages the 3 implant slots for a single player.
/// </summary>
public class ImplantManager
{
    private readonly Dictionary<ImplantSlot, Implant?> _slots = new()
    {
        [ImplantSlot.Neural] = null,
        [ImplantSlot.Core] = null,
    };

    public event Action<ImplantSlot, Implant>? OnImplantEquipped;
    public event Action<ImplantSlot, Implant>? OnImplantRemoved;

    public Implant? GetImplant(ImplantSlot slot) =>
        _slots.GetValueOrDefault(slot);

    public bool HasImplant(ImplantSlot slot) =>
        _slots.GetValueOrDefault(slot) != null;

    /// <summary>
    /// Equip an implant. Returns the previously equipped implant (if any).
    /// </summary>
    public Implant? Equip(ImplantData data)
    {
        var slot = data.Slot;
        var previous = _slots[slot];
        if (previous != null)
            OnImplantRemoved?.Invoke(slot, previous);

        var implant = new Implant(data);
        _slots[slot] = implant;
        OnImplantEquipped?.Invoke(slot, implant);
        return previous;
    }

    public void Remove(ImplantSlot slot)
    {
        var implant = _slots[slot];
        if (implant != null)
        {
            _slots[slot] = null;
            OnImplantRemoved?.Invoke(slot, implant);
        }
    }

    /// <summary>
    /// Calculate total bonus of a specific effect type across all equipped implants.
    /// </summary>
    public int GetTotalBonus(string effectType)
    {
        return _slots.Values
            .Where(i => i?.IsActive == true)
            .SelectMany(i => i!.Data.Effects)
            .Where(e => e.Type == effectType)
            .Sum(e => e.Value);
    }

    public List<Implant> GetAllEquipped() =>
        _slots.Values.Where(i => i != null).Cast<Implant>().ToList();
}

/// <summary>
/// Database of implant definitions.
/// </summary>
public class ImplantDatabase
{
    private readonly Dictionary<string, ImplantData> _implants = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public void LoadFromJson(string json)
    {
        var implants = JsonSerializer.Deserialize<List<ImplantData>>(json, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize implant data");
        foreach (var i in implants) _implants[i.Id] = i;
    }

    public void LoadFromFile(string path) => LoadFromJson(File.ReadAllText(path));

    public void LoadFromDirectory(string dir)
    {
        if (!Directory.Exists(dir)) return;
        foreach (var file in Directory.GetFiles(dir, "*.json"))
            LoadFromFile(file);
    }

    public ImplantData? GetImplant(string id) => _implants.GetValueOrDefault(id);

    public List<ImplantData> GetBySlot(ImplantSlot slot) =>
        _implants.Values.Where(i => i.Slot == slot).ToList();

    public List<ImplantData> GetForClass(CardClass cardClass) =>
        _implants.Values.Where(i => i.ClassRestriction == null || i.ClassRestriction == cardClass).ToList();
}

// =============================================================================
//  ImplantBonusPower — §2.7 "Implant assembly generates PersistentPower"
// =============================================================================

/// <summary>
/// Assembled at combat-start from the player's equipped implants.
/// Each event-driven implant effect (energy, draw, block, kill bonus, etc.) becomes
/// a Dispatcher subscription on this Power, removing the scattered
/// <c>_implants.GetTotalBonus()</c> call sites from CombatManager.
///
/// Initialization-time effects (maxHp, maxHandSize) and complex effects
/// (precognitionModule, battlefieldInterface) are left in CombatManager for now.
/// </summary>
public class ImplantBonusPower : AbstractPower
{
    public override string PowerId => "ImplantBonus";
    public override string Name   => "植入体加成";
    public override string GetDescription() => "植入体效果的事件钩子集合。";

    private readonly ImplantManager _manager;
    private readonly DeckManager?   _deck;
    private int _turnCount;

    public ImplantBonusPower(ImplantManager manager, DeckManager? deck)
    {
        _manager = manager;
        _deck    = deck;
        Amount   = 1; // non-zero so stacking is detected but we never actually stack
    }

    // ---- helpers ------------------------------------------------------------

    private int Bonus(string type) => _manager.GetTotalBonus(type);

    // -------------------------------------------------------------------------
    //  Subscribe all event-driven implant effects to the Dispatcher.
    //  This override REPLACES the default AbstractPower.SubscribeTo() so only
    //  the implant-specific events are registered (not all 10 virtual hooks).
    // -------------------------------------------------------------------------

    public override void SubscribeTo(CombatEventDispatcher bus)
    {
        // AtTurnStart: energy bonus, draw bonus, resonance-draw bonus, block gain
        AddImplantSub(bus, TriggerKind.AtTurnStart, OnTurnStartEvent);
        // AtTurnEnd: overchargeToStrength, overchargeThorns, turnSelfDamage, overloadCircuit
        AddImplantSub(bus, TriggerKind.AtTurnEnd, OnTurnEndEvent);
        // OnKill: killEnergy
        AddImplantSub(bus, TriggerKind.OnKill, OnKillEvent);
        // OnCardPlayed: pulseProcessor draw
        AddImplantSub(bus, TriggerKind.OnCardPlayed, OnCardPlayedEvent);
    }

    // Use a list (not dict) so multiple subs to the same TriggerKind are all tracked.
    private readonly List<(TriggerKind kind, CombatEventDispatcher.Handler handler)> _implantSubs = new();

    private void AddImplantSub(CombatEventDispatcher bus, TriggerKind kind,
                               CombatEventDispatcher.Handler handler)
    {
        _implantSubs.Add((kind, handler));
        bus.Subscribe(kind, handler, Priority);
    }

    public override void UnsubscribeFrom(CombatEventDispatcher bus)
    {
        foreach (var (kind, handler) in _implantSubs)
            bus.Unsubscribe(kind, handler);
        _implantSubs.Clear();
    }

    // ---- event handlers -----------------------------------------------------

    private void OnTurnStartEvent(CombatEvent evt)
    {
        if (evt.Actor != Owner || Owner is not PlayerCharacter player) return;
        _turnCount++;

        // Extra energy at turn start
        int energyBonus = Bonus("energy");
        if (energyBonus > 0)
            player.CurrentEnergy = Math.Min(player.MaxEnergy, player.CurrentEnergy + energyBonus);

        // Extra draws at turn start — draw directly (sync) so cards appear in hand immediately
        int drawBonus = Bonus("drawPerTurn");
        if (drawBonus > 0)
            _deck?.Draw(drawBonus);

        // Resonance-conditional draw — also sync
        int resonanceDrawThreshold = Bonus("resonanceDrawBonus");
        if (resonanceDrawThreshold > 0 && _deck != null)
        {
            int resonance = player.Powers.GetStacks(CommonPowerIds.Resonance);
            int bonusDraw = resonance / resonanceDrawThreshold;
            if (bonusDraw > 0)
                _deck.Draw(bonusDraw);
        }

        // Block gain at turn start
        int blockBonus = Bonus("turnStartBlock");
        if (blockBonus > 0)
            player.GainBlock(blockBonus);
    }

    private void OnTurnEndEvent(CombatEvent evt)
    {
        if (evt.Actor != Owner || Owner is not PlayerCharacter player) return;

        int overchargeToStrength = Bonus("overchargeToStrength");
        int overchargeThorns     = Bonus("overchargeThorns");
        if (overchargeToStrength > 0 || overchargeThorns > 0)
        {
            int stacks = player.Powers.GetStacks(CommonPowerIds.Overcharge);
            if (stacks > 0)
            {
                if (overchargeToStrength > 0)
                    ActionManager?.AddToBottom(new ApplyPowerAction(player,
                        new StrengthPower { Amount = stacks * overchargeToStrength }));
                if (overchargeThorns > 0)
                    ActionManager?.AddToBottom(new ApplyPowerAction(player,
                        new ThornsPower { Amount = stacks * overchargeThorns }));
            }
        }

        int selfDamage = Bonus("turnSelfDamage");
        if (selfDamage > 0)
            ActionManager?.AddToBottom(new SelfDamageAction(player, selfDamage));

        int overloadDamagePerUnused = Bonus("overloadCircuit");
        if (overloadDamagePerUnused > 0 && player.CurrentEnergy > 0)
            ActionManager?.AddToBottom(new SelfDamageAction(player, player.CurrentEnergy * overloadDamagePerUnused));
    }

    private void OnKillEvent(CombatEvent evt)
    {
        if (evt.Actor != Owner || Owner is not PlayerCharacter player) return;
        int killEnergy = Bonus("killEnergy");
        if (killEnergy > 0)
            player.CurrentEnergy = Math.Min(player.MaxEnergy, player.CurrentEnergy + killEnergy);
    }

    private void OnCardPlayedEvent(CombatEvent evt)
    {
        if (evt.Actor != Owner || Owner is not PlayerCharacter player) return;
        int pulseProcessor = Bonus("pulseProcessor");
        if (pulseProcessor > 0 && _deck != null && _deck.Hand.Count < _deck.MaxHandSize)
            _deck.Draw(1); // sync draw so the card is immediately available this turn
    }
}
