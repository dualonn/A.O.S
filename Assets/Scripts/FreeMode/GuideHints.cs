using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// The guided start: small corner cards with one hint at a time for a new player's first hour
/// (founding, food and housing, wood and stone, research, raids, heroes, the overview, saving).
/// Each hint shows once, when the game state calls for it, and never blocks anything.
/// Also a "Help" section at the bottom of the research panel: a controls reference (F1) and the
/// hints' on/off switch. Old saves start with hints off; a brand-new game turns them on.
/// </summary>
public class GuideHints : MonoBehaviour
{
    private Settlement settlement;
    private FreeModeController controller;
    private Action<Func<string>> showTip;
    private UnityAction hideTip;
    private Action fit;

    private RectTransform card, help;
    private TextMeshProUGUI cardText, toggleLabel;
    private CollapsibleSection section;
    private string showing;           // the hint on the card, or null
    private float checkAt, quietUntil, foundedFor;
    private bool toggleShown, toggleKnown, founded;

    // ---------- Brand-new games turn hints on ----------

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Hook()
    {
        // (after any reset of the hooks at SubsystemRegistration; domain reload is off, so unhook
        // first and the handler is only ever added once)
        NewGameHooks.Started -= OnNewGame;
        NewGameHooks.Started += OnNewGame;
    }

    private static void OnNewGame(Settlement s)
    {
        if (s == null || s.State == null) return;
        s.State.guide = new GuideState { on = true, decided = true };
    }

    /// <summary>Saves that never settled the switch: on only if this is plainly a fresh start.</summary>
    private void Decide()
    {
        var g = settlement.Guide;
        if (g.decided) return;
        g.decided = true;
        var s = settlement.State;
        g.on = s.buildings.Count == 0 && s.playTime < 10f && s.researched.Count == 0 && s.worlds.Count <= 1;
    }

    // ---------- Building ----------

    public static GuideHints Create(Transform root, Settlement settlement, FreeModeController controller,
        Transform researchContent, Action<Func<string>> showTip, UnityAction hideTip, Action fitPanels)
    {
        var go = new GameObject("GuideHints", typeof(RectTransform));
        go.transform.SetParent(root, false);
        UIBuilder.Stretch((RectTransform)go.transform);
        var g = go.AddComponent<GuideHints>();
        g.settlement = settlement;
        g.controller = controller;
        g.showTip = showTip;
        g.hideTip = hideTip;
        g.fit = fitPanels;
        g.BuildCard((RectTransform)go.transform);
        g.BuildHelp((RectTransform)go.transform);
        g.BuildSection(researchContent);
        return g;
    }

    private void BuildCard(RectTransform root)
    {
        // Bottom left, just right of the build menu and clear of the messages and selection panel.
        card = UIBuilder.VerticalPanel("HintCard", root, 10, 6);
        UIBuilder.Anchor(card, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(330, 150), new Vector2(340, 0));
        card.GetComponent<Image>().color = new Color(0.07f, 0.09f, 0.08f, 0.92f);
        cardText = UIBuilder.Text("Text", card, "", 16);
        cardText.textWrappingMode = TextWrappingModes.Normal;
        var row = UIBuilder.Row("Buttons", card, 6);
        row.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
        var ok = UIBuilder.Button("GotIt", row, Dismiss, out var okLabel, 28, 15);
        okLabel.text = "Got it"; okLabel.alignment = TextAlignmentOptions.Center;
        ok.GetComponent<LayoutElement>().flexibleWidth = 1;
        var off = UIBuilder.Button("HintsOff", row, () => SetOn(false), out var offLabel, 28, 15);
        offLabel.text = "<color=#bbb>Turn hints off</color>"; offLabel.alignment = TextAlignmentOptions.Center;
        off.GetComponent<LayoutElement>().flexibleWidth = 1;
        card.gameObject.SetActive(false);
    }

