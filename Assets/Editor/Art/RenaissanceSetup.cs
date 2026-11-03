using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The Renaissance: gunpowder, banks and (the fantasy layer) alchemy. Printing Press (Medieval
/// research) opens it. New resources: Saltpetre (a dry-grassland deposit), Gunpowder (Saltpetre +
/// Charcoal at a Powder Mill) and Quicksilver (a hill deposit for the alchemists). New deposits go
/// last in the placement order, so nothing already on the map moves. Medieval buildings rank up
/// into Renaissance ones; the last crewed tower (a cannon bastion); cannons and fire flasks burst
/// over an area. Safe to run again. (Tools > Age of Stickman > Setup Renaissance.)
/// </summary>
public static partial class PlaceholderArt
{
    [MenuItem("Tools/Age of Stickman/Setup Renaissance")]
    public static void SetupRenaissance()
    {
        var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Resources/GameDatabase.asset");
        Age medieval = db.ages[4];
        var age = AgeAsset(db, "RenaissanceAge", "Renaissance", 5,
            "Gunpowder and printing, banks and painted domes, and alchemists bent over their green fires.");

        // ---------- Resources and the map ----------
        ResourceType food = Res(db, "Food"), wood = Res(db, "Wood"), stone = Res(db, "Stone"), iron = Res(db, "Iron"),
            charcoal = Res(db, "Charcoal"), tools = Res(db, "Tools"), gold = Res(db, "Gold"), coin = Res(db, "Coin"),
            steel = Res(db, "Steel"), runestone = Res(db, "Runestone");
        var saltpetre = MedievalResource(db, "Saltpetre", "Saltpetre", age, "Runestone");
        var gunpowder = MedievalResource(db, "Gunpowder", "Gunpowder", age, "Saltpetre");
        var quicksilver = MedievalResource(db, "Quicksilver", "Quicksilver", age, "Gunpowder");
        // Measured on three worlds: Saltpetre ~0.8% of land (a little above the ores, as powder takes a lot),
        // Quicksilver ~0.5% (level with copper and runestone).
        var saltDeposit = NewDeposit(db, "Saltpetre", "Saltpetre", saltpetre, false, 0.78f, 0f, 0.4f, 0.2f);
        var quickDeposit = NewDeposit(db, "Quicksilver", "Quicksilver", quicksilver, true, 0.64f, 0f, 1f, -1f);

        // ---------- Research ----------
        var printing = Tech(db, "PrintingPress", "Printing Press", medieval,
            "Books for everyone who can read, and soon everyone can. Opens the way to the Renaissance.",
            110f, Cost(food, 1200, gold, 300, coin, 300, runestone, 80), "RuneWarding");
        printing.requirements.minimumAge = medieval;
        printing.requirements.technologies = new List<Technology>();
        EditorUtility.SetDirty(printing);

        Technology RTech(string id, string name, string description, float time, List<ResourceAmount> cost, string after, params Technology[] needs)
        {
            var t = Tech(db, id, name, age, description, time, cost, after);
            t.requirements.minimumAge = age;
            t.requirements.technologies = new List<Technology>(needs);
            t.unitModifiers = new List<UnitStatModifier>();
            t.productionModifiers = new List<ProductionModifier>();
            t.buildingHealthMultiplier = 1f;
            t.coinPerPersonPerSecond = 0f;
            t.researchSpeedMultiplier = 1f;
            EditorUtility.SetDirty(t);
            return t;
        }
        var powder = RTech("Gunpowder", "Gunpowder", "Saltpetre, charcoal and sulphur, ground fine. Unlocks the Saltpetre Works and the Powder Mill.",
            90f, Cost(charcoal, 400, iron, 300, gold, 100), "PrintingPress");
        var pikeShot = RTech("PikeAndShot", "Pike and Shot", "Halberds to hold the line, arquebuses to break the charge. Unlocks the Tercio Barracks and the Musket Range; all troops hit 4% harder.",
            100f, Cost(steel, 150, gunpowder, 60), powder.id, powder);
        pikeShot.unitModifiers = new List<UnitStatModifier> { new UnitStatModifier { attackMultiplier = 1.04f } };
        var cuirassiers = RTech("Cuirassiers", "Cuirassiers", "Riders in breastplates who fire a brace of pistols at the gallop. Unlocks the Cuirassier Stables.",
            90f, Cost(food, 800, steel, 120, gunpowder, 40), pikeShot.id, pikeShot);
        var artillery = RTech("Artillery", "Artillery", "Bronze guns on wheeled carriages. Unlocks the Cannon Foundry.",
            110f, Cost(steel, 200, gunpowder, 100, tools, 60), cuirassiers.id, powder);
        var bastions = RTech("BastionForts", "Bastion Forts", "Low, thick, angled walls that shrug off cannon. Unlocks bastion walls and the Cannon Bastion; buildings have 8% more health.",
            110f, Cost(stone, 1200, gunpowder, 80), artillery.id, powder);
        bastions.buildingHealthMultiplier = 1.08f;
        var banking = RTech("Banking", "Banking", "Letters of credit and counting houses. Unlocks the Bank; everyone pays a little more in taxes.",
            80f, Cost(gold, 400, coin, 300), bastions.id);
        banking.coinPerPersonPerSecond = 0.002f;
        var anatomy = RTech("Anatomy", "Anatomy", "Physicians who have looked inside. Unlocks the Hospital; all troops have 4% more health.",
            90f, Cost(food, 800, gold, 200), banking.id);
        anatomy.unitModifiers = new List<UnitStatModifier> { new UnitStatModifier { healthMultiplier = 1.04f } };
        var astronomy = RTech("Astronomy", "Astronomy", "Lenses, charts and new heavens. Research goes 5% faster.",
            80f, Cost(gold, 300, coin, 200, tools, 40), anatomy.id);
        astronomy.researchSpeedMultiplier = 1.05f;
        var alchemy = RTech("Alchemy", "Alchemy", "The search for the stone that turns lead to gold. Unlocks the Quicksilver Mine; everything you produce comes in 5% faster.",
            90f, Cost(stone, 600, gold, 200, runestone, 100), astronomy.id);
        alchemy.productionModifiers = new List<ProductionModifier> { new ProductionModifier { resource = null, multiplier = 1.05f } };
        var transmutation = RTech("Transmutation", "Transmutation", "Alchemists who take their fire to war. Unlocks the Alchemist's Lab.",
            100f, Cost(quicksilver, 120, gold, 200), alchemy.id, alchemy);

        age.advanceRequirements = new Requirements
        {
            technologies = new List<Technology> { printing },
            cost = Cost(food, 4500, wood, 3500, stone, 3500, iron, 1000, gold, 600, coin, 600, steel, 300),
            population = 400,
        };
        EditorUtility.SetDirty(age);

        // ---------- Units ----------
        var arquebusier = MedievalUnit("Arquebusier", "Arquebusier", age, "Crossbowman", 260, 48, 2.6f, 6.0f, 0.9f);
        var halberdier = MedievalUnit("Halberdier", "Halberdier", age, "Pikeman", 520, 40, 1.15f, 1.0f, 0.9f);
        var cuirassier = MedievalUnit("Cuirassier", "Cuirassier", age, "Knight", 560, 40, 1.8f, 3.2f, 1.7f);
        var physician = MedievalUnit("Physician", "Physician", age, "Monk", 260, 28, 1.1f, 0.8f, 1.0f);
        physician.healer = true; physician.healRange = 10f;
        var alchemist = MedievalUnit("Alchemist", "Alchemist", age, "RuneWarden", 280, 34, 2.4f, 4.8f, 0.9f);
        var cannon = MedievalUnit("FieldCannon", "Field Cannon", age, "Crossbowman", 420, 90, 4.5f, 8.0f, 0.55f);
        cannon.populationCost = 2;
        foreach (var u in new[] { arquebusier, halberdier, cuirassier, physician, alchemist, cannon })
        {
            u.upkeep = new List<ResourceRate> { Rate(food, 0.01f), Rate(coin, 0.05f) };
            EditorUtility.SetDirty(u);
        }
        age.playerUnits = new List<UnitDefinition> { arquebusier, halberdier, cuirassier, physician, alchemist, cannon };

        var landsknecht = MedievalUnit("Landsknecht", "Landsknecht", age, "Brigand", 520, 46, 1.3f, 0.75f, 0.95f);
        var highwayman = MedievalUnit("Highwayman", "Highwayman", age, "Reaver", 480, 40, 1.8f, 3.2f, 1.6f);
        var musketeer = MedievalUnit("BrigandMusketeer", "Brigand Musketeer", age, "BanditCrossbow", 240, 44, 2.6f, 5.8f, 0.9f);
        age.enemyUnits = new List<UnitDefinition> { landsknecht, highwayman, musketeer };
        EditorUtility.SetDirty(age);

        // ---------- Buildings ----------
        var merchantHouse = RankUp(db, "Townhouse", "MerchantHouse", "Merchant House", age, printing,
            "A tall stucco house over a shop arcade. Houses 54 people.",
            Cost(stone, 320, wood, 180, gold, 50), Cost(stone, 160, wood, 80, gold, 30, tools, 10), 750);
        merchantHouse.housing = 54;
        merchantHouse.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Fourth Floor", description = "+10 people.", requirements = new Requirements { cost = Cost(stone, 140, wood, 100) }, upgradeTime = 25f, extraHousing = 10 },
            new BuildingUpgrade { name = "Balconies", description = "+10 people.", requirements = new Requirements { cost = Cost(stone, 120, iron, 30, gold, 30) }, upgradeTime = 25f, extraHousing = 10 },
        };

        var bank = RankUp(db, "Guildhall", "Bank", "Bank", age, banking,
            "Marble, pilasters and a dome: a counting house that turns gold into a great deal of coin.",
            Cost(stone, 300, gold, 100, steel, 20), Cost(stone, 150, gold, 60), 650);
        bank.workers = 3;
        bank.production = new List<ResourceRate> { Rate(coin, 1.3f) };
        bank.inputs = new List<ResourceRate> { Rate(gold, 0.06f) };
        bank.upgrades = new List<BuildingUpgrade>
        {
            Upgrade("Letters of Credit", "+25% coin.", Cost(gold, 100, coin, 120), 1.25f),
            Upgrade("Bourse", "+25% coin.", Cost(gold, 120, steel, 30), 1.25f),
        };

        var tercio = RankUp(db, "ArmsHall", "TercioBarracks", "Tercio Barracks", age, pikeShot,
            "Brick barracks and a drill square. Trains Halberdiers in breastplates.",
            Cost(stone, 320, wood, 220, steel, 80), Cost(stone, 160, steel, 50), 950);
        Trains(tercio, halberdier, 4, steel, gold, food, coin);

        var musketRange = RankUp(db, "CrossbowRange", "MusketRange", "Musket Range", age, pikeShot,
            "A firing line and distant targets. Trains Arquebusiers: slow to load, but a ball punches through armour.",
            Cost(wood, 320, stone, 200, steel, 60, gunpowder, 40), Cost(wood, 160, gunpowder, 40), 700);
        Trains(musketRange, arquebusier, 4, steel, gold, food, coin);

        var cuirassierStables = RankUp(db, "KnightsHall", "CuirassierStables", "Cuirassier Stables", age, cuirassiers,
            "Brick stables for heavy horse. Trains Cuirassiers, who ride in close and fire their pistols.",
            Cost(stone, 320, wood, 200, steel, 80, food, 300), Cost(stone, 160, steel, 50), 700);
        Trains(cuirassierStables, cuirassier, 3, steel, gold, food, coin);

        var hospital = RankUp(db, "Monastery", "Hospital", "Hospital", age, anatomy,
            "A long loggia and a domed chapel. Trains Physicians, the best healers yet.",
            Cost(stone, 360, wood, 200, gold, 60), Cost(stone, 180, gold, 40), 700);
        hospital.trainsUnit = physician;
        hospital.maxUnits = 3;
        hospital.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Surgery", description = "+1 physician.", requirements = new Requirements { cost = Cost(food, 250, stone, 120) }, upgradeTime = 25f, extraUnits = 1 },
            new BuildingUpgrade { name = "Apothecary", description = "Physicians heal 30% more and are 20% tougher.", requirements = new Requirements { cost = Cost(gold, 80, quicksilver, 30) }, upgradeTime = 30f,
                trainedUnits = new UnitStatModifier { attackMultiplier = 1.3f, healthMultiplier = 1.2f } },
        };

        var cannonBastion = RankUp(db, "CastleTower", "CannonBastion", "Cannon Bastion", age, bastions,
            "A low, thick brick bastion with a cannon on top. Its shot bursts among the raiders. (The last tower with a crew in the open.)",
            Cost(stone, 400, gunpowder, 60, steel, 40), Cost(stone, 200, gunpowder, 40), 1000);
        cannonBastion.workers = 3;
        cannonBastion.towerRange = 11f;
        cannonBastion.towerDamage = 110;
        cannonBastion.towerCooldown = 3.4f;
        cannonBastion.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Longer Barrel", description = "+1.5 range.", requirements = new Requirements { cost = Cost(steel, 40, gunpowder, 30) }, upgradeTime = 25f, extraTowerRange = 1.5f },
            new BuildingUpgrade { name = "Chain Shot", description = "+35 damage.", requirements = new Requirements { cost = Cost(iron, 80, gunpowder, 40) }, upgradeTime = 25f, extraTowerDamage = 35 },
            new BuildingUpgrade { name = "Earth Rampart", description = "+500 health.", requirements = new Requirements { cost = Cost(stone, 250) }, upgradeTime = 30f, extraHealth = 500 },
        };

        var bastionWall = RankUp(db, "CastleWall", "BastionWall", "Bastion Wall", age, bastions,
            "A low, thick brick wall, angled to turn cannon shot.", Cost(stone, 30), Cost(stone, 24), 2400);
        var bastionGate = RankUp(db, "CastleGatehouse", "BastionGate", "Bastion Gate", age, bastions,
            "A brick gate between angled bastions.", Cost(stone, 60, iron, 10), Cost(stone, 48, iron, 8), 3360);
        bastionWall.upgrades = new List<BuildingUpgrade>();
        bastionGate.upgrades = new List<BuildingUpgrade>();

        var saltWorks = NewBuilding(db, "RuneQuarry", "SaltpetreWorks", "Saltpetre Works", age, powder, new Vector2Int(3, 3),
            "Nitre beds and boiling vats. Must be built on or next to Saltpetre (white crust on dry ground).",
            Cost(wood, 260, stone, 160, iron, 60), 380);
        saltWorks.requiredDeposit = saltDeposit;
        saltWorks.canBuildOnHills = false;
        saltWorks.workers = 4;
        saltWorks.production = new List<ResourceRate> { Rate(saltpetre, 0.14f) };
        saltWorks.upgrades = new List<BuildingUpgrade>
        {
            Upgrade("More Beds", "+30% saltpetre.", Cost(wood, 160, stone, 80), 1.3f),
            Upgrade("Copper Vats", "+30% saltpetre.", Cost(iron, 60, tools, 30), 1.3f),
        };

        var powderMill = NewBuilding(db, "Steelworks", "PowderMill", "Powder Mill", age, powder, new Vector2Int(3, 3),
            "A water-driven stamp mill: grinds Saltpetre and Charcoal into Gunpowder.",
            Cost(stone, 300, wood, 280, iron, 80), 420);
        powderMill.workers = 4;
        powderMill.production = new List<ResourceRate> { Rate(gunpowder, 0.1f) };
        powderMill.inputs = new List<ResourceRate> { Rate(saltpetre, 0.1f), Rate(charcoal, 0.08f) };
        powderMill.upgrades = new List<BuildingUpgrade>
        {
            Upgrade("Corning Sieves", "+30% gunpowder.", Cost(wood, 160, steel, 30), 1.3f),
            Upgrade("Second Wheel", "+30% gunpowder.", Cost(stone, 200, tools, 40), 1.3f),
        };

        var quickMine = NewBuilding(db, "RuneQuarry", "QuicksilverMine", "Quicksilver Mine", age, alchemy, new Vector2Int(3, 3),
            "Digs cinnabar from the hills and boils out the quicksilver. Must be built on or next to a Quicksilver deposit.",
            Cost(wood, 240, stone, 180, steel, 40), 400);
        quickMine.requiredDeposit = quickDeposit;
        quickMine.canBuildOnHills = true;
        quickMine.workers = 4;
        quickMine.production = new List<ResourceRate> { Rate(quicksilver, 0.1f) };
        quickMine.upgrades = new List<BuildingUpgrade>
        {
            Upgrade("Deeper Adit", "+30% quicksilver.", Cost(wood, 180, iron, 60), 1.3f),
            Upgrade("Better Retorts", "+30% quicksilver.", Cost(steel, 40, tools, 30), 1.3f),
        };

        var foundry = NewBuilding(db, "PikeYard", "CannonFoundry", "Cannon Foundry", age, artillery, new Vector2Int(3, 3),
            "Casts bronze guns. Trains Field Cannons: slow and fragile, but their shot bursts among the enemy.",
            Cost(stone, 360, wood, 200, steel, 120, gunpowder, 60), 600);
        foundry.trainsUnit = cannon;
        foundry.maxUnits = 2;
        foundry.workers = 4;
        foundry.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Second Mould", description = "+1 cannon.", requirements = new Requirements { cost = Cost(steel, 120, gunpowder, 60) }, upgradeTime = 35f, extraUnits = 1 },
            new BuildingUpgrade { name = "Bored Barrels", description = "Cannons hit 25% harder.", requirements = new Requirements { cost = Cost(steel, 100, tools, 60) }, upgradeTime = 35f,
                trainedUnits = new UnitStatModifier { attackMultiplier = 1.25f } },
        };

        var lab = NewBuilding(db, "RuneShrine", "AlchemistLab", "Alchemist's Lab", age, transmutation, new Vector2Int(2, 2),
            "A crooked tower full of green light. Trains Alchemists, who hurl flasks of fire that burst over an area.",
            Cost(stone, 260, quicksilver, 80, gold, 80), 480);
        lab.trainsUnit = alchemist;
        lab.maxUnits = 2;
        lab.workers = 2;
        lab.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Bigger Alembic", description = "+1 alchemist.", requirements = new Requirements { cost = Cost(quicksilver, 60, gold, 60) }, upgradeTime = 25f, extraUnits = 1 },
            new BuildingUpgrade { name = "Greek Fire", description = "Alchemists hit 25% harder.", requirements = new Requirements { cost = Cost(quicksilver, 100, gunpowder, 40) }, upgradeTime = 30f,
                trainedUnits = new UnitStatModifier { attackMultiplier = 1.25f } },
        };

        var all = new[] { merchantHouse, bank, tercio, musketRange, cuirassierStables, hospital, cannonBastion, bastionWall, bastionGate, saltWorks, powderMill, quickMine, foundry, lab };
        foreach (var b in all) EditorUtility.SetDirty(b);
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();

        // ---------- Art ----------
        saltDeposit.sprites = PixelDeposits("Saltpetre", 3, Ore.Saltpetre);
        quickDeposit.sprites = PixelDeposits("Quicksilver", 3, Ore.Quicksilver);
        EditorUtility.SetDirty(saltDeposit);
        EditorUtility.SetDirty(quickDeposit);
        foreach (var b in all)
            if (!b.IsWall) GenerateBuildingLooks(b);
        GenerateWallArt();
        GenerateWarriorsAndCrews(db);
        SetupBosses();
        GenerateIconArt();
        AssetDatabase.SaveAssets();
        Debug.Log("Renaissance set up.");
    }

    private static Age AgeAsset(GameDatabase db, string id, string display, int order, string description)
    {
        string path = $"Assets/Data/Ages/{id}.asset";
        var age = AssetDatabase.LoadAssetAtPath<Age>(path);
        if (age == null)
        {
            age = ScriptableObject.CreateInstance<Age>();
            AssetDatabase.CreateAsset(age, path);
        }
        age.displayName = display;
        age.order = order;
        age.description = description;
        if (!db.ages.Contains(age)) db.ages.Add(age);
        EditorUtility.SetDirty(age);
        return age;
    }

    /// <summary>
    /// A map deposit, always added at the end of the list: deposits are placed in list order, so
    /// a new one only fills tiles that were empty and nothing already on the map moves.
    /// </summary>
    private static DepositType NewDeposit(GameDatabase db, string id, string display, ResourceType resource, bool onHills,
        float rarity, float minMoisture, float maxMoisture, float patchScale)
    {
        string path = $"Assets/Data/Deposits/{id}.asset";
        var dep = AssetDatabase.LoadAssetAtPath<DepositType>(path);
        if (dep == null)
        {
            dep = ScriptableObject.CreateInstance<DepositType>();
            AssetDatabase.CreateAsset(dep, path);
        }
        var iron = db.deposits.Find(d => d != null && d.id == "IronOre");
        dep.id = id;
        dep.displayName = display;
        dep.resource = resource;
        dep.onHills = onHills;
        dep.patchScale = patchScale > 0 ? patchScale : iron != null ? iron.patchScale : 0.22f;
        dep.rarity = rarity;
        dep.minMoisture = minMoisture;
        dep.maxMoisture = maxMoisture;
        if (!db.deposits.Contains(dep)) db.deposits.Add(dep);
        EditorUtility.SetDirty(dep);
        return dep;
    }
}
