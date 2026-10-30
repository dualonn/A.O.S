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
/// x..x+1, y..y+1. Generated from a seed by MapGenerator, so it is never saved itself.
/// </summary>
public class MapData
{
    public readonly int Width;
    public readonly int Height;
    public readonly int Seed;
    public readonly List<DepositType> Deposits;

    private readonly TerrainType[] terrain;
    private readonly byte[] deposit; // 0 = none, otherwise index + 1 into Deposits

    public MapData(int width, int height, int seed, List<DepositType> deposits)
    {
        Width = width;
        Height = height;
        Seed = seed;
        Deposits = deposits;
        terrain = new TerrainType[width * height];
        deposit = new byte[width * height];
    }

    public RectInt Bounds => new RectInt(0, 0, Width, Height);

    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

    public TerrainType GetTerrain(int x, int y) => InBounds(x, y) ? terrain[y * Width + x] : TerrainType.Water;

    public void SetTerrain(int x, int y, TerrainType t)
    {
        if (InBounds(x, y))
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
        int d = deposit[y * Width + x];
        return d == 0 ? null : Deposits[d - 1];
    }

    public void SetDeposit(int x, int y, DepositType type)
    {
        if (InBounds(x, y))
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
