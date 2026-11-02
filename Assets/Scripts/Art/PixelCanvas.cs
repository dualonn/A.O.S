using System;
using UnityEngine;

/// <summary>
/// A small pixel-art canvas (y up, (0,0) = bottom-left pixel) with crisp lines, ramp-shaded
/// shapes and a selective outline. Used by the editor to bake art and by the game to draw heroes
/// in whatever gear they wear.
/// </summary>
public class PixelCanvas
{
    public readonly int W, H;
    private readonly Color32[] px;

    /// <summary>Optional placement for drawing in a local frame (rotate round a pivot, then shift).</summary>
    public Vector2 Pivot, Shift;
    public float Angle; // degrees, counter-clockwise

    public PixelCanvas(int w, int h)
    {
        W = w;
        H = h;
        px = new Color32[w * h];
    }

    // ---------- Pixels ----------

    public bool Inside(int x, int y) => x >= 0 && y >= 0 && x < W && y < H;

    public Color32 Get(int x, int y) => Inside(x, y) ? px[y * W + x] : default;

    public void Put(int x, int y, Color32 c)
    {
        if (!Inside(x, y) || c.a == 0) return;
        int i = y * W + x;
        if (c.a == 255)
        {
            px[i] = c;
            return;
        }
        var d = px[i];
        float t = c.a / 255f;
        px[i] = new Color32(
            (byte)(d.r + (c.r - d.r) * t), (byte)(d.g + (c.g - d.g) * t), (byte)(d.b + (c.b - d.b) * t),
            (byte)Mathf.Max(d.a, c.a));
    }

    public void Put(float x, float y, Color32 c) => Put(Mathf.FloorToInt(x), Mathf.FloorToInt(y), c);

    public void Put(Vector2 p, Color32 c) => Put(p.x, p.y, c);

    public void Clear(int x, int y)
    {
        if (Inside(x, y)) px[y * W + x] = default;
    }

    public void Rect(int x0, int y0, int x1, int y1, Color32 c)
    {
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                Put(x, y, c);
    }

    // ---------- Local frame (for leaning and falling figures) ----------

    /// <summary>Local point -> canvas point, through the current pivot, angle and shift.</summary>
    public Vector2 T(Vector2 p)
    {
        if (Angle == 0f) return p + Shift;
        float r = Angle * Mathf.Deg2Rad, cos = Mathf.Cos(r), sin = Mathf.Sin(r);
        Vector2 d = p - Pivot;
        return Pivot + new Vector2(d.x * cos - d.y * sin, d.x * sin + d.y * cos) + Shift;
    }

    /// <summary>Canvas point -> local point.</summary>
    public Vector2 Inv(Vector2 q)
    {
        q -= Shift;
        if (Angle == 0f) return q;
        float r = -Angle * Mathf.Deg2Rad, cos = Mathf.Cos(r), sin = Mathf.Sin(r);
        Vector2 d = q - Pivot;
        return Pivot + new Vector2(d.x * cos - d.y * sin, d.x * sin + d.y * cos);
    }

