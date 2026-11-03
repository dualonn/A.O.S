using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The game's sound: one-shot effects, music that follows the age (battle music during raids),
/// and an ambience loop that follows the biome under the camera.
/// Clips live in Resources/Audio (built by Tools/Audio). A sound name like "sword_hit" picks a
/// random variant (sword_hit_1, sword_hit_2...).
/// </summary>
public static class GameAudio
{
    public enum Mode { All, NoMusic, Muted }

    const float SfxVolume = 0.8f, UiVolume = 0.6f, MusicVolume = 0.42f, AmbienceVolume = 0.32f;
    const int Voices = 28;              // one-shots playing at once
    const int MaxSameSound = 3;         // copies of one sound at once (big battles)
    const float SameSoundGap = 0.05f;   // seconds between two starts of one sound

    static Dictionary<string, List<AudioClip>> clips;
    static readonly Dictionary<string, float> lastStart = new Dictionary<string, float>();
    static AudioHost host;
    static Mode mode;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        clips = null;
        lastStart.Clear();
        host = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot() => Ensure();

    public static Mode CurrentMode => mode;

    static bool Ensure()
    {
        if (host != null) return true;
        if (!Application.isPlaying) return false;
        mode = (Mode)PlayerPrefs.GetInt("AOS.SoundMode", 0);
        clips = new Dictionary<string, List<AudioClip>>();
        var variant = new Regex(@"_\d+$");
        foreach (var clip in Resources.LoadAll<AudioClip>("Audio/SFX"))
        {
            string key = variant.Replace(clip.name, "");
            if (!clips.TryGetValue(key, out var list)) clips[key] = list = new List<AudioClip>();
            list.Add(clip);
        }
        var go = new GameObject("GameAudio");
        Object.DontDestroyOnLoad(go);
        host = go.AddComponent<AudioHost>();
        host.Init(Voices);
        return true;
    }

    /// <summary>A sound out in the world: panned and faded by where it is on screen; skipped far off screen.</summary>
    public static void Play(string name, Vector3 at, float volume = 1f)
    {
        if (!Ensure() || mode == Mode.Muted) return;
        var cam = Camera.main;
        float pan = 0f;
        if (cam != null)
        {
            var vp = cam.WorldToViewportPoint(at);
            float off = Mathf.Max(Mathf.Max(-vp.x, vp.x - 1f), Mathf.Max(-vp.y, vp.y - 1f));
            if (off > 0.35f) return;                                     // well off screen: silent
            if (off > 0f) volume *= 1f - off / 0.35f;
            pan = Mathf.Clamp((vp.x - 0.5f) * 1.3f, -0.75f, 0.75f);
            if (cam.orthographic)                                         // zoomed far out: quieter
                volume *= Mathf.Clamp(9f / Mathf.Max(1f, cam.orthographicSize), 0.3f, 1f);
        }
        Start(name, volume * SfxVolume, pan, Random.Range(0.93f, 1.07f));
    }

    /// <summary>An interface or announcement sound: centred, no pitch change.</summary>
    public static void UI(string name, float volume = 1f)
    {
        if (!Ensure() || mode == Mode.Muted) return;
        Start(name, volume * UiVolume, 0f, 1f);
    }

    static void Start(string name, float volume, float pan, float pitch)
    {
        if (volume < 0.02f) return;
        if (!clips.TryGetValue(name, out var list) || list.Count == 0)
        {
            if (Debug.isDebugBuild && !lastStart.ContainsKey("missing:" + name))
            {
                lastStart["missing:" + name] = 0f;
                Debug.LogWarning($"GameAudio: no sound called '{name}'");
            }
            return;
        }
        float now = Time.unscaledTime;
        if (lastStart.TryGetValue(name, out float last) && now - last < SameSoundGap) return;
        if (host.CountPlaying(list) >= MaxSameSound) return;
        lastStart[name] = now;
        host.PlayOneShot(list[Random.Range(0, list.Count)], volume, pan, pitch);
    }

    // ------------------------------------------------------------------ combat helpers

