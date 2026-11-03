using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Culture and faith buildings, one or two per age band (Settlement.CultureBuildingSpecs): each
/// line ranks up into the next age's building, with two upgrades in between. Creates or updates
/// only these buildings and draws only their art. Safe to run again.
/// (Tools > Age of Stickman > Setup Culture.)
/// </summary>
public static partial class PlaceholderArt
{
    [MenuItem("Tools/Age of Stickman/Setup Culture")]
    public static void SetupCulture()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Setup Culture: stop play mode first."); return; }
        var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Resources/GameDatabase.asset");
        if (db == null) { Debug.LogError("Setup Culture: no GameDatabase."); return; }
        System.IO.Directory.CreateDirectory($"{DataRoot}/Buildings");

        var made = new List<BuildingDefinition>();
        var lines = new Dictionary<bool, List<BuildingDefinition>> { { false, new List<BuildingDefinition>() }, { true, new List<BuildingDefinition>() } };
        foreach (var spec in Settlement.CultureBuildingSpecs)
        {
            var age = db.ages.Find(a => a != null && a.name == spec.age);
            if (age == null) { Debug.LogWarning($"Setup Culture: no {spec.age}, skipping the {spec.name}."); continue; }
            int a0 = age.order;
            var cost = CulCost(db, a0, spec.faith, 1f);
            var def = Building(db, spec.id, spec.name, age, null, new Vector2Int(spec.size, spec.size),
                new Vector2Int(spec.size, spec.size + spec.tall), spec.desc, cost, 30f + 10f * a0, 300 + 200 * a0, spec.workers, null);
            def.housing = 0;
            def.production = new List<ResourceRate>();
            def.inputs = new List<ResourceRate>();
            def.trainsUnit = null;
            def.maxUnits = 0;
            def.towerRange = 0;
            def.defensePower = 0;
            def.wonder = false;
            def.wonderEffect = null;
            def.maxCount = 0;
            def.canBuildOnHills = false;
            def.waterBonus = 0;
            def.isTownCenter = false;
            def.upgrades = new List<BuildingUpgrade>
            {
                CulUpgrade(spec.faith ? "Lay Brothers" : "Patrons", spec.faith ? "More hands for the rites: +35% faith, +1 mood." : "Wealthy patrons pay for more: +35% culture, +1 mood.",
                    CulCost(db, a0, spec.faith, 0.4f), 1.35f),
                CulUpgrade(spec.faith ? "Holy Relics" : "Grand Season", spec.faith ? "Pilgrims come to see the relics: +35% more faith, +1 mood." : "The best of the age on its stage: +35% more culture, +1 mood.",
                    CulCost(db, a0, spec.faith, 0.6f), 1.35f),
            };
            def.rankUpTo = null;
            def.rankUpRequirements = new Requirements();
            EditorUtility.SetDirty(def);
            made.Add(def);
            lines[spec.faith].Add(def);
        }
        // Each line ranks up into the next age's building (ages flow: no jumps).
        foreach (var line in lines.Values)
        {
            line.Sort((x, y) => x.age.order.CompareTo(y.age.order));
            for (int i = 0; i + 1 < line.Count; i++)
            {
                var from = line[i];
                var to = line[i + 1];
                from.rankUpTo = to;
                from.rankUpRequirements = new Requirements { minimumAge = to.age, cost = CulCost(db, to.age.order, Settlement.CultureSpecOf(to.id, out var s) && s.faith, 0.6f) };
                from.rankUpTime = 40f;
                EditorUtility.SetDirty(from);
            }
        }
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();

        foreach (var def in made) GenerateCultureLooks(def);
        AssetDatabase.SaveAssets();
        Debug.Log($"Culture set up: {made.Count} culture and faith buildings.");
    }

    private static BuildingUpgrade CulUpgrade(string name, string description, List<ResourceAmount> cost, float output)
    {
        var u = Upgrade(name, description, cost, output);
        u.upgradeTime = 25f;
        return u;
    }

    /// <summary>
    /// What a culture or faith building of an age costs (times 'share'): food, wood and stone, plus
    /// the two newest goods of its age (tools, gold, steel, gears, power cells...).
    /// </summary>
    private static List<ResourceAmount> CulCost(GameDatabase db, int age, bool faith, float share)
    {
        float s = Mathf.Pow(1f + age, 1.25f) * share;
        var parts = new List<(string id, float amount)>
        {
            ("Food", 60 * s), ("Wood", (faith ? 60 : 90) * s), ("Stone", (faith ? 110 : 70) * s),
        };
        var extras = new List<(string id, float amount)>();
        if (age >= 2) extras.Add(("Tools", 8 * s));
        if (age >= 3) extras.Add((faith ? "Gold" : "Coin", 25 * s));
        if (age >= 4) extras.Add(("Steel", 12 * s));
        if (age >= 7) extras.Add(("Gears", 10 * s));
        if (age >= 9) extras.Add(("PowerCells", 8 * s));
        if (age >= 10) extras.Add(("Microchips", 6 * s));
        if (age >= 11) extras.Add(("Mana", 6 * s));
        if (age >= 12) extras.Add(("Starmetal", 5 * s));
        if (age >= 13) extras.Add(("Sunfire", 4 * s));
        if (age >= 15) extras.Add(("Quintessence", 3 * s));
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
}
