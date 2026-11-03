"""Small synthesizer toolkit for A.O.S sound effects and music.

Everything is numpy arrays at 44.1 kHz. Mono signals are 1-D; mixes are (n, 2) stereo.
Effects (reverb, delay, chorus, compression) come from pedalboard.
"""
import numpy as np
import scipy.signal as sig
import soundfile as sf
import pedalboard as pb
import os

SR = 44100
rng = np.random.default_rng(1)


def seed(s):
    global rng
    rng = np.random.default_rng(s)


def n(dur):
    return max(1, int(round(dur * SR)))


def tt(dur):
    return np.arange(n(dur)) / SR


# ---------------------------------------------------------------- pitch

NOTE = {'C': 0, 'D': 2, 'E': 4, 'F': 5, 'G': 7, 'A': 9, 'B': 11}


def midi(name):
    """'A4' -> 69, 'C#3' -> 49, 'Bb2' -> 46. Ints pass through."""
    if isinstance(name, (int, np.integer)):
        return int(name)
    s = name.strip()
    v = NOTE[s[0].upper()]
    i = 1
    while i < len(s) and s[i] in '#b':
        v += 1 if s[i] == '#' else -1
        i += 1
    return v + 12 * (int(s[i:]) + 1)


def hz(m):
    if isinstance(m, str):
        m = midi(m)
    return 440.0 * 2 ** ((m - 69) / 12)


# ---------------------------------------------------------------- oscillators

def phase_of(freq, dur):
    """Phase (cycles) for a constant or per-sample frequency array."""
    if np.isscalar(freq):
        return freq * tt(dur)
    return np.cumsum(freq) / SR


def sine(freq, dur, ph=0.0):
    return np.sin(2 * np.pi * (phase_of(freq, dur) + ph))


def saw(freq, dur, ph=0.0):
    p = (phase_of(freq, dur) + ph) % 1.0
    return _blep(2 * p - 1, p, freq, dur)


def square(freq, dur, duty=0.5):
    p = phase_of(freq, dur) % 1.0
    return np.where(p < duty, 1.0, -1.0)


def tri(freq, dur):
    p = phase_of(freq, dur) % 1.0
    return 4 * np.abs(p - 0.5) - 1


def _blep(x, p, freq, dur):
    # polyBLEP: soften the saw's reset to cut harsh aliasing
    dt = np.broadcast_to(np.clip(np.asarray(freq, dtype=float) / SR, 1e-6, 0.5), p.shape)
    y = x.copy()
    m = p < dt
    y[m] -= (p[m] / dt[m] - 1) ** 2
    m2 = p > 1 - dt
    y[m2] += ((p[m2] - 1) / dt[m2] + 1) ** 2
    return y


def noise(dur):
    return rng.uniform(-1, 1, n(dur))


def pink(dur):
    w = rng.standard_normal(n(dur))
    b, a = [0.049922035, -0.095993537, 0.050612699, -0.004408786], [1, -2.494956002, 2.017265875, -0.522189400]
    y = sig.lfilter(b, a, w)
    return y / (np.max(np.abs(y)) + 1e-9)


def brown(dur):
    y = np.cumsum(rng.standard_normal(n(dur)))
    y = sig.lfilter([1, -1], [1, -0.999], y)
    return y / (np.max(np.abs(y)) + 1e-9)


def glide(f0, f1, dur, curve='exp'):
    k = np.linspace(0, 1, n(dur))
    if curve == 'exp':
        return f0 * (f1 / f0) ** k
    return f0 + (f1 - f0) * k


# ---------------------------------------------------------------- envelopes

def adsr(dur, a=0.01, d=0.1, s=0.7, r=0.1):
    N = n(dur)
    ai, di, ri = n(a), n(d), n(r)
    env = np.full(N, s, dtype=float)
    ai = min(ai, N)
    env[:ai] = np.linspace(0, 1, ai, endpoint=False)
    de = min(N, ai + di)
    env[ai:de] = np.linspace(1, s, de - ai, endpoint=False)
    ri = min(ri, N)
    env[N - ri:] *= np.linspace(1, 0, ri)
    return env


