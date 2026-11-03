using System;
using UnityEngine;
using P = PixelPalette;

/// <summary>
/// Pieces for drawing buildings in pixel art: walls in each material, roofs, doors, windows,
/// chimneys, banners, fires and yard props. Coordinates are pixels from the footprint's
/// bottom-left corner (32 a tile), y up. Draw back to front: things further back (higher up the
/// footprint) first.
/// </summary>
public class BuildingKit
{
    public enum Mat { Logs, Planks, Plaster, Mud, Stone, Marble, Hide, Wattle }
    public enum RoofType { Thatch, Tile, Shingle, Slate, Hide }

    public readonly PixelCanvas C;
    public readonly int Ox, Oy;
    public readonly int Frame, Frames;
    public readonly int Level;
    public readonly Color32[] Team = P.Blue;
    private readonly System.Random rng;

    public BuildingKit(PixelCanvas canvas, int ox, int oy, int level, int frame, int frames, int seed)
    {
        C = canvas;
        Ox = ox;
        Oy = oy;
        Level = level;
        Frame = frame;
        Frames = Mathf.Max(1, frames);
        C.Shift = new Vector2(ox, oy);
        rng = new System.Random(seed);
    }

    /// <summary>0..1 through the animation loop.</summary>
    public float T => Frame / (float)Frames;

    public float Wave(float phase = 0) => Mathf.Sin((T + phase) * Mathf.PI * 2f);

    public int R(int min, int max) => rng.Next(min, max);

    // ---------- Pixels ----------

    public void Px(int x, int y, Color32 c) => C.Put(x + Ox, y + Oy, c);

    public Color32 Get(int x, int y) => C.Get(x + Ox, y + Oy);

    public void Erase(int x, int y) => C.Clear(x + Ox, y + Oy);

    public void Rect(int x0, int y0, int x1, int y1, Color32 c)
    {
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                Px(x, y, c);
    }

    public void HLine(int x0, int x1, int y, Color32 c) { for (int x = x0; x <= x1; x++) Px(x, y, c); }

    public void VLine(int x, int y0, int y1, Color32 c) { for (int y = y0; y <= y1; y++) Px(x, y, c); }

    public void Line(float x0, float y0, float x1, float y1, Color32 c, int w = 1) => C.Line(new Vector2(x0, y0), new Vector2(x1, y1), c, w);

    public void Polygon(Vector2[] pts, Func<Vector2, Color32?> shader) => C.Polygon(pts, shader);

    public void Ellipse(float cx, float cy, float rx, float ry, Color32[] ramp, Func<Vector2, bool> only = null) => C.Ellipse(new Vector2(cx, cy), rx, ry, ramp, only);

    /// <summary>Darken a pixel already drawn (for shading the shadow side).</summary>
    public void Darken(int x, int y, float amount)
    {
        var c = Get(x, y);
        if (c.a > 0) Px(x, y, PixelCanvas.Mix(c, P.Ink, amount));
    }

    // ---------- Ground ----------

    /// <summary>A patch of trampled earth under a yard: a rounded box with a ragged, natural edge.</summary>
    public void Ground(int x0, int y0, int x1, int y1, Color32[] ramp = null)
    {
        ramp ??= P.Dirt;
        float cx = (x0 + x1) / 2f, cy = (y0 + y1) / 2f, rx = (x1 - x0) / 2f + 0.5f, ry = (y1 - y0) / 2f + 0.5f;
        for (int y = y0 - 2; y <= y1 + 2; y++)
            for (int x = x0 - 2; x <= x1 + 2; x++)
            {
                float dx = Mathf.Abs(x + 0.5f - cx) / rx, dy = Mathf.Abs(y + 0.5f - cy) / ry;
                float d = Mathf.Pow(Mathf.Pow(dx, 4) + Mathf.Pow(dy, 4), 0.25f);
                d += (Mathf.Sin(x * 0.9f + y * 0.3f) + Mathf.Sin(y * 1.3f - x * 0.4f)) * 0.03f;
                if (d > 1f) continue;
                int n = (x * 7 + y * 13) % 17;
                Color32 col = d > 0.93f ? ramp[1] : n == 0 ? ramp[3] : n == 9 ? ramp[1] : ramp[2];
                if (d > 0.86f && d <= 0.93f && (x + y) % 3 == 0) col = P.Grass[1];   // grass creeping in at the edge
                Px(x, y, col);
            }
    }

