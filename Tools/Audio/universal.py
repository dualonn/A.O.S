"""Universal Age music: CC0 choral/ambient pieces with a slow, distant clock-tick motif (creation and time).
Usage: python universal.py <downloads_dir>   -> Assets/Resources/Audio/Music/universal_*.ogg
"""
import sys
import numpy as np
import pedalboard as pb
import import_sounds as imp
from synth import SR, n, rms_db, hp, lp

imp.DL = sys.argv[1]


def load(pattern):
    x = imp.load(imp.find(pattern))
    return x if x.shape[1] == 2 else np.repeat(x, 2, 1)


def repeat_loop(x, times, xf=3.0):
    """Chain a loop with cross-fades into one longer piece."""
    X = n(xf)
    out = x.copy()
    w = np.linspace(0, 1, X)[:, None]
    for _ in range(times - 1):
        out[-X:] = out[-X:] * (1 - w) + x[:X] * w
        out = np.concatenate([out, x[X:]])
    return out


def clock_layer(length, level_db, period=1.0, start=4.0):
    """Tick ... tock, once a second, far away in a big hall; fades in and out."""
    ticks = [load(f'ticking clock - tick{i}.wav').mean(1) for i in (1, 2, 3, 4)]
    dry = np.zeros(length)
    t, k = start, 0
    while n(t) < length - n(1.0):
        tk = ticks[k % 4] * (1.0 if k % 2 == 0 else 0.7)   # tick louder than tock
        s = n(t)
        e = min(length, s + len(tk))
        dry[s:e] += tk[:e - s]
        t += period
        k += 1
    dry = lp(hp(dry, 300), 5000)
    st = np.stack([dry * 0.9, dry * 0.7], 1)
    wet = pb.Pedalboard([pb.Reverb(room_size=0.95, wet_level=0.55, dry_level=0.35, damping=0.4, width=1.0)])(
        st.T.astype(np.float32), SR).T.astype(np.float64)[:length]
    env = np.clip(np.arange(length) / n(10), 0, 1) * np.clip((length - np.arange(length)) / n(12), 0, 1)
    wet *= env[:, None]
    return wet, level_db


def build(name, music, tick_db):
    clock, _ = clock_layer(len(music), tick_db)
    # set the clock relative to the music's loudness
    clock *= 10 ** ((rms_db(music.mean(1)) + tick_db - rms_db(clock.mean(1) + 1e-12)) / 20)
    x = music + clock
    x *= 10 ** ((-18 - rms_db(x.mean(1))) / 20)
    x *= min(1.0, 0.89 / np.abs(x).max())
    path = imp.write(x, f'Music/{name}', ogg=True, quality=0.62)
    print(f'{name}: {len(x) / SR:.1f}s -> {path}')


build('universal_1', repeat_loop(load('Heavenly Loop.flac'), 5), -16)
build('universal_2', repeat_loop(load('ethereal/ethereal.mp3'), 4, 4.0), -17)
build('universal_3', load('first_light_particles_0.wav'), -19)
