using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// People in the HUD: a folding "People" section in the research panel (classes, needs met and
/// schooling in one line, and a button for the census), and the Census: a window listing this
/// world's classes with what each needs and likes, schooling, and what to build next. Also puts
/// comfort decorations on the houses in view.
/// </summary>
public class PeoplePanel : MonoBehaviour
{
    public static PeoplePanel Instance { get; private set; }

    private Settlement settlement;
    private FreeModeController controller;
    private Action<Func<string>> setTip;
    private Action hideTip, fit;

    private CollapsibleSection section;
    private TextMeshProUGUI summary;
    private RectTransform window, list;
    private TextMeshProUGUI title, info, education, nextHeader, next;
    private readonly List<TextMeshProUGUI> rows = new List<TextMeshProUGUI>();
    private readonly List<int> rowTier = new List<int>();
    private float refreshAt, decorAt;

    public bool IsOpen => window != null && window.gameObject.activeSelf;

    public static PeoplePanel Create(Transform root, Settlement settlement, FreeModeController controller, Transform researchList,
        Action<Func<string>> setTip, Action hideTip, Action fit)
    {
        var go = new GameObject("People", typeof(RectTransform));
        go.transform.SetParent(root, false);
        UIBuilder.Stretch((RectTransform)go.transform);
        var p = go.AddComponent<PeoplePanel>();
        p.settlement = settlement;
        p.controller = controller;
        p.setTip = setTip;
        p.hideTip = hideTip;
        p.fit = fit;
        if (researchList != null)
        {
            p.section = new CollapsibleSection("People", researchList, "People", new Color(0.95f, 0.78f, 0.55f));
            p.section.SetOpen(false);
            p.section.Toggled += _ => fit?.Invoke();
            p.summary = UIBuilder.Text("Summary", p.section.Body, "", 15);
            p.summary.raycastTarget = true;
            UIBuilder.OnHover(p.summary.gameObject, () => setTip?.Invoke(p.SectionHelp), () => hideTip?.Invoke());
            var open = UIBuilder.Button("Census", p.section.Body, () => p.Show(!p.IsOpen), out var label, 32, 15);
            label.text = "Census";
            label.alignment = TextAlignmentOptions.Center;
            UIBuilder.OnHover(open.gameObject, () => setTip?.Invoke(() => "<b>Census</b>\nEvery class on this world: how many, what they need and like, how comfortable their homes are; schooling; and what to build next."), () => hideTip?.Invoke());
        }
        p.BuildWindow((RectTransform)go.transform);
        Instance = p;
        return p;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public static void Toggle()
    {
        if (Instance != null) Instance.Show(!Instance.IsOpen);
    }

    private string SectionHelp() =>
        "<b>People</b>\nYour people form classes by the age of the homes they live in. Each class needs a couple of goods " +
        "(pottery, cloth, ale, books, coffee, electronics, holo-media; gold, mana and chips) and likes a couple more.\n" +
        "Needs met cheer people and let homes grow comfortable, prosperous and grand (more room, finer looks); unmet needs cost a little mood. " +
        "Higher classes and schooled people work advanced buildings better; schooling speeds research.\n" +
        "<size=85%><color=#aaa>Wants come in gently, over a quarter of an hour, and each new good over ten minutes more.</color></size>";

    private void BuildWindow(RectTransform root)
    {
        window = UIBuilder.VerticalPanel("CensusPanel", root, 12, 6);
        UIBuilder.Anchor(window, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -104), new Vector2(600, 0));
        var head = UIBuilder.Row("Head", window, 6);
        title = UIBuilder.Text("Title", head, "<b>Census</b>", 22);
        title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        var close = UIBuilder.Button("Close", head, () => Show(false), out var closeLabel, 30, 16);
        close.GetComponent<LayoutElement>().preferredWidth = 34;
        closeLabel.text = "X"; closeLabel.alignment = TextAlignmentOptions.Center;

        info = UIBuilder.Text("Info", window, "", 15);
        info.textWrappingMode = TextWrappingModes.Normal;
        info.raycastTarget = true;
        UIBuilder.OnHover(info.gameObject, () => setTip?.Invoke(SectionHelp), () => hideTip?.Invoke());

        list = UIBuilder.ScrollList("Classes", window, out var element, 4);
        element.preferredHeight = 330;

        education = UIBuilder.Text("Education", window, "", 15);
        education.textWrappingMode = TextWrappingModes.Normal;
        education.raycastTarget = true;
        UIBuilder.OnHover(education.gameObject, () => setTip?.Invoke(() => settlement.PeopleEducationTooltip()), () => hideTip?.Invoke());

        nextHeader = UIBuilder.Text("NextHeader", window, "<b>What to build next</b>", 16);
        next = UIBuilder.Text("Next", window, "", 14);
        next.textWrappingMode = TextWrappingModes.Normal;
        window.gameObject.SetActive(false);
    }

