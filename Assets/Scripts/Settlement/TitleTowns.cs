using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// The title screen's towns: a tidy town of any age, laid out in the throwaway world behind the
/// menu (MenuMode only: none of this is ever saved). TitleMontage calls it once per age.
/// </summary>
public partial class Settlement
{
    private readonly List<RectInt> titleTaken = new List<RectInt>();
    private static List<Vector2Int> titleBase;
    private List<Vector2Int> titleSpiral;

    private static int TitleOrder(BuildingDefinition b) => b.age ? b.age.order : 0;
    private static bool TitleShore(BuildingDefinition b) => b.id.StartsWith("Sea") || b.id.StartsWith("River");

    private static readonly string[] TitleTowerWords = { "Tower", "Bastion", "Battery", "Turret", "Coil", "Emplacement", "Pillbox", "Nest", "Obelisk", "Lance", "Array", "Cannon", "Mortar", "Lens", "Mirror", "Watchtower", "Lookout" };
    private static readonly string[] TitleNotTowers = { "Utl", "Cul", "Fth", "Trade", "Sea", "Dis", "Ppl", "Wild", "River" };

    private static bool TitleIsTower(BuildingDefinition b) =>
        !b.IsWall && b.housing == 0 && !b.trainsUnit && b.production.Count == 0 && !b.wonder && !b.requiredDeposit && !b.isTownCenter
        && !TitleNotTowers.Any(p => b.id.StartsWith(p)) && TitleTowerWords.Any(w => b.id.Contains(w))
        && b.id != "HelionTower" && b.id != "SmartTower" && b.id != "SkylineTower";

    private static readonly string[] TitleGenerators = { "UtlSteamDynamo", "UtlCoalPlant", "UtlNuclearPlant", "UtlSolarFarm", "UtlManaReactor", "UtlAetherReactor", "UtlStellarTap", "UtlDysonCollector", "UtlMatterSynth", "UtlVoidEngine" };
    private static readonly string[] TitleWaterWorks = { "UtlWaterTower", "UtlPurifier", "UtlAtmoExtractor", "UtlFontOfWaters" };
    private static readonly string[] TitleParks = { "UtlCityPark", "UtlSylvanGrove", "UtlGarden" };
    private static readonly string[] TitleOldWater = { "UtlCistern", "UtlFountain" };

    /// <summary>Open land near 'near', away from raider camps: where the title's towns stand.</summary>
    public Vector2Int TitleFindCentre(Vector2Int near)
    {
        if (!MenuMode) return near;
        float best = float.MinValue;
        Vector2Int bestC = near;
        for (int dy = -30; dy <= 30; dy += 3)
            for (int dx = -30; dx <= 30; dx += 3)
            {
                var c = near + new Vector2Int(dx, dy);
                int land = 0;
                for (int y = -13; y < 13; y += 2)
                    for (int x = -13; x < 13; x += 2)
                        if (Map.CanBuildOn(new RectInt(c.x + x, c.y + y, 1, 1), false, out _)) land++;
                float score = land - (dx * dx + dy * dy) * 0.01f;
                foreach (var camp in State.camps)
                    if ((camp.cell - c).sqrMagnitude < 24 * 24) score -= 60;
                if (score > best) { best = score; bestC = c; }
            }
        return bestC;
    }

