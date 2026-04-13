using Godot;
using System;
using System.Linq;
using RogueCardGame.Core.Cards;

namespace RogueCardGame;

public partial class RestScene : Control
{
    private Label _hpLabel = null!;
    private Button _restBtn = null!;
    private Button _upgradeBtn = null!;
    private VBoxContainer _optionsContainer = null!;
    private VBoxContainer _cardListContainer = null!;
    private Label _title = null!;

    // Campfire glow animation
    private ColorRect? _glowRect;
    private float _glowTime;
    private TopBarHUD? _topBar;

    public override void _Ready()
    {
        _title = GetNode<Label>("Title");
        _hpLabel = GetNode<Label>("HpLabel");
        _restBtn = GetNode<Button>("OptionsContainer/RestBtn");
        _upgradeBtn = GetNode<Button>("OptionsContainer/UpgradeBtn");
        _optionsContainer = GetNode<VBoxContainer>("OptionsContainer");
        _cardListContainer = GetNode<VBoxContainer>("CardListContainer");

        _restBtn.Pressed += OnRestPressed;
        _upgradeBtn.Pressed += OnUpgradePressed;

        CyberFx.AddScanlines(this, 0.03f);
        CyberFx.AddVignette(this);

        _topBar = TopBarHUD.Attach(this);

        AddCampfireGlow();
        AddDecorations();
        StyleUI();
        AnimateEntry();
        AudioManager.Instance?.PlayBgm(AudioManager.BgmPaths.Event);
        UpdateHpDisplay();
    }

    public override void _Process(double delta)
    {
        if (_glowRect == null) return;
        _glowTime += (float)delta;
        float pulse = 0.08f + 0.04f * MathF.Sin(_glowTime * 1.5f)
                     + 0.02f * MathF.Sin(_glowTime * 3.7f);
        _glowRect.Color = new Color(1f, 0.4f, 0.1f, pulse);
    }

