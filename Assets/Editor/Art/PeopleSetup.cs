using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using K = BuildingKit;

/// <summary>
/// People: the consumer goods (Pottery, Cloth, Ale, Books, Coffee, Electronics, Holo-media) and
/// their workshops, the line of schools (House of Tablets to Omniversity, each ranking up into the
/// next), and the decorations comfortable homes put out. Creates or updates only these and draws
/// only their art (icons in Icons/People, decorations in People). Safe to run again.
/// (Tools > Age of Stickman > Setup People.)
/// </summary>
public static partial class PlaceholderArt
{
    [MenuItem("Tools/Age of Stickman/Setup People")]
    public static void SetupPeople()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Setup People: stop play mode first."); return; }
        var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Resources/GameDatabase.asset");
        if (db == null) { Debug.LogError("Setup People: no GameDatabase."); return; }
        System.IO.Directory.CreateDirectory($"{DataRoot}/Buildings");
        System.IO.Directory.CreateDirectory($"{DataRoot}/Resources");

        // The new goods, each after the resources of its age on the bar.
        var icons = new Dictionary<string, System.Action<PixelCanvas>>
        {
            { "Pottery", IcPplPottery }, { "Cloth", IcPplCloth }, { "Ale", IcPplAle }, { "Books", IcPplBooks },
            { "Coffee", IcPplCoffee }, { "Electronics", IcPplElectronics }, { "HoloMedia", IcPplHolo },
        };
        var after = new Dictionary<string, string>
        {
            { "Pottery", "Charcoal" }, { "Cloth", "Pottery" }, { "Ale", "Tools" }, { "Books", "Runestone" },
            { "Coffee", "Quicksilver" }, { "Electronics", "PowerCells" }, { "HoloMedia", "Sunfire" },
        };
        foreach (var spec in PeopleCatalog.Goods)
        {
            if (spec.shared || !icons.ContainsKey(spec.id)) continue;
            var age = db.GetAge(spec.age);
            PplResource(db, spec.id, spec.name, age, after[spec.id], icons[spec.id]);
        }

        var made = new List<BuildingDefinition>();

        // Workshops.
        foreach (var w in PeopleCatalog.Workshops)
        {
            var age = db.ages.Find(a => a != null && a.name == w.age);
            var good = db.GetResource(w.good);
            if (age == null || good == null) { Debug.LogWarning($"Setup People: skipping the {w.name} (no {w.age} or {w.good})."); continue; }
            var tech = string.IsNullOrEmpty(w.tech) ? null : db.technologies.Find(t => t != null && t.id == w.tech);
            var def = Building(db, w.id, w.name, age, null, new Vector2Int(w.w, w.h), new Vector2Int(w.w, w.h + w.tall), w.desc,
                PplCost(db, age.order, 0.8f), 20f + 6f * age.order, 220 + 160 * age.order, w.workers, tech);
            PplClear(def);
            def.production = new List<ResourceRate> { Rate(good, w.rate) };
            def.inputs = new List<ResourceRate>();
            foreach (var (id, rate) in w.inputs)
            {
                var r = db.GetResource(id);
                if (r != null) def.inputs.Add(Rate(r, rate));
            }
            def.runsWithoutInputs = def.inputs.Count > 0 ? 0.25f : 0f;
            def.upgrades = new List<BuildingUpgrade>
            {
                PplUpgrade("More Hands", "Another workbench and an apprentice: +25% output.", PplCost(db, age.order, 0.4f), 1.25f),
                PplUpgrade("Master Craft", "Better tools and a master's eye: +30% more output.", PplCost(db, age.order, 0.6f), 1.3f),
            };
            EditorUtility.SetDirty(def);
            made.Add(def);
        }

        // Schools: a line ranking up age by age.
        var schools = new List<BuildingDefinition>();
        foreach (var s in PeopleCatalog.Schools)
        {
            var age = db.ages.Find(a => a != null && a.name == s.age);
            if (age == null) { Debug.LogWarning($"Setup People: no {s.age}, skipping the {s.name}."); continue; }
            var tech = string.IsNullOrEmpty(s.tech) ? null : db.technologies.Find(t => t != null && t.id == s.tech);
            var def = Building(db, s.id, s.name, age, null, new Vector2Int(s.size, s.size), new Vector2Int(s.size, s.size + s.tall), s.desc,
                PplCost(db, age.order, 1f), 30f + 8f * age.order, 300 + 200 * age.order, s.workers, tech);
            PplClear(def);
            def.production = new List<ResourceRate>();
            def.inputs = new List<ResourceRate>();
            def.upgrades = new List<BuildingUpgrade>
            {
                PplUpgrade("More Teachers", "More teachers and a bigger hall: +35% pupils.", PplCost(db, age.order, 0.45f), 1.35f),
                PplUpgrade("Great Library", "A library worth the journey: +35% more pupils.", PplCost(db, age.order, 0.65f), 1.35f),
            };
            EditorUtility.SetDirty(def);
            made.Add(def);
            schools.Add(def);
        }
        schools.Sort((x, y) => x.age.order.CompareTo(y.age.order));
        for (int i = 0; i + 1 < schools.Count; i++)
        {
            var from = schools[i];
            var to = schools[i + 1];
            from.rankUpTo = to;
            from.rankUpRequirements = new Requirements { minimumAge = to.age, cost = PplCost(db, to.age.order, 0.6f) };
            from.rankUpTime = 40f;
            EditorUtility.SetDirty(from);
        }
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();

