using Godot;
using RogueCardGame.Core.Cards;
using RogueCardGame.Core.Characters;
using RogueCardGame.Core.Combat;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace RogueCardGame;

/// <summary>
/// Headless scene-transition smoke check for the Phase 3 run loop.
/// Run with:
/// Godot --headless --path . --scene res://scenes/testing/Phase3FlowSmoke.tscn
/// </summary>
public partial class Phase3FlowSmokeRunner : Node
{
    private const int SmokeSeed = 731919;

    private string _savePath = "";
    private string? _saveBackupPath;
    private int _assertionCount;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        CallDeferred(MethodName.RunDeferred);
    }

    private async void RunDeferred()
    {
        try
        {
            BackupUserSaves();
            KeepRunnerAliveAcrossSceneChanges();
            await RunFlowSmoke();
            await CleanupCurrentScene();
            await StopAudioPlayback();
            ClearTextureCaches();
            await WaitFrames(6);
            GD.Print($"[Phase3FlowSmoke] PASS ({_assertionCount} checks)");
            GetTree().Quit(0);
        }
        catch (Exception ex)
        {
            await CleanupCurrentScene();
            await StopAudioPlayback();
            ClearTextureCaches();
            await WaitFrames(6);
            GD.PrintErr($"[Phase3FlowSmoke] FAIL: {ex.Message}");
            GD.PrintErr(ex.StackTrace ?? "");
            GetTree().Quit(1);
        }
        finally
        {
            RestoreUserSaves();
        }
    }

    private async Task RunFlowSmoke()
    {
        GD.Print("[Phase3FlowSmoke] Starting flow smoke");
        await WaitFrames(2);

        var manager = GameManager.Instance
            ?? throw new InvalidOperationException("GameManager autoload is not available.");
        manager.StartNewRun(CardClass.Vanguard, SmokeSeed);

        GD.Print("[Phase3FlowSmoke] Loading map scene");
        SceneManager.Instance.ChangeSceneImmediate(SceneManager.Scenes.Map);
        var mapScene = await WaitForScene("MapScene");
        Require(manager.CurrentRun?.CurrentSceneId == "map", "Run did not start on map scene id.");
        Require(manager.CurrentRun?.CurrentMap?.CurrentNode == null, "New run should not have a current map node before route selection.");

        GD.Print("[Phase3FlowSmoke] Pressing first accessible map node");
        PressFirstAccessibleMapNode(mapScene);
        var combatScene = await WaitForScene("CombatScene");
        Require(manager.CurrentRun?.CurrentSceneId == "combat", "Map click did not persist combat scene id.");
        Require(manager.CurrentRun?.CurrentMap?.CurrentNode?.Type == Core.Map.RoomType.Combat, "Map click did not move to a combat node.");
        RequireNode<HBoxContainer>(combatScene, "EnemyArea");
        RequireNode<Control>(combatScene, "HandArea");

        GD.Print("[Phase3FlowSmoke] Autoplaying combat to victory");
        AutoplayCombatToVictory(combatScene);
        var rewardScene = await WaitForScene("RewardScene");
        Require(manager.CurrentRun?.CurrentSceneId == "reward", "Combat victory did not persist reward scene id.");
        RequireNode<VBoxContainer>(rewardScene, "RewardList");
        RequireNode<HBoxContainer>(rewardScene, "CardRewardArea");

        GD.Print("[Phase3FlowSmoke] Returning to map from rewards");
        var skipButton = RequireNode<Button>(rewardScene, "SkipBtn");
        Press(skipButton);
        var returnedMapScene = await WaitForScene("MapScene");
        Require(manager.CurrentRun?.CurrentSceneId == "map", "Reward skip did not persist map scene id.");
        Require(manager.CurrentRun?.FloorsCleared == 1, "Combat victory did not increment cleared floor count.");
        Require(manager.CurrentRun?.CurrentMap?.CurrentNode?.IsVisited == true, "Visited combat node was not retained after returning to map.");
        RequireNode<Control>(returnedMapScene, "MapScroll/MapContainer");
    }

    private void PressFirstAccessibleMapNode(Node mapScene)
    {
        var container = RequireNode<Control>(mapScene, "MapScroll/MapContainer");
        var button = EnumerateDescendants(container)
            .OfType<Button>()
            .FirstOrDefault(btn => !btn.Disabled);

        Require(button != null, "Map scene did not expose an accessible route button.");
        Press(button!);
    }

    private void AutoplayCombatToVictory(Node combatScene)
    {
        var combat = GetCombatManager(combatScene);
        var refreshAll = combatScene.GetType().GetMethod("RefreshAll", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("CombatScene.RefreshAll method was not found.");
        var processResult = combatScene.GetType().GetMethod("ProcessCombatResult", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("CombatScene.ProcessCombatResult method was not found.");

        Require(combat.Enemies.Count > 0, "CombatManager did not create enemies for the selected node.");

        int cardsPlayed = 0;
        int turnsTaken = 0;
        while (combat.IsActive && turnsTaken < 12)
        {
            var player = combat.Players.FirstOrDefault(p => p.IsAlive && !p.IsDowned)
                ?? throw new InvalidOperationException("Autoplay combat has no active player.");
            var deck = combat.PlayerDecks.GetValueOrDefault(player.Id)
                ?? throw new InvalidOperationException("Autoplay combat has no player deck.");

            bool playedAny = true;
            while (combat.IsActive && playedAny)
            {
                playedAny = false;
                foreach (var card in deck.Hand
                    .OrderByDescending(card => card.Data.Type == CardType.Attack)
                    .ThenBy(card => card.EffectiveCost)
                    .ToList())
                {
                    if (!combat.CanPlay(player, card))
                        continue;

                    var target = ChooseTarget(combat, player, card);
                    bool played = target != null
                        ? combat.TryPlayCard(player, card, target)
                        : combat.TryPlayCard(player, card);

                    if (!played)
                        continue;

                    cardsPlayed++;
                    playedAny = true;
                    refreshAll.Invoke(combatScene, null);
                    break;
                }
            }

            if (!combat.IsActive)
                break;

            combat.EndPlayerTurn();
            turnsTaken++;
            refreshAll.Invoke(combatScene, null);
        }

        Require(cardsPlayed > 0, "Autoplay combat did not play any cards.");
        Require(combat.Enemies.All(enemy => !enemy.IsAlive), "Autoplay combat did not defeat all enemies within 12 turns.");
        processResult.Invoke(combatScene, null);
    }

    private static CombatManager GetCombatManager(Node combatScene)
    {
        var combatField = combatScene.GetType().GetField("_combat", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("CombatScene._combat field was not found.");
        return combatField.GetValue(combatScene) as CombatManager
            ?? throw new InvalidOperationException("CombatScene did not create a CombatManager.");
    }

    private static Combatant? ChooseTarget(CombatManager combat, PlayerCharacter player, Card card)
    {
        var targetType = card.Data.EffectiveTargetType;
        if (targetType is TargetType.Self or TargetType.None)
            return null;

        if (targetType is not (TargetType.SingleEnemy or TargetType.SingleAlly))
            return null;

        var validTargets = combat.Targeting.GetValidTargets(
            player,
            targetType,
            card.Data.Range,
            combat.Enemies.Where(enemy => enemy.IsAlive),
            combat.Players.Where(p => p.IsAlive));

        return validTargets
            .OrderBy(target => target is Enemy enemy ? enemy.CurrentHp : int.MaxValue)
            .FirstOrDefault();
    }

    private void KeepRunnerAliveAcrossSceneChanges()
    {
        if (GetTree().CurrentScene != this)
            return;

        var host = new Node { Name = "Phase3FlowSmokeSceneHost" };
        GetTree().Root.AddChild(host);
        GetTree().CurrentScene = host;
    }

    private async Task<Node> WaitForScene(string expectedName, double timeoutSeconds = 4.0)
    {
        ulong deadline = Time.GetTicksMsec() + (ulong)(timeoutSeconds * 1000);
        while (Time.GetTicksMsec() < deadline)
        {
            var currentScene = GetTree().CurrentScene;
            if (currentScene?.Name == expectedName)
            {
                await WaitForSceneManagerIdle();
                await WaitFrames(4);
                return currentScene;
            }

            await WaitFrames(1);
        }

        var actual = GetTree().CurrentScene?.Name.ToString() ?? "<none>";
        throw new InvalidOperationException($"Timed out waiting for scene '{expectedName}'. Current scene: {actual}.");
    }

    private async Task WaitForSceneManagerIdle(double timeoutSeconds = 3.0)
    {
        var transitionField = typeof(SceneManager).GetField("_isTransitioning", BindingFlags.Instance | BindingFlags.NonPublic);
        if (transitionField == null || SceneManager.Instance == null)
            return;

        ulong deadline = Time.GetTicksMsec() + (ulong)(timeoutSeconds * 1000);
        while (Time.GetTicksMsec() < deadline)
        {
            bool isTransitioning = (bool)(transitionField.GetValue(SceneManager.Instance) ?? false);
            if (!isTransitioning)
                return;

            await WaitFrames(1);
        }

        throw new InvalidOperationException("Timed out waiting for SceneManager transition to complete.");
    }

    private async Task CleanupCurrentScene()
    {
        var currentScene = GetTree().CurrentScene;
        if (currentScene != null && currentScene != this)
        {
            GetTree().CurrentScene = null;
            currentScene.QueueFree();
            await WaitFrames(10);
        }
    }

    private async Task StopAudioPlayback()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.StopBgm();
            foreach (var player in AudioManager.Instance.GetChildren().OfType<AudioStreamPlayer>())
            {
                player.Stop();
                player.Stream = null;
            }
        }

        await WaitFrames(4);
    }

    private static void ClearTextureCaches()
    {
        CyberCardFactory.ClearTextureCache();
        CombatScene.ClearTextureCache();
    }

    private async Task WaitFrames(int count)
    {
        for (int i = 0; i < count; i++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private T RequireNode<T>(Node root, string path) where T : Node
    {
        var node = root.GetNodeOrNull<T>(path);
        Require(node != null, $"{root.Name} is missing node '{path}' ({typeof(T).Name}).");
        return node!;
    }

    private void Require(bool condition, string message)
    {
        _assertionCount++;
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void Press(Button button)
    {
        button.EmitSignal(BaseButton.SignalName.Pressed);
    }

    private static System.Collections.Generic.IEnumerable<Node> EnumerateDescendants(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            yield return child;
            foreach (var descendant in EnumerateDescendants(child))
                yield return descendant;
        }
    }

    private void BackupUserSaves()
    {
        _savePath = Path.TrimEndingDirectorySeparator(ProjectSettings.GlobalizePath("user://saves/"));
        if (!Directory.Exists(_savePath))
            return;

        _saveBackupPath = Path.Combine(Path.GetTempPath(), $"roguecardgame-flow-smoke-{Guid.NewGuid():N}");
        CopyDirectory(_savePath, _saveBackupPath);
    }

    private void RestoreUserSaves()
    {
        if (string.IsNullOrWhiteSpace(_savePath))
            return;

        if (Directory.Exists(_savePath))
            Directory.Delete(_savePath, recursive: true);

        if (_saveBackupPath != null && Directory.Exists(_saveBackupPath))
        {
            CopyDirectory(_saveBackupPath, _savePath);
            Directory.Delete(_saveBackupPath, recursive: true);
        }
    }

    private static void CopyDirectory(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);

        foreach (var file in Directory.EnumerateFiles(sourceDir))
        {
            var targetFile = Path.Combine(targetDir, Path.GetFileName(file));
            File.Copy(file, targetFile, overwrite: true);
        }

        foreach (var directory in Directory.EnumerateDirectories(sourceDir))
        {
            var targetSubdir = Path.Combine(targetDir, Path.GetFileName(directory));
            CopyDirectory(directory, targetSubdir);
        }
    }
}
