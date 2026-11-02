using System.Collections.Generic;
using UnityEngine;
using P = PixelPalette;
using K = BuildingKit;

/// <summary>
/// Medieval Age buildings in pixel art: castles, half-timbered towns, steel and the carved runes
/// of the fantasy layer. Same rules as the rest (32 px a tile, upgrades add to the look, a short
/// loop of frames); registered next to the other buildings' drawings.
/// </summary>
public static partial class PlaceholderArt
{
    private static readonly Dictionary<string, (PixelDrawer draw, int frames, float fps)> MedievalBuildingArt =
        new Dictionary<string, (PixelDrawer, int, float)>
        {
            { "Steelworks", (PxSteelworks, 4, 6f) }, { "RuneQuarry", (PxRuneQuarry, 4, 4f) }, { "Townhouse", (PxTownhouse, 4, 4f) },
            { "Guildhall", (PxGuildhall, 4, 3f) }, { "ArmsHall", (PxArmsHall, 4, 4f) }, { "CrossbowRange", (PxCrossbowRange, 4, 3f) },
            { "KnightsHall", (PxKnightsHall, 4, 3f) }, { "Monastery", (PxMonastery, 4, 3f) }, { "CastleTower", (PxCastleTower, 4, 4f) },
            { "PikeYard", (PxPikeYard, 4, 4f) }, { "RuneShrine", (PxRuneShrine, 4, 5f) },
        };

    /// <summary>Dark oak beams over plaster: the half-timbered look of a medieval town.</summary>
    private static void Timbers(K k, int x0, int y0, int x1, int y1, bool braces = true)
    {
        var beam = P.Wood[0];
        k.HLine(x0, x1, y0, beam); k.HLine(x0, x1, y1, beam);
        for (int x = x0; x <= x1; x += 8) k.VLine(x, y0, y1, beam);
        k.VLine(x1, y0, y1, beam);
        if (!braces) return;
        for (int x = x0; x + 8 <= x1; x += 16)
            k.Line(x, y0, x + 8, y1, beam);
    }

    /// <summary>A steep slate roof with a ridge, for northern towns and castles.</summary>
    private static void SlateRoof(K k, int x0, int x1, int yb, int h) => k.Roof(x0, x1, yb, h, K.RoofType.Slate, (x1 - x0) / 4);

    private static void RuneGlyph(K k, int x, int y, Color32 c)
    {
        // a small angular rune: a stave with two twigs
        k.VLine(x, y, y + 4, c);
        k.Px(x + 1, y + 3, c); k.Px(x + 2, y + 4, c);
        k.Px(x - 1, y + 1, c); k.Px(x - 2, y, c);
    }

    // ---------- Economy ----------

    private static void PxSteelworks(K k)
    {
        // A stone blast house with a tall stack, a water-driven bellows and glowing steel coming out.
        k.Ground(2, 2, 94, 46, P.Dirt);
        int top = Hall(k, 6, 62, 10, 26, K.Mat.Stone, K.RoofType.Slate, 16, 10, windows: 1, door: false);
        k.Chimney(46, top + 4, 9, 34 + k.Level * 6, P.Stone);
        if (k.Level >= 1) k.Chimney(16, top + 2, 7, 22, P.Stone);
        // the furnace mouth
        float glow = 0.5f + 0.5f * k.Wave();
        var mouth = glow > 0.5f ? P.Glow : P.Fire;
        k.Rect(26, 11, 40, 24, P.Coal[1]); k.Rect(28, 11, 38, 20, mouth[2]); k.Rect(30, 11, 36, 16, mouth[3]);
        // the water wheel turning the bellows
        float cx = 78, cy = 24, r = 14;
        k.Ellipse(cx, cy, r, r, P.Wood, l => (l - new Vector2(cx, cy)).sqrMagnitude > (r - 3) * (r - 3));
        for (int i = 0; i < 6; i++)
        {
            float a = (i / 6f + k.T / 6f) * Mathf.PI * 2;
            k.Line(cx, cy, cx + Mathf.Cos(a) * (r - 1), cy + Mathf.Sin(a) * (r - 1), P.Wood[1]);
        }
        k.Rect(66, 4, 94, 8, P.Water[2]); k.HLine(66, 94, 8, P.Water[3]);
        // steel bars cooling on a rack, more with each level
        for (int i = 0; i < 3 + k.Level * 2; i++)
            k.Rect(8 + (i % 4) * 7, 3 + (i / 4) * 3, 13 + (i % 4) * 7, 4 + (i / 4) * 3, i % 3 == 0 ? P.Glow[1] : P.Steel[2]);
        k.Anvil(46, 4);
        if (k.Level >= 2) k.Banner(2, 30, 22);
        k.Finish();
        k.Smoke(50, top + 40 + k.Level * 6, 1.2f, 160);
        if (k.Frame % 2 == 0) { k.Px(44, 9, P.Glow[3]); k.Px(48, 11, P.Fire[2]); }   // sparks off the anvil
    }

