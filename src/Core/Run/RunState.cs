using RogueCardGame.Core;
using RogueCardGame.Core.Cards;
using RogueCardGame.Core.Characters;
using RogueCardGame.Core.Combat;
using RogueCardGame.Core.Deck;
using RogueCardGame.Core.Environment;
using RogueCardGame.Core.Events;
using RogueCardGame.Core.Implants;
using RogueCardGame.Core.Map;
using RogueCardGame.Core.Potions;
using RogueCardGame.Core.Shop;
using System.Text.Json;

namespace RogueCardGame.Core.Run;

/// <summary>
/// Complete state of a single roguelike run from start to victory/death.
/// Owns all per-run data: player, deck, map, currencies, systems.
/// </summary>
public class RunState
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    // Identity
    public int Seed { get; }
    public SeededRandom Random { get; }

    // Player
    public PlayerCharacter Player { get; }
    public List<Card> MasterDeck { get; } = [];
    public int Gold { get; set; } = 99;

    // Per-run systems
    public PotionManager Potions { get; } = new();
    public ImplantManager Implants { get; } = new();

    // Map
    public ActMap? CurrentMap { get; set; }
    public int CurrentAct { get; set; } = 1;
    public int FloorsCleared { get; set; }

    // Databases
    public CardDatabase CardDb { get; } = new();
    public PotionDatabase PotionDb { get; } = new();
    public ImplantDatabase ImplantDb { get; } = new();
    public EventDatabase EventDb { get; } = new();
    public ClassDatabase ClassDb { get; } = new();
    public EnemyDatabase EnemyDb { get; } = new();

    // State flags
    public bool IsRunActive { get; set; }
    public RunResult? Result { get; set; }
    public HashSet<string> SeenOneTimeEvents { get; } = [];
    public string CurrentSceneId { get; set; } = string.Empty;

    // Events
    public event Action<RunState>? OnRunStarted;
    public event Action<RunResult>? OnRunEnded;
    public event Action<int>? OnActChanged;
    public event Action<int>? OnGoldChanged;

    public RunState(int seed, CardClass playerClass, string? dataDirectory = null)
    {
        Seed = seed;
        Random = new SeededRandom(seed);

        // Load class definitions if dataDirectory provided
        if (dataDirectory != null)
        {
            string classesDir = Path.Combine(dataDirectory, "classes");
            if (Directory.Exists(classesDir))
                ClassDb.LoadFromDirectory(classesDir);
        }

        // Use ClassDatabase to create character (falls back to basic constructor)
        var classDef = ClassDb.GetClassByEnum(playerClass);
        Player = classDef != null
            ? ClassDatabase.CreateCharacter(classDef)
            : new PlayerCharacter(playerClass.ToString(), playerClass, 75);

        Gold = BalanceConfig.Current.GlobalBalance.StartingGold;
    }

    /// <summary>
    /// Initialize and start a new run.
    /// </summary>
    public void StartRun(string dataDirectory)
    {
        // Load all data
        LoadData(dataDirectory);

        // Build starter deck from class definition's starterDeckCardIds
        var classDef = ClassDb.GetClassByEnum(Player.Class);
        var starterIds = classDef?.StarterDeckCardIds ?? [];
        foreach (var cardId in starterIds)
        {
            var cardData = CardDb.GetCard(cardId);
            if (cardData != null)
                MasterDeck.Add(new Card(cardData));
        }

        // Generate Act 1 map
        var mapGen = new MapGenerator(Random);
        CurrentMap = mapGen.Generate(1);
        CurrentAct = 1;
        IsRunActive = true;
        CurrentSceneId = "map";

        OnRunStarted?.Invoke(this);
    }

    /// <summary>
    /// Add gold with event notification.
    /// </summary>
    public void AddGold(int amount)
    {
        Gold += amount;
        OnGoldChanged?.Invoke(Gold);
    }

    /// <summary>
    /// Spend gold. Returns false if insufficient.
    /// </summary>
    public bool TrySpendGold(int amount)
    {
        if (Gold < amount) return false;
        Gold -= amount;
        OnGoldChanged?.Invoke(Gold);
        return true;
    }

    /// <summary>
    /// Add a card to the master deck.
    /// </summary>
    public void AddCardToDeck(CardData cardData)
    {
        MasterDeck.Add(new Card(cardData));
    }

    /// <summary>
    /// Remove a card from the master deck.
    /// </summary>
    public bool RemoveCardFromDeck(Card card)
    {
        return MasterDeck.Remove(card);
    }

    // Prototype Core system — auto-activates after observing early play patterns
    public PrototypeCoreSystem PrototypeCore { get; } = new();

    // Environment system — holds room-specific battlefield modifiers
    public EnvironmentSystem? Environment { get; private set; }

    /// <summary>
    /// Create a combat encounter from the current run state.
    /// </summary>
    public CombatManager CreateCombat(List<Characters.Enemy> enemies)
    {
        var combat = new CombatManager(Random.Next(int.MaxValue), CardDb);

        // Create deck copies for combat
        var combatDeck = MasterDeck.Select(c => c.Clone()).ToList();

        // Apply implant bonuses
        var classDef = ClassDb.GetClassByEnum(Player.Class);
        int baseEnergy = classDef?.BaseEnergy ?? BalanceConfig.Current.GlobalBalance.BaseEnergyPerTurn;
        int baseDraw = classDef?.DrawPerTurn ?? BalanceConfig.Current.GlobalBalance.BaseDrawPerTurn;
        Player.MaxEnergy = baseEnergy + Implants.GetTotalBonus("energy");
        Player.DrawPerTurn = baseDraw + Implants.GetTotalBonus("drawPerTurn");
        Player.MaxHp = Player.MaxHp + Implants.GetTotalBonus("maxHp");

        // Generate environment modifiers for this room
        Environment = new EnvironmentSystem(Random);
        Environment.GenerateForRoom(CurrentAct);

        var playerDecks = new Dictionary<int, List<Card>>
        {
            [Player.Id] = combatDeck
        };

        combat.Initialize([Player], enemies, playerDecks, Environment, PrototypeCore, Implants);
        return combat;
    }

    /// <summary>
    /// Create enemies for the current map node from the enemy database.
    /// </summary>
    public List<Enemy> CreateEnemiesForCurrentNode()
    {
        var node = CurrentMap?.CurrentNode;
        if (node == null)
            return [CreateFallbackEnemy()];

        return node.Type switch
        {
            RoomType.Boss => CreateBossEncounter(CurrentAct),
            RoomType.EliteCombat => CreateEliteEncounter(CurrentAct),
            RoomType.Combat => CreateNormalEncounter(CurrentAct),
            _ => [CreateFallbackEnemy()]
        };
    }

    public Enemy? CreateEnemyById(string id) => EnemyDb.CreateEnemy(id);

    /// <summary>
    /// Handle post-combat rewards.
    /// </summary>
    public void OnCombatVictory(bool wasElite, bool wasBoss)
    {
        FloorsCleared++;

        var gb = BalanceConfig.Current.GlobalBalance;
        int goldReward = wasBoss ? gb.BossGoldReward
            : wasElite ? gb.EliteGoldReward
            : Random.Next(gb.NormalGoldRewardMin, gb.NormalGoldRewardMax);
        AddGold(goldReward);

        // Potion drop chance
        if (Random.NextDouble() < gb.PotionDropChance)
        {
            // Award random potion (handled by reward screen)
        }
    }

    /// <summary>
    /// Progress to the next act.
    /// </summary>
    public void AdvanceAct()
    {
        CurrentAct++;
        if (CurrentAct > BalanceConfig.Current.MapGeneration.ActsTotal)
        {
            EndRun(true);
            return;
        }

        var mapGen = new MapGenerator(Random);
        CurrentMap = mapGen.Generate(CurrentAct);
        OnActChanged?.Invoke(CurrentAct);
    }

    /// <summary>
    /// End the run.
    /// </summary>
    public void EndRun(bool victory)
    {
        IsRunActive = false;
        CurrentSceneId = victory ? "victory" : "gameover";
        Result = new RunResult
        {
            Victory = victory,
            FloorsCleared = FloorsCleared,
            Act = CurrentAct,
            PlayerClass = Player.Class,
            Seed = Seed,
            Gold = Gold,
            CardsInDeck = MasterDeck.Count
        };
        OnRunEnded?.Invoke(Result);
    }

    /// <summary>
    /// Rest at a rest site: heal 30% of max HP.
    /// </summary>
    public void Rest()
    {
        int healPercent = BalanceConfig.Current.GlobalBalance.RestHealPercent;
        int healAmount = (int)(Player.MaxHp * healPercent / 100f);
        Player.Heal(healAmount);
    }

    private void LoadData(string dataDirectory)
    {
        string Resolve(string sub) => Path.Combine(dataDirectory, sub);

        if (Directory.Exists(Resolve("cards")))
            CardDb.LoadFromDirectory(Resolve("cards"));
        if (Directory.Exists(Resolve("potions")))
            PotionDb.LoadFromDirectory(Resolve("potions"));
        if (Directory.Exists(Resolve("implants")))
            ImplantDb.LoadFromDirectory(Resolve("implants"));
        if (Directory.Exists(Resolve("events")))
            EventDb.LoadFromDirectory(Resolve("events"));
        if (Directory.Exists(Resolve("enemies")))
            EnemyDb.LoadFromDirectory(Resolve("enemies"));
    }

    private List<Enemy> CreateNormalEncounter(int act)
    {
        var pool = EnemyDb.GetNormalPool(act);
        if (pool.Count == 0)
            return [CreateFallbackEnemy()];

        var shuffled = pool.ToList();
        Random.Shuffle(shuffled);
        int maxCount = Math.Min(3, shuffled.Count);
        int encounterCount = maxCount == 1 ? 1 : Random.Next(2, maxCount + 1);
        return shuffled.Take(encounterCount).Select(data => new Enemy(data)).ToList();
    }

    private List<Enemy> CreateEliteEncounter(int act)
    {
        var pool = EnemyDb.GetElitePool(act);
        if (pool.Count == 0)
            return CreateNormalEncounter(act);

        var selected = pool[Random.Next(pool.Count)];
        return [new Enemy(selected)];
    }

    private List<Enemy> CreateBossEncounter(int act)
    {
        var pool = EnemyDb.GetBossPool(act);
        if (pool.Count == 0)
            return [CreateFallbackEnemy()];

        var selected = pool[Random.Next(pool.Count)];
        return [new Enemy(selected)];
    }

    private static Enemy CreateFallbackEnemy()
    {
        return new Enemy(new EnemyData
        {
            Id = "fallback_enemy",
            Name = "测试敌人",
            MaxHp = 30,
            PreferredRow = FormationRow.Front,
            IntentPatterns =
            [
                new EnemyIntentPattern { Type = EnemyIntentType.Attack, Value = 8 },
                new EnemyIntentPattern { Type = EnemyIntentType.Defend, Value = 5, Scope = TargetScope.Self }
            ]
        });
    }

    public string Serialize()
    {
        var data = new RunStateSaveData
        {
            Seed = Seed,
            PlayerClass = Player.Class,
            PlayerName = Player.Name,
            PlayerMaxHp = Player.MaxHp,
            PlayerCurrentHp = Player.CurrentHp,
            PlayerPreferredRow = Player.PreferredRow,
            PlayerMaxEnergy = Player.MaxEnergy,
            PlayerCurrentEnergy = Player.CurrentEnergy,
            PlayerDrawPerTurn = Player.DrawPerTurn,
            PlayerClassResourceName = Player.ClassResourceName,
            Gold = Gold,
            CurrentAct = CurrentAct,
            FloorsCleared = FloorsCleared,
            IsRunActive = IsRunActive,
            CurrentSceneId = CurrentSceneId,
            SeenOneTimeEvents = SeenOneTimeEvents.ToList(),
            PrototypeIdentity = PrototypeCore.Identity,
            PrototypeActivated = PrototypeCore.IsActivated,
            MasterDeck = MasterDeck.Select(card => new SavedCardState
            {
                CardId = card.Data.Id,
                IsUpgraded = card.IsUpgraded,
                Branch = card.Branch,
                CurrentCost = card.CurrentCost,
                TempCostModifier = card.TempCostModifier
            }).ToList(),
            EquippedImplants = Implants.GetAllEquipped().Select(implant => implant.Data.Id).ToList(),
            PotionSlots = Potions.Slots.Select(potion => potion?.Id).ToList(),
            Map = SerializeMap(CurrentMap),
            Result = Result == null ? null : new SavedRunResult
            {
                Victory = Result.Victory,
                FloorsCleared = Result.FloorsCleared,
                Act = Result.Act,
                PlayerClass = Result.PlayerClass,
                Seed = Result.Seed,
                Gold = Result.Gold,
                CardsInDeck = Result.CardsInDeck
            }
        };

        return JsonSerializer.Serialize(data, JsonOptions);
    }

    public static RunState Deserialize(string json, string? dataDirectory = null)
    {
        var data = JsonSerializer.Deserialize<RunStateSaveData>(json, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize run state save data.");

        var run = new RunState(data.Seed, data.PlayerClass, dataDirectory);
        run.LoadDataIfNeeded(dataDirectory);
        run.RestoreFromSaveData(data);
        return run;
    }

    private static SavedActMap? SerializeMap(ActMap? map)
    {
        if (map == null) return null;

        return new SavedActMap
        {
            ActNumber = map.ActNumber,
            TotalRows = map.TotalRows,
            CurrentNodeId = map.CurrentNode?.Id,
            Nodes = map.Nodes.Select(node => new SavedMapNode
            {
                Id = node.Id,
                Row = node.Row,
                Column = node.Column,
                Type = node.Type,
                ConnectedTo = [.. node.ConnectedTo],
                IsVisited = node.IsVisited,
                IsRevealed = node.IsRevealed,
                EncounterId = node.EncounterId,
                EventId = node.EventId
            }).ToList()
        };
    }

    private static ActMap? DeserializeMap(SavedActMap? data)
    {
        if (data == null) return null;

        var map = new ActMap(data.ActNumber, data.TotalRows);
        foreach (var savedNode in data.Nodes)
        {
            var node = new MapNode(savedNode.Id, savedNode.Row, savedNode.Column, savedNode.Type)
            {
                IsVisited = savedNode.IsVisited,
                IsRevealed = savedNode.IsRevealed,
                EncounterId = savedNode.EncounterId,
                EventId = savedNode.EventId
            };
            node.ConnectedTo.AddRange(savedNode.ConnectedTo);
            map.Nodes.Add(node);
        }

        if (data.CurrentNodeId.HasValue)
            map.CurrentNode = map.GetNode(data.CurrentNodeId.Value);

        return map;
    }

    private void LoadDataIfNeeded(string? dataDirectory)
    {
        if (string.IsNullOrWhiteSpace(dataDirectory))
            return;

        LoadData(dataDirectory);
    }

    private void RestoreFromSaveData(RunStateSaveData data)
    {
        Player.Name = data.PlayerName;
        Player.MaxHp = data.PlayerMaxHp;
        Player.CurrentHp = Math.Min(data.PlayerCurrentHp, data.PlayerMaxHp);
        Player.PreferredRow = data.PlayerPreferredRow;
        Player.MaxEnergy = data.PlayerMaxEnergy;
        Player.CurrentEnergy = data.PlayerCurrentEnergy;
        Player.DrawPerTurn = data.PlayerDrawPerTurn;
        Player.ClassResourceName = data.PlayerClassResourceName ?? string.Empty;

        Gold = data.Gold;
        CurrentAct = data.CurrentAct;
        FloorsCleared = data.FloorsCleared;
        IsRunActive = data.IsRunActive;
        CurrentSceneId = data.CurrentSceneId ?? string.Empty;

        SeenOneTimeEvents.Clear();
        foreach (var eventId in data.SeenOneTimeEvents)
            SeenOneTimeEvents.Add(eventId);

        MasterDeck.Clear();
        foreach (var savedCard in data.MasterDeck)
        {
            var cardData = CardDb.GetCard(savedCard.CardId);
            if (cardData == null)
                continue;

            var card = new Card(cardData)
            {
                CurrentCost = savedCard.CurrentCost,
                TempCostModifier = savedCard.TempCostModifier
            };

            if (savedCard.IsUpgraded)
                card.Upgrade(savedCard.Branch);

            card.CurrentCost = savedCard.CurrentCost;
            card.TempCostModifier = savedCard.TempCostModifier;
            MasterDeck.Add(card);
        }

        foreach (var slot in Enum.GetValues<ImplantSlot>())
            Implants.Remove(slot);
        foreach (var implantId in data.EquippedImplants)
        {
            var implant = ImplantDb.GetImplant(implantId);
            if (implant != null)
                Implants.Equip(implant);
        }

        for (int i = 0; i < Potions.MaxSlots; i++)
            Potions.DiscardPotion(i);
        foreach (var potionId in data.PotionSlots.Where(id => !string.IsNullOrWhiteSpace(id)))
        {
            var potion = PotionDb.GetPotion(potionId!);
            if (potion != null)
                Potions.TryAddPotion(potion);
        }

        CurrentMap = DeserializeMap(data.Map);

        Result = data.Result == null ? null : new RunResult
        {
            Victory = data.Result.Victory,
            FloorsCleared = data.Result.FloorsCleared,
            Act = data.Result.Act,
            PlayerClass = data.Result.PlayerClass,
            Seed = data.Result.Seed,
            Gold = data.Result.Gold,
            CardsInDeck = data.Result.CardsInDeck
        };
    }
}

