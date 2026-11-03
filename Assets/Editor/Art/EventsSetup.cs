using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using P = PixelPalette;

/// <summary>
/// Art for the random events: an icon for each kind, the fallen star and lost cart that wait on
/// the map, the merchants' ox-cart and the star trader's ship. Draws only this art.
/// (Tools > Age of Stickman > Setup Events.)
/// </summary>
public static partial class PlaceholderArt
{
    private static readonly (string id, System.Action<PixelCanvas> draw)[] EventIcons =
    {
        ("Merchant", IcEvMerchant), ("Festival", IcEvFestival), ("Harvest", IcEvHarvest), ("Drought", IcEvDrought),
        ("Plague", IcEvPlague), ("Meteor", IcEvMeteor), ("Refugees", IcEvRefugees), ("WanderingHero", IcEvHero),
        ("Stranger", IcEvStranger), ("GoldRush", IcEvGold), ("Wildfire", IcEvFire), ("Eclipse", IcEvEclipse),
        ("Rival", IcEvRival), ("LostCaravan", IcEvCart), ("Scholars", IcEvScholars), ("SolarFlare", IcEvFlare),
        ("Derelict", IcEvDerelict), ("Comet", IcEvComet),
    };

    [MenuItem("Tools/Age of Stickman/Setup Events")]
    public static void SetupEvents()
    {
        const string path = "Assets/Resources/EventArt.asset";
        var art = AssetDatabase.LoadAssetAtPath<EventArt>(path);
        if (art == null)
        {
            Directory.CreateDirectory("Assets/Resources");
            art = ScriptableObject.CreateInstance<EventArt>();
            AssetDatabase.CreateAsset(art, path);
        }
        art.iconIds = new List<string>();
        art.icons = new List<Sprite>();
        foreach (var (id, draw) in EventIcons)
        {
            art.iconIds.Add(id);
            art.icons.Add(IconPixel($"Icons/Events/{id}.png", draw));
        }

        // A fallen star, still glowing in its crater.
        var m = new PixelCanvas(32, 24);
        m.Ellipse(new Vector2(16, 6), 12, 4, P.Dirt);
        m.Ellipse(new Vector2(16, 7), 7, 2.5f, new[] { P.Coal[0], P.Coal[1], P.Coal[1], P.Coal[2] });
        m.Ellipse(new Vector2(16, 10), 5, 4.5f, new[] { P.Starmetal[0], P.Starmetal[1], P.Starmetal[2], P.Starmetal[3] });
        m.Put(14, 12, P.Fire[3]); m.Put(17, 9, P.Fire[2]); m.Put(18, 12, P.White);
        for (int i = 0; i < 4; i++) m.Put(10 + i * 4, 15 + (i % 2) * 3, P.Fire[2]);
        m.Outline(P.Ink, 0.7f);
        art.meteor = SavePixel(m, "Events/Meteor.png", TilePx, new Vector2(0.5f, 0.2f));

        // A lost cart tipped in the grass, its load spilled.
        art.cart = SavePixel(EvCart(true), "Events/LostCart.png", TilePx, new Vector2(0.5f, 0.15f));

        // The merchants' ox-cart, walking (4 frames).
        art.caravanWalk = new List<Sprite>();
        for (int f = 0; f < 4; f++) art.caravanWalk.Add(SavePixel(EvCaravan(f), $"Events/Caravan_{f}.png", TilePx, new Vector2(0.5f, 0.1f)));

        // The star trader: a fat-bellied freighter with its hold open, on landing struts.
        var s = new PixelCanvas(64, 40);
        s.Ellipse(new Vector2(32, 20), 26, 10, P.Steel);
        s.Rect(14, 18, 50, 20, P.Steel[1]);
        s.Ellipse(new Vector2(48, 24), 6, 4, new[] { P.Water[1], P.Water[2], P.Water[3], P.White });
        s.Rect(22, 12, 36, 18, P.Brass[2]);
        s.Rect(24, 13, 34, 17, P.Coal[1]);
        for (int i = 0; i < 3; i++) s.Rect(25 + i * 3, 14, 26 + i * 3, 15, i == 1 ? P.Gold[3] : P.Wood[2]);   // crates in the hold
        foreach (int x in new[] { 14, 50 }) { s.Line(new Vector2(x, 12), new Vector2(x - 3, 3), P.Steel[1], 2); s.Rect(x - 5, 2, x - 1, 3, P.Steel[2]); }
        s.Ellipse(new Vector2(6, 20), 3, 3, new[] { P.Fire[1], P.Fire[2], P.Fire[3], P.White });
        s.Outline(P.Ink, 0.7f);
        art.starTrader = SavePixel(s, "Events/StarTrader.png", TilePx, new Vector2(0.5f, 0.05f));

        EditorUtility.SetDirty(art);
        AssetDatabase.SaveAssets();
        Debug.Log($"Events set up: {art.icons.Count} icons.");
    }

