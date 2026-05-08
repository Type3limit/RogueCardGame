using RogueCardGame.Core.Cards;
using RogueCardGame.Core.Characters;
using RogueCardGame.Core.Combat.Powers;

namespace RogueCardGame.Core.Combat;

/// <summary>
/// Boolean expression tree used by the unified Behavior primitive.
/// Parsed once from JSON on card load and evaluated repeatedly at runtime.
/// Use <see cref="Expr.Parse"/> to build from the legacy string-condition form
/// for backwards compatibility with existing card JSON.
/// </summary>
public abstract record Expr
{
    public abstract bool Evaluate(CardEffectContext ctx);

    // --- Leaf nodes ---

    public sealed record Const(bool Value) : Expr
    {
        public override bool Evaluate(CardEffectContext ctx) => Value;
    }

    public sealed record HasPower(string PowerId, bool OnSource = true) : Expr
    {
        public override bool Evaluate(CardEffectContext ctx)
        {
            var c = OnSource ? ctx.Source : ctx.Target;
            return c != null && c.Powers.HasPower(PowerId);
        }
    }

    public sealed record PowerGte(string PowerId, int Threshold, bool OnSource = true) : Expr
    {
        public override bool Evaluate(CardEffectContext ctx)
        {
            var c = OnSource ? ctx.Source : ctx.Target;
            return c != null && c.Powers.GetStacks(PowerId) >= Threshold;
        }
    }

    public sealed record HpPercentLt(int PercentTimes100, bool OnSource = true) : Expr
    {
        public override bool Evaluate(CardEffectContext ctx)
        {
            var c = OnSource ? ctx.Source : ctx.Target;
            if (c == null || c.MaxHp <= 0) return false;
            return c.CurrentHp * 10_000 < PercentTimes100 * c.MaxHp;
        }
    }

    public sealed record LostHpThisCard() : Expr
    {
        public override bool Evaluate(CardEffectContext ctx) => ctx.TotalSelfHpLossThisCard > 0;
    }

    public sealed record ConsumedOverchargeThisTurn() : Expr
    {
        public override bool Evaluate(CardEffectContext ctx) => ctx.ConsumedOverchargeThisTurn;
    }

    // --- Combinators ---

    public sealed record And(Expr L, Expr R) : Expr
    {
        public override bool Evaluate(CardEffectContext ctx) => L.Evaluate(ctx) && R.Evaluate(ctx);
    }

    public sealed record Or(Expr L, Expr R) : Expr
    {
        public override bool Evaluate(CardEffectContext ctx) => L.Evaluate(ctx) || R.Evaluate(ctx);
    }

    public sealed record Not(Expr Inner) : Expr
    {
        public override bool Evaluate(CardEffectContext ctx) => !Inner.Evaluate(ctx);
    }

    /// <summary>
    /// Parse a legacy string-condition (as used in current JSON) into an Expr tree.
    /// Returns Const(true) when the input is null/empty (unconditional effect).
    /// Unknown tokens fall back to a <see cref="StringCondition"/> which defers to ConditionEvaluator
    /// so no existing behaviour is broken while card JSON is migrated piecemeal.
    /// </summary>
    public static Expr Parse(string? condition)
    {
        if (string.IsNullOrWhiteSpace(condition)) return new Const(true);
        var c = condition.Trim();

        if (c.Equals("hasOvercharge", StringComparison.OrdinalIgnoreCase))
            return new PowerGte(CommonPowerIds.Overcharge, 1);
        if (c.Equals("noOvercharge", StringComparison.OrdinalIgnoreCase))
            return new Not(new PowerGte(CommonPowerIds.Overcharge, 1));
        if (c.Equals("hpBelow50%", StringComparison.OrdinalIgnoreCase))
            return new HpPercentLt(50_00);
        if (c.Equals("lostHpThisTurn", StringComparison.OrdinalIgnoreCase))
            return new LostHpThisCard();
        if (c.Equals("addLostHpThisTurn", StringComparison.OrdinalIgnoreCase))
            return new LostHpThisCard();
        if (c.Equals("consumedOverchargeThisTurn", StringComparison.OrdinalIgnoreCase))
            return new ConsumedOverchargeThisTurn();
        if (c.Equals("targetVulnerable", StringComparison.OrdinalIgnoreCase))
            return new HasPower(CommonPowerIds.Vulnerable, OnSource: false);
        if (c.Equals("targetMarked", StringComparison.OrdinalIgnoreCase))
            return new HasPower(CommonPowerIds.Mark, OnSource: false);
        if (c.Equals("targetPoisoned", StringComparison.OrdinalIgnoreCase))
            return new HasPower(CommonPowerIds.Poison, OnSource: false);

        // power>=N
        if (c.Contains(">="))
        {
            var parts = c.Split(">=", 2);
            if (parts.Length == 2 && int.TryParse(parts[1].Trim(), out int n))
            {
                var id = parts[0].Trim();
                string powerId = id.ToLowerInvariant() switch
                {
                    "resonance" => CommonPowerIds.Resonance,
                    "protocolstack" => CommonPowerIds.ProtocolStack,
                    "overcharge" => CommonPowerIds.Overcharge,
                    "hacked" => CommonPowerIds.Hacked,
                    "poison" => CommonPowerIds.Poison,
                    _ => id,
                };
                return new PowerGte(powerId, n);
            }
        }

        return new StringCondition(c);
    }

    /// <summary>Adapter to the string-based ConditionEvaluator for conditions not yet ported.</summary>
    public sealed record StringCondition(string Raw) : Expr
    {
        public override bool Evaluate(CardEffectContext ctx) => ConditionEvaluator.Evaluate(Raw, ctx);
    }
}