    /// <summary>What a unit fights with (from its sprite setup).</summary>
    public static WeaponClass WeaponOf(Unit u)
    {
        var v = u != null ? u.GetComponent<SpriteUnitVisual>() : null;
        return v != null ? v.weaponClass : WeaponClass.Melee;
    }

    /// <summary>Wolf, spider or none.</summary>
    public static Beast BeastOf(Unit u)
    {
        var v = u != null ? u.GetComponent<SpriteUnitVisual>() : null;
        return v != null && v.figure != null ? v.figure.beast : Beast.None;
    }

    /// <summary>A melee wind-up (beasts snarl now and then instead of whooshing a blade).</summary>
    public static void Swing(Unit u)
    {
        if (u == null) return;
        switch (BeastOf(u))
        {
            case Beast.Wolf: if (Random.value < 0.3f) Play("wolf_growl", u.transform.position, 0.6f); return;
            case Beast.Spider: if (Random.value < 0.3f) Play("spider_hiss", u.transform.position, 0.6f); return;
        }
        Play("sword_swing", u.transform.position, 0.45f);
    }

    /// <summary>A ranged unit (or tower) lets fly.</summary>
    public static void Fire(WeaponClass weapon, Vector3 at)
    {
        switch (weapon)
        {
            case WeaponClass.Crossbow: Play("crossbow_shot", at, 0.8f); break;
            case WeaponClass.Thrown: Play("throw", at, 0.7f); break;
            case WeaponClass.Staff: Play("staff_cast", at, 0.7f); break;
            case WeaponClass.Musket: Play("musket_shot", at, 0.8f); break;
            case WeaponClass.Cannon: Play("cannon_shot", at); break;
            default: Play("bow_shot", at, 0.8f); break;
        }
    }

    /// <summary>A melee blow connects.</summary>
    public static void MeleeHit(Unit attacker, Unit target)
    {
        if (target == null) return;
        string sound = attacker != null && BeastOf(attacker) != Beast.None ? "flesh_hit"
            : target.armour > 0 ? "sword_hit" : "blade_hit";
        Play(sound, target.transform.position, 0.75f);
    }

    /// <summary>The tool sound a villager makes working at this building (null: works quietly).</summary>
    public static string WorkSound(BuildingDefinition def)
    {
        if (def == null) return null;
        string deposit = def.requiredDeposit != null ? def.requiredDeposit.name : "";
        string id = def.name;
        if (deposit.Contains("Forest") || id.Contains("Lumber") || id.Contains("Wood")) return "chop";
        if (deposit.Contains("Berr") || id.Contains("Forag")) return "forage";
        if (deposit.Length > 0 || id.Contains("Quarry") || id.Contains("Mine")) return "pick";
        if (id.Contains("Farm") || id.Contains("Field")) return "hoe";
        if (id.Contains("Smith") || id.Contains("Forge") || id.Contains("Foundry") || id.Contains("Armour") || id.Contains("Weapon")) return "forge";
        if (id.Contains("Saw") || id.Contains("Carpent")) return "saw_wood";
        return null;
    }

    /// <summary>A sound at a building on the live map (nothing if no map is showing).</summary>
    public static void AtBuilding(string name, BuildingState building, BuildingDefinition def, float volume = 1f)
    {
        var controller = FreeModeController.Instance;
        if (controller == null || controller.Grid == null || building == null || def == null) return;
        Play(name, controller.Grid.AreaCenter(building.cell, def.size), volume);
    }

    /// <summary>A unit falls.</summary>
    public static void Death(Unit u, bool boss)
    {
        if (u == null) return;
        var at = u.transform.position;
        if (boss) Play("boss_death", at);
        else if (BeastOf(u) == Beast.Wolf) Play("wolf_growl", at, 0.8f);
        else if (BeastOf(u) == Beast.Spider) Play("spider_hiss", at, 0.8f);
        else Play("death", at, 0.6f);
        Play("body_fall", at, 0.5f);
    }