    /// <summary>
    /// Replace everything with a town of this age round 'centre' (title screen only). Returns the
    /// town's bounds; 'clear' is the patch whose trees should be cleared by the view.
    /// </summary>
    public RectInt TitleBuildTown(int age, Vector2Int centre, out RectInt clear, int seed = 0)
    {
        clear = new RectInt(centre, Vector2Int.one);
        if (!MenuMode) return clear;
        var db = Database;
        var rng = new System.Random(seed != 0 ? seed : Environment.TickCount);
        bool Chance(double p) => rng.NextDouble() < p;
        int Range(int min, int maxInclusive) => rng.Next(min, maxInclusive + 1);
        age = Mathf.Clamp(age, 0, Mathf.Max(0, db.ages.Count - 1));

        // A clean slate in this age: the research of the ages before it, a healthy stock.
        State.buildings.Clear();
        State.ageIndex = age;
        State.researched.Clear();
        foreach (var tech in db.technologies)
        {
            if (tech == null || tech.IsLevelled) continue;
            int order = tech.age != null ? tech.age.order : 0;
            if (order < age) State.researched.Add(tech.id);
        }
        foreach (var r in db.resources)
        {
            if (r == null) continue;
            var stock = StockOf(r.id);
            if (stock == null) State.resources.Add(stock = new ResourceStock { id = r.id });
            stock.amount = 2000f + 1500f * age;
        }
        State.secondsUntilWave = 99999f;

        titleTaken.Clear();
        if (titleBase == null)
        {
            titleBase = new List<Vector2Int>();
            for (int y = -45; y <= 45; y++) for (int x = -45; x <= 45; x++) titleBase.Add(new Vector2Int(x, y));
        }
        // This town's shape: round, long or skewed, leaning any way, with ragged edges.
        float stretch = (float)(0.65 + rng.NextDouble() * 0.9), lean = (float)(rng.NextDouble() * Math.PI);
        float cos = Mathf.Cos(lean), sin = Mathf.Sin(lean), rag = (float)(rng.NextDouble() * 0.35);
        titleSpiral = titleBase.OrderBy(v =>
        {
            float u = v.x * cos + v.y * sin, w = -v.x * sin + v.y * cos;
            float d = u * u / stretch + w * w * stretch;
            return d * (1f + rag * (float)rng.NextDouble());
        }).ToList();

        var B = db.buildings.Where(b => b && TitleOrder(b) <= age).ToList();
        BuildingDefinition Newest(Func<BuildingDefinition, bool> p) => B.Where(p).OrderByDescending(TitleOrder).FirstOrDefault();
        // One of this age's or the last age's kind (a town keeps a few older buildings), else the newest there is.
        BuildingDefinition Pick(Func<BuildingDefinition, bool> p)
        {
            var recent = B.Where(p).Where(b => TitleOrder(b) >= age - 1).ToList();
            return recent.Count > 0 ? recent[rng.Next(recent.Count)] : Newest(p);
        }
        List<BuildingDefinition> Some(IEnumerable<BuildingDefinition> from, int count) =>
            from.OrderBy(_ => rng.Next()).Take(count).ToList();
        int gap = Chance(0.35) ? 0 : 1;   // a tight huddle, or room to walk between

        // How big: anything from a hamlet of a few huts to a sprawling city.
        double roll = rng.NextDouble();
        int tier = roll < 0.14 ? 0 : roll < 0.36 ? 1 : roll < 0.64 ? 2 : roll < 0.87 ? 3 : 4;   // hamlet, village, town, city, mega-city
        float reach = new[] { 0.55f, 0.8f, 1f, 1.35f, 1.75f }[tier];
        int R(int r) => Mathf.Min(44, Mathf.RoundToInt(r * reach));
        double civic = new[] { 0.2, 0.55, 0.8, 1, 1 }[tier];

        var tc = B.FirstOrDefault(b => b.isTownCenter && TitleOrder(b) == 0);
        if (tc == null) return clear;
        TitlePlace(tc, centre, 1, 10);
        int wonders = age < 1 ? 0 : tier switch { 0 => 0, 1 => Chance(0.3) ? 1 : 0, 2 => Chance(0.75) ? 1 : 0, 3 => 1, _ => Range(2, 3) };
        foreach (var w in Some(B.Where(b => b.wonder && TitleOrder(b) >= age - 3), wonders)) TitlePlace(w, centre, 1, R(18));
        int civics = tier >= 4 ? Range(2, 3) : tier == 3 ? Range(1, 2) : 1;
        for (int i = 0; i < civics; i++)
        {
            if (Chance(civic)) TitlePlace(Pick(b => b.id.StartsWith("Cul") && !b.wonder && b.housing == 0), centre, 1, R(18));
            if (Chance(civic)) TitlePlace(Pick(b => b.id.StartsWith("Fth") && !b.wonder && b.housing == 0), centre, 1, R(18));
        }
        if (age >= 2 && Chance(civic * 0.75)) TitlePlace(Pick(b => b.id.StartsWith("Ppl") && b.production.Count == 0), centre, 1, R(20));
        if (Chance(civic * 0.75)) TitlePlace(Pick(b => b.id.StartsWith("Trade") && !b.IsWall && b.size.x > 1), centre, 1, R(22));
        // Homes: mostly this age's, with a few older ones still standing.
        var homes = B.Where(b => b.housing > 0 && !TitleShore(b) && !b.isTownCenter && !b.foundsColonies).OrderByDescending(TitleOrder).Take(2).ToList();
        int houses = tier switch { 0 => Range(2, 4), 1 => Range(5, 8), 2 => Range(8, 13), 3 => Range(16, 26), _ => Range(32, 50) } + age / 4;
        for (int i = 0; i < houses && homes.Count > 0; i++)
            TitlePlace(homes.Count > 1 && Chance(0.25) ? homes[1] : homes[0], centre, gap, R(26));
        int utilitySets = tier switch { 0 => Chance(0.5) ? 1 : 0, 1 or 2 => 1, 3 => 2, _ => 3 };
        for (int i = 0; i < utilitySets; i++)
        {
            if (age >= 7)
            {
                TitlePlace(Pick(b => TitleGenerators.Contains(b.id)), centre, 1, R(28));
                if (Chance(0.8)) TitlePlace(Pick(b => TitleWaterWorks.Contains(b.id)), centre, 1, R(28));
                if (Chance(0.6)) TitlePlace(Pick(b => TitleParks.Contains(b.id)), centre, 1, R(28));
            }
            else if (age >= 1 && Chance(0.7)) TitlePlace(Pick(b => TitleOldWater.Contains(b.id)), centre, 1, R(26));
        }
        if (age >= 1)
        {
            int works = tier switch { 0 => Range(0, 1), 1 => Range(1, 3), 2 => Range(2, 5), 3 => Range(5, 8), _ => Range(8, 14) };
            var kinds = B.Where(b => b.production.Count > 0 && !b.requiredDeposit && !TitleShore(b) && TitleOrder(b) >= age - 2).ToList();
            for (int i = 0; i < works && kinds.Count > 0; i++) TitlePlace(kinds[rng.Next(kinds.Count)], centre, 1, R(30));
        }
        if (age >= 1 && age <= 6)
        {
            var farm = B.FirstOrDefault(b => b.id == "Farm");
            int farms = tier switch { 0 => Range(0, 1), 1 => Range(1, 3), 2 => Range(0, 4), 3 => Range(3, 6), _ => Range(5, 9) };
            for (int i = 0; i < farms && farm; i++) TitlePlace(farm, centre, 1, R(32));
        }
        int camps = age == 0 ? Range(0, 2) : tier switch { 0 => Range(0, 1), 1 => Range(1, 2), 2 => Range(1, 4), 3 => Range(3, 6), _ => Range(5, 9) };
        var trainers = B.Where(b => b.trainsUnit && !TitleShore(b) && TitleOrder(b) >= age - 1).ToList();
        for (int i = 0; i < camps && trainers.Count > 0; i++) TitlePlace(trainers[rng.Next(trainers.Count)], centre, 1, R(32));
        var core = TitleBounds(0, titleTaken.Count, centre);

        // Towers just outside the corners of the town.
        var towers = B.Where(TitleIsTower).Where(b => TitleOrder(b) >= age - 1).OrderBy(_ => rng.Next()).Take(2).ToList();
        if (towers.Count == 0) towers = B.Where(TitleIsTower).OrderByDescending(TitleOrder).Take(1).ToList();
        var corners = new[] { new Vector2Int(core.xMin - 3, core.yMin - 3), new Vector2Int(core.xMax + 1, core.yMin - 3), new Vector2Int(core.xMin - 3, core.yMax + 1), new Vector2Int(core.xMax + 1, core.yMax + 1) }
            .OrderBy(_ => rng.Next()).Take(Range(2, 4)).ToArray();
        for (int i = 0; i < corners.Length && towers.Count > 0; i++) TitlePlace(towers[i % towers.Count], corners[i], 0, 3);
        core = TitleBounds(0, titleTaken.Count, centre);

        // Trees and bushes inside the walls go (the view clears their tiles too).
        clear = new RectInt(core.xMin - 2, core.yMin - 2, core.width + 4, core.height + 4);
        for (int y = clear.yMin; y < clear.yMax; y++)
            for (int x = clear.xMin; x < clear.xMax; x++)
                Map.SetDeposit(x, y, null);

        // Gatherers and mines on real deposits, outside the walls.
        int gatherers = tier switch { 0 => Range(1, 2), 1 => Range(2, 3), 2 => Range(2, 5), _ => Range(4, 7) };
        foreach (var g in Some(B.Where(b => b.requiredDeposit && !TitleShore(b) && (TitleOrder(b) >= age - 4 || TitleOrder(b) == 0 && age <= 3)), gatherers))
            TitlePlace(g, centre, 1, 44, core);

        // Walls round the town, with a gate in each side.
        var wall = Newest(b => b.IsWall && !b.id.Contains("Gate") && !TitleShore(b) && !b.id.StartsWith("Trade") && !b.id.StartsWith("Dis") && !b.id.StartsWith("Utl"));
        if (wall && Chance(new[] { 0.1, 0.45, 0.7, 0.75, 0.6 }[tier]))
        {
            var gate = B.FirstOrDefault(b => b.IsWall && b.id.Contains("Gate") && TitleOrder(b) == TitleOrder(wall));
            var r = new RectInt(core.xMin - 1, core.yMin - 1, core.width + 2, core.height + 2);
            int mx = r.xMin + r.width / 2, my = r.yMin + r.height / 2;
            for (int x = r.xMin; x < r.xMax; x++)
                for (int y = r.yMin; y < r.yMax; y++)
                {
                    if (x != r.xMin && x != r.xMax - 1 && y != r.yMin && y != r.yMax - 1) continue;
                    bool isGate = (x == mx && (y == r.yMin || y == r.yMax - 1)) || (y == my && (x == r.xMin || x == r.xMax - 1));
                    TitleTryAt(isGate && gate ? gate : wall, new Vector2Int(x, y), 0);
                }
        }

        // A lived-in look: most buildings carry some of their upgrades, at full health.
        foreach (var b in State.buildings)
        {
            var d = db.GetBuilding(b.definitionId);
            if (d != null && d.upgrades.Count > 0 && age >= 1)
                b.upgradeLevel = rng.NextDouble() < 0.6 ? d.upgrades.Count : rng.Next(0, d.upgrades.Count + 1);
        }
        RecalculateDerived();
        foreach (var b in State.buildings) b.health = StatsOf(b).maxHealth;
        State.population = Housing * 0.85f;
        RecalculateDerived();
        RaiseChanged();
        return core;
    }

