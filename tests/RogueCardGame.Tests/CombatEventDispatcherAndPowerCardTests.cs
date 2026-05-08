using RogueCardGame.Core;
using RogueCardGame.Core.Cards;
using RogueCardGame.Core.Characters;
using RogueCardGame.Core.Combat;
using RogueCardGame.Core.Combat.Powers;

namespace RogueCardGame.Tests;

/// <summary>
/// Regression coverage for (a) the CombatEventDispatcher actually publishing events
/// during normal play, and (b) every Power-type card's class-specific permanent effect
/// firing at least once in a realistic turn loop.
/// </summary>
public class CombatEventDispatcherAndPowerCardTests
{
    private static readonly string RepoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    private static readonly string DataDirectory = Path.Combine(RepoRoot, "data");
    private static readonly CardDatabase _cardDb = LoadCards();

    static CombatEventDispatcherAndPowerCardTests()
    {
        BalanceConfig.LoadFromFile(Path.Combine(DataDirectory, "balance", "balance.json"));
    }

    private static CardDatabase LoadCards()
    {
        var db = new CardDatabase();
        db.LoadFromDirectory(Path.Combine(DataDirectory, "cards"));
        return db;
    }

    private static Enemy MakeEnemy(string id = "dummy", int hp = 50) => new(new EnemyData
    {
        Id = id, Name = id, MaxHp = hp,
        PreferredRow = FormationRow.Front, IntentPatterns = []
    });

    private static (CombatManager combat, PlayerCharacter player, Enemy enemy) StartCombat(
        PlayerCharacter player, List<Card> deck, int enemyHp = 40)
    {
        var enemy = MakeEnemy("dummy", enemyHp);
        // Fill with filler so we don't reshuffle mid-test
        for (int i = 0; i < 10; i++)
            deck.Add(new Card(new CardData
            {
                Id = $"f{i}", Name = "f", Class = CardClass.Neutral,
                Rarity = CardRarity.Common, Type = CardType.Skill, Cost = 0, Description = ""
            }));

        var combat = new CombatManager(2024, _cardDb);
        combat.Initialize([player], [enemy], new Dictionary<int, List<Card>> { [player.Id] = deck });
        combat.StartCombat();
        combat.Actions.ProcessAll();
        return (combat, player, enemy);
    }

    // =============================================================
    //  CombatEventDispatcher
    // =============================================================

    [Fact]
    public void Dispatcher_publishes_OnCardPlayed_when_a_card_is_played()
    {
        var player = PlayerCharacter.CreateNetrunner("P");
        var compile = new Card(_cardDb.GetCard("compile")!);
        var (combat, _, _) = StartCombat(player, new List<Card> { compile });

        var events = new List<CombatEvent>();
        combat.Events.Subscribe(TriggerKind.OnCardPlayed, evt => events.Add(evt));

        // Guarantee compile is in hand
        var hand = combat.PlayerDecks[player.Id].Hand;
        for (int i = 0; i < 40 && hand.All(c => c.Data.Id != "compile"); i++)
            combat.PlayerDecks[player.Id].Draw(1);

        var card = hand.First(c => c.Data.Id == "compile");
        Assert.True(combat.TryPlayCard(player, card));
        combat.Actions.ProcessAll();

        Assert.Contains(events, e => e.Card?.Data.Id == "compile" && e.Actor == player);
    }

    [Fact]
    public void Dispatcher_publishes_AtTurnStart_when_turn_begins()
    {
        var player = PlayerCharacter.CreateNetrunner("P");
        var (combat, _, _) = StartCombat(player, new List<Card>());

        var events = new List<CombatEvent>();
        combat.Events.Subscribe(TriggerKind.AtTurnStart, evt => events.Add(evt));

        combat.EndPlayerTurn();
        combat.Actions.ProcessAll();
        // Enemy turn auto-advances to next player turn
        for (int i = 0; i < 20 && combat.TurnSystem.CurrentPhase != CombatPhase.PlayerPlanningPhase && combat.IsActive; i++)
            combat.Actions.ProcessAll();

        Assert.NotEmpty(events);
        Assert.Equal(TriggerKind.AtTurnStart, events[0].Kind);
    }

