using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>What an expansion goal asks for.</summary>
public enum GoalKind { Explore, Biome, Camps, Sites, Outpost, Vein, Chief, Population }

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
        _ => 0,
    };

    /// <summary>(done so far, needed).</summary>
    public (int have, int need) GoalProgress(GoalState g)
    {
        switch (g.kind)
        {
            case GoalKind.Biome: return (State.biomesVisited.Contains((int)g.biome) ? 1 : 0, 1);
            case GoalKind.Outpost:
            case GoalKind.Population: return (Mathf.Min(GoalCounter(g.kind), g.target), g.target);
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
        _ => g.kind.ToString(),
    };

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
        _ => b.ToString(),
    };

    // ---------- Rewards ----------

    public List<ResourceAmount> GoalReward(GoalState g)
    {
        int a = State.ageIndex + 1;
        float k = 1f + 0.05f * Mathf.Min(State.goalsDone, 40);
        int R(float v) => Mathf.RoundToInt(v * k / 10f) * 10;
        return Costs(("Food", R(150 * a)), ("Wood", R(120 * a)), ("Stone", R(90 * a)), ("Gold", R(30 * a)),
            ("Essence", CanEnchant ? R(30 + 10 * a) : 0));
    }

    private void CompleteGoal(GoalState g)
    {
        var reward = GoalReward(g);
        foreach (var r in reward) AddResource(r.resource, r.amount);
        State.goalsDone++;
        Notify($"Goal done: {GoalTitle(g)}! You get {Describe(reward)}.");
        GainLoot(0.3f, 0.4f, "as a reward", Rarity.Rare);
    }

    // ---------- Choosing goals ----------

    private GoalState NewGoal(System.Random rng)
    {
        int step = Mathf.Min(State.goalsDone, 60);   // targets grow slowly, then level off
        var options = new List<GoalState>();
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
        if (changed) Changed?.Invoke();
    }
}
