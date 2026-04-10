using Godot;
using RogueCardGame.Core;
using RogueCardGame.Core.Cards;
using RogueCardGame.Core.Characters;
using RogueCardGame.Core.Combat;
using RogueCardGame.Core.Deck;

namespace RogueCardGame.UI;

/// <summary>
/// Main combat scene controller. This is the top-level Godot node
/// that orchestrates the combat UI and connects it to the core logic.
/// Attach this script to the root node of the CombatScene.tscn.
/// </summary>
public partial class CombatScene : Node2D
{
    private CombatManager? _combat;
    private PlayerCharacter? _player;
    private CardDatabase _cardDb = new();

    // UI references
    private CombatHUD? _hud;
    private HandDisplay? _handDisplay;
    private FormationUI? _formationUI;
    private Label? _messageLabel;
    private Button? _endTurnButton;
    private Button? _switchRowButton;

    // Target selection state
    private Card? _pendingCard;
    private bool _awaitingTarget;

    public override void _Ready()
    {
        _hud = GetNodeOrNull<CombatHUD>("CombatHUD");
        _handDisplay = GetNodeOrNull<HandDisplay>("HandDisplay");
        _formationUI = GetNodeOrNull<FormationUI>("FormationUI");
        _messageLabel = GetNodeOrNull<Label>("MessageLabel");
        _endTurnButton = GetNodeOrNull<Button>("EndTurnButton");
        _switchRowButton = GetNodeOrNull<Button>("SwitchRowButton");

        if (_endTurnButton != null)
            _endTurnButton.Pressed += OnEndTurnPressed;
        if (_switchRowButton != null)
            _switchRowButton.Pressed += OnSwitchRowPressed;
        if (_handDisplay != null)
            _handDisplay.CardSelected += OnCardSelected;

        // Load card data
        string cardsPath = ProjectSettings.GlobalizePath("res://data/cards/");
        if (DirAccess.DirExistsAbsolute(cardsPath))
            _cardDb.LoadFromDirectory(cardsPath);

        StartTestCombat();
    }

    private void StartTestCombat()
    {
        _player = PlayerCharacter.CreateVanguard("先锋");

        // Build starter deck
        var starterCards = new List<Card>();
        var starterData = _cardDb.GetStarterDeck(CardClass.Vanguard);
        if (starterData.Count > 0)
        {
            foreach (var data in starterData)
            {
                int copies = data.Rarity == CardRarity.Starter ? 3 : 1;
                for (int i = 0; i < copies; i++)
                    starterCards.Add(new Card(data));
            }
        }
        else
        {
            starterCards = CreateFallbackDeck();
        }

        // Create test enemies
        var enemies = CreateTestEnemies();

        // Setup combat
        _combat = new CombatManager(GD.Randi().GetHashCode());
        var playerDecks = new Dictionary<int, List<Card>>
        {
            [_player.Id] = starterCards
        };

        _combat.Initialize([_player], enemies, playerDecks);
        _combat.OnCombatEnded += OnCombatEnded;
        _combat.OnCardPlayed += OnCardPlayedEvent;
        _combat.OnEnemyAction += OnEnemyActionEvent;

        // Bind UI
        _hud?.BindCombat(_combat, _player);
        _handDisplay?.BindCombat(_combat, _player);
        _formationUI?.BindCombat(_combat);

        // Start!
        _combat.StartCombat();
        RefreshUI();
        ShowMessage("战斗开始！");
    }

    private void OnCardSelected(int cardInstanceId)
    {
        if (_combat == null || _player == null) return;

        var deck = _combat.PlayerDecks.GetValueOrDefault(_player.Id);
        if (deck == null) return;

        var card = deck.Hand.FirstOrDefault(c => c.InstanceId == cardInstanceId);
        if (card == null) return;

        if (!_player.CanPlayCard(card))
        {
            ShowMessage("能量不足！");
            _handDisplay?.DeselectCard();
            return;
        }

        // Check if card needs a target
        if (card.Data.TargetType == TargetType.SingleEnemy)
        {
            _pendingCard = card;
            _awaitingTarget = true;
            ShowMessage("选择一个敌人作为目标...");
            return;
        }

        // Auto-target cards (self, all enemies, etc.)
        PlayCard(card, null);
    }

