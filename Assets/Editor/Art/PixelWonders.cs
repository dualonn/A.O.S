using System.Collections.Generic;
using UnityEngine;
using P = PixelPalette;
using K = BuildingKit;

/// <summary>
/// Great Wonders in pixel art, one per age, each far bigger and busier than an ordinary building
/// of its age; and the natural wonders (waterfalls, crystal spires, calderas...) found in the wild.
/// </summary>
public static partial class PlaceholderArt
{
    private static readonly Dictionary<string, (PixelDrawer draw, int frames, float fps)> WonderBuildingArt =
        new Dictionary<string, (PixelDrawer, int, float)>
        {
            { "StoneCircle", (PxStoneCircle, 4, 2f) }, { "GreatZiggurat", (PxZiggurat, 4, 4f) }, { "HangingGardens", (PxHangingGardens, 4, 4f) },
            { "GreatLibrary", (PxGreatLibrary, 4, 3f) }, { "GrandCathedral", (PxCathedral, 4, 3f) }, { "GrandObservatory", (PxObservatory, 4, 3f) },
            { "RoyalAcademy", (PxRoyalAcademy, 4, 3f) }, { "GreatEngine", (PxGreatEngine, 4, 6f) }, { "Skyscraper", (PxSkyscraper, 4, 3f) },
            { "TeslaTower", (PxTeslaTower, 4, 8f) }, { "DataArk", (PxDataArk, 4, 5f) }, { "ArchmagesSpire", (PxArchmagesSpire, 4, 4f) },
            { "SpaceElevator", (PxSpaceElevator, 4, 4f) }, { "GreatSunforge", (PxSunforge, 4, 6f) }, { "FoldNexus", (PxFoldNexus, 4, 5f) },
            { "GalacticSenate", (PxGalacticSenate, 4, 3f) }, { "CradleOfCreation", (PxCradle, 4, 5f) },
        };

    // ---------- Shared pieces ----------

    /// <summary>A shaded block: light on the left edge, a dark line along the bottom.</summary>
    private static void Block(K k, int x0, int y0, int x1, int y1, Color32[] r)
    {
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                k.Px(x, y, x < x0 + 2 ? r[3] : y == y0 ? r[0] : ((x * 7 + y * 3) % 23 == 0 ? r[1] : r[2]));
    }

    /// <summary>A row of columns with a base and a beam.</summary>
    private static void Colonnade(K k, int x0, int x1, int y, int h, int step)
    {
        for (int x = x0; x <= x1 - 3; x += step) k.Column(x, y, h);
    }

    private static void Glow(K k, float cx, float cy, float r, Color32[] ramp) =>
        k.Ellipse(cx, cy, r, r, new[] { ramp[1], ramp[2], ramp[3], P.White });

    // ---------- Stone to Classical ----------

    private static void PxStoneCircle(K k)
    {
        k.Ground(2, 2, 125, 90, P.Grass);
        // Standing stones round a ring, back ones first.
        var stones = new List<(int x, int y, int h)>();
        for (int i = 0; i < 12; i++)
        {
            float a = i / 12f * Mathf.PI * 2f;
            stones.Add((Mathf.RoundToInt(64 + Mathf.Cos(a) * 50), Mathf.RoundToInt(46 + Mathf.Sin(a) * 28), 22 + (i * 5) % 9));
        }
        stones.Sort((a, b) => b.y.CompareTo(a.y));
        foreach (var (x, y, h) in stones)
        {
            Block(k, x - 4, y, x + 4, y + h, P.Rock);
            k.HLine(x - 4, x + 4, y + h, P.Rock[4]);
        }
        // Lintels across three pairs of stones.
        k.Rect(30, 78, 50, 81, P.Rock[3]); k.Rect(78, 78, 98, 81, P.Rock[3]);
        // The altar stone and its fire.
        Block(k, 56, 40, 72, 48, P.Stone);
        k.Fire(64, 49, 6, 8);
        // The midsummer sun rising over the heel stone.
        int sy = 92 + k.Frame * 4;
        Glow(k, 64, sy, 6, P.Gold);
        k.Line(64, 50, 64, sy - 7, new Color32(255, 230, 150, 120));
        k.Finish();
    }

