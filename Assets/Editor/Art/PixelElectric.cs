using System.Collections.Generic;
using UnityEngine;
using P = PixelPalette;
using K = BuildingKit;

/// <summary>
/// Electric Age buildings in pixel art, retro-futuristic (the 1950s and 60s look): chrome and turquoise,
/// boomerang roofs and starbursts, rocket fins, Tesla coils and the green glow of atomic power.
/// </summary>
public static partial class PlaceholderArt
{
    private static readonly Dictionary<string, (PixelDrawer draw, int frames, float fps)> ElectricBuildingArt =
        new Dictionary<string, (PixelDrawer, int, float)>
        {
            { "SkylineTower", (PxSkylineTower, 4, 3f) }, { "SkyBank", (PxSkyBank, 4, 3f) }, { "TrooperAcademy", (PxTrooperAcademy, 4, 4f) },
            { "BlasterRange", (PxBlasterRange, 4, 6f) }, { "HoverPool", (PxHoverPool, 4, 4f) }, { "MediLab", (PxMediLab, 4, 4f) },
            { "TeslaCoil", (PxTeslaCoil, 4, 8f) }, { "LaserTower", (PxLaserTower, 4, 5f) }, { "AtomicCannon", (PxAtomicCannon, 4, 5f) },
            { "ArcFurnace", (PxArcFurnace, 4, 6f) }, { "AutomatedPlant", (PxAutomatedPlant, 4, 6f) }, { "HoverTankWorks", (PxHoverTankWorks, 4, 5f) },
            { "RobotFoundry", (PxRobotFoundry, 4, 5f) }, { "RocketPad", (PxRocketPad, 4, 6f) }, { "UraniumMine", (PxUraniumMine, 4, 5f) },
            { "AtomicReactor", (PxAtomicReactor, 4, 5f) },
        };

    private static readonly Color32[] Turquoise = { P.Hex("1b5a5c"), P.Hex("2e8f8c"), P.Hex("5cc8bd"), P.Hex("a8eee2") };
    private static readonly Color32[] Stucco = { P.Hex("8c8478"), P.Hex("c8c0b0"), P.Hex("e8e2d4"), P.Hex("faf6ec") };

    /// <summary>A plain modernist wall in pale stucco with a turquoise band.</summary>
    private static void ModernistWall(K k, int x0, int y0, int x1, int y1)
    {
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                k.Px(x, y, x < x0 + 2 ? Stucco[3] : x > x1 - 2 ? Stucco[1] : Stucco[2]);
        k.HLine(x0, x1, y0 + 2, Turquoise[2]);
    }

    private static void Starburst(K k, int x, int y, Color32 c)
    {
        k.Line(x - 3, y, x + 3, y, c); k.Line(x, y - 3, x, y + 3, c);
        k.Px(x - 2, y - 2, c); k.Px(x + 2, y + 2, c); k.Px(x - 2, y + 2, c); k.Px(x + 2, y - 2, c);
    }

    private static void Ribbon(K k, int x0, int x1, int y, int h, Color32[] ramp)
    {
        // a swooping boomerang roof
        for (int x = x0; x <= x1; x++)
        {
            float t = (x - x0) / (float)(x1 - x0);
            int top = y + Mathf.RoundToInt(Mathf.Sin(t * Mathf.PI) * h * 0.5f + t * h * 0.5f);
            for (int yy = y; yy <= top; yy++) k.Px(x, yy, yy == top ? ramp[3] : ramp[2]);
        }
    }

    private static void Sparks(K k, float x0, float y0, float x1, float y1)
    {
        int f = k.Frame;
        Vector2 a = new Vector2(x0, y0), b = new Vector2(x1, y1);
        for (int i = 0; i < 4; i++)
        {
            Vector2 p0 = Vector2.Lerp(a, b, i / 4f) + new Vector2(0, ((i + f) % 2 == 0 ? 2 : -2) * (i == 0 ? 0 : 1));
            Vector2 p1 = Vector2.Lerp(a, b, (i + 1) / 4f) + new Vector2(0, ((i + 1 + f) % 2 == 0 ? 2 : -2) * (i == 3 ? 0 : 1));
            k.Line(p0.x, p0.y, p1.x, p1.y, f % 2 == 0 ? P.White : P.Water[4]);
        }
    }

