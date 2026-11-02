using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using P = PixelPalette;
using K = BuildingKit;

/// <summary>
/// Every building in pixel art, one look per upgrade level (upgrades visibly add to it), most as a
/// short loop (smoke, fire, banners, swaying crops). Pixels from the footprint's bottom-left
/// corner, 32 a tile; the canvas has room round the art for smoke and flags.
/// (Tools > Age of Stickman > Generate Building Art.)
/// </summary>
public static partial class PlaceholderArt
{
    private delegate void PixelDrawer(K k);

    private static readonly Dictionary<string, (PixelDrawer draw, int frames, float fps)> PixelBuildingArt =
        new Dictionary<string, (PixelDrawer, int, float)>
        {
            { "Campfire", (PxCampfire, 4, 7f) }, { "Hut", (PxHut, 1, 1f) }, { "ForagerCamp", (PxForagerCamp, 4, 3f) },
            { "LumberCamp", (PxLumberCamp, 4, 5f) }, { "Barracks", (PxBarracks, 4, 4f) }, { "Quarry", (PxQuarry, 4, 5f) },
            { "BronzeFoundry", (PxFoundry, 4, 6f) }, { "CharcoalKiln", (PxKiln, 4, 4f) }, { "House", (PxMudHouse, 4, 4f) },
            { "Farm", (PxFarm, 4, 3f) }, { "WarriorLodge", (PxWarriorLodge, 4, 4f) }, { "Watchtower", (PxWatchtower, 4, 4f) },
            { "AxeHall", (PxAxeHall, 4, 4f) }, { "ChariotYard", (PxChariotYard, 4, 4f) }, { "IronMine", (PxIronMine, 4, 5f) },
            { "Smithy", (PxSmithy, 4, 6f) }, { "StoneHouse", (PxStoneHouse, 4, 4f) }, { "Garrison", (PxGarrison, 4, 4f) },
            { "ArcheryRange", (PxArcheryRange, 4, 3f) }, { "StoneTower", (PxStoneTower, 4, 4f) }, { "SlingerCamp", (PxSlingerCamp, 4, 4f) },
            { "GoldMine", (PxGoldMine, 4, 5f) }, { "Mint", (PxMint, 4, 4f) }, { "Insula", (PxInsula, 4, 4f) },
            { "LegionFort", (PxLegionFort, 4, 4f) }, { "ArcherAcademy", (PxArcherAcademy, 4, 3f) }, { "Stables", (PxStables, 4, 3f) },
            { "BallistaTower", (PxBallistaTower, 4, 4f) }, { "SkirmisherCamp", (PxSkirmisherCamp, 4, 4f) }, { "ElephantPen", (PxElephantPen, 4, 3f) },
        };

    // Room round the art box for smoke, flags and overhangs.
    private const int MarginSide = 12, MarginBottom = 4, MarginTop = 40;

    [MenuItem("Tools/Age of Stickman/Generate Building Art")]
    public static void GenerateBuildingsOnly()
    {
        var database = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Resources/GameDatabase.asset");
        foreach (var def in database.buildings)
            if (def != null) GenerateBuildingLooks(def);
        AssetDatabase.SaveAssets();
        Debug.Log("Building art generated.");
    }

    private static void GenerateBuildingLooks(BuildingDefinition def)
    {
        if (!PixelBuildingArt.TryGetValue(def.id, out var art)) return;
        string dir = $"Buildings/{def.id}";
        ResetDir(dir);
        int artW = Mathf.RoundToInt(def.ArtSize.x * TilePx), artH = Mathf.RoundToInt(def.ArtSize.y * TilePx);
        int w = artW + MarginSide * 2, h = artH + MarginBottom + MarginTop;
        var pivot = new Vector2(MarginSide / (float)w, MarginBottom / (float)h);
        def.looks = new List<BuildingLook>();
        for (int level = 0; level <= def.upgrades.Count; level++)
        {
            var look = new BuildingLook { fps = art.fps };
            for (int f = 0; f < art.frames; f++)
            {
                var c = new PixelCanvas(w, h);
                var k = new K(c, MarginSide, MarginBottom, level, f, art.frames, def.id.GetHashCode());
                art.draw(k);
                look.frames.Add(SavePixel(c, $"{dir}/{def.id}_L{level}_F{f}.png", PixelsPerTile, pivot));
            }
            def.looks.Add(look);
        }
        def.icon = def.looks[0].frames[0];
        EditorUtility.SetDirty(def);
    }

    // ---------- Shared compositions ----------

    /// <summary>A walled building with a roof, door and windows. Returns the top of the walls.</summary>
    private static int Hall(K k, int x0, int x1, int y0, int wallH, K.Mat mat, K.RoofType roof, int roofH, int inset,
        int windows = 2, bool lit = true, bool arched = false, bool door = true)
    {
        int top = y0 + wallH;
        k.Wall(x0, y0, x1, top, mat);
        int mid = (x0 + x1) / 2;
        if (door) k.Door(mid - 3, y0 + 1, 7, Mathf.Min(12, wallH - 3), arched);
        for (int i = 0; i < windows; i++)
        {
            int wx = i % 2 == 0 ? x0 + 5 + (i / 2) * 10 : x1 - 9 - (i / 2) * 10;
            if (Mathf.Abs(wx + 2 - mid) < 8) continue;
            k.Window(wx, y0 + wallH / 2 - 1, 5, 5, lit);
        }
        k.Roof(x0 - 3, x1 + 3, top - 1, roofH, roof, inset);
        return top;
    }

