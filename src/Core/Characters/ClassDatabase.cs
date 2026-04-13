using System.Text.Json;
using System.Text.Json.Serialization;
using RogueCardGame.Core.Cards;
using RogueCardGame.Core.Combat;

namespace RogueCardGame.Core.Characters;

/// <summary>
/// JSON-driven class definition. All class properties loaded from data/classes/*.json.
/// </summary>
public sealed class ClassDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string NameEn { get; init; } = "";
    public string Description { get; init; } = "";
    public int MaxHp { get; init; } = 75;
    public string StartRow { get; init; } = "front";
    public int BaseEnergy { get; init; } = 3;
    public int DrawPerTurn { get; init; } = 5;
    public ClassResourceDef? ClassResource { get; init; }
    public List<string> StarterDeckCardIds { get; init; } = [];
    public string UnlockCondition { get; init; } = "default";
    public string Color { get; init; } = "#FFFFFF";
    public string IconPath { get; init; } = "";

    [JsonIgnore]
    public FormationRow PreferredRow => StartRow.ToLowerInvariant() == "back"
        ? FormationRow.Back : FormationRow.Front;

    [JsonIgnore]
    public CardClass CardClass => Id.ToLowerInvariant() switch
    {
        "vanguard" => CardClass.Vanguard,
        "psion" => CardClass.Psion,
        "netrunner" => CardClass.Netrunner,
        "symbiote" => CardClass.Symbiote,
        _ => CardClass.Neutral
    };
}

public sealed class ClassResourceDef
{
    public string Id { get; init; } = "";
    public string StatusType { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string DisplayNameEn { get; init; } = "";
    public int StartValue { get; init; }
}

/// <summary>
/// Database of class definitions loaded from JSON.
/// </summary>
public class ClassDatabase
{
    private readonly Dictionary<string, ClassDefinition> _classes = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public IReadOnlyDictionary<string, ClassDefinition> Classes => _classes;

    public void LoadFromFile(string path)
    {
        string json = File.ReadAllText(path);
        var def = JsonSerializer.Deserialize<ClassDefinition>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Failed to deserialize class from {path}");
        _classes[def.Id] = def;
    }

    public void LoadFromDirectory(string dir)
    {
        if (!Directory.Exists(dir)) return;
        foreach (var file in Directory.GetFiles(dir, "*.json"))
            LoadFromFile(file);
    }

    public ClassDefinition? GetClass(string id) => _classes.GetValueOrDefault(id);

    public ClassDefinition? GetClassByEnum(CardClass cardClass)
    {
        string id = cardClass switch
        {
            CardClass.Vanguard => "vanguard",
            CardClass.Psion => "psion",
            CardClass.Netrunner => "netrunner",
            CardClass.Symbiote => "symbiote",
            _ => ""
        };
        return GetClass(id);
    }

    public List<ClassDefinition> GetAll() => _classes.Values.ToList();

    public List<ClassDefinition> GetUnlocked()
    {
        // For now return all; MetaProgress integration later
        return GetAll();
    }

    /// <summary>
    /// Create a PlayerCharacter from a class definition.
    /// </summary>
    public static PlayerCharacter CreateCharacter(ClassDefinition def)
    {
        var character = new PlayerCharacter(def.Name, def.CardClass, def.MaxHp);
        character.PreferredRow = def.PreferredRow;
        character.MaxEnergy = def.BaseEnergy;
        character.DrawPerTurn = def.DrawPerTurn;
        if (def.ClassResource != null)
            character.ClassResourceName = def.ClassResource.DisplayName;
        return character;
    }
}