    // ---------- Walls ----------

    public void Wall(int x0, int y0, int x1, int y1, Mat mat)
    {
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                Px(x, y, WallPixel(mat, x - x0, y - y0, x1 - x0, y1 - y0));
        if (mat == Mat.Plaster)
        {
            var w = P.Wood;
            for (int y = y0; y <= y1; y++) { Px(x0, y, w[2]); Px(x0 + 1, y, w[2]); Px(x1 - 1, y, w[1]); Px(x1, y, w[1]); }
            HLine(x0, x1, y0, w[1]); HLine(x0, x1, y0 + 1, w[1]); HLine(x0, x1, y1, w[0]);
            if (y1 - y0 > 16) HLine(x0, x1, y0 + (y1 - y0) / 2, w[1]);
        }
        if (mat == Mat.Logs)
            for (int y = y0; y <= y1; y += 3) { Px(x0, y + 1, P.Wood[3]); Px(x1, y + 1, P.Wood[2]); }
        // shadow side
        for (int y = y0; y <= y1; y++) { Darken(x1, y, 0.25f); Darken(x1 - 1, y, 0.12f); }
    }

    private Color32 WallPixel(Mat mat, int x, int y, int w, int h)
    {
        bool left = x == 0;
        switch (mat)
        {
            case Mat.Logs:
            {
                int r = y % 3;
                if ((x * 5 + y) % 23 == 0) return P.Wood[1];
                return r == 2 ? P.Wood[3] : r == 1 ? P.Wood[2] : P.Wood[1];
            }
            case Mat.Planks:
            {
                int b = x / 3;
                if (x % 3 == 2) return P.Wood[1];
                return b % 2 == 0 ? P.Wood[2] : P.Wood[3];
            }
            case Mat.Plaster:
                return ((x * 11 + y * 7) % 17 == 0) ? P.Plaster[3] : left ? P.Plaster[3] : P.Plaster[2];
            case Mat.Mud:
            {
                bool seamH = y % 4 == 3 && (x + y) % 5 != 0;
                bool seamV = (x + (y / 4 % 2) * 3) % 6 == 0 && y % 4 != 3;
                if (y == h) return P.Mud[3];
                if (seamH || seamV) return P.Mud[1];
                return left ? P.Mud[3] : ((x * 3 + y * 5) % 13 == 0 ? P.Mud[1] : P.Mud[2]);
            }
            case Mat.Stone:
            {
                int row = y / 4, ry = y % 4;
                int off = (row * 37) % 8;
                int bw = 6 + (row % 3);
                int bx = (x + off) % bw, block = (x + off) / bw;
                if (ry == 0 || bx == 0) return P.Stone[1];
                int tone = (block * 7 + row * 3) % 5;
                Color32 face = tone == 0 ? P.Stone[3] : tone == 1 ? PixelCanvas.Mix(P.Stone[1], P.Stone[2], 0.5f) : P.Stone[2];
                return ry == 3 && tone != 1 ? PixelCanvas.Mix(face, P.Stone[4], 0.35f) : face;
            }
            case Mat.Marble:
            {
                int px = x % 12;
                if (y <= 1) return P.Marble[1];
                if (y >= h - 1) return P.Marble[3];
                if (px == 0 || px == 1) return P.Marble[3];
                if (px == 2) return P.Marble[1];
                return P.Marble[2];
            }
            case Mat.Hide:
                if (x % 6 == 3 && y % 2 == 0) return P.Leather[1];
                return ((x / 6 + y / 7) % 3 == 0) ? P.Leather[3] : P.Leather[2];
            case Mat.Wattle:
                return ((x + (y / 2) * 2) % 4 < 2) ? P.Wood[2] : P.Wood[1];
        }
        return P.Stone[2];
    }

    // ---------- Roofs ----------

    private static Color32[] RoofRamp(RoofType r) => r switch
    {
        RoofType.Tile => P.Tile,
        RoofType.Shingle => P.Wood,
        RoofType.Slate => new[] { P.Hex("23262e"), P.Hex("3a3f4a"), P.Hex("565d6b"), P.Hex("7a8394") },
        RoofType.Hide => P.Leather,
        _ => P.Thatch,
    };

