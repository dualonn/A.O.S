using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Games: a folded section in the research panel (under the Chronicle) that opens one window with
/// two tabs: your saved games (load, delete) and a new-game setup (map, difficulty, raids,
/// starting age, home world, seed). Changing game always saves the one you're playing first.
/// </summary>
public class GamesPanel : MonoBehaviour
{
    public static GamesPanel Instance { get; private set; }

    private enum Tab { Saved, NewGame }

    private Settlement settlement;
    private Action<Func<string>> showTip;
    private UnityAction hideTip;
    private Action fit;

    private CollapsibleSection section;
    private RectTransform panel, content;
    private TextMeshProUGUI footer;
    private Button savedTab, newTab;
    private Tab tab;

    // Delete needs a second click within a few seconds.
    private int deleteArmed = -1;
    private float deleteArmedUntil;

    // The setup being chosen (defaults = the classic game).
    private GameOptions setup = new GameOptions();
    private string seedText = "";
    private bool startArmed;
    private float startArmedUntil;

    public bool IsOpen => panel != null && panel.gameObject.activeSelf;

    public static GamesPanel Create(Transform root, Settlement settlement, Transform researchContent,
        Action<Func<string>> showTip, UnityAction hideTip, Action fitPanels)
    {
        var go = new GameObject("Games", typeof(RectTransform));
        go.transform.SetParent(root, false);
        UIBuilder.Stretch((RectTransform)go.transform);
        var p = go.AddComponent<GamesPanel>();
        p.settlement = settlement;
        p.showTip = showTip;
        p.hideTip = hideTip;
        p.fit = fitPanels;
        p.BuildSection(researchContent);
        p.BuildWindow((RectTransform)go.transform);
        Instance = p;
        NewGameHooks.NewGameRequested += p.OnNewGameRequested;
        return p;
    }

    private void OnDestroy()
    {
        NewGameHooks.NewGameRequested -= OnNewGameRequested;
        if (Instance == this) Instance = null;
    }

    private void OnNewGameRequested() => Open(Tab.NewGame);

    // ---------- Section ----------

    private void BuildSection(Transform parent)
    {
        if (parent == null) return;
        section = new CollapsibleSection("Games", parent, "Games", new Color(0.7f, 0.8f, 0.95f));
        section.SetOpen(false);
        section.Toggled += _ => fit?.Invoke();
        var row = UIBuilder.Row("Row", section.Body, 5);
        row.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
        SectionButton(row, "Saved games", Tab.Saved, "Your games side by side. Load one (the game you're playing is saved first), or delete one you're done with.");
        SectionButton(row, "New game", Tab.NewGame, "Start another game in a new slot: pick the map, difficulty, raids, starting age, home world and seed. This game is saved and stays in the list.");
        section.SetNote(settlement != null && settlement.PlayingSlot > 0 ? $"Game {settlement.PlayingSlot}" : "test save");
    }

    private void SectionButton(Transform row, string text, Tab t, string help)
    {
        var b = UIBuilder.Button(text, row, () => Open(t), out var label, 32, 15);
        label.text = text;
        label.alignment = TextAlignmentOptions.Center;
        b.GetComponent<LayoutElement>().flexibleWidth = 1;
        if (showTip != null) UIBuilder.OnHover(b.gameObject, () => showTip(() => $"<b>{text}</b>\n{help}"), hideTip);
    }

    // ---------- Window ----------

