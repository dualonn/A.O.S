using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// How a kind of world generates and looks: its lands (biome mix, which biome surrounds the
/// landing site), how much water and hill, rivers, which deposits are common, and its colours.
/// The home profile reproduces the original map exactly.
/// </summary>
public class WorldProfile
{
    public const string HomeId = "Home";

    public string id;
    public string displayName;
    [TextArea] public string description;

    [Tooltip("The land round the landing site (the home world's is always temperate).")]
    public Biome homeBiome = Biome.Temperate;

    // Biome rules: three noise fields (warmth, wetness, wildness), each 0..1, plus a bias per world.
    public float warmBias, wetBias, wildBias;
    public float crystalAbove = 0.74f, volcanicBelow = 0.26f;
    public float desertWarm = 0.58f, desertWet = 0.56f, tundraBelow = 0.37f, swampWet = 0.62f;
    [Tooltip("Radius of the landing area that keeps the home biome, and how far it blends out.")]
    public float homeRadius = 110f, homeBlend = 70f;

    [Tooltip("Share of land under water and on hills (negative: the game's defaults).")]
    public float waterShare = -1f, hillShare = -1f;
    [Tooltip("Rivers (negative: the game's default).")]
    public int riverCount = -1;
    public float terrainScaleMultiplier = 1f;

    [Tooltip("Deposits made more (positive) or less common on this world: rarity is lowered by this much.")]
    public Dictionary<string, float> depositBoost = new Dictionary<string, float>();

    [Tooltip("Lands swapped for others on this world (e.g. a barren moon has no green lands).")]
    public Dictionary<Biome, Biome> remap = new Dictionary<Biome, Biome>();

    [Tooltip("The people native to this kind of world (their units are named Natives_Unit).")]
    public string natives;

    [Tooltip("Tints for this world's sky (fog), water and hills.")]
    public Color sky = new Color(0.08f, 0.09f, 0.12f), water = Color.white, hills = Color.white;

    public static readonly WorldProfile Home = new WorldProfile
    {
        id = HomeId, displayName = "Homeworld",
        description = "Where the first fire was lit: green lands, rivers and hills, with stranger lands further out.",
    };

    private static Dictionary<string, WorldProfile> registry;

    /// <summary>Every kind of world, by id.</summary>
    public static IReadOnlyDictionary<string, WorldProfile> All
    {
        get
        {
            if (registry == null)
            {
                registry = new Dictionary<string, WorldProfile>();
                foreach (var p in Catalogue()) registry[p.id] = p;
            }
            return registry;
        }
    }

    public static WorldProfile Get(string id) => id != null && All.TryGetValue(id, out var p) ? p : Home;

