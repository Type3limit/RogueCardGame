using RogueCardGame.Core;

namespace RogueCardGame.Core.Combat;

/// <summary>
/// Manages aggro (threat) values for all combatants.
/// Enemies use aggro to decide which player to attack within the same row.
/// </summary>
public class AggroSystem
{
    private readonly Dictionary<int, float> _aggroValues = new();

    public static float AggroPerDamage => BalanceConfig.Current.Aggro.AggroPerDamage;
    public static float AggroPerHealing => BalanceConfig.Current.Aggro.AggroPerHealing;
    public static float AggroPerBuff => BalanceConfig.Current.Aggro.AggroPerBuff;

    public float GetAggro(int combatantId)
    {
        return _aggroValues.GetValueOrDefault(combatantId, 0f);
    }

    public void AddAggro(int combatantId, float amount)
    {
        _aggroValues[combatantId] = GetAggro(combatantId) + amount;
    }

    public void SetAggro(int combatantId, float value)
    {
        _aggroValues[combatantId] = value;
    }

    /// <summary>
    /// Select the target from a group of candidates based on aggro + formation rules.
    /// For single-target attacks: pick highest aggro among front-row players.
    /// If no front-row players, pick highest aggro among back-row.
    /// </summary>
    public int SelectTarget(
        IEnumerable<int> candidates,
        FormationSystem formation,
        bool ignoreFormation = false)
    {
        var candidateList = candidates.ToList();
        if (candidateList.Count == 0)
            throw new InvalidOperationException("No valid targets");

        if (candidateList.Count == 1)
            return candidateList[0];

        if (!ignoreFormation)
        {
            var frontRow = candidateList
                .Where(id => formation.GetPosition(id) == FormationRow.Front)
                .ToList();

            if (frontRow.Count > 0)
                candidateList = frontRow;
        }

        // Among remaining candidates, pick highest aggro
        return candidateList
            .OrderByDescending(id => GetAggro(id))
            .First();
    }

    /// <summary>
    /// Check if a specific combatant has Taunt active.
    /// If so, they become the forced target regardless of aggro.
    /// </summary>
    public int? GetTauntTarget(IEnumerable<int> candidates, Func<int, bool> hasTaunt)
    {
        var taunters = candidates.Where(hasTaunt).ToList();
        if (taunters.Count == 0) return null;

        // If multiple taunters, pick highest aggro among them
        return taunters.OrderByDescending(id => GetAggro(id)).First();
    }

    /// <summary>
    /// Decay all aggro values slightly each turn to prevent permanent lock-on.
    /// </summary>
    public void DecayAggro(float? decayRate = null)
    {
        float rate = decayRate ?? BalanceConfig.Current.Aggro.AggroDecayRate;
        foreach (var key in _aggroValues.Keys.ToList())
        {
            _aggroValues[key] *= rate;
        }
    }

    public void RemoveCombatant(int combatantId)
    {
        _aggroValues.Remove(combatantId);
    }

    public void Clear()
    {
        _aggroValues.Clear();
    }
}
