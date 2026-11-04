using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// How utilities reach buildings (Cities: Skylines style, but simpler):
/// - Districts. Generators and substations power every building within their district radius
///   (10-18 tiles by age and tier, +1 per upgrade, capped); wells, cisterns, fountains, towers,
///   pumps and purifiers water a district the same way; data centres and relays connect one.
/// - Conduction. A covered building passes the supply on to buildings touching it or standing
///   within a short gap, up to ConductHops hops: it fills in a district's edges without lighting
///   a whole town.
/// - Lines. Power poles, pylons and wireless masts only carry power between districts and towns:
///   a line inside or beside a district joins its grid, and lines in link range join each other.
///   Districts that touch (their buildings conducting into each other) share one grid too.
///   Aqueducts bring water to districts with no source; the water mains join pumps and towers.
/// - From the Stellar Age a beam relay joins every grid on the world into one, and the noosphere
///   joins every data network; coverage of buildings still comes from the districts.
/// - Towns (gap-linked groups of buildings, the same rule as SettlementZones) give the grids
///   their names ("Ravenford grid") and the per-town lines in the Utilities panel.
/// Worked out on the utilities' 2-4 s timer: a bucket grid of footprints, no per-frame work.
/// </summary>
public partial class Settlement
{
    /// <summary>One town or outpost as the utilities see it, with what its buildings get.</summary>
    public struct UtlTown
    {
        public string name;
        public bool outpost;
        public int buildings, homes;
        public Vector2 centre;
        /// <summary>Its main grids (-1 = none): the power grid feeding most of it, the water network serving it.</summary>
        public int powerGrid, waterGrid;
        /// <summary>Power its works need; how many of them are off every grid.</summary>
        public float powerNeed;
        public int powerOff;
        /// <summary>People in its homes; how many of them have water.</summary>
        public float waterNeed, waterServed;
        // (working out)
        internal float sx, sy, mainPower, mainWater;
        internal int rank, nameId;
    }

    /// <summary>One connected network (power, water or data).</summary>
    public struct UtlGrid
    {
        /// <summary>Power made / water capacity / compute.</summary>
        public float supply;
        /// <summary>Power needed / people wanting water.</summary>
        public float demand;
        /// <summary>Water handed out.</summary>
        public float used;
        public int generators, nodes;
        public bool clean;
        /// <summary>The town it's named after (that of its biggest source), -1 if none.</summary>
        public int main;
        internal float mainAmount;
        internal int townsSeen, lastTown;
    }

    /// <summary>A building's footprint in an overlay, coloured by how its network is doing (kind 1 water, 2 power, 3 data).</summary>
    public struct UtlCovRect { public RectInt r; public float level; public byte kind; }

    // ---------- The buildings, bucketed ----------

    private struct UtlGB { public Rect r; public Vector2 c; public int id, town; public bool carrier, working; public BuildingState b; public BuildingDefinition def; }

    private const float UtlBucket = 8f;
    private readonly List<UtlGB> utlGB = new List<UtlGB>();
    private readonly Dictionary<long, int> utlBucketHead = new Dictionary<long, int>();
    private readonly Dictionary<int, int> utlIdx = new Dictionary<int, int>();
    private int[] utlBNext = new int[256], utlGParent = new int[256], utlReach = new int[256], utlNodeOf = new int[256];
    private byte[] utlDepth = new byte[256];
    private float utlBMaxHalf = 0.5f;
    private readonly List<int> utlHits = new List<int>();
    private readonly Queue<int> utlBfs = new Queue<int>();
    private readonly Dictionary<int, int> utlGridRoots = new Dictionary<int, int>();
    private readonly Dictionary<int, string> utlNames = new Dictionary<int, string>();
    private readonly List<UtlNode> utlWAll = new List<UtlNode>();
    private readonly List<int> utlFirst = new List<int>();
    private int[] utlTParent = new int[64];

    /// <summary>Lines and relays carry a network between districts; they're not part of a town.</summary>
    private static bool UtlIsCarrier(UtlSpec spec) =>
        spec != null && (spec.role == UtlRole.PowerNode && !spec.district || spec.role == UtlRole.Data && spec.amount <= 0f);

    private int UtlIndexOf(BuildingState b) => b != null && utlIdx.TryGetValue(b.instanceId, out int i) ? i : -1;