    private static void PxZiggurat(K k)
    {
        k.Ground(2, 2, 157, 60, P.Sand);
        var brick = P.Mud;
        for (int i = 0; i < 5; i++)
        {
            int x0 = 10 + i * 14, x1 = 149 - i * 14, y0 = 6 + i * 18, y1 = 23 + i * 18;
            Block(k, x0, y0, x1, y1, brick);
            for (int x = x0 + 4; x < x1; x += 8) k.VLine(x, y0 + 3, y1 - 3, brick[1]);   // pilasters
        }
        // The great stair up the middle.
        for (int y = 6; y < 96; y += 3) k.HLine(72, 87, y, (y / 3) % 2 == 0 ? P.Sand[3] : P.Sand[2]);
        // The temple on the top, its door lit.
        Block(k, 62, 96, 97, 116, P.Plaster);
        k.Roof(58, 101, 116, 10, BuildingKit.RoofType.Tile, 6);
        k.Door(75, 96, 10, 12, true);
        k.Torch(60, 96); k.Torch(99, 96);
        k.Fire(80, 127, 4, 6 + k.Frame % 2 * 2);
        k.Banner(16, 24, 16); k.Banner(140, 24, 16);
        k.Finish();
    }

    private static void PxHangingGardens(K k)
    {
        k.Ground(2, 2, 157, 60, P.Sand);
        var stone = P.Plaster;
        for (int i = 0; i < 4; i++)
        {
            int x0 = 12 + i * 16, x1 = 147 - i * 16, y0 = 6 + i * 24, y1 = 26 + i * 24;
            Block(k, x0, y0, x1, y1, stone);
            for (int x = x0 + 6; x < x1 - 4; x += 12) { k.Rect(x, y0 + 4, x + 5, y0 + 14, P.Coal[1]); k.Px(x + 2, y0 + 14, stone[3]); }   // arches
            // Trees and flowers along the terrace, vines hanging over the edge.
            for (int x = x0 + 4; x < x1 - 4; x += 9)
            {
                k.Ellipse(x + 2, y1 + 5, 5, 4, new[] { P.Leaf[1], P.Leaf[2], P.Leaf[3], P.Leaf[4] });
                if ((x / 9 + i) % 3 == 0) k.Px(x + 2, y1 + 7, P.Flower[2]);
                for (int v = 0; v < 6 + (x % 4); v++) k.Px(x + (v % 2), y1 - v, v % 2 == 0 ? P.Leaf[3] : P.Leaf[2]);
            }
        }
        // Water lifted to the top runs down in a fall to the pool.
        for (int y = 8; y < 102; y++) k.Px(150 - 0, y, ((y + k.Frame * 3) % 6 < 3) ? P.Water[3] : P.Water[2]);
        k.Ellipse(146, 8, 10, 4, P.Water);
        k.Ellipse(80, 110, 18, 8, new[] { P.Leaf[1], P.Leaf[2], P.Leaf[3], P.Leaf[4] });   // the great tree on top
        k.Rect(78, 98, 82, 106, P.Wood[1]);
        k.Finish();
    }

    private static void PxGreatLibrary(K k)
    {
        k.Ground(2, 2, 157, 60, P.Dirt);
        // Wide marble steps, a colonnade, the pediment.
        for (int i = 0; i < 4; i++) k.Rect(10 + i * 4, 4 + i * 3, 149 - i * 4, 6 + i * 3, P.Marble[3 - (i % 2)]);
        Block(k, 22, 16, 137, 60, P.Marble);
        Colonnade(k, 24, 136, 16, 40, 12);
        k.Polygon(new[] { new Vector2(16, 61), new Vector2(143, 61), new Vector2(80, 86) }, l => l.x < 60 ? (Color32?)P.Marble[3] : P.Marble[2]);
        k.Line(16, 61, 143, 61, P.Gold[3]);
        k.Ellipse(80, 70, 6, 4, P.Gold);   // the owl's shield in the pediment
        k.Door(72, 16, 16, 22, true);
        // Scroll racks inside the door, a scholar's lamp flickering.
        for (int i = 0; i < 3; i++) k.HLine(74, 85, 20 + i * 5, P.Linen[2]);
        k.Px(80, 34, k.Frame % 2 == 0 ? P.Fire[3] : P.Fire[2]);
        k.Banner(14, 16, 22); k.Banner(144, 16, 22);
        k.Finish();
    }

    // ---------- Medieval to Industrial ----------

