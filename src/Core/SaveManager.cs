using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RogueCardGame.Core;

// ─────────────────────────────────────────────────────────────
// Save system with checksum validation
// ─────────────────────────────────────────────────────────────

public class SaveData
{
    public string Version { get; set; } = "1.0.0";
    public DateTime SavedAt { get; set; } = DateTime.UtcNow;
    public string Checksum { get; set; } = "";

    // Meta-progression (always saved)
    public string MetaProgressJson { get; set; } = "";

    // Active run (if any)
    public bool HasActiveRun { get; set; }
    public string RunStateJson { get; set; } = "";

    // Settings
    public string SettingsJson { get; set; } = "";
}

public class GameSettings
{
    public float MasterVolume { get; set; } = 1.0f;
    public float MusicVolume { get; set; } = 0.8f;
    public float SfxVolume { get; set; } = 1.0f;
    public string Language { get; set; } = "zh_cn";
    public bool Fullscreen { get; set; } = true;
    public int ResolutionWidth { get; set; } = 1920;
    public int ResolutionHeight { get; set; } = 1080;
    public bool VSync { get; set; } = true;
    public bool ScreenShake { get; set; } = true;
    public float AnimationSpeed { get; set; } = 1.0f;
    public bool ShowDamageNumbers { get; set; } = true;
    public bool ConfirmEndTurn { get; set; } = true;
    public bool AutoEndTurn { get; set; } = false;
}

/// <summary>
/// Manages save/load operations with integrity checking.
/// Save file location: user://saves/ (Godot user directory)
/// </summary>
public class SaveManager
{
    private const string SaveFileName = "save.json";
    private const string MetaSaveFileName = "meta.json";
    private const string SettingsFileName = "settings.json";

    private readonly string _savePath;

    public SaveManager(string basePath)
    {
        _savePath = basePath;
    }

    /// <summary>
    /// Save complete game state.
    /// </summary>
    public bool Save(SaveData data)
    {
        try
        {
            // Compute checksum over payload
            string payload = data.MetaProgressJson + data.RunStateJson;
            data.Checksum = ComputeChecksum(payload);
            data.SavedAt = DateTime.UtcNow;

            string json = JsonSerializer.Serialize(data, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            string fullPath = Path.Combine(_savePath, SaveFileName);
            Directory.CreateDirectory(_savePath);
            File.WriteAllText(fullPath, json);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Load game state. Returns null if file doesn't exist or is corrupted.
    /// </summary>
    public SaveData? Load()
    {
        try
        {
            string fullPath = Path.Combine(_savePath, SaveFileName);
            if (!File.Exists(fullPath)) return null;

            string json = File.ReadAllText(fullPath);
            var data = JsonSerializer.Deserialize<SaveData>(json);
            if (data == null) return null;

            // Validate checksum
            string payload = data.MetaProgressJson + data.RunStateJson;
            string expectedChecksum = ComputeChecksum(payload);
            if (data.Checksum != expectedChecksum)
            {
                // Save file corrupted or tampered
                return null;
            }

            return data;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Save settings independently (doesn't affect game save).
    /// </summary>
    public bool SaveSettings(GameSettings settings)
    {
        try
        {
            string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            string fullPath = Path.Combine(_savePath, SettingsFileName);
            Directory.CreateDirectory(_savePath);
            File.WriteAllText(fullPath, json);
            return true;
        }
        catch { return false; }
    }

    /// <summary>
    /// Load settings.
    /// </summary>
    public GameSettings LoadSettings()
    {
        try
        {
            string fullPath = Path.Combine(_savePath, SettingsFileName);
            if (!File.Exists(fullPath)) return new GameSettings();
            string json = File.ReadAllText(fullPath);
            return JsonSerializer.Deserialize<GameSettings>(json) ?? new GameSettings();
        }
        catch { return new GameSettings(); }
    }

    /// <summary>
    /// Delete the active run save (keeping meta-progress).
    /// </summary>
    public bool DeleteRunSave()
    {
        var data = Load();
        if (data == null) return false;
        data.HasActiveRun = false;
        data.RunStateJson = "";
        return Save(data);
    }

    /// <summary>
    /// Check if a save file exists.
    /// </summary>
    public bool HasSave()
    {
        return File.Exists(Path.Combine(_savePath, SaveFileName));
    }

    /// <summary>
    /// Check if there's an active run in the save.
    /// </summary>
    public bool HasActiveRun()
    {
        var data = Load();
        return data?.HasActiveRun ?? false;
    }

    private static string ComputeChecksum(string data)
    {
        using var sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(data + "RogueCardGame_Salt_2024"));
        return Convert.ToBase64String(hash);
    }
}
