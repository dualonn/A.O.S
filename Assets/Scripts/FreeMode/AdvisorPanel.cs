using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>One thing the advisor wants you to know about, with where "Show me" takes you.</summary>
public class AdvisorIssue
{
    public enum Go { None, Pan, PanSelect, Build, Section, Culture, People, Civics, CivicsTaxes, World, Custom }

    /// <summary>The kind of problem (for muting: all issues of a kind go quiet together).</summary>
    public string kind;
    /// <summary>0..100: how much it matters right now (higher first).</summary>
    public float urgency;
    /// <summary>One short plain sentence for the card.</summary>
    public string text;
    /// <summary>The detail, on hover.</summary>
    public string tip;
    /// <summary>Good news: only shown when nothing is wrong, never counted as an issue.</summary>
    public bool good;

    public Go go;
    public Vector3 at;
    public Vector2Int cell;
    public string arg;
    public int world;
    public Action custom;

    // Fluent "Show me" targets: scan.Add(...).Pan(p)
    public AdvisorIssue Pan(Vector3 p) { go = Go.Pan; at = p; return this; }
    public AdvisorIssue Select(Vector3 p, Vector2Int c) { go = Go.PanSelect; at = p; cell = c; return this; }
    public AdvisorIssue Build(string search) { go = Go.Build; arg = search; return this; }
    public AdvisorIssue Section(string name) { go = Go.Section; arg = name; return this; }
    public AdvisorIssue Open(Go panel) { go = panel; return this; }
    public AdvisorIssue World(int index) { go = Go.World; world = index; return this; }
    public AdvisorIssue Do(Action a) { go = Go.Custom; custom = a; return this; }

    internal void Clear()
    {
        kind = text = tip = arg = null;
        urgency = 0f; good = false; go = Go.None; at = default; cell = default; world = 0; custom = null;
    }
}

/// <summary>What a check gets: the game, and a way to report issues (pooled: no garbage per issue).</summary>
public class AdvisorScan
{
    public Settlement s;
    public FreeModeController controller;
    /// <summary>How many scans have run (checks that are dearer can run every few scans).</summary>
    public int pass;
    internal readonly List<AdvisorIssue> pool = new List<AdvisorIssue>();
    internal int count;

    public AdvisorIssue Add(string kind, float urgency, string text, string tip = null)
    {
        if (count == pool.Count) pool.Add(new AdvisorIssue());
        var i = pool[count++];
        i.Clear();
        i.kind = kind; i.urgency = urgency; i.text = text; i.tip = tip;
        return i;
    }

    public AdvisorIssue Good(string kind, string text, string tip = null)
    {
        var i = Add(kind, 0f, text, tip);
        i.good = true;
        return i;
    }

    /// <summary>The world position of a building's middle.</summary>
    public Vector3 Where(BuildingState b, BuildingDefinition def)
    {
        var size = def != null ? def.size : Vector2Int.one;
        if (controller != null && controller.Grid != null) return controller.Grid.AreaCenter(b.cell, size);
        return new Vector3(b.cell.x + size.x * 0.5f, b.cell.y + size.y * 0.5f, 0f);
    }

    /// <summary>" on Glimmer" when you have more than one world (so lines stay short at home).</summary>
    public string On => s.State.worlds.Count > 1 ? $" on {s.CurrentWorld.name}" : "";
}

/// <summary>
/// The advisor's checks: each a small static method that looks at one system and reports what's wrong.
/// Add more with AdvisorChecks.Add(MyCheck) from a [RuntimeInitializeOnLoadMethod] (static methods only,
/// so adding twice is ignored). Keep each cheap: they run every couple of seconds, not every frame.
/// </summary>
public static class AdvisorChecks
{
    public delegate void Check(AdvisorScan scan);

    public static readonly List<Check> All = new List<Check>
    {
        Raids, Food, Disasters, Civics, Mood, Ruined, Utilities, Housing, People, Trade, Sea, Research, OtherWorlds, GoodNews,
    };

    public static void Add(Check c)
    {
        if (c != null && !All.Contains(c)) All.Add(c);
    }

    // ---------- Food (tracked from the store going up and down, per world) ----------

    private static readonly List<float> foodLast = new List<float>(), foodAt = new List<float>(), foodRate = new List<float>();

    /// <summary>Food per second (net) on a world, smoothed over the last scans; 0 until known.</summary>
    public static float FoodRate(int world) => world < foodRate.Count ? foodRate[world] : 0f;

    private static void TrackFood(Settlement s)
    {
        var worlds = s.State.worlds;
        while (foodLast.Count < worlds.Count) { foodLast.Add(-1f); foodAt.Add(-1f); foodRate.Add(0f); }
        float now = s.State.playTime;
        for (int i = 0; i < worlds.Count; i++)
        {
            float stock = s.AdvisorFoodOf(worlds[i]);
            if (foodAt[i] >= 0f)
            {
                float dt = now - foodAt[i];
                if (dt < 0.5f) continue;   // paused: keep what we knew
                float rate = (stock - foodLast[i]) / dt;
                foodRate[i] = Mathf.Lerp(foodRate[i], rate, 0.35f);
            }
            foodLast[i] = stock;
            foodAt[i] = now;
        }
    }

