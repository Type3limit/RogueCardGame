using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace RogueCardGame.Core.Progression;

// ─────────────────────────────────────────────────────────────
// Meta-progression: Cross-run unlocks and statistics
// ─────────────────────────────────────────────────────────────

/// <summary>
/// Tracks cross-run progression including unlocks, statistics, and ascension levels.
/// Persisted to disk between game sessions.
/// </summary>
public class MetaProgress
{
    // ─── Unlocks ───
    public HashSet<string> UnlockedCards { get; set; } = new();
    public HashSet<string> UnlockedImplants { get; set; } = new();
    public HashSet<string> UnlockedClasses { get; set; } = new() { "vanguard" }; // Vanguard unlocked by default
    public HashSet<string> CompletedAchievements { get; set; } = new();

    // ─── Ascension (per class) ───
    public Dictionary<string, int> AscensionLevel { get; set; } = new();
    public int MaxAscension { get; } = 20;

    // ─── Statistics ───
    public PlayerStats Stats { get; set; } = new();

    // ─── Run history ───
    public List<RunRecord> RunHistory { get; set; } = new();
    public int MaxRunHistorySize { get; set; } = 100;

    // ─────── Unlock methods ───────

    public bool TryUnlockCard(string cardId)
    {
        return UnlockedCards.Add(cardId);
    }

    public bool TryUnlockClass(string classId)
    {
        return UnlockedClasses.Add(classId);
    }

    public bool TryUnlockImplant(string implantId)
    {
        return UnlockedImplants.Add(implantId);
    }

    public bool TryCompleteAchievement(string achievementId)
    {
        return CompletedAchievements.Add(achievementId);
    }

    // ─────── Ascension ───────

    public int GetAscension(string classId) =>
        AscensionLevel.GetValueOrDefault(classId, 0);

    public bool TryAdvanceAscension(string classId)
    {
        int current = GetAscension(classId);
        if (current >= MaxAscension) return false;
        AscensionLevel[classId] = current + 1;
        return true;
    }

    // ─────── Run recording ───────

    public void RecordRun(RunRecord record)
    {
        RunHistory.Add(record);
        if (RunHistory.Count > MaxRunHistorySize)
            RunHistory.RemoveAt(0);

        // Update stats
        Stats.TotalRuns++;
        if (record.Victory)
        {
            Stats.TotalWins++;
            Stats.WinsByClass[record.ClassName] =
                Stats.WinsByClass.GetValueOrDefault(record.ClassName, 0) + 1;
        }
        Stats.TotalEnemiesKilled += record.EnemiesKilled;
        Stats.TotalDamageDealt += record.DamageDealt;
        Stats.TotalGoldEarned += record.GoldEarned;
        Stats.HighestFloor = Math.Max(Stats.HighestFloor, record.FloorReached);
        Stats.TotalPlayTimeMinutes += record.PlayTimeMinutes;

        // Check unlock conditions
        CheckRunUnlocks(record);
    }

    // ─────── Unlock conditions ───────

    private void CheckRunUnlocks(RunRecord record)
    {
        // Unlock Psion after first win
        if (record.Victory && Stats.TotalWins >= 1)
            TryUnlockClass("psion");

        // Unlock Netrunner after winning with 2 different classes
        if (Stats.WinsByClass.Count >= 2)
            TryUnlockClass("netrunner");

        // Unlock Symbiote after reaching floor 30 (Act 3)
        if (record.FloorReached >= 30)
            TryUnlockClass("symbiote");

        // Unlock specific cards at milestones
        if (Stats.TotalRuns >= 5)
            TryUnlockCard("penetrating_shot");
        if (Stats.TotalRuns >= 10)
            TryUnlockCard("team_charge");
        if (Stats.TotalEnemiesKilled >= 100)
            TryUnlockCard("focus_fire");
    }

    // ─────── Serialization ───────

    public string Serialize()
    {
        return JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    }

    public static MetaProgress Deserialize(string json)
    {
        return JsonSerializer.Deserialize<MetaProgress>(json) ?? new MetaProgress();
    }
}

