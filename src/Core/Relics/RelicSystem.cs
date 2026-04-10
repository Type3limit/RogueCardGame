using System.Text.Json;
using System.Text.Json.Serialization;

namespace RogueCardGame.Core.Relics;

/// <summary>
/// When a relic triggers its effect.
/// </summary>
public enum RelicTrigger
{
    OnCombatStart,
    OnCombatEnd,
    OnTurnStart,
    OnTurnEnd,
    OnCardPlayed,
    OnDamageDealt,
    OnDamageTaken,
    OnHeal,
    OnBlockGained,
    OnStatusApplied,
    OnEnemyKilled,
    OnGoldGained,
    OnCardDrawn,
    OnFormationSwitch,
    OnHackComplete,
    OnLinkActivated,
    Passive,        // Always active
    OnPickup        // One-time when acquired
}

/// <summary>
/// Rarity tiers for relics.
/// </summary>
public enum RelicRarity
{
    Starter,
    Common,
    Uncommon,
    Rare,
    Boss,
    Shop,
    Event
}

/// <summary>
/// Data definition for a relic loaded from JSON.
/// </summary>
public sealed class RelicData
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string NameEn { get; init; } = "";
    public required string Description { get; init; }
    public required RelicRarity Rarity { get; init; }
    public required RelicTrigger Trigger { get; init; }
    public string? EffectType { get; init; }
    public int Value { get; init; }
    public int SecondaryValue { get; init; }
    public string? Condition { get; init; }
    public string? ArtPath { get; init; }
    public string? Lore { get; init; }
    public bool IsClassSpecific { get; init; }
    public string? ClassRestriction { get; init; }
}

/// <summary>
/// Runtime relic instance carried by a player.
/// </summary>
public class Relic
{
    public RelicData Data { get; }
    public bool IsActive { get; set; } = true;
    public int Counter { get; set; } // For relics that count events

    public Relic(RelicData data) => Data = data;

    /// <summary>
    /// Check if this relic should trigger given the current event.
    /// </summary>
    public bool ShouldTrigger(RelicTrigger trigger)
    {
        return IsActive && Data.Trigger == trigger;
    }
}

/// <summary>
/// Manages a player's collection of relics.
/// </summary>
public class RelicManager
{
    private readonly List<Relic> _relics = [];
    public IReadOnlyList<Relic> Relics => _relics;

    public event Action<Relic>? OnRelicAcquired;
    public event Action<Relic, RelicTrigger>? OnRelicTriggered;

    public void AddRelic(RelicData data)
    {
        var relic = new Relic(data);
        _relics.Add(relic);
        OnRelicAcquired?.Invoke(relic);

        if (data.Trigger == RelicTrigger.OnPickup)
            TriggerEffect(relic, RelicTrigger.OnPickup);
    }

    public bool HasRelic(string id) =>
        _relics.Any(r => r.Data.Id == id);

    /// <summary>
    /// Fire all relics matching the given trigger.
    /// Returns cumulative effect values.
    /// </summary>
    public List<(Relic relic, int value)> FireTrigger(RelicTrigger trigger)
    {
        var results = new List<(Relic, int)>();
        foreach (var relic in _relics.Where(r => r.ShouldTrigger(trigger)))
        {
            TriggerEffect(relic, trigger);
            results.Add((relic, relic.Data.Value));
        }
        return results;
    }

    /// <summary>
    /// Get total passive bonus of a specific effect type.
    /// </summary>
    public int GetPassiveBonus(string effectType)
    {
        return _relics
            .Where(r => r.IsActive && r.Data.Trigger == RelicTrigger.Passive
                && r.Data.EffectType == effectType)
            .Sum(r => r.Data.Value);
    }

    private void TriggerEffect(Relic relic, RelicTrigger trigger)
    {
        relic.Counter++;
        OnRelicTriggered?.Invoke(relic, trigger);
    }
}

/// <summary>
/// Database of all relic definitions.
/// </summary>
public class RelicDatabase
{
    private readonly Dictionary<string, RelicData> _relics = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public void LoadFromJson(string json)
    {
        var relics = JsonSerializer.Deserialize<List<RelicData>>(json, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize relic data");
        foreach (var relic in relics)
            _relics[relic.Id] = relic;
    }

    public void LoadFromFile(string path) => LoadFromJson(File.ReadAllText(path));

    public void LoadFromDirectory(string dir)
    {
        if (!Directory.Exists(dir)) return;
        foreach (var file in Directory.GetFiles(dir, "*.json"))
            LoadFromFile(file);
    }

    public RelicData? GetRelic(string id) => _relics.GetValueOrDefault(id);

    public List<RelicData> GetByRarity(RelicRarity rarity) =>
        _relics.Values.Where(r => r.Rarity == rarity).ToList();
}
