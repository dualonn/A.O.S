using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pathfinding over the free mode map: A* on tiles, 8 directions, water blocked,
/// hills cost more. Paths are smoothed so units walk in straight lines where they can.
/// Walls block everyone and gates block raiders. Raiders still plan through walls at a high
/// cost: they go round through any gap, and only break through when there's no way round.
/// </summary>
public class MapNavigator
{
    /// <summary>Set while a free mode map is active; null elsewhere (units then walk straight).</summary>
    public static MapNavigator Current { get; set; }

    // Domain reload is off in this project, so clear statics at each Play.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Current = null;

    private const int MaxExpansions = 12000;

    /// <summary>Extra path cost for a raider to go through a wall tile (it has to smash it).</summary>
    private const float WallBreakCost = 30f;

    private readonly MapData map;
    private readonly float hillSpeed;

    // Searches run in a fixed window of tiles around the trip (the map itself has no edge).
    private const int Window = 512;
    private int originX, originY;

    // Reused search buffers, one entry per tile of the window
    private readonly float[] cost = new float[Window * Window];
    private readonly int[] parent = new int[Window * Window];
    private readonly int[] stamp = new int[Window * Window];
    private int searchId;

    public MapNavigator(MapData map, float hillSpeed)
    {
        this.map = map;
        this.hillSpeed = hillSpeed;
    }

    public bool IsWalkable(Vector3 world, Team team = Team.Player) => map.Passable(world, team);

    private bool Passable(int x, int y, Team team) => map.Passable(x, y, team);

