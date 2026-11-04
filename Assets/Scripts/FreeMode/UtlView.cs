using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Utilities on the map of the world you're looking at:
/// - Overlays (Mode: water, power, data or pollution), also shown by themselves while you place a
///   utility: each district's reach as a faint ring, the buildings its network reaches filled in
///   (blue, yellow, cyan; orange where it's short, red for works and homes it misses), with the
///   lines between its nodes, or the smog as a heat map.
/// - Power cables hanging between poles and pylons (always).
/// - Smog: a faint pixel haze over polluted quarters (greenish on toxic worlds).
/// Fixed textures and renderer pools, redrawn only when the networks are worked out again.
/// Sets itself up in any scene with a free mode map.
/// </summary>
public class UtlView : MonoBehaviour
{
    /// <summary>0 off, 1 water, 2 power, 3 data, 4 pollution.</summary>
    public static int Mode;
    public static readonly string[] ModeNames = { "Off", "Water", "Power", "Data", "Smog" };

    private const int CoverOrder = 85, LinkOrder = 86, WireOrder = -4, HazeOrder = 88;
    private const int CoverTex = 384;           // tiles across (1 pixel a tile)
    private const int HazeTiles = 192, HazePx = 2;
    private const int LinkCap = 300, WireCap = 400;

    private Settlement settlement;
    private FreeModeController controller;
    private Camera cam;

    private Texture2D coverTex, hazeTex;
    private Color32[] coverPx, hazePx;
    private byte[] best, fill;
    private SpriteRenderer cover, haze;
    private SpriteRenderer[] links, wires;
    private Sprite dot;
    private int linksUsed, wiresUsed;

    private int seenVersion = -1, seenMode = -1, seenWorld = -1, hazeVersion = -1, wireVersion = -1;
    private float checkAt;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Mode = 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        TryCreate();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryCreate();

    private static void TryCreate()
    {
        if (FindAnyObjectByType<FreeModeController>() == null || FindAnyObjectByType<UtlView>() != null) return;
        new GameObject("Utilities").AddComponent<UtlView>();
    }