    private static void PxCathedral(K k)
    {
        k.Ground(2, 2, 157, 50, P.Dirt);
        k.Wall(24, 8, 135, 70, BuildingKit.Mat.Stone);
        k.Roof(20, 139, 70, 22, BuildingKit.RoofType.Slate, 10);
        // Twin west towers with spires, and the great central spire.
        k.Wall(8, 8, 30, 104, BuildingKit.Mat.Stone); k.Cone(19, 104, 14, 36, BuildingKit.RoofType.Slate);
        k.Wall(129, 8, 151, 104, BuildingKit.Mat.Stone); k.Cone(140, 104, 14, 36, BuildingKit.RoofType.Slate);
        k.Wall(72, 70, 88, 112, BuildingKit.Mat.Stone); k.Cone(80, 112, 11, 52, BuildingKit.RoofType.Slate);
        k.Px(80, 165, P.Gold[3]); k.VLine(80, 160, 168, P.Gold[2]); k.HLine(77, 83, 165, P.Gold[2]);
        // The rose window, in stained glass.
        k.Ellipse(80, 50, 12, 12, new[] { P.Stone[1], P.Blue[2], P.Red[2], P.Gold[3] });
        for (int i = 0; i < 8; i++) k.Line(80, 50, 80 + Mathf.Cos(i * 0.785f) * 11, 50 + Mathf.Sin(i * 0.785f) * 11, P.Stone[1]);
        k.Door(72, 8, 16, 22, true);
        for (int i = 0; i < 4; i++) { k.Window(38 + i * 10, 34, 4, 14); k.Window(102 + i * 8, 34, 4, 14); }
        // Buttresses along the nave.
        for (int x = 40; x < 128; x += 22) k.Polygon(new[] { new Vector2(x, 8), new Vector2(x + 5, 8), new Vector2(x + 2, 60) }, _ => P.Stone[2]);
        // A bell swinging in the north tower.
        k.Ellipse(19 + (k.Frame % 2 == 0 ? -1 : 1), 92, 4, 4, P.Gold);
        k.Finish();
    }

    private static void PxObservatory(K k)
    {
        k.Ground(2, 2, 157, 50, P.Dirt);
        k.Wall(20, 8, 139, 56, BuildingKit.Mat.Marble);
        for (int i = 0; i < 6; i++) k.Window(28 + i * 19, 26, 6, 12);
        k.Door(72, 8, 16, 20, true);
        // The great dome on bronze rails, its slit turning with the telescope.
        k.Ellipse(80, 56, 46, 40, new[] { P.Bronze[0], P.Bronze[1], P.Bronze[2], P.Bronze[3] }, l => l.y >= 56);
        for (int i = 1; i < 4; i++) k.Ellipse(80, 56, 46 - i * 2, 40 - i * 10, P.Bronze, l => l.y >= 56 && l.y < 57 + i);
        int slit = 66 + k.Frame * 6;
        k.Rect(slit - 3, 60, slit + 3, 92, P.Coal[0]);
        float a = 0.9f + k.Frame * 0.08f;
        k.Line(slit, 72, slit + Mathf.Cos(a) * 34, 72 + Mathf.Sin(a) * 34, P.Brass[2], 4);
        k.Ellipse(slit + Mathf.Cos(a) * 34, 72 + Mathf.Sin(a) * 34, 3, 3, P.Brass);
        // Stars over it.
        for (int i = 0; i < 6; i++) k.Px(18 + i * 24, 120 + (i * 7) % 18, (i + k.Frame) % 3 == 0 ? P.White : P.Moonstone[3]);
        k.Finish();
    }

    private static void PxRoyalAcademy(K k)
    {
        k.Ground(2, 2, 157, 50, P.Dirt);
        k.Wall(8, 8, 151, 62, BuildingKit.Mat.Plaster);
        k.FlatRoof(6, 153, 62, 4, P.Stone);
        for (int row = 0; row < 2; row++) for (int i = 0; i < 10; i++) if (i < 4 || i > 5) k.Window(14 + i * 14, 18 + row * 22, 6, 12);
        // The portico and the dome of the great hall.
        Colonnade(k, 58, 102, 8, 52, 9);
        k.Polygon(new[] { new Vector2(54, 62), new Vector2(106, 62), new Vector2(80, 76) }, _ => P.Marble[2]);
        k.Ellipse(80, 76, 22, 24, new[] { P.Green[0], P.Green[1], P.Green[2], P.Green[3] }, l => l.y >= 76);
        k.VLine(80, 100, 112, P.Gold[2]);
        k.Banner(80, 104, 10);
        k.Door(74, 8, 12, 16, true);
        // Candles lit in the windows as the philosophers argue late.
        for (int i = 0; i < 4; i++) k.Px(17 + ((i * 3 + k.Frame) % 10) * 14, 22, P.Fire[3]);
        k.Finish();
    }