    // ---------- Housing and money ----------

    private static void PxSkylineTower(K k)
    {
        // A slim modernist tower block on stilts: ribbons of glass, round porthole windows, a needle on top.
        k.Ground(2, 2, 62, 6);
        for (int x = 10; x <= 54; x += 11) k.VLine(x, 2, 8, P.Chrome[1]);
        int floors = 6 + (k.Level >= 1 ? 1 : 0), top = 8 + floors * 8;
        ModernistWall(k, 6, 8, 58, top);
        for (int f = 0; f < floors; f++) k.Rect(8, 11 + f * 8, 56, 15 + f * 8, f % 2 == 0 ? P.Water[2] : P.Water[1]);
        for (int f = 0; f < floors; f += 2) k.Ellipse(50, 13 + f * 8, 2, 2, P.Glow);
        k.Rect(4, top, 60, top + 2, Turquoise[2]);
        k.VLine(32, top + 3, top + 16, P.Chrome[2]); k.Px(32, top + 17, P.Red[3]);
        if (k.Level >= 2) Starburst(k, 14, top + 6, P.Gold[3]);
        k.Finish();
        if (k.Frame % 2 == 0) k.Px(32, top + 17, P.White);
    }

    private static void PxSkyBank(K k)
    {
        // A sleek bank tower with a swooping roof and a starburst sign.
        k.Rect(2, 2, 62, 4, Stucco[1]);
        ModernistWall(k, 8, 5, 56, 38);
        for (int i = 0; i < 4; i++) k.Rect(12 + i * 11, 10, 18 + i * 11, 34, P.Water[2]);
        k.Door(28, 5, 8, 10);
        Ribbon(k, 4, 60, 39, 10, Turquoise);
        Starburst(k, 50, 48, P.Gold[3]);
        k.Ellipse(16, 46, 4, 2, P.Gold);
        for (int i = 0; i < 2 + k.Level; i++) k.HLine(44 + i * 4, 47 + i * 4, 3, P.Gold[2]);
        k.Finish();
        if (k.Frame % 2 == 1) Starburst(k, 50, 48, P.White);
    }

    // ---------- Industry ----------

    private static void PxUraniumMine(K k)
    {
        // A hillside shaft with yellow hazard bands; green ore glows in the carts, a chrome headframe above.
        k.Ground(8, 2, 88, 26, P.Dirt);
        k.Mound(50, 44, 46, 28, P.Rock);
        for (int i = 0; i < 6; i++) { int x = 18 + i * 11, y = 36 + (i * 7) % 20; if (k.Get(x, y).a > 200) { k.Px(x, y, P.Ray[2]); k.Px(x + 1, y, P.Ray[1]); } }
        k.MineEntrance(36, 30, 20, 20, stone: true);
        for (int x = 36; x <= 56; x += 4) { k.Rect(x, 50, x + 1, 51, P.Gold[2]); k.Rect(x + 2, 50, x + 3, 51, P.Coal[1]); }
        k.Rails(42, 50, 2);
        k.Cart(38, 6, P.Ray);
        k.Line(70, 6, 76, 40, P.Chrome[1], 2); k.Line(82, 6, 76, 40, P.Chrome[2], 2);
        k.Ellipse(76, 42, 4, 4, P.Chrome);
        if (k.Level >= 1) k.Cart(54, 6, P.Ray);
        k.Finish();
        if (k.Frame % 2 == 0) k.Px(42, 9, P.Ray[3]);
    }

