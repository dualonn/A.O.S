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
    private ArtLibrary art;
    private CameraController cameraController;
    private TextMeshProUGUI ageText;
    private TextMeshProUGUI peopleText;
    private TextMeshProUGUI waveText;
    private TextMeshProUGUI researchText;
    private readonly Dictionary<ResourceType, TextMeshProUGUI> resourceChips = new Dictionary<ResourceType, TextMeshProUGUI>();
    private TextMeshProUGUI messageLog;
    private RectTransform tooltip;
    private TextMeshProUGUI tooltipText;
    private RectTransform selectionPanel;
    private TextMeshProUGUI selectionText;
    private Button repairButton;
    private RectTransform foundingBanner;
    private readonly List<(Age age, TextMeshProUGUI header)> ageHeaders = new List<(Age, TextMeshProUGUI)>();
    private Button upgradeButton;
    private TextMeshProUGUI upgradeLabel;
    private Button stanceButton;
    private TextMeshProUGUI stanceLabel;
    private Button allStanceButton;
    private TextMeshProUGUI allStanceLabel;
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
        art = ArtLibrary.Load();
        UIBuilder.PanelSprite = art != null ? art.panel : null;
        cameraController = Camera.main != null ? Camera.main.GetComponent<CameraController>() : null;

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

        // Top bar: a row of icon + value chips, with view controls on the right
        var bar = UIBuilder.Panel("TopBar", root, UIBuilder.PanelColor).rectTransform;
        UIBuilder.Anchor(bar, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -4), new Vector2(-8, 56));
        var chips = UIBuilder.Row("Chips", bar, 26);
        UIBuilder.Stretch(chips);
        chips.offsetMin = new Vector2(16, 0);
        chips.offsetMax = new Vector2(-520, 0);

        ageText = Chip(chips, null);
        foreach (var res in settlement.Database.resources)
        {
            resourceChips[res] = Chip(chips, res.icon);
        }
        peopleText = Chip(chips, art != null ? art.peopleIcon : null);
        waveText = Chip(chips, art != null ? art.enemyIcon : null);
        researchText = Chip(chips, art != null ? art.researchIcon : null);

        var zoomOut = UIBuilder.Button("ZoomOut", bar, () => cameraController?.ZoomOut(), out var zoomOutLabel, 40, 26);
        UIBuilder.Anchor((RectTransform)zoomOut.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f),
            new Vector2(-250, 0), new Vector2(44, 40));
        zoomOutLabel.text = "-";
        zoomOutLabel.alignment = TextAlignmentOptions.Center;
        var zoomIn = UIBuilder.Button("ZoomIn", bar, () => cameraController?.ZoomIn(), out var zoomInLabel, 40, 26);
        UIBuilder.Anchor((RectTransform)zoomIn.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f),
            new Vector2(-200, 0), new Vector2(44, 40));
        zoomInLabel.text = "+";
        zoomInLabel.alignment = TextAlignmentOptions.Center;
        UIBuilder.OnHover(zoomIn.gameObject, () => tooltipSource = ZoomHelp, HideTooltip);
        UIBuilder.OnHover(zoomOut.gameObject, () => tooltipSource = ZoomHelp, HideTooltip);

        allStanceButton = UIBuilder.Button("AllStance", bar, ToggleAllStances, out allStanceLabel, 40, 18);
        UIBuilder.Anchor((RectTransform)allStanceButton.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f),
            new Vector2(-310, 0), new Vector2(190, 40));
        allStanceLabel.alignment = TextAlignmentOptions.Center;
        UIBuilder.OnHover(allStanceButton.gameObject, () => tooltipSource = StanceHelp, HideTooltip);

        var speed = UIBuilder.Button("Speed", bar, CycleSpeed, out speedLabel, 40, 20);
        UIBuilder.Anchor((RectTransform)speed.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f),
            new Vector2(-10, 0), new Vector2(130, 40));
        speedLabel.alignment = TextAlignmentOptions.Center;
        speedLabel.text = "Speed x1";

        // Build menu (left)
        var build = UIBuilder.VerticalPanel("BuildPanel", root);
        UIBuilder.Anchor(build, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(10, -66), new Vector2(300, 0));
        UIBuilder.Text("Title", build, "<b>Build</b>", 24);
        // Grouped by age: each age's buildings under its own heading.
        var db = settlement.Database;
        foreach (var age in db.ages)
        {
            var header = UIBuilder.Text("Age", build, $"<size=85%><color=#fd8>{age.displayName}</color></size>", 18);
            ageHeaders.Add((age, header));
            foreach (var def in db.buildings)
            {
                var defAge = def.age != null ? def.age : db.ages[0];
                if (defAge != age)
                {
                    continue;
                }
                var d = def;
                var button = UIBuilder.Button(def.id, build, () => controller.Placer.Begin(d), out var label, 56, 18, def.icon);
                UIBuilder.OnHover(button.gameObject, () => tooltipSource = () => BuildingTooltip(d), HideTooltip);
                buildButtons.Add((def, button, label));
            }
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
        upgradeButton = UIBuilder.Button("Upgrade", selectionPanel, UpgradeSelected, out upgradeLabel, 54);
        upgradeButton.image.color = new Color(0.25f, 0.33f, 0.22f);
        UIBuilder.OnHover(upgradeButton.gameObject, () => tooltipSource = UpgradeTooltip, HideTooltip);
        stanceButton = UIBuilder.Button("Stance", selectionPanel, ToggleSelectedStance, out stanceLabel, 50);
        UIBuilder.OnHover(stanceButton.gameObject, () => tooltipSource = StanceHelp, HideTooltip);
        repairButton = UIBuilder.Button("Repair", selectionPanel, RepairSelected, out repairLabel, 44);
        UIBuilder.Button("Demolish", selectionPanel, DemolishSelected, out var demolishLabel, 44).image.color = new Color(0.45f, 0.18f, 0.18f);
        demolishLabel.text = "Demolish (no refund)";
        selectionPanel.gameObject.SetActive(false);

        // Founding banner (top centre, until the Campfire is placed)
        foundingBanner = UIBuilder.Panel("FoundingBanner", root, new Color(0.1f, 0.08f, 0.02f, 0.9f)).rectTransform;
        UIBuilder.Anchor(foundingBanner, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -70), new Vector2(760, 74));
        foundingBanner.GetComponent<Image>().raycastTarget = false;
        var bannerText = UIBuilder.Text("Text", foundingBanner,
            "<b>Choose where to found your settlement</b>\n<size=80%>Place your Campfire. Look for forest, berries and stone nearby, and water for bonuses.</size>",
            22, TextAlignmentOptions.Center);
        UIBuilder.Stretch(bannerText.rectTransform, 8);

        // Messages (bottom centre)
        var log = UIBuilder.Panel("Messages", root, new Color(0, 0, 0, 0.45f)).rectTransform;
        UIBuilder.Anchor(log, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 10), new Vector2(900, 130));
        log.GetComponent<Image>().raycastTarget = false;
        messageLog = UIBuilder.Text("Text", log, "", 18, TextAlignmentOptions.BottomLeft);
        UIBuilder.Stretch(messageLog.rectTransform, 10);
        messageGroup = log.gameObject.AddComponent<CanvasGroup>();
        messageGroup.alpha = 0;
        messageGroup.blocksRaycasts = false;

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
        FadeMessages();
    }

    private void RefreshAll()
    {
        RefreshTopBar();
        foundingBanner.gameObject.SetActive(!settlement.IsFounded);
        RefreshAllStanceButton();
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

    /// <summary>An icon followed by a text value, added to a row. Returns the text.</summary>
    private static TextMeshProUGUI Chip(Transform row, Sprite icon)
    {
        var chip = UIBuilder.Row("Chip", row, 6);
        if (icon != null)
        {
            UIBuilder.Icon("Icon", chip, icon, 34);
        }
        return UIBuilder.Text("Value", chip, "", 22, TextAlignmentOptions.MidlineLeft);
    }

    private void RefreshTopBar()
    {
        ageText.text = $"<b>{settlement.CurrentAge?.displayName}</b>";

        var visible = new HashSet<ResourceType>(VisibleResources());
        foreach (var pair in resourceChips)
        {
            var res = pair.Key;
            pair.Value.transform.parent.gameObject.SetActive(visible.Contains(res));
            float rate = rates.TryGetValue(res, out float r) ? r : 0;
            string rateText = Mathf.Abs(rate) < 0.05f ? "" :
                rate > 0 ? $" <color=#8f8><size=70%>+{rate:0.0}/s</size></color>" : $" <color=#f88><size=70%>{rate:0.0}/s</size></color>";
            string name = res.icon == null ? res.displayName + " " : "";
            pair.Value.text = $"{name}<b>{Mathf.FloorToInt(settlement.GetAmount(res))}</b>{rateText}";
        }

        string workers = settlement.WorkersNeeded > settlement.Population
            ? $" <color=#fc6><size=70%>({settlement.WorkersNeeded} jobs)</size></color>" : "";
        peopleText.text = $"<b>{settlement.Population}/{settlement.Housing}</b>{workers}";
        waveText.text = $"<b>{FormatTime(settlement.Waves.SecondsUntilWave)}</b>";

        var researching = settlement.Database.GetTechnology(settlement.State.researchingId ?? "");
        researchText.transform.parent.gameObject.SetActive(researching != null);
        if (researching != null)
        {
            researchText.text = $"{researching.displayName} <b>{settlement.State.researchProgress:P0}</b>";
        }
    }

    private static string ZoomHelp() =>
        "<b>View</b>\nMouse wheel or +/- to zoom\nWASD / arrow keys or middle-drag to move";

    private void RefreshBuildButtons()
    {
        // Show the current and earlier ages, plus a preview of the next one.
        foreach (var (age, header) in ageHeaders)
        {
            header.gameObject.SetActive(settlement.IsFounded && age.order <= settlement.State.ageIndex + 1);
        }
        foreach (var (def, button, label) in buildButtons)
        {
            int ageOrder = def.age != null ? def.age.order : 0;
            bool locked = ageOrder > settlement.State.ageIndex + 1;
            // Before founding only the town center is offered; afterwards it's hidden once built.
            bool founded = settlement.IsFounded;
            bool hidden = locked || (founded ? def.isTownCenter && settlement.CountAll(def) > 0 : !def.isTownCenter);
            button.gameObject.SetActive(!hidden);
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
            sb.Append($"Health {Mathf.FloorToInt(state.health)}/{settlement.StatsOf(state).maxHealth}{ruined}\n");
        }
        sb.Append(StatsText(def, state.upgradeLevel));
        if (def.requiredDeposit != null || def.waterBonus > 0)
        {
            string color = state.siteQuality >= 0.99f ? "#8f8" : state.siteQuality >= 0.5f ? "#fe8" : "#fb7";
            sb.Append($"Site output: <color={color}>{state.siteQuality:P0}</color>\n");
        }
        selectionText.text = sb.ToString().TrimEnd();

        bool trains = def.trainsUnit != null;
        stanceButton.gameObject.SetActive(trains);
        if (trains)
        {
            stanceLabel.text = StanceText(state.stance) + "\n<size=75%><color=#aaa>Click to switch</color></size>";
        }

        // Upgrades: the next tier for this building, or "fully upgraded".
        bool hasUpgrades = def.upgrades.Count > 0 || def.rankUpTo != null;
        upgradeButton.gameObject.SetActive(hasUpgrades && state.IsBuilt);
        if (hasUpgrades)
        {
            var next = settlement.NextUpgrade(state);
            var rankTarget = settlement.RankUpTarget(state);
            if (next == null && rankTarget != null)
            {
                // Fully upgraded: offer to rank up into the next age's version.
                if (state.rankingUp)
                {
                    upgradeButton.interactable = false;
                    upgradeLabel.text = $"<b>Ranking up: {rankTarget.displayName}</b>\n<size=80%><color=#fd6>{state.upgradeProgress:P0}</color></size>";
                }
                else
                {
                    bool can = settlement.CanRankUp(state, out string why);
                    upgradeButton.interactable = can;
                    upgradeLabel.text = $"<b>Rank up: {rankTarget.displayName}</b>\n" +
                                        $"<size=80%>{SubtitleFor(def.rankUpRequirements.cost, can, why)}</size>";
                }
            }
            else if (next == null)
            {
                upgradeButton.interactable = false;
                upgradeLabel.text = "<b>Fully upgraded</b>";
            }
            else if (state.upgrading)
            {
                upgradeButton.interactable = false;
                upgradeLabel.text = $"<b>Upgrading: {next.name}</b>\n<size=80%><color=#fd6>{state.upgradeProgress:P0}</color></size>";
            }
            else
            {
                bool can = settlement.CanUpgrade(state, out string why);
                upgradeButton.interactable = can;
                upgradeLabel.text = $"<b>Upgrade: {next.name}</b>  <size=75%>({state.upgradeLevel + 1}/{def.upgrades.Count})</size>\n" +
                                    $"<size=80%>{SubtitleFor(next.requirements.cost, can, why)}</size>";
            }
        }

        bool damaged = state.IsBuilt && state.health < settlement.StatsOf(state).maxHealth;
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

    // ---------- Troop stances ----------

    private static string StanceText(TroopStance stance) => stance == TroopStance.Defend
        ? "<b>Stance:</b> <color=#8cf>Defend the town</color>"
        : "<b>Stance:</b> <color=#f96>Advance and attack</color>";

    private static string StanceHelp() =>
        "<b>Troop stance</b>\n" +
        "<color=#8cf>Defend</color>: hold the town, fight raiders that come into it, then return.\n" +
        "<color=#f96>Attack</color>: march out and hunt raiders anywhere on the map.";

    private void ToggleSelectedStance()
    {
        var view = controller.Selected;
        if (view == null)
        {
            return;
        }
        view.State.stance = view.State.stance == TroopStance.Defend ? TroopStance.Attack : TroopStance.Defend;
        RefreshAll();
    }

    /// <summary>The stance shared by all troop buildings, or null if they differ / there are none.</summary>
    private TroopStance? CommonStance()
    {
        TroopStance? common = null;
        foreach (var view in controller.Buildings)
        {
            if (view.Definition.trainsUnit == null)
            {
                continue;
            }
            if (common == null)
            {
                common = view.State.stance;
            }
            else if (common != view.State.stance)
            {
                return null;
            }
        }
        return common;
    }

    private void ToggleAllStances()
    {
        var common = CommonStance();
        controller.SetAllStances(common == TroopStance.Defend ? TroopStance.Attack : TroopStance.Defend);
        RefreshAll();
    }

    private void RefreshAllStanceButton()
    {
        bool any = controller.Buildings.Any(v => v.Definition.trainsUnit != null);
        allStanceButton.interactable = any;
        var common = CommonStance();
        allStanceLabel.text = !any ? "<color=#888>No troops yet</color>"
            : common == TroopStance.Defend ? "All troops: <color=#8cf>Defend</color>"
            : common == TroopStance.Attack ? "All troops: <color=#f96>Attack</color>"
            : "All troops: Mixed";
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
        lastMessageTime = Time.unscaledTime;
    }

    /// <summary>Messages stay up for a while after the latest one, then fade so the map is clear.</summary>
    private void FadeMessages()
    {
        if (messageGroup == null)
        {
            return;
        }
        float age = Time.unscaledTime - lastMessageTime;
        messageGroup.alpha = messages.Count == 0 ? 0 : Mathf.Clamp01(1f - (age - MessageHoldSeconds) / 1.5f);
    }

    private CanvasGroup messageGroup;
    private float lastMessageTime = -999f;
    private const float MessageHoldSeconds = 8f;

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
        sb.Append(StatsText(def, 0));
        sb.Append(PlacementText(def));
        if (def.upgrades.Count > 0)
        {
            sb.Append($"<color=#bdb>Upgrades: {string.Join(", ", def.upgrades.Select(u => u.name))}</color>\n");
        }
        AppendMissing(sb, settlement.CanBuild(def, out string missing), missing);
        return sb.ToString().TrimEnd();
    }

    /// <summary>Where a building may go, in words.</summary>
    private static string PlacementText(BuildingDefinition def)
    {
        var sb = new StringBuilder();
        if (def.requiredDeposit != null)
        {
            sb.Append($"<color=#fe8>Build near {def.requiredDeposit.displayName}</color>: needs {def.minDepositTiles}+ tiles " +
                      $"within {def.catchmentRadius}; full output at {def.fullOutputTiles}\n");
        }
        if (def.waterBonus > 0)
        {
            sb.Append($"<color=#8cf>+{def.waterBonus:P0} output near water</color>\n");
        }
        if (def.canBuildOnHills)
        {
            sb.Append("Can be built on hills\n");
        }
        if (def.isTownCenter)
        {
            sb.Append("<color=#fe8>Founds your settlement. Pick the spot carefully!</color>\n");
        }
        return sb.ToString();
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

    /// <summary>A building's numbers at a given upgrade level.</summary>
    private static string StatsText(BuildingDefinition def, int level)
    {
        var s = Settlement.StatsOf(def, level);
        var sb = new StringBuilder();
        foreach (var rate in def.production)
        {
            if (rate.resource != null)
            {
                sb.Append($"Produces {rate.perSecond * s.output:0.##} {rate.resource.displayName}/s\n");
            }
        }
        if (s.housing > 0)
        {
            sb.Append($"Houses {s.housing} people\n");
        }
        if (s.workers > 0)
        {
            sb.Append($"Needs {s.workers} workers\n");
        }
        if (def.trainsUnit != null)
        {
            sb.Append($"Trains up to {s.maxUnits} {def.trainsUnit.displayName}\n");
            var m = s.trainedUnits;
            var bonus = new List<string>();
            if (m.speedMultiplier != 1) bonus.Add($"speed {m.speedMultiplier - 1:+0%;-0%}");
            if (m.attackMultiplier != 1) bonus.Add($"attack {m.attackMultiplier - 1:+0%;-0%}");
            if (m.healthMultiplier != 1) bonus.Add($"health {m.healthMultiplier - 1:+0%;-0%}");
            if (bonus.Count > 0)
            {
                sb.Append($"Troops: {string.Join(", ", bonus)}\n");
            }
        }
        if (s.defense > 0)
        {
            sb.Append($"Defence +{s.defense:0.#}\n");
        }
        return sb.ToString();
    }

    private string UpgradeTooltip()
    {
        var view = controller.Selected;
        if (view == null)
        {
            return "";
        }
        var next = settlement.NextUpgrade(view.State);
        var rankTarget = settlement.RankUpTarget(view.State);
        if (next == null && rankTarget != null)
        {
            var from = view.Definition;
            var now = settlement.StatsOf(view.State);
            var then = Settlement.StatsOf(rankTarget, 0);
            var rank = new StringBuilder($"<b>Rank up into a {rankTarget.displayName}</b>\n");
            rank.Append($"<color=#ccc>Rebuild it in place as the {rankTarget.age.displayName} version. " +
                        "Its upgrades start again from the first tier.</color>\n");
            if (!string.IsNullOrEmpty(rankTarget.description))
            {
                rank.Append($"<color=#ccc>{rankTarget.description}</color>\n");
            }
            rank.Append($"Cost: {CostText(from.rankUpRequirements.cost)}   Time: {from.rankUpTime:0}s\n");
            var changes = new List<string>();
            if (then.housing != now.housing) changes.Add($"housing {now.housing} to {then.housing}");
            if (then.maxHealth != now.maxHealth) changes.Add($"health {now.maxHealth} to {then.maxHealth}");
            if (rankTarget.trainsUnit != null && rankTarget.trainsUnit != from.trainsUnit) changes.Add($"trains {rankTarget.trainsUnit.displayName}s");
            if (then.maxUnits != now.maxUnits && rankTarget.trainsUnit != null) changes.Add($"{then.maxUnits} troops");
            if (changes.Count > 0)
            {
                rank.Append($"<color=#8f8>{string.Join(", ", changes)}</color>\n");
            }
            AppendMissing(rank, settlement.CanRankUp(view.State, out string why), why);
            return rank.ToString().TrimEnd();
        }
        if (next == null)
        {
            return "<b>Fully upgraded</b>";
        }
        var sb = new StringBuilder($"<b>{next.name}</b>\n");
        if (!string.IsNullOrEmpty(next.description))
        {
            sb.Append($"<color=#ccc>{next.description}</color>\n");
        }
        sb.Append($"Cost: {CostText(next.requirements.cost)}   Time: {next.upgradeTime:0}s\n");
        var effects = new List<string>();
        if (next.productionMultiplier != 1) effects.Add($"output {next.productionMultiplier - 1:+0%;-0%}");
        if (next.extraHousing != 0) effects.Add($"+{next.extraHousing} housing");
        if (next.extraHealth != 0) effects.Add($"+{next.extraHealth} health");
        if (next.extraDefense != 0) effects.Add($"+{next.extraDefense:0.#} defence");
        if (next.extraUnits != 0) effects.Add($"+{next.extraUnits} troop");
        if (next.extraCatchment != 0) effects.Add($"+{next.extraCatchment} reach");
        var m = next.trainedUnits;
        if (m != null)
        {
            if (m.speedMultiplier != 1) effects.Add($"troop speed {m.speedMultiplier - 1:+0%;-0%}");
            if (m.attackMultiplier != 1) effects.Add($"troop attack {m.attackMultiplier - 1:+0%;-0%}");
            if (m.healthMultiplier != 1) effects.Add($"troop health {m.healthMultiplier - 1:+0%;-0%}");
        }
        if (effects.Count > 0)
        {
            sb.Append($"<color=#8f8>{string.Join(", ", effects)}</color>\n");
        }
        AppendMissing(sb, settlement.CanUpgrade(view.State, out string missing), missing);
        return sb.ToString().TrimEnd();
    }

    private void UpgradeSelected()
    {
        var view = controller.Selected;
        if (view == null)
        {
            return;
        }
        bool rankUp = settlement.NextUpgrade(view.State) == null && settlement.RankUpTarget(view.State) != null;
        string reason;
        bool started = rankUp ? settlement.TryStartRankUp(view.State, out reason) : settlement.TryStartUpgrade(view.State, out reason);
        if (!started)
        {
            AddMessage(reason.Split('\n')[0]);
        }
        RefreshAll();
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
