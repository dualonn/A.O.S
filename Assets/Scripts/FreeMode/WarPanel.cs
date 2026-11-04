using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Warfare in the HUD, kept out of the way:
/// - an "Army" section in the research panel (once you train troops): the commander, the record,
///   each regiment of this world (click to select its building) and the latest battle reports;
/// - a Drill button in the building panel of training buildings (a capped upgrade line);
/// - a line or two under a selected troop: its rank and morale, and for heroes, command.
/// Each part refreshes itself on a timer.
/// </summary>
public class WarPanel : MonoBehaviour
{
    private Settlement settlement;
    private FreeModeController controller;
    private Action<Func<string>> setTip;
    private Action hideTip, fit;

    private CollapsibleSection section;
    private TextMeshProUGUI summary, reportsText;
    private readonly List<(Button button, TextMeshProUGUI label, int building)> rows = new List<(Button, TextMeshProUGUI, int)>();
    private readonly List<WarRegiment> shown = new List<WarRegiment>();
    private float refreshAt;
    private bool wasVisible;

    private static string Cost(List<ResourceAmount> cost) =>
        string.Join(", ", cost.Where(c => c.resource != null).Select(c => $"{c.amount} {c.resource.displayName}"));

    // ---------- The Army section ----------

    public static WarPanel Create(Transform root, Settlement settlement, FreeModeController controller, Transform list,
        Action<Func<string>> setTip, Action hideTip, Action fit)
    {
        if (list == null || settlement == null) return null;
        var go = new GameObject("WarPanel", typeof(RectTransform));
        go.transform.SetParent(root, false);
        var p = go.AddComponent<WarPanel>();
        p.settlement = settlement;
        p.controller = controller;
        p.setTip = setTip;
        p.hideTip = hideTip;
        p.fit = fit;
        p.section = new CollapsibleSection("Army", list, "Army", new Color(0.95f, 0.55f, 0.45f));
        p.section.SetOpen(false);
        p.section.Toggled += _ => fit?.Invoke();
        p.summary = UIBuilder.Text("Summary", p.section.Body, "", 15);
        p.summary.raycastTarget = true;
        UIBuilder.OnHover(p.summary.gameObject, () => setTip?.Invoke(p.Help), () => hideTip?.Invoke());
        p.reportsText = UIBuilder.Text("Reports", p.section.Body, "", 14);
        p.reportsText.raycastTarget = true;
        UIBuilder.OnHover(p.reportsText.gameObject, () => setTip?.Invoke(p.ReportsTip), () => hideTip?.Invoke());
        p.section.SetVisible(false);
        return p;
    }

    private string Help() =>
        "<b>Army</b>\nTroops learn from every fight: kills and battles survived raise them from Recruit to Regular, Veteran, Elite " +
        "and Legend (a mark by the health bar), each tougher and harder-hitting. Troops are trained again when they fall, but " +
        "each building's <b>regiment</b> keeps its experience: recruits join seasoned regiments at a higher rank (as high as its " +
        "Drill allows).\n<b>Morale</b> drops when friends fall nearby, when outnumbered or shelled, and when a chief roars; it rises " +
        "near heroes and the commander, at home in your towns, and with a happy people. Broken troops run for a few seconds, then " +
        "rally. Raiders break too, and a raid that breaks runs for home.\n<b>Commander</b>: select a hero and appoint them. Troops near " +
        "the commander's banner fight harder and hold firmer, and the commander earns command traits over battles.\n" +
        "<b>Siege</b>: bigger raids bring siege engines that go for your towers and buildings; build a siege workshop to train your own " +
        "for burning camps.";

    private string ReportsTip()
    {
        var list = settlement.War.reports;
        if (list.Count == 0) return "<b>Battle reports</b>\nNone yet.";
        var sb = new StringBuilder("<b>Battle reports</b>");
        for (int i = list.Count - 1; i >= 0 && i >= list.Count - 8; i--)
        {
            var r = list[i];
            sb.Append($"\n<color=#aaa>{XTime(r.at)}, {r.world}:</color> {r.Line()}");
        }
        return sb.ToString();
    }

    private static string XTime(float t)
    {
        int m = Mathf.FloorToInt(t / 60f);
        return m >= 60 ? $"{m / 60}h {m % 60:00}m" : $"{m}m";
    }