    /// <summary>Every footprint within 'range' (gap) of r, into utlHits.</summary>
    private void UtlQuery(Rect r, float range)
    {
        utlHits.Clear();
        if (utlGB.Count == 0) return;
        float pad = range + utlBMaxHalf;
        int bx0 = Mathf.FloorToInt((r.xMin - pad) / UtlBucket), bx1 = Mathf.FloorToInt((r.xMax + pad) / UtlBucket);
        int by0 = Mathf.FloorToInt((r.yMin - pad) / UtlBucket), by1 = Mathf.FloorToInt((r.yMax + pad) / UtlBucket);
        for (int by = by0; by <= by1; by++)
            for (int bx = bx0; bx <= bx1; bx++)
            {
                if (!utlBucketHead.TryGetValue(UtlKey(bx, by), out int i)) continue;
                while (i >= 0)
                {
                    if (Gap(utlGB[i].r, r) <= range) utlHits.Add(i);
                    i = utlBNext[i];
                }
            }
    }

    private int UtlGFind(int i)
    {
        while (utlGParent[i] != i) { utlGParent[i] = utlGParent[utlGParent[i]]; i = utlGParent[i]; }
        return i;
    }

    private void UtlTInit(int count)
    {
        if (utlTParent.Length < count) utlTParent = new int[Mathf.NextPowerOfTwo(count)];
        for (int i = 0; i < count; i++) utlTParent[i] = i;
    }

    private int UtlTFind(int i)
    {
        while (utlTParent[i] != i) { utlTParent[i] = utlTParent[utlTParent[i]]; i = utlTParent[i]; }
        return i;
    }

    private void UtlTUnion(int a, int b)
    {
        a = UtlTFind(a); b = UtlTFind(b);
        if (a != b) utlTParent[a] = b;
    }

    /// <summary>The buildings bucketed, and grouped into towns (gap-linked, like SettlementZones; lines left out).</summary>
    private void UtlGroupTowns(UtlNet n)
    {
        utlGB.Clear(); utlBucketHead.Clear(); utlIdx.Clear();
        n.townOf.Clear(); n.towns.Clear(); n.cover.Clear();
        utlBMaxHalf = 0.5f;
        foreach (var b in State.buildings)
        {
            var def = Database.GetBuilding(b.definitionId);
            if (def == null || def.IsWall) continue;
            UtlCatalog.TryGet(def.id, out var spec);
            var r = new Rect(b.cell.x, b.cell.y, def.size.x, def.size.y);
            utlIdx[b.instanceId] = utlGB.Count;
            utlGB.Add(new UtlGB { r = r, c = r.center, id = b.instanceId, town = -1, carrier = UtlIsCarrier(spec), working = IsWorking(b, def), b = b, def = def });
            utlBMaxHalf = Mathf.Max(utlBMaxHalf, Mathf.Max(def.size.x, def.size.y) * 0.5f);
        }
        int count = utlGB.Count;
        if (utlBNext.Length < count)
        {
            int size = Mathf.NextPowerOfTwo(count);
            utlBNext = new int[size]; utlGParent = new int[size]; utlReach = new int[size]; utlNodeOf = new int[size]; utlDepth = new byte[size];
        }
        for (int i = 0; i < count; i++)
        {
            var c = utlGB[i].c;
            long key = UtlKey(Mathf.FloorToInt(c.x / UtlBucket), Mathf.FloorToInt(c.y / UtlBucket));
            utlBNext[i] = utlBucketHead.TryGetValue(key, out int head) ? head : -1;
            utlBucketHead[key] = i;
            utlGParent[i] = i;
        }
        float merge = Database.settlementMergeDistance;
        for (int i = 0; i < count; i++)
        {
            if (utlGB[i].carrier) continue;
            UtlQuery(utlGB[i].r, merge);
            foreach (int j in utlHits)
            {
                if (j == i || utlGB[j].carrier) continue;
                int a = UtlGFind(i), b = UtlGFind(j);
                if (a != b) utlGParent[a] = b;
            }
        }
        utlGridRoots.Clear();
        for (int i = 0; i < count; i++)
        {
            var gb = utlGB[i];
            if (gb.carrier) continue;
            var b = gb.b;
            int root = UtlGFind(i);
            if (!utlGridRoots.TryGetValue(root, out int t))
            {
                t = n.towns.Count;
                utlGridRoots[root] = t;
                n.towns.Add(new UtlTown { outpost = true, powerGrid = -1, waterGrid = -1, rank = -1, nameId = int.MaxValue });
            }
            var def = gb.def;
            var town = n.towns[t];
            town.buildings++;
            town.sx += gb.c.x; town.sy += gb.c.y;
            bool home = StatsOf(def, b.upgradeLevel).housing > 0;
            if (home) town.homes++;
            if (home || def.isTownCenter) town.outpost = false;
            // Named after its trading post (as the trade routes call it), else its town centre, a home, any building.
            int rank = TradeCatalog.PostTier(def) >= 0 ? 3 : def.isTownCenter ? 2 : home ? 1 : 0;
            if (rank > town.rank || rank == town.rank && b.instanceId < town.nameId) { town.rank = rank; town.nameId = b.instanceId; }
            n.towns[t] = town;
            gb.town = t;
            utlGB[i] = gb;
            n.townOf[b.instanceId] = t;
        }
        for (int t = 0; t < n.towns.Count; t++)
        {
            var town = n.towns[t];
            town.centre = new Vector2(town.sx, town.sy) / Mathf.Max(1, town.buildings);
            if (!utlNames.TryGetValue(town.nameId, out town.name)) utlNames[town.nameId] = town.name = TradeTownName(town.nameId);
            n.towns[t] = town;
        }
    }

