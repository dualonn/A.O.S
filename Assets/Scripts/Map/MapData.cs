using System.Collections.Generic;
using UnityEngine;

/// <summary>The look and life of a region: its ground, trees and creatures (terrain types are unchanged).</summary>
public enum Biome : byte { Temperate, Desert, Tundra, Swamp, Volcanic, Crystal, Barren, Jungle, Fungal }

public enum TerrainType : byte
{
    Grass,
    Water,
    Hill
}

/// <summary>What a placed wall piece does to movement on its tile.</summary>
public enum WallTile : byte
{
    None,
    /// <summary>Blocks everyone.</summary>
    Wall,
    /// <summary>Lets your units through; blocks raiders.</summary>
    Gate,
    /// <summary>A bridge or ford over water: everyone walks over it; boats can't pass under.</summary>
    Bridge,
    /// <summary>A road on land (TradeRoads): everyone walks over it, faster.</summary>
    Road,
    /// <summary>Rail track on land: everyone walks over it; trains run on it.</summary>
    Rail,
    /// <summary>An aqueduct or pipe line (Utilities): everyone walks under or over it.</summary>
    Conduit = 20
}

/// <summary>
/// The free mode map: terrain, natural deposits and walls per tile. Tile (x, y) covers world
/// x..x+1, y..y+1. The map has no edge: it is stored as sparse square chunks (zones) that are
/// generated from the seed as they're explored, so only explored land costs memory.
/// Tiles are never saved, only the seed and which zones have been explored.
/// </summary>
public class MapData
{
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

    /// <summary>The biome at a tile (temperate round the starting area).</summary>
    public Biome BiomeAt(int x, int y) => MapGenerator.BiomeAt(Settings, x, y);

    public Biome BiomeAt(Vector3 world) => BiomeAt(Mathf.FloorToInt(world.x), Mathf.FloorToInt(world.y));

    /// <summary>Goes up whenever walls change, so units re-plan their routes.</summary>
    public int WallVersion { get; private set; }

    private class Chunk
    {
        public TerrainType[] terrain;
        public byte[] deposit; // 0 = none, otherwise index + 1 into Deposits
        public byte[] wall;    // made when the first wall goes up here
        public bool generated, revealed;
        public bool interior;  // an underground level: walkable, but not part of the explored map
    }

    private readonly Dictionary<Vector2Int, Chunk> chunks = new Dictionary<Vector2Int, Chunk>();
    private readonly List<Vector2Int> revealedZones = new List<Vector2Int>();
    private readonly List<Vector2Int> wallCells = new List<Vector2Int>();

    // The last chunk looked up: most lookups land in the same chunk as the one before.
    private Vector2Int lastKey = new Vector2Int(int.MinValue, int.MinValue);
    private Chunk lastChunk;

    public MapData(int seed, int chunkSize, List<DepositType> deposits)
    {
        Seed = seed;
        ChunkSize = chunkSize;
        Deposits = deposits;
    }

    // ---------- Chunks ----------

    /// <summary>Division that rounds down for negative numbers too.</summary>
    public static int FloorDiv(int a, int b) => a >= 0 ? a / b : -((-a + b - 1) / b);

    private Chunk Find(int x, int y)
    {
        var key = new Vector2Int(FloorDiv(x, ChunkSize), FloorDiv(y, ChunkSize));
        if (key == lastKey)
        {
            return lastChunk;
        }
        chunks.TryGetValue(key, out var chunk);
        lastKey = key;
        lastChunk = chunk;
        return chunk;
    }

    private Chunk FindOrMake(int cx, int cy)
    {
        var key = new Vector2Int(cx, cy);
        if (!chunks.TryGetValue(key, out var chunk))
        {
            int n = ChunkSize * ChunkSize;
            chunk = new Chunk { terrain = new TerrainType[n], deposit = new byte[n] };
            chunks[key] = chunk;
            if (key == lastKey) lastChunk = chunk;
        }
        return chunk;
    }

