namespace RogueCardGame.Core.Cards;

/// <summary>
/// Card types determine base behavior and UI styling.
/// </summary>
public enum CardType
{
    Attack,
    Skill,
    Power,
    Status,
    Curse
}

/// <summary>
/// Card rarity affects drop rates and visual styling.
/// </summary>
public enum CardRarity
{
    Starter,
    Common,
    Uncommon,
    Rare,
    Special // events, boss rewards, etc.
}

/// <summary>
/// Character class that owns the card. Colorless cards are available to all.
/// </summary>
public enum CardClass
{
    Neutral,
    Colorless,
    Vanguard,
    Psion,
    Netrunner,
    Symbiote
}

/// <summary>
/// Range type determines how positioning affects damage.
/// v3 rules: Melee = disabled in back row; Ranged = first card from back row +20%.
/// </summary>
public enum CardRange
{
    Melee,  // Can only be played from front row (back row: disabled)
    Ranged, // Back row: first play per turn +20%, subsequent plays normal
    None    // No range restriction or modifier (skills, powers, position-reactive)
}

/// <summary>
/// Target scope for AOE/single-target cards.
/// </summary>
public enum TargetType
{
    SingleEnemy,
    AllEnemies,
    FrontRowEnemies,
    BackRowEnemies,
    Self,
    SingleAlly,
    AllAllies,
    FrontRowAllies,
    BackRowAllies,
    All,
    None // self-only effects with no explicit target
}
