using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// The Chronicle: a folding section in the research panel (beside the Journal) that opens one
/// window with tabs: the Chronicle of your people, achievements, statistics, all your worlds at a
/// glance, the market, and every message you've had. Also the speed hotkeys ([ ] and P).
/// </summary>
public class XRecordsPanel : MonoBehaviour
{
    public static XRecordsPanel Instance { get; private set; }

    public enum Tab { Chronicle, Achievements, Statistics, Worlds, Market, Messages }

    private Settlement settlement;
    private FreeModeController controller;
    private FreeModeHUD hud;
    private Action<Func<string>> showTip;
    private UnityAction hideTip;
    private Action fit;

    private CollapsibleSection section;
    private RectTransform panel, content;
    private ScrollRect scroll;
    private TextMeshProUGUI title, footer;
    private readonly Dictionary<Tab, (Button button, TextMeshProUGUI label)> tabButtons = new Dictionary<Tab, (Button, TextMeshProUGUI)>();
    private Tab tab;
    private float refreshAt, sectionAt;
    private string shownSignature;
    private int chronicleShown = 150;

    // The message history lives for the whole play session (the scene is rebuilt on a world switch).
    private static readonly List<string> messages = new List<string>();
    private const int MessageCap = 150;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { messages.Clear(); Instance = null; }

    public bool IsOpen => panel != null && panel.gameObject.activeSelf;

    /// <summary>Add the Chronicle section to the research list, and the window it opens.</summary>
    public static XRecordsPanel Create(Transform root, Settlement settlement, FreeModeController controller, FreeModeHUD hud,
        Transform researchContent, Action<Func<string>> showTip, UnityAction hideTip, Action fitPanels)
    {
        var go = new GameObject("Chronicle", typeof(RectTransform));
        go.transform.SetParent(root, false);
        UIBuilder.Stretch((RectTransform)go.transform);
        var p = go.AddComponent<XRecordsPanel>();
        p.settlement = settlement;
        p.controller = controller;
        p.hud = hud;
        p.showTip = showTip;
        p.hideTip = hideTip;
        p.fit = fitPanels;
        p.BuildSection(researchContent);
        p.BuildWindow((RectTransform)go.transform);
        Instance = p;
        if (controller != null)
        {
            controller.MessageShown += p.OnMessage;
            var markers = new GameObject("NaturalWonderMarkers").AddComponent<XWonderMarkers>();
            markers.transform.SetParent(controller.transform, false);
            markers.Init(controller);
        }
        return p;
    }

    private void OnDestroy()
    {
        if (controller != null) controller.MessageShown -= OnMessage;
        if (Instance == this) Instance = null;
    }

    private void OnMessage(string message)
    {
        if (settlement == null || settlement.State == null) return;
        messages.Add($"<color=#888>{Stamp(settlement.State.playTime)}</color>  {message}");
        if (messages.Count > MessageCap) messages.RemoveRange(0, messages.Count - MessageCap);
    }

    // ---------- The section in the research list ----------

    private void BuildSection(Transform parent)
    {
        if (parent == null) return;
        section = new CollapsibleSection("Chronicle", parent, "Chronicle", new Color(0.95f, 0.7f, 0.45f));
        section.SetOpen(false);
        section.Toggled += _ => fit?.Invoke();
        Pair(section.Body, ("Chronicle", Tab.Chronicle, "The history of your people: every age, war, wonder and world."),
                           ("Achievements", Tab.Achievements, "Deeds across every part of the game. Most pay a little coin; together they raise your ruler's title."));
        Pair(section.Body, ("Statistics", Tab.Statistics, "Everything counted: raids, kills, buildings, resources, ages and more."),
                           ("Your worlds", Tab.Worlds, "All your worlds side by side: people, buildings and stock."));
        Pair(section.Body, ("Market", Tab.Market, "Sell goods for coin and buy them back. Prices drift with supply and demand, and boom or bust now and then."),
                           ("Messages", Tab.Messages, "Every message since you started playing this session."));
    }