def decay(dur, k=6.0, a=0.002):
    """Exponential decay; k = how many e-folds across dur."""
    t = tt(dur)
    env = np.exp(-k * t / dur)
    ai = n(a)
    env[:ai] *= np.linspace(0, 1, ai)
    return env


def fade(x, fin=0.005, fout=0.01):
    x = x.copy()
    a, b = min(n(fin), len(x)), min(n(fout), len(x))
    x[:a] *= np.linspace(0, 1, a)
    x[len(x) - b:] *= np.linspace(1, 0, b)
    return x


# ---------------------------------------------------------------- filters

def lp(x, fc, order=2):
    b, a = sig.butter(order, min(fc, SR * 0.45), 'low', fs=SR)
    return sig.lfilter(b, a, x, axis=0)


def hp(x, fc, order=2):
    b, a = sig.butter(order, max(fc, 10), 'high', fs=SR)
    return sig.lfilter(b, a, x, axis=0)


def bp(x, lo, hi, order=2):
    b, a = sig.butter(order, [max(lo, 10), min(hi, SR * 0.45)], 'band', fs=SR)
    return sig.lfilter(b, a, x, axis=0)


def sweep(x, fc, kind='low', q=0.707, block=128):
    """Time-varying resonant filter (biquad per block). fc: array same length as x, or (f0, f1)."""
    if isinstance(fc, tuple):
        fc = glide(fc[0], fc[1], len(x) / SR)
    y = np.zeros_like(x)
    z = np.zeros(2)
    for i in range(0, len(x), block):
        f = float(np.clip(fc[min(i, len(fc) - 1)], 20, SR * 0.45))
        w = 2 * np.pi * f / SR
        al = np.sin(w) / (2 * q)
        cw = np.cos(w)
        if kind == 'low':
            b = [(1 - cw) / 2, 1 - cw, (1 - cw) / 2]
        elif kind == 'high':
            b = [(1 + cw) / 2, -(1 + cw), (1 + cw) / 2]
        else:  # band
            b = [al, 0, -al]
        a = [1 + al, -2 * cw, 1 - al]
        y[i:i + block], z = sig.lfilter(b, a, x[i:i + block], zi=z)
    return y


# ---------------------------------------------------------------- instruments (mono voices)

def pluck(freq, dur, bright=0.5, damp=0.996, body=True):
    """Karplus-Strong string, vectorised per period."""
    N = n(dur)
    P = max(2, int(round(SR / freq)))
    buf = rng.uniform(-1, 1, P)
    buf = lp(buf, 800 + bright * 9000, 1)
    out = np.zeros(N + P)
    out[:P] = buf
    i = P
    while i < N:
        prev = out[i - P:i]
        before = out[i - P - 1] if i > P else prev[0]
        out[i:i + P] = 0.5 * (prev + np.concatenate([[before], prev[:-1]])) * damp
        i += P
    y = out[:N]
    if body:
        y = y + 0.3 * bp(y, 180, 900)
    return fade(y, 0.001, 0.02)


def fm(freq, dur, ratio=1.0, index=2.0, idx_env=None, env=None):
    t = tt(dur)
    ie = idx_env if idx_env is not None else np.ones(len(t))
    mod = index * ie * np.sin(2 * np.pi * freq * ratio * t)
    y = np.sin(2 * np.pi * freq * t + mod)
    return y * (env if env is not None else 1.0)


def bell(freq, dur):
    e = decay(dur, 5)
    y = fm(freq, dur, 3.5, 3.0, decay(dur, 8), e)
    y += 0.4 * fm(freq * 2.01, dur, 1.4, 1.0, decay(dur, 10), decay(dur, 9))
    return y * 0.6


