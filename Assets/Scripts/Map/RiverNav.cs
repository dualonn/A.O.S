using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Getting about by boat: A* over open water (8 directions, no cutting corners past land), for
/// warships, ferries and raiders' boats. Bridges and fords block boats. Boats that end up aground
/// head straight for the nearest open water. Plans are kept per boat and re-made when the goal
/// moves, every few seconds, or when bridges change (the map's WallVersion).
/// </summary>
public static class RiverNav
{
    private const int Window = 256;
    private const int MaxExpansions = 14000;
    private const int SearchesPerFrame = 4;

    private static readonly float[] cost = new float[Window * Window];
    private static readonly int[] parent = new int[Window * Window];
    private static readonly int[] stamp = new int[Window * Window];
    private static readonly List<(int node, float pri)> heap = new List<(int, float)>();
    private static int searchId, searchFrame = -1, searchesThisFrame;

    private class Plan
    {
        public readonly List<Vector3> path = new List<Vector3>();
        public Vector3 goal, askedGoal, snappedGoal;
        public float plannedAt = -99f, lineAt = -99f, retryAfter = -99f;
        public bool lineClear, snapKnown;
        public int walls = -1;
        public Vector3 lineFrom;
    }

    private static readonly Dictionary<Unit, Plan> plans = new Dictionary<Unit, Plan>();
    private static float nextSweep;