    private void Update()
    {
        if (settlement == null || settlement.State == null || Time.unscaledTime < refreshAt) return;
        refreshAt = Time.unscaledTime + 1f;
        bool any = settlement.State.buildings.Exists(b => settlement.Database.GetBuilding(b.definitionId)?.trainsUnit != null);
        section.SetVisible(any);
        if (any != wasVisible) { wasVisible = any; fit?.Invoke(); }
        if (!any) return;

        var war = settlement.War;
        shown.Clear();
        int world = settlement.State.currentWorld;
        foreach (var r in war.regiments)
            if (r.world == world && settlement.State.buildings.Exists(b => b.instanceId == r.building)) shown.Add(r);
        shown.Sort((a, b) => b.xp.CompareTo(a.xp));
        int best = shown.Count > 0 ? Settlement.RegimentRank(shown[0]) : 0;
        section.SetNote(shown.Count == 0 ? "untried" : $"{shown.Count} regiment{(shown.Count == 1 ? "" : "s")} · best {WarCatalog.RankName(best)}");
        if (!section.Open) return;

        var commander = settlement.CommanderHere();
        var sb = new StringBuilder("<size=90%>");
        if (commander != null)
        {
            var rec = settlement.WarHero(commander.id, false);
            int traits = rec != null ? rec.traits.Count : 0;
            sb.Append($"Commander: <color=#fd6>{commander.name}</color>{(settlement.IsDown(commander) ? " <color=#f88>(down)</color>" : "")}" +
                      $" · {traits}/{settlement.WarTraitSlots} traits\n");
        }
        else sb.Append("<color=#aaa>No commander: select a hero to appoint one.</color>\n");
        sb.Append($"<color=#ccc>{war.battles} battles · {war.victories} won{(war.raidersRouted > 0 ? $" · {war.raidersRouted} raiders routed" : "")}</color></size>");
        summary.text = sb.ToString();

        bool rebuilt = false;
        int count = Mathf.Min(shown.Count, 8);
        while (rows.Count < count)
        {
            int index = rows.Count;
            var b = UIBuilder.Button("Regiment", section.Body, () => SelectRow(index), out var label, 30, 14);
            UIBuilder.OnHover(b.gameObject, () => setTip?.Invoke(() => RowTip(index)), () => hideTip?.Invoke());
            rows.Add((b, label, 0));
            rebuilt = true;
        }
        for (int i = 0; i < rows.Count; i++)
        {
            bool show = i < count;
            if (rows[i].button.gameObject.activeSelf != show) { rows[i].button.gameObject.SetActive(show); rebuilt = true; }
            if (!show) continue;
            var r = shown[i];
            string text = $"<b>{settlement.RegimentName(r.building)}</b>  {WarCatalog.RankText(Settlement.RegimentRank(r))}" +
                          $"<size=85%><color=#ccc>  {r.kills} kills{(r.drill > 0 ? $" · drill {r.drill}" : "")}</color></size>";
            if (rows[i].label.text != text) rows[i].label.text = text;
            rows[i] = (rows[i].button, rows[i].label, r.building);
        }
        reportsText.transform.SetAsLastSibling();
        var last = war.reports.Count > 0 ? war.reports[war.reports.Count - 1] : null;
        reportsText.text = last == null ? "<size=85%><color=#aaa>No battles yet.</color></size>"
            : $"<size=85%><color=#fc8>Last battle</color> <color=#aaa>({last.world})</color>: {last.Line()}</size>";
        if (rebuilt) fit?.Invoke();
    }

    private string RowTip(int index)
    {
        if (index >= rows.Count) return "";
        var r = settlement.Regiment(rows[index].building, false);
        if (r == null) return "";
        int rank = Settlement.RegimentRank(r);
        string next = rank < WarCatalog.MaxRank ? $"{r.xp:0}/{WarCatalog.RegimentXp[rank + 1]:0} xp to {WarCatalog.RankName(rank + 1)}" : "the highest rank";
        return $"<b>{settlement.RegimentName(r.building)} regiment</b>: {WarCatalog.RankText(rank)}\n{next}\n" +
               $"{r.kills} kills, {r.battles} battles, {r.fallen} fallen\nRecruits join as {WarCatalog.RankName(settlement.StartRank(r.building))}. " +
               $"Drill {r.drill}/{WarCatalog.MaxDrill}.\n<color=#aaa>Click to select the building.</color>";
    }

    private void SelectRow(int index)
    {
        if (controller == null || index >= rows.Count) return;
        int id = rows[index].building;
        foreach (var v in controller.Buildings)
        {
            if (v.State.instanceId != id) continue;
            controller.Select(v);
            controller.FocusOn(controller.Grid.AreaCenter(v.State.cell, v.Definition.size), Camera.main != null ? Camera.main.orthographicSize : 12f);
            return;
        }
    }