    private void BuildHelp(RectTransform root)
    {
        help = UIBuilder.VerticalPanel("ControlsWindow", root, 14, 6);
        UIBuilder.Anchor(help, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(620, 0));
        var head = UIBuilder.Row("Head", help, 6);
        UIBuilder.Text("Title", head, "<b>Controls</b>", 22).gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        var close = UIBuilder.Button("Close", head, () => ShowHelp(false), out var closeLabel, 30, 16);
        close.GetComponent<LayoutElement>().preferredWidth = 34;
        closeLabel.text = "X"; closeLabel.alignment = TextAlignmentOptions.Center;
        var text = UIBuilder.Text("Text", help, ControlsText, 16);
        text.textWrappingMode = TextWrappingModes.Normal;
        help.gameObject.SetActive(false);
    }

    private const string ControlsText =
        "<color=#fd6><b>Camera</b></color>\n" +
        "Move: <b>WASD</b> / <b>arrow keys</b>, or drag with the <b>middle mouse button</b>\n" +
        "Zoom: <b>mouse wheel</b> or <b>+ / -</b>. Zoom far out for the overview, further still for the map of zones.\n" +
        "<color=#fd6><b>Selecting</b></color>\n" +
        "<b>Left click</b> a building or troop. <b>Drag</b> a box round troops.\n" +
        "<b>Shift</b> adds to the selection. <b>Double click</b> selects all of that kind on screen (<b>Ctrl</b>: the whole map).\n" +
        "<b>Esc</b> clears the selection or closes a window.\n" +
        "<color=#fd6><b>Orders</b></color>\n" +
        "<b>Right click</b> with troops selected: move or attack there. Right click a dungeon to send them down.\n" +
        "<color=#fd6><b>Building</b></color>\n" +
        "Click an entry in the Build menu, then click the map to place it. <b>Shift</b> keeps placing.\n" +
        "<b>Right click</b> or <b>Esc</b> cancels.\n" +
        "<color=#fd6><b>Game speed</b></color>\n" +
        "Speed button (top right) cycles x1 to x10. <b>]</b> faster, <b>[</b> slower, <b>P</b> pause.\n" +
        "<color=#fd6><b>Saving</b></color>\n" +
        "The game saves by itself every 30 seconds and when you quit.\n" +
        "<color=#aaa><size=90%>F1 opens this window. Hover almost anything for a tooltip.</size></color>";

    private void BuildSection(Transform parent)
    {
        if (parent == null) return;
        section = new CollapsibleSection("Help", parent, "Help", new Color(0.7f, 0.85f, 0.75f));
        section.SetOpen(false);
        section.Toggled += _ => fit?.Invoke();
        var row = UIBuilder.Row("Row", section.Body, 5);
        row.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
        var controls = UIBuilder.Button("Controls", row, () => ShowHelp(!HelpOpen), out var cLabel, 32, 15);
        cLabel.text = "Controls"; cLabel.alignment = TextAlignmentOptions.Center;
        controls.GetComponent<LayoutElement>().flexibleWidth = 1;
        var toggle = UIBuilder.Button("Hints", row, () => SetOn(!settlement.Guide.on), out toggleLabel, 32, 15);
        toggleLabel.alignment = TextAlignmentOptions.Center;
        toggle.GetComponent<LayoutElement>().flexibleWidth = 1;
        var dis = UIBuilder.Button("Disasters", row, () => { settlement.State.disastersEnabled = !settlement.State.disastersEnabled; RefreshDisasters(true); }, out disLabel, 32, 15);
        disLabel.alignment = TextAlignmentOptions.Center;
        dis.GetComponent<LayoutElement>().flexibleWidth = 1;
        RefreshDisasters(true);
        if (showTip != null)
        {
            UIBuilder.OnHover(dis.gameObject, () => showTip(() => "<b>Natural disasters</b> Floods, wildfire, volcanoes, earthquakes, plague and meteor showers. " +
                "Turning them off stops new ones; one already under way plays out."), hideTip);
            UIBuilder.OnHover(controls.gameObject, () => showTip(() => "<b>Controls</b>\nKeys and mouse at a glance. <color=#ccc>(F1)</color>"), hideTip);
            UIBuilder.OnHover(toggle.gameObject, () => showTip(() => "<b>Beginner hints</b>\nSmall cards with a tip when it's useful: founding, food, research, raids, heroes...\n" +
                "Each shows once. Turning them back on shows the ones you haven't seen."), hideTip);
        }
    }

