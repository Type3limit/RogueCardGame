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
/// Data definition for an enemy type, loaded from JSON.
/// </summary>
public sealed class EnemyData
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required int MaxHp { get; init; }
    public required FormationRow PreferredRow { get; init; }
    public required List<EnemyIntentPattern> IntentPatterns { get; init; }
    public int MinHpScalePerPlayer { get; init; } = 0;
    public string? ArtPath { get; init; }
    public bool IsElite { get; init; }
    public bool IsBoss { get; init; }
    public bool IsHackable { get; init; } = true;
    public int HackThreshold { get; init; } = 10;
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
}

/// <summary>
/// Runtime enemy instance in combat.
/// </summary>
public class Enemy : Combatant
{
    public EnemyData Data { get; }
    public EnemyIntent? CurrentIntent { get; set; }
    public bool IsHacked { get; set; }
    public int HackedTurnsLeft { get; set; }

    public Enemy(EnemyData data, int playerCount = 1)
        : base(data.Name, data.MaxHp + data.MinHpScalePerPlayer * Math.Max(0, playerCount - 1))
    {
        Data = data;
    }

    /// <summary>
    /// Check if the enemy can be hacked (not a boss, hackable flag, etc.).
    /// </summary>
    public bool CanBeHacked()
    {
        return Data.IsHackable && !Data.IsBoss && !IsHacked;
    }

    /// <summary>
    /// Try to hack this enemy. Returns true if hack threshold reached.
    /// </summary>
    public bool TryHack()
    {
        if (!CanBeHacked()) return false;

        int progress = StatusEffects.GetStacks(StatusType.HackProgress);
        if (progress >= Data.HackThreshold)
        {
            StatusEffects.Remove(StatusType.HackProgress);
            IsHacked = true;
            HackedTurnsLeft = 2;
            return true;
        }
        return false;
    }

    public void TickHack()
    {
        if (!IsHacked) return;
        HackedTurnsLeft--;
        if (HackedTurnsLeft <= 0)
        {
            IsHacked = false;
            StatusEffects.Apply(StatusType.SystemChaos, 2, 3);
        }
    }
}