    private static PixelCanvas EvCart(bool tipped)
    {
        var c = new PixelCanvas(40, 28);
        c.Rect(8, 8, 30, 15, P.Wood[2]);
        c.Rect(8, 15, 30, 16, P.Wood[3]);
        for (int x = 10; x < 30; x += 5) c.Line(new Vector2(x, 8), new Vector2(x, 15), P.Wood[1]);
        c.Ellipse(new Vector2(12, 7), 4, 4, P.Wood);
        c.Ellipse(new Vector2(12, 7), 1.2f, 1.2f, new[] { P.Coal[1], P.Coal[1], P.Coal[1], P.Coal[1] });
        if (tipped)
        {
            c.Ellipse(new Vector2(31, 14), 3.5f, 3.5f, P.Wood);   // the wheel off
            c.Rect(31, 4, 36, 8, P.Linen[2]); c.Rect(2, 4, 7, 9, P.Straw[2]);   // spilled sacks and bales
            c.Put(34, 6, P.Gold[3]);
        }
        else
        {
            c.Ellipse(new Vector2(27, 7), 4, 4, P.Wood);
            c.Polygon(new[] { new Vector2(9, 16), new Vector2(29, 16), new Vector2(27, 24), new Vector2(11, 24) }, _ => P.Linen[2]);   // the canvas cover
            c.Line(new Vector2(11, 20), new Vector2(27, 20), P.Linen[1]);
        }
        c.Outline(P.Ink, 0.7f);
        return c;
    }

    private static PixelCanvas EvCaravan(int f)
    {
        var c = new PixelCanvas(64, 36);
        // the cart (behind), then the ox, then the merchant walking beside
        var cart = EvCart(false);
        for (int y = 0; y < 28; y++)
            for (int x = 0; x < 40; x++)
            {
                var px = cart.Get(x, y);
                if (px.a > 0) c.Put(x, y, px);
            }
        int step = f % 2 == 0 ? 0 : 1;
        var ox = P.Leather;
        c.Ellipse(new Vector2(48, 13), 9, 5, ox);
        c.Ellipse(new Vector2(58, 15), 4, 3.5f, ox);
        c.Line(new Vector2(57, 18), new Vector2(55, 21), P.Bone[3]); c.Line(new Vector2(60, 18), new Vector2(62, 21), P.Bone[3]);   // horns
        foreach (float lx in new[] { 42f, 46f, 51f, 55f }) c.Line(new Vector2(lx, 9), new Vector2(lx + (((int)lx + f) % 2 == 0 ? step : -step), 3), ox[0], 2);
        c.Line(new Vector2(30, 11), new Vector2(40, 13), P.Wood[1], 2);   // the shaft
        // the merchant: a stick figure in a robe with a staff
        int mx = 36, by = 2 + step;
        c.Polygon(new[] { new Vector2(mx - 3, by), new Vector2(mx + 3, by), new Vector2(mx + 2, by + 9), new Vector2(mx - 2, by + 9) }, _ => P.Purple[2]);
        c.Ellipse(new Vector2(mx, by + 12), 2.4f, 2.4f, P.Skin);
        c.Line(new Vector2(mx + 4, by - 1), new Vector2(mx + 4, by + 14), P.Wood[2]);
        c.Outline(P.Ink, 0.7f);
        return c;
    }

    private static Vector2 Ev(float x, float y) => new Vector2(x, y);

