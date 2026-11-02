using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Draws the free mode map and maps tiles to world positions. Tile (0,0) is at this object's
/// position; each tile is cellSize world units. Only explored zones are drawn; the map grows
/// zone by zone as the player explores, up to a (very large) size limit.
///
/// Three levels of detail, by camera zoom:
///  1. Detail: ground and deposit Tilemaps (deposits split into regions so updates stay cheap).
///  2. Overview: pre-drawn pictures, a couple of pixels per tile, one per region.
///  3. Strategy: one coloured square per zone, plus map markers (settlements) drawn by others.
/// </summary>
public class SettlementGrid : MonoBehaviour
{
    public float cellSize = 1f;

    [Header("Levels of detail (camera zoom)")]
    [Tooltip("Above this zoom the detailed tiles are swapped for the overview pictures.")]
    public float overviewZoom = 26f;

    [Tooltip("Above this zoom the overview is swapped for the strategy map of zone squares.")]
    public float strategyZoom = 90f;

    public MapData Map { get; private set; }

    /// <summary>Bounding box of the explored map (camera limits).</summary>
    public Rect MapRect { get; private set; }

    public enum Detail { Tiles, Overview, Strategy }
    public Detail CurrentDetail { get; private set; } = Detail.Tiles;

    private const int RegionTiles = 64;          // deposit tilemap and overview picture regions
    private const int OverviewPixelsPerTile = 4;

    private static readonly Color LinesIdle = new Color(1, 1, 1, 0f);
    private static readonly Color LinesPlacing = new Color(1, 1, 1, 0.35f);
    private static readonly Color32 FogColor = new Color32(24, 32, 22, 255);
    private static readonly Color32 GrassColor = new Color32(108, 153, 79, 255);
    private static readonly Color32 WaterColor = new Color32(69, 133, 204, 255);
    private static readonly Color32 HillColor = new Color32(143, 130, 110, 255);

    private GameObject tileGrid;
    private SpriteRenderer gridLines;
    private Tilemap groundTilemap;
    private TilemapRenderer groundRenderer;
    private Tile grassTile, waterTile, hillTile;

    // Smooth terrain: hills and water drawn on grids offset half a tile, one shape per corner pattern.
    private bool smoothTerrain;
    private Tile[] grassVariants;
    private Tilemap waterTilemap;
    private TilemapRenderer waterRenderer;
    private Tile[] hillFullVariants;
    // Hill tiers 1-3: each tier is its own layer, drawn over the middle of the one below.
    private readonly List<Tilemap> hillTilemaps = new List<Tilemap>();
    private readonly List<TilemapRenderer> hillRenderers = new List<TilemapRenderer>();
    private readonly List<Tile[]> hillTiers = new List<Tile[]>();
    private TileBase[] waterShapes;

    private class Region
    {
        public Tilemap deposits;
        public TilemapRenderer depositRenderer;
        public Texture2D overview;
        public SpriteRenderer overviewRenderer;
        public bool overviewDirty;
    }

    private readonly Dictionary<Vector2Int, Region> regions = new Dictionary<Vector2Int, Region>();
    private readonly Dictionary<Sprite, Tile> depositTileFor = new Dictionary<Sprite, Tile>();
    private readonly HashSet<Vector2Int> hiddenDeposits = new HashSet<Vector2Int>();
    private readonly List<TileChangeData> depositChanges = new List<TileChangeData>();

    private Texture2D strategyTexture;
    private SpriteRenderer strategy;

    // ---------- Building ----------