    /// <summary>A pointed-stake palisade seen from the front.</summary>
    private static void Palisade(K k, int x0, int x1, int y, int h)
    {
        for (int x = x0; x <= x1; x += 3)
        {
            for (int yy = y; yy < y + h; yy++) { k.Px(x, yy, P.Wood[2]); k.Px(x + 1, yy, P.Wood[3]); k.Px(x + 2, yy, P.Wood[1]); }
            k.Px(x + 1, y + h, P.Wood[3]);
        }
        k.HLine(x0, x1 + 2, y + h / 2, P.Leather[1]);   // lashing
    }

    private static void Crenellated(K k, int x0, int x1, int y, Color32[] ramp)
    {
        for (int x = x0; x <= x1; x++)
        {
            k.Px(x, y, ramp[3]);
            if ((x - x0) % 6 < 3) { k.Px(x, y + 1, ramp[2]); k.Px(x, y + 2, ramp[2]); k.Px(x, y + 3, ramp[3]); }
        }
    }

    // ---------- Stone Age ----------

    private static void PxCampfire(K k)
    {
        k.Ground(4, 2, 60, 24);
        if (k.Level >= 1)
        {
            // hide drying rack at the back
            k.VLine(42, 14, 30, P.Wood[1]); k.VLine(56, 14, 30, P.Wood[1]); k.HLine(41, 57, 30, P.Wood[2]);
            k.Rect(44, 18, 54, 29, P.Leather[3]); k.Rect(45, 19, 53, 28, P.Leather[2]);
        }
        if (k.Level >= 2) { k.LongLogs(4, 14, 12, 1); k.Pot(18, 16); }
        // stone ring, logs and the fire
        for (int i = 0; i < 10; i++)
        {
            float a = i / 10f * Mathf.PI * 2;
            k.Ellipse(32 + Mathf.Cos(a) * 9, 9 + Mathf.Sin(a) * 4.5f, 2, 1.6f, P.Stone);
        }
        k.Line(25, 7, 39, 11, P.Wood[1], 2); k.Line(25, 11, 39, 7, P.Wood[2], 2);
        if (k.Level >= 1)
        {
            k.Line(20, 6, 22, 20, P.Wood[1]); k.Line(44, 6, 42, 20, P.Wood[1]); k.HLine(21, 43, 20, P.Wood[2]);
            k.Ellipse(32, 18.5f, 3.5f, 2, new[] { P.Red[0], P.Red[1], P.Leather[3], P.Leather[3] });   // roasting meat
        }
        k.LongLogs(6, 2, 10, 1); k.LongLogs(46, 2, 10, 1);
        k.Fire(27, 9, 11, 10 + k.Level);
        k.Finish();
        k.Smoke(33, 24, 1f, 150);
    }

    private static void PxHut(K k)
    {
        bool big = k.Level >= 1;
        int x0 = big ? 10 : 14, x1 = big ? 54 : 50;
        k.Ground(6, 2, 58, 12);
        if (k.Level >= 2) { k.Fence(2, 62, 2, 5); k.LogPile(48, 4, 2); }
        k.Wall(x0, 4, x1, big ? 22 : 18, big ? K.Mat.Mud : K.Mat.Wattle);
        if (k.Level >= 2) { k.Rect(x0, 4, x1, 6, P.Stone[2]); k.HLine(x0, x1, 6, P.Stone[4]); }
        // round the bottom corners off so it reads as a round hut
        for (int i = 0; i < 2; i++) { k.Erase(x0 + i, 4); k.Erase(x1 - i, 4); }
        k.Door(28, 5, 8, big ? 13 : 11, arched: true, open: true);
        k.Rect(28, 13, 30, 17, P.Leather[3]);   // rolled-up hide flap
        k.Cone(32, big ? 20 : 16, big ? 30 : 26, big ? 34 : 30, K.RoofType.Thatch);
        if (k.Level >= 1) { k.Pot(8, 4); k.Basket(52, 4, P.Berry); }
        k.Finish();
    }

    private static void PxForagerCamp(K k)
    {
        k.Ground(2, 2, 62, 26);
        // a lean-to of branches and leaves
        void LeanTo(int x0, int x1, int y)
        {
            k.VLine(x0, y, y + 16, P.Wood[1]); k.VLine(x1, y, y + 16, P.Wood[1]);
            for (int yy = 0; yy < 14; yy++)
                for (int x = x0 - 2; x <= x1 + 2; x++)
                    k.Px(x, y + 4 + yy, (x * 3 + yy * 5) % 7 == 0 ? P.Leaf[1] : yy > 10 ? P.Leaf[3] : P.Leaf[2]);
            k.HLine(x0 - 2, x1 + 2, y + 3, P.Leaf[0]);
        }
        LeanTo(10, 34, 14);
        if (k.Level >= 1) LeanTo(38, 56, 18);
        if (k.Level >= 2) k.Cone(50, 6, 11, 18, K.RoofType.Thatch);
        int n = 2 + k.Level * 2;
        for (int i = 0; i < n; i++) k.Basket(4 + i * 8, 2 + (i % 2) * 2, i % 3 == 0 ? P.Berry : i % 3 == 1 ? P.Gold : P.Leaf);
        k.Finish();
        // a bee or two buzzing round the berries
        int bx = 14 + Mathf.RoundToInt(k.Wave() * 4), by = 12 + Mathf.RoundToInt(k.Wave(0.25f) * 2);
        k.Px(bx, by, P.Gold[2]); k.Px(bx + 1, by, P.Ink);
    }

