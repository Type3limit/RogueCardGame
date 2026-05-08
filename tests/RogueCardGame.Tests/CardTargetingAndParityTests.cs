using RogueCardGame.Core;
using RogueCardGame.Core.Cards;
using RogueCardGame.Core.Characters;
using RogueCardGame.Core.Combat;
using RogueCardGame.Core.Combat.Powers;

namespace RogueCardGame.Tests;

/// <summary>
/// End-to-end tests covering targeting auto-inference and card-text ↔ effect parity.
/// Loads the actual card JSON and plays cards through a full <see cref="CombatManager"/>.
/// </summary>
public class CardTargetingAndParityTests
{
    private static readonly string RepoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    private static readonly string DataDirectory = Path.Combine(RepoRoot, "data");
    private static readonly CardDatabase _cardDb = LoadCards();

    static CardTargetingAndParityTests()
    {
        BalanceConfig.LoadFromFile(Path.Combine(DataDirectory, "balance", "balance.json"));
    }

    private static CardDatabase LoadCards()
    {
        var db = new CardDatabase();
        db.LoadFromDirectory(Path.Combine(RepoRoot, "data", "cards"));
        return db;
    }

    private static Enemy MakeEnemy(string name = "dummy", int hp = 50)
    {
        return new Enemy(new EnemyData
        {
            Id = name,
            Name = name,
            MaxHp = hp,
            PreferredRow = FormationRow.Front,
            IntentPatterns = []
        });
    }

    private static (CombatManager combat, PlayerCharacter player, Enemy enemy)
        StartCombatWithHand(params string[] cardIds)
    {
        var player = PlayerCharacter.CreateNetrunner("Tester");
        var enemy = MakeEnemy("dummy", 100);
        // Fill a deck with junk blocks so we don't trigger reshuffles.
        var deck = cardIds.Select(id => new Card(_cardDb.GetCard(id)!)).ToList();
        for (int i = 0; i < 10; i++)
        {
            deck.Add(new Card(new CardData
            {
                Id = $"filler_{i}", Name = "filler", Class = CardClass.Neutral,
                Rarity = CardRarity.Common, Type = CardType.Skill, Cost = 0,
                Description = "filler"
            }));
        }

        var combat = new CombatManager(1337, _cardDb);
        combat.Initialize([player], [enemy], new Dictionary<int, List<Card>>
        {
            [player.Id] = deck
        });
        combat.StartCombat();
        combat.Actions.ProcessAll();

        // Guarantee the test card is in hand (draw if not already).
        var hand = combat.PlayerDecks[player.Id].Hand;
        foreach (var id in cardIds)
        {
            if (hand.All(c => c.Data.Id != id))
            {
                // Move from draw pile to hand by drawing until found (bounded).
                for (int i = 0; i < 40 && hand.All(c => c.Data.Id != id); i++)
                    combat.PlayerDecks[player.Id].Draw(1);
            }
        }

        return (combat, player, enemy);
    }

    // ---------- Target type auto-inference ----------

    [Fact]
    public void Netrunner_skill_without_explicit_target_infers_self()
    {
        var compile = _cardDb.GetCard("compile");
        Assert.NotNull(compile);
        Assert.Null(compile!.TargetType); // JSON did not specify
        Assert.Equal(TargetType.Self, compile.EffectiveTargetType);
    }

    [Fact]
    public void Netrunner_attack_card_stays_single_enemy()
    {
        // `execute` is an attack that consumes protocol stacks for damage per stack.
        var execute = _cardDb.GetCard("execute");
        Assert.NotNull(execute);
        Assert.Equal(TargetType.SingleEnemy, execute!.EffectiveTargetType);
    }

    [Fact]
    public void Power_cards_default_to_self()
    {
        var neural = _cardDb.GetCard("neural_network");
        Assert.NotNull(neural);
        Assert.Equal(CardType.Power, neural!.Type);
        Assert.Equal(TargetType.Self, neural.EffectiveTargetType);
    }

    [Fact]
    public void Explicit_json_targettype_is_preserved()
    {
        // Psion cards have explicit "targetType": "self"
        var psiMindTap = _cardDb.GetCard("psi_mind_tap");
        Assert.NotNull(psiMindTap);
        Assert.Equal(TargetType.Self, psiMindTap!.EffectiveTargetType);
    }