    // ---------- Districts and conduction ----------

    private void UtlCoverBegin()
    {
        int count = utlGB.Count;
        for (int i = 0; i < count; i++) { utlReach[i] = -1; utlDepth[i] = 255; utlNodeOf[i] = -1; }
        utlBfs.Clear();
    }

    /// <summary>Building j gets the supply of network node 'dsu' at this depth (two networks meeting there join).</summary>
    private void UtlReachSet(int j, int dsu, int depth)
    {
        if (!utlGB[j].working) return;
        if (utlReach[j] < 0) { utlReach[j] = dsu; utlDepth[j] = (byte)depth; utlBfs.Enqueue(j); }
        else
        {
            UtlTUnion(utlReach[j], dsu);
            if (depth < utlDepth[j]) { utlDepth[j] = (byte)depth; utlBfs.Enqueue(j); }
        }
        if (utlNodeOf[j] >= 0) UtlTUnion(utlNodeOf[j], dsu);   // a district reaching a line, a substation or another plant: one grid
    }

    /// <summary>A district: every working building within r of 'at'. Lines (no district) only take in what they stand on.</summary>
    private void UtlSeed(Vector2 at, float r, int dsu, bool conducts)
    {
        if (r <= 0f) return;
        UtlQuery(new Rect(at.x, at.y, 0f, 0f), r);
        int depth = conducts ? 0 : UtlCatalog.ConductHops;
        foreach (int j in utlHits) UtlReachSet(j, dsu, depth);
    }

    /// <summary>Covered buildings pass it on to those touching them or a short gap away, a few hops deep.</summary>
    private void UtlConduct()
    {
        while (utlBfs.Count > 0)
        {
            int i = utlBfs.Dequeue();
            int depth = utlDepth[i];
            if (depth >= UtlCatalog.ConductHops || utlGB[i].carrier) continue;
            UtlQuery(utlGB[i].r, UtlCatalog.ConductGap);
            int from = utlReach[i];
            for (int h = 0; h < utlHits.Count; h++)
            {
                int j = utlHits[h];
                if (j != i) UtlReachSet(j, from, depth + 1);
            }
        }
    }

    /// <summary>Number the networks: every node's root becomes a grid.</summary>
    private void UtlMakeGrids(List<UtlGrid> grids, int nodes)
    {
        grids.Clear();
        utlGridRoots.Clear();
        for (int i = 0; i < nodes; i++)
        {
            int root = UtlTFind(i);
            if (utlGridRoots.ContainsKey(root)) continue;
            utlGridRoots[root] = grids.Count;
            grids.Add(new UtlGrid { main = -1, lastTown = -1 });
        }
    }

    private int UtlGridOf(int dsu) => dsu >= 0 && utlGridRoots.TryGetValue(UtlTFind(dsu), out int g) ? g : -1;

    /// <summary>The grid of the building at spatial index i (-1 if nothing reaches it).</summary>
    private int UtlGridAt(int i) => i >= 0 && utlReach[i] >= 0 ? UtlGridOf(utlReach[i]) : -1;

