using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Fills out the first four ages so they're as full as the later ones: more troops, raiders and
/// chiefs, a Stone Age lookout, and more research (small global bonuses) in each.
/// Safe to run again; it only draws the units, chiefs and buildings it adds.
/// (Tools > Age of Stickman > Setup Early Age Depth.)
/// </summary>
public static partial class PlaceholderArt
{
    [MenuItem("Tools/Age of Stickman/Setup Early Age Depth")]
    public static void SetupEarlyAgeDepth()
    {
        var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Resources/GameDatabase.asset");
        Age stoneAge = db.ages.Find(a => a != null && a.name == "StoneAge"), bronzeAge = db.ages.Find(a => a != null && a.name == "BronzeAge"),
            ironAge = db.ages.Find(a => a != null && a.name == "IronAge"), classicalAge = db.ages.Find(a => a != null && a.name == "ClassicalAge");
        ResourceType food = Res(db, "Food"), wood = Res(db, "Wood"), stone = Res(db, "Stone"), bronze = Res(db, "Bronze"),
            iron = Res(db, "Iron"), gold = Res(db, "Gold"), coin = Res(db, "Coin");
        var melee = AssetDatabase.LoadAssetAtPath<TroopClass>("Assets/Data/TroopClasses/Melee.asset");
        var ranged = AssetDatabase.LoadAssetAtPath<TroopClass>("Assets/Data/TroopClasses/Ranged.asset");
        var cavalry = AssetDatabase.LoadAssetAtPath<TroopClass>("Assets/Data/TroopClasses/Cavalry.asset");

        // ---------- Research: small global bonuses, chained like the later ages ----------
        Technology T(Age age, string id, string name, string description, float time, List<ResourceAmount> cost, string after, params Technology[] needs)
        {
            var t = Tech(db, id, name, age, description, time, cost, after);
            t.requirements.minimumAge = age;
            t.requirements.technologies = new List<Technology>(needs);
            t.unitModifiers = new List<UnitStatModifier>();
            t.productionModifiers = new List<ProductionModifier>();
            t.buildingHealthMultiplier = 1f;
            t.coinPerPersonPerSecond = 0f;
            t.researchSpeedMultiplier = 1f;
            t.maxLevel = 1;
            EditorUtility.SetDirty(t);
            return t;
        }
        UnitStatModifier Mod(TroopClass cls, float hp = 1f, float atk = 1f, float spd = 1f, float range = 1f) =>
            new UnitStatModifier { troopClass = cls, healthMultiplier = hp, attackMultiplier = atk, speedMultiplier = spd, rangeMultiplier = range };
        ProductionModifier Prod(ResourceType r, float m) => new ProductionModifier { resource = r, multiplier = m };
        Technology Find(string id) => db.technologies.Find(t => t != null && t.id == id);

        // Stone Age
        var stoneTools = Find("StoneTools"); var woodworking = Find("Woodworking");
        var fireHardening = T(stoneAge, "FireHardening", "Fire-Hardened Spears", "Spear points hardened in the coals. Unlocks the Hunting Lodge; close-fighters hit 3% harder.",
            35f, Cost(wood, 70, food, 40), "HideArmor", stoneTools);
        fireHardening.unitModifiers = new List<UnitStatModifier> { Mod(melee, atk: 1.03f) };
        var atlatl = T(stoneAge, "Atlatl", "Spear-Throwers", "A notched stick that throws a spear twice as far. Unlocks the Throwing Ground; shooters reach 3% further.",
            40f, Cost(wood, 90, food, 50), fireHardening.id, fireHardening);
        atlatl.unitModifiers = new List<UnitStatModifier> { Mod(ranged, range: 1.03f) };
        var lookouts = T(stoneAge, "Lookouts", "Lookouts", "Someone on a high place, watching for smoke. Unlocks the Lookout Platform; buildings have 4% more health.",
            40f, Cost(wood, 100, stone, 30), atlatl.id, woodworking);
        lookouts.buildingHealthMultiplier = 1.04f;
        var smoking = T(stoneAge, "SmokingAndDrying", "Smoking and Drying", "Meat and fish smoked over the fire keep through the winter. Food comes in 5% faster.",
            35f, Cost(food, 90, wood, 40), lookouts.id);
        smoking.productionModifiers = new List<ProductionModifier> { Prod(food, 1.05f) };
        var storytelling = T(stoneAge, "Storytelling", "Storytelling", "What the old ones knew, told round the fire so it isn't lost. Research goes 3% faster.",
            45f, Cost(food, 120), smoking.id);
        storytelling.researchSpeedMultiplier = 1.03f;

        // Bronze Age
        var bronzeWeapons = Find("BronzeWeapons"); var masonry = Find("Masonry"); var agriculture = Find("Agriculture");
        var bronzeBows = T(bronzeAge, "BronzeBows", "Bronze-Tipped Arrows", "Arrowheads cast in bronze. Unlocks the Bowyer's Range; shooters hit 3% harder.",
            50f, Cost(wood, 120, bronze, 30), "CharcoalBurning", bronzeWeapons);
        bronzeBows.unitModifiers = new List<UnitStatModifier> { Mod(ranged, atk: 1.03f) };
        var herbalism = T(bronzeAge, "Herbalism", "Herbalism", "Which leaves close a wound and which ones stop a fever. Unlocks the Shaman's Hut; all troops have 3% more health.",
            50f, Cost(food, 150, wood, 60), bronzeBows.id);
        herbalism.unitModifiers = new List<UnitStatModifier> { Mod(null, hp: 1.03f) };
        var pottery = T(bronzeAge, "Pottery", "Pottery", "Fired jars that keep grain dry and mice out. Food comes in 5% faster.",
            40f, Cost(food, 100, stone, 60), herbalism.id, agriculture);
        pottery.productionModifiers = new List<ProductionModifier> { Prod(food, 1.05f) };
        var wheel = T(bronzeAge, "TheWheel", "The Spoked Wheel", "Light spoked wheels instead of solid ones. Riders and chariots move 4% faster.",
            50f, Cost(wood, 150, bronze, 30), pottery.id);
        wheel.unitModifiers = new List<UnitStatModifier> { Mod(cavalry, spd: 1.04f) };
        var writing = T(bronzeAge, "Writing", "Writing", "Marks pressed into clay that say what was traded and what was owed. Research goes 3% faster.",
            55f, Cost(food, 160, stone, 80), wheel.id, masonry);
        writing.researchSpeedMultiplier = 1.03f;

        // Iron Age
        var archery = Find("Archery"); var smithing = Find("Smithing"); var ironTools = Find("IronTools");
        var shieldwalls = T(ironAge, "Shieldwalls", "Shield Walls", "Shields locked rim to rim, spears over the top. Unlocks the Spear Guard Hall; close-fighters have 4% more health.",
            55f, Cost(wood, 140, iron, 50), "Roads", smithing);
        shieldwalls.unitModifiers = new List<UnitStatModifier> { Mod(melee, hp: 1.04f) };
        var bloomery = T(ironAge, "Bloomery", "The Bloomery", "A taller clay furnace with bellows: more iron from the same ore. Iron comes in 8% faster.",
            50f, Cost(stone, 140, wood, 100), shieldwalls.id, ironTools);
        bloomery.productionModifiers = new List<ProductionModifier> { Prod(iron, 1.08f) };
        var horseBreeding = T(ironAge, "HorseBreeding", "Horse Breeding", "Bigger, steadier horses bred from the best. Riders have 4% more health.",
            55f, Cost(food, 220, gold, 20), bloomery.id, archery);
        horseBreeding.unitModifiers = new List<UnitStatModifier> { Mod(cavalry, hp: 1.04f) };
        var hillforts = T(ironAge, "Hillforts", "Hillforts", "Ditches, banks and timber walls on every hill. Buildings have 6% more health.",
            60f, Cost(stone, 180, wood, 120), horseBreeding.id);
        hillforts.buildingHealthMultiplier = 1.06f;
        var calendar = T(ironAge, "TheCalendar", "The Calendar", "Stones set to catch the midwinter sun: knowing when to plant. Research goes 3% faster.",
            65f, Cost(stone, 200, food, 120), hillforts.id);
        calendar.researchSpeedMultiplier = 1.03f;

        // Classical Age
        var engineering = Find("Engineering"); var discipline = Find("Discipline");
        var aqueducts = T(classicalAge, "Aqueducts", "Aqueducts", "Water carried from the hills on arches of stone. Food comes in 5% faster; buildings have 4% more health.",
            70f, Cost(stone, 300, gold, 40), "Discipline", engineering);
        aqueducts.productionModifiers = new List<ProductionModifier> { Prod(food, 1.05f) };
        aqueducts.buildingHealthMultiplier = 1.04f;
        var games = T(classicalAge, "Games", "The Games", "Races, wrestling and the javelin, every fourth summer. All troops move 3% faster.",
            65f, Cost(food, 300, gold, 50, coin, 40), aqueducts.id, discipline);
        games.unitModifiers = new List<UnitStatModifier> { Mod(null, spd: 1.03f) };

        // ---------- Troops ----------
        var hunter = MedievalUnit("Hunter", "Hunter", stoneAge, "Clubman", 110, 11, 1.05f, 0.75f, 0.95f);
        var spearThrower = MedievalUnit("SpearThrower", "Spear-Thrower", stoneAge, "Slinger", 70, 9, 1.5f, 3.6f, 0.85f);
        var bronzeArcher = MedievalUnit("BronzeArcher", "Bronze Archer", bronzeAge, "Archer", 100, 12, 1.4f, 4.2f, 0.85f);
        var shaman = MedievalUnit("Shaman", "Shaman", bronzeAge, "Capsarius", 110, 8, 1.2f, 0.8f, 0.95f);
        shaman.healer = true; shaman.healRange = 7f;
        var spearGuard = MedievalUnit("SpearGuard", "Spear Guard", ironAge, "Spearman", 280, 17, 1.1f, 0.85f, 0.85f);
        foreach (var u in new[] { hunter, spearThrower, bronzeArcher, shaman, spearGuard }) EditorUtility.SetDirty(u);
        void AddUnit(List<UnitDefinition> list, UnitDefinition u) { list.RemoveAll(x => x == null); if (!list.Contains(u)) list.Add(u); }
        AddUnit(stoneAge.playerUnits, hunter); AddUnit(stoneAge.playerUnits, spearThrower);
        AddUnit(bronzeAge.playerUnits, bronzeArcher); AddUnit(bronzeAge.playerUnits, shaman);
        AddUnit(ironAge.playerUnits, spearGuard);

        // ---------- Raiders ----------
        var hurler = MedievalUnit("StoneHurler", "Stone Hurler", stoneAge, "RaiderArcher", 75, 10, 1.4f, 3.4f, 0.9f);
        var brute = MedievalUnit("CaveBrute", "Cave Brute", stoneAge, "EnemyClubman", 190, 16, 1.4f, 0.6f, 0.65f);
        var chariotRaider = MedievalUnit("ChariotRaider", "Chariot Raider", bronzeAge, "WarChariot", 220, 16, 1.2f, 0.9f, 1.5f);
        var bowman = MedievalUnit("BronzeBowman", "Raider Bowman", bronzeAge, "RaiderArcher", 105, 13, 1.4f, 4f, 0.9f);
        var axeRaider = MedievalUnit("RaiderAxeman", "Raider Axeman", ironAge, "IronRaider", 230, 25, 1.2f, 0.65f, 0.95f);
        var hoplite = MedievalUnit("MercenaryHoplite", "Mercenary Hoplite", classicalAge, "Barbarian", 380, 26, 1.1f, 0.8f, 0.9f);
        var warbandSlinger = MedievalUnit("WarbandSlinger", "Warband Slinger", classicalAge, "RaiderArcher", 170, 22, 1.3f, 4.2f, 1.0f);
        foreach (var u in new[] { hurler, brute, chariotRaider, bowman, axeRaider, hoplite, warbandSlinger }) EditorUtility.SetDirty(u);
        AddUnit(stoneAge.enemyUnits, hurler); AddUnit(stoneAge.enemyUnits, brute);
        AddUnit(bronzeAge.enemyUnits, chariotRaider); AddUnit(bronzeAge.enemyUnits, bowman);
        AddUnit(ironAge.enemyUnits, axeRaider);
        AddUnit(classicalAge.enemyUnits, hoplite); AddUnit(classicalAge.enemyUnits, warbandSlinger);
        foreach (var a in new[] { stoneAge, bronzeAge, ironAge, classicalAge }) EditorUtility.SetDirty(a);

        // ---------- Buildings ----------
        // Troop buildings: what they train and three upgrades, paid in what the age has.
        void TrainsEarly(BuildingDefinition b, UnitDefinition unit, int count, ResourceType metal, int scale)
        {
            b.trainsUnit = unit;
            b.maxUnits = count;
            b.workers = Mathf.Max(2, b.workers);
            b.upgrades = new List<BuildingUpgrade>
            {
                new BuildingUpgrade { name = "Bigger Camp", description = "+1 troop.", requirements = new Requirements { cost = Cost(food, 60 * scale, wood, 40 * scale) }, upgradeTime = 20f, extraUnits = 1 },
                new BuildingUpgrade { name = "Better Weapons", description = "Troops trained here are 15% tougher and hit 10% harder.",
                    requirements = new Requirements { cost = Cost(metal, 20 * scale, wood, 40 * scale) }, upgradeTime = 25f,
                    trainedUnits = new UnitStatModifier { healthMultiplier = 1.15f, attackMultiplier = 1.1f } },
                new BuildingUpgrade { name = "Seasoned Leaders", description = "+1 troop, +" + (25 * scale) + " health.", requirements = new Requirements { cost = Cost(food, 100 * scale, metal, 15 * scale) },
                    upgradeTime = 25f, extraUnits = 1, extraHealth = 25 * scale },
            };
        }
        void RankInto(BuildingDefinition from, string toId, Age age, List<ResourceAmount> cost)
        {
            var to = db.GetBuilding(toId);
            if (to == null) return;
            from.rankUpTo = to;
            from.rankUpRequirements = new Requirements { minimumAge = age, cost = cost, technologies = new List<Technology>(to.requirements.technologies.FindAll(t => t != null)) };
            from.rankUpTime = 30f;
        }

        var lodge = NewBuilding(db, "AxeHall", "HuntingLodge", "Hunting Lodge", stoneAge, fireHardening, new Vector2Int(3, 2),
            "A hide-roofed lodge hung with antlers and drying meat. Trains Hunters: quick on their feet, with a fire-hardened spear.",
            Cost(wood, 90, food, 40), 220);
        TrainsEarly(lodge, hunter, 4, stone, 1);
        RankInto(lodge, "AxeHall", bronzeAge, Cost(wood, 80, bronze, 20));

        var throwing = NewBuilding(db, "SlingerCamp", "ThrowingGround", "Throwing Ground", stoneAge, atlatl, new Vector2Int(3, 2),
            "A trampled field with straw targets and a rack of light spears. Trains Spear-Throwers, who hurl them from far off.",
            Cost(wood, 100, food, 50), 200);
        TrainsEarly(throwing, spearThrower, 3, stone, 1);
        RankInto(throwing, "SlingerCamp", ironAge, Cost(wood, 100, iron, 20));

        var lookout = NewBuilding(db, "Watchtower", "LookoutPlatform", "Lookout Platform", stoneAge, lookouts, new Vector2Int(2, 2),
            "A platform of lashed poles with a spear-thrower on watch. Shoots raiders that come near.",
            Cost(wood, 110, stone, 20), 260);
        lookout.towerRange = 5f;
        lookout.towerDamage = 9;
        lookout.towerCooldown = 1.7f;
        lookout.workers = 1;
        lookout.operatorOffset = new Vector2(1f, 1.45f);
        lookout.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Hardened Points", description = "+4 damage.", requirements = new Requirements { cost = Cost(wood, 60, stone, 20) }, upgradeTime = 20f, extraTowerDamage = 4 },
            new BuildingUpgrade { name = "Taller Poles", description = "+1 range.", requirements = new Requirements { cost = Cost(wood, 90, food, 40) }, upgradeTime = 20f, extraTowerRange = 1f },
        };
        RankInto(lookout, "Watchtower", bronzeAge, Cost(wood, 80, stone, 60));

