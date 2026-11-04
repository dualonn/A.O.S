using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>One good's price on the market: a factor on its base value, where it's heading, and its recent history.</summary>
[Serializable]
public class XPrice
{
    public string id;
    public float factor = 1f;
    [Tooltip("Where the price is drifting to (1 normally; higher in a boom, lower in a bust).")]
    public float target = 1f;
    [Tooltip("Play time when a boom or bust ends.")]
    public float shockUntil;
    [Tooltip("Recent prices (factors), oldest first.")]
    public List<float> history = new List<float>();
}

/// <summary>The market's saved state (prices are shared by all your worlds: traders go everywhere).</summary>
[Serializable]
public class XMarketState
{
    public List<XPrice> prices = new List<XPrice>();
    [Tooltip("Seconds until prices next move, and until the next boom or bust.")]
    public float driftIn = 5f, shockIn = 360f;
    [Tooltip("The latest market news.")]
    public string news;
}

/// <summary>
/// The market: sell goods for Coin and buy them back, at prices that drift. Selling a lot of
/// something floods the market (its price falls); buying a lot makes it dear. Now and then a good
/// booms or busts for a few minutes, and the news comes to you. Trading needs Coin and a building
/// that makes it (a Treasury or one of its later ranks) on the world you're on.
/// </summary>
public partial class Settlement
{
    public const int XMarketHistory = 60;
    public const float XMarketDriftSeconds = 15f;
    public const float XSellShare = 0.85f, XBuyShare = 1.15f;

    private XMarketState XMarket
    {
        get
        {
            var rec = XRec;
            if (rec.market == null) rec.market = new XMarketState();
            return rec.market;
        }
    }

    /// <summary>What a good is worth in Coin, before the market moves it (rarer, later goods are dearer).</summary>
    public static float XBaseValue(ResourceType r)
    {
        if (r == null) return 0f;
        switch (r.id)
        {
            case "Food": return 0.5f;
            case "Wood": return 0.6f;
            case "Stone": return 0.7f;
            case "Gold": return 2.5f;
            case "Essence": return 3f;
        }
        int order = r.unlockedIn != null ? r.unlockedIn.order : 0;
        return Mathf.Round(0.8f * Mathf.Pow(1.45f, order) * 10f) / 10f;
    }

    /// <summary>Goods the market deals in now: everything you've reached except Coin itself.</summary>
    public List<ResourceType> XMarketGoods()
    {
        var list = new List<ResourceType>();
        foreach (var r in Database.resources)
        {
            if (r == null || r == Database.coin) continue;
            if (r.unlockedIn != null && r.unlockedIn.order > State.ageIndex) continue;
            list.Add(r);
        }
        return list;
    }

    public XPrice XPriceOf(ResourceType r)
    {
        var m = XMarket;
        var p = m.prices.Find(x => x.id == r.id);
        if (p == null)
        {
            p = new XPrice { id = r.id };
            m.prices.Add(p);
        }
        return p;
    }

    /// <summary>Coin for one unit, selling and buying.</summary>
    public float XSellPrice(ResourceType r) => XBaseValue(r) * XPriceOf(r).factor * XSellShare * CivicsSellFactor;
    public float XBuyPrice(ResourceType r) => XBaseValue(r) * XPriceOf(r).factor * XBuyShare * CivicsBuyFactor;

    /// <summary>A size of trade that moves the price a few percent (bigger in later ages).</summary>
    public float XMarketLot => 100f * (1f + State.ageIndex);

    /// <summary>The building on this world that lets you trade (one making Coin), if any.</summary>
    public BuildingDefinition XMarketBuilding(out bool built)
    {
        built = false;
        var coin = Database.coin;
        BuildingDefinition first = null;
        if (coin == null) return null;
        foreach (var b in State.buildings)
        {
            var def = Database.GetBuilding(b.definitionId);
            if (def == null || !b.IsBuilt || b.health <= 0) continue;
            if (def.production.Exists(p => p.resource == coin)) { built = true; return def; }
        }
        foreach (var def in Database.buildings)
        {
            if (def == null || !def.production.Exists(p => p.resource == coin)) continue;
            if (first == null || (def.age != null && first.age != null && def.age.order < first.age.order)) first = def;
        }
        return first;
    }

    /// <summary>Can you trade here? 'why' says what's missing.</summary>
    public bool XCanTrade(out string why)
    {
        why = null;
        var coin = Database.coin;
        if (coin == null) { why = "There's no coin in this game yet."; return false; }
        if (coin.unlockedIn != null && coin.unlockedIn.order > State.ageIndex) { why = $"Coin comes in the {coin.unlockedIn.displayName}."; return false; }
        var b = XMarketBuilding(out bool built);
        if (!built) { why = $"Build a {(b != null ? b.displayName : "treasury")} on this world to trade here."; return false; }
        return true;
    }

