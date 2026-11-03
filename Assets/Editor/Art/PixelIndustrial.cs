using System.Collections.Generic;
using UnityEngine;
using P = PixelPalette;
using K = BuildingKit;

/// <summary>
/// Industrial Age buildings in pixel art: soot-dark brick, iron and glass, sawtooth roofs and tall
/// stacks, with the steampunk layer in brass pipework, turning gears and violet aether light.
/// </summary>
public static partial class PlaceholderArt
{
    private static readonly Dictionary<string, (PixelDrawer draw, int frames, float fps)> IndustrialBuildingArt =
        new Dictionary<string, (PixelDrawer, int, float)>
        {
            { "Terrace", (PxTerrace, 4, 3f) }, { "Exchange", (PxExchange, 4, 3f) }, { "GrenadierDepot", (PxGrenadierDepot, 4, 4f) },
            { "RifleRange", (PxRifleRange, 4, 4f) }, { "DragoonStables", (PxDragoonStables, 4, 3f) }, { "Infirmary", (PxInfirmary, 4, 3f) },
            { "SteamGunTurret", (PxSteamGunTurret, 4, 5f) }, { "BessemerWorks", (PxBessemerWorks, 4, 6f) }, { "EngineWorks", (PxEngineWorks, 4, 6f) },
            { "AetherLab", (PxAetherLab, 4, 6f) }, { "CoalMine", (PxCoalMine, 4, 5f) }, { "MachineWorks", (PxMachineWorks, 4, 6f) },
            { "AetherMine", (PxAetherMine, 4, 6f) }, { "AutomatonFoundry", (PxAutomatonFoundry, 4, 6f) },
        };

    /// <summary>Soot-darkened factory brick.</summary>
    private static readonly Color32[] SootBrick = { P.Hex("2e1a18"), P.Hex("5a2c24"), P.Hex("7e3c2e"), P.Hex("a25a44") };

