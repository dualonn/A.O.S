using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Sea routes: a port in two of your towns (on water that joins up) makes a route, the sea's answer
/// to the trading posts' roads (TradeRoutes.cs). Merchant ships sail to and fro; each trip home
/// brings coin and goods, more for long routes between big towns, and ships carry far more than
/// wagons. Pirates prey on them (from the Medieval Age on); lighthouses over a port and warships
/// in the harbours keep them safer. On the world you're looking at the ships sail the map
/// (SeaWorks) and pirates attack for real; elsewhere the trips run on timers.
/// </summary>
public partial class Settlement
{
    public const float SeaRouteMaxDistance = 240f;
    public const float SeaLostSeconds = 60f;

    public List<SeaRoute> SeaRoutesHere => Sea.routes;

    // Routes cut off: when to look for a way again (not saved: worked out again after a reload).
    private readonly Dictionary<int, float> seaRetryAt = new Dictionary<int, float>();
    private readonly Dictionary<int, int> seaPathWalls = new Dictionary<int, int>();

    private struct SeaPortEntry { public BuildingState b; public int tier, zone, slots; public Vector2 centre; }

    private void SeaTickRoutes(SeaWorldState sea, SeaWorldCache cache, float dt)
    {
        sea.relinkIn -= dt;
        if (sea.relinkIn <= 0f)
        {
            sea.relinkIn = 5f;
            SeaRelink(sea);
            SeaFindOnePath(sea);
        }
        if (sea.routes.Count == 0) return;
        if (!offWorld && SeaWorks.Instance != null && SeaWorks.Instance.DrivesShips) return;   // ships on the map make the trips
        foreach (var r in sea.routes)
        {
            if (r.blocked || !r.pathKnown || State.playTime < r.lostUntil) continue;
            r.progress += dt / Mathf.Max(6f, SeaTripSeconds(r));
            if (r.progress < 1f) continue;
            r.progress = 0f;
            r.toB = !r.toB;
            if (offWorld && Random.value < SeaPirateRisk(r)) { SeaShipLost(r, "Pirates"); continue; }
            SeaTripDone(r);
        }
    }

    private void SeaRelink(SeaWorldState sea)
    {
        var zones = SettlementZones();
        var ports = new List<SeaPortEntry>();
        foreach (var b in State.buildings)
        {
            var def = Database.GetBuilding(b.definitionId);
            if (!SeaCatalog.TryGet(def, out var info) || info.role != SeaRole.Port || !IsWorking(b, def) || b.Paused) continue;
            var centre = b.cell + (Vector2)def.size * 0.5f;
            int zone = TradeZoneOf(zones, centre);
            if (zone < 0 || zones[zone].IsOutpost) continue;   // a port trades for a town
            int tier = Mathf.Clamp(info.tier, 0, SeaCatalog.ShipTierCount - 1);
            int slots = Mathf.Min(SeaCatalog.MaxPortRoutes, SeaCatalog.PortRoutes[tier] + Mathf.Min(3, b.upgradeLevel));
            ports.Add(new SeaPortEntry { b = b, tier = tier, zone = zone, slots = slots, centre = centre });
        }
        var pairs = new List<(int i, int j, float d)>();
        for (int i = 0; i < ports.Count; i++)
            for (int j = i + 1; j < ports.Count; j++)
            {
                if (ports[i].zone == ports[j].zone) continue;
                float d = Vector2.Distance(ports[i].centre, ports[j].centre);
                if (d <= SeaRouteMaxDistance) pairs.Add((i, j, d));
            }
        pairs.Sort((x, y) => x.d.CompareTo(y.d));
        var free = new int[ports.Count];
        for (int i = 0; i < ports.Count; i++) free[i] = ports[i].slots;
        var townPairs = new HashSet<long>();
        var keep = new List<SeaRoute>();
        foreach (var (i, j, _) in pairs)
        {
            if (free[i] <= 0 || free[j] <= 0) continue;
            int za = Mathf.Min(ports[i].zone, ports[j].zone), zb = Mathf.Max(ports[i].zone, ports[j].zone);
            if (!townPairs.Add(((long)za << 32) | (uint)zb)) continue;
            int a = ports[i].b.instanceId, b = ports[j].b.instanceId;
            var route = sea.routes.Find(r => (r.a == a && r.b == b) || (r.a == b && r.b == a));
            // A pair already known to have no way over the water doesn't use up a slot.
            if (route != null && route.pathKnown && route.blocked) { keep.Add(route); continue; }
            free[i]--; free[j]--;
            if (route == null) route = new SeaRoute { id = sea.nextId++, a = a, b = b };
            route.sizeA = zones[route.a == a ? ports[i].zone : ports[j].zone].buildings.Count;
            route.sizeB = zones[route.a == a ? ports[j].zone : ports[i].zone].buildings.Count;
            if (route.path == null) route.path = new List<Vector2>();
            keep.Add(route);
        }
        sea.routes.Clear();
        sea.routes.AddRange(keep);
    }

