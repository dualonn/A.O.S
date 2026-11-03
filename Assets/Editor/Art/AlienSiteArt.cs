using UnityEditor;
using UnityEngine;
using P = PixelPalette;
using K = BuildingKit;

/// <summary>
/// What the wild sites look like on other worlds: precursor ruins of black glass with glowing
/// glyphs, sunken precursor vaults, alien monoliths, and crashed ships in their craters.
/// Only these are drawn (the home world's sites are left as they are).
/// (Tools > Age of Stickman > Setup Alien Sites.)
/// </summary>
public static partial class PlaceholderArt
{
    [MenuItem("Tools/Age of Stickman/Setup Alien Sites")]
    public static void SetupAlienSites()
    {
        var art = AssetDatabase.LoadAssetAtPath<ArtLibrary>("Assets/Resources/ArtLibrary.asset");
        ResetDir("World/AlienSites");
        art.alienRuins = AlienPx("PrecursorRuins", 3, k => PxPrecursorRuins(k, false));
        art.alienRuinsLooted = AlienPx("PrecursorRuinsLooted", 3, k => PxPrecursorRuins(k, true));
        art.alienVault = AlienPx("PrecursorVault", 3, k => PxPrecursorVault(k, false));
        art.alienVaultSealed = AlienPx("PrecursorVaultSealed", 3, k => PxPrecursorVault(k, true));
        art.alienMonolith = AlienPx("Monolith", 2, k => PxMonolith(k, false));
        art.alienMonolithUsed = AlienPx("MonolithUsed", 2, k => PxMonolith(k, true));
        art.alienCrash = AlienPx("CrashSite", 1, PxCrashSite);
        EditorUtility.SetDirty(art);
        AssetDatabase.SaveAssets();
        Debug.Log("Alien sites set up.");
    }

    private static Sprite AlienPx(string name, int tiles, System.Action<K> draw)
    {
        int w = tiles * TilePx + MarginSide * 2, h = tiles * TilePx + MarginBottom + MarginTop;
        var c = new PixelCanvas(w, h);
        var k = new K(c, MarginSide, MarginBottom, 0, 0, 1, name.GetHashCode());
        draw(k);
        return SavePixel(c, $"World/AlienSites/{name}.png", PixelsPerTile, new Vector2(MarginSide / (float)w, MarginBottom / (float)h));
    }

    private static readonly Color32[] Obsidian = { P.Hex("0c0c12"), P.Hex("1a1a26"), P.Hex("2c2c40"), P.Hex("4a4a66") };

    /// <summary>A pillar of black glass with a glowing glyph line up it.</summary>
    private static void GlassPillar(K k, int x, int y, int h, bool lit, bool broken)
    {
        for (int yy = y; yy < y + h; yy++)
        {
            k.Px(x, yy, Obsidian[3]); k.Px(x + 1, yy, Obsidian[2]); k.Px(x + 2, yy, Obsidian[2]); k.Px(x + 3, yy, Obsidian[1]);
            if ((yy - y) % 5 == 2) k.Px(x + 2, yy, lit ? P.Neon[3] : Obsidian[3]);
        }
        if (broken) { k.Px(x + 1, y + h, Obsidian[2]); k.Px(x + 2, y + h + 1, Obsidian[3]); }
        else k.HLine(x - 1, x + 4, y + h, Obsidian[3]);
    }

    private static void PxPrecursorRuins(K k, bool looted)
    {
        k.Ground(2, 2, 94, 30, P.Stone);
        k.Ellipse(48, 12, 40, 8, new[] { Obsidian[1], Obsidian[2], Obsidian[2], Obsidian[3] }, l => l.y >= 6);   // a black glass floor
        for (int i = 0; i < 5; i++) k.HLine(18 + i * 13, 24 + i * 13, 12, looted ? Obsidian[3] : P.Neon[2]);
        GlassPillar(k, 12, 10, 34, !looted, false);
        GlassPillar(k, 30, 14, 22, !looted, true);
        GlassPillar(k, 66, 14, 28, !looted, true);
        GlassPillar(k, 82, 10, 36, !looted, false);
        // a fallen ring of the gate, half buried
        k.Ellipse(50, 10, 12, 6, Obsidian, l => l.y >= 10 && Vector2.Distance(l, new Vector2(50, 10)) > 7);
        // the casket: open and dark once looted, sealed and glowing before
        k.Rect(42, 14, 56, 22, Obsidian[2]); k.HLine(42, 56, 22, Obsidian[3]);
        if (looted) k.Rect(44, 20, 54, 24, Obsidian[0]);
        else { k.HLine(44, 54, 18, P.Neon[3]); k.Px(49, 23, P.White); }
        k.Finish();
    }

