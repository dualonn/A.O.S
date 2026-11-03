using UnityEditor;
using UnityEngine;
using P = PixelPalette;

/// <summary>
/// Icons for the seasons and the weather (Tools > Age of Stickman > Setup Seasons and Weather).
/// Draws only these icons, into their own folder, and the WeatherArt asset the HUD reads.
/// </summary>
public static partial class PlaceholderArt
{
    [MenuItem("Tools/Age of Stickman/Setup Seasons and Weather")]
    public static void SetupSeasonsAndWeather()
    {
        const string path = "Assets/Resources/WeatherArt.asset";
        var art = AssetDatabase.LoadAssetAtPath<WeatherArt>(path);
        if (art == null)
        {
            art = ScriptableObject.CreateInstance<WeatherArt>();
            AssetDatabase.CreateAsset(art, path);
        }
        var seasons = (Season[])System.Enum.GetValues(typeof(Season));
        art.seasons = new Sprite[seasons.Length];
        foreach (var s in seasons)
        {
            var season = s;
            art.seasons[(int)s] = IconPixel($"Weather/Season_{s}.png", c => IcSeason(c, season));
        }
        var kinds = (WeatherKind[])System.Enum.GetValues(typeof(WeatherKind));
        art.weather = new Sprite[kinds.Length];
        foreach (var k in kinds)
        {
            var kind = k;
            art.weather[(int)k] = IconPixel($"Weather/Weather_{k}.png", c => IcWeather(c, kind));
        }
        EditorUtility.SetDirty(art);
        AssetDatabase.SaveAssets();
        Debug.Log("Seasons and weather set up.");
    }

    private static readonly Color32[] WxSunRamp = { P.Hex("c86a10"), P.Hex("f0a020"), P.Hex("ffd84a"), P.Hex("fff6c0") };
    private static readonly Color32[] WxCloudRamp = { P.Hex("6a7484"), P.Hex("a4aebc"), P.Hex("d6dde6"), P.Hex("f6f8fb") };
    private static readonly Color32[] WxStormRamp = { P.Hex("1e2230"), P.Hex("363c50"), P.Hex("525a72"), P.Hex("7a849c") };
    private static readonly Color32[] WxLeafRamp = { P.Hex("1f5a2a"), P.Hex("2f8a3a"), P.Hex("5cbc4e"), P.Hex("a8e070") };
    private static readonly Color32[] WxAutumnRamp = { P.Hex("7a2a10"), P.Hex("c0501a"), P.Hex("e88a2a"), P.Hex("ffc060") };
    private static readonly Color32 WxDrop = P.Hex("6aa8f0"), WxIce = P.Hex("e8f4ff"), WxBolt = P.Hex("fff6a0");

