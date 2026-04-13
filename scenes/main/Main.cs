using Godot;

namespace RogueCardGame;

/// <summary>
/// Entry point scene. Redirects to MainMenu.
/// </summary>
public partial class Main : Node
{
	public override void _Ready()
	{
		GD.Print("[Main] Game starting...");
		// Defer to next frame so autoloads are fully initialized
		CallDeferred(nameof(GoToMainMenu));
	}

	private void GoToMainMenu()
	{
		GetTree().ChangeSceneToFile(SceneManager.Scenes.MainMenu);
	}
}
