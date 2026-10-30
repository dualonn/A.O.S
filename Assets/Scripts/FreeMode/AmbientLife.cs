using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Keeps a handful of cosmetic villagers living in the settlement's houses.
/// The number follows the population (one villager per 'peoplePerVillager' people),
/// capped so busy towns stay readable. Purely visual.
/// </summary>
public class AmbientLife : MonoBehaviour
{
    [Tooltip("One visible villager for every this many people.")]
    public float peoplePerVillager = 2f;

    public int maxVillagers = 40;
    public int maxPerHouse = 3;

    private FreeModeController controller;
    private ArtLibrary art;
    private readonly List<Villager> villagers = new List<Villager>();
    private float timer;

    public SettlementGrid Grid => controller.Grid;
    public bool UnderAttack => controller.WaveInProgress;

    public void Init(FreeModeController owner)
    {
        controller = owner;
        art = ArtLibrary.Load();
    }

    private void Update()
    {
        if (art == null || art.villagerIdle == null)
        {
            return;
        }
        timer -= Time.deltaTime;
        if (timer > 0)
        {
            return;
        }
        // Stagger arrivals and departures so villagers trickle in and out.
        timer = 0.6f;
        Reconcile();
    }

    private void Reconcile()
    {
        villagers.RemoveAll(v => v == null);
        var homes = controller.Buildings
            .Where(b => b.Definition.housing > 0 && controller.Settlement.IsWorking(b.State, b.Definition))
            .ToList();

        int desired = homes.Count == 0 ? 0 : Mathf.Min(
            Mathf.CeilToInt(controller.Settlement.Population / peoplePerVillager),
            maxVillagers,
            homes.Count * maxPerHouse);

        // Villagers whose home stopped working move out.
        foreach (var v in villagers)
        {
            if (!v.Leaving && !homes.Contains(v.Home))
            {
                v.Leave();
            }
        }

        var staying = villagers.Where(v => !v.Leaving).ToList();
        if (staying.Count < desired)
        {
            // Move into the emptiest house.
            var home = homes.OrderBy(h => staying.Count(v => v.Home == h)).First();
            var villager = new GameObject().AddComponent<Villager>();
            villager.transform.SetParent(transform, false);
            villager.Init(this, home, art);
            villagers.Add(villager);
        }
        else if (staying.Count > desired)
        {
            staying[staying.Count - 1].Leave();
        }
    }

    /// <summary>Just in front of the middle of a building's bottom edge.</summary>
    public Vector3 DoorOf(BuildingView building)
    {
        Vector3 center = Grid.AreaCenter(building.State.cell, building.Definition.size);
        Vector3 bottom = Grid.CellToWorld(building.State.cell);
        // Slightly below the base so villagers stand in front of the building.
        return new Vector3(center.x, bottom.y - 0.08f, 0);
    }

    public BuildingView RandomWorkplace()
    {
        var workplaces = controller.Buildings
            .Where(b => b.Definition.production.Count > 0 && controller.Settlement.IsWorking(b.State, b.Definition))
            .ToList();
        return workplaces.Count == 0 ? null : workplaces[Random.Range(0, workplaces.Count)];
    }

    /// <summary>The icon of what a workplace makes, for villagers to carry home.</summary>
    public Sprite ProductOf(BuildingView building)
    {
        foreach (var rate in building.Definition.production)
        {
            if (rate.resource != null && rate.resource.icon != null)
            {
                return rate.resource.icon;
            }
        }
        return null;
    }

    /// <summary>A random spot near a building, kept inside the settlement.</summary>
    public Vector3 RandomSpotNear(BuildingView building)
    {
        Vector3 door = DoorOf(building);
        Vector3 min = (Vector3)Grid.MapRect.min + Vector3.one * 0.3f;
        Vector3 max = (Vector3)Grid.MapRect.max - Vector3.one * 0.3f;
        for (int attempt = 0; attempt < 8; attempt++)
        {
            Vector2 offset = Random.insideUnitCircle * 4f;
            Vector3 spot = door + new Vector3(offset.x, offset.y - 0.5f, 0);
            spot.x = Mathf.Clamp(spot.x, min.x, max.x);
            spot.y = Mathf.Clamp(spot.y, min.y, max.y);
            spot.z = 0;
            if (MapNavigator.Current == null || MapNavigator.Current.IsWalkable(spot))
            {
                return spot;
            }
        }
        return door;
    }
}
