using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// The trade buildings, by id: trading posts in each age (each ranks up into the next) and the
/// roads and rail laid between them. The data assets come from Tools > Age of Stickman > Setup
/// Caravans; this is what the game needs to know about them beyond their BuildingDefinition.
/// </summary>
public static class TradeCatalog
{
    /// <summary>Trading posts by tier: ox wagons, horse caravans, wagon trains, steam trains, maglevs, hover freighters.</summary>
    public static readonly string[] PostIds =
        { "TradePost", "TradeCaravanserai", "TradeCoachingInn", "TradeRailStation", "TradeMaglevTerminal", "TradeHoverPort" };

    public static readonly string[] VehicleNames =
        { "Ox wagon", "Horse caravan", "Wagon train", "Steam train", "Maglev freighter", "Hover freighter" };

    /// <summary>Per vehicle tier: speed (tiles a second), what one trip carries (a factor), and toughness.</summary>
    public static readonly float[] Speed = { 0.75f, 1.1f, 1.35f, 2.6f, 3.8f, 4.4f };
    public static readonly float[] Capacity = { 1f, 1.7f, 2.6f, 5f, 7.5f, 11f };
    public static readonly int[] Health = { 160, 300, 520, 1400, 2400, 3600 };

    /// <summary>Routes a post of each tier runs before upgrades (each upgrade adds one, up to MaxRoutes).</summary>
    public static readonly int[] BaseRoutes = { 1, 2, 2, 3, 3, 4 };
    public const int MaxRoutes = 5;

    /// <summary>Trains need track: the share of the way that must be rail (else wagons run instead).</summary>
    public const float RailNeeded = 0.6f;

    public const int TierTrain = 3, TierMaglev = 4, TierHover = 5;

    /// <summary>Research (small global bonuses).</summary>
    public const string TechTolls = "TradeTollRoads", TechCredit = "TradeLettersOfCredit", TechTimetables = "TradeTimetables",
        TechLogistics = "TradeLogistics";
    public static readonly string[] TechIds = { TechTolls, TechCredit, TechTimetables, TechLogistics };

    public static int PostTier(BuildingDefinition def) => def == null || def.id == null ? -1 : Array.IndexOf(PostIds, def.id);
    public static bool IsPost(BuildingDefinition def) => PostTier(def) >= 0;
    public static bool IsRoad(BuildingDefinition def) => def != null && TradeRoads.IsRoadTile(def.wallKind);
    public static bool IsTradeTech(string id) => id != null && Array.IndexOf(TechIds, id) >= 0;
}

/// <summary>A trade route between two of your trading posts in different towns on one world.</summary>
[Serializable]
public class TradeRoute
{
    public int id;
    [Tooltip("Instance ids of the posts at either end.")]
    public int a, b;
    public int trips;
    public float coin;
    [Tooltip("Value (in coin) of the goods the caravans brought home.")]
    public float goods;
    public int plundered;

    [Tooltip("0..1 along the way, and which way the caravan is heading.")]
    public float progress;
    public bool toB = true;

    [Tooltip("Play time when a new caravan sets out after one was lost.")]
    public float lostUntil;

    [Tooltip("The way overland (tiles from post to post, worked out on the map), and how much of it is road and rail.")]
    public List<Vector2> path = new List<Vector2>();
    public float length;
    public float roadShare, railShare;
    [Tooltip("No way overland (water in between, no bridge or ford): the caravans can't go.")]
    public bool blocked;

    [Tooltip("Buildings in the towns at either end (bigger towns trade more), and outposts on the way.")]
    public int sizeA, sizeB;
    public int outposts;
}

/// <summary>One world's trade: its routes, and what they've earned.</summary>
[Serializable]
public class TradeWorldState
{
    public List<TradeRoute> routes = new List<TradeRoute>();
    public int nextId = 1;
    public float relinkIn;
    public int trips;
    public float coin;
    public int plundered;
}

