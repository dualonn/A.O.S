using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The Interstellar Age: space folded by gravity-magic and warp engines together. Riftblades in phase armour, Gravimancers, riders on star whales, Riftmenders, the hovering Gravity Engine pyramid, Prism Sentinels (the construct's successor) and Fold Cruisers. Rift reavers slip through from between the worlds. Riftstone (with rich veins) is refined with mana into Warp Cells. Fold Theory (Stellar research) opens it; every Stellar line ranks up.
/// Safe to run again. (Tools > Age of Stickman > Setup Interstellar Age.)
/// </summary>
public static partial class PlaceholderArt
{
    [MenuItem("Tools/Age of Stickman/Setup Interstellar Age")]
    public static void SetupInterstellar()
    {
        var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Resources/GameDatabase.asset");
        Age prev = db.ages.Find(a => a != null && a.name == "StellarAge");
        var age = AgeAsset(db, "InterstellarAge", "Interstellar Age", 14,
            "Space folded by gravity-magic and warp engines: phase blades, gravity staffs, star whales, pyramids that bend the ground, and cruisers that skip between stars.");

        ResourceType food = Res(db, "Food"), wood = Res(db, "Wood"), stone = Res(db, "Stone"), iron = Res(db, "Iron"),
            gold = Res(db, "Gold"), coin = Res(db, "Coin"), steel = Res(db, "Steel"), gears = Res(db, "Gears"),
            cells = Res(db, "PowerCells"), chips = Res(db, "Microchips"), mana = Res(db, "Mana"),
            starmetal = Res(db, "Starmetal"), cores = Res(db, "AstralCores"), prevRefined = Res(db, "Sunfire");
        var raw = MedievalResource(db, "Riftstone", "Riftstone", age, "Sunfire");
        var refined = MedievalResource(db, "WarpCells", "Warp Cells", age, "Riftstone");
        var deposit = NewDeposit(db, "Riftstone", "Riftstone", raw, true, 0.72f, 0f, 1f, -1f);
        deposit.richVeins = true;
        EditorUtility.SetDirty(deposit);

        var gate = Tech(db, "FoldTheory", "Fold Theory", prev,
            "Space is a cloth, and cloth can be folded. Opens the way to the Interstellar Age.",
            195f, Cost(food, 4370, gold, 1260, coin, 1840, prevRefined, 290, starmetal, 400), "SolarCollectors");
        gate.requirements.minimumAge = prev;
        gate.requirements.technologies = new List<Technology>();
        EditorUtility.SetDirty(gate);

        Technology T(string id, string name, string description, float time, List<ResourceAmount> cost, string after, params Technology[] needs)
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
        var resTech = T("RiftMining", "Rift Mining", "Stone that is partly somewhere else. Unlocks the Riftstone Quarry and the Warp Cell Plant.",
            150f, Cost(steel, 920, cores, 160, mana, 210), "FoldTheory");
        var blades = T("PhaseBlades", "Phase Blades", "Blades that are only half here, so armour hardly stops them. Paladin Citadels rank up into the Riftblade Hall; all troops hit 4% harder.",
            150f, Cost(steel, 920, refined, 80), resTech.id, resTech);
        blades.unitModifiers = new List<UnitStatModifier> { new UnitStatModifier { attackMultiplier = 1.04f } };
        var ranged = T("Gravimancy", "Gravimancy", "Staffs that pull, crush and fling with gravity. Helion Academies rank up into the Gravity Academy.",
            150f, Cost(gold, 920, refined, 103, chips, 200), blades.id, blades);
        var air = T("WhaleTaming", "Whale Taming", "The great whales that swim between stars, gentled and saddled. Sunwing Aeries rank up into the Leviathan Roost; troops move 4% faster.",
            150f, Cost(cells, 400, refined, 103, mana, 160), ranged.id, resTech);
        air.unitModifiers = new List<UnitStatModifier> { new UnitStatModifier { speedMultiplier = 1.04f } };
        var healing = T("PathMending", "Path Mending", "Wounds folded shut, as if they had never been. Dawn Cathedrals rank up into the Temple of Paths; all troops have 4% more health.",
            140f, Cost(food, 3910, gold, 690, refined, 80), air.id);
        healing.unitModifiers = new List<UnitStatModifier> { new UnitStatModifier { healthMultiplier = 1.04f } };
        var lances = T("PhaseLances", "Phase Lances", "Bolts that skip the space between. Sun Lances rank up into Phase Lances, sunward into phase walls; buildings have 8% more health.",
            155f, Cost(stone, 3910, refined, 180, starmetal, 160), healing.id, ranged);
        lances.buildingHealthMultiplier = 1.08f;
        var longTower = T("GravityLenses", "Gravity Lenses", "A lens of bent space that drops crushing wells on raiders far away. Burning Mirrors rank up into Gravity Lenses.",
            155f, Cost(gold, 1040, refined, 210, chips, 160), lances.id, resTech);
        var splashTower = T("RiftMortars", "Rift Mortars", "Shards of a broken rift, thrown. Flare Cannons rank up into Rift Mortars.",
            155f, Cost(cells, 400, refined, 210, starmetal, 160), longTower.id, resTech);
        var forging = T("FoldForging", "Fold Forging", "Metal folded through space a thousand times. Sun Forges rank up into the Fold Forge; steel comes 10% faster.",
            150f, Cost(steel, 1040, refined, 160), splashTower.id, healing);
        forging.productionModifiers = new List<ProductionModifier> { new ProductionModifier { resource = steel, multiplier = 1.1f } };
        var works = T("PrismWorks", "Prism Works", "Constructs of living crystal at the benches. Radiant Works rank up into Prism Works; everything you produce comes in 5% faster.",
            155f, Cost(gears, 690, refined, 160, cores, 110), forging.id, forging);
        works.productionModifiers = new List<ProductionModifier> { new ProductionModifier { resource = null, multiplier = 1.05f } };
        var heavyTech = T("GravityEngines", "Gravity Engines", "A black pyramid that hovers by bending the ground beneath it, and crushes what it points at. Orb Forges rank up into the Pyramid Forge.",
            165f, Cost(steel, 1260, refined, 250, cells, 250), works.id, ranged);
        var specialTech = T("PrismSentinels", "Prism Sentinels", "A construct of faceted crystal that remembers every blow. Radiant Sanctums rank up into the Prism Vault.",
            170f, Cost(starmetal, 520, refined, 250, chips, 250), heavyTech.id, works);
        var shipTech = T("FoldCruisers", "Fold Cruisers", "Warships that fold space to jump into a fight. Solar Docks rank up into the Fold Yard.",
            165f, Cost(steel, 1490, refined, 250, cores, 160), specialTech.id, air);
        var housingTech = T("RiftSpires", "Rift Spires", "Towers bigger inside than out. Helion Towers rank up into Rift Spires.",
            140f, Cost(stone, 4370, refined, 160, steel, 400), shipTech.id);
        var moneyTech = T("FoldTrade", "Fold Trade", "Goods that arrive before they are sent. Sunmarkets rank up into the Fold Exchange; everyone pays a little more in taxes.",
            140f, Cost(gold, 1490, coin, 1490, refined, 103), housingTech.id);
        moneyTech.coinPerPersonPerSecond = 0.002f;
        var sight = T("RiftSight", "Rift Sight", "Looking through the folds at what is far away, and at what is coming. Research goes 5% faster.",
            150f, Cost(gold, 1260, refined, 160, chips, 210), moneyTech.id);
        sight.researchSpeedMultiplier = 1.05f;
        var powerTech = T("RiftTaps", "Rift Taps", "A tap in a small rift, drawing on the energy between worlds. Solar Collectors rank up into Rift Taps.",
            160f, Cost(stone, 3910, refined, 250, starmetal, 250), sight.id, resTech);

        age.advanceRequirements = new Requirements
        {
            technologies = new List<Technology> { gate },
            cost = Cost(food, 14950, wood, 9200, stone, 12650, iron, 3450, gold, 2070, coin, 2530, steel, 2070, prevRefined, 340, starmetal, 400),
            population = 1200,
        };
        EditorUtility.SetDirty(age);
        var melee = MedievalUnit("Riftblade", "Riftblade", age, "SolarPaladin", 1242, 172, 0.9f, 0.9f, 1.15f);
        var rangedU = MedievalUnit("Gravimancer", "Gravimancer", age, "HelionMagus", 690, 149, 1.6f, 8.5f, 1.0f);
        var flyer = MedievalUnit("WhaleRider", "Whale Rider", age, "Sunwing", 1322, 120, 1.1f, 6.5f, 3.0f);
        flyer.movement = Movement.Air; flyer.flyHeight = 1.0f; flyer.populationCost = 2;
        var healer = MedievalUnit("Riftmender", "Riftmender", age, "Dawnkeeper", 667, 92, 1.0f, 0.8f, 1.1f);
        healer.healer = true; healer.healRange = 18f;
        var heavy = MedievalUnit("GravityEngine", "Gravity Engine", age, "SunOrb", 3334, 345, 3.2f, 11.5f, 0.9f);
        heavy.populationCost = 3; heavy.movement = Movement.Ground; heavy.flyHeight = 0f;
        var special = MedievalUnit("PrismSentinel", "Prism Sentinel", age, "RadiantAutomaton", 3219, 172, 0.95f, 1.2f, 1.05f);
        special.populationCost = 2;
        var ship = MedievalUnit("FoldCruiser", "Fold Cruiser", age, "SolarBarque", 2875, 224, 2.4f, 7.5f, 1.45f);
        ship.populationCost = 3; ship.movement = Movement.Air; ship.flyHeight = 1.9f;
        foreach (var x in new[] { melee, rangedU, flyer, healer })
        {
            x.upkeep = new List<ResourceRate> { Rate(food, 0.016f), Rate(coin, 0.11f) };
            EditorUtility.SetDirty(x);
        }
        foreach (var x in new[] { heavy, special, ship })
        {
            x.upkeep = new List<ResourceRate> { Rate(refined, 0.012f), Rate(coin, 0.11f) };
            EditorUtility.SetDirty(x);
        }
        age.playerUnits = new List<UnitDefinition> { melee, rangedU, flyer, healer, heavy, special, ship };
        var eMelee = MedievalUnit("RiftReaver", "Rift Reaver", age, "EclipseZealot", 1380, 138, 1.0f, 0.9f, 1.2f);
        var eRanged = MedievalUnit("RiftWitch", "Rift Witch", age, "EclipseSeer", 713, 126, 1.7f, 8f, 1.0f);
        var eAir = MedievalUnit("VoidRayRider", "Void Ray Rider", age, "ShadeGlider", 1150, 103, 1.3f, 6.5f, 2.8f);
        eAir.movement = Movement.Air; eAir.flyHeight = 1.0f;
        foreach (var x in new[] { eMelee, eRanged, eAir }) EditorUtility.SetDirty(x);
        age.enemyUnits = new List<UnitDefinition> { eMelee, eRanged, eAir };
        EditorUtility.SetDirty(age);
        var phaseBolt = PixelProjectile("PhaseBolt", 12, 7, c =>
        {
            var p = PixelPalette.Phase;
            c.LineRaw(new Vector2(0, 3), new Vector2(3, 3), p[1]); c.LineRaw(new Vector2(5, 3), new Vector2(7, 3), p[2]);
            c.Ellipse(new Vector2(9, 3.5f), 2.3f, 2.3f, new[] { p[1], p[2], p[3], PixelPalette.White });
        });
        var well = PixelProjectile("GravityWell", 14, 14, c =>
        {
            var p = PixelPalette.Phase;
            c.Ellipse(new Vector2(7, 7), 6f, 6f, new[] { p[2], p[1], p[0], p[0] });
            c.Ellipse(new Vector2(7, 7), 2.5f, 2.5f, new[] { PixelPalette.Coal[0], PixelPalette.Coal[0], PixelPalette.Coal[0], PixelPalette.Coal[0] });
            c.Put(1, 7, p[3]); c.Put(12, 7, p[3]);
        });
        var shard = PixelProjectile("RiftShard", 12, 10, c =>
        {
            var r = PixelPalette.Rift;
            c.Polygon(new[] { new Vector2(1, 5), new Vector2(7, 9), new Vector2(11, 5), new Vector2(7, 1) }, l => l.x < 6 ? r[2] : r[3]);
            c.Put(7, 5, PixelPalette.White);
        });
        void Crewless(BuildingDefinition t, float range, int damage, float cooldown, Sprite shot, float arc, float speed, float splash, float mx, float my)
        {
            t.towerRange = range; t.towerDamage = damage; t.towerCooldown = cooldown;
            t.operatorIdle = null; t.operatorShoot = new List<Sprite>();
            t.towerProjectile = shot; t.towerProjectileArc = arc; t.towerProjectileSpeed = speed;
            t.towerSplash = splash; t.towerMuzzleSmoke = false;
            t.towerMuzzle = new Vector2(mx / TilePx, my / TilePx);
            t.workers = 2;
        }
        var house = RankUp(db, "HelionTower", "RiftSpire", "Rift Spire", age, housingTech,
            "A slender spire much bigger inside than out, its windows opening onto other skies. Houses 240 people.",
            Cost(stone, 1150, refined, 69, steel, 250), Cost(stone, 580, refined, 34), 2357);
        house.housing = 240;
        house.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Folded Halls", description = "+35 people.", requirements = new Requirements { cost = Cost(stone, 800, refined, 46) }, upgradeTime = 25f, extraHousing = 35 },
            new BuildingUpgrade { name = "Pocket Districts", description = "+40 people.", requirements = new Requirements { cost = Cost(refined, 92, gold, 250) }, upgradeTime = 30f, extraHousing = 40 },
        };
        var bank = RankUp(db, "Sunmarket", "FoldExchange", "Fold Exchange", age, moneyTech,
            "A hall where crates arrive through the air before they are sent. Coin from trade between worlds.",
            Cost(stone, 1040, gold, 520, refined, 46), Cost(stone, 520, gold, 250), 1897);
        bank.workers = 3;
        bank.production = new List<ResourceRate> { Rate(coin, 7.4f) };
        bank.inputs = new List<ResourceRate> { Rate(gold, 0.1f) };
        bank.upgrades = new List<BuildingUpgrade>
        {
            Upgrade("Early Arrivals", "+25% coin.", Cost(refined, 57, coin, 800), 1.25f),
            Upgrade("Seven-World Ledger", "+25% coin.", Cost(refined, 92, gold, 400), 1.25f),
        };
        var meleeHall = RankUp(db, "PaladinCitadel", "RiftbladeHall", "Riftblade Hall", age, blades,
            "A hall whose doorway shows a different room every time. Trains Riftblades.",
            Cost(stone, 1040, steel, 380, refined, 57), Cost(stone, 520, refined, 29), 2300);
        Trains(meleeHall, melee, 4, steel, gold, food, coin);
        var rangedHall = RankUp(db, "HelionAcademy", "GravityAcademy", "Gravity Academy", age, ranged,
            "A tower with stones floating round it in slow orbits. Trains Gravimancers.",
            Cost(stone, 1040, steel, 380, refined, 57), Cost(stone, 520, refined, 29), 1782);
        Trains(rangedHall, rangedU, 4, steel, gold, food, coin);
        var airHall = RankUp(db, "SunwingAerie", "LeviathanRoost", "Leviathan Roost", age, air,
            "A great frame of rings where star whales rest between flights. Raises Whale Riders: flyers that strike from the sky.",
            Cost(stone, 1040, steel, 380, refined, 57), Cost(stone, 520, refined, 29), 1782);
        Trains(airHall, flyer, 3, steel, gold, food, coin);
        var temple = RankUp(db, "DawnCathedral", "TempleOfPaths", "Temple of Paths", age, healing,
            "A temple of endless corridors folded into one room. Trains Riftmenders.",
            Cost(stone, 1040, gold, 320, refined, 46), Cost(stone, 520, refined, 23), 1782);
        temple.trainsUnit = healer;
        temple.maxUnits = 3;
        temple.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Second Path", description = "+1 riftmender.", requirements = new Requirements { cost = Cost(refined, 46, gold, 180) }, upgradeTime = 25f, extraUnits = 1 },
            new BuildingUpgrade { name = "Unwound Wounds", description = "Riftmenders heal 30% more and are 20% tougher.", requirements = new Requirements { cost = Cost(refined, 92, gold, 250) }, upgradeTime = 30f,
                trainedUnits = new UnitStatModifier { attackMultiplier = 1.3f, healthMultiplier = 1.2f } },
        };
        var lance = RankUp(db, "SunLance", "PhaseLance", "Phase Lance", age, lances,
            "A spike of teal crystal that flickers in and out of the world as it fires.",
            Cost(stone, 750, refined, 80, starmetal, 34), Cost(stone, 380, refined, 40), 3219);
        Crewless(lance, 12.5f, 89, 0.4f, phaseBolt, 0f, 34f, 0.9f, 32f, 54f);
        lance.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Longer Reach", description = "+1.5 range.", requirements = new Requirements { cost = Cost(refined, 69, stone, 380) }, upgradeTime = 25f, extraTowerRange = 1.5f },
            new BuildingUpgrade { name = "Harder Strikes", description = "+22 damage.", requirements = new Requirements { cost = Cost(refined, 92, cores, 34) }, upgradeTime = 25f, extraTowerDamage = 22 },
        };
        var longT = RankUp(db, "BurningMirror", "GravityLens", "Gravity Lens", age, longTower,
            "A ring of bent space on a black pillar. It drops crushing wells on raiders from very far away.",
            Cost(stone, 860, refined, 140, gold, 250), Cost(stone, 440, refined, 69), 2242);
        Crewless(longT, 24f, 598, 2.8f, well, 0.15f, 22f, 1.5f, 32f, 52f);
        longT.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Further Still", description = "+2 range.", requirements = new Requirements { cost = Cost(refined, 110, gold, 180) }, upgradeTime = 25f, extraTowerRange = 2f },
            new BuildingUpgrade { name = "Heavier Blows", description = "+160 damage.", requirements = new Requirements { cost = Cost(refined, 160, cores, 69) }, upgradeTime = 30f, extraTowerDamage = 160 },
        };
        var splashT = RankUp(db, "FlareCannon", "RiftMortar", "Rift Mortar", age, splashTower,
            "A cracked dome holding a small, angry rift. Each shot is a shard of it that bursts wide.",
            Cost(stone, 980, refined, 110, cells, 180), Cost(stone, 480, refined, 57), 3679);
        Crewless(splashT, 16f, 575, 3.1f, shard, 0.2f, 16f, 3.1f, 32f, 42f);
        splashT.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Longer Throw", description = "+2 range.", requirements = new Requirements { cost = Cost(refined, 110, cells, 150) }, upgradeTime = 25f, extraTowerRange = 2f },
            new BuildingUpgrade { name = "Wider Bursts", description = "+160 damage.", requirements = new Requirements { cost = Cost(refined, 160, cores, 69) }, upgradeTime = 30f, extraTowerDamage = 160 },
        };
        var wall = RankUp(db, "SunwardWall", "PhaseWall", "Phase Wall", age, lances,
            "A shimmering wall of teal light that is only half in this world, and stops whatever is wholly in it.",
            Cost(stone, 18, refined, 2), Cost(stone, 15, refined, 2), 7819);
        var gateB = RankUp(db, "SunwardGate", "PhaseGate", "Phase Gate", age, lances,
            "A gate that friends simply walk through.",
            Cost(stone, 34, refined, 6), Cost(stone, 30, refined, 4), 10350);
        wall.upgrades = new List<BuildingUpgrade>();
        gateB.upgrades = new List<BuildingUpgrade>();
        var forge = RankUp(db, "SunForge", "FoldForge", "Fold Forge", age, forging,
            "A forge that folds the metal through space a thousand times. Steel without end.",
            Cost(stone, 860, steel, 520, refined, 69), Cost(stone, 440, refined, 34), 2185);
        forge.workers = 5;
        forge.production = new List<ResourceRate> { Rate(steel, 1.32f) };
        forge.inputs = new List<ResourceRate> { Rate(iron, 0.48f), Rate(refined, 0.012f) };
        forge.upgrades = new List<BuildingUpgrade>
        {
            Upgrade("Thousand Folds", "+30% steel.", Cost(refined, 92, stone, 520), 1.3f),
            Upgrade("Rift Crucibles", "+30% steel.", Cost(steel, 520, cores, 46), 1.3f),
        };
        var worksB = RankUp(db, "RadiantWorks", "PrismWorks", "Prism Works", age, works,
            "A crystal hall where faceted constructs turn out gears. Gears without end.",
            Cost(stone, 750, gears, 320, refined, 46), Cost(stone, 380, refined, 23), 1897);
        worksB.workers = 4;
        worksB.production = new List<ResourceRate> { Rate(gears, 0.80f) };
        worksB.inputs = new List<ResourceRate> { Rate(steel, 0.2f), Rate(refined, 0.008f) };
        worksB.upgrades = new List<BuildingUpgrade>
        {
            Upgrade("More Facets", "+30% gears.", Cost(refined, 69, gears, 180), 1.3f),
            Upgrade("Endless Shifts", "+30% gears.", Cost(steel, 380, cores, 46), 1.3f),
        };
        var heavyHall = RankUp(db, "OrbForge", "PyramidForge", "Pyramid Forge", age, heavyTech,
            "A black pyramid hangar where gravity engines are raised. Builds Gravity Engines: hovering pyramids that crush whole raids.",
            Cost(stone, 1040, steel, 750, refined, 130), Cost(stone, 520, refined, 63), 2760);
        heavyHall.trainsUnit = heavy;
        heavyHall.maxUnits = 2;
        heavyHall.workers = 6;
        heavyHall.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Second Cradle", description = "+1 gravity engine.", requirements = new Requirements { cost = Cost(steel, 630, refined, 103) }, upgradeTime = 35f, extraUnits = 1 },
            new BuildingUpgrade { name = "Denser Stone", description = "Gravity Engines are 25% tougher.", requirements = new Requirements { cost = Cost(refined, 150, starmetal, 150) }, upgradeTime = 35f,
                trainedUnits = new UnitStatModifier { healthMultiplier = 1.25f } },
        };
        var specialHall = RankUp(db, "RadiantSanctum", "PrismVault", "Prism Vault", age, specialTech,
            "A vault of crystal where sentinels grow facet by facet.",
            Cost(stone, 1040, steel, 750, refined, 130), Cost(stone, 520, refined, 63), 2760);
        specialHall.trainsUnit = special;
        specialHall.maxUnits = 4;
        specialHall.workers = 4;
        specialHall.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Second Vault", description = "+1 prism sentinel.", requirements = new Requirements { cost = Cost(steel, 630, refined, 103) }, upgradeTime = 35f, extraUnits = 1 },
            new BuildingUpgrade { name = "Sharper Facets", description = "Prism Sentinels hit 25% harder and are 20% tougher.", requirements = new Requirements { cost = Cost(refined, 150, starmetal, 150) }, upgradeTime = 35f,
                trainedUnits = new UnitStatModifier { attackMultiplier = 1.25f, healthMultiplier = 1.2f } },
        };
        var shipHall = RankUp(db, "SolarDock", "FoldYard", "Fold Yard", age, shipTech,
            "A yard of rings where cruisers jump in and out of folded space. Builds Fold Cruisers, and launches ships to other worlds (open the star map).",
            Cost(stone, 1040, steel, 750, refined, 130), Cost(stone, 520, refined, 63), 2760);
        shipHall.trainsUnit = ship;
        shipHall.maxUnits = 2;
        shipHall.workers = 4;
        shipHall.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Second Ring", description = "+1 fold cruiser.", requirements = new Requirements { cost = Cost(steel, 630, refined, 103) }, upgradeTime = 35f, extraUnits = 1 },
            new BuildingUpgrade { name = "Phase Hulls", description = "Fold Cruisers are 25% tougher.", requirements = new Requirements { cost = Cost(refined, 150, starmetal, 150) }, upgradeTime = 35f,
                trainedUnits = new UnitStatModifier { healthMultiplier = 1.25f } },
        };
        shipHall.launchesShips = true;
        var power = RankUp(db, "SolarCollector", "RiftTap", "Rift Tap", age, powerTech,
            "A ring of pylons round a small rift, drawing power from between worlds. Power cells without end.",
            Cost(stone, 1720, refined, 180, steel, 630), Cost(stone, 860, refined, 92), 2530);
        power.workers = 5;
        power.production = new List<ResourceRate> { Rate(cells, 1.03f) };
        power.inputs = new List<ResourceRate> { Rate(mana, 0.02f) };
        power.upgrades = new List<BuildingUpgrade>
        {
            Upgrade("Wider Rift", "+30% power cells.", Cost(stone, 1040, refined, 92), 1.3f),
            Upgrade("Steady Folds", "+30% power cells.", Cost(refined, 150, cores, 69), 1.3f),
        };
        var mine = NewBuilding(db, "HeliolitePit", "RiftstoneQuarry", "Riftstone Quarry", age, resTech, new Vector2Int(3, 3),
            "A quarry where the stone flickers half out of the world. Must be built on or next to Riftstone, or right on a rich riftstone vein.",
            Cost(steel, 460, stone, 630, mana, 80), 1150);
        mine.requiredDeposit = deposit;
        mine.canBuildOnHills = true;
        mine.workers = 5;
        mine.production = new List<ResourceRate> { Rate(raw, 0.13f) };
        mine.upgrades = new List<BuildingUpgrade>
        {
            Upgrade("Phase Picks", "+30% riftstone.", Cost(steel, 380, mana, 80), 1.3f),
            Upgrade("Fold Survey", "+30% riftstone.", Cost(chips, 130, gold, 250), 1.3f),
        };
        var refinery = NewBuilding(db, "SunfireRefinery", "WarpCellPlant", "Warp Cell Plant", age, resTech, new Vector2Int(3, 3),
            "Riftstone is folded with mana into Warp Cells: a pocket of bent space in a can.",
            Cost(stone, 1040, steel, 380, gold, 250), 1494);
        refinery.workers = 5;
        refinery.production = new List<ResourceRate> { Rate(refined, 0.09f) };
        refinery.inputs = new List<ResourceRate> { Rate(raw, 0.1f), Rate(mana, 0.03f) };
        refinery.runsWithoutInputs = 0f;
        refinery.upgrades = new List<BuildingUpgrade>
        {
            Upgrade("Tighter Folds", "+30% warp cells.", Cost(stone, 750, gold, 250), 1.3f),
            Upgrade("Mana Seals", "+30% warp cells.", Cost(mana, 130, chips, 103), 1.3f),
        };

        var all = new[] { house, bank, meleeHall, rangedHall, airHall, temple, lance, longT, splashT, wall, gateB, forge, worksB, heavyHall, specialHall, shipHall, power, mine, refinery };
        foreach (var x in all) EditorUtility.SetDirty(x);
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();

        deposit.sprites = PixelDeposits("Riftstone", 3, Ore.Riftstone);
        EditorUtility.SetDirty(deposit);
        foreach (var x in all)
            if (!x.IsWall) GenerateBuildingLooks(x);
        GenerateWallArt(14);
        GenerateWarriorsAndCrews(db, new HashSet<string>
            { "Riftblade", "Gravimancer", "WhaleRider", "Riftmender", "GravityEngine", "PrismSentinel", "FoldCruiser", "RiftReaver", "RiftWitch", "VoidRayRider" });
        lance.towerProjectile = AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/Units/Projectiles/PhaseBolt.png");
        EditorUtility.SetDirty(lance);
        SetupBosses("InterstellarAge");
        GenerateIconArt();
        SetupRichVeins();
        AssetDatabase.SaveAssets();
        Debug.Log("Interstellar Age set up.");
    }
}
