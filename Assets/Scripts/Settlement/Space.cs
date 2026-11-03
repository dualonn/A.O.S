using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Your place among the stars: which planet each of your worlds is, which systems you've
/// scanned, and how far and fast your ships go (from Navigation and Star Drive research).
/// </summary>
public partial class Settlement
{
    /// <summary>Is the world you're on another world (not the home world)? Its wild places are alien.</summary>
    public bool OnAlienWorld => State != null && CurrentWorld.profile != WorldProfile.HomeId;

    /// <summary>The native people's fighters on the world you're on (none at home).</summary>
    public List<UnitDefinition> NativeUnits()
    {
        string prefix = WorldProfile.Get(CurrentWorld.profile).natives;
        if (string.IsNullOrEmpty(prefix)) return new List<UnitDefinition>();
        return Database.natives.FindAll(u => u != null && !u.isBoss && u.name.StartsWith(prefix + "_"));
    }

    /// <summary>The native people's chief on the world you're on (null at home).</summary>
    public UnitDefinition NativeChief()
    {
        string prefix = WorldProfile.Get(CurrentWorld.profile).natives;
        if (string.IsNullOrEmpty(prefix)) return null;
        return Database.natives.Find(u => u != null && u.isBoss && u.name.StartsWith(prefix + "_"));
    }

    /// <summary>The galaxy is built from the save's seed.</summary>
    private void EnsureGalaxy() => Galaxy.Init(State.mapSeed);

    /// <summary>The planet a world is on.</summary>
    public Planet PlanetOf(WorldState w)
    {
        EnsureGalaxy();
        var s = Galaxy.Get(w.system);
        return w.planet >= 0 && w.planet < s.bodies.Count ? s.bodies[w.planet] : null;
    }

    /// <summary>The planet you're looking at.</summary>
    public Planet CurrentPlanet => State != null ? PlanetOf(CurrentWorld) : null;

    /// <summary>Which of your worlds is on this planet (-1: none).</summary>
    public int WorldIndexOf(Planet p)
    {
        if (p == null) return -1;
        for (int i = 0; i < State.worlds.Count; i++)
            if (State.worlds[i].system == p.system.key && State.worlds[i].planet == p.index) return i;
        return -1;
    }

    /// <summary>Systems where you have a settled world.</summary>
    public List<StarSystem> SettledSystems()
    {
        EnsureGalaxy();
        var list = new List<StarSystem>();
        foreach (var w in State.worlds)
        {
            if (!IsWorldFounded(w)) continue;
            var s = Galaxy.Get(w.system);
            if (!list.Contains(s)) list.Add(s);
        }
        return list;
    }

    public bool HasColonyIn(StarSystem s) => State.worlds.Exists(w => w.system == s.key && IsWorldFounded(w));

    /// <summary>Has this system been scanned (its worlds known)? Your home system always has.</summary>
    public bool IsScanned(StarSystem s) => s.IsHome || State.scannedSystems.Contains(s.key) || HasColonyIn(s);

    public void MarkScanned(StarSystem s)
    {
        if (!State.scannedSystems.Contains(s.key)) State.scannedSystems.Add(s.key);
    }

    /// <summary>How far ships can fly from a settled system, in light-years (Navigation research).</summary>
    public float ShipReach
    {
        get
        {
            float reach = 0f;
            foreach (var id in State.researched)
            {
                var t = Database.GetTechnology(id);
                if (t != null) reach += t.navigationRange;
            }
            return reach;
        }
    }

    /// <summary>How fast ships fly (Star Drive research): 1 = the first drives.</summary>
    public float ShipSpeed
    {
        get
        {
            float speed = 1f;
            foreach (var id in State.researched)
            {
                var t = Database.GetTechnology(id);
                if (t != null) speed += t.driveSpeed;
            }
            return speed;
        }
    }

    /// <summary>Can ships reach this system (it's yours, or within reach of one of your settled systems)?</summary>
    public bool InReach(StarSystem s) => InReachDirect(s) || (WormholeFrom(s) is StarSystem other && InReachDirect(other));

    private bool InReachDirect(StarSystem s)
    {
        float reach = ShipReach;
        foreach (var home in SettledSystems())
            if (home == s || Galaxy.Distance(home, s) <= reach + 0.01f) return true;
        return false;
    }

    /// <summary>A line per world for the star map.</summary>
    public string WorldsSummary()
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < State.worlds.Count; i++)
        {
            var w = State.worlds[i];
            sb.Append(i == State.currentWorld ? "> " : "  ");
            sb.Append($"<b>{w.name}</b> <color=#aaa>({WorldProfile.Get(w.profile).displayName}, {w.buildings.Count} buildings, {Mathf.FloorToInt(w.population)} people)</color>\n");
        }
        return sb.ToString();
    }
}