    private void UtlGridSource(List<UtlGrid> grids, int g, UtlNode node, float amount)
    {
        if (g < 0) return;
        var gr = grids[g];
        gr.nodes++;
        if (node.town >= 0 && (gr.main < 0 || amount > gr.mainAmount)) { gr.main = node.town; gr.mainAmount = amount; }
        grids[g] = gr;
    }

    /// <summary>A node's district radius: its own, +1 per upgrade (up to 2), capped.</summary>
    private static float UtlDistrict(UtlNode node, float bonus) =>
        node.cover <= 0f ? 0f : Mathf.Min(UtlCatalog.MaxCover, node.cover + Mathf.Min(2, node.b.upgradeLevel) + bonus);

    private void UtlAddCover(UtlNet n, int i, byte kind, float level)
    {
        if (i < 0) return;
        var r = utlGB[i].r;
        n.cover.Add(new UtlCovRect { r = new RectInt((int)r.x, (int)r.y, (int)r.width, (int)r.height), level = level, kind = kind });
    }

    private void UtlAddRing(UtlNet n, UtlNode node, float r, byte kind, float level) =>
        n.discs.Add(new UtlDisc { at = node.at, r = Mathf.Max(1.2f, r), kind = kind, level = level });

    // ---------- Water ----------

    private void UtlTownWater(UtlNet n, int hydraulics)
    {
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
            utlCompA.Add(UtlCatalog.AqueductPeople);
            utlCompFlag.Add(fed);
        }
        // Every water point: wells and cisterns, the mains, fountains.
        utlWAll.Clear();
        utlWAll.AddRange(utlWaterNodes);
        utlWAll.AddRange(utlMains);
        utlWAll.AddRange(utlFountains);
        int W = utlWAll.Count, A = utlCompA.Count;
        UtlTInit(W + A);
        // Mills and pumps feed the lines they touch.
        for (int i = 0; i < W; i++)
        {
            var f = utlWAll[i];
            if (!f.spec.feeds) continue;
            int last = -1;
            foreach (int comp in UtlTouching(f))
            {
                if (comp == last) continue;
                last = comp;
                utlCompFlag[comp] = true;
                utlCompA[comp] += UtlCatalog.MillFeedPeople;
                UtlTUnion(i, W + comp);
            }
        }
        // The mains: pumps, towers, purifiers, extractors... joined underground.
        if (utlMains.Count > 0)
        {
            UtlJoin(utlMains, n, 1, true);
            utlFirst.Clear();
            int baseIndex = utlWaterNodes.Count;
            for (int i = 0; i < utlMains.Count; i++)
            {
                int comp = utlMains[i].comp;
                while (utlFirst.Count <= comp) utlFirst.Add(-1);
                if (utlFirst[comp] < 0) utlFirst[comp] = baseIndex + i;
                else UtlTUnion(baseIndex + i, utlFirst[comp]);
            }
        }
        // Fountains: on a fed line they hand out its water (and add their own); alone, a small basin.
        for (int i = W - utlFountains.Count; i < W; i++)
        {
            var f = utlWAll[i];
            int comp = -1;
            foreach (int c in UtlTouching(f)) if (utlCompFlag[c]) { comp = c; break; }
            if (comp >= 0) { UtlTUnion(i, W + comp); f.info = 1; }
            else { f.amount = f.amount > 0f ? UtlCatalog.FountainBasinPeople : 0f; f.cover = UtlCatalog.FountainBasinCover + hydraulics; f.info = 2; }
        }
        // Districts round every water point; fed lines water the buildings along them.
        UtlCoverBegin();
        for (int i = 0; i < W; i++)
        {
            int at = UtlIndexOf(utlWAll[i].b);
            if (at >= 0) utlNodeOf[at] = i;
        }
        for (int i = 0; i < W; i++) UtlSeed(utlWAll[i].at, UtlDistrict(utlWAll[i], 0f), i, true);
        foreach (var pair in utlAqComp)
        {
            if (!utlCompFlag[pair.Value]) continue;
            UtlQuery(new Rect(UtlKeyX(pair.Key), UtlKeyY(pair.Key), 1f, 1f), 0.01f);
            foreach (int j in utlHits) UtlReachSet(j, W + pair.Value, 0);
        }
        UtlConduct();