    private void Pair(Transform parent, params (string label, Tab tab, string help)[] buttons)
    {
        var row = UIBuilder.Row("Row", parent, 5);
        row.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
        foreach (var (text, t, help) in buttons)
        {
            var tt = t;
            var b = UIBuilder.Button(text, row, () => Open(tt), out var label, 32, 15);
            label.text = text;
            label.alignment = TextAlignmentOptions.Center;
            b.GetComponent<LayoutElement>().flexibleWidth = 1;
            string h = help;
            if (showTip != null) UIBuilder.OnHover(b.gameObject, () => showTip(() => $"<b>{text}</b>\n{h}"), hideTip);
        }
    }

    // ---------- The window ----------

    private void BuildWindow(RectTransform root)
    {
        panel = UIBuilder.VerticalPanel("ChronicleWindow", root, 12, 6);
        UIBuilder.Anchor(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(860, 0));
        var head = UIBuilder.Row("Head", panel, 6);
        title = UIBuilder.Text("Title", head, "", 22);
        title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        var close = UIBuilder.Button("Close", head, () => Show(false), out var closeLabel, 30, 16);
        close.GetComponent<LayoutElement>().preferredWidth = 34;
        closeLabel.text = "X"; closeLabel.alignment = TextAlignmentOptions.Center;

        var tabs = UIBuilder.Row("Tabs", panel, 4);
        tabs.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
        foreach (Tab t in Enum.GetValues(typeof(Tab)))
        {
            var tt = t;
            var b = UIBuilder.Button(t.ToString(), tabs, () => { tab = tt; Rebuild(); }, out var label, 30, 15);
            label.text = t == Tab.Worlds ? "Worlds" : t.ToString();
            label.alignment = TextAlignmentOptions.Center;
            b.GetComponent<LayoutElement>().flexibleWidth = 1;
            tabButtons[t] = (b, label);
        }
        content = UIBuilder.ScrollList("Content", panel, out var element, 4);
        element.preferredHeight = 520;
        scroll = element.GetComponent<ScrollRect>();
        footer = UIBuilder.Text("Footer", panel, "", 14);
        footer.textWrappingMode = TextWrappingModes.Normal;
        panel.gameObject.SetActive(false);
    }

    /// <summary>Open the window on a tab.</summary>
    public void Open(Tab t)
    {
        tab = t;
        Show(true);
    }

    private void Show(bool open)
    {
        panel.gameObject.SetActive(open);
        if (!open) { hideTip?.Invoke(); return; }
        transform.SetAsLastSibling();
        var tip = transform.parent != null ? transform.parent.Find("Tooltip") : null;
        if (tip != null) tip.SetAsLastSibling();
        chronicleShown = 150;
        Rebuild();
    }

    private void Update()
    {
        if (settlement == null || settlement.State == null) return;
        Hotkeys();
        if (Time.unscaledTime >= sectionAt)
        {
            sectionAt = Time.unscaledTime + 1f;
            RefreshSection();
        }
        if (!IsOpen) return;
        if (Input.GetKeyDown(KeyCode.Escape)) { Show(false); return; }
        if (Time.unscaledTime < refreshAt) return;
        refreshAt = Time.unscaledTime + (tab == Tab.Market ? 0.5f : 2f);
        if (tab == Tab.Market) { RefreshMarket(); return; }
        if (Signature() != shownSignature) Rebuild(keepScroll: true);
    }

    /// <summary>[ and ] change the game speed, P pauses (not while typing in a search box).</summary>
    private void Hotkeys()
    {
        if (hud == null || UIBuilder.Typing) return;
        if (Input.GetKeyDown(KeyCode.LeftBracket)) hud.XStepSpeed(-1);
        if (Input.GetKeyDown(KeyCode.RightBracket)) hud.XStepSpeed(1);
        if (Input.GetKeyDown(KeyCode.P) || Input.GetKeyDown(KeyCode.Pause)) hud.XTogglePause();
    }

    /// <summary>The speed button's tooltip.</summary>
    public static string SpeedHelp() =>
        "<b>Game speed</b>\nClick to go faster (x1, x2, x3, x5, x10).\n<color=#ccc>Keys: <b>]</b> faster, <b>[</b> slower, <b>P</b> pause.</color>";

