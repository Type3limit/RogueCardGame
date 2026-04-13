using RogueCardGame.Core.Cards;
using RogueCardGame.Core.Characters;
using RogueCardGame.Core.Combat.Powers;
using RogueCardGame.Core.Deck;

namespace RogueCardGame.Core.Combat.Actions;

// =====================================================================
//  COMMON ACTIONS — Game actions used by cards, enemies, and powers
// =====================================================================

/// <summary>
/// Deal damage from source to target(s). Goes through power modifier chain.
/// Modeled after STS's DamageAction.
/// </summary>
public class DealDamageAction : GameAction
{
    public Combatant Source { get; }
    public List<Combatant> Targets { get; }
    public int BaseDamage { get; }
    public CardRange Range { get; }
    public FormationSystem? Formation { get; }
    public AggroSystem? Aggro { get; }

    public DealDamageAction(Combatant source, List<Combatant> targets, int baseDamage,
        CardRange range = CardRange.None, FormationSystem? formation = null, AggroSystem? aggro = null)
    {
        Source = source;
        Targets = targets;
        BaseDamage = baseDamage;
        Range = range;
        Formation = formation;
        Aggro = aggro;
        Duration = ActionType.Fast;
    }

    public override void Execute()
    {
        // Step 1: Calculate outgoing damage through source power modifiers
        float damage = BaseDamage;
        damage += Source.Powers.GetStacks(CommonPowerIds.Strength);  // Strength adds flat
        damage = Source.Powers.ModifyAttackDamage(damage);

        // Formation multiplier
        if (Formation != null)
            damage *= Formation.GetDamageMultiplier(Source.Id, Range);

        int finalDamage = Math.Max(0, (int)damage);

        // Step 2: Apply to each target
        foreach (var target in Targets.Where(t => t.IsAlive))
        {
            // Target's incoming damage modifiers
            float modified = target.Powers.ModifyDamageTaken(finalDamage);
            int incoming = Math.Max(0, (int)modified);

            // Block absorption
            int blocked = Math.Min(target.Block, incoming);
            target.Block -= blocked;
            int hpDamage = incoming - blocked;
            target.CurrentHp = Math.Max(0, target.CurrentHp - hpDamage);

            // Trigger hooks
            if (hpDamage > 0)
            {
                Source.Powers.TriggerOnDealDamage(target, hpDamage);
                target.Powers.TriggerOnTakeDamage(hpDamage);
                Aggro?.AddAggro(Source.Id, hpDamage * AggroSystem.AggroPerDamage);
            }

            // Check kill
            if (!target.IsAlive)
            {
                target.Powers.TriggerOnDeath();
                Source.Powers.TriggerOnKill(target);
            }
        }

        IsDone = true;
    }
}

/// <summary>
/// Multi-hit damage (e.g., "Hit 3 times for 4 damage each").
/// </summary>
public class MultiHitDamageAction : GameAction
{
    public Combatant Source { get; }
    public List<Combatant> Targets { get; }
    public int BaseDamage { get; }
    public int HitCount { get; }
    public CardRange Range { get; }
    public FormationSystem? Formation { get; }
    public AggroSystem? Aggro { get; }

    public MultiHitDamageAction(Combatant source, List<Combatant> targets, int baseDamage, int hitCount,
        CardRange range = CardRange.None, FormationSystem? formation = null, AggroSystem? aggro = null)
    {
        Source = source; Targets = targets; BaseDamage = baseDamage; HitCount = hitCount;
        Range = range; Formation = formation; Aggro = aggro;
        Duration = ActionType.Medium;
    }

    public override void Execute()
    {
        for (int i = 0; i < HitCount; i++)
        {
            AddToTop(new DealDamageAction(Source, Targets, BaseDamage, Range, Formation, Aggro));
        }
        IsDone = true;
    }
}

/// <summary>
/// Gain block for a combatant. Goes through power modifier chain.
/// </summary>
public class GainBlockAction : GameAction
{
    public Combatant Target { get; }
    public int BaseBlock { get; }
    public FormationSystem? Formation { get; }

