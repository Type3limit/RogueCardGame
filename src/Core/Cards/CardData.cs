namespace RogueCardGame.Core.Cards;

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
    public required CardRange Range { get; init; }
    public required TargetType TargetType { get; init; }
    public required string Description { get; init; }
    public bool IsLink { get; init; } = false;
    public string? LinkDescription { get; init; }
    public List<CardEffectData> Effects { get; init; } = [];
    public CardUpgradeData? Upgrade { get; init; }
    public List<string> Keywords { get; init; } = [];
    public string? ArtPath { get; init; }
    public string? SfxPath { get; init; }
    public string? Lore { get; init; }
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
}

/// <summary>
/// Upgrade information for a card.
/// </summary>
public sealed class CardUpgradeData
{
    public int? Cost { get; init; }
    public string? Description { get; init; }
    public List<CardEffectData> Effects { get; init; } = [];
}