    private void RefreshSection()
    {
        if (section == null) return;
        var rec = settlement.XRec;
        int total = XAchievements.All(settlement).Count;
        string fresh = rec.unseen > 0 ? $"  <color=#fd6>{rec.unseen} new</color>" : "";
        section.SetNote($"{rec.achieved.Count}/{total}{fresh}");
    }

    private string Signature()
    {
        var rec = settlement.XRec;
        int coarse = Mathf.FloorToInt(settlement.State.playTime / 5f);
        return tab switch
        {
            Tab.Chronicle => $"{rec.chronicle.Count}:{chronicleShown}",
            Tab.Achievements => $"{rec.achieved.Count}:{coarse}",
            Tab.Messages => $"{messages.Count}",
            _ => $"{coarse}:{settlement.State.worlds.Count}",
        };
    }

    // ---------- Contents ----------

    private void Rebuild(bool keepScroll = false)
    {
        float pos = scroll != null ? scroll.verticalNormalizedPosition : 1f;
        foreach (Transform child in content) Destroy(child.gameObject);
        marketRows.Clear();
        bars.Clear();
        spark = null;
        foreach (var pair in tabButtons)
            pair.Value.button.image.color = pair.Key == tab ? new Color(0.42f, 0.33f, 0.2f) : UIBuilder.ButtonColor;
        footer.text = "";
        shownSignature = Signature();
        switch (tab)
        {
            case Tab.Chronicle: BuildChronicle(); break;
            case Tab.Achievements: BuildAchievements(); break;
            case Tab.Statistics: BuildStatistics(); break;
            case Tab.Worlds: BuildWorlds(); break;
            case Tab.Market: BuildMarket(); break;
            case Tab.Messages: BuildMessages(); break;
        }
        if (scroll == null) return;
        Canvas.ForceUpdateCanvases();
        // Histories open at the latest line; the rest at the top.
        bool bottom = tab == Tab.Chronicle || tab == Tab.Messages;
        scroll.verticalNormalizedPosition = keepScroll ? pos : bottom ? 0f : 1f;
    }

    private TextMeshProUGUI Block(string text, float size = 16)
    {
        var t = UIBuilder.Text("Text", content, text, size);
        t.textWrappingMode = TextWrappingModes.Normal;
        t.richText = true;
        return t;
    }

    /// <summary>"Year 3 · 1h 05m" (a year is a home-world year of four seasons).</summary>
    public static string Stamp(float at)
    {
        if (at < 0f) return "Before the records";
        float year = Settlement.SeasonLength * 4f;
        int y = 1 + Mathf.FloorToInt(at / year);
        int h = Mathf.FloorToInt(at / 3600f), m = Mathf.FloorToInt(at % 3600f / 60f);
        return $"Year {y} · {h}h {m:00}m";
    }

    private static string KindColour(XChronicleKind k) => k switch
    {
        XChronicleKind.Age => "#ffd56a",
        XChronicleKind.War => "#ff9a80",
        XChronicleKind.Hero => "#ffd98a",
        XChronicleKind.Discovery => "#c9e7a0",
        XChronicleKind.Wonder => "#8fe8ff",
        XChronicleKind.Space => "#9cc8ff",
        XChronicleKind.Achievement => "#ffe08a",
        XChronicleKind.Trade => "#b8f0b0",
        XChronicleKind.Disaster => "#ff7070",
        _ => "#e8e0c8",
    };

