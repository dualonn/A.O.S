"""A.O.S sound effects. Usage:
    python sfx.py               # render everything
    python sfx.py sword bow     # render only sounds whose name contains any of these words
Output: Assets/Audio/SFX/<Group>/<name>.wav (short) and Assets/Audio/Ambience/<name>.ogg (loops).
"""
import sys
import numpy as np
import pedalboard as pb
from synth import *

SOUNDS = {}


def sound(group, variants=1):
    def reg(fn):
        for v in range(1, variants + 1):
            name = fn.__name__ + (f'_{v}' if variants > 1 else '')
            SOUNDS[name] = (group, fn, v)
        return fn
    return reg


def room(x, size=0.25, wet=0.12):
    return reverb(x, size, wet, 0.6, 0.8, 0.6)


def metal_ring(freq, dur, k=6):
    """Inharmonic struck metal (blades, armour, anvils)."""
    y = np.zeros(n(dur))
    for r, g in ((1, 1), (2.76, 0.6), (5.4, 0.4), (8.93, 0.25), (13.3, 0.12)):
        y += g * sine(freq * r * (1 + rng.uniform(-0.01, 0.01)), dur) * decay(dur, k + r * 0.8)
    return y


def whoosh(dur, f0, f1, q=1.5, shape=None):
    x = noise(dur)
    y = sweep(x, (f0, f1), 'band', q)
    env = shape if shape is not None else np.sin(np.pi * np.linspace(0, 1, n(dur))) ** 2
    return y * env


def thud(freq=80, dur=0.25):
    return sine(glide(freq * 2, freq, dur), dur) * decay(dur, 9) + 0.5 * lp(noise(dur), 600) * decay(dur, 25)


# ================================================================ combat

@sound('Combat', 3)
def sword_swing(v):
    seed(10 + v)
    d = 0.22 + 0.04 * v
    y = whoosh(d, 600 + 200 * v, 3500 + 500 * v, 2.0, np.sin(np.pi * np.linspace(0, 1, n(d)) ** 0.7) ** 3)
    return room(y, 0.15, 0.06)


@sound('Combat', 3)
def sword_hit(v):
    seed(20 + v)
    d = 0.5
    y = metal_ring(900 + 170 * v, d, 9) * 0.5
    y += hp(noise(d), 2500) * decay(d, 40)
    y += 0.6 * thud(110, d)
    return room(y)


@sound('Combat', 2)
def blunt_hit(v):
    seed(30 + v)
    d = 0.3
    y = thud(70 + 15 * v, d) + 0.4 * bp(noise(d), 200, 1500) * decay(d, 30)
    return room(y, 0.2, 0.08)


@sound('Combat', 2)
def flesh_hit(v):
    seed(35 + v)
    d = 0.22
    y = lp(noise(d), 1400) * decay(d, 22) + 0.7 * thud(90 + 20 * v, d)
    return room(y, 0.15, 0.05)


@sound('Combat', 2)
def shield_block(v):
    seed(40 + v)
    d = 0.6
    y = thud(130, d) + 0.5 * bp(noise(d), 400, 2500) * decay(d, 20)
    y += 0.25 * metal_ring(420 + 60 * v, d, 7)
    return room(y)


@sound('Combat', 2)
def bow_shot(v):
    seed(50 + v)
    d = 0.35
    y = pluck(140 + 20 * v, d, 0.9, 0.985, False) * 0.8
    w = whoosh(0.18, 2000, 6000, 3) * 0.5
    y[:len(w)] += w
    return room(y, 0.15, 0.06)


@sound('Combat', 2)
def arrow_hit(v):
    seed(60 + v)
    d = 0.25
    y = bp(noise(d), 1500, 6000) * decay(d, 50) + 0.7 * thud(160 + 30 * v, d)
    y += 0.3 * pluck(320 + 40 * v, d, 0.6, 0.97, False)
    return room(y, 0.15, 0.05)


@sound('Combat')
def crossbow_shot(v):
    seed(70)
    d = 0.45
    y = 0.8 * pluck(95, d, 1.0, 0.98, False)
    y += hp(noise(d), 1500) * decay(d, 60)  # trigger clack
    y += 0.5 * metal_ring(1800, d, 25)
    w = whoosh(0.15, 2500, 7000, 3) * 0.4
    y[:len(w)] += w
    return room(y)


@sound('Combat')
def throw(v):
    seed(75)
    return room(whoosh(0.3, 400, 2200, 1.5) * 0.9, 0.15, 0.05)


@sound('Combat')
def staff_cast(v):
    seed(80)
    d = 1.0
    f = glide(300, 1200, d)
    y = 0.5 * sine(f, d) * sine(f * 1.5, d) + 0.4 * whoosh(d, 500, 6000, 4)
    y += 0.3 * sum(sine(f * k * (1 + 0.003 * k), d) for k in (2, 3, 5)) * 0.3
    y *= adsr(d, 0.05, 0.3, 0.6, 0.4)
    return reverb(fx(y, pb.Chorus(rate_hz=3, depth=0.5, mix=0.5)), 0.6, 0.3, 0.4, 1, 1.5)


@sound('Combat')
def magic_hit(v):
    seed(85)
    d = 0.8
    y = sum(bell(hz(m), d) for m in (84, 88, 91)) * 0.4
    y += whoosh(d, 6000, 500, 2, decay(d, 6)) * 0.6
    return reverb(y, 0.5, 0.3, 0.4, 1, 1.2)


@sound('Combat', 2)
def musket_shot(v):
    seed(90 + v)
    d = 1.2
    crack = hp(noise(d), 800) * decay(d, 40)
    boom = lp(noise(d), 500 + 100 * v) * decay(d, 9) + 0.8 * sine(glide(120, 40, d), d) * decay(d, 12)
    y = crack * 0.9 + boom
    y = fx(y, pb.Distortion(drive_db=8))
    return reverb(y, 0.7, 0.25, 0.5, 1, 1.5)


@sound('Combat')
def cannon_shot(v):
    seed(95)
    d = 2.2
    boom = lp(noise(d), 300) * decay(d, 7) + sine(glide(90, 28, d), d) * decay(d, 5)
    y = boom * 1.2 + 0.5 * hp(noise(d), 1000) * decay(d, 35)
    y = fx(y, pb.Distortion(drive_db=12), pb.LowpassFilter(3000))
    return reverb(y, 0.85, 0.3, 0.6, 1, 2.0)


