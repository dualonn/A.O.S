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
    private TextMeshProUGUI moreText;
    private readonly List<ResourceType> overflowResources = new List<ResourceType>();
    private TextMeshProUGUI messageLog;
    private RectTransform tooltip;
    private TextMeshProUGUI tooltipText;
    private RectTransform selectionPanel;
    private TextMeshProUGUI selectionText;
    private Button repairButton;
    private RectTransform foundingBanner;
    private RectTransform canvasRect;
    // Side panels: each a header, then a scroll list of age sections that fold open and shut.
    private RectTransform buildPanel, researchPanel;
    private RectTransform buildContent, researchContent;
    private LayoutElement buildScroll, researchScroll;
    private readonly Dictionary<Age, CollapsibleSection> buildSections = new Dictionary<Age, CollapsibleSection>();
    private readonly Dictionary<BuildCategory, CollapsibleSection> categorySections = new Dictionary<BuildCategory, CollapsibleSection>();
    private TMP_InputField buildSearch, researchSearch;
    private string buildFilter = "", researchFilter = "";
    private bool groupByType = true;
    private TextMeshProUGUI groupLabel;
    private readonly Dictionary<Age, CollapsibleSection> researchSections = new Dictionary<Age, CollapsibleSection>();
    private CollapsibleSection masterySection;
    private int sectionsAge = -1;
    private Button upgradeButton;
    private TextMeshProUGUI upgradeLabel;
    private Button stanceButton;
    private TextMeshProUGUI stanceLabel;
    private Button allStanceButton;
    private TextMeshProUGUI allStanceLabel;
    private TextMeshProUGUI repairLabel;
    private Button selectTypeButton;
    private RectTransform automationRow;
    private UnitPanel unitPanel;
    private Button priorityButton, autoRepairButton, queueButton, autoRepairNewButton;
    private TextMeshProUGUI priorityLabel, autoRepairLabel, queueLabel, autoRepairNewLabel;
    private TextMeshProUGUI selectTypeLabel;
    private TextMeshProUGUI demolishLabel;
    private float demolishArmedUntil = -1f;
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

    private static readonly float[] Speeds = { 1f, 2f, 3f, 5f, 10f };
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
        controller.UnitSelectionChanged += () => unitPanel.Refresh();
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
        var chips = UIBuilder.Row("Chips", bar, 16);
        UIBuilder.Stretch(chips);
        chips.offsetMin = new Vector2(16, 0);
        chips.offsetMax = new Vector2(-520, 0);

        ageText = Chip(chips, null);
        foreach (var res in settlement.Database.resources)
        {
            var r = res;
            resourceChips[res] = Chip(chips, res.icon);
            // Hover a resource for its name, exact stock and rate (the bar itself stays compact).
            var chipObject = resourceChips[res].transform.parent.gameObject;
            chipObject.AddComponent<Image>().color = Color.clear;
            UIBuilder.OnHover(chipObject, () => tooltipSource = () => ResourceTooltip(r), HideTooltip);
        }
        // Resources that don't fit on the bar fold into one "+N" chip; hover it to see them all.
        moreText = Chip(chips, null);
        var moreChip = moreText.transform.parent.gameObject;
        moreChip.AddComponent<Image>().color = Color.clear;
        UIBuilder.OnHover(moreChip, () => tooltipSource = OverflowTooltip, HideTooltip);
        peopleText = Chip(chips, art != null ? art.peopleIcon : null);
        // Hovering the people chip explains who's working; the bar itself stays short.
        var peopleChip = peopleText.transform.parent.gameObject;
        peopleChip.AddComponent<Image>().color = Color.clear;
        UIBuilder.OnHover(peopleChip, () => tooltipSource = PeopleTooltip, HideTooltip);
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

        canvasRect = (RectTransform)root;
        var db = settlement.Database;

        // Build menu (left): search, then folding sections by type (or by age), in a scroll list.
        buildPanel = SidePanel("BuildPanel", root, left: true);
        var buildHeader = UIBuilder.Row("Header", buildPanel, 8);
        UIBuilder.Text("Title", buildHeader, "<b>Build</b>", 24).gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        var groupButton = UIBuilder.Button("Grouping", buildHeader, ToggleGrouping, out groupLabel, 30, 14);
        groupButton.GetComponent<LayoutElement>().preferredWidth = 120;
        groupLabel.alignment = TextAlignmentOptions.Center;
        UIBuilder.OnHover(groupButton.gameObject, () => tooltipSource = () => "<b>Grouping</b>\nBy type: housing, food, industry, military... Older versions you've outgrown are tucked away (search still finds them).\nBy age: everything, age by age.", HideTooltip);
        buildSearch = UIBuilder.SearchField("Search", buildPanel, "Search buildings...", text => { buildFilter = text.Trim(); RefreshBuildButtons(); });
        buildContent = UIBuilder.ScrollList("Buildings", buildPanel, out buildScroll);
        groupByType = PlayerPrefs.GetInt("AOS.BuildGroupByType", 1) == 1;
        foreach (BuildCategory category in System.Enum.GetValues(typeof(BuildCategory)))
        {
            var section = new CollapsibleSection(category.ToString(), buildContent, CategoryName(category), CategoryAccent(category));
            section.Toggled += _ => FitSidePanels();
            categorySections[category] = section;
        }
        foreach (var age in db.ages)
        {
            buildSections[age] = AgeSection(age, buildContent);
        }
        foreach (var def in db.buildings)
        {
            if (def == null) continue;
            var d = def;
            var button = UIBuilder.Button(def.id, buildContent, () => controller.Placer.Begin(d), out var label, 56, 18, def.icon);
            UIBuilder.OnHover(button.gameObject, () => tooltipSource = () => BuildingTooltip(d), HideTooltip);
            buildButtons.Add((def, button, label));
        }
        ApplyGrouping();
        UIBuilder.Text("Hint", buildPanel, "<size=80%><color=#aaa>Click to place. Shift keeps placing.\nRight click / Esc cancels.</color></size>", 16);
        autoRepairNewButton = UIBuilder.Button("AutoRepairNew", buildPanel, ToggleAutoRepairNew, out autoRepairNewLabel, 34, 15);
        UIBuilder.OnHover(autoRepairNewButton.gameObject, () => tooltipSource = AutomationHelp, HideTooltip);

        // Ages and research (right): advancing stays on top, research folds by age below.
        researchPanel = SidePanel("ResearchPanel", root, left: false);
        UIBuilder.Text("AgeTitle", researchPanel, "<b>Age</b>", 24);
        // Progress through your age (ages are grown into, not bought: see the tooltip).
        advanceButton = UIBuilder.Button("AgeProgress", researchPanel, () => { }, out advanceLabel, 60);
        UIBuilder.OnHover(advanceButton.gameObject, () => tooltipSource = AgeTooltip, HideTooltip);
        UIBuilder.Text("Title", researchPanel, "<b>Research</b>", 24);
        researchSearch = UIBuilder.SearchField("Search", researchPanel, "Search research...", text => { researchFilter = text.Trim(); RefreshResearchButtons(); });
        researchContent = UIBuilder.ScrollList("Research", researchPanel, out researchScroll);
        foreach (var age in db.ages)
        {
            var section = AgeSection(age, researchContent);
            researchSections[age] = section;
            foreach (var tech in db.technologies)
            {
                if (tech == null || tech.IsLevelled || (tech.age != null ? tech.age : db.ages[0]) != age)
                {
                    continue;
                }
                AddResearchButton(tech, section.Body);
            }
        }
        // Mastery: research taken in levels, for small steady gains once the ages' research runs out.
        masterySection = new CollapsibleSection("Mastery", researchContent, "Mastery", new Color(0.55f, 0.85f, 0.95f));
        masterySection.Toggled += _ => FitSidePanels();
        foreach (var tech in db.technologies)
        {
            if (tech != null && tech.IsLevelled) AddResearchButton(tech, masterySection.Body);
        }
        // Expansion goals: three at a time, open by default.
        goalsSection = new CollapsibleSection("Goals", researchContent, "Goals", new Color(0.6f, 0.95f, 0.55f));
        goalsSection.Toggled += _ => FitSidePanels();
        goalsSection.Root.SetSiblingIndex(0);
        // The journal: lore from lore stones and blessings from shrines, folded away under research.
        journalSection = new CollapsibleSection("Journal", researchContent, "Journal", new Color(0.95f, 0.82f, 0.5f));
        journalSection.SetOpen(false);

        // Dungeon progress (top centre, only while a party is underground)
        dungeonPanel = UIBuilder.VerticalPanel("DungeonPanel", root, 8, 4);
        UIBuilder.Anchor(dungeonPanel, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -52), new Vector2(380, 0));
        dungeonText = UIBuilder.Text("Info", dungeonPanel, "", 18, TMPro.TextAlignmentOptions.Center);
        dungeonButton = UIBuilder.Button("View", dungeonPanel, ToggleDungeonView, out dungeonButtonLabel, 30, 16);
        dungeonPanel.gameObject.SetActive(false);

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
        // Priority and automation: small toggles side by side.
        automationRow = UIBuilder.Row("Automation", selectionPanel, 6);
        automationRow.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
        priorityButton = SmallToggle("Priority", CyclePriority, out priorityLabel, PriorityHelp);
        autoRepairButton = SmallToggle("AutoRepair", ToggleAutoRepair, out autoRepairLabel, AutomationHelp);
        queueButton = SmallToggle("Queue", ToggleQueue, out queueLabel, AutomationHelp);
        selectTypeButton = UIBuilder.Button("SelectType", selectionPanel, SelectAllOfType, out selectTypeLabel, 40);
        UIBuilder.OnHover(selectTypeButton.gameObject, () => tooltipSource = SelectionHelp, HideTooltip);
        UIBuilder.Button("Demolish", selectionPanel, DemolishSelected, out demolishLabel, 44).image.color = new Color(0.45f, 0.18f, 0.18f);
        demolishLabel.text = "Demolish (no refund)";
        selectionPanel.gameObject.SetActive(false);

        // Selected troops (same spot as the building panel; only one shows at a time)
        unitPanel = new UnitPanel(controller, root, source => tooltipSource = source, HideTooltip);

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

        // The regular refresh is spread over a few frames, one part per frame, so it never causes a hitch.
        refreshTimer -= Time.unscaledDeltaTime;
        if (refreshTimer <= 0 && refreshPart < 0)
        {
            refreshTimer = 0.25f;
            refreshPart = 0;
        }
        if (refreshPart >= 0)
        {
            RefreshPart(refreshPart);
            refreshPart = refreshPart + 1 < RefreshParts ? refreshPart + 1 : -1;
        }
        UpdateTooltip();
        FadeMessages();
    }

    private const int RefreshParts = 5;
    private int refreshPart = -1;
    private static readonly string[] RefreshPartNames = { "HUD.TopBar", "HUD.Build", "HUD.Research", "HUD.Selection", "HUD.Panels" };

    private void RefreshPart(int part)
    {
        UnityEngine.Profiling.Profiler.BeginSample(RefreshPartNames[part]);
        switch (part)
        {
            case 0:
                RefreshTopBar();
                foundingBanner.gameObject.SetActive(!settlement.IsFounded);
                RefreshAllStanceButton();
                break;
            case 1: RefreshBuildButtons(); break;
            case 2: RefreshResearchButtons(); break;
            case 3: RefreshSelection(); unitPanel.Refresh(); break;
            default:
                RefreshAutoRepairNew();
                RefreshJournal();
                RefreshGoals();
                RefreshDungeon();
                break;
        }
        UnityEngine.Profiling.Profiler.EndSample();
    }

    private void RefreshAll()
    {
        RefreshTopBar();
        foundingBanner.gameObject.SetActive(!settlement.IsFounded);
        RefreshAllStanceButton();
        RefreshBuildButtons();
        RefreshResearchButtons();
        RefreshSelection();
        unitPanel.Refresh();
        RefreshAutoRepairNew();
        RefreshJournal();
        RefreshGoals();
        RefreshDungeon();
    }

    // ---------- Dungeon progress ----------

    private RectTransform dungeonPanel;
    private TMPro.TextMeshProUGUI dungeonText, dungeonButtonLabel;
    private UnityEngine.UI.Button dungeonButton;
    private DungeonRun shownRun;

    private void RefreshDungeon()
    {
        DungeonRun run = controller.Viewing;
        if (run == null)
            foreach (var r in DungeonRun.Active)
                if (r != null && r.Party.Count > 0) { run = r; break; }
        shownRun = run;
        dungeonPanel.gameObject.SetActive(run != null);
        if (run == null) return;
        // one block per room: done, current, still dark
        var bar = new System.Text.StringBuilder();
        for (int i = 0; i < run.Rooms.Count; i++)
        {
            string colour = i < run.Current ? "#8f8" : i == run.Current ? "#fd6" : "#666";
            // the throne room's block is purple until the party reaches it
            if (run.Rooms[i].throne && i > run.Current) colour = "#a070e0";
            bar.Append($"<color={colour}>{(run.Rooms[i].throne ? "[ K ]" : "[ ]")}</color> ");
        }
        string where = run.Current >= run.Rooms.Count ? "The hoard!" : run.Rooms[run.Current].throne ? "Throne room" : $"Room {run.Current + 1} of {run.Rooms.Count}";
        dungeonText.text = $"<b><color=#c9a6ff>{run.Title}</color></b>\n{bar}\n<size=85%>{where}  ·  {run.Party.Count} troops below</size>";
        dungeonButtonLabel.text = controller.Viewing == run ? "Back to the surface" : "Go down to them";
    }

    private void ToggleDungeonView()
    {
        if (shownRun == null) return;
        if (controller.Viewing == shownRun) controller.ShowSurface();
        else controller.FocusDungeon(shownRun);
    }

    // ---------- Goals ----------

    private CollapsibleSection goalsSection;
    private readonly List<TMPro.TextMeshProUGUI> goalLabels = new List<TMPro.TextMeshProUGUI>();
    private string goalsShown;

    private void RefreshGoals()
    {
        if (goalsSection == null) return;
        var goals = settlement.State.goals;
        var sb = new System.Text.StringBuilder();
        foreach (var g in goals) { var (h, n) = settlement.GoalProgress(g); sb.Append(g.kind).Append(g.target).Append(h).Append('|'); }
        string key = sb.ToString();
        if (key == goalsShown) return;
        goalsShown = key;
        while (goalLabels.Count < goals.Count)
        {
            int index = goalLabels.Count;
            var row = UIBuilder.Button("Goal" + index, goalsSection.Body, () => { }, out var label, 30, 15);
            UIBuilder.OnHover(row.gameObject, () => tooltipSource = () => GoalTip(index), HideTooltip);
            goalLabels.Add(label);
        }
        for (int i = 0; i < goalLabels.Count; i++)
        {
            bool shown = i < goals.Count;
            goalLabels[i].transform.parent.gameObject.SetActive(shown);
            if (!shown) continue;
            var (have, need) = settlement.GoalProgress(goals[i]);
            string count = need > 1 ? $" <color=#aaa>{have}/{need}</color>" : "";
            goalLabels[i].text = $"<color=#bf9>{settlement.GoalTitle(goals[i])}</color>{count}";
        }
        goalsSection.SetNote(settlement.State.goalsDone > 0 ? $"{settlement.State.goalsDone} done" : "");
    }

    private string GoalTip(int index)
    {
        var goals = settlement.State.goals;
        if (index >= goals.Count) return "";
        var g = goals[index];
        var (have, need) = settlement.GoalProgress(g);
        return $"<b>{settlement.GoalTitle(g)}</b> <color=#aaa>({have}/{need})</color>\n{settlement.GoalHint(g)}\n" +
               $"<color=#8f8>Reward: {Settlement.Describe(settlement.GoalReward(g))}, and a rare or better item.</color>";
    }

    // ---------- Journal ----------

    private CollapsibleSection journalSection;
    private int journalShown = -1;

    private void RefreshJournal()
    {
        var blessings = settlement.Blessings();
        int count = settlement.State.journal.Count + blessings.Count * 1000;
        if (journalSection == null || count == journalShown) return;
        journalShown = count;
        foreach (Transform child in journalSection.Body) Destroy(child.gameObject);
        int lore = settlement.State.journal.Count;
        journalSection.SetNote(lore + blessings.Count > 0 ? $"{lore}/{Lore.Entries.Length} lore, {blessings.Count} blessings" : "");
        if (lore + blessings.Count == 0)
        {
            UIBuilder.Text("Empty", journalSection.Body, "<size=85%><color=#aaa>Nothing yet. Send units to read lore stones, and heroes to kneel at shrines.</color></size>", 16);
            return;
        }
        foreach (var b in blessings)
        {
            var tech = b;
            var row = UIBuilder.Button(tech.id, journalSection.Body, () => { }, out var label, 30, 15);
            label.text = $"<color=#9df>{tech.displayName}</color>";
            UIBuilder.OnHover(row.gameObject, () => tooltipSource = () => $"<b>{tech.displayName}</b>\n{tech.description}", HideTooltip);
        }
        foreach (int id in settlement.State.journal)
        {
            if (id < 0 || id >= Lore.Entries.Length) continue;
            var entry = Lore.Entries[id];
            var row = UIBuilder.Button("Lore" + id, journalSection.Body, () => { }, out var label, 30, 15);
            label.text = $"<color=#fd6>{entry.title}</color>";
            UIBuilder.OnHover(row.gameObject, () => tooltipSource = () => $"<b>{entry.title}</b>\n<i>{entry.text}</i>", HideTooltip);
        }
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

    private static readonly string[] BasicResources = { "Food", "Wood", "Stone" };

    /// <summary>
    /// Keep the bar to what fits: food, wood and stone always, then the newest resources (the ones
    /// this age is about), and the rest folded into the "+N" chip.
    /// </summary>
    private void FitResourceChips()
    {
        var row = (RectTransform)moreText.transform.parent.parent;
        var layout = row.GetComponent<HorizontalLayoutGroup>();
        float available = row.rect.width, spacing = layout != null ? layout.spacing : 0;
        float Width(TextMeshProUGUI t) => LayoutUtility.GetPreferredWidth((RectTransform)t.transform.parent) + spacing;

        float used = 70f;   // room for the "+N" chip
        foreach (var t in new[] { ageText, peopleText, waveText, researchText })
            if (t != null && t.transform.parent.gameObject.activeSelf) used += Width(t);

        var shown = resourceChips.Where(p => p.Value.transform.parent.gameObject.activeSelf).Select(p => p.Key)
            .OrderBy(r => System.Array.IndexOf(BasicResources, r.id) is int i && i >= 0 ? i : 99)
            .ThenByDescending(r => r.unlockedIn != null ? r.unlockedIn.order : 0)
            .ToList();
        overflowResources.Clear();
        foreach (var res in shown)
        {
            float w = Width(resourceChips[res]);
            if (used + w <= available || System.Array.IndexOf(BasicResources, res.id) >= 0) used += w;
            else
            {
                resourceChips[res].transform.parent.gameObject.SetActive(false);
                overflowResources.Add(res);
            }
        }
        overflowResources.Sort((a, b) => settlement.Database.resources.IndexOf(a).CompareTo(settlement.Database.resources.IndexOf(b)));
        moreText.transform.parent.gameObject.SetActive(overflowResources.Count > 0);
        moreText.text = $"<b>+{overflowResources.Count}</b>";
    }

    private string OverflowTooltip()
    {
        var sb = new StringBuilder("<b>More resources</b>");
        foreach (var res in overflowResources)
        {
            float rate = rates.TryGetValue(res, out float r) ? r : 0;
            string rateText = Mathf.Abs(rate) < 0.05f ? "" : rate > 0 ? $"  <color=#8f8>+{rate:0.0}/s</color>" : $"  <color=#f88>{rate:0.0}/s</color>";
            sb.Append($"\n{res.displayName}: <b>{Short(settlement.GetAmount(res))}</b>{rateText}");
        }
        return sb.ToString();
    }

    /// <summary>A stock in a few characters: 950, 9,870, 23.8k, 1.2M.</summary>
    private static string Short(float value)
    {
        float v = Mathf.Floor(value);
        if (v < 10000f) return v.ToString("#,0");
        if (v < 1000000f) return (v / 1000f).ToString(v < 100000f ? "0.0" : "0") + "k";
        return (v / 1000000f).ToString("0.0") + "M";
    }

    private string ResourceTooltip(ResourceType res)
    {
        float rate = rates.TryGetValue(res, out float r) ? r : 0;
        string rateText = Mathf.Abs(rate) < 0.05f ? "<color=#aaa>steady</color>" :
            rate > 0 ? $"<color=#8f8>+{rate:0.00} a second</color>" : $"<color=#f88>{rate:0.00} a second</color>";
        return $"<b>{res.displayName}</b>\n{Mathf.FloorToInt(settlement.GetAmount(res)):#,0} in store, {rateText}";
    }

    /// <summary>An icon followed by a text value, added to a row. Returns the text.</summary>
    private static TextMeshProUGUI Chip(Transform row, Sprite icon)
    {
        var chip = UIBuilder.Row("Chip", row, 6);
        if (icon != null)
        {
            UIBuilder.Icon("Icon", chip, icon, 34);
        }
        var text = UIBuilder.Text("Value", chip, "", 22, TextAlignmentOptions.MidlineLeft);
        text.lineSpacing = -28;   // the small rate line tucks up under the stock
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }

    private void RefreshTopBar()
    {
        ageText.text = $"<b>{settlement.CurrentAge?.displayName}</b> <size=75%><color=#aaa>{settlement.CurrentAgeProgress:P0}</color></size>";

        var visible = new HashSet<ResourceType>(VisibleResources());
        foreach (var pair in resourceChips)
        {
            var res = pair.Key;
            float rate = rates.TryGetValue(res, out float r) ? r : 0;
            float amount = settlement.GetAmount(res);
            // Nothing in stock and nothing coming in: leave it off the bar (hover the build menu for costs).
            bool show = visible.Contains(res) && (amount >= 1f || Mathf.Abs(rate) >= 0.05f);
            pair.Value.transform.parent.gameObject.SetActive(show);
            if (!show) continue;
            // The stock, and the rate small underneath, so a full bar of resources still fits.
            string rateText = Mathf.Abs(rate) < 0.05f ? "" :
                rate > 0 ? $"\n<color=#8f8><size=62%>+{rate:0.0}/s</size></color>" : $"\n<color=#f88><size=62%>{rate:0.0}/s</size></color>";
            string name = res.icon == null ? res.displayName + " " : "";
            pair.Value.text = $"{name}<b>{Short(amount)}</b>{rateText}";
        }
        FitResourceChips();

        // People / room, plus one small note: idle people, or how many jobs are going unfilled.
        int idle = settlement.Population - settlement.WorkersNeeded;
        string note = idle > 0 ? $" <size=70%><color=#9c9>{idle} idle</color></size>"
            : idle < 0 ? $" <size=70%><color=#fc6>{-idle} short</color></size>" : "";
        peopleText.text = $"<b>{settlement.Population}/{settlement.Housing}</b>{note}";
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

    /// <summary>A side panel under the top bar: grows with its contents, up to the screen's height.</summary>
    private static RectTransform SidePanel(string name, Transform root, bool left)
    {
        var panel = UIBuilder.VerticalPanel(name, root);
        float x = left ? 0 : 1;
        UIBuilder.Anchor(panel, new Vector2(x, 1), new Vector2(x, 1), new Vector2(x, 1), new Vector2(left ? 10 : -10, -66), new Vector2(left ? 310 : 330, 0));
        return panel;
    }

    private static readonly Color[] AgeAccents =
    {
        new Color(0.85f, 0.75f, 0.55f), new Color(0.95f, 0.65f, 0.35f), new Color(0.75f, 0.8f, 0.9f),
        new Color(0.95f, 0.85f, 0.45f), new Color(0.7f, 0.85f, 0.65f), new Color(0.8f, 0.7f, 0.95f),
        new Color(0.6f, 0.82f, 0.98f), new Color(0.85f, 0.55f, 0.45f),   // Enlightenment, Industrial
        new Color(0.7f, 0.75f, 0.5f), new Color(0.6f, 0.9f, 0.7f),       // Modern, Electric
        new Color(0.45f, 0.85f, 1f), new Color(0.78f, 0.6f, 1f),         // Information, Arcane
        new Color(0.95f, 0.85f, 0.5f),                                   // Astral
    };

    private CollapsibleSection AgeSection(Age age, Transform parent)
    {
        var accent = AgeAccents[Mathf.Clamp(age.order, 0, AgeAccents.Length - 1)];
        var section = new CollapsibleSection(age.name, parent, age.displayName, accent);
        section.Toggled += _ => FitSidePanels();
        return section;
    }

    /// <summary>Open the current age's sections and fold the rest, whenever the age changes.</summary>
    private void SyncSectionsToAge()
    {
        if (sectionsAge == settlement.State.ageIndex)
        {
            return;
        }
        sectionsAge = settlement.State.ageIndex;
        foreach (var sections in new[] { buildSections, researchSections })
        {
            foreach (var pair in sections)
            {
                pair.Value.SetOpen(pair.Key.order == sectionsAge);
            }
        }
    }

    /// <summary>Let each side panel grow with its list, but stop at the bottom of the screen (then it scrolls).</summary>
    private void FitSidePanels()
    {
        FitSidePanel(buildPanel, buildScroll, buildContent);
        FitSidePanel(researchPanel, researchScroll, researchContent);
    }

    private void FitSidePanel(RectTransform panel, LayoutElement scroll, RectTransform content)
    {
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        float wanted = LayoutUtility.GetPreferredHeight(content);
        float rest = LayoutUtility.GetPreferredHeight(panel) - scroll.preferredHeight;
        float room = canvasRect.rect.height - 66 - 14 - rest;
        float height = Mathf.Max(80, Mathf.Min(wanted, room));
        if (!Mathf.Approximately(height, scroll.preferredHeight))
        {
            scroll.preferredHeight = height;
            LayoutRebuilder.MarkLayoutForRebuild(panel);
        }
    }

    private void RefreshBuildButtons()
    {
        SyncSectionsToAge();
        bool searching = buildFilter.Length > 0;
        foreach (var (def, button, label) in buildButtons)
        {
            int ageOrder = def.age != null ? def.age.order : 0;
            bool locked = ageOrder > settlement.State.ageIndex + 1;
            // Before founding only the town center is offered; afterwards it's hidden once built.
            bool founded = settlement.IsFounded;
            bool hidden = locked || (founded ? def.isTownCenter && settlement.CountAll(def) > 0 : !def.isTownCenter);
            if (searching)
            {
                hidden |= !Matches(def, buildFilter);
            }
            else if (groupByType)
            {
                hidden |= Outdated(def); // grouped by type, show only the newest version of each
            }
            if (button.gameObject.activeSelf == hidden) button.gameObject.SetActive(!hidden);
            bool can = settlement.CanBuild(def, out string missing);
            button.interactable = can;   // (section headers count the ready ones, so this runs for all shown)
            if (hidden || !button.gameObject.activeInHierarchy) continue;   // folded away: no text to redraw
            bool placingThis = controller.Placer.Current == def;
            string ageTag = groupByType && def.age != null
                ? $"  <size=70%><color=#{ColorUtility.ToHtmlStringRGB(AgeAccents[Mathf.Clamp(ageOrder, 0, AgeAccents.Length - 1)])}>{def.age.displayName.Replace(" Age", "")}</color></size>" : "";
            string text = $"<b>{def.displayName}</b>{ageTag}{(placingThis ? " <color=#8f8>(placing)</color>" : "")}\n" +
                          $"<size=80%>{SubtitleFor(def.requirements.cost, can, missing)}</size>";
            if (label.text != text) label.text = text;
        }

        // Section headers: only the current grouping's, hidden when empty, with how many can be built.
        foreach (var pair in categorySections) RefreshBuildSection(pair.Value, groupByType, searching, null);
        foreach (var pair in buildSections) RefreshBuildSection(pair.Value, !groupByType, searching, pair.Key);
        FitSidePanels();
    }

    private void RefreshBuildSection(CollapsibleSection section, bool active, bool searching, Age age)
    {
        if (!active)
        {
            section.SetVisible(false);
            return;
        }
        int shown = 0, ready = 0;
        foreach (var (_, button, _) in buildButtons)
        {
            if (button.transform.parent != section.Body || !button.gameObject.activeSelf) continue;
            shown++;
            if (button.interactable) ready++;
        }
        section.SetVisible(shown > 0);
        if (searching && shown > 0 && !section.Open)
        {
            section.SetOpen(true); // show every match while searching
        }
        bool next = age != null && age.order == settlement.State.ageIndex + 1;
        section.SetNote(next ? "next age" : ready > 0 ? $"{ready} ready" : $"{shown}");
    }

    // ---------- Build menu grouping and search ----------

    private enum BuildCategory { Settlement, Housing, Food, Materials, Industry, Military, Defence, Walls }

    private static string CategoryName(BuildCategory c) => c switch
    {
        BuildCategory.Walls => "Walls and Gates",
        _ => c.ToString(),
    };

    private static Color CategoryAccent(BuildCategory c) => c switch
    {
        BuildCategory.Settlement => new Color(0.95f, 0.8f, 0.45f),
        BuildCategory.Housing => new Color(0.9f, 0.7f, 0.55f),
        BuildCategory.Food => new Color(0.65f, 0.85f, 0.45f),
        BuildCategory.Materials => new Color(0.75f, 0.65f, 0.5f),
        BuildCategory.Industry => new Color(0.95f, 0.6f, 0.35f),
        BuildCategory.Military => new Color(0.95f, 0.45f, 0.4f),
        BuildCategory.Defence => new Color(0.6f, 0.75f, 0.95f),
        _ => new Color(0.75f, 0.75f, 0.78f),
    };

    /// <summary>What kind of building this is, worked out from what it does.</summary>
    private static BuildCategory CategoryOf(BuildingDefinition def)
    {
        if (def.IsWall) return BuildCategory.Walls;
        if (def.isTownCenter) return BuildCategory.Settlement;
        if (def.trainsUnit != null) return BuildCategory.Military;
        if (def.towerRange > 0) return BuildCategory.Defence;
        if (def.production.Count == 0) return def.housing > 0 ? BuildCategory.Housing : BuildCategory.Settlement;
        foreach (var rate in def.production)
        {
            if (rate.resource == null) continue;
            if (rate.resource.id == "Food") return BuildCategory.Food;
            if (rate.resource.id == "Wood" || rate.resource.id == "Stone") return BuildCategory.Materials;
        }
        return BuildCategory.Industry;
    }

    /// <summary>An older version you've outgrown: it ranks up into something you can already build.</summary>
    private bool Outdated(BuildingDefinition def)
    {
        var next = def.rankUpTo;
        return next != null && (next.age == null || next.age.order <= settlement.State.ageIndex);
    }

    private static bool Matches(BuildingDefinition def, string filter)
    {
        var c = System.StringComparison.OrdinalIgnoreCase;
        return def.displayName.IndexOf(filter, c) >= 0
               || (def.description != null && def.description.IndexOf(filter, c) >= 0)
               || CategoryName(CategoryOf(def)).IndexOf(filter, c) >= 0
               || def.production.Exists(r => r.resource != null && r.resource.displayName.IndexOf(filter, c) >= 0);
    }

    /// <summary>Move every build button into the sections of the current grouping.</summary>
    private void ApplyGrouping()
    {
        groupLabel.text = groupByType ? "By type" : "By age";
        var db = settlement.Database;
        var ordered = buildButtons.OrderBy(b => groupByType ? (int)CategoryOf(b.def) : 0)
            .ThenBy(b => b.def.age != null ? b.def.age.order : 0)
            .ThenBy(b => db.buildings.IndexOf(b.def));
        foreach (var (def, button, _) in ordered)
        {
            var section = groupByType ? categorySections[CategoryOf(def)]
                : buildSections[def.age != null ? def.age : db.ages[0]];
            button.transform.SetParent(section.Body, false);
            button.transform.SetAsLastSibling();
        }
    }

    private void ToggleGrouping()
    {
        groupByType = !groupByType;
        PlayerPrefs.SetInt("AOS.BuildGroupByType", groupByType ? 1 : 0);
        ApplyGrouping();
        RefreshBuildButtons();
    }

    private void AddResearchButton(Technology tech, Transform parent)
    {
        var button = UIBuilder.Button(tech.id, parent, () => TryResearch(tech), out var label);
        UIBuilder.OnHover(button.gameObject, () => tooltipSource = () => TechTooltip(tech), HideTooltip);
        researchButtons.Add((tech, button, label));
    }

    private void RefreshResearchButtons()
    {
        int levels = 0, maxLevels = 0, masteryShown = 0;
        foreach (var (tech, button, label) in researchButtons)
        {
            bool done = settlement.IsFullyResearched(tech);
            bool locked = tech.age != null && tech.age.order > settlement.State.ageIndex;
            bool match = researchFilter.Length == 0
                || tech.displayName.IndexOf(researchFilter, System.StringComparison.OrdinalIgnoreCase) >= 0
                || (tech.description != null && tech.description.IndexOf(researchFilter, System.StringComparison.OrdinalIgnoreCase) >= 0);
            bool show = !done && !locked && match;
            if (button.gameObject.activeSelf != show) button.gameObject.SetActive(show);
            string level = "";
            if (tech.IsLevelled)
            {
                int at = settlement.ResearchLevel(tech);
                levels += at;
                maxLevels += settlement.MaxLevel(tech);
                if (show) masteryShown++;
                level = $"  <size=80%><color=#9df>Lv {at}/{settlement.MaxLevel(tech)}</color></size>";
            }
            // Hidden, or in a folded section: nothing to redraw (it's refreshed again when shown).
            if (!show || !button.gameObject.activeInHierarchy) continue;
            bool current = settlement.State.researchingId == tech.id;
            bool can = settlement.CanResearch(tech, out string missing);
            button.interactable = can;
            string sub = current
                ? $"<color=#8cf>Researching {settlement.State.researchProgress:P0}</color>"
                : SubtitleFor(settlement.ResearchCost(tech), can, missing) + $" <color=#aaa>({settlement.ResearchTime(tech):0}s)</color>";
            string text = $"<b>{tech.displayName}</b>{level}\n<size=80%>{sub}</size>";
            if (label.text != text) label.text = text;   // re-laying out text is the costly part
        }
        masterySection.SetVisible(masteryShown > 0 || levels > 0);
        masterySection.SetNote(levels >= maxLevels ? "all done" : $"{levels}/{maxLevels} levels");
        foreach (var pair in researchSections)
        {
            int total = 0, done = 0, shown = 0;
            foreach (var (tech, button, _) in researchButtons)
            {
                if (button.transform.parent != pair.Value.Body) continue;
                total++;
                if (settlement.HasResearched(tech)) done++;
                if (button.gameObject.activeSelf) shown++;
            }
            pair.Value.SetVisible(shown > 0 || (done > 0 && pair.Key.order <= settlement.State.ageIndex));
            pair.Value.SetNote(shown == 0 ? "all done" : $"{done}/{total} done");
        }

        var next = settlement.NextAge;
        float progress = settlement.CurrentAgeProgress;
        int filled = Mathf.RoundToInt(progress * 24);
        string bar = $"<color=#8f8>{new string('|', filled)}</color><color=#555>{new string('|', 24 - filled)}</color>";
        string after = next == null ? "the latest age" : $"next: the {next.displayName}";
        advanceLabel.text = $"<b>{settlement.CurrentAge?.displayName}</b>  {progress:P0}\n<size=80%>{bar}  <color=#aaa>{after}</color></size>";
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
        if (Time.unscaledTime > demolishArmedUntil)
        {
            demolishLabel.text = controller.Selection.Count > 1 ? $"Demolish all {controller.Selection.Count} (no refund)" : "Demolish (no refund)";
        }
        RefreshAutomationRow();
        if (controller.Selection.Count > 1)
        {
            RefreshGroupSelection();
            return;
        }
        var state = view.State;
        var def = view.Definition;

        int sameType = controller.AllOfType(def, false).Count;
        selectTypeButton.gameObject.SetActive(sameType > 1);
        selectTypeLabel.text = $"Select all {def.displayName} ({sameType})";

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
        if (state.IsBuilt && state.health > 0)
        {
            var live = settlement.StatsOf(state);
            if (live.workers > 0)
            {
                int filled = Mathf.RoundToInt(settlement.StaffingOf(state) * live.workers);
                string color = filled >= live.workers ? "#8f8" : filled > 0 ? "#fe8" : "#f88";
                sb.Append(state.Paused ? "<color=#aaa>Paused: no workers, does nothing</color>\n"
                    : $"Workers: <color={color}>{filled}/{live.workers}</color>\n");
            }
            if (def.inputs.Count > 0 && !state.Paused)
            {
                float supply = settlement.InputSupplyOf(state);
                if (supply < 0.999f)
                {
                    float run = Mathf.Lerp(def.runsWithoutInputs, 1f, supply);
                    sb.Append($"<color=#fc6>Short of inputs: running at {run:P0}</color>\n");
                }
            }
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

    private void RepairSelected()
    {
        if (controller.Selection.Count > 1)
        {
            RepairGroup();
            return;
        }
        var view = controller.Selected;
        if (view != null && !settlement.TryRepair(view.State, out string reason))
        {
            AddMessage(reason);
        }
        RefreshAll();
    }

    private void DemolishSelected()
    {
        var picked = new List<BuildingView>(controller.Selection);
        if (picked.Count == 0)
        {
            return;
        }
        if (picked.Count > 1 && Time.unscaledTime > demolishArmedUntil)
        {
            // Two clicks for a whole group, so a stray click can't flatten the town.
            demolishArmedUntil = Time.unscaledTime + 3f;
            demolishLabel.text = $"<color=#fc8>Click again to demolish all {picked.Count}</color>";
            return;
        }
        demolishArmedUntil = -1f;
        foreach (var view in picked)
        {
            settlement.Demolish(view.State);
        }
    }

    private void SelectAllOfType()
    {
        var view = controller.Selected;
        if (view != null)
        {
            controller.SetSelection(controller.AllOfType(view.Definition, false));
        }
    }

    private static string SelectionHelp() =>
        "<b>Selecting buildings</b>\n" +
        "Drag on the ground to box-select.\n" +
        "Shift + click adds or removes one. Shift + drag adds a box.\n" +
        "Double-click selects every one of that type on screen; Ctrl + double-click, on the whole map.";

    // ---------- Group selection ----------

    private enum Step { None, Upgrade, RankUp }

    /// <summary>A building's next upgrade step (an upgrade tier, or ranking up) and its cost.</summary>
    private Step NextStep(BuildingState state, out List<ResourceAmount> cost)
    {
        var next = settlement.NextUpgrade(state);
        if (next != null)
        {
            cost = next.requirements.cost;
            return Step.Upgrade;
        }
        var def = settlement.Database.GetBuilding(state.definitionId);
        if (def != null && settlement.RankUpTarget(state) != null)
        {
            cost = def.rankUpRequirements.cost;
            return Step.RankUp;
        }
        cost = null;
        return Step.None;
    }

    private bool CanStep(BuildingState state, Step step, out string why) =>
        step == Step.Upgrade ? settlement.CanUpgrade(state, out why) : settlement.CanRankUp(state, out why);

    private bool StartStep(BuildingState state, Step step, out string why) =>
        step == Step.Upgrade ? settlement.TryStartUpgrade(state, out why) : settlement.TryStartRankUp(state, out why);

    private static int CostSize(List<ResourceAmount> cost)
    {
        int total = 0;
        if (cost != null) foreach (var c in cost) total += c.amount;
        return total;
    }

    /// <summary>Selected buildings that could take their next step, cheapest first.</summary>
    private List<(BuildingState state, Step step, List<ResourceAmount> cost)> UpgradeCandidates(out int busy, out int done)
    {
        busy = done = 0;
        var list = new List<(BuildingState, Step, List<ResourceAmount>)>();
        foreach (var view in controller.Selection)
        {
            var step = NextStep(view.State, out var cost);
            if (step == Step.None) done++;
            else if (view.State.upgrading) busy++;
            else list.Add((view.State, step, cost));
        }
        list.Sort((a, b) => CostSize(a.Item3).CompareTo(CostSize(b.Item3)));
        return list;
    }

    private void RefreshGroupSelection()
    {
        var picked = controller.Selection;
        selectTypeButton.gameObject.SetActive(false);

        // Header: how many of what, and what state they're in.
        var counts = new Dictionary<string, int>();
        int building = 0, damaged = 0, ruined = 0;
        foreach (var view in picked)
        {
            counts.TryGetValue(view.Definition.displayName, out int n);
            counts[view.Definition.displayName] = n + 1;
            if (!view.State.IsBuilt) building++;
            else if (view.State.health <= 0) ruined++;
            else if (view.State.health < settlement.StatsOf(view.State).maxHealth) damaged++;
        }
        var kinds = counts.OrderByDescending(p => p.Value).Select(p => $"{p.Value} {p.Key}");
        var sb = new StringBuilder($"<b>{picked.Count} buildings</b>\n<size=85%>{string.Join(", ", kinds)}</size>");
        var notes = new List<string>();
        if (building > 0) notes.Add($"{building} being built");
        if (damaged > 0) notes.Add($"<color=#fe8>{damaged} damaged</color>");
        if (ruined > 0) notes.Add($"<color=#f77>{ruined} ruined</color>");
        if (notes.Count > 0) sb.Append($"\n<size=85%>{string.Join(", ", notes)}</size>");
        selectionText.text = sb.ToString();

        // Upgrade all: the next step of each, cheapest first, as far as resources go.
        var candidates = UpgradeCandidates(out int busy, out int done);
        upgradeButton.gameObject.SetActive(candidates.Count + busy > 0);
        if (candidates.Count + busy > 0)
        {
            var ready = candidates.FindAll(c => CanStep(c.state, c.step, out _));
            var total = Settlement.SumCost(ready.Select(c => c.cost));
            int affordable = 0;
            var running = new List<List<ResourceAmount>>();
            foreach (var c in ready)
            {
                running.Add(c.cost);
                if (!settlement.CanAfford(Settlement.SumCost(running))) break;
                affordable++;
            }
            upgradeButton.interactable = ready.Count > 0;
            string title = ready.Count > 0 ? $"<b>Upgrade all</b>  <size=75%>({ready.Count} of {picked.Count} ready)</size>" : "<b>Upgrade all</b>";
            string sub;
            if (ready.Count == 0)
            {
                string why = "";
                if (candidates.Count > 0) CanStep(candidates[0].state, candidates[0].step, out why);
                sub = busy > 0 && candidates.Count == 0 ? $"<color=#fd6>{busy} upgrading</color>"
                    : $"<color=#f88>{FirstLine(why)}</color>";
            }
            else
            {
                sub = CostText(total) + (affordable < ready.Count ? $"  <color=#fd6>(enough for {affordable})</color>" : "");
                if (busy > 0) sub += $"  <color=#aaa>{busy} busy</color>";
            }
            upgradeLabel.text = $"{title}\n<size=80%>{sub}</size>";
        }

        // Stance: shared if they all agree.
        TroopStance? common = null;
        bool mixed = false, anyTrains = false;
        foreach (var view in picked)
        {
            if (view.Definition.trainsUnit == null) continue;
            anyTrains = true;
            if (common == null) common = view.State.stance;
            else if (common != view.State.stance) mixed = true;
        }
        stanceButton.gameObject.SetActive(anyTrains);
        if (anyTrains)
        {
            stanceLabel.text = (mixed ? "<b>Stance:</b> Mixed" : StanceText(common.Value)) + "\n<size=75%><color=#aaa>Click to switch them all</color></size>";
        }

        // Repair all damaged ones.
        var repairs = new List<List<ResourceAmount>>();
        foreach (var view in picked)
        {
            if (view.State.IsBuilt && view.State.health < settlement.StatsOf(view.State).maxHealth)
            {
                repairs.Add(settlement.RepairCost(view.State));
            }
        }
        repairButton.gameObject.SetActive(repairs.Count > 0);
        if (repairs.Count > 0)
        {
            var cost = Settlement.SumCost(repairs);
            repairButton.interactable = repairs.Exists(settlement.CanAfford);
            repairLabel.text = $"Repair all {repairs.Count} ({CostText(cost)})";
        }
    }

    private static string FirstLine(string text) => string.IsNullOrEmpty(text) ? "" : text.Split('\n')[0];

    private void UpgradeGroup()
    {
        var candidates = UpgradeCandidates(out _, out _);
        int started = 0, tried = 0;
        string firstProblem = null;
        foreach (var c in candidates)
        {
            if (!CanStep(c.state, c.step, out string why))
            {
                firstProblem ??= why;
                continue;
            }
            tried++;
            if (StartStep(c.state, c.step, out why)) started++;
            else firstProblem ??= why;
        }
        if (started > 0)
        {
            string rest = started < candidates.Count && firstProblem != null ? $" The rest: {FirstLine(firstProblem)}" : "";
            AddMessage($"Started {started} of {candidates.Count} upgrades.{rest}");
        }
        else if (firstProblem != null)
        {
            AddMessage(FirstLine(firstProblem));
        }
        RefreshAll();
    }

    private void RepairGroup()
    {
        int repaired = 0, damaged = 0;
        string problem = null;
        var picked = new List<BuildingView>(controller.Selection);
        // Ruins first: they produce nothing until fixed.
        picked.Sort((a, b) => a.State.health.CompareTo(b.State.health));
        foreach (var view in picked)
        {
            if (!view.State.IsBuilt || view.State.health >= settlement.StatsOf(view.State).maxHealth) continue;
            damaged++;
            if (settlement.TryRepair(view.State, out string why)) repaired++;
            else problem ??= why;
        }
        AddMessage(repaired == damaged ? $"Repaired {repaired} buildings." : $"Repaired {repaired} of {damaged}. {FirstLine(problem)}");
        RefreshAll();
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
        // A group follows the first troop building: if it defends, they all attack, and back.
        TroopStance? first = null;
        foreach (var v in controller.Selection)
        {
            if (v.Definition.trainsUnit != null) { first = v.State.stance; break; }
        }
        if (first == null)
        {
            return;
        }
        var next = first == TroopStance.Defend ? TroopStance.Attack : TroopStance.Defend;
        foreach (var v in controller.Selection)
        {
            if (v.Definition.trainsUnit != null) v.State.stance = next;
        }
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

    private string PeopleTooltip()
    {
        int people = settlement.Population, room = settlement.Housing, jobs = settlement.WorkersNeeded;
        int working = Mathf.Min(people, jobs);
        var sb = new StringBuilder("<b>People</b>\n");
        sb.Append($"{people} living here, room for {room}\n");
        sb.Append($"{working} working, {people - working} idle");
        if (jobs > people)
        {
            sb.Append($"\n<color=#fc6>{jobs - people} jobs unfilled: buildings run at {settlement.WorkerEfficiency:P0}</color>");
        }
        int understaffed = 0, paused = 0;
        foreach (var view in controller.Buildings)
        {
            if (!settlement.IsWorking(view.State, view.Definition) || view.Definition.workers <= 0) continue;
            if (view.State.Paused) paused++;
            else if (settlement.StaffingOf(view.State) < 0.999f) understaffed++;
        }
        if (understaffed > 0) sb.Append($"\n<color=#fc6>{understaffed} buildings short of workers</color> <size=80%>(high priority ones fill first)</size>");
        if (paused > 0) sb.Append($"\n<color=#aaa>{paused} paused</color>");
        var due = settlement.UpkeepPerSecond(out int troops);
        if (troops > 0)
        {
            var parts = due.Where(d => d.Value > 0).Select(d => $"{d.Value:0.##} {d.Key.displayName}/s");
            sb.Append($"\nArmy: {troops} troops, upkeep {string.Join(", ", parts)}");
            if (settlement.UpkeepShort) sb.Append("\n<color=#f88>Upkeep unpaid: troops aren't training</color>");
        }
        float tax = settlement.TaxPerPersonPerSecond;
        if (tax > 0 && settlement.Database.coin != null)
        {
            sb.Append($"\nTaxes: +{people * tax:0.##} Coin/s");
        }
        sb.Append(people < room
            ? $"\n<size=80%><color=#aaa>Someone new arrives every {settlement.Database.secondsPerNewPerson:0}s while there's food.</color></size>"
            : "\n<size=80%><color=#aaa>Houses are full. Build more housing to grow.</color></size>");
        return sb.ToString();
    }

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
            if (def.requiredDeposit.richVeins)
                sb.Append("<color=#fd6>Or on a cleared rich vein in the wild</color>: several times the output\n");
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
        if (tech.IsLevelled)
        {
            sb.Append($"<color=#9df>Level {settlement.ResearchLevel(tech)} of {settlement.MaxLevel(tech)}</color> <size=85%><color=#aaa>(each level adds the bonus again, and costs more)</color></size>\n");
        }
        sb.Append($"Cost: {CostText(settlement.ResearchCost(tech))}   Time: {settlement.ResearchTime(tech):0}s\n");
        AppendMissing(sb, settlement.CanResearch(tech, out string missing), missing);
        return sb.ToString().TrimEnd();
    }

    private string AgeTooltip()
    {
        var age = settlement.CurrentAge;
        var info = settlement.AgeProgress(settlement.State.ageIndex);
        var sb = new StringBuilder($"<b>{age?.displayName}</b>  {info.total:P0} through\n");
        sb.Append("<size=85%><color=#ccc>You grow into an age through its research, its buildings, your stores and your people.</color></size>\n");
        sb.Append($"Research {info.researchDone}/{info.researchTotal}");
        if (info.missingResearch.Count > 0) sb.Append($"  <color=#aaa>({Few(info.missingResearch)})</color>");
        sb.Append($"\nBuildings {info.buildingsDone}/{info.buildingsTotal}");
        if (info.missingBuildings.Count > 0) sb.Append($"  <color=#aaa>({Few(info.missingBuildings)})</color>");
        sb.Append($"\nStores {info.resources:P0}");
        if (info.resourceGoals.Count > 0) sb.Append($"  <color=#aaa>({string.Join(", ", info.resourceGoals)})</color>");
        sb.Append($"\nPeople {settlement.Population}/{info.populationGoal}\n");
        var next = settlement.NextAge;
        if (next == null)
        {
            sb.Append("<color=#8f8>This is the latest age.</color>");
            return sb.ToString();
        }
        sb.Append($"\n<b>The {next.displayName}</b>");
        if (!string.IsNullOrEmpty(next.description)) sb.Append($"\n<size=85%><color=#ccc>{next.description}</color></size>");
        if (settlement.CanAdvanceAge(out string missing)) sb.Append("\n<color=#8f8>On its way!</color>");
        else
        {
            sb.Append("\n<color=#f99>To enter it:</color>");
            foreach (var line in missing.Split('\n')) sb.Append($"\n<color=#f99>  - {line.Trim()}</color>");
        }
        return sb.ToString();
    }

    private static string Few(System.Collections.Generic.List<string> names) =>
        string.Join(", ", names.GetRange(0, Mathf.Min(3, names.Count))) + (names.Count > 3 ? "..." : "");

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
        foreach (var input in def.inputs)
        {
            if (input.resource != null)
            {
                sb.Append($"Uses {input.perSecond * s.output:0.##} {input.resource.displayName}/s\n");
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
            var upkeep = def.trainsUnit.upkeep.FindAll(u => u.resource != null && u.perSecond > 0)
                .Select(u => $"{u.perSecond:0.###} {u.resource.displayName}/s");
            if (upkeep.Any())
            {
                sb.Append($"Upkeep per troop: {string.Join(", ", upkeep)}\n");
            }
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
        if (controller.Selection.Count > 1)
        {
            UpgradeGroup();
            return;
        }
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

    // ---------- Priority and automation ----------

    private Button SmallToggle(string name, UnityEngine.Events.UnityAction onClick, out TextMeshProUGUI label, System.Func<string> help)
    {
        var button = UIBuilder.Button(name, automationRow, onClick, out label, 40, 15);
        button.GetComponent<LayoutElement>().flexibleWidth = 1;
        label.alignment = TextAlignmentOptions.Center;
        UIBuilder.OnHover(button.gameObject, () => tooltipSource = help, HideTooltip);
        return button;
    }

    private static string OnOff(int on, int total) =>
        on == total ? "<color=#8f8>On</color>" : on == 0 ? "<color=#aaa>Off</color>" : "<color=#fe8>Mixed</color>";

    private static string PriorityName(int priority) =>
        priority < 0 ? "<color=#aaa>Paused</color>" : priority > 0 ? "<color=#8cf>High</color>" : "Normal";

    private void RefreshAutomationRow()
    {
        var picked = controller.Selection;
        int built = 0, workers = 0, repairOn = 0, queueable = 0, queueOn = 0;
        int? priority = null;
        bool mixedPriority = false;
        foreach (var view in picked)
        {
            var state = view.State;
            if (!state.IsBuilt) continue;
            built++;
            if (state.autoRepair) repairOn++;
            bool hasMore = settlement.NextUpgrade(state) != null || settlement.RankUpTarget(state) != null;
            if (hasMore)
            {
                queueable++;
                if (state.queueUpgrades) queueOn++;
            }
            if (view.Definition.workers > 0)
            {
                workers++;
                if (priority == null) priority = state.priority;
                else if (priority != state.priority) mixedPriority = true;
            }
        }
        automationRow.gameObject.SetActive(built > 0);
        priorityButton.gameObject.SetActive(workers > 0);
        priorityLabel.text = $"Priority: {(mixedPriority ? "Mixed" : PriorityName(priority ?? 0))}";
        autoRepairLabel.text = $"Auto-repair: {OnOff(repairOn, built)}";
        queueButton.gameObject.SetActive(queueable > 0);
        queueLabel.text = $"Auto-upgrade: {OnOff(queueOn, queueable)}";
    }

    private void CyclePriority()
    {
        var first = controller.Selection.FirstOrDefault(v => v.Definition.workers > 0);
        if (first == null) return;
        // Normal -> High -> Paused -> Normal
        int next = first.State.priority == 0 ? 1 : first.State.priority > 0 ? -1 : 0;
        foreach (var view in controller.Selection)
        {
            if (view.Definition.workers > 0) view.State.priority = next;
        }
        RefreshAll();
    }

    private void ToggleAutoRepair()
    {
        bool allOn = controller.Selection.Where(v => v.State.IsBuilt).All(v => v.State.autoRepair);
        foreach (var view in controller.Selection) view.State.autoRepair = !allOn;
        RefreshAll();
    }

    private void ToggleQueue()
    {
        var queueable = controller.Selection.Where(v => v.State.IsBuilt &&
            (settlement.NextUpgrade(v.State) != null || settlement.RankUpTarget(v.State) != null)).ToList();
        bool allOn = queueable.All(v => v.State.queueUpgrades);
        foreach (var view in queueable) view.State.queueUpgrades = !allOn;
        if (!allOn) AddMessage($"Auto-upgrade on for {queueable.Count}: each starts its next upgrade as soon as you can afford it.");
        RefreshAll();
    }

    private void ToggleAutoRepairNew()
    {
        settlement.State.autoRepairNew = !settlement.State.autoRepairNew;
        RefreshAll();
    }

    private void RefreshAutoRepairNew()
    {
        if (autoRepairNewLabel != null)
        {
            autoRepairNewLabel.text = $"<size=90%>New buildings auto-repair: {(settlement.State.autoRepairNew ? "<color=#8f8>On</color>" : "<color=#aaa>Off</color>")}</size>";
        }
    }

    private static string PriorityHelp() =>
        "<b>Worker priority</b>\n" +
        "When people run short, <color=#8cf>High</color> buildings get workers first, then Normal ones in the order you built them.\n" +
        "<color=#aaa>Paused</color> buildings use no workers and do nothing.\n" +
        "<size=80%><color=#aaa>Click to cycle Normal, High, Paused.</color></size>";

    private static string AutomationHelp() =>
        "<b>Automation</b>\n" +
        "<b>Auto-repair</b>: fixes damage (and ruins) by itself once raiders have gone, if you can afford it.\n" +
        "<b>Auto-upgrade</b>: starts the next upgrade, then the rank up, as soon as you can afford them.";
}