    /// <summary>Cycles sound on / music off / all off (kept between sessions).</summary>
    public static void CycleMode()
    {
        Ensure();
        mode = (Mode)(((int)mode + 1) % 3);
        PlayerPrefs.SetInt("AOS.SoundMode", (int)mode);
        PlayerPrefs.Save();
    }

    public static string ModeLabel => mode == Mode.All ? "Sound" : mode == Mode.NoMusic ? "No music" : "Muted";

    /// <summary>The top-bar sound button (sound on / music off / muted).</summary>
    public static void AddSoundButton(Transform bar, float x)
    {
        Ensure();
        TextMeshProUGUI label = null;
        var button = UIBuilder.Button("Sound", bar, () => { CycleMode(); label.text = ModeLabel; }, out label, 40, 18);
        UIBuilder.Anchor((RectTransform)button.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f),
            new Vector2(x, 0), new Vector2(100, 40));
        label.text = ModeLabel;
        label.alignment = TextAlignmentOptions.Center;
    }

    // ------------------------------------------------------------------ music & ambience choice

    internal static float MusicLevel => mode == Mode.All ? MusicVolume : 0f;
    internal static float AmbienceLevel => mode == Mode.Muted ? 0f : AmbienceVolume;

    /// <summary>Which music playlist fits right now.</summary>
    internal static string MusicEra()
    {
        var controller = FreeModeController.Instance;
        if (controller != null && controller.WaveInProgress) return "battle";
        var settlement = Settlement.Instance;
        var age = settlement != null ? settlement.CurrentAge : null;
        int order = age != null ? age.order : 0;
        if (order <= 3) return "ancient";        // Stone, Bronze, Iron, Classical
        if (order == 4) return "medieval";
        if (order <= 6) return "renaissance";    // Renaissance, Enlightenment
        if (order == 7) return "industrial";
        if (order <= 9) return "modern";         // Modern, Electric
        if (order <= 11) return "information";   // Information, Arcane
        return "space";                          // Astral, Stellar, Interstellar, Galactic
    }

    /// <summary>Which ambience loop fits the place the camera is looking at.</summary>
    internal static string AmbienceName()
    {
        var controller = FreeModeController.Instance;
        if (controller != null && controller.Viewing != null) return "amb_dungeon";
        var settlement = Settlement.Instance;
        var cam = Camera.main;
        if (settlement == null || settlement.Map == null || cam == null) return "amb_temperate";
        switch (settlement.Map.BiomeAt(cam.transform.position))
        {
            case Biome.Desert: return "amb_desert";
            case Biome.Tundra: return "amb_tundra";
            case Biome.Swamp: return "amb_swamp";
            case Biome.Volcanic: return "amb_volcanic";
            case Biome.Crystal: return "amb_crystal";
            case Biome.Barren: return "amb_barren";
            case Biome.Jungle: return "amb_jungle";
            case Biome.Fungal: return "amb_fungal";
            default: return "amb_temperate";
        }
    }
}

/// <summary>Holds the audio sources: a pool for one-shots, two for music and two for ambience (for cross-fades).</summary>
public class AudioHost : MonoBehaviour
{
    AudioSource[] voices;
    AudioSource musicA, musicB, ambA, ambB;
    string era, ambience;
    float nextCheck, silenceUntil, battleHoldUntil;
    readonly Dictionary<string, List<string>> playlists = new Dictionary<string, List<string>>();
    readonly Dictionary<string, int> playlistPos = new Dictionary<string, int>();

    public void Init(int count)
    {
        voices = new AudioSource[count];
        for (int i = 0; i < count; i++) voices[i] = NewSource(false);
        musicA = NewSource(false); musicB = NewSource(false);
        ambA = NewSource(true); ambB = NewSource(true);
        silenceUntil = Time.unscaledTime + 1.5f;   // let the scene settle first
    }

    AudioSource NewSource(bool loop)
    {
        var s = gameObject.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.loop = loop;
        s.spatialBlend = 0f;
        s.ignoreListenerPause = false;
        return s;
    }

