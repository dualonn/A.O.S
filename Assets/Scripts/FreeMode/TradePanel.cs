using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Trade routes in the HUD: a folding "Trade" section in the research panel (shown once you have
/// a trading post on this world). One row per route: the towns, the vehicle, trips and coin;
/// hover for the details, click to look at its caravan. A line at the top sums it all up.
/// </summary>
public class TradePanel : MonoBehaviour
{
    private Settlement settlement;
    private FreeModeController controller;
    private Action<Func<string>> setTip;
    private Action hideTip, fit;

    private CollapsibleSection section;
    private TextMeshProUGUI summary;
    private readonly List<(Button button, TextMeshProUGUI label)> rows = new List<(Button, TextMeshProUGUI)>();
    private float refreshAt;
    private bool wasVisible;

    public static TradePanel Create(Transform root, Settlement settlement, FreeModeController controller, Transform list,
        Action<Func<string>> setTip, Action hideTip, Action fit)
    {
        if (list == null) return null;
        var go = new GameObject("TradePanel", typeof(RectTransform));
        go.transform.SetParent(root, false);
        var p = go.AddComponent<TradePanel>();
        p.settlement = settlement;
        p.controller = controller;
        p.setTip = setTip;
        p.hideTip = hideTip;
        p.fit = fit;
        p.section = new CollapsibleSection("Trade", list, "Trade", new Color(0.95f, 0.8f, 0.45f));
        p.section.SetOpen(false);
        p.section.Toggled += _ => fit?.Invoke();
        p.summary = UIBuilder.Text("Summary", p.section.Body, "", 15);
        p.summary.raycastTarget = true;
        UIBuilder.OnHover(p.summary.gameObject, () => setTip?.Invoke(p.Help), () => hideTip?.Invoke());
        p.section.SetVisible(false);
        return p;
    }

    private string Help() =>
        "<b>Trade</b>\nA trading post in two of your towns opens a trade route. Caravans go to and fro along it, and every trip home " +
        "brings coin and goods: more for long routes between big towns, and more on roads. Outposts the route passes " +
        $"near produce {Settlement.TradeOutpostBonus:P0} more.\nRaiders and beasts attack caravans on the road; a lost caravan is " +
        "replaced after a while. Rank posts up with the ages: horse caravans, wagon trains, then steam trains and maglevs " +
        "(which need rail track laid between the stations), and hover freighters.";

    private void Update()
    {
        if (settlement == null || settlement.State == null || Time.unscaledTime < refreshAt) return;
        refreshAt = Time.unscaledTime + 1f;
        var routes = settlement.TradeRoutesHere;
        bool anyPost = routes.Count > 0 || settlement.State.buildings.Exists(b => TradeCatalog.IsPost(settlement.Database.GetBuilding(b.definitionId)));
        section.SetVisible(anyPost);
        if (anyPost != wasVisible) { wasVisible = anyPost; fit?.Invoke(); }
        if (!anyPost) return;

        float perMinute = settlement.TradeIncomePerMinute();
        section.SetNote(routes.Count == 0 ? "no routes" : $"{routes.Count} route{(routes.Count == 1 ? "" : "s")} · ~{perMinute:0}/min");
        if (!section.Open) return;
        var t = settlement.TradeState;
        summary.text = routes.Count == 0
            ? "<size=90%><color=#aaa>Build a trading post in a second town to open a route.</color></size>"
            : $"<size=90%>{t.trips} trips · {t.coin:N0} coin{(t.plundered > 0 ? $" · {t.plundered} plundered" : "")}</size>";

        bool rebuilt = false;
        while (rows.Count < routes.Count)
        {
            int index = rows.Count;
            var b = UIBuilder.Button("Route", section.Body, () => Look(index), out var label, 38, 14);
            UIBuilder.OnHover(b.gameObject, () => setTip?.Invoke(() => index < settlement.TradeRoutesHere.Count
                ? settlement.TradeRouteTooltip(settlement.TradeRoutesHere[index]) : ""), () => hideTip?.Invoke());
            rows.Add((b, label));
            rebuilt = true;
        }
        for (int i = 0; i < rows.Count; i++)
        {
            bool show = i < routes.Count;
            if (rows[i].button.gameObject.activeSelf != show) { rows[i].button.gameObject.SetActive(show); rebuilt = true; }
            if (!show) continue;
            var r = routes[i];
            int tier = settlement.TradeVehicleTier(r);
            string state = r.blocked && tier != TradeCatalog.TierHover ? " <color=#f88>cut off</color>"
                : settlement.State.playTime < r.lostUntil ? " <color=#fb7>plundered</color>" : "";
            string text = $"<b>{settlement.TradeRouteName(r)}</b>{state}\n<size=85%><color=#ccc>{TradeCatalog.VehicleNames[tier]} · {r.trips} trips · {r.coin:N0} coin</color></size>";
            if (rows[i].label.text != text) rows[i].label.text = text;
        }
        if (rebuilt) fit?.Invoke();
    }

    /// <summary>Look at a route's caravan (or the middle of the route if none is out).</summary>
    private void Look(int index)
    {
        var routes = settlement.TradeRoutesHere;
        if (controller == null || index >= routes.Count) return;
        var r = routes[index];
        var caravan = TradeWorks.Instance != null ? TradeWorks.Instance.CaravanOf(r) : null;
        if (caravan != null) { controller.FocusOn(caravan.transform.position); return; }
        var pa = settlement.TradePost(r.a); var pb = settlement.TradePost(r.b);
        if (pa != null && pb != null) controller.FocusOn((settlement.TradePostDoor(pa) + settlement.TradePostDoor(pb)) * 0.5f, 30f);
    }
}
