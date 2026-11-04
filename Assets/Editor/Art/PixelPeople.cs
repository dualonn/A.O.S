using System.Collections.Generic;
using UnityEngine;
using P = PixelPalette;
using K = BuildingKit;

/// <summary>
/// People in pixel art: the workshops of the consumer goods (potter's kiln, weaver's hut, brewery,
/// scriptorium, coffee plantation, radio works, holo studio), the schools from a house of tablets
/// to the Omniversity, the goods' icons, and the decorations comfortable homes put out in front.
/// Upgrades add more of everything.
/// </summary>
public static partial class PlaceholderArt
{
    private static readonly Dictionary<string, (PixelDrawer draw, int frames, float fps)> PeopleBuildingArt =
        new Dictionary<string, (PixelDrawer, int, float)>
        {
            { "PplPotter", (PxPplPotter, 4, 4f) }, { "PplWeaver", (PxPplWeaver, 4, 4f) }, { "PplBrewery", (PxPplBrewery, 4, 3f) },
            { "PplScriptorium", (PxPplScriptorium, 4, 2f) }, { "PplPlantation", (PxPplPlantation, 4, 3f) },
            { "PplRadioWorks", (PxPplRadioWorks, 4, 5f) }, { "PplHoloStudio", (PxPplHoloStudio, 4, 5f) },
            { "PplTabletHouse", (PxPplTabletHouse, 4, 3f) }, { "PplAcademy", (PxPplAcademy, 4, 2f) },
            { "PplUniversity", (PxPplUniversity, 4, 2f) }, { "PplRoyalAcademy", (PxPplRoyalAcademy, 4, 2f) },
            { "PplPolytechnic", (PxPplPolytechnic, 4, 3f) }, { "PplRadiumInstitute", (PxPplRadium, 4, 6f) },
            { "PplResearchInstitute", (PxPplResearch, 4, 4f) }, { "PplArcaneCollege", (PxPplArcane, 4, 4f) },
            { "PplNeuralAcademy", (PxPplNeural, 4, 4f) }, { "PplOmniversity", (PxPplOmniversity, 4, 3f) },
        };

    private static readonly Color32[] PplClay = { P.Hex("5a2a16"), P.Hex("8e4626"), P.Hex("c06a3a"), P.Hex("e8a070") };
    private static readonly Color32[] PplCopper = { P.Hex("4a2412"), P.Hex("8a4a22"), P.Hex("c87a3a"), P.Hex("f2b072") };
    private static readonly Color32[] PplCoffee = { P.Hex("2a160c"), P.Hex("4e2c18"), P.Hex("7a4a2a"), P.Hex("a8724a") };
    private static readonly Color32[] PplBush = { P.Hex("1a3a1c"), P.Hex("2e5e2a"), P.Hex("4a8a3a"), P.Hex("c83a2a") };
    private static readonly Color32[] PplIvy = { P.Hex("1c3a22"), P.Hex("2e5a32"), P.Hex("4a8048"), P.Hex("7ab06a") };
    private static readonly Color32[] PplChrome = { P.Hex("3a4048"), P.Hex("6a7480"), P.Hex("a8b2bc"), P.Hex("e6ecf2") };

    /// <summary>Draws a people building's looks (one per upgrade level), like the other buildings'.</summary>
    private static void GeneratePeopleLooks(BuildingDefinition def)
    {
        if (!PeopleBuildingArt.TryGetValue(def.id, out var art)) return;
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
        UnityEditor.EditorUtility.SetDirty(def);
    }

    // ---------- Workshops ----------

    private static void PxPplPotter(K k)
    {
        // A mud-brick workshop with a wheel under a reed awning, a domed kiln glowing, pots drying.
        k.Ground(2, 2, 61, 50, P.Dirt);
        int top = Hall(k, 4, 34, 18, 18, K.Mat.Mud, K.RoofType.Thatch, 14, 8, 1, true);
        // the kiln: a clay dome with a fire mouth and smoke
        k.Ellipse(48, 6, 11, 16, PplClay, l => l.y >= 6);
        k.HLine(38, 58, 6, PplClay[0]);
        k.Rect(45, 8, 51, 13, P.Coal[1]);
        k.Fire(45, 8, 6, 4);
        k.Smoke(48, 34, 0.7f, 130);
        // the wheel and the potter's bench
        k.Ellipse(18, 9, 5, 2, P.Wood);
        k.Ellipse(18, 11, 3, 2.5f, PplClay);
        k.Rect(24, 6, 30, 8, P.Wood[2]);
        // pots drying in a row
        for (int i = 0; i < 2 + k.Level * 2; i++) k.Pot(4 + i * 6 + (i > 3 ? 30 : 0), 2, i % 2 == 0 ? PplClay : P.Tile);
        if (k.Level >= 1) { k.Rect(6, top + 1, 32, top + 2, P.Straw[2]); k.Pot(26, 12, P.Mud); }
        if (k.Level >= 2) k.Banner(2, 18, 22, PplClay);
        k.Finish();
    }

