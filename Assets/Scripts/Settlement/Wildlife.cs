using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

// ===================================================================================================
// Wildlife, hunting and taming.
//
// Every world has herds roaming its explored land, chosen by biome and by the kind of world: deer,
// boar, wolves, bears and mammoths at home (griffins, wyverns and crystal beasts in its stranger
// lands), and alien fauna on other worlds. A herd is a few numbers (where it is, how many) that the
// simulation moves about and thins or regrows, on every world, whether you're looking or not. The
// scene (WildlifeView) draws the herd you can see as animals, and only turns them into real fighting
// units when your people come close, so herds cost nothing in a big fight.
//
// - Grazers flee from your people. Big beasts fight back if you come too close. Predators attack.
// - Herds never appear near your settlements, but now and then a predator wanders into town.
// - Hunting Camps (and the Ranch, Game Reserve and Xenobiology Lab after them) hunt the herds in
//   their reach for food (and, later, coin from hides). Hunting too hard thins the herds.
// - Beast Pens (then the Royal Menagerie and the Xenofauna Pen) tame beasts from the herds nearby
//   into war beasts that fight for you; a hero may take one as a companion, for a small bonus.
// - Legendary beasts (a white stag, an ancient mammoth, a sky whale...) turn up now and then: hunt
//   one down for a great prize, or tame it as a hero's companion.
// ===================================================================================================

/// <summary>How a wild creature behaves when your people come near.</summary>
public enum WildRole { Grazer, Beast, Predator }

/// <summary>One kind of wild creature (gameplay numbers; its art and units are in the WildlifeLibrary).</summary>
public class WildSpecies
{
    public string id, name, plural, note;
    public WildRole role;
    public Biome[] biomes;
    /// <summary>0: the home world's ordinary lands; 1: the home world's fantasy lands; 2: other worlds.</summary>
    public int realm;
    public int herdMin = 2, herdMax = 4;
    /// <summary>Food from each one hunted.</summary>
    public float meat = 40f;
    /// <summary>Tamed into this (the unit id in the WildlifeLibrary), or null if it can't be tamed.</summary>
    public string tameName;
    /// <summary>The age (index) you must reach before you can tame it.</summary>
    public int tameAge;
    /// <summary>How long one takes to tame (1 = a wolf).</summary>
    public float tameTime = 1f;
    public bool flyer, legendary;
    /// <summary>Wakes (flees or fights) when your people come this close (tiles).</summary>
    public float wake = 5f;

    public bool Tamable => tameName != null;
    /// <summary>The pen it needs: 0 a Beast Pen, 1 a Royal Menagerie, 2 a Xenofauna Pen.</summary>
    public int PenTier => realm;
}

[Serializable]
public class WildHerd
{
    public int id;
    public string species;
    [Tooltip("Where it is now, where it calls home, and where it's heading (tiles).")]
    public Vector2 pos, home, target;
    public int count, max;
    [Tooltip("Seconds until it grows by one (while it hasn't been hunted lately).")]
    public float regrowIn = 90f;
    [Tooltip("Play time it was last hunted (it only regrows once left alone a while).")]
    public float huntedAt = -999f;
    [Tooltip("Heading into one of your settlements (a beast on the prowl).")]
    public bool toTown;
    [Tooltip("Play time until which it runs (scared off).")]
    public float fleeUntil;
}

[Serializable]
public class WildPen
{
    [Tooltip("The pen building's instance id.")]
    public int building;
    public float progress;
    [Tooltip("The beast being tamed now (species id), for the panel.")]
    public string taming;
    [Tooltip("The tamed beasts kept here (species ids): each is a war beast in the field.")]
    public List<string> beasts = new List<string>();
}

/// <summary>A world's wildlife (each world keeps its own).</summary>
[Serializable]
public class WildWorldState
{
    public List<WildHerd> herds = new List<WildHerd>();
    public List<WildPen> pens = new List<WildPen>();
    public float spawnIn = 3f;
    [Tooltip("Seconds until a beast next wanders towards one of your towns.")]
    public float roamIn = 600f;
    [Tooltip("Seconds until a legendary beast may next be seen.")]
    public float legendIn = 1500f;
    public int nextId = 1;
    public int hunted;
}

/// <summary>A hero's tamed companion.</summary>
[Serializable]
public class WildCompanion
{
    public int hero;
    public string species;
    public bool legendary;
}

/// <summary>Wildlife records shared by all worlds.</summary>
[Serializable]
public class WildGlobalState
{
    public List<WildCompanion> companions = new List<WildCompanion>();
    public int animalsHunted, beastsTamed, legendsSlain, legendsTamed;
}

public partial class Settlement
{
    // ---------- The creatures ----------

    private static readonly Biome[] Green = { Biome.Temperate, Biome.Jungle };

