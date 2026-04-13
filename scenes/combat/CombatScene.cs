using Godot;
using System;
using System.Collections.Generic;
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
	private Control? _hoveredCardControl;
	private float _idleTime;

	// --- Card fan constants ---
	private const float CardWidth = 120f;
	private const float CardHeight = 168f;
	private const float FanAngle = 2.5f;
	private const float FanLift = 40f;
	private const float HoverLift = 80f;
	private const float HoverScale = 1.2f;
	private const float CardOverlap = 0.72f;

	// --- Texture cache ---
	private static readonly Dictionary<string, Texture2D> _textureCache = new();

	private static Texture2D LoadTex(string resPath)
	{
		if (_textureCache.TryGetValue(resPath, out var cached)) return cached;
		var tex = GD.Load<Texture2D>(resPath);
		if (tex != null) _textureCache[resPath] = tex;
		return tex!;
	}

	private static string GetCharacterTexturePath(CardClass cls) => cls switch
	{
		CardClass.Vanguard => "res://resources/textures/characters/vanguard.svg",
		CardClass.Psion => "res://resources/textures/characters/psion.svg",
		CardClass.Netrunner => "res://resources/textures/characters/netrunner.svg",
		CardClass.Symbiote => "res://resources/textures/characters/symbiote.svg",
		_ => "res://resources/textures/characters/vanguard.svg"
	};

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

		CyberFx.AddScanlines(this, 0.03f);
		CyberFx.AddVignette(this);

		_topBar = TopBarHUD.Attach(this);

		BuildPlayerCharacter();
		BuildEnergyOrb();
		BuildPileDisplays();
		StyleUI();
		StartCombat();
	}

	// ======================================================================
	//  PLAYER CHARACTER DISPLAY (STS2-style: sprite on battlefield left)
	// ======================================================================
	private void BuildPlayerCharacter()
	{
		// Player sprite - left side of battlefield
		_playerSprite = new TextureRect
		{
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		_playerSprite.SetAnchorsPreset(LayoutPreset.FullRect);
		_playerSprite.AnchorLeft = 0.06f; _playerSprite.AnchorRight = 0.22f;
		_playerSprite.AnchorTop = 0.08f; _playerSprite.AnchorBottom = 0.52f;
		AddChild(_playerSprite);

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
			float pp = _idleTime * 1.0f;
			float pb = 1f + 0.008f * MathF.Sin(pp);
			_playerSprite.Scale = new Vector2(pb, pb);
		}

		// Energy orb pulse
		if (_player != null && _player.CurrentEnergy > 0 && !_inputLocked)
		{
			float pulse = 1f + 0.02f * MathF.Sin(_idleTime * 2.5f);
			_energyOrb.Scale = new Vector2(pulse, pulse);
		}
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

		// Discard pile (bottom-right)
		var discardPile = CreatePilePanel(
			"res://resources/textures/ui/icon_discard_pile.svg",
			new Color(0.65f, 0.35f, 0.15f));
		discardPile.SetAnchorsPreset(LayoutPreset.FullRect);
		discardPile.AnchorLeft = 0.93f; discardPile.AnchorRight = 0.98f;
		discardPile.AnchorTop = 0.86f; discardPile.AnchorBottom = 0.96f;
		AddChild(discardPile);
		_discardPileCountLabel = discardPile.GetChild(0).GetChild<Label>(1);

		// Exhaust pile (small, above discard)
		var exhaustPile = CreatePilePanel(
			"res://resources/textures/ui/icon_exhaust_pile.svg",
			new Color(0.5f, 0.15f, 0.15f));
		exhaustPile.SetAnchorsPreset(LayoutPreset.FullRect);
		exhaustPile.AnchorLeft = 0.93f; exhaustPile.AnchorRight = 0.98f;
		exhaustPile.AnchorTop = 0.78f; exhaustPile.AnchorBottom = 0.85f;
		AddChild(exhaustPile);
		_exhaustPileCountLabel = exhaustPile.GetChild(0).GetChild<Label>(1);
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
		var enemies = CreateEnemiesForNode(run);
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

		if (_combat.PlayerDecks.TryGetValue(_player.Id, out var deck))
			_deck = deck;

		_turnNumber = 1;
		_combatResult = null;
		_combat.StartCombat();

		// Set player character sprite
		_playerSprite.Texture = LoadTex(GetCharacterTexturePath(_player.Class));

		var currentNode = run.CurrentMap?.CurrentNode;
		bool isBoss = currentNode?.Type == RoomType.Boss;
		AudioManager.Instance?.PlayBgm(isBoss ? AudioManager.BgmPaths.Boss : AudioManager.BgmPaths.Combat);

		BuildEnemyPanels();
		RefreshAll();
		ShowTurnBanner("\u2694 \u6218 \u6597 \u5f00 \u59cb \u2694", new Color(0.9f, 0.7f, 0.1f));
	}

	private List<Enemy> CreateEnemiesForNode(Core.Run.RunState run)
	{
		var node = run.CurrentMap?.CurrentNode;
		if (node == null) return [new Enemy(CreateFallbackEnemyData())];
		bool isElite = node.Type == RoomType.EliteCombat;
		bool isBoss = node.Type == RoomType.Boss;
		int act = run.CurrentAct;
		var enemies = new List<Enemy>();
		if (isBoss) enemies.Add(new Enemy(CreateBossData(act)));
		else if (isElite) enemies.Add(new Enemy(CreateEliteData(act)));
		else { int c = run.Random.Next(1, 4); for (int i = 0; i < c; i++) enemies.Add(new Enemy(CreateNormalEnemyData(act, run.Random))); }
		return enemies;
	}

	private static EnemyData CreateFallbackEnemyData() => new()
	{
		Id = "fallback", Name = "\u6d4b\u8bd5\u654c\u4eba", MaxHp = 30, PreferredRow = FormationRow.Front,
		IntentPatterns = [new EnemyIntentPattern { Type = EnemyIntentType.Attack, Value = 8 }, new EnemyIntentPattern { Type = EnemyIntentType.Defend, Value = 5 }]
	};

	private static EnemyData CreateNormalEnemyData(int act, SeededRandom rng)
	{
		int hp = 20 + act * 10 + rng.Next(0, 10); int dmg = 5 + act * 3;
		string[] names = ["\u8d5b\u535a\u66b4\u5f92", "\u6d41\u6d6a\u673a\u5668\u4eba", "\u75c5\u6bd2\u8820\u866b", "\u51c0\u5316\u65e0\u4eba\u673a", "\u9ed1\u5e02\u8d70\u79c1\u8005"];
		return new()
		{
			Id = $"normal_{act}_{rng.Next(1000)}", Name = names[rng.Next(names.Length)],
			MaxHp = hp, PreferredRow = rng.NextDouble() < 0.5 ? FormationRow.Front : FormationRow.Back,
			IntentPatterns = [
				new EnemyIntentPattern { Type = EnemyIntentType.Attack, Value = dmg, Weight = 0.5f },
				new EnemyIntentPattern { Type = EnemyIntentType.AttackDefend, Value = dmg - 2, Weight = 0.3f },
				new EnemyIntentPattern { Type = EnemyIntentType.Defend, Value = dmg, Weight = 0.2f }]
		};
	}

	private static EnemyData CreateEliteData(int act) => new()
	{
		Id = $"elite_{act}", Name = act switch { 1 => "\u94ec\u5408\u91d1\u6267\u6cd5\u8005", 2 => "\u8d5b\u535a\u6b66\u58eb", _ => "\u6697\u7f51\u5b88\u62a4\u8005" },
		MaxHp = 60 + act * 25, PreferredRow = FormationRow.Front, IsElite = true,
		IntentPatterns = [
			new EnemyIntentPattern { Type = EnemyIntentType.Attack, Value = 10 + act * 5, Weight = 0.4f },
			new EnemyIntentPattern { Type = EnemyIntentType.AttackDefend, Value = 8 + act * 3, Weight = 0.3f },
			new EnemyIntentPattern { Type = EnemyIntentType.Buff, Value = 3, Weight = 0.15f },
			new EnemyIntentPattern { Type = EnemyIntentType.Debuff, Value = 2, Weight = 0.15f }]
	};

	private static EnemyData CreateBossData(int act) => new()
	{
		Id = $"boss_{act}", Name = act switch { 1 => "\u5783\u573e\u573a\u9886\u4e3b", 2 => "\u516c\u53f8\u6267\u884c\u5b98", _ => "AI\u6838\u5fc3" },
		MaxHp = 120 + act * 50, PreferredRow = FormationRow.Front, IsBoss = true,
		IntentPatterns = [
			new EnemyIntentPattern { Type = EnemyIntentType.Attack, Value = 15 + act * 5, Weight = 0.35f },
			new EnemyIntentPattern { Type = EnemyIntentType.AttackDefend, Value = 12 + act * 3, Weight = 0.25f },
			new EnemyIntentPattern { Type = EnemyIntentType.Buff, Value = 4, Weight = 0.15f },
			new EnemyIntentPattern { Type = EnemyIntentType.Debuff, Value = 3, Weight = 0.15f },
			new EnemyIntentPattern { Type = EnemyIntentType.Special, Value = 20 + act * 8, Weight = 0.1f }],
		Phases = [new EnemyPhase { HpThreshold = 0.5f, EntryEffect = "\u8fdb\u5165\u72c2\u66b4\u72b6\u6001\uff01",
			IntentPatterns = [
				new EnemyIntentPattern { Type = EnemyIntentType.Attack, Value = 20 + act * 7, Weight = 0.5f },
				new EnemyIntentPattern { Type = EnemyIntentType.Special, Value = 25 + act * 10, Weight = 0.3f },
				new EnemyIntentPattern { Type = EnemyIntentType.Buff, Value = 5, Weight = 0.2f }] }]
	};

	// ======================================================================
	//  ENEMY PANELS
	// ======================================================================
	private void BuildEnemyPanels()
	{
		foreach (var child in _enemyArea.GetChildren()) child.QueueFree();
		_enemyPanels.Clear();
		if (_combat == null) return;
		foreach (var enemy in _combat.Enemies)
		{
			var panel = new EnemyPanel(enemy);
			_enemyArea.AddChild(panel.Root);
			_enemyPanels.Add(panel);
			panel.ClickArea.GuiInput += (ev) =>
			{
				if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
					OnEnemyClicked(enemy);
			};
		}
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
		_turnLabel.Text = $"TURN {_turnNumber}";
	}

	private void RefreshHand()
	{
		foreach (var child in _handArea.GetChildren()) child.QueueFree();
		_cardButtons.Clear();
		_hoveredCardIndex = -1;
		if (_deck == null || _player == null || _combat == null) return;

		var cards = _deck.Hand.ToList();
		int count = cards.Count;
		if (count == 0) return;

		AudioManager.Instance?.PlaySfx(AudioManager.SfxPaths.CardDraw);

		float areaWidth = _handArea.Size.X;
		float areaHeight = _handArea.Size.Y;

		// Calculate spacing: cards overlap more when many
		float cardSpacing = MathF.Min(CardWidth * CardOverlap, (areaWidth - CardWidth) / MathF.Max(count - 1, 1));
		float totalWidth = (count - 1) * cardSpacing + CardWidth;
		float startX = (areaWidth - totalWidth) / 2f;

		for (int i = 0; i < count; i++)
		{
			var card = cards[i];
			int idx = i;

			// Fan angle: cards at edges rotate outward
			float centerOffset = i - (count - 1) / 2f;
			float angle = centerOffset * FanAngle;
			float lift = -MathF.Abs(centerOffset) * (FanLift / MathF.Max(count / 2f, 1));

			float x = startX + i * cardSpacing;
			float y = areaHeight - CardHeight + lift;

			var cardUI = CreateCardUI(card, idx);
			cardUI.Position = new Vector2(x, y);
			cardUI.Rotation = Mathf.DegToRad(angle);
			cardUI.PivotOffset = new Vector2(CardWidth / 2, CardHeight); // bottom center pivot
			cardUI.ZIndex = i;

			_handArea.AddChild(cardUI);
			_cardButtons[card] = cardUI;
		}
	}

	private Control CreateCardUI(Card card, int handIndex)
	{
		bool canPlay = _combat!.CanPlay(_player!, card);
		bool selected = _selectedCard == card;
		var tc = GetCardTypeColor(card);

		var outer = new PanelContainer();
		outer.CustomMinimumSize = new Vector2(CardWidth, CardHeight);
		outer.Size = new Vector2(CardWidth, CardHeight);

		Color bg = canPlay ? new Color(0.04f, 0.04f, 0.07f, 0.97f) : new Color(0.03f, 0.03f, 0.05f, 0.75f);
		Color bd = selected ? Colors.White : (canPlay ? tc * 0.5f : tc * 0.15f);

		var style = new StyleBoxFlat
		{
			BgColor = bg, BorderColor = bd,
			BorderWidthBottom = selected ? 2 : 1, BorderWidthTop = selected ? 2 : 1,
			BorderWidthLeft = selected ? 2 : 1, BorderWidthRight = selected ? 2 : 1,
			CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
			CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6,
			ShadowColor = canPlay ? tc * 0.15f : Colors.Transparent,
			ShadowSize = canPlay ? 3 : 0
		};
		outer.AddThemeStyleboxOverride("panel", style);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 1);

		// --- Card art area (top portion with illustration) ---
		var artContainer = new PanelContainer();
		artContainer.CustomMinimumSize = new Vector2(0, 55);
		var artBg = new StyleBoxFlat
		{
			BgColor = canPlay ? tc * 0.12f : tc * 0.04f,
			CornerRadiusTopLeft = 5, CornerRadiusTopRight = 5,
			ContentMarginLeft = 4, ContentMarginRight = 4,
			ContentMarginTop = 2, ContentMarginBottom = 2
		};
		artContainer.AddThemeStyleboxOverride("panel", artBg);

		// Card art texture
		var artTexRect = new TextureRect
		{
			Texture = LoadTex(GetCardArtPath(card.Data.Type)),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			Modulate = canPlay ? Colors.White : new Color(1, 1, 1, 0.3f),
		};
		artContainer.AddChild(artTexRect);

		// Cost orb overlaid on art (top-left)
		var costOrb = new PanelContainer();
		costOrb.SetAnchorsPreset(LayoutPreset.TopLeft);
		costOrb.OffsetLeft = 3; costOrb.OffsetTop = 3;
		costOrb.OffsetRight = 27; costOrb.OffsetBottom = 25;
		var costStyle = new StyleBoxFlat
		{
			BgColor = canPlay ? new Color(0f, 0.12f, 0.18f, 0.92f) : new Color(0.06f, 0.06f, 0.08f, 0.7f),
			BorderColor = canPlay ? new Color(0f, 0.7f, 0.8f) : new Color(0.2f, 0.2f, 0.25f),
			BorderWidthBottom = 1, BorderWidthTop = 1, BorderWidthLeft = 1, BorderWidthRight = 1,
			CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10,
			CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10,
			ContentMarginLeft = 4, ContentMarginRight = 4,
		};
		costOrb.AddThemeStyleboxOverride("panel", costStyle);
		var costLbl = new Label
		{
			Text = card.EffectiveCost.ToString(),
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
		};
		costLbl.AddThemeFontSizeOverride("font_size", 16);
		costLbl.AddThemeColorOverride("font_color", canPlay ? new Color(0f, 1f, 1f) : new Color(0.3f, 0.3f, 0.35f));
		costOrb.AddChild(costLbl);
		artContainer.AddChild(costOrb);

		vbox.AddChild(artContainer);

		// --- Card name ---
		var nameLbl = new Label
		{
			Text = card.DisplayName,
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		nameLbl.AddThemeFontSizeOverride("font_size", 11);
		nameLbl.AddThemeColorOverride("font_color", canPlay ? Colors.White : new Color(0.35f, 0.35f, 0.4f));
		vbox.AddChild(nameLbl);

		// --- Divider ---
		vbox.AddChild(new ColorRect { Color = canPlay ? tc * 0.3f : tc * 0.08f, CustomMinimumSize = new Vector2(0, 1) });

		// --- Description ---
		var desc = new Label
		{
			Text = card.ActiveDescription,
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		desc.AddThemeFontSizeOverride("font_size", 9);
		desc.AddThemeColorOverride("font_color", canPlay ? new Color(0.65f, 0.65f, 0.7f) : new Color(0.25f, 0.25f, 0.3f));
		vbox.AddChild(desc);

		// --- Bottom accent ---
		vbox.AddChild(new ColorRect { Color = canPlay ? tc * 0.4f : tc * 0.1f, CustomMinimumSize = new Vector2(0, 2) });
		outer.AddChild(vbox);

		// === HOVER ===
		if (canPlay)
		{
			outer.MouseDefaultCursorShape = CursorShape.PointingHand;
			outer.MouseEntered += () =>
			{
				if (_inputLocked) return;

				if (_hoveredCardControl != null && _hoveredCardControl != outer && _hoveredCardIndex >= 0)
				{
					var prev = _hoveredCardControl;
					int prevIdx = _hoveredCardIndex;
					int prevCount = _deck?.Hand.Count ?? 0;
					float prevCenterOffset = prevIdx - (prevCount - 1) / 2f;
					float prevAngle = prevCenterOffset * FanAngle;
					float prevLiftVal = -MathF.Abs(prevCenterOffset) * (FanLift / MathF.Max(prevCount / 2f, 1));
					float prevY = _handArea.Size.Y - CardHeight + prevLiftVal;

					var ptw = prev.CreateTween();
					ptw.TweenProperty(prev, "position:y", prevY, 0.05f);
					ptw.Parallel().TweenProperty(prev, "scale", Vector2.One, 0.05f);
					ptw.Parallel().TweenProperty(prev, "rotation", Mathf.DegToRad(prevAngle), 0.04f);
					prev.ZIndex = prevIdx;
				}

				_hoveredCardIndex = handIndex;
				_hoveredCardControl = outer;
				var tw = outer.CreateTween();
				tw.TweenProperty(outer, "position:y", outer.Position.Y - HoverLift, 0.1f)
					.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
				tw.Parallel().TweenProperty(outer, "scale", new Vector2(HoverScale, HoverScale), 0.1f)
					.SetTrans(Tween.TransitionType.Cubic);
				tw.Parallel().TweenProperty(outer, "rotation", 0f, 0.08f);
				outer.ZIndex = 100;
				style.ShadowSize = 10;
				style.ShadowColor = tc * 0.35f;
				style.BorderColor = canPlay ? tc : bd;
			};
			outer.MouseExited += () =>
			{
				if (_hoveredCardControl == outer)
				{
					_hoveredCardIndex = -1;
					_hoveredCardControl = null;
				}
				int count = _deck?.Hand.Count ?? 0;
				float centerOffset = handIndex - (count - 1) / 2f;
				float angle = centerOffset * FanAngle;
				float lift = -MathF.Abs(centerOffset) * (FanLift / MathF.Max(count / 2f, 1));
				float y = _handArea.Size.Y - CardHeight + lift;

				var tw = outer.CreateTween();
				tw.TweenProperty(outer, "position:y", y, 0.08f);
				tw.Parallel().TweenProperty(outer, "scale", Vector2.One, 0.08f);
				tw.Parallel().TweenProperty(outer, "rotation", Mathf.DegToRad(angle), 0.06f);
				outer.ZIndex = handIndex;
				style.ShadowSize = 3;
				style.ShadowColor = tc * 0.15f;
				style.BorderColor = bd;
			};
		}

		outer.GuiInput += (ev) =>
		{
			if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
				OnCardClicked(card);
		};

		return outer;
	}

	// ======================================================================
	//  CARD PLAY - ANIMATED
	// ======================================================================
	private void OnCardClicked(Card card)
	{
		if (_inputLocked || _combat == null || _player == null) return;
		if (!_combat.CanPlay(_player, card)) return;

		AudioManager.Instance?.PlaySfx(AudioManager.SfxPaths.CardSelect);

		if (card.Data.TargetType is TargetType.Self or TargetType.None or TargetType.AllEnemies or
			TargetType.FrontRowEnemies or TargetType.BackRowEnemies)
		{
			PlayCardAnimated(card, null);
		}
		else
		{
			_selectedCard = card;
			HighlightValidTargets();
			RefreshHand();
		}
	}

	private void OnEnemyClicked(Enemy enemy)
	{
		if (_inputLocked || _selectedCard == null || _combat == null || _player == null) return;
		PlayCardAnimated(_selectedCard, enemy);
	}

	private async void PlayCardAnimated(Card card, Enemy? target)
	{
		if (_combat == null || _player == null) return;
		_inputLocked = true;

		var hpBefore = _enemyPanels.ToDictionary(ep => ep.Enemy, ep => ep.Enemy.CurrentHp);

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

		if (!success) { _inputLocked = false; _selectedCard = null; RefreshAll(); return; }

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
					_player!, _selectedCard.Data.TargetType, _selectedCard.Data.Range,
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

		// Player powers
		var playerPowers = new List<string>();
		foreach (var power in _player.Powers.Powers)
		{
			string pIcon = power.IsDebuff ? "\u25bc" : "\u25b2";
			playerPowers.Add($"{pIcon}{power.Name}:{power.Amount}");
		}
		_playerPowerLabel.Text = string.Join("  ", playerPowers);

		var row = _combat.Formation.GetPosition(_player.Id);
		_rowLabel.Text = row == FormationRow.Front ? "\u2694 \u524d\u6392" : "\ud83c\udfaf \u540e\u6392";
		_rowLabel.AddThemeColorOverride("font_color", row == FormationRow.Front ? new Color(1f, 0.55f, 0.2f) : new Color(0.35f, 0.7f, 0.95f));
		_switchRowBtn.Text = _player.CurrentEnergy >= 1 ? "\u5207\u6362\u9635\u578b" : "\u80fd\u91cf\u4e0d\u8db3";
		_switchRowBtn.Disabled = _player.CurrentEnergy < 1 || _player.Powers.HasPower(RogueCardGame.Core.Combat.Powers.CommonPowerIds.Rooted);

		_topBar?.Refresh();
	}

	private void RefreshEnemies()
	{
		if (_combat == null) return;
		foreach (var p in _enemyPanels) p.Update(_combat.Formation);
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
			if (isBoss)
			{
				if (run.CurrentAct >= 3)
					SceneManager.Instance.ChangeScene(SceneManager.Scenes.Victory);
				else { run.AdvanceAct(); SceneManager.Instance.ChangeScene(SceneManager.Scenes.Map); }
			}
			else
				SceneManager.Instance.ChangeScene(SceneManager.Scenes.Reward);
		}
		else
		{
			run.EndRun(false);
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
					_ => "res://resources/textures/ui/intent_attack.svg"
				};
				_intentIcon.Texture = LoadTex(intentTexPath);

				// Intent value
				_intentValueLabel.Text = it.Type switch
				{
					EnemyIntentType.Attack => $"{it.Value}" + (it.HitCount > 1 ? $"x{it.HitCount}" : ""),
					EnemyIntentType.AttackDefend => $"{it.Value}",
					EnemyIntentType.Defend => $"{it.Value}",
					_ => ""
				};
				Color ic = it.Type switch
				{
					EnemyIntentType.Attack => new Color(1f, 0.3f, 0.3f),
					EnemyIntentType.AttackDefend => new Color(1f, 0.5f, 0.2f),
					EnemyIntentType.Defend => new Color(0.35f, 0.65f, 1f),
					EnemyIntentType.Buff => new Color(0.85f, 0.65f, 0.08f),
					EnemyIntentType.Debuff => new Color(0.65f, 0.18f, 0.75f),
					_ => new Color(0.6f, 0.6f, 0.6f)
				};
				_intentValueLabel.AddThemeColorOverride("font_color", ic);
				_intentBadge.Visible = true;
			}
			else _intentBadge.Visible = false;

			var row = formation.GetPosition(Enemy.Id);
			_nameLabel.Text = Enemy.Name + (row == FormationRow.Back ? " [\u540e\u6392]" : "");

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
