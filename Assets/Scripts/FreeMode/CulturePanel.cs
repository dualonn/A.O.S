using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Culture and faith: a small mood badge under the top bar (right of the world switcher) opens a
/// panel with four tabs: Mood (why people feel as they do, and festivals to hold), Traditions
/// (culture perks in slots), Faith (found a religion, tenets, blessings, hire great people) and
/// Great People (use once, or settle in a building).
/// </summary>
public class CulturePanel : MonoBehaviour
{
    public static CulturePanel Instance { get; private set; }

    private Settlement settlement;
    private RectTransform panel, list;
    private TextMeshProUGUI title, info, note, badgeLabel;
    private Button badge;
    private readonly List<(Button button, TextMeshProUGUI label)> tabs = new List<(Button, TextMeshProUGUI)>();
    private int tab;
    private float refreshAt, rebuildAt;

    // Founding a religion: the tenets picked so far, and the name on offer.
    private readonly List<string> picks = new List<string>();
    private string faithName;

    private static readonly string[] TabNames = { "Mood", "Traditions", "Faith", "Great People" };

    public bool IsOpen => panel != null && panel.gameObject.activeSelf;

    public static CulturePanel Create(Transform root, Settlement settlement)
    {
        var go = new GameObject("Culture", typeof(RectTransform));
        go.transform.SetParent(root, false);
        UIBuilder.Stretch((RectTransform)go.transform);
        // (A full-screen holder that doesn't block clicks: only its children take them.)
        var p = go.AddComponent<CulturePanel>();
        p.settlement = settlement;
        p.Build((RectTransform)go.transform);
        Instance = p;
        return p;
    }

    /// <summary>Open (or close) the panel, e.g. from a hotkey.</summary>
    public static void Toggle()
    {
        if (Instance != null) Instance.Show(!Instance.IsOpen);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Build(RectTransform root)
    {
        badge = UIBuilder.Button("CultureBadge", root, () => Show(!IsOpen), out badgeLabel, 32, 15);
        UIBuilder.Anchor((RectTransform)badge.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, 1), new Vector2(136, -64), new Vector2(200, 36));
        badgeLabel.alignment = TextAlignmentOptions.Center;
        badge.gameObject.SetActive(false);

        panel = UIBuilder.VerticalPanel("CulturePanel", root, 12, 6);
        UIBuilder.Anchor(panel, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -104), new Vector2(560, 0));
        var head = UIBuilder.Row("Head", panel, 6);
        title = UIBuilder.Text("Title", head, "<b>Culture and Faith</b>", 22);
        title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        var close = UIBuilder.Button("Close", head, () => Show(false), out var closeLabel, 30, 16);
        close.GetComponent<LayoutElement>().preferredWidth = 34;
        closeLabel.text = "X"; closeLabel.alignment = TextAlignmentOptions.Center;

        var tabRow = UIBuilder.Row("Tabs", panel, 4);
        tabRow.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
        for (int i = 0; i < TabNames.Length; i++)
        {
            int index = i;
            var b = UIBuilder.Button(TabNames[i], tabRow, () => { tab = index; picks.Clear(); note.text = ""; Rebuild(); }, out var label, 32, 15);
            b.GetComponent<LayoutElement>().flexibleWidth = 1;
            label.text = TabNames[i];
            label.alignment = TextAlignmentOptions.Center;
            tabs.Add((b, label));
        }

