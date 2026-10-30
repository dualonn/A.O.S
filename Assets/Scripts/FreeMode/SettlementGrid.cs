using UnityEngine;

/// <summary>
/// Maps settlement grid cells to world positions and draws the grid.
/// Cell (0,0) is at this object's position; each cell is cellSize world units.
/// </summary>
public class SettlementGrid : MonoBehaviour
{
    public float cellSize = 1f;
    public Color groundColor = new Color(0.30f, 0.45f, 0.25f);

    public Vector2Int Size { get; private set; }

    public void Build(Vector2Int size)
    {
        Size = size;

        var ground = new GameObject("Ground").AddComponent<SpriteRenderer>();
        ground.transform.SetParent(transform, false);
        ground.sprite = SimpleSprites.Square;
        ground.color = groundColor;
        ground.transform.localScale = new Vector3(size.x * cellSize, size.y * cellSize, 1);
        ground.sortingOrder = -20;

        var lines = new GameObject("GridLines").AddComponent<SpriteRenderer>();
        lines.transform.SetParent(transform, false);
        lines.sprite = SimpleSprites.GridCell;
        lines.drawMode = SpriteDrawMode.Tiled;
        lines.size = new Vector2(size.x, size.y);
        lines.transform.localScale = Vector3.one * cellSize;
        lines.sortingOrder = -19;
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
