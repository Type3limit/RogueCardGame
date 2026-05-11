using Godot;
using RogueCardGame.Core.Cards;
using RogueCardGame.Core.Characters;
using RogueCardGame.Core.Combat;
using RogueCardGame.Core.Events;
using RogueCardGame.Core.Map;
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace RogueCardGame;

/// <summary>
/// Headless full-act smoke check for Phase 3.
/// Run with:
/// Godot --headless --path . --scene res://scenes/testing/Phase3ActSmoke.tscn
/// </summary>
public partial class Phase3ActSmokeRunner : Node
{
    private const int SmokeSeed = 731919;
    private const int TargetAct = 1;

    private string _savePath = "";
    private string? _saveBackupPath;
    private int _assertionCount;
    private int _roomsVisited;
    private int _combatsWon;
    private int _rewardsHandled;
    private int _eventsHandled;
    private int _shopsHandled;
    private int _restsHandled;

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
            await RunActSmoke();
            await CleanupCurrentScene();
            await StopAudioPlayback();
            GD.Print($"[Phase3ActSmoke] PASS ({_assertionCount} checks, rooms={_roomsVisited}, combats={_combatsWon}, rewards={_rewardsHandled}, events={_eventsHandled}, shops={_shopsHandled}, rests={_restsHandled})");
            GetTree().Quit(0);
        }
        catch (Exception ex)
        {
            await CleanupCurrentScene();
            await StopAudioPlayback();
            GD.PrintErr($"[Phase3ActSmoke] FAIL: {ex.Message}");
            GD.PrintErr(ex.StackTrace ?? "");
            GetTree().Quit(1);
        }
        finally
        {
            RestoreUserSaves();
        }
    }

    private async Task RunActSmoke()
    {
        GD.Print("[Phase3ActSmoke] Starting full Act 1 smoke");
        await WaitFrames(2);

        var manager = GameManager.Instance
            ?? throw new InvalidOperationException("GameManager autoload is not available.");
        manager.StartNewRun(CardClass.Vanguard, SmokeSeed);
        var run = manager.CurrentRun
            ?? throw new InvalidOperationException("Smoke run was not created.");
        SeedSmokeSupplies(run);

        SceneManager.Instance.ChangeSceneImmediate(SceneManager.Scenes.Map);
        await WaitForScene("MapScene");

        while (run.CurrentAct == TargetAct && run.IsRunActive && _roomsVisited < 24)
        {
            var mapScene = await WaitForScene("MapScene");
            RequireNode<Control>(mapScene, "MapScroll/MapContainer");
            TryUseHealingPotion(run, 0.45f);

            var nextNode = ChooseNextNode(run);
            if (nextNode.Type == RoomType.Boss)
                PrepareForBossSmoke(run);
            _roomsVisited++;
            GD.Print($"[Phase3ActSmoke] Room {_roomsVisited}: {nextNode.Type} row={nextNode.Row} id={nextNode.Id} hp={run.Player.CurrentHp}/{run.Player.MaxHp} deck={run.MasterDeck.Count}");

            NavigateToRoom(run, nextNode);

            switch (nextNode.Type)
            {
                case RoomType.Combat:
                case RoomType.EliteCombat:
                case RoomType.Boss:
                    await HandleCombatRoom(run, nextNode);
                    break;
                case RoomType.Event:
                    await HandleEventRoom();
                    break;
                case RoomType.Shop:
                    await HandleShopRoom(run);
                    break;
                case RoomType.RestSite:
                    await HandleRestRoom(run);
                    break;
                case RoomType.Treasure:
                    await HandleRewardRoom(run);
                    break;
                default:
                    throw new InvalidOperationException($"Unhandled act smoke room type: {nextNode.Type}");
            }
        }

        Require(run.CurrentAct == TargetAct + 1, $"Full act smoke did not advance to Act {TargetAct + 1}. Current act: {run.CurrentAct}.");
        Require(run.CurrentSceneId == "map", "Full act smoke did not end on map scene id after boss reward.");
        Require(run.CurrentMap?.ActNumber == TargetAct + 1, "Full act smoke did not create the next act map.");
        Require(_roomsVisited >= 10, "Full act smoke visited too few rooms.");
        Require(_combatsWon >= 3, "Full act smoke won too few combats.");
        Require(_rewardsHandled >= _combatsWon, "Full act smoke did not handle combat rewards.");
    }

    private async Task HandleCombatRoom(RogueCardGame.Core.Run.RunState run, MapNode node)
    {
        var combatScene = await WaitForScene("CombatScene", 8.0);
        int cardsPlayed = AutoplayCombatToVictory(combatScene, node.Type == RoomType.Boss ? 40 : 24);
        _combatsWon++;

        Require(cardsPlayed > 0, "Act smoke combat did not play any cards.");
        var rewardScene = await WaitForScene("RewardScene", 8.0);
        Require(run.CurrentSceneId == "reward", "Combat victory did not persist reward scene id.");
        RequireNode<VBoxContainer>(rewardScene, "RewardList");
        await HandleRewardScene(run, rewardScene);
    }

    private async Task HandleRewardRoom(RogueCardGame.Core.Run.RunState run)
    {
        var rewardScene = await WaitForScene("RewardScene", 8.0);
        await HandleRewardScene(run, rewardScene);
    }

    private async Task HandleRewardScene(RogueCardGame.Core.Run.RunState run, Node rewardScene)
    {
        await WaitSeconds(0.75);
        await WaitFrames(4);

        var rewardList = RequireNode<VBoxContainer>(rewardScene, "RewardList");
        foreach (var rewardPanel in rewardList.GetChildren().OfType<PanelContainer>().ToList())
            Click(rewardPanel);

        int deckBefore = run.MasterDeck.Count;
        var cardPanel = ChooseRewardCardPanel(rewardScene);
        if (cardPanel != null)
        {
            Click(cardPanel);
            await WaitFrames(8);
            Require(run.MasterDeck.Count == deckBefore + 1, "Act smoke reward card selection did not add a card.");
        }

        _rewardsHandled++;
        var skipButton = RequireNode<Button>(rewardScene, "SkipBtn");
        Press(skipButton);
        await WaitForScene("MapScene", 8.0);
    }

    private async Task HandleEventRoom()
    {
        var eventScene = await WaitForScene("EventScene", 8.0);
        await WaitSeconds(0.75);
        await WaitFrames(4);

        var choices = RequireNode<VBoxContainer>(eventScene, "EventPanel/Content/ChoicesContainer");
        var choicePanel = ChooseEventChoicePanel(eventScene, choices);
        Require(choicePanel != null, "Act smoke event room did not expose an available choice.");

        Click(choicePanel!);
        await WaitSeconds(0.45);
        await WaitFrames(4);

        var continueButton = RequireNode<Button>(eventScene, "EventPanel/Content/ContinueBtn");
        Require(continueButton.Visible, "Act smoke event choice did not reveal continue button.");
        Press(continueButton);
        _eventsHandled++;
        await WaitForScene("MapScene", 8.0);
    }

    private async Task HandleShopRoom(RogueCardGame.Core.Run.RunState run)
    {
        var shopScene = await WaitForScene("ShopScene", 8.0);
        var cardRow = RequireNode<HBoxContainer>(shopScene, "ShopScroll/ShopContent/CardSection/CardRow");

        int deckBefore = run.MasterDeck.Count;
        int goldBefore = run.Gold;
        var cardPanel = cardRow.GetChildren().OfType<PanelContainer>().FirstOrDefault();
        if (cardPanel != null && run.Gold > 0)
        {
            Click(cardPanel);
            await WaitFrames(8);
            Require(run.MasterDeck.Count >= deckBefore, "Act smoke shop purchase reduced deck size unexpectedly.");
            if (run.MasterDeck.Count > deckBefore)
                Require(run.Gold < goldBefore, "Act smoke shop purchase added a card without spending gold.");
        }

        _shopsHandled++;
        var leaveButton = RequireNode<Button>(shopScene, "LeaveBtn");
        Press(leaveButton);
        await WaitForScene("MapScene", 8.0);
    }

    private async Task HandleRestRoom(RogueCardGame.Core.Run.RunState run)
    {
        var restScene = await WaitForScene("RestScene", 8.0);
        var restButton = RequireNode<Button>(restScene, "OptionsContainer/RestBtn");
        int hpBefore = run.Player.CurrentHp;
        Press(restButton);
        await WaitFrames(6);
        Require(run.Player.CurrentHp >= hpBefore, "Act smoke rest room reduced player HP.");
        SeedSmokeSupplies(run);
        _restsHandled++;
        await WaitForScene("MapScene", 8.0);
    }

    private int AutoplayCombatToVictory(Node combatScene, int turnLimit)
    {
        var combat = GetCombatManager(combatScene);
        var refreshAll = combatScene.GetType().GetMethod("RefreshAll", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("CombatScene.RefreshAll method was not found.");
        var processResult = combatScene.GetType().GetMethod("ProcessCombatResult", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("CombatScene.ProcessCombatResult method was not found.");

        Require(combat.Enemies.Count > 0, "Act smoke combat did not create enemies.");

        int cardsPlayed = 0;
        int turnsTaken = 0;
        int bossEmergencyRefills = 0;
        var run = GameManager.Instance.CurrentRun
            ?? throw new InvalidOperationException("Act smoke has no current run during combat.");
        bool isBossCombat = run.CurrentMap?.CurrentNode?.Type == RoomType.Boss;
        while (combat.IsActive && turnsTaken < turnLimit)
        {
            var player = combat.Players.FirstOrDefault(p => p.IsAlive && !p.IsDowned)
                ?? throw new InvalidOperationException("Act smoke combat has no active player.");
            var deck = combat.PlayerDecks.GetValueOrDefault(player.Id)
                ?? throw new InvalidOperationException("Act smoke combat has no player deck.");

            if (isBossCombat && player.CurrentHp <= player.MaxHp * 0.38f && !HasHealingPotion(run) && bossEmergencyRefills < 2)
            {
                SeedSmokeSupplies(run);
                bossEmergencyRefills++;
                GD.Print($"[Phase3ActSmoke] Boss emergency potion refill #{bossEmergencyRefills}");
            }
            TryUseHealingPotion(run, 0.38f);

            bool playedAny = true;
            while (combat.IsActive && playedAny)
            {
                playedAny = false;
                foreach (var card in OrderAutoplayHand(combat, player, deck.Hand))
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

        Require(combat.Players.Any(player => player.IsAlive && !player.IsDowned), "Act smoke player died during combat.");
        Require(combat.Enemies.All(enemy => !enemy.IsAlive), $"Act smoke combat did not defeat all enemies within {turnLimit} turns.");
        processResult.Invoke(combatScene, null);
        return cardsPlayed;
    }

    private static void SeedSmokeSupplies(RogueCardGame.Core.Run.RunState run)
    {
        var repairPotion = run.PotionDb.GetPotion("repair_nano");
        if (repairPotion == null)
            return;

        while (run.Potions.HasEmptySlot())
            run.Potions.TryAddPotion(repairPotion);
    }

    private static void PrepareForBossSmoke(RogueCardGame.Core.Run.RunState run)
    {
        for (int i = 0; i < run.Potions.MaxSlots; i++)
            run.Potions.DiscardPotion(i);

        SeedSmokeSupplies(run);
        while (TryUseHealingPotion(run, 0.82f))
        {
        }
    }

    private static bool TryUseHealingPotion(RogueCardGame.Core.Run.RunState run, float hpRatioThreshold)
    {
        if (run.Player.CurrentHp > run.Player.MaxHp * hpRatioThreshold)
            return false;

        for (int i = 0; i < run.Potions.Slots.Count; i++)
        {
            var potion = run.Potions.Slots[i];
            if (potion == null)
                continue;

            if (!potion.EffectType.Equals("healPercent", StringComparison.OrdinalIgnoreCase)
                && !potion.EffectType.Equals("heal", StringComparison.OrdinalIgnoreCase))
                continue;

            var used = run.Potions.UsePotion(i);
            if (used == null)
                return false;

            int healAmount = used.EffectType.Equals("healPercent", StringComparison.OrdinalIgnoreCase)
                ? Math.Max(1, (int)(run.Player.MaxHp * used.Value / 100f))
                : used.Value;
            run.Player.Heal(healAmount);
            GD.Print($"[Phase3ActSmoke] Used potion {used.Id}, healed {healAmount}, hp={run.Player.CurrentHp}/{run.Player.MaxHp}");
            return true;
        }

        return false;
    }

    private static bool HasHealingPotion(RogueCardGame.Core.Run.RunState run)
    {
        return run.Potions.Slots.Any(potion => potion != null
            && (potion.EffectType.Equals("healPercent", StringComparison.OrdinalIgnoreCase)
                || potion.EffectType.Equals("heal", StringComparison.OrdinalIgnoreCase)));
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
            .OrderByDescending(GetTargetPriority)
            .FirstOrDefault();
    }

    private static double GetTargetPriority(Combatant target)
    {
        if (target is not Enemy enemy)
            return 0;

        int incomingDamage = enemy.CurrentIntent?.Type is EnemyIntentType.Attack or EnemyIntentType.AttackDefend or EnemyIntentType.Special
            ? enemy.CurrentIntent.Value * Math.Max(1, enemy.CurrentIntent.HitCount)
            : 0;

        int supportThreat = enemy.CurrentIntent?.Type is EnemyIntentType.Heal or EnemyIntentType.Buff or EnemyIntentType.Summon ? 8 : 0;
        return incomingDamage * 6.0 + supportThreat + enemy.MaxHp * 0.1 - enemy.CurrentHp * 0.35;
    }

    private static List<Card> OrderAutoplayHand(CombatManager combat, PlayerCharacter player, IReadOnlyList<Card> hand)
    {
        int incomingDamage = combat.Enemies
            .Where(enemy => enemy.IsAlive)
            .Select(enemy => enemy.CurrentIntent)
            .Where(intent => intent != null)
            .Sum(intent => intent!.Type is EnemyIntentType.Attack or EnemyIntentType.AttackDefend or EnemyIntentType.Special
                ? intent.Value * Math.Max(1, intent.HitCount)
                : 0);

        bool needsBlock = player.CurrentHp <= incomingDamage + 12 || player.CurrentHp <= player.MaxHp * 0.35f;

        return hand
            .OrderByDescending(card => needsBlock && card.Data.Type == CardType.Skill)
            .ThenByDescending(card => !needsBlock && card.Data.Type == CardType.Attack)
            .ThenBy(card => card.EffectiveCost)
            .ToList();
    }

    private static PanelContainer? ChooseRewardCardPanel(Node rewardScene)
    {
        var cardArea = rewardScene.GetNodeOrNull<HBoxContainer>("CardRewardArea");
        if (cardArea == null)
            return null;

        var panels = cardArea.GetChildren().OfType<PanelContainer>().ToList();
        if (panels.Count == 0)
            return null;

        var rewardDefsField = rewardScene.GetType().GetField("_rewardCardDefs", BindingFlags.Instance | BindingFlags.NonPublic);
        if (rewardDefsField?.GetValue(rewardScene) is IDictionary rewardDefs)
        {
            var attackPanel = panels
                .Select(panel => new { Panel = panel, Data = rewardDefs[panel] as CardData })
                .Where(entry => entry.Data != null)
                .OrderByDescending(entry => entry.Data!.Type == CardType.Attack)
                .ThenByDescending(entry => entry.Data!.Rarity)
                .Select(entry => entry.Panel)
                .FirstOrDefault();

            if (attackPanel != null)
                return attackPanel;
        }

        return panels.FirstOrDefault();
    }

    private static PanelContainer? ChooseEventChoicePanel(Node eventScene, VBoxContainer choices)
    {
        var panels = choices.GetChildren().OfType<PanelContainer>().ToList();
        if (panels.Count == 0)
            return null;

        var available = panels
            .Select((panel, index) => new { Panel = panel, Index = index })
            .Where(entry => entry.Panel.MouseDefaultCursorShape == Control.CursorShape.PointingHand)
            .ToList();
        if (available.Count == 0)
            return null;

        var currentEventField = eventScene.GetType().GetField("_currentEvent", BindingFlags.Instance | BindingFlags.NonPublic);
        if (currentEventField?.GetValue(eventScene) is EventData eventData)
        {
            return available
                .OrderByDescending(entry => ScoreEventChoice(eventData.Choices[entry.Index]))
                .Select(entry => entry.Panel)
                .FirstOrDefault();
        }

        return available.First().Panel;
    }

    private static double ScoreEventChoice(EventChoice choice)
    {
        double score = 0;
        foreach (var outcome in choice.Outcomes)
        {
            double probability = Math.Clamp(outcome.Probability, 0f, 1f);
            score += probability * (outcome.Effect switch
            {
                EventChoiceEffect.GainHp => outcome.Value * 2.0,
                EventChoiceEffect.HealPercent => outcome.Value * 120.0,
                EventChoiceEffect.GainMaxHp => outcome.Value * 3.0,
                EventChoiceEffect.LoseHp => -outcome.Value * 3.0,
                EventChoiceEffect.LoseHpPercent => -outcome.Value * 180.0,
                EventChoiceEffect.GainGold => outcome.Value * 0.08,
                EventChoiceEffect.LoseGold => -outcome.Value * 0.03,
                EventChoiceEffect.GainCard => 10.0 + outcome.Value,
                EventChoiceEffect.UpgradeCard => 14.0 + outcome.Value,
                EventChoiceEffect.GainImplant => 18.0 + outcome.Value,
                EventChoiceEffect.GainPotion => 8.0 + outcome.Value,
                EventChoiceEffect.RemoveCard => 3.0,
                EventChoiceEffect.LosePotion => -4.0,
                EventChoiceEffect.Nothing => 0.0,
                _ => 0.0
            });
        }

        if (choice.Outcomes.Count == 1 && choice.Outcomes[0].Effect == EventChoiceEffect.Nothing)
            score += 1.0;

        return score;
    }

    private static MapNode ChooseNextNode(RogueCardGame.Core.Run.RunState run)
    {
        var map = run.CurrentMap
            ?? throw new InvalidOperationException("Act smoke run has no current map.");
        var reachable = map.GetReachableNodes()
            .Where(node => !node.IsVisited)
            .ToList();
        if (reachable.Count == 0)
            throw new InvalidOperationException("Act smoke map has no reachable nodes.");

        if (reachable.Any(node => node.Type == RoomType.Boss))
            return reachable.First(node => node.Type == RoomType.Boss);

        bool wantsRecovery = run.Player.CurrentHp <= run.Player.MaxHp * 0.65f;
        if (wantsRecovery && reachable.Any(node => node.Type == RoomType.RestSite))
            return reachable.First(node => node.Type == RoomType.RestSite);

        return reachable
            .OrderBy(node => GetRoomPriority(node, wantsRecovery))
            .ThenBy(node => node.Column)
            .First();
    }

    private static int GetRoomPriority(MapNode node, bool wantsRecovery) => (node.Type, wantsRecovery) switch
    {
        (RoomType.RestSite, true) => 0,
        (RoomType.Event, true) => 1,
        (RoomType.Shop, true) => 2,
        (RoomType.Treasure, true) => 3,
        (RoomType.Combat, true) => 4,
        (RoomType.EliteCombat, true) => 5,
        (RoomType.Boss, _) => 6,
        _ => GetDefaultRoomPriority(node)
    };

    private static int GetDefaultRoomPriority(MapNode node) => node.Type switch
    {
        RoomType.Combat => 0,
        RoomType.Event => 1,
        RoomType.Shop => 2,
        RoomType.RestSite => 3,
        RoomType.Treasure => 4,
        RoomType.EliteCombat => 5,
        RoomType.Boss => 6,
        _ => 9
    };

    private static void NavigateToRoom(RogueCardGame.Core.Run.RunState run, MapNode node)
    {
        run.CurrentMap?.MoveTo(node.Id);

        switch (node.Type)
        {
            case RoomType.Combat:
            case RoomType.EliteCombat:
            case RoomType.Boss:
                GameManager.Instance.SetCurrentRunScene("combat");
                SceneManager.Instance.ChangeScene(SceneManager.Scenes.Combat);
                break;
            case RoomType.RestSite:
                GameManager.Instance.SetCurrentRunScene("rest");
                SceneManager.Instance.ChangeScene(SceneManager.Scenes.Rest);
                break;
            case RoomType.Shop:
                GameManager.Instance.SetCurrentRunScene("shop");
                SceneManager.Instance.ChangeScene(SceneManager.Scenes.Shop);
                break;
            case RoomType.Event:
                GameManager.Instance.SetCurrentRunScene("event");
                SceneManager.Instance.ChangeScene(SceneManager.Scenes.Event);
                break;
            case RoomType.Treasure:
                GameManager.Instance.SetCurrentRunScene("reward");
                SceneManager.Instance.ChangeScene(SceneManager.Scenes.Reward);
                break;
            default:
                throw new InvalidOperationException($"Cannot navigate act smoke to room type {node.Type}.");
        }
    }

    private void KeepRunnerAliveAcrossSceneChanges()
    {
        if (GetTree().CurrentScene != this)
            return;

        var host = new Node { Name = "Phase3ActSmokeSceneHost" };
        GetTree().Root.AddChild(host);
        GetTree().CurrentScene = host;
    }

    private async Task<Node> WaitForScene(string expectedName, double timeoutSeconds = 5.0)
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

    private async Task WaitForSceneManagerIdle(double timeoutSeconds = 4.0)
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
        await StopAudioPlayback();

        var currentScene = GetTree().CurrentScene;
        if (currentScene != null && currentScene != this)
        {
            GetTree().CurrentScene = null;
            currentScene.QueueFree();
            await WaitFrames(10);
        }

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

        await WaitFrames(8);
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

    private void BackupUserSaves()
    {
        _savePath = Path.TrimEndingDirectorySeparator(ProjectSettings.GlobalizePath("user://saves/"));
        if (!Directory.Exists(_savePath))
            return;

        _saveBackupPath = Path.Combine(Path.GetTempPath(), $"roguecardgame-act-smoke-{Guid.NewGuid():N}");
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
