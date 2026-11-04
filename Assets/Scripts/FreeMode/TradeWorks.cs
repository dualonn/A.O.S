using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Trade routes in the free mode scene:
/// - Works out each route's way overland once (with the map's own pathfinder, a slice a frame),
///   again only when walls, roads or bridges change, or now and then while a route is cut off.
/// - Keeps one caravan on the map per route, driving the trips (the Settlement pays for them).
/// - Road and rail tiles: their pieces join up with the roads (or rail) beside them.
/// Added to the free mode controller; the routes themselves are saved per world (TradeRoutes.cs).
/// </summary>
public class TradeWorks : MonoBehaviour
{
    public static TradeWorks Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Instance = null;

    private FreeModeController controller;
    private Settlement S => controller != null ? controller.Settlement : null;

    private class Way
    {
        public List<Vector3> points;      // the current way (null until first worked out)
        public int walls = int.MinValue;  // the map's wall version it was worked out for
        public Vector3 from, to;          // its ends (the posts' doors)
        public float retryAt;             // cut off: try again after this (the map may have grown)
        public bool reached;
    }

    private readonly Dictionary<int, Way> ways = new Dictionary<int, Way>();
    private readonly Dictionary<int, TradeCaravan> caravans = new Dictionary<int, TradeCaravan>();
    private readonly List<int> scratch = new List<int>();

    // The route being searched for (one at a time, a slice a frame).
    private int searching = -1;
    private Way searchingWay;
    private readonly System.Diagnostics.Stopwatch clock = new System.Diagnostics.Stopwatch();
    private const double SearchMsPerFrame = 1.0;

    private float nextSync;

    public void Init(FreeModeController owner)
    {
        controller = owner;
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        var s = S;
        if (s == null || s.State == null || s.Map == null || !s.IsFounded) return;
        if (Time.time >= nextSync)
        {
            nextSync = Time.time + 0.5f;
            Sync(s);
        }
        Search(s);
    }

    /// <summary>Match ways and caravans to the routes there are now.</summary>
    private void Sync(Settlement s)
    {
        var routes = s.TradeRoutesHere;
        var nav = MapNavigator.Current;
        // Gone: routes that closed.
        scratch.Clear();
        foreach (var id in caravans.Keys) if (!routes.Exists(r => r.id == id)) scratch.Add(id);
        foreach (var id in scratch)
        {
            if (caravans[id] != null) Destroy(caravans[id].gameObject);
            caravans.Remove(id);
        }
        scratch.Clear();
        foreach (var id in ways.Keys) if (!routes.Exists(r => r.id == id)) scratch.Add(id);
        foreach (var id in scratch) ways.Remove(id);

        foreach (var r in routes)
        {
            var pa = s.TradePost(r.a);
            var pb = s.TradePost(r.b);
            if (pa == null || pb == null) continue;
            if (!ways.TryGetValue(r.id, out var way))
            {
                way = new Way();
                // A saved way: use it straight away (it's checked again once the walls are known).
                if (r.path != null && r.path.Count >= 2)
                {
                    way.points = new List<Vector3>();
                    foreach (var p in r.path) way.points.Add(p);
                    way.reached = !r.blocked;
                }
                ways[r.id] = way;
            }
            way.from = s.TradePostDoor(pa);
            way.to = s.TradePostDoor(pb);
            int tier = s.TradeVehicleTier(r);

            // The caravan: one per route, while its way is open (a new one after a loss).
            caravans.TryGetValue(r.id, out var caravan);
            if (caravan != null && caravan.Tier != tier && caravan.IsAlive && caravan.Route.progress < 0.02f)
            {
                Destroy(caravan.gameObject);   // the posts ranked up: the next trip goes in the new vehicle
                caravan = null;
            }
            if (caravan != null && !caravan.IsAlive) caravan = null;
            if (caravan == null)
            {
                caravans.Remove(r.id);
                var points = tier == TradeCatalog.TierHover ? new List<Vector3> { way.from, way.to } : way.points;
                bool open = tier == TradeCatalog.TierHover || (way.points != null && way.reached);
                if (open && points != null && points.Count >= 2 && s.State.playTime >= r.lostUntil)
                    caravans[r.id] = TradeCaravan.Spawn(this, r, points, tier, s.TradeCaravanHealth(tier));
            }
        }
    }

    /// <summary>Work out one route's way at a time, a little each frame.</summary>
    private void Search(Settlement s)
    {
        var nav = MapNavigator.Current;
        if (nav == null) return;
        if (searching < 0)
        {
            // Next route whose way is missing, out of date (walls changed), or cut off and due a retry.
            foreach (var pair in ways)
            {
                var w = pair.Value;
                bool stale = w.walls != nav.WallVersion;
                bool retry = !w.reached && w.walls == nav.WallVersion && Time.time >= w.retryAt && w.retryAt > 0f;
                if (!stale && !retry) continue;
                searching = pair.Key;
                searchingWay = w;
                w.walls = nav.WallVersion;
                nav.BeginRouteSearch(w.from, w.to);
                break;
            }
            if (searching < 0) return;
        }
        clock.Restart();
        while (clock.Elapsed.TotalMilliseconds < SearchMsPerFrame)
        {
            if (!nav.RunRouteSearch(512, out var found, out bool reached)) continue;
            Finish(s, found, reached);
            return;
        }
    }