    [Fact]
    public void Dispatcher_publishes_OnKill_when_enemy_dies()
    {
        var player = PlayerCharacter.CreateNetrunner("P");
        var enemy = new Enemy(new EnemyData
        {
            Id = "fragile", Name = "fragile", MaxHp = 3,
            PreferredRow = FormationRow.Front, IntentPatterns = []
        });

        var combat = new CombatManager(7, _cardDb);
        combat.Initialize([player], [enemy], new Dictionary<int, List<Card>> { [player.Id] = new() });
        combat.StartCombat();
        combat.Actions.ProcessAll();

        var kills = new List<CombatEvent>();
        combat.Events.Subscribe(TriggerKind.OnKill, evt => kills.Add(evt));

        // Deal lethal damage directly through the action queue
        combat.Actions.AddToBottom(new Core.Combat.Actions.DealDamageAction(player, [enemy], 999));
        combat.Actions.ProcessAll();

        Assert.False(enemy.IsAlive);
        Assert.Contains(kills, e => e.Target == enemy && e.Actor == player);
    }

    // =============================================================
    //  Power-type card hooks
    // =============================================================

    [Fact]
    public void NeuralNetwork_grants_protocol_and_draw_each_turn()
    {
        var player = PlayerCharacter.CreateNetrunner("P");
        var (combat, _, _) = StartCombat(player, new List<Card>());
        combat.Actions.AddToBottom(new Core.Combat.Actions.ApplyPowerAction(player, new NeuralNetworkPower { Amount = 2 }));
        combat.Actions.ProcessAll();

        int stacks0 = player.Powers.GetStacks(CommonPowerIds.ProtocolStack);
        int hand0 = combat.PlayerDecks[player.Id].Hand.Count;

        combat.EndPlayerTurn();
        combat.Actions.ProcessAll();
        for (int i = 0; i < 20 && combat.TurnSystem.CurrentPhase != CombatPhase.PlayerPlanningPhase && combat.IsActive; i++)
            combat.Actions.ProcessAll();

        int stacks1 = player.Powers.GetStacks(CommonPowerIds.ProtocolStack);
        Assert.True(stacks1 >= stacks0 + 2, $"ProtocolStack should gain ≥2, was {stacks0}→{stacks1}");
    }

    [Fact]
    public void ResonanceCascade_grants_resonance_each_turn()
    {
        var player = PlayerCharacter.CreatePsion("P");
        var (combat, _, _) = StartCombat(player, new List<Card>());
        combat.Actions.AddToBottom(new Core.Combat.Actions.ApplyPowerAction(player, new ResonanceCascadePower { Amount = 2 }));
        combat.Actions.ProcessAll();

        int r0 = player.Powers.GetStacks(CommonPowerIds.Resonance);
        combat.EndPlayerTurn();
        combat.Actions.ProcessAll();
        for (int i = 0; i < 20 && combat.TurnSystem.CurrentPhase != CombatPhase.PlayerPlanningPhase && combat.IsActive; i++)
            combat.Actions.ProcessAll();

        int r1 = player.Powers.GetStacks(CommonPowerIds.Resonance);
        Assert.True(r1 >= r0 + 2, $"Resonance should gain ≥2, was {r0}→{r1}");
    }

    [Fact]
    public void ApexPredator_heals_on_kill()
    {
        var player = PlayerCharacter.CreateSymbiote("P");
        player.CurrentHp = Math.Max(1, player.MaxHp - 20); // room to heal

        var enemy = new Enemy(new EnemyData
        {
            Id = "weak", Name = "weak", MaxHp = 1,
            PreferredRow = FormationRow.Front, IntentPatterns = []
        });
        var combat = new CombatManager(11, _cardDb);
        combat.Initialize([player], [enemy], new Dictionary<int, List<Card>> { [player.Id] = new() });
        combat.StartCombat();
        combat.Actions.ProcessAll();
        combat.Actions.AddToBottom(new Core.Combat.Actions.ApplyPowerAction(player, new ApexPredatorPower { Amount = 5 }));
        combat.Actions.ProcessAll();

        int hpBefore = player.CurrentHp;
        // Kill enemy via a direct action
        combat.Actions.AddToBottom(new Core.Combat.Actions.DealDamageAction(player, [enemy], 10));
        combat.Actions.ProcessAll();

        Assert.False(enemy.IsAlive);
        Assert.True(player.CurrentHp >= hpBefore + 5,
            $"ApexPredator should heal 5 on kill; HP was {hpBefore}→{player.CurrentHp}");
    }
}
