using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The player's settings (volumes, UI scale, screen, autosave, hints), saved in PlayerPrefs and
/// loaded before the first scene. Every change applies at once, is saved, and raises Changed.
/// The Settings window (SettingsPanel) edits these; the game reads them.
/// </summary>
public static class GameSettings
{
    // PlayerPrefs keys. The volume keys are shared with GameAudio ("AOS.Vol." + channel).
    public const string VolumePrefix = "AOS.Vol.";
    public const string UIScaleKey = "AOS.UIScale";
    public const string FullscreenKey = "AOS.Fullscreen";
    public const string ResolutionWidthKey = "AOS.ResolutionW";
    public const string ResolutionHeightKey = "AOS.ResolutionH";
    public const string VSyncKey = "AOS.VSync";
    public const string AutosaveKey = "AOS.AutosaveMinutes";
    public const string ShowHintsKey = "AOS.ShowHints";

    /// <summary>The volume channels, in the order the Settings window shows them.</summary>
    public static readonly string[] Channels = { "Master", "Music", "Effects", "Ambience" };

    public const float UIScaleMin = 0.75f, UIScaleMax = 1.5f, UIScaleStep = 0.05f;
    public static readonly Vector2 ReferenceResolution = new Vector2(1920, 1080);

    /// <summary>The autosave choices, in minutes.</summary>
    public static readonly float[] AutosaveOptions = { 0.5f, 1f, 2f, 5f, 10f };

    /// <summary>Raised after any setting changes (the HUD re-applies the UI scale on it).</summary>
    public static event Action Changed;

    private static readonly float[] volumes = { 1f, 1f, 1f, 1f };
    private static float uiScale = 1f;
    private static float autosaveMinutes = 0.5f;
    private static bool showHints = true;
    private static bool fullscreen = true;
    private static bool vSync = true;
    private static Vector2Int resolution;   // (0,0) = whatever the screen is now
    private static bool loaded;

    // GameAudio.SetVolume(string, float), found by reflection so this compiles with or without it.
    private static MethodInfo setVolume;
    private static bool setVolumeLooked;

    // Domain reload is off: start each play from scratch.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Changed = null;
        loaded = false;
        setVolume = null;
        setVolumeLooked = false;
        resolutionList = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void LoadOnStartup()
    {
        Load();
        ApplyVolumes();
        ApplyScreen();
    }

