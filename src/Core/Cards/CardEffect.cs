using RogueCardGame.Core.Cards;
using RogueCardGame.Core.Characters;
using RogueCardGame.Core.Combat.Actions;
using RogueCardGame.Core.Combat.Powers;
using RogueCardGame.Core.Deck;
using RogueCardGame.Core.Utils;

namespace RogueCardGame.Core.Combat;

/// <summary>
/// Interface for all card effects. Uses Command pattern.
/// </summary>
public interface ICardEffect
{
    void Execute(CardEffectContext context);
    string GetDescription(CardEffectContext? context = null);
}

/// <summary>
/// Context passed to card effects during execution.
/// </summary>
public sealed class CardEffectContext
{
    public required Combatant Source { get; init; }
    public required Combatant? Target { get; init; }
    public required List<Combatant> AllTargets { get; init; }
    public required FormationSystem Formation { get; init; }
    public required AggroSystem Aggro { get; init; }
    public Card? Card { get; init; }
    public CardRange Range { get; init; }
    /// <summary>STS-style action queue. Effects enqueue actions here for cascading execution.</summary>
    public ActionManager? Actions { get; init; }
    /// <summary>Player's deck manager (for draw/discard actions).</summary>
    public DeckManager? Deck { get; init; }
    /// <summary>Card database (for creating new card instances).</summary>
    public CardDatabase? CardDb { get; init; }
    public Func<string, int>? ImplantBonusProvider { get; init; }
    public int LastDamageDealt { get; set; }
    public int TotalDamageDealtThisCard { get; set; }
    public int LastSelfHpLoss { get; set; }
    public int TotalSelfHpLossThisCard { get; set; }
    public bool ConsumedOverchargeThisTurn { get; set; }
    /// <summary>All living enemies on the field (for "per poisoned enemy on field" type effects).</summary>
    public List<Combatant>? AllEnemies { get; init; }
}

/// <summary>
/// Composite effect: executes multiple effects in sequence.
/// </summary>
public class CompositeEffect : ICardEffect
{
    private readonly List<ICardEffect> _effects = [];

    public CompositeEffect(params ICardEffect[] effects)
    {
        _effects.AddRange(effects);
    }

    public void Add(ICardEffect effect) => _effects.Add(effect);

    public void Execute(CardEffectContext context)
    {
        foreach (var effect in _effects)
            effect.Execute(context);
    }

    public string GetDescription(CardEffectContext? context = null) =>
        string.Join("; ", _effects.Select(e => e.GetDescription(context)));
}

/// <summary>
/// Wraps another effect; only executes when the source is in the required row.
/// </summary>
public class RowConditionalEffect : ICardEffect
{
    private readonly ICardEffect _inner;
    private readonly FormationRow _requiredRow;

    public RowConditionalEffect(ICardEffect inner, FormationRow requiredRow)
    {
        _inner = inner;
        _requiredRow = requiredRow;
    }

    public void Execute(CardEffectContext context)
    {
        if (context.Formation.GetPosition(context.Source.Id) == _requiredRow)
            _inner.Execute(context);
    }

    public string GetDescription(CardEffectContext? context = null) =>
        $"[{(_requiredRow == FormationRow.Front ? "前排" : "后排")}] {_inner.GetDescription(context)}";
}

/// <summary>
/// Wrapper that overrides the context to target a selector-chosen set regardless of the card's target type.
/// Used for per-effect targeting when an individual effect has "target": "self"/"allEnemies"/... in JSON.
/// </summary>
public class SelfTargetWrapper : ICardEffect
{
    private readonly ICardEffect _inner;
    private readonly TargetSelector _selector;

    public SelfTargetWrapper(ICardEffect inner) : this(inner, TargetSelector.Self) { }

    public SelfTargetWrapper(ICardEffect inner, TargetSelector selector)
    {
        _inner = inner;
        _selector = selector;
    }

    public void Execute(CardEffectContext context)
    {
        var resolved = _selector.Select(context);
        if (resolved.Count == 0) return;
        var first = resolved[0];
        var selfContext = new CardEffectContext
        {
            Source = context.Source,
            Target = first,
            AllTargets = resolved,
            Formation = context.Formation,
            Aggro = context.Aggro,
            Card = context.Card,
            Range = context.Range,
            Actions = context.Actions,
            Deck = context.Deck,
            CardDb = context.CardDb,
            ImplantBonusProvider = context.ImplantBonusProvider,
            LastDamageDealt = context.LastDamageDealt,
            TotalDamageDealtThisCard = context.TotalDamageDealtThisCard,
            LastSelfHpLoss = context.LastSelfHpLoss,
            TotalSelfHpLossThisCard = context.TotalSelfHpLossThisCard
        };
        _inner.Execute(selfContext);
        context.LastDamageDealt = selfContext.LastDamageDealt;
        context.TotalDamageDealtThisCard = selfContext.TotalDamageDealtThisCard;
        context.LastSelfHpLoss = selfContext.LastSelfHpLoss;
        context.TotalSelfHpLossThisCard = selfContext.TotalSelfHpLossThisCard;
    }

    public string GetDescription(CardEffectContext? context = null) =>
        _inner.GetDescription(context);
}

/// <summary>
/// Deal damage to one or more targets. Enqueues DealDamageAction for cascading hooks.
/// </summary>
public class DealDamageEffect : ICardEffect
{
    public int BaseDamage { get; }

    public DealDamageEffect(int baseDamage) => BaseDamage = baseDamage;

    public void Execute(CardEffectContext context)
    {
        if (context.Actions != null)
        {
            context.Actions.AddToBottom(new DealDamageAction(
                context.Source, context.AllTargets, BaseDamage,
                context.Range, context.Formation, context.Aggro));
        }
        else
        {
            // Fallback: direct execution (no action queue available)
            int damage = context.Source.CalculateAttackDamage(BaseDamage);
            float multiplier = context.Formation.GetDamageMultiplier(context.Source.Id, context.Range);
            damage = (int)(damage * multiplier);
            foreach (var target in context.AllTargets)
                target.TakeDamage(damage);
        }
    }

    public string GetDescription(CardEffectContext? context = null) =>
        $"造成 {BaseDamage} 点伤害";
}

/// <summary>
/// Gain block (armor). Enqueues GainBlockAction.
/// </summary>
public class GainBlockEffect : ICardEffect
{
    public int BaseBlock { get; }

    public GainBlockEffect(int baseBlock) => BaseBlock = baseBlock;

    public void Execute(CardEffectContext context)
    {
        if (context.Actions != null)
        {
            context.Actions.AddToBottom(new GainBlockAction(
                context.Source, BaseBlock, context.Formation));
        }
        else
        {
            int bonus = context.Formation.GetBlockBonus(context.Source.Id);
            context.Source.GainBlock(BaseBlock + bonus);
        }
    }

    public string GetDescription(CardEffectContext? context = null) =>
        $"获得 {BaseBlock} 点护甲";
}

/// <summary>
/// Draw cards from deck. Enqueues DrawCardAction so draw happens after current effect chain.
/// </summary>
public class DrawCardsEffect : ICardEffect
{
    public int Count { get; }

    public DrawCardsEffect(int count) => Count = count;

    public void Execute(CardEffectContext context)
    {
        if (context.Actions != null && context.Deck != null && context.Source is PlayerCharacter player)
        {
            context.Actions.AddToBottom(new DrawCardAction(player, Count, context.Deck));
        }
        // else: no action queue available (unit tests) — silently no-op.
    }

    public string GetDescription(CardEffectContext? context = null) =>
        $"抽 {Count} 张牌";
}

/// <summary>
/// Apply a status/power to target(s). Enqueues ApplyPowerAction.
/// </summary>
public class ApplyStatusEffect : ICardEffect
{
    public StatusType StatusType { get; }
    public int Stacks { get; }
    public int Duration { get; }

    public ApplyStatusEffect(StatusType type, int stacks, int duration = -1)
    {
        StatusType = type;
        Stacks = stacks;
        Duration = duration;
    }

    public void Execute(CardEffectContext context)
    {
        foreach (var target in context.AllTargets)
        {
            if (context.Actions != null)
            {
                var power = PowerFactory.CreateFromStatusType(StatusType, Stacks);
                context.Actions.AddToBottom(new ApplyPowerAction(target, power, context.Source));
            }
            else
            {
                target.StatusEffects.Apply(StatusType, Stacks, Duration);
            }
        }
    }