    /// <summary>A hipped roof seen from the front: eaves at 'yb' (x0..x1), ridge 'h' rows up, sloping in by 'inset' each side.</summary>
    public void Roof(int x0, int x1, int yb, int h, RoofType type, int inset = 12)
    {
        var r = RoofRamp(type);
        for (int y = yb; y <= yb + h; y++)
        {
            float t = (y - yb) / (float)h;
            int left = Mathf.RoundToInt(x0 + inset * t), right = Mathf.RoundToInt(x1 - inset * t);
            for (int x = left; x <= right; x++)
            {
                Color32 col;
                int row = (y - yb) / 3, ry = (y - yb) % 3;
                switch (type)
                {
                    case RoofType.Tile:
                        col = ry == 0 ? r[1] : ((x + row * 2) % 4 == 0 ? r[1] : ry == 2 ? r[3] : r[2]);
                        break;
                    case RoofType.Shingle:
                    case RoofType.Slate:
                        col = ry == 0 || (x + row * 3) % 6 == 0 ? r[1] : r[2];
                        break;
                    case RoofType.Hide:
                        col = (x - left) % 7 == 0 ? r[1] : r[2];
                        break;
                    default:
                        int stroke = (x * 7 + (y / 3) * 13) % 11;
                        col = stroke > 2 ? r[2] : r[1];
                        if (stroke == 7 && y % 3 == 0) col = r[3];
                        break;
                }
                if (x < left + 2) col = r[3];
                else if (x > right - 3) col = r[1];
                Px(x, y, col);
            }
        }
        for (int x = x0; x <= x1; x++) { Px(x, yb, x % 3 == 0 ? r[1] : r[0]); Px(x, yb + 1, r[1]); }   // eaves
        int rl = x0 + inset, rr = x1 - inset;
        for (int x = rl; x <= rr; x++) { Px(x, yb + h, r[1]); Px(x, yb + h + 1, x % 2 == 0 ? r[0] : r[1]); }   // ridge
    }

    /// <summary>A flat roof seen from above the front parapet (mud-brick and Roman houses).</summary>
    public void FlatRoof(int x0, int x1, int yb, int depth, Color32[] ramp)
    {
        for (int y = yb; y < yb + depth; y++)
            for (int x = x0; x <= x1; x++)
                Px(x, y, (x * 5 + y * 3) % 19 == 0 ? ramp[2] : ramp[3]);
        HLine(x0, x1, yb + depth, ramp[2]);
        HLine(x0, x1, yb, ramp[1]);
        HLine(x0, x1, yb + 1, ramp[2]);
        for (int y = yb; y <= yb + depth; y++) { Px(x0, y, ramp[2]); Px(x1, y, ramp[1]); }
    }

    /// <summary>A cone (hut or tent roof): base half-width 'rx' at 'yb', tip 'h' up.</summary>
    public void Cone(float cx, int yb, float rx, int h, RoofType type)
    {
        var r = RoofRamp(type);
        for (int y = yb; y <= yb + h; y++)
        {
            float t = (y - yb) / (float)h;
            float half = rx * (1 - t);
            for (int x = Mathf.FloorToInt(cx - half); x <= Mathf.CeilToInt(cx + half); x++)
            {
                float s = (x + 0.5f - cx) / Mathf.Max(1f, half);
                if (Mathf.Abs(s) > 1.05f) continue;
                Color32 col = s < -0.5f ? r[3] : s > 0.45f ? r[1] : r[2];
                if (type == RoofType.Thatch && (x * 7 + y * 3) % 9 == 0) col = r[1];
                if (y == yb && x % 2 == 0) col = r[0];
                Px(x, y, col);
            }
        }
    }

    /// <summary>An A-frame tent seen from the front, its door flap open.</summary>
    public void Tent(int cx, int yb, int half, int h, Color32[] ramp)
    {
        for (int y = yb; y <= yb + h; y++)
        {
            float t = (y - yb) / (float)h;
            int w = Mathf.RoundToInt(half * (1 - t));
            for (int x = cx - w; x <= cx + w; x++)
            {
                bool door = Mathf.Abs(x - cx) <= Mathf.Max(0, w / 2 - 1) && t < 0.55f;
                Color32 col = door ? P.Coal[1] : x < cx - w / 2 ? ramp[3] : x > cx + w / 2 ? ramp[1] : ramp[2];
                Px(x, y, col);
            }
        }
        Line(cx, yb + h, cx, yb + h + 3, P.Wood[1]);   // pole tip
    }