    private void Finish(Settlement s, List<Vector3> found, bool reached)
    {
        var way = searchingWay;
        int id = searching;
        searching = -1;
        searchingWay = null;
        if (way == null) return;
        // The search runs from the first door; make sure the way starts and ends at the doors.
        if (found != null && found.Count > 0)
        {
            if (Vector2.Distance(found[0], way.from) > 0.3f) found.Insert(0, way.from);
            if (reached && Vector2.Distance(found[found.Count - 1], way.to) > 0.3f) found.Add(way.to);
        }
        way.points = found;
        way.reached = reached && found != null && found.Count >= 2;
        way.retryAt = way.reached ? 0f : Time.time + 20f;
        var route = s.TradeRoutesHere.Find(r => r.id == id);
        if (route == null) return;
        s.TradeSetPath(route, found, way.reached);
        if (way.reached && caravans.TryGetValue(id, out var c) && c != null && c.IsAlive) c.SetPath(found);
    }

    /// <summary>The way a route takes, if worked out (for the panel and markers).</summary>
    public List<Vector3> WayOf(TradeRoute r) => ways.TryGetValue(r.id, out var w) ? w.points : null;

    public TradeCaravan CaravanOf(TradeRoute r) => caravans.TryGetValue(r.id, out var c) && c != null && c.IsAlive ? c : null;

    // ---------- Called by caravans ----------

    public string TripDone(TradeCaravan caravan)
    {
        var s = S;
        if (s == null || caravan.Route == null) return "";
        string note = s.TradeTripDone(caravan.Route);
        GameAudio.Play(caravan.Tier == TradeCatalog.TierTrain ? "trade_train_whistle" : "trade_coins", caravan.transform.position, 0.6f);
        return note;
    }

    public void CaravanLost(TradeCaravan caravan)
    {
        var s = S;
        if (s == null || caravan.Route == null) return;
        GameAudio.Play("trade_caravan_lost", caravan.transform.position, 0.8f);
        s.TradeCaravanLost(caravan.Route);
        caravans.Remove(caravan.Route.id);
    }

    /// <summary>A little rising note over the map ("+12 Coin").</summary>
    public static void Popup(Vector3 at, string text)
    {
        if (Instance == null) return;
        var go = new GameObject("TradePopup");
        go.transform.SetParent(Instance.transform, false);
        go.transform.position = new Vector3(at.x, at.y, -1f);
        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text = $"<color=#ffe27a>{text}</color>";
        tmp.fontSize = 3.2f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.outlineWidth = 0.25f;
        tmp.outlineColor = new Color32(0, 0, 0, 220);
        tmp.rectTransform.sizeDelta = new Vector2(8f, 1.2f);
        tmp.sortingOrder = 20;
        go.AddComponent<TradePopupFloat>();
    }

    // ---------- Road and rail pieces ----------

    /// <summary>
    /// Wall-mask hook: roads join roads and rail joins rail (never walls or bridges). True if this
    /// tile is a road or rail piece. Mask: N=1, E=2, S=4, W=8.
    /// </summary>
    public static bool RoadMaskFor(FreeModeController c, Vector2Int cell, out int mask)
    {
        mask = 0;
        var here = c.WallAt(cell);
        if (here == null || !TradeCatalog.IsRoad(here.Definition)) return false;
        var kind = here.Definition.wallKind;
        bool Same(Vector2Int at)
        {
            var v = c.WallAt(at);
            return v != null && v.Definition.wallKind == kind;
        }
        mask = (Same(cell + Vector2Int.up) ? 1 : 0) | (Same(cell + Vector2Int.right) ? 2 : 0)
             | (Same(cell + Vector2Int.down) ? 4 : 0) | (Same(cell + Vector2Int.left) ? 8 : 0);
        return true;
    }

    /// <summary>Line-placement hook: a road or rail line at an angle gets a tile at each corner, so it joins up.</summary>
    public static void Staircase(BuildingDefinition def, List<Vector2Int> cells)
    {
        if (!TradeCatalog.IsRoad(def) || cells == null) return;
        for (int i = cells.Count - 1; i > 0; i--)
        {
            Vector2Int a = cells[i - 1], b = cells[i];
            if (a.x != b.x && a.y != b.y) cells.Insert(i, new Vector2Int(b.x, a.y));
        }
    }

    /// <summary>BuildingView hook: a road or rail piece picks its art from the mask. True if this is one.</summary>
    public static bool RoadPiece(BuildingView view, int mask, SpriteRenderer body, bool force)
    {
        if (view == null || !TradeCatalog.IsRoad(view.Definition)) return false;
        if (!force || body == null) return true;
        var pieces = view.Definition.pieces;
        if (pieces.Count > 0) body.sprite = pieces[Mathf.Clamp(mask & 15, 0, pieces.Count - 1)];
        return true;
    }
}