    public string GetDescription(CardEffectContext? context = null) =>
        $"施加 {Stacks} 层 {StatusType}";
}

/// <summary>
/// Gain energy this turn. Enqueues GainEnergyAction.
/// </summary>
public class GainEnergyEffect : ICardEffect
{
    public int Amount { get; }

    public GainEnergyEffect(int amount) => Amount = amount;

    public void Execute(CardEffectContext context)
    {
        if (context.Actions != null && context.Source is PlayerCharacter player)
        {
            context.Actions.AddToBottom(new GainEnergyAction(player, Amount));
        }
        else if (context.Source is PlayerCharacter p)
        {
            p.CurrentEnergy += Amount;
        }
    }

    public string GetDescription(CardEffectContext? context = null) =>
        $"获得 {Amount} 点能量";
}

/// <summary>
/// Heal target(s). Enqueues HealAction.
/// </summary>
public class HealEffect : ICardEffect
{
    public int Amount { get; }

    public HealEffect(int amount) => Amount = amount;

    public void Execute(CardEffectContext context)
    {
        foreach (var target in context.AllTargets)
        {
            if (context.Actions != null)
            {
                context.Actions.AddToBottom(new HealAction(target, Amount));
            }
            else
            {
                int healed = target.Heal(Amount);
                context.Aggro.AddAggro(context.Source.Id, healed * AggroSystem.AggroPerHealing);
            }
        }
    }

    public string GetDescription(CardEffectContext? context = null) =>
        $"回复 {Amount} 点生命值";
}

/// <summary>
/// Force target to switch formation row. Enqueues ForceRepositionAction.
/// </summary>
public class ForceRepositionEffect : ICardEffect
{
    public FormationRow TargetRow { get; }

    public ForceRepositionEffect(FormationRow targetRow) => TargetRow = targetRow;

    public void Execute(CardEffectContext context)
    {
        foreach (var target in context.AllTargets)
        {
            if (context.Actions != null)
                context.Actions.AddToBottom(new ForceRepositionAction(target, TargetRow, context.Formation));
            else
                context.Formation.ForcePosition(target.Id, TargetRow);
        }
    }

    public string GetDescription(CardEffectContext? context = null) =>
        $"将目标移至{(TargetRow == FormationRow.Front ? "前排" : "后排")}";
}

/// <summary>
/// Repositions the SOURCE combatant to a specific row (the player itself moves).
/// </summary>
public class SelfRepositionEffect : ICardEffect
{
    public FormationRow? TargetRow { get; }
    public bool Toggle { get; }

    public SelfRepositionEffect(FormationRow targetRow) { TargetRow = targetRow; }
    public SelfRepositionEffect() { Toggle = true; }

    public void Execute(CardEffectContext context)
    {
        if (Toggle)
        {
            var curr = context.Formation.GetPosition(context.Source.Id);
            context.Formation.ForcePosition(context.Source.Id,
                curr == FormationRow.Front ? FormationRow.Back : FormationRow.Front);
        }
        else
        {
            context.Formation.ForcePosition(context.Source.Id, TargetRow!.Value);
        }
    }

    public string GetDescription(CardEffectContext? context = null) =>
        Toggle ? "切换自身站位" :
            $"将自身移至{(TargetRow == FormationRow.Front ? "前排" : "后排")}";
}

/// <summary>
/// Deal damage then position-switch the source. Enqueues DashDamageAction.
/// </summary>
public class DashDamageEffect : ICardEffect
{
    public int BaseDamage { get; }
    public bool ReturnToBack { get; }

    public DashDamageEffect(int baseDamage, bool returnToBack = true)
    {
        BaseDamage = baseDamage;
        ReturnToBack = returnToBack;
    }

    public void Execute(CardEffectContext context)
    {
        if (context.Actions != null)
        {
            context.Actions.AddToBottom(new DashDamageAction(
                context.Source, context.AllTargets, BaseDamage,
                ReturnToBack, context.Formation, context.Aggro));
        }
        else
        {
            int damage = context.Source.CalculateAttackDamage(BaseDamage);
            foreach (var target in context.AllTargets)
                target.TakeDamage(damage);
            var currentRow = context.Formation.GetPosition(context.Source.Id);
            var destRow = ReturnToBack ? FormationRow.Back : FormationRow.Front;
            if (currentRow != destRow)
                context.Formation.ForcePosition(context.Source.Id, destRow);
        }
    }

    public string GetDescription(CardEffectContext? context = null) =>
        $"造成 {BaseDamage} 点伤害，随后{(ReturnToBack ? "退入后排" : "突入前排")}";
}

/// <summary>
/// Consume all Overcharge stacks and deal bonus damage per stack. Enqueues OverchargeConsumeAction.
/// </summary>
public class OverchargeConsumeEffect : ICardEffect
{
    public int DamagePerStack { get; }
    public int BaseDamage { get; }

    public OverchargeConsumeEffect(int baseDamage, int damagePerStack = 3)
    {
        BaseDamage = baseDamage;
        DamagePerStack = damagePerStack;
    }

    public void Execute(CardEffectContext context)
    {
        if (context.Actions != null)
        {
            context.Actions.AddToBottom(new OverchargeConsumeAction(
                context.Source, context.AllTargets, BaseDamage, DamagePerStack,
                context.Range, context.Formation, context.Aggro));
        }
        else
        {
            int stacks = context.Source.StatusEffects.GetStacks(StatusType.Overcharge);
            int totalDamage = BaseDamage + stacks * DamagePerStack;
            if (stacks > 0) context.Source.StatusEffects.Remove(StatusType.Overcharge);
            totalDamage = context.Source.CalculateAttackDamage(totalDamage);
            float multiplier = context.Formation.GetDamageMultiplier(context.Source.Id, context.Range);
            totalDamage = (int)(totalDamage * multiplier);
            foreach (var target in context.AllTargets)
                target.TakeDamage(totalDamage);
        }
    }

    public string GetDescription(CardEffectContext? context = null) =>
        $"造成 {BaseDamage}+超载层数×{DamagePerStack} 点伤害，消耗所有超载";
}

/// <summary>
/// Add a copy of a named card to the discard pile (curse/status card mechanic).
/// </summary>
public class AddToDiscardEffect : ICardEffect
{
    public string CardId { get; }

    public AddToDiscardEffect(string cardId) => CardId = cardId;

    public void Execute(CardEffectContext context)
    {
        if (context.Actions != null && context.Source is PlayerCharacter player)
            context.Actions.AddToBottom(new AddCardToDiscardAction(player, CardId, context.CardDb, context.Deck));
    }

    public string GetDescription(CardEffectContext? context = null) =>
        $"将 [{CardId}] 加入弃牌堆";
}

// =====================================================================
//  PSION: RESONANCE EFFECTS
// =====================================================================

/// <summary>Deal damage = baseDamage + Resonance stacks × multiplier.</summary>
public class ResonanceDamageEffect : ICardEffect
{
    public int BaseDamage { get; }
    public double Multiplier { get; }

    public ResonanceDamageEffect(int baseDamage, double multiplier = 1)
    {
        BaseDamage = baseDamage;
        Multiplier = multiplier;
    }

    public void Execute(CardEffectContext context)
    {
        int resonance = context.Source.Powers.GetStacks(CommonPowerIds.Resonance);
        int totalDamage = BaseDamage + (int)(resonance * Multiplier);
        if (context.Actions != null)
            context.Actions.AddToBottom(new DealDamageAction(
                context.Source, context.AllTargets, totalDamage,
                context.Range, context.Formation, context.Aggro));
    }

    public string GetDescription(CardEffectContext? context = null)
    {
        int res = context?.Source.Powers.GetStacks(CommonPowerIds.Resonance) ?? 0;
        return BaseDamage > 0
            ? $"造成 {BaseDamage}+共鸣×{Multiplier} ({BaseDamage + res * Multiplier}) 点伤害"
            : $"造成 共鸣×{Multiplier} ({res * Multiplier}) 点伤害";
    }
}

/// <summary>Gain block = Resonance stacks × multiplier.</summary>
public class ResonanceBlockEffect : ICardEffect
{
    public double Multiplier { get; }
    public ResonanceBlockEffect(double multiplier) => Multiplier = multiplier;

