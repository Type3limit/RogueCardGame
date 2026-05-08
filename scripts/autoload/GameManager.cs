using Godot;
using System;
using System.Linq;
using RogueCardGame.Core;
using RogueCardGame.Core.Cards;
using RogueCardGame.Core.Characters;
using RogueCardGame.Core.Progression;
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

    public MetaProgress MetaProgress { get; private set; } = new();
    private SaveManager? _saveManager;

    public bool HasActiveRunSave() => _saveManager?.HasActiveRun() ?? false;

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

        // Init save system and load meta-progress
        string savePath = ProjectSettings.GlobalizePath("user://saves/");
        _saveManager = new SaveManager(savePath);
        var saveData = _saveManager.Load();
        if (saveData != null && saveData.MetaProgressJson.Length > 0)
        {
            MetaProgress = MetaProgress.Deserialize(saveData.MetaProgressJson);
            GD.Print("[GameManager] MetaProgress loaded");
        }

        if (saveData?.HasActiveRun == true && !string.IsNullOrWhiteSpace(saveData.RunStateJson))
        {
            try
            {
                CurrentRun = RunState.Deserialize(saveData.RunStateJson, DataDirectory);
                SelectedClass = CurrentRun.Player.Class;
                GD.Print("[GameManager] Active run loaded");
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[GameManager] Failed to load active run: {ex.Message}");
                _saveManager.DeleteRunSave();
                CurrentRun = null;
            }
        }

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
        SaveCurrentRun();
        OnRunStarted?.Invoke();
        GD.Print($"[GameManager] Run started: {playerClass}, Seed: {runSeed}");
    }

    public bool SaveCurrentRun()
    {
        if (_saveManager == null || CurrentRun == null || !CurrentRun.IsRunActive)
            return false;

        var existingSave = _saveManager.Load() ?? new SaveData();
        existingSave.MetaProgressJson = MetaProgress.Serialize();
        existingSave.HasActiveRun = true;
        existingSave.RunStateJson = CurrentRun.Serialize();
        return _saveManager.Save(existingSave);
    }

    public bool SetCurrentRunScene(string sceneId)
    {
        if (CurrentRun == null)
            return false;

        CurrentRun.CurrentSceneId = sceneId;
        return SaveCurrentRun();
    }

    public bool TryLoadActiveRun()
    {
        if (_saveManager == null)
            return false;

        var saveData = _saveManager.Load();
        if (saveData?.HasActiveRun != true || string.IsNullOrWhiteSpace(saveData.RunStateJson))
            return false;

        CurrentRun = RunState.Deserialize(saveData.RunStateJson, DataDirectory);
        SelectedClass = CurrentRun.Player.Class;
        return true;
    }

    /// <summary>
    /// End the current run.
    /// </summary>
    public void EndCurrentRun(bool victory)
    {
        if (CurrentRun != null)
        {
            var run = CurrentRun;
            run.EndRun(victory);

            // Record run into meta-progress and persist
            if (_saveManager != null)
            {
                var record = new RunRecord
                {
                    ClassName = run.Player.Class.ToString().ToLowerInvariant(),
                    Victory = victory,
                    FloorReached = run.FloorsCleared,
                    GoldEarned = run.Gold,
                    ImplantsUsed = run.Implants.GetAllEquipped().Select(i => i.Data.Id).ToList()
                };
                MetaProgress.RecordRun(record);

                var existingSave = _saveManager.Load() ?? new SaveData();
                existingSave.MetaProgressJson = MetaProgress.Serialize();
                existingSave.HasActiveRun = false;
                existingSave.RunStateJson = "";
                _saveManager.Save(existingSave);
                GD.Print("[GameManager] Run recorded and saved");
            }
        }
        CurrentRun = null;
        OnRunEnded?.Invoke();
    }

    /// <summary>
    /// Clean up run state.
    /// </summary>
    public void ClearRun()
    {
        CurrentRun = null;
        _saveManager?.DeleteRunSave();
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
