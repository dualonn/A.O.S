using System.Collections.Generic;
using UnityEngine;
using P = PixelPalette;
using K = BuildingKit;

/// <summary>
/// Renaissance buildings in pixel art: stucco and red tile, arcades and domes, brick bastions,
/// powder and the alchemists' green fires. Registered next to the other buildings' drawings.
/// </summary>
public static partial class PlaceholderArt
{
    private static readonly Dictionary<string, (PixelDrawer draw, int frames, float fps)> RenaissanceBuildingArt =
        new Dictionary<string, (PixelDrawer, int, float)>
        {
            { "MerchantHouse", (PxMerchantHouse, 4, 4f) }, { "Bank", (PxBank, 4, 3f) }, { "TercioBarracks", (PxTercioBarracks, 4, 4f) },
            { "MusketRange", (PxMusketRange, 4, 4f) }, { "CuirassierStables", (PxCuirassierStables, 4, 3f) }, { "Hospital", (PxHospital, 4, 3f) },
            { "CannonBastion", (PxCannonBastion, 4, 4f) }, { "SaltpetreWorks", (PxSaltpetreWorks, 4, 4f) }, { "PowderMill", (PxPowderMill, 4, 6f) },
            { "QuicksilverMine", (PxQuicksilverMine, 4, 5f) }, { "CannonFoundry", (PxCannonFoundry, 4, 6f) }, { "AlchemistLab", (PxAlchemistLab, 4, 5f) },
        };

    /// <summary>Red brick, for bastions and foundries.</summary>
    private static readonly Color32[] Brick = { P.Hex("4a1e18"), P.Hex("7a3424"), P.Hex("a8503a"), P.Hex("cf7556") };