    private void BuildChronicle()
    {
        var rec = settlement.XRec;
        title.text = $"<b>The Chronicle of your people</b>  <size=70%><color=#ccc>{settlement.XRulerTitle()} · {settlement.CurrentAge?.displayName}</color></size>";
        var list = rec.chronicle;
        int from = Mathf.Max(0, list.Count - chronicleShown);
        if (from > 0)
        {
            var more = UIBuilder.Button("Older", content, () => { chronicleShown += 150; Rebuild(); }, out var label, 28, 14);
            label.text = $"Show older entries ({from} more)";
            label.alignment = TextAlignmentOptions.Center;
        }
        if (list.Count == 0) { Block("<color=#aaa>Nothing written yet. The scribes start soon.</color>"); return; }
        // In blocks of 40 lines (one text each), with a heading where a new age began.
        var sb = new StringBuilder();
        int lines = 0;
        int lastAge = int.MinValue;
        bool many = settlement.State.worlds.Count > 1;
        for (int i = from; i < list.Count; i++)
        {
            var e = list[i];
            int age = AgeAt(rec, e.at);
            if (age != lastAge)
            {
                lastAge = age;
                if (sb.Length > 0) { Block(sb.ToString().TrimEnd()); sb.Clear(); lines = 0; }
                var head = Block($"<b><color=#ffd56a>{settlement.Database.GetAge(age)?.displayName ?? "The early days"}</color></b>", 18);
                head.alignment = TextAlignmentOptions.Center;
            }
            string where = many && !string.IsNullOrEmpty(e.world) ? $" <color=#9cf>[{e.world}]</color>" : "";
            sb.Append($"<color=#888><size=85%>{Stamp(e.at)}</size></color>{where}  <color={KindColour((XChronicleKind)e.kind)}>{e.text}</color>\n");
            if (++lines >= 40) { Block(sb.ToString().TrimEnd()); sb.Clear(); lines = 0; }
        }
        if (sb.Length > 0) Block(sb.ToString().TrimEnd());
        footer.text = $"<color=#aaa>{list.Count} entries. The Chronicle keeps the founding and the latest {Settlement.XChronicleCap}.</color>";
    }

    /// <summary>Which age your people were in at this time (from when each age was reached).</summary>
    private static int AgeAt(XRecords rec, float at)
    {
        int best = 0;
        foreach (var s in rec.ages)
            if (s.age > best && (s.at < 0f || s.at <= at)) best = s.age;
        return best;
    }

    private void BuildAchievements()
    {
        var rec = settlement.XRec;
        rec.unseen = 0;
        var all = XAchievements.All(settlement);
        title.text = $"<b>Achievements</b>  <size=70%><color=#ccc>{rec.achieved.Count} of {all.Count} · your people call you <color=#ffd56a>{settlement.XRulerTitle()}</color></color></size>";
        foreach (var cat in XAchievements.Categories)
        {
            var sb = new StringBuilder();
            int have = 0, count = 0;
            foreach (var a in all)
            {
                if (a.category != cat) continue;
                count++;
                int idx = rec.achieved.IndexOf(a.id);
                bool done = idx >= 0;
                if (done) have++;
                string reward = a.reward > 0 ? $" <color=#9f9>(+coin)</color>" : "";
                if (done)
                {
                    float at = idx < rec.achievedAt.Count ? rec.achievedAt[idx] : -1f;
                    sb.Append($"<color=#ffd56a>[x] <b>{a.title}</b></color>  <color=#ccc>{a.description}</color>  <size=80%><color=#888>{(at >= 0 ? Stamp(at) : "earned before the records")}</color></size>\n");
                }
                else
                {
                    string prog = "";
                    if (a.progress != null)
                    {
                        var (h, n) = a.progress(settlement);
                        prog = $"  <color=#9cf>{Short(h)}/{Short(n)}</color>";
                    }
                    sb.Append($"<color=#777>[ ]</color> <b>{a.title}</b>  <color=#aaa>{a.description}</color>{prog}{reward}\n");
                }
            }
            if (count == 0) continue;
            var head = Block($"<b><color=#f0c080>{cat}</color></b>  <size=80%><color=#aaa>{have}/{count}</color></size>", 18);
            Block(sb.ToString().TrimEnd(), 15);
        }
        footer.text = "<color=#aaa>Rewards are a little coin (or food, before coin), given on the world you're on. Titles: Chieftain, Elder, Lord, King, High King, Emperor...</color>";
    }

