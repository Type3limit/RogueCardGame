"""
Generate detailed cyberpunk-themed SVG assets for RogueCardGame.
Replaces basic placeholder SVGs with richer designs featuring
circuit patterns, neon glow effects, and sci-fi aesthetics.
"""
import os
import math

BASE = os.path.dirname(os.path.abspath(__file__))
TEX = os.path.join(BASE, "resources", "textures")

def write_svg(subdir, name, content):
    d = os.path.join(TEX, subdir)
    os.makedirs(d, exist_ok=True)
    path = os.path.join(d, f"{name}.svg")
    with open(path, "w", encoding="utf-8") as f:
        f.write(content)
    print(f"  -> {subdir}/{name}.svg")


# ─── Shared SVG defs for neon glow ───
NEON_DEFS = """<defs>
  <filter id="glow" x="-50%" y="-50%" width="200%" height="200%">
    <feGaussianBlur stdDeviation="3" result="blur"/>
    <feMerge><feMergeNode in="blur"/><feMergeNode in="SourceGraphic"/></feMerge>
  </filter>
  <filter id="glow-strong" x="-50%" y="-50%" width="200%" height="200%">
    <feGaussianBlur stdDeviation="6" result="blur"/>
    <feMerge><feMergeNode in="blur"/><feMergeNode in="SourceGraphic"/></feMerge>
  </filter>
  <linearGradient id="cyber-bg" x1="0" y1="0" x2="0" y2="1">
    <stop offset="0%" stop-color="#0a0a1a"/>
    <stop offset="100%" stop-color="#050510"/>
  </linearGradient>
</defs>"""

def circuit_lines(w, h, color="#00cccc", opacity=0.15, seed=42):
    """Generate circuit-board-style decorative lines."""
    import random
    rng = random.Random(seed)
    lines = []
    for _ in range(12):
        x1 = rng.randint(0, w)
        y1 = rng.randint(0, h)
        horizontal = rng.choice([True, False])
        length = rng.randint(20, 80)
        if horizontal:
            x2, y2 = min(x1 + length, w), y1
        else:
            x2, y2 = x1, min(y1 + length, h)
        lines.append(f'<line x1="{x1}" y1="{y1}" x2="{x2}" y2="{y2}" stroke="{color}" stroke-width="1" opacity="{opacity}"/>')
        # Add a small dot at junction
        lines.append(f'<circle cx="{x2}" cy="{y2}" r="2" fill="{color}" opacity="{opacity + 0.1}"/>')
    return "\n  ".join(lines)


# ═══════════════════════════════════════
# BACKGROUNDS
# ═══════════════════════════════════════

