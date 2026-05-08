using RogueCardGame.Core;
using RogueCardGame.Core.Cards;
using RogueCardGame.Core.Characters;
using RogueCardGame.Core.Combat;
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

    // Additional status types
    public const string Mark = "Mark";
    public const string Rootkit = "Rootkit";
    public const string CompileAccel = "CompileAccel";
    public const string MeleeStrength = "MeleeStrength";
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
    private bool _skipNextDecay;

    public override string PowerId => CommonPowerIds.Resonance;
    public override string Name => "共鸣";
    public override string GetDescription() => $"共鸣层数: {Amount}。连续使用技能时获得增益。";

    public override void AtTurnEnd()
    {
        if (Owner == null || Amount <= 0) return;

        bool halfDecay = GetImplantBonus("resonanceDecayHalf") > 0;
        if (!halfDecay)
        {
            Amount = Math.Max(0, Amount - 1);
            if (Amount == 0)
                Owner.Powers.RemovePower(PowerId);
            return;
        }

        if (_skipNextDecay)
        {
            _skipNextDecay = false;
            return;
        }

        Amount = Math.Max(0, Amount - 1);
        _skipNextDecay = true;
        if (Amount == 0)
            Owner.Powers.RemovePower(PowerId);
    }
}

/// <summary>
/// Psion: delayed resonance trigger — converts to Resonance stacks at the start of next turn.
/// Applied by DelayedResonanceEffect. Consumes itself immediately after triggering.
/// </summary>
public class GainResonanceNextTurnPower : AbstractPower
{
    public override string PowerId => "GainResonanceNextTurn";
    public override string Name => "蓄积共鸣";
    public override string GetDescription() => $"下回合开始时获得 {Amount} 层共鸣。";

    public override void AtTurnStart()
    {
        if (Owner == null) return;
        // Convert to Resonance at turn start, then remove self
        ActionManager?.AddToBottom(new ApplyPowerAction(Owner, new ResonancePower { Amount = Amount }));
        Owner.Powers.RemovePower(PowerId);
    }
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
    public int MaxStacks => 10 + GetImplantBonus("protocolStackSize");

    public override string PowerId => CommonPowerIds.ProtocolStack;
    public override string Name => "协议栈";
    public override string GetDescription() => $"协议栈层数: {Amount}/{MaxStacks}。可被消耗卡牌转化为伤害。";

    public override void OnApply()
    {
        Amount = Math.Min(Amount, MaxStacks);
    }

    public override void OnStacksAdded(int addedAmount)
    {
        Amount = Math.Min(Amount, MaxStacks);
    }
}

/// <summary>Netrunner: hack/intrusion progress on an enemy. At threshold, stuns the enemy for 1 turn and resets.</summary>
public class HackedPower : AbstractPower
{
    public override string PowerId => CommonPowerIds.Hacked;
    public override string Name => "入侵";
    public override string GetDescription() =>
        $"入侵进度: {Amount}/{BalanceConfig.Current.GlobalBalance.HackThreshold}。达到阈值时敌人被眩晕并重置。";
    public override bool IsDebuff => true;

    public override void AtTurnStart()
    {
        if (Owner == null) return;
        int threshold = BalanceConfig.Current.GlobalBalance.HackThreshold;
        if (Amount >= threshold)
        {
            // Control achieved: stun for 1 turn and reset hack progress
            ActionManager?.AddToTop(new ApplyPowerAction(Owner, new StunnedPower { Amount = 1 }));
            Owner.Powers.RemovePower(PowerId);
        }
    }
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

/// <summary>Symbiote: deal X damage back to attackers. Damage bypasses block (STS behavior).</summary>
public class ThornsPower : AbstractPower
{
    public override string PowerId => CommonPowerIds.Thorns;
    public override string Name => "荆棘";
    public override string GetDescription() => $"受到攻击时，对攻击者造成 {Amount} 点直接伤害。";

    public override void OnTakeDamage(int amount, Combatant? attacker = null)
    {
        if (attacker == null || Owner == null || Amount <= 0) return;
        // Thorns retaliatory damage bypasses block (like STS). Reuse PoisonDamageAction.
        ActionManager?.AddToTop(new PoisonDamageAction(attacker, Amount));
    }
}

/// <summary>Symbiote: gain block equal to HP lost this turn. Duration-based.</summary>
public class PainThresholdPower : AbstractPower
{
    public override string PowerId => CommonPowerIds.PainThreshold;
    public override string Name => "痛觉阈值";
    public override string GetDescription() => $"本回合每失去 {Amount} HP，获得等量护甲。";
    public override bool TicksDown => true;

    public override void OnTakeDamage(int amount, Combatant? attacker = null)
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

