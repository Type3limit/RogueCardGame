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
    Colorless,
    Vanguard,
    Psion,
    Netrunner,
    Symbiote
}

/// <summary>
/// Range type determines how positioning affects damage.
/// </summary>
public enum CardRange
{
    Melee,  // Front row: full damage; Back row: -25% damage
    Ranged, // Front row: normal; Back row: +15% damage
    None    // No range modifier (skills, powers)
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