    // ---------- Openings ----------

    public void Door(int x, int y, int w, int h, bool arched = false, bool open = false)
    {
        for (int yy = y; yy < y + h; yy++)
            for (int xx = x; xx < x + w; xx++)
            {
                bool top = arched && yy >= y + h - 2 && (xx == x || xx == x + w - 1 || (yy == y + h - 1 && (xx == x + 1 || xx == x + w - 2)));
                if (top) continue;
                Color32 col = open ? P.Coal[1] : ((xx - x) % 3 == 0 ? P.Wood[1] : xx > x + w - 2 ? P.Wood[1] : P.Wood[2]);
                Px(xx, yy, col);
            }
        if (!open) Px(x + w - 2, y + h / 2 - 1, P.Gold[3]);
        HLine(x - 1, x + w, y - 1, P.Stone[3]);   // step
    }

    public void Window(int x, int y, int w = 5, int h = 5, bool lit = true)
    {
        for (int yy = y; yy < y + h; yy++)
            for (int xx = x; xx < x + w; xx++)
            {
                bool frame = xx == x || xx == x + w - 1 || yy == y || yy == y + h - 1;
                bool mull = xx == x + w / 2 || yy == y + h / 2;
                Px(xx, yy, frame ? P.Wood[1] : mull ? P.Wood[2] : lit ? ((xx - x) < 2 && yy > y + h / 2 ? P.Glow[3] : P.Glow[2]) : P.Coal[2]);
            }
        HLine(x - 1, x + w, y - 1, P.Wood[2]);
    }

    /// <summary>A flower box under a window.</summary>
    public void FlowerBox(int x, int y, int w)
    {
        HLine(x, x + w - 1, y, P.Wood[1]);
        for (int i = 0; i < w; i++) Px(x + i, y + 1, i % 3 == 1 ? P.Flower[i % 2] : P.Leaf[3]);
    }

    // ---------- Chimneys, smoke, fire, banners ----------

    public void Chimney(int x, int y, int w, int h, Color32[] ramp = null)
    {
        ramp ??= P.Stone;
        for (int yy = y; yy < y + h; yy++)
            for (int xx = x; xx < x + w; xx++)
                Px(xx, yy, (yy % 3 == 0 || (xx + yy / 3) % 4 == 0) ? ramp[1] : xx == x ? ramp[3] : ramp[2]);
        HLine(x - 1, x + w, y + h, ramp[3]);
        HLine(x - 1, x + w, y + h - 1, ramp[1]);
    }

    /// <summary>Soft smoke puffs rising from (x, y): drawn after the outline.</summary>
    public void Smoke(int x, int y, float scale = 1f, byte alpha = 170)
    {
        for (int i = 0; i < 3; i++)
        {
            float k = (T + i / 3f) % 1f;
            float px = x + Mathf.Sin(k * 5f + i) * 2f * scale + k * 4 * scale, py = y + k * 18 * scale;
            float r = (1.5f + k * 2.5f) * scale;
            byte a = (byte)(alpha * (1 - k));
            for (int yy = Mathf.FloorToInt(py - r); yy <= py + r; yy++)
                for (int xx = Mathf.FloorToInt(px - r); xx <= px + r; xx++)
                    if ((xx + 0.5f - px) * (xx + 0.5f - px) + (yy + 0.5f - py) * (yy + 0.5f - py) <= r * r)
                        Px(xx, yy, new Color32(232, 228, 226, a));
        }
    }

    /// <summary>A flickering fire, 'w' wide, standing on (x, y).</summary>
    public void Fire(int x, int y, int w, int maxH = 8)
    {
        var f = P.Fire;
        for (int i = 0; i < w; i++)
        {
            float centre = 1f - Mathf.Abs(i - (w - 1) / 2f) / (w / 2f + 0.5f);
            int h = Mathf.Max(1, Mathf.RoundToInt(maxH * centre * (0.7f + 0.3f * Mathf.Sin(T * 6.28f * 2 + i * 1.7f))));
            for (int j = 0; j < h; j++)
            {
                float k = j / (float)h;
                Px(x + i, y + j, k < 0.3f ? f[3] : k < 0.6f ? f[2] : k < 0.85f ? f[1] : f[0]);
            }
        }
        if (Frame % 2 == 0) Px(x + w / 2 + 1, y + maxH + 1, f[3]);   // a spark
    }

