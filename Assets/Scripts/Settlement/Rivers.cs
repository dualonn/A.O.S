using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The rivers-and-sea buildings, by id: bridges and fords (placed in lines over water, like
/// walls), fishing docks (food from the water round them) and harbours (train boats and run a
/// ferry). The data assets come from Tools > Age of Stickman > Setup Rivers; this is what the
/// game needs to know about them beyond their BuildingDefinition.
/// </summary>
public static class RiverCatalog
{
    public enum Kind { None, Bridge, Fishing, Harbour }

    public struct BridgeInfo { public int reach; }
    public struct FishingInfo { public float fullFish; public int minWater; }
    public struct HarbourInfo { public int ferrySeats; public float ferrySpeed; public int tier; }

    /// <summary>Bridges: how far out from the bank (tiles) a piece can stand.</summary>
    public static readonly Dictionary<string, BridgeInfo> Bridges = new Dictionary<string, BridgeInfo>
    {
        { "RiverFord", new BridgeInfo { reach = 1 } },
        { "RiverBridgeWood", new BridgeInfo { reach = 3 } },
        { "RiverBridgeStone", new BridgeInfo { reach = 4 } },
        { "RiverBridgeIron", new BridgeInfo { reach = 6 } },
        { "RiverBridgeSteel", new BridgeInfo { reach = 8 } },
        { "RiverBridgeEnergy", new BridgeInfo { reach = 11 } },
    };

    /// <summary>Fishing: the catch (water tiles, shoals count double) for full output, and the least water to build at all.</summary>
    public static readonly Dictionary<string, FishingInfo> Fishing = new Dictionary<string, FishingInfo>
    {
        { "RiverFishingDock", new FishingInfo { fullFish = 20f, minWater = 5 } },
        { "RiverFishery", new FishingInfo { fullFish = 26f, minWater = 6 } },
        { "RiverCannery", new FishingInfo { fullFish = 32f, minWater = 8 } },
        { "RiverAquafarm", new FishingInfo { fullFish = 40f, minWater = 10 } },
    };

    /// <summary>Harbours: ferry seats and speed, and the age band of their boats (0 = reed rafts .. 5 = hover-barges).</summary>
    public static readonly Dictionary<string, HarbourInfo> Harbours = new Dictionary<string, HarbourInfo>
    {
        { "RiverRaftLanding", new HarbourInfo { ferrySeats = 3, ferrySpeed = 2.2f, tier = 0 } },
        { "RiverGalleyDock", new HarbourInfo { ferrySeats = 5, ferrySpeed = 2.5f, tier = 1 } },
        { "RiverLongshipYard", new HarbourInfo { ferrySeats = 6, ferrySpeed = 2.8f, tier = 2 } },
        { "RiverShipyard", new HarbourInfo { ferrySeats = 8, ferrySpeed = 3.0f, tier = 3 } },
        { "RiverSteamDockyard", new HarbourInfo { ferrySeats = 10, ferrySpeed = 3.6f, tier = 4 } },
        { "RiverHoverDock", new HarbourInfo { ferrySeats = 12, ferrySpeed = 4.5f, tier = 5 } },
    };

    public static Kind KindOf(BuildingDefinition def)
    {
        if (def == null || def.id == null || !def.id.StartsWith("River")) return Kind.None;
        if (def.wallKind == WallTile.Bridge || Bridges.ContainsKey(def.id)) return Kind.Bridge;
        if (Fishing.ContainsKey(def.id)) return Kind.Fishing;
        if (Harbours.ContainsKey(def.id)) return Kind.Harbour;
        return Kind.None;
    }

    public static bool IsBridge(BuildingDefinition def) => def != null && def.wallKind == WallTile.Bridge;

    /// <summary>Research ids and what they do (small global bonuses; units' boosts are on the Technology assets).</summary>
    public const string TechBridgeworks = "RiverBridgeworks", TechIronGirders = "RiverIronGirders", TechSuspension = "RiverSuspensionBridges",
        TechFerryCrews = "RiverFerryCrews", TechCharts = "RiverCharts";

    /// <summary>Which age band of boats raiders row in on (by your age index).</summary>
    public static int RaidBoatTier(int ageIndex) => ageIndex <= 2 ? 0 : ageIndex <= 6 ? 1 : ageIndex <= 9 ? 2 : 3;
}

/// <summary>Rivers and the sea: bridge, dock and harbour sites, fishing catches, cheaper bridges and island finds.</summary>
public partial class Settlement
{
    // ---------- Building sites ----------

    /// <summary>
    /// CheckSite for the river buildings (null for everything else). Bridges go on open water a
    /// short way out from the bank; docks and harbours on the shore.
    /// </summary>
    public bool? RiverCheckSite(BuildingDefinition def, Vector2Int cell, out float quality, out string reason)
    {
        quality = 1f;
        reason = null;
        var kind = RiverCatalog.KindOf(def);
        if (kind == RiverCatalog.Kind.None || Map == null) return null;
        var area = new RectInt(cell, def.size);
        if (kind == RiverCatalog.Kind.Bridge)
        {
            int x = cell.x, y = cell.y;
            if (!Map.InBounds(x, y)) { reason = "Off the edge of the map"; return false; }
            if (Map.GetTerrain(x, y) != TerrainType.Water) { reason = "Bridges go over water"; return false; }
            if (!IsAreaFree(def, cell)) { reason = "Something is already built there"; return false; }
            int reach = RiverBridgeReach(def);
            int out_ = RiverDistanceFromBank(x, y, reach + 1);
            if (out_ > reach)
            {
                reason = $"Too far from the bank for a {def.displayName} (up to {reach} tile{(reach == 1 ? "" : "s")} out)";
                return false;
            }
            return true;
        }
        if (!Map.CanBuildOn(area, def.canBuildOnHills, out reason)) return false;
        if (!IsAreaFree(def, cell)) { reason = "Something is already built there"; return false; }
        if (CampAt(area) != null) { reason = "There's a raider camp here: clear it first"; return false; }
        if (SiteAt(area) != null) { reason = "Something old stands here"; return false; }
        if (!RiverTouchesWater(area))
        {
            reason = kind == RiverCatalog.Kind.Harbour ? "Must stand on the shore, beside open water" : "Must stand on the bank, beside the water";
            return false;
        }
        if (kind == RiverCatalog.Kind.Harbour)
        {
            int open = RiverOpenWater(area, 4);
            if (open < 14) { reason = $"Needs open water for its boats ({open}/14 tiles nearby)"; return false; }
        }
        if (kind == RiverCatalog.Kind.Fishing)
        {
            var info = RiverCatalog.Fishing[def.id];
            int water = RiverOpenWater(area, def.catchmentRadius);
            if (water < info.minWater) { reason = $"Needs water to fish ({water}/{info.minWater} tiles nearby)"; return false; }
        }
        quality = RiverSiteQuality(def, area, def.catchmentRadius) ?? 1f;
        return true;
    }

    /// <summary>SiteQuality for fishing docks: the catch in their waters (null for everything else).</summary>
    public float? RiverSiteQuality(BuildingDefinition def, RectInt area, int radius)
    {
        if (RiverCatalog.KindOf(def) != RiverCatalog.Kind.Fishing || Map == null) return null;
        var info = RiverCatalog.Fishing[def.id];
        float fish = RiverCatch(area, radius);
        float q = Mathf.Clamp(fish / Mathf.Max(1f, info.fullFish), 0f, 1.25f);
        if (def.waterBonus > 0) q *= 1f + def.waterBonus;
        // Rich seas: ocean worlds fish best.
        if (Map.Settings?.profile != null && Map.Settings.profile.id == "Ocean") q *= 1.3f;
        return q;
    }

    /// <summary>How much fish the water round 'area' holds: a point a tile, two in a shoal, a little more out at sea.</summary>
    public float RiverCatch(RectInt area, int radius)
    {
        var c = MapData.Catchment(area, radius);
        float fish = 0f;
        float ox = (Map.Seed % 997) * 0.37f, oy = (Map.Seed % 787) * 0.53f;
        for (int y = c.yMin; y < c.yMax; y++)
            for (int x = c.xMin; x < c.xMax; x++)
            {
                if (!Map.InBounds(x, y) || Map.GetTerrain(x, y) != TerrainType.Water) continue;
                float f = 1f;
                if (Mathf.PerlinNoise(x * 0.17f + ox, y * 0.17f + oy) > 0.6f) f += 1f;   // a shoal
                if (Map.GetTerrain(x + 1, y) == TerrainType.Water && Map.GetTerrain(x - 1, y) == TerrainType.Water
                    && Map.GetTerrain(x, y + 1) == TerrainType.Water && Map.GetTerrain(x, y - 1) == TerrainType.Water) f += 0.25f;
                if (Map.WallAt(x, y) != WallTile.None) f *= 0.5f;   // under a bridge
                fish += f;
            }
        return fish;
    }

    /// <summary>Water tiles round 'area' within 'radius' (bridged ones too).</summary>
    public int RiverOpenWater(RectInt area, int radius)
    {
        var c = MapData.Catchment(area, radius);
        int n = 0;
        for (int y = c.yMin; y < c.yMax; y++)
            for (int x = c.xMin; x < c.xMax; x++)
                if (Map.InBounds(x, y) && Map.GetTerrain(x, y) == TerrainType.Water) n++;
        return n;
    }

    /// <summary>Is a tile right beside the footprint water?</summary>
    public bool RiverTouchesWater(RectInt area)
    {
        for (int y = area.yMin - 1; y <= area.yMax; y++)
            for (int x = area.xMin - 1; x <= area.xMax; x++)
            {
                if (area.Contains(new Vector2Int(x, y))) continue;
                if (Map.InBounds(x, y) && Map.GetTerrain(x, y) == TerrainType.Water && Map.WallAt(x, y) == WallTile.None) return true;
            }
        return false;
    }

    /// <summary>How many tiles out from dry land this water tile is (up to 'cap').</summary>
    public int RiverDistanceFromBank(int x, int y, int cap)
    {
        for (int r = 1; r <= cap; r++)
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                    int tx = x + dx, ty = y + dy;
                    if (Map.InBounds(tx, ty) && Map.GetTerrain(tx, ty) != TerrainType.Water) return r;
                }
        return cap + 1;
    }

    /// <summary>How far out a bridge of this kind reaches, with research.</summary>
    public int RiverBridgeReach(BuildingDefinition def)
    {
        int reach = def != null && RiverCatalog.Bridges.TryGetValue(def.id, out var b) ? b.reach : 1;
        if (reach <= 1) return reach;   // a ford is a ford
        if (State.researched.Contains(RiverCatalog.TechBridgeworks)) reach += 1;
        if (State.researched.Contains(RiverCatalog.TechIronGirders)) reach += 1;
        if (State.researched.Contains(RiverCatalog.TechSuspension)) reach += 2;
        return reach;
    }

    // ---------- Costs ----------

    private readonly Dictionary<string, List<ResourceAmount>> riverCosts = new Dictionary<string, List<ResourceAmount>>();
    private int riverCostResearch = -1;
    private SettlementState riverCostFor;

    /// <summary>What a building costs: bridges are cheaper with bridge research (everything else as listed).</summary>
    public List<ResourceAmount> RiverCostOf(BuildingDefinition def)
    {
        if (!RiverCatalog.IsBridge(def)) return def.requirements.cost;
        if (riverCostFor != State || riverCostResearch != State.researched.Count)
        {
            riverCosts.Clear();
            riverCostFor = State;
            riverCostResearch = State.researched.Count;
        }
        if (riverCosts.TryGetValue(def.id, out var cached)) return cached;
        float m = 1f;
        if (State.researched.Contains(RiverCatalog.TechBridgeworks)) m *= 0.85f;
        if (State.researched.Contains(RiverCatalog.TechIronGirders)) m *= 0.9f;
        var list = m >= 0.999f ? def.requirements.cost
            : def.requirements.cost.ConvertAll(c => new ResourceAmount { resource = c.resource, amount = Mathf.Max(1, Mathf.RoundToInt(c.amount * m)) });
        riverCosts[def.id] = list;
        return list;
    }

    // ---------- The water round your towns ----------

    /// <summary>Your harbours on this world (finished and standing).</summary>
    public List<BuildingState> RiverHarbours()
    {
        var list = new List<BuildingState>();
        foreach (var b in State.buildings)
        {
            var def = Database.GetBuilding(b.definitionId);
            if (RiverCatalog.KindOf(def) == RiverCatalog.Kind.Harbour && IsWorking(b, def)) list.Add(b);
        }
        return list;
    }

    /// <summary>Ferry seats for a harbour: its kind, plus one per upgrade, plus Ferry Crews research.</summary>
    public int RiverFerrySeats(BuildingState harbour)
    {
        var def = Database.GetBuilding(harbour.definitionId);
        if (def == null || !RiverCatalog.Harbours.TryGetValue(def.id, out var info)) return 0;
        int seats = info.ferrySeats + harbour.upgradeLevel;
        if (State.researched.Contains(RiverCatalog.TechFerryCrews)) seats += 2;
        return seats;
    }

    public float RiverFerrySpeed(BuildingState harbour)
    {
        var def = Database.GetBuilding(harbour.definitionId);
        if (def == null || !RiverCatalog.Harbours.TryGetValue(def.id, out var info)) return 2f;
        float speed = info.ferrySpeed;
        if (State.researched.Contains(RiverCatalog.TechFerryCrews)) speed *= 1.2f;
        if (State.researched.Contains(RiverCatalog.TechCharts)) speed *= 1.1f;
        return speed;
    }

    // ---------- Islands ----------

    /// <summary>
    /// Islands worth reaching: a patch of land the sea cuts off (found as its zones are explored)
    /// gets an old site of its own, a tier tougher and richer than the mainland's: a rich vein,
    /// ruins, a dungeon or a cache. Placed from the map seed and saved like other sites.
    /// </summary>
    public void RiverRollIslands(IEnumerable<Vector2Int> zones)
    {
        if (!IsFounded || Map == null) return;
        var settlements = SettlementZones();
        var seen = new HashSet<Vector2Int>();
        int c = Map.ChunkSize;
        foreach (var zone in zones)
        {
            if (!Map.IsZoneRevealed(zone.x, zone.y)) continue;
            var rect = Map.ZoneRect(zone.x, zone.y);
            // Only zones with water in them can hold (part of) an island.
            bool anyWater = false;
            for (int y = rect.yMin; y < rect.yMax && !anyWater; y += 2)
                for (int x = rect.xMin; x < rect.xMax; x += 2)
                    if (Map.GetTerrain(x, y) == TerrainType.Water) { anyWater = true; break; }
            if (!anyWater) continue;
            for (int y = rect.yMin; y < rect.yMax; y++)
                for (int x = rect.xMin; x < rect.xMax; x++)
                {
                    var start = new Vector2Int(x, y);
                    if (seen.Contains(start) || Map.GetTerrain(x, y) == TerrainType.Water || !Map.InBounds(x, y)) continue;
                    var island = RiverIsland(start, 700, seen);
                    if (island == null || island.Count < 14) continue;
                    RiverPlaceIslandSite(island, settlements);
                }
        }
    }

    /// <summary>The land tiles joined to 'start', or null if it runs on past 'limit' tiles or into unexplored land.</summary>
    private List<Vector2Int> RiverIsland(Vector2Int start, int limit, HashSet<Vector2Int> seen)
    {
        var tiles = new List<Vector2Int>();
        var queue = new Queue<Vector2Int>();
        var mine = new HashSet<Vector2Int> { start };
        queue.Enqueue(start);
        bool open = false;
        while (queue.Count > 0)
        {
            var t = queue.Dequeue();
            tiles.Add(t);
            if (tiles.Count > limit) { open = true; break; }
            for (int i = 0; i < 4; i++)
            {
                var n = t + (i == 0 ? Vector2Int.right : i == 1 ? Vector2Int.left : i == 2 ? Vector2Int.up : Vector2Int.down);
                if (mine.Contains(n)) continue;
                if (!Map.InBounds(n.x, n.y)) { open = true; continue; }   // runs off into the unknown: can't tell yet
                if (Map.GetTerrain(n.x, n.y) == TerrainType.Water) continue;
                mine.Add(n);
                queue.Enqueue(n);
            }
        }
        foreach (var t in mine) seen.Add(t);
        return open ? null : tiles;
    }

    private void RiverPlaceIslandSite(List<Vector2Int> island, List<SettlementZone> settlements)
    {
        // The island's own place in the seed: from its lowest-leftmost tile.
        var key = island[0];
        foreach (var t in island) if (t.y < key.y || (t.y == key.y && t.x < key.x)) key = t;
        var keyZone = Map.ZoneOf(key.x, key.y);
        foreach (var s in State.sites)
        {
            // Already has its find (or another site): nothing more.
            if (s.zoneXY == keyZone) return;
            foreach (var t in island) if (new RectInt(s.cell, Vector2Int.one * s.Size).Contains(t)) return;
        }
        if (State.camps.Exists(cp => cp.zoneXY == keyZone)) return;
        // Your own island: nothing wild to find.
        foreach (var t in island)
            foreach (var z in settlements)
                if (z.Contains(t + Vector2.one * 0.5f)) return;
        float roll = CampHash(keyZone, 901 + key.x * 7 + key.y * 13);
        var veins = VeinDeposits();
        var kind = roll < 0.32f && veins.Count > 0 ? SiteKind.Vein : roll < 0.62f ? SiteKind.Ruins : roll < 0.8f ? SiteKind.Dungeon : SiteKind.Cache;
        var site = new SiteState { kind = kind, zoneXY = keyZone, variant = (int)(CampHash(keyZone, 903) * 1000), extraTier = 1 };
        if (kind == SiteKind.Vein) site.veinId = veins[site.variant % veins.Count].id;
        int size = site.Size;
        var tileSet = new HashSet<Vector2Int>(island);
        // Try spots on the island (the same ones every time), nearest its middle first.
        Vector2 mid = Vector2.zero;
        foreach (var t in island) mid += (Vector2)t;
        mid /= island.Count;
        var spots = new List<Vector2Int>(island);
        spots.Sort((a, b) => ((Vector2)a - mid).sqrMagnitude.CompareTo(((Vector2)b - mid).sqrMagnitude));
        foreach (var cell in spots)
        {
            var area = new RectInt(cell, Vector2Int.one * size);
            bool fits = true;
            for (int y = area.yMin; y < area.yMax && fits; y++)
                for (int x = area.xMin; x < area.xMax; x++)
                {
                    var p = new Vector2Int(x, y);
                    if (!tileSet.Contains(p) || Map.GetTerrain(x, y) == TerrainType.Water
                        || (kind != SiteKind.Vein && Map.GetTerrain(x, y) == TerrainType.Hill)) { fits = false; break; }
                }
            if (!fits || CampAt(area) != null || SiteAt(area) != null) continue;
            bool built = false;
            foreach (var b in State.buildings)
            {
                var d = Database.GetBuilding(b.definitionId);
                if (d != null && new RectInt(b.cell, d.size).Overlaps(area)) { built = true; break; }
            }
            if (built) continue;
            site.cell = cell;
            site.zoneXY = Map.ZoneOf(cell.x, cell.y);
            if (State.sites.Exists(s => s.zoneXY == site.zoneXY)) return;
            State.sites.Add(site);
            return;
        }
    }
}