def gen_backgrounds():
    print("=== Backgrounds ===")

    # Main menu background - dark cityscape silhouette with neon
    write_svg("backgrounds", "main_menu_bg", f'''<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1920 1080" width="1920" height="1080">
{NEON_DEFS}
<rect width="1920" height="1080" fill="url(#cyber-bg)"/>
<!-- Grid floor -->
<g opacity="0.15" stroke="#00cccc" stroke-width="1">
  {"".join(f'<line x1="{x}" y1="600" x2="{960 + (x - 960) * 3}" y2="1080"/>' for x in range(0, 1921, 120))}
  {"".join(f'<line x1="0" y1="{y}" x2="1920" y2="{y}"/>' for y in range(650, 1081, 60))}
</g>
<!-- City silhouette -->
<g fill="#0d0d20" stroke="#00cccc33" stroke-width="1">
  <rect x="100" y="200" width="120" height="400" rx="2"/>
  <rect x="140" y="100" width="60" height="500" rx="2"/>
  <rect x="300" y="250" width="100" height="350" rx="2"/>
  <rect x="450" y="180" width="80" height="420" rx="2"/>
  <rect x="580" y="300" width="150" height="300" rx="2"/>
  <rect x="800" y="150" width="90" height="450" rx="2"/>
  <rect x="950" y="220" width="130" height="380" rx="2"/>
  <rect x="1100" y="280" width="70" height="320" rx="2"/>
  <rect x="1200" y="180" width="110" height="420" rx="2"/>
  <rect x="1350" y="250" width="80" height="350" rx="2"/>
  <rect x="1470" y="150" width="100" height="450" rx="2"/>
  <rect x="1620" y="230" width="90" height="370" rx="2"/>
  <rect x="1750" y="200" width="120" height="400" rx="2"/>
</g>
<!-- Neon signs -->
<g filter="url(#glow)">
  <rect x="310" y="320" width="80" height="20" rx="2" fill="#ff0066" opacity="0.6"/>
  <rect x="810" y="280" width="70" height="15" rx="2" fill="#00cccc" opacity="0.5"/>
  <rect x="1210" y="350" width="90" height="18" rx="2" fill="#cc00ff" opacity="0.5"/>
  <rect x="1480" y="260" width="60" height="16" rx="2" fill="#ffcc00" opacity="0.4"/>
</g>
<!-- Title area glow -->
<ellipse cx="960" cy="400" rx="400" ry="200" fill="#00cccc" opacity="0.03" filter="url(#glow-strong)"/>
</svg>''')

    # Combat background - dark arena with energy grid
    write_svg("backgrounds", "combat_bg", f'''<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1920 1080" width="1920" height="1080">
{NEON_DEFS}
<rect width="1920" height="1080" fill="#060612"/>
<!-- Hex grid pattern -->
<g opacity="0.08" stroke="#00cccc" stroke-width="0.5" fill="none">
  {"".join(f'<polygon points="{x},{y-15} {x+13},{y-7} {x+13},{y+7} {x},{y+15} {x-13},{y+7} {x-13},{y-7}"/>' for x in range(0, 1921, 30) for y in range(0, 1081, 30))}
</g>
<!-- Arena border glow -->
<rect x="40" y="40" width="1840" height="1000" rx="20" fill="none" stroke="#00cccc" stroke-width="2" opacity="0.2" filter="url(#glow)"/>
<rect x="60" y="60" width="1800" height="960" rx="15" fill="none" stroke="#cc00ff" stroke-width="1" opacity="0.1"/>
<!-- Center divider -->
<line x1="0" y1="540" x2="1920" y2="540" stroke="#ff006644" stroke-width="2" stroke-dasharray="20,10"/>
<!-- Energy particles (decorative circles) -->
<g filter="url(#glow)">
  <circle cx="200" cy="300" r="3" fill="#00cccc" opacity="0.4"/>
  <circle cx="500" cy="150" r="2" fill="#cc00ff" opacity="0.3"/>
  <circle cx="1400" cy="200" r="3" fill="#00cccc" opacity="0.4"/>
  <circle cx="1700" cy="400" r="2" fill="#ff0066" opacity="0.3"/>
  <circle cx="900" cy="100" r="4" fill="#00cccc" opacity="0.2"/>
</g>
</svg>''')

    # Map background - network / data highway visualization
    write_svg("backgrounds", "map_bg", f'''<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1920 1080" width="1920" height="1080">
{NEON_DEFS}
<rect width="1920" height="1080" fill="#05050f"/>
<!-- Datastream vertical lines -->
<g opacity="0.06" stroke="#00cccc" stroke-width="1">
  {"".join(f'<line x1="{x}" y1="0" x2="{x}" y2="1080"/>' for x in range(0, 1921, 60))}
</g>
<!-- Subtle horizontal data ribbons -->
<g opacity="0.04" fill="#cc00ff">
  <rect x="0" y="100" width="1920" height="2"/>
  <rect x="0" y="300" width="1920" height="1"/>
  <rect x="0" y="500" width="1920" height="2"/>
  <rect x="0" y="700" width="1920" height="1"/>
  <rect x="0" y="900" width="1920" height="2"/>
</g>
<!-- Scattered data nodes -->
<g filter="url(#glow)" opacity="0.15">
  {"".join(f'<circle cx="{x}" cy="{y}" r="1.5" fill="#00cccc"/>' for x, y in [(150,200),(400,450),(750,180),(1100,650),(1500,350),(1800,800),(300,900),(600,700),(1300,150),(960,540)])}
</g>
</svg>''')

    # Shop background
    write_svg("backgrounds", "shop_bg", f'''<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1920 1080" width="1920" height="1080">
{NEON_DEFS}
<rect width="1920" height="1080" fill="#0a0814"/>
<!-- Shop shelves suggestion -->
<g opacity="0.12" stroke="#ffcc00" stroke-width="1" fill="none">
  <rect x="100" y="100" width="400" height="300" rx="5"/>
  <rect x="100" y="450" width="400" height="300" rx="5"/>
  <rect x="1420" y="100" width="400" height="300" rx="5"/>
  <rect x="1420" y="450" width="400" height="300" rx="5"/>
</g>
<!-- Neon shop sign -->
<g filter="url(#glow)">
  <rect x="760" y="40" width="400" height="60" rx="8" fill="none" stroke="#ffcc00" stroke-width="2" opacity="0.5"/>
  <text x="960" y="80" text-anchor="middle" font-family="monospace" font-size="28" fill="#ffcc00" opacity="0.7" filter="url(#glow)">CYBER SHOP</text>
</g>
{circuit_lines(1920, 1080, "#ffcc00", 0.08, 99)}
</svg>''')

    # Event background
    write_svg("backgrounds", "event_bg", f'''<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1920 1080" width="1920" height="1080">
{NEON_DEFS}
<rect width="1920" height="1080" fill="#08060f"/>
<!-- Mystery aura -->
<radialGradient id="mystery" cx="50%" cy="50%">
  <stop offset="0%" stop-color="#cc00ff" stop-opacity="0.08"/>
  <stop offset="100%" stop-color="#cc00ff" stop-opacity="0"/>
</radialGradient>
<ellipse cx="960" cy="540" rx="600" ry="400" fill="url(#mystery)"/>
<!-- Glitch-like horizontal lines -->
<g opacity="0.1">
  {"".join(f'<rect x="{100 + (i*137) % 800}" y="{200 + i * 60}" width="{200 + (i*73) % 300}" height="1" fill="#cc00ff"/>' for i in range(12))}
</g>
{circuit_lines(1920, 1080, "#cc00ff", 0.06, 77)}
</svg>''')

    # Rest background
    write_svg("backgrounds", "rest_bg", f'''<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1920 1080" width="1920" height="1080">
{NEON_DEFS}
<rect width="1920" height="1080" fill="#060810"/>
<!-- Soft warm center glow -->
<radialGradient id="campfire" cx="50%" cy="60%">
  <stop offset="0%" stop-color="#ff6600" stop-opacity="0.06"/>
  <stop offset="60%" stop-color="#ff6600" stop-opacity="0.02"/>
  <stop offset="100%" stop-color="#ff6600" stop-opacity="0"/>
</radialGradient>
<ellipse cx="960" cy="650" rx="500" ry="300" fill="url(#campfire)"/>
<!-- Stars / data points -->
<g fill="#ffffff" opacity="0.3">
  {"".join(f'<circle cx="{(i*257) % 1920}" cy="{(i*131) % 400}" r="{0.5 + (i % 3) * 0.5}"/>' for i in range(30))}
</g>
</svg>''')

# ═══════════════════════════════════════
# CHARACTERS (more detailed cyberpunk silhouettes)
# ═══════════════════════════════════════