    public void Execute(CardEffectContext context)
    {
        int resonance = context.Source.Powers.GetStacks(CommonPowerIds.Resonance);
        int block = (int)(resonance * Multiplier);
        if (block > 0 && context.Actions != null)
            context.Actions.AddToBottom(new GainBlockAction(context.Source, block, context.Formation));
    }

    public string GetDescription(CardEffectContext? context = null)
    {
        int res = context?.Source.Powers.GetStacks(CommonPowerIds.Resonance) ?? 0;
        return $"获得 共鸣×{Multiplier} ({res * Multiplier}) 护甲";
    }
}

/// <summary>Consume all Resonance stacks and deal bonus damage per stack.</summary>
public class ConsumeResonanceEffect : ICardEffect
{
    public int DamagePerStack { get; }
    public ConsumeResonanceEffect(int damagePerStack) => DamagePerStack = damagePerStack;

    public void Execute(CardEffectContext context)
    {
        int stacks = context.Source.Powers.GetStacks(CommonPowerIds.Resonance);
        if (stacks > 0)
        {
            int damage = stacks * DamagePerStack;
            damage = (int)context.Source.Powers.ModifyConsumeResonanceDamage(damage, stacks);
            context.Source.Powers.RemovePower(CommonPowerIds.Resonance);
            if (context.Actions != null)
                context.Actions.AddToBottom(new DealDamageAction(
                    context.Source, context.AllTargets, damage,
                    context.Range, context.Formation, context.Aggro));
        }
    }

    public string GetDescription(CardEffectContext? context = null) =>
        $"消耗所有共鸣，每层额外 +{DamagePerStack} 伤害";
}

/// <summary>Halve Resonance stacks (round down).</summary>
public class HalveResonanceEffect : ICardEffect
{
    public void Execute(CardEffectContext context)
    {
        var power = context.Source.Powers.GetPower(CommonPowerIds.Resonance);
        if (power != null) power.Amount /= 2;
        if (power != null && power.Amount <= 0) context.Source.Powers.RemovePower(CommonPowerIds.Resonance);
    }
    public string GetDescription(CardEffectContext? context = null) => "共鸣减半";
}

/// <summary>Clear all Resonance stacks.</summary>
public class ClearResonanceEffect : ICardEffect
{
    public void Execute(CardEffectContext context) =>
        context.Source.Powers.RemovePower(CommonPowerIds.Resonance);
    public string GetDescription(CardEffectContext? context = null) => "共鸣归零";
}

/// <summary>Delayed resonance: gain stacks at the START of next turn via GainResonanceNextTurnPower trigger.</summary>
public class DelayedResonanceEffect : ICardEffect
{
    public int Amount { get; }
    public DelayedResonanceEffect(int amount) => Amount = amount;

    public void Execute(CardEffectContext context)
    {
        // Apply a trigger power that fires at the START of the next turn, converting to Resonance.
        var power = new GainResonanceNextTurnPower { Amount = Amount };
        context.Actions?.AddToBottom(new ApplyPowerAction(context.Source, power));
    }

    public string GetDescription(CardEffectContext? context = null) =>
        $"下回合开始时获得 {Amount} 层共鸣";
}

/// <summary>Conditional damage: if condition met, deal bonus damage instead of base.</summary>
public class ConditionalDamageEffect : ICardEffect
{
    public int BaseDamage { get; }
    public int BonusDamage { get; }
    public string Condition { get; }

    public ConditionalDamageEffect(int baseDamage, int bonusDamage, string condition)
    {
        BaseDamage = baseDamage;
        BonusDamage = bonusDamage;
        Condition = condition;
    }

    public void Execute(CardEffectContext context)
    {
        bool condMet = EvaluateCondition(context);
        int damage = condMet ? BonusDamage : BaseDamage;
        context.Actions?.AddToBottom(new DealDamageAction(
            context.Source, context.AllTargets, damage,
            context.Range, context.Formation, context.Aggro));
    }

    private bool EvaluateCondition(CardEffectContext context) =>
        ConditionEvaluator.Evaluate(Condition, context);

    public string GetDescription(CardEffectContext? context = null) =>
        $"造成 {BaseDamage} 伤害 (条件满足: {BonusDamage})";
}

/// <summary>Scry: look at top N cards. Simplified as draw for now.</summary>
public class ScryEffect : ICardEffect
{
    public int Count { get; }
    public ScryEffect(int count) => Count = count;

    public void Execute(CardEffectContext context)
    {
        // Simplified: just draw cards (full scry UI would need modal)
        int drawCount = Count > 2 ? 1 : Count;
        TryDraw(context, drawCount);
    }

    internal static void TryDraw(CardEffectContext context, int count)
    {
        if (count <= 0) return;
        if (context.Actions != null && context.Deck != null && context.Source is PlayerCharacter player)
            context.Actions.AddToBottom(new DrawCardAction(player, count, context.Deck));
    }
    public string GetDescription(CardEffectContext? context = null) =>
        $"查看抽牌堆顶 {Count} 张牌";
}

/// <summary>Apply a named power by ID (for class-specific powers like ResonanceCascade, NeuralNetwork).</summary>
public class ApplyPowerByIdEffect : ICardEffect
{
    public string PowerIdStr { get; }
    public int Amount { get; }

    public ApplyPowerByIdEffect(string powerId, int amount)
    {
        PowerIdStr = powerId;
        Amount = amount;
    }

    public void Execute(CardEffectContext context)
    {
        AbstractPower? power = PowerIdStr switch
        {
            "ResonanceCascade" => new ResonanceCascadePower { Amount = Amount },
            "NeuralNetwork" => new NeuralNetworkPower { Amount = Amount },
            "ApexPredator" => new ApexPredatorPower { Amount = Amount },
            "FrontlineCommander" => new FrontlineCommanderPower { Amount = Amount > 0 ? Amount : 1 },
            "WarMachine" => new WarMachinePower { Amount = Amount > 0 ? Amount : 1 },
            "ResonanceEngine" => new ResonanceEnginePower { Amount = Amount > 0 ? Amount : 1 },
            "TranscendentPsion" => new TranscendentPsionPower { Amount = 1 },
            "MindSovereign" => new MindSovereignPower { Amount = 1 },
            "PassiveScan" => new PassiveScanPower { Amount = 1 },
            "Firewall" => new FirewallAbilityPower { Amount = 1 },
            "PersistentLink" => new PersistentLinkPower { Amount = 1 },
            "TacticalErosion" => new TacticalErosionPower { Amount = 1 },
            "BloodRitual" => new BloodRitualPower { Amount = 1 },
            _ => null
        };
        if (power != null)
            context.Actions?.AddToBottom(new ApplyPowerAction(context.Source, power));
        else
            EffectLog.Warn($"[ApplyPowerByIdEffect] Unknown PowerId: \"{PowerIdStr}\"");
    }

    public string GetDescription(CardEffectContext? context = null) =>
        $"获得永久效果: {PowerIdStr}";
}

// =====================================================================
//  NETRUNNER: PROTOCOL / HACK EFFECTS
// =====================================================================

/// <summary>Add protocol stacks to the player.</summary>
public class ProtocolStackEffect : ICardEffect
{
    public int Amount { get; }
    public ProtocolStackEffect(int amount) => Amount = amount;

    public void Execute(CardEffectContext context)
    {
        var power = new ProtocolStackPower { Amount = Amount };
        context.Actions?.AddToBottom(new ApplyPowerAction(context.Source, power));
    }
    public string GetDescription(CardEffectContext? context = null) =>
        $"协议栈 +{Amount}";
}

/// <summary>Apply hack/intrusion stacks to target enemies.</summary>
public class HackEffect : ICardEffect
{
    public int Amount { get; }
    public HackEffect(int amount) => Amount = amount;

    public void Execute(CardEffectContext context)
    {
        int speedBonus = context.ImplantBonusProvider?.Invoke("hackSpeedBonus") ?? 0;
        int finalAmount = speedBonus == 0
            ? Amount
            : Amount + (int)MathF.Ceiling(Amount * speedBonus / 100f);

        foreach (var target in context.AllTargets)
        {
            var power = new HackedPower { Amount = finalAmount };
            context.Actions?.AddToBottom(new ApplyPowerAction(target, power));
        }
    }
    public string GetDescription(CardEffectContext? context = null) =>
        $"施加 {Amount} 层入侵进度";
}

