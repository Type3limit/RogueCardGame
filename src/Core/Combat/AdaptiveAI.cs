using RogueCardGame.Core.Characters;

namespace RogueCardGame.Core.Combat;

/// <summary>
/// Tracks player behavior patterns during a run.
/// Used by the Adaptive AI to shift enemy strategies.
/// </summary>
public class PlayerBehaviorProfile
{
    // Tracked metrics (normalized 0.0 - 1.0)
    public float AggressivenessRatio { get; private set; } // Attack vs Skill usage
    public float FrontRowPreference { get; private set; }  // Time spent in front row
    public float BlockingTendency { get; private set; }    // Block generation frequency
    public float HackingFrequency { get; private set; }    // Hack card usage
    public float LinkUsage { get; private set; }           // Synergy link card usage

    // Raw counters for calculation
    private int _totalCardsPlayed;
    private int _attackCardsPlayed;
    private int _skillCardsPlayed;
    private int _blockCardsPlayed;
    private int _hackCardsPlayed;
    private int _linkCardsPlayed;
    private int _turnsInFront;
    private int _turnsInBack;
    private int _totalTurns;

    /// <summary>
    /// Record a card being played for behavior tracking.
    /// </summary>
    public void RecordCardPlayed(Cards.CardType type, bool isHack, bool isLink)
    {
        _totalCardsPlayed++;

        if (type == Cards.CardType.Attack) _attackCardsPlayed++;
        if (type == Cards.CardType.Skill) _skillCardsPlayed++;
        if (isHack) _hackCardsPlayed++;
        if (isLink) _linkCardsPlayed++;

        Recalculate();
    }

    public void RecordBlockGained()
    {
        _blockCardsPlayed++;
        Recalculate();
    }

    public void RecordTurnPosition(FormationRow row)
    {
        _totalTurns++;
        if (row == FormationRow.Front) _turnsInFront++;
        else _turnsInBack++;
        Recalculate();
    }

    private void Recalculate()
    {
        if (_totalCardsPlayed > 0)
        {
            AggressivenessRatio = (float)_attackCardsPlayed / _totalCardsPlayed;
            BlockingTendency = (float)_blockCardsPlayed / _totalCardsPlayed;
            HackingFrequency = (float)_hackCardsPlayed / _totalCardsPlayed;
            LinkUsage = (float)_linkCardsPlayed / _totalCardsPlayed;
        }

        if (_totalTurns > 0)
            FrontRowPreference = (float)_turnsInFront / _totalTurns;
    }
}

/// <summary>
/// Adaptive AI that adjusts enemy behavior based on player behavior profiles.
/// Shifts intent weights up to 30% to counter dominant player strategies.
/// </summary>
public class AdaptiveAI
{
    private readonly Dictionary<int, PlayerBehaviorProfile> _profiles = new();

    /// <summary>Maximum weight shift percentage.</summary>
    public const float MaxAdaptation = 0.30f;

    /// <summary>Number of turns before adaptation kicks in.</summary>
    public const int AdaptationDelay = 3;

    public PlayerBehaviorProfile GetProfile(int playerId)
    {
        if (!_profiles.ContainsKey(playerId))
            _profiles[playerId] = new PlayerBehaviorProfile();
        return _profiles[playerId];
    }

    /// <summary>
    /// Adjust enemy intent weights based on observed player behavior.
    /// Returns modified weight multipliers for each intent type.
    /// </summary>
    public Dictionary<EnemyIntentType, float> GetAdaptedWeights(
        int turnNumber,
        IEnumerable<PlayerBehaviorProfile> profiles)
    {
        var weights = new Dictionary<EnemyIntentType, float>
        {
            [EnemyIntentType.Attack] = 1.0f,
            [EnemyIntentType.AttackDefend] = 1.0f,
            [EnemyIntentType.Defend] = 1.0f,
            [EnemyIntentType.Buff] = 1.0f,
            [EnemyIntentType.Debuff] = 1.0f,
            [EnemyIntentType.Heal] = 1.0f,
        };

        if (turnNumber < AdaptationDelay) return weights;

        // Average all player profiles
        var profileList = profiles.ToList();
        if (profileList.Count == 0) return weights;

        float avgAggro = profileList.Average(p => p.AggressivenessRatio);
        float avgBlock = profileList.Average(p => p.BlockingTendency);
        float avgFront = profileList.Average(p => p.FrontRowPreference);
        float avgHack = profileList.Average(p => p.HackingFrequency);

        // Counter-strategy: if players are aggressive, enemies defend more
        if (avgAggro > 0.6f)
        {
            float shift = Math.Min(MaxAdaptation, (avgAggro - 0.5f) * 0.6f);
            weights[EnemyIntentType.Defend] += shift;
            weights[EnemyIntentType.Attack] -= shift * 0.5f;
        }

        // If players block a lot, enemies debuff more
        if (avgBlock > 0.4f)
        {
            float shift = Math.Min(MaxAdaptation, (avgBlock - 0.3f) * 0.6f);
            weights[EnemyIntentType.Debuff] += shift;
            weights[EnemyIntentType.Buff] += shift * 0.5f;
        }

        // If players favor front row, enemies use more AoE/back-targeting
        if (avgFront > 0.7f)
        {
            weights[EnemyIntentType.Attack] += MaxAdaptation * 0.3f;
        }

        // If players hack a lot, enemies buff/heal to survive
        if (avgHack > 0.15f)
        {
            float shift = Math.Min(MaxAdaptation, avgHack * 1.5f);
            weights[EnemyIntentType.Heal] += shift;
            weights[EnemyIntentType.Buff] += shift * 0.5f;
        }

        // Clamp all weights to [0.5, 1.5]
        foreach (var key in weights.Keys.ToList())
            weights[key] = Math.Clamp(weights[key], 0.5f, 1.0f + MaxAdaptation);

        return weights;
    }

    /// <summary>
    /// Boss-specific real-time adaptation.
    /// Shifts every 2-3 turns based on recent behavior.
    /// </summary>
    public EnemyIntentType GetBossAdaptedAction(
        int turnNumber,
        PlayerBehaviorProfile profile,
        Deck.SeededRandom random)
    {
        if (turnNumber % 3 != 0) return EnemyIntentType.Unknown; // No change

        // Boss-specific counter-moves
        if (profile.AggressivenessRatio > 0.6f && random.NextDouble() < 0.5)
            return EnemyIntentType.Defend;

        if (profile.BlockingTendency > 0.4f && random.NextDouble() < 0.4)
            return EnemyIntentType.Debuff;

        if (profile.HackingFrequency > 0.2f && random.NextDouble() < 0.3)
            return EnemyIntentType.Buff; // Anti-hack resistance

        return EnemyIntentType.Unknown;
    }

    public void Clear() => _profiles.Clear();
}