    private static void PxRuneQuarry(K k)
    {
        // A hillside cut into dark runestone, its carved faces glowing; a crane and finished stones.
        k.Ground(6, 2, 90, 54, P.Dirt);
        k.Mound(50, 60, 44, 22, Slate);
        k.Pit(14, 22, 70, 52);
        var rune = P.Rune;
        float pulse = 0.5f + 0.5f * k.Wave();
        Color32 glow = pulse > 0.5f ? rune[3] : rune[2];
        foreach (var (x, y) in new[] { (24, 34), (40, 40), (56, 32), (64, 46), (30, 46) }) RuneGlyph(k, x, y, glow);
        // finished runestones, standing on end
        for (int i = 0; i < 2 + k.Level; i++)
        {
            int x = 70 + (i % 3) * 8, y = 6 + (i / 3) * 4;
            k.Rect(x, y, x + 5, y + 10, P.Coal[2]); k.Rect(x, y + 9, x + 5, y + 10, P.Coal[3]);
            RuneGlyph(k, x + 3, y + 3, rune[2]);
        }
        if (k.Level >= 1)
        {
            k.Line(78, 30, 78, 70, P.Wood[2], 2); k.Line(78, 68, 46, 60, P.Wood[3], 2);
            int hy = 42 + Mathf.RoundToInt(k.Wave() * 3);
            k.Line(48, 60, 48, hy + 4, P.Linen[2]); k.Rect(45, hy, 51, hy + 4, P.Coal[2]);
        }
        k.Cart(6, 4, P.Coal);
        if (k.Level >= 2) k.Banner(90, 8, 22);
        k.Finish();
        // a chisel striking
        bool up = k.Frame % 2 == 0;
        k.Line(36, 30, up ? 39 : 41, up ? 38 : 33, P.Wood[3]);
        if (!up) { k.Px(43, 33, glow); k.Px(44, 35, rune[3]); }
    }

    private static void PxTownhouse(K k)
    {
        // Two half-timbered houses with jettied upper floors and steep slate roofs.
        k.Ground(2, 2, 62, 6);
        int floors = 2 + (k.Level >= 1 ? 1 : 0);
        void House(int x0, int x1)
        {
            int y = 2;
            for (int f = 0; f < floors; f++)
            {
                int jetty = f == 0 ? 0 : 1;   // each floor overhangs the one below
                k.Wall(x0 - jetty, y, x1 + jetty, y + 14, f == 0 ? K.Mat.Stone : K.Mat.Plaster);
                if (f > 0) Timbers(k, x0 - jetty, y, x1 + jetty, y + 14);
                k.Window((x0 + x1) / 2 - 2, y + 5, 5, 5, f > 0);
                y += 14;
            }
            SlateRoof(k, x0 - 3, x1 + 3, y, 16);
        }
        House(4, 30); House(34, 60);
        k.Door(13, 3, 7, 10, arched: true); k.Door(43, 3, 7, 10);
        if (k.Level >= 2) { k.FlowerBox(9, 20, 12); k.FlowerBox(39, 20, 12); k.Barrel(28, 2); }
        k.Finish();
        // a shop sign swinging
        int sw = Mathf.RoundToInt(k.Wave());
        k.HLine(30, 36, 17, P.Wood[1]); k.Rect(31 + sw, 12, 35 + sw, 16, P.Gold[2]);
    }

