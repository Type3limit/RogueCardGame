"""
Cyberpunk 2077-inspired audio generator for RogueCardGame.
Modern "future synth" / darksynth style.
- Deep sub-bass, filtered noise sweeps, layered detuned oscillators
- Reverb tails, stereo chorus, sidechain-like pumping
- Each BGM is distinct in mood and structure
- SFX use FM synthesis and granular-style textures
All pure Python, no external dependencies.
"""

import wave
import struct
import math
import random
import os

SAMPLE_RATE = 44100
BASE_DIR = os.path.dirname(os.path.abspath(__file__))
BGM_DIR = os.path.join(BASE_DIR, "resources", "audio", "bgm")
SFX_DIR = os.path.join(BASE_DIR, "resources", "audio", "sfx")


def ensure_dirs():
    os.makedirs(BGM_DIR, exist_ok=True)
    os.makedirs(SFX_DIR, exist_ok=True)


def write_wav(filepath, samples, sample_rate=SAMPLE_RATE, stereo=False):
    """Write 16-bit WAV. samples is list of floats [-1,1] (mono) or list of (L,R) tuples (stereo)."""
    nch = 2 if stereo else 1
    with wave.open(filepath, "w") as f:
        f.setnchannels(nch)
        f.setsampwidth(2)
        f.setframerate(sample_rate)
        if stereo:
            for l, r in samples:
                l = max(-1.0, min(1.0, l))
                r = max(-1.0, min(1.0, r))
                f.writeframes(struct.pack("<hh", int(l * 30000), int(r * 30000)))
        else:
            for s in samples:
                s = max(-1.0, min(1.0, s))
                f.writeframes(struct.pack("<h", int(s * 30000)))
    dur = len(samples) / sample_rate
    print(f"  -> {os.path.basename(filepath)} ({dur:.1f}s, {'stereo' if stereo else 'mono'})")


# ───────────── Synthesis Core ─────────────

def sine(freq, t):
    return math.sin(2.0 * math.pi * freq * t)

def saw(freq, t):
    """Band-limited-ish sawtooth via additive (6 harmonics)."""
    s = 0.0
    for k in range(1, 7):
        s += ((-1) ** (k + 1)) * math.sin(2.0 * math.pi * freq * k * t) / k
    return s * 0.6

def pulse(freq, t, width=0.5):
    phase = (freq * t) % 1.0
    return 1.0 if phase < width else -1.0

def noise_sample():
    return random.uniform(-1, 1)

