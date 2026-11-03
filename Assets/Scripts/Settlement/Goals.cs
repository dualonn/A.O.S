using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>What an expansion goal asks for.</summary>
public enum GoalKind
{
    Explore, Biome, Camps, Sites, Outpost, Vein, Chief, Population,
    // Out among the stars (once you have Starships)
    Survey, Colony, ColonyOn, Cargo, Anomalies, StarGates, ReachAge, OrbitalMine
}

/// <summary>One of the three goals shown at a time. Progress is measured from saved counters, so it survives a reload.</summary>
[Serializable]
public class GoalState
{
    public GoalKind kind;
    [Tooltip("How much is needed (zones, camps, tiles from home, people...).")]
    public int target;
    [Tooltip("The counter's value when the goal was set; progress counts from here.")]
    public int baseline;
    [Tooltip("Biome goals: which land to reach.")]
    public Biome biome;
    [Tooltip("Colony goals: which kind of world to settle (a WorldProfile id).")]
    public string profile;
}

/// <summary>
/// Expansion goals: three procedural objectives at a time that reward pushing outward
/// (explore, reach new lands, clear camps and wild sites, settle far out, work rich veins, kill chiefs, grow).
/// Each pays resources and a guaranteed good item for your age, then a new one takes its place.
/// Targets grow slowly with every goal done, so they never run out.
/// </summary>
public partial class Settlement
{
    public const int GoalCount = 3;
    private float goalTimer;
    private int biomeScanned;

    // ---------- Counters (called where things happen) ----------

    public void CountChiefKill() => State.chiefsKilled++;

    /// <summary>Lands your explorers have set foot in (from the explored zones' centres).</summary>
    private void ScanBiomes()
    {
        if (Map == null) return;
        if (biomeScanned > State.explored.Count) biomeScanned = 0;
        for (; biomeScanned < State.explored.Count; biomeScanned++)
        {
            var z = State.explored[biomeScanned];
            var c = Map.ZoneRect(z.x, z.y).center;
            int b = (int)Map.BiomeAt((int)c.x, (int)c.y);
            if (!State.biomesVisited.Contains(b)) State.biomesVisited.Add(b);
        }
    }

    /// <summary>How far (in tiles) the furthest finished building is from the town centre.</summary>
    public int FurthestBuilding()
    {
        var centre = State.buildings.Find(b => Database.GetBuilding(b.definitionId)?.isTownCenter == true);
        if (centre == null) return 0;
        float best = 0;
        foreach (var b in State.buildings)
            if (b.buildProgress >= 1f) best = Mathf.Max(best, Vector2Int.Distance(b.cell, centre.cell));
        return Mathf.FloorToInt(best);
    }

    /// <summary>Buildings working a rich vein's seam.</summary>
    private int VeinsWorked()
    {
        int n = 0;
        foreach (var site in State.sites)
        {
            if (site.kind != SiteKind.Vein || !site.done) continue;
            if (State.buildings.Exists(b => b.cell == site.cell && b.buildProgress >= 1f && Database.GetBuilding(b.definitionId)?.requiredDeposit?.id == site.veinId)) n++;
        }
        return n;
    }

    /// <summary>The counter a goal kind is measured by.</summary>
    private int GoalCounter(GoalKind kind) => kind switch
    {
        GoalKind.Explore => State.explored.Count,
        GoalKind.Camps => State.campsCleared,
        GoalKind.Sites => State.sitesDone,
        GoalKind.Chief => State.chiefsKilled,
        GoalKind.Vein => VeinsWorked(),
        GoalKind.Outpost => FurthestBuilding(),
        GoalKind.Population => Mathf.FloorToInt(State.population),
        GoalKind.Survey => State.scannedSystems.Count,
        GoalKind.Colony => FoundedWorlds(),
        GoalKind.Cargo => State.routes.Count,
        GoalKind.Anomalies => State.anomaliesDone.Count,
        GoalKind.StarGates => State.worlds.FindAll(w => HasStarGateOn(w)).Count,
        GoalKind.OrbitalMine => State.orbitalMines.Count,
        _ => 0,
    };

