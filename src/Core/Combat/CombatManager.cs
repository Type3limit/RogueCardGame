using RogueCardGame.Core.Cards;
using RogueCardGame.Core.Characters;
using RogueCardGame.Core.Combat.Actions;
using RogueCardGame.Core.Combat.Powers;
using RogueCardGame.Core.Deck;
using RogueCardGame.Core.Environment;
using RogueCardGame.Core.Implants;

namespace RogueCardGame.Core.Combat;

/// <summary>
/// Main combat manager that orchestrates a complete battle encounter.
/// Uses ActionManager (STS-style action queue) for cascading effects.
/// Powers on combatants provide lifecycle hooks for extensible mechanics.
/// </summary>
public class CombatManager
{
    // Subsystems
    public TurnSystem TurnSystem { get; }
    public FormationSystem Formation { get; }
    public AggroSystem Aggro { get; }
    public TargetingSystem Targeting { get; }
    public EnemyAI EnemyAI { get; }
    public ActionManager Actions { get; }
    public CombatEventDispatcher Events { get; } = new();
    private readonly SeededRandom _random;

    // Card database reference — also exposed for BehaviorExecutor
    private CardDatabase? _cardDb;
    public CardDatabase? CardDb => _cardDb;

    // Implant bonus provider exposed for BehaviorExecutor
    public Func<string, int>? ImplantBonusProvider => _implants != null
        ? effectType => _implants.GetTotalBonus(effectType)
        : null;

    // Optional system references
    private EnvironmentSystem? _env;
    private PrototypeCoreSystem? _protoCore;
    private ImplantManager? _implants;

    // Per-turn tracking
    private bool _switchedRowThisTurn;
    private bool _isFirstMeleeThisTurn;
    private bool _isFirstRangedThisTurn;
    private int _turnCount;
#pragma warning disable CS0414 // _postSwitchDiscountReady: planned battlefieldInterface feature, not yet wired to card cost reduction
    private bool _postSwitchDiscountReady; // battlefieldInterface: next card costs -1 after row swap
#pragma warning restore CS0414

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
    public event Action<bool>? OnCombatEnded;
    public event Action<string>? OnPhaseTransition;
    /// <summary>Fired when an enemy summons a new combatant. Subscribers should call AddEnemy().</summary>
    public event Action<Enemy>? OnEnemySummoned;
    /// <summary>Fired when scry triggers (precognitionModule). UI should display peeked cards for selection.</summary>
    public event Action<PlayerCharacter, DeckManager, List<Card>, int>? OnScryTriggered;

    /// <summary>Optional log sink for diagnostic messages during combat (null = silent).</summary>
    public Action<string>? Log { get; set; }

    /// <summary>
    /// Optional factory used to create summoned enemies by ID (set by CombatScene).
    /// Called when an enemy's Summon intent fires; value = count to summon.
    /// </summary>
    public Func<string, Enemy?>? EnemyFactory { get; set; }

    public CombatManager(int seed, CardDatabase? cardDb = null)
    {
        _random = new SeededRandom(seed);
        TurnSystem = new TurnSystem();
        Formation = new FormationSystem();
        Aggro = new AggroSystem();
        Targeting = new TargetingSystem(Formation, Aggro);
        EnemyAI = new EnemyAI(_random);
        Actions = new ActionManager { Combat = this };
        _cardDb = cardDb;
    }

    /// <summary>
    /// Initialize combat with players and enemies.
    /// </summary>
    public void Initialize(
        IEnumerable<PlayerCharacter> players,
        IEnumerable<Enemy> enemies,
        Dictionary<int, List<Card>> playerDecks,
        EnvironmentSystem? env = null,
        PrototypeCoreSystem? protoCore = null,
        ImplantManager? implants = null)
    {
        _env = env;
        _protoCore = protoCore;
        _implants = implants;

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
            player.Powers.ActionManager = Actions;
            player.Powers.EventBus = Events;
            player.Powers.ImplantBonusProvider = effectType => _implants?.GetTotalBonus(effectType) ?? 0;

            if (playerDecks.TryGetValue(player.Id, out var cards))
            {
                var deck = new DeckManager(_random);
                deck.Initialize(cards);

                // Implant: maxHandSize modifier
                int handSizeDelta = _implants?.GetTotalBonus("maxHandSize") ?? 0;
                if (handSizeDelta != 0)
                    deck.MaxHandSize = Math.Max(1, deck.MaxHandSize + handSizeDelta);

                PlayerDecks[player.Id] = deck;
            }

            // Implant: maxHp modifier (applied once at combat start)
            int maxHpDelta = _implants?.GetTotalBonus("maxHp") ?? 0;
            if (maxHpDelta != 0)
            {
                player.MaxHp = Math.Max(1, player.MaxHp + maxHpDelta);
                player.CurrentHp = Math.Min(player.CurrentHp, player.MaxHp);
            }
        }