@sound('Combat', 2)
def explosion(v):
    seed(100 + v)
    d = 2.5
    y = lp(brown(d), 900) * decay(d, 5, 0.01) * 1.5 + sine(glide(70, 25, d), d) * decay(d, 4)
    debris = hp(noise(d), 2000) * decay(d, 6) * (rng.random(n(d)) > 0.995)
    y += lp(debris, 6000) * 3
    y = fx(y, pb.Distortion(drive_db=10))
    return reverb(y, 0.9, 0.3, 0.5, 1, 2.0)


@sound('Combat', 2)
def body_fall(v):
    seed(110 + v)
    d = 0.6
    y = thud(65, d) * 0.9
    s = n(0.12 + 0.03 * v)
    y[s:] += 0.6 * thud(80, d)[:len(y) - s]
    y += 0.3 * lp(noise(d), 800) * decay(d, 8)
    return room(y, 0.2, 0.06)


@sound('Combat', 2)
def armor_clank(v):
    seed(115 + v)
    d = 0.35
    y = np.zeros(n(d))
    for k in range(3):
        s = n(rng.uniform(0, 0.12))
        r = metal_ring(rng.uniform(1500, 3000), d, 25)[:len(y) - s]
        y[s:] += r * rng.uniform(0.3, 0.6)
    return room(y, 0.15, 0.05)


# ---------------------------------------------------------------- bosses

@sound('Boss')
def boss_slam(v):
    seed(120)
    d = 2.5
    y = lp(brown(d), 200) * decay(d, 6) * 1.5 + sine(glide(55, 22, d), d) * decay(d, 4) * 1.5
    y += 0.6 * lp(noise(d), 3000) * decay(d, 14)
    rubble = lp(hp(noise(d), 600) * (rng.random(n(d)) > 0.993), 4000) * decay(d, 3, 0.2)
    y += rubble * 4
    y = fx(y, pb.Distortion(drive_db=9))
    return reverb(y, 0.9, 0.3, 0.5, 1, 2)


@sound('Boss')
def boss_warcry(v):
    seed(125)
    d = 2.2
    # roaring growl: low choir through distortion + war horn
    t = tt(d)
    f = 95 * (1 + 0.15 * np.sin(np.pi * t / d)) * (1 + 0.02 * rng.standard_normal(n(d)).cumsum() / 400)
    growl = sum(bp(saw(f * k, d), 300, 2400) for k in (1, 1.01, 0.995))
    growl *= adsr(d, 0.08, 0.3, 0.8, 0.6) * (1 + 0.4 * np.sin(2 * np.pi * 23 * t))
    horn = brass(hz('D3'), d, 0.8) + 0.6 * brass(hz('A3'), d, 0.6)
    y = fx(growl * 0.6, pb.Distortion(drive_db=14)) [:, 0] + horn * 0.5
    return reverb(y, 0.8, 0.3, 0.5, 1, 1.5)


@sound('Boss')
def boss_summon(v):
    seed(130)
    d = 2.5
    y = whoosh(d, 100, 1500, 3, adsr(d, 1.2, 0.3, 0.8, 0.8)) * 0.8
    y += 0.5 * sum(sine(hz(m) * (1 + 0.004 * np.sin(2 * np.pi * 3 * tt(d))), d) for m in (38, 45, 49)) * adsr(d, 1.0, 0.3, 0.8, 0.9)
    s = n(1.5)
    y[s:] += 1.2 * thud(55, d - 1.5)[:len(y) - s]
    return reverb(y, 0.85, 0.35, 0.5, 1, 2)


# ================================================================ building

@sound('Building')
def place(v):
    seed(200)
    d = 0.3
    y = thud(120, d) + 0.4 * bp(noise(d), 300, 2500) * decay(d, 25)
    return room(y)


@sound('Building', 3)
def hammer(v):
    seed(205 + v)
    d = 0.35
    y = 0.8 * thud(200 + 30 * v, d) + 0.5 * bp(noise(d), 800, 4000) * decay(d, 50)
    y += 0.25 * pluck(260 + 40 * v, d, 0.8, 0.97, True)
    return room(y, 0.25, 0.1)


@sound('Building', 2)
def saw_wood(v):
    seed(210 + v)
    d = 0.9
    t = tt(d)
    stroke = np.abs(np.sin(2 * np.pi * 1.7 * t)) ** 0.5
    y = bp(noise(d), 1800, 5000) * stroke * (0.6 + 0.4 * (np.sin(2 * np.pi * 140 * t) > 0))
    return room(fade(y, 0.02, 0.1))


@sound('Building')
def build_complete(v):
    seed(215)
    y = Mix(2)
    for i, m in enumerate((67, 72, 76, 79)):
        y.add(pluck(hz(m), 1.2, 0.7, 0.997) * 0.6, i * 0.08, pan=-0.3 + 0.2 * i)
    y.add(thud(150, 0.3), 0, 0.6)
    y.add(bell(hz(84), 1.5) * 0.4, 0.32)
    return reverb(y.buf, 0.5, 0.2, 0.5, 1, 1)


@sound('Building')
def demolish(v):
    seed(220)
    d = 2.0
    y = np.zeros(n(d))
    for k in range(14):
        s = n(rng.uniform(0, 1.0) ** 1.5)
        hit = thud(rng.uniform(70, 160), 0.5) * rng.uniform(0.3, 1)
        hit += 0.4 * bp(noise(0.5), 400, 3000) * decay(0.5, 18) * rng.uniform(0.3, 1)
        e = min(len(y), s + len(hit))
        y[s:e] += hit[:e - s]
    y += 0.5 * lp(noise(d), 700) * decay(d, 3, 0.05)
    return reverb(y, 0.6, 0.2, 0.5, 1, 1)


