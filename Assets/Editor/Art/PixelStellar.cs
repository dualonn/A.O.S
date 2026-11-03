using System.Collections.Generic;
using UnityEngine;
using P = PixelPalette;
using K = BuildingKit;

/// <summary>
/// Stellar Age buildings in pixel art: white stone and gold, turning sun-discs, curved mirrors
/// aimed at the sky, lenses of heliolite and bottled sunfire glowing orange. The sun harnessed.
/// </summary>
public static partial class PlaceholderArt
{
    private static readonly Dictionary<string, (PixelDrawer draw, int frames, float fps)> StellarBuildingArt =
        new Dictionary<string, (PixelDrawer, int, float)>
        {
            { "HelionTower", (PxHelionTower, 4, 3f) }, { "Sunmarket", (PxSunmarket, 4, 4f) }, { "PaladinCitadel", (PxPaladinCitadel, 4, 4f) },
            { "HelionAcademy", (PxHelionAcademy, 4, 5f) }, { "SunwingAerie", (PxSunwingAerie, 4, 5f) }, { "DawnCathedral", (PxDawnCathedral, 4, 4f) },
            { "SunLance", (PxSunLance, 4, 6f) }, { "BurningMirror", (PxBurningMirror, 4, 4f) }, { "FlareCannon", (PxFlareCannon, 4, 6f) },
            { "SunForge", (PxSunForge, 4, 6f) }, { "RadiantWorks", (PxRadiantWorks, 4, 5f) }, { "OrbForge", (PxOrbForge, 4, 4f) },
            { "RadiantSanctum", (PxRadiantSanctum, 4, 4f) }, { "SolarDock", (PxSolarDock, 4, 4f) }, { "SolarCollector", (PxSolarCollector, 4, 5f) },
            { "HeliolitePit", (PxHeliolitePit, 4, 5f) }, { "SunfireRefinery", (PxSunfireRefinery, 4, 6f) },
        };

    private static Color32[] Sun => P.Solar;
    private static Color32[] White => P.Synth;

