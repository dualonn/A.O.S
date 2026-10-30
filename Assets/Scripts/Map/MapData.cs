using System.Collections.Generic;
using UnityEngine;

public enum TerrainType : byte
{
    Grass,
    Water,
    Hill
}

/// <summary>
/// The free mode map: terrain and natural deposits per tile. Tile (x, y) covers world
/// x..x+1, y..y+1. The arrays cover the whole size limit (Width x Height), but only the
/// Revealed area exists in play; it grows chunk by chunk as the player explores.
/// Generated from a seed by MapGenerator, so tiles are never saved, only the seed and Revealed.
/// </summary>
public class MapData
{
    /// <summary>Size of the whole map limit, in tiles.</summary>
    public readonly int Width;
    public readonly int Height;
    public readonly int Seed;
    public readonly int ChunkSize;
    public readonly List<DepositType> Deposits;

    /// <summary>
    /// Bounding box of the explored zones (for camera limits). The explored land itself is
    /// an irregular set of zones: use IsZoneRevealed / InBounds for what actually exists.
    /// </summary>
    public RectInt Revealed { get; private set; }

    /// <summary>Generation settings fixed when the map was created, reused for every new chunk.</summary>
    public MapGenerator.Settings Settings { get; set; }

    private readonly TerrainType[] terrain;
    private readonly byte[] deposit; // 0 = none, otherwise index + 1 into Deposits
    private readonly bool[] chunkDone;
    private readonly bool[] zoneRevealed;

    public MapData(int width, int height, int seed, int chunkSize, List<DepositType> deposits)
    {
        Width = width;
        Height = height;
        Seed = seed;
        ChunkSize = chunkSize;
        Deposits = deposits;
        terrain = new TerrainType[width * height];
        deposit = new byte[width * height];
        chunkDone = new bool[ChunksX * ChunksY];
        zoneRevealed = new bool[ChunksX * ChunksY];
    }

    // ---------- Zones (the map grows one zone = one chunk at a time) ----------

    public bool ZoneInLimit(int cx, int cy) => cx >= 0 && cy >= 0 && cx < ChunksX && cy < ChunksY;

    public bool IsZoneRevealed(int cx, int cy) => ZoneInLimit(cx, cy) && zoneRevealed[cy * ChunksX + cx];

    public int ZoneIndex(int cx, int cy) => cy * ChunksX + cx;

    public Vector2Int ZoneOf(int x, int y) => new Vector2Int(Mathf.FloorToInt(x / (float)ChunkSize), Mathf.FloorToInt(y / (float)ChunkSize));

    public RectInt ZoneRect(int cx, int cy) =>
        new RectInt(cx * ChunkSize, cy * ChunkSize, Mathf.Min(ChunkSize, Width - cx * ChunkSize), Mathf.Min(ChunkSize, Height - cy * ChunkSize));

    /// <summary>Mark a (generated) zone as explored and widen the bounding box.</summary>
    public void SetZoneRevealed(int cx, int cy)
    {
        if (!ZoneInLimit(cx, cy) || zoneRevealed[ZoneIndex(cx, cy)])
        {
            return;
        }
        zoneRevealed[ZoneIndex(cx, cy)] = true;
        var z = ZoneRect(cx, cy);
        if (Revealed.width == 0)
        {
            Revealed = z;
        }
        else
        {
            int xMin = Mathf.Min(Revealed.xMin, z.xMin), yMin = Mathf.Min(Revealed.yMin, z.yMin);
            int xMax = Mathf.Max(Revealed.xMax, z.xMax), yMax = Mathf.Max(Revealed.yMax, z.yMax);
            Revealed = new RectInt(xMin, yMin, xMax - xMin, yMax - yMin);
        }
    }

    /// <summary>Explored zones, as indices (for saving).</summary>
    public List<int> RevealedZoneIndices()
    {
        var list = new List<int>();
        for (int i = 0; i < zoneRevealed.Length; i++)
        {
            if (zoneRevealed[i])
            {
                list.Add(i);
            }
        }
        return list;
    }

    /// <summary>True if an explored zone touches unexplored land (or the limit) on that side.</summary>
    public bool IsFrontier(int cx, int cy, int dx, int dy) => IsZoneRevealed(cx, cy) && !IsZoneRevealed(cx + dx, cy + dy);