    private static void PxGreatEngine(K k)
    {
        k.Ground(2, 2, 157, 50, P.Dirt);
        k.Wall(8, 8, 110, 70, BuildingKit.Mat.Stone);
        k.Roof(4, 114, 70, 20, BuildingKit.RoofType.Slate, 8);
        for (int i = 0; i < 5; i++) k.Window(16 + i * 18, 30, 8, 18);
        k.Door(48, 8, 14, 18);
        // Two great chimneys.
        k.Chimney(20, 86, 10, 70); k.Chimney(84, 86, 10, 64);
        k.Smoke(25, 158, 1.4f); k.Smoke(89, 152, 1.3f);
        // The flywheel, taller than the hall, turning.
        float cx = 134, cy = 50, r = 26;
        k.Ellipse(cx, cy, r, r, P.Iron, l => (l - new Vector2(cx, cy)).magnitude > r - 3);
        float spin = k.Frame * Mathf.PI / 8f;
        for (int i = 0; i < 6; i++) k.Line(cx, cy, cx + Mathf.Cos(spin + i * 1.047f) * (r - 2), cy + Mathf.Sin(spin + i * 1.047f) * (r - 2), P.Iron[2], 2);
        Glow(k, cx, cy, 4, P.Brass);
        // The piston driving it.
        float px = 112 + Mathf.Cos(spin) * 8;
        k.Rect(108, 46, 118, 54, P.Brass[1]);
        k.Line(px, 50, cx + Mathf.Cos(spin) * 14, cy + Mathf.Sin(spin) * 14, P.Steel[3], 2);
        k.Finish();
    }

    // ---------- Modern to Information ----------

    private static void PxSkyscraper(K k)
    {
        k.Ground(2, 2, 125, 40, P.Stone);
        // A tower in three setbacks, glass and steel, windows lit at random.
        int[] x0 = { 22, 32, 42 }, x1 = { 105, 95, 85 }, top = { 120, 190, 236 };
        int y = 6;
        for (int s = 0; s < 3; s++)
        {
            for (int yy = y; yy <= top[s]; yy++)
                for (int xx = x0[s]; xx <= x1[s]; xx++)
                {
                    bool frame = (xx - x0[s]) % 6 == 0 || (yy - y) % 7 == 0;
                    bool lit = ((xx / 6) * 31 + (yy / 7) * 17 + k.Frame * 5) % 11 < 3;
                    k.Px(xx, yy, frame ? P.Steel[xx < x0[s] + 3 ? 3 : 1] : lit ? P.Gold[3] : (xx < (x0[s] + x1[s]) / 2 ? P.Chrome[2] : P.Chrome[1]));
                }
            k.HLine(x0[s] - 2, x1[s] + 2, top[s], P.Steel[3]);
            y = top[s];
        }
        k.VLine(64, 236, 264, P.Steel[2]);
        k.Px(64, 265, k.Frame % 2 == 0 ? P.Red[3] : P.Red[1]);   // the aircraft light
        k.Door(56, 6, 16, 12);
        k.Finish();
    }

    private static void PxTeslaTower(K k)
    {
        k.Ground(2, 2, 125, 40, P.Dirt);
        k.Wall(36, 6, 92, 28, BuildingKit.Mat.Stone);
        k.Door(58, 6, 12, 14);
        // A copper lattice tower.
        var cu = P.Bronze;
        for (int y = 28; y < 230; y += 10)
        {
            float w0 = Mathf.Lerp(26, 6, (y - 28) / 202f), w1 = Mathf.Lerp(26, 6, (y + 10 - 28) / 202f);
            k.Line(64 - w0, y, 64 - w1, y + 10, cu[2], 2); k.Line(64 + w0, y, 64 + w1, y + 10, cu[1], 2);
            k.Line(64 - w0, y, 64 + w1, y + 10, cu[1]); k.Line(64 + w0, y, 64 - w1, y + 10, cu[1]);
        }
        // The great coil sphere, and lightning arcing off it.
        k.Ellipse(64, 238, 16, 10, new[] { P.Steel[1], P.Steel[2], P.Steel[3], P.White });
        var bolt = new Color32(170, 230, 255, 255);
        for (int b = 0; b < 3; b++)
        {
            float a = (b * 2.1f + k.Frame * 0.9f) % (Mathf.PI * 2f);
            Vector2 at = new Vector2(64 + Mathf.Cos(a) * 16, 238 + Mathf.Sin(a) * 10);
            for (int s = 0; s < 5; s++)
            {
                Vector2 next = at + new Vector2(Mathf.Cos(a) * 6 + ((s * 7 + k.Frame) % 5 - 2), Mathf.Sin(a) * 4 + ((s * 3 + b) % 5 - 2));
                k.Line(at.x, at.y, next.x, next.y, bolt);
                at = next;
            }
        }
        k.Finish();
    }

