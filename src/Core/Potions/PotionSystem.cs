using System.Text.Json;
using System.Text.Json.Serialization;

namespace RogueCardGame.Core.Potions;

/// <summary>
/// When a potion can be used.
/// </summary>
public enum PotionUseTime
{
    Combat,
    Map,
    Anytime
}

/// <summary>
/// Potion rarity.
/// </summary>
public enum PotionRarity
{
    Common,
    Uncommon,
    Rare
}

/// <summary>
/// Data definition for a potion type.
/// </summary>
public sealed class PotionData
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string NameEn { get; init; } = "";
    public required string Description { get; init; }
    public required PotionRarity Rarity { get; init; }
    public required PotionUseTime UseTime { get; init; }
    public required string EffectType { get; init; }
    public int Value { get; init; }
    public int SecondaryValue { get; init; }
    public string? StatusId { get; init; }
    public string? ArtPath { get; init; }
}

/// <summary>
/// Manages a player's potion inventory (max 3 slots by default).
/// </summary>
public class PotionManager
{
    private readonly PotionData?[] _slots;
    public int MaxSlots { get; }
    public IReadOnlyList<PotionData?> Slots => _slots;

    public event Action<int, PotionData>? OnPotionUsed;
    public event Action<int, PotionData>? OnPotionAcquired;
    public event Action<int>? OnPotionDiscarded;

    public PotionManager(int maxSlots = 3)
    {
        MaxSlots = maxSlots;
        _slots = new PotionData?[maxSlots];
    }

    public bool HasEmptySlot() => _slots.Any(s => s == null);

    public int? GetEmptySlot()
    {
        for (int i = 0; i < _slots.Length; i++)
            if (_slots[i] == null) return i;
        return null;
    }

    public bool TryAddPotion(PotionData potion)
    {
        int? slot = GetEmptySlot();
        if (slot == null) return false;
        _slots[slot.Value] = potion;
        OnPotionAcquired?.Invoke(slot.Value, potion);
        return true;
    }

    public PotionData? UsePotion(int slot)
    {
        if (slot < 0 || slot >= _slots.Length) return null;
        var potion = _slots[slot];
        if (potion == null) return null;
        _slots[slot] = null;
        OnPotionUsed?.Invoke(slot, potion);
        return potion;
    }

    public void DiscardPotion(int slot)
    {
        if (slot >= 0 && slot < _slots.Length)
        {
            _slots[slot] = null;
            OnPotionDiscarded?.Invoke(slot);
        }
    }
}

/// <summary>
/// Database of all potion definitions.
/// </summary>
public class PotionDatabase
{
    private readonly Dictionary<string, PotionData> _potions = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public void LoadFromJson(string json)
    {
        var potions = JsonSerializer.Deserialize<List<PotionData>>(json, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize potion data");
        foreach (var p in potions) _potions[p.Id] = p;
    }

    public void LoadFromFile(string path) => LoadFromJson(File.ReadAllText(path));

    public void LoadFromDirectory(string dir)
    {
        if (!Directory.Exists(dir)) return;
        foreach (var file in Directory.GetFiles(dir, "*.json"))
            LoadFromFile(file);
    }

    public PotionData? GetPotion(string id) => _potions.GetValueOrDefault(id);
    public List<PotionData> GetByRarity(PotionRarity rarity) =>
        _potions.Values.Where(p => p.Rarity == rarity).ToList();
    public List<PotionData> GetAll() => _potions.Values.ToList();
}