    private static void PxPplWeaver(K k)
    {
        // A wattle hut, an upright loom outside, and dyed cloth drying on a line.
        k.Ground(2, 2, 61, 50, P.Grass);
        Hall(k, 30, 60, 20, 16, K.Mat.Wattle, K.RoofType.Thatch, 14, 8, 1, true);
        // the loom: two posts, a beam and coloured warp threads
        k.VLine(6, 4, 26, P.Wood[1]); k.VLine(22, 4, 26, P.Wood[1]);
        k.HLine(5, 23, 26, P.Wood[2]); k.HLine(5, 23, 6, P.Wood[2]);
        for (int x = 8; x <= 20; x++) k.VLine(x, 7, 25, x % 3 == 0 ? P.Red[2] : x % 3 == 1 ? P.Linen[2] : P.Blue[2]);
        k.Rect(8, 7, 20, 12, P.Red[1]);
        // the drying line, cloth swaying
        int lines = 1 + k.Level;
        for (int l = 0; l < lines; l++)
        {
            int y = 44 + l * 4 - (l > 0 ? 10 : 0);
            k.HLine(4, 30, y, P.Wood[2]);
            for (int i = 0; i < 4; i++)
            {
                var col = (i + l) % 3 == 0 ? P.Gold : (i + l) % 3 == 1 ? P.Purple : P.Green;
                int sway = (k.Frame + i) % 2;
                k.Rect(5 + i * 6, y - 7 + sway, 9 + i * 6, y - 1, col[2]);
                k.VLine(5 + i * 6, y - 7 + sway, y - 1, col[1]);
            }
        }
        k.Basket(36, 2, P.Linen);
        if (k.Level >= 1) k.Basket(44, 2, P.Fur);
        k.Finish();
    }

    private static void PxPplBrewery(K k)
    {
        // A stone-and-timber brewhouse with a copper kettle steaming, barrels stacked, hop vines.
        k.Ground(2, 2, 61, 50, P.Dirt);
        int top = Hall(k, 6, 58, 10, 22, K.Mat.Stone, K.RoofType.Shingle, 18, 10, 2, true, true);
        k.Chimney(46, top + 4, 5, 14, P.Stone);
        k.Smoke(48, top + 22, 0.8f, 140);
        // the copper kettle at the side door
        k.Ellipse(14, 8, 6, 5, PplCopper);
        k.Rect(9, 2, 19, 5, P.Stone[1]);
        k.Smoke(14, 16, 0.5f, 110);
        // barrels and hops
        for (int i = 0; i < 2 + k.Level; i++) k.Barrel(40 + (i % 3) * 6, 2 + (i / 3) * 7);
        for (int y = 12; y < top; y += 3) k.Px(8 + (y % 2), y, P.Leaf[2]);
        if (k.Level >= 2)
        {
            // a tavern sign: a gold tankard on a board
            k.HLine(28, 38, top - 2, P.Wood[1]);
            k.Rect(30, top - 10, 36, top - 3, P.Wood[2]);
            k.Rect(32, top - 8, 34, top - 5, P.Gold[2]);
        }
        k.Finish();
    }