def flute(freq, dur, vib=0.006, breath=0.12):
    t = tt(dur)
    v = 1 + vib * np.sin(2 * np.pi * 5.2 * t) * np.clip(t / 0.3, 0, 1)
    f = freq * v
    y = sine(f, dur) + 0.25 * sine(2 * f, dur) + 0.08 * sine(3 * f, dur)
    y += breath * bp(noise(dur), freq * 0.8, freq * 3)
    return y * adsr(dur, 0.06, 0.1, 0.85, min(0.15, dur * 0.3))


def reed(freq, dur, bright=2500):
    """Shawm / oboe / clarinet-ish."""
    t = tt(dur)
    f = freq * (1 + 0.004 * np.sin(2 * np.pi * 5.5 * t))
    y = square(f, dur, 0.35) * 0.6 + saw(f, dur) * 0.4
    y = lp(y, bright, 2)
    return y * adsr(dur, 0.03, 0.08, 0.8, min(0.1, dur * 0.3))


def brass(freq, dur, bright=1.0):
    t = tt(dur)
    f = freq * (1 + 0.003 * np.sin(2 * np.pi * 5 * t))
    y = saw(f, dur)
    cut = freq * (1.5 + 5 * bright * adsr(dur, 0.08, 0.2, 0.6, 0.1))
    y = sweep(y, cut, 'low', 1.2)
    return y * adsr(dur, 0.04, 0.1, 0.85, min(0.12, dur * 0.3))


def strings(freq, dur, a=0.25, r=0.4, bright=3000):
    """Detuned saw ensemble (pad / string section)."""
    y = np.zeros(n(dur))
    t = tt(dur)
    for d in (-0.007, -0.002, 0.003, 0.008):
        f = freq * (1 + d) * (1 + 0.003 * np.sin(2 * np.pi * (4.8 + d * 100) * t))
        y += saw(f, dur, rng.random())
    y = lp(y / 4, bright, 2)
    return y * adsr(dur, a, 0.2, 0.9, min(r, dur * 0.5))


def pad(freq, dur, a=1.0, r=1.5, bright=1800):
    y = np.zeros(n(dur))
    for d in (-0.01, 0, 0.011):
        y += saw(freq * (1 + d), dur, rng.random()) + 0.5 * tri(freq * 2 * (1 + d), dur)
    y = lp(y / 4, bright, 2)
    return y * adsr(dur, a, 0.5, 0.9, min(r, dur * 0.6))


def organ(freq, dur):
    y = sum(g * sine(freq * h, dur) for h, g in ((0.5, 0.5), (1, 1), (2, 0.6), (3, 0.3), (4, 0.25), (8, 0.1)))
    return y / 2.5 * adsr(dur, 0.01, 0.05, 1, 0.05)


def harpsichord(freq, dur):
    y = pluck(freq, dur, bright=1.0, damp=0.995, body=False)
    y += 0.5 * pluck(freq * 2, dur, bright=1.0, damp=0.993, body=False)
    return hp(y, 120)


def piano(freq, dur):
    e = decay(max(dur, 0.6), 4.5)[:n(dur)]
    y = np.zeros(n(dur))
    for h in range(1, 9):
        inh = h * np.sqrt(1 + 0.0004 * h * h)
        y += (1 / h ** 1.3) * sine(freq * inh, dur) * decay(dur, 4 + h * 0.8)[:n(dur)]
    y += 0.2 * lp(noise(dur), 3000) * decay(dur, 60)
    return fade(y * e ** 0.3, 0.001, 0.05)


def chip(freq, dur, duty=0.25):
    return square(freq, dur, duty) * adsr(dur, 0.002, 0.05, 0.6, 0.03)


def theremin(freq, dur):
    t = tt(dur)
    f = freq * (1 + 0.012 * np.sin(2 * np.pi * 6 * t) * np.clip(t / 0.4, 0, 1))
    y = sine(f, dur) + 0.15 * sine(2 * f, dur)
    return y * adsr(dur, 0.12, 0.1, 0.9, min(0.25, dur * 0.4))


def synth_lead(freq, dur, bright=4000):
    y = saw(freq, dur) + saw(freq * 1.006, dur) + 0.5 * square(freq / 2, dur)
    y = sweep(y / 2.5, freq + bright * adsr(dur, 0.01, 0.25, 0.4, 0.1), 'low', 2.0)
    return y * adsr(dur, 0.005, 0.1, 0.8, min(0.1, dur * 0.3))


