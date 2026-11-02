using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// The free mode settlement simulation. Starts the first time the player enters free mode,
/// then runs whenever the game is open, in every scene (it survives scene loads).
/// Scenes only display it and send it commands; all rules live here.
/// </summary>
public class Settlement : MonoBehaviour
{
    public static Settlement Instance { get; private set; }

    public GameDatabase Database { get; private set; }
    public SettlementState State { get; private set; }
    public WaveDirector Waves { get; private set; }

    /// <summary>The generated map (rebuilt from State.mapSeed; never saved itself).</summary>
    public MapData Map { get; private set; }

    /// <summary>True once the player has placed their town center (Campfire).</summary>
    public bool IsFounded => State.buildings.Exists(b => Database.GetBuilding(b.definitionId)?.isTownCenter == true);

    /// <summary>Something structural changed (building placed/finished/removed, research done, new age).</summary>
    public event Action Changed;

    /// <summary>Player-facing messages ("Hut finished", "Raiders stole 30 Wood"...).</summary>
    public event Action<string> Message;

    public Age CurrentAge => Database.GetAge(State.ageIndex);
    public Age NextAge => Database.GetAge(State.ageIndex + 1);
    public int Population => Mathf.FloorToInt(State.population);
    public int Housing { get; private set; }
    public int WorkersNeeded { get; private set; }

    /// <summary>People actually at work (jobs are filled by priority, then in build order).</summary>
    public int WorkersAssigned { get; private set; }

    /// <summary>0 to 1: share of all jobs filled.</summary>
    public float WorkerEfficiency => WorkersNeeded == 0 ? 1f : Mathf.Min(1f, (float)WorkersAssigned / WorkersNeeded);

    // Per building (by instance id), worked out each tick; not saved.
    private readonly Dictionary<int, float> staffing = new Dictionary<int, float>();
    private readonly Dictionary<int, float> inputSupply = new Dictionary<int, float>();
    private readonly Dictionary<int, int> liveTroops = new Dictionary<int, int>();

    /// <summary>0 to 1: share of this building's jobs that are filled (0 when paused).</summary>
    public float StaffingOf(BuildingState building) =>
        building.Paused ? 0f : staffing.TryGetValue(building.instanceId, out float s) ? s : 1f;

    /// <summary>0 to 1: how much of its inputs this building got last tick (1 if it needs none).</summary>
    public float InputSupplyOf(BuildingState building) =>
        inputSupply.TryGetValue(building.instanceId, out float s) ? s : 1f;

    /// <summary>The live scene reports how many troops a building really has (for upkeep).</summary>
    public void ReportLiveTroops(BuildingState building, int count) => liveTroops[building.instanceId] = count;

    /// <summary>Leaving the live scene: upkeep goes back to assuming full garrisons.</summary>
    public void ClearLiveTroops() => liveTroops.Clear();

    /// <summary>True while the army's upkeep can't be paid: troops stop training.</summary>
    public bool UpkeepShort { get; private set; }

    /// <summary>Set by the live scene: true while raiders are about (auto-repair waits).</summary>
    public Func<bool> RaidUnderway;

    /// <summary>How advanced the player is. Drives wave strength.</summary>
    /// <summary>How many current-age enemies' worth the next wave brings (grows with research and survival).</summary>
    public float ThreatLevel => Mathf.Min(Database.maxWaveUnits,
        Database.waveBaseUnits + State.researched.Count * Database.waveUnitsPerTech
        + State.wavesSurvived * Database.waveUnitsPerWaveSurvived);

    private const float AutosaveInterval = 30f;
    private float autosaveTimer;
    private float populationTimer;

    private static string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

    /// <summary>EditorPrefs key: when set, the editor plays on a separate test save.</summary>
    public const string TestSaveKey = "AOS.TestSave";

    public static bool UsingTestSave
    {
        get
        {
#if UNITY_EDITOR
            return UnityEditor.EditorPrefs.GetBool(TestSaveKey, false);
#else
            return false;
#endif
        }
    }

    private string loadedFrom;

    private static string SaveFileName => UsingTestSave ? "settlement_test.json" : "settlement.json";
    public static bool HasSave => File.Exists(SavePath);

    // ---------- Lifetime ----------

    /// <summary>The project has domain reload off, so statics must be cleared at each Play.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    /// <summary>Once free mode has been started, keep it running from game launch.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ResumeIfStarted()
    {
        if (HasSave)
        {
            EnsureStarted();
        }
    }

    /// <summary>Start (or return) the settlement. Called when the player enters free mode.</summary>
    public static Settlement EnsureStarted()
    {
        if (Instance != null)
        {
            return Instance;
        }
        var database = GameDatabase.Load();
        if (database == null)
        {
            Debug.LogError($"Settlement: no GameDatabase found at Resources/{GameDatabase.ResourcePath}.");
            return null;
        }
        var go = new GameObject("Settlement");
        DontDestroyOnLoad(go);
        var settlement = go.AddComponent<Settlement>();
        settlement.Initialise(database);
        settlement.Changed += () => settlement.wallsDirty = true;
        return settlement;
    }

    // ---------- Raider camps ----------

    /// <summary>Raised when raiders move back into a cleared camp.</summary>
    public event Action<CampState> CampReoccupied;

    private float CampHash(Vector2Int zone, int salt)
    {
        unchecked
        {
            int h = State.mapSeed * 73856093 ^ zone.x * 19349663 ^ zone.y * 49979693 ^ salt * 83492791;
            h = (h ^ (h >> 13)) * 1274126177;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
        }
    }

    /// <summary>
    /// Give these explored zones their camps. Each zone's roll comes from the map seed, so the same
    /// zones always hold camps; zones near your buildings never do.
    /// </summary>
    public void RollCamps(IEnumerable<Vector2Int> zones)
    {
        if (!IsFounded || Map == null)
        {
            return; // nothing to raid yet
        }
        var settlements = SettlementZones();
        foreach (var zone in zones)
        {
            if (State.camps.Exists(c => c.zoneXY == zone) || CampHash(zone, 1) >= Database.campChance)
            {
                continue;
            }
            int zx = zone.x, zy = zone.y;
            if (!Map.IsZoneRevealed(zx, zy))
            {
                continue;
            }
            var rect = Map.ZoneRect(zx, zy);
            // Try a few spots in the zone (same ones every time) for open, flat, empty ground.
            for (int attempt = 0; attempt < 24; attempt++)
            {
                int x = rect.x + 1 + (int)(CampHash(zone, 10 + attempt) * Mathf.Max(1, rect.width - CampState.Size - 2));
                int y = rect.y + 1 + (int)(CampHash(zone, 50 + attempt) * Mathf.Max(1, rect.height - CampState.Size - 2));
                var cell = new Vector2Int(x, y);
                var area = new RectInt(cell, Vector2Int.one * CampState.Size);
                if (!CampSiteClear(area))
                {
                    continue;
                }
                Vector2 centre = cell + Vector2.one * (CampState.Size * 0.5f);
                bool tooClose = false;
                foreach (var s in settlements)
                {
                    if (s.DistanceTo(centre) < Mathf.Max(Database.campMinDistance, s.safeDistance)) tooClose = true;
                }
                if (tooClose)
                {
                    break; // this zone is too near home: no camp
                }
                State.camps.Add(new CampState { zoneXY = zone, cell = cell });
                break;
            }
        }
    }

