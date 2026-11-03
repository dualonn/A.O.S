using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>One line of the Chronicle: something worth remembering, and when.</summary>
[Serializable]
public class XChronicleEntry
{
    [Tooltip("Play time when it happened (-1: before the Chronicle was kept).")]
    public float at;
    public string text;
    [Tooltip("What kind of moment it was (XChronicleKind), for its colour.")]
    public int kind;
    [Tooltip("The world it happened on.")]
    public string world;
}

/// <summary>An age reached, and when.</summary>
[Serializable]
public class XAgeStamp
{
    public int age;
    public float at;
    public string world;
}

/// <summary>The kinds of Chronicle entry (their colours).</summary>
public enum XChronicleKind { Founding, Age, War, Hero, Discovery, Wonder, Space, Achievement, Trade, Disaster }

/// <summary>
/// The civilisation's records: statistics, achievements and the Chronicle, plus the market.
/// Saved with the game (missing in old saves, so everything has a safe default).
/// </summary>
[Serializable]
public class XRecords
{
    [Tooltip("Set the first time the records are kept (old saves start them from what they already have).")]
    public bool started;
    public float startedAt;

    // ----- Statistics (counted where things happen) -----
    public int raidsHeld, raidsBroken, raidsOverrun;
    public int enemiesSlain, troopsLost, heroesFallen;
    public int buildingsBuilt;
    public float gathered;
    public List<ResourceStock> gatheredBy = new List<ResourceStock>();
    public List<XAgeStamp> ages = new List<XAgeStamp>();
    public int bestPopulation;
    public int marketTrades;
    public float marketCoinEarned, marketCoinSpent;
    public bool soldInBoom;

    // ----- Achievements -----
    public List<string> achieved = new List<string>();
    public List<float> achievedAt = new List<float>();
    [Tooltip("Achievements earned since the panel was last opened.")]
    public int unseen;

    // ----- The Chronicle -----
    public List<XChronicleEntry> chronicle = new List<XChronicleEntry>();

    // ----- What the Chronicle has already noticed (counters looked at now and then) -----
    public int seenAge = -1, seenFounded, seenHeroes, seenChiefs, seenCamps, seenSites, seenGoals;
    public int seenScanned, seenAnomalies, seenRoutes, seenMines, seenWormholes, seenLore, seenItems;
    public int seenPopMilestone, seenKillMilestone, seenBuildMilestone, seenRaidMilestone, seenHeroLevel;
    public List<string> seenWonders = new List<string>();
    public List<string> seenNatural = new List<string>();
    public List<string> seenTreaties = new List<string>();

    // ----- The market -----
    public XMarketState market = new XMarketState();
}

/// <summary>
/// Statistics, the Chronicle and achievements. Everything is counted cheaply: a few hooks where
/// things happen (kills, raids, buildings, production), and counters looked at every couple of seconds.
/// </summary>
public partial class Settlement
{
    public const int XChronicleCap = 400;

    /// <summary>The records (made on first use, so old saves load as they were).</summary>
    public XRecords XRec
    {
        get
        {
            if (State.xrecords == null) State.xrecords = new XRecords();
            return State.xrecords;
        }
    }

    /// <summary>Raised when an achievement is earned (for the panel's badge).</summary>
    public event Action<XAchievement> XAchievementEarned;

    private float xRecordsTimer;

    // ---------- Hooks (called where things happen) ----------

    /// <summary>An enemy died (a beguiled or allied one doesn't count).</summary>
    public void XRecordKill(Enemy enemy)
    {
        if (State == null || enemy == null || enemy.Charmed) return;
        XRec.enemiesSlain++;
    }

    /// <summary>One of your troops died.</summary>
    public void XRecordTroopLost(bool hero)
    {
        if (State == null) return;
        if (hero) XRec.heroesFallen++;
        else XRec.troopsLost++;
    }

    /// <summary>A building was finished.</summary>
    private void XRecordBuilt(BuildingDefinition def)
    {
        if (def != null && def.IsWall) return;   // walls go up in dozens: they don't count
        XRec.buildingsBuilt++;
    }

    // Where each resource's tally is in the list (worked out once; the list only grows).
    private readonly Dictionary<string, ResourceStock> xGatheredIndex = new Dictionary<string, ResourceStock>();
    private XRecords xGatheredFor;

