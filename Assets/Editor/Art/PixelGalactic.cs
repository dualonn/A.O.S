using System.Collections.Generic;
using UnityEngine;
using P = PixelPalette;
using K = BuildingKit;

/// <summary>
/// Galactic Age buildings in pixel art: night-black stone with the stars showing through it,
/// silver trim, little galaxies turning over roofs, comets and gates. The galaxy as a home.
/// </summary>
public static partial class PlaceholderArt
{
    private static readonly Dictionary<string, (PixelDrawer draw, int frames, float fps)> GalacticBuildingArt =
        new Dictionary<string, (PixelDrawer, int, float)>
        {
            { "CelestialSpire", (PxCelestialSpire, 4, 3f) }, { "GalacticExchange", (PxGalacticExchange, 4, 4f) }, { "AegisHall", (PxAegisHall, 4, 4f) },
            { "StarweaverConclave", (PxStarweaverConclave, 4, 4f) }, { "CometHangar", (PxCometHangar, 4, 5f) }, { "TempleOfLife", (PxTempleOfLife, 4, 3f) },
            { "SingularityLance", (PxSingularityLance, 4, 6f) }, { "SupernovaArray", (PxSupernovaArray, 4, 5f) }, { "QuasarBattery", (PxQuasarBattery, 4, 6f) },
            { "StellarAnvil", (PxStellarAnvil, 4, 6f) }, { "CelestialWorks", (PxCelestialWorks, 4, 5f) }, { "GateFoundry", (PxGateFoundry, 4, 4f) },
            { "AvatarSanctum", (PxAvatarSanctum, 4, 3f) }, { "ArkYard", (PxArkYard, 4, 4f) }, { "DysonRelay", (PxDysonRelay, 4, 6f) },
            { "StardustCollector", (PxStardustCollector, 4, 5f) }, { "QuintessenceDistillery", (PxQuintessenceDistillery, 4, 6f) },
            { "StarGate", (PxStarGate, 4, 6f) },
        };

    private static Color32[] Cz => P.Cosmic;