    private static void PxLumberCamp(K k)
    {
        k.Ground(2, 2, 62, 28, P.Dirt);
        if (k.Level >= 2) Hall(k, 34, 60, 16, 12, K.Mat.Planks, K.RoofType.Shingle, 10, 6, windows: 0, door: false);
        else
        {
            k.VLine(40, 14, 28, P.Wood[1]); k.VLine(58, 14, 28, P.Wood[1]);
            k.Roof(37, 61, 26, 6, K.RoofType.Thatch, 2);
        }
        k.LongLogs(36, 6, 18, 2 + k.Level);
        k.LogPile(4, 12, 3 + (k.Level >= 1 ? 1 : 0));
        if (k.Level >= 1) { k.Line(22, 18, 30, 26, P.Wood[1]); k.Line(30, 18, 22, 26, P.Wood[1]); k.HLine(20, 34, 24, P.Wood[3]); }
        // chopping stump with the axe rising and falling
        k.Rect(20, 3, 28, 7, P.Wood[2]); k.HLine(20, 28, 8, P.Wood[3]); k.Px(24, 8, P.Wood[1]);
        bool up = k.Frame % 2 == 0;
        if (up) { k.Line(27, 9, 31, 17, P.Wood[3]); k.Rect(30, 16, 32, 18, P.Steel[2]); }
        else { k.Line(27, 9, 33, 10, P.Wood[3]); k.Rect(23, 8, 25, 10, P.Steel[2]); k.Px(18, 9, P.Wood[3]); k.Px(31, 11, P.Wood[3]); }
        k.Finish();
    }

    private static void PxBarracks(K k)
    {
        k.Ground(4, 2, 92, 34);
        Palisade(k, 2, 92, 52, 16 + (k.Level >= 2 ? 4 : 0));
        var mat = k.Level >= 2 ? K.Mat.Planks : K.Mat.Logs;
        Hall(k, 8, 56, 34, 16, mat, K.RoofType.Thatch, 18, 10, windows: 2);
        if (k.Level >= 3) { k.Rect(8, 34, 56, 36, P.Stone[2]); k.HLine(8, 56, 36, P.Stone[4]); }
        // training dummy and weapon rack
        k.VLine(72, 12, 26, P.Wood[1]); k.HLine(66, 78, 22, P.Wood[2]); k.Ellipse(72, 28, 3, 3, P.Straw); k.Rect(69, 14, 75, 20, P.Straw[2]);
        k.SpearRack(80, 30, 3, axes: true);
        if (k.Level >= 1) k.Banner(62, 36, 22);
        if (k.Level >= 3) k.Banner(4, 52, 24);
        k.Barrel(12, 10); k.Crate(20, 8);
        k.Finish();
    }

    private static void PxQuarry(K k)
    {
        k.Ground(6, 2, 90, 54, P.Dirt);
        k.Mound(52, 62, 42, 18);
        k.Pit(14, 20, 66, 50);
        // cut steps in the pit wall
        for (int i = 0; i < 3; i++) k.HLine(18 + i * 6, 62 - i * 6, 26 + i * 7, P.Rock[3]);
        k.StonePile(70, 8, 4 + k.Level * 2);
        if (k.Level >= 1) k.Cart(4, 4, P.Stone);
        if (k.Level >= 2)
        {
            // a wooden crane over the pit
            k.Line(76, 30, 76, 66, P.Wood[2], 2); k.Line(76, 64, 44, 56, P.Wood[3], 2);
            k.Line(46, 56, 46, 40 + Mathf.RoundToInt(k.Wave() * 3), P.Linen[2]);
            k.Rect(43, 36 + Mathf.RoundToInt(k.Wave() * 3), 49, 40 + Mathf.RoundToInt(k.Wave() * 3), P.Stone[3]);
        }
        // a pick striking the rock
        bool up = k.Frame % 2 == 0;
        k.Line(36, 32, up ? 40 : 42, up ? 42 : 36, P.Wood[3]);
        k.Rect(up ? 38 : 41, up ? 41 : 35, up ? 42 : 44, up ? 43 : 37, P.Steel[2]);
        k.Finish();
        if (!up) { k.Px(46, 36, P.Stone[4]); k.Px(47, 38, P.Stone[4]); k.Px(45, 39, P.Stone[3]); }
    }

    // ---------- Bronze Age ----------

