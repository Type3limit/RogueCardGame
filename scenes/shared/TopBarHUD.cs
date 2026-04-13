using Godot;
using System;
using RogueCardGame.Core.Cards;
using RogueCardGame.Core.Characters;
using RogueCardGame.Core.Run;

namespace RogueCardGame;

/// <summary>
/// Persistent top-bar HUD shown across all in-run scenes (STS2-style).
/// Layout: [Avatar | ❤ HP | 💰 Gold | Potion Slots | Relic Icons | Floor | 🃏 Deck | ⚙ Settings]
/// </summary>
public partial class TopBarHUD : PanelContainer
{
    private Label _hpLabel = null!;
    private Label _goldLabel = null!;
    private HBoxContainer _potionSlots = null!;
    private Label _floorLabel = null!;
    private Label _deckLabel = null!;
    private Button _settingsBtn = null!;
    private Label _avatarLabel = null!;

    private const float BarHeight = 38f;

    /// <summary>
    /// Create and attach TopBarHUD to a parent Control.
    /// Returns the instance for further updates.
    /// </summary>
    public static TopBarHUD Attach(Control parent)
    {
        var hud = new TopBarHUD();
        hud.Build();
        parent.AddChild(hud);
        hud.Refresh();
        return hud;
    }

    private void Build()
    {
        // Anchor to top of screen, full width
        SetAnchorsPreset(LayoutPreset.TopWide);
        CustomMinimumSize = new Vector2(0, BarHeight);
        MouseFilter = MouseFilterEnum.Ignore;
        ZIndex = 50;

        var bgStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.02f, 0.02f, 0.04f, 0.92f),
            BorderColor = new Color(0.12f, 0.14f, 0.22f),
            BorderWidthBottom = 1,
            ContentMarginLeft = 12, ContentMarginRight = 12,
            ContentMarginTop = 4, ContentMarginBottom = 4,
            ShadowColor = new Color(0f, 0f, 0f, 0.35f),
            ShadowSize = 4, ShadowOffset = new Vector2(0, 2)
        };
        AddThemeStyleboxOverride("panel", bgStyle);

        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 16);
        hbox.MouseFilter = MouseFilterEnum.Ignore;

        // === Avatar ===
        var avatarPanel = new PanelContainer();
        var avatarStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.08f, 0.15f),
            BorderColor = new Color(0.3f, 0.5f, 0.7f),
            BorderWidthBottom = 1, BorderWidthTop = 1, BorderWidthLeft = 1, BorderWidthRight = 1,
            CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
            CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4,
            ContentMarginLeft = 4, ContentMarginRight = 4
        };
        avatarPanel.AddThemeStyleboxOverride("panel", avatarStyle);
        _avatarLabel = new Label { Text = "⚔" };
        _avatarLabel.AddThemeFontSizeOverride("font_size", 18);
        _avatarLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.8f, 1f));
        avatarPanel.AddChild(_avatarLabel);
        avatarPanel.MouseFilter = MouseFilterEnum.Ignore;
        hbox.AddChild(avatarPanel);

        // === HP ===
        var hpBox = new HBoxContainer();
        hpBox.AddThemeConstantOverride("separation", 4);
        hpBox.MouseFilter = MouseFilterEnum.Ignore;
        var heartIcon = new Label { Text = "❤" };
        heartIcon.AddThemeFontSizeOverride("font_size", 16);
        heartIcon.AddThemeColorOverride("font_color", new Color(0.9f, 0.2f, 0.2f));
        hpBox.AddChild(heartIcon);
        _hpLabel = new Label { Text = "75/75" };
        _hpLabel.AddThemeFontSizeOverride("font_size", 16);
        _hpLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.25f, 0.25f));
        hpBox.AddChild(_hpLabel);
        hbox.AddChild(hpBox);

        // === Gold ===
        var goldBox = new HBoxContainer();
        goldBox.AddThemeConstantOverride("separation", 4);
        goldBox.MouseFilter = MouseFilterEnum.Ignore;
        var coinIcon = new Label { Text = "💰" };
        coinIcon.AddThemeFontSizeOverride("font_size", 14);
        goldBox.AddChild(coinIcon);
        _goldLabel = new Label { Text = "99" };
        _goldLabel.AddThemeFontSizeOverride("font_size", 16);
        _goldLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.8f, 0.2f));
        goldBox.AddChild(_goldLabel);
        hbox.AddChild(goldBox);

        // === Separator ===
        hbox.AddChild(new VSeparator { Modulate = new Color(0.3f, 0.3f, 0.4f, 0.5f) });

        // === Potion Slots (3) ===
        _potionSlots = new HBoxContainer();
        _potionSlots.AddThemeConstantOverride("separation", 6);
        _potionSlots.MouseFilter = MouseFilterEnum.Ignore;
        for (int i = 0; i < 3; i++)
        {
            var slot = new PanelContainer();
            slot.CustomMinimumSize = new Vector2(26, 26);
            var slotStyle = new StyleBoxFlat
            {
                BgColor = new Color(0.06f, 0.06f, 0.1f, 0.8f),
                BorderColor = new Color(0.2f, 0.25f, 0.35f),
                BorderWidthBottom = 1, BorderWidthTop = 1, BorderWidthLeft = 1, BorderWidthRight = 1,
                CornerRadiusBottomLeft = 5, CornerRadiusBottomRight = 5,
                CornerRadiusTopLeft = 5, CornerRadiusTopRight = 5
            };
            slot.AddThemeStyleboxOverride("panel", slotStyle);
            var slotLabel = new Label { Text = "", HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center };
            slotLabel.AddThemeFontSizeOverride("font_size", 14);
            slot.AddChild(slotLabel);
            slot.MouseFilter = MouseFilterEnum.Ignore;
            _potionSlots.AddChild(slot);
        }
        hbox.AddChild(_potionSlots);

        // === Spacer ===
        hbox.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });

        // === Floor/Act info ===
        _floorLabel = new Label { Text = "Act 1" };
        _floorLabel.AddThemeFontSizeOverride("font_size", 13);
        _floorLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.55f, 0.7f));
        hbox.AddChild(_floorLabel);

        // === Separator ===
        hbox.AddChild(new VSeparator { Modulate = new Color(0.3f, 0.3f, 0.4f, 0.5f) });

        // === Deck count ===
        var deckBox = new HBoxContainer();
        deckBox.AddThemeConstantOverride("separation", 4);
        deckBox.MouseFilter = MouseFilterEnum.Ignore;
        var deckIcon = new Label { Text = "🃏" };
        deckIcon.AddThemeFontSizeOverride("font_size", 14);
        deckBox.AddChild(deckIcon);
        _deckLabel = new Label { Text = "10" };
        _deckLabel.AddThemeFontSizeOverride("font_size", 14);
        _deckLabel.AddThemeColorOverride("font_color", new Color(0.65f, 0.7f, 0.8f));
        deckBox.AddChild(_deckLabel);
        hbox.AddChild(deckBox);

        // === Settings button ===
        _settingsBtn = new Button { Text = "⚙" };
        _settingsBtn.AddThemeFontSizeOverride("font_size", 18);
        var settStyle = new StyleBoxFlat
        {
            BgColor = Colors.Transparent,
            ContentMarginLeft = 4, ContentMarginRight = 4
        };
        _settingsBtn.AddThemeStyleboxOverride("normal", settStyle);
        _settingsBtn.AddThemeStyleboxOverride("hover", settStyle);
        _settingsBtn.AddThemeColorOverride("font_color", new Color(0.55f, 0.6f, 0.7f));
        _settingsBtn.AddThemeColorOverride("font_hover_color", new Color(0.8f, 0.85f, 1f));
        _settingsBtn.Pressed += () =>
        {
            AudioManager.Instance?.PlaySfx(AudioManager.SfxPaths.ButtonClick);
            SceneManager.Instance.ChangeScene(SceneManager.Scenes.Settings);
        };
        hbox.AddChild(_settingsBtn);

        AddChild(hbox);
    }

    /// <summary>Refresh all HUD elements from current RunState.</summary>
    public void Refresh()
    {
        var run = GameManager.Instance?.CurrentRun;
        if (run == null) return;

        var player = run.Player;
        _hpLabel.Text = $"{player.CurrentHp}/{player.MaxHp}";

        // HP color based on percentage
        float pct = (float)player.CurrentHp / player.MaxHp;
        _hpLabel.AddThemeColorOverride("font_color",
            pct > 0.5f ? new Color(0.9f, 0.25f, 0.25f)
            : pct > 0.25f ? new Color(0.95f, 0.6f, 0.15f)
            : new Color(0.95f, 0.15f, 0.15f));

        _goldLabel.Text = run.Gold.ToString();

        // Avatar based on class
        _avatarLabel.Text = player.Class switch
        {
            CardClass.Vanguard => "⚔",
            CardClass.Psion => "🔮",
            CardClass.Netrunner => "💻",
            CardClass.Symbiote => "🧬",
            _ => "👤"
        };

        // Floor info
        int floor = run.FloorsCleared;
        _floorLabel.Text = $"Act {run.CurrentAct}  F{floor}";

        // Deck count
        _deckLabel.Text = run.MasterDeck.Count.ToString();

        // Potions
        var potionSlots = _potionSlots.GetChildren();
        var potions = run.Potions.Slots;
        for (int i = 0; i < potionSlots.Count; i++)
        {
            if (potionSlots[i] is PanelContainer slot && slot.GetChild(0) is Label lbl)
            {
                if (i < potions.Count && potions[i] != null)
                {
                    lbl.Text = "🧪";
                    lbl.AddThemeColorOverride("font_color", new Color(0.4f, 0.8f, 0.5f));
                }
                else
                {
                    lbl.Text = "";
                }
            }
        }
    }
}
