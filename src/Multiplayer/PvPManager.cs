using System;
using System.Collections.Generic;
using System.Linq;

namespace RogueCardGame.Multiplayer;

// ─────────────────────────────────────────────────────────────
// PvP Manager — 1v1 and 2v2 competitive modes
// ─────────────────────────────────────────────────────────────

public enum PvPMode
{
    Duel1v1,
    Team2v2
}

public enum PvPPhase
{
    BanPick,      // Character/card selection phase
    Setup,        // Choose starting position
    Combat,       // Actual combat
    Result        // Win/lose screen
}

public class PvPTeam
{
    public int TeamId { get; set; }
    public List<int> PlayerPeerIds { get; set; } = new();
    public bool IsFrontRowAlive { get; set; } = true;
}

/// <summary>
/// Manages PvP-specific logic including:
/// - 1v1 duel rules (front/back row tradeoffs for single player)
/// - 2v2 team combat (front/back row with team synergy)
/// - Independent PvP number layer (damage/block values scaled separately)
/// - Hack → "Disrupt" conversion for PvP (disable next card, not mind control)
/// </summary>
public class PvPManager
{
    public PvPMode Mode { get; private set; }
    public PvPPhase Phase { get; private set; } = PvPPhase.BanPick;

    private readonly NetworkManager _network;
    private readonly PvPTeam[] _teams = new PvPTeam[2];

    // PvP balance multipliers (independent from PvE)
    public float PvPDamageMultiplier { get; set; } = 0.75f; // PvP damage reduced
    public float PvPBlockMultiplier { get; set; } = 1.0f;
    public float PvPHealMultiplier { get; set; } = 0.8f;
    public int PvPBackRowDamageReduction { get; set; } = 30; // % reduction when attacking back row with front alive
    public int PvPMaxTurns { get; set; } = 30; // Sudden death after this

    public event Action<PvPPhase>? OnPhaseChanged;
    public event Action<int>? OnTeamWin; // teamId
    public event Action? OnSuddenDeath;

    public PvPManager(NetworkManager network)
    {
        _network = network;
        _teams[0] = new PvPTeam { TeamId = 0 };
        _teams[1] = new PvPTeam { TeamId = 1 };
    }

    /// <summary>
    /// Initialize PvP match.
    /// </summary>
    public void InitMatch(PvPMode mode, List<int> team1Peers, List<int> team2Peers)
    {
        Mode = mode;
        Phase = PvPPhase.BanPick;

        _teams[0].PlayerPeerIds = new List<int>(team1Peers);
        _teams[1].PlayerPeerIds = new List<int>(team2Peers);
        _teams[0].IsFrontRowAlive = true;
        _teams[1].IsFrontRowAlive = true;

        OnPhaseChanged?.Invoke(Phase);
    }

    /// <summary>
    /// Apply PvP damage scaling.
    /// </summary>
    public int ScalePvPDamage(int baseDamage, int attackerTeam, int targetTeam,
        bool attackerFrontRow, bool targetFrontRow)
    {
        float scaled = baseDamage * PvPDamageMultiplier;

        // Back row damage reduction if front row is alive
        if (!targetFrontRow && _teams[targetTeam].IsFrontRowAlive)
        {
            scaled *= (100f - PvPBackRowDamageReduction) / 100f;
        }

        // Back row attacker gets reduced melee damage
        if (!attackerFrontRow)
        {
            // Melee penalty already handled by formation system
        }

        return Math.Max(1, (int)Math.Round(scaled));
    }

    /// <summary>
    /// Apply PvP block scaling.
    /// </summary>
    public int ScalePvPBlock(int baseBlock)
    {
        return Math.Max(0, (int)Math.Round(baseBlock * PvPBlockMultiplier));
    }

    /// <summary>
    /// Apply PvP heal scaling.
    /// </summary>
    public int ScalePvPHeal(int baseHeal)
    {
        return Math.Max(1, (int)Math.Round(baseHeal * PvPHealMultiplier));
    }

    /// <summary>
    /// In PvP, Hack becomes "Disrupt" — disables opponent's next card play
    /// instead of mind-controlling enemies.
    /// </summary>
    public int ConvertHackToDisrupt(int hackProgress)
    {
        // Every 5 hack progress = 1 turn of card disruption
        return hackProgress / 5;
    }

    /// <summary>
    /// Check if a team's front row is eliminated.
    /// </summary>
    public void UpdateFrontRowStatus(int teamId, bool hasFrontRowAlive)
    {
        _teams[teamId].IsFrontRowAlive = hasFrontRowAlive;
    }

    /// <summary>
    /// Check win condition after a player is eliminated.
    /// </summary>
    public int CheckWinCondition()
    {
        // A team loses when all their players are eliminated
        for (int i = 0; i < 2; i++)
        {
            // If opposing team has no players left, team i wins
            int opponent = 1 - i;
            if (_teams[opponent].PlayerPeerIds.Count == 0)
            {
                Phase = PvPPhase.Result;
                OnTeamWin?.Invoke(i);
                return i;
            }
        }
        return -1; // No winner yet
    }

    /// <summary>
    /// Remove an eliminated player from their team.
    /// </summary>
    public void EliminatePlayer(int peerId)
    {
        foreach (var team in _teams)
        {
            team.PlayerPeerIds.Remove(peerId);
        }
    }

    /// <summary>
    /// Check sudden death condition (turn limit exceeded).
    /// </summary>
    public bool CheckSuddenDeath(int turnNumber)
    {
        if (turnNumber >= PvPMaxTurns)
        {
            OnSuddenDeath?.Invoke();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Advance PvP phase.
    /// </summary>
    public void AdvancePhase()
    {
        Phase = Phase switch
        {
            PvPPhase.BanPick => PvPPhase.Setup,
            PvPPhase.Setup => PvPPhase.Combat,
            PvPPhase.Combat => PvPPhase.Result,
            _ => Phase
        };
        OnPhaseChanged?.Invoke(Phase);
    }

    public PvPTeam GetTeam(int teamId) => _teams[teamId];

    public int GetPlayerTeam(int peerId)
    {
        if (_teams[0].PlayerPeerIds.Contains(peerId)) return 0;
        if (_teams[1].PlayerPeerIds.Contains(peerId)) return 1;
        return -1;
    }
}
