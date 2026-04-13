using Godot;
using System;

namespace RogueCardGame;

public partial class VictoryScene : Control
{
    private Label _title = null!;
    private Label _statsLabel = null!;
    private Button _menuBtn = null!;
    private float _time;

    public override void _Ready()
    {
        _title = GetNode<Label>("Title");
        _statsLabel = GetNode<Label>("StatsLabel");
        _menuBtn = GetNode<Button>("MenuBtn");

        _menuBtn.Pressed += OnMenuPressed;

        CyberFx.AddScanlines(this, 0.03f);
        CyberFx.AddVignette(this);

        StyleUI();
        AddDecorations();
        ShowStats();
        AnimateEntry();
        AudioManager.Instance?.PlayBgm(AudioManager.BgmPaths.Victory);
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
    }

    private void StyleUI()
    {
        var cyan = new Color(0f, 1f, 0.85f);
        var gold = new Color(1f, 0.85f, 0.3f);

        _title.AddThemeFontSizeOverride("font_size", 64);
        _title.AddThemeColorOverride("font_color", gold);
        _title.AddThemeColorOverride("font_shadow_color", new Color(0.8f, 0.6f, 0f, 0.3f));

        _statsLabel.AddThemeFontSizeOverride("font_size", 20);
        _statsLabel.AddThemeColorOverride("font_color", new Color(0.82f, 0.88f, 0.92f));

        // Styled button
        _menuBtn.Text = "◀ 返回主菜单";
        _menuBtn.CustomMinimumSize = new Vector2(280, 56);
        var norm = new StyleBoxFlat
        {
            BgColor = new Color(0.04f, 0.12f, 0.12f, 0.92f),
            BorderColor = cyan * 0.5f,
            BorderWidthBottom = 2, BorderWidthTop = 2,
            BorderWidthLeft = 3, BorderWidthRight = 1,
            CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
            CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8,
            ContentMarginLeft = 24, ContentMarginRight = 24,
            ContentMarginTop = 12, ContentMarginBottom = 12,
            ShadowColor = cyan * 0.08f, ShadowSize = 8
        };
        var hover = (StyleBoxFlat)norm.Duplicate();
        hover.BgColor = new Color(0.06f, 0.18f, 0.18f, 0.95f);
        hover.BorderColor = cyan;
        hover.BorderWidthLeft = 5;
        hover.ShadowSize = 14;
        hover.ShadowColor = cyan * 0.2f;
        _menuBtn.AddThemeStyleboxOverride("normal", norm);
        _menuBtn.AddThemeStyleboxOverride("hover", hover);
        _menuBtn.AddThemeColorOverride("font_color", new Color(0.8f, 0.88f, 0.9f));
        _menuBtn.AddThemeColorOverride("font_hover_color", cyan);
        _menuBtn.AddThemeFontSizeOverride("font_size", 20);

        _menuBtn.MouseEntered += () =>
            _menuBtn.CreateTween().TweenProperty(_menuBtn, "position:x", _menuBtn.Position.X + 6, 0.1f);
        _menuBtn.MouseExited += () =>
            _menuBtn.CreateTween().TweenProperty(_menuBtn, "position:x", _menuBtn.Position.X - 6, 0.08f);
    }

    private void AddDecorations()
    {
        var cyan = new Color(0f, 1f, 0.85f);

        // Trophy icon (large, semi-transparent background)
        var trophy = new Label { Text = "◆" };
        trophy.AddThemeFontSizeOverride("font_size", 180);
        trophy.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.3f, 0.06f));
        trophy.SetAnchorsPreset(LayoutPreset.FullRect);
        trophy.AnchorLeft = 0.3f; trophy.AnchorRight = 0.7f;
        trophy.AnchorTop = 0.02f; trophy.AnchorBottom = 0.38f;
        trophy.HorizontalAlignment = HorizontalAlignment.Center;
        trophy.VerticalAlignment = VerticalAlignment.Center;
        trophy.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(trophy);

        // Horizontal decorative lines
        var lineL = new ColorRect();
        lineL.SetAnchorsPreset(LayoutPreset.FullRect);
        lineL.AnchorLeft = 0.08f; lineL.AnchorRight = 0.35f;
        lineL.AnchorTop = 0.28f; lineL.AnchorBottom = 0.282f;
        lineL.Color = cyan * 0.15f;
        lineL.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(lineL);

        var lineR = new ColorRect();
        lineR.SetAnchorsPreset(LayoutPreset.FullRect);
        lineR.AnchorLeft = 0.65f; lineR.AnchorRight = 0.92f;
        lineR.AnchorTop = 0.28f; lineR.AnchorBottom = 0.282f;
        lineR.Color = cyan * 0.15f;
        lineR.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(lineR);

        // Success text
        var sysText = new Label { Text = ">> CORE_BREACH_COMPLETE :: AI_OVERRIDE_SUCCESS :: CYBERSPACE_SECURED" };
        sysText.AddThemeFontSizeOverride("font_size", 10);
        sysText.AddThemeColorOverride("font_color", cyan * 0.12f);
        sysText.SetAnchorsPreset(LayoutPreset.FullRect);
        sysText.AnchorLeft = 0.02f; sysText.AnchorRight = 0.98f;
        sysText.AnchorTop = 0.95f; sysText.AnchorBottom = 0.99f;
        sysText.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(sysText);
    }

    private void AnimateEntry()
    {
        // Title rises in
        _title.Modulate = new Color(1, 1, 1, 0);
        _title.Scale = new Vector2(0.7f, 0.7f);
        _title.PivotOffset = _title.Size / 2;
        var tw = CreateTween();
        tw.TweenProperty(_title, "modulate:a", 1f, 0.5f)
            .SetTrans(Tween.TransitionType.Cubic);
        tw.Parallel().TweenProperty(_title, "scale", Vector2.One, 0.5f)
            .SetTrans(Tween.TransitionType.Elastic).SetEase(Tween.EaseType.Out);

        // Stats fade
        _statsLabel.Modulate = new Color(1, 1, 1, 0);
        var stw = CreateTween();
        stw.TweenProperty(_statsLabel, "modulate:a", 1f, 0.5f).SetDelay(0.4f);

        // Button
        _menuBtn.Modulate = new Color(1, 1, 1, 0);
        _menuBtn.Position += new Vector2(-30, 0);
        var btw = CreateTween();
        btw.TweenProperty(_menuBtn, "modulate:a", 1f, 0.3f).SetDelay(0.8f);
        btw.Parallel().TweenProperty(_menuBtn, "position:x", _menuBtn.Position.X + 30, 0.3f)
            .SetDelay(0.8f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
    }

    private void ShowStats()
    {
        var run = GameManager.Instance.CurrentRun;
        if (run?.Result == null)
        {
            _statsLabel.Text = "胜利！";
            return;
        }

        var r = run.Result;
        _statsLabel.Text = $"职业: {run.Player.Name}\n" +
                          $"通关: 第{r.Act}幕 - 第{r.FloorsCleared}层\n" +
                          $"金币: {r.Gold}\n" +
                          $"卡牌数: {r.CardsInDeck}\n" +
                          $"种子: {r.Seed}\n\n" +
                          $"你成功入侵了AI核心，赛博世界迎来了新的秩序。";
    }

    private void OnMenuPressed()
    {
        AudioManager.Instance?.PlaySfx(AudioManager.SfxPaths.ButtonClick);
        GameManager.Instance.ClearRun();
        SceneManager.Instance.ChangeScene(SceneManager.Scenes.MainMenu);
    }
}
