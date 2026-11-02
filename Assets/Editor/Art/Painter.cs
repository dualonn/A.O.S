using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tiny anti-aliased 2D painter for generating placeholder art.
/// Coordinates are in "units" (scaled by 'scale' pixels per unit), origin bottom-left.
/// Shapes are drawn from signed distance functions, so edges are smooth at any size.
/// </summary>
public class Painter
{
    public readonly int Width;
    public readonly int Height;
    public readonly float Scale;

    /// <summary>Outline width in units, used by the Outlined* helpers.</summary>
    public float Outline = 0.035f;
    public Color Ink = new Color(0.08f, 0.07f, 0.07f);

    /// <summary>
    /// Where unit (0, 0) sits on the canvas, in units from its bottom-left corner. Lets a drawing
    /// spill past its nominal area (smoke, flags) onto a bigger canvas without changing its code.
    /// </summary>
    public Vector2 Origin;

    private readonly Color[] pixels;

    public Painter(float widthUnits, float heightUnits, float pixelsPerUnit)
    {
        Scale = pixelsPerUnit;
        Width = Mathf.RoundToInt(widthUnits * pixelsPerUnit);
        Height = Mathf.RoundToInt(heightUnits * pixelsPerUnit);
        pixels = new Color[Width * Height];
    }

    // ---------- Core ----------

