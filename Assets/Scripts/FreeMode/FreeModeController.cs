using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Runs the free mode scene: starts the settlement, keeps building views in sync with it,
/// handles selection, and fights waves for real while the player is watching.
/// </summary>
public class FreeModeController : MonoBehaviour, ILiveBattlefield
{
    [Header("Scene references")]
    public SettlementGrid grid;

    [Tooltip("The path enemies walk. Reaching its end means breaking into the settlement.")]
    public WayPoints enemyPath;

    [Tooltip("Where trained troops gather to defend.")]
    public Transform rallyPoint;

    [Header("Tuning")]
    public float rallySpread = 1.5f;
    public float troopTrainSeconds = 6f;
    public float secondsBetweenWaveSpawns = 1f;

    public Settlement Settlement { get; private set; }
    public SettlementGrid Grid => grid;
    public BuildingPlacer Placer { get; private set; }
    public BuildingView Selected { get; private set; }

    /// <summary>For the HUD: selection changed, or a message to show.</summary>
    public event Action<BuildingView> SelectionChanged;
    public event Action<string> MessageShown;

    private readonly Dictionary<int, BuildingView> views = new Dictionary<int, BuildingView>();

    // Live wave tracking
    private readonly List<Enemy> liveEnemies = new List<Enemy>();
    private readonly List<UnitDefinition> unspawned = new List<UnitDefinition>();
    private float liveWaveStrength;
    private bool waveInProgress;

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
        Placer = new GameObject("BuildingPlacer").AddComponent<BuildingPlacer>();
        Placer.transform.SetParent(transform, false);
        Placer.Init(this);

        Settlement.Changed += SyncViews;
        Settlement.Message += ShowMessage;
        Settlement.Waves.LiveBattlefield = this;
        Enemy.ReachedEnd += OnEnemyReachedEnd;
        SyncViews();
    }

    private void OnDestroy()
    {
        Enemy.ReachedEnd -= OnEnemyReachedEnd;
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

    public Vector3 GetRallyPoint()
    {
        Vector3 center = rallyPoint != null ? rallyPoint.position : grid.transform.position;
        Vector2 offset = UnityEngine.Random.insideUnitCircle * rallySpread;
        return center + new Vector3(offset.x, offset.y, 0);
    }

    public void ShowMessage(string message) => MessageShown?.Invoke(message);

    // ---------- Live waves ----------

    public void SpawnWave(Wave wave)
    {
        if (enemyPath == null || enemyPath.positions.Count == 0)
        {
            // Nowhere to fight it: fall back to the numbers.
            Settlement.Waves.AutoResolve(wave);
            return;
        }
        liveWaveStrength += wave.strength;
        unspawned.AddRange(wave.units);
        waveInProgress = true;
        ShowMessage(wave.units.Count == 1 ? "An enemy is coming!" : $"A wave of {wave.units.Count} enemies is coming!");
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
                var enemy = Instantiate(unit.prefab, enemyPath.positions[0], Quaternion.identity).GetComponent<Enemy>();
                if (enemy != null)
                {
                    enemy.path = enemyPath;
                    liveEnemies.Add(enemy);
                }
            }
            yield return new WaitForSeconds(secondsBetweenWaveSpawns);
        }
    }

    private void OnEnemyReachedEnd(Enemy enemy)
    {
        if (!liveEnemies.Remove(enemy) || liveWaveStrength <= 0)
        {
            return;
        }
        float share = Settlement.UnitPower(enemy.definition) / liveWaveStrength;
        Settlement.ApplyRaid(share);
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
            liveWaveStrength = 0;
            Settlement.State.wavesSurvived++;
            ShowMessage("The wave is over.");
        }
    }

    /// <summary>Leaving mid-wave: whatever hasn't been dealt with is resolved by the numbers.</summary>
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

    // ---------- Helpers ----------

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
