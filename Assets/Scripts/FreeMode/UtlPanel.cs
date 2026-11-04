using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Utilities in the HUD:
/// - A folding "Utilities" section in the research panel: one line each for water, power, data
///   and the air (hover for what they do), and a row of overlay toggles (water, power, data, smog).
/// - A small "Overlay" button in the build menu's header that cycles the same overlays.
/// No top-bar chips: everything else is in tooltips and the selection panel.
/// </summary>
public class UtlPanel : MonoBehaviour
{
    private Settlement settlement;
    private Action<Func<string>> setTip;
    private Action hideTip, fit;

    private CollapsibleSection section;
    private TextMeshProUGUI summary, headerLabel;
    private readonly List<(Image image, int mode)> toggles = new List<(Image, int)>();
    private float refreshAt;
    private int shownMode = -1;
    private bool wasVisible;

    private static readonly Color Off = new Color(0.22f, 0.24f, 0.28f, 1f);
    private static readonly Color[] OnColors =
    {
        Color.clear, new Color(0.2f, 0.42f, 0.62f), new Color(0.6f, 0.5f, 0.15f), new Color(0.15f, 0.5f, 0.52f), new Color(0.5f, 0.3f, 0.45f),
    };

    public static UtlPanel Create(Transform root, Settlement settlement, FreeModeController controller, Transform list,
        Action<Func<string>> setTip, Action hideTip, Action fit)
    {
        if (list == null || settlement == null) return null;
        var go = new GameObject("UtlPanel", typeof(RectTransform));
        go.transform.SetParent(root, false);
        var p = go.AddComponent<UtlPanel>();
        p.settlement = settlement;
        p.setTip = setTip;
        p.hideTip = hideTip;
        p.fit = fit;
        p.section = new CollapsibleSection("Utilities", list, "Utilities", new Color(0.5f, 0.82f, 0.95f));
        p.section.SetOpen(false);
        p.section.Toggled += _ => fit?.Invoke();
        p.summary = UIBuilder.Text("Summary", p.section.Body, "", 15);
        p.summary.raycastTarget = true;
        UIBuilder.OnHover(p.summary.gameObject, () => setTip?.Invoke(settlement.UtlHelpText), () => hideTip?.Invoke());
        var row = UIBuilder.Row("Overlays", p.section.Body, 4);
        row.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
        for (int m = 1; m < UtlView.ModeNames.Length; m++)
        {
            int mode = m;
            var b = UIBuilder.Button(UtlView.ModeNames[m], row, () => p.Pick(mode), out var label, 28, 13);
            label.text = UtlView.ModeNames[m];
            label.alignment = TextAlignmentOptions.Center;
            UIBuilder.OnHover(b.gameObject, () => setTip?.Invoke(() => OverlayHelp(mode)), () => hideTip?.Invoke());
            p.toggles.Add((b.image, mode));
        }
        p.AddHeaderButton(root);
        p.section.SetVisible(false);
        return p;
    }

    /// <summary>The cycling overlay button beside "By type" in the build menu header.</summary>
    private void AddHeaderButton(Transform root)
    {
        Transform header = null;
        foreach (var t in root.GetComponentsInChildren<RectTransform>(true))
            if (t.name == "Header" && t.parent != null && t.parent.name == "BuildPanel") { header = t; break; }
        if (header == null) return;
        var b = UIBuilder.Button("Overlay", header, Cycle, out headerLabel, 30, 14);
        b.GetComponent<LayoutElement>().preferredWidth = 80;
        var title = header.Find("Title");   // keep "Build" on one line beside the two buttons
        if (title != null && title.TryGetComponent<TMPro.TextMeshProUGUI>(out var titleText)) titleText.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
        headerLabel.alignment = TextAlignmentOptions.Center;
        b.transform.SetSiblingIndex(Mathf.Max(1, header.childCount - 2));   // before the grouping button
        UIBuilder.OnHover(b.gameObject, () => setTip?.Invoke(() =>
            "<b>Map overlay</b>\nClick to cycle: water, power, data, smog, off. Rings show each district; the buildings a network reaches are filled in " +
            "(orange where it's short, red for the ones it misses).\nIt also shows by itself while you place a utility."), () => hideTip?.Invoke());
    }

    private static string OverlayHelp(int mode) => mode switch
    {
        1 => "<b>Water overlay</b>\nThe district round each well, fountain, tower and purifier, and the homes it waters (homes beside watered ones get it too). Light blue: clean water. Red: homes without water, or a point with nothing to give.",
        2 => "<b>Power overlay</b>\nThe district round each generator and substation, the works it powers (works beside powered ones get it too), and the lines joining districts into grids. Yellow: enough power. Orange: a brownout. Red: works off every grid.",
        3 => "<b>Data overlay</b>\nThe district round each data centre and relay, and the works it connects (6% faster); data centres speed up research.",
        _ => "<b>Smog overlay</b>\nHow polluted the air is: yellow haze, brown smog, violet where acid rain falls.",
    };

    private void Pick(int mode)
    {
        UtlView.Mode = UtlView.Mode == mode ? 0 : mode;
        Refresh();
    }

    private void Cycle()
    {
        UtlView.Mode = (UtlView.Mode + 1) % UtlView.ModeNames.Length;
        Refresh();
    }

    private void Refresh()
    {
        shownMode = UtlView.Mode;
        foreach (var (image, mode) in toggles) image.color = mode == shownMode ? OnColors[mode] : Off;
        if (headerLabel != null) headerLabel.text = shownMode == 0 ? "Overlay" : UtlView.ModeNames[shownMode];
    }

    private void Update()
    {
        if (settlement == null || settlement.State == null) return;
        if (UtlView.Mode != shownMode) Refresh();
        if (Time.unscaledTime < refreshAt) return;
        refreshAt = Time.unscaledTime + 1f;
        var n = settlement.UtilitiesHere;
        bool visible = settlement.IsFounded && (settlement.State.ageIndex >= 1 || n.waterPoints > 0);
        if (visible != wasVisible) { wasVisible = visible; section.SetVisible(visible); fit?.Invoke(); }
        if (!visible) return;
        string note = n.waterNeed > 0f ? $"water {n.WaterCoverage:P0}" : "";
        if (n.HasGrid) note += $"{(note.Length > 0 ? " · " : "")}{(n.brownout ? "<color=#f99>" : "")}power {n.supply:0}/{n.demand:0}{(n.brownout ? "</color>" : "")}";
        section.SetNote(note);
        if (!section.Open) return;
        string text = $"<size=90%>{settlement.UtlSummaryText()}</size>";
        if (summary.text != text) summary.text = text;
    }
}
