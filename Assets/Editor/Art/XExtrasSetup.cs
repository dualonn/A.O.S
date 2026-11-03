using UnityEditor;
using UnityEngine;
using P = PixelPalette;

/// <summary>
/// Art for helper X's extras: the natural wonder map icon (on the zoomed-out maps). Draws only
/// that one icon; safe to run again. (Tools > Age of Stickman > Setup Extras.)
/// </summary>
public static partial class PlaceholderArt
{
    [MenuItem("Tools/Age of Stickman/Setup Extras")]
    public static void SetupExtras()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Setup Extras: stop playing first.");
            return;
        }
        var art = AssetDatabase.LoadAssetAtPath<ArtLibrary>("Assets/Resources/ArtLibrary.asset");
        if (art == null) { Debug.LogError("Setup Extras: no ArtLibrary at Assets/Resources/ArtLibrary.asset."); return; }
        var icon = IconPixel("World/Sites/Icon_NaturalWonder.png", IcNaturalWonder);
        int k = (int)SiteKind.NaturalWonder;
        while (art.siteIcons.Count < k) art.siteIcons.Add(null);
        if (art.siteIcons.Count == k) art.siteIcons.Add(icon);
        else art.siteIcons[k] = icon;
        EditorUtility.SetDirty(art);
        AssetDatabase.SaveAssets();
        Debug.Log("Extras set up: natural wonder map icon.");
    }

    /// <summary>A snow-capped peak under a golden starburst: a place of wonder.</summary>
    private static void IcNaturalWonder(PixelCanvas c)
    {
        // The starburst behind: eight short rays round a bright heart.
        var heart = new Vector2(10, 14);
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI / 4f;
            float len = i % 2 == 0 ? 5.5f : 3.5f;
            c.LineRaw(heart, heart + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * len, i % 2 == 0 ? P.Gold[2] : P.Gold[1]);
        }
        c.Ellipse(heart, 2.2f, 2.2f, new[] { P.Gold[1], P.Gold[2], P.Gold[3], P.White });
        // The peak: lit on the left, shaded on the right, snow on top.
        c.Polygon(new[] { new Vector2(1, 1), new Vector2(10, 12), new Vector2(19, 1) },
            l => l.x < 10 ? P.Stone[3] : P.Stone[1]);
        c.Polygon(new[] { new Vector2(6.5f, 7.5f), new Vector2(10, 12), new Vector2(13.5f, 7.5f), new Vector2(11.5f, 8.5f), new Vector2(10, 7), new Vector2(8.5f, 8.5f) },
            l => l.x < 10 ? P.White : P.Stone[4]);
        // A waterfall down its face, and a pool at its foot.
        for (int y = 2; y < 8; y++) c.Put(9 + (y % 2), y, y % 3 == 0 ? P.Water[4] : P.Water[3]);
        c.Rect(6, 1, 13, 2, P.Water[2]);
        c.Put(8, 2, P.Water[4]); c.Put(11, 1, P.Water[4]);
    }
}
