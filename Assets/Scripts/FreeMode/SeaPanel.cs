using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The sea in the HUD: a folding "Sea Trade" section right below Trade in the research panel
/// (shown once this world has a fishing building, port or anything else of the sea's). A summary
/// line (fishing grounds, what's swimming about), then one row per sea route: the towns, the ship,
/// trips and coin; hover for details, click to look at its ship. A monster on the loose gets a row
/// of its own. It also starts the scene's SeaWorks (boats, ships, shoals and creatures).
/// </summary>
public class SeaPanel : MonoBehaviour
{
    private Settlement settlement;
    private FreeModeController controller;
    private Action<Func<string>> setTip;
    private Action hideTip, fit;

    private CollapsibleSection section;
    private TextMeshProUGUI summary;
    private Button monsterButton;
    private TextMeshProUGUI monsterLabel;
    private readonly List<(Button button, TextMeshProUGUI label)> rows = new List<(Button, TextMeshProUGUI)>();
    private float refreshAt;
    private bool wasVisible;

    public static SeaPanel Create(Transform root, Settlement settlement, FreeModeController controller, Transform list,
        Action<Func<string>> setTip, Action hideTip, Action fit)
    {
        if (list == null || settlement == null) return null;
        SeaWorks.Create(controller);
        var go = new GameObject("SeaPanel", typeof(RectTransform));
        go.transform.SetParent(root, false);
        var p = go.AddComponent<SeaPanel>();
        p.settlement = settlement;
        p.controller = controller;
        p.setTip = setTip;
        p.hideTip = hideTip;
        p.fit = fit;
        p.section = new CollapsibleSection("SeaTrade", list, "Sea Trade", new Color(0.45f, 0.78f, 0.95f));
        p.section.SetOpen(false);
        p.section.Toggled += _ => fit?.Invoke();
        p.summary = UIBuilder.Text("Summary", p.section.Body, "", 15);
        p.summary.raycastTarget = true;
        UIBuilder.OnHover(p.summary.gameObject, () => setTip?.Invoke(p.Help), () => hideTip?.Invoke());
        p.monsterButton = UIBuilder.Button("Monster", p.section.Body, p.LookAtMonster, out p.monsterLabel, 30, 14);
        UIBuilder.OnHover(p.monsterButton.gameObject, () => setTip?.Invoke(p.MonsterTip), () => hideTip?.Invoke());
        p.monsterButton.gameObject.SetActive(false);
        p.section.SetVisible(false);
        return p;
    }

    private string Help()
    {
        var sea = settlement.Sea;
        return "<b>The sea</b>\nFish shoals glint on the water: fishing buildings send their boats out to those in reach, and catch more " +
               "the richer they are. Shoals fished hard thin out and move on, and regrow when left alone; new ones turn up as you explore. " +
               "Oyster beds by the shore feed pearl divers.\n" +
               "Whales roam the open sea for whalers; sea serpents sink fishing boats and harry ships, and now and then a monster " +
               "rises from the deep: sink it with warships for a hoard.\n" +
               "A port in two of your towns on the same water opens a sea route for merchant ships. Pirates prey on them from the " +
               "Medieval Age: lighthouses and warships keep them off.\n" +
               $"<size=85%><color=#aaa>This world: {sea.shoals.Count} fishing grounds, {sea.fishCaught:N0} fish landed, {sea.whalesTaken} whales taken, " +
               $"{sea.boatsLost} boats lost, {sea.piratesSunk} pirates sunk, {sea.monstersSlain} monsters slain.</color></size>";
    }