    private static void PxDataArk(K k)
    {
        k.Ground(2, 2, 157, 60, P.Rock);
        // A mountain with a vault door in it, the cooling river running out below.
        k.Mound(80, 30, 76, 70, P.Rock);
        k.Ellipse(80, 98, 30, 14, new[] { P.Stone[3], P.Stone[4], P.White, P.White }, l => l.y > 92);   // snow
        k.Rect(58, 8, 102, 44, P.Tactical[1]);
        k.Ellipse(80, 26, 16, 16, P.Chrome);
        for (int i = 0; i < 8; i++) k.Line(80, 26, 80 + Mathf.Cos(i * 0.785f + k.Frame * 0.2f) * 14, 26 + Mathf.Sin(i * 0.785f + k.Frame * 0.2f) * 14, P.Tactical[2]);
        Glow(k, 80, 26, 4, P.Neon);
        // Server lights blinking in slits up the mountain, data running down to the door.
        for (int i = 0; i < 10; i++) k.Px(30 + i * 11, 50 + (i * 13) % 30, (i + k.Frame) % 3 == 0 ? P.Neon[3] : P.Neon[1]);
        for (int y = 44; y < 80; y += 2) k.Px(80, y, (y / 2 + k.Frame) % 4 == 0 ? P.Neon[3] : P.Neon[1]);
        for (int x = 102; x < 150; x++) k.Px(x, 8 + (int)(Mathf.Sin(x * 0.3f) * 1.5f), (x + k.Frame * 2) % 5 < 2 ? P.Water[3] : P.Water[2]);
        k.Finish();
    }

    // ---------- Arcane to Universal ----------

    private static void PxArchmagesSpire(K k)
    {
        k.Ground(2, 2, 125, 40, P.Dirt);
        // A tower of stacked, slightly twisted floors, a new floor for every spell.
        for (int f = 0; f < 11; f++)
        {
            int half = 24 - f * 2, sway = (int)(Mathf.Sin(f * 0.7f) * 4), y0 = 6 + f * 20;
            Block(k, 64 - half + sway, y0, 64 + half + sway, y0 + 18, f % 2 == 0 ? P.Stone : P.Rock);
            k.HLine(64 - half - 2 + sway, 64 + half + 2 + sway, y0 + 18, P.Arcane[2]);
            k.Window(62 + sway, y0 + 6, 4, 7, (f + k.Frame) % 3 != 0);
        }
        k.Cone(64, 226, 10, 26, BuildingKit.RoofType.Slate);
        // The crystal at the top and the runes circling the tower.
        Glow(k, 64, 258, 5, P.Mana);
        for (int i = 0; i < 6; i++)
        {
            float a = i * 1.047f + k.Frame * 0.4f;
            k.Px(Mathf.RoundToInt(64 + Mathf.Cos(a) * 34), Mathf.RoundToInt(120 + i * 18 + Mathf.Sin(a) * 6), P.Rune[3]);
        }
        k.Door(58, 6, 12, 14, true);
        k.Finish();
    }

    private static void PxSpaceElevator(K k)
    {
        k.Ground(2, 2, 125, 50, P.Stone);
        // The anchor station, the tether going up out of sight, a climber riding it.
        k.Rect(14, 6, 113, 30, P.Starmetal[1]);
        k.HLine(14, 113, 30, P.Starmetal[3]);
        for (int i = 0; i < 6; i++) k.Window(20 + i * 15, 14, 6, 8);
        k.Ellipse(64, 34, 22, 6, P.Astral);
        for (int y = 36; y < 300; y++) { k.Px(63, y, P.Starmetal[3]); k.Px(64, y, P.Starmetal[2]); }
        int cy = 60 + k.Frame * 50;
        k.Rect(58, cy, 69, cy + 10, P.Steel[2]); k.Px(63, cy + 5, P.Astral[3]);
        // Struts holding the base of the tether.
        k.Line(30, 30, 62, 80, P.Starmetal[2], 2); k.Line(98, 30, 66, 80, P.Starmetal[2], 2);
        k.Finish();
    }

