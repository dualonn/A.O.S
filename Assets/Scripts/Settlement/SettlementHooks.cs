using System;
using System.Collections.Generic;

/// <summary>
/// Shared extension points so several systems can feed mood, production, research and growth
/// without all patching the same lines. Register from a [RuntimeInitializeOnLoadMethod]
/// with Add(...) (adding the same static method twice is ignored). Every source runs on the
/// world currently being ticked (State is that world's while TickBackground runs).
/// </summary>
public static class SettlementHooks
{
    /// <summary>Adds mood reasons for the world being worked out: add(amount, reason). Runs once a second per world.</summary>
    public delegate void MoodSource(Settlement s, Action<float, string> add);
    /// <summary>A multiplier on one building's output of one resource (1 = no change).</summary>
    public delegate float BuildingFactor(Settlement s, BuildingState building, BuildingDefinition def, ResourceType resource);
    /// <summary>A whole-world multiplier (1 = no change).</summary>
    public delegate float WorldFactor(Settlement s);

    public static readonly List<MoodSource> Mood = new List<MoodSource>();
    public static readonly List<BuildingFactor> Production = new List<BuildingFactor>();
    public static readonly List<WorldFactor> Research = new List<WorldFactor>();
    public static readonly List<WorldFactor> Growth = new List<WorldFactor>();

    public static void Add<T>(List<T> list, T source) where T : Delegate
    {
        if (source != null && !list.Contains(source)) list.Add(source);
    }

    public static void MoodOf(Settlement s, Action<float, string> add)
    {
        for (int i = 0; i < Mood.Count; i++) Mood[i](s, add);
    }

    public static float ProductionOf(Settlement s, BuildingState b, BuildingDefinition def, ResourceType r)
    {
        float f = 1f;
        for (int i = 0; i < Production.Count; i++) f *= Production[i](s, b, def, r);
        return f;
    }

    public static float ResearchOf(Settlement s)
    {
        float f = 1f;
        for (int i = 0; i < Research.Count; i++) f *= Research[i](s);
        return f;
    }

    public static float GrowthOf(Settlement s)
    {
        float f = 1f;
        for (int i = 0; i < Growth.Count; i++) f *= Growth[i](s);
        return f;
    }
}
