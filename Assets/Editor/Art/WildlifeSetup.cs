using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using P = PixelPalette;
using K = BuildingKit;

/// <summary>
/// Wildlife, hunting and taming: every wild creature (its art, the wild unit a herd becomes when it
/// fights, and the war beast it's tamed into), the hunting buildings (Hunting Camp, Game Ranch,
/// Game Reserve, Xenobiology Lab), the taming buildings (Beast Pen, Royal Menagerie, Xenofauna Pen)
/// and their research. Makes and draws only its own assets; safe to run again.
/// (Tools > Age of Stickman > Setup Wildlife.)
/// </summary>
public static partial class PlaceholderArt
{
    private struct WildUnitSpec
    {
        public string id;
        public int hp, atk;
        public float speed, range, cooldown;
    }

    // Wild stats: (species, health, attack, speed, reach, seconds between attacks).
    private static readonly WildUnitSpec[] WildUnitSpecs =
    {
        W("Deer", 90, 6, 1.3f), W("Antelope", 80, 6, 1.4f), W("Boar", 180, 14, 1.2f), W("Wolf", 110, 12, 1.4f),
        W("Bear", 450, 26, 1.0f, 0.8f, 1.3f), W("Mammoth", 1100, 34, 0.8f, 1.0f, 1.6f), W("Mossback", 200, 8, 0.7f),
        W("Griffin", 650, 38, 1.5f, 0.8f), W("Wyvern", 850, 46, 1.4f, 0.9f), W("CrystalBeast", 1500, 42, 0.8f, 1.0f, 1.5f),
        W("FrostStrider", 500, 40, 1.3f), W("IceLurker", 1100, 90, 1.4f), W("SandSkitter", 400, 30, 1.4f),
        W("DuneWyrm", 2400, 110, 0.8f, 1.1f, 1.5f), W("EmberHound", 900, 85, 1.5f), W("MagmaTortoise", 2600, 95, 0.6f, 1.0f, 1.6f),
        W("RazorRaptor", 950, 95, 1.6f), W("CanopyStrider", 1200, 50, 1.1f, 1.0f), W("ShardStalker", 1050, 95, 1.5f),
        W("PrismMoth", 400, 30, 1.3f), W("MireLurker", 1400, 100, 1.0f, 0.9f), W("ShellCrawler", 700, 50, 1.0f),
        W("CraterHulk", 2400, 115, 0.8f, 1.0f, 1.5f), W("SporeGrazer", 600, 30, 0.9f),
        W("WhiteStag", 2500, 45, 1.5f, 0.9f), W("AncientMammoth", 6000, 70, 0.8f, 1.2f, 1.6f), W("StormGriffin", 4000, 80, 1.6f, 1.0f),
        W("SkyWhale", 12000, 160, 0.7f, 1.4f, 1.8f), W("Shardback", 9000, 170, 0.7f, 1.3f, 1.7f),
    };

    private static WildUnitSpec W(string id, int hp, int atk, float speed, float range = 0.7f, float cooldown = 1.1f) =>
        new WildUnitSpec { id = id, hp = hp, atk = atk, speed = speed, range = range, cooldown = cooldown };

    [MenuItem("Tools/Age of Stickman/Setup Wildlife")]
    public static void SetupWildlife()
    {
        var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Resources/GameDatabase.asset");
        if (db == null) { Debug.LogError("No GameDatabase."); return; }
        Age A(string name) => db.ages.Find(a => a != null && a.name == name);
        var stone = A("StoneAge");
        if (stone == null) { Debug.LogError("Wildlife: no Stone Age."); return; }
        Directory.CreateDirectory("Assets/Data/Units");
        Directory.CreateDirectory("Assets/Data/Buildings");
        Directory.CreateDirectory("Assets/Data/Technologies");

        var techs = SetupWildTechs(db, A);
        var buildings = SetupWildBuildings(db, A, techs);
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();

        SetupWildUnits(db, A);
        foreach (var def in buildings)
            if (WildBuildingArt.TryGetValue(def.id, out var art)) GenerateWildLooks(def, art.draw, art.frames, art.fps);
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        Debug.Log($"Wildlife set up: {Settlement.WildCatalogue.Length} creatures, {buildings.Count} buildings, {techs.Count} research.");
    }

    // ---------- Research ----------