    /// <summary>Is a good booming (dear), or in a bust (cheap)?</summary>
    public int XShock(ResourceType r)
    {
        var p = XPriceOf(r);
        if (State.playTime >= p.shockUntil) return 0;
        return p.target > 1.05f ? 1 : p.target < 0.95f ? -1 : 0;
    }

    /// <summary>Sell 'amount' of a good for Coin. Returns what happened (or why not).</summary>
    public bool XSell(ResourceType r, float amount, out string result)
    {
        result = null;
        if (!XCanTrade(out result)) return false;
        amount = Mathf.Floor(Mathf.Min(amount, GetAmount(r)));
        if (amount < 1f) { result = $"You have no {r.displayName} to sell."; return false; }
        float coin = Mathf.Floor(amount * XBaseValue(r) * XTradeFactor(r, -amount) * XSellShare * CivicsSellFactor);
        if (coin < 1f) { result = "Not worth anything at that price."; return false; }
        bool boom = XShock(r) > 0;
        AddResource(r, -amount);
        AddResource(Database.coin, coin);
        XMovePrice(r, -amount);
        var rec = XRec;
        rec.marketTrades++;
        rec.marketCoinEarned += coin;
        if (boom) rec.soldInBoom = true;
        result = $"Sold {amount:0} {r.displayName} for {coin:0} {Database.coin.displayName}.";
        GameAudio.UI("market_trade");
        return true;
    }

    /// <summary>Buy 'amount' of a good with Coin (as much as you can afford, up to that).</summary>
    public bool XBuy(ResourceType r, float amount, out string result)
    {
        result = null;
        if (!XCanTrade(out result)) return false;
        float each = XBuyPrice(r);
        float have = GetAmount(Database.coin);
        amount = Mathf.Floor(Mathf.Min(amount, have / Mathf.Max(0.0001f, each)));
        amount = XAffordable(r, amount, have);
        if (amount < 1f) { result = $"Not enough {Database.coin.displayName}."; return false; }
        float coin = Mathf.Ceil(XBuyCost(r, amount));
        AddResource(Database.coin, -coin);
        AddResource(r, amount);
        XMovePrice(r, amount);
        var rec = XRec;
        rec.marketTrades++;
        rec.marketCoinSpent += coin;
        result = $"Bought {amount:0} {r.displayName} for {coin:0} {Database.coin.displayName}.";
        GameAudio.UI("market_trade");
        return true;
    }

    /// <summary>Supply and demand: selling pushes the price down, buying pushes it up.</summary>
    private void XMovePrice(ResourceType r, float signedAmount)
    {
        var p = XPriceOf(r);
        p.factor = Mathf.Clamp(p.factor * Mathf.Exp(0.05f * signedAmount / XMarketLot), 0.2f, 5f);
    }

    /// <summary>Prices drift back towards their target, with a little noise; now and then a boom or a bust.</summary>
    private void XTickMarket(float dt)
    {
        var m = XMarket;
        m.driftIn -= dt;
        if (m.driftIn <= 0f)
        {
            m.driftIn = XMarketDriftSeconds;
            float now = State.playTime;
            foreach (var r in XMarketGoods())
            {
                var p = XPriceOf(r);
                if (now >= p.shockUntil) p.target = 1f;
                float noise = (Random.value - 0.5f) * 0.06f;
                p.factor = Mathf.Clamp(p.factor + (p.target - p.factor) * 0.12f + noise * p.factor, 0.2f, 5f);
                p.history.Add(p.factor);
                if (p.history.Count > XMarketHistory) p.history.RemoveRange(0, p.history.Count - XMarketHistory);
            }
        }
        m.shockIn -= dt;
        if (m.shockIn <= 0f)
        {
            m.shockIn = Random.Range(240f, 480f);
            var goods = XMarketGoods();
            if (goods.Count == 0) return;
            var r = goods[Random.Range(0, goods.Count)];
            var p = XPriceOf(r);
            bool boom = Random.value < 0.5f;
            p.target = boom ? Random.Range(1.6f, 2.2f) : Random.Range(0.4f, 0.6f);
            p.shockUntil = State.playTime + Random.Range(180f, 360f);
            m.news = boom
                ? $"<color=#9f9>Boom!</color> {r.displayName} is wanted everywhere: its price is climbing."
                : $"<color=#f99>Bust!</color> The market is flooded with {r.displayName}: its price is falling.";
            // Only news to you once you can trade somewhere.
            if (State.worlds.Exists(w => XWorldCanTrade(w)))
            {
                Notify($"<b>Market:</b> {m.news}");
                GameAudio.UI("market_shock");
            }
        }
    }

    private bool XWorldCanTrade(WorldState w)
    {
        var coin = Database.coin;
        if (coin == null) return false;
        foreach (var b in w.buildings)
        {
            if (!b.IsBuilt || b.health <= 0) continue;
            var def = Database.GetBuilding(b.definitionId);
            if (def != null && def.production.Exists(p => p.resource == coin)) return true;
        }
        return false;
    }

    /// <summary>The market's latest news (for the panel).</summary>
    public string XMarketNews => XMarket.news;
}