    private static void PxSunforge(K k)
    {
        k.Ground(2, 2, 157, 60, P.Dirt);
        k.Wall(10, 8, 70, 60, BuildingKit.Mat.Stone);
        k.Door(30, 8, 14, 18);
        k.Wall(90, 8, 150, 60, BuildingKit.Mat.Stone);
        k.Door(112, 8, 14, 18);
        // Golden arms holding a little sun between the halls.
        k.Line(66, 60, 80, 100, P.Gold[2], 4); k.Line(94, 60, 80, 100, P.Gold[2], 4);
        int pulse = k.Frame % 4;
        k.Ellipse(80, 108, 18 + pulse * 0.6f, 18 + pulse * 0.6f, new[] { P.Solar[1], P.Solar[2], P.Solar[3], P.White });
        for (int i = 0; i < 12; i++)
        {
            float a = i * 0.524f + k.Frame * 0.15f;
            k.Line(80 + Mathf.Cos(a) * 21, 108 + Mathf.Sin(a) * 21, 80 + Mathf.Cos(a) * (26 + pulse), 108 + Mathf.Sin(a) * (26 + pulse), P.Solar[3]);
        }
        // Molten steel pouring from the halls.
        for (int y = 30; y < 40; y++) { k.Px(70 + (y + k.Frame) % 2, y, P.Fire[3]); k.Px(89 - (y + k.Frame) % 2, y, P.Fire[3]); }
        k.Finish();
    }

    private static void PxFoldNexus(K k)
    {
        k.Ground(2, 2, 157, 60, P.Stone);
        k.Ellipse(80, 20, 70, 14, new[] { P.Coal[0], P.Coal[1], P.Phase[1], P.Phase[2] });
        // Rings of folded space, crossing each other, doorways to other skies.
        for (int ring = 0; ring < 4; ring++)
        {
            float cx = 40 + ring * 27, cy = 60 + (ring % 2) * 16, rx = 16, ry = 30;
            k.Ellipse(cx, cy, rx - 2, ry - 2, new[] { P.Coal[0], P.Coal[0], P.Phase[0], P.Phase[1] });
            for (int i = 0; i < 60; i++)
            {
                float t = i / 60f * Mathf.PI * 2f;
                k.Px(Mathf.RoundToInt(cx + Mathf.Cos(t) * rx), Mathf.RoundToInt(cy + Mathf.Sin(t) * ry), (i + k.Frame * 6 + ring * 15) % 20 < 5 ? P.Phase[3] : P.Riftstone[2]);
            }
            k.Px(Mathf.RoundToInt(cx), Mathf.RoundToInt(cy), P.White);
        }
        k.Finish();
    }

    private static void PxGalacticSenate(K k)
    {
        k.Ground(2, 2, 189, 60, P.Stone);
        for (int i = 0; i < 4; i++) k.Rect(10 + i * 4, 4 + i * 3, 181 - i * 4, 6 + i * 3, P.Marble[3 - (i % 2)]);
        Block(k, 20, 16, 171, 64, P.Marble);
        Colonnade(k, 24, 168, 16, 46, 12);
        // The dome painted with the galaxy, every world's banner round it.
        k.Ellipse(96, 64, 56, 50, new[] { P.Cosmic[0], P.Cosmic[1], P.Cosmic[1], P.Cosmic[2] }, l => l.y >= 64);
        for (int i = 0; i < 14; i++) k.Px(56 + (i * 37) % 80, 70 + (i * 13) % 40, (i + k.Frame) % 3 == 0 ? P.White : P.Cosmic[3]);
        for (int arm = 0; arm < 2; arm++)
            for (int i = 0; i < 18; i++)
            {
                float t = i / 18f, a = arm * Mathf.PI + t * 5f + k.Frame * 0.2f;
                k.Px(Mathf.RoundToInt(96 + Mathf.Cos(a) * 20 * t), Mathf.RoundToInt(88 + Mathf.Sin(a) * 8 * t), t < 0.4f ? P.White : P.Cosmic[3]);
            }
        Color32[][] flags = { P.Red, P.Blue, P.Green, P.Gold, P.Purple, P.Phase };
        for (int i = 0; i < 6; i++) k.Banner(26 + i * 28, 64, 12, flags[i]);
        k.Door(88, 16, 16, 24, true);
        k.Finish();
    }

