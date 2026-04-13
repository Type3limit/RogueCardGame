using RogueCardGame.Core.Characters;
using RogueCardGame.Core.Combat.Actions;

namespace RogueCardGame.Core.Combat.Powers;

/// <summary>Central registry of power ID strings to avoid magic strings.</summary>
public static class CommonPowerIds
{
    // Debuffs
    public const string Vulnerable = "Vulnerable";
    public const string Weak = "Weak";
    public const string Frail = "Frail";
    public const string Poison = "Poison";
    public const string Rooted = "Rooted";

    // Buffs
    public const string Strength = "Strength";
    public const string Dexterity = "Dexterity";
    public const string Taunt = "Taunt";
    public const string Regeneration = "Regeneration";
    public const string Stealth = "Stealth";

    // Class-specific
    public const string Overcharge = "Overcharge";
    public const string Resonance = "Resonance";
    public const string Firewall = "Firewall";

    // Netrunner-specific
    public const string ProtocolStack = "ProtocolStack";
    public const string Hacked = "Hacked";
    public const string Stunned = "Stunned";

    // Symbiote-specific
    public const string Thorns = "Thorns";
    public const string PainThreshold = "PainThreshold";
    public const string ParasiticBond = "ParasiticBond";

    // Example extensibility: Doom (from the zhihu analysis)
    public const string Doom = "Doom";
}

// =====================================================================
//  DEBUFF POWERS
// =====================================================================

/// <summary>Take 50% more damage from attacks. Ticks down each turn.</summary>
public class VulnerablePower : AbstractPower
{
    public override string PowerId => CommonPowerIds.Vulnerable;
    public override string Name => "易伤";
    public override string GetDescription() => $"受到的攻击伤害增加50%。剩余 {Amount} 回合。";
    public override bool IsDebuff => true;
    public override bool TicksDown => true;

    public override float ModifyDamageTaken(float baseDamage) => baseDamage * 1.5f;
}

/// <summary>Deal 25% less attack damage. Ticks down each turn.</summary>
public class WeakPower : AbstractPower
{
    public override string PowerId => CommonPowerIds.Weak;
    public override string Name => "虚弱";
    public override string GetDescription() => $"造成的攻击伤害减少25%。剩余 {Amount} 回合。";
    public override bool IsDebuff => true;
    public override bool TicksDown => true;

    public override float ModifyAttackDamage(float baseDamage) => baseDamage * 0.75f;
}

/// <summary>Gain 25% less block. Ticks down each turn.</summary>
public class FrailPower : AbstractPower
{
    public override string PowerId => CommonPowerIds.Frail;
    public override string Name => "脆弱";
    public override string GetDescription() => $"获得的护甲减少25%。剩余 {Amount} 回合。";
    public override bool IsDebuff => true;
    public override bool TicksDown => true;

    public override float ModifyBlockGain(float baseBlock) => baseBlock * 0.75f;
}

/// <summary>Take X poison damage at end of turn. Reduce by 1 each turn.</summary>
public class PoisonPower : AbstractPower
{
    public override string PowerId => CommonPowerIds.Poison;
    public override string Name => "中毒";
    public override string GetDescription() => $"回合结束时受到 {Amount} 点伤害，然后层数减1。";
    public override bool IsDebuff => true;
    public override bool TicksDown => true;

    public override void AtTurnEnd()
    {
        if (Owner == null || Amount <= 0) return;
        // Poison damage bypasses block modification but still goes through the action queue
        // for proper cascading (e.g., on-damage triggers)
        ActionManager?.AddToTop(new PoisonDamageAction(Owner, Amount));
    }
}

/// <summary>Cannot switch formation rows. Ticks down.</summary>
public class RootedPower : AbstractPower
{
    public override string PowerId => CommonPowerIds.Rooted;
    public override string Name => "锁定";
    public override string GetDescription() => $"无法切换阵型。剩余 {Amount} 回合。";
    public override bool IsDebuff => true;
    public override bool TicksDown => true;
}

// =====================================================================
//  BUFF POWERS
// =====================================================================