def bass(freq, dur, bright=600):
    y = saw(freq, dur) * 0.6 + sine(freq, dur)
    y = sweep(y, freq + bright * decay(dur, 6), 'low', 1.5)
    return y * adsr(dur, 0.005, 0.1, 0.8, 0.05)


def choir(freq, dur, vowel='a'):
    formants = {'a': (800, 1150, 2900), 'o': (450, 800, 2830), 'u': (325, 700, 2530), 'e': (400, 1600, 2700)}[vowel]
    t = tt(dur)
    y = np.zeros(n(dur))
    for d in (-0.006, 0, 0.005):
        f = freq * (1 + d) * (1 + 0.005 * np.sin(2 * np.pi * (5 + 30 * d) * t))
        y += saw(f, dur, rng.random())
    out = sum(g * bp(y, fc * 0.9, fc * 1.1) for fc, g in zip(formants, (1, 0.6, 0.3)))
    return out * adsr(dur, 0.3, 0.2, 0.9, min(0.5, dur * 0.4))


# ---------------------------------------------------------------- drums

def kick(dur=0.45, f0=150, f1=45, click=0.3):
    f = f1 + (f0 - f1) * np.exp(-tt(dur) * 30)
    y = sine(f, dur) * decay(dur, 7)
    y += click * hp(noise(dur), 2000) * decay(dur, 200)
    return y


def snare(dur=0.25, tone=190):
    y = 0.6 * sine(glide(tone * 1.4, tone, dur), dur) * decay(dur, 18)
    y += bp(noise(dur), 1500, 9000) * decay(dur, 9)
    return y


def hat(dur=0.06, open_=False):
    d = 0.35 if open_ else dur
    return hp(noise(d), 7000, 4) * decay(d, 5 if open_ else 8)


def tom(freq=110, dur=0.4):
    f = freq * (1 + 0.6 * np.exp(-tt(dur) * 25))
    return sine(f, dur) * decay(dur, 6) + 0.15 * bp(noise(dur), 300, 2000) * decay(dur, 30)


def frame_drum(freq=90, dur=0.5):
    return tom(freq, dur) * 0.9 + 0.25 * lp(noise(dur), 1200) * decay(dur, 20)


def taiko(dur=1.0):
    f = 60 * (1 + 0.8 * np.exp(-tt(dur) * 18))
    return sine(f, dur) * decay(dur, 5) + 0.3 * lp(noise(dur), 500) * decay(dur, 15)


def shaker(dur=0.12):
    return bp(noise(dur), 4000, 12000) * adsr(dur, 0.03, 0.03, 0.4, 0.06)


def timpani(freq=80, dur=1.6):
    y = sum(g * sine(freq * h, dur) * decay(dur, 3 + h) for h, g in ((1, 1), (1.5, 0.5), (1.99, 0.4), (2.44, 0.25)))
    return y + 0.2 * lp(noise(dur), 800) * decay(dur, 40)


def clap(dur=0.3):
    y = np.zeros(n(dur))
    for k, off in enumerate((0, 0.01, 0.02, 0.032)):
        s = n(off)
        seg = bp(noise(dur - off), 900, 5000) * decay(dur - off, 25 if k < 3 else 10)
        seg = seg[:len(y) - s]
        y[s:s + len(seg)] += seg
    return y


# ---------------------------------------------------------------- mixing

def stereo(x, pan=0.0):
    a = (pan + 1) * np.pi / 4
    return np.stack([x * np.cos(a), x * np.sin(a)], axis=1)


class Mix:
    """A stereo canvas; place mono or stereo sounds at times (seconds)."""

    def __init__(self, dur):
        self.buf = np.zeros((n(dur), 2))

    def add(self, x, at=0.0, gain=1.0, pan=0.0):
        if x.ndim == 1:
            x = stereo(x, pan)
        s = n(at) if at > 0 else 0
        if s >= len(self.buf):
            need = s + len(x) - len(self.buf)
            self.buf = np.concatenate([self.buf, np.zeros((need, 2))])
        e = s + len(x)
        if e > len(self.buf):
            self.buf = np.concatenate([self.buf, np.zeros((e - len(self.buf), 2))])
        self.buf[s:e] += x * gain
        return self