    private void Update()
    {
        if (settlement == null || settlement.State == null || Time.unscaledTime < refreshAt) return;
        refreshAt = Time.unscaledTime + 1f;
        var sea = settlement.Sea;
        var routes = settlement.SeaRoutesHere;
        bool any = routes.Count > 0 || settlement.State.buildings.Exists(b => SeaCatalog.TryGet(settlement.Database.GetBuilding(b.definitionId), out _));
        section.SetVisible(any);
        if (any != wasVisible) { wasVisible = any; fit?.Invoke(); }
        if (!any) return;

        float perMinute = settlement.SeaIncomePerMinute();
        string life = settlement.SeaLifeSummary(out var monster);
        section.SetNote(routes.Count == 0
            ? $"{sea.shoals.Count} fishing grounds"
            : $"{routes.Count} route{(routes.Count == 1 ? "" : "s")} · ~{perMinute:0}/min");
        bool rebuilt = false;
        bool showMonster = monster != null;
        if (monsterButton.gameObject.activeSelf != showMonster) { monsterButton.gameObject.SetActive(showMonster); rebuilt = true; }
        if (showMonster)
        {
            var sp = SeaCatalog.SpeciesOf(monster.species);
            monsterLabel.text = $"<color=#f88><b>{sp.name}</b></color> <size=85%>haunts the sea · {monster.life:P0} life</size>";
        }
        if (!section.Open) { if (rebuilt) fit?.Invoke(); return; }

        int rich = 0;
        foreach (var s in sea.shoals) if (s.fish >= s.max * 0.6f) rich++;
        string lifeLine = life.Length > 0 ? $"\n<color=#ccc>At sea: {life}</color>" : "";
        summary.text = routes.Count == 0
            ? $"<size=90%>{sea.shoals.Count} fishing grounds ({rich} rich){lifeLine}\n<color=#aaa>Build a port in two towns on the same water to open a sea route.</color></size>"
            : $"<size=90%>{sea.shoals.Count} fishing grounds ({rich} rich) · {sea.trips} voyages · {sea.coin:N0} coin{(sea.plundered > 0 ? $" · {sea.plundered} lost" : "")}{lifeLine}</size>";

        while (rows.Count < routes.Count)
        {
            int index = rows.Count;
            var b = UIBuilder.Button("SeaRoute", section.Body, () => Look(index), out var label, 38, 14);
            UIBuilder.OnHover(b.gameObject, () => setTip?.Invoke(() => index < settlement.SeaRoutesHere.Count
                ? settlement.SeaRouteTooltip(settlement.SeaRoutesHere[index]) : ""), () => hideTip?.Invoke());
            rows.Add((b, label));
            rebuilt = true;
        }
        for (int i = 0; i < rows.Count; i++)
        {
            bool show = i < routes.Count;
            if (rows[i].button.gameObject.activeSelf != show) { rows[i].button.gameObject.SetActive(show); rebuilt = true; }
            if (!show) continue;
            var r = routes[i];
            string state = !r.pathKnown ? " <color=#aaa>charting</color>" : r.blocked ? " <color=#f88>cut off</color>"
                : settlement.State.playTime < r.lostUntil ? " <color=#fb7>ship lost</color>" : "";
            string text = $"<b>{settlement.SeaRouteName(r)}</b>{state}\n<size=85%><color=#ccc>{SeaCatalog.ShipNames[settlement.SeaShipTier(r)]} · {r.trips} trips · {r.coin:N0} coin</color></size>";
            if (rows[i].label.text != text) rows[i].label.text = text;
        }
        if (rebuilt) fit?.Invoke();
    }

    private string MonsterTip()
    {
        settlement.SeaLifeSummary(out var monster);
        if (monster == null) return "";
        var sp = SeaCatalog.SpeciesOf(monster.species);
        float left = monster.leaveAt - settlement.State.playTime;
        return $"<b>{sp.name}</b>\n{sp.note}\nLife: {monster.life:P0}  ·  goes back to the deep in about {Mathf.Max(0f, left) / 60f:0} min\n" +
               "<size=85%><color=#aaa>Send warships: it fights when they come near. Lighthouse beams burn it. Click to look.</color></size>";
    }

    private void LookAtMonster()
    {
        settlement.SeaLifeSummary(out var monster);
        if (monster == null || controller == null || SeaWorks.Instance == null) return;
        if (SeaWorks.Instance.MonsterPosition(monster, out var at)) controller.FocusOn(at, 14f);
    }

    private void Look(int index)
    {
        var routes = settlement.SeaRoutesHere;
        if (controller == null || index >= routes.Count) return;
        var r = routes[index];
        var ship = SeaWorks.Instance != null ? SeaWorks.Instance.ShipOf(r) : null;
        if (ship != null) { controller.FocusOn(ship.transform.position); return; }
        var pa = settlement.TradePost(r.a); var pb = settlement.TradePost(r.b);
        if (pa != null && pb != null) controller.FocusOn(((Vector3)(Vector2)pa.cell + (Vector3)(Vector2)pb.cell) * 0.5f, 30f);
    }
}