    private static void PxFoundry(K k)
    {
        k.Ground(2, 2, 94, 40);
        k.Wall(6, 8, 58, 30, K.Mat.Mud);
        k.FlatRoof(5, 59, 30, 12, P.Mud);
        k.Chimney(44, 36, 7, 24 + k.Level * 4, P.Mud);
        if (k.Level >= 1) k.Chimney(14, 36, 6, 16, P.Mud);
        k.Door(28, 9, 8, 12, arched: true, open: true);
        k.Window(12, 18, 6, 5); k.Window(46, 18, 6, 5);
        // the furnace: a domed kiln with a glowing mouth
        void Furnace(float cx)
        {
            k.Ellipse(cx, 16, 12, 13, P.Mud, l => l.y >= 4);
            float glow = 0.5f + 0.5f * k.Wave();
            var mouth = glow > 0.5f ? P.Glow : P.Fire;
            k.Rect((int)cx - 4, 5, (int)cx + 3, 11, mouth[2]); k.Rect((int)cx - 3, 5, (int)cx + 2, 8, mouth[3]);
        }
        if (k.Level >= 2) { Furnace(68); Furnace(86); }
        else Furnace(78);
        for (int i = 0; i < 2 + k.Level; i++) { k.Rect(8 + i * 6, 2, 12 + i * 6, 3, P.Bronze[2]); k.HLine(8 + i * 6, 12 + i * 6, 4, P.Bronze[3]); }
        k.Finish();
        k.Smoke(47, 62 + k.Level * 4, 1.2f);
        if (k.Level >= 1) k.Smoke(16, 54, 0.8f);
    }

    private static void PxKiln(K k)
    {
        k.Ground(2, 2, 62, 40, P.Dirt);
        void Mound(float cx, float cy, float r, bool brick)
        {
            k.Ellipse(cx, cy, r, r * 0.8f, brick ? P.Stone : P.Mud, l => l.y >= 3);
            if (brick) { k.Rect((int)cx - 3, 3, (int)cx + 3, 9, P.Coal[1]); k.Fire((int)cx - 2, 3, 5, 4); }
            else for (int i = 0; i < 5; i++) k.Px((int)(cx - r * 0.6f) + i * 3, (int)cy + (i % 2), P.Coal[2]);
        }
        k.LongLogs(40, 20, 16, 3);
        Mound(26, 14, 16, k.Level >= 2);
        if (k.Level >= 1) Mound(50, 10, 10, false);
        k.Finish();
        k.Smoke(26, 28, 1.1f);
        if (k.Level >= 1) k.Smoke(50, 19, 0.8f);
    }

    private static void PxMudHouse(K k)
    {
        k.Ground(4, 2, 60, 10);
        k.Wall(8, 4, 56, 26, K.Mat.Mud);
        k.FlatRoof(7, 57, 26, 16, P.Mud);
        if (k.Level >= 1)
        {
            k.Wall(30, 34, 54, 48, K.Mat.Mud);
            k.FlatRoof(29, 55, 48, 10, P.Mud);
            k.Window(38, 39, 5, 5, false);
        }
        k.Door(26, 5, 7, 12, arched: true);
        k.Window(12, 13, 5, 5, false); k.Window(44, 13, 5, 5, false);
        // ladder up to the roof
        k.VLine(4, 4, 30, P.Wood[2]); k.VLine(7, 4, 30, P.Wood[2]);
        for (int y = 6; y < 30; y += 4) k.HLine(4, 7, y, P.Wood[3]);
        if (k.Level >= 2)
        {
            // a striped awning and pots by the door
            for (int x = 20; x <= 40; x++) { k.Px(x, 20, (x / 3) % 2 == 0 ? P.Blue[2] : P.Linen[3]); k.Px(x, 19, (x / 3) % 2 == 0 ? P.Blue[1] : P.Linen[2]); }
            k.Pot(36, 2); k.Pot(42, 2);
        }
        k.Pot(14, 28);
        k.Finish();
        k.Smoke(18, 36, 0.8f, 120);
    }

    private static void PxFarm(K k)
    {
        // fields of wheat with a fence, a scarecrow and a granary at the back
        int rows = 9 + k.Level * 2;
        k.Rect(4, 5, 72, 6 + rows * 4, P.Dirt[2]);
        for (int r = rows - 1; r >= 0; r--) k.CropRow(5, 71, 6 + r * 4, 3, k.Level >= 2 && r % 3 == 0 ? P.Green : P.Straw);
        if (k.Level >= 1) { k.HLine(4, 72, 4, P.Water[2]); k.HLine(4, 72, 3, P.Water[3]); }
        if (k.Level >= 2) Hall(k, 72, 94, 58, 14, K.Mat.Planks, K.RoofType.Tile, 12, 5, windows: 0);
        else { k.Wall(76, 60, 92, 70, K.Mat.Wattle); k.Cone(84, 69, 10, 14, K.RoofType.Thatch); }
        // scarecrow
        k.VLine(40, 20, 34, P.Wood[1]); k.HLine(35, 45, 30, P.Wood[2]); k.Ellipse(40, 36, 2.5f, 2.5f, P.Straw);
        k.Rect(37, 24, 43, 30, P.Red[2]); k.HLine(36, 44, 38, P.Leather[2]);
        k.Fence(2, 94, 0, 5);
        k.Hay(76, 8); if (k.Level >= 1) k.Hay(84, 14);
        k.Finish();
    }