    private static void PxPplScriptorium(K k)
    {
        // A stone hall with tall lit windows, a bell-cote and copyists' desks; later a printing press.
        k.Ground(2, 2, 61, 50, P.Dirt);
        int top = Hall(k, 6, 58, 8, 26, K.Mat.Stone, K.RoofType.Slate, 20, 10, 0, true, true);
        for (int i = 0; i < 3; i++) if (i != 1) { k.Rect(12 + i * 16, 16, 17 + i * 16, 27, P.Stone[1]); k.Rect(13 + i * 16, 17, 16 + i * 16, 26, P.Glow[2]); k.Px(14 + i * 16, 26, P.Glow[3]); }
        // bell-cote
        k.Rect(28, top + 14, 36, top + 22, P.Stone[2]);
        k.Rect(30, top + 15, 34, top + 19, P.Coal[1]);
        k.Ellipse(32, top + 17, 1.5f, 1.5f, P.Gold);
        // an open book painted over the door
        k.Rect(27, 25, 37, 29, P.Linen[3]); k.VLine(32, 25, 29, P.Wood[1]);
        for (int x = 28; x <= 36; x += 2) if (x != 32) k.Px(x, 27, P.Ink);
        if (k.Level >= 1) k.Banner(2, 10, 28, P.Red);
        if (k.Level >= 2)
        {
            // a printing press under a lean-to
            k.Rect(44, 2, 58, 4, P.Wood[1]);
            k.VLine(46, 4, 12, P.Wood[2]); k.VLine(56, 4, 12, P.Wood[2]); k.HLine(45, 57, 12, P.Wood[3]);
            k.Rect(49, 5, 53, 8, P.Iron[2]); k.VLine(51, 8, 11, P.Iron[3]);
        }
        k.Finish();
    }

    private static void PxPplPlantation(K k)
    {
        // Rows of coffee bushes heavy with red cherries, sugar cane, a drying floor of beans and a sack store.
        k.Ground(2, 2, 93, 60, P.Dirt);
        int rows = 4 + k.Level;
        for (int r = 0; r < rows; r++)
            for (int x = 7 + (r % 2) * 4; x <= 54; x += 8)
            {
                int y = 58 - r * 7;
                k.Ellipse(x, y, 3.5f, 2.8f, new[] { PplBush[0], PplBush[1], PplBush[2], PplIvy[3] });
                k.Px(x - 1, y, PplBush[3]); k.Px(x + 1, y + 1, PplBush[3]);
            }
        // the drying floor
        k.Rect(60, 4, 92, 20, P.Stone[2]);
        for (int x = 62; x <= 90; x++) for (int y = 6; y <= 18; y++) if ((x * 3 + y * 5) % 4 != 0) k.Px(x, y, (x + y) % 3 == 0 ? PplCoffee[3] : PplCoffee[2]);
        // a little store with a tile roof
        Hall(k, 64, 92, 24, 14, K.Mat.Plaster, K.RoofType.Tile, 12, 6, 1, true);
        TradeSacks(k, 6, 4, 2 + k.Level);
        k.Finish();
    }

    private static void PxPplRadioWorks(K k)
    {
        // A chrome-and-stucco factory with a giant radio dial on its front and a mast blinking on top.
        k.Ground(2, 2, 93, 40, P.Dirt);
        ModernistWall(k, 6, 6, 90, 40);
        k.Rect(6, 40, 90, 43, PplChrome[2]);
        // the dial: a round glowing face with a needle
        k.Ellipse(30, 24, 11, 11, PplChrome);
        k.Ellipse(30, 24, 9, 9, P.Glow);
        for (int i = 0; i < 8; i++) k.Px(Mathf.RoundToInt(30 + Mathf.Cos(i * 0.785f) * 7), Mathf.RoundToInt(24 + Mathf.Sin(i * 0.785f) * 7), P.Ink);
        float a = 0.5f + k.Frame * 0.6f;
        k.Line(30, 24, 30 + Mathf.Cos(a) * 7, 24 + Mathf.Sin(a) * 7, P.Red[2]);
        // speaker grille and windows
        for (int y = 10; y <= 34; y += 3) k.HLine(46, 60, y, PplChrome[1]);
        for (int i = 0; i < 3; i++) k.Window(66 + i * 8, 22, 5, 9, true);
        k.Door(70, 6, 10, 12);
        // the mast, its light blinking
        k.VLine(80, 43, 72, PplChrome[1]); k.Line(76, 43, 80, 64, PplChrome[1]); k.Line(84, 43, 80, 64, PplChrome[1]);
        k.Px(80, 73, k.Frame % 2 == 0 ? P.Red[3] : P.Red[1]);
        if (k.Level >= 1) CulBulbs(k, 8, 88, 41);
        if (k.Level >= 2) { k.Rect(10, 44, 26, 52, P.Red[2]); k.Rect(12, 46, 24, 50, P.Gold[3]); }   // a lit sign on the roof
        k.Finish();
    }