    private bool CampSiteClear(RectInt area)
    {
        for (int y = area.yMin - 1; y <= area.yMax; y++)
        {
            for (int x = area.xMin - 1; x <= area.xMax; x++)
            {
                if (!Map.InBounds(x, y) || Map.GetTerrain(x, y) != TerrainType.Grass || Map.GetDeposit(x, y) != null)
                {
                    return false;
                }
            }
        }
        foreach (var building in State.buildings)
        {
            var def = Database.GetBuilding(building.definitionId);
            if (def != null && new RectInt(building.cell, def.size).Overlaps(area)) return false;
        }
        return true;
    }

    /// <summary>A live (not cleared) camp overlapping this area, if any.</summary>
    public CampState CampAt(RectInt area)
    {
        foreach (var camp in State.camps)
        {
            if (!camp.cleared && new RectInt(camp.cell, Vector2Int.one * CampState.Size).Overlaps(area)) return camp;
        }
        return null;
    }

    /// <summary>
    /// How tough a camp is, as an age index: further from your town is tougher, and each
    /// reoccupation adds one, but never more than one age ahead of you.
    /// </summary>
    public int CampTier(CampState camp)
    {
        float best = float.MaxValue;
        foreach (var s in SettlementZones())
        {
            if (!s.IsOutpost) best = Mathf.Min(best, s.DistanceTo(camp.Centre));
        }
        int byDistance = best == float.MaxValue ? 0 : Mathf.FloorToInt(Mathf.Max(0, best - Database.campMinDistance) / Mathf.Max(1f, Database.campTierDistance));
        return Mathf.Clamp(byDistance + camp.extraTier, 0, Mathf.Min(Database.ages.Count - 1, State.ageIndex + 1));
    }

    /// <summary>A camp was destroyed: hand out its loot, put raids back a little, and start its comeback timer.</summary>
    public List<ResourceAmount> ClearCamp(CampState camp)
    {
        var loot = new List<ResourceAmount>();
        if (camp == null || camp.cleared)
        {
            return loot;
        }
        int t = CampTier(camp);
        void Add(string id, int amount)
        {
            var res = Database.resources.Find(r => r != null && r.id == id);
            if (res != null && amount > 0) loot.Add(new ResourceAmount { resource = res, amount = amount });
        }
        Add("Food", 100 * (t + 1));
        Add("Wood", 100 * (t + 1));
        Add("Stone", 60 * (t + 1));
        if (t >= 1) Add("Bronze", 30 * t);
        if (t >= 2) Add("Iron", 25 * t);
        if (t >= 3) Add("Gold", 30);
        foreach (var item in loot) AddResource(item.resource, item.amount);
        camp.cleared = true;
        camp.reoccupyIn = Database.campReoccupySeconds;
        State.secondsUntilWave += 90f;
        Notify($"Raider camp destroyed! You took {Describe(loot)}, and raids will hold off a while.");
        Changed?.Invoke();
        return loot;
    }

    private void TickCamps(float dt)
    {
        foreach (var camp in State.camps)
        {
            if (!camp.cleared) continue;
            camp.reoccupyIn -= dt;
            if (camp.reoccupyIn <= 0)
            {
                camp.cleared = false;
                camp.extraTier++;
                Notify("Raiders have moved back into one of the camps you cleared, stronger than before.");
                CampReoccupied?.Invoke(camp);
                Changed?.Invoke();
            }
        }
    }

    /// <summary>Live camps within raiding range of a settlement, nearest first.</summary>
    public List<CampState> CampsNear(SettlementZone settlement)
    {
        var list = State.camps.FindAll(c => !c.cleared && settlement.DistanceTo(c.Centre) <= Database.campRaidRange);
        list.Sort((a, b) => settlement.DistanceTo(a.Centre).CompareTo(settlement.DistanceTo(b.Centre)));
        return list;
    }

    // ---------- Walls ----------

    private bool wallsDirty = true;

    /// <summary>Put every standing, finished wall and gate on the map so units path around them.</summary>
    private void UpdateWallLayer()
    {
        if (!wallsDirty || Map == null)
        {
            return;
        }
        wallsDirty = false;
        var pieces = new List<(Vector2Int, WallTile)>();
        foreach (var building in State.buildings)
        {
            var def = Database.GetBuilding(building.definitionId);
            if (def != null && def.IsWall && IsWorking(building, def))
            {
                pieces.Add((building.cell, def.wallKind));
            }
        }
        Map.SetWalls(pieces);
    }

    private void Initialise(GameDatabase database)
    {
        Instance = this;
        Database = database;
        // Save back to the file we loaded from, even if the test-save switch changes meanwhile.
        loadedFrom = SavePath;
        if (UsingTestSave)
        {
            Debug.Log("[Settlement] Playing on the TEST save (settlement_test.json). Your real save is untouched.");
        }
        State = LoadState() ?? NewState();
        if (State.mapSeed == 0)
        {
            State.mapSeed = NewSeed();
        }
        UpgradeOldSave();
        Map = MapGenerator.Create(State.mapSeed, Database, State.explored);
        State.explored = Map.RevealedZones();
        // Older saves could hold fractional building health; snap it to whole points.
        foreach (var building in State.buildings)
        {
            building.health = Mathf.Round(building.health);
        }
        RecalculateDerived();
        RepairUnderfilledHealth();
        Waves = gameObject.AddComponent<WaveDirector>();
        Waves.Initialise(this);
        RollCamps(Map.RevealedZones());
    }

    /// <summary>The save format this build writes.</summary>
    private const int CurrentSaveVersion = 1;

    /// <summary>
    /// Bring an older save up to date. Version 0 stored zones as a single number from when the
    /// map had an edge (row * zones-per-row + column); now they're (column, row) so the map can
    /// grow forever. Every building, explored zone and camp stays exactly where it was.
    /// </summary>
    private void UpgradeOldSave()
    {
        if (State.saveVersion >= CurrentSaveVersion)
        {
            return;
        }
        int perRow = MapGenerator.LegacyZonesPerRow(Database);
        foreach (int index in State.exploredZones)
        {
            var zone = new Vector2Int(index % perRow, index / perRow);
            if (!State.explored.Contains(zone)) State.explored.Add(zone);
        }
        State.exploredZones.Clear();
        int size = Mathf.Max(4, Database.chunkSize);
        foreach (var camp in State.camps)
        {
            camp.zoneXY = new Vector2Int(MapData.FloorDiv(camp.cell.x, size), MapData.FloorDiv(camp.cell.y, size));
        }
        State.saveVersion = CurrentSaveVersion;
    }

    private SettlementState NewState()
    {
        var state = new SettlementState
        {
            population = Database.startingPopulation,
            secondsUntilWave = Database.firstWaveDelay,
            mapSeed = NewSeed(),
            saveVersion = CurrentSaveVersion
        };
        foreach (var start in Database.startingResources)
        {
            if (start.resource != null)
            {
                state.resources.Add(new ResourceStock { id = start.resource.id, amount = start.amount });
            }
        }
        return state;
    }