    private void BuildWindow(RectTransform root)
    {
        panel = UIBuilder.VerticalPanel("GamesWindow", root, 12, 6);
        UIBuilder.Anchor(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(720, 0));
        var head = UIBuilder.Row("Head", panel, 6);
        var title = UIBuilder.Text("Title", head, "<b>Games</b>", 22);
        title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        var close = UIBuilder.Button("Close", head, () => Show(false), out var closeLabel, 30, 16);
        close.GetComponent<LayoutElement>().preferredWidth = 34;
        closeLabel.text = "X"; closeLabel.alignment = TextAlignmentOptions.Center;

        var tabs = UIBuilder.Row("Tabs", panel, 4);
        tabs.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
        savedTab = TabButton(tabs, "Saved games", Tab.Saved);
        newTab = TabButton(tabs, "New game", Tab.NewGame);

        content = UIBuilder.ScrollList("Content", panel, out var element, 5);
        element.preferredHeight = 440;
        footer = UIBuilder.Text("Footer", panel, "", 14);
        footer.textWrappingMode = TextWrappingModes.Normal;
        panel.gameObject.SetActive(false);
    }

    private Button TabButton(Transform row, string text, Tab t)
    {
        var b = UIBuilder.Button(text, row, () => { tab = t; Rebuild(); }, out var label, 30, 15);
        label.text = text;
        label.alignment = TextAlignmentOptions.Center;
        b.GetComponent<LayoutElement>().flexibleWidth = 1;
        return b;
    }

    private void Open(Tab t)
    {
        tab = t;
        Show(true);
    }

    private void Show(bool open)
    {
        if (panel == null) return;
        panel.gameObject.SetActive(open);
        GameAudio.UI(open ? "book_open" : "book_close");
        deleteArmed = -1;
        startArmed = false;
        if (!open) { hideTip?.Invoke(); return; }
        transform.SetAsLastSibling();
        var tip = transform.parent != null ? transform.parent.Find("Tooltip") : null;
        if (tip != null) tip.SetAsLastSibling();
        Rebuild();
    }

    private void Update()
    {
        if (!IsOpen) return;
        if (Input.GetKeyDown(KeyCode.Escape) && !UIBuilder.Typing) { Show(false); return; }
        // Disarm a pending delete / start after a few seconds.
        if (deleteArmed >= 0 && Time.unscaledTime > deleteArmedUntil) { deleteArmed = -1; Rebuild(); }
        if (startArmed && Time.unscaledTime > startArmedUntil) { startArmed = false; Rebuild(); }
    }

    private void Rebuild()
    {
        foreach (Transform child in content) Destroy(child.gameObject);
        savedTab.image.color = tab == Tab.Saved ? new Color(0.42f, 0.33f, 0.2f) : UIBuilder.ButtonColor;
        newTab.image.color = tab == Tab.NewGame ? new Color(0.42f, 0.33f, 0.2f) : UIBuilder.ButtonColor;
        footer.text = "";
        if (Settlement.UsingTestSave)
        {
            Block("<color=#fd6>The editor is playing on the test save</color> (settlement_test.json). Saved games and new games are off until the test-save switch is turned off.");
            return;
        }
        if (tab == Tab.Saved) BuildSaved(); else BuildNewGame();
    }

    private TextMeshProUGUI Block(string text, float size = 16)
    {
        var t = UIBuilder.Text("Text", content, text, size);
        t.textWrappingMode = TextWrappingModes.Normal;
        t.richText = true;
        return t;
    }

    // ---------- Saved games ----------

