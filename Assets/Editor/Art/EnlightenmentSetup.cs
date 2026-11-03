using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The Age of Enlightenment, between the Renaissance and the Industrial Age, so the ages follow
/// each other without a jump: flintlocks and bayonets, hussars and light infantry, star forts, coke
/// furnaces, banks and encyclopaedias, and (the fantasy layer) galvanism and clockwork, which lead
/// on to the Industrial Age's aether and automatons. Natural Philosophy (Renaissance research) opens
/// it; Steam Engine (its own research) opens the Industrial Age. Coal arrives here with coke smelting.
/// Also joins up lines that used to stop dead: the Pike Yard grows into the Tercio Barracks and the
/// Rune Shrine into the Alchemist's Lab (runes, then alchemy, galvanism and aether).
/// Safe to run again. (Tools > Age of Stickman > Setup Age of Enlightenment.)
/// </summary>
public static partial class PlaceholderArt
{
    [MenuItem("Tools/Age of Stickman/Setup Age of Enlightenment")]
    public static void SetupEnlightenment()
    {
        var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Resources/GameDatabase.asset");
        Age renaissance = db.ages.Find(a => a != null && a.name == "RenaissanceAge");
        var age = AgeAsset(db, "EnlightenmentAge", "Enlightenment", 6,
            "Reason and powder smoke: flintlocks and bayonets, star forts, encyclopaedias, and philosophers coaxing lightning into jars.");
        // It sits between the Renaissance and the Industrial Age, which moves up one.
        var industrialAge = db.ages.Find(a => a != null && a.name == "IndustrialAge");
        if (industrialAge != null)
        {
            industrialAge.order = 7;
            EditorUtility.SetDirty(industrialAge);
            db.ages.Remove(age);
            db.ages.Insert(db.ages.IndexOf(industrialAge), age);
        }

        // ---------- Resources ----------
        ResourceType food = Res(db, "Food"), wood = Res(db, "Wood"), stone = Res(db, "Stone"), iron = Res(db, "Iron"),
            tools = Res(db, "Tools"), gold = Res(db, "Gold"), coin = Res(db, "Coin"), steel = Res(db, "Steel"),
            gunpowder = Res(db, "Gunpowder"), quicksilver = Res(db, "Quicksilver");
        // Coal comes with coke smelting (the deposit keeps its place in the map's list, so nothing moves).
        var coal = MedievalResource(db, "Coal", "Coal", age, "Quicksilver");
        var coalDeposit = NewDeposit(db, "Coal", "Coal", coal, true, 0.62f, 0f, 1f, -1f);

        // ---------- Research ----------
        var philosophy = Tech(db, "NaturalPhilosophy", "Natural Philosophy", renaissance,
            "Measure, test and doubt. Opens the way to the Age of Enlightenment.",
            115f, Cost(food, 1300, gold, 350, coin, 450, gunpowder, 80, quicksilver, 40), "Transmutation");
        philosophy.requirements.minimumAge = renaissance;
        philosophy.requirements.technologies = new List<Technology>();
        EditorUtility.SetDirty(philosophy);

        Technology ETech(string id, string name, string description, float time, List<ResourceAmount> cost, string after, params Technology[] needs)
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
        var flintlocks = ETech("Flintlocks", "Flintlocks and Bayonets", "A spark from a flint, and a blade on the muzzle: every musketeer is a pikeman too. Tercio Barracks rank up into Musketeer Barracks; all troops hit 4% harder.",
            95f, Cost(steel, 180, gunpowder, 100), "NaturalPhilosophy");
        flintlocks.unitModifiers = new List<UnitStatModifier> { new UnitStatModifier { attackMultiplier = 1.04f } };
        var lightInfantry = ETech("LightInfantry", "Light Infantry", "Huntsmen with rifled carbines who fight from cover. Musket Ranges rank up into the Jäger Range.",
            90f, Cost(steel, 150, gunpowder, 80, wood, 600), flintlocks.id, flintlocks);
        var hussars = ETech("Hussars", "Hussars", "Light horsemen in fur busbies who ride down skirmishers with the sabre. Cuirassier Stables rank up into Hussar Stables.",
            90f, Cost(food, 900, steel, 140), lightInfantry.id);
        var coke = ETech("CokeSmelting", "Coke Smelting", "Coal baked into coke burns hotter than charcoal. Unlocks the Coal Mine; Steelworks rank up into the Blast Furnace; steel comes 10% faster.",
            100f, Cost(iron, 500, tools, 120, stone, 600), hussars.id);
        coke.productionModifiers = new List<ProductionModifier> { new ProductionModifier { resource = steel, multiplier = 1.1f } };
        var starForts = ETech("StarForts", "Star Forts", "Low, angled ramparts with not one blind spot. Unlocks star-fort walls and the Casemate Battery; buildings have 8% more health.",
            105f, Cost(stone, 1300, gunpowder, 100), coke.id);
        starForts.buildingHealthMultiplier = 1.08f;
        var howitzers = ETech("Howitzers", "Howitzers", "Short guns that lob shells over walls. Cannon Foundries rank up into the Artillery Park.",
            105f, Cost(steel, 220, gunpowder, 120, tools, 60), starForts.id, flintlocks);
        var banking = ETech("CentralBanking", "Central Banking", "A national bank and paper money. Banks rank up into the Royal Bank; everyone pays a little more in taxes.",
            85f, Cost(gold, 450, coin, 350), howitzers.id);
        banking.coinPerPersonPerSecond = 0.002f;
        var surgery = ETech("SurgeonsGuild", "Surgeons' Guild", "Surgeons with proper training and sharp saws. Hospitals rank up into Surgeons' Halls; all troops have 4% more health.",
            90f, Cost(food, 900, gold, 200), banking.id);
        surgery.unitModifiers = new List<UnitStatModifier> { new UnitStatModifier { healthMultiplier = 1.04f } };
        var encyclopedie = ETech("Encyclopedie", "The Encyclopédie", "All knowledge, in thirty-five volumes. Merchant Houses rank up into Georgian Townhouses; research goes 5% faster.",
            95f, Cost(gold, 300, coin, 300, wood, 500), surgery.id);
        encyclopedie.researchSpeedMultiplier = 1.05f;
        var clockwork = ETech("Clockwork", "Clockwork", "Springs, escapements and toothed wheels that keep perfect time. Everything you produce comes in 5% faster.",
            100f, Cost(tools, 150, gold, 250, steel, 100), encyclopedie.id);
        clockwork.productionModifiers = new List<ProductionModifier> { new ProductionModifier { resource = null, multiplier = 1.05f } };
        var galvanism = ETech("Galvanism", "Galvanism", "Lightning caught in jars, made to leap between globes of copper. Alchemist's Labs rank up into the Galvanic Lab.",
            100f, Cost(quicksilver, 120, gold, 200, steel, 80), clockwork.id, clockwork);

        age.advanceRequirements = new Requirements
        {
            technologies = new List<Technology> { philosophy },
            cost = Cost(food, 5200, wood, 4000, stone, 4000, iron, 1200, gold, 700, coin, 750, steel, 400, gunpowder, 200),
            population = 450,
        };
        EditorUtility.SetDirty(age);
        // Every age asks for some people (the Iron and Classical Ages had no goal at all).
        foreach (var (name, people) in new[] { ("IronAge", 40), ("ClassicalAge", 120) })
        {
            var a = db.ages.Find(x => x != null && x.name == name);
            if (a == null) continue;
            a.advanceRequirements.population = people;
            EditorUtility.SetDirty(a);
        }

        // ---------- Units ----------
        var musketeer = MedievalUnit("Musketeer", "Musketeer", age, "Arquebusier", 290, 52, 2.4f, 6.2f, 0.9f);
        var jager = MedievalUnit("Jager", "Jäger", age, "Arquebusier", 260, 56, 2.6f, 7.0f, 0.95f);
        var hussar = MedievalUnit("Hussar", "Hussar", age, "Knight", 600, 42, 1.0f, 1.0f, 1.85f);
        var surgeon = MedievalUnit("RegimentalSurgeon", "Regimental Surgeon", age, "Physician", 280, 31, 1.05f, 0.8f, 1.0f);
        surgeon.healer = true; surgeon.healRange = 10.5f;
        var galvanist = MedievalUnit("Galvanist", "Galvanist", age, "RuneWarden", 300, 40, 2.2f, 5.2f, 0.9f);
        var howitzer = MedievalUnit("Howitzer", "Howitzer", age, "FieldCannon", 460, 105, 4.4f, 8.5f, 0.55f);
        howitzer.populationCost = 2;
        foreach (var u in new[] { musketeer, jager, hussar, surgeon, galvanist, howitzer })
        {
            u.upkeep = new List<ResourceRate> { Rate(food, 0.01f), Rate(coin, 0.055f) };
            EditorUtility.SetDirty(u);
        }
        age.playerUnits = new List<UnitDefinition> { musketeer, jager, hussar, surgeon, galvanist, howitzer };
        var privateer = MedievalUnit("Privateer", "Privateer", age, "Landsknecht", 560, 50, 1.2f, 0.8f, 1.0f);
        var fusilier = MedievalUnit("BrigandFusilier", "Brigand Fusilier", age, "BrigandMusketeer", 270, 50, 2.5f, 6.2f, 0.9f);
        var marauder = MedievalUnit("MarauderHussar", "Marauder Hussar", age, "Highwayman", 560, 38, 1.0f, 0.9f, 1.7f);
        age.enemyUnits = new List<UnitDefinition> { privateer, fusilier, marauder };
        EditorUtility.SetDirty(age);

        // Smooth the step from the Middle Ages: the Renaissance's gun cavalry and arquebusiers had
        // less punch than the knights and crossbowmen they replaced.
        var cuirassier = AssetDatabase.LoadAssetAtPath<UnitDefinition>("Assets/Data/Units/Cuirassier.asset");
        var arquebusier = AssetDatabase.LoadAssetAtPath<UnitDefinition>("Assets/Data/Units/Arquebusier.asset");
        if (cuirassier != null) { cuirassier.attackPower = 56; EditorUtility.SetDirty(cuirassier); }
        if (arquebusier != null) { arquebusier.attackPower = 54; EditorUtility.SetDirty(arquebusier); }

        // ---------- Buildings ----------
        var townhouse = RankUp(db, "MerchantHouse", "GeorgianTownhouse", "Georgian Townhouse", age, encyclopedie,
            "A tall, flat-fronted brick townhouse with white sash windows. Houses 62 people.",
            Cost(stone, 360, wood, 190, steel, 30), Cost(stone, 180, wood, 90, steel, 20), 820);
        townhouse.housing = 62;
        townhouse.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Garret", description = "+10 people.", requirements = new Requirements { cost = Cost(stone, 150, wood, 110) }, upgradeTime = 25f, extraHousing = 10 },
            new BuildingUpgrade { name = "Mews Behind", description = "+10 people.", requirements = new Requirements { cost = Cost(stone, 130, gold, 30) }, upgradeTime = 25f, extraHousing = 10 },
        };

