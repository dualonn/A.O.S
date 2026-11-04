using UnityEngine;

/// <summary>
/// Small read-only summaries the Advisor card (FreeMode/AdvisorPanel.cs) reads. Kept in one place so
/// that when a system's internals change (utilities especially), only these few lines need touching.
/// Nothing here is saved and nothing here changes the game.
/// </summary>
public partial class Settlement
{
    /// <summary>The utilities of the world you're looking at, as the advisor needs them.</summary>
    public struct AdvisorUtlSummary
    {
        public bool brownout, hasGrid;
        public int unpowered;
        public float supply, demand, waterCoverage, waterNeed, smogWorst;
        public Vector2 smogAt;
    }

    public AdvisorUtlSummary AdvisorUtilities()
    {
        var s = new AdvisorUtlSummary();
        if (State == null || State.worlds.Count == 0) return s;
        var n = UtilitiesHere;
        if (n == null) return s;
        s.brownout = n.brownout;
        s.hasGrid = n.HasGrid;
        s.unpowered = n.unpowered;
        s.supply = n.supply;
        s.demand = n.demand;
        s.waterCoverage = n.WaterCoverage;
        s.waterNeed = n.waterNeed;
        s.smogWorst = n.smogWorst;
        s.smogAt = n.worstAt;
        return s;
    }

    /// <summary>A world's food in store (any world, live or in the background).</summary>
    public float AdvisorFoodOf(WorldState w)
    {
        var food = Database != null ? Database.food : null;
        if (food == null || w == null) return 0f;
        var list = w.resources;
        for (int i = 0; i < list.Count; i++)
            if (list[i].id == food.id) return list[i].amount;
        return 0f;
    }

    /// <summary>The name of the building nearest a point (for "smog over the forge quarter"), or null.</summary>
    public string AdvisorNearestBuildingName(Vector2 at, out Vector2 where)
    {
        where = at;
        string best = null;
        float bestD = float.MaxValue;
        var list = State.buildings;
        for (int i = 0; i < list.Count; i++)
        {
            var b = list[i];
            var def = Database.GetBuilding(b.definitionId);
            if (def == null) continue;
            var c = new Vector2(b.cell.x + def.size.x * 0.5f, b.cell.y + def.size.y * 0.5f);
            float d = (c - at).sqrMagnitude;
            if (d < bestD) { bestD = d; best = def.displayName; where = c; }
        }
        return best;
    }
}