    private bool Passable(Vector2 p, Team team) => map.Passable(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y), team);

    public int WallVersion => map.WallVersion;

    /// <summary>
    /// The first wall tile (blocking this team) on the way from 'from' towards 'to', within
    /// 'reach'. For raiders deciding what to smash.
    /// </summary>
    public bool WallAhead(Vector3 from, Vector3 to, float reach, Team team, out Vector2Int cell)
    {
        Vector2 dir = to - from;
        float length = Mathf.Min(reach, dir.magnitude);
        cell = default;
        if (length < 0.001f)
        {
            return false;
        }
        dir.Normalize();
        for (float d = 0.1f; d <= length; d += 0.1f)
        {
            Vector2 p = (Vector2)from + dir * d;
            int x = Mathf.FloorToInt(p.x), y = Mathf.FloorToInt(p.y);
            if (map.IsWalkable(x, y) && !map.Passable(x, y, team))
            {
                cell = new Vector2Int(x, y);
                return true;
            }
        }
        return false;
    }

    /// <summary>Movement speed multiplier at a position (slower on hills).</summary>
    public float SpeedFactor(Vector3 world) =>
        map.GetTerrain(Mathf.FloorToInt(world.x), Mathf.FloorToInt(world.y)) == TerrainType.Hill ? hillSpeed : 1f;

    /// <summary>
    /// True if a straight walk from a to b never crosses water. Away from the two ends it also
    /// checks a strip either side of the line, so units don't clip the corners of water tiles.
    /// </summary>
    public bool ClearLine(Vector3 a, Vector3 b, Team team = Team.Player)
    {
        Vector2 delta = b - a;
        float distance = delta.magnitude;
        if (distance < 0.0001f)
        {
            return Passable(b, team);
        }
        Vector2 side = new Vector2(-delta.y, delta.x) / distance * 0.25f;
        int steps = Mathf.CeilToInt(distance / 0.15f);
        for (int i = 1; i <= steps; i++)
        {
            float t = i / (float)steps;
            Vector2 p = Vector2.Lerp(a, b, t);
            if (!Passable(p, team))
            {
                return false;
            }
            float along = t * distance;
            if (along > 0.35f && distance - along > 0.35f
                && (!Passable(p + side, team) || !Passable(p - side, team)))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Move from 'from' towards 'to' by up to 'maxDelta' without stepping into water: blocked
    /// steps slide along the shore instead. A unit already in water heads for the nearest dry land.
    /// </summary>
    public Vector3 Step(Vector3 from, Vector3 to, float maxDelta, Team team = Team.Player)
    {
        if (!Passable(from, team))
        {
            Vector3 dry = NearestWalkable(from, team);
            dry.z = from.z;
            return Vector3.MoveTowards(from, dry, Mathf.Max(maxDelta, 0.02f));
        }
        Vector3 next = Vector3.MoveTowards(from, to, maxDelta);
        if (Passable(next, team) && !CutsCorner(from, next, team))
        {
            return next;
        }
        // Slide along whichever axis stays dry, trying the larger part of the move first.
        Vector3 move = next - from;
        var slideX = new Vector3(from.x + move.x, from.y, from.z);
        var slideY = new Vector3(from.x, from.y + move.y, from.z);
        bool xFirst = Mathf.Abs(move.x) >= Mathf.Abs(move.y);
        Vector3 first = xFirst ? slideX : slideY, second = xFirst ? slideY : slideX;
        if (Passable(first, team))
        {
            return first;
        }
        return Passable(second, team) ? second : from;
    }

    /// <summary>Stepping diagonally into the next tile between two blocked ones (a diagonal wall).</summary>
    private bool CutsCorner(Vector3 from, Vector3 next, Team team)
    {
        int fx = Mathf.FloorToInt(from.x), fy = Mathf.FloorToInt(from.y);
        int nx = Mathf.FloorToInt(next.x), ny = Mathf.FloorToInt(next.y);
        return fx != nx && fy != ny && !Passable(nx, fy, team) && !Passable(fx, ny, team);
    }

    /// <summary>
    /// World-space waypoints from 'from' to 'to' (the last one is 'to' itself, or the nearest
    /// reachable tile to it). Empty if no route was found.
    /// </summary>
    public List<Vector3> FindPath(Vector3 from, Vector3 to, Team team = Team.Player)
    {
        var result = new List<Vector3>();
        const int w = Window;
        // Centre the window on the trip; a goal beyond it is approached as far as the window goes.
        Vector3 goalPoint = NearestWalkable(to, team);
        Vector2 reach = goalPoint - from;
        float limit = Window * 0.5f - 4f;
        if (Mathf.Abs(reach.x) > limit * 2f || Mathf.Abs(reach.y) > limit * 2f)
        {
            float k = limit * 2f / Mathf.Max(Mathf.Abs(reach.x), Mathf.Abs(reach.y));
            goalPoint = NearestWalkable(from + (Vector3)(reach * k), team);
        }
        Vector3 mid = (from + goalPoint) * 0.5f;
        originX = Mathf.FloorToInt(mid.x) - Window / 2;
        originY = Mathf.FloorToInt(mid.y) - Window / 2;
        int start = TileIndex(from);
        int goal = TileIndex(goalPoint);
        // Raiders may plan through walls (at a cost); everyone else goes round them.
        bool breaksWalls = team == Team.Enemy;
        if (start < 0 || goal < 0)
        {
            return result;
        }

        searchId++;
        var open = new MinHeap();
        cost[start] = 0;
        parent[start] = -1;
        stamp[start] = searchId;
        open.Push(start, Heuristic(start, goal));

        int closest = start;
        float closestH = Heuristic(start, goal);
        int expansions = 0;
        bool found = false;

        while (open.Count > 0 && expansions++ < MaxExpansions)
        {
            int current = open.Pop();
            if (current == goal)
            {
                found = true;
                break;
            }
            int cx = current % w, cy = current / w;
            float h = Heuristic(current, goal);
            if (h < closestH)
            {
                closestH = h;
                closest = current;
            }

            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0)
                    {
                        continue;
                    }
                    int nx = cx + dx, ny = cy + dy;
                    if (nx < 0 || ny < 0 || nx >= Window || ny >= Window)
                    {
                        continue;
                    }
                    int wx = nx + originX, wy = ny + originY;
                    bool clear = Passable(wx, wy, team);
                    if (!clear && !(breaksWalls && map.IsWalkable(wx, wy)))
                    {
                        continue;
                    }
                    // No cutting corners past water or walls.
                    if (dx != 0 && dy != 0 && (!Passable(wx, cy + originY, team) || !Passable(cx + originX, wy, team)))
                    {
                        continue;
                    }
                    int next = ny * w + nx;
                    float step = (dx != 0 && dy != 0 ? 1.414f : 1f) * map.MoveCost(wx, wy) + (clear ? 0f : WallBreakCost);
                    float newCost = cost[current] + step;
                    if (stamp[next] != searchId || newCost < cost[next])
                    {
                        stamp[next] = searchId;
                        cost[next] = newCost;
                        parent[next] = current;
                        open.Push(next, newCost + Heuristic(next, goal));
                    }
                }
            }
        }

        // Walk back from the goal (or the closest point reached if the goal was cut off).
        int node = found ? goal : closest;
        var tiles = new List<int>();
        while (node >= 0)
        {
            tiles.Add(node);
            node = parent[node];
        }
        tiles.Reverse();

        // Smooth: keep only the points needed to stay off water.
        Vector3 anchor = from;
        for (int i = 1; i < tiles.Count; i++)
        {
            Vector3 candidate = Center(tiles[i]);
            if (!ClearLine(anchor, candidate, team))
            {
                Vector3 previous = Center(tiles[i - 1]);
                result.Add(previous);
                anchor = previous;
            }
        }
        result.Add(found && Passable(goalPoint, team) ? new Vector3(goalPoint.x, goalPoint.y, 0) : Center(tiles[tiles.Count - 1]));
        return result;
    }

    /// <summary>'p' if it's dry, otherwise the centre of the nearest dry tile.</summary>
    public Vector3 NearestWalkable(Vector3 p, Team team = Team.Player)
    {
        if (Passable(p, team))
        {
            return p;
        }
        int px = Mathf.FloorToInt(p.x), py = Mathf.FloorToInt(p.y);
        for (int r = 1; r < 12; r++)
        {
            for (int y = -r; y <= r; y++)
            {
                for (int x = -r; x <= r; x++)
                {
                    if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(y)) == r && Passable(px + x, py + y, team))
                    {
                        return new Vector3(px + x + 0.5f, py + y + 0.5f, 0);
                    }
                }
            }
        }
        return p;
    }

    /// <summary>Index of a world tile inside the search window (-1 if outside it or unexplored).</summary>
    private int TileIndex(Vector3 p)
    {
        int x = Mathf.FloorToInt(p.x), y = Mathf.FloorToInt(p.y);
        int lx = x - originX, ly = y - originY;
        if (lx < 0 || ly < 0 || lx >= Window || ly >= Window || !map.InBounds(x, y))
        {
            return -1;
        }
        return ly * Window + lx;
    }

    private Vector3 Center(int index) => new Vector3(index % Window + originX + 0.5f, index / Window + originY + 0.5f, 0);

    private float Heuristic(int a, int b)
    {
        int ax = a % Window, ay = a / Window, bx = b % Window, by = b / Window;
        int dx = Mathf.Abs(ax - bx), dy = Mathf.Abs(ay - by);
        return Mathf.Max(dx, dy) + 0.414f * Mathf.Min(dx, dy);
    }

    /// <summary>A small binary min-heap of (node, priority).</summary>
    private class MinHeap
    {
        private readonly List<(int node, float priority)> items = new List<(int, float)>();
        public int Count => items.Count;

        public void Push(int node, float priority)
        {
            items.Add((node, priority));
            int i = items.Count - 1;
            while (i > 0)
            {
                int p = (i - 1) / 2;
                if (items[p].priority <= items[i].priority) break;
                (items[p], items[i]) = (items[i], items[p]);
                i = p;
            }
        }

        public int Pop()
        {
            int top = items[0].node;
            int last = items.Count - 1;
            items[0] = items[last];
            items.RemoveAt(last);
            int i = 0;
            while (true)
            {
                int l = i * 2 + 1, r = l + 1, smallest = i;
                if (l < items.Count && items[l].priority < items[smallest].priority) smallest = l;
                if (r < items.Count && items[r].priority < items[smallest].priority) smallest = r;
                if (smallest == i) break;
                (items[smallest], items[i]) = (items[i], items[smallest]);
                i = smallest;
            }
            return top;
        }
    }
}

