using Godot;
using RogueCardGame.Core.Cards;
using RogueCardGame.Core.Map;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace RogueCardGame;

/// <summary>
/// Headless scene-level smoke checks for the Phase 3 UI flow.
/// Run with:
/// Godot --headless --path . --scene res://scenes/testing/Phase3UiSmoke.tscn
/// </summary>
public partial class Phase3UiSmokeRunner : Node
{
    private const int SmokeSeed = 340031;

    private Node? _activeScene;
    private string _savePath = "";
    private string? _saveBackupPath;
    private int _assertionCount;

    public override async void _Ready()
    {
        try
        {
            BackupUserSaves();
            await RunSmokeChecks();
            await CleanupActiveScene();
            await StopAudioPlayback();
            ClearTextureCaches();
            await WaitFrames(6);
            GD.Print($"[Phase3UiSmoke] PASS ({_assertionCount} checks)");
            GetTree().Quit(0);
        }
        catch (Exception ex)
        {
            await CleanupActiveScene();
            await StopAudioPlayback();
            ClearTextureCaches();
            await WaitFrames(6);
            GD.PrintErr($"[Phase3UiSmoke] FAIL: {ex.Message}");
            GD.PrintErr(ex.StackTrace ?? "");
            GetTree().Quit(1);
        }
        finally
        {
            RestoreUserSaves();
        }
    }

    private async Task RunSmokeChecks()
    {
        await WaitFrames(2);

        var manager = GameManager.Instance
            ?? throw new InvalidOperationException("GameManager autoload is not available.");

        manager.StartNewRun(CardClass.Vanguard, SmokeSeed);
        var run = manager.CurrentRun
            ?? throw new InvalidOperationException("Smoke run was not created.");

        CheckCombatAnimationAssets();

        await CheckMainMenu();
        await CheckMap();
        await CheckCombat(run);
        await CheckReward(run);
        await CheckShop();
        await CheckEvent();
        await CheckRest();
    }

    private async Task CheckMainMenu()
    {
        var scene = await LoadScene("res://scenes/menus/MainMenu.tscn", 3);
        RequireNode<Button>(scene, "VBox/NewGameBtn");
        RequireNode<Button>(scene, "VBox/ContinueBtn");
        RequireNode<Button>(scene, "VBox/SettingsBtn");
        RequireNode<GridContainer>(scene, "ClassSelectPanel/MarginBox/ClassVBox/ClassGrid");
    }

    private async Task CheckMap()
    {
        var scene = await LoadScene("res://scenes/map/Map.tscn", 5);
        RequireNode<ScrollContainer>(scene, "MapScroll");
        var mapContainer = RequireNode<Control>(scene, "MapScroll/MapContainer");
        RequireNode<Label>(scene, "TopBar/ActLabel");
        RequireNode<Button>(scene, "TopBar/DeckBtn");
        Require(mapContainer.GetChildCount() > 0, "Map scene did not render map nodes or paths.");
    }

    private async Task CheckCombat(RogueCardGame.Core.Run.RunState run)
    {
        SetCurrentRoom(run, RoomType.Combat);

        var scene = await LoadScene("res://scenes/combat/Combat.tscn", 8);
        var enemyArea = RequireNode<HBoxContainer>(scene, "EnemyArea");
        var enemyFrontLane = RequireNode<HBoxContainer>(scene, "EnemyArea/EnemyLaneBoard/EnemyFrontLaneBand/EnemyFrontLaneRow/EnemyFrontLane");
        var enemyBackLane = RequireNode<HBoxContainer>(scene, "EnemyArea/EnemyLaneBoard/EnemyBackLaneBand/EnemyBackLaneRow/EnemyBackLane");
        var handArea = RequireNode<Control>(scene, "HandArea");
        var playerPortrait = RequireNode<TextureRect>(scene, "PlayerPortrait");
        RequireNode<Button>(scene, "EndTurnBtn");
        RequireNode<Button>(scene, "FormationArea/SwitchRowBtn");
        RequireNode<Label>(scene, "DeckInfo/DrawPileLabel");
        Require(enemyArea.GetChildCount() > 0, "Combat scene did not render enemies.");
        Require(enemyFrontLane.GetChildCount() + enemyBackLane.GetChildCount() > 0, "Combat scene did not place enemies in formation lanes.");
        Require(handArea.GetChildCount() > 0, "Combat scene did not render the opening hand.");
        Require(playerPortrait.Texture != null, "Combat scene did not load the player portrait texture.");

        if (run.Player.Class == CardClass.Vanguard)
        {
            var firstFrame = playerPortrait.Texture;
            Require(firstFrame != null, "Vanguard combat idle portrait did not load its first frame.");
            var firstRid = firstFrame!.GetRid();
            await WaitSeconds(0.16);
            var nextFrame = playerPortrait.Texture;
            Require(nextFrame != null, "Vanguard combat idle portrait lost its texture.");
            Require(firstRid != nextFrame!.GetRid(), "Vanguard combat idle portrait did not advance frames.");
        }
    }

