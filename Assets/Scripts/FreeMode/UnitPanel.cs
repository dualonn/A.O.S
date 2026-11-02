using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The panel for selected troops (bottom centre, where the building panel goes). One troop:
/// its stats and orders. Several: a short summary and a scrolling list to pick one from.
/// </summary>
public class UnitPanel
{
    private readonly FreeModeController controller;
    private readonly RectTransform panel;
    private readonly TextMeshProUGUI info;
    private readonly RectTransform listContent;
    private readonly LayoutElement listElement;
    private readonly Button homeButton;
    private readonly List<(Troop troop, Button button, TextMeshProUGUI label)> rows = new List<(Troop, Button, TextMeshProUGUI)>();
    private readonly GearPanel gear;

    /// <summary>Extra buttons and info for a single selected troop (heroes and gear hook in here).</summary>
    public RectTransform Extras { get; }

    public UnitPanel(FreeModeController controller, Transform root, System.Action<System.Func<string>> showTooltip, System.Action hideTooltip)
    {
        this.controller = controller;
        panel = UIBuilder.VerticalPanel("UnitPanel", root);
        UIBuilder.Anchor(panel, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 150), new Vector2(460, 0));
        info = UIBuilder.Text("Info", panel, "", 19);
        listContent = UIBuilder.ScrollList("Units", panel, out listElement, 4);
        Extras = UIBuilder.Rect("Extras", panel);
        var extrasLayout = Extras.gameObject.AddComponent<VerticalLayoutGroup>();
        extrasLayout.spacing = 6;
        extrasLayout.childControlWidth = extrasLayout.childControlHeight = true;
        extrasLayout.childForceExpandWidth = true;
        extrasLayout.childForceExpandHeight = false;
        gear = new GearPanel(controller, Extras, showTooltip, hideTooltip);
        homeButton = UIBuilder.Button("Home", panel, controller.ReturnSelectedHome, out var homeLabel, 40);
        homeLabel.text = "Back to their building";
        UIBuilder.OnHover(homeButton.gameObject, () => showTooltip(() =>
            "<b>Orders</b>\nRight-click the ground to send selected troops there; they hold that spot.\n" +
            "This sends them back to the building that trained them. Changing the building's stance does too."), () => hideTooltip());
        panel.gameObject.SetActive(false);
    }

    public bool Visible => panel.gameObject.activeSelf;

    public void Refresh()
    {
        var units = controller.SelectedUnits;
        panel.gameObject.SetActive(units.Count > 0);
        if (units.Count == 0)
        {
            return;
        }
        var settlement = controller.Settlement;
        if (units.Count == 1)
        {
            var t = units[0];
            var def = t.definition;
            string title = t.Hero != null ? $"<color=#fd6>{t.Hero.name}</color> <size=80%>{def?.displayName}</size>"
                : def != null ? def.displayName : t.name;
            var sb = new StringBuilder($"<b>{title}</b>\n");
            sb.Append($"Health {t.health}/{t.maxHealth}   Attack {t.attackPower}   Speed {t.moveSpeed:0.0}\n");
            if (t.Home != null)
            {
                sb.Append($"<size=85%><color=#aaa>From the {t.Home.Definition.displayName}. " +
                          $"Stance: {(t.Stance == TroopStance.Defend ? "<color=#8cf>Defend</color>" : "<color=#f96>Attack</color>")}</color></size>\n");
            }
            if (t.HasOrder) sb.Append("<size=85%><color=#8f8>Holding where you sent it</color></size>");
            info.text = sb.ToString().TrimEnd();
            listElement.gameObject.SetActive(false);
        }
        else
        {
            var kinds = units.Where(u => u != null && u.definition != null)
                .GroupBy(u => u.definition.displayName)
                .OrderByDescending(g => g.Count())
                .Select(g => $"{g.Count()} {g.Key}");
            info.text = $"<b>{units.Count} troops</b>\n<size=85%>{string.Join(", ", kinds)}</size>\n" +
                        "<size=80%><color=#aaa>Right-click to send them. Click a name to pick one.</color></size>";
            listElement.gameObject.SetActive(true);
            SyncRows(units);
        }
        Extras.gameObject.SetActive(units.Count == 1);
        gear.Refresh();
    }

    /// <summary>One row per selected troop: icon, name, health; click to select just that one.</summary>
    private void SyncRows(IReadOnlyList<Troop> units)
    {
        while (rows.Count < units.Count)
        {
            int index = rows.Count;
            var button = UIBuilder.Button("Unit", listContent, () => PickRow(index), out var label, 30, 15);
            rows.Add((null, button, label));
        }
        for (int i = 0; i < rows.Count; i++)
        {
            bool used = i < units.Count;
            var row = rows[i];
            row.button.gameObject.SetActive(used);
            if (!used) continue;
            var t = units[i];
            float share = t.maxHealth > 0 ? t.health / (float)t.maxHealth : 1f;
            string color = share > 0.66f ? "#8f8" : share > 0.33f ? "#fe8" : "#f88";
            string who = t.Hero != null ? $"<color=#fd6>{t.Hero.name}</color>" : t.definition != null ? t.definition.displayName : t.name;
            row.label.text = $"{who}  <color={color}><size=85%>{t.health}/{t.maxHealth}</size></color>";
            rows[i] = (t, row.button, row.label);
        }
        listElement.preferredHeight = Mathf.Min(units.Count, 6) * 34f;
    }

    private void PickRow(int index)
    {
        if (index < rows.Count && rows[index].troop != null)
        {
            controller.SetUnitSelection(new[] { rows[index].troop });
        }
    }
}