    private static Dictionary<string, Technology> SetupWildTechs(GameDatabase db, System.Func<string, Age> A)
    {
        var r = new Dictionary<string, Technology>();
        ResourceType Res(string id) => db.GetResource(id);
        List<ResourceAmount> C(params (string res, int n)[] items)
        {
            var list = new List<ResourceAmount>();
            foreach (var (res, n) in items) { var t = Res(res); if (t != null) list.Add(new ResourceAmount { resource = t, amount = n }); }
            return list;
        }
        Technology T(string id, string name, string age, string desc, float time, List<ResourceAmount> cost, params string[] needs)
        {
            var a = A(age) ?? A("StoneAge");
            var t = Tech(db, id, name, a, desc, time, cost, null);
            t.requirements.minimumAge = a;
            t.requirements.technologies = new List<Technology>();
            foreach (var n in needs) if (r.TryGetValue(n, out var pre)) t.requirements.technologies.Add(pre);
            t.unitModifiers = new List<UnitStatModifier>();
            t.productionModifiers = new List<ProductionModifier>();
            t.maxLevel = 1;
            EditorUtility.SetDirty(t);
            r[id] = t;
            return t;
        }
        T("WildTracking", "Tracking", "StoneAge", "Reading tracks and droppings. Unlocks the Hunting Camp; hunting yields 10% more.", 35, C(("Food", 50), ("Wood", 40)));
        T("WildTaming", "Animal Taming", "StoneAge", "A wolf cub raised by the fire is a wolf on your side. Unlocks the Beast Pen, which tames war beasts from the herds nearby.", 60, C(("Food", 120), ("Wood", 80)), "WildTracking");
        T("WildBeastLore", "Beast Lore", "BronzeAge", "What each beast eats, fears and loves. Taming goes 20% faster.", 80, C(("Food", 250), ("Bronze", 40)), "WildTaming");
        var master = T("WildMasterHunters", "Master Huntsmen", "BronzeAge", "Each level: hunting yields 4% more (up to 5 levels).", 70, C(("Food", 200), ("Wood", 150)), "WildTracking");
        master.maxLevel = 5;
        master.extraLevelsPerAge = 0;
        master.levelCostGrowth = 1.6f;
        master.levelTimeGrowth = 1.25f;
        EditorUtility.SetDirty(master);
        T("WildGameLaws", "Game Laws", "MedievalAge", "Seasons for hunting and for leaving be. Herds regrow 25% faster, and Hunting Camps can rank up into Game Ranches.", 120, C(("Food", 800), ("Gold", 120)), "WildMasterHunters");
        T("WildWhispering", "Beast Whispering", "MedievalAge", "Griffins, wyverns and crystal beasts will answer to the right voice. Beast Pens can rank up into Royal Menageries; legendary beasts can be tamed as heroes' companions.", 140, C(("Food", 900), ("Gold", 200), ("Iron", 150)), "WildBeastLore");
        T("WildBreeding", "Selective Breeding", "EnlightenmentAge", "Keep the best, breed the best. Hunting yields 10% more.", 150, C(("Food", 1500), ("Coin", 300)), "WildGameLaws");
        T("WildConservation", "Conservation", "IndustrialAge", "Some land is better left wild. Herds regrow 25% faster, and Game Ranches can rank up into Game Reserves.", 170, C(("Food", 2500), ("Coin", 600), ("Steel", 200)), "WildBreeding");
        T("WildXenofauna", "Xenofauna Studies", "AstralAge", "The beasts of other worlds, catalogued and understood. Unlocks the Xenobiology Lab and the Xenofauna Pen, which tames alien beasts.", 200, C(("Food", 5000), ("Starmetal", 200), ("Microchips", 200)), "WildConservation");
        return r;
    }

    // ---------- Buildings ----------