def gen_characters():
    print("=== Characters ===")

    # Netrunner - sleek hacker with visor and data cables
    write_svg("characters", "netrunner", f'''<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 128 192" width="128" height="192">
{NEON_DEFS}
<rect width="128" height="192" rx="4" fill="#0a0a1a"/>
{circuit_lines(128, 192, "#00cccc", 0.1, 10)}
<!-- Body shadow -->
<ellipse cx="64" cy="180" rx="30" ry="6" fill="#00cccc" opacity="0.1" filter="url(#glow)"/>
<!-- Legs -->
<path d="M50,145 L45,180 L55,180 L58,145" fill="#0d1a2a" stroke="#00cccc" stroke-width="0.5" opacity="0.8"/>
<path d="M70,145 L73,180 L83,180 L78,145" fill="#0d1a2a" stroke="#00cccc" stroke-width="0.5" opacity="0.8"/>
<!-- Torso - armored jacket -->
<path d="M40,75 L38,145 L88,145 L86,75 Z" fill="#0d1a2a" stroke="#00cccc" stroke-width="1" opacity="0.9"/>
<!-- Jacket details -->
<line x1="64" y1="80" x2="64" y2="140" stroke="#00cccc" stroke-width="0.5" opacity="0.3"/>
<rect x="48" y="95" width="32" height="3" rx="1" fill="#00cccc" opacity="0.2"/>
<rect x="50" y="105" width="28" height="2" rx="1" fill="#00cccc" opacity="0.15"/>
<!-- Arms -->
<path d="M40,80 L25,120 L30,122 L42,85" fill="#0d1a2a" stroke="#00cccc" stroke-width="0.5" opacity="0.7"/>
<path d="M86,80 L100,115 L95,118 L84,85" fill="#0d1a2a" stroke="#00cccc" stroke-width="0.5" opacity="0.7"/>
<!-- Data cables from right arm -->
<path d="M98,116 Q110,100 105,80" fill="none" stroke="#00cccc" stroke-width="1" opacity="0.4" filter="url(#glow)"/>
<path d="M96,118 Q112,105 108,88" fill="none" stroke="#00cccc" stroke-width="0.5" opacity="0.3"/>
<!-- Head -->
<ellipse cx="64" cy="55" rx="18" ry="22" fill="#0d1a2a" stroke="#00cccc" stroke-width="1"/>
<!-- Visor / cyber eyes -->
<rect x="46" y="48" width="36" height="8" rx="4" fill="#00cccc" opacity="0.7" filter="url(#glow)"/>
<!-- Hair / hood outline -->
<path d="M46,45 Q64,25 82,45" fill="none" stroke="#00cccc" stroke-width="1.5" opacity="0.5"/>
<!-- Floating holographic display near hand -->
<g filter="url(#glow)" opacity="0.5">
  <rect x="15" y="105" width="18" height="12" rx="1" fill="none" stroke="#00cccc" stroke-width="0.5"/>
  <line x1="17" y1="109" x2="31" y2="109" stroke="#00cccc" stroke-width="0.3"/>
  <line x1="17" y1="112" x2="27" y2="112" stroke="#00cccc" stroke-width="0.3"/>
</g>
<text x="64" y="12" text-anchor="middle" font-size="7" fill="#00cccc" opacity="0.5" font-family="monospace">NETRUNNER</text>
</svg>''')

    # Vanguard - heavy armored frontline warrior
    write_svg("characters", "vanguard", f'''<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 128 192" width="128" height="192">
{NEON_DEFS}
<rect width="128" height="192" rx="4" fill="#0a0a1a"/>
{circuit_lines(128, 192, "#ff6600", 0.1, 20)}
<ellipse cx="64" cy="180" rx="35" ry="6" fill="#ff6600" opacity="0.1" filter="url(#glow)"/>
<!-- Legs - heavy armored -->
<path d="M44,140 L38,180 L56,180 L54,140" fill="#1a1008" stroke="#ff6600" stroke-width="0.5" opacity="0.9"/>
<path d="M74,140 L76,180 L92,180 L86,140" fill="#1a1008" stroke="#ff6600" stroke-width="0.5" opacity="0.9"/>
<!-- Armor plates on legs -->
<rect x="40" y="155" width="14" height="8" rx="2" fill="#ff6600" opacity="0.15"/>
<rect x="78" y="155" width="14" height="8" rx="2" fill="#ff6600" opacity="0.15"/>
<!-- Torso - heavy power armor -->
<path d="M32,70 L28,140 L100,140 L96,70 Z" fill="#1a1008" stroke="#ff6600" stroke-width="1.5" opacity="0.9"/>
<!-- Chest plate -->
<path d="M40,75 L64,85 L88,75 L88,110 L64,115 L40,110 Z" fill="#ff6600" opacity="0.1" stroke="#ff6600" stroke-width="0.5"/>
<!-- Energy core in chest -->
<circle cx="64" cy="95" r="8" fill="none" stroke="#ff6600" stroke-width="1.5" opacity="0.6" filter="url(#glow)"/>
<circle cx="64" cy="95" r="4" fill="#ff6600" opacity="0.4" filter="url(#glow)"/>
<!-- Shoulders - big pauldrons -->
<ellipse cx="30" cy="78" rx="14" ry="10" fill="#1a1008" stroke="#ff6600" stroke-width="1" opacity="0.9"/>
<ellipse cx="98" cy="78" rx="14" ry="10" fill="#1a1008" stroke="#ff6600" stroke-width="1" opacity="0.9"/>
<!-- Arms -->
<path d="M28,85 L18,130 L28,132 L35,88" fill="#1a1008" stroke="#ff6600" stroke-width="0.5" opacity="0.8"/>
<path d="M100,85 L108,125 L100,128 L95,88" fill="#1a1008" stroke="#ff6600" stroke-width="0.5" opacity="0.8"/>
<!-- Shield in left hand -->
<path d="M8,110 L18,95 L28,110 L18,135 Z" fill="#1a1008" stroke="#ff6600" stroke-width="1" opacity="0.7" filter="url(#glow)"/>
<!-- Head - helmeted -->
<path d="M46,40 L46,65 Q64,75 82,65 L82,40 Q64,30 46,40" fill="#1a1008" stroke="#ff6600" stroke-width="1"/>
<!-- Visor slit -->
<rect x="48" y="50" width="32" height="5" rx="2" fill="#ff6600" opacity="0.6" filter="url(#glow)"/>
<text x="64" y="12" text-anchor="middle" font-size="7" fill="#ff6600" opacity="0.5" font-family="monospace">VANGUARD</text>
</svg>''')

    # Psion - mystical / psychic energy wielder
    write_svg("characters", "psion", f'''<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 128 192" width="128" height="192">
{NEON_DEFS}
<rect width="128" height="192" rx="4" fill="#0a0a1a"/>
{circuit_lines(128, 192, "#cc00ff", 0.1, 30)}
<ellipse cx="64" cy="180" rx="28" ry="6" fill="#cc00ff" opacity="0.1" filter="url(#glow)"/>
<!-- Floating cloak / robes -->
<path d="M36,70 L30,180 L98,180 L92,70 Q64,60 36,70" fill="#12061a" stroke="#cc00ff" stroke-width="1" opacity="0.9"/>
<!-- Inner robe glow -->
<path d="M44,80 L40,170 L88,170 L84,80 Q64,72 44,80" fill="#cc00ff" opacity="0.04"/>
<!-- Robe energy lines -->
<path d="M50,90 L46,160" stroke="#cc00ff" stroke-width="0.5" opacity="0.2"/>
<path d="M78,90 L82,160" stroke="#cc00ff" stroke-width="0.5" opacity="0.2"/>
<!-- Arms raised, channeling -->
<path d="M36,78 L15,60 L20,55 L40,72" fill="#12061a" stroke="#cc00ff" stroke-width="0.5" opacity="0.7"/>
<path d="M92,78 L113,60 L108,55 L88,72" fill="#12061a" stroke="#cc00ff" stroke-width="0.5" opacity="0.7"/>
<!-- Psychic energy orbs at hands -->
<circle cx="17" cy="57" r="8" fill="#cc00ff" opacity="0.2" filter="url(#glow-strong)"/>
<circle cx="17" cy="57" r="4" fill="#cc00ff" opacity="0.4" filter="url(#glow)"/>
<circle cx="111" cy="57" r="8" fill="#cc00ff" opacity="0.2" filter="url(#glow-strong)"/>
<circle cx="111" cy="57" r="4" fill="#cc00ff" opacity="0.4" filter="url(#glow)"/>
<!-- Energy connection between hands -->
<path d="M25,57 Q64,30 103,57" fill="none" stroke="#cc00ff" stroke-width="1" opacity="0.3" filter="url(#glow)" stroke-dasharray="4,3"/>
<!-- Head - ethereal, with third eye -->
<ellipse cx="64" cy="48" rx="16" ry="20" fill="#12061a" stroke="#cc00ff" stroke-width="1"/>
<!-- Eyes -->
<ellipse cx="56" cy="46" rx="4" ry="2" fill="#cc00ff" opacity="0.6" filter="url(#glow)"/>
<ellipse cx="72" cy="46" rx="4" ry="2" fill="#cc00ff" opacity="0.6" filter="url(#glow)"/>
<!-- Third eye -->
<circle cx="64" cy="36" r="3" fill="#cc00ff" opacity="0.5" filter="url(#glow-strong)"/>
<!-- Psychic aura -->
<ellipse cx="64" cy="90" rx="50" ry="70" fill="none" stroke="#cc00ff" stroke-width="0.5" opacity="0.1" stroke-dasharray="3,5"/>
<text x="64" y="12" text-anchor="middle" font-size="7" fill="#cc00ff" opacity="0.5" font-family="monospace">PSION</text>
</svg>''')

    # Symbiote - organic/tech hybrid with tendrils
    write_svg("characters", "symbiote", f'''<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 128 192" width="128" height="192">
{NEON_DEFS}
<rect width="128" height="192" rx="4" fill="#0a0a1a"/>
{circuit_lines(128, 192, "#00ff66", 0.1, 40)}
<ellipse cx="64" cy="180" rx="30" ry="6" fill="#00ff66" opacity="0.1" filter="url(#glow)"/>
<!-- Organic tendrils from back -->
<g stroke="#00ff66" stroke-width="1.5" fill="none" opacity="0.4" filter="url(#glow)">
  <path d="M50,70 Q30,40 15,50 Q5,60 10,80"/>
  <path d="M78,70 Q98,35 110,45 Q120,55 115,75"/>
  <path d="M55,65 Q40,30 45,15"/>
  <path d="M73,65 Q88,30 83,15"/>
</g>
<!-- Legs - asymmetric, partially organic -->
<path d="M48,140 L42,175 L55,178 L56,140" fill="#0a1a0d" stroke="#00ff66" stroke-width="0.5" opacity="0.8"/>
<path d="M72,140 L78,178 L88,175 L82,140" fill="#0a1a0d" stroke="#00ff66" stroke-width="0.5" opacity="0.8"/>
<!-- Organic pattern on legs -->
<path d="M46,155 Q50,150 54,155 Q50,160 46,155" fill="#00ff66" opacity="0.15"/>
<!-- Torso - organic/tech hybrid -->
<path d="M38,68 L34,140 L94,140 L90,68 Q64,58 38,68" fill="#0a1a0d" stroke="#00ff66" stroke-width="1" opacity="0.9"/>
<!-- Bio-circuit veins on torso -->
<g stroke="#00ff66" stroke-width="0.8" fill="none" opacity="0.3">
  <path d="M50,80 Q55,95 50,110 Q55,125 52,135"/>
  <path d="M78,80 Q73,95 78,110 Q73,125 76,135"/>
  <path d="M60,75 L60,90 Q64,100 68,90 L68,75"/>
</g>
<!-- Symbiote core -->
<ellipse cx="64" cy="100" rx="10" ry="12" fill="#00ff66" opacity="0.15" filter="url(#glow)"/>
<ellipse cx="64" cy="100" rx="5" ry="6" fill="#00ff66" opacity="0.3" filter="url(#glow)"/>
<!-- Arms with bio-armor -->
<path d="M38,72 L22,115 L30,118 L42,78" fill="#0a1a0d" stroke="#00ff66" stroke-width="0.5" opacity="0.8"/>
<path d="M90,72 L106,115 L98,118 L86,78" fill="#0a1a0d" stroke="#00ff66" stroke-width="0.5" opacity="0.8"/>
<!-- Head - partially covered in symbiote mass -->
<ellipse cx="64" cy="48" rx="18" ry="22" fill="#0a1a0d" stroke="#00ff66" stroke-width="1"/>
<!-- Symbiote coverage on head -->
<path d="M46,42 Q55,30 64,35 Q73,30 82,42 L78,50 Q64,45 50,50 Z" fill="#00ff66" opacity="0.12"/>
<!-- Glowing eyes -->
<ellipse cx="55" cy="48" rx="3" ry="4" fill="#00ff66" opacity="0.7" filter="url(#glow)"/>
<ellipse cx="73" cy="48" rx="3" ry="4" fill="#00ff66" opacity="0.7" filter="url(#glow)"/>
<text x="64" y="12" text-anchor="middle" font-size="7" fill="#00ff66" opacity="0.5" font-family="monospace">SYMBIOTE</text>
</svg>''')