    // ---------- Updating ----------

    private bool HelpOpen => help != null && help.gameObject.activeSelf;

    private void ShowHelp(bool open)
    {
        help.gameObject.SetActive(open);
        GameAudio.UI(open ? "book_open" : "book_close");
        if (!open) { hideTip?.Invoke(); return; }
        transform.SetAsLastSibling();
        var tip = transform.parent != null ? transform.parent.Find("Tooltip") : null;
        if (tip != null) tip.SetAsLastSibling();
    }

    private void SetOn(bool on)
    {
        var g = settlement.Guide;
        g.on = on;
        g.decided = true;
        if (!on) Hide();
        quietUntil = Time.unscaledTime + 4f;
        RefreshToggle();
    }

    private TMPro.TextMeshProUGUI disLabel;
    private int disShown = -1;

    private void RefreshDisasters(bool force = false)
    {
        if (disLabel == null || settlement == null || settlement.State == null) return;
        int on = settlement.State.disastersEnabled ? 1 : 0;
        if (!force && on == disShown) return;
        disShown = on;
        disLabel.text = on == 1 ? "Disasters: <color=#8f8>on</color>" : "Disasters: <color=#aaa>off</color>";
    }

    private void RefreshToggle()
    {
        RefreshDisasters();
        if (toggleLabel == null) return;
        bool on = settlement.Guide.on;
        if (toggleKnown && toggleShown == on) return;
        toggleKnown = true;
        toggleShown = on;
        toggleLabel.text = on ? "Hints: <color=#8f8>on</color>" : "Hints: <color=#aaa>off</color>";
    }

    private void Update()
    {
        if (settlement == null || settlement.State == null) return;
        if (Input.GetKeyDown(KeyCode.F1) && !UIBuilder.Typing) ShowHelp(!HelpOpen);
        if (HelpOpen && Input.GetKeyDown(KeyCode.Escape)) ShowHelp(false);

        if (founded) foundedFor += Time.deltaTime;   // game time: pausing pauses the hints too
        if (Time.unscaledTime < checkAt) return;
        checkAt = Time.unscaledTime + 1f;
        founded = settlement.IsFounded;

        Decide();
        RefreshToggle();
        var g = settlement.Guide;
        if (!g.on) { if (showing != null) Hide(); return; }
        if (showing != null || Time.unscaledTime < quietUntil) return;
        string id = NextHint(out string text);
        if (id == null) return;
        Show(id, text);
    }

    private void Show(string id, string text)
    {
        showing = id;
        var g = settlement.Guide;
        if (!g.seen.Contains(id)) g.seen.Add(id);   // seen once it's shown, so a reload never repeats it
        cardText.text = text;
        card.gameObject.SetActive(true);
        GameAudio.UI("hint_show");
    }

    private void Dismiss()
    {
        Hide();
        quietUntil = Time.unscaledTime + 15f;   // a breather before the next one
    }

    private void Hide()
    {
        showing = null;
        if (card != null) card.gameObject.SetActive(false);
    }

    // ---------- The hints ----------

    private bool Seen(string id) => settlement.Guide.seen.Contains(id);

