using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using P = PixelPalette;

/// <summary>
/// Icons (resources, people, raiders, research, gear) and townsfolk in pixel art.
/// (Tools > Age of Stickman > Generate Icon Art.)
/// </summary>
public static partial class PlaceholderArt
{
    private const int IconPx = 20;

    [MenuItem("Tools/Age of Stickman/Generate Icon Art")]
    public static void GenerateIconArt()
    {
        var art = AssetDatabase.LoadAssetAtPath<ArtLibrary>("Assets/Resources/ArtLibrary.asset");
        var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Resources/GameDatabase.asset");
        GeneratePixelIcons(art, db);
        GeneratePixelVillagers(art);
        EditorUtility.SetDirty(art);
        AssetDatabase.SaveAssets();
        Debug.Log("Icon art generated.");
    }

    private static void GeneratePixelIcons(ArtLibrary art, GameDatabase db)
    {
        ResetDir("Icons");
        ResetDir("Icons/Gear");
        var drawers = new Dictionary<string, System.Action<PixelCanvas>>
        {
            { "Food", IcFood }, { "Wood", IcWood }, { "Stone", IcStone }, { "Bronze", c => IcIngot(c, P.Bronze) },
            { "Iron", c => IcIngot(c, P.Iron) }, { "Gold", IcGold }, { "Charcoal", IcCharcoal }, { "Tools", IcTools }, { "Coin", IcCoin },
            { "Steel", c => IcIngot(c, P.Steel) }, { "Runestone", IcRunestone },
            { "Saltpetre", IcSaltpetre }, { "Gunpowder", IcGunpowder }, { "Quicksilver", IcQuicksilver }, { "Coal", IcCoal }, { "Gears", IcGears }, { "Aether", IcAether }, { "Oil", IcOil }, { "Fuel", IcFuel }, { "Uranium", IcUranium }, { "PowerCells", IcPowerCells }, { "Silicon", IcSilicon }, { "Microchips", IcMicrochips },
        };
        foreach (var res in db.resources)
            if (res != null && drawers.TryGetValue(res.id, out var draw))
            {
                res.icon = IconPixel($"Icons/{res.id}.png", draw);
                EditorUtility.SetDirty(res);
            }
        art.peopleIcon = IconPixel("Icons/People.png", c => IcPerson(c, P.Blue, false));
        art.enemyIcon = IconPixel("Icons/Enemy.png", c => IcPerson(c, P.Red, true));
        art.researchIcon = IconPixel("Icons/Research.png", IcScroll);
        foreach (var t in db.itemTemplates)
        {
            if (t == null) continue;
            var look = t.look;
            float power = t.minPower;
            t.icon = IconPixel($"Icons/Gear/{t.id}.png", c => IcGear(c, look, power));
            EditorUtility.SetDirty(t);
        }
    }

    private static Sprite IconPixel(string path, System.Action<PixelCanvas> draw)
    {
        var c = new PixelCanvas(IconPx, IconPx);
        draw(c);
        c.Outline(P.Ink, 0.7f);
        return SavePixel(c, path, IconPx, new Vector2(0.5f, 0.5f));
    }

    private static Vector2 Iv(float x, float y) => new Vector2(x, y);

    // ---------- Resources ----------

    private static void IcFood(PixelCanvas c)
    {
        // a roast leg with a bone, and two berries
        c.Line(Iv(12, 8), Iv(16, 4), P.Bone[3], 2);
        c.Ellipse(Iv(16.5f, 4), 1.4f, 1.4f, P.Bone); c.Ellipse(Iv(15, 2.5f), 1.4f, 1.4f, P.Bone);
        c.Ellipse(Iv(9, 11), 6, 5, new[] { P.Red[0], P.Leather[2], P.Wood[3], P.Glow[2] });
        c.Ellipse(Iv(4, 4), 2, 2, P.Berry); c.Ellipse(Iv(7, 3), 2, 2, P.Berry);
    }