    /// <summary>A banner on a pole, waving: top of pole at (x, y + h).</summary>
    public void Banner(int x, int y, int h, Color32[] ramp = null, bool emblem = true)
    {
        ramp ??= Team;
        VLine(x, y, y + h, P.Wood[2]);
        VLine(x + 1, y, y + h, P.Wood[1]);
        Px(x, y + h + 1, P.Gold[3]); Px(x + 1, y + h + 1, P.Gold[2]);
        for (int i = 0; i < 9; i++)
        {
            int dy = Mathf.RoundToInt(Mathf.Sin(T * 6.28f + i * 0.7f) * 1.2f);
            for (int j = 0; j < 7; j++)
            {
                if (i == 8 && j == 3) continue;   // swallow tail
                Color32 col = j == 6 ? ramp[3] : j == 0 ? ramp[1] : ramp[2];
                Px(x + 2 + i, y + h - j + dy, col);
            }
            if (emblem && (i == 3 || i == 4)) Px(x + 2 + i, y + h - 3 + dy, P.Gold[2]);
        }
    }

    public void Torch(int x, int y)
    {
        VLine(x, y, y + 5, P.Wood[1]);
        Fire(x - 1, y + 6, 3, 3);
    }

    // ---------- Props ----------

    public void Crate(int x, int y)
    {
        Rect(x, y, x + 5, y + 4, P.Wood[2]);
        HLine(x, x + 5, y + 4, P.Wood[3]);
        Line(x, y, x + 5, y + 4, P.Wood[1]);
        VLine(x + 5, y, y + 4, P.Wood[1]);
    }

    public void Barrel(int x, int y)
    {
        for (int yy = 0; yy < 6; yy++)
            for (int xx = 0; xx < 5; xx++)
                Px(x + xx, y + yy, yy == 1 || yy == 4 ? P.Iron[2] : xx == 0 ? P.Wood[3] : xx == 4 ? P.Wood[1] : P.Wood[2]);
        HLine(x, x + 4, y + 6, P.Wood[3]);
    }

    public void Pot(int x, int y, Color32[] ramp = null)
    {
        ramp ??= P.Tile;
        Ellipse(x + 2.5f, y + 2.5f, 2.6f, 2.6f, ramp);
        HLine(x + 1, x + 3, y + 5, ramp[1]);
    }

    public void LogPile(int x, int y, int rows)
    {
        for (int r = 0; r < rows; r++)
            for (int i = 0; i < rows - r; i++)
            {
                int cx = x + i * 4 + r * 2, cy = y + r * 3;
                Rect(cx, cy, cx + 2, cy + 2, P.Wood[2]);
                Px(cx + 1, cy + 1, P.Wood[3]);
                Px(cx, cy + 2, P.Wood[3]);
            }
    }

    public void LongLogs(int x, int y, int len, int count)
    {
        for (int i = 0; i < count; i++)
        {
            int yy = y + i * 2;
            HLine(x + i, x + len + i, yy, P.Wood[2]);
            HLine(x + i, x + len + i, yy + 1, P.Wood[3]);
            Px(x + len + i, yy, P.Wood[3]); Px(x + len + i + 1, yy, P.Wood[3]); Px(x + len + i, yy + 1, P.Wood[2]);
        }
    }

    public void StonePile(int x, int y, int n)
    {
        for (int i = 0; i < n; i++)
        {
            int bx = x + (i % 3) * 5 + (i / 3) * 2, by = y + (i / 3) * 3;
            Rect(bx, by, bx + 4, by + 2, P.Stone[2]);
            HLine(bx, bx + 4, by + 2, P.Stone[4]);
            VLine(bx + 4, by, by + 1, P.Stone[1]);
        }
    }

