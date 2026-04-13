using Godot;
using System;

namespace RogueCardGame;

/// <summary>
/// Shared cyberpunk visual effects: particles, scanlines, vignette, glitch.
/// Call static methods to add atmosphere to any Control scene.
/// </summary>
public static class CyberFx
{
    /// <summary>Add a dark vignette overlay to the scene.</summary>
    public static ColorRect AddVignette(Control parent)
    {
        var vignette = new ColorRect();
        vignette.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        vignette.Color = Colors.Transparent;
        vignette.MouseFilter = Control.MouseFilterEnum.Ignore;
        vignette.Material = CreateVignetteMaterial();
        parent.AddChild(vignette);
        return vignette;
    }

    private static ShaderMaterial CreateVignetteMaterial()
    {
        var shader = new Shader();
        shader.Code = @"
shader_type canvas_item;
void fragment() {
    vec2 uv = UV * 2.0 - 1.0;
    float d = dot(uv, uv);
    float v = smoothstep(0.4, 1.8, d);
    COLOR = vec4(0.0, 0.0, 0.0, v * 0.55);
}";
        return new ShaderMaterial { Shader = shader };
    }

    /// <summary>Add animated scanline overlay.</summary>
    public static ColorRect AddScanlines(Control parent, float alpha = 0.06f)
    {
        var rect = new ColorRect();
        rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        rect.Color = Colors.Transparent;
        rect.MouseFilter = Control.MouseFilterEnum.Ignore;

        var shader = new Shader();
        shader.Code = @"
shader_type canvas_item;
uniform float alpha_mult : hint_range(0.0, 0.3) = 0.06;
void fragment() {
    float line = step(0.5, fract(FRAGCOORD.y / 3.0));
    float scroll = fract(TIME * 0.03);
    float band = smoothstep(0.0, 0.1, abs(fract(UV.y + scroll) - 0.5) - 0.4);
    COLOR = vec4(0.0, 0.0, 0.0, line * alpha_mult + band * 0.02);
}";
        var mat = new ShaderMaterial { Shader = shader };
        mat.SetShaderParameter("alpha_mult", alpha);
        rect.Material = mat;
        parent.AddChild(rect);
        return rect;
    }

    /// <summary>Add floating cyber-particles (data fragments, hex chars, circuit dots).</summary>
    public static Control AddParticles(Control parent, int count = 30, Color? tint = null)
    {
        var container = new Control();
        container.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        container.MouseFilter = Control.MouseFilterEnum.Ignore;
        parent.AddChild(container);

        var color = tint ?? new Color(0f, 0.8f, 0.85f, 0.2f);
        var rng = new RandomNumberGenerator();
        rng.Randomize();

        for (int i = 0; i < count; i++)
        {
            float x = rng.RandfRange(0f, 1f);
            float y = rng.RandfRange(0f, 1f);
            float sz = rng.RandfRange(1.5f, 4f);
            float speed = rng.RandfRange(8f, 35f);
            float drift = rng.RandfRange(-15f, 15f);
            float startAlpha = rng.RandfRange(0.08f, 0.35f);

            bool isText = rng.Randf() < 0.3f;

            Control particle;
            if (isText)
            {
                string[] chars = ["0", "1", "A", "F", "//", ">>", "::", "0x", "<<", "##"];
                var lbl = new Label { Text = chars[rng.RandiRange(0, chars.Length - 1)] };
                lbl.AddThemeFontSizeOverride("font_size", (int)(sz * 3.5f));
                lbl.AddThemeColorOverride("font_color", new Color(color.R, color.G, color.B, startAlpha * 0.6f));
                particle = lbl;
            }
            else
            {
                var dot = new ColorRect();
                dot.Size = new Vector2(sz, sz);
                dot.Color = new Color(color.R, color.G, color.B, startAlpha);
                particle = dot;
            }

            particle.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            particle.AnchorLeft = x; particle.AnchorRight = x + 0.002f;
            particle.AnchorTop = y; particle.AnchorBottom = y + 0.002f;
            particle.MouseFilter = Control.MouseFilterEnum.Ignore;
            container.AddChild(particle);

            // Animate: float upward + drift, fade in/out, loop
            AnimateParticle(parent, particle, x, y, speed, drift, startAlpha);
        }

        return container;
    }

    private static void AnimateParticle(Control scene, Control p, float startX, float startY,
        float speed, float drift, float alpha)
    {
        float duration = (1f + startY) * (40f / Mathf.Max(speed, 1f));
        duration = Mathf.Clamp(duration, 2f, 14f);

        var tw = scene.CreateTween().SetLoops();
        tw.TweenProperty(p, "anchor_top", -0.05f, duration)
            .From(startY).SetTrans(Tween.TransitionType.Linear);
        tw.Parallel().TweenProperty(p, "anchor_bottom", -0.05f + 0.002f, duration)
            .From(startY + 0.002f);
        tw.Parallel().TweenProperty(p, "anchor_left", startX + drift * 0.001f, duration)
            .From(startX);
        tw.Parallel().TweenProperty(p, "anchor_right", startX + drift * 0.001f + 0.002f, duration)
            .From(startX + 0.002f);
        tw.Parallel().TweenProperty(p, "modulate:a", 0f, duration * 0.3f)
            .SetDelay(duration * 0.7f);
        // Reset
        tw.TweenProperty(p, "anchor_top", startY, 0f);
        tw.TweenProperty(p, "anchor_bottom", startY + 0.002f, 0f);
        tw.TweenProperty(p, "anchor_left", startX, 0f);
        tw.TweenProperty(p, "anchor_right", startX + 0.002f, 0f);
        tw.TweenProperty(p, "modulate:a", 1f, 0f);
    }

    /// <summary>Create a glow label with animated pulse.</summary>
    public static void PulseGlow(Control node, Color glowColor, float minAlpha = 0.5f, float maxAlpha = 1f, float period = 2f)
    {
        var tw = node.CreateTween().SetLoops();
        tw.TweenProperty(node, "modulate", new Color(1, 1, 1, maxAlpha), period / 2)
            .From(new Color(1, 1, 1, minAlpha))
            .SetTrans(Tween.TransitionType.Sine);
        tw.TweenProperty(node, "modulate", new Color(1, 1, 1, minAlpha), period / 2)
            .SetTrans(Tween.TransitionType.Sine);
    }

    /// <summary>Stagger-animate children: fade in + slide from given direction.</summary>
    public static void StaggerFadeIn(Control scene, Control[] items, float delayEach = 0.08f, float slideY = 20f)
    {
        for (int i = 0; i < items.Length; i++)
        {
            var item = items[i];
            item.Modulate = new Color(1, 1, 1, 0);
            item.Position += new Vector2(0, slideY);
            var tw = scene.CreateTween();
            tw.TweenProperty(item, "modulate:a", 1f, 0.3f).SetDelay(i * delayEach);
            tw.Parallel().TweenProperty(item, "position:y", item.Position.Y - slideY, 0.3f)
                .SetDelay(i * delayEach).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        }
    }
}
