using System.Collections.Generic;
using UnityEngine;
using P = PixelPalette;

/// <summary>
/// Wild creatures in pixel art, facing right, 12 frames like every unit: idle, walk x4, attack x4
/// (a bite, a charge, or for grazers a head-down graze), death x3. Built from a few rigs (four
/// legs, two legs, moth, whale, crab, puffball) with each species' proportions, coat and extras.
/// Tamed beasts wear a blue saddle-cloth and collar. Frames are cropped to the creature, with the
/// pivot between its feet (or under a flyer's shadow).
/// </summary>
public static class WildArt
{
    public enum Rig { Quad, Biped, Moth, Whale, Crab, Puff }
    public enum Horns { None, Antlers, Tusks, BoarTusks, Curved, Straight, Crystals, CrystalHorn }

    public class Look
    {
        public Rig rig = Rig.Quad;
        public Color32[] fur, belly, accent, horn;
        public float S = 1f;
        public float len = 15f, r = 4f, legs = 8f, legW = 2f, neck = 4f, neckAngle = 45f, head = 3f, snout = 3.5f, ear = 2f, tail = 5f;
        public int tailKind;               // 0 short, 1 bushy, 2 thin, 3 tufted, 4 heavy (reptile), 5 stinger
        public Horns horns;
        public float hornSize = 1f;
        public bool hump, shaggy, spots, stripes, glowEyes, sixLegs, mane, shell, wings, beak, talons, trunk, plates, crystals, cat, longJaw, antennae, flames, fangs, roundEars;
        public float fly;                  // flies this many pixels up (0 = walks)
        public Color32 eye = new Color32(30, 20, 20, 255);
    }

    private static Color32[] R(params string[] hex)
    {
        var r = new Color32[hex.Length];
        for (int i = 0; i < hex.Length; i++) r[i] = P.Hex(hex[i]);
        return r;
    }

    // ---------- The species ----------