    /// <summary>Production made something (on any world).</summary>
    private void XRecordGathered(ResourceType r, float amount)
    {
        if (r == null || amount <= 0f) return;
        var rec = XRec;
        if (xGatheredFor != rec)
        {
            xGatheredFor = rec;
            xGatheredIndex.Clear();
            foreach (var s in rec.gatheredBy) if (s != null && s.id != null) xGatheredIndex[s.id] = s;
        }
        if (!xGatheredIndex.TryGetValue(r.id, out var stock))
        {
            stock = new ResourceStock { id = r.id };
            rec.gatheredBy.Add(stock);
            xGatheredIndex[r.id] = stock;
        }
        stock.amount += amount;
        rec.gathered += amount;
    }

    /// <summary>A raid is over: held (nothing lost), or it broke through (severity 0..1).</summary>
    public void XRecordRaid(bool brokeThrough, float severity)
    {
        if (State == null) return;
        var rec = XRec;
        if (!brokeThrough) rec.raidsHeld++;
        else rec.raidsBroken++;
        if (severity >= 0.5f)
        {
            rec.raidsOverrun++;
            XChronicle(XChronicleKind.Disaster, "Raiders overran the defences and left the town burning.");
        }
    }

    // ---------- The Chronicle ----------

    /// <summary>Write a line in the Chronicle (on the world being ticked).</summary>
    public void XChronicle(XChronicleKind kind, string text)
    {
        if (State == null || string.IsNullOrEmpty(text)) return;
        var list = XRec.chronicle;
        list.Add(new XChronicleEntry { at = State.playTime, text = text, kind = (int)kind, world = CurrentWorld.name });
        if (list.Count > XChronicleCap) list.RemoveRange(1, list.Count - XChronicleCap);   // (the founding line stays)
    }

    // ---------- Looked at every couple of seconds ----------

    private void XTickRecords(float dt)
    {
        if (offWorld) return;
        XTickMarket(dt);
        xRecordsTimer -= dt;
        if (xRecordsTimer > 0f) return;
        xRecordsTimer = 2f;
        var rec = XRec;
        if (!rec.started) XStartRecords(rec);
        XNotice(rec);
        XCheckAchievements(rec);
    }

    /// <summary>The first look: an old save starts its records from what it already has, quietly.</summary>
    private void XStartRecords(XRecords rec)
    {
        rec.started = true;
        rec.startedAt = State.playTime;
        bool fresh = State.playTime < 600f && State.ageIndex == 0;
        int held = 0;
        foreach (var w in State.worlds) held += w.wavesSurvived;
        if (rec.raidsHeld + rec.raidsBroken == 0) rec.raidsHeld = held;
        for (int a = 0; a <= State.ageIndex; a++)
            if (!rec.ages.Exists(x => x.age == a)) rec.ages.Add(new XAgeStamp { age = a, at = fresh && a == 0 ? 0f : -1f, world = State.worlds.Count > 0 ? State.worlds[0].name : "" });
        rec.chronicle.Insert(0, new XChronicleEntry
        {
            at = fresh ? 0f : -1f, kind = (int)XChronicleKind.Founding, world = State.worlds.Count > 0 ? State.worlds[0].name : "",
            text = fresh ? "A small band of people gather round a fire. Here begins the story of your people."
                         : $"The scribes begin the Chronicle. Your people already live in the {CurrentAge?.displayName ?? "first age"}.",
        });
        // Everything already done counts as seen, so the Chronicle doesn't fill with old news.
        XNotice(rec, quiet: true);
        rec.seenPopMilestone = XMilestoneIndex(PopMilestones, XTotalPopulation());
        rec.seenKillMilestone = XMilestoneIndex(KillMilestones, rec.enemiesSlain);
        rec.seenBuildMilestone = XMilestoneIndex(BuildMilestones, rec.buildingsBuilt);
        rec.seenRaidMilestone = XMilestoneIndex(RaidMilestones, rec.raidsHeld);
        // Achievements already earned are given quietly (no rewards for the past).
        foreach (var a in XAchievements.All(this))
            if (!rec.achieved.Contains(a.id) && a.check(this)) { rec.achieved.Add(a.id); rec.achievedAt.Add(-1f); }
    }