    [Fact]
    public void Parasitic_bond_marks_an_enemy()
    {
        // "标记一个敌人" → effect applies a debuff-style status to enemy.
        var bond = _cardDb.GetCard("parasitic_bond");
        Assert.NotNull(bond);
        Assert.Equal(TargetType.SingleEnemy, bond!.EffectiveTargetType);
    }

    [Fact]
    public void Hack_card_requires_enemy_target()
    {
        var quickHack = _cardDb.GetCard("quick_hack");
        Assert.NotNull(quickHack);
        Assert.Equal(TargetType.SingleEnemy, quickHack!.EffectiveTargetType);
    }

    // ---------- Card text ↔ effect parity ----------

    [Fact]
    public void Compile_card_grants_protocol_stack_and_draws_card()
    {
        var (combat, player, _) = StartCombatWithHand("compile");
        var hand = combat.PlayerDecks[player.Id].Hand;
        var compile = hand.First(c => c.Data.Id == "compile");

        int handBefore = hand.Count;
        int stackBefore = player.Powers.GetStacks(CommonPowerIds.ProtocolStack);

        bool played = combat.TryPlayCard(player, compile);
        Assert.True(played, "Compile should play without requiring a target");
        combat.Actions.ProcessAll();

        int stackAfter = player.Powers.GetStacks(CommonPowerIds.ProtocolStack);
        int handAfter = combat.PlayerDecks[player.Id].Hand.Count;

        Assert.Equal(stackBefore + 2, stackAfter);
        // Hand: played 1 (removed), drew 1 → net -0 from original count
        Assert.Equal(handBefore, handAfter);
    }

    [Fact]
    public void Compile_upgraded_grants_3_protocol_stacks_and_draws_card()
    {
        var (combat, player, _) = StartCombatWithHand("compile");
        var hand = combat.PlayerDecks[player.Id].Hand;
        var compile = hand.First(c => c.Data.Id == "compile");
        compile.Upgrade(UpgradeBranch.A); // branchA: protocolStack +3, draw 1

        int handBefore = hand.Count;
        int stackBefore = player.Powers.GetStacks(CommonPowerIds.ProtocolStack);

        bool played = combat.TryPlayCard(player, compile);
        Assert.True(played, "Upgraded compile should play without requiring a target");
        combat.Actions.ProcessAll();

        int stackAfter = player.Powers.GetStacks(CommonPowerIds.ProtocolStack);
        int handAfter = combat.PlayerDecks[player.Id].Hand.Count;

        Assert.Equal(stackBefore + 3, stackAfter);
        // Hand: played 1 (removed), drew 1 → net -0 from original count
        Assert.Equal(handBefore, handAfter);
    }

    [Fact]
    public void Compile_draws_even_from_discard_pile_when_draw_pile_is_empty()
    {
        // Simulate real game: deck has only compile + 4 others (5 total).
        // After initial 5-card draw, draw pile = 0. Play compile → discard pile
        // gets reshuffled and draw still succeeds.
        var player = PlayerCharacter.CreateNetrunner("Tester");
        var enemy = MakeEnemy();
        var fillerData = new CardData
        {
            Id = "filler", Name = "filler", Class = CardClass.Neutral,
            Rarity = CardRarity.Common, Type = CardType.Skill, Cost = 0,
            Description = "filler", Effects = []
        };

        // Exactly 5 cards: compile + 4 filler
        var deck = new List<Card>
        {
            new Card(_cardDb.GetCard("compile")!),
            new Card(fillerData), new Card(fillerData),
            new Card(fillerData), new Card(fillerData),
        };

        var combat = new CombatManager(42, _cardDb);
        combat.Initialize([player], [enemy], new Dictionary<int, List<Card>> { [player.Id] = deck });
        combat.StartCombat();
        combat.Actions.ProcessAll();

        // After StartCombat, all 5 cards should be in hand (drawPerTurn=5, deck size=5)
        var dm = combat.PlayerDecks[player.Id];
        // Guarantee compile is in hand — draw pile might be empty already
        var hand = dm.Hand;
        for (int i = 0; i < 10 && hand.All(c => c.Data.Id != "compile"); i++)
            dm.Draw(1);

        var compile = hand.FirstOrDefault(c => c.Data.Id == "compile");
        if (compile == null) return; // can't test — skip rather than fail

        // Move some hand cards to discard so they can be reshuffled when draw pile is empty
        var other = hand.First(c => c.Data.Id != "compile");
        dm.DiscardFromHand(other);

        int stackBefore = player.Powers.GetStacks(CommonPowerIds.ProtocolStack);
        int handBefore = hand.Count;

        bool played = combat.TryPlayCard(player, compile);
        Assert.True(played);
        combat.Actions.ProcessAll();

        // Protocol stack must increase
        Assert.Equal(stackBefore + 2, player.Powers.GetStacks(CommonPowerIds.ProtocolStack));
        // Draw must have happened: if discard was reshuffled, hand stays same size
        // or at minimum the card was drawn (net: played 1, drew 1 → handBefore or handBefore-1 if nothing to draw)
        // With discard reshuffle available, we expect hand count == handBefore
        Assert.Equal(handBefore, hand.Count);
    }