/// <summary>Consume all protocol stacks and deal damage per stack.</summary>
public class ConsumeProtocolEffect : ICardEffect
{
    public int DamagePerStack { get; }
    public ConsumeProtocolEffect(int damagePerStack) => DamagePerStack = damagePerStack;

    public void Execute(CardEffectContext context)
    {
        int stacks = context.Source.Powers.GetStacks(CommonPowerIds.ProtocolStack);
        if (stacks > 0)
        {
            context.Source.Powers.RemovePower(CommonPowerIds.ProtocolStack);
            int damage = stacks * DamagePerStack;
            context.Actions?.AddToBottom(new DealDamageAction(
                context.Source, context.AllTargets, damage,
                context.Range, context.Formation, context.Aggro));
        }
    }
    public string GetDescription(CardEffectContext? context = null)
    {
        int stacks = context?.Source.Powers.GetStacks(CommonPowerIds.ProtocolStack) ?? 0;
        return $"消耗协议栈，每层造成 {DamagePerStack} 伤害 (当前: {stacks * DamagePerStack})";
    }
}

/// <summary>Deal damage = protocol stacks × multiplier (optionally consuming).</summary>
public class ProtocolDamageEffect : ICardEffect
{
    public int DamagePerStack { get; }
    public bool ConsumeStacks { get; }

    public ProtocolDamageEffect(int damagePerStack, bool consume)
    {
        DamagePerStack = damagePerStack;
        ConsumeStacks = consume;
    }

    public void Execute(CardEffectContext context)
    {
        int stacks = context.Source.Powers.GetStacks(CommonPowerIds.ProtocolStack);
        int damage = stacks * DamagePerStack;
        if (ConsumeStacks && stacks > 0)
            context.Source.Powers.RemovePower(CommonPowerIds.ProtocolStack);
        if (damage > 0)
            context.Actions?.AddToBottom(new DealDamageAction(
                context.Source, context.AllTargets, damage,
                context.Range, context.Formation, context.Aggro));
    }
    public string GetDescription(CardEffectContext? context = null) =>
        $"造成 协议栈×{DamagePerStack} 伤害" + (ConsumeStacks ? " (消耗)" : "");
}

/// <summary>Double protocol stacks up to a cap.</summary>
public class DoubleProtocolEffect : ICardEffect
{
    public int Cap { get; }
    public DoubleProtocolEffect(int cap) => Cap = cap;

    public void Execute(CardEffectContext context)
    {
        var power = context.Source.Powers.GetPower(CommonPowerIds.ProtocolStack);
        if (power is ProtocolStackPower protocolPower)
        {
            power.Amount = Math.Min(power.Amount * 2, Math.Min(Cap, protocolPower.MaxStacks));
        }
        else if (power != null)
        {
            power.Amount = Math.Min(power.Amount * 2, Cap);
        }
    }
    public string GetDescription(CardEffectContext? context = null) =>
        $"协议栈翻倍 (上限{Cap})";
}

/// <summary>Instantly hack a target (bypass progress).</summary>
public class InstantHackEffect : ICardEffect
{
    public int Duration { get; }
    public InstantHackEffect(int duration) => Duration = duration;

    public void Execute(CardEffectContext context)
    {
        foreach (var target in context.AllTargets)
        {
            var power = new StunnedPower { Amount = Duration };
            context.Actions?.AddToBottom(new ApplyPowerAction(target, power));
        }
    }
    public string GetDescription(CardEffectContext? context = null) =>
        $"目标立即被入侵 (持续{Duration}回合)";
}

// =====================================================================
//  SYMBIOTE: SELF-DAMAGE / LIFESTEAL EFFECTS
// =====================================================================

/// <summary>
/// Lose HP (flat amount). Bypasses block, cannot kill. Enqueues SelfDamageAction.
/// </summary>
public class SelfDamageEffect : ICardEffect
{
    public int Amount { get; }
    public SelfDamageEffect(int amount) => Amount = amount;

    public void Execute(CardEffectContext context)
    {
        int loss = Amount;
        if ((context.ImplantBonusProvider?.Invoke("erosionSelfDamageHalf") ?? 0) > 0)
            loss = Math.Max(1, loss / 2);

        if (context.Actions != null)
            context.Actions.AddToBottom(new SelfDamageAction(context.Source, loss));
        else
            context.Source.CurrentHp = Math.Max(1, context.Source.CurrentHp - loss);
    }
    public string GetDescription(CardEffectContext? context = null) =>
        $"失去 {Amount} HP";
}

/// <summary>
/// Lose HP equal to a percentage of current HP. Enqueues SelfDamageAction.
/// </summary>
public class SelfDamagePercentEffect : ICardEffect
{
    public int Percent { get; }
    public SelfDamagePercentEffect(int percent) => Percent = percent;

    public void Execute(CardEffectContext context)
    {
        int loss = context.Source.CurrentHp * Percent / 100;
        if (loss <= 0) return;

        if ((context.ImplantBonusProvider?.Invoke("erosionSelfDamageHalf") ?? 0) > 0)
            loss = Math.Max(1, loss / 2);

        if (context.Actions != null)
            context.Actions.AddToBottom(new SelfDamageAction(context.Source, loss));
        else
            context.Source.CurrentHp = Math.Max(1, context.Source.CurrentHp - loss);
    }
    public string GetDescription(CardEffectContext? context = null) =>
        $"失去当前HP的 {Percent}%";
}

/// <summary>Heal based on last damage dealt (percentage). Simplified: heal flat from recent damage context.</summary>
public class LifestealEffect : ICardEffect
{
    public int Percent { get; }
    public LifestealEffect(int percent) => Percent = percent;

    public void Execute(CardEffectContext context)
    {
        int dealt = context.LastDamageDealt;
        if (dealt <= 0) return;

        int heal = Math.Max(1, dealt * Percent / 100);
        context.Actions?.AddToBottom(new HealAction(context.Source, heal));
    }
    public string GetDescription(CardEffectContext? context = null) =>
        $"回复造成伤害的 {Percent}% HP";
}

/// <summary>Spread poison from target to adjacent enemies.</summary>
public class SpreadPoisonEffect : ICardEffect
{
    public int Amount { get; }
    public SpreadPoisonEffect(int amount) => Amount = amount;

    public void Execute(CardEffectContext context)
    {
        // Apply poison to all targets (spread to others in the target list)
        foreach (var target in context.AllTargets)
        {
            var power = new PoisonPower { Amount = Amount };
            context.Actions?.AddToBottom(new ApplyPowerAction(target, power));
        }
    }
    public string GetDescription(CardEffectContext? context = null) =>
        $"毒素扩散 {Amount} 层到相邻敌人";
}

/// <summary>Conditional block: gain extra block if condition met.</summary>
public class ConditionalBlockEffect : ICardEffect
{
    public string Condition { get; }
    public int Amount { get; }

    public ConditionalBlockEffect(string condition, int amount)
    {
        Condition = condition;
        Amount = amount;
    }

    public void Execute(CardEffectContext context)
    {
        if (ConditionEvaluator.Evaluate(Condition, context))
            context.Actions?.AddToBottom(new GainBlockAction(context.Source, Amount, context.Formation));
    }
    public string GetDescription(CardEffectContext? context = null) =>
        $"条件满足时额外获得 {Amount} 护甲";
}

/// <summary>Deal damage based on HP lost this card play (for Pandemonium).</summary>
public class DamageFromHpLostEffect : ICardEffect
{
    public double Multiplier { get; }
    public DamageFromHpLostEffect(double multiplier) => Multiplier = multiplier;

    public void Execute(CardEffectContext context)
    {
        int hpLost = context.TotalSelfHpLossThisCard;
        int damage = Math.Max(1, (int)(hpLost * Multiplier));

        int bonus = context.ImplantBonusProvider?.Invoke("erosionBonusDamage") ?? 0;
        if (bonus > 0)
            damage = (int)MathF.Ceiling(damage * (100 + bonus) / 100f);

        context.Actions?.AddToBottom(new DealDamageAction(
            context.Source, context.AllTargets, damage,
            context.Range, context.Formation, context.Aggro));
    }
    public string GetDescription(CardEffectContext? context = null) =>
        $"造成失去HP×{Multiplier}的伤害";
}