    private static readonly int[] PopMilestones = { 10, 25, 50, 100, 250, 500, 1000, 2500, 5000, 10000, 25000, 50000, 100000 };
    private static readonly int[] KillMilestones = { 1, 100, 500, 1000, 2500, 5000, 10000, 25000, 50000, 100000 };
    private static readonly int[] BuildMilestones = { 10, 50, 100, 250, 500, 1000, 2500, 5000, 10000 };
    private static readonly int[] RaidMilestones = { 1, 10, 25, 50, 100, 250, 500, 1000 };

    /// <summary>How many milestones 'value' has passed.</summary>
    private static int XMilestoneIndex(int[] steps, float value)
    {
        int n = 0;
        while (n < steps.Length && value >= steps[n]) n++;
        return n;
    }

    public int XTotalPopulation()
    {
        float n = 0;
        foreach (var w in State.worlds) n += w.population;
        return Mathf.FloorToInt(n);
    }

    public int XFoundedWorlds()
    {
        int n = 0;
        foreach (var w in State.worlds) if (IsWorldFounded(w)) n++;
        return n;
    }

    public int XTotalExplored()
    {
        int n = 0;
        foreach (var w in State.worlds) n += w.explored != null ? w.explored.Count : 0;
        return n;
    }

    public int XNaturalWondersFound()
    {
        int n = 0;
        foreach (var w in State.worlds)
            foreach (var s in w.sites) if (s.kind == SiteKind.NaturalWonder && s.done) n++;
        return n;
    }

    /// <summary>Great Wonders standing (their power is in force).</summary>
    public int XGreatWondersStanding()
    {
        int n = 0;
        foreach (var def in Database.buildings)
            if (def != null && def.wonder && def.wonderEffect != null && State.researched.Contains(def.wonderEffect.id)) n++;
        return n;
    }

    public int XDistinctResearch()
    {
        var set = new HashSet<string>(State.researched);
        return set.Count;
    }

    public int XBiomesVisitedAnywhere()
    {
        var set = new HashSet<int>();
        foreach (var w in State.worlds) if (w.biomesVisited != null) foreach (var b in w.biomesVisited) set.Add(b);
        return set.Count;
    }

    public int XBestHeroLevel()
    {
        int best = 0;
        foreach (var h in State.heroes) best = Mathf.Max(best, h.level + 1);
        return best;
    }