    private int FoundedWorlds() => State.worlds.FindAll(w => IsWorldFounded(w)).Count;

    /// <summary>Starships researched (or a Starport up): goals start pointing out to the stars.</summary>
    private bool SpaceOpen => State.researched.Contains("Starships") || State.worlds.Exists(w => HasStarportOn(w));

    private static readonly string[] ColonyKinds = { "Ice", "Desert", "Ocean", "Volcanic", "Jungle", "Crystal", "Swamp", "Barren", "Fungal", "Temperate" };

    /// <summary>The space ages a goal can point you to (whichever exist in this game).</summary>
    private static readonly string[] SpaceAges = { "StellarAge", "InterstellarAge", "GalacticAge", "UniversalAge" };

    /// <summary>(done so far, needed).</summary>
    public (int have, int need) GoalProgress(GoalState g)
    {
        switch (g.kind)
        {
            case GoalKind.Biome: return (State.biomesVisited.Contains((int)g.biome) ? 1 : 0, 1);
            case GoalKind.ColonyOn: return (State.worlds.Exists(w => w.profile == g.profile && IsWorldFounded(w)) ? 1 : 0, 1);
            case GoalKind.ReachAge: return (State.ageIndex >= g.target ? 1 : 0, 1);
            case GoalKind.Outpost:
            case GoalKind.Population:
            case GoalKind.Colony:
            case GoalKind.Cargo:
            case GoalKind.StarGates:
            case GoalKind.OrbitalMine: return (Mathf.Min(GoalCounter(g.kind), g.target), g.target);
            default: return (Mathf.Clamp(GoalCounter(g.kind) - g.baseline, 0, g.target), g.target);
        }
    }

    public string GoalTitle(GoalState g) => g.kind switch
    {
        GoalKind.Explore => $"Explore {g.target} new zones",
        GoalKind.Biome => $"Set foot in the {BiomeName(g.biome)}",
        GoalKind.Camps => g.target == 1 ? "Burn a raider camp" : $"Burn {g.target} raider camps",
        GoalKind.Sites => $"Clear or loot {g.target} wild sites",
        GoalKind.Outpost => $"Build {g.target} tiles from home",
        GoalKind.Vein => "Work a rich vein",
        GoalKind.Chief => g.target == 1 ? "Kill a raider chief" : $"Kill {g.target} raider chiefs",
        GoalKind.Population => $"Grow to {g.target} people",
        GoalKind.Survey => $"Survey {g.target} star systems",
        GoalKind.Colony => g.target <= 2 ? "Found a colony on another world" : $"Have {g.target} settled worlds",
        GoalKind.ColonyOn => $"Found a colony on {Article(WorldProfile.Get(g.profile).displayName)}",
        GoalKind.Cargo => g.target == 1 ? "Set up a cargo route" : $"Run {g.target} cargo routes",
        GoalKind.Anomalies => $"Deal with {g.target} anomalies",
        GoalKind.StarGates => "Link two worlds with Star Gates",
        GoalKind.ReachAge => $"Reach the {Database.GetAge(g.target)?.displayName ?? "next age"}",
        GoalKind.OrbitalMine => g.target == 1 ? "Build an orbital mine" : $"Run {g.target} orbital mines",
        _ => g.kind.ToString(),
    };

    private static string Article(string name) => ("AEIOU".IndexOf(char.ToUpper(name[0])) >= 0 ? "an " : "a ") + name.ToLower();