    private static void PxCradle(K k)
    {
        k.Ground(2, 2, 189, 60, P.Stone);
        // A pearl platform and great gold arms, an armillary round a new world being made.
        Block(k, 24, 6, 167, 26, P.Pearl);
        for (int x = 26; x < 166; x += 9) k.Px(x, 18, P.Prism[(x / 9 + k.Frame) % 4]);
        float cx = 96, cy = 110;
        float spin = k.Frame * Mathf.PI / 8f;
        for (int ring = 0; ring < 3; ring++)
            for (int i = 0; i < 100; i++)
            {
                float t = i / 100f * Mathf.PI * 2f, tilt = spin + ring * 1.05f;
                float x = Mathf.Cos(t) * 56, y = Mathf.Sin(t) * 56;
                k.Px(Mathf.RoundToInt(cx + x * (ring == 2 ? Mathf.Cos(tilt) : 1f)), Mathf.RoundToInt(cy + (ring == 2 ? y : y * Mathf.Cos(tilt))), i % 10 == 0 ? P.White : P.Gold[2 + ring % 2]);
            }
        k.Line(40, 26, cx - 40, cy - 30, P.Gold[2], 3); k.Line(152, 26, cx + 40, cy - 30, P.Gold[2], 3);
        // The little world, still forming: ocean, a first continent, a thin sky.
        k.Ellipse(cx, cy, 16, 16, new[] { P.Water[0], P.Water[1], P.Water[2], P.Water[3] });
        k.Ellipse(cx - 4 + k.Frame, cy + 3, 7, 5, new[] { P.Seed[1], P.Seed[2], P.Seed[2], P.Seed[3] });
        for (int i = 0; i < 4; i++) k.Line(cx, cy - 18, cx + (i - 1.5f) * 14, 26, new Color32(P.Prism[i].r, P.Prism[i].g, P.Prism[i].b, 110));
        k.Finish();
    }

    // ---------- Natural wonders ----------

    private static void PxFalls(K k)
    {
        k.Ground(2, 2, 125, 70, P.Grass);
        // A cliff with a whole river falling off it into a pool, mist and a rainbow.
        k.Mound(64, 70, 62, 90, P.Rock);
        k.Ellipse(64, 158, 40, 6, P.Grass);
        for (int y = 30; y < 156; y++)
            for (int x = 54; x <= 74; x++)
                k.Px(x, y, ((y + x * 3) % 7 < 3) ? P.Water[4] : P.Water[3 - ((x + y) % 2)]);
        k.Ellipse(64, 24, 40, 10, P.Water);
        for (int i = 0; i < 12; i++) k.Px(34 + i * 5, 34 + (i * 7) % 8, P.White);   // spray
        for (int b = 0; b < 4; b++)
            for (int i = 0; i <= 30; i++)
            {
                float t = i / 30f * Mathf.PI;
                k.Px(Mathf.RoundToInt(64 + Mathf.Cos(t) * (34 - b)), Mathf.RoundToInt(40 + Mathf.Sin(t) * (24 - b)), P.Prism[b]);
            }
        k.Finish();
    }

    private static void PxCrystalSpire(K k)
    {
        k.Ground(2, 2, 125, 70, P.Rock);
        // One crystal taller than any tower, smaller ones round its foot.
        k.Polygon(new[] { new Vector2(52, 10), new Vector2(76, 10), new Vector2(70, 150), new Vector2(62, 176), new Vector2(56, 150) },
            l => l.x < 60 ? (Color32?)P.Aether[3] : l.x < 66 ? P.Aether[2] : P.Aether[1]);
        k.Line(62, 14, 62, 170, P.White);
        foreach (var (x, h) in new[] { (30, 40), (40, 60), (88, 52), (100, 34), (20, 24), (108, 22) })
            k.Polygon(new[] { new Vector2(x - 5, 10), new Vector2(x + 5, 10), new Vector2(x, 10 + h) }, l => l.x < x ? (Color32?)P.Mana[3] : P.Mana[2]);
        for (int i = 0; i < 8; i++) k.Px(20 + i * 13, 70 + (i * 17) % 80, P.Mana[3]);
        k.Finish();
    }

    private static void PxElderTree(K k)
    {
        k.Ground(2, 2, 125, 70, P.Grass);
        // A trunk as wide as a house, roots like walls, a canopy like a green hill.
        var bark = P.Wood;
        k.Polygon(new[] { new Vector2(44, 6), new Vector2(84, 6), new Vector2(74, 110), new Vector2(54, 110) }, l => l.x < 56 ? (Color32?)bark[3] : l.x < 70 ? bark[2] : bark[1]);
        for (int y = 14; y < 104; y += 9) k.Line(58, y, 64, y + 6, bark[0]);
        foreach (var (x0, x1) in new[] { (44, 10), (84, 118), (50, 24), (78, 104) }) k.Line(x0, 8, x1, 4, bark[2], 4);   // roots
        k.Ellipse(64, 140, 60, 36, new[] { P.Leaf[1], P.Leaf[2], P.Leaf[3], P.Leaf[4] });
        k.Ellipse(30, 120, 28, 20, new[] { P.Leaf[0], P.Leaf[1], P.Leaf[2], P.Leaf[3] });
        k.Ellipse(98, 124, 28, 20, new[] { P.Leaf[0], P.Leaf[1], P.Leaf[2], P.Leaf[3] });
        for (int i = 0; i < 10; i++) k.Px(20 + i * 10, 110 + (i * 13) % 50, i % 3 == 0 ? P.Flower[2] : P.Glow[3]);
        k.Finish();
    }