    private static List<BuildingDefinition> SetupWildBuildings(GameDatabase db, System.Func<string, Age> A, Dictionary<string, Technology> t)
    {
        ResourceType Res(string id) => db.GetResource(id);
        List<ResourceAmount> C(params (string res, int n)[] items)
        {
            var list = new List<ResourceAmount>();
            foreach (var (res, n) in items) { var x = Res(res); if (x != null) list.Add(new ResourceAmount { resource = x, amount = n }); }
            return list;
        }
        BuildingDefinition B(string id, string name, string age, Vector2Int size, int tall, string desc, List<ResourceAmount> cost, float time, int hp, int workers, string tech)
        {
            var a = A(age) ?? A("StoneAge");
            var def = Building(db, id, name, a, null, size, new Vector2Int(size.x, size.y + tall), desc, cost, time, hp, workers, t[tech]);
            def.production = new List<ResourceRate>();
            def.inputs = new List<ResourceRate>();
            def.trainsUnit = null;
            def.maxUnits = 0;
            def.housing = 0;
            def.towerRange = 0;
            def.catchmentRadius = 3;
            def.waterBonus = 0;
            def.canBuildOnHills = false;
            def.rankUpTo = null;
            def.rankUpRequirements = new Requirements();
            return def;
        }
        BuildingUpgrade U(string name, string desc, List<ResourceAmount> cost, float output = 1f, UnitStatModifier mod = null, int health = 0) =>
            new BuildingUpgrade { name = name, description = desc, requirements = new Requirements { cost = cost }, upgradeTime = 25f, productionMultiplier = output, trainedUnits = mod ?? new UnitStatModifier(), extraHealth = health };
        void Rank(BuildingDefinition from, BuildingDefinition to, string tech, List<ResourceAmount> cost)
        {
            from.rankUpTo = to;
            from.rankUpRequirements = new Requirements { cost = cost, technologies = new List<Technology> { t[tech] } };
            from.rankUpTime = 40f;
        }

        var camp = B("WildHuntCamp", "Hunting Camp", "StoneAge", new Vector2Int(2, 2), 1,
            "Hunters' tents and drying racks. Its hunters bring in food from the herds within 18 tiles (deer, boar, mammoths, not predators): build it out in the wild, near the game. Hunting hard thins the herds.",
            C(("Wood", 70), ("Food", 20)), 20, 150, 2, "WildTracking");
        camp.upgrades = new List<BuildingUpgrade>
        {
            U("Skinning Racks", "+20% food.", C(("Wood", 60), ("Food", 40)), 1.2f),
            U("Wider Trails", "+10% food, and hunters range 2 tiles further.", C(("Wood", 90), ("Stone", 30)), 1.1f),
            U("Smokehouse", "+25% food, and 2 tiles further again.", C(("Wood", 120), ("Stone", 60)), 1.25f),
        };
        var ranch = B("WildRanch", "Game Ranch", "MedievalAge", new Vector2Int(3, 3), 1,
            "Fenced pastures where wild herds are rounded up and kept. Food (and, later, coin from hides) from the herds within 20 tiles, and half as much even with none; it thins the herds less than hunting does.",
            C(("Wood", 400), ("Stone", 200), ("Food", 200)), 45, 600, 4, "WildGameLaws");
        ranch.upgrades = new List<BuildingUpgrade>
        {
            U("Tannery", "+20% output.", C(("Wood", 250), ("Iron", 60)), 1.2f),
            U("Drovers", "+10% output, and 2 tiles further.", C(("Food", 300), ("Gold", 40)), 1.1f),
            U("Winter Barns", "+25% output, and 2 tiles further again.", C(("Wood", 400), ("Stone", 300)), 1.25f),
        };
        var reserve = B("WildReserve", "Game Reserve", "IndustrialAge", new Vector2Int(3, 3), 1,
            "Wardens, a lodge and a great deal of wild land. Food and coin from the herds within 22 tiles; it never hunts a herd below half, and the herds around it regrow twice as fast.",
            C(("Wood", 800), ("Stone", 600), ("Steel", 150), ("Coin", 200)), 60, 1500, 5, "WildConservation");
        reserve.upgrades = new List<BuildingUpgrade>
        {
            U("Game Wardens", "+20% output.", C(("Coin", 300), ("Steel", 100)), 1.2f),
            U("Rail Siding", "+10% output, and 2 tiles further.", C(("Steel", 200), ("Gears", 80)), 1.1f),
            U("Cold Store", "+25% output, and 2 tiles further again.", C(("Steel", 250), ("Coin", 400)), 1.25f),
        };
        var lab = B("WildXenoLab", "Xenobiology Lab", "AstralAge", new Vector2Int(3, 3), 2,
            "Domes of glass where alien beasts are studied and bred. Much food and coin from the herds within 24 tiles (a quarter more on other worlds); it barely thins them.",
            C(("Steel", 1500), ("Starmetal", 300), ("Microchips", 200), ("Coin", 800)), 90, 4000, 6, "WildXenofauna");
        lab.upgrades = new List<BuildingUpgrade>
        {
            U("Gene Bank", "+20% output.", C(("Microchips", 200), ("Coin", 600)), 1.2f),
            U("Drone Herders", "+10% output, and 2 tiles further.", C(("Starmetal", 200), ("Microchips", 150)), 1.1f),
            U("Cloning Vats", "+25% output, and 2 tiles further again.", C(("Starmetal", 300), ("Coin", 1000)), 1.25f),
        };
        Rank(camp, ranch, "WildGameLaws", C(("Wood", 300), ("Stone", 150), ("Food", 150)));
        Rank(ranch, reserve, "WildConservation", C(("Wood", 600), ("Steel", 100), ("Coin", 150)));
        Rank(reserve, lab, "WildXenofauna", C(("Steel", 1000), ("Starmetal", 200), ("Microchips", 150)));

        var pen = B("WildBeastPen", "Beast Pen", "StoneAge", new Vector2Int(3, 2), 1,
            "A stockade, a cage and some very brave tamers. They tame wolves and boar (bears and mammoths from the Bronze Age) from the herds within 18 tiles into war beasts that fight for you. Keeps 2 (+1 per upgrade); they eat a little food.",
            C(("Wood", 120), ("Food", 60), ("Stone", 20)), 30, 300, 2, "WildTaming");
        var strong = new UnitStatModifier { healthMultiplier = 1.2f, attackMultiplier = 1.1f };
        pen.upgrades = new List<BuildingUpgrade>
        {
            U("Bigger Pens", "Keeps 1 more beast; tames 25% faster.", C(("Wood", 150), ("Food", 80))),
            U("Strong Leashes", "Keeps 1 more; its beasts are 20% tougher and hit 10% harder.", C(("Wood", 200), ("Stone", 80)), 1f, strong),
            U("Patient Tamers", "Keeps 1 more; tames 25% faster again.", C(("Food", 250), ("Stone", 120))),
        };
        var menagerie = B("WildMenagerie", "Royal Menagerie", "MedievalAge", new Vector2Int(3, 3), 2,
            "Gilded cages and an aerie. Tames every beast of the home world within 20 tiles, griffins, wyverns and crystal beasts too, and can tame a legendary beast to be a hero's companion. Keeps 3 (+1 per upgrade).",
            C(("Wood", 600), ("Stone", 500), ("Gold", 150), ("Food", 300)), 50, 1200, 4, "WildWhispering");
        menagerie.upgrades = new List<BuildingUpgrade>
        {
            U("Aerie", "Keeps 1 more beast; tames 25% faster.", C(("Wood", 400), ("Gold", 80))),
            U("Beast Armour", "Keeps 1 more; its beasts are 25% tougher and hit 15% harder.", C(("Iron", 200), ("Gold", 100)), 1f, new UnitStatModifier { healthMultiplier = 1.25f, attackMultiplier = 1.15f }),
            U("Master Tamers", "Keeps 1 more; tames 25% faster again.", C(("Food", 800), ("Gold", 150))),
        };
        var xenoPen = B("WildXenoPen", "Xenofauna Pen", "AstralAge", new Vector2Int(3, 3), 2,
            "Force-fenced habitats for the beasts of other worlds. Tames any beast within 22 tiles, alien ones too, and legends as heroes' companions. Keeps 4 (+1 per upgrade).",
            C(("Steel", 1200), ("Starmetal", 250), ("Food", 1500)), 80, 3500, 5, "WildXenofauna");
        xenoPen.upgrades = new List<BuildingUpgrade>
        {
            U("Habitat Domes", "Keeps 1 more beast; tames 25% faster.", C(("Steel", 600), ("Starmetal", 100))),
            U("Neural Harness", "Keeps 1 more; its beasts are 30% tougher and hit 20% harder.", C(("Starmetal", 250), ("Microchips", 150)), 1f, new UnitStatModifier { healthMultiplier = 1.3f, attackMultiplier = 1.2f }),
            U("Xeno Handlers", "Keeps 1 more; tames 25% faster again.", C(("Food", 3000), ("Microchips", 200))),
        };
        Rank(pen, menagerie, "WildWhispering", C(("Wood", 400), ("Stone", 300), ("Gold", 100)));
        Rank(menagerie, xenoPen, "WildXenofauna", C(("Steel", 800), ("Starmetal", 150)));

        var all = new List<BuildingDefinition> { camp, ranch, reserve, lab, pen, menagerie, xenoPen };
        foreach (var b in all) EditorUtility.SetDirty(b);
        return all;
    }

