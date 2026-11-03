using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds the free mode map from a seed, one chunk at a time. Every tile depends only on its
/// position and the seed, so chunks generated later line up seamlessly with earlier ones:
/// grassland, lakes, hills, meandering rivers and deposit patches all continue across chunks.
/// </summary>
public static class MapGenerator
{
    /// <summary>Per-map generation constants, fixed when the map is created.</summary>
    public class Settings
    {
        public float hx, hy, mx, my, rx, ry;
        public float heightMin, heightMax, moistMin, moistMax;
        public float waterLine, hillLine;
        public float riverWidth;
        public float terrainScale;
        public float[] depositX, depositY;
        // Biomes (drawn last from the seed so nothing above changes for old maps)
        public float tx, ty, wx, wy, ex, ey;
        public Vector2 home;
        /// <summary>The kind of world (biome rules, water and hills, deposits, colours).</summary>
        public WorldProfile profile = WorldProfile.Home;
    }

    /// <summary>How wide a biome region is (lower = bigger regions).</summary>
    private const float BiomeScale = 0.0032f;
    /// <summary>The noise values each landing biome is pulled towards round the landing site.</summary>
    private static Vector3 HomeLean(Biome b) => b switch
    {
        Biome.Desert => new Vector3(0.75f, 0.4f, 0.5f),
        Biome.Tundra => new Vector3(0.2f, 0.5f, 0.5f),
        Biome.Swamp => new Vector3(0.5f, 0.8f, 0.5f),
        Biome.Volcanic => new Vector3(0.5f, 0.5f, 0.1f),
        Biome.Crystal => new Vector3(0.5f, 0.5f, 0.9f),
        // (barren, jungle and fungal lands come from a world's swaps: they lean like the land they replace)
        _ => new Vector3(0.5f, 0.5f, 0.5f),
    };

    /// <summary>
    /// The biome at a tile: from large, slow noise for warmth, wetness and wildness (the strange
    /// lands). Only changes the look and life of the land, never its terrain or deposits, and the
    /// starting area is always temperate, so existing settlements are untouched.
    /// </summary>
    public static Biome BiomeAt(Settings s, int x, int y)
    {
        if (s == null) return Biome.Temperate;
        var p = s.profile ?? WorldProfile.Home;
        float d = Vector2.Distance(new Vector2(x, y), s.home);
        if (d < p.homeRadius) return p.homeBiome;
        float k = Mathf.Clamp01((d - p.homeRadius) / p.homeBlend);
        // Near the landing site the land leans to the world's home biome; further out, the noise
        // (shifted by the world's own leanings) takes over. (The home world leans to temperate.)
        Vector3 lean = HomeLean(p.homeBiome);
        float warm = Mathf.Lerp(lean.x, Mathf.PerlinNoise(x * BiomeScale + s.tx, y * BiomeScale + s.ty) + p.warmBias, k);
        float wet = Mathf.Lerp(lean.y, Mathf.PerlinNoise(x * BiomeScale * 1.3f + s.wx, y * BiomeScale * 1.3f + s.wy) + p.wetBias, k);
        float wild = Mathf.Lerp(lean.z, Mathf.PerlinNoise(x * BiomeScale * 1.8f + s.ex, y * BiomeScale * 1.8f + s.ey) + p.wildBias, k);
        // Ragged borders: a little fine noise so lands blend into each other instead of meeting in a line.
        float j = (Mathf.PerlinNoise(x * 0.09f + s.tx * 0.37f, y * 0.09f + s.ty * 0.37f) - 0.5f) * 0.05f
                + (Mathf.PerlinNoise(x * 0.31f + s.wx * 0.21f, y * 0.31f + s.wy * 0.21f) - 0.5f) * 0.025f;
        warm += j; wet -= j; wild += j;
        return p.Pick(warm, wet, wild);
    }

    private const float RiverScale = 0.011f;