    /// <summary>Work out the way over the water for one route that needs it (one a relink: it's a long search).</summary>
    private void SeaFindOnePath(SeaWorldState sea)
    {
        foreach (var r in sea.routes)
        {
            // A known way that a new bridge now cuts: look again.
            if (r.pathKnown && !r.blocked && (!seaPathWalls.TryGetValue(r.id, out int walls) || walls != Map.WallVersion))
            {
                seaPathWalls[r.id] = Map.WallVersion;
                if (SeaPathCut(r)) r.pathKnown = false;
            }
            bool due = !r.pathKnown || (r.blocked && (!seaRetryAt.TryGetValue(r.id, out float at) || Time.unscaledTime >= at));
            if (!due) continue;
            SeaWorkOutPath(r);
            return;
        }
    }

    private bool SeaPathCut(SeaRoute r)
    {
        for (int i = 0; i + 1 < r.path.Count; i++)
        {
            float d = Vector2.Distance(r.path[i], r.path[i + 1]);
            int n = Mathf.Max(1, Mathf.CeilToInt(d));
            for (int k = 0; k <= n; k++)
                if (!RiverNav.Sailable(Map, Vector2.Lerp(r.path[i], r.path[i + 1], k / (float)n))) return true;
        }
        return false;
    }

    private void SeaWorkOutPath(SeaRoute r)
    {
        var pa = TradePost(r.a); var pb = TradePost(r.b);
        r.pathKnown = true;
        seaPathWalls[r.id] = Map.WallVersion;
        seaRetryAt[r.id] = Time.unscaledTime + 60f;
        r.path.Clear();
        r.length = 0f;
        if (pa == null || pb == null || !SeaPortWater(pa, out var wa) || !SeaPortWater(pb, out var wb)) { r.blocked = true; return; }
        var found = RiverNav.FindPath(Map, wa, wb, out bool reached, 60000);
        r.blocked = !reached;
        if (!reached) return;
        r.path.Add(wa);
        foreach (var p in found) r.path.Add(p);
        for (int i = 0; i + 1 < r.path.Count; i++) r.length += Vector2.Distance(r.path[i], r.path[i + 1]);
        if (!offWorld) Notify($"A sea route opens between {TradeTownName(r.a)} and {TradeTownName(r.b)} ({r.length:0} tiles by sea).");
    }

    /// <summary>The open water a port's ships tie up at.</summary>
    public bool SeaPortWater(BuildingState port, out Vector3 water)
    {
        water = default;
        var def = Database.GetBuilding(port.definitionId);
        if (def == null || Map == null) return false;
        var centre = (Vector3)(port.cell + (Vector2)def.size * 0.5f);
        return RiverNav.NearestSailable(Map, centre, 7, out water);
    }

    // ---------- What a route does ----------

    public int SeaShipTier(SeaRoute r)
    {
        int Tier(int id)
        {
            var p = TradePost(id);
            var def = p != null ? Database.GetBuilding(p.definitionId) : null;
            return SeaCatalog.TryGet(def, out var info) ? info.tier : 0;
        }
        return Mathf.Clamp(Mathf.Min(Tier(r.a), Tier(r.b)), 0, SeaCatalog.ShipTierCount - 1);
    }

    public float SeaShipSpeed(int tier) => SeaCatalog.ShipSpeed[Mathf.Clamp(tier, 0, SeaCatalog.ShipTierCount - 1)]
        * (SeaHas(SeaCatalog.TechReckoning) ? 1.1f : 1f) * (SeaHas(SeaCatalog.TechSteamPackets) ? 1.1f : 1f);