    public override void OnTakeDamage(int amount, Combatant? attacker = null)
    {
        if (Source == null || Source == Owner || !Source.IsAlive || Amount <= 0 || amount <= 0)
            return;
        int heal = Math.Max(1, amount * Amount / 100);
        ActionManager?.AddToBottom(new HealAction(Source, heal));
    }
}

/// <summary>Psion Mark: marks a target. Unlike Vulnerable it does not modify damage; used by cost/kill conditions.</summary>
public class MarkPower : AbstractPower
{
    public override string PowerId => CommonPowerIds.Mark;
    public override string Name => "标记";
    public override string GetDescription() => $"被标记。剩余 {Amount} 回合。";
    public override bool IsDebuff => true;
    public override bool TicksDown => true;
}

/// <summary>Symbiote-internal: rootkit/compile helpers as ticking debuff/buff (placeholder stacks).</summary>
public class RootkitPower : AbstractPower
{
    public override string PowerId => CommonPowerIds.Rootkit;
    public override string Name => "Rootkit";
    public override string GetDescription() => $"入侵保护 {Amount} 层。";
    public override bool IsDebuff => false;
}

public class CompileAccelPower : AbstractPower
{
    public override string PowerId => CommonPowerIds.CompileAccel;
    public override string Name => "编译加速";
    public override string GetDescription() => $"本回合下 {Amount} 张「程序」卡费用 -1。";
    public override bool IsDebuff => false;

    public override int ModifyCardCost(Card card, int baseCost, Combatant? target)
    {
        if (Amount > 0 && card.Data.Type == CardType.Skill)
            return Math.Max(0, baseCost - 1);
        return baseCost;
    }

    public override void OnCardPlayed(Card card)
    {
        if (Amount > 0 && card.Data.Type == CardType.Skill)
        {
            Amount--;
            if (Amount <= 0 && Owner != null)
                ActionManager?.AddToBottom(new RemovePowerDelayedAction(Owner, PowerId));
        }
    }
}

public class MeleeStrengthPower : AbstractPower
{
    public override string PowerId => CommonPowerIds.MeleeStrength;
    public override string Name => "近战强化";
    public override string GetDescription() => $"近战攻击伤害 +{Amount}（回合结束失效）。";
    public override bool IsDebuff => false;

    public override void AtTurnEnd()
    {
        if (Owner != null)
            ActionManager?.AddToBottom(new RemovePowerDelayedAction(Owner, PowerId));
    }
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
        {
            Manager?.Combat?.Events.Publish(new CombatEvent
            {
                Kind = TriggerKind.OnTakeDamage, Target = Target, Amount = hpDamage
            });
        }
        if (!Target.IsAlive)
        {
            Manager?.Combat?.Events.Publish(new CombatEvent { Kind = TriggerKind.OnDeath, Actor = Target });
        }
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
            StatusType.Mark => new MarkPower(),
            StatusType.Rootkit => new RootkitPower(),
            StatusType.CompileAccel => new CompileAccelPower(),
            StatusType.MeleeStrength => new MeleeStrengthPower(),
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
        CommonPowerIds.Mark => StatusType.Mark,
        CommonPowerIds.Rootkit => StatusType.Rootkit,
        CommonPowerIds.CompileAccel => StatusType.CompileAccel,
        CommonPowerIds.MeleeStrength => StatusType.MeleeStrength,
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
        StatusType.Mark => CommonPowerIds.Mark,
        StatusType.Rootkit => CommonPowerIds.Rootkit,
        StatusType.CompileAccel => CommonPowerIds.CompileAccel,
        StatusType.MeleeStrength => CommonPowerIds.MeleeStrength,
        _ => type.ToString()
    };
}

/// <summary>Buff power: heals the owner when they kill an enemy.</summary>
public class HealOnKillPower : AbstractPower
{
    public override string PowerId => "HealOnKill";
    public override string Name => "击杀回复";
    public override bool IsDebuff => false;
    public override string GetDescription() => $"击杀时回复 {Amount} 点生命";

    public override void OnKill(Combatant victim)
    {
        if (Owner != null)
            ActionManager?.AddToBottom(new HealAction(Owner, Amount));
    }
}

/// <summary>Buff power: amplifies the next Resonance consume by +Amount damage per stack.</summary>
public class ResonanceAmplifyPower : AbstractPower
{
    public override string PowerId => "ResonanceAmplify";
    public override string Name => "共鸣增幅";
    public override bool IsDebuff => false;
    public override string GetDescription() => $"下次消耗共鸣时每层额外 +{Amount} 伤害";

