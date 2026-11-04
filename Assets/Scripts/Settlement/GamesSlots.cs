using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Save slots and new games. Slot 1 is settlement.json (the game you've always played); new games
/// go to settlement_slot2.json, settlement_slot3.json, ... The slot being played is remembered in
/// PlayerPrefs (never in a save). The editor's test-save switch still wins over all of this.
///
/// Changing game: the free mode scene is unloaded first (so a raid in progress is settled into the
/// old game as usual), then the old game is saved to its own file and removed, the new slot is
/// remembered, and the free mode scene is loaded again, which starts the settlement from that slot.
/// </summary>
public partial class Settlement
{
    /// <summary>PlayerPrefs key: the save slot being played (1 = settlement.json).</summary>
    public const string SlotKey = "AOS.SaveSlot";
    private const string SummaryKeyPrefix = "AOS.SlotSummary.";

    /// <summary>The slot that will be (or is being) played. Never below 1.</summary>
    public static int ActiveSlot => Mathf.Max(1, PlayerPrefs.GetInt(SlotKey, 1));

    public static string SlotFileName(int slot) => slot <= 1 ? "settlement.json" : $"settlement_slot{slot}.json";
    public static string SlotPath(int slot) => Path.Combine(Application.persistentDataPath, SlotFileName(slot));
    public static bool SlotExists(int slot) => File.Exists(SlotPath(slot));

    // The options for the game about to be created, and the slot they're for.
    private static GameOptions pendingOptions;
    private static int pendingSlot;
    private static bool switchingGame;
    private static Dictionary<string, WorldProfile> gamesHomeLike;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void GamesResetStatics()
    {
        pendingOptions = null;
        pendingSlot = 0;
        switchingGame = false;
        gamesHomeLike = null;
    }

    // Which slot this game was loaded from (0 = the editor's test save), and whether it's brand new.
    private int gamesSlot;
    private bool gamesNewGame;

    /// <summary>The slot being played now (0 while on the editor's test save).</summary>
    public int PlayingSlot => gamesSlot;

    private static readonly GameOptions DefaultOptions = new GameOptions();

    /// <summary>The options this game was started with (defaults for games from before the setup screen).</summary>
    public GameOptions Options
    {
        get
        {
            if (State == null) return DefaultOptions;
            if (State.options == null) State.options = new GameOptions();
            return State.options;
        }
    }

    public float RaidStrengthFactor => Options.RaidStrength;
    public float RaidIntervalFactor => Options.RaidInterval;
    public bool RaidsOff => Options.raids == GameRaids.Off;

    // ---------- Starting a game ----------

    /// <summary>Called from Initialise (with the file it will save to), before loading.</summary>
    private void GamesNoteSlot() => gamesSlot = UsingTestSave ? 0 : ActiveSlot;

    /// <summary>A brand-new state was just made: apply the chosen options (defaults = the classic start).</summary>
    private void GamesApplyOptions(SettlementState state)
    {
        gamesNewGame = true;
        var opts = pendingOptions != null && pendingSlot == gamesSlot && gamesSlot > 0 ? pendingOptions.Copy() : new GameOptions();
        pendingOptions = null;
        pendingSlot = 0;
        state.options = opts;
        state.disastersEnabled = !opts.noDisasters;
        var home = state.worlds.Count > 0 ? state.worlds[0] : null;
        if (home == null) return;

        // Seed: typed, or the one just rolled (kept so the setup can be repeated).
        if (opts.seed != 0)
        {
            state.mapSeed = opts.seed;
            home.seed = opts.seed;
        }
        else opts.seed = state.mapSeed;

        // Home world kind must be one that exists.
        if (!string.IsNullOrEmpty(opts.homeKind) && !WorldProfile.All.ContainsKey(opts.homeKind)) opts.homeKind = "";

        // Bigger starts: extra rings of explored zones round the classic starting area.
        if (opts.ExtraRings > 0)
        {
            var area = MapGenerator.InitialArea(Database);
            int c = Mathf.Max(4, Database.chunkSize);
            int x0 = area.xMin / c, y0 = area.yMin / c;
            int x1 = Mathf.CeilToInt(area.xMax / (float)c), y1 = Mathf.CeilToInt(area.yMax / (float)c);
            for (int cy = y0 - opts.ExtraRings; cy < y1 + opts.ExtraRings; cy++)
                for (int cx = x0 - opts.ExtraRings; cx < x1 + opts.ExtraRings; cx++)
                    if (cx < x0 || cx >= x1 || cy < y0 || cy >= y1) home.explored.Add(new Vector2Int(cx, cy));
        }

        // Starting age: the ages before it count as lived through (their research done).
        opts.startAge = Mathf.Clamp(opts.startAge, 0, Mathf.Max(0, Database.ages.Count - 1));
        int k = opts.startAge;
        if (k > 0)
        {
            state.ageIndex = k;
            foreach (var tech in Database.technologies)
            {
                if (tech == null || tech.IsLevelled) continue;   // Mastery stays to be earned
                int order = tech.age != null ? tech.age.order : 0;
                if (order < k && !state.researched.Contains(tech.id)) state.researched.Add(tech.id);
            }
            // A stock fitting the age: half of what entering it asked for, on top of the usual start.
            var age = Database.GetAge(k);
            if (age != null)
            {
                foreach (var item in age.advanceRequirements.cost)
                {
                    if (item.resource == null || item.amount <= 0) continue;
                    var stock = state.resources.Find(r => r.id == item.resource.id);
                    if (stock == null) state.resources.Add(stock = new ResourceStock { id = item.resource.id });
                    stock.amount += Mathf.Round(item.amount * 0.5f);
                }
            }
            home.population += Mathf.Min(8, k / 2);
        }

        // Difficulty: more or less to start with.
        float f = opts.StartingStock;
        if (!Mathf.Approximately(f, 1f))
            foreach (var stock in state.resources) stock.amount = Mathf.Round(stock.amount * f);
    }

    /// <summary>End of Initialise: a brand-new game is saved at once (if its file doesn't exist yet) and announced.</summary>
    private void GamesAfterInitialise()
    {
        if (!gamesNewGame) return;
        gamesNewGame = false;
        // Only ever creates a missing file: an existing (e.g. unreadable) save is never overwritten here.
        if (!string.IsNullOrEmpty(loadedFrom) && !File.Exists(loadedFrom)) Save();
        NewGameHooks.RaiseStarted(this);
    }

    // ---------- Home world kinds ----------

    /// <summary>
    /// The profile a world's map is made from. The home world can be given another kind of land at
    /// the start; it stays "home" (its id is the home id) so home-world rules still apply to it.
    /// </summary>
    private WorldProfile GamesProfileOf(WorldState w)
    {
        var p = WorldProfile.Get(w.profile);
        if (State == null || State.worlds.Count == 0 || !ReferenceEquals(w, State.worlds[0]) || w.profile != WorldProfile.HomeId) return p;
        string kind = Options.homeKind;
        if (string.IsNullOrEmpty(kind) || kind == WorldProfile.HomeId || !WorldProfile.All.TryGetValue(kind, out var chosen)) return p;
        if (gamesHomeLike == null) gamesHomeLike = new Dictionary<string, WorldProfile>();
        if (!gamesHomeLike.TryGetValue(kind, out var copy))
        {
            var clone = typeof(object).GetMethod("MemberwiseClone", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            copy = (WorldProfile)clone.Invoke(chosen, null);
            copy.id = WorldProfile.HomeId;
            copy.displayName = WorldProfile.Home.displayName;
            copy.natives = null;   // no natives on your own home world
            gamesHomeLike[kind] = copy;
        }
        return copy;
    }

    /// <summary>Zones of the classic starting area left unexplored on each side (small starts).</summary>
    private int GamesInsetOf(WorldState w) =>
        State != null && State.worlds.Count > 0 && ReferenceEquals(w, State.worlds[0]) && Options.ExtraRings < 0 ? -Options.ExtraRings : 0;

    // ---------- Summaries (for the games list) ----------

    /// <summary>After each save: a one-line summary of this slot, kept in PlayerPrefs so the list never reads saves.</summary>
    private void GamesWriteSummary()
    {
        if (gamesSlot <= 0 || State == null) return;
        try { PlayerPrefs.SetString(SummaryKeyPrefix + gamesSlot, GamesSummary()); }
        catch (Exception e) { Debug.LogWarning($"Settlement: couldn't store the game summary: {e.Message}"); }
    }

    /// <summary>"Medieval Age · 2 worlds · 140 people · 3h 05m played|Hard · rare raids"</summary>
    public string GamesSummary()
    {
        float people = 0f;
        foreach (var w in State.worlds) people += w.population;
        int worlds = State.worlds.Count;
        string age = CurrentAge != null ? CurrentAge.displayName : "Stone Age";
        return $"{age} · {worlds} world{(worlds == 1 ? "" : "s")} · {Mathf.FloorToInt(people)} people · {Played(State.playTime)} played|{Options.Describe(Database)}";
    }

    public static string Played(float seconds)
    {
        int m = Mathf.FloorToInt(seconds / 60f);
        return m >= 60 ? $"{m / 60}h {m % 60:00}m" : $"{m}m";
    }

    /// <summary>The stored summary of a slot (empty if none yet), split into its status and options parts.</summary>
    public static (string status, string options) SlotSummary(int slot)
    {
        string s = PlayerPrefs.GetString(SummaryKeyPrefix + slot, "");
        int bar = s.IndexOf('|');
        return bar < 0 ? (s, "") : (s.Substring(0, bar), s.Substring(bar + 1));
    }

    /// <summary>Every slot with a save file (slot 1 first), plus the slot being played.</summary>
    public static List<int> ListSlots()
    {
        var slots = new List<int>();
        if (SlotExists(1)) slots.Add(1);
        try
        {
            foreach (var path in Directory.GetFiles(Application.persistentDataPath, "settlement_slot*.json"))
            {
                string name = Path.GetFileNameWithoutExtension(path);
                if (int.TryParse(name.Substring("settlement_slot".Length), out int n) && n >= 2 && SlotFileName(n) == Path.GetFileName(path) && !slots.Contains(n))
                    slots.Add(n);
            }
        }
        catch (Exception e) { Debug.LogWarning($"Settlement: couldn't list saved games: {e.Message}"); }
        int active = ActiveSlot;
        if (!UsingTestSave && !slots.Contains(active)) slots.Add(active);
        slots.Sort();
        return slots;
    }

    /// <summary>The first free slot for a new game (never slot 1).</summary>
    public static int FreeSlot()
    {
        for (int n = 2; n < 1000; n++)
            if (!SlotExists(n) && n != ActiveSlot) return n;
        return -1;
    }

    // ---------- Switching and deleting ----------

    public static bool IsSwitchingGame => switchingGame;

    /// <summary>
    /// Save this game and play another: an existing slot (newGame null) or a new game in a free slot.
    /// Returns false (with why) if it can't.
    /// </summary>
    public static bool SwitchGame(int slot, GameOptions newGame, out string why)
    {
        why = "";
        if (switchingGame) { why = "Already changing game."; return false; }
        if (UsingTestSave) { why = "The editor is playing on the test save: turn it off to use saved games."; return false; }
        if (Instance == null || FreeModeController.Instance == null) { why = "No game is running."; return false; }
        if (slot < 1) { why = "No such game."; return false; }
        if (newGame == null && !SlotExists(slot)) { why = "That game's save file is missing."; return false; }
        if (newGame != null && (slot < 2 || SlotExists(slot))) { why = "That slot is already used."; return false; }
        if (newGame == null && slot == Instance.gamesSlot) { why = "You're playing that game."; return false; }
        int sceneIndex = FreeModeController.Instance.gameObject.scene.buildIndex;
        if (sceneIndex < 0) { why = "The free mode scene isn't in the build list."; return false; }
        switchingGame = true;
        var go = new GameObject("ChangingGame");
        DontDestroyOnLoad(go);
        go.AddComponent<GamesSwitcher>().Begin(slot, newGame != null ? newGame.Copy() : null, sceneIndex);
        return true;
    }

    /// <summary>Step 2 of a switch, once the scene is gone: save the game to its own file and remove it.</summary>
    internal static void ShutDownForSwitch()
    {
        var s = Instance;
        if (s == null) return;
        s.Save();                                    // to the file it was loaded from
        foreach (var b in s.GetComponents<Behaviour>()) b.enabled = false;
        s.State = null;                              // so OnDestroy doesn't save again
        Instance = null;
        Destroy(s.gameObject);
    }

    /// <summary>Step 3: remember the slot (and a new game's options) before the scene starts it.</summary>
    internal static void SelectSlot(int slot, GameOptions newGame)
    {
        pendingOptions = newGame;
        pendingSlot = newGame != null ? slot : 0;
        PlayerPrefs.SetInt(SlotKey, slot);
        PlayerPrefs.Save();
    }

    internal static void SwitchFinished() => switchingGame = false;

    /// <summary>Delete a saved game: never slot 1 (settlement.json), and never the one being played.</summary>
    public static bool DeleteSlot(int slot, out string why)
    {
        why = "";
        if (slot < 2) { why = "Your first game can't be deleted here."; return false; }
        if (slot == ActiveSlot || (Instance != null && slot == Instance.gamesSlot)) { why = "You can't delete the game you're playing."; return false; }
        string path = SlotPath(slot);
        if (Path.GetFileName(path) != $"settlement_slot{slot}.json") { why = "Not a saved-game slot."; return false; }
        try
        {
            if (File.Exists(path)) File.Delete(path);
            PlayerPrefs.DeleteKey(SummaryKeyPrefix + slot);
            PlayerPrefs.Save();
            return true;
        }
        catch (Exception e)
        {
            why = e.Message;
            return false;
        }
    }
}

/// <summary>Runs a change of game across scene loads (see Settlement.SwitchGame).</summary>
public class GamesSwitcher : MonoBehaviour
{
    public void Begin(int slot, GameOptions newGame, int sceneIndex) => StartCoroutine(Run(slot, newGame, sceneIndex));

    private IEnumerator Run(int slot, GameOptions newGame, int sceneIndex)
    {
        Time.timeScale = 1f;
        var old = FreeModeController.Instance != null ? FreeModeController.Instance.gameObject.scene : SceneManager.GetActiveScene();
        // 1. Unload the free mode scene while the game still runs, so it tidies up as it always does.
        var holding = SceneManager.CreateScene("AOS_ChangingGame");
        SceneManager.SetActiveScene(holding);
        var unload = SceneManager.UnloadSceneAsync(old);
        if (unload == null)
        {
            Debug.LogError("[Settlement] Couldn't change game: the scene wouldn't unload. Nothing was changed.");
            SceneManager.SetActiveScene(old);
            SceneManager.UnloadSceneAsync(holding);
            Settlement.SwitchFinished();
            Destroy(gameObject);
            yield break;
        }
        while (!unload.isDone) yield return null;
        // 2. Save the old game to its own file and remove it.
        Settlement.ShutDownForSwitch();
        yield return null;
        // 3. Remember the new slot, 4. load the scene: it starts the settlement from that slot.
        Settlement.SelectSlot(slot, newGame);
        Debug.Log(newGame != null
            ? $"[Settlement] Starting a new game in slot {slot} ({Settlement.SlotFileName(slot)})."
            : $"[Settlement] Loading game {slot} ({Settlement.SlotFileName(slot)}).");
        SceneManager.LoadScene(sceneIndex, LoadSceneMode.Single);
        yield return null;
        Settlement.SwitchFinished();
        Destroy(gameObject);
    }
}
