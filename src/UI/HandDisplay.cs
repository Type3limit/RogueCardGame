using Godot;
using RogueCardGame.Core.Cards;
using RogueCardGame.Core.Characters;
using RogueCardGame.Core.Combat;
using RogueCardGame.Core.Deck;

namespace RogueCardGame.UI;

/// <summary>
/// Displays the player's hand as a fan of cards at the bottom of the screen.
/// Manages card selection and play interactions.
/// </summary>
public partial class HandDisplay : Control
{
    [Export] public float CardSpacing { get; set; } = 120f;
    [Export] public float FanAngle { get; set; } = 2f; // degrees per card from center
    [Export] public float HoverLift { get; set; } = 30f;

    private HBoxContainer? _container;
    private PackedScene? _cardScene;
    private readonly List<CardUI> _cardUIs = [];

    private CardUI? _selectedCard;
    private CombatManager? _combat;
    private PlayerCharacter? _player;

    [Signal] public delegate void CardSelectedEventHandler(int cardInstanceId);

    // C# event fallback
    public event Action<int>? CardSelected;

    public override void _Ready()
    {
        _container = GetNodeOrNull<HBoxContainer>("HandContainer");
    }

    public void BindCombat(CombatManager combat, PlayerCharacter player)
    {
        _combat = combat;
        _player = player;
    }

    /// <summary>
    /// Refresh the hand display from the player's current hand.
    /// </summary>
    public void RefreshHand()
    {
        if (_player == null || _combat == null) return;

        var deck = _combat.PlayerDecks.GetValueOrDefault(_player.Id);
        if (deck == null) return;

        ClearHand();

        foreach (var card in deck.Hand)
        {
            var cardUI = CreateCardUI(card);
            _cardUIs.Add(cardUI);
            _container?.AddChild(cardUI);
        }

        ArrangeCards();
    }

    public void ClearHand()
    {
        foreach (var ui in _cardUIs)
            ui.QueueFree();
        _cardUIs.Clear();
        _selectedCard = null;
    }

    private CardUI CreateCardUI(Card card)
    {
        var cardUI = new CardUI();
        cardUI.SetCard(card);
        cardUI.CustomMinimumSize = new Vector2(140, 200);
        cardUI.CardClicked += OnCardClicked;
        return cardUI;
    }

    private void OnCardClicked(CardUI card)
    {
        if (_selectedCard == card)
        {
            // Deselect
            _selectedCard.IsSelected = false;
            _selectedCard = null;
        }
        else
        {
            // Select new card
            if (_selectedCard != null)
                _selectedCard.IsSelected = false;

            _selectedCard = card;
            _selectedCard.IsSelected = true;

            if (card.Card != null)
                CardSelected?.Invoke(card.Card.InstanceId);
        }
    }

    /// <summary>
    /// Get the currently selected card, if any.
    /// </summary>
    public Card? GetSelectedCard() => _selectedCard?.Card;

    /// <summary>
    /// Deselect the current card.
    /// </summary>
    public void DeselectCard()
    {
        if (_selectedCard != null)
        {
            _selectedCard.IsSelected = false;
            _selectedCard = null;
        }
    }

    private void ArrangeCards()
    {
        if (_cardUIs.Count == 0) return;

        float totalWidth = _cardUIs.Count * CardSpacing;
        float startX = -totalWidth / 2f;

        for (int i = 0; i < _cardUIs.Count; i++)
        {
            var card = _cardUIs[i];
            float x = startX + i * CardSpacing;
            float angle = (i - _cardUIs.Count / 2f) * FanAngle;
            card.Position = new Vector2(x + Size.X / 2, 0);
            card.RotationDegrees = angle;
            card.SetOriginalPosition(card.Position);
        }
    }
}