/// <summary>Convert current HP percentage to max HP. Enqueues ModifyMaxHpAction.</summary>
public class HpToMaxHpEffect : ICardEffect
{
    public int Percent { get; }
    public HpToMaxHpEffect(int percent) => Percent = percent;

    public void Execute(CardEffectContext context)
    {
        int gain = context.Source.CurrentHp * Percent / 100;
        if (gain <= 0) return;
        if (context.Actions != null)
        {
            // First queue: lose current HP (self-damage, bypasses block)
            context.Actions.AddToBottom(new SelfDamageAction(context.Source, gain));
            // Then: increase max HP
            context.Actions.AddToBottom(new ModifyMaxHpAction(context.Source, gain));
        }
        else
        {
            context.Source.CurrentHp -= gain;
            context.Source.MaxHp += gain;
        }
    }
    public string GetDescription(CardEffectContext? context = null) =>
        $"将当前HP的{Percent}%转化为最大HP";
}

/// <summary>Row-conditional effect from JSON data (frontEffect/backEffect).</summary>
public class RowConditionalFromDataEffect : ICardEffect
{
    private readonly CardEffectData? _frontData;
    private readonly CardEffectData? _backData;

    public RowConditionalFromDataEffect(CardEffectData? frontData, CardEffectData? backData)
    {
        _frontData = frontData;
        _backData = backData;
    }

    public void Execute(CardEffectContext context)
    {
        var row = context.Formation.GetPosition(context.Source.Id);
        var data = row == FormationRow.Front ? _frontData : _backData;
        if (data != null)
            CardEffectFactory.Create(data).Execute(context);
    }

    public string GetDescription(CardEffectContext? context = null) =>
        "根据站位执行不同效果";
}

// =====================================================================
//  ADDITIONAL EFFECT TYPES (from new card data)
// =====================================================================

/// <summary>Gain block equal to Overcharge stacks × perStack.</summary>
public class BlockPerOverchargeEffect : ICardEffect
{
    public int PerStack { get; }
    public BlockPerOverchargeEffect(int perStack) => PerStack = perStack;

    public void Execute(CardEffectContext context)
    {
        int stacks = context.Source.StatusEffects.GetStacks(StatusType.Overcharge);
        int block = stacks * PerStack;
        if (block > 0)
            context.Actions?.AddToBottom(new GainBlockAction(context.Source, block, context.Formation));
    }
    public string GetDescription(CardEffectContext? context = null) =>
        $"每层超载获得 {PerStack} 点护甲";
}

/// <summary>Gain block equal to Resonance stacks × perStack.</summary>
public class BlockPerResonanceEffect : ICardEffect
{
    public int PerStack { get; }
    public BlockPerResonanceEffect(int perStack) => PerStack = perStack;

    public void Execute(CardEffectContext context)
    {
        int stacks = context.Source.Powers.GetStacks(CommonPowerIds.Resonance);
        int block = stacks * PerStack;
        if (block > 0)
            context.Actions?.AddToBottom(new GainBlockAction(context.Source, block, context.Formation));
    }
    public string GetDescription(CardEffectContext? context = null) =>
        $"每层共鸣获得 {PerStack} 点护甲";
}

/// <summary>Draw cards if a condition is met (e.g., hasOvercharge).</summary>
public class ConditionalDrawEffect : ICardEffect
{
    public string Condition { get; }
    public int Amount { get; }
    public ConditionalDrawEffect(string condition, int amount)
    {
        Condition = condition;
        Amount = amount;
    }

    public void Execute(CardEffectContext context)
    {
        bool met = ConditionEvaluator.Evaluate(Condition, context);
        if (met)
            ScryEffect.TryDraw(context, Amount);
    }
    public string GetDescription(CardEffectContext? context = null) =>
        $"条件满足时抽 {Amount} 张牌";
}

/// <summary>Gain energy if a condition is met.</summary>
public class ConditionalEnergyEffect : ICardEffect
{
    public string Condition { get; }
    public int Amount { get; }
    public ConditionalEnergyEffect(string condition, int amount)
    {
        Condition = condition;
        Amount = amount;
    }

    public void Execute(CardEffectContext context)
    {
        bool met = ConditionEvaluator.Evaluate(Condition, context);
        if (met && context.Source is PlayerCharacter player)
            context.Actions?.AddToBottom(new GainEnergyAction(player, Amount));
    }
    public string GetDescription(CardEffectContext? context = null) =>
        $"条件满足时获得 {Amount} 点能量";
}

/// <summary>Apply a status effect if a condition is met.</summary>
public class ConditionalStatusEffect : ICardEffect
{
    public string Condition { get; }
    public StatusType StatusType { get; }
    public int Amount { get; }
    public ConditionalStatusEffect(string condition, StatusType statusType, int amount)
    {
        Condition = condition;
        StatusType = statusType;
        Amount = amount;
    }

    public void Execute(CardEffectContext context)
    {
        bool met = ConditionEvaluator.Evaluate(Condition, context);
        if (met)
        {
            foreach (var target in context.AllTargets)
            {
                var power = PowerFactory.CreateFromStatusType(StatusType, Amount);
                context.Actions?.AddToBottom(new ApplyPowerAction(target, power));
            }
        }
    }
    public string GetDescription(CardEffectContext? context = null) =>
        $"条件满足时施加 {Amount} 层状态";
}

/// <summary>Deal damage per debuff on the target.</summary>
public class DamagePerDebuffEffect : ICardEffect
{
    public int DamagePerDebuff { get; }
    public DamagePerDebuffEffect(int damagePerDebuff) => DamagePerDebuff = damagePerDebuff;

    public void Execute(CardEffectContext context)
    {
        foreach (var target in context.AllTargets)
        {
            int debuffCount = target.Powers.Powers
                .Count(p => p.IsDebuff);
            int damage = debuffCount * DamagePerDebuff;
            if (damage > 0)
                context.Actions?.AddToBottom(new DealDamageAction(
                    context.Source, [target], damage,
                    context.Range, context.Formation, context.Aggro));
        }
    }
    public string GetDescription(CardEffectContext? context = null) =>
        $"每个负面状态造成 {DamagePerDebuff} 点伤害";
}

/// <summary>Deal damage per poisoned enemy on the battlefield.</summary>
public class DamagePerPoisonedEnemyEffect : ICardEffect
{
    public int DamagePerPoisoned { get; }
    public DamagePerPoisonedEnemyEffect(int damagePerPoisoned) => DamagePerPoisoned = damagePerPoisoned;

    public void Execute(CardEffectContext context)
    {
        // Count ALL enemies on field (not just targeted ones) — card text: "场上每个中毒的敌人"
        var enemies = context.AllEnemies ?? context.AllTargets;
        int poisoned = enemies.Count(t =>
            t.Powers.GetStacks(CommonPowerIds.Poison) > 0);
        int damage = poisoned * DamagePerPoisoned;
        if (damage > 0)
            context.Actions?.AddToBottom(new DealDamageAction(
                context.Source, context.AllTargets, damage,
                context.Range, context.Formation, context.Aggro));
    }
    public string GetDescription(CardEffectContext? context = null) =>
        $"每个中毒敌人造成 {DamagePerPoisoned} 点伤害";
}

/// <summary>Dominate a marked enemy — force it to attack its allies.</summary>
public class DominateMarkedEffect : ICardEffect
{
    public int Percent { get; }
    public DominateMarkedEffect(int percent) => Percent = percent;

    public void Execute(CardEffectContext context)
    {
        // Simplified: deal damage to all enemies equal to target's attack * percent
        // (full implementation would swap target to player side for 1 turn)
        foreach (var target in context.AllTargets)
        {
            // Check if target has any "marked"-like debuff (Vulnerable as proxy)
            if (target.StatusEffects.GetStacks(StatusType.Vulnerable) > 0)
            {
                int dominatedDmg = target.CalculateAttackDamage(0) * Percent / 100;
                if (dominatedDmg > 0)
                {
                    // Target attacks its allies (deal damage to other enemies)
                    var allies = context.AllTargets.Where(t => t.Id != target.Id).ToList();
                    if (allies.Count > 0)
                        context.Actions?.AddToBottom(new DealDamageAction(
                            target, allies, dominatedDmg,
                            CardRange.None, context.Formation, context.Aggro));
                    // Remove the mark
                    target.StatusEffects.Remove(StatusType.Vulnerable);
                }
            }
        }
    }
    public string GetDescription(CardEffectContext? context = null) =>
        $"控制被标记敌人攻击其盟友 ({Percent}% 攻击力)";
}

