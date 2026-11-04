using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Rivers and the sea in the free mode scene:
/// - Bridges and fords: their pieces join up along a line; raiders crossing wear them down.
/// - Warships patrol the water round their harbour (they fight like any troop, but only afloat).
/// - Ferries: troops you send to land the water cuts off (an island, the far shore of a lake) walk
///   to the bank, a harbour's ferry picks them up and lands them on the far side.
/// - Naval raids: on coasts with open sea, some raids row in by boat and land on the shore, just
///   outside your settlement's safe zone. Sink the boats first and the raiders aboard drown.
/// Added to the free mode controller; nothing here is saved (bridges are buildings, the rest is live).
/// </summary>
public class RiverWorks : MonoBehaviour
{
    public static RiverWorks Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Instance = null; internalOrder = false; }

    private FreeModeController controller;
    private Settlement S => controller != null ? controller.Settlement : null;
    private MapData Map => S != null ? S.Map : null;

    public void Init(FreeModeController owner)
    {
        controller = owner;
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        foreach (var trip in trips)
            if (trip.boat != null) Destroy(trip.boat);
    }

    private void Update()
    {
        if (controller == null || S == null || Map == null || !S.IsFounded) return;
        TickBridges();
        TickPatrols();
        TickFerries();
        TickNavalRaid();
    }

    private void LateUpdate()
    {
        if (controller == null || S == null || Map == null) return;
        if (pending.Count > 0) PlanFerries();
    }

    // =====================================================================
    // Bridges
    // =====================================================================

    private float nextBridgeWear;

    /// <summary>Raiders crossing a bridge hack at it as they go (they never set out to smash one).</summary>
    private void TickBridges()
    {
        if (Time.time < nextBridgeWear) return;
        nextBridgeWear = Time.time + 0.5f;
        foreach (var u in Unit.Living)
        {
            if (!(u is Enemy e) || !e.IsAlive || !e.IsRaider || e.Team != Team.Enemy || e.Moves != Movement.Ground) continue;
            var p = e.transform.position;
            var cell = new Vector2Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y));
            if (Map.WallAt(cell.x, cell.y) != WallTile.Bridge) continue;
            var view = controller.WallAt(cell);
            if (view == null || !controller.IsStanding(view)) continue;
            S.DamageBuilding(view.State, Mathf.Max(1f, e.attackPower * 0.15f));
            view.Shake();
            if (Random.value < 0.15f) GameAudio.Play("river_bridge_creak", p, 0.5f);
        }
    }

    /// <summary>
    /// Wall-mask hook: bridges join only bridges and walls only walls, so a wall beside a bridge
    /// doesn't sprout a run out over the water. True if this tile holds either.
    /// </summary>
    public static bool WallMaskFor(FreeModeController c, Vector2Int cell, out int mask)
    {
        mask = 0;
        var here = c.WallAt(cell);
        if (here == null) return false;
        bool bridge = RiverCatalog.IsBridge(here.Definition);
        bool Same(Vector2Int at)
        {
            var v = c.WallAt(at);
            return v != null && RiverCatalog.IsBridge(v.Definition) == bridge && !TradeCatalog.IsRoad(v.Definition);
        }
        bool n = Same(cell + Vector2Int.up), e = Same(cell + Vector2Int.right), s = Same(cell + Vector2Int.down), w = Same(cell + Vector2Int.left);
        mask = (n ? 1 : 0) | (e ? 2 : 0) | (s ? 4 : 0) | (w ? 8 : 0);
        if (bridge) return true;   // bridges run straight: no diagonal joins
        if (!n && !e && Same(cell + new Vector2Int(1, 1))) mask |= 16;
        if (!n && !w && Same(cell + new Vector2Int(-1, 1))) mask |= 128;
        if (!s && !e && Same(cell + new Vector2Int(1, -1))) mask |= 32;
        if (!s && !w && Same(cell + new Vector2Int(-1, -1))) mask |= 64;
        return true;
    }

    /// <summary>Bridge piece indices (see the Setup Rivers art).</summary>
    public const int PiecePad = 0, PieceWestEnd = 1, PieceEastWest = 2, PieceEastEnd = 3, PieceSouthEnd = 4, PieceNorthSouth = 5,
        PieceNorthEnd = 6, PieceEastWestSingle = 7, PieceNorthSouthSingle = 8, PieceCount = 9;

    /// <summary>
    /// BuildingView hook: pick a bridge's piece from the bridges beside it (and the banks, for a
    /// one-piece ford). True if this is a bridge (the wall code then leaves it alone).
    /// </summary>
    public static bool BridgePiece(BuildingView view, int mask, SpriteRenderer body, bool force)
    {
        if (view == null || !RiverCatalog.IsBridge(view.Definition)) return false;
        if (!force || body == null) return true;
        var pieces = view.Definition.pieces;
        bool n = (mask & 1) != 0, e = (mask & 2) != 0, s = (mask & 4) != 0, w = (mask & 8) != 0;
        int index;
        if ((e || w) && !(n || s)) index = e && w ? PieceEastWest : e ? PieceWestEnd : PieceEastEnd;
        else if ((n || s) && !(e || w)) index = n && s ? PieceNorthSouth : n ? PieceSouthEnd : PieceNorthEnd;
        else if (!(n || e || s || w))
        {
            // On its own: lie across whichever way has banks either side.
            var map = Settlement.Instance != null ? Settlement.Instance.Map : null;
            var c = view.State.cell;
            bool Land(int dx, int dy) => map != null && map.InBounds(c.x + dx, c.y + dy) && map.GetTerrain(c.x + dx, c.y + dy) != TerrainType.Water;
            bool ew = Land(1, 0) || Land(-1, 0), ns = Land(0, 1) || Land(0, -1);
            index = ew && !ns ? PieceEastWestSingle : ns && !ew ? PieceNorthSouthSingle : PiecePad;
        }
        else index = PiecePad;   // a junction
        if (pieces.Count > 0) body.sprite = pieces[Mathf.Clamp(index, 0, pieces.Count - 1)];
        return true;
    }

    /// <summary>Line-placement hook: bridges are dragged out dead straight (along the longer way).</summary>
    public static Vector2Int LineEnd(BuildingDefinition def, Vector2Int start, Vector2Int end)
    {
        if (!RiverCatalog.IsBridge(def)) return end;
        return Mathf.Abs(end.x - start.x) >= Mathf.Abs(end.y - start.y) ? new Vector2Int(end.x, start.y) : new Vector2Int(start.x, end.y);
    }

    // =====================================================================
    // Warships on patrol
    // =====================================================================

    private float nextPatrolCheck;
    private readonly Dictionary<Troop, float> patrolAt = new Dictionary<Troop, float>();
    private readonly Dictionary<BuildingView, Vector3> harbourWater = new Dictionary<BuildingView, Vector3>();

    /// <summary>The open water a harbour's boats keep to (worked out once per harbour).</summary>
    public bool HarbourWater(BuildingView harbour, out Vector3 water)
    {
        water = default;
        if (harbour == null) return false;
        if (harbourWater.TryGetValue(harbour, out water)) return true;
        var centre = controller.Grid.AreaCenter(harbour.State.cell, harbour.Definition.size);
        if (!RiverNav.NearestSailable(Map, centre, 10, out water)) return false;
        harbourWater[harbour] = water;
        return true;
    }

    private void TickPatrols()
    {
        if (Time.time < nextPatrolCheck) return;
        nextPatrolCheck = Time.time + 1f;
        foreach (var u in Unit.Living)
        {
            if (!(u is Troop t) || !t.IsAlive || t.Moves != Movement.Water || t.HasOrder || t.currentTarget != null) continue;
            if (patrolAt.TryGetValue(t, out float at) && Time.time < at) continue;
            patrolAt[t] = Time.time + Random.Range(6f, 12f);
            Vector3 home = t.transform.position;
            if (t.Home != null && HarbourWater(t.Home, out var hw)) home = hw;
            for (int attempt = 0; attempt < 12; attempt++)
            {
                Vector2 off = Random.insideUnitCircle * 7f;
                var p = home + new Vector3(off.x, off.y, 0);
                if (RiverNav.Sailable(Map, p) && RiverNav.ClearLine(Map, home, p))
                {
                    t.SetRallyPoint(p);
                    break;
                }
            }
        }
        if (patrolAt.Count > 64)
        {
            var gone = new List<Troop>();
            foreach (var k in patrolAt.Keys) if (k == null) gone.Add(k);
            foreach (var k in gone) patrolAt.Remove(k);
        }
    }

    // =====================================================================
    // Ferries
    // =====================================================================

    private static bool internalOrder;
    private readonly List<(Troop troop, Vector3 dest)> pending = new List<(Troop, Vector3)>();

    private class Trip
    {
        public BuildingView harbour;
        public GameObject boat;
        public SpriteRenderer sprite;
        public List<Sprite> idle = new List<Sprite>(), moving = new List<Sprite>();
        public readonly List<Troop> waiting = new List<Troop>(), aboard = new List<Troop>();
        public readonly Dictionary<Troop, Vector3> goal = new Dictionary<Troop, Vector3>();
        public Vector3 pickupWater, pickupLand, dropWater, dropLand, homeWater;
        public List<Vector3> path = new List<Vector3>();
        public int stage;   // 0 to the pickup, 1 boarding, 2 crossing, 3 home
        public float stageAt, speed;
        public int seats;
    }

    private readonly List<Trip> trips = new List<Trip>();
    private const int MaxTripsPerHarbour = 3;

    /// <summary>Troop.OrderMoveTo hook: you sent a troop somewhere (looked at after this frame's orders are all in).</summary>
    public static void Ordered(Troop troop, Vector3 destination)
    {
        if (internalOrder || Instance == null || troop == null || troop.Moves != Movement.Ground) return;
        Instance.pending.Add((troop, destination));
    }

    private static void OrderInternal(Troop t, Vector3 destination)
    {
        if (t == null) return;
        internalOrder = true;
        try { t.OrderMoveTo(destination); }
        finally { internalOrder = false; }
    }

    private Trip TripOf(Troop t)
    {
        foreach (var trip in trips) if (trip.waiting.Contains(t) || trip.aboard.Contains(t)) return trip;
        return null;
    }

    // ---------- Is it across the water? ----------

    private class Region
    {
        public HashSet<Vector2Int> tiles;
        public bool closed;
    }

    private readonly List<Region> regions = new List<Region>();

    private static Vector2Int Cell(Vector3 p) => new Vector2Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y));

    /// <summary>The land (and bridges) joined to 'start', up to a few thousand tiles: closed if that's all there is.</summary>
    private Region RegionOf(Vector2Int start)
    {
        foreach (var r in regions) if (r.tiles.Contains(start)) return r;
        var region = new Region { tiles = new HashSet<Vector2Int> { start }, closed = true };
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(start);
        const int Budget = 6000;
        while (queue.Count > 0)
        {
            if (region.tiles.Count > Budget) { region.closed = false; break; }
            var t = queue.Dequeue();
            for (int i = 0; i < 4; i++)
            {
                var nb = t + (i == 0 ? Vector2Int.right : i == 1 ? Vector2Int.left : i == 2 ? Vector2Int.up : Vector2Int.down);
                if (region.tiles.Contains(nb) || !Map.IsWalkable(nb.x, nb.y)) continue;
                region.tiles.Add(nb);
                queue.Enqueue(nb);
            }
        }
        regions.Add(region);
        return region;
    }

    /// <summary>True when no walk joins the two (one of them is on land the water cuts off).</summary>
    private bool AcrossWater(Vector3 from, Vector3 to)
    {
        Vector2Int a = Cell(from), b = Cell(to);
        if (!Map.IsWalkable(a.x, a.y) || !Map.IsWalkable(b.x, b.y)) return false;
        var rb = RegionOf(b);
        if (rb.tiles.Contains(a)) return false;
        if (rb.closed) return true;
        var ra = RegionOf(a);
        return ra.closed && !ra.tiles.Contains(b);
    }

    // ---------- Planning trips ----------

    private void PlanFerries()
    {
        var orders = new List<(Troop troop, Vector3 dest)>(pending);
        pending.Clear();
        regions.Clear();
        var nav = MapNavigator.Current;
        if (nav == null) return;
        var harbours = new List<BuildingView>();
        foreach (var v in controller.Buildings)
            if (RiverCatalog.KindOf(v.Definition) == RiverCatalog.Kind.Harbour && controller.IsStanding(v)) harbours.Add(v);
        if (harbours.Count == 0) return;

        var crossing = new List<(Troop troop, Vector3 dest)>();
        foreach (var (t, rawDest) in orders)
        {
            if (t == null || !t.IsAlive) continue;
            var trip = TripOf(t);
            if (trip != null)
            {
                if (trip.aboard.Contains(t)) { trip.goal[t] = rawDest; continue; }   // a new goal for when we land
                trip.waiting.Remove(t);
                trip.goal.Remove(t);
            }
            Vector3 dest = nav.NearestWalkable(rawDest, Team.Player);
            if (AcrossWater(t.transform.position, dest)) crossing.Add((t, rawDest));
        }
        if (crossing.Count == 0) return;

        // One crossing per order: the troops' middle to the order's middle.
        Vector3 from = Vector3.zero, to = Vector3.zero;
        foreach (var (t, d) in crossing) { from += t.transform.position; to += d; }
        from /= crossing.Count; to /= crossing.Count;
        from.z = to.z = 0;
        var startRegion = RegionOf(Cell(nav.NearestWalkable(crossing[0].troop.transform.position, Team.Player)));
        var endRegion = RegionOf(Cell(nav.NearestWalkable(crossing[0].dest, Team.Player)));
        if (!ShoreSpot(from, startRegion, out var pickupWater, out var pickupLand) || !ShoreSpot(to, endRegion, out var dropWater, out var dropLand))
        {
            controller.ShowMessage("No ferry can reach there: there's no shore to land on.");
            return;
        }
        // The nearest harbour whose water joins both shores.
        harbours.Sort((x, y) => Vector2.Distance(x.transform.position, from).CompareTo(Vector2.Distance(y.transform.position, from)));
        BuildingView harbour = null;
        Vector3 homeWater = default;
        foreach (var h in harbours)
        {
            if (CountTrips(h) >= MaxTripsPerHarbour || !HarbourWater(h, out var hw)) continue;
            RiverNav.FindPath(Map, hw, pickupWater, out bool r1, 40000);
            if (!r1) continue;
            RiverNav.FindPath(Map, pickupWater, dropWater, out bool r2, 40000);
            if (!r2) continue;
            harbour = h;
            homeWater = hw;
            break;
        }
        if (harbour == null)
        {
            controller.ShowMessage("No ferry can reach there: none of your harbours is on that water (or all its ferries are out).");
            GameAudio.UI("error");
            return;
        }
        int seats = Mathf.Max(1, S.RiverFerrySeats(harbour.State));
        float speed = S.RiverFerrySpeed(harbour.State);
        int carried = 0, left = 0;
        for (int i = 0; i < crossing.Count; i += seats)
        {
            if (CountTrips(harbour) >= MaxTripsPerHarbour) { left += crossing.Count - i; break; }
            var trip = NewTrip(harbour, homeWater, pickupWater, pickupLand, dropWater, dropLand, seats, speed);
            for (int k = i; k < Mathf.Min(crossing.Count, i + seats); k++)
            {
                var (t, d) = crossing[k];
                trip.waiting.Add(t);
                trip.goal[t] = d;
                Vector2 jitter = Random.insideUnitCircle * 0.8f;
                var spot = pickupLand + new Vector3(jitter.x, jitter.y, 0);
                OrderInternal(t, Map.IsWalkable(spot) ? spot : pickupLand);
                carried++;
            }
        }
        if (carried > 0)
        {
            controller.ShowMessage(left > 0
                ? $"The {harbour.Definition.displayName}'s ferries will carry {carried} troops across ({left} must wait for the next order)."
                : $"The {harbour.Definition.displayName}'s ferry will carry {(carried == 1 ? "your troop" : $"{carried} troops")} across.");
            GameAudio.Play("river_ferry_bell", harbour.transform.position, 0.8f);
        }
    }

    private int CountTrips(BuildingView h)
    {
        int n = 0;
        foreach (var t in trips) if (t.harbour == h) n++;
        return n;
    }

    /// <summary>Open water near 'near', and the bank beside it on the given land.</summary>
    private bool ShoreSpot(Vector3 near, Region land, out Vector3 water, out Vector3 bank)
    {
        water = bank = near;
        var c = Cell(near);
        float best = float.MaxValue;
        bool found = false;
        for (int r = 0; r <= 24 && !(found && r > Mathf.Sqrt(best) + 1); r++)
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                    int x = c.x + dx, y = c.y + dy;
                    if (!RiverNav.Sailable(Map, x, y)) continue;
                    for (int i = 0; i < 4; i++)
                    {
                        int bx = x + (i == 0 ? 1 : i == 1 ? -1 : 0), by = y + (i == 2 ? 1 : i == 3 ? -1 : 0);
                        var b = new Vector2Int(bx, by);
                        if (!Map.IsWalkable(bx, by) || (land != null && land.closed && !land.tiles.Contains(b))) continue;
                        if (land != null && !land.closed && !land.tiles.Contains(b)) continue;
                        float d = (b - c).sqrMagnitude;
                        if (d < best)
                        {
                            best = d;
                            water = new Vector3(x + 0.5f, y + 0.5f, 0);
                            bank = new Vector3(bx + 0.5f, by + 0.5f, 0);
                            found = true;
                        }
                    }
                }
        return found;
    }

    private Trip NewTrip(BuildingView harbour, Vector3 homeWater, Vector3 pickupWater, Vector3 pickupLand, Vector3 dropWater, Vector3 dropLand, int seats, float speed)
    {
        var trip = new Trip
        {
            harbour = harbour, homeWater = homeWater, pickupWater = pickupWater, pickupLand = pickupLand,
            dropWater = dropWater, dropLand = dropLand, seats = seats, speed = speed, stageAt = Time.time,
        };
        // The ferry is one of the harbour's own boats.
        var look = harbour.Definition.trainsUnit != null && harbour.Definition.trainsUnit.prefab != null
            ? harbour.Definition.trainsUnit.prefab.GetComponent<SpriteUnitVisual>() : null;
        trip.boat = new GameObject("Ferry");
        trip.boat.transform.SetParent(transform, false);
        trip.boat.transform.position = homeWater;
        trip.boat.transform.localScale = Vector3.one * controller.unitScale;
        trip.sprite = trip.boat.AddComponent<SpriteRenderer>();
        trip.sprite.sortingOrder = BuildingView.SortingOrder;
        if (look != null)
        {
            if (look.idle != null) trip.idle.Add(look.idle);
            trip.moving.AddRange(look.walk);
            if (trip.moving.Count == 0) trip.moving.AddRange(trip.idle);
            trip.sprite.sprite = look.idle;
        }
        else
        {
            trip.sprite.sprite = SimpleSprites.Square;
            trip.boat.transform.localScale = new Vector3(1.4f, 0.5f, 1f);
            trip.sprite.color = new Color(0.55f, 0.36f, 0.2f);
        }
        trip.path = RiverNav.FindPath(Map, homeWater, pickupWater, out _, 40000);
        trips.Add(trip);
        return trip;
    }

    private void TickFerries()
    {
        for (int i = trips.Count - 1; i >= 0; i--)
        {
            var trip = trips[i];
            if (trip.boat == null) { ReleaseAll(trip); trips.RemoveAt(i); continue; }
            bool moving = trip.stage != 1 && trip.path.Count > 0;
            if (moving) SailAlong(trip);
            Animate(trip, moving);
            switch (trip.stage)
            {
                case 0:
                    if (trip.path.Count == 0) { trip.stage = 1; trip.stageAt = Time.time; }
                    break;
                case 1:
                    Board(trip);
                    break;
                case 2:
                    if (trip.path.Count == 0) Land(trip);
                    break;
                case 3:
                    if (trip.path.Count == 0)
                    {
                        Destroy(trip.boat);
                        trips.RemoveAt(i);
                    }
                    break;
            }
        }
    }

    private void SailAlong(Trip trip)
    {
        var t = trip.boat.transform;
        float step = trip.speed * Time.deltaTime;
        while (trip.path.Count > 0 && step > 0f)
        {
            Vector3 target = trip.path[0];
            target.z = t.position.z;
            float d = Vector2.Distance(t.position, target);
            if (Mathf.Abs(target.x - t.position.x) > 0.01f) trip.sprite.flipX = target.x < t.position.x;
            if (d <= step) { t.position = target; step -= d; trip.path.RemoveAt(0); }
            else { t.position = Vector3.MoveTowards(t.position, target, step); step = 0f; }
        }
        var p = t.position;
        t.position = new Vector3(p.x, p.y, BuildingView.DepthFor(p.y));
    }

    private void Animate(Trip trip, bool moving)
    {
        var frames = moving && trip.moving.Count > 0 ? trip.moving : trip.idle;
        if (frames.Count == 0) return;
        trip.sprite.sprite = frames[(int)(Time.time * 6f) % frames.Count];
    }

    private void Board(Trip trip)
    {
        var boatPos = trip.boat.transform.position;
        for (int k = trip.waiting.Count - 1; k >= 0; k--)
        {
            var t = trip.waiting[k];
            if (t == null || !t.IsAlive) { trip.waiting.RemoveAt(k); continue; }
            if (Vector2.Distance(t.transform.position, trip.pickupLand) < 1.8f || Vector2.Distance(t.transform.position, boatPos) < 2.2f)
            {
                Puff.Spawn(t.transform.position, 0.35f, new Color(0.75f, 0.9f, 1f, 0.8f), 4, 0.5f);
                t.gameObject.SetActive(false);
                trip.waiting.RemoveAt(k);
                trip.aboard.Add(t);
                GameAudio.Play("river_splash", boatPos, 0.4f);
            }
        }
        bool timedOut = Time.time - trip.stageAt > 25f;
        if (trip.waiting.Count > 0 && !timedOut) return;
        // Whoever didn't make it carries on with what you told them.
        foreach (var t in trip.waiting) if (t != null && trip.goal.TryGetValue(t, out var g)) OrderInternal(t, g);
        trip.waiting.Clear();
        if (trip.aboard.Count == 0)
        {
            trip.stage = 3;
            trip.path = RiverNav.FindPath(Map, boatPos, trip.homeWater, out _, 40000);
            return;
        }
        trip.stage = 2;
        trip.path = RiverNav.FindPath(Map, boatPos, trip.dropWater, out _, 40000);
    }

    private void Land(Trip trip)
    {
        foreach (var t in trip.aboard)
        {
            if (t == null) continue;
            Vector2 jitter = Random.insideUnitCircle * 0.6f;
            var spot = trip.dropLand + new Vector3(jitter.x, jitter.y, 0);
            if (!Map.IsWalkable(spot)) spot = trip.dropLand;
            spot.z = t.transform.position.z;
            t.transform.position = spot;
            t.gameObject.SetActive(true);
            Puff.Spawn(spot, 0.35f, new Color(0.75f, 0.9f, 1f, 0.8f), 4, 0.5f);
            if (trip.goal.TryGetValue(t, out var g)) OrderInternal(t, g);
        }
        GameAudio.Play("river_splash", trip.boat.transform.position, 0.6f);
        trip.aboard.Clear();
        trip.stage = 3;
        trip.path = RiverNav.FindPath(Map, trip.boat.transform.position, trip.homeWater, out _, 40000);
    }

    /// <summary>The ferry is gone: put anyone aboard back on dry land where it was.</summary>
    private void ReleaseAll(Trip trip)
    {
        var nav = MapNavigator.Current;
        foreach (var t in trip.aboard)
        {
            if (t == null) continue;
            var at = nav != null ? nav.NearestWalkable(trip.pickupLand) : trip.pickupLand;
            at.z = t.transform.position.z;
            t.transform.position = at;
            t.gameObject.SetActive(true);
        }
        trip.aboard.Clear();
    }

    /// <summary>Troops on a ferry right now (for the HUD or anyone who asks).</summary>
    public int TroopsAboard
    {
        get
        {
            int n = 0;
            foreach (var t in trips) n += t.aboard.Count;
            return n;
        }
    }

    // =====================================================================
    // Naval raids
    // =====================================================================

    private class NavalRaid
    {
        public readonly List<Enemy> boats = new List<Enemy>();
        public int boatCount;
        public Vector3 start, landWater, landLand;
        public float setOutAt;
        public bool landed;
        public int sunk;
    }

    private NavalRaid raid;
    private readonly List<(Enemy boat, Vector3 home, float until)> leaving = new List<(Enemy, Vector3, float)>();

    /// <summary>The raid's boats are still at sea: the raiders aren't ashore yet.</summary>
    public static bool HoldingRaid => Instance != null && Instance.raid != null && !Instance.raid.landed;

    /// <summary>
    /// FreeModeController.SpawnWave hook: maybe this raid comes by sea. Returns where the raiders
    /// will come ashore (or 'origin' unchanged for a raid on foot).
    /// </summary>
    public static Vector3 ConsiderNavalRaid(bool openRaid, Vector3 origin, int raiders)
    {
        if (!openRaid || Instance == null) return origin;
        return Instance.TryNavalRaid(origin, raiders);
    }

    private Vector3 TryNavalRaid(Vector3 origin, int raiders)
    {
        if (raid != null) return origin;
        var lib = RiverLibrary.Load();
        if (lib == null || lib.raidBoats.Count == 0) return origin;
        var profile = Map.Settings?.profile;
        float chance = profile != null && profile.id == "Ocean" ? 0.65f : 0.3f;
        if (Random.value > chance) return origin;
        if (!FindLanding(out var landWater, out var landLand, out var start)) return origin;
        int tier = Mathf.Clamp(RiverCatalog.RaidBoatTier(S.State.ageIndex), 0, lib.raidBoats.Count - 1);
        var def = lib.raidBoats[tier];
        if (def == null || def.prefab == null) return origin;
        raid = new NavalRaid { start = start, landWater = landWater, landLand = landLand, setOutAt = Time.time };
        int count = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(1, raiders) / 5f), 1, 4);
        for (int i = 0; i < count; i++)
        {
            Vector3 at = start;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                Vector2 j = Random.insideUnitCircle * 2.5f;
                var p = start + new Vector3(j.x, j.y, 0);
                if (RiverNav.Sailable(Map, p)) { at = p; break; }
            }
            var go = controller.SpawnUnit(def.prefab, at);
            var boat = go.GetComponent<Enemy>();
            if (boat == null) { Destroy(go); continue; }
            Vector2 spread = Random.insideUnitCircle * 0.8f;
            var target = landWater + new Vector3(spread.x, spread.y, 0);
            boat.StartGuard(RiverNav.Sailable(Map, target) ? target : landWater, 0.5f);
            boat.InLiveWave = true;
            raid.boats.Add(boat);
        }
        raid.boatCount = raid.boats.Count;
        if (raid.boatCount == 0) { raid = null; return origin; }
        controller.ShowMessage(raid.boatCount == 1 ? "A raiders' boat is coming in from the sea!" : $"{raid.boatCount} raiders' boats are coming in from the sea!");
        GameAudio.UI("river_raid_horn");
        return landLand;
    }

    /// <summary>
    /// Where a raid could row in: a stretch of shore outside every safe zone, reachable by water
    /// from open sea a good way out, and by land from the town it's after.
    /// </summary>
    private bool FindLanding(out Vector3 landWater, out Vector3 landLand, out Vector3 start)
    {
        landWater = landLand = start = default;
        var zones = S.SettlementZones();
        var towns = zones.FindAll(z => !z.IsOutpost);
        if (towns.Count == 0) return false;
        for (int attempt = 0; attempt < 8; attempt++)
        {
            var zone = towns[Random.Range(0, towns.Count)];
            if (zone.buildings.Count == 0) continue;
            var from = zone.buildings[Random.Range(0, zone.buildings.Count)];
            float angle = Random.value * Mathf.PI * 2f;
            var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            // Out from the town to the first water.
            Vector2Int? firstWater = null;
            for (float d = 0; d < zone.safeDistance + 40f; d += 1f)
            {
                var p = from.center + dir * d;
                var c = Cell(p);
                if (!Map.InBounds(c.x, c.y)) break;
                if (RiverNav.Sailable(Map, c.x, c.y)) { firstWater = c; break; }
            }
            if (firstWater == null) continue;
            // Along the water from there: the nearest bank outside the safe zones, and how far out the sea goes.
            if (!SeaFrom(firstWater.Value, zones, out landWater, out landLand, out start)) continue;
            var path = MapNavigator.Current?.FindPath(landLand, zone.centre, Team.Enemy);
            if (path != null && path.Count > 0 && !zone.Near(path[path.Count - 1], 2f)) continue;
            return true;
        }
        return false;
    }

    private bool SeaFrom(Vector2Int water, List<Settlement.SettlementZone> zones, out Vector3 landWater, out Vector3 landLand, out Vector3 start)
    {
        landWater = landLand = start = default;
        var dist = new Dictionary<Vector2Int, int> { { water, 0 } };
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(water);
        bool haveBank = false;
        Vector2Int bankWater = water, far = water;
        int farD = 0;
        const int Budget = 9000;
        while (queue.Count > 0 && dist.Count < Budget)
        {
            var t = queue.Dequeue();
            int d = dist[t];
            if (d > farD && d <= 34) { farD = d; far = t; }
            for (int i = 0; i < 4; i++)
            {
                var nb = t + (i == 0 ? Vector2Int.right : i == 1 ? Vector2Int.left : i == 2 ? Vector2Int.up : Vector2Int.down);
                if (!haveBank && Map.IsWalkable(nb.x, nb.y) && Map.GetTerrain(nb.x, nb.y) != TerrainType.Water)
                {
                    var bankPoint = new Vector2(nb.x + 0.5f, nb.y + 0.5f);
                    float clear = float.MaxValue;
                    foreach (var z in zones) clear = Mathf.Min(clear, z.Clearance(bankPoint));
                    if (clear >= 1f)
                    {
                        haveBank = true;
                        bankWater = t;
                        landLand = new Vector3(bankPoint.x, bankPoint.y, 0);
                    }
                }
                if (dist.ContainsKey(nb) || !RiverNav.Sailable(Map, nb.x, nb.y)) continue;
                dist[nb] = d + 1;
                queue.Enqueue(nb);
            }
        }
        if (!haveBank || farD < 16) return false;
        // Set out from open sea well away from the landing.
        start = new Vector3(far.x + 0.5f, far.y + 0.5f, 0);
        if (Vector2.Distance(start, new Vector2(bankWater.x, bankWater.y)) < 10f) return false;
        landWater = new Vector3(bankWater.x + 0.5f, bankWater.y + 0.5f, 0);
        return true;
    }

    /// <summary>
    /// FreeModeController.SpawnRoutine hook, once the boats are in: drop the raiders who drowned
    /// with sunk boats, and come ashore where they landed. Returns the new spawn origin.
    /// </summary>
    public static Vector3 LandRaid(List<UnitDefinition> unspawned, Vector3 origin)
    {
        var w = Instance;
        if (w == null || w.raid == null) return origin;
        var r = w.raid;
        w.raid = null;
        if (r.sunk > 0 && unspawned != null && unspawned.Count > 0)
        {
            int drowned = r.sunk >= r.boatCount ? unspawned.Count : Mathf.RoundToInt(unspawned.Count * r.sunk / (float)r.boatCount);
            drowned = Mathf.Clamp(drowned, 0, unspawned.Count);
            for (int i = 0; i < drowned; i++) unspawned.RemoveAt(unspawned.Count - 1);
            if (drowned > 0)
                w.controller.ShowMessage(unspawned.Count == 0
                    ? "Every raiders' boat went down: the raid drowned before it reached the shore!"
                    : $"A raiders' boat went down with {drowned} raider{(drowned == 1 ? "" : "s")} aboard!");
        }
        if (unspawned != null && unspawned.Count > 0) GameAudio.Play("river_splash", r.landLand, 0.7f);
        return r.landLand;
    }

    private void TickNavalRaid()
    {
        // Boats that have dropped their raiders row back out to sea and are gone.
        for (int i = leaving.Count - 1; i >= 0; i--)
        {
            var (boat, home, until) = leaving[i];
            if (boat == null || !boat.IsAlive) { leaving.RemoveAt(i); continue; }
            if (Vector2.Distance(boat.transform.position, home) < 1.5f || Time.time > until)
            {
                Destroy(boat.gameObject);
                leaving.RemoveAt(i);
            }
        }
        if (raid == null || raid.landed) return;
        int alive = 0;
        bool ashore = false;
        foreach (var b in raid.boats)
        {
            if (b == null || !b.IsAlive) continue;
            alive++;
            if (Vector2.Distance(b.transform.position, raid.landWater) < 1.6f) ashore = true;
        }
        raid.sunk = raid.boatCount - alive;
        if (alive == 0 && raid.boatCount > 0) GameAudio.Play("river_boat_sink", raid.landWater, 0.8f);
        if (alive == 0 || ashore || Time.time - raid.setOutAt > 90f)
        {
            raid.landed = true;
            foreach (var b in raid.boats)
            {
                if (b == null || !b.IsAlive) continue;
                b.InLiveWave = false;
                b.StartGuard(raid.start, 0.5f);
                leaving.Add((b, raid.start, Time.time + 45f));
            }
        }
    }
}