    /// <summary>Read everything from PlayerPrefs (safe to call more than once).</summary>
    public static void Load()
    {
        for (int i = 0; i < Channels.Length; i++)
            volumes[i] = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumePrefix + Channels[i], 1f));
        uiScale = SnapScale(PlayerPrefs.GetFloat(UIScaleKey, 1f));
        autosaveMinutes = NearestAutosave(PlayerPrefs.GetFloat(AutosaveKey, 0.5f));
        showHints = PlayerPrefs.GetInt(ShowHintsKey, 1) == 1;
        fullscreen = PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1;
        vSync = PlayerPrefs.GetInt(VSyncKey, QualitySettings.vSyncCount > 0 ? 1 : 0) == 1;
        resolution = new Vector2Int(PlayerPrefs.GetInt(ResolutionWidthKey, 0), PlayerPrefs.GetInt(ResolutionHeightKey, 0));
        loaded = true;
    }

    private static void EnsureLoaded()
    {
        if (!loaded) Load();
    }

    private static void Save()
    {
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    // ---------- Volume ----------

    /// <summary>A channel's volume, 0..1 ("Master", "Music", "Effects", "Ambience").</summary>
    public static float GetVolume(string channel)
    {
        EnsureLoaded();
        int i = Array.IndexOf(Channels, channel);
        return i >= 0 ? volumes[i] : 1f;
    }

    public static void SetVolume(string channel, float value)
    {
        EnsureLoaded();
        int i = Array.IndexOf(Channels, channel);
        if (i < 0) return;
        volumes[i] = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(VolumePrefix + channel, volumes[i]);
        ApplyVolume(i);
        Save();
    }

    public static float MasterVolume { get => GetVolume("Master"); set => SetVolume("Master", value); }
    public static float MusicVolume { get => GetVolume("Music"); set => SetVolume("Music", value); }
    public static float EffectsVolume { get => GetVolume("Effects"); set => SetVolume("Effects", value); }
    public static float AmbienceVolume { get => GetVolume("Ambience"); set => SetVolume("Ambience", value); }

    private static void ApplyVolumes()
    {
        for (int i = 0; i < Channels.Length; i++) ApplyVolume(i);
    }

    /// <summary>Hand a channel to GameAudio.SetVolume if it has one; otherwise Master goes to the listener.</summary>
    private static void ApplyVolume(int i)
    {
        if (!setVolumeLooked)
        {
            setVolumeLooked = true;
            setVolume = typeof(GameAudio).GetMethod("SetVolume", BindingFlags.Static | BindingFlags.Public,
                null, new[] { typeof(string), typeof(float) }, null);
        }
        if (setVolume != null)
        {
            try { setVolume.Invoke(null, new object[] { Channels[i], volumes[i] }); }
            catch (Exception e) { Debug.LogWarning("GameSettings: GameAudio.SetVolume failed: " + e.Message); }
        }
        else if (i == 0)
        {
            AudioListener.volume = volumes[0];
        }
    }

    // ---------- UI scale ----------

    /// <summary>How big the HUD and menus are drawn, 0.75..1.5 (1 = normal).</summary>
    public static float UIScale
    {
        get { EnsureLoaded(); return uiScale; }
        set
        {
            EnsureLoaded();
            uiScale = SnapScale(value);
            PlayerPrefs.SetFloat(UIScaleKey, uiScale);
            Save();
        }
    }

    private static float SnapScale(float value) =>
        Mathf.Clamp(Mathf.Round(value / UIScaleStep) * UIScaleStep, UIScaleMin, UIScaleMax);

    /// <summary>Size a canvas for the UI scale: a smaller reference resolution draws everything bigger.</summary>
    public static void ApplyUIScale(CanvasScaler scaler)
    {
        if (scaler == null) return;
        scaler.referenceResolution = ReferenceResolution / UIScale;
    }

    // ---------- Game ----------

    /// <summary>Minutes between autosaves (one of AutosaveOptions).</summary>
    public static float AutosaveMinutes
    {
        get { EnsureLoaded(); return autosaveMinutes; }
        set
        {
            EnsureLoaded();
            autosaveMinutes = NearestAutosave(value);
            PlayerPrefs.SetFloat(AutosaveKey, autosaveMinutes);
            Save();
        }
    }

    /// <summary>Seconds between autosaves.</summary>
    public static float AutosaveSeconds => AutosaveMinutes * 60f;

    private static float NearestAutosave(float minutes)
    {
        float best = AutosaveOptions[0];
        foreach (var option in AutosaveOptions)
            if (Mathf.Abs(option - minutes) < Mathf.Abs(best - minutes)) best = option;
        return best;
    }

    /// <summary>Whether the game shows its hint cards.</summary>
    public static bool ShowHints
    {
        get { EnsureLoaded(); return showHints; }
        set
        {
            EnsureLoaded();
            showHints = value;
            PlayerPrefs.SetInt(ShowHintsKey, value ? 1 : 0);
            Save();
        }
    }

    // ---------- Screen (PC only) ----------

    /// <summary>True where the screen settings mean something (not in a web page).</summary>
    public static bool HasScreenSettings
    {
        get
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            return true;
#else
            return false;
#endif
        }
    }

    public static bool Fullscreen
    {
        get { EnsureLoaded(); return fullscreen; }
        set
        {
            EnsureLoaded();
            fullscreen = value;
            PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0);
            ApplyScreen();
            Save();
        }
    }

    public static bool VSync
    {
        get { EnsureLoaded(); return vSync; }
        set
        {
            EnsureLoaded();
            vSync = value;
            PlayerPrefs.SetInt(VSyncKey, value ? 1 : 0);
            ApplyScreen();
            Save();
        }
    }

    private static List<Resolution> resolutionList;

    /// <summary>The screen sizes on offer: one per width x height (its highest refresh rate), smallest first.</summary>
    public static List<Resolution> Resolutions
    {
        get
        {
            if (resolutionList != null && resolutionList.Count > 0) return resolutionList;
            var best = new Dictionary<Vector2Int, Resolution>();
            foreach (var r in Screen.resolutions)
            {
                var key = new Vector2Int(r.width, r.height);
                if (!best.TryGetValue(key, out var have) || r.refreshRateRatio.value > have.refreshRateRatio.value) best[key] = r;
            }
            resolutionList = new List<Resolution>(best.Values);
            resolutionList.Sort((a, b) => a.width != b.width ? a.width.CompareTo(b.width) : a.height.CompareTo(b.height));
            return resolutionList;
        }
    }

    /// <summary>The chosen size's index in Resolutions (the current screen size if none was picked; -1 if neither is listed).</summary>
    public static int ResolutionIndex
    {
        get
        {
            EnsureLoaded();
            var list = Resolutions;
            var want = resolution.x > 0 ? resolution : new Vector2Int(Screen.width, Screen.height);
            for (int i = 0; i < list.Count; i++)
                if (list[i].width == want.x && list[i].height == want.y) return i;
            return list.Count > 0 ? list.Count - 1 : -1;
        }
        set
        {
            EnsureLoaded();
            var list = Resolutions;
            if (list.Count == 0) return;
            var r = list[Mathf.Clamp(value, 0, list.Count - 1)];
            resolution = new Vector2Int(r.width, r.height);
            PlayerPrefs.SetInt(ResolutionWidthKey, r.width);
            PlayerPrefs.SetInt(ResolutionHeightKey, r.height);
            ApplyScreen();
            Save();
        }
    }

    /// <summary>"1920x1080" for a size in Resolutions.</summary>
    public static string ResolutionLabel(int index)
    {
        var list = Resolutions;
        if (index < 0 || index >= list.Count) return $"{Screen.width}x{Screen.height}";
        return $"{list[index].width}x{list[index].height}";
    }

    /// <summary>Put the saved window mode, size and VSync on the screen (PC builds only).</summary>
    private static void ApplyScreen()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        QualitySettings.vSyncCount = vSync ? 1 : 0;
        if (Application.isEditor) return;   // (the Game view keeps its own size; the choices are still saved)
        var mode = fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        if (resolution.x > 0 && resolution.y > 0)
            Screen.SetResolution(resolution.x, resolution.y, mode);
        else
            Screen.fullScreenMode = mode;
#endif
    }

    // ---------- Defaults ----------

    /// <summary>Everything back to how a new player starts (the screen size stays as it is).</summary>
    public static void ResetToDefaults()
    {
        EnsureLoaded();
        for (int i = 0; i < Channels.Length; i++)
        {
            volumes[i] = 1f;
            PlayerPrefs.SetFloat(VolumePrefix + Channels[i], 1f);
        }
        ApplyVolumes();
        uiScale = 1f;
        PlayerPrefs.SetFloat(UIScaleKey, uiScale);
        autosaveMinutes = 0.5f;
        PlayerPrefs.SetFloat(AutosaveKey, autosaveMinutes);
        showHints = true;
        PlayerPrefs.SetInt(ShowHintsKey, 1);
        fullscreen = true;
        PlayerPrefs.SetInt(FullscreenKey, 1);
        vSync = true;
        PlayerPrefs.SetInt(VSyncKey, 1);
        resolution = Vector2Int.zero;
        PlayerPrefs.DeleteKey(ResolutionWidthKey);
        PlayerPrefs.DeleteKey(ResolutionHeightKey);
        ApplyScreen();
        Save();
    }
}