    private static void PxGuildhall(K k)
    {
        // A stone arcade below, a timbered hall above, a gilded coin sign and a small bell tower.
        k.Ground(2, 2, 62, 6);
        k.Wall(4, 2, 60, 18, K.Mat.Stone);
        for (int i = 0; i < 4; i++) k.Door(7 + i * 14, 3, 8, 12, arched: true, open: true);
        k.Wall(2, 18, 62, 38, K.Mat.Plaster);
        Timbers(k, 2, 18, 62, 38);
        for (int i = 0; i < 3; i++) k.Window(10 + i * 19, 25, 6, 6);
        SlateRoof(k, -1, 65, 38, 18);
        // bell turret on the ridge
        k.Wall(28, 52, 36, 62, K.Mat.Planks); k.Cone(32, 62, 6, 8, K.RoofType.Slate);
        int bell = Mathf.RoundToInt(k.Wave() * 1.2f);
        k.Ellipse(32 + bell, 55, 2, 2.5f, P.Bronze);
        // the guild's gilded coin
        k.Ellipse(32, 44, 4, 4, P.Gold); k.Px(31, 45, P.White);
        for (int i = 0; i < 1 + k.Level; i++) k.Crate(46 + i * 6, 2);
        if (k.Level >= 1) k.Banner(0, 2, 26);
        if (k.Level >= 2) k.HLine(2, 62, 18, P.Gold[2]);
        k.Finish();
    }

    // ---------- Military ----------

    private static void PxArmsHall(K k)
    {
        // A square stone keep with a crenellated top and a timber hall beside it.
        k.Ground(2, 2, 94, 30);
        int hallTop = Hall(k, 40, 90, 8, 18, K.Mat.Stone, K.RoofType.Slate, 14, 10, windows: 2);
        k.Wall(6, 4, 40, 58, K.Mat.Stone);
        Crenellated(k, 6, 40, 59, P.Stone);
        k.Rect(18, 4, 28, 20, P.Coal[1]); k.Door(19, 5, 9, 14, arched: true);
        for (int y = 28; y <= 48; y += 10) k.Rect(22, y, 23, y + 5, P.Coal[2]);   // arrow slits
        k.SpearRack(46, 2, 2 + k.Level);
        k.ShieldDecor(82, 16);
        k.Banner(30, 60, 14);
        if (k.Level >= 1) k.Banner(88, hallTop + 2, 16);
        if (k.Level >= 2) { k.Rect(6, 4, 40, 6, P.Stone[1]); k.HLine(6, 40, 6, P.Stone[4]); }
        if (k.Level >= 3) k.HLine(6, 40, 58, P.Gold[2]);
        k.Finish();
    }

    private static void PxCrossbowRange(K k)
    {
        // A shooting range: a timber gallery, pavise shields, straw targets far off.
        k.Ground(2, 2, 94, 80, P.Dirt);
        k.Wall(8, 56, 88, 70, K.Mat.Planks);
        Timbers(k, 8, 56, 88, 70, braces: false);
        k.Roof(4, 92, 70, 12, K.RoofType.Shingle, 6);
        for (int i = 0; i < 3 + Mathf.Min(k.Level, 1); i++) k.Target(10 + i * 22, 30);
        // pavises (tall standing shields) along the firing line
        for (int i = 0; i < 3; i++)
        {
            int x = 14 + i * 26;
            k.Rect(x, 6, x + 8, 20, k.Team[2]); k.Rect(x, 6, x + 1, 20, k.Team[3]); k.HLine(x, x + 8, 20, P.Wood[1]);
            k.Px(x + 4, 13, P.Gold[2]); k.Px(x + 4, 14, P.Gold[2]);
        }
        k.Barrel(86, 2);
        if (k.Level >= 1) k.Banner(2, 4, 22);
        if (k.Level >= 2) k.Banner(90, 30, 20);
        if (k.Level >= 3) k.HLine(4, 92, 70, P.Gold[2]);
        k.Finish();
    }