/// <summary>
/// Trade routes between your own towns: a trading post in two towns makes a route, and caravans
/// (later trains, maglevs and hover freighters) go to and fro along it. Each trip home brings
/// coin and some goods, more for long routes between big towns. Outposts the way passes get a
/// production bonus. On the world you're looking at the caravans are on the map (TradeWorks
/// drives them and raiders can plunder them); on other worlds the trips run on timers.
/// </summary>
public partial class Settlement
{
    /// <summary>Furthest apart (tiles) two posts can be and still trade.</summary>
    public const float TradeMaxDistance = 220f;

    /// <summary>Outposts this close to a route (tiles) count as on it, and what they gain.</summary>
    public const float TradeOutpostReach = 6f, TradeOutpostBonus = 0.1f, TradeOutpostRoadBonus = 0.05f;

    public const float TradeRelinkSeconds = 4f, TradeLostSeconds = 45f;

    public TradeWorldState TradeState
    {
        get
        {
            var w = CurrentWorld;
            if (w.trade == null) w.trade = new TradeWorldState();
            if (w.trade.routes == null) w.trade.routes = new List<TradeRoute>();
            return w.trade;
        }
    }

    /// <summary>This world's routes.</summary>
    public List<TradeRoute> TradeRoutesHere => TradeState.routes;

    // Buildings (by instance id) in outposts on a route, per world. Worked out again at each relink.
    private readonly Dictionary<int, HashSet<int>> tradeLinked = new Dictionary<int, HashSet<int>>();
    private readonly Dictionary<int, float> tradeLinkedBonus = new Dictionary<int, float>();

    /// <summary>Production factor for a building in an outpost on a trade route (1 otherwise).</summary>
    public float TradeLinkFactor(BuildingState building)
    {
        if (tradeLinked.Count == 0 || !tradeLinked.TryGetValue(State.currentWorld, out var set) || !set.Contains(building.instanceId)) return 1f;
        return tradeLinkedBonus.TryGetValue(building.instanceId, out float bonus) ? 1f + bonus : 1f + TradeOutpostBonus;
    }

    // ---------- Ticking ----------

    private void TickTrade(float dt)
    {
        var t = TradeState;
        t.relinkIn -= dt;
        if (t.relinkIn <= 0f)
        {
            t.relinkIn = TradeRelinkSeconds;
            TradeRelink();
        }
        if (t.routes.Count == 0) return;
        // The world you're looking at: the caravans on the map make the trips.
        if (!offWorld && TradeWorks.Instance != null && TradeWorks.Instance.isActiveAndEnabled) return;
        foreach (var r in t.routes)
        {
            if (State.playTime < r.lostUntil) continue;
            if (r.blocked && TradeVehicleTier(r) != TradeCatalog.TierHover) continue;
            r.progress += dt / Mathf.Max(5f, TradeTripSeconds(r));
            if (r.progress < 1f) continue;
            r.progress = 0f;
            r.toB = !r.toB;
            // Out of sight, now and then raiders get a caravan (only where there are camps about).
            if (offWorld && Random.value < TradeOffWorldRisk()) { TradeCaravanLost(r); continue; }
            TradeTripDone(r);
        }
    }

    private float TradeOffWorldRisk()
    {
        int camps = 0;
        foreach (var c in State.camps) if (!c.cleared) camps++;
        return camps == 0 ? 0f : Mathf.Min(0.08f, 0.01f + camps * 0.005f);
    }

    // ---------- Routes ----------

    private struct TradePostInfo { public BuildingState b; public int tier, zone, slots; public Vector2 centre; }

