using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Natural disasters, played out on the map: floods along the rivers, wildfires through the
/// forests, eruptions from the hills of volcanic worlds, earthquakes, plague in crowded towns and,
/// in the space ages, meteor showers. They're rare (never in the first 20 minutes, then one every
/// quarter of an hour or so on the world you watch), they come with a warning where one makes
/// sense, and each has its counters: levees and walls hold back water and lava, wells and fire
/// brigades put fires out (cleared land stops them spreading), a seismoscope warns early, ash
/// shelters and shield domes cover what's under them, wells and healers fight sickness, and two
/// pieces of research (raised foundations, quake-proof frames) soften the worst.
/// Each world keeps its own; worlds in the background only play out what's already under way.
/// Settings: State.disastersEnabled, State.disasterFrequency (and the static DisastersAllowed).
/// </summary>
public partial class Settlement
{
    /// <summary>No disaster before this much play, nor this soon after loading or changing worlds.</summary>
    public const float DisFirstAfter = 1200f, DisGrace = 180f;
    private const float DisStepTime = 0.5f;

    /// <summary>Master switch (code, tests): false stops new disasters everywhere. Per game: State.disastersEnabled.</summary>
    public static bool DisastersAllowed = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void DisResetStatics() => DisastersAllowed = true;

    /// <summary>A disaster on the world you're looking at was announced, struck or ended.</summary>
    public event Action<DisasterState> DisasterNews;

    /// <summary>A fire crew threw water from 'from' onto a fire at 'to' (the view draws it).</summary>
    public struct DisSplash { public Vector2 from, to; }
    public readonly DisSplash[] DisSplashes = new DisSplash[16];
    public int DisSplashCount { get; private set; }

    public bool DisastersOn => DisastersAllowed && State.disastersEnabled && State.disasterFrequency > 0.01f;

    /// <summary>The random event cards for wildfire and plague give way to the real thing.</summary>
    private bool DisReplacesCards => DisastersOn;

    private float disStepTimer;
    private int disSeenWorld = -1;
    private float disGraceUntil;

    private readonly Dictionary<int, HashSet<int>> disOut = new Dictionary<int, HashSet<int>>();
    private readonly Dictionary<int, HashSet<long>> disTileCache = new Dictionary<int, HashSet<long>>();
    private readonly HashSet<long> disKnown = new HashSet<long>();
    private readonly Dictionary<long, BuildingState> disOcc = new Dictionary<long, BuildingState>();
    private readonly List<BuildingState> disPick = new List<BuildingState>();
    private readonly List<(string kind, float weight, Vector2 at)> disPlans = new List<(string, float, Vector2)>();

    private struct DisGuardAt { public Vector2 at; public float r2, power; public DisGuardKind kind; }
    private readonly List<DisGuardAt> disGuards = new List<DisGuardAt>();

