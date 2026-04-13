using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using RogueCardGame.Core.Events;

namespace RogueCardGame;

public partial class EventScene : Control
{
    private Label _title = null!;
    private RichTextLabel _description = null!;
    private VBoxContainer _choicesContainer = null!;
    private RichTextLabel _resultLabel = null!;
    private Button _continueBtn = null!;

    private EventData? _currentEvent;
    private EventSystem? _eventSystem;

    // Typewriter
    private string _fullDescText = "";
    private int _charIndex;
    private float _typeTimer;
    private const float TypeSpeed = 0.025f;
    private bool _typing;
    private TopBarHUD? _topBar;

    public override void _Ready()
    {
        _title = GetNode<Label>("EventPanel/Content/Title");
        _description = GetNode<RichTextLabel>("EventPanel/Content/Description");
        _choicesContainer = GetNode<VBoxContainer>("EventPanel/Content/ChoicesContainer");
        _resultLabel = GetNode<RichTextLabel>("EventPanel/Content/ResultLabel");
        _continueBtn = GetNode<Button>("EventPanel/Content/ContinueBtn");

        _continueBtn.Pressed += OnContinuePressed;

        CyberFx.AddScanlines(this, 0.03f);
        CyberFx.AddVignette(this);

        _topBar = TopBarHUD.Attach(this);

        StyleUI();
        AddDecorations();
        AnimateEntry();
        AudioManager.Instance?.PlayBgm(AudioManager.BgmPaths.Event);
        LoadEvent();
    }

    public override void _Process(double delta)
    {
        if (!_typing) return;

        _typeTimer += (float)delta;
        while (_typeTimer >= TypeSpeed && _charIndex < _fullDescText.Length)
        {
            _typeTimer -= TypeSpeed;
            _charIndex++;
            _description.Text = _fullDescText[.._charIndex];
        }
        if (_charIndex >= _fullDescText.Length)
            _typing = false;
    }

    public override void _Input(InputEvent ev)
    {
        // Skip typewriter on click
        if (_typing && ev is InputEventMouseButton { Pressed: true })
        {
            _typing = false;
            _description.Text = _fullDescText;
        }
    }

