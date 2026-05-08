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
    private Control? _upgradeConfirmOverlay;

    public override void _Ready()
    {
        GameManager.Instance.SetCurrentRunScene("rest");

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
        var visual = CyberCardFactory.CreateGameplayCard(
            card,
            new Vector2(190, 190),
            compact: true,
            footer: "点击预览升级",
            showDescription: true);
        var panel = visual.Root;
        CyberCardFactory.AttachHover(visual, scale: 1.05f, hoverShadow: 12);
        panel.MouseDefaultCursorShape = CursorShape.PointingHand;

        panel.GuiInput += (ev) =>
        {
            if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            {
                AudioManager.Instance?.PlaySfx(AudioManager.SfxPaths.CardSelect);
                ShowUpgradeConfirm(card);
            }
        };

        return panel;
    }

    private void ShowUpgradeConfirm(Card card)
    {
        if (card.Data.Upgrade?.HasBranches == true)
        {
            ShowBranchUpgradeConfirm(card);
            return;
        }

        // Remove any existing overlay
        _upgradeConfirmOverlay?.QueueFree();

        // Full-screen dimmed backdrop
        var backdrop = new ColorRect
        {
            Color = new Color(0f, 0f, 0f, 0.75f),
            ZIndex = 200,
        };
        backdrop.SetAnchorsPreset(LayoutPreset.FullRect);
        backdrop.MouseFilter = MouseFilterEnum.Stop;
        backdrop.GuiInput += ev =>
        {
            if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            {
                _upgradeConfirmOverlay?.QueueFree();
                _upgradeConfirmOverlay = null;
            }
        };

        // Centered modal
        var modal = new PanelContainer { MouseFilter = MouseFilterEnum.Stop };
        modal.SetAnchorsPreset(LayoutPreset.FullRect);
        modal.AnchorLeft = 0.05f; modal.AnchorRight = 0.95f;
        modal.AnchorTop = 0.06f; modal.AnchorBottom = 0.94f;
        modal.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.04f, 0.05f, 0.09f, 0.98f),
            BorderColor = new Color(0.28f, 0.48f, 0.75f, 0.78f),
            BorderWidthBottom = 2, BorderWidthTop = 2,
            BorderWidthLeft = 2, BorderWidthRight = 2,
            CornerRadiusBottomLeft = 14, CornerRadiusBottomRight = 14,
            CornerRadiusTopLeft = 14, CornerRadiusTopRight = 14,
            ContentMarginLeft = 22, ContentMarginRight = 22,
            ContentMarginTop = 18, ContentMarginBottom = 18,
        });

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 14);

        // Header
        var header = new Label { Text = "升级预览", HorizontalAlignment = HorizontalAlignment.Center };
        header.AddThemeFontSizeOverride("font_size", 28);
        header.AddThemeColorOverride("font_color", new Color(0.4f, 0.72f, 1f));
        root.AddChild(header);

        // Before / after card row
        var cardRow = new HBoxContainer { SizeFlagsVertical = SizeFlags.Expand };
        cardRow.AddThemeConstantOverride("separation", 20);

        // "Before" column
        var beforeCol = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.Expand };
        beforeCol.AddThemeConstantOverride("separation", 6);
        var beforeHdr = new Label { Text = "升级前", HorizontalAlignment = HorizontalAlignment.Center };
        beforeHdr.AddThemeFontSizeOverride("font_size", 15);
        beforeHdr.AddThemeColorOverride("font_color", new Color(0.55f, 0.65f, 0.78f));
        beforeCol.AddChild(beforeHdr);
        var beforeVisual = CyberCardFactory.CreateGameplayCard(
            card, new Vector2(210, 300), compact: false, showDescription: true);
        beforeVisual.Root.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        beforeVisual.Root.MouseFilter = MouseFilterEnum.Ignore;
        beforeCol.AddChild(beforeVisual.Root);
        cardRow.AddChild(beforeCol);

        // Arrow
        var arrowContainer = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        arrowContainer.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        var arrow = new Label { Text = "➜", VerticalAlignment = VerticalAlignment.Center };
        arrow.AddThemeFontSizeOverride("font_size", 40);
        arrow.AddThemeColorOverride("font_color", new Color(0.3f, 0.88f, 0.45f));
        arrowContainer.AddChild(arrow);
        cardRow.AddChild(arrowContainer);

        // "After" column (cloned + upgraded)
        var afterCol = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.Expand };
        afterCol.AddThemeConstantOverride("separation", 6);
        var afterHdr = new Label { Text = "升级后", HorizontalAlignment = HorizontalAlignment.Center };
        afterHdr.AddThemeFontSizeOverride("font_size", 15);
        afterHdr.AddThemeColorOverride("font_color", new Color(0.3f, 0.88f, 0.45f));
        afterCol.AddChild(afterHdr);
        var upgradedClone = card.Clone();
        upgradedClone.Upgrade();
        var afterVisual = CyberCardFactory.CreateGameplayCard(
            upgradedClone, new Vector2(210, 300), compact: false, showDescription: true);
        afterVisual.Root.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        afterVisual.Root.MouseFilter = MouseFilterEnum.Ignore;
        afterCol.AddChild(afterVisual.Root);
        cardRow.AddChild(afterCol);

        root.AddChild(cardRow);

        // Button row
        var btnRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        btnRow.AddThemeConstantOverride("separation", 20);

        var cancelBtn = new Button { Text = "×  取消", CustomMinimumSize = new Vector2(140, 46) };
        cancelBtn.AddThemeFontSizeOverride("font_size", 18);
        cancelBtn.AddThemeStyleboxOverride("normal", new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.1f, 0.15f),
            BorderColor = new Color(0.38f, 0.42f, 0.52f),
            BorderWidthBottom = 1, BorderWidthTop = 1,
            BorderWidthLeft = 1, BorderWidthRight = 1,
            CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
            CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8,
            ContentMarginLeft = 14, ContentMarginRight = 14,
            ContentMarginTop = 8, ContentMarginBottom = 8,
        });
        cancelBtn.AddThemeColorOverride("font_color", new Color(0.6f, 0.68f, 0.78f));
        cancelBtn.Pressed += () =>
        {
            _upgradeConfirmOverlay?.QueueFree();
            _upgradeConfirmOverlay = null;
        };
        btnRow.AddChild(cancelBtn);

        var confirmBtnStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.06f, 0.2f, 0.1f),
            BorderColor = new Color(0.28f, 0.72f, 0.38f, 0.9f),
            BorderWidthBottom = 2, BorderWidthTop = 2,
            BorderWidthLeft = 2, BorderWidthRight = 2,
            CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
            CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8,
            ContentMarginLeft = 18, ContentMarginRight = 18,
            ContentMarginTop = 8, ContentMarginBottom = 8,
        };
        var confirmBtnHover = new StyleBoxFlat
        {
            BgColor = new Color(0.1f, 0.3f, 0.16f),
            BorderColor = new Color(0.3f, 0.92f, 0.45f),
            BorderWidthBottom = 2, BorderWidthTop = 2,
            BorderWidthLeft = 2, BorderWidthRight = 2,
            CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
            CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8,
            ContentMarginLeft = 18, ContentMarginRight = 18,
            ContentMarginTop = 8, ContentMarginBottom = 8,
        };
        var confirmBtn = new Button { Text = "✓  确认升级", CustomMinimumSize = new Vector2(160, 46) };
        confirmBtn.AddThemeFontSizeOverride("font_size", 18);
        confirmBtn.AddThemeStyleboxOverride("normal", confirmBtnStyle);
        confirmBtn.AddThemeStyleboxOverride("hover", confirmBtnHover);
        confirmBtn.AddThemeColorOverride("font_color", new Color(0.45f, 1f, 0.55f));
        confirmBtn.Pressed += () =>
        {
            _upgradeConfirmOverlay?.QueueFree();
            _upgradeConfirmOverlay = null;

            card.Upgrade();
            AudioManager.Instance?.PlaySfx(AudioManager.SfxPaths.CardSelect);

            var flash = new ColorRect();
            flash.SetAnchorsPreset(LayoutPreset.FullRect);
            flash.Color = new Color(0.3f, 0.65f, 1f, 0.28f);
            flash.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(flash);
            var tw = CreateTween();
            tw.TweenProperty(flash, "color:a", 0f, 0.5f);
            tw.TweenCallback(Callable.From(() => flash.QueueFree()));

            var timer = GetTree().CreateTimer(0.6);
            timer.Timeout += GoToMap;
        };
        btnRow.AddChild(confirmBtn);
        root.AddChild(btnRow);

        modal.AddChild(root);
        backdrop.AddChild(modal);
        AddChild(backdrop);
        _upgradeConfirmOverlay = backdrop;
    }

    private void ShowBranchUpgradeConfirm(Card card)
    {
        _upgradeConfirmOverlay?.QueueFree();

        var branchA = card.Data.Upgrade!.BranchA!;
        var branchB = card.Data.Upgrade!.BranchB!;

        // Full-screen dimmed backdrop
        var backdrop = new ColorRect
        {
            Color = new Color(0f, 0f, 0f, 0.75f),
            ZIndex = 200,
        };
        backdrop.SetAnchorsPreset(LayoutPreset.FullRect);
        backdrop.MouseFilter = MouseFilterEnum.Stop;
        backdrop.GuiInput += ev =>
        {
            if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            {
                _upgradeConfirmOverlay?.QueueFree();
                _upgradeConfirmOverlay = null;
            }
        };

        // Centered modal (wider to fit 3 card columns)
        var modal = new PanelContainer { MouseFilter = MouseFilterEnum.Stop };
        modal.SetAnchorsPreset(LayoutPreset.FullRect);
        modal.AnchorLeft = 0.02f; modal.AnchorRight = 0.98f;
        modal.AnchorTop = 0.04f; modal.AnchorBottom = 0.96f;
        modal.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.04f, 0.05f, 0.09f, 0.98f),
            BorderColor = new Color(0.28f, 0.48f, 0.75f, 0.78f),
            BorderWidthBottom = 2, BorderWidthTop = 2,
            BorderWidthLeft = 2, BorderWidthRight = 2,
            CornerRadiusBottomLeft = 14, CornerRadiusBottomRight = 14,
            CornerRadiusTopLeft = 14, CornerRadiusTopRight = 14,
            ContentMarginLeft = 22, ContentMarginRight = 22,
            ContentMarginTop = 18, ContentMarginBottom = 18,
        });

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 12);

        // Header
        var header = new Label { Text = "选择升级分支", HorizontalAlignment = HorizontalAlignment.Center };
        header.AddThemeFontSizeOverride("font_size", 28);
        header.AddThemeColorOverride("font_color", new Color(0.4f, 0.72f, 1f));
        root.AddChild(header);

        // Card row: before | → | branch A | separator | branch B
        var cardRow = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        cardRow.AddThemeConstantOverride("separation", 8);

        // "Before" column
        var beforeCol = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.Expand };
        beforeCol.AddThemeConstantOverride("separation", 6);
        var beforeHdr = new Label { Text = "升级前", HorizontalAlignment = HorizontalAlignment.Center };
        beforeHdr.AddThemeFontSizeOverride("font_size", 15);
        beforeHdr.AddThemeColorOverride("font_color", new Color(0.55f, 0.65f, 0.78f));
        beforeCol.AddChild(beforeHdr);
        var beforeVisual = CyberCardFactory.CreateGameplayCard(
            card, new Vector2(170, 240), compact: false, showDescription: true);
        beforeVisual.Root.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        beforeVisual.Root.MouseFilter = MouseFilterEnum.Ignore;
        beforeCol.AddChild(beforeVisual.Root);
        cardRow.AddChild(beforeCol);

        // Arrow
        var arrowContainer = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        arrowContainer.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        var arrow = new Label { Text = "➜", VerticalAlignment = VerticalAlignment.Center };
        arrow.AddThemeFontSizeOverride("font_size", 36);
        arrow.AddThemeColorOverride("font_color", new Color(0.3f, 0.88f, 0.45f));
        arrowContainer.AddChild(arrow);
        cardRow.AddChild(arrowContainer);

        // Branch A column
        var branchACol = BuildBranchOptionColumn(card, branchA, UpgradeBranch.A, new Color(0.3f, 0.75f, 1f));
        cardRow.AddChild(branchACol);

        // Vertical separator
        var sep = new ColorRect
        {
            Color = new Color(0.28f, 0.48f, 0.75f, 0.3f),
            CustomMinimumSize = new Vector2(2, 0),
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        cardRow.AddChild(sep);

        // Branch B column
        var branchBCol = BuildBranchOptionColumn(card, branchB, UpgradeBranch.B, new Color(1f, 0.65f, 0.25f));
        cardRow.AddChild(branchBCol);

        root.AddChild(cardRow);

        // Cancel button row
        var btnRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        var cancelBtn = new Button { Text = "×  取消", CustomMinimumSize = new Vector2(140, 46) };
        cancelBtn.AddThemeFontSizeOverride("font_size", 18);
        cancelBtn.AddThemeStyleboxOverride("normal", new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.1f, 0.15f),
            BorderColor = new Color(0.38f, 0.42f, 0.52f),
            BorderWidthBottom = 1, BorderWidthTop = 1,
            BorderWidthLeft = 1, BorderWidthRight = 1,
            CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
            CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8,
            ContentMarginLeft = 14, ContentMarginRight = 14,
            ContentMarginTop = 8, ContentMarginBottom = 8,
        });
        cancelBtn.AddThemeColorOverride("font_color", new Color(0.6f, 0.68f, 0.78f));
        cancelBtn.Pressed += () =>
        {
            _upgradeConfirmOverlay?.QueueFree();
            _upgradeConfirmOverlay = null;
        };
        btnRow.AddChild(cancelBtn);
        root.AddChild(btnRow);

        modal.AddChild(root);
        backdrop.AddChild(modal);
        AddChild(backdrop);
        _upgradeConfirmOverlay = backdrop;
    }

    private VBoxContainer BuildBranchOptionColumn(Card card, CardUpgradeBranch branch, UpgradeBranch branchType, Color accentColor)
    {
        var col = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.Expand };
        col.AddThemeConstantOverride("separation", 8);

        // Branch name label
        var nameLabel = new Label
        {
            Text = branch.Name,
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.Off,
        };
        nameLabel.AddThemeFontSizeOverride("font_size", 18);
        nameLabel.AddThemeColorOverride("font_color", accentColor);
        col.AddChild(nameLabel);

        // Card preview (clone + upgrade with this branch)
        var clone = card.Clone();
        clone.Upgrade(branchType);
        var visual = CyberCardFactory.CreateGameplayCard(
            clone, new Vector2(170, 240), compact: false, showDescription: true);
        visual.Root.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        visual.Root.MouseFilter = MouseFilterEnum.Ignore;
        col.AddChild(visual.Root);

        // Spacer to push button to bottom
        var spacer = new Control { SizeFlagsVertical = SizeFlags.ExpandFill };
        col.AddChild(spacer);

        // Confirm button
        var btnText = branchType == UpgradeBranch.A ? "✓  选择路线 A" : "✓  选择路线 B";
        var btn = new Button
        {
            Text = btnText,
            CustomMinimumSize = new Vector2(160, 46),
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
        };
        btn.AddThemeFontSizeOverride("font_size", 18);
        btn.AddThemeStyleboxOverride("normal", new StyleBoxFlat
        {
            BgColor = new Color(0.06f, 0.2f, 0.1f),
            BorderColor = new Color(accentColor.R, accentColor.G, accentColor.B, 0.9f),
            BorderWidthBottom = 2, BorderWidthTop = 2,
            BorderWidthLeft = 2, BorderWidthRight = 2,
            CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
            CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8,
            ContentMarginLeft = 18, ContentMarginRight = 18,
            ContentMarginTop = 8, ContentMarginBottom = 8,
        });
        btn.AddThemeColorOverride("font_color", accentColor);
        btn.Pressed += () =>
        {
            _upgradeConfirmOverlay?.QueueFree();
            _upgradeConfirmOverlay = null;

            card.Upgrade(branchType);
            AudioManager.Instance?.PlaySfx(AudioManager.SfxPaths.CardSelect);

            var flash = new ColorRect();
            flash.SetAnchorsPreset(LayoutPreset.FullRect);
            flash.Color = new Color(0.3f, 0.65f, 1f, 0.28f);
            flash.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(flash);
            var tw = CreateTween();
            tw.TweenProperty(flash, "color:a", 0f, 0.5f);
            tw.TweenCallback(Callable.From(() => flash.QueueFree()));

            var timer = GetTree().CreateTimer(0.6);
            timer.Timeout += GoToMap;
        };
        col.AddChild(btn);

        return col;
    }

    private void GoToMap()
    {
        GameManager.Instance.SetCurrentRunScene("map");
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