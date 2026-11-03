using System.Collections.Generic;
using UnityEngine;
using P = PixelPalette;
using K = BuildingKit;

/// <summary>
/// Interstellar Age buildings in pixel art: dark slate with seams of teal phase-light, stones
/// floating in slow orbits, rings of bent space and doorways onto other skies. Space folded.
/// </summary>
public static partial class PlaceholderArt
{
    private static readonly Dictionary<string, (PixelDrawer draw, int frames, float fps)> InterstellarBuildingArt =
        new Dictionary<string, (PixelDrawer, int, float)>
        {
            { "RiftSpire", (PxRiftSpire, 4, 3f) }, { "FoldExchange", (PxFoldExchange, 4, 4f) }, { "RiftbladeHall", (PxRiftbladeHall, 4, 4f) },
            { "GravityAcademy", (PxGravityAcademy, 4, 4f) }, { "LeviathanRoost", (PxLeviathanRoost, 4, 3f) }, { "TempleOfPaths", (PxTempleOfPaths, 4, 4f) },
            { "PhaseLance", (PxPhaseLance, 4, 6f) }, { "GravityLens", (PxGravityLens, 4, 4f) }, { "RiftMortar", (PxRiftMortar, 4, 6f) },
            { "FoldForge", (PxFoldForge, 4, 6f) }, { "PrismWorks", (PxPrismWorks, 4, 5f) }, { "PyramidForge", (PxPyramidForge, 4, 4f) },
            { "PrismVault", (PxPrismVault, 4, 4f) }, { "FoldYard", (PxFoldYard, 4, 4f) }, { "RiftTap", (PxRiftTap, 4, 6f) },
            { "RiftstoneQuarry", (PxRiftstoneQuarry, 4, 5f) }, { "WarpCellPlant", (PxWarpCellPlant, 4, 6f) },
        };

    private static Color32[] Ph => P.Phase;
    private static readonly Color32[] DarkSlate = { P.Hex("14161e"), P.Hex("22263a"), P.Hex("323850"), P.Hex("4a5270") };

