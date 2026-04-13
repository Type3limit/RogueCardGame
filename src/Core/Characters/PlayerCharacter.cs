using RogueCardGame.Core.Cards;
using RogueCardGame.Core.Combat;
using RogueCardGame.Core.Combat.Powers;

namespace RogueCardGame.Core.Characters;

/// <summary>
/// A player character in combat with deck, energy, class mechanics, etc.
/// </summary>
public class PlayerCharacter : Combatant
{
    public CardClass Class { get; }
    public FormationRow PreferredRow { get; set; }

    // Energy
    public int MaxEnergy { get; set; } = 3;
    public int CurrentEnergy { get; set; }

    // Draw
    public int DrawPerTurn { get; set; } = 5;

    // Multiplayer
    public int SpeedValue { get; set; } = 10;
    public bool IsDowned { get; set; }
    public int DownedTurnsLeft { get; set; }

    // Class-specific resource display name (can be overridden by ClassDatabase)
    public string ClassResourceName { get; set; } = "";

    private string DefaultClassResourceName => Class switch
    {
        CardClass.Vanguard => "超载",
        CardClass.Psion => "共鸣",
        CardClass.Netrunner => "协议栈",
        CardClass.Symbiote => "侵蚀",
        _ => ""
    };

    public string EffectiveClassResourceName => 
        string.IsNullOrEmpty(ClassResourceName) ? DefaultClassResourceName : ClassResourceName;

    public int ClassResourceValue => Class switch
    {
        CardClass.Vanguard => Powers.GetStacks(CommonPowerIds.Overcharge),
        CardClass.Psion => Powers.GetStacks(CommonPowerIds.Resonance),
        _ => 0
    };

    public PlayerCharacter(string name, CardClass @class, int maxHp)
        : base(name, maxHp)
    {
        Class = @class;
        PreferredRow = @class switch
        {
            CardClass.Vanguard => FormationRow.Front,
            CardClass.Psion => FormationRow.Back,
            CardClass.Netrunner => FormationRow.Back,
            CardClass.Symbiote => FormationRow.Front,
            _ => FormationRow.Front
        };
    }

    public override void OnTurnStart()
    {
        base.OnTurnStart();
        CurrentEnergy = MaxEnergy;
    }

    public bool CanPlayCard(Card card)
    {
        return CurrentEnergy >= card.EffectiveCost && !IsDowned;
    }

    public void SpendEnergy(int amount)
    {
        CurrentEnergy = Math.Max(0, CurrentEnergy - amount);
    }

    /// <summary>
    /// Handle downed state. Returns true if the player is permanently out.
    /// </summary>
    public bool TickDowned()
    {
        if (!IsDowned) return false;
        DownedTurnsLeft--;
        return DownedTurnsLeft <= 0;
    }

    public void GoDown()
    {
        IsDowned = true;
        DownedTurnsLeft = 3;
        CurrentHp = 0;
    }

    public void Revive(int hp)
    {
        IsDowned = false;
        DownedTurnsLeft = 0;
        CurrentHp = Math.Max(1, hp);
    }

    /// <summary>
    /// Create a default Vanguard character.
    /// </summary>
    public static PlayerCharacter CreateVanguard(string name = "先锋") =>
        new(name, CardClass.Vanguard, 85);

    /// <summary>
    /// Create a default Psion character.
    /// </summary>
    public static PlayerCharacter CreatePsion(string name = "灵能者") =>
        new(name, CardClass.Psion, 65);

    /// <summary>
    /// Create a default Netrunner character.
    /// </summary>
    public static PlayerCharacter CreateNetrunner(string name = "黑客") =>
        new(name, CardClass.Netrunner, 70);

    /// <summary>
    /// Create a default Symbiote character.
    /// </summary>
    public static PlayerCharacter CreateSymbiote(string name = "共生体") =>
        new(name, CardClass.Symbiote, 75);
}
