using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shown under a single selected troop. Ordinary troops get a "Promote to hero" button.
/// Heroes get their level, five gear slots, and the matching items from your inventory:
/// pick a slot, then click an item to put it on (hover to compare with what's worn).
/// </summary>
public class GearPanel
{
    private readonly FreeModeController controller;
    private readonly System.Action<System.Func<string>> showTooltip;
    private readonly System.Action hideTooltip;
    private readonly RectTransform root;
    private readonly Button promoteButton;
    private readonly TextMeshProUGUI promoteLabel;
    private readonly RectTransform heroBlock;
    private readonly TextMeshProUGUI heroInfo;
    private readonly List<(GearSlot slot, Button button, TextMeshProUGUI label)> slotButtons = new List<(GearSlot, Button, TextMeshProUGUI)>();
    private readonly RectTransform itemList;
    private readonly LayoutElement itemListElement;
    private readonly TextMeshProUGUI emptyNote;
    private readonly Button removeButton;
    private readonly RectTransform enchantRow;
    private readonly RectTransform classRow;
    private readonly List<(HeroClass cls, Button button)> classButtons = new List<(HeroClass, Button)>();
    private readonly Button enhanceButton, enchantButton, rerollButton, salvageButton;
    private readonly TextMeshProUGUI enhanceLabel, enchantLabel, rerollLabel, salvageLabel;
    private readonly List<(Button button, TextMeshProUGUI label)> itemRows = new List<(Button, TextMeshProUGUI)>();
    private readonly List<ItemInstance> shownItems = new List<ItemInstance>();
    private GearSlot activeSlot = GearSlot.Weapon;

    private Settlement Settlement => controller.Settlement;

    public GearPanel(FreeModeController controller, Transform parent, System.Action<System.Func<string>> showTooltip, System.Action hideTooltip)
    {
        this.controller = controller;
        this.showTooltip = showTooltip;
        this.hideTooltip = hideTooltip;
        root = UIBuilder.Rect("Gear", parent);
        var layout = root.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 6;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        promoteButton = UIBuilder.Button("Promote", root, Promote, out promoteLabel, 48);
        promoteButton.image.color = new Color(0.42f, 0.33f, 0.14f);
        UIBuilder.OnHover(promoteButton.gameObject, () => showTooltip(PromoteHelp), () => hideTooltip());

        heroBlock = UIBuilder.Rect("Hero", root);
        var heroLayout = heroBlock.gameObject.AddComponent<VerticalLayoutGroup>();
        heroLayout.spacing = 5;
        heroLayout.childControlWidth = heroLayout.childControlHeight = true;
        heroLayout.childForceExpandWidth = true;
        heroLayout.childForceExpandHeight = false;
        heroInfo = UIBuilder.Text("Info", heroBlock, "", 16);

        // At level 2 a hero can choose its path (once).
        classRow = UIBuilder.Row("Path", heroBlock, 4);
        classRow.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
        foreach (var cls in new[] { HeroClass.Warrior, HeroClass.Ranger, HeroClass.Mage, HeroClass.Priest })
        {
            var c = cls;
            var b = UIBuilder.Button(cls.ToString(), classRow, () => ChooseClass(c), out var label, 34, 13);
            b.GetComponent<LayoutElement>().flexibleWidth = 1;
            label.alignment = TextAlignmentOptions.Center;
            label.text = cls.ToString();
            b.image.color = new Color(0.36f, 0.28f, 0.12f);
            UIBuilder.OnHover(b.gameObject, () => showTooltip(() => ClassTip(c)), () => hideTooltip());
            classButtons.Add((cls, b));
        }

        // Five slots in a row: click one to see the items that fit it.
        var slots = UIBuilder.Row("Slots", heroBlock, 4);
        slots.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
        foreach (GearSlot slot in System.Enum.GetValues(typeof(GearSlot)))
        {
            var s = slot;
            var button = UIBuilder.Button(slot.ToString(), slots, () => { activeSlot = s; Refresh(); }, out var label, 46, 13);
            button.GetComponent<LayoutElement>().flexibleWidth = 1;
            label.alignment = TextAlignmentOptions.Center;
            UIBuilder.OnHover(button.gameObject, () => showTooltip(() => SlotTooltip(s)), () => hideTooltip());
            slotButtons.Add((slot, button, label));
        }

        itemList = UIBuilder.ScrollList("Items", heroBlock, out itemListElement, 3);
        emptyNote = UIBuilder.Text("Empty", heroBlock, "", 14);
        removeButton = UIBuilder.Button("Remove", heroBlock, Remove, out var removeLabel, 30, 14);
        removeLabel.text = "Take it off";

        // Enchanting (after Rune Enchanting): works on the item worn in the chosen slot.
        enchantRow = UIBuilder.Row("Enchanting", heroBlock, 4);
        enchantRow.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
        Button Small(string name, System.Action act, System.Func<string> tip, out TextMeshProUGUI label)
        {
            var b = UIBuilder.Button(name, enchantRow, () => act(), out label, 40, 12);
            b.GetComponent<LayoutElement>().flexibleWidth = 1;
            label.alignment = TextAlignmentOptions.Center;
            b.image.color = new Color(0.24f, 0.2f, 0.34f);
            UIBuilder.OnHover(b.gameObject, () => showTooltip(tip), () => hideTooltip());
            return b;
        }
        enhanceButton = Small("Enhance", () => Act((s, i) => (s.Enhance(i, out var w), w)), () => CostTip("Enhance", "Each level makes the item's own stats 10% stronger, up to +5.", Worn != null ? Settlement.EnhanceCost(Worn) : null), out enhanceLabel);
        enchantButton = Small("Enchant", () => Act((s, i) => (s.Enchant(i, out var w), w)), () => CostTip("Enchant", "Bind one extra magic trait to the item (it replaces any enchantment it already has).", Worn != null ? Settlement.EnchantCost(Worn) : null), out enchantLabel);
        rerollButton = Small("Reroll", () => Act((s, i) => (s.Reroll(i, out var w), w)), () => CostTip("Re-roll", "Give the item new traits, as many as its rarity allows.", Worn != null ? Settlement.RerollCost(Worn) : null), out rerollLabel);
        salvageButton = Small("Salvage", SalvageSpares, SalvageTip, out salvageLabel);
    }