    /// <summary>Every kind of wild creature.</summary>
    public static readonly WildSpecies[] WildCatalogue =
    {
        // The home world's ordinary lands.
        new WildSpecies { id = "Deer", name = "Red Deer", plural = "deer", role = WildRole.Grazer, biomes = new[] { Biome.Temperate, Biome.Tundra, Biome.Jungle },
            herdMin = 4, herdMax = 7, meat = 40, wake = 5f, note = "Shy grazers: they bolt from your people. Hunting Camps hunt them." },
        new WildSpecies { id = "Antelope", name = "Dune Antelope", plural = "antelope", role = WildRole.Grazer, biomes = new[] { Biome.Desert, Biome.Barren },
            herdMin = 4, herdMax = 6, meat = 35, wake = 6f, note = "Fleet grazers of the dry lands. Hunting Camps hunt them." },
        new WildSpecies { id = "Boar", name = "Wild Boar", plural = "boar", role = WildRole.Beast, biomes = new[] { Biome.Temperate, Biome.Swamp, Biome.Jungle },
            herdMin = 2, herdMax = 4, meat = 60, tameName = "War Boar", tameAge = 0, tameTime = 0.8f, wake = 3.5f,
            note = "Charges anyone who comes too close. Hunted for meat; a Beast Pen can tame them." },
        new WildSpecies { id = "Wolf", name = "Grey Wolf", plural = "wolves", role = WildRole.Predator, biomes = new[] { Biome.Temperate, Biome.Tundra },
            herdMin = 3, herdMax = 5, meat = 25, tameName = "War Wolf", tameAge = 0, wake = 7f,
            note = "A hunting pack: it attacks whoever wanders near. A Beast Pen can tame them." },
        new WildSpecies { id = "Bear", name = "Cave Bear", plural = "bears", role = WildRole.Predator, biomes = new[] { Biome.Temperate, Biome.Tundra, Biome.Swamp },
            herdMin = 1, herdMax = 2, meat = 120, tameName = "War Bear", tameAge = 1, tameTime = 1.6f, wake = 6f,
            note = "Huge and bad-tempered. Tame from the Bronze Age." },
        new WildSpecies { id = "Mammoth", name = "Woolly Mammoth", plural = "mammoths", role = WildRole.Beast, biomes = new[] { Biome.Tundra },
            herdMin = 2, herdMax = 4, meat = 300, tameName = "War Mammoth", tameAge = 1, tameTime = 2.4f, wake = 3.5f,
            note = "Gentle giants that trample whoever bothers them. A whole winter's meat each. Tame from the Bronze Age." },
        new WildSpecies { id = "Mossback", name = "Mossback", plural = "mossbacks", role = WildRole.Grazer, biomes = new[] { Biome.Fungal, Biome.Swamp },
            herdMin = 3, herdMax = 5, meat = 55, wake = 4f, note = "Slow, moss-grown grazers of the damp lands. Hunting Camps hunt them." },

        // The home world's fantasy lands.
        new WildSpecies { id = "Griffin", name = "Griffin", plural = "griffins", role = WildRole.Predator, realm = 1, biomes = new[] { Biome.Crystal, Biome.Fungal, Biome.Tundra },
            herdMin = 1, herdMax = 3, meat = 150, tameName = "Griffin Knight", tameAge = 4, tameTime = 2.5f, flyer = true, wake = 7f,
            note = "Lion and eagle in one, and proud of both. A Royal Menagerie can tame them (Medieval Age)." },
        new WildSpecies { id = "Wyvern", name = "Wyvern", plural = "wyverns", role = WildRole.Predator, realm = 1, biomes = new[] { Biome.Volcanic, Biome.Barren },
            herdMin = 1, herdMax = 2, meat = 180, tameName = "Wyvern Knight", tameAge = 4, tameTime = 3f, flyer = true, wake = 7f,
            note = "A two-legged dragon with a venomous tail. A Royal Menagerie can tame them (Medieval Age)." },
        new WildSpecies { id = "CrystalBeast", name = "Crystal Beast", plural = "crystal beasts", role = WildRole.Beast, realm = 1, biomes = new[] { Biome.Crystal },
            herdMin = 1, herdMax = 3, meat = 200, tameName = "Crystal Behemoth", tameAge = 4, tameTime = 3f, wake = 3.5f,
            note = "Crystal grows on its back like moss. Peaceful until crossed. A Royal Menagerie can tame them." },

        // Other worlds.
        new WildSpecies { id = "FrostStrider", name = "Frost Strider", plural = "frost striders", role = WildRole.Grazer, realm = 2, biomes = new[] { Biome.Tundra },
            herdMin = 3, herdMax = 6, meat = 90, wake = 6f, note = "Six-legged grazers that lick the frost off the rocks." },
        new WildSpecies { id = "IceLurker", name = "Ice Lurker", plural = "ice lurkers", role = WildRole.Predator, realm = 2, biomes = new[] { Biome.Tundra },
            herdMin = 1, herdMax = 3, meat = 160, tameName = "Tamed Ice Lurker", tameAge = 12, tameTime = 2f, wake = 6f,
            note = "White as the snow it hides under. A Xenofauna Pen can tame them." },
        new WildSpecies { id = "SandSkitter", name = "Sand Skitter", plural = "sand skitters", role = WildRole.Grazer, realm = 2, biomes = new[] { Biome.Desert },
            herdMin = 4, herdMax = 7, meat = 60, wake = 6f, note = "Quick little shell-backs that graze on dune grass." },
        new WildSpecies { id = "DuneWyrm", name = "Dune Wyrm", plural = "dune wyrms", role = WildRole.Beast, realm = 2, biomes = new[] { Biome.Desert, Biome.Barren },
            herdMin = 1, herdMax = 2, meat = 400, tameName = "Tamed Dune Wyrm", tameAge = 12, tameTime = 3f, wake = 4f,
            note = "A great armoured worm on stubby legs. A Xenofauna Pen can tame them." },
        new WildSpecies { id = "EmberHound", name = "Ember Hound", plural = "ember hounds", role = WildRole.Predator, realm = 2, biomes = new[] { Biome.Volcanic },
            herdMin = 2, herdMax = 4, meat = 80, tameName = "Tamed Ember Hound", tameAge = 12, tameTime = 1.5f, wake = 7f,
            note = "Hounds with coals for hearts, hunting in packs. A Xenofauna Pen can tame them." },
        new WildSpecies { id = "MagmaTortoise", name = "Magma Tortoise", plural = "magma tortoises", role = WildRole.Beast, realm = 2, biomes = new[] { Biome.Volcanic, Biome.Barren },
            herdMin = 1, herdMax = 3, meat = 350, tameName = "Tamed Magma Tortoise", tameAge = 12, tameTime = 3f, wake = 3f,
            note = "Its shell glows like a forge. A Xenofauna Pen can tame them." },
        new WildSpecies { id = "RazorRaptor", name = "Razor Raptor", plural = "razor raptors", role = WildRole.Predator, realm = 2, biomes = new[] { Biome.Jungle, Biome.Temperate },
            herdMin = 2, herdMax = 4, meat = 90, tameName = "Tamed Razor Raptor", tameAge = 12, tameTime = 1.8f, wake = 7f,
            note = "Feathered, clever and fast. A Xenofauna Pen can tame them." },
        new WildSpecies { id = "CanopyStrider", name = "Canopy Strider", plural = "canopy striders", role = WildRole.Grazer, realm = 2, biomes = new[] { Biome.Jungle, Biome.Temperate, Biome.Swamp },
            herdMin = 3, herdMax = 5, meat = 140, wake = 5f, note = "Long-necked browsers that eat the tops of the trees." },
        new WildSpecies { id = "ShardStalker", name = "Shard Stalker", plural = "shard stalkers", role = WildRole.Predator, realm = 2, biomes = new[] { Biome.Crystal },
            herdMin = 1, herdMax = 3, meat = 110, tameName = "Tamed Shard Stalker", tameAge = 12, tameTime = 2f, wake = 7f,
            note = "A cat of living crystal. A Xenofauna Pen can tame them." },
        new WildSpecies { id = "PrismMoth", name = "Prism Moth", plural = "prism moths", role = WildRole.Grazer, realm = 2, biomes = new[] { Biome.Crystal, Biome.Fungal },
            herdMin = 3, herdMax = 6, meat = 50, flyer = true, wake = 5f, note = "Moths as big as dogs, wings like stained glass." },
        new WildSpecies { id = "MireLurker", name = "Mire Lurker", plural = "mire lurkers", role = WildRole.Predator, realm = 2, biomes = new[] { Biome.Swamp, Biome.Fungal },
            herdMin = 1, herdMax = 3, meat = 130, tameName = "Tamed Mire Lurker", tameAge = 12, tameTime = 2f, wake = 6f,
            note = "All jaws and patience. A Xenofauna Pen can tame them." },
        new WildSpecies { id = "ShellCrawler", name = "Shell Crawler", plural = "shell crawlers", role = WildRole.Grazer, realm = 2, biomes = new[] { Biome.Temperate, Biome.Swamp, Biome.Desert },
            herdMin = 3, herdMax = 6, meat = 70, wake = 4f, note = "Crabs the size of ponies, grazing the shore." },
        new WildSpecies { id = "CraterHulk", name = "Crater Hulk", plural = "crater hulks", role = WildRole.Beast, realm = 2, biomes = new[] { Biome.Barren },
            herdMin = 1, herdMax = 2, meat = 300, tameName = "Tamed Crater Hulk", tameAge = 12, tameTime = 3f, wake = 3.5f,
            note = "A rock-skinned brute that grazes on lichen and metal. A Xenofauna Pen can tame them." },
        new WildSpecies { id = "SporeGrazer", name = "Spore Grazer", plural = "spore grazers", role = WildRole.Grazer, realm = 2, biomes = new[] { Biome.Fungal },
            herdMin = 3, herdMax = 6, meat = 80, wake = 5f, note = "Puffball beasts that drift between the giant mushrooms." },

        // Legendary beasts: one at a time, rare, and worth a great deal.
        new WildSpecies { id = "WhiteStag", name = "The White Stag", plural = "white stags", role = WildRole.Beast, legendary = true, biomes = new[] { Biome.Temperate, Biome.Tundra },
            herdMin = 1, herdMax = 1, meat = 600, tameName = "The White Stag", tameAge = 4, tameTime = 4f, wake = 4f,
            note = "A legend: a stag as white as moonlight. Hunt it down for a great prize, or tame it (Royal Menagerie) as a hero's companion." },
        new WildSpecies { id = "AncientMammoth", name = "The Ancient Mammoth", plural = "ancient mammoths", role = WildRole.Beast, legendary = true, biomes = new[] { Biome.Tundra },
            herdMin = 1, herdMax = 1, meat = 1500, tameName = "The Ancient Mammoth", tameAge = 4, tameTime = 5f, wake = 4f,
            note = "A legend: the oldest living thing on the plains. Hunt it, or tame it (Royal Menagerie) as a hero's companion." },
        new WildSpecies { id = "StormGriffin", name = "The Storm Griffin", plural = "storm griffins", role = WildRole.Predator, realm = 1, legendary = true, flyer = true,
            biomes = new[] { Biome.Crystal, Biome.Volcanic, Biome.Fungal }, herdMin = 1, herdMax = 1, meat = 800, tameName = "The Storm Griffin", tameAge = 4, tameTime = 5f, wake = 6f,
            note = "A legend: a griffin that rides the thunder. Bring it down, or tame it (Royal Menagerie) as a hero's companion." },
        new WildSpecies { id = "SkyWhale", name = "The Sky Whale", plural = "sky whales", role = WildRole.Beast, realm = 2, legendary = true, flyer = true,
            biomes = new[] { Biome.Temperate, Biome.Desert, Biome.Tundra, Biome.Swamp, Biome.Volcanic, Biome.Crystal, Biome.Barren, Biome.Jungle, Biome.Fungal },
            herdMin = 1, herdMax = 1, meat = 3000, tameName = "The Sky Whale", tameAge = 12, tameTime = 6f, wake = 4f,
            note = "A legend: a whale that swims through the sky. Hunt it, or tame it (Xenofauna Pen) as a hero's companion." },
        new WildSpecies { id = "Shardback", name = "The Shardback", plural = "shardbacks", role = WildRole.Beast, realm = 2, legendary = true, biomes = new[] { Biome.Crystal, Biome.Barren, Biome.Volcanic },
            herdMin = 1, herdMax = 1, meat = 2000, tameName = "The Shardback", tameAge = 12, tameTime = 6f, wake = 4f,
            note = "A legend: a beast the size of a hill, crusted with crystal. Hunt it, or tame it (Xenofauna Pen) as a hero's companion." },
    };