    private void BuildSaved()
    {
        int playing = settlement != null ? settlement.PlayingSlot : 0;
        foreach (int slot in Settlement.ListSlots())
        {
            int s = slot;
            bool here = s == playing;
            var (status, options) = Settlement.SlotSummary(s);
            if (here && settlement.State != null)
            {
                string live = settlement.GamesSummary();
                int bar = live.IndexOf('|');
                status = bar < 0 ? live : live.Substring(0, bar);
                options = bar < 0 ? "" : live.Substring(bar + 1);
            }
            if (string.IsNullOrEmpty(status)) status = s == 1 ? "Your first game" : "No summary yet";
            string when = "";
            try { if (Settlement.SlotExists(s)) when = $"last saved {File.GetLastWriteTime(Settlement.SlotPath(s)):d MMM HH:mm}"; }
            catch (Exception) { }

            var box = UIBuilder.VerticalPanel("Game" + s, content, 8, 3);
            box.GetComponent<Image>().color = here ? new Color(0.2f, 0.26f, 0.18f, 0.9f) : new Color(0.14f, 0.15f, 0.18f, 0.9f);
            var row = UIBuilder.Row("Row", box, 6);
            var text = UIBuilder.Text("Info", row,
                $"<b>Game {s}</b>{(s == 1 ? " <color=#aaa>(your first game)</color>" : "")}{(here ? "  <color=#9f9>playing</color>" : "")}\n" +
                $"<size=85%>{status}</size>" +
                (string.IsNullOrEmpty(options) ? "" : $"\n<size=80%><color=#9cf>{options}</color></size>") +
                (when.Length > 0 ? $"\n<size=75%><color=#888>{when}</color></size>" : ""), 17);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            if (!here)
            {
                var load = UIBuilder.Button("Load", row, () => Load(s), out var loadLabel, 34, 15);
                load.GetComponent<LayoutElement>().preferredWidth = 90;
                loadLabel.text = "Load"; loadLabel.alignment = TextAlignmentOptions.Center;
                if (showTip != null) UIBuilder.OnHover(load.gameObject, () => showTip(() => $"<b>Load game {s}</b>\nThe game you're playing is saved first, then game {s} opens where you left it."), hideTip);
                if (s >= 2)
                {
                    bool armed = deleteArmed == s;
                    var del = UIBuilder.Button("Delete", row, () => Delete(s), out var delLabel, 34, 15);
                    del.GetComponent<LayoutElement>().preferredWidth = 100;
                    del.image.color = armed ? new Color(0.55f, 0.18f, 0.15f) : UIBuilder.ButtonColor;
                    delLabel.text = armed ? "Sure?" : "Delete"; delLabel.alignment = TextAlignmentOptions.Center;
                    if (showTip != null) UIBuilder.OnHover(del.gameObject, () => showTip(() => $"<b>Delete game {s}</b>\nClick twice. This removes its save for good."), hideTip);
                }
            }
        }
        footer.text = "<color=#aaa>Your games autosave. Loading another game saves this one first; your first game (Game 1) is never deleted here.</color>";
    }

    private void Load(int slot)
    {
        if (!Settlement.SwitchGame(slot, null, out string why)) { footer.text = $"<color=#f88>{why}</color>"; return; }
        footer.text = $"Saving and loading game {slot}...";
    }

    private void Delete(int slot)
    {
        if (deleteArmed != slot)
        {
            deleteArmed = slot;
            deleteArmedUntil = Time.unscaledTime + 4f;
            Rebuild();
            return;
        }
        deleteArmed = -1;
        bool ok = Settlement.DeleteSlot(slot, out string why);
        Rebuild();
        footer.text = ok ? $"Game {slot} deleted." : $"<color=#f88>{why}</color>";
    }

    // ---------- New game ----------