/// <summary>Apply hack stacks to target when it acts (simplified: apply immediately as a trap).</summary>
public class HackOnActionEffect : ICardEffect
{
    public int Amount { get; }
    public HackOnActionEffect(int amount) => Amount = amount;

    public void Execute(CardEffectContext context)
    {
        foreach (var target in context.AllTargets)
        {
            var power = new HackedPower { Amount = Amount };
            context.Actions?.AddToBottom(new ApplyPowerAction(target, power));
        }
    }
    public string GetDescription(CardEffectContext? context = null) =>
        $"目标行动时额外施加 {Amount} 层入侵";
}

/// <summary>Apply hack stacks equal to a percentage of damage dealt.</summary>
public class HackPercentOfDamageEffect : ICardEffect
{
    public int Percent { get; }
    public HackPercentOfDamageEffect(int percent) => Percent = percent;

    public void Execute(CardEffectContext context)
    {
        int dealt = context.LastDamageDealt;
        int hackStacks = dealt * Percent / 100;
        if (hackStacks <= 0) return;
        foreach (var target in context.AllTargets)
        {
            var power = new HackedPower { Amount = hackStacks };
            context.Actions?.AddToBottom(new ApplyPowerAction(target, power));
        }
    }
    public string GetDescription(CardEffectContext? context = null) =>
        $"施加等同于伤害 {Percent}% 的入侵层数";
}

/// <summary>Heal when you kill an enemy (applied as a power).</summary>
public class HealOnKillEffect : ICardEffect
{
    public int Amount { get; }
    public HealOnKillEffect(int amount) => Amount = amount;

    public void Execute(CardEffectContext context)
    {
        // Apply a persistent power that heals on kill
        var power = new HealOnKillPower { Amount = Amount };
        context.Actions?.AddToBottom(new ApplyPowerAction(context.Source, power));
    }
    public string GetDescription(CardEffectContext? context = null) =>
        $"击杀时回复 {Amount} 点生命";
}

/// <summary>Heal based on poison damage dealt this turn.</summary>
public class HealPerPoisonDamageEffect : ICardEffect
{
    public int HealPerStack { get; }
    public HealPerPoisonDamageEffect(int healPerStack) => HealPerStack = healPerStack;

    public void Execute(CardEffectContext context)
    {
        // Count poison stacks on all enemies, heal that amount
        int totalPoison = context.AllTargets.Sum(t =>
            t.Powers.GetStacks(CommonPowerIds.Poison));
        int heal = totalPoison * HealPerStack;
        if (heal > 0)
            context.Actions?.AddToBottom(new HealAction(context.Source, heal));
    }
    public string GetDescription(CardEffectContext? context = null) =>
        $"根据毒素层数回复生命 (每层 {HealPerStack})";
}

/// <summary>Deal damage that ignores block/armor.</summary>
public class PiercingDamageEffect : ICardEffect
{
    public int Damage { get; }
    public PiercingDamageEffect(int damage) => Damage = damage;

    public void Execute(CardEffectContext context)
    {
        foreach (var target in context.AllTargets)
        {
            // Direct HP loss, bypasses block
            int finalDmg = context.Source.CalculateAttackDamage(Damage);
            float multiplier = context.Formation.GetDamageMultiplier(context.Source.Id, context.Range);
            finalDmg = (int)(finalDmg * multiplier);
            target.CurrentHp = Math.Max(0, target.CurrentHp - finalDmg);
            context.LastDamageDealt = finalDmg;
        }
    }
    public string GetDescription(CardEffectContext? context = null) =>
        $"造成 {Damage} 点无视护甲的伤害";
}

/// <summary>Remove debuffs from self.</summary>
public class PurgeDebuffsEffect : ICardEffect
{
    public int Count { get; }
    public PurgeDebuffsEffect(int count) => Count = count;

    public void Execute(CardEffectContext context)
    {
        var debuffs = context.Source.Powers.Powers
            .Where(p => p.IsDebuff)
            .Take(Count > 0 ? Count : int.MaxValue)
            .ToList();
        foreach (var debuff in debuffs)
            context.Source.Powers.RemovePower(debuff.PowerId);
    }
    public string GetDescription(CardEffectContext? context = null) =>
        Count > 0 ? $"移除 {Count} 个负面状态" : "移除所有负面状态";
}

/// <summary>Deal random damage between min and max values.</summary>
public class RandomDamageEffect : ICardEffect
{
    public int MinDamage { get; }
    public int MaxDamage { get; }
    private static readonly Random _rng = new();

    public RandomDamageEffect(int minDamage, int maxDamage)
    {
        MinDamage = minDamage;
        MaxDamage = maxDamage;
    }

    public void Execute(CardEffectContext context)
    {
        int damage = _rng.Next(MinDamage, MaxDamage + 1);
        context.Actions?.AddToBottom(new DealDamageAction(
            context.Source, context.AllTargets, damage,
            context.Range, context.Formation, context.Aggro));
    }
    public string GetDescription(CardEffectContext? context = null) =>
        $"造成 {MinDamage}~{MaxDamage} 点随机伤害";
}

/// <summary>Amplify next resonance consumption: each stack deals extra bonusPerStack damage.</summary>
public class ResonanceAmplifyEffect : ICardEffect
{
    public int BonusPerStack { get; }
    public ResonanceAmplifyEffect(int bonusPerStack) => BonusPerStack = bonusPerStack;

    public void Execute(CardEffectContext context)
    {
        // Apply a temporary power that boosts the next resonance consume
        var power = new ResonanceAmplifyPower { Amount = BonusPerStack };
        context.Actions?.AddToBottom(new ApplyPowerAction(context.Source, power));
    }
    public string GetDescription(CardEffectContext? context = null) =>
        $"下次消耗共鸣时每层额外 +{BonusPerStack} 伤害";
}

/// <summary>Retrieve cards from the discard pile into hand.</summary>
public class RetrieveFromDiscardEffect : ICardEffect
{
    public int Count { get; }
    public RetrieveFromDiscardEffect(int count) => Count = count;

    public void Execute(CardEffectContext context)
    {
        // Simplified: draw cards as proxy (full impl would need discard pile access)
        ScryEffect.TryDraw(context, Count);
    }
    public string GetDescription(CardEffectContext? context = null) =>
        $"从弃牌堆中将 {Count} 张牌加入手牌";
}

/// <summary>Search the deck for a specific card type.</summary>
public class SearchDeckEffect : ICardEffect
{
    public int Count { get; }
    public SearchDeckEffect(int count) => Count = count;

    public void Execute(CardEffectContext context)
    {
        // Simplified: draw and scry — full impl would need deck search UI
        ScryEffect.TryDraw(context, Count);
    }
    public string GetDescription(CardEffectContext? context = null) =>
        $"搜索牌库，选择 {Count} 张牌加入手牌";
}

/// <summary>Set HP to a percentage of max HP.</summary>
public class SetHpPercentEffect : ICardEffect
{
    public int Percent { get; }
    public SetHpPercentEffect(int percent) => Percent = percent;

    public void Execute(CardEffectContext context)
    {
        int targetHp = context.Source.MaxHp * Percent / 100;
        if (targetHp < context.Source.CurrentHp)
        {
            int loss = context.Source.CurrentHp - targetHp;
            context.Actions?.AddToBottom(new SelfDamageAction(context.Source, loss));
        }
        else
        {
            int heal = targetHp - context.Source.CurrentHp;
            context.Actions?.AddToBottom(new HealAction(context.Source, heal));
        }
    }
    public string GetDescription(CardEffectContext? context = null) =>
        $"将生命值设为最大值的 {Percent}%";
}

/// <summary>Switch the caster to the front row.</summary>
public class SwitchToFrontEffect : ICardEffect
{
    public void Execute(CardEffectContext context)
    {
        context.Formation.SetPosition(context.Source.Id, FormationRow.Front);
    }
    public string GetDescription(CardEffectContext? context = null) =>
        "切换至前排";
}

