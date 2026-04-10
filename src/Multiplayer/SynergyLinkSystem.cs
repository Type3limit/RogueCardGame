using System;
using System.Collections.Generic;
using System.Linq;
using RogueCardGame.Core.Cards;

namespace RogueCardGame.Multiplayer;

// ─────────────────────────────────────────────────────────────
// Synergy Link System — Core multiplayer cooperation mechanic
// ─────────────────────────────────────────────────────────────

public enum LinkType
{
    Attack,   // A marks → B attacks = bonus damage
    Defense,  // A shields → B defends = bonus armor for both
    Amplify,  // A debuffs → B attacks = ignore armor / extra dmg
    Chain,    // A debuff → B elemental attack = trigger explosion
    Heal      // A heals → B heals = 1.5x healing for both
}

public enum LinkState
{
    Inactive,
    Waiting,   // Link card played, waiting for response
    Triggered, // Response received, link activated
    Expired    // Turn ended without response
}

/// <summary>
/// Represents a pending link waiting for a response.
/// </summary>
public class PendingLink
{
    public string LinkId { get; set; } = "";
    public int InitiatorPeerId { get; set; }
    public string InitiatorCardId { get; set; } = "";
    public LinkType Type { get; set; }
    public string TargetEnemyId { get; set; } = ""; // For targeted links
    public LinkState State { get; set; } = LinkState.Waiting;
    public int TurnCreated { get; set; }

    // Link bonus values
    public int BonusDamage { get; set; }
    public int BonusBlock { get; set; }
    public int BonusHeal { get; set; }
    public float DamageMultiplier { get; set; } = 1f;
    public bool IgnoreArmor { get; set; }
    public string BonusStatusId { get; set; } = "";
    public int BonusStatusStacks { get; set; }

    // Response conditions
    public LinkResponseCondition ResponseCondition { get; set; } = new();
}

/// <summary>
/// Conditions for a link response to trigger.
/// </summary>
public class LinkResponseCondition
{
    public bool RequireAttack { get; set; }
    public bool RequireDefend { get; set; }
    public bool RequireHeal { get; set; }
    public bool RequireSameTarget { get; set; }
    public bool RequireDifferentTarget { get; set; }
    public string RequireCardType { get; set; } = ""; // empty = any
    public int RequireResponderInRow { get; set; } = -1; // -1 = any, 0 = front, 1 = back
}

/// <summary>
/// Result of a triggered link for applying bonuses.
/// </summary>
public class LinkResult
{
    public string LinkId { get; set; } = "";
    public int InitiatorPeerId { get; set; }
    public int ResponderPeerId { get; set; }
    public LinkType Type { get; set; }
    public int BonusDamage { get; set; }
    public int BonusBlock { get; set; }
    public int BonusHeal { get; set; }
    public float DamageMultiplier { get; set; } = 1f;
    public bool IgnoreArmor { get; set; }
    public string BonusStatusId { get; set; } = "";
    public int BonusStatusStacks { get; set; }
}

// ─────────────────────────────────────────────────────────────
// SynergyLinkSystem
// ─────────────────────────────────────────────────────────────

/// <summary>
/// Manages synergy link cards in multiplayer combat.
/// When a player plays a link card, it enters a pending state.
/// If another player responds with a qualifying card before end of turn,
/// the link triggers with enhanced effects.
/// If no response, the base (weaker) effect applies.
/// </summary>
public class SynergyLinkSystem
{
    private readonly List<PendingLink> _pendingLinks = new();
    private readonly List<LinkResult> _resolvedLinks = new();
    private int _linkIdCounter;

    public event Action<PendingLink>? OnLinkCreated;
    public event Action<LinkResult>? OnLinkTriggered;
    public event Action<PendingLink>? OnLinkExpired;

    /// <summary>
    /// Create a pending link when a link card is played.
    /// </summary>
    public PendingLink CreateLink(int initiatorPeerId, string cardId, LinkType type,
        int turnNumber, string targetEnemyId = "")
    {
        var link = new PendingLink
        {
            LinkId = $"link_{++_linkIdCounter}",
            InitiatorPeerId = initiatorPeerId,
            InitiatorCardId = cardId,
            Type = type,
            TargetEnemyId = targetEnemyId,
            State = LinkState.Waiting,
            TurnCreated = turnNumber
        };

        // Set default bonuses and conditions based on link type
        ApplyLinkTypeDefaults(link);

        _pendingLinks.Add(link);
        OnLinkCreated?.Invoke(link);
        return link;
    }