    public void Fence(int x0, int x1, int y, int h = 6)
    {
        for (int x = x0; x <= x1; x++)
        {
            Px(x, y + h - 2, P.Wood[3]);
            Px(x, y + h / 2 - 1, P.Wood[2]);
        }
        for (int x = x0; x <= x1; x += 5)
        {
            VLine(x, y, y + h, P.Wood[2]);
            Px(x, y + h, P.Wood[3]);
        }
    }

    public void SpearRack(int x, int y, int n, bool axes = false)
    {
        HLine(x - 1, x + n * 3, y + 5, P.Wood[1]);
        for (int i = 0; i < n; i++)
        {
            int sx = x + i * 3;
            VLine(sx, y, y + 13, P.Wood[3]);
            if (axes) { Rect(sx + 1, y + 10, sx + 2, y + 13, P.Steel[2]); Px(sx + 2, y + 13, P.Steel[3]); }
            else { Px(sx, y + 14, P.Steel[3]); Px(sx, y + 15, P.Steel[2]); }
        }
        HLine(x - 1, x + n * 3, y + 1, P.Wood[1]);
    }

    public void ShieldDecor(int cx, int cy, Color32[] ramp = null)
    {
        ramp ??= Team;
        Ellipse(cx, cy, 3.2f, 3.2f, ramp);
        Px(cx, cy, P.Gold[3]);
    }

    public void Target(int x, int y)
    {
        VLine(x + 3, y, y + 5, P.Wood[1]);
        Line(x + 1, y, x + 3, y + 4, P.Wood[2]);
        Ellipse(x + 3.5f, y + 9, 4.2f, 4.4f, P.Straw);
        Ellipse(x + 3.5f, y + 9, 2.6f, 2.8f, new[] { P.Red[1], P.Red[2], P.Red[2], P.Red[3] });
        Px(x + 3, y + 9, P.White);
    }

    public void Anvil(int x, int y)
    {
        Rect(x + 1, y, x + 4, y + 2, P.Iron[1]);
        Rect(x, y + 3, x + 6, y + 4, P.Iron[2]);
        HLine(x, x + 6, y + 5, P.Iron[3]);
        Px(x + 7, y + 4, P.Iron[2]);
    }

    public void Hay(int x, int y)
    {
        Rect(x, y, x + 7, y + 4, P.Straw[2]);
        HLine(x, x + 7, y + 4, P.Straw[3]);
        VLine(x + 2, y, y + 4, P.Straw[1]); VLine(x + 5, y, y + 4, P.Straw[1]);
    }

    public void Trough(int x, int y, int w)
    {
        Rect(x, y, x + w, y + 2, P.Wood[1]);
        HLine(x + 1, x + w - 1, y + 3, P.Water[3]);
        Px(x, y + 3, P.Wood[2]); Px(x + w, y + 3, P.Wood[1]);
    }

    public void Basket(int x, int y, Color32[] fill)
    {
        Rect(x, y, x + 5, y + 3, P.Straw[1]);
        HLine(x, x + 5, y + 2, P.Straw[2]);
        for (int i = 0; i < 5; i++) Px(x + i + 1, y + 4, fill[(i % 2) + 2]);
        Px(x + 2, y + 5, fill[3]);
    }

    public void Cart(int x, int y, Color32[] load = null)
    {
        Rect(x, y + 3, x + 10, y + 6, P.Wood[2]);
        HLine(x, x + 10, y + 6, P.Wood[3]);
        Ellipse(x + 3, y + 2, 2.4f, 2.4f, P.Wood);
        Ellipse(x + 8, y + 2, 2.4f, 2.4f, P.Wood);
        Px(x + 3, y + 2, P.Wood[0]); Px(x + 8, y + 2, P.Wood[0]);
        if (load != null)
            for (int i = 0; i < 9; i++) Px(x + 1 + i, y + 7 + (i % 3 == 1 ? 1 : 0), load[(i % 2) + 1]);
    }

    public void Column(int x, int y, int h)
    {
        for (int yy = y; yy < y + h; yy++)
        {
            Px(x, yy, P.Marble[3]); Px(x + 1, yy, P.Marble[2]); Px(x + 2, yy, P.Marble[2]); Px(x + 3, yy, P.Marble[1]);
        }
        HLine(x - 1, x + 4, y + h, P.Marble[3]);
        HLine(x - 1, x + 4, y - 1, P.Marble[1]);
    }