        // The networks and what they can give.
        var grids = n.waterGrids;
        UtlMakeGrids(grids, W + A);
        for (int i = 0; i < W; i++)
        {
            var w = utlWAll[i];
            int g = UtlGridOf(i);
            var gr = grids[g];
            gr.supply += w.amount;
            gr.clean |= w.spec.clean && w.amount > 0f;
            grids[g] = gr;
            UtlGridSource(grids, g, w, w.amount);
        }
        for (int a = 0; a < A; a++)
        {
            if (!utlCompFlag[a]) continue;
            int g = UtlGridOf(W + a);
            var gr = grids[g];
            gr.supply += utlCompA[a];
            grids[g] = gr;
        }
        // Homes take what their network has to spare (first come, first served).
        float need = 0f, served = 0f, clean = 0f, capacity = 0f;
        n.water.Clear(); n.clean.Clear();
        foreach (var h in utlHouses)
        {
            need += h.amount;
            int i = UtlIndexOf(h.b), g = UtlGridAt(i);
            float got = 0f;
            bool isClean = false;
            if (g >= 0)
            {
                var gr = grids[g];
                gr.demand += h.amount;
                got = Mathf.Clamp(gr.supply - gr.used, 0f, h.amount);
                gr.used += got;
                isClean = gr.clean;
                grids[g] = gr;
            }
            served += got;
            bool has = got >= h.amount * 0.5f;
            if (has)
            {
                n.water.Add(h.b.instanceId);
                if (isClean) { n.clean.Add(h.b.instanceId); clean += got; }
            }
            if (h.town >= 0)
            {
                var town = n.towns[h.town];
                town.waterNeed += h.amount;
                town.waterServed += got;
                if (g >= 0 && h.amount > town.mainWater) { town.mainWater = h.amount; town.waterGrid = g; }
                n.towns[h.town] = town;
            }
            UtlAddCover(n, i, 1, g < 0 || grids[g].supply <= 0f ? 0f : !has ? 0.5f : isClean ? 1f : 0.75f);
        }
        foreach (var gr in grids) capacity += gr.supply;
        n.waterNeed = need; n.waterServed = served; n.waterCapacity = capacity; n.cleanServed = clean;
        n.waterNets = 0;
        foreach (var gr in grids) if (gr.supply > 0f) n.waterNets++;