    internal static void ResetTracking()
    {
        foodLast.Clear(); foodAt.Clear(); foodRate.Clear();
    }

    private static string Minutes(float seconds) =>
        seconds < 90f ? $"{Mathf.Max(5f, Mathf.Round(seconds / 5f) * 5f):0} s" : $"{seconds / 60f:0} min";

    private static void Food(AdvisorScan scan)
    {
        var s = scan.s;
        TrackFood(s);
        var food = s.Database.food;
        float pop = s.State.population;
        if (food == null || pop < 1f) return;
        float stock = s.GetAmount(food);
        float rate = FoodRate(s.State.currentWorld);
        float eat = pop * s.Database.foodPerPersonPerSecond;
        string detail = $"<b>Food</b>\n{stock:0} in store · {(rate >= 0 ? "+" : "")}{rate * 60f:0.#} a minute, all told\n" +
                        $"{pop:0} people eat about {eat * 60f:0.#} a minute.\nShow me: the Build menu, filtered to what makes food.";
        if (stock < 1f)
        {
            scan.Add("food", 97f, $"Your people are starving{scan.On}: build food", detail).Build(food.displayName);
            return;
        }
        if (rate < -0.005f)
        {
            float runway = stock / -rate;
            if (runway < 300f)
                scan.Add("food", Mathf.Clamp(92f - runway / 8f, 55f, 92f), $"Food runs out in {Minutes(runway)}{scan.On}", detail).Build(food.displayName);
        }
    }

    // ---------- Raids (one generic check: whatever is coming next) ----------

    private static void Raids(AdvisorScan scan)
    {
        var s = scan.s;
        if (s.RaidsOff || !s.IsFounded || scan.controller == null) return;
        Vector3 town = scan.controller.TownCenterPosition;
        if (scan.controller.WaveInProgress)
        {
            scan.Add("raid", 94f, "Under attack: defend the town",
                "<b>Under attack</b>\nTroops on <b>Defend</b> stay near the town; <b>Attack</b> sends them after the enemy. " +
                "Towers and walls near homes help most.").Pan(town);
            return;
        }
        float t = s.State.secondsUntilWave;
        if (t > 0f && t < 60f)
            scan.Add("raid", 72f - t / 3f, $"Attack expected in {Minutes(t)}",
                "<b>Trouble on the way</b>\nThe next attackers arrive soon. Get troops home, repair walls and towers, " +
                "and keep the townsfolk inside the walls.").Pan(town);
    }

    // ---------- Disasters ----------

    private static void Disasters(AdvisorScan scan)
    {
        var s = scan.s;
        var d = s.DisasterAlert;
        if (d == null) return;
        string line = s.DisStatus(d);
        scan.Add("disaster", d.struck ? 84f : 78f, line,
            $"<b>{line}</b>\nShow me: the camera goes there. The alert under the top bar follows it too.").Pan(new Vector3(d.centre.x, d.centre.y, 0f));
    }

    // ---------- Government ----------

    private static void Civics(AdvisorScan scan)
    {
        var s = scan.s;
        var w = s.CurrentWorld;
        var c = s.CivOf(w);
        float now = s.State.playTime;
        if (c.revoltAt > now)
        {
            scan.Add("revolt", 89f, $"Rebels rise in {Minutes(c.revoltAt - now)}: calm them",
                "<b>Rebels are gathering</b>\nRaise the mood (festivals, culture, faith, lower taxes) or grant a pardon in the Government panel, " +
                "and they melt away.").Open(AdvisorIssue.Go.Civics);
            return;
        }
        if (c.strikeUntil > now)
            scan.Add("revolt", 55f, "General strike: work is slowed",
                $"<b>General strike</b>\nAfter a revolt, work runs slower for another {Minutes(c.strikeUntil - now)}.").Open(AdvisorIssue.Go.Civics);
        else if (s.CivRevoltRisk(w) >= 2)
            scan.Add("revolt", 62f, "Revolt brewing: mood and trust are low",
                "<b>Revolt brewing</b>\nUnhappy people under a harsh or doubted government may rise. Raise the mood, ease harsh laws, or lower taxes.").Open(AdvisorIssue.Go.Civics);

        if (s.CivIsCustomary(w)) return;
        int tax = s.CivTaxLevel(w);
        bool heavy = tax >= 4;
        if (c.legitimacy < 30f)
            scan.Add("legit", 48f + (30f - c.legitimacy) * 0.6f, heavy ? "Legitimacy low under heavy taxes" : "Legitimacy is low",
                $"<b>Legitimacy {c.legitimacy:0}</b> ({Settlement.CivLegitName(c.legitimacy)}), {s.CivLegitRate(w):+0.#;-0.#} a minute\n" +
                $"Taxes: {Settlement.CivTaxNames[Mathf.Clamp(tax, 0, Settlement.CivTaxNames.Length - 1)]}. " +
                "Good mood, culture and fitting laws build it up; harsh laws and high taxes wear it down.")
                .Open(heavy ? AdvisorIssue.Go.CivicsTaxes : AdvisorIssue.Go.Civics);
    }

