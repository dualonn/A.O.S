using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Everything the natural disasters keep in the save (see DisSim.cs).</summary>
[Serializable]
public class DisasterSave
{
    [Tooltip("One entry per world that has had (or is waiting for) a disaster.")]
    public List<DisasterWorld> worlds = new List<DisasterWorld>();
    public int nextId = 1;
}

/// <summary>One world's disasters: what's happening there now, and when the next might come.</summary>
[Serializable]
public class DisasterWorld
{
    [Tooltip("Index into SettlementState.worlds.")]
    public int world;

    [Tooltip("Seconds (of watched play) until the next disaster may come; -1 = not rolled yet.")]
    public float nextIn = -1f;

    public List<DisasterState> active = new List<DisasterState>();

    [Tooltip("Kinds seen lately, newest last (so the same one doesn't come twice running).")]
    public List<string> recent = new List<string>();
}

/// <summary>A disaster under way (or its scars fading after it).</summary>
[Serializable]
public class DisasterState
{
    public int id;
    [Tooltip("Flood, Wildfire, Volcano, Quake, Plague or Meteors.")]
    public string kind;
    [Tooltip("Where it is: the river bank, the first tree alight, the vent, the epicentre...")]
    public Vector2 centre;
    public float radius;
    public float severity = 1f;

    [Tooltip("Seconds of warning left before it strikes.")]
    public float warn;
    public bool struck;
    [Tooltip("The warning came early (a seismoscope).")]
    public bool warnedEarly;
    [Tooltip("Seconds it has left once struck.")]
    public float left;
    [Tooltip("It's over: only its scars (marks) are left, fading.")]
    public bool over;

    [Tooltip("Live tiles: flooded, burning, lava, or meteors on their way (with times).")]
    public List<Vector2Int> tiles = new List<Vector2Int>();
    public List<float> times = new List<float>();

    [Tooltip("Scars fading on the ground (receding water, burnt forest, cooling rock, craters), and when each goes.")]
    public List<Vector2Int> marks = new List<Vector2Int>();
    public List<float> markTimes = new List<float>();

    [Tooltip("Buildings alight, or houses where the sickness is (instance ids), with times.")]
    public List<int> hit = new List<int>();
    public List<float> hitTimes = new List<float>();

    [Tooltip("Buildings damaged so far (instance ids).")]
    public List<int> damaged = new List<int>();

    public int ruined, lost, saved, burnt;
    public float pulse, pulse2, carry;
    public int ring;
}

/// <summary>What a counter building protects against.</summary>
public enum DisGuardKind { Fire, Water, Ash, Meteor, Heal }

/// <summary>The disaster counter buildings, by id (assets from Tools > Age of Stickman > Setup Disasters).</summary>
public static class DisCatalog
{
    public const string Well = "DisWell", FireWatch = "DisFireWatch", FireStation = "DisFireStation", FireDrones = "DisFireDrones",
        Levee = "DisLevee", FloodBarrier = "DisFloodBarrier", Seismoscope = "DisSeismoscope", AshShelter = "DisAshShelter",
        ShieldDome = "DisShieldDome";

    public const string TechRaised = "DisRaisedFoundations", TechQuakeFrames = "DisQuakeFrames";

    public struct Guard
    {
        public DisGuardKind kind;
        public int radius;
        [Tooltip("Fire: how well its crew put fires out (0..1).")]
        public float power;
        public Guard(DisGuardKind kind, int radius, float power = 1f) { this.kind = kind; this.radius = radius; this.power = power; }
    }

    /// <summary>What each counter building guards, and how far (tiles from its middle).</summary>
    public static readonly Dictionary<string, Guard[]> Guards = new Dictionary<string, Guard[]>
    {
        { Well, new[] { new Guard(DisGuardKind.Fire, 6, 0.3f), new Guard(DisGuardKind.Water, 6) } },
        { FireWatch, new[] { new Guard(DisGuardKind.Fire, 9, 0.45f), new Guard(DisGuardKind.Water, 8) } },
        { FireStation, new[] { new Guard(DisGuardKind.Fire, 12, 0.65f), new Guard(DisGuardKind.Water, 10) } },
        { FireDrones, new[] { new Guard(DisGuardKind.Fire, 16, 0.85f), new Guard(DisGuardKind.Water, 12) } },
        { AshShelter, new[] { new Guard(DisGuardKind.Ash, 8) } },
        { ShieldDome, new[] { new Guard(DisGuardKind.Meteor, 13), new Guard(DisGuardKind.Ash, 13) } },
    };

    /// <summary>Healers (anything that trains medics) shelter the sick this far round them.</summary>
    public const int HealRadius = 14;

    /// <summary>Extra seconds of warning a seismoscope gives (plus some per upgrade).</summary>
    public const float SeismoWarning = 35f, SeismoPerUpgrade = 15f;
}