    // ---------- Creatures ----------

    private static void SetupWildUnits(GameDatabase db, System.Func<string, Age> A)
    {
        string libPath = "Assets/Resources/WildlifeLibrary.asset";
        var lib = AssetDatabase.LoadAssetAtPath<WildlifeLibrary>(libPath);
        if (lib == null)
        {
            lib = ScriptableObject.CreateInstance<WildlifeLibrary>();
            AssetDatabase.CreateAsset(lib, libPath);
        }
        lib.entries = new List<WildlifeLibrary.Entry>();
        var melee = AssetDatabase.LoadAssetAtPath<TroopClass>("Assets/Data/TroopClasses/Melee.asset");
        var cavalry = AssetDatabase.LoadAssetAtPath<TroopClass>("Assets/Data/TroopClasses/Cavalry.asset") ?? melee;
        var wildClass = db.wolf != null && db.wolf.troopClass != null ? db.wolf.troopClass : melee;
        string[] names = { "Idle", "Walk0", "Walk1", "Walk2", "Walk3", "Attack0", "Attack1", "Attack2", "Attack3", "Death0", "Death1", "Death2" };

        foreach (var sp in Settlement.WildCatalogue)
        {
            var spec = System.Array.Find(WildUnitSpecs, s => s.id == sp.id);
            if (spec.id == null) { Debug.LogWarning($"Wildlife: no stats for {sp.id}."); continue; }
            var look = WildArt.LookOf(sp.id);
            var entry = new WildlifeLibrary.Entry { species = sp.id, flyer = sp.flyer };

            // The wild one.
            string wid = $"Wild_{sp.id}";
            var frames = WildArt.Render(look, false, out var pivot);
            var sprites = new List<Sprite>();
            ResetDir($"Units/{wid}");
            for (int f = 0; f < frames.Length; f++) sprites.Add(SavePixel(frames[f], $"Units/{wid}/{wid}_{names[f]}.png", UnitPPU, pivot));
            entry.frames = sprites;
            var wild = WildDef(wid, sp.name, A(sp.realm == 2 ? "AstralAge" : "StoneAge") ?? A("StoneAge"), wildClass, spec, 1f);
            wild.prefab = BuildWildPrefab(wid, sprites, frames[0], pivot, true, wild, sp.legendary);
            wild.icon = sprites[0];
            EditorUtility.SetDirty(wild);
            entry.wild = wild;

            // The tamed one (legends bond with heroes instead).
            if (sp.Tamable && !sp.legendary)
            {
                string tid = $"Tamed_{sp.id}";
                var age = db.GetAge(sp.tameAge) ?? A("StoneAge");
                UnitDefinition tame;
                if (sp.id == "Mammoth" || sp.id == "Griffin" || sp.id == "Wyvern")
                {
                    // Ridden: a rider in hides on the beast's back (the existing mount rigs).
                    string template = sp.id == "Mammoth" ? "WarElephant" : sp.id == "Griffin" ? "GriffinRider" : "WyvernRider";
                    int hp = Mathf.RoundToInt(spec.hp * 1.3f), atk = Mathf.RoundToInt(spec.atk * 1.15f);
                    tame = MedievalUnit(tid, sp.tameName, age, template, hp, atk, sp.id == "Mammoth" ? 1.6f : 1.3f, sp.id == "Mammoth" ? 1.1f : 5f, sp.id == "Mammoth" ? 0.85f : 2.2f);
                    tame.upkeep = new List<ResourceRate>();
                    tame.isNative = false;
                    var style = sp.id == "Mammoth"
                        ? Fig(B, GearLook.Hood, GearLook.Spear, body: GearLook.HideVest, mount: MountKind.Elephant, coat: "Fur")
                        : sp.id == "Griffin"
                            ? Fig(B, GearLook.CrestedHelm, GearLook.Javelin, body: GearLook.HideVest, mount: MountKind.Griffin, coat: "Griffin")
                            : Fig(B, GearLook.Hood, GearLook.Javelin, body: GearLook.Jerkin, mount: MountKind.Griffin, coat: "Wyvern");
                    tame.prefab = BuildPixelUnit(tid, style, false, tame);
                    tame.icon = AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/Units/{tid}/{tid}_Idle.png");
                }
                else
                {
                    var tf = WildArt.Render(look, true, out var tp);
                    var ts = new List<Sprite>();
                    ResetDir($"Units/{tid}");
                    for (int f = 0; f < tf.Length; f++) ts.Add(SavePixel(tf[f], $"Units/{tid}/{tid}_{names[f]}.png", UnitPPU, tp));
                    tame = WildDef(tid, sp.tameName, age, cavalry, spec, 1.3f);
                    tame.prefab = BuildWildPrefab(tid, ts, tf[0], tp, false, tame, false);
                    tame.icon = ts[0];
                }
                EditorUtility.SetDirty(tame);
                entry.tame = tame;
            }
            lib.entries.Add(entry);
        }
        EditorUtility.SetDirty(lib);
    }