    /// <summary>
    /// Check if a card played by another player triggers any pending links.
    /// Returns the triggered link result, or null if no link triggered.
    /// </summary>
    public LinkResult? TryTriggerLink(int responderPeerId, CardData card,
        string targetEnemyId, bool isFrontRow)
    {
        // Can't respond to your own link
        var pendingLink = _pendingLinks.FirstOrDefault(l =>
            l.State == LinkState.Waiting &&
            l.InitiatorPeerId != responderPeerId &&
            CheckResponseCondition(l, card, targetEnemyId, isFrontRow));

        if (pendingLink == null)
            return null;

        pendingLink.State = LinkState.Triggered;

        var result = new LinkResult
        {
            LinkId = pendingLink.LinkId,
            InitiatorPeerId = pendingLink.InitiatorPeerId,
            ResponderPeerId = responderPeerId,
            Type = pendingLink.Type,
            BonusDamage = pendingLink.BonusDamage,
            BonusBlock = pendingLink.BonusBlock,
            BonusHeal = pendingLink.BonusHeal,
            DamageMultiplier = pendingLink.DamageMultiplier,
            IgnoreArmor = pendingLink.IgnoreArmor,
            BonusStatusId = pendingLink.BonusStatusId,
            BonusStatusStacks = pendingLink.BonusStatusStacks
        };

        _resolvedLinks.Add(result);
        OnLinkTriggered?.Invoke(result);
        return result;
    }

    /// <summary>
    /// End of turn: expire all unresolved links.
    /// Expired links apply their base (non-linked) effect only.
    /// </summary>
    public List<PendingLink> ExpireLinks(int turnNumber)
    {
        var expired = _pendingLinks
            .Where(l => l.State == LinkState.Waiting && l.TurnCreated <= turnNumber)
            .ToList();

        foreach (var link in expired)
        {
            link.State = LinkState.Expired;
            OnLinkExpired?.Invoke(link);
        }

        // Clean up old links
        _pendingLinks.RemoveAll(l => l.State != LinkState.Waiting);

        return expired;
    }

    /// <summary>
    /// Get all currently pending links (for UI display).
    /// </summary>
    public IReadOnlyList<PendingLink> GetPendingLinks() => _pendingLinks
        .Where(l => l.State == LinkState.Waiting).ToList();

    /// <summary>
    /// Get links triggered this turn (for VFX/feedback).
    /// </summary>
    public IReadOnlyList<LinkResult> GetResolvedLinks() => _resolvedLinks;

    /// <summary>
    /// Clear resolved links at start of new turn.
    /// </summary>
    public void ClearResolved()
    {
        _resolvedLinks.Clear();
    }

    // ─────── Internal helpers ───────

    private bool CheckResponseCondition(PendingLink link, CardData card,
        string targetEnemyId, bool isFrontRow)
    {
        var cond = link.ResponseCondition;

        if (cond.RequireAttack && card.Type != CardType.Attack) return false;
        if (cond.RequireDefend && card.Type != CardType.Skill) return false;
        if (cond.RequireHeal && card.Type != CardType.Skill) return false;

        if (cond.RequireSameTarget &&
            !string.IsNullOrEmpty(link.TargetEnemyId) &&
            targetEnemyId != link.TargetEnemyId)
            return false;

        if (cond.RequireDifferentTarget &&
            !string.IsNullOrEmpty(link.TargetEnemyId) &&
            targetEnemyId == link.TargetEnemyId)
            return false;

        if (cond.RequireResponderInRow >= 0)
        {
            bool needFront = cond.RequireResponderInRow == 0;
            if (needFront != isFrontRow) return false;
        }

        return true;
    }

    private static void ApplyLinkTypeDefaults(PendingLink link)
    {
        switch (link.Type)
        {
            case LinkType.Attack:
                link.BonusDamage = 8;
                link.ResponseCondition = new LinkResponseCondition
                {
                    RequireAttack = true,
                    RequireSameTarget = true
                };
                break;

            case LinkType.Defense:
                link.BonusBlock = 6;
                link.ResponseCondition = new LinkResponseCondition
                {
                    RequireDefend = true
                };
                break;

            case LinkType.Amplify:
                link.DamageMultiplier = 1.5f;
                link.IgnoreArmor = true;
                link.ResponseCondition = new LinkResponseCondition
                {
                    RequireAttack = true
                };
                break;

            case LinkType.Chain:
                link.BonusDamage = 12;
                link.BonusStatusId = "Vulnerable";
                link.BonusStatusStacks = 2;
                link.ResponseCondition = new LinkResponseCondition
                {
                    RequireAttack = true
                };
                break;

            case LinkType.Heal:
                link.BonusHeal = 5;
                link.ResponseCondition = new LinkResponseCondition
                {
                    RequireHeal = true
                };
                break;
        }
    }
}
