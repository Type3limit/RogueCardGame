using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RogueCardGame.Core.Cards;
using RogueCardGame.Core.Characters;
using RogueCardGame.Core.Combat;
using RogueCardGame.Core.Deck;
using RogueCardGame.Core.Map;

namespace RogueCardGame;

public partial class CombatScene : Control
{
	// --- Scene tree nodes ---
	private HBoxContainer _enemyArea = null!;
	private HBoxContainer _enemyFrontLane = null!;
	private HBoxContainer _enemyBackLane = null!;
	private Control _handArea = null!;
	private Button _endTurnBtn = null!;
	private Button _switchRowBtn = null!;
	private Label _rowLabel = null!;
	private Label _energyLabel = null!;
	private Label _blockLabel = null!;
	private Label _classResourceLabel = null!;
	private Label _drawPileLabel = null!;
	private Label _discardPileLabel = null!;
	private Label _exhaustPileLabel = null!;
	private Control _damageNumbers = null!;
	private Label _turnLabel = null!;

	// --- Player character display ---
	private ColorRect _playerPortraitGlow = null!;
	private ColorRect _playerPortraitScan = null!;
	private TextureRect _playerSprite = null!;
	private ProgressBar _hpBarDynamic = null!;
	private Label _hpTextDynamic = null!;
	private TextureRect _playerBlockIcon = null!;
	private Label _playerBlockLabel = null!;
	private Label _resourceBadge = null!;
	private Label _playerPowerLabel = null!;

	// --- Energy orb ---
	private PanelContainer _energyOrb = null!;
	private Label _energyOrbLabel = null!;

	// --- Pile displays ---
	private Label _drawPileCountLabel = null!;
	private Label _discardPileCountLabel = null!;
	private Label _exhaustPileCountLabel = null!;

	// --- Pile viewer overlay ---
	private enum PileViewMode { DrawPile, DiscardPile, ExhaustPile, FullDeck }
	private PileViewMode _currentPileViewMode;
	private Control? _pileViewerBackdrop;
	private Label? _pileViewerTitle;
	private Label? _pileViewerCountNote;
	private HFlowContainer? _pileViewerCardList;
	private Button[]? _pileViewerTabBtns;
	private Control? _scryBackdrop;

	// --- Readability / feedback UI ---
	private PanelContainer _cardPreviewPanel = null!;
	private StyleBoxFlat _cardPreviewStyle = null!;
	private Label _cardPreviewTitle = null!;
	private Label _cardPreviewMeta = null!;
	private Label _cardPreviewState = null!;
	private Label _cardPreviewDesc = null!;
	private PanelContainer _formationHintPanel = null!;
	private Label _formationHintLabel = null!;
	private Label _formationStateLabel = null!;
	private PanelContainer _selectionHintPanel = null!;
	private Label _selectionHintLabel = null!;
	private PanelContainer _toastPanel = null!;
	private Label _toastLabel = null!;
	private Tween? _toastTween;
	private PanelContainer? _renderLayerDebugPanel;

	// --- Combat state ---
	private CombatManager? _combat;
	private PlayerCharacter? _player;
	private DeckManager? _deck;
	private Card? _selectedCard;
	private readonly List<EnemyPanel> _enemyPanels = [];
	private readonly Dictionary<Card, Control> _cardButtons = new();
	private int _turnNumber;

	// --- Top bar ---
	private TopBarHUD? _topBar;

	// --- Animation state ---
	private bool _inputLocked;
	private bool? _combatResult;
	private int _hoveredCardIndex = -1;
	private Control? _hoveredCardHitbox;
	private Control? _hoveredCardVisual;
	private StyleBoxFlat? _hoveredCardStyle;
	private Color _hoveredCardBaseBorderColor = Colors.Transparent;
	private Color _hoveredCardBaseShadowColor = Colors.Transparent;
	private float _idleTime;
	private readonly List<Texture2D> _playerPortraitIdleFrames = [];
	private readonly List<Texture2D> _playerPortraitActionFrames = [];
	private float _playerPortraitIdleTime;
	private int _playerPortraitIdleFrame = -1;
	private float _playerPortraitActionTime;
	private int _playerPortraitActionFrame = -1;
	private bool _playerPortraitActionPlaying;

	// --- Card fan constants ---
	private const float CardWidth = 120f;
	private const float CardHeight = 168f;
	private const float BaseFanAngle = 2.2f;
	private const float DenseHandFanAngle = 1.4f;
	private const float BaseFanLift = 28f;
	private const float DenseHandFanLift = 18f;
	private const float HoverLift = 56f;
	private const float HoverScale = 1.12f;
	private const float HoverSidePadding = CardWidth * (HoverScale - 1f) * 0.5f;
	private const float HoverTopPadding = CardHeight * (HoverScale - 1f);
	private const float CardHitboxWidth = CardWidth + HoverSidePadding * 2f;
	private const float CardHitboxHeight = CardHeight + HoverLift + HoverTopPadding;
	private const float MinDenseHandSpacingRatio = 0.72f;
	private const float PlayerPortraitIdleFps = 25f;
	private const float PlayerPortraitActionFps = 25f;
	private static readonly bool DebugRenderLayers = false;
	private static readonly bool DebugHandRenderDump = false;
	private int _handRenderDumpSequence;

	// --- Texture cache ---
	private static readonly Dictionary<string, Texture2D> _textureCache = new();

	public static void ClearTextureCache()
	{
		_textureCache.Clear();
	}

	private static Texture2D LoadTex(string resPath)
	{
		if (_textureCache.TryGetValue(resPath, out var cached)) return cached;
		var tex = LoadRasterTextureFromFile(resPath) ?? GD.Load<Texture2D>(resPath);
		if (tex != null) _textureCache[resPath] = tex;
		return tex!;
	}

	private static Texture2D? LoadRasterTextureFromFile(string path)
	{
		string ext = Path.GetExtension(path).ToLowerInvariant();
		if (ext is not (".png" or ".jpg" or ".jpeg" or ".webp"))
			return null;

		string filePath = ProjectSettings.GlobalizePath(path);
		if (!File.Exists(filePath))
			return null;

		var image = Image.LoadFromFile(filePath);
		if (image == null || image.IsEmpty())
			return null;

		return ImageTexture.CreateFromImage(image);
	}

	private static string GetCharacterTexturePath(CardClass cls) => cls switch
	{
		CardClass.Vanguard => "res://resources/textures/characters/vanguard.png",
		CardClass.Psion => "res://resources/textures/characters/psion.png",
		CardClass.Netrunner => "res://resources/textures/characters/netrunner.png",
		CardClass.Symbiote => "res://resources/textures/characters/symbiote.png",
		_ => "res://resources/textures/characters/vanguard.png"
	};

	private static string GetCharacterAnimationClassDir(CardClass cls) => cls switch
	{
		CardClass.Vanguard => "vanguard",
		CardClass.Psion => "psion",
		CardClass.Netrunner => "netrunner",
		CardClass.Symbiote => "symbiote",
		_ => string.Empty
	};

	private static string GetCharacterAnimationDir(CardClass cls, string animation)
	{
		string classDir = GetCharacterAnimationClassDir(cls);
		return classDir.Length == 0 || string.IsNullOrWhiteSpace(animation)
			? string.Empty
			: $"res://resources/textures/characters/animations/{classDir}/{animation}";
	}

	private static string GetCharacterCombatIdleDir(CardClass cls) =>
		GetCharacterAnimationDir(cls, "combat_idle");

	private static string GetCharacterSpecialAnimationName(CardClass cls) => cls switch
	{
		CardClass.Vanguard => "overload",
		CardClass.Psion => "resonance",
		CardClass.Netrunner => "protocol_stack",
		CardClass.Symbiote => "erosion",
		_ => string.Empty
	};

	private static bool ResourceFileExists(string resPath)
	{
		if (string.IsNullOrWhiteSpace(resPath))
			return false;

		string filePath = ProjectSettings.GlobalizePath(resPath);
		return File.Exists(filePath) || ResourceLoader.Exists(resPath);
	}

	private static string GetEnemyTexturePath(Enemy enemy)
	{
		if (enemy.Data.IsBoss) return "res://resources/textures/enemies/enemy_boss.svg";
		if (enemy.Data.IsElite) return "res://resources/textures/enemies/enemy_elite.svg";
		return "res://resources/textures/enemies/enemy_generic.svg";
	}

	private static string GetCardArtPath(CardType type) => type switch
	{
		CardType.Attack => "res://resources/textures/cards/card_art_attack.svg",
		CardType.Skill => "res://resources/textures/cards/card_art_skill.svg",
		CardType.Power => "res://resources/textures/cards/card_art_power.svg",
		_ => "res://resources/textures/cards/card_art_skill.svg"
	};

	public override void _Ready()
	{
		GameManager.Instance.SetCurrentRunScene("combat");

		_enemyArea = GetNode<HBoxContainer>("EnemyArea");
		_handArea = GetNode<Control>("HandArea");
		_endTurnBtn = GetNode<Button>("EndTurnBtn");
		_switchRowBtn = GetNode<Button>("FormationArea/SwitchRowBtn");
		_rowLabel = GetNode<Label>("FormationArea/RowLabel");
		_energyLabel = GetNode<Label>("PlayerInfo/EnergyLabel");
		_blockLabel = GetNode<Label>("PlayerInfo/BlockLabel");
		_classResourceLabel = GetNode<Label>("PlayerInfo/ClassResourceLabel");
		_drawPileLabel = GetNode<Label>("DeckInfo/DrawPileLabel");
		_discardPileLabel = GetNode<Label>("DeckInfo/DiscardPileLabel");
		_exhaustPileLabel = GetNode<Label>("DeckInfo/ExhaustPileLabel");
		_damageNumbers = GetNode<Control>("DamageNumbers");
		_turnLabel = GetNode<Label>("TurnLabel");

		_endTurnBtn.Pressed += OnEndTurnPressed;
		_switchRowBtn.Pressed += OnSwitchRowPressed;

		GetNode<ProgressBar>("PlayerInfo/HpBar").Visible = false;
		GetNode<Label>("PlayerInfo/HpText").Visible = false;
		_energyLabel.Visible = false;
		_blockLabel.Visible = false;
		_classResourceLabel.Visible = false;

		var backgroundImage = GetNode<Control>("BackgroundImage");
		var scanlines = CyberFx.AddScanlines(this, 0.03f);
		var vignette = CyberFx.AddVignette(this);
		if (DebugRenderLayers)
		{
			scanlines.Name = "DebugScanlines";
			vignette.Name = "DebugVignette";
		}
		MoveChild(scanlines, backgroundImage.GetIndex() + 1);
		MoveChild(vignette, scanlines.GetIndex() + 1);
		if (DebugRenderLayers)
		{
			AddSceneLayerBadge(scanlines, $"scan #{scanlines.GetIndex():00}", new Color(0.18f, 0.9f, 0.98f), 12f);
			AddSceneLayerBadge(vignette, $"vig #{vignette.GetIndex():00}", new Color(1f, 0.56f, 0.32f), 40f);
		}

		_topBar = TopBarHUD.Attach(this);

		BuildPlayerCharacter();
		BuildEnergyOrb();
		BuildPileDisplays();
		BuildCombatHelpUi();
		StyleUI();
		StartCombat();
		if (DebugRenderLayers)
			CallDeferred(nameof(RefreshRenderLayerDebugOverlay));
	}

