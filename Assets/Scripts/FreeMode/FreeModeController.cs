using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Runs the free mode scene: starts the settlement, keeps building views in sync with it,
/// handles selection, and fights waves for real while the player is watching.
/// Raiders roam in from a random direction, smash and loot buildings, and run off with
/// what they can carry; troops defend the whole town.
/// </summary>
public class FreeModeController : MonoBehaviour, ILiveBattlefield, IRaidTargets
{
    [Header("Scene references")]
    public SettlementGrid grid;

    [Header("Tuning")]
    public float rallySpread = 1.5f;
    public float troopTrainSeconds = 6f;
    public float secondsBetweenWaveSpawns = 0.6f;

    [Tooltip("Units are drawn at this size in free mode so they suit the building scale.")]
    public float unitScale = 0.6f;

    [Tooltip("Defending troops hold the settlement plus this much ground around it.")]
    public float townGuardMargin = 3f;

    public Settlement Settlement { get; private set; }
    public SettlementGrid Grid => grid;
    public BuildingPlacer Placer { get; private set; }
    public BuildingView Selected { get; private set; }
    public IEnumerable<BuildingView> Buildings => views.Values;
    public bool WaveInProgress => waveInProgress;

    /// <summary>For the HUD: selection changed, or a message to show.</summary>
    public event Action<BuildingView> SelectionChanged;
    public event Action<string> MessageShown;

    private readonly Dictionary<int, BuildingView> views = new Dictionary<int, BuildingView>();

    // Live wave tracking
    private readonly List<Enemy> liveEnemies = new List<Enemy>();
    private readonly List<UnitDefinition> unspawned = new List<UnitDefinition>();
    private readonly List<ResourceAmount> stolenThisWave = new List<ResourceAmount>();
    private bool waveInProgress;
    private Vector3 raidOrigin;

    private void Awake()
    {
        Settlement = Settlement.EnsureStarted();
        if (Settlement == null)
        {
            enabled = false;
            return;
        }

        EnsureEventSystem();
        grid.Build(Settlement.Database.gridSize);
        WorldDecorator.Decorate(transform, grid, null, null);
        if (Camera.main != null)
        {
            var cameraController = Camera.main.GetComponent<CameraController>();
            if (cameraController == null)
            {
                cameraController = Camera.main.gameObject.AddComponent<CameraController>();
            }
            // Keep the view over the map.
            cameraController.bounds = grid.MapRect;
        }
        Placer = new GameObject("BuildingPlacer").AddComponent<BuildingPlacer>();
        Placer.transform.SetParent(transform, false);
        Placer.Init(this);

        var life = new GameObject("AmbientLife").AddComponent<AmbientLife>();
        life.transform.SetParent(transform, false);
        life.Init(this);

        Settlement.Changed += SyncViews;
        Settlement.Message += ShowMessage;
        Settlement.Waves.LiveBattlefield = this;
        SyncViews();
    }

    private void OnDestroy()
    {
        if (Settlement == null)
        {
            return;
        }
        Settlement.Changed -= SyncViews;
        Settlement.Message -= ShowMessage;
        if (ReferenceEquals(Settlement.Waves.LiveBattlefield, this))
        {
            Settlement.Waves.LiveBattlefield = null;
        }
        ResolveUnfinishedWave();
    }

    private void Update()
    {
        if (!Placer.IsPlacing && Input.GetMouseButtonDown(0) && !BuildingPlacer.IsPointerOverUI())
        {
            Vector2Int cell = grid.WorldToCell(SettlementGrid.MouseWorld());
            Select(BuildingAt(cell));
        }
        if (Input.GetKeyDown(KeyCode.Escape) && !Placer.IsPlacing)
        {
            Select(null);
        }

        TrackLiveWave();
    }

    // ---------- Buildings ----------

    private void SyncViews()
    {
        var alive = new HashSet<int>();
        foreach (var state in Settlement.State.buildings)
        {
            alive.Add(state.instanceId);
            if (views.ContainsKey(state.instanceId))
            {
                continue;
            }
            var def = Settlement.Database.GetBuilding(state.definitionId);
            if (def == null)
            {
                Debug.LogWarning($"Unknown building '{state.definitionId}' in save; skipping.");
                continue;
            }
            var view = new GameObject().AddComponent<BuildingView>();
            view.transform.SetParent(grid.transform, true);
            view.Init(this, state, def);
            views.Add(state.instanceId, view);
        }

        var removed = new List<int>();
        foreach (var pair in views)
        {
            if (!alive.Contains(pair.Key))
            {
                removed.Add(pair.Key);
            }
        }
        foreach (int id in removed)
        {
            if (Selected == views[id])
            {
                Select(null);
            }
            Destroy(views[id].gameObject);
            views.Remove(id);
        }
    }