/// <summary>No-op effect for unknown types (logs warning, doesn't crash).</summary>
public class NoOpEffect : ICardEffect
{
    private readonly string _type;
    public NoOpEffect(string type) => _type = type;
    public void Execute(CardEffectContext context) =>
        EffectLog.Warn($"[CardEffect] Unimplemented effect type: {_type}");
    public string GetDescription(CardEffectContext? context = null) => $"[{_type}]";
}

/// <summary>
/// Multi-hit effect: executes inner effect multiple times.
/// Used by cards with "times" field (e.g., "hit 3 times").
/// </summary>
public class MultiHitEffect : ICardEffect
{
    private readonly ICardEffect _inner;
    private readonly int _times;

    public MultiHitEffect(ICardEffect inner, int times)
    {
        _inner = inner;
        _times = times;
    }

    public void Execute(CardEffectContext context)
    {
        for (int i = 0; i < _times; i++)
            _inner.Execute(context);
    }

    public string GetDescription(CardEffectContext? context = null) =>
        $"x{_times}: {_inner.GetDescription(context)}";
}

// =====================================================================
//  CLASS-SPECIFIC POWERS (used by ApplyPowerByIdEffect)
// =====================================================================

/// <summary>Psion: gain Resonance stacks at start of each turn.</summary>
public class ResonanceCascadePower : AbstractPower
{
    public override string PowerId => "ResonanceCascade";
    public override string Name => "共鸣级联";
    public override string GetDescription() => $"每回合开始时获得 {Amount} 层共鸣。";

    public override void AtTurnStart()
    {
        if (Owner != null)
            ActionManager?.AddToBottom(new ApplyPowerAction(Owner, new ResonancePower { Amount = Amount }));
    }
}

/// <summary>Netrunner: gain protocol stacks + draw at start of each turn.</summary>
public class NeuralNetworkPower : AbstractPower
{
    public override string PowerId => "NeuralNetwork";
    public override string Name => "神经网络";
    public override string GetDescription() => $"每回合开始时协议栈+{Amount}，抽1张牌。";

    public override void AtTurnStart()
    {
        if (Owner is not PlayerCharacter player) return;
        ActionManager?.AddToBottom(new ApplyPowerAction(player, new ProtocolStackPower { Amount = Amount }));
        if (ActionManager?.Combat?.PlayerDecks.TryGetValue(player.Id, out var deck) == true)
            ActionManager.AddToBottom(new DrawCardAction(player, 1, deck));
    }
}

/// <summary>Symbiote: gain strength per HP lost, heal on kill.</summary>
public class ApexPredatorPower : AbstractPower
{
    public override string PowerId => "ApexPredator";
    public override string Name => "顶级掠食者";
    public override string GetDescription() => $"每失去5HP获得1力量。击杀回复{Amount}HP。";
    private int _hpLostAccumulator;

    public override void OnTakeDamage(int amount, Combatant? attacker = null)
    {
        _hpLostAccumulator += amount;
        int strengthGain = _hpLostAccumulator / 5;
        if (strengthGain > 0 && Owner != null)
        {
            _hpLostAccumulator %= 5;
            ActionManager?.AddToBottom(new ApplyPowerAction(Owner, new StrengthPower { Amount = strengthGain }));
        }
    }

    public override void OnKill(Combatant victim)
    {
        if (Owner != null)
            ActionManager?.AddToBottom(new HealAction(Owner, Amount));
    }
}

/// <summary>
/// Factory that creates ICardEffect instances from CardEffectData.
/// </summary>
public static class CardEffectFactory
{
    private static readonly Dictionary<string, Func<CardEffectData, ICardEffect>> _registry =
        new(StringComparer.OrdinalIgnoreCase)
    {
        // === Core effects ===
        ["damage"] = d => new DealDamageEffect(d.Value),
        ["block"] = d => new GainBlockEffect(d.Value),
        ["armor"] = d => new GainBlockEffect(d.Value),
        ["draw"] = d => new DrawCardsEffect(d.Value),
        ["status"] = d => new ApplyStatusEffect(
            Enum.Parse<StatusType>(d.StatusId!, true),
            d.Value,
            d.SecondaryValue > 0 ? d.SecondaryValue : d.Duration > 0 ? d.Duration : -1),
        ["applystatus"] = d => new ApplyStatusEffect(
            Enum.Parse<StatusType>(d.StatusId!, true),
            d.Value,
            d.SecondaryValue > 0 ? d.SecondaryValue : d.Duration > 0 ? d.Duration : -1),
        ["energy"] = d => new GainEnergyEffect(d.Value),
        ["gainenergy"] = d => new GainEnergyEffect(d.Value),
        ["loseenergy"] = d => new GainEnergyEffect(-d.Value),
        ["heal"] = d => new HealEffect(d.Value),
        ["reposition"] = d => new ForceRepositionEffect(
            d.Value == 0 ? FormationRow.Front : FormationRow.Back),
        ["selfreposition"] = d => d.Value switch {
            0 => new SelfRepositionEffect(FormationRow.Front),
            1 => new SelfRepositionEffect(FormationRow.Back),
            _ => new SelfRepositionEffect()
        },
        ["dashdamage"] = d => new DashDamageEffect(d.Value, d.SecondaryValue != 0),
        ["overchargeconsume"] = d => new OverchargeConsumeEffect(d.Value, d.SecondaryValue > 0 ? d.SecondaryValue : 3),
        ["consumeovercharge"] = d => new OverchargeConsumeEffect(d.Value, d.SecondaryValue > 0 ? d.SecondaryValue : 3),
        ["addtodiscard"] = d => new AddToDiscardEffect(d.CardId ?? "glitch"),

        // === Psion: Resonance mechanics ===
        ["resonancedamage"] = d => new ResonanceDamageEffect(d.BaseDamage, d.Multiplier),
        ["resonanceblock"] = d => new ResonanceBlockEffect(d.Multiplier),
        ["consumeresonance"] = d => new ConsumeResonanceEffect(d.DamagePerStack),
        ["halveresonance"] = d => new HalveResonanceEffect(),
        ["clearresonance"] = d => new ClearResonanceEffect(),
        ["delayedresonance"] = d => new DelayedResonanceEffect(d.Value),
        ["conditionaldamage"] = d => new ConditionalDamageEffect(d.BaseDamage, d.BonusDamage, d.Condition ?? ""),
        ["scry"] = d => new ScryEffect(d.Value),
        ["applypower"] = d => new ApplyPowerByIdEffect(d.PowerId ?? "", d.Value),

        // === Netrunner: Protocol/Hack mechanics ===
        ["protocolstack"] = d => new ProtocolStackEffect(d.Value),
        ["hack"] = d => new HackEffect(d.Value),
        ["consumeprotocol"] = d => new ConsumeProtocolEffect(d.DamagePerStack),
        ["protocoldamage"] = d => new ProtocolDamageEffect(d.DamagePerStack, d.Consume),
        ["doubleprotocol"] = d => new DoubleProtocolEffect(d.Cap > 0 ? d.Cap : 10),
        ["instanthack"] = d => new InstantHackEffect(d.Duration > 0 ? d.Duration : 1),

        // === Symbiote: Self-damage/lifesteal mechanics ===
        ["selfdamage"] = d => new SelfDamageEffect(d.Value),
        ["selfdamagepercent"] = d => new SelfDamagePercentEffect(d.Percent),
        ["lifesteal"] = d => new LifestealEffect(d.Percent),
        ["spreadpoison"] = d => new SpreadPoisonEffect(d.Value),
        ["conditionalblock"] = d => new ConditionalBlockEffect(d.Condition ?? "", d.Value),
        ["damagefromhplost"] = d => new DamageFromHpLostEffect(d.Multiplier),
        ["hptomaxhp"] = d => new HpToMaxHpEffect(d.Percent),
        ["rowconditional"] = d => new RowConditionalFromDataEffect(d.FrontEffect, d.BackEffect),

        // === Additional effect types (from expanded card data) ===
        ["blockperovercharge"] = d => new BlockPerOverchargeEffect(d.Value > 0 ? d.Value : 1),
        ["blockperresonance"] = d => new BlockPerResonanceEffect(d.Value > 0 ? d.Value : 1),
        ["conditionaldraw"] = d => new ConditionalDrawEffect(d.Condition ?? "", d.Value > 0 ? d.Value : 1),
        ["conditionalenergy"] = d => new ConditionalEnergyEffect(d.Condition ?? "", d.Value > 0 ? d.Value : 1),
        ["conditionalstatus"] = d => new ConditionalStatusEffect(
            d.Condition ?? "",
            Enum.TryParse<StatusType>(d.StatusId ?? "", true, out var cs) ? cs : StatusType.Vulnerable,
            d.Value > 0 ? d.Value : 1),
        ["damagedperdebuff"] = d => new DamagePerDebuffEffect(d.Value > 0 ? d.Value : 2),
        ["damageperdebuff"] = d => new DamagePerDebuffEffect(d.Value > 0 ? d.Value : 2),
        ["damageperpoisonedenemy"] = d => new DamagePerPoisonedEnemyEffect(d.Value > 0 ? d.Value : 3),
        ["dominatemarked"] = d => new DominateMarkedEffect(d.Percent > 0 ? d.Percent : 50),
        ["hackonaction"] = d => new HackOnActionEffect(d.Value > 0 ? d.Value : 3),
        ["hackpercentofdamage"] = d => new HackPercentOfDamageEffect(d.Percent > 0 ? d.Percent : 50),
        ["healonkill"] = d => new HealOnKillEffect(d.Value > 0 ? d.Value : 3),
        ["healperpoisondamage"] = d => new HealPerPoisonDamageEffect(d.Value > 0 ? d.Value : 1),
        ["piercingdamage"] = d => new PiercingDamageEffect(d.Value),
        ["purgedebuffs"] = d => new PurgeDebuffsEffect(d.Value),
        ["randomdamage"] = d => new RandomDamageEffect(d.Value, d.SecondaryValue > 0 ? d.SecondaryValue : d.Value + 5),
        ["resonanceamplify"] = d => new ResonanceAmplifyEffect(d.Value > 0 ? d.Value : 2),
        ["retrievefromdiscard"] = d => new RetrieveFromDiscardEffect(d.Value > 0 ? d.Value : 1),
        ["searchdeck"] = d => new SearchDeckEffect(d.Value > 0 ? d.Value : 1),
        ["sethppercent"] = d => new SetHpPercentEffect(d.Percent > 0 ? d.Percent : 50),
        ["switchtofront"] = d => new SwitchToFrontEffect(),
    };