	public override void _Input(InputEvent ev)
	{
		if (_scryBackdrop?.Visible == true && ev.IsActionPressed("ui_cancel"))
		{
			GetViewport().SetInputAsHandled();
			return;
		}

		// Close pile viewer with Escape (takes priority over card selection cancel)
		if (_pileViewerBackdrop?.Visible == true && ev.IsActionPressed("ui_cancel"))
		{
			HidePileViewer();
			GetViewport().SetInputAsHandled();
			return;
		}

		if (_inputLocked || _selectedCard == null)
			return;

		if (ev.IsActionPressed("ui_cancel") || ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right })
		{
			CancelCardSelection("已取消选牌");
			GetViewport().SetInputAsHandled();
		}
	}

	private void RefreshRenderLayerDebugOverlay()
	{
		if (!DebugRenderLayers)
			return;

		var entries = new List<(string label, Color color)>();
		foreach (Node child in GetChildren())
		{
			if (child.Name.ToString() == "RenderLayerDebugPanel" || child is not CanvasItem canvasItem)
				continue;

			string childName = child.Name.ToString();
			entries.Add(($"{canvasItem.GetIndex():00} {childName}", GetRenderLayerDebugColor(childName)));
		}

		_renderLayerDebugPanel?.QueueFree();
		_renderLayerDebugPanel = CreateRenderLayerDebugPanel(entries);
		AddChild(_renderLayerDebugPanel);
	}

	private static PanelContainer CreateRenderLayerDebugPanel(IReadOnlyList<(string label, Color color)> entries)
	{
		var panel = new PanelContainer
		{
			Name = "RenderLayerDebugPanel",
			MouseFilter = MouseFilterEnum.Ignore,
			ZIndex = 4096
		};
		panel.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
		panel.OffsetLeft = 12f;
		panel.OffsetTop = 12f;
		panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.02f, 0.026f, 0.038f, 0.94f),
			BorderColor = new Color(0.78f, 0.84f, 0.96f, 0.26f),
			BorderWidthBottom = 1,
			BorderWidthLeft = 1,
			BorderWidthRight = 1,
			BorderWidthTop = 1,
			CornerRadiusBottomLeft = 6,
			CornerRadiusBottomRight = 6,
			CornerRadiusTopLeft = 6,
			CornerRadiusTopRight = 6,
			ContentMarginBottom = 6,
			ContentMarginLeft = 8,
			ContentMarginRight = 8,
			ContentMarginTop = 6
		});

		var box = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		box.AddThemeConstantOverride("separation", 2);

		var title = new Label
		{
			Text = "COMBAT ROOT ORDER",
			MouseFilter = MouseFilterEnum.Ignore
		};
		title.AddThemeFontSizeOverride("font_size", 9);
		title.AddThemeColorOverride("font_color", new Color(0.92f, 0.96f, 1f));
		box.AddChild(title);

		foreach (var (label, color) in entries)
		{
			var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
			row.AddThemeConstantOverride("separation", 5);

			var swatch = new ColorRect
			{
				Color = color,
				CustomMinimumSize = new Vector2(7f, 7f),
				MouseFilter = MouseFilterEnum.Ignore
			};
			row.AddChild(swatch);

			var itemLabel = new Label
			{
				Text = label,
				MouseFilter = MouseFilterEnum.Ignore
			};
			itemLabel.AddThemeFontSizeOverride("font_size", 8);
			itemLabel.AddThemeColorOverride("font_color", new Color(0.84f, 0.88f, 0.96f));
			row.AddChild(itemLabel);

			box.AddChild(row);
		}

		panel.AddChild(box);
		return panel;
	}

	private static Color GetRenderLayerDebugColor(string nodeName)
	{
		string lowerName = nodeName.ToLowerInvariant();
		if (lowerName.Contains("background"))
			return new Color(0.46f, 0.62f, 0.98f);
		if (lowerName.Contains("enemy"))
			return new Color(1f, 0.42f, 0.42f);
		if (lowerName.Contains("hand"))
			return new Color(0.52f, 0.96f, 0.72f);
		if (lowerName.Contains("scan"))
			return new Color(0.18f, 0.9f, 0.98f);
		if (lowerName.Contains("vignette"))
			return new Color(1f, 0.56f, 0.32f);
		if (lowerName.Contains("damage"))
			return new Color(1f, 0.84f, 0.28f);
		if (lowerName.Contains("turn") || lowerName.Contains("topbar"))
			return new Color(0.9f, 0.72f, 1f);
		return new Color(0.76f, 0.82f, 0.94f);
	}

	private static void AddSceneLayerBadge(Control layer, string label, Color color, float topOffset)
	{
		if (layer.GetNodeOrNull<PanelContainer>("DebugLayerBadge") != null)
			return;

		var badge = new PanelContainer
		{
			Name = "DebugLayerBadge",
			MouseFilter = MouseFilterEnum.Ignore,
			ZIndex = 4096
		};
		badge.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
		badge.OffsetLeft = 12f;
		badge.OffsetTop = topOffset;
		badge.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.025f, 0.03f, 0.044f, 0.9f),
			BorderColor = new Color(color.R, color.G, color.B, 0.42f),
			BorderWidthBottom = 1,
			BorderWidthLeft = 1,
			BorderWidthRight = 1,
			BorderWidthTop = 1,
			CornerRadiusBottomLeft = 5,
			CornerRadiusBottomRight = 5,
			CornerRadiusTopLeft = 5,
			CornerRadiusTopRight = 5,
			ContentMarginBottom = 4,
			ContentMarginLeft = 6,
			ContentMarginRight = 6,
			ContentMarginTop = 4
		});

		var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		row.AddThemeConstantOverride("separation", 4);

		var swatch = new ColorRect
		{
			Color = color,
			CustomMinimumSize = new Vector2(6f, 6f),
			MouseFilter = MouseFilterEnum.Ignore
		};
		row.AddChild(swatch);

		var text = new Label
		{
			Text = label,
			MouseFilter = MouseFilterEnum.Ignore
		};
		text.AddThemeFontSizeOverride("font_size", 9);
		text.AddThemeColorOverride("font_color", new Color(0.9f, 0.94f, 1f));
		row.AddChild(text);

		badge.AddChild(row);
		layer.AddChild(badge);
	}

	// ======================================================================
	//  PLAYER CHARACTER DISPLAY (STS2-style: sprite on battlefield left)
	// ======================================================================
	private void BuildPlayerCharacter()
	{
		_playerPortraitGlow = new ColorRect
		{
			Color = new Color(0f, 0f, 0f, 0f),
			MouseFilter = MouseFilterEnum.Ignore
		};
		_playerPortraitGlow.SetAnchorsPreset(LayoutPreset.FullRect);
		_playerPortraitGlow.AnchorLeft = 0.055f; _playerPortraitGlow.AnchorRight = 0.225f;
		_playerPortraitGlow.AnchorTop = 0.075f; _playerPortraitGlow.AnchorBottom = 0.525f;
		AddChild(_playerPortraitGlow);

		_playerPortraitScan = new ColorRect
		{
			Color = new Color(1f, 1f, 1f, 0.08f),
			MouseFilter = MouseFilterEnum.Ignore
		};
		_playerPortraitScan.SetAnchorsPreset(LayoutPreset.FullRect);
		_playerPortraitScan.AnchorLeft = 0.06f; _playerPortraitScan.AnchorRight = 0.22f;
		_playerPortraitScan.AnchorTop = 0.08f; _playerPortraitScan.AnchorBottom = 0.083f;
		AddChild(_playerPortraitScan);

		// Player sprite - left side of battlefield
		_playerSprite = new TextureRect
		{
			Name = "PlayerPortrait",
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		_playerSprite.SetAnchorsPreset(LayoutPreset.FullRect);
		_playerSprite.AnchorLeft = 0.06f; _playerSprite.AnchorRight = 0.22f;
		_playerSprite.AnchorTop = 0.08f; _playerSprite.AnchorBottom = 0.52f;
		AddChild(_playerSprite);
		MoveChild(_playerPortraitScan, _playerSprite.GetIndex() + 1);

		// HP bar directly under player sprite
		var hpPanel = new PanelContainer();
		hpPanel.SetAnchorsPreset(LayoutPreset.FullRect);
		hpPanel.AnchorLeft = 0.06f; hpPanel.AnchorRight = 0.22f;
		hpPanel.AnchorTop = 0.53f; hpPanel.AnchorBottom = 0.575f;
		var hpStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.02f, 0.02f, 0.05f, 0.85f),
			CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3,
			CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3,
			ContentMarginLeft = 4, ContentMarginRight = 4,
			ContentMarginTop = 1, ContentMarginBottom = 1,
		};
		hpPanel.AddThemeStyleboxOverride("panel", hpStyle);
		AddChild(hpPanel);

		var hpHbox = new HBoxContainer();
		hpHbox.AddThemeConstantOverride("separation", 4);

		var hpIconTex = new TextureRect
		{
			Texture = LoadTex("res://resources/textures/ui/icon_hp.svg"),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			CustomMinimumSize = new Vector2(16, 16),
		};
		hpHbox.AddChild(hpIconTex);

		_hpBarDynamic = new ProgressBar
		{
			CustomMinimumSize = new Vector2(60, 14),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			ShowPercentage = false
		};
		_hpBarDynamic.AddThemeStyleboxOverride("background", new StyleBoxFlat
		{
			BgColor = new Color(0.1f, 0.03f, 0.03f),
			CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3,
			CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3
		});
		hpHbox.AddChild(_hpBarDynamic);

		_hpTextDynamic = new Label { Text = "HP" };
		_hpTextDynamic.AddThemeFontSizeOverride("font_size", 12);
		_hpTextDynamic.AddThemeColorOverride("font_color", Colors.White);
		hpHbox.AddChild(_hpTextDynamic);

		hpPanel.AddChild(hpHbox);

		// Block badge under HP bar
		var blockRow = new HBoxContainer();
		blockRow.SetAnchorsPreset(LayoutPreset.FullRect);
		blockRow.AnchorLeft = 0.06f; blockRow.AnchorRight = 0.22f;
		blockRow.AnchorTop = 0.58f; blockRow.AnchorBottom = 0.605f;
		blockRow.AddThemeConstantOverride("separation", 3);
		AddChild(blockRow);

		_playerBlockIcon = new TextureRect
		{
			Texture = LoadTex("res://resources/textures/ui/icon_block.svg"),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			CustomMinimumSize = new Vector2(14, 14),
			Visible = false,
		};
		blockRow.AddChild(_playerBlockIcon);

		_playerBlockLabel = new Label { Text = "" };
		_playerBlockLabel.AddThemeFontSizeOverride("font_size", 12);
		_playerBlockLabel.AddThemeColorOverride("font_color", new Color(0.35f, 0.65f, 1f));
		blockRow.AddChild(_playerBlockLabel);

		// Resource badge
		_resourceBadge = new Label { Text = "" };
		_resourceBadge.SetAnchorsPreset(LayoutPreset.FullRect);
		_resourceBadge.AnchorLeft = 0.06f; _resourceBadge.AnchorRight = 0.40f;
		_resourceBadge.AnchorTop = 0.61f; _resourceBadge.AnchorBottom = 0.64f;
		_resourceBadge.AddThemeFontSizeOverride("font_size", 11);
		_resourceBadge.AddThemeColorOverride("font_color", new Color(0.7f, 0.5f, 0.1f));
		AddChild(_resourceBadge);

		// Power status
		_playerPowerLabel = new Label { Text = "" };
		_playerPowerLabel.SetAnchorsPreset(LayoutPreset.FullRect);
		_playerPowerLabel.AnchorLeft = 0.06f; _playerPowerLabel.AnchorRight = 0.40f;
		_playerPowerLabel.AnchorTop = 0.645f; _playerPowerLabel.AnchorBottom = 0.675f;
		_playerPowerLabel.AddThemeFontSizeOverride("font_size", 10);
		_playerPowerLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.7f, 0.8f));
		AddChild(_playerPowerLabel);
	}

	// ======================================================================
	//  ENERGY ORB (STS2-style: bottom-left)
	// ======================================================================
	private void BuildEnergyOrb()
	{
		_energyOrb = new PanelContainer();
		_energyOrb.SetAnchorsPreset(LayoutPreset.FullRect);
		_energyOrb.AnchorLeft = 0.02f; _energyOrb.AnchorRight = 0.07f;
		_energyOrb.AnchorTop = 0.74f; _energyOrb.AnchorBottom = 0.84f;
		_energyOrb.PivotOffset = new Vector2(30, 30);
		var orbStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.01f, 0.06f, 0.10f, 0.97f),
			BorderColor = new Color(0f, 0.85f, 0.85f),
			BorderWidthBottom = 2, BorderWidthTop = 2,
			BorderWidthLeft = 2, BorderWidthRight = 2,
			CornerRadiusBottomLeft = 28, CornerRadiusBottomRight = 28,
			CornerRadiusTopLeft = 28, CornerRadiusTopRight = 28,
			ShadowColor = new Color(0f, 0.5f, 0.7f, 0.25f),
			ShadowSize = 6
		};
		_energyOrb.AddThemeStyleboxOverride("panel", orbStyle);
		_energyOrbLabel = new Label
		{
			Text = "3/3",
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center
		};
		_energyOrbLabel.AddThemeFontSizeOverride("font_size", 20);
		_energyOrbLabel.AddThemeColorOverride("font_color", new Color(0f, 1f, 1f));
		_energyOrb.AddChild(_energyOrbLabel);
		AddChild(_energyOrb);
	}

	// ======================================================================
	//  _PROCESS: Idle animations
	// ======================================================================
	public override void _Process(double delta)
	{
		_idleTime += (float)delta;

		// Enemy idle breathing
		foreach (var ep in _enemyPanels)
		{
			if (!ep.Enemy.IsAlive) continue;
			float phase = _idleTime * 1.2f + ep.Enemy.Name.GetHashCode() * 0.1f;
			float breathe = 1f + 0.012f * MathF.Sin(phase);
			float sway = 0.003f * MathF.Sin(phase * 0.7f);
			ep.SpriteRect.Scale = new Vector2(breathe + sway, breathe - sway);
		}

		// Player sprite idle breathing
		if (_playerSprite != null && _player != null && _player.IsAlive)
		{
			if (!UpdatePlayerPortraitAction(delta))
				UpdatePlayerPortraitIdle(delta);

			float pp = _idleTime * 1.0f;
			float pb = 1f + 0.008f * MathF.Sin(pp);
			_playerSprite.Scale = new Vector2(pb, pb);
			UpdatePlayerPortraitFx(pp);
		}

		// Energy orb pulse
		if (_player != null && _player.CurrentEnergy > 0 && !_inputLocked)
		{
			float pulse = 1f + 0.02f * MathF.Sin(_idleTime * 2.5f);
			_energyOrb.Scale = new Vector2(pulse, pulse);
		}
	}

	public override void _ExitTree()
	{
		ReleasePlayerPortraitFrames();
	}

	// ======================================================================
	//  SCREEN SHAKE
	// ======================================================================
	private void ScreenShake(float intensity = 6f, int shakes = 4)
	{
		var origPos = Position;
		var tw = CreateTween();
		for (int i = 0; i < shakes; i++)
		{
			float f = intensity * (1f - (float)i / shakes);
			tw.TweenProperty(this, "position",
				origPos + new Vector2((float)GD.RandRange(-f, f), (float)GD.RandRange(-f, f)), 0.025f);
		}
		tw.TweenProperty(this, "position", origPos, 0.025f);
	}

	// ======================================================================
	//  STYLE
	// ======================================================================
	private void StyleUI()
	{
		var endNorm = new StyleBoxFlat
		{
			BgColor = new Color(0.5f, 0.08f, 0.08f, 0.95f),
			BorderColor = new Color(0.9f, 0.3f, 0.2f),
			BorderWidthBottom = 2, BorderWidthTop = 2,
			BorderWidthLeft = 2, BorderWidthRight = 2,
			CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
			CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6,
			ShadowColor = new Color(0.9f, 0.15f, 0.08f, 0.2f),
			ShadowSize = 4
		};
		var endHov = (StyleBoxFlat)endNorm.Duplicate();
		endHov.BgColor = new Color(0.65f, 0.12f, 0.12f);
		endHov.ShadowSize = 8;
		_endTurnBtn.AddThemeStyleboxOverride("normal", endNorm);
		_endTurnBtn.AddThemeStyleboxOverride("hover", endHov);
		_endTurnBtn.AddThemeColorOverride("font_color", Colors.White);
		_endTurnBtn.AddThemeFontSizeOverride("font_size", 14);

		var swNorm = new StyleBoxFlat
		{
			BgColor = new Color(0.04f, 0.1f, 0.18f, 0.85f),
			BorderColor = new Color(0f, 0.4f, 0.5f),
			BorderWidthBottom = 1, BorderWidthTop = 1,
			BorderWidthLeft = 1, BorderWidthRight = 1,
			CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
			CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4
		};
		_switchRowBtn.AddThemeStyleboxOverride("normal", swNorm);
		_switchRowBtn.AddThemeColorOverride("font_color", new Color(0f, 0.7f, 0.75f));
		_switchRowBtn.AddThemeFontSizeOverride("font_size", 11);

		GetNode<Label>("FormationArea/FormationLabel").Visible = false;
		_rowLabel.AddThemeFontSizeOverride("font_size", 12);

		_turnLabel.AddThemeFontSizeOverride("font_size", 14);
		_turnLabel.AddThemeColorOverride("font_color", new Color(0.35f, 0.5f, 0.7f, 0.5f));
	}

	// ======================================================================
	//  PILE DISPLAYS (STS2-style: draw pile bottom-left, discard bottom-right)
	// ======================================================================
	private void BuildPileDisplays()
	{
		// Draw pile (bottom-left, below energy orb)
		var drawPile = CreatePilePanel(
			"res://resources/textures/ui/icon_draw_pile.svg",
			new Color(0.2f, 0.55f, 0.8f));
		drawPile.SetAnchorsPreset(LayoutPreset.FullRect);
		drawPile.AnchorLeft = 0.02f; drawPile.AnchorRight = 0.07f;
		drawPile.AnchorTop = 0.86f; drawPile.AnchorBottom = 0.96f;
		AddChild(drawPile);
		_drawPileCountLabel = drawPile.GetChild(0).GetChild<Label>(1);
		drawPile.MouseDefaultCursorShape = CursorShape.PointingHand;
		drawPile.GuiInput += ev =>
		{
			if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
				ShowPileViewer(PileViewMode.DrawPile);
		};

		// Discard pile (bottom-right)
		var discardPile = CreatePilePanel(
			"res://resources/textures/ui/icon_discard_pile.svg",
			new Color(0.65f, 0.35f, 0.15f));
		discardPile.SetAnchorsPreset(LayoutPreset.FullRect);
		discardPile.AnchorLeft = 0.93f; discardPile.AnchorRight = 0.98f;
		discardPile.AnchorTop = 0.86f; discardPile.AnchorBottom = 0.96f;
		AddChild(discardPile);
		_discardPileCountLabel = discardPile.GetChild(0).GetChild<Label>(1);
		discardPile.MouseDefaultCursorShape = CursorShape.PointingHand;
		discardPile.GuiInput += ev =>
		{
			if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
				ShowPileViewer(PileViewMode.DiscardPile);
		};

		// Exhaust pile (small, above discard)
		var exhaustPile = CreatePilePanel(
			"res://resources/textures/ui/icon_exhaust_pile.svg",
			new Color(0.5f, 0.15f, 0.15f));
		exhaustPile.SetAnchorsPreset(LayoutPreset.FullRect);
		exhaustPile.AnchorLeft = 0.93f; exhaustPile.AnchorRight = 0.98f;
		exhaustPile.AnchorTop = 0.78f; exhaustPile.AnchorBottom = 0.85f;
		AddChild(exhaustPile);
		_exhaustPileCountLabel = exhaustPile.GetChild(0).GetChild<Label>(1);
		exhaustPile.MouseDefaultCursorShape = CursorShape.PointingHand;
		exhaustPile.GuiInput += ev =>
		{
			if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
				ShowPileViewer(PileViewMode.ExhaustPile);
		};
	}

	private PanelContainer CreatePilePanel(string iconPath, Color accent)
	{
		var panel = new PanelContainer();
		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.03f, 0.03f, 0.06f, 0.88f),
			BorderColor = accent * 0.4f,
			BorderWidthBottom = 1, BorderWidthTop = 1,
			BorderWidthLeft = 1, BorderWidthRight = 1,
			CornerRadiusBottomLeft = 5, CornerRadiusBottomRight = 5,
			CornerRadiusTopLeft = 5, CornerRadiusTopRight = 5,
			ContentMarginLeft = 3, ContentMarginRight = 3,
			ContentMarginTop = 2, ContentMarginBottom = 2
		};
		panel.AddThemeStyleboxOverride("panel", style);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 0);

		var icon = new TextureRect
		{
			Texture = LoadTex(iconPath),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			CustomMinimumSize = new Vector2(24, 24),
			SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
		};
		vbox.AddChild(icon);

		var countLabel = new Label
		{
			Text = "0",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		countLabel.AddThemeFontSizeOverride("font_size", 16);
		countLabel.AddThemeColorOverride("font_color", accent);
		vbox.AddChild(countLabel);

		panel.AddChild(vbox);
		return panel;
	}

	private void BuildCombatHelpUi()
	{
		BuildCardPreviewPanel();
		BuildFormationHintPanel();
		BuildSelectionHintPanel();
		BuildToastPanel();
		BuildPileViewerOverlay();
		SetCardPreviewPlaceholder();
	}

	private void BuildCardPreviewPanel()
	{
		_cardPreviewPanel = new PanelContainer();
		_cardPreviewPanel.SetAnchorsPreset(LayoutPreset.FullRect);
		_cardPreviewPanel.AnchorLeft = 0.23f; _cardPreviewPanel.AnchorRight = 0.43f;
		_cardPreviewPanel.AnchorTop = 0.12f; _cardPreviewPanel.AnchorBottom = 0.56f;
		_cardPreviewPanel.MouseFilter = MouseFilterEnum.Ignore;

		_cardPreviewStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.03f, 0.04f, 0.08f, 0.96f),
			BorderColor = new Color(0.16f, 0.25f, 0.35f),
			BorderWidthBottom = 2, BorderWidthTop = 2,
			BorderWidthLeft = 2, BorderWidthRight = 2,
			CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10,
			CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10,
			ContentMarginLeft = 14, ContentMarginRight = 14,
			ContentMarginTop = 12, ContentMarginBottom = 12,
			ShadowColor = new Color(0f, 0f, 0f, 0.28f),
			ShadowSize = 10,
			ShadowOffset = new Vector2(0, 4)
		};
		_cardPreviewPanel.AddThemeStyleboxOverride("panel", _cardPreviewStyle);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 6);

		var header = new Label { Text = "战斗预览" };
		header.AddThemeFontSizeOverride("font_size", 11);
		header.AddThemeColorOverride("font_color", new Color(0.35f, 0.7f, 0.85f, 0.8f));
		vbox.AddChild(header);

		_cardPreviewTitle = new Label();
		_cardPreviewTitle.AddThemeFontSizeOverride("font_size", 24);
		_cardPreviewTitle.AddThemeColorOverride("font_color", Colors.White);
		_cardPreviewTitle.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		vbox.AddChild(_cardPreviewTitle);

		_cardPreviewMeta = new Label();
		_cardPreviewMeta.AddThemeFontSizeOverride("font_size", 11);
		_cardPreviewMeta.AddThemeColorOverride("font_color", new Color(0.5f, 0.62f, 0.72f));
		_cardPreviewMeta.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		vbox.AddChild(_cardPreviewMeta);

		var divider = new ColorRect
		{
			Color = new Color(0.18f, 0.28f, 0.38f, 0.6f),
			CustomMinimumSize = new Vector2(0, 1)
		};
		vbox.AddChild(divider);

		_cardPreviewState = new Label();
		_cardPreviewState.AddThemeFontSizeOverride("font_size", 12);
		_cardPreviewState.AddThemeColorOverride("font_color", new Color(0.7f, 0.8f, 0.85f));
		_cardPreviewState.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		vbox.AddChild(_cardPreviewState);

		_cardPreviewDesc = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			VerticalAlignment = VerticalAlignment.Top
		};
		_cardPreviewDesc.AddThemeFontSizeOverride("font_size", 16);
		_cardPreviewDesc.AddThemeColorOverride("font_color", new Color(0.83f, 0.86f, 0.92f));
		vbox.AddChild(_cardPreviewDesc);

		_cardPreviewPanel.AddChild(vbox);
		AddChild(_cardPreviewPanel);
	}

	private void BuildFormationHintPanel()
	{
		_formationHintPanel = new PanelContainer();
		_formationHintPanel.SetAnchorsPreset(LayoutPreset.FullRect);
		_formationHintPanel.AnchorLeft = 0.08f; _formationHintPanel.AnchorRight = 0.22f;
		_formationHintPanel.AnchorTop = 0.68f; _formationHintPanel.AnchorBottom = 0.82f;
		_formationHintPanel.MouseFilter = MouseFilterEnum.Ignore;
		_formationHintPanel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.03f, 0.05f, 0.08f, 0.92f),
			BorderColor = new Color(0.0f, 0.45f, 0.55f, 0.45f),
			BorderWidthBottom = 1, BorderWidthTop = 1,
			BorderWidthLeft = 2, BorderWidthRight = 1,
			CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
			CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8,
			ContentMarginLeft = 10, ContentMarginRight = 10,
			ContentMarginTop = 8, ContentMarginBottom = 8
		});

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 4);

		var title = new Label { Text = "站位规则" };
		title.AddThemeFontSizeOverride("font_size", 11);
		title.AddThemeColorOverride("font_color", new Color(0.35f, 0.7f, 0.85f, 0.75f));
		vbox.AddChild(title);

		_formationHintLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
		_formationHintLabel.AddThemeFontSizeOverride("font_size", 12);
		_formationHintLabel.AddThemeColorOverride("font_color", new Color(0.86f, 0.9f, 0.94f));
		vbox.AddChild(_formationHintLabel);

		_formationStateLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
		_formationStateLabel.AddThemeFontSizeOverride("font_size", 11);
		_formationStateLabel.AddThemeColorOverride("font_color", new Color(0.58f, 0.68f, 0.76f));
		vbox.AddChild(_formationStateLabel);

		_formationHintPanel.AddChild(vbox);
		AddChild(_formationHintPanel);
	}

	private void BuildSelectionHintPanel()
	{
		_selectionHintPanel = new PanelContainer();
		_selectionHintPanel.SetAnchorsPreset(LayoutPreset.FullRect);
		_selectionHintPanel.AnchorLeft = 0.38f; _selectionHintPanel.AnchorRight = 0.62f;
		_selectionHintPanel.AnchorTop = 0.60f; _selectionHintPanel.AnchorBottom = 0.65f;
		_selectionHintPanel.Visible = false;
		_selectionHintPanel.MouseFilter = MouseFilterEnum.Ignore;
		_selectionHintPanel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.06f, 0.11f, 0.16f, 0.94f),
			BorderColor = new Color(0f, 0.78f, 0.82f, 0.55f),
			BorderWidthBottom = 1, BorderWidthTop = 1,
			BorderWidthLeft = 2, BorderWidthRight = 2,
			CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
			CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8,
			ContentMarginLeft = 12, ContentMarginRight = 12,
			ContentMarginTop = 8, ContentMarginBottom = 8
		});

		_selectionHintLabel = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		_selectionHintLabel.AddThemeFontSizeOverride("font_size", 13);
		_selectionHintLabel.AddThemeColorOverride("font_color", new Color(0.82f, 0.96f, 0.98f));
		_selectionHintPanel.AddChild(_selectionHintLabel);
		AddChild(_selectionHintPanel);
	}

	private void BuildToastPanel()
	{
		_toastPanel = new PanelContainer();
		_toastPanel.SetAnchorsPreset(LayoutPreset.FullRect);
		_toastPanel.AnchorLeft = 0.31f; _toastPanel.AnchorRight = 0.69f;
		_toastPanel.AnchorTop = 0.665f; _toastPanel.AnchorBottom = 0.715f;
		_toastPanel.Visible = false;
		_toastPanel.MouseFilter = MouseFilterEnum.Ignore;
		_toastPanel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.06f, 0.08f, 0.11f, 0.95f),
			BorderColor = new Color(0.28f, 0.4f, 0.5f, 0.7f),
			BorderWidthBottom = 1, BorderWidthTop = 1,
			BorderWidthLeft = 2, BorderWidthRight = 2,
			CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
			CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8,
			ContentMarginLeft = 12, ContentMarginRight = 12,
			ContentMarginTop = 6, ContentMarginBottom = 6,
			ShadowColor = new Color(0f, 0f, 0f, 0.2f),
			ShadowSize = 8,
			ShadowOffset = new Vector2(0, 3)
		});

		_toastLabel = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		_toastLabel.AddThemeFontSizeOverride("font_size", 13);
		_toastLabel.AddThemeColorOverride("font_color", new Color(0.84f, 0.9f, 0.95f));
		_toastPanel.AddChild(_toastLabel);
		AddChild(_toastPanel);
	}

	private void SetCardPreviewPlaceholder()
	{
		_cardPreviewTitle.Text = "悬停卡牌查看详情";
		_cardPreviewMeta.Text = "费用 / 类型 / 射程 / 目标";
		_cardPreviewState.Text = "选中需要目标的牌后，这里会锁定显示；右键或 Esc 可以取消选牌。";
		_cardPreviewDesc.Text = "优先关注：是否受站位限制、当前能不能打、敌方下一步会打谁。";
		SetCardPreviewAccent(new Color(0.16f, 0.25f, 0.35f), false);
	}

	private void RefreshCardPreview()
	{
		if (_selectedCard != null)
		{
			bool canPlay = _combat != null && _player != null && _combat.CanPlay(_player, _selectedCard);
			UpdateCardPreview(_selectedCard, canPlay, true);
			return;
		}

		SetCardPreviewPlaceholder();
	}

	private void UpdateCardPreview(Card card, bool canPlay, bool locked = false)
	{
		var accent = GetCardTypeColor(card);
		var row = _combat != null && _player != null
			? _combat.Formation.GetPosition(_player.Id)
			: FormationRow.Front;
		string rowText = row == FormationRow.Front ? "前排" : "后排";
		string targetText = GetTargetTypeText(card.Data.EffectiveTargetType);
		string rangeText = GetRangeText(card.Data.Range);
		string cardTypeText = GetCardTypeText(card.Data.Type);
		string stateText;

		if (card.IsPositionReactive)
		{
			stateText = $"当前站位：{rowText}，本次将触发{rowText}效果";
		}
		else if (!canPlay)
		{
			stateText = $"当前不可打出：{GetCardPlayBlockReason(card)}";
		}
		else
		{
			stateText = locked
				? $"已选中，等待目标：{targetText}"
				: "当前可打出";
		}

		if (locked)
			stateText += " · 右键或 Esc 取消";

		_cardPreviewTitle.Text = card.DisplayName;
		_cardPreviewMeta.Text = $"{cardTypeText} · {rangeText} · {targetText} · 费用 {card.EffectiveCost}";
		_cardPreviewState.Text = stateText;
		_cardPreviewDesc.Text = card.ActiveDescription;
		SetCardPreviewAccent(accent, canPlay || locked);
	}

	private void SetCardPreviewAccent(Color accent, bool active)
	{
		_cardPreviewStyle.BorderColor = active ? accent * 0.85f : accent;
		_cardPreviewStyle.ShadowColor = active
			? new Color(accent.R, accent.G, accent.B, 0.18f)
			: new Color(0f, 0f, 0f, 0.28f);
		_cardPreviewStyle.ShadowSize = active ? 14 : 10;
	}

	private void UpdateFormationHint()
	{
		if (_player == null || _combat == null)
			return;

		var row = _combat.Formation.GetPosition(_player.Id);
		bool moved = _combat.Formation.HasMovedThisTurn(_player.Id);
		bool rooted = _player.Powers.HasPower(RogueCardGame.Core.Combat.Powers.CommonPowerIds.Rooted);

		if (row == FormationRow.Front)
		{
			_formationHintLabel.Text = "前排承受单体攻击；获得护甲时额外 +3。";
		}
		else
		{
			_formationHintLabel.Text = "后排禁用近战；本回合第一张远程牌伤害 +20%。"
				+ "\n提示：前排敌人被推到后排会失去攻击能力。";
		}

		_formationStateLabel.Text = rooted
			? "当前状态：已被锁定，不能换位。"
			: moved
				? "当前状态：本回合已经换位。"
				: $"当前状态：换位需要 1 点能量，你现在有 {_player.CurrentEnergy} 点。";
	}

	private void ShowSelectionHint(string text)
	{
		_selectionHintLabel.Text = text;
		_selectionHintPanel.Visible = true;
		_selectionHintPanel.Modulate = Colors.White;
	}

	private void HideSelectionHint()
	{
		_selectionHintPanel.Visible = false;
	}

	private void ShowToast(string text, Color? color = null)
	{
		_toastTween?.Kill();
		_toastLabel.Text = text;
		_toastLabel.AddThemeColorOverride("font_color", color ?? new Color(0.84f, 0.9f, 0.95f));
		_toastPanel.Visible = true;
		_toastPanel.Modulate = new Color(1, 1, 1, 0);
		_toastPanel.Position = new Vector2(0, 0);

		_toastTween = CreateTween();
		_toastTween.TweenProperty(_toastPanel, "modulate:a", 1f, 0.12f);
		_toastTween.Parallel().TweenProperty(_toastPanel, "position:y", -8f, 0.12f)
			.From(8f)
			.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		_toastTween.TweenInterval(1.1f);
		_toastTween.TweenProperty(_toastPanel, "modulate:a", 0f, 0.2f);
		_toastTween.TweenCallback(Callable.From(() =>
		{
			_toastPanel.Visible = false;
			_toastPanel.Position = Vector2.Zero;
		}));
	}

	private void CancelCardSelection(string message)
	{
		_selectedCard = null;
		HideSelectionHint();
		RefreshHand();
		HighlightValidTargets();
		RefreshCardPreview();
		ShowToast(message, new Color(0.75f, 0.8f, 0.86f));
	}

	private string GetCardPlayBlockReason(Card card)
	{
		if (_player == null || _combat == null)
			return "当前无法打出";

		if (_player.CurrentEnergy < card.EffectiveCost)
			return $"需要 {card.EffectiveCost} 点能量，你只有 {_player.CurrentEnergy} 点";

		if (_combat.Formation.GetPosition(_player.Id) == FormationRow.Back && card.Data.Range == CardRange.Melee)
			return "后排不能使用近战牌";

		return "当前阶段不可打出";
	}

	private static string GetCardTypeText(CardType type) => type switch
	{
		CardType.Attack => "攻击牌",
		CardType.Skill => "技能牌",
		CardType.Power => "能力牌",
		_ => type.ToString()
	};

	private static string GetRangeText(CardRange range) => range switch
	{
		CardRange.Melee => "近战",
		CardRange.Ranged => "远程",
		CardRange.None => "通用",
		_ => range.ToString()
	};

	private static string GetTargetTypeText(TargetType targetType) => targetType switch
	{
		TargetType.SingleEnemy => "单体敌人",
		TargetType.AllEnemies => "全体敌人",
		TargetType.FrontRowEnemies => "前排敌人",
		TargetType.BackRowEnemies => "后排敌人",
		TargetType.Self => "自身",
		TargetType.SingleAlly => "单体友军",
		TargetType.AllAllies => "全体友军",
		TargetType.All => "全场",
		TargetType.None => "无目标",
		_ => targetType.ToString()
	};

	private static string GetIntentScopeText(TargetScope scope) => scope switch
	{
		TargetScope.SingleFront => "前排仇恨最高",
		TargetScope.SingleBack => "直接打后排",
		TargetScope.SingleAny => "仇恨最高目标",
		TargetScope.AllFront => "命中前排全体",
		TargetScope.AllBack => "命中后排全体",
		TargetScope.All => "命中我方全体",
		TargetScope.Self => "作用于自身",
		TargetScope.AllEnemies => "作用于敌方全体",
		_ => scope.ToString()
	};

	private static string GetIntentKeywordText(List<string> keywords)
	{
		if (keywords.Count == 0)
			return string.Empty;

		var labels = keywords.Select(keyword => keyword.ToLowerInvariant() switch
		{
			"lock" => "附加锁定",
			"breakcover" => "溅射后排",
			_ => keyword
		});

		return string.Join("、", labels);
	}

	private static string BuildIntentDetailText(EnemyIntent intent)
	{
		var parts = new List<string>();
		if (!string.IsNullOrWhiteSpace(intent.Description))
			parts.Add(intent.Description!);

		parts.Add(GetIntentScopeText(intent.Scope));

		var keywordText = GetIntentKeywordText(intent.Keywords);
		if (!string.IsNullOrEmpty(keywordText))
			parts.Add(keywordText);

		return string.Join(" · ", parts);
	}

	// ======================================================================
	//  CARD COLORS / HELPERS
	// ======================================================================
	private static Color GetCardTypeColor(Card card) => card.Data.Type switch
	{
		CardType.Attack => new Color(0.85f, 0.18f, 0.18f),
		CardType.Skill => new Color(0.18f, 0.45f, 0.85f),
		CardType.Power => new Color(0.8f, 0.55f, 0.1f),
		_ => new Color(0.35f, 0.35f, 0.4f)
	};

	private Control CreateCardGhost(Card card, Vector2 pos, Vector2 sz)
	{
		var col = GetCardTypeColor(card);
		var ghost = new PanelContainer();
		ghost.Position = pos;
		ghost.CustomMinimumSize = sz;
		ghost.Size = sz;
		ghost.PivotOffset = sz / 2;
		ghost.MouseFilter = MouseFilterEnum.Ignore;
		var style = new StyleBoxFlat
		{
			BgColor = col * 0.4f,
			BorderColor = col,
			BorderWidthBottom = 2, BorderWidthTop = 2,
			BorderWidthLeft = 2, BorderWidthRight = 2,
			CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
			CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8,
			ShadowColor = col * 0.5f, ShadowSize = 15
		};
		ghost.AddThemeStyleboxOverride("panel", style);
		var lbl = new Label
		{
			Text = card.DisplayName,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center
		};
		lbl.AddThemeFontSizeOverride("font_size", 14);
		lbl.AddThemeColorOverride("font_color", Colors.White);
		ghost.AddChild(lbl);
		return ghost;
	}

	// ======================================================================
	//  VISUAL FX
	// ======================================================================
	private void SpawnImpactFlash(Vector2 pos, Color color)
	{
		var flash = new ColorRect();
		flash.Size = new Vector2(70, 70);
		flash.PivotOffset = new Vector2(35, 35);
		flash.Position = pos - new Vector2(35, 35);
		flash.Color = new Color(color.R, color.G, color.B, 0.6f);
		flash.MouseFilter = MouseFilterEnum.Ignore;
		_damageNumbers.AddChild(flash);
		var tw = CreateTween();
		tw.TweenProperty(flash, "scale", new Vector2(2.5f, 2.5f), 0.18f)
			.SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
		tw.Parallel().TweenProperty(flash, "modulate:a", 0f, 0.22f);
		tw.TweenCallback(Callable.From(() => flash.QueueFree()));
	}

	private void SpawnParticleBurst(Vector2 pos, Color color, int count)
	{
		for (int i = 0; i < count; i++)
		{
			var p = new ColorRect();
			p.Size = new Vector2(4, 4);
			p.PivotOffset = new Vector2(2, 2);
			p.Position = pos;
			p.Color = i % 2 == 0 ? color : Colors.White;
			p.MouseFilter = MouseFilterEnum.Ignore;
			_damageNumbers.AddChild(p);

			float angle = (float)GD.RandRange(0, Mathf.Tau);
			float dist = (float)GD.RandRange(50, 170);
			var target = pos + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;

			var tw = CreateTween();
			tw.SetParallel(true);
			tw.TweenProperty(p, "position", target, 0.3f)
				.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
			tw.TweenProperty(p, "modulate:a", 0f, 0.3f).SetDelay(0.08f);
			tw.SetParallel(false);
			tw.TweenCallback(Callable.From(() => p.QueueFree()));
		}
	}

	private void SpawnPlayerDamageFlash(int damage)
	{
		// Screen shake on heavy hits
		if (damage >= 10)
			ScreenShake(8f, 5);
		else if (damage >= 5)
			ScreenShake(4f, 3);

		var flash = new ColorRect();
		flash.SetAnchorsPreset(LayoutPreset.FullRect);
		flash.Color = new Color(0.6f, 0f, 0f, 0.18f);
		flash.MouseFilter = MouseFilterEnum.Ignore;
		AddChild(flash);
		var tw = CreateTween();
		tw.TweenProperty(flash, "color:a", 0f, 0.35f);
		tw.TweenCallback(Callable.From(() => flash.QueueFree()));

		var lbl = new Label { Text = $"-{damage}" };
		lbl.AddThemeFontSizeOverride("font_size", 34);
		lbl.AddThemeColorOverride("font_color", new Color(1f, 0.12f, 0.12f));
		lbl.Position = new Vector2(Size.X * 0.4f, Size.Y * 0.49f);
		_damageNumbers.AddChild(lbl);
		var tw2 = CreateTween();
		tw2.TweenProperty(lbl, "position:y", lbl.Position.Y - 55, 0.7f)
			.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		tw2.Parallel().TweenProperty(lbl, "scale", new Vector2(1.3f, 1.3f), 0.07f);
		tw2.Parallel().TweenProperty(lbl, "scale", Vector2.One, 0.12f).SetDelay(0.07f);
		tw2.TweenProperty(lbl, "modulate:a", 0f, 0.35f);
		tw2.TweenCallback(Callable.From(() => lbl.QueueFree()));
	}

	private async void ShowTurnBanner(string text, Color color, Action? onDone = null)
	{
		var banner = new PanelContainer();
		banner.SetAnchorsPreset(LayoutPreset.FullRect);
		banner.AnchorLeft = 0f; banner.AnchorRight = 1f;
		banner.AnchorTop = 0.42f; banner.AnchorBottom = 0.58f;
		banner.MouseFilter = MouseFilterEnum.Ignore;
		var style = new StyleBoxFlat
		{
			BgColor = new Color(color.R * 0.08f, color.G * 0.08f, color.B * 0.08f, 0.93f),
			BorderColor = color * 0.4f,
			BorderWidthTop = 1, BorderWidthBottom = 1
		};
		banner.AddThemeStyleboxOverride("panel", style);
		var lbl = new Label
		{
			Text = text,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center
		};
		lbl.AddThemeFontSizeOverride("font_size", 38);
		lbl.AddThemeColorOverride("font_color", color);
		banner.AddChild(lbl);

		banner.Modulate = new Color(1, 1, 1, 0);
		banner.PivotOffset = new Vector2(Size.X / 2, Size.Y * 0.08f);
		banner.Scale = new Vector2(1f, 0.3f);
		AddChild(banner);

		var tw = CreateTween();
		tw.TweenProperty(banner, "modulate:a", 1f, 0.1f);
		tw.Parallel().TweenProperty(banner, "scale:y", 1f, 0.15f)
			.SetTrans(Tween.TransitionType.Back);
		tw.TweenInterval(0.5f);
		tw.TweenProperty(banner, "modulate:a", 0f, 0.12f);
		tw.TweenCallback(Callable.From(() =>
		{
			banner.QueueFree();
			onDone?.Invoke();
		}));
	}

	// ======================================================================
	//  COMBAT INIT
	// ======================================================================
	private void StartCombat()
	{
		var run = GameManager.Instance.CurrentRun;
		if (run == null) { GD.PrintErr("[CombatScene] No active run!"); return; }

		_player = run.Player;
		var enemies = run.CreateEnemiesForCurrentNode();
		_combat = run.CreateCombat(enemies);
		if (_combat == null)
		{
			GD.PrintErr("[CombatScene] Failed to create combat");
			SceneManager.Instance.ChangeScene(SceneManager.Scenes.Map);
			return;
		}

		_combat.OnCardPlayed += (_, card) => GD.Print($"[Combat] Played: {card.DisplayName}");
		_combat.OnCombatEnded += OnCombatEnded;
		_combat.OnEnemyAction += (e, i) => GD.Print($"[Combat] {e.Name}: {i.Type} {i.Value}");
		_combat.OnEnemySummoned += AddEnemyPanel;
		_combat.OnScryTriggered += ShowScryChoice;
		_combat.EnemyFactory = run.CreateEnemyById;

		if (_combat.PlayerDecks.TryGetValue(_player.Id, out var deck))
			_deck = deck;

		_turnNumber = 1;
		_combatResult = null;
		_combat.StartCombat();

		// Set player character sprite / combat idle loop.
		SetPlayerPortrait(_player.Class);

		var currentNode = run.CurrentMap?.CurrentNode;
		bool isBoss = currentNode?.Type == RoomType.Boss;
		AudioManager.Instance?.PlayBgm(isBoss ? AudioManager.BgmPaths.Boss : AudioManager.BgmPaths.Combat);

		BuildEnemyPanels();
		RefreshAll();
		ShowTurnBanner("\u2694 \u6218 \u6597 \u5f00 \u59cb \u2694", new Color(0.9f, 0.7f, 0.1f));
	}

	// ======================================================================
	//  ENEMY PANELS
	// ======================================================================
	private void BuildEnemyPanels()
	{
		ClearEnemyArea();
		BuildEnemyLaneBoard();
		_enemyPanels.Clear();
		if (_combat == null) return;
		foreach (var enemy in _combat.Enemies)
		{
			var panel = CreateEnemyPanel(enemy);
			_enemyPanels.Add(panel);
			MovePanelToCurrentLane(panel);
		}
	}

	private void UpdatePlayerPortraitFx(float phase)
	{
		if (_player == null || _playerPortraitGlow == null || _playerPortraitScan == null)
			return;

		var accent = CyberCardFactory.GetClassAccent(_player.Class);
		float pulse = 0.09f + 0.05f * (0.5f + 0.5f * MathF.Sin(phase * 1.6f));
		_playerPortraitGlow.Color = new Color(accent.R, accent.G, accent.B, pulse);

		float scanT = 0.5f + 0.5f * MathF.Sin(phase * 0.85f);
		float top = Mathf.Lerp(0.095f, 0.49f, scanT);
		_playerPortraitScan.AnchorTop = top;
		_playerPortraitScan.AnchorBottom = top + 0.006f;
		_playerPortraitScan.Color = new Color(
			Mathf.Lerp(accent.R, 1f, 0.35f),
			Mathf.Lerp(accent.G, 1f, 0.35f),
			Mathf.Lerp(accent.B, 1f, 0.35f),
			0.08f + pulse * 0.5f);
	}

	private void SetPlayerPortrait(CardClass cls)
	{
		ReleasePlayerPortraitFrames();

		LoadPlayerPortraitFrames(cls, "combat_idle", _playerPortraitIdleFrames);
		if (_playerPortraitIdleFrames.Count > 0)
		{
			_playerPortraitIdleFrame = 0;
			_playerSprite.Texture = _playerPortraitIdleFrames[0];
			return;
		}

		_playerSprite.Texture = LoadTex(GetCharacterTexturePath(cls));
	}

	private void ReleasePlayerPortraitFrames()
	{
		if (_playerSprite != null)
			_playerSprite.Texture = null;

		_playerPortraitIdleFrames.Clear();
		_playerPortraitActionFrames.Clear();
		_playerPortraitIdleTime = 0f;
		_playerPortraitIdleFrame = -1;
		_playerPortraitActionTime = 0f;
		_playerPortraitActionFrame = -1;
		_playerPortraitActionPlaying = false;
	}

	private void LoadPlayerPortraitFrames(CardClass cls, string animation, List<Texture2D> target)
	{
		target.Clear();
		string dir = GetCharacterAnimationDir(cls, animation);
		if (dir.Length == 0)
			return;

		for (int i = 0; i < 64; i++)
		{
			string framePath = $"{dir}/frame_{i:00}.png";
			if (!ResourceFileExists(framePath))
				break;

			var frame = LoadTex(framePath);
			if (frame != null)
				target.Add(frame);
		}
	}

	private void PlayPlayerPortraitAction(string animation)
	{
		if (_player == null || _playerSprite == null || string.IsNullOrWhiteSpace(animation))
			return;

		LoadPlayerPortraitFrames(_player.Class, animation, _playerPortraitActionFrames);
		if (_playerPortraitActionFrames.Count == 0)
			return;

		_playerPortraitActionTime = 0f;
		_playerPortraitActionFrame = 0;
		_playerPortraitActionPlaying = true;
		_playerSprite.Texture = _playerPortraitActionFrames[0];
	}

	private bool UpdatePlayerPortraitAction(double delta)
	{
		if (!_playerPortraitActionPlaying || _playerPortraitActionFrames.Count == 0)
			return false;

		_playerPortraitActionTime += (float)delta;
		int frame = (int)(_playerPortraitActionTime * PlayerPortraitActionFps);
		if (frame >= _playerPortraitActionFrames.Count)
		{
			_playerPortraitActionFrames.Clear();
			_playerPortraitActionFrame = -1;
			_playerPortraitActionPlaying = false;
			UpdatePlayerPortraitIdle(0);
			return false;
		}

		if (frame != _playerPortraitActionFrame)
		{
			_playerPortraitActionFrame = frame;
			_playerSprite.Texture = _playerPortraitActionFrames[frame];
		}

		return true;
	}

	private void UpdatePlayerPortraitIdle(double delta)
	{
		if (_playerPortraitIdleFrames.Count <= 1)
			return;

		_playerPortraitIdleTime += (float)delta;
		int frame = (int)(_playerPortraitIdleTime * PlayerPortraitIdleFps) % _playerPortraitIdleFrames.Count;
		if (frame == _playerPortraitIdleFrame)
			return;

		_playerPortraitIdleFrame = frame;
		_playerSprite.Texture = _playerPortraitIdleFrames[frame];
	}

	private void AddEnemyPanel(Enemy enemy)
	{
		var panel = CreateEnemyPanel(enemy);
		_enemyPanels.Add(panel);
		MovePanelToCurrentLane(panel);
	}

	private EnemyPanel CreateEnemyPanel(Enemy enemy)
	{
		var panel = new EnemyPanel(enemy);
		panel.ClickArea.GuiInput += (ev) =>
		{
			if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
				OnEnemyClicked(enemy);
		};
		return panel;
	}

	private void ClearEnemyArea()
	{
		foreach (var child in _enemyArea.GetChildren())
		{
			_enemyArea.RemoveChild(child);
			child.QueueFree();
		}
	}

	private void BuildEnemyLaneBoard()
	{
		_enemyArea.Alignment = BoxContainer.AlignmentMode.Center;

		var board = new VBoxContainer
		{
			Name = "EnemyLaneBoard",
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		board.AddThemeConstantOverride("separation", 6);

		_enemyBackLane = CreateEnemyLane(board, "EnemyBackLaneBand", "EnemyBackLane", "\u540e\u6392", new Color(0.22f, 0.5f, 0.9f));

		var divider = new ColorRect
		{
			Name = "EnemyLaneDivider",
			Color = new Color(0.7f, 0.9f, 1f, 0.24f),
			CustomMinimumSize = new Vector2(0, 2),
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		board.AddChild(divider);

		_enemyFrontLane = CreateEnemyLane(board, "EnemyFrontLaneBand", "EnemyFrontLane", "\u524d\u6392", new Color(1f, 0.55f, 0.22f));
		_enemyArea.AddChild(board);
	}

	private static HBoxContainer CreateEnemyLane(
		VBoxContainer board,
		string bandName,
		string laneName,
		string labelText,
		Color accent)
	{
		var band = new PanelContainer
		{
			Name = bandName,
			CustomMinimumSize = new Vector2(0, 138),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		band.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.025f, 0.04f, 0.065f, 0.72f),
			BorderColor = new Color(accent.R, accent.G, accent.B, 0.55f),
			BorderWidthLeft = 2,
			BorderWidthRight = 1,
			BorderWidthTop = 1,
			BorderWidthBottom = 1,
			CornerRadiusBottomLeft = 6,
			CornerRadiusBottomRight = 6,
			CornerRadiusTopLeft = 6,
			CornerRadiusTopRight = 6,
			ContentMarginLeft = 8,
			ContentMarginRight = 8,
			ContentMarginTop = 5,
			ContentMarginBottom = 5
		});

		var row = new HBoxContainer
		{
			Name = $"{laneName}Row",
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		row.AddThemeConstantOverride("separation", 8);

		var label = new Label
		{
			Name = $"{laneName}Label",
			Text = labelText,
			CustomMinimumSize = new Vector2(34, 0),
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		label.AddThemeFontSizeOverride("font_size", 12);
		label.AddThemeColorOverride("font_color", accent);
		row.AddChild(label);

		var lane = new HBoxContainer
		{
			Name = laneName,
			Alignment = BoxContainer.AlignmentMode.Center,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		lane.AddThemeConstantOverride("separation", 12);
		row.AddChild(lane);

		band.AddChild(row);
		board.AddChild(band);
		return lane;
	}

	private HBoxContainer GetLaneForEnemy(Enemy enemy)
	{
		var row = _combat?.Formation.GetPosition(enemy.Id) ?? enemy.Data.PreferredRow;
		return row == FormationRow.Front ? _enemyFrontLane : _enemyBackLane;
	}

	private void MovePanelToCurrentLane(EnemyPanel panel)
	{
		var lane = GetLaneForEnemy(panel.Enemy);
		if (panel.Root.GetParent() == lane)
			return;

		panel.Root.GetParent()?.RemoveChild(panel.Root);
		lane.AddChild(panel.Root);
	}


	// ======================================================================
	//  CARD FAN LAYOUT
	// ======================================================================
	private void RefreshAll()
	{
		RefreshHand();
		RefreshPlayerInfo();
		RefreshEnemies();
		RefreshDeckInfo();
		RefreshCardPreview();
		_turnLabel.Text = $"TURN {_turnNumber}";
	}

	private void RefreshHand()
	{
		foreach (var child in _handArea.GetChildren()) child.QueueFree();
		_cardButtons.Clear();
		ClearHoveredCardState();
		if (_deck == null || _player == null || _combat == null) return;

		var cards = _deck.Hand.ToList();
		int count = cards.Count;
		if (count == 0) return;

		AudioManager.Instance?.PlaySfx(AudioManager.SfxPaths.CardDraw);

		float areaWidth = _handArea.Size.X;
		float areaHeight = _handArea.Size.Y;
		float fanAngle = GetFanAngleForHand(count);
		float fanLift = GetFanLiftForHand(count);

		// Small and medium hands get more breathing room; dense hands compress gracefully.
		float cardSpacing = GetCardSpacingForHand(count, areaWidth);
		float totalWidth = (count - 1) * cardSpacing + CardWidth;
		float startX = (areaWidth - totalWidth) / 2f;

		for (int i = 0; i < count; i++)
		{
			var card = cards[i];
			int idx = i;

			// Fan angle: cards at edges rotate outward
			float centerOffset = i - (count - 1) / 2f;
			float angle = centerOffset * fanAngle;
			float lift = -MathF.Abs(centerOffset) * (fanLift / MathF.Max(count / 2f, 1));

			float x = startX + i * cardSpacing;
			float y = areaHeight - CardHeight + lift;

			var cardUI = CreateCardUI(card, idx, out var cardVisual);
			cardUI.Position = new Vector2(x - HoverSidePadding, y - HoverLift - HoverTopPadding);
			cardUI.ZIndex = i;
			cardVisual.Rotation = Mathf.DegToRad(angle);

			_handArea.AddChild(cardUI);
			_cardButtons[card] = cardVisual;
		}

		if (DebugHandRenderDump)
			CallDeferred(nameof(DumpHandRenderState));
	}

	private Control CreateCardUI(Card card, int handIndex, out Control visualCard)
	{
		bool canPlay = _combat!.CanPlay(_player!, card);
		bool selected = _selectedCard == card;
		var baseVisualPosition = GetCardVisualBasePosition();
		var hoveredVisualPosition = GetCardVisualHoverPosition();

		var hitbox = new Control();
		hitbox.CustomMinimumSize = new Vector2(CardHitboxWidth, CardHitboxHeight);
		hitbox.Size = new Vector2(CardHitboxWidth, CardHitboxHeight);

		var cardVisualShell = CyberCardFactory.CreateGameplayCard(
			card,
			new Vector2(CardWidth, CardHeight),
			compact: true,
			dimmed: false,
			footer: card.IsPositionReactive ? "前后排效果不同" : null,
			showDescription: true,
			selected: selected,
			minimalCompactStyle: false,
			opaqueCompactStyle: false,
			neutralCompactStyle: true,
			debugLayerMarkers: DebugRenderLayers);
		var outer = cardVisualShell.Root;
		var style = cardVisualShell.Style;
		var tc = cardVisualShell.Accent;
		outer.Position = baseVisualPosition;
		outer.PivotOffset = new Vector2(CardWidth / 2, CardHeight);
		if (!canPlay)
		{
			style.BorderColor = new Color(style.BorderColor.R, style.BorderColor.G, style.BorderColor.B, 0.4f);
			style.ShadowColor = new Color(0f, 0f, 0f, 0.08f);
			style.ShadowSize = Math.Max(style.ShadowSize - 2, 2);
		}
		SetMouseFilterRecursive(outer, MouseFilterEnum.Ignore);
		hitbox.AddChild(outer);

		// === HOVER ===
		hitbox.MouseEntered += () => UpdateCardPreview(card, canPlay, _selectedCard == card);
		hitbox.MouseExited += () => RefreshCardPreview();

		if (canPlay)
		{
			hitbox.MouseDefaultCursorShape = CursorShape.PointingHand;
			hitbox.MouseEntered += () =>
			{
				if (_inputLocked) return;

				if (_hoveredCardVisual != null && _hoveredCardVisual != outer && _hoveredCardIndex >= 0)
					ResetHoveredCardVisual(clearState: false);

				_hoveredCardIndex = handIndex;
				_hoveredCardHitbox = hitbox;
				_hoveredCardVisual = outer;
				_hoveredCardStyle = style;
				_hoveredCardBaseBorderColor = style.BorderColor;
				_hoveredCardBaseShadowColor = style.ShadowColor;
				var tw = outer.CreateTween();
				tw.TweenProperty(outer, "position", hoveredVisualPosition, 0.1f)
					.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
				tw.Parallel().TweenProperty(outer, "scale", new Vector2(HoverScale, HoverScale), 0.1f)
					.SetTrans(Tween.TransitionType.Cubic);
				tw.Parallel().TweenProperty(outer, "rotation", 0f, 0.08f);
				hitbox.ZIndex = 100;
				style.ShadowSize = 12;
				style.ShadowColor = tc * 0.35f;
				style.BorderColor = Colors.White;
			};
			hitbox.MouseExited += () =>
			{
				if (_hoveredCardVisual == outer)
					ResetHoveredCardVisual();
			};
		}

		hitbox.GuiInput += (ev) =>
		{
			if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
				OnCardClicked(card);
		};

		visualCard = outer;
		return hitbox;
	}

	private void ResetHoveredCardVisual(bool clearState = true)
	{
		if (_hoveredCardVisual == null || _hoveredCardStyle == null || _hoveredCardIndex < 0)
		{
			if (clearState)
				ClearHoveredCardState();
			return;
		}

		int count = _deck?.Hand.Count ?? 0;
		float centerOffset = _hoveredCardIndex - (count - 1) / 2f;
		float angle = centerOffset * GetFanAngleForHand(count);

		var tw = _hoveredCardVisual.CreateTween();
		tw.TweenProperty(_hoveredCardVisual, "position", GetCardVisualBasePosition(), 0.08f);
		tw.Parallel().TweenProperty(_hoveredCardVisual, "scale", Vector2.One, 0.08f);
		tw.Parallel().TweenProperty(_hoveredCardVisual, "rotation", Mathf.DegToRad(angle), 0.06f);

		if (_hoveredCardHitbox != null)
			_hoveredCardHitbox.ZIndex = _hoveredCardIndex;

		_hoveredCardStyle.ShadowSize = 3;
		_hoveredCardStyle.ShadowColor = _hoveredCardBaseShadowColor;
		_hoveredCardStyle.BorderColor = _hoveredCardBaseBorderColor;

		if (clearState)
			ClearHoveredCardState();
	}

	private void ClearHoveredCardState()
	{
		_hoveredCardIndex = -1;
		_hoveredCardHitbox = null;
		_hoveredCardVisual = null;
		_hoveredCardStyle = null;
		_hoveredCardBaseBorderColor = Colors.Transparent;
		_hoveredCardBaseShadowColor = Colors.Transparent;
	}

	private static Vector2 GetCardVisualBasePosition() => new(HoverSidePadding, HoverLift + HoverTopPadding);

	private static Vector2 GetCardVisualHoverPosition() => new(HoverSidePadding, HoverTopPadding);

	private void DumpHandRenderState()
	{
		if (!DebugHandRenderDump || _deck == null)
			return;

		_handRenderDumpSequence++;
		var cards = _deck.Hand.ToList();
		GD.Print($"[HandRenderDump] ===== begin #{_handRenderDumpSequence} cards={cards.Count} =====");

		// 1) Walk ancestor chain from HandArea up to root
		var ancestorMod = Colors.White;
		Node? cur = _handArea;
		while (cur != null)
		{
			if (cur is CanvasItem ci)
			{
				var hasMat = ci.Material != null;
				GD.Print($"[HandRenderDump] ANCESTOR {ci.Name}<{ci.GetType().Name}> mod={FormatColor(ci.Modulate)} self={FormatColor(ci.SelfModulate)} hasMaterial={hasMat}");
				ancestorMod = MultiplyColor(ancestorMod, MultiplyColor(ci.Modulate, ci.SelfModulate));
			}
			cur = cur.GetParent();
		}
		GD.Print($"[HandRenderDump] ACCUMULATED_ANCESTOR_MOD={FormatColor(ancestorMod)}");

		// 2) List all direct children of CombatScene (this) to find overlays above HandArea
		int handIdx = _handArea.GetIndex();
		GD.Print($"[HandRenderDump] HandArea index={handIdx}, scene children={GetChildCount()}");
		for (int c = 0; c < GetChildCount(); c++)
		{
			var child = GetChild(c);
			string line = $"[HandRenderDump] SCENE_CHILD[{c}] {SanitizeForDump(child.Name)}<{child.GetType().Name}>";
			if (child is CanvasItem ci2)
			{
				line += $" z={ci2.ZIndex} visible={ci2.Visible}";
				if (ci2.Material != null) line += " HAS_MATERIAL";
			}
			if (child is Control ctrl)
				line += $" anchors=({ctrl.AnchorLeft:F2},{ctrl.AnchorTop:F2},{ctrl.AnchorRight:F2},{ctrl.AnchorBottom:F2})";
			if (c == handIdx) line += " <<< HAND";
			GD.Print(line);
		}

		// 3) Dump first card only (reduce log spam)
		if (cards.Count > 0 && _handArea.GetChildCount() > 0 && _handArea.GetChild(0) is Control hitbox)
		{
			var card = cards[0];
			GD.Print($"[HandRenderDump] CARD[0] {card.Data.Id}#{card.InstanceId} / {card.DisplayName}");
			DumpCanvasItemTree(hitbox, 0, ancestorMod);
		}

		GD.Print($"[HandRenderDump] ===== end #{_handRenderDumpSequence} =====");
	}

	private void DumpCanvasItemTree(CanvasItem item, int depth, Color inheritedModulate)
	{
		Color localModulate = item.Modulate;
		Color selfModulate = item.SelfModulate;
		Color branchModulate = MultiplyColor(inheritedModulate, localModulate);
		Color finalModulate = MultiplyColor(branchModulate, selfModulate);
		string indent = new(' ', depth * 2);
		string renderSource = DescribeCanvasItemRender(item, finalModulate);

		GD.Print(
			$"[HandRenderDump] {indent}{item.Name}<{item.GetType().Name}> " +
			$"visible={item.Visible} z={item.ZIndex} " +
			$"mod={FormatColor(localModulate)} self={FormatColor(selfModulate)} final={FormatColor(finalModulate)}" +
			(renderSource.Length > 0 ? $" {renderSource}" : string.Empty));

		foreach (var child in item.GetChildren())
		{
			if (child is CanvasItem childCanvasItem)
				DumpCanvasItemTree(childCanvasItem, depth + 1, branchModulate);
		}
	}

	private static string DescribeCanvasItemRender(CanvasItem item, Color finalModulate)
	{
		var parts = new List<string>();

		switch (item)
		{
			case ColorRect colorRect:
				parts.Add($"color={FormatColor(colorRect.Color)}");
				parts.Add($"rendered={FormatColor(MultiplyColor(colorRect.Color, finalModulate))}");
				break;

			case PanelContainer panelContainer when panelContainer.GetThemeStylebox("panel") is StyleBoxFlat panelStyle:
				parts.Add($"bg={FormatColor(panelStyle.BgColor)}");
				parts.Add($"bgRendered={FormatColor(MultiplyColor(panelStyle.BgColor, finalModulate))}");
				parts.Add($"border={FormatColor(panelStyle.BorderColor)}");
				parts.Add($"borderRendered={FormatColor(MultiplyColor(panelStyle.BorderColor, finalModulate))}");
				break;

			case Label label:
				Color fontColor = label.GetThemeColor("font_color");
				parts.Add($"font={FormatColor(fontColor)}");
				parts.Add($"fontRendered={FormatColor(MultiplyColor(fontColor, finalModulate))}");
				parts.Add($"text=\"{SanitizeForDump(label.Text)}\"");
				break;

			case TextureRect textureRect:
				parts.Add($"texture={(textureRect.Texture?.ResourcePath ?? "<generated>")}");
				break;
		}

		return string.Join(" | ", parts);
	}

	private static string SanitizeForDump(string text)
	{
		return text
			.Replace("\r", string.Empty)
			.Replace("\n", "\\n")
			.Replace("\"", "'");
	}

	private static Color MultiplyColor(Color left, Color right)
	{
		return new Color(
			left.R * right.R,
			left.G * right.G,
			left.B * right.B,
			left.A * right.A);
	}

	private static string FormatColor(Color color)
	{
		return $"({color.R:0.###},{color.G:0.###},{color.B:0.###},{color.A:0.###})";
	}

	private static float GetCardSpacingForHand(int count, float areaWidth)
	{
		if (count <= 1)
			return 0f;

		float targetRatio = count switch
		{
			<= 4 => 0.92f,
			<= 6 => 0.84f,
			<= 8 => 0.78f,
			_ => MinDenseHandSpacingRatio
		};

		float targetSpacing = CardWidth * targetRatio;
		float availableSpacing = (areaWidth - CardWidth) / MathF.Max(count - 1, 1);
		return MathF.Min(targetSpacing, availableSpacing);
	}

	private static float GetFanAngleForHand(int count)
	{
		float density = Mathf.Clamp((count - 1) / 8f, 0f, 1f);
		return Mathf.Lerp(BaseFanAngle, DenseHandFanAngle, density);
	}

	private static float GetFanLiftForHand(int count)
	{
		float density = Mathf.Clamp((count - 1) / 8f, 0f, 1f);
		return Mathf.Lerp(BaseFanLift, DenseHandFanLift, density);
	}

	private static void SetMouseFilterRecursive(Control control, MouseFilterEnum mouseFilter)
	{
		control.MouseFilter = mouseFilter;
		foreach (var child in control.GetChildren().OfType<Control>())
			SetMouseFilterRecursive(child, mouseFilter);
	}

	// ======================================================================
	//  CARD PLAY - ANIMATED
	// ======================================================================
	private void OnCardClicked(Card card)
	{
		if (_inputLocked || _combat == null || _player == null) return;
		if (_selectedCard == card)
		{
			CancelCardSelection("已取消选牌");
			return;
		}

		if (!_combat.CanPlay(_player, card))
		{
			UpdateCardPreview(card, false);
			ShowToast(GetCardPlayBlockReason(card), new Color(1f, 0.55f, 0.35f));
			return;
		}

		AudioManager.Instance?.PlaySfx(AudioManager.SfxPaths.CardSelect);

		if (card.Data.EffectiveTargetType is TargetType.Self or TargetType.None or TargetType.AllEnemies or
			TargetType.FrontRowEnemies or TargetType.BackRowEnemies)
		{
			HideSelectionHint();
			PlayCardAnimated(card, null);
		}
		else
		{
			_selectedCard = card;
			UpdateCardPreview(card, true, true);
			ShowSelectionHint($"选择目标：{GetTargetTypeText(card.Data.EffectiveTargetType)} · 右键或 Esc 取消");
			HighlightValidTargets();
			RefreshHand();
		}
	}

	private void OnEnemyClicked(Enemy enemy)
	{
		if (_inputLocked || _selectedCard == null || _combat == null || _player == null) return;
		PlayCardAnimated(_selectedCard, enemy);
	}

	private string? GetPlayerPortraitActionForCard(Card card, int playerBlockBefore)
	{
		if (_player == null)
			return null;

		var row = _combat?.Formation.GetPosition(_player.Id) ?? _player.PreferredRow;
		var effects = FlattenEffects(card.GetEffectsForRow(row)).ToList();

		if (card.Data.Type == CardType.Attack)
		{
			if (effects.Any(IsClassResourceAnimationEffect))
				return GetCharacterSpecialAnimationName(_player.Class);

			return "attack";
		}

		if (_player.Block > playerBlockBefore || effects.Any(IsBlockAnimationEffect))
			return "gain_armor";

		if (card.Data.Type == CardType.Power || effects.Any(IsClassResourceAnimationEffect))
			return GetCharacterSpecialAnimationName(_player.Class);

		if (effects.Any(CardEffectFactory.EffectRequiresEnemyTarget))
			return "attack";

		return null;
	}

	private static IEnumerable<CardEffectData> FlattenEffects(IEnumerable<CardEffectData> effects)
	{
		foreach (var effect in effects)
		{
			yield return effect;
			if (effect.FrontEffect != null)
				yield return effect.FrontEffect;
			if (effect.BackEffect != null)
				yield return effect.BackEffect;
		}
	}

	private static bool IsBlockAnimationEffect(CardEffectData effect)
	{
		string type = NormalizeEffectType(effect);
		return type is "block"
			or "armor"
			or "conditionalblock"
			or "blockperovercharge"
			or "blockperresonance"
			or "resonanceblock";
	}

	private static bool IsClassResourceAnimationEffect(CardEffectData effect)
	{
		string type = NormalizeEffectType(effect);
		return type is "overchargeconsume"
			or "consumeovercharge"
			or "blockperovercharge"
			or "resonancedamage"
			or "resonanceblock"
			or "consumeresonance"
			or "resonanceamplify"
			or "delayedresonance"
			or "clearresonance"
			or "halveresonance"
			or "blockperresonance"
			or "protocolstack"
			or "consumeprotocol"
			or "protocoldamage"
			or "doubleprotocol"
			or "hack"
			or "hackonaction"
			or "hackpercentofdamage"
			or "instanthack"
			or "selfdamage"
			or "selfdamagepercent"
			or "damagefromhplost"
			or "lifesteal"
			or "spreadpoison"
			or "healperpoisondamage"
			or "damageperpoisonedenemy";
	}

	private static string NormalizeEffectType(CardEffectData effect) =>
		(effect.Type ?? string.Empty).Trim().ToLowerInvariant();

	private async void PlayCardAnimated(Card card, Enemy? target)
	{
		if (_combat == null || _player == null) return;
		_inputLocked = true;

		var hpBefore = _enemyPanels.ToDictionary(ep => ep.Enemy, ep => ep.Enemy.CurrentHp);
		int playerBlockBefore = _player.Block;

		Vector2 cardPos = new Vector2(Size.X / 2, Size.Y * 0.78f);
		Vector2 cardSz = new Vector2(CardWidth, CardHeight);
		if (_cardButtons.TryGetValue(card, out var ui))
		{
			cardPos = ui.GlobalPosition;
			cardSz = ui.Size;
		}

		Vector2 impactPos;
		if (target != null)
		{
			var ep = _enemyPanels.FirstOrDefault(p => p.Enemy == target);
			impactPos = ep != null ? ep.SpriteRect.GlobalPosition + ep.SpriteRect.Size / 2
				: new Vector2(Size.X / 2, Size.Y * 0.25f);
		}
		else
			impactPos = new Vector2(Size.X / 2, Size.Y * 0.25f);

		bool success = target != null
			? _combat.TryPlayCard(_player, card, target)
			: _combat.TryPlayCard(_player, card);

		if (!success)
		{
			_inputLocked = false;
			if (target != null)
			{
				ShowToast("该目标当前不可选", new Color(1f, 0.55f, 0.35f));
				HighlightValidTargets();
				RefreshCardPreview();
				return;
			}

			_selectedCard = null;
			HideSelectionHint();
			RefreshAll();
			return;
		}

		HideSelectionHint();

		string? portraitAction = GetPlayerPortraitActionForCard(card, playerBlockBefore);
		if (portraitAction != null)
			PlayPlayerPortraitAction(portraitAction);

		AudioManager.Instance?.PlaySfx(AudioManager.SfxPaths.CardPlay);

		var ghost = CreateCardGhost(card, cardPos, cardSz);
		_damageNumbers.AddChild(ghost);

		var tw = CreateTween();
		tw.SetParallel(true);
		tw.TweenProperty(ghost, "position", impactPos - cardSz * 0.15f, 0.18f)
			.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
		tw.TweenProperty(ghost, "scale", new Vector2(0.25f, 0.25f), 0.18f);
		tw.TweenProperty(ghost, "rotation", Mathf.DegToRad(10f), 0.12f);
		tw.TweenProperty(ghost, "modulate:a", 0.5f, 0.16f);
		await ToSignal(tw, Tween.SignalName.Finished);
		if (!IsInsideTree()) return;

		ghost.QueueFree();
		AudioManager.Instance?.PlaySfx(AudioManager.SfxPaths.AttackHit);
		SpawnImpactFlash(impactPos, GetCardTypeColor(card));
		SpawnParticleBurst(impactPos, GetCardTypeColor(card), 12);

		foreach (var ep in _enemyPanels)
		{
			if (hpBefore.TryGetValue(ep.Enemy, out int before))
			{
				int delta = before - ep.Enemy.CurrentHp;
				if (delta > 0) ep.AnimateHit(delta, _damageNumbers);
			}
		}

		if (card.Data.Type == CardType.Skill && _player.Block > 0)
		{
			AudioManager.Instance?.PlaySfx(AudioManager.SfxPaths.BlockGain);
			var bf = new ColorRect();
			bf.SetAnchorsPreset(LayoutPreset.FullRect);
			bf.Color = new Color(0.15f, 0.4f, 0.9f, 0.1f);
			bf.MouseFilter = MouseFilterEnum.Ignore;
			AddChild(bf);
			var tw3 = CreateTween();
			tw3.TweenProperty(bf, "color:a", 0f, 0.25f);
			tw3.TweenCallback(Callable.From(() => bf.QueueFree()));
		}

		await ToSignal(GetTree().CreateTimer(0.25), SceneTreeTimer.SignalName.Timeout);
		if (!IsInsideTree()) return;

		_selectedCard = null;
		RefreshAll();

		if (_combatResult != null)
		{
			await ToSignal(GetTree().CreateTimer(0.4), SceneTreeTimer.SignalName.Timeout);
			if (!IsInsideTree()) return;
			ProcessCombatResult();
			return;
		}
		_inputLocked = false;
	}

	// ======================================================================
	//  END TURN - ANIMATED ENEMY PHASE
	// ======================================================================
	private void OnEndTurnPressed()
	{
		if (_inputLocked || _combat == null || !_combat.IsActive) return;
		AudioManager.Instance?.PlaySfx(AudioManager.SfxPaths.TurnEnd);
		_selectedCard = null;
		AnimateEnemyTurn();
	}

	private async void AnimateEnemyTurn()
	{
		if (_combat == null || _player == null) return;
		_inputLocked = true;

		int playerHpBefore = _player.CurrentHp;

		var capturedActions = new List<(Enemy enemy, EnemyIntent intent, int playerHpAfter, int playerBlockAfter)>();
		void CaptureHandler(Enemy e, EnemyIntent i) =>
			capturedActions.Add((e, i, _player.CurrentHp, _player.Block));
		_combat.OnEnemyAction += CaptureHandler;
		_combat.EndPlayerTurn();
		_turnNumber++;
		_combat.OnEnemyAction -= CaptureHandler;

		ShowTurnBanner("\u654c \u65b9 \u56de \u5408", new Color(0.9f, 0.2f, 0.15f));
		await ToSignal(GetTree().CreateTimer(0.8), SceneTreeTimer.SignalName.Timeout);
		if (!IsInsideTree()) return;

		int trackHp = playerHpBefore;
		foreach (var (enemy, intent, hpAfter, blockAfter) in capturedActions)
		{
			if (!IsInsideTree()) return;
			var ep = _enemyPanels.FirstOrDefault(p => p.Enemy == enemy);
			if (ep == null) continue;

			AudioManager.Instance?.PlaySfx(AudioManager.SfxPaths.EnemyAttack);
			ep.FlashIntent();

			// Enemy lunge forward on attack
			if (intent.Type is EnemyIntentType.Attack or EnemyIntentType.AttackDefend or EnemyIntentType.Special)
			{
				ep.AnimateLunge();
			}

			await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);
			if (!IsInsideTree()) return;

			int hpLost = trackHp - hpAfter;
			if (hpLost > 0)
			{
				SpawnPlayerDamageFlash(hpLost);
				var orbTw = CreateTween();
				orbTw.TweenProperty(_energyOrb, "scale", new Vector2(0.9f, 1.1f), 0.04f);
				orbTw.TweenProperty(_energyOrb, "scale", Vector2.One, 0.06f);
			}

			trackHp = hpAfter;
			RefreshPlayerInfo();
			RefreshEnemies();

			await ToSignal(GetTree().CreateTimer(0.35), SceneTreeTimer.SignalName.Timeout);
			if (!IsInsideTree()) return;
		}

		if (_combatResult != null)
		{
			await ToSignal(GetTree().CreateTimer(0.4), SceneTreeTimer.SignalName.Timeout);
			if (!IsInsideTree()) return;
			ProcessCombatResult();
			return;
		}

		ShowTurnBanner("\u4f60 \u7684 \u56de \u5408", new Color(0f, 0.85f, 0.85f));
		await ToSignal(GetTree().CreateTimer(0.6), SceneTreeTimer.SignalName.Timeout);
		if (!IsInsideTree()) return;

		RefreshAll();
		_inputLocked = false;
	}

	// ======================================================================
	//  TARGETING
	// ======================================================================
	private void HighlightValidTargets()
	{
		foreach (var panel in _enemyPanels)
		{
			bool valid = _selectedCard != null && _combat != null &&
				_combat.Targeting.GetValidTargets(
					_player!, _selectedCard.Data.EffectiveTargetType, _selectedCard.Data.Range,
					_combat.Enemies.Where(e => e.IsAlive),
					_combat.Players.Where(p => p.IsAlive))
				.Contains(panel.Enemy);
			panel.SetHighlight(valid);
		}
	}

	private void OnSwitchRowPressed()
	{
		if (_inputLocked || _combat == null || _player == null) return;
		if (_combat.TrySwitchRow(_player))
		{
			AudioManager.Instance?.PlaySfx(AudioManager.SfxPaths.ButtonClick);
			RefreshAll();
		}
		else
		{
			ShowToast(GetSwitchRowBlockReason(), new Color(1f, 0.55f, 0.35f));
		}
	}

	private string GetSwitchRowBlockReason()
	{
		if (_player == null || _combat == null)
			return "当前无法换位";

		if (_player.Powers.HasPower(RogueCardGame.Core.Combat.Powers.CommonPowerIds.Rooted))
			return "已被锁定，不能换位";

		if (_combat.Formation.HasMovedThisTurn(_player.Id))
			return "本回合已经换位";

		if (_player.CurrentEnergy < 1)
			return "换位需要 1 点能量";

		return "当前无法换位";
	}

	// ======================================================================
	//  PILE VIEWER OVERLAY (STS2-style: click pile panel to preview cards)
	// ======================================================================
	private void BuildPileViewerOverlay()
	{
		// Full-screen semi-transparent backdrop — clicking outside the modal closes the viewer
		_pileViewerBackdrop = new ColorRect
		{
			Color = new Color(0f, 0f, 0f, 0.72f),
			Visible = false,
			ZIndex = 200,
		};
		_pileViewerBackdrop.SetAnchorsPreset(LayoutPreset.FullRect);
		_pileViewerBackdrop.GuiInput += ev =>
		{
			if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
				HidePileViewer();
		};
		AddChild(_pileViewerBackdrop);

		// Centered modal panel (MouseFilter.Stop prevents clicks propagating to backdrop)
		var modal = new PanelContainer { MouseFilter = MouseFilterEnum.Stop };
		modal.SetAnchorsPreset(LayoutPreset.FullRect);
		modal.AnchorLeft = 0.08f; modal.AnchorRight = 0.92f;
		modal.AnchorTop = 0.05f; modal.AnchorBottom = 0.95f;
		modal.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.04f, 0.05f, 0.09f, 0.98f),
			BorderColor = new Color(0.22f, 0.38f, 0.58f, 0.78f),
			BorderWidthBottom = 2, BorderWidthTop = 2,
			BorderWidthLeft = 2, BorderWidthRight = 2,
			CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10,
			CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10,
			ContentMarginLeft = 14, ContentMarginRight = 14,
			ContentMarginTop = 12, ContentMarginBottom = 12,
		});

		var root = new VBoxContainer();
		root.AddThemeConstantOverride("separation", 8);

		// Header: title + count note + close button
		var header = new HBoxContainer();
		header.AddThemeConstantOverride("separation", 10);

		_pileViewerTitle = new Label
		{
			SizeFlagsHorizontal = SizeFlags.Expand,
			VerticalAlignment = VerticalAlignment.Center,
		};
		_pileViewerTitle.AddThemeFontSizeOverride("font_size", 18);
		_pileViewerTitle.AddThemeColorOverride("font_color", new Color(0.84f, 0.94f, 1f));
		header.AddChild(_pileViewerTitle);

		_pileViewerCountNote = new Label { VerticalAlignment = VerticalAlignment.Center };
		_pileViewerCountNote.AddThemeFontSizeOverride("font_size", 13);
		_pileViewerCountNote.AddThemeColorOverride("font_color", new Color(0.5f, 0.62f, 0.72f));
		header.AddChild(_pileViewerCountNote);

		var closeBtn = new Button { Text = "✕  关闭", CustomMinimumSize = new Vector2(80, 0) };
		closeBtn.AddThemeFontSizeOverride("font_size", 13);
		closeBtn.Pressed += HidePileViewer;
		header.AddChild(closeBtn);
		root.AddChild(header);

		// Tab buttons: draw / discard / exhaust / full deck
		var tabBar = new HBoxContainer();
		tabBar.AddThemeConstantOverride("separation", 4);
		string[] tabNames = ["抽牌堆", "弃牌堆", "消耗堆", "全牌库"];
		_pileViewerTabBtns = new Button[4];
		for (int i = 0; i < 4; i++)
		{
			int idx = i;
			var btn = new Button
			{
				Text = tabNames[i],
				ToggleMode = true,
				CustomMinimumSize = new Vector2(90, 0),
			};
			btn.AddThemeFontSizeOverride("font_size", 13);
			btn.Pressed += () =>
			{
				_currentPileViewMode = (PileViewMode)idx;
				PopulatePileViewer(_currentPileViewMode);
				UpdatePileViewerTabs();
			};
			tabBar.AddChild(btn);
			_pileViewerTabBtns[i] = btn;
		}
		root.AddChild(tabBar);

		// Scrollable list of card entries
		// HorizontalScrollMode must be Disabled to prevent AutowrapMode labels from collapsing to zero height
		var scroll = new ScrollContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
		};
		_pileViewerCardList = new HFlowContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_pileViewerCardList.AddThemeConstantOverride("h_separation", 8);
		_pileViewerCardList.AddThemeConstantOverride("v_separation", 8);
		scroll.AddChild(_pileViewerCardList);
		root.AddChild(scroll);

		modal.AddChild(root);
		_pileViewerBackdrop.AddChild(modal);
	}

	private void ShowPileViewer(PileViewMode mode)
	{
		if (_pileViewerBackdrop == null || _deck == null) return;
		_currentPileViewMode = mode;
		PopulatePileViewer(mode);
		UpdatePileViewerTabs();
		_pileViewerBackdrop.Visible = true;
	}

	private void HidePileViewer()
	{
		if (_pileViewerBackdrop != null)
			_pileViewerBackdrop.Visible = false;
	}

	private void PopulatePileViewer(PileViewMode mode)
	{
		if (_pileViewerCardList == null || _pileViewerTitle == null || _pileViewerCountNote == null || _deck == null)
			return;

		foreach (Node child in _pileViewerCardList.GetChildren().ToArray())
		{
			_pileViewerCardList.RemoveChild(child);
			child.Free();
		}

		IEnumerable<Card> cards;
		string title;
		string note;

		switch (mode)
		{
			case PileViewMode.DrawPile:
				cards = _deck.DrawPile;
				title = "抽牌堆";
				note = "（顺序已随机化）";
				break;
			case PileViewMode.DiscardPile:
				cards = _deck.DiscardPile;
				title = "弃牌堆";
				note = "";
				break;
			case PileViewMode.ExhaustPile:
				cards = _deck.ExhaustPile;
				title = "消耗堆";
				note = "（已从牌库永久移除）";
				break;
			default: // FullDeck
				cards = _deck.DrawPile
					.Concat(_deck.Hand)
					.Concat(_deck.DiscardPile)
					.Concat(_deck.ExhaustPile)
					.OrderBy(c => c.Data.Type)
					.ThenBy(c => c.Data.Cost)
					.ThenBy(c => c.DisplayName);
				title = "全牌库";
				note = $"（抽 {_deck.DrawPile.Count} · 手 {_deck.Hand.Count} · 弃 {_deck.DiscardPile.Count} · 消耗 {_deck.ExhaustPile.Count}）";
				break;
		}

		var cardList = cards.ToList();
		_pileViewerTitle.Text = title;
		_pileViewerCountNote.Text = $"{cardList.Count} 张  {note}";

		if (cardList.Count == 0)
		{
			var empty = new Label { Text = "（空）", HorizontalAlignment = HorizontalAlignment.Center };
			empty.AddThemeColorOverride("font_color", new Color(0.45f, 0.5f, 0.55f));
			_pileViewerCardList.AddChild(empty);
			return;
		}

		foreach (var card in cardList)
		{
			var visual = CyberCardFactory.CreateGameplayCard(
				card, new Vector2(140, 195), compact: true, showDescription: true);
			visual.Root.MouseFilter = MouseFilterEnum.Ignore;
			_pileViewerCardList.AddChild(visual.Root);
		}
	}

	private void UpdatePileViewerTabs()
	{
		if (_pileViewerTabBtns == null || _deck == null) return;
		int totalDeck = _deck.DrawPile.Count + _deck.Hand.Count + _deck.DiscardPile.Count + _deck.ExhaustPile.Count;
		int[] counts = [_deck.DrawPile.Count, _deck.DiscardPile.Count, _deck.ExhaustPile.Count, totalDeck];
		string[] baseNames = ["抽牌堆", "弃牌堆", "消耗堆", "全牌库"];
		for (int i = 0; i < _pileViewerTabBtns.Length; i++)
		{
			_pileViewerTabBtns[i].Text = $"{baseNames[i]}({counts[i]})";
			_pileViewerTabBtns[i].ButtonPressed = _currentPileViewMode == (PileViewMode)i;
		}
	}

	private void ShowScryChoice(PlayerCharacter player, DeckManager deck, List<Card> peeked, int keepCount)
	{
		if (_combat == null || peeked.Count == 0)
			return;

		HidePileViewer();
		_scryBackdrop?.QueueFree();

		var backdrop = new ColorRect
		{
			Color = new Color(0f, 0f, 0f, 0.76f),
			ZIndex = 260,
			MouseFilter = MouseFilterEnum.Stop,
		};
		backdrop.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(backdrop);
		_scryBackdrop = backdrop;

		var modal = new PanelContainer { MouseFilter = MouseFilterEnum.Stop };
		modal.SetAnchorsPreset(LayoutPreset.FullRect);
		modal.AnchorLeft = 0.22f; modal.AnchorRight = 0.78f;
		modal.AnchorTop = 0.16f; modal.AnchorBottom = 0.84f;
		modal.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.035f, 0.045f, 0.075f, 0.98f),
			BorderColor = new Color(0.4f, 0.72f, 1f, 0.72f),
			BorderWidthBottom = 2, BorderWidthTop = 2,
			BorderWidthLeft = 2, BorderWidthRight = 2,
			CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10,
			CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10,
			ContentMarginLeft = 20, ContentMarginRight = 20,
			ContentMarginTop = 18, ContentMarginBottom = 18,
			ShadowColor = new Color(0.05f, 0.5f, 1f, 0.18f),
			ShadowSize = 18,
		});

		var root = new VBoxContainer();
		root.AddThemeConstantOverride("separation", 14);

		var title = new Label
		{
			Text = "预知模块",
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		title.AddThemeFontSizeOverride("font_size", 28);
		title.AddThemeColorOverride("font_color", new Color(0.72f, 0.9f, 1f));
		root.AddChild(title);

		var hint = new Label
		{
			Text = keepCount <= 1 ? "选择 1 张置于抽牌堆顶" : $"选择 {keepCount} 张置于抽牌堆顶",
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		hint.AddThemeFontSizeOverride("font_size", 15);
		hint.AddThemeColorOverride("font_color", new Color(0.55f, 0.66f, 0.78f));
		root.AddChild(hint);

		var cardsRow = new HBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		cardsRow.AddThemeConstantOverride("separation", 18);

		bool resolved = false;
		foreach (var card in peeked)
		{
			var visual = CyberCardFactory.CreateGameplayCard(
				card, new Vector2(200, 296), compact: false, footer: "置于牌堆顶", showDescription: true);
			visual.Root.MouseDefaultCursorShape = CursorShape.PointingHand;
			CyberCardFactory.AttachHover(visual, scale: 1.04f, hoverShadow: 18);
			visual.Root.GuiInput += ev =>
			{
				if (resolved || ev is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
					return;

				resolved = true;
				_combat.CompleteScry(player, deck, peeked, card);
				_scryBackdrop?.QueueFree();
				_scryBackdrop = null;
				RefreshDeckInfo();
				ShowToast($"已预知：{card.DisplayName}", new Color(0.58f, 0.82f, 1f));
			};
			cardsRow.AddChild(visual.Root);
		}

		root.AddChild(cardsRow);
		modal.AddChild(root);
		backdrop.AddChild(modal);
	}



	// ======================================================================
	//  REFRESH
	// ======================================================================
	private void RefreshPlayerInfo()
	{
		if (_player == null || _combat == null) return;

		_hpBarDynamic.MaxValue = _player.MaxHp;
		_hpBarDynamic.Value = _player.CurrentHp;

		float pct = (float)_player.CurrentHp / _player.MaxHp;
		Color hpCol = pct > 0.5f ? new Color(0.15f, 0.7f, 0.25f)
			: pct > 0.25f ? new Color(0.8f, 0.65f, 0.1f)
			: new Color(0.85f, 0.12f, 0.12f);

		_hpBarDynamic.AddThemeStyleboxOverride("fill", new StyleBoxFlat
		{
			BgColor = hpCol,
			CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3,
			CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3
		});
		_hpTextDynamic.Text = $"{_player.CurrentHp}/{_player.MaxHp}";

		_energyOrbLabel.Text = $"{_player.CurrentEnergy}/{_player.MaxEnergy}";
		bool hasE = _player.CurrentEnergy > 0;
		_energyOrbLabel.AddThemeColorOverride("font_color", hasE ? new Color(0f, 1f, 1f) : new Color(0.25f, 0.25f, 0.3f));
		var orbSt = (StyleBoxFlat)_energyOrb.GetThemeStylebox("panel").Duplicate();
		orbSt.BorderColor = hasE ? new Color(0f, 0.85f, 0.85f) : new Color(0.15f, 0.15f, 0.2f);
		orbSt.ShadowColor = hasE ? new Color(0f, 0.5f, 0.7f, 0.25f) : Colors.Transparent;
		_energyOrb.AddThemeStyleboxOverride("panel", orbSt);

		// Block display
		if (_player.Block > 0)
		{
			_playerBlockIcon.Visible = true;
			_playerBlockLabel.Text = _player.Block.ToString();
		}
		else
		{
			_playerBlockIcon.Visible = false;
			_playerBlockLabel.Text = "";
		}

		// Resource badge
		var resName = _player.EffectiveClassResourceName;
		_resourceBadge.Text = !string.IsNullOrEmpty(resName) ? $"{resName}: {_player.ClassResourceValue}" : "";

		// Player powers — skip the power that's already shown in the class resource badge
		var classResPowerId = _player.ClassResourcePowerId;
		var playerPowers = new List<string>();
		foreach (var power in _player.Powers.Powers)
		{
			if (power.PowerId == classResPowerId) continue; // already shown in resource badge
			string pIcon = power.IsDebuff ? "\u25bc" : "\u25b2";
			playerPowers.Add($"{pIcon}{power.Name}:{power.Amount}");
		}
		_playerPowerLabel.Text = string.Join("  ", playerPowers);

		var row = _combat.Formation.GetPosition(_player.Id);
		_rowLabel.Text = row == FormationRow.Front ? "\u2694 \u524d\u6392" : "\ud83c\udfaf \u540e\u6392";
		_rowLabel.AddThemeColorOverride("font_color", row == FormationRow.Front ? new Color(1f, 0.55f, 0.2f) : new Color(0.35f, 0.7f, 0.95f));
		_switchRowBtn.Text = _combat.Formation.HasMovedThisTurn(_player.Id) ? "\u5df2\u6362\u4f4d" : _player.CurrentEnergy >= 1 ? "\u5207\u6362\u9635\u578b" : "\u80fd\u91cf\u4e0d\u8db3";
		_switchRowBtn.Disabled = _player.CurrentEnergy < 1 || _player.Powers.HasPower(RogueCardGame.Core.Combat.Powers.CommonPowerIds.Rooted);
		UpdateFormationHint();

		_topBar?.Refresh();
	}

	private void RefreshEnemies()
	{
		if (_combat == null) return;
		foreach (var p in _enemyPanels)
		{
			MovePanelToCurrentLane(p);
			p.Update(_combat.Formation);
		}
	}

	private void RefreshDeckInfo()
	{
		if (_deck == null) return;

		if (_drawPileCountLabel != null)
			_drawPileCountLabel.Text = _deck.DrawPile.Count.ToString();
		if (_discardPileCountLabel != null)
			_discardPileCountLabel.Text = _deck.DiscardPile.Count.ToString();
		if (_exhaustPileCountLabel != null)
			_exhaustPileCountLabel.Text = _deck.ExhaustPile.Count.ToString();
	}

	// ======================================================================
	//  COMBAT EVENTS
	// ======================================================================
	private void OnCombatEnded(bool victory)
	{
		_combatResult = victory;
		GD.Print($"[Combat] Result: {(victory ? "Victory" : "Defeat")}");
	}

	private void ProcessCombatResult()
	{
		if (_combatResult == null) return;
		var run = GameManager.Instance.CurrentRun;
		if (run == null) return;

		if (_combatResult.Value)
		{
			var node = run.CurrentMap?.CurrentNode;
			bool isElite = node?.Type == RoomType.EliteCombat;
			bool isBoss = node?.Type == RoomType.Boss;
			run.OnCombatVictory(isElite, isBoss);
			if (isBoss && run.CurrentAct >= Core.BalanceConfig.Current.MapGeneration.ActsTotal)
			{
				GameManager.Instance.EndCurrentRun(true);
				SceneManager.Instance.ChangeScene(SceneManager.Scenes.Victory);
			}
			else
			{
				GameManager.Instance.SetCurrentRunScene("reward");
				SceneManager.Instance.ChangeScene(SceneManager.Scenes.Reward);
			}
		}
		else
		{
			GameManager.Instance.EndCurrentRun(false);
			SceneManager.Instance.ChangeScene(SceneManager.Scenes.GameOver);
		}
	}

	// ======================================================================
	//  ENEMY PANEL (STS2-style: sprite + HP bar + intent)
	// ======================================================================
	private class EnemyPanel
	{
		public Enemy Enemy { get; }
		public Control Root { get; }
		public TextureRect SpriteRect { get; }
		public Control ClickArea { get; }
		private readonly Label _nameLabel;
		private readonly ProgressBar _hpBar;
		private readonly Label _hpText;
		private readonly Label _blockText;
		private readonly TextureRect _intentIcon;
		private readonly Label _intentValueLabel;
		private readonly PanelContainer _intentBadge;
		private readonly Label _intentDetailLabel;
		private readonly Label _statusText;
		private readonly PanelContainer _highlightBorder;

		public EnemyPanel(Enemy enemy)
		{
			Enemy = enemy;
			bool isBoss = enemy.Data.IsBoss;
			bool isElite = enemy.Data.IsElite;
			Color accent = isBoss ? new Color(0.85f, 0.12f, 0.12f)
				: isElite ? new Color(0.95f, 0.5f, 0.08f) : new Color(0.4f, 0.55f, 0.75f);

			Root = new VBoxContainer();
			((VBoxContainer)Root).AddThemeConstantOverride("separation", 2);
			Root.CustomMinimumSize = new Vector2(isBoss ? 180 : 130, 0);

			// --- Intent badge (above sprite) ---
			_intentBadge = new PanelContainer();
			_intentBadge.CustomMinimumSize = new Vector2(0, 28);
			_intentBadge.PivotOffset = new Vector2(50, 14);
			_intentBadge.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
			var iStyle = new StyleBoxFlat
			{
				BgColor = new Color(0.05f, 0.05f, 0.08f, 0.9f),
				CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
				CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4,
				ContentMarginLeft = 4, ContentMarginRight = 4,
				ContentMarginTop = 1, ContentMarginBottom = 1
			};
			_intentBadge.AddThemeStyleboxOverride("panel", iStyle);

			var intentHbox = new HBoxContainer();
			intentHbox.AddThemeConstantOverride("separation", 3);

			_intentIcon = new TextureRect
			{
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				CustomMinimumSize = new Vector2(20, 20),
			};
			intentHbox.AddChild(_intentIcon);

			_intentValueLabel = new Label { Text = "" };
			_intentValueLabel.AddThemeFontSizeOverride("font_size", 14);
			intentHbox.AddChild(_intentValueLabel);

			_intentBadge.AddChild(intentHbox);
			Root.AddChild(_intentBadge);

			_intentDetailLabel = new Label
			{
				HorizontalAlignment = HorizontalAlignment.Center,
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
				CustomMinimumSize = new Vector2(0, isBoss ? 30 : 24)
			};
			_intentDetailLabel.AddThemeFontSizeOverride("font_size", 10);
			_intentDetailLabel.AddThemeColorOverride("font_color", new Color(0.62f, 0.7f, 0.78f));
			Root.AddChild(_intentDetailLabel);

			// --- Sprite container with highlight border ---
			var spriteContainer = new Control();
			spriteContainer.CustomMinimumSize = new Vector2(isBoss ? 170 : 120, isBoss ? 180 : 140);
			spriteContainer.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;

			// Highlight border (shown when targeted)
			_highlightBorder = new PanelContainer();
			_highlightBorder.SetAnchorsPreset(LayoutPreset.FullRect);
			_highlightBorder.Visible = false;
			var hlStyle = new StyleBoxFlat
			{
				BgColor = Colors.Transparent,
				BorderColor = new Color(0f, 1f, 1f, 0.7f),
				BorderWidthBottom = 2, BorderWidthTop = 2,
				BorderWidthLeft = 2, BorderWidthRight = 2,
				CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
				CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6,
				ShadowColor = new Color(0f, 0.8f, 0.8f, 0.2f),
				ShadowSize = 8,
			};
			_highlightBorder.AddThemeStyleboxOverride("panel", hlStyle);
			spriteContainer.AddChild(_highlightBorder);

			// Enemy sprite
			SpriteRect = new TextureRect
			{
				Texture = LoadTex(GetEnemyTexturePath(enemy)),
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				MouseFilter = MouseFilterEnum.Ignore,
			};
			SpriteRect.SetAnchorsPreset(LayoutPreset.FullRect);
			SpriteRect.PivotOffset = new Vector2(isBoss ? 85 : 60, isBoss ? 90 : 70);
			spriteContainer.AddChild(SpriteRect);

			// Click area (transparent, captures clicks)
			ClickArea = new Control();
			ClickArea.SetAnchorsPreset(LayoutPreset.FullRect);
			ClickArea.MouseDefaultCursorShape = CursorShape.PointingHand;
			spriteContainer.AddChild(ClickArea);

			Root.AddChild(spriteContainer);

			// --- Name ---
			_nameLabel = new Label
			{
				Text = enemy.Name,
				HorizontalAlignment = HorizontalAlignment.Center
			};
			_nameLabel.AddThemeFontSizeOverride("font_size", isBoss ? 14 : 12);
			_nameLabel.AddThemeColorOverride("font_color", accent);
			Root.AddChild(_nameLabel);

			// Boss/Elite tag
			if (isBoss || isElite)
			{
				var tag = new Label
				{
					Text = isBoss ? "\u25c6 BOSS" : "\u25c6 ELITE",
					HorizontalAlignment = HorizontalAlignment.Center
				};
				tag.AddThemeFontSizeOverride("font_size", 9);
				tag.AddThemeColorOverride("font_color", accent * 0.7f);
				Root.AddChild(tag);
			}

			// --- HP bar ---
			_hpBar = new ProgressBar
			{
				MaxValue = enemy.MaxHp,
				Value = enemy.CurrentHp,
				CustomMinimumSize = new Vector2(0, 12),
				ShowPercentage = false,
				SizeFlagsHorizontal = SizeFlags.ExpandFill
			};
			_hpBar.AddThemeStyleboxOverride("fill", new StyleBoxFlat
			{
				BgColor = new Color(0.7f, 0.1f, 0.1f),
				CornerRadiusBottomLeft = 2, CornerRadiusBottomRight = 2,
				CornerRadiusTopLeft = 2, CornerRadiusTopRight = 2
			});
			_hpBar.AddThemeStyleboxOverride("background", new StyleBoxFlat
			{
				BgColor = new Color(0.1f, 0.03f, 0.03f),
				CornerRadiusBottomLeft = 2, CornerRadiusBottomRight = 2,
				CornerRadiusTopLeft = 2, CornerRadiusTopRight = 2
			});
			Root.AddChild(_hpBar);

			// HP text
			_hpText = new Label
			{
				Text = $"{enemy.CurrentHp}/{enemy.MaxHp}",
				HorizontalAlignment = HorizontalAlignment.Center
			};
			_hpText.AddThemeFontSizeOverride("font_size", 10);
			_hpText.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.85f));
			Root.AddChild(_hpText);

			// Block text
			_blockText = new Label { Text = "", HorizontalAlignment = HorizontalAlignment.Center };
			_blockText.AddThemeFontSizeOverride("font_size", 11);
			_blockText.AddThemeColorOverride("font_color", new Color(0.35f, 0.65f, 1f));
			Root.AddChild(_blockText);

			// Status text
			_statusText = new Label { Text = "", HorizontalAlignment = HorizontalAlignment.Center };
			_statusText.AddThemeFontSizeOverride("font_size", 9);
			Root.AddChild(_statusText);
		}

		public void Update(FormationSystem formation)
		{
			_hpBar.Value = Enemy.CurrentHp;
			_hpText.Text = $"{Enemy.CurrentHp}/{Enemy.MaxHp}";
			_blockText.Text = Enemy.Block > 0 ? $"\u25c8 {Enemy.Block}" : "";

			float p = (float)Enemy.CurrentHp / Enemy.MaxHp;
			Color hc = p > 0.5f ? new Color(0.7f, 0.1f, 0.1f) : p > 0.25f ? new Color(0.8f, 0.45f, 0.08f) : new Color(0.45f, 0.06f, 0.06f);
			_hpBar.AddThemeStyleboxOverride("fill", new StyleBoxFlat
			{
				BgColor = hc,
				CornerRadiusBottomLeft = 2, CornerRadiusBottomRight = 2,
				CornerRadiusTopLeft = 2, CornerRadiusTopRight = 2
			});

			if (Enemy.CurrentIntent != null)
			{
				var it = Enemy.CurrentIntent;
				// Intent icon
				string intentTexPath = it.Type switch
				{
					EnemyIntentType.Attack => "res://resources/textures/ui/intent_attack.svg",
					EnemyIntentType.AttackDefend => "res://resources/textures/ui/intent_attack.svg",
					EnemyIntentType.Defend => "res://resources/textures/ui/intent_defend.svg",
					EnemyIntentType.Buff => "res://resources/textures/ui/intent_buff.svg",
					EnemyIntentType.Debuff => "res://resources/textures/ui/intent_debuff.svg",
					EnemyIntentType.Special => "res://resources/textures/ui/intent_special.svg",
					EnemyIntentType.Disabled => "res://resources/textures/ui/intent_defend.svg",
					_ => "res://resources/textures/ui/intent_attack.svg"
				};
				_intentIcon.Texture = LoadTex(intentTexPath);

				// Intent value
				_intentValueLabel.Text = it.Type switch
				{
					EnemyIntentType.Attack => $"{it.Value}" + (it.HitCount > 1 ? $"x{it.HitCount}" : ""),
					EnemyIntentType.AttackDefend => $"{it.Value}",
					EnemyIntentType.Defend => $"{it.Value}",
					EnemyIntentType.Buff => $"+{it.Value}",
					EnemyIntentType.Debuff => $"-{it.Value}",
					EnemyIntentType.Heal => $"+{it.Value}",
					EnemyIntentType.Summon => $"x{Math.Max(1, it.Value)}",
					EnemyIntentType.Special => "!",
					EnemyIntentType.Disabled => "\u2718",
					_ => ""
				};
				Color ic = it.Type switch
				{
					EnemyIntentType.Attack => new Color(1f, 0.3f, 0.3f),
					EnemyIntentType.AttackDefend => new Color(1f, 0.5f, 0.2f),
					EnemyIntentType.Defend => new Color(0.35f, 0.65f, 1f),
					EnemyIntentType.Buff => new Color(0.85f, 0.65f, 0.08f),
					EnemyIntentType.Debuff => new Color(0.65f, 0.18f, 0.75f),
					EnemyIntentType.Heal => new Color(0.28f, 0.82f, 0.45f),
					EnemyIntentType.Summon => new Color(0.8f, 0.82f, 0.92f),
					EnemyIntentType.Special => new Color(1f, 0.2f, 0.55f),
					EnemyIntentType.Disabled => new Color(0.45f, 0.45f, 0.5f),
					_ => new Color(0.6f, 0.6f, 0.6f)
				};
				_intentValueLabel.AddThemeColorOverride("font_color", ic);
				_intentDetailLabel.Text = BuildIntentDetailText(it);
				ClickArea.TooltipText = _intentDetailLabel.Text;
				_intentBadge.Visible = true;
			}
			else
			{
				_intentBadge.Visible = false;
				_intentDetailLabel.Text = "";
				ClickArea.TooltipText = "";
			}

			var row = formation.GetPosition(Enemy.Id);
			bool isFront = row == FormationRow.Front;
			_nameLabel.Text = Enemy.Name + (isFront ? " [\u524d\u6392]" : " [\u540e\u6392]");

			// Position boundary visual: front = warm, back = cool
			Color posColor = isFront
				? new Color(1f, 0.65f, 0.35f)
				: new Color(0.35f, 0.65f, 1f);
			_nameLabel.AddThemeColorOverride("font_color", posColor);

			// Status/power icons
			var statParts = new List<string>();
			foreach (var power in Enemy.Powers.Powers)
			{
				string icon = power.PowerId switch
				{
					var id when id.Contains("Strength") => $"\u2191{power.Amount}",
					var id when id.Contains("Weak") => $"\u2193{power.Amount}",
					var id when id.Contains("Vulnerable") => $"\u25bc{power.Amount}",
					var id when id.Contains("Poison") => $"\u2620{power.Amount}",
					var id when id.Contains("Hacked") => $"\u2261{power.Amount}",
					var id when id.Contains("Stunned") => $"\u26a1{power.Amount}",
					_ => $"\u25c8{power.Amount}"
				};
				statParts.Add(icon);
			}
			_statusText.Text = string.Join(" ", statParts);
			_statusText.AddThemeColorOverride("font_color", new Color(0.8f, 0.6f, 0.2f));

			Root.Visible = Enemy.IsAlive;
		}

		public void SetHighlight(bool lit)
		{
			_highlightBorder.Visible = lit;
		}

		public void AnimateHit(int damage, Control overlay)
		{
			var tw = SpriteRect.CreateTween();
			tw.TweenProperty(SpriteRect, "scale", new Vector2(0.93f, 1.07f), 0.04f);
			tw.TweenProperty(SpriteRect, "scale", new Vector2(1.05f, 0.95f), 0.04f);
			tw.TweenProperty(SpriteRect, "scale", Vector2.One, 0.05f);

			// Red flash overlay on sprite
			var flash = new ColorRect();
			flash.SetAnchorsPreset(LayoutPreset.FullRect);
			flash.Color = new Color(1f, 0.08f, 0.08f, 0.35f);
			flash.MouseFilter = MouseFilterEnum.Ignore;
			SpriteRect.AddChild(flash);
			var tw2 = SpriteRect.CreateTween();
			tw2.TweenProperty(flash, "modulate:a", 0f, 0.25f);
			tw2.TweenCallback(Callable.From(() => flash.QueueFree()));

			_hpBar.Value = Enemy.CurrentHp + damage;
			var tw4 = _hpBar.CreateTween();
			tw4.TweenProperty(_hpBar, "value", (double)Enemy.CurrentHp, 0.35f)
				.SetTrans(Tween.TransitionType.Cubic);
			_hpText.Text = $"{Enemy.CurrentHp}/{Enemy.MaxHp}";

			var pos = SpriteRect.GlobalPosition + SpriteRect.Size / 2;
			var lbl = new Label { Text = $"-{damage}" };
			lbl.AddThemeFontSizeOverride("font_size", damage > 15 ? 30 : 22);
			lbl.AddThemeColorOverride("font_color", damage > 15 ? new Color(1f, 0.8f, 0.08f) : new Color(1f, 0.15f, 0.15f));
			lbl.Position = pos - new Vector2(16, 10);
			lbl.PivotOffset = new Vector2(16, 10);
			overlay.AddChild(lbl);

			var tw3 = overlay.CreateTween();
			tw3.TweenProperty(lbl, "position:y", lbl.Position.Y - 50, 0.6f)
				.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
			tw3.Parallel().TweenProperty(lbl, "scale", new Vector2(1.3f, 1.3f), 0.06f);
			tw3.Parallel().TweenProperty(lbl, "scale", Vector2.One, 0.12f).SetDelay(0.06f);
			tw3.TweenProperty(lbl, "modulate:a", 0f, 0.3f);
			tw3.TweenCallback(Callable.From(() => lbl.QueueFree()));
		}

		public void FlashIntent()
		{
			var tw = _intentBadge.CreateTween();
			tw.TweenProperty(_intentBadge, "scale", new Vector2(1.2f, 1.2f), 0.06f)
				.SetTrans(Tween.TransitionType.Back);
			tw.TweenProperty(_intentBadge, "scale", Vector2.One, 0.1f);
		}

		public void AnimateLunge()
		{
			float origY = SpriteRect.Position.Y;
			var tw = SpriteRect.CreateTween();
			tw.TweenProperty(SpriteRect, "position:y", origY + 20, 0.08f)
				.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
			tw.TweenProperty(SpriteRect, "position:y", origY - 4, 0.06f)
				.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
			tw.TweenProperty(SpriteRect, "position:y", origY, 0.1f);
		}
	}
}