    public int ChunksX => Mathf.CeilToInt(Width / (float)ChunkSize);
    public int ChunksY => Mathf.CeilToInt(Height / (float)ChunkSize);

    public RectInt Bounds => Revealed;
    public RectInt Limit => new RectInt(0, 0, Width, Height);

    /// <summary>True for tiles that exist in play (in an explored zone).</summary>
    public bool InBounds(int x, int y) => InArray(x, y) && zoneRevealed[(y / ChunkSize) * ChunksX + x / ChunkSize];

    private bool InArray(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

    public bool IsChunkGenerated(int cx, int cy) => chunkDone[cy * ChunksX + cx];

    public void MarkChunkGenerated(int cx, int cy) => chunkDone[cy * ChunksX + cx] = true;

    /// <summary>Terrain of a tile. Unrevealed tiles read as water (impassable).</summary>
    public TerrainType GetTerrain(int x, int y) => InBounds(x, y) ? terrain[y * Width + x] : TerrainType.Water;

    /// <summary>Terrain whether or not it is revealed yet (for drawing freshly generated chunks).</summary>
    public TerrainType RawTerrain(int x, int y) => InArray(x, y) ? terrain[y * Width + x] : TerrainType.Water;

    public void SetTerrain(int x, int y, TerrainType t)
    {
        if (InArray(x, y))
        {
            terrain[y * Width + x] = t;
        }
    }

    public DepositType GetDeposit(int x, int y)
    {
        if (!InBounds(x, y))
        {
            return null;
        }
        return RawDeposit(x, y);
    }

    public DepositType RawDeposit(int x, int y)
    {
        if (!InArray(x, y))
        {
            return null;
        }
        int d = deposit[y * Width + x];
        return d == 0 ? null : Deposits[d - 1];
    }

    public void SetDeposit(int x, int y, DepositType type)
    {
        if (InArray(x, y))
        {
            deposit[y * Width + x] = (byte)(type == null ? 0 : Deposits.IndexOf(type) + 1);
        }
    }

    public bool IsWalkable(int x, int y) => InBounds(x, y) && GetTerrain(x, y) != TerrainType.Water;

    public bool IsWalkable(Vector3 world) => IsWalkable(Mathf.FloorToInt(world.x), Mathf.FloorToInt(world.y));

    /// <summary>Cost to step onto a tile (hills are slow).</summary>
    public float MoveCost(int x, int y) => GetTerrain(x, y) == TerrainType.Hill ? 2f : 1f;

    /// <summary>The square of tiles a building at 'area' gathers from: its footprint plus 'radius' all round.</summary>
    public static RectInt Catchment(RectInt area, int radius) =>
        new RectInt(area.xMin - radius, area.yMin - radius, area.width + radius * 2, area.height + radius * 2);

    public int CountDeposit(DepositType type, RectInt area, int radius)
    {
        if (type == null)
        {
            return 0;
        }
        int count = 0;
        var c = Catchment(area, radius);
        for (int y = c.yMin; y < c.yMax; y++)
        {
            for (int x = c.xMin; x < c.xMax; x++)
            {
                if (GetDeposit(x, y) == type)
                {
                    count++;
                }
            }
        }
        return count;
    }

    public bool AnyWater(RectInt area, int radius)
    {
        var c = Catchment(area, radius);
        for (int y = c.yMin; y < c.yMax; y++)
        {
            for (int x = c.xMin; x < c.xMax; x++)
            {
                if (InBounds(x, y) && GetTerrain(x, y) == TerrainType.Water)
                {
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>True if every tile of 'area' is on the map and of an allowed terrain.</summary>
    public bool CanBuildOn(RectInt area, bool allowHills, out string reason)
    {
        for (int y = area.yMin; y < area.yMax; y++)
        {
            for (int x = area.xMin; x < area.xMax; x++)
            {
                if (!InBounds(x, y))
                {
                    reason = "Off the edge of the map";
                    return false;
                }
                var t = GetTerrain(x, y);
                if (t == TerrainType.Water)
                {
                    reason = "Can't build on water";
                    return false;
                }
                if (t == TerrainType.Hill && !allowHills)
                {
                    reason = "Can't build on hills";
                    return false;
                }
            }
        }
        reason = null;
        return true;
    }
}