    /// <summary>
    /// The starting area: the configured map size, on chunk lines. It sits where it always has
    /// (centred in the old size limit), so existing saves keep their terrain and buildings.
    /// </summary>
    public static RectInt InitialArea(GameDatabase db)
    {
        int c = Mathf.Max(4, db.chunkSize);
        int w = Mathf.CeilToInt(db.mapSize.x / (float)c) * c;
        int h = Mathf.CeilToInt(db.mapSize.y / (float)c) * c;
        Vector2Int limit = Limit(db);
        int x = Mathf.FloorToInt((limit.x - w) / 2f / c) * c;
        int y = Mathf.FloorToInt((limit.y - h) / 2f / c) * c;
        return new RectInt(Mathf.Max(0, x), Mathf.Max(0, y), Mathf.Min(w, limit.x), Mathf.Min(h, limit.y));
    }

    /// <summary>The old map size limit: only used to place the starting area and to read old saves.</summary>
    public static Vector2Int Limit(GameDatabase db) =>
        new Vector2Int(Mathf.Max(db.mapSize.x, db.mapMaxSize.x), Mathf.Max(db.mapSize.y, db.mapMaxSize.y));

    /// <summary>Zones per row in the old fixed-size map (old saves stored zones as row * this + column).</summary>
    public static int LegacyZonesPerRow(GameDatabase db) => Mathf.CeilToInt(Limit(db).x / (float)Mathf.Max(4, db.chunkSize));

    /// <summary>Create the map: the starting area plus any zones explored in a saved game.</summary>
    public static MapData Create(int seed, GameDatabase db, List<Vector2Int> savedZones, WorldProfile profile = null)
    {
        var deposits = new List<DepositType>(db.deposits.FindAll(d => d != null));
        var map = new MapData(seed, Mathf.Max(4, db.chunkSize), deposits);
        var initial = InitialArea(db);
        map.Settings = BuildSettings(seed, db, initial, deposits.Count, profile ?? WorldProfile.Home);

        var zones = new List<Vector2Int>();
        int c = map.ChunkSize;
        for (int cy = initial.yMin / c; cy < Mathf.CeilToInt(initial.yMax / (float)c); cy++)
        {
            for (int cx = initial.xMin / c; cx < Mathf.CeilToInt(initial.xMax / (float)c); cx++)
            {
                zones.Add(new Vector2Int(cx, cy));
            }
        }
        if (savedZones != null)
        {
            zones.AddRange(savedZones);
        }
        RevealZones(map, zones);
        return map;
    }

    /// <summary>
    /// Explore zones: generate them if needed and make them part of the map.
    /// Returns the tile rects of zones that just became explored (for drawing).
    /// </summary>
    public static List<RectInt> RevealZones(MapData map, IEnumerable<Vector2Int> zones)
    {
        var shown = new List<RectInt>();
        foreach (var z in zones)
        {
            if (!map.ZoneInLimit(z.x, z.y) || map.IsZoneRevealed(z.x, z.y))
            {
                continue;
            }
            var rect = map.ZoneRect(z.x, z.y);
            if (!map.IsChunkGenerated(z.x, z.y))
            {
                GenerateArea(map, rect);
                map.MarkChunkGenerated(z.x, z.y);
            }
            map.SetZoneRevealed(z.x, z.y);
            shown.Add(rect);
        }
        return shown;
    }

    // ---------- Settings ----------