        var bowyers = NewBuilding(db, "ArcheryRange", "BowyersRange", "Bowyer's Range", bronzeAge, bronzeBows, new Vector2Int(3, 2),
            "A mud-brick workshop where bows are strung, with butts of straw to shoot at. Trains Bronze Archers.",
            Cost(wood, 160, bronze, 30, stone, 40), 300);
        TrainsEarly(bowyers, bronzeArcher, 4, bronze, 2);
        RankInto(bowyers, "ArcheryRange", ironAge, Cost(wood, 120, iron, 30));

        var shamanHut = NewBuilding(db, "Valetudinarium", "ShamansHut", "Shaman's Hut", bronzeAge, herbalism, new Vector2Int(2, 2),
            "A round hut of wattle and hide, herbs drying in the smoke and charms on the door. Trains Shamans, who tend the wounded.",
            Cost(wood, 120, food, 80), 220);
        shamanHut.trainsUnit = shaman;
        shamanHut.maxUnits = 2;
        shamanHut.workers = 2;
        shamanHut.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Second Fire", description = "+1 shaman.", requirements = new Requirements { cost = Cost(food, 140, wood, 80) }, upgradeTime = 20f, extraUnits = 1 },
            new BuildingUpgrade { name = "Strong Medicine", description = "Shamans heal 30% more and are 20% tougher.", requirements = new Requirements { cost = Cost(food, 180, bronze, 30) }, upgradeTime = 25f,
                trainedUnits = new UnitStatModifier { attackMultiplier = 1.3f, healthMultiplier = 1.2f } },
        };
        RankInto(shamanHut, "Valetudinarium", classicalAge, Cost(stone, 140, gold, 20));

        var spearHall = NewBuilding(db, "Garrison", "SpearGuardHall", "Spear Guard Hall", ironAge, shieldwalls, new Vector2Int(3, 2),
            "A long hall with round shields hung down its walls. Trains Spear Guards: they hold the line and don't give ground.",
            Cost(wood, 180, stone, 100, iron, 40), 420);
        TrainsEarly(spearHall, spearGuard, 4, iron, 3);

        var added = new[] { lodge, throwing, lookout, bowyers, shamanHut, spearHall };
        foreach (var b in added) EditorUtility.SetDirty(b);
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();

        // ---------- Art: only what this adds ----------
        foreach (var b in added) GenerateBuildingLooks(b);
        GenerateWarriorsAndCrews(db, new HashSet<string>
        {
            "Hunter", "SpearThrower", "BronzeArcher", "Shaman", "SpearGuard",
            "StoneHurler", "CaveBrute", "ChariotRaider", "BronzeBowman", "RaiderAxeman", "MercenaryHoplite", "WarbandSlinger",
            "LookoutPlatform",
        });
        SetupBosses(null, new HashSet<string> { "BossBoneSpeaker", "BossBronzeGiant", "BossWarQueen", "BossElephantLord" });
        AssetDatabase.SaveAssets();
        Debug.Log("Early age depth set up.");
    }
}