def supersaw(freq, t, detune=0.008, voices=5):
    """Detuned supersaw — the signature modern synth sound."""
    s = 0.0
    for i in range(voices):
        ratio = 1.0 + detune * (i - voices // 2) / max(voices // 2, 1)
        s += saw(freq * ratio, t)
    return s / voices

def fm_synth(carrier_freq, mod_freq, mod_index, t):
    """FM synthesis — great for metallic and bell-like tones."""
    return math.sin(2.0 * math.pi * carrier_freq * t +
                    mod_index * math.sin(2.0 * math.pi * mod_freq * t))

def env_ad(t, attack, decay):
    """Simple attack-decay envelope."""
    if t < attack:
        return t / attack if attack > 0 else 1.0
    elif t < attack + decay:
        return 1.0 - (t - attack) / decay if decay > 0 else 0.0
    return 0.0

def env_adsr(t, dur, a=0.01, d=0.05, s_level=0.7, r=0.15):
    rs = dur - r
    if t < a:
        return t / a if a > 0 else 1.0
    if t < a + d:
        return 1.0 - (1.0 - s_level) * (t - a) / d
    if t < rs:
        return s_level
    if t < dur:
        return s_level * (dur - t) / r if r > 0 else 0.0
    return 0.0

def env_smooth(t, dur, fade_in=1.0, fade_out=2.0):
    """Smooth fade for long pads."""
    v = 1.0
    if t < fade_in:
        v = t / fade_in
    if t > dur - fade_out:
        v = min(v, (dur - t) / fade_out)
    return max(0.0, v)


# ───────────── Filters ─────────────

class LPF:
    """Resonant low-pass filter (2-pole biquad)."""
    def __init__(self, cutoff=1000.0, resonance=0.5, sr=SAMPLE_RATE):
        self.sr = sr
        self.x1 = self.x2 = self.y1 = self.y2 = 0.0
        self.set_params(cutoff, resonance)

    def set_params(self, cutoff, resonance):
        cutoff = max(20.0, min(cutoff, self.sr * 0.45))
        w = 2.0 * math.pi * cutoff / self.sr
        q = max(0.5, 1.0 / (2.0 * (1.0 - min(resonance, 0.95))))
        sin_w = math.sin(w)
        cos_w = math.cos(w)
        alpha = sin_w / (2.0 * q)
        a0 = 1.0 + alpha
        self._b0 = (1.0 - cos_w) / 2.0 / a0
        self._b1 = (1.0 - cos_w) / a0
        self._b2 = self._b0
        self._a1 = -2.0 * cos_w / a0
        self._a2 = (1.0 - alpha) / a0

    def process(self, x):
        y = self._b0 * x + self._b1 * self.x1 + self._b2 * self.x2 - self._a1 * self.y1 - self._a2 * self.y2
        self.x2 = self.x1
        self.x1 = x
        self.y2 = self.y1
        self.y1 = y
        return y


def apply_lpf(samples, cutoff=1000, resonance=0.3):
    filt = LPF(cutoff, resonance)
    return [filt.process(s) for s in samples]

def apply_hpf_simple(samples, alpha=0.995):
    """Simple single-pole high-pass filter."""
    out = [samples[0]]
    for i in range(1, len(samples)):
        out.append(alpha * (out[-1] + samples[i] - samples[i - 1]))
    return out

def reverb_schroeder(samples, room_size=0.7):
    """Multi-tap comb filter reverb."""
    delays = [int(d * room_size) for d in [1557, 1617, 1491, 1422]]
    gains = [0.25, 0.23, 0.21, 0.19]
    out = list(samples)
    for delay, gain in zip(delays, gains):
        for i in range(delay, len(out)):
            out[i] += out[i - delay] * gain
    peak = max(abs(s) for s in out) or 1.0
    if peak > 0.95:
        out = [s * 0.9 / peak for s in out]
    return out

def stereo_widen(samples, delay_ms=12):
    """Create stereo from mono with slight delay + inverted phase on right."""
    delay = int(SAMPLE_RATE * delay_ms / 1000)
    result = []
    for i in range(len(samples)):
        l = samples[i]
        r = samples[i - delay] * 0.92 if i >= delay else 0.0
        result.append((l, r))
    return result

def soft_clip(x, drive=1.5):
    return math.tanh(x * drive)

def normalize(samples, target=0.85):
    peak = max(abs(s) for s in samples) or 1.0
    return [s * target / peak for s in samples]

def mix_layers(layers, vols=None):
    """Mix multiple mono sample lists."""
    maxlen = max(len(l) for l in layers)
    if vols is None:
        vols = [1.0] * len(layers)
    out = [0.0] * maxlen
    for arr, v in zip(layers, vols):
        for i in range(len(arr)):
            out[i] += arr[i] * v
    return normalize(out)


# ───────────── Note helpers ─────────────

_NOTE_MAP = {'C': -9, 'C#': -8, 'Db': -8, 'D': -7, 'D#': -6, 'Eb': -6,
             'E': -5, 'F': -4, 'F#': -3, 'Gb': -3, 'G': -2, 'G#': -1,
             'Ab': -1, 'A': 0, 'A#': 1, 'Bb': 1, 'B': 2}

def nf(name):
    """Note name to frequency. e.g. 'C4', 'F#3'"""
    octave = int(name[-1])
    note = name[:-1]
    semitone = _NOTE_MAP[note] + (octave - 4) * 12
    return 440.0 * (2 ** (semitone / 12.0))


# ═══════════════════════════════════════════════════════
# SFX — Modern cinematic UI sounds
# ═══════════════════════════════════════════════════════

def gen_sfx_card_play():
    """Smooth futuristic whoosh with metallic tail. 0.4s"""
    dur = 0.4
    n = int(SAMPLE_RATE * dur)
    samples = []
    for i in range(n):
        t = i / SAMPLE_RATE
        env = env_adsr(t, dur, 0.005, 0.08, 0.3, 0.2)
        cutoff_factor = max(0.01, 1.0 - t / dur)
        nz = noise_sample() * 0.4 * cutoff_factor * env
        fm = fm_synth(600 - 400 * (t / dur), 80, 3.0 * (1 - t / dur), t) * 0.25 * env
        sub = sine(60, t) * 0.3 * env_ad(t, 0.003, 0.1)
        samples.append(nz + fm + sub)
    return normalize(apply_lpf(reverb_schroeder(samples, 0.5), 4000, 0.2))


def gen_sfx_card_select():
    """Clean digital tick + harmonic ping. 0.12s"""
    dur = 0.12
    n = int(SAMPLE_RATE * dur)
    samples = []
    for i in range(n):
        t = i / SAMPLE_RATE
        env = env_ad(t, 0.001, 0.11)
        s = fm_synth(2000, 500, 2.0, t) * 0.3 * env
        s += sine(4000, t) * 0.15 * env_ad(t, 0.001, 0.05)
        samples.append(s)
    return normalize(samples)


def gen_sfx_card_draw():
    """Smooth ascending glide with air. 0.25s"""
    dur = 0.25
    n = int(SAMPLE_RATE * dur)
    samples = []
    for i in range(n):
        t = i / SAMPLE_RATE
        env = env_adsr(t, dur, 0.01, 0.04, 0.4, 0.1)
        freq = 300 + 1200 * (t / dur) ** 0.7
        s = supersaw(freq, t, 0.005, 3) * 0.2 * env
        s += noise_sample() * 0.08 * env * (1 - t / dur)
        samples.append(s)
    return normalize(apply_lpf(reverb_schroeder(samples, 0.3), 6000))


def gen_sfx_card_discard():
    """Low drop with digital crumble. 0.3s"""
    dur = 0.3
    n = int(SAMPLE_RATE * dur)
    samples = []
    for i in range(n):
        t = i / SAMPLE_RATE
        env = env_adsr(t, dur, 0.005, 0.05, 0.3, 0.15)
        freq = 800 * math.exp(-t * 8)
        s = fm_synth(freq, freq * 0.7, 2.5 * (1 - t / dur), t) * 0.3 * env
        s += noise_sample() * 0.1 * env * max(0, 1 - t * 6)
        samples.append(s)
    return normalize(apply_lpf(samples, 3000))


def gen_sfx_attack_hit():
    """Heavy cinematic impact — layered sub + crack + distortion. 0.5s"""
    dur = 0.5
    n = int(SAMPLE_RATE * dur)
    samples = []
    for i in range(n):
        t = i / SAMPLE_RATE
        sub_env = env_ad(t, 0.002, 0.2)
        sub = sine(40 + 30 * math.exp(-t * 20), t) * 0.5 * sub_env
        crack_env = env_ad(t, 0.001, 0.03)
        crack = noise_sample() * 0.6 * crack_env
        mid_env = env_ad(t, 0.003, 0.15)
        mid = soft_clip(saw(150, t) * 2.0, 3.0) * 0.2 * mid_env
        tail_env = env_adsr(t, dur, 0.01, 0.1, 0.15, 0.25)
        tail = fm_synth(200, 80, 1.5, t) * 0.1 * tail_env
        samples.append(sub + crack + mid + tail)
    return normalize(reverb_schroeder(samples, 0.6))


def gen_sfx_block_gain():
    """Energy shield activation — sweep up + resonant buzz. 0.35s"""
    dur = 0.35
    n = int(SAMPLE_RATE * dur)
    raw = []
    for i in range(n):
        t = i / SAMPLE_RATE
        env = env_adsr(t, dur, 0.02, 0.06, 0.5, 0.15)
        freq = 200 + 1500 * (t / dur) ** 0.5
        s = supersaw(freq, t, 0.01, 5) * 0.25 * env
        s += sine(freq * 2, t) * 0.1 * env
        raw.append(s)
    filtered = []
    filt = LPF(500, 0.6)
    for i, s in enumerate(raw):
        t = i / SAMPLE_RATE
        filt.set_params(500 + 5000 * (t / dur), 0.6)
        filtered.append(filt.process(s))
    return normalize(reverb_schroeder(filtered, 0.4))


def gen_sfx_heal():
    """Warm shimmer — layered harmonics with chorus. 0.5s"""
    dur = 0.5
    n = int(SAMPLE_RATE * dur)
    samples = []
    base = nf('C5')
    for i in range(n):
        t = i / SAMPLE_RATE
        env = env_adsr(t, dur, 0.03, 0.1, 0.4, 0.2)
        s = sine(base, t) * 0.2
        s += sine(base * 1.25, t) * 0.15
        s += sine(base * 1.5, t) * 0.12
        s += sine(base * 2, t) * 0.08
        s += sine(base * 1.003, t) * 0.1
        s += sine(base * 0.997, t) * 0.1
        sparkle_env = env_ad(t, 0.0, dur) * (0.5 + 0.5 * sine(6, t))
        s += sine(base * 4, t) * 0.03 * sparkle_env
        samples.append(s * env)
    return normalize(reverb_schroeder(samples, 0.8))


def gen_sfx_status_apply():
    """Digital glitch burst — bitcrushed texture. 0.25s"""
    dur = 0.25
    n = int(SAMPLE_RATE * dur)
    samples = []
    rng = random.Random(42)
    hold_val = 0.0
    for i in range(n):
        t = i / SAMPLE_RATE
        env = env_adsr(t, dur, 0.003, 0.04, 0.4, 0.1)
        mod_idx = 4.0
        if i % 200 == 0:
            mod_idx += 3.0 * rng.random()
        s = fm_synth(300 + 100 * pulse(6, t, 0.3), 150, mod_idx, t) * 0.3 * env
        if i % 8 == 0:
            hold_val = s
        samples.append(hold_val)
    return normalize(apply_lpf(samples, 5000))


def gen_sfx_button_click():
    """Minimal futuristic UI click. 0.06s"""
    dur = 0.06
    n = int(SAMPLE_RATE * dur)
    samples = []
    for i in range(n):
        t = i / SAMPLE_RATE
        env = env_ad(t, 0.001, 0.055)
        s = fm_synth(3000, 1000, 1.5, t) * 0.25 * env
        s += sine(1500, t) * 0.15 * env
        samples.append(s)
    return normalize(samples)


def gen_sfx_button_hover():
    """Ultra-subtle hover tone. 0.05s"""
    dur = 0.05
    n = int(SAMPLE_RATE * dur)
    samples = []
    for i in range(n):
        t = i / SAMPLE_RATE
        env = env_ad(t, 0.002, 0.045)
        s = sine(2200, t) * 0.12 * env
        samples.append(s)
    return normalize(samples)


def gen_sfx_turn_end():
    """Mechanical servo sweep + digital confirmation. 0.45s"""
    dur = 0.45
    n = int(SAMPLE_RATE * dur)
    samples = []
    for i in range(n):
        t = i / SAMPLE_RATE
        env = env_adsr(t, dur, 0.01, 0.08, 0.35, 0.2)
        nz = noise_sample() * 0.2 * env
        s = 0.0
        if t > 0.25:
            beep_env = env_adsr(t - 0.25, 0.2, 0.005, 0.03, 0.5, 0.1)
            s = fm_synth(1200, 300, 1.0, t) * 0.2 * beep_env
        clunk = sine(50, t) * 0.3 * env_ad(t, 0.002, 0.08)
        samples.append(nz + s + clunk)
    filt = LPF(2000, 0.4)
    filtered = []
    for i, s in enumerate(samples):
        t = i / SAMPLE_RATE
        cf = 800 + 4000 * abs(math.sin(math.pi * t / dur))
        filt.set_params(cf, 0.4)
        filtered.append(filt.process(s))
    return normalize(reverb_schroeder(filtered, 0.4))


def gen_sfx_enemy_attack():
    """Aggressive energy slash — descending screech + bass. 0.4s"""
    dur = 0.4
    n = int(SAMPLE_RATE * dur)
    samples = []
    for i in range(n):
        t = i / SAMPLE_RATE
        env = env_adsr(t, dur, 0.002, 0.04, 0.3, 0.2)
        carrier = 1200 * math.exp(-t * 6)
        s = fm_synth(carrier, carrier * 0.5, 5.0 * (1 - t / dur), t) * 0.25 * env
        s += noise_sample() * 0.3 * env_ad(t, 0.001, 0.05)
        s += soft_clip(sine(55, t) * 2, 2.0) * 0.2 * env_ad(t, 0.002, 0.15)
        samples.append(s)
    return normalize(apply_lpf(reverb_schroeder(samples, 0.5), 6000))


def gen_sfx_gold_gain():
    """Satisfying coin/credit collect — two FM bells. 0.3s"""
    dur = 0.3
    n = int(SAMPLE_RATE * dur)
    samples = []
    for i in range(n):
        t = i / SAMPLE_RATE
        if t < 0.15:
            env1 = env_ad(t, 0.002, 0.14)
            s = fm_synth(1400, 2800, 3.0, t) * 0.2 * env1
        else:
            t2 = t - 0.13
            env2 = env_ad(t2, 0.002, 0.15)
            s = fm_synth(1800, 3600, 3.0, t) * 0.2 * env2
        s += sine(5000, t) * 0.03 * env_ad(t, 0.0, 0.1)
        samples.append(s)
    return normalize(reverb_schroeder(samples, 0.6))


def gen_sfx_map_node():
    """Selection confirm — clean resonant ping. 0.2s"""
    dur = 0.2
    n = int(SAMPLE_RATE * dur)
    samples = []
    f = nf('A4')
    for i in range(n):
        t = i / SAMPLE_RATE
        env = env_ad(t, 0.003, 0.19)
        s = fm_synth(f, f * 1.5, 2.0, t) * 0.25 * env
        s += sine(f * 2, t) * 0.1 * env
        samples.append(s)
    return normalize(reverb_schroeder(samples, 0.5))


# ═══════════════════════════════════════════════════════
# BGM — Distinct Cyberpunk 2077-inspired tracks
# ═══════════════════════════════════════════════════════

def _make_kick_modern():
    """Punchy 808-style kick with sub tail."""
    dur = 0.25
    n = int(SAMPLE_RATE * dur)
    samples = []
    for i in range(n):
        t = i / SAMPLE_RATE
        freq = 45 + 155 * math.exp(-t * 40)
        env = math.exp(-t * 12) * 0.9
        s = soft_clip(sine(freq, t) * 1.5, 2.0) * env
        s += noise_sample() * 0.4 * env_ad(t, 0.0, 0.005)
        samples.append(s)
    return samples


def _make_clap():
    """Layered clap with reverb."""
    dur = 0.2
    n = int(SAMPLE_RATE * dur)
    samples = []
    for i in range(n):
        t = i / SAMPLE_RATE
        env = env_ad(t, 0.001, 0.19)
        s = 0.0
        for offset in [0, 0.008, 0.015]:
            if t >= offset:
                s += noise_sample() * 0.3 * env_ad(t - offset, 0.001, 0.05)
        s *= env
        samples.append(s)
    return apply_lpf(reverb_schroeder(samples, 0.3), 6000)


def _make_hat_closed():
    dur = 0.04
    n = int(SAMPLE_RATE * dur)
    samples = []
    for i in range(n):
        t = i / SAMPLE_RATE
        env = math.exp(-t * 120)
        s = noise_sample() * 0.25 * env
        samples.append(s)
    return apply_hpf_simple(samples, 0.98)


def _make_hat_open():
    dur = 0.15
    n = int(SAMPLE_RATE * dur)
    samples = []
    for i in range(n):
        t = i / SAMPLE_RATE
        env = math.exp(-t * 20)
        s = noise_sample() * 0.2 * env
        samples.append(s)
    return apply_hpf_simple(samples, 0.98)


def drum_pattern(bpm, duration, pattern, sound_func):
    """Place a sound on a beat grid."""
    beat_dur = 60.0 / bpm
    n_total = int(SAMPLE_RATE * duration)
    result = [0.0] * n_total
    n_beats = int(duration / beat_dur)
    pat_len = len(pattern)
    for beat in range(n_beats):
        idx = beat % pat_len
        vel = pattern[idx]
        if vel <= 0:
            continue
        start = int(beat * beat_dur * SAMPLE_RATE)
        sound = sound_func()
        for j, sv in enumerate(sound):
            pos = start + j
            if pos < n_total:
                result[pos] += sv * vel
    return result


def gen_bass_sequence(bpm, duration, notes, gate_pattern, sound_type='supersaw'):
    """Generate a bass sequence with gate pattern."""
    step_dur = 60.0 / bpm / 2
    n_total = int(SAMPLE_RATE * duration)
    result = [0.0] * n_total
    steps = int(duration / step_dur)
    pat_len = len(gate_pattern)

    for step in range(steps):
        gate = gate_pattern[step % pat_len]
        if gate <= 0:
            continue
        freq = notes[step % len(notes)]
        start = int(step * step_dur * SAMPLE_RATE)
        note_len = int(step_dur * 0.85 * SAMPLE_RATE)
        for j in range(note_len):
            pos = start + j
            if pos >= n_total:
                break
            t_local = j / SAMPLE_RATE
            env = env_adsr(t_local, step_dur * 0.85, 0.005, 0.02, 0.7, 0.03)
            t_global = (start + j) / SAMPLE_RATE
            if sound_type == 'supersaw':
                s = supersaw(freq, t_global, 0.006, 3) * env * gate
            elif sound_type == 'sub':
                s = sine(freq, t_global) * env * gate * 0.8
            else:
                s = saw(freq, t_global) * env * gate
            result[pos] += s
    return result


def gen_pad_layer(duration, freqs, volume=0.15, lfo_rate=0.15, fade_in=2.0, fade_out=3.0):
    """Lush detuned pad."""
    n = int(SAMPLE_RATE * duration)
    result = [0.0] * n
    for i in range(n):
        t = i / SAMPLE_RATE
        env = env_smooth(t, duration, fade_in, fade_out)
        lfo = 1.0 + 0.01 * sine(lfo_rate, t)
        s = 0.0
        for freq in freqs:
            s += supersaw(freq * lfo, t, 0.004, 3) * 0.15
            s += sine(freq * 0.5, t) * 0.08
        s = s / max(len(freqs), 1) * volume * env
        result[i] = s
    return result


def gen_arp_layer(bpm, duration, notes, volume=0.15, sound='fm'):
    """Modern arpeggio layer."""
    step_dur = 60.0 / bpm / 4
    n_total = int(SAMPLE_RATE * duration)
    result = [0.0] * n_total
    steps = int(duration / step_dur)

    for step in range(steps):
        freq = notes[step % len(notes)]
        start = int(step * step_dur * SAMPLE_RATE)
        note_dur = step_dur * 0.6
        note_samples = int(note_dur * SAMPLE_RATE)
        for j in range(note_samples):
            pos = start + j
            if pos >= n_total:
                break
            t_local = j / SAMPLE_RATE
            env = env_adsr(t_local, note_dur, 0.003, 0.02, 0.4, 0.03)
            t_global = pos / SAMPLE_RATE
            if sound == 'fm':
                s = fm_synth(freq, freq * 1.5, 1.5, t_global) * env * volume
            elif sound == 'saw':
                s = supersaw(freq, t_global, 0.005, 3) * env * volume * 0.5
            else:
                s = sine(freq, t_global) * env * volume
            result[pos] += s
    return result


def sidechain_pump(samples, bpm, depth=0.5):
    """Fake sidechain compression — volume duck on kick beats."""
    beat_dur = 60.0 / bpm
    out = list(samples)
    for i in range(len(out)):
        t = i / SAMPLE_RATE
        beat_pos = (t % beat_dur) / beat_dur
        duck = 1.0 - depth * max(0, 1.0 - beat_pos * 6)
        out[i] *= duck
    return out


# ─── BGM Tracks ───

def gen_bgm_main_menu(duration=30.0):
    """
    MAIN MENU: Dark ambient drone. Blade Runner / Vangelis inspired.
    Slow, atmospheric, no beat. Evolving pad with sub drone.
    """
    drone_freq = nf('A1')
    n = int(SAMPLE_RATE * duration)
    drone = []
    for i in range(n):
        t = i / SAMPLE_RATE
        env = env_smooth(t, duration, 4.0, 5.0)
        s = sine(drone_freq, t) * 0.25
        s += sine(drone_freq * 1.002, t) * 0.2
        s += sine(drone_freq * 0.998, t) * 0.2
        s += sine(drone_freq * 3, t) * 0.05 * (0.5 + 0.5 * sine(0.08, t))
        s += sine(drone_freq * 5, t) * 0.02 * (0.5 + 0.5 * sine(0.12, t))
        drone.append(s * env)

    chords = [
        [nf('A2'), nf('C3'), nf('E3')],
        [nf('D3'), nf('F3'), nf('A3')],
        [nf('F2'), nf('Ab2'), nf('C3')],
        [nf('E2'), nf('G#2'), nf('B2')],
    ]
    chord_dur = duration / len(chords)
    pad = [0.0] * n
    for ci, chord in enumerate(chords):
        start_t = ci * chord_dur
        end_t = start_t + chord_dur
        for i in range(int(start_t * SAMPLE_RATE), min(int(end_t * SAMPLE_RATE), n)):
            t = i / SAMPLE_RATE
            local_t = t - start_t
            env = env_smooth(local_t, chord_dur, 2.0, 2.0) * 0.12
            s = 0
            for freq in chord:
                lfo = 1.0 + 0.003 * sine(0.2 + freq * 0.0001, t)
                s += supersaw(freq * lfo, t, 0.005, 3) * 0.12
            pad[i] = s * env

    atmo = []
    rng = random.Random(1)
    filt = LPF(300, 0.3)
    for i in range(n):
        t = i / SAMPLE_RATE
        env = env_smooth(t, duration, 3.0, 4.0)
        cf = 200 + 400 * (0.5 + 0.5 * sine(0.05, t))
        filt.set_params(cf, 0.3)
        s = filt.process(rng.uniform(-1, 1)) * 0.08 * env
        atmo.append(s)

    mixed = mix_layers([drone, pad, atmo], [1.0, 1.0, 1.0])
    result = reverb_schroeder(mixed, 0.9)
    return stereo_widen(normalize(result), 15)


def gen_bgm_map(duration=30.0):
    """
    MAP: Lo-fi exploration. 85 BPM, soft keys, atmospheric pads.
    """
    bpm = 85
    kick_pat = [0.6, 0, 0, 0.3, 0.6, 0, 0, 0]
    kicks = drum_pattern(bpm, duration, kick_pat, _make_kick_modern)
    hat_pat = [0.3, 0.15, 0.25, 0.15, 0.3, 0.15, 0.25, 0.2]
    hats = drum_pattern(bpm, duration, hat_pat, _make_hat_closed)

    half = duration / 2
    p1 = gen_pad_layer(half, [nf('F2'), nf('A2'), nf('C3'), nf('E3')], 0.12, 0.2, 3.0, 3.0)
    p2 = gen_pad_layer(half, [nf('A2'), nf('C3'), nf('E3'), nf('G3')], 0.12, 0.2, 3.0, 3.0)
    pad = p1 + p2

    arp_notes = [nf('C4'), nf('E4'), nf('G4'), nf('A4'), nf('G4'), nf('E4'),
                 nf('F4'), nf('A4'), nf('C5'), nf('E5'), nf('C5'), nf('A4')]
    arp = gen_arp_layer(bpm, duration, arp_notes, 0.1, 'sine')

    sub_notes = [nf('F1')] * 4 + [nf('A1')] * 4
    sub_gate = [1, 0, 0.6, 0, 1, 0, 0, 0]
    sub = gen_bass_sequence(bpm, duration, sub_notes, sub_gate, 'sub')

    mixed = mix_layers([kicks, hats, pad, arp, sub], [0.5, 0.35, 1.0, 0.7, 0.5])
    mixed = sidechain_pump(mixed, bpm, 0.2)
    return stereo_widen(normalize(reverb_schroeder(mixed, 0.5)), 10)


def gen_bgm_combat(duration=30.0):
    """
    COMBAT: Driving darksynth. 130 BPM, heavy kick, aggressive bass.
    """
    bpm = 130
    kick_pat = [1, 0, 0, 0.5, 1, 0, 0.4, 0]
    kicks = drum_pattern(bpm, duration, kick_pat, _make_kick_modern)
    clap_pat = [0, 0, 1, 0, 0, 0, 1, 0]
    claps = drum_pattern(bpm, duration, clap_pat, _make_clap)
    hat_pat = [0.4, 0.2, 0.35, 0.2, 0.4, 0.2, 0.35, 0.25]
    hats = drum_pattern(bpm, duration, hat_pat, _make_hat_closed)
    oh_pat = [0, 0, 0, 0, 0, 0, 0, 0.35]
    ohs = drum_pattern(bpm, duration, oh_pat, _make_hat_open)

    bass_notes = [nf('E1'), nf('E1'), nf('G1'), nf('E1'),
                  nf('E1'), nf('B1'), nf('D2'), nf('E1')]
    bass_gate = [1, 0, 0.8, 0.5, 1, 0, 0.7, 0]
    bass_raw = gen_bass_sequence(bpm, duration, bass_notes, bass_gate, 'supersaw')
    bass = apply_lpf(bass_raw, 800, 0.4)

    arp_notes = [nf('E4'), nf('B4'), nf('E5'), nf('D5'),
                 nf('B4'), nf('G4'), nf('E4'), nf('D4')]
    arp = gen_arp_layer(bpm, duration, arp_notes, 0.15, 'saw')

    pad = gen_pad_layer(duration, [nf('E2'), nf('B2'), nf('E3')], 0.1, 0.1, 2.0, 3.0)

    cycle = 60.0 / bpm * 16
    n = int(SAMPLE_RATE * duration)
    riser = []
    filt = LPF(200, 0.5)
    for i in range(n):
        t = i / SAMPLE_RATE
        phase = (t % cycle) / cycle
        filt.set_params(200 + 3000 * phase * phase, 0.5)
        s = filt.process(noise_sample()) * 0.06 * phase
        riser.append(s)

    mixed = mix_layers([kicks, claps, hats, ohs, bass, arp, pad, riser],
                       [0.7, 0.45, 0.3, 0.25, 0.65, 0.45, 0.7, 0.5])
    mixed = sidechain_pump(mixed, bpm, 0.35)
    return stereo_widen(normalize(mixed), 8)


def gen_bgm_boss(duration=30.0):
    """
    BOSS: Intense industrial darksynth. 145 BPM. Distorted, chaotic.
    """
    bpm = 145
    kick_pat = [1, 0.6, 0.7, 0, 1, 0.6, 0.7, 0.4]
    kicks = drum_pattern(bpm, duration, kick_pat, _make_kick_modern)
    clap_pat = [0, 0, 1, 0.3, 0, 0.3, 1, 0]
    claps = drum_pattern(bpm, duration, clap_pat, _make_clap)
    hat_pat = [0.5, 0.3, 0.45, 0.3, 0.5, 0.3, 0.45, 0.35]
    hats = drum_pattern(bpm, duration, hat_pat, _make_hat_closed)

    bass_notes = [nf('D1'), nf('D1'), nf('F1'), nf('D1'),
                  nf('A1'), nf('D1'), nf('C2'), nf('Bb1')]
    bass_gate = [1, 0.5, 0.8, 0, 1, 0.7, 0.8, 0.5]
    bass_raw = gen_bass_sequence(bpm, duration, bass_notes, bass_gate, 'supersaw')
    bass = [soft_clip(s * 2, 3.0) for s in bass_raw]
    bass = apply_lpf(bass, 600, 0.5)

    arp_notes = [nf('D4'), nf('F4'), nf('A4'), nf('D5'),
                 nf('C5'), nf('A4'), nf('F4'), nf('E4'),
                 nf('D4'), nf('A4'), nf('D5'), nf('F5'),
                 nf('E5'), nf('D5'), nf('A4'), nf('F4')]
    arp = gen_arp_layer(bpm, duration, arp_notes, 0.18, 'fm')

    pad = gen_pad_layer(duration, [nf('D2'), nf('F2'), nf('A2'), nf('C#3')], 0.1, 0.08, 2.0, 3.0)

    n = int(SAMPLE_RATE * duration)
    industrial = []
    filt = LPF(1000, 0.6)
    rng = random.Random(77)
    for i in range(n):
        t = i / SAMPLE_RATE
        env = env_smooth(t, duration, 1.0, 2.0)
        cycle_phase = (t % 4) / 4
        filt.set_params(500 + 2000 * cycle_phase, 0.6)
        s = filt.process(rng.uniform(-1, 1)) * 0.06 * env
        if i % int(SAMPLE_RATE * 60 / bpm * 4) < 200:
            s += fm_synth(800, 200, 8, t) * 0.04 * env
        industrial.append(s)

    mixed = mix_layers([kicks, claps, hats, bass, arp, pad, industrial],
                       [0.7, 0.5, 0.3, 0.6, 0.5, 0.6, 0.4])
    mixed = sidechain_pump(mixed, bpm, 0.4)
    return stereo_widen(normalize(mixed), 8)


def gen_bgm_shop(duration=30.0):
    """
    SHOP: Chill synthwave. 90 BPM. Warm, jazzy chords.
    """
    bpm = 90
    kick_pat = [0.5, 0, 0, 0.3, 0.5, 0, 0, 0]
    kicks = drum_pattern(bpm, duration, kick_pat, _make_kick_modern)
    hat_pat = [0.2, 0.15, 0.2, 0.15, 0.2, 0.15, 0.2, 0.15]
    hats = drum_pattern(bpm, duration, hat_pat, _make_hat_closed)

    sections = [
        [nf('C2'), nf('E2'), nf('G2'), nf('B2'), nf('D3')],
        [nf('F2'), nf('A2'), nf('C3'), nf('E3')],
        [nf('D2'), nf('F2'), nf('A2'), nf('C3'), nf('E3')],
        [nf('G2'), nf('B2'), nf('D3'), nf('F3')],
    ]
    n = int(SAMPLE_RATE * duration)
    pad = [0.0] * n
    sec_dur = duration / len(sections)
    for si, freqs in enumerate(sections):
        start = int(si * sec_dur * SAMPLE_RATE)
        seg = gen_pad_layer(sec_dur, freqs, 0.12, 0.25, 2.0, 2.0)
        for j, s in enumerate(seg):
            if start + j < n:
                pad[start + j] = s

    arp_notes = [nf('E4'), nf('G4'), nf('B4'), nf('D5'),
                 nf('C5'), nf('A4'), nf('G4'), nf('E4')]
    arp = gen_arp_layer(bpm, duration, arp_notes, 0.08, 'sine')

    sub_notes = [nf('C1')] * 2 + [nf('F1')] * 2 + [nf('D1')] * 2 + [nf('G1')] * 2
    sub_gate = [0.8, 0, 0, 0, 0.8, 0, 0, 0]
    sub = gen_bass_sequence(bpm, duration, sub_notes, sub_gate, 'sub')

    mixed = mix_layers([kicks, hats, pad, arp, sub], [0.4, 0.3, 1.0, 0.6, 0.4])
    mixed = sidechain_pump(mixed, bpm, 0.15)
    return stereo_widen(normalize(reverb_schroeder(mixed, 0.7)), 12)


def gen_bgm_event(duration=30.0):
    """
    EVENT: Mysterious / suspenseful. No beat. Sparse, eerie, cinematic.
    """
    n = int(SAMPLE_RATE * duration)

    drone = []
    for i in range(n):
        t = i / SAMPLE_RATE
        env = env_smooth(t, duration, 4.0, 5.0)
        s = sine(nf('B1'), t) * 0.15 * env
        s += sine(nf('F2'), t) * 0.12 * env
        s += sine(nf('B1') * 1.002, t) * 0.1 * env
        drone.append(s)

    bells = [0.0] * n
    rng = random.Random(42)
    bell_times = sorted([rng.uniform(2, duration - 2) for _ in range(12)])
    bell_freqs = [nf(nn) for nn in ['B4', 'F5', 'E5', 'C5', 'G4', 'D5',
                                      'A4', 'F#5', 'B5', 'E4', 'C#5', 'G5']]
    for bi, bt in enumerate(bell_times):
        freq = bell_freqs[bi % len(bell_freqs)]
        start = int(bt * SAMPLE_RATE)
        bell_dur = 1.5
        for j in range(int(bell_dur * SAMPLE_RATE)):
            pos = start + j
            if pos >= n:
                break
            t = j / SAMPLE_RATE
            env = env_ad(t, 0.005, bell_dur - 0.01) * 0.15
            bells[pos] += fm_synth(freq, freq * 2.5, 2.0 * math.exp(-t * 2), bt + t) * env

    atmo = []
    filt = LPF(300, 0.4)
    rng2 = random.Random(88)
    for i in range(n):
        t = i / SAMPLE_RATE
        env = env_smooth(t, duration, 3.0, 4.0)
        cf = 200 + 800 * (0.5 + 0.5 * sine(0.03, t))
        filt.set_params(cf, 0.4)
        atmo.append(filt.process(rng2.uniform(-1, 1)) * 0.06 * env)

    pad = gen_pad_layer(duration, [nf('B2'), nf('D3'), nf('F3')], 0.08, 0.1, 5.0, 5.0)

    mixed = mix_layers([drone, bells, atmo, pad], [1.0, 0.8, 1.0, 0.8])
    return stereo_widen(normalize(reverb_schroeder(mixed, 1.0)), 18)


def gen_bgm_victory(duration=20.0):
    """
    VICTORY: Euphoric synthwave. 120 BPM. Major key, triumphant.
    """
    bpm = 120
    kick_pat = [0.8, 0, 0.5, 0, 0.8, 0, 0.5, 0]
    kicks = drum_pattern(bpm, duration, kick_pat, _make_kick_modern)
    clap_pat = [0, 0, 0.7, 0, 0, 0, 0.7, 0]
    claps = drum_pattern(bpm, duration, clap_pat, _make_clap)
    hat_pat = [0.3, 0.2, 0.3, 0.2, 0.3, 0.2, 0.3, 0.25]
    hats = drum_pattern(bpm, duration, hat_pat, _make_hat_closed)

    half = duration / 2
    p1 = gen_pad_layer(half, [nf('C3'), nf('E3'), nf('G3'), nf('C4')], 0.15, 0.2, 2.0, 2.0)
    p2 = gen_pad_layer(half, [nf('G2'), nf('B2'), nf('D3'), nf('G3')], 0.15, 0.2, 2.0, 2.0)
    pad = p1 + p2

    arp_notes = [nf('C4'), nf('E4'), nf('G4'), nf('C5'),
                 nf('E5'), nf('G5'), nf('E5'), nf('C5'),
                 nf('G4'), nf('B4'), nf('D5'), nf('G5'),
                 nf('D5'), nf('B4'), nf('G4'), nf('D4')]
    arp = gen_arp_layer(bpm, duration, arp_notes, 0.15, 'saw')

    sub_notes = [nf('C1')] * 4 + [nf('G1')] * 4
    sub_gate = [1, 0, 0, 0, 1, 0, 0, 0]
    sub = gen_bass_sequence(bpm, duration, sub_notes, sub_gate, 'sub')

    mixed = mix_layers([kicks, claps, hats, pad, arp, sub],
                       [0.6, 0.4, 0.3, 0.9, 0.6, 0.45])
    mixed = sidechain_pump(mixed, bpm, 0.25)
    return stereo_widen(normalize(mixed), 10)


def gen_bgm_defeat(duration=20.0):
    """
    DEFEAT: Slow, somber ambient. No beat. Descending, hollow.
    """
    n = int(SAMPLE_RATE * duration)

    p1 = gen_pad_layer(duration * 0.6, [nf('F2'), nf('Ab2'), nf('C3')], 0.15, 0.1, 3.0, 4.0)
    p2 = gen_pad_layer(duration * 0.4, [nf('Db2'), nf('F2'), nf('Ab2')], 0.15, 0.1, 3.0, 4.0)
    pad = p1 + p2
    pad.extend([0.0] * max(0, n - len(pad)))

    melody = [0.0] * n
    mel_notes = [nf('C5'), nf('Ab4'), nf('F4'), nf('C4'), nf('Ab3'), nf('F3')]
    note_dur = duration / len(mel_notes)
    for mi, freq in enumerate(mel_notes):
        start = int(mi * note_dur * SAMPLE_RATE)
        nn = int(note_dur * SAMPLE_RATE)
        for j in range(nn):
            pos = start + j
            if pos >= n:
                break
            t_local = j / SAMPLE_RATE
            env = env_adsr(t_local, note_dur, 0.5, 0.5, 0.3, 1.0)
            t_global = (start + j) / SAMPLE_RATE
            melody[pos] = sine(freq, t_global) * 0.1 * env
            melody[pos] += sine(freq * 1.003, t_global) * 0.05 * env

    wind = []
    filt = LPF(200, 0.3)
    rng = random.Random(55)
    for i in range(n):
        t = i / SAMPLE_RATE
        env = env_smooth(t, duration, 2.0, 3.0)
        cf = 150 + 300 * (0.5 + 0.5 * sine(0.04, t))
        filt.set_params(cf, 0.3)
        wind.append(filt.process(rng.uniform(-1, 1)) * 0.07 * env)

    drone = []
    for i in range(n):
        t = i / SAMPLE_RATE
        env = env_smooth(t, duration, 3.0, 4.0)
        drone.append(sine(nf('F1'), t) * 0.12 * env)

    mixed = mix_layers([pad[:n], melody, wind, drone], [1.0, 0.8, 1.0, 1.0])
    return stereo_widen(normalize(reverb_schroeder(mixed, 1.0)), 15)


# ═══════════════════════════════════════════════════════
# MAIN
# ═══════════════════════════════════════════════════════

def main():
    ensure_dirs()

    print("=== Generating Modern Cyberpunk SFX ===")
    sfx_map = {
        "card_play": gen_sfx_card_play,
        "card_select": gen_sfx_card_select,
        "card_draw": gen_sfx_card_draw,
        "card_discard": gen_sfx_card_discard,
        "attack_hit": gen_sfx_attack_hit,
        "block_gain": gen_sfx_block_gain,
        "heal": gen_sfx_heal,
        "status_apply": gen_sfx_status_apply,
        "button_click": gen_sfx_button_click,
        "button_hover": gen_sfx_button_hover,
        "turn_end": gen_sfx_turn_end,
        "enemy_attack": gen_sfx_enemy_attack,
        "gold_gain": gen_sfx_gold_gain,
        "map_node": gen_sfx_map_node,
    }
    for name, func in sfx_map.items():
        samples = func()
        write_wav(os.path.join(SFX_DIR, f"{name}.wav"), samples)

    print("\n=== Generating Cyberpunk 2077-style BGM ===")
    bgm_map = {
        "main_menu": gen_bgm_main_menu,
        "map": gen_bgm_map,
        "combat": gen_bgm_combat,
        "boss": gen_bgm_boss,
        "shop": gen_bgm_shop,
        "event": gen_bgm_event,
        "victory": gen_bgm_victory,
        "defeat": gen_bgm_defeat,
    }
    for name, func in bgm_map.items():
        samples = func()
        is_stereo = len(samples) > 0 and isinstance(samples[0], tuple)
        write_wav(os.path.join(BGM_DIR, f"{name}.wav"), samples, stereo=is_stereo)

    print("\n=== Done! All audio regenerated with Cyberpunk 2077 style. ===")


if __name__ == "__main__":
    main()