    private static void BrickWall(K k, int x0, int y0, int x1, int y1)
    {
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                int row = (y - y0) / 3, bx = (x - x0 + (row % 2) * 3) % 6;
                Color32 c = (y - y0) % 3 == 0 || bx == 0 ? Brick[0] : ((x / 6 + row) % 3 == 0 ? Brick[3] : Brick[2]);
                k.Px(x, y, c);
            }
    }

    /// <summary>A row of round arches (an arcade or loggia) along the front of a building.</summary>
    private static void Arcade(K k, int x0, int x1, int y, int h, int n)
    {
        int span = (x1 - x0) / n;
        for (int i = 0; i < n; i++)
        {
            int ax = x0 + i * span + 2;
            k.Door(ax, y, span - 4, h, arched: true, open: true);
        }
        for (int i = 0; i <= n; i++) k.Column(x0 + i * span - 1, y, h - 1);
    }

    private static void Dome(K k, float cx, int yb, float r, Color32[] ramp)
    {
        k.Ellipse(cx, yb, r, r * 0.8f, ramp, l => l.y >= yb);
        k.VLine((int)cx, yb + (int)(r * 0.8f), yb + (int)(r * 0.8f) + 4, P.Gold[2]);   // lantern spike
        k.Px((int)cx, yb + (int)(r * 0.8f) + 5, P.Gold[3]);
    }

    // ---------- Economy and housing ----------

    private static void PxMerchantHouse(K k)
    {
        // A tall stucco house over a shop arcade, with balconies and a tile roof.
        k.Ground(2, 2, 62, 6);
        k.Wall(4, 2, 60, 16, K.Mat.Stone);
        Arcade(k, 6, 58, 3, 12, 3);
        int floors = 2 + (k.Level >= 1 ? 1 : 0), y = 16;
        for (int f = 0; f < floors; f++)
        {
            k.Wall(4, y, 60, y + 13, K.Mat.Plaster);
            for (int i = 0; i < 3; i++) k.Window(9 + i * 18, y + 4, 6, 7, true);
            k.HLine(4, 60, y, P.Stone[3]);
            if (f == 0) { k.HLine(6, 58, y + 2, P.Iron[1]); for (int x = 6; x <= 58; x += 3) k.VLine(x, y + 1, y + 2, P.Iron[1]); }   // balcony rail
            y += 13;
        }
        k.Roof(1, 63, y, 14, K.RoofType.Tile, 12);
        if (k.Level >= 2) { k.FlowerBox(8, 18, 10); k.FlowerBox(44, 18, 10); }
        k.Crate(2, 2); k.Barrel(56, 2);
        k.Finish();
        int sway = Mathf.RoundToInt(k.Wave());
        k.Rect(28 + sway, y - 30, 34 + sway, y - 26, k.Team[2]);   // a hanging cloth over the balcony
    }

    private static void PxBank(K k)
    {
        // A marble-fronted bank: pilasters, a pediment, a little dome, coins on the steps.
        k.Rect(2, 2, 62, 4, P.Marble[1]); k.Rect(4, 5, 60, 7, P.Marble[2]);
        k.Wall(6, 8, 58, 36, K.Mat.Plaster);
        k.Door(28, 8, 8, 14, arched: true);
        for (int i = 0; i < 4; i++) k.Column(8 + i * 15 + (i >= 2 ? 3 : 0), 8, 27);
        for (int i = 0; i < 2; i++) k.Window(16 + i * 28, 24, 6, 7);
        k.Rect(4, 36, 60, 39, P.Marble[3]); k.HLine(4, 60, 36, P.Marble[1]);
        for (int yy = 40; yy < 50; yy++)
        {
            int half = 28 - (yy - 40) * 3;
            for (int x = 32 - half; x <= 32 + half; x++) k.Px(x, yy, x < 32 - half + 2 ? P.Marble[3] : P.Marble[2]);
        }
        Dome(k, 32, 49, 9, P.Tile);
        k.Ellipse(32, 44, 3, 2.5f, P.Gold);
        for (int i = 0; i < 2 + k.Level; i++) for (int h = 0; h < 3 + i % 2; h++) k.HLine(42 + i * 5, 45 + i * 5, 2 + h, h % 2 == 0 ? P.Gold[2] : P.Gold[3]);
        if (k.Level >= 1) k.Banner(0, 2, 30);
        k.Finish();
        if (k.Frame % 2 == 0) k.Px(43, 6, P.White);
    }

    private static void PxSaltpetreWorks(K k)
    {
        // Open nitre beds crusted white, a lean-to over boiling vats, sacks of saltpetre.
        k.Ground(2, 2, 94, 70, P.Dirt);
        for (int i = 0; i < 3 + k.Level; i++)
        {
            int x = 6 + (i % 3) * 30, y = 40 + (i / 3) * 18;
            k.Rect(x, y, x + 24, y + 12, P.Dirt[0]);
            k.HLine(x, x + 24, y + 12, P.Wood[1]);
            for (int j = 0; j < 14; j++) k.Px(x + 2 + (j * 7) % 21, y + 2 + (j * 5) % 9, j % 3 == 0 ? P.White : P.Linen[3]);
        }
        k.VLine(52, 6, 28, P.Wood[2]); k.VLine(90, 6, 28, P.Wood[2]);
        k.Roof(48, 94, 28, 8, K.RoofType.Shingle, 3);
        foreach (int x in new[] { 58, 74 })
        {
            k.Ellipse(x + 5, 10, 7, 5, P.Iron);
            k.Fire(x + 1, 3, 8, 4);
            k.HLine(x, x + 10, 14, P.Linen[3]);   // the brine's white scum
        }
        for (int i = 0; i < 3; i++) k.Ellipse(10 + i * 9, 8, 4, 5, P.Linen);
        k.Finish();
        k.Smoke(64, 18, 0.7f, 110); k.Smoke(80, 18, 0.7f, 110);
    }

    private static void PxPowderMill(K k)
    {
        // A stone mill by a race of water, its wheel turning the stamps; powder barrels marked in red.
        k.Ground(2, 2, 94, 40, P.Dirt);
        int top = Hall(k, 8, 60, 14, 22, K.Mat.Stone, K.RoofType.Tile, 14, 10, windows: 2);
        float cx = 76, cy = 24, r = 15;
        k.Rect(64, 4, 94, 9, P.Water[2]); k.HLine(64, 94, 9, P.Water[3]);
        k.Ellipse(cx, cy, r, r, P.Wood, l => (l - new Vector2(cx, cy)).sqrMagnitude > (r - 3) * (r - 3));
        for (int i = 0; i < 8; i++)
        {
            float a = (i / 8f + k.T / 8f) * Mathf.PI * 2;
            k.Line(cx, cy, cx + Mathf.Cos(a) * (r - 1), cy + Mathf.Sin(a) * (r - 1), P.Wood[1]);
        }
        for (int i = 0; i < 3 + k.Level; i++)
        {
            int x = 8 + i * 9;
            k.Barrel(x, 3);
            k.Px(x + 3, 7, P.Red[2]); k.Px(x + 4, 8, P.Red[2]); k.Px(x + 4, 6, P.Red[2]); k.Px(x + 5, 7, P.Red[2]);
        }
        // the stamps rising and falling inside the open door
        bool up = k.Frame % 2 == 0;
        k.Rect(30, 15, 40, 28, P.Coal[1]);
        k.Rect(33, up ? 22 : 17, 37, up ? 27 : 22, P.Wood[2]);
        if (k.Level >= 2) k.Banner(2, 4, 22, P.Red);
        k.Finish();
    }

    private static void PxQuicksilverMine(K k)
    {
        // A hillside adit; a retort furnace boils the cinnabar and quicksilver beads in a trough.
        k.Ground(8, 2, 88, 26, P.Dirt);
        k.Mound(50, 44, 46, 28);
        for (int i = 0; i < 12; i++)
        {
            int x = 14 + k.R(0, 70), y = 34 + k.R(0, 40);
            if (k.Get(x, y).a > 200) { k.Px(x, y, P.Red[1]); if (i % 3 == 0) k.Px(x + 1, y, P.Red[2]); }   // cinnabar
        }
        k.MineEntrance(36, 30, 20, 20, stone: true);
        k.Rails(42, 50, 2);
        k.Cart(38, 6, P.Rock);
        // the retort: a brick oven with a pipe into a cooling pot
        BrickWall(k, 66, 6, 82, 20);
        k.Fire(70, 6, 8, 5);
        k.Line(80, 18, 90, 12, P.Iron[2], 2);
        k.Pot(86, 4, P.Iron);
        for (int i = 0; i < 2 + k.Level; i++) k.Ellipse(14 + i * 6, 6, 1.6f, 1.2f, P.Steel);   // silver beads
        k.Torch(32, 30); k.Torch(58, 30);
        if (k.Level >= 2) k.Banner(4, 6, 22);
        k.Finish();
        k.Smoke(74, 22, 0.6f, 110);
        if (k.Frame % 2 == 1) k.Px(15, 7, P.White);
    }

    // ---------- Military ----------

    private static void PxTercioBarracks(K k)
    {
        // A brick and stucco barracks with racks of pikes and arquebuses, a drum and a flag.
        k.Ground(2, 2, 94, 30);
        BrickWall(k, 8, 10, 62, 34);
        k.Rect(8, 10, 62, 13, P.Stone[2]); k.HLine(8, 62, 13, P.Stone[4]);
        k.Door(30, 11, 10, 15, arched: true);
        for (int i = 0; i < 2; i++) k.Window(14 + i * 38, 22, 6, 6);
        k.Roof(5, 65, 34, 14, K.RoofType.Tile, 10);
        for (int i = 0; i < 4 + k.Level; i++) { int x = 66 + i * 4; k.Line(x, 2, x + 1, 46, P.Wood[2]); k.Px(x + 1, 47, P.Steel[3]); }
        k.HLine(64, 94, 22, P.Wood[1]);
        k.Ellipse(16, 5, 4, 3, new[] { P.Red[1], P.Red[2], P.Linen[2], P.Linen[3] });   // the drum
        k.Banner(44, 48, 14);
        if (k.Level >= 2) k.HLine(8, 62, 34, P.Gold[2]);
        k.Finish();
    }

    private static void PxMusketRange(K k)
    {
        // A firing line behind a low wall, targets far off, powder kegs, and drifting smoke.
        k.Ground(2, 2, 94, 80, P.Dirt);
        k.Wall(8, 56, 88, 70, K.Mat.Plaster);
        Arcade(k, 10, 86, 56, 12, 5);
        k.Roof(4, 92, 70, 12, K.RoofType.Tile, 6);
        for (int i = 0; i < 3 + Mathf.Min(k.Level, 1); i++) k.Target(10 + i * 22, 30);
        k.Rect(6, 8, 90, 12, P.Stone[2]); k.HLine(6, 90, 12, P.Stone[4]);   // the firing wall
        for (int i = 0; i < 2; i++) { k.Barrel(12 + i * 10, 2); k.Px(15 + i * 10, 6, P.Red[2]); }
        if (k.Level >= 1) k.Banner(2, 14, 22);
        if (k.Level >= 2) k.Banner(90, 30, 20);
        k.Finish();
        if (k.Frame == 1) { k.Px(40, 15, P.Glow[3]); k.Px(41, 15, P.White); }
        k.Smoke(44, 16, 0.6f, 90);
    }

    private static void PxCuirassierStables(K k)
    {
        // Brick stables for heavy horse, a saddle rack and a pistol-and-sword trophy over the door.
        k.Ground(2, 2, 94, 30);
        BrickWall(k, 6, 14, 90, 32);
        k.Roof(3, 93, 32, 14, K.RoofType.Tile, 10);
        for (int i = 0; i < 4; i++)
        {
            int x = 12 + i * 20;
            k.Rect(x, 15, x + 10, 22, P.Wood[1]); k.Rect(x, 23, x + 10, 30, P.Coal[1]);
            if (i < 2 + k.Level)
            {
                var coat = i % 2 == 0 ? P.Horse : P.DarkHorse;
                k.Ellipse(x + 6, 26, 3, 4, coat);
                k.Rect(x + 6, 22 + (k.Frame % 2), x + 9, 25 + (k.Frame % 2), coat[2]);
                k.Px(x + 7, 28, P.Ink);
            }
        }
        k.ShieldDecor(48, 36, P.Steel);
        k.Hay(4, 2); k.Trough(70, 2, 12);
        if (k.Level >= 1) k.Banner(92, 4, 22);
        k.Finish();
    }

    private static void PxHospital(K k)
    {
        // A hospital with a long loggia of round arches and a small dome over the chapel.
        k.Ground(4, 2, 92, 26, P.Dirt);
        k.Wall(6, 12, 90, 40, K.Mat.Plaster);
        Arcade(k, 8, 88, 12, 13, 6);
        for (int i = 0; i < 4; i++) k.Window(14 + i * 20, 30, 5, 6);
        k.Roof(3, 93, 40, 12, K.RoofType.Tile, 8);
        Dome(k, 48, 50, 8, P.Tile);
        for (int i = 0; i < 2 + k.Level; i++)
        {
            int x = 6 + i * 16;
            k.Rect(x, 3, x + 11, 8, P.Dirt[1]);
            for (int j = 0; j < 6; j++) k.Px(x + 1 + j * 2, 6 + (j % 2), j % 3 == 0 ? P.Flower[3] : P.Leaf[3]);
        }
        k.Rect(70, 4, 84, 6, P.Linen[3]); k.HLine(70, 84, 3, P.Wood[1]);
        if (k.Level >= 1) k.Banner(92, 4, 22, P.Linen);
        k.Finish();
    }

    private static void PxCannonBastion(K k)
    {
        // A low, thick brick bastion with sloped stone faces; the gun crew works on its open top.
        const int top = 40;
        k.Ground(4, 2, 60, 8);
        for (int y = 2; y < top - 6; y++)
        {
            int inset = (y - 2) / 6;   // the battered (sloping) walls
            for (int x = 6 + inset; x <= 58 - inset; x++)
                k.Px(x, y, (y % 4 == 0) ? Brick[0] : ((x / 6 + y / 4) % 3 == 0 ? Brick[3] : Brick[2]));
        }
        k.Rect(8, 2, 56, 5, P.Stone[2]); k.HLine(8, 56, 5, P.Stone[4]);
        k.Door(28, 3, 8, 11, arched: true);
        TowerDeck(k, 10, 54, top, K.Mat.Stone, true);
        if (k.Level >= 1) k.Banner(56, 2, 26);
        if (k.Level >= 2) { k.Barrel(2, 2); k.Ellipse(8, 3, 1.5f, 1.5f, P.Iron); k.Ellipse(11, 3, 1.5f, 1.5f, P.Iron); }
        if (k.Level >= 3) k.HLine(10, 54, top - 7, P.Gold[2]);
        k.Finish();
    }

    private static void PxCannonFoundry(K k)
    {
        // A brick casting house with a tall stack; a finished barrel on trestles, a casting pit glowing.
        k.Ground(2, 2, 94, 40, P.Dirt);
        BrickWall(k, 6, 12, 62, 38);
        k.Roof(3, 65, 38, 12, K.RoofType.Tile, 10);
        k.Chimney(48, 48, 9, 30 + k.Level * 4, Brick);
        k.Door(24, 13, 12, 16, arched: true, open: true);
        float glow = 0.5f + 0.5f * k.Wave();
        var mouth = glow > 0.5f ? P.Glow : P.Fire;
        k.Rect(26, 13, 34, 20, mouth[2]);
        // a finished cannon on trestles
        k.VLine(70, 2, 10, P.Wood[1]); k.VLine(88, 2, 10, P.Wood[1]);
        k.Ellipse(79, 12, 12, 3, P.Bronze);
        k.Ellipse(68, 12, 2.5f, 2.5f, P.Bronze);
        for (int i = 0; i < k.Level; i++) k.Ellipse(70 + i * 6, 3, 2, 2, P.Iron);   // shot
        if (k.Level >= 1) k.Banner(2, 4, 24);
        k.Finish();
        k.Smoke(52, 80 + k.Level * 4, 1.1f, 160);
    }

    private static void PxAlchemistLab(K k)
    {
        // A crooked round tower with a pointed roof, green light in the windows, an alembic and green smoke.
        k.Ground(4, 2, 60, 12, P.Dirt);
        k.Wall(14, 2, 44, 46, K.Mat.Stone);
        for (int y = 2; y < 46; y++) { k.Darken(14, y, 0.35f); k.Darken(44, y, 0.35f); }
        k.Cone(29, 46, 19, 22, K.RoofType.Slate);
        k.Door(25, 3, 8, 12, arched: true);
        float pulse = 0.5f + 0.5f * k.Wave();
        var green = P.Green;
        foreach (var (x, y) in new[] { (19, 24), (35, 30), (27, 36) })
        {
            k.Rect(x, y, x + 4, y + 5, P.Coal[1]);
            k.Rect(x + 1, y + 1, x + 3, y + 4, pulse > 0.5f ? green[3] : green[2]);
        }
        // the alembic on a brazier outside
        k.Fire(48, 3, 8, 4);
        k.Ellipse(52, 11, 4, 4, new[] { P.Coal[2], green[1], green[2], green[3] });
        k.Line(55, 14, 60, 18, P.Bronze[2]);
        for (int i = 0; i < 2 + k.Level; i++) k.Ellipse(6 + i * 4, 4, 1.4f, 2, new[] { P.Coal[2], i % 2 == 0 ? P.Purple[2] : green[2], P.White, P.White });
        if (k.Level >= 1) k.Banner(2, 12, 20, P.Purple);
        k.Finish();
        // green smoke curling from the roof
        for (int i = 0; i < 3; i++)
        {
            int sx = 30 + Mathf.RoundToInt(Mathf.Sin((k.T + i * 0.3f) * 6.28f) * 2), sy = 70 + i * 4 + k.Frame;
            k.Px(sx, sy, green[3]); k.Px(sx + 1, sy, green[2]);
        }
    }
}