    private void AddCampfireGlow()
    {
        _glowRect = new ColorRect();
        _glowRect.SetAnchorsPreset(LayoutPreset.FullRect);
        _glowRect.Color = new Color(1f, 0.4f, 0.1f, 0.06f);
        _glowRect.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_glowRect);
        MoveChild(_glowRect, 2); // above background, below UI
    }

    private void StyleUI()
    {
        // Title
        _title.Text = "🔥 休 息 站 🔥";
        _title.AddThemeFontSizeOverride("font_size", 42);
        _title.AddThemeColorOverride("font_color", new Color(1f, 0.65f, 0.2f));

        // HP label
        _hpLabel.AddThemeFontSizeOverride("font_size", 26);
        _hpLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.3f, 0.3f));

        // Style buttons as large panels
        StyleOptionButton(_restBtn, new Color(0.2f, 0.7f, 0.3f), "🛏", "恢复体力，继续前行");
        StyleOptionButton(_upgradeBtn, new Color(0.3f, 0.5f, 0.9f), "⬆", "强化一张卡牌的效果");
    }

    private void StyleOptionButton(Button btn, Color accent, string icon, string subtitle)
    {
        btn.CustomMinimumSize = new Vector2(0, 80);

        var style = new StyleBoxFlat
        {
            BgColor = accent * 0.1f,
            BorderColor = accent * 0.5f,
            BorderWidthBottom = 2, BorderWidthTop = 2,
            BorderWidthLeft = 3, BorderWidthRight = 2,
            CornerRadiusBottomLeft = 12, CornerRadiusBottomRight = 12,
            CornerRadiusTopLeft = 12, CornerRadiusTopRight = 12,
            ContentMarginLeft = 25, ContentMarginRight = 25,
            ContentMarginTop = 12, ContentMarginBottom = 12,
            ShadowColor = accent * 0.15f,
            ShadowSize = 6
        };
        btn.AddThemeStyleboxOverride("normal", style);

        var hoverStyle = (StyleBoxFlat)style.Duplicate();
        hoverStyle.BgColor = accent * 0.2f;
        hoverStyle.BorderColor = accent;
        hoverStyle.ShadowSize = 12;
        btn.AddThemeStyleboxOverride("hover", hoverStyle);

        var disabledStyle = (StyleBoxFlat)style.Duplicate();
        disabledStyle.BgColor = new Color(0.08f, 0.08f, 0.1f);
        disabledStyle.BorderColor = new Color(0.2f, 0.2f, 0.2f);
        disabledStyle.ShadowSize = 0;
        btn.AddThemeStyleboxOverride("disabled", disabledStyle);

        btn.AddThemeFontSizeOverride("font_size", 22);
        btn.AddThemeColorOverride("font_color", Colors.White);
        btn.AddThemeColorOverride("font_disabled_color", new Color(0.4f, 0.4f, 0.4f));
    }

    private void UpdateHpDisplay()
    {
        var run = GameManager.Instance.CurrentRun;
        if (run == null) return;

        int healAmount = (int)(run.Player.MaxHp * 0.3f);
        float hpRatio = (float)run.Player.CurrentHp / run.Player.MaxHp;
        var hpColor = hpRatio > 0.5f ? new Color(0.3f, 0.9f, 0.3f)
                    : hpRatio > 0.25f ? new Color(0.9f, 0.8f, 0.2f)
                    : new Color(0.9f, 0.2f, 0.2f);

        _hpLabel.Text = $"❤ {run.Player.CurrentHp} / {run.Player.MaxHp}";
        _hpLabel.AddThemeColorOverride("font_color", hpColor);

        _restBtn.Text = $"🛏 休息  —  恢复 {healAmount} HP";

        int upgradableCards = run.MasterDeck.Count(c => !c.IsUpgraded);
        _upgradeBtn.Text = $"⬆ 升级卡牌  —  {upgradableCards} 张可升级";
        _upgradeBtn.Disabled = upgradableCards == 0;
    }

    private void OnRestPressed()
    {
        var run = GameManager.Instance.CurrentRun;
        if (run == null) return;

        AudioManager.Instance?.PlaySfx(AudioManager.SfxPaths.Heal);

        // Flash heal effect
        var flash = new ColorRect();
        flash.SetAnchorsPreset(LayoutPreset.FullRect);
        flash.Color = new Color(0.2f, 0.9f, 0.3f, 0.3f);
        flash.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(flash);
        var tw = CreateTween();
        tw.TweenProperty(flash, "color:a", 0f, 0.6f);
        tw.TweenCallback(Callable.From(() => flash.QueueFree()));

        run.Rest();
        _topBar?.Refresh();

        // Delayed go to map
        var timer = GetTree().CreateTimer(0.8);
        timer.Timeout += GoToMap;
    }

    private void OnUpgradePressed()
    {
        var run = GameManager.Instance.CurrentRun;
        if (run == null) return;

        AudioManager.Instance?.PlaySfx(AudioManager.SfxPaths.CardSelect);

        // Fade out options
        var tw = CreateTween();
        tw.TweenProperty(_optionsContainer, "modulate:a", 0f, 0.2f);
        tw.TweenCallback(Callable.From(() =>
        {
            _optionsContainer.Visible = false;
            ShowCardUpgradeList(run);
        }));
    }

    private void ShowCardUpgradeList(Core.Run.RunState run)
    {
        _cardListContainer.Visible = true;
        _cardListContainer.Modulate = new Color(1, 1, 1, 0);

        foreach (var child in _cardListContainer.GetChildren())
            child.QueueFree();

        // Header
        var header = new Label { Text = "选择一张卡牌进行升级" };
        header.AddThemeFontSizeOverride("font_size", 26);
        header.AddThemeColorOverride("font_color", new Color(0.4f, 0.6f, 1f));
        header.HorizontalAlignment = HorizontalAlignment.Center;
        _cardListContainer.AddChild(header);

        // Scrollable card grid
        var scroll = new ScrollContainer();
        scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
        scroll.CustomMinimumSize = new Vector2(0, 400);

        var grid = new GridContainer { Columns = 3 };
        grid.AddThemeConstantOverride("h_separation", 12);
        grid.AddThemeConstantOverride("v_separation", 12);
        grid.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        var upgradable = run.MasterDeck.Where(c => !c.IsUpgraded).ToList();

        foreach (var card in upgradable)
        {
            var cardPanel = CreateCardUpgradePanel(card);
            grid.AddChild(cardPanel);
        }

        scroll.AddChild(grid);
        _cardListContainer.AddChild(scroll);

        // Cancel button
        var cancelBtn = new Button { Text = "← 返回" };
        cancelBtn.CustomMinimumSize = new Vector2(200, 44);
        cancelBtn.AddThemeFontSizeOverride("font_size", 20);
        var cancelStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.1f, 0.1f, 0.15f),
            BorderColor = new Color(0.4f, 0.4f, 0.5f),
            BorderWidthBottom = 1, BorderWidthTop = 1,
            BorderWidthLeft = 1, BorderWidthRight = 1,
            CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
            CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6,
            ContentMarginLeft = 15, ContentMarginRight = 15
        };
        cancelBtn.AddThemeStyleboxOverride("normal", cancelStyle);
        cancelBtn.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
        cancelBtn.Pressed += () =>
        {
            _cardListContainer.Visible = false;
            _optionsContainer.Visible = true;
            _optionsContainer.Modulate = Colors.White;
        };
        _cardListContainer.AddChild(cancelBtn);

        // Fade in
        var tw = CreateTween();
        tw.TweenProperty(_cardListContainer, "modulate:a", 1f, 0.25f);
    }

    private PanelContainer CreateCardUpgradePanel(Card card)
    {
        var typeColor = card.Data.Type switch
        {
            CardType.Attack => new Color(0.85f, 0.2f, 0.2f),
            CardType.Skill => new Color(0.2f, 0.5f, 0.85f),
            CardType.Power => new Color(0.8f, 0.6f, 0.15f),
            _ => new Color(0.4f, 0.4f, 0.4f)
        };

        var panel = new PanelContainer();
        panel.CustomMinimumSize = new Vector2(180, 130);

        var style = new StyleBoxFlat
        {
            BgColor = typeColor * 0.1f,
            BorderColor = typeColor * 0.5f,
            BorderWidthBottom = 2, BorderWidthTop = 2,
            BorderWidthLeft = 2, BorderWidthRight = 2,
            CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10,
            CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10,
            ContentMarginLeft = 10, ContentMarginRight = 10,
            ContentMarginTop = 8, ContentMarginBottom = 8,
            ShadowColor = typeColor * 0.15f,
            ShadowSize = 4
        };
        panel.AddThemeStyleboxOverride("panel", style);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 4);

        // Cost + type
        var topRow = new HBoxContainer();
        var costLabel = new Label { Text = $"[{card.CurrentCost}]" };
        costLabel.AddThemeFontSizeOverride("font_size", 20);
        costLabel.AddThemeColorOverride("font_color", new Color(0f, 0.9f, 0.9f));
        topRow.AddChild(costLabel);

        var spacer = new Control();
        spacer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        topRow.AddChild(spacer);

        string typeIcon = card.Data.Type switch
        {
            CardType.Attack => "⚔",
            CardType.Skill => "🛡",
            CardType.Power => "⚡",
            _ => "?"
        };
        var typeLabel = new Label { Text = typeIcon };
        typeLabel.AddThemeFontSizeOverride("font_size", 18);
        topRow.AddChild(typeLabel);
        vbox.AddChild(topRow);

        // Name
        var nameLabel = new Label { Text = card.DisplayName };
        nameLabel.AddThemeFontSizeOverride("font_size", 17);
        nameLabel.AddThemeColorOverride("font_color", Colors.White);
        nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(nameLabel);

        // Upgrade hint
        var hint = new Label { Text = "⬆ 点击升级" };
        hint.AddThemeFontSizeOverride("font_size", 13);
        hint.AddThemeColorOverride("font_color", new Color(0.3f, 0.7f, 1f));
        hint.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(hint);

        panel.AddChild(vbox);

        // Hover
        panel.MouseEntered += () =>
        {
            var tw = CreateTween();
            tw.TweenProperty(panel, "scale", new Vector2(1.05f, 1.05f), 0.12f)
                .SetTrans(Tween.TransitionType.Back);
            style.BorderColor = typeColor;
            style.ShadowSize = 10;
        };
        panel.MouseExited += () =>
        {
            var tw = CreateTween();
            tw.TweenProperty(panel, "scale", Vector2.One, 0.12f);
            style.BorderColor = typeColor * 0.5f;
            style.ShadowSize = 4;
        };

        panel.GuiInput += (ev) =>
        {
            if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            {
                card.Upgrade();
                AudioManager.Instance?.PlaySfx(AudioManager.SfxPaths.CardSelect);

                // Flash
                var flash = new ColorRect();
                flash.SetAnchorsPreset(LayoutPreset.FullRect);
                flash.Color = new Color(0.3f, 0.6f, 1f, 0.25f);
                flash.MouseFilter = MouseFilterEnum.Ignore;
                AddChild(flash);
                var tw = CreateTween();
                tw.TweenProperty(flash, "color:a", 0f, 0.5f);
                tw.TweenCallback(Callable.From(() => flash.QueueFree()));

                var timer = GetTree().CreateTimer(0.6);
                timer.Timeout += GoToMap;
            }
        };

        return panel;
    }

    private void GoToMap()
    {
        SceneManager.Instance.ChangeScene(SceneManager.Scenes.Map);
    }

    private void AddDecorations()
    {
        // Ember particles (tiny floating orange dots)
        var embers = new Label { Text = "· · · ·  · ·  ·  ·" };
        embers.AddThemeFontSizeOverride("font_size", 14);
        embers.AddThemeColorOverride("font_color", new Color(1f, 0.5f, 0.1f, 0.15f));
        embers.SetAnchorsPreset(LayoutPreset.FullRect);
        embers.AnchorLeft = 0.2f; embers.AnchorRight = 0.8f;
        embers.AnchorTop = 0.25f; embers.AnchorBottom = 0.35f;
        embers.HorizontalAlignment = HorizontalAlignment.Center;
        embers.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(embers);

        // Subtle divider under title
        var divider = new ColorRect();
        divider.SetAnchorsPreset(LayoutPreset.FullRect);
        divider.AnchorLeft = 0.2f; divider.AnchorRight = 0.8f;
        divider.AnchorTop = 0.22f; divider.AnchorBottom = 0.222f;
        divider.Color = new Color(1f, 0.5f, 0.15f, 0.12f);
        divider.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(divider);

        // System text
        var sysText = new Label { Text = ">> SAFE_ZONE :: NEURAL_REPAIR_AVAILABLE" };
        sysText.AddThemeFontSizeOverride("font_size", 10);
        sysText.AddThemeColorOverride("font_color", new Color(1f, 0.6f, 0.2f, 0.1f));
        sysText.SetAnchorsPreset(LayoutPreset.FullRect);
        sysText.AnchorLeft = 0.02f; sysText.AnchorRight = 0.98f;
        sysText.AnchorTop = 0.96f; sysText.AnchorBottom = 0.99f;
        sysText.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(sysText);
    }

    private void AnimateEntry()
    {
        // Title fade+scale
        _title.Modulate = new Color(1, 1, 1, 0);
        _title.PivotOffset = _title.Size / 2;
        var tw = CreateTween();
        tw.TweenProperty(_title, "modulate:a", 1f, 0.4f)
            .SetTrans(Tween.TransitionType.Cubic);

        // HP label
        _hpLabel.Modulate = new Color(1, 1, 1, 0);
        var hw = CreateTween();
        hw.TweenProperty(_hpLabel, "modulate:a", 1f, 0.3f).SetDelay(0.2f);

        // Options stagger
        int idx = 0;
        foreach (var btn in new Control[] { _restBtn, _upgradeBtn })
        {
            btn.Modulate = new Color(1, 1, 1, 0);
            btn.Position += new Vector2(-20, 0);
            var btw = CreateTween();
            btw.TweenProperty(btn, "modulate:a", 1f, 0.3f).SetDelay(0.35f + idx * 0.12f);
            btw.Parallel().TweenProperty(btn, "position:x", btn.Position.X + 20, 0.3f)
                .SetDelay(0.35f + idx * 0.12f)
                .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            idx++;
        }
    }
}