    private static void IcWood(PixelCanvas c)
    {
        c.Capsule(Iv(3, 7), Iv(14, 7), 3.5f, P.Wood);
        c.Ellipse(Iv(15.5f, 7), 3, 3.5f, new[] { P.Wood[1], P.Wood[3], P.Wood[3], P.Straw[3] });
        c.Put(15, 7, P.Wood[2]);
        c.Capsule(Iv(5, 13), Iv(13, 13), 3, P.Wood);
        c.Ellipse(Iv(14, 13), 2.5f, 3, new[] { P.Wood[1], P.Wood[3], P.Wood[3], P.Straw[3] });
    }

    private static void IcStone(PixelCanvas c)
    {
        c.Ellipse(Iv(10, 8), 8, 6, P.Stone);
        c.Ellipse(Iv(8, 10), 4, 2.5f, new[] { P.Stone[3], P.Stone[3], P.Stone[4], P.Stone[4] });
        c.Line(Iv(12, 4), Iv(15, 7), P.Stone[1]);
    }

    private static void IcIngot(PixelCanvas c, Color32[] r)
    {
        c.Polygon(new[] { Iv(2, 4), Iv(18, 4), Iv(15, 11), Iv(5, 11) }, l => l.y > 9 ? r[3] : l.x > 14 ? r[1] : r[2]);
        c.Polygon(new[] { Iv(5, 10), Iv(15, 10), Iv(13, 15), Iv(7, 15) }, l => l.y > 13 ? r[3] : l.x > 12 ? r[1] : r[2]);
        c.Put(8, 14, P.White);
    }

    private static void IcGold(PixelCanvas c)
    {
        c.Ellipse(Iv(8, 7), 6, 5, P.Gold); c.Ellipse(Iv(13, 11), 5, 4, P.Gold);
        c.Put(6, 9, P.White); c.Put(12, 13, P.White);
    }

    private static void IcCharcoal(PixelCanvas c)
    {
        c.Ellipse(Iv(7, 6), 5, 4, P.Coal); c.Ellipse(Iv(13, 7), 4.5f, 3.5f, P.Coal); c.Ellipse(Iv(10, 11), 4.5f, 3.5f, P.Coal);
        c.Put(9, 12, P.Fire[2]); c.Put(5, 7, P.Fire[1]);
    }

    private static void IcTools(PixelCanvas c)
    {
        c.Line(Iv(4, 3), Iv(15, 14), P.Wood[3], 2);
        c.Rect(13, 13, 18, 16, P.Iron[2]); c.Put(17, 16, P.Iron[3]);
        c.Line(Iv(15, 3), Iv(5, 13), P.Wood[2], 2);
        c.Line(Iv(2, 11), Iv(7, 16), P.Steel[2], 2);
    }

    private static void IcRunestone(PixelCanvas c)
    {
        // a dark standing stone with a glowing rune
        c.Polygon(new[] { Iv(5, 2), Iv(15, 2), Iv(14, 17), Iv(9, 19), Iv(6, 16) }, l => l.x < 8 ? P.Coal[3] : l.x > 12 ? P.Coal[1] : P.Coal[2]);
        for (int y = 6; y < 14; y++) c.Put(10, y, P.Rune[3]);
        c.Put(11, 11, P.Rune[2]); c.Put(12, 12, P.Rune[2]); c.Put(9, 8, P.Rune[2]); c.Put(8, 7, P.Rune[2]);
    }

    private static void IcSaltpetre(PixelCanvas c)
    {
        // a sack spilling white crystals
        c.Ellipse(Iv(10, 8), 6.5f, 6, P.Linen);
        c.Line(Iv(7, 14), Iv(13, 14), P.Leather[2], 2);
        for (int i = 0; i < 6; i++) c.Put(4 + i * 2, 2 + (i % 2), i % 2 == 0 ? P.White : P.Linen[3]);
    }

    private static void IcGunpowder(PixelCanvas c)
    {
        // a powder keg with a red cross, a little black powder spilt beside it
        c.Rect(5, 3, 14, 15, P.Wood[2]); c.Rect(5, 3, 6, 15, P.Wood[3]);
        c.Line(Iv(5, 6), Iv(14, 6), P.Iron[1]); c.Line(Iv(5, 12), Iv(14, 12), P.Iron[1]);
        c.Line(Iv(8, 7), Iv(11, 11), P.Red[2]); c.Line(Iv(11, 7), Iv(8, 11), P.Red[2]);
        for (int i = 0; i < 4; i++) c.Put(14 + i, 2 + (i % 2), P.Coal[1]);
    }