    private static void IcEvMerchant(PixelCanvas c)
    {
        c.Ellipse(Ev(10, 8), 6, 5, P.Linen);   // a sack
        c.Line(Ev(8, 13), Ev(12, 13), P.Linen[0], 2);
        c.Ellipse(Ev(15, 5), 3, 3, P.Gold); c.Put(15, 5, P.Gold[3]);
        c.Ellipse(Ev(5, 4), 2.5f, 2.5f, P.Gold);
    }

    private static void IcEvFestival(PixelCanvas c)
    {
        c.Line(Ev(2, 16), Ev(18, 14), P.Wood[2]);
        for (int i = 0; i < 5; i++) c.Polygon(new[] { Ev(3 + i * 3, 15.5f - i * 0.4f), Ev(6 + i * 3, 15f - i * 0.4f), Ev(4.5f + i * 3, 11 - i * 0.4f) }, i % 2 == 0 ? P.Red[2] : P.Gold[2]);
        c.Ellipse(Ev(10, 5), 3, 3, new[] { P.Fire[1], P.Fire[2], P.Fire[3], P.White });
    }

    private static void IcEvHarvest(PixelCanvas c)
    {
        for (int i = 0; i < 4; i++) { c.Line(Ev(5 + i * 3, 2), Ev(7 + i * 2, 15), P.Straw[2]); c.Ellipse(Ev(7 + i * 2, 15), 1.5f, 2.5f, P.Straw); }
        c.Line(Ev(4, 7), Ev(16, 7), P.Red[2], 2);
    }

    private static void IcEvDrought(PixelCanvas c)
    {
        c.Rect(2, 2, 17, 8, P.Dirt[2]);
        c.Line(Ev(4, 3), Ev(8, 7), P.Dirt[0]); c.Line(Ev(8, 7), Ev(12, 4), P.Dirt[0]); c.Line(Ev(12, 4), Ev(15, 7), P.Dirt[0]);
        c.Ellipse(Ev(14, 14), 4, 4, new[] { P.Fire[1], P.Fire[2], P.Fire[3], P.White });
    }

    private static void IcEvPlague(PixelCanvas c)
    {
        c.Ellipse(Ev(10, 9), 6, 6, P.Green);
        for (int i = 0; i < 8; i++) { float a = i * 0.785f; c.Put(Ev(10 + Mathf.Cos(a) * 8, 9 + Mathf.Sin(a) * 8), P.Green[2]); }
        c.Put(8, 10, P.Green[0]); c.Put(12, 8, P.Green[0]);
    }

    private static void IcEvMeteor(PixelCanvas c)
    {
        c.Line(Ev(17, 17), Ev(8, 8), P.Fire[2], 3);
        c.Line(Ev(17, 17), Ev(8, 8), P.Fire[3]);
        c.Ellipse(Ev(7, 7), 4, 4, P.Starmetal);
        c.Put(6, 8, P.White);
    }

    private static void IcEvRefugees(PixelCanvas c)
    {
        foreach (var (x, h) in new[] { (5, 10), (10, 12), (15, 8) })
        {
            c.Line(Ev(x, 2), Ev(x, 2 + h - 3), P.Linen[1], 2);
            c.Ellipse(Ev(x, 2 + h), 1.8f, 1.8f, P.Skin);
        }
        c.Rect(12, 5, 17, 9, P.Wood[2]);   // a bundle
    }

    private static void IcEvHero(PixelCanvas c)
    {
        c.Line(Ev(4, 2), Ev(15, 17), P.Steel[3], 2);
        c.Line(Ev(3, 7), Ev(9, 3), P.Gold[2], 2);
        c.Polygon(new[] { Ev(12, 2), Ev(18, 2), Ev(18, 10), Ev(15, 13), Ev(12, 10) }, P.Blue[2]);
    }

    private static void IcEvStranger(PixelCanvas c)
    {
        c.Polygon(new[] { Ev(4, 2), Ev(16, 2), Ev(13, 14), Ev(10, 17), Ev(7, 14) }, P.Coal[2]);
        c.Ellipse(Ev(10, 12), 3, 3, new[] { P.Coal[0], P.Coal[0], P.Coal[0], P.Coal[1] });
        c.Put(9, 12, P.Purple[3]); c.Put(11, 12, P.Purple[3]);
    }