    public int SeaShipHealth(int tier) => Mathf.RoundToInt(SeaCatalog.ShipHealth[Mathf.Clamp(tier, 0, SeaCatalog.ShipTierCount - 1)]
        * (SeaHas(SeaCatalog.TechInsurance) ? 1.15f : 1f));

    private float SeaRouteLength(SeaRoute r)
    {
        if (r.length > 0f) return r.length;
        var pa = TradePost(r.a); var pb = TradePost(r.b);
        return pa != null && pb != null ? Vector2.Distance(pa.cell, pb.cell) * 1.3f : 30f;
    }

    public float SeaTripSeconds(SeaRoute r) => SeaRouteLength(r) / Mathf.Max(0.1f, SeaShipSpeed(SeaShipTier(r))) + 6f;

    private bool SeaRouteLit(SeaRoute r)
    {
        var cache = SeaCache;
        var pa = TradePost(r.a); var pb = TradePost(r.b);
        return (pa != null && SeaLitTier(cache, pa.cell) >= 0) || (pb != null && SeaLitTier(cache, pb.cell) >= 0);
    }

    private float SeaResearchFactor =>
        1f + (SeaHas(SeaCatalog.TechCogs) ? 0.1f : 0f) + (SeaHas(SeaCatalog.TechContainers) ? 0.1f : 0f) + (SeaHas(SeaCatalog.TechCoastal) ? 0.05f : 0f);

    /// <summary>What one trip is worth, in coin.</summary>
    public float SeaTripValue(SeaRoute r)
    {
        int tier = SeaShipTier(r);
        float length = Mathf.Clamp(SeaRouteLength(r), 10f, 300f);
        float size = 0.6f + 0.04f * Mathf.Min(60, r.sizeA + r.sizeB);
        float ports = (TradePostOutput(r.a) + TradePostOutput(r.b)) * 0.5f;
        float lit = SeaRouteLit(r) ? 1.1f : 1f;
        return 0.3f * length * SeaCatalog.ShipCapacity[tier] * size * ports * lit * SeaResearchFactor * (OnOceanWorld ? 1.15f : 1f);
    }

    /// <summary>A ship got home: coin, and goods from the other town. Returns a short note.</summary>
    public string SeaTripDone(SeaRoute r)
    {
        var sea = Sea;
        float value = SeaTripValue(r);
        bool coinOpen = IsResourceUnlocked("Coin") && Database.coin != null;
        float coin = coinOpen ? value * 0.75f : 0f;
        float goodsValue = coinOpen ? value * 0.25f : value * 0.8f;
        string note = "";
        if (coin > 0f) { AddResource(Database.coin, coin); note = $"+{coin:0} {Database.coin.displayName}"; }
        var goods = XMarketGoods();
        if (goods.Count > 0 && goodsValue > 0f)
        {
            var good = goods[(r.trips * 3 + r.id) % goods.Count];
            float amount = goodsValue / Mathf.Max(0.1f, XBaseValue(good));
            AddResource(good, amount);
            note += (note.Length > 0 ? ", " : "") + $"+{amount:0} {good.displayName}";
            r.goods += goodsValue;
        }
        r.trips++;
        r.coin += coin;
        sea.trips++;
        sea.coin += coin;
        return note;
    }

    /// <summary>A ship was lost (to pirates or the deep): Marine Insurance pays back a share; another sails after a while.</summary>
    public void SeaShipLost(SeaRoute r, string by)
    {
        var sea = Sea;
        r.lostUntil = State.playTime + SeaLostSeconds;
        r.progress = 0f;
        r.plundered++;
        sea.plundered++;
        string insured = "";
        if (SeaHas(SeaCatalog.TechInsurance) && Database.coin != null && IsResourceUnlocked("Coin"))
        {
            float back = SeaTripValue(r) * 0.4f;
            AddResource(Database.coin, back);
            insured = $" The underwriters paid {back:0} coin.";
        }
        Notify($"{by} took a {SeaCatalog.ShipNames[SeaShipTier(r)].ToLowerInvariant()} between {TradeTownName(r.a)} and {TradeTownName(r.b)}.{insured} Another sails in {SeaLostSeconds:0}s.");
    }

