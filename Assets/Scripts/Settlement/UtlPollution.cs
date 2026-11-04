using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pollution on a coarse grid of 8x8-tile cells, per world (see Utilities.cs). Every few seconds
/// each source puts its pollution into its cell (and a third of it into the cells round it);
/// forests, parks, scrubbers and terraformers take it out; recycling plants and research cut
/// what the sources give off. The air follows that slowly (it takes a quarter of a minute or so
/// to clear or thicken). Thin air holds smog longer, storms blow it away.
/// </summary>
public partial class Settlement
{
    private static long UtlCellKey(int cx, int cy) => ((long)cx << 32) ^ (uint)cy;

    private static int UtlCellOf(float v) => Mathf.FloorToInt(v / UtlCatalog.Cell);

    /// <summary>The pollution in the air at a point (0 = clean).</summary>
    public float UtlSmogAt(UtlNet n, float x, float y)
    {
        if (n == null || n.smog.Count == 0) return 0f;
        return n.smog.TryGetValue(UtlCellKey(UtlCellOf(x), UtlCellOf(y)), out float p) ? p : 0f;
    }

    private readonly List<long> utlKeys = new List<long>();

    /// <summary>Forest tiles in a cell (counted once a minute: fires and felling change them).</summary>
    private int UtlForestIn(UtlNet n, long key, int cx, int cy)
    {
        if (n.forest.TryGetValue(key, out int count)) return count;
        count = 0;
        DepositType forest = null;
        foreach (var d in Map.Deposits) if (d != null && d.id == "Forest") { forest = d; break; }
        if (forest != null)
            for (int y = cy * UtlCatalog.Cell; y < (cy + 1) * UtlCatalog.Cell; y++)
                for (int x = cx * UtlCatalog.Cell; x < (cx + 1) * UtlCatalog.Cell; x++)
                    if (Map.GetDeposit(x, y) == forest) count++;
        n.forest[key] = count;
        return count;
    }

    private void UtlPollute(UtlNet n)
    {
        if (State.playTime - n.forestAt > 60f) { n.forest.Clear(); n.forestAt = State.playTime; }
        var target = n.smogTarget;
        target.Clear();

        // How much the sources give off: research, recycling, the world's air.
        float tech = (Researched(UtlCatalog.TechSmokeFilters) ? 0.85f : 1f) * (Researched(UtlCatalog.TechScrubbers) ? 0.85f : 1f)
                     * (Researched(UtlCatalog.TechSmartGrid) ? 0.9f : 1f) * (Researched(UtlCatalog.TechAtmoEng) ? 0.75f : 1f);
        if (HasTrait(ThinAir)) tech *= 1.25f;
        if (HasTrait(Storms)) tech *= 0.8f;
        foreach (var s in utlSources)
        {
            float p = s.amount * tech;
            foreach (var r in utlRecyclers)
            {
                float reach = r.cover + s.half;
                if ((r.at - s.at).sqrMagnitude <= reach * reach) { p *= r.amount; break; }
            }
            int cx = UtlCellOf(s.at.x), cy = UtlCellOf(s.at.y);
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    long k = UtlCellKey(cx + dx, cy + dy);
                    target.TryGetValue(k, out float v);
                    target[k] = v + (dx == 0 && dy == 0 ? p : p * 0.35f);
                }
        }
        if (target.Count > 0)
        {
            // Parks, scrubbers and terraformers.
            foreach (var a in utlAbsorbers)
            {
                int cx = UtlCellOf(a.at.x), cy = UtlCellOf(a.at.y), r = Mathf.Max(0, a.spec.cover);
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        long k = UtlCellKey(cx + dx, cy + dy);
                        if (!target.TryGetValue(k, out float v)) continue;
                        target[k] = v - (dx == 0 && dy == 0 ? a.amount : a.amount * 0.5f);
                    }
            }
            // Forests breathe it in.
            utlKeys.Clear();
            foreach (var k in target.Keys) utlKeys.Add(k);
            float perForest = 5f / (UtlCatalog.Cell * UtlCatalog.Cell);
            foreach (var k in utlKeys)
            {
                float v = target[k];
                if (v > 0f) v -= UtlForestIn(n, k, (int)(k >> 32), (int)(uint)(k & 0xffffffffL)) * perForest;
                target[k] = Mathf.Max(0f, v);
            }
        }

        // The air follows slowly (straight to it the first time, e.g. after loading).
        bool first = n.smog.Count == 0;
        foreach (var pair in target)
        {
            n.smog.TryGetValue(pair.Key, out float cur);
            n.smog[pair.Key] = first ? pair.Value : cur + (pair.Value - cur) * 0.25f;
        }
        utlKeys.Clear();
        foreach (var k in n.smog.Keys) if (!target.ContainsKey(k)) utlKeys.Add(k);
        foreach (var k in utlKeys)
        {
            float v = n.smog[k] * 0.75f;
            if (v < 0.05f) n.smog.Remove(k); else n.smog[k] = v;
        }
        utlKeys.Clear();
        foreach (var pair in n.smog) if (pair.Value < 0.05f) utlKeys.Add(pair.Key);
        foreach (var k in utlKeys) n.smog.Remove(k);

        // How it is where people live, and the worst of it.
        float sum = 0f, weight = 0f, worst = 0f;
        foreach (var h in utlHouses)
        {
            float p = UtlSmogAt(n, h.at.x, h.at.y);
            sum += p * h.amount;
            weight += h.amount;
        }
        Vector2 worstAt = default;
        foreach (var pair in n.smog)
            if (pair.Value > worst)
            {
                worst = pair.Value;
                worstAt = new Vector2(((int)(pair.Key >> 32) + 0.5f) * UtlCatalog.Cell, ((int)(uint)(pair.Key & 0xffffffffL) + 0.5f) * UtlCatalog.Cell);
            }
        n.smogAvg = weight > 0f ? sum / weight : 0f;
        n.smogWorst = worst;
        n.worstAt = worstAt;
    }
}
