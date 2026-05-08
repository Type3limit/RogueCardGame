using Godot;
using System;
using System.Linq;
using RogueCardGame.Core.Cards;
using RogueCardGame.Core.Implants;
using RogueCardGame.Core.Potions;
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
    private ColorRect? _removalOverlay;
    private PanelContainer? _removalDialog;

    private ShopManager? _shop;
    private TopBarHUD? _topBar;

    public override void _Ready()
    {
        GameManager.Instance.SetCurrentRunScene("shop");

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
        bool hasRemovalTarget = item.Type != ShopItemType.CardRemoval || (run != null && run.MasterDeck.Count > 0);
        bool canAfford = run != null && run.Gold >= item.Price && hasRemovalTarget;
        bool isSold = item.IsSold;

        if (item.Type == ShopItemType.Card && item.CardData != null)
        {
            var visual = CyberCardFactory.CreateGameplayCard(
                item.CardData,
                new Vector2(196, 276),
                compact: false,
                dimmed: isSold || !canAfford,
                footer: isSold ? "已售出" : $"💰 {item.Price}",
                showDescription: true);
            var cardPanel = visual.Root;

            if (!isSold)
                CyberCardFactory.AttachHover(visual, scale: 1.04f, hoverShadow: canAfford ? 16 : 8);

            if (!isSold && canAfford)
            {
                cardPanel.GuiInput += (inputEvent) =>
                {
                    if (inputEvent is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
                        OnItemClicked(item);
                };
                cardPanel.MouseDefaultCursorShape = CursorShape.PointingHand;
            }

            return cardPanel;
        }

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

        // Description (for potions)
        if (item.Type == ShopItemType.Potion && item.PotionData != null)
        {
            string useTimeText = item.PotionData.UseTime switch
            {
                PotionUseTime.Combat => "⚔ 战斗中使用",
                PotionUseTime.Map    => "🗺 地图上使用",
                _                    => "🕐 随时使用"
            };
            Color useTimeColor = item.PotionData.UseTime switch
            {
                PotionUseTime.Combat => new Color(0.9f, 0.45f, 0.3f),
                PotionUseTime.Map    => new Color(0.35f, 0.8f, 0.45f),
                _                    => new Color(0.6f, 0.7f, 0.8f)
            };
            var useTimeLabel = new Label { Text = useTimeText, HorizontalAlignment = HorizontalAlignment.Center };
            useTimeLabel.AddThemeFontSizeOverride("font_size", 11);
            useTimeLabel.AddThemeColorOverride("font_color", isSold ? useTimeColor * 0.35f : useTimeColor);
            vbox.AddChild(useTimeLabel);

            var descLabel = new Label
            {
                Text = item.PotionData.Description,
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(0, 40)
            };
            descLabel.AddThemeFontSizeOverride("font_size", 11);
            descLabel.AddThemeColorOverride("font_color",
                isSold ? new Color(0.25f, 0.25f, 0.3f) : new Color(0.6f, 0.72f, 0.62f));
            vbox.AddChild(descLabel);
        }

        // Description (for implants)
        if (item.Type == ShopItemType.Implant && item.ImplantData != null)
        {
            string slotText = item.ImplantData.Slot switch
            {
                ImplantSlot.Neural => "★ 神经槽",
                ImplantSlot.Core   => "★ 核心槽",
                _                  => "★ 通用槽"
            };
            Color slotColor = item.ImplantData.Slot switch
            {
                ImplantSlot.Neural => new Color(0.4f, 0.7f, 1f),
                ImplantSlot.Core   => new Color(0.9f, 0.55f, 0.2f),
                _                  => new Color(0.65f, 0.7f, 0.8f)
            };
            var slotLabel = new Label { Text = slotText, HorizontalAlignment = HorizontalAlignment.Center };
            slotLabel.AddThemeFontSizeOverride("font_size", 11);
            slotLabel.AddThemeColorOverride("font_color", isSold ? slotColor * 0.35f : slotColor);
            vbox.AddChild(slotLabel);

            var descLabel = new Label
            {
                Text = item.ImplantData.Description,
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(0, 40)
            };
            descLabel.AddThemeFontSizeOverride("font_size", 11);
            descLabel.AddThemeColorOverride("font_color",
                isSold ? new Color(0.25f, 0.25f, 0.3f) : new Color(0.72f, 0.64f, 0.82f));
            vbox.AddChild(descLabel);
        }

        // Description (for card removal service)
        if (item.Type == ShopItemType.CardRemoval)
        {
            var descLabel = new Label
            {
                Text = "点击后可从牌库中\n选择一张卡牌永久移除",
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(0, 40)
            };
            descLabel.AddThemeFontSizeOverride("font_size", 11);
            descLabel.AddThemeColorOverride("font_color",
                isSold ? new Color(0.25f, 0.25f, 0.3f) : new Color(0.78f, 0.55f, 0.55f));
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
                Text = hasRemovalTarget || item.Type != ShopItemType.CardRemoval
                    ? $"💰 {item.Price}"
                    : "无可移除卡牌",
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

        if (item.Type == ShopItemType.CardRemoval)
        {
            ShowCardRemovalSelector(item);
            return;
        }

        if (!run.TrySpendGold(item.Price)) return;

        AudioManager.Instance?.PlaySfx(AudioManager.SfxPaths.CardSelect);
        ShowPurchaseFlash();

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
        }

        item.IsSold = true;
        BuildItems();
        UpdateGold();
        _topBar?.Refresh();
    }

    private void ShowPurchaseFlash()
    {
        var flash = new ColorRect();
        flash.SetAnchorsPreset(LayoutPreset.FullRect);
        flash.Color = new Color(1f, 0.82f, 0.15f, 0.2f);
        flash.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(flash);
        var ftw = CreateTween();
        ftw.TweenProperty(flash, "color:a", 0f, 0.4f);
        ftw.TweenCallback(Callable.From(() => flash.QueueFree()));
    }

    private void ShowCardRemovalSelector(ShopItem item)
    {
        var run = GameManager.Instance.CurrentRun;
        if (run == null || run.MasterDeck.Count == 0)
            return;

        CloseCardRemovalSelector();

        _removalOverlay = new ColorRect();
        _removalOverlay.SetAnchorsPreset(LayoutPreset.FullRect);
        _removalOverlay.Color = new Color(0f, 0f, 0f, 0.55f);
        _removalOverlay.MouseFilter = MouseFilterEnum.Stop;
        AddChild(_removalOverlay);

        _removalDialog = new PanelContainer();
        _removalDialog.SetAnchorsPreset(LayoutPreset.FullRect);
        _removalDialog.AnchorLeft = 0.14f; _removalDialog.AnchorRight = 0.86f;
        _removalDialog.AnchorTop = 0.12f; _removalDialog.AnchorBottom = 0.86f;
        _removalDialog.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.04f, 0.05f, 0.09f, 0.98f),
            BorderColor = new Color(0f, 0.9f, 0.9f, 0.35f),
            BorderWidthBottom = 2, BorderWidthTop = 2,
            BorderWidthLeft = 2, BorderWidthRight = 2,
            CornerRadiusBottomLeft = 14, CornerRadiusBottomRight = 14,
            CornerRadiusTopLeft = 14, CornerRadiusTopRight = 14,
            ContentMarginLeft = 18, ContentMarginRight = 18,
            ContentMarginTop = 16, ContentMarginBottom = 16,
            ShadowColor = new Color(0f, 0f, 0f, 0.35f),
            ShadowSize = 12,
            ShadowOffset = new Vector2(0, 4)
        });

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 10);

        var title = new Label { Text = "选择一张要永久移除的卡牌" };
        title.AddThemeFontSizeOverride("font_size", 28);
        title.AddThemeColorOverride("font_color", new Color(1f, 0.82f, 0.15f));
        title.HorizontalAlignment = HorizontalAlignment.Center;
        root.AddChild(title);

        var subtitle = new Label { Text = $"服务价格：💰 {item.Price}" };
        subtitle.AddThemeFontSizeOverride("font_size", 14);
        subtitle.AddThemeColorOverride("font_color", new Color(0.65f, 0.75f, 0.85f));
        subtitle.HorizontalAlignment = HorizontalAlignment.Center;
        root.AddChild(subtitle);

        var scroll = new ScrollContainer();
        scroll.SizeFlagsVertical = SizeFlags.ExpandFill;

        var grid = new GridContainer { Columns = 3 };
        grid.AddThemeConstantOverride("h_separation", 12);
        grid.AddThemeConstantOverride("v_separation", 12);
        grid.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        foreach (var card in run.MasterDeck.ToList())
            grid.AddChild(CreateRemovalCardPanel(card, item));

        scroll.AddChild(grid);
        root.AddChild(scroll);

        var cancelBtn = new Button { Text = "取消" };
        cancelBtn.CustomMinimumSize = new Vector2(180, 42);
        cancelBtn.AddThemeFontSizeOverride("font_size", 18);
        cancelBtn.AddThemeStyleboxOverride("normal", new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.1f, 0.15f),
            BorderColor = new Color(0.4f, 0.45f, 0.5f),
            BorderWidthBottom = 1, BorderWidthTop = 1,
            BorderWidthLeft = 1, BorderWidthRight = 1,
            CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
            CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6
        });
        cancelBtn.Pressed += CloseCardRemovalSelector;
        root.AddChild(cancelBtn);

        _removalDialog.AddChild(root);
        AddChild(_removalDialog);
    }

    private PanelContainer CreateRemovalCardPanel(Card card, ShopItem item)
    {
        var visual = CyberCardFactory.CreateGameplayCard(
            card,
            new Vector2(196, 276),
            compact: false,
            footer: "点击选择移除",
            showDescription: true);
        var panel = visual.Root;
        panel.MouseDefaultCursorShape = CursorShape.PointingHand;
        panel.GuiInput += (ev) =>
        {
            if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
                ShowRemovalConfirmDialog(card, item);
        };
        CyberCardFactory.AttachHover(visual, scale: 1.04f, hoverShadow: 12);

        return panel;
    }

    private void ShowRemovalConfirmDialog(Card card, ShopItem item)
    {
        var run = GameManager.Instance.CurrentRun;
        if (run == null || item.IsSold) return;

        // Dim the selector while confirm is visible
        var confirmBackdrop = new ColorRect
        {
            Color = new Color(0f, 0f, 0f, 0.7f),
            ZIndex = 50,
        };
        confirmBackdrop.SetAnchorsPreset(LayoutPreset.FullRect);
        confirmBackdrop.MouseFilter = MouseFilterEnum.Stop;
        _removalDialog?.AddChild(confirmBackdrop);

        var dialog = new PanelContainer { MouseFilter = MouseFilterEnum.Stop, ZIndex = 51 };
        dialog.SetAnchorsPreset(LayoutPreset.FullRect);
        dialog.AnchorLeft = 0.1f; dialog.AnchorRight = 0.9f;
        dialog.AnchorTop = 0.15f; dialog.AnchorBottom = 0.85f;
        dialog.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.04f, 0.05f, 0.09f, 0.99f),
            BorderColor = new Color(0.85f, 0.25f, 0.25f, 0.7f),
            BorderWidthBottom = 2, BorderWidthTop = 2,
            BorderWidthLeft = 2, BorderWidthRight = 2,
            CornerRadiusBottomLeft = 12, CornerRadiusBottomRight = 12,
            CornerRadiusTopLeft = 12, CornerRadiusTopRight = 12,
            ContentMarginLeft = 20, ContentMarginRight = 20,
            ContentMarginTop = 18, ContentMarginBottom = 18,
        });

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 14);

        var hdr = new Label { Text = "确认永久移除", HorizontalAlignment = HorizontalAlignment.Center };
        hdr.AddThemeFontSizeOverride("font_size", 24);
        hdr.AddThemeColorOverride("font_color", new Color(1f, 0.38f, 0.38f));
        root.AddChild(hdr);

        var warning = new Label
        {
            Text = "此操作不可撤销，该卡牌将永久从牌库消失",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        warning.AddThemeFontSizeOverride("font_size", 13);
        warning.AddThemeColorOverride("font_color", new Color(0.72f, 0.55f, 0.55f));
        root.AddChild(warning);

        // Preview the card being removed
        var cardPreview = CyberCardFactory.CreateGameplayCard(
            card, new Vector2(196, 276), compact: false, showDescription: true);
        cardPreview.Root.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        cardPreview.Root.MouseFilter = MouseFilterEnum.Ignore;
        root.AddChild(cardPreview.Root);

        var costNote = new Label
        {
            Text = $"费用：💰 {item.Price}",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        costNote.AddThemeFontSizeOverride("font_size", 16);
        costNote.AddThemeColorOverride("font_color", new Color(1f, 0.82f, 0.15f));
        root.AddChild(costNote);

        var btnRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        btnRow.AddThemeConstantOverride("separation", 16);

        var cancelBtn = new Button { Text = "× 取消", CustomMinimumSize = new Vector2(120, 42) };
        cancelBtn.AddThemeFontSizeOverride("font_size", 16);
        cancelBtn.AddThemeStyleboxOverride("normal", new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.1f, 0.15f),
            BorderColor = new Color(0.38f, 0.42f, 0.5f),
            BorderWidthBottom = 1, BorderWidthTop = 1,
            BorderWidthLeft = 1, BorderWidthRight = 1,
            CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
            CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8,
            ContentMarginLeft = 12, ContentMarginRight = 12,
            ContentMarginTop = 6, ContentMarginBottom = 6,
        });
        cancelBtn.AddThemeColorOverride("font_color", new Color(0.62f, 0.68f, 0.78f));
        cancelBtn.Pressed += () => { confirmBackdrop.QueueFree(); dialog.QueueFree(); };
        btnRow.AddChild(cancelBtn);

        var confirmBtn = new Button { Text = "✓ 确认移除", CustomMinimumSize = new Vector2(140, 42) };
        confirmBtn.AddThemeFontSizeOverride("font_size", 16);
        confirmBtn.AddThemeStyleboxOverride("normal", new StyleBoxFlat
        {
            BgColor = new Color(0.18f, 0.06f, 0.06f),
            BorderColor = new Color(0.85f, 0.28f, 0.28f, 0.85f),
            BorderWidthBottom = 2, BorderWidthTop = 2,
            BorderWidthLeft = 2, BorderWidthRight = 2,
            CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
            CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8,
            ContentMarginLeft = 12, ContentMarginRight = 12,
            ContentMarginTop = 6, ContentMarginBottom = 6,
        });
        confirmBtn.AddThemeStyleboxOverride("hover", new StyleBoxFlat
        {
            BgColor = new Color(0.28f, 0.08f, 0.08f),
            BorderColor = new Color(1f, 0.35f, 0.35f),
            BorderWidthBottom = 2, BorderWidthTop = 2,
            BorderWidthLeft = 2, BorderWidthRight = 2,
            CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
            CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8,
            ContentMarginLeft = 12, ContentMarginRight = 12,
            ContentMarginTop = 6, ContentMarginBottom = 6,
        });
        confirmBtn.AddThemeColorOverride("font_color", new Color(1f, 0.55f, 0.55f));
        confirmBtn.Pressed += () =>
        {
            confirmBackdrop.QueueFree();
            dialog.QueueFree();
            ConfirmCardRemoval(card, item);
        };
        btnRow.AddChild(confirmBtn);
        root.AddChild(btnRow);

        dialog.AddChild(root);
        _removalDialog?.AddChild(dialog);
    }

    private void ConfirmCardRemoval(Card card, ShopItem item)
    {
        var run = GameManager.Instance.CurrentRun;
        if (run == null || item.IsSold) return;
        if (!run.TrySpendGold(item.Price))
        {
            CloseCardRemovalSelector();
            return;
        }

        run.RemoveCardFromDeck(card);
        item.IsSold = true;
        AudioManager.Instance?.PlaySfx(AudioManager.SfxPaths.CardSelect);
        ShowPurchaseFlash();
        CloseCardRemovalSelector();
        BuildItems();
        UpdateGold();
        _topBar?.Refresh();
    }

    private void CloseCardRemovalSelector()
    {
        _removalDialog?.QueueFree();
        _removalOverlay?.QueueFree();
        _removalDialog = null;
        _removalOverlay = null;
    }

    private void UpdateGold()
    {
        var run = GameManager.Instance.CurrentRun;
        _goldLabel.Text = $"💰 {run?.Gold ?? 0}";
    }

    private void OnLeavePressed()
    {
        CloseCardRemovalSelector();
        AudioManager.Instance?.PlaySfx(AudioManager.SfxPaths.ButtonClick);
        GameManager.Instance.SetCurrentRunScene("map");
        SceneManager.Instance.ChangeScene(SceneManager.Scenes.Map);
    }
}