    public int CountPlaying(List<AudioClip> variants)
    {
        int n = 0;
        foreach (var v in voices) if (v.isPlaying && variants.Contains(v.clip)) n++;
        return n;
    }

    public void PlayOneShot(AudioClip clip, float volume, float pan, float pitch)
    {
        AudioSource free = null;
        foreach (var v in voices) if (!v.isPlaying) { free = v; break; }
        if (free == null) return;   // everything busy: drop it rather than cut another sound
        free.clip = clip;
        free.volume = volume;
        free.panStereo = pan;
        free.pitch = pitch;
        free.Play();
    }

    void Update()
    {
        float now = Time.unscaledTime;
        float dt = Time.unscaledDeltaTime;
        if (now >= nextCheck)
        {
            nextCheck = now + 1f;
            UpdateMusic(now);
            UpdateAmbience();
        }
        Fade(musicA, musicB, GameAudio.MusicLevel, dt, 0.4f);
        Fade(ambA, ambB, GameAudio.AmbienceLevel, dt, 0.5f);
    }

    // Source A is the one fading in (or holding); B fades out and stops.
    static void Fade(AudioSource a, AudioSource b, float level, float dt, float speed)
    {
        if (a.clip != null) a.volume = Mathf.MoveTowards(a.volume, level, dt * speed * Mathf.Max(level, 0.05f) * 2f);
        if (b.isPlaying)
        {
            b.volume = Mathf.MoveTowards(b.volume, 0f, dt * speed);
            if (b.volume <= 0.001f) b.Stop();
        }
    }

    void UpdateMusic(float now)
    {
        string want = GameAudio.MusicEra();
        if (want == "battle") battleHoldUntil = now + 15f;
        else if (era == "battle" && now < battleHoldUntil) want = "battle";   // don't flicker between short raids

        bool ended = musicA.clip != null && !musicA.isPlaying;
        bool nearEnd = musicA.isPlaying && musicA.clip != null && musicA.time > musicA.clip.length - 4f;
        if (want != era)
        {
            era = want;
            StartTrack(want);
        }
        else if (nearEnd)
        {
            // let the track finish, then a quiet gap before the next one
            Swap(ref musicA, ref musicB);
            musicA.clip = null;
            silenceUntil = now + Random.Range(12f, 35f);
        }
        else if ((ended || musicA.clip == null) && now >= silenceUntil)
            StartTrack(want);
    }

    void StartTrack(string playlist)
    {
        var clip = NextTrack(playlist);
        if (clip == null) return;
        Swap(ref musicA, ref musicB);
        musicA.clip = clip;
        musicA.volume = 0f;
        musicA.loop = false;
        musicA.Play();
    }

    AudioClip NextTrack(string playlist)
    {
        if (!playlists.TryGetValue(playlist, out var names))
        {
            names = new List<string>();
            for (int i = 1; i < 50; i++)
            {
                string path = $"Audio/Music/{playlist}_{i}";
                if (Resources.Load<AudioClip>(path) == null) break;
                names.Add(path);
            }
            // shuffle once; then play through in order so tracks don't repeat back to back
            for (int i = names.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (names[i], names[j]) = (names[j], names[i]); }
            playlists[playlist] = names;
        }
        if (names.Count == 0) return null;
        playlistPos.TryGetValue(playlist, out int pos);
        playlistPos[playlist] = pos + 1;
        return Resources.Load<AudioClip>(names[pos % names.Count]);
    }

    void UpdateAmbience()
    {
        string want = GameAudio.AmbienceName();
        if (want == ambience) return;
        var clip = Resources.Load<AudioClip>("Audio/Ambience/" + want);
        if (clip == null) return;
        ambience = want;
        Swap(ref ambA, ref ambB);
        ambA.clip = clip;
        ambA.volume = 0f;
        ambA.time = Random.Range(0f, clip.length * 0.8f);   // don't always start at the same spot
        ambA.Play();
    }

    static void Swap(ref AudioSource a, ref AudioSource b) => (a, b) = (b, a);
}
