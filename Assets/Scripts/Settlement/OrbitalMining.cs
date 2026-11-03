using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>A mine in orbit, over an asteroid belt or a gas giant, sending what it gathers down to one of your worlds.</summary>
[Serializable]
public class OrbitalMineState
{
    public Vector2Int system;
    public int body;
    [Tooltip("The world in that system it sends its haul to.")]
    public int world;
}

/// <summary>
/// Astro-mining: belts and gas giants can't be settled, but they can be mined from orbit once a
/// world in the same system has a Starport. A belt gives starmetal, iron and stone; a gas giant
/// gives power cells and mana. Each Astro-mining level makes every mine yield more.
/// </summary>
public partial class Settlement
{
    public List<ResourceAmount> OrbitalMineCost()
    {
        var cost = new List<ResourceAmount>();
        void Add(string id, int amount) { var r = Database.GetResource(id); if (r != null) cost.Add(new ResourceAmount { resource = r, amount = amount }); }
        Add("Steel", 1200); Add("Gears", 300); Add("PowerCells", 200);
        return cost;
    }

    public bool HasOrbitalMine(Planet p) => State.orbitalMines.Exists(m => m.system == p.system.key && m.body == p.index);

    /// <summary>Can you put a mine over this body (from the world you're on)?</summary>
    public bool CanMine(Planet p, out string why)
    {
        why = null;
        if (p.kind != BodyKind.AsteroidBelt && p.kind != BodyKind.GasGiant) { why = "Only belts and gas giants are mined from orbit."; return false; }
        if (HasOrbitalMine(p)) { why = "Already mined."; return false; }
        if (CurrentWorld.system != p.system.key) { why = "Go to one of your worlds in this system."; return false; }
        if (!HasStarport) { why = "This world needs a Starport."; return false; }
        if (!State.researched.Contains("AstroMining")) { why = "Research Astro-mining."; return false; }
        if (!CanAfford(OrbitalMineCost())) { why = "Not enough resources."; return false; }
        return true;
    }

    public void BuildOrbitalMine(Planet p)
    {
        if (!CanMine(p, out string why)) { Notify(why); return; }
        Spend(OrbitalMineCost());
        State.orbitalMines.Add(new OrbitalMineState { system = p.system.key, body = p.index, world = State.currentWorld });
        GameAudio.UI("orbital_mine");
        Notify($"An orbital mine opens over {p.name}. Its haul comes down to {CurrentWorld.name}.");
    }

    private float miningTimer;

    /// <summary>Every few seconds each mine sends its haul down.</summary>
    private void TickOrbitalMines(float dt)
    {
        if (State.orbitalMines.Count == 0) return;
        miningTimer += dt;
        if (miningTimer < 5f) return;
        float step = miningTimer;
        miningTimer = 0f;
        float yield = 1f + 0.25f * Mathf.Max(0, Levels("AstroMining") - 1);
        EnsureGalaxy();
        foreach (var m in State.orbitalMines)
        {
            if (m.world < 0 || m.world >= State.worlds.Count) continue;
            var system = Galaxy.Get(m.system);
            if (m.body < 0 || m.body >= system.bodies.Count) continue;
            var w = State.worlds[m.world];
            void Give(string id, float perSecond)
            {
                var stock = w.resources.Find(r => r.id == id);
                if (stock == null) { stock = new ResourceStock { id = id }; w.resources.Add(stock); }
                stock.amount += perSecond * step * yield;
            }
            if (system.bodies[m.body].kind == BodyKind.AsteroidBelt) { Give("Starmetal", 0.25f); Give("Iron", 1.2f); Give("Stone", 2f); }
            else { Give("PowerCells", 0.8f); Give("Mana", 0.3f); }
        }
    }
}
