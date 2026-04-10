using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace RogueCardGame.Core;

// ─────────────────────────────────────────────────────────────
// Localization system: CSV-based translation
// ─────────────────────────────────────────────────────────────

public enum Language
{
    ZhCn, // 简体中文 (default)
    En,   // English
    Ja    // 日本語
}

/// <summary>
/// Manages game localization. Loads translation strings from CSV files.
/// CSV format: key,zh_cn,en,ja
/// </summary>
public class LocalizationManager
{
    private static LocalizationManager? _instance;
    public static LocalizationManager Instance => _instance ??= new LocalizationManager();

    public Language CurrentLanguage { get; private set; } = Language.ZhCn;

    private readonly Dictionary<string, Dictionary<Language, string>> _translations = new();

    /// <summary>
    /// Load translations from a CSV string.
    /// Format: key,zh_cn,en,ja (header row required)
    /// </summary>
    public void LoadFromCsv(string csvContent)
    {
        var lines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2) return;

        // Parse header to determine column mapping
        var header = ParseCsvLine(lines[0]);
        var langMap = new Dictionary<int, Language>();
        for (int i = 1; i < header.Length; i++)
        {
            var col = header[i].Trim().ToLowerInvariant();
            if (col == "zh_cn" || col == "zh") langMap[i] = Language.ZhCn;
            else if (col == "en") langMap[i] = Language.En;
            else if (col == "ja") langMap[i] = Language.Ja;
        }

        // Parse data rows
        for (int row = 1; row < lines.Length; row++)
        {
            var cols = ParseCsvLine(lines[row]);
            if (cols.Length < 2) continue;

            string key = cols[0].Trim();
            if (string.IsNullOrEmpty(key)) continue;

            if (!_translations.ContainsKey(key))
                _translations[key] = new Dictionary<Language, string>();

            foreach (var (colIndex, lang) in langMap)
            {
                if (colIndex < cols.Length && !string.IsNullOrWhiteSpace(cols[colIndex]))
                {
                    _translations[key][lang] = cols[colIndex].Trim();
                }
            }
        }
    }

    /// <summary>
    /// Set the active language.
    /// </summary>
    public void SetLanguage(Language lang)
    {
        CurrentLanguage = lang;
    }

    /// <summary>
    /// Get a translated string by key. Falls back to ZhCn, then returns the key itself.
    /// </summary>
    public string Get(string key)
    {
        if (_translations.TryGetValue(key, out var translations))
        {
            if (translations.TryGetValue(CurrentLanguage, out var text))
                return text;
            if (translations.TryGetValue(Language.ZhCn, out var fallback))
                return fallback;
        }
        return key;
    }

    /// <summary>
    /// Get a formatted translated string with parameters.
    /// </summary>
    public string GetFormatted(string key, params object[] args)
    {
        string template = Get(key);
        return string.Format(template, args);
    }

    /// <summary>
    /// Check if a key exists.
    /// </summary>
    public bool HasKey(string key) => _translations.ContainsKey(key);

    /// <summary>
    /// Get all available keys.
    /// </summary>
    public IEnumerable<string> GetAllKeys() => _translations.Keys;

    private static string[] ParseCsvLine(string line)
    {
        var result = new List<string>();
        bool inQuotes = false;
        var current = new System.Text.StringBuilder();

        foreach (char c in line)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        result.Add(current.ToString());
        return result.ToArray();
    }
}

/// <summary>
/// Shorthand for localization access.
/// Usage: L.Get("ui.start_game") or L.T("ui.start_game")
/// </summary>
public static class L
{
    public static string Get(string key) => LocalizationManager.Instance.Get(key);
    public static string T(string key) => LocalizationManager.Instance.Get(key);
    public static string F(string key, params object[] args) => LocalizationManager.Instance.GetFormatted(key, args);
}
