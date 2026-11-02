using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates the production-chain content: Charcoal, Tools and Coin; the Charcoal Kiln, Smithy
/// and Mint; the research that unlocks them; fuel for the foundry and bloomery; Tools in Iron and
/// Classical upgrade costs; and troop upkeep. Safe to run again: it only fills in what's missing.
/// </summary>
public static partial class PlaceholderArt
{
    private const string DataRoot = "Assets/Data";

    [MenuItem("Tools/Age of Stickman/Setup Economy Data")]
    public static void SetupEconomy()
    {
        var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Resources/GameDatabase.asset");
        Age stone = db.ages[0], bronze = db.ages[1], iron = db.ages[2], classical = db.ages[3];

        // Resources
        var charcoal = Resource(db, "Charcoal", "Charcoal", bronze, "Bronze", DrawCharcoalIcon);
        var tools = Resource(db, "Tools", "Tools", iron, "Iron", DrawToolsIcon);
        var coin = Resource(db, "Coin", "Coin", classical, "Gold", DrawCoinIcon);
        db.coin = coin;
        ResourceType wood = Res(db, "Wood"), stoneRes = Res(db, "Stone"), food = Res(db, "Food"),
            bronzeRes = Res(db, "Bronze"), ironRes = Res(db, "Iron"), gold = Res(db, "Gold");

        // Research
        var charcoalBurning = Tech(db, "CharcoalBurning", "Charcoal Burning", bronze,
            "Smoulder wood into charcoal: +5% output from all buildings. Unlocks the Charcoal Kiln, which fuels foundries and bloomeries.",
            40f, Cost(wood, 100, food, 60), "BronzeWeapons");
        AddProductionBonus(charcoalBurning, 1.05f);
        var smithing = Tech(db, "Smithing", "Smithing", iron,
            "Forge iron into tools: +5% building health. Unlocks the Smithy, which makes the Tools later upgrades need.",
            45f, Cost(ironRes, 40, wood, 80), "IronTools");
        smithing.buildingHealthMultiplier = 1.05f;
        var taxation = Tech(db, "Taxation", "Taxation", classical,
            "A census and proper tax collectors: +75% Coin from taxes.",
            40f, Cost(gold, 30, food, 150), "Coinage");
        taxation.coinPerPersonPerSecond = 0.003f;
        EditorUtility.SetDirty(taxation);
        var coinage = db.technologies.Find(x => x != null && x.id == "Coinage");
        taxation.requirements.technologies = new List<Technology> { coinage };
        var ironWorking = db.technologies.Find(x => x != null && x.id == "IronWorking");
        if (ironWorking != null && !ironWorking.requirements.technologies.Contains(charcoalBurning))
        {
            // Bloomeries burn charcoal: learn it before iron.
            ironWorking.requirements.technologies.Add(charcoalBurning);
            EditorUtility.SetDirty(ironWorking);
        }

        // Buildings
        var kiln = Building(db, "CharcoalKiln", "Charcoal Kiln", bronze, "BronzeFoundry", new Vector2Int(2, 2), new Vector2Int(2, 2),
            "Burns wood slowly under turf into charcoal, the fuel for smelting bronze and iron.",
            Cost(wood, 60, stoneRes, 40), 20f, 160, 2, charcoalBurning);
        kiln.production = new List<ResourceRate> { Rate(charcoal, 0.25f) };
        kiln.inputs = new List<ResourceRate> { Rate(wood, 0.4f) };
        kiln.upgrades = new List<BuildingUpgrade>
        {
            Upgrade("Turf Clamp", "A sealed turf cover wastes less wood.", Cost(wood, 60, stoneRes, 40), 1.25f),
            Upgrade("Brick Beehive", "A permanent brick kiln that burns hotter and cleaner.", Cost(stoneRes, 80, bronzeRes, 20), 1.3f),
        };

        var smithy = Building(db, "Smithy", "Smithy", iron, "IronMine", new Vector2Int(3, 2), new Vector2Int(3, 3),
            "Forges iron and charcoal into tools. Iron Age and later upgrades need Tools.",
            Cost(wood, 120, stoneRes, 100, ironRes, 30), 30f, 260, 3, smithing);
        smithy.production = new List<ResourceRate> { Rate(tools, 0.12f) };
        smithy.inputs = new List<ResourceRate> { Rate(ironRes, 0.08f), Rate(charcoal, 0.1f) };
        smithy.upgrades = new List<BuildingUpgrade>
        {
            Upgrade("Bellows", "Leather bellows keep the forge white-hot.", Cost(wood, 80, ironRes, 30), 1.25f),
            Upgrade("Second Forge", "Another hearth, a quench trough and an apprentice.", Cost(stoneRes, 120, ironRes, 40), 1.3f),
        };

        var mint = Building(db, "Mint", "Mint", classical, "GoldMine", new Vector2Int(2, 2), new Vector2Int(2, 3),
            "Strikes gold into coin. Every person pays a little Coin in taxes; the mint adds more.",
            Cost(stoneRes, 180, wood, 80, gold, 40), 30f, 300, 3, coinage);
        mint.production = new List<ResourceRate> { Rate(coin, 0.6f) };
        mint.inputs = new List<ResourceRate> { Rate(gold, 0.05f) };
        mint.upgrades = new List<BuildingUpgrade>
        {
            Upgrade("Die Engravers", "Sharper dies strike more coins per blank.", Cost(stoneRes, 120, gold, 30), 1.25f),
            Upgrade("Assay Office", "Weighed and tested gold: nothing wasted.", Cost(stoneRes, 160, gold, 40), 1.3f),
        };

        // Fuel for the metal works: they still run at half speed without it.
        foreach (var (id, rate) in new[] { ("BronzeFoundry", 0.1f), ("IronMine", 0.12f) })
        {
            var def = db.buildings.Find(b => b != null && b.id == id);
            if (def == null) continue;
            def.inputs = new List<ResourceRate> { Rate(charcoal, rate) };
            def.runsWithoutInputs = 0.5f;
            EditorUtility.SetDirty(def);
        }

        // Tools in Iron and Classical upgrade and rank-up costs.
        foreach (var def in db.buildings)
        {
            if (def == null || def.IsWall) continue; // walls stay cheap per piece
            bool late = def.age != null && def.age.order >= iron.order;
            if (late)
            {
                foreach (var u in def.upgrades) AddTools(u.requirements, tools);
            }
            if (def.rankUpTo != null && def.rankUpTo.age != null && def.rankUpTo.age.order >= iron.order)
            {
                AddTools(def.rankUpRequirements, tools);
            }
            EditorUtility.SetDirty(def);
        }

        // Upkeep: every troop eats; Classical troops are paid in Coin as well.
        foreach (var def in db.buildings)
        {
            var unit = def != null ? def.trainsUnit : null;
            if (unit == null) continue;
            unit.upkeep = new List<ResourceRate> { Rate(food, 0.01f) };
            if (unit.age != null && unit.age.order >= classical.order)
            {
                unit.upkeep.Add(Rate(coin, 0.03f));
            }
            EditorUtility.SetDirty(unit);
        }

        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        foreach (var def in new[] { kiln, smithy, mint })
        {
            GenerateBuildingLooks(def);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Economy data set up: Charcoal, Tools, Coin, Charcoal Kiln, Smithy, Mint, upkeep.");
    }

    // ---------- Helpers ----------

    private static ResourceType Res(GameDatabase db, string id) => db.resources.Find(r => r != null && r.id == id);

    private static ResourceType Resource(GameDatabase db, string id, string display, Age unlockedIn, string after, System.Action<Painter> icon)
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
        res.icon = Icon($"Icons/{id}.png", icon);
        EditorUtility.SetDirty(res);
        if (!db.resources.Contains(res))
        {
            int i = db.resources.FindIndex(r => r != null && r.id == after);
            db.resources.Insert(i < 0 ? db.resources.Count : i + 1, res);
        }
        return res;
    }

