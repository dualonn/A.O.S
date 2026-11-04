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

    // Most trips are planned at once by the quick search. One that needs a long way round (a river
    // with its crossing far off) is handed to the full search, which runs a little each frame.
    private const int LongExpansions = Window * Window / 2;
    private const double LongMsPerFrame = 1.5;
    private readonly Search quick, full;
    private readonly Queue<PathFollower> longQueue = new Queue<PathFollower>();
    private PathFollower longFor;
    private readonly System.Diagnostics.Stopwatch longClock = new System.Diagnostics.Stopwatch();

    public MapNavigator(MapData map, float hillSpeed)
    {
        this.map = map;
        this.hillSpeed = hillSpeed;
        quick = new Search(map);
        full = new Search(map);
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
        TradeRoads.Speed(map, Mathf.FloorToInt(world.x), Mathf.FloorToInt(world.y), hillSpeed);   // roads, rail, hills

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
    /// reachable tile to it). Empty if no route was found. A quick search: a trip that needs a
    /// very long way round comes back unfinished (see RequestLongPath).
    /// </summary>
    public List<Vector3> FindPath(Vector3 from, Vector3 to, Team team = Team.Player) => FindPath(from, to, team, out _);

    public List<Vector3> FindPath(Vector3 from, Vector3 to, Team team, out bool reached)
    {
        quick.Begin(from, NearestWalkable(to, team), to, team, MaxExpansions, 1f, this);
        quick.Run(int.MaxValue);
        return quick.Result(out reached);
    }

    /// <summary>Plan this follower's trip in full, in the background (a little each frame).</summary>
    public void RequestLongPath(PathFollower follower)
    {
        if (follower.longQueued) return;
        follower.longQueued = true;
        longQueue.Enqueue(follower);
    }

    /// <summary>Run the background search for up to a moment of real time (once a frame).</summary>
    public void PumpLong()
    {
        longClock.Restart();
        while (longClock.Elapsed.TotalMilliseconds < LongMsPerFrame)
        {
            if (longFor == null)
            {
                if (longQueue.Count == 0) return;
                var next = longQueue.Dequeue();
                if (next.Abandoned) { next.longQueued = false; continue; }
                longFor = next;
                // A little greedy (weight 1.2): far fewer tiles looked at, paths barely longer.
                full.Begin(next.askedFrom, NearestWalkable(next.askedGoal, next.askedTeam), next.askedGoal, next.askedTeam, LongExpansions, 1.2f, this);
            }
            if (full.Run(768))
            {
                var path = full.Result(out bool reached);
                var f = longFor;
                longFor = null;
                f.longQueued = false;
                f.ReceiveLong(path, reached, full.Asked);
            }
        }
    }

    // Trade routes (TradeWorks): a long search of their own, run a slice at a time.
    private Search routeSearch;

    public void BeginRouteSearch(Vector3 from, Vector3 to)
    {
        routeSearch ??= new Search(map);
        routeSearch.Begin(from, NearestWalkable(to, Team.Player), to, Team.Player, LongExpansions, 1.2f, this);
    }

    /// <summary>Search on for up to 'budget' tiles; true once done ('path' is then the way, or as near as it got).</summary>
    public bool RunRouteSearch(int budget, out List<Vector3> path, out bool reached)
    {
        path = null;
        reached = false;
        if (routeSearch == null || !routeSearch.Run(budget)) return routeSearch == null;
        path = routeSearch.Result(out reached);
        return true;
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

    /// <summary>
    /// One A* search over the window round a trip, with its own buffers, so the quick search and
    /// the background one can both be under way. Each tile's terrain is looked up once per search
    /// and remembered, which is most of what makes searching fast.
    /// </summary>
    private class Search
    {
        private readonly MapData map;
        private readonly float[] cost = new float[Window * Window];
        private readonly int[] parent = new int[Window * Window];
        private readonly int[] stamp = new int[Window * Window];
        private readonly byte[] info = new byte[Window * Window];
        private readonly int[] infoStamp = new int[Window * Window];
        private readonly MinHeap open = new MinHeap();
        private int id, originX, originY, start, goal, closest, expansions, maxExpansions;
        private float closestH, weight;
        private bool found, done, breaksWalls;
        private Team team;
        private Vector3 from, goalPoint;
        private MapNavigator nav;

        /// <summary>Where the trip was asked to go (before snapping to dry land).</summary>
        public Vector3 Asked { get; private set; }

        private const byte Clear = 1, Dry = 2, Hill = 4, Road = 8;

        public Search(MapData map) { this.map = map; }

        public void Begin(Vector3 from, Vector3 goalPoint, Vector3 asked, Team team, int maxExpansions, float weight, MapNavigator nav)
        {
            this.nav = nav;
            this.from = from;
            this.team = team;
            this.maxExpansions = maxExpansions;
            this.weight = weight;
            Asked = asked;
            // Centre the window on the trip; a goal beyond it is approached as far as the window goes.
            Vector2 reach = goalPoint - from;
            float limit = Window * 0.5f - 4f;
            if (Mathf.Abs(reach.x) > limit * 2f || Mathf.Abs(reach.y) > limit * 2f)
            {
                float k = limit * 2f / Mathf.Max(Mathf.Abs(reach.x), Mathf.Abs(reach.y));
                goalPoint = nav.NearestWalkable(from + (Vector3)(reach * k), team);
            }
            this.goalPoint = goalPoint;
            Vector3 mid = (from + goalPoint) * 0.5f;
            originX = Mathf.FloorToInt(mid.x) - Window / 2;
            originY = Mathf.FloorToInt(mid.y) - Window / 2;
            // Raiders may plan through walls (at a cost); everyone else goes round them.
            breaksWalls = team == Team.Enemy;
            id++;
            open.Clear();
            expansions = 0;
            found = false;
            start = TileIndex(from);
            goal = TileIndex(goalPoint);
            done = start < 0 || goal < 0;
            if (done) return;
            cost[start] = 0;
            parent[start] = -1;
            stamp[start] = id;
            closest = start;
            closestH = Heuristic(start, goal);
            open.Push(start, closestH * weight);
        }

        /// <summary>What we know of a window tile: passable, dry, hill.</summary>
        private byte Info(int lx, int ly)
        {
            int i = ly * Window + lx;
            if (infoStamp[i] == id) return info[i];
            int wx = lx + originX, wy = ly + originY;
            byte b = 0;
            if (map.IsWalkable(wx, wy))
            {
                b |= Dry;
                if (map.Passable(wx, wy, team)) b |= Clear;
                if (map.GetTerrain(wx, wy) == TerrainType.Hill) b |= Hill;
                if (TradeRoads.IsRoadTile(map.WallAt(wx, wy))) b |= Road;
            }
            info[i] = b;
            infoStamp[i] = id;
            return b;
        }

        private bool PassableAt(Vector2 p)
        {
            int wx = Mathf.FloorToInt(p.x), wy = Mathf.FloorToInt(p.y);
            int lx = wx - originX, ly = wy - originY;
            if (lx < 0 || ly < 0 || lx >= Window || ly >= Window) return map.Passable(wx, wy, team);
            return (Info(lx, ly) & Clear) != 0;
        }

        /// <summary>Search on for up to 'budget' more tiles. True once finished (found, or given up).</summary>
        public bool Run(int budget)
        {
            const int w = Window;
            while (!done && budget-- > 0)
            {
                if (open.Count == 0 || expansions++ >= maxExpansions) { done = true; break; }
                int current = open.Pop();
                if (open.LastPriority > cost[current] + Heuristic(current, goal) * weight + 0.001f) { expansions--; continue; }   // stale entry
                if (current == goal) { found = true; done = true; break; }
                int cx = current % w, cy = current / w;
                float h = Heuristic(current, goal);
                if (h < closestH) { closestH = h; closest = current; }
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = cx + dx, ny = cy + dy;
                        if (nx < 0 || ny < 0 || nx >= w || ny >= w) continue;
                        byte inf = Info(nx, ny);
                        bool clear = (inf & Clear) != 0;
                        if (!clear && !(breaksWalls && (inf & Dry) != 0)) continue;
                        // No cutting corners past water or walls.
                        if (dx != 0 && dy != 0 && ((Info(nx, cy) & Clear) == 0 || (Info(cx, ny) & Clear) == 0)) continue;
                        int next = ny * w + nx;
                        float step = (dx != 0 && dy != 0 ? 1.414f : 1f) * ((inf & Road) != 0 ? 0.7f : (inf & Hill) != 0 ? 2f : 1f) + (clear ? 0f : WallBreakCost);
                        float newCost = cost[current] + step;
                        if (stamp[next] != id || newCost < cost[next])
                        {
                            stamp[next] = id;
                            cost[next] = newCost;
                            parent[next] = current;
                            open.Push(next, newCost + Heuristic(next, goal) * weight);
                        }
                    }
                }
            }
            return done;
        }

        /// <summary>The waypoints found (to the goal, or as near it as the search got).</summary>
        public List<Vector3> Result(out bool reached)
        {
            var result = new List<Vector3>();
            reached = found;
            if (start < 0 || goal < 0) return result;
            int node = found ? goal : closest;
            var tiles = new List<int>();
            while (node >= 0) { tiles.Add(node); node = parent[node]; }
            tiles.Reverse();
            // Smooth: keep only the points needed to stay off water (and a point every so often on
            // a long straight, so checking lines stays cheap).
            Vector3 anchor = from;
            for (int i = 1; i < tiles.Count; i++)
            {
                Vector3 candidate = Center(tiles[i]);
                if ((candidate - anchor).sqrMagnitude > 28f * 28f || !ClearLine(anchor, candidate))
                {
                    Vector3 previous = Center(tiles[i - 1]);
                    result.Add(previous);
                    anchor = previous;
                }
            }
            result.Add(found && PassableAt(goalPoint) ? new Vector3(goalPoint.x, goalPoint.y, 0) : Center(tiles[tiles.Count - 1]));
            return result;
        }

        /// <summary>Same test as MapNavigator.ClearLine, on the tiles this search already knows.</summary>
        private bool ClearLine(Vector3 a, Vector3 b)
        {
            Vector2 delta = b - a;
            float distance = delta.magnitude;
            if (distance < 0.0001f) return PassableAt(b);
            Vector2 side = new Vector2(-delta.y, delta.x) / distance * 0.25f;
            int steps = Mathf.CeilToInt(distance / 0.15f);
            for (int i = 1; i <= steps; i++)
            {
                float t = i / (float)steps;
                Vector2 p = Vector2.Lerp(a, b, t);
                if (!PassableAt(p)) return false;
                float along = t * distance;
                if (along > 0.35f && distance - along > 0.35f && (!PassableAt(p + side) || !PassableAt(p - side))) return false;
            }
            return true;
        }

        /// <summary>Index of a world tile inside the search window (-1 if outside it or unexplored).</summary>
        private int TileIndex(Vector3 p)
        {
            int x = Mathf.FloorToInt(p.x), y = Mathf.FloorToInt(p.y);
            int lx = x - originX, ly = y - originY;
            if (lx < 0 || ly < 0 || lx >= Window || ly >= Window || !map.InBounds(x, y)) return -1;
            return ly * Window + lx;
        }

        private Vector3 Center(int index) => new Vector3(index % Window + originX + 0.5f, index / Window + originY + 0.5f, 0);

        private static float Heuristic(int a, int b)
        {
            int ax = a % Window, ay = a / Window, bx = b % Window, by = b / Window;
            int dx = Mathf.Abs(ax - bx), dy = Mathf.Abs(ay - by);
            return Mathf.Max(dx, dy) + 0.414f * Mathf.Min(dx, dy);
        }
    }

    /// <summary>A small binary min-heap of (node, priority).</summary>
    private class MinHeap
    {
        private readonly List<(int node, float priority)> items = new List<(int, float)>();
        public int Count => items.Count;

        public void Clear() => items.Clear();

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

        /// <summary>The priority of the entry Pop last returned.</summary>
        public float LastPriority { get; private set; }

        public int Pop()
        {
            int top = items[0].node;
            LastPriority = items[0].priority;
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
/// re-planning when the goal moves or the old plan gets stale. A trip that needs a long way
/// round (the crossing of a river far off) is planned in full in the background, and kept.
/// </summary>
public class PathFollower
{
    private List<Vector3> path = new List<Vector3>();
    private Vector3 plannedGoal;
    private float plannedAt = -99f;
    private int plannedWalls = -1;
    private float retryAfter = -99f;   // after a trip found to be cut off, wait before trying again
    private bool longPlan;             // the plan we're on came from the full search

    // For the background search: the trip as last asked, and whether one is queued for us.
    internal bool longQueued;
    internal Vector3 askedFrom, askedGoal;
    internal Team askedTeam;
    private float askedAt;

    /// <summary>Not asked for directions for a while (the unit stopped, or died): drop its search.</summary>
    internal bool Abandoned => Time.unscaledTime - askedAt > 3f;

    // The straight-line check is costly over long distances: reuse its answer for a moment.
    private Vector3 lineFrom, lineGoal;
    private bool lineClear;
    private float lineAt = -99f;

    // A* searches are the most expensive thing units do. Only a few run each frame; the rest keep
    // their old plan (or head straight on) and try again next frame, so a crowd re-planning at
    // once is spread out instead of freezing the game.
    private const int SearchesPerFrame = 6;
    private const double SearchMsPerFrame = 3.0;
    private static int searchFrame = -1, searchesThisFrame, pumpFrame = -1;
    private static readonly System.Diagnostics.Stopwatch searchClock = new System.Diagnostics.Stopwatch();

    // Domain reload is off in this project, so clear statics at each Play.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { searchFrame = -1; searchesThisFrame = 0; pumpFrame = -1; }

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
        float now = Time.unscaledTime;
        askedFrom = position; askedGoal = goal; askedTeam = team; askedAt = now;
        if (pumpFrame != Time.frameCount)
        {
            pumpFrame = Time.frameCount;
            UnityEngine.Profiling.Profiler.BeginSample("PathFollower.LongSearch");
            nav.PumpLong();
            UnityEngine.Profiling.Profiler.EndSample();
        }
        // Straight shot available: no need to plan. (Checked again every 0.15 real seconds, or when things move a lot.)
        if (now - lineAt > 0.15f || (lineGoal - goal).sqrMagnitude > 0.25f || (lineFrom - position).sqrMagnitude > 1f)
        {
            lineClear = nav.ClearLine(position, goal, team);
            lineAt = now; lineGoal = goal; lineFrom = position;
        }
        if (lineClear)
        {
            path.Clear();
            longPlan = false;
            return goal;
        }
        // Plans go stale on real time, so a faster game doesn't mean more searches per second.
        // A long way round, once found, is kept until the goal moves off or the walls change.
        bool wallsChanged = plannedWalls != nav.WallVersion;
        bool stale = !longPlan && now - plannedAt > 2f;
        bool goalMoved = Vector2.Distance(plannedGoal, goal) > (longPlan ? 4f : 1.5f);
        if ((path.Count == 0 || stale || goalMoved || wallsChanged) && (now >= retryAfter || wallsChanged) && !longQueued && TakeSearch())
        {
            UnityEngine.Profiling.Profiler.BeginSample("PathFollower.FindPath");
            searchClock.Start();
            var found = nav.FindPath(position, goal, team, out bool reached);
            searchClock.Stop();
            UnityEngine.Profiling.Profiler.EndSample();
            if (reached)
            {
                path = found;
                longPlan = false;
            }
            else
            {
                // Too far round for a quick search (or cut off): plan the whole way in the background.
                // Meanwhile head as near as the quick search got, unless we're already on a long plan.
                if (!longPlan || path.Count == 0) path = found;
                nav.RequestLongPath(this);
            }
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

    /// <summary>The background search is done: take its route (or, cut off, the nearest it got).</summary>
    internal void ReceiveLong(List<Vector3> found, bool reached, Vector3 goal)
    {
        float now = Time.unscaledTime;
        if (found.Count > 0) path = found;
        longPlan = reached;
        plannedGoal = goal;
        plannedAt = now;
        // Cut off for real (an island, unexplored land between): don't keep searching.
        retryAfter = reached ? -99f : now + 8f;
    }
}