    private static void PxAtomicReactor(K k)
    {
        // A white containment dome and a cooling tower breathing steam; a glowing green core seen through a port.
        k.Ground(2, 2, 94, 40, P.Dirt);
        k.Ellipse(30, 10, 22, 24, Stucco, l => l.y >= 10);
        k.Rect(8, 4, 52, 10, Stucco[1]);
        float pulse = 0.5f + 0.5f * k.Wave();
        k.Ellipse(30, 18, 5, 5, pulse > 0.5f ? P.Ray : new[] { P.Ray[0], P.Ray[1], P.Ray[2], P.Ray[2] });
        k.Door(26, 4, 8, 8);
        // the cooling tower, waisted
        for (int y = 4; y < 46; y++)
        {
            float t = (y - 4) / 42f;
            int half = Mathf.RoundToInt(13 - Mathf.Sin(t * Mathf.PI) * 4);
            for (int x = 74 - half; x <= 74 + half; x++) k.Px(x, y, x < 74 - half + 3 ? Stucco[3] : x > 74 + half - 3 ? Stucco[1] : Stucco[2]);
        }
        k.Line(52, 10, 62, 10, P.Chrome[1], 2);
        for (int x = 8; x <= 52; x += 8) { k.Rect(x, 2, x + 3, 3, P.Gold[2]); }
        k.Finish();
        k.Smoke(74, 50, 1.4f, 120); k.Smoke(70, 56, 1.0f, 90);
    }

    private static void PxArcFurnace(K k)
    {
        // A steel plant where great carbon electrodes arc into the melt: blue-white sparks through the roof.
        k.Ground(2, 2, 94, 40, P.Dirt);
        Corrugated(k, 6, 8, 70, 34, P.Chrome);
        k.Rect(4, 34, 72, 37, Turquoise[1]);
        for (int i = 0; i < 3; i++) { int x = 22 + i * 12; k.Rect(x, 38, x + 2, 52, P.Coal[2]); k.VLine(x + 1, 52, 56, P.Chrome[2]); }
        Sparks(k, 22, 50, 48, 50);
        k.Door(30, 9, 14, 16, open: true);
        k.Rect(32, 9, 42, 18, k.Frame % 2 == 0 ? P.White : P.Glow[3]);
        k.Rails(52, 94, 2);
        k.Rect(78, 6, 90, 10, P.Chrome[1]); k.Ellipse(84, 13, 5, 4, P.Chrome);
        k.Finish();
    }

    private static void PxAutomatedPlant(K k)
    {
        // A long white plant with robot arms working behind a glass wall and a conveyor of crates out front.
        k.Ground(2, 2, 94, 40, P.Dirt);
        ModernistWall(k, 6, 8, 80, 30);
        k.Rect(10, 13, 76, 26, P.Water[1]);
        for (int i = 0; i < 4; i++)
        {
            int x = 18 + i * 16;
            float a = (k.T + i * 0.25f) * Mathf.PI * 2;
            k.VLine(x, 14, 18, P.Chrome[2]);
            k.Line(x, 18, x + Mathf.Cos(a) * 5, 18 + Mathf.Abs(Mathf.Sin(a)) * 5, P.Gold[2]);
        }
        Ribbon(k, 4, 84, 30, 6, Turquoise);
        k.HLine(6, 90, 5, P.Iron[2]);
        for (int i = 0; i < 4; i++) k.Crate(8 + ((i * 20 + k.Frame * 4) % 80), 6);
        k.Finish();
    }

    // ---------- Military ----------

    private static void PxTrooperAcademy(K k)
    {
        // A low modernist academy with a rocket on a plinth and a flag.
        k.Ground(2, 2, 94, 30);
        ModernistWall(k, 6, 10, 70, 28);
        for (int i = 0; i < 5; i++) if (i != 2) k.Ellipse(14 + i * 13, 20, 3, 3, P.Water);
        k.Door(34, 11, 8, 12);
        Ribbon(k, 3, 73, 28, 8, Turquoise);
        // a rocket on its plinth
        k.Rect(78, 4, 90, 8, Stucco[1]);
        k.Ellipse(84, 22, 4, 13, P.Chrome); k.Polygon(new[] { new Vector2(80, 10), new Vector2(76, 6), new Vector2(80, 16) }, _ => P.Red[2]);
        k.Polygon(new[] { new Vector2(88, 10), new Vector2(92, 6), new Vector2(88, 16) }, _ => P.Red[2]);
        k.Px(84, 35, P.Red[3]);
        k.Banner(52, 30, 12);
        k.Finish();
    }

