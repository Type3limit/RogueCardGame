using Godot;
using System;
using System.Collections.Generic;

namespace RogueCardGame;

/// <summary>
/// Global audio manager. Autoloaded singleton.
/// Handles BGM playback with crossfade, and SFX playback.
/// All audio paths are configurable for easy resource replacement.
/// </summary>
public partial class AudioManager : Node
{
    public static AudioManager Instance { get; private set; } = null!;

    private AudioStreamPlayer _bgmPlayer = null!;
    private AudioStreamPlayer _bgmFadePlayer = null!;
    private readonly List<AudioStreamPlayer> _sfxPool = [];

    private const int SfxPoolSize = 8;
    private const float CrossfadeDuration = 1.5f;
    private float _crossfadeTimer;
    private bool _isCrossfading;
    private string _currentBgm = "";
    private float _bgmVolumeDb = 0f;

    // Audio bus volumes
    public float MasterVolume
    {
        get => Mathf.DbToLinear(AudioServer.GetBusVolumeDb(0));
        set => AudioServer.SetBusVolumeDb(0, Mathf.LinearToDb(Mathf.Clamp(value, 0f, 1f)));
    }

    // BGM paths - easily replaceable
    public static class BgmPaths
    {
        public const string MainMenu = "res://resources/audio/bgm/main_menu.ogg";
        public const string Map = "res://resources/audio/bgm/map.ogg";
        public const string Combat = "res://resources/audio/bgm/combat.ogg";
        public const string Boss = "res://resources/audio/bgm/boss.ogg";
        public const string Shop = "res://resources/audio/bgm/shop.ogg";
        public const string Event = "res://resources/audio/bgm/event.ogg";
        public const string Victory = "res://resources/audio/bgm/victory.ogg";
        public const string Defeat = "res://resources/audio/bgm/defeat.ogg";
    }

    // SFX paths - easily replaceable
    public static class SfxPaths
    {
        public const string CardPlay = "res://resources/audio/sfx/card_play.ogg";
        public const string CardSelect = "res://resources/audio/sfx/card_select.ogg";
        public const string CardDraw = "res://resources/audio/sfx/card_draw.ogg";
        public const string CardDiscard = "res://resources/audio/sfx/card_discard.ogg";
        public const string AttackHit = "res://resources/audio/sfx/attack_hit.ogg";
        public const string BlockGain = "res://resources/audio/sfx/block_gain.ogg";
        public const string Heal = "res://resources/audio/sfx/heal.ogg";
        public const string StatusApply = "res://resources/audio/sfx/status_apply.ogg";
        public const string ButtonClick = "res://resources/audio/sfx/button_click.ogg";
        public const string ButtonHover = "res://resources/audio/sfx/button_hover.ogg";
        public const string TurnEnd = "res://resources/audio/sfx/turn_end.ogg";
        public const string EnemyAttack = "res://resources/audio/sfx/enemy_attack.ogg";
        public const string GoldGain = "res://resources/audio/sfx/gold_gain.ogg";
        public const string MapNodeSelect = "res://resources/audio/sfx/map_node.ogg";
    }

    public override void _Ready()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;

        // Create BGM players (loop enabled)
        _bgmPlayer = new AudioStreamPlayer { Bus = "Master", Autoplay = false };
        AddChild(_bgmPlayer);
        _bgmPlayer.Finished += OnBgmFinished;
        _bgmFadePlayer = new AudioStreamPlayer { Bus = "Master", Autoplay = false };
        AddChild(_bgmFadePlayer);

        // Create SFX pool
        for (int i = 0; i < SfxPoolSize; i++)
        {
            var sfx = new AudioStreamPlayer { Bus = "Master" };
            AddChild(sfx);
            _sfxPool.Add(sfx);
        }
    }

    /// <summary>BGM finished — restart for seamless looping.</summary>
    private void OnBgmFinished()
    {
        if (!string.IsNullOrEmpty(_currentBgm) && _bgmPlayer.Stream != null)
            _bgmPlayer.Play();
    }

    public override void _Process(double delta)
    {
        if (!_isCrossfading) return;

        _crossfadeTimer += (float)delta;
        float t = Mathf.Clamp(_crossfadeTimer / CrossfadeDuration, 0f, 1f);

        _bgmFadePlayer.VolumeDb = Mathf.LinearToDb(1f - t) + _bgmVolumeDb;
        _bgmPlayer.VolumeDb = Mathf.LinearToDb(t) + _bgmVolumeDb;

        if (t >= 1f)
        {
            _isCrossfading = false;
            _bgmFadePlayer.Stop();
            _bgmPlayer.VolumeDb = _bgmVolumeDb;
        }
    }

    /// <summary>
    /// Play BGM with crossfade. If the same track is already playing, do nothing.
    /// </summary>
    public void PlayBgm(string path)
    {
        if (path == _currentBgm && _bgmPlayer.Playing) return;

        var stream = TryLoadAudio(path);
        if (stream == null) return;

        if (_bgmPlayer.Playing)
        {
            // Crossfade
            _bgmFadePlayer.Stream = _bgmPlayer.Stream;
            _bgmFadePlayer.VolumeDb = _bgmPlayer.VolumeDb;
            _bgmFadePlayer.Play(_bgmPlayer.GetPlaybackPosition());

            _bgmPlayer.Stream = stream;
            _bgmPlayer.VolumeDb = Mathf.LinearToDb(0.001f);
            _bgmPlayer.Play();

            _crossfadeTimer = 0f;
            _isCrossfading = true;
        }
        else
        {
            _bgmPlayer.Stream = stream;
            _bgmPlayer.VolumeDb = _bgmVolumeDb;
            _bgmPlayer.Play();
        }

        _currentBgm = path;
    }

    /// <summary>
    /// Stop BGM with optional fade out.
    /// </summary>
    public void StopBgm()
    {
        _bgmPlayer.Stop();
        _bgmFadePlayer.Stop();
        _isCrossfading = false;
        _currentBgm = "";
    }

    /// <summary>
    /// Play a one-shot sound effect.
    /// </summary>
    public void PlaySfx(string path)
    {
        var stream = TryLoadAudio(path);
        if (stream == null) return;

        // Find an available player from the pool
        foreach (var player in _sfxPool)
        {
            if (!player.Playing)
            {
                player.Stream = stream;
                player.Play();
                return;
            }
        }

        // All busy, use the first one (oldest)
        _sfxPool[0].Stream = stream;
        _sfxPool[0].Play();
    }

    private static AudioStream? TryLoadAudio(string path)
    {
        if (ResourceLoader.Exists(path))
            return GD.Load<AudioStream>(path);

        // Try .wav fallback if .ogg not found
        string wavPath = path.Replace(".ogg", ".wav");
        if (wavPath != path && ResourceLoader.Exists(wavPath))
            return GD.Load<AudioStream>(wavPath);

        // Silent fail - audio not yet created
        return null;
    }

    /// <summary>
    /// Set BGM volume (0-1 linear scale).
    /// </summary>
    public void SetBgmVolume(float volume)
    {
        _bgmVolumeDb = Mathf.LinearToDb(Mathf.Clamp(volume, 0.001f, 1f));
        if (!_isCrossfading)
            _bgmPlayer.VolumeDb = _bgmVolumeDb;
    }

    /// <summary>
    /// Set SFX volume (0-1 linear scale).
    /// </summary>
    public void SetSfxVolume(float volume)
    {
        float db = Mathf.LinearToDb(Mathf.Clamp(volume, 0f, 1f));
        foreach (var player in _sfxPool)
            player.VolumeDb = db;
    }
}