    /// <summary>Pair up trading posts in different towns (nearest first), keeping routes that still stand.</summary>
    private void TradeRelink()
    {
        var t = TradeState;
        var zones = SettlementZones();
        var posts = new List<TradePostInfo>();
        foreach (var b in State.buildings)
        {
            var def = Database.GetBuilding(b.definitionId);
            int tier = TradeCatalog.PostTier(def);
            if (tier < 0 || !IsWorking(b, def) || b.Paused) continue;
            var centre = (Vector2)b.cell + (Vector2)def.size * 0.5f;
            int zone = TradeZoneOf(zones, centre);
            if (zone < 0 || zones[zone].IsOutpost) continue;   // a post trades for a town, not an outpost
            int slots = Mathf.Min(TradeCatalog.MaxRoutes, TradeCatalog.BaseRoutes[tier] + b.upgradeLevel);
            posts.Add(new TradePostInfo { b = b, tier = tier, zone = zone, slots = slots, centre = centre });
        }

        // Every pair in different towns, nearest first; one route per pair of towns.
        var pairs = new List<(int i, int j, float d)>();
        for (int i = 0; i < posts.Count; i++)
            for (int j = i + 1; j < posts.Count; j++)
            {
                if (posts[i].zone == posts[j].zone) continue;
                float d = Vector2.Distance(posts[i].centre, posts[j].centre);
                if (d <= TradeMaxDistance) pairs.Add((i, j, d));
            }
        pairs.Sort((x, y) => x.d.CompareTo(y.d));
        var free = new int[posts.Count];
        for (int i = 0; i < posts.Count; i++) free[i] = posts[i].slots;
        var townPairs = new HashSet<long>();
        var keep = new List<TradeRoute>();
        foreach (var (i, j, _) in pairs)
        {
            if (free[i] <= 0 || free[j] <= 0) continue;
            int za = Mathf.Min(posts[i].zone, posts[j].zone), zb = Mathf.Max(posts[i].zone, posts[j].zone);
            if (!townPairs.Add(((long)za << 32) | (uint)zb)) continue;
            free[i]--; free[j]--;
            int a = posts[i].b.instanceId, b = posts[j].b.instanceId;
            var route = t.routes.Find(r => (r.a == a && r.b == b) || (r.a == b && r.b == a));
            if (route == null)
            {
                route = new TradeRoute { id = t.nextId++, a = a, b = b };
                if (!offWorld) Notify($"A trade route opens between {TradeTownName(a)} and {TradeTownName(b)}.");
            }
            route.sizeA = zones[route.a == a ? posts[i].zone : posts[j].zone].buildings.Count;
            route.sizeB = zones[route.a == a ? posts[j].zone : posts[i].zone].buildings.Count;
            if (route.path == null) route.path = new List<Vector2>();
            keep.Add(route);
        }
        t.routes.Clear();
        t.routes.AddRange(keep);
        TradeLinkOutposts(zones);
    }

    /// <summary>The settlement a point is in (inside one of its buildings), or -1.</summary>
    private static int TradeZoneOf(List<SettlementZone> zones, Vector2 point)
    {
        for (int i = 0; i < zones.Count; i++)
        {
            var bb = zones[i].bounds;
            if (point.x < bb.xMin || point.x > bb.xMax || point.y < bb.yMin || point.y > bb.yMax) continue;
            if (zones[i].DistanceTo(point) <= 0.01f) return i;
        }
        return -1;
    }

    /// <summary>Find the outposts each route passes near, and give their buildings the bonus.</summary>
    private void TradeLinkOutposts(List<SettlementZone> zones)
    {
        int world = State.currentWorld;
        if (!tradeLinked.TryGetValue(world, out var set)) tradeLinked[world] = set = new HashSet<int>();
        foreach (var id in set) tradeLinkedBonus.Remove(id);
        set.Clear();
        var t = TradeState;
        if (t.routes.Count == 0) return;
        var outposts = new List<int>();
        for (int i = 0; i < zones.Count; i++) if (zones[i].IsOutpost) outposts.Add(i);
        foreach (var r in t.routes) r.outposts = 0;
        if (outposts.Count == 0) return;

        var line = new List<Vector2>();
        var hit = new HashSet<int>();
        foreach (var r in t.routes)
        {
            line.Clear();
            if (r.path != null && r.path.Count >= 2) line.AddRange(r.path);
            else
            {
                var pa = TradePost(r.a); var pb = TradePost(r.b);
                if (pa == null || pb == null) continue;
                line.Add(pa.cell); line.Add(pb.cell);
            }
            float bonus = TradeOutpostBonus + TradeOutpostRoadBonus * Mathf.Clamp01(Mathf.Max(r.roadShare, r.railShare) * 2f);
            foreach (int zi in outposts)
            {
                var z = zones[zi];
                if (TradeDistanceToLine(z, line) > TradeOutpostReach) continue;
                r.outposts++;
                hit.Add(zi);
                // Every building of that outpost gets the bonus (the best route's, if several pass).
                foreach (var b in State.buildings)
                {
                    var def = Database.GetBuilding(b.definitionId);
                    if (def == null || def.IsWall || def.production.Count == 0) continue;
                    var c = (Vector2)b.cell + (Vector2)def.size * 0.5f;
                    if (!z.bounds.Contains(c) || z.DistanceTo(c) > 0.01f) continue;
                    set.Add(b.instanceId);
                    tradeLinkedBonus[b.instanceId] = tradeLinkedBonus.TryGetValue(b.instanceId, out float old) ? Mathf.Max(old, bonus) : bonus;
                }
            }
        }
    }