    private static void PxPplHoloStudio(K k)
    {
        // A sun-white studio under a glass dome, a holo-stage outside casting whole worlds of light.
        k.Ground(2, 2, 93, 40, P.Sand);
        WhiteStone(k, 6, 6, 62, 34);
        GlassDome(k, 34, 34, 22, 18);
        SunWindows(k, 10, 58, 14, 8, 8);
        k.Door(28, 6, 10, 10);
        // the holo-stage
        k.Ellipse(78, 8, 12, 4, PplChrome);
        Holo(k, 68, 12, 88, 40);
        k.Ellipse(78, 26, 5, 5, P.Neon);
        if (k.Level >= 1) FloatingRing(k, 78, 26, 9, 3);
        if (k.Level >= 2) SunDisc(k, 14, 52, 4);
        k.Finish();
    }

    // ---------- Schools ----------

    private static void PxPplTabletHouse(K k)
    {
        // A flat-roofed mud-brick school: a reed awning, pupils' benches, stacks of clay tablets.
        k.Ground(2, 2, 61, 50, P.Dirt);
        k.Wall(8, 14, 56, 40, K.Mat.Mud);
        k.Rect(6, 40, 58, 43, P.Mud[1]);
        k.Door(28, 14, 8, 12, false, true);
        k.Window(14, 26, 5, 5, true); k.Window(46, 26, 5, 5, true);
        // the awning over the benches
        k.HLine(6, 30, 20, P.Straw[2]); k.HLine(6, 30, 21, P.Straw[3]);
        k.VLine(7, 4, 20, P.Wood[1]);
        for (int i = 0; i < 2; i++) k.Rect(10 + i * 9, 4, 16 + i * 9, 6, P.Wood[2]);
        // stacks of tablets
        for (int i = 0; i < 2 + k.Level; i++) { k.Rect(40 + i * 6, 3 + (i % 2), 44 + i * 6, 6 + (i % 2), PplClay[2]); k.HLine(40 + i * 6, 44 + i * 6, 6 + (i % 2), PplClay[3]); }
        // carved signs on the lintel
        for (int x = 24; x <= 40; x += 3) k.Px(x, 30, PplClay[0]);
        if (k.Level >= 2) k.Banner(58, 14, 24, P.Blue);
        k.Finish();
    }

    private static void PxPplAcademy(K k)
    {
        // A marble stoa with columns and a pediment, an olive tree, a sundial in the grove.
        k.Ground(2, 2, 61, 50, P.Grass);
        for (int i = 0; i < 2; i++) k.Rect(4 + i * 2, 6 + i * 2, 59 - i * 2, 7 + i * 2, P.Marble[3 - i]);
        Block(k, 8, 10, 55, 36, P.Marble);
        k.Door(28, 10, 8, 14, true, true);
        Colonnade(k, 9, 55, 10, 26, 8);
        k.Rect(6, 36, 57, 39, P.Marble[2]);
        Pediment(k, 6, 57, 40, 12, P.Marble);
        k.Px(31, 45, P.Gold[3]); k.Px(32, 45, P.Gold[3]);
        // the olive tree and a sundial
        k.VLine(4, 4, 20, P.Wood[1]);
        k.Ellipse(4, 24, 5, 4, P.Olive);
        k.Rect(56, 2, 60, 5, P.Marble[2]); k.Px(58, 6, P.Iron[2]);
        if (k.Level >= 1) { k.Pot(12, 2, P.Tile); k.Pot(46, 2, P.Tile); }
        if (k.Level >= 2) CulBanners(k, 2, 61, 36, 18, P.Blue);
        k.Finish();
    }

    private static void PxPplUniversity(K k)
    {
        // Stone colleges round a cloister, a clock tower and a gatehouse with the arms over it.
        k.Ground(2, 2, 93, 80, P.Grass);
        k.Wall(6, 12, 90, 44, K.Mat.Stone);
        SlateRoof(k, 3, 93, 43, 16);
        Arcade(k, 10, 46, 12, 14, 4);
        for (int i = 0; i < 4; i++) { k.Rect(56 + i * 8, 26, 60 + i * 8, 36, P.Stone[1]); k.Rect(57 + i * 8, 27, 59 + i * 8, 35, P.Glow[2]); }
        // clock tower
        k.Wall(66, 44, 82, 84, K.Mat.Stone);
        k.Roof(64, 84, 84, 14, K.RoofType.Slate, 8);
        Clock(k, 74, 70, 5);
        k.Rect(71, 52, 77, 60, P.Coal[1]);
        // the gate and its arms
        k.Door(52, 12, 10, 12, true);
        ShieldDecorAt(k, 57, 30);
        if (k.Level >= 1) CulBanners(k, 4, 92, 12, 30, P.Purple);
        if (k.Level >= 2) for (int i = 0; i < 6; i++) k.Px(12 + i * 6, 46 + (i % 2), P.Gold[3]);   // gilded finials
        k.Finish();
    }

