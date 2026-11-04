using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Roads and rail on the map. Road and rail tiles are wall pieces that everyone walks over (like
/// a bridge on land): roads speed up anyone on them, rail a little (sleepers are awkward to walk
/// on), and trains run on rail. Paths prefer both. The tiles themselves are buildings (Setup Caravans).
/// </summary>
public static class TradeRoads
{
    /// <summary>How much faster units and caravans go on a road, and walkers on rail.</summary>
    public const float RoadSpeed = 1.4f, RailWalkSpeed = 1.15f;

    public static bool IsRoadTile(WallTile w) => w == WallTile.Road || w == WallTile.Rail;

    /// <summary>Movement speed factor on a tile: roads and rail first, then hills.</summary>
    public static float Speed(MapData map, int x, int y, float hillSpeed)
    {
        var w = map.WallAt(x, y);
        if (w == WallTile.Road) return RoadSpeed;
        if (w == WallTile.Rail) return RailWalkSpeed;
        return map.GetTerrain(x, y) == TerrainType.Hill ? hillSpeed : 1f;
    }
}
