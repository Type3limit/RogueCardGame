using RogueCardGame.Core.Cards;
using RogueCardGame.Core.Characters;
using RogueCardGame.Core.Deck;

namespace RogueCardGame.Core.Combat;

/// <summary>
/// Main combat manager that orchestrates a complete battle encounter.
/// Coordinates all subsystems: turns, formation, aggro, targeting, energy, effects.
/// </summary>
public class CombatManager
{
    // Subsystems
    public TurnSystem TurnSystem { get; }
    public FormationSystem Formation { get; }
    public AggroSystem Aggro { get; }
    public TargetingSystem Targeting { get; }
    public EnemyAI EnemyAI { get; }
    private readonly SeededRandom _random;

    // Combatants
    public List<PlayerCharacter> Players { get; } = [];
    public List<Enemy> Enemies { get; } = [];
    public Dictionary<int, DeckManager> PlayerDecks { get; } = new();

    // State
    public bool IsActive => TurnSystem.CurrentPhase is not
        (CombatPhase.Victory or CombatPhase.Defeat or CombatPhase.NotStarted);

    // Events
    public event Action<PlayerCharacter, Card>? OnCardPlayed;
    public event Action<Enemy, EnemyIntent>? OnEnemyAction;
    public event Action<Combatant, int>? OnDamageDealt;
    public event Action<Combatant, int>? OnBlockGained;
    public event Action<bool>? OnCombatEnded;

    public CombatManager(int seed)
    {
        _random = new SeededRandom(seed);
        TurnSystem = new TurnSystem();
        Formation = new FormationSystem();
        Aggro = new AggroSystem();
        Targeting = new TargetingSystem(Formation, Aggro);
        EnemyAI = new EnemyAI(_random);
    }

    /// <summary>
    /// Initialize combat with players and enemies.
    /// </summary>
    public void Initialize(
        IEnumerable<PlayerCharacter> players,
        IEnumerable<Enemy> enemies,
        Dictionary<int, List<Card>> playerDecks)
    {
        Players.Clear();
        Enemies.Clear();
        PlayerDecks.Clear();
        Formation.Clear();
        Aggro.Clear();

        // Setup players
        foreach (var player in players)
        {
            Players.Add(player);
            Formation.SetPosition(player.Id, player.PreferredRow);

            if (playerDecks.TryGetValue(player.Id, out var cards))
            {
                var deck = new DeckManager(_random);
                deck.Initialize(cards);
                PlayerDecks[player.Id] = deck;
            }
        }

        // Setup enemies
        foreach (var enemy in enemies)
        {
            Enemies.Add(enemy);
            Formation.SetPosition(enemy.Id, enemy.Data.PreferredRow);
        }
    }

    /// <summary>
    /// Start the combat encounter.
    /// </summary>
    public void StartCombat()
    {
        TurnSystem.StartCombat();
        StartPlayerPhase();
    }

    /// <summary>
    /// Player planning phase: draw cards, refresh energy.
    /// </summary>
    private void StartPlayerPhase()
    {
        Formation.ResetTurnMoves();

        foreach (var player in Players.Where(p => p.IsAlive && !p.IsDowned))
        {
            player.OnTurnStart();
            if (PlayerDecks.TryGetValue(player.Id, out var deck))
            {
                deck.ResetTempModifiers();
                deck.Draw(player.DrawPerTurn);
            }
        }

        // Roll enemy intents for this turn
        var state = GetCombatState();
        foreach (var enemy in Enemies.Where(e => e.IsAlive && !e.IsHacked))
        {
            enemy.CurrentIntent = EnemyAI.SelectIntent(enemy, state);
        }
    }