    public GainBlockAction(Combatant target, int baseBlock, FormationSystem? formation = null)
    {
        Target = target; BaseBlock = baseBlock; Formation = formation;
        Duration = ActionType.Instant;
    }

    public override void Execute()
    {
        float block = BaseBlock;
        if (Formation != null)
            block += Formation.GetBlockBonus(Target.Id);
        block = Target.Powers.ModifyBlockGain(block);
        Target.Block += Math.Max(0, (int)block);
        IsDone = true;
    }
}

/// <summary>
/// Draw cards for a player.
/// </summary>
public class DrawCardAction : GameAction
{
    public PlayerCharacter Player { get; }
    public int Count { get; }
    public DeckManager? Deck { get; }

    public DrawCardAction(PlayerCharacter player, int count, DeckManager? deck)
    {
        Player = player; Count = count; Deck = deck;
        Duration = ActionType.Fast;
    }

    public override void Execute()
    {
        if (Deck == null) { IsDone = true; return; }
        var drawn = Deck.Draw(Count);
        foreach (var card in drawn)
            Player.Powers.TriggerOnCardDrawn(card);
        IsDone = true;
    }
}

/// <summary>
/// Apply a Power to a combatant.
/// </summary>
public class ApplyPowerAction : GameAction
{
    public Combatant Target { get; }
    public AbstractPower Power { get; }

    public ApplyPowerAction(Combatant target, AbstractPower power)
    {
        Target = target; Power = power;
        Duration = ActionType.Instant;
    }

    public override void Execute()
    {
        Target.Powers.ApplyPower(Power, Target);
        IsDone = true;
    }
}

/// <summary>
/// Gain energy for a player.
/// </summary>
public class GainEnergyAction : GameAction
{
    public PlayerCharacter Player { get; }
    public int Amount { get; }

    public GainEnergyAction(PlayerCharacter player, int amount)
    {
        Player = player; Amount = amount;
        Duration = ActionType.Instant;
    }

    public override void Execute()
    {
        Player.CurrentEnergy += Amount;
        IsDone = true;
    }
}

/// <summary>
/// Heal a combatant.
/// </summary>
public class HealAction : GameAction
{
    public Combatant Target { get; }
    public int Amount { get; }

    public HealAction(Combatant target, int amount)
    {
        Target = target; Amount = amount;
        Duration = ActionType.Instant;
    }

    public override void Execute()
    {
        int before = Target.CurrentHp;
        Target.CurrentHp = Math.Min(Target.MaxHp, Target.CurrentHp + Amount);
        int healed = Target.CurrentHp - before;
        if (healed > 0)
            Target.Powers.TriggerOnHeal(healed);
        IsDone = true;
    }
}

/// <summary>
/// Force a combatant to a specific formation row.
/// </summary>
public class ForceRepositionAction : GameAction
{
    public Combatant Target { get; }
    public FormationRow Row { get; }
    public FormationSystem Formation { get; }

    public ForceRepositionAction(Combatant target, FormationRow row, FormationSystem formation)
    {
        Target = target; Row = row; Formation = formation;
        Duration = ActionType.Instant;
    }

    public override void Execute()
    {
        Formation.ForcePosition(Target.Id, Row);
        IsDone = true;
    }
}

/// <summary>
/// Add a card by ID to the discard pile.
/// </summary>
public class AddCardToDiscardAction : GameAction
{
    public PlayerCharacter Player { get; }
    public string CardId { get; }
    public CardDatabase? CardDb { get; }
    public DeckManager? Deck { get; }

    public AddCardToDiscardAction(PlayerCharacter player, string cardId, CardDatabase? cardDb, DeckManager? deck)
    {
        Player = player; CardId = cardId; CardDb = cardDb; Deck = deck;
        Duration = ActionType.Instant;
    }

    public override void Execute()
    {
        if (CardDb != null && Deck != null)
        {
            var card = CardDb.CreateCard(CardId);
            if (card != null) Deck.AddToDiscard(card);
        }
        IsDone = true;
    }
}

/// <summary>
/// Exhaust a card (STS: remove from play for the rest of combat).
/// </summary>
public class ExhaustCardAction : GameAction
{
    public Card Card { get; }
    public DeckManager Deck { get; }