        foreach (var def in made) GeneratePeopleLooks(def);
        PplDecorArt();
        AssetDatabase.SaveAssets();
        Debug.Log($"People set up: {made.Count} workshops and schools, the new goods and the comfort decorations.");
    }

    private static void PplClear(BuildingDefinition def)
    {
        def.housing = 0;
        def.trainsUnit = null;
        def.maxUnits = 0;
        def.towerRange = 0;
        def.towerDamage = 0;
        def.defensePower = 0;
        def.wonder = false;
        def.wonderEffect = null;
        def.maxCount = 0;
        def.canBuildOnHills = false;
        def.waterBonus = 0;
        def.isTownCenter = false;
        def.rankUpTo = null;
        def.rankUpRequirements = new Requirements();
    }

    private static BuildingUpgrade PplUpgrade(string name, string description, List<ResourceAmount> cost, float output)
    {
        var u = Upgrade(name, description, cost, output);
        u.upgradeTime = 25f;
        return u;
    }

    /// <summary>What a people building of an age costs (times 'share'): wood, stone and food, plus the newest goods of its age.</summary>
    private static List<ResourceAmount> PplCost(GameDatabase db, int age, float share)
    {
        float s = Mathf.Pow(1f + age, 1.25f) * share;
        var parts = new List<(string id, float amount)> { ("Wood", 80 * s), ("Stone", 70 * s), ("Food", 40 * s) };
        var extras = new List<(string id, float amount)>();
        if (age >= 2) extras.Add(("Tools", 8 * s));
        if (age >= 3) extras.Add(("Coin", 20 * s));
        if (age >= 4) extras.Add(("Steel", 10 * s));
        if (age >= 7) extras.Add(("Gears", 8 * s));
        if (age >= 9) extras.Add(("PowerCells", 6 * s));
        if (age >= 10) extras.Add(("Microchips", 5 * s));
        if (age >= 11) extras.Add(("Mana", 5 * s));
        if (age >= 12) extras.Add(("Starmetal", 4 * s));
        if (age >= 13) extras.Add(("Sunfire", 3 * s));
        if (age >= 15) extras.Add(("Quintessence", 2 * s));
        for (int i = Mathf.Max(0, extras.Count - 2); i < extras.Count; i++) parts.Add(extras[i]);
        var list = new List<ResourceAmount>();
        foreach (var (id, amount) in parts)
        {
            var r = db.GetResource(id);
            int n = Mathf.RoundToInt(amount / 5f) * 5;
            if (r != null && n > 0) list.Add(new ResourceAmount { resource = r, amount = n });
        }
        return list;
    }

    private static ResourceType PplResource(GameDatabase db, string id, string display, Age unlockedIn, string after, System.Action<PixelCanvas> icon)
    {
        string path = $"{DataRoot}/Resources/{id}.asset";
        var res = AssetDatabase.LoadAssetAtPath<ResourceType>(path);
        if (res == null)
        {
            res = ScriptableObject.CreateInstance<ResourceType>();
            AssetDatabase.CreateAsset(res, path);
        }
        res.id = id;
        res.displayName = display;
        res.unlockedIn = unlockedIn;
        // (Icons/People: "Generate Icon Art" clears only the top Icons folder, so these survive it.)
        res.icon = IconPixel($"Icons/People/{id}.png", icon);
        EditorUtility.SetDirty(res);
        if (!db.resources.Contains(res))
        {
            int i = db.resources.FindIndex(r => r != null && r.id == after);
            db.resources.Insert(i < 0 ? db.resources.Count : i + 1, res);
        }
        return res;
    }

    /// <summary>The comfort decorations: four styles by age, three levels each, saved on the PeopleArt asset.</summary>
    private static void PplDecorArt()
    {
        const string assetPath = "Assets/Resources/PeopleArt.asset";
        var art = AssetDatabase.LoadAssetAtPath<PeopleArt>(assetPath);
        if (art == null)
        {
            art = ScriptableObject.CreateInstance<PeopleArt>();
            AssetDatabase.CreateAsset(art, assetPath);
        }
        ResetDir("People");
        art.decor = new List<Sprite>();
        var pivot = new Vector2(0f, PplDecorBottom / (float)PplDecorH);
        for (int style = 0; style < PeopleArt.Styles; style++)
            for (int level = 1; level <= 3; level++)
            {
                var c = new PixelCanvas(PplDecorW, PplDecorH);
                var k = new K(c, 0, PplDecorBottom, level, 0, 1, style * 7 + level);
                PplDecor(k, style, level);
                k.Finish();
                art.decor.Add(SavePixel(c, $"People/Decor_S{style}_L{level}.png", PixelsPerTile, pivot));
            }
        EditorUtility.SetDirty(art);
    }
}
