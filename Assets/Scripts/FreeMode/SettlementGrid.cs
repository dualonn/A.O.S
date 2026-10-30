using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Draws the generated free mode map and maps tiles to world positions.
/// Tile (0,0) is at this object's position; each tile is cellSize world units.
/// Ground (grass, water, hills) is a Tilemap; deposits are individual sprites so units
/// can walk in front of and behind them.
/// </summary>
public class SettlementGrid : MonoBehaviour
{
    public float cellSize = 1f;

    [Tooltip("Scenery drawn beyond the map edge so the view never shows emptiness.")]
    public float sceneryBeyondEdge = 25f;

    public Vector2Int Size { get; private set; }
    public MapData Map { get; private set; }

    /// <summary>The playable map. Raiders enter at its edges.</summary>
    public Rect MapRect { get; private set; }

    /// <summary>The map plus a strip of scenery beyond it.</summary>
    public Rect WorldArea { get; private set; }

    private SpriteRenderer gridLines;
    private static readonly Color LinesIdle = new Color(1, 1, 1, 0f);
    private static readonly Color LinesPlacing = new Color(1, 1, 1, 0.35f);

    private GameObject tileGrid;

    [Header("Zoomed-out overview (level of detail)")]
    [Tooltip("Above this camera zoom the detailed map is swapped for a simple pre-drawn overview.")]
    public float overviewZoom = 26f;

    private TilemapRenderer groundRenderer;
    private TilemapRenderer depositRenderer;
    private SpriteRenderer overview;
    private bool showingOverview;

    /// <summary>
    /// Bake the whole map into one small picture: a few pixels per tile, coloured by terrain
    /// and deposit, with a little pattern so forests and rocks still read as forests and rocks.
    /// </summary>
    private void BuildOverview(MapData map)
    {
        const int px = 4; // pixels per tile
        var tex = new Texture2D(map.Width * px, map.Height * px, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        var pixels = new Color32[tex.width * tex.height];
        var grass = new Color32(108, 153, 79, 255);
        var water = new Color32(69, 133, 204, 255);
        var hill = new Color32(143, 130, 110, 255);
        var rng = new System.Random(map.Seed);
        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                var t = map.GetTerrain(x, y);
                Color32 baseColor = t == TerrainType.Water ? water : t == TerrainType.Hill ? hill : grass;
                var deposit = map.GetDeposit(x, y);
                Color32 mark = deposit != null ? OverviewColor(deposit) : baseColor;
                for (int dy = 0; dy < px; dy++)
                {
                    for (int dx = 0; dx < px; dx++)
                    {
                        // Deposits: a blob in the tile centre over the terrain colour.
                        bool inMark = deposit != null && dx >= 1 && dx <= 2 + (rng.Next(2)) && dy >= 1 && dy <= 2 + (rng.Next(2));
                        pixels[(y * px + dy) * tex.width + x * px + dx] = inMark ? mark : baseColor;
                    }
                }
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();

        overview = new GameObject("Overview").AddComponent<SpriteRenderer>();
        overview.transform.SetParent(transform, false);
        overview.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.zero, px / cellSize);
        overview.sortingOrder = -20;
        overview.enabled = false;
    }

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

    private void LateUpdate()
    {
        var cam = Camera.main;
        if (cam == null || overview == null)
        {
            return;
        }
        // A little hysteresis so it doesn't flicker right at the threshold.
        bool want = showingOverview ? cam.orthographicSize > overviewZoom * 0.9f : cam.orthographicSize > overviewZoom;
        if (want == showingOverview)
        {
            return;
        }
        showingOverview = want;
        overview.enabled = want;
        groundRenderer.enabled = !want;
        depositRenderer.enabled = !want;
    }

    public void Build(MapData map)
    {
        Map = map;
        Size = new Vector2Int(map.Width, map.Height);
        var art = ArtLibrary.Load();
        Vector2 origin = transform.position;
        MapRect = new Rect(origin, new Vector2(map.Width, map.Height) * cellSize);
        WorldArea = new Rect(MapRect.min - Vector2.one * sceneryBeyondEdge, MapRect.size + Vector2.one * sceneryBeyondEdge * 2);

        // Grass beyond the edge
        var wild = Tiled("Beyond", art != null ? art.grassTile : null, new Color(0.3f, 0.45f, 0.25f), -23);
        wild.transform.position = new Vector3(WorldArea.x, WorldArea.y, 0);
        wild.size = WorldArea.size;

        BuildGround(map, art);
        BuildDeposits(map);
        BuildOverview(map);
        DrawMapEdge();

        gridLines = new GameObject("GridLines").AddComponent<SpriteRenderer>();
        gridLines.transform.SetParent(transform, false);
        gridLines.sprite = SimpleSprites.GridCell;
        gridLines.drawMode = SpriteDrawMode.Tiled;
        gridLines.size = new Vector2(map.Width, map.Height);
        gridLines.transform.localScale = Vector3.one * cellSize;
        gridLines.sortingOrder = -19;
        SetPlacing(false);
    }

