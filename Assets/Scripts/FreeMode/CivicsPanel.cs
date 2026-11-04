using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Government: a folding section in the research panel (the government, its legitimacy, and three
/// buttons) that opens one window with tabs: Overview (bonuses, laws in force, stability, pardon),
/// Governments (the forms of government through the ages, with their crests), Laws (policies by
/// category in slots, with a search box) and Taxes (the tax rate and tithes). Each world has its own.
/// </summary>
public class CivicsPanel : MonoBehaviour
{
    public static CivicsPanel Instance { get; private set; }

    public enum Tab { Overview, Governments, Laws, Taxes }

    private Settlement settlement;
    private Action<Func<string>> showTip;
    private UnityAction hideTip;
    private Action fit;

    private CollapsibleSection section;
    private TextMeshProUGUI sectionText;
    private RectTransform panel, content;
    private ScrollRect scroll;
    private Image crest;
    private TextMeshProUGUI title, info, note;
    private TMP_InputField search;
    private readonly Dictionary<Tab, Button> tabButtons = new Dictionary<Tab, Button>();
    private Tab tab;
    private string filter = "";
    private float refreshAt, sectionAt;
    private string shownSignature;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Instance = null;

    public bool IsOpen => panel != null && panel.gameObject.activeSelf;

    public static CivicsPanel Create(Transform root, Settlement settlement, Transform researchContent,
        Action<Func<string>> showTip, UnityAction hideTip, Action fitPanels)
    {
        var go = new GameObject("Government", typeof(RectTransform));
        go.transform.SetParent(root, false);
        UIBuilder.Stretch((RectTransform)go.transform);
        var p = go.AddComponent<CivicsPanel>();
        p.settlement = settlement;
        p.showTip = showTip;
        p.hideTip = hideTip;
        p.fit = fitPanels;
        p.BuildSection(researchContent);
        p.BuildWindow((RectTransform)go.transform);
        Instance = p;
        return p;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ---------- The section in the research list ----------

    private void BuildSection(Transform parent)
    {
        if (parent == null) return;
        section = new CollapsibleSection("Government", parent, "Government", new Color(0.85f, 0.75f, 0.95f));
        section.SetOpen(false);
        section.Toggled += _ => fit?.Invoke();
        sectionText = UIBuilder.Text("Summary", section.Body, "", 15);
        sectionText.textWrappingMode = TextWrappingModes.Normal;
        sectionText.raycastTarget = true;
        if (showTip != null) UIBuilder.OnHover(sectionText.gameObject, () => showTip(Help), hideTip);
        RectTransform row = null;
        void B(string text, Tab t, string help)
        {
            if (row == null || row.childCount >= 2)
            {
                row = UIBuilder.Row("Row", section.Body, 5);
                row.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
            }
            var b = UIBuilder.Button(text, row, () => Open(t), out var label, 32, 15);
            label.text = text;
            label.alignment = TextAlignmentOptions.Center;
            b.GetComponent<LayoutElement>().flexibleWidth = 1;
            if (showTip != null) UIBuilder.OnHover(b.gameObject, () => showTip(() => $"<b>{text}</b>\n{help}"), hideTip);
        }
        B("Overview", Tab.Overview, "Your government on this world: its bonuses and drawbacks, the laws in force, and how stable it is.");
        B("Governments", Tab.Governments, "The forms of government through the ages: choose one (the first choice is free).");
        B("Laws", Tab.Laws, "Policies in slots by category: economy, military, society, faith, science and expansion. Each is a trade-off.");
        B("Taxes", Tab.Taxes, "The tax rate (coin against mood) and tithes for the faith.");
        section.SetVisible(false);
    }

    private static string Help() =>
        "<b>Government</b>\nEach world has its own government, laws and taxes. Governments follow the ages, each with its bonuses, " +
        "drawbacks and slots for laws. Changing government brings a spell of anarchy (less work, research and tax) and can't be done again for a while; " +
        "the first choice is free.\n<b>Legitimacy</b> grows with a happy people, culture and some governments and laws; harsh rule and frequent " +
        "reform wear it down. High legitimacy lifts work; low legitimacy saps it, and harsh rule over a miserable people can end in revolt.";

    private void RefreshSection()
    {
        if (section == null) return;
        var s = settlement;
        bool show = s.IsFounded;
        if (show != section.Root.gameObject.activeSelf) { section.SetVisible(show); fit?.Invoke(); }
        if (!show) return;
        var w = s.CurrentWorld;
        var c = s.CivOf(w);
        var g = s.GovOf(w);
        bool anarchy = s.CivInAnarchy(w);
        section.SetNote(anarchy ? "<color=#fb7>anarchy</color>" : g.name);
        if (!section.Open) return;
        sectionText.text = $"<size=90%>{StatusLine(w, c, g)}</size>";
    }

    private string StatusLine(WorldState w, CivicsWorld c, CivGovernment g)
    {
        var s = settlement;
        float now = s.State.playTime;
        if (s.CivInAnarchy(w)) return $"<color=#fb7>Anarchy</color>: the {g.name} takes power in {(c.anarchyUntil - now) / 60f:0.0} min.";
        string legit = s.CivIsCustomary(w) ? "<color=#aaa>the old ways</color>"
            : $"legitimacy <color=#{ColorUtility.ToHtmlStringRGB(Settlement.CivLegitColour(c.legitimacy))}>{Settlement.CivLegitName(c.legitimacy)} {c.legitimacy:0}</color>";
        string laws = $"{s.CivPoliciesOf(w).Count} law{(s.CivPoliciesOf(w).Count == 1 ? "" : "s")}";
        string risk = c.revoltAt > 0 ? " · <color=#f97>rebels gathering!</color>" : s.CivRevoltRisk(w) >= 2 ? " · <color=#f97>revolt risk</color>" : "";
        return $"<b>{g.name}</b> · {legit} · {laws}{risk}";
    }

    // ---------- The window ----------

    private void BuildWindow(RectTransform root)
    {
        panel = UIBuilder.VerticalPanel("GovernmentWindow", root, 12, 6);
        UIBuilder.Anchor(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(760, 0));
        var head = UIBuilder.Row("Head", panel, 8);
        crest = UIBuilder.Icon("Crest", head, null, 44);
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
            var b = UIBuilder.Button(t.ToString(), tabs, () => { tab = tt; note.text = ""; Rebuild(); }, out var label, 30, 15);
            label.text = t == Tab.Overview ? "Overview" : t == Tab.Laws ? "Laws" : t.ToString();
            label.alignment = TextAlignmentOptions.Center;
            b.GetComponent<LayoutElement>().flexibleWidth = 1;
            tabButtons[t] = b;
        }
        info = UIBuilder.Text("Info", panel, "", 15);
        info.textWrappingMode = TextWrappingModes.Normal;
        search = UIBuilder.SearchField("Search", panel, "Search laws (name or effect)...", text => { filter = text.Trim(); Rebuild(true); });
        content = UIBuilder.ScrollList("Content", panel, out var element, 4);
        element.preferredHeight = 460;
        scroll = element.GetComponent<ScrollRect>();
        note = UIBuilder.Text("Note", panel, "", 14);
        note.textWrappingMode = TextWrappingModes.Normal;
        panel.gameObject.SetActive(false);
    }

    public void Open(Tab t)
    {
        tab = t;
        Show(true);
    }

    public static void Toggle()
    {
        if (Instance != null) Instance.Show(!Instance.IsOpen);
    }

    private void Show(bool open)
    {
        panel.gameObject.SetActive(open);
        GameAudio.UI(open ? "book_open" : "book_close");
        if (!open) { hideTip?.Invoke(); return; }
        transform.SetAsLastSibling();
        var tip = transform.parent != null ? transform.parent.Find("Tooltip") : null;
        if (tip != null) tip.SetAsLastSibling();
        note.text = "";
        Rebuild();
    }

    private void Update()
    {
        if (settlement == null || settlement.State == null) return;
        if (Time.unscaledTime >= sectionAt)
        {
            sectionAt = Time.unscaledTime + 1f;
            RefreshSection();
        }
        if (!IsOpen) return;
        if (!settlement.IsFounded) { Show(false); return; }
        if (Input.GetKeyDown(KeyCode.Escape) && !UIBuilder.Typing) { Show(false); return; }
        if (Time.unscaledTime < refreshAt) return;
        refreshAt = Time.unscaledTime + 1f;
        RefreshInfo();
        if (Signature() != shownSignature) Rebuild(true);
    }

    private string Signature()
    {
        var s = settlement;
        var w = s.CurrentWorld;
        var c = s.CivOf(w);
        int coarse = Mathf.FloorToInt(s.State.playTime / 5f);
        // (what can be afforded, not the culture itself, so the list isn't rebuilt every second)
        int culture = 0;
        foreach (var p in Settlement.Policies) if (s.CivPolicyCost(p, w) <= s.Culture.culture) culture++;
        if (s.Culture.culture >= s.CivSwitchCulture(w)) culture += 1000;
        return $"{tab}:{s.State.currentWorld}:{c.gov}:{s.CivPoliciesOf(w).Count}:{c.taxOffset}:{c.tithe}:{s.CivInAnarchy(w)}:{s.State.ageIndex}:{culture}:{(tab == Tab.Overview ? coarse : 0)}";
    }

    // ---------- Contents ----------

    private void RefreshInfo()
    {
        var s = settlement;
        var w = s.CurrentWorld;
        var c = s.CivOf(w);
        var g = s.GovOf(w);
        crest.sprite = CivicsEmblems.For(g);
        title.text = $"<b>{g.name}</b> <size=70%><color=#aaa>on {w.name}</color></size>";
        string pools = $"<color=#e9c>Culture {s.Culture.culture:0}</color>" + (s.Database.coin != null ? $"   <color=#fd6>Coin {s.GetAmount(s.Database.coin):0}</color>" : "");
        string legit = s.CivIsCustomary(w) ? "Legitimacy: <color=#aaa>the old ways (counts once you choose a government)</color>"
            : $"Legitimacy: <color=#{ColorUtility.ToHtmlStringRGB(Settlement.CivLegitColour(c.legitimacy))}><b>{Settlement.CivLegitName(c.legitimacy)}</b> {c.legitimacy:0}/100</color> <color=#aaa>({Sign(s.CivLegitRate(w))}{s.CivLegitRate(w):0.0}/min)</color>";
        info.text = $"{StatusLine(w, c, g)}\n{legit}\n{pools}";
        search.gameObject.SetActive(tab == Tab.Laws);
    }

    private static string Sign(float v) => v >= 0 ? "+" : "";

    private void Rebuild(bool keepScroll = false)
    {
        float pos = scroll != null ? scroll.verticalNormalizedPosition : 1f;
        foreach (Transform child in content) Destroy(child.gameObject);
        foreach (var pair in tabButtons)
            pair.Value.image.color = pair.Key == tab ? new Color(0.38f, 0.3f, 0.45f) : UIBuilder.ButtonColor;
        shownSignature = Signature();
        RefreshInfo();
        switch (tab)
        {
            case Tab.Overview: BuildOverview(); break;
            case Tab.Governments: BuildGovernments(); break;
            case Tab.Laws: BuildLaws(); break;
            case Tab.Taxes: BuildTaxes(); break;
        }
        if (scroll == null) return;
        Canvas.ForceUpdateCanvases();
        scroll.verticalNormalizedPosition = keepScroll ? pos : 1f;
    }

    private void BuildOverview()
    {
        var s = settlement;
        var w = s.CurrentWorld;
        var c = s.CivOf(w);
        var g = s.GovOf(w);
        float now = s.State.playTime;
        Header($"<color=#dcf>{g.name}</color> <size=80%><color=#aaa>{(string.IsNullOrEmpty(g.ruler) ? "" : $"ruled by the {g.ruler.ToLowerInvariant()}")}</color></size>");
        Block($"<size=90%>{g.desc}</size>");
        Block(g.fx.Text("   "));
        if (g.member)
        {
            var cap = s.CivFederationCapital();
            Block(cap != null && cap != w ? $"<color=#9cf>The Federation's laws (set on {cap.name}) are in force here.</color>"
                : "<color=#f99>There's no Stellar Federation to belong to: no federal laws apply.</color>");
        }

        Header("Laws in force");
        var laws = s.CivPoliciesOf(w);
        for (int i = 0; i < 6; i++)
        {
            var cat = (CivCategory)i;
            int slots = s.CivSlots(w, cat);
            var names = new List<string>();
            foreach (var id in laws) { var p = Settlement.PolicyById(id); if (p != null && p.cat == cat) names.Add(p.name); }
            if (slots == 0 && names.Count == 0) continue;
            string col = ColorUtility.ToHtmlStringRGB(Settlement.CivCategoryColour(cat));
            Block($"<color=#{col}>{Settlement.CivCategoryName(cat)}</color> <color=#aaa>({names.Count}/{slots})</color>: {(names.Count > 0 ? string.Join(", ", names) : "<color=#888>empty</color>")}");
        }
        Header("All in all");
        var fx = s.CivTotalsHere.fx;
        Block($"<size=90%>{fx.Text("   ")}</size>");

        Header("Stability");
        int risk = s.CivRevoltRisk(w);
        string riskText = c.revoltAt > 0 ? $"<color=#f97>Rebels are gathering: they rise in {Mathf.Max(0f, c.revoltAt - now):0}s.</color>"
            : risk >= 2 ? "<color=#f97>Revolt is in the air: the people are miserable under harsh rule.</color>"
            : risk == 1 ? "<color=#fc8>Grumbling: unhappy people under harsh rule. Keep them content or ease off.</color>"
            : "<color=#9e9>No sign of revolt.</color>";
        Block(riskText);
        if (c.strikeUntil > now) Block($"<color=#fb7>A general strike: work -20% for {(c.strikeUntil - now) / 60f:0.0} min more.</color>");
        string cooldown = now < c.switchReadyAt ? $"Next change of government in {(c.switchReadyAt - now) / 60f:0.0} min." : "The government may be changed.";
        Block($"<size=90%><color=#ccc>{cooldown}  Changes: {c.changes} · revolts: {c.revolts}" +
              (string.IsNullOrEmpty(c.previous) || c.changes == 0 ? "" : $" · before: {Settlement.GovernmentById(c.previous).name}") + "</color></size>");
        var cost = s.CivPardonCost();
        bool ready = now >= c.pardonReadyAt;
        Action($"Grant a pardon  <size=80%>({Settlement.CostText(cost)})</size>",
            ready ? "Legitimacy +12, and any rebels gathering go home." : $"Ready in {(c.pardonReadyAt - now) / 60f:0.0} min.",
            ready && s.CanAfford(cost), (out string why) => s.CivPardon(out why));
    }

    private void BuildGovernments()
    {
        var s = settlement;
        var w = s.CurrentWorld;
        var c = s.CivOf(w);
        float now = s.State.playTime;
        bool free = s.CivSwitchIsFree(w);
        var money = s.CivSwitchCost(w);
        float culture = s.CivSwitchCulture(w);
        string price = free ? "<color=#9e9>Your first choice is free, with no anarchy.</color>"
            : $"Changing costs {culture:0} culture{(money.Count > 0 ? $" and {Settlement.CostText(money)}" : "")}, brings anarchy, and then {Settlement.CivSwitchCooldown / 60f:0} min before the next change.";
        if (now < c.switchReadyAt) price += $"  <color=#fc8>Next change in {(c.switchReadyAt - now) / 60f:0.0} min.</color>";
        Block($"<size=90%>{price}</size>");
        var current = s.GovOf(w);
        int shown = 0;
        foreach (var g in Settlement.Governments)
        {
            if (g.given && g.id != Settlement.CivCustomary) continue;
            if (g.age > s.State.ageIndex + 1) continue;
            if (g.colonyOnly && s.State.currentWorld == 0) continue;
            bool isNow = g == current;
            bool unlocked = s.CivGovUnlocked(g, w, out string why);
            string ageName = s.Database.GetAge(g.age)?.displayName ?? "";
            string head = isNow ? "<color=#9e9>[now]</color> " : "";
            string name = g.id == Settlement.CivCustomary ? "Return to Customary Rule" : g.name;
            float anarchy = s.CivAnarchyFor(w, g);
            string hint = unlocked || isNow
                ? g.fx.Text(" · ") + (isNow || anarchy <= 0 ? "" : $" <color=#aaa>(anarchy {anarchy / 60f:0.0} min)</color>")
                : $"<color=#f99>{why}</color>";
            var gg = g;
            Action($"{head}<b>{name}</b>  <size=80%><color=#aaa>{ageName}</color></size>", hint, !isNow && unlocked,
                (out string w2) => s.CivSetGovernment(gg.id, out w2), CivicsEmblems.For(g), () => s.CivGovTooltip(gg));
            shown++;
        }
        if (shown == 0) Block("<color=#aaa>No governments to choose yet.</color>");
    }

    private void BuildLaws()
    {
        var s = settlement;
        var w = s.CurrentWorld;
        var laws = s.CivPoliciesOf(w);
        bool readOnly = s.GovOf(w).member && s.CivLawsOf(w) != w;
        if (readOnly) Block($"<color=#9cf>This world follows the Federation's laws: change them on {s.CivLawsOf(w).name}.</color>");
        else Block("<size=90%><color=#ccc>Laws cost culture to pass; repealing is free but costs a little legitimacy. More slots come with later governments, a religion (Faith) and a second world (Expansion).</color></size>");
        string f = filter.ToLowerInvariant();
        for (int i = 0; i < 6; i++)
        {
            var cat = (CivCategory)i;
            int slots = s.CivSlots(w, cat), used = s.CivUsed(w, cat);
            bool headed = false;
            // Laws in force first, then the rest in age order.
            for (int pass = 0; pass < 2; pass++)
                foreach (var p in Settlement.Policies)
                {
                    if (p.cat != cat) continue;
                    bool inForce = laws.Contains(p.id);
                    if (pass == 0 != inForce) continue;
                    if (!inForce && p.age > s.State.ageIndex + 1) continue;
                    if (f.Length > 0 && !Matches(p, f)) continue;
                    if (!headed)
                    {
                        headed = true;
                        string col = ColorUtility.ToHtmlStringRGB(Settlement.CivCategoryColour(cat));
                        Header($"<color=#{col}>{Settlement.CivCategoryName(cat)}</color>  <size=80%><color=#aaa>{used}/{slots} slot{(slots == 1 ? "" : "s")}</color></size>");
                    }
                    var pp = p;
                    bool unlocked = s.CivPolicyUnlocked(p, w, out string why);
                    var rival = s.CivRival(p, w);
                    float cost = s.CivPolicyCost(p, w);
                    string state = inForce ? "<color=#9e9>[law]</color> " : "";
                    string act = readOnly ? "" : inForce ? "click to repeal"
                        : !unlocked ? $"<color=#f99>{why}</color>"
                        : rival != null ? $"<color=#fb7>rules out {rival.name}</color>"
                        : used >= slots ? "<color=#fb7>no free slot</color>"
                        : $"pass for {cost:0} culture";
                    bool enabled = !readOnly && (inForce || unlocked && rival == null && used < slots && s.Culture.culture >= cost);
                    Action($"{state}<b>{p.name}</b>  <size=80%>{act}</size>", p.fx.Text(" · "), enabled,
                        (out string w2) => inForce ? s.CivRepeal(pp.id, out w2) : s.CivAdopt(pp.id, out w2), null, () => s.CivPolicyTooltip(pp));
                }
        }
    }

    private static bool Matches(CivPolicy p, string f)
    {
        if (p.name.ToLowerInvariant().Contains(f) || p.desc.ToLowerInvariant().Contains(f)) return true;
        if (Settlement.CivCategoryName(p.cat).ToLowerInvariant().Contains(f)) return true;
        foreach (var (text, _) in p.fx.Lines()) if (text.ToLowerInvariant().Contains(f)) return true;
        return false;
    }

    private void BuildTaxes()
    {
        var s = settlement;
        var w = s.CurrentWorld;
        var c = s.CivOf(w);
        int level = s.CivTaxLevel(w);
        var coin = s.Database.coin;
        bool coinOn = coin != null && (coin.unlockedIn == null || coin.unlockedIn.order <= s.State.ageIndex);
        Header("Tax rate");
        if (!coinOn) Block($"<color=#aaa>Taxes are paid in coin, which comes in the {coin?.unlockedIn?.displayName ?? "Classical Age"}. Until then the rate does nothing.</color>");
        else Block($"<size=90%>Taxes now: {s.TaxPerPersonPerSecond * s.Population * 60f:0} coin a minute from {s.Population} people. Raising taxes costs a little legitimacy.</size>");
        string[] notes = { "No taxes at all: people love it.", "A light touch.", "As it has always been.", "Squeezing a little harder.", "Heavy taxes: harsh rule.", "Crushing: very harsh rule." };
        for (int i = 0; i < Settlement.CivTaxNames.Length; i++)
        {
            int li = i;
            var fx = new CivEffect { tax = Settlement.CivTaxRate[i], mood = Settlement.CivTaxMood[i], harsh = i >= 5 ? 2 : i >= 4 ? 1 : 0 };
            string head = i == level ? "<color=#9e9>[now]</color> " : "";
            Action($"{head}<b>{Settlement.CivTaxNames[i]}</b>  <size=80%><color=#aaa>{notes[i]}</color></size>", fx.Text(" · "), i != level,
                (out string why) => { why = null; s.CivSetTax(li); return true; });
        }
        Header("Tithes");
        Block(s.HasReligion ? $"<size=90%>Part of the taxes go to {s.Culture.religion}: more faith, less coin.</size>"
            : "<size=90%><color=#aaa>Tithes feed the faith of your shrines and temples (they do more once you have a religion).</color></size>");
        string[] titheNotes = { "The temples live on gifts.", "A tenth for the gods.", "A fifth for the gods." };
        for (int i = 0; i < Settlement.CivTitheNames.Length; i++)
        {
            int li = i;
            var fx = new CivEffect { faith = 1f + 0.15f * i, tax = 1f - 0.08f * i, mood = i == 0 ? 0 : i == 1 ? -1 : -3 };
            string head = i == c.tithe ? "<color=#9e9>[now]</color> " : "";
            Action($"{head}<b>{Settlement.CivTitheNames[i]}</b>  <size=80%><color=#aaa>{titheNotes[i]}</color></size>",
                fx.Text(" · ") + (i > 0 ? " <color=#aaa>(no mood cost under a Theocracy, an Ascended Synod or a State Religion)</color>" : ""),
                i != c.tithe, (out string why) => { why = null; s.CivSetTithe(li); return true; });
        }
    }

    // ---------- Widgets ----------

    private void Header(string text)
    {
        var t = UIBuilder.Text("Header", content, text, 17);
        t.textWrappingMode = TextWrappingModes.Normal;
    }

    private void Block(string text)
    {
        var t = UIBuilder.Text("Text", content, text, 15);
        t.textWrappingMode = TextWrappingModes.Normal;
    }

    private delegate bool Act(out string why);

    private void Action(string text, string hint, bool enabled, Act act, Sprite icon = null, Func<string> tooltip = null)
    {
        var b = UIBuilder.Button("Action", content, () =>
        {
            bool ok = act(out string why);
            Rebuild(true);
            note.text = ok ? "" : $"<color=#f99>{why}</color>";
        }, out var label, 48, 15, icon);
        label.text = $"{text}\n<size=75%><color=#ccc>{hint}</color></size>";
        label.alignment = TextAlignmentOptions.Left;
        label.textWrappingMode = TextWrappingModes.Normal;
        b.GetComponent<LayoutElement>().preferredHeight = 50;
        b.interactable = enabled;
        if (tooltip != null && showTip != null) UIBuilder.OnHover(b.gameObject, () => showTip(tooltip), hideTip);
    }
}