    // ---------- Mood ----------

    /// <summary>The reason that pulls mood down the most ("-15 starving" -> "starving").</summary>
    private static string WorstReason(List<string> reasons, out int worst)
    {
        worst = 0;
        string best = null;
        if (reasons == null) return null;
        for (int i = 0; i < reasons.Count; i++)
        {
            string r = reasons[i];
            int open = r.IndexOf('>'), close = open >= 0 ? r.IndexOf('<', open) : -1;
            if (open < 0 || close < 0 || !int.TryParse(r.Substring(open + 1, close - open - 1), out int v)) continue;
            if (v < worst)
            {
                worst = v;
                int end = r.IndexOf("</color>", StringComparison.Ordinal);
                best = end >= 0 ? r.Substring(end + 8).Trim() : r;
            }
        }
        return best;
    }

    private static void Mood(AdvisorScan scan)
    {
        var s = scan.s;
        if (s.State.population < 3f) return;
        float m = s.MoodHere;
        if (m >= 40f) return;
        var reasons = s.MoodReasonsHere;
        string why = WorstReason(reasons, out _);
        string tip = $"<b>Mood {m:0}</b> ({Settlement.MoodName(m)})\n" + (reasons != null ? string.Join("\n", reasons) : "");
        scan.Add("mood", Mathf.Clamp(70f - m, 30f, 70f), why != null ? $"People are unhappy: {why}" : "People are unhappy", tip)
            .Open(AdvisorIssue.Go.Culture);
    }

    // ---------- Ruined buildings ----------

    private static void Ruined(AdvisorScan scan)
    {
        var s = scan.s;
        var db = s.Database;
        int count = 0, auto = 0;
        BuildingState first = null;
        BuildingDefinition firstDef = null;
        var list = s.State.buildings;
        for (int i = 0; i < list.Count; i++)
        {
            var b = list[i];
            if (!b.IsBuilt || b.health > 0f) continue;
            count++;
            if (b.autoRepair) auto++;
            if (first == null || (first.autoRepair && !b.autoRepair)) { first = b; firstDef = db.GetBuilding(b.definitionId); }
        }
        if (count == 0) return;
        string name = firstDef != null ? firstDef.displayName : "building";
        string text = count == 1 ? $"{name} ruined: repair it" : $"{count} buildings ruined: repair them";
        scan.Add("ruined", auto == count ? 30f : 58f, text,
            $"<b>Ruined buildings</b>\n{count} stand ruined and do nothing until repaired{(auto > 0 ? $" ({auto} will repair themselves once you can afford it)" : "")}.\n" +
            "Show me: the camera goes to one and selects it, so you can press Repair.").Select(scan.Where(first, firstDef), first.cell);
    }

    // ---------- Utilities ----------

    private static void Utilities(AdvisorScan scan)
    {
        var s = scan.s;
        if (!s.IsFounded) return;
        var u = s.AdvisorUtilities();
        if (u.brownout)
            scan.Add("power", 64f, $"Brownout{scan.On}: build a generator",
                $"<b>Brownout</b>\n{u.supply:0} power made, {u.demand:0} needed: every building on the grid slows. " +
                "Build generators (or rank them up), or switch some works off.").Build("Utilities");
        else if (u.hasGrid && u.unpowered > 0)
            scan.Add("power", 28f, $"{u.unpowered} works off the grid: extend the lines",
                "<b>Off the grid</b>\nThese works need power but no power line reaches them. Build a power node or line near them.").Section("Utilities");
        if (u.waterNeed > 8f && u.waterCoverage < 0.5f)
            scan.Add("water", 34f, $"Only {u.waterCoverage:P0} of homes have water",
                "<b>Water</b>\nHomes with water grow faster, are happier and fall sick less. Build wells (later pumps and water towers) near homes.").Build("Utilities");
        if (u.smogWorst >= 20f)
        {
            string near = s.AdvisorNearestBuildingName(u.smogAt, out _);
            scan.Add("smog", Mathf.Clamp(22f + (u.smogWorst - 20f) * 0.5f, 22f, 50f),
                near != null ? $"Smog over the {near} quarter" : "Smog over the town",
                $"<b>Smog</b> ({UtlCatalog.SmogWord(u.smogWorst)})\nDirty works foul the air round them: mood falls, and later acid rain. " +
                "Plant trees, move homes away, or build filters and cleaner power.").Pan(new Vector3(u.smogAt.x, u.smogAt.y, 0f));
        }
    }

    // ---------- Housing ----------