    private static void PxWarriorLodge(K k)
    {
        k.Ground(8, 2, 88, 34);
        Hall(k, 10, 86, 36, 22, K.Mat.Mud, k.Level >= 2 ? K.RoofType.Tile : K.RoofType.Thatch, 20, 14, windows: 4, arched: true);
        if (k.Level >= 2) { k.Rect(10, 36, 86, 38, P.Stone[2]); k.HLine(10, 86, 38, P.Stone[4]); }
        k.ShieldDecor(24, 50, P.Leather); k.ShieldDecor(72, 50, P.Leather);
        if (k.Level >= 3) { k.ShieldDecor(34, 52, P.Bronze); k.ShieldDecor(62, 52, P.Bronze); }
        k.SpearRack(6, 12, 4); k.SpearRack(74, 12, 4);
        if (k.Level >= 1) k.Banner(44, 14, 20);
        k.Crate(30, 4); k.Barrel(60, 4);
        k.Finish();
    }

    /// <summary>A crewed tower's deck: the parapet's top row is where the crew's waist sits.</summary>
    private static void TowerDeck(K k, int x0, int x1, int parapetTop, K.Mat mat, bool crenels)
    {
        k.Wall(x0, parapetTop - 7, x1, parapetTop, mat);
        if (crenels)
        {
            // merlons only at the corners, so the crew stays in view
            k.Rect(x0, parapetTop + 1, x0 + 4, parapetTop + 4, P.Stone[3]);
            k.Rect(x1 - 4, parapetTop + 1, x1, parapetTop + 4, P.Stone[2]);
        }
    }

    private static void PxWatchtower(K k)
    {
        int top = k.Level >= 1 ? 53 : 46;   // matches the crew offset (1.45 cells, +0.2 from level 1)
        k.Ground(6, 2, 58, 14);
        if (k.Level >= 2) { k.Rect(12, 2, 52, 6, P.Stone[2]); k.HLine(12, 52, 6, P.Stone[4]); }
        // four legs and cross braces
        foreach (int x in new[] { 14, 48 }) { k.VLine(x, 2, top - 8, P.Wood[2]); k.VLine(x + 1, 2, top - 8, P.Wood[1]); }
        k.Line(15, 8, 48, top - 12, P.Wood[1]); k.Line(48, 8, 15, top - 12, P.Wood[1]);
        // ladder
        k.VLine(28, 2, top - 8, P.Wood[3]); k.VLine(32, 2, top - 8, P.Wood[3]);
        for (int y = 5; y < top - 8; y += 4) k.HLine(28, 32, y, P.Wood[2]);
        TowerDeck(k, 10, 54, top, K.Mat.Logs, false);
        if (k.Level >= 3) { k.Banner(52, top, 14); }
        k.Finish();
        if (k.Level >= 3) k.Fire(6, top + 1, 4, 4);
    }

    private static void PxAxeHall(K k)
    {
        k.Ground(2, 2, 94, 30);
        int top = Hall(k, 10, 86, 20, 20, K.Mat.Logs, K.RoofType.Thatch, 24, 16, windows: 2);
        // bull horns over the door
        k.Line(41, top + 4, 37, top + 9, P.Bone[3]); k.Line(55, top + 4, 59, top + 9, P.Bone[3]); k.Rect(44, top + 2, 52, top + 5, P.Bone[2]);
        k.SpearRack(66, 2, 4, axes: true);
        k.Rect(14, 2, 22, 6, P.Wood[2]); k.HLine(14, 22, 7, P.Wood[3]);   // chopping block
        if (k.Level >= 1) k.Banner(4, 20, 22);
        if (k.Level >= 2) k.SpearRack(28, 2, 3, axes: true);
        if (k.Level >= 3) k.Banner(88, 20, 22);
        k.Finish();
    }

    private static void Chariot(K k, int x, int y)
    {
        k.Rect(x, y + 4, x + 10, y + 10, P.Wood[2]); k.HLine(x, x + 10, y + 10, P.Bronze[3]); k.HLine(x, x + 10, y + 7, P.Blue[2]);
        k.Ellipse(x + 4, y + 4, 4.5f, 4.5f, P.Wood); k.Px(x + 4, y + 4, P.Wood[0]);
        k.Line(x + 10, y + 6, x + 22, y + 9, P.Wood[1]);
    }

    private static void PxChariotYard(K k)
    {
        k.Ground(4, 2, 92, 40);
        Hall(k, 6, 44, 34, 16, K.Mat.Planks, k.Level >= 3 ? K.RoofType.Tile : K.RoofType.Thatch, 14, 8, windows: 0);
        k.Fence(46, 94, 30, 6);
        Chariot(k, 52, 10);
        if (k.Level >= 2) Chariot(k, 20, 4);
        k.Hay(78, 34); k.Trough(4, 4, 10);
        if (k.Level >= 1) k.Banner(88, 8, 22);
        k.Finish();
    }

    // ---------- Iron Age ----------

    private static void PxIronMine(K k)
    {
        k.Ground(8, 2, 88, 26, P.Dirt);
        k.Mound(50, 44, 46, 28);
        k.MineEntrance(38, 30, 20, 20, stone: k.Level >= 2);
        k.Rails(44, 52, 2);
        k.Line(46, 6, 48, 30, P.Iron[2]); k.Line(51, 6, 53, 30, P.Iron[2]);
        k.Cart(40, 6, P.Iron);
        for (int i = 0; i < 6 + k.Level * 2; i++) k.Px(70 + (i % 4) * 3, 4 + (i / 4) * 2, i % 2 == 0 ? P.Hex("a4503a") : P.Iron[2]);
        if (k.Level >= 1) k.MineEntrance(12, 22, 14, 14);
        k.Torch(34, 30); k.Torch(60, 30);
        if (k.Level >= 2) k.Chimney(80, 30, 6, 22);
        k.Finish();
        if (k.Level >= 2) k.Smoke(83, 54, 0.9f);
    }