    public BuildingView BuildingAt(Vector2Int cell)
    {
        foreach (var view in views.Values)
        {
            if (view.Contains(cell))
            {
                return view;
            }
        }
        return null;
    }

    public void Select(BuildingView view)
    {
        if (Selected == view)
        {
            return;
        }
        if (Selected != null)
        {
            Selected.SetSelected(false);
        }
        Selected = view;
        if (Selected != null)
        {
            Selected.SetSelected(true);
        }
        SelectionChanged?.Invoke(Selected);
    }

    /// <summary>Troops idle around the building that trained them.</summary>
    public Vector3 GetRallyPoint(BuildingView home)
    {
        Vector3 center = home != null
            ? grid.AreaCenter(home.State.cell, home.Definition.size) - new Vector3(0, home.Definition.size.y * 0.5f + 0.8f, 0)
            : SettlementCenter;
        Vector2 offset = UnityEngine.Random.insideUnitCircle * rallySpread;
        return center + new Vector3(offset.x, offset.y, 0);
    }

    public void ShowMessage(string message) => MessageShown?.Invoke(message);

    /// <summary>Spawn a troop or enemy prefab at free mode scale.</summary>
    public GameObject SpawnUnit(GameObject prefab, Vector3 position)
    {
        var go = Instantiate(prefab, position, Quaternion.identity);
        go.transform.localScale *= unitScale;
        // Smaller bodies, shorter reach (otherwise they'd hit from a distance).
        var unit = go.GetComponent<Unit>();
        if (unit != null)
        {
            unit.attackRange *= unitScale;
        }
        // Troops go for the nearest raider; their building sets their stance.
        if (unit is Troop troop)
        {
            troop.targetPriority = Troop.TargetPriority.Closest;
            troop.SetStance(TroopStance.Defend, TownZone, SightRange);
        }
        return go;
    }

    private Vector3 SettlementCenter => grid.AreaCenter(Vector2Int.zero, grid.Size);

    /// <summary>The ground Defend troops hold: the settlement plus a margin.</summary>
    public Rect TownZone
    {
        get
        {
            Vector2 min = grid.CellToWorld(Vector2Int.zero);
            Vector2 size = new Vector2(grid.Size.x, grid.Size.y) * grid.cellSize;
            return new Rect(min - Vector2.one * townGuardMargin, size + Vector2.one * townGuardMargin * 2);
        }
    }

    /// <summary>Troops can spot raiders anywhere on the map.</summary>
    public float SightRange => grid.MapRect.size.magnitude;

    /// <summary>Set the stance of every troop-training building at once.</summary>
    public void SetAllStances(TroopStance stance)
    {
        foreach (var view in views.Values)
        {
            if (view.Definition.trainsUnit != null)
            {
                view.State.stance = stance;
            }
        }
        ShowMessage(stance == TroopStance.Defend ? "All troops: defend the town." : "All troops: advance and attack!");
    }

    // ---------- Live waves ----------

    public void SpawnWave(Wave wave)
    {
        // Raiders enter together from a random point on the map edge.
        raidOrigin = grid.RandomEdgePoint();

        unspawned.AddRange(wave.units);
        waveInProgress = true;
        string from = CompassName(raidOrigin - SettlementCenter);
        ShowMessage(wave.units.Count == 1
            ? $"A raider is approaching from the {from}!"
            : $"{wave.units.Count} raiders are approaching from the {from}!");
        StartCoroutine(SpawnRoutine());
    }

    private IEnumerator SpawnRoutine()
    {
        while (unspawned.Count > 0)
        {
            var unit = unspawned[0];
            unspawned.RemoveAt(0);
            if (unit.prefab != null)
            {
                Vector2 jitter = UnityEngine.Random.insideUnitCircle * 2f;
                Rect map = grid.MapRect;
                Vector3 spawn = raidOrigin + new Vector3(jitter.x, jitter.y, 0);
                spawn.x = Mathf.Clamp(spawn.x, map.xMin, map.xMax);
                spawn.y = Mathf.Clamp(spawn.y, map.yMin, map.yMax);
                var enemy = SpawnUnit(unit.prefab, spawn).GetComponent<Enemy>();
                if (enemy != null)
                {
                    enemy.StartRaid(this);
                    liveEnemies.Add(enemy);
                }
            }
            yield return new WaitForSeconds(secondsBetweenWaveSpawns);
        }
    }

