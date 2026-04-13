using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using RogueCardGame.Core;
using RogueCardGame.Core.Cards;
using RogueCardGame.Core.Map;

namespace RogueCardGame;

public partial class RewardScene : Control
{
    private Label _title = null!;
    private VBoxContainer _rewardList = null!;
    private HBoxContainer _cardRewardArea = null!;
    private Button _skipBtn = null!;

    private List<CardData> _cardRewards = [];
    private int _revealIndex;
    private readonly List<Control> _pendingCards = [];
    private TopBarHUD? _topBar;

    public override void _Ready()
    {
        _title = GetNode<Label>("Title");
        _rewardList = GetNode<VBoxContainer>("RewardList");
        _cardRewardArea = GetNode<HBoxContainer>("CardRewardArea");
        _skipBtn = GetNode<Button>("SkipBtn");

        _skipBtn.Pressed += OnSkipPressed;

        CyberFx.AddScanlines(this, 0.03f);
        CyberFx.AddVignette(this);

        _topBar = TopBarHUD.Attach(this);

        StyleBackground();
        StyleUI();
        BuildRewards();

        AudioManager.Instance?.PlayBgm(AudioManager.BgmPaths.Map);

        // Stagger reveal
        _revealIndex = 0;
        var timer = new Godot.Timer { WaitTime = 0.12, Autostart = true };
        timer.Timeout += RevealNext;
        AddChild(timer);
    }

    private void RevealNext()
    {
        // Reveal reward list items
        var allItems = new List<Control>();
        foreach (var c in _rewardList.GetChildren())
            if (c is Control ctrl) allItems.Add(ctrl);
        foreach (var c in _pendingCards)
            allItems.Add(c);

        if (_revealIndex < allItems.Count)
        {
            var item = allItems[_revealIndex];
            item.Modulate = new Color(1, 1, 1, 0);
            item.Visible = true;
            var tw = CreateTween();
            tw.TweenProperty(item, "modulate:a", 1.0f, 0.25f)
                .SetTrans(Tween.TransitionType.Cubic);
            tw.Parallel().TweenProperty(item, "position:y",
                item.Position.Y, 0.25f)
                .From(item.Position.Y + 20f)
                .SetTrans(Tween.TransitionType.Back);
            _revealIndex++;
        }
    }

    private void StyleBackground()
    {
        // Overlay gradient
        var grad = new ColorRect();
        grad.SetAnchorsPreset(LayoutPreset.FullRect);
        grad.Color = new Color(0.02f, 0.02f, 0.06f, 0.7f);
        AddChild(grad);
        MoveChild(grad, 1);
    }