    public ExhaustCardAction(Card card, DeckManager deck)
    {
        Card = card; Deck = deck;
        Duration = ActionType.Instant;
    }

    public override void Execute()
    {
        Deck.Exhaust(Card);
        IsDone = true;
    }
}

/// <summary>
/// Discard a specific card from hand.
/// </summary>
public class DiscardCardAction : GameAction
{
    public Card Card { get; }
    public DeckManager Deck { get; }

    public DiscardCardAction(Card card, DeckManager deck)
    {
        Card = card; Deck = deck;
        Duration = ActionType.Instant;
    }

    public override void Execute()
    {
        Deck.DiscardFromHand(Card);
        IsDone = true;
    }
}

/// <summary>
/// Consume Overcharge stacks and deal bonus damage per stack (Vanguard class mechanic).
/// </summary>
public class OverchargeConsumeAction : GameAction
{
    public Combatant Source { get; }
    public List<Combatant> Targets { get; }
    public int BaseDamage { get; }
    public int DamagePerStack { get; }
    public CardRange Range { get; }
    public FormationSystem? Formation { get; }
    public AggroSystem? Aggro { get; }

    public OverchargeConsumeAction(Combatant source, List<Combatant> targets,
        int baseDamage, int damagePerStack,
        CardRange range = CardRange.None, FormationSystem? formation = null, AggroSystem? aggro = null)
    {
        Source = source; Targets = targets;
        BaseDamage = baseDamage; DamagePerStack = damagePerStack;
        Range = range; Formation = formation; Aggro = aggro;
        Duration = ActionType.Fast;
    }

    public override void Execute()
    {
        int stacks = Source.Powers.GetStacks(CommonPowerIds.Overcharge);
        int totalDamage = BaseDamage + stacks * DamagePerStack;
        if (stacks > 0)
            Source.Powers.RemovePower(CommonPowerIds.Overcharge);

        AddToTop(new DealDamageAction(Source, Targets, totalDamage, Range, Formation, Aggro));
        IsDone = true;
    }
}

/// <summary>
/// Dash attack: deal damage then reposition source (Vanguard phase-burst).
/// </summary>
public class DashDamageAction : GameAction
{
    public Combatant Source { get; }
    public List<Combatant> Targets { get; }
    public int BaseDamage { get; }
    public bool ReturnToBack { get; }
    public FormationSystem Formation { get; }
    public AggroSystem? Aggro { get; }

    public DashDamageAction(Combatant source, List<Combatant> targets,
        int baseDamage, bool returnToBack, FormationSystem formation, AggroSystem? aggro = null)
    {
        Source = source; Targets = targets;
        BaseDamage = baseDamage; ReturnToBack = returnToBack;
        Formation = formation; Aggro = aggro;
        Duration = ActionType.Medium;
    }

    public override void Execute()
    {
        // Enqueue: first damage, then reposition
        var dest = ReturnToBack ? FormationRow.Back : FormationRow.Front;
        AddToTop(new ForceRepositionAction(Source, dest, Formation));
        AddToTop(new DealDamageAction(Source, Targets, BaseDamage, CardRange.Melee, Formation, Aggro));
        IsDone = true;
    }
}

/// <summary>
/// Execute a "doom check" — kill enemies whose Doom stacks >= current HP.
/// Used by the Doom power system as a proof of extensibility.
/// </summary>
public class DoomExecuteAction : GameAction
{
    public List<Combatant> Targets { get; }
    public Combatant? Source { get; }

    public DoomExecuteAction(List<Combatant> targets, Combatant? source = null)
    {
        Targets = targets; Source = source;
        Duration = ActionType.Fast;
    }

    public override void Execute()
    {
        foreach (var target in Targets.Where(t => t.IsAlive))
        {
            int doom = target.Powers.GetStacks(CommonPowerIds.Doom);
            if (doom >= target.CurrentHp)
            {
                // Execute! Set HP to 0 directly (not damage — bypasses block, strength, etc.)
                target.CurrentHp = 0;
                target.Powers.TriggerOnDeath();
                if (Source != null)
                    Source.Powers.TriggerOnKill(target);
            }
        }
        IsDone = true;
    }
}