    /// <summary>Paint every canvas pixel whose centre maps (in local space) into 'bounds', coloured by 'shader' (null = skip).</summary>
    public void FillLocal(Rect bounds, Func<Vector2, Color32?> shader)
    {
        Vector2 a = T(bounds.min), b = T(new Vector2(bounds.xMax, bounds.yMin)), c = T(bounds.max), d = T(new Vector2(bounds.xMin, bounds.yMax));
        int x0 = Mathf.FloorToInt(Mathf.Min(Mathf.Min(a.x, b.x), Mathf.Min(c.x, d.x))) - 1;
        int x1 = Mathf.CeilToInt(Mathf.Max(Mathf.Max(a.x, b.x), Mathf.Max(c.x, d.x))) + 1;
        int y0 = Mathf.FloorToInt(Mathf.Min(Mathf.Min(a.y, b.y), Mathf.Min(c.y, d.y))) - 1;
        int y1 = Mathf.CeilToInt(Mathf.Max(Mathf.Max(a.y, b.y), Mathf.Max(c.y, d.y))) + 1;
        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                if (!Inside(x, y)) continue;
                Vector2 l = Inv(new Vector2(x + 0.5f, y + 0.5f));
                if (!bounds.Contains(l)) continue;
                var col = shader(l);
                if (col.HasValue) Put(x, y, col.Value);
            }
        }
    }

    /// <summary>A crisp pixel line between two local points; 'w' thickens it.</summary>
    public void Line(Vector2 a, Vector2 b, Color32 c, int w = 1)
    {
        a = T(a);
        b = T(b);
        LineRaw(a, b, c, w);
    }

    public void LineRaw(Vector2 a, Vector2 b, Color32 c, int w = 1)
    {
        float dx = b.x - a.x, dy = b.y - a.y;
        int n = Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy))));
        bool steep = Mathf.Abs(dy) > Mathf.Abs(dx);
        for (int i = 0; i <= n; i++)
        {
            float t = i / (float)n;
            int x = Mathf.FloorToInt(a.x + dx * t), y = Mathf.FloorToInt(a.y + dy * t);
            Put(x, y, c);
            if (w >= 2) { if (steep) Put(x + 1, y, c); else Put(x, y + 1, c); }
            if (w >= 3) { if (steep) Put(x - 1, y, c); else Put(x, y - 1, c); }
            if (w >= 4) { if (steep) Put(x + 2, y, c); else Put(x, y + 2, c); }
        }
    }

    // ---------- Shaded shapes (local space) ----------

    /// <summary>Light comes from the top-left of the screen.</summary>
    public static readonly Vector2 Light = new Vector2(-0.55f, 0.83f);

    /// <summary>A ramp-shaded ellipse. 'only' (local point) can clip it, e.g. to the top half of a head.</summary>
    public void Ellipse(Vector2 c, float rx, float ry, Color32[] ramp, Func<Vector2, bool> only = null, float bias = 0f)
    {
        FillLocal(new Rect(c.x - rx - 1, c.y - ry - 1, rx * 2 + 2, ry * 2 + 2), l =>
        {
            float dx = (l.x - c.x) / rx, dy = (l.y - c.y) / ry;
            float d = dx * dx + dy * dy;
            if (d > 1f || (only != null && !only(l))) return null;
            return Shade(ramp, dx * Light.x + dy * Light.y + bias, d);
        });
    }

    /// <summary>A thick, shaded segment with round ends (limbs, torsos, shafts).</summary>
    public void Capsule(Vector2 a, Vector2 b, float r, Color32[] ramp, float bias = 0f)
    {
        var box = new Rect(Mathf.Min(a.x, b.x) - r - 1, Mathf.Min(a.y, b.y) - r - 1, Mathf.Abs(a.x - b.x) + r * 2 + 2, Mathf.Abs(a.y - b.y) + r * 2 + 2);
        Vector2 ab = b - a;
        float len2 = Mathf.Max(0.0001f, ab.sqrMagnitude);
        FillLocal(box, l =>
        {
            float t = Mathf.Clamp01(Vector2.Dot(l - a, ab) / len2);
            Vector2 off = l - (a + ab * t);
            float d = off.sqrMagnitude / (r * r);
            if (d > 1f) return null;
            Vector2 n = off / r;
            return Shade(ramp, n.x * Light.x + n.y * Light.y + bias, d);
        });
    }

    /// <summary>A filled polygon (local points), flat colour or shaded across its width from the left.</summary>
    public void Polygon(Vector2[] pts, Color32 c) => Polygon(pts, _ => c);

    public void Polygon(Vector2[] pts, Func<Vector2, Color32?> shader)
    {
        float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
        foreach (var p in pts) { x0 = Mathf.Min(x0, p.x); y0 = Mathf.Min(y0, p.y); x1 = Mathf.Max(x1, p.x); y1 = Mathf.Max(y1, p.y); }
        FillLocal(new Rect(x0, y0, x1 - x0, y1 - y0), l => InPolygon(l, pts) ? shader(l) : null);
    }

    public static bool InPolygon(Vector2 p, Vector2[] v)
    {
        bool inside = false;
        for (int i = 0, j = v.Length - 1; i < v.Length; j = i++)
        {
            if ((v[i].y > p.y) != (v[j].y > p.y) && p.x < (v[j].x - v[i].x) * (p.y - v[i].y) / (v[j].y - v[i].y) + v[i].x)
                inside = !inside;
        }
        return inside;
    }

    /// <summary>Pick a ramp colour from a lighting value (-1..1) and the distance from the centre (0..1).</summary>
    public static Color32 Shade(Color32[] ramp, float v, float d)
    {
        if (ramp.Length >= 5)
        {
            if (v > 0.6f && d < 0.7f) return ramp[4];
            if (v > 0.15f) return ramp[3];
            if (v > -0.35f) return ramp[2];
            if (v > -0.8f) return ramp[1];
            return ramp[0];
        }
        if (v > 0.55f && d < 0.75f) return ramp[3];
        if (v > -0.2f) return ramp[2];
        if (v > -0.75f || d < 0.8f) return ramp[1];
        return ramp[0];
    }

    // ---------- Finishing ----------

    /// <summary>
    /// Selective outline: every empty pixel beside the art becomes a darkened copy of its darkest
    /// neighbour (softer and richer than plain black).
    /// </summary>
    public void Outline(Color32 ink, float strength = 0.6f)
    {
        var src = (Color32[])px.Clone();
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                if (src[y * W + x].a > 0) continue;
                bool found = false;
                Color32 best = default;
                float bestLum = float.MaxValue;
                Check(x + 1, y); Check(x - 1, y); Check(x, y + 1); Check(x, y - 1);
                if (found) px[y * W + x] = Mix(best, ink, strength);

                void Check(int nx, int ny)
                {
                    if (nx < 0 || ny < 0 || nx >= W || ny >= H) return;
                    var c = src[ny * W + nx];
                    if (c.a < 200) return;
                    float lum = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
                    if (lum < bestLum) { bestLum = lum; best = c; found = true; }
                }
            }
        }
    }

    /// <summary>A soft oval ground shadow, only where nothing is drawn yet.</summary>
    public void Shadow(float cx, float cy, float rx, float ry, byte alpha = 80)
    {
        for (int y = Mathf.FloorToInt(cy - ry) - 1; y <= cy + ry + 1; y++)
            for (int x = Mathf.FloorToInt(cx - rx) - 1; x <= cx + rx + 1; x++)
            {
                float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                if (dx * dx + dy * dy <= 1f && Get(x, y).a == 0) Put(x, y, new Color32(20, 14, 24, alpha));
            }
    }

    /// <summary>Erase every row below 'y' (tower crews standing behind a parapet).</summary>
    public void ClearBelow(int y)
    {
        for (int yy = 0; yy < Mathf.Min(y, H); yy++)
            for (int x = 0; x < W; x++)
                px[yy * W + x] = default;
    }

    public void FlipX()
    {
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W / 2; x++)
            {
                int a = y * W + x, b = y * W + (W - 1 - x);
                (px[a], px[b]) = (px[b], px[a]);
            }
    }

    public void Blit(PixelCanvas other, int ox, int oy)
    {
        for (int y = 0; y < other.H; y++)
            for (int x = 0; x < other.W; x++)
                Put(ox + x, oy + y, other.Get(x, y));
    }

    public Color32[] Pixels => px;

    public Texture2D ToTexture()
    {
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    public static Color32 Mix(Color32 a, Color32 b, float t) => new Color32(
        (byte)(a.r + (b.r - a.r) * t), (byte)(a.g + (b.g - a.g) * t), (byte)(a.b + (b.b - a.b) * t), (byte)(a.a + (b.a - a.a) * t));

    public static Color32 Hex(string h, byte a = 255)
    {
        ColorUtility.TryParseHtmlString("#" + h.TrimStart('#'), out var c);
        var c32 = (Color32)c;
        c32.a = a;
        return c32;
    }
}
