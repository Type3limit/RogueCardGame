using RogueCardGame.Core.Combat;

namespace RogueCardGame.Core.Characters;

/// <summary>
/// Enemy intent types - what the enemy plans to do on its turn.
/// </summary>
public enum EnemyIntentType
{
    Attack,
    AttackDefend,
    Defend,
    Buff,
    Debuff,
    Summon,
    Heal,
    Special,
    Unknown
}

/// <summary>
/// An enemy's declared intent for the current turn.
/// </summary>
public sealed class EnemyIntent
{
    public EnemyIntentType Type { get; init; }
    public int Value { get; init; }
    public int HitCount { get; init; } = 1;
    public TargetScope Scope { get; init; } = TargetScope.SingleFront;
    public string? Description { get; init; }
    public List<string> Keywords { get; init; } = [];
}

/// <summary>
/// Targeting scope for enemy attacks.
/// </summary>
public enum TargetScope
{
    SingleFront,     // Highest aggro in front row
    SingleBack,      // Targets back row directly (sniper)
    SingleAny,       // Highest aggro regardless of row
    AllFront,        // All front-row players
    AllBack,         // All back-row players
    All,             // All players
    Self,            // Self-buff/heal
    AllEnemies       // Buff all allies (from enemy perspective)
}

/// <summary>
/// A behavior phase for multi-phase enemies. Activated at HP thresholds.
/// </summary>
public sealed class EnemyPhase
{
    public float HpThreshold { get; init; }  // 0.0–1.0, activates at or below this HP fraction
    public List<EnemyIntentPattern> IntentPatterns { get; init; } = [];
    public string? EntryEffect { get; init; } // Optional description of phase-change ability
}

/// <summary>
/// Data definition for an enemy type, loaded from JSON.
/// </summary>
public sealed class EnemyData
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required int MaxHp { get; init; }
    public required FormationRow PreferredRow { get; init; }
    public required List<EnemyIntentPattern> IntentPatterns { get; init; }
    /// <summary>Optional phase list for multi-phase enemies (boss, elite).</summary>
    public List<EnemyPhase> Phases { get; init; } = [];
    public int MinHpScalePerPlayer { get; init; } = 0;
    public string? ArtPath { get; init; }
    public bool IsElite { get; init; }
    public bool IsBoss { get; init; }
}

/// <summary>
/// A pattern entry in an enemy's intent sequence/pool.
/// </summary>
public sealed class EnemyIntentPattern
{
    public required EnemyIntentType Type { get; init; }
    public required int Value { get; init; }
    public int HitCount { get; init; } = 1;
    public TargetScope Scope { get; init; } = TargetScope.SingleFront;
    public float Weight { get; init; } = 1.0f;
    public string? Description { get; init; }
    /// <summary>
    /// Position keywords that modify how this intent works.
    /// Supported: "lock" (apply Rooted to target), "breakCover" (splash to back row).
    /// </summary>
    public List<string> Keywords { get; init; } = [];
}

/// <summary>
/// Runtime enemy instance in combat.
/// </summary>
public class Enemy : Combatant
{
    public EnemyData Data { get; }
    public EnemyIntent? CurrentIntent { get; set; }
    private int _currentPhaseIndex = -1; // -1 = using base patterns

    public Enemy(EnemyData data, int playerCount = 1)
        : base(data.Name, data.MaxHp + data.MinHpScalePerPlayer * Math.Max(0, playerCount - 1))
    {
        Data = data;
    }

    /// <summary>
    /// Get the active intent patterns based on current HP and phase thresholds.
    /// </summary>
    public List<EnemyIntentPattern> GetActivePatterns()
    {
        if (Data.Phases.Count == 0)
            return Data.IntentPatterns;

        float hpFraction = (float)CurrentHp / MaxHp;
        // Find highest priority phase (lowest threshold that current HP is at or below)
        for (int i = Data.Phases.Count - 1; i >= 0; i--)
        {
            if (hpFraction <= Data.Phases[i].HpThreshold)
                return Data.Phases[i].IntentPatterns;
        }
        return Data.IntentPatterns;
    }

    /// <summary>
    /// Check if a phase transition just occurred; fire entry effect if so.
    /// Returns the phase entry description if a new phase was entered, else null.
    /// </summary>
    public string? CheckPhaseTransition()
    {
        if (Data.Phases.Count == 0) return null;

        float hpFraction = (float)CurrentHp / MaxHp;
        int newPhaseIndex = -1;
        for (int i = Data.Phases.Count - 1; i >= 0; i--)
        {
            if (hpFraction <= Data.Phases[i].HpThreshold)
            {
                newPhaseIndex = i;
                break;
            }
        }

        if (newPhaseIndex != _currentPhaseIndex)
        {
            _currentPhaseIndex = newPhaseIndex;
            return newPhaseIndex >= 0
                ? Data.Phases[newPhaseIndex].EntryEffect
                : null;
        }
        return null;
    }
}