    public override void _Input(InputEvent @event)
    {
        if (_awaitingTarget && @event is InputEventMouseButton mb
            && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
        {
            // Simple target selection: click on enemy area
            // In a full implementation, this would raycast to enemy UI nodes
            if (_pendingCard != null && _combat != null)
            {
                var aliveEnemies = _combat.Enemies.Where(e => e.IsAlive).ToList();
                if (aliveEnemies.Count > 0)
                {
                    // For now, target the first alive enemy
                    PlayCard(_pendingCard, aliveEnemies[0]);
                }
            }
            _awaitingTarget = false;
            _pendingCard = null;
        }

        // Keyboard shortcuts
        if (@event is InputEventKey key && key.Pressed)
        {
            if (key.Keycode == Key.E)
                OnEndTurnPressed();
            else if (key.Keycode == Key.R)
                OnSwitchRowPressed();
        }
    }

    private void PlayCard(Card card, Combatant? target)
    {
        if (_combat == null || _player == null) return;

        bool success = _combat.TryPlayCard(_player, card, target);
        if (success)
        {
            ShowMessage($"打出: {card.DisplayName}");
        }
        else
        {
            ShowMessage("无法打出此牌！");
        }

        _handDisplay?.DeselectCard();
        RefreshUI();
    }

    private void OnEndTurnPressed()
    {
        if (_combat == null || !_combat.IsActive) return;

        _combat.EndPlayerTurn();
        RefreshUI();
        ShowMessage($"回合 {_combat.TurnSystem.TurnNumber}");
    }

    private void OnSwitchRowPressed()
    {
        if (_combat == null || _player == null) return;

        bool switched = _combat.TrySwitchRow(_player);
        if (switched)
        {
            var newRow = _combat.Formation.GetPosition(_player.Id);
            ShowMessage($"移动至{(newRow == FormationRow.Front ? "前排" : "后排")}");
        }
        else
        {
            ShowMessage("本回合已移动过！");
        }
        RefreshUI();
    }

    private void OnCombatEnded(bool victory)
    {
        ShowMessage(victory ? "🎉 战斗胜利！" : "💀 战斗失败...");
        if (_endTurnButton != null) _endTurnButton.Disabled = true;
    }

    private void OnCardPlayedEvent(PlayerCharacter player, Card card)
    {
        GD.Print($"[Combat] {player.Name} played {card.DisplayName}");
    }

    private void OnEnemyActionEvent(Enemy enemy, EnemyIntent intent)
    {
        GD.Print($"[Combat] {enemy.Name}: {intent.Description}");
    }

    private void RefreshUI()
    {
        _hud?.RefreshAll();
        _handDisplay?.RefreshHand();
        _formationUI?.RefreshFormation();
    }

    private void ShowMessage(string text)
    {
        if (_messageLabel != null)
            _messageLabel.Text = text;
        GD.Print($"[Message] {text}");
    }

    private List<Enemy> CreateTestEnemies()
    {
        return
        [
            new(new EnemyData
            {
                Id = "sentry",
                Name = "重装哨兵",
                MaxHp = 45,
                PreferredRow = FormationRow.Front,
                IntentPatterns =
                [
                    new() { Type = EnemyIntentType.Attack, Value = 8, Scope = TargetScope.SingleFront, Weight = 2, Description = "攻击 8" },
                    new() { Type = EnemyIntentType.Defend, Value = 8, Scope = TargetScope.Self, Weight = 1, Description = "防御 8" },
                    new() { Type = EnemyIntentType.Attack, Value = 5, HitCount = 2, Scope = TargetScope.SingleFront, Weight = 1, Description = "连击 5×2" }
                ]
            }),
            new(new EnemyData
            {
                Id = "drone",
                Name = "攻击无人机",
                MaxHp = 28,
                PreferredRow = FormationRow.Back,
                IsHackable = true,
                HackThreshold = 8,
                IntentPatterns =
                [
                    new() { Type = EnemyIntentType.Attack, Value = 6, Scope = TargetScope.SingleAny, Weight = 2, Description = "射击 6" },
                    new() { Type = EnemyIntentType.Buff, Value = 1, Scope = TargetScope.Self, Weight = 1, Description = "强化 (力量+1)" }
                ]
            })
        ];
    }

    private static List<Card> CreateFallbackDeck()
    {
        var strike = new CardData
        {
            Id = "basic_strike", Name = "斩击",
            Class = CardClass.Neutral, Rarity = CardRarity.Starter,
            Type = CardType.Attack, Cost = 1, Range = CardRange.Melee,
            TargetType = TargetType.SingleEnemy, Description = "造成 6 点伤害。",
            Effects = [new CardEffectData { Type = "damage", Value = 6 }]
        };
        var defend = new CardData
        {
            Id = "basic_defend", Name = "防御",
            Class = CardClass.Neutral, Rarity = CardRarity.Starter,
            Type = CardType.Skill, Cost = 1, Range = CardRange.None,
            TargetType = TargetType.Self, Description = "获得 5 点护甲。",
            Effects = [new CardEffectData { Type = "block", Value = 5 }]
        };

        var deck = new List<Card>();
        for (int i = 0; i < 5; i++) deck.Add(new Card(strike));
        for (int i = 0; i < 4; i++) deck.Add(new Card(defend));
        return deck;
    }
}