    private Troop Troop => controller.SelectedUnits.Count == 1 ? controller.SelectedUnits[0] : null;

    public void Refresh()
    {
        var troop = Troop;
        root.gameObject.SetActive(troop != null);
        if (troop == null)
        {
            return;
        }
        var hero = troop.Hero;
        promoteButton.gameObject.SetActive(hero == null);
        heroBlock.gameObject.SetActive(hero != null);
        if (hero == null)
        {
            bool can = Settlement.CanPromote(out string why);
            promoteButton.interactable = can;
            string cost = string.Join(", ", Settlement.PromoteCost().Select(c => $"{c.amount} {c.resource.displayName}"));
            promoteLabel.text = $"<b>Promote to hero</b>  <size=75%>({Settlement.State.heroes.Count}/{Settlement.HeroSlots})</size>\n" +
                                $"<size=80%>{(can ? cost : $"<color=#f88>{why}</color>")}</size>";
            return;
        }

        // Level and what the gear adds up to.
        var stats = Settlement.HeroStats(hero);
        string xp = hero.level + 1 >= Settlement.LevelCap ? "max level for this age" : $"{hero.xp:0}/{Settlement.XpToNext(hero):0} xp";
        string path = hero.heroClass != HeroClass.None ? $"  <color=#fc6>{hero.heroClass}</color>" : hero.level >= Settlement.ClassLevel ? "  <color=#fc6>choose a path:</color>" : "";
        heroInfo.text = $"<color=#fd6><b>{hero.name}</b></color>  Level {hero.level + 1}{path} <size=80%><color=#aaa>({xp})</color></size>\n" +
                        $"<size=85%>{Items.Describe(stats).Replace("\n", "  ·  ")}</size>";

        classRow.gameObject.SetActive(hero.heroClass == HeroClass.None && hero.level >= Settlement.ClassLevel);
        foreach (var (cls, button) in classButtons) button.interactable = Settlement.CanChooseClass(hero, cls, out _);
        while (hero.equipped.Count < 5) hero.equipped.Add(0);
        foreach (var (slot, button, label) in slotButtons)
        {
            var item = Settlement.GetItem(hero.equipped[(int)slot]);
            string name = item != null ? $"<color=#{Items.RarityHex(item.rarity)}>{Short(Items.DisplayName(item))}</color>" : "<color=#777>empty</color>";
            bool active = slot == activeSlot;
            label.text = $"{(active ? "<b>" : "")}{slot}{(active ? "</b>" : "")}\n<size=80%>{name}</size>";
            button.image.color = active ? new Color(0.3f, 0.32f, 0.38f) : UIBuilder.ButtonColor;
        }

        // Items that fit the chosen slot, best first.
        shownItems.Clear();
        int unfit = 0;
        string unfitWhy = null;
        var unit = Settlement.FindUnit(hero.unitId);
        foreach (var item in Settlement.State.inventory)
        {
            var t = Settlement.GetTemplate(item.template);
            if (t == null || t.slot != activeSlot) continue;
            if (Items.Fits(t, unit, out string why)) shownItems.Add(item);
            else { unfit++; unfitWhy = why; }
        }
        shownItems.Sort((a, b) => Items.Score(Stats(b)).CompareTo(Items.Score(Stats(a))));
        while (itemRows.Count < shownItems.Count)
        {
            int index = itemRows.Count;
            var row = UIBuilder.Button("Item", itemList, () => EquipRow(index), out var rowLabel, 30, 14);
            UIBuilder.OnHover(row.gameObject, () => showTooltip(() => index < shownItems.Count ? ItemTooltip(shownItems[index]) : ""), () => hideTooltip());
            itemRows.Add((row, rowLabel));
        }
        for (int i = 0; i < itemRows.Count; i++)
        {
            bool used = i < shownItems.Count;
            itemRows[i].button.gameObject.SetActive(used);
            if (!used) continue;
            var item = shownItems[i];
            var wearer = Settlement.WhoWears(item.uid);
            string worn = wearer == hero ? " <color=#8f8>(worn)</color>" : wearer != null ? $" <color=#aaa>({wearer.name.Split(' ')[0]})</color>" : "";
            itemRows[i].label.text = $"<color=#{Items.RarityHex(item.rarity)}>{Items.DisplayName(item)}</color> <size=80%><color=#aaa>power {item.power:0.0}</color></size>{worn}";
        }
        itemListElement.gameObject.SetActive(shownItems.Count > 0);
        itemListElement.preferredHeight = Mathf.Min(shownItems.Count, 5) * 33f;
        emptyNote.gameObject.SetActive(shownItems.Count == 0 || unfit > 0);
        emptyNote.text = unfit > 0
            ? $"<size=85%><color=#aaa>{unfit} more {(unfit == 1 ? "piece doesn't" : "don't")} fit: {unfitWhy.ToLower()}.</color></size>"
            : $"<color=#aaa>No {activeSlot.ToString().ToLower()} gear yet. Burn down raider camps (and soon, ruins and dungeons) to find some.</color>";
        removeButton.gameObject.SetActive(hero.equipped[(int)activeSlot] != 0);

        enchantRow.gameObject.SetActive(Settlement.CanEnchant);
        if (Settlement.CanEnchant)
        {
            var worn = Worn;
            enhanceButton.interactable = enchantButton.interactable = rerollButton.interactable = worn != null;
            enhanceLabel.text = worn == null ? "Enhance" : worn.enhance >= Settlement.MaxEnhance ? $"+{Settlement.MaxEnhance} (max)" : $"Enhance\n<size=80%>to +{worn.enhance + 1}</size>";
            enhanceButton.interactable = worn != null && worn.enhance < Settlement.MaxEnhance;
            enchantLabel.text = worn != null && worn.enchanted ? "Re-enchant" : "Enchant";
            rerollLabel.text = "Re-roll";
            int spares = Settlement.SpareItems(hero, activeSlot).Count;
            salvageButton.interactable = spares > 0;
            salvageLabel.text = $"Salvage\n<size=80%>{spares} spare</size>";
        }
    }