    // ---------- Drill (building panel) ----------

    public static void CreateDrill(Transform selectionPanel, FreeModeController controller, Action<Func<string>> setTip, Action hideTip)
    {
        var button = UIBuilder.Button("Drill", selectionPanel, () => DoDrill(controller), out var label, 46);
        button.image.color = new Color(0.33f, 0.22f, 0.2f);
        var r = selectionPanel.gameObject.AddComponent<WarDrillButton>();   // (on the panel: it keeps running while the button is hidden)
        r.controller = controller;
        r.button = button;
        r.label = label;
        UIBuilder.OnHover(button.gameObject, () => setTip?.Invoke(r.Tip), () => hideTip?.Invoke());
        button.gameObject.SetActive(false);
    }

    private static void DoDrill(FreeModeController controller)
    {
        var view = controller != null ? controller.Selected : null;
        if (view == null) return;
        controller.Settlement.Drill(view.State);
    }

    // ---------- Under a selected troop ----------

    public static void CreateUnitBlock(Transform extras, FreeModeController controller, Action<Func<string>> setTip, Action hideTip)
    {
        if (extras == null) return;
        var rt = UIBuilder.Rect("War", extras);
        var layout = rt.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 4;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        rt.SetSiblingIndex(0);
        var block = rt.gameObject.AddComponent<WarUnitBlock>();
        block.controller = controller;
        block.info = UIBuilder.Text("Info", rt, "", 15);
        block.info.raycastTarget = true;
        UIBuilder.OnHover(block.info.gameObject, () => setTip?.Invoke(block.Tip), () => hideTip?.Invoke());
        block.command = UIBuilder.Button("Command", rt, block.ToggleCommand, out block.commandLabel, 34, 14);
        block.command.image.color = new Color(0.42f, 0.24f, 0.18f);
        UIBuilder.OnHover(block.command.gameObject, () => setTip?.Invoke(block.CommandTip), () => hideTip?.Invoke());
    }
}

/// <summary>The Drill button in the building panel: shows itself for training buildings.</summary>
public class WarDrillButton : MonoBehaviour
{
    public FreeModeController controller;
    public Button button;
    public TextMeshProUGUI label;
    private float refreshAt;

    private void OnEnable() => refreshAt = 0f;

    public void Refresh()
    {
        var view = controller != null && controller.Selection.Count == 1 ? controller.Selected : null;
        var s = controller != null ? controller.Settlement : null;
        bool show = view != null && s != null && view.Definition.trainsUnit != null && view.State.IsBuilt;
        if (button.gameObject.activeSelf != show) button.gameObject.SetActive(show);
        if (!show) return;
        var r = s.Regiment(view.State.instanceId, false);
        int level = r != null ? r.drill : 0;
        if (level >= WarCatalog.MaxDrill)
        {
            button.interactable = false;
            label.text = $"<b>Fully drilled</b>  <size=75%>({WarCatalog.DrillNames[WarCatalog.MaxDrill - 1]})</size>";
            return;
        }
        bool can = s.CanDrill(view.State, out string why);
        button.interactable = can;
        string cost = string.Join(", ", s.DrillCost(view.State).Where(c => c.resource != null).Select(c => $"{c.amount} {c.resource.displayName}"));
        label.text = $"<b>Drill: {WarCatalog.DrillNames[level]}</b>  <size=75%>({level + 1}/{WarCatalog.MaxDrill})</size>\n" +
                     $"<size=80%>{(can ? cost : $"<color=#f88>{why}</color> · {cost}")}</size>";
    }

    public string Tip()
    {
        var view = controller != null ? controller.Selected : null;
        var s = controller != null ? controller.Settlement : null;
        if (view == null || s == null) return "";
        var r = s.Regiment(view.State.instanceId, false);
        int level = r != null ? r.drill : 0;
        return "<b>Drill</b>\nEach level: troops trained here learn 10% faster, have +4 morale, and recruits may join one rank higher " +
               "(as high as the regiment itself has risen: Regular with no drill, up to Elite).\n" +
               $"Drill {level}/{WarCatalog.MaxDrill}. Regiment: {WarCatalog.RankText(Settlement.RegimentRank(r))}.";
    }