    private static void IcEvGold(PixelCanvas c)
    {
        c.Ellipse(Ev(10, 6), 8, 4, P.Steel);   // a pan
        c.Put(8, 7, P.Gold[3]); c.Put(11, 6, P.Gold[3]); c.Put(13, 8, P.Gold[2]);
        c.Line(Ev(4, 12), Ev(9, 17), P.Wood[2], 2); c.Line(Ev(6, 17), Ev(12, 12), P.Steel[2], 2);   // a pick
    }

    private static void IcEvFire(PixelCanvas c)
    {
        c.Polygon(new[] { Ev(4, 2), Ev(16, 2), Ev(13, 10), Ev(10, 18), Ev(7, 10) }, P.Fire[1]);
        c.Polygon(new[] { Ev(7, 2), Ev(13, 2), Ev(11, 8), Ev(10, 12), Ev(9, 8) }, P.Fire[3]);
        c.Line(Ev(3, 2), Ev(3, 9), P.Wood[1], 2); c.Ellipse(Ev(3, 11), 2, 3, P.Leaf);
    }

    private static void IcEvEclipse(PixelCanvas c)
    {
        c.Ellipse(Ev(10, 10), 7, 7, new[] { P.Gold[2], P.Gold[3], P.White, P.White });
        c.Ellipse(Ev(11, 10), 6, 6, new[] { P.Coal[0], P.Coal[0], P.Coal[0], P.Coal[1] });
    }

    private static void IcEvRival(PixelCanvas c)
    {
        c.Line(Ev(3, 3), Ev(16, 16), P.Steel[3], 2);
        c.Line(Ev(16, 3), Ev(3, 16), P.Steel[3], 2);
        c.Line(Ev(2, 6), Ev(6, 2), P.Gold[2], 2); c.Line(Ev(13, 2), Ev(17, 6), P.Gold[2], 2);
    }

    private static void IcEvCart(PixelCanvas c)
    {
        c.Rect(3, 6, 15, 11, P.Wood[2]);
        c.Ellipse(Ev(6, 5), 2.5f, 2.5f, P.Wood); c.Ellipse(Ev(13, 5), 2.5f, 2.5f, P.Wood);
        c.Polygon(new[] { Ev(3, 11), Ev(15, 11), Ev(13, 16), Ev(5, 16) }, P.Linen[2]);
        c.Put(17, 16, P.Gold[3]); c.Put(18, 14, P.White);   // "?"
    }

    private static void IcEvScholars(PixelCanvas c)
    {
        c.Rect(3, 3, 16, 14, P.Linen[2]);
        c.Line(Ev(10, 3), Ev(10, 14), P.Linen[0]);
        for (int i = 0; i < 3; i++) { c.Line(Ev(4, 6 + i * 3), Ev(8, 6 + i * 3), P.Coal[2]); c.Line(Ev(12, 6 + i * 3), Ev(15, 6 + i * 3), P.Coal[2]); }
    }

    private static void IcEvFlare(PixelCanvas c)
    {
        c.Ellipse(Ev(7, 7), 6, 6, new[] { P.Fire[1], P.Gold[2], P.Gold[3], P.White });
        c.Line(Ev(11, 11), Ev(18, 18), P.Gold[3], 2);
        c.Line(Ev(14, 9), Ev(18, 12), P.Fire[2]);
    }

    private static void IcEvDerelict(PixelCanvas c)
    {
        c.Polygon(new[] { Ev(2, 8), Ev(14, 5), Ev(18, 10), Ev(14, 14), Ev(3, 12) }, P.Steel[1]);
        c.Line(Ev(6, 6), Ev(9, 13), P.Coal[0]);   // a crack
        c.Put(15, 10, P.Fire[2]);
    }

    private static void IcEvComet(PixelCanvas c)
    {
        for (int i = 0; i < 5; i++) c.Ellipse(Ev(5 + i * 2.5f, 15 - i * 2.5f), 2 + i * 0.4f, 2 + i * 0.4f, new[] { P.Water[1], P.Water[2], P.Water[3], P.Water[4] });
        c.Ellipse(Ev(15, 5), 3, 3, new[] { P.Water[3], P.Water[4], P.White, P.White });
    }
}