    public void Build(MapData map)
    {
        Map = map;
        var art = ArtLibrary.Load();

        // Unexplored land: plain fog colour under everything (stretched, not tiled).
        var fog = new GameObject("Fog").AddComponent<SpriteRenderer>();
        fog.transform.SetParent(transform, false);
        fog.sprite = SimpleSprites.Square;
        fog.color = FogColor;
        fog.sortingOrder = -28;
        fog.transform.localPosition = new Vector3(-200, -200, 0);
        fog.transform.localScale = new Vector3(map.Width + 400, map.Height + 400, 1);

        tileGrid = new GameObject("TileGrid");
        tileGrid.transform.SetParent(transform, false);
        tileGrid.AddComponent<Grid>().cellSize = new Vector3(cellSize, cellSize, 0);
        var groundObject = new GameObject("Ground");
        groundObject.transform.SetParent(tileGrid.transform, false);
        groundTilemap = groundObject.AddComponent<Tilemap>();
        groundRenderer = groundObject.AddComponent<TilemapRenderer>();
        groundRenderer.sortingOrder = -27;
        grassTile = MakeTile(art != null ? art.grassTile : null, new Color(0.42f, 0.6f, 0.31f));
        waterTile = MakeTile(art != null ? art.waterTile : null, new Color(0.25f, 0.48f, 0.75f));
        hillTile = MakeTile(art != null ? art.hillTile : null, new Color(0.55f, 0.5f, 0.42f));
        BuildSmoothTerrain(art);

        // Strategy map: one pixel per zone, point-filtered into crisp squares.
        strategyTexture = new Texture2D(map.ChunksX, map.ChunksY, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        var fogPixels = new Color32[map.ChunksX * map.ChunksY];
        for (int i = 0; i < fogPixels.Length; i++)
        {
            fogPixels[i] = FogColor;
        }
        strategyTexture.SetPixels32(fogPixels);
        strategy = new GameObject("StrategyMap").AddComponent<SpriteRenderer>();
        strategy.transform.SetParent(transform, false);
        strategy.sprite = Sprite.Create(strategyTexture, new Rect(0, 0, map.ChunksX, map.ChunksY), Vector2.zero, 1f / (map.ChunkSize * cellSize));
        strategy.sortingOrder = -20;
        strategy.enabled = false;

        // Grid lines over the explored map; 8x8-cell tiles keep the tile count low.
        gridLines = new GameObject("GridLines").AddComponent<SpriteRenderer>();
        gridLines.transform.SetParent(transform, false);
        gridLines.sprite = SimpleSprites.GridBlock;
        gridLines.drawMode = SpriteDrawMode.Tiled;
        gridLines.transform.localScale = Vector3.one * cellSize;
        gridLines.sortingOrder = -19;
        SetPlacing(false);

        var all = new List<RectInt>();
        foreach (int index in map.RevealedZoneIndices())
        {
            all.Add(map.ZoneRect(index % map.ChunksX, index / map.ChunksX));
        }
        OnMapGrown(all);
    }

    /// <summary>Draw newly explored zones.</summary>
    public void OnMapGrown(List<RectInt> newAreas)
    {
        foreach (var area in newAreas)
        {
            DrawArea(area);
            PaintStrategyZone(area);
        }
        foreach (var region in regions.Values)
        {
            if (region.overviewDirty)
            {
                region.overview.Apply();
                region.overviewDirty = false;
            }
        }
        strategyTexture.Apply();
        UpdateBounds();
        ApplyDetail(CurrentDetail, force: true);
    }

    private void UpdateBounds()
    {
        var r = Map.Revealed;
        MapRect = new Rect((Vector2)transform.position + new Vector2(r.x, r.y) * cellSize, new Vector2(r.width, r.height) * cellSize);
        var r8 = new Vector2Int(r.xMin - r.xMin % 8, r.yMin - r.yMin % 8);
        gridLines.transform.position = CellToWorld(r8);
        gridLines.size = new Vector2(Mathf.Ceil((r.xMax - r8.x) / 8f) * 8, Mathf.Ceil((r.yMax - r8.y) / 8f) * 8);
    }

    // ---------- Regions ----------

    private Region RegionAt(int x, int y)
    {
        var key = new Vector2Int(x / RegionTiles, y / RegionTiles);
        if (regions.TryGetValue(key, out var region))
        {
            return region;
        }
        region = new Region();
        var depositObject = new GameObject($"Deposits {key.x},{key.y}");
        depositObject.transform.SetParent(tileGrid.transform, false);
        region.deposits = depositObject.AddComponent<Tilemap>();
        region.depositRenderer = depositObject.AddComponent<TilemapRenderer>();
        // Trees, rocks and ores sort one by one with units and buildings, by where they touch
        // the ground (each tile's depth is set in DepositChange), so a figure can walk behind a tree.
        region.depositRenderer.sortingOrder = BuildingView.SortingOrder;
        region.depositRenderer.mode = TilemapRenderer.Mode.Individual;

        int px = OverviewPixelsPerTile;
        region.overview = new Texture2D(RegionTiles * px, RegionTiles * px, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        var fill = new Color32[region.overview.width * region.overview.height];
        for (int i = 0; i < fill.Length; i++)
        {
            fill[i] = FogColor;
        }
        region.overview.SetPixels32(fill);
        region.overviewRenderer = new GameObject($"Overview {key.x},{key.y}").AddComponent<SpriteRenderer>();
        region.overviewRenderer.transform.SetParent(transform, false);
        region.overviewRenderer.transform.localPosition = new Vector3(key.x * RegionTiles, key.y * RegionTiles, 0) * cellSize;
        region.overviewRenderer.sprite = Sprite.Create(region.overview,
            new Rect(0, 0, region.overview.width, region.overview.height), Vector2.zero, px / cellSize);
        region.overviewRenderer.sortingOrder = -20;
        regions[key] = region;
        return region;
    }

    // ---------- Drawing ----------

    /// <summary>Draw one area (inside a single region): ground, deposits and its overview pixels.</summary>
    private void DrawArea(RectInt area)
    {
        int n = area.width * area.height;
        var positions = new Vector3Int[n];
        var tiles = new TileBase[n];
        int px = OverviewPixelsPerTile;
        var block = new Color32[px * px];
        var region = RegionAt(area.xMin, area.yMin);
        int baseX = (area.xMin / RegionTiles) * RegionTiles, baseY = (area.yMin / RegionTiles) * RegionTiles;
        int i = 0;
        for (int y = area.yMin; y < area.yMax; y++)
        {
            for (int x = area.xMin; x < area.xMax; x++)
            {
                var t = Map.RawTerrain(x, y);
                positions[i] = new Vector3Int(x, y, 0);
                tiles[i] = smoothTerrain ? GrassAt(x, y)
                    : t == TerrainType.Water ? waterTile : t == TerrainType.Hill ? hillTile : grassTile;
                i++;

                var deposit = Map.RawDeposit(x, y);
                PaintOverviewTile(region, x, y, baseX, baseY, block);

                if (deposit != null && deposit.sprites.Count > 0 && !hiddenDeposits.Contains(new Vector2Int(x, y)))
                {
                    depositChanges.Add(DepositChange(x, y, deposit));
                }
            }
        }
        region.overviewDirty = true;
        groundTilemap.SetTiles(positions, tiles);
        if (smoothTerrain)
        {
            DrawEdges(area);
        }
        region.deposits.SetTiles(depositChanges.ToArray(), true);
        depositChanges.Clear();
    }

    /// <summary>One square on the strategy map: the zone's most common terrain or deposit colour.</summary>
    private void PaintStrategyZone(RectInt area)
    {
        int grass = 0, water = 0, hill = 0, forest = 0;
        for (int y = area.yMin; y < area.yMax; y++)
        {
            for (int x = area.xMin; x < area.xMax; x++)
            {
                var t = Map.RawTerrain(x, y);
                if (t == TerrainType.Water) water++;
                else if (t == TerrainType.Hill) hill++;
                else if (Map.RawDeposit(x, y)?.id == "Forest") forest++;
                else grass++;
            }
        }
        Color32 c = water >= grass && water >= hill && water >= forest ? WaterColor
            : hill >= grass && hill >= forest ? HillColor
            : forest >= grass ? new Color32(46, 98, 48, 255)
            : GrassColor;
        strategyTexture.SetPixel(area.xMin / Map.ChunkSize, area.yMin / Map.ChunkSize, c);
    }

    /// <summary>One deposit sprite with a stable per-tile jitter, size and mirroring.</summary>
    private TileChangeData DepositChange(int x, int y, DepositType type)
    {
        var sprite = type.sprites[(int)(Hash01(x, y, 1) * type.sprites.Count) % type.sprites.Count];
        if (!depositTileFor.TryGetValue(sprite, out var tile))
        {
            tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            tile.flags = TileFlags.LockColor;
            depositTileFor[sprite] = tile;
        }
        // Varied size and a loose scatter so deposit fields don't line up in rows.
        float scale = (0.58f + Hash01(x, y, 2) * 0.32f) / Mathf.Max(0.5f, sprite.bounds.size.x);
        float flip = Hash01(x, y, 3) < 0.5f ? -1f : 1f;
        float jx = (Hash01(x, y, 4) - 0.5f) * 0.6f, jy = (Hash01(x, y, 5) - 0.5f) * 0.4f, baseY = -0.3f;
        if (smoothTerrain)
        {
            // Keep rocks off the cliff faces of hill terraces: step back from a drop, and stay
            // low under a cliff rising behind, so the height steps stay visible.
            int level = HillLevel(x, y);
            if (HillLevel(x, y - 1) < level)
            {
                baseY = -0.02f;
                jy = Mathf.Abs(jy) * 0.5f;
                scale *= 0.85f;
            }
            else if (HillLevel(x, y + 1) > level)
            {
                baseY = -0.38f;
                jy = -Mathf.Abs(jy) * 0.3f;
                scale *= 0.85f;
            }
            if (HillLevel(x - 1, y) < level) jx = Mathf.Abs(jx) * 0.5f + 0.08f;
            if (HillLevel(x + 1, y) < level) jx = -Mathf.Abs(jx) * 0.5f - 0.08f;
        }
        // The sprite's pivot is its foot; tiles sit on the cell centre.
        float footY = transform.position.y + (y + 0.5f + jy + baseY) * cellSize;
        float depth = BuildingView.DepthFor(footY) - tileGrid.transform.position.z;
        var offset = new Vector3(jx, jy + baseY, depth);
        return new TileChangeData(new Vector3Int(x, y, 0), tile, Color.white,
            Matrix4x4.TRS(offset, Quaternion.identity, new Vector3(scale * flip, scale, 1)));
    }

    /// <summary>Hide deposits under buildings (and show them again when a building goes).</summary>
    public void RefreshCoverage(IEnumerable<RectInt> buildingAreas)
    {
        var covered = new HashSet<Vector2Int>();
        foreach (var area in buildingAreas)
        {
            for (int y = area.yMin; y < area.yMax; y++)
            {
                for (int x = area.xMin; x < area.xMax; x++)
                {
                    covered.Add(new Vector2Int(x, y));
                }
            }
        }
        // Only touch tiles whose state changed.
        var previously = new HashSet<Vector2Int>(hiddenDeposits);
        hiddenDeposits.Clear();
        hiddenDeposits.UnionWith(covered);
        foreach (var cell in covered)
        {
            if (!previously.Contains(cell))
            {
                RegionAt(cell.x, cell.y).deposits.SetTile(new Vector3Int(cell.x, cell.y, 0), null);
            }
        }
        foreach (var cell in previously)
        {
            if (!covered.Contains(cell))
            {
                var type = Map.RawDeposit(cell.x, cell.y);
                if (type != null && type.sprites.Count > 0 && Map.InBounds(cell.x, cell.y))
                {
                    RegionAt(cell.x, cell.y).deposits.SetTiles(new[] { DepositChange(cell.x, cell.y, type) }, true);
                }
            }
        }
    }

    // ---------- Level of detail ----------

    private void LateUpdate()
    {
        var cam = Camera.main;
        if (cam == null || Map == null)
        {
            return;
        }
        float size = cam.orthographicSize;
        // A little hysteresis so it doesn't flicker right at a threshold.
        Detail want = CurrentDetail;
        if (size > strategyZoom * (CurrentDetail == Detail.Strategy ? 0.9f : 1f)) want = Detail.Strategy;
        else if (size > overviewZoom * (CurrentDetail != Detail.Tiles ? 0.9f : 1f)) want = Detail.Overview;
        else want = Detail.Tiles;
        ApplyDetail(want, force: false);
    }

    private void ApplyDetail(Detail detail, bool force)
    {
        if (detail == CurrentDetail && !force)
        {
            return;
        }
        CurrentDetail = detail;
        bool tiles = detail == Detail.Tiles;
        groundRenderer.enabled = tiles;
        if (smoothTerrain)
        {
            foreach (var renderer in hillRenderers)
            {
                renderer.enabled = tiles;
            }
            waterRenderer.enabled = tiles;
        }
        foreach (var region in regions.Values)
        {
            region.depositRenderer.enabled = tiles;
            region.overviewRenderer.enabled = detail == Detail.Overview;
        }
        strategy.enabled = detail == Detail.Strategy;
    }

    // ---------- Smooth terrain ----------

    private void BuildSmoothTerrain(ArtLibrary art)
    {
        smoothTerrain = art != null && art.grassVariants.Count > 0 && art.hillShapes.Count == 16
            && art.waterFrameCount > 0 && art.waterFrames.Count == 16 * art.waterFrameCount;
        if (!smoothTerrain)
        {
            return;
        }
        grassVariants = art.grassVariants.ConvertAll(sprite => MakeTile(sprite, Color.white)).ToArray();
        foreach (var shapes in new[] { art.hillShapes, art.hillShapes2, art.hillShapes3 })
        {
            if (shapes.Count == 16)
            {
                hillTiers.Add(shapes.ConvertAll(sprite => sprite != null ? MakeTile(sprite, Color.white) : null).ToArray());
            }
        }
        hillFullVariants = art.hillFullVariants.ConvertAll(sprite => MakeTile(sprite, Color.white)).ToArray();
        waterShapes = new TileBase[16];
        for (int mask = 1; mask < 16; mask++)
        {
            var tile = ScriptableObject.CreateInstance<AnimatedTerrainTile>();
            tile.frames = art.waterFrames.GetRange(mask * art.waterFrameCount, art.waterFrameCount).ToArray();
            tile.framesPerSecond = art.waterFps;
            waterShapes[mask] = tile;
        }
        for (int tier = 0; tier < hillTiers.Count; tier++)
        {
            hillTilemaps.Add(MakeEdgeLayer("Hills " + (tier + 1), -26 + tier, out var renderer));
            hillRenderers.Add(renderer);
        }
        waterTilemap = MakeEdgeLayer("Water", -23, out waterRenderer);
        if (art.waterMaterial != null)
        {
            waterRenderer.sharedMaterial = art.waterMaterial;
        }
    }

    /// <summary>A tilemap shifted half a tile, so each of its cells sits where four tiles meet.</summary>
    private Tilemap MakeEdgeLayer(string name, int order, out TilemapRenderer renderer)
    {
        var go = new GameObject(name);
        go.transform.SetParent(tileGrid.transform, false);
        go.transform.localPosition = new Vector3(0.5f, 0.5f, 0) * cellSize;
        var tilemap = go.AddComponent<Tilemap>();
        renderer = go.AddComponent<TilemapRenderer>();
        renderer.sortingOrder = order;
        return tilemap;
    }

    private Tile GrassAt(int x, int y)
    {
        // Mostly the plain variant, with the detailed ones sprinkled in.
        float roll = Hash01(x, y, 7);
        if (roll < 0.55f || grassVariants.Length == 1)
        {
            return grassVariants[0];
        }
        return grassVariants[1 + Mathf.Min(grassVariants.Length - 2, (int)((roll - 0.55f) / 0.45f * (grassVariants.Length - 1)))];
    }

    /// <summary>
    /// How high each tile looks on the detailed map: 0 = not hill, 1 = hill, 2 = hill with hills
    /// all round it, 3 = deep inside a big hill area. Covers 'rect' grown by one tile each side.
    /// </summary>
    private int[] HillLevels(RectInt rect, out RectInt grown)
    {
        grown = new RectInt(rect.xMin - 1, rect.yMin - 1, rect.width + 2, rect.height + 2);
        var levels = new int[grown.width * grown.height];
        int i = 0;
        for (int y = grown.yMin; y < grown.yMax; y++)
        {
            for (int x = grown.xMin; x < grown.xMax; x++)
            {
                levels[i++] = HillLevel(x, y);
            }
        }
        return levels;
    }

    private int HillLevel(int x, int y) =>
        Map.RawTerrain(x, y) != TerrainType.Hill ? 0
        : !AllHill(x, y, 1) ? 1
        : !AllHill(x, y, 3) ? 2 : 3;

    private bool AllHill(int cx, int cy, int radius)
    {
        for (int y = cy - radius; y <= cy + radius; y++)
        {
            for (int x = cx - radius; x <= cx + radius; x++)
            {
                if (Map.RawTerrain(x, y) != TerrainType.Hill)
                {
                    return false;
                }
            }
        }
        return true;
    }

    /// <summary>Update the hill and water edge cells whose corners touch 'area'.</summary>
    private void DrawEdges(RectInt area)
    {
        var levels = HillLevels(area, out var grown);
        int w = area.width + 1, h = area.height + 1, n = w * h;
        var positions = new Vector3Int[n];
        var hills = new TileBase[hillTiers.Count][];
        for (int tier = 0; tier < hills.Length; tier++)
        {
            hills[tier] = new TileBase[n];
        }
        var water = new TileBase[n];
        int i = 0;
        for (int y = area.yMin - 1; y < area.yMax; y++)
        {
            for (int x = area.xMin - 1; x < area.xMax; x++)
            {
                int waterMask = 0;
                for (int c = 0; c < 4; c++)
                {
                    if (Map.RawTerrain(x + (c & 1), y + (c >> 1)) == TerrainType.Water) waterMask |= 1 << c;
                }
                positions[i] = new Vector3Int(x, y, 0);
                for (int tier = 0; tier < hills.Length; tier++)
                {
                    int mask = 0;
                    for (int c = 0; c < 4; c++)
                    {
                        int tx = x + (c & 1), ty = y + (c >> 1);
                        if (levels[(ty - grown.yMin) * grown.width + (tx - grown.xMin)] > tier) mask |= 1 << c;
                    }
                    hills[tier][i] = tier == 0 && mask == 15 && hillFullVariants.Length > 0 && Hash01(x, y, 13) < 0.5f
                        ? hillFullVariants[(int)(Hash01(x, y, 17) * hillFullVariants.Length) % hillFullVariants.Length]
                        : hillTiers[tier][mask];
                }
                water[i] = waterShapes[waterMask];
                i++;
            }
        }
        for (int tier = 0; tier < hills.Length; tier++)
        {
            hillTilemaps[tier].SetTiles(positions, hills[tier]);
        }
        waterTilemap.SetTiles(positions, water);
    }

    /// <summary>
    /// Paint one tile of the zoomed-out picture. Each pixel blends the four nearest tiles, so
    /// coasts, hills and forests come out as smooth shapes instead of blocks.
    /// </summary>
    private void PaintOverviewTile(Region region, int x, int y, int baseX, int baseY, Color32[] block)
    {
        int px = OverviewPixelsPerTile;
        for (int sy = 0; sy < px; sy++)
        {
            for (int sx = 0; sx < px; sx++)
            {
                // Position among tile centres: corners (ix, iy) .. (ix + 1, iy + 1).
                float cx = x + (sx + 0.5f) / px - 0.5f, cy = y + (sy + 0.5f) / px - 0.5f;
                int ix = Mathf.FloorToInt(cx), iy = Mathf.FloorToInt(cy);
                float u = cx - ix, v = cy - iy;
                float waterW = 0, hillW = 0, depositW = 0, bestDeposit = 0;
                DepositType deposit = null;
                for (int c = 0; c < 4; c++)
                {
                    int tx = ix + (c & 1), ty = iy + (c >> 1);
                    float weight = ((c & 1) == 1 ? u : 1 - u) * ((c >> 1) == 1 ? v : 1 - v);
                    var t = Map.RawTerrain(tx, ty);
                    if (t == TerrainType.Water) waterW += weight;
                    else if (t == TerrainType.Hill) hillW += weight;
                    var d = Map.RawDeposit(tx, ty);
                    if (d != null)
                    {
                        depositW += weight;
                        if (weight > bestDeposit)
                        {
                            bestDeposit = weight;
                            deposit = d;
                        }
                    }
                }
                Color32 color;
                if (waterW > 0.5f) color = WaterColor;
                else if (depositW > 0.5f) color = OverviewColor(deposit);
                else if (hillW > 0.5f) color = HillColor;
                else color = GrassColor;
                block[sy * px + sx] = color;
            }
        }
        region.overview.SetPixels32((x - baseX) * px, (y - baseY) * px, px, px, block);
    }

    // ---------- Helpers ----------

    private static Color32 OverviewColor(DepositType deposit)
    {
        switch (deposit.id)
        {
            case "Forest": return new Color32(46, 98, 48, 255);
            case "Berries": return new Color32(176, 52, 66, 255);
            case "Stone": return new Color32(196, 196, 202, 255);
            case "CopperOre": return new Color32(214, 128, 60, 255);
            default: return new Color32(230, 220, 120, 255);
        }
    }

    private static Tile MakeTile(Sprite sprite, Color fallback)
    {
        var tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = sprite != null ? sprite : SimpleSprites.Square;
        tile.color = sprite != null ? Color.white : fallback;
        return tile;
    }

    private float Hash01(int x, int y, int salt)
    {
        unchecked
        {
            uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)(Map.Seed * 83492791) ^ (uint)(salt * 2654435761u);
            h ^= h >> 13;
            h *= 0x5bd1e995;
            h ^= h >> 15;
            return (h & 0xFFFFFF) / 16777216f;
        }
    }

    /// <summary>All zone edges where explored land meets unexplored land (or the limit), in world space.</summary>
    private List<(Vector3 a, Vector3 b)> FrontierEdges()
    {
        var edges = new List<(Vector3, Vector3)>();
        int c = Map.ChunkSize;
        foreach (int index in Map.RevealedZoneIndices())
        {
            int zx = index % Map.ChunksX, zy = index / Map.ChunksX;
            Vector3 min = CellToWorld(new Vector2Int(zx * c, zy * c));
            Vector3 max = min + new Vector3(c, c, 0) * cellSize;
            if (Map.IsFrontier(zx, zy, -1, 0)) edges.Add((new Vector3(min.x + 0.5f, min.y), new Vector3(min.x + 0.5f, max.y)));
            if (Map.IsFrontier(zx, zy, 1, 0)) edges.Add((new Vector3(max.x - 0.5f, min.y), new Vector3(max.x - 0.5f, max.y)));
            if (Map.IsFrontier(zx, zy, 0, -1)) edges.Add((new Vector3(min.x, min.y + 0.5f), new Vector3(max.x, min.y + 0.5f)));
            if (Map.IsFrontier(zx, zy, 0, 1)) edges.Add((new Vector3(min.x, max.y - 0.5f), new Vector3(max.x, max.y - 0.5f)));
        }
        return edges;
    }

    /// <summary>A random walkable point on the edge of the explored land.</summary>
    public Vector3 RandomEdgePoint()
    {
        var edges = FrontierEdges();
        for (int attempt = 0; attempt < 60 && edges.Count > 0; attempt++)
        {
            var (a, b) = edges[Random.Range(0, edges.Count)];
            Vector3 p = Vector3.Lerp(a, b, Random.Range(0.05f, 0.95f));
            if (Map.IsWalkable(p))
            {
                return p;
            }
        }
        return MapRect.center;
    }

    /// <summary>The walkable point on the edge of the explored land nearest to 'from'.</summary>
    public Vector3 NearestEdgePoint(Vector3 from)
    {
        Vector3 best = MapRect.center;
        float bestDistance = float.MaxValue;
        foreach (var (a, b) in FrontierEdges())
        {
            Vector3 ab = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(from - a, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
            Vector3 p = a + ab * t;
            float d = (p - from).sqrMagnitude;
            if (d < bestDistance && Map.IsWalkable(p))
            {
                bestDistance = d;
                best = p;
            }
        }
        return bestDistance < float.MaxValue ? best : RandomEdgePoint();
    }

    /// <summary>Show the grid lines while placing a building.</summary>
    public void SetPlacing(bool placing)
    {
        if (gridLines != null)
        {
            gridLines.color = placing ? LinesPlacing : LinesIdle;
        }
    }

    public Vector3 CellToWorld(Vector2Int cell) =>
        transform.position + new Vector3(cell.x * cellSize, cell.y * cellSize, 0);

    /// <summary>Centre of an area that starts at 'cell' and is 'size' cells big.</summary>
    public Vector3 AreaCenter(Vector2Int cell, Vector2Int size) =>
        CellToWorld(cell) + new Vector3(size.x * cellSize, size.y * cellSize, 0) * 0.5f;

    public Vector2Int WorldToCell(Vector3 world)
    {
        Vector3 local = (world - transform.position) / cellSize;
        return new Vector2Int(Mathf.FloorToInt(local.x), Mathf.FloorToInt(local.y));
    }

    public static Vector3 MouseWorld()
    {
        Camera cam = Camera.main;
        Vector3 mouse = Input.mousePosition;
        mouse.z = -cam.transform.position.z;
        Vector3 world = cam.ScreenToWorldPoint(mouse);
        world.z = 0;
        return world;
    }
}