@sound('Building')
def gate_open(v):
    seed(225)
    d = 1.6
    t = tt(d)
    creak = bp(saw(180 + 60 * np.sin(2 * np.pi * 0.7 * t) + 15 * rng.standard_normal(n(d)), d), 400, 2000)
    creak *= (0.5 + 0.5 * np.sin(2 * np.pi * 9 * t) ** 2) * adsr(d, 0.1, 0.2, 0.7, 0.4)
    y = creak * 0.6
    s = n(1.3)
    y[s:] += thud(80, d - 1.3)[:len(y) - s]
    return room(y, 0.4, 0.15)


@sound('Building')
def gate_close(v):
    seed(226)
    d = 0.9
    y = whoosh(0.35, 300, 900, 1.2) * 0.3
    y = np.concatenate([y, np.zeros(n(d) - len(y))])
    s = n(0.3)
    y[s:] += (thud(70, d - 0.3) * 1.2 + 0.3 * metal_ring(300, d - 0.3, 10))[:len(y) - s]
    return room(y, 0.4, 0.15)


@sound('Building')
def forge(v):
    seed(230)
    d = 1.2
    y = metal_ring(1100, d, 5) * 0.6 + thud(180, d) * 0.5 + hp(noise(d), 3000) * decay(d, 60)
    return room(y, 0.35, 0.12)


# ================================================================ gathering

@sound('Gather', 3)
def chop(v):
    seed(300 + v)
    d = 0.35
    y = thud(150 + 25 * v, d) * 0.9 + bp(noise(d), 600, 3500) * decay(d, 30) * 0.7
    y += 0.3 * pluck(380 + 30 * v, d, 0.5, 0.95, True)
    return room(y, 0.3, 0.1)


@sound('Gather')
def tree_fall(v):
    seed(310)
    d = 2.8
    t = tt(d)
    crack = bp(noise(d), 800, 5000) * (rng.random(n(d)) > 0.97) * np.clip(1.4 - t, 0, 1)
    y = lp(crack, 6000) * 2
    leaves = bp(noise(1.2), 2500, 8000) * np.linspace(0, 1, n(1.2)) ** 2 * 0.6
    s = n(0.9)
    y[s:s + len(leaves)] += leaves
    s2 = n(2.0)
    y[s2:] += (thud(55, d - 2.0) * 1.5 + 0.5 * lp(noise(d - 2.0), 900) * decay(d - 2.0, 6))
    return reverb(y, 0.6, 0.2, 0.6, 1, 1.2)


@sound('Gather', 3)
def pick(v):
    seed(320 + v)
    d = 0.45
    y = metal_ring(1400 + 200 * v, d, 18) * 0.4 + thud(180, d) * 0.6
    y += 0.6 * bp(noise(d), 1500, 7000) * decay(d, 35)
    # pebbles
    for k in range(4):
        s = n(rng.uniform(0.05, 0.25))
        pb_ = bp(noise(0.05), 3000, 8000) * decay(0.05, 8) * 0.3
        y[s:s + len(pb_)] += pb_[:len(y) - s]
    return room(y, 0.35, 0.12)


@sound('Gather', 2)
def forage(v):
    seed(330 + v)
    d = 0.4
    y = bp(noise(d), 1500, 7000) * adsr(d, 0.05, 0.1, 0.5, 0.2) * (0.6 + 0.4 * rng.random(n(d)))
    y += 0.3 * thud(250, d)
    return room(y, 0.2, 0.05)


@sound('Gather', 2)
def hoe(v):
    seed(335 + v)
    d = 0.35
    y = lp(noise(d), 1500) * decay(d, 14) + 0.6 * thud(120 + 20 * v, d)
    return room(y, 0.2, 0.05)


@sound('Gather', 2)
def splash(v):
    seed(340 + v)
    d = 0.7
    y = bp(noise(d), 600, 6000) * decay(d, 7, 0.01)
    for k in range(6):
        s = n(rng.uniform(0.05, 0.35))
        f0 = rng.uniform(600, 1400)
        bub = sine(glide(f0, f0 * 2.2, 0.06), 0.06) * decay(0.06, 5) * 0.3
        y[s:s + len(bub)] += bub
    return room(y, 0.3, 0.1)


@sound('Gather')
def coins(v):
    seed(345)
    d = 0.7
    y = np.zeros(n(d))
    for k in range(7):
        s = n(rng.uniform(0, 0.25))
        c = metal_ring(rng.uniform(2800, 4200), d - 0.25, 14) * rng.uniform(0.3, 0.7)
        y[s:s + len(c)] += c
    return room(y, 0.2, 0.08)


# ================================================================ UI

@sound('UI')
def click(v):
    seed(400)
    d = 0.05
    return sine(glide(1800, 1200, d), d) * decay(d, 10) + 0.3 * hp(noise(d), 4000) * decay(d, 40)


@sound('UI')
def hover(v):
    d = 0.04
    return 0.4 * sine(2400, d) * decay(d, 8)


@sound('UI')
def panel_open(v):
    seed(405)
    d = 0.18
    y = whoosh(d, 800, 3000, 1.5) * 0.5 + 0.4 * pluck(hz(76), d, 0.6, 0.99, False)
    return room(y, 0.2, 0.08)


@sound('UI')
def panel_close(v):
    seed(406)
    d = 0.16
    y = whoosh(d, 3000, 800, 1.5) * 0.5 + 0.4 * pluck(hz(69), d, 0.6, 0.99, False)
    return room(y, 0.2, 0.08)


@sound('UI')
def tab(v):
    seed(407)
    return pluck(hz(81), 0.12, 0.8, 0.98, False) * 0.7


@sound('UI')
def error(v):
    d = 0.3
    y = Mix(0.4)
    for i in range(2):
        y.add(lp(square(hz(50), 0.12, 0.4), 1500) * adsr(0.12, 0.005, 0.03, 0.8, 0.03), i * 0.13)
    return y.buf


@sound('UI')
def confirm(v):
    y = Mix(0.6)
    y.add(pluck(hz(72), 0.4, 0.7, 0.995, False), 0)
    y.add(pluck(hz(79), 0.5, 0.7, 0.995, False), 0.07)
    return room(y.buf, 0.3, 0.12)


@sound('UI')
def notify(v):
    y = Mix(1.2)
    y.add(bell(hz(81), 1.0) * 0.6, 0)
    y.add(bell(hz(88), 1.0) * 0.5, 0.12)
    return reverb(y.buf, 0.5, 0.2, 0.5, 1, 1)