public sealed class RunStateSaveData
{
    public int Seed { get; set; }
    public CardClass PlayerClass { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public int PlayerMaxHp { get; set; }
    public int PlayerCurrentHp { get; set; }
    public FormationRow PlayerPreferredRow { get; set; }
    public int PlayerMaxEnergy { get; set; }
    public int PlayerCurrentEnergy { get; set; }
    public int PlayerDrawPerTurn { get; set; }
    public string? PlayerClassResourceName { get; set; }
    public int Gold { get; set; }
    public int CurrentAct { get; set; }
    public int FloorsCleared { get; set; }
    public bool IsRunActive { get; set; }
    public string CurrentSceneId { get; set; } = string.Empty;
    public List<string> SeenOneTimeEvents { get; set; } = [];
    public PrototypeIdentity PrototypeIdentity { get; set; }
    public bool PrototypeActivated { get; set; }
    public List<SavedCardState> MasterDeck { get; set; } = [];
    public List<string> EquippedImplants { get; set; } = [];
    public List<string?> PotionSlots { get; set; } = [];
    public SavedActMap? Map { get; set; }
    public SavedRunResult? Result { get; set; }
}

public sealed class SavedCardState
{
    public string CardId { get; set; } = string.Empty;
    public bool IsUpgraded { get; set; }
    public UpgradeBranch Branch { get; set; }
    public int CurrentCost { get; set; }
    public int TempCostModifier { get; set; }
}

public sealed class SavedActMap
{
    public int ActNumber { get; set; }
    public int TotalRows { get; set; }
    public int? CurrentNodeId { get; set; }
    public List<SavedMapNode> Nodes { get; set; } = [];
}

public sealed class SavedMapNode
{
    public int Id { get; set; }
    public int Row { get; set; }
    public int Column { get; set; }
    public RoomType Type { get; set; }
    public List<int> ConnectedTo { get; set; } = [];
    public bool IsVisited { get; set; }
    public bool IsRevealed { get; set; }
    public string? EncounterId { get; set; }
    public string? EventId { get; set; }
}

public sealed class SavedRunResult
{
    public bool Victory { get; set; }
    public int FloorsCleared { get; set; }
    public int Act { get; set; }
    public CardClass PlayerClass { get; set; }
    public int Seed { get; set; }
    public int Gold { get; set; }
    public int CardsInDeck { get; set; }
}

/// <summary>
/// Summary of a completed run.
/// </summary>
public class RunResult
{
    public bool Victory { get; init; }
    public int FloorsCleared { get; init; }
    public int Act { get; init; }
    public CardClass PlayerClass { get; init; }
    public int Seed { get; init; }
    public int Gold { get; init; }
    public int CardsInDeck { get; init; }
}