    private static void PxPrecursorVault(K k, bool sealedShut)
    {
        k.Ground(2, 2, 94, 40, P.Stone);
        // a sunken hexagonal pit with a door at the bottom, a ring of glyph-stones round it
        k.Polygon(new[] { new Vector2(22, 8), new Vector2(74, 8), new Vector2(86, 20), new Vector2(74, 32), new Vector2(22, 32), new Vector2(10, 20) },
            l => l.y > 26 ? (Color32?)Obsidian[3] : Obsidian[1]);
        k.Polygon(new[] { new Vector2(32, 12), new Vector2(64, 12), new Vector2(70, 20), new Vector2(64, 28), new Vector2(32, 28), new Vector2(26, 20) },
            _ => Obsidian[0]);
        var glow = sealedShut ? Obsidian[2] : P.Arcane[3];
        k.Ellipse(48, 20, 9, 6, sealedShut ? Obsidian : new[] { P.Arcane[0], P.Arcane[1], P.Arcane[2], P.Arcane[3] });
        k.HLine(40, 56, 20, glow);
        for (int i = 0; i < 6; i++)
        {
            float a = i * Mathf.PI / 3f;
            int x = Mathf.RoundToInt(48 + Mathf.Cos(a) * 42), y = Mathf.RoundToInt(20 + Mathf.Sin(a) * 15);
            k.Rect(x - 2, y, x + 2, y + 10, Obsidian[2]); k.VLine(x - 2, y, y + 10, Obsidian[3]);
            k.Px(x, y + 6, sealedShut ? Obsidian[3] : P.Neon[3]);
        }
        k.Finish();
    }

    private static void PxMonolith(K k, bool used)
    {
        k.Ground(6, 2, 58, 18, P.Stone);
        k.Rect(20, 4, 44, 8, Obsidian[2]); k.HLine(20, 44, 8, Obsidian[3]);
        // a tall black slab, perfectly smooth, a seam of light down its face
        k.Rect(25, 9, 39, 58, Obsidian[1]); k.VLine(25, 9, 58, Obsidian[3]); k.VLine(39, 9, 58, Obsidian[0]);
        k.HLine(25, 39, 58, Obsidian[3]);
        var glow = used ? Obsidian[2] : P.Gold[3];
        k.VLine(32, 12, 55, glow);
        for (int y = 16; y < 54; y += 9) k.HLine(29, 35, y, used ? Obsidian[2] : P.Arcane[3]);
        if (!used)
        {
            // shards of stone hanging in the air round it
            foreach (var (x, y) in new[] { (14, 40), (50, 46), (12, 24), (52, 28) })
                k.Polygon(new[] { new Vector2(x, y - 3), new Vector2(x + 2, y), new Vector2(x, y + 3), new Vector2(x - 2, y) }, _ => Obsidian[3]);
            k.Px(32, 60, P.White);
        }
        k.Finish();
    }

    private static void PxCrashSite(K k)
    {
        k.Ellipse(16, 6, 15, 6, P.Dirt, l => l.y >= 1);
        k.Ellipse(16, 6, 11, 4, new[] { P.Coal[0], P.Coal[1], P.Dirt[1], P.Dirt[2] }, l => l.y >= 3);   // a scorched crater
        // a small ship nose-down in it, a broken fin and some scattered panels
        k.Polygon(new[] { new Vector2(8, 10), new Vector2(22, 18), new Vector2(26, 14), new Vector2(12, 6) }, l => l.y > 13 ? (Color32?)P.Steel[3] : P.Steel[2]);
        k.Line(22, 18, 27, 23, P.Steel[1], 2);
        k.Px(14, 10, P.Neon[3]);
        k.Rect(25, 5, 27, 6, P.Steel[2]); k.Rect(4, 4, 6, 5, P.Steel[1]);
        for (int i = 0; i < 4; i++) k.Px(20 + i, 22 + i * 2, P.Coal[2]);   // a thread of smoke
        k.Finish();
    }
}
