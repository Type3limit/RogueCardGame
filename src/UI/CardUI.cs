using Godot;
using RogueCardGame.Core.Cards;

namespace RogueCardGame.UI;

/// <summary>
/// Visual representation of a single card in the player's hand.
/// Handles hover, selection, drag, and play interactions.
/// </summary>
public partial class CardUI : Control
{
    [Export] public string CardId { get; set; } = "";

    private Card? _card;
    private bool _isHovered;
    private bool _isDragging;
    private Vector2 _dragOffset;
    private Vector2 _originalPosition;

    // Visual elements (assigned in _Ready from scene tree)
    private Label? _nameLabel;
    private Label? _costLabel;
    private Label? _descriptionLabel;
    private Label? _typeLabel;
    private Panel? _cardFrame;
    private TextureRect? _artworkRect;
    private Panel? _linkIndicator;

    public Card? Card => _card;
    public bool IsSelected { get; set; }

    [Signal] public delegate void CardClickedEventHandler(CardUI card);
    [Signal] public delegate void CardPlayedEventHandler(CardUI card);

    // C# event fallback (Godot source generator runs only in-editor)
    public event Action<CardUI>? CardClicked;
    public event Action<CardUI>? CardPlayed;

    public override void _Ready()
    {
        _nameLabel = GetNodeOrNull<Label>("CardFrame/NameLabel");
        _costLabel = GetNodeOrNull<Label>("CardFrame/CostLabel");
        _descriptionLabel = GetNodeOrNull<Label>("CardFrame/DescriptionLabel");
        _typeLabel = GetNodeOrNull<Label>("CardFrame/TypeLabel");
        _cardFrame = GetNodeOrNull<Panel>("CardFrame");
        _artworkRect = GetNodeOrNull<TextureRect>("CardFrame/Artwork");
        _linkIndicator = GetNodeOrNull<Panel>("CardFrame/LinkIndicator");

        MouseEntered += OnMouseEntered;
        MouseExited += OnMouseExited;
    }

    public void SetCard(Card card)
    {
        _card = card;
        UpdateDisplay();
    }

    public void UpdateDisplay()
    {
        if (_card == null) return;

        if (_nameLabel != null) _nameLabel.Text = _card.DisplayName;
        if (_costLabel != null) _costLabel.Text = _card.EffectiveCost.ToString();
        if (_descriptionLabel != null) _descriptionLabel.Text = _card.ActiveDescription;
        if (_typeLabel != null) _typeLabel.Text = GetTypeText(_card.Data.Type);
        if (_linkIndicator != null) _linkIndicator.Visible = _card.Data.IsLink;

        UpdateFrameColor();
    }

    private void UpdateFrameColor()
    {
        if (_cardFrame == null || _card == null) return;

        // Card type colors based on the art design spec
        var styleBox = new StyleBoxFlat();
        styleBox.BgColor = _card.Data.Type switch
        {
            CardType.Attack => new Color(0.8f, 0.2f, 0.2f, 0.9f), // Red
            CardType.Skill => new Color(0.2f, 0.5f, 0.8f, 0.9f),  // Blue
            CardType.Power => new Color(0.6f, 0.4f, 0.8f, 0.9f),  // Purple
            _ => new Color(0.3f, 0.3f, 0.3f, 0.9f)
        };
        styleBox.CornerRadiusTopLeft = 8;
        styleBox.CornerRadiusTopRight = 8;
        styleBox.CornerRadiusBottomLeft = 8;
        styleBox.CornerRadiusBottomRight = 8;

        if (_isHovered)
            styleBox.BorderColor = new Color(1f, 0.9f, 0.3f, 1f); // Gold highlight
        else if (IsSelected)
            styleBox.BorderColor = new Color(0f, 1f, 0.5f, 1f); // Green selection

        if (_isHovered || IsSelected)
            styleBox.BorderWidthBottom = styleBox.BorderWidthTop =
                styleBox.BorderWidthLeft = styleBox.BorderWidthRight = 2;

        _cardFrame.AddThemeStyleboxOverride("panel", styleBox);
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
        {
            if (mb.Pressed)
            {
                IsSelected = !IsSelected;
                CardClicked?.Invoke(this);
                UpdateDisplay();
            }
        }
    }

    private void OnMouseEntered()
    {
        _isHovered = true;
        // Hover animation: scale up slightly
        var tween = CreateTween();
        tween.TweenProperty(this, "scale", new Vector2(1.15f, 1.15f), 0.1f);
        tween.TweenProperty(this, "position:y", Position.Y - 20, 0.1f);
        UpdateDisplay();
    }

    private void OnMouseExited()
    {
        _isHovered = false;
        var tween = CreateTween();
        tween.TweenProperty(this, "scale", Vector2.One, 0.1f);
        tween.TweenProperty(this, "position:y", _originalPosition.Y, 0.1f);
        UpdateDisplay();
    }

    public void SetOriginalPosition(Vector2 pos)
    {
        _originalPosition = pos;
    }

    private static string GetTypeText(CardType type) => type switch
    {
        CardType.Attack => "攻击",
        CardType.Skill => "技能",
        CardType.Power => "能力",
        CardType.Status => "状态",
        CardType.Curse => "诅咒",
        _ => ""
    };
}
