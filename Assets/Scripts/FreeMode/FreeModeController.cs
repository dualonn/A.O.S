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
    public static FreeModeController Instance { get; private set; }
    public SettlementGrid Grid => grid;
    public BuildingPlacer Placer { get; private set; }
    /// <summary>The first selected building (the one the single-building panel shows).</summary>
    public BuildingView Selected => selection.Count > 0 ? selection[0] : null;

    /// <summary>Every selected building, in the order they were picked.</summary>
    public IReadOnlyList<BuildingView> Selection => selection;

    /// <summary>Selected troops (buildings and troops are never selected together).</summary>
    public IReadOnlyList<Troop> SelectedUnits => unitSelection;

    /// <summary>Troops stationed on this world from elsewhere, and boarding troopships.</summary>
    public Garrison Garrison { get; } = new Garrison();

    /// <summary>Raised when the selected troops change.</summary>
    public event Action UnitSelectionChanged;

    private readonly List<Troop> unitSelection = new List<Troop>();
    public IEnumerable<BuildingView> Buildings => views.Values;
    public bool WaveInProgress => waveInProgress;

    /// <summary>For the HUD: selection changed, or a message to show.</summary>
    public event Action<BuildingView> SelectionChanged;
    public event Action<string> MessageShown;

    private readonly Dictionary<int, BuildingView> views = new Dictionary<int, BuildingView>();
    private readonly List<BuildingView> selection = new List<BuildingView>();

    // Live wave tracking
    private readonly List<Enemy> liveEnemies = new List<Enemy>();
    private readonly List<UnitDefinition> unspawned = new List<UnitDefinition>();
    private readonly List<ResourceAmount> stolenThisWave = new List<ResourceAmount>();
    private bool waveInProgress;
    private Vector3 raidOrigin;
    // Where the current raid is heading: the nearest building to where it set out.
    private Vector3 raidTargetPoint;

    /// <summary>Is the raid in progress heading for this settlement (from the moment it sets out)?</summary>
    public bool RaidTargets(Settlement.SettlementZone zone) =>
        waveInProgress && zone != null && zone.DistanceTo(raidTargetPoint) <= 1f;

    /// <summary>Is this raider one of the raid in progress, and is that raid heading for this settlement?</summary>
    public bool InRaidOn(Settlement.SettlementZone zone, Unit unit) =>
        unit is Enemy e && e.InLiveWave && RaidTargets(zone);

    private void Awake()
    {
        Instance = this;
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
        gameObject.AddComponent<RiverWorks>().Init(this);
        gameObject.AddComponent<TradeWorks>().Init(this);   // trade routes, caravans, road pieces   // bridges, ferries, warships, naval raids

        var wildlife = new GameObject("Wildlife").AddComponent<WildlifeView>();
        wildlife.transform.SetParent(transform, false);
        wildlife.Init(this);
        QuestMarkers.Create(this);   // quest banners, lairs and their named bosses

        Settlement.Changed += SyncViews;
        Settlement.Message += ShowMessage;
        Settlement.MapGrown += OnMapGrown;
        Settlement.WorldSwitched += OnWorldSwitched;
        Settlement.Waves.LiveBattlefield = this;
        Settlement.RaidUnderway = () => waveInProgress;
        SyncViews();
    }

    /// <summary>Another world is now in view: rebuild the whole scene round its map.</summary>
    private void OnWorldSwitched()
    {
        enabled = false;
        UnityEngine.SceneManagement.SceneManager.LoadScene(gameObject.scene.buildIndex);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (Settlement == null)
        {
            return;
        }
        Settlement.Changed -= SyncViews;
        Settlement.Message -= ShowMessage;
        Settlement.MapGrown -= OnMapGrown;
        Settlement.WorldSwitched -= OnWorldSwitched;
        if (ReferenceEquals(Settlement.Waves.LiveBattlefield, this))
        {
            Settlement.Waves.LiveBattlefield = null;
        }
        MapNavigator.Current = null;
        Settlement.RaidUnderway = null;
        Settlement.ClearLiveTroops();
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

        UpdateCampMarkers(zoomedOut, screenSize, raidMarker.sprite);
        UpdateSiteMarkers(zoomedOut, screenSize);

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

    // ---------- Raider camps ----------

    private readonly Dictionary<CampState, CampView> campViews = new Dictionary<CampState, CampView>();
    private float nextCampSync;
    private readonly List<SpriteRenderer> campMarkers = new List<SpriteRenderer>();

    /// <summary>
    /// A view for every camp: live ones with their guards, burnt ones as smouldering ruins (until
    /// raiders move back in, or you build over the ashes).
    /// </summary>
    private void SyncCamps()
    {
        if (Time.unscaledTime < nextCampSync)
        {
            return;
        }
        nextCampSync = Time.unscaledTime + 1f;
        foreach (var camp in Settlement.State.camps)
        {
            bool has = campViews.TryGetValue(camp, out var view) && view != null;
            bool builtOver = camp.cleared && BuiltOver(new RectInt(camp.cell, Vector2Int.one * CampState.Size));
            // Raiders moved back into a ruin, or someone built over the ashes: take the old view down.
            if (has && ((!camp.cleared && view.Burnt) || builtOver))
            {
                view.Remove();
                campViews.Remove(camp);
                has = false;
            }
            if (!has && !builtOver)
            {
                view = new GameObject().AddComponent<CampView>();
                view.transform.SetParent(grid.transform, true);
                view.Init(this, camp, Settlement.CampTier(camp));
                campViews[camp] = view;
            }
        }
    }

    private bool BuiltOver(RectInt area)
    {
        foreach (var b in Settlement.State.buildings)
        {
            var def = Settlement.Database.GetBuilding(b.definitionId);
            if (def != null && new RectInt(b.cell, def.size).Overlaps(area)) return true;
        }
        return false;
    }

    /// <summary>Troops burnt a camp down.</summary>
    public void CampDestroyed(CampView view)
    {
        Settlement.ClearCamp(view.State);
        Settlement.NativeCampBurnt();
        view.BurnDown(instant: false);   // it burns, then stays as a ruin until raiders return
        GameAudio.Play("camp_destroyed", view.transform.position);
    }

    /// <summary>The nearest live camp within raiding range of 'from', if any.</summary>
    private CampState NearestCamp(Vector3 from)
    {
        CampState best = null;
        float bestDistance = Settlement.Database.campRaidRange;
        foreach (var camp in Settlement.State.camps)
        {
            if (camp.cleared) continue;
            float d = Vector2.Distance(from, camp.Centre);
            if (d < bestDistance)
            {
                bestDistance = d;
                best = camp;
            }
        }
        return best;
    }

    /// <summary>A walkable spot just outside a camp, for raiders setting out or coming home.</summary>
    private Vector3 BesideCamp(CampState camp)
    {
        var map = Settlement.Map;
        for (int i = 0; i < 16; i++)
        {
            float angle = i * Mathf.PI * 2f / 16f + UnityEngine.Random.value * 0.3f;
            Vector3 p = (Vector3)camp.Centre + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * (CampState.Size * 0.5f + 1.2f);
            if (map.IsWalkable(p)) return p;
        }
        return camp.Centre;
    }

    // ---------- Dungeon levels ----------

    /// <summary>The dungeon level the camera is looking at (null = the surface).</summary>
    public DungeonRun Viewing { get; private set; }
    private Vector3 surfaceCamera;
    private float surfaceZoom;

    /// <summary>Look down into a dungeon level.</summary>
    public void FocusDungeon(DungeonRun run)
    {
        var cam = Camera.main;
        if (run == null || cam == null) return;
        var cc = cam.GetComponent<CameraController>();
        if (Viewing == null)
        {
            surfaceCamera = cam.transform.position;
            surfaceZoom = cam.orthographicSize;
        }
        Viewing = run;
        var r = run.WorldRect;
        if (cc != null) cc.overrideBounds = r;
        var room = run.Rooms[Mathf.Clamp(run.Current, 0, run.Rooms.Count - 1)];
        cam.transform.position = new Vector3(room.Centre.x, room.Centre.y, cam.transform.position.z);
    }

    private Coroutine pan;

    /// <summary>Glide the camera to a point (following a dungeon party room to room).</summary>
    public void PanTo(Vector3 point)
    {
        if (pan != null) StopCoroutine(pan);
        pan = StartCoroutine(PanRoutine(point));
    }

    private IEnumerator PanRoutine(Vector3 point)
    {
        var cam = Camera.main;
        if (cam == null) yield break;
        Vector3 from = cam.transform.position, to = new Vector3(point.x, point.y, from.z);
        for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / 1.2f)
        {
            if (cam == null) yield break;
            float k = t * t * (3 - 2 * t);
            cam.transform.position = Vector3.Lerp(from, to, k);
            yield return null;
        }
        if (cam != null) cam.transform.position = to;
        pan = null;
    }

    /// <summary>Back up to the surface.</summary>
    public void ShowSurface()
    {
        var cam = Camera.main;
        if (Viewing == null || cam == null) return;
        var cc = cam.GetComponent<CameraController>();
        if (cc != null) cc.overrideBounds = null;
        cam.transform.position = surfaceCamera;
        cam.orthographicSize = surfaceZoom;
        Viewing = null;
    }

    /// <summary>A run ended: if we were looking at it, come back up.</summary>
    public void LeaveDungeonView(DungeonRun run)
    {
        if (Viewing == run) ShowSurface();
    }

    // ---------- Wild sites ----------

    private readonly Dictionary<SiteState, SiteView> siteViews = new Dictionary<SiteState, SiteView>();
    private float nextSiteSync;
    private readonly List<SpriteRenderer> siteMarkers = new List<SpriteRenderer>();

    /// <summary>A view for every site on the map; taken caches disappear.</summary>
    private void SyncSites()
    {
        if (Time.unscaledTime < nextSiteSync) return;
        nextSiteSync = Time.unscaledTime + 1f;
        foreach (var site in Settlement.State.sites)
        {
            bool has = siteViews.TryGetValue(site, out var view);
            bool gone = site.kind == SiteKind.Cache && site.done;
            if (!gone && !has)
            {
                view = new GameObject().AddComponent<SiteView>();
                view.transform.SetParent(transform, false);
                view.Init(this, site, Settlement.SiteTier(site));
                siteViews[site] = view;
            }
            else if (gone && has)
            {
                if (view != null) view.Remove();
                siteViews.Remove(site);
            }
        }
    }

    /// <summary>Site icons on the zoomed-out maps (used-up sites fade).</summary>
    private void UpdateSiteMarkers(bool zoomedOut, float screenSize)
    {
        int i = 0;
        var art = ArtLibrary.Load();
        if (zoomedOut && art != null && art.siteIcons.Count >= 6)
        {
            foreach (var site in Settlement.State.sites)
            {
                if (site.kind == SiteKind.Cache && site.done) continue;
                if (site.kind == SiteKind.NaturalWonder) continue;   // drawn by XWonderMarkers (own icon, glow, hover)
                if ((int)site.kind >= art.siteIcons.Count) continue;
                var icon = art.siteIcons[(int)site.kind];
                if (icon == null) continue;
                if (siteMarkers.Count <= i) siteMarkers.Add(MakeMarker("SiteMarker", icon));
                var m = siteMarkers[i++];
                m.sprite = icon;
                m.enabled = true;
                m.color = site.done ? new Color(1f, 1f, 1f, 0.45f) : Color.white;
                m.transform.position = new Vector3(site.Centre.x, site.Centre.y, -1);
                m.transform.localScale = Vector3.one * screenSize * 0.7f / Mathf.Max(0.01f, icon.bounds.size.y);
            }
        }
        for (; i < siteMarkers.Count; i++) siteMarkers[i].enabled = false;
    }

    private void UpdateCampMarkers(bool zoomedOut, float screenSize, Sprite icon)
    {
        int i = 0;
        if (zoomedOut && icon != null)
        {
            foreach (var camp in Settlement.State.camps)
            {
                if (camp.cleared) continue;
                if (campMarkers.Count <= i)
                {
                    var marker = MakeMarker("CampMarker", icon);
                    marker.color = new Color(1f, 0.75f, 0.7f);
                    campMarkers.Add(marker);
                }
                var m = campMarkers[i++];
                m.enabled = true;
                m.transform.position = new Vector3(camp.Centre.x, camp.Centre.y, -1);
                m.transform.localScale = Vector3.one * screenSize * 0.8f / Mathf.Max(0.01f, icon.bounds.size.y);
            }
        }
        for (; i < campMarkers.Count; i++) campMarkers[i].enabled = false;
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
            if (unit != null && unit.Team == Team.Player && unit.IsAlive && !(unit is Troop tr && tr.Dungeon != null))
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
                    if (map.IsZoneRevealed(zx, zy))
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
        UpdateSelectionInput();
        UpdateUnitOrders();
        if (Input.GetKeyDown(KeyCode.Escape) && !Placer.IsPlacing)
        {
            Select(null);
            SetUnitSelection(null);
        }
        SyncCamps();
        SyncSites();

        TrackLiveWave();
        OrbitalStrikes();
        Garrison.Keep(this);
        ExploreAroundUnitsAndBuildings();
        UpdateMarkers();
    }

    // ---------- Orbital defence ----------

    private float nextStrike;

    /// <summary>
    /// Orbital defence platforms (research, on a world with a Starport) drop a strike on the
    /// toughest raider inside one of your settlements every so often; more levels hit harder.
    /// </summary>
    private void OrbitalStrikes()
    {
        int level = Settlement.OrbitalLevel;
        if (level <= 0 || Time.time < nextStrike) return;
        nextStrike = Time.time + Mathf.Max(4f, 14f - level);
        Unit best = null;
        var zones = CurrentZones;
        foreach (var u in Unit.Living)
        {
            if (u == null || !u.IsAlive || u.Team != Team.Enemy || !(u is Enemy e) || !e.IsRaider) continue;
            if (!Settlement.InAnyZone(zones, u.transform.position)) continue;
            if (best == null || u.health > best.health) best = u;
        }
        if (best == null) return;
        Puff.Blast(best.transform.position, 1.2f, true);
        best.TakeDamage(250 * level);
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
                int selectedAt = selection.IndexOf(existing);
                Destroy(existing.gameObject);
                views.Remove(state.instanceId);
                var rankedDef = Settlement.Database.GetBuilding(state.definitionId);
                if (rankedDef != null)
                {
                    var ranked = new GameObject().AddComponent<BuildingView>();
                    ranked.transform.SetParent(grid.transform, true);
                    ranked.Init(this, state, rankedDef);
                    views.Add(state.instanceId, ranked);
                    if (selectedAt >= 0)
                    {
                        selection[selectedAt] = ranked;
                        ranked.SetSelected(true, selection.Count == 1);
                        selectionDirty = true;
                    }
                }
                else if (selectedAt >= 0)
                {
                    selection.RemoveAt(selectedAt);
                    selectionDirty = true;
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
            if (selection.Remove(views[id]))
            {
                selectionDirty = true;
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
        cellIndexDirty = true;
        foreach (var view in views.Values)
        {
            if (view.Definition.IsWall) view.SetWallMask(WallMask(view.State.cell));
        }
        if (selectionDirty)
        {
            selectionDirty = false;
            SelectionChanged?.Invoke(Selected);
        }
    }

    private bool selectionDirty;

    public BuildingView BuildingAt(Vector2Int cell)
    {
        if (cellIndexDirty)
        {
            RebuildCellIndex();
        }
        return byCell.TryGetValue(cell, out var view) && view != null ? view : null;
    }

    // Every footprint tile -> its building (walls make for hundreds of small buildings).
    private readonly Dictionary<Vector2Int, BuildingView> byCell = new Dictionary<Vector2Int, BuildingView>();
    private bool cellIndexDirty = true;

    private void RebuildCellIndex()
    {
        cellIndexDirty = false;
        byCell.Clear();
        foreach (var view in views.Values)
        {
            var area = new RectInt(view.State.cell, view.Definition.size);
            foreach (var c in area.allPositionsWithin) byCell[c] = view;
        }
    }

    // ---------- Selection ----------

    /// <summary>Select one building (or nothing).</summary>
    public void Select(BuildingView view) => SetSelection(view != null ? new[] { view } : null);

    /// <summary>Replace the selection with these buildings.</summary>
    public void SetSelection(IEnumerable<BuildingView> picked)
    {
        var next = new List<BuildingView>();
        if (picked != null)
        {
            foreach (var view in picked)
            {
                if (view != null && !next.Contains(view)) next.Add(view);
            }
        }
        if (next.Count == selection.Count && next.TrueForAll(selection.Contains))
        {
            return;
        }
        foreach (var view in selection)
        {
            if (view != null) view.SetSelected(false);
        }
        selection.Clear();
        selection.AddRange(next);
        foreach (var view in selection)
        {
            view.SetSelected(true, selection.Count == 1);
        }
        if (selection.Count > 0)
        {
            SetUnitSelection(null);
        }
        SelectionChanged?.Invoke(Selected);
    }

    /// <summary>Add these buildings to the selection.</summary>
    public void AddToSelection(IEnumerable<BuildingView> picked)
    {
        var next = new List<BuildingView>(selection);
        next.AddRange(picked);
        SetSelection(next);
    }

    /// <summary>Add a building to the selection, or take it out if it's already in.</summary>
    public void ToggleSelected(BuildingView view)
    {
        if (view == null)
        {
            return;
        }
        var next = new List<BuildingView>(selection);
        if (!next.Remove(view)) next.Add(view);
        SetSelection(next);
    }

    /// <summary>Every building of this type: on screen, or anywhere on the map.</summary>
    public List<BuildingView> AllOfType(BuildingDefinition def, bool onScreenOnly)
    {
        var list = new List<BuildingView>();
        var cam = Camera.main;
        Rect view = default;
        if (cam != null)
        {
            float h = cam.orthographicSize, w = h * cam.aspect;
            view = new Rect(cam.transform.position.x - w, cam.transform.position.y - h, w * 2, h * 2);
        }
        foreach (var v in views.Values)
        {
            if (v.Definition != def) continue;
            if (onScreenOnly && cam != null && !view.Contains(grid.AreaCenter(v.State.cell, v.Definition.size))) continue;
            list.Add(v);
        }
        return list;
    }

    // Box select: press on the ground, drag past a few pixels, release.
    private const float DragThresholdPixels = 8f;
    private bool pressing, dragging;
    private Vector2 pressScreen;
    private Vector3 pressWorld;
    private float lastClickTime = -1f;
    private BuildingView lastClicked;
    private LineRenderer boxOutline;
    private SpriteRenderer boxFill;

    private static bool ShiftHeld => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
    private static bool CtrlHeld => Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

    private void UpdateSelectionInput()
    {
        if (Placer.IsPlacing)
        {
            pressing = dragging = false;
            ShowBox(false);
            return;
        }
        if (Input.GetMouseButtonDown(0) && !BuildingPlacer.IsPointerOverUI())
        {
            pressing = true;
            dragging = false;
            pressScreen = Input.mousePosition;
            pressWorld = SettlementGrid.MouseWorld();
        }
        if (!pressing)
        {
            return;
        }
        if (!dragging && ((Vector2)Input.mousePosition - pressScreen).magnitude > DragThresholdPixels)
        {
            dragging = true;
        }
        Vector3 now = SettlementGrid.MouseWorld();
        if (dragging)
        {
            DrawBox(pressWorld, now);
        }
        if (!Input.GetMouseButtonUp(0))
        {
            return;
        }
        pressing = false;
        if (dragging)
        {
            dragging = false;
            ShowBox(false);
            // Troops in the box win over buildings.
            var troops = TroopsInBox(pressWorld, now);
            if (troops.Count > 0)
            {
                if (ShiftHeld) troops.InsertRange(0, unitSelection);
                SetUnitSelection(troops);
                return;
            }
            var picked = BuildingsInBox(pressWorld, now);
            if (ShiftHeld) AddToSelection(picked);
            else SetSelection(picked);
            return;
        }

        var troop = TroopAt(now);
        if (troop != null)
        {
            ClickTroop(troop);
            return;
        }
        var clicked = BuildingAt(grid.WorldToCell(now));
        if (clicked == null && unitSelection.Count == 0 && CampView.At(now) != null && DiplomacyPanel.Open(Settlement)) return;
        if (clicked == null && unitSelection.Count > 0 && !ShiftHeld)
        {
            SetUnitSelection(null);
        }
        bool doubleClick = clicked != null && clicked == lastClicked && Time.unscaledTime - lastClickTime < 0.35f;
        lastClicked = clicked;
        lastClickTime = Time.unscaledTime;
        if (doubleClick)
        {
            // Double-click: every building of that type on screen (Ctrl: on the whole map).
            var same = AllOfType(clicked.Definition, !CtrlHeld);
            if (ShiftHeld) AddToSelection(same);
            else SetSelection(same);
            lastClicked = null;
        }
        else if (ShiftHeld)
        {
            ToggleSelected(clicked);
        }
        else
        {
            Select(clicked);
        }
    }

    // ---------- Troop selection and orders ----------

    private Troop lastClickedTroop;
    private float lastTroopClickTime = -1f;

    /// <summary>Your troop nearest the point, if one is close enough to have been clicked.</summary>
    private Troop TroopAt(Vector3 world)
    {
        Troop best = null;
        float bestDistance = 0.45f;
        foreach (var unit in Unit.Living)
        {
            if (unit is Troop t && t.IsAlive)
            {
                // Aim at the body, a little above the feet.
                float d = Vector2.Distance(world, t.transform.position + Vector3.up * 0.25f);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = t;
                }
            }
        }
        return best;
    }

    private List<Troop> TroopsInBox(Vector3 a, Vector3 b)
    {
        var box = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        var list = new List<Troop>();
        foreach (var unit in Unit.Living)
        {
            if (unit is Troop t && t.IsAlive && box.Contains(t.transform.position)) list.Add(t);
        }
        return list;
    }

    private void ClickTroop(Troop troop)
    {
        bool doubleClick = troop == lastClickedTroop && Time.unscaledTime - lastTroopClickTime < 0.35f;
        lastClickedTroop = troop;
        lastTroopClickTime = Time.unscaledTime;
        if (doubleClick)
        {
            // Every troop of that kind on screen.
            var cam = Camera.main;
            float h = cam.orthographicSize, w = h * cam.aspect;
            var view = new Rect(cam.transform.position.x - w, cam.transform.position.y - h, w * 2, h * 2);
            var same = new List<Troop>();
            foreach (var unit in Unit.Living)
            {
                if (unit is Troop t && t.IsAlive && t.definition == troop.definition && view.Contains(t.transform.position)) same.Add(t);
            }
            SetUnitSelection(same);
            lastClickedTroop = null;
        }
        else if (ShiftHeld)
        {
            var next = new List<Troop>(unitSelection);
            if (!next.Remove(troop)) next.Add(troop);
            SetUnitSelection(next);
        }
        else
        {
            SetUnitSelection(new List<Troop> { troop });
        }
    }

    /// <summary>Replace the selected troops (null or empty clears them).</summary>
    public void SetUnitSelection(IEnumerable<Troop> troops)
    {
        var next = new List<Troop>();
        if (troops != null)
        {
            foreach (var t in troops)
            {
                if (t != null && t.IsAlive && !next.Contains(t)) next.Add(t);
            }
        }
        if (next.Count == unitSelection.Count && next.TrueForAll(unitSelection.Contains))
        {
            return;
        }
        foreach (var t in unitSelection)
        {
            if (t != null) t.SetSelected(false);
        }
        unitSelection.Clear();
        unitSelection.AddRange(next);
        foreach (var t in unitSelection) t.SetSelected(true);
        if (unitSelection.Count > 0 && selection.Count > 0)
        {
            SetSelection(null);
        }
        UnitSelectionChanged?.Invoke();
    }

    /// <summary>Right-click sends the selected troops there, spread in a loose block; they hold that spot.</summary>
    private void UpdateUnitOrders()
    {
        // Drop fallen troops from the selection.
        if (unitSelection.RemoveAll(t => t == null || !t.IsAlive) > 0)
        {
            UnitSelectionChanged?.Invoke();
        }
        if (unitSelection.Count == 0 || Placer.IsPlacing || !Input.GetMouseButtonDown(1) || BuildingPlacer.IsPointerOverUI())
        {
            return;
        }
        Vector3 target = SettlementGrid.MouseWorld();
        var site = SiteView.At(target);
        if (site != null && site.State.kind == SiteKind.Dungeon && !site.State.done)
        {
            // Right-click on a dungeon: the selected troops go down into it together.
            DungeonRun.For(this, site.State).Enlist(unitSelection);
            ShowOrderMarker((Vector3)site.State.Centre, new Color(0.75f, 0.55f, 1f, 0.9f));
            ShowMessage(unitSelection.Count == 1 ? "Heading into the dungeon." : $"{unitSelection.Count} troops are heading into the dungeon.");
            return;
        }
        var camp = CampView.At(target);
        if (camp != null)
        {
            // Right-click on a raider camp: everyone selected commits to burning it down.
            foreach (var t in unitSelection) t.OrderAttackCamp(camp);
            ShowOrderMarker(camp.Centre, new Color(1f, 0.45f, 0.35f, 0.9f));
            ShowMessage(unitSelection.Count == 1 ? "Attacking the raider camp." : $"{unitSelection.Count} troops are attacking the raider camp.");
            return;
        }
        int n = unitSelection.Count;
        int columns = Mathf.CeilToInt(Mathf.Sqrt(n));
        float spacing = 0.7f;
        for (int i = 0; i < n; i++)
        {
            int row = i / columns, col = i % columns;
            Vector3 offset = new Vector3((col - (columns - 1) * 0.5f) * spacing, -(row - (n - 1) / columns * 0.5f) * spacing, 0);
            unitSelection[i].OrderMoveTo(target + offset);
        }
        ShowOrderMarker(target, new Color(0.5f, 1f, 0.5f, 0.9f));
    }

    /// <summary>Send the selected troops back to their buildings.</summary>
    public void ReturnSelectedHome()
    {
        foreach (var t in unitSelection)
        {
            if (t != null && t.Home != null) t.ReturnHome(GetRallyPoint(t.Home));
        }
    }

    private SpriteRenderer orderMarker;
    private float orderMarkerUntil;

    private void ShowOrderMarker(Vector3 at, Color color)
    {
        if (orderMarker == null)
        {
            orderMarker = new GameObject("OrderMarker").AddComponent<SpriteRenderer>();
            orderMarker.transform.SetParent(transform, false);
            orderMarker.sprite = SimpleSprites.Ring;
            orderMarker.sortingOrder = 18;
        }
        orderMarker.color = color;
        orderMarker.transform.position = new Vector3(at.x, at.y, -1f);
        orderMarker.enabled = true;
        orderMarkerUntil = Time.unscaledTime + 0.6f;
    }

    private void LateUpdate()
    {
        if (orderMarker != null && orderMarker.enabled)
        {
            float left = orderMarkerUntil - Time.unscaledTime;
            orderMarker.enabled = left > 0;
            orderMarker.transform.localScale = Vector3.one * (0.6f + (0.6f - left) * 1.2f);
        }
    }

    private List<BuildingView> BuildingsInBox(Vector3 a, Vector3 b)
    {
        Vector2Int c0 = grid.WorldToCell(Vector3.Min(a, b));
        Vector2Int c1 = grid.WorldToCell(Vector3.Max(a, b));
        var box = new RectInt(c0, c1 - c0 + Vector2Int.one);
        var list = new List<BuildingView>();
        foreach (var v in views.Values)
        {
            if (new RectInt(v.State.cell, v.Definition.size).Overlaps(box)) list.Add(v);
        }
        return list;
    }

    private void DrawBox(Vector3 a, Vector3 b)
    {
        if (boxOutline == null)
        {
            boxOutline = new GameObject("SelectionBox").AddComponent<LineRenderer>();
            boxOutline.transform.SetParent(transform, false);
            boxOutline.loop = true;
            boxOutline.positionCount = 4;
            boxOutline.useWorldSpace = true;
            boxOutline.material = new Material(Shader.Find("Sprites/Default"));
            boxOutline.startColor = boxOutline.endColor = new Color(1f, 0.92f, 0.3f, 0.95f);
            boxOutline.sortingOrder = 20;
            boxFill = new GameObject("Fill").AddComponent<SpriteRenderer>();
            boxFill.transform.SetParent(boxOutline.transform, false);
            var tex = Texture2D.whiteTexture;
            boxFill.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.zero, tex.width);
            boxFill.color = new Color(1f, 0.92f, 0.3f, 0.12f);
            boxFill.sortingOrder = 19;
        }
        ShowBox(true);
        Vector3 min = Vector3.Min(a, b), max = Vector3.Max(a, b);
        min.z = max.z = -2f;
        boxOutline.SetPositions(new[] { min, new Vector3(min.x, max.y, -2f), max, new Vector3(max.x, min.y, -2f) });
        var cam = Camera.main;
        boxOutline.startWidth = boxOutline.endWidth = cam != null ? cam.orthographicSize * 0.006f : 0.08f;
        boxFill.transform.position = min;
        boxFill.transform.localScale = new Vector3(max.x - min.x, max.y - min.y, 1);
    }

    private void ShowBox(bool on)
    {
        if (boxOutline != null) boxOutline.gameObject.SetActive(on);
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
            if (Settlement != null && Settlement.Map != null && Settlement.Map.IsWalkable(spot))
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
    /// <summary>The settlement a building at 'home' belongs to (null before there is one).</summary>
    public Settlement.SettlementZone SettlementFor(Vector3 home) => Settlement.NearestZone(CurrentZones, home);

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

    /// <summary>How far troops spot raiders (the map is endless, so this is a fixed reach).</summary>
    public float SightRange => 140f;

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
        // Raiders set out from the nearest camp in range; with none, they gather just outside a
        // settlement's safe zone, on explored land.
        var camp = CampForRaid();
        raidOrigin = camp != null ? BesideCamp(camp) : RaidSpawnPoint();
        raidOrigin = RiverWorks.ConsiderNavalRaid(camp == null, raidOrigin, wave.units.Count);   // some raids come by sea
        var aim = NearestTarget(raidOrigin);
        raidTargetPoint = aim != null ? ClosestPointOn(aim, raidOrigin) : TownCenterPosition;

        unspawned.AddRange(wave.units);
        waveInProgress = true;
        GameAudio.UI("raid_warning");
        string from = CompassName(raidOrigin - TownCenterPosition);
        string where = camp != null ? $"the camp to the {from}" : $"the {from}";
        if (wave.boss != null)
        {
            int band = wave.units.Count - 1;
            ShowMessage(band > 0
                ? $"{wave.boss.displayName} leads {band} raider{(band == 1 ? "" : "s")} from {where}!"
                : $"{wave.boss.displayName} is coming from {where}!");
        }
        else ShowMessage(wave.units.Count == 1
            ? $"A raider is coming from {where}!"
            : $"{wave.units.Count} raiders are coming from {where}!");
        StartCoroutine(SpawnRoutine());
    }

    private IEnumerator SpawnRoutine()
    {
        while (RiverWorks.HoldingRaid) yield return null;   // a raid by sea comes ashore when its boats land
        raidOrigin = RiverWorks.LandRaid(unspawned, raidOrigin);
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
                    enemy.InLiveWave = true;
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
            Settlement.XRecordRaid(stolenThisWave.Count > 0, 0f);
            string loss = Settlement.Describe(stolenThisWave);
            GameAudio.UI(loss.Length > 0 ? "notify" : "confirm");
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
            // Walls aren't worth raiding: raiders only smash the ones in their way.
            if (!IsStanding(view) || view.Definition.IsWall)
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
        GameAudio.Play(UnityEngine.Random.value < 0.2f ? "creak" : "blunt_hit", raider.transform.position, 0.55f);
        if (building.Definition.IsWall)
        {
            return; // nothing to loot in a wall
        }
        var stolen = Settlement.Steal(raider.lootPerHit);
        raider.Carry(stolen);
    }

    public BuildingView WallAt(Vector2Int cell)
    {
        var view = BuildingAt(cell);
        return view != null && view.Definition.IsWall ? view : null;
    }

    /// <summary>
    /// How this wall tile joins its neighbours: N=1, E=2, S=4, W=8, and diagonally NE=16, SE=32,
    /// SW=64, NW=128 (a diagonal only counts when no straight join already connects that way).
    /// </summary>
    public int WallMask(Vector2Int cell)
    {
        if (TradeWorks.RoadMaskFor(this, cell, out int roadMask)) return roadMask;   // roads join roads, rail joins rail
        if (RiverWorks.WallMaskFor(this, cell, out int riverMask)) return riverMask;   // bridges join bridges, walls join walls
        bool n = WallAt(cell + Vector2Int.up) != null, e = WallAt(cell + Vector2Int.right) != null;
        bool sth = WallAt(cell + Vector2Int.down) != null, w = WallAt(cell + Vector2Int.left) != null;
        int mask = (n ? 1 : 0) | (e ? 2 : 0) | (sth ? 4 : 0) | (w ? 8 : 0);
        if (!n && !e && WallAt(cell + new Vector2Int(1, 1)) != null) mask |= 16;
        if (!n && !w && WallAt(cell + new Vector2Int(-1, 1)) != null) mask |= 128;
        if (!sth && !e && WallAt(cell + new Vector2Int(1, -1)) != null) mask |= 32;
        if (!sth && !w && WallAt(cell + new Vector2Int(-1, -1)) != null) mask |= 64;
        return mask;
    }

    /// <summary>Raiders flee straight out of the settlement they hit, to just past its safe zone.</summary>
    public Vector3 ExitPointFor(Vector3 from)
    {
        // Home to the nearest camp, if there is one.
        var camp = NearestCamp(from);
        if (camp != null)
        {
            return BesideCamp(camp);
        }
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
    /// <summary>A live camp within raiding range of one of your towns (picked at random among towns), or null.</summary>
    private CampState CampForRaid()
    {
        var targets = Settlement.SettlementZones().FindAll(z => !z.IsOutpost);
        if (targets.Count == 0)
        {
            return null;
        }
        var target = targets[UnityEngine.Random.Range(0, targets.Count)];
        var camps = Settlement.CampsNear(target);
        return camps.Count > 0 ? camps[0] : null;
    }

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
            var path = MapNavigator.Current?.FindPath(p, zone.centre, Team.Enemy);
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