    private static void ShieldDecorAt(K k, int x, int y) => k.ShieldDecor(x, y, P.Red);

    private static void PxPplRoyalAcademy(K k)
    {
        // A brick and stone academy with sash windows, a portico, and an observatory dome on top.
        k.Ground(2, 2, 93, 80, P.Grass);
        GeorgianWall(k, 6, 10, 90, 50);
        for (int i = 0; i < 4; i++) { Sash(k, 12 + i * 8, 22); Sash(k, 12 + i * 8, 36); Sash(k, 62 + i * 7, 22); Sash(k, 62 + i * 7, 36); }
        // the portico
        k.Rect(40, 10, 56, 12, P.Marble[3]);
        for (int x = 40; x <= 54; x += 5) k.Column(x, 12, 30);
        Pediment(k, 38, 58, 42, 10, P.Marble);
        k.Door(45, 12, 6, 12, true);
        k.Rect(4, 50, 92, 53, P.Marble[2]);
        // the observatory dome with a telescope slot and the telescope poking out
        Dome(k, 48, 53, 14, P.Steel);
        k.Rect(47, 56, 49, 64, P.Coal[1]);
        k.Line(48, 60, 58, 70, P.Brass[2], 2);
        if (k.Level >= 1) { k.FlowerBox(12, 20, 30); k.FlowerBox(62, 20, 26); }
        if (k.Level >= 2) CulBanners(k, 4, 92, 10, 36, P.Blue);
        k.Finish();
    }

    private static void PxPplPolytechnic(K k)
    {
        // Red-brick halls under a sawtooth roof, a clock over the door and a workshop chimney.
        k.Ground(2, 2, 93, 80, P.Dirt);
        BrickWall(k, 6, 10, 90, 46);
        SawtoothRoof(k, 6, 90, 46, 6, 10);
        for (int i = 0; i < 9; i++) if (i != 4) k.Window(9 + i * 9, 26, 5, 10, true);
        k.Door(43, 10, 10, 14, true);
        Clock(k, 48, 34, 4);
        k.Chimney(80, 46, 6, 26, P.Stone);
        k.Smoke(83, 74, 1f, 150);
        Cog(k, 20, 16, 4, k.Frame);
        if (k.Level >= 1) for (int i = 0; i < 3; i++) k.Crate(60 + i * 7, 2);
        if (k.Level >= 2) k.Banner(4, 10, 40, P.Gold);
        k.Finish();
    }

    private static void PxPplRadium(K k)
    {
        // A chrome and turquoise tower of labs, a Tesla spire crackling on top.
        k.Ground(2, 2, 93, 40, P.Dirt);
        ModernistWall(k, 6, 6, 90, 36);
        ModernistWall(k, 30, 36, 66, 100);
        for (int f = 0; f < 8; f++) for (int i = 0; i < 4; i++) k.Rect(34 + i * 8, 40 + f * 7, 38 + i * 8, 44 + f * 7, (f + i) % 3 == 0 ? P.Glow[2] : Turquoise[1]);
        for (int i = 0; i < 9; i++) if (i != 4) k.Window(9 + i * 9, 16, 5, 9, true);
        k.Door(43, 6, 10, 12);
        // the spire
        k.Rect(44, 100, 52, 104, PplChrome[1]);
        k.VLine(48, 104, 124, PplChrome[2]);
        k.Ellipse(48, 126, 4, 4, PplChrome);
        Sparks(k, 44, 126, 30, 118);
        if (k.Frame % 2 == 1) Sparks(k, 52, 126, 66, 120);
        if (k.Level >= 1) CulBulbs(k, 8, 88, 37);
        if (k.Level >= 2) Starburst(k, 20, 30, P.Gold[3]);
        k.Finish();
    }