@sound('UI')
def research_complete(v):
    y = Mix(2.2)
    for i, m in enumerate((72, 76, 79, 84)):
        y.add(bell(hz(m), 1.6) * 0.45, i * 0.11, pan=-0.4 + 0.27 * i)
    y.add(pad(hz(60), 1.8, 0.05, 1.0, 2500) * 0.25 + pad(hz(67), 1.8, 0.05, 1.0, 2500) * 0.2, 0.3)
    return reverb(y.buf, 0.7, 0.3, 0.4, 1, 1.5)


@sound('UI')
def goal_complete(v):
    y = Mix(1.6)
    for i, m in enumerate((67, 71, 74, 79)):
        y.add(pluck(hz(m), 1.0, 0.8, 0.997) * 0.7, i * 0.07)
    y.add(bell(hz(91), 1.2) * 0.3, 0.3)
    return reverb(y.buf, 0.5, 0.25, 0.5, 1, 1)


@sound('UI')
def age_up(v):
    """Big fanfare: brass call + timpani + choir swell."""
    seed(420)
    y = Mix(5)
    call = [(0, 0.25, 'C4'), (0.25, 0.25, 'G4'), (0.5, 0.25, 'C5'), (0.75, 1.3, 'E5')]
    for at, d, m in call:
        y.add(brass(hz(m), d + 0.1, 1.0) * 0.5, at, pan=-0.1)
        y.add(brass(hz(m) / 2, d + 0.1, 0.7) * 0.35, at, pan=0.15)
    for m in ('C3', 'G3', 'C4', 'E4', 'G4'):
        y.add(choir(hz(m), 3.5, 'a') * 0.18, 0.75, pan=rng.uniform(-0.5, 0.5))
        y.add(strings(hz(m), 3.5, 0.3, 1.5) * 0.15, 0.75, pan=rng.uniform(-0.5, 0.5))
    for at in (0, 0.5, 0.75):
        y.add(timpani(hz('C2'), 1.6) * 0.5, at)
    y.add(bell(hz('C6'), 3) * 0.3, 0.75)
    return reverb(y.buf, 0.85, 0.3, 0.4, 1, 2.5)


@sound('UI')
def raid_warning(v):
    """War horn, two long blasts."""
    seed(425)
    y = Mix(4)
    for at, d in ((0, 1.1), (1.3, 1.9)):
        f = hz('A2') * (1 + 0.03 * np.clip(1 - tt(d) / 0.15, 0, 1) * -1)
        h = saw(f, d) * 0.7 + saw(f * 1.004, d) * 0.5
        h = sweep(h, 200 + 1200 * adsr(d, 0.25, 0.3, 0.7, 0.3), 'low', 1.5) * adsr(d, 0.15, 0.2, 0.85, 0.35)
        y.add(h, at)
        y.add(taiko(1.0) * 0.7, at)
    return reverb(y.buf, 0.9, 0.35, 0.5, 1, 2.5)


@sound('UI')
def victory(v):
    y = Mix(4)
    mel = [(0, 'G4', 0.2), (0.2, 'C5', 0.2), (0.4, 'E5', 0.2), (0.6, 'G5', 0.6), (1.2, 'E5', 0.2), (1.4, 'G5', 1.4)]
    for at, m, d in mel:
        y.add(brass(hz(m), d + 0.1, 1.0) * 0.5, at)
        y.add(flute(hz(m) * 2, d + 0.1) * 0.15, at)
    for m in ('C3', 'E3', 'G3', 'C4'):
        y.add(strings(hz(m), 2.6, 0.2, 1.2) * 0.2, 1.4)
    y.add(timpani(hz('C2')) * 0.5, 0.6)
    y.add(timpani(hz('G1')) * 0.5, 1.4)
    return reverb(y.buf, 0.8, 0.3, 0.4, 1, 2)


@sound('UI')
def defeat(v):
    y = Mix(4.5)
    for i, (m, d) in enumerate((('E4', 0.7), ('D4', 0.7), ('C4', 0.7), ('B3', 2.2))):
        y.add(strings(hz(m), d + 0.3, 0.1, 0.6, 2000) * 0.4, i * 0.7)
        y.add(strings(hz(m) / 2, d + 0.3, 0.1, 0.6, 1500) * 0.3, i * 0.7)
    y.add(timpani(hz('E1'), 2.5) * 0.5, 2.1)
    return reverb(y.buf, 0.85, 0.3, 0.5, 1, 2)


# ---------------------------------------------------------------- items & heroes

@sound('Items')
def pickup(v):
    y = Mix(0.5)
    y.add(pluck(hz(76), 0.3, 0.8, 0.99, False) * 0.6, 0)
    y.add(pluck(hz(83), 0.4, 0.8, 0.99, False) * 0.6, 0.06)
    return room(y.buf, 0.2, 0.1)


@sound('Items')
def equip(v):
    seed(500)
    d = 0.35
    y = 0.6 * bp(noise(d), 600, 4000) * decay(d, 14) + 0.3 * metal_ring(1300, d, 14)
    return room(y, 0.2, 0.08)


RARITY = {'common': (72, [0]), 'uncommon': (72, [0, 7]), 'rare': (72, [0, 4, 7]),
          'epic': (74, [0, 3, 7, 10]), 'legendary': (72, [0, 4, 7, 11, 14, 19])}


def _drop(r):
    base, iv = RARITY[r]
    k = list(RARITY).index(r)
    y = Mix(1.5 + 0.4 * k)
    for i, d in enumerate(iv):
        y.add(bell(hz(base + d), 1.2 + 0.3 * k) * 0.45, i * 0.07, pan=(i - len(iv) / 2) * 0.12)
    if k >= 3:
        d = 1.5
        y.add(whoosh(d, 2000, 9000, 6, adsr(d, 0.4, 0.3, 0.5, 0.6)) * 0.25, 0)
    if k == 4:
        for m in (base - 12, base - 5, base):
            y.add(choir(hz(m), 2.2, 'a') * 0.15, 0.2)
    return reverb(y.buf, 0.5 + 0.08 * k, 0.2 + 0.03 * k, 0.4, 1, 1.5)


