using RogueCardGame.Core.Cards;
using RogueCardGame.Core.Combat;

namespace RogueCardGame.Core;

/// <summary>
/// Prototype Core identity — emerges automatically from observed player behavior
/// in the first few turns, giving the run a build identity before the first Boss.
/// This is a lightweight signal, not a full system: 1 passive bonus per run.
/// </summary>
public enum PrototypeIdentity
{
    None,
    /// <summary>Observed frequent front-row melee play. Bonus: first melee each turn gains +1 Overcharge.</summary>
    FrontPressProtocol,
    /// <summary>Observed frequent row-switching (tactical). Bonus: after switching, next card costs 1 less.</summary>
    TacticalProtocol,
    /// <summary>Observed frequent back-row ranged play. Bonus: first ranged card each turn deals +3 damage.</summary>
    RangedSuppressProtocol,
}

/// <summary>
/// Tracks early-run player behavior and auto-activates a Prototype Core identity.
/// Should be called by CombatManager after each card is played in the first run.
/// </summary>
public class PrototypeCoreSystem
{
    private int _frontMeleeCount;
    private int _backRangedCount;
    private int _switchCount;
    private int _turnsObserved;
    private bool _activated;

    public PrototypeIdentity Identity { get; private set; } = PrototypeIdentity.None;
    public bool IsActivated => _activated;

    /// <summary>Activation requires this many turns of observation.</summary>
    public const int ActivationTurns = 3;

    /// <summary>
    /// Record a card play. Call from CombatManager on TryPlayCard success.
    /// </summary>
    public void RecordCardPlay(CardRange range, FormationRow row)
    {
        if (_activated) return;

        if (range == CardRange.Melee && row == FormationRow.Front)
            _frontMeleeCount++;
        else if (range == CardRange.Ranged && row == FormationRow.Back)
            _backRangedCount++;
    }

    /// <summary>
    /// Record a row switch. Call from CombatManager on TrySwitchRow success.
    /// </summary>
    public void RecordSwitch()
    {
        if (_activated) return;
        _switchCount++;
    }

    /// <summary>
    /// Call at end of each player turn. Once enough turns are observed, activate identity.
    /// Returns the activated identity description if newly activated, else null.
    /// </summary>
    public string? AdvanceTurn()
    {
        if (_activated) return null;
        _turnsObserved++;
        if (_turnsObserved < ActivationTurns) return null;

        // Determine identity from observed behavior
        int maxScore = Math.Max(_frontMeleeCount, Math.Max(_backRangedCount, _switchCount));
        if (maxScore == 0)
        {
            Identity = PrototypeIdentity.TacticalProtocol; // default
        }
        else if (_frontMeleeCount >= _backRangedCount && _frontMeleeCount >= _switchCount)
        {
            Identity = PrototypeIdentity.FrontPressProtocol;
        }
        else if (_switchCount >= _frontMeleeCount && _switchCount >= _backRangedCount)
        {
            Identity = PrototypeIdentity.TacticalProtocol;
        }
        else
        {
            Identity = PrototypeIdentity.RangedSuppressProtocol;
        }

        _activated = true;
        return GetActivationMessage();
    }

    /// <summary>
    /// Apply the prototype bonus when playing a card (called from combat context).
    /// Returns extra bonus damage or 0 if none.
    /// </summary>
    public int GetBonusDamage(CardRange range, FormationRow row, bool isFirstOfTypeThisTurn)
    {
        if (!_activated || !isFirstOfTypeThisTurn) return 0;
        return Identity switch
        {
            PrototypeIdentity.RangedSuppressProtocol when range == CardRange.Ranged => 3,
            _ => 0
        };
    }

    /// <summary>
    /// Get temp cost reduction that applies to next card after switching rows.
    /// </summary>
    public int GetPostSwitchCostReduction()
    {
        if (!_activated) return 0;
        return Identity == PrototypeIdentity.TacticalProtocol ? 1 : 0;
    }

    private string GetActivationMessage() => Identity switch
    {
        PrototypeIdentity.FrontPressProtocol
            => "【原型核心激活】前压协议：每回合第一张近战牌获得 +1 超载",
        PrototypeIdentity.TacticalProtocol
            => "【原型核心激活】战术协议：换位后下一张牌费用 -1",
        PrototypeIdentity.RangedSuppressProtocol
            => "【原型核心激活】远程压制协议：每回合第一张远程牌额外 +3 伤害",
        _ => "【原型核心激活】"
    };

    public string GetDescription() => Identity switch
    {
        PrototypeIdentity.FrontPressProtocol  => "前压协议：每回合第一张近战牌 +1 超载",
        PrototypeIdentity.TacticalProtocol    => "战术协议：换位后下一张牌费用 -1",
        PrototypeIdentity.RangedSuppressProtocol => "远程压制协议：每回合第一张远程牌 +3 伤害",
        _                                     => "(未激活)"
    };
}
