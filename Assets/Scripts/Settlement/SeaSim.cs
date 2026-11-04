using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// The sea economy's simulation (see SeaData.cs): shoals drift, are fished down and regrow; sea
/// life roams and is hunted; fishing, whaling, pearl diving and the rest of the shore's buildings
/// earn by what's round them; lighthouses, salt and monsters change that. Runs on every world (in
/// the background for the others). The sea routes are in SeaTrade.cs, building sites in SeaSites.cs.
/// </summary>
public partial class Settlement
{
    // ---------- State ----------

    /// <summary>This world's sea.</summary>
    public SeaWorldState Sea
    {
        get
        {
            var w = CurrentWorld;
            if (w.sea == null) w.sea = new SeaWorldState();
            var s = w.sea;
            if (s.shoals == null) s.shoals = new List<SeaShoal>();
            if (s.pods == null) s.pods = new List<SeaPod>();
            if (s.routes == null) s.routes = new List<SeaRoute>();
            return s;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void SeaRegisterHooks()
    {
        SettlementHooks.Add(SettlementHooks.Production, SeaProductionHook);
        SettlementHooks.Add(SettlementHooks.Mood, SeaMoodHook);
    }

    /// <summary>A sea building in the world being ticked (gathered every couple of seconds).</summary>
    private class SeaSpot
    {
        public BuildingState b;
        public BuildingDefinition def;
        public SeaBuilding info;
        public Vector2 centre;
        public int reach;
        public float depth = 1f;   // oil rigs: how deep the water round it is (0..1)
    }

    /// <summary>What the sea code knows about one world between ticks (not saved: worked out again).</summary>
    private class SeaWorldCache
    {
        public int revealed = -1;
        public readonly List<Vector2Int> waterZones = new List<Vector2Int>();
        public readonly HashSet<Vector2Int> checkedZones = new HashSet<Vector2Int>();
        public readonly List<SeaSpot> spots = new List<SeaSpot>();
        public readonly List<(Vector2 at, float r, int tier)> lights = new List<(Vector2, float, int)>();
        public int salt, pearls, fishing, harbours;
        public float tidalPower;
        public bool monsterNear;
        public float gatherIn, harmIn = 20f;
        public int buildingCount = -1;
    }

    private readonly Dictionary<int, SeaWorldCache> seaCaches = new Dictionary<int, SeaWorldCache>();

    // Per building (instance ids are unique across worlds): its output factor, food factor, and what it found.
    private readonly Dictionary<int, (float all, float food)> seaFactor = new Dictionary<int, (float, float)>();
    private readonly Dictionary<int, (int shoals, float rich, int pods, bool scared, bool lit)> seaFound = new Dictionary<int, (int, float, int, bool, bool)>();
    private readonly Dictionary<int, float> seaScaredUntil = new Dictionary<int, float>();
    private readonly Dictionary<int, float> seaKillIn = new Dictionary<int, float>();
    private float seaTimer;

    private SeaWorldCache SeaCache
    {
        get
        {
            if (!seaCaches.TryGetValue(State.currentWorld, out var c)) seaCaches[State.currentWorld] = c = new SeaWorldCache();
            return c;
        }
    }

    private bool SeaHas(string tech) => State.researched.Contains(tech);

    private int SeaLevel(string tech)
    {
        int n = 0;
        foreach (var t in State.researched) if (t == tech) n++;
        return n;
    }

    private bool OnOceanWorld => CurrentWorld.profile == "Ocean";

    /// <summary>Tidal power on the world being looked at (for the power grid): see SeaCatalog.TidalPowerPerGenerator.</summary>
    public float SeaTidalPower => SeaCache.tidalPower;

    // ---------- Ticking ----------

    /// <summary>The sea, every half second (every background step on other worlds).</summary>
    private void SeaTick(float dt)
    {
        if (!offWorld)
        {
            seaTimer += dt;
            if (seaTimer < 0.5f) return;
            dt = seaTimer;
            seaTimer = 0f;
        }
        if (Map == null || !IsFounded) return;
        var sea = Sea;
        var cache = SeaCache;
        SeaFindWaterZones(cache);
        cache.gatherIn -= dt;
        if (cache.gatherIn <= 0f || cache.buildingCount != State.buildings.Count)
        {
            cache.gatherIn = 2f;
            SeaGather(cache);
        }
        SeaTickShoals(sea, cache, dt);
        SeaTickPods(sea, cache, dt);
        SeaTickBuildings(sea, cache, dt);
        SeaTickRoutes(sea, cache, dt);
        SeaTickHarm(sea, cache, dt);
    }

    /// <summary>The explored zones with a fair bit of sea in them (only newly explored ones are looked at).</summary>
    private void SeaFindWaterZones(SeaWorldCache cache)
    {
        if (cache.revealed == Map.RevealedZoneCount) return;
        cache.revealed = Map.RevealedZoneCount;
        foreach (var z in Map.RevealedZones())
        {
            if (!cache.checkedZones.Add(z)) continue;
            var rect = Map.ZoneRect(z.x, z.y);
            int water = 0, samples = 0;
            for (int y = rect.yMin + 1; y < rect.yMax; y += Mathf.Max(1, rect.height / 8))
                for (int x = rect.xMin + 1; x < rect.xMax; x += Mathf.Max(1, rect.width / 8))
                {
                    samples++;
                    if (RiverNav.Sailable(Map, x, y)) water++;
                }
            if (samples > 0 && water >= samples * 0.2f) cache.waterZones.Add(z);
        }
    }

    /// <summary>Gather this world's sea buildings, lighthouses, salt pans and the rest.</summary>
    private void SeaGather(SeaWorldCache cache)
    {
        cache.buildingCount = State.buildings.Count;
        cache.spots.Clear();
        cache.lights.Clear();
        cache.salt = cache.pearls = cache.fishing = cache.harbours = 0;
        cache.tidalPower = 0f;
        foreach (var b in State.buildings)
        {
            var def = Database.GetBuilding(b.definitionId);
            if (def == null) continue;
            if (RiverCatalog.KindOf(def) == RiverCatalog.Kind.Harbour && IsWorking(b, def)) cache.harbours++;
            if (!SeaCatalog.TryGet(def, out var info) || !IsWorking(b, def)) continue;
            var spot = new SeaSpot { b = b, def = def, info = info, centre = b.cell + (Vector2)def.size * 0.5f };
            spot.reach = info.reach <= 0 ? 0 : info.reach + (info.role == SeaRole.Fishing && SeaHas(SeaCatalog.TechSonar) ? 3 : 0) + 2 * Mathf.Min(3, b.upgradeLevel);
            if (info.role == SeaRole.Light) spot.reach = info.reach + 2 * Mathf.Min(3, b.upgradeLevel);
            if (info.role == SeaRole.Oil) spot.depth = Mathf.Clamp01(RiverOpenWater(new RectInt(b.cell, def.size), 4) / 70f);
            cache.spots.Add(spot);
            float staff = b.Paused ? 0f : StaffingOf(b);
            switch (info.role)
            {
                case SeaRole.Light: if (!b.Paused) cache.lights.Add((spot.centre, spot.reach, info.tier)); break;
                case SeaRole.Salt: if (staff > 0f) cache.salt++; break;
                case SeaRole.Pearl: if (staff > 0f) cache.pearls++; break;
                case SeaRole.Fishing: case SeaRole.Whaling: case SeaRole.Kelp: case SeaRole.Ranch: if (staff > 0f) cache.fishing++; break;
                case SeaRole.Tidal:
                    float hp = b.health / Mathf.Max(1f, StatsOf(def, b.upgradeLevel).maxHealth);
                    cache.tidalPower += (SeaCatalog.TidalPowerPerGenerator + SeaCatalog.TidalPowerPerUpgrade * Mathf.Min(3, b.upgradeLevel)) * staff * Mathf.Clamp01(hp);
                    break;
            }
        }
    }

    /// <summary>Is this point under a lighthouse's beam (and the best beam's tier, or -1)?</summary>
    private int SeaLitTier(SeaWorldCache cache, Vector2 p)
    {
        int best = -1;
        foreach (var l in cache.lights)
            if (l.tier > best && (l.at - p).sqrMagnitude <= l.r * l.r) best = l.tier;
        return best;
    }

    // ---------- Shoals ----------

    private int SeaShoalTarget(SeaWorldCache cache) =>
        Mathf.Clamp(Mathf.RoundToInt(cache.waterZones.Count * (OnOceanWorld ? 0.8f : 0.5f)), cache.waterZones.Count > 0 ? 2 : 0, 40);

    private float SeaRegrowFactor => (1f + 0.12f * Mathf.Min(SeaCatalog.ManagementLevels, SeaLevel(SeaCatalog.TechManagement))) * (OnOceanWorld ? 1.25f : 1f);

    private void SeaTickShoals(SeaWorldState sea, SeaWorldCache cache, float dt)
    {
        int target = SeaShoalTarget(cache);
        sea.shoalIn -= dt;
        if (sea.shoalIn <= 0f)
        {
            sea.shoalIn = sea.shoals.Count < target / 2 ? 3f : Random.Range(45f, 100f);
            if (sea.shoals.Count < target) SeaSpawnShoal(sea, cache);
        }
        float regrow = SeaRegrowFactor;
        for (int i = sea.shoals.Count - 1; i >= 0; i--)
        {
            var s = sea.shoals[i];
            int sx = Mathf.FloorToInt(s.pos.x), sy = Mathf.FloorToInt(s.pos.y);
            if (!Map.InBounds(sx, sy)) continue;   // (its part of the map isn't loaded: leave it be)
            if (!RiverNav.Sailable(Map, sx, sy)) { sea.shoals.RemoveAt(i); continue; }   // bridged over
            float f = Mathf.Clamp01(s.fish / Mathf.Max(1f, s.max));
            // Regrowth: slow when nearly empty, fastest half full, nothing once full.
            float rate = s.max * 0.0035f * regrow * (0.2f + f) * (1f - f);
            if (State.playTime - s.fishedAt < 20f) rate *= 0.5f;
            s.fish = Mathf.Min(s.max, s.fish + rate * dt);
            if (s.kind != SeaCatalog.ShoalFish) continue;
            // A fished-out shoal moves on to new waters.
            if (f < 0.12f && (s.home - s.pos).sqrMagnitude < 9f) SeaMoveShoalHome(s);
            Vector2 to = s.target - s.pos;
            float d = to.magnitude;
            if (d > 0.05f)
            {
                var next = s.pos + to / d * Mathf.Min(d, 0.05f * dt);
                if (RiverNav.Sailable(Map, next)) s.pos = next;
                else s.target = s.pos;
            }
            else if (Random.value < dt * 0.04f)
            {
                var t = s.home + Random.insideUnitCircle * 9f;
                if (RiverNav.Sailable(Map, t)) s.target = t;
            }
        }
    }

    private void SeaMoveShoalHome(SeaShoal s)
    {
        for (int attempt = 0; attempt < 8; attempt++)
        {
            var t = s.pos + Random.insideUnitCircle.normalized * Random.Range(12f, 30f);
            if (!RiverNav.Sailable(Map, t) || !RiverNav.ClearLine(Map, s.pos, t)) continue;
            s.home = s.target = t;
            return;
        }
        s.home = s.pos + Vector2.one * 0.01f;   // nowhere to go: stay and regrow
    }

    private void SeaSpawnShoal(SeaWorldState sea, SeaWorldCache cache)
    {
        if (cache.waterZones.Count == 0) return;
        for (int attempt = 0; attempt < 10; attempt++)
        {
            var zone = cache.waterZones[Random.Range(0, cache.waterZones.Count)];
            var rect = Map.ZoneRect(zone.x, zone.y);
            var p = new Vector2(Random.Range(rect.xMin + 0.5f, rect.xMax - 0.5f), Random.Range(rect.yMin + 0.5f, rect.yMax - 0.5f));
            int x = Mathf.FloorToInt(p.x), y = Mathf.FloorToInt(p.y);
            if (!RiverNav.Sailable(Map, x, y)) continue;
            bool crowded = false;
            foreach (var other in sea.shoals) if ((other.pos - p).sqrMagnitude < 49f) { crowded = true; break; }
            if (crowded) continue;
            int fromBank = RiverDistanceFromBank(x, y, 4);
            bool oysters = fromBank <= 2 && Random.value < 0.35f;
            float max = oysters ? 60f : SeaCatalog.ShoalFull * (OnOceanWorld ? 1.4f : 1f) * Random.Range(0.8f, 1.2f);
            p = new Vector2(x + 0.5f, y + 0.5f);
            sea.shoals.Add(new SeaShoal
            {
                id = sea.nextId++, kind = oysters ? SeaCatalog.ShoalOysters : SeaCatalog.ShoalFish,
                pos = p, home = p, target = p, max = max, fish = max * Random.Range(0.55f, 0.9f),
            });
            return;
        }
    }

    /// <summary>Fish within 'reach' of a point (as shoal-fulls: 1 = one full shoal), of one kind.</summary>
    private float SeaRichness(SeaWorldState sea, Vector2 at, float reach, int kind, out int count)
    {
        float rich = 0f;
        count = 0;
        float r2 = reach * reach;
        foreach (var s in sea.shoals)
        {
            if (s.kind != kind || (s.pos - at).sqrMagnitude > r2) continue;
            count++;
            rich += s.fish / SeaCatalog.ShoalFull;
        }
        return rich;
    }

    /// <summary>Take 'amount' of fish from the shoals in reach, the fullest first.</summary>
    private void SeaDrain(SeaWorldState sea, Vector2 at, float reach, int kind, float amount)
    {
        if (amount <= 0f) return;
        float r2 = reach * reach;
        SeaShoal best = null;
        foreach (var s in sea.shoals)
            if (s.kind == kind && (s.pos - at).sqrMagnitude <= r2 && (best == null || s.fish > best.fish)) best = s;
        if (best == null) return;
        float took = Mathf.Min(best.fish, amount);
        best.fish -= took;
        best.fishedAt = State.playTime;
        sea.fishCaught += took;
    }

    // ---------- Sea life ----------

    private void SeaSpeciesFor(bool monster, List<SeaSpecies> into)
    {
        into.Clear();
        bool alien = OnAlienWorld;
        foreach (var sp in SeaCatalog.Species)
        {
            if (sp.monster != monster || sp.minAge > State.ageIndex) continue;
            if (alien != (sp.realm == 2)) continue;
            into.Add(sp);
        }
    }

    private readonly List<SeaSpecies> seaSpeciesWork = new List<SeaSpecies>();

    private void SeaTickPods(SeaWorldState sea, SeaWorldCache cache, float dt)
    {
        var zones = SettlementZones();
        int ordinary = 0;
        bool haveMonster = false;
        foreach (var p in sea.pods)
        {
            var sp = SeaCatalog.SpeciesOf(p.species);
            if (sp == null) continue;
            if (sp.monster) haveMonster = true; else ordinary++;
        }
        int target = Mathf.Clamp(Mathf.RoundToInt(cache.waterZones.Count * 0.14f), 0, 9);
        sea.podIn -= dt;
        if (sea.podIn <= 0f)
        {
            sea.podIn = Random.Range(70f, 160f);
            if (ordinary < target) SeaSpawnPod(sea, cache, zones, false);
        }
        sea.monsterIn -= dt;
        if (sea.monsterIn <= 0f)
        {
            sea.monsterIn = Random.Range(2400f, 4200f);
            if (!haveMonster && cache.waterZones.Count >= 6) SeaSpawnPod(sea, cache, zones, true);
        }

        cache.monsterNear = false;
        for (int i = sea.pods.Count - 1; i >= 0; i--)
        {
            var p = sea.pods[i];
            var sp = SeaCatalog.SpeciesOf(p.species);
            if (sp == null || p.count <= 0) { sea.pods.RemoveAt(i); continue; }
            if (sp.monster && State.playTime >= p.leaveAt)
            {
                Notify($"{sp.name} sinks back into the deep. The coast is quiet again.");
                sea.pods.RemoveAt(i);
                continue;
            }
            bool fleeing = State.playTime < p.fleeUntil;
            float speed = fleeing ? 0.7f : sp.monster ? 0.2f : 0.12f;
            Vector2 to = p.target - p.pos;
            float d = to.magnitude;
            if (d > 0.1f)
            {
                var next = p.pos + to / d * Mathf.Min(d, speed * dt);
                if (RiverNav.Sailable(Map, next)) p.pos = next;
                else p.target = p.pos;
            }
            else if (Random.value < dt * (sp.monster ? 0.12f : 0.06f))
            {
                float roam = sp.monster && p.toCoast ? 6f : 14f;
                var centre = p.toCoast ? p.target : p.home;
                for (int a = 0; a < 4; a++)
                {
                    var t = centre + Random.insideUnitCircle * roam;
                    if (RiverNav.Sailable(Map, t)) { p.target = t; break; }
                }
            }
            if (sp.monster)
            {
                p.life = Mathf.Min(1f, p.life + dt * 0.0008f);   // its wounds close slowly
                foreach (var z in zones)
                    if (!z.IsOutpost && z.Near(p.pos, 22f)) { cache.monsterNear = true; break; }
                continue;
            }
            // Left in peace, a pod grows back.
            if (p.count < p.max && State.playTime - p.huntedAt > 90f)
            {
                p.regrowIn -= dt * SeaRegrowFactor;
                if (p.regrowIn <= 0f) { p.regrowIn = 150f; p.count++; }
            }
        }
    }

    private void SeaSpawnPod(SeaWorldState sea, SeaWorldCache cache, List<SettlementZone> zones, bool monster)
    {
        if (cache.waterZones.Count == 0) return;
        SeaSpeciesFor(monster, seaSpeciesWork);
        if (seaSpeciesWork.Count == 0) return;
        // At home, serpents keep to the deep and turn up less than whales.
        var sp = seaSpeciesWork[Random.Range(0, seaSpeciesWork.Count)];
        if (!monster && sp.role == WildRole.Predator && seaSpeciesWork.Count > 1 && Random.value < 0.5f)
            sp = seaSpeciesWork.Find(s => s.role == WildRole.Grazer) ?? sp;
        float keep = monster ? 30f : sp.role == WildRole.Predator ? 18f : 12f;
        for (int attempt = 0; attempt < 12; attempt++)
        {
            var zone = cache.waterZones[Random.Range(0, cache.waterZones.Count)];
            var rect = Map.ZoneRect(zone.x, zone.y);
            var p = new Vector2(Random.Range(rect.xMin + 0.5f, rect.xMax - 0.5f), Random.Range(rect.yMin + 0.5f, rect.yMax - 0.5f));
            if (!RiverNav.Sailable(Map, p) || RiverDistanceFromBank(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y), 3) < 3) continue;
            bool near = false;
            foreach (var z in zones) if (z.Near(p, keep)) { near = true; break; }
            if (near) continue;
            int n = Random.Range(sp.countMin, sp.countMax + 1);
            var pod = new SeaPod { id = sea.nextId++, species = sp.id, pos = p, home = p, target = p, count = n, max = n };
            if (monster)
            {
                pod.leaveAt = State.playTime + Random.Range(1300f, 1900f);
                pod.toCoast = SeaCoastTarget(out var coast);
                if (pod.toCoast) pod.target = coast;
                Notify($"<color=#ff9a6a><b>{sp.name} has been sighted!</b></color> {sp.note} Sink it for a hoard from the deep.");
                if (!offWorld) GameAudio.UI("sea_monster_roar", 0.8f);
            }
            sea.pods.Add(pod);
            return;
        }
    }

    /// <summary>Somewhere on the water by your boats (a fishing building or port) for a monster to haunt.</summary>
    private bool SeaCoastTarget(out Vector2 at)
    {
        at = default;
        var spots = SeaCache.spots;
        if (spots.Count == 0) return false;
        var s = spots[Random.Range(0, spots.Count)];
        if (!RiverNav.NearestSailable(Map, s.centre, 8, out var w)) return false;
        at = w;
        return true;
    }

    // ---------- The buildings ----------

    private void SeaTickBuildings(SeaWorldState sea, SeaWorldCache cache, float dt)
    {
        float nets = SeaHas(SeaCatalog.TechNets) ? 1.05f : 1f;
        float sonar = SeaHas(SeaCatalog.TechSonar) ? 1.05f : 1f;
        float saltFood = 1f + 0.06f * Mathf.Min(3, cache.salt);
        bool ocean = OnOceanWorld;
        foreach (var s in cache.spots)
        {
            var b = s.b;
            var info = s.info;
            float staff = b.Paused ? 0f : StaffingOf(b);
            int lit = SeaLitTier(cache, s.centre);
            float light = lit < 0 ? 1f : 1.08f + 0.03f * lit;
            bool scared = seaScaredUntil.TryGetValue(b.instanceId, out float until) && State.playTime < until;
            float all = 1f, food = 1f;
            int shoals = 0, pods = 0;
            float rich = 0f;
            switch (info.role)
            {
                case SeaRole.Fishing:
                case SeaRole.Pearl:
                {
                    if (s.reach <= 0) { all = (ocean ? 1.2f : 1f) * nets; break; }   // fish pens: their own fish
                    int kind = info.role == SeaRole.Pearl ? SeaCatalog.ShoalOysters : SeaCatalog.ShoalFish;
                    rich = SeaRichness(sea, s.centre, s.reach, kind, out shoals);
                    all = Mathf.Lerp(info.floor, info.top, Mathf.Clamp01(rich / Mathf.Max(0.1f, info.need)));
                    if (SeaPredatorNear(sea, s.centre, s.reach)) all *= 0.7f;
                    if (scared) all *= 0.35f;
                    all *= light * (info.role == SeaRole.Fishing ? nets * sonar : 1f);
                    float output = StatsOf(s.def, b.upgradeLevel).output;
                    SeaDrain(sea, s.centre, s.reach, kind, info.drain * staff * output * dt);
                    if (info.role == SeaRole.Fishing) food = saltFood;
                    break;
                }
                case SeaRole.Whaling:
                {
                    pods = SeaHuntable(sea, s.centre, s.reach, info.tier, out var biggest);
                    all = Mathf.Lerp(info.floor, info.top, Mathf.Clamp01(pods / Mathf.Max(0.1f, info.need))) * light;
                    if (scared) all *= 0.35f;
                    food = saltFood;
                    if (biggest != null && staff > 0f) SeaWhalersHunt(sea, s, biggest, staff, dt);
                    break;
                }
                case SeaRole.Kelp: all = (ocean ? 1.25f : 1f) * (SeaHas(SeaCatalog.TechPlankton) ? 1.05f : 1f); break;
                case SeaRole.Ranch: all = ocean ? 1.3f : 1f; break;
                case SeaRole.Oil: all = Mathf.Lerp(info.floor, info.top, s.depth); break;
                case SeaRole.Tidal: all = ocean ? 1.25f : 1f; break;
                case SeaRole.Salt: all = ocean ? 1.2f : 1f; break;
            }
            seaFactor[b.instanceId] = (all, food);
            seaFound[b.instanceId] = (shoals, rich, pods, scared, lit >= 0);
        }
    }

    private bool SeaPredatorNear(SeaWorldState sea, Vector2 at, float reach)
    {
        float r2 = reach * reach;
        foreach (var p in sea.pods)
        {
            var sp = SeaCatalog.SpeciesOf(p.species);
            if (sp != null && sp.role == WildRole.Predator && State.playTime >= p.fleeUntil && (p.pos - at).sqrMagnitude <= r2) return true;
        }
        return false;
    }

    /// <summary>Creatures whalers of this tier can hunt within reach (grazers; predators too for harpoon wharves).</summary>
    private int SeaHuntable(SeaWorldState sea, Vector2 at, float reach, int tier, out SeaPod biggest)
    {
        biggest = null;
        int n = 0;
        float r2 = reach * reach;
        foreach (var p in sea.pods)
        {
            var sp = SeaCatalog.SpeciesOf(p.species);
            if (sp == null || sp.monster || (sp.role == WildRole.Predator && tier < 1)) continue;
            if ((p.pos - at).sqrMagnitude > r2) continue;
            n += p.count;
            if (biggest == null || p.count > biggest.count) biggest = p;
        }
        return n;
    }

    /// <summary>Every so often the whalers bring one in: meat, coin, and oil once there's a use for it.</summary>
    private void SeaWhalersHunt(SeaWorldState sea, SeaSpot s, SeaPod pod, float staff, float dt)
    {
        seaKillIn.TryGetValue(s.b.instanceId, out float left);
        if (left <= 0f) left = 60f;
        left -= dt * staff * (1f + 0.25f * Mathf.Min(3, s.b.upgradeLevel));
        if (left > 0f) { seaKillIn[s.b.instanceId] = left; return; }
        seaKillIn[s.b.instanceId] = 60f;
        bool keepOne = SeaHas(SeaCatalog.TechManagement);   // managed waters: never hunt a pod out
        if (keepOne && pod.count <= 1) return;
        var sp = SeaCatalog.SpeciesOf(pod.species);
        pod.count--;
        pod.huntedAt = State.playTime;
        pod.fleeUntil = State.playTime + 20f;
        sea.whalesTaken++;
        if (sp == null) return;
        float k = 0.5f * (OnAlienWorld ? XenoRewardFactor : 1f);
        SeaGive("Food", sp.food * k);
        SeaGive("Coin", sp.coin * k);
        if (s.info.tier >= 1) SeaGive("Oil", sp.oil * k);
        if (sp.mana > 0f && s.info.tier >= 1) SeaGive("Mana", sp.mana * k);
    }

    /// <summary>Give a resource if it's unlocked yet (rewards never show things from later ages).</summary>
    private float SeaGive(string id, float amount)
    {
        var r = Database.GetResource(id);
        if (r == null || amount <= 0f || (r.unlockedIn != null && r.unlockedIn.order > State.ageIndex)) return 0f;
        AddResource(r, amount);
        return amount;
    }

    // ---------- Harm: monsters and serpents among the boats (in the background; in view it's fought for real) ----------

    private void SeaTickHarm(SeaWorldState sea, SeaWorldCache cache, float dt)
    {
        cache.harmIn -= dt;
        if (cache.harmIn > 0f) return;
        cache.harmIn = 30f;
        if (!offWorld && SeaWorks.Instance != null && SeaWorks.Instance.isActiveAndEnabled) return;
        foreach (var p in sea.pods)
        {
            var sp = SeaCatalog.SpeciesOf(p.species);
            if (sp == null || sp.role != WildRole.Predator || State.playTime < p.fleeUntil) continue;
            if (Random.value > (sp.monster ? 0.35f : 0.12f)) continue;
            foreach (var s in cache.spots)
            {
                if (s.info.boats <= 0 || (s.centre - p.pos).sqrMagnitude > s.reach * s.reach) continue;
                SeaBoatLost(s.b, s.def, sp);
                break;
            }
        }
    }

    /// <summary>A boat of this building went down to a creature: its catch is poor for a minute.</summary>
    public void SeaBoatLost(BuildingState b, BuildingDefinition def, SeaSpecies by)
    {
        seaScaredUntil[b.instanceId] = State.playTime + 60f;
        Sea.boatsLost++;
        if (by != null) Notify($"{(by.monster ? by.name : "A " + by.name.ToLowerInvariant())} sank a boat from the {def.displayName}! Its catch will be poor for a while.");
    }

    /// <summary>A sea creature fell to your troops (in view): meat, coin and, for a monster, a hoard and a trophy.</summary>
    public void SeaCreatureSlain(int podId, string species)
    {
        var sp = SeaCatalog.SpeciesOf(species);
        if (sp == null) return;
        var sea = Sea;
        var pod = sea.pods.Find(p => p.id == podId);
        if (pod != null)
        {
            pod.count = Mathf.Max(0, pod.count - 1);
            pod.huntedAt = State.playTime;
            if (pod.count == 0) sea.pods.Remove(pod);
        }
        float k = OnAlienWorld ? XenoRewardFactor : 1f;
        if (!sp.monster)
        {
            SeaGive("Food", sp.food * 0.5f * k);
            SeaGive("Coin", sp.coin * 0.5f * k);
            return;
        }
        sea.monstersSlain++;
        var loot = new List<ResourceAmount>();
        void Give(string id, float amount)
        {
            float n = Mathf.Round(SeaGive(id, amount * k));
            var r = Database.GetResource(id);
            if (n > 0f && r != null) loot.Add(new ResourceAmount { resource = r, amount = (int)n });
        }
        Give("Food", sp.food);
        Give("Coin", sp.coin + 60 * State.ageIndex);
        Give("Gold", 80 + 30 * State.ageIndex);
        Give("Oil", sp.oil);
        Give("Mana", sp.mana);
        if (sp.realm == 2) Give("Essence", 80 + 10 * State.ageIndex);
        var item = GainLoot(0.8f, 1f, $"in the wreck of {sp.name.Replace("The ", "the ")}", Rarity.Rare, sp.realm == 2);
        Notify($"<color=#ffd56a><b>{sp.name} is slain!</b></color> Its hoard from the deep: {Describe(loot)}{(item != null ? ", and a trophy" : "")}.");
        if (!offWorld) GameAudio.UI("quest_complete");
        RaiseChanged();
    }

    /// <summary>A monster in a fight with your ships was hurt (keeps its wounds between fights).</summary>
    public void SeaMonsterHurt(int podId, float lifeLeft)
    {
        var pod = Sea.pods.Find(p => p.id == podId);
        if (pod != null) pod.life = Mathf.Clamp01(lifeLeft);
    }

    /// <summary>A creature was driven off (too badly hurt, or chased away): it keeps away for a while.</summary>
    public void SeaDriveOff(int podId, float seconds)
    {
        var pod = Sea.pods.Find(p => p.id == podId);
        if (pod == null) return;
        pod.fleeUntil = State.playTime + seconds;
        pod.toCoast = false;
        pod.target = pod.home;
    }

    // ---------- Hooks ----------

    private static float SeaProductionHook(Settlement s, BuildingState b, BuildingDefinition def, ResourceType r)
    {
        if (s.seaFactor.Count == 0 || !s.seaFactor.TryGetValue(b.instanceId, out var f)) return 1f;
        return r != null && r == s.Database.food ? f.all * f.food : f.all;
    }

    private static void SeaMoodHook(Settlement s, System.Action<float, string> add)
    {
        if (!s.seaCaches.TryGetValue(s.State.currentWorld, out var c)) return;
        if (c.pearls > 0) add(Mathf.Min(3, c.pearls), "Pearls from the sea");
        if (c.fishing >= 3) add(1f, "Fresh fish on the table");
        if (c.monsterNear) add(-3f, "A sea monster haunts the coast");
    }

    // ---------- Text ----------

    /// <summary>Lines for the selected building's panel (nothing for buildings that aren't the sea's).</summary>
    public void SeaSelectionInfo(System.Text.StringBuilder sb, BuildingState state, BuildingDefinition def)
    {
        if (state == null || !SeaCatalog.TryGet(def, out var info) || !state.IsBuilt) return;
        seaFactor.TryGetValue(state.instanceId, out var f);
        seaFound.TryGetValue(state.instanceId, out var found);
        int reach = info.reach + 2 * Mathf.Min(3, state.upgradeLevel) + (info.role == SeaRole.Fishing && SeaHas(SeaCatalog.TechSonar) ? 3 : 0);
        string Pct(float x) => x >= 1f ? $"<color=#8f8>{x:P0}</color>" : x >= 0.7f ? $"<color=#fe8>{x:P0}</color>" : $"<color=#f88>{x:P0}</color>";
        switch (info.role)
        {
            case SeaRole.Fishing:
            case SeaRole.Pearl:
                if (info.reach <= 0) { sb.Append($"Steady catch: {Pct(f.all > 0 ? f.all : 1f)}\n"); break; }
                string what = info.role == SeaRole.Pearl ? "oyster beds" : "shoals";
                sb.Append(found.shoals > 0
                    ? $"Fishing grounds within {reach} tiles: {found.shoals} {what} ({found.rich:0.0} full)\n"
                    : $"<color=#fb7>No {what} within {reach} tiles</color> <size=80%><color=#aaa>(they show on the water as silver glints)</color></size>\n");
                sb.Append($"Catch: {Pct(f.all > 0 ? f.all : 1f)}");
                if (f.food > 1.001f) sb.Append($"  <size=80%><color=#cdf>(salt: +{f.food - 1f:P0} food)</color></size>");
                sb.Append('\n');
                if (found.scared) sb.Append("<color=#f88>Lost a boat to something in the water: fishing poorly for a while</color>\n");
                if (found.lit) sb.Append("<size=80%><color=#fe9>Under a lighthouse's beam: boats come home safely</color></size>\n");
                break;
            case SeaRole.Whaling:
                sb.Append(found.pods > 0
                    ? $"Sea life within {reach} tiles: <color=#8f8>{found.pods}</color> to hunt\nCatch: {Pct(f.all)}\n"
                    : $"<color=#fb7>Nothing to hunt within {reach} tiles</color> <size=80%><color=#aaa>(whales keep to open sea)</color></size>\n");
                if (info.tier >= 1) sb.Append("<size=80%><color=#aaa>Harpoon guns: hunts sea serpents too, and brings in oil</color></size>\n");
                break;
            case SeaRole.Light:
                int r = info.reach + 2 * Mathf.Min(3, state.upgradeLevel);
                sb.Append($"Beam: {r} tiles  <size=80%><color=#aaa>(boats in it fish {0.08f + 0.03f * info.tier:P0} more, ships are safer, raiders' boats burn)</color></size>\n");
                break;
            case SeaRole.Port:
                SeaPortInfo(sb, state, info);
                break;
            case SeaRole.Oil:
                sb.Append($"Drilling: {Pct(f.all)} <size=80%><color=#aaa>(deeper water, more oil)</color></size>\n");
                break;
            case SeaRole.Tidal:
                float hp = state.health / Mathf.Max(1f, StatsOf(def, state.upgradeLevel).maxHealth);
                sb.Append($"Tidal power: {(SeaCatalog.TidalPowerPerGenerator + SeaCatalog.TidalPowerPerUpgrade * Mathf.Min(3, state.upgradeLevel)) * StaffingOf(state) * Mathf.Clamp01(hp):0.0}\n");
                break;
            case SeaRole.Salt:
                sb.Append($"Salt preserves the catch: fishing and whaling bring {Mathf.Min(3, SeaCache.salt) * 0.06f:P0} more food <size=80%><color=#aaa>(+6% a pan, up to 3)</color></size>\n");
                break;
            case SeaRole.Kelp:
            case SeaRole.Ranch:
                sb.Append($"Output: {Pct(f.all > 0 ? f.all : 1f)}{(OnOceanWorld ? " <size=80%><color=#9df>(ocean world)</color></size>" : "")}\n");
                break;
            case SeaRole.Housing:
                if (info.site == SeaSite.Floating) sb.Append("<size=80%><color=#9df>Out on the water: raiders on foot can't reach it</color></size>\n");
                break;
            case SeaRole.Wall:
                sb.Append("<size=80%><color=#9cf>Holds back floodwater, like a levee</color></size>\n");
                break;
        }
    }

    /// <summary>Raiders on foot leave alone buildings that stand wholly out on the water (they can't reach them).</summary>
    public bool SeaOutOfReach(BuildingState b, BuildingDefinition def)
    {
        if (Map == null || !SeaCatalog.TryGet(def, out var info)) return false;
        if (info.site != SeaSite.Floating && info.site != SeaSite.Offshore) return false;
        return true;
    }

    /// <summary>Sea creatures and the monster for the panel.</summary>
    public string SeaLifeSummary(out SeaPod monster)
    {
        monster = null;
        int whales = 0, hunters = 0;
        foreach (var p in Sea.pods)
        {
            var sp = SeaCatalog.SpeciesOf(p.species);
            if (sp == null) continue;
            if (sp.monster) monster = p;
            else if (sp.role == WildRole.Predator) hunters += p.count;
            else whales += p.count;
        }
        var parts = new List<string>();
        if (whales > 0) parts.Add($"{whales} {(OnAlienWorld ? "rays" : "whales")}");
        if (hunters > 0) parts.Add($"<color=#fb7>{hunters} {(OnAlienWorld ? "void eels" : "serpents")}</color>");
        if (monster != null) parts.Add($"<color=#f88>{SeaCatalog.SpeciesOf(monster.species).name}</color>");
        return parts.Count == 0 ? "" : string.Join(" · ", parts);
    }
}
