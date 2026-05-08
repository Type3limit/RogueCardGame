using RogueCardGame.Core;
using RogueCardGame.Core.Cards;
using RogueCardGame.Core.Characters;
using RogueCardGame.Core.Combat;
using RogueCardGame.Core.Combat.Actions;
using RogueCardGame.Core.Combat.Powers;
using RogueCardGame.Core.Deck;

namespace RogueCardGame.Tests;

/// <summary>
/// Regression tests for the Core refactor (phases P0–P7).
/// Covers the 4 previously-broken powers and the unified cost pipeline.
/// </summary>
public class CorePowerRegressionTests
{
    static CorePowerRegressionTests()
    {
        BalanceConfig.LoadFromFile("../../data/balance/balance.json");
    }

    // ---------- Helpers ----------

    private static CardEffectContext CreateContext(
        Combatant source,
        Combatant target,
        FormationSystem formation,
        AggroSystem aggro,
        ActionManager? actions = null)
    {
        return new CardEffectContext
        {
            Source = source,
            Target = target,
            AllTargets = [target],
            Formation = formation,
            Aggro = aggro,
            Actions = actions,
            Range = CardRange.Melee,
            LastDamageDealt = 0,
            TotalDamageDealtThisCard = 0,
            LastSelfHpLoss = 0,
            TotalSelfHpLossThisCard = 0
        };
    }

    private static Enemy MakeEnemy(string name, int hp)
    {
        return new Enemy(new EnemyData
        {
            Id = name.ToLowerInvariant(),
            Name = name,
            MaxHp = hp,
            PreferredRow = FormationRow.Front,
            IntentPatterns = []
        });
    }

    private static Card MakeAttackCard(int cost) => new(new CardData
    {
        Id = "atk",
        Name = "atk",
        Class = CardClass.Neutral,
        Rarity = CardRarity.Common,
        Type = CardType.Attack,
        Cost = cost,
        Description = "atk"
    });

    // ---------- WarMachine ----------

    [Fact]
    public void WarMachinePower_grants_block_per_overcharge_stack_consumed()
    {
        var player = PlayerCharacter.CreateVanguard("V");
        var enemy = MakeEnemy("E", 100);
        var formation = new FormationSystem();
        formation.SetPosition(player.Id, FormationRow.Front);
        formation.SetPosition(enemy.Id, FormationRow.Front);
        var actions = new ActionManager();
        player.Powers.ActionManager = actions;

        player.Powers.ApplyPower(new WarMachinePower { Amount = 1 }, player);
        player.Powers.ApplyPower(new OverchargePower { Amount = 5 }, player);

        actions.AddToBottom(new OverchargeConsumeAction(
            player, [enemy], baseDamage: 0, damagePerStack: 1,
            CardRange.Melee, formation));
        actions.ProcessAll();

        Assert.Equal(5, player.Block);
    }

    [Fact]
    public void WarMachinePower_draws_cards_every_three_stacks()
    {
        var player = PlayerCharacter.CreateVanguard("V");
        var enemy = MakeEnemy("E", 100);
        var formation = new FormationSystem();
        formation.SetPosition(player.Id, FormationRow.Front);
        formation.SetPosition(enemy.Id, FormationRow.Front);

        var deckCards = Enumerable.Range(0, 10).Select(i => new Card(new CardData
        {
            Id = $"c{i}", Name = $"c{i}", Class = CardClass.Neutral,
            Rarity = CardRarity.Common, Type = CardType.Skill, Cost = 0, Description = ""
        })).ToList();

        var combat = new CombatManager(123);
        combat.Initialize([player], [enemy], new Dictionary<int, List<Card>>
        {
            [player.Id] = deckCards
        });
        combat.StartCombat();
        // Drain any turn-start drawing so we have a stable baseline
        combat.Actions.ProcessAll();

        player.Powers.ApplyPower(new WarMachinePower { Amount = 1 }, player);
        player.Powers.ApplyPower(new OverchargePower { Amount = 6 }, player);

        int before = combat.PlayerDecks[player.Id].Hand.Count;
        combat.Actions.AddToBottom(new OverchargeConsumeAction(
            player, [enemy], 0, 1, CardRange.Melee, formation));
        combat.Actions.ProcessAll();
        int after = combat.PlayerDecks[player.Id].Hand.Count;

        Assert.Equal(before + 2, after); // 6 / 3 = 2 draws
    }

    // ---------- FrontlineCommander ----------

    [Fact]
    public void FrontlineCommanderPower_adds_bonus_damage_per_overcharge_stack()
    {
        var player = PlayerCharacter.CreateVanguard("V");
        var enemy = MakeEnemy("E", 200);
        var formation = new FormationSystem();
        formation.SetPosition(player.Id, FormationRow.Front);
        formation.SetPosition(enemy.Id, FormationRow.Front);
        var actions = new ActionManager();
        player.Powers.ActionManager = actions;

        player.Powers.ApplyPower(new FrontlineCommanderPower { Amount = 1 }, player);
        player.Powers.ApplyPower(new OverchargePower { Amount = 3 }, player);

        actions.AddToBottom(new OverchargeConsumeAction(
            player, [enemy], baseDamage: 5, damagePerStack: 0,
            CardRange.Melee, formation));
        actions.ProcessAll();

        // Base 5 + 3 stacks * 1 per-stack bonus from FrontlineCommander = 8
        Assert.Equal(200 - 8, enemy.CurrentHp);
    }

