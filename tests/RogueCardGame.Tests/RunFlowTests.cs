using RogueCardGame.Core;
using RogueCardGame.Core.Cards;
using RogueCardGame.Core.Map;
using RogueCardGame.Core.Run;

namespace RogueCardGame.Tests;

public class RunFlowTests
{
    private static readonly string RepoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    private static readonly string DataDirectory = Path.Combine(RepoRoot, "data");

    static RunFlowTests()
    {
        BalanceConfig.LoadFromFile(Path.Combine(DataDirectory, "balance", "balance.json"));
    }

    [Fact]
    public void RunState_should_walk_an_act_path_and_create_combat_for_each_combat_node()
    {
        var run = new RunState(20260510, CardClass.Vanguard, DataDirectory);
        run.StartRun(DataDirectory);

        Assert.Equal(1, run.CurrentAct);
        Assert.NotNull(run.CurrentMap);

        int combatNodesResolved = 0;
        bool reachedBoss = false;

        while (!reachedBoss)
        {
            var next = run.CurrentMap!.GetReachableNodes()
                .OrderBy(node => node.Row)
                .ThenBy(node => node.Column)
                .FirstOrDefault();

            Assert.NotNull(next);
            Assert.True(run.CurrentMap.CanMoveTo(next!.Id));
            run.CurrentMap.MoveTo(next.Id);

            if (next.Type is RoomType.Combat or RoomType.EliteCombat or RoomType.Boss)
            {
                var enemies = run.CreateEnemiesForCurrentNode();
                Assert.NotEmpty(enemies);
                Assert.All(enemies, enemy => Assert.True(enemy.MaxHp > 0));

                var combat = run.CreateCombat(enemies);
                combat.StartCombat();

                Assert.True(combat.PlayerDecks.ContainsKey(run.Player.Id));
                Assert.NotEmpty(combat.PlayerDecks[run.Player.Id].Hand);

                run.OnCombatVictory(
                    wasElite: next.Type == RoomType.EliteCombat,
                    wasBoss: next.Type == RoomType.Boss);
                combatNodesResolved++;
            }
            else if (next.Type == RoomType.RestSite)
            {
                run.Rest();
            }

            reachedBoss = next.Type == RoomType.Boss;
        }

        Assert.True(combatNodesResolved > 0);
        Assert.Equal(combatNodesResolved, run.FloorsCleared);

        run.AdvanceAct();

        Assert.Equal(2, run.CurrentAct);
        Assert.NotNull(run.CurrentMap);
        Assert.Equal(2, run.CurrentMap!.ActNumber);
        Assert.Null(run.CurrentMap.CurrentNode);
    }
}