    /// <summary>The kinds of world out there. (More are added as the galaxy grows.)</summary>
    private static IEnumerable<WorldProfile> Catalogue()
    {
        yield return Home;
        yield return new WorldProfile
        {
            id = "Temperate", displayName = "Garden World",
            description = "Green and mild, like home: forests, rivers and gentle hills.",
            warmBias = 0f, wetBias = 0.02f, homeRadius = 80f, natives = "Wildfolk",
        };
        yield return new WorldProfile
        {
            id = "Desert", displayName = "Desert World",
            description = "Dunes and salt flats under a hard sun; water is scarce and precious.",
            homeBiome = Biome.Desert, warmBias = 0.22f, wetBias = -0.2f, waterShare = 0.05f, riverCount = 1, natives = "Sandfolk",
            sky = new Color(0.16f, 0.11f, 0.06f), hills = new Color(1f, 0.9f, 0.75f),
        };
        yield return new WorldProfile
        {
            id = "Ice", displayName = "Ice World",
            description = "Frozen plains and glaciers; long nights, little food, and old things in the ice.",
            homeBiome = Biome.Tundra, warmBias = -0.25f, waterShare = 0.12f, natives = "Frostkin",
            sky = new Color(0.07f, 0.1f, 0.15f), water = new Color(0.8f, 0.92f, 1f), hills = new Color(0.9f, 0.95f, 1.05f),
        };
        yield return new WorldProfile
        {
            id = "Swamp", displayName = "Marsh World",
            description = "Warm fens and drowned forests, thick with life and fever.",
            homeBiome = Biome.Swamp, wetBias = 0.22f, waterShare = 0.22f, riverCount = 3, natives = "Bogfolk",
            sky = new Color(0.07f, 0.11f, 0.08f), water = new Color(0.75f, 0.9f, 0.7f),
        };
        yield return new WorldProfile
        {
            id = "Volcanic", displayName = "Volcanic World",
            description = "Ash plains and lava rivers; rich in heavy ores for those who dare.",
            homeBiome = Biome.Volcanic, wildBias = -0.22f, waterShare = 0.06f, hillShare = 0.24f, natives = "Ashborn",
            sky = new Color(0.14f, 0.06f, 0.05f), water = new Color(1f, 0.7f, 0.6f), hills = new Color(0.85f, 0.7f, 0.65f),
            depositBoost = new Dictionary<string, float> { { "IronOre", 0.04f }, { "Coal", 0.04f }, { "Uranium", 0.05f } },
        };
        yield return new WorldProfile
        {
            id = "Crystal", displayName = "Crystal World",
            description = "Forests of singing crystal; mana runs through the very ground.",
            homeBiome = Biome.Crystal, wildBias = 0.22f, natives = "Shardkin",
            sky = new Color(0.1f, 0.07f, 0.15f), hills = new Color(0.9f, 0.85f, 1.05f),
            depositBoost = new Dictionary<string, float> { { "Manastone", 0.06f }, { "Silicon", 0.04f } },
        };
        yield return new WorldProfile
        {
            id = "Ocean", displayName = "Ocean World",
            description = "Endless sea broken by green islands; every landing is a beachhead.",
            waterShare = 0.62f, riverCount = 0, homeRadius = 60f, natives = "Tidefolk",
            sky = new Color(0.06f, 0.1f, 0.14f),
        };
        yield return new WorldProfile
        {
            id = "Barren", displayName = "Barren Moon",
            description = "Grey dust and old craters under a black sky; little grows, but the ground is full of metal.",
            homeBiome = Biome.Barren, waterShare = 0.02f, riverCount = 0, hillShare = 0.2f,
            remap = new Dictionary<Biome, Biome> { { Biome.Temperate, Biome.Barren }, { Biome.Swamp, Biome.Barren }, { Biome.Desert, Biome.Barren } },
            sky = new Color(0.03f, 0.03f, 0.05f), hills = new Color(0.85f, 0.85f, 0.9f),
            depositBoost = new Dictionary<string, float> { { "IronOre", 0.05f }, { "Starmetal", 0.06f }, { "Silicon", 0.04f } },
        };
        yield return new WorldProfile
        {
            id = "Jungle", displayName = "Jungle World",
            description = "Hot, wet and endlessly green: giant trees, fierce beasts and ruins swallowed by the forest.",
            homeBiome = Biome.Jungle, warmBias = 0.12f, wetBias = 0.15f, waterShare = 0.16f, riverCount = 4, natives = "Wildfolk",
            remap = new Dictionary<Biome, Biome> { { Biome.Temperate, Biome.Jungle }, { Biome.Tundra, Biome.Jungle } },
            sky = new Color(0.05f, 0.11f, 0.07f), hills = new Color(0.8f, 1f, 0.8f),
        };
        yield return new WorldProfile
        {
            id = "Fungal", displayName = "Fungal World",
            description = "Forests of giant mushrooms glowing in the dusk; the air is thick with spores and something is always growing.",
            homeBiome = Biome.Fungal, wetBias = 0.1f, wildBias = 0.08f, waterShare = 0.14f, natives = "Bogfolk",
            remap = new Dictionary<Biome, Biome> { { Biome.Temperate, Biome.Fungal }, { Biome.Swamp, Biome.Fungal } },
            sky = new Color(0.1f, 0.05f, 0.1f), water = new Color(0.9f, 0.75f, 1f), hills = new Color(0.95f, 0.8f, 0.95f),
            depositBoost = new Dictionary<string, float> { { "Manastone", 0.03f } },
        };
    }

    // ---------- Generation helpers ----------

    public float WaterShare(GameDatabase db) => waterShare >= 0 ? waterShare : db.waterShare;
    public float HillShare(GameDatabase db) => hillShare >= 0 ? hillShare : db.hillShare;
    public int Rivers(GameDatabase db) => riverCount >= 0 ? riverCount : db.riverCount;
    public float DepositBoost(string depositId) => depositId != null && depositBoost.TryGetValue(depositId, out var b) ? b : 0f;

    /// <summary>The biome for these noise values (each 0..1, already biased).</summary>
    public Biome Pick(float warm, float wet, float wild)
    {
        var b = wild > crystalAbove ? Biome.Crystal : wild < volcanicBelow ? Biome.Volcanic
            : warm > desertWarm && wet < desertWet ? Biome.Desert : warm < tundraBelow ? Biome.Tundra
            : wet > swampWet ? Biome.Swamp : Biome.Temperate;
        return remap.Count > 0 && remap.TryGetValue(b, out var swapped) ? swapped : b;
    }
}