    public override float ModifyConsumeResonanceDamage(float damage, int stacks)
    {
        float bonus = stacks * Amount;
        ActionManager?.Combat?.Log?.Invoke($"[共鸣增幅] 消耗 {stacks} 层共鸣, 每层+{Amount} 额外伤害 = +{bonus}");
        // Remove self after amplifying (it's a one-time buff)
        if (Owner != null)
            ActionManager?.AddToBottom(new RemovePowerDelayedAction(Owner, PowerId));
        return damage + bonus;
    }
}

// =====================================================================
//  CLASS-SPECIFIC PERMANENT POWERS (from card applyPower effects)
// =====================================================================

/// <summary>Vanguard: front row gain Overcharge each turn; consuming Overcharge deals extra damage.</summary>
public class FrontlineCommanderPower : AbstractPower
{
    public override string PowerId => "FrontlineCommander";
    public override string Name => "前线指挥";
    public override string GetDescription() => $"你在前排时，每回合开始获得 {Amount} 层超载。消耗超载时每层额外造成 1 点伤害。";

    public override void AtTurnStart()
    {
        if (Owner == null) return;
        // Only grant Overcharge when in the front row (card text: "你在前排时")
        var formation = ActionManager?.Combat?.Formation;
        if (formation != null && formation.GetPosition(Owner.Id) != FormationRow.Front)
            return;
        ActionManager?.AddToBottom(new ApplyPowerAction(Owner, new OverchargePower { Amount = Amount }));
    }

    public override float ModifyOverchargeConsumeDamage(float damage, int stacks)
        => damage + stacks;
}

/// <summary>Vanguard: consuming Overcharge grants 1 block per stack and draws 1 card per 3 stacks.</summary>
public class WarMachinePower : AbstractPower
{
    public override string PowerId => "WarMachine";
    public override string Name => "战争机器";
    public override string GetDescription() => "消耗超载时每层获得 1 点护甲。每消耗 3 层抽 1 张牌。";

    public override void OnOverchargeConsumed(int stacks)
    {
        if (Owner == null || stacks <= 0) return;
        ActionManager?.AddToBottom(new GainBlockAction(Owner, stacks));
        int draws = stacks / 3;
        if (draws > 0 && Owner is PlayerCharacter p)
        {
            var deck = ActionManager?.Combat?.PlayerDecks.GetValueOrDefault(p.Id);
            if (deck != null)
                ActionManager?.AddToBottom(new DrawCardAction(p, draws, deck));
        }
    }
}

/// <summary>Psion: gain Resonance each turn; Resonance >= 5 grants bonus attack damage.</summary>
public class ResonanceEnginePower : AbstractPower
{
    public override string PowerId => "ResonanceEngine";
    public override string Name => "共鸣引擎";
    public override string GetDescription() => $"每回合开始获得 {Amount} 层共鸣。共鸣 ≥ 5 时攻击伤害 +3。";

    public override void AtTurnStart()
    {
        if (Owner == null) return;
        ActionManager?.AddToBottom(new ApplyPowerAction(Owner, new ResonancePower { Amount = Amount }));
    }

    public override float ModifyAttackDamage(float baseDamage)
    {
        int resonance = Owner?.Powers.GetStacks(CommonPowerIds.Resonance) ?? 0;
        return resonance >= 5 ? baseDamage + 3 : baseDamage;
    }
}

/// <summary>Psion: Resonance also grants +1 damage per stack to attacks; Resonance decays 2 per turn.</summary>
public class TranscendentPsionPower : AbstractPower
{
    public override string PowerId => "TranscendentPsion";
    public override string Name => "超灵者";
    public override string GetDescription() => "每层共鸣使攻击伤害 +1。共鸣每回合末减少 2 层。";

    public override float ModifyAttackDamage(float baseDamage)
    {
        int resonance = Owner?.Powers.GetStacks(CommonPowerIds.Resonance) ?? 0;
        return baseDamage + resonance;
    }

    public override void AtTurnEnd()
    {
        if (Owner == null) return;
        int resonance = Owner.Powers.GetStacks(CommonPowerIds.Resonance);
        if (resonance > 0)
        {
            int decay = Math.Min(2, resonance);
            Owner.Powers.ConsumeStacks(CommonPowerIds.Resonance, decay);
        }
    }
}

/// <summary>Psion: auto-mark all enemies each turn; attacking marked enemies costs -1; kill marked heals 3.</summary>
public class MindSovereignPower : AbstractPower
{
    public override string PowerId => "MindSovereign";
    public override string Name => "心灵主宰";
    public override string GetDescription() => "每回合标记所有敌人。攻击被标记敌人费用 -1。击杀回复 3 HP。";