    public static Look LookOf(string species)
    {
        var cream = R("8a7a62", "c4b494", "e6d8b8", "faf0dc");
        switch (species)
        {
            case "Deer": return new Look { fur = R("3a2216", "6e4128", "a0663c", "c99a66"), belly = cream, horn = P.Bone, len = 15, r = 4f, legs = 11, legW = 1.6f, neck = 7, neckAngle = 62, head = 2.6f, snout = 3f, ear = 2.6f, tail = 2, horns = Horns.Antlers, spots = true };
            case "Antelope": return new Look { fur = R("6e4a22", "a8783c", "d6a860", "f2d49a"), belly = cream, horn = P.Coal, len = 14, r = 3.8f, legs = 11, legW = 1.5f, neck = 6, neckAngle = 58, head = 2.5f, snout = 3f, ear = 2.2f, tail = 2, horns = Horns.Straight, stripes = true };
            case "Boar": return new Look { fur = R("1e1612", "3c2a22", "5e4434", "866650"), belly = R("3c2a22", "5e4434", "866650", "a8886e"), horn = P.Bone, len = 14, r = 5.4f, legs = 5, legW = 2.2f, neck = 2, neckAngle = 10, head = 3.6f, snout = 4f, ear = 2f, tail = 3, tailKind = 2, horns = Horns.BoarTusks, mane = true };
            case "Wolf": return new Look { fur = P.Wolf, belly = R("62626c", "8e8c92", "c2bfc0", "e8e6e6"), len = 15, r = 4f, legs = 8, neck = 3, neckAngle = 30, head = 3.1f, snout = 4.4f, ear = 2.6f, tail = 7, tailKind = 1, fangs = true, eye = P.Gold[3], S = 1.05f };
            case "Bear": return new Look { fur = P.Fur, belly = P.Fur, len = 17, r = 6.6f, legs = 7, legW = 3.2f, neck = 2, neckAngle = 25, head = 4f, snout = 3f, ear = 1.6f, roundEars = true, tail = 1, hump = true, shaggy = true, fangs = true, S = 1.25f };
            case "Mammoth": return new Look { fur = R("2a1810", "4e2e1e", "7a4a2e", "a26e46"), belly = R("2a1810", "4e2e1e", "7a4a2e", "a26e46"), horn = P.Bone, len = 20, r = 9f, legs = 9, legW = 4.2f, neck = 1, neckAngle = 30, head = 5.6f, snout = 0, ear = 2, roundEars = true, tail = 3, tailKind = 2, horns = Horns.Tusks, hump = true, shaggy = true, trunk = true, S = 1.7f };
            case "Mossback": return new Look { fur = R("2e3428", "4e5a40", "74805e", "9aa47e"), belly = R("4e5a40", "74805e", "9aa47e", "c0c8a0"), accent = P.Leaf, len = 15, r = 5f, legs = 4, legW = 2.6f, neck = 3, neckAngle = 20, head = 3f, snout = 2.4f, ear = 0, tail = 2, tailKind = 4, shell = true, S = 1.1f };
            case "Griffin": return new Look { fur = P.Griffin, belly = P.Griffin, accent = R("8a7a62", "dccab0", "f5ead6", "ffffff"), horn = P.Gold, len = 15, r = 4.6f, legs = 8, neck = 5, neckAngle = 55, head = 3f, snout = 0, ear = 1.5f, tail = 8, tailKind = 3, wings = true, beak = true, talons = true, fly = 7, eye = P.Gold[2], S = 1.3f };
            case "Wyvern": return new Look { rig = Rig.Biped, fur = P.Wyvern, belly = R("4a4a2a", "7a7440", "a8a060", "d0c890"), horn = P.Bone, len = 13, r = 4.4f, legs = 8, neck = 9, neckAngle = 50, head = 3f, snout = 4.5f, tail = 14, tailKind = 5, wings = true, horns = Horns.Curved, fangs = true, fly = 9, eye = P.Gold[3], S = 1.35f };
            case "CrystalBeast": return new Look { fur = P.Moonstone, belly = P.Stone, accent = P.Mana, horn = P.Mana, len = 18, r = 7f, legs = 7, legW = 3.6f, neck = 2, neckAngle = 20, head = 4f, snout = 3f, ear = 0, tail = 4, tailKind = 4, horns = Horns.CrystalHorn, hump = true, crystals = true, glowEyes = true, eye = P.Mana[3], S = 1.45f };
            case "FrostStrider": return new Look { fur = P.Frostskin, belly = R("a8c8e4", "c8dcf0", "e6f2ff", "ffffff"), horn = P.Steel, len = 15, r = 4f, legs = 12, legW = 1.4f, neck = 5, neckAngle = 50, head = 2.6f, snout = 2.6f, ear = 0, tail = 2, sixLegs = true, antennae = true, glowEyes = true, eye = P.Neon[2], S = 1.1f };
            case "IceLurker": return new Look { fur = R("6a8cb0", "a8c8e4", "dceaf6", "ffffff"), belly = R("a8c8e4", "dceaf6", "f2f8ff", "ffffff"), len = 17, r = 4.2f, legs = 7, legW = 2.4f, neck = 2, neckAngle = 20, head = 3.4f, snout = 2.6f, ear = 2f, tail = 10, tailKind = 2, cat = true, fangs = true, stripes = true, glowEyes = true, eye = P.Neon[2], S = 1.2f };
            case "SandSkitter": return new Look { fur = P.Sandskin, belly = P.Sand, accent = P.Bronze, len = 11, r = 3.4f, legs = 4, legW = 1.2f, neck = 1, neckAngle = 0, head = 2.4f, snout = 1.6f, ear = 0, tail = 2, tailKind = 4, sixLegs = true, plates = true, antennae = true, S = 1.1f };
            case "DuneWyrm": return new Look { fur = R("5a3e22", "8c6a3a", "b8945a", "dcc088"), belly = P.Sand, accent = P.Bronze, len = 28, r = 5f, legs = 3, legW = 2.2f, neck = 0, neckAngle = 0, head = 4.6f, snout = 0, ear = 0, tail = 6, tailKind = 4, sixLegs = true, plates = true, longJaw = false, fangs = true, S = 1.5f };
            case "EmberHound": return new Look { fur = P.Ashskin, belly = R("2a1a1a", "5a2a22", "8a4030", "c86a4a"), accent = P.Fire, len = 15, r = 4f, legs = 8, neck = 3, neckAngle = 30, head = 3.1f, snout = 4.2f, ear = 2.6f, tail = 7, tailKind = 1, fangs = true, flames = true, glowEyes = true, eye = P.Fire[3], S = 1.05f };
            case "MagmaTortoise": return new Look { fur = R("2a2220", "4a3c34", "6e5a4a", "947c66"), belly = P.Rock, accent = P.Fire, len = 18, r = 6f, legs = 4, legW = 3.4f, neck = 4, neckAngle = 15, head = 2.8f, snout = 2f, ear = 0, tail = 2, tailKind = 4, shell = true, glowEyes = true, eye = P.Fire[3], S = 1.55f };
            case "RazorRaptor": return new Look { rig = Rig.Biped, fur = R("1e3a24", "2f6236", "4f8f48", "86c46a"), belly = P.Linen, accent = P.Berry, len = 11, r = 3.6f, legs = 9, neck = 6, neckAngle = 45, head = 2.6f, snout = 4f, tail = 12, tailKind = 4, mane = true, fangs = true, eye = P.Gold[3], S = 1.1f };
            case "CanopyStrider": return new Look { fur = P.Reefskin, belly = R("2e7a72", "4ab0a0", "9ae0d0", "e0fff8"), accent = R("143a36", "1a4a4a", "245c58", "2e7a72"), horn = P.Reefskin, len = 15, r = 5f, legs = 15, legW = 2f, neck = 15, neckAngle = 72, head = 2.6f, snout = 3f, ear = 1.6f, tail = 4, tailKind = 2, spots = true, horns = Horns.Straight, hornSize = 0.5f, S = 1.5f };
            case "ShardStalker": return new Look { fur = P.Shardskin, belly = R("6a4aa0", "a080e0", "c8b4f0", "e0d0ff"), accent = P.Mana, len = 16, r = 4f, legs = 7, legW = 2.2f, neck = 2, neckAngle = 20, head = 3.2f, snout = 2.6f, ear = 2.2f, tail = 10, tailKind = 2, cat = true, crystals = true, fangs = true, glowEyes = true, eye = P.Mana[3], S = 1.15f };
            case "PrismMoth": return new Look { rig = Rig.Moth, fur = P.Linen, belly = P.Linen, accent = P.Prism, len = 8, r = 3f, legs = 3, head = 2.2f, antennae = true, fly = 11, eye = P.Coal[1], S = 1.6f };
            case "MireLurker": return new Look { fur = P.Bogskin, belly = R("4e6a2c", "7a9a46", "b0c878", "dce8b0"), accent = P.Troll, len = 22, r = 3.8f, legs = 3, legW = 2.4f, neck = 1, neckAngle = 0, head = 3f, snout = 7f, ear = 0, tail = 14, tailKind = 4, longJaw = true, plates = true, fangs = true, eye = P.Gold[3], S = 1.35f };
            case "ShellCrawler": return new Look { rig = Rig.Crab, fur = R("561726", "97262d", "d4453a", "ff8466"), belly = P.Sand, accent = P.Bone, len = 12, r = 5f, legs = 5, S = 1.1f };
            case "CraterHulk": return new Look { fur = P.Rock, belly = P.Stone, accent = P.Starmetal, len = 17, r = 7.4f, legs = 7, legW = 4f, neck = 1, neckAngle = 10, head = 3.6f, snout = 2.2f, ear = 0, tail = 1, hump = true, crystals = true, glowEyes = true, eye = P.Gold[3], S = 1.55f };
            case "SporeGrazer": return new Look { rig = Rig.Puff, fur = P.Purple, belly = P.Arcane, accent = P.Flower, len = 10, r = 6f, legs = 7, S = 1.1f };
            case "WhiteStag": return new Look { fur = R("9a948a", "d2ccc2", "f2eee6", "ffffff"), belly = R("d2ccc2", "f2eee6", "ffffff", "ffffff"), horn = P.Gold, len = 15, r = 4.2f, legs = 12, legW = 1.7f, neck = 7, neckAngle = 62, head = 2.7f, snout = 3f, ear = 2.6f, tail = 2, horns = Horns.Antlers, hornSize = 1.5f, glowEyes = true, eye = P.Mana[3], S = 1.45f };
            case "AncientMammoth": return new Look { fur = R("5a5048", "8a8076", "b8aea2", "e0d8cc"), belly = R("5a5048", "8a8076", "b8aea2", "e0d8cc"), horn = R("a89878", "d8c8a4", "f2e6c8", "fffaf0"), len = 21, r = 9.5f, legs = 9, legW = 4.4f, neck = 1, neckAngle = 30, head = 5.8f, ear = 2, roundEars = true, tail = 3, tailKind = 2, horns = Horns.Tusks, hornSize = 1.4f, hump = true, shaggy = true, trunk = true, glowEyes = true, eye = P.Mana[3], S = 2.3f };
            case "StormGriffin": return new Look { fur = R("1a2232", "2e3a52", "4a5a7a", "7a8cb0"), belly = R("2e3a52", "4a5a7a", "7a8cb0", "b0c0e0"), accent = R("4a5a7a", "a8b8d8", "e0e8ff", "ffffff"), horn = P.Neon, len = 15, r = 4.8f, legs = 8, neck = 5, neckAngle = 55, head = 3.1f, ear = 1.5f, tail = 9, tailKind = 3, wings = true, beak = true, talons = true, glowEyes = true, eye = P.Neon[3], fly = 9, S = 1.75f };
            case "SkyWhale": return new Look { rig = Rig.Whale, fur = P.Whale, belly = R("3a6aaa", "8ac0ff", "c8e4ff", "f0f8ff"), accent = P.Neon, len = 34, r = 9f, fly = 22, S = 2.1f };
            case "Shardback": return new Look { fur = P.Riftstone, belly = P.Cosmic, accent = P.Mana, horn = P.Mana, len = 19, r = 7.6f, legs = 7, legW = 4f, neck = 2, neckAngle = 20, head = 4.2f, snout = 3f, ear = 0, tail = 5, tailKind = 4, horns = Horns.CrystalHorn, hornSize = 1.4f, hump = true, crystals = true, glowEyes = true, eye = P.Mana[3], S = 2.3f };
        }
        return new Look { fur = P.Fur, belly = P.Fur };
    }

