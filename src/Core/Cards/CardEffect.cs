using RogueCardGame.Core.Cards;
using RogueCardGame.Core.Characters;

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
/// Deal damage to one or more targets.
/// </summary>
public class DealDamageEffect : ICardEffect
{
    public int BaseDamage { get; }

    public DealDamageEffect(int baseDamage) => BaseDamage = baseDamage;

    public void Execute(CardEffectContext context)
    {
        int damage = context.Source.CalculateAttackDamage(BaseDamage);
        float multiplier = context.Formation.GetDamageMultiplier(context.Source.Id, context.Range);
        damage = (int)(damage * multiplier);

        foreach (var target in context.AllTargets)
        {
            int dealt = target.TakeDamage(damage);
            context.Aggro.AddAggro(context.Source.Id, dealt * AggroSystem.AggroPerDamage);
        }
    }

    public string GetDescription(CardEffectContext? context = null) =>
        $"造成 {BaseDamage} 点伤害";
}

/// <summary>
/// Gain block (armor).
/// </summary>
public class GainBlockEffect : ICardEffect
{
    public int BaseBlock { get; }

    public GainBlockEffect(int baseBlock) => BaseBlock = baseBlock;

    public void Execute(CardEffectContext context)
    {
        int bonus = context.Formation.GetBlockBonus(context.Source.Id);
        context.Source.GainBlock(BaseBlock + bonus);
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
        // Drawing is handled externally by DeckManager; this signals intent
        // The CombatManager listens for draw requests
    }

    public string GetDescription(CardEffectContext? context = null) =>
        $"抽 {Count} 张牌";
}

/// <summary>
/// Apply a status effect to target(s).
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
            target.StatusEffects.Apply(StatusType, Stacks, Duration);
        }
    }

    public string GetDescription(CardEffectContext? context = null) =>
        $"施加 {Stacks} 层 {StatusType}";
}

/// <summary>
/// Gain energy this turn.
/// </summary>
public class GainEnergyEffect : ICardEffect
{
    public int Amount { get; }

    public GainEnergyEffect(int amount) => Amount = amount;

    public void Execute(CardEffectContext context)
    {
        if (context.Source is PlayerCharacter player)
            player.CurrentEnergy += Amount;
    }

    public string GetDescription(CardEffectContext? context = null) =>
        $"获得 {Amount} 点能量";
}

/// <summary>
/// Heal target(s).
/// </summary>
public class HealEffect : ICardEffect
{
    public int Amount { get; }

    public HealEffect(int amount) => Amount = amount;

    public void Execute(CardEffectContext context)
    {
        foreach (var target in context.AllTargets)
        {
            int healed = target.Heal(Amount);
            context.Aggro.AddAggro(context.Source.Id, healed * AggroSystem.AggroPerHealing);
        }
    }

    public string GetDescription(CardEffectContext? context = null) =>
        $"回复 {Amount} 点生命值";
}

/// <summary>
/// Force target to switch formation row.
/// </summary>
public class ForceRepositionEffect : ICardEffect
{
    public FormationRow TargetRow { get; }

    public ForceRepositionEffect(FormationRow targetRow) => TargetRow = targetRow;

    public void Execute(CardEffectContext context)
    {
        foreach (var target in context.AllTargets)
            context.Formation.ForcePosition(target.Id, TargetRow);
    }

    public string GetDescription(CardEffectContext? context = null) =>
        $"将目标移至{(TargetRow == FormationRow.Front ? "前排" : "后排")}";
}

/// <summary>
/// Add hack progress to enemy targets (Netrunner mechanic).
/// </summary>
public class HackProgressEffect : ICardEffect
{
    public int Progress { get; }

    public HackProgressEffect(int progress) => Progress = progress;

    public void Execute(CardEffectContext context)
    {
        foreach (var target in context.AllTargets)
        {
            if (target is Enemy enemy && enemy.CanBeHacked())
            {
                enemy.StatusEffects.Apply(StatusType.HackProgress, Progress);
                enemy.TryHack();
            }
        }
    }

    public string GetDescription(CardEffectContext? context = null) =>
        $"施加 {Progress} 层入侵进度";
}

/// <summary>
/// Factory that creates ICardEffect instances from CardEffectData.
/// </summary>
public static class CardEffectFactory
{
    public static ICardEffect Create(CardEffectData data)
    {
        return data.Type.ToLowerInvariant() switch
        {
            "damage" => new DealDamageEffect(data.Value),
            "block" or "armor" => new GainBlockEffect(data.Value),
            "draw" => new DrawCardsEffect(data.Value),
            "status" => new ApplyStatusEffect(
                Enum.Parse<StatusType>(data.StatusId!, true),
                data.Value,
                data.SecondaryValue > 0 ? data.SecondaryValue : -1),
            "energy" => new GainEnergyEffect(data.Value),
            "heal" => new HealEffect(data.Value),
            "reposition" => new ForceRepositionEffect(
                data.Value == 0 ? FormationRow.Front : FormationRow.Back),
            "hack" => new HackProgressEffect(data.Value),
            _ => throw new ArgumentException($"Unknown effect type: {data.Type}")
        };
    }

    public static ICardEffect CreateComposite(IEnumerable<CardEffectData> effects)
    {
        var composite = new CompositeEffect();
        foreach (var data in effects)
            composite.Add(Create(data));
        return composite;
    }
}
