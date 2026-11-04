using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Kind = WarCatalog.SiegeKind;
using K = BuildingKit;

/// <summary>
/// Deeper warfare: siege engines for both sides in every era (rams to reality breakers), the
/// siege workshop line that trains yours (one building ranking up through the ages), the
/// siegecraft research that unlocks each tier, a Siege troop class, the army doctrines (small
/// army-wide research bonuses), and Resources/WarDatabase listing the engines by tier.
/// Draws only the art it adds. Safe to run again. (Tools > Age of Stickman > Setup Warfare.)
/// </summary>
public static partial class PlaceholderArt
{
    private class WarTierSpec
    {
        public string age, tech, techName, techText, building, buildingName, buildingText, unit, unitName, raider, raiderName;
        public Kind kind;
        public int hp, atk, pop = 2, buildingHp = 900;
        public float cd, range, speed, mult, arc, shotSpeed = 16f, splash, hitDelay = 0.6f;
        public string shot;
        public bool smoke, fire, ramOnly, sky;
        public WeaponClass weapon;
        public object[] techCost, buildingCost, rankCost;
        public string[] upkeep;   // resource ids, food first
    }

    private static List<WarTierSpec> WarTiers => new List<WarTierSpec>
    {
        new WarTierSpec { age = "BronzeAge", tech = "Siegecraft", techName = "Siegecraft",
            techText = "A log slung under a roof of hides, pushed on wheels against a gate. Unlocks the Ram Shed: covered rams that batter raider camps.",
            building = "RamShed", buildingName = "Ram Shed", buildingText = "A yard and a hide-roofed shed where covered rams are built. Rams don't fight troops: they batter camps (6x damage).",
            unit = "CoveredRam", unitName = "Covered Ram", raider = "RaiderRam", raiderName = "Raider Ram", kind = Kind.Ram,
            hp = 520, atk = 18, cd = 2.6f, range = 0.9f, speed = 0.5f, mult = 6f, ramOnly = true, weapon = WeaponClass.Melee, hitDelay = 0.7f,
            techCost = new object[] { "Wood", 300, "Stone", 150, "Bronze", 40 }, buildingCost = new object[] { "Wood", 260, "Stone", 80 }, upkeep = new[] { "Food" } },
        new WarTierSpec { age = "ClassicalAge", tech = "TorsionEngines", techName = "Torsion Engines",
            techText = "Twisted sinew stores enormous force: the onager hurls stones over walls. Ram Sheds rank up into the Siege Workshop.",
            building = "SiegeWorkshop", buildingName = "Siege Workshop", buildingText = "Carpenters and rope-makers build onagers here: they lob stones at camps (4x damage) and at raiders bunched together.",
            unit = "Onager", unitName = "Onager", raider = "RaiderOnager", raiderName = "Raider Onager", kind = Kind.Onager,
            hp = 600, atk = 42, cd = 4.6f, range = 8f, speed = 0.5f, mult = 4f, shot = "SiegeStone", arc = 0.35f, shotSpeed = 11f, splash = 1.1f,
            weapon = WeaponClass.Thrown, hitDelay = 0.7f,
            techCost = new object[] { "Wood", 400, "Iron", 120, "Coin", 80 }, buildingCost = new object[] { "Wood", 360, "Iron", 80 }, rankCost = new object[] { "Wood", 200, "Iron", 60 }, upkeep = new[] { "Food", "Coin" } },
        new WarTierSpec { age = "MedievalAge", tech = "Counterweights", techName = "Counterweights",
            techText = "A box of stones drops and a long arm flings a boulder further than any onager. The workshop ranks up into the Siege Yard.",
            building = "SiegeYard", buildingName = "Siege Yard", buildingText = "A timber yard with a crane where trebuchets are raised. They throw boulders at camps from far off (4x damage).",
            unit = "Trebuchet", unitName = "Trebuchet", raider = "RaiderTrebuchet", raiderName = "Raider Trebuchet", kind = Kind.Trebuchet,
            hp = 900, atk = 60, cd = 6f, range = 10f, speed = 0.4f, mult = 4.5f, shot = "SiegeBoulder", arc = 0.45f, shotSpeed = 10f, splash = 1.4f,
            weapon = WeaponClass.Thrown, hitDelay = 0.8f, pop = 3,
            techCost = new object[] { "Wood", 500, "Iron", 200, "Stone", 300 }, buildingCost = new object[] { "Wood", 500, "Stone", 200 }, rankCost = new object[] { "Wood", 300, "Iron", 100 }, upkeep = new[] { "Food", "Coin" } },
        new WarTierSpec { age = "RenaissanceAge", tech = "GreatGuns", techName = "Great Guns",
            techText = "Bronze cast as thick as a man's arm: the great bombard breaks walls in an afternoon. The yard ranks up into the Bombard Foundry.",
            building = "BombardFoundry", buildingName = "Bombard Foundry", buildingText = "A casting pit and a boring mill for great bombards: huge guns that smash camps (4x damage).",
            unit = "GreatBombard", unitName = "Great Bombard", raider = "RaiderBombard", raiderName = "Raider Bombard", kind = Kind.Bombard,
            hp = 1100, atk = 90, cd = 5.5f, range = 9f, speed = 0.42f, mult = 4f, shot = "Cannonball", arc = 0.08f, shotSpeed = 16f, splash = 1.5f, smoke = true,
            weapon = WeaponClass.Cannon, pop = 3,
            techCost = new object[] { "Iron", 250, "Gunpowder", 80, "Coin", 150 }, buildingCost = new object[] { "Stone", 400, "Iron", 200 }, rankCost = new object[] { "Iron", 150, "Gunpowder", 40 }, upkeep = new[] { "Food", "Coin" } },
        new WarTierSpec { age = "IndustrialAge", tech = "ArmouredTraction", techName = "Armoured Traction",
            techText = "A boiler, caterpillar tracks and riveted plate: a siege gun that drives itself. The foundry ranks up into the Siege Works.",
            building = "SiegeWorks", buildingName = "Siege Works", buildingText = "A riveting shed and a test track for steam siege tanks: armoured, slow, and hard on camps (4x damage).",
            unit = "SteamSiegeTank", unitName = "Steam Siege Tank", raider = "RaiderSteamTank", raiderName = "Raider Steam Tank", kind = Kind.SteamTank,
            hp = 2200, atk = 110, cd = 4.2f, range = 8f, speed = 0.6f, mult = 4f, shot = "Cannonball", arc = 0.05f, shotSpeed = 18f, splash = 1.4f, smoke = true,
            weapon = WeaponClass.Cannon, pop = 3,
            techCost = new object[] { "Steel", 250, "Gears", 80, "Coal", 150 }, buildingCost = new object[] { "Steel", 260, "Stone", 300 }, rankCost = new object[] { "Steel", 150, "Gears", 40 }, upkeep = new[] { "Food", "Coal" } },
        new WarTierSpec { age = "ModernAge", tech = "HeavyArtillery", techName = "Heavy Artillery",
            techText = "Split trails, recoil cylinders and range tables: guns that shell what they can't see. The works rank up into the Heavy Gun Park.",
            building = "HeavyGunPark", buildingName = "Heavy Gun Park", buildingText = "Sandbagged gun pits and an ammunition dump. Siege howitzers shell camps from far away (4x damage).",
            unit = "SiegeHowitzer", unitName = "Siege Howitzer", raider = "RaiderHowitzer", raiderName = "Raider Howitzer", kind = Kind.Howitzer,
            hp = 1500, atk = 150, cd = 6f, range = 12f, speed = 0.5f, mult = 4f, shot = "Cannonball", arc = 0.25f, shotSpeed = 18f, splash = 1.8f, smoke = true,
            weapon = WeaponClass.Cannon, pop = 3,
            techCost = new object[] { "Steel", 300, "Fuel", 150, "Gunpowder", 120 }, buildingCost = new object[] { "Steel", 300, "Stone", 300 }, rankCost = new object[] { "Steel", 160, "Gunpowder", 60 }, upkeep = new[] { "Food", "Fuel" } },
        new WarTierSpec { age = "InformationAge", tech = "GuidedRockets", techName = "Guided Rockets",
            techText = "A truck, a rack of rockets and a computer to steer them. The gun park ranks up into the Rocket Battery.",
            building = "RocketBattery", buildingName = "Rocket Battery", buildingText = "A motor pool and a control bunker for missile carriers: they ripple rockets onto camps (4x damage).",
            unit = "MissileCarrier", unitName = "Missile Carrier", raider = "RaiderMissileCarrier", raiderName = "Raider Missile Truck", kind = Kind.Launcher,
            hp = 2200, atk = 220, cd = 6.5f, range = 13f, speed = 0.8f, mult = 4f, shot = "SiegeRocket", arc = 0.25f, shotSpeed = 15f, splash = 2f, fire = true,
            weapon = WeaponClass.Cannon, pop = 3,
            techCost = new object[] { "Steel", 300, "Microchips", 60, "Fuel", 150 }, buildingCost = new object[] { "Steel", 320, "Microchips", 30 }, rankCost = new object[] { "Steel", 160, "Microchips", 20 }, upkeep = new[] { "Food", "Fuel" } },
        new WarTierSpec { age = "ArcaneAge", tech = "SiegeGolems", techName = "Siege Golems",
            techText = "Runes cut deep into a mountain of stone: it walks, and its fists break anything. The battery ranks up into the Golemworks.",
            building = "Golemworks", buildingName = "Golemworks", buildingText = "A rune-lit quarry where colossi are carved and woken. They stride into camps and pound them (5x damage), and fight what gets in the way.",
            unit = "RuneColossus", unitName = "Rune Colossus", raider = "RaiderColossus", raiderName = "Rogue Colossus", kind = Kind.Colossus,
            hp = 4200, atk = 260, cd = 2.8f, range = 1.1f, speed = 0.5f, mult = 5f, weapon = WeaponClass.Melee, hitDelay = 0.7f, pop = 4,
            techCost = new object[] { "Runestone", 120, "Manastone", 80, "Stone", 600 }, buildingCost = new object[] { "Stone", 600, "Runestone", 60 }, rankCost = new object[] { "Runestone", 60, "Manastone", 40 }, upkeep = new[] { "Food", "Mana" } },
        new WarTierSpec { age = "StellarAge", tech = "WarMechs", techName = "War Mechs",
            techText = "Two legs, a sun-fed reactor and a shoulder cannon. The Golemworks rank up into the Mech Bay.",
            building = "MechBay", buildingName = "Mech Bay", buildingText = "A gantry and a reactor bay for siege mechs: they stride over rough ground and blast camps (4x damage).",
            unit = "SiegeMech", unitName = "Siege Mech", raider = "RaiderMech", raiderName = "Raider Mech", kind = Kind.Mech,
            hp = 4800, atk = 380, cd = 4f, range = 9f, speed = 0.6f, mult = 4f, shot = "SiegePlasma", arc = 0f, shotSpeed = 26f, splash = 1.6f,
            weapon = WeaponClass.Cannon, pop = 4,
            techCost = new object[] { "Starmetal", 150, "Sunfire", 60, "Steel", 300 }, buildingCost = new object[] { "Starmetal", 160, "Steel", 300 }, rankCost = new object[] { "Starmetal", 80, "Sunfire", 30 }, upkeep = new[] { "Food", "Sunfire" } },
        new WarTierSpec { age = "GalacticAge", tech = "OrbitalBombardment", techName = "Orbital Bombardment",
            techText = "A beacon on the ground, a warship in orbit: the strike comes down from the sky. The Mech Bay ranks up into the Strike Uplink.",
            building = "StrikeUplink", buildingName = "Strike Uplink", buildingText = "A dish array that links to the fleet overhead. Its orbital beacons call strikes down on camps (5x damage).",
            unit = "OrbitalBeacon", unitName = "Orbital Beacon", raider = "RaiderBeacon", raiderName = "Raider Strike Beacon", kind = Kind.Beacon,
            hp = 3600, atk = 600, cd = 8f, range = 14f, speed = 0.5f, mult = 5f, shot = "OrbitalLance", arc = 0f, shotSpeed = 26f, splash = 2.2f, fire = true, sky = true,
            weapon = WeaponClass.Staff, pop = 4,
            techCost = new object[] { "WarpCells", 80, "Riftstone", 80, "Starmetal", 200 }, buildingCost = new object[] { "Starmetal", 240, "Riftstone", 40 }, rankCost = new object[] { "WarpCells", 40, "Riftstone", 40 }, upkeep = new[] { "Food", "WarpCells" } },
        new WarTierSpec { age = "UniversalAge", tech = "Unmaking", techName = "Unmaking",
            techText = "What was made can be unmade: an engine that unwrites matter. The uplink ranks up into the Unmaking Engine.",
            building = "UnmakingEngine", buildingName = "Unmaking Engine", buildingText = "A ring of pale stone round a fold in the world. Reality breakers float out of it and erase camps (5x damage).",
            unit = "RealityBreaker", unitName = "Reality Breaker", raider = "RaiderBreaker", raiderName = "Unmade Breaker", kind = Kind.Breaker,
            hp = 7000, atk = 800, cd = 7f, range = 12f, speed = 0.45f, mult = 5f, shot = "UnmakingBolt", arc = 0f, shotSpeed = 30f, splash = 2.2f,
            weapon = WeaponClass.Staff, pop = 4,
            techCost = new object[] { "Primordium", 60, "GenesisMatter", 40, "Quintessence", 60 }, buildingCost = new object[] { "Primordium", 50, "Quintessence", 40 }, rankCost = new object[] { "Primordium", 30, "GenesisMatter", 20 }, upkeep = new[] { "Food", "Quintessence" } },
    };

