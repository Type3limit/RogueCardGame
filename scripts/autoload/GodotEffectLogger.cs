using Godot;
using RogueCardGame.Core.Utils;

namespace RogueCardGame.Scripts.Autoload;

/// <summary>
/// Bridges Core's IEffectLogger interface to Godot's GD.Print / GD.PrintErr.
/// Registered as an autoload in project.godot so EffectLog.Logger is set early.
/// </summary>
public partial class GodotEffectLogger : Node, IEffectLogger
{
    public override void _EnterTree()
    {
        EffectLog.Logger = this;
    }

    public void Error(string message) => GD.PrintErr(message);
    public void Warn(string message) => GD.PushWarning(message);
    public void Info(string message) => GD.Print(message);
}