    public override void AtTurnStart()
    {
        if (Owner == null || ActionManager?.Combat == null) return;
        foreach (var enemy in ActionManager.Combat.Enemies.Where(e => e.IsAlive))
            ActionManager.AddToBottom(new ApplyPowerAction(enemy, new MarkPower { Amount = 1 }));
    }

    public override int ModifyCardCost(Card card, int baseCost, Combatant? target)
    {
        if (card.Data.Type == CardType.Attack && target != null && target.Powers.HasPower(CommonPowerIds.Mark))
            return Math.Max(0, baseCost - 1);
        return baseCost;
    }

    public override void OnKill(Combatant victim)
    {
        if (Owner != null && victim.Powers.HasPower(CommonPowerIds.Mark))
            ActionManager?.AddToBottom(new HealAction(Owner, 3));
    }
}

/// <summary>Netrunner: gain ProtocolStack each turn if played >= 2 program cards last turn.</summary>
public class PassiveScanPower : AbstractPower
{
    public override string PowerId => "PassiveScan";
    public override string Name => "被动扫描";
    public override string GetDescription() => "若上回合打出 ≥ 2 张「程序」牌，获得 2 层协议栈。";
    private int _programCardsLastTurn;
    private int _programCardsThisTurn;

    public override void OnCardPlayed(Card card)
    {
        if (card.Data.Type == CardType.Skill)
            _programCardsThisTurn++;
    }

    public override void AtTurnStart()
    {
        if (Owner == null) return;
        if (_programCardsLastTurn >= 2)
            ActionManager?.AddToBottom(new ApplyPowerAction(Owner, new ProtocolStackPower { Amount = 2 }));
        _programCardsLastTurn = _programCardsThisTurn;
        _programCardsThisTurn = 0;
    }
}

/// <summary>Netrunner: gain block each turn equal to ProtocolStack (max 5).</summary>
public class FirewallAbilityPower : AbstractPower
{
    public override string PowerId => "FirewallAbility";
    public override string Name => "防火墙";
    public override string GetDescription() => "每回合开始获得等同于协议栈层数的护甲（最多 5 点）。";

    public override void AtTurnStart()
    {
        if (Owner == null) return;
        int stacks = Owner.Powers.GetStacks(CommonPowerIds.ProtocolStack);
        int block = Math.Min(5, stacks);
        if (block > 0)
            ActionManager?.AddToBottom(new GainBlockAction(Owner, block));
    }
}

/// <summary>Netrunner: playing program cards grants +1 ProtocolStack.</summary>
public class PersistentLinkPower : AbstractPower
{
    public override string PowerId => "PersistentLink";
    public override string Name => "持久连接";
    public override string GetDescription() => "打出「程序」牌时额外获得 1 层协议栈。";

    public override void OnCardPlayed(Card card)
    {
        if (card.Data.Type == CardType.Skill && Owner != null)
            ActionManager?.AddToBottom(new ApplyPowerAction(Owner, new ProtocolStackPower { Amount = 1 }));
    }
}

/// <summary>Symbiote: losing HP deals damage to a random enemy (50% of HP lost).</summary>
public class TacticalErosionPower : AbstractPower
{
    public override string PowerId => "TacticalErosion";
    public override string Name => "战术侵蚀";
    public override string GetDescription() => "每次失去 HP 时，对随机敌人造成失去 HP 50% 的伤害。";

    public override void OnTakeDamage(int amount, Combatant? attacker = null)
    {
        if (Owner == null || ActionManager?.Combat == null || amount <= 0) return;
        int damage = Math.Max(1, amount / 2);
        var enemies = ActionManager.Combat.Enemies.Where(e => e.IsAlive).ToList();
        if (enemies.Count > 0)
        {
            var rng = new Random();
            var target = enemies[rng.Next(enemies.Count)];
            ActionManager.AddToTop(new DealDamageAction(Owner, [target], damage));
        }
    }
}

/// <summary>Symbiote: if HP below 50%, gain Strength and Energy at turn start.</summary>
public class BloodRitualPower : AbstractPower
{
    public override string PowerId => "BloodRitual";
    public override string Name => "血之仪式";
    public override string GetDescription() => "回合开始时，若 HP < 50%，获得 1 层力量和 1 能量。";

    public override void AtTurnStart()
    {
        if (Owner == null) return;
        if (Owner.CurrentHp <= Owner.MaxHp / 2)
        {
            ActionManager?.AddToBottom(new ApplyPowerAction(Owner, new StrengthPower { Amount = 1 }));
            if (Owner is PlayerCharacter player)
                ActionManager?.AddToBottom(new GainEnergyAction(player, 1));
        }
    }
}