    private void ChooseClass(HeroClass cls)
    {
        var hero = Troop?.Hero;
        if (hero == null) return;
        if (!Settlement.ChooseClass(hero, cls, out string why)) controller.ShowMessage(why);
        Troop?.RefreshHeroTag();
        Refresh();
    }

    private string ClassTip(HeroClass cls)
    {
        var hero = Troop?.Hero;
        bool can = Settlement.CanChooseClass(hero, cls, out string why);
        return $"<b>{cls}</b>\n{Settlement.ClassDescription(cls)}" + (can ? "\n<color=#aaa>A hero chooses once.</color>" : $"\n<color=#f88>{why}</color>");
    }

    private ItemInstance Worn
    {
        get
        {
            var hero = Troop?.Hero;
            return hero != null ? Settlement.GetItem(hero.equipped[(int)activeSlot]) : null;
        }
    }

    private void Act(System.Func<Settlement, ItemInstance, (bool ok, string why)> action)
    {
        var item = Worn;
        if (item == null) return;
        var (ok, why) = action(Settlement, item);
        if (!ok && why != null) controller.ShowMessage(why);
        Refresh();
    }

    private void SalvageSpares()
    {
        var hero = Troop?.Hero;
        if (hero == null) return;
        Settlement.Salvage(Settlement.SpareItems(hero, activeSlot));
        Refresh();
    }

