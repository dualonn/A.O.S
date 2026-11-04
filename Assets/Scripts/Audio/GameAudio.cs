using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// The game's sound: one-shot effects, music that follows the age (battle music during raids),
/// an ambience loop that follows the biome under the camera, and a weather layer on top.
/// Clips live in Resources/Audio (built by Tools/Audio). A sound name like "sword_hit" picks a
/// random variant (sword_hit_1, sword_hit_2...). Effects load a few per frame at scene start
/// (listed in Resources/Audio/sfx_index.txt), so nothing stalls the first fight.
/// </summary>
public static class GameAudio
{
    public enum Mode { All, NoMusic, Muted }

    const float SfxVolume = 0.8f, UiVolume = 0.6f, MusicVolume = 0.42f, AmbienceVolume = 0.32f;
    const int Voices = 28;              // one-shots playing at once
    const int MaxSameSound = 3;         // copies of one sound at once (big battles)
    const float SameSoundGap = 0.05f;   // seconds between two starts of one sound
    const float DetailZoom = 10f;       // small work sounds only when zoomed in closer than this

    static readonly Dictionary<string, List<AudioClip>> clips = new Dictionary<string, List<AudioClip>>();
    static readonly Dictionary<string, float> lastStart = new Dictionary<string, float>();
    static readonly Dictionary<UnitDefinition, UnitSounds> unitSounds = new Dictionary<UnitDefinition, UnitSounds>();
    static AudioHost host;
    static Mode mode;
    static string weather;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        clips.Clear();
        lastStart.Clear();
        unitSounds.Clear();
        towerSounds.Clear();
        weather = null;
        host = null;
    }

    // Created before the first scene loads, so effects stream in while the map is being set up.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot() => Ensure();

    public static Mode CurrentMode => mode;

    static bool Ensure()
    {
        if (host != null) return true;
        if (!Application.isPlaying) return false;
        mode = (Mode)PlayerPrefs.GetInt("AOS.SoundMode", 0);
        var go = new GameObject("GameAudio");
        Object.DontDestroyOnLoad(go);
        host = go.AddComponent<AudioHost>();
        host.Init(Voices);
        host.StartCoroutine(LoadEffects());
        return true;
    }

    /// <summary>Load every effect a few per frame (async), so there is no hitch when the first ones play.</summary>
    static IEnumerator LoadEffects()
    {
        var index = Resources.Load<TextAsset>("Audio/sfx_index");
        if (index == null) yield break;
        var paths = index.text.Split('\n');
        Resources.UnloadAsset(index);
        const int perFrame = 6;
        var pending = new List<ResourceRequest>(perFrame);
        for (int i = 0; i < paths.Length; i += perFrame)
        {
            pending.Clear();
            for (int j = i; j < Mathf.Min(i + perFrame, paths.Length); j++)
            {
                string path = paths[j].Trim();
                if (path.Length > 0) pending.Add(Resources.LoadAsync<AudioClip>(path));
            }
            foreach (var request in pending)
            {
                while (!request.isDone) yield return null;
                if (request.asset is AudioClip clip)
                {
                    clip.LoadAudioData();
                    string key = BaseName(clip.name);
                    if (!clips.TryGetValue(key, out var list)) clips[key] = list = new List<AudioClip>();
                    list.Add(clip);
                }
            }
            yield return null;
        }
    }

    /// <summary>"sword_hit_3" -> "sword_hit" (variants share a name).</summary>
    static string BaseName(string name)
    {
        int u = name.LastIndexOf('_');
        if (u <= 0 || u == name.Length - 1) return name;
        for (int k = u + 1; k < name.Length; k++) if (name[k] < '0' || name[k] > '9') return name;
        return name.Substring(0, u);
    }

    /// <summary>A sound out in the world: panned and faded by where it is on screen; skipped far off screen.</summary>
    public static void Play(string name, Vector3 at, float volume = 1f)
    {
        if (name == null || !Ensure() || mode == Mode.Muted) return;
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

    /// <summary>A small everyday sound (tools, footsteps): only heard when zoomed in on it.</summary>
    public static void Detail(string name, Vector3 at, float volume = 1f)
    {
        var cam = Camera.main;
        if (cam != null && cam.orthographic && cam.orthographicSize > DetailZoom) return;
        Play(name, at, volume);
    }

    /// <summary>An interface or announcement sound: centred, no pitch change.</summary>
    public static void UI(string name, float volume = 1f)
    {
        if (name == null || !Ensure() || mode == Mode.Muted) return;
        Start(name, volume * UiVolume, 0f, 1f);
    }

    static void Start(string name, float volume, float pan, float pitch)
    {
        if (volume < 0.02f) return;
        if (!clips.TryGetValue(name, out var list) || list.Count == 0) return;   // unknown, or still loading
        float now = Time.unscaledTime;
        if (lastStart.TryGetValue(name, out float last) && now - last < SameSoundGap) return;
        if (host.CountPlaying(list) >= MaxSameSound) return;
        lastStart[name] = now;
        host.PlayOneShot(list[Random.Range(0, list.Count)], volume, pan, pitch);
    }

    // ------------------------------------------------------------------ combat

    /// <summary>The sounds one kind of unit makes, worked out once from its weapon and name.</summary>
    class UnitSounds
    {
        public string swing, fire, projectileHit, meleeHit, death;
    }

    static readonly string[] Machines = { "Automaton", "Android", "Robot", "Robo", "Mech", "Construct", "Car", "Tank", "Landship",
        "Walker", "Crawler", "Mole", "Drone", "Plane", "Jet", "Airship", "Skyship", "Frigate", "Cruiser", "Ark", "Engine", "Barque",
        "Skiff", "Sentinel", "Colossus", "Cannon", "Howitzer", "Throne", "GenesisShip", "RogueAI", "SunOrb", "Steam" };
    static readonly string[] Monsters = { "Troll", "Skeleton", "Spider", "Hive", "Unmade", "Void", "Rift", "Shade", "Wraith", "Bear",
        "Drake", "Wyvern", "Lich", "Devourer", "Herald", "Collapsar", "DarkSun", "HeatDeath", "Titan", "Golem", "Brute", "BarrowKing",
        "Unweaver", "Elephant", "Griffin" };
    static readonly string[] Lasers = { "Blaster", "Plasma", "Ray", "Laser", "Prism", "Nova", "Star", "Solar", "Sun", "Helion",
        "Void", "Rift", "Gravi", "Seraph", "Comet", "Fold" };
    static readonly string[] Rifles = { "Rifle", "Sniper", "Jager", "Infantry", "Commando", "MachineGun", "Gangster", "Hacker",
        "Biplane", "Jet", "Trench", "Galvanist", "Airship" };
    static readonly string[] Blasts = { "Tank", "Howitzer", "Cannon", "Grenadier", "Rocket", "Landship", "Armoured", "Walker", "Crawler" };

    static bool Has(string name, string[] words)
    {
        foreach (var w in words) if (name.Contains(w)) return true;
        return false;
    }

    static UnitSounds SoundsFor(Unit u)
    {
        var def = u.definition;
        if (def != null && unitSounds.TryGetValue(def, out var s)) return s;
        s = new UnitSounds();
        string id = def != null ? def.name : u.name;
        var weapon = u.WeaponKind;
        var beast = u.BeastKind;
        bool machine = Has(id, Machines) && !id.Contains("Titan") && !id.Contains("Hive");
        bool monster = !machine && (Has(id, Monsters) || (def != null && def.isNative));

        // swing (melee wind-up)
        if (beast != Beast.None || machine) s.swing = null;
        else if (id.Contains("Axe") || id.Contains("Barbarian") || id.Contains("Reaver") || id.Contains("Brute") || id.Contains("Club")) s.swing = "axe_swing";
        else if (id.Contains("Spear") || id.Contains("Pike") || id.Contains("Halberd") || id.Contains("Lance") || id.Contains("Hoplite")) s.swing = "spear_swing";
        else if (id.Contains("Dagger") || id.Contains("Thug") || id.Contains("Saboteur") || id.Contains("TunnelRat") || id.Contains("Sapper")) s.swing = "dagger_swing";
        else s.swing = "sword_swing";

        // ranged shot, and what its projectile sounds like landing. Guns follow the unit's age:
        // muskets up to the Enlightenment, rifles until the Arcane Age, energy weapons in space.
        int age = def != null && def.age != null ? def.age.order : 0;
        bool spaceAge = age >= 12;
        bool oldGun = id.Contains("Musket") || id.Contains("Arquebus") || id.Contains("Fusilier");
        if (weapon == WeaponClass.Staff) { s.fire = "staff_cast"; s.projectileHit = "magic_hit"; }
        else if (id.Contains("Grenad")) { s.fire = "throw"; s.projectileHit = null; }                      // the grenade's blast plays on landing
        else if (id.Contains("MachineGun") || id.Contains("Gatling")) { s.fire = "gatling"; s.projectileHit = "flesh_hit"; }
        else if (id.Contains("Missile") || id.Contains("Rocket")) { s.fire = "missile"; s.projectileHit = null; }
        else if (Has(id, Blasts) || weapon == WeaponClass.Cannon) { s.fire = spaceAge ? "beam" : "cannon_shot"; s.projectileHit = null; }   // splash plays the blast
        else if (Has(id, Lasers) && weapon != WeaponClass.Bow && weapon != WeaponClass.Thrown) { s.fire = "laser"; s.projectileHit = "flesh_hit"; }
        else if (weapon == WeaponClass.Musket || Has(id, Rifles))
        {
            s.fire = spaceAge && !oldGun ? "laser" : !oldGun && (age >= 7 || Has(id, Rifles)) ? "rifle_shot" : "musket_shot";
            s.projectileHit = "flesh_hit";
        }
        else if (weapon == WeaponClass.Crossbow) { s.fire = "crossbow_shot"; s.projectileHit = "arrow_hit"; }
        else if (weapon == WeaponClass.Thrown) { s.fire = spaceAge ? "laser" : "throw"; s.projectileHit = spaceAge ? "flesh_hit" : "blunt_hit"; }
        else { s.fire = "bow_shot"; s.projectileHit = "arrow_hit"; }

        // melee blow landing (armour is checked per hit)
        s.meleeHit = beast != Beast.None || monster ? "flesh_hit" : machine ? "blunt_hit" : null;

        // falling
        if (def != null && def.isBoss) s.death = machine ? "explosion" : "boss_death";
        else if (beast == Beast.Wolf) s.death = "wolf_growl";
        else if (beast == Beast.Spider) s.death = "spider_hiss";
        else if (machine) s.death = "cannon_impact";
        else if (monster) s.death = "monster_death";
        else s.death = "death";

        if (def != null) unitSounds[def] = s;
        return s;
    }

    /// <summary>A melee wind-up (beasts snarl now and then; machines just clank).</summary>
    public static void Swing(Unit u)
    {
        if (u == null) return;
        var s = SoundsFor(u);
        if (s.swing != null) Play(s.swing, u.transform.position, 0.45f);
        else if (Random.value < 0.25f)
        {
            var beast = u.BeastKind;
            Play(beast == Beast.Wolf ? "wolf_growl" : beast == Beast.Spider ? "spider_hiss" : "machine", u.transform.position, 0.5f);
        }
    }

    /// <summary>A unit lets fly.</summary>
    public static void Fire(Unit u)
    {
        if (u != null) Play(SoundsFor(u).fire, u.transform.position, 0.8f);
    }

    static readonly Dictionary<BuildingDefinition, string> towerSounds = new Dictionary<BuildingDefinition, string>();
    static readonly string[] TowerBeams = { "Laser", "Lance", "Beam", "Ray", "Mirror", "Uplink", "Light", "Lens", "Nova", "Quasar",
        "Singularity", "Paradox", "Rift", "Meteor", "Starfall", "Sun", "Phase", "Gravity", "Prism" };

    /// <summary>A tower lets fly: the sound is picked once per kind of tower, from its name and age.</summary>
    public static void TowerFire(BuildingDefinition def, Vector3 at)
    {
        if (def == null) return;
        if (!towerSounds.TryGetValue(def, out var sound))
        {
            string id = def.name;
            int age = def.age != null ? def.age.order : 0;
            if (id.Contains("Tesla")) sound = "zap";
            else if (id.Contains("Gatling")) sound = "gatling";
            else if (id.Contains("Railgun")) sound = "railgun";
            else if (id.Contains("Missile") || id.Contains("Rocket")) sound = "missile";
            else if (id.Contains("Ballista")) sound = "crossbow_shot";
            else if (id.Contains("Fireball")) sound = "fire_cast";
            else if (id.Contains("Arcane") || id.Contains("Obelisk") || id.Contains("Rune")) sound = "staff_cast";
            else if (Has(id, TowerBeams)) sound = def.towerSplash > 0f ? "beam" : "laser";
            else if (def.towerSplash > 0f) sound = age >= 12 ? "beam" : "cannon_shot";
            else if (def.towerMuzzleSmoke || id.Contains("Sniper") || id.Contains("Marksman") || id.Contains("Gun"))
                sound = age >= 7 ? "rifle_shot" : "musket_shot";
            else sound = age >= 12 ? "laser" : "bow_shot";
            towerSounds[def] = sound;
        }
        Play(sound, at, sound == "cannon_shot" || sound == "railgun" ? 1f : 0.8f);
    }

    /// <summary>A tower lets fly (old form: weapon class only).</summary>
    public static void Fire(WeaponClass weapon, Vector3 at)
    {
        switch (weapon)
        {
            case WeaponClass.Crossbow: Play("crossbow_shot", at, 0.8f); break;
            case WeaponClass.Musket: Play("musket_shot", at, 0.8f); break;
            case WeaponClass.Cannon: Play("cannon_shot", at); break;
            case WeaponClass.Staff: Play("staff_cast", at, 0.7f); break;
            default: Play("bow_shot", at, 0.8f); break;
        }
    }

    /// <summary>A projectile reaches its target (no shooter: a tower's arrow).</summary>
    public static void ProjectileHit(Unit shooter, Vector3 at)
    {
        Play(shooter != null ? SoundsFor(shooter).projectileHit : "arrow_hit", at, 0.6f);
    }

    /// <summary>A melee blow connects.</summary>
    public static void MeleeHit(Unit attacker, Unit target)
    {
        if (target == null) return;
        string sound = attacker != null ? SoundsFor(attacker).meleeHit : null;
        if (sound == null) sound = target.armour > 0 ? "sword_hit" : "blade_hit";
        Play(sound, target.transform.position, 0.75f);
    }

    /// <summary>A unit falls.</summary>
    public static void Death(Unit u)
    {
        if (u == null) return;
        var s = SoundsFor(u);
        var at = u.transform.position;
        Play(s.death, at, s.death == "death" ? 0.6f : 0.8f);
        if (s.death != "cannon_impact" && s.death != "explosion") Play("body_fall", at, 0.5f);
    }

    // ------------------------------------------------------------------ seasons, weather, events

    /// <summary>A new season begins.</summary>
    public static void SeasonStarts(Season season)
    {
        switch (season)
        {
            case Season.Spring: case Season.WetSeason: case Season.Rains: UI("season_spring"); break;
            case Season.Summer: UI("season_summer"); break;
            case Season.Autumn: UI("season_autumn"); break;
            case Season.Winter: UI("season_winter"); break;
            case Season.DrySeason: UI("wind_gust"); break;
        }
    }

    /// <summary>New weather rolls in (the weather loop itself comes from SetWeather).</summary>
    public static void WeatherStarts(WeatherKind kind)
    {
        switch (kind)
        {
            case WeatherKind.Storm: Thunder(); break;
            case WeatherKind.Sandstorm: case WeatherKind.AshFall: case WeatherKind.Snow: UI("wind_gust", 0.8f); break;
            case WeatherKind.Aurora: UI("faith_blessing", 0.6f); break;
        }
    }

    /// <summary>A random event's choice plays out: one sound per kind of event.</summary>
    public static void EventResolved(string kind)
    {
        switch (kind)
        {
            case "Merchant": case "LostCaravan": UI("market_trade"); break;
            case "Festival": UI("festival"); break;
            case "Harvest": UI("season_autumn"); break;
            case "Drought": UI("wind_gust"); break;
            case "Plague": UI("plague_bell"); break;
            case "Meteor": case "Comet": UI("meteor", 0.8f); break;
            case "Refugees": UI("wagon"); break;
            case "WanderingHero": UI("hero_recruit"); break;
            case "Stranger": UI("great_person_used"); break;
            case "GoldRush": UI("pick"); UI("coins"); break;
            case "Wildfire": UI("fire_crackle"); break;
            case "Eclipse": UI("lore_stone", 0.8f); break;
            case "Rival": UI("sword_draw"); break;
            case "Scholars": UI("page_flip"); break;
            case "SolarFlare": UI("zap"); break;
            case "Derelict": UI("scifi_door"); break;
            default: UI("confirm"); break;
        }
    }

    // ------------------------------------------------------------------ town

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
        var at = controller.Grid.AreaCenter(building.cell, def.size);
        Play(name, at, volume);
        if (name == "build_complete" && UtilitySound(def) is string settle) Play(settle, at, 0.8f);
    }

    /// <summary>The sound a finished utility makes as it comes to life (power, water, Tesla masts).</summary>
    public static string UtilitySound(BuildingDefinition def)
    {
        string id = def != null ? def.name : "";
        if (!id.StartsWith("Utl")) return null;
        if (id.Contains("Tesla")) return "utl_tesla_crackle";
        if (id.Contains("Aqueduct") || id.Contains("Fountain") || id.Contains("Waterwheel") || id.Contains("Pump") || id.Contains("Cistern"))
            return "utl_aqueduct_flow";
        if (id.Contains("Dynamo") || id.Contains("Plant") || id.Contains("Dam") || id.Contains("Turbine") || id.Contains("Reactor")
            || id.Contains("Substation") || id.Contains("Pylon") || id.Contains("Tap") || id.Contains("Collector") || id.Contains("Engine"))
            return "utl_power_hum";
        return null;
    }

    // ------------------------------------------------------------------ settings button

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
        if (order <= 15) return "space";         // Astral, Stellar, Interstellar, Galactic
        return "universal";                      // Universal: choirs and a slow clock (creation and time)
    }

    /// <summary>
    /// Weather sound on top of the biome ambience: "rain", "storm", "snow", "wind", or null for clear skies.
    /// Cross-fades when it changes; call it whenever the weather changes (repeat calls are free).
    /// </summary>
    public static void SetWeather(string kind)
    {
        Ensure();
        weather = string.IsNullOrEmpty(kind) ? null : kind;
    }

    /// <summary>A thunderclap, somewhere overhead (pass the strike point to place it on screen).</summary>
    public static void Thunder(Vector3? at = null)
    {
        if (at.HasValue) Play("thunder", at.Value, 1.2f);
        else UI("thunder", 0.9f);
    }

    internal static string WeatherLoop()
    {
        switch (weather)
        {
            case null: return null;
            case "snow": case "blizzard": return "amb_tundra";
            case "wind": return "amb_barren";
            default: return "amb_" + weather;   // amb_rain, amb_storm
        }
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

/// <summary>Holds the audio sources: a pool for one-shots, and pairs for music, ambience and weather (for cross-fades).</summary>
public class AudioHost : MonoBehaviour
{
    AudioSource[] voices;
    AudioSource musicA, musicB, ambA, ambB, wxA, wxB;
    string era, ambience, weatherLoop;
    float nextCheck, silenceUntil, battleHoldUntil;
    readonly Dictionary<string, List<string>> playlists = new Dictionary<string, List<string>>();
    readonly Dictionary<string, int> playlistPos = new Dictionary<string, int>();

    public void Init(int count)
    {
        voices = new AudioSource[count];
        for (int i = 0; i < count; i++) voices[i] = NewSource(false);
        musicA = NewSource(false); musicB = NewSource(false);
        ambA = NewSource(true); ambB = NewSource(true);
        wxA = NewSource(true); wxB = NewSource(true);
        silenceUntil = Time.unscaledTime + 1.5f;   // let the scene settle first
    }

    AudioSource NewSource(bool loop)
    {
        var s = gameObject.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.loop = loop;
        s.spatialBlend = 0f;
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
            nextCheck = now + 0.5f;
            UpdateMusic(now);
            UpdateAmbience();
            UpdateWeather();
        }
        Fade(musicA, musicB, GameAudio.MusicLevel, dt, 0.8f);   // ~0.5 s cross-fades
        Fade(ambA, ambB, GameAudio.AmbienceLevel, dt, 0.5f);
        Fade(wxA, wxB, weatherLoop != null ? GameAudio.AmbienceLevel * 1.1f : 0f, dt, 0.25f);
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
        if (want == "battle") battleHoldUntil = now + 3f;
        else if (era == "battle" && now < battleHoldUntil) want = "battle";   // don't flicker between short raids

        bool ended = musicA.clip != null && !musicA.isPlaying;
        bool nearEnd = musicA.isPlaying && musicA.clip != null && musicA.time > musicA.clip.length - 1f;
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
            silenceUntil = now + Random.Range(3f, 8f);
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

    void UpdateWeather()
    {
        string want = GameAudio.WeatherLoop();
        if (want == weatherLoop) return;
        weatherLoop = want;
        Swap(ref wxA, ref wxB);
        wxA.Stop();
        wxA.clip = want != null ? Resources.Load<AudioClip>("Audio/Ambience/" + want) : null;
        wxA.volume = 0f;
        if (wxA.clip != null)
        {
            wxA.time = Random.Range(0f, wxA.clip.length * 0.8f);
            wxA.Play();
        }
    }

    static void Swap(ref AudioSource a, ref AudioSource b) => (a, b) = (b, a);
}