    private RectInt TitleBounds(int from, int to, Vector2Int centre)
    {
        int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
        for (int i = from; i < to; i++)
        {
            var r = titleTaken[i];
            x0 = Mathf.Min(x0, r.xMin); y0 = Mathf.Min(y0, r.yMin); x1 = Mathf.Max(x1, r.xMax); y1 = Mathf.Max(y1, r.yMax);
        }
        return x0 == int.MaxValue ? new RectInt(centre, Vector2Int.one) : new RectInt(x0, y0, x1 - x0, y1 - y0);
    }

    private bool TitlePlace(BuildingDefinition def, Vector2Int near, int pad, int maxR, RectInt? outside = null)
    {
        if (!def) return false;
        foreach (var o in titleSpiral)
        {
            if (o.sqrMagnitude > maxR * maxR) continue;
            var cell = near + o - new Vector2Int(def.size.x / 2, def.size.y / 2);
            if (outside is RectInt keepOut && new RectInt(keepOut.xMin - 2, keepOut.yMin - 2, keepOut.width + 4, keepOut.height + 4).Overlaps(new RectInt(cell, def.size))) continue;
            if (TitleTryAt(def, cell, pad)) return true;
        }
        return false;
    }

    private bool TitleTryAt(BuildingDefinition def, Vector2Int cell, int pad)
    {
        var padded = new RectInt(cell.x - pad, cell.y - pad, def.size.x + 2 * pad, def.size.y + 2 * pad);
        foreach (var t in titleTaken) if (t.Overlaps(padded)) return false;
        if (!CheckSite(def, cell, out float quality, out _)) return false;
        State.buildings.Add(new BuildingState
        {
            instanceId = State.nextBuildingId++, definitionId = def.id, cell = cell,
            health = def.maxHealth, buildProgress = 1, siteQuality = quality, autoRepair = true
        });
        titleTaken.Add(new RectInt(cell, def.size));
        return true;
    }
}
