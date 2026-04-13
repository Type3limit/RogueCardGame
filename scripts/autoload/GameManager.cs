using Godot;
using System;
using RogueCardGame.Core;
using RogueCardGame.Core.Cards;
using RogueCardGame.Core.Characters;
using RogueCardGame.Core.Run;

namespace RogueCardGame;

/// <summary>
/// Global game state manager. Autoloaded singleton.
/// Holds the current RunState and manages game lifecycle.
/// </summary>
public partial class GameManager : Node
{
    public static GameManager Instance { get; private set; } = null!;

    public RunState? CurrentRun { get; private set; }
    public CardClass SelectedClass { get; set; } = CardClass.Vanguard;
    public string DataDirectory { get; private set; } = "";

    // Settings
    public string Language { get; set; } = "zh_cn";
    public float MasterVolume { get; set; } = 0.8f;
    public float BgmVolume { get; set; } = 0.7f;
    public float SfxVolume { get; set; } = 0.8f;

    public event Action? OnRunStarted;
    public event Action? OnRunEnded;

    public override void _Ready()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;

        // Resolve data directory
        DataDirectory = FindDataDirectory();
        GD.Print($"[GameManager] Data directory: {DataDirectory}");

        // Load balance config
        string balancePath = System.IO.Path.Combine(DataDirectory, "balance", "balance.json");
        BalanceConfig.LoadFromFile(balancePath);
        GD.Print("[GameManager] Balance config loaded");

        // Ensure placeholder audio exists for development
        PlaceholderAudioGenerator.EnsureExists();
    }

    /// <summary>
    /// Start a new run with the selected class.
    /// </summary>
    public void StartNewRun(CardClass playerClass, int? seed = null)
    {
        int runSeed = seed ?? (int)(Time.GetUnixTimeFromSystem() * 1000) % int.MaxValue;
        SelectedClass = playerClass;
        CurrentRun = new RunState(runSeed, playerClass, DataDirectory);
        CurrentRun.StartRun(DataDirectory);
        OnRunStarted?.Invoke();
        GD.Print($"[GameManager] Run started: {playerClass}, Seed: {runSeed}");
    }

    /// <summary>
    /// End the current run.
    /// </summary>
    public void EndCurrentRun(bool victory)
    {
        CurrentRun?.EndRun(victory);
        OnRunEnded?.Invoke();
    }

    /// <summary>
    /// Clean up run state.
    /// </summary>
    public void ClearRun()
    {
        CurrentRun = null;
    }

    private string FindDataDirectory()
    {
        // In Godot, res:// maps to the project root
        // Try the project data directory first
        string resPath = ProjectSettings.GlobalizePath("res://data");
        if (System.IO.Directory.Exists(resPath))
            return resPath;

        // Fallback: search relative to the executable
        string exeDir = System.IO.Path.GetDirectoryName(OS.GetExecutablePath()) ?? ".";
        string dataPath = System.IO.Path.Combine(exeDir, "data");
        if (System.IO.Directory.Exists(dataPath))
            return dataPath;

        // Last fallback: current working directory
        string cwdData = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "data");
        if (System.IO.Directory.Exists(cwdData))
            return cwdData;

        GD.PrintErr("[GameManager] Could not find data directory!");
        return "data";
    }
}