    private static void PxBlasterRange(K k)
    {
        // A firing hall facing a range of chrome target robots; green bolts streak downrange.
        k.Ground(2, 2, 94, 80, P.Dirt);
        ModernistWall(k, 10, 56, 60, 68);
        Ribbon(k, 6, 64, 68, 6, Turquoise);
        for (int i = 0; i < 3 + Mathf.Min(k.Level, 1); i++) { int x = 12 + i * 22; k.Rect(x, 30, x + 6, 42, P.Chrome[2]); k.Ellipse(x + 3, 45, 3, 3, P.Chrome); k.Px(x + 3, 45, P.Red[3]); }
        k.Rect(6, 8, 90, 11, Turquoise[1]);
        int f = k.Frame;
        k.Line(20 + f * 6, 14 + f * 6, 26 + f * 6, 20 + f * 6, P.Ray[2]);
        k.Finish();
    }

    private static void PxHoverPool(K k)
    {
        // An open-fronted garage with a hover car floating on its glowing pad; a power charger.
        k.Ground(2, 2, 94, 30);
        ModernistWall(k, 6, 8, 64, 30);
        Ribbon(k, 3, 67, 30, 6, Turquoise);
        k.Rect(14, 9, 56, 26, P.Coal[1]);
        int bob = k.Frame % 2;
        k.Ellipse(35, 11, 14, 2, P.Ray);
        k.Ellipse(35, 16 + bob, 14, 4, P.Chrome); k.HLine(22, 48, 16 + bob, k.Team[2]);
        k.Rect(78, 4, 86, 22, P.Chrome[2]); k.Ellipse(82, 18, 2, 2, P.Ray);
        k.Finish();
    }

    private static void PxMediLab(K k)
    {
        // A white clinic with a round window and a big red cross; a healing ray lamp on its roof.
        k.Ground(4, 2, 92, 26, P.Dirt);
        ModernistWall(k, 6, 8, 80, 34);
        k.Ellipse(24, 22, 6, 6, P.Water);
        k.Rect(48, 16, 64, 28, P.White); k.Rect(54, 16, 58, 28, P.Red[2]); k.Rect(48, 20, 64, 24, P.Red[2]);
        k.Door(34, 9, 8, 12);
        Ribbon(k, 3, 83, 34, 6, Turquoise);
        k.VLine(72, 37, 46, P.Chrome[1]); k.Ellipse(72, 47, 3, 2, P.Ray);
        if (k.Frame % 2 == 0) k.Line(72, 45, 66, 38, P.Ray[3]);
        k.Finish();
    }

    private static void PxTeslaCoil(K k)
    {
        // A tall coil on a chrome base topped with a copper ball; lightning crawls over it before it strikes.
        k.Ground(10, 2, 54, 8);
        k.Rect(20, 2, 44, 10, P.Chrome[2]); k.Rect(20, 2, 22, 10, P.Chrome[3]);
        for (int y = 10; y < 40; y += 2) k.HLine(26, 38, y, y % 4 == 0 ? P.Bronze[3] : P.Bronze[2]);
        k.Ellipse(32, 44, 7, 6, P.Bronze);
        k.Door(28, 2, 8, 7);
        if (k.Frame % 2 == 0) { Sparks(k, 32, 48, 22, 54); Sparks(k, 34, 46, 44, 52); }
        else Sparks(k, 38, 44, 48, 40);
        k.Finish();
    }

    private static void PxLaserTower(K k)
    {
        // A slim white tower with a domed top and a long red-lensed laser emitter.
        k.Ground(10, 2, 54, 8);
        ModernistWall(k, 22, 2, 42, 40);
        k.Ellipse(32, 42, 11, 8, P.Chrome, l => l.y >= 42);
        k.Door(28, 3, 8, 10);
        int recoil = k.Frame == 1 ? 1 : 0;
        k.Rect(36 - recoil, 44, 56 - recoil, 46, P.Chrome[1]);
        k.Ellipse(57 - recoil, 45, 2, 2, P.Red);
        k.Finish();
        if (k.Frame == 1) k.Line(59, 45, 64, 45, P.Red[3]);
    }

