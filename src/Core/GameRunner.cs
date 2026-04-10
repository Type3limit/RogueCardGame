using RogueCardGame.Core.Cards;
using RogueCardGame.Core.Characters;
using RogueCardGame.Core.Combat;
using RogueCardGame.Core.Deck;

namespace RogueCardGame.Core;

/// <summary>
/// High-level game orchestrator that wires up all systems and runs
/// a complete single-player combat encounter for testing.
/// This will be replaced by Godot scene scripts in the UI layer.
/// </summary>
public class GameRunner
{
    private CombatManager? _combat;
    private CardDatabase _cardDb = new();

    public CombatManager? Combat => _combat;

    public void LoadData(string cardsDirectory)
    {
        _cardDb.LoadFromDirectory(cardsDirectory);
    }

    /// <summary>
    /// Start a test combat with Vanguard vs basic enemies.
    /// </summary>
    public void StartTestCombat(int seed = 42)
    {
        _combat = new CombatManager(seed);

        // Create player
        var vanguard = PlayerCharacter.CreateVanguard();

        // Create starter deck
        var starterCards = new List<Card>();
        var starterData = _cardDb.GetStarterDeck(CardClass.Vanguard);
        foreach (var data in starterData)
        {
            // Add multiple copies of basic cards
            int copies = data.Id.Contains("strike") || data.Id.Contains("defend") ? 4 : 1;
            for (int i = 0; i < copies; i++)
                starterCards.Add(new Card(data));
        }

        // If no cards loaded from JSON, create minimal test deck
        if (starterCards.Count == 0)
        {
            starterCards = CreateFallbackDeck();
        }

        // Create enemies
        var enemies = new List<Enemy>
        {
            new(new EnemyData
            {
                Id = "test_sentry",
                Name = "测试哨兵",
                MaxHp = 40,
                PreferredRow = FormationRow.Front,
                IntentPatterns =
                [
                    new() { Type = EnemyIntentType.Attack, Value = 8, Scope = TargetScope.SingleFront, Weight = 2 },
                    new() { Type = EnemyIntentType.Defend, Value = 6, Scope = TargetScope.Self, Weight = 1 }
                ]
            }),
            new(new EnemyData
            {
                Id = "test_drone",
                Name = "测试无人机",
                MaxHp = 24,
                PreferredRow = FormationRow.Back,
                IsHackable = true,
                HackThreshold = 8,
                IntentPatterns =
                [
                    new() { Type = EnemyIntentType.Attack, Value = 5, Scope = TargetScope.SingleAny, Weight = 2 },
                    new() { Type = EnemyIntentType.Buff, Value = 1, Scope = TargetScope.Self, Weight = 1 }
                ]
            })
        };

        var playerDecks = new Dictionary<int, List<Card>>
        {
            [vanguard.Id] = starterCards
        };

        _combat.Initialize([vanguard], enemies, playerDecks);
        _combat.StartCombat();
    }

    private static List<Card> CreateFallbackDeck()
    {
        var strike = new CardData
        {
            Id = "basic_strike",
            Name = "斩击",
            Class = CardClass.Neutral,
            Rarity = CardRarity.Starter,
            Type = CardType.Attack,
            Cost = 1,
            Range = CardRange.Melee,
            TargetType = TargetType.SingleEnemy,
            Description = "造成 6 点伤害。",
            Effects = [new CardEffectData { Type = "damage", Value = 6 }]
        };

        var defend = new CardData
        {
            Id = "basic_defend",
            Name = "防御",
            Class = CardClass.Neutral,
            Rarity = CardRarity.Starter,
            Type = CardType.Skill,
            Cost = 1,
            Range = CardRange.None,
            TargetType = TargetType.Self,
            Description = "获得 5 点护甲。",
            Effects = [new CardEffectData { Type = "block", Value = 5 }]
        };

        var deck = new List<Card>();
        for (int i = 0; i < 5; i++) deck.Add(new Card(strike));
        for (int i = 0; i < 4; i++) deck.Add(new Card(defend));
        return deck;
    }
}
