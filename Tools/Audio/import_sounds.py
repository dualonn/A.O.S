"""Build the game's sound set from downloaded CC0 recordings.

Usage: python import_sounds.py <downloads_dir> [name filters...]
Each game sound maps to one or more recordings (each becomes a variant: name_1, name_2...).
Recordings are found by glob under <downloads_dir>. Multi-take recordings are cut into takes.
Output: Assets/Resources/Audio/{SFX/<Group>,Music,Ambience}. Sources are listed in Assets/Resources/Audio/CREDITS.txt.
"""
import glob, os, sys, re
import numpy as np
import soundfile as sf
import scipy.signal as sig
import pedalboard as pb
from synth import ROOT, SR, hp, lp, loudness, rms_db, fade, n

DL = sys.argv[1] if len(sys.argv) > 1 else '.'
SFX_ONLY = '--sfx' in sys.argv     # re-render effects only (skip music and ambience)
FILTERS = [a for a in sys.argv[2:] if a != '--sfx']


# ---------------------------------------------------------------- source specs

class S:
    """One recording (or one take of it), with optional tweaks."""

    def __init__(self, pattern, take=None, start=0.0, dur=None, gain=1.0, lowpass=None, highpass=None, room=0.0, rate=1.0):
        self.pattern, self.take, self.start, self.dur = pattern, take, start, dur
        self.gain, self.lowpass, self.highpass, self.room, self.rate = gain, lowpass, highpass, room, rate


def V(pattern, count=99, **kw):
    """Every file matching pattern (up to count), one variant each."""
    return [('glob', pattern, count, kw)]


def T(pattern, takes, **kw):
    """Takes cut out of a single multi-take recording."""
    return [S(pattern, take=t, **kw) for t in takes]


def L(*specs):
    """Layer several sources into one variant (each spec: S)."""
    return [('layer', specs)]


def expand(items):
    out = []
    for it in items:
        if isinstance(it, S):
            out.append([it])
        elif it[0] == 'glob':
            _, pat, count, kw = it
            files = find(pat, many=True)[:count]
            out += [[S(f, **kw)] for f in files]
        elif it[0] == 'layer':
            out.append(list(it[1]))
    return out


_cache = {}


def find(pattern, many=False):
    if os.path.isabs(pattern) and os.path.exists(pattern):
        return [pattern] if many else pattern
    hits = sorted(f for f in glob.glob(os.path.join(DL, '**', pattern), recursive=True)
                  if not re.search(r'(_0\.(ogg|mp3)$|[/\\]\._)', f))
    if not hits:
        raise FileNotFoundError(pattern)
    return hits if many else hits[0]