    private static int NewSeed() => UnityEngine.Random.Range(1, int.MaxValue);

    /// <summary>Raised with the tile areas just generated when the map grows.</summary>
    public event Action<List<RectInt>> MapGrown;

    /// <summary>
    /// Explore new zones (ones bordering explored land; the map has no edge).
    /// Returns how many were added.
    /// </summary>
    public int ExploreZones(IEnumerable<Vector2Int> zones)
    {
        var shown = MapGenerator.RevealZones(Map, zones);
        if (shown.Count == 0)
        {
            return 0;
        }
        var newZones = new List<Vector2Int>();
        foreach (var rect in shown)
        {
            var zone = Map.ZoneOf(rect.x, rect.y);
            newZones.Add(zone);
            State.explored.Add(zone);
        }
        RollCamps(newZones);
        MapGrown?.Invoke(shown);
        return shown.Count;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Save();
            Instance = null;
        }
    }

    private void OnApplicationQuit() => Save();

    private void OnApplicationPause(bool paused)
    {
        if (paused)
        {
            Save();
        }
    }

    // ---------- Simulation ----------

    private void Update()
    {
        Tick(Time.deltaTime);

        autosaveTimer += Time.deltaTime;
        if (autosaveTimer >= AutosaveInterval)
        {
            autosaveTimer = 0;
            Save();
        }
    }

    public void Tick(float dt)
    {
        UpdateWallLayer();
        if (!IsFounded)
        {
            return; // nothing happens until the player picks a spot
        }
        TickConstruction(dt);
        TickUpgrades(dt);
        RecalculateDerived();
        TickProduction(dt);
        TickUpkeep(dt);
        TickPopulation(dt);
        TickResearch(dt);
        TickAutomation(dt);
        TickCamps(dt);
    }

    // ---------- Building stats and upgrades ----------

    /// <summary>A building's effective numbers: its type's base values plus every finished upgrade.</summary>
    public struct BuildingStats
    {
        public int maxHealth;
        public int housing;
        public int workers;
        public int maxUnits;
        public float towerRange;
        public int towerDamage;
        public int catchment;
        public float defense;
        public float output;
        public UnitStatModifier trainedUnits;
    }

    public BuildingStats StatsOf(BuildingState building)
    {
        var def = Database.GetBuilding(building.definitionId);
        return StatsOf(def, building.upgradeLevel);
    }

    /// <summary>Global building health bonus from research (all buildings).</summary>
    public float BuildingHealthMultiplier
    {
        get
        {
            float m = 1f;
            foreach (var id in State.researched)
            {
                var tech = Database.GetTechnology(id);
                if (tech != null)
                {
                    m *= tech.buildingHealthMultiplier;
                }
            }
            return m;
        }
    }

    /// <summary>
    /// Older saves placed buildings with their base health, ignoring research bonuses, so they
    /// never showed full. A building sitting at exactly that research-less maximum gets topped up.
    /// </summary>
    private void RepairUnderfilledHealth()
    {
        foreach (var building in State.buildings)
        {
            var def = Database.GetBuilding(building.definitionId);
            if (def == null || building.health <= 0)
            {
                continue;
            }
            int full = StatsOf(def, building.upgradeLevel).maxHealth;
            int withoutResearch = full - (Mathf.RoundToInt(def.maxHealth * BuildingHealthMultiplier) - def.maxHealth);
            if (withoutResearch < full && Mathf.Approximately(building.health, withoutResearch))
            {
                building.health = full;
            }
        }
    }

    public static BuildingStats StatsOf(BuildingDefinition def, int level)
    {
        float researchHealth = Instance != null && Instance.State != null ? Instance.BuildingHealthMultiplier : 1f;
        var s = new BuildingStats
        {
            maxHealth = Mathf.RoundToInt(def.maxHealth * researchHealth),
            housing = def.housing,
            workers = def.workers,
            maxUnits = def.maxUnits,
            towerRange = def.towerRange,
            towerDamage = def.towerDamage,
            catchment = def.catchmentRadius,
            defense = def.defensePower,
            output = 1f,
            trainedUnits = new UnitStatModifier()
        };
        for (int i = 0; i < level && i < def.upgrades.Count; i++)
        {
            var u = def.upgrades[i];
            s.maxHealth += u.extraHealth;
            s.housing += u.extraHousing;
            s.maxUnits += u.extraUnits;
            s.towerRange += u.extraTowerRange;
            s.towerDamage += u.extraTowerDamage;
            s.catchment += u.extraCatchment;
            s.defense += u.extraDefense;
            s.output *= u.productionMultiplier;
            if (u.trainedUnits != null)
            {
                s.trainedUnits.healthMultiplier *= u.trainedUnits.healthMultiplier;
                s.trainedUnits.attackMultiplier *= u.trainedUnits.attackMultiplier;
                s.trainedUnits.speedMultiplier *= u.trainedUnits.speedMultiplier;
                s.trainedUnits.cooldownMultiplier *= u.trainedUnits.cooldownMultiplier;
                s.trainedUnits.rangeMultiplier *= u.trainedUnits.rangeMultiplier;
            }
        }
        return s;
    }

    public BuildingUpgrade NextUpgrade(BuildingState building)
    {
        var def = Database.GetBuilding(building.definitionId);
        return def != null && building.upgradeLevel < def.upgrades.Count ? def.upgrades[building.upgradeLevel] : null;
    }

    public bool CanUpgrade(BuildingState building, out string missing)
    {
        var next = NextUpgrade(building);
        if (next == null)
        {
            missing = "Fully upgraded";
            return false;
        }
        if (!building.IsBuilt)
        {
            missing = "Finish building it first";
            return false;
        }
        if (building.health <= 0)
        {
            missing = "Repair it first";
            return false;
        }
        if (building.upgrading)
        {
            missing = "Already upgrading";
            return false;
        }
        return Meets(next.requirements, out missing);
    }

    public bool TryStartUpgrade(BuildingState building, out string reason)
    {
        if (!CanUpgrade(building, out reason))
        {
            return false;
        }
        Spend(NextUpgrade(building).requirements.cost);
        building.upgrading = true;
        building.upgradeProgress = 0;
        Changed?.Invoke();
        return true;
    }

    // ---------- Rank up: a fully upgraded building becomes the next age's version, in place ----------

    /// <summary>What this building can rank up into, once fully upgraded (null if nothing).</summary>
    public BuildingDefinition RankUpTarget(BuildingState building)
    {
        var def = Database.GetBuilding(building.definitionId);
        return def != null ? def.rankUpTo : null;
    }

    public bool CanRankUp(BuildingState building, out string missing)
    {
        var def = Database.GetBuilding(building.definitionId);
        var target = def != null ? def.rankUpTo : null;
        if (target == null)
        {
            missing = "Can't rank up";
            return false;
        }
        if (building.upgradeLevel < def.upgrades.Count)
        {
            missing = "Finish its upgrades first";
            return false;
        }
        if (!building.IsBuilt)
        {
            missing = "Finish building it first";
            return false;
        }
        if (building.health <= 0)
        {
            missing = "Repair it first";
            return false;
        }
        if (building.upgrading)
        {
            missing = building.rankingUp ? "Already ranking up" : "Already upgrading";
            return false;
        }
        // Ranking up rebuilds in place, so the new building must fit on the old one's spot
        // (it can be smaller: e.g. a 3x3 Garrison becomes a 3x2 Legion Fort).
        if (target.size.x > def.size.x || target.size.y > def.size.y)
        {
            missing = $"{Name(target)} is too big to fit on this building's spot";
            return false;
        }
        if (!IsAgeUnlocked(target))
        {
            missing = $"Reach the {target.age.displayName}";
            return false;
        }
        return Meets(def.rankUpRequirements, out missing);
    }

    public bool TryStartRankUp(BuildingState building, out string reason)
    {
        if (!CanRankUp(building, out reason))
        {
            return false;
        }
        Spend(Database.GetBuilding(building.definitionId).rankUpRequirements.cost);
        building.upgrading = true;
        building.rankingUp = true;
        building.upgradeProgress = 0;
        Changed?.Invoke();
        return true;
    }

    private void TickRankUp(BuildingState building, float dt)
    {
        var def = Database.GetBuilding(building.definitionId);
        var target = def != null ? def.rankUpTo : null;
        if (target == null)
        {
            building.upgrading = building.rankingUp = false;
            return;
        }
        building.upgradeProgress += def.rankUpTime > 0 ? dt / def.rankUpTime : 1f;
        if (building.upgradeProgress < 1f)
        {
            return;
        }
        // Keep the same share of health; the new building starts its own upgrade track from scratch.
        float healthShare = Mathf.Clamp01(building.health / Mathf.Max(1, StatsOf(building).maxHealth));
        building.upgrading = building.rankingUp = false;
        building.upgradeProgress = 0;
        building.definitionId = target.id;
        building.upgradeLevel = 0;
        var stats = StatsOf(building);
        building.health = Mathf.Max(1, Mathf.Round(healthShare * stats.maxHealth));
        building.siteQuality = SiteQuality(target, new RectInt(building.cell, target.size), stats.catchment);
        Notify($"{Name(def)} ranked up into a {Name(target)}!");
        Changed?.Invoke();
    }

    private void TickUpgrades(float dt)
    {
        foreach (var building in State.buildings)
        {
            if (!building.upgrading)
            {
                continue;
            }
            if (building.rankingUp)
            {
                TickRankUp(building, dt);
                continue;
            }
            var next = NextUpgrade(building);
            if (next == null)
            {
                building.upgrading = false;
                continue;
            }
            building.upgradeProgress += next.upgradeTime > 0 ? dt / next.upgradeTime : 1f;
            if (building.upgradeProgress < 1f)
            {
                continue;
            }
            building.upgrading = false;
            building.upgradeProgress = 0;
            building.upgradeLevel++;
            building.health += next.extraHealth;
            var def = Database.GetBuilding(building.definitionId);
            if (next.extraCatchment != 0)
            {
                // A bigger reach can mean more deposit tiles: re-score the site.
                building.siteQuality = SiteQuality(def, new RectInt(building.cell, def.size), StatsOf(building).catchment);
            }
            Notify($"{Name(def)} upgraded: {next.name}.");
            Changed?.Invoke();
        }
    }

    private void TickConstruction(float dt)
    {
        foreach (var building in State.buildings)
        {
            if (building.IsBuilt)
            {
                continue;
            }
            var def = Database.GetBuilding(building.definitionId);
            building.buildProgress += def != null && def.buildTime > 0 ? dt / def.buildTime : 1f;
            if (building.IsBuilt)
            {
                building.buildProgress = 1f;
                if (def == null || !def.IsWall)
                {
                    Notify($"{Name(def)} finished building."); // walls go up in dozens: no message each
                }
                Changed?.Invoke();
            }
        }
    }

    private void TickProduction(float dt)
    {
        foreach (var building in State.buildings)
        {
            var def = Database.GetBuilding(building.definitionId);
            if (def == null || !IsWorking(building, def) || def.production.Count == 0)
            {
                continue;
            }
            float staff = StaffingOf(building);
            if (staff <= 0)
            {
                continue;
            }
            var stats = StatsOf(def, building.upgradeLevel);
            float activity = staff * (building.health / stats.maxHealth) * stats.output;

            // Inputs: take what's needed; short of any one, the whole building slows to match.
            float run = 1f;
            if (def.inputs.Count > 0)
            {
                float supply = 1f;
                foreach (var input in def.inputs)
                {
                    float need = input.perSecond * dt * activity;
                    if (input.resource != null && need > 0)
                    {
                        supply = Mathf.Min(supply, GetAmount(input.resource) / need);
                    }
                }
                supply = Mathf.Clamp01(supply);
                foreach (var input in def.inputs)
                {
                    if (input.resource != null)
                    {
                        AddResource(input.resource, -input.perSecond * dt * activity * supply);
                    }
                }
                inputSupply[building.instanceId] = supply;
                run = Mathf.Lerp(def.runsWithoutInputs, 1f, supply);
            }

            foreach (var rate in def.production)
            {
                if (rate.resource != null)
                {
                    float multiplier = ProductionMultiplier(rate.resource) * building.siteQuality;
                    AddResource(rate.resource, rate.perSecond * dt * activity * run * multiplier);
                }
            }
        }

        // Taxes: every person pays a little Coin once the right research is done.
        float tax = TaxPerPersonPerSecond;
        if (tax > 0 && Database.coin != null)
        {
            AddResource(Database.coin, Population * tax * dt);
        }
    }

    /// <summary>Coin each person pays per second: basic taxes once Coin is unlocked, plus research.</summary>
    public float TaxPerPersonPerSecond
    {
        get
        {
            var coin = Database.coin;
            bool unlocked = coin != null && (coin.unlockedIn == null || coin.unlockedIn.order <= State.ageIndex);
            if (!unlocked)
            {
                return 0f;
            }
            float tax = Database.taxPerPersonPerSecond;
            foreach (var id in State.researched)
            {
                var tech = Database.GetTechnology(id);
                if (tech != null) tax += tech.coinPerPersonPerSecond;
            }
            return tax;
        }
    }

    /// <summary>How many troops a working troop building has (live count if known, else a full garrison).</summary>
    public int TroopsOf(BuildingState building, BuildingDefinition def) =>
        liveTroops.TryGetValue(building.instanceId, out int n) ? n : StatsOf(def, building.upgradeLevel).maxUnits;

    /// <summary>The army's upkeep per second, by resource.</summary>
    public Dictionary<ResourceType, float> UpkeepPerSecond(out int troops)
    {
        troops = 0;
        var due = new Dictionary<ResourceType, float>();
        foreach (var building in State.buildings)
        {
            var def = Database.GetBuilding(building.definitionId);
            if (def == null || def.trainsUnit == null || !building.IsBuilt)
            {
                continue;
            }
            int count = TroopsOf(building, def);
            troops += count;
            foreach (var rate in def.trainsUnit.upkeep)
            {
                if (rate.resource == null) continue;
                due.TryGetValue(rate.resource, out float sum);
                due[rate.resource] = sum + rate.perSecond * count;
            }
        }
        return due;
    }

    private void TickUpkeep(float dt)
    {
        bool shortNow = false;
        foreach (var pair in UpkeepPerSecond(out _))
        {
            float amount = pair.Value * dt;
            float have = GetAmount(pair.Key);
            if (have < amount) shortNow = true;
            AddResource(pair.Key, -Mathf.Min(have, amount));
        }
        if (shortNow && !UpkeepShort)
        {
            Notify("You can't pay your army's upkeep. Troops stop training until you can.");
        }
        UpkeepShort = shortNow;
    }

    // ---------- Automation ----------

    private float automationTimer;

    /// <summary>Once a second: auto-repair and queued upgrades.</summary>
    private void TickAutomation(float dt)
    {
        automationTimer += dt;
        if (automationTimer < 1f)
        {
            return;
        }
        automationTimer = 0f;
        bool raid = RaidUnderway != null && RaidUnderway();
        foreach (var building in State.buildings.ToArray())
        {
            if (!building.IsBuilt)
            {
                continue;
            }
            if (building.autoRepair && !raid && building.health < StatsOf(building).maxHealth && CanAfford(RepairCost(building)))
            {
                TryRepair(building, out _);
            }
            if (building.queueUpgrades && !building.upgrading)
            {
                if (NextUpgrade(building) != null)
                {
                    if (CanUpgrade(building, out _)) TryStartUpgrade(building, out _);
                }
                else if (RankUpTarget(building) != null)
                {
                    if (CanRankUp(building, out _)) TryStartRankUp(building, out _);
                }
                else
                {
                    building.queueUpgrades = false; // nothing left to do
                }
            }
        }
    }

    private void TickPopulation(float dt)
    {
        var food = Database.food;
        if (food == null)
        {
            return;
        }

        float upkeep = State.population * Database.foodPerPersonPerSecond * dt;
        float available = GetAmount(food);
        bool starving = available < upkeep;
        AddResource(food, -Mathf.Min(upkeep, available));

        if (starving)
        {
            // Without food people slowly leave.
            populationTimer += dt;
            if (populationTimer >= Database.secondsPerNewPerson && State.population >= 1)
            {
                populationTimer = 0;
                State.population -= 1;
                Notify("Your people are starving. Someone has left.");
            }
        }
        else if (State.population < Housing)
        {
            populationTimer += dt;
            if (populationTimer >= Database.secondsPerNewPerson)
            {
                populationTimer = 0;
                State.population = Mathf.Min(Housing, State.population + 1);
            }
        }
        else
        {
            populationTimer = 0;
        }
    }

    private void TickResearch(float dt)
    {
        if (string.IsNullOrEmpty(State.researchingId))
        {
            return;
        }
        var tech = Database.GetTechnology(State.researchingId);
        if (tech == null)
        {
            State.researchingId = null;
            return;
        }
        State.researchProgress += tech.researchTime > 0 ? dt / tech.researchTime : 1f;
        if (State.researchProgress >= 1f)
        {
            State.researched.Add(tech.id);
            if (tech.buildingHealthMultiplier != 1f)
            {
                // Health research raises current health too, so buildings don't suddenly look damaged.
                foreach (var building in State.buildings)
                {
                    if (building.health > 0)
                    {
                        building.health = Mathf.Min(building.health * tech.buildingHealthMultiplier, StatsOf(building).maxHealth);
                    }
                }
            }
            State.researchingId = null;
            State.researchProgress = 0;
            Notify($"Research complete: {tech.displayName}.");
            Changed?.Invoke();
        }
    }

    // ---------- Settlements (groups of buildings) and their raider-free zones ----------

    /// <summary>
    /// One settlement: a group of buildings standing close together. Its raider-free zone follows
    /// the shape of the group (everywhere within safeDistance of one of its buildings), so a long
    /// town along a river gets a long zone rather than one huge circle.
    /// </summary>
    public class SettlementZone
    {
        /// <summary>Footprints of the buildings in this settlement, in tiles.</summary>
        public readonly List<Rect> buildings = new List<Rect>();
        public Rect bounds;
        public Vector2 centre;
        public float safeDistance;
        public int housingBuildings;

        /// <summary>
        /// A group with no housing and no town centre (a lone lumber camp, say): it keeps raiders
        /// from spawning on top of it, but its zone is small and raids never set out for it.
        /// </summary>
        public bool IsOutpost;

        /// <summary>Distance from 'point' to the nearest building of this settlement.</summary>
        public float DistanceTo(Vector2 point)
        {
            float best = float.MaxValue;
            foreach (var r in buildings)
            {
                float dx = Mathf.Max(r.xMin - point.x, 0, point.x - r.xMax);
                float dy = Mathf.Max(r.yMin - point.y, 0, point.y - r.yMax);
                best = Mathf.Min(best, dx * dx + dy * dy);
            }
            return Mathf.Sqrt(best);
        }

        /// <summary>How far outside the zone 'point' is (negative inside).</summary>
        public float Clearance(Vector2 point) => DistanceTo(point) - safeDistance;

        public bool Contains(Vector2 point) => Clearance(point) < 0;
    }

    /// <summary>
    /// Every settlement. Buildings belong together when the gap between their footprints is at
    /// most settlementMergeDistance, so farms, camps and walls join the town they work for. A
    /// group with housing or the town centre is a settlement; its zone reaches settlementSafeRadius
    /// past its buildings, a little more as it grows. Any other group is an outpost.
    /// </summary>
    public List<SettlementZone> SettlementZones()
    {
        var rects = new List<Rect>();
        var homes = new List<bool>();
        var cores = new List<bool>();
        foreach (var building in State.buildings)
        {
            var def = Database.GetBuilding(building.definitionId);
            if (def == null || def.IsWall)
            {
                continue; // walls don't make a settlement bigger
            }
            rects.Add(new Rect(building.cell, def.size));
            bool home = StatsOf(def, building.upgradeLevel).housing > 0;
            homes.Add(home);
            cores.Add(home || def.isTownCenter);
        }

        var zones = new List<SettlementZone>();
        var grouped = new bool[rects.Count];
        float merge = Database.settlementMergeDistance;
        for (int i = 0; i < rects.Count; i++)
        {
            if (grouped[i])
            {
                continue;
            }
            // Flood out from this building to every building within merge distance of the group.
            var zone = new SettlementZone { IsOutpost = true };
            var queue = new Queue<int>();
            queue.Enqueue(i);
            grouped[i] = true;
            while (queue.Count > 0)
            {
                int k = queue.Dequeue();
                zone.buildings.Add(rects[k]);
                if (homes[k]) zone.housingBuildings++;
                if (cores[k]) zone.IsOutpost = false;
                for (int j = 0; j < rects.Count; j++)
                {
                    if (!grouped[j] && Gap(rects[j], rects[k]) <= merge)
                    {
                        grouped[j] = true;
                        queue.Enqueue(j);
                    }
                }
            }

            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            var centre = Vector2.zero;
            foreach (var r in zone.buildings)
            {
                min = Vector2.Min(min, r.min);
                max = Vector2.Max(max, r.max);
                centre += r.center;
            }
            zone.bounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            zone.centre = centre / zone.buildings.Count;
            zone.safeDistance = zone.IsOutpost
                ? Database.outpostSafeRadius
                : Database.settlementSafeRadius + Mathf.Min(Database.settlementSafeRadiusGrowthMax,
                    Database.settlementSafeRadiusPerBuilding * zone.buildings.Count);
            zones.Add(zone);
        }
        return zones;
    }

    /// <summary>Gap between two footprints (0 when they touch or overlap).</summary>
    private static float Gap(Rect a, Rect b)
    {
        float dx = Mathf.Max(0, Mathf.Max(a.xMin - b.xMax, b.xMin - a.xMax));
        float dy = Mathf.Max(0, Mathf.Max(a.yMin - b.yMax, b.yMin - a.yMax));
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>The settlement 'point' is in or nearest to (null when there are none).</summary>
    public static SettlementZone NearestZone(List<SettlementZone> zones, Vector2 point, bool settlementsOnly = false)
    {
        SettlementZone best = null;
        float bestClearance = float.MaxValue;
        foreach (var zone in zones)
        {
            if (settlementsOnly && zone.IsOutpost)
            {
                continue;
            }
            float c = zone.Clearance(point);
            if (c < bestClearance)
            {
                bestClearance = c;
                best = zone;
            }
        }
        return best;
    }

    public static bool InAnyZone(List<SettlementZone> zones, Vector2 point)
    {
        foreach (var zone in zones)
        {
            if (zone.Contains(point))
            {
                return true;
            }
        }
        return false;
    }

    private void RecalculateDerived()
    {
        int housing = 0;
        int workers = 0;
        var jobs = new List<(BuildingState building, int workers, int order)>();
        for (int i = 0; i < State.buildings.Count; i++)
        {
            var building = State.buildings[i];
            var def = Database.GetBuilding(building.definitionId);
            if (def == null || !IsWorking(building, def))
            {
                continue;
            }
            var stats = StatsOf(def, building.upgradeLevel);
            housing += stats.housing;
            if (building.Paused)
            {
                staffing[building.instanceId] = 0f;
                continue;
            }
            if (stats.workers <= 0)
            {
                staffing[building.instanceId] = 1f;
                continue;
            }
            workers += stats.workers;
            jobs.Add((building, stats.workers, i));
        }
        Housing = housing;
        WorkersNeeded = workers;

        // Fill jobs: high priority first, then in the order buildings were placed.
        jobs.Sort((a, b) => a.building.priority != b.building.priority
            ? b.building.priority.CompareTo(a.building.priority)
            : a.order.CompareTo(b.order));
        int pool = Population, assigned = 0;
        foreach (var job in jobs)
        {
            int take = Mathf.Min(job.workers, pool);
            pool -= take;
            assigned += take;
            staffing[job.building.instanceId] = take / (float)job.workers;
        }
        WorkersAssigned = assigned;
    }

    /// <summary>Finished and not ruined.</summary>
    public bool IsWorking(BuildingState building, BuildingDefinition def) =>
        building.IsBuilt && building.health > 0 && def != null;

    // ---------- Resources ----------

    public float GetAmount(ResourceType resource)
    {
        var stock = State.resources.Find(s => s.id == resource.id);
        return stock != null ? stock.amount : 0;
    }

    public void AddResource(ResourceType resource, float amount)
    {
        var stock = State.resources.Find(s => s.id == resource.id);
        if (stock == null)
        {
            stock = new ResourceStock { id = resource.id };
            State.resources.Add(stock);
        }
        stock.amount = Mathf.Max(0, stock.amount + amount);
    }

    /// <summary>Several costs added together (for group actions).</summary>
    public static List<ResourceAmount> SumCost(IEnumerable<List<ResourceAmount>> costs)
    {
        var total = new List<ResourceAmount>();
        foreach (var cost in costs)
        {
            if (cost == null) continue;
            foreach (var item in cost)
            {
                if (item.resource == null || item.amount <= 0) continue;
                int i = total.FindIndex(t => t.resource == item.resource);
                if (i < 0) total.Add(item);
                else total[i] = new ResourceAmount { resource = item.resource, amount = total[i].amount + item.amount };
            }
        }
        return total;
    }

    public bool CanAfford(List<ResourceAmount> cost)
    {
        foreach (var item in cost)
        {
            if (item.resource != null && GetAmount(item.resource) < item.amount)
            {
                return false;
            }
        }
        return true;
    }

    private void Spend(List<ResourceAmount> cost)
    {
        foreach (var item in cost)
        {
            if (item.resource != null)
            {
                AddResource(item.resource, -item.amount);
            }
        }
    }

    // ---------- Requirements ----------

    public bool HasResearched(Technology tech) => tech != null && State.researched.Contains(tech.id);

    public int CountBuilt(BuildingDefinition def) =>
        State.buildings.FindAll(b => b.definitionId == def.id && b.IsBuilt).Count;

    public int CountAll(BuildingDefinition def) => State.buildings.FindAll(b => b.definitionId == def.id).Count;

    /// <summary>True if every condition and the cost are met. Otherwise 'missing' lists what isn't.</summary>
    public bool Meets(Requirements req, out string missing)
    {
        var problems = new StringBuilder();
        if (req != null)
        {
            if (req.minimumAge != null && req.minimumAge.order > State.ageIndex)
            {
                problems.AppendLine($"Reach the {req.minimumAge.displayName}");
            }
            foreach (var tech in req.technologies)
            {
                if (tech != null && !HasResearched(tech))
                {
                    problems.AppendLine($"Research {tech.displayName}");
                }
            }
            foreach (var need in req.buildings)
            {
                if (need.building != null && CountBuilt(need.building) < need.count)
                {
                    problems.AppendLine($"Build {need.count} {need.building.displayName} ({CountBuilt(need.building)}/{need.count})");
                }
            }
            if (Population < req.population)
            {
                problems.AppendLine($"Population {req.population} ({Population}/{req.population})");
            }
            foreach (var item in req.cost)
            {
                if (item.resource != null && GetAmount(item.resource) < item.amount)
                {
                    problems.AppendLine(
                        $"{item.amount} {item.resource.displayName} ({Mathf.FloorToInt(GetAmount(item.resource))}/{item.amount})"
                    );
                }
            }
        }
        missing = problems.ToString().Replace("\r", "").TrimEnd();
        return missing.Length == 0;
    }

    // ---------- Buildings ----------

    /// <summary>
    /// Can 'def' go at 'cell'? Checks terrain, overlap and the deposits it needs (OpenTTD-style).
    /// 'quality' is the output multiplier the site would give (deposit coverage + water bonus).
    /// </summary>
    public bool CheckSite(BuildingDefinition def, Vector2Int cell, out float quality, out string reason)
    {
        quality = 1f;
        var area = new RectInt(cell, def.size);
        if (!Map.CanBuildOn(area, def.canBuildOnHills, out reason))
        {
            return false;
        }
        if (!IsAreaFree(def, cell))
        {
            reason = "Something is already built there";
            return false;
        }
        if (CampAt(area) != null)
        {
            reason = "There's a raider camp here: clear it first";
            return false;
        }
        if (def.requiredDeposit != null)
        {
            int count = Map.CountDeposit(def.requiredDeposit, area, def.catchmentRadius);
            if (count < def.minDepositTiles)
            {
                reason = $"Needs {def.requiredDeposit.displayName} nearby ({count}/{def.minDepositTiles} tiles)";
                return false;
            }
        }
        quality = SiteQuality(def, area, def.catchmentRadius);
        reason = null;
        return true;
    }

    /// <summary>Output multiplier from deposit tiles within 'radius' plus any water bonus.</summary>
    public float SiteQuality(BuildingDefinition def, RectInt area, int radius)
    {
        float quality = 1f;
        if (def.requiredDeposit != null)
        {
            int count = Map.CountDeposit(def.requiredDeposit, area, radius);
            quality = Mathf.Clamp01(count / (float)Mathf.Max(1, def.fullOutputTiles));
        }
        if (def.waterBonus > 0 && Map.AnyWater(area, radius))
        {
            quality *= 1f + def.waterBonus;
        }
        return quality;
    }

    public bool IsAreaFree(BuildingDefinition def, Vector2Int cell, int ignoreInstanceId = 0)
    {
        var area = new RectInt(cell, def.size);
        foreach (var other in State.buildings)
        {
            if (other.instanceId == ignoreInstanceId)
            {
                continue;
            }
            var otherDef = Database.GetBuilding(other.definitionId);
            if (otherDef != null && area.Overlaps(new RectInt(other.cell, otherDef.size)))
            {
                return false;
            }
        }
        return true;
    }

    public bool CanBuild(BuildingDefinition def, out string missing)
    {
        if (!def.isTownCenter && !IsFounded)
        {
            missing = "Found your settlement first: place a Campfire";
            return false;
        }
        if (!IsAgeUnlocked(def))
        {
            missing = $"Reach the {def.age.displayName}";
            return false;
        }
        if (def.maxCount > 0 && CountAll(def) >= def.maxCount)
        {
            missing = $"You can only have {def.maxCount} {def.displayName}";
            return false;
        }
        return Meets(def.requirements, out missing);
    }

    /// <summary>Each age unlocks its buildings; earlier ages' buildings stay available.</summary>
    public bool IsAgeUnlocked(BuildingDefinition def) => def.age == null || def.age.order <= State.ageIndex;

    public bool TryPlaceBuilding(BuildingDefinition def, Vector2Int cell, out BuildingState placed, out string reason)
    {
        placed = null;
        if (!CanBuild(def, out reason))
        {
            return false;
        }
        if (!CheckSite(def, cell, out float quality, out reason))
        {
            return false;
        }
        bool founding = def.isTownCenter && !IsFounded;
        Spend(def.requirements.cost);
        placed = new BuildingState
        {
            instanceId = State.nextBuildingId++,
            definitionId = def.id,
            cell = cell,
            health = StatsOf(def, 0).maxHealth,   // includes research bonuses, so it starts full
            buildProgress = def.buildTime > 0 ? 0 : 1,
            siteQuality = quality,
            autoRepair = State.autoRepairNew
        };
        State.buildings.Add(placed);
        if (founding)
        {
            // Raiders only start coming once there's something to raid.
            State.secondsUntilWave = Database.firstWaveDelay;
            Notify("Your settlement is founded! Build huts and camps near food and wood.");
            RollCamps(Map.RevealedZones());
        }
        Changed?.Invoke();
        return true;
    }

    public void Demolish(BuildingState building)
    {
        if (State.buildings.Remove(building))
        {
            Changed?.Invoke();
        }
    }

    public List<ResourceAmount> RepairCost(BuildingState building)
    {
        var cost = new List<ResourceAmount>();
        var def = Database.GetBuilding(building.definitionId);
        if (def == null)
        {
            return cost;
        }
        float damage = 1f - building.health / StatsOf(building).maxHealth;
        foreach (var item in def.requirements.cost)
        {
            int amount = Mathf.CeilToInt(item.amount * Database.repairCostShare * damage);
            if (amount > 0)
            {
                cost.Add(new ResourceAmount { resource = item.resource, amount = amount });
            }
        }
        return cost;
    }

    public bool TryRepair(BuildingState building, out string reason)
    {
        var def = Database.GetBuilding(building.definitionId);
        var cost = RepairCost(building);
        if (!CanAfford(cost))
        {
            reason = "Not enough resources to repair";
            return false;
        }
        Spend(cost);
        building.health = StatsOf(building).maxHealth;
        reason = null;
        Changed?.Invoke();
        return true;
    }

    // ---------- Research ----------

    public bool CanResearch(Technology tech, out string missing)
    {
        if (HasResearched(tech))
        {
            missing = "Already researched";
            return false;
        }
        if (!string.IsNullOrEmpty(State.researchingId))
        {
            missing = "Already researching something";
            return false;
        }
        return Meets(tech.requirements, out missing);
    }

    public bool TryStartResearch(Technology tech, out string reason)
    {
        if (!CanResearch(tech, out reason))
        {
            return false;
        }
        Spend(tech.requirements.cost);
        State.researchingId = tech.id;
        State.researchProgress = 0;
        Changed?.Invoke();
        return true;
    }

    // ---------- Ages ----------

    public bool CanAdvanceAge(out string missing)
    {
        if (NextAge == null)
        {
            missing = "This is the latest age";
            return false;
        }
        return Meets(NextAge.advanceRequirements, out missing);
    }

    public bool TryAdvanceAge(out string reason)
    {
        if (!CanAdvanceAge(out reason))
        {
            return false;
        }
        Spend(NextAge.advanceRequirements.cost);
        State.ageIndex++;
        Notify($"Your civilisation has entered the {CurrentAge.displayName}!");
        Changed?.Invoke();
        return true;
    }

    // ---------- Upgrades ----------

    public float ProductionMultiplier(ResourceType resource)
    {
        float multiplier = 1f;
        foreach (var id in State.researched)
        {
            var tech = Database.GetTechnology(id);
            if (tech == null)
            {
                continue;
            }
            foreach (var mod in tech.productionModifiers)
            {
                if (mod.resource == null || mod.resource == resource)
                {
                    multiplier *= mod.multiplier;
                }
            }
        }
        return multiplier;
    }

    /// <summary>Combined effect of all researched tech on one unit type.</summary>
    public UnitStatModifier GetUnitModifier(UnitDefinition unit)
    {
        var total = new UnitStatModifier();
        foreach (var id in State.researched)
        {
            var tech = Database.GetTechnology(id);
            if (tech == null)
            {
                continue;
            }
            foreach (var mod in tech.unitModifiers)
            {
                if (mod.Affects(unit))
                {
                    total.healthMultiplier *= mod.healthMultiplier;
                    total.attackMultiplier *= mod.attackMultiplier;
                    total.speedMultiplier *= mod.speedMultiplier;
                    total.cooldownMultiplier *= mod.cooldownMultiplier;
                    total.rangeMultiplier *= mod.rangeMultiplier;
                }
            }
        }
        return total;
    }

    // ---------- Defence and raids ----------

    /// <summary>Rough fighting strength of a unit type. A Stone Age Clubman is about 1.</summary>
    public static float UnitPower(UnitDefinition unit, UnitStatModifier modifier = null)
    {
        if (unit == null)
        {
            return 0;
        }
        float health = unit.maxHealth * (modifier?.healthMultiplier ?? 1f);
        float attack = unit.attackPower * (modifier?.attackMultiplier ?? 1f);
        float cooldown = Mathf.Max(0.1f, unit.attackCooldown * (modifier?.cooldownMultiplier ?? 1f));
        // Ranged units deal their damage from safety, so they count double.
        float ranged = unit.projectile != null ? 2f : 1f;
        return health / 100f * (attack / 10f) / cooldown * ranged;
    }

    public static UnitStatModifier Combine(UnitStatModifier a, UnitStatModifier b)
    {
        return new UnitStatModifier
        {
            healthMultiplier = a.healthMultiplier * b.healthMultiplier,
            attackMultiplier = a.attackMultiplier * b.attackMultiplier,
            speedMultiplier = a.speedMultiplier * b.speedMultiplier,
            cooldownMultiplier = a.cooldownMultiplier * b.cooldownMultiplier,
            rangeMultiplier = a.rangeMultiplier * b.rangeMultiplier
        };
    }

    /// <summary>Settlement defence used when a wave is resolved without being watched.</summary>
    public float DefensePower
    {
        get
        {
            float total = 0;
            foreach (var building in State.buildings)
            {
                var def = Database.GetBuilding(building.definitionId);
                if (def == null || !IsWorking(building, def))
                {
                    continue;
                }
                var stats = StatsOf(def, building.upgradeLevel);
                float healthFactor = building.health / stats.maxHealth;
                total += stats.defense * healthFactor;
                if (stats.towerRange > 0)
                {
                    // A tower counts like a few troops, scaled by its damage rate.
                    total += stats.towerDamage / 10f / Mathf.Max(0.2f, def.towerCooldown) * 1.5f * healthFactor * StaffingOf(building);
                }
                if (def.trainsUnit != null)
                {
                    var mod = Combine(GetUnitModifier(def.trainsUnit), stats.trainedUnits);
                    total += stats.maxUnits * UnitPower(def.trainsUnit, mod);
                }
            }
            return total;
        }
    }

    /// <summary>
    /// Raiders broke through. severity 0..1 scales how much is stolen and smashed
    /// (1 = the whole wave got through).
    /// </summary>
    public void ApplyRaid(float severity)
    {
        severity = Mathf.Clamp01(severity);
        if (severity <= 0)
        {
            return;
        }

        var report = new StringBuilder("Raiders broke through!");

        float lossShare = severity * Database.maxResourceLossShare;
        foreach (var stock in State.resources)
        {
            float lost = stock.amount * lossShare;
            stock.amount -= lost;
            var resource = Database.GetResource(stock.id);
            if (lost >= 1 && resource != null)
            {
                report.Append($" -{Mathf.FloorToInt(lost)} {resource.displayName}.");
            }
        }

        var standing = State.buildings.FindAll(b => b.IsBuilt && b.health > 0);
        float totalHealth = 0;
        foreach (var b in standing)
        {
            totalHealth += StatsOf(b).maxHealth;
        }
        // Damage is dealt in whole points so health always matches what the UI shows.
        float damageLeft = Mathf.Round(totalHealth * severity * Database.maxBuildingDamageShare);
        int ruined = 0;
        int guard = 100;
        while (damageLeft >= 1f && standing.Count > 0 && guard-- > 0)
        {
            var target = standing[UnityEngine.Random.Range(0, standing.Count)];
            float hit = Mathf.Round(Mathf.Min(damageLeft, target.health, StatsOf(target).maxHealth * 0.5f));
            hit = Mathf.Clamp(hit, 1f, target.health);
            target.health -= hit;
            damageLeft -= hit;
            if (target.health <= 0f)
            {
                target.health = 0;
                standing.Remove(target);
                ruined++;
            }
        }
        if (ruined > 0)
        {
            report.Append($" {ruined} building(s) ruined and need repair.");
        }

        Notify(report.ToString());
        Changed?.Invoke();
    }

    /// <summary>A raider hits one building (live fights in the free mode scene).</summary>
    public void DamageBuilding(BuildingState building, float amount)
    {
        if (building == null || !building.IsBuilt || building.health <= 0)
        {
            return;
        }
        building.health = Mathf.Max(0, building.health - Mathf.Round(amount));
        if (building.health <= 0)
        {
            var def = Database.GetBuilding(building.definitionId);
            Notify($"Raiders ruined a {Name(def)}! Repair it to use it again.");
            Changed?.Invoke();
        }
    }

    /// <summary>Take up to 'amount' resources from the stockpiles, spread across what's there.</summary>
    public List<ResourceAmount> Steal(int amount)
    {
        var taken = new List<ResourceAmount>();
        var available = State.resources.FindAll(s => s.amount >= 1);
        for (int i = 0; i < amount && available.Count > 0; i++)
        {
            var stock = available[UnityEngine.Random.Range(0, available.Count)];
            stock.amount -= 1;
            var res = Database.GetResource(stock.id);
            int index = taken.FindIndex(t => t.resource == res);
            if (index >= 0)
            {
                var t = taken[index];
                t.amount++;
                taken[index] = t;
            }
            else
            {
                taken.Add(new ResourceAmount { resource = res, amount = 1 });
            }
            if (stock.amount < 1)
            {
                available.Remove(stock);
            }
        }
        return taken;
    }

    /// <summary>Give back loot recovered from a defeated raider.</summary>
    public void ReturnResources(List<ResourceAmount> loot)
    {
        foreach (var item in loot)
        {
            if (item.resource != null)
            {
                AddResource(item.resource, item.amount);
            }
        }
    }

    public static string Describe(List<ResourceAmount> amounts)
    {
        var parts = amounts.FindAll(a => a.resource != null && a.amount > 0)
            .ConvertAll(a => $"{a.amount} {a.resource.displayName}");
        return string.Join(", ", parts);
    }

    // ---------- Save / load ----------

    public void Save()
    {
        if (State == null)
        {
            return;
        }
        if (Map != null)
        {
            State.explored = Map.RevealedZones();
        }
        try
        {
            File.WriteAllText(loadedFrom ?? SavePath, JsonUtility.ToJson(State, true));
        }
        catch (Exception e)
        {
            Debug.LogError($"Settlement: failed to save: {e.Message}");
        }
    }

    private static SettlementState LoadState()
    {
        if (!HasSave)
        {
            return null;
        }
        try
        {
            return JsonUtility.FromJson<SettlementState>(File.ReadAllText(SavePath));
        }
        catch (Exception e)
        {
            Debug.LogError($"Settlement: save file unreadable, starting fresh: {e.Message}");
            return null;
        }
    }

    /// <summary>Delete the save. The settlement starts fresh next time free mode is entered.</summary>
    public static void DeleteSave()
    {
        if (Instance != null)
        {
            var go = Instance.gameObject;
            Instance.State = null; // so OnDestroy doesn't re-save
            Instance = null;
            if (Application.isPlaying)
            {
                Destroy(go);
            }
            else
            {
                DestroyImmediate(go);
            }
        }
        if (HasSave)
        {
            File.Delete(SavePath);
        }
    }

    // ---------- Helpers ----------

    private void Notify(string message)
    {
        Debug.Log($"[Settlement] {message}");
        Message?.Invoke(message);
    }

    private static string Name(BuildingDefinition def) => def != null ? def.displayName : "Building";
}
