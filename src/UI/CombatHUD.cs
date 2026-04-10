using Godot;
using RogueCardGame.Core.Cards;
using RogueCardGame.Core.Characters;
using RogueCardGame.Core.Combat;

namespace RogueCardGame.UI;

/// <summary>
/// Main combat HUD that displays all combat information:
/// player stats, enemy display, hand, energy, formation indicators.
/// </summary>
public partial class CombatHUD : Control
{
    // Player stats
    private Label? _playerHpLabel;
    private ProgressBar? _playerHpBar;
    private Label? _playerBlockLabel;
    private Label? _playerEnergyLabel;
    private Label? _playerClassResource;
    private Label? _turnLabel;

    // Formation
    private Label? _formationLabel;
    private Button? _switchRowButton;

    // Hand display
    private HBoxContainer? _handContainer;

    // Enemy display
    private VBoxContainer? _frontEnemyContainer;
    private VBoxContainer? _backEnemyContainer;

    // Action buttons
    private Button? _endTurnButton;

    // Game state reference
    private CombatManager? _combat;
    private PlayerCharacter? _activePlayer;

    [Signal] public delegate void EndTurnPressedEventHandler();
    [Signal] public delegate void SwitchRowPressedEventHandler();

    // C# event fallback
    public event Action? EndTurnPressed;
    public event Action? SwitchRowPressed;

    public override void _Ready()
    {
        _playerHpLabel = GetNodeOrNull<Label>("%PlayerHPLabel");
        _playerHpBar = GetNodeOrNull<ProgressBar>("%PlayerHPBar");
        _playerBlockLabel = GetNodeOrNull<Label>("%PlayerBlockLabel");
        _playerEnergyLabel = GetNodeOrNull<Label>("%EnergyLabel");
        _playerClassResource = GetNodeOrNull<Label>("%ClassResourceLabel");
        _turnLabel = GetNodeOrNull<Label>("%TurnLabel");
        _formationLabel = GetNodeOrNull<Label>("%FormationLabel");
        _switchRowButton = GetNodeOrNull<Button>("%SwitchRowButton");
        _handContainer = GetNodeOrNull<HBoxContainer>("%HandContainer");
        _frontEnemyContainer = GetNodeOrNull<VBoxContainer>("%FrontEnemyContainer");
        _backEnemyContainer = GetNodeOrNull<VBoxContainer>("%BackEnemyContainer");
        _endTurnButton = GetNodeOrNull<Button>("%EndTurnButton");

        if (_endTurnButton != null)
            _endTurnButton.Pressed += () => EndTurnPressed?.Invoke();
        if (_switchRowButton != null)
            _switchRowButton.Pressed += () => SwitchRowPressed?.Invoke();
    }

    public void BindCombat(CombatManager combat, PlayerCharacter player)
    {
        _combat = combat;
        _activePlayer = player;
        RefreshAll();
    }

    public void RefreshAll()
    {
        RefreshPlayerStats();
        RefreshEnemyDisplay();
        RefreshFormation();
        RefreshTurnInfo();
    }

    public void RefreshPlayerStats()
    {
        if (_activePlayer == null) return;

        if (_playerHpLabel != null)
            _playerHpLabel.Text = $"{_activePlayer.CurrentHp}/{_activePlayer.MaxHp}";
        if (_playerHpBar != null)
        {
            _playerHpBar.MaxValue = _activePlayer.MaxHp;
            _playerHpBar.Value = _activePlayer.CurrentHp;
        }
        if (_playerBlockLabel != null)
            _playerBlockLabel.Text = _activePlayer.Block > 0
                ? $"🛡️{_activePlayer.Block}" : "";
        if (_playerEnergyLabel != null)
            _playerEnergyLabel.Text = $"⚡{_activePlayer.CurrentEnergy}/{_activePlayer.MaxEnergy}";
        if (_playerClassResource != null)
        {
            var resName = _activePlayer.ClassResourceName;
            var resValue = _activePlayer.ClassResourceValue;
            _playerClassResource.Text = resValue > 0 ? $"{resName}: {resValue}" : "";
        }
    }

    public void RefreshEnemyDisplay()
    {
        if (_combat == null) return;

        // Clear existing enemy displays
        ClearChildren(_frontEnemyContainer);
        ClearChildren(_backEnemyContainer);

        foreach (var enemy in _combat.Enemies)
        {
            if (!enemy.IsAlive) continue;

            var label = new Label();
            label.Text = FormatEnemyInfo(enemy);

            var row = _combat.Formation.GetPosition(enemy.Id);
            if (row == FormationRow.Front)
                _frontEnemyContainer?.AddChild(label);
            else
                _backEnemyContainer?.AddChild(label);
        }
    }

    public void RefreshFormation()
    {
        if (_activePlayer == null || _combat == null) return;

        var row = _combat.Formation.GetPosition(_activePlayer.Id);
        if (_formationLabel != null)
            _formationLabel.Text = row == FormationRow.Front ? "📍前排" : "📍后排";
        if (_switchRowButton != null)
            _switchRowButton.Text = row == FormationRow.Front ? "🔄 移至后排" : "🔄 移至前排";
    }

    public void RefreshTurnInfo()
    {
        if (_combat == null) return;
        if (_turnLabel != null)
            _turnLabel.Text = $"回合 {_combat.TurnSystem.TurnNumber}";
    }

    private string FormatEnemyInfo(Enemy enemy)
    {
        var intent = enemy.CurrentIntent;
        var intentStr = intent != null ? $" [{intent.Description ?? intent.Type.ToString()}]" : "";
        var hackStr = "";

        if (enemy.CanBeHacked())
        {
            var progress = enemy.StatusEffects.GetStacks(StatusType.HackProgress);
            if (progress > 0)
                hackStr = $" 💻{progress}/{enemy.Data.HackThreshold}";
        }
        if (enemy.IsHacked)
            hackStr = " 💻已入侵";

        return $"{enemy.Name} HP:{enemy.CurrentHp}/{enemy.MaxHp}" +
            (enemy.Block > 0 ? $" 🛡️{enemy.Block}" : "") +
            intentStr + hackStr;
    }

    private static void ClearChildren(Node? parent)
    {
        if (parent == null) return;
        foreach (var child in parent.GetChildren())
            child.QueueFree();
    }
}
