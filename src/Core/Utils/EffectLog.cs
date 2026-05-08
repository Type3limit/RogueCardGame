namespace RogueCardGame.Core.Utils;

/// <summary>
/// Pluggable logger interface for Core layer. Keeps src/Core free of Godot dependencies.
/// Godot-side initialization injects a GodotEffectLogger implementation.
/// </summary>
public interface IEffectLogger
{
    void Error(string message);
    void Warn(string message);
    void Info(string message);
}

/// <summary>Static access point for the injected logger. Silently no-ops when not injected (e.g. in unit tests).</summary>
public static class EffectLog
{
    public static IEffectLogger? Logger { get; set; }

    public static void Error(string message) => Logger?.Error(message);
    public static void Warn(string message) => Logger?.Warn(message);
    public static void Info(string message) => Logger?.Info(message);
}
