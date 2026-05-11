using Godot;
using RogueCardGame.Core.Cards;
using RogueCardGame.Core.Map;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace RogueCardGame;

/// <summary>
/// Headless interaction checks for Phase 3 branch scenes.
/// Run with:
/// Godot --headless --path . --scene res://scenes/testing/Phase3InteractionSmoke.tscn
/// </summary>
public partial class Phase3InteractionSmokeRunner : Node
{
    private const int SmokeSeed = 820451;

    private Node? _activeScene;
    private string _savePath = "";
    private string? _saveBackupPath;
    private int _assertionCount;

    public override async void _Ready()
    {
        try
        {
            BackupUserSaves();
            await RunInteractionChecks();
            await CleanupActiveScene();
            await StopAudioPlayback();
            GD.Print($"[Phase3InteractionSmoke] PASS ({_assertionCount} checks)");
            GetTree().Quit(0);
        }
        catch (Exception ex)
        {
            await CleanupActiveScene();
            await StopAudioPlayback();
            GD.PrintErr($"[Phase3InteractionSmoke] FAIL: {ex.Message}");
            GD.PrintErr(ex.StackTrace ?? "");
            GetTree().Quit(1);
        }
        finally
        {
            RestoreUserSaves();
        }
    }

    private async Task RunInteractionChecks()
    {
        await WaitFrames(2);

        await CheckRewardCardSelection();
        await CheckShopCardPurchase();
        await CheckEventChoiceResolution();
        await CheckRestUpgrade();
        await CheckRestHeal();
    }

    private async Task CheckRewardCardSelection()
    {
        var run = StartSmokeRun(SmokeSeed + 1);
        SetCurrentRoom(run, RoomType.Combat);
        run.OnCombatVictory(wasElite: false, wasBoss: false);

        var scene = await LoadScene("res://scenes/reward/Reward.tscn", 10);
        var cardRewardArea = RequireNode<HBoxContainer>(scene, "CardRewardArea");
        var skipButton = RequireNode<Button>(scene, "SkipBtn");
        int deckBefore = run.MasterDeck.Count;

        var rewardCard = cardRewardArea.GetChildren().OfType<PanelContainer>().FirstOrDefault();
        Require(rewardCard != null, "Reward scene did not expose a card reward panel.");
        Click(rewardCard!);
        await WaitFrames(6);

        Require(run.MasterDeck.Count == deckBefore + 1, "Clicking a reward card did not add it to the master deck.");
        Require(skipButton.Text.Contains("返回地图", StringComparison.Ordinal), "Reward skip button did not switch to return-map state after choosing a card.");
    }

    private async Task CheckShopCardPurchase()
    {
        var run = StartSmokeRun(SmokeSeed + 2);
        run.Gold = 500;

        var scene = await LoadScene("res://scenes/shop/Shop.tscn", 10);
        var cardRow = RequireNode<HBoxContainer>(scene, "ShopScroll/ShopContent/CardSection/CardRow");
        int deckBefore = run.MasterDeck.Count;
        int goldBefore = run.Gold;

        var shopCard = cardRow.GetChildren().OfType<PanelContainer>().FirstOrDefault();
        Require(shopCard != null, "Shop scene did not expose a card purchase panel.");
        Click(shopCard!);
        await WaitFrames(8);

        Require(run.MasterDeck.Count == deckBefore + 1, "Buying a shop card did not add it to the master deck.");
        Require(run.Gold < goldBefore, "Buying a shop card did not spend gold.");
    }

