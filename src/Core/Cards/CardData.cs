namespace RogueCardGame.Core.Cards;

using RogueCardGame.Core.Combat;

/// <summary>
/// Immutable definition of a card loaded from data files.
/// Instances represent the "template"; runtime cards reference these.
/// </summary>
public sealed class CardData
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string NameEn { get; init; } = "";
    public required CardClass Class { get; init; }
    public required CardRarity Rarity { get; init; }
    public required CardType Type { get; init; }
    public required int Cost { get; init; }
    public CardRange Range { get; init; } = CardRange.None;
    /// <summary>
    /// Explicit target scope from JSON. If null, target type is inferred from the effect set
    /// (self-only effect lists resolve to Self; attack-bearing effect lists resolve to SingleEnemy).
    /// Consumers should read <see cref="EffectiveTargetType"/>.
    /// </summary>
    public TargetType? TargetType { get; init; }
    public required string Description { get; init; }
    public List<CardEffectData> Effects { get; init; } = [];
    /// <summary>Effects when played from the front row (overrides Effects if non-empty).</summary>
    public List<CardEffectData> FrontEffects { get; init; } = [];
    /// <summary>Effects when played from the back row (overrides Effects if non-empty).</summary>
    public List<CardEffectData> BackEffects { get; init; } = [];
    public CardUpgradeData? Upgrade { get; init; }
    public List<string> Keywords { get; init; } = [];
    /// <summary>
    /// Optional illustration path for this card.
    /// Supports full res:// paths, or relative names under resources/textures/cards/art/.
    /// When omitted, the UI falls back to convention-based discovery by class/id/nameEn and finally to generic type art.
    /// </summary>
    public string? ArtPath { get; init; }
    public string? SfxPath { get; init; }
    public string? Lore { get; init; }
    /// <summary>When true, this card is removed from the deck after one use (exhausts).</summary>
    public bool Exhaust { get; init; }
    // Link system (co-op, kept as data marker only)
    public bool IsLink { get; init; }
    public string? LinkDescription { get; init; }

    /// <summary>
    /// The resolved target type used by gameplay. When the JSON sets <see cref="TargetType"/>
    /// explicitly, that value is used. Otherwise the value is inferred from <see cref="Effects"/>
    /// (plus row-specific variants): if any effect requires an enemy target the card is
    /// <see cref="Core.Cards.TargetType.SingleEnemy"/>; otherwise <see cref="Core.Cards.TargetType.Self"/>.
    /// Power-type cards default to Self.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public TargetType EffectiveTargetType
    {
        get
        {
            if (TargetType.HasValue) return TargetType.Value;
            if (Type == CardType.Power) return Core.Cards.TargetType.Self;

            IEnumerable<CardEffectData> candidates = Effects
                .Concat(FrontEffects)
                .Concat(BackEffects);
            foreach (var eff in candidates)
            {
                if (CardEffectFactory.EffectRequiresEnemyTarget(eff))
                    return Core.Cards.TargetType.SingleEnemy;
            }
            return Core.Cards.TargetType.Self;
        }
    }
}

/// <summary>
/// Serializable effect entry in card data.
/// </summary>
public sealed class CardEffectData
{
    public required string Type { get; init; }
    public int Value { get; init; }
    public int SecondaryValue { get; init; }
    public string? StatusId { get; init; }
    public TargetType? OverrideTarget { get; init; }
    /// <summary>Optional card id for AddToDiscard effects.</summary>
    public string? CardId { get; init; }

    // --- Extended fields for class-specific mechanics ---
    /// <summary>Base damage for resonance/conditional effects.</summary>
    public int BaseDamage { get; init; }
    /// <summary>Multiplier for resonance/protocol/hp-based effects.</summary>
    public double Multiplier { get; init; } = 1;
    /// <summary>Damage per stack for consume effects (protocol, resonance).</summary>
    public int DamagePerStack { get; init; }
    /// <summary>Percentage for lifesteal, selfDamagePercent, hpToMaxHp.</summary>
    public int Percent { get; init; }
    /// <summary>Whether to consume stacks (protocol effects).</summary>
    public bool Consume { get; init; } = true;
    /// <summary>Cap for doubling effects.</summary>
    public int Cap { get; init; }
    /// <summary>Condition string for conditional effects (e.g., "resonance>=5", "lostHpThisTurn").</summary>
    public string? Condition { get; init; }
    /// <summary>Bonus damage for conditional effects.</summary>
    public int BonusDamage { get; init; }
    /// <summary>Duration for timed effects.</summary>
    public int Duration { get; init; }
    /// <summary>Power ID for applyPower effects.</summary>
    public string? PowerId { get; init; }
    /// <summary>Front-row specific effect for rowConditional.</summary>
    public CardEffectData? FrontEffect { get; init; }
    /// <summary>Back-row specific effect for rowConditional.</summary>
    public CardEffectData? BackEffect { get; init; }
    /// <summary>Override target as string from JSON (e.g. "self", "allEnemies").</summary>
    public string? Target { get; init; }
    /// <summary>Number of times this effect triggers (e.g., hit 3 times).</summary>
    public int Times { get; init; } = 1;
    /// <summary>Damage percentage multiplier for conditional damage with multiplier.</summary>
    public double DamageMultiplier { get; init; } = 1;
}

/// <summary>
/// A named upgrade branch (A or B variant).
/// </summary>
public sealed class CardUpgradeBranch
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public int? Cost { get; init; }
    public List<CardEffectData> Effects { get; init; } = [];
}

/// <summary>
/// Upgrade information for a card. Supports single upgrade or A/B branch choice.
/// </summary>
public sealed class CardUpgradeData
{
    public int? Cost { get; init; }
    public string? Description { get; init; }
    public List<CardEffectData> Effects { get; init; } = [];
    /// <summary>Branch A upgrade option (if branching upgrades are offered).</summary>
    public CardUpgradeBranch? BranchA { get; init; }
    /// <summary>Branch B upgrade option (if branching upgrades are offered).</summary>
    public CardUpgradeBranch? BranchB { get; init; }
    public bool HasBranches => BranchA != null && BranchB != null;
}
