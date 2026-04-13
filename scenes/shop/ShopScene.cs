using Godot;
using System;
using System.Linq;
using RogueCardGame.Core.Cards;
using RogueCardGame.Core.Shop;

namespace RogueCardGame;

public partial class ShopScene : Control
{
    private Label _goldLabel = null!;
    private Label _title = null!;
    private HBoxContainer _cardRow = null!;
    private HBoxContainer _miscRow = null!;
    private HBoxContainer _serviceRow = null!;
    private Button _leaveBtn = null!;

    private ShopManager? _shop;
    private TopBarHUD? _topBar;

    public override void _Ready()
    {
        _goldLabel = GetNode<Label>("TopBar/GoldLabel");
        _title = GetNode<Label>("TopBar/Title");
        _leaveBtn = GetNode<Button>("LeaveBtn");
        _cardRow = GetNode<HBoxContainer>("ShopScroll/ShopContent/CardSection/CardRow");
        _miscRow = GetNode<HBoxContainer>("ShopScroll/ShopContent/MiscSection/MiscRow");
        _serviceRow = GetNode<HBoxContainer>("ShopScroll/ShopContent/ServiceSection/ServiceRow");

        _leaveBtn.Pressed += OnLeavePressed;

        CyberFx.AddScanlines(this, 0.03f);
        CyberFx.AddVignette(this);

        _topBar = TopBarHUD.Attach(this);

        StyleUI();
        AudioManager.Instance?.PlayBgm(AudioManager.BgmPaths.Shop);
        GenerateShop();
    }