    private static void PxKnightsHall(K k)
    {
        // Stone stables for war horses, a lance rack and a tilting yard's striped pennants.
        k.Ground(2, 2, 94, 30);
        Hall(k, 6, 90, 14, 18, K.Mat.Stone, K.RoofType.Slate, 16, 10, windows: 0, door: false);
        for (int i = 0; i < 4; i++)
        {
            int x = 12 + i * 20;
            k.Rect(x, 15, x + 10, 22, P.Wood[1]); k.Rect(x, 23, x + 10, 30, P.Coal[1]);
            if (i < 2 + k.Level)
            {
                var coat = i % 2 == 0 ? P.DarkHorse : P.Horse;
                k.Ellipse(x + 6, 26, 3, 4, coat);
                k.Rect(x + 6, 22 + (k.Frame % 2), x + 9, 25 + (k.Frame % 2), coat[2]);
                k.Rect(x + 5, 25, x + 7, 27, P.Steel[2]);   // chanfron
            }
        }
        // a rack of lances with pennants
        for (int i = 0; i < 3; i++)
        {
            k.Line(4 + i * 3, 2, 6 + i * 3, 40, P.Wood[3]);
            k.Rect(7 + i * 3, 34, 10 + i * 3, 37, i % 2 == 0 ? k.Team[2] : P.Linen[3]);
        }
        k.Hay(60, 2); k.Trough(74, 2, 12);
        if (k.Level >= 1) k.Banner(92, 4, 22);
        if (k.Level >= 2) k.HLine(6, 90, 32, P.Gold[2]);
        k.Finish();
    }

    private static void PxMonastery(K k)
    {
        // A stone church with a bell tower, a cloister walk and herb beds.
        k.Ground(4, 2, 92, 26, P.Dirt);
        int top = Hall(k, 30, 90, 14, 24, K.Mat.Stone, K.RoofType.Slate, 18, 10, windows: 3, arched: true);
        k.Wall(8, 14, 30, 64, K.Mat.Stone);
        k.Cone(19, 64, 13, 16, K.RoofType.Slate);
        k.Rect(16, 48, 22, 56, P.Coal[1]);
        int bell = Mathf.RoundToInt(k.Wave() * 1.5f);
        k.Ellipse(19 + bell, 51, 2.5f, 3, P.Bronze);
        k.Door(15, 15, 9, 14, arched: true);
        // a round window over the nave door
        k.Ellipse(60, 30, 3, 3, new[] { P.Blue[1], P.Blue[2], P.Glow[2], P.Glow[3] });
        // cloister arches along the front
        k.Rect(30, 12, 90, 14, P.Stone[2]);
        for (int i = 0; i < 5; i++) k.Door(33 + i * 12, 4, 7, 9, arched: true, open: true);
        for (int i = 0; i < 1 + k.Level; i++)
        {
            int x = 6 + i * 12;
            k.Rect(x, 3, x + 9, 7, P.Dirt[1]);
            for (int j = 0; j < 5; j++) k.Px(x + 1 + j * 2, 5 + (j % 2), j % 2 == 0 ? P.Leaf[3] : P.Flower[2]);
        }
        if (k.Level >= 1) k.Banner(92, 4, 22, P.Linen);
        k.Finish();
    }

