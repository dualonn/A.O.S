using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Why a war party set out (shown in the threat tooltip and the warnings).</summary>
public enum RaidMotive { Plunder, Vengeance, Season, Chief, Wealth, Hunger, Wilds, Sent }

/// <summary>
/// One world's raid threat (see RaidMarch.cs): how restless each camp is, the roaming bands of the
/// wilds, and the war parties marching across the map. Old saves start with an empty one and are
/// set up from their old raid countdown the first time the world ticks.
/// </summary>
[Serializable]
public class RaidWorld
{
    [Tooltip("Set up (old saves and new worlds start false: the first raid follows the old countdown).")]
    public bool started;

    [Tooltip("Play time of the first raid's muster (0 = none pending).")]
    public float nextForcedAt;

    [Tooltip("No war party musters before this play time (a camp burnt, a breather after a raid).")]
    public float holdUntil;

    [Tooltip("0..1: the roaming bands of the wilds (they only gather while no camp is in reach).")]
    public float wildsReady;

    public List<RaidCampMood> camps = new List<RaidCampMood>();
    public List<RaidParty> parties = new List<RaidParty>();

    public int nextPartyId = 1;
    public int partiesSent, partiesScattered;

    [Tooltip("Play time of the last 'the camp is rallying' note (they're kept rare).")]
    public float lastRallyNote = -999f;

    // ---------- Runtime only (not saved) ----------

    [NonSerialized] public float lastWritten = -1f;
    [NonSerialized] public float sourcesAt = -1f, lookoutsAt = -1f, checkAt, estimateAt;
    [NonSerialized] public readonly List<RaidSource> sources = new List<RaidSource>();
    [NonSerialized] public readonly List<RaidLookout> lookouts = new List<RaidLookout>();
    [NonSerialized] public bool globalSight;
    [NonSerialized] public float techReach, bestReach;
    [NonSerialized] public int towers;
    [NonSerialized] public bool wasLive;
}

/// <summary>How a raider camp feels about you.</summary>
[Serializable]
public class RaidCampMood
{
    public Vector2Int cell;

    [Tooltip("0..1: fighters gathered for the next war party (at 1 it sets out).")]
    public float ready;

    [Tooltip("0..1: anger at being attacked (raises attention, fades over ten minutes).")]
    public float anger;

    public int sent;
    public bool rallyNoted;
    public bool wasCleared;
}

/// <summary>A war party on its way (or gathering at its camp).</summary>
[Serializable]
public class RaidParty
{
    public int id;
    public bool fromCamp;
    public Vector2Int campCell;
    public Vector2 origin, target;

    [Tooltip("Tiles marched, of 'length' (arrival is just outside the target town's safe zone).")]
    public float travelled, length;
    public float speed;

    [Tooltip("Seconds left gathering at the camp before it sets out.")]
    public float gatherLeft;

    [Tooltip("Unit asset names (the war party's make-up, chosen when it musters).")]
    public List<string> units = new List<string>();
    public string boss;
    public float strength;
    public int motive;
    public int groups = 1;
    public bool spotted;
    public float setOut;

    [Tooltip("For the warnings: where it set out from ('the camp to the north-east'), its short bearing ('NE') and the town it's after.")]
    public string from, dir, town;

    /// <summary>Where it is now: 'travelled' tiles from its origin, straight towards its target.</summary>
    public Vector2 Position
    {
        get
        {
            Vector2 d = target - origin;
            float l = d.magnitude;
            return l < 0.01f ? target : origin + d / l * Mathf.Min(travelled, l);
        }
    }

    public float SecondsLeft => gatherLeft + Mathf.Max(0f, length - travelled) / Mathf.Max(0.1f, speed);
}

/// <summary>Where war parties come from (worked out every couple of seconds).</summary>
public class RaidSource
{
    public CampState camp;   // null = the wilds
    public RaidCampMood mood;
    public float attention, rate, distance;
    public Settlement.SettlementZone target;
    public Vector2 at;
}

/// <summary>Something that watches for war parties: a tower, a lookout, a town's own people.</summary>
public struct RaidLookout
{
    public Vector2 at;
    public float reach;
}