    private static void Housing(AdvisorScan scan)
    {
        var s = scan.s;
        if (!s.IsFounded || s.Housing <= 0 || s.Population < s.Housing) return;
        var food = s.Database.food;
        if (food != null && s.GetAmount(food) < 1f) return;   // food first: no one moves in while starving
        scan.Add("housing", 46f, "Homes full: build housing",
            $"<b>Homes full</b>\n{s.Population}/{s.Housing} people. New people only move in with room to live.").Build("Housing");
    }

    // ---------- People's needs ----------

    private static void People(AdvisorScan scan)
    {
        var s = scan.s;
        if (s.State.population < 5f) return;
        float met = s.PeopleNeedsMet;
        if (met >= 0.85f) return;
        var list = s.PeopleSuggestions(1);
        if (list.Count == 0 || list[0].StartsWith("<", StringComparison.Ordinal)) return;
        string line = list[0];
        // "Build a Scriptorium: books is 40% supplied (...)" -> "Books only 40% supplied: build a Scriptorium"
        string text = line, search = null;
        int colon = line.IndexOf(": ", StringComparison.Ordinal);
        if (colon > 0)
        {
            string act = line.Substring(0, colon), state = line.Substring(colon + 2);
            int paren = state.IndexOf(" (", StringComparison.Ordinal);
            if (paren > 0) state = state.Substring(0, paren);
            int isAt = state.IndexOf(" is ", StringComparison.Ordinal);
            if (isAt > 0) state = state.Substring(0, isAt) + " only " + state.Substring(isAt + 4);
            if (state.Length > 0) state = char.ToUpperInvariant(state[0]) + state.Substring(1);
            text = $"{state}: {char.ToLowerInvariant(act[0])}{act.Substring(1)}";
            if (act.StartsWith("Build a ", StringComparison.Ordinal)) search = act.Substring(8);
        }
        var issue = scan.Add("needs", 36f + (0.85f - met) * 40f, text,
            $"<b>Needs</b> {met:P0} met\n{line}\nUnmet needs cost mood and keep homes from growing.\n<color=#aaa>{s.PeopleClassSummary()}</color>");
        if (search != null) issue.Build(search); else issue.Open(AdvisorIssue.Go.People);
    }

    // ---------- Trade by land ----------

    private static void Trade(AdvisorScan scan)
    {
        var s = scan.s;
        var routes = s.TradeRoutesHere;
        if (routes == null || routes.Count == 0) return;
        float now = s.State.playTime;
        TradeRoute cut = null, lost = null;
        int cuts = 0;
        for (int i = 0; i < routes.Count; i++)
        {
            var r = routes[i];
            if (r.blocked) { cuts++; if (cut == null) cut = r; }
            else if (now < r.lostUntil && lost == null) lost = r;
        }
        if (cut != null)
        {
            var post = s.TradePost(cut.a);
            var issue = scan.Add("trade", 40f, cuts > 1 ? $"{cuts} trade routes cut off" : $"Trade route {Settlement.TradeTownName(cut.a)}-{Settlement.TradeTownName(cut.b)} cut off",
                "<b>Cut off</b>\nCaravans can't find a way between the two towns (water, walls or the map's edge). A road or a bridge reopens it.");
            if (post != null) issue.Pan(s.TradePostDoor(post)); else issue.Section("Trade");
        }
        if (lost != null)
        {
            var post = s.TradePost(lost.a);
            var issue = scan.Add("trade", 34f, $"Caravan lost on the {Settlement.TradeTownName(lost.a)} road",
                $"<b>Caravan lost</b>\nBetween {Settlement.TradeTownName(lost.a)} and {Settlement.TradeTownName(lost.b)}; another sets out in {lost.lostUntil - now:0}s. " +
                "Towers or troops along the road keep it safe.");
            if (post != null) issue.Pan(s.TradePostDoor(post)); else issue.Section("Trade");
        }
    }

    // ---------- Trade by sea ----------

    private static void Sea(AdvisorScan scan)
    {
        var s = scan.s;
        var routes = s.SeaRoutesHere;
        if (routes == null || routes.Count == 0) return;
        float now = s.State.playTime;
        SeaRoute cut = null, lost = null, risky = null;
        float worstRisk = 0f;
        for (int i = 0; i < routes.Count; i++)
        {
            var r = routes[i];
            if (!r.pathKnown) continue;
            if (r.blocked) { if (cut == null) cut = r; continue; }
            if (now < r.lostUntil) { if (lost == null) lost = r; continue; }
            float risk = s.SeaPirateRisk(r);
            if (risk > worstRisk) { worstRisk = risk; risky = r; }
        }
        if (cut != null)
            scan.Add("sea", 38f, $"Sea route {s.SeaRouteName(cut)} cut off", $"<b>Cut off</b>\n{s.SeaRouteStatus(cut)}").Section("SeaTrade");
        if (lost != null)
            scan.Add("sea", 40f, $"Ship lost on the {s.SeaRouteName(lost)} route",
                $"<b>Ship lost</b>\n{s.SeaRouteStatus(lost)}\nLighthouses and harbours cut the risk; warships drive pirates off.").Section("SeaTrade");
        else if (risky != null && worstRisk >= 0.08f)
            scan.Add("sea", 24f, $"Pirates on the {s.SeaRouteName(risky)} route",
                $"<b>Pirates</b>\n{worstRisk:P0} chance a trip is taken. Light the way (lighthouses), build harbours, or send warships.").Section("SeaTrade");
    }

