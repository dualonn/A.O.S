using System.Collections.Generic;
using UnityEngine;

/// <summary>A kind of star: sets a system's look and what its worlds tend to be.</summary>
public enum StarType { Yellow, RedDwarf, BlueGiant, Binary, Neutron, VoidTouched }

/// <summary>What sort of body a planet is: worlds can be settled, the rest are mined or studied.</summary>
public enum BodyKind { World, Moon, AsteroidBelt, GasGiant }

/// <summary>A planet, moon, belt or gas giant in a star system.</summary>
public class Planet
{
    public StarSystem system;
    public int index;
    public string name;
    public BodyKind kind;
    [Tooltip("The world profile (for worlds and moons you can land on).")]
    public string profile;
    [Tooltip("Orbit, 0..1 from the star outwards (sets the look and the trip within the system).")]
    public float orbit;
    public List<string> traits = new List<string>();
    public int moons;

    public bool CanLand => kind == BodyKind.World || kind == BodyKind.Moon;
    public Vector2Int SystemKey => system.key;
    /// <summary>The seed its map is generated from.</summary>
    public int Seed => Galaxy.Hash(system.key.x, system.key.y, index, Galaxy.Seed ^ 0x5eed);
}

/// <summary>A star and its bodies.</summary>
public class StarSystem
{
    public Vector2Int key;          // (sector x * 64 + slot, sector y) style unique key, see Galaxy
    public Vector2 position;        // light-years from home
    public string name;
    public StarType star;
    public List<Planet> bodies = new List<Planet>();
    public bool IsHome => key == Vector2Int.zero;
}

/// <summary>
/// The endless galaxy, generated from the save's seed: square sectors of space, each with a few
/// star systems at fixed spots. Nothing is stored except what you've found or settled; every
/// system can be rebuilt at any time from its key. The home system is the seven worlds of the lore.
/// </summary>
public static class Galaxy
{
    /// <summary>Width of a sector, in light-years.</summary>
    public const float SectorSize = 40f;
    private const int MaxPerSector = 6;

    public static int Seed { get; private set; } = 1;
    private static readonly Dictionary<Vector2Int, StarSystem> cache = new Dictionary<Vector2Int, StarSystem>();

    public static void Init(int seed)
    {
        if (seed == Seed && cache.Count > 0) return;
        Seed = seed;
        cache.Clear();
    }

    public static int Hash(int a, int b, int c, int d)
    {
        unchecked
        {
            uint h = (uint)a * 73856093u ^ (uint)b * 19349663u ^ (uint)c * 83492791u ^ (uint)d * 2654435761u;
            h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
            return (int)(h & 0x7fffffff);
        }
    }

    private static float Rand01(int a, int b, int c, int salt) => (Hash(a, b, c, Seed + salt * 7919) & 0xFFFFFF) / 16777216f;

    // ---------- Systems ----------

    /// <summary>The key of a system: its sector times the slots per sector, plus its slot.</summary>
    public static Vector2Int Key(Vector2Int sector, int slot) => new Vector2Int(sector.x * MaxPerSector + slot, sector.y);
    public static Vector2Int SectorOf(Vector2Int key) => new Vector2Int(Mathf.FloorToInt(key.x / (float)MaxPerSector), key.y);

    /// <summary>The star systems in a sector of space.</summary>
    public static List<StarSystem> SystemsIn(Vector2Int sector)
    {
        var list = new List<StarSystem>();
        int count = sector == Vector2Int.zero ? 1 + (int)(Rand01(sector.x, sector.y, 0, 1) * 3) : 2 + (int)(Rand01(sector.x, sector.y, 0, 1) * (MaxPerSector - 1));
        for (int slot = 0; slot < count; slot++)
        {
            var s = Get(Key(sector, slot));
            if (s != null) list.Add(s);
        }
        return list;
    }

