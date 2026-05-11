using Godot;
using System.IO;
using RogueCardGame.Core.Cards;

namespace RogueCardGame;

/// <summary>
/// Shared builder for high-contrast cyberpunk card visuals used across combat, rewards, rest, and shop.
/// </summary>
public static class CyberCardFactory
{
    private const string CardArtRoot = "res://resources/textures/cards/art";
    private static readonly string[] CardArtExtensions = [".png", ".webp", ".jpg", ".jpeg", ".svg"];

    internal sealed class ClassSignatureFx
    {
        public CardClass CardClass { get; }
        public Control Root { get; }
        public Control[] Elements { get; }

        public ClassSignatureFx(CardClass cardClass, Control root, params Control[] elements)
        {
            CardClass = cardClass;
            Root = root;
            Elements = elements;
        }
    }

    public sealed class CardVisual
    {
        public PanelContainer Root { get; }
        public StyleBoxFlat Style { get; }
        public Color Accent { get; }
        public Control ArtStage { get; }
        public ColorRect ScanBeam { get; }
        public ColorRect GlitchBarPrimary { get; }
        public ColorRect GlitchBarSecondary { get; }
        public Control? RareOverlay { get; }
        public Control? SelectionOverlay { get; }
        public ColorRect? SelectionEnergyFlow { get; }
        internal ClassSignatureFx? ClassSignature { get; }

        internal CardVisual(
            PanelContainer root,
            StyleBoxFlat style,
            Color accent,
            Control artStage,
            ColorRect scanBeam,
            ColorRect glitchBarPrimary,
            ColorRect glitchBarSecondary,
            Control? rareOverlay,
            Control? selectionOverlay,
            ColorRect? selectionEnergyFlow,
            ClassSignatureFx? classSignature)
        {
            Root = root;
            Style = style;
            Accent = accent;
            ArtStage = artStage;
            ScanBeam = scanBeam;
            GlitchBarPrimary = glitchBarPrimary;
            GlitchBarSecondary = glitchBarSecondary;
            RareOverlay = rareOverlay;
            SelectionOverlay = selectionOverlay;
            SelectionEnergyFlow = selectionEnergyFlow;
            ClassSignature = classSignature;
        }
    }

    private static readonly Dictionary<string, Texture2D> TextureCache = new();

    public static void ClearTextureCache()
    {
        TextureCache.Clear();
    }

    public static CardVisual CreateGameplayCard(
        Card card,
        Vector2 minSize,
        bool compact = false,
        bool dimmed = false,
        string? footer = null,
        bool showDescription = true,
        bool selected = false,
        bool minimalCompactStyle = false,
        bool opaqueCompactStyle = false,
        bool neutralCompactStyle = false,
        bool debugLayerMarkers = false)
    {
        return CreateGameplayCard(
            card.Data,
            minSize,
            compact,
            dimmed,
            footer,
            showDescription,
            card.ActiveDescription,
            card.EffectiveCost,
            GetUpgradeBadge(card),
            selected,
            minimalCompactStyle,
            opaqueCompactStyle,
            neutralCompactStyle,
            debugLayerMarkers);
    }

