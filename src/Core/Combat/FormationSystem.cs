namespace RogueCardGame.Core.Combat;

/// <summary>
/// Represents the front/back row position on the battlefield.
/// </summary>
public enum FormationRow
{
    Front,
    Back
}

/// <summary>
/// Manages the formation (front/back row) system for all combatants.
/// Handles position assignment, switching, aggro routing, and range modifiers.
/// </summary>
public class FormationSystem
{
    /// <summary>
    /// Damage modifier when a melee card is played from the back row.
    /// </summary>
    public const float BackRowMeleePenalty = 0.75f;

    /// <summary>
    /// Damage bonus when a ranged card is played from the back row.
    /// </summary>
    public const float BackRowRangedBonus = 1.15f;

    /// <summary>
    /// Extra block gained when acquiring armor in the front row.
    /// </summary>
    public const int FrontRowBlockBonus = 2;

    private readonly Dictionary<int, FormationRow> _positions = new();
    private readonly HashSet<int> _usedMoveThisTurn = new();

    public FormationRow GetPosition(int combatantId)
    {
        return _positions.GetValueOrDefault(combatantId, FormationRow.Front);
    }

    public void SetPosition(int combatantId, FormationRow row)
    {
        _positions[combatantId] = row;
    }

    /// <summary>
    /// Try to switch a combatant's row. Returns false if they already moved this turn.
    /// </summary>
    public bool TrySwitchRow(int combatantId, bool freeSwitch = false)
    {
        if (!freeSwitch && _usedMoveThisTurn.Contains(combatantId))
            return false;

        var current = GetPosition(combatantId);
        _positions[combatantId] = current == FormationRow.Front
            ? FormationRow.Back
            : FormationRow.Front;

        if (!freeSwitch)
            _usedMoveThisTurn.Add(combatantId);

        return true;
    }

    /// <summary>
    /// Force a combatant to a specific row (e.g., via "pull" or "push" card effects).
    /// Ignores move-per-turn restriction.
    /// </summary>
    public void ForcePosition(int combatantId, FormationRow row)
    {
        _positions[combatantId] = row;
    }

    /// <summary>
    /// Calculate damage multiplier based on attacker position and card range.
    /// </summary>
    public float GetDamageMultiplier(int attackerId, Cards.CardRange range)
    {
        var row = GetPosition(attackerId);

        return (row, range) switch
        {
            (FormationRow.Back, Cards.CardRange.Melee) => BackRowMeleePenalty,
            (FormationRow.Back, Cards.CardRange.Ranged) => BackRowRangedBonus,
            _ => 1.0f
        };
    }

    /// <summary>
    /// Calculate bonus block for front row defenders.
    /// </summary>
    public int GetBlockBonus(int defenderId)
    {
        return GetPosition(defenderId) == FormationRow.Front ? FrontRowBlockBonus : 0;
    }

    /// <summary>
    /// Check if a target is "covered" (has allies in front row blocking access).
    /// Back-row targets are covered when any ally is in the front row.
    /// </summary>
    public bool IsTargetCovered(int targetId, IEnumerable<int> alliedIds)
    {
        if (GetPosition(targetId) != FormationRow.Back)
            return false;

        return alliedIds.Any(id => id != targetId && GetPosition(id) == FormationRow.Front);
    }

    public void ResetTurnMoves()
    {
        _usedMoveThisTurn.Clear();
    }

    public void Clear()
    {
        _positions.Clear();
        _usedMoveThisTurn.Clear();
    }
}