    private static readonly Vector2Int[] DisDirs8 =
    {
        new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1),
        new Vector2Int(1, 1), new Vector2Int(-1, 1), new Vector2Int(1, -1), new Vector2Int(-1, -1),
    };

    private static long DisKey(int x, int y) => ((long)x << 32) ^ (uint)y;
    private static long DisKey(Vector2Int c) => DisKey(c.x, c.y);

    // ---------- Save and lookups ----------

    private DisasterSave DisSave
    {
        get
        {
            if (State.disasters == null) State.disasters = new DisasterSave();
            return State.disasters;
        }
    }

    private DisasterWorld DisWorldOf(int world)
    {
        var list = DisSave.worlds;
        for (int i = 0; i < list.Count; i++) if (list[i].world == world) return list[i];
        var w = new DisasterWorld { world = world };
        list.Add(w);
        return w;
    }

    /// <summary>Disasters (and their fading scars) on the world you're looking at.</summary>
    public List<DisasterState> DisastersHere => DisWorldOf(State.currentWorld).active;

    /// <summary>The disaster to warn about on this world (the newest not yet over), or null.</summary>
    public DisasterState DisasterAlert
    {
        get
        {
            var list = DisastersHere;
            for (int i = list.Count - 1; i >= 0; i--) if (!list[i].over) return list[i];
            return null;
        }
    }

    private HashSet<int> DisOutSet(int world)
    {
        if (!disOut.TryGetValue(world, out var set)) disOut[world] = set = new HashSet<int>();
        return set;
    }

    /// <summary>Flooded, burning or buried under lava: the building can't work (read by production).</summary>
    public float DisWorkFactor(BuildingState building) =>
        disOut.TryGetValue(State.currentWorld, out var set) && set.Count > 0 && set.Contains(building.instanceId) ? 0f : 1f;

    /// <summary>Is this building out of action because of a disaster right now?</summary>
    public bool DisIsOut(BuildingState building) => DisWorkFactor(building) <= 0f;

    private BuildingState DisBuilding(int id)
    {
        var list = State.buildings;
        for (int i = 0; i < list.Count; i++) if (list[i].instanceId == id) return list[i];
        return null;
    }

    private Vector2 DisCentreOf(BuildingState b, BuildingDefinition def) =>
        new Vector2(b.cell.x + def.size.x * 0.5f, b.cell.y + def.size.y * 0.5f);

    private BuildingState DisTownCentre()
    {
        var list = State.buildings;
        for (int i = 0; i < list.Count; i++)
            if (Database.GetBuilding(list[i].definitionId)?.isTownCenter == true) return list[i];
        return null;
    }

    private DepositType DisForest()
    {
        if (Map == null) return null;
        foreach (var d in Map.Deposits) if (d != null && d.id == "Forest") return d;
        return null;
    }

    // ---------- Guards: what protects where ----------

    private int disGuardsWorld = -1;

    private void DisGatherGuards()
    {
        disGuards.Clear();
        disGuardsWorld = State.currentWorld;
        UtlAddWaterGuards();   // fountains, towers, pumps... (Utilities.cs)
        foreach (var b in State.buildings)
        {
            var def = Database.GetBuilding(b.definitionId);
            if (def == null || !IsWorking(b, def)) continue;
            var c = DisCentreOf(b, def);
            if (DisCatalog.Guards.TryGetValue(def.id, out var guards))
                foreach (var g in guards)
                    disGuards.Add(new DisGuardAt { at = c, r2 = g.radius * g.radius, power = g.power, kind = g.kind });
            if (def.trainsUnit != null && def.trainsUnit.healer)
                disGuards.Add(new DisGuardAt { at = c, r2 = DisCatalog.HealRadius * DisCatalog.HealRadius, power = 1f, kind = DisGuardKind.Heal });
        }
    }

    /// <summary>The best guard of a kind over a point (0 = none), and where it stands.</summary>
    private float DisGuard(Vector2 p, DisGuardKind kind, out Vector2 from)
    {
        float best = 0f;
        from = p;
        for (int i = 0; i < disGuards.Count; i++)
        {
            var g = disGuards[i];
            if (g.kind != kind || g.power <= best || (g.at - p).sqrMagnitude > g.r2) continue;
            best = g.power;
            from = g.at;
        }
        return best;
    }

    private bool DisHasGuard(DisGuardKind kind)
    {
        foreach (var g in disGuards) if (g.kind == kind) return true;
        return false;
    }

    /// <summary>Is a meteor falling here stopped (a shield dome over it, or planetary shields)?</summary>
    public bool DisBlocked(Vector2 at)
    {
        if (Researched("PlanetaryShields")) return true;
        if (disGuardsWorld != State.currentWorld || disGuards.Count == 0) DisGatherGuards();   // (a background world may have just used the list)
        return DisGuard(at, DisGuardKind.Meteor, out _) > 0f;
    }

    /// <summary>Extra warning from this world's best seismoscope (0 without one).</summary>
    private float DisSeismoBonus()
    {
        float best = 0f;
        foreach (var b in State.buildings)
        {
            if (b.definitionId != DisCatalog.Seismoscope) continue;
            var def = Database.GetBuilding(b.definitionId);
            if (!IsWorking(b, def)) continue;
            best = Mathf.Max(best, DisCatalog.SeismoWarning + DisCatalog.SeismoPerUpgrade * Mathf.Min(2, b.upgradeLevel));
        }
        return best;
    }

    private void DisAddSplash(Vector2 from, Vector2 to)
    {
        if (offWorld) return;
        DisSplashes[DisSplashCount % DisSplashes.Length] = new DisSplash { from = from, to = to };
        DisSplashCount++;
    }

    // ---------- The clock ----------

    /// <summary>The world you're watching: called from Tick.</summary>
    private void DisTick(float dt)
    {
        if (disSeenWorld != State.currentWorld)
        {
            // Just loaded, or just changed worlds: give the player a moment.
            disSeenWorld = State.currentWorld;
            disGraceUntil = State.playTime + DisGrace;
        }
        disStepTimer += dt;
        if (disStepTimer < DisStepTime) return;
        float step = Mathf.Min(disStepTimer, 2f);
        disStepTimer = 0f;
        DisRun(step, false);
    }

    /// <summary>A world in the background: what's under way plays out; nothing new starts there.</summary>
    private void DisTickBackground(float dt) => DisRun(Mathf.Min(dt, 2f), true);

    private void DisRun(float dt, bool background)
    {
        var dw = DisWorldOf(State.currentWorld);
        DisOutSet(State.currentWorld).Clear();
        if (dw.active.Count > 0)
        {
            DisGatherGuards();
            for (int i = dw.active.Count - 1; i >= 0; i--)
            {
                var d = dw.active[i];
                DisAdvance(d, dt);
                if (d.over && d.marks.Count == 0)
                {
                    dw.active.RemoveAt(i);
                    disTileCache.Remove(d.id);
                }
            }
        }
        if (background || !DisastersOn || Map == null) return;
        foreach (var d in dw.active) if (!d.over) return;   // one at a time
        if (dw.nextIn < 0f) dw.nextIn = Random.Range(300f, 900f);
        if (State.playTime < DisFirstAfter || State.playTime < disGraceUntil) return;
        dw.nextIn -= dt * Mathf.Clamp(State.disasterFrequency, 0f, 5f);
        if (dw.nextIn > 0f) return;
        bool raid = FreeModeController.Instance != null && FreeModeController.Instance.WaveInProgress;
        if (raid || State.secondsUntilWave < 30f)
        {
            dw.nextIn = 30f;
            return;
        }
        dw.nextIn = Random.Range(1000f, 1600f);
        if (!DisStart(dw)) dw.nextIn = 150f;
    }

    // ---------- Choosing one ----------

    private BuildingState DisRandomBuilding(bool housesOnly = false)
    {
        disPick.Clear();
        foreach (var b in State.buildings)
        {
            if (!b.IsBuilt) continue;
            var def = Database.GetBuilding(b.definitionId);
            if (def == null || def.IsWall) continue;
            if (housesOnly && def.housing <= 0) continue;
            disPick.Add(b);
        }
        return disPick.Count == 0 ? null : disPick[Random.Range(0, disPick.Count)];
    }

    private bool DisStart(DisasterWorld dw)
    {
        DisGatherGuards();
        string last = dw.recent.Count > 0 ? dw.recent[dw.recent.Count - 1] : null;
        disPlans.Clear();
        float total = 0f;
        void Add(string kind, float weight, Vector2 at)
        {
            if (weight <= 0f) return;
            if (kind == last) weight *= 0.3f;
            disPlans.Add((kind, weight, at));
            total += weight;
        }
        if (DisPlanFlood(out var at)) Add("Flood", DisFloodWeight(), at);
        if (DisPlanFire(out at)) Add("Wildfire", DisFireWeight(), at);
        if (DisPlanVolcano(out at, out float vw)) Add("Volcano", vw, at);
        if (DisPlanQuake(out at, out float qw)) Add("Quake", qw, at);
        if (DisPlanPlague(out at, out float pw)) Add("Plague", pw, at);
        if (DisPlanMeteors(out at, out float mw)) Add("Meteors", mw, at);
        if (disPlans.Count == 0) return false;
        float pick = Random.value * total;
        foreach (var p in disPlans)
        {
            pick -= p.weight;
            if (pick > 0f) continue;
            DisBegin(dw, p.kind, p.at);
            return true;
        }
        DisBegin(dw, disPlans[disPlans.Count - 1].kind, disPlans[disPlans.Count - 1].at);
        return true;
    }

    private bool DisPlanFlood(out Vector2 at)
    {
        at = default;
        if (CurrentClimate == Climate.Airless) return false;
        for (int tries = 0; tries < 14; tries++)
        {
            var b = DisRandomBuilding();
            if (b == null) return false;
            var def = Database.GetBuilding(b.definitionId);
            if (RiverCatalog.KindOf(def) != RiverCatalog.Kind.None) continue;
            int cx = b.cell.x + def.size.x / 2, cy = b.cell.y + def.size.y / 2;
            int best = int.MaxValue;
            for (int dy = -7; dy <= 7; dy++)
                for (int dx = -7; dx <= 7; dx++)
                {
                    int x = cx + dx, y = cy + dy, d2 = dx * dx + dy * dy;
                    if (d2 >= best || !Map.InBounds(x, y) || Map.GetTerrain(x, y) != TerrainType.Water) continue;
                    bool shore = false;
                    for (int k = 0; k < 4 && !shore; k++)
                    {
                        var n = DisDirs8[k];
                        shore = Map.InBounds(x + n.x, y + n.y) && Map.GetTerrain(x + n.x, y + n.y) == TerrainType.Grass;
                    }
                    if (!shore) continue;
                    best = d2;
                    at = new Vector2(x + 0.5f, y + 0.5f);
                }
            if (best != int.MaxValue) return true;
        }
        return false;
    }

    private float DisFloodWeight()
    {
        var c = CurrentClimate;
        float w = 1f;
        w *= CurrentSeason switch
        {
            Season.Spring => 2.2f, Season.Rains => 2.4f, Season.WetSeason => 2.2f,
            Season.Winter => 0.4f, Season.DrySeason => 0.3f, _ => 1f,
        };
        w *= CurrentWeather switch
        {
            WeatherKind.Rain => 1.8f, WeatherKind.Storm => 2.2f, WeatherKind.Snow => 0.5f, WeatherKind.HeatWave => 0.3f, _ => 1f,
        };
        if (c == Climate.Cold) w *= 0.6f;
        if (c == Climate.Volcanic) w *= 0.5f;
        return w;
    }

    private bool DisPlanFire(out Vector2 at)
    {
        at = default;
        var forest = DisForest();
        if (forest == null || CurrentClimate == Climate.Airless) return false;
        for (int tries = 0; tries < 10; tries++)
        {
            var b = DisRandomBuilding();
            if (b == null) return false;
            var def = Database.GetBuilding(b.definitionId);
            int cx = b.cell.x + def.size.x / 2, cy = b.cell.y + def.size.y / 2, count = 0, seen = 0;
            for (int dy = -10; dy <= 10; dy++)
                for (int dx = -10; dx <= 10; dx++)
                {
                    int x = cx + dx, y = cy + dy;
                    if (Map.GetDeposit(x, y) != forest) continue;
                    count++;
                    int d2 = dx * dx + dy * dy;
                    if (d2 < 9 || d2 > 100) continue;
                    if (Random.Range(0, ++seen) == 0) at = new Vector2(x + 0.5f, y + 0.5f);   // one of them, evenly
                }
            if (count >= 14 && seen > 0) return true;
        }
        return false;
    }

    private float DisFireWeight()
    {
        float w = CurrentSeason switch
        {
            Season.Summer => 1.8f, Season.DrySeason => 2f, Season.Autumn => 1.1f, Season.Winter => 0.25f,
            Season.Spring => 0.7f, Season.Rains => 0.3f, Season.WetSeason => 0.5f, _ => 1f,
        };
        w *= CurrentWeather switch
        {
            WeatherKind.HeatWave => 2.4f, WeatherKind.Storm => 1.6f, WeatherKind.Clear => 1.1f, WeatherKind.Rain => 0.2f,
            WeatherKind.Snow => 0.1f, WeatherKind.Fog => 0.6f, _ => 1f,
        };
        if (HasTrait(Scorching)) w *= 1.5f;
        if (CurrentClimate == Climate.Cold) w *= 0.5f;
        return w;
    }

    private bool DisPlanVolcano(out Vector2 at, out float weight)
    {
        at = default;
        weight = 0f;
        var tc = DisTownCentre();
        if (tc == null) return false;
        bool volcanic = CurrentClimate == Climate.Volcanic;
        for (int i = 0; i < 300; i++)
        {
            float a = Random.value * Mathf.PI * 2f, r = Random.Range(10f, 26f);
            int x = tc.cell.x + Mathf.RoundToInt(Mathf.Cos(a) * r), y = tc.cell.y + Mathf.RoundToInt(Mathf.Sin(a) * r);
            if (!Map.InBounds(x, y) || Map.GetTerrain(x, y) != TerrainType.Hill) continue;
            if (!volcanic && Map.BiomeAt(x, y) != Biome.Volcanic) continue;   // at home, only the fire mountains
            at = new Vector2(x + 0.5f, y + 0.5f);
            weight = volcanic ? 1.5f : 0.35f;
            return true;
        }
        return false;
    }

    private bool DisPlanQuake(out Vector2 at, out float weight)
    {
        at = default;
        weight = 0f;
        var tc = DisTownCentre();
        if (tc == null) return false;
        int hills = 0;
        for (int i = 0; i < 200; i++)
        {
            int x = tc.cell.x + Random.Range(-20, 21), y = tc.cell.y + Random.Range(-20, 21);
            if (Map.InBounds(x, y) && Map.GetTerrain(x, y) == TerrainType.Hill) hills++;
        }
        weight = 0.55f * (hills >= 25 ? 1.6f : hills >= 8 ? 1f : 0.6f);
        if (HasTrait(Quakes)) weight *= 2f;
        if (CurrentClimate == Climate.Volcanic) weight *= 1.3f;
        at = new Vector2(tc.cell.x, tc.cell.y) + Random.insideUnitCircle * 10f;
        return true;
    }

    private bool DisPlanPlague(out Vector2 at, out float weight)
    {
        at = default;
        weight = 0f;
        float pop = State.population;
        if (pop < 40f) return false;
        var house = DisRandomBuilding(true);
        if (house == null) return false;
        var hd = Database.GetBuilding(house.definitionId);
        at = DisCentreOf(house, hd);
        int near = 0;
        foreach (var b in State.buildings)
        {
            if (!b.IsBuilt) continue;
            var def = Database.GetBuilding(b.definitionId);
            if (def == null || def.housing <= 0) continue;
            if ((DisCentreOf(b, def) - at).sqrMagnitude <= 36f) near++;
        }
        weight = Mathf.Clamp((pop - 40f) / 250f + 0.2f, 0.2f, 1.2f) * (near >= 6 ? 1.6f : near >= 3 ? 1f : 0.5f);
        if (DisGuard(at, DisGuardKind.Water, out _) > 0f) weight *= 0.5f;   // clean water
        return true;
    }

    private bool DisPlanMeteors(out Vector2 at, out float weight)
    {
        at = default;
        weight = 0f;
        bool airless = CurrentClimate == Climate.Airless;
        if (State.ageIndex < 12 && !airless) return false;
        var b = DisRandomBuilding();
        if (b == null) return false;
        at = DisCentreOf(b, Database.GetBuilding(b.definitionId));
        weight = airless ? 1.2f : 0.8f;
        if (Researched("PlanetaryShields")) weight *= 0.5f;
        return true;
    }

    // ---------- Names and words ----------

    public static string DisTitle(DisasterState d, int age) => d.kind switch
    {
        "Flood" => "Flood",
        "Wildfire" => "Wildfire",
        "Volcano" => "Eruption",
        "Quake" => "Earthquake",
        "Plague" => Era(age, "The Coughing Sickness", "Plague", "An Outbreak"),
        "Meteors" => "Meteor Shower",
        _ => d.kind,
    };

    /// <summary>Where a spot is from the town centre, in words ("to the north-east").</summary>
    private string DisWhere(Vector2 at)
    {
        var tc = DisTownCentre();
        if (tc == null) return "nearby";
        var v = at - new Vector2(tc.cell.x + 1f, tc.cell.y + 1f);
        if (v.magnitude < 6f) return "in the middle of town";
        string[] dirs = { "east", "north-east", "north", "north-west", "west", "south-west", "south", "south-east" };
        int i = Mathf.RoundToInt(Mathf.Atan2(v.y, v.x) / (Mathf.PI / 4f));
        return "to the " + dirs[((i % 8) + 8) % 8];
    }

    /// <summary>One line for the alert under the top bar: what's happening and how long.</summary>
    public string DisStatus(DisasterState d)
    {
        string title = $"<color=#ffb070><b>{DisTitle(d, State.ageIndex)}</b></color>";
        if (!d.struck) return $"{title}  <color=#ddd>in {Mathf.CeilToInt(Mathf.Max(0f, d.warn))}s</color>";
        string what = d.kind switch
        {
            "Flood" => $"{d.tiles.Count} tiles under water",
            "Wildfire" => $"{d.tiles.Count} tiles burning" + (d.hit.Count > 0 ? $", {d.hit.Count} buildings alight" : ""),
            "Volcano" => "lava and ash",
            "Quake" => "the ground is shaking",
            "Plague" => $"{d.hit.Count} houses sick",
            "Meteors" => "meteors falling",
            _ => "",
        };
        return $"{title}  <color=#ddd>{what}</color>";
    }

    // ---------- Beginning, striking, ending ----------

    private void DisBegin(DisasterWorld dw, string kind, Vector2 at)
    {
        var d = new DisasterState { id = DisSave.nextId++, kind = kind, centre = at, severity = Random.Range(0.75f, 1.25f) };
        string where = DisWhere(at);
        string msg;
        float seismo = DisSeismoBonus();
        switch (kind)
        {
            case "Flood":
                d.warn = 25f;
                d.radius = 9f;
                msg = $"<color=#7cf>The river is rising</color> {where}: it will burst its banks in about 25 seconds.";
                if (!State.buildings.Exists(b => b.definitionId == DisCatalog.Levee || b.definitionId == DisCatalog.FloodBarrier))
                    msg += " <color=#aaa>(Levees along the bank keep floodwater out.)</color>";
                break;
            case "Wildfire":
                d.radius = 12f;
                d.left = 240f;
                d.tiles.Add(new Vector2Int(Mathf.FloorToInt(at.x), Mathf.FloorToInt(at.y)));
                d.times.Add(12f);
                d.struck = true;
                msg = (CurrentWeather == WeatherKind.Storm ? "<color=#f96>Lightning set the woods alight</color> " : "<color=#f96>A wildfire has broken out</color> in the woods ") + where + "!";
                if (!DisHasGuard(DisGuardKind.Fire)) msg += " <color=#aaa>(Wells and fire brigades put fires out; cleared land stops them spreading.)</color>";
                break;
            case "Volcano":
                d.warn = 30f + seismo;
                d.warnedEarly = seismo > 0f;
                d.radius = 14f;
                msg = $"<color=#f84>The ground trembles and the mountain smokes</color> {where}: an eruption is coming in about {Mathf.RoundToInt(d.warn)} seconds."
                      + (d.warnedEarly ? " Your seismoscope gave early warning." : "");
                break;
            case "Quake":
                d.warn = seismo > 0f ? 20f + seismo : 4f;
                d.warnedEarly = seismo > 0f;
                d.radius = 16f;
                msg = d.warnedEarly
                    ? $"<color=#fc8>The seismoscope's dragon has dropped its ball</color>: an earthquake is coming in about {Mathf.RoundToInt(d.warn)} seconds!"
                    : "<color=#fc8>The ground shudders...</color> <color=#aaa>(A seismoscope would have warned you.)</color>";
                break;
            case "Plague":
                d.radius = 4f;
                d.left = 150f;
                d.struck = true;
                msg = $"<color=#bd8>{DisTitle(d, State.ageIndex)}</color>: a sickness is spreading through the houses {where}.";
                if (!DisHasGuard(DisGuardKind.Heal) && !DisHasGuard(DisGuardKind.Water)) msg += " <color=#aaa>(Wells and healers slow it down.)</color>";
                break;
            default:   // Meteors
                d.warn = 20f;
                d.radius = 16f;
                msg = $"<color=#f9c>Meteors are falling</color> {where}: the first will land in about 20 seconds."
                      + (Researched("PlanetaryShields") ? " The planetary shields are up." : DisHasGuard(DisGuardKind.Meteor) ? "" : " <color=#aaa>(Shield domes stop them.)</color>");
                break;
        }
        dw.active.Add(d);
        dw.recent.Add(kind);
        while (dw.recent.Count > 3) dw.recent.RemoveAt(0);
        Notify(msg);
        if (!offWorld)
        {
            GameAudio.UI(kind == "Plague" ? "plague_bell" : "raid_warning", 0.7f);
            DisasterNews?.Invoke(d);
        }
        RaiseChanged();
    }

    private void DisStrike(DisasterState d)
    {
        d.struck = true;
        float now = State.playTime;
        switch (d.kind)
        {
            case "Flood":
            {
                DisFloodTiles(d, 2 + Mathf.RoundToInt(1.5f * d.severity));
                d.left = Random.Range(50f, 80f) * d.severity;
                string held = d.saved > 0 ? $" Levees and walls held the water back in {d.saved} places." : "";
                Notify($"<color=#7cf>The river has burst its banks!</color> {d.tiles.Count} tiles are under water.{held}");
                if (!offWorld) GameAudio.UI("river_splash");
                break;
            }
            case "Volcano":
            {
                d.tiles.Add(new Vector2Int(Mathf.FloorToInt(d.centre.x), Mathf.FloorToInt(d.centre.y)));
                d.times.Add(0f);
                d.left = Random.Range(40f, 60f);
                d.ring = 0;
                var w = CurrentWorld;
                w.weather = (int)WeatherKind.AshFall;
                w.weatherLeft = Mathf.Max(w.weatherLeft, d.left + 40f);
                // People caught out in the ash: shelters keep them safe.
                int housing = 0, exposed = 0;
                foreach (var b in State.buildings)
                {
                    var def = Database.GetBuilding(b.definitionId);
                    if (def == null || def.housing <= 0 || !IsWorking(b, def)) continue;
                    var c = DisCentreOf(b, def);
                    housing += def.housing;
                    if ((c - d.centre).sqrMagnitude <= d.radius * d.radius && DisGuard(c, DisGuardKind.Ash, out _) <= 0f) exposed += def.housing;
                }
                int n = housing > 0 ? Mathf.FloorToInt(State.population * 0.04f * d.severity * exposed / housing) : 0;
                if (n > 0) { State.population = Mathf.Max(1f, State.population - n); d.lost += n; }
                Notify($"<color=#f84>The mountain erupts!</color> Lava pours down its side and ash falls on the town." +
                       (n > 0 ? $" {n} people were caught in the ash." : " Your people sheltered from the ash."));
                if (!offWorld) { GameAudio.UI("explosion"); GameAudio.UI("earthquake", 0.8f); ApplyWeatherFx(); WeatherChanged?.Invoke(); }
                break;
            }
            case "Quake":
            {
                float mult = (Researched(DisCatalog.TechQuakeFrames) ? 0.6f : 1f) * (Researched("PlanetaryShields") ? 0.5f : 1f) * (d.warnedEarly ? 0.85f : 1f);
                float r = d.radius;
                foreach (var b in State.buildings.ToArray())
                {
                    if (!b.IsBuilt || b.health <= 0) continue;
                    var def = Database.GetBuilding(b.definitionId);
                    if (def == null) continue;
                    float dist = Vector2.Distance(DisCentreOf(b, def), d.centre);
                    if (dist > r) continue;
                    float f = 1f - dist / r;
                    bool hill = Map != null && Map.GetTerrain(b.cell.x, b.cell.y) == TerrainType.Hill;
                    DisHurt(b, def, (0.05f + 0.2f * f) * d.severity * mult * (hill ? 1.3f : 1f), 0.08f, d);
                }
                for (int i = 0; i < 14; i++)
                {
                    var p = d.centre + Random.insideUnitCircle * r * 0.6f;
                    DisMark(d, new Vector2Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y)), now + Random.Range(70f, 100f));
                }
                d.left = 6f;
                Notify($"<color=#fc8>An earthquake!</color> {d.damaged.Count} buildings were shaken and cracked." +
                       (Researched(DisCatalog.TechQuakeFrames) ? " Quake-proof frames kept the worst off." : ""));
                if (!offWorld) GameAudio.UI("earthquake");
                break;
            }
            case "Meteors":
                d.left = 22f;
                d.pulse = 0f;
                if (!offWorld) GameAudio.UI("meteor");
                break;
        }
        if (!offWorld) DisasterNews?.Invoke(d);
        RaiseChanged();
    }

    private void DisEnd(DisasterState d)
    {
        if (d.over) return;
        d.over = true;
        float now = State.playTime;
        // what was live becomes a fading scar
        for (int i = 0; i < d.tiles.Count; i++)
        {
            if (d.kind == "Meteors") continue;
            DisMark(d, d.tiles[i], now + (d.kind == "Volcano" ? Random.Range(80f, 120f) : Random.Range(12f, 30f)));
        }
        d.tiles.Clear();
        d.times.Clear();
        d.hit.Clear();
        d.hitTimes.Clear();
        disTileCache.Remove(d.id);

        string title = DisTitle(d, State.ageIndex);
        int hurt = d.damaged.Count;
        var sb = new StringBuilder();
        switch (d.kind)
        {
            case "Flood": sb.Append("The floodwater has gone down."); break;
            case "Wildfire": sb.Append($"The wildfire is out. {d.burnt} tiles of forest burnt."); break;
            case "Volcano": sb.Append("The eruption is over; the lava is cooling."); break;
            case "Quake": sb.Append("The ground is still again."); break;
            case "Plague": sb.Append("The sickness has passed."); break;
            default: sb.Append("The meteor shower is over."); break;
        }
        if (hurt > 0) sb.Append($" {hurt} building{(hurt == 1 ? " was" : "s were")} damaged");
        if (d.ruined > 0) sb.Append($", {d.ruined} ruined");
        if (hurt > 0) sb.Append(" (repair them, or let auto-repair).");
        if (d.lost > 0) sb.Append($" {d.lost} people died.");
        if (d.saved > 0)
            sb.Append(d.kind switch
            {
                "Wildfire" => $" Your fire crews put out {d.saved} fires.",
                "Meteors" => $" Shields stopped {d.saved} meteors.",
                "Flood" => $" Levees held in {d.saved} places.",
                "Volcano" => $" Walls turned the lava aside in {d.saved} places.",
                _ => "",
            });
        Notify(sb.ToString());

        if (hurt >= 4 || d.ruined > 0 || d.lost >= 5)
        {
            var c = new StringBuilder($"{title} on {CurrentWorld.name}:");
            if (hurt > 0) c.Append($" {hurt} buildings damaged");
            if (d.ruined > 0) c.Append($", {d.ruined} ruined");
            if (d.lost > 0) c.Append(hurt > 0 ? $", {d.lost} people lost" : $" {d.lost} people lost");
            c.Append('.');
            XChronicle(XChronicleKind.Disaster, c.ToString());
        }
        if (!offWorld) DisasterNews?.Invoke(d);
        RaiseChanged();
    }

    // ---------- Each step ----------

    private void DisAdvance(DisasterState d, float dt)
    {
        float now = State.playTime;
        // scars fade
        if (d.marks.Count > 0)
        {
            int j = 0;
            for (int i = 0; i < d.marks.Count; i++)
            {
                if (d.markTimes[i] <= now) continue;
                d.marks[j] = d.marks[i];
                d.markTimes[j] = d.markTimes[i];
                j++;
            }
            if (j < d.marks.Count)
            {
                d.marks.RemoveRange(j, d.marks.Count - j);
                d.markTimes.RemoveRange(j, d.markTimes.Count - j);
            }
        }
        if (d.over) return;
        if (!d.struck)
        {
            d.warn -= dt;
            if (d.warn <= 0f) DisStrike(d);
            return;
        }
        switch (d.kind)
        {
            case "Flood": DisFloodStep(d, dt); break;
            case "Wildfire": DisFireStep(d, dt); break;
            case "Volcano": DisVolcanoStep(d, dt); break;
            case "Plague": DisPlagueStep(d, dt); break;
            case "Meteors": DisMeteorStep(d, dt); break;
            default:
                d.left -= dt;
                if (d.left <= 0f) DisEnd(d);
                break;
        }
    }

    private void DisMark(DisasterState d, Vector2Int tile, float until)
    {
        if (d.marks.Count >= 900) return;
        d.marks.Add(tile);
        d.markTimes.Add(until);
    }

    /// <summary>Damage a building by a share of its health, never below 'floorShare' of it. True if it was hurt.</summary>
    private bool DisHurt(BuildingState b, BuildingDefinition def, float share, float floorShare, DisasterState d)
    {
        if (b == null || def == null || !b.IsBuilt || b.health <= 0) return false;
        float max = StatsOf(def, b.upgradeLevel).maxHealth;
        float floor = Mathf.Round(max * floorShare);
        if (b.health <= floor) return false;
        share *= CivicsDisasterFactor;   // laws (Civics.cs)
        b.health = Mathf.Max(floor, b.health - Mathf.Max(1f, Mathf.Round(max * share)));
        if (!d.damaged.Contains(b.instanceId)) d.damaged.Add(b.instanceId);
        if (b.health <= 0f)
        {
            d.ruined++;
            if (def.IsWall) wallsDirty = true;
            Notify($"The {DisTitle(d, State.ageIndex).ToLowerInvariant()} ruined a {Name(def)}! Repair it to use it again.");
            if (!offWorld) GameAudio.AtBuilding("collapse", b, def);
            RaiseChanged();
        }
        return true;
    }

    private bool DisStopsWater(int x, int y)
    {
        var wall = Map.WallAt(x, y);
        return wall == WallTile.Wall || wall == WallTile.Gate;
    }

    /// <summary>Flood the land next to the water near the centre, up to 'reach' tiles from it; walls, levees and hills hold it back.</summary>
    private void DisFloodTiles(DisasterState d, int reach)
    {
        d.tiles.Clear();
        d.times.Clear();
        if (Map == null) return;
        var queue = new Queue<(int x, int y, int dist)>();
        var seen = new HashSet<long>();
        int r = Mathf.CeilToInt(d.radius);
        int cx = Mathf.FloorToInt(d.centre.x), cy = Mathf.FloorToInt(d.centre.y);
        for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
            {
                int x = cx + dx, y = cy + dy;
                if (dx * dx + dy * dy > r * r || !Map.InBounds(x, y) || Map.GetTerrain(x, y) != TerrainType.Water) continue;
                seen.Add(DisKey(x, y));
                queue.Enqueue((x, y, 0));
            }
        float limit = (r + reach + 1) * (r + reach + 1);
        while (queue.Count > 0 && d.tiles.Count < 420)
        {
            var (x, y, dist) = queue.Dequeue();
            if (dist >= reach) continue;
            for (int k = 0; k < 4; k++)
            {
                int nx = x + DisDirs8[k].x, ny = y + DisDirs8[k].y;
                long key = DisKey(nx, ny);
                if (!seen.Add(key)) continue;
                if ((nx - cx) * (nx - cx) + (ny - cy) * (ny - cy) > limit || !Map.InBounds(nx, ny)) continue;
                if (Map.GetTerrain(nx, ny) != TerrainType.Grass) continue;   // water's already water; hills stay dry
                if (DisStopsWater(nx, ny)) { d.saved++; continue; }
                // ragged edges at the far reach
                if (dist + 1 == reach && Random.value < 0.35f) continue;
                d.tiles.Add(new Vector2Int(nx, ny));
                d.times.Add(dist + 1);
                queue.Enqueue((nx, ny, dist + 1));
            }
        }
        disTileCache.Remove(d.id);
    }

    private HashSet<long> DisTileSet(DisasterState d)
    {
        if (disTileCache.TryGetValue(d.id, out var set) && set.Count == d.tiles.Count) return set;
        if (set == null) disTileCache[d.id] = set = new HashSet<long>();
        set.Clear();
        foreach (var t in d.tiles) set.Add(DisKey(t));
        return set;
    }

    private void DisFloodStep(DisasterState d, float dt)
    {
        d.left -= dt;
        d.pulse += dt;
        bool hurt = d.pulse >= 3f;
        if (hurt) d.pulse = 0f;
        var set = DisTileSet(d);
        var outSet = DisOutSet(State.currentWorld);
        float mult = Researched(DisCatalog.TechRaised) ? 0.5f : 1f;
        float reachSq = (d.radius + 8f) * (d.radius + 8f);
        foreach (var b in State.buildings)
        {
            if (!b.IsBuilt || b.health <= 0) continue;
            var def = Database.GetBuilding(b.definitionId);
            if (def == null || def.IsWall || RiverCatalog.KindOf(def) != RiverCatalog.Kind.None) continue;
            if ((DisCentreOf(b, def) - d.centre).sqrMagnitude > reachSq) continue;
            bool wet = false;
            for (int y = 0; y < def.size.y && !wet; y++)
                for (int x = 0; x < def.size.x && !wet; x++)
                    wet = set.Contains(DisKey(b.cell.x + x, b.cell.y + y));
            if (!wet) continue;
            outSet.Add(b.instanceId);
            if (hurt) DisHurt(b, def, 0.025f * mult, 0.3f, d);
        }
        if (d.left <= 0f) DisEnd(d);
    }

    private float DisFireSpread()
    {
        float s = CurrentWeather switch
        {
            WeatherKind.Rain => 0.35f, WeatherKind.Storm => 0.5f, WeatherKind.Snow => 0.25f, WeatherKind.Fog => 0.8f,
            WeatherKind.HeatWave => 1.6f, WeatherKind.Sandstorm => 1.3f, _ => 1f,
        };
        s *= CurrentSeason switch { Season.Summer => 1.25f, Season.DrySeason => 1.3f, Season.Winter => 0.5f, Season.Rains => 0.5f, _ => 1f };
        return s;
    }

    private void DisBuildOccupancy()
    {
        disOcc.Clear();
        foreach (var b in State.buildings)
        {
            if (!b.IsBuilt || b.health <= 0) continue;
            var def = Database.GetBuilding(b.definitionId);
            if (def == null || def.IsWall) continue;
            for (int y = 0; y < def.size.y; y++)
                for (int x = 0; x < def.size.x; x++)
                    disOcc[DisKey(b.cell.x + x, b.cell.y + y)] = b;
        }
    }

    private void DisFireStep(DisasterState d, float dt)
    {
        d.left -= dt;
        var forest = DisForest();
        var weather = CurrentWeather;
        bool wet = weather == WeatherKind.Rain || weather == WeatherKind.Storm || weather == WeatherKind.Snow;
        float spread = DisFireSpread(), k = dt / DisStepTime, now = State.playTime;
        bool late = d.left <= 0f;
        disKnown.Clear();
        foreach (var t in d.tiles) disKnown.Add(DisKey(t));
        foreach (var t in d.marks) disKnown.Add(DisKey(t));
        DisBuildOccupancy();
        int start = d.tiles.Count;
        for (int i = start - 1; i >= 0; i--)
        {
            if (i >= d.tiles.Count) continue;
            var t = d.tiles[i];
            var p = new Vector2(t.x + 0.5f, t.y + 0.5f);
            float guard = DisGuard(p, DisGuardKind.Fire, out var from);
            d.times[i] -= dt * (wet ? 2f : 1f) * (late ? 4f : 1f);
            bool doused = guard > 0f && Random.value < guard * 0.3f * k;
            if (doused) { d.saved++; DisAddSplash(from, p); }
            if (doused || d.times[i] <= 0f)
            {
                int last = d.tiles.Count - 1;
                d.tiles[i] = d.tiles[last]; d.times[i] = d.times[last];
                d.tiles.RemoveAt(last); d.times.RemoveAt(last);
                DisMark(d, t, now + Random.Range(130f, 170f));
                d.burnt++;
                continue;
            }
            if (late || d.tiles.Count >= 220) continue;
            float chance = 0.09f * spread * (guard > 0f ? 0.3f : 1f) * k;
            for (int n = 0; n < 8; n++)
            {
                int nx = t.x + DisDirs8[n].x, ny = t.y + DisDirs8[n].y;
                long key = DisKey(nx, ny);
                if (disKnown.Contains(key)) continue;
                if (Map.GetDeposit(nx, ny) == forest && forest != null)
                {
                    if (Random.value >= chance) continue;
                    d.tiles.Add(new Vector2Int(nx, ny));
                    d.times.Add(Random.Range(8f, 14f));
                    disKnown.Add(key);
                }
                else if (n < 4 && disOcc.TryGetValue(key, out var b) && !d.hit.Contains(b.instanceId) && Random.value < chance * 0.8f)
                {
                    d.hit.Add(b.instanceId);
                    d.hitTimes.Add(Random.Range(14f, 22f));
                }
            }
        }

        // Buildings alight.
        d.pulse += dt;
        bool hurt = d.pulse >= 2f;
        if (hurt) d.pulse = 0f;
        var outSet = DisOutSet(State.currentWorld);
        for (int i = d.hit.Count - 1; i >= 0; i--)
        {
            var b = DisBuilding(d.hit[i]);
            var def = b != null ? Database.GetBuilding(b.definitionId) : null;
            if (def == null || b.health <= 0)
            {
                d.hit.RemoveAt(i); d.hitTimes.RemoveAt(i);
                continue;
            }
            outSet.Add(b.instanceId);
            var c = DisCentreOf(b, def);
            float guard = DisGuard(c, DisGuardKind.Fire, out var from);
            d.hitTimes[i] -= dt * (wet ? 2f : 1f) * (guard > 0f ? 1.8f : 1f) * (late ? 3f : 1f);
            if (guard > 0f && Random.value < guard * 0.25f * k)
            {
                d.saved++;
                DisAddSplash(from, c);
                d.hitTimes[i] = 0f;
            }
            if (hurt) DisHurt(b, def, 0.045f * (guard > 0f ? 0.4f : 1f), 0.1f, d);
            if (d.hitTimes[i] <= 0f)
            {
                d.hit.RemoveAt(i); d.hitTimes.RemoveAt(i);
                continue;
            }
            // to the building next door
            if (!late && Random.value < 0.03f * spread * (guard > 0f ? 0.25f : 1f) * k)
            {
                int side = Random.Range(0, 4);
                int x = side == 0 ? b.cell.x - 1 : side == 1 ? b.cell.x + def.size.x : b.cell.x + Random.Range(0, def.size.x);
                int y = side == 2 ? b.cell.y - 1 : side == 3 ? b.cell.y + def.size.y : b.cell.y + Random.Range(0, def.size.y);
                if (side < 2) y = b.cell.y + Random.Range(0, def.size.y);
                if (disOcc.TryGetValue(DisKey(x, y), out var next) && next != b && !d.hit.Contains(next.instanceId))
                {
                    d.hit.Add(next.instanceId);
                    d.hitTimes.Add(Random.Range(14f, 22f));
                }
            }
        }
        if (d.tiles.Count == 0 && d.hit.Count == 0) DisEnd(d);
    }

    private void DisVolcanoStep(DisasterState d, float dt)
    {
        d.left -= dt;
        float now = State.playTime;
        // The lava spreads a ring at a time; walls turn it aside, water stops it.
        d.pulse += dt;
        int maxRing = Mathf.RoundToInt(3f + 3f * d.severity);
        if (d.pulse >= 1.6f && d.ring < maxRing && d.left > 0f)
        {
            d.pulse = 0f;
            var set = DisTileSet(d);
            int count = d.tiles.Count;
            for (int i = 0; i < count; i++)
            {
                if (Mathf.RoundToInt(d.times[i]) != d.ring) continue;
                var t = d.tiles[i];
                for (int n = 0; n < 4; n++)
                {
                    int nx = t.x + DisDirs8[n].x, ny = t.y + DisDirs8[n].y;
                    long key = DisKey(nx, ny);
                    if (set.Contains(key) || !Map.InBounds(nx, ny) || Map.GetTerrain(nx, ny) == TerrainType.Water) continue;
                    if (DisStopsWater(nx, ny)) { d.saved++; continue; }
                    if (Random.value > 0.72f) continue;
                    d.tiles.Add(new Vector2Int(nx, ny));
                    d.times.Add(d.ring + 1);
                    set.Add(key);
                }
            }
            d.ring++;
        }
        // What the lava touches burns; ash falls on what isn't sheltered.
        d.pulse2 += dt;
        if (d.pulse2 >= 2f)
        {
            d.pulse2 = 0f;
            d.hit.Clear();
            var set = DisTileSet(d);
            float ash2 = d.radius * d.radius;
            foreach (var b in State.buildings)
            {
                if (!b.IsBuilt || b.health <= 0) continue;
                var def = Database.GetBuilding(b.definitionId);
                if (def == null) continue;
                var c = DisCentreOf(b, def);
                if ((c - d.centre).sqrMagnitude > ash2) continue;
                bool lava = false;
                for (int y = 0; y < def.size.y && !lava; y++)
                    for (int x = 0; x < def.size.x && !lava; x++)
                        lava = set.Contains(DisKey(b.cell.x + x, b.cell.y + y));
                if (lava)
                {
                    d.hit.Add(b.instanceId);
                    DisHurt(b, def, 0.15f, 0f, d);
                }
                else if (DisGuard(c, DisGuardKind.Ash, out _) <= 0f) DisHurt(b, def, 0.012f, 0.6f, d);
            }
        }
        // buildings under the lava can't work
        var outSet = DisOutSet(State.currentWorld);
        foreach (int id in d.hit) outSet.Add(id);
        if (d.left <= 0f)
        {
            DisMark(d, new Vector2Int(Mathf.FloorToInt(d.centre.x), Mathf.FloorToInt(d.centre.y)), now + 140f);
            DisEnd(d);
        }
    }

    private void DisPlagueStep(DisasterState d, float dt)
    {
        d.left -= dt;
        bool healers = DisGuard(d.centre, DisGuardKind.Heal, out _) > 0f;
        bool water = DisGuard(d.centre, DisGuardKind.Water, out _) > 0f;
        if (healers) d.left -= dt * 0.8f;   // healers end it sooner
        d.pulse += dt;
        if (d.pulse >= 12f)
        {
            d.pulse = 0f;
            d.radius = Mathf.Min(10f, d.radius + (water ? 0.5f : 1f));
        }
        // Which houses are sick now, and how badly (clean water and healers help).
        d.hit.Clear();
        d.hitTimes.Clear();
        float housing = 0f, sick = 0f, r2 = d.radius * d.radius;
        foreach (var b in State.buildings)
        {
            var def = Database.GetBuilding(b.definitionId);
            if (def == null || def.housing <= 0 || !IsWorking(b, def)) continue;
            housing += def.housing;
            var c = DisCentreOf(b, def);
            if ((c - d.centre).sqrMagnitude > r2) continue;
            float m = 1f;
            if (DisGuard(c, DisGuardKind.Water, out _) > 0f) m *= 0.6f;
            if (DisGuard(c, DisGuardKind.Heal, out _) > 0f) m *= 0.35f;
            sick += def.housing * m;
            d.hit.Add(b.instanceId);
            d.hitTimes.Add(m);
        }
        if (housing > 0f)
        {
            float mult = Researched("Herbalism") ? 0.8f : 1f;
            d.carry += State.population * 0.05f / 150f * (sick / housing) * mult * dt;
            int n = Mathf.FloorToInt(d.carry);
            if (n > 0 && State.population > 2f)
            {
                d.carry -= n;
                State.population = Mathf.Max(1f, State.population - n);
                d.lost += n;
            }
        }
        if (d.left <= 0f) DisEnd(d);
    }

    private void DisMeteorStep(DisasterState d, float dt)
    {
        d.left -= dt;
        float now = State.playTime;
        d.pulse -= dt;
        if (d.left > 0f && d.pulse <= 0f)
        {
            d.pulse = Random.Range(0.8f, 1.5f);
            Vector2 target;
            var b = Random.value < 0.55f ? DisRandomBuilding() : null;
            var def = b != null ? Database.GetBuilding(b.definitionId) : null;
            if (def != null && (DisCentreOf(b, def) - d.centre).sqrMagnitude <= d.radius * d.radius) target = DisCentreOf(b, def);
            else target = d.centre + Random.insideUnitCircle * d.radius;
            d.tiles.Add(new Vector2Int(Mathf.FloorToInt(target.x), Mathf.FloorToInt(target.y)));
            d.times.Add(now + 1.1f);
        }
        for (int i = d.tiles.Count - 1; i >= 0; i--)
        {
            if (d.times[i] > now) continue;
            var t = d.tiles[i];
            var at = new Vector2(t.x + 0.5f, t.y + 0.5f);
            d.tiles.RemoveAt(i);
            d.times.RemoveAt(i);
            if (DisBlocked(at)) { d.saved++; continue; }
            foreach (var b in State.buildings)
            {
                if (!b.IsBuilt || b.health <= 0) continue;
                var def = Database.GetBuilding(b.definitionId);
                if (def == null) continue;
                // distance from the impact to the footprint
                float dx = Mathf.Max(b.cell.x - at.x, 0f, at.x - (b.cell.x + def.size.x));
                float dy = Mathf.Max(b.cell.y - at.y, 0f, at.y - (b.cell.y + def.size.y));
                if (dx * dx + dy * dy > 2.9f) continue;
                DisHurt(b, def, Random.Range(0.12f, 0.22f), 0.05f, d);
            }
            DisMark(d, t, now + 60f);
        }
        if (d.left <= 0f && d.tiles.Count == 0) DisEnd(d);
    }
}