    private static void PxCastleTower(K k)
    {
        // A round stone tower; the crossbowman stands at its open top (crewed, so no roof).
        const int top = 46;
        k.Ground(6, 2, 58, 10);
        k.Wall(12, 2, 52, top - 8, K.Mat.Stone);
        // round it off: shade the sides
        for (int y = 2; y < top - 8; y++) { k.Darken(12, y, 0.35f); k.Darken(13, y, 0.2f); k.Darken(51, y, 0.25f); k.Darken(52, y, 0.4f); }
        k.Door(28, 3, 8, 12, arched: true);
        k.Rect(31, 22, 32, 28, P.Coal[2]);
        // machicolations under the parapet
        for (int x = 10; x <= 52; x += 5) k.Rect(x, top - 10, x + 2, top - 8, P.Stone[1]);
        TowerDeck(k, 9, 55, top, K.Mat.Stone, true);
        if (k.Level >= 1) k.Banner(54, 2, 30);
        if (k.Level >= 2) k.HLine(9, 55, top - 7, P.Gold[2]);
        if (k.Level >= 3) { k.Torch(6, 12); k.Torch(56, 12); }
        k.Finish();
    }

    private static void PxPikeYard(K k)
    {
        // A timbered barracks and a drill yard bristling with pikes.
        k.Ground(2, 2, 94, 34);
        int top = Hall(k, 6, 54, 30, 16, K.Mat.Plaster, K.RoofType.Shingle, 16, 10, windows: 2);
        Timbers(k, 6, 30, 54, 30 + 16);
        // pike racks: long poles leaning, more with each level
        for (int i = 0; i < 4 + k.Level * 2; i++)
        {
            int x = 60 + i * 4;
            k.Line(x, 2, x + 2, 50, P.Wood[2]);
            k.Px(x + 2, 51, P.Steel[3]); k.Px(x + 2, 52, P.Steel[2]);
        }
        k.HLine(58, 92, 24, P.Wood[1]);
        // a straw dummy to drive pikes into
        k.VLine(34, 4, 18, P.Wood[1]); k.Rect(31, 10, 37, 18, P.Straw[2]); k.Ellipse(34, 21, 2.5f, 2.5f, P.Straw);
        if (k.Level >= 1) k.Banner(4, 4, 22);
        if (k.Level >= 2) k.HLine(6, 54, 30, P.Gold[2]);
        k.Finish();
    }

    private static void PxRuneShrine(K k)
    {
        // A ring of tall runestones round an altar stone whose carvings pulse with light.
        k.Ground(4, 2, 60, 22, P.Grass);
        var rune = P.Rune;
        float pulse = 0.5f + 0.5f * k.Wave();
        Color32 glow = pulse > 0.66f ? rune[3] : pulse > 0.33f ? rune[2] : rune[1];
        // back stones first (smaller, higher), then the altar, then front stones
        foreach (var (x, y, h) in new[] { (14, 22, 18), (30, 26, 22), (46, 22, 18) })
        {
            k.Rect(x, y, x + 6, y + h, P.Coal[2]); k.Rect(x, y, x + 1, y + h, P.Coal[3]); k.HLine(x, x + 6, y + h, P.Stone[2]);
            RuneGlyph(k, x + 3, y + h / 2, glow);
        }
        k.Rect(22, 8, 42, 16, P.Stone[2]); k.HLine(22, 42, 16, P.Stone[4]);
        for (int x = 25; x <= 39; x += 7) RuneGlyph(k, x, 10, glow);
        foreach (var (x, y, h) in new[] { (6, 4, 22), (52, 4, 22) })
        {
            k.Rect(x, y, x + 7, y + h, P.Coal[2]); k.Rect(x, y, x + 1, y + h, P.Coal[3]); k.HLine(x, x + 7, y + h, P.Stone[2]);
            RuneGlyph(k, x + 4, y + h / 2, glow);
        }
        if (k.Level >= 1) { k.Torch(2, 4); k.Torch(60, 4); }
        k.Finish();
        // motes of rune-light rising off the altar
        for (int i = 0; i < 3 + k.Level; i++)
        {
            int x = 26 + i * 4, y = 18 + (i * 5 + k.Frame * 3) % 14;
            k.Px(x, y, i % 2 == 0 ? rune[3] : rune[2]);
        }
    }
}