    private static Dictionary<string, WildSpecies> wildIndex;

    public static WildSpecies WildSpeciesOf(string id)
    {
        if (wildIndex == null)
        {
            wildIndex = new Dictionary<string, WildSpecies>();
            foreach (var s in WildCatalogue) wildIndex[s.id] = s;
        }
        return id != null && wildIndex.TryGetValue(id, out var sp) ? sp : null;
    }

    // ---------- Buildings ----------

    /// <summary>Hunting buildings: (id, food per second, coin per second, share it makes with no herds, herd thinning, reach).</summary>
    private static readonly (string id, float food, float coin, float floor, float thin, int reach)[] HuntBuildings =
    {
        ("WildHuntCamp", 1.2f, 0f, 0f, 1f, 18),
        ("WildRanch", 3.2f, 0.15f, 0.45f, 0.6f, 20),
        ("WildReserve", 6.5f, 0.4f, 0.6f, 0.35f, 22),
        ("WildXenoLab", 13f, 0.9f, 0.7f, 0.3f, 24),
    };

    /// <summary>Taming buildings: (id, tier, beasts kept at level 0, reach).</summary>
    private static readonly (string id, int tier, int cap, int reach)[] PenBuildings =
    {
        ("WildBeastPen", 0, 2, 18),
        ("WildMenagerie", 1, 3, 20),
        ("WildXenoPen", 2, 4, 22),
    };