    private void StyleUI()
    {
        // Title
        _title.AddThemeFontSizeOverride("font_size", 36);
        _title.AddThemeColorOverride("font_color", new Color(0.5f, 0.7f, 1f));

        // Event panel
        var panelStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.05f, 0.05f, 0.09f, 0.95f),
            BorderColor = new Color(0.25f, 0.35f, 0.6f),
            BorderWidthBottom = 2, BorderWidthTop = 2,
            BorderWidthLeft = 2, BorderWidthRight = 2,
            CornerRadiusBottomLeft = 16, CornerRadiusBottomRight = 16,
            CornerRadiusTopLeft = 16, CornerRadiusTopRight = 16,
            ContentMarginLeft = 40, ContentMarginRight = 40,
            ContentMarginTop = 30, ContentMarginBottom = 30,
            ShadowColor = new Color(0.15f, 0.2f, 0.4f, 0.5f),
            ShadowSize = 12,
            ShadowOffset = new Vector2(0, 4)
        };
        GetNode<PanelContainer>("EventPanel").AddThemeStyleboxOverride("panel", panelStyle);

        // Description
        _description.AddThemeFontSizeOverride("normal_font_size", 20);
        _description.AddThemeColorOverride("default_color", new Color(0.8f, 0.82f, 0.9f));

        // Result label
        _resultLabel.AddThemeFontSizeOverride("normal_font_size", 20);

        // Continue button
        var contStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.1f, 0.2f, 0.35f),
            BorderColor = new Color(0f, 0.8f, 0.8f),
            BorderWidthBottom = 2, BorderWidthTop = 2,
            BorderWidthLeft = 2, BorderWidthRight = 2,
            CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
            CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8,
            ContentMarginLeft = 30, ContentMarginRight = 30,
            ContentMarginTop = 8, ContentMarginBottom = 8,
            ShadowColor = new Color(0f, 0.5f, 0.5f, 0.3f),
            ShadowSize = 6
        };
        _continueBtn.AddThemeStyleboxOverride("normal", contStyle);
        var contHover = (StyleBoxFlat)contStyle.Duplicate();
        contHover.BgColor = new Color(0.15f, 0.3f, 0.5f);
        contHover.ShadowSize = 10;
        _continueBtn.AddThemeStyleboxOverride("hover", contHover);
        _continueBtn.AddThemeColorOverride("font_color", new Color(0f, 0.95f, 0.95f));
        _continueBtn.AddThemeFontSizeOverride("font_size", 22);
        _continueBtn.Text = "继续 →";
    }

    private void LoadEvent()
    {
        var run = GameManager.Instance.CurrentRun;
        if (run == null) return;

        _eventSystem = new EventSystem(run.Random);
        var allEvents = run.EventDb.GetAll();
        _currentEvent = _eventSystem.SelectEvent(allEvents, run.CurrentAct);

        if (_currentEvent == null)
        {
            _title.Text = "📡 信号丢失";
            _description.Text = "扫描范围内没有探测到任何异常信号...";
            _continueBtn.Visible = true;
            return;
        }

        _title.Text = $"📡 {_currentEvent.Title}";

        // Typewriter effect
        _fullDescText = _currentEvent.Description;
        _description.Text = "";
        _charIndex = 0;
        _typeTimer = 0;
        _typing = true;

        // Delay choices slightly
        var timer = GetTree().CreateTimer(0.6);
        timer.Timeout += BuildChoices;
    }

    private void BuildChoices()
    {
        foreach (var child in _choicesContainer.GetChildren())
            child.QueueFree();

        if (_currentEvent == null) return;

        for (int i = 0; i < _currentEvent.Choices.Count; i++)
        {
            var choice = _currentEvent.Choices[i];
            int choiceIndex = i;

            var panel = new PanelContainer();
            panel.CustomMinimumSize = new Vector2(0, 54);

            var choiceColor = (i % 3) switch
            {
                0 => new Color(0.3f, 0.5f, 0.8f),
                1 => new Color(0.7f, 0.4f, 0.2f),
                _ => new Color(0.4f, 0.7f, 0.3f)
            };

            var style = new StyleBoxFlat
            {
                BgColor = choiceColor * 0.12f,
                BorderColor = choiceColor * 0.5f,
                BorderWidthBottom = 1, BorderWidthTop = 1,
                BorderWidthLeft = 3, BorderWidthRight = 1,
                CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
                CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8,
                ContentMarginLeft = 20, ContentMarginRight = 20,
                ContentMarginTop = 10, ContentMarginBottom = 10,
                ShadowColor = choiceColor * 0.1f,
                ShadowSize = 4
            };
            panel.AddThemeStyleboxOverride("panel", style);

            var hbox = new HBoxContainer();
            hbox.AddThemeConstantOverride("separation", 12);

            // Number badge
            var badge = new Label { Text = $"{i + 1}" };
            badge.AddThemeFontSizeOverride("font_size", 22);
            badge.AddThemeColorOverride("font_color", choiceColor);
            hbox.AddChild(badge);

            var textVbox = new VBoxContainer();
            textVbox.AddThemeConstantOverride("separation", 2);
            textVbox.SizeFlagsHorizontal = SizeFlags.ExpandFill;

            var textLabel = new Label { Text = choice.Text };
            textLabel.AddThemeFontSizeOverride("font_size", 19);
            textLabel.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.9f));
            textLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            textVbox.AddChild(textLabel);

            // STS2-style: preview outcomes
            var preview = GetOutcomePreview(choice);
            if (!string.IsNullOrEmpty(preview))
            {
                var previewLbl = new Label { Text = preview };
                previewLbl.AddThemeFontSizeOverride("font_size", 13);
                previewLbl.AddThemeColorOverride("font_color", new Color(0.55f, 0.55f, 0.65f));
                previewLbl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                textVbox.AddChild(previewLbl);
            }

            hbox.AddChild(textVbox);

            var arrow = new Label { Text = "→" };
            arrow.AddThemeFontSizeOverride("font_size", 20);
            arrow.AddThemeColorOverride("font_color", choiceColor * 0.7f);
            hbox.AddChild(arrow);

            panel.AddChild(hbox);

            // Hover
            panel.MouseEntered += () =>
            {
                AudioManager.Instance?.PlaySfx(AudioManager.SfxPaths.ButtonHover);
                var tw = CreateTween();
                tw.TweenProperty(panel, "modulate", new Color(1.15f, 1.15f, 1.15f), 0.12f);
                style.BorderColor = choiceColor;
                style.ShadowSize = 8;
            };
            panel.MouseExited += () =>
            {
                var tw = CreateTween();
                tw.TweenProperty(panel, "modulate", Colors.White, 0.12f);
                style.BorderColor = choiceColor * 0.5f;
                style.ShadowSize = 4;
            };
            panel.GuiInput += (ev) =>
            {
                if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
                    OnChoiceMade(choiceIndex);
            };

            // Stagger reveal
            panel.Modulate = new Color(1, 1, 1, 0);
            _choicesContainer.AddChild(panel);

            var tw2 = CreateTween();
            tw2.TweenProperty(panel, "modulate:a", 1.0f, 0.25f)
                .SetDelay(i * 0.12f)
                .SetTrans(Tween.TransitionType.Cubic);
        }
    }

    private void OnChoiceMade(int choiceIndex)
    {
        if (_currentEvent == null || _eventSystem == null) return;

        AudioManager.Instance?.PlaySfx(AudioManager.SfxPaths.CardSelect);

        var outcomes = _eventSystem.ResolveChoice(_currentEvent, choiceIndex);
        ApplyOutcomes(outcomes);

        // Fade out choices
        foreach (var child in _choicesContainer.GetChildren())
        {
            if (child is Control ctrl)
            {
                var tw = CreateTween();
                tw.TweenProperty(ctrl, "modulate:a", 0f, 0.2f);
            }
        }

        var hideTimer = GetTree().CreateTimer(0.3);
        hideTimer.Timeout += () =>
        {
            _choicesContainer.Visible = false;
            _resultLabel.Visible = true;
            _continueBtn.Visible = true;

            // Fade in result
            _resultLabel.Modulate = new Color(1, 1, 1, 0);
            var tw = CreateTween();
            tw.TweenProperty(_resultLabel, "modulate:a", 1.0f, 0.3f);
        };
    }

    private void ApplyOutcomes(List<EventChoiceOutcome> outcomes)
    {
        var run = GameManager.Instance.CurrentRun;
        if (run == null) return;

        var resultText = "";

        foreach (var outcome in outcomes)
        {
            switch (outcome.Effect)
            {
                case EventChoiceEffect.GainGold:
                    run.AddGold(outcome.Value);
                    AudioManager.Instance?.PlaySfx(AudioManager.SfxPaths.GoldGain);
                    resultText += $"[color=yellow]💰 获得 {outcome.Value} 金币[/color]\n";
                    break;
                case EventChoiceEffect.LoseGold:
                    run.TrySpendGold(outcome.Value);
                    resultText += $"[color=red]💰 失去 {outcome.Value} 金币[/color]\n";
                    break;
                case EventChoiceEffect.GainHp:
                    run.Player.Heal(outcome.Value);
                    AudioManager.Instance?.PlaySfx(AudioManager.SfxPaths.Heal);
                    resultText += $"[color=green]❤ 恢复 {outcome.Value} 生命值[/color]\n";
                    break;
                case EventChoiceEffect.LoseHp:
                    run.Player.TakeDamage(outcome.Value);
                    resultText += $"[color=red]❤ 失去 {outcome.Value} 生命值[/color]\n";
                    break;
                case EventChoiceEffect.GainMaxHp:
                    run.Player.MaxHp += outcome.Value;
                    run.Player.Heal(outcome.Value);
                    resultText += $"[color=green]⬆ 最大生命值 +{outcome.Value}[/color]\n";
                    break;
                case EventChoiceEffect.LoseMaxHp:
                    run.Player.MaxHp = Math.Max(1, run.Player.MaxHp - outcome.Value);
                    if (run.Player.CurrentHp > run.Player.MaxHp)
                        run.Player.CurrentHp = run.Player.MaxHp;
                    resultText += $"[color=red]⬇ 最大生命值 -{outcome.Value}[/color]\n";
                    break;
                case EventChoiceEffect.Nothing:
                    resultText += outcome.FlavorText ?? "什么都没有发生。\n";
                    break;
                default:
                    resultText += outcome.FlavorText ?? $"{outcome.Effect}\n";
                    break;
            }
        }

        _resultLabel.Text = resultText;

        _topBar?.Refresh();
    }

    private static string GetOutcomePreview(EventChoice choice)
    {
        var parts = new List<string>();
        foreach (var o in choice.Outcomes)
        {
            var s = o.Effect switch
            {
                EventChoiceEffect.GainGold => $"💰+{o.Value}",
                EventChoiceEffect.LoseGold => $"💰-{o.Value}",
                EventChoiceEffect.GainHp => $"❤+{o.Value}",
                EventChoiceEffect.LoseHp => $"❤-{o.Value}",
                EventChoiceEffect.GainMaxHp => $"⬆MaxHP+{o.Value}",
                EventChoiceEffect.LoseMaxHp => $"⬇MaxHP-{o.Value}",
                EventChoiceEffect.Nothing => null,
                _ => null
            };
            if (s != null) parts.Add(s);
        }
        return string.Join("  ", parts);
    }

    private void OnContinuePressed()
    {
        AudioManager.Instance?.PlaySfx(AudioManager.SfxPaths.ButtonClick);
        SceneManager.Instance.ChangeScene(SceneManager.Scenes.Map);
    }

    private void AddDecorations()
    {
        // Signal frequency text at bottom
        var freqText = new Label { Text = ">> FREQ_SCAN :: ANOMALY_SIGNAL_DETECTED :: ANALYZING..." };
        freqText.AddThemeFontSizeOverride("font_size", 10);
        freqText.AddThemeColorOverride("font_color", new Color(0.3f, 0.45f, 0.7f, 0.12f));
        freqText.SetAnchorsPreset(LayoutPreset.FullRect);
        freqText.AnchorLeft = 0.02f; freqText.AnchorRight = 0.98f;
        freqText.AnchorTop = 0.96f; freqText.AnchorBottom = 0.99f;
        freqText.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(freqText);

        // Corner brackets on event panel
        var panelNode = GetNode<PanelContainer>("EventPanel");
        var tl = new Label { Text = "┌" };
        tl.AddThemeFontSizeOverride("font_size", 24);
        tl.AddThemeColorOverride("font_color", new Color(0.3f, 0.5f, 0.8f, 0.25f));
        tl.Position = new Vector2(-6, -6);
        tl.MouseFilter = MouseFilterEnum.Ignore;
        panelNode.AddChild(tl);

        var br = new Label { Text = "┘" };
        br.AddThemeFontSizeOverride("font_size", 24);
        br.AddThemeColorOverride("font_color", new Color(0.3f, 0.5f, 0.8f, 0.25f));
        br.SetAnchorsPreset(LayoutPreset.BottomRight);
        br.Position = new Vector2(-10, -22);
        br.MouseFilter = MouseFilterEnum.Ignore;
        panelNode.AddChild(br);
    }

    private void AnimateEntry()
    {
        var panel = GetNode<PanelContainer>("EventPanel");
        panel.Modulate = new Color(1, 1, 1, 0);
        panel.Scale = new Vector2(0.95f, 0.95f);
        panel.PivotOffset = panel.Size / 2;

        var tw = CreateTween();
        tw.TweenProperty(panel, "modulate:a", 1f, 0.4f)
            .SetTrans(Tween.TransitionType.Cubic);
        tw.Parallel().TweenProperty(panel, "scale", Vector2.One, 0.35f)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
    }
}