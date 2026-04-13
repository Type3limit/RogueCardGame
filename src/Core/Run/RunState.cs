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

namespace RogueCardGame.Core.Run;

/// <summary>
/// Complete state of a single roguelike run from start to victory/death.
/// Owns all per-run data: player, deck, map, currencies, systems.
/// </summary>
public class RunState
{
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

    // State flags
    public bool IsRunActive { get; set; }
    public RunResult? Result { get; set; }

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

        // Build starter deck
        var starterCards = CardDb.GetStarterDeck(Player.Class);
        foreach (var cardData in starterCards)
        {
            int copies = cardData.Id.Contains("strike") || cardData.Id.Contains("defend") ? 4 : 1;
            for (int i = 0; i < copies; i++)
                MasterDeck.Add(new Card(cardData));
        }

        // Generate Act 1 map
        var mapGen = new MapGenerator(Random);
        CurrentMap = mapGen.Generate(1);
        CurrentAct = 1;
        IsRunActive = true;

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

        var playerDecks = new Dictionary<int, List<Card>>
        {
            [Player.Id] = combatDeck
        };

        combat.Initialize([Player], enemies, playerDecks);
        return combat;
    }

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
    }
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