    private void TrackLiveWave()
    {
        if (!waveInProgress)
        {
            return;
        }
        liveEnemies.RemoveAll(e => e == null || !e.IsAlive);
        if (liveEnemies.Count == 0 && unspawned.Count == 0)
        {
            waveInProgress = false;
            Settlement.State.wavesSurvived++;
            string loss = Settlement.Describe(stolenThisWave);
            ShowMessage(loss.Length > 0 ? $"The raid is over. Raiders got away with {loss}." : "The raid is over. Nothing was lost!");
            stolenThisWave.Clear();
        }
    }

    /// <summary>Leaving mid-wave: raiders still about are resolved by the numbers.</summary>
    private void ResolveUnfinishedWave()
    {
        if (!waveInProgress)
        {
            return;
        }
        var rest = new Wave();
        foreach (var enemy in liveEnemies)
        {
            if (enemy != null && enemy.IsAlive)
            {
                rest.units.Add(enemy.definition);
                rest.strength += Settlement.UnitPower(enemy.definition);
            }
        }
        foreach (var unit in unspawned)
        {
            rest.units.Add(unit);
            rest.strength += Settlement.UnitPower(unit);
        }
        liveEnemies.Clear();
        unspawned.Clear();
        waveInProgress = false;
        if (rest.units.Count > 0)
        {
            Settlement.Waves.AutoResolve(rest);
        }
    }

    // ---------- IRaidTargets (what raiders attack) ----------

    public BuildingView NearestTarget(Vector3 from)
    {
        BuildingView best = null;
        float bestDistance = float.MaxValue;
        foreach (var view in views.Values)
        {
            if (!IsStanding(view))
            {
                continue;
            }
            float d = Vector2.Distance(from, ClosestPointOn(view, from));
            if (d < bestDistance)
            {
                bestDistance = d;
                best = view;
            }
        }
        return best;
    }

    public bool IsStanding(BuildingView building) =>
        building != null && building.State.IsBuilt && building.State.health > 0;

    public Vector3 ClosestPointOn(BuildingView building, Vector3 from)
    {
        Vector3 min = grid.CellToWorld(building.State.cell);
        Vector3 max = min + new Vector3(building.Definition.size.x, building.Definition.size.y, 0) * grid.cellSize;
        return new Vector3(Mathf.Clamp(from.x, min.x, max.x), Mathf.Clamp(from.y, min.y, max.y), 0);
    }

    public void HitBuilding(BuildingView building, Enemy raider)
    {
        Settlement.DamageBuilding(building.State, raider.attackPower);
        building.Shake();
        var stolen = Settlement.Steal(raider.lootPerHit);
        raider.Carry(stolen);
    }

    public Vector3 ExitPointFor(Vector3 from) => grid.NearestEdgePoint(from);

    public void RaiderEscaped(Enemy raider)
    {
        liveEnemies.Remove(raider);
        Merge(stolenThisWave, raider.Loot);
    }

    public void RaiderKilled(Enemy raider)
    {
        if (raider.LootCarried > 0)
        {
            Settlement.ReturnResources(raider.Loot);
            ShowMessage($"Recovered {Settlement.Describe(raider.Loot)} from a fallen raider.");
        }
    }

    // ---------- Helpers ----------

    private static void Merge(List<ResourceAmount> into, List<ResourceAmount> add)
    {
        foreach (var item in add)
        {
            int i = into.FindIndex(x => x.resource == item.resource);
            if (i >= 0)
            {
                var x = into[i];
                x.amount += item.amount;
                into[i] = x;
            }
            else
            {
                into.Add(item);
            }
        }
    }

    private static string CompassName(Vector3 dir)
    {
        string[] names = { "east", "north-east", "north", "north-west", "west", "south-west", "south", "south-east" };
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        int index = Mathf.RoundToInt(((angle % 360) + 360) % 360 / 45f) % 8;
        return names[index];
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current == null && FindAnyObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }
    }
}