    /// <summary>A block of big white stones with a gold coping and a gold sun inlaid now and then.</summary>
    private static void WhiteStone(K k, int x0, int y0, int x1, int y1)
    {
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                int row = (y - y0) / 6;
                bool joint = (y - y0) % 6 == 0 || ((x - x0 + (row % 2) * 7) % 14) == 0;
                k.Px(x, y, joint ? White[0] : x < x0 + 2 ? White[2] : White[1]);
            }
        k.HLine(x0, x1, y1, Sun[2]);
        k.HLine(x0, x1, y1 - 1, Sun[1]);
        for (int x = x0 + 10; x < x1 - 4; x += 22)
        {
            int y = (y0 + y1) / 2;
            k.Px(x, y, Sun[3]); k.Px(x - 1, y, Sun[2]); k.Px(x + 1, y, Sun[2]); k.Px(x, y - 1, Sun[2]); k.Px(x, y + 1, Sun[2]);
        }
    }

    /// <summary>A sun-disc with turning rays.</summary>
    private static void SunDisc(K k, float x, float y, float r)
    {
        k.Ellipse(x, y, r, r, new[] { Sun[1], Sun[2], Sun[3], P.White });
        float a = k.T * Mathf.PI * 2f * 0.25f;
        for (int i = 0; i < 8; i++)
        {
            float t = a + i * Mathf.PI / 4f;
            k.Line(x + Mathf.Cos(t) * (r + 1), y + Mathf.Sin(t) * (r + 1), x + Mathf.Cos(t) * (r + 3), y + Mathf.Sin(t) * (r + 3), Sun[2]);
        }
    }

    /// <summary>Glass windows lit gold.</summary>
    private static void SunWindows(K k, int x0, int x1, int y, int h, int step)
    {
        for (int x = x0; x + 3 <= x1; x += step)
        {
            k.Rect(x, y, x + 3, y + h, Sun[2]);
            k.Rect(x, y + h - 1, x + 1, y + h, P.White);
        }
    }

    private static void PxHelionTower(K k)
    {
        // A white tower of many floors, a sun-disc crowning it that drinks the day.
        k.Ground(2, 2, 62, 6);
        int floors = 9 + (k.Level >= 1 ? 1 : 0) + (k.Level >= 2 ? 1 : 0), top = 4 + floors * 7;
        WhiteStone(k, 16, 4, 48, top);
        for (int f = 1; f < floors; f++) for (int i = 0; i < 3; i++) k.Rect(20 + i * 9, 6 + f * 7, 23 + i * 9, 9 + f * 7, (f + i + k.Frame) % 5 == 0 ? P.White : Sun[2]);
        k.Rect(12, top, 52, top + 2, Sun[2]);
        k.Polygon(new[] { new Vector2(18, top + 3), new Vector2(46, top + 3), new Vector2(32, top + 10) }, l => l.x < 28 ? (Color32?)White[2] : White[1]);
        SunDisc(k, 32, top + 18, 6);
        k.Door(28, 4, 8, 8, arched: true);
        k.Finish();
    }

    private static void PxSunmarket(K k)
    {
        // A golden-domed hall, little suns in bottles rising and falling over it.
        k.Ground(2, 2, 62, 6);
        WhiteStone(k, 6, 4, 58, 26);
        SunWindows(k, 10, 54, 12, 6, 9);
        k.Ellipse(32, 26, 20, 12, Sun, l => l.y >= 26);
        k.Door(27, 4, 10, 9, arched: true);
        float a = k.T * Mathf.PI * 2f;
        for (int i = 0; i < 4 + k.Level; i++)
        {
            float t = a * 0.5f + i * 1.3f;
            int x = Mathf.RoundToInt(32 + Mathf.Cos(t) * 22), y = Mathf.RoundToInt(48 + Mathf.Sin(t) * 4);
            k.Rect(x, y, x + 1, y + 2, P.Steel[3]); k.Px(x, y + 1, Sun[3]);
        }
        k.Finish();
    }

    private static void PxPaladinCitadel(K k)
    {
        // A white citadel with gold-capped towers, a burning sword over its gate.
        k.Ground(2, 2, 94, 30);
        WhiteStone(k, 6, 6, 70, 30);
        foreach (int x in new[] { 6, 60 }) { WhiteStone(k, x, 6, x + 10, 40); k.Polygon(new[] { new Vector2(x - 1, 41), new Vector2(x + 11, 41), new Vector2(x + 5, 48) }, _ => Sun[2]); }
        SunWindows(k, 20, 56, 16, 7, 9);
        k.Door(34, 6, 9, 12, arched: true);
        int flame = k.Frame % 2;
        k.Rect(82, 6, 83, 34, Sun[3]); k.HLine(78, 87, 10, Sun[1]);
        for (int y = 14; y < 34; y += 3) k.Px(84 + (y / 3 + flame) % 2, y, P.Fire[2]);
        k.Banner(70, 30, 12);
        k.Finish();
    }

    private static void PxHelionAcademy(K k)
    {
        // A tower with a sun-disc for a crown over a yard of targets burning in the light.
        k.Ground(2, 2, 94, 80, P.Dirt);
        WhiteStone(k, 10, 50, 40, 76);
        SunWindows(k, 15, 36, 58, 7, 7);
        SunDisc(k, 25, 84, 6);
        k.Door(22, 50, 7, 8, arched: true);
        for (int i = 0; i < 3 + Mathf.Min(k.Level, 1); i++)
        {
            int x = 54 + (i % 2) * 18, y = 18 + (i / 2) * 24;
            k.Ellipse(x, y, 5, 5, new[] { White[0], White[1], Sun[2], White[2] });
            k.VLine(x, y - 10, y - 5, White[0]);
            if ((k.Frame + i) % 3 == 0) k.Px(x + 1, y + 6, P.Fire[3]);
        }
        int f = k.Frame;
        k.Line(36 + f * 4, 46 - f * 6, 40 + f * 4, 44 - f * 6, Sun[3], 2);
        k.Finish();
    }

    private static void PxSunwingAerie(K k)
    {
        // A tall white perch; wings of light hang drying on its frames, a sunwing on its top.
        k.Ground(2, 2, 94, 30);
        WhiteStone(k, 30, 4, 60, 40);
        k.Rect(24, 40, 66, 43, Sun[2]);
        k.Door(41, 4, 8, 10, arched: true);
        foreach (int x in new[] { 8, 74 })
        {
            k.VLine(x, 4, 30, White[0]); k.VLine(x + 14, 4, 30, White[0]); k.HLine(x, x + 14, 30, Sun[1]);
            k.Polygon(new[] { new Vector2(x + 2, 28), new Vector2(x + 12, 28), new Vector2(x + 7, 12) }, _ => (k.Frame + x) % 2 == 0 ? Sun[3] : Sun[2]);
        }
        int up = k.Frame % 2 == 0 ? 8 : 0;
        k.Polygon(new[] { new Vector2(44, 48), new Vector2(32, 48 + up + 4), new Vector2(40, 46) }, _ => Sun[3]);
        k.Polygon(new[] { new Vector2(46, 48), new Vector2(58, 48 + up + 4), new Vector2(50, 46) }, _ => Sun[2]);
        k.Rect(44, 44, 46, 50, White[1]);
        k.Finish();
    }

    private static void PxDawnCathedral(K k)
    {
        // A cathedral of white glass glowing gold from within, a sunrise window over its door.
        k.Ground(4, 2, 92, 26, P.Dirt);
        WhiteStone(k, 8, 4, 80, 28);
        for (int i = 0; i < 6; i++) { int x = 12 + i * 11; k.Rect(x, 9, x + 4, 24, (i + k.Frame) % 4 == 0 ? P.White : Sun[2]); }
        k.Polygon(new[] { new Vector2(6, 29), new Vector2(82, 29), new Vector2(44, 46) }, l => l.y < 32 ? (Color32?)White[0] : White[1]);
        k.Ellipse(44, 34, 8, 6, Sun, l => l.y >= 30);
        k.VLine(44, 46, 58, White[2]); k.HLine(40, 48, 54, White[2]);
        k.Door(39, 4, 10, 10, arched: true);
        k.Finish();
    }

    private static void PxSunLance(K k)
    {
        // A white needle on a gold-trimmed base, a lens of heliolite at its tip.
        k.Ground(10, 2, 54, 8);
        WhiteStone(k, 18, 2, 46, 12);
        for (int y = 13; y < 48; y++) { int half = Mathf.Max(2, 7 - (y - 13) / 6); k.HLine(32 - half, 32 + half, y, y % 7 == 0 ? Sun[2] : (y < 30 ? White[1] : White[2])); }
        for (int y = 16; y < 46; y += 6) k.Px(32, y, (y / 6 + k.Frame) % 2 == 0 ? Sun[3] : Sun[1]);
        k.Ellipse(32, 52, 4, 4, P.Heliolite);
        k.Ellipse(32, 52, 2, 2, k.Frame % 2 == 0 ? new[] { P.White, P.White, P.White, P.White } : P.Heliolite);
        k.Finish();
    }

    private static void PxBurningMirror(K k)
    {
        // A great curved mirror on a turret, a robed mage at its foot aiming it at the sky.
        k.Ground(10, 2, 54, 8);
        WhiteStone(k, 22, 2, 42, 18);
        k.Rect(29, 18, 35, 26, Sun[1]);
        int tilt = k.Frame % 4 < 2 ? 0 : 1;
        k.Ellipse(32, 40 + tilt, 20, 9, new[] { White[1], White[2], White[3], P.White }, l => l.y <= 40 + tilt + 2);
        k.Ellipse(32, 40 + tilt, 20, 9, new[] { Sun[1], Sun[1], Sun[1], Sun[1] }, l => l.y > 40 + tilt + 2 && l.y < 40 + tilt + 4);
        k.Line(32, 40, 32, 58, P.Steel[2]);
        k.Ellipse(32, 59, 2.2f, 2.2f, new[] { Sun[2], Sun[3], P.White, P.White });
        if (k.Frame % 2 == 0) { k.Px(28, 62, Sun[3]); k.Px(36, 61, Sun[3]); }
        k.Door(28, 3, 8, 8, arched: true);
        k.Finish();
    }

    private static void PxFlareCannon(K k)
    {
        // A squat white tower, a ring of small mirrors round a captive flare burning on its top.
        k.Ground(2, 2, 62, 8);
        WhiteStone(k, 14, 2, 50, 24);
        k.Rect(10, 25, 54, 28, Sun[1]); k.HLine(10, 54, 28, Sun[2]);
        int pulse = k.Frame % 2 == 0 ? 2 : 0;
        k.Ellipse(32, 38, 7 + pulse, 7 + pulse, new[] { P.Fire[1], Sun[1], Sun[2], Sun[3] });
        k.Ellipse(32, 38, 3, 3, new[] { P.White, P.White, P.White, P.White });
        foreach (int x in new[] { 18, 46 }) { k.Rect(x - 2, 29, x + 2, 42, White[1]); k.Rect(x - 1, 40, x + 1, 46, White[3]); }
        k.Door(28, 3, 8, 8, arched: true);
        k.Finish();
    }

    private static void PxSunForge(K k)
    {
        // A forge under a tilted mirror that throws a beam of sunlight into the crucible.
        k.Ground(2, 2, 94, 40, P.Dirt);
        WhiteStone(k, 6, 8, 66, 30);
        SunWindows(k, 44, 62, 16, 6, 7);
        k.Door(28, 8, 10, 11, arched: true);
        k.Polygon(new[] { new Vector2(70, 34), new Vector2(92, 40), new Vector2(90, 46), new Vector2(68, 40) }, _ => White[2]);
        k.Line(80, 38, 80, 16, k.Frame % 2 == 0 ? Sun[3] : Sun[2], 2);
        k.Rect(74, 6, 88, 14, P.Iron[1]); k.Rect(76, 8, 86, 13, P.Heliolite[2]);
        for (int i = 0; i < 4; i++) k.Px(80 + k.R(-6, 8), 16 + k.R(0, 8), (i + k.Frame) % 2 == 0 ? P.White : Sun[3]);
        k.Finish(60, 40);
    }

    private static void PxRadiantWorks(K k)
    {
        // A glass hall; haloed constructs carry gears along a line.
        k.Ground(2, 2, 94, 40, P.Dirt);
        WhiteStone(k, 6, 8, 84, 26);
        for (int x = 8; x < 84; x += 8) k.Polygon(new[] { new Vector2(x, 27), new Vector2(x + 8, 27), new Vector2(x + 8, 33) }, _ => Sun[2]);
        k.Door(14, 8, 9, 10, arched: true);
        for (int i = 0; i < 3; i++)
        {
            int x = 34 + ((i * 20 + k.Frame * 5) % 56);
            k.Rect(x, 9, x + 3, 16, White[2]); k.Ellipse(x + 1.5f, 18, 2, 2, White);
            k.HLine(x - 1, x + 4, 21, Sun[3]);
            k.Ellipse(x + 6, 14, 2, 2, P.Brass);
        }
        k.Finish();
    }

    private static void PxOrbForge(K k)
    {
        // A domed forge, a sun orb hovering half-made in its open gate.
        k.Ground(2, 2, 94, 40, P.Dirt);
        WhiteStone(k, 4, 8, 56, 28);
        k.Ellipse(30, 28, 22, 12, new[] { White[1], White[2], White[3], P.White }, l => l.y >= 28);
        k.Door(22, 8, 14, 14, arched: true);
        int bob = k.Frame % 4 < 2 ? 0 : 1;
        k.Ellipse(76, 20 + bob, 11, 10, new[] { Sun[0], Sun[1], Sun[2], Sun[3] });
        k.Ellipse(76, 18 + bob, 4, 3, new[] { Sun[3], P.White, P.White, P.White });
        for (int i = 0; i < 24; i++)
        {
            float t = i / 24f * Mathf.PI * 2f + k.T * 2f;
            k.Px(Mathf.RoundToInt(76 + Mathf.Cos(t) * 14), Mathf.RoundToInt(20 + bob + Mathf.Sin(t) * 3), Mathf.Sin(t) < 0 ? Sun[3] : Sun[1]);
        }
        k.Finish();
    }

    private static void PxRadiantSanctum(K k)
    {
        // A ring of white pillars round a pool of light; an automaton rises from it, haloed.
        k.Ground(4, 2, 60, 12, P.Dirt);
        k.Ellipse(32, 10, 20, 5, new[] { Sun[1], Sun[2], Sun[3], P.White });
        for (int i = 0; i < 4; i++) { int x = 8 + i * 15; k.Rect(x, 4, x + 5, 28, White[1]); k.VLine(x, 4, 28, White[2]); k.HLine(x - 1, x + 6, 28, Sun[2]); }
        k.Rect(30, 12, 34, 22, White[2]); k.Ellipse(32, 24, 2.5f, 2.5f, White);
        k.Ellipse(32, 29, 4, 1, new[] { Sun[2], Sun[3], Sun[3], P.White }, l => Mathf.Abs(l.y - 29) > 0.3f || Mathf.Abs(l.x - 32) > 2.5f);
        if (k.Frame % 2 == 0) k.Px(31, 25, Sun[3]);
        k.Finish();
    }

    private static void PxSolarDock(K k)
    {
        // A tall white dock with golden mooring arms; a barque's sail of light rides above it.
        k.Ground(2, 2, 94, 70, P.Dirt);
        for (int x = 4; x < 92; x++) for (int y = 30; y < 40; y++) k.Px(x, y, (x + y) % 9 == 0 ? White[0] : White[1]);
        WhiteStone(k, 10, 40, 24, 66);
        SunDisc(k, 17, 72, 4);
        int bob = k.Frame % 4 < 2 ? 0 : 1;
        k.Line(24, 60, 40, 54 + bob, Sun[2]);
        k.Ellipse(62, 52 + bob, 20, 5, Sun, l => l.y <= 52 + bob);
        k.HLine(42, 82, 52 + bob, Sun[3]);
        k.VLine(58, 52 + bob, 78 + bob, Sun[0]);
        k.Polygon(new[] { new Vector2(59, 78 + bob), new Vector2(59, 55 + bob), new Vector2(76, 56 + bob) }, _ => (Color32)Sun[3]);
        k.Polygon(new[] { new Vector2(57, 76 + bob), new Vector2(57, 56 + bob), new Vector2(46, 57 + bob) }, _ => (Color32)Sun[2]);
        k.Finish();
    }

    private static void PxSolarCollector(K k)
    {
        // A field of mirrors turning with the sun, feeding a tower with a blazing top.
        k.Ground(2, 2, 94, 40, P.Dirt);
        WhiteStone(k, 40, 4, 54, 34);
        SunDisc(k, 47, 40, 5);
        int tilt = k.Frame % 2;
        foreach (var (x, y) in new[] { (8, 6), (20, 8), (8, 20), (20, 22), (66, 6), (78, 8), (66, 20), (78, 22) })
        {
            k.VLine(x + 4, y, y + 4, P.Steel[2]);
            k.Polygon(new[] { new Vector2(x, y + 5 + tilt), new Vector2(x + 9, y + 6), new Vector2(x + 9, y + 9), new Vector2(x, y + 8 + tilt) }, _ => White[3]);
            k.Px(x + 4, y + 7, Sun[3]);
        }
        k.Finish();
    }

    private static void PxHeliolitePit(K k)
    {
        // An open pit glowing orange from the heliolite in its walls; a crane lifts glowing slabs.
        k.Ground(2, 2, 94, 70, P.Dirt);
        k.Pit(10, 20, 60, 56);
        for (int i = 0; i < 6; i++)
        {
            int x = 18 + (i * 13) % 40, y = 26 + (i * 7) % 24;
            k.Polygon(new[] { new Vector2(x, y), new Vector2(x + 2, y + 6), new Vector2(x + 4, y) }, _ => P.Heliolite[2]);
            k.Px(x + 2, y + 1, (i + k.Frame) % 3 == 0 ? P.White : P.Heliolite[3]);
        }
        k.VLine(76, 20, 60, White[0]); k.HLine(46, 82, 60, White[1]); k.HLine(46, 82, 59, Sun[2]);
        int lift = (k.Frame % 4) * 3;
        k.VLine(52, 48 - lift, 59, P.Steel[2]); k.Rect(49, 44 - lift, 55, 47 - lift, P.Heliolite[2]);
        for (int i = 0; i < 2 + k.Level; i++) k.Ellipse(14 + i * 9, 8, 4, 3, P.Heliolite);
        k.Finish();
    }

    private static void PxSunfireRefinery(K k)
    {
        // A white hall under a great lens, melting heliolite into bottles of sunfire on a rack.
        k.Ground(2, 2, 94, 40, P.Dirt);
        WhiteStone(k, 6, 6, 52, 28);
        k.Ellipse(29, 34, 12, 4, new[] { White[2], White[3], P.White, P.White });
        k.Line(29, 30, 29, 18, k.Frame % 2 == 0 ? Sun[3] : Sun[2], 2);
        k.Door(22, 6, 9, 10, arched: true);
        k.Rect(62, 6, 88, 9, White[1]);
        for (int i = 0; i < 3 + Mathf.Min(k.Level, 1); i++)
        {
            int x = 66 + i * 6;
            k.Rect(x - 1, 10, x + 1, 15, P.Steel[3]);
            k.Px(x, 12, (i + k.Frame) % 2 == 0 ? P.White : Sun[3]);
        }
        k.VLine(62, 9, 30, White[2]); k.VLine(88, 9, 30, White[2]); k.HLine(60, 90, 30, Sun[2]);
        k.Ellipse(75, 24, 3, 3, P.Heliolite);
        k.Finish();
    }
}