# ═══════════════════════════════════════
# CARDS
# ═══════════════════════════════════════

def gen_cards():
    print("=== Cards ===")

    def card_template(name, main_color, icon_svg, type_name):
        return f'''<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 200 280" width="200" height="280">
{NEON_DEFS}
<!-- Card base -->
<rect width="200" height="280" rx="12" fill="#0a0a1a" stroke="{main_color}" stroke-width="2"/>
<!-- Inner frame -->
<rect x="8" y="8" width="184" height="264" rx="8" fill="none" stroke="{main_color}" stroke-width="0.5" opacity="0.3"/>
<!-- Top bar - card type -->
<rect x="12" y="12" width="176" height="24" rx="4" fill="{main_color}" opacity="0.1"/>
<text x="100" y="28" text-anchor="middle" font-family="monospace" font-size="10" fill="{main_color}" opacity="0.7">{type_name}</text>
<!-- Art area -->
<rect x="16" y="42" width="168" height="120" rx="6" fill="#060612" stroke="{main_color}" stroke-width="0.5" opacity="0.5"/>
{icon_svg}
<!-- Energy cost circle -->
<circle cx="28" cy="28" r="14" fill="#0a0a1a" stroke="{main_color}" stroke-width="1.5"/>
<circle cx="28" cy="28" r="10" fill="{main_color}" opacity="0.15"/>
<!-- Description area -->
<rect x="16" y="170" width="168" height="80" rx="4" fill="{main_color}" opacity="0.05"/>
<line x1="30" y1="190" x2="170" y2="190" stroke="{main_color}" stroke-width="0.3" opacity="0.3"/>
<line x1="30" y1="205" x2="160" y2="205" stroke="{main_color}" stroke-width="0.3" opacity="0.2"/>
<line x1="30" y1="220" x2="150" y2="220" stroke="{main_color}" stroke-width="0.3" opacity="0.15"/>
<!-- Bottom decorative -->
<rect x="60" y="258" width="80" height="3" rx="1" fill="{main_color}" opacity="0.2"/>
<!-- Corner circuits -->
<g stroke="{main_color}" stroke-width="0.5" opacity="0.2">
  <path d="M12,270 L12,260 L22,260"/>
  <path d="M188,270 L188,260 L178,260"/>
  <path d="M12,10 L12,20 L22,20"/>
  <path d="M188,10 L188,20 L178,20"/>
</g>
</svg>'''

    # Attack card
    attack_icon = '''<g transform="translate(100,102)" filter="url(#glow)">
  <path d="M-20,-30 L5,-10 L-5,0 L20,30 L10,10 L20,0 Z" fill="#ff3344" opacity="0.7"/>
  <path d="M-15,-20 L0,-5 L15,-20" fill="none" stroke="#ff3344" stroke-width="1" opacity="0.4"/>
</g>'''
    write_svg("cards", "card_attack", card_template("Attack", "#ff3344", attack_icon, "ATTACK"))

    # Skill card
    skill_icon = '''<g transform="translate(100,102)" filter="url(#glow)">
  <circle r="20" fill="none" stroke="#3388ff" stroke-width="2" opacity="0.6"/>
  <circle r="12" fill="none" stroke="#3388ff" stroke-width="1" opacity="0.4"/>
  <circle r="5" fill="#3388ff" opacity="0.5"/>
  <line x1="-25" y1="0" x2="-15" y2="0" stroke="#3388ff" stroke-width="1" opacity="0.3"/>
  <line x1="15" y1="0" x2="25" y2="0" stroke="#3388ff" stroke-width="1" opacity="0.3"/>
  <line x1="0" y1="-25" x2="0" y2="-15" stroke="#3388ff" stroke-width="1" opacity="0.3"/>
  <line x1="0" y1="15" x2="0" y2="25" stroke="#3388ff" stroke-width="1" opacity="0.3"/>
</g>'''
    write_svg("cards", "card_skill", card_template("Skill", "#3388ff", skill_icon, "SKILL"))

    # Power card
    power_icon = '''<g transform="translate(100,102)" filter="url(#glow)">
  <polygon points="0,-28 8,-8 28,-8 12,6 18,26 0,14 -18,26 -12,6 -28,-8 -8,-8" fill="#ffaa00" opacity="0.5"/>
  <polygon points="0,-18 5,-5 18,-5 8,4 12,17 0,9 -12,17 -8,4 -18,-5 -5,-5" fill="#ffaa00" opacity="0.3"/>
</g>'''
    write_svg("cards", "card_power", card_template("Power", "#ffaa00", power_icon, "POWER"))