    private void CheckCombatAnimationAssets()
    {
        var required = new Dictionary<string, string[]>
        {
            ["vanguard"] = ["combat_idle", "attack", "gain_armor", "overload"],
            ["psion"] = ["combat_idle", "attack", "gain_armor", "resonance"],
            ["netrunner"] = ["combat_idle", "attack", "gain_armor", "protocol_stack"],
            ["symbiote"] = ["combat_idle", "attack", "gain_armor", "erosion"],
        };

        foreach (var (cls, actions) in required)
        {
            foreach (var action in actions)
            {
                string label = $"{cls}/{action}";
                string dir = ProjectSettings.GlobalizePath($"res://resources/textures/characters/animations/{cls}/{action}");
                string firstFrame = Path.Combine(dir, "frame_00.png");
                string lastFrame = Path.Combine(dir, "frame_24.png");
                Require(Directory.Exists(dir), $"{label} animation directory is missing.");
                int frameCount = Directory.GetFiles(dir, "frame_*.png").Length;
                Require(frameCount >= 25, $"{label} animation should contain at least 25 PNG frames, found {frameCount}.");
                Require(File.Exists(firstFrame), $"{label} animation is missing frame_00.png.");
                Require(File.Exists(lastFrame), $"{label} animation is missing frame_24.png.");
                Require(!File.ReadAllBytes(firstFrame).SequenceEqual(File.ReadAllBytes(lastFrame)), $"{label} animation frame_00 and frame_24 should not be identical.");
            }
        }
    }

    private async Task CheckReward(RogueCardGame.Core.Run.RunState run)
    {
        SetCurrentRoom(run, RoomType.Combat);
        run.OnCombatVictory(wasElite: false, wasBoss: false);

        var scene = await LoadScene("res://scenes/reward/Reward.tscn", 8);
        var rewardList = RequireNode<VBoxContainer>(scene, "RewardList");
        var cardRewardArea = RequireNode<HBoxContainer>(scene, "CardRewardArea");
        RequireNode<Button>(scene, "SkipBtn");
        Require(rewardList.GetChildCount() > 0, "Reward scene did not render reward items.");
        Require(cardRewardArea.GetChildCount() >= 3, "Reward scene did not render card reward choices.");
    }

    private async Task CheckShop()
    {
        var scene = await LoadScene("res://scenes/shop/Shop.tscn", 8);
        var cardRow = RequireNode<HBoxContainer>(scene, "ShopScroll/ShopContent/CardSection/CardRow");
        var miscRow = RequireNode<HBoxContainer>(scene, "ShopScroll/ShopContent/MiscSection/MiscRow");
        var serviceRow = RequireNode<HBoxContainer>(scene, "ShopScroll/ShopContent/ServiceSection/ServiceRow");
        RequireNode<Button>(scene, "LeaveBtn");
        Require(cardRow.GetChildCount() > 0, "Shop scene did not render card inventory.");
        Require(miscRow.GetChildCount() > 0, "Shop scene did not render potion or implant inventory.");
        Require(serviceRow.GetChildCount() > 0, "Shop scene did not render services.");
    }

    private async Task CheckEvent()
    {
        var scene = await LoadScene("res://scenes/event/Event.tscn", 5);
        await WaitSeconds(0.75);
        await WaitFrames(3);
        var title = RequireNode<Label>(scene, "EventPanel/Content/Title");
        var choices = RequireNode<VBoxContainer>(scene, "EventPanel/Content/ChoicesContainer");
        Require(!string.IsNullOrWhiteSpace(title.Text), "Event scene title is empty.");
        Require(choices.GetChildCount() > 0, "Event scene did not render choices after typewriter delay.");
    }

    private async Task CheckRest()
    {
        var scene = await LoadScene("res://scenes/rest/Rest.tscn", 5);
        RequireNode<Label>(scene, "HpLabel");
        RequireNode<Button>(scene, "OptionsContainer/RestBtn");
        RequireNode<Button>(scene, "OptionsContainer/UpgradeBtn");
        RequireNode<VBoxContainer>(scene, "CardListContainer");
    }

    private async Task<Node> LoadScene(string path, int waitFrames)
    {
        if (_activeScene != null)
        {
            await CleanupActiveScene();
        }

        var packed = GD.Load<PackedScene>(path)
            ?? throw new InvalidOperationException($"Could not load scene: {path}");

        _activeScene = packed.Instantiate();
        AddChild(_activeScene);
        await WaitFrames(waitFrames);
        return _activeScene;
    }

    private async Task CleanupActiveScene()
    {
        if (_activeScene == null)
            return;

        RemoveChild(_activeScene);
        _activeScene.QueueFree();
        _activeScene = null;
        await WaitFrames(10);
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

    private async Task WaitSeconds(double seconds)
    {
        await ToSignal(GetTree().CreateTimer(seconds), Godot.Timer.SignalName.Timeout);
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

    private static void SetCurrentRoom(RogueCardGame.Core.Run.RunState run, RoomType type)
    {
        if (run.CurrentMap == null)
            throw new InvalidOperationException("Current run has no map.");

        var node = new MapNode(9000 + (int)type, 1, 0, type)
        {
            IsRevealed = true,
            IsVisited = true
        };
        run.CurrentMap.CurrentNode = node;
    }

    private void BackupUserSaves()
    {
        _savePath = Path.TrimEndingDirectorySeparator(ProjectSettings.GlobalizePath("user://saves/"));
        if (!Directory.Exists(_savePath))
            return;

        _saveBackupPath = Path.Combine(Path.GetTempPath(), $"roguecardgame-ui-smoke-{Guid.NewGuid():N}");
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
