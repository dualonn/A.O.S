using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Utilities, per world, worked out every couple of seconds from the buildings (every few seconds
/// for worlds in the background):
/// - Water. Wells, cisterns and the fire crews' pumps serve the houses round them; an aqueduct line
///   touching a river or lake fills the fountains along it; water mills and steam pumps feed the
///   mains, water towers hand it out; purifiers, atmospheric extractors, arcane fonts and matter
///   synthesisers give clean water (anywhere, even on dry or airless worlds). Each network serves
///   so many people. Covered homes grow faster and are happier, sickness spreads less there (the
///   plague in DisSim.cs counts every water point as clean water), and irrigated farms grow more.
/// - Power, from the Industrial Age. Generators (steam, dams, coal, oil, wind, atomic, solar, mana,
///   aether, stellar taps, Dyson swarms, vacuum engines, and the reactors already in the game) feed
///   a grid of poles, pylons, substations and wireless masts. Each connected grid shares its power
///   out: short of it, every building on that grid gets the same share (a brownout).
///   Industrial and Modern works only gain from power (+10%); Electric Age and later works need it
///   (-15% without it, +10% with it) - see the phase-in below.
/// - Data, from the Information Age: data centres (and later psionic, quantum and noospheric
///   networks) speed up research, and works in reach of the network run a little faster.
/// - Pollution, on a coarse grid (8x8-tile cells): mines, furnaces, refineries and power stations
///   give it off; forests, parks, scrubbers and terraformers take it out; recycling plants and
///   research cut it. Smog lowers mood and growth, spoils crops and timber nearby, and at its worst
///   (from the Industrial Age) acid rain eats at buildings.
/// Phase-in for old saves: the first time a world with industrial works is seen, it gets an hour of
/// grace before the lack of power, water or clean air bites; the bonuses work at once.
/// </summary>
public partial class Settlement
{
    // ---------- Hooks ----------

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void UtlRegister()
    {
        SettlementHooks.Add(SettlementHooks.Mood, UtlMoodHook);
        SettlementHooks.Add(SettlementHooks.Production, UtlProductionHook);
        SettlementHooks.Add(SettlementHooks.Research, UtlResearchHook);
        SettlementHooks.Add(SettlementHooks.Growth, UtlGrowthHook);
    }

    private static void UtlMoodHook(Settlement s, Action<float, string> add) => s.UtlMoodOf(add);
    private static float UtlProductionHook(Settlement s, BuildingState b, BuildingDefinition def, ResourceType r) => s.UtlFactor(b, r);
    private static float UtlResearchHook(Settlement s) => s.utlNets.TryGetValue(s.State.currentWorld, out var n) ? n.research : 1f;
    private static float UtlGrowthHook(Settlement s) => s.utlNets.TryGetValue(s.State.currentWorld, out var n) ? n.growth : 1f;

    // ---------- Per world, worked out (never saved) ----------

    /// <summary>Overlay rings: where a network reaches, and how well it's doing there (1 fine, below 1 short, 0 nothing).</summary>
    public struct UtlDisc { public Vector2 at; public float r, level; public byte kind; }

    /// <summary>A link between two nodes of a network (kind 1 water mains, 2 power, 3 data); wired ones are drawn as cables.</summary>
    public struct UtlLink { public Vector2 a, b; public float ha, hb; public byte kind; public bool wired; }

    /// <summary>What a utility building is doing now: its network's numbers (for the selection panel).</summary>
    public struct UtlInfo { public float a, b; public byte code; }

    /// <summary>One world's utilities as last worked out.</summary>
    public class UtlNet
    {
        public float nextAt = -999f, lastAt = -1f;
        /// <summary>Goes up every time it's worked out (the view redraws).</summary>
        public int version;
        public readonly Dictionary<int, float> prod = new Dictionary<int, float>(), crop = new Dictionary<int, float>();
        /// <summary>Consumers on a grid -> that grid's share of what it needs (missing = not on any grid).</summary>
        public readonly Dictionary<int, float> power = new Dictionary<int, float>();
        public readonly HashSet<int> water = new HashSet<int>(), clean = new HashSet<int>(), data = new HashSet<int>();
        public readonly Dictionary<int, float> fuel = new Dictionary<int, float>();
        public readonly Dictionary<int, UtlInfo> info = new Dictionary<int, UtlInfo>();
        public readonly Dictionary<long, float> smog = new Dictionary<long, float>();
        public readonly Dictionary<long, float> smogTarget = new Dictionary<long, float>();
        public readonly Dictionary<long, int> forest = new Dictionary<long, int>();
        public float forestAt = -999f;
        public readonly List<UtlLink> links = new List<UtlLink>();
        public readonly List<UtlDisc> discs = new List<UtlDisc>();
        public readonly List<Vector2> guardAt = new List<Vector2>();
        public readonly List<float> guardR = new List<float>();

        // Summary
        public float waterNeed, waterServed, waterCapacity, cleanServed;
        public float supply, demand, demandMet;
        public int grids, shortGrids, consumers, unpowered;
        public float compute;
        public int dataNodes;
        public float smogAvg, smogWorst;
        public Vector2 worstAt;
        public float moodWater, moodLight, moodSmog, moodBrown, moodData, growth = 1f, research = 1f;
        public int terraformers, waterPoints, generators, powerNodes, absorbers;
        public bool grace, brownout;
        public float graceLeft, brownoutNoticeAt = -999f, acidAt = -999f, acidNoticeAt = -999f, outNoticeAt = -999f;

        public float WaterCoverage => waterNeed > 0 ? Mathf.Clamp01(waterServed / waterNeed) : 1f;
        public float PowerShare => demand > 0 ? Mathf.Clamp01(demandMet / demand) : 1f;
        public bool HasGrid => generators > 0 || powerNodes > 0;
    }

    private readonly Dictionary<int, UtlNet> utlNets = new Dictionary<int, UtlNet>();

    private UtlNet UtlNetOf(int world)
    {
        if (!utlNets.TryGetValue(world, out var n)) utlNets[world] = n = new UtlNet();
        return n;
    }

    /// <summary>The utilities of the world you're looking at (or the one being ticked).</summary>
    public UtlNet UtilitiesHere => UtlNetOf(State.currentWorld);

    private UtlWorldSave UtlSave
    {
        get
        {
            if (CurrentWorld.utl == null) CurrentWorld.utl = new UtlWorldSave();
            return CurrentWorld.utl;
        }
    }

    private float UtlFactor(BuildingState b, ResourceType r)
    {
        if (!utlNets.TryGetValue(State.currentWorld, out var n)) return 1f;
        float f = 1f;
        if (n.prod.Count > 0 && n.prod.TryGetValue(b.instanceId, out float p)) f = p;
        if (r != null && n.crop.Count > 0 && (r.id == "Food" || r.id == "Wood") && n.crop.TryGetValue(b.instanceId, out float c)) f *= c;
        return f;
    }

    private void UtlMoodOf(Action<float, string> add)
    {
        if (!utlNets.TryGetValue(State.currentWorld, out var n) || n.version == 0) return;
        if (n.moodWater != 0f) add(n.moodWater, n.moodWater > 0 ? "clean water" : "no clean water");
        if (n.moodLight != 0f) add(n.moodLight, "electric light");
        if (n.moodBrown != 0f) add(n.moodBrown, "brownouts");
        if (n.moodData != 0f) add(n.moodData, "connected homes");
        if (n.moodSmog != 0f) add(n.moodSmog, $"smog ({UtlCatalog.SmogWord(n.smogAvg)} air)");
    }

    // ---------- The clock (called from Tick and TickBackground) ----------

    private void UtlTick(float dt)
    {
        if (Map == null) return;
        var n = UtlNetOf(State.currentWorld);
        if (State.playTime < n.nextAt && n.version > 0) return;
        n.nextAt = State.playTime + (offWorld ? 4f : 2f);
        float step = n.lastAt < 0f ? 0f : Mathf.Clamp(State.playTime - n.lastAt, 0f, 8f);
        n.lastAt = State.playTime;
        UtlPhaseIn(n);
        UtlCompute(n, step);
        UtlPollute(n);
        UtlFactors(n);
        UtlAcid(n);
        n.version++;
    }

    /// <summary>A world seen for the first time: old saves with industry get their hour of grace.</summary>
    private void UtlPhaseIn(UtlNet n)
    {
        var save = UtlSave;
        if (save.version == 0)
        {
            // The very first time (an old save): every world with industry gets the grace at once.
            // After that, a world seen for the first time is a new one: no grace needed.
            bool firstEver = true;
            foreach (var w in State.worlds) if (w != CurrentWorld && w.utl != null && w.utl.version > 0) { firstEver = false; break; }
            bool legacy = false;
            if (firstEver)
                foreach (var w in State.worlds)
                {
                    if (w.utl == null) w.utl = new UtlWorldSave();
                    w.utl.version = 1;
                    if (UtlHasIndustry(w)) { w.utl.graceUntil = State.playTime + UtlCatalog.LegacyGrace; legacy = true; }
                }
            save.version = 1;
            if (legacy)
                Notify("Utilities have come to your works: water, power grids and the smog of industry. " +
                       "For the next hour your old works carry on as they were; after that, Electric Age works without power run 15% slower. " +
                       "<color=#aaa>(Build generators and power lines: see Utilities in the research panel.)</color>");
        }
        n.graceLeft = Mathf.Max(0f, save.graceUntil - State.playTime);
        if (n.grace && n.graceLeft <= 0f) Notify("The grace for your old works is over: Electric Age works without power now run 15% slower.");
        n.grace = n.graceLeft > 0f;
    }

    private bool UtlHasIndustry(WorldState w)
    {
        foreach (var b in w.buildings)
        {
            var def = Database.GetBuilding(b.definitionId);
            if (def != null && !UtlCatalog.IsUtility(def) && def.age != null && def.age.order >= UtlCatalog.IndustrialAge) return true;
        }
        return false;
    }

    // ---------- Scratch lists (one world is worked out at a time) ----------

    private class UtlNode
    {
        public BuildingState b;
        public BuildingDefinition def;
        public UtlSpec spec;
        public Vector2 at;
        public float half, amount, cover, link;
        public bool wireless;
        public int comp;
    }

    private readonly List<UtlNode> utlPool = new List<UtlNode>();
    private int utlPoolUsed;
    private readonly List<UtlNode> utlPowerNodes = new List<UtlNode>(), utlWaterNodes = new List<UtlNode>(), utlDataNodes = new List<UtlNode>(),
        utlConsumers = new List<UtlNode>(), utlHouses = new List<UtlNode>(), utlFarms = new List<UtlNode>(), utlSources = new List<UtlNode>(),
        utlAbsorbers = new List<UtlNode>(), utlRecyclers = new List<UtlNode>(), utlFountains = new List<UtlNode>(), utlFeeders = new List<UtlNode>(),
        utlCooled = new List<UtlNode>(), utlMains = new List<UtlNode>();
    private readonly HashSet<long> utlAqTiles = new HashSet<long>();
    private readonly Dictionary<long, int> utlAqComp = new Dictionary<long, int>();
    private readonly Queue<long> utlQueue = new Queue<long>();
    private readonly Dictionary<int, int> utlRoots = new Dictionary<int, int>();
    private readonly List<float> utlCompA = new List<float>(), utlCompB = new List<float>();
    private readonly List<bool> utlCompFlag = new List<bool>();
    private readonly List<(float d, int i, int j)> utlEdges = new List<(float, int, int)>();
    private int[] utlParent = new int[64];

    private struct UtlWComp { public float cap, used; public bool clean; }
    private readonly List<UtlWComp> utlWComps = new List<UtlWComp>();
    private struct UtlWDisc { public Vector2 at; public float r; public int comp; }
    private readonly List<UtlWDisc> utlWDiscs = new List<UtlWDisc>();

    private static long UtlKey(int x, int y) => ((long)x << 32) ^ (uint)y;
    private static int UtlKeyX(long k) => (int)(k >> 32);
    private static int UtlKeyY(long k) => (int)(uint)(k & 0xffffffffL);

    private UtlNode UtlNew(BuildingState b, BuildingDefinition def, UtlSpec spec, Vector2 at, float half)
    {
        UtlNode node;
        if (utlPoolUsed < utlPool.Count) node = utlPool[utlPoolUsed];
        else { node = new UtlNode(); utlPool.Add(node); }
        utlPoolUsed++;
        node.b = b; node.def = def; node.spec = spec; node.at = at; node.half = half;
        node.amount = 0f; node.cover = spec != null ? spec.cover : 0f; node.link = spec != null ? spec.link : 0f;
        node.wireless = spec != null && spec.wireless; node.comp = -1;
        return node;
    }

    private int UtlFind(int i)
    {
        while (utlParent[i] != i) { utlParent[i] = utlParent[utlParent[i]]; i = utlParent[i]; }
        return i;
    }

    /// <summary>Join nodes in reach of each other (shortest links first, so the links drawn make a tidy tree).</summary>
    private void UtlJoin(List<UtlNode> nodes, UtlNet n, byte kind, bool mains)
    {
        int count = nodes.Count;
        if (utlParent.Length < count) utlParent = new int[Mathf.NextPowerOfTwo(count)];
        for (int i = 0; i < count; i++) utlParent[i] = i;
        utlEdges.Clear();
        int limit = Mathf.Min(count, 900);   // (a sane cap: the pairs grow with the square)
        for (int i = 0; i < limit; i++)
        {
            var a = nodes[i];
            if (a.link <= 0f) continue;
            for (int j = i + 1; j < limit; j++)
            {
                var c = nodes[j];
                if (c.link <= 0f) continue;
                float reach = mains || a.wireless || c.wireless ? Mathf.Max(a.link, c.link) : Mathf.Min(a.link, c.link);
                float dx = a.at.x - c.at.x, dy = a.at.y - c.at.y;
                float d2 = dx * dx + dy * dy;
                if (d2 <= reach * reach) utlEdges.Add((d2, i, j));
            }
        }
        utlEdges.Sort((x, y) => x.d.CompareTo(y.d));
        foreach (var e in utlEdges)
        {
            int ra = UtlFind(e.i), rb = UtlFind(e.j);
            if (ra == rb) continue;
            utlParent[ra] = rb;
            var a = nodes[e.i]; var c = nodes[e.j];
            n.links.Add(new UtlLink { a = a.at, b = c.at, ha = UtlTopOf(a), hb = UtlTopOf(c), kind = kind, wired = kind == 2 && !a.wireless && !c.wireless });
        }
        // Number the groups 0..k-1.
        utlRoots.Clear();
        int groups = 0;
        for (int i = 0; i < count; i++)
        {
            int root = UtlFind(i);
            if (!utlRoots.TryGetValue(root, out int g)) { g = groups++; utlRoots[root] = g; }
            nodes[i].comp = g;
        }
    }

    /// <summary>How high above its middle a node's wires hang (the top of its art).</summary>
    private static float UtlTopOf(UtlNode node) => Mathf.Max(0.3f, node.def.ArtSize.y - node.def.size.y * 0.5f - 0.45f);

    private float UtlWork(BuildingState b, BuildingDefinition def)
    {
        var stats = StatsOf(def, b.upgradeLevel);
        return StaffingOf(b) * DisWorkFactor(b) * Mathf.Clamp01(b.health / Mathf.Max(1f, stats.maxHealth)) * Mathf.Max(0.1f, stats.output);
    }

    /// <summary>Last worked-out share of power this building gets (grace counts as powered).</summary>
    private float UtlPoweredLast(UtlNet n, BuildingState b)
    {
        if (n.power.TryGetValue(b.instanceId, out float r)) return r;
        return n.grace ? 1f : 0f;
    }

    // ---------- Working it out ----------

    private void UtlCompute(UtlNet n, float step)
    {
        utlPoolUsed = 0;
        utlPowerNodes.Clear(); utlWaterNodes.Clear(); utlDataNodes.Clear(); utlConsumers.Clear(); utlHouses.Clear(); utlFarms.Clear();
        utlSources.Clear(); utlAbsorbers.Clear(); utlRecyclers.Clear(); utlFountains.Clear(); utlFeeders.Clear(); utlCooled.Clear(); utlMains.Clear();
        utlAqTiles.Clear();
        n.links.Clear(); n.discs.Clear(); n.guardAt.Clear(); n.guardR.Clear(); n.info.Clear();
        n.terraformers = 0; n.waterPoints = 0; n.generators = 0; n.powerNodes = 0; n.absorbers = 0;

        float techSupply = (Researched(UtlCatalog.TechAlternating) ? 1.1f : 1f) * (Researched(UtlCatalog.TechSmartGrid) ? 1.1f : 1f);
        float techWater = Researched(UtlCatalog.TechSanitation) ? 1.25f : 1f;
        int hydraulics = Researched(UtlCatalog.TechHydraulics) ? 2 : 0;
        var food = Database.food;

        foreach (var b in State.buildings)
        {
            var def = Database.GetBuilding(b.definitionId);
            if (def == null || !IsWorking(b, def)) continue;
            if (def.IsWall)
            {
                if (def.id == UtlCatalog.Aqueduct) utlAqTiles.Add(UtlKey(b.cell.x, b.cell.y));
                continue;
            }
            var at = new Vector2(b.cell.x + def.size.x * 0.5f, b.cell.y + def.size.y * 0.5f);
            float half = Mathf.Max(def.size.x, def.size.y) * 0.5f;
            UtlCatalog.TryGet(def.id, out var spec);
            float work = -1f;
            if (spec != null)
            {
                switch (spec.role)
                {
                    case UtlRole.Generator:
                    {
                        work = UtlWork(b, def);
                        var g = UtlNew(b, def, spec, at, half);
                        g.amount = spec.amount * work * UtlFuel(n, b, def, spec, work, step) * UtlGeneratorFactor(b, def, spec) * techSupply;
                        utlPowerNodes.Add(g);
                        if (spec.cooling) utlCooled.Add(g);
                        n.generators++;
                        break;
                    }
                    case UtlRole.PowerNode:
                        utlPowerNodes.Add(UtlNew(b, def, spec, at, half));
                        n.powerNodes++;
                        break;
                    case UtlRole.Water:
                    case UtlRole.WaterNode:
                    case UtlRole.Fountain:
                    {
                        work = UtlWork(b, def);
                        var w = UtlNew(b, def, spec, at, half);
                        float amount = spec.amount * work * techWater;
                        if (spec.demand > 0) amount *= 0.5f + 0.5f * UtlPoweredLast(n, b);   // pumps and extractors want power
                        if (def.id == UtlCatalog.Extractor && HasTrait(ThinAir)) amount *= 0.6f;
                        if (def.inputs.Count > 0) amount *= UtlFuel(n, b, def, spec, work, step);
                        w.amount = amount;
                        if (def.id == UtlCatalog.Cistern || def.id == UtlCatalog.Fountain) w.cover += hydraulics;
                        if (spec.role == UtlRole.Fountain) utlFountains.Add(w);
                        else if (spec.mains) utlMains.Add(w);
                        else utlWaterNodes.Add(w);
                        if (spec.feeds) utlFeeders.Add(w);
                        n.waterPoints++;
                        break;
                    }
                    case UtlRole.Data:
                    {
                        work = UtlWork(b, def);
                        var d = UtlNew(b, def, spec, at, half);
                        d.amount = spec.amount * work;
                        utlDataNodes.Add(d);
                        break;
                    }
                    case UtlRole.Absorber:
                    {
                        work = UtlWork(b, def);
                        var a = UtlNew(b, def, spec, at, half);
                        a.amount = spec.amount * work * (spec.demand > 0 ? 0.4f + 0.6f * UtlPoweredLast(n, b) : 1f);
                        utlAbsorbers.Add(a);
                        if (def.id == UtlCatalog.Terraformer) n.terraformers++;
                        n.absorbers++;
                        break;
                    }
                    case UtlRole.Recycler:
                    {
                        var r = UtlNew(b, def, spec, at, half);
                        r.amount = spec.amount;
                        utlRecyclers.Add(r);
                        n.absorbers++;
                        break;
                    }
                }
            }
            int demand = UtlCatalog.Demand(def);
            if (demand > 0)
            {
                var c = UtlNew(b, def, spec, at, half);
                c.amount = demand;
                utlConsumers.Add(c);
            }
            float pollution = UtlCatalog.PollutionOf(def);
            if (pollution > 0f)
            {
                if (work < 0f) work = UtlWork(b, def);
                var s = UtlNew(b, def, spec, at, half);
                s.amount = pollution * work * (spec != null && !spec.existing ? 1f - 0.3f * Mathf.Min(2, b.upgradeLevel) : 1f);
                if (s.amount > 0.01f) utlSources.Add(s);
            }
            var stats = StatsOf(def, b.upgradeLevel);
            if (stats.housing > 0)
            {
                var h = UtlNew(b, def, spec, at, half);
                h.amount = stats.housing;
                utlHouses.Add(h);
            }
            if (food != null && def.production.Count > 0 && def.production.Exists(r => r.resource == food && r.perSecond > 0))
                utlFarms.Add(UtlNew(b, def, spec, at, half));
        }

        UtlWorkWater(n, hydraulics);
        UtlWorkPower(n);
        UtlWorkData(n);
    }

    /// <summary>A utility's own fuel: taken here (the usual production skips buildings that make no goods). 0..1 of full output.</summary>
    private float UtlFuel(UtlNet n, BuildingState b, BuildingDefinition def, UtlSpec spec, float work, float step)
    {
        if (def.inputs.Count == 0) return 1f;
        if (spec.existing)
        {
            // Reactors already in the game burn their fuel in the usual production; follow how well they're fed.
            return Mathf.Lerp(def.runsWithoutInputs, 1f, InputSupplyOf(b));
        }
        if (step <= 0f) return n.fuel.TryGetValue(b.instanceId, out float last) ? last : 1f;
        float supply = 1f;
        foreach (var input in def.inputs)
        {
            float need = input.perSecond * step * work;
            if (input.resource != null && need > 0f) supply = Mathf.Min(supply, GetAmount(input.resource) / need);
        }
        supply = Mathf.Clamp01(supply);
        foreach (var input in def.inputs)
            if (input.resource != null && input.perSecond > 0f) AddResource(input.resource, -input.perSecond * step * work * supply);
        inputSupply[b.instanceId] = supply;
        float run = Mathf.Lerp(def.runsWithoutInputs, 1f, supply);
        n.fuel[b.instanceId] = run;
        return run;
    }

    /// <summary>Where and on what world a generator stands: wind on hills and in storms, sun under long nights, dams by deep water...</summary>
    private float UtlGeneratorFactor(BuildingState b, BuildingDefinition def, UtlSpec spec)
    {
        float f = 1f;
        var area = new RectInt(b.cell, def.size);
        switch (def.id)
        {
            case UtlCatalog.WindTurbine:
                if (Map.GetTerrain(b.cell.x, b.cell.y) == TerrainType.Hill) f *= 1.5f;
                if (HasTrait(Storms)) f *= 1.3f;
                if (HasTrait(ThinAir)) f *= 0.6f;
                break;
            case UtlCatalog.SolarFarm:
            case UtlCatalog.StellarTap:
            case "SolarCollector":
                if (HasTrait(LongNights)) f *= def.id == UtlCatalog.SolarFarm ? 0.6f : 0.8f;
                if (HasTrait(Scorching)) f *= 1.3f;
                break;
            case UtlCatalog.HydroDam:
            {
                int water = 0;
                for (int y = area.yMin - 2; y < area.yMax + 2; y++)
                    for (int x = area.xMin - 2; x < area.xMax + 2; x++)
                        if (Map.InBounds(x, y) && Map.GetTerrain(x, y) == TerrainType.Water) water++;
                f *= Mathf.Clamp(water / 8f, 0.4f, 1.3f);
                break;
            }
            case UtlCatalog.ManaReactor:
            case UtlCatalog.AetherReactor:
                if (HasTrait(ManaRich)) f *= 1.2f;
                if (Researched(UtlCatalog.TechLeyAttune)) f *= 1.15f;
                break;
        }
        return f;
    }

    private void UtlWorkWater(UtlNet n, int hydraulics)
    {
        utlWComps.Clear();
        utlWDiscs.Clear();
        // Aqueducts: each joined-up line is fed if it touches water, or a mill or pump.
        utlAqComp.Clear();
        utlCompA.Clear(); utlCompFlag.Clear();
        foreach (var start in utlAqTiles)
        {
            if (utlAqComp.ContainsKey(start)) continue;
            int comp = utlCompA.Count;
            bool fed = false;
            utlQueue.Clear();
            utlQueue.Enqueue(start);
            utlAqComp[start] = comp;
            while (utlQueue.Count > 0)
            {
                long k = utlQueue.Dequeue();
                int x = UtlKeyX(k), y = UtlKeyY(k);
                if (!fed)
                    for (int dy = -1; dy <= 1 && !fed; dy++)
                        for (int dx = -1; dx <= 1 && !fed; dx++)
                            if (Map.InBounds(x + dx, y + dy) && Map.GetTerrain(x + dx, y + dy) == TerrainType.Water) fed = true;
                for (int d = 0; d < 4; d++)
                {
                    long nk = UtlKey(x + (d == 0 ? 1 : d == 1 ? -1 : 0), y + (d == 2 ? 1 : d == 3 ? -1 : 0));
                    if (utlAqTiles.Contains(nk) && !utlAqComp.ContainsKey(nk)) { utlAqComp[nk] = comp; utlQueue.Enqueue(nk); }
                }
            }
            utlCompA.Add(fed ? UtlCatalog.AqueductPeople : 0f);
            utlCompFlag.Add(fed);
        }
        // Mills and pumps feed the lines they touch.
        foreach (var f in utlFeeders)
        {
            int last = -1;
            foreach (int comp in UtlTouching(f))
            {
                if (comp == last) continue;
                last = comp;
                if (!utlCompFlag[comp]) { utlCompFlag[comp] = true; }
                utlCompA[comp] += UtlCatalog.MillFeedPeople;
            }
        }
        int aqBase = utlWComps.Count;
        for (int i = 0; i < utlCompA.Count; i++)
            utlWComps.Add(new UtlWComp { cap = utlCompFlag[i] ? utlCompA[i] : 0f });
        // Fountains: on a fed line they hand out its water (and add their own); alone, a small basin.
        foreach (var f in utlFountains)
        {
            int comp = -1;
            foreach (int c in UtlTouching(f)) if (utlCompFlag[c]) { comp = c; break; }
            if (comp >= 0)
            {
                var wc = utlWComps[aqBase + comp];
                wc.cap += f.amount;
                utlWComps[aqBase + comp] = wc;
                UtlWaterDisc(n, f, f.cover, aqBase + comp);
                n.info[f.b.instanceId] = new UtlInfo { code = 1 };
            }
            else
            {
                utlWComps.Add(new UtlWComp { cap = UtlCatalog.FountainBasinPeople * (f.amount > 0f ? 1f : 0f) });
                UtlWaterDisc(n, f, UtlCatalog.FountainBasinCover + hydraulics, utlWComps.Count - 1);
                n.info[f.b.instanceId] = new UtlInfo { code = 2 };
            }
        }
        // Wells, cisterns and mills: each serves its own round.
        foreach (var w in utlWaterNodes)
        {
            utlWComps.Add(new UtlWComp { cap = w.amount, clean = w.spec.clean });
            UtlWaterDisc(n, w, w.cover, utlWComps.Count - 1);
        }
        // The mains: pumps, towers, purifiers, extractors... joined underground.
        if (utlMains.Count > 0)
        {
            UtlJoin(utlMains, n, 1, true);
            int mainsBase = utlWComps.Count;
            int groups = 0;
            foreach (var m in utlMains) groups = Mathf.Max(groups, m.comp + 1);
            for (int g = 0; g < groups; g++) utlWComps.Add(new UtlWComp());
            foreach (var m in utlMains)
            {
                var wc = utlWComps[mainsBase + m.comp];
                wc.cap += m.amount;
                wc.clean |= m.spec.clean;
                utlWComps[mainsBase + m.comp] = wc;
            }
            foreach (var m in utlMains) UtlWaterDisc(n, m, m.cover, mainsBase + m.comp);
        }
        // Homes take their water from the first point in reach with some to spare.
        float need = 0f, served = 0f, clean = 0f, capacity = 0f;
        n.water.Clear(); n.clean.Clear();
        foreach (var h in utlHouses)
        {
            need += h.amount;
            float got = 0f;
            bool isClean = false;
            for (int i = 0; i < utlWDiscs.Count && got < h.amount; i++)
            {
                var d = utlWDiscs[i];
                float r = d.r + h.half;
                if ((d.at - h.at).sqrMagnitude > r * r) continue;
                var wc = utlWComps[d.comp];
                float spare = wc.cap - wc.used;
                if (spare <= 0f) continue;
                float take = Mathf.Min(spare, h.amount - got);
                wc.used += take;
                utlWComps[d.comp] = wc;
                got += take;
                isClean |= wc.clean;
            }
            served += got;
            if (got >= h.amount * 0.5f)
            {
                n.water.Add(h.b.instanceId);
                if (isClean) { n.clean.Add(h.b.instanceId); clean += got; }
            }
        }
        foreach (var wc in utlWComps) capacity += wc.cap;
        n.waterNeed = need; n.waterServed = served; n.waterCapacity = capacity; n.cleanServed = clean;
        // Overlay rings, and what each water point is doing.
        foreach (var d in utlWDiscs)
        {
            var wc = utlWComps[d.comp];
            n.discs.Add(new UtlDisc { at = d.at, r = d.r, kind = 1, level = wc.cap <= 0f ? 0f : wc.clean ? 1f : 0.75f });
            if (wc.cap > 0f) { n.guardAt.Add(d.at); n.guardR.Add(d.r); }
        }
        foreach (var w in utlWaterNodes) UtlWaterInfo(n, w);
        foreach (var w in utlMains) UtlWaterInfo(n, w);
        foreach (var f in utlFountains)
            if (n.info.TryGetValue(f.b.instanceId, out var inf)) { UtlWaterInfo(n, f); var i2 = n.info[f.b.instanceId]; i2.code = inf.code; n.info[f.b.instanceId] = i2; }
        // Irrigated farms; steam plants with cooling water.
        foreach (var f in utlFarms) if (UtlInWater(f)) n.water.Add(f.b.instanceId);
        foreach (var g in utlCooled)
            if (!Map.AnyWater(new RectInt(g.b.cell, g.def.size), 4) && !UtlInWater(g)) { g.amount *= 0.6f; n.info[g.b.instanceId] = new UtlInfo { code = 9 }; }
    }

    private readonly List<int> utlTouch = new List<int>();

    /// <summary>The aqueduct lines touching a building's footprint (beside it or at its corners).</summary>
    private List<int> UtlTouching(UtlNode node)
    {
        utlTouch.Clear();
        if (utlAqComp.Count == 0) return utlTouch;
        var c = node.b.cell; var s = node.def.size;
        for (int y = c.y - 1; y <= c.y + s.y; y++)
            for (int x = c.x - 1; x <= c.x + s.x; x++)
            {
                if (x >= c.x && x < c.x + s.x && y >= c.y && y < c.y + s.y) continue;
                if (utlAqComp.TryGetValue(UtlKey(x, y), out int comp) && !utlTouch.Contains(comp)) utlTouch.Add(comp);
            }
        return utlTouch;
    }

    private void UtlWaterDisc(UtlNet n, UtlNode node, float r, int comp)
    {
        utlWDiscs.Add(new UtlWDisc { at = node.at, r = r, comp = comp });
        node.comp = comp;
    }

    private void UtlWaterInfo(UtlNet n, UtlNode node)
    {
        if (node.comp < 0 || node.comp >= utlWComps.Count) return;
        var wc = utlWComps[node.comp];
        n.info[node.b.instanceId] = new UtlInfo { a = wc.used, b = wc.cap, code = (byte)(wc.clean ? 4 : 3) };
    }

    private bool UtlInWater(UtlNode node)
    {
        foreach (var d in utlWDiscs)
        {
            if (utlWComps[d.comp].cap <= 0f) continue;
            float r = d.r + node.half;
            if ((d.at - node.at).sqrMagnitude <= r * r) return true;
        }
        return false;
    }

    private void UtlWorkPower(UtlNet n)
    {
        n.power.Clear();
        n.supply = 0f; n.demand = 0f; n.demandMet = 0f; n.grids = 0; n.shortGrids = 0; n.consumers = 0; n.unpowered = 0;
        var nodes = utlPowerNodes;
        int groups = 0;
        if (nodes.Count > 0)
        {
            UtlJoin(nodes, n, 2, false);
            foreach (var p in nodes) groups = Mathf.Max(groups, p.comp + 1);
        }
        utlCompA.Clear(); utlCompB.Clear();
        for (int g = 0; g < groups; g++) { utlCompA.Add(0f); utlCompB.Add(0f); }
        foreach (var p in nodes) { utlCompA[p.comp] += p.amount; n.supply += p.amount; }
        // Each consumer joins the grid of the first node whose reach covers it.
        foreach (var c in utlConsumers)
        {
            n.consumers++;
            n.demand += c.amount;
            c.comp = -1;
            for (int i = 0; i < nodes.Count; i++)
            {
                var p = nodes[i];
                float r = p.cover + c.half;
                if ((p.at - c.at).sqrMagnitude > r * r) continue;
                if (c.comp < 0 || utlCompA[p.comp] > utlCompA[c.comp]) c.comp = p.comp;
                if (utlCompA[c.comp] > 0f) break;
            }
            if (c.comp >= 0) utlCompB[c.comp] += c.amount;
        }
        n.grids = groups;
        foreach (var c in utlConsumers)
        {
            if (c.comp < 0) { n.unpowered++; continue; }
            float supply = utlCompA[c.comp], demand = utlCompB[c.comp];
            float share = demand > 0f ? Mathf.Clamp01(supply / demand) : 1f;
            n.power[c.b.instanceId] = share;
            n.demandMet += c.amount * share;
        }
        for (int g = 0; g < groups; g++)
            if (utlCompB[g] > utlCompA[g] * 1.001f && utlCompA[g] > 0f) n.shortGrids++;
        foreach (var p in nodes)
        {
            float supply = utlCompA[p.comp], demand = utlCompB[p.comp];
            float level = supply <= 0f ? 0f : demand <= 0f ? 1f : Mathf.Clamp01(supply / demand);
            n.discs.Add(new UtlDisc { at = p.at, r = p.cover, kind = 2, level = level });
            n.info[p.b.instanceId] = new UtlInfo { a = p.spec.role == UtlRole.Generator ? p.amount : supply, b = demand,
                code = (byte)(p.spec.role == UtlRole.Generator ? (n.info.TryGetValue(p.b.instanceId, out var old) && old.code == 9 ? 9 : 5) : 6) };
        }
        // Brownouts: say so when they start (not more than every two minutes).
        bool brown = n.shortGrids > 0;
        if (brown && !n.brownout && State.playTime - n.brownoutNoticeAt > 120f)
        {
            n.brownoutNoticeAt = State.playTime;
            UtlSave.brownouts++;
            Notify($"Brownout: your grid needs {n.demand:0} power but makes {n.supply:0}. Every building on it slows down; build more generators.");
            if (!offWorld) GameAudio.UI("utl_brownout");
        }
        n.brownout = brown;
    }

    private void UtlWorkData(UtlNet n)
    {
        n.data.Clear();
        n.compute = 0f;
        n.dataNodes = utlDataNodes.Count;
        if (utlDataNodes.Count == 0) { n.research = 1f; return; }
        UtlJoin(utlDataNodes, n, 3, false);
        int groups = 0;
        foreach (var d in utlDataNodes) groups = Mathf.Max(groups, d.comp + 1);
        utlCompA.Clear();
        for (int g = 0; g < groups; g++) utlCompA.Add(0f);
        float quantum = Researched(UtlCatalog.TechQuantumNet) ? 1.25f : 1f;
        foreach (var d in utlDataNodes)
        {
            // Computers need power (the old data banks run on their own).
            float powered = UtlCatalog.Demand(d.def) > 0 && !(d.spec != null && d.spec.existing) ? (n.power.TryGetValue(d.b.instanceId, out float r) ? r : n.grace ? 1f : 0.25f) : 1f;
            float c = d.amount * powered * quantum;
            utlCompA[d.comp] += c;
            n.compute += c;
        }
        n.research = 1f + Mathf.Min(0.4f, 0.025f * n.compute);
        foreach (var d in utlDataNodes)
        {
            n.discs.Add(new UtlDisc { at = d.at, r = d.cover, kind = 3, level = utlCompA[d.comp] > 0f ? 1f : 0.3f });
            n.info[d.b.instanceId] = new UtlInfo { a = utlCompA[d.comp], b = n.compute, code = 7 };
        }
        // Who's in reach of a network that has some compute on it.
        void Reach(UtlNode node)
        {
            foreach (var d in utlDataNodes)
            {
                if (utlCompA[d.comp] <= 0f) continue;
                float r = d.cover + node.half;
                if ((d.at - node.at).sqrMagnitude <= r * r) { n.data.Add(node.b.instanceId); return; }
            }
        }
        foreach (var c in utlConsumers) Reach(c);
        foreach (var h in utlHouses) Reach(h);
    }

    // ---------- Effects ----------

    /// <summary>Per-building output factors, and the mood and growth they add up to.</summary>
    private void UtlFactors(UtlNet n)
    {
        n.prod.Clear();
        n.crop.Clear();
        bool grace = n.grace;
        foreach (var b in State.buildings)
        {
            var def = Database.GetBuilding(b.definitionId);
            if (def == null || def.production.Count == 0 || !b.IsBuilt) continue;
            float f = 1f;
            if (UtlCatalog.Demand(def) > 0)
            {
                bool needs = UtlCatalog.NeedsPower(def);
                if (n.power.TryGetValue(b.instanceId, out float share))
                    f *= needs ? Mathf.Lerp(grace ? 1f : UtlCatalog.UnpoweredFactor, 1f + UtlCatalog.PoweredBonus, share) : 1f + UtlCatalog.PoweredBonus * share;
                else if (needs && !grace) f *= UtlCatalog.UnpoweredFactor;
                if (def.age != null && def.age.order >= UtlCatalog.InformationAge && n.data.Contains(b.instanceId)) f *= 1.06f;
            }
            if (n.water.Contains(b.instanceId) && def.housing <= 0) f *= 1.08f;   // irrigation
            if (f != 1f) n.prod[b.instanceId] = f;
            if (!grace && n.smog.Count > 0)
            {
                float p = UtlSmogAt(n, b.cell.x + def.size.x * 0.5f, b.cell.y + def.size.y * 0.5f);
                if (p > 2f) n.crop[b.instanceId] = 1f - Mathf.Min(0.3f, p / 50f);
            }
        }

        // Mood and growth.
        float pop = State.population;
        float cover = n.WaterCoverage;
        n.moodWater = 0f;
        if (pop >= 20f && n.waterNeed > 0f && cover >= 0.6f) n.moodWater = n.cleanServed >= n.waterServed * 0.5f && n.cleanServed > 0f ? 3f : 2f;
        else if (!grace && pop >= 120f && State.ageIndex >= 4 && cover < 0.25f) n.moodWater = -2f;
        // Electric light: most homes on a powered grid.
        float homes = 0f, lit = 0f, connected = 0f;
        foreach (var h in utlHouses)
        {
            homes += h.amount;
            if (n.power.TryGetValue(h.b.instanceId, out float share)) lit += h.amount * share;
            if (n.data.Contains(h.b.instanceId)) connected += h.amount;
        }
        n.moodLight = State.ageIndex >= UtlCatalog.ModernAge && homes > 0f && lit >= homes * 0.5f ? 2f : 0f;
        n.moodBrown = n.brownout ? -1f : 0f;
        n.moodData = State.ageIndex >= UtlCatalog.InformationAge && homes > 0f && connected >= homes * 0.5f ? 1f : 0f;
        n.moodSmog = 0f;
        if (!grace && n.smogAvg >= 4f)
            n.moodSmog = -Mathf.Min(6f, Mathf.Round(n.smogAvg / 4f)) * (HasTrait(ToxicAir) ? 0.5f : 1f);   // (under domes already, on toxic worlds)
        float growth = 1f + 0.12f * cover + (n.cleanServed > 0f && n.waterNeed > 0f ? 0.04f * Mathf.Clamp01(n.cleanServed / n.waterNeed) : 0f);
        if (Researched(UtlCatalog.TechSanitation)) growth += 0.03f * cover;
        if (!grace) growth *= 1f - Mathf.Min(0.2f, n.smogAvg / 80f);
        if (HasTrait(ToxicAir) && n.terraformers > 0) growth *= 1f + 0.12f * Mathf.Min(2, n.terraformers);   // terraformers thin the poison out
        n.growth = growth;
    }

    /// <summary>Acid rain under the worst smog (Industrial Age on): buildings there wear down, never below 40%.</summary>
    private void UtlAcid(UtlNet n)
    {
        if (n.grace || State.ageIndex < UtlCatalog.IndustrialAge || n.smogWorst < 28f) return;
        if (State.playTime - n.acidAt < 30f) return;
        n.acidAt = State.playTime;
        int hit = 0;
        foreach (var b in State.buildings)
        {
            var def = Database.GetBuilding(b.definitionId);
            if (def == null || !b.IsBuilt || b.health <= 0f) continue;
            if (UtlSmogAt(n, b.cell.x + def.size.x * 0.5f, b.cell.y + def.size.y * 0.5f) < 28f) continue;
            float max = StatsOf(def, b.upgradeLevel).maxHealth;
            if (b.health <= max * 0.4f) continue;
            b.health = Mathf.Max(max * 0.4f, b.health - max * 0.015f);
            hit++;
        }
        if (hit > 0 && State.playTime - n.acidNoticeAt > 300f)
        {
            n.acidNoticeAt = State.playTime;
            Notify($"Acid rain is eating at {hit} building{(hit == 1 ? "" : "s")} under the worst of the smog. " +
                   "<color=#aaa>(Parks, scrubbers, recycling and clean power clear the air.)</color>");
            if (!offWorld) GameAudio.UI("utl_acid_rain", 0.7f);
        }
    }

    // ---------- Disasters: every water point counts as clean water against the plague ----------

    /// <summary>Called by DisSim when it gathers its guards (the wells' own are already there).</summary>
    private void UtlAddWaterGuards()
    {
        if (!utlNets.TryGetValue(State.currentWorld, out var n)) return;
        for (int i = 0; i < n.guardAt.Count; i++)
            disGuards.Add(new DisGuardAt { at = n.guardAt[i], r2 = n.guardR[i] * n.guardR[i], power = 1f, kind = DisGuardKind.Water });
    }

    // ---------- Building sites: dams and mills by the water ----------

    private bool utlChecking;

    /// <summary>CheckSite for utilities that must stand by water (null for everything else).</summary>
    private bool? UtlCheckSite(BuildingDefinition def, Vector2Int cell, out float quality, out string reason)
    {
        quality = 1f;
        reason = null;
        if (utlChecking || Map == null || !UtlCatalog.TryGet(def.id, out var spec) || spec.waterReach <= 0) return null;
        utlChecking = true;
        try
        {
            if (!CheckSite(def, cell, out quality, out reason)) return false;
            if (!Map.AnyWater(new RectInt(cell, def.size), spec.waterReach))
            {
                reason = $"{def.displayName}s stand on the bank of a river, lake or sea";
                return false;
            }
            return true;
        }
        finally { utlChecking = false; }
    }

    // ---------- Words ----------

    /// <summary>Build-menu tooltip lines: what the building gives, needs and gives off.</summary>
    public string UtlBuildingText(BuildingDefinition def)
    {
        if (def == null) return "";
        var sb = new StringBuilder();
        UtlCatalog.TryGet(def.id, out var spec);
        if (spec != null)
        {
            switch (spec.role)
            {
                case UtlRole.Water:
                    sb.Append($"<color=#8cf>Water for {spec.amount:0} people</color> within {spec.cover} tiles");
                    if (spec.mains) sb.Append($"; joins the mains (towers and pumps within {spec.link} tiles)");
                    if (spec.clean) sb.Append(", <color=#9ef>purified</color>");
                    if (spec.feeds) sb.Append(". Fills an aqueduct it touches");
                    sb.Append('\n');
                    break;
                case UtlRole.WaterNode:
                    sb.Append($"<color=#8cf>Hands out water from the mains</color> within {spec.cover} tiles; needs a pump or purifier within {spec.link} tiles\n");
                    break;
                case UtlRole.Aqueduct:
                    sb.Append($"<color=#8cf>Carries water</color> from a river or lake it touches to the fountains along it ({UtlCatalog.AqueductPeople:0} people, more with water mills feeding it). Drag out a line\n");
                    break;
                case UtlRole.Fountain:
                    sb.Append($"<color=#8cf>Water for {spec.amount:0} people</color> within {spec.cover} tiles when it touches a fed aqueduct; alone, a basin for {UtlCatalog.FountainBasinPeople:0} within {UtlCatalog.FountainBasinCover}\n");
                    break;
                case UtlRole.Generator:
                    sb.Append($"<color=#fe6>Makes {spec.amount:0} power</color> for buildings within {spec.cover} tiles, and for the poles and pylons linked to it");
                    if (spec.existing) sb.Append(" (as well as its power cells)");
                    if (spec.cooling) sb.Append(". Needs cooling water close by (a river, or a water supply), or runs at 60%");
                    sb.Append('\n');
                    break;
                case UtlRole.PowerNode:
                    sb.Append($"<color=#fe6>Carries power</color>: powers buildings within {spec.cover} tiles and links to {(spec.wireless ? "any pole, mast or generator" : "poles and generators")} within {spec.link} tiles{(spec.wireless ? " (wireless)" : "")}\n");
                    break;
                case UtlRole.Data:
                    sb.Append(spec.amount > 0
                        ? $"<color=#7ef>Data: {spec.amount:0.#} compute</color> (research +{spec.amount * 2.5f:0.#}%); works within {spec.cover} tiles of the network run 6% faster; links {spec.link} tiles\n"
                        : $"<color=#7ef>Carries data</color>: works within {spec.cover} tiles join the network; links {spec.link} tiles\n");
                    break;
                case UtlRole.Absorber:
                    sb.Append($"<color=#9e9>Cleans the air</color>: takes {spec.amount:0} pollution out of {(spec.cover == 0 ? "the air round it" : $"the air for {spec.cover * UtlCatalog.Cell} tiles round")}\n");
                    break;
                case UtlRole.Recycler:
                    sb.Append($"<color=#9e9>Recycling</color>: works within {spec.cover} tiles give off {1f - spec.amount:P0} less pollution\n");
                    break;
            }
            if (spec.waterReach > 0) sb.Append("<color=#8cf>Build on the bank of a river, lake or sea</color>\n");
        }
        int demand = UtlCatalog.Demand(def);
        if (demand > 0)
            sb.Append(UtlCatalog.NeedsPower(def)
                ? $"<color=#fe6>Uses {demand} power</color>: +{UtlCatalog.PoweredBonus:P0} output on a grid, -{1f - UtlCatalog.UnpoweredFactor:P0} without\n"
                : $"<color=#fe6>Uses {demand} power</color> if on a grid: +{UtlCatalog.PoweredBonus:P0} output\n");
        float pollution = UtlCatalog.PollutionOf(def);
        if (pollution > 0.05f) sb.Append($"<color=#c9a>Pollution {pollution:0.#}</color> (smog lowers mood and growth nearby)\n");
        return sb.ToString();
    }

    /// <summary>Selection panel lines: is it powered, watered, in smog; what its network is doing.</summary>
    public void UtlSelectionInfo(StringBuilder sb, BuildingState state, BuildingDefinition def)
    {
        if (state == null || def == null || !state.IsBuilt) return;
        var n = UtilitiesHere;
        if (n.version == 0) return;
        if (n.info.TryGetValue(state.instanceId, out var inf))
        {
            switch (inf.code)
            {
                case 1: case 3: case 4:
                    sb.Append(inf.b > 0f
                        ? $"<color=#8cf>Water: serving {inf.a:0}/{inf.b:0} people</color>{(inf.code == 4 ? " <color=#9ef>(purified)</color>" : "")}{(inf.code == 1 ? " from its aqueduct" : "")}\n"
                        : "<color=#f99>No water here</color>: a tower needs a pump or purifier on its mains\n");
                    break;
                case 2: sb.Append("<color=#fc6>A basin only</color>: set it beside an aqueduct that reaches a river to serve the whole quarter\n"); break;
                case 5: sb.Append($"<color=#fe6>Making {inf.a:0} power</color> for a grid needing {inf.b:0}\n"); break;
                case 9: sb.Append($"<color=#fc6>Short of cooling water: making {inf.a:0} power</color> (60%). A river within 4 tiles or a water supply fixes it\n"); break;
                case 6: sb.Append(inf.a <= 0f ? "<color=#f99>No generator on this grid</color>\n" : $"<color=#fe6>Grid: {inf.a:0} power for {inf.b:0} needed</color>\n"); break;
                case 7: sb.Append($"<color=#7ef>Network compute {inf.a:0.#}</color> (all networks {inf.b:0.#}: research +{(n.research - 1f):P0})\n"); break;
            }
        }
        int demand = UtlCatalog.Demand(def);
        if (demand > 0)
        {
            bool needs = UtlCatalog.NeedsPower(def);
            if (n.power.TryGetValue(state.instanceId, out float share))
                sb.Append(share >= 0.999f ? $"<color=#fe6>Powered</color> ({demand} power)\n" : $"<color=#fc6>Brownout: {share:P0} of its power</color>\n");
            else if (needs)
                sb.Append(n.grace ? $"<color=#fc6>No power</color> <color=#aaa>(old works' grace: {n.graceLeft / 60f:0} min left, then -15% output)</color>\n"
                                  : "<color=#f99>No power: -15% output</color>. Build a generator, or poles to one\n");
        }
        if (def.housing > 0 && n.waterNeed > 0f)
            sb.Append(n.water.Contains(state.instanceId) ? (n.clean.Contains(state.instanceId) ? "<color=#9ef>Clean running water</color>\n" : "<color=#8cf>Has water</color>\n")
                : "<color=#aaa>No water supply in reach</color>\n");
        float p = UtlSmogAt(n, state.cell.x + def.size.x * 0.5f, state.cell.y + def.size.y * 0.5f);
        if (p >= 3f) sb.Append($"<color=#c9a>Air: {UtlCatalog.SmogWord(p)} ({p:0})</color>{(p >= 28f && State.ageIndex >= UtlCatalog.IndustrialAge ? ": acid rain" : "")}\n");
    }

    /// <summary>The Utilities section's summary, one line per network.</summary>
    public string UtlSummaryText()
    {
        var n = UtilitiesHere;
        var sb = new StringBuilder();
        sb.Append(n.waterNeed > 0f
            ? $"<color=#8cf>Water</color> {n.WaterCoverage:P0} of homes · {n.waterServed:0}/{n.waterCapacity:0}\n"
            : "<color=#8cf>Water</color> <color=#aaa>no homes yet</color>\n");
        if (State.ageIndex >= UtlCatalog.IndustrialAge || n.HasGrid)
            sb.Append(n.HasGrid
                ? $"<color=#fe6>Power</color> {n.supply:0} made · {n.demand:0} needed{(n.brownout ? " <color=#f99>brownout</color>" : "")}{(n.unpowered > 0 ? $" · <color=#fc6>{n.unpowered} off-grid</color>" : "")}\n"
                : $"<color=#fe6>Power</color> <color=#fc6>no grid yet</color> · {n.demand:0} needed\n");
        if (State.ageIndex >= UtlCatalog.InformationAge || n.dataNodes > 0)
            sb.Append($"<color=#7ef>Data</color> {n.compute:0.#} compute · research +{n.research - 1f:P0}\n");
        sb.Append($"<color=#c9a>Air</color> {UtlCatalog.SmogWord(n.smogAvg)} in town{(n.smogWorst >= 8f ? $" · worst {UtlCatalog.SmogWord(n.smogWorst)}" : "")}");
        if (n.grace) sb.Append($"\n<color=#aaa>Old works' grace: {n.graceLeft / 60f:0} min left</color>");
        return sb.ToString();
    }

    /// <summary>The Utilities section's hover help.</summary>
    public string UtlHelpText()
    {
        var n = UtilitiesHere;
        var sb = new StringBuilder("<b>Utilities</b>\n");
        sb.Append($"<color=#8cf>Water</color>: wells, cisterns, aqueducts and fountains, water mills, pumps and towers, purifiers. Homes with water grow up to {0.12f + 0.04f:P0} faster and are happier, and sickness spreads less there. Farms in reach are irrigated (+8%).\n");
        sb.Append($"   {n.waterPoints} water points · {n.WaterCoverage:P0} of homes served{(n.cleanServed > 0 ? $" ({n.cleanServed:0} on clean water)" : "")}\n");
        sb.Append("<color=#fe6>Power</color> (Industrial Age on): generators feed buildings in reach and the poles, pylons and masts linked to them. " +
                  $"Works on a grid run {UtlCatalog.PoweredBonus:P0} faster; Electric Age works without power {1f - UtlCatalog.UnpoweredFactor:P0} slower. Short of power, every building on that grid slows (a brownout).\n");
        sb.Append($"   {n.generators} generators · {n.powerNodes} poles · {n.grids} grid{(n.grids == 1 ? "" : "s")} · {n.supply:0} made, {n.demand:0} needed\n");
        sb.Append("<color=#7ef>Data</color> (Information Age on): data centres speed research (2.5% per compute, up to 40%); works in reach of the network run 6% faster.\n");
        sb.Append("<color=#c9a>Pollution</color>: mines, furnaces, refineries and power stations. Smog lowers mood and growth, spoils crops and timber, and the worst brings acid rain. Forests, parks, scrubbers, recycling, clean power and research clear it.\n");
        sb.Append("<color=#aaa>Overlays show each network on the map (they also show by themselves while you place a utility).</color>");
        return sb.ToString();
    }
}
