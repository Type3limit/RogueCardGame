using Godot;
using System;

namespace RogueCardGame;

public partial class GameOverScene : Control
{
    private Label _title = null!;
    private Label _statsLabel = null!;
    private Button _retryBtn = null!;
    private Button _menuBtn = null!;
    private ColorRect? _pulseOverlay;
    private float _time;

    public override void _Ready()
    {
        _title = GetNode<Label>("Title");
        _statsLabel = GetNode<Label>("StatsLabel");
        _retryBtn = GetNode<Button>("RetryBtn");
        _menuBtn = GetNode<Button>("MenuBtn");

        _retryBtn.Pressed += OnRetryPressed;
        _menuBtn.Pressed += OnMenuPressed;

        CyberFx.AddScanlines(this, 0.05f);
        CyberFx.AddVignette(this);

        StyleUI();
        ShowStats();
        AnimateEntry();
        AudioManager.Instance?.PlayBgm(AudioManager.BgmPaths.Defeat);
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        if (_pulseOverlay != null)
        {
            float a = 0.04f + 0.02f * MathF.Sin(_time * 1.2f);
            _pulseOverlay.Color = new Color(0.5f, 0f, 0f, a);
        }
    }

    private void StyleUI()
    {
        // Red ambient pulse
        _pulseOverlay = new ColorRect();
        _pulseOverlay.SetAnchorsPreset(LayoutPreset.FullRect);
        _pulseOverlay.Color = new Color(0.5f, 0f, 0f, 0.04f);
        _pulseOverlay.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_pulseOverlay);
        MoveChild(_pulseOverlay, 1);

        // Decorative glitch lines
        AddDeathDecorations();

        // Title
        _title.AddThemeFontSizeOverride("font_size", 60);
        _title.AddThemeColorOverride("font_color", new Color(0.85f, 0.08f, 0.08f));
        _title.AddThemeColorOverride("font_shadow_color", new Color(0.3f, 0f, 0f, 0.5f));

        _statsLabel.AddThemeFontSizeOverride("font_size", 20);
        _statsLabel.AddThemeColorOverride("font_color", new Color(0.65f, 0.65f, 0.7f));