    /// <summary>The first hint whose moment has come and that hasn't been shown yet.</summary>
    private string NextHint(out string text)
    {
        text = null;
        var s = settlement.State;

        if (!founded)
        {
            if (!Seen("found") && s.playTime > 3f)
                return Hint("found", out text, "Found your town",
                    "Open the <b>Build</b> menu on the left and pick the <b>Campfire</b>. Place it near forest, berry bushes and stone; water nearby gives bonuses.\n" +
                    "<color=#aaa>Move with WASD, zoom with the mouse wheel. F1 lists every control.</color>");
            return null;
        }

        if (!Seen("food") && foundedFor > 45f && !Produces("Food"))
            return Hint("food", out text, "Food",
                "Your people eat. Build something that makes <b>Food</b> (a <b>Forager Camp</b> by berry bushes, or a <b>Hunting Lodge</b>) so the town can grow.");

        if (!Seen("wood") && foundedFor > 75f && (!Produces("Wood") || !Produces("Stone")))
            return Hint("wood", out text, "Wood and stone",
                "Most buildings cost <b>wood</b> and <b>stone</b>. Put a <b>Lumber Camp</b> by a forest and a <b>Quarry</b> by stone: the more of it in reach, the more they make (the placing preview shows it).");

        if (!Seen("housing") && foundedFor > 30f && settlement.Housing > 0 && settlement.Population >= settlement.Housing)
            return Hint("housing", out text, "Housing",
                "Every home is full. Build more <b>housing</b> and new people move in; more people fill more jobs. Hover the people chip to see who's working.");

        if (!Seen("research") && foundedFor > 100f && s.researched.Count == 0 && s.researchJobs.Count == 0)
            return Hint("research", out text, "Research",
                "The panel on the right holds <b>research</b>: small lasting bonuses and new buildings. Click one you can afford to start it.");

        if (!Seen("slots") && s.researchJobs.Count == 1 && settlement.ResearchSlots > 1)
            return Hint("slots", out text, "Research slots",
                $"You can study <b>{settlement.ResearchSlots}</b> things at once. Start another research beside this one; later ages open more slots.");

        if (!Seen("raid") && s.wavesSurvived == 0 && settlement.Waves != null && settlement.Waves.SecondsUntilWave > 0f && settlement.Waves.SecondsUntilWave < 90f)
            return Hint("raid", out text, "Raiders are coming",
                "Raider camps gather fighters and send <b>war parties</b> across the map; the raid chip on the top bar shows the threat (hover it, click to look). Towers and lookouts spot them sooner. Train troops (a <b>Barracks</b>) and build towers near your homes; walls slow raiders down. " +
                "Troops <b>Defend</b> the town by default; switch a building to <b>Attack</b> to hunt raiders and their camps.");

        if (!Seen("heroes") && s.heroes.Count == 0 && foundedFor > 240f && HasTroops())
            return Hint("heroes", out text, "Heroes",
                "Select one of your troops and <b>promote</b> it to a hero. Heroes are saved, level up, wear five pieces of gear and take on quests. " +
                "Loot comes from camps, bosses, dungeons and ruins.");

        if (!Seen("overview") && foundedFor > 360f)
        {
            var grid = controller != null ? controller.Grid : null;
            if (grid != null && grid.CurrentDetail != SettlementGrid.Detail.Tiles) { settlement.Guide.seen.Add("overview"); return null; }   // found it already
            return Hint("overview", out text, "The wide view",
                "Zoom far out with the mouse wheel for the <b>overview</b> of the whole land, and further still for the map of zones: camps, sites and what you've explored.");
        }

        if (!Seen("saving") && foundedFor > 480f)
            return Hint("saving", out text, "Saving",
                "The game <b>saves by itself</b> every 30 seconds and when you quit, so you can stop any time. " +
                "These hints can be switched off or on under <b>Help</b> at the bottom of the research panel.");
        return null;
    }

    private static string Hint(string id, out string text, string title, string body)
    {
        text = $"<b><color=#bf9>{title}</color></b>\n{body}";
        return id;
    }

    /// <summary>Does any building you have (placed or built) make this resource?</summary>
    private bool Produces(string resourceId)
    {
        var db = settlement.Database;
        foreach (var b in settlement.State.buildings)
        {
            var def = db.GetBuilding(b.definitionId);
            if (def == null) continue;
            foreach (var p in def.production)
                if (p.resource != null && p.resource.id == resourceId && p.perSecond > 0) return true;
        }
        return false;
    }

    private bool HasTroops()
    {
        var db = settlement.Database;
        foreach (var b in settlement.State.buildings)
        {
            if (!b.IsBuilt) continue;
            var def = db.GetBuilding(b.definitionId);
            if (def != null && def.trainsUnit != null) return true;
        }
        return false;
    }
}
