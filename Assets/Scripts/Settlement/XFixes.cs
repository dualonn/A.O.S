using UnityEngine;

/// <summary>
/// Goal counters that count across all your worlds. Measured on the world you happened to be on,
/// a goal set on one world could finish the moment you switched to a bigger one (or never).
/// </summary>
public partial class Settlement
{
    /// <summary>How far (in tiles) the furthest finished building is from its world's town centre, on any world.</summary>
    public int XFurthestAnyWorld()
    {
        float best = 0;
        foreach (var w in State.worlds)
        {
            var centre = w.buildings.Find(b => Database.GetBuilding(b.definitionId)?.isTownCenter == true);
            if (centre == null) continue;
            foreach (var b in w.buildings)
                if (b.buildProgress >= 1f) best = Mathf.Max(best, Vector2Int.Distance(b.cell, centre.cell));
        }
        return Mathf.FloorToInt(best);
    }

    /// <summary>Buildings working a rich vein's seam, on all your worlds.</summary>
    public int XVeinsWorkedAll()
    {
        int n = 0;
        foreach (var w in State.worlds)
            foreach (var site in w.sites)
            {
                if (site.kind != SiteKind.Vein || !site.done) continue;
                if (w.buildings.Exists(b => b.cell == site.cell && b.buildProgress >= 1f && Database.GetBuilding(b.definitionId)?.requiredDeposit?.id == site.veinId)) n++;
            }
        return n;
    }

    // The world ScanBiomes last scanned (its position in the explored list belongs to that world).
    private int xBiomeScanWorld = -1;

    /// <summary>Start the biome scan over when the world changes.</summary>
    private void XResetBiomeScan()
    {
        if (xBiomeScanWorld == State.currentWorld) return;
        xBiomeScanWorld = State.currentWorld;
        biomeScanned = 0;
    }
}