    // The button lives under a panel that refreshes a few times a second; keep in step with it.
    private void Update()
    {
        if (Time.unscaledTime < refreshAt) return;
        refreshAt = Time.unscaledTime + 0.25f;
        Refresh();
    }
}

/// <summary>Rank and morale for one selected troop, and command for heroes.</summary>
public class WarUnitBlock : MonoBehaviour
{
    public FreeModeController controller;
    public TextMeshProUGUI info, commandLabel;
    public Button command;
    private float refreshAt;

    private Troop Troop => controller != null && controller.SelectedUnits.Count == 1 ? controller.SelectedUnits[0] : null;

    private void Update()
    {
        if (Time.unscaledTime < refreshAt) return;
        refreshAt = Time.unscaledTime + 0.25f;
        var t = Troop;
        var s = controller != null ? controller.Settlement : null;
        if (t == null || s == null) return;
        var w = t.War;
        var sb = new StringBuilder("<size=95%>");
        if (t.Hero == null)
        {
            int rank = w != null ? w.rank : 0;
            string next = rank < WarCatalog.MaxRank ? $" <color=#aaa>{(w != null ? w.xp : 0):0}/{WarCatalog.TroopXp[rank + 1]:0} xp</color>" : "";
            sb.Append($"Rank: {WarCatalog.RankText(rank)}{next}{(w != null && w.kills > 0 ? $" · {w.kills} kills" : "")}\n");
        }
        float morale = w != null && w.morale >= 0f ? w.morale : WarCatalog.BaseMorale;
        sb.Append($"Morale: {WarCatalog.MoraleName(morale, w != null && w.routing)} <color=#aaa>{morale:0}</color>");
        if (w != null && w.inAura) sb.Append(" · <color=#fd6>by the commander</color>");
        if (t is SiegeTroop st) sb.Append($"\n<color=#fc8>Siege engine</color>: {st.structureMultiplier:0}x damage to camps{(st.ramOnly ? "; doesn't fight troops" : "")}");
        bool isCommander = t.Hero != null && s.IsCommander(t.Hero);
        if (t.Hero != null)
        {
            var rec = s.WarHero(t.Hero.id, false);
            if (isCommander) sb.Append("\n<color=#fd6>Commands the army</color>");
            if (rec != null && rec.traits.Count > 0)
                sb.Append("\n<size=90%>" + string.Join(", ", rec.traits.Select(id => WarCatalog.Trait(id)?.name).Where(n => n != null)) + "</size>");
        }
        sb.Append("</size>");
        info.text = sb.ToString();
        command.gameObject.SetActive(t.Hero != null);
        if (t.Hero != null)
            commandLabel.text = isCommander ? "Relieve of command" : "<b>Appoint as commander</b>";
    }

    public void ToggleCommand()
    {
        var t = Troop;
        var s = controller != null ? controller.Settlement : null;
        if (t == null || t.Hero == null || s == null) return;
        if (s.IsCommander(t.Hero)) s.RelieveCommander(t.Hero);
        else s.AppointCommander(t.Hero);
        refreshAt = 0f;
    }

    public string Tip()
    {
        var t = Troop;
        if (t == null) return "";
        return "<b>Rank and morale</b>\nKills and battles survived raise a troop's rank (tougher, harder-hitting, steadier). " +
               "Morale falls when friends die nearby, when outnumbered or shelled, and when a chief roars; it rises near heroes, the " +
               "commander's banner and at home. Below 35 a troop wavers (hits softer); broken, it runs for a few seconds, then rallies.";
    }

    public string CommandTip()
    {
        var t = Troop;
        var s = controller != null ? controller.Settlement : null;
        if (t == null || t.Hero == null || s == null) return "";
        var rec = s.WarHero(t.Hero.id, false);
        var sb = new StringBuilder("<b>Commander</b>\nOne hero commands the army on each world. Troops within about " +
                                   $"{WarCatalog.CommandRadius:0} tiles of the banner get +5% attack and +12 morale, and the whole army +4 morale. " +
                                   "Over battles the commander earns command traits " +
                                   $"({(rec != null ? rec.traits.Count : 0)}/{s.WarTraitSlots}; more places in later ages).");
        if (rec != null)
            foreach (var id in rec.traits)
            {
                var tr = WarCatalog.Trait(id);
                if (tr != null) sb.Append($"\n<color=#fd6>{tr.name}</color>: {tr.text}");
            }
        string next = s.WarNextTraits(t.Hero);
        if (next.Length > 0) sb.Append("\n<b>Working towards</b>" + next);
        return sb.ToString();
    }
}