        info = UIBuilder.Text("Info", panel, "", 15);
        info.textWrappingMode = TextWrappingModes.Normal;
        list = UIBuilder.ScrollList("List", panel, out var listElement, 4);
        listElement.preferredHeight = 360;
        note = UIBuilder.Text("Note", panel, "", 14);
        note.textWrappingMode = TextWrappingModes.Normal;
        panel.gameObject.SetActive(false);
    }

    private void Show(bool open)
    {
        panel.gameObject.SetActive(open);
        GameAudio.UI(open ? "book_open" : "book_close");
        if (!open) return;
        panel.SetAsLastSibling();
        var tip = panel.parent.parent != null ? panel.parent.parent.Find("Tooltip") : null;
        if (tip != null) tip.SetAsLastSibling();
        note.text = "";
        picks.Clear();
        Rebuild();
    }

    private void Update()
    {
        if (settlement == null || settlement.State == null) return;
        if (Time.unscaledTime < refreshAt) return;
        refreshAt = Time.unscaledTime + 0.5f;
        bool show = settlement.IsFounded;
        if (badge.gameObject.activeSelf != show) badge.gameObject.SetActive(show);
        if (!show && IsOpen) Show(false);
        if (show)
        {
            float m = settlement.MoodHere;
            string col = ColorUtility.ToHtmlStringRGB(Settlement.MoodColour(m));
            badgeLabel.text = $"<color=#{col}>{Settlement.MoodName(m)}</color> {m:0}  <color=#bbb>|</color>  Culture";
        }
        if (!IsOpen) return;
        if (Time.unscaledTime >= rebuildAt) Rebuild();
        else RefreshInfo();
    }

    // ---------- Contents ----------

    private void RefreshInfo()
    {
        var s = settlement;
        var c = s.Culture;
        string pools = $"<color=#e9c>Culture {c.culture:0}</color> (+{s.CultureRateTotal:0.##}/s)   <color=#ad9>Faith {c.faith:0}</color> (+{s.FaithRateTotal:0.##}/s)";
        switch (tab)
        {
            case 0:
            {
                float m = s.MoodHere;
                var wc = s.CultureOf(s.CurrentWorld);
                string col = ColorUtility.ToHtmlStringRGB(Settlement.MoodColour(m));
                float prod = Settlement.MoodProduction(m) - 1f, growth = Settlement.MoodGrowth(m) - 1f;
                string trend = wc.target > m + 1 ? " rising" : wc.target < m - 1 ? " falling" : "";
                var why = s.MoodReasonsHere;
                info.text = $"Mood on {s.CurrentWorld.name}: <color=#{col}><b>{Settlement.MoodName(m)}</b></color> {m:0}/100<color=#aaa>{trend} (heading for {wc.target:0})</color>\n" +
                            $"Work {Sign(prod)}{prod:P0}, people move in {Sign(growth)}{growth:P0}{(m >= 82 ? ", culture +20% (a golden age)" : "")}\n" +
                            $"<size=85%>{(why.Count > 0 ? string.Join("   ", why) : "<color=#aaa>(worked out in a moment)</color>")}</size>\n" + pools;
                break;
            }
            case 1:
                info.text = $"Traditions: small perks for all your worlds. Adopt them with culture, then put them in slots (free to swap).\n" +
                            $"Slots in use: <b>{c.slotted.Count}/{s.TraditionSlots}</b> <color=#aaa>(one more every three ages)</color>\n" + pools;
                break;
            case 2:
                info.text = s.HasReligion
                    ? $"<b><color=#ad9>{c.religion}</color></b>\nTenets: {string.Join(", ", c.tenets.ConvertAll(t => Settlement.TenetById(t)?.name))}" +
                      $" <color=#aaa>({c.tenets.Count}/{s.TenetSlots}{(s.TenetSlots < 3 ? "; a third in the Medieval Age" : "")})</color>\n" + pools
                    : $"No religion yet. Shrines and temples make faith; with {s.FoundingCost:0} faith you can found one. Pick two tenets below ({picks.Count}/2).\n" + pools;
                break;
            default:
            {
                float need = s.GreatNeeded, have = Mathf.Min(c.greatProgress, need);
                int filled = Mathf.RoundToInt(have / Mathf.Max(1f, need) * 20f);
                string bar = new string('|', filled) + $"<color=#555>{new string('|', 20 - filled)}</color>";
                info.text = $"Great people are born from culture and faith. Next one: {bar} {have / Mathf.Max(1f, need):P0}\n" +
                            $"Waiting: {s.GreatWaiting}/{Settlement.MaxWaitingGreat}. Use one for a burst, or settle them in a culture or faith building for good.\n" + pools;
                break;
            }
        }
    }

    private static string Sign(float v) => v >= 0 ? "+" : "";

    private void Rebuild()
    {
        rebuildAt = Time.unscaledTime + 3f;
        for (int i = 0; i < tabs.Count; i++)
            tabs[i].button.image.color = i == tab ? new Color(0.35f, 0.3f, 0.42f) : UIBuilder.ButtonColor;
        foreach (Transform child in list) Destroy(child.gameObject);
        RefreshInfo();
        var s = settlement;
        var c = s.Culture;
        switch (tab)
        {
            case 0:
                Header("Festivals");
                float wait = s.FestivalWait;
                foreach (var f in s.CultureFestivals())
                {
                    string key = f.key;
                    string hint = wait > 0 ? $"<color=#fc8>Ready in {wait / 60f:0.0} min.</color> {f.desc}" : f.desc;
                    Action($"{f.name}  <size=80%>({Settlement.CostText(f.cost)})</size>", hint, wait <= 0 && s.CanAfford(f.cost),
                        (out string w) => s.CultureHoldFestival(key, out w));
                }
                break;
            case 1:
                foreach (var p in Settlement.Traditions)
                {
                    if (p.age > s.State.ageIndex + 1) continue;
                    string id = p.id;
                    bool have = c.traditions.Contains(id), slotted = c.slotted.Contains(id);
                    bool locked = p.age > s.State.ageIndex;
                    string state = slotted ? "<color=#9e9>[in a slot]</color> " : have ? "<color=#aaa>[adopted]</color> " : "";
                    string action = locked ? $"<color=#aaa>{s.Database.GetAge(p.age)?.displayName}</color>"
                        : slotted ? "click to take out" : have ? "click to slot" : $"adopt for {s.TraditionCost(p):0} culture";
                    Action($"{state}<b>{p.name}</b>  <size=80%>{action}</size>", p.desc, !locked && (have || c.culture >= s.TraditionCost(p)),
                        (out string w) => have ? s.CultureToggleSlot(id, out w) : s.CultureAdopt(id, out w));
                }
                break;
            case 2:
                BuildFaith();
                break;
            default:
                BuildPeople();
                break;
        }
    }

    private void BuildFaith()
    {
        var s = settlement;
        var c = s.Culture;
        if (!s.HasReligion)
        {
            faithName ??= s.RandomReligionName();
            Header("Tenets");
            foreach (var t in Settlement.Tenets)
            {
                string id = t.id;
                bool picked = picks.Contains(id);
                Action($"{(picked ? "<color=#9e9>[picked]</color> " : "")}<b>{t.name}</b>", t.desc, picked || picks.Count < 2, (out string w) =>
                {
                    w = null;
                    if (!picks.Remove(id)) picks.Add(id);
                    return true;
                });
            }
            Header("Found it");
            Action($"Found <b>{faithName}</b>  <size=80%>({s.FoundingCost:0} faith)</size>", picks.Count == 2 ? "Your tenets work on every world from then on." : "Pick two tenets first.",
                picks.Count == 2 && c.faith >= s.FoundingCost, (out string w) =>
                {
                    bool ok = s.FaithFound(faithName, picks, out w);
                    if (ok) picks.Clear();
                    return ok;
                });
            Action("Another name", "Free.", true, (out string w) => { w = null; faithName = s.RandomReligionName(); return true; });
            return;
        }
        if (c.tenets.Count < s.TenetSlots)
        {
            Header($"A new tenet ({s.ExtraTenetCost:0} faith)");
            foreach (var t in Settlement.Tenets)
            {
                if (c.tenets.Contains(t.id)) continue;
                string id = t.id;
                Action($"<b>{t.name}</b>", t.desc, c.faith >= s.ExtraTenetCost, (out string w) => s.FaithAddTenet(id, out w));
            }
        }
        Header("Blessings");
        foreach (var o in s.FaithOffers())
        {
            string key = o.key;
            Action($"{o.name}  <size=80%>({o.faith:0} faith)</size>", o.desc, c.faith >= o.faith, (out string w) => s.FaithBuy(key, out w));
        }
        Header($"Hire a great person ({s.GreatHireCost:0} faith)");
        foreach (var kind in Settlement.GreatKinds)
        {
            string k = kind;
            Action($"A {Settlement.GreatTitle(k, s.State.ageIndex).ToLower()} <size=80%><color=#aaa>({k})</color></size>", Settlement.GreatUseText(k),
                c.faith >= s.GreatHireCost && s.GreatWaiting < Settlement.MaxWaitingGreat, (out string w) => s.GreatHire(k, out w));
        }
        Action("Rename the faith", "Free.", true, (out string w) => { w = null; s.FaithRename(s.RandomReligionName()); return true; });
    }

    private void BuildPeople()
    {
        var s = settlement;
        var c = s.Culture;
        if (c.people.Count == 0)
        {
            Header("<color=#aaa>No great people yet. Culture and faith buildings bring them.</color>");
            return;
        }
        foreach (var p in c.people)
        {
            int id = p.id;
            if (p.Settled)
            {
                string world = p.world >= 0 && p.world < s.State.worlds.Count ? s.State.worlds[p.world].name : "?";
                var b = p.world >= 0 && p.world < s.State.worlds.Count ? s.State.worlds[p.world].buildings.Find(x => x.instanceId == p.building) : null;
                string where = b != null ? s.Database.GetBuilding(b.definitionId)?.displayName ?? "a building" : "a building";
                string down = b != null && (b.health <= 0 || !b.IsBuilt) ? " <color=#f88>(the building is ruined: no bonus until it's repaired)</color>" : "";
                Header($"<color=#fd6>{p.name}</color> <size=80%><color=#aaa>lives in the {where} on {world}.{down}\n{Settlement.GreatSettleText(p.kind)}</color></size>");
                continue;
            }
            Header($"<color=#fd6>{p.name}</color> <size=80%><color=#aaa>({p.kind}, waiting)</color></size>");
            var target = s.GreatSettleTarget(p);
            string targetName = target != null ? s.Database.GetBuilding(target.definitionId)?.displayName : null;
            Action("Use now", Settlement.GreatUseText(p.kind), true, (out string w) => s.GreatUse(id, out w));
            Action(targetName != null ? $"Settle in the {targetName}" : "Settle", Settlement.GreatSettleText(p.kind), target != null, (out string w) => s.GreatSettle(id, out w));
        }
    }

    // ---------- Widgets ----------

    private void Header(string text)
    {
        var t = UIBuilder.Text("Header", list, text, 16);
        t.textWrappingMode = TextWrappingModes.Normal;
    }

    private delegate bool Act(out string why);

    private void Action(string text, string hint, bool enabled, Act act)
    {
        var b = UIBuilder.Button("Action", list, () =>
        {
            bool ok = act(out string why);
            Rebuild();
            note.text = ok ? "" : $"<color=#f99>{why}</color>";
        }, out var label, 34, 15);
        label.text = $"{text}\n<size=75%><color=#ccc>{hint}</color></size>";
        label.alignment = TextAlignmentOptions.Left;
        b.GetComponent<LayoutElement>().preferredHeight = 44;
        b.interactable = enabled;
    }
}