    private static void PxPplResearch(K k)
    {
        // Dark glass labs on a campus lawn, a data centre humming and a dish on the roof.
        k.Ground(2, 2, 93, 80, P.Grass);
        GlassBlock(k, 6, 10, 60, 52);
        GlassBlock(k, 62, 10, 90, 34, true);
        for (int x = 66; x < 88; x += 4) for (int y = 14; y < 32; y += 3) k.Px(x, y, (x + y + k.Frame) % 3 == 0 ? P.Neon[3] : P.Neon[1]);
        k.Door(28, 10, 10, 12);
        Dish(k, 46, 60, 7);
        k.VLine(20, 52, 66, PplChrome[2]); k.Px(20, 67, k.Frame % 2 == 0 ? P.Red[3] : P.Red[1]);
        for (int i = 0; i < 3; i++) k.Ellipse(12 + i * 30, 5, 3, 2, P.Leaf);   // lawn shrubs
        if (k.Level >= 1) Holo(k, 64, 36, 88, 44);
        if (k.Level >= 2) Dish(k, 76, 40, 5);
        k.Finish();
    }

    private static void PxPplArcane(K k)
    {
        // Spired towers of dark stone round a hall, runes glowing, crystals floating over them.
        k.Ground(2, 2, 93, 80, P.Grass);
        k.Wall(14, 10, 82, 46, K.Mat.Stone);
        k.Roof(11, 85, 45, 16, K.RoofType.Slate, 14);
        for (int i = 0; i < 4; i++) { k.Rect(22 + i * 16, 22, 27 + i * 16, 36, P.Stone[1]); k.Rect(23 + i * 16, 23, 26 + i * 16, 35, (k.Frame + i) % 2 == 0 ? P.Arcane[2] : P.Arcane[3]); }
        k.Door(43, 10, 10, 14, true);
        // the towers, with pointed purple roofs
        foreach (int x in new[] { 4, 80 })
        {
            k.Wall(x, 10, x + 12, 86, K.Mat.Stone);
            k.Polygon(new[] { new Vector2(x - 2, 86), new Vector2(x + 14, 86), new Vector2(x + 6, 112) }, l => l.x < x + 6 ? P.Arcane[2] : P.Arcane[1]);
            k.Rect(x + 4, 64, x + 8, 72, P.Arcane[3]);
            RuneGlyph(k, x + 6, 40, P.Mana[3]);
        }
        FloatingCrystal(k, 48, 76, 5);
        if (k.Level >= 1) FloatingCrystal(k, 30, 68, 3, P.Arcane);
        if (k.Level >= 2) FloatingCrystal(k, 66, 68, 3, P.Arcane);
        k.Finish();
    }

    private static void PxPplNeural(K k)
    {
        // A sun-white academy under a dome of light where students learn in linked dreams.
        k.Ground(2, 2, 93, 80, P.Sand);
        WhiteStone(k, 6, 10, 90, 44);
        SunWindows(k, 10, 86, 20, 10, 9);
        k.Door(43, 10, 10, 12);
        GlassDome(k, 48, 44, 30, 26);
        // the linked dreamers: lines of light between nodes in the dome
        for (int i = 0; i < 5; i++)
        {
            int x = 30 + i * 9, y = 52 + (i % 2) * 8;
            k.Px(x, y, P.White);
            if (i > 0 && (i + k.Frame) % 2 == 0) k.Line(x - 9, 52 + ((i - 1) % 2) * 8, x, y, P.Mana[3]);
        }
        if (k.Level >= 1) SunDisc(k, 48, 76, 4);
        if (k.Level >= 2) { FloatingRing(k, 48, 60, 34, 6); }
        k.Finish();
    }

    private static void PxPplOmniversity(K k)
    {
        // Pearl halls under a floating ring, a galaxy turning above: every world's knowledge taught.
        k.Ground(2, 2, 93, 80, P.Grass);
        PearlStone(k, 6, 10, 90, 48);
        for (int i = 0; i < 8; i++) if (i != 4 && i != 3) k.Rect(10 + i * 10, 22, 15 + i * 10, 38, P.Prism[(i + k.Frame) % 4]);
        k.Door(41, 10, 14, 16, true);
        PearlStone(k, 28, 48, 68, 80);
        Galaxy(k, 48, 112, 14, 0.5f);
        FloatingRing(k, 48, 96, 30, 7);
        CulStarfield(k, 4, 84, 92, 140, 16);
        if (k.Level >= 1) FloatingRing(k, 48, 104, 22, 5);
        if (k.Level >= 2) CulBanners(k, 4, 92, 10, 36, P.Cosmic);
        k.Finish();
    }

    // ---------- Icons ----------

