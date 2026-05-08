using System.Text.Json;
using System.Text.Json.Serialization;

namespace RogueCardGame.Core.Characters;

/// <summary>
/// Loads enemy definitions from JSON files and groups them by act / role.
/// </summary>
public class EnemyDatabase
{
    private readonly Dictionary<string, EnemyData> _enemies = new();
    private readonly Dictionary<int, List<string>> _normalByAct = new();
    private readonly Dictionary<int, List<string>> _eliteByAct = new();
    private readonly Dictionary<int, List<string>> _bossByAct = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public IReadOnlyDictionary<string, EnemyData> Enemies => _enemies;

    public void LoadFromJson(string json, int actNumber)
    {
        var enemies = JsonSerializer.Deserialize<List<EnemyData>>(json, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize enemy data");

        foreach (var enemy in enemies)
        {
            _enemies[enemy.Id] = enemy;
            Register(actNumber, enemy);
        }
    }

    public void LoadFromFile(string path)
    {
        int actNumber = ParseActNumber(Path.GetFileNameWithoutExtension(path));
        LoadFromJson(File.ReadAllText(path), actNumber);
    }

    public void LoadFromDirectory(string dir)
    {
        if (!Directory.Exists(dir)) return;

        foreach (var file in Directory.GetFiles(dir, "*.json"))
            LoadFromFile(file);
    }

    public EnemyData? GetEnemy(string id) => _enemies.GetValueOrDefault(id);

    public Enemy? CreateEnemy(string id, int playerCount = 1)
    {
        var data = GetEnemy(id);
        return data != null ? new Enemy(data, playerCount) : null;
    }

    public List<EnemyData> GetNormalPool(int actNumber) =>
        GetPool(_normalByAct, actNumber);

    public List<EnemyData> GetElitePool(int actNumber) =>
        GetPool(_eliteByAct, actNumber);

    public List<EnemyData> GetBossPool(int actNumber) =>
        GetPool(_bossByAct, actNumber);

    private void Register(int actNumber, EnemyData enemy)
    {
        if (enemy.IsBoss)
        {
            RegisterId(_bossByAct, actNumber, enemy.Id);
            return;
        }

        if (enemy.IsElite)
        {
            RegisterId(_eliteByAct, actNumber, enemy.Id);
            return;
        }

        RegisterId(_normalByAct, actNumber, enemy.Id);
    }

    private static void RegisterId(Dictionary<int, List<string>> buckets, int actNumber, string id)
    {
        if (!buckets.TryGetValue(actNumber, out var ids))
        {
            ids = [];
            buckets[actNumber] = ids;
        }

        if (!ids.Contains(id))
            ids.Add(id);
    }

    private List<EnemyData> GetPool(Dictionary<int, List<string>> buckets, int actNumber)
    {
        if (!buckets.TryGetValue(actNumber, out var ids))
            return [];

        return ids
            .Select(id => _enemies.GetValueOrDefault(id))
            .Where(enemy => enemy != null)
            .Cast<EnemyData>()
            .ToList();
    }

    private static int ParseActNumber(string fileName)
    {
        for (int i = 0; i < fileName.Length - 3; i++)
        {
            if (!fileName.AsSpan(i).StartsWith("act", StringComparison.OrdinalIgnoreCase))
                continue;

            int digitIndex = i + 3;
            if (digitIndex < fileName.Length && char.IsDigit(fileName[digitIndex]))
                return fileName[digitIndex] - '0';
        }

        return 1;
    }
}