    /// <summary>A block of night-black stone: stars twinkle in it, a silver rim along the top.</summary>
    private static void NightStone(K k, int x0, int y0, int x1, int y1)
    {
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                int h = ((x * 73) ^ (y * 151)) & 127;
                Color32 c = x < x0 + 2 ? Cz[2] : (y - y0) % 10 == 0 ? Cz[1] : Cz[0];
                if (h < 3) c = (h + k.Frame) % 3 == 0 ? P.White : Cz[3];
                k.Px(x, y, c);
            }
        k.HLine(x0, x1, y1, P.Steel[3]);
        k.HLine(x0, x1, y1 - 1, P.Steel[1]);
    }

    /// <summary>A little spiral galaxy turning slowly, two arms round a bright core.</summary>
    private static void Galaxy(K k, float cx, float cy, float r, float squash = 0.5f)
    {
        float spin = k.T * Mathf.PI * 2f * 0.25f;
        for (int arm = 0; arm < 2; arm++)
            for (int i = 0; i < 28; i++)
            {
                float t = i / 28f;
                float a = spin + arm * Mathf.PI + t * Mathf.PI * 2.2f;
                float d = r * t;
                var c = t < 0.35f ? P.White : t < 0.7f ? Cz[3] : Cz[2];
                k.Px(Mathf.RoundToInt(cx + Mathf.Cos(a) * d), Mathf.RoundToInt(cy + Mathf.Sin(a) * d * squash), c);
            }
        k.Ellipse(cx, cy, Mathf.Max(1.5f, r * 0.18f), Mathf.Max(1.2f, r * 0.14f), new[] { Cz[3], P.White, P.White, P.White });
    }

    /// <summary>A twinkling four-point star.</summary>
    private static void Twinkle(K k, int x, int y, int phase)
    {
        bool big = (k.Frame + phase) % 4 < 2;
        k.Px(x, y, P.White);
        k.Px(x - 1, y, Cz[3]); k.Px(x + 1, y, Cz[3]); k.Px(x, y - 1, Cz[3]); k.Px(x, y + 1, Cz[3]);
        if (big) { k.Px(x - 2, y, Cz[2]); k.Px(x + 2, y, Cz[2]); k.Px(x, y + 2, Cz[2]); k.Px(x, y - 2, Cz[2]); }
    }

    /// <summary>A ring of silver round a patch of deep space (a gate), its rim lights running round.</summary>
    private static void GateRingArt(K k, float cx, float cy, float rx, float ry)
    {
        k.Ellipse(cx, cy, rx - 1, ry - 1, new[] { Cz[0], Cz[0], Cz[1], Cz[2] });
        for (int i = 0; i < 5; i++) k.Px(Mathf.RoundToInt(cx + ((i * 7 + k.Frame) % 9 - 4) * rx / 6f), Mathf.RoundToInt(cy + ((i * 5) % 7 - 3) * ry / 5f), i % 2 == 0 ? P.White : Cz[3]);
        for (int i = 0; i < 72; i++)
        {
            float t = i / 72f * Mathf.PI * 2f;
            int x = Mathf.RoundToInt(cx + Mathf.Cos(t) * rx), y = Mathf.RoundToInt(cy + Mathf.Sin(t) * ry);
            k.Px(x, y, (i + k.Frame * 6) % 18 < 4 ? P.White : P.Steel[2]);
            k.Px(Mathf.RoundToInt(cx + Mathf.Cos(t) * (rx + 1)), Mathf.RoundToInt(cy + Mathf.Sin(t) * (ry + 1)), P.Steel[1]);
        }
    }

    private static void PxCelestialSpire(K k)
    {
        k.Ground(2, 2, 62, 6);
        int floors = 11 + (k.Level >= 1 ? 1 : 0) + (k.Level >= 2 ? 1 : 0), top = 4 + floors * 7;
        NightStone(k, 20, 4, 44, top);
        for (int f = 1; f < floors; f++) for (int i = 0; i < 3; i++) k.Rect(23 + i * 7, 6 + f * 7, 26 + i * 7, 9 + f * 7, (f + i + k.Frame) % 5 == 0 ? Cz[3] : Cz[2]);
        // a needle into orbit, a ring station round its tip
        k.Polygon(new[] { new Vector2(20, top + 1), new Vector2(44, top + 1), new Vector2(32, top + 24) }, l => l.x < 30 ? (Color32?)Cz[1] : Cz[0]);
        k.VLine(32, top + 24, top + 30, P.Steel[3]);
        k.Ellipse(32, top + 18, 14, 2, new[] { P.Steel[1], P.Steel[2], P.Steel[3], P.White }, l => Mathf.Abs(l.y - (top + 18)) > 0.6f || Mathf.Abs(l.x - 32) > 9);
        Twinkle(k, 32, top + 31, 0);
        k.Door(28, 4, 8, 8);
        k.Finish();
    }

    private static void PxGalacticExchange(K k)
    {
        k.Ground(2, 2, 62, 6);
        NightStone(k, 6, 4, 58, 26);
        // a dome painted with the whole galaxy
        k.Ellipse(32, 27, 22, 14, new[] { Cz[0], Cz[1], Cz[1], Cz[2] }, l => l.y >= 27);
        Galaxy(k, 32, 33, 12, 0.4f);
        for (int i = 0; i < 4; i++) k.VLine(12 + i * 13, 6, 24, P.Steel[2]);
        k.Door(28, 4, 8, 9);
        // coins of every world flicking in over it
        int f = k.Frame;
        for (int i = 0; i < 3 + k.Level; i++) { int x = 10 + i * 11, y = 44 + ((f + i) % 4) * 2; k.Ellipse(x, y, 2, 2, (i % 2 == 0) ? P.Gold : P.Brass); }
        k.Finish();
    }

    private static void PxAegisHall(K k)
    {
        k.Ground(2, 2, 94, 30);
        NightStone(k, 6, 6, 72, 30);
        k.Polygon(new[] { new Vector2(4, 31), new Vector2(74, 31), new Vector2(39, 44) }, l => l.x < 36 ? (Color32?)Cz[1] : Cz[0]);
        k.HLine(4, 74, 31, P.Steel[3]);
        k.Door(33, 6, 12, 14);
        for (int i = 0; i < 4; i++) k.Rect(12 + i * 6, 18, 14 + i * 6, 25, Cz[2]);
        for (int i = 0; i < 3; i++) k.Rect(52 + i * 6, 18, 54 + i * 6, 25, Cz[2]);
        // the singularity blade over the door: black, edged in violet, a point of collapsed star
        for (int y = 46; y < 60; y++) { k.Px(39, y, P.Coal[0]); k.Px(40, y, P.Coal[0]); k.Px(41, y, (y + k.Frame) % 3 == 0 ? P.White : Cz[3]); }
        k.HLine(35, 45, 45, P.Steel[3]);
        k.Ellipse(40, 62, 2, 2, new[] { P.Coal[0], P.Coal[0], Cz[2], Cz[3] });
        k.Banner(72, 30, 12);
        k.Finish();
    }

    private static void PxStarweaverConclave(K k)
    {
        k.Ground(2, 2, 94, 80, P.Dirt);
        NightStone(k, 12, 50, 40, 74);
        k.Ellipse(26, 75, 15, 9, new[] { Cz[0], Cz[1], Cz[1], Cz[2] }, l => l.y >= 75);
        k.Door(22, 50, 7, 8);
        Galaxy(k, 26, 92, 10);
        // students in the yard, each with a little galaxy turning over their staff
        for (int i = 0; i < 3 + Mathf.Min(k.Level, 1); i++)
        {
            int x = 54 + (i % 2) * 18, y = 16 + (i / 2) * 24;
            k.VLine(x, y, y + 10, P.Steel[3]);
            Galaxy(k, x, y + 15, 5);
        }
        k.Finish();
    }

    private static void PxCometHangar(K k)
    {
        // A launch ring with a captive comet hanging in it, its tail streaming out behind.
        k.Ground(2, 2, 94, 30);
        NightStone(k, 4, 4, 20, 46);
        NightStone(k, 74, 4, 90, 46);
        k.HLine(20, 74, 44, P.Steel[2]); k.HLine(20, 74, 45, P.Steel[3]);
        var cm = P.Comet;
        int bob = k.Frame % 4 < 2 ? 0 : 1;
        for (int i = 0; i < 6; i++)
            k.Ellipse(58 - i * 6, 28 + bob + i, 6 - i * 0.7f, 4 - i * 0.4f, new[] { cm[0], cm[1], cm[2], cm[3] });
        k.Ellipse(60, 28 + bob, 6, 5, new[] { cm[1], cm[2], cm[3], P.White });
        if (k.Frame % 2 == 0) k.Px(26, 34 + bob, cm[3]);
        k.Finish();
    }

    private static void PxTempleOfLife(K k)
    {
        k.Ground(4, 2, 92, 26, P.Dirt);
        NightStone(k, 8, 4, 80, 22);
        k.Door(40, 4, 10, 12);
        // a great tree of starlight growing out of the roof
        var leaf = P.Leaf;
        k.Rect(43, 23, 47, 40, P.Steel[2]);
        k.Line(45, 34, 30, 44, P.Steel[2], 2); k.Line(45, 36, 60, 46, P.Steel[2], 2);
        k.Ellipse(45, 52, 22, 12, new[] { leaf[1], leaf[2], Cz[3], P.White });
        for (int i = 0; i < 8; i++) k.Px(28 + (i * 5) % 34, 46 + (i * 7) % 12, (i + k.Frame) % 3 == 0 ? P.White : Cz[3]);
        k.Finish();
    }

    private static void PxSingularityLance(K k)
    {
        k.Ground(10, 2, 54, 8);
        NightStone(k, 18, 2, 46, 12);
        k.Polygon(new[] { new Vector2(26, 13), new Vector2(38, 13), new Vector2(32, 48) }, l => l.x < 31 ? (Color32?)Cz[1] : Cz[0]);
        k.Line(32, 14, 32, 46, Cz[3]);
        // the point of collapsed star at its tip, light bending round it
        bool flick = k.Frame % 2 == 0;
        k.Ellipse(32, 53, flick ? 5 : 4, flick ? 5 : 4, new[] { Cz[2], Cz[3], Cz[3], P.White });
        k.Ellipse(32, 53, 2.5f, 2.5f, new[] { P.Coal[0], P.Coal[0], P.Coal[0], P.Coal[0] });
        k.Finish();
    }

    private static void PxSupernovaArray(K k)
    {
        k.Ground(10, 2, 54, 8);
        NightStone(k, 27, 2, 37, 32);
        // a ring of mirrors round a captured supernova
        for (int i = 0; i < 8; i++)
        {
            float t = i / 8f * Mathf.PI * 2f + k.T * 0.8f;
            int x = Mathf.RoundToInt(32 + Mathf.Cos(t) * 16), y = Mathf.RoundToInt(46 + Mathf.Sin(t) * 10);
            k.Rect(x - 1, y - 1, x + 1, y + 1, P.Steel[3]);
        }
        var sun = P.Solar;
        int pulse = k.Frame % 4;
        k.Ellipse(32, 46, 6 + pulse * 0.6f, 6 + pulse * 0.6f, new[] { sun[1], sun[2], sun[3], P.White });
        k.Finish();
    }

    private static void PxQuasarBattery(K k)
    {
        k.Ground(2, 2, 62, 8);
        NightStone(k, 14, 2, 50, 22);
        k.Ellipse(32, 23, 18, 8, new[] { Cz[0], Cz[1], Cz[1], Cz[2] }, l => l.y >= 23);
        // a quasar's twin beams spinning on top
        k.Ellipse(32, 32, 4, 3, new[] { Cz[2], Cz[3], P.White, P.White });
        float a = k.Frame * Mathf.PI / 4f;
        for (int s = -1; s <= 1; s += 2)
            for (int i = 3; i < 14; i++)
                k.Px(Mathf.RoundToInt(32 + Mathf.Cos(a) * i * s), Mathf.RoundToInt(32 + Mathf.Sin(a) * i * s * 0.6f + i * 0.4f), i < 9 ? P.White : Cz[3]);
        k.Door(28, 3, 8, 8);
        k.Finish();
    }

    private static void PxStellarAnvil(K k)
    {
        k.Ground(2, 2, 94, 40, P.Dirt);
        NightStone(k, 6, 8, 64, 30);
        k.Door(26, 8, 10, 11);
        // an anvil of neutron-star matter, sparks of starlight flying off every blow
        k.Rect(70, 6, 88, 10, P.Steel[1]);
        k.Polygon(new[] { new Vector2(68, 11), new Vector2(90, 11), new Vector2(86, 18), new Vector2(72, 18) }, l => l.y > 16 ? (Color32?)Cz[2] : Cz[0]);
        int f = k.Frame;
        int hammer = f % 2 == 0 ? 20 : 26;
        k.Rect(77, hammer, 81, hammer + 4, P.Steel[3]); k.VLine(79, hammer + 4, hammer + 12, P.Wood[2]);
        if (f % 2 == 0) for (int i = 0; i < 4; i++) k.Px(72 + i * 5, 20 + (i * 3) % 6, i % 2 == 0 ? P.White : Cz[3]);
        k.Finish(60, 40);
    }

    private static void PxCelestialWorks(K k)
    {
        k.Ground(2, 2, 94, 40, P.Dirt);
        NightStone(k, 6, 8, 84, 26);
        k.Door(14, 8, 9, 10);
        // workers of living starlight carrying gears along the hall
        for (int i = 0; i < 3; i++)
        {
            int x = 34 + ((i * 20 + k.Frame * 5) % 46);
            k.Ellipse(x, 14, 2, 4, new[] { Cz[2], Cz[3], P.White, P.White });
            k.Px(x, 19, P.White);
            k.Ellipse(x + 5, 15, 2, 2, P.Brass);
        }
        Galaxy(k, 70, 34, 7, 0.45f);
        k.Finish();
    }

    private static void PxGateFoundry(K k)
    {
        k.Ground(2, 2, 94, 40, P.Dirt);
        NightStone(k, 4, 6, 40, 30);
        k.Door(16, 6, 10, 10);
        // a gate ring being raised in a cradle, hovering a little over it
        k.Rect(52, 6, 90, 9, P.Steel[1]);
        k.VLine(54, 9, 18, P.Steel[2]); k.VLine(88, 9, 18, P.Steel[2]);
        int bob = k.Frame % 4 < 2 ? 0 : 1;
        GateRingArt(k, 71, 26 + bob, 9, 13);
        k.Finish();
    }

    private static void PxAvatarSanctum(K k)
    {
        k.Ground(4, 2, 60, 12, P.Dirt);
        NightStone(k, 6, 4, 58, 24);
        k.Door(28, 4, 8, 10);
        // open to the night: an avatar of the sky stepping down, crowned with five stars
        int bob = k.Frame % 4 < 2 ? 0 : 1;
        k.Ellipse(32, 36 + bob, 5, 9, new[] { Cz[0], Cz[1], Cz[1], Cz[2] });
        k.Ellipse(32, 48 + bob, 3.5f, 3.5f, new[] { Cz[0], Cz[1], Cz[2], Cz[3] });
        for (int i = 0; i < 5; i++) k.Px(28 + i * 2, 53 + bob + (i % 2), P.White);
        for (int i = 0; i < 4; i++) k.Px(30 + (i * 3) % 5, 32 + bob + i * 2, Cz[3]);
        Twinkle(k, 12, 40, 0); Twinkle(k, 52, 44, 2);
        k.Finish();
    }

    private static void PxArkYard(K k)
    {
        k.Ground(2, 2, 94, 70, P.Dirt);
        for (int x = 4; x < 92; x++) for (int y = 28; y < 36; y++) k.Px(x, y, (x + y) % 9 == 0 ? Cz[1] : Cz[0]);
        NightStone(k, 6, 36, 18, 66);
        // gantries round an ark the size of a city
        for (int i = 0; i < 4; i++) k.VLine(26 + i * 20, 36, 66, P.Steel[2]);
        k.HLine(24, 90, 66, P.Steel[3]);
        k.Rect(28, 44, 86, 54, P.Steel[1]);
        k.HLine(28, 86, 54, P.Steel[3]);
        for (int i = 0; i < 9; i++) k.Px(31 + i * 6, 49, (i + k.Frame) % 3 == 0 ? P.White : Cz[3]);
        foreach (int tx in new[] { 36, 52, 66 }) k.Rect(tx, 55, tx + 4, 60, P.Steel[2]);
        if (k.Frame % 2 == 0) k.Px(26 + (k.Frame * 9) % 60, 66, P.White);   // a welding spark
        k.Finish();
    }

    private static void PxDysonRelay(K k)
    {
        k.Ground(2, 2, 94, 40, P.Dirt);
        NightStone(k, 34, 6, 62, 18);
        // a great dish catching a beam from the mirrors round the sun
        k.Ellipse(48, 30, 18, 7, new[] { P.Steel[1], P.Steel[2], P.Steel[3], P.White }, l => l.y <= 30);
        k.VLine(48, 18, 24, P.Steel[2]);
        var sun = P.Solar;
        for (int y = 31; y < 64; y++) if ((y + k.Frame * 3) % 4 != 0) k.Px(48 + (y - 31) / 6, y, y % 3 == 0 ? P.White : sun[3]);
        for (int i = 0; i < 5; i++) k.Px(56 + i * 7, 60 + (i % 2) * 3, (i + k.Frame) % 2 == 0 ? sun[3] : P.Steel[3]);
        k.Finish();
    }

    private static void PxStardustCollector(K k)
    {
        k.Ground(2, 2, 94, 70, P.Dirt);
        // nets and funnels catching dust of dead stars as it falls
        var sd = P.Stardust;
        for (int i = 0; i < 3; i++)
        {
            int x = 16 + i * 26;
            k.VLine(x, 10, 44, P.Steel[2]); k.VLine(x + 16, 10, 44, P.Steel[2]);
            for (int y = 30; y < 44; y += 3) k.HLine(x, x + 16, y, sd[1]);
            k.Polygon(new[] { new Vector2(x + 2, 28), new Vector2(x + 14, 28), new Vector2(x + 8, 18) }, l => (Color32?)P.Steel[1]);
            for (int j = 0; j < 4; j++) k.Px(x + 3 + (j * 4 + k.Frame) % 12, 46 + ((j * 7 + k.Frame * 3) % 18), j % 2 == 0 ? sd[3] : P.White);
        }
        for (int i = 0; i < 2 + k.Level; i++) k.Ellipse(14 + i * 9, 6, 4, 2.5f, sd);
        k.Finish();
    }

    private static void PxQuintessenceDistillery(K k)
    {
        k.Ground(2, 2, 94, 40, P.Dirt);
        NightStone(k, 6, 6, 52, 26);
        k.Door(22, 6, 9, 10);
        // a still of crystal with a little galaxy condensing inside it
        k.Ellipse(29, 34, 9, 6, new[] { Cz[0], Cz[1], Cz[2], Cz[3] });
        Galaxy(k, 29, 34, 6, 0.6f);
        k.Rect(62, 6, 88, 9, P.Steel[1]);
        for (int i = 0; i < 3 + Mathf.Min(k.Level, 1); i++)
        {
            int x = 64 + i * 6;
            k.Rect(x, 10, x + 3, 16, Cz[1]); k.Px(x + 1, 13, (i + k.Frame) % 2 == 0 ? P.White : Cz[3]);
        }
        k.VLine(62, 9, 28, P.Steel[2]); k.VLine(88, 9, 28, P.Steel[2]); k.HLine(60, 90, 28, P.Steel[3]);
        k.Finish();
    }

    private static void PxStarGate(K k)
    {
        // A megastructure: a vast silver ring on two pylons, another world's sky showing through it.
        k.Ground(2, 2, 126, 30);
        NightStone(k, 8, 4, 30, 40);
        NightStone(k, 98, 4, 120, 40);
        k.Rect(30, 4, 98, 10, P.Steel[1]); k.HLine(30, 98, 10, P.Steel[3]);
        GateRingArt(k, 64, 60, 34, 44);
        // the open gate: a shimmer of light crossing it
        int f = k.Frame;
        for (int i = 0; i < 6; i++) k.HLine(46 + (i * 5 + f * 3) % 12, 82 - (i * 5 + f * 3) % 12, 34 + i * 10, Cz[2]);
        Twinkle(k, 64, 106, 0);
        k.Finish();
    }
}