    // ---------- Frames ----------

    public const int FrameCount = 12;

    /// <summary>All 12 frames, cropped to the creature, and the pivot (its feet) as a share of the frame.</summary>
    public static PixelCanvas[] Render(Look look, bool tamed, out Vector2 pivot)
    {
        const int W = 220, H = 180, ox = 90, oy = 8;
        var big = new PixelCanvas[FrameCount];
        int x0 = W, y0 = H, x1 = 0, y1 = 0;
        for (int f = 0; f < FrameCount; f++)
        {
            var c = new PixelCanvas(W, H);
            Draw(c, look, f, ox, oy, tamed);
            big[f] = c;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    if (c.Get(x, y).a > 0) { x0 = Mathf.Min(x0, x); y0 = Mathf.Min(y0, y); x1 = Mathf.Max(x1, x); y1 = Mathf.Max(y1, y); }
        }
        x0 = Mathf.Min(x0, ox - 2) - 1; x1 = Mathf.Max(x1, ox + 2) + 1;
        y0 = Mathf.Min(y0, oy) - 1; y1 += 1;
        int w = x1 - x0 + 1, h = y1 - y0 + 1;
        var frames = new PixelCanvas[FrameCount];
        for (int f = 0; f < FrameCount; f++)
        {
            var c = new PixelCanvas(w, h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    c.Put(x, y, big[f].Get(x + x0, y + y0));
            frames[f] = c;
        }
        pivot = new Vector2((ox - x0 + 0.5f) / w, (oy - y0) / (float)h);
        return frames;
    }

    private struct Pose
    {
        public int gait;      // walking: 0..3, or -1 standing
        public float lunge;   // forward shift of the whole body (attack)
        public float dip;     // head lowered (attack / graze), 0..1
        public bool open;     // mouth open
        public int death;     // 0 alive, 1 stagger, 2 falling, 3 down
        public float bob;
    }

    private static Pose PoseFor(Look l, int frame)
    {
        var p = new Pose { gait = -1 };
        if (frame >= 1 && frame <= 4) { p.gait = frame - 1; p.bob = frame % 2 == 0 ? 1 : 0; }
        else if (frame >= 5 && frame <= 8)
        {
            int a = frame - 5;
            if (l.fur != null && IsGrazerLook(l))
            {
                p.dip = a == 0 ? 0.6f : 1f;            // head down to graze
                p.open = a == 2;
            }
            else
            {
                p.lunge = a == 0 ? -1.5f : a == 1 ? 2.5f : a == 2 ? 4f : 1f;
                p.dip = a == 1 || a == 2 ? 0.45f : 0.15f;
                p.open = a == 1 || a == 2;
            }
        }
        else if (frame >= 9) p.death = frame - 8;
        return p;
    }

    // Grazers (no fangs, no beak, not a hunter) graze in their "attack" frames.
    private static bool IsGrazerLook(Look l) => !l.fangs && !l.beak && !l.talons && l.horns != Horns.Tusks && l.horns != Horns.BoarTusks && l.horns != Horns.CrystalHorn && !l.hump && !l.shell;

    public static void Draw(PixelCanvas c, Look l, int frame, int ox, int oy, bool tamed)
    {
        var p = PoseFor(l, frame);
        float alt = l.fly > 0 && p.death < 2 ? l.fly * l.S + (p.death == 1 ? -l.fly * l.S * 0.4f : (frame % 4 < 2 ? 1 : 0)) : 0f;
        c.Pivot = new Vector2(ox, oy + alt);
        c.Angle = p.death == 1 ? -9f : 0f;
        c.Shift = Vector2.zero;
        switch (l.rig)
        {
            case Rig.Quad: Quad(c, l, p, ox + p.lunge * l.S, oy + alt, tamed, frame); break;
            case Rig.Biped: Biped(c, l, p, ox + p.lunge * l.S, oy + alt, tamed, frame); break;
            case Rig.Moth: Moth(c, l, p, ox, oy + alt, frame); break;
            case Rig.Whale: Whale(c, l, p, ox, oy + alt, tamed, frame); break;
            case Rig.Crab: Crab(c, l, p, ox + p.lunge * l.S * 0.5f, oy, tamed, frame); break;
            case Rig.Puff: Puff(c, l, p, ox, oy, tamed, frame); break;
        }
        c.Angle = 0f;
        c.Pivot = Vector2.zero;
        c.Outline(P.Ink, 0.6f);
        float span = (l.len * 0.55f + l.r) * l.S;
        c.Shadow(ox + 1, oy, span, Mathf.Max(1.6f, 1.8f * l.S), (byte)(alt > 0 ? 50 : 80));
    }

    private static Vector2 V(float x, float y) => new Vector2(x, y);

    private static Color32[] Dark(Color32[] r) => new[] { r[0], r[0], r[1], r[1] };

    // ---------- Four legs ----------

    private static void Quad(PixelCanvas c, Look l, Pose p, float x, float gy, bool tamed, int frame)
    {
        float S = l.S;
        bool down = p.death >= 2;
        float sink = p.death == 2 ? 0.5f : p.death == 3 ? 1f : 0f;
        float legH = l.legs * S * (1f - sink * 0.85f);
        float by = gy + legH + l.r * S * (1f - sink * 0.25f) + p.bob;   // body centre height
        float half = l.len * 0.5f * S;
        Vector2 rear = V(x - half, by), front = V(x + half * 0.9f, by + (l.hump ? 0.8f * S : 0));
        var fur = l.fur;
        var dark = Dark(fur);

        // Wings behind (far wing first).
        if (l.wings && !down) Wing(c, l, front + V(-2 * S, l.r * S * 0.6f), frame, true);

        // Far legs.
        int legCount = l.sixLegs ? 3 : 2;
        if (!down) for (int i = 0; i < legCount; i++) QuadLeg(c, l, LegX(l, x, i, legCount), by - l.r * S * 0.5f, gy, Phase(p, i, true), dark, i == legCount - 1, true);

        // Tail.
        Tail(c, l, rear + V(-1 * S, l.r * S * 0.35f), down, frame, fur);

        // Body: a long rounded barrel, deeper at the chest.
        float rr = l.r * S;
        c.Capsule(rear, front, rr, fur);
        c.Ellipse(front + V(0, -0.2f * S), rr * 1.08f, rr * 1.05f, fur);
        if (l.hump) c.Ellipse(front + V(-1.5f * S, rr * 0.55f), rr * 0.9f, rr * 0.75f, fur);
        // Pale belly.
        if (l.belly != null) c.Capsule(rear + V(1.5f * S, -rr * 0.55f), front + V(-0.5f * S, -rr * 0.6f), rr * 0.38f, l.belly, -0.3f);
        // Coat details.
        if (l.shaggy) for (float t = 0; t <= 1f; t += 0.12f)
            {
                var q = Vector2.Lerp(rear, front, t) + V(0, -rr * 0.85f);
                c.Line(q, q + V(-0.6f * S, -1.6f * S), fur[0]);
                c.Line(q + V(0.8f * S, rr * 1.6f), q + V(0.2f * S, rr * 1.6f - 1.2f * S), fur[3]);
            }
        if (l.spots) for (int i = 0; i < 6; i++)
            {
                var q = Vector2.Lerp(rear, front, 0.15f + i * 0.13f) + V(0, (i % 2 == 0 ? 0.35f : -0.05f) * rr);
                c.Put(c.T(q), (l.belly ?? fur)[3]);
            }
        if (l.stripes) for (int i = 0; i < 4; i++)
            {
                var q = Vector2.Lerp(rear, front, 0.2f + i * 0.17f);
                c.Line(q + V(0, rr * 0.7f), q + V(-0.8f * S, -rr * 0.1f), fur[0]);
            }
        if (l.plates) for (int i = 0; i < 6; i++)
            {
                var q = Vector2.Lerp(rear, front, 0.08f + i * 0.17f) + V(0, rr * 0.75f);
                c.Ellipse(q, 1.6f * S, 1.1f * S, l.accent ?? fur);
            }
        if (l.mane)
        {
            for (int i = 0; i < 7; i++)
            {
                var q = Vector2.Lerp(front, rear, i * 0.1f) + V(0, rr * 0.9f);
                c.Line(q, q + V(-0.6f * S, 1.6f * S + (i % 2) * S), fur[0], 2);
            }
        }
        if (l.shell)
        {
            var sh = l.accent != null && l.accent != P.Fire ? l.accent : fur;
            var mid = Vector2.Lerp(rear, front, 0.5f) + V(0, rr * 0.45f);
            c.Ellipse(mid, half * 1.05f + rr * 0.3f, rr * 1.05f, sh == fur ? new[] { fur[0], fur[1], fur[2], fur[3] } : sh, q => q.y > by - rr * 0.35f);
            // plates, and the glow between them for a magma tortoise
            for (int i = -2; i <= 2; i++)
            {
                var q = mid + V(i * half * 0.36f, rr * 0.35f - Mathf.Abs(i) * 0.6f * S);
                if (l.accent == P.Fire) { c.Line(q + V(-1.2f * S, -1 * S), q + V(1.2f * S, 0.4f * S), P.Fire[2]); c.Put(c.T(q), P.Fire[3]); }
                else c.Put(c.T(q), sh[3]);
            }
        }
        if (l.crystals)
        {
            var cr = l.accent ?? P.Mana;
            for (int i = 0; i < 5; i++)
            {
                var q = Vector2.Lerp(rear, front, 0.1f + i * 0.2f) + V(0, rr * (l.hump && i >= 3 ? 1.25f : 0.85f));
                float hgt = (2.5f + (i * 7 % 3)) * S;
                c.Polygon(new[] { q + V(-1.2f * S, 0), q + V(1.2f * S, 0), q + V(0.4f * S, hgt), q + V(-0.6f * S, hgt * 0.8f) }, v => v.x < q.x ? cr[3] : cr[2]);
            }
        }
        if (l.flames && !down)
        {
            for (int i = 0; i < 5; i++)
            {
                var q = Vector2.Lerp(rear, front, 0.15f + i * 0.18f) + V(0, rr * 0.9f);
                float h = (1.5f + ((i + frame) % 3)) * S;
                c.Line(q, q + V(-0.6f * S, h), i % 2 == 0 ? P.Fire[2] : P.Fire[3], 2);
            }
            for (int i = 0; i < 3; i++) c.Put(c.T(Vector2.Lerp(rear, front, 0.3f + i * 0.2f)), P.Fire[3]);
        }

        // Tamed: a blue saddle-cloth with gold trim.
        if (tamed)
        {
            var mid = Vector2.Lerp(rear, front, 0.5f);
            var team = P.Blue;
            c.Polygon(new[] { mid + V(-3.2f * S, rr * 0.95f), mid + V(3.2f * S, rr * 0.95f), mid + V(3.6f * S, -rr * 0.2f), mid + V(-3.6f * S, -rr * 0.2f) },
                v => v.y < mid.y - rr * 0.1f ? P.Gold[2] : v.x < mid.x ? team[3] : team[2]);
        }

        // Neck and head.
        Head(c, l, p, front, rr, down, tamed, frame);

        // Near legs.
        if (!down) for (int i = 0; i < legCount; i++) QuadLeg(c, l, LegX(l, x, i, legCount) + 0.8f * S, by - l.r * S * 0.5f, gy, Phase(p, i, false), fur, i == legCount - 1, false);
        else
        {
            // Lying: legs folded under.
            for (int i = 0; i < legCount; i++)
            {
                float lx = LegX(l, x, i, legCount);
                c.Line(V(lx, by - rr * 0.6f), V(lx + 2.5f * S, gy + 1), fur[1], Mathf.Max(2, Mathf.RoundToInt(l.legW * S * 0.8f)));
            }
        }

        // Near wing on top.
        if (l.wings) Wing(c, l, front + V(-2.5f * S, l.r * S * 0.4f), frame, false, down);
    }

    private static float LegX(Look l, float x, int i, int count)
    {
        float half = l.len * 0.5f * l.S;
        if (count == 3) return x - half * 0.9f + i * half * 0.9f;
        return i == 0 ? x - half * 0.85f : x + half * 0.75f;
    }

    /// <summary>Stride of one leg: -1..1 (forward positive), and whether it's lifted.</summary>
    private static float Phase(Pose p, int leg, bool far)
    {
        if (p.gait < 0) return 0f;
        // Diagonal pairs move together: near-front with far-back.
        int offset = (leg + (far ? 1 : 0)) % 2 == 0 ? 0 : 2;
        int ph = (p.gait + offset) % 4;
        return ph == 0 ? 1f : ph == 1 ? 0f : ph == 2 ? -1f : 0f;
    }

    private static void QuadLeg(PixelCanvas c, Look l, float x, float top, float gy, float stride, Color32[] ramp, bool front, bool far)
    {
        float S = l.S;
        float sw = stride * 2.2f * S;
        float lift = stride > 0.5f ? 1.2f * S : 0f;
        int w = Mathf.Max(1, Mathf.RoundToInt(l.legW * S));
        float h = top - gy;
        // Hind legs bend back at the hock, front legs at the knee forward.
        Vector2 hip = V(x, top), knee = V(x + sw * 0.4f + (front ? 0.6f * S : -1.2f * S), gy + h * 0.5f + lift),
            foot = V(x + sw, gy + lift + (far ? 0.5f : 0));
        if (l.legW >= 3f) c.Capsule(hip, knee, l.legW * 0.55f * S, ramp);
        else c.Line(hip, knee, ramp[2], w + 1);
        c.Line(knee, foot, ramp[1], w);
        // Hoof, paw or talon.
        var tip = l.talons && front ? P.Gold : l.cat || l.fangs ? ramp : P.Coal;
        c.Put(c.T(foot + V(0.5f, 0)), tip[1]);
        if (w >= 2) c.Put(c.T(foot + V(1.5f, 0)), tip[0]);
        if (l.talons && front) c.Put(c.T(foot + V(2.5f, 0)), P.Gold[2]);
    }

    private static void Tail(PixelCanvas c, Look l, Vector2 at, bool down, int frame, Color32[] fur)
    {
        float S = l.S;
        float t = l.tail * S;
        if (t <= 0) return;
        float sway = (frame % 4 < 2 ? 0.5f : -0.5f) * S;
        switch (l.tailKind)
        {
            case 1: c.Capsule(at, at + V(-t, (down ? -1 : -t * 0.45f) + sway), 1.8f * S, fur); break;                          // bushy
            case 2: c.Line(at, at + V(-t, (down ? 0 : -t * 0.6f) + sway), fur[1], Mathf.Max(1, Mathf.RoundToInt(S))); break; // thin
            case 3:                                                                                                          // tufted (lion)
                c.Line(at, at + V(-t * 0.8f, t * 0.1f + sway), fur[1], Mathf.Max(1, Mathf.RoundToInt(S)));
                c.Ellipse(at + V(-t * 0.85f, t * 0.12f + sway), 1.4f * S, 1.4f * S, l.belly == fur ? P.Hair : Dark(fur));
                break;
            case 4: c.Capsule(at, at + V(-t, (down ? -0.5f : -l.r * S * 0.7f) + sway * 0.5f), Mathf.Max(1.2f, l.r * S * 0.4f), fur); break;   // heavy
            default: c.Ellipse(at + V(-0.5f * S, -0.5f * S), Mathf.Max(1f, t * 0.6f), Mathf.Max(1f, 1.2f * S), l.belly ?? fur); break;     // short
        }
    }

    private static void Head(PixelCanvas c, Look l, Pose p, Vector2 chest, float rr, bool down, bool tamed, int frame)
    {
        float S = l.S;
        float ang = (down ? -10f : l.neckAngle * (1f - p.dip) - 40f * p.dip) * Mathf.Deg2Rad;
        Vector2 neckBase = chest + V(rr * 0.5f, rr * 0.3f);
        Vector2 hp = neckBase + V(Mathf.Cos(ang), Mathf.Sin(ang)) * (l.neck * S + l.head * S * 0.4f);
        if (down) hp = V(chest.x + rr * 0.9f + l.head * S, chest.y - rr * 0.4f);
        if (p.dip > 0.7f && !down) hp.y = Mathf.Max(hp.y, chest.y - rr - l.legs * S + l.head * S);   // nose to the grass
        var fur = l.fur;
        if (l.neck > 0.5f) c.Capsule(neckBase, hp, Mathf.Max(1.4f, Mathf.Min(rr * 0.6f, l.head * S * 0.85f)), fur);
        if (l.mane && l.neck > 2f) c.Line(neckBase + V(-1 * S, rr * 0.6f), hp + V(-1 * S, l.head * S * 0.6f), fur[0], 2);
        if (tamed) c.Line(Vector2.Lerp(neckBase, hp, 0.55f) + V(-1.2f * S, 1.2f * S), Vector2.Lerp(neckBase, hp, 0.55f) + V(1.2f * S, -1.2f * S), P.Blue[2], 2);   // collar

        float hr = l.head * S;
        if (l.beak)
        {
            // Eagle's head: white feathers and a hooked golden beak.
            var head = l.accent ?? P.Linen;
            c.Ellipse(hp, hr, hr * 0.95f, head);
            Vector2 tip = hp + V(hr + 2.4f * S, -0.6f * S - (p.open ? 0.6f * S : 0));
            c.Polygon(new[] { hp + V(hr * 0.5f, 0.8f * S), tip, hp + V(hr * 0.5f, -1.2f * S) }, v => v.y > hp.y - 0.3f * S ? l.horn[2] : l.horn[1]);
            c.Put(c.T(tip + V(0, -0.6f)), l.horn[0]);
            c.Polygon(new[] { hp + V(-hr * 0.4f, hr * 0.5f), hp + V(-hr * 1.3f, hr * 1.1f), hp + V(-hr * 0.1f, hr * 0.9f) }, v => head[2]);   // ear tuft
        }
        else
        {
            c.Ellipse(hp, hr, hr * (l.cat ? 0.85f : 0.9f), fur);
            if (l.trunk)
            {
                // Mammoth: a long trunk curling down, from a high domed head.
                c.Ellipse(hp + V(-0.5f * S, hr * 0.45f), hr * 0.8f, hr * 0.7f, fur);
                Vector2 t1 = hp + V(hr * 0.9f, -hr * 0.6f), t2 = t1 + V(1.2f * S, -hr * 0.9f - (p.dip > 0 ? 1.5f * S : 0)), t3 = t2 + V(p.open ? 2.4f * S : 1f * S, -1.6f * S);
                c.Capsule(hp + V(hr * 0.4f, 0), t1, 1.9f * S, fur);
                c.Capsule(t1, t2, 1.5f * S, fur);
                c.Capsule(t2, t3, 1.1f * S, fur);
            }
            else if (l.snout > 0)
            {
                float sl = l.snout * S;
                float sr = l.longJaw ? 1.3f * S : Mathf.Max(1f, hr * 0.5f);
                Vector2 tip = hp + V(hr * 0.5f + sl, -hr * 0.3f);
                c.Capsule(hp + V(hr * 0.3f, -hr * 0.2f), tip, sr, fur);
                c.Put(c.T(tip + V(sr * 0.6f, sr * 0.3f)), P.Ink);                                     // nose
                if (l.horns == Horns.BoarTusks) c.Ellipse(tip + V(sr * 0.4f, 0), 0.9f * S, sr * 0.9f, l.belly ?? fur);   // pig's disc
                if (p.open)
                {
                    c.Polygon(new[] { hp + V(hr * 0.4f, -hr * 0.5f), tip + V(sr * 0.8f, -sr * 1.6f), tip + V(sr * 0.2f, -sr * 0.4f) }, v => P.Red[1]);
                    if (l.fangs) { c.Put(c.T(tip + V(0, -sr * 0.8f)), P.Bone[3]); c.Put(c.T(tip + V(-sl * 0.4f, -sr * 0.9f)), P.Bone[3]); }
                }
                else if (l.fangs && (l.cat || l.longJaw)) c.Put(c.T(tip + V(-0.5f * S, -sr)), P.Bone[3]);
                if (l.longJaw) for (int i = 0; i < 3; i++) c.Put(c.T(hp + V(hr + i * sl * 0.28f, -hr * 0.4f - sr * 0.6f)), P.Bone[3]);
            }
            // Ears.
            if (l.ear > 0)
            {
                float e = l.ear * S;
                if (l.roundEars) c.Ellipse(hp + V(-hr * 0.35f, hr * 0.8f), e * 0.8f, e * 0.8f, Dark(fur));
                else c.Polygon(new[] { hp + V(-hr * 0.55f, hr * 0.55f), hp + V(-hr * 0.4f - e * 0.3f, hr * 0.6f + e * 1.1f), hp + V(-hr * 0.05f, hr * 0.7f) }, v => fur[2]);
            }
        }
        if (l.mane && l.neck <= 2f && !l.beak)
            for (int i = 0; i < 3; i++) c.Line(hp + V(-hr * 0.6f - i * 1.2f * S, hr * 0.5f), hp + V(-hr * 0.8f - i * 1.2f * S, hr * 0.5f + 2f * S), fur[0], 2);

        // Horns and the like.
        float hs = l.hornSize * S;
        var horn = l.horn ?? P.Bone;
        switch (l.horns)
        {
            case Horns.Antlers:
            {
                Vector2 b = hp + V(-hr * 0.2f, hr * 0.8f);
                Vector2 m = b + V(-2.2f * hs, 3.5f * hs), t = m + V(-1.5f * hs, 3f * hs);
                c.Line(b, m, horn[2]); c.Line(m, t, horn[3]);
                c.Line(m, m + V(1.8f * hs, 1.6f * hs), horn[3]); c.Line(Vector2.Lerp(b, m, 0.5f), Vector2.Lerp(b, m, 0.5f) + V(1.6f * hs, 1.2f * hs), horn[2]);
                c.Line(t, t + V(1.4f * hs, 1.2f * hs), horn[3]); c.Line(m + V(-0.6f * hs, 1.2f * hs), m + V(-2.6f * hs, 2.2f * hs), horn[2]);
                if (l.glowEyes) { c.Put(c.T(t + V(1.4f * hs, 1.2f * hs)), P.White); c.Put(c.T(t), P.White); }
                break;
            }
            case Horns.Straight:
            {
                Vector2 b = hp + V(-hr * 0.1f, hr * 0.8f);
                c.Line(b, b + V(-3.5f * hs, 5.5f * hs), horn[1], Mathf.Max(1, Mathf.RoundToInt(S)));
                c.Line(b + V(0.8f, 0), b + V(-2.5f * hs, 5.8f * hs), horn[2]);
                break;
            }
            case Horns.Curved:
            {
                Vector2 b = hp + V(-hr * 0.3f, hr * 0.7f);
                c.Line(b, b + V(-2.5f * hs, 2f * hs), horn[2], 2); c.Line(b + V(-2.5f * hs, 2f * hs), b + V(-4.5f * hs, 1f * hs), horn[3]);
                break;
            }
            case Horns.Tusks:
            {
                Vector2 b = hp + V(hr * 0.6f, -hr * 0.45f);
                Vector2 m = b + V(2.6f * hs, -2.4f * hs), t = m + V(2.6f * hs, 1.8f * hs);
                c.Line(b, m, horn[2], Mathf.Max(2, Mathf.RoundToInt(1.5f * S)));
                c.Line(m, t, horn[3], Mathf.Max(1, Mathf.RoundToInt(S)));
                c.Put(c.T(t + V(0.6f * hs, 1f * hs)), horn[3]);
                break;
            }
            case Horns.BoarTusks:
            {
                Vector2 b = hp + V(hr * 0.5f + l.snout * S * 0.6f, -hr * 0.5f);
                c.Line(b, b + V(0.8f * S, 1.8f * S), horn[3]);
                break;
            }
            case Horns.CrystalHorn:
            {
                Vector2 b = hp + V(hr * 0.4f, hr * 0.6f);
                c.Polygon(new[] { b + V(-1.2f * hs, 0), b + V(1.2f * hs, 0), b + V(2.6f * hs, 4.4f * hs) }, v => v.x < b.x + 0.6f * hs ? horn[3] : horn[2]);
                break;
            }
        }
        if (l.antennae)
        {
            Vector2 b = hp + V(0, hr * 0.8f);
            c.Line(b, b + V(2.5f * S, 3f * S), (l.accent ?? fur)[1]);
            c.Put(c.T(b + V(2.8f * S, 3.3f * S)), l.glowEyes ? l.eye : fur[3]);
        }
        // Eye.
        Vector2 eye = hp + V(hr * (l.beak ? 0.3f : 0.2f), hr * 0.25f);
        if (p.death > 0) c.Put(c.T(eye), P.Coal[2]);
        else
        {
            c.Put(c.T(eye), l.eye);
            if (l.glowEyes) c.Put(c.T(eye + V(1, 0)), l.eye);
        }
    }

    private static void Wing(PixelCanvas c, Look l, Vector2 shoulder, int frame, bool far, bool down = false)
    {
        float S = l.S;
        // Flap: up, level, down, level (walk and idle); held high to strike.
        int ph = frame % 4;
        float lift = down ? -0.3f : frame >= 5 && frame <= 8 ? 1f : ph == 0 ? 1f : ph == 2 ? -0.6f : 0.3f;
        float span = (l.rig == Rig.Biped ? 12f : 15f) * S * (far ? 0.8f : 1f);
        Vector2 tip = shoulder + V(-span * 0.55f, span * (0.35f + 0.55f * lift));
        Vector2 elbow = shoulder + V(-span * 0.15f, span * (0.25f + 0.35f * lift));
        var ramp = l.rig == Rig.Biped ? l.fur : (far ? Dark(l.fur) : l.fur);
        if (l.rig == Rig.Biped)
        {
            // A wyvern's bat wing: arm bones with skin stretched to the body.
            var skin = far ? Dark(l.belly) : l.belly;
            c.Polygon(new[] { shoulder, elbow, tip, shoulder + V(-span * 0.5f, -1 * S), shoulder + V(-span * 0.1f, -2 * S) }, v => v.y > shoulder.y + span * 0.2f ? skin[2] : skin[1]);
            c.Line(shoulder, elbow, ramp[1], 2);
            c.Line(elbow, tip, ramp[1]);
            c.Line(elbow, shoulder + V(-span * 0.5f, -1 * S), ramp[1]);
        }
        else
        {
            // Feathered: rows of feathers, pale at the leading edge.
            var lead = l.accent ?? P.Linen;
            for (int i = 0; i < 5; i++)
            {
                float t = i / 4f;
                Vector2 root = Vector2.Lerp(shoulder, elbow, t);
                Vector2 end = Vector2.Lerp(elbow, tip, t) + V(-1.5f * S * t, -3.5f * S + t * 1f);
                c.Line(root, end, i == 0 ? ramp[3] : ramp[2 - (i % 2)], Mathf.Max(2, Mathf.RoundToInt(1.6f * S)));
            }
            c.Line(shoulder, elbow, lead[2], 2);
            c.Line(elbow, tip, lead[1], 2);
        }
    }

    // ---------- Two legs (wyvern, raptor) ----------

    private static void Biped(PixelCanvas c, Look l, Pose p, float x, float gy, bool tamed, int frame)
    {
        float S = l.S;
        bool down = p.death >= 2;
        float sink = p.death == 2 ? 0.5f : p.death == 3 ? 1f : 0f;
        float legH = l.legs * S * (1f - sink * 0.8f);
        float by = gy + legH + l.r * S * 0.8f + p.bob;
        float half = l.len * 0.5f * S;
        // Leaning forward: hips back and low, chest forward and a little higher.
        Vector2 hip = V(x - half * 0.5f, by), chest = V(x + half * 0.7f, by + (down ? 0 : 1.5f * S));
        var fur = l.fur;
        if (l.wings && !down) Wing(c, l, chest + V(-1 * S, l.r * S * 0.5f), frame, true);
        if (!down) BipedLeg(c, l, hip, gy, Phase(p, 0, true), Dark(fur));
        // Tail, out behind for balance.
        Vector2 tailEnd = hip + V(-l.tail * S, (down ? -1 : 1.5f) * S + (frame % 4 < 2 ? 0.6f : -0.6f) * S);
        c.Capsule(hip, Vector2.Lerp(hip, tailEnd, 0.5f), l.r * S * 0.55f, fur);
        c.Capsule(Vector2.Lerp(hip, tailEnd, 0.5f), tailEnd, Mathf.Max(0.8f, l.r * S * 0.25f), fur);
        if (l.tailKind == 5) c.Polygon(new[] { tailEnd + V(0, 1.5f * S), tailEnd + V(-3f * S, 0), tailEnd + V(0, -1.5f * S) }, v => l.horn[2]);
        c.Capsule(hip, chest, l.r * S, fur);
        c.Capsule(hip + V(1 * S, -l.r * S * 0.5f), chest + V(0, -l.r * S * 0.55f), l.r * S * 0.4f, l.belly, -0.3f);
        if (l.mane) for (int i = 0; i < 4; i++) c.Line(Vector2.Lerp(hip, chest, i / 4f) + V(0, l.r * S * 0.9f), Vector2.Lerp(hip, chest, i / 4f) + V(-1 * S, l.r * S + 2f * S), l.accent[2], 2);
        if (tamed)
        {
            var mid = Vector2.Lerp(hip, chest, 0.5f);
            c.Polygon(new[] { mid + V(-2.5f * S, l.r * S * 0.95f), mid + V(2.5f * S, l.r * S * 0.95f), mid + V(2.8f * S, -l.r * S * 0.1f), mid + V(-2.8f * S, -l.r * S * 0.1f) },
                v => v.y < mid.y ? P.Gold[2] : P.Blue[2]);
        }
        // Little arms (raptor) with claws.
        if (!l.wings && !down)
        {
            Vector2 sh = chest + V(1 * S, -l.r * S * 0.2f), hand = sh + V(2.2f * S + p.lunge * 0.3f * S, -2.2f * S);
            c.Line(sh, hand, fur[1], 2);
            c.Put(c.T(hand + V(1, -1)), P.Bone[3]);
        }
        Head(c, l, p, chest, l.r * S, down, tamed, frame);
        if (!down) BipedLeg(c, l, hip + V(1 * S, 0), gy, Phase(p, 0, false), fur);
        else c.Line(hip + V(0, -l.r * S * 0.5f), hip + V(3 * S, -l.r * S), fur[1], 2);
        if (l.wings) Wing(c, l, chest + V(-1.5f * S, l.r * S * 0.4f), frame, false, down);
    }

    private static void BipedLeg(PixelCanvas c, Look l, Vector2 hip, float gy, float stride, Color32[] ramp)
    {
        float S = l.S;
        float sw = stride * 3f * S, lift = stride > 0.5f ? 1.5f * S : 0f;
        Vector2 knee = V(hip.x + 1.5f * S + sw * 0.4f, (hip.y + gy) * 0.5f + 1 * S + lift);
        Vector2 ankle = V(hip.x - 0.8f * S + sw * 0.8f, gy + 2f * S + lift);
        Vector2 foot = V(ankle.x + 2.5f * S, gy + lift);
        c.Capsule(hip, knee, Mathf.Max(1.2f, l.r * S * 0.45f), ramp);
        c.Line(knee, ankle, ramp[1], 2);
        c.Line(ankle, foot, ramp[1], 2);
        c.Put(c.T(foot + V(1, 0)), P.Bone[2]);
        if (l.rig == Rig.Biped && l.fangs) c.Put(c.T(ankle + V(1.5f * S, 1 * S)), P.Bone[3]);   // sickle claw
    }

    // ---------- Moth ----------

    private static void Moth(PixelCanvas c, Look l, Pose p, float x, float gy, int frame)
    {
        float S = l.S;
        if (p.death >= 2) { gy = gy - l.fly * S + 2; }
        var prism = l.accent ?? P.Prism;
        int ph = frame % 4;
        float flap = p.death >= 2 ? 0.1f : ph == 0 ? 1f : ph == 1 ? 0.4f : ph == 2 ? -0.4f : 0.4f;
        Vector2 body = V(x, gy + 3 * S);
        // Far wings, body, near wings: big panes of colour with dark veins.
        void WingPair(bool far)
        {
            float k = far ? 0.75f : 1f;
            Vector2 up = body + V(-1 * S, 9 * S * flap * k + 1 * S), back = body + V(-8 * S * k, 5 * S * flap * k);
            var a = far ? prism[3] : prism[0];
            var b = far ? prism[2] : prism[1];
            c.Polygon(new[] { body, up + V(3 * S, 0), up + V(-2 * S, 1 * S), back }, v => (int)((v.x + v.y) / (2 * S)) % 2 == 0 ? a : b);
            c.Polygon(new[] { body, back + V(0, -2 * S), body + V(-4 * S * k, -4 * S) }, v => far ? prism[1] : prism[3]);
            c.Line(body, up, P.Coal[1]);
            c.Line(body, back, P.Coal[1]);
        }
        WingPair(true);
        c.Capsule(body + V(-4 * S, 0), body + V(2 * S, 0.5f * S), 2.2f * S, l.fur);
        c.Ellipse(body + V(3.5f * S, 0.8f * S), l.head * S, l.head * S, l.fur);
        c.Line(body + V(4 * S, 2.5f * S), body + V(6.5f * S, 5.5f * S), P.Coal[2]);
        c.Line(body + V(3 * S, 2.5f * S), body + V(4.5f * S, 6 * S), P.Coal[2]);
        c.Put(c.T(body + V(4.5f * S, 1 * S)), p.death > 0 ? P.Coal[2] : P.Coal[0]);
        for (int i = 0; i < 3; i++) c.Line(body + V(-1 * S + i * 1.5f * S, -1.5f * S), body + V(-1.5f * S + i * 1.5f * S, -4 * S), P.Coal[2]);
        WingPair(false);
        // Glittering dust.
        if (p.death == 0) for (int i = 0; i < 3; i++) c.Put(c.T(body + V(-6 * S + ((frame + i * 3) % 5) * 2 * S, -3 * S - i * 1.5f * S)), prism[(frame + i) % 4]);
    }

    // ---------- Whale ----------

    private static void Whale(PixelCanvas c, Look l, Pose p, float x, float gy, bool tamed, int frame)
    {
        float S = l.S;
        if (p.death >= 2) gy = gy - l.fly * S * (p.death == 2 ? 0.5f : 1f) + 4 * S;
        float half = l.len * 0.5f * S, rr = l.r * S;
        float swim = (frame % 4 < 2 ? 1f : -1f) * S;
        Vector2 head = V(x + half * 0.6f, gy + rr + 2 * S), tail = V(x - half, gy + rr + 2 * S + swim);
        var skin = l.fur;
        // Tail flukes, body, pale ridged belly, fins.
        Vector2 fl = tail + V(-2 * S, 0);
        c.Polygon(new[] { fl, fl + V(-6 * S, 5 * S + swim * 2), fl + V(-3 * S, 0), fl + V(-6 * S, -4 * S + swim * 2) }, v => skin[1]);
        c.Capsule(tail, head, rr * 0.55f, skin);
        c.Ellipse(head + V(-rr * 0.2f, 0), rr * 1.25f, rr, skin);
        c.Ellipse(Vector2.Lerp(tail, head, 0.55f), half * 0.75f, rr * 0.95f, skin);
        c.Capsule(Vector2.Lerp(tail, head, 0.25f) + V(0, -rr * 0.55f), head + V(rr * 0.6f, -rr * 0.55f), rr * 0.35f, l.belly, -0.3f);
        for (int i = 0; i < 6; i++) c.Line(head + V(-i * 2.2f * S, -rr * 0.35f), head + V(-i * 2.2f * S - 0.6f * S, -rr * 0.85f), l.belly[0]);
        Vector2 fin = Vector2.Lerp(tail, head, 0.6f) + V(0, -rr * 0.4f);
        c.Polygon(new[] { fin, fin + V(-5 * S, -5 * S - swim), fin + V(2 * S, -1 * S) }, v => skin[0]);
        // Glowing spots along its flank (a creature of the sky).
        var glow = l.accent ?? P.Neon;
        for (int i = 0; i < 7; i++) c.Put(c.T(Vector2.Lerp(tail, head, 0.1f + i * 0.12f) + V(0, rr * 0.35f + (i % 2) * S)), glow[(i + frame) % 2 == 0 ? 3 : 2]);
        if (tamed) c.Line(head + V(-rr * 1.2f, rr * 0.9f), head + V(-rr * 1.5f, -rr * 0.9f), P.Blue[2], 3);
        Vector2 eye = head + V(rr * 0.5f, rr * 0.1f);
        c.Put(c.T(eye), p.death > 0 ? P.Coal[2] : P.White);
        c.Put(c.T(eye + V(1, 0)), P.Ink);
        c.Line(head + V(rr * 0.4f, -rr * 0.3f), head + V(rr * 1.2f, -rr * 0.2f), skin[0]);   // mouth
    }

    // ---------- Crab ----------

    private static void Crab(PixelCanvas c, Look l, Pose p, float x, float gy, bool tamed, int frame)
    {
        float S = l.S;
        bool down = p.death >= 2;
        float by = gy + (down ? 2.5f * S : l.legs * S + 1 * S) + p.bob;
        var shell = l.fur;
        void Legs(bool far)
        {
            for (int i = 0; i < 3; i++)
            {
                float lx = x - 3 * S + i * 3 * S;
                float step = p.gait < 0 ? 0 : ((p.gait + i + (far ? 1 : 0)) % 2 == 0 ? 1 : -1) * 1.2f * S;
                Vector2 h = V(lx, by), k = V(lx + (i - 1) * 2.5f * S + step * 0.5f, by + 1.5f * S), f = V(lx + (i - 1) * 4f * S + step, down ? by - 1 : gy);
                c.Line(h, k, far ? shell[0] : shell[1], 2);
                c.Line(k, f, far ? shell[0] : shell[1]);
            }
        }
        Legs(true);
        c.Ellipse(V(x, by + 1.5f * S), l.len * 0.5f * S, l.r * S * 0.7f, shell, q => q.y > by - 0.5f * S);
        c.Ellipse(V(x, by), l.len * 0.45f * S, 1.6f * S, l.belly);
        for (int i = -2; i <= 2; i++) c.Put(c.T(V(x + i * 2 * S, by + 2.5f * S - Mathf.Abs(i) * 0.4f * S)), shell[3]);
        if (tamed) c.Line(V(x - 3 * S, by + 3.5f * S), V(x + 3 * S, by + 3.5f * S), P.Blue[2], 2);
        // Eyestalks and a big claw.
        Vector2 eyes = V(x + 3 * S, by + 3.5f * S);
        c.Line(eyes, eyes + V(0.5f * S, 2.5f * S), shell[1]); c.Put(c.T(eyes + V(0.5f * S, 3 * S)), down ? P.Coal[2] : P.Ink);
        c.Line(eyes + V(-1.5f * S, 0), eyes + V(-1.2f * S, 2.2f * S), shell[1]); c.Put(c.T(eyes + V(-1.2f * S, 2.7f * S)), down ? P.Coal[2] : P.Ink);
        Legs(false);
        Vector2 arm = V(x + l.len * 0.45f * S, by + 1 * S), claw = arm + V(3 * S + p.lunge * 0.4f * S, 2 * S + (p.open ? 1.5f * S : 0));
        c.Line(arm, claw, shell[1], 2);
        c.Ellipse(claw + V(1 * S, 0), 2.4f * S, 1.8f * S, shell);
        c.Line(claw + V(1.5f * S, 0), claw + V(3.4f * S, p.open ? 1.5f * S : 0.3f * S), shell[0]);
    }

    // ---------- Puffball ----------

    private static void Puff(PixelCanvas c, Look l, Pose p, float x, float gy, bool tamed, int frame)
    {
        float S = l.S;
        bool down = p.death >= 2;
        float rr = l.r * S * (p.death == 3 ? 0.8f : 1f);
        float by = gy + (down ? rr * 0.8f : l.legs * S + rr * 0.8f) + p.bob - p.dip * 2 * S;
        for (int i = 0; i < 4 && !down; i++)
        {
            float lx = x - rr * 0.6f + i * rr * 0.4f;
            float st = p.gait < 0 ? 0 : ((p.gait + i) % 2 == 0 ? 1 : -1) * 1.2f * S;
            c.Line(V(lx, by - rr * 0.6f), V(lx + st, gy), i % 2 == 0 ? l.fur[0] : l.fur[1], 2);
        }
        c.Ellipse(V(x, by), rr, rr * 0.9f, l.fur);
        c.Ellipse(V(x - rr * 0.1f, by + rr * 0.55f), rr * 0.8f, rr * 0.45f, l.belly, null, 0.3f);
        var dots = l.accent ?? P.Flower;
        for (int i = 0; i < 6; i++) c.Ellipse(V(x - rr * 0.6f + (i * 7 % 5) * rr * 0.3f, by - rr * 0.3f + (i * 3 % 4) * rr * 0.3f), 0.9f * S, 0.9f * S, new[] { dots[i % 2], dots[i % 2], dots[(i + 1) % 4], dots[(i + 1) % 4] });
        if (tamed) c.Line(V(x - rr * 0.8f, by - rr * 0.2f), V(x + rr * 0.8f, by - rr * 0.2f), P.Blue[2], 2);
        // A little face, and spores drifting off it.
        Vector2 face = V(x + rr * 0.7f, by - rr * 0.1f);
        c.Put(c.T(face), down ? P.Coal[2] : P.Ink); c.Put(c.T(face + V(-2 * S, 0)), down ? P.Coal[2] : P.Ink);
        if (p.open) c.Put(c.T(face + V(-1 * S, -1.5f * S)), P.Red[1]);
        if (!down) for (int i = 0; i < 3; i++) c.Put(c.T(V(x - rr + ((frame * 2 + i * 5) % 9) * S, by + rr + 1 + ((frame + i * 2) % 6) * S)), dots[3]);
    }
}
