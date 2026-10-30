using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dresses the free mode map: draws the enemy path, the rally flag, and scatters trees,
/// rocks and bushes around (never on the settlement or the path). Same layout every time.
/// </summary>
public static class WorldDecorator
{
    private const float PathWidth = 1.6f;

    public static void Decorate(Transform parent, SettlementGrid grid, WayPoints path, Transform rallyPoint, int seed = 1234)
    {
        var art = ArtLibrary.Load();
        var root = new GameObject("World").transform;
        root.SetParent(parent, false);

        var pathPoints = new List<Vector3>();
        if (path != null)
        {
            foreach (Transform child in path.transform)
            {
                pathPoints.Add(child.position);
            }
            DrawPath(root, pathPoints, art);
        }

        if (rallyPoint != null && art != null && art.rallyFlag != null)
        {
            var flag = Prop(root, art.rallyFlag, rallyPoint.position + new Vector3(0.6f, 0.4f, 0));
            flag.name = "RallyFlag";
        }

        if (art == null)
        {
            return;
        }

        var rng = new System.Random(seed);
        Rect area = grid.WorldArea;
        var settlement = new Rect(grid.transform.position, (Vector2)grid.Size * grid.cellSize);
        settlement.min -= Vector2.one * 1.5f;
        settlement.max += Vector2.one * 1.5f;

        int placed = 0;
        for (int attempt = 0; attempt < 2500 && placed < 420; attempt++)
        {
            var pos = new Vector3(
                area.xMin + (float)rng.NextDouble() * area.width,
                area.yMin + (float)rng.NextDouble() * area.height,
                0);
            if (settlement.Contains(pos) || NearPath(pos, pathPoints, PathWidth) ||
                (rallyPoint != null && Vector2.Distance(pos, rallyPoint.position) < 3f))
            {
                continue;
            }

            double roll = rng.NextDouble();
            List<Sprite> pool = roll < 0.6 ? art.trees : roll < 0.8 ? art.bushes : art.rocks;
            if (pool == null || pool.Count == 0)
            {
                continue;
            }
            var sprite = pool[rng.Next(pool.Count)];
            var prop = Prop(root, sprite, pos);
            float scale = 0.85f + (float)rng.NextDouble() * 0.35f;
            prop.transform.localScale = new Vector3(rng.NextDouble() < 0.5 ? -scale : scale, scale, 1);
            placed++;
        }
    }

    private static SpriteRenderer Prop(Transform parent, Sprite sprite, Vector3 pos)
    {
        var sr = new GameObject(sprite.name).AddComponent<SpriteRenderer>();
        sr.transform.SetParent(parent, false);
        // Lower props are nearer the viewer, so draw them in front (same sorting order, nearer z).
        sr.transform.position = new Vector3(pos.x, pos.y, pos.y * 0.01f);
        sr.sprite = sprite;
        sr.sortingOrder = -10;
        return sr;
    }

    private static void DrawPath(Transform parent, List<Vector3> points, ArtLibrary art)
    {
        var pathRoot = new GameObject("Path").transform;
        pathRoot.SetParent(parent, false);
        for (int i = 0; i < points.Count - 1; i++)
        {
            Vector2 a = points[i];
            Vector2 b = points[i + 1];
            if (i == 0)
            {
                // Extend the first segment off into the distance.
                a -= (b - a).normalized * 30f;
            }
            Vector2 dir = (b - a).normalized;
            Vector2 perp = new Vector2(-dir.y, dir.x);
            float half = PathWidth / 2;

            var sr = new GameObject($"Segment {i}").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(pathRoot, false);
            sr.sprite = art != null && art.pathTile != null ? art.pathTile : SimpleSprites.Square;
            if (art == null || art.pathTile == null)
            {
                sr.color = new Color(0.6f, 0.48f, 0.3f);
            }
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = new Vector2((b - a).magnitude + half * 2, PathWidth);
            Vector2 origin = a - dir * half - perp * half;
            sr.transform.position = origin;
            sr.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            sr.sortingOrder = -18;
        }
    }

    private static bool NearPath(Vector3 pos, List<Vector3> points, float width)
    {
        for (int i = 0; i < points.Count - 1; i++)
        {
            Vector2 a = points[i];
            Vector2 b = points[i + 1];
            if (i == 0)
            {
                a -= (b - a).normalized * 30f;
            }
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot((Vector2)pos - a, ab) / ab.sqrMagnitude);
            if (Vector2.Distance(pos, a + ab * t) < width + 0.6f)
            {
                return true;
            }
        }
        return false;
    }
}