    /// <summary>Compare the counters with what the Chronicle last saw, and write down what changed.</summary>
    private void XNotice(XRecords rec, bool quiet = false)
    {
        void Say(XChronicleKind kind, string text) { if (!quiet) XChronicle(kind, text); }

        // Ages
        if (State.ageIndex != rec.seenAge)
        {
            if (rec.seenAge >= 0 && State.ageIndex > rec.seenAge)
            {
                for (int a = rec.seenAge + 1; a <= State.ageIndex; a++)
                {
                    if (!rec.ages.Exists(x => x.age == a)) rec.ages.Add(new XAgeStamp { age = a, at = State.playTime, world = CurrentWorld.name });
                    Say(XChronicleKind.Age, $"Your people entered the <color=#fd6>{Database.GetAge(a)?.displayName ?? "next age"}</color>.");
                }
            }
            rec.seenAge = State.ageIndex;
        }
        // Worlds founded
        int founded = XFoundedWorlds();
        if (founded > rec.seenFounded)
        {
            if (rec.seenFounded == 0) Say(XChronicleKind.Founding, "The settlement is founded. The first fire is lit for good.");
            else if (founded > 1)
            {
                var newest = State.worlds.FindLast(w => IsWorldFounded(w));
                Say(XChronicleKind.Space, $"A colony is founded on <color=#9cf>{newest?.name}</color>. Your people now live on {founded} worlds.");
            }
            rec.seenFounded = founded;
        }
        // Heroes
        if (State.heroes.Count > rec.seenHeroes)
        {
            for (int i = rec.seenHeroes; i < State.heroes.Count; i++)
            {
                var h = State.heroes[i];
                Say(XChronicleKind.Hero, $"<color=#ffd98a>{h.name}</color> rose to be a hero of your people.");
            }
            rec.seenHeroes = State.heroes.Count;
        }
        rec.seenHeroes = Mathf.Min(rec.seenHeroes, State.heroes.Count);
        int bestLevel = XBestHeroLevel();
        if (bestLevel > rec.seenHeroLevel)
        {
            if (bestLevel >= 10 && bestLevel % 5 == 0 && rec.seenHeroLevel < bestLevel)
            {
                var h = State.heroes.Find(x => x.level + 1 == bestLevel);
                Say(XChronicleKind.Hero, $"{h?.name ?? "A hero"} reached level {bestLevel}: songs are sung of them.");
            }
            rec.seenHeroLevel = bestLevel;
        }
        // Fighting
        if (State.chiefsKilled > rec.seenChiefs)
        {
            int n = State.chiefsKilled - rec.seenChiefs;
            Say(XChronicleKind.War, n == 1 ? "A raider chief was slain." : $"{n} raider chiefs were slain.");
            rec.seenChiefs = State.chiefsKilled;
        }
        if (State.campsCleared > rec.seenCamps)
        {
            if (rec.seenCamps == 0) Say(XChronicleKind.War, "Your troops burnt their first raider camp.");
            else if (State.campsCleared / 10 > rec.seenCamps / 10) Say(XChronicleKind.War, $"{State.campsCleared / 10 * 10} raider camps burnt.");
            rec.seenCamps = State.campsCleared;
        }
        int killStep = XMilestoneIndex(KillMilestones, rec.enemiesSlain);
        if (killStep > rec.seenKillMilestone)
        {
            int v = KillMilestones[killStep - 1];
            Say(XChronicleKind.War, v == 1 ? "First blood: an enemy fell to your people." : $"{v:N0} enemies have fallen to your people.");
            rec.seenKillMilestone = killStep;
        }
        int raidStep = XMilestoneIndex(RaidMilestones, rec.raidsHeld);
        if (raidStep > rec.seenRaidMilestone)
        {
            int v = RaidMilestones[raidStep - 1];
            Say(XChronicleKind.War, v == 1 ? "The first raid was driven off." : $"{v:N0} raids driven off.");
            rec.seenRaidMilestone = raidStep;
        }
        // Growth
        int pop = XTotalPopulation();
        rec.bestPopulation = Mathf.Max(rec.bestPopulation, pop);
        int popStep = XMilestoneIndex(PopMilestones, pop);
        if (popStep > rec.seenPopMilestone)
        {
            Say(XChronicleKind.Founding, $"Your people number {PopMilestones[popStep - 1]:N0}.");
            rec.seenPopMilestone = popStep;
        }
        int buildStep = XMilestoneIndex(BuildMilestones, rec.buildingsBuilt);
        if (buildStep > rec.seenBuildMilestone)
        {
            Say(XChronicleKind.Founding, $"{BuildMilestones[buildStep - 1]:N0} buildings raised.");
            rec.seenBuildMilestone = buildStep;
        }
        // Exploring
        if (State.sitesDone > rec.seenSites)
        {
            if (rec.seenSites == 0) Say(XChronicleKind.Discovery, "Your explorers made their first find in the wild.");
            else if (State.sitesDone / 10 > rec.seenSites / 10) Say(XChronicleKind.Discovery, $"{State.sitesDone / 10 * 10} wild places explored.");
            rec.seenSites = State.sitesDone;
        }
        if (State.journal.Count > rec.seenLore)
        {
            for (int i = rec.seenLore; i < State.journal.Count; i++)
            {
                int id = State.journal[i];
                if (id >= 0 && id < Lore.Entries.Length) Say(XChronicleKind.Discovery, $"The scholars wrote down an old tale: <i>{Lore.Entries[id].title}</i>.");
            }
            rec.seenLore = State.journal.Count;
        }
        foreach (var w in State.worlds)
            foreach (var s in w.sites)
            {
                if (s.kind != SiteKind.NaturalWonder || !s.done) continue;
                string key = $"{w.seed}:{s.variant}";
                if (rec.seenNatural.Contains(key)) continue;
                rec.seenNatural.Add(key);
                Say(XChronicleKind.Wonder, $"Your people beheld <color=#8fe>{NaturalWonderOf(s).name}</color> on {w.name}.");
            }
        foreach (var def in Database.buildings)
        {
            if (def == null || !def.wonder || def.wonderEffect == null) continue;
            if (!State.researched.Contains(def.wonderEffect.id) || rec.seenWonders.Contains(def.id)) continue;
            rec.seenWonders.Add(def.id);
            Say(XChronicleKind.Wonder, $"The <color=#ffd56a>{def.displayName}</color> was raised, a wonder of the age.");
        }
        int items = Mathf.Max(0, State.nextItemId - 1);
        if (items / 25 > rec.seenItems / 25 && items > 0) Say(XChronicleKind.Hero, $"{items / 25 * 25} treasures have passed through your vaults.");
        rec.seenItems = items;
        if (State.goalsDone / 10 > rec.seenGoals / 10) Say(XChronicleKind.Founding, $"{State.goalsDone / 10 * 10} great goals achieved.");
        rec.seenGoals = State.goalsDone;
        // Diplomacy
        foreach (var w in State.worlds)
        {
            var d = w.diplomacy;
            if (d == null) continue;
            string natives = WorldProfile.Get(w.profile).natives;
            if (string.IsNullOrEmpty(natives)) continue;
            string peaceKey = $"{w.seed}:peace", allyKey = $"{w.seed}:ally";
            if (d.peace && !rec.seenTreaties.Contains(peaceKey)) { rec.seenTreaties.Add(peaceKey); Say(XChronicleKind.Space, $"Peace was made with the {natives} of {w.name}."); }
            if (d.alliance && !rec.seenTreaties.Contains(allyKey)) { rec.seenTreaties.Add(allyKey); Say(XChronicleKind.Space, $"The {natives} of {w.name} swore an alliance with you."); }
        }
        // Space
        if (State.scannedSystems.Count > rec.seenScanned)
        {
            if (rec.seenScanned == 0) Say(XChronicleKind.Space, "Your first probe charted another star.");
            else if (State.scannedSystems.Count / 10 > rec.seenScanned / 10) Say(XChronicleKind.Space, $"{State.scannedSystems.Count / 10 * 10} star systems charted.");
            rec.seenScanned = State.scannedSystems.Count;
        }
        if (State.anomaliesDone.Count > rec.seenAnomalies)
        {
            Say(XChronicleKind.Space, rec.seenAnomalies == 0 ? "Your people met their first anomaly among the stars." : "Another mystery of the void was faced.");
            rec.seenAnomalies = State.anomaliesDone.Count;
        }
        if (State.wormholes.Count > rec.seenWormholes)
        {
            Say(XChronicleKind.Space, "A wormhole was charted: two far stars are now one step apart.");
            rec.seenWormholes = State.wormholes.Count;
        }
        if (State.routes.Count > rec.seenRoutes)
        {
            if (rec.seenRoutes == 0) Say(XChronicleKind.Trade, "The first cargo route between worlds opened.");
            rec.seenRoutes = State.routes.Count;
        }
        rec.seenRoutes = Mathf.Min(rec.seenRoutes, State.routes.Count);
        if (State.orbitalMines.Count > rec.seenMines)
        {
            if (rec.seenMines == 0) Say(XChronicleKind.Space, "The first mine opened in orbit.");
            rec.seenMines = State.orbitalMines.Count;
        }
    }

