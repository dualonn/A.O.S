using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds a free mode map from a seed: grassland with lakes, rivers and hills, then
/// deposit patches (forest, berries, stone, ore...). The same seed always gives the same map.
/// </summary>
public static class MapGenerator
{
    public static MapData Generate(int seed, GameDatabase db)
    {
        int w = db.mapSize.x;
        int h = db.mapSize.y;
        var deposits = new List<DepositType>(db.deposits.FindAll(d => d != null));
        var map = new MapData(w, h, seed, deposits);
        var rng = new System.Random(seed);

        float hx = Offset(rng), hy = Offset(rng), mx = Offset(rng), my = Offset(rng);
        float[] height = new float[w * h];
        float[] moisture = new float[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                height[y * w + x] = Fbm(x * db.terrainScale + hx, y * db.terrainScale + hy, 5);
                moisture[y * w + x] = Fbm(x * db.terrainScale * 1.6f + mx, y * db.terrainScale * 1.6f + my, 3);
            }
        }
        Stretch(height);
        Stretch(moisture);

        // Lakes are the lowest share of the map, hills the highest.
        float waterLine = Percentile(height, db.waterShare);
        float hillLine = Percentile(height, 1f - db.hillShare);
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float v = height[y * w + x];
                map.SetTerrain(x, y, v <= waterLine ? TerrainType.Water : v >= hillLine ? TerrainType.Hill : TerrainType.Grass);
            }
        }

        for (int i = 0; i < db.riverCount; i++)
        {
            CarveRiver(map, height, rng);
        }

        PlaceDeposits(map, moisture, deposits, rng);
        return map;
    }

    // ---------- Rivers ----------

    /// <summary>Start high up and flow downhill until reaching water or the map edge.</summary>
    private static void CarveRiver(MapData map, float[] height, System.Random rng)
    {
        int w = map.Width, h = map.Height;
        // Pick a fairly high starting tile.
        int sx = 0, sy = 0;
        float best = -1;
        for (int i = 0; i < 40; i++)
        {
            int x = rng.Next(4, w - 4), y = rng.Next(4, h - 4);
            if (height[y * w + x] > best)
            {
                best = height[y * w + x];
                sx = x;
                sy = y;
            }
        }

        var visited = new HashSet<int>();
        int cx = sx, cy = sy;
        int[] dx = { 1, -1, 0, 0 };
        int[] dy = { 0, 0, 1, -1 };
        for (int step = 0; step < w * 2; step++)
        {
            if (step > 3 && map.GetTerrain(cx, cy) == TerrainType.Water)
            {
                return; // reached a lake or another river
            }
            map.SetTerrain(cx, cy, TerrainType.Water);
            if (rng.NextDouble() < 0.35)
            {
                // Occasionally two tiles wide.
                int k = rng.Next(4);
                if (map.InBounds(cx + dx[k], cy + dy[k]))
                {
                    map.SetTerrain(cx + dx[k], cy + dy[k], TerrainType.Water);
                }
            }
            visited.Add(cy * w + cx);
            if (cx == 0 || cy == 0 || cx == w - 1 || cy == h - 1)
            {
                return; // flowed off the map
            }

            // Mostly downhill, with some wander.
            int nextX = -1, nextY = -1;
            float lowest = float.MaxValue;
            for (int k = 0; k < 4; k++)
            {
                int nx = cx + dx[k], ny = cy + dy[k];
                if (!map.InBounds(nx, ny) || visited.Contains(ny * w + nx))
                {
                    continue;
                }
                float v = height[ny * w + nx] + (float)rng.NextDouble() * 0.04f;
                if (v < lowest)
                {
                    lowest = v;
                    nextX = nx;
                    nextY = ny;
                }
            }
            if (nextX < 0)
            {
                Pond(map, cx, cy, 2);
                return;
            }
            // Stuck in a dip: pool into a small lake and keep going.
            if (lowest > height[cy * w + cx] + 0.06f)
            {
                Pond(map, cx, cy, 2);
            }
            cx = nextX;
            cy = nextY;
        }
    }

    private static void Pond(MapData map, int cx, int cy, int r)
    {
        for (int y = -r; y <= r; y++)
        {
            for (int x = -r; x <= r; x++)
            {
                if (x * x + y * y <= r * r)
                {
                    map.SetTerrain(cx + x, cy + y, TerrainType.Water);
                }
            }
        }
    }

    // ---------- Deposits ----------

    private static void PlaceDeposits(MapData map, float[] moisture, List<DepositType> deposits, System.Random rng)
    {
        int w = map.Width;
        foreach (var type in deposits)
        {
            float ox = Offset(rng), oy = Offset(rng);
            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (map.GetDeposit(x, y) != null)
                    {
                        continue;
                    }
                    var t = map.GetTerrain(x, y);
                    if (t == TerrainType.Water || (t == TerrainType.Hill) != type.onHills)
                    {
                        continue;
                    }
                    float m = moisture[y * w + x];
                    if (m < type.minMoisture || m > type.maxMoisture)
                    {
                        continue;
                    }
                    float patch = Mathf.PerlinNoise(x * type.patchScale + ox, y * type.patchScale + oy);
                    // A little speckle so patch edges aren't perfectly smooth.
                    patch += ((float)rng.NextDouble() - 0.5f) * 0.08f;
                    if (patch > type.rarity)
                    {
                        map.SetDeposit(x, y, type);
                    }
                }
            }
        }
    }

    // ---------- Noise helpers ----------

    private static float Offset(System.Random rng) => (float)rng.NextDouble() * 10000f;

    private static float Fbm(float x, float y, int octaves)
    {
        float sum = 0, amp = 1, norm = 0, freq = 1;
        for (int i = 0; i < octaves; i++)
        {
            sum += Mathf.PerlinNoise(x * freq, y * freq) * amp;
            norm += amp;
            amp *= 0.5f;
            freq *= 2f;
        }
        return sum / norm;
    }

    /// <summary>Rescale values to span 0..1.</summary>
    private static void Stretch(float[] values)
    {
        float min = float.MaxValue, max = float.MinValue;
        foreach (float v in values)
        {
            min = Mathf.Min(min, v);
            max = Mathf.Max(max, v);
        }
        float range = Mathf.Max(1e-5f, max - min);
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = (values[i] - min) / range;
        }
    }

    private static float Percentile(float[] values, float share)
    {
        var copy = (float[])values.Clone();
        Array.Sort(copy);
        int index = Mathf.Clamp(Mathf.RoundToInt(share * (copy.Length - 1)), 0, copy.Length - 1);
        return copy[index];
    }
}
