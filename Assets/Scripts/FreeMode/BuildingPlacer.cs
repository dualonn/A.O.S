using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Placement mode: a ghost footprint follows the mouse, green when the building fits and is
/// affordable, red otherwise. Left click places (hold Shift to keep placing), right click or Esc cancels.
/// </summary>
public class BuildingPlacer : MonoBehaviour
{
    public BuildingDefinition Current { get; private set; }
    public bool IsPlacing => Current != null;

    private FreeModeController controller;
    private SpriteRenderer ghost;

    public void Init(FreeModeController owner)
    {
        controller = owner;
        ghost = new GameObject("PlacementGhost").AddComponent<SpriteRenderer>();
        ghost.transform.SetParent(transform, false);
        ghost.sprite = SimpleSprites.Square;
        ghost.sortingOrder = 10;
        ghost.enabled = false;
    }

    public void Begin(BuildingDefinition def)
    {
        Current = def;
        controller.Select(null);
    }

    public void Cancel()
    {
        Current = null;
        ghost.enabled = false;
    }

    private void Update()
    {
        if (Current == null)
        {
            return;
        }

        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
        {
            Cancel();
            return;
        }

        var grid = controller.Grid;
        var settlement = controller.Settlement;
        Vector2Int cell = CellUnderMouse(grid);

        bool fits = settlement.IsAreaFree(Current, cell);
        bool canBuild = settlement.CanBuild(Current, out _);

        ghost.enabled = true;
        ghost.transform.position = grid.CellToWorld(cell);
        ghost.transform.localScale = new Vector3(Current.size.x * grid.cellSize, Current.size.y * grid.cellSize, 1);
        ghost.color = fits && canBuild ? new Color(0.3f, 1f, 0.3f, 0.45f) : new Color(1f, 0.25f, 0.25f, 0.45f);

        if (Input.GetMouseButtonDown(0) && !IsPointerOverUI())
        {
            if (settlement.TryPlaceBuilding(Current, cell, out _, out string reason))
            {
                bool keepPlacing = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                if (!keepPlacing || !settlement.CanBuild(Current, out _))
                {
                    Cancel();
                }
            }
            else
            {
                controller.ShowMessage(reason.Split('\n')[0]);
            }
        }
    }

    /// <summary>Cell for the footprint's corner so the footprint is centred on the mouse.</summary>
    private Vector2Int CellUnderMouse(SettlementGrid grid)
    {
        Vector3 mouse = SettlementGrid.MouseWorld();
        Vector3 halfFootprint = new Vector3(Current.size.x - 1, Current.size.y - 1, 0) * grid.cellSize * 0.5f;
        return grid.WorldToCell(mouse - halfFootprint);
    }

    public static bool IsPointerOverUI() =>
        EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
}