    private static void PxSmithy(K k)
    {
        k.Ground(2, 2, 94, 34);
        int top = Hall(k, 6, 62, 22, 18, K.Mat.Stone, k.Level >= 1 ? K.RoofType.Tile : K.RoofType.Shingle, 18, 10, windows: 1);
        k.Chimney(46, top + 4, 7, 22, P.Stone);
        // open forge with a glowing hearth, anvil and quench barrel
        k.Rect(66, 4, 90, 22, P.Stone[2]); k.HLine(66, 90, 22, P.Stone[4]);
        k.Rect(70, 8, 86, 16, P.Coal[1]);
        float g = 0.5f + 0.5f * k.Wave();
        k.Rect(71, 9, 85, 12, g > 0.5f ? P.Glow[3] : P.Glow[2]); k.Rect(73, 9, 83, 10, P.Fire[3]);
        k.Anvil(30, 4); k.Barrel(42, 4);
        k.SpearRack(8, 4, 3 + k.Level);
        if (k.Level >= 2) { k.VLine(64, 30, 44, P.Wood[1]); k.Rect(64, 38, 72, 44, P.Wood[2]); k.Anvil(65, 39); }   // hanging sign
        k.Finish();
        k.Smoke(49, top + 28, 1f);
        if (k.Frame % 2 == 0) { k.Px(34, 12, P.Fire[3]); k.Px(37, 13, P.Glow[3]); }   // sparks off the anvil
    }

    private static void PxStoneHouse(K k)
    {
        k.Ground(4, 2, 60, 8);
        int wallH = k.Level >= 1 ? 28 : 20;
        int top = Hall(k, 8, 56, 4, wallH, K.Mat.Stone, K.RoofType.Slate, 20, 12, windows: k.Level >= 1 ? 4 : 2);
        k.Chimney(44, top + 6, 6, 14);
        if (k.Level >= 2) { k.Fence(0, 6, 2, 5); k.Fence(58, 64, 2, 5); k.FlowerBox(13, 12, 7); k.FlowerBox(43, 12, 7); }
        k.Finish();
        k.Smoke(47, top + 22, 0.9f);
    }

    private static void PxGarrison(K k)
    {
        k.Ground(2, 2, 94, 30);
        k.Wall(8, 6, 88, 40, K.Mat.Stone);
        Crenellated(k, 8, 88, 41, P.Stone);
        // gate
        k.Rect(40, 6, 56, 24, P.Coal[1]); k.Door(42, 7, 13, 16, arched: true);
        k.Rect(14, 24, 16, 30, P.Coal[2]); k.Rect(80, 24, 82, 30, P.Coal[2]);   // arrow slits
        // corner tower(s)
        void Tower(int x)
        {
            k.Wall(x, 6, x + 16, 58, K.Mat.Stone);
            Crenellated(k, x - 1, x + 17, 59, P.Stone);
            k.Rect(x + 7, 44, x + 8, 50, P.Coal[2]);
        }
        Tower(72);
        if (k.Level >= 2) Tower(8);
        if (k.Level >= 1) k.Banner(78, 62, 16);
        if (k.Level >= 3) { k.Banner(14, 62, 16); k.HLine(8, 88, 6, P.Gold[2]); }
        k.Finish();
    }

    private static void PxArcheryRange(K k)
    {
        k.Ground(4, 4, 92, 60, P.Dirt);
        int targets = 2 + Mathf.Min(k.Level, 2);
        for (int i = 0; i < targets; i++) k.Target(12 + i * 20, 62);
        Hall(k, 64, 92, 40, 12, K.Mat.Planks, K.RoofType.Shingle, 10, 6, windows: 0);
        k.Fence(4, 92, 28, 5);   // the shooting line
        k.Hay(8, 44); k.Hay(48, 46);
        for (int i = 0; i < 3; i++) k.VLine(70 + i * 3, 6, 18, P.Wood[3]);   // arrows in a barrel
        k.Barrel(69, 4);
        if (k.Level >= 1) k.Banner(4, 4, 22);
        if (k.Level >= 3) { k.Rect(20, 4, 56, 6, P.Wood[2]); k.Roof(18, 58, 18, 6, K.RoofType.Shingle, 2); k.VLine(20, 6, 18, P.Wood[1]); k.VLine(56, 6, 18, P.Wood[1]); }
        k.Finish();
    }

    private static void PxStoneTower(K k)
    {
        const int top = 46;
        k.Ground(6, 2, 58, 10);
        k.Wall(12, 2, 52, top - 8, K.Mat.Stone);
        k.Door(28, 3, 8, 12, arched: true);
        k.Rect(31, 24, 32, 30, P.Coal[2]);
        k.Wall(9, top - 8, 55, top - 6, K.Mat.Stone);   // corbelled ledge
        TowerDeck(k, 10, 54, top, K.Mat.Stone, true);
        if (k.Level >= 1) k.Banner(4, 2, 30);
        if (k.Level >= 3) k.HLine(9, 55, top - 7, P.Gold[2]);
        k.Finish();
        if (k.Level >= 2) { k.Fire(10, top + 5, 4, 4); k.Fire(50, top + 5, 4, 4); }
    }