    /// <summary>A system by its key (built on first use).</summary>
    public static StarSystem Get(Vector2Int key)
    {
        if (cache.TryGetValue(key, out var s)) return s;
        s = key == Vector2Int.zero ? BuildHome() : Build(key);
        cache[key] = s;
        return s;
    }

    private static StarSystem Build(Vector2Int key)
    {
        var sector = SectorOf(key);
        int slot = key.x - sector.x * MaxPerSector;
        var s = new StarSystem
        {
            key = key,
            position = new Vector2(
                (sector.x + 0.1f + 0.8f * Rand01(key.x, key.y, slot, 2)) * SectorSize,
                (sector.y + 0.1f + 0.8f * Rand01(key.x, key.y, slot, 3)) * SectorSize) - new Vector2(SectorSize * 0.5f, SectorSize * 0.5f),
        };
        float r = Rand01(key.x, key.y, 0, 4);
        s.star = r < 0.42f ? StarType.RedDwarf : r < 0.74f ? StarType.Yellow : r < 0.84f ? StarType.Binary
            : r < 0.92f ? StarType.BlueGiant : r < 0.97f ? StarType.Neutron : StarType.VoidTouched;
        s.name = StarName(key);
        int bodies = 2 + (int)(Rand01(key.x, key.y, 0, 5) * 6);
        for (int i = 0; i < bodies; i++)
        {
            float orbit = (i + 0.5f) / bodies;
            var p = new Planet { system = s, index = i, orbit = orbit };
            float k = Rand01(key.x, key.y, i, 6);
            p.kind = orbit > 0.55f && k < 0.3f ? BodyKind.GasGiant : k > 0.9f ? BodyKind.AsteroidBelt : k > 0.78f ? BodyKind.Moon : BodyKind.World;
            p.profile = p.CanLand ? PickProfile(s.star, orbit, Rand01(key.x, key.y, i, 7), p.kind == BodyKind.Moon) : null;
            p.moons = p.kind == BodyKind.GasGiant ? 1 + (int)(Rand01(key.x, key.y, i, 8) * 4) : (int)(Rand01(key.x, key.y, i, 8) * 2.4f);
            p.name = p.kind == BodyKind.AsteroidBelt ? $"{s.name} Belt" : $"{s.name} {Roman(i + 1)}";
            AddTraits(p, Rand01(key.x, key.y, i, 9), Rand01(key.x, key.y, i, 10));
            s.bodies.Add(p);
        }
        return s;
    }

    /// <summary>The seven worlds of the lore, round a yellow sun; you start on the third.</summary>
    private static StarSystem BuildHome()
    {
        var s = new StarSystem { key = Vector2Int.zero, position = Vector2.zero, name = "Sol Prime", star = StarType.Yellow };
        (string name, BodyKind kind, string profile, string[] traits, int moons)[] seven =
        {
            ("Ember", BodyKind.World, "Volcanic", new[] { "Scorching" }, 0),
            ("Rust", BodyKind.World, "Desert", new[] { "Red Dust", "Thin Air" }, 1),
            ("Homeworld", BodyKind.World, WorldProfile.HomeId, new string[0], 1),
            ("Verdance", BodyKind.World, "Temperate", new[] { "Fertile" }, 0),
            ("Glimmer", BodyKind.World, "Crystal", new[] { "Mana-rich" }, 2),
            ("Rime", BodyKind.World, "Ice", new[] { "Long Nights", "Ancient Ruins" }, 1),
            ("Thalassa", BodyKind.World, "Ocean", new[] { "Storms" }, 3),
        };
        for (int i = 0; i < seven.Length; i++)
        {
            var w = seven[i];
            var p = new Planet { system = s, index = i, name = w.name, kind = w.kind, profile = w.profile, orbit = (i + 0.5f) / seven.Length, moons = w.moons };
            p.traits.AddRange(w.traits);
            s.bodies.Add(p);
        }
        // The belt beyond them, and a gas giant at the edge.
        s.bodies.Add(new Planet { system = s, index = 7, name = "The Shards", kind = BodyKind.AsteroidBelt, orbit = 0.9f });
        s.bodies.Add(new Planet { system = s, index = 8, name = "Old Titan", kind = BodyKind.GasGiant, orbit = 0.97f, moons = 4 });
        return s;
    }