    private static UnitDefinition WildDef(string id, string display, Age age, TroopClass cls, WildUnitSpec spec, float toughness)
    {
        string path = $"Assets/Data/Units/{id}.asset";
        var def = AssetDatabase.LoadAssetAtPath<UnitDefinition>(path);
        if (def == null)
        {
            def = ScriptableObject.CreateInstance<UnitDefinition>();
            AssetDatabase.CreateAsset(def, path);
        }
        def.name = id;
        def.displayName = display;
        def.age = age;
        def.troopClass = cls;
        def.maxHealth = Mathf.RoundToInt(spec.hp * toughness);
        def.attackPower = spec.atk;
        def.moveSpeed = spec.speed;
        def.attackRange = spec.range;
        def.attackCooldown = spec.cooldown;
        def.detectionRange = 4f;
        def.hitDelay = 0.3f;
        def.projectile = null;
        def.movement = Movement.Ground;   // flyers swoop low enough to be fought hand to hand
        def.isBoss = false;
        def.isNative = false;
        def.upkeep = new List<ResourceRate>();
        def.populationCost = 0;
        return def;
    }

    /// <summary>A unit prefab from ready-made frames: a WildBeast (wild) or a Troop (tamed).</summary>
    private static GameObject BuildWildPrefab(string name, List<Sprite> frames, PixelCanvas idle, Vector2 pivot, bool wild, UnitDefinition definition, bool legend)
    {
        float ppu = UnitPPU;
        int top = 0, left = idle.W, right = 0, gy = Mathf.RoundToInt(pivot.y * idle.H);
        for (int y = 0; y < idle.H; y++)
            for (int x = 0; x < idle.W; x++)
                if (idle.Get(x, y).a > 200) { top = Mathf.Max(top, y); left = Mathf.Min(left, x); right = Mathf.Max(right, x); }
        float height = (top - gy) / ppu, width = (right - left) / ppu;
        float cx = pivot.x * idle.W;

        var go = new GameObject($"{name}Unit") { tag = wild ? "enemy" : "troop" };
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = frames[0];
        var visual = go.AddComponent<SpriteUnitVisual>();
        visual.idle = frames[0];
        visual.walk = new List<Sprite> { frames[1], frames[2], frames[3], frames[4] };
        visual.attack = new List<Sprite> { frames[5], frames[6], frames[7], frames[8] };
        visual.death = new List<Sprite> { frames[9], frames[10], frames[11] };
        visual.impactFrame = 2;
        // A beast's figure (no gear): it snarls and growls rather than shouting.
        visual.figure = new FigureStyle { team = wild ? "Red" : "Blue", beast = Beast.Wolf, weapon = GearLook.None };
        visual.pixelsPerUnit = ppu;
        visual.weaponClass = WeaponClass.Melee;
        visual.barHeight = height + 3f / ppu;
        visual.barWidth = Mathf.Clamp(width * 0.8f, 12f / ppu, 48f / ppu);
        visual.alwaysShowBar = legend;
        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = Mathf.Max(width, height) * 0.32f;
        col.offset = new Vector2((left + right) * 0.5f / ppu - cx / ppu, height * 0.45f);
        go.AddComponent<MouseTouching>();
        Unit unit = wild ? go.AddComponent<WildBeast>() : (Unit)go.AddComponent<Troop>();
        unit.definition = definition;
        unit.personalSpace = Mathf.Clamp(width * 0.35f, 0.38f, 1.4f);
        string prefabPath = $"Assets/Prefabs/Units/{name}Unit.prefab";
        Directory.CreateDirectory("Assets/Prefabs/Units");
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        Object.DestroyImmediate(go);
        return prefab;
    }