    // ---------- Achievements ----------

    private void XCheckAchievements(XRecords rec)
    {
        foreach (var a in XAchievements.All(this))
        {
            if (rec.achieved.Contains(a.id)) continue;
            bool done;
            try { done = a.check(this); }
            catch (Exception) { done = false; }
            if (!done) continue;
            rec.achieved.Add(a.id);
            rec.achievedAt.Add(State.playTime);
            rec.unseen++;
            string reward = XGiveReward(a);
            Notify($"<color=#ffd56a><b>Achievement: {a.title}!</b></color> {a.description}{reward}");
            XChronicle(XChronicleKind.Achievement, $"Achievement earned: <color=#ffd56a>{a.title}</color>.");
            GameAudio.UI("achievement");
            XAchievementEarned?.Invoke(a);
            return;   // one at a time, so the messages don't pile up
        }
    }

    /// <summary>An achievement's small reward (coin, or food before coin), on the world you're on.</summary>
    private string XGiveReward(XAchievement a)
    {
        if (a.reward <= 0) return "";
        var coin = Database.coin;
        bool hasCoin = coin != null && (coin.unlockedIn == null || coin.unlockedIn.order <= State.ageIndex);
        var r = hasCoin ? coin : Database.food;
        if (r == null) return "";
        float amount = Mathf.Round(a.reward * (hasCoin ? 40f : 60f) * (1f + State.ageIndex) / 10f) * 10f;
        AddResource(r, amount);
        return $" <color=#9f9>(+{amount:0} {r.displayName})</color>";
    }