    // Domain reload is off in this project, so clear statics at each Play.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        plans.Clear();
        searchFrame = -1;
        searchesThisFrame = 0;
        nextSweep = 0f;
    }

    private static MapData Map => Settlement.Instance != null ? Settlement.Instance.Map : null;

    // ---------- Tiles ----------

    /// <summary>Open water a boat can be on: explored, water, and no bridge or ford over it.</summary>
    public static bool Sailable(MapData map, int x, int y) =>
        map != null && map.InBounds(x, y) && map.GetTerrain(x, y) == TerrainType.Water && map.WallAt(x, y) == WallTile.None;

    public static bool Sailable(MapData map, Vector2 p) => Sailable(map, Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y));

    /// <summary>Land a unit can stand on beside the water (bridges count).</summary>
    public static bool Dry(MapData map, int x, int y) => map != null && map.IsWalkable(x, y);

    /// <summary>The centre of the nearest open-water tile to 'p' within 'maxRadius' tiles.</summary>
    public static bool NearestSailable(MapData map, Vector3 p, int maxRadius, out Vector3 water)
    {
        int px = Mathf.FloorToInt(p.x), py = Mathf.FloorToInt(p.y);
        water = p;
        if (Sailable(map, px, py)) return true;
        for (int r = 1; r <= maxRadius; r++)
        {
            float best = float.MaxValue;
            bool found = false;
            for (int y = -r; y <= r; y++)
                for (int x = -r; x <= r; x++)
                {
                    if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(y)) != r || !Sailable(map, px + x, py + y)) continue;
                    float d = x * x + y * y;
                    if (d < best) { best = d; water = new Vector3(px + x + 0.5f, py + y + 0.5f, p.z); found = true; }
                }
            if (found) return true;
        }
        return false;
    }

    /// <summary>A straight run over open water (with a little room either side so hulls don't clip the shore).</summary>
    public static bool ClearLine(MapData map, Vector3 a, Vector3 b)
    {
        Vector2 delta = b - a;
        float distance = delta.magnitude;
        if (distance < 0.0001f) return Sailable(map, b);
        Vector2 side = new Vector2(-delta.y, delta.x) / distance * 0.25f;
        int steps = Mathf.CeilToInt(distance / 0.2f);
        for (int i = 1; i <= steps; i++)
        {
            float t = i / (float)steps;
            Vector2 p = Vector2.Lerp(a, b, t);
            if (!Sailable(map, p)) return false;
            float along = t * distance;
            if (along > 0.4f && distance - along > 0.4f && (!Sailable(map, p + side) || !Sailable(map, p - side))) return false;
        }
        return true;
    }

    // ---------- Searching ----------

    /// <summary>
    /// Waypoints over open water from 'from' to 'to' (the last one is 'to', or the nearest point the
    /// search reached). 'reached' is false when the water there doesn't connect.
    /// </summary>
    public static List<Vector3> FindPath(MapData map, Vector3 from, Vector3 to, out bool reached, int maxExpansions = MaxExpansions)
    {
        var result = new List<Vector3>();
        reached = false;
        if (map == null) return result;
        Vector2 reach = to - from;
        float limit = Window * 0.5f - 4f;
        if (Mathf.Abs(reach.x) > limit * 2f || Mathf.Abs(reach.y) > limit * 2f)
        {
            float k = limit * 2f / Mathf.Max(Mathf.Abs(reach.x), Mathf.Abs(reach.y));
            to = from + (Vector3)(reach * k);
        }
        Vector3 mid = (from + to) * 0.5f;
        int ox = Mathf.FloorToInt(mid.x) - Window / 2, oy = Mathf.FloorToInt(mid.y) - Window / 2;
        int Index(Vector3 p)
        {
            int lx = Mathf.FloorToInt(p.x) - ox, ly = Mathf.FloorToInt(p.y) - oy;
            return lx < 0 || ly < 0 || lx >= Window || ly >= Window ? -1 : ly * Window + lx;
        }
        bool Open(int lx, int ly) => lx >= 0 && ly >= 0 && lx < Window && ly < Window && Sailable(map, lx + ox, ly + oy);
        int start = Index(from), goal = Index(to);
        if (start < 0 || goal < 0) return result;
        searchId++;
        heap.Clear();
        cost[start] = 0; parent[start] = -1; stamp[start] = searchId;
        int gx = goal % Window, gy = goal / Window;
        float H(int n)
        {
            int dx = Mathf.Abs(n % Window - gx), dy = Mathf.Abs(n / Window - gy);
            return Mathf.Max(dx, dy) + 0.414f * Mathf.Min(dx, dy);
        }
        int closest = start;
        float closestH = H(start);
        Push(start, closestH);
        int expansions = 0;
        while (heap.Count > 0 && expansions++ < maxExpansions)
        {
            int cur = Pop();
            if (cur == goal) { reached = true; break; }
            float h = H(cur);
            if (h < closestH) { closestH = h; closest = cur; }
            int cx = cur % Window, cy = cur / Window;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = cx + dx, ny = cy + dy;
                    if (!Open(nx, ny)) continue;
                    if (dx != 0 && dy != 0 && (!Open(nx, cy) || !Open(cx, ny))) continue;
                    int next = ny * Window + nx;
                    float c = cost[cur] + (dx != 0 && dy != 0 ? 1.414f : 1f);
                    if (stamp[next] != searchId || c < cost[next])
                    {
                        stamp[next] = searchId; cost[next] = c; parent[next] = cur;
                        Push(next, c + H(next));
                    }
                }
        }
        int node = reached ? goal : closest;
        var tiles = new List<int>();
        while (node >= 0) { tiles.Add(node); node = parent[node]; }
        tiles.Reverse();
        Vector3 Centre(int n) => new Vector3(n % Window + ox + 0.5f, n / Window + oy + 0.5f, from.z);
        Vector3 anchor = from;
        for (int i = 1; i < tiles.Count; i++)
        {
            Vector3 candidate = Centre(tiles[i]);
            if ((candidate - anchor).sqrMagnitude > 24f * 24f || !ClearLine(map, anchor, candidate))
            {
                Vector3 previous = Centre(tiles[i - 1]);
                result.Add(previous);
                anchor = previous;
            }
        }
        result.Add(reached && Sailable(map, to) ? new Vector3(to.x, to.y, from.z) : Centre(tiles[tiles.Count - 1]));
        return result;
    }

    /// <summary>Is there open water connecting these two points (within the search window)?</summary>
    public static bool Connected(MapData map, Vector3 from, Vector3 to)
    {
        if (!NearestSailable(map, from, 3, out var a) || !NearestSailable(map, to, 3, out var b)) return false;
        FindPath(map, a, b, out bool reached, 40000);
        return reached;
    }

    private static void Push(int node, float pri)
    {
        heap.Add((node, pri));
        int i = heap.Count - 1;
        while (i > 0)
        {
            int p = (i - 1) / 2;
            if (heap[p].pri <= heap[i].pri) break;
            (heap[p], heap[i]) = (heap[i], heap[p]);
            i = p;
        }
    }

    private static int Pop()
    {
        int top = heap[0].node;
        int last = heap.Count - 1;
        heap[0] = heap[last];
        heap.RemoveAt(last);
        int i = 0;
        while (true)
        {
            int l = i * 2 + 1, r = l + 1, s = i;
            if (l < heap.Count && heap[l].pri < heap[s].pri) s = l;
            if (r < heap.Count && heap[r].pri < heap[s].pri) s = r;
            if (s == i) break;
            (heap[s], heap[i]) = (heap[i], heap[s]);
            i = s;
        }
        return top;
    }

    private static bool TakeSearch()
    {
        if (searchFrame != Time.frameCount) { searchFrame = Time.frameCount; searchesThisFrame = 0; }
        if (searchesThisFrame >= SearchesPerFrame) return false;
        searchesThisFrame++;
        return true;
    }

    // ---------- Sailing (Unit.MoveTowards for boats) ----------

    /// <summary>
    /// One frame of sailing towards 'destination' (snapped to the nearest open water) by up to
    /// 'maxDelta'. Returns the new position; 'heading' is the point being steered for and
    /// 'arrived' is true once the boat is there.
    /// </summary>
    public static Vector3 Sail(Unit boat, Vector3 destination, float maxDelta, out Vector3 heading, out bool arrived)
    {
        var map = Map;
        Vector3 pos = boat.transform.position;
        destination.z = pos.z;
        arrived = false;
        if (map == null)
        {
            heading = destination;
            var np = Vector3.MoveTowards(pos, destination, maxDelta);
            arrived = Vector2.Distance(np, destination) < 0.05f;
            return np;
        }
        Sweep();
        if (!plans.TryGetValue(boat, out var plan))
        {
            plan = new Plan();
            plans[boat] = plan;
        }
        // Aground (launched from a slipway, or shoved onto the bank): back into the water.
        if (!Sailable(map, pos))
        {
            if (NearestSailable(map, pos, 16, out var wet))
            {
                heading = wet;
                return Vector3.MoveTowards(pos, wet, Mathf.Max(maxDelta, 0.02f));
            }
            heading = pos;
            arrived = true;
            return pos;
        }
        // The goal on the water: worked out again only when it moves.
        if (!plan.snapKnown || (plan.askedGoal - destination).sqrMagnitude > 0.25f)
        {
            plan.askedGoal = destination;
            plan.snapKnown = true;
            plan.snappedGoal = NearestSailable(map, destination, 14, out var g) ? g : pos;
        }
        Vector3 goal = plan.snappedGoal;
        goal.z = pos.z;
        float now = Time.unscaledTime;
        if (now - plan.lineAt > 0.2f || (plan.lineFrom - pos).sqrMagnitude > 1f || (plan.goal - goal).sqrMagnitude > 0.25f)
        {
            plan.lineClear = ClearLine(map, pos, goal);
            plan.lineAt = now;
            plan.lineFrom = pos;
        }
        Vector3 next;
        if (plan.lineClear)
        {
            plan.path.Clear();
            plan.goal = goal;
            next = goal;
        }
        else
        {
            bool moved = Vector2.Distance(plan.goal, goal) > 1.5f;
            bool stale = now - plan.plannedAt > 3f;
            bool walls = plan.walls != map.WallVersion;
            if ((plan.path.Count == 0 || moved || stale || walls) && (now >= plan.retryAfter || walls || moved) && TakeSearch())
            {
                var found = FindPath(map, pos, goal, out bool reached);
                plan.path.Clear();
                plan.path.AddRange(found);
                plan.goal = goal;
                plan.plannedAt = now;
                plan.walls = map.WallVersion;
                plan.retryAfter = reached ? -99f : now + 4f;
            }
            while (plan.path.Count > 1 && Vector2.Distance(pos, plan.path[0]) < 0.3f) plan.path.RemoveAt(0);
            next = plan.path.Count > 0 ? plan.path[0] : goal;
        }
        next.z = pos.z;
        heading = next;
        Vector3 step = Vector3.MoveTowards(pos, next, maxDelta);
        Vector3 result = pos;
        if (Sailable(map, step)) result = step;
        else
        {
            Vector3 move = step - pos;
            var sx = new Vector3(pos.x + move.x, pos.y, pos.z);
            var sy = new Vector3(pos.x, pos.y + move.y, pos.z);
            bool xFirst = Mathf.Abs(move.x) >= Mathf.Abs(move.y);
            Vector3 first = xFirst ? sx : sy, second = xFirst ? sy : sx;
            result = Sailable(map, first) ? first : Sailable(map, second) ? second : pos;
        }
        arrived = Vector2.Distance(result, goal) < 0.08f || (plan.path.Count <= 1 && !plan.lineClear && Vector2.Distance(result, next) < 0.08f && plan.retryAfter > now);
        return result;
    }

    /// <summary>Forget boats that are gone (every few seconds).</summary>
    private static void Sweep()
    {
        if (Time.unscaledTime < nextSweep) return;
        nextSweep = Time.unscaledTime + 5f;
        var dead = new List<Unit>();
        foreach (var u in plans.Keys) if (u == null || !u.IsAlive) dead.Add(u);
        foreach (var u in dead) plans.Remove(u);
    }
}