    private void Start()
    {
        settlement = Settlement.Instance;
        controller = FreeModeController.Instance != null ? FreeModeController.Instance : FindAnyObjectByType<FreeModeController>();
        cam = Camera.main;
        if (settlement == null) { enabled = false; return; }
        var white = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        white.SetPixel(0, 0, Color.white);
        white.Apply();
        dot = Sprite.Create(white, new Rect(0, 0, 1, 1), new Vector2(0f, 0.5f), 1f, 0, SpriteMeshType.FullRect);

        coverTex = new Texture2D(CoverTex, CoverTex, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        coverPx = new Color32[CoverTex * CoverTex];
        best = new byte[CoverTex * CoverTex];
        fill = new byte[CoverTex * CoverTex];
        cover = MakeRenderer("Overlay", Sprite.Create(coverTex, new Rect(0, 0, CoverTex, CoverTex), Vector2.zero, 1f, 0, SpriteMeshType.FullRect), CoverOrder);

        int hp = HazeTiles * HazePx;
        hazeTex = new Texture2D(hp, hp, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        hazePx = new Color32[hp * hp];
        haze = MakeRenderer("Smog", Sprite.Create(hazeTex, new Rect(0, 0, hp, hp), Vector2.zero, HazePx, 0, SpriteMeshType.FullRect), HazeOrder);

        links = new SpriteRenderer[LinkCap];
        for (int i = 0; i < LinkCap; i++) links[i] = MakeRenderer("Link", dot, LinkOrder);
        wires = new SpriteRenderer[WireCap];
        for (int i = 0; i < WireCap; i++) wires[i] = MakeRenderer("Wire", dot, WireOrder);
    }

    private SpriteRenderer MakeRenderer(string name, Sprite sprite, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = order;
        sr.enabled = false;
        return sr;
    }

    /// <summary>The overlay showing now: the one picked, or the one for the utility being placed.</summary>
    private int ShownMode()
    {
        if (Mode != 0) return Mode;
        var placer = controller != null ? controller.Placer : null;
        return placer != null && placer.IsPlacing ? UtlCatalog.OverlayFor(placer.Current) : 0;
    }

    private void Update()
    {
        if (settlement == null || settlement.State == null || Time.unscaledTime < checkAt) return;
        checkAt = Time.unscaledTime + 0.25f;
        var n = settlement.UtilitiesHere;
        int world = settlement.State.currentWorld;
        int mode = ShownMode();
        bool worldChanged = world != seenWorld;
        if (worldChanged) { seenWorld = world; seenVersion = -1; hazeVersion = -1; wireVersion = -1; }
        if (mode != seenMode || n.version != seenVersion)
        {
            seenMode = mode;
            seenVersion = n.version;
            DrawOverlay(n, mode);
        }
        if (n.version != wireVersion)
        {
            wireVersion = n.version;
            DrawWires(n);
        }
        // The haze changes slowly: redraw it every other time the air is worked out.
        if (n.version >= hazeVersion + 2 || hazeVersion < 0)
        {
            hazeVersion = n.version;
            DrawHaze(n);
        }
        if (haze.enabled)
        {
            // A slow drift, so it reads as air and not paint.
            float t = Time.time * 0.15f;
            haze.transform.position = hazeBase + new Vector3(Mathf.Sin(t) * 0.25f, Mathf.Cos(t * 0.7f) * 0.15f, 0f);
        }
    }

    // ---------- Overlays ----------

    private static Color32 LevelColor(int mode, float level, bool edge)
    {
        byte a = (byte)(edge ? 150 : 62);
        if (level <= 0.01f) return new Color32(235, 80, 70, a);
        switch (mode)
        {
            case 1: return level >= 0.99f ? new Color32(110, 220, 255, a) : new Color32(70, 140, 255, a);
            case 2: return level >= 0.99f ? new Color32(255, 222, 80, a) : new Color32(255, 150, 50, a);
            default: return level >= 0.99f ? new Color32(90, 240, 230, a) : new Color32(120, 160, 170, a);
        }
    }

    private void DrawOverlay(Settlement.UtlNet n, int mode)
    {
        linksUsed = 0;
        if (mode == 0)
        {
            cover.enabled = false;
            HideFrom(links, 0);
            return;
        }
        // Bounds: the rings (or the smog), clipped round the camera if they're bigger than the texture.
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        if (mode == 4)
        {
            foreach (var pair in n.smog)
            {
                float cx = (int)(pair.Key >> 32) * UtlCatalog.Cell, cy = (int)(uint)(pair.Key & 0xffffffffL) * UtlCatalog.Cell;
                minX = Mathf.Min(minX, cx); minY = Mathf.Min(minY, cy);
                maxX = Mathf.Max(maxX, cx + UtlCatalog.Cell); maxY = Mathf.Max(maxY, cy + UtlCatalog.Cell);
            }
        }
        else
        {
            foreach (var d in n.discs)
            {
                if (d.kind != mode) continue;
                minX = Mathf.Min(minX, d.at.x - d.r); minY = Mathf.Min(minY, d.at.y - d.r);
                maxX = Mathf.Max(maxX, d.at.x + d.r); maxY = Mathf.Max(maxY, d.at.y + d.r);
            }
            foreach (var c in n.cover)
            {
                if (c.kind != mode) continue;
                minX = Mathf.Min(minX, c.r.xMin - 1); minY = Mathf.Min(minY, c.r.yMin - 1);
                maxX = Mathf.Max(maxX, c.r.xMax + 1); maxY = Mathf.Max(maxY, c.r.yMax + 1);
            }
        }
        if (minX > maxX) { cover.enabled = false; HideFrom(links, 0); return; }
        int x0 = Mathf.FloorToInt(minX) - 1, y0 = Mathf.FloorToInt(minY) - 1;
        int w = Mathf.CeilToInt(maxX) + 1 - x0, h = Mathf.CeilToInt(maxY) + 1 - y0;
        if (w > CoverTex || h > CoverTex)
        {
            Vector3 c = cam != null ? cam.transform.position : new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, 0f);
            if (w > CoverTex) x0 = Mathf.Clamp(Mathf.RoundToInt(c.x) - CoverTex / 2, x0, x0 + w - CoverTex);
            if (h > CoverTex) y0 = Mathf.Clamp(Mathf.RoundToInt(c.y) - CoverTex / 2, y0, y0 + h - CoverTex);
        }
        drawW = Mathf.Min(w, CoverTex);
        drawH = Mathf.Min(h, CoverTex);
        System.Array.Clear(coverPx, 0, coverPx.Length);
        if (mode == 4) PaintSmog(n, x0, y0);
        else PaintRings(n, mode, x0, y0);
        coverTex.SetPixels32(coverPx);
        coverTex.Apply(false);
        cover.transform.position = new Vector3(x0, y0, 0f);
        cover.enabled = true;

        // The links between the nodes of this network.
        if (mode != 4)
            foreach (var l in n.links)
            {
                if (l.kind != mode && !(mode == 1 && l.kind == 1)) continue;
                if (linksUsed >= LinkCap) break;
                var col = mode == 1 ? new Color(0.45f, 0.8f, 1f, 0.85f) : mode == 2 ? new Color(1f, 0.85f, 0.3f, 0.9f) : new Color(0.4f, 1f, 0.95f, 0.85f);
                Segment(links[linksUsed++], l.a, l.b, 0.12f, col, mode == 3);
            }
        HideFrom(links, linksUsed);
    }

    private int drawW = CoverTex, drawH = CoverTex;

    private void PaintRings(Settlement.UtlNet n, int mode, int x0, int y0)
    {
        System.Array.Clear(best, 0, best.Length);
        // best = 1 + level*254 of the best ring over each tile.
        foreach (var d in n.discs)
        {
            if (d.kind != mode) continue;
            byte lv = (byte)(1 + Mathf.RoundToInt(Mathf.Clamp01(d.level) * 254f));
            int r = Mathf.CeilToInt(d.r);
            float r2 = d.r * d.r;
            for (int ty = Mathf.FloorToInt(d.at.y) - r; ty <= Mathf.FloorToInt(d.at.y) + r; ty++)
            {
                int py = ty - y0;
                if (py < 0 || py >= CoverTex) continue;
                for (int tx = Mathf.FloorToInt(d.at.x) - r; tx <= Mathf.FloorToInt(d.at.x) + r; tx++)
                {
                    int px = tx - x0;
                    if (px < 0 || px >= CoverTex) continue;
                    float dx = tx + 0.5f - d.at.x, dy = ty + 0.5f - d.at.y;
                    if (dx * dx + dy * dy > r2) continue;
                    int i = py * CoverTex + px;
                    if (lv > best[i]) best[i] = lv;
                }
            }
        }
        // The buildings the network reaches (or misses), filled in a little stronger than the rings.
        System.Array.Clear(fill, 0, fill.Length);
        foreach (var c in n.cover)
        {
            if (c.kind != mode) continue;
            byte lv = (byte)(1 + Mathf.RoundToInt(Mathf.Clamp01(c.level) * 254f));
            for (int ty = c.r.yMin; ty < c.r.yMax; ty++)
            {
                int py = ty - y0;
                if (py < 0 || py >= CoverTex) continue;
                for (int tx = c.r.xMin; tx < c.r.xMax; tx++)
                {
                    int px = tx - x0;
                    if (px < 0 || px >= CoverTex) continue;
                    int i = py * CoverTex + px;
                    if (fill[i] == 0 || lv < fill[i]) fill[i] = lv;   // a building short of supply shows as short
                }
            }
        }
        for (int py = 0; py < drawH; py++)
            for (int px = 0; px < drawW; px++)
            {
                int i = py * CoverTex + px;
                if (fill[i] != 0)
                {
                    var col = LevelColor(mode, (fill[i] - 1) / 254f, false);
                    col.a = 120;
                    coverPx[i] = col;
                    continue;
                }
                byte b = best[i];
                if (b == 0) continue;
                // The rim of the covered area is drawn bolder, so its reach reads at a glance.
                bool edge = px == 0 || py == 0 || px == CoverTex - 1 || py == CoverTex - 1
                            || best[i - 1] == 0 || best[i + 1] == 0 || best[i - CoverTex] == 0 || best[i + CoverTex] == 0;
                coverPx[i] = LevelColor(mode, (b - 1) / 254f, edge);
            }
    }

    // Smog cell values copied out of the dictionary for the area being drawn (no lookups per pixel).
    private float[] cells = new float[64];
    private int cellX0, cellY0, cellW, cellH;

    private void LoadCells(Settlement.UtlNet n, int x0, int y0, int tilesW, int tilesH)
    {
        cellX0 = Mathf.FloorToInt((float)x0 / UtlCatalog.Cell) - 1;
        cellY0 = Mathf.FloorToInt((float)y0 / UtlCatalog.Cell) - 1;
        cellW = tilesW / UtlCatalog.Cell + 3;
        cellH = tilesH / UtlCatalog.Cell + 3;
        if (cells.Length < cellW * cellH) cells = new float[Mathf.NextPowerOfTwo(cellW * cellH)];
        for (int cy = 0; cy < cellH; cy++)
            for (int cx = 0; cx < cellW; cx++)
                cells[cy * cellW + cx] = n.smog.TryGetValue(((long)(cellX0 + cx) << 32) ^ (uint)(cellY0 + cy), out float v) ? v : 0f;
    }

    /// <summary>Smog at a point, smoothed between the cell centres (from the loaded cells).</summary>
    private float SmogSmooth(float x, float y)
    {
        float fx = x / UtlCatalog.Cell - 0.5f - cellX0, fy = y / UtlCatalog.Cell - 0.5f - cellY0;
        int cx = Mathf.FloorToInt(fx), cy = Mathf.FloorToInt(fy);
        float tx = fx - cx, ty = fy - cy;
        float Get(int ax, int ay) => ax < 0 || ay < 0 || ax >= cellW || ay >= cellH ? 0f : cells[ay * cellW + ax];
        float a = Mathf.Lerp(Get(cx, cy), Get(cx + 1, cy), tx), b = Mathf.Lerp(Get(cx, cy + 1), Get(cx + 1, cy + 1), tx);
        return Mathf.Lerp(a, b, ty);
    }

    private void PaintSmog(Settlement.UtlNet n, int x0, int y0)
    {
        LoadCells(n, x0, y0, drawW, drawH);
        for (int py = 0; py < drawH; py++)
            for (int px = 0; px < drawW; px++)
            {
                float p = SmogSmooth(x0 + px + 0.5f, y0 + py + 0.5f);
                if (p < 1.5f) continue;
                float t = Mathf.Clamp01(p / 30f);
                // clear -> yellow-brown -> dark violet, stepped like pixel art
                int step = Mathf.Min(4, Mathf.FloorToInt(t * 5f));
                Color32 c = step switch
                {
                    0 => new Color32(200, 200, 120, 50),
                    1 => new Color32(210, 170, 80, 80),
                    2 => new Color32(190, 110, 60, 105),
                    3 => new Color32(150, 60, 90, 130),
                    _ => new Color32(100, 30, 110, 155),
                };
                coverPx[py * CoverTex + px] = c;
            }
    }

    private void Segment(SpriteRenderer sr, Vector2 a, Vector2 b, float width, Color color, bool dashed)
    {
        var d = b - a;
        float len = d.magnitude;
        if (len < 0.01f) { sr.enabled = false; return; }
        sr.transform.position = new Vector3(a.x, a.y, -0.5f);
        sr.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        sr.transform.localScale = new Vector3(len, width, 1f);
        sr.color = dashed ? new Color(color.r, color.g, color.b, color.a * 0.7f) : color;
        sr.enabled = true;
    }

    private static void HideFrom(SpriteRenderer[] list, int from)
    {
        for (int i = from; i < list.Length; i++) if (list[i].enabled) list[i].enabled = false;
    }

    // ---------- Cables ----------

    private static readonly Color WireColor = new Color(0.12f, 0.1f, 0.12f, 0.85f);

    /// <summary>Cables sag between poles: two straight runs meeting a little below the middle.</summary>
    private void DrawWires(Settlement.UtlNet n)
    {
        wiresUsed = 0;
        foreach (var l in n.links)
        {
            if (!l.wired || wiresUsed + 2 > WireCap) continue;
            var a = new Vector2(l.a.x, l.a.y + l.ha);
            var b = new Vector2(l.b.x, l.b.y + l.hb);
            float len = Vector2.Distance(a, b);
            var mid = (a + b) * 0.5f + Vector2.down * (0.2f + len * 0.025f);
            Segment(wires[wiresUsed++], a, mid, 0.06f, WireColor, false);
            Segment(wires[wiresUsed++], mid, b, 0.06f, WireColor, false);
        }
        HideFrom(wires, wiresUsed);
    }

    // ---------- Smog haze ----------

    private Vector3 hazeBase;

    // 4x4 ordered dither, for a pixel-art haze rather than a smooth fog.
    private static readonly int[] Bayer = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

    private void DrawHaze(Settlement.UtlNet n)
    {
        float worst = 0f;
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        foreach (var pair in n.smog)
        {
            if (pair.Value < 5f) continue;
            worst = Mathf.Max(worst, pair.Value);
            float cx = (int)(pair.Key >> 32) * UtlCatalog.Cell, cy = (int)(uint)(pair.Key & 0xffffffffL) * UtlCatalog.Cell;
            minX = Mathf.Min(minX, cx - UtlCatalog.Cell); minY = Mathf.Min(minY, cy - UtlCatalog.Cell);
            maxX = Mathf.Max(maxX, cx + UtlCatalog.Cell * 2); maxY = Mathf.Max(maxY, cy + UtlCatalog.Cell * 2);
        }
        if (worst < 5f) { haze.enabled = false; return; }
        int x0 = Mathf.FloorToInt(minX), y0 = Mathf.FloorToInt(minY);
        int w = Mathf.CeilToInt(maxX) - x0, h = Mathf.CeilToInt(maxY) - y0;
        if (w > HazeTiles || h > HazeTiles)
        {
            Vector3 c = cam != null ? cam.transform.position : new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, 0f);
            if (w > HazeTiles) x0 = Mathf.Clamp(Mathf.RoundToInt(c.x) - HazeTiles / 2, x0, x0 + w - HazeTiles);
            if (h > HazeTiles) y0 = Mathf.Clamp(Mathf.RoundToInt(c.y) - HazeTiles / 2, y0, y0 + h - HazeTiles);
        }
        bool toxic = settlement.HasTrait(Settlement.ToxicAir);
        Color32 tint = toxic ? new Color32(150, 175, 95, 0) : new Color32(140, 122, 104, 0);
        int size = HazeTiles * HazePx;
        System.Array.Clear(hazePx, 0, hazePx.Length);
        LoadCells(n, x0, y0, HazeTiles, HazeTiles);
        for (int ty = 0; ty < HazeTiles; ty++)
        {
            float wy = y0 + ty + 0.5f;
            for (int tx = 0; tx < HazeTiles; tx++)
            {
                float wx = x0 + tx + 0.5f;
                float p = SmogSmooth(wx, wy);
                if (p < 5f) continue;
                // A soft billow so it isn't flat: two slow ripples in world space.
                float billow = Mathf.Sin(wx * 0.35f + wy * 0.2f) * 0.5f + Mathf.Sin(wx * 0.13f - wy * 0.31f) * 0.5f;
                float t = Mathf.Clamp01((p - 5f) / 25f) * (0.75f + 0.25f * billow);
                int level = Mathf.FloorToInt(t * 48f);   // three alpha steps, dithered between
                for (int sy = 0; sy < HazePx; sy++)
                    for (int sx = 0; sx < HazePx; sx++)
                    {
                        int px = tx * HazePx + sx, py = ty * HazePx + sy;
                        int stepIndex = level / 16;
                        if (level % 16 > Bayer[(py & 3) * 4 + (px & 3)]) stepIndex++;
                        if (stepIndex <= 0) continue;
                        hazePx[py * size + px] = new Color32(tint.r, tint.g, tint.b, (byte)Mathf.Min(90, stepIndex * 26));
                    }
            }
        }
        hazeTex.SetPixels32(hazePx);
        hazeTex.Apply(false);
        hazeBase = new Vector3(x0, y0, -0.8f);
        haze.transform.position = hazeBase;
        haze.enabled = true;
    }
}