    private void BuildNewGame()
    {
        var db = settlement != null ? settlement.Database : null;
        Block("<size=85%><color=#ccc>A fresh start in a new slot. The game you're playing is saved and stays in Saved games.</color></size>");

        Choice("Starting land", GameOptions.Name(setup.mapSize),
            () => setup.mapSize = Step(setup.mapSize, -1), () => setup.mapSize = Step(setup.mapSize, 1),
            "The map has no edge and grows as you explore; this is how much land is explored when you begin. Normal is the classic start.");
        Choice("Difficulty", GameOptions.Name(setup.difficulty),
            () => setup.difficulty = Step(setup.difficulty, -1), () => setup.difficulty = Step(setup.difficulty, 1),
            "How strong raids are, and how much you start with.\nEasy: raids x0.7, stock x1.5\nNormal: the classic game\nHard: raids x1.35, stock x0.8\nBrutal: raids x1.75, stock x0.6");
        Choice("Raids", GameOptions.Name(setup.raids),
            () => setup.raids = Step(setup.raids, -1), () => setup.raids = Step(setup.raids, 1),
            "How often raiders come.\nRare: 60% longer between raids\nFrequent: about a third sooner\nOff: a peaceful game (camps still stand on the map for you to clear)");
        Choice("Disasters", setup.noDisasters ? "Off" : "On",
            () => setup.noDisasters = !setup.noDisasters, () => setup.noDisasters = !setup.noDisasters,
            "Floods, wildfire, volcanoes, earthquakes, plague and meteor showers, each with buildings that guard against them. Never in the first 20 minutes. You can switch them off later under Help.");
        int ages = db != null ? Mathf.Max(1, db.ages.Count) : 1;
        string ageName = db != null && db.GetAge(setup.startAge) != null ? db.GetAge(setup.startAge).displayName : "Stone Age";
        Choice("Starting age", ageName,
            () => setup.startAge = (setup.startAge - 1 + ages) % ages, () => setup.startAge = (setup.startAge + 1) % ages,
            "Begin in a later age: the research of every earlier age comes done, with a stock fitting the age. You still found your settlement from a campfire.");
        var kinds = new List<string>(WorldProfile.All.Keys);
        int ki = Mathf.Max(0, kinds.IndexOf(string.IsNullOrEmpty(setup.homeKind) ? WorldProfile.HomeId : setup.homeKind));
        Choice("Home world", GameOptions.HomeName(kinds.Count > 0 ? kinds[ki] : ""),
            () => setup.homeKind = kinds[(ki - 1 + kinds.Count) % kinds.Count], () => setup.homeKind = kinds[(ki + 1) % kinds.Count],
            () => $"The land your people start on.\n<color=#ccc>{WorldProfile.Get(kinds.Count > 0 ? kinds[ki] : WorldProfile.HomeId).description}</color>");

        // Seed: blank = random.
        var seedRow = UIBuilder.Row("Seed", content, 6);
        var seedLabel = UIBuilder.Text("Label", seedRow, "Seed", 17);
        seedLabel.gameObject.AddComponent<LayoutElement>().preferredWidth = 170;
        var field = UIBuilder.SearchField("SeedField", seedRow, "Random (or type a number or word)", t => seedText = t.Trim());
        field.text = seedText;
        field.GetComponent<LayoutElement>().flexibleWidth = 1;
        var rnd = UIBuilder.Button("Random", seedRow, () => { seedText = ""; Rebuild(); }, out var rndLabel, 34, 15);
        rnd.GetComponent<LayoutElement>().preferredWidth = 90;
        rndLabel.text = "Random"; rndLabel.alignment = TextAlignmentOptions.Center;
        if (showTip != null) UIBuilder.OnHover(field.gameObject, () => showTip(() => "<b>Seed</b>\nThe same seed (and home world) gives the same map. Leave blank for a random one."), hideTip);

        // Start (two clicks) and reset.
        var row = UIBuilder.Row("Buttons", content, 6);
        row.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
        var reset = UIBuilder.Button("Reset", row, () => { setup = new GameOptions(); seedText = ""; Rebuild(); }, out var resetLabel, 40, 16);
        resetLabel.text = "Classic settings"; resetLabel.alignment = TextAlignmentOptions.Center;
        reset.GetComponent<LayoutElement>().flexibleWidth = 1;
        var start = UIBuilder.Button("Start", row, StartNewGame, out var startLabel, 40, 17);
        startLabel.text = startArmed ? "<b>Click again to start</b>" : "<b>Start new game</b>";
        startLabel.alignment = TextAlignmentOptions.Center;
        start.image.color = startArmed ? new Color(0.25f, 0.45f, 0.22f) : new Color(0.22f, 0.32f, 0.2f);
        start.GetComponent<LayoutElement>().flexibleWidth = 2;
        int slot = Settlement.FreeSlot();
        footer.text = slot > 0
            ? $"<color=#aaa>It will be Game {slot}. Game {(settlement != null ? settlement.PlayingSlot : 1)} is saved first and stays in Saved games.</color>"
            : "<color=#f88>No free slot.</color>";
    }

