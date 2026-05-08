using RogueCardGame.Core.Characters;
using RogueCardGame.Core.Combat;
using RogueCardGame.Core.Combat.Powers;
using RogueCardGame.Core.Utils;

namespace RogueCardGame.Core.Cards;

/// <summary>
/// Unified condition evaluator for card effects.
/// Replaces scattered condition logic in each conditional effect class.
/// All condition strings used in JSON card data are resolved here.
/// </summary>
public static class ConditionEvaluator
{
    /// <summary>
    /// Evaluate a condition string against the current effect context.
    /// </summary>
    public static bool Evaluate(string condition, CardEffectContext context)
    {
        // Source-side power checks
        if (condition.Equals("hasOvercharge", StringComparison.OrdinalIgnoreCase))
            return context.Source.Powers.GetStacks(CommonPowerIds.Overcharge) > 0;

        if (condition.Equals("noOvercharge", StringComparison.OrdinalIgnoreCase))
            return context.Source.Powers.GetStacks(CommonPowerIds.Overcharge) == 0;

        if (condition.Equals("lostHpThisTurn", StringComparison.OrdinalIgnoreCase))
            return context.Source.CurrentHp < context.Source.MaxHp;

        if (condition.Equals("hpBelow50%", StringComparison.OrdinalIgnoreCase))
            return context.Source.CurrentHp <= context.Source.MaxHp / 2;

        if (condition.Equals("addLostHpThisTurn", StringComparison.OrdinalIgnoreCase))
            return context.TotalSelfHpLossThisCard > 0;

        // Target-side checks (require a valid target)
        if (context.Target != null)
        {
            if (condition.Equals("targetVulnerable", StringComparison.OrdinalIgnoreCase))
                return context.Target.Powers.HasPower(CommonPowerIds.Vulnerable);

            if (condition.Equals("targetMarked", StringComparison.OrdinalIgnoreCase))
                return context.Target.Powers.HasPower(CommonPowerIds.Mark);

            if (condition.Equals("targetPoisoned", StringComparison.OrdinalIgnoreCase))
                return context.Target.Powers.HasPower(CommonPowerIds.Poison);
        }

        // Generic power>=N pattern: matches any power ID like "resonance>=5", "protocolStack>=3"
        if (condition.Contains(">="))
        {
            var parts = condition.Split(">=", 2);
            if (parts.Length == 2 && int.TryParse(parts[1], out int threshold))
            {
                string powerId = ResolvePowerId(parts[0].Trim());
                return context.Source.Powers.GetStacks(powerId) >= threshold;
            }
        }

        // consumedOverchargeThisTurn — requires runtime flag
        if (condition.Equals("consumedOverchargeThisTurn", StringComparison.OrdinalIgnoreCase))
            return context.ConsumedOverchargeThisTurn;

        EffectLog.Warn($"[ConditionEvaluator] Unknown condition: \"{condition}\"");
        return false;
    }

    /// <summary>
    /// Map friendly condition names to actual PowerId strings.
    /// Supports both exact PowerId and common aliases.
    /// </summary>
    private static string ResolvePowerId(string name) => name.ToLowerInvariant() switch
    {
        "resonance" => CommonPowerIds.Resonance,
        "protocolstack" or "protocolstack" => CommonPowerIds.ProtocolStack,
        "overcharge" => CommonPowerIds.Overcharge,
        "hacked" => CommonPowerIds.Hacked,
        "poison" => CommonPowerIds.Poison,
        _ => name // pass through as-is for direct PowerId strings
    };
}
