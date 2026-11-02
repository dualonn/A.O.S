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
        heroInfo.text = $"<color=#fd6><b>{hero.name}</b></color>  Level {hero.level + 1} <size=80%><color=#aaa>({xp})</color></size>\n" +
                        $"<size=85%>{Items.Describe(stats).Replace("\n", "  ·  ")}</size>";

        while (hero.equipped.Count < 5) hero.equipped.Add(0);
        foreach (var (slot, button, label) in slotButtons)
        {
            var item = Settlement.GetItem(hero.equipped[(int)slot]);
            string name = item != null ? $"<color=#{Items.RarityHex(item.rarity)}>{Short(item.name)}</color>" : "<color=#777>empty</color>";
            bool active = slot == activeSlot;
            label.text = $"{(active ? "<b>" : "")}{slot}{(active ? "</b>" : "")}\n<size=80%>{name}</size>";
            button.image.color = active ? new Color(0.3f, 0.32f, 0.38f) : UIBuilder.ButtonColor;
        }

        // Items that fit the chosen slot, best first.
        shownItems.Clear();
        foreach (var item in Settlement.State.inventory)
        {
            var t = Settlement.GetTemplate(item.template);
            if (t != null && t.slot == activeSlot) shownItems.Add(item);
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
            itemRows[i].label.text = $"<color=#{Items.RarityHex(item.rarity)}>{item.name}</color> <size=80%><color=#aaa>power {item.power:0.0}</color></size>{worn}";
        }
        itemListElement.gameObject.SetActive(shownItems.Count > 0);
        itemListElement.preferredHeight = Mathf.Min(shownItems.Count, 5) * 33f;
        emptyNote.gameObject.SetActive(shownItems.Count == 0);
        emptyNote.text = $"<color=#aaa>No {activeSlot.ToString().ToLower()} gear yet. Burn down raider camps (and soon, ruins and dungeons) to find some.</color>";
        removeButton.gameObject.SetActive(hero.equipped[(int)activeSlot] != 0);
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
        var sb = new StringBuilder($"<b><color=#{Items.RarityHex(item.rarity)}>{item.name}</color></b>\n");
        sb.Append($"<size=85%><color=#aaa>{item.rarity} {template?.slot.ToString().ToLower()}, power {item.power:0.0}</color></size>\n");
        sb.Append(Items.Describe(stats));
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
