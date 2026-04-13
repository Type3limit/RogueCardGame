using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using RogueCardGame.Core.Map;

namespace RogueCardGame;

public partial class MapScene : Control
{
    private Control _mapContainer = null!;
    private ScrollContainer _mapScroll = null!;
    private Label _actLabel = null!;
    private Label _floorLabel = null!;
    private Label _goldLabel = null!;
    private Label _hpLabel = null!;
    private Label _implantInfo = null!;
    private Button _deckBtn = null!;

    private readonly List<MapNodeUI> _nodeUIs = [];

    private static readonly Dictionary<RoomType, Color> RoomColors = new()
    {
        [RoomType.Combat] = new Color(0.85f, 0.25f, 0.25f),
        [RoomType.EliteCombat] = new Color(1f, 0.55f, 0.1f),
        [RoomType.Boss] = new Color(1f, 0.08f, 0.08f),
        [RoomType.RestSite] = new Color(0.2f, 0.85f, 0.4f),
        [RoomType.Shop] = new Color(1f, 0.82f, 0.15f),
        [RoomType.Event] = new Color(0.45f, 0.55f, 0.95f),
        [RoomType.Treasure] = new Color(1f, 0.75f, 0f),
        [RoomType.Start] = new Color(0f, 0.9f, 0.9f),
        [RoomType.Victory] = new Color(1f, 0.85f, 0f),
    };

    private static readonly Dictionary<RoomType, string> RoomIcons = new()
    {
        [RoomType.Combat] = "\u2694",
        [RoomType.EliteCombat] = "\u2620",
        [RoomType.Boss] = "\ud83d\udc79",
        [RoomType.RestSite] = "\ud83d\udd25",
        [RoomType.Shop] = "$",
        [RoomType.Event] = "?",
        [RoomType.Treasure] = "\u2605",
        [RoomType.Start] = "\u25b6",
        [RoomType.Victory] = "\u2b50",
    };

    private static readonly Dictionary<RoomType, string> RoomLabels = new()
    {
        [RoomType.Combat] = "\u6218\u6597",
        [RoomType.EliteCombat] = "\u7cbe\u82f1",
        [RoomType.Boss] = "\u9996\u9886",
        [RoomType.RestSite] = "\u7be1\u706b",
        [RoomType.Shop] = "\u5546\u5e97",
        [RoomType.Event] = "\u4e8b\u4ef6",
        [RoomType.Treasure] = "\u5b9d\u7bb1",
        [RoomType.Start] = "\u8d77\u70b9",
        [RoomType.Victory] = "\u80dc\u5229",
    };

    private const float NodeSize = 60f;
    private const float MapPadding = 60f;
    private const float RowSpacing = 110f;

    public override void _Ready()
    {
        _mapScroll = GetNode<ScrollContainer>("MapScroll");
        _mapContainer = GetNode<Control>("MapScroll/MapContainer");
        _actLabel = GetNode<Label>("TopBar/ActLabel");
        _floorLabel = GetNode<Label>("TopBar/FloorLabel");
        _goldLabel = GetNode<Label>("TopBar/GoldLabel");
        _hpLabel = GetNode<Label>("TopBar/HpLabel");
        _deckBtn = GetNode<Button>("TopBar/DeckBtn");
        _implantInfo = GetNode<Label>("BottomBar/ImplantInfo");

        // Atmosphere
        CyberFx.AddParticles(this, 20, new Color(0f, 0.6f, 0.7f, 0.1f));
        CyberFx.AddScanlines(this, 0.03f);
        CyberFx.AddVignette(this);

        StyleTopBar();
        StyleBottomBar();
        AudioManager.Instance?.PlayBgm(AudioManager.BgmPaths.Map);
        BuildMap();
        UpdatePlayerInfo();
        ScrollToCurrentNode();
    }

