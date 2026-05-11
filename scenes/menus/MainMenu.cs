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
		GetNode<Button>("VBox/ContinueBtn").Pressed += OnContinuePressed;
		GetNode<Button>("VBox/SettingsBtn").Pressed += OnSettingsPressed;
		GetNode<Button>("VBox/QuitBtn").Pressed += OnQuitPressed;
		GetNode<Button>("ClassSelectPanel/MarginBox/ClassVBox/BackBtn").Pressed += OnBackPressed;

		StyleMenuButtons();
		StyleBackButton();
		UpdateContinueButton();

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

	private void OnContinuePressed()
	{
		AudioManager.Instance?.PlaySfx(AudioManager.SfxPaths.ButtonClick);
		if (!GameManager.Instance.TryLoadActiveRun())
		{
			UpdateContinueButton();
			return;
		}

		SceneManager.Instance.ChangeScene(ResolveRunScene(GameManager.Instance.CurrentRun));
	}

	private void UpdateContinueButton()
	{
		var continueBtn = GetNode<Button>("VBox/ContinueBtn");
		continueBtn.Disabled = !GameManager.Instance.HasActiveRunSave();
	}

	private static string ResolveRunScene(RogueCardGame.Core.Run.RunState? run)
	{
		return run?.CurrentSceneId switch
		{
			"combat" => SceneManager.Scenes.Combat,
			"reward" => SceneManager.Scenes.Reward,
			"rest" => SceneManager.Scenes.Rest,
			"shop" => SceneManager.Scenes.Shop,
			"event" => SceneManager.Scenes.Event,
			"gameover" => SceneManager.Scenes.GameOver,
			"victory" => SceneManager.Scenes.Victory,
			_ => SceneManager.Scenes.Map
		};
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
				CyberCardFactory.GetClassPortraitPath(CardClass.Vanguard));
			CreateClassCard("灵能者", "PSION", "远程念力输出者，掌控战场节奏。",
				CardClass.Psion, new Color(0.66f, 0.33f, 0.97f), 65, 3, 5, "共鸣 Resonance",
				CyberCardFactory.GetClassPortraitPath(CardClass.Psion));
			CreateClassCard("黑客", "NETRUNNER", "数据控制大师，操控信息之流。",
				CardClass.Netrunner, new Color(0.02f, 0.71f, 0.83f), 70, 3, 5, "协议栈 Protocols",
				CyberCardFactory.GetClassPortraitPath(CardClass.Netrunner));
			CreateClassCard("共生体", "SYMBIOTE", "生物融合战士，暗涌侵蚀一切。",
				CardClass.Symbiote, new Color(0.13f, 0.77f, 0.37f), 75, 3, 5, "侵蚀 Erosion",
				CyberCardFactory.GetClassPortraitPath(CardClass.Symbiote));
		}
		else
		{
			foreach (var def in classes)
			{
				var color = new Color(def.Color);
				string mechanic = def.ClassResource != null
					? $"{def.ClassResource.DisplayName} {def.ClassResource.DisplayNameEn}" : "";
				string portrait = CyberCardFactory.GetClassPortraitPath(def.CardClass);
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
		card.CustomMinimumSize = new Vector2(0, 540);
		card.PivotOffset = new Vector2(170, 270);
		card.ClipContents = true;

		var cardStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.02f, 0.025f, 0.05f, 0.985f),
			BorderColor = accent * 0.34f,
			BorderWidthBottom = 2, BorderWidthTop = 2,
			BorderWidthLeft = 2, BorderWidthRight = 2,
			CornerRadiusBottomLeft = 14, CornerRadiusBottomRight = 14,
			CornerRadiusTopLeft = 14, CornerRadiusTopRight = 14,
			ShadowColor = new Color(accent.R, accent.G, accent.B, 0.1f),
			ShadowSize = 10
		};
		card.AddThemeStyleboxOverride("panel", cardStyle);
		_cardStyles[card] = cardStyle;
		_cardAccents[card] = accent;

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 0);
		vbox.SizeFlagsVertical = SizeFlags.ExpandFill;

		var artStage = new Control
		{
			CustomMinimumSize = new Vector2(0, 300),
			SizeFlagsVertical = SizeFlags.ExpandFill,
			ClipContents = true
		};

		var artBase = new ColorRect
		{
			Color = new Color(0.035f, 0.04f, 0.06f, 1f),
			MouseFilter = MouseFilterEnum.Ignore
		};
		artBase.SetAnchorsPreset(LayoutPreset.FullRect);
		artStage.AddChild(artBase);

		var accentPlate = new ColorRect
		{
			Color = new Color(accent.R, accent.G, accent.B, 0.28f),
			MouseFilter = MouseFilterEnum.Ignore
		};
		accentPlate.SetAnchorsPreset(LayoutPreset.FullRect);
		accentPlate.AnchorLeft = 0.16f; accentPlate.AnchorRight = 0.96f;
		accentPlate.AnchorTop = 0.02f; accentPlate.AnchorBottom = 0.88f;
		artStage.AddChild(accentPlate);

		var leftRail = new ColorRect
		{
			Color = new Color(0.01f, 0.01f, 0.02f, 0.96f),
			MouseFilter = MouseFilterEnum.Ignore
		};
		leftRail.SetAnchorsPreset(LayoutPreset.FullRect);
		leftRail.AnchorRight = 0f;
		leftRail.OffsetRight = 28f;
		artStage.AddChild(leftRail);

		var railAccent = new ColorRect
		{
			Color = accent,
			MouseFilter = MouseFilterEnum.Ignore
		};
		railAccent.SetAnchorsPreset(LayoutPreset.FullRect);
		railAccent.AnchorRight = 0f;
		railAccent.OffsetRight = 6f;
		artStage.AddChild(railAccent);

		var verticalCode = new Label
		{
			Text = string.Join("\n", nameEn.ToUpperInvariant().ToCharArray()),
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore
		};
		verticalCode.AddThemeFontSizeOverride("font_size", 10);
		verticalCode.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f, 0.72f));
		verticalCode.SetAnchorsPreset(LayoutPreset.FullRect);
		verticalCode.AnchorRight = 0f;
		verticalCode.OffsetRight = 28f;
		artStage.AddChild(verticalCode);

		var topChipRow = new HBoxContainer();
		topChipRow.SetAnchorsPreset(LayoutPreset.FullRect);
		topChipRow.AnchorBottom = 0f;
		topChipRow.OffsetLeft = 38f; topChipRow.OffsetRight = -12f;
		topChipRow.OffsetTop = 12f; topChipRow.OffsetBottom = 42f;
		topChipRow.AddChild(CreatePosterChip($"HP {hp}", new Color(0.95f, 0.2f, 0.25f), Colors.White));
		topChipRow.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
		topChipRow.AddChild(CreatePosterChip(cardClass.ToString().ToUpperInvariant(), accent, new Color(0.03f, 0.03f, 0.05f)));
		artStage.AddChild(topChipRow);

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
					CustomMinimumSize = new Vector2(0, 300),
					SizeFlagsHorizontal = SizeFlags.ExpandFill,
					SizeFlagsVertical = SizeFlags.ExpandFill,
					Modulate = new Color(1, 1, 1, 0.92f),
					MouseFilter = MouseFilterEnum.Ignore
				};
				portrait.SetAnchorsPreset(LayoutPreset.FullRect);
				portrait.AnchorLeft = 0.08f; portrait.AnchorRight = 1.04f;
				portrait.AnchorTop = 0.04f; portrait.AnchorBottom = 1.04f;
				artStage.AddChild(portrait);
			}
		}

		string classIcon = cardClass switch
		{
			CardClass.Vanguard => "⚔",
			CardClass.Psion => "◈",
			CardClass.Netrunner => "⟁",
			CardClass.Symbiote => "◉",
			_ => "◇"
		};
		var iconLbl = new Label { Text = classIcon, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
		iconLbl.AddThemeFontSizeOverride("font_size", 88);
		iconLbl.AddThemeColorOverride("font_color", new Color(accent.R, accent.G, accent.B, 0.12f));
		iconLbl.SetAnchorsPreset(LayoutPreset.FullRect);
		iconLbl.MouseFilter = MouseFilterEnum.Ignore;
		artStage.AddChild(iconLbl);

		var topShade = new ColorRect
		{
			Color = new Color(0f, 0f, 0f, 0.18f),
			MouseFilter = MouseFilterEnum.Ignore
		};
		topShade.SetAnchorsPreset(LayoutPreset.FullRect);
		topShade.AnchorBottom = 0f;
		topShade.OffsetBottom = 44f;
		artStage.AddChild(topShade);

		var bottomShade = new ColorRect
		{
			Color = new Color(0f, 0f, 0f, 0.55f),
			MouseFilter = MouseFilterEnum.Ignore
		};
		bottomShade.SetAnchorsPreset(LayoutPreset.FullRect);
		bottomShade.AnchorTop = 1f;
		bottomShade.OffsetTop = -84f;
		artStage.AddChild(bottomShade);

		var titlePlate = new PanelContainer();
		titlePlate.SetAnchorsPreset(LayoutPreset.FullRect);
		titlePlate.AnchorTop = 1f; titlePlate.AnchorBottom = 1f;
		titlePlate.OffsetLeft = 38f; titlePlate.OffsetRight = -12f;
		titlePlate.OffsetTop = -78f; titlePlate.OffsetBottom = -10f;
		titlePlate.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.02f, 0.03f, 0.05f, 0.92f),
			BorderColor = accent * 0.38f,
			BorderWidthLeft = 2,
			CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
			CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8,
			ContentMarginLeft = 12, ContentMarginRight = 12,
			ContentMarginTop = 8, ContentMarginBottom = 8
		});

		var titleBox = new VBoxContainer();
		titleBox.AddThemeConstantOverride("separation", 2);
		var enTitle = new Label { Text = nameEn, MouseFilter = MouseFilterEnum.Ignore };
		enTitle.AddThemeFontSizeOverride("font_size", 13);
		enTitle.AddThemeColorOverride("font_color", new Color(accent.R, accent.G, accent.B, 0.72f));
		titleBox.AddChild(enTitle);

		var cnTitle = new Label { Text = name, MouseFilter = MouseFilterEnum.Ignore };
		cnTitle.AddThemeFontSizeOverride("font_size", 30);
		cnTitle.AddThemeColorOverride("font_color", Colors.White);
		titleBox.AddChild(cnTitle);
		titlePlate.AddChild(titleBox);
		artStage.AddChild(titlePlate);

		var accentLine = new ColorRect { Color = accent * 0.72f, CustomMinimumSize = new Vector2(0, 3), SizeFlagsHorizontal = SizeFlags.ExpandFill };
		vbox.AddChild(artStage);
		vbox.AddChild(accentLine);

		var contentMargin = new MarginContainer();
		contentMargin.AddThemeConstantOverride("margin_left", 18);
		contentMargin.AddThemeConstantOverride("margin_right", 18);
		contentMargin.AddThemeConstantOverride("margin_top", 14);
		contentMargin.AddThemeConstantOverride("margin_bottom", 18);
		contentMargin.SizeFlagsVertical = SizeFlags.ExpandFill;

		var contentVbox = new VBoxContainer();
		contentVbox.AddThemeConstantOverride("separation", 10);

		var descLabel = new Label
		{
			Text = desc, AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(0, 54), HorizontalAlignment = HorizontalAlignment.Left
		};
		descLabel.AddThemeFontSizeOverride("font_size", 14);
		descLabel.AddThemeColorOverride("font_color", new Color(0.76f, 0.78f, 0.84f));
		contentVbox.AddChild(descLabel);

		var statsRow = new HBoxContainer();
		statsRow.AddThemeConstantOverride("separation", 10);
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
				BgColor = new Color(accent.R * 0.12f, accent.G * 0.12f, accent.B * 0.12f, 0.95f),
				BorderColor = accent * 0.35f,
				BorderWidthBottom = 1, BorderWidthTop = 1, BorderWidthLeft = 1, BorderWidthRight = 1,
				CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
				CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6,
				ContentMarginLeft = 12, ContentMarginRight = 12,
				ContentMarginTop = 4, ContentMarginBottom = 4
			};
			badge.AddThemeStyleboxOverride("panel", badgeStyle);
			var badgeLabel = new Label { Text = $"◈ {mechanic}" };
			badgeLabel.AddThemeFontSizeOverride("font_size", 12);
			badgeLabel.AddThemeColorOverride("font_color", accent * 0.85f);
			badge.AddChild(badgeLabel);
			badgeContainer.AddChild(badge);
			contentVbox.AddChild(badgeContainer);
		}

		contentVbox.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });

		// Select button
		var selectBtn = new Button { Text = $"▶  {name}" };
		selectBtn.AddThemeFontSizeOverride("font_size", 18);
		selectBtn.CustomMinimumSize = new Vector2(0, 46);
		var btnNorm = new StyleBoxFlat
		{
			BgColor = new Color(accent.R * 0.16f, accent.G * 0.16f, accent.B * 0.16f, 0.95f),
			BorderColor = accent * 0.48f,
			BorderWidthBottom = 1, BorderWidthTop = 1, BorderWidthLeft = 3, BorderWidthRight = 0,
			CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
			CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6,
			ContentMarginTop = 8, ContentMarginBottom = 8
		};
		var btnHov = (StyleBoxFlat)btnNorm.Duplicate();
		btnHov.BgColor = new Color(accent.R * 0.3f, accent.G * 0.3f, accent.B * 0.3f, 0.98f);
		btnHov.BorderColor = accent;
		btnHov.BorderWidthLeft = 5;
		btnHov.ShadowColor = accent * 0.25f;
		btnHov.ShadowSize = 8;
		selectBtn.AddThemeStyleboxOverride("normal", btnNorm);
		selectBtn.AddThemeStyleboxOverride("hover", btnHov);
		selectBtn.AddThemeColorOverride("font_color", new Color(0.88f, 0.9f, 0.94f));
		selectBtn.AddThemeColorOverride("font_hover_color", Colors.White);
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

	private static PanelContainer CreatePosterChip(string text, Color bg, Color font)
	{
		var chip = new PanelContainer();
		chip.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = bg,
			CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
			CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6,
			ContentMarginLeft = 8, ContentMarginRight = 8,
			ContentMarginTop = 4, ContentMarginBottom = 4
		});
		var label = new Label { Text = text };
		label.AddThemeFontSizeOverride("font_size", 11);
		label.AddThemeColorOverride("font_color", font);
		chip.AddChild(label);
		return chip;
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