    public static bool IsHuntBuilding(BuildingDefinition def) => def != null && HuntIndex(def.id) >= 0;
    public static bool IsBeastPen(BuildingDefinition def) => def != null && PenIndex(def.id) >= 0;

    /// <summary>Trains troops of some kind (a barracks, or a pen of tamed beasts).</summary>
    public static bool TrainsTroops(BuildingDefinition def) => def != null && (def.trainsUnit != null || IsBeastPen(def));

    private static int HuntIndex(string id)
    {
        for (int i = 0; i < HuntBuildings.Length; i++) if (HuntBuildings[i].id == id) return i;
        return -1;
    }

    private static int PenIndex(string id)
    {
        for (int i = 0; i < PenBuildings.Length; i++) if (PenBuildings[i].id == id) return i;
        return -1;
    }

    /// <summary>How many tamed beasts a pen keeps: its base, plus one per upgrade (at most 3 upgrades).</summary>
    public int PenCap(BuildingState b, BuildingDefinition def)
    {
        int i = PenIndex(def.id);
        return i < 0 ? 0 : PenBuildings[i].cap + Mathf.Min(3, b.upgradeLevel);
    }

    private int HuntReach(BuildingState b, BuildingDefinition def)
    {
        int i = HuntIndex(def.id);
        return i < 0 ? 0 : HuntBuildings[i].reach + 2 * Mathf.Min(3, b.upgradeLevel);
    }

    private int PenReach(BuildingState b, BuildingDefinition def)
    {
        int i = PenIndex(def.id);
        return i < 0 ? 0 : PenBuildings[i].reach + 2 * Mathf.Min(3, b.upgradeLevel);
    }

    // ---------- Research (small global bonuses) ----------

    private bool WildHas(string tech) => State.researched.Contains(tech);

    private int WildLevel(string tech)
    {
        int n = 0;
        foreach (var t in State.researched) if (t == tech) n++;
        return n;
    }

    /// <summary>Hunting yield from research: Tracking +10%, Master Huntsmen +4% a level (5 levels), Selective Breeding +10%.</summary>
    private float HuntResearch => 1f + (WildHas("WildTracking") ? 0.1f : 0f) + 0.04f * Mathf.Min(5, WildLevel("WildMasterHunters"))
        + (WildHas("WildBreeding") ? 0.1f : 0f);

    /// <summary>Taming speed from research: Beast Lore +20%.</summary>
    private float TameResearch => 1f + (WildHas("WildBeastLore") ? 0.2f : 0f);

    /// <summary>How fast herds regrow: Game Laws and Conservation each +25%.</summary>
    private float RegrowResearch => 1f + (WildHas("WildGameLaws") ? 0.25f : 0f) + (WildHas("WildConservation") ? 0.25f : 0f);

    // ---------- State ----------

    /// <summary>This world's wildlife.</summary>
    public WildWorldState Wild
    {
        get
        {
            var w = CurrentWorld;
            if (w.wildlife == null) w.wildlife = new WildWorldState();
            return w.wildlife;
        }
    }

    public WildGlobalState WildGlobal
    {
        get
        {
            if (State.wildGlobal == null) State.wildGlobal = new WildGlobalState();
            return State.wildGlobal;
        }
    }

    public WildHerd FindHerd(int id)
    {
        foreach (var h in Wild.herds) if (h.id == id) return h;
        return null;
    }

    public WildPen PenOf(int building, bool create = false)
    {
        foreach (var p in Wild.pens) if (p.building == building) return p;
        if (!create) return null;
        var pen = new WildPen { building = building };
        Wild.pens.Add(pen);
        return pen;
    }

    // ---------- Simulation ----------

    private const int MaxHerds = 26;
    private const float WildStep = 0.5f;
    private float wildTimer;

    // Per hunting building (instance ids are unique across worlds): seconds to its next kill, and what it last found.
    private readonly Dictionary<int, float> huntKillIn = new Dictionary<int, float>();
    private readonly Dictionary<int, (int animals, float rate, float coin)> huntInfo = new Dictionary<int, (int, float, float)>();

    /// <summary>Herds roam, regrow, get hunted and tamed. Runs on every world (offWorld in the background).</summary>
    private void TickWildlife(float dt)
    {
        if (!offWorld)
        {
            wildTimer += dt;
            if (wildTimer < WildStep) return;
            dt = wildTimer;
            wildTimer = 0f;
        }
        if (Map == null || !IsFounded) return;
        var w = Wild;
        var zones = SettlementZones();

        // New herds wander in from the wild (fast while the land is empty, then slowly).
        int target = Mathf.Clamp(Mathf.RoundToInt(Map.RevealedZoneCount * 0.45f), 4, MaxHerds);
        int ordinary = 0;
        foreach (var h in w.herds) if (!WildSpeciesOf(h.species)?.legendary ?? false) ordinary++;
        w.spawnIn -= dt;
        if (w.spawnIn <= 0f)
        {
            w.spawnIn = (ordinary < target / 2 ? 3f : UnityEngine.Random.Range(50f, 100f)) / RegrowResearch;
            if (ordinary < target) TrySpawnHerd(zones, false);
        }

        // Now and then, a legend.
        w.legendIn -= dt;
        if (w.legendIn <= 0f)
        {
            w.legendIn = UnityEngine.Random.Range(1800f, 3000f);
            bool have = w.herds.Exists(h => WildSpeciesOf(h.species)?.legendary == true);
            if (!have && Map.RevealedZoneCount >= 10) TrySpawnHerd(zones, true);
        }

        TickHerds(w, zones, dt);
        TickHunting(w, dt);
        TickPens(w, dt);
        TickRoaming(w, zones, dt);
    }