    // ---------- Research ----------

    private static bool canResearch;

    private static void Research(AdvisorScan scan)
    {
        var s = scan.s;
        if (!s.IsFounded || s.State.playTime < 60f) return;
        int jobs = s.State.researchJobs.Count, slots = s.ResearchSlots;
        if (jobs >= slots) return;
        // Is anything startable? (dearer: worked out every fourth scan)
        if (scan.pass % 4 == 0)
        {
            canResearch = false;
            int age = s.State.ageIndex;
            foreach (var tech in s.Database.technologies)
            {
                if (tech == null || (tech.age != null && tech.age.order > age) || s.IsFullyResearched(tech)) continue;
                if (s.CanResearch(tech, out _)) { canResearch = true; break; }
            }
        }
        if (!canResearch) return;
        var current = s.CurrentAge;
        var issue = scan.Add("research", jobs == 0 ? 38f : 26f, jobs == 0 ? "Nothing being researched" : $"Research slot idle ({jobs}/{slots})",
            $"<b>Research</b>\n{jobs} of {slots} slots busy, and there's research you can afford. Every slot works at once.");
        if (current != null) issue.Section(current.name);
    }

    // ---------- Your other worlds (read cheaply from their saved state) ----------

    private static void OtherWorlds(AdvisorScan scan)
    {
        var s = scan.s;
        var worlds = s.State.worlds;
        if (worlds.Count < 2) return;
        float now = s.State.playTime;
        float eatPer = s.Database.foodPerPersonPerSecond;
        for (int i = 0; i < worlds.Count; i++)
        {
            if (i == s.State.currentWorld) continue;
            var w = worlds[i];
            if (w.population < 1f || !s.IsWorldFounded(w)) continue;
            float stock = s.AdvisorFoodOf(w), rate = FoodRate(i);
            if (stock < 1f)
            {
                scan.Add("worlds", 74f, $"{w.name} is starving",
                    $"<b>{w.name}</b>\n{w.population:0} people and no food in store. Show me: go to {w.name}.").World(i);
                continue;
            }
            if (rate < -0.005f && stock / -rate < 240f)
            {
                scan.Add("worlds", 58f, $"Food on {w.name} runs out in {Minutes(stock / -rate)}",
                    $"<b>{w.name}</b>\n{stock:0} food, {rate * 60f:0.#} a minute ({w.population:0} people eat about {w.population * eatPer * 60f:0.#}).").World(i);
                continue;
            }
            var c = s.CivOf(w);
            if (c.revoltAt > now)
            {
                scan.Add("worlds", 70f, $"Rebels gather on {w.name}", $"<b>{w.name}</b>\nThey rise in {Minutes(c.revoltAt - now)} unless the mood improves or you pardon them.").World(i);
                continue;
            }
            float m = s.MoodOf(w);
            if (m < 30f)
                scan.Add("worlds", 40f, $"Unrest on {w.name}", $"<b>{w.name}</b>\nMood {m:0} ({Settlement.MoodName(m)}).").World(i);
        }
    }

    // ---------- Good news (only when nothing is wrong) ----------

    private static void GoodNews(AdvisorScan scan)
    {
        var s = scan.s;
        if (!s.IsFounded || s.State.population < 3f) return;
        if (s.State.population >= 20f && s.PeopleNeedsMet >= 0.98f)
            scan.Good("good", "All needs met: people are content", "<b>All needs met</b>\nEvery class has the goods it needs.");
        else if (s.MoodHere >= 70f)
            scan.Good("good", $"People are {Settlement.MoodName(s.MoodHere).ToLowerInvariant()}", "<b>Good mood</b>\nHappy people work and grow faster.");
        else
            scan.Good("good", "All is well", "<b>All is well</b>\nNothing needs you right now.");
    }
}

/// <summary>
/// The Advisor: a small, quiet card (bottom right, left of the research panel) with the two or three
/// things that most need you right now, each with "Show me". Folds to a one-line pill; remembers that.
/// Any kind of issue can be muted for ten minutes. Checks run every 2.5 seconds, never per frame.
/// </summary>
public class AdvisorPanel : MonoBehaviour
{
    private const int Rows = 3;
    private const float Every = 2.5f, MuteFor = 600f;
    private const string CollapsedKey = "AOS.AdvisorCollapsed";

