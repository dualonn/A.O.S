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
    public float unitScale = 0.42f;

    [Tooltip("Defending troops hold the settlement plus this much ground around it.")]
    public float townGuardMargin = 5f;

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
        grid.Build(Settlement.Map);
        MapNavigator.Current = new MapNavigator(Settlement.Map, Settlement.Database.hillSpeed);
        SetUpCamera();
        Placer = new GameObject("BuildingPlacer").AddComponent<BuildingPlacer>();
        Placer.transform.SetParent(transform, false);
        Placer.Init(this);

        var life = new GameObject("AmbientLife").AddComponent<AmbientLife>();
        life.transform.SetParent(transform, false);
        life.Init(this);

        Settlement.Changed += SyncViews;
        Settlement.Message += ShowMessage;
        Settlement.MapGrown += OnMapGrown;
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
        Settlement.MapGrown -= OnMapGrown;
        if (ReferenceEquals(Settlement.Waves.LiveBattlefield, this))
        {
            Settlement.Waves.LiveBattlefield = null;
        }
        MapNavigator.Current = null;
        ResolveUnfinishedWave();
    }

    /// <summary>
    /// Before founding: show the whole map so the player can pick a spot.
    /// After: start over the settlement.
    /// </summary>
    private void SetUpCamera()
    {
        var cam = Camera.main;
        if (cam == null)
        {
            return;
        }
        var cameraController = cam.GetComponent<CameraController>();
        if (cameraController == null)
        {
            cameraController = cam.gameObject.AddComponent<CameraController>();
        }
        Rect map = grid.MapRect;
        UpdateCameraLimits();
        float fitWhole = cameraController.maxZoom;

        if (Settlement.IsFounded)
        {
            cam.transform.position = new Vector3(TownCenterPosition.x, TownCenterPosition.y, cam.transform.position.z);
            cam.orthographicSize = 12f;
        }
        else
        {
            cam.transform.position = new Vector3(map.center.x, map.center.y, cam.transform.position.z);
            cam.orthographicSize = fitWhole;
        }
    }

    /// <summary>Keep the camera over the explored map and let it zoom out to see all of it.</summary>
    private void UpdateCameraLimits()
    {
        var cam = Camera.main;
        var cameraController = cam != null ? cam.GetComponent<CameraController>() : null;
        if (cameraController == null)
        {
            return;
        }
        Rect map = grid.MapRect;
        cameraController.bounds = map;
        float fitWhole = Mathf.Max(map.height * 0.5f, map.width * 0.5f / Mathf.Max(0.5f, cam.aspect)) + 4f;
        cameraController.maxZoom = Mathf.Clamp(fitWhole, 60f, 1200f);
    }

    // ---------- Map markers (thumbnails shown when zoomed out) ----------

    private SpriteRenderer settlementMarker;
    private SpriteRenderer raidMarker;

    private SpriteRenderer MakeMarker(string objectName, Sprite sprite)
    {
        var sr = new GameObject(objectName).AddComponent<SpriteRenderer>();
        sr.transform.SetParent(transform, false);
        sr.sprite = sprite;
        sr.sortingOrder = 30;
        sr.enabled = false;
        return sr;
    }

    /// <summary>Settlement and raid thumbnails that keep a fixed on-screen size on the zoomed-out maps.</summary>
    private void UpdateMarkers()
    {
        var cam = Camera.main;
        if (cam == null)
        {
            return;
        }
        if (settlementMarker == null)
        {
            var campfire = Settlement.Database.buildings.Find(b => b.isTownCenter);
            var art = ArtLibrary.Load();
            settlementMarker = MakeMarker("SettlementMarker", campfire != null ? campfire.icon : null);
            raidMarker = MakeMarker("RaidMarker", art != null ? art.enemyIcon : null);
        }
        bool zoomedOut = grid.CurrentDetail != SettlementGrid.Detail.Tiles;
        float screenSize = cam.orthographicSize * 0.12f; // about 6% of the screen height

        settlementMarker.enabled = zoomedOut && Settlement.IsFounded && settlementMarker.sprite != null;
        if (settlementMarker.enabled)
        {
            Vector3 p = TownCenterPosition;
            settlementMarker.transform.position = new Vector3(p.x, p.y, -1);
            float h = settlementMarker.sprite.bounds.size.y;
            settlementMarker.transform.localScale = Vector3.one * screenSize / Mathf.Max(0.01f, h);
        }

        liveEnemies.RemoveAll(e => e == null);
        raidMarker.enabled = zoomedOut && waveInProgress && liveEnemies.Count > 0 && raidMarker.sprite != null;
        if (raidMarker.enabled)
        {
            Vector3 sum = Vector3.zero;
            foreach (var e in liveEnemies)
            {
                sum += e.transform.position;
            }
            Vector3 p = sum / liveEnemies.Count;
            raidMarker.transform.position = new Vector3(p.x, p.y, -1);
            float h = raidMarker.sprite.bounds.size.y;
            raidMarker.transform.localScale = Vector3.one * screenSize / Mathf.Max(0.01f, h);
        }
    }

    private void OnMapGrown(List<RectInt> newAreas)
    {
        grid.OnMapGrown(newAreas);
        UpdateCameraLimits();
    }

    private float nextGrowCheck;

    private const int MaxZonesPerCheck = 2;

    /// <summary>
    /// The map is endless up to its size limit, growing in zones. Your units and buildings do
    /// the exploring: an unexplored zone (touching explored land) opens up once one of them gets
    /// within sight of it. Moving the camera never explores anything.
    /// </summary>
    private void ExploreAroundUnitsAndBuildings()
    {
        if (Time.unscaledTime < nextGrowCheck)
        {
            return;
        }
        nextGrowCheck = Time.unscaledTime + 0.25f;
        var map = Settlement.Map;
        float sight = Settlement.Database.growWhenWithin * grid.cellSize;

        // Who can see: every placed building, and every living unit of yours.
        var eyes = new List<Rect>();
        foreach (var view in views.Values)
        {
            Vector2 corner = grid.CellToWorld(view.State.cell);
            eyes.Add(new Rect(corner, (Vector2)view.Definition.size * grid.cellSize));
        }
        foreach (var unit in Unit.Living)
        {
            if (unit != null && unit.Team == Team.Player && unit.IsAlive)
            {
                eyes.Add(new Rect(unit.transform.position, Vector2.zero));
            }
        }

        // Unexplored zones next to explored land, with how close the nearest eye is.
        var closest = new Dictionary<Vector2Int, float>();
        foreach (var eye in eyes)
        {
            Vector2Int min = map.ZoneOf(Mathf.FloorToInt(eye.xMin - sight), Mathf.FloorToInt(eye.yMin - sight));
            Vector2Int max = map.ZoneOf(Mathf.FloorToInt(eye.xMax + sight), Mathf.FloorToInt(eye.yMax + sight));
            for (int zy = min.y; zy <= max.y; zy++)
            {
                for (int zx = min.x; zx <= max.x; zx++)
                {
                    if (!map.ZoneInLimit(zx, zy) || map.IsZoneRevealed(zx, zy))
                    {
                        continue;
                    }
                    // Exploration spreads outward from known land.
                    if (!map.IsZoneRevealed(zx - 1, zy) && !map.IsZoneRevealed(zx + 1, zy) &&
                        !map.IsZoneRevealed(zx, zy - 1) && !map.IsZoneRevealed(zx, zy + 1))
                    {
                        continue;
                    }
                    var zr = map.ZoneRect(zx, zy);
                    float dx = Mathf.Max(0, Mathf.Max(zr.xMin - eye.xMax, eye.xMin - zr.xMax));
                    float dy = Mathf.Max(0, Mathf.Max(zr.yMin - eye.yMax, eye.yMin - zr.yMax));
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d > sight)
                    {
                        continue;
                    }
                    var key = new Vector2Int(zx, zy);
                    if (!closest.TryGetValue(key, out float known) || d < known)
                    {
                        closest[key] = d;
                    }
                }
            }
        }
        if (closest.Count == 0)
        {
            return;
        }
        // Closest first, a few per check so there's never a hitch.
        var candidates = new List<Vector2Int>(closest.Keys);
        candidates.Sort((x, y) => closest[x].CompareTo(closest[y]));
        if (candidates.Count > MaxZonesPerCheck)
        {
            candidates.RemoveRange(MaxZonesPerCheck, candidates.Count - MaxZonesPerCheck);
        }
        Settlement.ExploreZones(candidates);
    }

    /// <summary>Move and zoom the camera onto a point (e.g. right after founding).</summary>
    public void FocusOn(Vector3 point, float zoom = 12f)
    {
        var cam = Camera.main;
        if (cam == null)
        {
            return;
        }
        cam.transform.position = new Vector3(point.x, point.y, cam.transform.position.z);
        cam.orthographicSize = zoom;
    }

    /// <summary>Centre of the town center building (or of the map before founding).</summary>
    public Vector3 TownCenterPosition
    {
        get
        {
            foreach (var view in views.Values)
            {
                if (view.Definition.isTownCenter)
                {
                    return grid.AreaCenter(view.State.cell, view.Definition.size);
                }
            }
            foreach (var state in Settlement.State.buildings)
            {
                var def = Settlement.Database.GetBuilding(state.definitionId);
                if (def != null && def.isTownCenter)
                {
                    return grid.AreaCenter(state.cell, def.size);
                }
            }
            return grid.MapRect.center;
        }
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
        ExploreAroundUnitsAndBuildings();
        UpdateMarkers();
    }

    // ---------- Buildings ----------

    private void SyncViews()
    {
        var alive = new HashSet<int>();
        foreach (var state in Settlement.State.buildings)
        {
            alive.Add(state.instanceId);
            if (views.TryGetValue(state.instanceId, out var existing))
            {
                if (existing.Definition.id == state.definitionId)
                {
                    continue;
                }
                // Ranked up into a different building: rebuild its view in place.
                bool wasSelected = Selected == existing;
                if (wasSelected)
                {
                    Select(null);
                }
                Destroy(existing.gameObject);
                views.Remove(state.instanceId);
                var rankedDef = Settlement.Database.GetBuilding(state.definitionId);
                if (rankedDef != null)
                {
                    var ranked = new GameObject().AddComponent<BuildingView>();
                    ranked.transform.SetParent(grid.transform, true);
                    ranked.Init(this, state, rankedDef);
                    views.Add(state.instanceId, ranked);
                    if (wasSelected)
                    {
                        Select(ranked);
                    }
                }
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

        var areas = new List<RectInt>();
        foreach (var view in views.Values)
        {
            areas.Add(new RectInt(view.State.cell, view.Definition.size));
        }
        grid.RefreshCoverage(areas);
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
        // Somewhere dry on the map, near the building.
        Rect map = grid.MapRect;
        for (int attempt = 0; attempt < 30; attempt++)
        {
            Vector2 offset = UnityEngine.Random.insideUnitCircle * (rallySpread + attempt * 0.3f);
            Vector3 spot = center + new Vector3(offset.x, offset.y, 0);
            spot.x = Mathf.Clamp(spot.x, map.xMin + 0.5f, map.xMax - 0.5f);
            spot.y = Mathf.Clamp(spot.y, map.yMin + 0.5f, map.yMax - 0.5f);
            if (Settlement.Map.IsWalkable(spot))
            {
                return spot;
            }
        }
        return center;
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
            troop.SetStance(TroopStance.Defend, TownZoneFor(position), SightRange);
        }
        return go;
    }

    private Vector3 SettlementCenter => grid.MapRect.center;

    /// <summary>The ground Defend troops hold: the area around all your buildings, plus a margin.</summary>
    private List<Settlement.SettlementZone> zonesThisFrame;
    private int zonesFrame = -1;

    /// <summary>Settlement zones, worked out at most once per frame.</summary>
    public List<Settlement.SettlementZone> CurrentZones
    {
        get
        {
            if (zonesFrame != Time.frameCount || zonesThisFrame == null)
            {
                zonesThisFrame = Settlement.SettlementZones();
                zonesFrame = Time.frameCount;
            }
            return zonesThisFrame;
        }
    }

    /// <summary>
    /// The area troops from a building at 'home' defend: the buildings of that one settlement
    /// (plus a margin), so a second settlement far away doesn't stretch it across the map.
    /// </summary>
    public Rect TownZoneFor(Vector3 home)
    {
        var zone = Settlement.NearestZone(CurrentZones, home);
        if (zone == null)
        {
            return TownZone;
        }
        var b = zone.bounds;
        return Rect.MinMaxRect(b.xMin - townGuardMargin, b.yMin - townGuardMargin, b.xMax + townGuardMargin, b.yMax + townGuardMargin);
    }

    public Rect TownZone
    {
        get
        {
            if (views.Count == 0)
            {
                return new Rect(TownCenterPosition - Vector3.one * 8f, Vector2.one * 16f);
            }
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            foreach (var view in views.Values)
            {
                Vector2 a = grid.CellToWorld(view.State.cell);
                Vector2 b = a + (Vector2)view.Definition.size * grid.cellSize;
                min = Vector2.Min(min, a);
                max = Vector2.Max(max, b);
            }
            min -= Vector2.one * townGuardMargin;
            max += Vector2.one * townGuardMargin;
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
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
        // Raiders gather just outside a settlement's safe zone, on explored land.
        raidOrigin = RaidSpawnPoint();

        unspawned.AddRange(wave.units);
        waveInProgress = true;
        string from = CompassName(raidOrigin - TownCenterPosition);
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
                // Spread the group out a little, never into water, unexplored land or a safe zone.
                Vector3 spawn = raidOrigin;
                var zones = Settlement.SettlementZones();
                for (int attempt = 0; attempt < 8; attempt++)
                {
                    Vector2 jitter = UnityEngine.Random.insideUnitCircle * 2f;
                    Vector3 p = raidOrigin + new Vector3(jitter.x, jitter.y, 0);
                    if (Settlement.Map.IsWalkable(p) && !Settlement.InAnyZone(zones, p))
                    {
                        spawn = p;
                        break;
                    }
                }
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

    /// <summary>Raiders flee straight out of the settlement they hit, to just past its safe zone.</summary>
    public Vector3 ExitPointFor(Vector3 from)
    {
        var zones = Settlement.SettlementZones();
        var zone = Settlement.NearestZone(zones, from);
        if (zone == null)
        {
            return grid.NearestEdgePoint(from);
        }
        Vector2 away = (Vector2)from - zone.centre;
        float baseAngle = away.sqrMagnitude > 0.01f ? Mathf.Atan2(away.y, away.x) : UnityEngine.Random.value * Mathf.PI * 2f;
        // Straight out if possible, otherwise swing round until there's open, explored land.
        for (int i = 0; i < 24; i++)
        {
            float angle = baseAngle + (i % 2 == 0 ? 1 : -1) * (i / 2) * 0.26f;
            var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            if (FirstClearPoint(zones, (Vector2)from, dir, out var p))
            {
                return p;
            }
        }
        return Settlement.Map.IsWalkable(raidOrigin) ? raidOrigin : grid.NearestEdgePoint(from);
    }

    /// <summary>Walk from 'start' along 'dir' to the first open spot 3 tiles past every safe zone.</summary>
    private bool FirstClearPoint(List<Settlement.SettlementZone> zones, Vector2 start, Vector2 dir, out Vector3 point)
    {
        for (float d = 0; d < 160f; d += 1f)
        {
            var p = start + dir * d;
            if (!Settlement.Map.InBounds(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y)))
            {
                break;
            }
            if (MinClearance(zones, p) >= 3f)
            {
                if (Settlement.Map.IsWalkable(p))
                {
                    point = p;
                    return true;
                }
            }
        }
        point = default;
        return false;
    }

    private static float MinClearance(List<Settlement.SettlementZone> zones, Vector2 p)
    {
        float c = float.MaxValue;
        foreach (var zone in zones)
        {
            c = Mathf.Min(c, zone.Clearance(p));
        }
        return c;
    }

    /// <summary>
    /// Where a raid starts: a walkable, explored, reachable spot in a band just outside a random
    /// settlement's safe zone. If the explored land doesn't reach that far yet, the explored spot
    /// furthest from every settlement. Before there's any town, the edge of the explored land.
    /// </summary>
    public Vector3 RaidSpawnPoint()
    {
        var zones = Settlement.SettlementZones();
        var targets = zones.FindAll(z => !z.IsOutpost);
        if (targets.Count == 0)
        {
            return grid.RandomEdgePoint();
        }
        var map = Settlement.Map;
        float band = Settlement.Database.raidSpawnBand;
        Vector3 fallback = Vector3.positiveInfinity;
        Vector3 furthest = Vector3.positiveInfinity;
        float furthestClearance = float.MinValue;
        for (int attempt = 0; attempt < 160; attempt++)
        {
            // Set out from one of the town's buildings, so long towns get raided from any side.
            var zone = targets[UnityEngine.Random.Range(0, targets.Count)];
            var from = zone.buildings[UnityEngine.Random.Range(0, zone.buildings.Count)];
            float angle = UnityEngine.Random.value * Mathf.PI * 2f;
            var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            float distance = Mathf.Max(from.width, from.height) * 0.5f + zone.safeDistance + UnityEngine.Random.Range(2f, band);
            Vector3 p = from.center + dir * distance;
            if (!map.IsWalkable(p))
            {
                continue;
            }
            float clearance = MinClearance(zones, p);
            if (clearance < 0)
            {
                if (clearance > furthestClearance)
                {
                    furthestClearance = clearance;
                    furthest = p;
                }
                continue;
            }
            if (float.IsInfinity(fallback.x))
            {
                fallback = p;
            }
            // Skip islands and pockets the raiders couldn't walk out of.
            var path = MapNavigator.Current?.FindPath(p, zone.centre);
            if (path == null || (path.Count > 0 && zone.Contains(path[path.Count - 1])))
            {
                return p;
            }
        }
        if (!float.IsInfinity(fallback.x)) return fallback;
        if (!float.IsInfinity(furthest.x)) return furthest;
        return grid.RandomEdgePoint();
    }

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