for _r in RARITY:
    SOUNDS['drop_' + _r] = ('Items', (lambda r: (lambda v: _drop(r)))(_r), 1)


@sound('Items')
def enchant(v):
    seed(510)
    d = 2.0
    y = Mix(d + 0.5)
    for i in range(16):
        m = 84 + [0, 2, 4, 7, 9, 12, 14, 16][i % 8] - (i // 8) * 0
        y.add(bell(hz(m), 0.6) * 0.2, i * 0.08, pan=np.sin(i))
    y.add(whoosh(d, 400, 7000, 3, adsr(d, 0.6, 0.2, 0.7, 0.8)) * 0.4)
    y.add(pad(hz(60), d, 0.4, 0.8, 3000) * 0.3)
    return reverb(fx(y.buf, pb.Chorus(rate_hz=1.5, depth=0.4, mix=0.4)), 0.8, 0.35, 0.4, 1, 1.8)


@sound('Items')
def chest_open(v):
    seed(515)
    d = 0.8
    t = tt(d)
    creak = bp(saw(240 + 80 * t / d, d), 500, 2500) * (0.5 + 0.5 * np.sin(2 * np.pi * 14 * t) ** 2) * adsr(d, 0.05, 0.1, 0.6, 0.2)
    y = Mix(1.6)
    y.add(creak * 0.4)
    y.add(thud(110, 0.3) * 0.6, 0.6)
    for i, m in enumerate((79, 83, 86, 91)):
        y.add(bell(hz(m), 0.8) * 0.25, 0.7 + i * 0.06)
    return room(y.buf, 0.35, 0.15)


@sound('Items')
def level_up(v):
    y = Mix(1.8)
    for i, m in enumerate((60, 64, 67, 72, 76, 79, 84)):
        y.add(pluck(hz(m), 0.8, 0.8, 0.996) * 0.5, i * 0.05, pan=-0.5 + i / 6)
    y.add(bell(hz(96), 1.2) * 0.3, 0.35)
    return reverb(y.buf, 0.5, 0.25, 0.4, 1, 1)


@sound('Items')
def hero_recruit(v):
    seed(520)
    y = Mix(2.5)
    for at, m, d in ((0, 'D4', 0.18), (0.18, 'A4', 0.18), (0.36, 'D5', 1.2)):
        y.add(brass(hz(m), d + 0.1) * 0.5, at)
    y.add(timpani(hz('D2')) * 0.4, 0.36)
    for m in ('D3', 'A3', 'F#4'):
        y.add(strings(hz(m), 1.6, 0.1, 0.8) * 0.18, 0.36)
    return reverb(y.buf, 0.7, 0.25, 0.4, 1, 1.5)


# ================================================================ sites

@sound('Sites')
def dungeon_enter(v):
    seed(600)
    d = 3.0
    y = lp(brown(d), 300) * adsr(d, 0.5, 0.5, 0.7, 1.5) * 0.8
    for m, g in ((33, 0.4), (40, 0.3), (39, 0.2)):
        y += g * sine(hz(m), d) * adsr(d, 1.0, 0.5, 0.8, 1.2)
    drip = Mix(d)
    for at in (0.6, 1.4, 2.3):
        drip.add(sine(glide(1500, 2600, 0.04), 0.04) * decay(0.04, 4) * 0.3, at, pan=rng.uniform(-0.6, 0.6))
    return reverb(drip.buf + stereo(y, 0) * 1.4, 0.95, 0.45, 0.3, 1, 3)


@sound('Sites')
def shrine_bless(v):
    seed(605)
    y = Mix(3.5)
    for i, m in enumerate(('D4', 'F#4', 'A4', 'D5')):
        y.add(choir(hz(m), 3.0, 'a') * 0.3, i * 0.15, pan=-0.3 + 0.2 * i)
    for i, m in enumerate((86, 90, 93, 98)):
        y.add(bell(hz(m), 1.5) * 0.2, 0.5 + i * 0.1)
    return reverb(y.buf, 0.9, 0.4, 0.3, 1, 2.5)


@sound('Sites')
def discover(v):
    """Found ruins, a lore stone, a vein."""
    seed(610)
    y = Mix(2.6)
    for i, m in enumerate(('E4', 'B4', 'E5', 'G#5')):
        y.add(flute(hz(m), 1.8 - i * 0.2) * 0.3, i * 0.18)
    y.add(pad(hz('E3'), 2.2, 0.2, 1.0) * 0.4, 0)
    y.add(bell(hz('B5'), 1.5) * 0.3, 0.6)
    return reverb(y.buf, 0.8, 0.35, 0.4, 1, 2)


@sound('Sites')
def lore_stone(v):
    seed(615)
    d = 3.0
    y = sum(sine(hz(m) * (1 + 0.002 * np.sin(2 * np.pi * (0.5 + i) * tt(d))), d) * g
            for i, (m, g) in enumerate(((45, 0.5), (52, 0.4), (57, 0.3), (64, 0.2))))
    y = y * adsr(d, 0.8, 0.5, 0.8, 1.2) + 0.15 * whoosh(d, 300, 2000, 4, adsr(d, 1, 0.5, 0.6, 1))
    return reverb(fx(y, pb.Chorus(rate_hz=0.7, depth=0.5, mix=0.5)), 0.9, 0.4, 0.4, 1, 2.5)


@sound('Sites')
def camp_destroyed(v):
    seed(620)
    y = Mix(3)
    y.add(demolish(1)[:, 0] * 0.7)
    for i, m in enumerate(('G3', 'C4', 'E4', 'G4')):
        y.add(brass(hz(m), 0.6 if i < 3 else 1.4) * 0.35, 0.6 + i * 0.18)
    return reverb(y.buf, 0.7, 0.25, 0.5, 1, 1.5)


# ================================================================ space / late ages

@sound('Space')
def rocket_launch(v):
    seed(700)
    d = 7.0
    t = tt(d)
    rumble = lp(brown(d), 250) * 1.4 + lp(noise(d), 1200) * 0.6
    env = np.clip(t / 1.2, 0, 1) * np.clip((d - t) / 3.0, 0, 1) ** 1.5
    roar = sweep(noise(d), glide(400, 2500, d), 'low', 0.9) * 0.6
    y = (rumble + roar) * env
    y = fx(y, pb.Distortion(drive_db=6))
    ign = Mix(d)
    ign.add(explosion(1)[:, 0] * 0.6)
    return reverb(ign.buf[:len(y)] + y, 0.9, 0.3, 0.5, 1, 2.5)


@sound('Space')
def probe_launch(v):
    seed(705)
    d = 2.0
    y = whoosh(d, 200, 5000, 2, adsr(d, 0.05, 0.4, 0.4, 1.2)) * 0.8
    y += 0.3 * sine(glide(200, 1600, d), d) * decay(d, 4)
    y += 0.3 * thud(80, d)
    return reverb(fx(y, pb.Phaser(rate_hz=2, depth=0.6, mix=0.5)), 0.7, 0.25, 0.5, 1, 1.5)


@sound('Space')
def warp(v):
    seed(710)
    d = 2.5
    f = glide(60, 1800, d)
    y = saw(f, d) * 0.3 + saw(f * 1.5, d) * 0.2
    y = sweep(y, f * 3, 'low', 4) * adsr(d, 1.5, 0.2, 0.8, 0.5)
    y += whoosh(d, 100, 9000, 2, adsr(d, 1.8, 0.1, 0.8, 0.5)) * 0.4
    s = n(2.0)
    y[s:] += thud(60, d - 2.0)[:len(y) - s] * 0.8
    return reverb(fx(y, pb.Phaser(rate_hz=4, depth=0.8, mix=0.6)), 0.9, 0.35, 0.3, 1, 2)


@sound('Space')
def scan_ping(v):
    d = 1.5
    y = sine(1320, d) * decay(d, 9) + 0.4 * sine(1980, d) * decay(d, 12)
    return reverb(fx(y, pb.Delay(delay_seconds=0.22, feedback=0.4, mix=0.35)), 0.6, 0.2, 0.5, 1, 1)


@sound('Space')
def colony_ship(v):
    seed(715)
    y = Mix(6)
    y.add(rocket_launch(1) * 0.8)
    for i, m in enumerate(('C4', 'G4', 'C5', 'E5')):
        y.add(pad(hz(m), 4, 0.8, 2) * 0.2, 1.5 + i * 0.3)
    return y.buf


@sound('Space')
def anomaly(v):
    seed(720)
    d = 3.0
    t = tt(d)
    y = sine(220 * (1 + 0.3 * np.sin(2 * np.pi * 0.7 * t)), d) * sine(331 * (1 + 0.2 * np.sin(2 * np.pi * 1.3 * t)), d)
    y += 0.3 * bp(noise(d), 1000, 3000) * (np.sin(2 * np.pi * 7 * t) > 0.6)
    y *= adsr(d, 0.6, 0.3, 0.8, 1.0)
    return reverb(fx(y, pb.Chorus(rate_hz=0.4, depth=0.8, mix=0.6)), 0.95, 0.45, 0.3, 1, 2.5)


@sound('Space')
def orbital_mine(v):
    seed(725)
    d = 1.4
    y = sweep(saw(55, d) + 0.5 * square(110.5, d), (300, 3000), 'low', 3) * adsr(d, 0.1, 0.3, 0.6, 0.6) * 0.5
    y += 0.3 * hp(noise(d), 4000) * (rng.random(n(d)) > 0.98) * adsr(d, 0.1, 0.2, 0.7, 0.5)
    return reverb(y, 0.6, 0.2, 0.5, 1, 1)


@sound('Space', 2)
def laser(v):
    seed(730 + v)
    d = 0.35
    y = square(glide(2400 - 600 * v, 300, d), d, 0.3) * decay(d, 6)
    y = lp(y, 5000) + 0.3 * sine(glide(1800, 200, d), d) * decay(d, 5)
    return reverb(y, 0.4, 0.15, 0.5, 1, 0.6)


@sound('Space')
def machine_hum(v):
    """Factory / power plant one-shot thunk (Industrial+)."""
    seed(735)
    d = 1.0
    y = thud(90, d) * 0.7 + metal_ring(600, d, 8) * 0.3 + 0.4 * hp(noise(d), 2000) * decay(d, 30)
    s = n(0.3)
    y[s:] += (bp(noise(d - 0.3), 2000, 6000) * decay(d - 0.3, 5) * 0.4)  # steam hiss
    return room(y, 0.4, 0.15)


@sound('Space')
def zap(v):
    """Electric Age: tesla coil crackle."""
    seed(740)
    d = 0.8
    pulses = (rng.random(n(d)) > 0.985).astype(float)
    y = bp(pulses, 1500, 9000) * 6 + 0.4 * saw(120, d) * (rng.random(n(d)) > 0.5)
    y *= adsr(d, 0.01, 0.2, 0.6, 0.3)
    y = fx(y, pb.Distortion(drive_db=10))[:, 0]
    return room(y, 0.3, 0.1)


# ================================================================ ambience loops

def loop_render(fn, dur, xfade=2.0, **kw):
    """Render dur+xfade seconds and cross-fade the extra tail into the start: seamless loop."""
    x = fn(dur + xfade, **kw)
    if x.ndim == 1:
        x = stereo(x, 0) * np.sqrt(2)
    L, X = n(dur), n(xfade)
    out = x[:L].copy()
    w = np.linspace(0, 1, X)[:, None]
    out[:X] = out[:X] * w + x[L:L + X] * (1 - w)
    return out


def amb_wind(d, base=400, gust=0.08, bright=1.0):
    t = tt(d)
    g = 0.5 + 0.5 * lp(rng.standard_normal(n(d)), gust, 1) * 60
    g = np.clip(g, 0.1, 1.5)
    fc = (base + base * 1.5 * g) * bright
    l = sweep(pink(d), fc, 'band', 1.2) * g
    r = sweep(pink(d), fc * 1.1, 'band', 1.2) * g[::-1]
    return np.stack([l, r], 1)


def birds(d, count=18, kind='forest'):
    m = Mix(d)
    for k in range(count):
        at = rng.uniform(0, d - 1)
        f0 = rng.uniform(2200, 4500)
        pan = rng.uniform(-0.8, 0.8)
        notes = rng.integers(2, 7)
        for j in range(notes):
            nd = rng.uniform(0.04, 0.12)
            f = glide(f0 * rng.uniform(0.8, 1.2), f0 * rng.uniform(0.7, 1.5), nd)
            c = sine(f * (1 + 0.05 * sine(rng.uniform(20, 60), nd)), nd) * np.sin(np.pi * np.linspace(0, 1, n(nd)))
            m.add(c * rng.uniform(0.05, 0.15), at + j * nd * 1.3, pan=pan)
    return m.buf[:n(d)]


def crickets(d, rate=16, f=4200, density=0.6):
    t = tt(d)
    out = np.zeros((n(d), 2))
    for k in range(5):
        fk = f * rng.uniform(0.9, 1.1)
        chirp = (np.sin(2 * np.pi * rate * rng.uniform(0.85, 1.15) * t + rng.random() * 6) > 0.3).astype(float)
        gate = (lp(rng.standard_normal(n(d)), 0.3, 1) * 40 > (1 - density))
        y = sine(fk, d) * lp(chirp * gate, 300) * 0.05
        out += stereo(y, rng.uniform(-0.8, 0.8))
    return out


def water(d, bright=1.0):
    y = bp(pink(d), 300, 3000 * bright) * 0.6
    m = Mix(d)
    for k in range(int(d * 25)):
        at = rng.uniform(0, d - 0.1)
        f0 = rng.uniform(500, 1600) * bright
        b = sine(glide(f0, f0 * 1.8, 0.05), 0.05) * decay(0.05, 4)
        m.add(b * rng.uniform(0.02, 0.07), at, pan=rng.uniform(-0.7, 0.7))
    return m.buf[:n(d)] + stereo(y, 0)


def _amb(name):
    def wrap(fn):
        SOUNDS['amb_' + name] = ('Ambience', fn, 1)
        return fn
    return wrap


@_amb('temperate')
def _(v):
    seed(800)
    return loop_render(lambda d: amb_wind(d, 350) * 0.3 + birds(d, 30) + crickets(d, density=0.2) * 0.3, 40)


@_amb('night')
def _(v):
    seed(801)
    return loop_render(lambda d: amb_wind(d, 250) * 0.15 + crickets(d, density=0.8) * 1.5, 40)


@_amb('river')
def _(v):
    seed(802)
    return loop_render(lambda d: water(d) + birds(d, 8) * 0.6, 30)


@_amb('desert')
def _(v):
    seed(803)
    return loop_render(lambda d: amb_wind(d, 600, 0.15, 1.3) * 0.7 + stereo(hp(pink(d), 4000) * 0.03, 0), 40)


@_amb('tundra')
def _(v):
    seed(804)
    return loop_render(lambda d: amb_wind(d, 700, 0.2, 1.6) * 0.8 + amb_wind(d, 180) * 0.4, 40)


def swamp_bed(d):
    m = Mix(d)
    for k in range(int(d * 1.2)):
        at = rng.uniform(0, d - 1)
        f = rng.uniform(140, 320)
        croak_n = rng.integers(2, 5)
        for j in range(croak_n):
            cd = 0.12
            c = bp(saw(f, cd) * (np.sin(2 * np.pi * 40 * tt(cd)) > 0), f, f * 6) * np.sin(np.pi * np.linspace(0, 1, n(cd)))
            m.add(c * 0.12, at + j * 0.18, pan=rng.uniform(-0.8, 0.8))
    return m.buf[:n(d)] + crickets(d, 30, 5200, 0.7) + stereo(lp(pink(d), 500) * 0.1, 0)


@_amb('swamp')
def _(v):
    seed(805)
    return loop_render(swamp_bed, 40)


@_amb('volcanic')
def _(v):
    seed(806)

    def f(d):
        rumble = stereo(lp(brown(d), 120) * 1.0, 0)
        hiss = stereo(hp(pink(d), 2500) * 0.05, 0.3)
        m = Mix(d)
        for k in range(int(d * 4)):
            at = rng.uniform(0, d - 0.1)
            m.add(hp(noise(0.02), 2000) * decay(0.02, 5) * rng.uniform(0.05, 0.2), at, pan=rng.uniform(-1, 1))
        for k in range(3):
            m.add(lp(brown(2.0), 200) * decay(2.0, 3, 0.3) * 0.6, rng.uniform(0, d - 2))
        return rumble + hiss + m.buf[:n(d)]
    return loop_render(f, 40)


@_amb('crystal')
def _(v):
    seed(807)

    def f(d):
        m = Mix(d)
        scale = [0, 2, 4, 7, 9]
        for k in range(int(d * 1.5)):
            mm = 84 + scale[rng.integers(5)] + 12 * rng.integers(0, 2)
            m.add(bell(hz(mm), 3) * rng.uniform(0.03, 0.08), rng.uniform(0, d - 3), pan=rng.uniform(-0.9, 0.9))
        hum = sum(sine(hz(x) * (1 + 0.003 * np.sin(2 * np.pi * 0.1 * tt(d) + x)), d) for x in (48, 55, 64)) * 0.05
        return reverb(m.buf[:n(d)] + stereo(hum, 0), 0.9, 0.4, 0.3, 1, 0)[:n(d)] + amb_wind(d, 900) * 0.08
    return loop_render(f, 40)


@_amb('fungal')
def _(v):
    seed(808)

    def f(d):
        m = Mix(d)
        for k in range(int(d * 2)):
            fr = rng.uniform(900, 2200)
            m.add(sine(glide(fr, fr * 2, 0.05), 0.05) * decay(0.05, 4) * rng.uniform(0.05, 0.15), rng.uniform(0, d - 0.1), pan=rng.uniform(-0.9, 0.9))
        for k in range(int(d * 0.5)):
            dd = 1.5
            m.add(lp(noise(dd), 600) * adsr(dd, 0.6, 0.2, 0.6, 0.6) * 0.15, rng.uniform(0, d - dd))  # spore puff
        drone = sine(hz(36), d) * 0.08 + sine(hz(43) * (1 + 0.004 * np.sin(2 * np.pi * 0.2 * tt(d))), d) * 0.05
        return reverb(m.buf[:n(d)] + stereo(drone, 0), 0.85, 0.35, 0.4, 1, 0)[:n(d)]
    return loop_render(f, 40)


@_amb('jungle')
def _(v):
    seed(809)
    return loop_render(lambda d: birds(d, 60) * 1.2 + crickets(d, 22, 5500, 0.9) + swamp_bed(d) * 0.3 + amb_wind(d, 300) * 0.15, 40)


@_amb('barren')
def _(v):
    seed(810)
    return loop_render(lambda d: amb_wind(d, 220, 0.05) * 0.6 + amb_wind(d, 1200, 0.1) * 0.1, 40)


@_amb('rain')
def _(v):
    seed(811)

    def f(d):
        bed = np.stack([bp(pink(d), 800, 9000), bp(pink(d), 800, 9000)], 1) * 0.5
        m = Mix(d)
        for k in range(int(d * 40)):
            m.add(hp(noise(0.01), 3000) * decay(0.01, 4) * rng.uniform(0.02, 0.1), rng.uniform(0, d - 0.02), pan=rng.uniform(-1, 1))
        return bed + m.buf[:n(d)]
    return loop_render(f, 30)


@_amb('town')
def _(v):
    """Distant settlement: hammering, chopping, a dog-free murmur of work."""
    seed(812)

    def f(d):
        m = Mix(d)
        for k in range(int(d * 0.8)):
            at = rng.uniform(0, d - 2)
            kind = rng.integers(3)
            src = (hammer, chop, pick)[kind]
            reps = rng.integers(2, 5)
            for j in range(reps):
                m.add(src(rng.integers(1, 4))[:, 0] * rng.uniform(0.06, 0.15), at + j * rng.uniform(0.5, 0.8), pan=rng.uniform(-0.8, 0.8))
        bed = amb_wind(d, 300) * 0.2 + birds(d, 12) * 0.5
        return lp(m.buf[:n(d)], 3500) + bed
    return loop_render(f, 40)


@_amb('industrial')
def _(v):
    seed(813)

    def f(d):
        t = tt(d)
        beat = 60 / 96
        m = Mix(d)
        for i in range(int(d / beat)):
            m.add(thud(70, 0.3) * 0.25, i * beat, pan=-0.3)
            if i % 2:
                m.add(hp(noise(0.15), 2500) * decay(0.15, 6) * 0.08, i * beat + beat / 2, pan=0.4)
            if i % 8 == 0:
                m.add(bp(noise(1.2), 2000, 7000) * adsr(1.2, 0.05, 0.3, 0.5, 0.6) * 0.12, i * beat + 0.2, pan=0.6)
        hum = lp(saw(50, d) * 0.15 + saw(100.3, d) * 0.05, 400)
        return lp(m.buf[:n(d)], 4000) + stereo(hum, 0) + amb_wind(d, 200) * 0.1
    return loop_render(f, 30, 0.2)


@_amb('space')
def _(v):
    seed(814)

    def f(d):
        t = tt(d)
        drone = sum(g * sine(hz(m) * (1 + 0.002 * np.sin(2 * np.pi * 0.05 * (k + 1) * t)), d) for k, (m, g) in
                    enumerate(((31, 0.2), (38, 0.15), (43, 0.1), (50, 0.05))))
        air = sweep(pink(d), 400 + 300 * np.sin(2 * np.pi * t / d * 2) ** 2, 'band', 2) * 0.15
        m = Mix(d)
        for k in range(int(d * 0.3)):
            m.add(scan_ping(1)[:, 0] * 0.04, rng.uniform(0, d - 3), pan=rng.uniform(-0.8, 0.8))
        return reverb(stereo(drone + air, 0) + m.buf[:n(d)], 0.95, 0.5, 0.3, 1, 0)[:n(d)]
    return loop_render(f, 40)


@_amb('dungeon')
def _(v):
    seed(815)

    def f(d):
        drone = lp(brown(d), 150) * 0.4
        m = Mix(d)
        for k in range(int(d * 0.6)):
            fr = rng.uniform(1200, 2400)
            m.add(sine(glide(fr, fr * 1.7, 0.04), 0.04) * decay(0.04, 4) * 0.12, rng.uniform(0, d - 0.1), pan=rng.uniform(-0.9, 0.9))
        for k in range(2):
            m.add(lp(noise(2), 300) * adsr(2, 0.8, 0.3, 0.5, 0.8) * 0.3, rng.uniform(0, d - 2))
        return reverb(stereo(drone, 0) + m.buf[:n(d)], 0.95, 0.5, 0.3, 1, 0)[:n(d)]
    return loop_render(f, 40)


# ================================================================ main

# Only these are used in the game; everything else now comes from recordings (import_sounds.py).
KEEP = {'research_complete', 'goal_complete', 'level_up', 'enchant', 'discover', 'shrine_bless', 'lore_stone',
        'drop_common', 'drop_uncommon', 'drop_rare', 'drop_epic', 'drop_legendary',
        'amb_night', 'amb_desert', 'amb_tundra', 'amb_swamp', 'amb_volcanic', 'amb_crystal', 'amb_fungal',
        'amb_barren', 'amb_space', 'amb_dungeon'}


def main(filters):
    done = 0
    for name, (group, fn, v) in SOUNDS.items():
        if name not in KEEP and '--all' not in filters:
            continue
        filters = [f for f in filters if f != '--all']
        if filters and not any(f in name for f in filters):
            continue
        x = fn(v)
        if group == 'Ambience':
            path = save(x, f'Ambience/{name}', peak=0.7, ogg=True, trim=False, loud=-24)
        else:
            # UI blips sit quieter than the world; big moments (fanfares, booms) a little louder
            target = {'UI': -18, 'Boss': -11}.get(group, -14)
            if name in ('hover', 'click', 'tab'):
                target = -22
            path = save(x, f'SFX/{group}/{name}', loud=target)
        done += 1
        print(f'{name:22s} {len(x) / SR:5.2f}s  {path.split("Audio")[-1]}')
    print(done, 'sounds')
    sys.argv = sys.argv[:1] + ['.']          # import_sounds reads its own args; only its index writer is needed
    from import_sounds import write_index
    print(write_index(), 'effects in sfx_index.txt')


if __name__ == '__main__':
    main(sys.argv[1:])
