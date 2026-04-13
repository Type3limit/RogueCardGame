using Godot;
using System;
using System.Collections.Generic;
using RogueCardGame.Core.Cards;
using RogueCardGame.Core.Characters;

namespace RogueCardGame;

public partial class MainMenu : Control
{
    private VBoxContainer _mainVBox = null!;
    private PanelContainer _classSelectPanel = null!;
    private GridContainer _classGrid = null!;
    private Label _titleLabel = null!;
    private Label _subtitleLabel = null!;
    private ClassDatabase _classDb = new();

    private readonly List<PanelContainer> _classCards = [];
    private readonly Dictionary<PanelContainer, StyleBoxFlat> _cardStyles = new();
    private readonly Dictionary<PanelContainer, Color> _cardAccents = new();
    private Label? _scrollingData;
    private float _scrollTime;

    public override void _Ready()
    {
        _mainVBox = GetNode<VBoxContainer>("VBox");
        _classSelectPanel = GetNode<PanelContainer>("ClassSelectPanel");
        _classGrid = GetNode<GridContainer>("ClassSelectPanel/MarginBox/ClassVBox/ClassGrid");
        _titleLabel = GetNode<Label>("VBox/Title");
        _subtitleLabel = GetNode<Label>("VBox/Subtitle");

        // --- ATMOSPHERE ---
        CyberFx.AddParticles(this, 40, new Color(0f, 0.85f, 0.9f, 0.15f));
        CyberFx.AddScanlines(this, 0.04f);
        CyberFx.AddVignette(this);

        // --- TITLE STYLE ---
        _titleLabel.AddThemeFontSizeOverride("font_size", 80);
        _titleLabel.AddThemeColorOverride("font_color", new Color(0f, 0.95f, 0.95f));
        _titleLabel.AddThemeColorOverride("font_shadow_color", new Color(0f, 0.4f, 0.6f, 0.4f));

        _subtitleLabel.AddThemeFontSizeOverride("font_size", 18);
        _subtitleLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.2f, 0.6f, 0.7f));

        // Title glow pulse
        CyberFx.PulseGlow(_titleLabel, new Color(0f, 1f, 1f), 0.75f, 1f, 3f);

        // --- GLITCH DECORATION LINES ---
        AddDecoLines();

        // Panel bg
        var panelBg = new StyleBoxFlat { BgColor = new Color(0.02f, 0.02f, 0.06f, 0.98f) };
        _classSelectPanel.AddThemeStyleboxOverride("panel", panelBg);

        var classTitle = GetNode<Label>("ClassSelectPanel/MarginBox/ClassVBox/ClassTitle");
        classTitle.AddThemeFontSizeOverride("font_size", 42);
        classTitle.AddThemeColorOverride("font_color", new Color(0f, 0.95f, 0.95f));

        var classSubtitle = GetNode<Label>("ClassSelectPanel/MarginBox/ClassVBox/ClassSubtitle");
        classSubtitle.AddThemeFontSizeOverride("font_size", 14);
        classSubtitle.AddThemeColorOverride("font_color", new Color(0f, 0.5f, 0.5f, 0.5f));

        // Connect buttons
        GetNode<Button>("VBox/NewGameBtn").Pressed += OnNewGamePressed;
        GetNode<Button>("VBox/SettingsBtn").Pressed += OnSettingsPressed;
        GetNode<Button>("VBox/QuitBtn").Pressed += OnQuitPressed;
        GetNode<Button>("ClassSelectPanel/MarginBox/ClassVBox/BackBtn").Pressed += OnBackPressed;

        StyleMenuButtons();
        StyleBackButton();

        string classDir = System.IO.Path.Combine(GameManager.Instance.DataDirectory, "classes");
        _classDb.LoadFromDirectory(classDir);
        AudioManager.Instance?.PlayBgm(AudioManager.BgmPaths.MainMenu);

        // Stagger-animate menu buttons
        AnimateMenuEntry();
    }

    public override void _Process(double delta)
    {
        // Scrolling data stream at bottom
        if (_scrollingData != null)
        {
            _scrollTime += (float)delta * 30f;
            int offset = (int)_scrollTime % _scrollingData.Text.Length;
            _scrollingData.VisibleCharacters = offset;
        }
    }

    // ===== DECORATIVE ELEMENTS =====
    private void AddDecoLines()
    {
        // Horizontal accent lines flanking title
        var lineL = new ColorRect();
        lineL.SetAnchorsPreset(LayoutPreset.FullRect);
        lineL.AnchorLeft = 0.05f; lineL.AnchorRight = 0.35f;
        lineL.AnchorTop = 0.28f; lineL.AnchorBottom = 0.281f;
        lineL.Color = new Color(0f, 0.7f, 0.8f, 0.15f);
        lineL.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(lineL);

        var lineR = new ColorRect();
        lineR.SetAnchorsPreset(LayoutPreset.FullRect);
        lineR.AnchorLeft = 0.65f; lineR.AnchorRight = 0.95f;
        lineR.AnchorTop = 0.28f; lineR.AnchorBottom = 0.281f;
        lineR.Color = new Color(0f, 0.7f, 0.8f, 0.15f);
        lineR.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(lineR);

        // Corner accent brackets
        string[] corners = ["┌", "┐", "└", "┘"];
        float[][] pos = [[0.03f, 0.03f], [0.95f, 0.03f], [0.03f, 0.95f], [0.95f, 0.95f]];
        for (int i = 0; i < 4; i++)
        {
            var c = new Label { Text = corners[i] };
            c.AddThemeFontSizeOverride("font_size", 28);
            c.AddThemeColorOverride("font_color", new Color(0f, 0.6f, 0.6f, 0.12f));
            c.SetAnchorsPreset(LayoutPreset.FullRect);
            c.AnchorLeft = pos[i][0]; c.AnchorRight = pos[i][0] + 0.03f;
            c.AnchorTop = pos[i][1]; c.AnchorBottom = pos[i][1] + 0.04f;
            c.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(c);
        }

        // Bottom data stream text (scrolling)
        var streamText = "SYS.INIT >> CYBER_SPIRE v0.1.0a :: NEURAL_LINK ACTIVE :: MEM 0xFFAA02 OK :: DECK_LOADER READY :: COMBAT_ENGINE ONLINE :: MAP_GEN SEEDED :: AUDIO_SYS LOADED :: >>> ";
        _scrollingData = new Label { Text = streamText + streamText };
        _scrollingData.AddThemeFontSizeOverride("font_size", 11);
        _scrollingData.AddThemeColorOverride("font_color", new Color(0f, 0.6f, 0.6f, 0.15f));
        _scrollingData.SetAnchorsPreset(LayoutPreset.FullRect);
        _scrollingData.AnchorLeft = 0.02f; _scrollingData.AnchorRight = 0.98f;
        _scrollingData.AnchorTop = 0.97f; _scrollingData.AnchorBottom = 1f;
        _scrollingData.ClipText = true;
        _scrollingData.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_scrollingData);
    }

    private void AnimateMenuEntry()
    {
        // Title slide in
        _titleLabel.Modulate = new Color(1, 1, 1, 0);
        _titleLabel.Position += new Vector2(0, -30);
        var ttw = CreateTween();
        ttw.TweenProperty(_titleLabel, "modulate:a", 1f, 0.6f)
            .SetTrans(Tween.TransitionType.Cubic);
        ttw.Parallel().TweenProperty(_titleLabel, "position:y", _titleLabel.Position.Y + 30, 0.6f)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);

        // Subtitle fade
        _subtitleLabel.Modulate = new Color(1, 1, 1, 0);
        var stw = CreateTween();
        stw.TweenProperty(_subtitleLabel, "modulate:a", 1f, 0.4f).SetDelay(0.3f);

        // Buttons stagger
        int idx = 0;
        foreach (var child in _mainVBox.GetChildren())
        {
            if (child is Button btn)
            {
                btn.Modulate = new Color(1, 1, 1, 0);
                btn.Position += new Vector2(-40, 0);
                var btw = CreateTween();
                btw.TweenProperty(btn, "modulate:a", 1f, 0.3f).SetDelay(0.4f + idx * 0.1f);
                btw.Parallel().TweenProperty(btn, "position:x", btn.Position.X + 40, 0.3f)
                    .SetDelay(0.4f + idx * 0.1f)
                    .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
                idx++;
            }
        }
    }

    // ===== BUTTON STYLES =====
    private void StyleMenuButtons()
    {
        var cyan = new Color(0f, 0.85f, 0.85f);
        foreach (var child in _mainVBox.GetChildren())
        {
            if (child is not Button btn) continue;
            var norm = new StyleBoxFlat
            {
                BgColor = new Color(0.04f, 0.06f, 0.12f, 0.85f),
                BorderColor = cyan * 0.35f,
                BorderWidthBottom = 1, BorderWidthTop = 1,
                BorderWidthLeft = 2, BorderWidthRight = 0,
                CornerRadiusBottomLeft = 2, CornerRadiusBottomRight = 2,
                CornerRadiusTopLeft = 2, CornerRadiusTopRight = 2,
                ContentMarginLeft = 30, ContentMarginRight = 30,
                ContentMarginTop = 14, ContentMarginBottom = 14
            };
            var hover = (StyleBoxFlat)norm.Duplicate();
            hover.BgColor = new Color(0.06f, 0.1f, 0.2f, 0.95f);
            hover.BorderColor = cyan;
            hover.BorderWidthLeft = 4;
            hover.ShadowColor = new Color(0f, 0.6f, 0.8f, 0.2f);
            hover.ShadowSize = 8;

            btn.AddThemeStyleboxOverride("normal", norm);
            btn.AddThemeStyleboxOverride("hover", hover);
            btn.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.75f));
            btn.AddThemeColorOverride("font_hover_color", cyan);
            btn.AddThemeFontSizeOverride("font_size", 22);

            // Left accent indicator on hover
            btn.MouseEntered += () =>
            {
                var tw = btn.CreateTween();
                tw.TweenProperty(btn, "position:x", btn.Position.X + 8, 0.12f)
                    .SetTrans(Tween.TransitionType.Cubic);
            };
            btn.MouseExited += () =>
            {
                var tw = btn.CreateTween();
                tw.TweenProperty(btn, "position:x", btn.Position.X - 8, 0.1f);
            };
        }
    }

    private void StyleBackButton()
    {
        var backBtn = GetNode<Button>("ClassSelectPanel/MarginBox/ClassVBox/BackBtn");
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.05f, 0.05f, 0.1f, 0.85f),
            BorderColor = new Color(0.3f, 0.3f, 0.4f, 0.5f),
            BorderWidthBottom = 1, BorderWidthTop = 1,
            BorderWidthLeft = 2, BorderWidthRight = 0,
            CornerRadiusBottomLeft = 2, CornerRadiusBottomRight = 2,
            CornerRadiusTopLeft = 2, CornerRadiusTopRight = 2,
            ContentMarginLeft = 20, ContentMarginRight = 20,
            ContentMarginTop = 8, ContentMarginBottom = 8
        };
        var hover = (StyleBoxFlat)style.Duplicate();
        hover.BgColor = new Color(0.08f, 0.08f, 0.15f);
        hover.BorderColor = new Color(0.5f, 0.5f, 0.6f, 0.8f);
        hover.BorderWidthLeft = 4;
        backBtn.AddThemeStyleboxOverride("normal", style);
        backBtn.AddThemeStyleboxOverride("hover", hover);
        backBtn.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.55f));
        backBtn.AddThemeColorOverride("font_hover_color", new Color(0.85f, 0.85f, 0.9f));
        backBtn.AddThemeFontSizeOverride("font_size", 18);
    }

    // ===== ACTIONS =====
    private void OnNewGamePressed()
    {
        AudioManager.Instance?.PlaySfx(AudioManager.SfxPaths.ButtonClick);
        ShowClassSelection();
    }

    private void ShowClassSelection()
    {
        _mainVBox.Visible = false;
        _classSelectPanel.Visible = true;

        _classCards.Clear();
        _cardStyles.Clear();
        _cardAccents.Clear();
        foreach (var child in _classGrid.GetChildren()) child.QueueFree();

        var classes = _classDb.GetAll();
        if (classes.Count == 0)
        {
            CreateClassCard("先锋", "VANGUARD", "重装战士，擅长前排近战与护甲叠加。",
                CardClass.Vanguard, new Color(0.9f, 0.22f, 0.27f), 85, 3, 5, "超载 Overcharge",
                "res://resources/textures/characters/vanguard.svg");
            CreateClassCard("灵能者", "PSION", "远程念力输出者，掌控战场节奏。",
                CardClass.Psion, new Color(0.66f, 0.33f, 0.97f), 65, 3, 5, "共鸣 Resonance",
                "res://resources/textures/characters/psion.svg");
            CreateClassCard("黑客", "NETRUNNER", "数据控制大师，操控信息之流。",
                CardClass.Netrunner, new Color(0.02f, 0.71f, 0.83f), 70, 3, 5, "协议栈 Protocols",
                "res://resources/textures/characters/netrunner.svg");
            CreateClassCard("共生体", "SYMBIOTE", "生物融合战士，暗涌侵蚀一切。",
                CardClass.Symbiote, new Color(0.13f, 0.77f, 0.37f), 75, 3, 5, "侵蚀 Erosion",
                "res://resources/textures/characters/symbiote.svg");
        }
        else
        {
            foreach (var def in classes)
            {
                var color = new Color(def.Color);
                string mechanic = def.ClassResource != null
                    ? $"{def.ClassResource.DisplayName} {def.ClassResource.DisplayNameEn}" : "";
                string portrait = $"res://resources/textures/characters/{def.Id}.svg";
                CreateClassCard(def.Name, def.NameEn.ToUpperInvariant(), def.Description,
                    def.CardClass, color, def.MaxHp, def.BaseEnergy, def.DrawPerTurn,
                    mechanic, portrait);
            }
        }

        // Stagger animation
        for (int i = 0; i < _classCards.Count; i++)
        {
            var card = _classCards[i];
            card.Modulate = new Color(1, 1, 1, 0);
            card.Scale = new Vector2(0.92f, 0.92f);
            var tween = CreateTween();
            tween.SetParallel(true);
            tween.TweenProperty(card, "modulate:a", 1.0f, 0.35f)
                .SetDelay(i * 0.08f).SetEase(Tween.EaseType.Out);
            tween.TweenProperty(card, "scale", Vector2.One, 0.35f)
                .SetDelay(i * 0.08f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        }
    }

    private void CreateClassCard(string name, string nameEn, string desc,
        CardClass cardClass, Color accent, int hp, int energy, int draw,
        string mechanic, string portraitPath)
    {
        var card = new PanelContainer();
        card.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        card.SizeFlagsVertical = SizeFlags.ExpandFill;
        card.CustomMinimumSize = new Vector2(0, 500);
        card.PivotOffset = new Vector2(200, 250);

        var cardStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.03f, 0.03f, 0.07f, 0.97f),
            BorderColor = accent * 0.3f,
            BorderWidthBottom = 2, BorderWidthTop = 2,
            BorderWidthLeft = 2, BorderWidthRight = 2,
            CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
            CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8,
            ShadowColor = new Color(accent.R, accent.G, accent.B, 0.08f),
            ShadowSize = 8
        };
        card.AddThemeStyleboxOverride("panel", cardStyle);
        _cardStyles[card] = cardStyle;
        _cardAccents[card] = accent;

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 0);

        // Portrait
        var portraitContainer = new PanelContainer();
        portraitContainer.CustomMinimumSize = new Vector2(0, 200);
        portraitContainer.SizeFlagsVertical = SizeFlags.ShrinkBegin;
        var portraitBg = new StyleBoxFlat
        {
            BgColor = new Color(accent.R * 0.08f, accent.G * 0.08f, accent.B * 0.08f, 1f),
            CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6
        };
        portraitContainer.AddThemeStyleboxOverride("panel", portraitBg);

        if (ResourceLoader.Exists(portraitPath))
        {
            var portraitTex = GD.Load<Texture2D>(portraitPath);
            if (portraitTex != null)
            {
                var portrait = new TextureRect
                {
                    Texture = portraitTex,
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                    CustomMinimumSize = new Vector2(0, 200),
                    SizeFlagsHorizontal = SizeFlags.ExpandFill,
                    SizeFlagsVertical = SizeFlags.ExpandFill,
                    Modulate = new Color(1, 1, 1, 0.65f)
                };
                portraitContainer.AddChild(portrait);
            }
        }

        // Class icon overlay
        string classIcon = cardClass switch
        {
            CardClass.Vanguard => "⚔",
            CardClass.Psion => "◈",
            CardClass.Netrunner => "⟁",
            CardClass.Symbiote => "◉",
            _ => "◇"
        };
        var iconLbl = new Label { Text = classIcon, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        iconLbl.AddThemeFontSizeOverride("font_size", 60);
        iconLbl.AddThemeColorOverride("font_color", new Color(accent.R, accent.G, accent.B, 0.15f));
        iconLbl.SetAnchorsPreset(LayoutPreset.FullRect);
        iconLbl.MouseFilter = MouseFilterEnum.Ignore;
        portraitContainer.AddChild(iconLbl);

        var accentLine = new ColorRect { Color = accent * 0.6f, CustomMinimumSize = new Vector2(0, 2), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        vbox.AddChild(portraitContainer);
        vbox.AddChild(accentLine);

        // Content
        var contentMargin = new MarginContainer();
        contentMargin.AddThemeConstantOverride("margin_left", 20);
        contentMargin.AddThemeConstantOverride("margin_right", 20);
        contentMargin.AddThemeConstantOverride("margin_top", 14);
        contentMargin.AddThemeConstantOverride("margin_bottom", 16);
        contentMargin.SizeFlagsVertical = SizeFlags.ExpandFill;

        var contentVbox = new VBoxContainer();
        contentVbox.AddThemeConstantOverride("separation", 8);

        var nameLabel = new Label { Text = name, HorizontalAlignment = HorizontalAlignment.Center };
        nameLabel.AddThemeFontSizeOverride("font_size", 30);
        nameLabel.AddThemeColorOverride("font_color", accent);
        contentVbox.AddChild(nameLabel);

        var enLabel = new Label { Text = nameEn, HorizontalAlignment = HorizontalAlignment.Center };
        enLabel.AddThemeFontSizeOverride("font_size", 11);
        enLabel.AddThemeColorOverride("font_color", new Color(accent.R, accent.G, accent.B, 0.35f));
        contentVbox.AddChild(enLabel);

        var descLabel = new Label
        {
            Text = desc, AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0, 36), HorizontalAlignment = HorizontalAlignment.Center
        };
        descLabel.AddThemeFontSizeOverride("font_size", 13);
        descLabel.AddThemeColorOverride("font_color", new Color(0.55f, 0.55f, 0.6f));
        contentVbox.AddChild(descLabel);

        // Stats
        var statsRow = new HBoxContainer();
        statsRow.AddThemeConstantOverride("separation", 16);
        statsRow.Alignment = BoxContainer.AlignmentMode.Center;
        statsRow.AddChild(CreateStatBadge($"♥ {hp}", new Color(0.9f, 0.25f, 0.25f)));
        statsRow.AddChild(CreateStatBadge($"⚡ {energy}", new Color(0.2f, 0.75f, 0.9f)));
        statsRow.AddChild(CreateStatBadge($"✦ {draw}", new Color(0.9f, 0.8f, 0.2f)));
        contentVbox.AddChild(statsRow);

        if (!string.IsNullOrEmpty(mechanic))
        {
            var badgeContainer = new CenterContainer();
            var badge = new PanelContainer();
            var badgeStyle = new StyleBoxFlat
            {
                BgColor = new Color(accent.R * 0.1f, accent.G * 0.1f, accent.B * 0.1f, 0.9f),
                BorderColor = accent * 0.25f,
                BorderWidthBottom = 1, BorderWidthTop = 1, BorderWidthLeft = 1, BorderWidthRight = 1,
                CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
                CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4,
                ContentMarginLeft = 12, ContentMarginRight = 12,
                ContentMarginTop = 3, ContentMarginBottom = 3
            };
            badge.AddThemeStyleboxOverride("panel", badgeStyle);
            var badgeLabel = new Label { Text = $"◈ {mechanic}" };
            badgeLabel.AddThemeFontSizeOverride("font_size", 11);
            badgeLabel.AddThemeColorOverride("font_color", accent * 0.7f);
            badge.AddChild(badgeLabel);
            badgeContainer.AddChild(badge);
            contentVbox.AddChild(badgeContainer);
        }

        contentVbox.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });

        // Select button
        var selectBtn = new Button { Text = $"▶  {name}" };
        selectBtn.AddThemeFontSizeOverride("font_size", 16);
        selectBtn.CustomMinimumSize = new Vector2(0, 42);
        var btnNorm = new StyleBoxFlat
        {
            BgColor = new Color(accent.R * 0.12f, accent.G * 0.12f, accent.B * 0.12f, 0.9f),
            BorderColor = accent * 0.4f,
            BorderWidthBottom = 1, BorderWidthTop = 1, BorderWidthLeft = 2, BorderWidthRight = 0,
            CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
            CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4,
            ContentMarginTop = 8, ContentMarginBottom = 8
        };
        var btnHov = (StyleBoxFlat)btnNorm.Duplicate();
        btnHov.BgColor = new Color(accent.R * 0.25f, accent.G * 0.25f, accent.B * 0.25f, 0.95f);
        btnHov.BorderColor = accent;
        btnHov.BorderWidthLeft = 4;
        btnHov.ShadowColor = accent * 0.2f;
        btnHov.ShadowSize = 6;
        selectBtn.AddThemeStyleboxOverride("normal", btnNorm);
        selectBtn.AddThemeStyleboxOverride("hover", btnHov);
        selectBtn.AddThemeColorOverride("font_color", new Color(0.75f, 0.75f, 0.8f));
        selectBtn.AddThemeColorOverride("font_hover_color", accent);
        selectBtn.Pressed += () => OnClassSelected(cardClass);
        contentVbox.AddChild(selectBtn);

        contentMargin.AddChild(contentVbox);
        vbox.AddChild(contentMargin);
        card.AddChild(vbox);

        card.MouseEntered += () => OnCardHoverEnter(card);
        card.MouseExited += () => OnCardHoverExit(card);

        _classGrid.AddChild(card);
        _classCards.Add(card);
    }

    private PanelContainer CreateStatBadge(string text, Color color)
    {
        var panel = new PanelContainer();
        var st = new StyleBoxFlat
        {
            BgColor = new Color(color.R * 0.08f, color.G * 0.08f, color.B * 0.08f, 0.8f),
            CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3,
            CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3,
            ContentMarginLeft = 8, ContentMarginRight = 8,
            ContentMarginTop = 2, ContentMarginBottom = 2
        };
        panel.AddThemeStyleboxOverride("panel", st);
        var lbl = new Label { Text = text };
        lbl.AddThemeFontSizeOverride("font_size", 15);
        lbl.AddThemeColorOverride("font_color", color);
        panel.AddChild(lbl);
        return panel;
    }

    private void OnCardHoverEnter(PanelContainer card)
    {
        if (!_cardAccents.TryGetValue(card, out var accent)) return;
        if (!_cardStyles.TryGetValue(card, out var style)) return;
        AudioManager.Instance?.PlaySfx(AudioManager.SfxPaths.ButtonClick);
        style.BorderColor = accent * 0.8f;
        style.ShadowColor = new Color(accent.R, accent.G, accent.B, 0.25f);
        style.ShadowSize = 16;
        var tw = CreateTween();
        tw.TweenProperty(card, "scale", new Vector2(1.03f, 1.03f), 0.12f)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
    }

    private void OnCardHoverExit(PanelContainer card)
    {
        if (!_cardAccents.TryGetValue(card, out var accent)) return;
        if (!_cardStyles.TryGetValue(card, out var style)) return;
        style.BorderColor = accent * 0.3f;
        style.ShadowColor = new Color(accent.R, accent.G, accent.B, 0.08f);
        style.ShadowSize = 8;
        var tw = CreateTween();
        tw.TweenProperty(card, "scale", Vector2.One, 0.12f)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
    }

    private void OnClassSelected(CardClass cardClass)
    {
        AudioManager.Instance?.PlaySfx(AudioManager.SfxPaths.ButtonClick);
        try
        {
            GameManager.Instance.StartNewRun(cardClass);
            SceneManager.Instance.ChangeScene(SceneManager.Scenes.Map);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Failed to start run: {ex.Message}");
            GD.PrintErr(ex.StackTrace);
        }
    }

    private void OnBackPressed()
    {
        AudioManager.Instance?.PlaySfx(AudioManager.SfxPaths.ButtonClick);
        _classSelectPanel.Visible = false;
        _mainVBox.Visible = true;
    }

    private void OnSettingsPressed()
    {
        AudioManager.Instance?.PlaySfx(AudioManager.SfxPaths.ButtonClick);
        SceneManager.Instance.ChangeScene(SceneManager.Scenes.Settings);
    }

    private void OnQuitPressed() => GetTree().Quit();
}