/// <summary>Deal X more attack damage (additive).</summary>
public class StrengthPower : AbstractPower
{
    public override string PowerId => CommonPowerIds.Strength;
    public override string Name => "力量";
    public override string GetDescription() => $"攻击伤害 +{Amount}。";
    // Note: Strength is applied additively in DealDamageAction (+=stacks), not via modifier hook
}

/// <summary>Gain X more block (additive).</summary>
public class DexterityPower : AbstractPower
{
    public override string PowerId => CommonPowerIds.Dexterity;
    public override string Name => "敏捷";
    public override string GetDescription() => $"获得护甲 +{Amount}。";

    public override float ModifyBlockGain(float baseBlock) => baseBlock + Amount;
}

/// <summary>Force enemies to target this combatant. Ticks down.</summary>
public class TauntPower : AbstractPower
{
    public override string PowerId => CommonPowerIds.Taunt;
    public override string Name => "嘲讽";
    public override string GetDescription() => $"敌人被迫攻击此目标。剩余 {Amount} 回合。";
    public override bool TicksDown => true;
}

/// <summary>Heal X HP at end of turn. Reduce by 1 each turn.</summary>
public class RegenerationPower : AbstractPower
{
    public override string PowerId => CommonPowerIds.Regeneration;
    public override string Name => "再生";
    public override string GetDescription() => $"回合结束时恢复 {Amount} 点生命值，然后层数减1。";
    public override bool TicksDown => true;

    public override void AtTurnEnd()
    {
        if (Owner == null || Amount <= 0) return;
        ActionManager?.AddToTop(new HealAction(Owner, Amount));
    }
}

/// <summary>Cannot be targeted by single-target attacks. Ticks down.</summary>
public class StealthPower : AbstractPower
{
    public override string PowerId => CommonPowerIds.Stealth;
    public override string Name => "隐匿";
    public override string GetDescription() => $"无法被单体攻击选中。剩余 {Amount} 回合。";
    public override bool TicksDown => true;
}

// =====================================================================
//  CLASS-SPECIFIC POWERS
// =====================================================================

/// <summary>Vanguard: stored energy for burst attacks.</summary>
public class OverchargePower : AbstractPower
{
    public override string PowerId => CommonPowerIds.Overcharge;
    public override string Name => "超载";
    public override string GetDescription() => $"超载层数: {Amount}。可被特殊卡牌消耗转化为伤害。";
}

/// <summary>Psion: skill chain amplifier. Powered skills gain bonus.</summary>
public class ResonancePower : AbstractPower
{
    public override string PowerId => CommonPowerIds.Resonance;
    public override string Name => "共鸣";
    public override string GetDescription() => $"共鸣层数: {Amount}。连续使用技能时获得增益。";
}

/// <summary>Netrunner: damage reduction shield.</summary>
public class FirewallPower : AbstractPower
{
    public override string PowerId => CommonPowerIds.Firewall;
    public override string Name => "防火墙";
    public override string GetDescription() => $"减少 {Amount} 点受到的伤害。";

    public override float ModifyDamageTaken(float baseDamage) => Math.Max(0, baseDamage - Amount);
}

/// <summary>Netrunner: protocol stack counter. Cards consume for damage.</summary>
public class ProtocolStackPower : AbstractPower
{
    public override string PowerId => CommonPowerIds.ProtocolStack;
    public override string Name => "协议栈";
    public override string GetDescription() => $"协议栈层数: {Amount}。可被消耗卡牌转化为伤害。";
}

/// <summary>Netrunner: hack/intrusion progress on an enemy. At threshold, disables actions.</summary>
public class HackedPower : AbstractPower
{
    public override string PowerId => CommonPowerIds.Hacked;
    public override string Name => "入侵";
    public override string GetDescription() => $"入侵进度: {Amount}。达到阈值时敌人被控制。";
    public override bool IsDebuff => true;
}

/// <summary>Skip next action. Ticks down.</summary>
public class StunnedPower : AbstractPower
{
    public override string PowerId => CommonPowerIds.Stunned;
    public override string Name => "眩晕";
    public override string GetDescription() => $"跳过 {Amount} 次行动。";
    public override bool IsDebuff => true;
    public override bool TicksDown => true;
}

