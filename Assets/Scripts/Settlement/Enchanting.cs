using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Enchanting gear, unlocked by Rune Enchanting (Medieval research):
/// - Salvage: break spare items into Essence (dungeons, lairs and ruins give some too).
/// - Enhance: +1 to +5, each level 10% more from the item's base stats. Capped, so gear stays a bonus.
/// - Enchant: one extra magic trait, bound with a rare material from the wild (it replaces any old one).
/// - Re-roll: new traits, same rarity.
/// </summary>
public partial class Settlement
{
    public const string EnchantingTech = "RuneEnchanting";
    public const int MaxEnhance = 5;

    public bool CanEnchant => State.researched.Contains(EnchantingTech);
    public ResourceType Essence => Database.resources.Find(r => r != null && r.id == "Essence");

    private static readonly float[] RarityCost = { 1f, 1.2f, 1.5f, 2f, 3f };

    private static float PowerCost(ItemInstance item) => (1f + 0.25f * Mathf.Max(0f, item.power)) * RarityCost[(int)item.rarity];

    // ---------- Costs ----------

    public List<ResourceAmount> EnhanceCost(ItemInstance item) => Costs(
        ("Essence", Mathf.RoundToInt(12 * (item.enhance + 1) * PowerCost(item))),
        ("Gold", 20 * (item.enhance + 1)));

    /// <summary>The rare material an enchantment is bound with: the rarest one around the item's age.</summary>
    public string EnchantMaterial(ItemInstance item) =>
        item.power < 5 ? "Runestone" : item.power < 6.5f ? "Quicksilver" : item.power < 8.5f ? "Aether" : item.power < 10 ? "Uranium" : item.power < 11 ? "Silicon" : item.power < 12 ? "Manastone" : item.power < 13 ? "Starmetal" : item.power < 14 ? "Heliolite" : item.power < 15 ? "Riftstone" : item.power < 16 ? "Stardust" : "Primordium";

    public List<ResourceAmount> EnchantCost(ItemInstance item) => Costs(
        ("Essence", Mathf.RoundToInt(40 * PowerCost(item))),
        (EnchantMaterial(item), Mathf.RoundToInt(12 * RarityCost[(int)item.rarity])));

    public List<ResourceAmount> RerollCost(ItemInstance item) => Costs(("Essence", Mathf.RoundToInt(25 * PowerCost(item))));

    /// <summary>Essence from breaking an item down.</summary>
    public int SalvageYield(ItemInstance item) => Mathf.RoundToInt(4 * (1 + (int)item.rarity) * (1f + 0.25f * Mathf.Max(0f, item.power)) * (1f + 0.3f * item.enhance));

    private List<ResourceAmount> Costs(params (string id, int amount)[] amounts)
    {
        var list = new List<ResourceAmount>();
        foreach (var (id, amount) in amounts)
        {
            var res = Database.resources.Find(r => r != null && r.id == id);
            if (res != null && amount > 0) list.Add(new ResourceAmount { resource = res, amount = amount });
        }
        return list;
    }

    public bool CanAfford(List<ResourceAmount> cost, out string missing)
    {
        missing = null;
        foreach (var c in cost)
            if (GetAmount(c.resource) < c.amount) { missing = $"Needs {c.amount} {c.resource.displayName} ({Mathf.FloorToInt(GetAmount(c.resource))})"; return false; }
        return true;
    }

    private bool Pay(List<ResourceAmount> cost, out string why)
    {
        why = null;
        if (!CanEnchant) { why = "Research Rune Enchanting first"; return false; }
        if (!CanAfford(cost, out why)) return false;
        foreach (var c in cost) AddResource(c.resource, -c.amount);
        return true;
    }

    // ---------- Actions ----------

    public bool Enhance(ItemInstance item, out string why)
    {
        why = null;
        if (item == null) { why = "No item"; return false; }
        if (item.enhance >= MaxEnhance) { why = $"Already +{MaxEnhance}, as far as it goes"; return false; }
        if (!Pay(EnhanceCost(item), out why)) return false;
        item.enhance++;
        GameAudio.UI("forge");
        Notify($"{Items.DisplayName(item)} is stronger.");
        RaiseChanged();
        return true;
    }

    public bool Enchant(ItemInstance item, out string why)
    {
        why = null;
        if (item == null) { why = "No item"; return false; }
        if (!Pay(EnchantCost(item), out why)) return false;
        var rng = new System.Random(State.mapSeed ^ item.uid * 7919 ^ (int)(State.playTime * 10));
        item.enchant = Items.RollEnchantment(item, rng);
        item.enchanted = true;
        GameAudio.UI("enchant");
        Notify($"{Items.DisplayName(item)} glows: {Items.EnchantmentName(item.enchant.trait)}.");
        RaiseChanged();
        return true;
    }

    public bool Reroll(ItemInstance item, out string why)
    {
        why = null;
        if (item == null) { why = "No item"; return false; }
        if (item.traits.Count == 0) { why = "A common item has no traits to re-roll"; return false; }
        var template = GetTemplate(item.template);
        if (!Pay(RerollCost(item), out why)) return false;
        var rng = new System.Random(State.mapSeed ^ item.uid * 104729 ^ (int)(State.playTime * 10));
        Items.RerollTraits(item, template, rng);
        GameAudio.UI("enchant");
        Notify($"New traits: {Items.DisplayName(item)}.");
        RaiseChanged();
        return true;
    }

    /// <summary>Spare items for a slot that are worse than what this hero wears there (and worn by nobody).</summary>
    public List<ItemInstance> SpareItems(HeroState hero, GearSlot slot)
    {
        var list = new List<ItemInstance>();
        var worn = hero != null ? GetItem(hero.equipped[(int)slot]) : null;
        float bar = worn != null ? Items.Score(Items.StatsOf(worn, GetTemplate(worn.template))) : float.MaxValue;
        foreach (var item in State.inventory)
        {
            var t = GetTemplate(item.template);
            if (t == null || t.slot != slot || WhoWears(item.uid) != null) continue;
            if (Items.Score(Items.StatsOf(item, t)) < bar) list.Add(item);
        }
        return list;
    }

    public int Salvage(List<ItemInstance> items)
    {
        var essence = Essence;
        if (essence == null || !CanEnchant) return 0;
        int total = 0;
        foreach (var item in items)
        {
            if (WhoWears(item.uid) != null) continue;
            total += SalvageYield(item);
            State.inventory.Remove(item);
        }
        if (total > 0)
        {
            AddResource(essence, total);
            GameAudio.UI("coins");
            Notify($"Salvaged {items.Count} item{(items.Count == 1 ? "" : "s")} into {total} Essence.");
            RaiseChanged();
        }
        return total;
    }
}