    private static float TradeDistanceToLine(SettlementZone z, List<Vector2> line)
    {
        float best = float.MaxValue;
        var bb = z.bounds;
        for (int i = 0; i + 1 < line.Count; i++)
        {
            Vector2 p = line[i], q = line[i + 1];
            // Quick reject: the segment's box, grown by the reach, misses the outpost's box.
            float minX = Mathf.Min(p.x, q.x) - TradeOutpostReach, maxX = Mathf.Max(p.x, q.x) + TradeOutpostReach;
            float minY = Mathf.Min(p.y, q.y) - TradeOutpostReach, maxY = Mathf.Max(p.y, q.y) + TradeOutpostReach;
            if (bb.xMax < minX || bb.xMin > maxX || bb.yMax < minY || bb.yMin > maxY) continue;
            foreach (var rect in z.buildings)
            {
                var c = Closest(p, q, rect.center);
                float dx = Mathf.Max(rect.xMin - c.x, 0, c.x - rect.xMax);
                float dy = Mathf.Max(rect.yMin - c.y, 0, c.y - rect.yMax);
                best = Mathf.Min(best, Mathf.Sqrt(dx * dx + dy * dy));
            }
        }
        return best;
    }

    private static Vector2 Closest(Vector2 p, Vector2 q, Vector2 x)
    {
        var d = q - p;
        float l = d.sqrMagnitude;
        if (l < 0.0001f) return p;
        return p + d * Mathf.Clamp01(Vector2.Dot(x - p, d) / l);
    }

    // ---------- What a route does ----------

    public BuildingState TradePost(int instanceId)
    {
        var list = State.buildings;
        for (int i = 0; i < list.Count; i++) if (list[i].instanceId == instanceId) return list[i];
        return null;
    }

    /// <summary>Where caravans stop at a post: just in front of the middle of its front edge.</summary>
    public Vector3 TradePostDoor(BuildingState post)
    {
        var def = post != null ? Database.GetBuilding(post.definitionId) : null;
        if (def == null) return Vector3.zero;
        return new Vector3(post.cell.x + def.size.x * 0.5f, post.cell.y - 0.25f, 0f);
    }

    /// <summary>The vehicle a route runs: the lesser of its two posts, and trains only where there's track.</summary>
    public int TradeVehicleTier(TradeRoute r)
    {
        var pa = TradePost(r.a); var pb = TradePost(r.b);
        int ta = pa != null ? TradeCatalog.PostTier(Database.GetBuilding(pa.definitionId)) : 0;
        int tb = pb != null ? TradeCatalog.PostTier(Database.GetBuilding(pb.definitionId)) : 0;
        int tier = Mathf.Clamp(Mathf.Min(ta, tb), 0, TradeCatalog.TierHover);
        if ((tier == TradeCatalog.TierTrain || tier == TradeCatalog.TierMaglev) && r.railShare < TradeCatalog.RailNeeded) tier = 2;
        return tier;
    }

    /// <summary>A post's own tier (what it could run with track and a partner to match).</summary>
    public int TradePostTier(BuildingState post) => post != null ? TradeCatalog.PostTier(Database.GetBuilding(post.definitionId)) : -1;