    // ---------- Building art ----------

    private static readonly Dictionary<string, (PixelDrawer draw, int frames, float fps)> WildBuildingArt =
        new Dictionary<string, (PixelDrawer, int, float)>
        {
            { "WildHuntCamp", (PxWildHuntCamp, 4, 4f) }, { "WildRanch", (PxWildRanch, 4, 3f) },
            { "WildReserve", (PxWildReserve, 4, 3f) }, { "WildXenoLab", (PxWildXenoLab, 4, 4f) },
            { "WildBeastPen", (PxWildBeastPen, 4, 4f) }, { "WildMenagerie", (PxWildMenagerie, 4, 3f) },
            { "WildXenoPen", (PxWildXenoPen, 4, 4f) },
        };

    /// <summary>Like GenerateBuildingLooks, for this setup's own drawers (its own folders only).</summary>
    private static void GenerateWildLooks(BuildingDefinition def, PixelDrawer draw, int frameCount, float fps)
    {
        string dir = $"Buildings/{def.id}";
        ResetDir(dir);
        int artW = def.ArtSize.x * TilePx, artH = def.ArtSize.y * TilePx;
        int w = artW + MarginSide * 2, h = artH + MarginBottom + MarginTop;
        var pivot = new Vector2(MarginSide / (float)w, MarginBottom / (float)h);
        def.looks = new List<BuildingLook>();
        for (int level = 0; level <= def.upgrades.Count; level++)
        {
            var look = new BuildingLook { fps = fps };
            for (int f = 0; f < frameCount; f++)
            {
                var c = new PixelCanvas(w, h);
                var k = new K(c, MarginSide, MarginBottom, level, f, frameCount, def.id.GetHashCode());
                draw(k);
                look.frames.Add(SavePixel(c, $"{dir}/{def.id}_L{level}_F{f}.png", PixelsPerTile, pivot));
            }
            def.looks.Add(look);
        }
        def.icon = def.looks[0].frames[0];
        EditorUtility.SetDirty(def);
    }

    /// <summary>A stretched hide on a frame.</summary>
    private static void HideRack(K k, int x, int y, int w, int h)
    {
        k.VLine(x, y, y + h + 2, P.Wood[1]); k.VLine(x + w, y, y + h + 2, P.Wood[1]);
        k.HLine(x, x + w, y + h + 2, P.Wood[2]);
        k.Rect(x + 2, y + 3, x + w - 2, y + h, P.Leather[2]);
        k.Rect(x + 3, y + 4, x + w - 3, y + h - 1, P.Leather[3]);
        k.Line(x, y + h, x + 2, y + h - 1, P.Linen[0]); k.Line(x + w, y + h, x + w - 2, y + h - 1, P.Linen[0]);
    }

    /// <summary>Antlers mounted over a door or gate.</summary>
    private static void Antlers(K k, int cx, int y)
    {
        k.Line(cx - 1, y, cx - 5, y + 6, P.Bone[3]); k.Line(cx - 3, y + 3, cx - 7, y + 4, P.Bone[2]); k.Line(cx - 4, y + 5, cx - 4, y + 8, P.Bone[2]);
        k.Line(cx + 1, y, cx + 5, y + 6, P.Bone[3]); k.Line(cx + 3, y + 3, cx + 7, y + 4, P.Bone[2]); k.Line(cx + 4, y + 5, cx + 4, y + 8, P.Bone[2]);
        k.Rect(cx - 1, y - 1, cx + 1, y + 1, P.Bone[1]);
    }