    /// <summary>What your people call their ruler, from how many achievements you have.</summary>
    public string XRulerTitle()
    {
        int n = XRec.achieved.Count;
        bool space = XFoundedWorlds() > 1;
        if (n >= 90) return space ? "Eternal Sovereign of the Stars" : "Eternal Sovereign";
        if (n >= 70) return space ? "Star-Emperor" : "Emperor of Ages";
        if (n >= 50) return "Emperor";
        if (n >= 35) return "High King";
        if (n >= 22) return "King";
        if (n >= 12) return "Lord";
        if (n >= 5) return "Elder";
        return "Chieftain";
    }
}

/// <summary>One achievement: what it asks for, its title, and its small reward (0 = just the title).</summary>
public class XAchievement
{
    public string id, title, description, category;
    public int reward;
    public Func<Settlement, bool> check;
    /// <summary>Progress towards it, as (have, need), when that makes sense.</summary>
    public Func<Settlement, (float have, float need)> progress;
}

/// <summary>The achievements, across every part of the game. Built once per database.</summary>
public static class XAchievements
{
    private static List<XAchievement> all;
    private static GameDatabase builtFor;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { all = null; builtFor = null; }

    public static readonly string[] Categories = { "Settlement", "War", "Heroes", "Exploration", "Ages", "Knowledge", "Space", "Trade", "Time" };