    [MenuItem("Tools/Age of Stickman/Setup Warfare")]
    public static void SetupWarfare()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Stop playing first: Setup Warfare changes assets."); return; }
        var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Resources/GameDatabase.asset");
        if (db == null) { Debug.LogError("No GameDatabase."); return; }
        Age AgeNamed(string n) => db.ages.Find(a => a != null && a.name == n);
        List<ResourceAmount> WCostOf(object[] pairs)
        {
            var list = new List<ResourceAmount>();
            if (pairs == null) return list;
            for (int i = 0; i + 1 < pairs.Length; i += 2)
            {
                var r = Res(db, (string)pairs[i]);
                if (r != null) list.Add(new ResourceAmount { resource = r, amount = (int)pairs[i + 1] });
            }
            return list;
        }

        // ---------- The Siege troop class ----------
        Directory.CreateDirectory($"{DataRoot}/TroopClasses");
        string clsPath = $"{DataRoot}/TroopClasses/Siege.asset";
        var siegeClass = AssetDatabase.LoadAssetAtPath<TroopClass>(clsPath);
        if (siegeClass == null) { siegeClass = ScriptableObject.CreateInstance<TroopClass>(); AssetDatabase.CreateAsset(siegeClass, clsPath); }
        siegeClass.displayName = "Siege";
        siegeClass.description = "Engines that batter walls, towers and camps far harder than they fight troops.";
        EditorUtility.SetDirty(siegeClass);