/// <summary>Symbiote: deal X damage back to attackers.</summary>
public class ThornsPower : AbstractPower
{
    public override string PowerId => CommonPowerIds.Thorns;
    public override string Name => "荆棘";
    public override string GetDescription() => $"受到攻击时，对攻击者造成 {Amount} 点伤害。";

    public override void OnTakeDamage(int amount)
    {
        // Thorns: retaliate against the attacker (simplified: damage is dealt as action)
        // Note: In STS thorns fires on attack hit, not on all damage. Simplified here.
    }
}

/// <summary>Symbiote: gain block equal to HP lost this turn. Duration-based.</summary>
public class PainThresholdPower : AbstractPower
{
    public override string PowerId => CommonPowerIds.PainThreshold;
    public override string Name => "痛觉阈值";
    public override string GetDescription() => $"本回合每失去 {Amount} HP，获得等量护甲。";
    public override bool TicksDown => true;

    public override void OnTakeDamage(int amount)
    {
        if (Owner == null) return;
        int blockGain = amount * Amount;
        ActionManager?.AddToTop(new GainBlockAction(Owner, blockGain));
    }
}

/// <summary>Symbiote: marked enemy - when it takes damage, heal the source.</summary>
public class ParasiticBondPower : AbstractPower
{
    public override string PowerId => CommonPowerIds.ParasiticBond;
    public override string Name => "寄生链接";
    public override string GetDescription() => $"受到伤害时，链接源回复伤害的 {Amount}% HP。";
    public override bool IsDebuff => true;
    public override bool TicksDown => true;
}

// =====================================================================
//  DOOM POWER — Proof of extensibility from the zhihu analysis
//  "灾厄": A custom debuff that executes enemies when stacks >= HP
// =====================================================================

/// <summary>
/// Doom: a custom debuff that can execute enemies.
/// - Stacks are applied by cards.
/// - At end of enemy turn, checks if stacks >= current HP → instant kill.
/// - Does NOT interact with Strength, Vulnerable, etc.
/// - This demonstrates that adding "new mechanism cards" is now natural.
/// </summary>
public class DoomPower : AbstractPower
{
    public override string PowerId => CommonPowerIds.Doom;
    public override string Name => "灾厄";
    public override string GetDescription() =>
        $"灾厄层数: {Amount}。当灾厄层数 ≥ 当前生命值时，立即处决。";
    public override bool IsDebuff => true;

    /// <summary>
    /// At end of turn, check doom execution.
    /// This hook fires naturally — no need to modify battle flow!
    /// </summary>
    public override void AtTurnEnd()
    {
        if (Owner == null || Amount <= 0) return;
        if (Amount >= Owner.CurrentHp)
        {
            // Execute! Enqueue as action so other powers can respond
            ActionManager?.AddToTop(new DoomExecuteAction([Owner]));
        }
    }
}

// =====================================================================
//  HELPER ACTIONS for powers
// =====================================================================

/// <summary>
/// Poison damage — bypasses normal damage modificiation but still triggers on-damage hooks.
/// </summary>
public class PoisonDamageAction : GameAction
{
    public Combatant Target { get; }
    public int Amount { get; }

    public PoisonDamageAction(Combatant target, int amount)
    {
        Target = target; Amount = amount;
        Duration = ActionType.Instant;
    }

    public override void Execute()
    {
        // Poison bypasses block
        int hpDamage = Math.Min(Amount, Target.CurrentHp);
        Target.CurrentHp = Math.Max(0, Target.CurrentHp - Amount);
        if (hpDamage > 0)
            Target.Powers.TriggerOnTakeDamage(hpDamage);
        if (!Target.IsAlive)
            Target.Powers.TriggerOnDeath();
        IsDone = true;
    }
}

