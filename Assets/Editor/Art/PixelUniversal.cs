using System.Collections.Generic;
using UnityEngine;
using P = PixelPalette;
using K = BuildingKit;

/// <summary>
/// Universal Age buildings in pixel art: pearl-white stone of the first moment with gold capstones
/// and rainbow light in its seams, clocks and hourglasses, living trees and seeds. Creation and time.
/// </summary>
public static partial class PlaceholderArt
{
    private static readonly Dictionary<string, (PixelDrawer draw, int frames, float fps)> UniversalBuildingArt =
        new Dictionary<string, (PixelDrawer, int, float)>
        {
            { "WorldTree", (PxWorldTree, 4, 3f) }, { "UniversalExchange", (PxUniversalExchange, 4, 4f) }, { "GenesisHall", (PxGenesisHall, 4, 4f) },
            { "LoomOfWorlds", (PxLoomOfWorlds, 4, 5f) }, { "SeraphSpire", (PxSeraphSpire, 4, 4f) }, { "Chronosanctum", (PxChronosanctum, 4, 3f) },
            { "GenesisLance", (PxGenesisLance, 4, 6f) }, { "FirstLightArray", (PxFirstLightArray, 4, 5f) }, { "ParadoxBattery", (PxParadoxBattery, 4, 6f) },
            { "CreationForge", (PxCreationForge, 4, 6f) }, { "Timeworks", (PxTimeworks, 4, 5f) }, { "WorldEngineCradle", (PxWorldEngineCradle, 4, 4f) },
            { "TitanVault", (PxTitanVault, 4, 3f) }, { "SeedYard", (PxSeedYard, 4, 4f) }, { "ZeroPointTap", (PxZeroPointTap, 4, 6f) },
            { "PrimordiumRift", (PxPrimordiumRift, 4, 5f) }, { "GenesisCrucible", (PxGenesisCrucible, 4, 6f) },
            { "GenesisEngine", (PxGenesisEngine, 4, 6f) },
        };

    private static Color32[] Pl => P.Pearl;