    private static Technology Tech(GameDatabase db, string id, string display, Age age, string description,
        float time, List<ResourceAmount> cost, string after)
    {
        string path = $"{DataRoot}/Technologies/{id}.asset";
        var tech = AssetDatabase.LoadAssetAtPath<Technology>(path);
        if (tech == null)
        {
            tech = ScriptableObject.CreateInstance<Technology>();
            AssetDatabase.CreateAsset(tech, path);
        }
        tech.id = id;
        tech.displayName = display;
        tech.age = age;
        tech.description = description;
        tech.researchTime = time;
        tech.requirements.cost = cost;
        EditorUtility.SetDirty(tech);
        if (!db.technologies.Contains(tech))
        {
            int i = db.technologies.FindIndex(t => t != null && t.id == after);
            db.technologies.Insert(i < 0 ? db.technologies.Count : i + 1, tech);
        }
        return tech;
    }

    private static void AddProductionBonus(Technology tech, float multiplier)
    {
        tech.productionModifiers = new List<ProductionModifier> { new ProductionModifier { resource = null, multiplier = multiplier } };
    }

    private static BuildingDefinition Building(GameDatabase db, string id, string display, Age age, string after,
        Vector2Int size, Vector2Int artSize, string description, List<ResourceAmount> cost, float buildTime,
        int health, int workers, Technology needs)
    {
        string path = $"{DataRoot}/Buildings/{id}.asset";
        var def = AssetDatabase.LoadAssetAtPath<BuildingDefinition>(path);
        if (def == null)
        {
            def = ScriptableObject.CreateInstance<BuildingDefinition>();
            AssetDatabase.CreateAsset(def, path);
        }
        def.id = id;
        def.displayName = display;
        def.age = age;
        def.size = size;
        def.artSize = artSize == size ? Vector2Int.zero : artSize;
        def.description = description;
        def.requirements.cost = cost;
        def.requirements.technologies = needs != null ? new List<Technology> { needs } : new List<Technology>();
        def.buildTime = buildTime;
        def.maxHealth = health;
        def.workers = workers;
        def.requiredDeposit = null;
        EditorUtility.SetDirty(def);
        if (!db.buildings.Contains(def))
        {
            int i = db.buildings.FindIndex(b => b != null && b.id == after);
            db.buildings.Insert(i < 0 ? db.buildings.Count : i + 1, def);
        }
        return def;
    }