    private float TradeRouteLength(TradeRoute r)
    {
        if (r.length > 0f && TradeVehicleTier(r) != TradeCatalog.TierHover) return r.length;
        var pa = TradePost(r.a); var pb = TradePost(r.b);
        float straight = pa != null && pb != null ? Vector2.Distance(pa.cell, pb.cell) : 20f;
        return TradeVehicleTier(r) == TradeCatalog.TierHover ? straight : straight * 1.35f;
    }

    /// <summary>Caravan speed from research (tiles a second, before roads).</summary>
    public float TradeSpeed(int tier) =>
        TradeCatalog.Speed[Mathf.Clamp(tier, 0, 5)] * (HasTradeTech(TradeCatalog.TechTimetables) ? 1.15f : 1f);

    public float TradeTripSeconds(TradeRoute r)
    {
        int tier = TradeVehicleTier(r);
        float speed = TradeSpeed(tier);
        if (tier <= 2) speed *= 1f + (TradeRoads.RoadSpeed - 1f) * r.roadShare;
        return TradeRouteLength(r) / Mathf.Max(0.1f, speed) + 4f;   // a few seconds loading at each end
    }

    private bool HasTradeTech(string id) => State.researched.Contains(id);

    public float TradeResearchFactor
    {
        get
        {
            float f = 1f;
            if (HasTradeTech(TradeCatalog.TechTolls)) f += 0.1f;
            if (HasTradeTech(TradeCatalog.TechCredit)) f += 0.1f;
            if (HasTradeTech(TradeCatalog.TechLogistics)) f += 0.1f;
            return f;
        }
    }

    /// <summary>Caravan toughness (Letters of Credit pays for guards).</summary>
    public int TradeCaravanHealth(int tier) =>
        Mathf.RoundToInt(TradeCatalog.Health[Mathf.Clamp(tier, 0, 5)] * (HasTradeTech(TradeCatalog.TechCredit) ? 1.2f : 1f));

    /// <summary>What one trip is worth, in coin: longer routes and bigger towns trade more.</summary>
    public float TradeTripValue(TradeRoute r)
    {
        int tier = TradeVehicleTier(r);
        float length = Mathf.Clamp(TradeRouteLength(r), 8f, 260f);
        float size = 0.6f + 0.04f * Mathf.Min(60, r.sizeA + r.sizeB);
        float posts = (TradePostOutput(r.a) + TradePostOutput(r.b)) * 0.5f;
        float roads = 1f + 0.25f * Mathf.Max(r.roadShare, r.railShare);
        return 0.3f * length * TradeCatalog.Capacity[tier] * size * posts * roads * TradeResearchFactor * CivicsTradeFactor;
    }

    private float TradePostOutput(int id)
    {
        var p = TradePost(id);
        if (p == null) return 1f;
        var def = Database.GetBuilding(p.definitionId);
        if (def == null) return 1f;
        float health = p.health / Mathf.Max(1f, StatsOf(def, p.upgradeLevel).maxHealth);
        return StatsOf(def, p.upgradeLevel).output * Mathf.Max(0.25f, StaffingOf(p)) * Mathf.Clamp(health, 0.25f, 1f);
    }

    private bool TradeCoinUnlocked
    {
        get
        {
            var coin = Database.coin;
            return coin != null && (coin.unlockedIn == null || coin.unlockedIn.order <= State.ageIndex);
        }
    }

    /// <summary>A caravan got home: pay for the trip (coin, and goods it brought back). Returns a short note.</summary>
    public string TradeTripDone(TradeRoute r)
    {
        var t = TradeState;
        float value = TradeTripValue(r);
        float goodsValue = TradeCoinUnlocked ? value * 0.2f : value * 0.8f;
        float coin = TradeCoinUnlocked ? value * 0.8f : 0f;
        string note = "";
        if (coin > 0f)
        {
            AddResource(Database.coin, coin);
            note = $"+{coin:0} {Database.coin.displayName}";
        }
        // Goods from the other town: a different one each trip, from what the market deals in.
        var goods = XMarketGoods();
        if (goods.Count > 0 && goodsValue > 0f)
        {
            var good = goods[(r.trips + r.id) % goods.Count];
            float amount = goodsValue / Mathf.Max(0.1f, XBaseValue(good));
            AddResource(good, amount);
            note += (note.Length > 0 ? ", " : "") + $"+{amount:0} {good.displayName}";
            r.goods += goodsValue;
        }
        r.trips++;
        r.coin += coin;
        t.trips++;
        t.coin += coin;
        return note;
    }