/// <summary>
/// Factory to create Power instances from the old StatusType enum (backward compatibility).
/// </summary>
public static class PowerFactory
{
    /// <summary>
    /// Create an AbstractPower from the legacy StatusType enum.
    /// Returns a concrete Power with the given amount.
    /// </summary>
    public static AbstractPower CreateFromStatusType(StatusType type, int amount)
    {
        AbstractPower power = type switch
        {
            StatusType.Vulnerable => new VulnerablePower(),
            StatusType.Weak => new WeakPower(),
            StatusType.Frail => new FrailPower(),
            StatusType.Poison => new PoisonPower(),
            StatusType.Strength => new StrengthPower(),
            StatusType.Dexterity => new DexterityPower(),
            StatusType.Taunt => new TauntPower(),
            StatusType.Regeneration => new RegenerationPower(),
            StatusType.Overcharge => new OverchargePower(),
            StatusType.Resonance => new ResonancePower(),
            StatusType.Firewall => new FirewallPower(),
            StatusType.Rooted => new RootedPower(),
            StatusType.Stealth => new StealthPower(),
            StatusType.ProtocolStack => new ProtocolStackPower(),
            StatusType.Hacked => new HackedPower(),
            StatusType.Stunned => new StunnedPower(),
            StatusType.Thorns => new ThornsPower(),
            StatusType.PainThreshold => new PainThresholdPower(),
            StatusType.ParasiticBond => new ParasiticBondPower(),
            _ => throw new ArgumentException($"Unknown status type: {type}")
        };
        power.Amount = amount;
        return power;
    }

    /// <summary>Map a PowerId string to the corresponding StatusType for backward compat.</summary>
    public static StatusType? ToStatusType(string powerId) => powerId switch
    {
        CommonPowerIds.Vulnerable => StatusType.Vulnerable,
        CommonPowerIds.Weak => StatusType.Weak,
        CommonPowerIds.Frail => StatusType.Frail,
        CommonPowerIds.Poison => StatusType.Poison,
        CommonPowerIds.Strength => StatusType.Strength,
        CommonPowerIds.Dexterity => StatusType.Dexterity,
        CommonPowerIds.Taunt => StatusType.Taunt,
        CommonPowerIds.Regeneration => StatusType.Regeneration,
        CommonPowerIds.Overcharge => StatusType.Overcharge,
        CommonPowerIds.Resonance => StatusType.Resonance,
        CommonPowerIds.Firewall => StatusType.Firewall,
        CommonPowerIds.Rooted => StatusType.Rooted,
        CommonPowerIds.Stealth => StatusType.Stealth,
        CommonPowerIds.ProtocolStack => StatusType.ProtocolStack,
        CommonPowerIds.Hacked => StatusType.Hacked,
        CommonPowerIds.Stunned => StatusType.Stunned,
        CommonPowerIds.Thorns => StatusType.Thorns,
        CommonPowerIds.PainThreshold => StatusType.PainThreshold,
        CommonPowerIds.ParasiticBond => StatusType.ParasiticBond,
        _ => null
    };

    /// <summary>Map a StatusType to its PowerId string.</summary>
    public static string ToPowerId(StatusType type) => type switch
    {
        StatusType.Vulnerable => CommonPowerIds.Vulnerable,
        StatusType.Weak => CommonPowerIds.Weak,
        StatusType.Frail => CommonPowerIds.Frail,
        StatusType.Poison => CommonPowerIds.Poison,
        StatusType.Strength => CommonPowerIds.Strength,
        StatusType.Dexterity => CommonPowerIds.Dexterity,
        StatusType.Taunt => CommonPowerIds.Taunt,
        StatusType.Regeneration => CommonPowerIds.Regeneration,
        StatusType.Overcharge => CommonPowerIds.Overcharge,
        StatusType.Resonance => CommonPowerIds.Resonance,
        StatusType.Firewall => CommonPowerIds.Firewall,
        StatusType.Rooted => CommonPowerIds.Rooted,
        StatusType.Stealth => CommonPowerIds.Stealth,
        StatusType.ProtocolStack => CommonPowerIds.ProtocolStack,
        StatusType.Hacked => CommonPowerIds.Hacked,
        StatusType.Stunned => CommonPowerIds.Stunned,
        StatusType.Thorns => CommonPowerIds.Thorns,
        StatusType.PainThreshold => CommonPowerIds.PainThreshold,
        StatusType.ParasiticBond => CommonPowerIds.ParasiticBond,
        _ => type.ToString()
    };
}
