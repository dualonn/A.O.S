using System.Collections.Generic;
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
            // Same as the placed building: native size, pivot on the footprint corner.
            ghostSprite.transform.localScale = Vector3.one * controller.Grid.cellSize;
        }
        ghostSprite.enabled = def.icon != null;
    }

    public void Cancel()
    {
        Current = null;
        lineStart = null;
        HideLineGhosts(0);
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

        if (Current.IsWall && UpdateWallLine(cell))
        {
            return;
        }

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
        info.transform.position = corner + new Vector3(Current.size.x * 0.5f, Current.ArtSize.y + 0.3f, 0) * grid.cellSize;
        info.text = !siteOk ? $"<color=#ff8080>{siteProblem}</color>"
            : !affordable ? $"<color=#ffb070>{missing.Split('\n')[0]}</color>"
            : SiteText(quality);

        if (Input.GetMouseButtonDown(0) && !IsPointerOverUI())
        {
            if (settlement.TryPlaceBuilding(Current, cell, out _, out string reason))
            {
                GameAudio.Play("place", grid.AreaCenter(cell, Current.size));
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
                GameAudio.UI("error");
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
        if (Current.requiredDeposit != null && Current.requiredDeposit.richVeins && Current.size == Vector2Int.one * 3)
        {
            // Mines snap onto a rich vein of their deposit when the mouse is over it.
            float best = 2.5f;
            Vector2Int? snap = null;
            foreach (var site in controller.Settlement.State.sites)
            {
                if (site.kind != SiteKind.Vein || site.veinId != Current.requiredDeposit.id) continue;
                float d = Vector2.Distance(site.Centre, mouse);
                if (d < best) { best = d; snap = site.cell; }
            }
            if (snap.HasValue) return snap.Value;
        }
        Vector3 halfFootprint = new Vector3(Current.size.x - 1, Current.size.y - 1, 0) * grid.cellSize * 0.5f;
        return grid.WorldToCell(mouse - halfFootprint);
    }

    // ---------- Walls: drag out a line ----------

    private Vector2Int? lineStart;
    private readonly List<SpriteRenderer> lineGhosts = new List<SpriteRenderer>();

    /// <summary>
    /// Walls are dragged out: press, drag along (it bends once, like an L) and release to build
    /// every piece you can afford. Returns true while a line is being dragged.
    /// </summary>
    private bool UpdateWallLine(Vector2Int cell)
    {
        if (lineStart == null)
        {
            if (Input.GetMouseButtonDown(0) && !IsPointerOverUI())
            {
                lineStart = cell;
            }
            else
            {
                return false; // just hovering: the normal single ghost
            }
        }

        var settlement = controller.Settlement;
        var grid = controller.Grid;
        var cells = LineCells(lineStart.Value, cell);
        int good = 0;
        for (int i = 0; i < cells.Count; i++)
        {
            bool ok = settlement.CheckSite(Current, cells[i], out _, out _);
            if (ok) good++;
            var g = LineGhost(i);
            g.transform.position = grid.CellToWorld(cells[i]);
            g.transform.localScale = Vector3.one * grid.cellSize;
            g.color = ok ? Good : Bad;
        }
        HideLineGhosts(cells.Count);
        ghost.enabled = false;
        ghostSprite.enabled = false;
        catchment.enabled = false;

        var each = Current.requirements.cost;
        var total = Settlement.SumCost(System.Linq.Enumerable.Repeat(each, good));
        bool afford = settlement.CanAfford(total);
        info.enabled = true;
        info.transform.position = grid.CellToWorld(cell) + new Vector3(0.5f, 1.6f, 0) * grid.cellSize;
        string costText = string.Join(", ", total.ConvertAll(c => $"{c.amount} {c.resource.displayName}"));
        info.text = $"<color={(afford ? "#a0ffa0" : "#ffb070")}>{good} pieces: {costText}</color>";

        if (!Input.GetMouseButtonUp(0))
        {
            return true;
        }
        // Release: build along the line, in order, while it's affordable.
        lineStart = null;
        HideLineGhosts(0);
        int placed = 0;
        string problem = null;
        foreach (var c in cells)
        {
            if (!settlement.CheckSite(Current, c, out _, out _)) continue;
            if (settlement.TryPlaceBuilding(Current, c, out _, out string reason)) placed++;
            else
            {
                problem = reason;
                break;
            }
        }
        if (placed > 0)
        {
            GameAudio.UI("place");
            controller.ShowMessage(problem == null ? $"Building {placed} {Current.displayName} pieces."
                : $"Building {placed} {Current.displayName} pieces. {FirstLine(problem)}");
        }
        else if (problem != null)
        {
            controller.ShowMessage(FirstLine(problem));
        }
        // Walls usually come in several runs: stay in placing mode until cancelled.
        return true;
    }

    private static string FirstLine(string text) => string.IsNullOrEmpty(text) ? "" : text.Split('\n')[0];

    /// <summary>
    /// Cells from a to b. Mostly-straight drags give a straight run; anything steeper gives a
    /// diagonal line (joined corner to corner) so walls can run at an angle.
    /// </summary>
    private static List<Vector2Int> LineCells(Vector2Int a, Vector2Int b)
    {
        var cells = new List<Vector2Int>();
        int dx = b.x - a.x, dy = b.y - a.y;
        int ax = Mathf.Abs(dx), ay = Mathf.Abs(dy);
        int major = Mathf.Max(ax, ay), minor = Mathf.Min(ax, ay);
        if (major == 0)
        {
            cells.Add(a);
            return cells;
        }
        if (minor * 3 < major)
        {
            // Nearly straight: snap to the row or column.
            for (int i = 0; i <= major; i++)
            {
                cells.Add(ax >= ay ? new Vector2Int(a.x + i * System.Math.Sign(dx), a.y) : new Vector2Int(a.x, a.y + i * System.Math.Sign(dy)));
            }
            return cells;
        }
        // At an angle: a line of tiles touching corner to corner.
        for (int i = 0; i <= major; i++)
        {
            float t = i / (float)major;
            cells.Add(new Vector2Int(a.x + Mathf.RoundToInt(dx * t), a.y + Mathf.RoundToInt(dy * t)));
        }
        return cells;
    }

    private SpriteRenderer LineGhost(int i)
    {
        while (lineGhosts.Count <= i)
        {
            var g = new GameObject("WallGhost").AddComponent<SpriteRenderer>();
            g.transform.SetParent(transform, false);
            g.sprite = SimpleSprites.Square;
            g.sortingOrder = 10;
            lineGhosts.Add(g);
        }
        lineGhosts[i].enabled = true;
        return lineGhosts[i];
    }

    private void HideLineGhosts(int from)
    {
        for (int i = from; i < lineGhosts.Count; i++) lineGhosts[i].enabled = false;
    }

    public static bool IsPointerOverUI() =>
        EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
}