    /// <summary>Fix noise offsets and levels by sampling the starting area once.</summary>
    private static Settings BuildSettings(int seed, GameDatabase db, RectInt sampleArea, int depositCount, WorldProfile profile)
    {
        var rng = new System.Random(seed);
        var s = new Settings
        {
            hx = Offset(rng), hy = Offset(rng), mx = Offset(rng), my = Offset(rng), rx = Offset(rng), ry = Offset(rng),
            terrainScale = db.terrainScale * profile.terrainScaleMultiplier,
            riverWidth = 0.0045f * Mathf.Max(0, profile.Rivers(db)),
            profile = profile,
            depositX = new float[depositCount],
            depositY = new float[depositCount]
        };
        for (int i = 0; i < depositCount; i++)
        {
            s.depositX[i] = Offset(rng);
            s.depositY[i] = Offset(rng);
        }

        var heights = new List<float>();
        var moists = new List<float>();
        for (int y = sampleArea.yMin; y < sampleArea.yMax; y += 2)
        {
            for (int x = sampleArea.xMin; x < sampleArea.xMax; x += 2)
            {
                heights.Add(RawHeight(s, x, y));
                moists.Add(RawMoisture(s, x, y));
            }
        }
        heights.Sort();
        moists.Sort();
        s.heightMin = heights[0];
        s.heightMax = heights[heights.Count - 1];
        s.moistMin = moists[0];
        s.moistMax = moists[moists.Count - 1];
        s.waterLine = Normalize(heights[Index(heights.Count, profile.WaterShare(db))], s.heightMin, s.heightMax);
        s.hillLine = Normalize(heights[Index(heights.Count, 1f - profile.HillShare(db))], s.heightMin, s.heightMax);
        s.tx = Offset(rng); s.ty = Offset(rng); s.wx = Offset(rng); s.wy = Offset(rng); s.ex = Offset(rng); s.ey = Offset(rng);
        s.home = sampleArea.center;
        return s;
    }

    // ---------- Tiles ----------

    private static void GenerateArea(MapData map, RectInt area)
    {
        var s = map.Settings;
        for (int y = area.yMin; y < area.yMax; y++)
        {
            for (int x = area.xMin; x < area.xMax; x++)
            {
                float h = Normalize(RawHeight(s, x, y), s.heightMin, s.heightMax);
                var t = h <= s.waterLine ? TerrainType.Water : h >= s.hillLine ? TerrainType.Hill : TerrainType.Grass;

                // Rivers: a thin band along a noise contour meanders endlessly across chunks.
                if (t != TerrainType.Hill && s.riverWidth > 0)
                {
                    float r = Mathf.PerlinNoise(x * RiverScale + s.rx, y * RiverScale + s.ry);
                    if (Mathf.Abs(r - 0.5f) < s.riverWidth)
                    {
                        t = TerrainType.Water;
                    }
                }
                map.SetTerrain(x, y, t);
                map.SetDeposit(x, y, null);
                if (t == TerrainType.Water)
                {
                    continue;
                }

                float m = Normalize(RawMoisture(s, x, y), s.moistMin, s.moistMax);
                for (int i = 0; i < map.Deposits.Count; i++)
                {
                    var type = map.Deposits[i];
                    if ((t == TerrainType.Hill) != type.onHills || m < type.minMoisture || m > type.maxMoisture)
                    {
                        continue;
                    }
                    float patch = Mathf.PerlinNoise(x * type.patchScale + s.depositX[i], y * type.patchScale + s.depositY[i]);
                    patch += (Hash01(x, y, map.Seed, i) - 0.5f) * 0.08f; // ragged edges
                    if (patch > type.rarity - (s.profile != null ? s.profile.DepositBoost(type.id) : 0f))
                    {
                        map.SetDeposit(x, y, type);
                        break;
                    }
                }
            }
        }
    }

    // ---------- Noise helpers ----------

    private static float RawHeight(Settings s, int x, int y) => Fbm(x * s.terrainScale + s.hx, y * s.terrainScale + s.hy, 5);

    private static float RawMoisture(Settings s, int x, int y) =>
        Fbm(x * s.terrainScale * 1.6f + s.mx, y * s.terrainScale * 1.6f + s.my, 3);

    private static float Normalize(float v, float min, float max) => Mathf.Clamp01((v - min) / Mathf.Max(1e-5f, max - min));

    private static int Index(int count, float share) => Mathf.Clamp(Mathf.RoundToInt(share * (count - 1)), 0, count - 1);

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

    /// <summary>A stable pseudo-random 0..1 per tile (same answer whenever the chunk is generated).</summary>
    private static float Hash01(int x, int y, int seed, int salt)
    {
        unchecked
        {
            uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)(seed * 83492791) ^ (uint)(salt * 2654435761u);
            h ^= h >> 13;
            h *= 0x5bd1e995;
            h ^= h >> 15;
            return (h & 0xFFFFFF) / 16777216f;
        }
    }
}