    public string GoalHint(GoalState g) => g.kind switch
    {
        GoalKind.Explore => "Send troops past the edge of the map you know.",
        GoalKind.Biome => "Explore until your troops reach that land.",
        GoalKind.Camps => "Raider camps sit out in the wild. Destroy their tents.",
        GoalKind.Sites => "Ruins, dungeons, lairs, shrines, caches and lore stones all count.",
        GoalKind.Outpost => "Any finished building counts, measured from the town centre.",
        GoalKind.Vein => "Clear an old mine's guards, then build the matching mine on its seam.",
        GoalKind.Chief => "Chiefs guard the tougher camps, and lead some raids.",
        GoalKind.Population => "Build housing and keep everyone fed.",
        GoalKind.Survey => "Send probes from a Starport (open the star map). Ancient gates and wormholes chart systems too.",
        GoalKind.Colony => "Send a colony ship to a world you've scanned, then build its Colony Hub.",
        GoalKind.ColonyOn => "Scan systems until you find one, then send a colony ship.",
        GoalKind.Cargo => "Open a colony on the star map and add a route; both worlds need a Starport.",
        GoalKind.Anomalies => "Probes find anomalies; pick a choice on the star map.",
        GoalKind.StarGates => "Build a Star Gate (Galactic Age) on two of your worlds.",
        GoalKind.ReachAge => "Research your way into the next age.",
        GoalKind.OrbitalMine => "From a Starport world, mine an asteroid belt or gas giant in its system (Astro-mining).",
        _ => "",
    };

    public static string BiomeName(Biome b) => b switch
    {
        Biome.Temperate => "green lands",
        Biome.Desert => "desert",
        Biome.Tundra => "frozen tundra",
        Biome.Swamp => "swamps",
        Biome.Volcanic => "volcanic wastes",
        Biome.Crystal => "crystal lands",
        Biome.Barren => "barren plains",
        Biome.Jungle => "jungle",
        Biome.Fungal => "fungal forests",
        _ => b.ToString(),
    };

    // ---------- Rewards ----------

    public List<ResourceAmount> GoalReward(GoalState g)
    {
        int a = State.ageIndex + 1;
        float k = 1f + 0.05f * Mathf.Min(State.goalsDone, 40);
        int R(float v) => Mathf.RoundToInt(v * k / 10f) * 10;
        bool space = g.kind >= GoalKind.Survey;
        return Costs(("Food", R(150 * a)), ("Wood", R(120 * a)), ("Stone", R(90 * a)), ("Gold", R(30 * a)),
            ("Essence", CanEnchant ? R(30 + 10 * a) : 0),
            ("Steel", space ? R(25 * a) : 0), ("PowerCells", space ? R(6 * a) : 0), ("Starmetal", space ? R(2 * a) : 0));
    }

    private void CompleteGoal(GoalState g)
    {
        var reward = GoalReward(g);
        foreach (var r in reward) AddResource(r.resource, r.amount);
        State.goalsDone++;
        Notify($"Goal done: {GoalTitle(g)}! You get {Describe(reward)}.");
        if (!offWorld) GameAudio.UI("goal_complete");
        GainLoot(0.3f, 0.4f, "as a reward", Rarity.Rare);
    }

    // ---------- Choosing goals ----------

    private readonly List<GoalState> pendingOffers = new List<GoalState>();