    public static CardVisual CreateGameplayCard(
        CardData cardData,
        Vector2 minSize,
        bool compact = false,
        bool dimmed = false,
        string? footer = null,
        bool showDescription = true,
        string? descriptionOverride = null,
        int? costOverride = null,
        string? stateBadge = null,
        bool selected = false,
        bool minimalCompactStyle = false,
        bool opaqueCompactStyle = false,
        bool neutralCompactStyle = false,
        bool debugLayerMarkers = false)
    {
        string description = descriptionOverride ?? cardData.Description;
        int cost = costOverride ?? cardData.Cost;
        bool useCompactMinimalStage = compact && minimalCompactStyle;
        bool useOpaqueCompactStyle = compact && opaqueCompactStyle && !useCompactMinimalStage;
        bool useNeutralCompactStyle = compact && neutralCompactStyle && !useCompactMinimalStage && !useOpaqueCompactStyle;
        bool useSharedCompactBackdrop = compact && !useCompactMinimalStage && !useOpaqueCompactStyle && !useNeutralCompactStyle;
        bool useNarrowCompactStyle = useNeutralCompactStyle || (useSharedCompactBackdrop && minSize.X <= 140f);

        var typeAccent = GetTypeAccent(cardData.Type);
        var classAccent = GetClassAccent(cardData.Class);
        var rarityAccent = GetRarityAccent(cardData.Rarity);

        if (useOpaqueCompactStyle)
        {
            return CreateOpaqueCompactGameplayCard(
                cardData,
                minSize,
                dimmed,
                footer,
                showDescription,
                description,
                cost,
                stateBadge,
                selected,
                classAccent,
                typeAccent,
                rarityAccent,
                debugLayerMarkers);
        }

        var compactFrameAccent = Blend(classAccent, new Color(0.84f, 0.9f, 0.98f), 0.12f);
        var compactDetailAccent = Blend(classAccent, typeAccent, 0.24f);
        var compactClassMarker = Blend(classAccent, new Color(0.88f, 0.92f, 0.98f), 0.18f);
        var compactTypeMarker = Blend(typeAccent, new Color(0.82f, 0.86f, 0.92f), 0.2f);
        // neutralCompact: class-colored accent + type-colored detail, solid dark backgrounds
        var neutralCompactAccent = Blend(classAccent, new Color(0.72f, 0.78f, 0.88f), 0.35f);
        var neutralCompactDetail = Blend(typeAccent, new Color(0.82f, 0.88f, 0.96f), 0.40f);
        var neutralCompactMarker = new Color(classAccent.R, classAccent.G, classAccent.B, 0.08f);
        var narrowCompactBackdropAccent = new Color(0.38f, 0.42f, 0.48f);
        var narrowCompactBackdropDetail = new Color(0.3f, 0.34f, 0.4f);
        var narrowCompactClassMarker = new Color(classAccent.R, classAccent.G, classAccent.B, 0.16f);
        var opaqueCompactBackdropAccent = Blend(classAccent, new Color(0.72f, 0.78f, 0.88f), 0.26f);
        var opaqueCompactBackdropDetail = Blend(typeAccent, new Color(0.64f, 0.7f, 0.82f), 0.22f);
        var opaqueCompactBackdropMarker = new Color(classAccent.R, classAccent.G, classAccent.B, 0.4f);
        var opaqueCompactEdge = Blend(classAccent, typeAccent, 0.34f);
        var opaqueCompactDetail = Blend(typeAccent, new Color(0.88f, 0.92f, 0.98f), 0.18f);
        var compactPanelTint = useNeutralCompactStyle
            ? new Color(0.046f, 0.052f, 0.068f)
            : Blend(
                new Color(0.05f, 0.055f, 0.07f),
                new Color(classAccent.R * 0.4f, classAccent.G * 0.4f, classAccent.B * 0.4f),
                0.24f);
        var compactInfoSurface = useNeutralCompactStyle
            ? new Color(0.052f, 0.058f, 0.078f, 1f)
            : useOpaqueCompactStyle
            ? new Color(0.054f, 0.061f, 0.082f, 1f)
            : new Color(0.026f, 0.033f, 0.046f, dimmed ? 0.96f : 0.98f);
        var compactInfoSurfaceRaised = useNeutralCompactStyle
            ? new Color(0.07f, 0.078f, 0.102f, 1f)
            : useOpaqueCompactStyle
            ? new Color(0.068f, 0.077f, 0.102f, 1f)
            : new Color(0.035f, 0.043f, 0.058f, 0.95f);
        var compactBackdropWash = useNeutralCompactStyle
            ? new Color(0.18f, 0.22f, 0.28f, 0.04f)
            : useOpaqueCompactStyle
            ? new Color(classAccent.R, classAccent.G, classAccent.B, dimmed ? 0.015f : 0.04f)
            : new Color(classAccent.R, classAccent.G, classAccent.B, dimmed ? 0.08f : 0.14f);
        var accent = useCompactMinimalStage
            ? compactFrameAccent
            : useNeutralCompactStyle
                ? neutralCompactAccent
            : useOpaqueCompactStyle
                ? opaqueCompactEdge
                : Blend(classAccent, new Color(0.84f, 0.9f, 0.98f), compact ? 0.08f : 0.05f);
        var detailAccent = useCompactMinimalStage
            ? compactDetailAccent
            : useNeutralCompactStyle
                ? neutralCompactDetail
            : useOpaqueCompactStyle
                ? opaqueCompactDetail
                : Blend(classAccent, typeAccent, compact ? 0.18f : 0.28f);
        float classBackAlpha = useCompactMinimalStage
            ? 0f
            : useNeutralCompactStyle
                ? 0f
            : useOpaqueCompactStyle
                ? 0f
                : dimmed ? (compact ? 0f : 0.08f) : (compact ? 0f : 0.18f);
        float portraitAlpha = useCompactMinimalStage
            ? 0f
            : useNeutralCompactStyle
                ? 0f
            : useOpaqueCompactStyle
                ? 0f
                : dimmed ? (compact ? 0f : 0.1f) : (compact ? 0f : 0.22f);
        Color artTint = Colors.White;
        float artAlpha = useCompactMinimalStage
            ? dimmed ? 0.12f : 0.34f
            : useNeutralCompactStyle
                ? 0f
            : useOpaqueCompactStyle
                ? 0f
                : dimmed ? (compact ? 0.14f : 0.28f) : (compact ? 0.42f : 0.92f);
        Color artScrimColor = useCompactMinimalStage
            ? new Color(0.012f, 0.015f, 0.024f, dimmed ? 0.68f : 0.42f)
            : useNeutralCompactStyle
                ? new Color(0.02f, 0.024f, 0.032f, 0.14f)
            : useOpaqueCompactStyle
                ? new Color(0f, 0f, 0f, 0f)
            : compact
                ? new Color(0.03f, 0.04f, 0.07f, dimmed ? 0.54f : useNarrowCompactStyle ? 0.6f : 0.42f)
                : new Color(0.03f, 0.04f, 0.06f, dimmed ? 0.18f : 0.1f);
        float railWidth = useCompactMinimalStage ? 8f : useNeutralCompactStyle ? 13f : compact ? 17f : 22f;
        float artHeight = useCompactMinimalStage ? minSize.Y * 0.38f : useNeutralCompactStyle ? minSize.Y * 0.34f : useOpaqueCompactStyle ? minSize.Y * 0.32f : compact ? minSize.Y * 0.56f : minSize.Y * 0.62f;
        int baseShadow = compact ? 6 : 10;
        Color panelBgColor = useCompactMinimalStage
            ? compactInfoSurface
            : useNeutralCompactStyle
                ? new Color(0.040f + classAccent.R * 0.042f, 0.046f + classAccent.G * 0.038f, 0.066f + classAccent.B * 0.048f, 1f)
            : useOpaqueCompactStyle
                ? dimmed ? new Color(0.048f, 0.054f, 0.072f, 0.995f) : new Color(0.056f, 0.063f, 0.084f, 1f)
                : dimmed ? new Color(0.05f, 0.05f, 0.07f, 0.88f) : new Color(0.035f, 0.04f, 0.07f, 0.98f);
        Color panelBorderColor = selected
            ? Colors.White
            : useCompactMinimalStage
                ? new Color(accent.R, accent.G, accent.B, dimmed ? 0.18f : 0.28f)
                : useNeutralCompactStyle
                    ? new Color(accent.R, accent.G, accent.B, dimmed ? 0.48f : 0.86f)
                : useOpaqueCompactStyle
                    ? new Color(accent.R, accent.G, accent.B, dimmed ? 0.42f : 0.72f)
                : dimmed ? accent * 0.25f : accent * 0.55f;
        Color panelShadowColor = useCompactMinimalStage
            ? new Color(accent.R, accent.G, accent.B, selected ? 0.18f : dimmed ? 0.04f : 0.08f)
            : useNeutralCompactStyle
                ? new Color(accent.R, accent.G, accent.B, selected ? 0.38f : dimmed ? 0.08f : 0.22f)
            : useOpaqueCompactStyle
                ? new Color(accent.R, accent.G, accent.B, selected ? 0.22f : dimmed ? 0.06f : 0.14f)
            : dimmed ? Colors.Transparent : new Color(accent.R, accent.G, accent.B, selected ? 0.32f : compact ? 0.12f : 0.2f);
        int panelShadowSize = useCompactMinimalStage
            ? selected ? baseShadow + 2 : dimmed ? 1 : 2
            : useNeutralCompactStyle
                ? dimmed ? Mathf.Max(baseShadow - 2, 2) : selected ? baseShadow + 3 : baseShadow + 1
            : useOpaqueCompactStyle
                ? dimmed ? Mathf.Max(baseShadow - 1, 3) : selected ? baseShadow + 4 : baseShadow + 2
            : dimmed ? 0 : selected ? baseShadow + 4 : baseShadow;

        var panel = new PanelContainer
        {
            CustomMinimumSize = minSize,
            Size = minSize,
            ClipContents = true,
            PivotOffset = minSize / 2f
        };

        var panelStyle = new StyleBoxFlat
        {
            BgColor = panelBgColor,
            BorderColor = panelBorderColor,
            BorderWidthBottom = 2,
            BorderWidthTop = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            CornerRadiusBottomLeft = compact ? 8 : 12,
            CornerRadiusBottomRight = compact ? 8 : 12,
            CornerRadiusTopLeft = compact ? 8 : 12,
            CornerRadiusTopRight = compact ? 8 : 12,
            ShadowColor = panelShadowColor,
            ShadowSize = panelShadowSize,
            AntiAliasing = true,
            AntiAliasingSize = 0.6f,
            ContentMarginLeft = 0,
            ContentMarginRight = 0,
            ContentMarginTop = compact ? 8 : 12,
            ContentMarginBottom = 0
        };
        panel.AddThemeStyleboxOverride("panel", panelStyle);

        var frame = new VBoxContainer();
        frame.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        frame.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        frame.AddThemeConstantOverride("separation", useOpaqueCompactStyle || useNeutralCompactStyle ? 0 : compact ? 4 : 6);
        panel.AddChild(frame);

        var artStage = new Control
        {
            CustomMinimumSize = new Vector2(0, artHeight),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            ClipContents = true
        };

        var artBg = new ColorRect
        {
            Color = useCompactMinimalStage
                ? new Color(compactPanelTint.R * 0.72f, compactPanelTint.G * 0.72f, compactPanelTint.B * 0.72f, 1f)
                : useNeutralCompactStyle
                    ? new Color(0.050f + classAccent.R * 0.052f, 0.057f + classAccent.G * 0.046f, 0.080f + classAccent.B * 0.058f, 1f)
                : useOpaqueCompactStyle
                    ? new Color(0.061f, 0.069f, 0.093f, 1f)
                : new Color(0.03f, 0.03f, 0.045f, 1f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        artBg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        artStage.AddChild(artBg);

        if (useCompactMinimalStage)
        {
            var compactWash = new ColorRect
            {
                Color = new Color(compactBackdropWash.R, compactBackdropWash.G, compactBackdropWash.B, dimmed ? 0.04f : 0.08f),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            compactWash.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            compactWash.AnchorLeft = 0.08f;
            compactWash.AnchorTop = 0.05f;
            compactWash.AnchorRight = 0.96f;
            compactWash.AnchorBottom = 0.9f;
            artStage.AddChild(compactWash);
        }
        else if (useOpaqueCompactStyle)
        {
            AddCompactBackdrop(
                artStage,
                minSize,
                artHeight,
                opaqueCompactBackdropAccent,
                opaqueCompactBackdropDetail,
                opaqueCompactBackdropMarker,
                cardData);
        }
        else if (useSharedCompactBackdrop)
        {
            AddCompactBackdrop(
                artStage,
                minSize,
                artHeight,
                useNarrowCompactStyle ? narrowCompactBackdropAccent : accent,
                useNarrowCompactStyle ? narrowCompactBackdropDetail : detailAccent,
                useNarrowCompactStyle ? narrowCompactClassMarker : compactClassMarker,
                cardData);
        }
        else if (useNeutralCompactStyle)
        {
            var portraitPath = GetClassPortraitPath(cardData.Class);
            if (portraitPath.Length > 0)
            {
                var portraitTexture = LoadTexture(portraitPath);
                if (portraitTexture != null)
                {
                    var portrait = new TextureRect
                    {
                        Texture = portraitTexture,
                        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                        StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                        Modulate = new Color(1f, 1f, 1f, dimmed ? 0.06f : 0.18f),
                        MouseFilter = Control.MouseFilterEnum.Ignore
                    };
                    portrait.SetAnchorsPreset(Control.LayoutPreset.FullRect);
                    portrait.AnchorLeft = 0.04f;
                    portrait.AnchorTop = -0.06f;
                    portrait.AnchorRight = 1.04f;
                    portrait.AnchorBottom = 1.08f;
                    artStage.AddChild(portrait);
                }
            }

            var artTexture = LoadTexture(GetCardArtPath(cardData));
            if (artTexture != null)
            {
                var art = new TextureRect
                {
                    Texture = artTexture,
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                    Modulate = new Color(1f, 1f, 1f, dimmed ? 0.08f : 0.22f),
                    MouseFilter = Control.MouseFilterEnum.Ignore
                };
                art.SetAnchorsPreset(Control.LayoutPreset.FullRect);
                art.AnchorLeft = 0.02f;
                art.AnchorTop = -0.02f;
                art.AnchorRight = 1.02f;
                art.AnchorBottom = 1.03f;
                artStage.AddChild(art);
            }
        }
        else
        {
            var classBack = new ColorRect
            {
                Color = new Color(classAccent.R, classAccent.G, classAccent.B, classBackAlpha),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            classBack.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            classBack.AnchorLeft = 0.12f;
            classBack.AnchorTop = 0.03f;
            classBack.AnchorRight = 0.98f;
            classBack.AnchorBottom = 0.96f;
            artStage.AddChild(classBack);

            var portraitPath = GetClassPortraitPath(cardData.Class);
            if (portraitPath.Length > 0)
            {
                var portraitTexture = LoadTexture(portraitPath);
                if (portraitTexture != null)
                {
                    var portrait = new TextureRect
                    {
                        Texture = portraitTexture,
                        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                        Modulate = new Color(classAccent.R, classAccent.G, classAccent.B, portraitAlpha),
                        MouseFilter = Control.MouseFilterEnum.Ignore
                    };
                    portrait.SetAnchorsPreset(Control.LayoutPreset.FullRect);
                    portrait.AnchorLeft = 0.02f;
                    portrait.AnchorTop = 0.03f;
                    portrait.AnchorRight = 1.02f;
                    portrait.AnchorBottom = 1.06f;
                    artStage.AddChild(portrait);
                }
            }
        }

        if (useCompactMinimalStage)
        {
            AddCompactBackdrop(artStage, minSize, artHeight, accent, detailAccent, compactClassMarker, cardData);
        }
        else if (!useOpaqueCompactStyle && !useNarrowCompactStyle)
        {
            var artTexture = LoadTexture(GetCardArtPath(cardData));
            if (artTexture != null)
            {
                var art = new TextureRect
                {
                    Texture = artTexture,
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                    Modulate = new Color(artTint.R, artTint.G, artTint.B, artAlpha),
                    MouseFilter = Control.MouseFilterEnum.Ignore
                };
                art.SetAnchorsPreset(Control.LayoutPreset.FullRect);
                art.AnchorLeft = 0.02f;
                art.AnchorTop = -0.02f;
                art.AnchorRight = 1.02f;
                art.AnchorBottom = 1.03f;
                artStage.AddChild(art);
            }
        }

        var artScrim = new ColorRect
        {
            Color = artScrimColor,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        artScrim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        artStage.AddChild(artScrim);

        ClassSignatureFx? classSignature = compact || useCompactMinimalStage || useOpaqueCompactStyle
            ? null
            : CreateClassSignatureFx(minSize, artHeight, cardData.Class, classAccent, compact, dimmed);
        if (classSignature != null)
            artStage.AddChild(classSignature.Root);

        var vignetteTop = new ColorRect
        {
            Color = useNeutralCompactStyle
                ? new Color(0f, 0f, 0f, 0.04f)
                : useOpaqueCompactStyle ? new Color(0f, 0f, 0f, 0.08f) : new Color(0f, 0f, 0f, 0.22f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        vignetteTop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        vignetteTop.AnchorBottom = 0f;
        vignetteTop.OffsetBottom = compact ? 34f : 42f;
        artStage.AddChild(vignetteTop);

        var vignetteBottom = new ColorRect
        {
            Color = useNeutralCompactStyle
                ? new Color(0f, 0f, 0f, 0.12f)
                : useOpaqueCompactStyle ? new Color(0f, 0f, 0f, 0.16f) : new Color(0f, 0f, 0f, 0.5f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        vignetteBottom.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        vignetteBottom.AnchorTop = 1f;
        vignetteBottom.OffsetTop = compact ? -48f : -64f;
        artStage.AddChild(vignetteBottom);

        var rail = new ColorRect
        {
            Color = useCompactMinimalStage
                ? new Color(accent.R, accent.G, accent.B, dimmed ? 0.22f : 0.34f)
                : useNeutralCompactStyle
                    ? new Color(classAccent.R * 0.45f + 0.06f, classAccent.G * 0.45f + 0.07f, classAccent.B * 0.45f + 0.09f, dimmed ? 0.8f : 1f)
                : useOpaqueCompactStyle
                    ? new Color(classAccent.R, classAccent.G, classAccent.B, dimmed ? 0.48f : 0.74f)
                : accent,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        rail.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        rail.AnchorRight = 0f;
        rail.OffsetRight = railWidth;
        artStage.AddChild(rail);

        var railGlow = new ColorRect
        {
            Color = new Color(0f, 0f, 0f, 0.45f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        railGlow.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        railGlow.AnchorLeft = 0f;
        railGlow.AnchorRight = 0f;
        railGlow.OffsetLeft = railWidth - 3f;
        railGlow.OffsetRight = railWidth;
        artStage.AddChild(railGlow);

        var railLabel = new Label
        {
            Text = ToVerticalText(GetTypeCode(cardData.Type)),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        railLabel.AddThemeFontSizeOverride("font_size", compact ? 10 : 11);
        railLabel.AddThemeColorOverride("font_color", useCompactMinimalStage
            ? new Color(0.9f, 0.95f, 1f, 0.42f)
            : useNeutralCompactStyle
                ? new Color(0.86f, 0.9f, 0.96f, 0.42f)
            : useOpaqueCompactStyle
                ? new Color(0.88f, 0.92f, 0.98f, 0.34f)
            : new Color(0.02f, 0.03f, 0.06f, 0.92f));
        railLabel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        railLabel.AnchorRight = 0f;
        railLabel.OffsetRight = railWidth;
        artStage.AddChild(railLabel);

        var header = new HBoxContainer();
        header.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        header.AnchorBottom = 0f;
        header.OffsetLeft = railWidth + 8f;
        header.OffsetRight = -8f;
        header.OffsetTop = 8f;
        header.OffsetBottom = compact ? 34f : 38f;
        header.Alignment = BoxContainer.AlignmentMode.Begin;
        if (!compact)
            header.AddChild(CreateChip(
                $"{cost}",
                useCompactMinimalStage || useOpaqueCompactStyle || useNeutralCompactStyle ? compactInfoSurfaceRaised : new Color(0f, 0.62f, 0.78f, 0.92f),
                Colors.White,
                13));
        header.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        if (!string.IsNullOrWhiteSpace(stateBadge) && !compact)
            header.AddChild(CreateChip(
                stateBadge!,
                useNeutralCompactStyle ? compactInfoSurfaceRaised : new Color(1f, 1f, 1f, useOpaqueCompactStyle ? 0.06f : 0.1f),
                useNeutralCompactStyle ? detailAccent : Colors.White,
                compact ? 9 : 10));
        header.AddChild(CreateChip(
            GetRarityCode(cardData.Rarity),
            useCompactMinimalStage || useOpaqueCompactStyle || useNeutralCompactStyle ? compactInfoSurfaceRaised : new Color(rarityAccent.R, rarityAccent.G, rarityAccent.B, 0.9f),
            useCompactMinimalStage || useOpaqueCompactStyle || useNeutralCompactStyle ? rarityAccent : new Color(0.03f, 0.03f, 0.05f),
            compact ? 10 : 11));
        artStage.AddChild(header);

        var titlePlate = new PanelContainer();
        titlePlate.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        titlePlate.AnchorTop = 1f;
        titlePlate.AnchorBottom = 1f;
        titlePlate.OffsetLeft = railWidth + 6f;
        titlePlate.OffsetRight = -6f;
        titlePlate.OffsetTop = compact ? (useNeutralCompactStyle ? -42f : -48f) : -60f;
        titlePlate.OffsetBottom = -6f;
        titlePlate.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = useCompactMinimalStage
                ? compactInfoSurfaceRaised
                : useNeutralCompactStyle
                    ? new Color(0.062f + classAccent.R * 0.04f, 0.070f + classAccent.G * 0.036f, 0.094f + classAccent.B * 0.046f, 1f)
                : useOpaqueCompactStyle
                    ? compactInfoSurfaceRaised
                : new Color(0.03f, 0.04f, 0.06f, 0.92f),
            BorderColor = useCompactMinimalStage
                ? new Color(accent.R, accent.G, accent.B, 0.18f)
                : useNeutralCompactStyle
                    ? new Color(accent.R, accent.G, accent.B, 0.38f)
                : useOpaqueCompactStyle
                    ? new Color(accent.R, accent.G, accent.B, 0.28f)
                : new Color(accent.R, accent.G, accent.B, 0.35f),
            BorderWidthLeft = 2,
            CornerRadiusBottomLeft = compact ? 6 : 8,
            CornerRadiusBottomRight = compact ? 6 : 8,
            CornerRadiusTopLeft = compact ? 6 : 8,
            CornerRadiusTopRight = compact ? 6 : 8,
            ContentMarginLeft = compact ? 8 : 10,
            ContentMarginRight = compact ? 8 : 10,
            ContentMarginTop = compact ? 5 : 6,
            ContentMarginBottom = compact ? 5 : 6
        });

        var titleBox = new VBoxContainer();
        titleBox.AddThemeConstantOverride("separation", 1);
        var title = new Label
        {
            Text = cardData.Name,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        title.AddThemeFontSizeOverride("font_size", compact ? (useNeutralCompactStyle ? 15 : 14) : 19);
        title.AddThemeColorOverride("font_color", Colors.White);
        titleBox.AddChild(title);

        string subtitleText = !string.IsNullOrWhiteSpace(cardData.NameEn)
            ? cardData.NameEn.ToUpperInvariant()
            : GetTypeLabel(cardData.Type).ToUpperInvariant();
        var subtitle = new Label
        {
            Text = subtitleText,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        subtitle.AddThemeFontSizeOverride("font_size", compact ? (useNeutralCompactStyle ? 8 : 7) : 10);
        subtitle.AddThemeColorOverride("font_color", useCompactMinimalStage
            ? new Color(detailAccent.R, detailAccent.G, detailAccent.B, 0.56f)
            : useNeutralCompactStyle
                ? new Color(detailAccent.R, detailAccent.G, detailAccent.B, 0.78f)
            : useOpaqueCompactStyle
                ? new Color(detailAccent.R, detailAccent.G, detailAccent.B, 0.72f)
            : new Color(detailAccent.R, detailAccent.G, detailAccent.B, 0.7f));
        titleBox.AddChild(subtitle);
        titlePlate.AddChild(titleBox);
        artStage.AddChild(titlePlate);

        var artBottomAccent = new ColorRect
        {
            Color = useCompactMinimalStage
                ? new Color(detailAccent.R, detailAccent.G, detailAccent.B, 0.16f)
                : useNeutralCompactStyle
                    ? new Color(detailAccent.R, detailAccent.G, detailAccent.B, 0.34f)
                : useOpaqueCompactStyle
                    ? new Color(detailAccent.R, detailAccent.G, detailAccent.B, 0.22f)
                : new Color(detailAccent.R, detailAccent.G, detailAccent.B, 0.68f),
            CustomMinimumSize = new Vector2(0, compact ? 2 : 3),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };

        var scanBeam = CreateScanBeam(minSize, artHeight, useCompactMinimalStage || useOpaqueCompactStyle ? new Color(0.64f, 0.7f, 0.78f) : accent, compact);
        artStage.AddChild(scanBeam);

        var glitchBarPrimary = CreateGlitchBar(minSize.X, artHeight * 0.28f, useCompactMinimalStage || useOpaqueCompactStyle ? new Color(0.4f, 0.44f, 0.5f) : detailAccent, compact, wide: true);
        artStage.AddChild(glitchBarPrimary);

        var glitchBarSecondary = CreateGlitchBar(minSize.X * 0.86f, artHeight * 0.7f, useCompactMinimalStage || useOpaqueCompactStyle ? new Color(0.52f, 0.56f, 0.62f) : rarityAccent, compact, wide: false);
        artStage.AddChild(glitchBarSecondary);

        if (compact && !useNeutralCompactStyle)
        {
            var costOverlay = new HBoxContainer();
            costOverlay.Position = new Vector2(railWidth + 5f, 4f);
            costOverlay.ZIndex = 10;
            costOverlay.MouseFilter = Control.MouseFilterEnum.Ignore;
            costOverlay.AddChild(CreateChip(
                $"{cost}",
                new Color(accent.R, accent.G, accent.B, 0.88f),
                Colors.White,
                11));
            artStage.AddChild(costOverlay);
        }

        frame.AddChild(artStage);
        frame.AddChild(artBottomAccent);

        var contentMargin = new MarginContainer();
        contentMargin.AddThemeConstantOverride("margin_left", compact ? 8 : 12);
        contentMargin.AddThemeConstantOverride("margin_right", compact ? 8 : 12);
        contentMargin.AddThemeConstantOverride("margin_top", useOpaqueCompactStyle || useNeutralCompactStyle ? 4 : compact ? 0 : 2);
        contentMargin.AddThemeConstantOverride("margin_bottom", compact ? 8 : 10);
        contentMargin.SizeFlagsVertical = Control.SizeFlags.ExpandFill;

        var content = new VBoxContainer();
        content.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        content.AddThemeConstantOverride("separation", compact ? 4 : 6);

        var metaRow = new HBoxContainer();
        metaRow.AddThemeConstantOverride("separation", 4);
        if (useNeutralCompactStyle)
            metaRow.AddChild(CreateMiniChip(
                $"{cost}",
                new Color(accent.R, accent.G, accent.B, 0.88f),
                Colors.White,
                9));
        string rangeTag = GetRangeLabel(cardData.Range);
        if (!string.IsNullOrEmpty(rangeTag))
            metaRow.AddChild(CreateMiniChip(
                rangeTag,
                useCompactMinimalStage || useOpaqueCompactStyle || useNeutralCompactStyle
                    ? compactInfoSurfaceRaised
                    : new Color(typeAccent.R, typeAccent.G, typeAccent.B, 0.14f),
                useCompactMinimalStage ? compactTypeMarker : typeAccent,
                compact ? 8 : 9));
        string targetTag = GetTargetLabel(cardData.EffectiveTargetType);
        if (!string.IsNullOrEmpty(targetTag))
            metaRow.AddChild(CreateMiniChip(
                targetTag,
                useCompactMinimalStage || useOpaqueCompactStyle || useNeutralCompactStyle
                    ? compactInfoSurfaceRaised
                    : new Color(0.72f, 0.76f, 0.88f, 0.12f),
                useCompactMinimalStage
                    ? new Color(0.82f, 0.86f, 0.94f)
                    : useNeutralCompactStyle
                        ? new Color(0.72f, 0.78f, 0.88f)
                    : new Color(0.78f, 0.82f, 0.92f),
                compact ? 8 : 9));
        content.AddChild(metaRow);

        if (showDescription)
        {
            var descriptionLabel = new Label
            {
                Text = description,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            descriptionLabel.AddThemeFontSizeOverride("font_size", compact ? (useNeutralCompactStyle ? 9 : 8) : 12);
            descriptionLabel.AddThemeColorOverride("font_color", dimmed
                ? new Color(0.48f, 0.5f, 0.56f)
                : useNeutralCompactStyle
                    ? new Color(0.88f, 0.91f, 0.95f)
                : useOpaqueCompactStyle
                    ? new Color(0.86f, 0.89f, 0.94f)
                    : new Color(0.86f, 0.88f, 0.92f));
            content.AddChild(descriptionLabel);
        }
        else
        {
            content.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill });
        }

        if (!string.IsNullOrWhiteSpace(footer))
            content.AddChild(CreateFooterChip(footer!, detailAccent, compact));

        contentMargin.AddChild(content);
        frame.AddChild(contentMargin);

        AddCornerMarks(panel, accent, compact);
        Control? rareOverlay = null;
        if (!dimmed && cardData.Rarity == CardRarity.Rare)
            rareOverlay = AddRareBorderOverlay(panel, accent, compact, minSize);

        Control? selectionOverlay = null;
        ColorRect? selectionEnergyFlow = null;
        if (selected)
            (selectionOverlay, selectionEnergyFlow) = AddSelectionOverlay(panel, accent, compact, minSize);

        var visual = new CardVisual(panel, panelStyle, accent, artStage, scanBeam, glitchBarPrimary, glitchBarSecondary, rareOverlay, selectionOverlay, selectionEnergyFlow, classSignature);
        StartClassSignatureAmbient(visual, compact);
        return visual;
    }

    private static CardVisual CreateOpaqueCompactGameplayCard(
        CardData cardData,
        Vector2 minSize,
        bool dimmed,
        string? footer,
        bool showDescription,
        string description,
        int cost,
        string? stateBadge,
        bool selected,
        Color classAccent,
        Color typeAccent,
        Color rarityAccent,
        bool debugLayerMarkers)
    {
        var accent = new Color(0.3f, 0.4f, 0.52f);
        var detailAccent = new Color(0.7f, 0.78f, 0.9f);
        var infoSurface = new Color(0.05f, 0.057f, 0.076f, 1f);
        var raisedSurface = new Color(0.064f, 0.074f, 0.098f, 1f);
        float artHeight = minSize.Y * 0.31f;
        float railWidth = 12f;

        var panel = new PanelContainer
        {
            CustomMinimumSize = minSize,
            Size = minSize,
            ClipContents = true,
            PivotOffset = minSize / 2f
        };

        var panelStyle = new StyleBoxFlat
        {
            BgColor = infoSurface,
            BorderColor = selected ? Colors.White : new Color(accent.R, accent.G, accent.B, dimmed ? 0.42f : 0.68f),
            BorderWidthBottom = 2,
            BorderWidthTop = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            ShadowColor = new Color(0f, 0f, 0f, selected ? 0.26f : dimmed ? 0.14f : 0.2f),
            ShadowSize = dimmed ? 5 : selected ? 10 : 8,
            ContentMarginLeft = 0,
            ContentMarginRight = 0,
            ContentMarginTop = 8,
            ContentMarginBottom = 0
        };
        panel.AddThemeStyleboxOverride("panel", panelStyle);

        var frame = new VBoxContainer();
        frame.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        frame.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        frame.AddThemeConstantOverride("separation", 0);
        panel.AddChild(frame);

        var artStage = new Control
        {
            CustomMinimumSize = new Vector2(0, artHeight),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            ClipContents = true
        };

        var artBg = new ColorRect
        {
            Color = new Color(0.058f, 0.067f, 0.09f, 1f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        artBg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        artStage.AddChild(artBg);

        AddCompactBackdrop(
            artStage,
            minSize,
            artHeight,
            new Color(0.46f, 0.52f, 0.62f),
            new Color(0.36f, 0.42f, 0.52f),
            new Color(classAccent.R, classAccent.G, classAccent.B, 0.24f),
            cardData);

        var rail = new ColorRect
        {
            Color = new Color(classAccent.R, classAccent.G, classAccent.B, dimmed ? 0.3f : 0.52f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        rail.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        rail.AnchorRight = 0f;
        rail.OffsetRight = railWidth;
        artStage.AddChild(rail);

        var railGlow = new ColorRect
        {
            Color = new Color(0f, 0f, 0f, 0.34f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        railGlow.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        railGlow.AnchorLeft = 0f;
        railGlow.AnchorRight = 0f;
        railGlow.OffsetLeft = railWidth - 2f;
        railGlow.OffsetRight = railWidth;
        artStage.AddChild(railGlow);

        var railLabel = new Label
        {
            Text = ToVerticalText(GetTypeCode(cardData.Type)),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        railLabel.AddThemeFontSizeOverride("font_size", 10);
        railLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.94f, 0.99f, 0.3f));
        railLabel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        railLabel.AnchorRight = 0f;
        railLabel.OffsetRight = railWidth;
        artStage.AddChild(railLabel);

        var header = new HBoxContainer();
        header.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        header.AnchorBottom = 0f;
        header.OffsetLeft = railWidth + 8f;
        header.OffsetRight = -8f;
        header.OffsetTop = 8f;
        header.OffsetBottom = 32f;
        header.Alignment = BoxContainer.AlignmentMode.Begin;
        header.AddChild(CreateChip(
            $"{cost}",
            raisedSurface,
            Colors.White,
            11));
        header.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        if (!string.IsNullOrWhiteSpace(stateBadge))
            header.AddChild(CreateChip(stateBadge!, new Color(1f, 1f, 1f, 0.06f), new Color(0.88f, 0.92f, 0.98f), 8));
        header.AddChild(CreateChip(
            GetRarityCode(cardData.Rarity),
            raisedSurface,
            rarityAccent,
            9));
        artStage.AddChild(header);

        var titlePlate = new PanelContainer();
        titlePlate.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        titlePlate.AnchorTop = 1f;
        titlePlate.AnchorBottom = 1f;
        titlePlate.OffsetLeft = railWidth + 6f;
        titlePlate.OffsetRight = -6f;
        titlePlate.OffsetTop = -44f;
        titlePlate.OffsetBottom = -6f;
        titlePlate.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = raisedSurface,
            BorderColor = new Color(accent.R, accent.G, accent.B, 0.24f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
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

        var titleBox = new VBoxContainer();
        titleBox.AddThemeConstantOverride("separation", 1);
        var title = new Label
        {
            Text = cardData.Name,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        title.AddThemeFontSizeOverride("font_size", 14);
        title.AddThemeColorOverride("font_color", Colors.White);
        titleBox.AddChild(title);

        string subtitleText = !string.IsNullOrWhiteSpace(cardData.NameEn)
            ? cardData.NameEn.ToUpperInvariant()
            : GetTypeLabel(cardData.Type).ToUpperInvariant();
        var subtitle = new Label
        {
            Text = subtitleText,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        subtitle.AddThemeFontSizeOverride("font_size", 7);
        subtitle.AddThemeColorOverride("font_color", new Color(detailAccent.R, detailAccent.G, detailAccent.B, 0.72f));
        titleBox.AddChild(subtitle);
        titlePlate.AddChild(titleBox);
        artStage.AddChild(titlePlate);

        var scanBeam = CreateScanBeam(minSize, artHeight, new Color(0.72f, 0.78f, 0.88f), compact: true);
        artStage.AddChild(scanBeam);

        var glitchBarPrimary = CreateGlitchBar(minSize.X, artHeight * 0.28f, new Color(detailAccent.R, detailAccent.G, detailAccent.B, 0.8f), compact: true, wide: true);
        artStage.AddChild(glitchBarPrimary);

        var glitchBarSecondary = CreateGlitchBar(minSize.X * 0.86f, artHeight * 0.7f, new Color(rarityAccent.R, rarityAccent.G, rarityAccent.B, 0.78f), compact: true, wide: false);
        artStage.AddChild(glitchBarSecondary);

        var artBottomAccent = new ColorRect
        {
            Color = new Color(detailAccent.R, detailAccent.G, detailAccent.B, 0.22f),
            CustomMinimumSize = new Vector2(0, 2),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };

        frame.AddChild(artStage);
        frame.AddChild(artBottomAccent);

        var contentMargin = new MarginContainer();
        contentMargin.AddThemeConstantOverride("margin_left", 8);
        contentMargin.AddThemeConstantOverride("margin_right", 8);
        contentMargin.AddThemeConstantOverride("margin_top", 4);
        contentMargin.AddThemeConstantOverride("margin_bottom", 8);
        contentMargin.SizeFlagsVertical = Control.SizeFlags.ExpandFill;

        var content = new VBoxContainer();
        content.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        content.AddThemeConstantOverride("separation", 4);

        var metaRow = new HBoxContainer();
        metaRow.AddThemeConstantOverride("separation", 4);
        string rangeTag = GetRangeLabel(cardData.Range);
        if (!string.IsNullOrEmpty(rangeTag))
            metaRow.AddChild(CreateMiniChip(rangeTag, raisedSurface, detailAccent, 8));
        string targetTag = GetTargetLabel(cardData.EffectiveTargetType);
        if (!string.IsNullOrEmpty(targetTag))
            metaRow.AddChild(CreateMiniChip(targetTag, raisedSurface, new Color(0.82f, 0.86f, 0.94f), 8));
        content.AddChild(metaRow);

        if (showDescription)
        {
            var descriptionLabel = new Label
            {
                Text = description,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            descriptionLabel.AddThemeFontSizeOverride("font_size", 8);
            descriptionLabel.AddThemeColorOverride("font_color", dimmed
                ? new Color(0.78f, 0.82f, 0.9f)
                : new Color(0.88f, 0.91f, 0.96f));
            content.AddChild(descriptionLabel);
        }
        else
        {
            content.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill });
        }

        if (!string.IsNullOrWhiteSpace(footer))
            content.AddChild(CreateFooterChip(footer!, detailAccent, compact: true));

        contentMargin.AddChild(content);
        frame.AddChild(contentMargin);

        AddCornerMarks(panel, accent, compact: true);
        Control? rareOverlay = null;
        if (!dimmed && cardData.Rarity == CardRarity.Rare)
            rareOverlay = AddRareBorderOverlay(panel, accent, compact: true, minSize);

        Control? selectionOverlay = null;
        ColorRect? selectionEnergyFlow = null;
        if (selected)
            (selectionOverlay, selectionEnergyFlow) = AddSelectionOverlay(panel, accent, compact: true, minSize);

        if (debugLayerMarkers)
        {
            var debugLayers = new List<(string label, Color color)>
            {
                ("01 artBg", new Color(0.36f, 0.58f, 0.96f)),
                ("02 backdrop", new Color(0.56f, 0.7f, 0.96f)),
                ("03 rail", classAccent),
                ("04 railGlow", new Color(0.72f, 0.74f, 0.82f)),
                ("05 railText", new Color(0.92f, 0.96f, 1f)),
                ("06 header", new Color(0.3f, 0.9f, 0.96f)),
                ("07 title", new Color(1f, 0.78f, 0.34f)),
                ("08 scanBeam", new Color(0.86f, 0.93f, 1f)),
                ("09 glitchA", detailAccent),
                ("10 glitchB", rarityAccent),
                ("11 artBottom", detailAccent),
                ("12 content", new Color(0.78f, 0.82f, 0.9f)),
                ("13 meta", new Color(0.58f, 1f, 0.72f))
            };

            debugLayers.Add(showDescription
                ? ("14 desc", new Color(1f, 0.92f, 0.58f))
                : ("14 filler", new Color(0.64f, 0.66f, 0.72f)));

            if (!string.IsNullOrWhiteSpace(footer))
                debugLayers.Add(("15 footer", new Color(1f, 0.54f, 0.54f)));

            debugLayers.Add(("16 corners", accent));

            if (rareOverlay != null)
                debugLayers.Add(("17 rare", new Color(1f, 0.9f, 0.3f)));

            if (selectionOverlay != null)
                debugLayers.Add(("18 select", new Color(0.76f, 0.96f, 1f)));

            AddDebugLayerLegend(panel, debugLayers, "combat card");
        }

        return new CardVisual(panel, panelStyle, accent, artStage, scanBeam, glitchBarPrimary, glitchBarSecondary, rareOverlay, selectionOverlay, selectionEnergyFlow, classSignature: null);
    }

    public static void AttachHover(CardVisual visual, float scale = 1.04f, int hoverShadow = 14)
    {
        var panel = visual.Root;
        var style = visual.Style;
        var accent = visual.Accent;
        int baseShadow = style.ShadowSize;
        Color baseBorder = style.BorderColor;
        Color baseShadowColor = style.ShadowColor;

        panel.MouseEntered += () =>
        {
            var tw = panel.CreateTween();
            tw.TweenProperty(panel, "scale", new Vector2(scale, scale), 0.14f)
                .SetTrans(Tween.TransitionType.Back)
                .SetEase(Tween.EaseType.Out);
            style.BorderColor = Colors.White;
            style.ShadowColor = new Color(accent.R, accent.G, accent.B, 0.28f);
            style.ShadowSize = hoverShadow;

            var scan = panel.CreateTween();
            visual.ScanBeam.Position = new Vector2(-visual.ScanBeam.Size.X * 1.15f, -visual.ScanBeam.Size.Y * 0.08f);
            visual.ScanBeam.Modulate = new Color(1f, 1f, 1f, 0f);
            scan.TweenProperty(visual.ScanBeam, "modulate:a", 0.48f, 0.08f);
            scan.Parallel().TweenProperty(visual.ScanBeam, "position:x", visual.ArtStage.Size.X + visual.ScanBeam.Size.X * 0.15f, 0.38f)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.Out);
            scan.TweenProperty(visual.ScanBeam, "modulate:a", 0f, 0.12f);

            TriggerGlitchBars(panel, visual);
            TriggerClassSignatureBurst(visual, 1f);
        };

        panel.MouseExited += () =>
        {
            var tw = panel.CreateTween();
            tw.TweenProperty(panel, "scale", Vector2.One, 0.12f)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.Out);
            style.BorderColor = baseBorder;
            style.ShadowColor = baseShadowColor;
            style.ShadowSize = baseShadow;

            visual.ScanBeam.Modulate = new Color(1f, 1f, 1f, 0f);
            visual.GlitchBarPrimary.Position = (Vector2)visual.GlitchBarPrimary.GetMeta("base_position");
            visual.GlitchBarSecondary.Position = (Vector2)visual.GlitchBarSecondary.GetMeta("base_position");
            visual.GlitchBarPrimary.Modulate = new Color(1f, 1f, 1f, 0f);
            visual.GlitchBarSecondary.Modulate = new Color(1f, 1f, 1f, 0f);
            RestoreClassSignatureFx(visual);
        };
    }

    public static void PlayDropReveal(CardVisual visual, bool emphasizeRare)
    {
        var panel = visual.Root;
        panel.Scale = new Vector2(emphasizeRare ? 0.9f : 0.94f, emphasizeRare ? 0.9f : 0.94f);
        panel.Modulate = new Color(1f, 1f, 1f, 0f);

        var entry = panel.CreateTween();
        entry.TweenProperty(panel, "modulate:a", 1f, emphasizeRare ? 0.34f : 0.24f)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
        entry.Parallel().TweenProperty(panel, "scale", Vector2.One, emphasizeRare ? 0.42f : 0.3f)
            .SetTrans(Tween.TransitionType.Back)
            .SetEase(Tween.EaseType.Out);

        if (emphasizeRare)
        {
            visual.Style.BorderColor = Colors.White;
            visual.Style.ShadowColor = new Color(visual.Accent.R, visual.Accent.G, visual.Accent.B, 0.4f);
            visual.Style.ShadowSize += 6;

            var flash = new ColorRect
            {
                Color = new Color(1f, 0.96f, 0.65f, 0.32f),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            flash.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            visual.ArtStage.AddChild(flash);

            var flashTween = panel.CreateTween();
            flashTween.TweenProperty(flash, "modulate:a", 0f, 0.32f)
                .From(0.95f)
                .SetTrans(Tween.TransitionType.Cubic);
            flashTween.TweenCallback(Callable.From(() => flash.QueueFree()));

            var overshoot = panel.CreateTween();
            overshoot.TweenInterval(0.1f);
            overshoot.TweenProperty(panel, "rotation_degrees", -1.4f, 0.08f);
            overshoot.TweenProperty(panel, "rotation_degrees", 1.2f, 0.09f);
            overshoot.TweenProperty(panel, "rotation_degrees", 0f, 0.11f);

            var scan = panel.CreateTween();
            visual.ScanBeam.Position = new Vector2(-visual.ScanBeam.Size.X * 1.15f, -visual.ScanBeam.Size.Y * 0.08f);
            visual.ScanBeam.Modulate = new Color(1f, 1f, 1f, 0f);
            scan.TweenProperty(visual.ScanBeam, "modulate:a", 0.62f, 0.07f);
            scan.Parallel().TweenProperty(visual.ScanBeam, "position:x", visual.ArtStage.Size.X + visual.ScanBeam.Size.X * 0.18f, 0.52f)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.Out);
            scan.TweenProperty(visual.ScanBeam, "modulate:a", 0f, 0.12f);

            TriggerGlitchBars(panel, visual);
        }

        TriggerClassSignatureBurst(visual, emphasizeRare ? 1.28f : 0.88f);
    }

    public static void PlaySelectionConfirm(CardVisual visual)
    {
        var panel = visual.Root;
        var pulse = panel.CreateTween();
        pulse.TweenProperty(panel, "scale", new Vector2(1.08f, 1.08f), 0.12f)
            .SetTrans(Tween.TransitionType.Back)
            .SetEase(Tween.EaseType.Out);
        pulse.TweenProperty(panel, "scale", new Vector2(1.04f, 1.04f), 0.18f)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);

        if (visual.SelectionEnergyFlow != null)
        {
            visual.SelectionEnergyFlow.Modulate = Colors.White;
            var runner = panel.CreateTween();
            runner.TweenProperty(visual.SelectionEnergyFlow, "position:x", panel.Size.X + 18f, 0.42f)
                .From(-visual.SelectionEnergyFlow.Size.X - 18f)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.Out);
        }

        TriggerClassSignatureBurst(visual, 1.45f);
    }

    private static void StartClassSignatureAmbient(CardVisual visual, bool compact)
    {
        if (visual.ClassSignature == null)
            return;

        if (compact)
            return;

        void Start()
        {
            visual.Root.TreeEntered -= Start;
            if (visual.Root.HasMeta("class_signature_started"))
                return;

            visual.Root.SetMeta("class_signature_started", true);
            var signature = visual.ClassSignature;
            if (signature == null)
                return;

            switch (signature.CardClass)
            {
                case CardClass.Vanguard:
                    StartAlphaLoop(visual.Root, signature.Root, 0.9f, 1.2f, compact ? 0.42f : 0.54f);
                    StartAlphaLoop(visual.Root, signature.Elements[0], 0.76f, 1.28f, compact ? 0.22f : 0.28f);
                    StartScaleLoop(visual.Root, signature.Elements[1], new Vector2(0.16f, 0.02f), compact ? 0.3f : 0.38f, 0.03f);
                    StartPositionLoop(visual.Root, signature.Elements[2], new Vector2(0f, compact ? -2f : -3f), compact ? 0.3f : 0.38f, 0.05f);
                    break;
                case CardClass.Psion:
                    StartAlphaLoop(visual.Root, signature.Root, 0.56f, 0.94f, compact ? 1.18f : 1.38f);
                    StartPositionLoop(visual.Root, signature.Root, new Vector2(0f, compact ? -2f : -4f), compact ? 1.22f : 1.44f, 0.12f);
                    StartScaleLoop(visual.Root, signature.Elements[0], new Vector2(0.12f, 0.12f), compact ? 1.04f : 1.2f, 0.08f);
                    StartScaleLoop(visual.Root, signature.Elements[1], new Vector2(0.08f, 0.08f), compact ? 0.88f : 1.04f, 0.05f);
                    StartScaleLoop(visual.Root, signature.Elements[2], new Vector2(0.16f, 0.16f), compact ? 1.14f : 1.34f, 0.08f);
                    break;
                case CardClass.Netrunner:
                    StartAlphaLoop(visual.Root, signature.Root, 0.42f, 1.22f, compact ? 0.18f : 0.22f);
                    StartPositionLoop(visual.Root, signature.Elements[0], new Vector2(compact ? 6f : 10f, 0f), compact ? 0.12f : 0.16f, 0.03f);
                    StartPositionLoop(visual.Root, signature.Elements[1], new Vector2(compact ? -4f : -7f, 0f), compact ? 0.14f : 0.18f, 0.03f);
                    StartPositionLoop(visual.Root, signature.Elements[2], new Vector2(compact ? 8f : 12f, 0f), compact ? 0.1f : 0.14f, 0.05f);
                    StartAlphaLoop(visual.Root, signature.Elements[1], 0.36f, 1.34f, compact ? 0.12f : 0.16f);
                    StartAlphaLoop(visual.Root, signature.Elements[3], 0.42f, 1.26f, compact ? 0.16f : 0.2f);
                    StartScaleLoop(visual.Root, signature.Elements[3], new Vector2(0.12f, 0.18f), compact ? 0.14f : 0.18f, 0.04f);
                    break;
                case CardClass.Symbiote:
                    StartAlphaLoop(visual.Root, signature.Root, 0.86f, 1.14f, compact ? 1.04f : 1.18f);
                    StartScaleLoop(visual.Root, signature.Root, new Vector2(0.02f, 0.05f), compact ? 1.18f : 1.34f, 0.1f);
                    StartScaleLoop(visual.Root, signature.Elements[0], new Vector2(0.12f, 0.18f), compact ? 0.94f : 1.08f, 0.08f);
                    StartScaleLoop(visual.Root, signature.Elements[1], new Vector2(0.08f, 0.12f), compact ? 0.82f : 0.96f, 0.12f);
                    StartPositionLoop(visual.Root, signature.Elements[1], new Vector2(compact ? 1f : 2f, compact ? -2f : -3f), compact ? 0.92f : 1.08f, 0.06f);
                    StartPositionLoop(visual.Root, signature.Elements[2], new Vector2(compact ? -1f : -2f, compact ? -2f : -4f), compact ? 1.08f : 1.24f, 0.08f);
                    break;
            }
        }

        if (visual.Root.IsInsideTree())
            Start();
        else
            visual.Root.TreeEntered += Start;
    }

    private static void TriggerClassSignatureBurst(CardVisual visual, float intensity)
    {
        var signature = visual.ClassSignature;
        if (signature == null)
            return;

        switch (signature.CardClass)
        {
            case CardClass.Vanguard:
                for (int i = 0; i < signature.Elements.Length; i++)
                {
                    var band = signature.Elements[i];
                    RestoreFxElement(band);
                    Vector2 basePosition = GetBasePosition(band);
                    Vector2 baseScale = GetBaseScale(band);
                    Color baseModulate = GetBaseModulate(band);
                    float rise = (7f + i * 4f) * intensity;
                    float stretch = 1f + (0.14f + i * 0.03f) * intensity;
                    var tween = visual.Root.CreateTween();
                    tween.TweenInterval(i * 0.03f);
                    tween.TweenProperty(band, "modulate:a", Mathf.Clamp(baseModulate.A + 0.36f * intensity, 0f, 1f), 0.05f);
                    tween.Parallel().TweenProperty(band, "position:y", basePosition.Y - rise, 0.1f)
                        .SetTrans(Tween.TransitionType.Cubic)
                        .SetEase(Tween.EaseType.Out);
                    tween.Parallel().TweenProperty(band, "scale", new Vector2(baseScale.X * stretch, baseScale.Y), 0.1f)
                        .SetTrans(Tween.TransitionType.Cubic)
                        .SetEase(Tween.EaseType.Out);
                    tween.TweenProperty(band, "position:y", basePosition.Y, 0.2f)
                        .SetTrans(Tween.TransitionType.Cubic)
                        .SetEase(Tween.EaseType.Out);
                    tween.Parallel().TweenProperty(band, "scale", baseScale, 0.2f)
                        .SetTrans(Tween.TransitionType.Cubic)
                        .SetEase(Tween.EaseType.Out);
                    tween.Parallel().TweenProperty(band, "modulate:a", baseModulate.A, 0.16f);
                }
                break;

            case CardClass.Psion:
                for (int i = 0; i < 2; i++)
                {
                    var ring = signature.Elements[i];
                    RestoreFxElement(ring);
                    Vector2 baseScale = GetBaseScale(ring);
                    Color baseModulate = GetBaseModulate(ring);
                    float spread = 1f + (0.22f + i * 0.12f) * intensity;
                    var tween = visual.Root.CreateTween();
                    tween.TweenInterval(i * 0.04f);
                    tween.TweenProperty(ring, "modulate:a", Mathf.Clamp(baseModulate.A + 0.32f * intensity, 0f, 1f), 0.08f);
                    tween.Parallel().TweenProperty(ring, "scale", new Vector2(baseScale.X * spread, baseScale.Y * spread), 0.24f)
                        .SetTrans(Tween.TransitionType.Sine)
                        .SetEase(Tween.EaseType.Out);
                    tween.Parallel().TweenProperty(ring, "position:y", GetBasePosition(ring).Y - (2f + i) * intensity, 0.24f)
                        .SetTrans(Tween.TransitionType.Sine)
                        .SetEase(Tween.EaseType.Out);
                    tween.TweenProperty(ring, "scale", baseScale, 0.34f)
                        .SetTrans(Tween.TransitionType.Cubic)
                        .SetEase(Tween.EaseType.Out);
                    tween.Parallel().TweenProperty(ring, "position:y", GetBasePosition(ring).Y, 0.34f)
                        .SetTrans(Tween.TransitionType.Cubic)
                        .SetEase(Tween.EaseType.Out);
                    tween.Parallel().TweenProperty(ring, "modulate:a", baseModulate.A, 0.28f);
                }

                var core = signature.Elements[2];
                RestoreFxElement(core);
                Vector2 baseCoreScale = GetBaseScale(core);
                Color baseCoreModulate = GetBaseModulate(core);
                var coreTween = visual.Root.CreateTween();
                coreTween.TweenProperty(core, "modulate:a", Mathf.Clamp(baseCoreModulate.A + 0.22f * intensity, 0f, 1f), 0.08f);
                coreTween.Parallel().TweenProperty(core, "scale", new Vector2(baseCoreScale.X, baseCoreScale.Y) * (1f + 0.24f * intensity), 0.18f)
                    .SetTrans(Tween.TransitionType.Sine)
                    .SetEase(Tween.EaseType.Out);
                coreTween.Parallel().TweenProperty(core, "position:y", GetBasePosition(core).Y - 3f * intensity, 0.18f)
                    .SetTrans(Tween.TransitionType.Sine)
                    .SetEase(Tween.EaseType.Out);
                coreTween.TweenProperty(core, "scale", baseCoreScale, 0.24f)
                    .SetTrans(Tween.TransitionType.Cubic)
                    .SetEase(Tween.EaseType.Out);
                coreTween.Parallel().TweenProperty(core, "position:y", GetBasePosition(core).Y, 0.24f)
                    .SetTrans(Tween.TransitionType.Cubic)
                    .SetEase(Tween.EaseType.Out);
                coreTween.Parallel().TweenProperty(core, "modulate:a", baseCoreModulate.A, 0.2f);
                break;

            case CardClass.Netrunner:
                for (int i = 0; i < signature.Elements.Length; i++)
                {
                    var slice = signature.Elements[i];
                    RestoreFxElement(slice);
                    Vector2 basePosition = GetBasePosition(slice);
                    Vector2 baseScale = GetBaseScale(slice);
                    Color baseModulate = GetBaseModulate(slice);
                    float direction = i % 2 == 0 ? 1f : -1f;
                    float offset = (8f + i * 4f) * intensity * direction;
                    float yOffset = (i % 2 == 0 ? -2f : 2f) * intensity;
                    float stretch = 1f + 0.1f * intensity;
                    var tween = visual.Root.CreateTween();
                    tween.TweenInterval(i * 0.016f);
                    tween.TweenProperty(slice, "modulate:a", Mathf.Clamp(baseModulate.A + 0.5f * intensity, 0f, 1f), 0.03f);
                    tween.Parallel().TweenProperty(slice, "position:x", basePosition.X + offset, 0.05f)
                        .SetTrans(Tween.TransitionType.Cubic)
                        .SetEase(Tween.EaseType.Out);
                    tween.Parallel().TweenProperty(slice, "position:y", basePosition.Y + yOffset, 0.05f)
                        .SetTrans(Tween.TransitionType.Cubic)
                        .SetEase(Tween.EaseType.Out);
                    tween.Parallel().TweenProperty(slice, "scale:y", baseScale.Y * stretch, 0.05f)
                        .SetTrans(Tween.TransitionType.Cubic)
                        .SetEase(Tween.EaseType.Out);
                    tween.TweenProperty(slice, "position:x", basePosition.X - offset * 0.52f, 0.06f)
                        .SetTrans(Tween.TransitionType.Cubic)
                        .SetEase(Tween.EaseType.Out);
                    tween.Parallel().TweenProperty(slice, "position:y", basePosition.Y - yOffset * 0.5f, 0.06f)
                        .SetTrans(Tween.TransitionType.Cubic)
                        .SetEase(Tween.EaseType.Out);
                    tween.Parallel().TweenProperty(slice, "scale:y", baseScale.Y, 0.06f)
                        .SetTrans(Tween.TransitionType.Cubic)
                        .SetEase(Tween.EaseType.Out);
                    tween.TweenProperty(slice, "position:x", basePosition.X, 0.04f)
                        .SetTrans(Tween.TransitionType.Cubic)
                        .SetEase(Tween.EaseType.Out);
                    tween.Parallel().TweenProperty(slice, "position:y", basePosition.Y, 0.04f)
                        .SetTrans(Tween.TransitionType.Cubic)
                        .SetEase(Tween.EaseType.Out);
                    tween.Parallel().TweenProperty(slice, "modulate:a", baseModulate.A, 0.06f);
                }
                break;

            case CardClass.Symbiote:
                for (int i = 0; i < signature.Elements.Length; i++)
                {
                    var blob = signature.Elements[i];
                    RestoreFxElement(blob);
                    Vector2 basePosition = GetBasePosition(blob);
                    Vector2 baseScale = GetBaseScale(blob);
                    Color baseModulate = GetBaseModulate(blob);
                    float swell = 1f + (0.16f + i * 0.06f) * intensity;
                    float drift = (4f + i * 2f) * intensity;
                    float sway = (i % 2 == 0 ? -1f : 1f) * (2f + i) * intensity;
                    var tween = visual.Root.CreateTween();
                    tween.TweenInterval(i * 0.03f);
                    tween.TweenProperty(blob, "modulate:a", Mathf.Clamp(baseModulate.A + 0.32f * intensity, 0f, 1f), 0.08f);
                    tween.Parallel().TweenProperty(blob, "scale", new Vector2(baseScale.X * swell, baseScale.Y * swell), 0.24f)
                        .SetTrans(Tween.TransitionType.Sine)
                        .SetEase(Tween.EaseType.Out);
                    tween.Parallel().TweenProperty(blob, "position:x", basePosition.X + sway, 0.24f)
                        .SetTrans(Tween.TransitionType.Sine)
                        .SetEase(Tween.EaseType.Out);
                    tween.Parallel().TweenProperty(blob, "position:y", basePosition.Y - drift, 0.24f)
                        .SetTrans(Tween.TransitionType.Sine)
                        .SetEase(Tween.EaseType.Out);
                    tween.TweenProperty(blob, "scale", baseScale, 0.3f)
                        .SetTrans(Tween.TransitionType.Cubic)
                        .SetEase(Tween.EaseType.Out);
                    tween.Parallel().TweenProperty(blob, "position:x", basePosition.X, 0.3f)
                        .SetTrans(Tween.TransitionType.Cubic)
                        .SetEase(Tween.EaseType.Out);
                    tween.Parallel().TweenProperty(blob, "position:y", basePosition.Y, 0.3f)
                        .SetTrans(Tween.TransitionType.Cubic)
                        .SetEase(Tween.EaseType.Out);
                    tween.Parallel().TweenProperty(blob, "modulate:a", baseModulate.A, 0.24f);
                }
                break;
        }
    }

    private static void RestoreClassSignatureFx(CardVisual visual)
    {
        var signature = visual.ClassSignature;
        if (signature == null)
            return;

        foreach (var element in signature.Elements)
            RestoreFxElement(element);
    }

    public static Color GetClassAccent(CardClass cardClass) => cardClass switch
    {
        CardClass.Vanguard => new Color(0.9f, 0.22f, 0.27f),
        CardClass.Psion => new Color(0.66f, 0.33f, 0.97f),
        CardClass.Netrunner => new Color(0.02f, 0.71f, 0.83f),
        CardClass.Symbiote => new Color(0.13f, 0.77f, 0.37f),
        _ => new Color(0.68f, 0.72f, 0.78f)
    };

    public static string GetClassPortraitPath(CardClass cardClass) => cardClass switch
    {
        CardClass.Vanguard => "res://resources/textures/characters/vanguard.png",
        CardClass.Psion => "res://resources/textures/characters/psion.png",
        CardClass.Netrunner => "res://resources/textures/characters/netrunner.png",
        CardClass.Symbiote => "res://resources/textures/characters/symbiote.png",
        _ => string.Empty
    };

    private static string GetCardArtPath(CardData cardData)
    {
        foreach (var candidate in BuildArtCandidates(cardData))
        {
            if (TryResolveArtCandidate(candidate, out var resolved))
                return resolved;
        }

        return cardData.Type switch
        {
            CardType.Attack => "res://resources/textures/cards/card_art_attack.svg",
            CardType.Skill => "res://resources/textures/cards/card_art_skill.svg",
            CardType.Power => "res://resources/textures/cards/card_art_power.svg",
            _ => "res://resources/textures/cards/card_art_skill.svg"
        };
    }

    private static Color GetTypeAccent(CardType type) => type switch
    {
        CardType.Attack => new Color(0.95f, 0.24f, 0.24f),
        CardType.Skill => new Color(0.2f, 0.62f, 0.98f),
        CardType.Power => new Color(0.96f, 0.72f, 0.2f),
        _ => new Color(0.6f, 0.62f, 0.68f)
    };

    private static Color GetRarityAccent(CardRarity rarity) => rarity switch
    {
        CardRarity.Rare => new Color(1f, 0.86f, 0.18f),
        CardRarity.Uncommon => new Color(0.45f, 0.78f, 1f),
        _ => new Color(0.84f, 0.86f, 0.9f)
    };

    private static string GetUpgradeBadge(Card card)
    {
        if (!card.IsUpgraded)
            return string.Empty;

        return card.Branch switch
        {
            UpgradeBranch.A => "UPG-A",
            UpgradeBranch.B => "UPG-B",
            _ => "UPG"
        };
    }

    private static string GetTypeCode(CardType type) => type switch
    {
        CardType.Attack => "ATK",
        CardType.Skill => "SKL",
        CardType.Power => "PWR",
        _ => "CRD"
    };

    private static string GetTypeLabel(CardType type) => type switch
    {
        CardType.Attack => "ATTACK",
        CardType.Skill => "SKILL",
        CardType.Power => "POWER",
        _ => "CARD"
    };

    private static string GetClassLabel(CardClass cardClass) => cardClass switch
    {
        CardClass.Vanguard => "先锋",
        CardClass.Psion => "灵能",
        CardClass.Netrunner => "黑客",
        CardClass.Symbiote => "共生",
        CardClass.Colorless => "中立",
        _ => cardClass.ToString()
    };

    private static string GetRarityCode(CardRarity rarity) => rarity switch
    {
        CardRarity.Rare => "RARE",
        CardRarity.Uncommon => "UNCOMMON",
        CardRarity.Starter => "START",
        _ => "COMMON"
    };

    private static string GetRangeLabel(CardRange range) => range switch
    {
        CardRange.Melee => "近战",
        CardRange.Ranged => "远程",
        _ => string.Empty
    };

    private static string GetTargetLabel(TargetType targetType) => targetType switch
    {
        TargetType.SingleEnemy => "单体",
        TargetType.AllEnemies => "全体",
        TargetType.FrontRowEnemies => "前排",
        TargetType.BackRowEnemies => "后排",
        TargetType.Self => "自身",
        TargetType.AllAllies => "友军",
        _ => string.Empty
    };

    private static PanelContainer CreateChip(string text, Color bgColor, Color textColor, int fontSize)
    {
        var chip = new PanelContainer();
        chip.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = bgColor,
            CornerRadiusBottomLeft = 7,
            CornerRadiusBottomRight = 7,
            CornerRadiusTopLeft = 7,
            CornerRadiusTopRight = 7,
            ContentMarginLeft = 8,
            ContentMarginRight = 8,
            ContentMarginTop = 4,
            ContentMarginBottom = 4
        });
        var label = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", textColor);
        chip.AddChild(label);
        return chip;
    }

    private static PanelContainer CreateMiniChip(string text, Color bgColor, Color textColor, int fontSize)
    {
        var chip = new PanelContainer();
        chip.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = bgColor,
            CornerRadiusBottomLeft = 5,
            CornerRadiusBottomRight = 5,
            CornerRadiusTopLeft = 5,
            CornerRadiusTopRight = 5,
            ContentMarginLeft = 6,
            ContentMarginRight = 6,
            ContentMarginTop = 3,
            ContentMarginBottom = 3,
            BorderColor = new Color(textColor.R, textColor.G, textColor.B, 0.18f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1
        });
        var label = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", textColor);
        chip.AddChild(label);
        return chip;
    }

    private static PanelContainer CreateIdentityChip(string text, Color markerColor, int fontSize)
    {
        var chip = new PanelContainer();
        chip.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.095f, 0.115f, 0.96f),
            CornerRadiusBottomLeft = 5,
            CornerRadiusBottomRight = 5,
            CornerRadiusTopLeft = 5,
            CornerRadiusTopRight = 5,
            ContentMarginLeft = 8,
            ContentMarginRight = 6,
            ContentMarginTop = 3,
            ContentMarginBottom = 3,
            BorderColor = new Color(0.84f, 0.88f, 0.94f, 0.08f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1
        });

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 5);

        var marker = new ColorRect
        {
            Color = markerColor,
            CustomMinimumSize = new Vector2(4f, 12f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        row.AddChild(marker);

        var label = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", new Color(0.78f, 0.82f, 0.88f));
        row.AddChild(label);

        chip.AddChild(row);
        return chip;
    }

    private static PanelContainer CreateFooterChip(string text, Color accent, bool compact)
    {
        var chip = new PanelContainer();
        chip.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(accent.R * 0.12f, accent.G * 0.12f, accent.B * 0.12f, 0.92f),
            BorderColor = new Color(accent.R, accent.G, accent.B, 0.28f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            ContentMarginLeft = compact ? 6 : 8,
            ContentMarginRight = compact ? 6 : 8,
            ContentMarginTop = compact ? 4 : 5,
            ContentMarginBottom = compact ? 4 : 5
        });
        var label = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", compact ? 9 : 10);
        label.AddThemeColorOverride("font_color", accent);
        chip.AddChild(label);
        return chip;
    }

    private static string ToVerticalText(string text)
    {
        return string.Join("\n", text.ToCharArray());
    }

    private static Texture2D? LoadTexture(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        if (TextureCache.TryGetValue(path, out var cached))
            return cached;

        var texture = LoadRasterTextureFromFile(path) ?? GD.Load<Texture2D>(path);
        if (texture != null)
            TextureCache[path] = texture;
        return texture;
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

    private static IEnumerable<string> BuildArtCandidates(CardData cardData)
    {
        string classFolder = cardData.Class.ToString().ToLowerInvariant();

        if (!string.IsNullOrWhiteSpace(cardData.ArtPath))
        {
            string explicitPath = NormalizeResourcePath(cardData.ArtPath!);
            if (explicitPath.StartsWith("res://", StringComparison.OrdinalIgnoreCase))
            {
                yield return explicitPath;
            }
            else
            {
                yield return $"{CardArtRoot}/{explicitPath}";
                yield return $"{CardArtRoot}/{classFolder}/{explicitPath}";
            }
        }

        string normalizedName = NormalizeArtSlug(string.IsNullOrWhiteSpace(cardData.NameEn) ? cardData.Name : cardData.NameEn);
        string normalizedId = NormalizeArtSlug(cardData.Id);

        yield return $"{CardArtRoot}/{classFolder}/{normalizedId}";
        yield return $"{CardArtRoot}/{normalizedId}";

        if (!string.IsNullOrWhiteSpace(normalizedName))
        {
            yield return $"{CardArtRoot}/{classFolder}/{normalizedName}";
            yield return $"{CardArtRoot}/{normalizedName}";
        }
    }

    private static bool TryResolveArtCandidate(string candidate, out string resolved)
    {
        string normalized = NormalizeResourcePath(candidate);
        if (HasKnownArtExtension(normalized))
        {
            if (ResourceLoader.Exists(normalized))
            {
                resolved = normalized;
                return true;
            }

            resolved = string.Empty;
            return false;
        }

        foreach (var extension in CardArtExtensions)
        {
            string path = normalized + extension;
            if (ResourceLoader.Exists(path))
            {
                resolved = path;
                return true;
            }
        }

        resolved = string.Empty;
        return false;
    }

    private static bool HasKnownArtExtension(string path)
    {
        return CardArtExtensions.Any(ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeResourcePath(string path)
    {
        return path.Replace('\\', '/').Trim();
    }

    private static string NormalizeArtSlug(string value)
    {
        Span<char> buffer = stackalloc char[value.Length];
        int index = 0;
        bool lastUnderscore = false;
        foreach (char raw in value)
        {
            char c = char.ToLowerInvariant(raw);
            if (char.IsLetterOrDigit(c))
            {
                buffer[index++] = c;
                lastUnderscore = false;
            }
            else if (!lastUnderscore)
            {
                buffer[index++] = '_';
                lastUnderscore = true;
            }
        }

        while (index > 0 && buffer[index - 1] == '_')
            index--;

        return index == 0 ? string.Empty : new string(buffer[..index]);
    }

    private static ColorRect CreateScanBeam(Vector2 minSize, float artHeight, Color accent, bool compact)
    {
        var beam = new ColorRect
        {
            Size = new Vector2(minSize.X * (compact ? 0.34f : 0.42f), artHeight * 1.45f),
            Position = new Vector2(-minSize.X * 0.55f, -artHeight * 0.16f),
            RotationDegrees = 16f,
            Color = new Color(Mathf.Lerp(accent.R, 1f, 0.45f), Mathf.Lerp(accent.G, 1f, 0.45f), Mathf.Lerp(accent.B, 1f, 0.45f), 0.32f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Modulate = new Color(1f, 1f, 1f, 0f)
        };
        return beam;
    }

    private static void AddCompactBackdrop(Control artStage, Vector2 minSize, float artHeight, Color accent, Color detailAccent, Color classMarker, CardData cardData)
    {
        var focusPlate = new PanelContainer
        {
            Size = new Vector2(minSize.X * 0.74f, artHeight * 0.72f),
            Position = new Vector2(18f, 14f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        focusPlate.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.02f, 0.025f, 0.034f, 0.16f),
            BorderColor = new Color(detailAccent.R, detailAccent.G, detailAccent.B, 0.04f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusBottomLeft = 18,
            CornerRadiusBottomRight = 18,
            CornerRadiusTopLeft = 18,
            CornerRadiusTopRight = 18
        });
        artStage.AddChild(focusPlate);

        var watermark = new Label
        {
            Text = GetTypeCode(cardData.Type),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        watermark.AddThemeFontSizeOverride("font_size", 38);
        watermark.AddThemeColorOverride("font_color", new Color(accent.R, accent.G, accent.B, 0.08f));
        watermark.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        watermark.OffsetLeft = 0f;
        watermark.OffsetRight = -10f;
        watermark.OffsetTop = 6f;
        watermark.OffsetBottom = -6f;
        artStage.AddChild(watermark);

        var lineTop = new ColorRect
        {
            Color = new Color(accent.R, accent.G, accent.B, 0.045f),
            Size = new Vector2(minSize.X * 0.5f, 2f),
            Position = new Vector2(16f, 18f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        artStage.AddChild(lineTop);

        var lineMid = new ColorRect
        {
            Color = new Color(detailAccent.R, detailAccent.G, detailAccent.B, 0.03f),
            Size = new Vector2(minSize.X * 0.36f, 2f),
            Position = new Vector2(16f, Mathf.Max(30f, artHeight * 0.56f)),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        artStage.AddChild(lineMid);

        var notch = new ColorRect
        {
            Color = classMarker,
            Size = new Vector2(12f, 12f),
            Position = new Vector2(minSize.X - 24f, 16f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        artStage.AddChild(notch);

        var subtype = new Label
        {
            Text = GetClassLabel(cardData.Class).ToUpperInvariant(),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        subtype.AddThemeFontSizeOverride("font_size", 8);
        subtype.AddThemeColorOverride("font_color", new Color(detailAccent.R, detailAccent.G, detailAccent.B, 0.22f));
        subtype.Position = new Vector2(16f, artHeight - 18f);
        artStage.AddChild(subtype);
    }

    private static ClassSignatureFx? CreateClassSignatureFx(Vector2 minSize, float artHeight, CardClass cardClass, Color accent, bool compact, bool dimmed)
    {
        if (cardClass == CardClass.Colorless)
            return null;

        float rootAlpha = compact ? (dimmed ? 0.03f : 0.08f) : dimmed ? 0.42f : 1f;
        var root = new Control
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ClipContents = true,
            Modulate = new Color(1f, 1f, 1f, rootAlpha)
        };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);

        switch (cardClass)
        {
            case CardClass.Vanguard:
            {
                var bandA = CreateSignatureBar(
                    new Vector2(minSize.X * 0.58f, compact ? 8f : 10f),
                    new Vector2(minSize.X * 0.22f, artHeight * 0.7f),
                    Blend(accent, new Color(1f, 0.56f, 0.14f), 0.7f),
                    0.4f,
                    -7f);
                var bandB = CreateSignatureBar(
                    new Vector2(minSize.X * 0.46f, compact ? 6f : 8f),
                    new Vector2(minSize.X * 0.31f, artHeight * 0.54f),
                    Blend(accent, new Color(1f, 0.82f, 0.22f), 0.54f),
                    0.54f,
                    5f);
                var bandC = CreateSignatureBar(
                    new Vector2(minSize.X * 0.38f, compact ? 5f : 6f),
                    new Vector2(minSize.X * 0.41f, artHeight * 0.82f),
                    Blend(accent, Colors.White, 0.28f),
                    0.34f,
                    -3f);
                root.AddChild(bandA);
                root.AddChild(bandB);
                root.AddChild(bandC);
                return new ClassSignatureFx(cardClass, root, bandA, bandB, bandC);
            }

            case CardClass.Psion:
            {
                Color phase = Blend(accent, new Color(0.92f, 0.9f, 1f), 0.42f);
                Vector2 outerSize = new(minSize.X * 0.72f, artHeight * 0.38f);
                Vector2 outerPosition = new((minSize.X - outerSize.X) * 0.48f, artHeight * 0.3f);
                var outerRing = CreateSignatureRing(outerSize, outerPosition, phase, compact ? 2 : 3, 0.18f, 0.16f);

                Vector2 innerSize = new(minSize.X * 0.54f, artHeight * 0.28f);
                Vector2 innerPosition = new((minSize.X - innerSize.X) * 0.44f, artHeight * 0.39f);
                var innerRing = CreateSignatureRing(innerSize, innerPosition, phase, compact ? 2 : 3, 0.24f, 0.22f);

                Vector2 coreSize = new(compact ? 26f : 34f, compact ? 26f : 34f);
                Vector2 corePosition = new((minSize.X - coreSize.X) * 0.52f, artHeight * 0.45f);
                var core = CreateSignatureBlob(coreSize, corePosition, Blend(phase, Colors.White, 0.34f), 0.14f, 0.34f, compact, 0f);

                root.AddChild(outerRing);
                root.AddChild(innerRing);
                root.AddChild(core);
                return new ClassSignatureFx(cardClass, root, outerRing, innerRing, core);
            }

            case CardClass.Netrunner:
            {
                Color corruption = Blend(accent, new Color(0.78f, 1f, 1f), 0.42f);
                var sliceA = CreateSignatureBar(
                    new Vector2(compact ? 10f : 12f, artHeight * 0.76f),
                    new Vector2(minSize.X * 0.2f, artHeight * 0.08f),
                    corruption,
                    0.18f,
                    -3.5f);
                var sliceB = CreateSignatureBar(
                    new Vector2(compact ? 8f : 10f, artHeight * 0.56f),
                    new Vector2(minSize.X * 0.49f, artHeight * 0.18f),
                    Blend(corruption, Colors.White, 0.22f),
                    0.3f,
                    2.6f);
                var sliceC = CreateSignatureBar(
                    new Vector2(compact ? 9f : 11f, artHeight * 0.52f),
                    new Vector2(minSize.X * 0.74f, artHeight * 0.14f),
                    corruption,
                    0.16f,
                    -1.6f);
                var sliceD = CreateSignatureBar(
                    new Vector2(minSize.X * 0.26f, compact ? 4f : 5f),
                    new Vector2(minSize.X * 0.4f, artHeight * 0.7f),
                    Blend(corruption, new Color(0.02f, 0.1f, 0.12f), 0.1f),
                    0.22f,
                    0.8f);
                root.AddChild(sliceA);
                root.AddChild(sliceB);
                root.AddChild(sliceC);
                root.AddChild(sliceD);
                return new ClassSignatureFx(cardClass, root, sliceA, sliceB, sliceC, sliceD);
            }

            case CardClass.Symbiote:
            {
                Color organic = Blend(accent, new Color(0.66f, 1f, 0.62f), 0.32f);
                var massA = CreateSignatureBlob(
                    new Vector2(minSize.X * 0.38f, artHeight * 0.24f),
                    new Vector2(minSize.X * 0.16f, artHeight * 0.56f),
                    organic,
                    0.32f,
                    0.34f,
                    compact,
                    -10f);
                var massB = CreateSignatureBlob(
                    new Vector2(minSize.X * 0.28f, artHeight * 0.2f),
                    new Vector2(minSize.X * 0.54f, artHeight * 0.32f),
                    Blend(organic, new Color(0.92f, 1f, 0.92f), 0.14f),
                    0.28f,
                    0.32f,
                    compact,
                    11f);
                var massC = CreateSignatureBlob(
                    new Vector2(minSize.X * 0.22f, artHeight * 0.16f),
                    new Vector2(minSize.X * 0.46f, artHeight * 0.7f),
                    organic,
                    0.22f,
                    0.26f,
                    compact,
                    -14f);
                root.AddChild(massA);
                root.AddChild(massB);
                root.AddChild(massC);
                return new ClassSignatureFx(cardClass, root, massA, massB, massC);
            }
        }

        return null;
    }

    private static ColorRect CreateGlitchBar(float width, float y, Color color, bool compact, bool wide)
    {
        Vector2 basePosition = new(width * (wide ? 0.14f : 0.28f), y);
        var bar = new ColorRect
        {
            Size = new Vector2(width * (wide ? 0.82f : 0.58f), compact ? 4f : 6f),
            Position = basePosition,
            Color = new Color(color.R, color.G, color.B, 0.95f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Modulate = new Color(1f, 1f, 1f, 0f)
        };
        bar.SetMeta("base_position", basePosition);
        return bar;
    }

    private static ColorRect CreateSignatureBar(Vector2 size, Vector2 position, Color color, float alpha, float rotationDegrees)
    {
        var bar = new ColorRect
        {
            Size = size,
            Position = position,
            PivotOffset = size / 2f,
            RotationDegrees = rotationDegrees,
            Color = new Color(color.R, color.G, color.B, 1f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Modulate = new Color(1f, 1f, 1f, alpha)
        };
        StoreFxElement(bar);
        return bar;
    }

    private static PanelContainer CreateSignatureRing(Vector2 size, Vector2 position, Color color, int borderWidth, float alpha, float fillAlpha)
    {
        int radius = Mathf.RoundToInt(size.Y * 0.5f);
        var ring = new PanelContainer
        {
            Size = size,
            Position = position,
            PivotOffset = size / 2f,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Modulate = new Color(1f, 1f, 1f, alpha)
        };
        ring.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(color.R, color.G, color.B, fillAlpha),
            BorderColor = new Color(color.R, color.G, color.B, 0.92f),
            BorderWidthBottom = borderWidth,
            BorderWidthLeft = borderWidth,
            BorderWidthRight = borderWidth,
            BorderWidthTop = borderWidth,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius,
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius
        });
        StoreFxElement(ring);
        return ring;
    }

    private static PanelContainer CreateSignatureBlob(Vector2 size, Vector2 position, Color color, float alpha, float borderAlpha, bool compact, float rotationDegrees)
    {
        int radius = Mathf.RoundToInt(size.Y * (compact ? 0.46f : 0.5f));
        var blob = new PanelContainer
        {
            Size = size,
            Position = position,
            PivotOffset = size / 2f,
            RotationDegrees = rotationDegrees,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Modulate = new Color(1f, 1f, 1f, alpha)
        };
        blob.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(color.R, color.G, color.B, 0.84f),
            BorderColor = new Color(Mathf.Lerp(color.R, 1f, 0.14f), Mathf.Lerp(color.G, 1f, 0.14f), Mathf.Lerp(color.B, 1f, 0.14f), borderAlpha),
            BorderWidthBottom = compact ? 1 : 2,
            BorderWidthLeft = compact ? 1 : 2,
            BorderWidthRight = compact ? 1 : 2,
            BorderWidthTop = compact ? 1 : 2,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius,
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius
        });
        StoreFxElement(blob);
        return blob;
    }

    private static (Control overlay, ColorRect runner) AddSelectionOverlay(PanelContainer panel, Color accent, bool compact, Vector2 minSize)
    {
        var overlay = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);

        float bracket = compact ? 12f : 16f;
        float thickness = compact ? 2f : 3f;

        AddBracket(overlay, true, true, bracket, thickness);
        AddBracket(overlay, false, true, bracket, thickness);
        AddBracket(overlay, true, false, bracket, thickness);
        AddBracket(overlay, false, false, bracket, thickness);

        var runner = new ColorRect
        {
            Size = new Vector2(compact ? 28f : 36f, compact ? 3f : 4f),
            Position = new Vector2(-40f, minSize.Y - (compact ? 8f : 10f)),
            Color = new Color(0.82f, 0.95f, 1f, 0.95f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        overlay.AddChild(runner);

        panel.AddChild(overlay);

        overlay.Modulate = new Color(1f, 1f, 1f, 0.88f);
        var overlayPulse = panel.CreateTween().SetLoops();
        overlayPulse.TweenProperty(overlay, "modulate:a", 0.45f, 0.65f)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        overlayPulse.TweenProperty(overlay, "modulate:a", 0.9f, 0.65f)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);

        var energyLoop = panel.CreateTween().SetLoops();
        energyLoop.TweenProperty(runner, "position:x", minSize.X + 18f, 0.95f)
            .From(-runner.Size.X - 18f)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
        energyLoop.TweenInterval(0.25f);

        return (overlay, runner);
    }

    private static void AddBracket(Control parent, bool left, bool top, float size, float thickness)
    {
        var horiz = new ColorRect { Color = new Color(0.84f, 0.96f, 1f, 0.92f), MouseFilter = Control.MouseFilterEnum.Ignore };
        horiz.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        horiz.AnchorLeft = left ? 0f : 1f;
        horiz.AnchorRight = left ? 0f : 1f;
        horiz.AnchorTop = top ? 0f : 1f;
        horiz.AnchorBottom = top ? 0f : 1f;
        horiz.OffsetLeft = left ? 6f : -(size + 6f);
        horiz.OffsetRight = left ? size + 6f : -6f;
        horiz.OffsetTop = top ? 6f : -(thickness + 6f);
        horiz.OffsetBottom = top ? thickness + 6f : -6f;
        parent.AddChild(horiz);

        var vert = new ColorRect { Color = new Color(0.84f, 0.96f, 1f, 0.92f), MouseFilter = Control.MouseFilterEnum.Ignore };
        vert.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        vert.AnchorLeft = left ? 0f : 1f;
        vert.AnchorRight = left ? 0f : 1f;
        vert.AnchorTop = top ? 0f : 1f;
        vert.AnchorBottom = top ? 0f : 1f;
        vert.OffsetLeft = left ? 6f : -(thickness + 6f);
        vert.OffsetRight = left ? thickness + 6f : -6f;
        vert.OffsetTop = top ? 6f : -(size + 6f);
        vert.OffsetBottom = top ? size + 6f : -6f;
        parent.AddChild(vert);
    }

    private static void TriggerGlitchBars(PanelContainer panel, CardVisual visual)
    {
        float firstOffset = MathF.Min(visual.ArtStage.Size.X * 0.08f, 18f);
        float secondOffset = -MathF.Min(visual.ArtStage.Size.X * 0.06f, 14f);
        Vector2 firstBasePosition = (Vector2)visual.GlitchBarPrimary.GetMeta("base_position");
        Vector2 secondBasePosition = (Vector2)visual.GlitchBarSecondary.GetMeta("base_position");

        var first = panel.CreateTween();
        visual.GlitchBarPrimary.Modulate = new Color(1f, 1f, 1f, 0f);
        visual.GlitchBarPrimary.Position = new Vector2(firstBasePosition.X - firstOffset, firstBasePosition.Y);
        first.TweenProperty(visual.GlitchBarPrimary, "modulate:a", 0.7f, 0.04f);
        first.Parallel().TweenProperty(visual.GlitchBarPrimary, "position:x", firstBasePosition.X + firstOffset, 0.08f);
        first.TweenProperty(visual.GlitchBarPrimary, "modulate:a", 0f, 0.06f);

        var second = panel.CreateTween();
        visual.GlitchBarSecondary.Modulate = new Color(1f, 1f, 1f, 0f);
        visual.GlitchBarSecondary.Position = new Vector2(secondBasePosition.X + secondOffset, secondBasePosition.Y);
        second.TweenInterval(0.05f);
        second.TweenProperty(visual.GlitchBarSecondary, "modulate:a", 0.55f, 0.03f);
        second.Parallel().TweenProperty(visual.GlitchBarSecondary, "position:x", secondBasePosition.X - secondOffset * 0.5f, 0.07f);
        second.TweenProperty(visual.GlitchBarSecondary, "modulate:a", 0f, 0.05f);
    }

    private static void StartAlphaLoop(PanelContainer panel, CanvasItem target, float minMultiplier, float maxMultiplier, float duration)
    {
        float baseAlpha = target.Modulate.A;
        var tween = panel.CreateTween().SetLoops();
        tween.TweenProperty(target, "modulate:a", Mathf.Clamp(baseAlpha * maxMultiplier, 0f, 1f), duration)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        tween.TweenProperty(target, "modulate:a", Mathf.Clamp(baseAlpha * minMultiplier, 0f, 1f), duration)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
    }

    private static void StartScaleLoop(PanelContainer panel, Control target, Vector2 delta, float duration, float interval)
    {
        Vector2 baseScale = GetBaseScale(target);
        var tween = panel.CreateTween().SetLoops();
        tween.TweenProperty(target, "scale", baseScale + delta, duration)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        tween.TweenProperty(target, "scale", baseScale - delta * 0.35f, duration)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        tween.TweenProperty(target, "scale", baseScale, duration * 0.72f)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
        if (interval > 0f)
            tween.TweenInterval(interval);
    }

    private static void StartPositionLoop(PanelContainer panel, Control target, Vector2 delta, float duration, float interval)
    {
        Vector2 basePosition = GetBasePosition(target);
        var tween = panel.CreateTween().SetLoops();
        tween.TweenProperty(target, "position", basePosition + delta, duration)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        tween.TweenProperty(target, "position", basePosition - delta * 0.4f, duration)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        tween.TweenProperty(target, "position", basePosition, duration * 0.68f)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
        if (interval > 0f)
            tween.TweenInterval(interval);
    }

    private static void StoreFxElement(Control control)
    {
        control.SetMeta("fx_base_position", control.Position);
        control.SetMeta("fx_base_scale", control.Scale);
        control.SetMeta("fx_base_modulate", control.Modulate);
    }

    private static void RestoreFxElement(Control control)
    {
        control.Position = GetBasePosition(control);
        control.Scale = GetBaseScale(control);
        control.Modulate = GetBaseModulate(control);
    }

    private static Vector2 GetBasePosition(Control control)
    {
        return (Vector2)control.GetMeta("fx_base_position");
    }

    private static Vector2 GetBaseScale(Control control)
    {
        return (Vector2)control.GetMeta("fx_base_scale");
    }

    private static Color GetBaseModulate(Control control)
    {
        return (Color)control.GetMeta("fx_base_modulate");
    }

    private static Control AddRareBorderOverlay(PanelContainer panel, Color accent, bool compact, Vector2 minSize)
    {
        var overlay = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);

        float lineSize = compact ? 2f : 3f;
        var top = new ColorRect { Color = new Color(1f, 0.94f, 0.45f, 0.9f), MouseFilter = Control.MouseFilterEnum.Ignore };
        top.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        top.AnchorBottom = 0f;
        top.OffsetBottom = lineSize;
        top.OffsetLeft = compact ? 10f : 14f;
        top.OffsetRight = -(compact ? 10f : 14f);
        overlay.AddChild(top);

        var bottom = new ColorRect { Color = new Color(accent.R, accent.G, accent.B, 0.85f), MouseFilter = Control.MouseFilterEnum.Ignore };
        bottom.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        bottom.AnchorTop = 1f;
        bottom.OffsetTop = -lineSize;
        bottom.OffsetLeft = compact ? 10f : 14f;
        bottom.OffsetRight = -(compact ? 10f : 14f);
        overlay.AddChild(bottom);

        var runner = new ColorRect
        {
            Size = new Vector2(compact ? 26f : 34f, lineSize + 1f),
            Position = new Vector2(-40f, compact ? 8f : 10f),
            Color = new Color(1f, 1f, 1f, 0.95f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        overlay.AddChild(runner);

        panel.AddChild(overlay);

        var pulse = panel.CreateTween().SetLoops();
        overlay.Modulate = new Color(1f, 1f, 1f, 0.35f);
        pulse.TweenProperty(overlay, "modulate:a", 0.95f, 0.85f)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        pulse.TweenProperty(overlay, "modulate:a", 0.35f, 0.95f)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);

        var runnerTween = panel.CreateTween().SetLoops();
        runnerTween.TweenProperty(runner, "position:x", minSize.X + 28f, 1.15f)
            .From(-runner.Size.X - 18f)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
        runnerTween.TweenInterval(0.6f);

        return overlay;
    }

    private static void AddCornerMarks(PanelContainer panel, Color accent, bool compact)
    {
        // PanelContainer (a Container subclass) force-fits all direct children to
        // fill the content area via fit_child_in_rect, ignoring anchor settings.
        // Wrap corner marks in a plain Control so anchors work correctly.
        var wrapper = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        wrapper.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        panel.AddChild(wrapper);

        float size = compact ? 12f : 16f;

        var tl = new ColorRect { Color = new Color(accent.R, accent.G, accent.B, 0.65f), MouseFilter = Control.MouseFilterEnum.Ignore };
        tl.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        tl.AnchorRight = 0f;
        tl.AnchorBottom = 0f;
        tl.OffsetRight = size;
        tl.OffsetBottom = 2f;
        wrapper.AddChild(tl);

        var br = new ColorRect { Color = new Color(accent.R, accent.G, accent.B, 0.55f), MouseFilter = Control.MouseFilterEnum.Ignore };
        br.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        br.AnchorLeft = 1f;
        br.AnchorTop = 1f;
        br.OffsetLeft = -size;
        br.OffsetTop = -2f;
        wrapper.AddChild(br);
    }

    private static void AddDebugLayerLegend(PanelContainer panel, IReadOnlyList<(string label, Color color)> layers, string title)
    {
        var overlay = new MarginContainer
        {
            Name = "DebugLayerLegend",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 4096
        };
        overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);

        var legend = new PanelContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 4096
        };
        legend.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        legend.OffsetLeft = -88f;
        legend.OffsetRight = -4f;
        legend.OffsetTop = 4f;
        legend.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.025f, 0.03f, 0.044f, 0.92f),
            BorderColor = new Color(0.74f, 0.82f, 0.98f, 0.22f),
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            CornerRadiusBottomLeft = 5,
            CornerRadiusBottomRight = 5,
            CornerRadiusTopLeft = 5,
            CornerRadiusTopRight = 5,
            ContentMarginBottom = 4,
            ContentMarginLeft = 5,
            ContentMarginRight = 5,
            ContentMarginTop = 4
        });

        var box = new VBoxContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        box.AddThemeConstantOverride("separation", 1);

        var titleLabel = new Label
        {
            Text = title.ToUpperInvariant(),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        titleLabel.AddThemeFontSizeOverride("font_size", 6);
        titleLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.95f, 1f, 0.84f));
        box.AddChild(titleLabel);

        foreach (var (label, color) in layers)
        {
            var row = new HBoxContainer
            {
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            row.AddThemeConstantOverride("separation", 4);

            var swatch = new ColorRect
            {
                Color = color,
                CustomMinimumSize = new Vector2(5f, 5f),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            row.AddChild(swatch);

            var itemLabel = new Label
            {
                Text = label,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            itemLabel.AddThemeFontSizeOverride("font_size", 6);
            itemLabel.AddThemeColorOverride("font_color", new Color(0.82f, 0.88f, 0.96f, 0.96f));
            row.AddChild(itemLabel);

            box.AddChild(row);
        }

        legend.AddChild(box);
        overlay.AddChild(legend);
        panel.AddChild(overlay);
    }

    private static Color Blend(Color a, Color b, float t)
    {
        return new Color(
            Mathf.Lerp(a.R, b.R, t),
            Mathf.Lerp(a.G, b.G, t),
            Mathf.Lerp(a.B, b.B, t),
            Mathf.Lerp(a.A, b.A, t));
    }
}