    private static void SootWall(K k, int x0, int y0, int x1, int y1)
    {
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                int row = (y - y0) / 3, bx = (x - x0 + (row % 2) * 3) % 6;
                Color32 c = (y - y0) % 3 == 0 || bx == 0 ? SootBrick[0] : ((x / 6 + row) % 4 == 0 ? SootBrick[3] : SootBrick[2]);
                k.Px(x, y, c);
            }
        // soot creeping down from the top
        for (int x = x0; x <= x1; x++)
            for (int d = 0; d < 3 + (x * 7 % 5); d++)
                k.Darken(x, y1 - d, 0.25f);
    }

    /// <summary>A factory roof of glazed teeth facing the light.</summary>
    private static void SawtoothRoof(K k, int x0, int x1, int y, int teeth, int h)
    {
        int w = (x1 - x0) / teeth;
        for (int t = 0; t < teeth; t++)
        {
            int a = x0 + t * w;
            for (int i = 0; i <= w; i++)
            {
                int top = y + Mathf.RoundToInt(h * (1f - i / (float)w));
                for (int yy = y; yy <= top; yy++)
                    k.Px(a + i, yy, i < 3 ? (yy > y + 1 ? P.Water[3] : P.Iron[2]) : (yy == top ? P.Iron[3] : P.Iron[1]));
            }
            k.VLine(a, y, y + h, P.Water[2]);
        }
        k.HLine(x0, x1, y, P.Iron[0]);
    }

    /// <summary>A brass gear turning with the animation.</summary>
    private static void Cog(K k, float cx, float cy, float r, float turn, Color32[] ramp = null)
    {
        ramp ??= P.Brass;
        int teeth = Mathf.Max(6, Mathf.RoundToInt(r * 1.6f));
        for (int i = 0; i < teeth; i++)
        {
            float a = (i / (float)teeth + turn / teeth) * Mathf.PI * 2f;
            k.Px(Mathf.RoundToInt(cx + Mathf.Cos(a) * (r + 1)), Mathf.RoundToInt(cy + Mathf.Sin(a) * (r + 1)), ramp[1]);
        }
        k.Ellipse(cx, cy, r, r, ramp);
        k.Ellipse(cx, cy, r * 0.35f, r * 0.35f, new[] { P.Coal[1], P.Coal[2], ramp[0], ramp[1] });
    }

    private static void Pipe(K k, int x0, int y0, int x1, int y1)
    {
        k.Line(x0, y0, x1, y1, P.Brass[1], 2);
        k.Line(x0, y0 + 1, x1, y1 + 1, P.Brass[2]);
        k.Px(x1, y1, P.Brass[3]);
    }

    private static void AetherGlow(K k, int x0, int y0, int x1, int y1)
    {
        float pulse = 0.5f + 0.5f * k.Wave();
        k.Rect(x0, y0, x1, y1, P.Coal[1]);
        k.Rect(x0 + 1, y0 + 1, x1 - 1, y1 - 1, pulse > 0.5f ? P.Aether[3] : P.Aether[2]);
    }

    // ---------- Housing and money ----------

    private static void PxTerrace(K k)
    {
        // A row of narrow brick houses with bay windows and chimney pots.
        k.Ground(2, 2, 62, 6);
        int floors = 3 + (k.Level >= 1 ? 1 : 0), y = 4;
        SootWall(k, 4, y, 60, y + floors * 11);
        k.Rect(4, y, 60, y + 3, P.Stone[2]); k.HLine(4, 60, y + 3, P.Stone[4]);
        for (int i = 0; i < 2; i++) k.Door(10 + i * 30, y + 1, 6, 10, arched: true);
        for (int f = 0; f < floors; f++)
            for (int i = 0; i < 3; i++)
            {
                if (f == 0 && i != 1) continue;   // the doors take the ground floor
                k.Window(9 + i * 20, y + 5 + f * 11, 5, 6, f % 2 == 0 || i == 1);
            }
        int top = y + floors * 11;
        k.Roof(2, 62, top, 10, K.RoofType.Slate, 8);
        k.Chimney(10, top + 2, 5, 14, SootBrick); k.Chimney(46, top + 2, 5, 16, SootBrick);
        if (k.Level >= 2) { k.FlowerBox(8, 16, 8); k.FlowerBox(44, 16, 8); }
        k.Finish();
        k.Smoke(12, top + 18, 0.5f, 110); k.Smoke(48, top + 20, 0.5f, 110);
    }

    private static void PxExchange(K k)
    {
        // A grand exchange: a columned front, a glass-and-iron roof and a ticking clock.
        k.Rect(2, 2, 62, 4, P.Marble[1]); k.Rect(4, 5, 60, 7, P.Marble[2]);
        k.Wall(6, 8, 58, 34, K.Mat.Stone);
        k.Door(27, 8, 10, 14, arched: true);
        for (int i = 0; i < 5; i++) k.Column(8 + i * 12, 8, 25);
        k.Rect(4, 34, 60, 37, P.Marble[3]); k.HLine(4, 60, 34, P.Marble[1]);
        // the glazed iron vault
        for (int yy = 38; yy < 52; yy++)
        {
            int half = (int)(28 * Mathf.Sqrt(1f - (yy - 38) / 14f));
            for (int x = 32 - half; x <= 32 + half; x++)
                k.Px(x, yy, (x - 32) % 6 == 0 || yy % 4 == 0 ? P.Iron[1] : (x < 32 - half / 2 ? P.Water[3] : P.Water[2]));
        }
        // the clock
        k.Ellipse(32, 30, 4, 4, new[] { P.Brass[0], P.Linen[2], P.Linen[3], P.White });
        float a = k.T * Mathf.PI * 2f;
        k.Line(32, 30, 32 + Mathf.Cos(a) * 3, 30 + Mathf.Sin(a) * 3, P.Coal[1]);
        k.Line(32, 30, 32, 32, P.Coal[1]);
        for (int i = 0; i < 2 + k.Level; i++) k.HLine(44 + i * 4, 47 + i * 4, 3, P.Gold[2]);
        if (k.Level >= 1) k.Banner(0, 2, 30);
        k.Finish();
    }

    // ---------- Industry ----------

    private static void PxCoalMine(K k)
    {
        // A pithead: an iron headframe and wheel over the shaft, a winding house, a heap of coal.
        k.Ground(2, 2, 94, 40, P.Dirt);
        k.Ellipse(76, 10, 17, 8, P.Coal, l => l.y >= 4);   // the coal heap
        for (int i = 0; i < 8; i++) k.Px(62 + k.R(0, 28), 6 + k.R(0, 9), P.Iron[3]);
        SootWall(k, 4, 8, 40, 30);
        k.Roof(2, 42, 30, 10, K.RoofType.Slate, 6);
        k.Door(16, 9, 8, 12);
        k.Chimney(30, 34, 5, 26, SootBrick);
        // the headframe
        int hx = 56;
        k.Line(hx - 10, 4, hx, 62, P.Iron[2], 2); k.Line(hx + 10, 4, hx, 62, P.Iron[1], 2);
        k.HLine(hx - 7, hx + 7, 22, P.Iron[2]); k.HLine(hx - 4, hx + 4, 42, P.Iron[2]);
        Cog(k, hx, 62, 6, k.Frame, P.Iron);
        int by = 8 + Mathf.RoundToInt((k.Wave() + 1f) * 9f);
        k.VLine(hx + 5, by + 4, 62, P.Linen[1]);
        k.Rect(hx + 3, by, hx + 7, by + 4, P.Iron[1]);
        k.Rails(64, 92, 2);
        k.Cart(78, 4, P.Coal);
        if (k.Level >= 1) k.Cart(66, 4, P.Coal);
        if (k.Level >= 2) k.Banner(92, 6, 22);
        k.Finish();
        k.Smoke(32, 62, 0.9f, 150);
    }

    private static void PxBessemerWorks(K k)
    {
        // A steel mill: a tilting converter roaring sparks, a sawtooth shed and two stacks.
        k.Ground(2, 2, 94, 40, P.Dirt);
        SootWall(k, 6, 8, 60, 30);
        SawtoothRoof(k, 6, 60, 30, 4, 8);
        k.Door(24, 9, 12, 14, open: true);
        k.Rect(26, 9, 34, 16, P.Glow[2]);
        k.Chimney(12, 38, 6, 28, SootBrick); k.Chimney(48, 38, 6, 30 + k.Level * 4, SootBrick);
        // the converter: an egg of iron on trunnions, pouring white-hot steel
        float tilt = 0.5f + 0.5f * k.Wave();
        k.VLine(66, 4, 22, P.Iron[1]); k.VLine(90, 4, 22, P.Iron[1]);
        k.Ellipse(78, 22, 9, 12, P.Iron);
        k.Ellipse(78 + tilt * 3, 33, 3.5f, 2, P.Fire);
        for (int i = 0; i < 6; i++) k.Px(76 + k.R(0, 10), 36 + k.R(0, 10), i % 2 == 0 ? P.Glow[3] : P.Fire[3]);
        k.Rect(72, 4, 86, 7, P.Fire[2]); k.HLine(72, 86, 7, P.Glow[3]);   // a ladle of steel
        Pipe(k, 60, 20, 70, 20);
        if (k.Level >= 2) k.Banner(2, 4, 22);
        k.Finish();
        k.Smoke(15, 70, 1.0f, 160); k.Smoke(51, 74 + k.Level * 4, 1.0f, 160);
    }

    private static void PxMachineWorks(K k)
    {
        // A machine shop: big wheels and gears turning behind tall windows, crates of parts.
        k.Ground(2, 2, 94, 40, P.Dirt);
        SootWall(k, 6, 8, 88, 34);
        SawtoothRoof(k, 6, 88, 34, 6, 8);
        for (int i = 0; i < 4; i++) k.Window(12 + i * 20, 18, 7, 10, true);
        k.Door(44, 9, 10, 12);
        Cog(k, 22, 22, 6, k.Frame);
        Cog(k, 32, 26, 4, -k.Frame * 1.5f, P.Iron);
        Cog(k, 72, 22, 6, k.Frame);
        Pipe(k, 6, 30, 88, 30);
        k.Chimney(80, 42, 6, 22, SootBrick);
        for (int i = 0; i < 2 + k.Level; i++) { k.Crate(4 + i * 10, 2); k.Px(8 + i * 10, 5, P.Brass[2]); }
        if (k.Level >= 2) k.Banner(92, 4, 22);
        k.Finish();
        k.Smoke(83, 66, 0.8f, 150);
        if (k.Frame % 2 == 0) k.Px(46, 22, P.White);   // a spark at the lathe
    }

    private static void PxAetherMine(K k)
    {
        // A hillside drift lit violet: crystal clusters in the rock, a brass pump and glowing crates.
        k.Ground(8, 2, 88, 26, P.Dirt);
        k.Mound(50, 44, 46, 28, P.Rock);
        for (int i = 0; i < 6; i++)
        {
            int x = 16 + i * 12 + k.R(0, 5), y = 36 + k.R(0, 26);
            if (k.Get(x, y).a < 200) continue;
            k.Line(x, y, x + 1, y + 4, P.Aether[2], 2); k.Px(x + 1, y + 4, P.Aether[3]); k.Px(x - 1, y + 1, P.Aether[1]);
        }
        k.MineEntrance(36, 30, 20, 20, stone: true);
        AetherGlow(k, 40, 31, 52, 44);
        k.Rails(42, 50, 2);
        k.Cart(38, 6, P.Aether);
        // the pump: a brass boiler with a gauge
        k.Ellipse(76, 14, 7, 9, P.Brass);
        k.Ellipse(76, 16, 2.5f, 2.5f, new[] { P.Coal[1], P.Linen[2], P.Linen[3], P.White });
        Pipe(k, 70, 22, 58, 34);
        for (int i = 0; i < 2 + k.Level; i++) { k.Crate(8 + i * 9, 2); k.Px(11 + i * 9, 5, P.Aether[3]); }
        if (k.Level >= 2) k.Banner(4, 22, 20, P.Purple);
        k.Finish();
        k.Smoke(76, 26, 0.5f, 120);
    }

    // ---------- Military ----------

    private static void PxGrenadierDepot(K k)
    {
        // Brick barracks with an iron-bound magazine; grenade boxes stacked by the door, a drum and colours.
        k.Ground(2, 2, 94, 30);
        SootWall(k, 8, 10, 62, 34);
        k.Rect(8, 10, 62, 13, P.Stone[2]); k.HLine(8, 62, 13, P.Stone[4]);
        k.Door(30, 11, 10, 15, arched: true);
        for (int i = 0; i < 2; i++) k.Window(14 + i * 38, 22, 6, 6);
        k.Roof(5, 65, 34, 12, K.RoofType.Slate, 10);
        // the magazine: a squat iron-plated store
        k.Rect(68, 2, 92, 22, P.Iron[1]); k.HLine(68, 92, 22, P.Iron[3]);
        for (int x = 70; x < 92; x += 5) for (int y = 4; y < 22; y += 5) k.Px(x, y, P.Brass[2]);
        k.Rect(76, 3, 84, 13, P.Coal[1]);
        for (int i = 0; i < 2 + k.Level; i++) { k.Crate(44 + i * 8, 2); k.Ellipse(48 + i * 8, 8, 1.5f, 1.5f, P.Iron); }
        k.Ellipse(16, 5, 4, 3, new[] { P.Red[1], P.Red[2], P.Linen[2], P.Linen[3] });
        k.Banner(44, 46, 14);
        k.Finish();
    }

    private static void PxRifleRange(K k)
    {
        // A covered firing shed, sandbags at the line, iron targets far down the range.
        k.Ground(2, 2, 94, 80, P.Dirt);
        SootWall(k, 8, 56, 88, 68);
        k.Roof(4, 92, 68, 10, K.RoofType.Slate, 6);
        for (int i = 0; i < 4; i++) k.Window(14 + i * 20, 59, 6, 5);
        for (int i = 0; i < 3 + Mathf.Min(k.Level, 1); i++)
        {
            int x = 12 + i * 22;
            k.VLine(x + 3, 30, 40, P.Iron[1]);
            k.Ellipse(x + 3, 42, 4, 4, P.Iron);
            k.Ellipse(x + 3, 42, 1.5f, 1.5f, new[] { P.Red[1], P.Red[2], P.Red[3], P.White });
        }
        for (int x = 6; x <= 90; x += 6) k.Ellipse(x, 9, 3.5f, 2.6f, P.Linen);   // sandbags
        for (int x = 9; x <= 87; x += 6) k.Ellipse(x, 13, 3.5f, 2.6f, P.Linen);
        if (k.Level >= 1) k.Banner(2, 16, 22);
        if (k.Level >= 2) k.Banner(90, 30, 20);
        k.Finish();
        if (k.Frame == 1) { k.Px(40, 17, P.Glow[3]); k.Px(41, 17, P.White); }
        k.Smoke(44, 18, 0.5f, 80);
    }

    private static void PxDragoonStables(K k)
    {
        // Long brick stables with iron-framed doors, a hay loft, and a carbine rack.
        k.Ground(2, 2, 94, 30);
        SootWall(k, 6, 14, 90, 32);
        k.Roof(3, 93, 32, 12, K.RoofType.Slate, 10);
        k.Rect(42, 36, 54, 44, P.Wood[1]); k.Rect(44, 36, 52, 42, P.Straw[2]);   // loft door with hay
        for (int i = 0; i < 4; i++)
        {
            int x = 12 + i * 20;
            k.Rect(x, 15, x + 10, 30, P.Iron[1]); k.Rect(x + 1, 23, x + 9, 29, P.Coal[1]);
            if (i < 2 + k.Level)
            {
                var coat = i % 2 == 0 ? P.DarkHorse : P.Horse;
                k.Ellipse(x + 5, 26, 3, 3.5f, coat);
                k.Rect(x + 5, 22 + (k.Frame % 2), x + 8, 25 + (k.Frame % 2), coat[2]);
                k.Px(x + 6, 27, P.Ink);
            }
        }
        k.Hay(4, 2); k.Trough(70, 2, 12);
        if (k.Level >= 1) k.Banner(92, 4, 22);
        k.Finish();
    }

    private static void PxInfirmary(K k)
    {
        // A tall brick infirmary with rows of windows, a red-cross flag and a steam boiler for clean linen.
        k.Ground(4, 2, 92, 26, P.Dirt);
        SootWall(k, 6, 8, 74, 42);
        for (int f = 0; f < 3; f++) for (int i = 0; i < 4; i++) k.Window(12 + i * 16, 14 + f * 9, 5, 6, true);
        k.Door(36, 9, 8, 12, arched: true);
        k.Roof(3, 77, 42, 10, K.RoofType.Slate, 8);
        // linen lines and the boiler
        k.Ellipse(84, 12, 6, 8, P.Brass);
        Pipe(k, 78, 16, 74, 20);
        for (int i = 0; i < 2 + k.Level; i++) k.Rect(6 + i * 10, 3, 13 + i * 10, 6, P.Linen[3]);
        k.VLine(40, 52, 66, P.Wood[2]);
        k.Rect(41, 60, 50, 66, P.White); k.Rect(44, 61, 46, 65, P.Red[2]); k.Rect(42, 62, 49, 64, P.Red[2]);
        k.Finish();
        k.Smoke(84, 22, 0.5f, 110);
    }

    private static void PxSteamGunTurret(K k)
    {
        // An enclosed iron turret on a brick base: a riveted dome, a long gun, steam venting. No crew in the open.
        k.Ground(4, 2, 60, 8);
        SootWall(k, 8, 2, 56, 26);
        k.Rect(8, 2, 56, 5, P.Stone[2]); k.HLine(8, 56, 5, P.Stone[4]);
        k.Door(28, 3, 8, 11);
        k.Rect(6, 26, 58, 29, P.Iron[2]); k.HLine(6, 58, 29, P.Iron[3]);
        // the dome
        k.Ellipse(32, 30, 20, 16, P.Iron, l => l.y >= 30);
        for (int x = 16; x <= 48; x += 6) k.Px(x, 34, P.Brass[2]);
        for (int x = 20; x <= 44; x += 6) k.Px(x, 40, P.Brass[2]);
        k.Rect(28, 34, 36, 37, P.Coal[1]);   // the vision slit
        k.Px(30, 35, P.Aether[3]);
        // the gun barrel, recoiling as it fires
        int recoil = k.Frame == 1 ? 2 : 0;
        k.Rect(44 - recoil, 34, 62 - recoil, 37, P.Iron[1]); k.HLine(44 - recoil, 62 - recoil, 37, P.Iron[3]);
        k.Rect(60 - recoil, 33, 62 - recoil, 38, P.Brass[2]);
        k.Chimney(18, 44, 4, 6, P.Iron);
        if (k.Level >= 1) k.Banner(58, 2, 22);
        if (k.Level >= 2) k.HLine(8, 56, 20, P.Brass[2]);
        if (k.Level >= 3) for (int x = 10; x < 56; x += 8) k.Px(x, 12, P.Brass[3]);
        k.Finish();
        if (k.Frame == 1) { k.Px(64, 35, P.Glow[3]); k.Px(65, 36, P.White); }
        k.Smoke(20, 54, 0.5f, 110);
    }

    private static void PxEngineWorks(K k)
    {
        // A great engine shed with rails running out of its doors, a crawler half-built on the slip.
        k.Ground(2, 2, 94, 40, P.Dirt);
        SootWall(k, 4, 8, 66, 36);
        SawtoothRoof(k, 4, 66, 36, 4, 9);
        k.Door(20, 9, 26, 20, open: true);
        k.Rails(10, 92, 2);
        // a crawler hull on the slip outside
        k.Rect(68, 8, 92, 18, P.Iron[2]); k.HLine(68, 92, 18, P.Iron[3]);
        k.HLine(68, 92, 12, k.Team[2]);
        for (int i = 0; i < 4; i++) k.Ellipse(71 + i * 6, 6, 2.5f, 2.5f, P.Iron);
        if (k.Level >= 1) { k.Rect(84, 19, 94, 21, P.Iron[1]); }
        Cog(k, 34, 22, 5, k.Frame);
        k.Chimney(56, 44, 6, 26, SootBrick);
        if (k.Level >= 2) k.Banner(2, 4, 24);
        k.Finish();
        k.Smoke(59, 70, 1.0f, 160);
        if (k.Frame % 2 == 1) { k.Px(80, 20, P.Glow[3]); k.Px(81, 21, P.White); }   // a welder's spark
    }

    private static void PxAutomatonFoundry(K k)
    {
        // A brass-domed foundry: an automaton stands in the open bay, aether coils crackling overhead.
        k.Ground(2, 2, 94, 40, P.Dirt);
        SootWall(k, 6, 8, 70, 34);
        k.Rect(4, 34, 72, 37, P.Iron[2]);
        for (int yy = 38; yy < 52; yy++)
        {
            int half = (int)(22 * Mathf.Sqrt(1f - (yy - 38) / 14f));
            for (int x = 38 - half; x <= 38 + half; x++) k.Px(x, yy, x < 38 - half / 2 ? P.Brass[3] : P.Brass[2]);
        }
        k.Door(26, 9, 22, 22, open: true);
        // the automaton in the bay
        k.Rect(33, 10, 41, 24, P.Brass[2]); k.Rect(34, 24, 40, 30, P.Brass[3]);
        k.HLine(35, 39, 27, P.Aether[3]);
        k.VLine(31, 14, 22, P.Brass[1]); k.VLine(43, 14, 22, P.Brass[1]);
        // aether coils on the roof
        foreach (int x in new[] { 14, 62 })
        {
            k.VLine(x, 36, 50, P.Bronze[2]);
            for (int y = 40; y < 50; y += 2) k.HLine(x - 1, x + 1, y, P.Bronze[3]);
            k.Ellipse(x, 52, 2.5f, 2.5f, P.Aether);
        }
        if (k.Frame % 2 == 0) k.Line(16, 52, 60, 52, P.Aether[3]);
        Cog(k, 82, 16, 7, k.Frame);
        for (int i = 0; i < 1 + k.Level; i++) k.Crate(76 + (i % 2) * 9, 2 + (i / 2) * 6);
        if (k.Level >= 2) k.Banner(92, 4, 22);
        k.Finish();
    }

    private static void PxAetherLab(K k)
    {
        // A glass-and-iron laboratory tower crowned with a crackling aether coil.
        k.Ground(4, 2, 60, 12, P.Dirt);
        SootWall(k, 14, 2, 44, 34);
        k.Door(25, 3, 8, 12, arched: true);
        // the glass conservatory top
        for (int y = 34; y < 50; y++)
            for (int x = 16; x <= 42; x++)
                k.Px(x, y, (x - 16) % 6 == 0 || y % 5 == 0 ? P.Iron[1] : (x < 28 ? P.Water[3] : P.Water[2]));
        AetherGlow(k, 24, 38, 34, 46);
        k.VLine(29, 50, 62, P.Bronze[2]);
        for (int y = 52; y < 62; y += 2) k.HLine(27, 31, y, P.Bronze[3]);
        k.Ellipse(29, 64, 3, 3, P.Aether);
        if (k.Frame % 2 == 1)
        {
            k.Line(29, 64, 22, 70, P.Aether[3]); k.Line(22, 70, 18, 66, P.Aether[2]);
        }
        else
        {
            k.Line(29, 64, 37, 69, P.Aether[3]); k.Line(37, 69, 41, 74, P.Aether[2]);
        }
        Pipe(k, 44, 10, 54, 10);
        k.Ellipse(55, 8, 4, 5, P.Brass);
        for (int i = 0; i < 2 + k.Level; i++) k.Ellipse(6 + i * 4, 4, 1.4f, 2, new[] { P.Coal[2], P.Aether[2], P.White, P.White });
        if (k.Level >= 1) k.Banner(2, 12, 20, P.Purple);
        k.Finish();
    }
}
