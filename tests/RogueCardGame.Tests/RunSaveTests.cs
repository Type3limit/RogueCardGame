using RogueCardGame.Core;
using RogueCardGame.Core.Cards;
using RogueCardGame.Core.Progression;
using RogueCardGame.Core.Run;

namespace RogueCardGame.Tests;

public class RunSaveTests
{
    private static readonly string RepoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    private static readonly string DataDirectory = Path.Combine(RepoRoot, "data");

    static RunSaveTests()
    {
        BalanceConfig.LoadFromFile(Path.Combine(DataDirectory, "balance", "balance.json"));
    }

    [Fact]
    public void RunState_should_roundtrip_via_serialize_and_deserialize()
    {
        var run = new RunState(4242, CardClass.Psion, DataDirectory);
        run.StartRun(DataDirectory);
        run.Player.CurrentHp = 37;
        run.Player.CurrentEnergy = 2;
        run.Player.DrawPerTurn = 6;
        run.Gold = 123;
        run.FloorsCleared = 4;
        run.CurrentAct = 2;
        run.CurrentSceneId = "shop";
        run.SeenOneTimeEvents.Add("one_time_event_alpha");

        run.MasterDeck[0].Upgrade();
        run.MasterDeck[1].TempCostModifier = -1;

        var implant = run.ImplantDb.GetImplant("psion_resonance_core");
        Assert.NotNull(implant);
        run.Implants.Equip(implant!);

        var potion = run.PotionDb.GetPotion("repair_nano");
        Assert.NotNull(potion);
        Assert.True(run.Potions.TryAddPotion(potion!));

        var reachableNode = run.CurrentMap!.GetReachableNodes().First();
        run.CurrentMap.MoveTo(reachableNode.Id);

        string json = run.Serialize();
        var loaded = RunState.Deserialize(json, DataDirectory);

        Assert.Equal(run.Seed, loaded.Seed);
        Assert.Equal(CardClass.Psion, loaded.Player.Class);
        Assert.Equal(37, loaded.Player.CurrentHp);
        Assert.Equal(run.Player.MaxHp, loaded.Player.MaxHp);
        Assert.Equal(2, loaded.Player.CurrentEnergy);
        Assert.Equal(6, loaded.Player.DrawPerTurn);
        Assert.Equal(123, loaded.Gold);
        Assert.Equal(4, loaded.FloorsCleared);
        Assert.Equal(2, loaded.CurrentAct);
        Assert.Equal("shop", loaded.CurrentSceneId);
        Assert.Contains("one_time_event_alpha", loaded.SeenOneTimeEvents);
        Assert.Equal(run.MasterDeck.Count, loaded.MasterDeck.Count);
        Assert.True(loaded.MasterDeck[0].IsUpgraded);
        Assert.Equal(run.MasterDeck[0].Branch, loaded.MasterDeck[0].Branch);
        Assert.Equal(-1, loaded.MasterDeck[1].TempCostModifier);
        Assert.Equal("psion_resonance_core", Assert.Single(loaded.Implants.GetAllEquipped()).Data.Id);
        Assert.Equal("repair_nano", Assert.Single(loaded.Potions.Slots, potion => potion != null)!.Id);
        Assert.NotNull(loaded.CurrentMap);
        Assert.Equal(run.CurrentMap.CurrentNode?.Id, loaded.CurrentMap!.CurrentNode?.Id);
        Assert.Equal(run.CurrentMap.Nodes.Count, loaded.CurrentMap.Nodes.Count);
        Assert.Equal(run.CurrentMap.Nodes.Count(n => n.IsVisited), loaded.CurrentMap.Nodes.Count(n => n.IsVisited));
    }

    [Fact]
    public void SaveManager_should_persist_and_reload_active_run_payload()
    {
        string saveDir = Path.Combine(Path.GetTempPath(), $"roguecardgame-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(saveDir);

        try
        {
            var run = new RunState(777, CardClass.Netrunner, DataDirectory);
            run.StartRun(DataDirectory);
            run.Gold = 222;
            run.CurrentSceneId = "event";

            var meta = new MetaProgress();
            meta.RecordRun(new RunRecord { ClassName = "vanguard", GoldEarned = 15, FloorReached = 3 });

            var manager = new SaveManager(saveDir);
            bool saved = manager.Save(new SaveData
            {
                MetaProgressJson = meta.Serialize(),
                HasActiveRun = true,
                RunStateJson = run.Serialize()
            });

            Assert.True(saved);
            Assert.True(manager.HasSave());
            Assert.True(manager.HasActiveRun());

            var loadedSave = manager.Load();
            Assert.NotNull(loadedSave);
            Assert.True(loadedSave!.HasActiveRun);

            var loadedRun = RunState.Deserialize(loadedSave.RunStateJson, DataDirectory);
            Assert.Equal(222, loadedRun.Gold);
            Assert.Equal("event", loadedRun.CurrentSceneId);

            var loadedMeta = MetaProgress.Deserialize(loadedSave.MetaProgressJson);
            Assert.Equal(1, loadedMeta.Stats.TotalRuns);

            Assert.True(manager.DeleteRunSave());
            Assert.False(manager.HasActiveRun());
        }
        finally
        {
            if (Directory.Exists(saveDir))
                Directory.Delete(saveDir, recursive: true);
        }
    }

    [Fact]
    public void SaveData_should_support_active_run_lifecycle_for_game_manager_flow()
    {
        string saveDir = Path.Combine(Path.GetTempPath(), $"roguecardgame-saveflow-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(saveDir);

        try
        {
            var manager = new SaveManager(saveDir);
            var meta = new MetaProgress();
            var run = new RunState(999, CardClass.Symbiote, DataDirectory);
            run.StartRun(DataDirectory);

            run.CurrentSceneId = "map";
            Assert.True(manager.Save(new SaveData
            {
                MetaProgressJson = meta.Serialize(),
                HasActiveRun = true,
                RunStateJson = run.Serialize()
            }));

            Assert.True(manager.HasActiveRun());
            var loaded = manager.Load();
            Assert.NotNull(loaded);
            Assert.True(loaded!.HasActiveRun);
            Assert.Equal("map", RunState.Deserialize(loaded.RunStateJson, DataDirectory).CurrentSceneId);

            run.CurrentSceneId = "shop";
            Assert.True(manager.Save(new SaveData
            {
                MetaProgressJson = meta.Serialize(),
                HasActiveRun = true,
                RunStateJson = run.Serialize()
            }));

            var updated = manager.Load();
            Assert.NotNull(updated);
            Assert.Equal("shop", RunState.Deserialize(updated!.RunStateJson, DataDirectory).CurrentSceneId);

            run.EndRun(false);
            Assert.True(manager.Save(new SaveData
            {
                MetaProgressJson = meta.Serialize(),
                HasActiveRun = false,
                RunStateJson = string.Empty
            }));

            Assert.False(manager.HasActiveRun());
        }
        finally
        {
            if (Directory.Exists(saveDir))
                Directory.Delete(saveDir, recursive: true);
        }
    }
}
