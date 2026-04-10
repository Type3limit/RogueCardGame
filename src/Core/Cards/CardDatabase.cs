using System.Text.Json;
using System.Text.Json.Serialization;

namespace RogueCardGame.Core.Cards;

/// <summary>
/// Loads and manages card definitions from JSON data files.
/// </summary>
public class CardDatabase
{
    private readonly Dictionary<string, CardData> _cards = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
        },
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public IReadOnlyDictionary<string, CardData> Cards => _cards;

    /// <summary>
    /// Load cards from a JSON string.
    /// </summary>
    public void LoadFromJson(string json)
    {
        var cards = JsonSerializer.Deserialize<List<CardData>>(json, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize card data");

        foreach (var card in cards)
        {
            _cards[card.Id] = card;
        }
    }

    /// <summary>
    /// Load cards from a file path.
    /// </summary>
    public void LoadFromFile(string path)
    {
        string json = File.ReadAllText(path);
        LoadFromJson(json);
    }

    /// <summary>
    /// Load all card JSON files from a directory.
    /// </summary>
    public void LoadFromDirectory(string directoryPath)
    {
        if (!Directory.Exists(directoryPath)) return;

        foreach (var file in Directory.GetFiles(directoryPath, "*.json"))
        {
            LoadFromFile(file);
        }
    }

    public CardData? GetCard(string id) =>
        _cards.GetValueOrDefault(id);

    public List<CardData> GetCardsByClass(CardClass cardClass) =>
        _cards.Values.Where(c => c.Class == cardClass).ToList();

    public List<CardData> GetCardsByRarity(CardRarity rarity) =>
        _cards.Values.Where(c => c.Rarity == rarity).ToList();

    public List<CardData> GetLinkCards() =>
        _cards.Values.Where(c => c.IsLink).ToList();

    /// <summary>
    /// Get the starter deck for a given class.
    /// </summary>
    public List<CardData> GetStarterDeck(CardClass cardClass) =>
        _cards.Values
            .Where(c => c.Class == cardClass && c.Rarity == CardRarity.Starter)
            .ToList();

    /// <summary>
    /// Create a runtime Card instance from a card ID.
    /// </summary>
    public Card? CreateCard(string id)
    {
        var data = GetCard(id);
        return data != null ? new Card(data) : null;
    }
}