    public void Rocks(int x, int y, int n)
    {
        for (int i = 0; i < n; i++)
            Ellipse(x + i * 4 + R(0, 2), y + (i % 2) * 2 + 2, 2.5f + R(0, 2), 2f + R(0, 2) * 0.5f, P.Rock);
    }

    /// <summary>A rocky hillside (mines and quarries): strata, a grassy cap, lit from the top-left.</summary>
    public void Mound(float cx, float cy, float rx, float ry, Color32[] ramp = null)
    {
        ramp ??= P.Rock;
        var rr = ramp;
        C.Ellipse(new Vector2(cx, cy), rx, ry, rr, l => l.y >= 1, -0.35f);
        for (int y = Mathf.FloorToInt(cy - ry); y <= cy + ry; y++)
            for (int x = Mathf.FloorToInt(cx - rx); x <= cx + rx; x++)
            {
                var c = Get(x, y);
                if (c.a < 200) continue;
                float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                float rim = dx * dx + dy * dy;
                // grassy cap with a ragged edge
                if (dy > 0.45f + Mathf.Sin(x * 0.7f) * 0.08f) { Px(x, y, rim > 0.85f ? P.Grass[1] : (x + y) % 5 == 0 ? P.Grass[3] : P.Grass[2]); continue; }
                // rock strata
                if ((y + Mathf.RoundToInt(Mathf.Sin(x * 0.25f) * 2)) % 6 == 0) Px(x, y, rr[1]);
                else if ((x * 5 + y * 3) % 23 == 0) Px(x, y, rr[3]);
            }
    }

    /// <summary>A timber-framed tunnel mouth into a mound.</summary>
    public void MineEntrance(int x, int y, int w, int h, bool stone = false)
    {
        var frame = stone ? P.Stone : P.Wood;
        Rect(x, y, x + w, y + h, P.Coal[0]);
        Rect(x + 2, y, x + w - 2, y + h - 2, P.Coal[1]);
        for (int yy = y; yy <= y + h; yy++) { Px(x, yy, frame[2]); Px(x + 1, yy, frame[1]); Px(x + w, yy, frame[1]); Px(x + w - 1, yy, frame[2]); }
        HLine(x - 1, x + w + 1, y + h, frame[3]);
        HLine(x - 1, x + w + 1, y + h + 1, frame[2]);
    }

    public void Rails(int x0, int x1, int y)
    {
        HLine(x0, x1, y, P.Iron[2]);
        HLine(x0, x1, y + 3, P.Iron[2]);
        for (int x = x0; x <= x1; x += 3) VLine(x, y, y + 3, P.Wood[1]);
    }

    /// <summary>A dark hollow in the ground (quarry pits).</summary>
    public void Pit(int x0, int y0, int x1, int y1)
    {
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                int wob = Mathf.RoundToInt(Mathf.Sin(y * 0.45f) * 2f);
                if (x < x0 + 2 + wob || x > x1 - 2 + wob) continue;
                bool rim = y == y1 || x == x0 + 2 + wob || x == x1 - 2 + wob;
                Px(x, y, rim ? P.Rock[1] : y > y1 - 3 ? P.Rock[0] : ((x + y) % 5 == 0 ? P.Rock[1] : P.Rock[2]));
            }
        HLine(x0, x1, y1 + 1, P.Rock[3]);
    }

    /// <summary>A furrow of crops: ridged earth with tufts that sway with the loop.</summary>
    public void CropRow(int x0, int x1, int y, int growth, Color32[] crop)
    {
        for (int x = x0; x <= x1; x++) { Px(x, y, P.Dirt[1]); Px(x, y + 1, P.Dirt[2]); Px(x, y + 2, P.Dirt[2]); }
        for (int x = x0 + 1; x <= x1; x += 3)
        {
            int sway = Mathf.RoundToInt(Mathf.Sin(T * 6.28f + x * 0.5f) * 0.6f);
            for (int j = 1; j <= growth; j++) Px(x + (j == growth ? sway : 0), y + j, j == growth ? crop[3] : crop[2]);
            Px(x + 1, y + 1, crop[1]);
        }
    }

    public void Finish(int smokeX = int.MinValue, int smokeY = 0)
    {
        C.Shift = Vector2.zero;
        C.Outline(P.Ink, 0.55f);
        C.Shift = new Vector2(Ox, Oy);
    }
}