    private void Show(bool open)
    {
        if (window == null) return;
        window.gameObject.SetActive(open);
        GameAudio.UI(open ? "book_open" : "book_close");
        if (!open) { hideTip?.Invoke(); return; }
        window.SetAsLastSibling();
        transform.SetAsLastSibling();
        var tip = transform.parent != null ? transform.parent.Find("Tooltip") : null;
        if (tip != null) tip.SetAsLastSibling();
        refreshAt = 0f;
    }

    private void Update()
    {
        if (settlement == null || settlement.State == null) return;
        if (Time.unscaledTime >= decorAt)
        {
            decorAt = Time.unscaledTime + 2f;
            DecorateHouses();
        }
        if (Time.unscaledTime < refreshAt) return;
        refreshAt = Time.unscaledTime + 1f;
        bool founded = settlement.IsFounded;
        if (section != null)
        {
            section.SetVisible(founded);
            if (founded)
            {
                var p = settlement.PeopleHere;
                float needs = settlement.PeopleNeedsMet;
                section.SetNote($"{(needs < 0.999f ? $"needs {needs:P0}" : "content")} · {p.literacy:P0} schooled");
                if (section.Open)
                    summary.text = $"<size=90%>{settlement.PeopleClassSummary(3)}</size>";
            }
        }
        if (!founded && IsOpen) Show(false);
        if (IsOpen) Refresh();
    }

    private void Refresh()
    {
        var s = settlement;
        var p = s.PeopleHere;
        title.text = $"<b>Census</b> <size=75%><color=#aaa>{s.CurrentWorld.name}</color></size>";
        string ramp = p.ramp < 0.999f ? $"\n<size=85%><color=#aaa>Wants are still coming in: {p.ramp:P0}.</color></size>" : "";
        float growth = p.growth - 1f, tax = s.ClassTaxFactor - 1f;
        info.text = $"{s.Population} people · needs {s.PeopleNeedsMet:P0} met · homes {p.comfortAvg:0.0}/3 comfort (+{p.bonusHousing} room)" +
                    $"\n<size=85%>Moving in <color=#9e9>+{growth:P0}</color> · class taxes x{s.ClassTaxFactor:0.00}</size>{ramp}";

        // One row per class living here, newest first.
        rowTier.Clear();
        for (int t = PeopleCatalog.ClassCount - 1; t >= 0; t--) if (p.classPop[t] >= 0.5f || p.houseCount[t] > 0) rowTier.Add(t);
        bool changed = false;
        while (rows.Count < rowTier.Count)
        {
            int index = rows.Count;
            var row = UIBuilder.Text("Class", list, "", 15);
            row.textWrappingMode = TextWrappingModes.Normal;
            row.raycastTarget = true;
            UIBuilder.OnHover(row.gameObject, () => setTip?.Invoke(() => index < rowTier.Count ? settlement.PeopleClassTooltip(rowTier[index]) : ""), () => hideTip?.Invoke());
            rows.Add(row);
            changed = true;
        }
        for (int i = 0; i < rows.Count; i++)
        {
            bool show = i < rowTier.Count;
            if (rows[i].gameObject.activeSelf != show) { rows[i].gameObject.SetActive(show); changed = true; }
            if (!show) continue;
            string text = s.PeopleClassRow(rowTier[i]);
            if (rows[i].text != text) rows[i].text = text;
        }
        education.text = s.PeopleEducationText();
        var tips = s.PeopleSuggestions();
        next.text = "· " + string.Join("\n· ", tips);
        if (changed) LayoutRebuilder.MarkLayoutForRebuild(list);
    }

    /// <summary>Comfort decorations on the houses in the scene (added once to each).</summary>
    private void DecorateHouses()
    {
        if (controller == null || controller.Grid == null) return;
        float cell = controller.Grid.cellSize;
        foreach (var view in controller.Buildings)
        {
            if (view == null || view.Definition == null || view.Definition.housing <= 0 || view.Definition.isTownCenter) continue;
            PeopleHouseDecor.Attach(view, settlement, cell);
        }
    }
}