    /// <summary>The planet you start on.</summary>
    public static Planet HomePlanet => Get(Vector2Int.zero).bodies[2];

    private static string PickProfile(StarType star, float orbit, float r, bool moon)
    {
        // Hot near the star, cold far out, strange round odd stars.
        if (star == StarType.VoidTouched) return r < 0.5f ? "Crystal" : "Volcanic";
        if (star == StarType.Neutron && r < 0.4f) return "Crystal";
        float heat = 1f - orbit + (star == StarType.BlueGiant ? 0.25f : star == StarType.RedDwarf ? -0.2f : 0f);
        if (moon && r < 0.5f) return heat > 0.6f ? "Volcanic" : "Ice";
        if (heat > 0.8f) return r < 0.6f ? "Volcanic" : "Desert";
        if (heat > 0.6f) return r < 0.5f ? "Desert" : r < 0.75f ? "Swamp" : "Temperate";
        if (heat > 0.4f) return r < 0.35f ? "Temperate" : r < 0.55f ? "Swamp" : r < 0.75f ? "Ocean" : "Crystal";
        return r < 0.6f ? "Ice" : r < 0.8f ? "Ocean" : "Crystal";
    }

    private static readonly string[] TraitPool =
    {
        "Low Gravity", "High Gravity", "Thin Air", "Toxic Air", "Storms", "Long Nights", "Rich Veins",
        "Hostile Fauna", "Ancient Ruins", "Fertile", "Mana-rich", "Starmetal Craters", "Quakes",
    };

    private static void AddTraits(Planet p, float a, float b)
    {
        if (!p.CanLand) return;
        if (a < 0.7f) p.traits.Add(TraitPool[(int)(a / 0.7f * TraitPool.Length) % TraitPool.Length]);
        if (b < 0.3f)
        {
            string t = TraitPool[(int)(b / 0.3f * TraitPool.Length + 5) % TraitPool.Length];
            if (!p.traits.Contains(t)) p.traits.Add(t);
        }
    }

    // ---------- Names ----------

    private static readonly string[] Start = { "Al", "Bel", "Cor", "Dra", "El", "Fen", "Gal", "Hy", "Ith", "Ka", "Lor", "Mi", "Nor", "Or", "Pyr", "Quel", "Ras", "Sol", "Tor", "Ul", "Vex", "Wy", "Xan", "Zer" };
    private static readonly string[] Middle = { "a", "e", "i", "o", "u", "ae", "io", "ar", "en", "is", "or", "yl" };
    private static readonly string[] End = { "ris", "tor", "nax", "lia", "dor", "mis", "thea", "gon", "rax", "nis", "vel", "dra", "phos", "tis" };

    public static string StarName(Vector2Int key)
    {
        int h = Hash(key.x, key.y, 11, Seed);
        return Start[h % Start.Length] + Middle[(h / 31) % Middle.Length] + End[(h / 997) % End.Length];
    }

    private static string Roman(int n) => n switch { 1 => "I", 2 => "II", 3 => "III", 4 => "IV", 5 => "V", 6 => "VI", 7 => "VII", 8 => "VIII", _ => n.ToString() };

    // ---------- Distances ----------

    /// <summary>Light-years between two systems.</summary>
    public static float Distance(StarSystem a, StarSystem b) => Vector2.Distance(a.position, b.position);

    /// <summary>How long a trip takes, in game seconds: within a system it's the orbits apart; between systems, the light-years.</summary>
    public static float TravelSeconds(Planet from, Planet to, float speed)
    {
        speed = Mathf.Max(0.05f, speed);
        if (from.system == to.system) return (40f + 120f * Mathf.Abs(from.orbit - to.orbit)) / speed;
        return (120f + 60f * Distance(from.system, to.system)) / speed;
    }
}