    private static void PxSlingerCamp(K k)
    {
        k.Ground(2, 2, 94, 60);
        k.Tent(24, 30, 16, 22, P.Linen);
        k.Tent(64, 34, 14, 20, k.Level >= 2 ? P.Blue : P.Linen);
        for (int i = 0; i < 3 + k.Level; i++) k.Ellipse(80 + (i % 3) * 4, 6 + (i / 3) * 3, 2, 1.6f, P.Stone);
        k.VLine(46, 10, 26, P.Wood[1]); k.Ellipse(46, 28, 3, 3, P.Straw);   // a target post
        k.Fire(8, 6, 6, 6);
        if (k.Level >= 1) k.Banner(88, 24, 20);
        k.Finish();
        k.Smoke(11, 14, 0.7f, 120);
    }

    // ---------- Classical Age ----------

    private static void PxGoldMine(K k)
    {
        k.Ground(8, 2, 88, 26, P.Dirt);
        k.Mound(50, 44, 46, 28);
        for (int i = 0; i < 14; i++)
        {
            int x = 14 + k.R(0, 70), y = 34 + k.R(0, 40);
            if (k.Get(x, y).a > 200) { k.Px(x, y, P.Gold[2]); if (i % 3 == 0) k.Px(x + 1, y + 1, P.Gold[3]); }
        }
        k.MineEntrance(38, 30, 20, 20, stone: true);
        k.Line(46, 6, 48, 30, P.Iron[2]); k.Line(51, 6, 53, 30, P.Iron[2]);
        k.Rails(44, 52, 2);
        k.Cart(40, 6, P.Gold);
        k.Torch(34, 30); k.Torch(60, 30);
        if (k.Level >= 1) { k.Line(76, 10, 76, 50, P.Wood[2], 2); k.Line(76, 48, 60, 44, P.Wood[3], 2); k.Line(62, 44, 62, 34, P.Linen[2]); }
        if (k.Level >= 2) k.Banner(84, 6, 22);
        k.Finish();
        if (k.Frame % 2 == 1) { k.Px(44, 16, P.White); k.Px(48, 15, P.Gold[3]); }   // glint on the gold
    }

    private static void PxMint(K k)
    {
        // a small marble temple of coin: steps, columns, a pediment
        k.Rect(4, 2, 60, 4, P.Marble[1]); k.Rect(6, 5, 58, 7, P.Marble[2]); k.HLine(6, 58, 7, P.Marble[3]);
        k.Wall(10, 8, 54, 44, K.Mat.Marble);
        k.Door(28, 8, 8, 16, arched: true);
        for (int i = 0; i < 4; i++) k.Column(8 + i * 14 + (i >= 2 ? 4 : 0), 8, 36);
        k.Rect(6, 45, 58, 48, P.Marble[3]); k.HLine(6, 58, 45, P.Marble[1]);
        // pediment
        for (int y = 49; y < 62; y++)
        {
            int half = 27 - (y - 49) * 2;
            for (int x = 32 - half; x <= 32 + half; x++) k.Px(x, y, x < 32 - half + 2 ? P.Tile[3] : x > 32 + half - 2 ? P.Tile[1] : P.Tile[2]);
        }
        k.Ellipse(32, 53, 3, 3, P.Gold);
        // coin stacks
        for (int i = 0; i < 2 + k.Level; i++) for (int h = 0; h < 3 + i % 2; h++) k.HLine(40 + i * 5, 43 + i * 5, 2 + h, h % 2 == 0 ? P.Gold[2] : P.Gold[3]);
        if (k.Level >= 1) k.HLine(6, 58, 48, P.Gold[2]);
        if (k.Level >= 2) { k.Banner(0, 2, 34); }
        k.Finish();
        if (k.Frame % 2 == 0) k.Px(41, 7, P.White);
    }

    private static void PxInsula(K k)
    {
        int floors = 2 + (k.Level >= 1 ? 1 : 0);
        int y = 2;
        for (int f = 0; f < floors; f++)
        {
            k.Wall(6, y, 58, y + 16, K.Mat.Plaster);
            for (int i = 0; i < 3; i++) k.Window(12 + i * 16, y + 6, 6, 6, f > 0);
            if (f > 0) k.HLine(6, 58, y, P.Wood[1]);   // balcony
            y += 16;
        }
        k.Door(28, 3, 8, 11, arched: true);
        k.Roof(3, 61, y, 18, K.RoofType.Tile, 10);
        if (k.Level >= 2)
        {
            for (int x = 6; x <= 58; x++) { k.Px(x, 15, (x / 4) % 2 == 0 ? P.Red[2] : P.Linen[3]); k.Px(x, 14, (x / 4) % 2 == 0 ? P.Red[1] : P.Linen[2]); }
            k.Pot(4, 1); k.Crate(52, 1);
        }
        k.Finish();
        // washing on a line, swaying
        int sway = Mathf.RoundToInt(k.Wave());
        k.HLine(60, 66, y - 4, P.Linen[1]);
        k.Rect(61 + sway, y - 8, 63 + sway, y - 5, P.Blue[3]);
    }