    private static void IcQuicksilver(PixelCanvas c)
    {
        // a glass flask of liquid silver
        c.Ellipse(Iv(10, 7), 6, 6, new[] { P.Steel[0], P.Steel[1], P.Steel[3], P.White });
        c.Rect(8, 12, 11, 17, P.Linen[3]);
        c.Rect(7, 17, 12, 18, P.Wood[2]);
        c.Put(7, 9, P.White);
    }

    private static void IcSilicon(PixelCanvas c)
    {
        // a cluster of clear quartz crystals
        foreach (var (x, h) in new[] { (6, 8), (10, 13), (14, 9) })
        {
            for (int y = 2; y < 2 + h; y++) { c.Put(x, y, P.Linen[3]); c.Put(x + 1, y, P.Stone[2]); c.Put(x - 1, y, P.White); }
            c.Put(x, 2 + h, P.White);
        }
    }

    private static void IcMicrochips(PixelCanvas c)
    {
        // a black chip with silver legs and a glowing die
        c.Rect(5, 5, 14, 14, P.Coal[1]);
        for (int i = 0; i < 4; i++) { c.Put(4, 6 + i * 2, P.Steel[3]); c.Put(15, 6 + i * 2, P.Steel[3]); c.Put(6 + i * 2, 4, P.Steel[3]); c.Put(6 + i * 2, 15, P.Steel[3]); }
        c.Rect(8, 8, 11, 11, P.Neon[2]);
    }

    private static void IcUranium(PixelCanvas c)
    {
        // a lump of ore glowing green
        c.Ellipse(Iv(10, 8), 7, 6, P.Rock);
        c.Put(8, 9, P.Ray[2]); c.Put(9, 10, P.Ray[3]); c.Put(12, 7, P.Ray[2]); c.Put(11, 6, P.Ray[1]); c.Put(6, 7, P.Ray[2]);
    }

    private static void IcPowerCells(PixelCanvas c)
    {
        // a glass cell with a glowing core and chrome caps
        c.Rect(6, 4, 14, 15, P.Chrome[1]); c.Rect(7, 6, 13, 13, P.Ray[1]); c.Rect(9, 7, 11, 12, P.Ray[3]);
        c.Rect(8, 16, 12, 17, P.Chrome[2]); c.Rect(8, 2, 12, 3, P.Chrome[2]);
    }

    private static void IcOil(PixelCanvas c)
    {
        // a fat black drop with a sheen
        c.Ellipse(Iv(10, 7), 6, 6, new[] { P.Coal[0], P.Coal[1], P.Coal[2], P.Coal[3] });
        for (int y = 12; y < 18; y++) { int h = (18 - y) / 2; c.Rect(10 - h, y, 10 + h, y, P.Coal[1]); }
        c.Put(7, 9, P.Aether[2]); c.Put(8, 10, P.Rune[2]);
    }

    private static void IcFuel(PixelCanvas c)
    {
        // a red jerrycan
        c.Rect(4, 2, 15, 15, P.Red[2]); c.Rect(4, 2, 5, 15, P.Red[3]); c.Rect(14, 2, 15, 15, P.Red[1]);
        c.Line(Iv(6, 5), Iv(13, 12), P.Red[1]); c.Line(Iv(13, 5), Iv(6, 12), P.Red[1]);
        c.Rect(8, 16, 12, 17, P.Iron[2]); c.Rect(12, 16, 14, 18, P.Iron[1]);
    }

    private static void IcCoal(PixelCanvas c)
    {
        // a heap of glossy black lumps
        c.Ellipse(Iv(7, 6), 5, 4, P.Coal); c.Ellipse(Iv(13, 6), 5, 4, P.Coal); c.Ellipse(Iv(10, 11), 5, 4.5f, P.Coal);
        c.Put(9, 13, P.Iron[3]); c.Put(14, 8, P.Iron[2]); c.Put(5, 7, P.Iron[2]);
    }

