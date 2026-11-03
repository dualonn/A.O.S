using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using P = PixelPalette;

/// <summary>
/// Ore deposits that tell apart at a glance: each ore has its own shape and colour (coal lumps,
/// a rune monolith, crystal spires, a geode, a meteorite crater, floating rift shards...), and the
/// rarer ones glow faintly on the ground so they're easy to spot.
/// (Tools > Age of Stickman > Regenerate Ore Art redraws only these sprites.)
/// </summary>
public static partial class PlaceholderArt
{
    // Rock ramps (dark to light, 5 shades).
    private static readonly Color32[] CopperRock = { P.Hex("3a2418"), P.Hex("64402a"), P.Hex("8e603e"), P.Hex("b48660"), P.Hex("d8b088") };
    private static readonly Color32[] RustRock = { P.Hex("3c1812"), P.Hex("6a2a1c"), P.Hex("98422c"), P.Hex("bc6844"), P.Hex("dc9468") };
    private static readonly Color32[] QuartzRock = { P.Hex("6a645e"), P.Hex("9a948c"), P.Hex("c4beb4"), P.Hex("e2ded6"), P.Hex("fbf8f2") };
    private static readonly Color32[] Basalt = { P.Hex("16141a"), P.Hex("2a2630"), P.Hex("3e3846"), P.Hex("565062"), P.Hex("726c7e") };
    private static readonly Color32[] Cinnabar = { P.Hex("3a0e14"), P.Hex("6e1a20"), P.Hex("a02c2a"), P.Hex("c8503c"), P.Hex("e88a66") };
    private static readonly Color32[] PitchRock = { P.Hex("1e2420"), P.Hex("323c34"), P.Hex("4a5a4c"), P.Hex("66786a"), P.Hex("8a9c8c") };
    private static readonly Color32[] NightDust = { P.Hex("14142a"), P.Hex("2a2a4a"), P.Hex("44447a"), P.Hex("6a6aa8"), P.Hex("9090c8") };
    // Ore ramps (4 shades).
    private static readonly Color32[] Patina = { P.Hex("1c5a48"), P.Hex("2f8c6c"), P.Hex("4fc092"), P.Hex("a0f0c8") };
    private static readonly Color32[] CopperMetal = { P.Hex("7a3410"), P.Hex("c4621e"), P.Hex("f0923c"), P.Hex("ffd0a0") };
    private static readonly Color32[] Quartz = { P.Hex("7894a4"), P.Hex("b8d4e0"), P.Hex("e8f6fc"), P.Hex("ffffff") };