def load(path):
    if path not in _cache:
        x, sr = sf.read(path, always_2d=True)
        if sr != SR:
            g = np.gcd(sr, SR)
            x = sig.resample_poly(x, SR // g, sr // g, axis=0)
        _cache[path] = x
    return _cache[path]


def takes_of(x, min_gap=0.12, thresh_db=-32):
    """Split a recording into separate takes by silence."""
    m = np.abs(x).max(1)
    w = n(0.01)
    env = np.sqrt(np.convolve(m ** 2, np.ones(w) / w, 'same'))
    on = env > env.max() * 10 ** (thresh_db / 20)
    segs, i, N = [], 0, len(on)
    idx = np.flatnonzero(on)
    if len(idx) == 0:
        return [(0, N)]
    start = prev = idx[0]
    for k in idx[1:]:
        if k - prev > n(min_gap):
            segs.append((start, prev))
            start = k
        prev = k
    segs.append((start, prev))
    segs = [(max(0, a - n(0.01)), min(N, b + n(0.08))) for a, b in segs if b - a > n(0.03)]
    return segs


def render_source(s):
    x = load(find(s.pattern)).copy()
    if s.take is not None:
        segs = takes_of(x)
        a, b = segs[s.take % len(segs)]
        x = x[a:b]
    if s.start or s.dur:
        cut = s.dur is not None and n(s.start + s.dur) < len(x)
        x = x[n(s.start): n(s.start + s.dur) if s.dur else None].copy()
        if cut:  # cut mid-sound: fade the tail out gently
            k = min(len(x), n(min(0.5, s.dur * 0.3)))
            x[-k:] *= np.linspace(1, 0, k)[:, None] ** 2
    if s.rate != 1.0:
        x = sig.resample_poly(x, 100, int(round(100 * s.rate)), axis=0)
    if s.highpass:
        x = hp(x, s.highpass)
    if s.lowpass:
        x = lp(x, s.lowpass)
    if s.room:
        st = x if x.shape[1] == 2 else np.repeat(x, 2, 1)
        st = np.concatenate([st, np.zeros((n(1.0), 2))])
        x = pb.Pedalboard([pb.Reverb(room_size=0.6, wet_level=s.room, dry_level=1.0, damping=0.5)])(st.T.astype(np.float32), SR).T
    return x * s.gain


def render_variant(specs):
    parts = [render_source(s) for s in specs]
    L_ = max(len(p) for p in parts)
    ch = max(p.shape[1] for p in parts)
    out = np.zeros((L_, ch))
    for p in parts:
        if p.shape[1] < ch:
            p = np.repeat(p, ch, 1)
        out[:len(p)] += p
    return out


def tidy(x, mono, thresh_db=-50):
    x = hp(x, 25, 1)
    if mono and x.shape[1] > 1:
        x = x.mean(1, keepdims=True)
    m = np.abs(x).max(1)
    on = np.flatnonzero(m > m.max() * 10 ** (thresh_db / 20))
    if len(on):
        x = x[max(0, on[0] - n(0.003)): on[-1] + n(0.02)]
    for c in range(x.shape[1]):
        x[:, c] = fade(x[:, c], 0.002, 0.03)
    return x


def write(x, rel, ogg=False, quality=0.45):
    path = os.path.join(ROOT, rel + ('.ogg' if ogg else '.wav'))
    os.makedirs(os.path.dirname(path), exist_ok=True)
    x = np.clip(x, -1, 1).astype(np.float32)
    if ogg:
        with sf.SoundFile(path, 'w', SR, x.shape[1], format='OGG', subtype='VORBIS', compression_level=quality) as f:
            for i in range(0, len(x), 8192):
                f.write(x[i:i + 8192])
    else:
        sf.write(path, x, SR, subtype='PCM_16')
    return path


def level(x, target, max_squash=9.0):
    """Bring a sound to a loudness target. Sharp sounds (shots, bows, hits) would clip first, so their
    peaks are limited (up to max_squash dB) instead of the whole sound being turned down."""
    x = x * 10 ** ((target - loudness(x)) / 20)
    pk = np.abs(x).max()
    if pk > 0.95:
        over = 20 * np.log10(pk / 0.95)
        squash = min(over, max_squash)
        if squash > 0.5:
            x = x * 10 ** (-(over - squash) / 20)           # what the limiter can't take, turn down
            st = x.T.astype(np.float32)
            x = pb.Pedalboard([pb.Limiter(threshold_db=-squash - 0.5, release_ms=60)])(st, SR).T.astype(np.float64)
            x = x * 10 ** ((target - loudness(x)) / 20)
        x *= min(1.0, 0.95 / np.abs(x).max())
    return x


def write_index():
    """Resources/Audio/sfx_index.txt: every effect clip, so the game can load them a few per frame."""
    paths = sorted(os.path.relpath(p, ROOT).replace(os.sep, '/')[:-4]
                   for p in glob.glob(os.path.join(ROOT, 'SFX', '**', '*.wav'), recursive=True))
    with open(os.path.join(ROOT, 'sfx_index.txt'), 'w', encoding='utf-8', newline='\n') as f:
        f.write('\n'.join('Audio/' + p for p in paths) + '\n')
    return len(paths)


def loop_xfade(x, xf=2.0):
    X = n(xf)
    if len(x) < 3 * X:
        return x
    body = x[:len(x) - X].copy()
    w = np.linspace(0, 1, X)[:, None]
    body[:X] = body[:X] * w + x[len(x) - X:] * (1 - w)
    return body


# ---------------------------------------------------------------- the sound list
# group -> {name: [sources]}; loudness targets per group below.

FOLEY_PATH = 'medieval-sound-effects-weapon-textures/x/'

SFX = {
    'Combat': {
        'sword_swing': T(FOLEY_PATH + 'Sabre Swing.wav', [0, 3, 6, 9, 12]) + T(FOLEY_PATH + 'Katana Swing.wav', [0, 2]),
        'axe_swing': T(FOLEY_PATH + 'Axe Swing.wav', [0, 2, 4]),
        'spear_swing': [S(FOLEY_PATH + 'Spear Swing.wav', start=t, dur=0.7) for t in (0.0, 1.5, 3.0)],
        'dagger_swing': T(FOLEY_PATH + 'Dagger Swing.wav', [0, 2, 4]),
        'sword_hit': V('sword-clash-0*.wav'),
        'blade_hit': V('oga-80-CC0-RPG-SFX_0/**/blade_0*.ogg'),
        'blunt_hit': V('impactPunch_heavy_00*.ogg', 3),
        'flesh_hit': V('impactPunch_medium_00*.ogg', 3),
        'shield_block': V('impactPlate_medium_00*.ogg', 3),
        'bow_shot': T(FOLEY_PATH + 'English Longbow Shoot.wav', [0, 2, 4]) + T(FOLEY_PATH + 'Scythian Recurve Shoot.wav', [0, 3]),
        'arrow_fly': T(FOLEY_PATH + 'Scythian Recurve Arrow Passby.wav', [0, 1, 2]),
        'arrow_hit': V('impactWood_light_00*.ogg', 3) + V('impactSoft_medium_00*.ogg', 2),
        'crossbow_shot': T(FOLEY_PATH + 'Crossbow Shoot.wav', [0, 2, 4]),
        'throw': V('oga-rpg_sound_pack/**/swing[23].wav'),
        'staff_cast': V('oga-80-CC0-RPG-SFX_0/**/spell_0*.ogg') + V('oga-rpg_sound_pack/**/magic1.wav'),
        'magic_hit': V('oga-80-CC0-RPG-SFX_0/**/spell_fire_0*.ogg', 4),
        'musket_shot': [S(f, dur=2.5, lowpass=6000, room=0.15) for f in
                        ('Arisaka/E_18P.wav', 'Arisaka/E_25P.wav', 'Mosin Nagant/M_21P.wav', '1917/B_16P.wav')],
        'rifle_shot': [S(f, dur=2.0) for f in ('Model 1894/L_17P.wav', 'Model 1894/L_23P.wav', 'SKS/U_14P.wav', 'SKS/U_19P.wav')],
        'cannon_shot': V('cannon-fire/cannon_fire.ogg') + V('92774/x/cannon_0[12].ogg'),
        'cannon_impact': V('cannon-hit-cannon/cannon_hit_cannon.ogg') + V('92774/x/bang_0[13].ogg'),
        'explosion': V('oga-100-CC0-SFX_0/**/explosion.ogg') + V('lowFrequency_explosion_00*.ogg') + V('residue-sfx/x/**/boom.ogg'),
        'death': V('oga-80-CC0-creature-SFX_0/**/hurt_0*.ogg'),
        'monster_death': V('oga-80-CC0-RPG-SFX_0/**/creature_die_01.ogg') + V('oga-80-CC0-creature-SFX_0/**/monster_0[1-3].ogg'),
        'monster_growl': V('oga-rpg_sound_pack/**/mnstr[1-6].wav'),
        'wolf_growl': V('oga-rpg_sound_pack/**/wolfman.wav') + V('oga-80-CC0-creature-SFX_0/**/howl.ogg'),
        'spider_hiss': V('oga-80-CC0-creature-SFX_0/**/bug_0[1-3].ogg'),
        'body_fall': V('impactSoft_heavy_00*.ogg', 3),
        'armor_clank': V('oga-rpg_sound_pack/**/chainmail[12].wav') + V('oga-rpg_sound_pack/**/armor-light.wav'),
        'sword_draw': V('oga-rpg_sound_pack/**/sword-unsheathe[1-3].wav') + V('oga-rpg_sound_pack/**/sword-unsheathe.wav'),
    },
    'Boss': {
        'boss_slam': L(S('lowFrequency_explosion_000.ogg'), S('impactWood_heavy_000.ogg', gain=0.8)),
        'boss_warcry': V('oga-rpg_sound_pack/**/giant[1-3].wav') + V('oga-80-CC0-RPG-SFX_0/**/creature_roar_0*.ogg'),
        'boss_summon': V('their-coming-generic-horn-sound/theircoming3.ogg'),
        'boss_death': V('oga-rpg_sound_pack/**/ogre[1-2].wav'),
    },
    'Building': {
        'place': V('impactWood_heavy_00*.ogg', 3),
        'hammer': V('impactPlank_medium_00*.ogg'),
        'build_complete': L(S('impactWood_heavy_003.ogg'), S('confirmation_002.ogg', gain=0.7)),
        'saw_wood': [S('handsaw-sawing-wood-01.wav', start=0.0, dur=2.2), S('handsaw-sawing-wood-01.wav', start=2.4, dur=2.2)],
        'demolish': V('oga-80-CC0-RPG-SFX_0/**/stones_0*.ogg'),
        'gate_open': V('rpg-audio/**/doorOpen_*.ogg'),
        'gate_close': V('rpg-audio/**/doorClose_*.ogg'),
        'creak': V('rpg-audio/**/creak*.ogg'),
        'forge': V('metal-hammer-hit-0*.wav'),
        'wall_place': V('oga-80-CC0-RPG-SFX_0/**/item_stone_0*.ogg', 3),
        'machine': V('oga-100-CC0-SFX_0/**/machine_0*.ogg'),
    },
    'Gather': {
        'chop': V('rpg-audio/**/chop.ogg') + V('impactWood_medium_00*.ogg', 4),
        'pick': V('impactMining_00*.ogg'),
        'forage': V('footstep_grass_00*.ogg', 3),
        'hoe': V('impactSoft_medium_00[2-4].ogg'),
        'splash': V('oga-100-CC0-SFX_0/**/splash_0*.ogg'),
        'coins': V('rpg-audio/**/handleCoins*.ogg') + V('oga-80-CC0-RPG-SFX_0/**/item_coins_0*.ogg', 2),
        'twigs': V('wood-twigs-break-0*.wav'),
        'footstep': V('footstep_grass_00*.ogg'),
    },
    'UI': {
        'click': V('interface-sounds/**/click_00*.ogg', 3),
        'hover': V('ui-audio/**/rollover[1-3].ogg'),
        'panel_open': V('interface-sounds/**/open_00[12].ogg'),
        'panel_close': V('interface-sounds/**/close_00[12].ogg'),
        'book_open': V('rpg-audio/**/bookOpen.ogg'),
        'book_close': V('rpg-audio/**/bookClose.ogg'),
        'page_flip': V('rpg-audio/**/bookFlip*.ogg'),
        'tab': V('interface-sounds/**/switch_00[12].ogg'),
        'error': V('interface-sounds/**/error_00[1-3].ogg'),
        'confirm': V('interface-sounds/**/confirmation_00[12].ogg'),
        'notify': V('interface-sounds/**/bong_001.ogg'),
        'age_up': V('Production_Intro_Theme.wav'),
        'raid_warning': [S('their-coming-generic-horn-sound/theircoming.ogg', dur=6.6)],   # first two blasts only
        'victory': V('medieval-victory-theme/victory.wav'),
        'defeat': V('medieval-defeat-theme/defeat.wav'),
    },
    'Items': {
        'pickup': V('oga-80-CC0-RPG-SFX_0/**/item_misc_0[1-3].ogg'),
        'equip': V('oga-rpg_sound_pack/**/cloth-heavy.wav') + V('oga-rpg_sound_pack/**/metal-small[12].wav') + V('rpg-audio/**/beltHandle*.ogg'),
        'gem': V('oga-80-CC0-RPG-SFX_0/**/item_gem_0*.ogg'),
        'chest_open': V('oga-100-CC0-SFX_0/**/wooded_box_open.ogg') + V('oga-100-CC0-SFX_0/**/key_open_01.ogg'),
        'potion': V('bottle-glass-uncork-01.wav') + V('vial-glass-uncork-01.wav'),
        'hero_recruit': V('oga-rpg_sound_pack/**/sword-unsheathe[45].wav'),
        'book': V('oga-80-CC0-RPG-SFX_0/**/book_0*.ogg', 2),
    },
    'Sites': {
        'dungeon_enter': V('oga-100-CC0-SFX_0/**/door_0[12].ogg'),
        'camp_destroyed': L(S('oga-80-CC0-RPG-SFX_0/**/stones_03.ogg'), S('impactWood_heavy_001.ogg', gain=0.8), S('wood-twigs-break-02.wav', gain=0.6)),
        'chain': V('oga-80-CC0-RPG-SFX_0/**/chain_0*.ogg'),
    },
    'Space': {
        'rocket_launch': V('thrusterFire_00[0-2].ogg', dur=3.5),
        'probe_launch': V('spaceEngineSmall_00[0-1].ogg', dur=2.5),
        'warp': V('digital-audio/**/phaseJump[1-3].ogg'),
        'scan_ping': V('computerNoise_00*.ogg', 2, dur=1.6),
        'anomaly': V('oga-80-CC0-creature-SFX_0/**/alien_0[1-3].ogg'),
        'orbital_mine': V('engineCircular_00[0-1].ogg', dur=2.5),
        'laser': V('laserSmall_00[0-2].ogg') + V('laserLarge_00[0-1].ogg'),
        'zap': V('digital-audio/**/zap[12].ogg'),
        'force_field': V('forceField_00[0-1].ogg'),
        'scifi_door': V('sci-fi-sounds/**/doorOpen_00[0-1].ogg'),
    },
    # seasons & weather, random events, diplomacy, wonders
    'World': {
        'thunder': [S('rain-thunder.ogg', start=21.3, dur=10), S('Dark_Rainy_Night(ambience).ogg', start=33.6, dur=5),
                    S('Dark_Rainy_Night(ambience).ogg', start=51.8, dur=4.5), S('Dark_Rainy_Night(ambience).ogg', start=89.5, dur=5)],
        'event_card': L(S('rpg-audio/x/Audio/bookFlip1.ogg'), S('oga-100-CC0-SFX_0/**/bell_01.ogg', gain=0.6)),
        'wonder_complete': L(S('oga-100-CC0-SFX_0/**/gong_01.ogg'), S('Heavenly Loop.flac', dur=7, gain=0.8)),
        'diplomacy': V('rpg-audio/**/bookPlace*.ogg'),
        'peace': V('oga-100-CC0-SFX_0/**/bell_02.ogg'),
        'war_declared': V('their-coming-generic-horn-sound/theircoming3.ogg'),
        'clock_tick': V('ticking clock - tick*.wav'),
    },
}

# loudest-0.3s loudness target per group (dB)
LOUD = {'Combat': -14, 'Boss': -11, 'Building': -15, 'Gather': -17, 'UI': -19, 'Items': -17, 'Sites': -15, 'Space': -15, 'World': -15}
QUIET_UI = {'click': -24, 'hover': -27, 'tab': -24, 'panel_open': -22, 'panel_close': -22, 'page_flip': -22}
LONG = {'wonder_complete': -15, 'thunder': -13, 'age_up': -16, 'victory': -18, 'defeat': -18, 'raid_warning': -22}
# per-sound loudness overrides (dB)
TARGET = {'boss_summon': -17, 'war_declared': -19, 'event_card': -20, 'clock_tick': -22}

# Music: era playlists (the game picks per age), played whole and cross-faded; stereo OGG.
MUSIC = {
    'title': ['FantasyOrchestralTheme_1.mp3'],
    'ancient': ['medieval-exploration/Exploration.wav', 'harvestseason.wav'],
    'medieval': ['Market_Day.wav', 'Minstrel_Dance.mp3', 'The_Old_Tower_Inn.mp3', 'The_Bards_Tale.mp3', 'Rejoicing.mp3'],
    'renaissance': ['HarpsiChordFlurry_5.ogg', 'C64 - Bach - wtk2-prelude2.ogg'],
    'industrial': ['allin.flac', 'ruskerdax_-_menacing_otherworld.mp3'],
    'modern': ['80s_song_mastered.mp3', 'nighum.mp3'],
    'information': ['exploration-theme/exploration_theme.ogg', 'sansho.mp3'],
    'space': ['cosmic_navigation_loop.flac', 'OutThere.ogg', 'boundless2.ogg', 'a_start_to_spacev3.24.ogg', 'space_echo.ogg', 'outer_space_1.mp3', 'starfield_romance1.wav'],
    'battle': ['battle_1.wav', 'call_to_war.wav'],
}

# Recorded ambience loops (the rest are synthesized by sfx.py)
AMBIENCE = {
    'amb_temperate': S('Forest_Ambience.mp3'),
    'amb_meadow': S('Birds and Wind - Ambient_1.ogg'),
    'amb_river': S('VistulaShort.mp3', start=10, dur=70),
    'amb_town': S('sfx100v2_loop_construction_site.ogg', gain=0.6),
    'amb_industrial': S('Factory.ogg'),
    'amb_jungle': S('Forest Birds/Raw/Forest 1.wav'),
    'amb_rain': S('rain-loopable/x/3.ogg'),
    'amb_storm': S('Dark_Rainy_Night(ambience).ogg', start=100, dur=95),
}


# ---------------------------------------------------------------- run

def want(name):
    return not FILTERS or any(f in name for f in FILTERS)


def main():
    made, missing = 0, []
    for group, sounds in SFX.items():
        for name, items in sounds.items():
            if not want(name):
                continue
            try:
                variants = expand(items)
            except FileNotFoundError as e:
                missing.append(f'{name}: {e}')
                continue
            # clear old variants of this sound
            for old in glob.glob(os.path.join(ROOT, 'SFX', group, name + '*.wav')):
                if re.fullmatch(re.escape(name) + r'(_\d+)?\.wav', os.path.basename(old)):
                    os.remove(old)
            for i, specs in enumerate(variants, 1):
                try:
                    x = render_variant(specs)
                except FileNotFoundError as e:
                    missing.append(f'{name}: {e}')
                    continue
                long_ = name in LONG
                x = tidy(x, mono=not long_)
                target = TARGET.get(name) or LONG.get(name) or QUIET_UI.get(name) or LOUD[group]
                x = level(x, target)
                rel = f'SFX/{group}/{name}' + (f'_{i}' if len(variants) > 1 else '')
                write(x, rel)
                made += 1
                print(f'{rel:32s} {len(x) / SR:5.2f}s  <- {" + ".join(os.path.basename(s.pattern) + ("#%d" % s.take if s.take is not None else "") for s in specs)}')

    for era, files in ({} if SFX_ONLY else MUSIC).items():
        for i, f in enumerate(files, 1):
            name = f'{era}_{i}'
            if not want(name):
                continue
            try:
                x = load(find(f))
            except FileNotFoundError as e:
                missing.append(f'{name}: {e}')
                continue
            if x.shape[1] == 1:
                x = np.repeat(x, 2, 1)
            x = x * 10 ** ((-18 - rms_db(x.mean(1))) / 20)
            pk = np.abs(x).max()
            if pk > 0.89:  # never clip: loud masters just end up a little quieter
                x *= 0.89 / pk
            if rms_db(x.mean(1)) < -20.5:  # peaky track: squeeze the peaks so it sits with the others
                x = pb.Pedalboard([pb.Compressor(threshold_db=-26, ratio=3.0, attack_ms=5, release_ms=200)])(
                    x.T.astype(np.float32), SR).T.astype(np.float64)
                x = x * 10 ** ((-18 - rms_db(x.mean(1))) / 20)
                x *= min(1.0, 0.89 / np.abs(x).max())
            write(x, f'Music/{name}', ogg=True, quality=0.62)
            made += 1
            print(f'Music/{name:24s} {len(x) / SR:6.1f}s  <- {os.path.basename(f)}')

    for name, s in ({} if SFX_ONLY else AMBIENCE).items():
        if not want(name):
            continue
        try:
            x = render_source(s)
        except FileNotFoundError as e:
            missing.append(f'{name}: {e}')
            continue
        if x.shape[1] == 1:
            x = np.repeat(x, 2, 1)
        x = loop_xfade(hp(x, 30, 1))
        x = x * 10 ** ((-24 - rms_db(x.mean(1))) / 20)
        x = np.clip(x, -0.9, 0.9)
        write(x, f'Ambience/{name}', ogg=True, quality=0.4)
        made += 1
        print(f'Ambience/{name:21s} {len(x) / SR:6.1f}s  <- {os.path.basename(s.pattern)}')

    print(made, 'files;', write_index(), 'effects in sfx_index.txt')
    if missing:
        print('MISSING:\n  ' + '\n  '.join(missing))


if __name__ == '__main__':
    main()