    public static List<XAchievement> All(Settlement s)
    {
        if (all != null && builtFor == s.Database) return all;
        builtFor = s.Database;
        all = new List<XAchievement>();
        void Count(string cat, string id, string title, string desc, int reward, Func<Settlement, float> value, float need)
        {
            all.Add(new XAchievement
            {
                id = id, title = title, description = desc, category = cat, reward = reward,
                check = x => value(x) >= need, progress = x => (Mathf.Min(value(x), need), need),
            });
        }
        void Flag(string cat, string id, string title, string desc, int reward, Func<Settlement, bool> check)
        {
            all.Add(new XAchievement { id = id, title = title, description = desc, category = cat, reward = reward, check = check });
        }

        // Settlement
        Flag("Settlement", "Founded", "First Fire", "Found your settlement.", 1, x => x.XFoundedWorlds() > 0);
        Count("Settlement", "Pop25", "Hamlet", "Have 25 people.", 1, x => x.XTotalPopulation(), 25);
        Count("Settlement", "Pop100", "Township", "Have 100 people.", 2, x => x.XTotalPopulation(), 100);
        Count("Settlement", "Pop300", "City", "Have 300 people.", 2, x => x.XTotalPopulation(), 300);
        Count("Settlement", "Pop1000", "Metropolis", "Have 1,000 people across your worlds.", 3, x => x.XTotalPopulation(), 1000);
        Count("Settlement", "Pop5000", "Teeming Millions", "Have 5,000 people across your worlds.", 3, x => x.XTotalPopulation(), 5000);
        Count("Settlement", "Build25", "Builder", "Finish 25 buildings.", 1, x => x.XRec.buildingsBuilt, 25);
        Count("Settlement", "Build150", "Architect", "Finish 150 buildings.", 2, x => x.XRec.buildingsBuilt, 150);
        Count("Settlement", "Build500", "Master Builder", "Finish 500 buildings.", 3, x => x.XRec.buildingsBuilt, 500);
        Count("Settlement", "Build2000", "Shaper of Lands", "Finish 2,000 buildings.", 3, x => x.XRec.buildingsBuilt, 2000);
        Count("Settlement", "Gather10k", "Gatherer", "Produce 10,000 resources.", 1, x => x.XRec.gathered, 10000);
        Count("Settlement", "Gather100k", "Provider", "Produce 100,000 resources.", 2, x => x.XRec.gathered, 100000);
        Count("Settlement", "Gather1m", "Bountiful", "Produce a million resources.", 3, x => x.XRec.gathered, 1000000);
        Count("Settlement", "Gather10m", "Horn of Plenty", "Produce ten million resources.", 3, x => x.XRec.gathered, 10000000);

        // War
        Count("War", "Kill1", "Blooded", "Slay an enemy.", 0, x => x.XRec.enemiesSlain, 1);
        Count("War", "Kill100", "Warband", "Slay 100 enemies.", 1, x => x.XRec.enemiesSlain, 100);
        Count("War", "Kill1000", "Legion", "Slay 1,000 enemies.", 2, x => x.XRec.enemiesSlain, 1000);
        Count("War", "Kill10000", "Unstoppable", "Slay 10,000 enemies.", 3, x => x.XRec.enemiesSlain, 10000);
        Count("War", "Held1", "Holdfast", "Drive off a raid without losing anything.", 1, x => x.XRec.raidsHeld, 1);
        Count("War", "Held10", "Bulwark", "Drive off 10 raids.", 1, x => x.XRec.raidsHeld, 10);
        Count("War", "Held50", "Impregnable", "Drive off 50 raids.", 2, x => x.XRec.raidsHeld, 50);
        Count("War", "Held200", "Eternal Watch", "Drive off 200 raids.", 3, x => x.XRec.raidsHeld, 200);
        Count("War", "Scarred", "Scarred but Standing", "Live through a raid that broke your defences.", 0, x => x.XRec.raidsBroken, 1);
        Count("War", "Camp1", "Camp Breaker", "Burn a raider camp.", 1, x => x.State.campsCleared, 1);
        Count("War", "Camp25", "Scourge of Raiders", "Burn 25 raider camps.", 2, x => x.State.campsCleared, 25);
        Count("War", "Chief1", "Chief Slayer", "Kill a raider chief.", 1, x => x.State.chiefsKilled, 1);
        Count("War", "Chief10", "Warlord's Bane", "Kill 10 raider chiefs.", 2, x => x.State.chiefsKilled, 10);

        // Heroes
        Count("Heroes", "Hero1", "A Hero Rises", "Promote a hero.", 1, x => x.State.heroes.Count, 1);
        Count("Heroes", "Hero5", "Fellowship", "Have 5 heroes.", 2, x => x.State.heroes.Count, 5);
        Count("Heroes", "Level10", "Champion", "Raise a hero to level 10.", 2, x => x.XBestHeroLevel(), 10);
        Count("Heroes", "Level20", "Legend", "Raise a hero to level 20.", 3, x => x.XBestHeroLevel(), 20);
        Count("Heroes", "Items25", "Treasure Hunter", "Find 25 items.", 1, x => x.State.nextItemId - 1, 25);
        Count("Heroes", "Items100", "Hoard of Relics", "Find 100 items.", 2, x => x.State.nextItemId - 1, 100);

        // Exploration
        Count("Exploration", "Zones10", "Wanderer", "Explore 10 zones.", 1, x => x.XTotalExplored(), 10);
        Count("Exploration", "Zones50", "Pathfinder", "Explore 50 zones.", 1, x => x.XTotalExplored(), 50);
        Count("Exploration", "Zones200", "Cartographer", "Explore 200 zones.", 2, x => x.XTotalExplored(), 200);
        Count("Exploration", "Zones500", "World Walker", "Explore 500 zones.", 3, x => x.XTotalExplored(), 500);
        Count("Exploration", "Sites5", "Ruin Raider", "Explore 5 wild places.", 1, x => x.State.sitesDone, 5);
        Count("Exploration", "Sites25", "Delver", "Explore 25 wild places.", 2, x => x.State.sitesDone, 25);
        Count("Exploration", "Sites100", "Seeker of Secrets", "Explore 100 wild places.", 3, x => x.State.sitesDone, 100);
        Count("Exploration", "Lore5", "Lorekeeper", "Find 5 old tales.", 1, x => x.State.journal.Count, 5);
        Count("Exploration", "Lore20", "Sage", "Find 20 old tales.", 2, x => x.State.journal.Count, 20);
        Count("Exploration", "Biomes5", "Many Lands", "Set foot in 5 kinds of land.", 1, x => x.XBiomesVisitedAnywhere(), 5);
        Count("Exploration", "BiomesAll", "Every Land Under the Sky", "Set foot in every kind of land.", 2, x => x.XBiomesVisitedAnywhere(), Enum.GetValues(typeof(Biome)).Length);
        Count("Exploration", "Natural1", "Awestruck", "Find a natural wonder.", 1, x => x.XNaturalWondersFound(), 1);
        Count("Exploration", "Natural4", "Wonders of the World", "Find 4 natural wonders.", 2, x => x.XNaturalWondersFound(), 4);

        // Ages: one for each age after the first.
        foreach (var age in s.Database.ages)
        {
            if (age == null || age.order <= 0) continue;
            var a = age;
            Flag("Ages", "Age:" + a.name, a.displayName, $"Enter the {a.displayName}.", 1 + a.order / 5, x => x.State.ageIndex >= a.order);
        }
        Count("Ages", "Wonder1", "Wonder Builder", "Raise a Great Wonder.", 2, x => x.XGreatWondersStanding(), 1);
        Count("Ages", "Wonder4", "Glory of the Ages", "Have 4 Great Wonders standing.", 3, x => x.XGreatWondersStanding(), 4);
        Count("Ages", "Wonder7", "Seven Wonders", "Have 7 Great Wonders standing.", 3, x => x.XGreatWondersStanding(), 7);

        // Knowledge
        Count("Knowledge", "Tech25", "Scholar", "Learn 25 things.", 1, x => x.XDistinctResearch(), 25);
        Count("Knowledge", "Tech100", "Polymath", "Learn 100 things.", 2, x => x.XDistinctResearch(), 100);
        Count("Knowledge", "Tech250", "Omniscient", "Learn 250 things.", 3, x => x.XDistinctResearch(), 250);
        Count("Knowledge", "Goals5", "Goal Getter", "Complete 5 goals.", 1, x => x.State.goalsDone, 5);
        Count("Knowledge", "Goals25", "Ambitious", "Complete 25 goals.", 2, x => x.State.goalsDone, 25);
        Count("Knowledge", "Goals100", "Driven", "Complete 100 goals.", 3, x => x.State.goalsDone, 100);

        // Space
        Count("Space", "Scan1", "First Light", "Chart another star system.", 1, x => x.State.scannedSystems.Count, 1);
        Count("Space", "Scan10", "Surveyor", "Chart 10 star systems.", 2, x => x.State.scannedSystems.Count, 10);
        Count("Space", "Scan50", "Star Charter", "Chart 50 star systems.", 3, x => x.State.scannedSystems.Count, 50);
        Count("Space", "Worlds2", "Colonist", "Found a colony on another world.", 2, x => x.XFoundedWorlds(), 2);
        Count("Space", "Worlds4", "Interplanetary", "Live on 4 worlds.", 3, x => x.XFoundedWorlds(), 4);
        Count("Space", "Worlds8", "Galactic Empire", "Live on 8 worlds.", 3, x => x.XFoundedWorlds(), 8);
        Count("Space", "Route1", "Trade Lanes", "Open a cargo route.", 1, x => x.State.routes.Count, 1);
        Count("Space", "Route5", "Merchant Fleet", "Run 5 cargo routes.", 2, x => x.State.routes.Count, 5);
        Count("Space", "Anomaly1", "Anomalous", "Deal with an anomaly.", 1, x => x.State.anomaliesDone.Count, 1);
        Count("Space", "Anomaly10", "Xeno-Scholar", "Deal with 10 anomalies.", 2, x => x.State.anomaliesDone.Count, 10);
        Count("Space", "Wormhole", "Wormhole Rider", "Chart a wormhole.", 2, x => x.State.wormholes.Count, 1);
        Count("Space", "Orbital", "Sky Miner", "Open an orbital mine.", 1, x => x.State.orbitalMines.Count, 1);
        Flag("Space", "Peace", "Peacemaker", "Make peace with the natives of a world.", 1, x => x.State.worlds.Exists(w => w.diplomacy != null && w.diplomacy.peace && !string.IsNullOrEmpty(WorldProfile.Get(w.profile).natives)));
        Flag("Space", "Alliance", "Friends Among the Stars", "Form an alliance with natives.", 2, x => x.State.worlds.Exists(w => w.diplomacy != null && w.diplomacy.alliance));

        // Trade
        Count("Trade", "Trade1", "Haggler", "Trade at the market.", 0, x => x.XRec.marketTrades, 1);
        Count("Trade", "Trade50", "Merchant Prince", "Make 50 trades at the market.", 2, x => x.XRec.marketTrades, 50);
        Count("Trade", "TradeCoin", "Tycoon", "Earn 10,000 coin selling at the market.", 2, x => x.XRec.marketCoinEarned, 10000);
        Flag("Trade", "Boom", "Played the Market", "Sell something while its price is booming.", 1, x => x.XRec.soldInBoom);

        // Time
        Count("Time", "Hour1", "Survivor", "Play for an hour.", 1, x => x.State.playTime / 3600f, 1);
        Count("Time", "Hour10", "Long Reign", "Play for 10 hours.", 2, x => x.State.playTime / 3600f, 10);
        Count("Time", "Hour50", "Eternal Dynasty", "Play for 50 hours.", 3, x => x.State.playTime / 3600f, 50);
        return all;
    }
}