    // ---------- ParasiticBond ----------

    [Fact]
    public void ParasiticBondPower_heals_source_when_target_takes_damage()
    {
        var player = PlayerCharacter.CreateSymbiote("S");
        player.CurrentHp = 20; // below max so heal is observable
        var enemy = MakeEnemy("E", 100);
        var formation = new FormationSystem();
        formation.SetPosition(player.Id, FormationRow.Front);
        formation.SetPosition(enemy.Id, FormationRow.Front);
        var actions = new ActionManager();
        enemy.Powers.ActionManager = actions;

        enemy.Powers.ApplyPower(new ParasiticBondPower { Amount = 50 }, enemy, source: player);

        actions.AddToBottom(new DealDamageAction(player, [enemy], 10, CardRange.Melee, formation));
        actions.ProcessAll();

        // Enemy takes 10 HP; parasitic bond heals player for 10 * 50% = 5
        Assert.Equal(25, player.CurrentHp);
    }

    // ---------- ResonanceAmplify one-shot ----------

    [Fact]
    public void ResonanceAmplifyPower_removes_itself_after_one_consume()
    {
        var player = PlayerCharacter.CreatePsion("P");
        var actions = new ActionManager();
        player.Powers.ActionManager = actions;

        player.Powers.ApplyPower(new ResonanceAmplifyPower { Amount = 2 }, player);
        Assert.True(player.Powers.HasPower("ResonanceAmplify"));

        // Simulate a resonance consume pipeline tick
        float dmg = player.Powers.ModifyConsumeResonanceDamage(0, stacks: 3);
        Assert.Equal(6f, dmg); // 3 stacks * Amount(2)
        actions.ProcessAll();

        Assert.False(player.Powers.HasPower("ResonanceAmplify"));
    }

    // ---------- MindSovereign cost reduction ----------

    [Fact]
    public void MindSovereignPower_reduces_attack_cost_on_marked_enemy()
    {
        var player = PlayerCharacter.CreatePsion("P");
        var enemy = MakeEnemy("E", 30);
        player.Powers.ApplyPower(new MindSovereignPower { Amount = 1 }, player);
        enemy.Powers.ApplyPower(new MarkPower { Amount = 1 }, enemy);

        var atk = MakeAttackCard(cost: 2);
        int modified = player.Powers.ModifyCardCost(atk, 2, enemy);

        Assert.Equal(1, modified);
    }

    [Fact]
    public void MindSovereignPower_leaves_cost_unchanged_on_unmarked_enemy()
    {
        var player = PlayerCharacter.CreatePsion("P");
        var enemy = MakeEnemy("E", 30);
        player.Powers.ApplyPower(new MindSovereignPower { Amount = 1 }, player);

        var atk = MakeAttackCard(cost: 2);
        int modified = player.Powers.ModifyCardCost(atk, 2, enemy);

        Assert.Equal(2, modified);
    }

    // ---------- Expr ----------

    [Fact]
    public void Expr_parses_power_threshold_and_evaluates()
    {
        var player = PlayerCharacter.CreatePsion("P");
        var enemy = MakeEnemy("E", 30);
        var ctx = CreateContext(player, enemy, new FormationSystem(), new AggroSystem());

        var expr = Expr.Parse("resonance>=5");
        Assert.False(expr.Evaluate(ctx));

        player.Powers.ApplyPower(new ResonancePower { Amount = 5 }, player);
        Assert.True(expr.Evaluate(ctx));
    }

    [Fact]
    public void Expr_combinators_work()
    {
        var player = PlayerCharacter.CreatePsion("P");
        var enemy = MakeEnemy("E", 30);
        var ctx = CreateContext(player, enemy, new FormationSystem(), new AggroSystem());

        var expr = new Expr.And(
            new Expr.HasPower(CommonPowerIds.Vulnerable, OnSource: false),
            new Expr.PowerGte(CommonPowerIds.Resonance, 3));

        Assert.False(expr.Evaluate(ctx));
        enemy.Powers.ApplyPower(new VulnerablePower { Amount = 2 }, enemy);
        player.Powers.ApplyPower(new ResonancePower { Amount = 3 }, player);
        Assert.True(expr.Evaluate(ctx));
    }

    // ---------- TargetSelector ----------

    [Fact]
    public void TargetSelector_parses_known_specs()
    {
        Assert.Same(TargetSelector.Self, TargetSelector.Parse("self"));
        Assert.Same(TargetSelector.AllEnemies, TargetSelector.Parse("allEnemies"));
        Assert.Same(TargetSelector.AllEnemies, TargetSelector.Parse("all_enemies"));
        Assert.Same(TargetSelector.Default, TargetSelector.Parse(null));
        Assert.Same(TargetSelector.Default, TargetSelector.Parse(""));
        Assert.Same(TargetSelector.Default, TargetSelector.Parse("default"));
    }

    [Fact]
    public void TargetSelector_self_returns_source_only()
    {
        var player = PlayerCharacter.CreatePsion("P");
        var enemy = MakeEnemy("E", 30);
        var ctx = CreateContext(player, enemy, new FormationSystem(), new AggroSystem());

        var list = TargetSelector.Self.Select(ctx);
        Assert.Single(list);
        Assert.Same(player, list[0]);
    }
}
