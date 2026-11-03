using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>A standing order: freighters carry this much of a resource from one world's Starport to another's, again and again.</summary>
[Serializable]
public class CargoRoute
{
    public int id;
    public int fromWorld, toWorld;
    public string resourceId;
    [Tooltip("How much each trip carries.")]
    public int amount = 200;
    public bool paused;
    [Tooltip("Play time when the next freighter leaves.")]
    public float nextDeparture;
}

/// <summary>
/// Trade between your worlds: cargo routes send freighters loaded from one world's stock to
/// another's. A freighter leaves when the last one is home again (a round trip), so faster drives
/// mean more trips. Each trip burns a little Power Cells at the world it leaves.
/// </summary>
public partial class Settlement
{
    public const string Freighter = "Freighter";
    public const int RouteStep = 50, RouteMax = 5000;

    private static readonly string FuelId = "PowerCells";
    private const int FuelPerTrip = 10;

    public bool HasStarportOn(WorldState w) =>
        w.buildings.Exists(b => b.IsBuilt && b.health > 0 && Database.GetBuilding(b.definitionId)?.launchesShips == true);

    /// <summary>Routes from one of your worlds to another.</summary>
    public List<CargoRoute> RoutesBetween(int from, int to) => State.routes.FindAll(r => r.fromWorld == from && r.toWorld == to);

    /// <summary>Start a route (it carries the resource there's most of, until you pick another).</summary>
    public CargoRoute AddRoute(int from, int to)
    {
        var w = State.worlds[from];
        ResourceStock most = null;
        foreach (var s in w.resources) if (most == null || s.amount > most.amount) most = s;
        var route = new CargoRoute
        {
            id = State.nextRouteId++, fromWorld = from, toWorld = to,
            resourceId = most != null ? most.id : Database.resources[0].id,
            nextDeparture = State.playTime + 5f,
        };
        State.routes.Add(route);
        return route;
    }

    public void RemoveRoute(CargoRoute route) => State.routes.Remove(route);

    /// <summary>Carry the next (or previous) resource on this route.</summary>
    public void CycleRouteResource(CargoRoute route, int step)
    {
        var list = Database.resources;
        int i = list.FindIndex(r => r != null && r.id == route.resourceId);
        for (int n = 0; n < list.Count; n++)
        {
            i = (i + step + list.Count) % list.Count;
            if (list[i] != null) { route.resourceId = list[i].id; return; }
        }
    }

    public float RoundTripSeconds(CargoRoute route)
    {
        var a = PlanetOf(State.worlds[route.fromWorld]);
        var b = PlanetOf(State.worlds[route.toWorld]);
        return a == null || b == null ? 600f : 2f * Galaxy.TravelSeconds(a, b, ShipSpeed);
    }

    private void TickRoutes()
    {
        foreach (var route in State.routes)
        {
            if (route.paused || State.playTime < route.nextDeparture) continue;
            if (route.fromWorld >= State.worlds.Count || route.toWorld >= State.worlds.Count) continue;
            var from = State.worlds[route.fromWorld];
            route.nextDeparture = State.playTime + RoundTripSeconds(route);
            if (!HasStarportOn(from) || !HasStarportOn(State.worlds[route.toWorld])) continue;
            var stock = from.resources.Find(r => r.id == route.resourceId);
            var fuel = from.resources.Find(r => r.id == FuelId);
            if (stock == null || stock.amount < 1f || fuel == null || fuel.amount < FuelPerTrip) continue;
            float load = Mathf.Min(route.amount, stock.amount);
            stock.amount -= load;
            fuel.amount -= FuelPerTrip;
            var ship = new ShipState
            {
                id = State.nextShipId++, kind = Freighter, fromWorld = route.fromWorld,
                toSystem = State.worlds[route.toWorld].system, toPlanet = State.worlds[route.toWorld].planet,
                departed = State.playTime, arrives = State.playTime + RoundTripSeconds(route) * 0.5f,
            };
            ship.cargo.Add(new ResourceStock { id = route.resourceId, amount = load });
            State.ships.Add(ship);
        }
    }

    /// <summary>A freighter unloads at the world it was sent to.</summary>
    private void Unload(ShipState ship, WorldState world)
    {
        foreach (var c in ship.cargo)
        {
            var stock = world.resources.Find(r => r.id == c.id);
            if (stock == null) world.resources.Add(new ResourceStock { id = c.id, amount = c.amount });
            else stock.amount += c.amount;
        }
    }
}
