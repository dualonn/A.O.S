using UnityEngine;

/// <summary>
/// Worlds you're not looking at keep running: their buildings work, people eat and grow, camps
/// stir, and raids come. Raids there are fought out by comparing strengths (as the battle
/// auto-resolve does), and the news comes to you with the world's name on it.
/// </summary>
public partial class Settlement
{
    /// <summary>True while a world in the background is being ticked (its state is swapped in).</summary>
    private bool offWorld;

    // Background worlds tick in steps, not every frame: cheaper, and nothing there needs smoothness.
    private const float OffWorldStep = 0.5f;
    private float offWorldTimer;

    /// <summary>Is this world founded (has a town centre)?</summary>
    public bool IsWorldFounded(WorldState world) =>
        world.buildings.Exists(b => Database.GetBuilding(b.definitionId)?.isTownCenter == true);

    private void TickOtherWorlds(float dt)
    {
        if (State.worlds.Count <= 1) return;
        offWorldTimer += dt;
        if (offWorldTimer < OffWorldStep) return;
        float step = offWorldTimer;
        offWorldTimer = 0f;
        for (int i = 0; i < State.worlds.Count; i++)
        {
            if (i == State.currentWorld || !IsWorldFounded(State.worlds[i])) continue;
            RunOnWorld(i, () => TickBackground(step));
        }
    }

    /// <summary>Run something with another world swapped in as the current one (its state and map), then swap back.</summary>
    public void RunOnWorld(int world, System.Action action)
    {
        if (world == State.currentWorld) { action(); return; }
        int home = State.currentWorld;
        var homeMap = Map;
        bool wasOffWorld = offWorld;
        var cache = zonesCache;
        State.currentWorld = world;
        Map = null;   // (so MapOf gives that world's map)
        Map = MapOf(world);
        offWorld = true;
        zonesCache = null;
        try
        {
            action();
        }
        finally
        {
            worldMaps[world] = Map;
            State.currentWorld = home;
            Map = homeMap;
            offWorld = wasOffWorld;
            zonesCache = cache;
            RecalculateDerived();
        }
    }

    private void TickBackground(float dt)
    {
        TickConstruction(dt);
        TickUpgrades(dt);
        RecalculateDerived();
        TickProduction(dt);
        TickUpkeep(dt);
        TickPopulation(dt);
        TickAutomation(dt);
        TickCamps(dt);
        TickSites(dt);
        TickBackgroundRaids(dt);
    }

    /// <summary>A background world's raid timer; raids there are settled by strength.</summary>
    private void TickBackgroundRaids(float dt)
    {
        State.secondsUntilWave -= dt;
        if (State.secondsUntilWave > 0) return;
        State.secondsUntilWave = Random.Range(Database.waveIntervalRange.x, Database.waveIntervalRange.y);
        var wave = Waves.Generate();
        if (wave.units.Count == 0) return;
        float severity = Waves.AutoResolve(wave);
        Notify(severity <= 0
            ? $"Raiders attacked and were driven off ({wave.units.Count} of them)."
            : severity < 0.5f
                ? $"Raiders broke through and did some damage before they were driven off."
                : $"Raiders overran the defences! Heavy damage and losses.");
    }
}