        // ---------- Shots ----------
        PixelProjectile("SiegeStone", 8, 8, c => { c.Ellipse(new Vector2(4, 4), 3.2f, 3f, PixelPalette.Stone); c.Put(3, 5, PixelPalette.Stone[4]); });
        PixelProjectile("SiegeBoulder", 11, 10, c => { c.Ellipse(new Vector2(5.5f, 5), 4.6f, 4.2f, PixelPalette.Rock); c.Put(4, 7, PixelPalette.Rock[4]); c.Put(7, 3, PixelPalette.Rock[0]); });
        PixelProjectile("SiegeRocket", 14, 5, c =>
        {
            c.LineRaw(new Vector2(3, 2), new Vector2(11, 2), PixelPalette.Chrome[2]); c.LineRaw(new Vector2(4, 3), new Vector2(10, 3), PixelPalette.Chrome[3]);
            c.Put(12, 2, PixelPalette.Red[2]); c.Put(3, 1, PixelPalette.Iron[1]); c.Put(3, 3, PixelPalette.Iron[1]);
            c.Put(1, 2, PixelPalette.Fire[3]); c.Put(2, 2, PixelPalette.Fire[2]); c.Put(0, 2, PixelPalette.Fire[1]);
        });
        PixelProjectile("SiegePlasma", 12, 7, c =>
        {
            var s = PixelPalette.Solar;
            c.LineRaw(new Vector2(0, 3), new Vector2(6, 3), s[1]); c.Ellipse(new Vector2(8, 3.5f), 2.6f, 2.4f, s); c.Put(8, 4, PixelPalette.White);
        });
        PixelProjectile("OrbitalLance", 20, 7, c =>
        {
            var r = PixelPalette.Rift;
            c.LineRaw(new Vector2(0, 3), new Vector2(15, 3), r[2], 2); c.LineRaw(new Vector2(4, 3), new Vector2(17, 3), r[3]);
            c.Ellipse(new Vector2(17, 3.5f), 2.4f, 2.6f, new[] { r[1], r[2], r[3], PixelPalette.White });
        });
        PixelProjectile("UnmakingBolt", 13, 7, c =>
        {
            var e = PixelPalette.Prism;
            c.LineRaw(new Vector2(0, 3), new Vector2(7, 3), PixelPalette.Pearl[2]); c.Ellipse(new Vector2(9, 3.5f), 2.6f, 2.6f, new[] { e[3], e[2], e[0], PixelPalette.White });
        });
        AssetDatabase.SaveAssets();