        // Rings, guards against the plague, and what each water point is doing.
        for (int i = 0; i < W; i++)
        {
            var w = utlWAll[i];
            int g = UtlGridOf(i);
            var gr = grids[g];
            float r = UtlDistrict(w, 0f);
            UtlAddRing(n, w, r, 1, gr.supply <= 0f ? 0f : gr.used < gr.demand * 0.99f ? 0.5f : gr.clean ? 1f : 0.75f);
            if (gr.supply > 0f) { n.guardAt.Add(w.at); n.guardR.Add(r); }
            byte code = w.info == 1 || w.info == 2 ? (byte)w.info : (byte)(gr.clean ? 4 : 3);
            n.info[w.b.instanceId] = new UtlInfo { a = gr.used, b = gr.supply, code = code, grid = g };
        }
        // Irrigated farms; steam plants with cooling water.
        foreach (var f in utlFarms)
        {
            int g = UtlGridAt(UtlIndexOf(f.b));
            if (g >= 0 && grids[g].supply > 0f) n.water.Add(f.b.instanceId);
        }
        foreach (var g in utlCooled)
        {
            int wg = UtlGridAt(UtlIndexOf(g.b));
            if (!Map.AnyWater(new RectInt(g.b.cell, g.def.size), 4) && !(wg >= 0 && grids[wg].supply > 0f))
            {
                g.amount *= 0.6f;
                n.info[g.b.instanceId] = new UtlInfo { code = 9 };
            }
        }
    }

    // ---------- Power ----------

    private void UtlTownPower(UtlNet n)
    {
        n.power.Clear();
        n.gridOf.Clear();
        n.supply = 0f; n.demand = 0f; n.demandMet = 0f; n.grids = 0; n.shortGrids = 0; n.consumers = 0; n.unpowered = 0;
        var nodes = utlPowerNodes;
        int P = nodes.Count;
        UtlTInit(P);
        bool world = false;
        if (P > 0)
        {
            // Lines in link range of each other (and of plants and substations) join up.
            UtlJoin(nodes, n, 2, false);
            utlFirst.Clear();
            for (int i = 0; i < P; i++)
            {
                int comp = nodes[i].comp;
                while (utlFirst.Count <= comp) utlFirst.Add(-1);
                if (utlFirst[comp] < 0) utlFirst[comp] = i;
                else UtlTUnion(i, utlFirst[comp]);
                if (nodes[i].spec.world) world = true;
            }
            // From orbit, one grid for the whole world.
            if (world) for (int i = 1; i < P; i++) UtlTUnion(i, 0);
        }
        float smart = Researched(UtlCatalog.TechSmartGrid) ? 2f : 0f;
        UtlCoverBegin();
        for (int i = 0; i < P; i++)
        {
            int at = UtlIndexOf(nodes[i].b);
            if (at >= 0) utlNodeOf[at] = i;
        }
        for (int i = 0; i < P; i++)
        {
            var p = nodes[i];
            if (p.spec.district) UtlSeed(p.at, UtlDistrict(p, smart), i, true);
            else
            {
                int own = UtlIndexOf(p.b);
                if (own >= 0) UtlReachSet(own, i, UtlCatalog.ConductHops);   // a line is reached where it stands
            }
        }
        UtlConduct();
        // Lines join the districts they stand in or beside, but power nothing themselves.
        for (int i = 0; i < P; i++)
        {
            var p = nodes[i];
            if (p.spec.district || p.cover <= 0f) continue;
            UtlQuery(new Rect(p.b.cell.x, p.b.cell.y, p.def.size.x, p.def.size.y), p.cover);
            foreach (int j in utlHits) if (utlReach[j] >= 0 && !utlGB[j].carrier) UtlTUnion(i, utlReach[j]);
        }

        var grids = n.powerGrids;
        UtlMakeGrids(grids, P);
        for (int i = 0; i < P; i++)
        {
            var p = nodes[i];
            if (p.spec.role != UtlRole.Generator) { UtlGridSource(grids, UtlGridOf(i), p, 0f); continue; }
            int g = UtlGridOf(i);
            var gr = grids[g];
            gr.supply += p.amount;
            gr.generators++;
            grids[g] = gr;
            UtlGridSource(grids, g, p, p.amount + 1f);
            n.supply += p.amount;
        }
        // Each consumer is on the grid that reaches it (if that grid has a generator).
        foreach (var c in utlConsumers)
        {
            n.consumers++;
            n.demand += c.amount;
            int i = UtlIndexOf(c.b), g = UtlGridAt(i);
            if (g >= 0 && grids[g].generators == 0) g = -1;
            c.comp = g;
            if (g >= 0) { var gr = grids[g]; gr.demand += c.amount; grids[g] = gr; }
            if (c.town >= 0)
            {
                var town = n.towns[c.town];
                town.powerNeed += c.amount;
                if (g < 0) town.powerOff++;
                else if (c.amount > town.mainPower) { town.mainPower = c.amount; town.powerGrid = g; }
                n.towns[c.town] = town;
            }
        }
        foreach (var gr in grids) if (gr.generators > 0) n.grids++;
        foreach (var c in utlConsumers)
        {
            int i = UtlIndexOf(c.b);
            if (c.comp < 0) { n.unpowered++; UtlAddCover(n, i, 2, 0f); continue; }
            var gr = grids[c.comp];
            float share = gr.demand > 0f ? Mathf.Clamp01(gr.supply / gr.demand) : 1f;
            n.power[c.b.instanceId] = share;
            n.gridOf[c.b.instanceId] = c.comp;
            n.demandMet += c.amount * share;
            UtlAddCover(n, i, 2, share);
        }
        foreach (var gr in grids)
            if (gr.demand > gr.supply * 1.001f && gr.supply > 0f) n.shortGrids++;
        for (int i = 0; i < P; i++)
        {
            var p = nodes[i];
            int g = UtlGridOf(i);
            var gr = grids[g];
            float level = gr.supply <= 0f ? 0f : gr.demand <= 0f ? 1f : Mathf.Clamp01(gr.supply / gr.demand);
            UtlAddRing(n, p, p.spec.district ? UtlDistrict(p, smart) : 0f, 2, level);
            bool gen = p.spec.role == UtlRole.Generator;
            n.info[p.b.instanceId] = new UtlInfo { a = gen ? p.amount : gr.supply, b = gr.demand, grid = g,
                code = (byte)(gen ? (n.info.TryGetValue(p.b.instanceId, out var old) && old.code == 9 ? 9 : 5) : 6) };
        }
        // Brownouts: say so when they start (not more than every two minutes).
        bool brown = n.shortGrids > 0;
        if (brown && !n.brownout && State.playTime - n.brownoutNoticeAt > 120f)
        {
            n.brownoutNoticeAt = State.playTime;
            UtlSave.brownouts++;
            int worst = -1;
            for (int g = 0; g < grids.Count; g++)
                if (grids[g].supply > 0f && grids[g].demand > grids[g].supply * 1.001f && (worst < 0 || grids[g].demand - grids[g].supply > grids[worst].demand - grids[worst].supply)) worst = g;
            var w = grids[Mathf.Max(0, worst)];
            Notify($"Brownout on the {UtlGridName(n, grids, worst)}: it needs {w.demand:0} power but makes {w.supply:0}. Every building on it slows down; build more generators there, or a power line to another grid.");
            if (!offWorld) GameAudio.UI("utl_brownout");
        }
        n.brownout = brown;
    }

    // ---------- Data ----------

    private void UtlTownData(UtlNet n)
    {
        n.data.Clear();
        n.compute = 0f;
        n.dataNodes = utlDataNodes.Count;
        n.dataGrids.Clear();
        if (utlDataNodes.Count == 0) { n.research = 1f; return; }
        var nodes = utlDataNodes;
        int D = nodes.Count;
        UtlJoin(nodes, n, 3, false);
        UtlTInit(D);
        utlFirst.Clear();
        bool world = false;
        for (int i = 0; i < D; i++)
        {
            int comp = nodes[i].comp;
            while (utlFirst.Count <= comp) utlFirst.Add(-1);
            if (utlFirst[comp] < 0) utlFirst[comp] = i;
            else UtlTUnion(i, utlFirst[comp]);
            if (nodes[i].spec.world) world = true;
        }
        if (world) for (int i = 1; i < D; i++) UtlTUnion(i, 0);
        UtlCoverBegin();
        for (int i = 0; i < D; i++)
        {
            int at = UtlIndexOf(nodes[i].b);
            if (at >= 0) utlNodeOf[at] = i;
        }
        for (int i = 0; i < D; i++) UtlSeed(nodes[i].at, UtlDistrict(nodes[i], 0f), i, true);
        UtlConduct();
        var grids = n.dataGrids;
        UtlMakeGrids(grids, D);
        float quantum = Researched(UtlCatalog.TechQuantumNet) ? 1.25f : 1f;
        for (int i = 0; i < D; i++)
        {
            var d = nodes[i];
            // Computers need power (the old data banks run on their own).
            float powered = UtlCatalog.Demand(d.def) > 0 && !(d.spec != null && d.spec.existing) ? (n.power.TryGetValue(d.b.instanceId, out float r) ? r : n.grace ? 1f : 0.25f) : 1f;
            float c = d.amount * powered * quantum;
            int g = UtlGridOf(i);
            var gr = grids[g];
            gr.supply += c;
            grids[g] = gr;
            UtlGridSource(grids, g, d, c + 0.01f);
            n.compute += c;
        }
        n.research = 1f + Mathf.Min(0.4f, 0.025f * n.compute);
        for (int i = 0; i < D; i++)
        {
            var d = nodes[i];
            int g = UtlGridOf(i);
            UtlAddRing(n, d, UtlDistrict(d, 0f), 3, grids[g].supply > 0f ? 1f : 0.3f);
            n.info[d.b.instanceId] = new UtlInfo { a = grids[g].supply, b = n.compute, code = 7, grid = g };
        }
        // Who a network with some compute on it reaches.
        void Reach(UtlNode node)
        {
            int i = UtlIndexOf(node.b), g = UtlGridAt(i);
            if (g < 0 || grids[g].supply <= 0f) return;
            if (n.data.Add(node.b.instanceId)) UtlAddCover(n, i, 3, 1f);
        }
        foreach (var c in utlConsumers) Reach(c);
        foreach (var h in utlHouses) Reach(h);
    }

    // ---------- Names ----------

    /// <summary>"Ravenford grid", "the Millbury network"... from the town of its biggest source.</summary>
    public string UtlGridName(UtlNet n, List<UtlGrid> grids, int g, string word = "grid")
    {
        if (n == null || grids == null || g < 0 || g >= grids.Count) return word;
        int t = grids[g].main;
        return t >= 0 && t < n.towns.Count ? $"{n.towns[t].name} {word}" : word;
    }
}