    private static void IcGears(PixelCanvas c)
    {
        // two meshing brass gears
        foreach (var (x, y, r) in new[] { (7f, 11f, 5f), (14f, 6f, 4f) })
        {
            for (int i = 0; i < 8; i++)
            {
                float a = i / 8f * Mathf.PI * 2f;
                c.Put(Iv(x + Mathf.Cos(a) * (r + 1), y + Mathf.Sin(a) * (r + 1)), P.Brass[1]);
            }
            c.Ellipse(Iv(x, y), r, r, P.Brass);
            c.Ellipse(Iv(x, y), r * 0.35f, r * 0.35f, new[] { P.Coal[1], P.Coal[1], P.Brass[0], P.Brass[0] });
        }
    }

    private static void IcAether(PixelCanvas c)
    {
        // a cluster of glowing violet crystals
        foreach (var (x, h) in new[] { (6, 9), (10, 14), (14, 8) })
        {
            for (int y = 2; y < 2 + h; y++) { c.Put(x, y, P.Aether[2]); c.Put(x + 1, y, P.Aether[1]); c.Put(x - 1, y, P.Aether[3]); }
            c.Put(x, 2 + h, P.Aether[3]);
        }
    }

    private static void IcCoin(PixelCanvas c)
    {
        for (int i = 0; i < 4; i++) { c.Ellipse(Iv(8, 4 + i * 2), 6, 2, P.Gold); }
        c.Ellipse(Iv(13, 11), 5, 5, P.Gold);
        c.Ellipse(Iv(13, 11), 2.5f, 2.5f, new[] { P.Gold[1], P.Gold[1], P.Gold[2], P.Gold[2] });
        c.Put(11, 13, P.White);
    }

    private static void IcPerson(PixelCanvas c, Color32[] team, bool raider)
    {
        c.Ellipse(Iv(10, 14.5f), 3.5f, 3.5f, P.Skin);
        c.Ellipse(Iv(10, 15.5f), 3.8f, 2.5f, P.Hair, l => l.y > 15.5f);
        c.Capsule(Iv(10, 4), Iv(10, 9), 3.5f, team);
        c.Line(Iv(9, 1), Iv(9, 4), P.Cloth[2], 2); c.Line(Iv(11, 1), Iv(11, 4), P.Cloth[1], 2);
        if (raider) { c.Line(Iv(14, 4), Iv(17, 14), P.Wood[3]); c.Rect(15, 12, 18, 15, P.Steel[2]); c.Line(Iv(6, 16), Iv(10, 16), P.Red[2]); }
    }

    private static void IcScroll(PixelCanvas c)
    {
        c.Rect(4, 4, 16, 15, P.Linen[3]);
        for (int y = 7; y <= 13; y += 2) c.Line(Iv(6, y), Iv(14, y), P.Linen[1]);
        c.Capsule(Iv(3, 4), Iv(17, 4), 1.6f, P.Linen); c.Capsule(Iv(3, 15), Iv(17, 15), 1.6f, P.Linen);
        c.Put(13, 6, P.Red[2]); c.Put(14, 6, P.Red[2]);
    }

    // ---------- Gear ----------

    private static Color32[] GearMetalRamp(float power) => power < 1 ? P.Stone : power < 2 ? P.Bronze : P.Steel;