/// <summary>
/// Per-unit helper: gives the next point to walk towards on the way to a goal,
/// re-planning when the goal moves or the old plan gets stale.
/// </summary>
public class PathFollower
{
    private List<Vector3> path = new List<Vector3>();
    private Vector3 plannedGoal;
    private float plannedAt = -99f;
    private int plannedWalls = -1;
    private float retryAfter = -99f;   // after a search that couldn't reach the goal, wait before trying again

    // The straight-line check is costly over long distances: reuse its answer for a moment.
    private Vector3 lineFrom, lineGoal;
    private bool lineClear;
    private float lineAt = -99f;

    // A* searches are the most expensive thing units do. Only a few run each frame; the rest keep
    // their old plan (or head straight on) and try again next frame, so a crowd re-planning at
    // once is spread out instead of freezing the game.
    private const int SearchesPerFrame = 6;
    private const double SearchMsPerFrame = 3.0;
    private static int searchFrame = -1, searchesThisFrame;
    private static readonly System.Diagnostics.Stopwatch searchClock = new System.Diagnostics.Stopwatch();

    private static bool TakeSearch()
    {
        if (searchFrame != Time.frameCount) { searchFrame = Time.frameCount; searchesThisFrame = 0; searchClock.Reset(); }
        if (searchesThisFrame >= SearchesPerFrame || searchClock.Elapsed.TotalMilliseconds >= SearchMsPerFrame) return false;
        searchesThisFrame++;
        return true;
    }

