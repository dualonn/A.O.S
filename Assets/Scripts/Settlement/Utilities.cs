using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Utilities, per world, worked out every couple of seconds from the buildings (every few seconds
/// for worlds in the background):
/// Every network reaches buildings by districts plus a little conduction (see UtlDistricts.cs).
/// - Water. Wells, cisterns and the fire crews' pumps water a district round them; an aqueduct line
///   touching a river or lake waters the buildings along it and fills its fountains; water mills and
///   steam pumps feed the mains, water towers hand it out; purifiers, atmospheric extractors, arcane
///   fonts and matter synthesisers give clean water (anywhere, even on dry or airless worlds). Each
///   network serves so many people. Covered homes grow faster and are happier, sickness spreads less there (the
///   plague in DisSim.cs counts every water point as clean water), and irrigated farms grow more.
/// - Power, from the Industrial Age. Generators (steam, dams, coal, oil, wind, atomic, solar, mana,
///   aether, stellar taps, Dyson swarms, vacuum engines, and the reactors already in the game) and
///   substations power a district each; poles, pylons and wireless masts are lines joining districts
///   and towns into grids. Each grid shares its power out: short of it, every building on that grid
///   gets the same share (a brownout).
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
    public struct UtlInfo { public float a, b; public byte code; public int grid; }

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
        /// <summary>Towns and outposts (gap-linked groups of buildings) and which one each building is in.</summary>
        public readonly List<UtlTown> towns = new List<UtlTown>();
        public readonly Dictionary<int, int> townOf = new Dictionary<int, int>();
        /// <summary>Consumers on a grid -> which power grid (index into powerGrids).</summary>
        public readonly Dictionary<int, int> gridOf = new Dictionary<int, int>();
        /// <summary>The networks: each power grid, water network and data network.</summary>
        public readonly List<UtlGrid> powerGrids = new List<UtlGrid>(), waterGrids = new List<UtlGrid>(), dataGrids = new List<UtlGrid>();
        /// <summary>Overlay fills: the buildings each network reaches (and the ones it misses).</summary>
        public readonly List<UtlCovRect> cover = new List<UtlCovRect>();

        // Summary
        public float waterNeed, waterServed, waterCapacity, cleanServed;
        public float supply, demand, demandMet;
        public int grids, shortGrids, consumers, unpowered, waterNets;
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
                       "<color=#aaa>(A generator powers the district round it; power lines link districts and towns. See Utilities in the research panel.)</color>");
        }
        if (save.version == 1)
        {
            // Power and water now reach by districts (not pole by pole): worlds that already had a grid get half an hour to settle in.
            save.version = 2;
            if (UtlHadGrid(CurrentWorld))
            {
                save.graceUntil = Mathf.Max(save.graceUntil, State.playTime + UtlCatalog.DistrictGrace);
                if (!offWorld) Notify("Power now spreads by districts: each generator and substation powers everything round it, and buildings pass it on to their neighbours. " +
                       "Your poles and pylons now link districts and towns into grids. " +
                       $"<color=#aaa>(Your works carry on as they were for the next {UtlCatalog.DistrictGrace / 60f:0} minutes; see the power overlay.)</color>");
            }
        }
        n.graceLeft = Mathf.Max(0f, save.graceUntil - State.playTime);
        if (n.grace && n.graceLeft <= 0f) Notify("The grace for your old works is over: Electric Age works without power now run 15% slower.");
        n.grace = n.graceLeft > 0f;
    }

    private bool UtlHadGrid(WorldState w)
    {
        foreach (var b in w.buildings)
            if (UtlCatalog.TryGet(b.definitionId, out var spec) && (spec.role == UtlRole.PowerNode || spec.role == UtlRole.Generator && !spec.existing)) return true;
        return false;
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
        /// <summary>The town it stands in (-1 for lines); what a fountain is doing (1 fed, 2 a basin).</summary>
        public int town, info;
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

    private static long UtlKey(int x, int y) => ((long)x << 32) ^ (uint)y;
    private static int UtlKeyX(long k) => (int)(k >> 32);
    private static int UtlKeyY(long k) => (int)(uint)(k & 0xffffffffL);

    private Dictionary<int, int> utlCurTowns;

    private UtlNode UtlNew(BuildingState b, BuildingDefinition def, UtlSpec spec, Vector2 at, float half)
    {
        UtlNode node;
        if (utlPoolUsed < utlPool.Count) node = utlPool[utlPoolUsed];
        else { node = new UtlNode(); utlPool.Add(node); }
        utlPoolUsed++;
        node.b = b; node.def = def; node.spec = spec; node.at = at; node.half = half;
        node.amount = 0f; node.cover = spec != null ? spec.cover : 0f; node.link = spec != null ? spec.link : 0f;
        node.wireless = spec != null && spec.wireless; node.comp = -1; node.info = 0;
        node.town = b != null && utlCurTowns != null && utlCurTowns.TryGetValue(b.instanceId, out int town) ? town : -1;
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
        UtlGroupTowns(n);
        utlCurTowns = n.townOf;

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

        UtlTownWater(n, hydraulics);
        UtlTownPower(n);
        UtlTownData(n);
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
}
