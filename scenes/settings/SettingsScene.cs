using Godot;
using RogueCardGame.Core;

namespace RogueCardGame;

/// <summary>
/// Settings screen for volume, language, and display options.
/// </summary>
public partial class SettingsScene : Control
{
    private VBoxContainer _container = null!;
    private Button _backBtn = null!;
    private Label _title = null!;

    // Sliders
    private HSlider _masterSlider = null!;
    private HSlider _bgmSlider = null!;
    private HSlider _sfxSlider = null!;

    public override void _Ready()
    {
        _title = GetNode<Label>("Title");
        _container = GetNode<VBoxContainer>("SettingsContainer");
        _backBtn = GetNode<Button>("BackBtn");

        _backBtn.Pressed += OnBackPressed;

        CyberFx.AddScanlines(this, 0.03f);
        CyberFx.AddVignette(this);

        StyleUI();
        BuildSettings();
        AnimateEntry();
    }

    private void StyleUI()
    {
        _title.Text = "⚙ 设 置 ⚙";
        _title.AddThemeFontSizeOverride("font_size", 42);
        _title.AddThemeColorOverride("font_color", new Color(0f, 0.95f, 0.95f));
        _title.AddThemeColorOverride("font_shadow_color", new Color(0f, 0.3f, 0.4f, 0.3f));

        var cyan = new Color(0f, 0.85f, 0.85f);
        var btnStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.06f, 0.08f, 0.14f, 0.9f),
            BorderColor = cyan * 0.5f,
            BorderWidthBottom = 2, BorderWidthTop = 2,
            BorderWidthLeft = 3, BorderWidthRight = 1,
            CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
            CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8,
            ContentMarginLeft = 24, ContentMarginRight = 24,
            ContentMarginTop = 10, ContentMarginBottom = 10,
            ShadowColor = cyan * 0.1f, ShadowSize = 6
        };
        var btnHover = (StyleBoxFlat)btnStyle.Duplicate();
        btnHover.BgColor = new Color(0.1f, 0.15f, 0.22f, 0.95f);
        btnHover.BorderColor = cyan;
        btnHover.BorderWidthLeft = 5;
        btnHover.ShadowSize = 12;
        btnHover.ShadowColor = cyan * 0.2f;
        _backBtn.AddThemeStyleboxOverride("normal", btnStyle);
        _backBtn.AddThemeStyleboxOverride("hover", btnHover);
        _backBtn.AddThemeColorOverride("font_color", new Color(0.75f, 0.75f, 0.8f));
        _backBtn.AddThemeColorOverride("font_hover_color", cyan);
        _backBtn.AddThemeFontSizeOverride("font_size", 20);
        _backBtn.Text = "◀ 返回主菜单";

        // Decorative line under title
        var lineDiv = new ColorRect();
        lineDiv.SetAnchorsPreset(LayoutPreset.FullRect);
        lineDiv.AnchorLeft = 0.2f; lineDiv.AnchorRight = 0.8f;
        lineDiv.AnchorTop = 0.16f; lineDiv.AnchorBottom = 0.162f;
        lineDiv.Color = cyan * 0.12f;
        lineDiv.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(lineDiv);

        // System text
        var sysText = new Label { Text = ">> SYSTEM_CONFIG :: AUDIO_SUBSYS :: DISPLAY_CTRL" };
        sysText.AddThemeFontSizeOverride("font_size", 10);
        sysText.AddThemeColorOverride("font_color", cyan * 0.1f);
        sysText.SetAnchorsPreset(LayoutPreset.FullRect);
        sysText.AnchorLeft = 0.02f; sysText.AnchorRight = 0.98f;
        sysText.AnchorTop = 0.96f; sysText.AnchorBottom = 0.99f;
        sysText.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(sysText);
    }

    private void BuildSettings()
    {
        var gm = GameManager.Instance;

        // Master Volume
        _masterSlider = AddSliderRow("主音量", gm.MasterVolume, v =>
        {
            gm.MasterVolume = v;
            AudioServer.SetBusVolumeDb(0, Mathf.LinearToDb(v));
        });

        // BGM Volume
        _bgmSlider = AddSliderRow("音乐音量", gm.BgmVolume, v =>
        {
            gm.BgmVolume = v;
            AudioManager.Instance?.SetBgmVolume(v);
        });

        // SFX Volume
        _sfxSlider = AddSliderRow("音效音量", gm.SfxVolume, v =>
        {
            gm.SfxVolume = v;
            AudioManager.Instance?.SetSfxVolume(v);
        });

        // Fullscreen toggle
        var fsRow = new HBoxContainer();
        var fsLabel = new Label { Text = "全屏", CustomMinimumSize = new Vector2(200, 0) };
        fsLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.9f, 0.9f));
        fsLabel.AddThemeFontSizeOverride("font_size", 18);
        var fsCheck = new CheckButton();
        fsCheck.ButtonPressed = DisplayServer.WindowGetMode() == DisplayServer.WindowMode.Fullscreen;
        fsCheck.Toggled += on =>
        {
            DisplayServer.WindowSetMode(on
                ? DisplayServer.WindowMode.Fullscreen
                : DisplayServer.WindowMode.Windowed);
        };
        fsRow.AddChild(fsLabel);
        fsRow.AddChild(fsCheck);
        _container.AddChild(fsRow);
    }

    private HSlider AddSliderRow(string label, float initialValue, System.Action<float> onChange)
    {
        var row = new HBoxContainer();
        var lbl = new Label { Text = label, CustomMinimumSize = new Vector2(200, 0) };
        lbl.AddThemeColorOverride("font_color", new Color(0.8f, 0.9f, 0.9f));
        lbl.AddThemeFontSizeOverride("font_size", 18);

        var slider = new HSlider
        {
            MinValue = 0,
            MaxValue = 1,
            Step = 0.05,
            Value = initialValue,
            CustomMinimumSize = new Vector2(250, 30)
        };

        var valLabel = new Label { Text = $"{(int)(initialValue * 100)}%" };
        valLabel.AddThemeColorOverride("font_color", new Color(0f, 0.95f, 0.95f));
        valLabel.AddThemeFontSizeOverride("font_size", 16);

        slider.ValueChanged += v =>
        {
            onChange((float)v);
            valLabel.Text = $"{(int)(v * 100)}%";
        };

        row.AddChild(lbl);
        row.AddChild(slider);
        row.AddChild(valLabel);
        _container.AddChild(row);

        return slider;
    }

    private void OnBackPressed()
    {
        AudioManager.Instance?.PlaySfx(AudioManager.SfxPaths.ButtonClick);
        SceneManager.Instance.ChangeScene(SceneManager.Scenes.MainMenu);
    }

    private void AnimateEntry()
    {
        // Title fade in
        _title.Modulate = new Color(1, 1, 1, 0);
        var ttw = CreateTween();
        ttw.TweenProperty(_title, "modulate:a", 1f, 0.35f)
            .SetTrans(Tween.TransitionType.Cubic);

        // Settings rows stagger
        int idx = 0;
        foreach (var child in _container.GetChildren())
        {
            if (child is Control ctrl)
            {
                ctrl.Modulate = new Color(1, 1, 1, 0);
                ctrl.Position += new Vector2(-20, 0);
                var tw = CreateTween();
                tw.TweenProperty(ctrl, "modulate:a", 1f, 0.25f).SetDelay(0.15f + idx * 0.08f);
                tw.Parallel().TweenProperty(ctrl, "position:x", ctrl.Position.X + 20, 0.25f)
                    .SetDelay(0.15f + idx * 0.08f)
                    .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
                idx++;
            }
        }

        // Back button
        _backBtn.Modulate = new Color(1, 1, 1, 0);
        var btw = CreateTween();
        btw.TweenProperty(_backBtn, "modulate:a", 1f, 0.3f).SetDelay(0.4f + idx * 0.08f);
    }
}
