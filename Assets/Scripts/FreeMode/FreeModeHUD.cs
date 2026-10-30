using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The free mode interface, built in code: resource bar, build menu, research and ages,
/// selected building panel, message log and tooltips.
/// </summary>
public class FreeModeHUD : MonoBehaviour
{
    public FreeModeController controller;

    private Settlement settlement;
    private TextMeshProUGUI topBar;
    private TextMeshProUGUI messageLog;
    private RectTransform tooltip;
    private TextMeshProUGUI tooltipText;
    private RectTransform selectionPanel;
    private TextMeshProUGUI selectionText;
    private Button repairButton;
    private TextMeshProUGUI repairLabel;
    private TextMeshProUGUI speedLabel;
    private Button advanceButton;
    private TextMeshProUGUI advanceLabel;

    private readonly List<(BuildingDefinition def, Button button, TextMeshProUGUI label)> buildButtons =
        new List<(BuildingDefinition, Button, TextMeshProUGUI)>();
    private readonly List<(Technology tech, Button button, TextMeshProUGUI label)> researchButtons =
        new List<(Technology, Button, TextMeshProUGUI)>();

    private readonly List<string> messages = new List<string>();
    private readonly Dictionary<ResourceType, float> lastAmounts = new Dictionary<ResourceType, float>();
    private readonly Dictionary<ResourceType, float> rates = new Dictionary<ResourceType, float>();
    private float rateTimer;
    private float refreshTimer;
    private System.Func<string> tooltipSource;

    private static readonly float[] Speeds = { 1f, 2f, 5f, 10f };
    private int speedIndex;

    private void Start()
    {
        settlement = controller.Settlement;
        if (settlement == null)
        {
            enabled = false;
            return;
        }
        BuildCanvas();
        controller.SelectionChanged += _ => RefreshSelection();
        controller.MessageShown += AddMessage;
        RefreshAll();
    }

    private void OnDestroy()
    {
        if (controller != null)
        {
            controller.MessageShown -= AddMessage;
        }
        Time.timeScale = 1f;
    }

    // ---------- Layout ----------

    private void BuildCanvas()
    {
        var canvasObject = new GameObject("FreeModeCanvas", typeof(RectTransform));
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();
        Transform root = canvasObject.transform;

        // Top bar
        var bar = UIBuilder.Panel("TopBar", root, UIBuilder.PanelColor).rectTransform;
        UIBuilder.Anchor(bar, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 56));
        topBar = UIBuilder.Text("Info", bar, "", 22, TextAlignmentOptions.MidlineLeft);
        UIBuilder.Stretch(topBar.rectTransform, 12);
        topBar.rectTransform.offsetMax = new Vector2(-150, -6);

