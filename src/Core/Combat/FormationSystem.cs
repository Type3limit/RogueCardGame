using RogueCardGame.Core;

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
    public float BackRowFirstRangedBonus => BalanceConfig.Current.Formation.BackRowFirstRangedBonus;
    public int FrontRowBlockBonus => BalanceConfig.Current.Formation.FrontRowBlockBonus;

    private readonly Dictionary<int, FormationRow> _positions = new();
    private readonly HashSet<int> _usedMoveThisTurn = new();
    // Tracks how many ranged cards each combatant has played this turn
    private readonly Dictionary<int, int> _rangedPlaysThisTurn = new();

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
    /// Back-row ranged: +20% for the FIRST ranged play this turn only.
    /// Back-row melee: cards are disallowed at the CombatManager level; multiplier = 1.0 as fallback.
    /// </summary>
    public float GetDamageMultiplier(int attackerId, Cards.CardRange range)
    {
        var row = GetPosition(attackerId);
        if (row == FormationRow.Back && range == Cards.CardRange.Ranged)
        {
            int plays = _rangedPlaysThisTurn.GetValueOrDefault(attackerId, 0);
            return plays == 0 ? BackRowFirstRangedBonus : 1.0f;
        }
        return 1.0f;
    }

    /// <summary>
    /// Record that a ranged card was played by a combatant this turn.
    /// Call this after calculating the multiplier.
    /// </summary>
    public void RecordRangedPlay(int combatantId)
    {
        _rangedPlaysThisTurn[combatantId] =
            _rangedPlaysThisTurn.GetValueOrDefault(combatantId, 0) + 1;
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

    public bool HasMovedThisTurn(int combatantId) => _usedMoveThisTurn.Contains(combatantId);

    public void ResetTurnMoves()
    {
        _usedMoveThisTurn.Clear();
        _rangedPlaysThisTurn.Clear();
    }

    public void Clear()
    {
        _positions.Clear();
        _usedMoveThisTurn.Clear();
        _rangedPlaysThisTurn.Clear();
    }
}