    /// <summary>Where to head next. Without a navigator (levels), that's simply the goal.</summary>
    public Vector3 NextPoint(Vector3 position, Vector3 goal, Team team = Team.Player)
    {
        var nav = MapNavigator.Current;
        if (nav == null)
        {
            return goal;
        }
        // Straight shot available: no need to plan. (Checked again every 0.15 real seconds, or when things move a lot.)
        float now = Time.unscaledTime;
        if (now - lineAt > 0.15f || (lineGoal - goal).sqrMagnitude > 0.25f || (lineFrom - position).sqrMagnitude > 1f)
        {
            lineClear = nav.ClearLine(position, goal, team);
            lineAt = now; lineGoal = goal; lineFrom = position;
        }
        if (lineClear)
        {
            path.Clear();
            return goal;
        }
        // Plans go stale on real time, so a faster game doesn't mean more searches per second.
        bool stale = now - plannedAt > 2f || plannedWalls != nav.WallVersion;
        bool wallsChanged = plannedWalls != nav.WallVersion;
        if ((path.Count == 0 || stale || Vector2.Distance(plannedGoal, goal) > 1.5f) && (now >= retryAfter || wallsChanged) && TakeSearch())
        {
            UnityEngine.Profiling.Profiler.BeginSample("PathFollower.FindPath");
            searchClock.Start();
            path = nav.FindPath(position, goal, team);
            searchClock.Stop();
            UnityEngine.Profiling.Profiler.EndSample();
            // No route all the way (water, or a long way round): failed searches are the costly ones, so back off.
            bool reached = path.Count > 0 && Vector2.Distance(path[path.Count - 1], goal) < 2f;
            retryAfter = reached ? -99f : now + 3f;
            plannedGoal = goal;
            plannedAt = now;
            plannedWalls = nav.WallVersion;
        }
        while (path.Count > 1 && Vector2.Distance(position, path[0]) < 0.3f)
        {
            path.RemoveAt(0);
        }
        return path.Count > 0 ? path[0] : goal;
    }
}
