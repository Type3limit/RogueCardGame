namespace RogueCardGame.Core.Combat;

/// <summary>
/// Combat state machine phases.
/// </summary>
public enum CombatPhase
{
    NotStarted,
    PlayerPlanningPhase,  // Players simultaneously plan card plays
    PlayerResolutionPhase, // Cards resolve in speed order
    EnemyPhase,           // Enemies execute intents
    TurnEndPhase,         // Status effects tick, environment effects
    Victory,
    Defeat
}

/// <summary>
/// Manages the energy system for a combat encounter.
/// </summary>
public class EnergySystem
{
    public int MaxEnergy { get; set; } = 3;
    public int CurrentEnergy { get; set; }

    public void RefreshEnergy()
    {
        CurrentEnergy = MaxEnergy;
    }

    public bool CanSpend(int amount) => CurrentEnergy >= amount;

    public bool TrySpend(int amount)
    {
        if (CurrentEnergy < amount) return false;
        CurrentEnergy -= amount;
        return true;
    }

    public void Gain(int amount)
    {
        CurrentEnergy += amount;
    }
}

/// <summary>
/// Manages turn sequencing and phase transitions in combat.
/// </summary>
public class TurnSystem
{
    public int TurnNumber { get; private set; }
    public CombatPhase CurrentPhase { get; private set; } = CombatPhase.NotStarted;

    public event Action<CombatPhase>? OnPhaseChanged;
    public event Action<int>? OnTurnStarted;
    public event Action<int>? OnTurnEnded;

    public void StartCombat()
    {
        TurnNumber = 0;
        TransitionTo(CombatPhase.PlayerPlanningPhase);
        StartNewTurn();
    }

    public void StartNewTurn()
    {
        TurnNumber++;
        OnTurnStarted?.Invoke(TurnNumber);
        TransitionTo(CombatPhase.PlayerPlanningPhase);
    }

    public void AdvancePhase()
    {
        var next = CurrentPhase switch
        {
            CombatPhase.NotStarted => CombatPhase.PlayerPlanningPhase,
            CombatPhase.PlayerPlanningPhase => CombatPhase.PlayerResolutionPhase,
            CombatPhase.PlayerResolutionPhase => CombatPhase.EnemyPhase,
            CombatPhase.EnemyPhase => CombatPhase.TurnEndPhase,
            CombatPhase.TurnEndPhase => CombatPhase.PlayerPlanningPhase,
            _ => CurrentPhase
        };

        if (next == CombatPhase.PlayerPlanningPhase && CurrentPhase == CombatPhase.TurnEndPhase)
        {
            OnTurnEnded?.Invoke(TurnNumber);
            StartNewTurn();
        }
        else
        {
            TransitionTo(next);
        }
    }

    public void EndCombat(bool victory)
    {
        TransitionTo(victory ? CombatPhase.Victory : CombatPhase.Defeat);
    }

    private void TransitionTo(CombatPhase phase)
    {
        if (CurrentPhase == phase) return;
        CurrentPhase = phase;
        OnPhaseChanged?.Invoke(phase);
    }
}
