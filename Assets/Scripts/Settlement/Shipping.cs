using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>A ship in flight between the stars.</summary>
[Serializable]
public class ShipState
{
    public int id;
    [Tooltip("Probe, Colony, Freighter or Troopship.")]
    public string kind;
    public int fromWorld;
    public Vector2Int toSystem;
    public int toPlanet;
    [Tooltip("Play time when it set out, and when it arrives.")]
    public float departed, arrives;
    public List<ResourceStock> cargo = new List<ResourceStock>();
    public float settlers;
    [Tooltip("Troopships: the troops aboard, by kind, and the hero aboard (0 = none).")]
    public List<GarrisonStock> troops = new List<GarrisonStock>();
    public int heroId;
}

/// <summary>
/// Ships from your Starports: probes that scan star systems, and colony ships that carry settlers
/// and supplies to a new world. Flight time comes from the distance and your Star Drives; how far
/// they can go, from Navigation.
/// </summary>
public partial class Settlement
{
    public const string Probe = "Probe", ColonyShip = "Colony";

    [Serializable]
    public struct ShipSpec { public string kind, name; public List<ResourceAmount> cost; public int settlers; }

    /// <summary>Does the world you're looking at have a working Starport?</summary>
    public bool HasStarport => State.buildings.Exists(b => b.IsBuilt && b.health > 0 && Database.GetBuilding(b.definitionId)?.launchesShips == true);

    /// <summary>
    /// A colony ship carries a Starport kit, so a new colony isn't cut off until it can make Starmetal and
    /// Astral Cores itself: on any world but the home world, the first ship-launching building costs nothing.
    /// </summary>
    public bool HasStarportKit => State.currentWorld != 0 && !CurrentWorld.starportKitUsed
        && !State.buildings.Exists(b => Database.GetBuilding(b.definitionId)?.launchesShips == true);

    private static readonly List<ResourceAmount> NoCost = new List<ResourceAmount>();

    /// <summary>What placing this building costs here (the Starport kit makes a colony's first one free).</summary>
    public List<ResourceAmount> CostOf(BuildingDefinition def) =>
        def.launchesShips && HasStarportKit ? NoCost : RiverCostOf(def);

    /// <summary>Once a colony's kit Starport is finished, the kit is spent.</summary>
    private void UseStarportKit()
    {
        if (!CurrentWorld.starportKitUsed && State.currentWorld != 0 && HasStarport) CurrentWorld.starportKitUsed = true;
    }

    /// <summary>What a ship costs to build (from the world it launches from).</summary>
    public List<ResourceAmount> ShipCost(string kind)
    {
        var cost = new List<ResourceAmount>();
        void Add(string id, int amount) { var r = Database.GetResource(id); if (r != null) cost.Add(new ResourceAmount { resource = r, amount = amount }); }
        if (kind == Probe) { Add("Steel", 150); Add("PowerCells", 40); Add("Microchips", 30); }
        else { Add("Steel", 800); Add("Wood", 1200); Add("Gears", 200); Add("PowerCells", 150); }
        return cost;
    }

    /// <summary>Supplies a colony ship carries to found the new world, taken from the world it leaves.</summary>
    private static readonly (string id, int amount)[] ColonySupplies =
    {
        ("Food", 1500), ("Wood", 1200), ("Stone", 1200), ("Iron", 300), ("Steel", 200), ("Gold", 100), ("Coin", 300),
    };
    public const int ColonySettlers = 25;

    /// <summary>Can a ship of this kind go to this planet from the world you're on? 'why' says what's missing.</summary>
    public bool CanSend(string kind, Planet target, out string why)
    {
        why = null;
        if (target == null) { why = "Pick a world."; return false; }
        if (!HasStarport) { why = "This world needs a Starport."; return false; }
        if (!InReach(target.system)) { why = "Out of your ships' reach (research Navigation)."; return false; }
        if (kind == Probe && IsScanned(target.system)) { why = "Already scanned."; return false; }
        if (kind == ColonyShip)
        {
            if (!target.CanLand) { why = "Nowhere to land."; return false; }
            int existing = WorldIndexOf(target);
            if (existing >= 0) { why = "You already have this world."; return false; }
            if (State.ships.Exists(s => s.kind == ColonyShip && s.toSystem == target.system.key && s.toPlanet == target.index)) { why = "A colony ship is already on its way."; return false; }
            if (Population < ColonySettlers + 10) { why = $"Needs {ColonySettlers} settlers to spare."; return false; }
        }
        if (!CanAfford(ShipCost(kind))) { why = "Not enough resources for the ship."; return false; }
        return true;
    }