    /// <summary>Raiders got a caravan: the goods are gone, and a new one sets out after a while.</summary>
    public void TradeCaravanLost(TradeRoute r)
    {
        var t = TradeState;
        r.lostUntil = State.playTime + TradeLostSeconds;
        r.progress = 0f;
        r.plundered++;
        t.plundered++;
        Notify($"Raiders plundered a {TradeCatalog.VehicleNames[TradeVehicleTier(r)].ToLower()} between {TradeTownName(r.a)} and {TradeTownName(r.b)}. Another sets out in {TradeLostSeconds:0}s.");
    }

    /// <summary>TradeWorks worked out the way overland: keep it (it's saved, so other worlds and reloads use it).</summary>
    public void TradeSetPath(TradeRoute r, List<Vector3> points, bool reached)
    {
        if (r.path == null) r.path = new List<Vector2>();
        r.path.Clear();
        r.blocked = !reached;
        r.length = 0f;
        r.roadShare = r.railShare = 0f;
        if (points == null || points.Count < 2) return;
        foreach (var p in points) r.path.Add(p);
        int samples = 0, road = 0, rail = 0;
        for (int i = 0; i + 1 < points.Count; i++)
        {
            float d = Vector2.Distance(points[i], points[i + 1]);
            r.length += d;
            int n = Mathf.Max(1, Mathf.CeilToInt(d / 0.5f));
            for (int k = 0; k < n; k++)
            {
                var p = Vector2.Lerp(points[i], points[i + 1], (k + 0.5f) / n);
                var w = Map != null ? Map.WallAt(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y)) : WallTile.None;
                samples++;
                if (w == WallTile.Road) road++;
                else if (w == WallTile.Rail) rail++;
            }
        }
        r.roadShare = samples > 0 ? road / (float)samples : 0f;
        r.railShare = samples > 0 ? rail / (float)samples : 0f;
    }

    // ---------- Names and text ----------

    private static readonly string[] TownFront = { "Ash", "Oak", "Mill", "High", "Elm", "Stone", "Raven", "Bright", "Wolf", "Thorn", "Glen", "Fair", "Iron", "Salt", "Red", "Kings", "Moor", "Wil", "Hart", "Brook" };
    private static readonly string[] TownBack = { "ford", "mere", "wick", "ton", "bury", "dale", "field", "haven", "stead", "holm", "gate", "cross", "wood", "by", "well", "march" };

    /// <summary>A name for the town a post stands in (fixed for that post).</summary>
    public static string TradeTownName(int postId)
    {
        uint h = (uint)postId * 2654435761u;
        return TownFront[(h >> 8) % TownFront.Length] + TownBack[(h >> 20) % TownBack.Length];
    }

    public string TradeRouteName(TradeRoute r) => $"{TradeTownName(r.a)} - {TradeTownName(r.b)}";

    /// <summary>Coin a minute from every route on this world, as things stand.</summary>
    public float TradeIncomePerMinute()
    {
        float total = 0f;
        foreach (var r in TradeRoutesHere)
        {
            if (r.blocked && TradeVehicleTier(r) != TradeCatalog.TierHover) continue;
            total += TradeTripValue(r) * 60f / Mathf.Max(5f, TradeTripSeconds(r));
        }
        return total;
    }

    public string TradeRouteStatus(TradeRoute r)
    {
        int tier = TradeVehicleTier(r);
        if (r.blocked && tier != TradeCatalog.TierHover) return "<color=#f88>Cut off: no way overland (build a bridge or ford)</color>";
        if (State.playTime < r.lostUntil) return $"<color=#fb7>Plundered: a new one sets out in {r.lostUntil - State.playTime:0}s</color>";
        string where = r.toB ? TradeTownName(r.b) : TradeTownName(r.a);
        return $"<color=#9df>On the road to {where} ({r.progress:P0})</color>";
    }

    public string TradeRouteTooltip(TradeRoute r)
    {
        int tier = TradeVehicleTier(r);
        var sb = new StringBuilder($"<b>{TradeRouteName(r)}</b>\n");
        sb.Append($"{TradeCatalog.VehicleNames[tier]}  ·  {TradeRouteLength(r):0} tiles");
        if (r.roadShare > 0.01f) sb.Append($"  ·  {r.roadShare:P0} road");
        if (r.railShare > 0.01f) sb.Append($"  ·  {r.railShare:P0} rail");
        sb.Append('\n');
        sb.Append($"Each trip: about {TradeTripValue(r):0} coin worth, every {TradeTripSeconds(r):0}s\n");
        sb.Append($"Towns: {r.sizeA} and {r.sizeB} buildings");
        if (r.outposts > 0) sb.Append($"  ·  {r.outposts} outpost{(r.outposts == 1 ? "" : "s")} on the way (+{TradeOutpostBonus:P0} output)");
        sb.Append('\n');
        sb.Append($"So far: {r.trips} trips, {r.coin:N0} coin, goods worth {r.goods:N0}");
        if (r.plundered > 0) sb.Append($", {r.plundered} plundered");
        sb.Append('\n').Append(TradeRouteStatus(r));
        int postTier = Mathf.Min(TradePostTier(TradePost(r.a)), TradePostTier(TradePost(r.b)));
        if ((postTier == TradeCatalog.TierTrain || postTier == TradeCatalog.TierMaglev) && tier < TradeCatalog.TierTrain)
            sb.Append($"\n<color=#fd8>Lay rail track along most of the way ({r.railShare:P0} of {TradeCatalog.RailNeeded:P0}) to run {TradeCatalog.VehicleNames[postTier].ToLower()}s.</color>");
        return sb.ToString();
    }

    /// <summary>Lines for the selected building's panel: a post's routes, a road's speed, an outpost's bonus.</summary>
    public void TradeSelectionInfo(StringBuilder sb, BuildingState state, BuildingDefinition def)
    {
        if (def == null || state == null) return;
        if (TradeCatalog.IsRoad(def))
        {
            sb.Append(def.wallKind == WallTile.Rail
                ? $"Trains run on it; walkers {TradeRoads.RailWalkSpeed - 1f:P0} faster\n"
                : $"Everyone on it moves {TradeRoads.RoadSpeed - 1f:P0} faster\n");
            return;
        }
        if (TradeLinkFactor(state) > 1.001f) sb.Append($"<color=#9e8>On a trade route: +{TradeLinkFactor(state) - 1f:P0} output</color>\n");
        int tier = TradeCatalog.PostTier(def);
        if (tier < 0) return;
        sb.Append($"<color=#fd8>{TradeTownName(state.instanceId)}</color>: ");
        int slots = Mathf.Min(TradeCatalog.MaxRoutes, TradeCatalog.BaseRoutes[tier] + state.upgradeLevel);
        int count = 0;
        foreach (var r in TradeRoutesHere) if (r.a == state.instanceId || r.b == state.instanceId) count++;
        sb.Append($"{count}/{slots} routes\n");
        if (count == 0)
        {
            var zones = SettlementZones();
            int zone = TradeZoneOf(zones, (Vector2)state.cell + (Vector2)def.size * 0.5f);
            sb.Append(zone >= 0 && zones[zone].IsOutpost
                ? "<color=#fb7>Must stand in a town (with housing or a town centre)</color>\n"
                : $"<color=#aaa>Build a trading post in another town (up to {TradeMaxDistance:0} tiles away) to open a route</color>\n");
            return;
        }
        foreach (var r in TradeRoutesHere)
        {
            if (r.a != state.instanceId && r.b != state.instanceId) continue;
            int other = r.a == state.instanceId ? r.b : r.a;
            sb.Append($"  to {TradeTownName(other)}: {TradeCatalog.VehicleNames[TradeVehicleTier(r)].ToLower()}, {r.trips} trips, {r.coin:N0} coin\n");
        }
    }
}
