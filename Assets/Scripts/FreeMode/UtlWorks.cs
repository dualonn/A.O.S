using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Aqueduct (conduit) pieces in the scene: dragged out in lines like walls, they join only other
/// conduits of the same kind (never walls, roads or bridges), with 16 pieces by which sides join
/// (N=1, E=2, S=4, W=8). Hooks called from the wall code (FreeModeController, BuildingView, BuildingPlacer).
/// </summary>
public static class UtlWorks
{
    /// <summary>Wall-mask hook: true if this tile is a conduit, with the sides that join it.</summary>
    public static bool ConduitMaskFor(FreeModeController c, Vector2Int cell, out int mask)
    {
        mask = 0;
        var here = c.WallAt(cell);
        if (here == null || !UtlCatalog.IsConduit(here.Definition)) return false;
        string id = here.Definition.id;
        bool Same(Vector2Int at)
        {
            var v = c.WallAt(at);
            return v != null && UtlCatalog.IsConduit(v.Definition) && v.Definition.id == id;
        }
        mask = (Same(cell + Vector2Int.up) ? 1 : 0) | (Same(cell + Vector2Int.right) ? 2 : 0)
             | (Same(cell + Vector2Int.down) ? 4 : 0) | (Same(cell + Vector2Int.left) ? 8 : 0);
        return true;
    }

    /// <summary>BuildingView hook: a conduit picks its piece from the mask. True if this is one.</summary>
    public static bool ConduitPiece(BuildingView view, int mask, SpriteRenderer body, bool force)
    {
        if (view == null || !UtlCatalog.IsConduit(view.Definition)) return false;
        if (!force || body == null) return true;
        var pieces = view.Definition.pieces;
        if (pieces.Count > 0) body.sprite = pieces[Mathf.Clamp(mask & 15, 0, pieces.Count - 1)];
        return true;
    }

    /// <summary>Line-placement hook: a conduit line at an angle gets a tile at each corner, so the water can flow round.</summary>
    public static void Staircase(BuildingDefinition def, List<Vector2Int> cells)
    {
        if (!UtlCatalog.IsConduit(def) || cells == null) return;
        for (int i = cells.Count - 1; i > 0; i--)
        {
            Vector2Int a = cells[i - 1], b = cells[i];
            if (a.x != b.x && a.y != b.y) cells.Insert(i, new Vector2Int(b.x, a.y));
        }
    }
}
