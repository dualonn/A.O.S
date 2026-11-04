using UnityEngine;

/// <summary>
/// Small helpers behind fixes to other systems (kept here so the patches to shared files stay one-liners):
/// founding checks without allocating, quest spots you can actually walk to, and market trades
/// priced along the way (so a big trade can't buy low and sell high on the same click).
/// </summary>
public partial class Settlement
{
    /// <summary>IsFounded without a closure (it's asked many times a frame).</summary>
    private bool GuideFoundedNow()
    {
        var list = State != null ? State.buildings : null;
        if (list == null) return false;
        for (int i = 0; i < list.Count; i++)
        {
            var def = Database.GetBuilding(list[i].definitionId);
            if (def != null && def.isTownCenter) return true;
        }
        return false;
    }

    /// <summary>Can a hero walk from 'from' to 'to' on the world in view (quests shouldn't send them across the sea)?</summary>
    private bool QuestReachable(Vector2 from, Vector2 to)
    {
        var nav = MapNavigator.Current;
        if (nav == null || offWorld || Map == null) return true;   // can't tell: don't hold the quest up
        nav.FindPath(new Vector3(from.x, from.y, 0), new Vector3(to.x, to.y, 0), Team.Player, out bool reached);
        return reached;
    }

    // ---------- Market: the average price across a trade ----------

    /// <summary>The average price factor over a trade (negative = selling), as the price moves with each part of it.</summary>
    private float XTradeFactor(ResourceType r, float signedAmount)
    {
        float f = XPriceOf(r).factor;
        const int Slices = 16;
        float step = Mathf.Exp(0.05f * signedAmount / Slices / Mathf.Max(1f, XMarketLot));
        float sum = 0f;
        for (int i = 0; i < Slices; i++)
        {
            float next = Mathf.Clamp(f * step, 0.2f, 5f);
            sum += (f + next) * 0.5f;
            f = next;
        }
        return sum / Slices;
    }

    /// <summary>What buying 'amount' costs in Coin, priced along the way.</summary>
    private float XBuyCost(ResourceType r, float amount) => amount * XBaseValue(r) * XTradeFactor(r, amount) * XBuyShare * CivicsBuyFactor;

    /// <summary>The most (up to 'amount', whole units) that 'have' Coin buys.</summary>
    private float XAffordable(ResourceType r, float amount, float have)
    {
        if (amount < 1f || Mathf.Ceil(XBuyCost(r, amount)) <= have) return amount;
        float lo = 0f, hi = amount;
        for (int i = 0; i < 24 && hi - lo > 1f; i++)
        {
            float mid = Mathf.Floor((lo + hi) * 0.5f);
            if (Mathf.Ceil(XBuyCost(r, mid)) <= have) lo = mid; else hi = mid;
        }
        return lo;
    }
}