    /// <summary>The species that can live at this spot on this world.</summary>
    private void SpeciesFor(Biome biome, bool legendary, List<WildSpecies> into)
    {
        into.Clear();
        bool alien = OnAlienWorld;
        bool fantasyLand = biome == Biome.Crystal || biome == Biome.Fungal || biome == Biome.Volcanic;
        foreach (var s in WildCatalogue)
        {
            if (s.legendary != legendary) continue;
            if (alien != (s.realm == 2)) continue;
            if (System.Array.IndexOf(s.biomes, biome) < 0) continue;
            // At home, fantasy beasts keep to the strange lands; ordinary beasts mostly keep out of them.
            if (!alien && s.realm == 1 && !fantasyLand) continue;
            into.Add(s);
        }
        // An alien world with nothing that fits its land: anything alien that lives on open ground.
        if (into.Count == 0 && alien && !legendary)
            foreach (var s in WildCatalogue)
                if (!s.legendary && s.realm == 2 && System.Array.IndexOf(s.biomes, Biome.Temperate) >= 0) into.Add(s);
    }

    private readonly List<WildSpecies> speciesWork = new List<WildSpecies>();

    /// <summary>How far from your buildings herds keep (towns 12 tiles, outposts 5), so a Hunting Camp at the edge of town (reach 18) finds game.</summary>
    private static float WildBuffer(SettlementZone z, bool legendary) => legendary ? 40f : z.IsOutpost ? 5f : 12f;

    private bool WildSpotClear(Vector2 p, List<SettlementZone> zones, bool legendary)
    {
        int x = Mathf.FloorToInt(p.x), y = Mathf.FloorToInt(p.y);
        if (!Map.InBounds(x, y) || !Map.IsZoneRevealed(Map.ZoneOf(x, y).x, Map.ZoneOf(x, y).y)) return false;
        if (Map.GetTerrain(x, y) == TerrainType.Water || Map.WallAt(x, y) != WallTile.None) return false;
        foreach (var z in zones)
            if (z.Near(p, WildBuffer(z, legendary))) return false;
        foreach (var c in State.camps)
            if (!c.cleared && (c.Centre - p).sqrMagnitude < 36f) return false;
        return true;
    }