    private async Task CheckEventChoiceResolution()
    {
        var run = StartSmokeRun(SmokeSeed + 3);
        run.Gold = 500;

        var scene = await LoadScene("res://scenes/event/Event.tscn", 5);
        await WaitSeconds(0.75);
        await WaitFrames(4);

        var choices = RequireNode<VBoxContainer>(scene, "EventPanel/Content/ChoicesContainer");
        var resultLabel = RequireNode<RichTextLabel>(scene, "EventPanel/Content/ResultLabel");
        var continueButton = RequireNode<Button>(scene, "EventPanel/Content/ContinueBtn");
        var choicePanel = choices.GetChildren().OfType<PanelContainer>()
            .FirstOrDefault(panel => panel.MouseDefaultCursorShape == Control.CursorShape.PointingHand);

        Require(choicePanel != null, "Event scene did not expose an available choice panel.");
        Click(choicePanel!);
        await WaitSeconds(0.45);
        await WaitFrames(4);

        Require(resultLabel.Visible, "Choosing an event option did not reveal the result label.");
        Require(continueButton.Visible, "Choosing an event option did not reveal the continue button.");
        Require(!string.IsNullOrWhiteSpace(resultLabel.Text), "Event result text is empty after resolving a choice.");
    }

    private async Task CheckRestUpgrade()
    {
        var run = StartSmokeRun(SmokeSeed + 4);

        var scene = await LoadScene("res://scenes/rest/Rest.tscn", 5);
        var upgradeButton = RequireNode<Button>(scene, "OptionsContainer/UpgradeBtn");
        Press(upgradeButton);
        await WaitSeconds(0.35);
        await WaitFrames(4);

        var cardList = RequireNode<VBoxContainer>(scene, "CardListContainer");
        Require(cardList.Visible, "Pressing rest upgrade did not reveal the card list.");

        var upgradeCardPanel = EnumerateDescendants(cardList).OfType<PanelContainer>().FirstOrDefault();
        Require(upgradeCardPanel != null, "Rest upgrade card list did not expose an upgradeable card panel.");
        Click(upgradeCardPanel!);
        await WaitFrames(10);

        var confirmButton = EnumerateDescendants(scene).OfType<Button>()
            .FirstOrDefault(button =>
                button.Text.Contains("确认升级", StringComparison.Ordinal)
                || button.Text.Contains("选择路线 A", StringComparison.Ordinal));
        Require(confirmButton != null, "Rest upgrade confirmation modal did not expose a confirm button.");

        Press(confirmButton!);
        await WaitFrames(6);

        Require(run.MasterDeck.Any(card => card.IsUpgraded), "Confirming a rest upgrade did not upgrade a card.");
    }

    private async Task CheckRestHeal()
    {
        var run = StartSmokeRun(SmokeSeed + 5);
        run.Player.CurrentHp = Math.Max(1, run.Player.MaxHp / 2);
        int hpBefore = run.Player.CurrentHp;

        var scene = await LoadScene("res://scenes/rest/Rest.tscn", 5);
        var restButton = RequireNode<Button>(scene, "OptionsContainer/RestBtn");
        Press(restButton);
        await WaitFrames(6);

        Require(run.Player.CurrentHp > hpBefore, "Pressing rest did not heal the player.");
    }

    private RogueCardGame.Core.Run.RunState StartSmokeRun(int seed)
    {
        var manager = GameManager.Instance
            ?? throw new InvalidOperationException("GameManager autoload is not available.");
        manager.StartNewRun(CardClass.Vanguard, seed);
        return manager.CurrentRun
            ?? throw new InvalidOperationException("Smoke run was not created.");
    }

    private async Task<Node> LoadScene(string path, int waitFrames)
    {
        if (_activeScene != null)
            await CleanupActiveScene();

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

        await StopAudioPlayback();
        RemoveChild(_activeScene);
        _activeScene.QueueFree();
        _activeScene = null;
        await WaitFrames(10);
        await StopAudioPlayback();
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

        await WaitFrames(12);
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

        run.CurrentMap.CurrentNode = new MapNode(9500 + (int)type, 1, 0, type)
        {
            IsRevealed = true,
            IsVisited = true
        };
    }

    private static void Press(Button button)
    {
        button.EmitSignal(BaseButton.SignalName.Pressed);
    }

    private static void Click(Control control)
    {
        control.EmitSignal(Control.SignalName.GuiInput, new InputEventMouseButton
        {
            ButtonIndex = MouseButton.Left,
            Pressed = true,
            Position = Vector2.Zero
        });
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

        _saveBackupPath = Path.Combine(Path.GetTempPath(), $"roguecardgame-interaction-smoke-{Guid.NewGuid():N}");
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