    private void StyleUI()
    {
        var gold = new Color(1f, 0.82f, 0.15f);
        var cyan = new Color(0f, 0.95f, 0.95f);

        _title.AddThemeFontSizeOverride("font_size", 36);
        _title.AddThemeColorOverride("font_color", gold);

        _goldLabel.AddThemeFontSizeOverride("font_size", 26);
        _goldLabel.AddThemeColorOverride("font_color", gold);

        // Section labels
        foreach (var sectionName in new[] { "CardSection", "MiscSection", "ServiceSection" })
        {
            var label = GetNode<Label>($"ShopScroll/ShopContent/{sectionName}/SectionLabel");
            label.AddThemeFontSizeOverride("font_size", 22);
            label.AddThemeColorOverride("font_color", cyan * 0.8f);
        }

        var leaveStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.15f, 0.2f, 0.95f),
            BorderColor = cyan,
            BorderWidthBottom = 2, BorderWidthTop = 2,
            BorderWidthLeft = 2, BorderWidthRight = 2,
            CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
            CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8,
            ContentMarginTop = 12, ContentMarginBottom = 12
        };
        var leaveHover = (StyleBoxFlat)leaveStyle.Duplicate();
        leaveHover.BgColor = new Color(0.12f, 0.22f, 0.3f);
        _leaveBtn.AddThemeStyleboxOverride("normal", leaveStyle);
        _leaveBtn.AddThemeStyleboxOverride("hover", leaveHover);
        _leaveBtn.AddThemeColorOverride("font_color", cyan);
        _leaveBtn.AddThemeFontSizeOverride("font_size", 22);
    }

    private void GenerateShop()
    {
        var run = GameManager.Instance.CurrentRun;
        if (run == null) return;

        _shop = new ShopManager(run.Random);
        _shop.GenerateShop(run.CardDb, run.PotionDb, run.ImplantDb, run.Player.Class, run.CurrentAct);

        BuildItems();
        UpdateGold();
    }

    private void BuildItems()
    {
        foreach (var child in _cardRow.GetChildren()) child.QueueFree();
        foreach (var child in _miscRow.GetChildren()) child.QueueFree();
        foreach (var child in _serviceRow.GetChildren()) child.QueueFree();

        if (_shop == null) return;

        int idx = 0;
        foreach (var item in _shop.Items)
        {
            var card = CreateShopCard(item);

            // Entry stagger animation
            card.Modulate = new Color(1, 1, 1, 0);
            card.Scale = new Vector2(0.85f, 0.85f);
            card.PivotOffset = card.CustomMinimumSize / 2;
            var tw = CreateTween();
            tw.TweenProperty(card, "modulate:a", 1f, 0.25f).SetDelay(0.1f + idx * 0.06f);
            tw.Parallel().TweenProperty(card, "scale", Vector2.One, 0.25f)
                .SetDelay(0.1f + idx * 0.06f)
                .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);

            switch (item.Type)
            {
                case ShopItemType.Card:
                    _cardRow.AddChild(card);
                    break;
                case ShopItemType.CardRemoval:
                    _serviceRow.AddChild(card);
                    break;
                default:
                    _miscRow.AddChild(card);
                    break;
            }
            idx++;
        }
    }

    private PanelContainer CreateShopCard(ShopItem item)
    {
        var run = GameManager.Instance.CurrentRun;
        bool canAfford = run != null && run.Gold >= item.Price;
        bool isSold = item.IsSold;

        // Determine accent color
        Color accent;
        if (item.Type == ShopItemType.Card && item.CardData != null)
        {
            accent = item.CardData.Type switch
            {
                CardType.Attack => new Color(0.85f, 0.2f, 0.2f),
                CardType.Skill => new Color(0.2f, 0.5f, 0.85f),
                CardType.Power => new Color(0.75f, 0.5f, 0.1f),
                _ => new Color(0.5f, 0.5f, 0.5f)
            };
        }
        else if (item.Type == ShopItemType.Potion)
            accent = new Color(0.3f, 0.8f, 0.4f);
        else if (item.Type == ShopItemType.Implant)
            accent = new Color(0.6f, 0.4f, 0.95f);
        else if (item.Type == ShopItemType.CardRemoval)
            accent = new Color(0.9f, 0.3f, 0.3f);
        else
            accent = new Color(0.5f, 0.5f, 0.5f);

        if (isSold) accent *= 0.3f;

        var panel = new PanelContainer();
        panel.CustomMinimumSize = new Vector2(180, 240);

        var panelStyle = new StyleBoxFlat
        {
            BgColor = isSold ? new Color(0.05f, 0.05f, 0.08f, 0.8f)
                : new Color(0.06f, 0.06f, 0.12f, 0.95f),
            BorderColor = isSold ? new Color(0.15f, 0.15f, 0.18f)
                : (canAfford ? accent * 0.7f : accent * 0.3f),
            BorderWidthBottom = 2, BorderWidthTop = 2,
            BorderWidthLeft = 2, BorderWidthRight = 2,
            CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10,
            CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10,
            ShadowColor = canAfford && !isSold
                ? new Color(accent.R, accent.G, accent.B, 0.15f) : Colors.Transparent,
            ShadowSize = canAfford && !isSold ? 8 : 0
        };
        panel.AddThemeStyleboxOverride("panel", panelStyle);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_top", 12);
        margin.AddThemeConstantOverride("margin_bottom", 12);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 6);

        // Type icon
        string typeIcon = item.Type switch
        {
            ShopItemType.Card => item.CardData?.Type switch
            {
                CardType.Attack => "⚔",
                CardType.Skill => "🛡",
                CardType.Power => "⚡",
                _ => "🃏"
            },
            ShopItemType.Potion => "🧪",
            ShopItemType.Implant => "⚙",
            ShopItemType.CardRemoval => "✂",
            _ => "?"
        };

        var iconLabel = new Label { Text = typeIcon, HorizontalAlignment = HorizontalAlignment.Center };
        iconLabel.AddThemeFontSizeOverride("font_size", 32);
        vbox.AddChild(iconLabel);

        // Name
        var nameLabel = new Label
        {
            Text = item.Name,
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        nameLabel.AddThemeFontSizeOverride("font_size", 16);
        nameLabel.AddThemeColorOverride("font_color", isSold ? new Color(0.3f, 0.3f, 0.35f) : accent);
        vbox.AddChild(nameLabel);

        // Rarity tag for cards
        if (item.Type == ShopItemType.Card && item.CardData != null)
        {
            string rarityText = item.CardData.Rarity switch
            {
                CardRarity.Rare => "稀有",
                CardRarity.Uncommon => "罕见",
                _ => "普通"
            };
            Color rarityColor = item.CardData.Rarity switch
            {
                CardRarity.Rare => new Color(1f, 0.8f, 0f),
                CardRarity.Uncommon => new Color(0.3f, 0.8f, 0.3f),
                _ => new Color(0.5f, 0.5f, 0.55f)
            };
            var rarityLabel = new Label
            {
                Text = rarityText,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            rarityLabel.AddThemeFontSizeOverride("font_size", 12);
            rarityLabel.AddThemeColorOverride("font_color", isSold ? rarityColor * 0.3f : rarityColor);
            vbox.AddChild(rarityLabel);
        }

        // Description (for cards)
        if (item.Type == ShopItemType.Card && item.CardData != null)
        {
            var descLabel = new Label
            {
                Text = item.CardData.Description,
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(0, 40)
            };
            descLabel.AddThemeFontSizeOverride("font_size", 11);
            descLabel.AddThemeColorOverride("font_color",
                isSold ? new Color(0.25f, 0.25f, 0.3f) : new Color(0.6f, 0.6f, 0.65f));
            vbox.AddChild(descLabel);
        }

        // Spacer
        var spacer = new Control();
        spacer.SizeFlagsVertical = SizeFlags.ExpandFill;
        vbox.AddChild(spacer);

        // Price or sold indicator
        if (isSold)
        {
            var soldLabel = new Label
            {
                Text = "已售出",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            soldLabel.AddThemeFontSizeOverride("font_size", 14);
            soldLabel.AddThemeColorOverride("font_color", new Color(0.3f, 0.3f, 0.35f));
            vbox.AddChild(soldLabel);
        }
        else
        {
            var priceLabel = new Label
            {
                Text = $"💰 {item.Price}",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            priceLabel.AddThemeFontSizeOverride("font_size", 18);
            priceLabel.AddThemeColorOverride("font_color",
                canAfford ? new Color(1f, 0.82f, 0.15f) : new Color(0.5f, 0.3f, 0.3f));
            vbox.AddChild(priceLabel);
        }

        margin.AddChild(vbox);
        panel.AddChild(margin);

        // Hover animation
        if (!isSold)
        {
            panel.PivotOffset = panel.CustomMinimumSize / 2;
            panel.MouseEntered += () =>
            {
                var hw = panel.CreateTween();
                hw.TweenProperty(panel, "scale", new Vector2(1.06f, 1.06f), 0.12f)
                    .SetTrans(Tween.TransitionType.Back);
                panelStyle.ShadowSize = canAfford ? 14 : 4;
                panelStyle.BorderColor = canAfford ? accent : accent * 0.5f;
            };
            panel.MouseExited += () =>
            {
                var hw = panel.CreateTween();
                hw.TweenProperty(panel, "scale", Vector2.One, 0.1f);
                panelStyle.ShadowSize = canAfford ? 8 : 0;
                panelStyle.BorderColor = canAfford ? accent * 0.7f : accent * 0.3f;
            };
        }

        // Click handler
        if (!isSold && canAfford)
        {
            panel.GuiInput += (inputEvent) =>
            {
                if (inputEvent is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
                    OnItemClicked(item);
            };
            panel.MouseDefaultCursorShape = CursorShape.PointingHand;
        }

        return panel;
    }

    private void OnItemClicked(ShopItem item)
    {
        var run = GameManager.Instance.CurrentRun;
        if (run == null || item.IsSold) return;
        if (!run.TrySpendGold(item.Price)) return;

        AudioManager.Instance?.PlaySfx(AudioManager.SfxPaths.CardSelect);

        // Purchase flash effect
        var flash = new ColorRect();
        flash.SetAnchorsPreset(LayoutPreset.FullRect);
        flash.Color = new Color(1f, 0.82f, 0.15f, 0.2f);
        flash.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(flash);
        var ftw = CreateTween();
        ftw.TweenProperty(flash, "color:a", 0f, 0.4f);
        ftw.TweenCallback(Callable.From(() => flash.QueueFree()));

        switch (item.Type)
        {
            case ShopItemType.Card when item.CardData != null:
                run.AddCardToDeck(item.CardData);
                break;
            case ShopItemType.Potion when item.PotionData != null:
                run.Potions.TryAddPotion(item.PotionData);
                break;
            case ShopItemType.Implant when item.ImplantData != null:
                run.Implants.Equip(item.ImplantData);
                break;
            case ShopItemType.CardRemoval:
                if (run.MasterDeck.Count > 0)
                {
                    var card = run.MasterDeck[^1];
                    run.RemoveCardFromDeck(card);
                }
                break;
        }

        item.IsSold = true;
        BuildItems();
        UpdateGold();
        _topBar?.Refresh();
    }

    private void UpdateGold()
    {
        var run = GameManager.Instance.CurrentRun;
        _goldLabel.Text = $"💰 {run?.Gold ?? 0}";
    }

    private void OnLeavePressed()
    {
        AudioManager.Instance?.PlaySfx(AudioManager.SfxPaths.ButtonClick);
        SceneManager.Instance.ChangeScene(SceneManager.Scenes.Map);
    }
}