    private static void IcPplPottery(PixelCanvas c)
    {
        // an amphora with handles and a painted band
        c.Ellipse(Iv(10, 8), 5, 6, PplClay);
        c.Rect(8, 13, 12, 16, PplClay[2]); c.Rect(7, 16, 13, 17, PplClay[3]);
        c.Line(Iv(6, 13), Iv(7, 16), PplClay[1]); c.Line(Iv(14, 13), Iv(13, 16), PplClay[1]);
        c.Rect(6, 8, 14, 9, P.Ink);
        c.Rect(8, 2, 12, 3, PplClay[1]);
        c.Put(8, 10, PplClay[3]);
    }

    private static void IcPplCloth(PixelCanvas c)
    {
        // a bolt of red cloth, unrolled a little
        c.Rect(3, 9, 15, 15, P.Red[2]);
        c.Ellipse(Iv(15, 12), 3, 3.5f, P.Red);
        c.Ellipse(Iv(15, 12), 1.2f, 1.4f, P.Red);
        c.Rect(3, 4, 11, 9, P.Red[1]);
        for (int x = 4; x <= 14; x += 3) c.Put(x, 12, P.Gold[3]);
        c.Rect(3, 14, 15, 14, P.Red[3]);
    }

    private static void IcPplAle(PixelCanvas c)
    {
        // a wooden tankard with foam
        c.Rect(5, 3, 13, 14, P.Wood[2]);
        c.Rect(5, 5, 13, 5, P.Iron[2]); c.Rect(5, 11, 13, 11, P.Iron[2]);
        c.Rect(6, 3, 7, 14, P.Wood[3]);
        c.Line(Iv(13, 12), Iv(16, 11), P.Wood[1], 2); c.Line(Iv(16, 11), Iv(16, 6), P.Wood[1], 2); c.Line(Iv(16, 6), Iv(13, 6), P.Wood[1], 2);
        c.Ellipse(Iv(9, 15.5f), 5, 2.5f, P.Linen);
        c.Put(6, 17, P.White); c.Put(11, 17, P.White);
    }

    private static void IcPplBooks(PixelCanvas c)
    {
        // a stack of three books
        c.Rect(3, 2, 16, 6, P.Blue[2]); c.Rect(4, 3, 15, 5, P.Linen[3]); c.Rect(3, 2, 4, 6, P.Blue[1]);
        c.Rect(4, 7, 15, 11, P.Red[2]); c.Rect(5, 8, 14, 10, P.Linen[3]); c.Rect(13, 7, 15, 11, P.Red[1]);
        c.Rect(5, 12, 14, 16, P.Green[2]); c.Rect(6, 13, 13, 15, P.Linen[3]); c.Rect(5, 12, 6, 16, P.Green[1]);
        c.Put(9, 9, P.Gold[3]); c.Put(10, 4, P.Gold[3]);
    }

    private static void IcPplCoffee(PixelCanvas c)
    {
        // a cup of coffee steaming on a saucer, beans beside it
        c.Ellipse(Iv(9, 3), 7, 1.8f, P.Linen);
        c.Rect(4, 4, 13, 10, P.Linen[3]); c.Rect(4, 4, 5, 10, P.Linen[2]);
        c.Rect(5, 10, 12, 11, PplCoffee[1]);
        c.Line(Iv(13, 9), Iv(15, 8), P.Linen[2], 2); c.Line(Iv(15, 8), Iv(13, 6), P.Linen[2]);
        c.Put(7, 13, P.Linen[2]); c.Put(8, 14, P.Linen[2]); c.Put(10, 13, P.Linen[2]); c.Put(11, 15, P.Linen[2]);
        c.Ellipse(Iv(16, 3), 1.6f, 1.2f, PplCoffee);
    }

    private static void IcPplElectronics(PixelCanvas c)
    {
        // a retro radio: a wooden case, a glowing dial and a grille
        c.Rect(2, 3, 17, 14, P.Wood[2]); c.Rect(2, 13, 17, 14, P.Wood[3]);
        c.Ellipse(Iv(6.5f, 8.5f), 3, 3, P.Glow);
        c.Put(6, 9, P.Red[2]); c.Put(7, 10, P.Red[2]);
        for (int y = 5; y <= 12; y += 2) c.Rect(11, y, 16, y, PplChrome[1]);
        c.Rect(4, 15, 4, 16, P.Coal[1]); c.Rect(15, 15, 15, 16, P.Coal[1]);
    }

