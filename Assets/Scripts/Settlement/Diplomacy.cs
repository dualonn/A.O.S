using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Where you stand with the people of one world (saved per world).</summary>
[Serializable]
public class DiplomacyState
{
    [Tooltip("Set the first time anyone looks: standing then starts from the people's temperament.")]
    public bool met;
    [Tooltip("-100 (out for your blood) to 100 (sworn friends).")]
    public float standing;
    public bool peace, trade, alliance, war;
    [Tooltip("Raids on this world stop until this play time (tribute paid, or bandits bought off).")]
    public float tributeUntil;
    public float nextGiftAt, nextWarPartyAt, nextGoodsAt;
}

/// <summary>
/// Diplomacy with the native peoples of other worlds: gifts, tribute, peace, trade and
/// alliance, all from one standing per world. At home the raider camps are bandits: you can
/// buy them off for a while, but they never make peace.
/// </summary>
public partial class Settlement
{
    /// <summary>What one native people are like and what they trade.</summary>
    public class NativeFaction
    {
        public string id, name;
        public float temperament;        // where standing starts
        public string wants, offers;     // resource ids: what they value as gifts, what they trade
        public string goods, goodsName;  // their signature craft (an item template id)
    }

    public static readonly NativeFaction[] Factions =
    {
        new NativeFaction { id = "Sandfolk", name = "Sandfolk", temperament = -10, wants = "Food", offers = "Gold", goods = "SandglassCharm", goodsName = "Sandglass Charm" },
        new NativeFaction { id = "Frostkin", name = "Frostkin", temperament = -35, wants = "Food", offers = "Iron", goods = "FrostfangTotem", goodsName = "Frostfang Totem" },
        new NativeFaction { id = "Bogfolk", name = "Bogfolk", temperament = -15, wants = "Iron", offers = "Mana", goods = "BogWitchFetish", goodsName = "Bog-Witch Fetish" },
        new NativeFaction { id = "Ashborn", name = "Ashborn", temperament = -45, wants = "Wood", offers = "Iron", goods = "EmberHeart", goodsName = "Ember Heart" },
        new NativeFaction { id = "Shardkin", name = "Shardkin", temperament = 0, wants = "Mana", offers = "Gold", goods = "ShardLens", goodsName = "Shard Lens" },
        new NativeFaction { id = "Tidefolk", name = "Tidefolk", temperament = -15, wants = "Wood", offers = "Food", goods = "TidepearlNecklace", goodsName = "Tidepearl Necklace" },
        new NativeFaction { id = "Wildfolk", name = "Wildfolk", temperament = -20, wants = "Iron", offers = "Wood", goods = "WildwoodTalisman", goodsName = "Wildwood Talisman" },
    };

    public enum Standing { Hostile, Wary, Neutral, Friendly, Allied }

    public const float PeaceAt = 0f, TradeAt = 40f, AllianceAt = 75f, PeaceBreaksBelow = -10f;
    public const float TributeSeconds = 480f, GiftCooldown = 45f, WarPartyCooldown = 300f, GoodsCooldown = 600f;