    private void TrySpawnHerd(List<SettlementZone> zones, bool legendary)
    {
        var revealed = Map.RevealedZones();
        if (revealed.Count == 0) return;
        for (int attempt = 0; attempt < 10; attempt++)
        {
            var zone = revealed[UnityEngine.Random.Range(0, revealed.Count)];
            var rect = Map.ZoneRect(zone.x, zone.y);
            var p = new Vector2(UnityEngine.Random.Range(rect.xMin + 1f, rect.xMax - 1f), UnityEngine.Random.Range(rect.yMin + 1f, rect.yMax - 1f));
            if (!WildSpotClear(p, zones, legendary)) continue;
            // Not on top of another herd.
            bool crowded = false;
            foreach (var h in Wild.herds) if ((h.pos - p).sqrMagnitude < 100f) { crowded = true; break; }
            if (crowded) continue;
            SpeciesFor(Map.BiomeAt(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y)), legendary, speciesWork);
            if (speciesWork.Count == 0) continue;
            var sp = speciesWork[UnityEngine.Random.Range(0, speciesWork.Count)];
            int n = UnityEngine.Random.Range(sp.herdMin, sp.herdMax + 1);
            var w = Wild;
            w.herds.Add(new WildHerd { id = w.nextId++, species = sp.id, pos = p, home = p, target = p, count = n, max = n, regrowIn = 90f });
            if (legendary)
                Notify($"<color=#ffd56a><b>Hunters speak of {sp.name.Replace("The ", "the ")}</b></color>, seen far out in the wild. {sp.note}");
            return;
        }
    }

    private void TickHerds(WildWorldState w, List<SettlementZone> zones, float dt)
    {
        float regrow = RegrowResearch;
        for (int i = w.herds.Count - 1; i >= 0; i--)
        {
            var h = w.herds[i];
            var sp = WildSpeciesOf(h.species);
            if (sp == null || h.count <= 0) { w.herds.RemoveAt(i); continue; }

            // Wander about home, slowly; faster when running or on the prowl.
            bool fleeing = State.playTime < h.fleeUntil;
            float speed = (fleeing ? 1.6f : h.toTown ? 0.7f : 0.25f) * (sp.flyer ? 1.4f : 1f);
            Vector2 to = h.target - h.pos;
            float d = to.magnitude;
            if (d > 0.05f)
            {
                Vector2 step = to / d * Mathf.Min(d, speed * dt);
                Vector2 next = h.pos + step;
                if (sp.flyer || Map.IsWalkable(new Vector3(next.x, next.y))) h.pos = next;
                else h.target = h.pos;   // water in the way: pick somewhere else
            }
            else if (!h.toTown && UnityEngine.Random.value < dt * 0.08f)
            {
                // Somewhere new to graze, near home, out on open land.
                for (int a = 0; a < 4; a++)
                {
                    var t = h.home + UnityEngine.Random.insideUnitCircle * (sp.role == WildRole.Predator ? 10f : 7f);
                    if (WildSpotClear(t, zones, false) || sp.legendary) { h.target = t; break; }
                }
            }

            // Left in peace, a herd regrows (a reserve nearby helps), up to its size.
            if (h.count < h.max && State.playTime - h.huntedAt > 60f)
            {
                h.regrowIn -= dt * regrow * (NearReserve(h.pos) ? 2f : 1f);
                if (h.regrowIn <= 0f)
                {
                    h.regrowIn = 90f;
                    h.count++;
                }
            }
        }
    }

    private bool NearReserve(Vector2 p)
    {
        foreach (var b in State.buildings)
        {
            if (b.definitionId != "WildReserve" && b.definitionId != "WildXenoLab") continue;
            if (!b.IsBuilt || b.health <= 0) continue;
            var def = Database.GetBuilding(b.definitionId);
            if (def == null) continue;
            float r = HuntReach(b, def);
            if ((BuildingCentre(b, def) - p).sqrMagnitude <= r * r) return true;
        }
        return false;
    }

    private static Vector2 BuildingCentre(BuildingState b, BuildingDefinition def) => b.cell + (Vector2)def.size * 0.5f;

    // ---------- Hunting ----------

    private void TickHunting(WildWorldState w, float dt)
    {
        var food = Database.food;
        if (food == null) return;
        var coin = Database.coin;
        bool coinOpen = coin != null && (coin.unlockedIn == null || coin.unlockedIn.order <= State.ageIndex);
        foreach (var b in State.buildings)
        {
            int hi = HuntIndex(b.definitionId);
            if (hi < 0) continue;
            var def = Database.GetBuilding(b.definitionId);
            if (def == null || !IsWorking(b, def)) { huntInfo.Remove(b.instanceId); continue; }
            float staff = StaffingOf(b);
            var spec = HuntBuildings[hi];
            var stats = StatsOf(def, b.upgradeLevel);
            float reach = HuntReach(b, def);
            Vector2 centre = BuildingCentre(b, def);

            // The game in reach: grazers and boar-like beasts, not predators or legends.
            int animals = 0;
            WildHerd biggest = null;
            foreach (var h in w.herds)
            {
                var sp = WildSpeciesOf(h.species);
                if (sp == null || sp.legendary || sp.role == WildRole.Predator) continue;
                if ((h.pos - centre).sqrMagnitude > reach * reach) continue;
                animals += h.count;
                if (biggest == null || h.count > biggest.count) biggest = h;
            }
            float game = Mathf.Clamp01(animals / 8f);
            float factor = Mathf.Lerp(spec.floor, 1f, game);
            float alien = OnAlienWorld && hi == 3 ? 1.25f : 1f;   // the lab knows alien beasts best
            float activity = staff * (b.health / Mathf.Max(1f, stats.maxHealth)) * stats.output * factor;
            float rate = spec.food * activity * HuntResearch * alien * ProductionMultiplier(food) * WorldProductionFactor(food);
            float coinRate = coinOpen ? spec.coin * activity * ProductionMultiplier(coin) * WorldProductionFactor(coin) : 0f;
            huntInfo[b.instanceId] = (animals, rate, coinRate);
            if (rate > 0f) AddResource(food, rate * dt);
            if (coinRate > 0f) AddResource(coin, coinRate * dt);

            // Every so often the hunters bring one down: hunting thins the herds (less so later on).
            if (biggest == null || staff <= 0f) continue;
            huntKillIn.TryGetValue(b.instanceId, out float left);
            if (left <= 0f) left = 30f;
            left -= dt * staff * spec.thin;
            if (left <= 0f)
            {
                left = 30f;
                bool keepHalf = hi >= 2;   // reserves never hunt a herd below half
                if (!keepHalf || biggest.count > Mathf.Max(1, biggest.max / 2))
                {
                    biggest.count--;
                    biggest.huntedAt = State.playTime;
                    w.hunted++;
                    WildGlobal.animalsHunted++;
                }
            }
            huntKillIn[b.instanceId] = left;
        }
    }

    // ---------- Taming ----------

    private void TickPens(WildWorldState w, float dt)
    {
        // Pens whose building is gone set their beasts free.
        w.pens.RemoveAll(p => !State.buildings.Exists(b => b.instanceId == p.building && IsBeastPen(Database.GetBuilding(b.definitionId))));
        var food = Database.food;
        foreach (var b in State.buildings)
        {
            int pi = PenIndex(b.definitionId);
            if (pi < 0) continue;
            var def = Database.GetBuilding(b.definitionId);
            if (def == null || !IsWorking(b, def)) continue;
            var pen = PenOf(b.instanceId, true);
            // Beasts eat: a little food each, every second.
            if (food != null && pen.beasts.Count > 0)
            {
                float upkeep = PenUpkeep(pen) * dt;
                if (GetAmount(food) < upkeep) { pen.taming = null; continue; }
                AddResource(food, -upkeep);
            }
            float staff = StaffingOf(b);
            if (staff <= 0f) { pen.taming = null; continue; }
            int tier = PenBuildings[pi].tier;
            bool full = pen.beasts.Count >= PenCap(b, def);
            var herd = TamableNear(BuildingCentre(b, def), PenReach(b, def), tier, full);
            if (herd == null) { pen.taming = null; continue; }
            var sp = WildSpeciesOf(herd.species);
            if (pen.taming != herd.species) { pen.taming = herd.species; pen.progress = 0f; }
            float speed = staff * TameResearch * (1f + 0.25f * Mathf.Min(3, b.upgradeLevel)) / (75f * sp.tameTime);
            pen.progress += dt * speed;
            if (pen.progress < 1f) continue;
            pen.progress = 0f;
            pen.taming = null;
            herd.count--;
            herd.huntedAt = State.playTime;
            WildGlobal.beastsTamed++;
            Tamed(pen, sp, def);
        }
    }

    /// <summary>Food a pen's beasts eat each second.</summary>
    private static float PenUpkeep(WildPen pen)
    {
        float f = 0f;
        foreach (var id in pen.beasts)
        {
            var sp = WildSpeciesOf(id);
            f += sp == null ? 0.05f : sp.realm == 2 ? 0.2f : sp.realm == 1 ? 0.12f : 0.04f + 0.03f * sp.tameTime;
        }
        return f;
    }

    /// <summary>A herd in reach that this pen could tame now. When the pen is full, only a legend (for a hero) will do.</summary>
    private WildHerd TamableNear(Vector2 centre, float reach, int tier, bool full)
    {
        WildHerd best = null;
        float bestD = reach * reach;
        bool fantasyOk = WildHas("WildWhispering");
        bool alienOk = WildHas("WildXenofauna");
        foreach (var h in Wild.herds)
        {
            var sp = WildSpeciesOf(h.species);
            if (sp == null || !sp.Tamable || h.count <= 0) continue;
            if (sp.PenTier > tier || sp.tameAge > State.ageIndex) continue;
            if (sp.realm == 1 && !fantasyOk) continue;
            if (sp.realm == 2 && !alienOk) continue;
            if (sp.legendary)
            {
                // A legend bonds with a hero, never sits in a pen: it needs a hero here without one.
                if (!fantasyOk || HeroWithoutLegend() == null) continue;
            }
            else if (full) continue;
            float d = (h.pos - centre).sqrMagnitude;
            if (sp.legendary) d *= 0.25f;   // worth going further for
            if (d < bestD) { bestD = d; best = h; }
        }
        return best;
    }

    /// <summary>A hero on this world without a companion (or with an ordinary one), for a legend to bond with.</summary>
    private HeroState HeroWithoutLegend()
    {
        HeroState pick = null;
        foreach (var hero in State.heroes)
        {
            if (hero.world != State.currentWorld) continue;
            var c = CompanionOf(hero);
            if (c == null) return hero;
            if (!c.legendary && pick == null) pick = hero;
        }
        return pick;
    }

    private void Tamed(WildPen pen, WildSpecies sp, BuildingDefinition def)
    {
        if (sp.legendary)
        {
            var hero = HeroWithoutLegend();
            if (hero != null)
            {
                SetCompanion(hero, sp.id, true);
                WildGlobal.legendsTamed++;
                Notify($"<color=#ffd56a><b>{sp.name} is tamed!</b></color> It has bonded with {hero.name}, and goes where they go. ({CompanionText(sp, true)})");
                if (!offWorld) GameAudio.UI("tame_legend");
                return;
            }
        }
        // Now and then a newly tamed beast takes to one of your heroes instead (if they have no companion yet).
        if (!sp.legendary && UnityEngine.Random.value < 0.3f)
        {
            foreach (var hero in State.heroes)
            {
                if (hero.world != State.currentWorld || CompanionOf(hero) != null) continue;
                SetCompanion(hero, sp.id, false);
                Notify($"Your tamers brought in a {Singular(sp)}, and it took to {hero.name}: a companion now. ({CompanionText(sp, false)})");
                if (!offWorld) GameAudio.UI("tame_success");
                return;
            }
        }
        pen.beasts.Add(sp.id);
        Notify($"Your tamers at the {def.displayName} brought in a {Singular(sp)}: one more {sp.tameName} for your army.");
        if (!offWorld) GameAudio.UI("tame_success");
    }

    private static string Singular(WildSpecies sp) => sp.name.ToLowerInvariant().Replace("the ", "");

    /// <summary>A pen's tamed beast fell in battle.</summary>
    public void PenBeastLost(int building, string species)
    {
        var pen = PenOf(building);
        if (pen != null) pen.beasts.Remove(species);
    }

    // ---------- Beasts on the prowl ----------

    private void TickRoaming(WildWorldState w, List<SettlementZone> zones, float dt)
    {
        w.roamIn -= dt;
        if (w.roamIn <= 0f)
        {
            w.roamIn = UnityEngine.Random.Range(480f, 900f);
            // The nearest hungry predator (or bad-tempered beast) within a day's walk of a town.
            WildHerd pick = null;
            SettlementZone town = null;
            float best = 70f;
            foreach (var h in w.herds)
            {
                var sp = WildSpeciesOf(h.species);
                if (sp == null || sp.legendary || sp.role == WildRole.Grazer || h.toTown) continue;
                foreach (var z in zones)
                {
                    if (z.IsOutpost) continue;
                    float d = z.DistanceTo(h.pos);
                    if (d < best) { best = d; pick = h; town = z; }
                }
            }
            if (pick != null)
            {
                pick.toTown = true;
                var near = town.buildings[UnityEngine.Random.Range(0, town.buildings.Count)];
                pick.target = near.center;
                var sp = WildSpeciesOf(pick.species);
                Notify($"<color=#fb7>{(pick.count > 1 ? $"A pack of {sp.plural}" : $"A {Singular(sp)}")} is prowling towards your town!</color> Your troops will see {(pick.count > 1 ? "them" : "it")} off.");
            }
        }

        // In the background, a prowler that reaches town takes some food and slinks off (in view, it's fought for real).
        if (!offWorld) return;
        foreach (var h in w.herds)
        {
            if (!h.toTown || (h.target - h.pos).sqrMagnitude > 4f) continue;
            h.toTown = false;
            h.target = h.home;
            var food = Database.food;
            if (food == null) continue;
            float took = Mathf.Min(GetAmount(food), 25f * h.count);
            AddResource(food, -took);
            var sp = WildSpeciesOf(h.species);
            Notify($"{(h.count > 1 ? sp.plural.Substring(0, 1).ToUpper() + sp.plural.Substring(1) : "A " + Singular(sp))} got into the stores and made off with {Mathf.RoundToInt(took)} food.");
        }
    }

    /// <summary>A prowler was seen off (or calmed down): it heads back to the wild.</summary>
    public void HerdGoHome(WildHerd h)
    {
        h.toTown = false;
        h.target = h.home;
    }

    // ---------- Hunting by hand: troops that kill wild beasts ----------

    /// <summary>One of a herd fell to your troops: its meat comes home (less than hunters would bring).</summary>
    public void WildBeastKilled(int herdId, string species, Vector3 at)
    {
        var sp = WildSpeciesOf(species);
        if (sp == null) return;
        var h = FindHerd(herdId);
        if (h != null)
        {
            h.count = Mathf.Max(0, h.count - 1);
            h.huntedAt = State.playTime;
            if (h.count == 0) Wild.herds.Remove(h);
        }
        WildGlobal.animalsHunted++;
        var food = Database.food;
        if (!sp.legendary)
        {
            if (food != null) AddResource(food, sp.meat * 0.5f);
            return;
        }
        // A legend brought down: a feast, a trophy, and a tale.
        WildGlobal.legendsSlain++;
        var loot = new List<ResourceAmount>();
        void Give(string id, float amount)
        {
            var r = Database.GetResource(id);
            if (r == null || amount <= 0 || (r.unlockedIn != null && r.unlockedIn.order > State.ageIndex)) return;
            int n = Mathf.RoundToInt(amount * (OnAlienWorld ? XenoRewardFactor : 1f));
            AddResource(r, n);
            loot.Add(new ResourceAmount { resource = r, amount = n });
        }
        Give("Food", sp.meat);
        Give("Gold", 60 + 25 * State.ageIndex);
        Give("Coin", 100 + 40 * State.ageIndex);
        if (sp.realm >= 1) Give("Mana", 80 + 20 * State.ageIndex);
        if (sp.realm == 2) Give("Essence", 60 + 10 * State.ageIndex);
        var item = GainLoot(0.6f, 0.8f, $"on {sp.name.Replace("The ", "the ")}", Rarity.Rare, sp.realm == 2);
        Notify($"<color=#ffd56a><b>{sp.name} has fallen!</b></color> Your hunters bring home {Describe(loot)}{(item != null ? " and a trophy" : "")}.");
        RaiseChanged();
    }

    // ---------- Companions ----------

    public WildCompanion CompanionOf(HeroState hero)
    {
        if (hero == null) return null;
        foreach (var c in WildGlobal.companions) if (c.hero == hero.id) return c;
        return null;
    }

    private void SetCompanion(HeroState hero, string species, bool legendary)
    {
        var c = CompanionOf(hero);
        if (c == null) { c = new WildCompanion { hero = hero.id }; WildGlobal.companions.Add(c); }
        c.species = species;
        c.legendary = legendary;
    }

    /// <summary>A companion's small bonus to its hero (a legend's is bigger): predators hit harder, beasts are tougher, flyers are quicker.</summary>
    public GearStats CompanionStats(HeroState hero)
    {
        var s = new GearStats();
        var c = CompanionOf(hero);
        var sp = c != null ? WildSpeciesOf(c.species) : null;
        if (sp == null) return s;
        float k = c.legendary ? 2.5f : 1f;
        if (sp.flyer) { s.speed += 0.06f * k; s.range += 0.04f * k; }
        else if (sp.role == WildRole.Predator) { s.attack += 0.06f * k; s.speed += 0.03f * k; }
        else { s.health += 0.08f * k; s.armour += 5f * k; }
        return s;
    }

    public static string CompanionText(WildSpecies sp, bool legendary)
    {
        float k = legendary ? 2.5f : 1f;
        if (sp.flyer) return $"+{6 * k:0}% speed and +{4 * k:0}% reach";
        if (sp.role == WildRole.Predator) return $"+{6 * k:0}% attack and +{3 * k:0}% speed";
        return $"+{8 * k:0}% health and +{5 * k:0} armour";
    }

    // ---------- The building panel ----------

    /// <summary>Hunting and taming lines for the selected building's panel (nothing for other buildings).</summary>
    public void AppendWildlifeInfo(StringBuilder sb, BuildingState b, BuildingDefinition def)
    {
        if (def == null || !b.IsBuilt || b.health <= 0) return;
        int hi = HuntIndex(def.id);
        if (hi >= 0)
        {
            int reach = HuntReach(b, def);
            if (huntInfo.TryGetValue(b.instanceId, out var info))
            {
                string coin = info.coin > 0.001f ? $" and {info.coin:0.00} coin" : "";
                sb.Append(info.animals > 0
                    ? $"Game within {reach} tiles: <color=#8f8>{info.animals}</color> animals\nHunting: {info.rate:0.0} food/s{coin}\n"
                    : $"<color=#fb7>No game within {reach} tiles</color>: {info.rate:0.0} food/s{coin}\n<size=80%><color=#aaa>Build it out in the wild, near the herds.</color></size>\n");
            }
            return;
        }
        if (!IsBeastPen(def)) return;
        var pen = PenOf(b.instanceId);
        int cap = PenCap(b, def);
        int have = pen?.beasts.Count ?? 0;
        sb.Append($"War beasts: {have}/{cap}");
        if (have > 0)
        {
            var counts = new Dictionary<string, int>();
            foreach (var id in pen.beasts) counts[id] = counts.TryGetValue(id, out var n) ? n + 1 : 1;
            var parts = new List<string>();
            foreach (var pair in counts) parts.Add($"{pair.Value} {WildSpeciesOf(pair.Key)?.tameName ?? pair.Key}");
            sb.Append($" <size=80%>({string.Join(", ", parts)}; eat {PenUpkeep(pen):0.00} food/s)</size>");
        }
        sb.Append('\n');
        var taming = pen != null ? WildSpeciesOf(pen.taming) : null;
        if (taming != null)
            sb.Append($"Taming {(taming.legendary ? taming.name.Replace("The ", "the ") : "a " + Singular(taming))}: <color=#fd6>{pen.progress:P0}</color>\n");
        else if (have >= cap)
            sb.Append("<size=80%><color=#aaa>Full: upgrade it to keep more.</color></size>\n");
        else
            sb.Append($"<size=80%><color=#fb7>No beasts it can tame within {PenReach(b, def)} tiles.</color></size>\n");
    }
}