    private int Local(int x, int y) =>
        (y - FloorDiv(y, ChunkSize) * ChunkSize) * ChunkSize + (x - FloorDiv(x, ChunkSize) * ChunkSize);

    // ---------- Walls and gates ----------

    public WallTile WallAt(int x, int y)
    {
        var chunk = Find(x, y);
        return chunk?.wall != null ? (WallTile)chunk.wall[Local(x, y)] : WallTile.None;
    }

    /// <summary>Replace every wall tile with these (standing walls and gates).</summary>
    public void SetWalls(IEnumerable<(Vector2Int cell, WallTile kind)> pieces)
    {
        // Nothing changed (the usual case: Settlement.Changed fires for all sorts of things): keep the
        // version, so units don't all re-plan their paths at once.
        var incoming = new List<(Vector2Int cell, WallTile kind)>();
        foreach (var p in pieces) if (p.kind != WallTile.None) incoming.Add(p);
        if (incoming.Count == wallCells.Count)
        {
            bool same = true;
            foreach (var (cell, kind) in incoming)
                if (WallAt(cell.x, cell.y) != kind) { same = false; break; }
            if (same) return;
        }
        pieces = incoming;
        foreach (var c in wallCells)
        {
            var chunk = Find(c.x, c.y);
            if (chunk?.wall != null) chunk.wall[Local(c.x, c.y)] = 0;
        }
        wallCells.Clear();
        foreach (var (cell, kind) in pieces)
        {
            if (kind == WallTile.None) continue;
            var chunk = FindOrMake(FloorDiv(cell.x, ChunkSize), FloorDiv(cell.y, ChunkSize));
            chunk.wall ??= new byte[ChunkSize * ChunkSize];
            chunk.wall[Local(cell.x, cell.y)] = (byte)kind;
            wallCells.Add(cell);
        }
        WallVersion++;
    }

    /// <summary>Walkable terrain, and no wall in this team's way (gates open for the player).</summary>
    public bool Passable(int x, int y, Team team)
    {
        var chunk = Find(x, y);
        if (chunk == null || !chunk.revealed) return false;
        int i = Local(x, y);
        var w = chunk.wall != null ? (WallTile)chunk.wall[i] : WallTile.None;
        if (chunk.terrain[i] == TerrainType.Water) return w == WallTile.Bridge;   // rivers: bridges and fords
        return w == WallTile.None || w == WallTile.Bridge || w == WallTile.Road || w == WallTile.Rail || w == WallTile.Conduit || (w == WallTile.Gate && team == Team.Player);
    }

    public bool Passable(Vector3 world, Team team) => Passable(Mathf.FloorToInt(world.x), Mathf.FloorToInt(world.y), team);

    // ---------- Zones (the map grows one zone = one chunk at a time) ----------

    /// <summary>The map has no edge: every zone can be explored.</summary>
    public bool ZoneInLimit(int cx, int cy) => true;

    public bool IsZoneRevealed(int cx, int cy) =>
        chunks.TryGetValue(new Vector2Int(cx, cy), out var chunk) && chunk.revealed && !chunk.interior;

    // ---------- Underground levels ----------

    /// <summary>
    /// Lay out an underground level in 'area' (far from the map): floor where 'isFloor' says so,
    /// solid rock elsewhere. Units can walk and pathfind there, but it never counts as explored
    /// land, isn't drawn as terrain and isn't saved.
    /// </summary>
    public void StampInterior(RectInt area, System.Func<int, int, bool> isFloor)
    {
        for (int y = area.yMin; y < area.yMax; y++)
            for (int x = area.xMin; x < area.xMax; x++)
            {
                var chunk = FindOrMake(FloorDiv(x, ChunkSize), FloorDiv(y, ChunkSize));
                chunk.generated = chunk.revealed = chunk.interior = true;
                int i = Local(x, y);
                chunk.terrain[i] = isFloor(x, y) ? TerrainType.Grass : TerrainType.Water;
                chunk.deposit[i] = 0;
            }
        WallVersion++;
    }