        var royalBank = RankUp(db, "Bank", "RoyalBank", "Royal Bank", age, banking,
            "A Palladian bank with a giant portico and the royal arms. Turns gold into a great deal of coin.",
            Cost(stone, 350, gold, 120, steel, 30), Cost(stone, 170, gold, 70), 720);
        royalBank.workers = 3;
        royalBank.production = new List<ResourceRate> { Rate(coin, 1.55f) };
        royalBank.inputs = new List<ResourceRate> { Rate(gold, 0.065f) };
        royalBank.upgrades = new List<BuildingUpgrade>
        {
            Upgrade("Banknotes", "+25% coin.", Cost(gold, 110, coin, 150), 1.25f),
            Upgrade("Bullion Vault", "+25% coin.", Cost(gold, 140, steel, 40), 1.25f),
        };

        var musketeerBarracks = RankUp(db, "TercioBarracks", "MusketeerBarracks", "Musketeer Barracks", age, flintlocks,
            "A long Georgian barracks round a parade ground. Trains Musketeers: flintlocks with bayonets fixed.",
            Cost(stone, 360, wood, 220, steel, 90, gunpowder, 40), Cost(stone, 180, steel, 55), 1020);
        Trains(musketeerBarracks, musketeer, 4, steel, gold, food, coin);