    private static void PxCaldera(K k)
    {
        k.Ground(2, 2, 125, 70, P.Rock);
        // A crater of black rock round a lake of fire, smoke rising.
        k.Mound(64, 30, 62, 46, P.Coal);
        k.Ellipse(64, 58, 36, 12, new[] { P.Fire[0], P.Fire[1], P.Fire[2], P.Fire[3] });
        for (int i = 0; i < 6; i++) k.Px(40 + i * 9, 58 + (i % 3) - 1, P.White);
        for (int i = 0; i < 5; i++) k.Line(30 + i * 16, 40, 34 + i * 16, 18, P.Fire[2]);   // lava runs
        k.Smoke(52, 78, 1.6f); k.Smoke(76, 84, 1.4f);
        k.Finish();
    }

    private static void PxSkyArch(K k)
    {
        k.Ground(2, 2, 125, 70, P.Sand);
        // A natural arch of red rock so high that clouds pass through it.
        var red = new[] { P.Hex("5a2418"), P.Hex("9a4428"), P.Hex("c8683c"), P.Hex("e8a070") };
        for (int i = 0; i <= 80; i++)
        {
            float t = i / 80f * Mathf.PI;
            float x = 64 + Mathf.Cos(t) * 48, y = 10 + Mathf.Sin(t) * 140;
            float w = 10 + (1f - Mathf.Sin(t)) * 6;
            k.Ellipse(x, y, w, 8, red);
        }
        k.Ellipse(64, 110, 22, 6, new[] { P.Stone[3], P.Stone[4], P.White, P.White });   // a cloud passing through
        k.Rocks(20, 8, 4); k.Rocks(100, 8, 4);
        k.Finish();
    }

    private static void PxSingingDunes(K k)
    {
        k.Ground(2, 2, 125, 80, P.Sand);
        // Dunes in rows, wind streaming off their crests.
        for (int d = 0; d < 4; d++)
        {
            int cy = 70 - d * 18;
            k.Ellipse(20 + d * 28, cy, 34, 16, P.Sand, l => l.y >= cy - 2);
            for (int i = 0; i < 4; i++) k.Line(30 + d * 28 + i * 4, cy + 14 - i, 50 + d * 28 + i * 6, cy + 16 - i, P.Sand[3]);
        }
        // The note they sing, carried on the wind.
        for (int i = 0; i < 5; i++) k.Line(14 + i * 22, 110 + (i % 2) * 6, 30 + i * 22, 112 + (i % 2) * 6, P.White);
        k.Finish();
    }

    private static void PxAuroraLake(K k)
    {
        k.Ground(2, 2, 125, 70, P.Stone);
        // A still lake holding the sky, auroras above and their reflection below.
        k.Ellipse(64, 34, 56, 26, new[] { P.Water[0], P.Water[1], P.Water[1], P.Water[2] });
        var green = new Color32(110, 255, 170, 255);
        for (int band = 0; band < 3; band++)
            for (int x = 8; x < 120; x++)
            {
                int y = 120 + band * 12 + (int)(Mathf.Sin(x * 0.08f + band) * 8);
                k.Px(x, y, band == 1 ? P.Mana[3] : green);
                k.Px(x, y - 1, band == 1 ? P.Mana[2] : P.Leaf[3]);
                if (x > 16 && x < 112) k.Px(x, 34 - (y - 120) / 6, band == 1 ? P.Mana[2] : P.Leaf[3]);   // the reflection
            }
        k.Ellipse(20, 70, 10, 30, P.Pine); k.Ellipse(108, 66, 9, 26, P.Pine);
        k.Finish();
    }

    private static void PxGreatRift(K k)
    {
        k.Ground(2, 2, 125, 90, P.Dirt);
        // The world splitting: a canyon cutting across, every layer of rock showing in its walls.
        var bands = new[] { P.Rock[1], P.Sand[2], P.Rock[2], P.Mud[2], P.Rock[3] };
        for (int x = 4; x < 124; x++)
        {
            int mid = 46 + (int)(Mathf.Sin(x * 0.07f) * 14);
            int half = 10 + (int)(Mathf.Sin(x * 0.13f + 1) * 3);
            for (int y = mid - half; y <= mid + half; y++)
            {
                int depth = Mathf.Abs(y - mid);
                k.Px(x, y, depth < 3 ? P.Coal[0] : bands[(y / 3 + x / 30) % bands.Length]);
            }
        }
        k.Rocks(14, 80, 3); k.Rocks(100, 12, 3);
        k.Finish();
    }
}