    /// <summary>Open or close one tile of an underground level (gates between rooms).</summary>
    public void SetInteriorFloor(int x, int y, bool floor)
    {
        var chunk = Find(x, y);
        if (chunk == null || !chunk.interior) return;
        chunk.terrain[Local(x, y)] = floor ? TerrainType.Grass : TerrainType.Water;
        WallVersion++;   // paths get re-planned
    }

    public Vector2Int ZoneOf(int x, int y) => new Vector2Int(FloorDiv(x, ChunkSize), FloorDiv(y, ChunkSize));

    public RectInt ZoneRect(int cx, int cy) => new RectInt(cx * ChunkSize, cy * ChunkSize, ChunkSize, ChunkSize);

    /// <summary>Mark a (generated) zone as explored and widen the bounding box.</summary>
    public void SetZoneRevealed(int cx, int cy)
    {
        var chunk = FindOrMake(cx, cy);
        if (chunk.revealed)
        {
            return;
        }
        chunk.revealed = true;
        revealedZones.Add(new Vector2Int(cx, cy));
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

    /// <summary>Explored zones (for saving), in the order they were explored.</summary>
    public List<Vector2Int> RevealedZones() => new List<Vector2Int>(revealedZones);

    public int RevealedZoneCount => revealedZones.Count;

    /// <summary>True if an explored zone touches unexplored land on that side.</summary>
    public bool IsFrontier(int cx, int cy, int dx, int dy) => IsZoneRevealed(cx, cy) && !IsZoneRevealed(cx + dx, cy + dy);

    public RectInt Bounds => Revealed;

    /// <summary>True for tiles that exist in play (in an explored zone).</summary>
    public bool InBounds(int x, int y)
    {
        var chunk = Find(x, y);
        return chunk != null && chunk.revealed;
    }

    public bool IsChunkGenerated(int cx, int cy) =>
        chunks.TryGetValue(new Vector2Int(cx, cy), out var chunk) && chunk.generated;

    public void MarkChunkGenerated(int cx, int cy) => FindOrMake(cx, cy).generated = true;

    /// <summary>Terrain of a tile. Unexplored tiles read as water (impassable).</summary>
    public TerrainType GetTerrain(int x, int y)
    {
        var chunk = Find(x, y);
        return chunk != null && chunk.revealed ? chunk.terrain[Local(x, y)] : TerrainType.Water;
    }

    /// <summary>Terrain whether or not it is revealed yet (for drawing freshly generated chunks).</summary>
    public TerrainType RawTerrain(int x, int y)
    {
        var chunk = Find(x, y);
        return chunk != null && chunk.generated ? chunk.terrain[Local(x, y)] : TerrainType.Water;
    }

    public void SetTerrain(int x, int y, TerrainType t) =>
        FindOrMake(FloorDiv(x, ChunkSize), FloorDiv(y, ChunkSize)).terrain[Local(x, y)] = t;

    public DepositType GetDeposit(int x, int y) => InBounds(x, y) ? RawDeposit(x, y) : null;

    public DepositType RawDeposit(int x, int y)
    {
        var chunk = Find(x, y);
        if (chunk == null || !chunk.generated)
        {
            return null;
        }
        int d = chunk.deposit[Local(x, y)];
        return d == 0 ? null : Deposits[d - 1];
    }

    public void SetDeposit(int x, int y, DepositType type) =>
        FindOrMake(FloorDiv(x, ChunkSize), FloorDiv(y, ChunkSize)).deposit[Local(x, y)] =
            (byte)(type == null ? 0 : Deposits.IndexOf(type) + 1);

    public bool IsWalkable(int x, int y) => InBounds(x, y) && (GetTerrain(x, y) != TerrainType.Water || WallAt(x, y) == WallTile.Bridge);

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