    private static void PxLegionFort(K k)
    {
        k.Ground(2, 2, 94, 36);
        k.Wall(10, 6, 86, 34, K.Mat.Stone);
        Crenellated(k, 10, 86, 35, P.Stone);
        k.Rect(40, 6, 56, 24, P.Coal[1]); k.Door(42, 7, 13, 16, arched: true);
        void Tower(int x)
        {
            k.Wall(x, 6, x + 18, 52, K.Mat.Stone);
            k.Roof(x - 2, x + 20, 52, 12, K.RoofType.Tile, 8);
            k.Rect(x + 8, 38, x + 9, 44, P.Coal[2]);
        }
        Tower(4); Tower(74);
        // the eagle standard by the gate
        k.VLine(60, 4, 34, P.Wood[2]);
        k.Rect(57, 30, 63, 33, P.Gold[2]); k.Px(56, 33, P.Gold[3]); k.Px(64, 33, P.Gold[3]); k.Px(60, 35, P.Gold[3]);
        k.Rect(58, 24, 62, 29, P.Red[2]);
        if (k.Level >= 1) k.Banner(10, 40, 18, P.Red);
        if (k.Level >= 2) k.Banner(80, 66, 14, P.Red);
        if (k.Level >= 3) k.HLine(10, 86, 6, P.Gold[2]);
        k.Finish();
    }

    private static void PxArcherAcademy(K k)
    {
        k.Ground(2, 2, 94, 80, P.Dirt);
        // a colonnade along the back
        k.Wall(10, 54, 86, 74, K.Mat.Marble);
        for (int i = 0; i < 6; i++) k.Column(12 + i * 14, 54, 20);
        k.Roof(6, 90, 74, 12, K.RoofType.Tile, 6);
        for (int i = 0; i < 3 + Mathf.Min(k.Level, 1); i++) k.Target(10 + i * 22, 28);
        k.Fence(4, 92, 12, 5);
        k.Barrel(84, 2);
        if (k.Level >= 1) k.Banner(2, 4, 22);
        if (k.Level >= 2) k.Banner(90, 30, 20);
        if (k.Level >= 3) k.HLine(6, 90, 74, P.Gold[2]);
        k.Finish();
    }

    private static void PxStables(K k)
    {
        k.Ground(2, 2, 94, 30);
        int top = Hall(k, 6, 90, 14, 18, K.Mat.Planks, k.Level >= 2 ? K.RoofType.Tile : K.RoofType.Shingle, 16, 10, windows: 0, door: false);
        // stall doors with horses looking out
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
        k.Hay(8, 2); k.Trough(70, 2, 12);
        if (k.Level >= 1) k.Banner(92, 4, 22);
        k.Finish();
    }

    private static void PxBallistaTower(K k)
    {
        const int top = 46;
        k.Ground(6, 2, 58, 10);
        k.Wall(12, 2, 52, top - 8, K.Mat.Stone);
        k.HLine(12, 52, 14, P.Marble[3]); k.HLine(12, 52, 28, P.Marble[3]);
        k.Door(28, 3, 8, 11, arched: true);
        k.Wall(9, top - 8, 55, top - 6, K.Mat.Marble);
        TowerDeck(k, 10, 54, top, K.Mat.Stone, true);
        if (k.Level >= 1) k.Banner(54, 2, 30, P.Red);
        if (k.Level >= 2) k.HLine(9, 55, top - 7, P.Gold[2]);
        k.Finish();
    }

    private static void PxSkirmisherCamp(K k)
    {
        k.Ground(2, 2, 94, 60);
        k.Tent(26, 30, 16, 22, P.Straw);
        k.Tent(66, 34, 14, 20, k.Level >= 2 ? P.Blue : P.Straw);
        k.SpearRack(80, 4, 3 + k.Level);
        k.Target(40, 8);
        k.Fire(10, 6, 6, 6);
        if (k.Level >= 1) k.Banner(4, 24, 22);
        k.Finish();
        k.Smoke(13, 14, 0.7f, 120);
    }

    private static void PxElephantPen(K k)
    {
        k.Ground(2, 2, 94, 62, P.Dirt);
        // a big roofed shelter on posts at the back
        foreach (int x in new[] { 10, 40, 70 }) { k.VLine(x, 34, 52, P.Wood[2]); k.VLine(x + 1, 34, 52, P.Wood[1]); }
        k.Roof(4, 82, 50, 14, k.Level >= 2 ? K.RoofType.Tile : K.RoofType.Thatch, 8);
        k.Hay(16, 36); k.Hay(48, 36);
        k.Trough(54, 10, 20);
        // a heavy log fence
        for (int x = 2; x <= 94; x += 6) { k.Rect(x, 2, x + 2, 14, P.Wood[2]); k.Px(x + 1, 14, P.Wood[3]); }
        k.HLine(2, 94, 11, P.Wood[3]); k.HLine(2, 94, 6, P.Wood[2]);
        if (k.Level >= 1) k.Banner(88, 16, 22);
        if (k.Level >= 3) k.HLine(4, 82, 50, P.Gold[2]);
        k.Finish();
    }
}