        // Setup enemies
        foreach (var enemy in enemies)
        {
            Enemies.Add(enemy);
            Formation.SetPosition(enemy.Id, enemy.Data.PreferredRow);
            enemy.Powers.ActionManager = Actions;
            enemy.Powers.EventBus = Events;
            enemy.Powers.ImplantBonusProvider = effectType => _implants?.GetTotalBonus(effectType) ?? 0;
        }
    }

    /// <summary>
    /// Start the combat encounter.
    /// </summary>
    public void StartCombat()
    {
        TurnSystem.StartCombat();
        // §2.7 — Assemble implant effects into a PersistentPower subscribed to the Dispatcher
        ApplyImplantPowers();
        StartPlayerPhase();
    }

    /// <summary>
    /// Assemble each player's equipped implants into an ImplantBonusPower.
    /// The Power subscribes to the Dispatcher for all event-driven implant effects,
    /// replacing the scattered _implants.GetTotalBonus() call sites.
    /// </summary>
    private void ApplyImplantPowers()
    {
        if (_implants == null) return;
        foreach (var player in Players.Where(p => p.IsAlive))
        {
            var deck = PlayerDecks.GetValueOrDefault(player.Id);
            var power = new Implants.ImplantBonusPower(_implants, deck);
            player.Powers.ApplyPower(power, player);
        }
    }

    /// <summary>
    /// Player planning phase: draw cards, refresh energy.
    /// </summary>
    private void StartPlayerPhase()
    {
        _turnCount++;
        Formation.ResetTurnMoves();

        // Reset per-turn prototype flags
        _switchedRowThisTurn = false;
        _isFirstMeleeThisTurn = true;
        _isFirstRangedThisTurn = true;
        _postSwitchDiscountReady = false;

        // Environment turn start
        _env?.OnTurnStart();

        foreach (var player in Players.Where(p => p.IsAlive && !p.IsDowned))
        {
            player.OnTurnStart();
            if (PlayerDecks.TryGetValue(player.Id, out var deck))
            {
                deck.ResetTempModifiers();

                // Base draw first — so ImplantBonusPower's AtTurnStart extra draws land on top
                deck.Draw(player.DrawPerTurn);
                int drawBonus = _env?.GetDrawBonus() ?? 0;
                if (drawBonus > 0) deck.Draw(drawBonus);
            }

            // Publish AtTurnStart after base draw — ImplantBonusPower adds energy/draw/block bonuses
            Events.Publish(new CombatEvent { Kind = TriggerKind.AtTurnStart, Actor = player });

            // Implant: precognitionModule — scry every 2 turns (complex UI logic, kept here for now)
            int precog = _implants?.GetTotalBonus("precognitionModule") ?? 0;
            if (precog > 0 && _turnCount % 2 == 0 && PlayerDecks.TryGetValue(player.Id, out var pDeck))
                PrecognitionTrigger(player, pDeck);
        }

        // Roll enemy intents for this turn
        var state = GetCombatState();
        foreach (var enemy in Enemies.Where(e => e.IsAlive))
        {
            enemy.CurrentIntent = EnemyAI.SelectIntent(enemy, state);
        }
    }

    /// <summary>
    /// Check if a card can be played by the player considering formation restrictions.
    /// Back-row players cannot play melee cards.
    /// </summary>
    public bool CanPlay(PlayerCharacter player, Card card)
    {
        if (!player.CanPlayCard(card)) return false;
        // Back-row restriction: melee cards are disabled
        if (Formation.GetPosition(player.Id) == FormationRow.Back &&
            card.Data.Range == CardRange.Melee)
            return false;
        return true;
    }

    /// <summary>
    /// Attempt to play a card from a player's hand.
    /// Returns true if the card was successfully played.
    /// </summary>
    public bool TryPlayCard(PlayerCharacter player, Card card, Combatant? target = null)
    {
        if (TurnSystem.CurrentPhase != CombatPhase.PlayerPlanningPhase)
            return false;

        if (!CanPlay(player, card))
            return false;

        var deck = PlayerDecks.GetValueOrDefault(player.Id);
        if (deck == null || !deck.Hand.Contains(card))
            return false;

        // Resolve targets
        var effectiveTargetType = card.Data.EffectiveTargetType;
        var validTargets = Targeting.GetValidTargets(
            player,
            effectiveTargetType,
            card.Data.Range,
            Enemies.Where(e => e.IsAlive),
            Players.Where(p => p.IsAlive));

        List<Combatant> targets;
        if (effectiveTargetType == TargetType.Self || effectiveTargetType == TargetType.None)
        {
            targets = [player];
        }
        else if (effectiveTargetType is TargetType.SingleEnemy or TargetType.SingleAlly)
        {
            if (target == null || !validTargets.Contains(target))
                return false;
            targets = [target];
        }
        else
        {
            targets = validTargets;
        }

        // Compute final card cost with environment and prototype modifiers
        int envCostMod = _env?.GetCostModifier(card.Data.Type, _env.IsFirstCardThisTurn) ?? 0;
        int protoCostMod = 0;
        if (_protoCore?.IsActivated == true
            && _protoCore.Identity == PrototypeIdentity.TacticalProtocol
            && _switchedRowThisTurn)
        {
            protoCostMod = _protoCore.GetPostSwitchCostReduction();
            _switchedRowThisTurn = false; // consume the post-switch discount
        }
        // Implant: battlefieldInterface — next card costs -1 after row swap
        int postSwitchDiscount = 0;
        if (_postSwitchDiscountReady)
        {
            postSwitchDiscount = _implants?.GetTotalBonus("battlefieldInterface") ?? 0;
            _postSwitchDiscountReady = false;
        }

        int finalCost = Math.Max(0, card.EffectiveCost + envCostMod - protoCostMod - postSwitchDiscount);

        // Power pipeline: allow powers (e.g. MindSovereign, CompileAccel) to further reduce cost.
        Combatant? costTarget = targets.Count > 0 ? targets[0] : target;
        finalCost = Math.Max(0, player.Powers.ModifyCardCost(card, finalCost, costTarget));

        // Re-check energy against modified cost
        if (player.CurrentEnergy < finalCost)
            return false;

        // Track enemies alive for kill-energy bonus
        int enemiesAliveBefore = Enemies.Count(e => e.IsAlive);

        // Spend energy
        player.SpendEnergy(finalCost);

        // Remove from hand
        deck.PlayFromHand(card);

        // Resolve effects: position-reactive cards choose effects by current row
        var playerRow = Formation.GetPosition(player.Id);
        List<CardEffectData> effectsToRun;
        if (card.IsPositionReactive)
        {
            effectsToRun = card.GetEffectsForRow(playerRow);
        }
        else
        {
            effectsToRun = card.ActiveEffects;
        }

        // Handle overloadConsume keyword — OverchargeConsumeEffect/Action handles stack reading & removal
        // No-op here: stacks are read and removed inside the effect/action itself

        // Execute effects via action queue
        var context = new CardEffectContext
        {
            Source = player,
            Target = targets.FirstOrDefault(),
            AllTargets = targets,
            Formation = Formation,
            Aggro = Aggro,
            Card = card,
            Range = card.Data.Range,
            Actions = Actions,
            Deck = deck,
            CardDb = _cardDb,
            ImplantBonusProvider = effectType => _implants?.GetTotalBonus(effectType) ?? 0,
            AllEnemies = Enemies.Where(e => e.IsAlive).Cast<Combatant>().ToList(),
            LastDamageDealt = 0,
            TotalDamageDealtThisCard = 0,
            LastSelfHpLoss = 0,
            TotalSelfHpLossThisCard = 0,
        };

        // RangedSuppressProtocol: first ranged card each turn deals +3 damage (temp strength)
        bool appliedProtoStrength = false;
        if (_protoCore?.IsActivated == true
            && _protoCore.Identity == PrototypeIdentity.RangedSuppressProtocol
            && card.Data.Range == CardRange.Ranged
            && _isFirstRangedThisTurn)
        {
            player.Powers.ApplyPower(new StrengthPower { Amount = 3 }, player);
            appliedProtoStrength = true;
        }

        // §2.8 — Build Behavior pipeline; BehaviorExecutor drives card effect execution
        var behaviors = BehaviorBuilder.BuildAll(effectsToRun, TriggerKind.OnCardPlayed);
        var behaviorCtx = new BehaviorCtx
        {
            Actor      = player,
            Target     = targets.FirstOrDefault(),
            AllTargets = targets,
            Card       = card,
            Actions    = Actions,
            Combat     = this,
            Deck       = deck,
        };
        try
        {
            // Keep CurrentEffectContext for LastDamageDealt / TotalDamageDealtThisCard tracking
            Actions.CurrentEffectContext = context;
            BehaviorExecutor.ExecuteAll(behaviors, behaviorCtx);

            // Drain the action queue — all enqueued actions execute with cascading
            Actions.ProcessAll();
        }
        finally
        {
            Actions.CurrentEffectContext = null;
        }

        // Remove temporary prototype strength boost
        if (appliedProtoStrength)
            player.Powers.ConsumeStacks(CommonPowerIds.Strength, 3);

        // Signal environment that a card was played (tracks first-card state)
        _env?.OnCardPlayed();

        // Kill energy is now handled by ImplantBonusPower.OnKillEvent (subscribed to OnKill Dispatcher)

        // Record ranged plays for the bonus tracking
        if (card.Data.Range == CardRange.Ranged)
            Formation.RecordRangedPlay(player.Id);

        // Prototype: record card play for identity tracking
        _protoCore?.RecordCardPlay(card.Data.Range, playerRow);

        // FrontPressProtocol: first melee each turn gives +1 Overcharge
        if (_protoCore?.IsActivated == true
            && _protoCore.Identity == PrototypeIdentity.FrontPressProtocol
            && card.Data.Range == CardRange.Melee
            && _isFirstMeleeThisTurn)
        {
            player.Powers.ApplyPower(new OverchargePower { Amount = 1 }, player);
            Actions.ProcessAll();
        }

        // Track first-of-type for this turn
        if (card.Data.Range == CardRange.Melee) _isFirstMeleeThisTurn = false;
        if (card.Data.Range == CardRange.Ranged) _isFirstRangedThisTurn = false;

        // Exhaust cards are removed from deck after one use; others go to discard
        if (card.Data.Type != CardType.Power)
        {
            if (card.Exhaust)
                deck.Exhaust(card);
            else
                deck.MoveToDiscard(card);
        }

        OnCardPlayed?.Invoke(player, card);

        // Trigger power hooks for card played — via Dispatcher (subscriptions handle per-power dispatch)
        // Fan-out to combat-scope subscribers (implants, environment, tests)
        Events.Publish(new CombatEvent { Kind = TriggerKind.OnCardPlayed, Actor = player, Card = card });
        Actions.ProcessAll();

        // pulseProcessor draw is now handled by ImplantBonusPower.OnCardPlayedEvent (subscribed to OnCardPlayed Dispatcher)

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
        // Reset enemy block at start of their turn (STS-style)
        foreach (var enemy in Enemies.Where(e => e.IsAlive))
        {
            enemy.OnTurnStart();
            // Publish so enemy powers (subscribed via Dispatcher) fire their AtTurnStart hooks
            Events.Publish(new CombatEvent { Kind = TriggerKind.AtTurnStart, Actor = enemy });
        }

        foreach (var enemy in Enemies.Where(e => e.IsAlive).ToList())
        {
            if (enemy.CurrentIntent != null)
            {
                // Formation linkage: melee enemies pushed to back row are disabled
                bool isAttackIntent = enemy.CurrentIntent.Type is EnemyIntentType.Attack or EnemyIntentType.AttackDefend;
                bool meleeDisabled = isAttackIntent
                    && enemy.Data.PreferredRow == FormationRow.Front
                    && Formation.GetPosition(enemy.Id) == FormationRow.Back;

                if (meleeDisabled)
                {
                    enemy.CurrentIntent = new EnemyIntent
                    {
                        Type = EnemyIntentType.Disabled,
                        Value = 0,
                        Scope = TargetScope.Self,
                        Description = "失能 (后排)"
                    };
                }

                // Handle Summon intents at CombatManager level (EnemyAI only logs them)
                if (enemy.CurrentIntent.Type == EnemyIntentType.Summon)
                {
                    if (EnemyFactory != null && enemy.CurrentIntent.SummonId != null)
                    {
                        int count = Math.Max(1, enemy.CurrentIntent.Value);
                        for (int i = 0; i < count; i++)
                        {
                            var summoned = EnemyFactory(enemy.CurrentIntent.SummonId);
                            if (summoned != null) AddEnemy(summoned);
                        }
                    }
                }
                else
                {
                    EnemyAI.ExecuteIntent(
                        enemy, enemy.CurrentIntent,
                        Targeting, Players, Enemies, Formation, Aggro);
                }

                OnEnemyAction?.Invoke(enemy, enemy.CurrentIntent);

                // Drain any actions enqueued by power hooks during enemy actions
                Actions.ProcessAll();
            }

            // Check phase transitions after each enemy action resolves
            string? phaseMsg = enemy.CheckPhaseTransition();
            if (phaseMsg != null)
                OnPhaseTransition?.Invoke($"{enemy.Data.Name}: {phaseMsg}");
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
            // Publish AtTurnEnd — ImplantBonusPower and all subscribed Powers react here
            Events.Publish(new CombatEvent { Kind = TriggerKind.AtTurnEnd, Actor = combatant });
            // Tick-down timed powers (Vulnerable, Weak, Regen, etc.) after effects resolve
            combatant.Powers.TickDownPowers();
        }

        // Tick downed players
        foreach (var player in Players.Where(p => p.IsDowned).ToList())
        {
            if (player.TickDowned())
            {
                // Player permanently out for this combat
            }
        }

        // Environment: apply end-of-turn effects (radiation, duration tick, etc.)
        _env?.ApplyTurnEndEffects(GetAllCombatants().ToList());
        Actions.ProcessAll();

        Aggro.DecayAggro();

        // Prototype: advance turn observation, fire event if newly activated
        string? protoMsg = _protoCore?.AdvanceTurn();
        if (protoMsg != null) OnPhaseTransition?.Invoke(protoMsg);

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
    /// Costs 1 energy normally; Rooted (Locked) status prevents switching entirely.
    /// </summary>
    public bool TrySwitchRow(PlayerCharacter player, bool free = false)
    {
        // Rooted players cannot switch rows
        if (player.Powers.HasPower(CommonPowerIds.Rooted))
            return false;

        // Implant: battlefieldInterface — row switches are always free
        int battlefieldInterface = _implants?.GetTotalBonus("battlefieldInterface") ?? 0;
        if (battlefieldInterface > 0)
            free = true;

        // GravityAnomaly: row switches always cost energy (overrides free)
        if (free && _env?.HasEffect(EnvironmentType.GravityAnomaly) == true)
            free = false;

        if (!free && player.CurrentEnergy < 1)
            return false;

        if (!Formation.TrySwitchRow(player.Id, free))
            return false;

        if (!free)
            player.SpendEnergy(1);

        // Record switch for prototype tracking
        _protoCore?.RecordSwitch();
        _switchedRowThisTurn = true;

        // Implant: battlefieldInterface — next card costs -1 after row swap
        if (battlefieldInterface > 0)
            _postSwitchDiscountReady = true;

        return true;
    }

    public void SetCardDatabase(CardDatabase db) => _cardDb = db;

    /// <summary>
    /// Add a summoned enemy to the active combat. Positions it in the formation and wires power hooks.
    /// Fires OnEnemySummoned so the scene can create a visual panel.
    /// </summary>
    public void AddEnemy(Enemy enemy)
    {
        Enemies.Add(enemy);
        Formation.SetPosition(enemy.Id, enemy.Data.PreferredRow);
        enemy.Powers.ActionManager = Actions;
        enemy.Powers.ImplantBonusProvider = effectType => _implants?.GetTotalBonus(effectType) ?? 0;
        // Roll initial intent for the newly summoned unit
        var state = GetCombatState();
        enemy.CurrentIntent = EnemyAI.SelectIntent(enemy, state);
        OnEnemySummoned?.Invoke(enemy);
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

    /// <summary>
    /// PrecognitionModule: peek at the top 2 cards of the draw pile, let the player pick 1 to keep.
    /// The event OnScryTriggered fires with (player, deck, peekedCards, keepCount).
    /// UI calls CompleteScry with the chosen card to finalize the scry.
    /// </summary>
    private void PrecognitionTrigger(PlayerCharacter player, DeckManager deck)
    {
        int peekCount = Math.Min(2, deck.DrawPileCount);
        if (peekCount <= 0) return;
        var peeked = deck.PeekDrawPile(peekCount);
        OnScryTriggered?.Invoke(player, deck, peeked, 1);
    }

    /// <summary>
    /// Complete a scry action: the chosen card stays on top, others go to the bottom of the draw pile.
    /// </summary>
    public void CompleteScry(PlayerCharacter player, DeckManager deck, List<Card> peeked, Card chosen)
    {
        // Remove all peeked cards from the draw pile
        foreach (var card in peeked)
            deck.TakeFromDrawPile(card);

        // Non-chosen cards go to the bottom (drawn last)
        foreach (var card in peeked)
        {
            if (card != chosen)
                deck.AddToDrawPileBottom(card);
        }

        // Chosen card stays on top (drawn next)
        deck.AddToDrawPileTop(chosen);
    }
}