    private static BuildingUpgrade Upgrade(string name, string description, List<ResourceAmount> cost, float output) =>
        new BuildingUpgrade
        {
            name = name,
            description = description,
            requirements = new Requirements { cost = cost },
            upgradeTime = 20f,
            productionMultiplier = output,
        };

    private static ResourceRate Rate(ResourceType res, float perSecond) => new ResourceRate { resource = res, perSecond = perSecond };

    private static List<ResourceAmount> Cost(params object[] pairs)
    {
        var list = new List<ResourceAmount>();
        for (int i = 0; i + 1 < pairs.Length; i += 2)
        {
            list.Add(new ResourceAmount { resource = (ResourceType)pairs[i], amount = (int)pairs[i + 1] });
        }
        return list;
    }

    /// <summary>Add Tools to a cost: about 1 per 25 of everything else, in steps of 5.</summary>
    private static void AddTools(Requirements req, ResourceType tools)
    {
        if (req == null || req.cost.Exists(c => c.resource == tools)) return;
        int total = 0;
        foreach (var c in req.cost) total += c.amount;
        if (total <= 0) return;
        int amount = Mathf.Max(5, Mathf.RoundToInt(total / 25f / 5f) * 5);
        req.cost.Add(new ResourceAmount { resource = tools, amount = amount });
    }
}
