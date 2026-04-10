namespace RogueCardGame.Core.Cards;

/// <summary>
/// A runtime card instance in a player's deck/hand/piles.
/// Wraps CardData with mutable state (upgraded, temp cost changes, etc.).
/// </summary>
public sealed class Card
{
    private static int _nextInstanceId = 0;

    public int InstanceId { get; }
    public CardData Data { get; }
    public bool IsUpgraded { get; private set; }

    /// <summary>
    /// Current cost (can be modified by status effects, environments, etc.).
    /// </summary>
    public int CurrentCost { get; set; }

    /// <summary>
    /// Temporary cost modifier that resets each turn.
    /// </summary>
    public int TempCostModifier { get; set; }

    public int EffectiveCost => Math.Max(0, CurrentCost + TempCostModifier);

    public string DisplayName => IsUpgraded ? $"{Data.Name}★" : Data.Name;

    public List<CardEffectData> ActiveEffects =>
        IsUpgraded && Data.Upgrade?.Effects.Count > 0
            ? Data.Upgrade.Effects
            : Data.Effects;

    public string ActiveDescription =>
        IsUpgraded && Data.Upgrade?.Description != null
            ? Data.Upgrade.Description
            : Data.Description;

    public Card(CardData data)
    {
        InstanceId = Interlocked.Increment(ref _nextInstanceId);
        Data = data;
        CurrentCost = data.Cost;
    }

    public void Upgrade()
    {
        if (IsUpgraded) return;
        IsUpgraded = true;
        if (Data.Upgrade?.Cost.HasValue == true)
        {
            CurrentCost = Data.Upgrade.Cost.Value;
        }
    }

    public void ResetTempModifiers()
    {
        TempCostModifier = 0;
    }

    public Card Clone()
    {
        var clone = new Card(Data)
        {
            IsUpgraded = IsUpgraded,
            CurrentCost = CurrentCost
        };
        return clone;
    }
}