# ═══════════════════════════════════════
# MAP NODES
# ═══════════════════════════════════════

def gen_map_nodes():
    print("=== Map Nodes ===")

    def map_node(name, color, inner_svg):
        return f'''<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64" width="64" height="64">
{NEON_DEFS}
<!-- Outer ring -->
<circle cx="32" cy="32" r="28" fill="#0a0a1a" stroke="{color}" stroke-width="2" opacity="0.9"/>
<!-- Inner ring -->
<circle cx="32" cy="32" r="22" fill="{color}" opacity="0.08"/>
{inner_svg}
<!-- Corner ticks -->
<g stroke="{color}" stroke-width="1" opacity="0.3">
  <line x1="32" y1="2" x2="32" y2="8"/>
  <line x1="32" y1="56" x2="32" y2="62"/>
  <line x1="2" y1="32" x2="8" y2="32"/>
  <line x1="56" y1="32" x2="62" y2="32"/>
</g>
</svg>'''

    # Combat node - sword icon
    write_svg("map", "node_combat", map_node("combat", "#ff3344",
        '<g filter="url(#glow)"><path d="M22,22 L42,42 M40,24 L42,22 L44,24 M20,40 L22,42 L24,40" stroke="#ff3344" stroke-width="2" fill="none" opacity="0.7"/></g>'))

    # Elite node - double swords
    write_svg("map", "node_elite", map_node("elite", "#ff6600",
        '<g filter="url(#glow)"><path d="M20,20 L44,44 M44,20 L20,44" stroke="#ff6600" stroke-width="2.5" fill="none" opacity="0.7"/><circle cx="32" cy="32" r="4" fill="#ff6600" opacity="0.4"/></g>'))

    # Boss node - skull-like
    write_svg("map", "node_boss", map_node("boss", "#ff0044",
        '<g filter="url(#glow-strong)"><circle cx="32" cy="30" r="12" fill="none" stroke="#ff0044" stroke-width="2" opacity="0.7"/><rect x="25" y="27" width="5" height="5" rx="1" fill="#ff0044" opacity="0.6"/><rect x="34" y="27" width="5" height="5" rx="1" fill="#ff0044" opacity="0.6"/><path d="M28,37 L32,40 L36,37" fill="none" stroke="#ff0044" stroke-width="1" opacity="0.5"/></g>'))

    # Event node - ? mark
    write_svg("map", "node_event", map_node("event", "#cc00ff",
        '<text x="32" y="40" text-anchor="middle" font-family="monospace" font-size="24" font-weight="bold" fill="#cc00ff" opacity="0.7" filter="url(#glow)">?</text>'))

    # Shop node - credit symbol
    write_svg("map", "node_shop", map_node("shop", "#ffcc00",
        '<text x="32" y="40" text-anchor="middle" font-family="monospace" font-size="22" font-weight="bold" fill="#ffcc00" opacity="0.7" filter="url(#glow)">$</text>'))

    # Rest node - camp/recharge
    write_svg("map", "node_rest", map_node("rest", "#00ff88",
        '''<g filter="url(#glow)">
  <path d="M22,40 L32,28 L42,40" fill="none" stroke="#00ff88" stroke-width="2" opacity="0.6"/>
  <line x1="32" y1="28" x2="32" y2="22" stroke="#00ff88" stroke-width="1.5" opacity="0.5"/>
  <circle cx="32" cy="20" r="3" fill="#00ff88" opacity="0.4"/>
</g>'''))

    # Treasure node
    write_svg("map", "node_treasure", map_node("treasure", "#ffdd44",
        '''<g filter="url(#glow)">
  <rect x="22" y="28" width="20" height="14" rx="2" fill="none" stroke="#ffdd44" stroke-width="1.5" opacity="0.6"/>
  <path d="M22,28 L27,22 L37,22 L42,28" fill="none" stroke="#ffdd44" stroke-width="1.5" opacity="0.6"/>
  <circle cx="32" cy="35" r="3" fill="#ffdd44" opacity="0.5"/>
</g>'''))


