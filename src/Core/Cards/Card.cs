namespace RogueCardGame.Core.Cards;

public enum UpgradeBranch { None, Standard, A, B }

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
    public UpgradeBranch Branch { get; private set; } = UpgradeBranch.None;

    /// <summary>
    /// Current cost (can be modified by status effects, environments, etc.).
    /// </summary>
    public int CurrentCost { get; set; }

    /// <summary>
    /// Temporary cost modifier that resets each turn.
    /// </summary>
    public int TempCostModifier { get; set; }

    public int EffectiveCost => Math.Max(0, CurrentCost + TempCostModifier);

    public string DisplayName => IsUpgraded
        ? Branch switch
        {
            UpgradeBranch.A => $"{Data.Name}★A",
            UpgradeBranch.B => $"{Data.Name}★B",
            _ => $"{Data.Name}★"
        }
        : Data.Name;

    public List<CardEffectData> ActiveEffects =>
        Branch switch
        {
            UpgradeBranch.A when Data.Upgrade?.BranchA?.Effects.Count > 0 => Data.Upgrade.BranchA.Effects,
            UpgradeBranch.B when Data.Upgrade?.BranchB?.Effects.Count > 0 => Data.Upgrade.BranchB.Effects,
            UpgradeBranch.Standard when Data.Upgrade?.Effects.Count > 0   => Data.Upgrade.Effects,
            _ => Data.Effects
        };

    public string ActiveDescription =>
        Branch switch
        {
            UpgradeBranch.A => Data.Upgrade?.BranchA?.Description ?? Data.Description,
            UpgradeBranch.B => Data.Upgrade?.BranchB?.Description ?? Data.Description,
            UpgradeBranch.Standard => Data.Upgrade?.Description ?? Data.Description,
            _ => Data.Description
        };

    /// <summary>True if the card has position-reactive effects defined.</summary>
    public bool IsPositionReactive =>
        Data.FrontEffects.Count > 0 || Data.BackEffects.Count > 0;

    /// <summary>
    /// Get effects for a specific row. Falls back to ActiveEffects when row effects are empty.
    /// </summary>
    public List<CardEffectData> GetEffectsForRow(Combat.FormationRow row) =>
        row switch
        {
            Combat.FormationRow.Front when Data.FrontEffects.Count > 0 => Data.FrontEffects,
            Combat.FormationRow.Back  when Data.BackEffects.Count > 0  => Data.BackEffects,
            _ => ActiveEffects
        };

    public Card(CardData data)
    {
        InstanceId = Interlocked.Increment(ref _nextInstanceId);
        Data = data;
        CurrentCost = data.Cost;
    }

    public void Upgrade(UpgradeBranch branch = UpgradeBranch.Standard)
    {
        if (IsUpgraded) return;
        IsUpgraded = true;
        Branch = branch;

        int? costOverride = branch switch
        {
            UpgradeBranch.A => Data.Upgrade?.BranchA?.Cost,
            UpgradeBranch.B => Data.Upgrade?.BranchB?.Cost,
            _               => Data.Upgrade?.Cost
        };
        if (costOverride.HasValue)
            CurrentCost = costOverride.Value;
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
            Branch = Branch,
            CurrentCost = CurrentCost
        };
        return clone;
    }
}