    // ===== TOP BAR =====
    private void StyleTopBar()
    {
        var topBar = GetNode<HBoxContainer>("TopBar");
        topBar.AddThemeConstantOverride("separation", 30);

        // Top bar background panel
        var topBg = new PanelContainer();
        topBg.SetAnchorsPreset(LayoutPreset.FullRect);
        topBg.AnchorRight = 1f; topBg.AnchorBottom = 0f;
        topBg.OffsetBottom = 65;
        topBg.MouseFilter = MouseFilterEnum.Ignore;
        var topStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.02f, 0.02f, 0.05f, 0.9f),
            BorderColor = new Color(0f, 0.5f, 0.5f, 0.2f),
            BorderWidthBottom = 1
        };
        topBg.AddThemeStyleboxOverride("panel", topStyle);
        AddChild(topBg);
        MoveChild(topBg, 2); // behind topbar labels

        _actLabel.AddThemeFontSizeOverride("font_size", 28);
        _actLabel.AddThemeColorOverride("font_color", new Color(0f, 0.95f, 0.95f));

        _floorLabel.AddThemeFontSizeOverride("font_size", 18);
        _floorLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.55f));

        _goldLabel.AddThemeFontSizeOverride("font_size", 20);
        _goldLabel.AddThemeColorOverride("font_color", new Color(1f, 0.82f, 0.15f));

        _hpLabel.AddThemeFontSizeOverride("font_size", 20);

        var deckStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.05f, 0.08f, 0.15f, 0.9f),
            BorderColor = new Color(0f, 0.6f, 0.6f, 0.4f),
            BorderWidthBottom = 1, BorderWidthTop = 1, BorderWidthLeft = 1, BorderWidthRight = 1,
            CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
            CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4,
            ContentMarginLeft = 12, ContentMarginRight = 12,
            ContentMarginTop = 4, ContentMarginBottom = 4
        };
        _deckBtn.AddThemeStyleboxOverride("normal", deckStyle);
        _deckBtn.AddThemeColorOverride("font_color", new Color(0f, 0.85f, 0.85f));
        _deckBtn.AddThemeFontSizeOverride("font_size", 16);
    }

    private void StyleBottomBar()
    {
        _implantInfo.AddThemeFontSizeOverride("font_size", 14);
        _implantInfo.AddThemeColorOverride("font_color", new Color(0.4f, 0.4f, 0.5f));
    }

    // ===== MAP BUILD =====
    private void BuildMap()
    {
        var run = GameManager.Instance.CurrentRun;
        if (run?.CurrentMap == null) return;
        var map = run.CurrentMap;

        foreach (var child in _mapContainer.GetChildren()) child.QueueFree();
        _nodeUIs.Clear();

        int totalRows = map.TotalRows;
        float mapWidth = _mapScroll.Size.X > 100 ? _mapScroll.Size.X : 800f;
        float mapHeight = (totalRows + 2) * RowSpacing + MapPadding * 2;
        _mapContainer.CustomMinimumSize = new Vector2(mapWidth, Mathf.Max(mapHeight, 600));

        for (int i = 0; i < map.Nodes.Count; i++)
        {
            var mapNode = map.Nodes[i];
            var nodesInRow = map.Nodes.Where(n => n.Row == mapNode.Row).ToList();
            int indexInRow = nodesInRow.IndexOf(mapNode);

            float x = mapWidth / 2f;
            if (nodesInRow.Count > 1)
            {
                float totalWidth = (nodesInRow.Count - 1) * 200f;
                x = (mapWidth - totalWidth) / 2f + indexInRow * 200f;
            }
            float y = mapHeight - MapPadding - (mapNode.Row + 1) * RowSpacing;

            _nodeUIs.Add(new MapNodeUI { MapNode = mapNode, Position = new Vector2(x, y) });
        }

        DrawConnections(map);

        foreach (var nodeUI in _nodeUIs)
        {
            var nodeControl = CreateMapNode(nodeUI.MapNode, nodeUI.Position);
            _mapContainer.AddChild(nodeControl);
            nodeUI.Node = nodeControl;
        }

        _actLabel.Text = $"\u7b2c{run.CurrentAct}\u5e55";
        _floorLabel.Text = $"\u5c42\u6570: {run.FloorsCleared}";

        // Animate accessible nodes with pulse
        AnimateAccessibleNodes();
    }

    private void AnimateAccessibleNodes()
    {
        foreach (var nodeUI in _nodeUIs)
        {
            if (IsNodeAccessible(nodeUI.MapNode) && !nodeUI.MapNode.IsVisited && nodeUI.Node != null)
            {
                CyberFx.PulseGlow(nodeUI.Node, Colors.White, 0.7f, 1f, 1.5f);
            }
        }
    }

    private Control CreateMapNode(MapNode mapNode, Vector2 center)
    {
        var roomType = mapNode.Type;
        bool isAccessible = IsNodeAccessible(mapNode);
        bool isVisited = mapNode.IsVisited;
        bool isCurrent = GameManager.Instance.CurrentRun?.CurrentMap?.CurrentNode == mapNode;
        bool isRevealed = mapNode.IsRevealed || isAccessible || isVisited;

        var color = RoomColors.GetValueOrDefault(roomType, new Color(0.5f, 0.5f, 0.5f));

        var container = new VBoxContainer();
        container.Position = new Vector2(center.X - NodeSize / 2f - 8, center.Y - NodeSize / 2f - 4);
        container.AddThemeConstantOverride("separation", 3);

        // Node button
        var btn = new Button();
        btn.CustomMinimumSize = new Vector2(NodeSize, NodeSize);
        btn.Size = new Vector2(NodeSize, NodeSize);
        btn.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        btn.ClipText = false;
        btn.PivotOffset = new Vector2(NodeSize / 2, NodeSize / 2);

        if (isRevealed)
        {
            string icon = RoomIcons.GetValueOrDefault(roomType, "?");
            btn.Text = icon;
            btn.AddThemeFontSizeOverride("font_size", 24);

            float bgAlpha = isVisited ? 0.2f : (isAccessible ? 0.45f : 0.15f);
            float borderWidth = isCurrent ? 3 : (isAccessible ? 2 : 1);
            Color borderColor = isCurrent ? new Color(0f, 1f, 1f)
                : isAccessible ? color : isVisited ? color * 0.3f : color * 0.4f;

            var style = new StyleBoxFlat
            {
                BgColor = new Color(color.R * 0.12f, color.G * 0.12f, color.B * 0.12f, bgAlpha),
                BorderColor = borderColor,
                BorderWidthBottom = (int)borderWidth, BorderWidthTop = (int)borderWidth,
                BorderWidthLeft = (int)borderWidth, BorderWidthRight = (int)borderWidth,
                CornerRadiusBottomLeft = 30, CornerRadiusBottomRight = 30,
                CornerRadiusTopLeft = 30, CornerRadiusTopRight = 30,
                ShadowColor = isCurrent ? new Color(0f, 0.9f, 0.9f, 0.35f)
                    : isAccessible ? new Color(color.R, color.G, color.B, 0.2f)
                    : Colors.Transparent,
                ShadowSize = isCurrent ? 10 : (isAccessible ? 6 : 0)
            };
            btn.AddThemeStyleboxOverride("normal", style);
            btn.AddThemeColorOverride("font_color", isVisited ? color * 0.4f : color);

            if (isAccessible && !isVisited)
            {
                var hoverStyle = (StyleBoxFlat)style.Duplicate();
                hoverStyle.BgColor = new Color(color.R * 0.25f, color.G * 0.25f, color.B * 0.25f, 0.6f);
                hoverStyle.BorderColor = Colors.White;
                hoverStyle.ShadowColor = new Color(color.R, color.G, color.B, 0.4f);
                hoverStyle.ShadowSize = 14;
                btn.AddThemeStyleboxOverride("hover", hoverStyle);
                btn.MouseDefaultCursorShape = CursorShape.PointingHand;

                // Hover scale
                btn.MouseEntered += () =>
                {
                    var tw = btn.CreateTween();
                    tw.TweenProperty(btn, "scale", new Vector2(1.15f, 1.15f), 0.1f)
                        .SetTrans(Tween.TransitionType.Back);
                };
                btn.MouseExited += () =>
                {
                    var tw = btn.CreateTween();
                    tw.TweenProperty(btn, "scale", Vector2.One, 0.08f);
                };

                btn.Pressed += () => OnMapNodeClicked(mapNode);
            }
            else
            {
                btn.Disabled = true;
                btn.AddThemeColorOverride("font_disabled_color", isVisited ? color * 0.35f : color * 0.6f);
                btn.AddThemeStyleboxOverride("disabled", style);
            }
        }
        else
        {
            btn.Text = "?";
            btn.Disabled = true;
            btn.AddThemeFontSizeOverride("font_size", 18);
            var hiddenStyle = new StyleBoxFlat
            {
                BgColor = new Color(0.04f, 0.04f, 0.08f, 0.4f),
                BorderColor = new Color(0.1f, 0.1f, 0.15f, 0.3f),
                BorderWidthBottom = 1, BorderWidthTop = 1,
                BorderWidthLeft = 1, BorderWidthRight = 1,
                CornerRadiusBottomLeft = 30, CornerRadiusBottomRight = 30,
                CornerRadiusTopLeft = 30, CornerRadiusTopRight = 30
            };
            btn.AddThemeStyleboxOverride("normal", hiddenStyle);
            btn.AddThemeStyleboxOverride("disabled", hiddenStyle);
            btn.AddThemeColorOverride("font_disabled_color", new Color(0.15f, 0.15f, 0.2f, 0.4f));
        }

        container.AddChild(btn);

        if (isRevealed)
        {
            var typeLabel = new Label
            {
                Text = RoomLabels.GetValueOrDefault(roomType, ""),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            typeLabel.AddThemeFontSizeOverride("font_size", 10);
            typeLabel.AddThemeColorOverride("font_color",
                isVisited ? new Color(0.2f, 0.2f, 0.25f) : new Color(color.R, color.G, color.B, 0.5f));
            container.AddChild(typeLabel);
        }

        return container;
    }

    private bool IsNodeAccessible(MapNode node)
    {
        var run = GameManager.Instance.CurrentRun;
        if (run?.CurrentMap == null) return false;
        return run.CurrentMap.CanMoveTo(node.Id);
    }

    // ===== CONNECTIONS =====
    private void DrawConnections(ActMap map)
    {
        for (int i = 0; i < map.Nodes.Count; i++)
        {
            var fromNode = map.Nodes[i];
            var fromUI = _nodeUIs.FirstOrDefault(n => n.MapNode == fromNode);
            if (fromUI == null) continue;

            foreach (int toId in fromNode.ConnectedTo)
            {
                var toNode = map.GetNode(toId);
                if (toNode == null) continue;
                var toUI = _nodeUIs.FirstOrDefault(n => n.MapNode == toNode);
                if (toUI == null) continue;

                bool isPathTaken = fromNode.IsVisited && toNode.IsVisited;
                bool isNextPath = fromNode.IsVisited && IsNodeAccessible(toNode);
                var toColor = RoomColors.GetValueOrDefault(toNode.Type, new Color(0.5f, 0.5f, 0.5f));

                var line = new Line2D();
                line.Antialiased = true;
                var from = fromUI.Position;
                var to = toUI.Position;

                // Smooth bezier curve
                int segments = 14;
                var controlOffset = new Vector2(0, (to.Y - from.Y) * 0.4f);
                for (int s = 0; s <= segments; s++)
                {
                    float t = s / (float)segments;
                    float u = 1 - t;
                    var point = u * u * u * from + 3 * u * u * t * (from + controlOffset)
                        + 3 * u * t * t * (to - controlOffset) + t * t * t * to;
                    line.AddPoint(point);
                }

                if (isPathTaken)
                {
                    line.Width = 3f;
                    line.DefaultColor = new Color(0f, 0.7f, 0.7f, 0.5f);
                }
                else if (isNextPath)
                {
                    line.Width = 2f;
                    line.DefaultColor = new Color(toColor.R, toColor.G, toColor.B, 0.4f);
                }
                else
                {
                    line.Width = 2f;
                    line.DefaultColor = new Color(0.25f, 0.3f, 0.4f, 0.55f);
                }

                _mapContainer.AddChild(line);
            }
        }

        // Add dashed vertical guide line
        float centerX = _mapContainer.CustomMinimumSize.X / 2f;
        var guide = new Line2D();
        guide.Antialiased = true;
        guide.Width = 0.5f;
        guide.DefaultColor = new Color(0f, 0.4f, 0.5f, 0.06f);
        float mapH = _mapContainer.CustomMinimumSize.Y;
        for (float y = 0; y < mapH; y += 8)
        {
            if ((int)(y / 8) % 2 == 0)
            {
                guide.AddPoint(new Vector2(centerX, y));
                guide.AddPoint(new Vector2(centerX, y + 4));
            }
        }
        _mapContainer.AddChild(guide);
    }

    private void ScrollToCurrentNode()
    {
        var run = GameManager.Instance.CurrentRun;
        var currentNode = run?.CurrentMap?.CurrentNode;
        if (currentNode == null)
        {
            // No current node — find first accessible or scroll to bottom (start)
            var firstAccessible = _nodeUIs.FirstOrDefault(n => IsNodeAccessible(n.MapNode));
            if (firstAccessible != null)
            {
                float scrollY = firstAccessible.Position.Y - _mapScroll.Size.Y / 2;
                CallDeferred(MethodName.SetScrollDeferred, Mathf.Max(0, scrollY));
            }
            else
            {
                // Scroll to bottom (start area)
                float maxScroll = _mapContainer.CustomMinimumSize.Y - _mapScroll.Size.Y;
                CallDeferred(MethodName.SetScrollDeferred, Mathf.Max(0, maxScroll));
            }
            return;
        }
        var currentUI = _nodeUIs.FirstOrDefault(n => n.MapNode == currentNode);
        if (currentUI == null) return;
        float sy = currentUI.Position.Y - _mapScroll.Size.Y / 2;
        CallDeferred(MethodName.SetScrollDeferred, Mathf.Max(0, sy));
    }

    private void SetScrollDeferred(float scrollY)
    {
        _mapScroll.ScrollVertical = (int)scrollY;
    }

    // ===== ACTIONS =====
    private void OnMapNodeClicked(MapNode node)
    {
        AudioManager.Instance?.PlaySfx(AudioManager.SfxPaths.MapNodeSelect);
        var run = GameManager.Instance.CurrentRun;
        if (run?.CurrentMap == null) return;
        run.CurrentMap.MoveTo(node.Id);

        switch (node.Type)
        {
            case RoomType.Combat:
            case RoomType.EliteCombat:
            case RoomType.Boss:
                SceneManager.Instance.ChangeScene(SceneManager.Scenes.Combat);
                break;
            case RoomType.RestSite:
                SceneManager.Instance.ChangeScene(SceneManager.Scenes.Rest);
                break;
            case RoomType.Shop:
                SceneManager.Instance.ChangeScene(SceneManager.Scenes.Shop);
                break;
            case RoomType.Event:
                SceneManager.Instance.ChangeScene(SceneManager.Scenes.Event);
                break;
            case RoomType.Treasure:
                SceneManager.Instance.ChangeScene(SceneManager.Scenes.Reward);
                break;
            default:
                GD.Print($"Unhandled room type: {node.Type}");
                break;
        }
    }

    private void UpdatePlayerInfo()
    {
        var run = GameManager.Instance.CurrentRun;
        if (run == null) return;

        _goldLabel.Text = $"\ud83d\udcb0 {run.Gold}";

        float pct = (float)run.Player.CurrentHp / run.Player.MaxHp;
        Color hpCol = pct > 0.5f ? new Color(0.15f, 0.8f, 0.3f) : pct > 0.25f ? new Color(0.9f, 0.7f, 0.15f) : new Color(0.9f, 0.2f, 0.2f);
        _hpLabel.Text = $"\u2764 {run.Player.CurrentHp}/{run.Player.MaxHp}";
        _hpLabel.AddThemeColorOverride("font_color", hpCol);

        var implants = run.Implants.GetAllEquipped();
        _implantInfo.Text = implants.Count > 0
            ? $"\u690d\u5165\u4f53: {string.Join(", ", implants.Select(i => i.Data.Name))}"
            : "\u690d\u5165\u4f53: \u65e0";
    }

    private class MapNodeUI
    {
        public MapNode MapNode = null!;
        public Control Node = null!;
        public Vector2 Position;
    }
}