# ═══════════════════════════════════════
# ENEMIES
# ═══════════════════════════════════════

def gen_enemies():
    print("=== Enemies ===")

    # Generic enemy - cyber drone / robot
    write_svg("enemies", "enemy_generic", f'''<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 128 160" width="128" height="160">
{NEON_DEFS}
<rect width="128" height="160" rx="4" fill="#0a0a1a"/>
{circuit_lines(128, 160, "#ff3344", 0.08, 50)}
<!-- Hover glow -->
<ellipse cx="64" cy="150" rx="25" ry="5" fill="#ff3344" opacity="0.15" filter="url(#glow)"/>
<!-- Body - angular robot -->
<path d="M44,60 L34,110 L50,120 L78,120 L94,110 L84,60 Z" fill="#1a0a0a" stroke="#ff3344" stroke-width="1" opacity="0.9"/>
<!-- Chest plate -->
<path d="M50,70 L64,65 L78,70 L78,100 L64,105 L50,100 Z" fill="#ff3344" opacity="0.08" stroke="#ff3344" stroke-width="0.5"/>
<!-- Eyes - menacing -->
<rect x="44" y="38" width="40" height="20" rx="4" fill="#1a0a0a" stroke="#ff3344" stroke-width="1"/>
<rect x="48" y="42" width="12" height="10" rx="2" fill="#ff3344" opacity="0.6" filter="url(#glow)"/>
<rect x="68" y="42" width="12" height="10" rx="2" fill="#ff3344" opacity="0.6" filter="url(#glow)"/>
<!-- Head top -->
<path d="M44,38 L54,25 L74,25 L84,38" fill="#1a0a0a" stroke="#ff3344" stroke-width="0.5"/>
<!-- Arms - mechanical -->
<path d="M34,70 L20,95 L28,100 L38,75" fill="#1a0a0a" stroke="#ff3344" stroke-width="0.5" opacity="0.8"/>
<path d="M94,70 L108,95 L100,100 L90,75" fill="#1a0a0a" stroke="#ff3344" stroke-width="0.5" opacity="0.8"/>
<!-- Weapon -->
<rect x="104" y="88" width="18" height="4" rx="1" fill="#ff3344" opacity="0.4" filter="url(#glow)"/>
</svg>''')

    # Boss enemy - large, imposing cyber-mech
    write_svg("enemies", "enemy_boss", f'''<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 192 224" width="192" height="224">
{NEON_DEFS}
<rect width="192" height="224" rx="4" fill="#0a0a1a"/>
{circuit_lines(192, 224, "#ff0044", 0.08, 60)}
<!-- Ground impact glow -->
<ellipse cx="96" cy="210" rx="60" ry="8" fill="#ff0044" opacity="0.15" filter="url(#glow-strong)"/>
<!-- Massive legs -->
<path d="M55,160 L45,205 L70,208 L68,160" fill="#1a0808" stroke="#ff0044" stroke-width="1" opacity="0.9"/>
<path d="M124,160 L122,208 L147,205 L137,160" fill="#1a0808" stroke="#ff0044" stroke-width="1" opacity="0.9"/>
<!-- Torso - massive power armor -->
<path d="M35,65 L25,160 L167,160 L157,65 Z" fill="#1a0808" stroke="#ff0044" stroke-width="2" opacity="0.9"/>
<!-- Core reactor -->
<circle cx="96" cy="110" r="18" fill="none" stroke="#ff0044" stroke-width="2" opacity="0.5" filter="url(#glow)"/>
<circle cx="96" cy="110" r="10" fill="#ff0044" opacity="0.3" filter="url(#glow-strong)"/>
<circle cx="96" cy="110" r="5" fill="#ff0044" opacity="0.5"/>
<!-- Armor plates -->
<path d="M45,75 L96,90 L147,75 L147,130 L96,140 L45,130 Z" fill="#ff0044" opacity="0.06" stroke="#ff0044" stroke-width="0.5"/>
<!-- Giant pauldrons -->
<ellipse cx="25" cy="72" rx="22" ry="16" fill="#1a0808" stroke="#ff0044" stroke-width="1.5"/>
<ellipse cx="167" cy="72" rx="22" ry="16" fill="#1a0808" stroke="#ff0044" stroke-width="1.5"/>
<!-- Spikes on shoulders -->
<path d="M15,60 L5,45 L25,56" fill="#1a0808" stroke="#ff0044" stroke-width="1"/>
<path d="M177,60 L187,45 L167,56" fill="#1a0808" stroke="#ff0044" stroke-width="1"/>
<!-- Arms -->
<path d="M25,82 L5,140 L18,145 L32,88" fill="#1a0808" stroke="#ff0044" stroke-width="0.5" opacity="0.8"/>
<path d="M167,82 L187,140 L174,145 L160,88" fill="#1a0808" stroke="#ff0044" stroke-width="0.5" opacity="0.8"/>
<!-- Head - menacing helmet -->
<path d="M66,30 L66,62 Q96,72 126,62 L126,30 Q96,18 66,30" fill="#1a0808" stroke="#ff0044" stroke-width="1.5"/>
<!-- Visor -->
<rect x="70" y="40" width="52" height="10" rx="3" fill="#ff0044" opacity="0.7" filter="url(#glow-strong)"/>
<!-- Crown/horns -->
<path d="M70,30 L60,15" stroke="#ff0044" stroke-width="2" opacity="0.5"/>
<path d="M122,30 L132,15" stroke="#ff0044" stroke-width="2" opacity="0.5"/>
<!-- Danger aura -->
<circle cx="96" cy="112" r="80" fill="none" stroke="#ff0044" stroke-width="0.5" opacity="0.05" stroke-dasharray="5,5"/>
<text x="96" y="14" text-anchor="middle" font-size="8" fill="#ff0044" opacity="0.4" font-family="monospace">BOSS</text>
</svg>''')


