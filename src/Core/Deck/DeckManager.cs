using RogueCardGame.Core.Cards;

namespace RogueCardGame.Core.Deck;

/// <summary>
/// Seed-based random number generator for deterministic card draws.
/// </summary>
public class SeededRandom
{
    private Random _random;
    public int Seed { get; }

    public SeededRandom(int seed)
    {
        Seed = seed;
        _random = new Random(seed);
    }

    public int Next(int maxExclusive) => _random.Next(maxExclusive);
    public int Next(int min, int maxExclusive) => _random.Next(min, maxExclusive);
    public double NextDouble() => _random.NextDouble();

    public void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}

/// <summary>
/// Manages a player's complete deck state during combat:
/// draw pile, hand, discard pile, exhaust pile.
/// </summary>
public class DeckManager
{
    private readonly List<Card> _drawPile = [];
    private readonly List<Card> _hand = [];
    private readonly List<Card> _discardPile = [];
    private readonly List<Card> _exhaustPile = [];
    private readonly SeededRandom _random;

    public IReadOnlyList<Card> DrawPile => _drawPile;
    public IReadOnlyList<Card> Hand => _hand;
    public IReadOnlyList<Card> DiscardPile => _discardPile;
    public IReadOnlyList<Card> ExhaustPile => _exhaustPile;

    public int MaxHandSize { get; set; } = 10;

    public event Action<Card>? OnCardDrawn;
    public event Action<Card>? OnCardDiscarded;
    public event Action<Card>? OnCardExhausted;
    public event Action? OnShuffled;

    public DeckManager(SeededRandom random)
    {
        _random = random;
    }

    /// <summary>
    /// Initialize the deck with the player's full card list. Shuffles into draw pile.
    /// </summary>
    public void Initialize(IEnumerable<Card> cards)
    {
        _drawPile.Clear();
        _hand.Clear();
        _discardPile.Clear();
        _exhaustPile.Clear();

        _drawPile.AddRange(cards);
        _random.Shuffle(_drawPile);
    }

    /// <summary>
    /// Draw N cards from the draw pile into hand.
    /// If draw pile runs out, shuffle discard into draw pile.
    /// </summary>
    public List<Card> Draw(int count)
    {
        var drawn = new List<Card>();

        for (int i = 0; i < count; i++)
        {
            if (_hand.Count >= MaxHandSize) break;

            if (_drawPile.Count == 0)
            {
                if (_discardPile.Count == 0) break;
                ShuffleDiscardIntoDraw();
            }

            if (_drawPile.Count == 0) break;

            var card = _drawPile[^1];
            _drawPile.RemoveAt(_drawPile.Count - 1);
            _hand.Add(card);
            drawn.Add(card);
            OnCardDrawn?.Invoke(card);
        }

        return drawn;
    }

    /// <summary>
    /// Play a card from hand (remove from hand, caller handles effects).
    /// </summary>
    public bool PlayFromHand(Card card)
    {
        return _hand.Remove(card);
    }

    /// <summary>
    /// Discard a card from hand to discard pile.
    /// </summary>
    public void DiscardFromHand(Card card)
    {
        if (_hand.Remove(card))
        {
            _discardPile.Add(card);
            OnCardDiscarded?.Invoke(card);
        }
    }

    /// <summary>
    /// Discard entire hand at end of turn.
    /// </summary>
    public void DiscardHand()
    {
        foreach (var card in _hand.ToList())
        {
            _discardPile.Add(card);
            OnCardDiscarded?.Invoke(card);
        }
        _hand.Clear();
    }

    /// <summary>
    /// Exhaust a card (remove from play permanently this combat).
    /// </summary>
    public void Exhaust(Card card)
    {
        _hand.Remove(card);
        _discardPile.Remove(card);
        _exhaustPile.Add(card);
        OnCardExhausted?.Invoke(card);
    }

    /// <summary>
    /// Move a played card to discard pile (normal play resolution).
    /// </summary>
    public void MoveToDiscard(Card card)
    {
        _discardPile.Add(card);
    }

    /// <summary>
    /// Add a temporary card to hand (e.g., from Synergy Link copy).
    /// </summary>
    public void AddToHand(Card card)
    {
        if (_hand.Count < MaxHandSize)
            _hand.Add(card);
    }

    /// <summary>
    /// Add a card directly to the discard pile (e.g., curse/status cards added mid-combat).
    /// </summary>
    public void AddToDiscard(Card card)
    {
        _discardPile.Add(card);
        OnCardDiscarded?.Invoke(card);
    }

    /// <summary>
    /// Add a card to the top of the draw pile.
    /// </summary>
    public void AddToDrawPileTop(Card card)
    {
        _drawPile.Add(card);
    }

    /// <summary>
    /// Peek at the top N cards of the draw pile.
    /// </summary>
    public List<Card> PeekDrawPile(int count)
    {
        int actual = Math.Min(count, _drawPile.Count);
        return _drawPile.Skip(_drawPile.Count - actual).Take(actual).Reverse().ToList();
    }

    private void ShuffleDiscardIntoDraw()
    {
        _drawPile.AddRange(_discardPile);
        _discardPile.Clear();
        _random.Shuffle(_drawPile);
        OnShuffled?.Invoke();
    }

    /// <summary>
    /// Reset temp cost modifiers on all cards (called at turn start).
    /// </summary>
    public void ResetTempModifiers()
    {
        foreach (var card in _hand)
            card.ResetTempModifiers();
    }
}