    private static void PxAtomicCannon(K k)
    {
        // A heavy chrome gun on a turntable, its barrel ringed with green glowing coils.
        k.Ground(2, 2, 62, 8);
        k.Ellipse(30, 6, 24, 5, Stucco);
        k.Ellipse(28, 16, 16, 10, P.Chrome, l => l.y >= 12);
        int recoil = k.Frame == 1 ? 3 : 0;
        k.Rect(36 - recoil, 18, 62 - recoil, 22, P.Chrome[1]); k.HLine(36 - recoil, 62 - recoil, 22, P.Chrome[3]);
        for (int x = 42; x < 60; x += 5) k.Rect(x - recoil, 17, x + 1 - recoil, 23, P.Ray[2]);
        k.Px(22, 20, P.Ray[3]);
        k.Finish();
        if (k.Frame == 1) { k.Ellipse(65, 20, 3, 2.5f, P.Ray); k.Px(67, 20, P.White); }
    }

    private static void PxHoverTankWorks(K k)
    {
        // A huge hangar with a domed roof; a hover tank floats out on its glowing pad.
        k.Ground(2, 2, 94, 40, P.Dirt);
        ModernistWall(k, 4, 8, 68, 30);
        k.Ellipse(36, 30, 32, 12, P.Chrome, l => l.y >= 30);
        k.Rect(18, 9, 54, 26, P.Coal[1]);
        int bob = k.Frame % 2;
        k.Ellipse(78, 6, 13, 2, P.Ray);
        k.Ellipse(78, 12 + bob, 13, 4, P.Chrome); k.Line(78, 15 + bob, 94, 15 + bob, P.Chrome[1], 2);
        k.Finish();
    }

    private static void PxRobotFoundry(K k)
    {
        // A chrome-domed foundry: a giant robot stands in its bay, eyes lighting up as it is switched on.
        k.Ground(4, 2, 60, 12, P.Dirt);
        ModernistWall(k, 8, 2, 56, 34);
        k.Ellipse(32, 34, 24, 12, P.Chrome, l => l.y >= 34);
        k.Rect(18, 3, 46, 30, P.Coal[1]);
        k.Rect(26, 6, 38, 22, P.Chrome[2]); k.Rect(28, 22, 36, 28, P.Chrome[3]);
        bool on = k.Frame % 2 == 0;
        k.Px(30, 25, on ? P.Ray[3] : P.Ray[1]); k.Px(34, 25, on ? P.Ray[3] : P.Ray[1]);
        k.Line(26, 18, 22, 10, P.Chrome[1], 2); k.Line(38, 18, 42, 10, P.Chrome[1], 2);
        Sparks(k, 14, 44, 50, 44);
        k.Finish();
    }

    private static void PxRocketPad(K k)
    {
        // A launch pad with a chrome rocket on its gantry and a control bunker; flame licks the pad.
        k.Ground(2, 2, 94, 70, P.Dirt);
        k.Ellipse(48, 10, 24, 6, Stucco);
        for (int y = 10; y < 70; y++) { k.Px(66, y, P.Red[2]); if (y % 6 == 0) k.HLine(62, 66, y, P.Red[1]); }
        k.Ellipse(48, 40, 6, 26, P.Chrome);
        k.Polygon(new[] { new Vector2(42, 18), new Vector2(36, 10), new Vector2(42, 28) }, _ => P.Red[2]);
        k.Polygon(new[] { new Vector2(54, 18), new Vector2(60, 10), new Vector2(54, 28) }, _ => P.Red[2]);
        k.Ellipse(48, 52, 2.5f, 2.5f, P.Water);
        ModernistWall(k, 74, 4, 92, 18);
        k.Fire(42, 10, 12, 4 + k.Frame % 2 * 2);
        k.Finish();
        k.Smoke(48, 14, 1.0f, 110);
    }
}