    /// <summary>
    /// Attempt to play a card from a player's hand.
    /// Returns true if the card was successfully played.
    /// </summary>
    public bool TryPlayCard(PlayerCharacter player, Card card, Combatant? target = null)
    {
        if (TurnSystem.CurrentPhase != CombatPhase.PlayerPlanningPhase)
            return false;

        if (!player.CanPlayCard(card))
            return false;

        var deck = PlayerDecks.GetValueOrDefault(player.Id);
        if (deck == null || !deck.Hand.Contains(card))
            return false;

        // Resolve targets
        var validTargets = Targeting.GetValidTargets(
            player,
            card.Data.TargetType,
            card.Data.Range,
            Enemies.Where(e => e.IsAlive),
            Players.Where(p => p.IsAlive));

        List<Combatant> targets;
        if (card.Data.TargetType == TargetType.Self)
        {
            targets = [player];
        }
        else if (card.Data.TargetType is TargetType.SingleEnemy or TargetType.SingleAlly)
        {
            if (target == null || !validTargets.Contains(target))
                return false;
            targets = [target];
        }
        else
        {
            targets = validTargets;
        }

        // Spend energy
        player.SpendEnergy(card.EffectiveCost);

        // Remove from hand
        deck.PlayFromHand(card);

        // Execute effects
        var context = new CardEffectContext
        {
            Source = player,
            Target = targets.FirstOrDefault(),
            AllTargets = targets,
            Formation = Formation,
            Aggro = Aggro,
            Card = card,
            Range = card.Data.Range
        };

        var effect = CardEffectFactory.CreateComposite(card.ActiveEffects);
        effect.Execute(context);

        // Move to discard (unless exhausted by effect)
        if (card.Data.Type != CardType.Power)
            deck.MoveToDiscard(card);

        OnCardPlayed?.Invoke(player, card);

        // Check victory
        CheckCombatEnd();

        return true;
    }

    /// <summary>
    /// Player ends their turn. Proceed to enemy phase.
    /// </summary>
    public void EndPlayerTurn()
    {
        // Discard remaining hand cards
        foreach (var player in Players.Where(p => p.IsAlive))
        {
            PlayerDecks.GetValueOrDefault(player.Id)?.DiscardHand();
        }

        // Enemy phase
        TurnSystem.AdvancePhase(); // → PlayerResolutionPhase
        TurnSystem.AdvancePhase(); // → EnemyPhase
        ExecuteEnemyPhase();
    }

    /// <summary>
    /// Execute all enemy actions.
    /// </summary>
    private void ExecuteEnemyPhase()
    {
        foreach (var enemy in Enemies.Where(e => e.IsAlive).ToList())
        {
            if (enemy.IsHacked)
            {
                // Hacked enemies attack other enemies
                enemy.TickHack();
                continue;
            }

            if (enemy.CurrentIntent != null)
            {
                EnemyAI.ExecuteIntent(
                    enemy, enemy.CurrentIntent,
                    Targeting, Players, Formation, Aggro);
                OnEnemyAction?.Invoke(enemy, enemy.CurrentIntent);
            }
        }

        // Check for downed players
        foreach (var player in Players.Where(p => p.CurrentHp <= 0 && !p.IsDowned))
        {
            player.GoDown();
        }

        CheckCombatEnd();

        if (IsActive)
        {
            // Turn end phase
            TurnSystem.AdvancePhase(); // → TurnEndPhase
            ExecuteTurnEnd();
        }
    }

    /// <summary>
    /// End of turn: tick status effects, decay aggro, advance turn.
    /// </summary>
    private void ExecuteTurnEnd()
    {
        foreach (var combatant in GetAllCombatants())
        {
            combatant.OnTurnEnd();
        }

        // Tick downed players
        foreach (var player in Players.Where(p => p.IsDowned).ToList())
        {
            if (player.TickDowned())
            {
                // Player permanently out for this combat
            }
        }

        Aggro.DecayAggro();

        CheckCombatEnd();

        if (IsActive)
        {
            TurnSystem.AdvancePhase(); // → Next PlayerPlanningPhase
            StartPlayerPhase();
        }
    }

    /// <summary>
    /// Check if combat should end.
    /// </summary>
    private void CheckCombatEnd()
    {
        if (Enemies.All(e => !e.IsAlive))
        {
            TurnSystem.EndCombat(true);
            OnCombatEnded?.Invoke(true);
        }
        else if (Players.All(p => !p.IsAlive || p.IsDowned))
        {
            TurnSystem.EndCombat(false);
            OnCombatEnded?.Invoke(false);
        }
    }

    /// <summary>
    /// Try to switch a player's formation row.
    /// </summary>
    public bool TrySwitchRow(PlayerCharacter player, bool free = false)
    {
        return Formation.TrySwitchRow(player.Id, free);
    }

    public CombatState GetCombatState() => new()
    {
        TurnNumber = TurnSystem.TurnNumber,
        Players = Players,
        Enemies = Enemies,
        Formation = Formation
    };

    private IEnumerable<Combatant> GetAllCombatants()
    {
        foreach (var p in Players) yield return p;
        foreach (var e in Enemies) yield return e;
    }
}