    private static void PxWildHuntCamp(K k)
    {
        k.Ground(2, 2, 62, 34, P.Dirt);
        // A hide tent with a smoke hole, a drying rack of meat, a fire and the day's catch.
        k.Tent(24, 12, 18, 30, P.Leather);
        k.Rect(21, 12, 27, 22, P.Leather[0]);
        Antlers(k, 24, 36);
        HideRack(k, 44, 14, 14, 12);
        for (int i = 0; i < 3; i++) k.Rect(47 + i * 4, 20 + (i % 2), 48 + i * 4, 25, i % 2 == 0 ? P.Red[1] : P.Red[2]);
        k.Fire(8, 4, 4, 6);
        // A deer carcass on a pole.
        k.Line(36, 6, 50, 6, P.Wood[2], 2);
        k.Ellipse(43, 4, 5, 2.4f, P.Horse);
        if (k.Level >= 1) { k.SpearRack(4, 12, 3); }
        if (k.Level >= 2) k.Banner(58, 14, 18, P.Leather);
        if (k.Level >= 3) { k.Rect(30, 2, 38, 10, P.Wood[1]); k.Rect(31, 3, 37, 9, P.Wood[2]); k.Smoke(34, 12, 0.5f, 120); }
        k.Finish();
        k.Smoke(9, 12, 0.6f, 110);
    }

    private static void PxWildRanch(K k)
    {
        k.Ground(2, 2, 94, 90, P.Grass);
        // A barn at the back, paddock fences in front, hay and a trough.
        int top = Hall(k, 18, 78, 52, 22, K.Mat.Planks, K.RoofType.Shingle, 20, 14, windows: 2);
        Antlers(k, 48, top + 2);
        k.Fence(4, 92, 44, 6);
        k.Fence(4, 92, 6, 6);
        for (int y = 6; y <= 44; y += 6) { k.VLine(4, y, y + 6, P.Wood[1]); k.VLine(92, y, y + 6, P.Wood[1]); }
        k.Hay(10, 30); k.Hay(80, 26);
        k.Trough(40, 16, 18);
        // A couple of penned deer.
        k.Ellipse(28, 24, 5, 2.6f, P.Horse); k.Line(32, 25, 34, 30, P.Horse[2], 2); k.Ellipse(35, 31, 1.8f, 1.5f, P.Horse);
        k.Ellipse(64, 18, 5, 2.6f, P.Horse); k.Line(68, 19, 70, 24, P.Horse[2], 2); k.Ellipse(71, 25, 1.8f, 1.5f, P.Horse);
        if (k.Level >= 1) HideRack(k, 6, 52, 10, 10);
        if (k.Level >= 2) k.Cart(70, 48, P.Straw);
        if (k.Level >= 3) k.Banner(88, 52, 22, P.Straw);
        k.Finish();
    }

    private static void PxWildReserve(K k)
    {
        k.Ground(2, 2, 94, 90, P.Grass);
        // Trees, a stone-and-timber lodge, a watchtower and a wardens' fence.
        for (int i = 0; i < 4; i++)
        {
            int x = 8 + i * 26, y = 64 + (i % 2) * 6;
            k.VLine(x, y - 8, y, P.Wood[1]);
            k.Ellipse(x, y + 4, 8, 9, P.Pine);
        }
        int top = Hall(k, 30, 74, 30, 18, K.Mat.Stone, K.RoofType.Slate, 16, 10, windows: 2);
        Antlers(k, 52, top + 2);
        k.VLine(12, 6, 46, P.Wood[1]); k.VLine(20, 6, 46, P.Wood[1]);
        k.Rect(9, 46, 23, 52, P.Wood[2]); k.Roof(8, 24, 52, 8, K.RoofType.Shingle, 8);
        k.Fence(26, 92, 6, 5);
        k.Ellipse(84, 20, 5, 2.6f, P.Horse); k.Line(88, 21, 90, 27, P.Horse[2], 2); k.Ellipse(91, 28, 1.8f, 1.5f, P.Horse);
        if (k.Level >= 1) k.Banner(78, 30, 22, P.Green);
        if (k.Level >= 2) k.Rails(26, 92, 2);
        if (k.Level >= 3) k.Chimney(64, top + 2, 4, 8);
        k.Finish();
    }

    private static void PxWildXenoLab(K k)
    {
        k.Ground(2, 2, 94, 92, P.Stone);
        // Glass domes over alien greenery, a lab block and a glowing specimen tank.
        var glass = P.Phase;
        k.Ellipse(28, 30, 22, 22, glass, v => v.y >= 30 + 4);
        k.Ellipse(28, 34, 16, 12, P.Seed, v => v.y >= 34 + 4);
        k.Rect(6, 26, 50, 34, P.Chrome[1]);
        k.Ellipse(72, 54, 18, 16, glass, v => v.y >= 58);
        k.Rect(56, 52, 88, 60, P.Chrome[1]);
        k.Rect(56, 12, 90, 40, P.Chrome[2]); k.Rect(58, 14, 88, 38, P.Chrome[1]);
        k.Window(62, 24, 6, 6); k.Window(76, 24, 6, 6);
        // Specimen tank.
        k.Rect(40, 6, 50, 24, P.Chrome[0]);
        int glow = k.Frame % 2 == 0 ? 2 : 3;
        k.Rect(41, 8, 49, 22, P.Neon[glow - 1]); k.Ellipse(45, 15, 3, 4, P.Ray);
        if (k.Level >= 1) k.Banner(90, 42, 20, P.Neon);
        if (k.Level >= 2) k.Rect(10, 6, 30, 12, P.Steel[1]);
        if (k.Level >= 3) { k.VLine(84, 40, 70, P.Chrome[2]); k.Px(84, 71, P.Neon[3]); }
        k.Finish();
    }

