using System.Text.Json;
using System.Text.Json.Serialization;
using RogueCardGame.Core.Deck;

namespace RogueCardGame.Core.Events;

/// <summary>
/// Choice type in a random event.
/// </summary>
public enum EventChoiceEffect
{
    GainGold,
    LoseGold,
    GainHp,
    LoseHp,
    GainMaxHp,
    LoseMaxHp,
    GainCard,
    RemoveCard,
    GainRelic,
    GainPotion,
    GainParts,
    LoseParts,
    UpgradeCard,
    GainImplant,
    StartCombat,
    Nothing
}

/// <summary>
/// A single choice within a random event.
/// </summary>
public sealed class EventChoice
{
    public required string Text { get; init; }
    public required List<EventChoiceOutcome> Outcomes { get; init; }
    public string? Condition { get; init; } // Optional condition to show this choice
}

/// <summary>
/// An outcome resulting from an event choice.
/// </summary>
public sealed class EventChoiceOutcome
{
    public required EventChoiceEffect Effect { get; init; }
    public int Value { get; init; }
    public string? TargetId { get; init; }
    public float Probability { get; init; } = 1.0f;
    public string? FlavorText { get; init; }
}

/// <summary>
/// Data definition for a random event.
/// </summary>
public sealed class EventData
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required List<EventChoice> Choices { get; init; }
    public int MinAct { get; init; } = 1;
    public int MaxAct { get; init; } = 99;
    public string? RequiredRelic { get; init; }
    public bool IsOneTime { get; init; } = false;
    public string? ArtPath { get; init; }
}

/// <summary>
/// Manages random event resolution.
/// </summary>
public class EventSystem
{
    private readonly SeededRandom _random;
    private readonly HashSet<string> _seenOneTimeEvents = [];

    public event Action<EventData>? OnEventStarted;
    public event Action<EventChoice, List<EventChoiceOutcome>>? OnChoiceMade;

    public EventSystem(SeededRandom random)
    {
        _random = random;
    }

    /// <summary>
    /// Select a random event for the current context.
    /// </summary>
    public EventData? SelectEvent(List<EventData> pool, int actNumber)
    {
        var available = pool
            .Where(e => actNumber >= e.MinAct && actNumber <= e.MaxAct)
            .Where(e => !e.IsOneTime || !_seenOneTimeEvents.Contains(e.Id))
            .ToList();

        if (available.Count == 0) return null;

        var selected = available[_random.Next(available.Count)];
        OnEventStarted?.Invoke(selected);
        return selected;
    }

    /// <summary>
    /// Resolve a choice and return the outcomes that occurred.
    /// </summary>
    public List<EventChoiceOutcome> ResolveChoice(EventData eventData, int choiceIndex)
    {
        if (choiceIndex < 0 || choiceIndex >= eventData.Choices.Count)
            return [];

        var choice = eventData.Choices[choiceIndex];
        var results = new List<EventChoiceOutcome>();

        foreach (var outcome in choice.Outcomes)
        {
            if (outcome.Probability >= 1.0f || _random.NextDouble() < outcome.Probability)
                results.Add(outcome);
        }

        if (eventData.IsOneTime)
            _seenOneTimeEvents.Add(eventData.Id);

        OnChoiceMade?.Invoke(choice, results);
        return results;
    }
}

/// <summary>
/// Database of event definitions.
/// </summary>
public class EventDatabase
{
    private readonly Dictionary<string, EventData> _events = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public void LoadFromJson(string json)
    {
        var events = JsonSerializer.Deserialize<List<EventData>>(json, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize event data");
        foreach (var e in events) _events[e.Id] = e;
    }

    public void LoadFromFile(string path) => LoadFromJson(File.ReadAllText(path));

    public void LoadFromDirectory(string dir)
    {
        if (!Directory.Exists(dir)) return;
        foreach (var file in Directory.GetFiles(dir, "*.json"))
            LoadFromFile(file);
    }

    public EventData? GetEvent(string id) => _events.GetValueOrDefault(id);
    public List<EventData> GetAll() => _events.Values.ToList();
}