    private static string Short(float v)
    {
        if (v >= 1e6f) return $"{v / 1e6f:0.#}M";
        if (v >= 1e4f) return $"{v / 1e3f:0.#}k";
        return v >= 100 || Mathf.Approximately(v, Mathf.Round(v)) ? $"{v:0}" : $"{v:0.#}";
    }

    private void BuildStatistics()
    {
        var s = settlement;
        var st = s.State;
        var rec = s.XRec;
        title.text = "<b>Statistics</b>";
        int built = 0, living = 0;
        foreach (var w in st.worlds) foreach (var b in w.buildings) if (b.IsBuilt) { built++; if (b.health > 0) living++; }
        var sb = new StringBuilder();
        sb.AppendLine("<b><color=#f0c080>Your people</color></b>");
        sb.AppendLine($"Time played: {Stamp(st.playTime)}");
        sb.AppendLine($"Age: {s.CurrentAge?.displayName}  ·  Ruler: {s.XRulerTitle()}");
        sb.AppendLine($"People: {s.XTotalPopulation():N0} now, {rec.bestPopulation:N0} at most");
        sb.AppendLine($"Worlds settled: {s.XFoundedWorlds()} of {st.worlds.Count} known");
        sb.AppendLine($"Buildings: {living:N0} standing, {rec.buildingsBuilt:N0} finished since the records began");
        sb.AppendLine($"Research: {s.XDistinctResearch()} things learnt  ·  Goals done: {st.goalsDone}");
        sb.AppendLine();
        sb.AppendLine("<b><color=#f0c080>War</color></b>");
        sb.AppendLine($"Raids driven off: {rec.raidsHeld:N0}  ·  broke through: {rec.raidsBroken:N0} (overran the town: {rec.raidsOverrun:N0})");
        sb.AppendLine($"Enemies slain: {rec.enemiesSlain:N0}  ·  Troops lost: {rec.troopsLost:N0}");
        sb.AppendLine($"Raider camps burnt: {st.campsCleared}  ·  Chiefs and bosses slain: {st.chiefsKilled}");
        sb.AppendLine();
        sb.AppendLine("<b><color=#f0c080>Heroes and treasure</color></b>");
        sb.AppendLine($"Heroes: {st.heroes.Count} (best level {s.XBestHeroLevel()})  ·  times fallen: {rec.heroesFallen}");
        sb.AppendLine($"Items found: {Mathf.Max(0, st.nextItemId - 1)}  ·  in your vaults: {st.inventory.Count}");
        sb.AppendLine();
        sb.AppendLine("<b><color=#f0c080>Exploring</color></b>");
        sb.AppendLine($"Zones explored: {s.XTotalExplored():N0}  ·  Kinds of land: {s.XBiomesVisitedAnywhere()}");
        sb.AppendLine($"Wild places explored: {st.sitesDone}  ·  Old tales found: {st.journal.Count}/{Lore.Entries.Length}");
        sb.AppendLine($"Natural wonders found: {s.XNaturalWondersFound()}  ·  Great Wonders standing: {s.XGreatWondersStanding()}");
        sb.AppendLine();
        sb.AppendLine("<b><color=#f0c080>The stars</color></b>");
        sb.AppendLine($"Systems charted: {st.scannedSystems.Count}  ·  Anomalies: {st.anomaliesDone.Count}  ·  Wormholes: {st.wormholes.Count / 2}");
        sb.AppendLine($"Cargo routes: {st.routes.Count}  ·  Orbital mines: {st.orbitalMines.Count}  ·  Ships in flight: {st.ships.Count}");
        sb.AppendLine();
        sb.AppendLine("<b><color=#f0c080>Trade</color></b>");
        sb.AppendLine($"Market trades: {rec.marketTrades}  ·  coin earned: {rec.marketCoinEarned:N0}  ·  spent: {rec.marketCoinSpent:N0}");
        Block(sb.ToString().TrimEnd());

        // Resources produced, the most first.
        var gathered = new List<ResourceStock>(rec.gatheredBy);
        gathered.Sort((a, b) => b.amount.CompareTo(a.amount));
        var g = new StringBuilder($"<b><color=#f0c080>Produced</color></b>  <size=80%><color=#aaa>{Short(rec.gathered)} in all</color></size>\n");
        int shown = 0;
        foreach (var r in gathered)
        {
            var res = s.Database.GetResource(r.id);
            if (res == null || r.amount < 1f) continue;
            g.Append($"{res.displayName}: {Short(r.amount)}    ");
            if (++shown % 4 == 0) g.Append('\n');
        }
        if (shown == 0) g.Append("<color=#aaa>Nothing yet.</color>");
        Block(g.ToString().TrimEnd());

        // Ages reached, and when.
        var a2 = new StringBuilder("<b><color=#f0c080>Ages reached</color></b>\n");
        var ages = new List<XAgeStamp>(rec.ages);
        ages.Sort((x, y) => x.age.CompareTo(y.age));
        foreach (var stamp in ages)
            a2.Append($"{s.Database.GetAge(stamp.age)?.displayName ?? "?"}: <color=#aaa>{Stamp(stamp.at)}</color>\n");
        Block(a2.ToString().TrimEnd());
        footer.text = rec.startedAt > 0 ? $"<color=#aaa>Counting began at {Stamp(rec.startedAt)}; older raids came from the save.</color>" : "";
    }

