using UnityEngine;

/// <summary>
/// Where the sea's buildings may stand: on the bank, by open sea, across the shoreline (stilt
/// houses, fish pens, tidal generators), out over the water (floating homes, kelp farms, oil rigs)
/// or along the shore (sea walls). Floating buildings must keep close to land or to another
/// floating building, so a town can grow out over an ocean world's sea a platform at a time.
/// Their quality is always 1: what the sea round them gives is worked out live (SeaSim.cs).
/// </summary>
public partial class Settlement
{
    /// <summary>CheckSite for the sea buildings (null for everything else, and for the rivers setup's docks).</summary>
    public bool? SeaCheckSite(BuildingDefinition def, Vector2Int cell, out float quality, out string reason)
    {
        quality = 1f;
        reason = null;
        if (Map == null || !SeaCatalog.TryGet(def, out var info) || info.site == SeaSite.River) return null;
        var area = new RectInt(cell, def.size);
        switch (info.site)
        {
            case SeaSite.WallLine:
            {
                if (!Map.CanBuildOn(area, true, out reason)) return false;
                if (!IsAreaFree(def, cell)) { reason = "Something is already built there"; return false; }
                if (CampAt(area) != null) { reason = "There's a raider camp here: clear it first"; return false; }
                if (!Map.AnyWater(area, 3)) { reason = "Sea walls go along the shore (within 3 tiles of the water)"; return false; }
                return true;
            }
            case SeaSite.Shore:
            case SeaSite.OpenShore:
            {
                if (!Map.CanBuildOn(area, false, out reason)) return false;
                if (!SeaAreaClear(def, cell, area, out reason)) return false;
                if (!RiverTouchesWater(area)) { reason = "Must stand on the bank, beside the water"; return false; }
                int need = info.site == SeaSite.Shore ? (info.role == SeaRole.Pearl ? 12 : 5) : info.role == SeaRole.Port ? 22 : info.role == SeaRole.Fishing ? 26 : 18;
                int open = RiverOpenWater(area, 4);
                if (open < need)
                {
                    reason = info.site == SeaSite.OpenShore ? $"Needs open sea beside it ({open}/{need} water tiles nearby)" : $"Needs more water nearby ({open}/{need} tiles)";
                    return false;
                }
                return true;
            }
            case SeaSite.Straddle:
            {
                int water = 0, land = 0;
                for (int y = area.yMin; y < area.yMax; y++)
                    for (int x = area.xMin; x < area.xMax; x++)
                    {
                        if (!Map.InBounds(x, y)) { reason = "Off the edge of the map"; return false; }
                        var t = Map.GetTerrain(x, y);
                        if (t == TerrainType.Hill) { reason = "Can't build on hills"; return false; }
                        if (Map.WallAt(x, y) != WallTile.None) { reason = "Something is already built there"; return false; }
                        if (t == TerrainType.Water) water++; else land++;
                    }
                if (water == 0 || land == 0) { reason = "Build it across the shoreline: partly on the bank, partly over the water"; return false; }
                if (!SeaAreaClear(def, cell, area, out reason)) return false;
                return true;
            }
            case SeaSite.Floating:
            case SeaSite.Offshore:
            {
                for (int y = area.yMin; y < area.yMax; y++)
                    for (int x = area.xMin; x < area.xMax; x++)
                    {
                        if (!Map.InBounds(x, y)) { reason = "Off the edge of the map"; return false; }
                        if (!RiverNav.Sailable(Map, x, y)) { reason = "Must stand wholly over open water"; return false; }
                    }
                if (!SeaAreaClear(def, cell, area, out reason)) return false;
                if (info.site == SeaSite.Offshore)
                {
                    int out_ = RiverDistanceFromBank(cell.x + def.size.x / 2, cell.y + def.size.y / 2, 3);
                    if (out_ < 2) { reason = "Too close to the shore: oil rigs stand out in deeper water (2 tiles or more out)"; return false; }
                    if (RiverOpenWater(area, 4) < 40) { reason = "Needs open sea round it"; return false; }
                }
                int reach = Mathf.Max(1, info.housingReach) + (SeaHas(SeaCatalog.TechStilts) ? 1 : 0);
                if (!SeaNearLandOrFloat(def, area, reach))
                {
                    reason = $"Too far out: keep within {reach} tile{(reach == 1 ? "" : "s")} of land or of another building on the water";
                    return false;
                }
                return true;
            }
        }
        return null;
    }

    private bool SeaAreaClear(BuildingDefinition def, Vector2Int cell, RectInt area, out string reason)
    {
        reason = null;
        if (!IsAreaFree(def, cell)) { reason = "Something is already built there"; return false; }
        if (CampAt(area) != null) { reason = "There's a raider camp here: clear it first"; return false; }
        if (SiteAt(area) != null) { reason = "Something old stands here"; return false; }
        return true;
    }

    /// <summary>Land, or one of your buildings standing on the water, within 'reach' tiles of the footprint.</summary>
    private bool SeaNearLandOrFloat(BuildingDefinition def, RectInt area, int reach)
    {
        var grown = new RectInt(area.xMin - reach, area.yMin - reach, area.width + reach * 2, area.height + reach * 2);
        for (int y = grown.yMin; y < grown.yMax; y++)
            for (int x = grown.xMin; x < grown.xMax; x++)
                if (Map.InBounds(x, y) && Map.GetTerrain(x, y) != TerrainType.Water) return true;
        foreach (var b in State.buildings)
        {
            var d = Database.GetBuilding(b.definitionId);
            if (d == null || d.IsWall || !SeaCatalog.TryGet(d, out var info)) continue;
            if (info.site != SeaSite.Floating && info.site != SeaSite.Offshore && info.site != SeaSite.Straddle) continue;
            if (new RectInt(b.cell, d.size).Overlaps(grown)) return true;
        }
        return false;
    }
}
