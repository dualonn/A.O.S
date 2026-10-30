using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Placement mode: a ghost of the building follows the mouse with its catchment area shown.
/// Green when the spot works (with the output it would give), red with the reason when not.
/// Left click places (hold Shift to keep placing), right click or Esc cancels.
/// </summary>
public class BuildingPlacer : MonoBehaviour
{
    public BuildingDefinition Current { get; private set; }
    public bool IsPlacing => Current != null;

    private FreeModeController controller;
    private SpriteRenderer ghost;
    private SpriteRenderer ghostSprite;
    private SpriteRenderer catchment;
    private TextMeshPro info;

    private static readonly Color Good = new Color(0.3f, 1f, 0.3f, 0.35f);
    private static readonly Color Bad = new Color(1f, 0.25f, 0.25f, 0.45f);

    public void Init(FreeModeController owner)
    {
        controller = owner;

        catchment = new GameObject("Catchment").AddComponent<SpriteRenderer>();
        catchment.transform.SetParent(transform, false);
        catchment.sprite = SimpleSprites.Square;
        catchment.sortingOrder = 9;
        catchment.enabled = false;

        ghost = new GameObject("PlacementGhost").AddComponent<SpriteRenderer>();
        ghost.transform.SetParent(transform, false);
        ghost.sprite = SimpleSprites.Square;
        ghost.sortingOrder = 10;
        ghost.enabled = false;

        // Preview of the building's art, drawn over the footprint tint.
        ghostSprite = new GameObject("PlacementPreview").AddComponent<SpriteRenderer>();
        ghostSprite.transform.SetParent(transform, false);
        ghostSprite.sortingOrder = 11;
        ghostSprite.color = new Color(1, 1, 1, 0.7f);
        ghostSprite.enabled = false;

        var infoObject = new GameObject("PlacementInfo");
        infoObject.transform.SetParent(transform, false);
        info = infoObject.AddComponent<TextMeshPro>();
        info.alignment = TextAlignmentOptions.Bottom;
        info.fontSize = 3.6f;
        info.outlineWidth = 0.25f;
        info.outlineColor = new Color32(0, 0, 0, 230);
        info.rectTransform.sizeDelta = new Vector2(14, 3);
        info.sortingOrder = 12;
        info.enabled = false;
    }

    public void Begin(BuildingDefinition def)
    {
        Current = def;
        controller.Select(null);
        controller.Grid.SetPlacing(true);
        if (def.icon != null)
        {
            ghostSprite.sprite = def.icon;
            Vector2 s = def.icon.bounds.size;
            ghostSprite.transform.localScale = new Vector3(def.size.x / s.x, def.size.y / s.y, 1);
        }
        ghostSprite.enabled = def.icon != null;
    }

    public void Cancel()
    {
        Current = null;
        ghost.enabled = false;
        ghostSprite.enabled = false;
        catchment.enabled = false;
        info.enabled = false;
        controller.Grid.SetPlacing(false);
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

        bool affordable = settlement.CanBuild(Current, out string missing);
        bool siteOk = settlement.CheckSite(Current, cell, out float quality, out string siteProblem);
        bool ok = affordable && siteOk;

        Vector3 corner = grid.CellToWorld(cell);
        ghost.enabled = true;
        ghost.transform.position = corner;
        ghost.transform.localScale = new Vector3(Current.size.x * grid.cellSize, Current.size.y * grid.cellSize, 1);
        ghost.color = ok ? Good : Bad;
        ghostSprite.transform.position = corner;

        // Catchment: the area this building gathers from.
        bool showCatchment = Current.requiredDeposit != null || Current.waterBonus > 0;
        catchment.enabled = showCatchment;
        if (showCatchment)
        {
            int r = Current.catchmentRadius;
            catchment.transform.position = corner - new Vector3(r, r, 0) * grid.cellSize;
            catchment.transform.localScale = new Vector3(Current.size.x + r * 2, Current.size.y + r * 2, 1) * grid.cellSize;
            catchment.color = ok ? new Color(1f, 0.95f, 0.5f, 0.14f) : new Color(1f, 0.4f, 0.3f, 0.12f);
        }

        info.enabled = true;
        info.transform.position = corner + new Vector3(Current.size.x * 0.5f, Current.size.y + 0.3f, 0) * grid.cellSize;
        info.text = !siteOk ? $"<color=#ff8080>{siteProblem}</color>"
            : !affordable ? $"<color=#ffb070>{missing.Split('\n')[0]}</color>"
            : SiteText(quality);

        if (Input.GetMouseButtonDown(0) && !IsPointerOverUI())
        {
            if (settlement.TryPlaceBuilding(Current, cell, out _, out string reason))
            {
                if (Current.isTownCenter)
                {
                    controller.FocusOn(grid.AreaCenter(cell, Current.size));
                }
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

    private string SiteText(float quality)
    {
        if (Current.requiredDeposit == null && Current.waterBonus <= 0)
        {
            return "<color=#a0ffa0>Click to build</color>";
        }
        string color = quality >= 0.99f ? "#a0ffa0" : quality >= 0.5f ? "#ffe080" : "#ffb070";
        string text = $"<color={color}>Output {quality:P0}</color>";
        if (Current.waterBonus > 0 && controller.Settlement.Map.AnyWater(
                new RectInt(CellUnderMouse(controller.Grid), Current.size), Current.catchmentRadius))
        {
            text += $" <color=#80c8ff>(+{Current.waterBonus:P0} water)</color>";
        }
        return text;
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
