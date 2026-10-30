using UnityEngine;

/// <summary>
/// Maps settlement grid cells to world positions and draws the ground and grid.
/// Cell (0,0) is at this object's position; each cell is cellSize world units.
/// </summary>
public class SettlementGrid : MonoBehaviour
{
    public float cellSize = 1f;

    [Tooltip("How much open land surrounds the settlement before the map edge.")]
    public float mapMargin = 12f;

    [Tooltip("Scenery drawn beyond the map edge so the view never shows emptiness.")]
    public float sceneryBeyondEdge = 25f;

    public Vector2Int Size { get; private set; }

    /// <summary>The playable map: the settlement plus open land around it. Raiders enter at its edges.</summary>
    public Rect MapRect { get; private set; }

    /// <summary>Everything that gets grass and scenery (the map plus a strip beyond it).</summary>
    public Rect WorldArea { get; private set; }

    private SpriteRenderer gridLines;
    private static readonly Color LinesIdle = new Color(1, 1, 1, 0.25f);
    private static readonly Color LinesPlacing = new Color(1, 1, 1, 0.8f);

    public void Build(Vector2Int size)
    {
        Size = size;
        var art = ArtLibrary.Load();
        Vector2 gridWorld = new Vector2(size.x, size.y) * cellSize;
        Vector2 origin = transform.position;
        MapRect = new Rect(origin - Vector2.one * mapMargin, gridWorld + Vector2.one * mapMargin * 2);
        WorldArea = new Rect(MapRect.min - Vector2.one * sceneryBeyondEdge,
            MapRect.size + Vector2.one * sceneryBeyondEdge * 2);
        DrawMapEdge();

        // Wild grass everywhere
        var wild = Tiled("Wilds", art != null ? art.grassTile : null, new Color(0.3f, 0.45f, 0.25f), -22);
        wild.transform.position = new Vector3(WorldArea.x, WorldArea.y, 0);
        wild.size = WorldArea.size;

        // Settlement ground
        var ground = Tiled("Ground", art != null ? art.settlementTile : null, new Color(0.45f, 0.55f, 0.3f), -21);
        ground.size = gridWorld;

        // Border
        var border = Tiled("Border", null, new Color(0.2f, 0.15f, 0.08f, 0.6f), -21);
        border.transform.localPosition = new Vector3(-0.08f, -0.08f, 0.01f);
        border.size = gridWorld + Vector2.one * 0.16f;

        // Grid lines
        gridLines = new GameObject("GridLines").AddComponent<SpriteRenderer>();
        gridLines.transform.SetParent(transform, false);
        gridLines.sprite = SimpleSprites.GridCell;
        gridLines.drawMode = SpriteDrawMode.Tiled;
        gridLines.size = new Vector2(size.x, size.y);
        gridLines.transform.localScale = Vector3.one * cellSize;
        gridLines.sortingOrder = -19;
        SetPlacing(false);
    }

    /// <summary>Darken everything outside the map so its edge is clear.</summary>
    private void DrawMapEdge()
    {
        var shade = new Color(0.05f, 0.07f, 0.04f, 0.45f);
        Rect map = MapRect;
        Rect world = WorldArea;
        // Four strips around the map: below, above, left, right.
        Strip("EdgeBelow", new Rect(world.xMin, world.yMin, world.width, map.yMin - world.yMin), shade);
        Strip("EdgeAbove", new Rect(world.xMin, map.yMax, world.width, world.yMax - map.yMax), shade);
        Strip("EdgeLeft", new Rect(world.xMin, map.yMin, map.xMin - world.xMin, map.height), shade);
        Strip("EdgeRight", new Rect(map.xMax, map.yMin, world.xMax - map.xMax, map.height), shade);
        // A thin darker line right on the edge.
        var line = new Color(0.05f, 0.05f, 0.03f, 0.6f);
        const float w = 0.12f;
        Strip("LineBelow", new Rect(map.xMin, map.yMin - w, map.width, w), line);
        Strip("LineAbove", new Rect(map.xMin, map.yMax, map.width, w), line);
        Strip("LineLeft", new Rect(map.xMin - w, map.yMin - w, w, map.height + w * 2), line);
        Strip("LineRight", new Rect(map.xMax, map.yMin - w, w, map.height + w * 2), line);
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
        // Above scenery (-10), below buildings and units.
        sr.sortingOrder = -9;
    }

    /// <summary>A random point on the map's edge.</summary>
    public Vector3 RandomEdgePoint()
    {
        Rect m = MapRect;
        float perimeter = 2 * (m.width + m.height);
        float t = Random.Range(0f, perimeter);
        if (t < m.width) return new Vector3(m.xMin + t, m.yMin, 0);
        t -= m.width;
        if (t < m.height) return new Vector3(m.xMax, m.yMin + t, 0);
        t -= m.height;
        if (t < m.width) return new Vector3(m.xMax - t, m.yMax, 0);
        t -= m.width;
        return new Vector3(m.xMin, m.yMax - t, 0);
    }

    /// <summary>The point on the map's edge nearest to 'from'.</summary>
    public Vector3 NearestEdgePoint(Vector3 from)
    {
        Rect m = MapRect;
        float x = Mathf.Clamp(from.x, m.xMin, m.xMax);
        float y = Mathf.Clamp(from.y, m.yMin, m.yMax);
        float toLeft = x - m.xMin, toRight = m.xMax - x, toBottom = y - m.yMin, toTop = m.yMax - y;
        float min = Mathf.Min(Mathf.Min(toLeft, toRight), Mathf.Min(toBottom, toTop));
        if (min == toLeft) return new Vector3(m.xMin, y, 0);
        if (min == toRight) return new Vector3(m.xMax, y, 0);
        if (min == toBottom) return new Vector3(x, m.yMin, 0);
        return new Vector3(x, m.yMax, 0);
    }

    /// <summary>Make the grid lines stand out while placing a building.</summary>
    public void SetPlacing(bool placing)
    {
        if (gridLines != null)
        {
            gridLines.color = placing ? LinesPlacing : LinesIdle;
        }
    }

    private SpriteRenderer Tiled(string objectName, Sprite sprite, Color fallbackColor, int order)
    {
        var sr = new GameObject(objectName).AddComponent<SpriteRenderer>();
        sr.transform.SetParent(transform, false);
        sr.sprite = sprite != null ? sprite : SimpleSprites.Square;
        sr.color = sprite != null ? Color.white : fallbackColor;
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.sortingOrder = order;
        return sr;
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