    /// <summary>A block of dark slate with teal light running in its seams, pulsing.</summary>
    private static void PhaseSlate(K k, int x0, int y0, int x1, int y1)
    {
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                bool seam = (y - y0) % 8 == 0 || ((x - x0) % 16) == 0;
                k.Px(x, y, seam ? ((x + y + k.Frame * 3) % 9 < 3 ? Ph[3] : Ph[1]) : x < x0 + 2 ? DarkSlate[3] : DarkSlate[2]);
            }
        k.HLine(x0, x1, y1, Ph[2]);
    }

    /// <summary>A stone floating in a slow orbit round a point.</summary>
    private static void Orbiter(K k, float cx, float cy, float rx, float ry, float phase, int size)
    {
        float t = k.T * Mathf.PI * 2f + phase;
        float x = cx + Mathf.Cos(t) * rx, y = cy + Mathf.Sin(t) * ry;
        k.Ellipse(x, y, size, size * 0.8f, new[] { DarkSlate[1], DarkSlate[2], DarkSlate[3], Ph[3] });
    }

    /// <summary>A doorway onto another sky: a frame round a patch of stars.</summary>
    private static void SkyDoor(K k, int x, int y, int w, int h)
    {
        k.Rect(x - 1, y, x + w, y + h + 1, Ph[2]);
        k.Rect(x, y, x + w - 1, y + h, P.Hex("06081a"));
        for (int i = 0; i < 5; i++) k.Px(x + 1 + (i * 7 + k.Frame) % Mathf.Max(1, w - 2), y + 1 + (i * 5) % Mathf.Max(1, h - 1), i % 2 == 0 ? P.White : Ph[3]);
    }

    private static void PxRiftSpire(K k)
    {
        k.Ground(2, 2, 62, 6);
        int floors = 10 + (k.Level >= 1 ? 1 : 0) + (k.Level >= 2 ? 1 : 0), top = 4 + floors * 7;
        PhaseSlate(k, 18, 4, 46, top);
        for (int f = 1; f < floors; f++) for (int i = 0; i < 2; i++) SkyDoor(k, 22 + i * 12, 6 + f * 7, 6, 4);
        k.Polygon(new[] { new Vector2(16, top + 1), new Vector2(48, top + 1), new Vector2(32, top + 22) }, l => l.x < 30 ? (Color32?)DarkSlate[3] : DarkSlate[2]);
        k.Px(32, top + 23, Ph[3]);
        Orbiter(k, 32, top + 10, 22, 4, 0f, 2);
        Orbiter(k, 32, top - 20, 20, 3, 2.1f, 2);
        k.Door(28, 4, 8, 8);
        k.Finish();
    }

    private static void PxFoldExchange(K k)
    {
        k.Ground(2, 2, 62, 6);
        PhaseSlate(k, 6, 4, 58, 28);
        SkyDoor(k, 12, 10, 12, 12); SkyDoor(k, 40, 10, 12, 12);
        k.Door(28, 4, 8, 9);
        // crates appearing in the air over it, then landing
        int f = k.Frame;
        for (int i = 0; i < 3 + k.Level; i++) { int x = 10 + i * 12, y = 40 + ((f + i) % 4) * 3; k.Rect(x, y, x + 5, y + 4, (f + i) % 4 == 0 ? Ph[3] : P.Wood[2]); }
        k.Finish();
    }

    private static void PxRiftbladeHall(K k)
    {
        k.Ground(2, 2, 94, 30);
        PhaseSlate(k, 6, 6, 72, 30);
        SkyDoor(k, 34, 6, 10, 14);
        for (int i = 0; i < 3; i++) SkyDoor(k, 12 + i * 8, 16, 4, 8);
        for (int i = 0; i < 3; i++) SkyDoor(k, 52 + i * 6, 16, 3, 8);
        // a blade of broken light standing at the door
        for (int y = 8; y < 34; y += 3) k.VLine(82, y, y + 1, (y / 3 + k.Frame) % 2 == 0 ? Ph[3] : Ph[2]);
        k.HLine(78, 86, 10, Ph[1]);
        k.Banner(72, 30, 12);
        k.Finish();
    }

    private static void PxGravityAcademy(K k)
    {
        k.Ground(2, 2, 94, 80, P.Dirt);
        PhaseSlate(k, 12, 50, 38, 78);
        k.Door(22, 50, 7, 8);
        for (int i = 0; i < 4; i++) Orbiter(k, 25, 70, 22, 6, i * 1.57f, 3);
        // stones held up in the yard by students' staffs
        for (int i = 0; i < 3 + Mathf.Min(k.Level, 1); i++)
        {
            int x = 54 + (i % 2) * 18, y = 18 + (i / 2) * 24, bob = (k.Frame + i) % 4 < 2 ? 0 : 2;
            k.Ellipse(x, y + bob + 6, 4, 3, DarkSlate);
            k.Ellipse(x, y - 2, 6, 1.5f, new[] { Ph[0], Ph[1], Ph[2], Ph[3] });
        }
        k.Finish();
    }

    private static void PxLeviathanRoost(K k)
    {
        // A great frame of rings where a star whale rests, its glowing spots slowly pulsing.
        k.Ground(2, 2, 94, 30);
        PhaseSlate(k, 4, 4, 22, 46);
        PhaseSlate(k, 72, 4, 90, 46);
        k.Ellipse(47, 40, 30, 10, new[] { Ph[1], Ph[2], Ph[2], Ph[3] }, l => Vector2.Distance(l, new Vector2(47, 40)) > 0 && Mathf.Abs((l.x - 47) / 30f * (l.x - 47) / 30f + (l.y - 40) / 10f * (l.y - 40) / 10f - 1f) < 0.25f);
        var whale = P.Whale;
        int bob = k.Frame % 4 < 2 ? 0 : 1;
        k.Ellipse(47, 40 + bob, 20, 6, whale);
        k.Ellipse(49, 37 + bob, 14, 2, new[] { whale[2], whale[3], whale[3], P.White });
        k.Polygon(new[] { new Vector2(28, 40 + bob), new Vector2(20, 46 + bob), new Vector2(22, 40 + bob), new Vector2(20, 34 + bob) }, _ => whale[1]);
        for (int i = 0; i < 5; i++) k.Px(36 + i * 5, 43 + bob, (i + k.Frame) % 2 == 0 ? whale[3] : whale[2]);
        k.Finish();
    }

    private static void PxTempleOfPaths(K k)
    {
        k.Ground(4, 2, 92, 26, P.Dirt);
        PhaseSlate(k, 8, 4, 80, 30);
        // corridors folding into one another: nested doorways shrinking into the distance
        for (int i = 0; i < 4; i++) SkyDoor(k, 30 + i * 4, 6 + i * 2, 28 - i * 8, 20 - i * 4);
        k.Polygon(new[] { new Vector2(6, 31), new Vector2(82, 31), new Vector2(44, 44) }, l => l.y < 34 ? (Color32?)DarkSlate[1] : DarkSlate[2]);
        k.Px(44, 45, Ph[3]);
        k.Finish();
    }

    private static void PxPhaseLance(K k)
    {
        k.Ground(10, 2, 54, 8);
        PhaseSlate(k, 18, 2, 46, 12);
        bool there = k.Frame % 2 == 0;
        for (int y = 13; y < 48; y++) { int half = Mathf.Max(1, 6 - (y - 13) / 7); if (there || y % 3 != 0) k.HLine(32 - half, 32 + half, y, y % 6 == 0 ? Ph[3] : Ph[2]); }
        k.Ellipse(32, 52, 3, 3, new[] { Ph[2], Ph[3], P.White, P.White });
        k.Finish();
    }

    private static void PxGravityLens(K k)
    {
        k.Ground(10, 2, 54, 8);
        PhaseSlate(k, 26, 2, 38, 36);
        for (int i = 0; i < 48; i++)
        {
            float t = i / 48f * Mathf.PI * 2f;
            k.Px(Mathf.RoundToInt(32 + Mathf.Cos(t) * 14), Mathf.RoundToInt(48 + Mathf.Sin(t) * 10), (i + k.Frame * 4) % 12 < 4 ? Ph[3] : Ph[1]);
        }
        k.Ellipse(32, 48, 5, 4, new[] { P.Coal[0], P.Coal[0], Ph[0], Ph[1] });
        k.Finish();
    }

    private static void PxRiftMortar(K k)
    {
        k.Ground(2, 2, 62, 8);
        PhaseSlate(k, 14, 2, 50, 24);
        k.Ellipse(32, 25, 18, 10, new[] { DarkSlate[1], DarkSlate[2], DarkSlate[3], DarkSlate[3] }, l => l.y >= 25);
        // a crack in the dome with a rift showing through
        var r = P.Rift;
        k.Line(26, 26, 32, 34, r[3], 2); k.Line(32, 34, 38, 28, r[3], 2);
        if (k.Frame % 2 == 0) k.Ellipse(32, 31, 3, 2, new[] { r[2], r[3], P.White, P.White });
        k.Door(28, 3, 8, 8);
        k.Finish();
    }

    private static void PxFoldForge(K k)
    {
        k.Ground(2, 2, 94, 40, P.Dirt);
        PhaseSlate(k, 6, 8, 66, 30);
        k.Door(28, 8, 10, 11);
        // a bar of metal passing in and out of a fold, again and again
        int f = k.Frame;
        k.Rect(72, 6, 90, 10, DarkSlate[2]);
        k.Ellipse(78, 20, 5, 7, new[] { P.Coal[0], P.Coal[0], Ph[1], Ph[3] });
        k.Ellipse(86, 20, 5, 7, new[] { P.Coal[0], P.Coal[0], Ph[1], Ph[3] });
        k.Rect(78 + f * 2 - 2, 18, 78 + f * 2 + 4, 21, P.Steel[3]);
        k.Finish(60, 40);
    }

    private static void PxPrismWorks(K k)
    {
        k.Ground(2, 2, 94, 40, P.Dirt);
        PhaseSlate(k, 6, 8, 84, 26);
        k.Door(14, 8, 9, 10);
        for (int i = 0; i < 3; i++)
        {
            int x = 34 + ((i * 20 + k.Frame * 5) % 56);
            k.Polygon(new[] { new Vector2(x, 9), new Vector2(x + 4, 9), new Vector2(x + 5, 16), new Vector2(x + 2, 20), new Vector2(x - 1, 16) },
                l => l.x < x + 2 ? (Color32?)P.Riftstone[3] : P.Riftstone[2]);
            k.Ellipse(x + 7, 14, 2, 2, P.Brass);
        }
        k.Finish();
    }

    private static void PxPyramidForge(K k)
    {
        k.Ground(2, 2, 94, 40, P.Dirt);
        k.Polygon(new[] { new Vector2(4, 6), new Vector2(56, 6), new Vector2(30, 40) }, l => l.x < 24 ? (Color32?)DarkSlate[3] : DarkSlate[2]);
        for (int i = 1; i < 4; i++) k.HLine(4 + i * 6, 56 - i * 6, 6 + i * 8, Ph[1]);
        k.Door(24, 6, 12, 10);
        int bob = k.Frame % 4 < 2 ? 0 : 1;
        k.Polygon(new[] { new Vector2(66, 16 + bob), new Vector2(90, 16 + bob), new Vector2(78, 36 + bob) }, l => l.x < 76 ? (Color32?)P.Coal[1] : P.Coal[2]);
        k.Ellipse(78, 10, 12, 2, new[] { Ph[0], Ph[1], Ph[2], Ph[3] });
        k.Finish();
    }

    private static void PxPrismVault(K k)
    {
        k.Ground(4, 2, 60, 12, P.Dirt);
        PhaseSlate(k, 6, 4, 58, 30);
        for (int i = 0; i < 3; i++)
        {
            int x = 12 + i * 16;
            SkyDoor(k, x, 8, 9, 16);
            k.Polygon(new[] { new Vector2(x + 2, 10), new Vector2(x + 7, 10), new Vector2(x + 6, 20), new Vector2(x + 4, 23), new Vector2(x + 3, 20) },
                l => (Color32?)P.Riftstone[(i + k.Frame) % 2 == 0 ? 3 : 2]);
        }
        k.Finish();
    }

    private static void PxFoldYard(K k)
    {
        k.Ground(2, 2, 94, 70, P.Dirt);
        for (int x = 4; x < 92; x++) for (int y = 30; y < 40; y++) k.Px(x, y, (x + y) % 9 == 0 ? DarkSlate[1] : DarkSlate[2]);
        PhaseSlate(k, 10, 40, 24, 66);
        // a great ring standing on the apron, a cruiser's prow coming through it
        for (int i = 0; i < 60; i++)
        {
            float t = i / 60f * Mathf.PI * 2f;
            k.Px(Mathf.RoundToInt(62 + Mathf.Cos(t) * 10), Mathf.RoundToInt(56 + Mathf.Sin(t) * 16), (i + k.Frame * 5) % 15 < 5 ? Ph[3] : Ph[1]);
        }
        int slide = k.Frame * 2;
        k.Polygon(new[] { new Vector2(56 + slide, 52), new Vector2(76 + slide, 54), new Vector2(56 + slide, 60) }, l => (Color32?)P.Coal[2]);
        k.Finish();
    }

    private static void PxRiftTap(K k)
    {
        k.Ground(2, 2, 94, 40, P.Dirt);
        var r = P.Rift;
        foreach (var (x, y) in new[] { (20, 6), (76, 6), (20, 30), (76, 30) }) { k.Rect(x - 2, y, x + 2, y + 10, DarkSlate[2]); k.Px(x, y + 10, Ph[3]); }
        k.Ellipse(48, 22, 9, 12, new[] { r[0], r[1], r[2], r[3] });
        k.Ellipse(48, 22, 4, 7, new[] { P.Coal[0], P.Coal[0], r[1], r[2] });
        int f = k.Frame;
        foreach (var (x, y) in new[] { (20, 16), (76, 16), (20, 40), (76, 40) })
            k.Px(Mathf.RoundToInt(Mathf.Lerp(x, 48, (f % 4) / 4f)), Mathf.RoundToInt(Mathf.Lerp(y, 22, (f % 4) / 4f)), Ph[3]);
        k.Finish();
    }

    private static void PxRiftstoneQuarry(K k)
    {
        k.Ground(2, 2, 94, 70, P.Dirt);
        k.Pit(10, 20, 60, 56);
        for (int i = 0; i < 6; i++)
        {
            int x = 18 + (i * 13) % 40, y = 26 + (i * 7) % 24;
            if ((i + k.Frame) % 3 == 0) continue;   // flickering half out of the world
            k.Polygon(new[] { new Vector2(x, y), new Vector2(x + 2, y + 6), new Vector2(x + 4, y) }, _ => P.Riftstone[2]);
            k.Px(x + 2, y + 1, P.Riftstone[3]);
        }
        k.VLine(76, 20, 60, DarkSlate[1]); k.HLine(46, 82, 60, DarkSlate[2]); k.HLine(46, 82, 59, Ph[2]);
        int lift = (k.Frame % 4) * 3;
        k.VLine(52, 48 - lift, 59, P.Steel[2]); k.Rect(49, 44 - lift, 55, 47 - lift, P.Riftstone[2]);
        for (int i = 0; i < 2 + k.Level; i++) k.Ellipse(14 + i * 9, 8, 4, 3, P.Riftstone);
        k.Finish();
    }

    private static void PxWarpCellPlant(K k)
    {
        k.Ground(2, 2, 94, 40, P.Dirt);
        PhaseSlate(k, 6, 6, 52, 28);
        k.Door(22, 6, 9, 10);
        k.Ellipse(29, 34, 9, 4, new[] { P.Coal[0], P.Coal[0], Ph[1], Ph[3] });
        k.Rect(62, 6, 88, 9, DarkSlate[1]);
        for (int i = 0; i < 3 + Mathf.Min(k.Level, 1); i++)
        {
            int x = 64 + i * 6;
            k.Rect(x, 10, x + 3, 15, Ph[1]); k.Px(x + 1, 12, (i + k.Frame) % 2 == 0 ? P.Coal[0] : Ph[3]);
        }
        k.VLine(62, 9, 30, DarkSlate[2]); k.VLine(88, 9, 30, DarkSlate[2]); k.HLine(60, 90, 30, Ph[2]);
        k.Finish();
    }
}