    private Settlement settlement;
    private FreeModeController controller;
    private Action<Func<string>> showTip;
    private UnityAction hideTip;
    private Action fit;
    private Transform root, researchContent;
    private RectTransform card, pill;
    private TextMeshProUGUI pillLabel, countLabel;
    private readonly RectTransform[] rows = new RectTransform[Rows];
    private readonly TextMeshProUGUI[] rowText = new TextMeshProUGUI[Rows];
    private readonly Image[] rowMark = new Image[Rows];
    private readonly GameObject[] rowShow = new GameObject[Rows], rowMute = new GameObject[Rows];
    private readonly AdvisorIssue[] shown = new AdvisorIssue[Rows];
    private readonly string[] shownTip = new string[Rows];
    private readonly AdvisorScan scan = new AdvisorScan();
    private readonly List<AdvisorIssue> ranked = new List<AdvisorIssue>();
    private readonly List<string> mutedKind = new List<string>();
    private readonly List<float> mutedUntil = new List<float>();
    private readonly bool[] failed = new bool[64];
    private GameObject hintCard;
    private TMP_InputField buildSearch;
    private bool collapsed;
    private int problems;
    private float nextAt;

    private static readonly Comparison<AdvisorIssue> ByUrgency = (a, b) => b.urgency.CompareTo(a.urgency);

    public static AdvisorPanel Create(Transform root, Settlement settlement, FreeModeController controller, Transform researchContent,
        Action<Func<string>> showTip, UnityAction hideTip, Action fitPanels)
    {
        var go = new GameObject("Advisor", typeof(RectTransform));
        go.transform.SetParent(root, false);
        UIBuilder.Stretch((RectTransform)go.transform);
        var p = go.AddComponent<AdvisorPanel>();
        p.settlement = settlement;
        p.controller = controller;
        p.root = root;
        p.researchContent = researchContent;
        p.showTip = showTip;
        p.hideTip = hideTip;
        p.fit = fitPanels;
        p.collapsed = PlayerPrefs.GetInt(CollapsedKey, 0) == 1;
        AdvisorChecks.ResetTracking();
        p.Build((RectTransform)go.transform);
        return p;
    }

    // ---------- Building ----------

    private void Build(RectTransform parent)
    {
        // Bottom right, just left of the research panel and above the messages; the hint card is bottom left.
        var corner = new Vector2(1, 0);
        var spot = new Vector2(-350, 150);
        card = UIBuilder.VerticalPanel("AdvisorCard", parent, 8, 4);
        UIBuilder.Anchor(card, corner, corner, corner, spot, new Vector2(330, 0));
        card.GetComponent<Image>().color = new Color(0.07f, 0.08f, 0.1f, 0.88f);

        var head = UIBuilder.Row("Head", card, 6);
        var title = UIBuilder.Text("Title", head, "<b>Advisor</b>", 15);
        title.color = new Color(0.85f, 0.88f, 0.95f);
        title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 0;
        countLabel = UIBuilder.Text("Count", head, "", 13);
        countLabel.color = new Color(1, 1, 1, 0.55f);
        countLabel.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        var fold = UIBuilder.Button("Fold", head, () => SetCollapsed(true), out var foldLabel, 22, 15);
        fold.GetComponent<LayoutElement>().preferredWidth = 26;
        foldLabel.text = "-"; foldLabel.alignment = TextAlignmentOptions.Center;
        Hover(fold.gameObject, () => "<b>Advisor</b>\nFold it to a small pill. It keeps watching.");
        Hover(title.gameObject, () => "<b>Advisor</b>\nThe few things that most need you right now, most urgent first, on every world. " +
            "<b>Show</b> takes you there; <b>x</b> quiets that kind of issue for ten minutes.");
        title.raycastTarget = true;

        for (int i = 0; i < Rows; i++)
        {
            int index = i;
            var row = UIBuilder.Row("Issue" + i, card, 5);
            row.gameObject.AddComponent<Image>().color = Color.clear;   // so the whole row shows the tooltip
            var mark = UIBuilder.Panel("Mark", row, Color.white);
            mark.raycastTarget = false;
            var markEl = mark.gameObject.AddComponent<LayoutElement>();
            markEl.preferredWidth = markEl.minWidth = 3;
            markEl.preferredHeight = 22;
            rowMark[i] = mark;
            var text = UIBuilder.Text("Text", row, "", 14);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.maxVisibleLines = 2;
            text.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            rowText[i] = text;
            var show = UIBuilder.Button("Show", row, () => ShowMe(index), out var showLabel, 24, 13);
            show.GetComponent<LayoutElement>().preferredWidth = 50;
            show.GetComponent<LayoutElement>().minWidth = 50;   // the sentence beside it never squeezes it
            showLabel.text = "Show"; showLabel.alignment = TextAlignmentOptions.Center;
            showLabel.textWrappingMode = TextWrappingModes.NoWrap;
            rowShow[i] = show.gameObject;
            var mute = UIBuilder.Button("Mute", row, () => Mute(index), out var muteLabel, 24, 13);
            mute.GetComponent<LayoutElement>().preferredWidth = 22;
            muteLabel.text = "<color=#999>x</color>"; muteLabel.alignment = TextAlignmentOptions.Center;
            muteLabel.rectTransform.offsetMin = muteLabel.rectTransform.offsetMax = Vector2.zero;
            rowMute[i] = mute.gameObject;
            Hover(row.gameObject, () => shownTip[index]);
            rows[i] = row;
            row.gameObject.SetActive(false);
        }

        var pillButton = UIBuilder.Button("AdvisorPill", parent, () => SetCollapsed(false), out pillLabel, 28, 14);
        pill = (RectTransform)pillButton.transform;
        UIBuilder.Anchor(pill, corner, corner, corner, spot, new Vector2(170, 28));
        pillButton.image.color = new Color(0.1f, 0.11f, 0.14f, 0.85f);
        pillLabel.alignment = TextAlignmentOptions.Center;
        Hover(pill.gameObject, PillTip);

        card.gameObject.SetActive(false);
        pill.gameObject.SetActive(false);
    }