    private static void PxWildBeastPen(K k)
    {
        k.Ground(2, 2, 94, 58, P.Dirt);
        // A palisade stockade, a timber cage and a tamer's hut, with a chained wolf.
        Palisade(k, 4, 92, 32, 18);
        int top = Hall(k, 8, 40, 14, 14, K.Mat.Logs, K.RoofType.Hide, 12, 8, windows: 1);
        Antlers(k, 24, top + 1);
        // Cage.
        k.Rect(52, 10, 84, 30, P.Wood[0]);
        for (int x = 52; x <= 84; x += 4) k.VLine(x, 10, 30, P.Wood[2]);
        k.HLine(52, 84, 30, P.Wood[3]); k.HLine(52, 84, 10, P.Wood[2]);
        k.Ellipse(68, 16, 6, 3, P.Wolf); k.Ellipse(74, 19, 2.6f, 2.2f, P.Wolf);
        // A chain and post.
        k.VLine(46, 2, 12, P.Wood[1]);
        k.Line(46, 8, 54, 4, P.Iron[2]);
        k.Trough(12, 2, 14);
        if (k.Level >= 1) k.Banner(88, 32, 18, P.Blue);
        if (k.Level >= 2) k.SpearRack(30, 2, 3);
        if (k.Level >= 3) { k.Rect(86, 4, 92, 10, P.Bone[2]); k.Px(88, 11, P.Bone[3]); }
        k.Finish();
    }

    private static void PxWildMenagerie(K k)
    {
        k.Ground(2, 2, 94, 92, P.Stone);
        // A stone hall with a gilded aerie dome, cages either side, banners.
        int top = Hall(k, 24, 72, 40, 22, K.Mat.Stone, K.RoofType.Slate, 14, 10, windows: 2, arched: true);
        // Aerie: a gilded cage dome on top.
        k.Ellipse(48, top + 14, 14, 14, P.Gold, v => v.y >= top + 14);
        for (int x = 36; x <= 60; x += 4) k.VLine(x, top + 14, top + 14 + (int)Mathf.Sqrt(Mathf.Max(0, 196 - (x - 48) * (x - 48))), P.Gold[1]);
        k.Ellipse(48, top + 18, 4, 3, P.Griffin);
        // Cages.
        for (int c = 0; c < 2; c++)
        {
            int x0 = c == 0 ? 4 : 76, x1 = x0 + 16;
            k.Rect(x0, 8, x1, 34, P.Stone[1]);
            k.Rect(x0 + 1, 9, x1 - 1, 32, P.Coal[1]);
            for (int x = x0; x <= x1; x += 3) k.VLine(x, 9, 32, P.Iron[2]);
            k.HLine(x0, x1, 34, P.Gold[2]);
        }
        k.Ellipse(12, 14, 5, 3, P.Mana);
        k.Ellipse(84, 14, 5, 3, P.Wyvern);
        k.Banner(26, 40, 20, P.Purple); k.Banner(70, 40, 20, P.Purple);
        if (k.Level >= 1) k.Torch(22, 20);
        if (k.Level >= 2) k.Torch(74, 20);
        if (k.Level >= 3) k.ShieldDecor(48, 30, P.Gold);
        k.Finish();
    }

    private static void PxWildXenoPen(K k)
    {
        k.Ground(2, 2, 94, 92, P.Stone);
        // Habitats behind shimmering force fences, a control post.
        var field = P.Neon;
        int pulse = k.Frame % 2;
        for (int c = 0; c < 2; c++)
        {
            int x0 = c == 0 ? 6 : 52, x1 = x0 + 38, y0 = 40, y1 = 84;
            k.Rect(x0, y0, x1, y0 + 2, P.Chrome[1]);
            k.Rect(x0 + 2, y0 + 3, x1 - 2, y1 - 20, c == 0 ? P.Seed[1] : P.Ashskin[1]);
            for (int x = x0; x <= x1; x += 6) k.VLine(x, y0, y1 - 18, P.Chrome[2]);
            for (int y = y0 + 4 + pulse * 2; y < y1 - 18; y += 4) k.HLine(x0 + 1, x1 - 1, y, new Color32(field[2].r, field[2].g, field[2].b, 140));
        }
        k.Ellipse(24, 50, 6, 3, P.Reefskin);
        k.Ellipse(70, 50, 6, 3, P.Ashskin);
        k.Rect(30, 8, 66, 30, P.Chrome[2]); k.Rect(32, 10, 64, 28, P.Chrome[1]);
        k.Window(38, 16, 6, 6); k.Window(52, 16, 6, 6);
        k.Rect(44, 30, 52, 36, P.Chrome[0]); k.Px(48, 37, field[3 - pulse]);
        if (k.Level >= 1) k.Banner(88, 10, 22, P.Neon);
        if (k.Level >= 2) k.Rect(6, 6, 24, 14, P.Steel[1]);
        if (k.Level >= 3) k.Rect(72, 6, 88, 14, P.Steel[1]);
        k.Finish();
    }
}
