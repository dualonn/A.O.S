using System.Text;
using UnityEngine;

/// <summary>Utilities in words: build-menu tooltips, the selection panel, the Utilities section (see Utilities.cs, UtlDistricts.cs).</summary>
public partial class Settlement
{
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
                    sb.Append($"<color=#8cf>Water for {spec.amount:0} people</color> in the district {spec.cover} tiles round it (and the buildings beside those)");
                    if (spec.mains) sb.Append($"; joins the mains (towers and pumps within {spec.link} tiles share their water)");
                    if (spec.clean) sb.Append(", <color=#9ef>purified</color>");
                    if (spec.feeds) sb.Append(". Fills an aqueduct it touches");
                    sb.Append('\n');
                    break;
                case UtlRole.WaterNode:
                    sb.Append($"<color=#8cf>Hands out the mains' water</color> to the district {spec.cover} tiles round it; needs a pump or purifier within {spec.link} tiles\n");
                    break;
                case UtlRole.Aqueduct:
                    sb.Append($"<color=#8cf>Brings water</color> to a district with none: a line from a river or lake (or a water mill or pump) waters the buildings along it and its fountains ({UtlCatalog.AqueductPeople:0} people, more with mills feeding it). Drag out a line\n");
                    break;
                case UtlRole.Fountain:
                    sb.Append($"<color=#8cf>Water for {spec.amount:0} people</color> in the district {spec.cover} tiles round it when it touches a fed aqueduct; alone, a basin for {UtlCatalog.FountainBasinPeople:0} within {UtlCatalog.FountainBasinCover}\n");
                    break;
                case UtlRole.Generator:
                    sb.Append($"<color=#fe6>Makes {spec.amount:0} power</color> for the district {spec.cover} tiles round it (buildings pass it on to their neighbours); power lines carry it to other districts and towns");
                    if (spec.existing) sb.Append(" (as well as its power cells)");
                    if (spec.cooling) sb.Append(". Needs cooling water close by (a river, or a water supply), or runs at 60%");
                    sb.Append('\n');
                    break;
                case UtlRole.PowerNode:
                    if (spec.district)
                        sb.Append($"<color=#fe6>Powers a district</color> {spec.cover} tiles round it from the grid it's linked to, and links lines within {spec.link} tiles\n");
                    else
                        sb.Append($"<color=#fe6>Power line</color>: links districts and towns into one grid. Joins the district it stands in, and {(spec.wireless ? "any line, mast or plant" : "lines and plants")} within {spec.link} tiles{(spec.wireless ? " (wireless)" : "")}. It doesn't power buildings itself\n");
                    if (spec.world) sb.Append("<color=#fe6>From orbit</color>: every grid on this world becomes one\n");
                    break;
                case UtlRole.Data:
                    sb.Append(spec.amount > 0
                        ? $"<color=#7ef>Data: {spec.amount:0.#} compute</color> (research +{spec.amount * 2.5f:0.#}%); works in the district {spec.cover} tiles round it join the network and run 6% faster; links {spec.link} tiles\n"
                        : $"<color=#7ef>Carries data</color>: works within {spec.cover} tiles join the network; links {spec.link} tiles\n");
                    if (spec.world) sb.Append("<color=#7ef>Every data network on this world becomes one</color>\n");
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
                ? $"<color=#fe6>Uses {demand} power</color>: +{UtlCatalog.PoweredBonus:P0} output in a powered district, -{1f - UtlCatalog.UnpoweredFactor:P0} without\n"
                : $"<color=#fe6>Uses {demand} power</color> in a powered district: +{UtlCatalog.PoweredBonus:P0} output\n");
        float pollution = UtlCatalog.PollutionOf(def);
        if (pollution > 0.05f) sb.Append($"<color=#c9a>Pollution {pollution:0.#}</color> (smog lowers mood and growth nearby)\n");
        return sb.ToString();
    }

    private string UtlTownName(UtlNet n, int town) => town >= 0 && town < n.towns.Count ? n.towns[town].name : "this town";

    private static UtlGrid UtlGridAtIndex(System.Collections.Generic.List<UtlGrid> grids, int g) => g >= 0 && g < grids.Count ? grids[g] : default;

    /// <summary>Selection panel lines: is it powered, watered, in smog; what its network is doing.</summary>
    public void UtlSelectionInfo(StringBuilder sb, BuildingState state, BuildingDefinition def)
    {
        if (state == null || def == null || !state.IsBuilt) return;
        var n = UtilitiesHere;
        if (n.version == 0) return;
        int town = n.townOf.TryGetValue(state.instanceId, out int t) ? t : -1;
        if (n.info.TryGetValue(state.instanceId, out var inf))
        {
            switch (inf.code)
            {
                case 1: case 3: case 4:
                    sb.Append(inf.b > 0f
                        ? $"<color=#8cf>{UtlGridName(n, n.waterGrids, inf.grid, "water")}: serving {inf.a:0}/{inf.b:0} people</color>{(inf.code == 4 ? " <color=#9ef>(purified)</color>" : "")}{(inf.code == 1 ? " from its aqueduct" : "")}\n"
                        : "<color=#f99>No water here</color>: a tower needs a pump or purifier on its mains\n");
                    break;
                case 2: sb.Append("<color=#fc6>A basin only</color>: set it beside an aqueduct that reaches a river to water the whole district\n"); break;
                case 5:
                {
                    var g = UtlGridAtIndex(n.powerGrids, inf.grid);
                    sb.Append($"<color=#fe6>Making {inf.a:0} power</color> for the {UtlGridName(n, n.powerGrids, inf.grid)} ({g.supply:0} made, {g.demand:0} needed)\n");
                    break;
                }
                case 9: sb.Append($"<color=#fc6>Short of cooling water: making {inf.a:0} power</color> (60%). A river within 4 tiles or a water supply fixes it\n"); break;
                case 6:
                {
                    UtlCatalog.TryGet(def.id, out var spec);
                    var g = UtlGridAtIndex(n.powerGrids, inf.grid);
                    if (inf.a <= 0f && g.generators == 0)
                        sb.Append(spec != null && spec.district
                            ? "<color=#f99>No generator on this grid</color>: link it to one with power lines, or build one in reach\n"
                            : $"<color=#f99>Not linked to a generator</color>: lines carry power from a district with one (link {spec?.link ?? 0} tiles)\n");
                    else
                        sb.Append($"<color=#fe6>{UtlGridName(n, n.powerGrids, inf.grid)}: {inf.a:0} power for {inf.b:0} needed</color>\n");
                    break;
                }
                case 7: sb.Append($"<color=#7ef>{UtlGridName(n, n.dataGrids, inf.grid, "network")}: {inf.a:0.#} compute</color> (all networks {inf.b:0.#}: research +{(n.research - 1f):P0})\n"); break;
            }
        }
        int demand = UtlCatalog.Demand(def);
        if (demand > 0)
        {
            bool needs = UtlCatalog.NeedsPower(def);
            if (n.power.TryGetValue(state.instanceId, out float share))
            {
                int g = n.gridOf.TryGetValue(state.instanceId, out int gi) ? gi : -1;
                string grid = g >= 0 ? $", {UtlGridName(n, n.powerGrids, g)}" : "";
                sb.Append(share >= 0.999f ? $"<color=#fe6>Powered</color> ({demand} power{grid})\n" : $"<color=#fc6>Brownout: {share:P0} of its power</color>{(grid.Length > 0 ? $" <color=#aaa>({grid.Substring(2)})</color>" : "")}\n");
            }
            else if (needs)
                sb.Append(n.grace ? $"<color=#fc6>No power</color> <color=#aaa>(old works' grace: {n.graceLeft / 60f:0} min left, then -15% output)</color>\n"
                                  : $"<color=#f99>No power: -15% output</color>. Build a generator or substation in reach, or a power line from a powered district of {UtlTownName(n, town)}\n");
        }
        if (def.housing > 0 && n.waterNeed > 0f)
            sb.Append(n.water.Contains(state.instanceId) ? (n.clean.Contains(state.instanceId) ? "<color=#9ef>Clean running water</color>\n" : "<color=#8cf>Has water</color>\n")
                : "<color=#aaa>No water: no well, fountain or tower in reach (or its network is full)</color>\n");
        float p = UtlSmogAt(n, state.cell.x + def.size.x * 0.5f, state.cell.y + def.size.y * 0.5f);
        if (p >= 3f) sb.Append($"<color=#c9a>Air: {UtlCatalog.SmogWord(p)} ({p:0})</color>{(p >= 28f && State.ageIndex >= UtlCatalog.IndustrialAge ? ": acid rain" : "")}\n");
    }

    private readonly int[] utlTop = new int[5];

    /// <summary>The Utilities section's summary: one line per network, then the biggest towns.</summary>
    public string UtlSummaryText()
    {
        var n = UtilitiesHere;
        var sb = new StringBuilder();
        bool powerEra = State.ageIndex >= UtlCatalog.IndustrialAge || n.HasGrid;
        sb.Append(n.waterNeed > 0f
            ? $"<color=#8cf>Water</color> {n.WaterCoverage:P0} of homes · {n.waterServed:0}/{n.waterCapacity:0}\n"
            : "<color=#8cf>Water</color> <color=#aaa>no homes yet</color>\n");
        if (powerEra)
            sb.Append(n.HasGrid
                ? $"<color=#fe6>Power</color> {n.supply:0} made · {n.demand:0} needed · {n.grids} grid{(n.grids == 1 ? "" : "s")}{(n.brownout ? " <color=#f99>brownout</color>" : "")}{(n.unpowered > 0 ? $" · <color=#fc6>{n.unpowered} off-grid</color>" : "")}\n"
                : $"<color=#fe6>Power</color> <color=#fc6>no grid yet</color> · {n.demand:0} needed\n");
        if (State.ageIndex >= UtlCatalog.InformationAge || n.dataNodes > 0)
            sb.Append($"<color=#7ef>Data</color> {n.compute:0.#} compute · research +{n.research - 1f:P0}\n");
        sb.Append($"<color=#c9a>Air</color> {UtlCatalog.SmogWord(n.smogAvg)} in town{(n.smogWorst >= 8f ? $" · worst {UtlCatalog.SmogWord(n.smogWorst)}" : "")}");

        // Towns, biggest first (towns before outposts; outposts only with something to say).
        int more = 0;
        for (int k = 0; k < utlTop.Length; k++) utlTop[k] = -1;
        for (int t = 0; t < n.towns.Count; t++)
        {
            var town = n.towns[t];
            if (town.outpost && town.powerNeed <= 0f && town.waterNeed <= 0f) continue;
            if (town.powerNeed <= 0f && town.waterNeed <= 0f) continue;
            int slot = -1;
            for (int k = 0; k < utlTop.Length; k++)
            {
                if (utlTop[k] < 0) { slot = k; break; }
                var o = n.towns[utlTop[k]];
                if (town.outpost != o.outpost ? !town.outpost : town.buildings > o.buildings) { slot = k; break; }
            }
            if (slot < 0) { more++; continue; }
            if (utlTop[utlTop.Length - 1] >= 0) more++;
            for (int k = utlTop.Length - 1; k > slot; k--) utlTop[k] = utlTop[k - 1];
            utlTop[slot] = t;
        }
        for (int k = 0; k < utlTop.Length && utlTop[k] >= 0; k++)
        {
            var town = n.towns[utlTop[k]];
            sb.Append($"\n  <b>{town.name}</b>{(town.outpost ? " <color=#aaa>outpost</color>" : "")}: ");
            bool any = false;
            if (powerEra && town.powerNeed > 0f)
            {
                if (town.powerGrid >= 0)
                {
                    var g = n.powerGrids[town.powerGrid];
                    bool shortOf = g.demand > g.supply * 1.001f;
                    sb.Append($"{(shortOf ? "<color=#f99>" : "")}{g.supply:0}/{g.demand:0} power{(shortOf ? "</color>" : "")}");
                    if (town.powerOff > 0) sb.Append($" <color=#fc6>({town.powerOff} off-grid)</color>");
                }
                else sb.Append("<color=#f99>no power</color>");
                any = true;
            }
            if (town.waterNeed > 0f)
            {
                if (any) sb.Append(", ");
                if (town.waterGrid >= 0)
                {
                    var g = n.waterGrids[town.waterGrid];
                    bool dry = town.waterServed < town.waterNeed * 0.95f;
                    sb.Append($"{(dry ? "<color=#fc6>" : "")}water for {g.supply:0}/{g.demand:0}{(dry ? "</color>" : "")}");
                    if (dry) sb.Append($" <color=#aaa>({town.waterNeed - town.waterServed:0} without)</color>");
                }
                else sb.Append("<color=#f99>no water</color>");
            }
        }
        if (more > 0) sb.Append($"\n  <color=#aaa>+{more} more</color>");
        if (n.grace) sb.Append($"\n<color=#aaa>Old works' grace: {n.graceLeft / 60f:0} min left</color>");
        return sb.ToString();
    }

    /// <summary>The Utilities section's hover help.</summary>
    public string UtlHelpText()
    {
        var n = UtilitiesHere;
        var sb = new StringBuilder("<b>Utilities</b>\n");
        sb.Append("Every network works by <b>districts</b>: a source serves the buildings within its reach, and each of those passes it on to the buildings beside it " +
                  $"(up to {UtlCatalog.ConductHops} buildings on). No pole by every house.\n");
        sb.Append($"<color=#8cf>Water</color>: wells, cisterns, fountains, water mills, pumps and towers, purifiers. Aqueducts bring water to districts with no source. Homes with water grow up to {0.12f + 0.04f:P0} faster and are happier, and sickness spreads less there. Watered farms are irrigated (+8%).\n");
        sb.Append($"   {n.waterPoints} water points · {n.waterNets} network{(n.waterNets == 1 ? "" : "s")} · {n.WaterCoverage:P0} of homes served{(n.cleanServed > 0 ? $" ({n.cleanServed:0} on clean water)" : "")}\n");
        sb.Append("<color=#fe6>Power</color> (Industrial Age on): a generator powers its district, a substation another district off the same grid. Poles and pylons are lines: they join districts and towns into one grid; " +
                  "wireless masts (Electric Age on) link them from far off, and a beam relay (Stellar Age) makes one grid of the whole world. " +
                  $"Works on a grid run {UtlCatalog.PoweredBonus:P0} faster; Electric Age works without power {1f - UtlCatalog.UnpoweredFactor:P0} slower. Short of power, every building on that grid slows (a brownout).\n");
        sb.Append($"   {n.generators} generators · {n.powerNodes} lines and substations · {n.grids} grid{(n.grids == 1 ? "" : "s")} · {n.supply:0} made, {n.demand:0} needed\n");
        sb.Append("<color=#7ef>Data</color> (Information Age on): data centres speed research (2.5% per compute, up to 40%); works in a connected district run 6% faster.\n");
        sb.Append("<color=#c9a>Pollution</color>: mines, furnaces, refineries and power stations. Smog lowers mood and growth, spoils crops and timber, and the worst brings acid rain. Forests, parks, scrubbers, recycling, clean power and research clear it.\n");
        sb.Append("<color=#aaa>Overlays show each district and the buildings it reaches (they also show by themselves while you place a utility).</color>");
        return sb.ToString();
    }
}