    private void Hover(GameObject go, Func<string> tip)
    {
        if (showTip == null) return;
        UIBuilder.OnHover(go, () => showTip(tip), hideTip);
    }

    private string pillTip = "";
    private readonly System.Text.StringBuilder tipBuilder = new System.Text.StringBuilder();

    private string PillTip() => pillTip;

    private void BuildPillTip()
    {
        var sb = tipBuilder;
        sb.Clear().Append("<b>Advisor</b>\n");
        int n = 0;
        foreach (var i in ranked)
        {
            if (i.good || n >= Rows) continue;
            sb.Append("· ").Append(i.text).Append('\n');
            n++;
        }
        if (n == 0) sb.Append("Nothing needs you right now.\n");
        sb.Append("<color=#aaa>Click to open.</color>");
        pillTip = sb.ToString();
    }

    // ---------- Updating ----------

    private void Update()
    {
        if (settlement == null || settlement.State == null || settlement.State.worlds.Count == 0) return;
        if (Time.unscaledTime < nextAt) return;
        nextAt = Time.unscaledTime + Every;
        Evaluate();
        Refresh();
    }

    private void Evaluate()
    {
        scan.s = settlement;
        scan.controller = controller;
        scan.count = 0;
        scan.pass++;
        var checks = AdvisorChecks.All;
        for (int c = 0; c < checks.Count; c++)
        {
            try { checks[c](scan); }
            catch (Exception e)
            {
                // one broken check never takes the card down; say so once
                if (c < failed.Length && !failed[c]) { failed[c] = true; Debug.LogException(e); }
            }
        }
        ranked.Clear();
        problems = 0;
        float now = Time.unscaledTime;
        for (int i = 0; i < scan.count; i++)
        {
            var issue = scan.pool[i];
            if (Muted(issue.kind, now)) continue;
            ranked.Add(issue);
            if (!issue.good) problems++;
        }
        ranked.Sort(ByUrgency);
    }

    private bool Muted(string kind, float now)
    {
        for (int i = mutedKind.Count - 1; i >= 0; i--)
        {
            if (mutedUntil[i] <= now) { mutedKind.RemoveAt(i); mutedUntil.RemoveAt(i); continue; }
            if (mutedKind[i] == kind) return true;
        }
        return false;
    }

    private void Refresh()
    {
        bool founded = settlement.IsFounded;
        bool underground = controller != null && controller.Viewing != null;
        if (hintCard == null && root != null)
        {
            var g = root.Find("GuideHints");
            var h = g != null ? g.Find("HintCard") : null;
            if (h != null) hintCard = h.gameObject;
        }
        bool hint = hintCard != null && hintCard.activeSelf;   // a beginner hint is up: stay folded so one card talks at a time
        if (!founded || underground)
        {
            card.gameObject.SetActive(false);
            pill.gameObject.SetActive(false);
            return;
        }
        bool open = !collapsed && !hint;
        card.gameObject.SetActive(open);
        pill.gameObject.SetActive(!open);

        float top = 0f;
        foreach (var i in ranked) if (!i.good) { top = Mathf.Max(top, i.urgency); }
        pillLabel.text = problems == 0
            ? "Advisor: <color=#9e9>all well</color>"
            : $"Advisor: <color=#{UrgencyHex(top)}>{problems} issue{(problems == 1 ? "" : "s")}</color>";
        if (!open) BuildPillTip();
        countLabel.text = problems == 0 ? "" : problems > Rows ? $"{Rows} of {problems}" : "";

        int shownCount = 0;
        for (int r = 0; r < Rows; r++) shown[r] = null;
        foreach (var issue in ranked)
        {
            if (shownCount >= Rows) break;
            if (issue.good && (problems > 0 || shownCount > 0)) continue;   // good news only when all is well, one line
            shown[shownCount++] = issue;
        }
        for (int r = 0; r < Rows; r++)
        {
            var issue = shown[r];
            rows[r].gameObject.SetActive(issue != null);
            if (issue == null) { shownTip[r] = ""; continue; }
            rowText[r].text = issue.good ? $"<color=#9e9>{issue.text}</color>" : issue.text;
            rowMark[r].color = issue.good ? new Color(0.45f, 0.8f, 0.45f, 0.9f) : UrgencyColour(issue.urgency);
            rowShow[r].SetActive(issue.go != AdvisorIssue.Go.None);
            rowMute[r].SetActive(!issue.good);
            shownTip[r] = issue.tip ?? issue.text ?? "";
        }
    }

