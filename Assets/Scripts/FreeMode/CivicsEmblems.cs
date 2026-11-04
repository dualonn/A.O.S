using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pixel-art crests for the governments, drawn in code (32x32): a heater shield in the government's
/// colours with a metal rim and a charge (crown, sun, gear, stars...) with a drop shadow so it reads
/// at a glance. Drawn once each and kept.
/// </summary>
public static class CivicsEmblems
{
    private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => cache.Clear();

    public static Sprite For(CivGovernment g)
    {
        if (g == null) return null;
        if (cache.TryGetValue(g.id, out var s) && s != null) return s;
        var tex = Draw(g).ToTexture();
        tex.name = "Emblem_" + g.id;
        s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 32f);
        s.name = tex.name;
        cache[g.id] = s;
        return s;
    }

    private const int N = 32;

    private static bool InShield(float x, float y)
    {
        if (y > 28.5f || y < 2.5f) return false;
        float half = y >= 14f ? 11.5f : 11.5f * Mathf.Sqrt(Mathf.Clamp01((y - 2.5f) / 11.5f));
        return Mathf.Abs(x - 16f) <= half;
    }

    private static PixelCanvas Draw(CivGovernment g)
    {
        var c = new PixelCanvas(N, N);
        Color32 f1 = PixelCanvas.Hex(g.field), f2 = PixelCanvas.Hex(g.field2), m = PixelCanvas.Hex(g.metal);
        Color32 mDark = PixelCanvas.Mix(m, new Color32(30, 20, 10, 255), 0.45f);
        Color32 mLight = PixelCanvas.Mix(m, new Color32(255, 255, 255, 255), 0.45f);
        Color32 shade = PixelCanvas.Mix(f2, new Color32(0, 0, 0, 255), 0.45f);

        // Field: lit from the top left, darker towards the point, a metal rim.
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float px = x + 0.5f, py = y + 0.5f;
                if (!InShield(px, py)) continue;
                bool rim = !InShield(px - 1, py) || !InShield(px + 1, py) || !InShield(px, py + 1) || !InShield(px, py - 1);
                if (rim) { c.Put(x, y, (x + y) % 5 == 0 ? mLight : m); continue; }
                bool inner = !InShield(px - 2, py) || !InShield(px + 2, py) || !InShield(px, py + 2) || !InShield(px, py - 2);
                float t = Mathf.Clamp01((28f - py) / 26f * 0.8f + (px - 16f) / 40f);
                var col = PixelCanvas.Mix(f1, f2, t);
                if (inner) col = PixelCanvas.Mix(col, shade, 0.5f);
                // a faint diagonal sheen
                if (Mathf.Abs((px - 8f) - (28f - py) * 0.6f) < 1.2f && py > 16f) col = PixelCanvas.Mix(col, new Color32(255, 255, 255, 255), 0.12f);
                c.Put(x, y, col);
            }

        // Charge: a shadow one pixel down-right, then the metal over it, then highlights.
        Charge(c, g.charge, 1, -1, shade, shade, shade);
        Charge(c, g.charge, 0, 0, m, mDark, mLight);
        c.Outline(new Color32(10, 8, 12, 255), 0.75f);
        return c;
    }

    // ---------- Primitives (offset by ox, oy) ----------

    private static void Disc(PixelCanvas c, float cx, float cy, float r, Color32 col)
    {
        for (int y = Mathf.FloorToInt(cy - r - 1); y <= cy + r + 1; y++)
            for (int x = Mathf.FloorToInt(cx - r - 1); x <= cx + r + 1; x++)
            {
                float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                if (dx * dx + dy * dy <= r * r) c.Put(x, y, col);
            }
    }

    private static void Ring(PixelCanvas c, float cx, float cy, float r, float w, Color32 col, float sy = 1f)
    {
        for (int y = Mathf.FloorToInt(cy - r - 1); y <= cy + r + 1; y++)
            for (int x = Mathf.FloorToInt(cx - r - 1); x <= cx + r + 1; x++)
            {
                float dx = x + 0.5f - cx, dy = (y + 0.5f - cy) / sy;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d <= r && d >= r - w) c.Put(x, y, col);
            }
    }

    private static void Box(PixelCanvas c, int x0, int y0, int x1, int y1, Color32 col) => c.Rect(x0, y0, x1, y1, col);

    private static void Ln(PixelCanvas c, float x0, float y0, float x1, float y1, Color32 col, int w = 1) =>
        c.LineRaw(new Vector2(x0, y0), new Vector2(x1, y1), col, w);

    private static void Poly(PixelCanvas c, Color32 col, params float[] xy)
    {
        var pts = new Vector2[xy.Length / 2];
        for (int i = 0; i < pts.Length; i++) pts[i] = new Vector2(xy[i * 2], xy[i * 2 + 1]);
        c.Polygon(pts, col);
    }

    private static void Star(PixelCanvas c, float cx, float cy, float r, float ri, Color32 col, int points = 5)
    {
        var pts = new float[points * 4];
        for (int i = 0; i < points * 2; i++)
        {
            float a = Mathf.PI / 2f + i * Mathf.PI / points;
            float rr = i % 2 == 0 ? r : ri;
            pts[i * 2] = cx + Mathf.Cos(a) * rr;
            pts[i * 2 + 1] = cy + Mathf.Sin(a) * rr;
        }
        Poly(c, col, pts);
    }

    private static void Hex(PixelCanvas c, float cx, float cy, float r, Color32 col)
    {
        var pts = new float[12];
        for (int i = 0; i < 6; i++)
        {
            float a = i * Mathf.PI / 3f;
            pts[i * 2] = cx + Mathf.Cos(a) * r;
            pts[i * 2 + 1] = cy + Mathf.Sin(a) * r;
        }
        Poly(c, col, pts);
    }

    // ---------- Charges ----------

    private static void Charge(PixelCanvas c, string kind, int ox, int oy, Color32 m, Color32 d, Color32 l)
    {
        float X(float x) => x + ox;
        float Y(float y) => y + oy;
        int XI(int x) => x + ox;
        int YI(int y) => y + oy;
        bool shadow = ox != 0 || oy != 0;
        switch (kind)
        {
            case "fire":
                Poly(c, m, X(16), Y(26), X(20), Y(19), X(19.5f), Y(13), X(16), Y(11), X(12.5f), Y(13), X(12), Y(19), X(14), Y(21), X(15), Y(18));
                if (!shadow) Poly(c, l, X(16), Y(20), X(18), Y(15), X(16), Y(12.5f), X(14), Y(15));
                Ln(c, X(10), Y(10), X(22), Y(8), shadow ? m : d, 2);
                Ln(c, X(10), Y(8), X(22), Y(10), shadow ? m : d, 2);
                break;
            case "anchor":
                Ln(c, X(16), Y(9), X(16), Y(23), m, 2);
                Ring(c, X(16.5f), Y(24.5f), 2.5f, 1.2f, m);
                Box(c, XI(12), YI(20), XI(20), YI(21), m);
                Ln(c, X(10), Y(14), X(16), Y(9), m, 2);
                Ln(c, X(22), Y(14), X(16), Y(9), m, 2);
                Poly(c, m, X(9), Y(16), X(12), Y(14), X(9), Y(13));
                Poly(c, m, X(23), Y(16), X(20), Y(14), X(23), Y(13));
                break;
            case "circle":
                for (int i = 0; i < 7; i++)
                {
                    float a = i * Mathf.PI * 2f / 7f + 0.3f;
                    Disc(c, X(16 + Mathf.Cos(a) * 6.5f), Y(17 + Mathf.Sin(a) * 6.5f), 1.7f, m);
                }
                Disc(c, X(16), Y(17), 2f, shadow ? m : l);
                break;
            case "spear":
                Ln(c, X(10), Y(7), X(20), Y(21), m, 3);
                Poly(c, m, X(18), Y(19), X(23.5f), Y(26), X(21.5f), Y(18.5f));
                Poly(c, m, X(18), Y(19), X(23.5f), Y(26), X(19.5f), Y(21.5f));
                if (!shadow) { Ln(c, X(12), Y(10), X(13.5f), Y(12), d, 2); Ln(c, X(21), Y(21), X(22.5f), Y(24), l); }
                break;
            case "fist":
                Box(c, XI(11), YI(12), XI(20), YI(20), m);
                Box(c, XI(13), YI(7), XI(19), YI(11), m);
                Box(c, XI(9), YI(13), XI(11), YI(17), m);
                if (!shadow)
                {
                    for (int i = 0; i < 3; i++) Box(c, XI(13 + i * 2 + i / 2), YI(17), XI(13 + i * 2 + i / 2), YI(20), d);
                    Box(c, XI(11), YI(16), XI(16), YI(16), d);
                    Box(c, XI(12), YI(20), XI(19), YI(20), l);
                }
                break;
            case "sun":
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI / 4f;
                    Ln(c, X(16 + Mathf.Cos(a) * 5.5f), Y(17 + Mathf.Sin(a) * 5.5f), X(16 + Mathf.Cos(a) * 9f), Y(17 + Mathf.Sin(a) * 9f), m, 2);
                }
                Disc(c, X(16), Y(17), 4.5f, m);
                if (!shadow) Disc(c, X(15), Y(18), 2f, l);
                break;
            case "sunking":
                for (int i = 0; i < 12; i++)
                {
                    float a = i * Mathf.PI / 6f;
                    float r = i % 2 == 0 ? 9.5f : 7.5f;
                    Ln(c, X(16 + Mathf.Cos(a) * 5f), Y(17 + Mathf.Sin(a) * 5f), X(16 + Mathf.Cos(a) * r), Y(17 + Mathf.Sin(a) * r), m, 2);
                }
                Disc(c, X(16), Y(17), 5.2f, m);
                if (!shadow)
                {
                    Box(c, XI(13), YI(18), XI(14), YI(18), d); Box(c, XI(18), YI(18), XI(19), YI(18), d);
                    Box(c, XI(14), YI(14), XI(18), YI(14), d); Box(c, XI(16), YI(16), XI(16), YI(17), d);
                }
                break;
            case "crown":
                Box(c, XI(9), YI(10), XI(23), YI(14), m);
                Poly(c, m, X(9), Y(14), X(9), Y(23), X(13), Y(14));
                Poly(c, m, X(12), Y(14), X(16), Y(25), X(20), Y(14));
                Poly(c, m, X(23), Y(14), X(23), Y(23), X(19), Y(14));
                Disc(c, X(9.5f), Y(23.5f), 1.3f, m); Disc(c, X(16), Y(25.5f), 1.4f, m); Disc(c, X(22.5f), Y(23.5f), 1.3f, m);
                if (!shadow)
                {
                    Box(c, XI(9), YI(11), XI(23), YI(11), d);
                    Disc(c, X(16), Y(12.5f), 1.2f, PixelCanvas.Hex("c03030"));
                    Disc(c, X(12), Y(12.5f), 0.9f, PixelCanvas.Hex("3060c0")); Disc(c, X(20), Y(12.5f), 0.9f, PixelCanvas.Hex("3060c0"));
                }
                break;
            case "laurel":
                for (int side = -1; side <= 1; side += 2)
                    for (int i = 0; i < 6; i++)
                    {
                        float a = -Mathf.PI / 2f + 0.35f + i * 0.42f;   // from the bottom, curving up each side
                        float lx = 16 + side * Mathf.Cos(a) * 7.5f, ly = 17 + Mathf.Sin(a) * 7.5f;
                        Disc(c, X(lx), Y(ly), 1.6f, m);
                        Disc(c, X(lx + side * 1.6f), Y(ly + 1.2f), 1f, m);
                    }
                if (!shadow) Box(c, XI(15), YI(8), XI(16), YI(10), d);
                break;
            case "coin":
                Disc(c, X(16), Y(17), 7f, m);
                Ring(c, X(16), Y(17), 5.5f, 1f, shadow ? m : d);
                if (!shadow)
                {
                    Box(c, XI(15), YI(13), XI(16), YI(21), d);
                    Box(c, XI(13), YI(19), XI(18), YI(20), d); Box(c, XI(13), YI(16), XI(18), YI(17), d); Box(c, XI(13), YI(13), XI(18), YI(14), d);
                    Disc(c, X(13.5f), Y(20.5f), 1.2f, l);
                }
                break;
            case "tower":
                Box(c, XI(11), YI(8), XI(21), YI(21), m);
                Box(c, XI(10), YI(21), XI(22), YI(22), m);
                for (int i = 0; i < 4; i++) Box(c, XI(10 + i * 4), YI(23), XI(11 + i * 4), YI(25), m);
                if (!shadow)
                {
                    Box(c, XI(14), YI(8), XI(18), YI(13), d); Disc(c, X(16), Y(13.5f), 2f, d);
                    Box(c, XI(15), YI(17), XI(16), YI(19), d);
                    Box(c, XI(11), YI(9), XI(11), YI(20), l);
                }
                break;
            case "ship":
                Poly(c, m, X(8), Y(13), X(24), Y(13), X(21), Y(8.5f), X(11), Y(8.5f));
                Ln(c, X(16), Y(13), X(16), Y(26), m, 1);
                Poly(c, m, X(17), Y(25), X(17), Y(15), X(23), Y(15));
                Poly(c, m, X(15), Y(23), X(15), Y(15), X(10), Y(15));
                if (!shadow) { Box(c, XI(9), YI(12), XI(23), YI(12), l); for (int i = 0; i < 4; i++) Box(c, XI(11 + i * 3), YI(10), XI(11 + i * 3), YI(10), d); }
                break;
            case "eye":
            case "eye2":
                Poly(c, m, X(7), Y(17), X(11), Y(21), X(16), Y(22.5f), X(21), Y(21), X(25), Y(17), X(21), Y(13), X(16), Y(11.5f), X(11), Y(13));
                if (!shadow)
                {
                    bool machine = kind == "eye2";
                    Disc(c, X(16), Y(17), 3.8f, machine ? PixelCanvas.Hex("202020") : d);
                    Disc(c, X(16), Y(17), machine ? 2.2f : 1.6f, machine ? m : PixelCanvas.Hex("101018"));
                    if (machine) Ring(c, X(16), Y(17), 3.8f, 0.8f, l);
                    Box(c, XI(15), YI(18), XI(15), YI(18), new Color32(255, 255, 255, 255));
                    if (!machine) for (int i = 0; i < 3; i++) Ln(c, X(12 + i * 4), Y(23), X(11 + i * 5), Y(26), m);
                }
                break;
            case "book":
                Poly(c, m, X(8), Y(11), X(16), Y(9), X(16), Y(22), X(8), Y(24));
                Poly(c, m, X(24), Y(11), X(16), Y(9), X(16), Y(22), X(24), Y(24));
                if (!shadow)
                {
                    Ln(c, X(16), Y(9), X(16), Y(22), d);
                    for (int i = 0; i < 4; i++) { Ln(c, X(10), Y(14 + i * 2.4f), X(14), Y(13.4f + i * 2.4f), d); Ln(c, X(18), Y(13.4f + i * 2.4f), X(22), Y(14 + i * 2.4f), d); }
                    Box(c, XI(15), YI(22), XI(17), YI(26), PixelCanvas.Hex("c03030"));
                }
                break;
            case "star":
                Star(c, X(16), Y(17), 9f, 3.8f, m);
                if (!shadow) Star(c, X(15.5f), Y(17.5f), 4f, 1.7f, l);
                break;
            case "stars":
                Ring(c, X(16), Y(17), 10f, 1.2f, m);
                Star(c, X(16), Y(21.5f), 4.4f, 1.9f, m);
                Star(c, X(11.5f), Y(13.5f), 4f, 1.7f, m);
                Star(c, X(20.5f), Y(13.5f), 4f, 1.7f, m);
                break;
            case "gear":
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI / 4f;
                    Disc(c, X(16 + Mathf.Cos(a) * 7f), Y(17 + Mathf.Sin(a) * 7f), 1.8f, m);
                }
                Disc(c, X(16), Y(17), 6.2f, m);
                Disc(c, X(16), Y(17), 2.4f, shadow ? m : d);
                if (!shadow) Ring(c, X(16), Y(17), 6.2f, 0.8f, l);
                break;
            case "tower2":
                Box(c, XI(9), YI(8), XI(13), YI(19), m);
                Box(c, XI(14), YI(8), XI(18), YI(26), m);
                Box(c, XI(19), YI(8), XI(23), YI(22), m);
                Box(c, XI(16), YI(26), XI(16), YI(28), m);
                if (!shadow)
                    for (int y = 10; y < 25; y += 3)
                    {
                        if (y < 18) { Box(c, XI(10), YI(y), XI(10), YI(y), d); Box(c, XI(12), YI(y), XI(12), YI(y), d); }
                        Box(c, XI(15), YI(y), XI(15), YI(y), l); Box(c, XI(17), YI(y), XI(17), YI(y), l);
                        if (y < 21) { Box(c, XI(20), YI(y), XI(20), YI(y), d); Box(c, XI(22), YI(y), XI(22), YI(y), d); }
                    }
                break;
            case "chip":
                Box(c, XI(11), YI(12), XI(21), YI(22), m);
                for (int i = 0; i < 4; i++)
                {
                    int p = 13 + i * 2 + (i > 1 ? 1 : 0);
                    Box(c, XI(p), YI(9), XI(p), YI(11), m); Box(c, XI(p), YI(23), XI(p), YI(25), m);
                    Box(c, XI(8), YI(p), XI(10), YI(p), m); Box(c, XI(22), YI(p), XI(24), YI(p), m);
                }
                if (!shadow) { Box(c, XI(14), YI(15), XI(18), YI(19), d); Box(c, XI(15), YI(16), XI(17), YI(18), l); }
                break;
            case "orb":
                Poly(c, m, X(12), Y(8), X(20), Y(8), X(17.5f), Y(12), X(14.5f), Y(12));
                Disc(c, X(16), Y(18), 6f, m);
                if (!shadow)
                {
                    Ring(c, X(16), Y(18), 6f, 1f, d);
                    Disc(c, X(14), Y(20), 1.8f, l);
                    Box(c, XI(23), YI(24), XI(23), YI(26), l); Box(c, XI(22), YI(25), XI(24), YI(25), l);
                    Box(c, XI(9), YI(23), XI(9), YI(23), l);
                }
                break;
            case "hex":
                Hex(c, X(16), Y(17), 8.5f, m);
                if (!shadow)
                {
                    Hex(c, X(16), Y(17), 6.5f, d);
                    Hex(c, X(16), Y(17), 3f, m);
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * Mathf.PI / 3f + Mathf.PI / 6f;
                        Disc(c, X(16 + Mathf.Cos(a) * 5f), Y(17 + Mathf.Sin(a) * 5f), 0.9f, l);
                    }
                }
                break;
            case "ring":
                Ring(c, X(16), Y(17), 10f, 1.4f, m, 0.35f);
                Disc(c, X(16), Y(17), 5f, m);
                if (!shadow)
                {
                    Disc(c, X(14.5f), Y(18.5f), 2f, l);
                    Box(c, XI(11), YI(16), XI(21), YI(16), d);
                    Star(c, X(22.5f), Y(24), 2f, 0.8f, l, 4);
                    Star(c, X(9.5f), Y(10), 1.6f, 0.7f, l, 4);
                }
                break;
            case "ascend":
                for (int i = 0; i < 3; i++)
                {
                    float y = 8 + i * 4.5f;
                    Ln(c, X(10), Y(y), X(16), Y(y + 5), m, 2);
                    Ln(c, X(22), Y(y), X(16), Y(y + 5), m, 2);
                }
                Ring(c, X(16), Y(25.5f), 3.2f, 1.1f, m, 0.55f);
                break;
            default:
                Star(c, X(16), Y(17), 8f, 3.5f, m);
                break;
        }
    }
}