    private GoalState NewGoal(System.Random rng)
    {
        int step = Mathf.Min(State.goalsDone, 60);   // targets grow slowly, then level off
        var options = pendingOffers;
        options.Clear();
        void Offer(GoalKind kind, int target, float weight = 1f)
        {
            if (State.goals.Exists(x => x.kind == kind)) return;
            var g = new GoalState { kind = kind, target = Mathf.Max(1, target), baseline = GoalCounter(kind) };
            for (int i = 0; i < Mathf.RoundToInt(weight * 2); i++) options.Add(g);
        }
        Offer(GoalKind.Explore, 4 + step / 4, 1.5f);
        Offer(GoalKind.Sites, 2 + step / 8);
        Offer(GoalKind.Camps, 1 + step / 10);
        Offer(GoalKind.Outpost, Mathf.Max(FurthestBuilding(), 12) + 10 + step / 2);
        if (State.ageIndex >= 1) Offer(GoalKind.Chief, 1 + step / 15, 0.5f);
        if (VeinDeposits().Count > 0) Offer(GoalKind.Vein, 1, 0.5f);
        if (Housing > State.population + 5) Offer(GoalKind.Population, Mathf.CeilToInt((State.population + 10 + step) / 5f) * 5, 0.5f);
        if (SpaceOpen) OfferSpaceGoals(Offer, rng);
        // A land you haven't reached yet.
        var unseen = new List<Biome>();
        foreach (Biome b in Enum.GetValues(typeof(Biome)))
            if (!State.biomesVisited.Contains((int)b) && !State.goals.Exists(x => x.kind == GoalKind.Biome)) unseen.Add(b);
        if (unseen.Count > 0)
        {
            Offer(GoalKind.Biome, 1, 1.5f);
            foreach (var g in options) if (g.kind == GoalKind.Biome) g.biome = unseen[rng.Next(unseen.Count)];
        }
        return options.Count > 0 ? options[rng.Next(options.Count)] : null;
    }

    /// <summary>Goals out among the stars: survey, settle, trade, explore anomalies, link worlds, reach the space ages.</summary>
    private void OfferSpaceGoals(System.Action<GoalKind, int, float> offer, System.Random rng)
    {
        int step = Mathf.Min(State.goalsDone, 60);
        offer(GoalKind.Survey, 3 + step / 10, 1f);
        int worlds = FoundedWorlds();
        offer(GoalKind.Colony, worlds + 1, worlds <= 1 ? 1.5f : 0.5f);
        var kinds = new List<string>();
        foreach (var k in ColonyKinds) if (!State.worlds.Exists(w => w.profile == k && IsWorldFounded(w))) kinds.Add(k);
        if (kinds.Count > 0 && !State.goals.Exists(x => x.kind == GoalKind.ColonyOn))
        {
            string pick = kinds[rng.Next(kinds.Count)];
            offer(GoalKind.ColonyOn, 1, 0.75f);
            foreach (var g in pendingOffers) if (g.kind == GoalKind.ColonyOn) g.profile = pick;
        }
        if (worlds >= 2) offer(GoalKind.Cargo, State.routes.Count + 1, 0.75f);
        offer(GoalKind.Anomalies, 2 + step / 20, 0.75f);
        if (Database.GetBuilding("StarGate") != null && State.ageIndex >= Database.ages.FindIndex(a => a != null && a.name == "GalacticAge") && worlds >= 2
            && State.worlds.FindAll(w => HasStarGateOn(w)).Count < 2)
            offer(GoalKind.StarGates, 2, 0.75f);
        if (State.researched.Contains("AstroMining")) offer(GoalKind.OrbitalMine, State.orbitalMines.Count + 1, 0.5f);
        // The next space age you haven't reached.
        foreach (var name in SpaceAges)
        {
            int i = Database.ages.FindIndex(a => a != null && a.name == name);
            if (i > State.ageIndex)
            {
                offer(GoalKind.ReachAge, i, i == State.ageIndex + 1 ? 1f : 0.4f);
                break;
            }
        }
    }

    private void TickGoals(float dt)
    {
        goalTimer -= dt;
        if (goalTimer > 0) return;
        goalTimer = 1f;
        ScanBiomes();
        bool changed = false;
        for (int i = State.goals.Count - 1; i >= 0; i--)
        {
            var g = State.goals[i];
            var (have, need) = GoalProgress(g);
            if (have >= need)
            {
                State.goals.RemoveAt(i);
                CompleteGoal(g);
                changed = true;
            }
        }
        var rng = new System.Random(State.mapSeed ^ (State.goalsDone + 1) * 7349 ^ State.goals.Count * 31);
        while (State.goals.Count < GoalCount)
        {
            var g = NewGoal(rng);
            if (g == null) break;
            State.goals.Add(g);
            changed = true;
        }
        if (changed) RaiseChanged();
    }
}