    private string CostTip(string title, string what, List<ResourceAmount> cost)
    {
        if (cost == null) return $"<b>{title}</b>\n{what}\n<color=#aaa>Wear something in this slot first.</color>";
        bool can = Settlement.CanAfford(cost, out _);
        string price = string.Join(", ", cost.Select(c => $"{c.amount} {c.resource.displayName}"));
        return $"<b>{title} {Items.DisplayName(Worn)}</b>\n{what}\n<color={(can ? "#8f8" : "#f88")}>{price}</color>";
    }

    private string SalvageTip()
    {
        var hero = Troop?.Hero;
        var spares = hero != null ? Settlement.SpareItems(hero, activeSlot) : new List<ItemInstance>();
        int essence = spares.Sum(i => Settlement.SalvageYield(i));
        return $"<b>Salvage spare {activeSlot.ToString().ToLower()} gear</b>\nBreak down the {spares.Count} piece{(spares.Count == 1 ? "" : "s")} worse than what's worn here " +
               $"(and worn by nobody) into <color=#c9f>{essence} Essence</color>.";
    }

    private GearStats Stats(ItemInstance item) => Items.StatsOf(item, Settlement.GetTemplate(item.template));

    private static string Short(string name) => name.Length > 16 ? name.Substring(0, 15) + "…" : name;

    private void Promote()
    {
        var troop = Troop;
        if (troop == null || troop.Hero != null || troop.Home == null)
        {
            return;
        }
        var hero = Settlement.Promote(troop.definition, troop.Home.State, out string why);
        if (hero == null)
        {
            controller.ShowMessage(why);
            return;
        }
        troop.Home.AdoptAsHero(troop, hero);
        Refresh();
    }

    private void EquipRow(int index)
    {
        var hero = Troop?.Hero;
        if (hero == null || index >= shownItems.Count) return;
        Settlement.Equip(hero, shownItems[index]);
        Refresh();
    }

    private void Remove()
    {
        var hero = Troop?.Hero;
        if (hero == null) return;
        Settlement.Unequip(hero, activeSlot);
        Refresh();
    }

    private string ItemTooltip(ItemInstance item)
    {
        var template = Settlement.GetTemplate(item.template);
        var stats = Stats(item);
        var sb = new StringBuilder($"<b><color=#{Items.RarityHex(item.rarity)}>{Items.DisplayName(item)}</color></b>\n");
        sb.Append($"<size=85%><color=#aaa>{item.rarity} {template?.slot.ToString().ToLower()}, power {item.power:0.0}</color></size>\n");
        sb.Append(Items.Describe(stats));
        if (item.enchanted) sb.Append($"\n<color=#c9f>Enchanted: {Items.EnchantmentName(item.enchant.trait)}</color>");
        var hero = Troop?.Hero;
        var worn = hero != null && template != null ? Settlement.GetItem(hero.equipped[(int)template.slot]) : null;
        if (worn != null && worn != item)
        {
            float diff = Items.Score(stats) - Items.Score(Stats(worn));
            sb.Append($"\n<size=85%>{(diff >= 0 ? "<color=#8f8>Better" : "<color=#f88>Worse")} than {worn.name}</color></size>");
        }
        return sb.ToString();
    }

    private string SlotTooltip(GearSlot slot)
    {
        var hero = Troop?.Hero;
        var item = hero != null ? Settlement.GetItem(hero.equipped[(int)slot]) : null;
        return item != null ? ItemTooltip(item) : $"<b>{slot}</b>\nNothing worn. Click to see your {slot.ToString().ToLower()} gear.";
    }

    private string PromoteHelp() =>
        "<b>Heroes</b>\nA hero keeps its name, levels up from kills and wears gear you find in the wild.\n" +
        "If it falls it recovers at home a couple of minutes later. You get one hero place now and one more each age.";
}