    [MenuItem("Tools/Age of Stickman/Regenerate Ore Art")]
    public static void RegenerateOreArt()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("Stop play mode first."); return; }
        var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Resources/GameDatabase.asset");
        var sets = new (string deposit, string file, Ore ore)[]
        {
            ("CopperOre", "Copper", Ore.Copper), ("IronOre", "Iron", Ore.Iron), ("GoldOre", "Gold", Ore.Gold),
            ("Runestone", "Runestone", Ore.Rune), ("Quicksilver", "Quicksilver", Ore.Quicksilver), ("Coal", "Coal", Ore.Coal),
            ("Aether", "Aether", Ore.Aether), ("Uranium", "Uranium", Ore.Uranium), ("Silicon", "Silicon", Ore.Silicon),
            ("Manastone", "Manastone", Ore.Mana), ("Starmetal", "Starmetal", Ore.Starmetal), ("Heliolite", "Heliolite", Ore.Heliolite),
            ("Riftstone", "Riftstone", Ore.Riftstone), ("Stardust", "Stardust", Ore.Stardust), ("Primordium", "Primordium", Ore.Primordium),
        };
        foreach (var (id, file, ore) in sets)
        {
            var dep = db.deposits.Find(d => d != null && d.id == id);
            if (dep == null) continue;
            dep.sprites = PixelDeposits(file, 3, ore);
            EditorUtility.SetDirty(dep);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Ore art regenerated.");
    }

    /// <summary>Draws the ore's own look. False for kinds still drawn by PixelDeposit (stone, oil, saltpetre).</summary>
    private static bool PixelOre(PixelCanvas c, int v, Ore ore)
    {
        var rng = new System.Random(v * 31 + (int)ore * 7 + 3);
        Color32? glow = null;
        switch (ore)
        {
            case Ore.Copper:
                OreBoulders(c, v, CopperRock);
                for (int i = 0; i < 3; i++) if (OreOnRock(c, rng, out var p)) c.Ellipse(p, 2.4f, 1.7f, Patina);
                for (int i = 0; i < 4; i++) if (OreOnRock(c, rng, out var p)) OreNugget(c, p, CopperMetal, 1.7f);
                break;

            case Ore.Iron:
                OreBoulders(c, v, RustRock);
                for (int i = 0; i < 3; i++)
                    if (OreOnRock(c, rng, out var p))
                    {
                        c.Line(p + new Vector2(-4, 1), p + new Vector2(0, -1), P.Coal[1], 2);
                        c.Line(p + new Vector2(0, -1), p + new Vector2(4, 0), P.Coal[2], 1);
                    }
                for (int i = 0; i < 3; i++) if (OreOnRock(c, rng, out var p)) OreNugget(c, p, P.Iron, 1.5f);
                break;

            case Ore.Gold:
                OreBoulders(c, v, QuartzRock);
                for (int i = 0; i < 4; i++) if (OreOnRock(c, rng, out var p)) OreNugget(c, p, P.Gold, i == 0 ? 2.6f : 1.9f);
                OreSparkle(c, 21 - v * 3, 17 - v, P.Gold[3]);
                OreSparkle(c, 9 + v * 2, 13, P.Gold[3], 1);
                break;

            case Ore.Coal:
                // a pile of black, jagged lumps with a bluish sheen
                var lumps = new List<(float x, float y, float r)> { (10, 7, 5), (21, 7, 5.5f), (15, 12, 5.5f), (25, 12, 3.5f), (7, 12, 3.5f) };
                if (v == 1) lumps.Add((16, 17, 3.5f));
                if (v == 2) lumps.RemoveAt(4);
                foreach (var (x, y, r) in lumps) OreLump(c, x, y, r, rng);
                break;

            case Ore.Rune:
            {
                // a carved standing stone with glowing runes, and a fallen stone beside it
                float mx = v == 1 ? 12 : 15;
                int top = 22 - v;
                c.Ellipse(new Vector2(mx + (v == 1 ? 10 : -8), 6), 5, 3.5f, Slate);
                c.Polygon(new[] { new Vector2(mx - 5, 3), new Vector2(mx + 5, 3), new Vector2(mx + 4, top - 2), new Vector2(mx + 1, top), new Vector2(mx - 4, top - 1) },
                    l => l.x < mx - 2 ? Slate[3] : l.x > mx + 2.5f ? Slate[1] : Slate[2]);
                int gx = Mathf.RoundToInt(mx) - 1;
                for (int g = 0; g < 3; g++)
                {
                    int gy = 6 + g * 5;
                    c.Put(gx, gy, P.Rune[2]); c.Put(gx, gy + 1, P.Rune[3]); c.Put(gx, gy + 2, P.Rune[2]);
                    c.Put(gx + 1 + (g % 2), gy + 1, P.Rune[2]); c.Put(gx - 1, gy + 2 - g % 2 * 2, P.Rune[2]);
                }
                glow = P.Rune[2];
                break;
            }

            case Ore.Quicksilver:
                OreBoulders(c, v, Cinnabar);
                // a pool of liquid silver in front, and beads of it on the rock
                c.Ellipse(new Vector2(14 + v * 2, 5), 7, 2.4f, P.Steel);
                c.Put(11 + v * 2, 6, P.White); c.Put(12 + v * 2, 6, P.White);
                for (int i = 0; i < 4; i++) if (OreOnRock(c, rng, out var p)) { c.Ellipse(p, 1.2f, 1.2f, P.Steel); c.Put(p.x - 0.5f, p.y + 0.5f, P.White); }
                break;

            case Ore.Aether:
                c.Ellipse(new Vector2(16, 6), 10, 4, P.Stone);
                foreach (var (x, h, lean) in new[] { (11f, 11, -0.18f), (16f, 16 - v, 0.04f), (21f, 12, 0.2f), (8f, 7, -0.3f), (24f, 7 + v, 0.32f) })
                    OreCrystal(c, x, 5, h, 1, lean, P.Aether);
                glow = P.Aether[2];
                break;

            case Ore.Uranium:
                OreBoulders(c, v, PitchRock);
                for (int i = 0; i < 5; i++) if (OreOnRock(c, rng, out var p)) OreNugget(c, p, P.Ray, i < 2 ? 2.1f : 1.5f);
                for (int i = 0; i < 3; i++) c.Put(8 + rng.Next(0, 16), 17 + rng.Next(0, 6), new Color32(124, 255, 124, 200));
                glow = P.Ray[2];
                break;

            case Ore.Silicon:
                // a cluster of clear quartz prisms on a little bed of sand
                c.Ellipse(new Vector2(16, 5), 10, 3, P.Linen);
                foreach (var (x, h, lean) in new[] { (10f, 8, -0.35f), (14f, 13, -0.1f), (18f, 11 + v, 0.12f), (22f, 8, 0.4f), (16f, 6, 0f), (7f, 5, -0.6f) })
                    OreCrystal(c, x, 4, h, 1, lean, Quartz);
                c.Put(15, 15, P.Neon[3]); c.Put(19, 13 + v, P.Neon[3]);
                break;

            case Ore.Mana:
            {
                // a split geode: dark stone outside, blue crystal inside, a mote of mana above it
                c.Ellipse(new Vector2(16, 9), 11, 7.5f, Slate);
                c.Ellipse(new Vector2(16, 8.5f), 7, 4.8f, new[] { P.Mana[0], P.Mana[0], P.Mana[1], P.Mana[1] });
                foreach (var (x, h, lean) in new[] { (12f, 5, -0.3f), (15f, 7, -0.1f), (18f, 6, 0.15f), (21f, 4, 0.4f) })
                    OreCrystal(c, x, 5, h, 0, lean, P.Mana);
                c.Ellipse(new Vector2(16 + v, 21), 1.6f, 1.6f, P.Mana);
                c.Put(15 + v, 22, P.White);
                glow = P.Mana[2];
                break;
            }

            case Ore.Starmetal:
                // a meteorite in its crater, its metal catching the light
                c.Ellipse(new Vector2(16, 6), 14, 5, P.Dirt);
                c.Ellipse(new Vector2(16, 6), 10, 3.4f, new[] { P.Dirt[0], P.Dirt[0], P.Dirt[1], P.Dirt[1] });
                c.Ellipse(new Vector2(16, 10), 7, 6, P.Starmetal);
                for (int i = 0; i < 6; i++) c.Put(11 + rng.Next(0, 10), 7 + rng.Next(0, 7), P.Starmetal[0]);
                c.Line(new Vector2(12, 13), new Vector2(17, 15), P.Starmetal[4], 1);
                c.Line(new Vector2(13, 12), new Vector2(16, 13), P.Gold[3], 1);
                OreSparkle(c, 22, 16, P.Gold[3], 1);
                break;

            case Ore.Heliolite:
                OreBoulders(c, v, Basalt);
                // glowing cracks, and embers drifting up
                for (int i = 0; i < 3; i++)
                    if (OreOnRock(c, rng, out var p))
                    {
                        var a = p + new Vector2(-4, 1); var m = p + new Vector2(rng.Next(-1, 2), -1); var e = p + new Vector2(4, 1);
                        c.Line(a, m, P.Heliolite[2], 1); c.Line(m, e, P.Heliolite[2], 1);
                        c.Put(m, P.Heliolite[3]);
                    }
                for (int i = 0; i < 4; i++) c.Put(7 + rng.Next(0, 18), 16 + rng.Next(0, 8), i % 2 == 0 ? P.Heliolite[3] : P.Fire[2]);
                glow = P.Heliolite[2];
                break;

            case Ore.Riftstone:
            {
                // shards hanging in the air round a bright tear in the world
                c.Line(new Vector2(16, 6), new Vector2(16, 22), P.Riftstone[3], 1);
                c.Line(new Vector2(16, 10), new Vector2(16, 18), P.White, 1);
                foreach (var (x, y, w, h) in new[] { (10f, 14f + v, 3.5f, 6f), (22f, 12f, 3f, 5f), (16f, 7f, 3f, 3.5f), (21f, 21f - v, 2f, 3f) })
                {
                    c.Polygon(new[] { new Vector2(x, y - h), new Vector2(x + w, y), new Vector2(x, y + h), new Vector2(x - w, y) },
                        l => l.x < x - 0.5f ? (l.y > y ? P.Riftstone[2] : P.Riftstone[1]) : (l.y > y ? P.Riftstone[1] : P.Riftstone[0]));
                    c.Put(x - 1, y + 1, P.Riftstone[3]);
                }
                glow = P.Riftstone[2];
                break;
            }

            case Ore.Stardust:
                // a heap of glittering dust from dead stars
                c.Ellipse(new Vector2(16, 7), 12, 5.5f, NightDust);
                c.Ellipse(new Vector2(15, 10), 7, 3.5f, NightDust);
                for (int i = 0; i < 16; i++)
                {
                    int x = rng.Next(5, 28), y = rng.Next(3, 14);
                    if (c.Get(x, y).a < 200) continue;
                    c.Put(x, y, i % 3 == 0 ? P.White : P.Stardust[3]);
                }
                OreSparkle(c, 10 + v * 3, 18, P.Stardust[3]);
                OreSparkle(c, 22 - v, 15, P.Stardust[3], 1);
                glow = P.Stardust[2];
                break;

            case Ore.Primordium:
            {
                // an egg-shaped stone of the first moment, banded with every colour
                foreach (var (x, y) in new[] { (7f, 5f), (25f, 5f), (11f, 3.5f), (21f, 3.5f) }) c.Ellipse(new Vector2(x, y), 2.2f, 1.6f, P.Stone);
                c.Ellipse(new Vector2(16, 12), 6.5f, 9, P.Primordium);
                var bands = new[] { P.Red[3], P.Gold[3], P.Ray[2], P.Neon[2], P.Aether[2] };
                for (int b = 0; b < bands.Length; b++)
                {
                    int y = 6 + b * 3 + v % 2;
                    for (int x = 10; x <= 22; x++)
                        if (c.Get(x, y).a >= 200 && (x + b) % 3 != 0) c.Put(x, y, bands[b]);
                }
                c.Ellipse(new Vector2(14.5f, 15), 1.5f, 2, new[] { P.Primordium[3], P.Primordium[3], P.White, P.White });
                glow = P.Primordium[2];
                break;
            }

            default:
                return false;
        }
        c.Outline(P.Ink, 0.55f);
        if (glow.HasValue) OreGlow(c, glow.Value);
        c.Shadow(16, 3, 13, 2.5f, 70);
        return true;
    }

    /// <summary>The usual boulder layouts, in a given rock.</summary>
    private static void OreBoulders(PixelCanvas c, int variant, Color32[] rock)
    {
        (float x, float y, float rx, float ry)[] stones = variant switch
        {
            0 => new (float, float, float, float)[] { (16, 10, 11, 8) },
            1 => new[] { (11f, 9f, 7.5f, 6f), (21f, 8f, 6.5f, 5f), (16f, 14f, 6.5f, 5.5f) },
            _ => new[] { (15f, 9f, 11.5f, 6f), (22f, 13f, 5.5f, 4.5f) },
        };
        foreach (var s in stones)
        {
            c.Ellipse(new Vector2(s.x, s.y), s.rx, s.ry, rock);
            c.Ellipse(new Vector2(s.x - s.rx * 0.15f, s.y + s.ry * 0.35f), s.rx * 0.6f, s.ry * 0.4f, new[] { rock[3], rock[3], rock[3], rock[4], rock[4] });
        }
    }

    /// <summary>A random point well inside what's drawn so far.</summary>
    private static bool OreOnRock(PixelCanvas c, System.Random rng, out Vector2 p)
    {
        for (int t = 0; t < 40; t++)
        {
            int x = rng.Next(4, 28), y = rng.Next(4, 20);
            if (c.Get(x, y).a >= 200 && c.Get(x + 2, y).a >= 200 && c.Get(x - 2, y).a >= 200 && c.Get(x, y + 2).a >= 200 && c.Get(x, y - 2).a >= 200)
            {
                p = new Vector2(x + 0.5f, y + 0.5f);
                return true;
            }
        }
        p = default;
        return false;
    }

    /// <summary>A chunky lump of ore with a dark rim and a bright glint.</summary>
    private static void OreNugget(PixelCanvas c, Vector2 p, Color32[] ramp, float r)
    {
        c.Ellipse(p, r + 0.6f, r * 0.85f + 0.5f, new[] { ramp[0], ramp[0], ramp[0], ramp[0] });
        c.Ellipse(p, r, r * 0.85f, ramp);
        c.Put(p.x - r * 0.4f, p.y + r * 0.4f, ramp[ramp.Length - 1]);
    }

    /// <summary>An angular lump of coal.</summary>
    private static void OreLump(PixelCanvas c, float x, float y, float r, System.Random rng)
    {
        var pts = new Vector2[6];
        for (int k = 0; k < pts.Length; k++)
        {
            float a = k * Mathf.PI * 2 / pts.Length + (float)rng.NextDouble() * 0.5f;
            float rr = r * (0.75f + (float)rng.NextDouble() * 0.35f);
            pts[k] = new Vector2(x + Mathf.Cos(a) * rr, y + Mathf.Sin(a) * rr * 0.8f);
        }
        c.Polygon(pts, l => l.x - x + (l.y - y) * -1.2f < -1.5f ? P.Coal[2] : l.x - x > 1.5f ? P.Coal[0] : P.Coal[1]);
        c.Put(x - r * 0.4f, y + r * 0.35f, P.Iron[3]);
        c.Put(x - r * 0.15f, y + r * 0.5f, P.Coal[3]);
    }

    /// <summary>A crystal prism growing up from (x, y): 'w' is its half-width, 'lean' its slant per pixel.</summary>
    private static void OreCrystal(PixelCanvas c, float x, float y, int h, int w, float lean, Color32[] r)
    {
        for (int d = 0; d < h; d++)
        {
            float t = d / (float)h;
            int half = t < 0.7f ? w : Mathf.Max(0, Mathf.RoundToInt(w * (1f - t) / 0.3f));
            float cx = x + lean * d;
            if (half == 0) { c.Put(cx, y + d, r[2]); continue; }
            for (int i = -half; i <= half; i++)
                c.Put(cx + i, y + d, i < 0 ? r[1] : i == 0 ? r[2] : r[Mathf.Min(3, r.Length - 1)]);
            c.Put(cx - half - 1, y + d, r[0]);
        }
        c.Put(x + lean * h, y + h, r[r.Length - 1]);
    }

    /// <summary>A four-pointed twinkle.</summary>
    private static void OreSparkle(PixelCanvas c, float x, float y, Color32 col, int arm = 2)
    {
        c.Put(x, y, P.White);
        for (int i = 1; i <= arm; i++)
        {
            c.Put(x + i, y, col); c.Put(x - i, y, col); c.Put(x, y + i, col); c.Put(x, y - i, col);
        }
    }

    /// <summary>A faint glow on the empty pixels round the deposit's base (rare ores stand out).</summary>
    private static void OreGlow(PixelCanvas c, Color32 col)
    {
        for (int y = 0; y < 12; y++)
            for (int x = 0; x < c.W; x++)
            {
                if (c.Get(x, y).a != 0) continue;
                float dx = (x + 0.5f - 16f) / 15f, dy = (y + 0.5f - 5f) / 5.5f;
                float d = dx * dx + dy * dy;
                if (d > 1f) continue;
                c.Put(x, y, new Color32(col.r, col.g, col.b, (byte)(d < 0.45f ? 135 : 80)));
            }
    }
}