    private void StyleUI()
    {
        _title.AddThemeFontSizeOverride("font_size", 40);
        _title.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.2f));
        _title.Text = "⚡ 战 利 品 ⚡";

        var skipStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.08f, 0.14f),
            BorderColor = new Color(0f, 0.85f, 0.85f),
            BorderWidthBottom = 2, BorderWidthTop = 2,
            BorderWidthLeft = 2, BorderWidthRight = 2,
            CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10,
            CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10,
            ContentMarginLeft = 30, ContentMarginRight = 30,
            ContentMarginTop = 8, ContentMarginBottom = 8,
            ShadowColor = new Color(0f, 0.7f, 0.7f, 0.3f),
            ShadowSize = 6
        };
        _skipBtn.AddThemeStyleboxOverride("normal", skipStyle);
        var skipHover = (StyleBoxFlat)skipStyle.Duplicate();
        skipHover.BgColor = new Color(0f, 0.25f, 0.3f);
        skipHover.ShadowSize = 10;
        _skipBtn.AddThemeStyleboxOverride("hover", skipHover);
        _skipBtn.AddThemeColorOverride("font_color", new Color(0f, 0.95f, 0.95f));
        _skipBtn.AddThemeFontSizeOverride("font_size", 22);
        _skipBtn.Text = "跳过 → 返回地图";
    }

    private void BuildRewards()
    {
        var run = GameManager.Instance.CurrentRun;
        if (run == null) return;

        var node = run.CurrentMap?.CurrentNode;
        bool isElite = node?.Type == RoomType.EliteCombat;
        bool isBoss = node?.Type == RoomType.Boss;

        var gb = BalanceConfig.Current.GlobalBalance;
        int goldReward = isBoss ? gb.BossGoldReward
            : isElite ? gb.EliteGoldReward
            : run.Random.Next(gb.NormalGoldRewardMin, gb.NormalGoldRewardMax);
        AddRewardItem("💰", $"{goldReward} 金币", new Color(1f, 0.85f, 0.2f), true, () =>
        {
            run.AddGold(goldReward);
            AudioManager.Instance?.PlaySfx(AudioManager.SfxPaths.GoldGain);
            _topBar?.Refresh();
        });

        // Potion drop
        if (run.Random.NextDouble() < 0.4 && run.Potions.HasEmptySlot())
        {
            var potionPool = run.PotionDb.GetAll();
            if (potionPool.Count > 0)
            {
                var potion = potionPool[run.Random.Next(potionPool.Count)];
                AddRewardItem("🧪", potion.Name, new Color(0.3f, 0.9f, 0.5f), false, () =>
                {
                    run.Potions.TryAddPotion(potion);
                    AudioManager.Instance?.PlaySfx(AudioManager.SfxPaths.CardSelect);
                });
            }
        }

        // Relic drop for elite/boss
        if (isElite || isBoss)
        {
            AddRewardItem("🔮", "随机遗物", new Color(0.7f, 0.5f, 1f), false, () =>
            {
                AudioManager.Instance?.PlaySfx(AudioManager.SfxPaths.CardSelect);
            });
        }

        GenerateCardRewards(run);
    }

    private void AddRewardItem(string icon, string text, Color color, bool autoCollect, Action? onCollect = null)
    {
        var panel = new PanelContainer();
        panel.CustomMinimumSize = new Vector2(0, 56);

        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.06f, 0.06f, 0.1f, 0.95f),
            BorderColor = color * 0.5f,
            BorderWidthBottom = 1, BorderWidthTop = 1,
            BorderWidthLeft = 3, BorderWidthRight = 1,
            CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
            CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8,
            ContentMarginLeft = 20, ContentMarginRight = 20,
            ContentMarginTop = 8, ContentMarginBottom = 8,
            ShadowColor = color * 0.15f,
            ShadowSize = 4
        };
        panel.AddThemeStyleboxOverride("panel", style);

        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 15);

        var iconLabel = new Label { Text = icon };
        iconLabel.AddThemeFontSizeOverride("font_size", 28);
        hbox.AddChild(iconLabel);

        var textLabel = new Label { Text = text };
        textLabel.AddThemeFontSizeOverride("font_size", 22);
        textLabel.AddThemeColorOverride("font_color", color);
        textLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        hbox.AddChild(textLabel);

        if (autoCollect)
        {
            var tag = new Label { Text = "✓ 已获得" };
            tag.AddThemeFontSizeOverride("font_size", 16);
            tag.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
            hbox.AddChild(tag);

            onCollect?.Invoke();
        }
        else
        {
            var tag = new Label { Text = "点击领取 →" };
            tag.AddThemeFontSizeOverride("font_size", 16);
            tag.AddThemeColorOverride("font_color", new Color(0.4f, 0.8f, 0.8f));
            hbox.AddChild(tag);

            panel.GuiInput += (ev) =>
            {
                if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
                {
                    onCollect?.Invoke();
                    tag.Text = "✓ 已获得";
                    tag.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
                    style.BorderColor = new Color(0.2f, 0.2f, 0.2f);
                    style.ShadowSize = 0;
                    panel.GuiInput -= null!; // no-op but prevents repeated clicks
                }
            };

            panel.MouseEntered += () =>
            {
                AudioManager.Instance?.PlaySfx(AudioManager.SfxPaths.ButtonHover);
                var tw = CreateTween();
                tw.TweenProperty(panel, "modulate", new Color(1.2f, 1.2f, 1.2f), 0.15f);
            };
            panel.MouseExited += () =>
            {
                var tw = CreateTween();
                tw.TweenProperty(panel, "modulate", Colors.White, 0.15f);
            };
        }

        panel.AddChild(hbox);
        panel.Visible = false; // revealed by stagger
        _rewardList.AddChild(panel);
    }

    private void GenerateCardRewards(Core.Run.RunState run)
    {
        var playerClass = run.Player.Class;
        var pool = run.CardDb.GetCardsByClass(playerClass)
            .Where(c => c.Rarity != CardRarity.Starter)
            .Concat(run.CardDb.GetCardsByClass(CardClass.Colorless)
                .Where(c => c.Rarity != CardRarity.Starter))
            .ToList();

        run.Random.Shuffle(pool);
        _cardRewards = pool.Take(3).ToList();

        foreach (var cardData in _cardRewards)
        {
            var card = CreateCardRewardPanel(cardData);
            card.Visible = false;
            _cardRewardArea.AddChild(card);
            _pendingCards.Add(card);
        }
    }

    private PanelContainer CreateCardRewardPanel(CardData cardData)
    {
        var typeColor = cardData.Type switch
        {
            CardType.Attack => new Color(0.85f, 0.2f, 0.2f),
            CardType.Skill => new Color(0.2f, 0.5f, 0.85f),
            CardType.Power => new Color(0.8f, 0.6f, 0.15f),
            _ => new Color(0.4f, 0.4f, 0.4f)
        };

        var rarityColor = cardData.Rarity switch
        {
            CardRarity.Rare => new Color(1f, 0.85f, 0.1f),
            CardRarity.Uncommon => new Color(0.4f, 0.7f, 1f),
            _ => new Color(0.6f, 0.6f, 0.6f)
        };

        var panel = new PanelContainer();
        panel.CustomMinimumSize = new Vector2(200, 280);

        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.06f, 0.06f, 0.1f, 0.98f),
            BorderColor = typeColor * 0.6f,
            BorderWidthBottom = 2, BorderWidthTop = 2,
            BorderWidthLeft = 2, BorderWidthRight = 2,
            CornerRadiusBottomLeft = 12, CornerRadiusBottomRight = 12,
            CornerRadiusTopLeft = 12, CornerRadiusTopRight = 12,
            ContentMarginLeft = 14, ContentMarginRight = 14,
            ContentMarginTop = 12, ContentMarginBottom = 12,
            ShadowColor = typeColor * 0.2f,
            ShadowSize = 8
        };
        panel.AddThemeStyleboxOverride("panel", style);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 6);

        // Cost badge + type
        var topRow = new HBoxContainer();
        var costBadge = new Label { Text = $"[{cardData.Cost}]" };
        costBadge.AddThemeFontSizeOverride("font_size", 24);
        costBadge.AddThemeColorOverride("font_color", new Color(0f, 0.9f, 0.9f));
        topRow.AddChild(costBadge);

        var spacer = new Control();
        spacer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        topRow.AddChild(spacer);

        string typeIcon = cardData.Type switch
        {
            CardType.Attack => "⚔",
            CardType.Skill => "🛡",
            CardType.Power => "⚡",
            _ => "?"
        };
        var typeLabel = new Label { Text = typeIcon };
        typeLabel.AddThemeFontSizeOverride("font_size", 22);
        topRow.AddChild(typeLabel);
        vbox.AddChild(topRow);

        // Name
        var nameLabel = new Label { Text = cardData.Name };
        nameLabel.AddThemeFontSizeOverride("font_size", 20);
        nameLabel.AddThemeColorOverride("font_color", Colors.White);
        nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(nameLabel);

        // Rarity tag
        string rarityText = cardData.Rarity switch
        {
            CardRarity.Rare => "★ 稀有",
            CardRarity.Uncommon => "◆ 罕见",
            _ => "○ 普通"
        };
        var rarityLabel = new Label { Text = rarityText };
        rarityLabel.AddThemeFontSizeOverride("font_size", 14);
        rarityLabel.AddThemeColorOverride("font_color", rarityColor);
        rarityLabel.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(rarityLabel);

        // Divider
        var divider = new ColorRect();
        divider.CustomMinimumSize = new Vector2(0, 2);
        divider.Color = typeColor * 0.4f;
        vbox.AddChild(divider);

        // Description
        var desc = new Label { Text = cardData.Description };
        desc.AddThemeFontSizeOverride("font_size", 14);
        desc.AddThemeColorOverride("font_color", new Color(0.75f, 0.75f, 0.8f));
        desc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        desc.SizeFlagsVertical = SizeFlags.ExpandFill;
        vbox.AddChild(desc);

        panel.AddChild(vbox);

        // Hover & click
        panel.MouseEntered += () =>
        {
            var tw = CreateTween();
            tw.TweenProperty(panel, "scale", new Vector2(1.05f, 1.05f), 0.15f)
                .SetTrans(Tween.TransitionType.Back);
            style.BorderColor = Colors.White;
            style.ShadowSize = 14;
        };
        panel.MouseExited += () =>
        {
            var tw = CreateTween();
            tw.TweenProperty(panel, "scale", Vector2.One, 0.15f);
            style.BorderColor = typeColor * 0.6f;
            style.ShadowSize = 8;
        };

        panel.GuiInput += (ev) =>
        {
            if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            {
                OnCardSelected(cardData, panel);
            }
        };

        return panel;
    }

    private void OnCardSelected(CardData cardData, PanelContainer selectedPanel)
    {
        var run = GameManager.Instance.CurrentRun;
        if (run == null) return;

        AudioManager.Instance?.PlaySfx(AudioManager.SfxPaths.CardSelect);
        run.AddCardToDeck(cardData);

        // Dim non-selected cards, highlight selected
        foreach (var child in _cardRewardArea.GetChildren())
        {
            if (child is PanelContainer pc)
            {
                if (pc == selectedPanel)
                {
                    var tw = CreateTween();
                    tw.TweenProperty(pc, "modulate", new Color(1.3f, 1.3f, 1.3f), 0.3f);
                }
                else
                {
                    var tw = CreateTween();
                    tw.TweenProperty(pc, "modulate", new Color(0.3f, 0.3f, 0.3f, 0.5f), 0.3f);
                }
                // Disable further input
                pc.MouseFilter = MouseFilterEnum.Ignore;
            }
        }

        // Auto return to map
        var timer = GetTree().CreateTimer(1.0);
        timer.Timeout += () => SceneManager.Instance.ChangeScene(SceneManager.Scenes.Map);
    }

    private void OnSkipPressed()
    {
        AudioManager.Instance?.PlaySfx(AudioManager.SfxPaths.ButtonClick);
        SceneManager.Instance.ChangeScene(SceneManager.Scenes.Map);
    }
}