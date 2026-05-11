using RogueCardGame.Core;
using RogueCardGame.Core.Cards;
using RogueCardGame.Core.Characters;
using RogueCardGame.Core.Combat;

namespace RogueCardGame.Tests;

public class Phase4FormationTests
{
    private static readonly string RepoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    private static readonly string DataDirectory = Path.Combine(RepoRoot, "data");
    private static readonly CardDatabase CardDb = LoadCards();

    static Phase4FormationTests()
    {
        BalanceConfig.LoadFromFile(Path.Combine(DataDirectory, "balance", "balance.json"));
    }

    [Fact]
    public void Combat_initializes_enemy_rows_and_melee_cannot_bypass_front_row()
    {
        var player = PlayerCharacter.CreateVanguard("Tester");
        var frontEnemy = MakeEnemy("front_guard", FormationRow.Front);
        var backEnemy = MakeEnemy("back_sniper", FormationRow.Back);
        var combat = new CombatManager(11, CardDb);

        combat.Initialize([player], [frontEnemy, backEnemy], new Dictionary<int, List<Card>>
        {
            [player.Id] = [MakeFillerCard()]
        });

        Assert.Equal(FormationRow.Front, combat.Formation.GetPosition(frontEnemy.Id));
        Assert.Equal(FormationRow.Back, combat.Formation.GetPosition(backEnemy.Id));

        var meleeTargets = combat.Targeting.GetValidTargets(
            player,
            TargetType.SingleEnemy,
            CardRange.Melee,
            combat.Enemies,
            combat.Players);
        var rangedTargets = combat.Targeting.GetValidTargets(
            player,
            TargetType.SingleEnemy,
            CardRange.Ranged,
            combat.Enemies,
            combat.Players);

        Assert.Equal([frontEnemy], meleeTargets);
        Assert.Contains(backEnemy, rangedTargets);
        Assert.Contains(frontEnemy, rangedTargets);
    }

    [Fact]
    public void Impact_shell_pushes_front_melee_enemy_back_and_disables_next_attack()
    {
        var player = PlayerCharacter.CreateVanguard("Tester");
        var enemy = MakeEnemy("front_bruiser", FormationRow.Front, new EnemyIntentPattern
        {
            Type = EnemyIntentType.Attack,
            Value = 10,
            Scope = TargetScope.SingleFront,
            Weight = 1f
        });
        var shell = new Card(CardDb.GetCard("vanguard_impact_shell")!);
        var combat = new CombatManager(17, CardDb);
        var enemyActions = new List<EnemyIntentType>();
        combat.OnEnemyAction += (_, intent) => enemyActions.Add(intent.Type);

        combat.Initialize([player], [enemy], new Dictionary<int, List<Card>>
        {
            [player.Id] = [shell]
        });
        combat.StartCombat();
        combat.Actions.ProcessAll();

        Assert.Equal(FormationRow.Front, combat.Formation.GetPosition(enemy.Id));
        Assert.True(combat.TryPlayCard(player, shell, enemy));
        combat.Actions.ProcessAll();

        Assert.Equal(FormationRow.Back, combat.Formation.GetPosition(enemy.Id));
        var hpBeforeEnemyTurn = player.CurrentHp;

        combat.EndPlayerTurn();
        combat.Actions.ProcessAll();

        Assert.Equal(hpBeforeEnemyTurn, player.CurrentHp);
        Assert.Contains(EnemyIntentType.Disabled, enemyActions);
    }

    private static CardDatabase LoadCards()
    {
        var db = new CardDatabase();
        db.LoadFromDirectory(Path.Combine(RepoRoot, "data", "cards"));
        return db;
    }

    private static Enemy MakeEnemy(
        string id,
        FormationRow row,
        params EnemyIntentPattern[] patterns)
    {
        return new Enemy(new EnemyData
        {
            Id = id,
            Name = id,
            MaxHp = 60,
            PreferredRow = row,
            IntentPatterns = patterns.ToList()
        });
    }

    private static Card MakeFillerCard()
    {
        return new Card(new CardData
        {
            Id = "formation_filler",
            Name = "Formation Filler",
            Class = CardClass.Neutral,
            Rarity = CardRarity.Common,
            Type = CardType.Skill,
            Cost = 0,
            Range = CardRange.None,
            TargetType = TargetType.Self,
            Description = "Test filler",
            Effects = []
        });
    }
}
