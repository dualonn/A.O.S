using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Ages are grown into, not bought. Each age has a progress score from what defines it: its
/// research, its kinds of buildings, stockpiles of its key resources and the size of your people.
/// You enter the next age by yourself once its gate research is done (e.g. Philosophy for the
/// Classical Age) and you're at least halfway through the age you're in.
/// </summary>
public partial class Settlement
{
    /// <summary>How far through the current age you need to be before its gate research takes you on.</summary>
    public const float AgeEntryProgress = 0.5f;

    private const float WeightResearch = 0.4f, WeightBuildings = 0.3f, WeightResources = 0.2f, WeightPopulation = 0.1f;

    public class AgeProgressInfo
    {
        public float total, research, buildings, resources, population;
        public int researchDone, researchTotal, buildingsDone, buildingsTotal;
        public readonly List<string> missingResearch = new List<string>();
        public readonly List<string> missingBuildings = new List<string>();
        public readonly List<string> resourceGoals = new List<string>();
        public int populationGoal;
    }

    /// <summary>How far through an age you are (0..1), and what's left.</summary>
    public AgeProgressInfo AgeProgress(int index)
    {
        var info = new AgeProgressInfo();
        var age = Database.GetAge(index);
        if (age == null) return info;

        foreach (var tech in Database.technologies)
        {
            if (tech == null) continue;
            var techAge = tech.age != null ? tech.age : Database.GetAge(0);
            if (techAge != age) continue;
            info.researchTotal++;
            if (HasResearched(tech)) info.researchDone++;
            else info.missingResearch.Add(tech.displayName);
        }
        foreach (var def in Database.buildings)
        {
            // Walls are optional defences, so they don't count towards an age.
            if (def == null || def.age != age || def.IsWall) continue;
            info.buildingsTotal++;
            if (HaveOrOutgrew(def)) info.buildingsDone++;
            else info.missingBuildings.Add(def.displayName);
        }
        // Resources and people: the goals the next age used to ask for (the latest age keeps its own).
        var goals = (Database.GetAge(index + 1) ?? age).advanceRequirements;
        float resourceShare = 1f;
        int counted = 0;
        foreach (var item in goals.cost)
        {
            if (item.resource == null || item.amount <= 0) continue;
            float have = Mathf.Clamp01(GetAmount(item.resource) / (float)item.amount);
            resourceShare = counted == 0 ? have : resourceShare + have;
            counted++;
            if (have < 1f) info.resourceGoals.Add($"{GetAmount(item.resource):0}/{item.amount} {item.resource.displayName}");
        }
        info.resources = counted > 0 ? resourceShare / counted : 1f;
        info.populationGoal = goals.population;
        info.population = goals.population > 0 ? Mathf.Clamp01(Population / (float)goals.population) : 1f;
        info.research = info.researchTotal > 0 ? info.researchDone / (float)info.researchTotal : 1f;
        info.buildings = info.buildingsTotal > 0 ? info.buildingsDone / (float)info.buildingsTotal : 1f;
        info.total = info.research * WeightResearch + info.buildings * WeightBuildings
                   + info.resources * WeightResources + info.population * WeightPopulation;
        return info;
    }

    /// <summary>
    /// You have one of these standing, or one ranked up into something you have (huts that became
    /// houses still count for the Stone Age).
    /// </summary>
    private bool HaveOrOutgrew(BuildingDefinition def)
    {
        int steps = 0;
        for (var d = def; d != null && steps++ < 12; d = d.rankUpTo)
        {
            if (CountBuilt(d) > 0) return true;
        }
        return false;
    }

    /// <summary>Progress through the age you're in (0..1).</summary>
    public float CurrentAgeProgress => AgeProgress(State.ageIndex).total;

    /// <summary>The research that opens the next age (none = you only need to finish this one).</summary>
    public List<Technology> NextAgeGate => NextAge != null ? NextAge.advanceRequirements.technologies : new List<Technology>();

    /// <summary>Ready to go on? (the gate research is done and you're far enough through this age)</summary>
    public bool CanAdvanceAge(out string missing)
    {
        missing = "";
        if (NextAge == null)
        {
            missing = "This is the latest age";
            return false;
        }
        var sb = new StringBuilder();
        foreach (var tech in NextAgeGate)
            if (tech != null && !HasResearched(tech)) sb.AppendLine($"Research {tech.displayName}");
        float p = CurrentAgeProgress;
        float need = NextAgeGate.Count > 0 ? AgeEntryProgress : 1f;
        if (p < need) sb.AppendLine($"Reach {need:P0} through the {CurrentAge.displayName} (now {p:P0})");
        missing = sb.ToString().TrimEnd();
        return missing.Length == 0;
    }

    private float nextAgeCheck;
    private int masteredNotified = -1;

    /// <summary>Move into the next age by yourself once you're ready; mark mastering an age.</summary>
    private void TickAges()
    {
        if (Time.unscaledTime < nextAgeCheck) return;
        nextAgeCheck = Time.unscaledTime + 1f;
        if (masteredNotified < 0) masteredNotified = CurrentAgeProgress >= 0.999f ? State.ageIndex : -2;
        if (CanAdvanceAge(out _))
        {
            State.ageIndex++;
            Notify($"Your civilisation has entered the <color=#fd6>{CurrentAge.displayName}</color>! New research and buildings are open to you.");
            Changed?.Invoke();
            return;
        }
        if (masteredNotified != State.ageIndex && CurrentAgeProgress >= 0.999f)
        {
            masteredNotified = State.ageIndex;
            Notify($"You have mastered the {CurrentAge.displayName}.");
        }
    }
}