        var speed = UIBuilder.Button("Speed", bar, CycleSpeed, out speedLabel, 40, 20);
        UIBuilder.Anchor((RectTransform)speed.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f),
            new Vector2(-10, 0), new Vector2(130, 40));
        speedLabel.alignment = TextAlignmentOptions.Center;
        speedLabel.text = "Speed x1";

        // Build menu (left)
        var build = UIBuilder.VerticalPanel("BuildPanel", root);
        UIBuilder.Anchor(build, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(10, -66), new Vector2(300, 0));
        UIBuilder.Text("Title", build, "<b>Build</b>", 24);
        foreach (var def in settlement.Database.buildings)
        {
            var d = def;
            var button = UIBuilder.Button(def.id, build, () => controller.Placer.Begin(d), out var label);
            UIBuilder.OnHover(button.gameObject, () => tooltipSource = () => BuildingTooltip(d), HideTooltip);
            buildButtons.Add((def, button, label));
        }
        UIBuilder.Text("Hint", build, "<size=80%><color=#aaa>Click to place. Shift keeps placing.\nRight click / Esc cancels.</color></size>", 16);

        // Research + ages (right)
        var research = UIBuilder.VerticalPanel("ResearchPanel", root);
        UIBuilder.Anchor(research, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-10, -66), new Vector2(330, 0));
        UIBuilder.Text("Title", research, "<b>Research</b>", 24);
        foreach (var tech in settlement.Database.technologies)
        {
            var t = tech;
            var button = UIBuilder.Button(tech.id, research, () => TryResearch(t), out var label);
            UIBuilder.OnHover(button.gameObject, () => tooltipSource = () => TechTooltip(t), HideTooltip);
            researchButtons.Add((tech, button, label));
        }
        UIBuilder.Text("AgeTitle", research, "<b>Age</b>", 24);
        advanceButton = UIBuilder.Button("Advance", research, TryAdvance, out advanceLabel, 60);
        UIBuilder.OnHover(advanceButton.gameObject, () => tooltipSource = AgeTooltip, HideTooltip);

        // Selected building (bottom centre)
        selectionPanel = UIBuilder.VerticalPanel("SelectionPanel", root);
        UIBuilder.Anchor(selectionPanel, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 150), new Vector2(460, 0));
        selectionText = UIBuilder.Text("Info", selectionPanel, "", 19);
        repairButton = UIBuilder.Button("Repair", selectionPanel, RepairSelected, out repairLabel, 44);
        UIBuilder.Button("Demolish", selectionPanel, DemolishSelected, out var demolishLabel, 44).image.color = new Color(0.45f, 0.18f, 0.18f);
        demolishLabel.text = "Demolish (no refund)";
        selectionPanel.gameObject.SetActive(false);

        // Messages (bottom centre)
        var log = UIBuilder.Panel("Messages", root, new Color(0, 0, 0, 0.45f)).rectTransform;
        UIBuilder.Anchor(log, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 10), new Vector2(900, 130));
        log.GetComponent<Image>().raycastTarget = false;
        messageLog = UIBuilder.Text("Text", log, "", 18, TextAlignmentOptions.BottomLeft);
        UIBuilder.Stretch(messageLog.rectTransform, 10);

        // Tooltip (follows the mouse)
        tooltip = UIBuilder.VerticalPanel("Tooltip", root, 12);
        tooltip.GetComponent<Image>().raycastTarget = false;
        tooltip.GetComponent<Image>().color = new Color(0.03f, 0.03f, 0.05f, 0.95f);
        UIBuilder.Anchor(tooltip, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(380, 0));
        tooltipText = UIBuilder.Text("Text", tooltip, "", 18);
        tooltip.gameObject.SetActive(false);
    }

    // ---------- Updating ----------

    private void Update()
    {
        TrackRates();

        refreshTimer -= Time.unscaledDeltaTime;
        if (refreshTimer <= 0)
        {
            refreshTimer = 0.25f;
            RefreshAll();
        }
        UpdateTooltip();
    }

    private void RefreshAll()
    {
        RefreshTopBar();
        RefreshBuildButtons();
        RefreshResearchButtons();
        RefreshSelection();
    }

    private void TrackRates()
    {
        rateTimer += Time.deltaTime;
        if (rateTimer < 2f)
        {
            return;
        }
        foreach (var res in settlement.Database.resources)
        {
            float now = settlement.GetAmount(res);
            if (lastAmounts.TryGetValue(res, out float before))
            {
                rates[res] = (now - before) / rateTimer;
            }
            lastAmounts[res] = now;
        }
        rateTimer = 0;
    }

    private void RefreshTopBar()
    {
        var sb = new StringBuilder();
        sb.Append($"<b>{settlement.CurrentAge?.displayName}</b>    ");
        foreach (var res in VisibleResources())
        {
            float rate = rates.TryGetValue(res, out float r) ? r : 0;
            string rateText = Mathf.Abs(rate) < 0.05f ? "" :
                rate > 0 ? $" <color=#8f8><size=75%>+{rate:0.0}/s</size></color>" : $" <color=#f88><size=75%>{rate:0.0}/s</size></color>";
            sb.Append($"{res.displayName} <b>{Mathf.FloorToInt(settlement.GetAmount(res))}</b>{rateText}    ");
        }
        string workers = settlement.WorkersNeeded > settlement.Population
            ? $" <color=#fc6><size=75%>({settlement.WorkersNeeded} jobs)</size></color>" : "";
        sb.Append($"People <b>{settlement.Population}/{settlement.Housing}</b>{workers}    ");
        sb.Append($"Next wave <b>{FormatTime(settlement.Waves.SecondsUntilWave)}</b>");

        var researching = settlement.Database.GetTechnology(settlement.State.researchingId ?? "");
        if (researching != null)
        {
            sb.Append($"    Researching <b>{researching.displayName}</b> {settlement.State.researchProgress:P0}");
        }
        topBar.text = sb.ToString();
    }

    private void RefreshBuildButtons()
    {
        foreach (var (def, button, label) in buildButtons)
        {
            bool locked = def.requirements.minimumAge != null && def.requirements.minimumAge.order > settlement.State.ageIndex + 1;
            button.gameObject.SetActive(!locked);
            bool can = settlement.CanBuild(def, out string missing);
            button.interactable = can;
            bool placingThis = controller.Placer.Current == def;
            label.text = $"<b>{def.displayName}</b>{(placingThis ? " <color=#8f8>(placing)</color>" : "")}\n" +
                         $"<size=80%>{SubtitleFor(def.requirements.cost, can, missing)}</size>";
        }
    }

    private void RefreshResearchButtons()
    {
        foreach (var (tech, button, label) in researchButtons)
        {
            bool done = settlement.HasResearched(tech);
            bool locked = tech.age != null && tech.age.order > settlement.State.ageIndex;
            button.gameObject.SetActive(!done && !locked);
            bool current = settlement.State.researchingId == tech.id;
            bool can = settlement.CanResearch(tech, out string missing);
            button.interactable = can;
            string sub = current
                ? $"<color=#8cf>Researching {settlement.State.researchProgress:P0}</color>"
                : SubtitleFor(tech.requirements.cost, can, missing) + $" <color=#aaa>({tech.researchTime:0}s)</color>";
            label.text = $"<b>{tech.displayName}</b>\n<size=80%>{sub}</size>";
        }

        var next = settlement.NextAge;
        if (next == null)
        {
            advanceButton.interactable = false;
            advanceLabel.text = "<b>Latest age reached</b>";
        }
        else
        {
            bool can = settlement.CanAdvanceAge(out _);
            advanceButton.interactable = can;
            advanceLabel.text = $"<b>Advance to the {next.displayName}</b>\n<size=80%><color={(can ? "#8f8" : "#f99")}>" +
                                $"{(can ? "Ready!" : "Hover to see what's needed")}</color></size>";
        }
    }

    private void RefreshSelection()
    {
        var view = controller.Selected;
        if (view == null || selectionPanel == null)
        {
            if (selectionPanel != null)
            {
                selectionPanel.gameObject.SetActive(false);
            }
            return;
        }
        selectionPanel.gameObject.SetActive(true);
        var state = view.State;
        var def = view.Definition;

        var sb = new StringBuilder($"<b>{def.displayName}</b>\n");
        if (!state.IsBuilt)
        {
            sb.Append($"Under construction: {state.buildProgress:P0}\n");
        }
        else
        {
            string ruined = state.health <= 0 ? " <color=#f77>(ruined: needs repair)</color>" : "";
            sb.Append($"Health {Mathf.CeilToInt(state.health)}/{def.maxHealth}{ruined}\n");
        }
        sb.Append(StatsText(def));
        selectionText.text = sb.ToString().TrimEnd();

        bool damaged = state.IsBuilt && state.health < def.maxHealth;
        repairButton.gameObject.SetActive(damaged);
        if (damaged)
        {
            var cost = settlement.RepairCost(state);
            repairButton.interactable = settlement.CanAfford(cost);
            repairLabel.text = $"Repair ({CostText(cost)})";
        }
    }

    // ---------- Actions ----------

    private void TryResearch(Technology tech)
    {
        if (!settlement.TryStartResearch(tech, out string reason))
        {
            AddMessage(reason.Split('\n')[0]);
        }
        RefreshAll();
    }

    private void TryAdvance()
    {
        if (!settlement.TryAdvanceAge(out string reason))
        {
            AddMessage(reason.Split('\n')[0]);
        }
        RefreshAll();
    }

    private void RepairSelected()
    {
        var view = controller.Selected;
        if (view != null && !settlement.TryRepair(view.State, out string reason))
        {
            AddMessage(reason);
        }
        RefreshAll();
    }

    private void DemolishSelected()
    {
        var view = controller.Selected;
        if (view != null)
        {
            settlement.Demolish(view.State);
        }
    }

    private void CycleSpeed()
    {
        speedIndex = (speedIndex + 1) % Speeds.Length;
        Time.timeScale = Speeds[speedIndex];
        speedLabel.text = $"Speed x{Speeds[speedIndex]:0}";
    }

    private void AddMessage(string message)
    {
        messages.Add(message);
        if (messages.Count > 5)
        {
            messages.RemoveAt(0);
        }
        if (messageLog != null)
        {
            messageLog.text = string.Join("\n", messages);
        }
    }

    // ---------- Tooltips ----------

    private void HideTooltip() => tooltipSource = null;

    private void UpdateTooltip()
    {
        if (tooltip == null)
        {
            return;
        }
        if (tooltipSource == null)
        {
            tooltip.gameObject.SetActive(false);
            return;
        }
        tooltip.gameObject.SetActive(true);
        tooltipText.text = tooltipSource();

        // Keep it on screen: open to the left / downwards when near the right / top edge.
        Vector2 mouse = Input.mousePosition;
        bool right = mouse.x > Screen.width * 0.5f;
        bool top = mouse.y > Screen.height * 0.5f;
        tooltip.pivot = new Vector2(right ? 1 : 0, top ? 1 : 0);
        var canvas = (RectTransform)tooltip.parent;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, mouse, null, out Vector2 local);
        tooltip.anchoredPosition = local + canvas.rect.size * 0.5f + new Vector2(right ? -20 : 20, top ? -20 : 20);
    }

    private string BuildingTooltip(BuildingDefinition def)
    {
        var sb = new StringBuilder($"<b>{def.displayName}</b>  <size=80%><color=#aaa>{def.size.x}x{def.size.y}</color></size>\n");
        if (!string.IsNullOrEmpty(def.description))
        {
            sb.Append($"<color=#ccc>{def.description}</color>\n");
        }
        sb.Append($"Cost: {CostText(def.requirements.cost)}   Build time: {def.buildTime:0}s\n");
        sb.Append(StatsText(def));
        AppendMissing(sb, settlement.CanBuild(def, out string missing), missing);
        return sb.ToString().TrimEnd();
    }

    private string TechTooltip(Technology tech)
    {
        var sb = new StringBuilder($"<b>{tech.displayName}</b>\n");
        if (!string.IsNullOrEmpty(tech.description))
        {
            sb.Append($"<color=#ccc>{tech.description}</color>\n");
        }
        sb.Append($"Cost: {CostText(tech.requirements.cost)}   Time: {tech.researchTime:0}s\n");
        AppendMissing(sb, settlement.CanResearch(tech, out string missing), missing);
        return sb.ToString().TrimEnd();
    }

    private string AgeTooltip()
    {
        var next = settlement.NextAge;
        if (next == null)
        {
            return "You have reached the latest age.";
        }
        var sb = new StringBuilder($"<b>{next.displayName}</b>\n");
        if (!string.IsNullOrEmpty(next.description))
        {
            sb.Append($"<color=#ccc>{next.description}</color>\n");
        }
        sb.Append($"Cost: {CostText(next.advanceRequirements.cost)}\n");
        AppendMissing(sb, settlement.CanAdvanceAge(out string missing), missing);
        return sb.ToString().TrimEnd();
    }

    private static void AppendMissing(StringBuilder sb, bool ok, string missing)
    {
        if (ok)
        {
            sb.Append("<color=#8f8>All requirements met</color>");
            return;
        }
        sb.Append("<color=#f99>Still needed:</color>\n");
        foreach (var line in missing.Split('\n'))
        {
            sb.Append($"<color=#f99>  - {line.Trim()}</color>\n");
        }
    }

    // ---------- Text helpers ----------

    private IEnumerable<ResourceType> VisibleResources() =>
        settlement.Database.resources.Where(r =>
            r != null && (r.unlockedIn == null || r.unlockedIn.order <= settlement.State.ageIndex));

    private static string StatsText(BuildingDefinition def)
    {
        var sb = new StringBuilder();
        foreach (var rate in def.production)
        {
            if (rate.resource != null)
            {
                sb.Append($"Produces {rate.perSecond:0.##} {rate.resource.displayName}/s\n");
            }
        }
        if (def.housing > 0)
        {
            sb.Append($"Houses {def.housing} people\n");
        }
        if (def.workers > 0)
        {
            sb.Append($"Needs {def.workers} workers\n");
        }
        if (def.trainsUnit != null)
        {
            sb.Append($"Trains up to {def.maxUnits} {def.trainsUnit.displayName}\n");
        }
        if (def.defensePower > 0)
        {
            sb.Append($"Defence +{def.defensePower:0.#}\n");
        }
        return sb.ToString();
    }

    /// <summary>
    /// Button subtitle: the cost, or, when something other than resources is blocking it
    /// (a tech, building, age...), that blocker so the player knows what to do next.
    /// </summary>
    private static string SubtitleFor(List<ResourceAmount> cost, bool can, string missing)
    {
        if (can)
        {
            return $"<color=#ddd>{CostText(cost)}</color>";
        }
        string firstLine = missing.Split('\n')[0];
        bool onlyResources = cost.Any(c => c.resource != null && firstLine.Contains(c.resource.displayName + " ("));
        return onlyResources
            ? $"<color=#f99>{CostText(cost)}</color>"
            : $"<color=#fc6>{firstLine}</color>";
    }

    private static string CostText(List<ResourceAmount> cost)
    {
        var parts = cost.Where(c => c.resource != null).Select(c => $"{c.amount} {c.resource.displayName}").ToList();
        return parts.Count == 0 ? "Free" : string.Join(", ", parts);
    }

    private static string FormatTime(float seconds)
    {
        seconds = Mathf.Max(0, seconds);
        return $"{(int)(seconds / 60)}:{(int)(seconds % 60):00}";
    }
}