    private void BuildWorlds()
    {
        var s = settlement;
        var st = s.State;
        title.text = $"<b>Your worlds</b>  <size=70%><color=#ccc>{s.XFoundedWorlds()} settled</color></size>";
        for (int i = 0; i < st.worlds.Count; i++)
        {
            var w = st.worlds[i];
            int index = i;
            bool here = i == st.currentWorld;
            bool founded = s.IsWorldFounded(w);
            int built = 0, ruined = 0;
            foreach (var b in w.buildings) { if (b.IsBuilt) built++; if (b.IsBuilt && b.health <= 0) ruined++; }
            var stocks = new List<ResourceStock>(w.resources);
            stocks.Sort((a, b) => b.amount.CompareTo(a.amount));
            var sb = new StringBuilder();
            sb.Append($"<b>{w.name}</b>{(here ? " <color=#9f9>(you're here)</color>" : "")}  <color=#9cf>{WorldProfile.Get(w.profile).displayName}</color>\n");
            if (!founded) sb.Append("<color=#aaa>Not settled yet.</color>\n");
            else
            {
                int garrison = s.GarrisonSize(w);
                sb.Append($"People {Mathf.FloorToInt(w.population)}  ·  Buildings {built}{(ruined > 0 ? $" <color=#f99>({ruined} ruined)</color>" : "")}  ·  Raids survived {w.wavesSurvived}{(garrison > 0 ? $"  ·  Garrison {garrison}" : "")}\n");
                string natives = WorldProfile.Get(w.profile).natives;
                if (!string.IsNullOrEmpty(natives) && w.diplomacy != null && w.diplomacy.met)
                    sb.Append($"<color=#ccc>{natives}: {(w.diplomacy.alliance ? "allies" : w.diplomacy.peace ? "at peace" : w.diplomacy.war ? "at war" : "no treaty")} (standing {w.diplomacy.standing:0})</color>\n");
                int shown = 0;
                foreach (var r in stocks)
                {
                    var res = s.Database.GetResource(r.id);
                    if (res == null || r.amount < 1f) continue;
                    sb.Append($"<size=90%>{res.displayName} {Short(r.amount)}</size>   ");
                    if (++shown >= 8) break;
                }
            }
            var row = UIBuilder.Row("World", content, 6);
            var text = UIBuilder.Text("Info", row, sb.ToString().TrimEnd(), 15);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            if (!here)
            {
                var go = UIBuilder.Button("Go", row, () => { Show(false); s.SwitchWorld(index); }, out var label, 34, 15);
                go.GetComponent<LayoutElement>().preferredWidth = 80;
                label.text = "Go there";
                label.alignment = TextAlignmentOptions.Center;
            }
        }
        footer.text = "<color=#aaa>Every world keeps working while you're away; raids there are settled by strength.</color>";
    }

    private void BuildMessages()
    {
        title.text = "<b>Messages</b>  <size=70%><color=#ccc>this session</color></size>";
        if (messages.Count == 0) { Block("<color=#aaa>No messages yet.</color>"); return; }
        var sb = new StringBuilder();
        int lines = 0;
        foreach (var m in messages)
        {
            sb.Append(m).Append('\n');
            if (++lines >= 30) { Block(sb.ToString().TrimEnd(), 15); sb.Clear(); lines = 0; }
        }
        if (sb.Length > 0) Block(sb.ToString().TrimEnd(), 15);
        footer.text = $"<color=#aaa>The latest {MessageCap} are kept.</color>";
    }

    // ---------- The market ----------

    private ResourceType selected;
    private int amountIndex = 1;
    private static readonly float[] Amounts = { 10f, 100f, 1000f, 10000f };
    private readonly List<(ResourceType res, TextMeshProUGUI label, Button sell, Button buy)> marketRows = new List<(ResourceType, TextMeshProUGUI, Button, Button)>();
    private RectTransform spark;
    private readonly List<Image> bars = new List<Image>();
    private TextMeshProUGUI marketStatus, sparkLabel, amountLabel, marketNote;
    private int marketGoodsCount;

    private void BuildMarket()
    {
        var s = settlement;
        title.text = "<b>Market</b>";
        marketStatus = Block("", 16);
        // The chosen good's price over the last quarter hour.
        sparkLabel = Block("", 15);
        spark = UIBuilder.Rect("Sparkline", content);
        spark.gameObject.AddComponent<LayoutElement>().preferredHeight = 64;
        var back = spark.gameObject.AddComponent<Image>();
        back.color = new Color(0, 0, 0, 0.35f);
        back.raycastTarget = false;
        for (int i = 0; i < Settlement.XMarketHistory; i++)
        {
            var bar = UIBuilder.Rect("Bar", spark).gameObject.AddComponent<Image>();
            bar.raycastTarget = false;
            bars.Add(bar);
        }
        var controls = UIBuilder.Row("Amount", content, 6);
        var amount = UIBuilder.Button("Amount", controls, () => { amountIndex = (amountIndex + 1) % Amounts.Length; RefreshMarket(); }, out amountLabel, 30, 15);
        amount.GetComponent<LayoutElement>().preferredWidth = 200;
        amountLabel.alignment = TextAlignmentOptions.Center;
        if (showTip != null) UIBuilder.OnHover(amount.gameObject, () => showTip(() => "<b>Trade size</b>\nHow much each Sell or Buy click trades. Big trades move the price more."), hideTip);
        marketNote = UIBuilder.Text("Note", controls, "", 14);
        marketNote.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        marketNote.textWrappingMode = TextWrappingModes.Normal;

        var goods = s.XMarketGoods();
        marketGoodsCount = goods.Count;
        if (selected == null || !goods.Contains(selected)) selected = goods.Count > 0 ? goods[0] : null;
        foreach (var r in goods)
        {
            var res = r;
            var row = UIBuilder.Row("Good", content, 4);
            var pick = UIBuilder.Button("Pick", row, () => { selected = res; RefreshMarket(); }, out var label, 30, 15, res.icon);
            pick.GetComponent<LayoutElement>().flexibleWidth = 1;
            var sell = UIBuilder.Button("Sell", row, () => Trade(res, true), out var sellLabel, 30, 15);
            sell.GetComponent<LayoutElement>().preferredWidth = 70;
            sellLabel.text = "Sell"; sellLabel.alignment = TextAlignmentOptions.Center;
            sell.image.color = new Color(0.33f, 0.24f, 0.18f);
            var buy = UIBuilder.Button("Buy", row, () => Trade(res, false), out var buyLabel, 30, 15);
            buy.GetComponent<LayoutElement>().preferredWidth = 70;
            buyLabel.text = "Buy"; buyLabel.alignment = TextAlignmentOptions.Center;
            buy.image.color = new Color(0.2f, 0.3f, 0.22f);
            marketRows.Add((res, label, sell, buy));
        }
        footer.text = "<color=#aaa>Prices are shared by all your worlds; trades use the stock of the world you're on. Selling floods the market (the price falls), buying makes a good dear, and prices drift back over time.</color>";
        RefreshMarket();
    }

    private void Trade(ResourceType r, bool sell)
    {
        selected = r;
        float amount = Amounts[amountIndex];
        bool ok = sell ? settlement.XSell(r, amount, out string result) : settlement.XBuy(r, amount, out result);
        if (marketNote != null) marketNote.text = ok ? $"<color=#9f9>{result}</color>" : $"<color=#f99>{result}</color>";
        RefreshMarket();
    }

    private void RefreshMarket()
    {
        if (marketStatus == null) return;
        var s = settlement;
        if (s.XMarketGoods().Count != marketGoodsCount) { Rebuild(); return; }
        bool can = s.XCanTrade(out string why);
        var coin = s.Database.coin;
        string news = string.IsNullOrEmpty(s.XMarketNews) ? "" : $"\n<size=90%>{s.XMarketNews}</size>";
        marketStatus.text = can
            ? $"You have <b>{s.GetAmount(coin):N0} {coin.displayName}</b> on {s.CurrentWorld.name}.{news}"
            : $"<color=#fc8>{why}</color> Prices still move, so you can watch them.{news}";
        amountLabel.text = $"Trade {Amounts[amountIndex]:N0} at a time";
        foreach (var (res, label, sell, buy) in marketRows)
        {
            var p = s.XPriceOf(res);
            int shock = s.XShock(res);
            string tag = shock > 0 ? " <color=#9f9>BOOM</color>" : shock < 0 ? " <color=#f99>BUST</color>" : "";
            float change = p.history.Count >= 5 ? p.factor / Mathf.Max(0.01f, p.history[p.history.Count - 5]) - 1f : 0f;
            string trend = Mathf.Abs(change) < 0.01f ? "<color=#999>steady</color>" : change > 0 ? $"<color=#9f9>+{change:P0}</color>" : $"<color=#f99>{change:P0}</color>";
            string pick = res == selected ? "<color=#fd6>></color> " : "";
            label.text = $"{pick}<b>{res.displayName}</b>{tag}  <size=85%><color=#ccc>have {Short(s.GetAmount(res))}</color>   sell {s.XSellPrice(res):0.##} · buy {s.XBuyPrice(res):0.##}   {trend}</size>";
            sell.interactable = can && s.GetAmount(res) >= 1f;
            buy.interactable = can;
        }
        // The sparkline.
        if (selected == null || spark == null) { if (sparkLabel != null) sparkLabel.text = ""; return; }
        var price = s.XPriceOf(selected);
        var h = price.history;
        float lo = float.MaxValue, hi = float.MinValue;
        foreach (var v in h) { lo = Mathf.Min(lo, v); hi = Mathf.Max(hi, v); }
        if (h.Count == 0) { lo = hi = price.factor; }
        float span = Mathf.Max(0.05f, hi - lo);
        float baseValue = Settlement.XBaseValue(selected);
        sparkLabel.text = $"<b>{selected.displayName}</b>  <size=85%><color=#ccc>last {h.Count * Settlement.XMarketDriftSeconds / 60f:0} min: low {lo * baseValue:0.##}, high {hi * baseValue:0.##}, usual {baseValue:0.##} coin</color></size>";
        int n = bars.Count;
        for (int i = 0; i < n; i++)
        {
            var bar = bars[i];
            int hi2 = h.Count - n + i;
            if (hi2 < 0) { bar.enabled = false; continue; }
            bar.enabled = true;
            float v = h[hi2];
            float t = 0.12f + 0.85f * (v - lo) / span;
            var rt = bar.rectTransform;
            rt.anchorMin = new Vector2(i / (float)n, 0.04f);
            rt.anchorMax = new Vector2((i + 0.75f) / n, Mathf.Clamp01(t));
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            bar.color = v >= 1.05f ? new Color(0.55f, 0.9f, 0.5f) : v <= 0.95f ? new Color(0.95f, 0.5f, 0.45f) : new Color(0.85f, 0.8f, 0.6f);
        }
    }
}
