using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Troops of one kind stationed on a world, brought there by troopship.</summary>
[Serializable]
public class GarrisonStock
{
    public string unitId;
    public int count;
}

/// <summary>
/// Troopships: carry troops (or a hero) from the world you're on to another of yours. Troops
/// that land join that world's garrison: they stand by its town centre, defend it, and count
/// towards its defence while you're away. A Star Gate at both ends makes the trip a few seconds.
/// </summary>
public partial class Settlement
{
    public const string Troopship = "Troopship";

    /// <summary>The most troops one world can have stationed from elsewhere.</summary>
    public const int GarrisonCap = 80;

    /// <summary>Food for the crossing, per troop.</summary>
    public const int TroopFare = 12;

    public int GarrisonSize(WorldState w)
    {
        int n = 0;
        if (w?.garrison != null) foreach (var g in w.garrison) n += g.count;
        return n;
    }

    /// <summary>Troops already on their way to this world.</summary>
    private int TroopsOnTheWay(WorldState w)
    {
        int n = 0;
        foreach (var s in State.ships)
            if (s.kind == Troopship && s.toSystem == w.system && s.toPlanet == w.planet && s.troops != null)
                foreach (var g in s.troops) n += g.count;
        return n;
    }

    public List<ResourceAmount> TroopshipCost(int troops)
    {
        return Costs(("Steel", 200), ("PowerCells", 50), ("Food", TroopFare * Mathf.Max(1, troops)));
    }

    /// <summary>Can a troopship take this many troops (or this hero) from here to world 'to'? 'why' says what's missing.</summary>
    public bool CanSendTroops(int to, int count, HeroState hero, out string why)
    {
        why = null;
        if (to < 0 || to >= State.worlds.Count || to == State.currentWorld) { why = "Pick another of your worlds."; return false; }
        var w = State.worlds[to];
        if (!IsWorldFounded(w)) { why = $"Found the colony on {w.name} first."; return false; }
        if (!HasStarport) { why = "This world needs a Starport."; return false; }
        var planet = PlanetOf(w);
        if (planet == null || !InReach(planet.system)) { why = "Out of your ships' reach (research Navigation)."; return false; }
        if (hero == null && count <= 0) { why = "No troops here to send."; return false; }
        if (hero != null && (IsDown(hero) || hero.world != State.currentWorld)) { why = $"{hero.name} can't board now."; return false; }
        if (hero == null && GarrisonSize(w) + TroopsOnTheWay(w) + count > GarrisonCap)
        {
            why = $"{w.name} can hold {GarrisonCap} troops from other worlds ({GarrisonSize(w) + TroopsOnTheWay(w)} there or on the way).";
            return false;
        }
        if (!CanAfford(TroopshipCost(count))) { why = "Not enough for the troopship (steel, power cells, food for the crossing)."; return false; }
        return true;
    }

    /// <summary>How many more troops world 'to' can take.</summary>
    public int GarrisonRoom(int to)
    {
        if (to < 0 || to >= State.worlds.Count) return 0;
        var w = State.worlds[to];
        return Mathf.Max(0, GarrisonCap - GarrisonSize(w) - TroopsOnTheWay(w));
    }

    /// <summary>Launch a troopship with these troops (already taken off the field) and/or a hero.</summary>
    public bool LaunchTroopship(int to, List<GarrisonStock> troops, HeroState hero)
    {
        int count = 0;
        foreach (var g in troops) count += g.count;
        if (!CanSendTroops(to, count, hero, out string why)) { Notify(why); return false; }
        Spend(TroopshipCost(count));
        var w = State.worlds[to];
        var ship = new ShipState
        {
            id = State.nextShipId++,
            kind = Troopship,
            fromWorld = State.currentWorld,
            toSystem = w.system,
            toPlanet = w.planet,
            departed = State.playTime,
            arrives = State.playTime + TripSeconds(PlanetOf(w)),
            heroId = hero != null ? hero.id : 0,
        };
        ship.troops.AddRange(troops);
        if (hero != null) hero.world = -1;   // aboard
        State.ships.Add(ship);
        string who = hero != null && count > 0 ? $"{hero.name} and {count} troops" : hero != null ? hero.name : $"{count} troops";
        Notify($"A troopship carries {who} to {w.name}, arriving in {StarMapPanel.FormatTime(ship.arrives - State.playTime)}.");
        RaiseChanged();
        return true;
    }

    /// <summary>A troopship lands: its troops join the garrison, its hero moves in.</summary>
    private void ArriveTroops(ShipState ship, Planet planet)
    {
        int to = WorldIndexOf(planet);
        if (to < 0) to = Mathf.Clamp(ship.fromWorld, 0, State.worlds.Count - 1);   // nowhere to land: back home
        var w = State.worlds[to];
        int count = 0;
        foreach (var g in ship.troops)
        {
            if (g == null || g.count <= 0) continue;
            var stock = w.garrison.Find(x => x.unitId == g.unitId);
            if (stock == null) w.garrison.Add(new GarrisonStock { unitId = g.unitId, count = g.count });
            else stock.count += g.count;
            count += g.count;
        }
        var hero = ship.heroId != 0 ? GetHero(ship.heroId) : null;
        if (hero != null) hero.world = to;
        string who = hero != null && count > 0 ? $"{hero.name} and {count} troops" : hero != null ? hero.name : $"{count} troops";
        Notify($"The troopship has landed on {w.name}: {who} {(hero != null && count == 0 ? "is" : "are")} there now."
            + (hero != null ? " (Heroes live at a troop building there.)" : ""));
    }

    /// <summary>A stationed troop on the world you're on died.</summary>
    public void GarrisonLost(string unitId)
    {
        var stock = CurrentWorld.garrison.Find(g => g.unitId == unitId);
        if (stock == null) return;
        stock.count--;
        if (stock.count <= 0) CurrentWorld.garrison.Remove(stock);
    }

    /// <summary>How strong a world's garrison is, for raids resolved while you're elsewhere.</summary>
    private float GarrisonPower()
    {
        float total = 0;
        foreach (var g in State.World.garrison)
        {
            var unit = FindUnit(g.unitId);
            if (unit != null) total += g.count * UnitPower(unit, GetUnitModifier(unit));
        }
        return total;
    }

    /// <summary>Raiders broke through while you were away: some of the garrison fell.</summary>
    private void GarrisonCasualties(float severity)
    {
        float share = Mathf.Clamp01(severity) * 0.4f;
        int lost = 0;
        foreach (var g in State.World.garrison)
        {
            int n = Mathf.FloorToInt(g.count * share);
            g.count -= n;
            lost += n;
        }
        State.World.garrison.RemoveAll(g => g.count <= 0);
        if (lost > 0) Notify($"{lost} of the troops stationed on {State.World.name} fell.");
    }

    /// <summary>Is this hero on the world you're looking at?</summary>
    public bool HeroHere(HeroState hero) => hero != null && hero.world == State.currentWorld;
}