    private static T Step<T>(T value, int dir) where T : Enum
    {
        var values = (T[])Enum.GetValues(typeof(T));
        // Show in a sensible order: the "Normal" (0) value sits in the middle of the list.
        var order = Ordered(values);
        int i = Array.IndexOf(order, value);
        return order[(i + dir + order.Length) % order.Length];
    }

    private static T[] Ordered<T>(T[] values) where T : Enum
    {
        if (typeof(T) == typeof(GameMapSize)) return (T[])(object)new[] { GameMapSize.Small, GameMapSize.Normal, GameMapSize.Large, GameMapSize.Huge };
        if (typeof(T) == typeof(GameDifficulty)) return (T[])(object)new[] { GameDifficulty.Easy, GameDifficulty.Normal, GameDifficulty.Hard, GameDifficulty.Brutal };
        if (typeof(T) == typeof(GameRaids)) return (T[])(object)new[] { GameRaids.Off, GameRaids.Rare, GameRaids.Normal, GameRaids.Frequent };
        return values;
    }

    private void Choice(string name, string value, Action prev, Action next, string help) => Choice(name, value, prev, next, () => help);

    private void Choice(string name, string value, Action prev, Action next, Func<string> help)
    {
        var row = UIBuilder.Row(name, content, 6);
        var label = UIBuilder.Text("Label", row, name, 17);
        label.gameObject.AddComponent<LayoutElement>().preferredWidth = 170;
        var left = UIBuilder.Button("Prev", row, () => { prev(); startArmed = false; Rebuild(); }, out var leftLabel, 34, 16);
        left.GetComponent<LayoutElement>().preferredWidth = 40;
        leftLabel.text = "<"; leftLabel.alignment = TextAlignmentOptions.Center;
        var shown = UIBuilder.Text("Value", row, $"<b>{value}</b>", 17, TextAlignmentOptions.Center);
        shown.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        var right = UIBuilder.Button("Next", row, () => { next(); startArmed = false; Rebuild(); }, out var rightLabel, 34, 16);
        right.GetComponent<LayoutElement>().preferredWidth = 40;
        rightLabel.text = ">"; rightLabel.alignment = TextAlignmentOptions.Center;
        // Hover anywhere on the row for what it means.
        var hit = row.gameObject.AddComponent<Image>();
        hit.color = Color.clear;
        if (showTip != null) UIBuilder.OnHover(row.gameObject, () => showTip(() => $"<b>{name}</b>\n{help()}"), hideTip);
    }

    private void StartNewGame()
    {
        if (!startArmed)
        {
            startArmed = true;
            startArmedUntil = Time.unscaledTime + 4f;
            Rebuild();
            return;
        }
        startArmed = false;
        int slot = Settlement.FreeSlot();
        var opts = setup.Copy();
        opts.seed = ParseSeed(seedText);
        if (opts.homeKind == WorldProfile.HomeId) opts.homeKind = "";
        string why = "No free slot.";
        if (slot < 0 || !Settlement.SwitchGame(slot, opts, out why))
        {
            Rebuild();
            footer.text = $"<color=#f88>{why}</color>";
            return;
        }
        footer.text = $"Saving this game and starting Game {slot}...";
    }

    /// <summary>A number is used as it is; a word becomes a number (the same word, the same map). Blank = random (0).</summary>
    private static int ParseSeed(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        if (int.TryParse(text, out int n) && n != 0) return n == int.MinValue ? int.MaxValue : Mathf.Abs(n);
        unchecked
        {
            uint h = 2166136261;
            foreach (char ch in text.ToLowerInvariant()) { h ^= ch; h *= 16777619; }
            int v = (int)(h & 0x7FFFFFFF);
            return v == 0 ? 1 : v;
        }
    }
}