    /// <summary>A block of pearl-white stone: gold capstones, and rainbow light running along its seams.</summary>
    private static void PearlStone(K k, int x0, int y0, int x1, int y1)
    {
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                bool seam = (y - y0) % 9 == 0 || ((x - x0 + ((y - y0) / 9 % 2) * 7) % 14) == 0;
                Color32 c = x < x0 + 2 ? Pl[3] : Pl[2];
                if (seam) c = P.Prism[((x + y) / 5 + k.Frame) % 4];
                k.Px(x, y, c);
            }
        k.HLine(x0, x1, y1, P.Gold[3]);
        k.HLine(x0, x1, y1 - 1, P.Gold[2]);
    }

    /// <summary>A clock face with hands that turn as the frames go round.</summary>
    private static void Clock(K k, float cx, float cy, float r)
    {
        k.Ellipse(cx, cy, r + 1, r + 1, P.Chrono);
        k.Ellipse(cx, cy, r, r, new[] { Pl[1], Pl[2], Pl[3], P.White });
        for (int i = 0; i < 12; i++) k.Px(Mathf.RoundToInt(cx + Mathf.Cos(i * 0.5236f) * (r - 1)), Mathf.RoundToInt(cy + Mathf.Sin(i * 0.5236f) * (r - 1)), P.Chrono[0]);
        float a = -k.Frame * Mathf.PI / 2f + Mathf.PI / 2f;
        k.Line(Mathf.RoundToInt(cx), Mathf.RoundToInt(cy), Mathf.RoundToInt(cx + Mathf.Cos(a) * (r - 1.5f)), Mathf.RoundToInt(cy + Mathf.Sin(a) * (r - 1.5f)), P.Chrono[0]);
        k.Line(Mathf.RoundToInt(cx), Mathf.RoundToInt(cy), Mathf.RoundToInt(cx), Mathf.RoundToInt(cy + r * 0.5f), P.Chrono[0]);
        k.Px(Mathf.RoundToInt(cx), Mathf.RoundToInt(cy), P.Chrono[3]);
    }

    /// <summary>An hourglass whose sand runs (up as often as down).</summary>
    private static void Hourglass(K k, int cx, int y0, int h)
    {
        int half = h / 2;
        k.HLine(cx - half / 2 - 2, cx + half / 2 + 2, y0, P.Chrono[1]);
        k.HLine(cx - half / 2 - 2, cx + half / 2 + 2, y0 + h, P.Chrono[1]);
        for (int y = 1; y < h; y++)
        {
            int w = Mathf.Abs(y - half) * (half / 2) / Mathf.Max(1, half) + 1;
            k.HLine(cx - w, cx + w, y0 + y, P.Pearl[3]);
        }
        int sand = (k.Frame % 2 == 0) ? 1 : -1;
        for (int y = 2; y < half - 1; y++) k.HLine(cx - Mathf.Max(0, (half - y) / 3), cx + Mathf.Max(0, (half - y) / 3), y0 + (sand > 0 ? y : h - y), P.Gold[2]);
        k.VLine(cx, y0 + 2, y0 + h - 2, P.Gold[3]);
    }

    /// <summary>A rainbow arc (an arch of light).</summary>
    private static void RainbowArc(K k, float cx, float cy, float r)
    {
        for (int band = 0; band < 4; band++)
            for (int i = 0; i <= 40; i++)
            {
                float t = i / 40f * Mathf.PI;
                k.Px(Mathf.RoundToInt(cx + Mathf.Cos(t) * (r - band)), Mathf.RoundToInt(cy + Mathf.Sin(t) * (r - band)), P.Prism[band]);
            }
    }

    private static void PxWorldTree(K k)
    {
        k.Ground(2, 2, 62, 6);
        // a trunk the size of a tower, homes in it, and a canopy full of light
        int top = 60 + (k.Level >= 1 ? 6 : 0) + (k.Level >= 2 ? 6 : 0);
        var bark = P.Wood;
        k.Polygon(new[] { new Vector2(22, 4), new Vector2(42, 4), new Vector2(36, top), new Vector2(28, top) }, l => l.x < 29 ? (Color32?)bark[3] : bark[2]);
        for (int y = 10; y < top - 4; y += 9) { k.Rect(29, y, 31, y + 3, (y / 9 + k.Frame) % 3 == 0 ? P.Gold[3] : P.Gold[2]); k.Rect(33, y + 4, 35, y + 7, P.Gold[2]); }
        var leaf = P.Seed;
        k.Ellipse(32, top + 10, 24, 14, new[] { leaf[1], leaf[2], leaf[2], leaf[3] });
        k.Ellipse(22, top + 4, 12, 8, new[] { leaf[0], leaf[1], leaf[2], leaf[2] });
        k.Ellipse(44, top + 6, 12, 8, new[] { leaf[0], leaf[1], leaf[2], leaf[2] });
        for (int i = 0; i < 9; i++) k.Px(14 + (i * 7) % 36, top + 2 + (i * 5) % 20, (i + k.Frame) % 3 == 0 ? P.White : P.Prism[i % 4]);
        k.Line(18, 4, 24, 14, bark[1], 2); k.Line(46, 4, 40, 14, bark[1], 2);   // roots
        k.Door(29, 4, 6, 7);
        k.Finish();
    }

    private static void PxUniversalExchange(K k)
    {
        k.Ground(2, 2, 62, 6);
        PearlStone(k, 6, 4, 58, 26);
        for (int i = 0; i < 4; i++) k.VLine(12 + i * 13, 6, 24, P.Gold[2]);
        RainbowArc(k, 32, 27, 20);
        k.Ellipse(32, 27, 15, 9, new[] { Pl[1], Pl[2], Pl[3], P.White }, l => l.y >= 27);
        k.Door(28, 4, 8, 9);
        int f = k.Frame;
        for (int i = 0; i < 3 + k.Level; i++) { int x = 10 + i * 11, y = 46 + ((f + i) % 4) * 2; k.Ellipse(x, y, 2, 2, (i % 2 == 0) ? P.Gold : P.Pearl); }
        k.Finish();
    }

    private static void PxGenesisHall(K k)
    {
        k.Ground(2, 2, 94, 30);
        PearlStone(k, 6, 6, 72, 30);
        k.Polygon(new[] { new Vector2(4, 31), new Vector2(74, 31), new Vector2(39, 44) }, l => l.x < 36 ? (Color32?)Pl[3] : Pl[2]);
        k.HLine(4, 74, 31, P.Gold[3]);
        k.Door(33, 6, 12, 14);
        for (int i = 0; i < 4; i++) k.Rect(12 + i * 6, 18, 14 + i * 6, 25, Pl[1]);
        for (int i = 0; i < 3; i++) k.Rect(52 + i * 6, 18, 54 + i * 6, 25, Pl[1]);
        // the worldblade over the door, a little new world turning on its point
        for (int y = 46; y < 60; y++) { k.Px(39, y, Pl[3]); k.Px(40, y, Pl[2]); k.Px(41, y, P.Prism[(y + k.Frame) % 4]); }
        k.HLine(35, 45, 45, P.Gold[3]);
        k.Ellipse(40, 63, 3, 3, new[] { P.Water[1], P.Water[2], P.Seed[2], P.White });
        k.Banner(72, 30, 12);
        k.Finish();
    }

    private static void PxLoomOfWorlds(K k)
    {
        k.Ground(2, 2, 94, 80, P.Dirt);
        PearlStone(k, 12, 50, 40, 74);
        k.Door(22, 50, 7, 8);
        RainbowArc(k, 26, 75, 13);
        // a great loom in the yard pulling threads of light out of the air
        k.VLine(52, 10, 46, P.Wood[2]); k.VLine(84, 10, 46, P.Wood[2]); k.HLine(52, 84, 46, P.Wood[3]); k.HLine(52, 84, 10, P.Wood[1]);
        for (int i = 0; i < 8; i++)
        {
            int x = 55 + i * 4;
            k.VLine(x, 12, 44, P.Prism[i % 4]);
        }
        int shuttle = 14 + (k.Frame * 8) % 28;
        k.HLine(53, 83, shuttle, P.White);
        k.Rect(64 + (k.Frame % 2) * 6, shuttle - 1, 68 + (k.Frame % 2) * 6, shuttle + 1, P.Gold[3]);
        k.Finish();
    }

    private static void PxSeraphSpire(K k)
    {
        k.Ground(2, 2, 94, 30);
        PearlStone(k, 38, 4, 56, 56);
        k.Polygon(new[] { new Vector2(36, 57), new Vector2(58, 57), new Vector2(47, 74) }, l => l.x < 46 ? (Color32?)Pl[3] : Pl[2]);
        // halos ringing the spire, turning
        for (int ring = 0; ring < 3; ring++)
            for (int i = 0; i < 36; i++)
            {
                float t = i / 36f * Mathf.PI * 2f + k.Frame * 0.4f + ring;
                k.Px(Mathf.RoundToInt(47 + Mathf.Cos(t) * (16 - ring * 3)), Mathf.RoundToInt(24 + ring * 14 + Mathf.Sin(t) * 2.5f), i % 6 == 0 ? P.White : P.Gold[3]);
            }
        k.Door(43, 4, 8, 10);
        // wings folded at its foot
        for (int side = -1; side <= 1; side += 2)
            k.Polygon(new[] { new Vector2(47 + side * 10, 8), new Vector2(47 + side * 30, 26), new Vector2(47 + side * 24, 8) }, l => (Color32?)P.Prism[(int)(l.y / 5) % 4]);
        k.Finish();
    }

    private static void PxChronosanctum(K k)
    {
        k.Ground(4, 2, 92, 26, P.Dirt);
        PearlStone(k, 8, 4, 80, 24);
        k.Door(14, 4, 10, 12);
        // a great hourglass where the sand runs up as often as down
        Hourglass(k, 54, 25, 28);
        Clock(k, 30, 16, 5);
        k.Finish();
    }

    private static void PxGenesisLance(K k)
    {
        k.Ground(10, 2, 54, 8);
        PearlStone(k, 18, 2, 46, 12);
        k.Polygon(new[] { new Vector2(26, 13), new Vector2(38, 13), new Vector2(32, 48) }, l => l.x < 31 ? (Color32?)Pl[3] : Pl[2]);
        for (int y = 14; y < 46; y++) k.Px(32, y, P.Prism[(y / 3 + k.Frame) % 4]);
        bool flick = k.Frame % 2 == 0;
        k.Ellipse(32, 53, flick ? 5 : 4, flick ? 5 : 4, new[] { P.Prism[3], P.Prism[2], P.Prism[1], P.White });
        k.Finish();
    }

    private static void PxFirstLightArray(K k)
    {
        k.Ground(10, 2, 54, 8);
        PearlStone(k, 26, 2, 38, 28);
        // a prism the size of a house, splitting the first light into a rainbow
        k.Polygon(new[] { new Vector2(20, 30), new Vector2(44, 30), new Vector2(32, 54) }, l => l.x < 30 ? (Color32?)P.White : Pl[3]);
        k.Line(20, 30, 32, 54, P.Gold[3]); k.Line(44, 30, 32, 54, P.Gold[2]);
        int pulse = k.Frame % 4;
        for (int i = 0; i < 4; i++) k.Line(38, 40 + i, 54, 36 + i * 3 + pulse, P.Prism[i]);
        k.Line(6, 42, 26, 42, P.White);
        k.Finish();
    }

    private static void PxParadoxBattery(K k)
    {
        k.Ground(2, 2, 62, 8);
        PearlStone(k, 14, 2, 50, 22);
        k.Ellipse(32, 23, 18, 8, new[] { P.Chrono[0], P.Chrono[1], P.Chrono[2], P.Chrono[2] }, l => l.y >= 23);
        Clock(k, 32, 34, 7);
        // the shot already landed: its afterimage hangs in the air, before it's fired
        if (k.Frame % 2 == 0) for (int i = 0; i < 6; i++) k.Px(40 + i * 3, 40 + (i % 2) * 2, P.Prism[i % 4]);
        k.Door(28, 3, 8, 8);
        k.Finish();
    }

    private static void PxCreationForge(K k)
    {
        k.Ground(2, 2, 94, 40, P.Dirt);
        PearlStone(k, 6, 8, 64, 30);
        k.Door(26, 8, 10, 11);
        // a little star in a cradle, beaten into metal
        k.Rect(70, 6, 88, 10, P.Gold[1]);
        k.VLine(71, 10, 30, P.Gold[2]); k.VLine(87, 10, 30, P.Gold[2]);
        int f = k.Frame;
        k.Ellipse(79, 22, 5 + (f % 2), 5 + (f % 2), new[] { P.Gold[2], P.Gold[3], P.White, P.White });
        if (f % 2 == 0) for (int i = 0; i < 5; i++) k.Px(72 + i * 4, 30 + (i * 3) % 6, i % 2 == 0 ? P.White : P.Gold[3]);
        k.Finish(60, 40);
    }

    private static void PxTimeworks(K k)
    {
        k.Ground(2, 2, 94, 40, P.Dirt);
        PearlStone(k, 6, 8, 84, 26);
        k.Door(14, 8, 9, 10);
        Clock(k, 60, 34, 8);
        // gears turning fast in the windows, the hours running faster in here
        for (int i = 0; i < 3; i++)
        {
            int x = 32 + i * 14;
            k.Ellipse(x, 16, 3, 3, P.Chrono);
            for (int j = 0; j < 6; j++) k.Px(Mathf.RoundToInt(x + Mathf.Cos(j * 1.047f + k.Frame * (i + 1)) * 4), Mathf.RoundToInt(16 + Mathf.Sin(j * 1.047f + k.Frame * (i + 1)) * 4), P.Chrono[2]);
            k.Px(x, 16, P.Chrono[3]);
        }
        k.Finish();
    }

    private static void PxWorldEngineCradle(K k)
    {
        k.Ground(2, 2, 94, 40, P.Dirt);
        PearlStone(k, 4, 6, 40, 30);
        k.Door(16, 6, 10, 10);
        // a little ringed planet growing in a gold cradle, turning
        k.Rect(52, 6, 90, 9, P.Gold[1]);
        k.VLine(54, 9, 16, P.Gold[2]); k.VLine(88, 9, 16, P.Gold[2]);
        int bob = k.Frame % 4 < 2 ? 0 : 1;
        k.Ellipse(71, 26 + bob, 10, 10, new[] { P.Water[0], P.Water[1], P.Water[2], P.Water[3] });
        k.Ellipse(68 + k.Frame, 28 + bob, 4, 3, new[] { P.Seed[1], P.Seed[2], P.Seed[2], P.Seed[3] });
        k.Ellipse(75 - k.Frame, 22 + bob, 3, 2, new[] { P.Seed[1], P.Seed[2], P.Seed[2], P.Seed[3] });
        for (int i = 0; i < 60; i++)
        {
            float t = i / 60f * Mathf.PI * 2f;
            if (Mathf.Sin(t) < 0 && Mathf.Abs(Mathf.Cos(t)) < 0.55f) continue;   // behind the planet
            k.Px(Mathf.RoundToInt(71 + Mathf.Cos(t) * 18), Mathf.RoundToInt(26 + bob + Mathf.Sin(t) * 4), Pl[3]);
        }
        k.Finish();
    }

    private static void PxTitanVault(K k)
    {
        k.Ground(4, 2, 60, 12, P.Dirt);
        PearlStone(k, 6, 4, 58, 24);
        k.Door(28, 4, 8, 10);
        // a bronze titan waiting in stopped time, an hourglass for a heart
        k.Rect(24, 25, 40, 50, P.Chrono[1]);
        k.Rect(24, 25, 26, 50, P.Chrono[2]);
        k.Ellipse(32, 54, 5, 4, P.Chrono);
        k.Px(30, 55, P.Chrono[3]); k.Px(34, 55, P.Chrono[3]);
        Hourglass(k, 32, 30, 12);
        k.Finish();
    }

    private static void PxSeedYard(K k)
    {
        k.Ground(2, 2, 94, 70, P.Dirt);
        for (int x = 4; x < 92; x++) for (int y = 28; y < 36; y++) k.Px(x, y, (x + y) % 7 == 0 ? P.Seed[1] : P.Dirt[2]);
        PearlStone(k, 6, 36, 18, 66);
        // living ships growing on gold gantries like seeds on a stalk
        for (int i = 0; i < 4; i++) k.VLine(26 + i * 20, 36, 66, P.Gold[2]);
        k.HLine(24, 90, 66, P.Gold[3]);
        k.Ellipse(57, 50, 28, 7, new[] { P.Seed[0], P.Seed[1], P.Seed[2], P.Seed[2] });
        for (int i = 0; i < 3; i++) k.Line(36, 48 + i * 2, 78, 49 + i, (i + k.Frame) % 2 == 0 ? P.Seed[3] : P.Gold[3]);
        k.Ellipse(86, 50, 3, 3, new[] { P.Prism[0], P.Prism[1], P.Gold[3], P.White });
        k.Finish();
    }

    private static void PxZeroPointTap(K k)
    {
        k.Ground(2, 2, 94, 40, P.Dirt);
        foreach (var (x, y) in new[] { (20, 6), (76, 6), (20, 30), (76, 30) }) { k.Rect(x - 2, y, x + 2, y + 10, Pl[2]); k.Px(x, y + 10, P.Gold[3]); }
        // a gold ring round nothing at all, power pulled out of the empty middle
        for (int i = 0; i < 60; i++)
        {
            float t = i / 60f * Mathf.PI * 2f;
            k.Px(Mathf.RoundToInt(48 + Mathf.Cos(t) * 12), Mathf.RoundToInt(24 + Mathf.Sin(t) * 12), (i + k.Frame * 5) % 15 < 4 ? P.White : P.Gold[3]);
            k.Px(Mathf.RoundToInt(48 + Mathf.Cos(t) * 13), Mathf.RoundToInt(24 + Mathf.Sin(t) * 13), P.Gold[1]);
        }
        int f = k.Frame;
        for (int i = 0; i < 4; i++)
        {
            float a = i * 1.57f + f * 0.4f;
            float d = 10 - (f % 4) * 2.5f;
            k.Px(Mathf.RoundToInt(48 + Mathf.Cos(a) * d), Mathf.RoundToInt(24 + Mathf.Sin(a) * d), P.Prism[i]);
        }
        k.Finish();
    }

    private static void PxPrimordiumRift(K k)
    {
        k.Ground(2, 2, 94, 70, P.Dirt);
        k.Pit(10, 20, 60, 56);
        var pr = P.Primordium;
        // a crack in the world, first light welling up through it
        for (int i = 0; i < 9; i++) k.Line(14 + i * 5, 38 + ((i % 2) * 6) - 3, 19 + i * 5, 38 + (((i + 1) % 2) * 6) - 3, (i + k.Frame) % 3 == 0 ? pr[3] : pr[2], 2);
        for (int i = 0; i < 5; i++) k.Px(16 + i * 9, 42 + ((i * 3 + k.Frame * 2) % 10), i % 2 == 0 ? P.White : pr[3]);
        k.VLine(76, 20, 60, Pl[1]); k.HLine(46, 82, 60, Pl[2]); k.HLine(46, 82, 59, P.Gold[2]);
        int lift = (k.Frame % 4) * 3;
        k.VLine(52, 48 - lift, 59, P.Gold[2]); k.Rect(49, 44 - lift, 55, 47 - lift, pr[2]);
        for (int i = 0; i < 2 + k.Level; i++) k.Ellipse(14 + i * 9, 8, 4, 3, pr);
        k.Finish();
    }

    private static void PxGenesisCrucible(K k)
    {
        k.Ground(2, 2, 94, 40, P.Dirt);
        PearlStone(k, 6, 6, 52, 26);
        k.Door(22, 6, 9, 10);
        // a gold crucible with new matter glowing in it, every colour at once
        k.Ellipse(29, 32, 10, 5, new[] { P.Gold[0], P.Gold[1], P.Gold[2], P.Gold[3] });
        for (int i = 0; i < 4; i++) k.Px(24 + i * 3, 34 + ((i + k.Frame) % 2), P.Prism[i]);
        k.Rect(62, 6, 88, 9, Pl[1]);
        for (int i = 0; i < 3 + Mathf.Min(k.Level, 1); i++)
        {
            int x = 64 + i * 6;
            k.Rect(x, 10, x + 3, 16, Pl[2]); k.Px(x + 1, 13, P.Prism[(i + k.Frame) % 4]);
        }
        k.VLine(62, 9, 28, Pl[2]); k.VLine(88, 9, 28, Pl[2]); k.HLine(60, 90, 28, P.Gold[3]);
        k.Finish();
    }

    private static void PxGenesisEngine(K k)
    {
        // A megastructure: a vast gold armillary round a heart of new creation, making matter out of nothing.
        k.Ground(2, 2, 126, 30);
        PearlStone(k, 8, 4, 30, 34);
        PearlStone(k, 98, 4, 120, 34);
        k.Rect(30, 4, 98, 10, Pl[1]); k.HLine(30, 98, 10, P.Gold[3]);
        float spin = k.Frame * Mathf.PI / 8f;
        for (int ring = 0; ring < 3; ring++)
            for (int i = 0; i < 90; i++)
            {
                float t = i / 90f * Mathf.PI * 2f;
                float tilt = spin + ring * 1.05f;
                float x = Mathf.Cos(t) * 32, y = Mathf.Sin(t) * 32;
                float ry = y * Mathf.Cos(tilt);
                k.Px(Mathf.RoundToInt(64 + x * (ring == 2 ? Mathf.Cos(tilt) : 1f)), Mathf.RoundToInt(56 + (ring == 2 ? y : ry)), i % 10 == 0 ? P.White : P.Gold[2 + ring % 2]);
            }
        int pulse = k.Frame % 4;
        k.Ellipse(64, 56, 9 + pulse * 0.7f, 9 + pulse * 0.7f, new[] { P.Prism[3], P.Prism[2], P.Prism[1], P.White });
        for (int i = 0; i < 6; i++) k.Px(64 + Mathf.RoundToInt(Mathf.Cos(i + spin) * 18), 56 + Mathf.RoundToInt(Mathf.Sin(i + spin) * 18), P.Prism[i % 4]);
        k.Finish();
    }
}