# ═══════════════════════════════════════
# INTENT ICONS
# ═══════════════════════════════════════

def gen_intents():
    print("=== Intent Icons ===")

    # Attack intent
    write_svg("effects", "intent_attack", f'''<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 48 48" width="48" height="48">
{NEON_DEFS}
<g filter="url(#glow)">
  <path d="M10,38 L24,8 L38,38 Z" fill="none" stroke="#ff3344" stroke-width="2.5" opacity="0.8"/>
  <line x1="24" y1="18" x2="24" y2="30" stroke="#ff3344" stroke-width="2" opacity="0.7"/>
  <circle cx="24" cy="34" r="2" fill="#ff3344" opacity="0.7"/>
</g>
</svg>''')

    # Defend intent
    write_svg("effects", "intent_defend", f'''<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 48 48" width="48" height="48">
{NEON_DEFS}
<g filter="url(#glow)">
  <path d="M24,6 L40,16 L40,30 Q40,42 24,46 Q8,42 8,30 L8,16 Z" fill="#3388ff" opacity="0.15" stroke="#3388ff" stroke-width="2"/>
  <path d="M24,14 L34,20 L34,28 Q34,36 24,40 Q14,36 14,28 L14,20 Z" fill="none" stroke="#3388ff" stroke-width="1" opacity="0.4"/>
</g>
</svg>''')

    # Buff intent
    write_svg("effects", "intent_buff", f'''<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 48 48" width="48" height="48">
{NEON_DEFS}
<g filter="url(#glow)">
  <path d="M24,6 L28,18 L40,18 L30,26 L34,38 L24,30 L14,38 L18,26 L8,18 L20,18 Z" fill="#ffaa00" opacity="0.3" stroke="#ffaa00" stroke-width="1.5"/>
</g>
</svg>''')