        // ---------- Research, units, the workshop line ----------
        string warDbPath = "Assets/Resources/WarDatabase.asset";
        var warDb = AssetDatabase.LoadAssetAtPath<WarDatabase>(warDbPath);
        if (warDb == null) { warDb = ScriptableObject.CreateInstance<WarDatabase>(); AssetDatabase.CreateAsset(warDb, warDbPath); }
        warDb.siege = new List<WarDatabase.SiegeTier>();

        Technology prevTech = db.GetTechnology("BronzeWorking");
        string prevBuilding = null;
        var made = new List<(WarTierSpec spec, BuildingDefinition building)>();
        foreach (var t in WarTiers)
        {
            var age = AgeNamed(t.age);
            if (age == null) { Debug.LogWarning($"Setup Warfare: no age {t.age}; skipping {t.unitName}."); continue; }
            string after = db.technologies.FindLast(x => x != null && x.age == age && !x.IsLevelled)?.id;
            var tech = Tech(db, t.tech, t.techName, age, t.techText, 60f + 8f * age.order, WCostOf(t.techCost), after);
            tech.requirements.minimumAge = age;
            tech.requirements.technologies = prevTech != null ? new List<Technology> { prevTech } : new List<Technology>();
            tech.unitModifiers = new List<UnitStatModifier>();
            tech.productionModifiers = new List<ProductionModifier>();
            tech.buildingHealthMultiplier = 1f;
            tech.researchSpeedMultiplier = 1f;
            EditorUtility.SetDirty(tech);

            var player = WarUnit(db, t.unit, t.unitName, age, siegeClass, t, false);
            var raider = WarUnit(db, t.raider, t.raiderName, age, siegeClass, t, true);
            player.prefab = BuildWarUnit(t.unit, t, false, player);
            raider.prefab = BuildWarUnit(t.raider, t, true, raider);
            player.icon = AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/Units/{t.unit}/{t.unit}_Idle.png");
            raider.icon = AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/Units/{t.raider}/{t.raider}_Idle.png");
            EditorUtility.SetDirty(player);
            EditorUtility.SetDirty(raider);
            warDb.siege.Add(new WarDatabase.SiegeTier { age = age.order, player = player, raider = raider });

            int health = t.buildingHp + 120 * age.order;
            var b = prevBuilding == null
                ? NewBuilding(db, "Barracks", t.building, t.buildingName, age, tech, new Vector2Int(3, 3), t.buildingText, WCostOf(t.buildingCost), health)
                : RankUp(db, prevBuilding, t.building, t.buildingName, age, tech, t.buildingText, WCostOf(t.buildingCost), WCostOf(t.rankCost), health);
            b.size = new Vector2Int(3, 3);
            b.artSize = Vector2Int.zero;
            b.trainsUnit = player;
            b.maxUnits = 2;
            b.workers = Mathf.Max(4, b.workers);
            b.towerRange = 0;
            b.housing = 0;
            var first = Res(db, (string)t.buildingCost[0]);
            var second = t.buildingCost.Length > 2 ? Res(db, (string)t.buildingCost[2]) : first;
            int scale = 1 + age.order / 2;
            b.upgrades = new List<BuildingUpgrade>
            {
                new BuildingUpgrade { name = "Second Crew", description = "+1 engine.", upgradeTime = 30f, extraUnits = 1,
                    requirements = new Requirements { cost = new List<ResourceAmount> { new ResourceAmount { resource = first, amount = 120 * scale } } } },
                new BuildingUpgrade { name = "Master Engineers", description = "Engines built here are 20% tougher and hit 25% harder.", upgradeTime = 35f,
                    trainedUnits = new UnitStatModifier { healthMultiplier = 1.2f, attackMultiplier = 1.25f },
                    requirements = new Requirements { cost = new List<ResourceAmount> { new ResourceAmount { resource = second, amount = 100 * scale } } } },
            };
            EditorUtility.SetDirty(b);
            made.Add((t, b));
            prevTech = tech;
            prevBuilding = t.building;
        }
        EditorUtility.SetDirty(warDb);