    private static void WxSun(PixelCanvas c, Vector2 at, float r)
    {
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI / 4f;
            c.Line(at + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (r + 1.5f), at + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (r + 3f), WxSunRamp[2]);
        }
        c.Ellipse(at, r, r, WxSunRamp);
    }

    private static void WxCloud(PixelCanvas c, float y, Color32[] ramp)
    {
        c.Ellipse(new Vector2(7, y), 4.5f, 3.5f, ramp);
        c.Ellipse(new Vector2(12, y + 1.5f), 5f, 4.2f, ramp);
        c.Ellipse(new Vector2(15.5f, y - 0.5f), 3.2f, 2.8f, ramp);
        c.Line(new Vector2(4, y - 2.5f), new Vector2(18, y - 2.5f), ramp[1]);
    }

    private static void WxFlake(PixelCanvas c, int x, int y, int r, Color32 col)
    {
        c.Line(new Vector2(x - r, y), new Vector2(x + r, y), col);
        c.Line(new Vector2(x, y - r), new Vector2(x, y + r), col);
        c.Line(new Vector2(x - r + 1, y - r + 1), new Vector2(x + r - 1, y + r - 1), col);
        c.Line(new Vector2(x - r + 1, y + r - 1), new Vector2(x + r - 1, y - r + 1), col);
    }

    private static void IcSeason(PixelCanvas c, Season s)
    {
        switch (s)
        {
            case Season.Spring:
                // a sprout with a blossom
                c.Line(new Vector2(10, 2), new Vector2(10, 11), WxLeafRamp[1], 2);
                c.Ellipse(new Vector2(6.5f, 9), 3.5f, 2f, WxLeafRamp);
                c.Ellipse(new Vector2(13.5f, 7), 3.5f, 2f, WxLeafRamp);
                c.Ellipse(new Vector2(10, 14), 3f, 3f, new[] { P.Hex("c04a7a"), P.Hex("e87aa8"), P.Hex("ffb0d0"), P.Hex("fff0f6") });
                c.Put(10, 14, WxSunRamp[2]);
                break;
            case Season.Summer:
                WxSun(c, new Vector2(10, 10), 4.5f);
                break;
            case Season.Autumn:
                // a maple-ish leaf, falling
                c.Polygon(new[] { new Vector2(10, 17), new Vector2(15, 12), new Vector2(17, 13), new Vector2(15, 7), new Vector2(10, 3), new Vector2(5, 7), new Vector2(3, 13), new Vector2(5, 12) },
                    l => l.x < 10 ? (Color32?)WxAutumnRamp[2] : WxAutumnRamp[1]);
                c.Line(new Vector2(10, 2), new Vector2(10, 15), WxAutumnRamp[0]);
                c.Line(new Vector2(10, 9), new Vector2(14, 12), WxAutumnRamp[0]);
                c.Line(new Vector2(10, 9), new Vector2(6, 12), WxAutumnRamp[0]);
                break;
            case Season.Winter:
                WxFlake(c, 10, 10, 7, WxIce);
                WxFlake(c, 10, 10, 3, P.Hex("9cc8f0"));
                break;
            case Season.DrySeason:
                // cracked earth under a low sun
                c.Rect(2, 2, 18, 8, P.Dirt[2]);
                c.Line(new Vector2(4, 8), new Vector2(7, 4), P.Dirt[0]); c.Line(new Vector2(7, 4), new Vector2(11, 6), P.Dirt[0]);
                c.Line(new Vector2(11, 6), new Vector2(15, 2), P.Dirt[0]); c.Line(new Vector2(13, 8), new Vector2(11, 6), P.Dirt[0]);
                WxSun(c, new Vector2(10, 14), 2.8f);
                break;
            case Season.WetSeason:
                c.Ellipse(new Vector2(8, 8), 5, 3, WxLeafRamp);
                c.Line(new Vector2(3, 6), new Vector2(13, 10), WxLeafRamp[0]);
                c.Ellipse(new Vector2(14, 14), 2, 2.6f, new[] { P.Hex("2a5aa0"), WxDrop, P.Hex("a8d0ff"), P.White });
                c.Put(14, 17, WxDrop);
                break;
            case Season.Rains:
                WxCloud(c, 13, WxCloudRamp);
                for (int i = 0; i < 5; i++) c.Line(new Vector2(5 + i * 3, 7), new Vector2(4 + i * 3, 2), WxDrop);
                break;
            default:   // no seasons: a grey crescent
                c.Ellipse(new Vector2(10, 10), 6, 6, new[] { P.Hex("4a4a54"), P.Hex("7a7a86"), P.Hex("a8a8b4"), P.Hex("d0d0da") });
                for (int y = 0; y < c.H; y++) for (int x = 0; x < c.W; x++) if ((x - 13) * (x - 13) + (y - 12) * (y - 12) <= 25) c.Clear(x, y);
                break;
        }
    }

    private static void IcWeather(PixelCanvas c, WeatherKind k)
    {
        switch (k)
        {
            case WeatherKind.Clear:
                WxSun(c, new Vector2(10, 10), 4f);
                break;
            case WeatherKind.Rain:
                WxCloud(c, 13, WxCloudRamp);
                for (int i = 0; i < 4; i++) c.Line(new Vector2(6 + i * 3, 7), new Vector2(5 + i * 3, 3), WxDrop);
                break;
            case WeatherKind.Storm:
                WxCloud(c, 13, WxStormRamp);
                c.Line(new Vector2(11, 9), new Vector2(8, 5), WxBolt, 2); c.Line(new Vector2(8, 5), new Vector2(11, 5), WxBolt, 2); c.Line(new Vector2(11, 5), new Vector2(8, 1), WxBolt, 2);
                break;
            case WeatherKind.Snow:
                WxCloud(c, 13, WxCloudRamp);
                foreach (var (x, y) in new[] { (6, 6), (11, 4), (15, 7), (8, 2) }) { c.Put(x, y, WxIce); c.Put(x + 1, y, WxIce); c.Put(x, y + 1, WxIce); c.Put(x - 1, y, WxIce); c.Put(x, y - 1, WxIce); }
                break;
            case WeatherKind.Fog:
                for (int i = 0; i < 4; i++) c.Line(new Vector2(2 + (i % 2) * 2, 4 + i * 4), new Vector2(17 - (i % 2) * 3, 4 + i * 4), WxCloudRamp[i % 2 == 0 ? 2 : 1], 2);
                break;
            case WeatherKind.HeatWave:
                WxSun(c, new Vector2(10, 13), 3.5f);
                for (int i = 0; i < 3; i++)
                    for (int x = 3; x < 18; x++) c.Put(x, 3 + i * 2 + ((x / 2) % 2), P.Hex("ff6a3a"));
                break;
            case WeatherKind.Sandstorm:
                for (int i = 0; i < 4; i++)
                {
                    int y = 4 + i * 4;
                    c.Line(new Vector2(2 + i, y), new Vector2(14 + (i % 2) * 3, y), P.Hex(i % 2 == 0 ? "d8a860" : "b88440"), 2);
                    c.Put(15 + (i % 2) * 3, y + 1, P.Hex("e8c080"));
                }
                break;
            case WeatherKind.AshFall:
                WxCloud(c, 13, WxStormRamp);
                foreach (var (x, y) in new[] { (6, 6), (10, 3), (14, 7), (8, 1), (16, 3), (12, 6) }) c.Put(x, y, P.Hex("8a8480"));
                break;
            case WeatherKind.Aurora:
                c.Rect(1, 1, 19, 19, P.Hex("101830"));
                for (int band = 0; band < 3; band++)
                    for (int x = 2; x < 19; x++)
                    {
                        int y = 6 + band * 4 + Mathf.RoundToInt(Mathf.Sin(x * 0.5f + band) * 1.5f);
                        c.Put(x, y, P.Hex(band == 1 ? "7affc0" : "40c890"));
                    }
                c.Put(4, 17, P.White); c.Put(15, 16, P.White);
                break;
        }
    }
}