def fx(x, *plugins):
    """Run pedalboard plugins over mono or stereo audio."""
    st = x if x.ndim == 2 else stereo(x, 0) * np.sqrt(2)
    out = pb.Pedalboard(list(plugins))(st.T.astype(np.float32), SR)
    return out.T.astype(np.float64)


def reverb(x, room=0.5, wet=0.25, damp=0.5, width=1.0, tail=1.0):
    st = x if x.ndim == 2 else stereo(x, 0) * np.sqrt(2)
    st = np.concatenate([st, np.zeros((n(tail), 2))])
    return fx(st, pb.Reverb(room_size=room, wet_level=wet, dry_level=1 - wet * 0.5, damping=damp, width=width))


def trim_silence(x, thresh=1e-4):
    a = np.abs(x).max(axis=1) if x.ndim == 2 else np.abs(x)
    idx = np.where(a > thresh)[0]
    if len(idx) == 0:
        return x
    return x[:idx[-1] + n(0.01)]


def normalize(x, peak=0.89):
    m = np.max(np.abs(x))
    return x * (peak / m) if m > 0 else x


def rms_db(x):
    return 20 * np.log10(np.sqrt(np.mean(x ** 2)) + 1e-12)


# ---------------------------------------------------------------- output

# Output folder. Set AOS_AUDIO_OUT to render somewhere else first (e.g. while the game is playing in the
# editor: files landing in Assets mid-play make Unity re-import and recompile) and copy in afterwards.
ROOT = os.environ.get('AOS_AUDIO_OUT') or os.path.normpath(
    os.path.join(os.path.dirname(__file__), '..', '..', 'Assets', 'Resources', 'Audio'))


def loudness(x, win=0.3):
    """RMS (dB) of the loudest `win`-second stretch: how loud a sound feels, ignoring its tail."""
    m = x.mean(1) if x.ndim == 2 else x
    w = min(len(m), n(win))
    c = np.concatenate([[0], np.cumsum(m ** 2)])
    e = (c[w:] - c[:-w]) / w
    return 10 * np.log10(e.max() + 1e-12)


def save(x, rel, peak=0.89, ogg=False, trim=True, mono=False, loud=None):
    """Write to Assets/Audio/<rel>(.wav|.ogg). Removes DC, trims tail, sets level, fades the end.
    loud: target loudness in dB (loudest 0.3 s for sounds; whole-file RMS when trim=False); peak caps it."""
    if x.ndim == 1:
        x = stereo(x, 0) * np.sqrt(2)
    x = hp(x, 20, 1)
    if trim:
        x = trim_silence(x)
    if loud is not None:
        cur = loudness(x) if trim else rms_db(x.mean(1))
        x = x * 10 ** ((loud - cur) / 20)
        if np.max(np.abs(x)) > peak:
            x = normalize(x, peak)
    else:
        x = normalize(x, peak)
    x = np.stack([fade(x[:, 0], 0.0005, 0.01), fade(x[:, 1], 0.0005, 0.01)], axis=1) if trim else x
    if mono:
        x = x.mean(axis=1, keepdims=True)
    path = os.path.join(ROOT, rel + ('.ogg' if ogg else '.wav'))
    os.makedirs(os.path.dirname(path), exist_ok=True)
    if ogg:
        # libsndfile crashes writing long Vorbis files in one call; write in blocks
        with sf.SoundFile(path, 'w', SR, x.shape[1], format='OGG', subtype='VORBIS', compression_level=0.35) as f:
            for i in range(0, len(x), 8192):
                f.write(x[i:i + 8192].astype(np.float32))
    else:
        sf.write(path, x.astype(np.float32), SR, subtype='PCM_16')
    return path