    /// <summary>The chance pirates take a ship on one trip, out of sight (none before the Medieval Age).</summary>
    public float SeaPirateRisk(SeaRoute r)
    {
        if (State.ageIndex < 4) return 0f;
        float risk = 0.06f;
        if (SeaRouteLit(r)) risk *= 0.5f;
        risk /= 1f + 0.4f * SeaCache.harbours;
        if (SeaCache.monsterNear) risk += 0.05f;
        return Mathf.Clamp(risk, 0f, 0.2f);
    }

    public float SeaIncomePerMinute()
    {
        float total = 0f;
        foreach (var r in SeaRoutesHere)
            if (!r.blocked && r.pathKnown) total += SeaTripValue(r) * 60f / Mathf.Max(6f, SeaTripSeconds(r));
        return total;
    }

    public string SeaRouteName(SeaRoute r) => $"{TradeTownName(r.a)} - {TradeTownName(r.b)}";

    public string SeaRouteStatus(SeaRoute r)
    {
        if (!r.pathKnown) return "<color=#aaa>Charting the way...</color>";
        if (r.blocked) return "<color=#f88>Cut off: no way over the water between the two ports</color>";
        if (State.playTime < r.lostUntil) return $"<color=#fb7>Ship lost: another sails in {r.lostUntil - State.playTime:0}s</color>";
        return $"<color=#9df>At sea, bound for {(r.toB ? TradeTownName(r.b) : TradeTownName(r.a))} ({r.progress:P0})</color>";
    }

    public string SeaRouteTooltip(SeaRoute r)
    {
        int tier = SeaShipTier(r);
        var sb = new StringBuilder($"<b>{SeaRouteName(r)}</b> <size=80%>(by sea)</size>\n");
        sb.Append($"{SeaCatalog.ShipNames[tier]}  ·  {SeaRouteLength(r):0} tiles\n");
        sb.Append($"Each trip: about {SeaTripValue(r):0} coin worth, every {SeaTripSeconds(r):0}s\n");
        sb.Append($"Towns: {r.sizeA} and {r.sizeB} buildings");
        if (SeaRouteLit(r)) sb.Append("  ·  <color=#fe9>lit by a lighthouse (+10%, safer)</color>");
        sb.Append('\n');
        float risk = SeaPirateRisk(r);
        if (risk > 0f) sb.Append($"Pirates: {(risk < 0.03f ? "<color=#8f8>low risk</color>" : risk < 0.06f ? "<color=#fe8>some risk</color>" : "<color=#f88>high risk</color>")} <size=80%><color=#aaa>(lighthouses and warships keep them off)</color></size>\n");
        sb.Append($"So far: {r.trips} trips, {r.coin:N0} coin, goods worth {r.goods:N0}");
        if (r.plundered > 0) sb.Append($", {r.plundered} lost");
        sb.Append('\n').Append(SeaRouteStatus(r));
        return sb.ToString();
    }

    private void SeaPortInfo(StringBuilder sb, BuildingState state, SeaBuilding info)
    {
        int tier = Mathf.Clamp(info.tier, 0, SeaCatalog.ShipTierCount - 1);
        int slots = Mathf.Min(SeaCatalog.MaxPortRoutes, SeaCatalog.PortRoutes[tier] + Mathf.Min(3, state.upgradeLevel));
        int count = 0;
        foreach (var r in SeaRoutesHere) if (r.a == state.instanceId || r.b == state.instanceId) count++;
        sb.Append($"<color=#fd8>{TradeTownName(state.instanceId)}</color> harbour: {count}/{slots} sea routes ({SeaCatalog.ShipNames[tier].ToLowerInvariant()}s)\n");
        if (count == 0)
        {
            sb.Append($"<color=#aaa>Build a port in another town on the same water (up to {SeaRouteMaxDistance:0} tiles away) to open a sea route</color>\n");
            return;
        }
        foreach (var r in SeaRoutesHere)
        {
            if (r.a != state.instanceId && r.b != state.instanceId) continue;
            int other = r.a == state.instanceId ? r.b : r.a;
            string st = r.blocked ? " <color=#f88>cut off</color>" : "";
            sb.Append($"  to {TradeTownName(other)}{st}: {r.trips} trips, {r.coin:N0} coin\n");
        }
    }
}