        var jagerRange = RankUp(db, "MusketRange", "JagerRange", "Jäger Range", age, lightInfantry,
            "A forester's lodge at the edge of a range. Trains Jägers: rifled carbines with a long, true reach.",
            Cost(wood, 340, stone, 220, steel, 70, gunpowder, 50), Cost(wood, 170, steel, 35), 750);
        Trains(jagerRange, jager, 4, steel, gold, food, coin);

        var hussarStables = RankUp(db, "CuirassierStables", "HussarStables", "Hussar Stables", age, hussars,
            "Elegant stables with arched doors and a cupola. Trains Hussars: fast light horse with the sabre.",
            Cost(stone, 350, wood, 210, steel, 90, food, 350), Cost(stone, 175, steel, 55), 750);
        Trains(hussarStables, hussar, 3, steel, gold, food, coin);

        var surgeonsHall = RankUp(db, "Hospital", "SurgeonsHall", "Surgeons' Hall", age, surgery,
            "A limestone hall with a domed anatomy theatre. Trains Regimental Surgeons.",
            Cost(stone, 390, wood, 200, gold, 70), Cost(stone, 195, gold, 45), 750);
        surgeonsHall.trainsUnit = surgeon;
        surgeonsHall.maxUnits = 3;
        surgeonsHall.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Lying-in Ward", description = "+1 surgeon.", requirements = new Requirements { cost = Cost(food, 280, stone, 140) }, upgradeTime = 25f, extraUnits = 1 },
            new BuildingUpgrade { name = "Anatomy Lessons", description = "Surgeons heal 30% more and are 20% tougher.", requirements = new Requirements { cost = Cost(gold, 90, quicksilver, 30) }, upgradeTime = 30f,
                trainedUnits = new UnitStatModifier { attackMultiplier = 1.3f, healthMultiplier = 1.2f } },
        };

        var casemate = RankUp(db, "CannonBastion", "CasemateBattery", "Casemate Battery", age, starForts,
            "A low star-fort casemate of dressed stone. The gunners work under its vault; the gun swings to meet the raiders and its shot bursts among them.",
            Cost(stone, 420, gunpowder, 70, steel, 60), Cost(stone, 210, gunpowder, 40), 1150);
        casemate.workers = 2;
        casemate.towerRange = 11.5f;
        casemate.towerDamage = 130;
        casemate.towerCooldown = 3.2f;
        casemate.operatorIdle = null;
        casemate.operatorShoot = new List<Sprite>();
        casemate.towerProjectile = AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/Units/Projectiles/Cannonball.png");
        casemate.towerProjectileArc = 0.05f;
        casemate.towerProjectileSpeed = 17f;
        casemate.towerSplash = 1.7f;
        casemate.towerMuzzleSmoke = true;
        casemate.towerMuzzle = new Vector2(62f / TilePx, 19.5f / TilePx);   // the gun's mouth in the casemate art
        casemate.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Ravelin", description = "+1.5 range.", requirements = new Requirements { cost = Cost(stone, 200, gunpowder, 30) }, upgradeTime = 25f, extraTowerRange = 1.5f },
            new BuildingUpgrade { name = "Grapeshot", description = "+40 damage.", requirements = new Requirements { cost = Cost(iron, 100, gunpowder, 50) }, upgradeTime = 25f, extraTowerDamage = 40 },
            new BuildingUpgrade { name = "Glacis", description = "+600 health.", requirements = new Requirements { cost = Cost(stone, 300) }, upgradeTime = 30f, extraHealth = 600 },
        };

        var starWall = RankUp(db, "BastionWall", "StarFortWall", "Star-Fort Wall", age, starForts,
            "A low, angled rampart of dressed limestone.", Cost(stone, 30), Cost(stone, 24), 2700);
        var starGate = RankUp(db, "BastionGate", "StarFortGate", "Star-Fort Gate", age, starForts,
            "A stone gate between angled bastions.", Cost(stone, 60, iron, 14), Cost(stone, 48, iron, 12), 3780);
        starWall.upgrades = new List<BuildingUpgrade>();
        starGate.upgrades = new List<BuildingUpgrade>();

        var blastFurnace = RankUp(db, "Steelworks", "BlastFurnace", "Blast Furnace", age, coke,
            "A tall stone furnace fed with coke, its bellows worked by a water wheel. Half as much steel again as the old works.",
            Cost(stone, 360, iron, 160, steel, 60), Cost(stone, 180, steel, 40), 800);
        blastFurnace.workers = 4;
        blastFurnace.production = new List<ResourceRate> { Rate(steel, 0.15f) };
        blastFurnace.inputs = new List<ResourceRate> { Rate(iron, 0.15f), Rate(coal, 0.08f) };
        blastFurnace.upgrades = new List<BuildingUpgrade>
        {
            Upgrade("Hot Blast", "+30% steel.", Cost(stone, 200, iron, 80), 1.3f),
            Upgrade("Puddling Furnace", "+30% steel.", Cost(steel, 60, coal, 80), 1.3f),
        };

        var artilleryPark = RankUp(db, "CannonFoundry", "ArtilleryPark", "Artillery Park", age, howitzers,
            "A walled gun park and powder magazine. Trains Howitzers, which lob bursting shells.",
            Cost(stone, 420, wood, 220, steel, 140, gunpowder, 80), Cost(stone, 210, steel, 70), 900);
        artilleryPark.trainsUnit = howitzer;
        artilleryPark.maxUnits = 2;
        artilleryPark.workers = 4;
        artilleryPark.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Second Battery", description = "+1 howitzer.", requirements = new Requirements { cost = Cost(steel, 140, gunpowder, 70) }, upgradeTime = 35f, extraUnits = 1 },
            new BuildingUpgrade { name = "Explosive Shells", description = "Howitzers hit 25% harder.", requirements = new Requirements { cost = Cost(gunpowder, 100, tools, 60) }, upgradeTime = 35f,
                trainedUnits = new UnitStatModifier { attackMultiplier = 1.25f } },
        };

        var galvanicLab = RankUp(db, "AlchemistLab", "GalvanicLab", "Galvanic Lab", age, galvanism,
            "A philosopher's tower with copper globes and banks of Leyden jars. Trains Galvanists, who throw bolts of bottled lightning.",
            Cost(stone, 300, quicksilver, 90, gold, 90), Cost(stone, 150, quicksilver, 45), 540);
        galvanicLab.trainsUnit = galvanist;
        galvanicLab.maxUnits = 2;
        galvanicLab.workers = 2;
        galvanicLab.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Bigger Battery", description = "+1 galvanist.", requirements = new Requirements { cost = Cost(quicksilver, 60, gold, 60) }, upgradeTime = 25f, extraUnits = 1 },
            new BuildingUpgrade { name = "Voltaic Pile", description = "Galvanists hit 25% harder.", requirements = new Requirements { cost = Cost(quicksilver, 90, steel, 50) }, upgradeTime = 30f,
                trainedUnits = new UnitStatModifier { attackMultiplier = 1.25f } },
        };

        var coalMine = NewBuilding(db, "IronMine", "CoalMine", "Coal Mine", age, coke, new Vector2Int(3, 3),
            "A pithead and winding gear over a deep shaft. Must be built on or next to Coal (black seams in the hills).",
            Cost(wood, 280, stone, 200, iron, 80), 450);
        coalMine.requiredDeposit = coalDeposit;
        coalMine.canBuildOnHills = true;
        coalMine.workers = 5;
        coalMine.production = new List<ResourceRate> { Rate(coal, 0.25f) };
        coalMine.upgrades = new List<BuildingUpgrade>
        {
            Upgrade("Pumping Engine", "+30% coal.", Cost(iron, 120, wood, 200), 1.3f),
            Upgrade("Second Shaft", "+30% coal.", Cost(stone, 300, iron, 100), 1.3f),
        };

        // ---------- Lines that used to stop dead ----------
        // Pikes don't vanish: the pike yard becomes a pike-and-shot tercio.
        JoinLine(db, "PikeYard", "TercioBarracks", "PikeAndShot", Cost(stone, 160, steel, 50));
        // Magic carries on: runes, then alchemy (then galvanism, then aether).
        JoinLine(db, "RuneShrine", "AlchemistLab", "Transmutation", Cost(quicksilver, 40, gold, 60));

        var all = new[] { townhouse, royalBank, musketeerBarracks, jagerRange, hussarStables, surgeonsHall, casemate, starWall, starGate, blastFurnace, artilleryPark, galvanicLab, coalMine };
        foreach (var b in all) EditorUtility.SetDirty(b);
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();

        // ---------- Art (only what this age adds) ----------
        foreach (var b in all)
            if (!b.IsWall) GenerateBuildingLooks(b);
        GenerateWallArt(6);
        GenerateWarriorsAndCrews(db, new HashSet<string>
            { "Musketeer", "Jager", "Hussar", "RegimentalSurgeon", "Galvanist", "Howitzer", "Privateer", "BrigandFusilier", "MarauderHussar", "Cuirassier", "Arquebusier" });
        SetupBosses("EnlightenmentAge");
        AssetDatabase.SaveAssets();

        // The Industrial Age now grows out of this one: re-link its rank-ups and research (its troops are unchanged).
        SetupIndustrialCore(redrawUnits: false);
        Debug.Log("Age of Enlightenment set up.");
    }

    /// <summary>Make an old building rank up into a later one it never used to lead to.</summary>
    private static void JoinLine(GameDatabase db, string fromId, string toId, string techId, List<ResourceAmount> cost)
    {
        var from = db.GetBuilding(fromId);
        var to = db.GetBuilding(toId);
        var tech = db.GetTechnology(techId);
        if (from == null || to == null) return;
        from.rankUpTo = to;
        from.rankUpRequirements = new Requirements { cost = cost, technologies = tech != null ? new List<Technology> { tech } : new List<Technology>() };
        from.rankUpTime = 40f;
        EditorUtility.SetDirty(from);
    }
}