    private static Color UrgencyColour(float u) =>
        u >= 80f ? new Color(0.95f, 0.38f, 0.32f) : u >= 50f ? new Color(0.98f, 0.72f, 0.3f) : new Color(0.62f, 0.7f, 0.8f);

    private static string UrgencyHex(float u) => u >= 80f ? "f87" : u >= 50f ? "fc6" : "bcd";

    private void SetCollapsed(bool fold)
    {
        collapsed = fold;
        PlayerPrefs.SetInt(CollapsedKey, fold ? 1 : 0);
        hideTip?.Invoke();
        GameAudio.UI(fold ? "panel_close" : "panel_open");
        nextAt = 0f;   // redraw now
    }

    private void Mute(int row)
    {
        var issue = shown[row];
        if (issue == null || issue.kind == null) return;
        mutedKind.Add(issue.kind);
        mutedUntil.Add(Time.unscaledTime + MuteFor);
        hideTip?.Invoke();
        if (controller != null) controller.ShowMessage($"Advisor: quiet about that for 10 minutes.");
        Evaluate();
        Refresh();
    }

    // ---------- Show me ----------

    private void ShowMe(int row)
    {
        var issue = shown[row];
        if (issue == null) return;
        switch (issue.go)
        {
            case AdvisorIssue.Go.Pan:
                if (controller != null) controller.PanTo(issue.at);
                break;
            case AdvisorIssue.Go.PanSelect:
                if (controller == null) break;
                controller.PanTo(issue.at);
                var view = controller.BuildingAt(issue.cell);
                if (view != null) controller.Select(view);
                break;
            case AdvisorIssue.Go.Build:
                FilterBuild(issue.arg);
                break;
            case AdvisorIssue.Go.Section:
                OpenSection(issue.arg);
                break;
            case AdvisorIssue.Go.Culture:
                if (CulturePanel.Instance != null && !CulturePanel.Instance.IsOpen) CulturePanel.Toggle();
                break;
            case AdvisorIssue.Go.People:
                if (PeoplePanel.Instance != null && !PeoplePanel.Instance.IsOpen) PeoplePanel.Toggle();
                break;
            case AdvisorIssue.Go.Civics:
                if (CivicsPanel.Instance != null) CivicsPanel.Instance.Open(CivicsPanel.Tab.Overview);
                break;
            case AdvisorIssue.Go.CivicsTaxes:
                if (CivicsPanel.Instance != null) CivicsPanel.Instance.Open(CivicsPanel.Tab.Taxes);
                break;
            case AdvisorIssue.Go.World:
                if (issue.world >= 0 && issue.world < settlement.State.worlds.Count && issue.world != settlement.State.currentWorld)
                    settlement.SwitchWorld(issue.world);
                break;
            case AdvisorIssue.Go.Custom:
                issue.custom?.Invoke();
                break;
        }
        nextAt = Time.unscaledTime + 0.5f;   // look again soon after acting
    }

    /// <summary>Type into the Build menu's search, so it lists what fixes the problem.</summary>
    private void FilterBuild(string search)
    {
        if (string.IsNullOrEmpty(search)) return;
        if (buildSearch == null && root != null)
        {
            var panel = root.Find("BuildPanel");
            var field = panel != null ? panel.Find("Search") : null;
            if (field != null) buildSearch = field.GetComponent<TMP_InputField>();
        }
        if (buildSearch == null) return;
        buildSearch.text = search;   // runs the menu's own filter
        fit?.Invoke();
    }

    /// <summary>Unfold a section of the research panel (Utilities, Trade, an age...) and scroll to it.</summary>
    private void OpenSection(string name)
    {
        if (researchContent == null || string.IsNullOrEmpty(name)) return;
        var section = researchContent.Find(name) as RectTransform;
        if (section == null || !section.gameObject.activeInHierarchy) return;
        var body = section.Find("Body");
        var header = section.Find("Header");
        if (body != null && !body.gameObject.activeSelf && header != null)
        {
            var button = header.GetComponent<Button>();
            if (button != null) button.onClick.Invoke();   // as if clicked: the panels refit themselves
        }
        else fit?.Invoke();
        Canvas.ForceUpdateCanvases();
        var content = (RectTransform)researchContent;
        var scroll = content.GetComponentInParent<ScrollRect>();
        if (scroll == null || scroll.viewport == null) return;
        float y = -section.anchoredPosition.y - section.rect.height * (1f - section.pivot.y);
        float max = Mathf.Max(0f, content.rect.height - scroll.viewport.rect.height);
        content.anchoredPosition = new Vector2(content.anchoredPosition.x, Mathf.Clamp(y, 0f, max));
    }
}
