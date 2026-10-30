using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pathfinding over the free mode map: A* on tiles, 8 directions, water blocked,
/// hills cost more. Paths are smoothed so units walk in straight lines where they can.
/// </summary>
public class MapNavigator
{
    /// <summary>Set while a free mode map is active; null elsewhere (units then walk straight).</summary>
    public static MapNavigator Current { get; set; }

    // Domain reload is off in this project, so clear statics at each Play.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Current = null;

    private const int MaxExpansions = 12000;

    private readonly MapData map;
    private readonly float hillSpeed;

    // Reused search buffers
    private readonly float[] cost;
    private readonly int[] parent;
    private readonly int[] stamp;
    private int searchId;

    public MapNavigator(MapData map, float hillSpeed)
    {
        this.map = map;
        this.hillSpeed = hillSpeed;
        int n = map.Width * map.Height;
        cost = new float[n];
        parent = new int[n];
        stamp = new int[n];
    }

    public bool IsWalkable(Vector3 world) => map.IsWalkable(world);

    /// <summary>Movement speed multiplier at a position (slower on hills).</summary>
    public float SpeedFactor(Vector3 world) =>
        map.GetTerrain(Mathf.FloorToInt(world.x), Mathf.FloorToInt(world.y)) == TerrainType.Hill ? hillSpeed : 1f;

    /// <summary>True if a straight walk from a to b never crosses water.</summary>
    public bool ClearLine(Vector3 a, Vector3 b)
    {
        float distance = Vector2.Distance(a, b);
        int steps = Mathf.CeilToInt(distance / 0.35f);
        for (int i = 1; i <= steps; i++)
        {
            if (!map.IsWalkable(Vector3.Lerp(a, b, i / (float)steps)))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// World-space waypoints from 'from' to 'to' (the last one is 'to' itself, or the nearest
    /// reachable tile to it). Empty if no route was found.
    /// </summary>
    public List<Vector3> FindPath(Vector3 from, Vector3 to)
    {
        var result = new List<Vector3>();
        int w = map.Width;
        int start = TileIndex(from);
        int goal = TileIndex(NearestWalkable(to));
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
                    if (!map.IsWalkable(nx, ny))
                    {
                        continue;
                    }
                    // No cutting corners past water.
                    if (dx != 0 && dy != 0 && (!map.IsWalkable(cx + dx, cy) || !map.IsWalkable(cx, cy + dy)))
                    {
                        continue;
                    }
                    int next = ny * w + nx;
                    float step = (dx != 0 && dy != 0 ? 1.414f : 1f) * map.MoveCost(nx, ny);
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
            if (!ClearLine(anchor, candidate))
            {
                Vector3 previous = Center(tiles[i - 1]);
                result.Add(previous);
                anchor = previous;
            }
        }
        result.Add(found && map.IsWalkable(to) ? new Vector3(to.x, to.y, 0) : Center(tiles[tiles.Count - 1]));
        return result;
    }

    private Vector3 NearestWalkable(Vector3 p)
    {
        if (map.IsWalkable(p))
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
                    if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(y)) == r && map.IsWalkable(px + x, py + y))
                    {
                        return new Vector3(px + x + 0.5f, py + y + 0.5f, 0);
                    }
                }
            }
        }
        return p;
    }

    private int TileIndex(Vector3 p)
    {
        int x = Mathf.FloorToInt(p.x), y = Mathf.FloorToInt(p.y);
        return map.InBounds(x, y) ? y * map.Width + x : -1;
    }

    private Vector3 Center(int index) => new Vector3(index % map.Width + 0.5f, index / map.Width + 0.5f, 0);

    private float Heuristic(int a, int b)
    {
        int ax = a % map.Width, ay = a / map.Width, bx = b % map.Width, by = b / map.Width;
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

    /// <summary>Where to head next. Without a navigator (levels), that's simply the goal.</summary>
    public Vector3 NextPoint(Vector3 position, Vector3 goal)
    {
        var nav = MapNavigator.Current;
        if (nav == null)
        {
            return goal;
        }
        // Straight shot available: no need to plan.
        if (Vector2.Distance(position, goal) < 2f || nav.ClearLine(position, goal))
        {
            path.Clear();
            return goal;
        }
        bool stale = Time.time - plannedAt > 2f;
        if (path.Count == 0 || stale || Vector2.Distance(plannedGoal, goal) > 1.5f)
        {
            path = nav.FindPath(position, goal);
            plannedGoal = goal;
            plannedAt = Time.time;
        }
        while (path.Count > 1 && Vector2.Distance(position, path[0]) < 0.3f)
        {
            path.RemoveAt(0);
        }
        return path.Count > 0 ? path[0] : goal;
    }
}
