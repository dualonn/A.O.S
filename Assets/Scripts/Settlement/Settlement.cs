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

    /// <summary>0 to 1. Buildings run slower when there aren't enough people to work them.</summary>
    public float WorkerEfficiency => WorkersNeeded == 0 ? 1f : Mathf.Min(1f, State.population / WorkersNeeded);

    /// <summary>How advanced the player is. Drives wave strength.</summary>
    public int ThreatLevel => State.ageIndex * 5 + State.researched.Count;

    private const float AutosaveInterval = 30f;
    private float autosaveTimer;
    private float populationTimer;

    private static string SavePath => Path.Combine(Application.persistentDataPath, "settlement.json");
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
        return settlement;
    }

    private void Initialise(GameDatabase database)
    {
        Instance = this;
        Database = database;
        State = LoadState() ?? NewState();
        if (State.mapSeed == 0)
        {
            State.mapSeed = NewSeed();
        }
        Map = MapGenerator.Create(State.mapSeed, Database, State.exploredZones);
        State.exploredZones = Map.RevealedZoneIndices();
        // Older saves could hold fractional building health; snap it to whole points.
        foreach (var building in State.buildings)
        {
            building.health = Mathf.Round(building.health);
        }
        RecalculateDerived();
        Waves = gameObject.AddComponent<WaveDirector>();
        Waves.Initialise(this);
    }

    private SettlementState NewState()
    {
        var state = new SettlementState
        {
            population = Database.startingPopulation,
            secondsUntilWave = Database.firstWaveDelay,
            mapSeed = NewSeed()
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
    /// Explore new zones (only ones bordering explored land, inside the size limit).
    /// Returns how many were added.
    /// </summary>
    public int ExploreZones(IEnumerable<Vector2Int> zones)
    {
        var shown = MapGenerator.RevealZones(Map, zones);
        if (shown.Count == 0)
        {
            return 0;
        }
        foreach (var rect in shown)
        {
            int index = Map.ZoneIndex(rect.x / Map.ChunkSize, rect.y / Map.ChunkSize);
            if (!State.exploredZones.Contains(index))
            {
                State.exploredZones.Add(index);
            }
        }
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
        if (!IsFounded)
        {
            return; // nothing happens until the player picks a spot
        }
        TickConstruction(dt);
        TickUpgrades(dt);
        RecalculateDerived();
        TickProduction(dt);
        TickPopulation(dt);
        TickResearch(dt);
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
        if (target.size != def.size)
        {
            missing = $"{Name(target)} needs a different footprint";
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
                Notify($"{Name(def)} finished building.");
                Changed?.Invoke();
            }
        }
    }

    private void TickProduction(float dt)
    {
        float efficiency = WorkerEfficiency;
        foreach (var building in State.buildings)
        {
            var def = Database.GetBuilding(building.definitionId);
            if (def == null || !IsWorking(building, def))
            {
                continue;
            }
            var stats = StatsOf(def, building.upgradeLevel);
            float healthFactor = building.health / stats.maxHealth;
            foreach (var rate in def.production)
            {
                if (rate.resource != null)
                {
                    float multiplier = ProductionMultiplier(rate.resource) * building.siteQuality * stats.output;
                    AddResource(rate.resource, rate.perSecond * dt * efficiency * healthFactor * multiplier);
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

    private void RecalculateDerived()
    {
        int housing = 0;
        int workers = 0;
        foreach (var building in State.buildings)
        {
            var def = Database.GetBuilding(building.definitionId);
            if (def == null || !IsWorking(building, def))
            {
                continue;
            }
            var stats = StatsOf(def, building.upgradeLevel);
            housing += stats.housing;
            workers += stats.workers;
        }
        Housing = housing;
        WorkersNeeded = workers;
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
            health = def.maxHealth,
            buildProgress = def.buildTime > 0 ? 0 : 1,
            siteQuality = quality
        };
        State.buildings.Add(placed);
        if (founding)
        {
            // Raiders only start coming once there's something to raid.
            State.secondsUntilWave = Database.firstWaveDelay;
            Notify("Your settlement is founded! Build huts and camps near food and wood.");
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
        return health / 100f * (attack / 10f) / cooldown;
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
                    total += stats.towerDamage / 10f / Mathf.Max(0.2f, def.towerCooldown) * 1.5f * healthFactor;
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
        try
        {
            File.WriteAllText(SavePath, JsonUtility.ToJson(State, true));
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