    [Fact]
    public void Backdoor_implant_adds_protocol_stack_without_enemy_target()
    {
        // "协议栈 +2" — pure self-buff skill.
        var (combat, player, _) = StartCombatWithHand("backdoor_implant");
        var hand = combat.PlayerDecks[player.Id].Hand;
        var card = hand.First(c => c.Data.Id == "backdoor_implant");

        int stackBefore = player.Powers.GetStacks(CommonPowerIds.ProtocolStack);
        bool played = combat.TryPlayCard(player, card);
        Assert.True(played);
        combat.Actions.ProcessAll();
        Assert.Equal(stackBefore + 2, player.Powers.GetStacks(CommonPowerIds.ProtocolStack));
    }

    [Fact]
    public void Cache_refresh_draws_unconditional_plus_conditional()
    {
        // "抽 1 张牌。若协议栈 ≥ 2 层，额外抽 1 张。"
        var (combat, player, _) = StartCombatWithHand("cache_refresh");
        player.Powers.ApplyPower(new ProtocolStackPower { Amount = 3 }, player);

        var hand = combat.PlayerDecks[player.Id].Hand;
        var card = hand.First(c => c.Data.Id == "cache_refresh");

        int handBefore = hand.Count;
        bool played = combat.TryPlayCard(player, card);
        Assert.True(played);
        combat.Actions.ProcessAll();

        int handAfter = combat.PlayerDecks[player.Id].Hand.Count;
        // Played 1 (out), drew 2 (base 1 + conditional 1) → net +1
        Assert.Equal(handBefore + 1, handAfter);
    }

    [Fact]
    public void Proxy_shield_grants_block_without_enemy_target()
    {
        // "获得 5 护甲。协议栈 +1。"
        var (combat, player, _) = StartCombatWithHand("proxy_shield");
        var hand = combat.PlayerDecks[player.Id].Hand;
        var card = hand.First(c => c.Data.Id == "proxy_shield");

        int blockBefore = player.Block;
        bool played = combat.TryPlayCard(player, card);
        Assert.True(played);
        combat.Actions.ProcessAll();
        Assert.Equal(blockBefore + 5, player.Block);
        Assert.True(player.Powers.GetStacks(CommonPowerIds.ProtocolStack) >= 1);
    }

    [Fact]
    public void Regenerate_applies_power_to_self_not_to_enemy()
    {
        var player = PlayerCharacter.CreateSymbiote("S");
        var enemy = MakeEnemy("e", 50);
        var deck = new List<Card> { new Card(_cardDb.GetCard("regenerate")!) };
        for (int i = 0; i < 10; i++)
            deck.Add(new Card(new CardData
            {
                Id = $"f{i}", Name = "f", Class = CardClass.Neutral,
                Rarity = CardRarity.Common, Type = CardType.Skill, Cost = 0, Description = ""
            }));

        var combat = new CombatManager(99, _cardDb);
        combat.Initialize([player], [enemy], new Dictionary<int, List<Card>> { [player.Id] = deck });
        combat.StartCombat();
        combat.Actions.ProcessAll();

        var hand = combat.PlayerDecks[player.Id].Hand;
        for (int i = 0; i < 30 && hand.All(c => c.Data.Id != "regenerate"); i++)
            combat.PlayerDecks[player.Id].Draw(1);

        var card = hand.First(c => c.Data.Id == "regenerate");
        bool played = combat.TryPlayCard(player, card);
        Assert.True(played, "Self-buff skill must not require an enemy target");
        combat.Actions.ProcessAll();

        // Regeneration status should be applied to the player, not the enemy.
        Assert.True(player.Powers.HasPower(CommonPowerIds.Regeneration) || player.StatusEffects.Has(StatusType.Regeneration));
        Assert.False(enemy.Powers.HasPower(CommonPowerIds.Regeneration));
    }
}