        // ---------- Doctrines: small army-wide research ----------
        Technology prevDoc = null;
        Technology Doctrine(string id, string name, string ageName, string text, object[] cost, float hp = 1f, float atk = 1f)
        {
            var age = AgeNamed(ageName);
            if (age == null) return prevDoc;
            string after = db.technologies.FindLast(x => x != null && x.age == age && !x.IsLevelled)?.id;
            var t = Tech(db, id, name, age, text, 50f + 6f * age.order, WCostOf(cost), after);
            t.requirements.minimumAge = age;
            t.requirements.technologies = prevDoc != null ? new List<Technology> { prevDoc } : new List<Technology>();
            t.unitModifiers = new List<UnitStatModifier>();
            if (hp != 1f || atk != 1f) t.unitModifiers.Add(new UnitStatModifier { healthMultiplier = hp, attackMultiplier = atk });
            t.productionModifiers = new List<ProductionModifier>();
            t.buildingHealthMultiplier = 1f;
            t.researchSpeedMultiplier = 1f;
            EditorUtility.SetDirty(t);
            prevDoc = t;
            return t;
        }
        Doctrine(WarCatalog.DocMuster, "Muster Rolls", "BronzeAge", "Every warrior's name and deeds written down: troops and regiments gain experience 10% faster.", new object[] { "Food", 200, "Wood", 150 });
        Doctrine(WarCatalog.DocEsprit, "Esprit de Corps", "IronAge", "Standards, songs and pride in the regiment: +5 morale for all troops.", new object[] { "Food", 300, "Iron", 80 });
        Doctrine(WarCatalog.DocCombined, "Combined Arms", "ClassicalAge", "Infantry, archers and riders fighting as one: all troops hit 3% harder.", new object[] { "Food", 400, "Coin", 100 }, atk: 1.03f);
        Doctrine(WarCatalog.DocStaff, "Staff Work", "MedievalAge", "Messengers, maps and captains who listen: the commander's aura reaches 2 tiles further, and commanders can hold one more trait.", new object[] { "Coin", 200, "Iron", 150 });
        Doctrine(WarCatalog.DocFieldworks, "Fieldworks", "RenaissanceAge", "Earthworks and gabions thrown up in an hour: troops have 3% more health, and +4 more morale defending a town.", new object[] { "Stone", 400, "Coin", 200 }, hp: 1.03f);
        Doctrine(WarCatalog.DocGeneralStaff, "General Staff", "IndustrialAge", "Officers trained to plan: experience 10% faster, and the commander's aura 2 tiles further.", new object[] { "Coin", 400, "Steel", 150 });
        Doctrine(WarCatalog.DocDepth, "Defence in Depth", "ModernAge", "Lines behind lines: troops break later (their breaking point is 4 lower).", new object[] { "Steel", 250, "Fuel", 120 });
        Doctrine(WarCatalog.DocNetwork, "Network Warfare", "InformationAge", "Every squad sees what every other sees: +4 morale for all troops.", new object[] { "Microchips", 60, "Steel", 200 });
        Doctrine(WarCatalog.DocPsionic, "Psionic Resolve", "AstralAge", "Minds steadied by star-trained mystics: +3 morale and troops break later.", new object[] { "AstralCores", 40, "Mana", 120 });
        Doctrine(WarCatalog.DocTotal, "Total Command", "GalacticAge", "One will across a thousand worlds: all troops have 3% more health and hit 3% harder.", new object[] { "WarpCells", 60, "Starmetal", 150 }, hp: 1.03f, atk: 1.03f);

        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        foreach (var (spec, building) in made) WarBuildingLooks(building, spec);
        AssetDatabase.SaveAssets();
        Debug.Log($"Warfare set up: {made.Count} siege tiers, 10 doctrines.");
    }

    /// <summary>A siege engine's definition (made fresh, not copied from a troop).</summary>
    private static UnitDefinition WarUnit(GameDatabase db, string id, string display, Age age, TroopClass cls, WarTierSpec t, bool raider)
    {
        string path = $"Assets/Data/Units/{id}.asset";
        var def = AssetDatabase.LoadAssetAtPath<UnitDefinition>(path);
        if (def == null) { def = ScriptableObject.CreateInstance<UnitDefinition>(); AssetDatabase.CreateAsset(def, path); }
        def.name = id;
        def.displayName = display;
        def.age = age;
        def.troopClass = cls;
        def.cost = 0;
        def.populationCost = t.pop;
        def.upkeep = new List<ResourceRate>();
        if (!raider && t.upkeep != null)
            for (int i = 0; i < t.upkeep.Length; i++)
            {
                var r = Res(db, t.upkeep[i]);
                if (r != null) def.upkeep.Add(Rate(r, i == 0 ? 0.04f : 0.02f + 0.002f * age.order));
            }
        def.maxHealth = Mathf.RoundToInt(t.hp * (raider ? 0.9f : 1f));
        def.attackPower = t.atk;
        def.attackCooldown = t.cd;
        def.attackRange = t.range;
        def.moveSpeed = t.speed;
        def.hitDelay = t.hitDelay;
        def.detectionRange = t.range > 2f ? 6.5f : 2.5f;
        def.movement = Movement.Ground;
        def.projectile = t.shot != null ? AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/Units/Projectiles/{t.shot}.png") : null;
        def.projectileArc = t.arc;
        def.projectileSpeed = t.shotSpeed;
        def.splashRadius = t.splash;
        def.splashFire = t.fire;
        def.muzzleSmoke = t.smoke;
        def.healer = false;
        def.isNative = false;
        def.isBoss = false;
        def.summons = null;
        EditorUtility.SetDirty(def);
        return def;
    }

    /// <summary>Bake a siege engine's frames and its prefab (a SiegeTroop for you, a SiegeEngine for raiders).</summary>
    private static GameObject BuildWarUnit(string name, WarTierSpec t, bool isEnemy, UnitDefinition definition)
    {
        string dir = $"Units/{name}";
        ResetDir(dir);
        float ppu = UnitPPU;
        var pivot = PixelSiege.PivotFor(t.kind);
        string[] names = { "Idle", "Walk0", "Walk1", "Walk2", "Walk3", "Attack0", "Attack1", "Attack2", "Attack3", "Death0", "Death1", "Death2" };
        var frames = new Sprite[PixelSiege.FrameCount];
        PixelCanvas idle = null;
        for (int f = 0; f < frames.Length; f++)
        {
            var c = PixelSiege.Draw(t.kind, isEnemy, f);
            if (f == 0) idle = c;
            frames[f] = SavePixel(c, $"{dir}/{name}_{names[f]}.png", ppu, pivot);
        }
        PixelSiege.CanvasFor(t.kind, out int w, out int h, out int cx, out int gy);
        int top = gy, left = w, right = 0;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (idle.Get(x, y).a > 200) { top = Mathf.Max(top, y); left = Mathf.Min(left, x); right = Mathf.Max(right, x); }
        float height = (top - gy) / ppu, width = (right - left) / ppu;

        var go = new GameObject($"{name}Unit") { tag = isEnemy ? "enemy" : "troop" };
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = frames[0];
        var visual = go.AddComponent<SpriteUnitVisual>();
        visual.idle = frames[0];
        visual.walk = new List<Sprite> { frames[1], frames[2], frames[3], frames[4] };
        visual.attack = new List<Sprite> { frames[5], frames[6], frames[7], frames[8] };
        visual.death = new List<Sprite> { frames[9], frames[10], frames[11] };
        visual.impactFrame = 2;
        visual.walkFps = 6f;
        visual.figure = null;
        visual.pixelsPerUnit = 0f;   // no hero rig: engines aren't redrawn in gear
        visual.weaponClass = t.weapon;
        visual.barHeight = height + 3f / ppu;
        visual.barWidth = Mathf.Clamp(width * 0.7f, 16f / ppu, 44f / ppu);
        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = Mathf.Max(width, height) * 0.3f;
        col.offset = new Vector2((left + right) * 0.5f / ppu - (cx + 0.5f) / ppu, height * 0.45f);
        go.AddComponent<MouseTouching>();
        Unit unit;
        if (isEnemy)
        {
            var e = go.AddComponent<SiegeEngine>();
            e.kind = t.kind;
            e.structureMultiplier = t.mult;
            e.fromSky = t.sky;
            e.lootPerHit = 0;
            e.lootCapacity = 9999;
            unit = e;
        }
        else
        {
            var s = go.AddComponent<SiegeTroop>();
            s.kind = t.kind;
            s.structureMultiplier = t.mult;
            s.ramOnly = t.ramOnly;
            s.fromSky = t.sky;
            unit = s;
        }
        unit.definition = definition;
        unit.personalSpace = 0.95f;
        string prefabPath = $"Assets/Prefabs/Units/{name}Unit.prefab";
        Directory.CreateDirectory("Assets/Prefabs/Units");
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        Object.DestroyImmediate(go);
        return prefab;
    }

    // ---------- The workshops ----------

    private static void WarBuildingLooks(BuildingDefinition def, WarTierSpec t)
    {
        string dir = $"Buildings/{def.id}";
        ResetDir(dir);
        int artW = Mathf.RoundToInt(def.ArtSize.x * TilePx), artH = Mathf.RoundToInt(def.ArtSize.y * TilePx);
        int w = artW + MarginSide * 2, h = artH + MarginBottom + MarginTop;
        var pivot = new Vector2(MarginSide / (float)w, MarginBottom / (float)h);
        var engine = PixelSiege.Draw(t.kind, false, 0);
        int tier = WarTiers.FindIndex(x => x.building == t.building);
        def.looks = new List<BuildingLook>();
        for (int level = 0; level <= def.upgrades.Count; level++)
        {
            var look = new BuildingLook { fps = 4f };
            for (int f = 0; f < 4; f++)
            {
                var c = new PixelCanvas(w, h);
                var k = new K(c, MarginSide, MarginBottom, level, f, 4, def.id.GetHashCode());
                WarYard(k, tier, artW);
                k.Finish();
                // the engine in the yard, drawn after the outline pass (it has its own)
                c.Blit(engine, MarginSide + 2, MarginBottom + 2);
                look.frames.Add(SavePixel(c, $"{dir}/{def.id}_L{level}_F{f}.png", PixelsPerTile, pivot));
            }
            def.looks.Add(look);
        }
        def.icon = def.looks[0].frames[0];
        EditorUtility.SetDirty(def);
    }

    /// <summary>A workshop yard: the hall behind, timber, shot and a banner, in the materials of its age.</summary>
    private static void WarYard(K k, int tier, int artW)
    {
        k.Ground(2, 2, artW - 3, 42, tier >= 5 ? PixelPalette.Stone : null);
        int x0 = 44, x1 = artW - 5, y0 = 42;
        bool flat = tier >= 5;
        var mat = tier switch { 0 => K.Mat.Wattle, 1 => K.Mat.Plaster, 2 => K.Mat.Planks, 3 or 4 or 5 or 8 or 9 => K.Mat.Stone, 6 => K.Mat.Plaster, _ => K.Mat.Marble };
        var roof = tier switch { 0 => K.RoofType.Thatch, 1 or 3 => K.RoofType.Tile, 2 => K.RoofType.Shingle, _ => K.RoofType.Slate };
        if (!flat) Hall(k, x0, x1, y0, 22, mat, roof, 16, 8, windows: 1);
        else
        {
            int top = y0 + 22;
            k.Wall(x0, y0, x1, top, mat);
            var ramp = tier == 5 ? PixelPalette.Stone : tier == 6 ? PixelPalette.Synth : tier == 7 ? PixelPalette.Moonstone
                : tier == 8 ? PixelPalette.Starmetal : tier == 9 ? PixelPalette.Riftstone : PixelPalette.Pearl;
            var accent = tier == 5 ? PixelPalette.Olive : tier == 6 ? PixelPalette.Neon : tier == 7 ? PixelPalette.Rune
                : tier == 8 ? PixelPalette.Solar : tier == 9 ? PixelPalette.Rift : PixelPalette.Prism;
            k.FlatRoof(x0 - 1, x1 + 1, top, 4, ramp);
            k.Rect(x0, top - 4, x1, top - 3, accent[2]);
            k.HLine(x0, x1, top - 3, accent[3]);
            // a wide hangar door, half open
            int dx0 = x0 + 4, dx1 = x0 + 24;
            k.Rect(dx0 - 1, y0, dx1 + 1, y0 + 17, ramp[1]);
            k.Rect(dx0, y0 + 1, dx1, y0 + 16, PixelPalette.Coal[1]);
            for (int y = y0 + 9; y <= y0 + 16; y += 2) k.HLine(dx0, dx1, y, PixelPalette.Coal[2]);
            for (int y = y0 + 1; y < y0 + 4; y++) k.HLine(dx0, dx1, y, (y + k.Frame) % 3 == 0 ? accent[2] : PixelPalette.Coal[0]);
            k.Window(x1 - 9, y0 + 9, 5, 5, true);
            k.Px(x1 - 3, top + 1, (k.Frame % 2 == 0 ? accent[3] : accent[1]));
            if (tier == 9)
            {
                // the uplink dish
                k.VLine(x1 - 8, top + 4, top + 14, PixelPalette.Starmetal[2]);
                k.Ellipse(x1 - 8, top + 17, 7, 3, PixelPalette.Riftstone);
                k.Px(x1 - 8, top + 21 + (k.Frame % 2), PixelPalette.Rift[3]);
            }
            if (tier == 7 || tier == 10)
                for (int i = 0; i < 3; i++) k.Px(x0 + 6 + i * 6, y0 + 16, (tier == 7 ? PixelPalette.Rune : PixelPalette.Prism)[2 + (k.Frame + i) % 2]);
        }
        // timber, ammunition and tools in the yard
        if (tier <= 3) k.LongLogs(6, 40, 16, 2);
        else k.Crate(8, 40);
        if (k.Level >= 1)
        {
            if (tier <= 2) k.StonePile(30, 38, 4);
            else { k.Barrel(30, 38); k.Barrel(35, 38); }
        }
        if (k.Level >= 2) k.Banner(x0 - 4, y0 + 2, 26);
        if (tier >= 3 && tier <= 6) { k.Chimney(x1 - 10, y0 + 22, 4, 10); k.Smoke(x1 - 8, y0 + 34, 0.8f); }
        else k.Torch(x0 + 2, y0 + 6);
    }
}