    private static void IcGear(PixelCanvas c, GearLook look, float power)
    {
        var metal = GearMetalRamp(power);
        Vector2 h = Iv(10, 8);
        switch (look)
        {
            case GearLook.Cap:
                c.Ellipse(h, 7, 6, P.Leather, l => l.y >= h.y); c.Line(Iv(2, 8), Iv(18, 8), P.Leather[1], 2); break;
            case GearLook.Hood:
                c.Ellipse(h, 7, 7, P.Wolf); c.Ellipse(Iv(11, 6), 4, 4, P.Coal, l => l.y < 8);
                c.Line(Iv(5, 14), Iv(6, 18), P.Wolf[2], 2); c.Line(Iv(14, 14), Iv(13, 18), P.Wolf[2], 2); break;
            case GearLook.DomeHelm:
                c.Ellipse(h, 7, 7, P.Bronze, l => l.y >= h.y); c.Line(Iv(2, 8), Iv(18, 8), P.Bronze[1], 2); c.Line(Iv(8, 16), Iv(12, 16), P.Blue[2], 2); break;
            case GearLook.ConeHelm:
                c.Polygon(new[] { Iv(3, 7), Iv(17, 7), Iv(10, 18) }, l => l.x < 8 ? P.Steel[3] : l.x < 12 ? P.Steel[2] : P.Steel[1]);
                c.Line(Iv(3, 7), Iv(17, 7), P.Steel[1], 2); c.Line(Iv(10, 7), Iv(10, 3), P.Steel[2], 2); break;
            case GearLook.CrestedHelm:
                c.Ellipse(h, 7, 7, P.Steel, l => l.y >= h.y - 1); c.Line(Iv(5, 8), Iv(5, 3), P.Steel[2], 2);
                for (int x = 4; x <= 16; x++) c.Line(Iv(x, 14), Iv(x, 17), x < 10 ? P.Red[3] : P.Red[2]); break;
            case GearLook.HideVest:
            case GearLook.Jerkin:
            case GearLook.Scale:
            case GearLook.Mail:
            case GearLook.Plate:
            {
                var r = look == GearLook.HideVest ? P.Fur : look == GearLook.Jerkin ? P.Linen : look == GearLook.Scale ? P.Bronze : P.Steel;
                c.Polygon(new[] { Iv(4, 2), Iv(16, 2), Iv(17, 14), Iv(13, 17), Iv(7, 17), Iv(3, 14) }, l => l.x < 7 ? r[3] : l.x > 13 ? r[1] : r[2]);
                c.Ellipse(Iv(10, 17), 3, 2, P.Coal);   // neck hole
                for (int y = 4; y < 15; y++) for (int x = 5; x < 16; x++)
                {
                    bool dot = look == GearLook.Mail ? (x + y) % 2 == 0 : look == GearLook.Scale ? y % 2 == 0 && (x + y / 2) % 2 == 0 : look == GearLook.Plate ? y % 3 == 0 : look == GearLook.Jerkin ? (x + y) % 3 == 0 : (x * 3 + y) % 7 == 0;
                    if (dot && c.Get(x, y).a > 200) c.Put(x, y, r[1]);
                }
                break;
            }
            case GearLook.Club:
                c.Line(Iv(4, 3), Iv(10, 10), P.Wood[2], 2);
                c.Capsule(Iv(10, 10), Iv(15, 15), 3, P.Wood); break;
            case GearLook.Spear:
                c.Line(Iv(2, 2), Iv(15, 15), P.Wood[3], 2);
                c.Polygon(new[] { Iv(13, 17), Iv(19, 19), Iv(17, 13) }, _ => metal[3]); break;
            case GearLook.Sword:
            case GearLook.ShortSword:
            {
                int len = look == GearLook.ShortSword ? 9 : 12;
                c.Line(Iv(6, 6), Iv(6 + len, 6 + len), P.Steel[3], 2);
                c.Line(Iv(3, 8), Iv(8, 3), look == GearLook.ShortSword ? P.Gold[2] : metal[1], 2);
                c.Line(Iv(2, 2), Iv(5, 5), P.Leather[2], 2); break;
            }
            case GearLook.Axe:
                c.Line(Iv(3, 3), Iv(15, 15), P.Wood[3], 2);
                c.Polygon(new[] { Iv(11, 17), Iv(18, 18), Iv(18, 10), Iv(13, 12) }, l => l.x > 16 ? metal[3] : metal[2]); break;
            case GearLook.Bow:
                for (int i = -8; i <= 8; i++) c.Put(Mathf.RoundToInt(6 + 5 * (1 - i * i / 64f)), 10 + i, P.Wood[2]);
                c.Line(Iv(6, 2), Iv(6, 18), P.Linen[3]);
                c.Line(Iv(4, 10), Iv(18, 10), P.Wood[3]); c.Put(18, 10, P.Steel[3]); break;
            case GearLook.HideShield:
                c.Ellipse(Iv(10, 10), 7, 9, P.Leather); for (int i = 0; i < 4; i++) c.Put(6 + i * 3, 6 + (i % 2) * 6, P.Fur[1]); break;
            case GearLook.WickerShield:
                c.Ellipse(Iv(10, 9), 8, 8, P.Straw, l => (l - Iv(10, 17)).sqrMagnitude > 16);
                for (int y = 3; y < 16; y += 2) c.Line(Iv(4, y), Iv(16, y), P.Straw[1]); break;
            case GearLook.RoundShield:
                c.Ellipse(Iv(10, 10), 8.5f, 8.5f, P.Blue); c.Ellipse(Iv(10, 10), 2.5f, 2.5f, metal); break;
            case GearLook.TowerShield:
                c.Rect(4, 1, 15, 18, P.Blue[2]); c.Rect(4, 1, 5, 18, P.Blue[3]); c.Rect(14, 1, 15, 18, P.Blue[1]);
                c.Ellipse(Iv(10, 10), 2.5f, 2.5f, P.Gold); c.Line(Iv(5, 15), Iv(14, 16), P.Gold[2]); c.Line(Iv(5, 4), Iv(14, 3), P.Gold[2]); break;
            case GearLook.Charm:
                c.Line(Iv(4, 17), Iv(10, 9), P.Leather[2]); c.Line(Iv(16, 17), Iv(10, 9), P.Leather[2]);
                c.Polygon(new[] { Iv(8, 9), Iv(12, 9), Iv(10, 2) }, l => l.x < 10 ? P.Bone[3] : P.Bone[2]); break;
            case GearLook.Horn:
                c.Capsule(Iv(4, 6), Iv(14, 14), 2.5f, P.Bone); c.Ellipse(Iv(15, 15), 3, 3, P.Bone);
                c.Line(Iv(7, 7), Iv(8, 10), P.Gold[2], 2); break;
            case GearLook.Amulet:
                c.Line(Iv(4, 18), Iv(10, 10), P.Gold[2]); c.Line(Iv(16, 18), Iv(10, 10), P.Gold[2]);
                c.Ellipse(Iv(10, 7), 4, 4, P.Glow); c.Put(9, 8, P.White); break;
            case GearLook.Ring:
                c.Ellipse(Iv(10, 8), 6, 6, P.Gold, l => (l - Iv(10, 8)).sqrMagnitude > 9);
                c.Ellipse(Iv(10, 14), 3, 3, new[] { P.Blue[1], P.Blue[2], P.Blue[3], P.White }); break;
            case GearLook.Wreath:
                for (int i = 0; i < 12; i++)
                {
                    float a = (i / 12f) * Mathf.PI * 1.6f + 0.9f;
                    c.Ellipse(Iv(10 + Mathf.Cos(a) * 6.5f, 10 + Mathf.Sin(a) * 6.5f), 2, 1.3f, P.Leaf);
                }
                c.Line(Iv(8, 3), Iv(12, 3), P.Gold[2], 2); break;
        }
    }

    // ---------- Townsfolk ----------

    private static void GeneratePixelVillagers(ArtLibrary art)
    {
        ResetDir("Units/Villager");
        var plain = new FigureStyle { team = "Linen", head = GearLook.None, weapon = GearLook.None, body = GearLook.None };
        var worker = plain.Clone();
        worker.weapon = GearLook.Axe;
        var pivot = PixelFigure.PivotFor(plain);
        Sprite Frame(FigureStyle s, int f, string name) => SavePixel(PixelFigure.Draw(s, f), $"Units/Villager/Villager_{name}.png", PixelsPerTile, pivot);
        art.villagerIdle = Frame(plain, 0, "Idle");
        art.villagerWalk = new List<Sprite> { Frame(plain, 1, "Walk0"), Frame(plain, 2, "Walk1"), Frame(plain, 3, "Walk2"), Frame(plain, 4, "Walk3") };
        art.villagerWork = new List<Sprite> { Frame(worker, 6, "Work0"), Frame(worker, 8, "Work1") };
    }
}