    /// <summary>The people of the world you're on (null at home, where there are only bandits).</summary>
    public NativeFaction NativesHere
    {
        get
        {
            string id = WorldProfile.Get(CurrentWorld.profile).natives;
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var f in Factions) if (f.id == id) return f;
            return null;
        }
    }

    /// <summary>Where you stand with the people here (met on first look).</summary>
    public DiplomacyState Diplomacy
    {
        get
        {
            var w = CurrentWorld;
            if (w.diplomacy == null) w.diplomacy = new DiplomacyState();
            if (!w.diplomacy.met)
            {
                w.diplomacy.met = true;
                var f = NativesHere;
                w.diplomacy.standing = f != null ? f.temperament : -60f;
            }
            return w.diplomacy;
        }
    }

    public Standing StandingHere
    {
        get
        {
            var d = Diplomacy;
            if (d.alliance) return Standing.Allied;
            return d.standing < -40 ? Standing.Hostile : d.standing < PeaceAt ? Standing.Wary : d.standing < TradeAt ? Standing.Neutral : Standing.Friendly;
        }
    }

    public static string StandingName(Standing s) => s.ToString();

    public static Color StandingColour(Standing s) => s switch
    {
        Standing.Hostile => new Color(1f, 0.4f, 0.35f),
        Standing.Wary => new Color(1f, 0.72f, 0.35f),
        Standing.Neutral => new Color(0.85f, 0.85f, 0.8f),
        Standing.Friendly => new Color(0.55f, 0.95f, 0.55f),
        _ => new Color(0.5f, 0.85f, 1f),
    };

    /// <summary>Peace with the people here: their camps leave your troops alone, and they don't raid.</summary>
    public bool NativesAtPeace
    {
        get
        {
            // (cached per frame and per world: a background world asks too, with its own state swapped in)
            if (Time.frameCount == peaceFrame && State != null && peaceWorld == State.currentWorld) return peaceCached;
            peaceFrame = Time.frameCount;
            peaceWorld = State != null ? State.currentWorld : -1;
            peaceCached = State != null && NativesHere != null && Diplomacy.peace;
            return peaceCached;
        }
    }
    private int peaceFrame = -1;
    private int peaceWorld = -1;
    private bool peaceCached;

    /// <summary>Tribute paid (or bandits bought off): no raids here for a while.</summary>
    public bool RaidsPaused => State != null && CurrentWorld.diplomacy != null && State.playTime < CurrentWorld.diplomacy.tributeUntil;

    /// <summary>How hard raids hit here, by how the people here feel about you (1 at home).</summary>
    public float DiplomacyThreatFactor
    {
        get
        {
            if (NativesHere == null) return 1f;
            var d = Diplomacy;
            if (d.war) return 1.2f;
            return StandingHere switch
            {
                Standing.Hostile => 1.05f,
                Standing.Wary => 1f,
                Standing.Neutral => d.peace ? 0.75f : 0.9f,
                Standing.Friendly => d.peace ? 0.75f : 0.85f,
                _ => 0.65f,
            };
        }
    }

    /// <summary>Allied warriors who'd come to defend you (counts when raids here are settled unseen).</summary>
    public float AllyDefencePower()
    {
        if (NativesHere == null || !Diplomacy.alliance) return 0f;
        float total = 0f;
        foreach (var u in NativeUnits()) total += UnitPower(u);
        return total;   // about one of each of their fighters
    }

    public bool OffWorld => offWorld;

    /// <summary>Costs grow with the age, so gifts stay a real choice.</summary>
    private float DiploScale => 1f + 0.6f * State.ageIndex;

    private int Scaled(float baseAmount) => Mathf.RoundToInt(baseAmount * DiploScale / 10f) * 10;

    private ResourceType Res(string id) => Database.GetResource(id);

    public List<ResourceAmount> GiftCost()
    {
        var f = NativesHere;
        return f == null ? new List<ResourceAmount>() : new List<ResourceAmount> { new ResourceAmount { resource = Res(f.wants), amount = Scaled(100) } };
    }

    public List<ResourceAmount> TributeCost() => new List<ResourceAmount>
    {
        new ResourceAmount { resource = Res("Coin"), amount = Scaled(NativesHere == null ? 160 : 120) },
        new ResourceAmount { resource = Res("Food"), amount = Scaled(60) },
    };

    public List<ResourceAmount> PeaceCost() => new List<ResourceAmount> { new ResourceAmount { resource = Res("Coin"), amount = Scaled(100) } };

    public List<ResourceAmount> AllianceCost()
    {
        var f = NativesHere;
        return new List<ResourceAmount>
        {
            new ResourceAmount { resource = Res(f != null ? f.wants : "Food"), amount = Scaled(300) },
            new ResourceAmount { resource = Res("Coin"), amount = Scaled(200) },
        };
    }

    public List<ResourceAmount> WarPartyCost() => new List<ResourceAmount> { new ResourceAmount { resource = Res("Food"), amount = Scaled(150) } };

    public List<ResourceAmount> GoodsCost()
    {
        var f = NativesHere;
        return new List<ResourceAmount>
        {
            new ResourceAmount { resource = Res("Coin"), amount = Scaled(f != null && Diplomacy.alliance ? 220 : 300) },
            new ResourceAmount { resource = Res(f != null ? f.wants : "Food"), amount = Scaled(80) },
        };
    }

    /// <summary>A trade at their stall: 100 of what they want for this many of what they offer (better as they like you more).</summary>
    public (ResourceAmount give, ResourceAmount get, bool ok) TradeOffer()
    {
        var f = NativesHere;
        if (f == null) return (default, default, false);
        float rate = 0.35f + 0.004f * Mathf.Max(0, Diplomacy.standing) + (Diplomacy.alliance ? 0.1f : 0f);
        int give = Scaled(100);
        return (new ResourceAmount { resource = Res(f.wants), amount = give },
                new ResourceAmount { resource = Res(f.offers), amount = Mathf.Max(1, Mathf.RoundToInt(give * rate)) }, true);
    }

    private bool PayNatives(List<ResourceAmount> cost, out string why)
    {
        why = null;
        foreach (var c in cost)
            if (c.resource == null) { why = "Not available here."; return false; }
        if (!CanAfford(cost)) { why = "Not enough: " + CostText(cost) + "."; return false; }
        Spend(cost);
        return true;
    }

    public static string CostText(List<ResourceAmount> cost)
    {
        var parts = new List<string>();
        foreach (var c in cost) if (c.resource != null) parts.Add($"{c.amount:0} {c.resource.displayName}");
        return string.Join(", ", parts);
    }

    /// <summary>Change standing here (a Xenologist on this world makes good will go further).</summary>
    private void ChangeStanding(float amount)
    {
        var d = Diplomacy;
        if (amount > 0) amount *= XenoRewardFactor;
        d.standing = Mathf.Clamp(d.standing + amount, -100f, 100f);
        if (d.peace && d.standing < PeaceBreaksBelow) EndPeace($"The {NativesHere?.name ?? "natives"} have had enough of you: the peace is over.");
        if (d.alliance && d.standing < TradeAt) { d.alliance = false; Notify($"The {NativesHere?.name} have left your alliance."); }
    }

    private void EndPeace(string why)
    {
        var d = Diplomacy;
        d.peace = d.trade = d.alliance = false;
        Notify(why);
    }

    // ---------- Actions (from the diplomacy panel) ----------

    public bool SendGift(out string why)
    {
        var d = Diplomacy;
        if (NativesHere == null) { why = "There's nobody here to give gifts to."; return false; }
        if (State.playTime < d.nextGiftAt) { why = $"They're still talking about your last gift (again in {d.nextGiftAt - State.playTime:0}s)."; return false; }
        if (!PayNatives(GiftCost(), out why)) return false;
        // The warmer they are already, the less one more gift moves them.
        ChangeStanding(8f * (1f - Mathf.Max(0, d.standing) / 150f));
        d.nextGiftAt = State.playTime + GiftCooldown;
        Notify($"The {NativesHere.name} accept your gift.");
        GameAudio.UI("diplomacy");
        return true;
    }

    public bool PayTribute(out string why)
    {
        if (!PayNatives(TributeCost(), out why)) return false;
        Diplomacy.tributeUntil = State.playTime + TributeSeconds;
        if (NativesHere != null) ChangeStanding(2f);
        Notify(NativesHere != null
            ? $"Tribute paid: the {NativesHere.name} won't raid you for {TributeSeconds / 60f:0} minutes."
            : $"The bandits take your coin and stay away, for {TributeSeconds / 60f:0} minutes.");
            GameAudio.UI("coins");
        return true;
    }

    public bool OfferPeace(out string why)
    {
        var d = Diplomacy;
        if (NativesHere == null) { why = "Bandits don't make peace."; return false; }
        if (d.peace) { why = "You're already at peace."; return false; }
        if (d.standing < PeaceAt) { why = "They don't trust you enough yet: send gifts first."; return false; }
        if (!PayNatives(PeaceCost(), out why)) return false;
        d.peace = true;
        d.war = false;
        ChangeStanding(5f);
        Notify($"Peace with the {NativesHere.name}! Their camps will leave your troops alone, and they won't raid you.");
        GameAudio.UI("peace");
        return true;
    }

    public bool OpenTrade(out string why)
    {
        var d = Diplomacy;
        if (NativesHere == null) { why = "Bandits don't trade."; return false; }
        if (!d.peace || d.standing < TradeAt) { why = "They'll trade with friends at peace."; return false; }
        d.trade = true;
        why = null;
        Notify($"The {NativesHere.name} open their stalls to you.");
        GameAudio.UI("diplomacy");
        return true;
    }

    public bool Trade(out string why)
    {
        why = null;
        var (give, get, ok) = TradeOffer();
        if (!ok || !Diplomacy.trade) { why = "No trade open."; return false; }
        if (get.resource == null) { why = "Not available here."; return false; }
        if (!PayNatives(new List<ResourceAmount> { give }, out why)) return false;
        AddResource(get.resource, get.amount);
        ChangeStanding(0.5f);
        GameAudio.UI("market_trade");
        return true;
    }

    public bool BuyGoods(out string why)
    {
        var f = NativesHere;
        var d = Diplomacy;
        why = null;
        if (f == null || !d.trade) { why = "No trade open."; return false; }
        if (State.playTime < d.nextGoodsAt) { why = $"Their crafters are still at work (ready in {(d.nextGoodsAt - State.playTime) / 60f:0.0} min)."; return false; }
        var template = Database.itemTemplates.Find(t => t != null && t.id == f.goods);
        if (template == null) { why = "Their goods aren't set up (run Setup Diplomacy)."; return false; }
        if (!PayNatives(GoodsCost(), out why)) return false;
        var rng = new System.Random(State.mapSeed ^ (State.nextItemId * 7919) ^ (int)(State.playTime * 10));
        var item = Items.Roll(new List<ItemTemplate> { template }, Progress, 0.2f, d.alliance ? 0.6f : 0.35f, State.nextItemId, rng,
            d.alliance ? Rarity.Rare : Rarity.Uncommon, template.xeno);
        if (item == null) { why = "They had nothing to sell."; return false; }
        State.nextItemId++;
        State.inventory.Add(item);
        d.nextGoodsAt = State.playTime + GoodsCooldown;
        Notify($"Bought <color=#{Items.RarityHex(item.rarity)}>{item.name}</color> from the {f.name}.");
        GameAudio.UI("market_trade"); GameAudio.UI("equip");
        return true;
    }

    public bool FormAlliance(out string why)
    {
        var d = Diplomacy;
        if (NativesHere == null) { why = "Bandits never ally."; return false; }
        if (d.alliance) { why = "You're already allies."; return false; }
        if (!d.peace || !d.trade || d.standing < AllianceAt) { why = "They'll ally with close friends who trade with them."; return false; }
        if (!PayNatives(AllianceCost(), out why)) return false;
        d.alliance = true;
        Notify($"Alliance with the {NativesHere.name}! Their warriors will come when you're raided here.");
        GameAudio.UI("peace");
        return true;
    }

    /// <summary>Allies send warriors to stand with you for a while. (The scene spawns them.)</summary>
    public bool AskWarParty(out string why)
    {
        var d = Diplomacy;
        if (NativesHere == null || !d.alliance) { why = "Only allies send war parties."; return false; }
        if (State.playTime < d.nextWarPartyAt) { why = $"Their warriors are resting (again in {(d.nextWarPartyAt - State.playTime) / 60f:0.0} min)."; return false; }
        if (!PayNatives(WarPartyCost(), out why)) return false;
        d.nextWarPartyAt = State.playTime + WarPartyCooldown;
        Notify($"A {NativesHere.name} war party comes to stand with you.");
        GameAudio.UI("hero_recruit");
        return true;
    }

    public void DeclareWar()
    {
        var d = Diplomacy;
        if (NativesHere == null) return;
        d.war = true;
        d.peace = d.trade = d.alliance = false;
        d.standing = Mathf.Min(d.standing, -80f);
        d.tributeUntil = 0f;
        Notify($"War with the {NativesHere.name}! Expect them to raid harder.");
        GameAudio.UI("war_declared");
    }

    // ---------- What you do to them ----------

    /// <summary>One of the people here died (a beguiled or allied one doesn't count).</summary>
    public void NativeKilled(Enemy enemy)
    {
        if (enemy == null || enemy.Charmed || enemy.definition == null || !enemy.definition.isNative || NativesHere == null) return;
        ChangeStanding(Diplomacy.war ? -0.5f : -1.5f);
    }

    /// <summary>You burnt one of their camps.</summary>
    public void NativeCampBurnt()
    {
        if (NativesHere == null) return;
        if (Diplomacy.peace) EndPeace($"You burnt a {NativesHere.name} camp: the peace is broken.");
        ChangeStanding(-25f);
    }

    /// <summary>Your troops struck a camp you were at peace with.</summary>
    public void BreakPeace(string why)
    {
        if (NativesHere == null || !Diplomacy.peace) return;
        EndPeace($"{why} The {NativesHere.name} take it as war.");
        ChangeStanding(-15f);
    }

    /// <summary>What a camp's label says: whose it is and where you stand (null at home: the usual raider camp).</summary>
    public string DiplomacyCampLabel(Enemy chief)
    {
        var f = NativesHere;
        if (f == null) return null;
        var s = StandingHere;
        string col = ColorUtility.ToHtmlStringRGB(StandingColour(s));
        string lead = chief != null ? $", led by <color=#ff8a70>{chief.definition.displayName}</color>" : "";
        string tail = Diplomacy.peace
            ? "At peace with you. Click it to talk."
            : "Click it to talk; or select troops and right-click it to burn it down.";
        return $"<color=#{col}>{f.name} camp ({StandingName(s)})</color>\n<size=75%>{f.name}{lead}. {tail}</size>";
    }
}