        // Button styling
        var deathRed = new Color(0.7f, 0.12f, 0.12f);
        StyleDeathButton(_retryBtn, deathRed, "▶ 再来一次");
        StyleDeathButton(_menuBtn, new Color(0.35f, 0.35f, 0.4f), "◀ 返回主菜单");
    }

    private void StyleDeathButton(Button btn, Color accent, string text)
    {
        btn.Text = text;
        btn.CustomMinimumSize = new Vector2(280, 56);
        var norm = new StyleBoxFlat
        {
            BgColor = new Color(accent.R * 0.15f, accent.G * 0.15f, accent.B * 0.15f, 0.92f),
            BorderColor = accent * 0.5f,
            BorderWidthBottom = 2, BorderWidthTop = 2,
            BorderWidthLeft = 3, BorderWidthRight = 1,
            CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
            CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8,
            ContentMarginLeft = 24, ContentMarginRight = 24,
            ContentMarginTop = 12, ContentMarginBottom = 12,
            ShadowColor = accent * 0.1f, ShadowSize = 6
        };
        var hover = (StyleBoxFlat)norm.Duplicate();
        hover.BgColor = new Color(accent.R * 0.25f, accent.G * 0.25f, accent.B * 0.25f, 0.95f);
        hover.BorderColor = accent;
        hover.BorderWidthLeft = 5;
        hover.ShadowSize = 12;
        hover.ShadowColor = accent * 0.25f;
        btn.AddThemeStyleboxOverride("normal", norm);
        btn.AddThemeStyleboxOverride("hover", hover);
        btn.AddThemeColorOverride("font_color", new Color(0.75f, 0.75f, 0.8f));
        btn.AddThemeColorOverride("font_hover_color", accent);
        btn.AddThemeFontSizeOverride("font_size", 20);

        btn.MouseEntered += () => btn.CreateTween().TweenProperty(btn, "position:x", btn.Position.X + 6, 0.1f);
        btn.MouseExited += () => btn.CreateTween().TweenProperty(btn, "position:x", btn.Position.X - 6, 0.08f);
    }

    private void AddDeathDecorations()
    {
        // Skull decoration
        var skull = new Label { Text = "☠" };
        skull.AddThemeFontSizeOverride("font_size", 120);
        skull.AddThemeColorOverride("font_color", new Color(0.5f, 0.05f, 0.05f, 0.08f));
        skull.SetAnchorsPreset(LayoutPreset.FullRect);
        skull.AnchorLeft = 0.35f; skull.AnchorRight = 0.65f;
        skull.AnchorTop = 0.05f; skull.AnchorBottom = 0.35f;
        skull.HorizontalAlignment = HorizontalAlignment.Center;
        skull.VerticalAlignment = VerticalAlignment.Center;
        skull.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(skull);

        // Broken line deco
        var lineL = new ColorRect();
        lineL.SetAnchorsPreset(LayoutPreset.FullRect);
        lineL.AnchorLeft = 0.08f; lineL.AnchorRight = 0.38f;
        lineL.AnchorTop = 0.32f; lineL.AnchorBottom = 0.322f;
        lineL.Color = new Color(0.5f, 0.08f, 0.08f, 0.2f);
        lineL.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(lineL);

        var lineR = new ColorRect();
        lineR.SetAnchorsPreset(LayoutPreset.FullRect);
        lineR.AnchorLeft = 0.62f; lineR.AnchorRight = 0.92f;
        lineR.AnchorTop = 0.32f; lineR.AnchorBottom = 0.322f;
        lineR.Color = new Color(0.5f, 0.08f, 0.08f, 0.2f);
        lineR.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(lineR);

        // Error glitch text
        var errText = new Label { Text = "ERR::NEURAL_LINK_SEVERED >> SYNAPTIC_FAILURE 0xDEAD" };
        errText.AddThemeFontSizeOverride("font_size", 10);
        errText.AddThemeColorOverride("font_color", new Color(0.5f, 0.1f, 0.1f, 0.15f));
        errText.SetAnchorsPreset(LayoutPreset.FullRect);
        errText.AnchorLeft = 0.02f; errText.AnchorRight = 0.98f;
        errText.AnchorTop = 0.95f; errText.AnchorBottom = 0.99f;
        errText.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(errText);
    }

    private void AnimateEntry()
    {
        // Title slam in
        _title.Modulate = new Color(1, 1, 1, 0);
        _title.Scale = new Vector2(1.5f, 1.5f);
        _title.PivotOffset = _title.Size / 2;
        var tw = CreateTween();
        tw.TweenProperty(_title, "modulate:a", 1f, 0.4f)
            .SetTrans(Tween.TransitionType.Cubic);
        tw.Parallel().TweenProperty(_title, "scale", Vector2.One, 0.4f)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);

        // Screen shake
        var origPos = Position;
        var shakeTw = CreateTween();
        for (int i = 0; i < 6; i++)
        {
            float intensity = 8f * (1f - i / 6f);
            shakeTw.TweenProperty(this, "position",
                origPos + new Vector2((float)GD.RandRange(-intensity, intensity),
                                     (float)GD.RandRange(-intensity, intensity)), 0.03f);
        }
        shakeTw.TweenProperty(this, "position", origPos, 0.03f);

        // Stats fade in
        _statsLabel.Modulate = new Color(1, 1, 1, 0);
        var stw = CreateTween();
        stw.TweenProperty(_statsLabel, "modulate:a", 1f, 0.5f).SetDelay(0.5f);

        // Buttons stagger
        int idx = 0;
        foreach (var btn in new[] { _retryBtn, _menuBtn })
        {
            btn.Modulate = new Color(1, 1, 1, 0);
            btn.Position += new Vector2(-30, 0);
            var btw = CreateTween();
            btw.TweenProperty(btn, "modulate:a", 1f, 0.3f).SetDelay(0.7f + idx * 0.12f);
            btw.Parallel().TweenProperty(btn, "position:x", btn.Position.X + 30, 0.3f)
                .SetDelay(0.7f + idx * 0.12f)
                .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            idx++;
        }
    }

    private void ShowStats()
    {
        var run = GameManager.Instance.CurrentRun;
        if (run?.Result == null)
        {
            _statsLabel.Text = "没有记录";
            return;
        }

        var r = run.Result;
        _statsLabel.Text = $"职业: {run.Player.Name}\n" +
                          $"到达: 第{r.Act}幕 - 第{r.FloorsCleared}层\n" +
                          $"金币: {r.Gold}\n" +
                          $"卡牌数: {r.CardsInDeck}\n" +
                          $"种子: {r.Seed}";
    }

    private void OnRetryPressed()
    {
        AudioManager.Instance?.PlaySfx(AudioManager.SfxPaths.ButtonClick);
        var lastClass = GameManager.Instance.SelectedClass;
        GameManager.Instance.ClearRun();
        GameManager.Instance.StartNewRun(lastClass);
        SceneManager.Instance.ChangeScene(SceneManager.Scenes.Map);
    }

    private void OnMenuPressed()
    {
        AudioManager.Instance?.PlaySfx(AudioManager.SfxPaths.ButtonClick);
        GameManager.Instance.ClearRun();
        SceneManager.Instance.ChangeScene(SceneManager.Scenes.MainMenu);
    }
}