    // ---------- Ground ----------

    private void BuildGround(MapData map, ArtLibrary art)
    {
        var gridObject = new GameObject("TileGrid");
        gridObject.transform.SetParent(transform, false);
        tileGrid = gridObject;
        var unityGrid = gridObject.AddComponent<Grid>();
        unityGrid.cellSize = new Vector3(cellSize, cellSize, 0);

        var tilemapObject = new GameObject("Ground");
        tilemapObject.transform.SetParent(gridObject.transform, false);
        var tilemap = tilemapObject.AddComponent<Tilemap>();
        var renderer = tilemapObject.AddComponent<TilemapRenderer>();
        renderer.sortingOrder = -21;
        groundRenderer = renderer;

        Tile grass = MakeTile(art != null ? art.grassTile : null, new Color(0.42f, 0.6f, 0.31f));
        Tile water = MakeTile(art != null ? art.waterTile : null, new Color(0.25f, 0.48f, 0.75f));
        Tile hill = MakeTile(art != null ? art.hillTile : null, new Color(0.55f, 0.5f, 0.42f));

        var positions = new Vector3Int[map.Width * map.Height];
        var tiles = new TileBase[positions.Length];
        int i = 0;
        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                positions[i] = new Vector3Int(x, y, 0);
                var t = map.GetTerrain(x, y);
                tiles[i] = t == TerrainType.Water ? water
                    : t == TerrainType.Hill ? hill
                    : grass;
                i++;
            }
        }
        tilemap.SetTiles(positions, tiles);
    }

    private static Tile MakeTile(Sprite sprite, Color fallback)
    {
        var tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = sprite != null ? sprite : SimpleSprites.Square;
        tile.color = sprite != null ? Color.white : fallback;
        return tile;
    }

    // ---------- Deposits ----------

    /// <summary>
    /// Deposits go on their own Tilemap (thousands of separate sprites were too slow).
    /// Drawn top rows first, so lower trees overlap the ones behind them.
    /// </summary>
    private void BuildDeposits(MapData map)
    {
        var tilemapObject = new GameObject("Deposits");
        tilemapObject.transform.SetParent(tileGrid.transform, false);
        depositTilemap = tilemapObject.AddComponent<Tilemap>();
        var renderer = tilemapObject.AddComponent<TilemapRenderer>();
        renderer.sortingOrder = -11;
        renderer.sortOrder = TilemapRenderer.SortOrder.TopLeft;
        renderer.mode = TilemapRenderer.Mode.Chunk;
        depositRenderer = renderer;

        var tileFor = new Dictionary<Sprite, Tile>();
        var rng = new System.Random(map.Seed ^ 0x5f3759df);
        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                var type = map.GetDeposit(x, y);
                if (type == null || type.sprites.Count == 0)
                {
                    continue;
                }
                var sprite = type.sprites[rng.Next(type.sprites.Count)];
                if (!tileFor.TryGetValue(sprite, out var tile))
                {
                    tile = ScriptableObject.CreateInstance<Tile>();
                    tile.sprite = sprite;
                    tile.flags = TileFlags.LockColor; // transform set per tile below
                    tileFor[sprite] = tile;
                }
                var cell = new Vector3Int(x, y, 0);
                // Jitter, size and mirroring so patches look natural, sized to about one tile.
                float scale = (0.6f + (float)rng.NextDouble() * 0.2f) / Mathf.Max(0.5f, sprite.bounds.size.x);
                float flip = rng.NextDouble() < 0.5 ? -1f : 1f;
                var offset = new Vector3(((float)rng.NextDouble() - 0.5f) * 0.4f, ((float)rng.NextDouble() - 0.5f) * 0.3f - 0.3f, 0);
                var matrix = Matrix4x4.TRS(offset, Quaternion.identity, new Vector3(scale * flip, scale, 1));
                depositTiles[new Vector2Int(x, y)] = (tile, matrix);
            }
        }
        ShowDeposits(null);
    }

    private Tilemap depositTilemap;
    private readonly Dictionary<Vector2Int, (Tile tile, Matrix4x4 matrix)> depositTiles =
        new Dictionary<Vector2Int, (Tile, Matrix4x4)>();
    private readonly HashSet<Vector2Int> hiddenDeposits = new HashSet<Vector2Int>();

    private void ShowDeposits(HashSet<Vector2Int> hide)
    {
        foreach (var pair in depositTiles)
        {
            bool hidden = hide != null && hide.Contains(pair.Key);
            var cell = new Vector3Int(pair.Key.x, pair.Key.y, 0);
            if (hidden)
            {
                depositTilemap.SetTile(cell, null);
            }
            else if (depositTilemap.GetTile(cell) == null)
            {
                depositTilemap.SetTile(cell, pair.Value.tile);
                depositTilemap.SetTransformMatrix(cell, pair.Value.matrix);
            }
        }
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
        if (covered.SetEquals(hiddenDeposits))
        {
            return;
        }
        hiddenDeposits.Clear();
        hiddenDeposits.UnionWith(covered);
        ShowDeposits(covered);
    }

    // ---------- Edge and helpers ----------

    /// <summary>Darken everything outside the map so its edge is clear.</summary>
    private void DrawMapEdge()
    {
        var shade = new Color(0.05f, 0.07f, 0.04f, 0.55f);
        Rect map = MapRect;
        Rect world = WorldArea;
        Strip("EdgeBelow", new Rect(world.xMin, world.yMin, world.width, map.yMin - world.yMin), shade);
        Strip("EdgeAbove", new Rect(world.xMin, map.yMax, world.width, world.yMax - map.yMax), shade);
        Strip("EdgeLeft", new Rect(world.xMin, map.yMin, map.xMin - world.xMin, map.height), shade);
        Strip("EdgeRight", new Rect(map.xMax, map.yMin, world.xMax - map.xMax, map.height), shade);
    }

    private void Strip(string objectName, Rect rect, Color color)
    {
        var sr = new GameObject(objectName).AddComponent<SpriteRenderer>();
        sr.transform.SetParent(transform, true);
        sr.transform.position = new Vector3(rect.x, rect.y, 0);
        sr.sprite = SimpleSprites.Square;
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.size = rect.size;
        sr.color = color;
        sr.sortingOrder = -9;
    }

    private SpriteRenderer Tiled(string objectName, Sprite sprite, Color fallbackColor, int order)
    {
        var sr = new GameObject(objectName).AddComponent<SpriteRenderer>();
        sr.transform.SetParent(transform, false);
        sr.sprite = sprite != null ? sprite : SimpleSprites.Square;
        sr.color = sprite != null ? new Color(0.75f, 0.8f, 0.75f) : fallbackColor;
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.sortingOrder = order;
        return sr;
    }

    /// <summary>A random walkable point on the map's edge.</summary>
    public Vector3 RandomEdgePoint()
    {
        Rect m = MapRect;
        for (int attempt = 0; attempt < 50; attempt++)
        {
            float perimeter = 2 * (m.width + m.height);
            float t = Random.Range(0f, perimeter);
            Vector3 p;
            if (t < m.width) p = new Vector3(m.xMin + t, m.yMin + 0.5f, 0);
            else if ((t -= m.width) < m.height) p = new Vector3(m.xMax - 0.5f, m.yMin + t, 0);
            else if ((t -= m.height) < m.width) p = new Vector3(m.xMax - t, m.yMax - 0.5f, 0);
            else p = new Vector3(m.xMin + 0.5f, m.yMax - (t - m.width), 0);
            if (Map == null || Map.IsWalkable(p))
            {
                return p;
            }
        }
        return MapRect.center;
    }

    /// <summary>The walkable point on the map's edge nearest to 'from'.</summary>
    public Vector3 NearestEdgePoint(Vector3 from)
    {
        Rect m = MapRect;
        float x = Mathf.Clamp(from.x, m.xMin + 0.5f, m.xMax - 0.5f);
        float y = Mathf.Clamp(from.y, m.yMin + 0.5f, m.yMax - 0.5f);
        float toLeft = x - m.xMin, toRight = m.xMax - x, toBottom = y - m.yMin, toTop = m.yMax - y;
        float min = Mathf.Min(Mathf.Min(toLeft, toRight), Mathf.Min(toBottom, toTop));
        Vector3 p = min == toLeft ? new Vector3(m.xMin + 0.5f, y, 0)
            : min == toRight ? new Vector3(m.xMax - 0.5f, y, 0)
            : min == toBottom ? new Vector3(x, m.yMin + 0.5f, 0)
            : new Vector3(x, m.yMax - 0.5f, 0);
        if (Map != null && !Map.IsWalkable(p))
        {
            return RandomEdgePoint();
        }
        return p;
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