    private static void IcPplHolo(PixelCanvas c)
    {
        // a holo-disc casting a little world of light
        c.Ellipse(Iv(10, 3.5f), 7, 2, PplChrome);
        for (int y = 5; y <= 15; y++) if (y % 2 == 0) c.Rect(10 - (y - 4) / 2, y, 10 + (y - 4) / 2, y, new Color32(90, 216, 255, 150));
        c.Ellipse(Iv(10, 12), 3.5f, 3.5f, P.Neon);
        c.Put(9, 13, P.White);
    }

    // ---------- Comfort decorations ----------

    public const int PplDecorW = 64, PplDecorH = 34, PplDecorBottom = 4;

    /// <summary>What a home of a style puts out at a comfort level (cumulative: grand homes have it all).</summary>
    private static void PplDecor(K k, int style, int level)
    {
        // Level 1: planters at the front corners.
        Color32[] pot = style == 0 ? PplClay : style == 1 ? P.Wood : style == 2 ? P.Iron : PplChrome;
        foreach (int x in new[] { 1, 56 })
        {
            if (style == 1) { k.Rect(x, 0, x + 6, 3, P.Wood[2]); k.HLine(x, x + 6, 3, P.Wood[3]); }
            else if (style == 3) { k.Rect(x + 1, 0, x + 5, 3, PplChrome[1]); k.HLine(x + 1, x + 5, 3, P.Neon[2]); }
            else k.Pot(x + 1, 0, pot);
            for (int i = 0; i < 5; i++)
            {
                var bloom = style == 3 ? (i % 2 == 0 ? P.Mana[3] : P.Arcane[3]) : P.Flower[(i + x) % 4];
                k.Px(x + 1 + i, 5 + (i % 2), i % 2 == 0 ? bloom : P.Leaf[3]);
                k.Px(x + 1 + i, 4, P.Leaf[2]);
            }
        }
        if (level < 2) return;
        // Level 2: a light by the door and a bench.
        int lx = 14;
        switch (style)
        {
            case 0: k.Torch(lx, 0); break;
            case 1: k.VLine(lx, 0, 9, P.Iron[1]); k.Rect(lx - 1, 9, lx + 1, 11, P.Iron[2]); k.Px(lx, 10, P.Glow[3]); break;
            case 2: k.VLine(lx, 0, 13, P.Iron[1]); k.HLine(lx, lx + 2, 13, P.Iron[1]); k.Rect(lx + 1, 11, lx + 3, 12, P.Glow[3]); break;
            default: k.VLine(lx, 0, 6, PplChrome[1]); k.Ellipse(lx, 10, 2, 2, P.Mana); k.Px(lx, 10, P.White); break;
        }
        var seat = style == 2 ? P.Green : style == 3 ? PplChrome : P.Wood;
        k.HLine(44, 52, 3, seat[2]); k.HLine(44, 52, 4, seat[3]);
        k.VLine(45, 0, 2, seat[1]); k.VLine(51, 0, 2, seat[1]);
        if (style >= 1) k.HLine(44, 52, 7, seat[2]);
        if (level < 3) return;
        // Level 3: festive bunting along the front, and a small fountain or statue.
        for (int x = 2; x <= 61; x++)
        {
            int y = 26 - Mathf.RoundToInt(Mathf.Sin((x - 2) / 59f * Mathf.PI) * 4);
            k.Px(x, y, style == 3 ? P.Neon[1] : P.Wood[1]);
            if ((x - 2) % 5 == 2)
            {
                Color32 flag = style == 2 ? ((x / 5) % 2 == 0 ? P.Glow[3] : P.Glow[2])
                    : style == 3 ? P.Prism[(x / 5) % 4]
                    : (x / 5) % 3 == 0 ? P.Red[2] : (x / 5) % 3 == 1 ? P.Gold[2] : P.Blue[2];
                k.Px(x, y - 1, flag); k.Px(x + 1, y - 1, flag); k.Px(x, y - 2, flag);
            }
        }
        if (style == 3) { k.Ellipse(24, 2, 3, 1.5f, PplChrome); k.Ellipse(24, 7, 1.5f, 2.5f, P.Neon); }
        else
        {
            var stone = style == 0 ? P.Rock : P.Marble;
            k.Ellipse(24, 2, 3.5f, 1.8f, stone);
            k.Px(23, 3, P.Water[3]); k.Px(25, 3, P.Water[3]); k.VLine(24, 3, 6, P.Water[2]); k.Px(24, 7, P.Water[4]);
        }
    }
}
