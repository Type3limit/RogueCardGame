using Godot;
using System;

namespace RogueCardGame;

/// <summary>
/// Global scene transition manager. Autoloaded singleton.
/// Handles scene changes with fade in/out animation.
/// </summary>
public partial class SceneManager : Node
{
    public static SceneManager Instance { get; private set; } = null!;

    private ColorRect _fadeOverlay = null!;
    private AnimationPlayer _animPlayer = null!;
    private string _pendingScene = "";
    private bool _isTransitioning;

    public event Action? OnSceneChangeStarted;
    public event Action? OnSceneChangeCompleted;

    // Scene paths
    public static class Scenes
    {
        public const string MainMenu = "res://scenes/menus/MainMenu.tscn";
        public const string Combat = "res://scenes/combat/Combat.tscn";
        public const string Map = "res://scenes/map/Map.tscn";
        public const string Shop = "res://scenes/shop/Shop.tscn";
        public const string Event = "res://scenes/event/Event.tscn";
        public const string Rest = "res://scenes/rest/Rest.tscn";
        public const string Reward = "res://scenes/reward/Reward.tscn";
        public const string GameOver = "res://scenes/menus/GameOver.tscn";
        public const string Victory = "res://scenes/menus/Victory.tscn";
        public const string Settings = "res://scenes/settings/Settings.tscn";
    }

    public override void _Ready()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;

        // Create fade overlay
        var canvas = new CanvasLayer { Layer = 100 };
        AddChild(canvas);

        _fadeOverlay = new ColorRect
        {
            Color = new Color(0, 0, 0, 0),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            AnchorsPreset = (int)Control.LayoutPreset.FullRect
        };
        // Set full rect anchors
        _fadeOverlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        canvas.AddChild(_fadeOverlay);

        // Create animation player for fade
        _animPlayer = new AnimationPlayer();
        AddChild(_animPlayer);
        CreateFadeAnimations();

        _animPlayer.AnimationFinished += OnAnimationFinished;
    }

    /// <summary>
    /// Change scene with fade transition.
    /// </summary>
    public void ChangeScene(string scenePath)
    {
        if (_isTransitioning) return;

        if (!ResourceLoader.Exists(scenePath))
        {
            GD.PrintErr($"[SceneManager] Scene not found: {scenePath}");
            return;
        }

        _isTransitioning = true;
        _pendingScene = scenePath;
        _fadeOverlay.MouseFilter = Control.MouseFilterEnum.Stop;
        OnSceneChangeStarted?.Invoke();

        _animPlayer.Play("fade_out");
    }

    /// <summary>
    /// Change scene immediately without transition.
    /// </summary>
    public void ChangeSceneImmediate(string scenePath)
    {
        GetTree().ChangeSceneToFile(scenePath);
    }

    private void OnAnimationFinished(StringName animName)
    {
        if (animName == "fade_out" && !string.IsNullOrEmpty(_pendingScene))
        {
            GetTree().ChangeSceneToFile(_pendingScene);
            _pendingScene = "";
            _animPlayer.Play("fade_in");
        }
        else if (animName == "fade_in")
        {
            _isTransitioning = false;
            _fadeOverlay.MouseFilter = Control.MouseFilterEnum.Ignore;
            OnSceneChangeCompleted?.Invoke();
        }
    }

    private void CreateFadeAnimations()
    {
        var lib = new AnimationLibrary();

        // Fade out (transparent -> black)
        var fadeOut = new Animation();
        fadeOut.Length = 0.4f;
        int trackOut = fadeOut.AddTrack(Animation.TrackType.Value);
        fadeOut.TrackSetPath(trackOut, _fadeOverlay.GetPath() + ":color:a");
        fadeOut.TrackInsertKey(trackOut, 0.0f, 0.0f);
        fadeOut.TrackInsertKey(trackOut, 0.4f, 1.0f);
        lib.AddAnimation("fade_out", fadeOut);

        // Fade in (black -> transparent)
        var fadeIn = new Animation();
        fadeIn.Length = 0.4f;
        int trackIn = fadeIn.AddTrack(Animation.TrackType.Value);
        fadeIn.TrackSetPath(trackIn, _fadeOverlay.GetPath() + ":color:a");
        fadeIn.TrackInsertKey(trackIn, 0.0f, 1.0f);
        fadeIn.TrackInsertKey(trackIn, 0.4f, 0.0f);
        lib.AddAnimation("fade_in", fadeIn);

        _animPlayer.AddAnimationLibrary("", lib);
    }
}