# ═══════════════════════════════════════
# UI ICONS
# ═══════════════════════════════════════

def gen_ui_icons():
    print("=== UI Icons ===")

    # HP icon - heart with circuit
    write_svg("ui", "icon_hp", f'''<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32" width="32" height="32">
{NEON_DEFS}
<g filter="url(#glow)">
  <path d="M16,28 Q2,18 2,10 Q2,4 8,4 Q12,4 16,10 Q20,4 24,4 Q30,4 30,10 Q30,18 16,28" fill="#ff3344" opacity="0.6" stroke="#ff3344" stroke-width="1"/>
  <path d="M16,12 L16,22" stroke="#ffffff" stroke-width="0.5" opacity="0.3"/>
  <path d="M10,16 L22,16" stroke="#ffffff" stroke-width="0.5" opacity="0.3"/>
</g>
</svg>''')

    # Attack icon - lightning bolt
    write_svg("ui", "icon_attack", f'''<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32" width="32" height="32">
{NEON_DEFS}
<g filter="url(#glow)">
  <path d="M18,2 L8,16 L14,16 L12,30 L24,14 L18,14 Z" fill="#ff6600" opacity="0.7" stroke="#ff6600" stroke-width="0.5"/>
</g>
</svg>''')

    # Defend/shield icon
    write_svg("ui", "icon_defend", f'''<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32" width="32" height="32">
{NEON_DEFS}
<g filter="url(#glow)">
  <path d="M16,3 L28,9 L28,18 Q28,27 16,30 Q4,27 4,18 L4,9 Z" fill="#3388ff" opacity="0.5" stroke="#3388ff" stroke-width="1"/>
  <path d="M16,8 L24,12 L24,18 Q24,24 16,26 Q8,24 8,18 L8,12 Z" fill="none" stroke="#3388ff" stroke-width="0.5" opacity="0.4"/>
</g>
</svg>''')

    # Energy icon - battery/energy cell
    write_svg("ui", "icon_energy", f'''<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32" width="32" height="32">
{NEON_DEFS}
<g filter="url(#glow)">
  <rect x="6" y="6" width="20" height="22" rx="3" fill="none" stroke="#ffcc00" stroke-width="1.5" opacity="0.7"/>
  <rect x="12" y="3" width="8" height="5" rx="1" fill="#ffcc00" opacity="0.4"/>
  <rect x="10" y="14" width="12" height="10" rx="1" fill="#ffcc00" opacity="0.4"/>
  <path d="M16,10 L13,16 L15,16 L14,22 L19,15 L17,15 Z" fill="#ffcc00" opacity="0.6"/>
</g>
</svg>''')

    # Gold icon - credit chip
    write_svg("ui", "icon_gold", f'''<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32" width="32" height="32">
{NEON_DEFS}
<g filter="url(#glow)">
  <circle cx="16" cy="16" r="13" fill="#ffcc00" opacity="0.2" stroke="#ffcc00" stroke-width="1.5"/>
  <circle cx="16" cy="16" r="9" fill="none" stroke="#ffcc00" stroke-width="0.5" opacity="0.4"/>
  <text x="16" y="20" text-anchor="middle" font-family="monospace" font-size="12" font-weight="bold" fill="#ffcc00" opacity="0.7">¢</text>
</g>
</svg>''')

    # Skill icon - gear/tech
    write_svg("ui", "icon_skill", f'''<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32" width="32" height="32">
{NEON_DEFS}
<g filter="url(#glow)">
  {"".join(f'<rect x="14" y="2" width="4" height="8" rx="1" fill="#00cccc" opacity="0.5" transform="rotate({a},16,16)"/>' for a in range(0, 360, 45))}
  <circle cx="16" cy="16" r="8" fill="none" stroke="#00cccc" stroke-width="1.5" opacity="0.6"/>
  <circle cx="16" cy="16" r="4" fill="#00cccc" opacity="0.3"/>
</g>
</svg>''')


# ═══════════════════════════════════════
# MAIN
# ═══════════════════════════════════════

if __name__ == "__main__":
    print("Generating cyberpunk SVG assets...\n")
    gen_backgrounds()
    gen_characters()
    gen_cards()
    gen_map_nodes()
    gen_enemies()
    gen_intents()
    gen_ui_icons()
    print("\n=== Done! All SVG assets generated. ===")