    public static ICardEffect Create(CardEffectData data)
    {
        ICardEffect inner;

        // Route conditional damage: if "damage" type has a condition, use ConditionalDamageEffect
        var normalizedType = data.Type.ToLowerInvariant();
        if (normalizedType == "damage" && !string.IsNullOrWhiteSpace(data.Condition))
        {
            int bonus = data.BonusDamage;
            // Support both Multiplier and DamageMultiplier fields from JSON
            double effectiveMultiplier = data.DamageMultiplier > 1 ? data.DamageMultiplier
                                       : data.Multiplier > 1 ? data.Multiplier : 1;
            if (bonus <= 0 && effectiveMultiplier > 1)
                bonus = (int)(data.Value * effectiveMultiplier);
            inner = new ConditionalDamageEffect(data.Value, bonus > 0 ? bonus : data.Value, data.Condition);
        }
        else if (_registry.TryGetValue(data.Type, out var factory))
            inner = factory(data);
        else
        {
            EffectLog.Warn($"CardEffectFactory: Unknown effect type \"{data.Type}\"");
            inner = new NoOpEffect(data.Type);
        }

        // Wrap with MultiHitEffect if Times > 1
        if (data.Times > 1)
            inner = new MultiHitEffect(inner, data.Times);

        // Wrap with selector if the individual effect specifies "target": "self"/"allEnemies"/...
        if (!string.IsNullOrWhiteSpace(data.Target)
            && !data.Target.Equals("default", StringComparison.OrdinalIgnoreCase))
        {
            var selector = TargetSelector.Parse(data.Target);
            if (selector != TargetSelector.Default)
                inner = new SelfTargetWrapper(inner, selector);
        }

        return inner;
    }

    public static ICardEffect CreateComposite(IEnumerable<CardEffectData> effects)
    {
        var composite = new CompositeEffect();
        foreach (var data in effects)
            composite.Add(Create(data));
        return composite;
    }

    /// <summary>
    /// Effect types that intrinsically require at least one enemy target.
    /// Used by <see cref="CardData.EffectiveTargetType"/> to auto-infer targeting
    /// when the JSON does not set <c>targetType</c> explicitly.
    /// </summary>
    private static readonly HashSet<string> _enemyTargetingEffectTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "damage",
            "multihit",
            "multihitdamage",
            "dashdamage",
            "piercingdamage",
            "randomdamage",
            "conditionaldamage",
            "damagefromhplost",
            "damagedperdebuff",
            "damageperdebuff",
            "damageperpoisonedenemy",
            "lifesteal",
            "hack",
            "instanthack",
            "hackonaction",
            "hackpercentofdamage",
            "consumeprotocol",
            "protocoldamage",
            "consumeresonance",
            "resonancedamage",
            "overchargeconsume",
            "consumeovercharge",
            "spreadpoison",
            "dominatemarked",
            "blockperresonance", // reads from source but effect's block target is self — keep non-enemy
            "blockperovercharge",
        };

    /// <summary>Effect types always self-only (self-buff, deck manipulation).</summary>
    private static readonly HashSet<string> _selfOnlyEffectTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "block", "armor", "draw", "energy", "gainenergy", "loseenergy",
            "heal", "protocolstack", "overchargegain", "resonance",
            "resonanceamplify", "resonanceblock", "scry", "addtodiscard",
            "retrievefromdiscard", "searchdeck", "purgedebuffs",
            "selfreposition", "switchtofront", "selfdamage", "selfdamagepercent",
            "doubleprotocol", "halveresonance", "clearresonance",
            "delayedresonance", "sethppercent", "hptomaxhp", "healonkill",
            "healperpoisondamage", "conditionaldraw", "conditionalenergy",
            "conditionalblock",
        };

    /// <summary>
    /// Returns true when the effect, given its JSON metadata, requires an enemy target.
    /// Respects <c>target</c> override ("self"/"allEnemies"/...) and the effect type's
    /// intrinsic class. Row-conditional effects recurse into their sub-effects.
    /// </summary>
    public static bool EffectRequiresEnemyTarget(CardEffectData data)
    {
        // Per-effect target override wins first.
        if (!string.IsNullOrWhiteSpace(data.Target))
        {
            var t = data.Target.Trim();
            if (t.Equals("self", StringComparison.OrdinalIgnoreCase)) return false;
            if (t.Equals("allallies", StringComparison.OrdinalIgnoreCase)
                || t.Equals("all_allies", StringComparison.OrdinalIgnoreCase)) return false;
            if (t.Equals("allenemies", StringComparison.OrdinalIgnoreCase)
                || t.Equals("all_enemies", StringComparison.OrdinalIgnoreCase)
                || t.Equals("randomenemy", StringComparison.OrdinalIgnoreCase)
                || t.Equals("random_enemy", StringComparison.OrdinalIgnoreCase))
                return false; // scope is AOE/random — no manual selection needed
        }

        var normalized = (data.Type ?? string.Empty).ToLowerInvariant();

        if (_selfOnlyEffectTypes.Contains(normalized))
            return false;

        if (_enemyTargetingEffectTypes.Contains(normalized))
            return true;

        // Row-conditional: defer to sub-effects.
        if (normalized == "rowconditional")
        {
            bool a = data.FrontEffect != null && EffectRequiresEnemyTarget(data.FrontEffect);
            bool b = data.BackEffect != null && EffectRequiresEnemyTarget(data.BackEffect);
            return a || b;
        }

        // applyStatus / status: debuff status types land on enemies; buffs on self.
        if (normalized is "status" or "applystatus")
        {
            if (Enum.TryParse<StatusType>(data.StatusId, true, out var st))
            {
                return st is StatusType.Vulnerable
                    or StatusType.Weak
                    or StatusType.Frail
                    or StatusType.Poison
                    or StatusType.Stunned
                    or StatusType.Hacked
                    or StatusType.Mark
                    or StatusType.ParasiticBond
                    or StatusType.Rootkit
                    or StatusType.Rooted;
            }
            // unknown status — conservative default: no target selection required.
            return false;
        }

        // applyPower: buff/debuff depends on PowerId; we can't always tell, so default to self-target.
        if (normalized == "applypower") return false;

        // Unknown/new effect type — default to non-targeting (safer: don't force target picking).
        return false;
    }
}
