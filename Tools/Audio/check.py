"""Measure every file in Assets/Audio: length, peak, loudness, DC offset, loop seam jump.
Flags anything that looks broken (silent, clipped, too quiet, clicky loop)."""
import glob, os, sys
import numpy as np
import soundfile as sf
from synth import ROOT

bad = 0
rows = []
for p in sorted(glob.glob(os.path.join(ROOT, '**', '*.*'), recursive=True)):
    if p.endswith('.meta') or not p.endswith(('.wav', '.ogg')):
        continue
    if len(sys.argv) > 1 and not any(f in p for f in sys.argv[1:]):
        continue
    x, sr = sf.read(p, always_2d=True)
    m = x.mean(1)
    peak = np.abs(x).max()
    rms = 20 * np.log10(np.sqrt(np.mean(m ** 2)) + 1e-12)
    dc = abs(m.mean())
    seam = np.abs(x[0] - x[-1]).max()
    step = np.percentile(np.abs(np.diff(m)), 99.9)
    flags = []
    if peak < 0.05: flags.append('SILENT')
    if peak > 0.99: flags.append('CLIP')
    if rms < -40: flags.append('QUIET')
    if dc > 0.02: flags.append('DC')
    loop = 'Ambience' in p or 'Music' in p
    if loop and seam > max(0.05, step * 2): flags.append(f'SEAM{seam:.2f}')
    bad += bool(flags)
    rel = p[len(ROOT) + 1:]
    print(f'{rel:45s} {len(x)/sr:6.2f}s pk {peak:.2f} rms {rms:6.1f}dB {" ".join(flags)}')
print('flagged:', bad)
