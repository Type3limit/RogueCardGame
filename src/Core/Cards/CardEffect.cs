using RogueCardGame.Core.Cards;
using RogueCardGame.Core.Characters;
using RogueCardGame.Core.Combat.Actions;
using RogueCardGame.Core.Combat.Powers;
using RogueCardGame.Core.Deck;

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
    /// <summary>Callback invoked when an effect wants to draw cards (resolved by CombatManager).</summary>
    public Action<int>? DrawCardCallback { get; init; }
    /// <summary>Callback to add a card by ID to the discard pile.</summary>
    public Action<string>? AddToDiscardCallback { get; init; }
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
/// Wrapper that overrides the context to target self (Source) regardless of the card's target type.
/// Used for per-effect targeting when an individual effect has "target": "self" in JSON.
/// </summary>
public class SelfTargetWrapper : ICardEffect
{
    private readonly ICardEffect _inner;

    public SelfTargetWrapper(ICardEffect inner) => _inner = inner;

    public void Execute(CardEffectContext context)
    {
        var selfContext = new CardEffectContext
        {
            Source = context.Source,
            Target = context.Source,
            AllTargets = [context.Source],
            Formation = context.Formation,
            Aggro = context.Aggro,
            Card = context.Card,
            Range = context.Range,
            Actions = context.Actions,
            Deck = context.Deck,
            CardDb = context.CardDb,
            DrawCardCallback = context.DrawCardCallback,
            AddToDiscardCallback = context.AddToDiscardCallback
        };
        _inner.Execute(selfContext);
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
/// Draw cards from deck.
/// </summary>
public class DrawCardsEffect : ICardEffect
{
    public int Count { get; }

    public DrawCardsEffect(int count) => Count = count;

    public void Execute(CardEffectContext context)
    {
        context.DrawCardCallback?.Invoke(Count);
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
                context.Actions.AddToBottom(new ApplyPowerAction(target, power));
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
        context.AddToDiscardCallback?.Invoke(CardId);
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
    public int Multiplier { get; }

    public ResonanceDamageEffect(int baseDamage, int multiplier = 1)
    {
        BaseDamage = baseDamage;
        Multiplier = multiplier;
    }

    public void Execute(CardEffectContext context)
    {
        int resonance = context.Source.Powers.GetStacks(CommonPowerIds.Resonance);
        int totalDamage = BaseDamage + resonance * Multiplier;
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
    public int Multiplier { get; }
    public ResonanceBlockEffect(int multiplier) => Multiplier = multiplier;

    public void Execute(CardEffectContext context)
    {
        int resonance = context.Source.Powers.GetStacks(CommonPowerIds.Resonance);
        int block = resonance * Multiplier;
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

/// <summary>Delayed resonance: gain stacks at next turn start. Simplified: just apply now.</summary>
public class DelayedResonanceEffect : ICardEffect
{
    public int Amount { get; }
    public DelayedResonanceEffect(int amount) => Amount = amount;

    public void Execute(CardEffectContext context)
    {
        // Simplified: apply resonance immediately (a full implementation would use a trigger power)
        var power = new ResonancePower { Amount = Amount };
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

    private bool EvaluateCondition(CardEffectContext context)
    {
        if (Condition.StartsWith("resonance>="))
        {
            int threshold = int.Parse(Condition["resonance>=".Length..]);
            return context.Source.Powers.GetStacks(CommonPowerIds.Resonance) >= threshold;
        }
        return false;
    }

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
        context.DrawCardCallback?.Invoke(Count > 2 ? 1 : Count);
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
            _ => null
        };
        if (power != null)
            context.Actions?.AddToBottom(new ApplyPowerAction(context.Source, power));
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
        foreach (var target in context.AllTargets)
        {
            var power = new HackedPower { Amount = Amount };
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
        if (power != null)
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

/// <summary>Lose HP (flat amount).</summary>
public class SelfDamageEffect : ICardEffect
{
    public int Amount { get; }
    public SelfDamageEffect(int amount) => Amount = amount;

    public void Execute(CardEffectContext context)
    {
        context.Source.CurrentHp = Math.Max(1, context.Source.CurrentHp - Amount);
    }
    public string GetDescription(CardEffectContext? context = null) =>
        $"失去 {Amount} HP";
}

/// <summary>Lose HP equal to a percentage of current HP.</summary>
public class SelfDamagePercentEffect : ICardEffect
{
    public int Percent { get; }
    public SelfDamagePercentEffect(int percent) => Percent = percent;

    public void Execute(CardEffectContext context)
    {
        int loss = context.Source.CurrentHp * Percent / 100;
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
        // Track damage from previous effects in this composite
        // Simplified: estimate heal from target HP loss
        int totalDamageEstimate = 0;
        foreach (var target in context.AllTargets)
            totalDamageEstimate += Math.Max(0, target.MaxHp - target.CurrentHp);
        int heal = Math.Max(1, totalDamageEstimate * Percent / 100);
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
        bool condMet = Condition switch
        {
            "lostHpThisTurn" => context.Source.CurrentHp < context.Source.MaxHp,
            _ => false
        };
        if (condMet)
            context.Actions?.AddToBottom(new GainBlockAction(context.Source, Amount, context.Formation));
    }
    public string GetDescription(CardEffectContext? context = null) =>
        $"条件满足时额外获得 {Amount} 护甲";
}

/// <summary>Deal damage based on HP lost this card play (for Pandemonium).</summary>
public class DamageFromHpLostEffect : ICardEffect
{
    public int Multiplier { get; }
    public DamageFromHpLostEffect(int multiplier) => Multiplier = multiplier;

    public void Execute(CardEffectContext context)
    {
        // Estimate: use the selfDamage that was already applied in this composite
        int hpLost = context.Source.MaxHp - context.Source.CurrentHp;
        int damage = Math.Max(1, hpLost * Multiplier / 3); // Approximate since we can't track exact loss
        context.Actions?.AddToBottom(new DealDamageAction(
            context.Source, context.AllTargets, damage,
            context.Range, context.Formation, context.Aggro));
    }
    public string GetDescription(CardEffectContext? context = null) =>
        $"造成失去HP×{Multiplier}的伤害";
}

/// <summary>Convert current HP percentage to max HP.</summary>
public class HpToMaxHpEffect : ICardEffect
{
    public int Percent { get; }
    public HpToMaxHpEffect(int percent) => Percent = percent;

    public void Execute(CardEffectContext context)
    {
        int gain = context.Source.CurrentHp * Percent / 100;
        context.Source.CurrentHp -= gain;
        context.Source.MaxHp += gain;
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

/// <summary>No-op effect for unknown types (logs warning, doesn't crash).</summary>
public class NoOpEffect : ICardEffect
{
    private readonly string _type;
    public NoOpEffect(string type) => _type = type;
    public void Execute(CardEffectContext context) =>
        Godot.GD.PrintErr($"[CardEffect] Unimplemented effect type: {_type}");
    public string GetDescription(CardEffectContext? context = null) => $"[{_type}]";
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
        if (Owner != null)
            ActionManager?.AddToBottom(new ApplyPowerAction(Owner, new ProtocolStackPower { Amount = Amount }));
    }
}

/// <summary>Symbiote: gain strength per HP lost, heal on kill.</summary>
public class ApexPredatorPower : AbstractPower
{
    public override string PowerId => "ApexPredator";
    public override string Name => "顶级掠食者";
    public override string GetDescription() => $"每失去5HP获得1力量。击杀回复{Amount}HP。";
    private int _hpLostAccumulator;

    public override void OnTakeDamage(int amount)
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
    public static ICardEffect Create(CardEffectData data)
    {
        ICardEffect inner = data.Type.ToLowerInvariant() switch
        {
            "damage" => new DealDamageEffect(data.Value),
            "block" or "armor" => new GainBlockEffect(data.Value),
            "draw" => new DrawCardsEffect(data.Value),
            "status" or "applystatus" => new ApplyStatusEffect(
                Enum.Parse<StatusType>(data.StatusId!, true),
                data.Value,
                data.SecondaryValue > 0 ? data.SecondaryValue : data.Duration > 0 ? data.Duration : -1),
            "energy" or "gainenergy" => new GainEnergyEffect(data.Value),
            "loseenergy" => new GainEnergyEffect(-data.Value),
            "heal" => new HealEffect(data.Value),
            "reposition" => new ForceRepositionEffect(
                data.Value == 0 ? FormationRow.Front : FormationRow.Back),
            "selfreposition" => data.Value switch {
                0 => new SelfRepositionEffect(FormationRow.Front),
                1 => new SelfRepositionEffect(FormationRow.Back),
                _ => new SelfRepositionEffect()
            },
            "dashdamage" => new DashDamageEffect(data.Value, data.SecondaryValue != 0),
            "overchargeconsume" or "consumeovercharge"
                => new OverchargeConsumeEffect(data.Value, data.SecondaryValue > 0 ? data.SecondaryValue : 3),
            "addtodiscard" => new AddToDiscardEffect(data.CardId ?? "glitch"),

            // === Psion: Resonance mechanics ===
            "resonancedamage" => new ResonanceDamageEffect(data.BaseDamage, data.Multiplier),
            "resonanceblock" => new ResonanceBlockEffect(data.Multiplier),
            "consumeresonance" => new ConsumeResonanceEffect(data.DamagePerStack),
            "halveresonance" => new HalveResonanceEffect(),
            "clearresonance" => new ClearResonanceEffect(),
            "delayedresonance" => new DelayedResonanceEffect(data.Value),
            "conditionaldamage" => new ConditionalDamageEffect(data.BaseDamage, data.BonusDamage, data.Condition ?? ""),
            "scry" => new ScryEffect(data.Value),
            "applypower" => new ApplyPowerByIdEffect(data.PowerId ?? "", data.Value),

            // === Netrunner: Protocol/Hack mechanics ===
            "protocolstack" => new ProtocolStackEffect(data.Value),
            "hack" => new HackEffect(data.Value),
            "consumeprotocol" => new ConsumeProtocolEffect(data.DamagePerStack),
            "protocoldamage" => new ProtocolDamageEffect(data.DamagePerStack, data.Consume),
            "doubleprotocol" => new DoubleProtocolEffect(data.Cap > 0 ? data.Cap : 10),
            "instanthack" => new InstantHackEffect(data.Duration > 0 ? data.Duration : 1),

            // === Symbiote: Self-damage/lifesteal mechanics ===
            "selfdamage" => new SelfDamageEffect(data.Value),
            "selfdamagepercent" => new SelfDamagePercentEffect(data.Percent),
            "lifesteal" => new LifestealEffect(data.Percent),
            "spreadpoison" => new SpreadPoisonEffect(data.Value),
            "conditionalblock" => new ConditionalBlockEffect(data.Condition ?? "", data.Value),
            "damagefromhplost" => new DamageFromHpLostEffect(data.Multiplier),
            "hptomaxhp" => new HpToMaxHpEffect(data.Percent),
            "rowconditional" => new RowConditionalFromDataEffect(data.FrontEffect, data.BackEffect),

            _ => new NoOpEffect(data.Type)
        };

        // Wrap with SelfTargetWrapper if the individual effect specifies "target": "self"
        if (data.Target?.Equals("self", StringComparison.OrdinalIgnoreCase) == true)
            inner = new SelfTargetWrapper(inner);

        return inner;
    }

    public static ICardEffect CreateComposite(IEnumerable<CardEffectData> effects)
    {
        var composite = new CompositeEffect();
        foreach (var data in effects)
            composite.Add(Create(data));
        return composite;
    }
}