    /// <summary>
    /// How long a ship from the world you're on takes to reach 'target': by drive and distance, or
    /// a few seconds if both ends have a Star Gate.
    /// </summary>
    public float TripSeconds(Planet target)
    {
        var there = State.worlds.Find(w => w.system == target.system.key && w.planet == target.index);
        if (GateLinked(CurrentWorld, there)) return GateTripSeconds;
        return Galaxy.TravelSeconds(CurrentPlanet ?? Galaxy.HomePlanet, target, ShipSpeed);
    }

    /// <summary>Launch a ship from the world you're on.</summary>
    public bool Launch(string kind, Planet target)
    {
        if (!CanSend(kind, target, out string why)) { Notify(why); return false; }
        Spend(ShipCost(kind));
        var ship = new ShipState
        {
            id = State.nextShipId++,
            kind = kind,
            fromWorld = State.currentWorld,
            toSystem = target.system.key,
            toPlanet = target.index,
            departed = State.playTime,
            arrives = State.playTime + TripSeconds(target),
        };
        if (kind == ColonyShip)
        {
            foreach (var (id, amount) in ColonySupplies)
            {
                float take = Mathf.Min(amount, GetAmount(Database.GetResource(id)));
                if (take <= 0) continue;
                AddResource(Database.GetResource(id), -take);
                ship.cargo.Add(new ResourceStock { id = id, amount = take });
            }
            ship.settlers = ColonySettlers;
            State.population -= ColonySettlers;
        }
        State.ships.Add(ship);
        if (!offWorld) GameAudio.UI(kind == Probe ? "probe_launch" : "rocket_launch");
        Notify($"{(kind == Probe ? "A probe" : "A colony ship")} sets out for {(IsScanned(target.system) ? target.name : target.system.name)}, arriving in {StarMapPanel.FormatTime(ship.arrives - State.playTime)}.");
        return true;
    }

    private void TickShips()
    {
        for (int i = State.ships.Count - 1; i >= 0; i--)
        {
            var ship = State.ships[i];
            if (State.playTime < ship.arrives) continue;
            State.ships.RemoveAt(i);
            Arrive(ship);
        }
    }

    private void Arrive(ShipState ship)
    {
        EnsureGalaxy();
        var system = Galaxy.Get(ship.toSystem);
        var planet = ship.toPlanet >= 0 && ship.toPlanet < system.bodies.Count ? system.bodies[ship.toPlanet] : null;
        if (ship.kind == Probe)
        {
            MarkScanned(system);
            int lands = system.bodies.FindAll(b => b.CanLand).Count;
            if (!offWorld) GameAudio.UI("scan_ping");
            Notify($"The probe reached {system.name}: {system.bodies.Count} bodies, {lands} worlds you could land on. (See the star map.)");
            return;
        }
        if (ship.kind == Freighter && planet != null)
        {
            int to = WorldIndexOf(planet);
            if (to >= 0) Unload(ship, State.worlds[to]);
            if (!offWorld && to == State.currentWorld) GameAudio.UI("cargo_drop");
            return;
        }
        if (ship.kind == Troopship)
        {
            ArriveTroops(ship, planet);
            return;
        }
        if (ship.kind == ColonyShip && planet != null)
        {
            MarkScanned(system);
            int index = WorldIndexOf(planet);
            if (index < 0)
            {
                index = AddWorld(new WorldState
                {
                    name = planet.name,
                    profile = planet.profile,
                    seed = planet.Seed,
                    system = system.key,
                    planet = planet.index,
                    population = 0,
                    secondsUntilWave = Database.firstWaveDelay,
                });
            }
            var world = State.worlds[index];
            foreach (var c in ship.cargo)
            {
                var stock = world.resources.Find(r => r.id == c.id);
                if (stock == null) world.resources.Add(new ResourceStock { id = c.id, amount = c.amount });
                else stock.amount += c.amount;
            }
            world.population += ship.settlers;
            if (!offWorld) GameAudio.UI("ship_landing");
            Notify($"The colony ship has landed on {planet.name}! Go there (star map or the world list) and choose where to build the colony. It brought a Starport kit: the colony's first Starport is free.");
        }
    }
}
