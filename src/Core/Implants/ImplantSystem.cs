using System.Text.Json;
using System.Text.Json.Serialization;
using RogueCardGame.Core.Cards;

namespace RogueCardGame.Core.Implants;

/// <summary>
/// Implant slot types - each player has one of each.
/// </summary>
public enum ImplantSlot
{
    Neural,   // Affects card draw, energy, hand mechanics
    Somatic,  // Affects body: HP, block, physical stats
    Core      // Fundamentally changes class mechanic
}

/// <summary>
/// Implant rarity.
/// </summary>
public enum ImplantRarity
{
    Common,
    Uncommon,
    Rare,
    Legendary
}

/// <summary>
/// Data definition for an implant.
/// </summary>
public sealed class ImplantData
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string NameEn { get; init; } = "";
    public required string Description { get; init; }
    public required ImplantSlot Slot { get; init; }
    public required ImplantRarity Rarity { get; init; }
    public CardClass? ClassRestriction { get; init; }
    public int CraftCost { get; init; } = 0; // Parts cost
    public List<ImplantEffectData> Effects { get; init; } = [];
    public string? ArtPath { get; init; }
    public string? Lore { get; init; }
}

/// <summary>
/// Individual effect within an implant.
/// </summary>
public sealed class ImplantEffectData
{
    public required string Type { get; init; } // e.g., "maxHp", "drawPerTurn", "energy", "strength", etc.
    public int Value { get; init; }
    public string? Condition { get; init; }
}

/// <summary>
/// Runtime implant instance equipped by a player.
/// </summary>
public class Implant
{
    public ImplantData Data { get; }
    public bool IsActive { get; set; } = true;

    public Implant(ImplantData data) => Data = data;
}

/// <summary>
/// Manages the 3 implant slots for a single player.
/// </summary>
public class ImplantManager
{
    private readonly Dictionary<ImplantSlot, Implant?> _slots = new()
    {
        [ImplantSlot.Neural] = null,
        [ImplantSlot.Core] = null,
    };

    public event Action<ImplantSlot, Implant>? OnImplantEquipped;
    public event Action<ImplantSlot, Implant>? OnImplantRemoved;

    public Implant? GetImplant(ImplantSlot slot) =>
        _slots.GetValueOrDefault(slot);

    public bool HasImplant(ImplantSlot slot) =>
        _slots.GetValueOrDefault(slot) != null;

    /// <summary>
    /// Equip an implant. Returns the previously equipped implant (if any).
    /// </summary>
    public Implant? Equip(ImplantData data)
    {
        var slot = data.Slot;
        var previous = _slots[slot];
        if (previous != null)
            OnImplantRemoved?.Invoke(slot, previous);

        var implant = new Implant(data);
        _slots[slot] = implant;
        OnImplantEquipped?.Invoke(slot, implant);
        return previous;
    }

    public void Remove(ImplantSlot slot)
    {
        var implant = _slots[slot];
        if (implant != null)
        {
            _slots[slot] = null;
            OnImplantRemoved?.Invoke(slot, implant);
        }
    }

    /// <summary>
    /// Calculate total bonus of a specific effect type across all equipped implants.
    /// </summary>
    public int GetTotalBonus(string effectType)
    {
        return _slots.Values
            .Where(i => i?.IsActive == true)
            .SelectMany(i => i!.Data.Effects)
            .Where(e => e.Type == effectType)
            .Sum(e => e.Value);
    }

    public List<Implant> GetAllEquipped() =>
        _slots.Values.Where(i => i != null).Cast<Implant>().ToList();
}

/// <summary>
/// Database of implant definitions.
/// </summary>
public class ImplantDatabase
{
    private readonly Dictionary<string, ImplantData> _implants = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public void LoadFromJson(string json)
    {
        var implants = JsonSerializer.Deserialize<List<ImplantData>>(json, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize implant data");
        foreach (var i in implants) _implants[i.Id] = i;
    }

    public void LoadFromFile(string path) => LoadFromJson(File.ReadAllText(path));

    public void LoadFromDirectory(string dir)
    {
        if (!Directory.Exists(dir)) return;
        foreach (var file in Directory.GetFiles(dir, "*.json"))
            LoadFromFile(file);
    }

    public ImplantData? GetImplant(string id) => _implants.GetValueOrDefault(id);

    public List<ImplantData> GetBySlot(ImplantSlot slot) =>
        _implants.Values.Where(i => i.Slot == slot).ToList();

    public List<ImplantData> GetForClass(CardClass cardClass) =>
        _implants.Values.Where(i => i.ClassRestriction == null || i.ClassRestriction == cardClass).ToList();
}