/// <summary>
/// Aggregate player statistics across all runs.
/// </summary>
public class PlayerStats
{
    public int TotalRuns { get; set; }
    public int TotalWins { get; set; }
    public Dictionary<string, int> WinsByClass { get; set; } = new();
    public int TotalEnemiesKilled { get; set; }
    public long TotalDamageDealt { get; set; }
    public long TotalGoldEarned { get; set; }
    public int HighestFloor { get; set; }
    public int TotalPlayTimeMinutes { get; set; }
    public int TotalCardsPlayed { get; set; }
    public int TotalImplantsEquipped { get; set; }
    public int FastestWinMinutes { get; set; } = int.MaxValue;

    public float WinRate => TotalRuns > 0 ? (float)TotalWins / TotalRuns * 100 : 0;
}

/// <summary>
/// Record of a single completed run.
/// </summary>
public class RunRecord
{
    public string RunId { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
    public string ClassName { get; set; } = "";
    public bool Victory { get; set; }
    public int FloorReached { get; set; }
    public int AscensionLevel { get; set; }
    public int PlayTimeMinutes { get; set; }
    public int EnemiesKilled { get; set; }
    public long DamageDealt { get; set; }
    public int GoldEarned { get; set; }
    public int CardsPlayed { get; set; }
    public List<string> ImplantsUsed { get; set; } = new();
    public string DeathCause { get; set; } = "";
    public bool IsMultiplayer { get; set; }
    public int PlayerCount { get; set; } = 1;
}

// ─────────────────────────────────────────────────────────────
// Ascension modifiers
// ─────────────────────────────────────────────────────────────

/// <summary>
/// Defines difficulty modifiers for each ascension level.
/// </summary>
public static class AscensionModifiers
{
    public static AscensionData GetModifiers(int level)
    {
        var data = new AscensionData { Level = level };

        // Cumulative modifiers
        if (level >= 1) data.EnemyHpMultiplier += 0.05f;       // +5% enemy HP
        if (level >= 2) data.EliteHpMultiplier += 0.10f;       // +10% elite HP
        if (level >= 3) data.LoseGoldOnRest = true;            // Resting costs 15 gold
        if (level >= 4) data.StartingHpPenalty = 5;            // Start with -5 HP
        if (level >= 5) data.HealingReduction = 0.25f;         // 25% less healing
        if (level >= 6) data.EnemyDamageBonus = 1;             // +1 enemy damage
        if (level >= 7) data.ShopPriceIncrease = 0.10f;        // +10% shop prices
        if (level >= 8) data.BossHpMultiplier += 0.15f;        // +15% boss HP
        if (level >= 9) data.ReducedPotionSlots = 1;           // -1 potion slot
        if (level >= 10) data.EnemyHpMultiplier += 0.10f;      // Additional +10% enemy HP
        if (level >= 11) data.StartWithCurse = true;           // Start with a curse card
        if (level >= 12) data.ReducedCardRewards = true;       // 1 less card choice at rewards
        if (level >= 13) data.EnemyDamageBonus += 1;           // Additional +1 enemy damage
        if (level >= 14) data.BossAdaptiveBoost = 0.10f;       // Boss AI adapts 10% faster
        if (level >= 15) data.EnvironmentHarshness += 1;       // Harsher environment effects
        if (level >= 16) data.StartingHpPenalty += 5;          // Additional -5 starting HP
        if (level >= 17) data.EliteCountIncrease = 1;          // +1 elite per act
        if (level >= 18) data.HealingReduction += 0.25f;       // Additional 25% less healing
        if (level >= 19) data.EnemyHpMultiplier += 0.15f;      // Additional +15% enemy HP
        if (level >= 20) data.HeartEnabled = true;             // Must fight the true final boss

        return data;
    }
}

public class AscensionData
{
    public int Level { get; set; }
    public float EnemyHpMultiplier { get; set; } = 1.0f;
    public float EliteHpMultiplier { get; set; } = 1.0f;
    public float BossHpMultiplier { get; set; } = 1.0f;
    public int EnemyDamageBonus { get; set; }
    public int StartingHpPenalty { get; set; }
    public float HealingReduction { get; set; }
    public float ShopPriceIncrease { get; set; }
    public bool LoseGoldOnRest { get; set; }
    public int ReducedPotionSlots { get; set; }
    public bool StartWithCurse { get; set; }
    public bool ReducedCardRewards { get; set; }
    public float BossAdaptiveBoost { get; set; }
    public int EnvironmentHarshness { get; set; }
    public int EliteCountIncrease { get; set; }
    public bool HeartEnabled { get; set; }
}