    /// <summary>Paint 'color' wherever sdf(p) &lt; 0 (p in units). 'bounds' limits the work area (units).</summary>
    public void Draw(Func<Vector2, float> sdf, Rect bounds, Color color)
    {
        int x0 = Mathf.Max(0, Mathf.FloorToInt((bounds.xMin + Origin.x) * Scale) - 2);
        int y0 = Mathf.Max(0, Mathf.FloorToInt((bounds.yMin + Origin.y) * Scale) - 2);
        int x1 = Mathf.Min(Width - 1, Mathf.CeilToInt((bounds.xMax + Origin.x) * Scale) + 2);
        int y1 = Mathf.Min(Height - 1, Mathf.CeilToInt((bounds.yMax + Origin.y) * Scale) + 2);
        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                var p = new Vector2((x + 0.5f) / Scale - Origin.x, (y + 0.5f) / Scale - Origin.y);
                float distancePixels = sdf(p) * Scale;
                float cover = Mathf.Clamp01(0.5f - distancePixels);
                if (cover > 0)
                {
                    Blend(x, y, color, cover);
                }
            }
        }
    }

    private void Blend(int x, int y, Color c, float cover)
    {
        float a = c.a * cover;
        int i = y * Width + x;
        Color d = pixels[i];
        float outA = a + d.a * (1 - a);
        if (outA <= 0)
        {
            return;
        }
        Color o = (c * a + d * d.a * (1 - a)) / outA;
        o.a = outA;
        pixels[i] = o;
    }

    /// <summary>Erase everything below a height (units), e.g. to keep only a figure's upper body.</summary>
    public void ClearBelow(float yUnits)
    {
        int rows = Mathf.Clamp(Mathf.RoundToInt((yUnits + Origin.y) * Scale), 0, Height);
        for (int i = 0; i < rows * Width; i++)
        {
            pixels[i] = Color.clear;
        }
    }

    /// <summary>Bounds of everything painted (alpha above 'threshold'), in units. Null if empty.</summary>
    public Rect? PaintedBounds(float threshold = 0.25f)
    {
        int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                if (pixels[y * Width + x].a > threshold)
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
        }
        if (maxX < 0)
        {
            return null;
        }
        return Rect.MinMaxRect(minX / Scale - Origin.x, minY / Scale - Origin.y, (maxX + 1) / Scale - Origin.x, (maxY + 1) / Scale - Origin.y);
    }

    public Texture2D ToTexture()
    {
        var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    // ---------- Distance functions ----------

    public static float SdCircle(Vector2 p, Vector2 c, float r) => (p - c).magnitude - r;

    public static float SdEllipse(Vector2 p, Vector2 c, Vector2 r)
    {
        Vector2 q = new Vector2((p.x - c.x) / r.x, (p.y - c.y) / r.y);
        return (q.magnitude - 1f) * Mathf.Min(r.x, r.y);
    }

    public static float SdSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 pa = p - a, ba = b - a;
        float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Mathf.Max(1e-6f, Vector2.Dot(ba, ba)));
        return (pa - ba * h).magnitude;
    }

    public static float SdBox(Vector2 p, Vector2 center, Vector2 half, float radius)
    {
        Vector2 q = new Vector2(Mathf.Abs(p.x - center.x), Mathf.Abs(p.y - center.y)) - half + Vector2.one * radius;
        return Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0) - radius;
    }

    public static float SdPolygon(Vector2 p, IList<Vector2> v)
    {
        float d = Vector2.Dot(p - v[0], p - v[0]);
        float s = 1f;
        for (int i = 0, j = v.Count - 1; i < v.Count; j = i, i++)
        {
            Vector2 e = v[j] - v[i];
            Vector2 w = p - v[i];
            Vector2 b = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / Vector2.Dot(e, e));
            d = Mathf.Min(d, Vector2.Dot(b, b));
            bool c1 = p.y >= v[i].y, c2 = p.y < v[j].y, c3 = e.x * w.y > e.y * w.x;
            if ((c1 && c2 && c3) || (!c1 && !c2 && !c3))
            {
                s = -s;
            }
        }
        return s * Mathf.Sqrt(d);
    }

    private static Rect BoundsOf(IList<Vector2> pts, float pad)
    {
        float xMin = float.MaxValue, yMin = float.MaxValue, xMax = float.MinValue, yMax = float.MinValue;
        foreach (var p in pts)
        {
            xMin = Mathf.Min(xMin, p.x); yMin = Mathf.Min(yMin, p.y);
            xMax = Mathf.Max(xMax, p.x); yMax = Mathf.Max(yMax, p.y);
        }
        return Rect.MinMaxRect(xMin - pad, yMin - pad, xMax + pad, yMax + pad);
    }

    // ---------- Shapes ----------

    public void Circle(Vector2 c, float r, Color col) =>
        Draw(p => SdCircle(p, c, r), new Rect(c.x - r, c.y - r, r * 2, r * 2), col);

    public void Ellipse(Vector2 c, Vector2 r, Color col) =>
        Draw(p => SdEllipse(p, c, r), new Rect(c.x - r.x, c.y - r.y, r.x * 2, r.y * 2), col);

    public void Line(Vector2 a, Vector2 b, float width, Color col)
    {
        float h = width / 2;
        Draw(p => SdSegment(p, a, b) - h, BoundsOf(new[] { a, b }, h), col);
    }

    public void Polyline(IList<Vector2> pts, float width, Color col, bool closed = false)
    {
        for (int i = 0; i < pts.Count - 1; i++)
        {
            Line(pts[i], pts[i + 1], width, col);
        }
        if (closed && pts.Count > 2)
        {
            Line(pts[pts.Count - 1], pts[0], width, col);
        }
    }

    public void Polygon(IList<Vector2> pts, Color col) =>
        Draw(p => SdPolygon(p, pts), BoundsOf(pts, 0), col);

    public void Box(Vector2 center, Vector2 size, float radius, Color col)
    {
        Vector2 half = size / 2;
        Draw(p => SdBox(p, center, half, radius), new Rect(center - half, size), col);
    }

    /// <summary>Box given by its corners (x0,y0)-(x1,y1).</summary>
    public void BoxMinMax(float x0, float y0, float x1, float y1, float radius, Color col) =>
        Box(new Vector2((x0 + x1) / 2, (y0 + y1) / 2), new Vector2(x1 - x0, y1 - y0), radius, col);

    // ---------- Flat stickman style: filled shape with an ink outline ----------

    public void OutlinedPolygon(IList<Vector2> pts, Color fill)
    {
        Draw(p => SdPolygon(p, pts) - Outline, BoundsOf(pts, Outline), Ink);
        Polygon(pts, fill);
    }

    public void OutlinedCircle(Vector2 c, float r, Color fill)
    {
        Circle(c, r + Outline, Ink);
        Circle(c, r, fill);
    }

    public void OutlinedEllipse(Vector2 c, Vector2 r, Color fill)
    {
        Ellipse(c, r + Vector2.one * Outline, Ink);
        Ellipse(c, r, fill);
    }

    public void OutlinedBox(float x0, float y0, float x1, float y1, float radius, Color fill)
    {
        BoxMinMax(x0 - Outline, y0 - Outline, x1 + Outline, y1 + Outline, radius + Outline, Ink);
        BoxMinMax(x0, y0, x1, y1, radius, fill);
    }

    /// <summary>A thick stroke with an ink edge (sticks, poles, handles).</summary>
    public void OutlinedLine(Vector2 a, Vector2 b, float width, Color fill)
    {
        Line(a, b, width + Outline * 2, Ink);
        Line(a, b, width, fill);
    }

    public void InkLine(Vector2 a, Vector2 b, float widthScale = 1f) => Line(a, b, Outline * widthScale, Ink);

    public static Vector2 V(float x, float y) => new Vector2(x, y);
}
