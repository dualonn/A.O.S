using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The Arcane Age, the first fully fantasy age: magic, read like code once the Information Age could
/// decode the old shrines' runes, then engineered. Spellblades and arcanists, griffin riders and
/// skyships, war drakes, temples of light, obelisks that throw mana and spires that call down stars,
/// runeward walls, and its special class, the runic golem (the android's successor). Spellcode
/// (Information research) opens it. Manastone is a new hill deposit (with rich veins), refined into Mana.
/// Every Information line ranks up into an Arcane building.
/// Safe to run again. (Tools > Age of Stickman > Setup Arcane Age.)
/// </summary>
public static partial class PlaceholderArt
{
    [MenuItem("Tools/Age of Stickman/Setup Arcane Age")]
    public static void SetupArcane()
    {
        var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Resources/GameDatabase.asset");
        Age information = db.ages.Find(a => a != null && a.name == "InformationAge");
        var age = AgeAsset(db, "ArcaneAge", "Arcane Age", 11,
            "Magic, rediscovered and built into machines: rune-armoured spellblades, mana rifles, griffins and mana-drive skyships, golems with crystal hearts.");

        // ---------- Resources and the map ----------
        ResourceType food = Res(db, "Food"), wood = Res(db, "Wood"), stone = Res(db, "Stone"), iron = Res(db, "Iron"),
            gold = Res(db, "Gold"), coin = Res(db, "Coin"), steel = Res(db, "Steel"), gears = Res(db, "Gears"),
            aether = Res(db, "Aether"), fuel = Res(db, "Fuel"), uranium = Res(db, "Uranium"), cells = Res(db, "PowerCells"),
            chips = Res(db, "Microchips"), runestone = Res(db, "Runestone");
        var manastone = MedievalResource(db, "Manastone", "Manastone", age, "Microchips");
        var mana = MedievalResource(db, "Mana", "Mana", age, "Manastone");
        var manaDeposit = NewDeposit(db, "Manastone", "Manastone", manastone, true, 0.7f, 0f, 1f, -1f);
        manaDeposit.richVeins = true;
        EditorUtility.SetDirty(manaDeposit);

        // ---------- Research ----------
        var spellcode = Tech(db, "Spellcode", "Spellcode", information,
            "The runes on the old shrines, read at last: not prayers but instructions. Opens the way to the Arcane Age.",
            140f, Cost(food, 2600, gold, 800, coin, 1000, chips, 300, runestone, 200), "FusionPower");
        spellcode.requirements.minimumAge = information;
        spellcode.requirements.technologies = new List<Technology>();
        EditorUtility.SetDirty(spellcode);

        Technology ATech(string id, string name, string description, float time, List<ResourceAmount> cost, string after, params Technology[] needs)
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
        var crystals = ATech("ManaCrystals", "Mana Crystals", "Stones that hold a spell the way a jar holds water. Unlocks the Manastone Mine and the Mana Well.",
            120f, Cost(steel, 400, chips, 150, runestone, 120), "Spellcode");
        var runeblades = ATech("Runeblades", "Runeblades", "Swords that remember every cut, worn with rune-plate power armour. Spec-Ops Bases rank up into the Spellblade Hall; all troops hit 4% harder.",
            120f, Cost(steel, 400, mana, 60), crystals.id, crystals);
        runeblades.unitModifiers = new List<UnitStatModifier> { new UnitStatModifier { attackMultiplier = 1.04f } };
        var arcanism = ATech("Arcanism", "Arcanism", "A staff made into a rifle, that fires spells like bullets. Plasma Ranges rank up into the Arcane Academy.",
            120f, Cost(gold, 400, mana, 80, chips, 60), runeblades.id, runeblades);
        var griffins = ATech("GriffinTaming", "Griffin Taming", "Proud beasts that let only the brave ride them. Drone Hangars rank up into the Griffin Roost; troops move 4% faster.",
            120f, Cost(food, 2000, gold, 300, mana, 60), arcanism.id, crystals);
        griffins.unitModifiers = new List<UnitStatModifier> { new UnitStatModifier { speedMultiplier = 1.04f } };
        var light = ATech("LightMagic", "Magic of Light", "Healing worked with sunlight and song. Nano Clinics rank up into the Temple of Light; all troops have 4% more health.",
            110f, Cost(food, 2000, gold, 400, mana, 60), griffins.id);
        light.unitModifiers = new List<UnitStatModifier> { new UnitStatModifier { healthMultiplier = 1.04f } };
        var obelisks = ATech("Obelisks", "Warding Obelisks", "Stones that watch and strike. Plasma Turrets rank up into Arcane Obelisks, hard light into runeward walls; buildings have 8% more health.",
            125f, Cost(stone, 2000, mana, 120, runestone, 100), light.id, arcanism);
        obelisks.buildingHealthMultiplier = 1.08f;
        var starfall = ATech("Starfall", "Starfall", "A spell that pulls a falling star down onto your enemies. Orbital Uplinks rank up into Starfall Spires.",
            125f, Cost(gold, 500, mana, 150, chips, 80), obelisks.id, crystals);
        var fireballs = ATech("Fireballs", "Fireballs", "The oldest spell in every book, now cast in volleys. Missile Batteries rank up into Fireball Spires.",
            125f, Cost(fuel, 300, mana, 120, steel, 300), starfall.id, crystals);
        var runeforging = ATech("Runeforging", "Runeforging", "Runes hammered into the steel as it is made. Nanoforges rank up into the Runeforge; steel comes 10% faster.",
            120f, Cost(steel, 500, mana, 100, runestone, 100), fireballs.id, light);
        runeforging.productionModifiers = new List<ProductionModifier> { new ProductionModifier { resource = steel, multiplier = 1.1f } };
        var animation = ATech("Animation", "Animation", "Tools that work on their own when told. Nanoassemblers rank up into the Animated Workshop; everything you produce comes in 5% faster.",
            125f, Cost(steel, 500, gears, 300, mana, 100), runeforging.id, runeforging);
        animation.productionModifiers = new List<ProductionModifier> { new ProductionModifier { resource = null, multiplier = 1.05f } };
        var walkers = ATech("DrakeTaming", "Drake Taming", "Drakes are too heavy to fly and too proud to be ridden, until they are given rune-plate barding to wear. Plasma Tank Works rank up into the Drake Pens.",
            135f, Cost(steel, 700, mana, 160, cells, 120), animation.id, arcanism);
        var golemancy = ATech("Golemancy", "Golemancy", "Stone, an iron frame and a mana core: a servant that never tires. Android Labs rank up into the Golem Sanctum.",
            140f, Cost(stone, 2400, mana, 200, runestone, 150, chips, 100), walkers.id, animation);
        var skyships = ATech("Skyships", "Skyships", "A steel-hulled ship with a crystal for a sail and a mana engine at the stern. Jet Hangars rank up into the Skyship Dock.",
            130f, Cost(wood, 3000, mana, 150, steel, 300), golemancy.id, griffins);
        var spires = ATech("Spires", "Spires of Light", "Towers held up as much by magic as by stone. Smart Towers rank up into the Spire of Lights.",
            110f, Cost(stone, 2400, mana, 80), skyships.id);
        var alchemy = ATech("TrueAlchemy", "True Alchemy", "Lead into gold, at last, and coins that count themselves. Data Banks rank up into the Arcane Treasury; everyone pays a little more in taxes.",
            110f, Cost(gold, 900, coin, 800, mana, 80), spires.id);
        alchemy.coinPerPersonPerSecond = 0.002f;
        var scrying = ATech("Scrying", "Scrying", "Crystals that show what is far away, or not yet known. Research goes 5% faster.",
            120f, Cost(gold, 700, mana, 120, chips, 100), alchemy.id);
        scrying.researchSpeedMultiplier = 1.05f;
        var leylines = ATech("Leylines", "Leylines", "The rivers of power under the land, tapped and wired into the grid. Fusion Reactors rank up into the Leyline Nexus.",
            135f, Cost(stone, 2000, mana, 200, uranium, 80), scrying.id, crystals);

        age.advanceRequirements = new Requirements
        {
            technologies = new List<Technology> { spellcode },
            cost = Cost(food, 10000, wood, 6500, stone, 8000, iron, 2400, gold, 1200, coin, 1600, steel, 1200, chips, 300, runestone, 200),
            population = 900,
        };
        EditorUtility.SetDirty(age);

        // ---------- Units ----------
        var spellblade = MedievalUnit("Spellblade", "Spellblade", age, "CyberThug", 820, 120, 0.9f, 0.9f, 1.15f);
        var arcanist = MedievalUnit("Arcanist", "Arcanist", age, "Commando", 480, 100, 1.7f, 7.6f, 1.0f);
        var griffinRider = MedievalUnit("GriffinRider", "Griffin Rider", age, "Commando", 950, 80, 1.3f, 5.5f, 2.6f);
        griffinRider.movement = Movement.Air; griffinRider.flyHeight = 1.0f; griffinRider.populationCost = 2;
        var lightbringer = MedievalUnit("Lightbringer", "Lightbringer", age, "Nanomedic", 460, 60, 1.0f, 0.8f, 1.1f);
        lightbringer.healer = true; lightbringer.healRange = 15f;
        var warwalker = MedievalUnit("WarDrake", "War Drake", age, "PlasmaTank", 2400, 230, 3.4f, 9f, 0.9f);
        warwalker.populationCost = 3; warwalker.movement = Movement.Ground; warwalker.flyHeight = 0f;
        var golem = MedievalUnit("RunicGolem", "Runic Golem", age, "Android", 2100, 115, 1.0f, 1.1f, 1.0f);
        golem.populationCost = 2;
        var skyship = MedievalUnit("Skyship", "Skyship", age, "Airship", 1900, 150, 2.8f, 4.5f, 1.2f);
        skyship.populationCost = 3; skyship.movement = Movement.Air; skyship.flyHeight = 1.7f;
        foreach (var u in new[] { spellblade, arcanist, griffinRider, lightbringer })
        {
            u.upkeep = new List<ResourceRate> { Rate(food, 0.013f), Rate(coin, 0.08f) };
            EditorUtility.SetDirty(u);
        }
        foreach (var u in new[] { warwalker, golem, skyship })   // enchanted things run on mana
        {
            u.upkeep = new List<ResourceRate> { Rate(mana, 0.02f), Rate(coin, 0.08f) };
            EditorUtility.SetDirty(u);
        }
        age.playerUnits = new List<UnitDefinition> { spellblade, arcanist, griffinRider, lightbringer, warwalker, golem, skyship };
        var hexblade = MedievalUnit("Hexblade", "Hexblade", age, "CyberThug", 950, 95, 1.0f, 0.9f, 1.15f);
        var warlock = MedievalUnit("Warlock", "Warlock", age, "Hacker", 500, 90, 1.8f, 7.0f, 1.0f);
        var wyvernRider = MedievalUnit("WyvernRider", "Wyvern Rider", age, "Hacker", 800, 70, 1.4f, 5.0f, 2.4f);
        wyvernRider.movement = Movement.Air; wyvernRider.flyHeight = 1.0f;
        foreach (var u in new[] { hexblade, warlock, wyvernRider }) EditorUtility.SetDirty(u);
        age.enemyUnits = new List<UnitDefinition> { hexblade, warlock, wyvernRider };
        EditorUtility.SetDirty(age);

        // ---------- Buildings ----------
        var spire = RankUp(db, "SmartTower", "SpireOfLights", "Spire of Lights", age, spires,
            "A slender moonstone tower, a crystal floating over its roof. Houses 150 people.",
            Cost(stone, 700, mana, 40, steel, 120), Cost(stone, 350, mana, 20), 1450);
        spire.housing = 150;
        spire.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Hanging Halls", description = "+20 people.", requirements = new Requirements { cost = Cost(stone, 400, mana, 30) }, upgradeTime = 25f, extraHousing = 20 },
            new BuildingUpgrade { name = "Folded Rooms", description = "+25 people.", requirements = new Requirements { cost = Cost(mana, 60, gold, 120) }, upgradeTime = 30f, extraHousing = 25 },
        };
        var treasury = RankUp(db, "DataBank", "ArcaneTreasury", "Arcane Treasury", age, alchemy,
            "A gold-domed vault, enchanted coins circling over it. Coin that counts itself.",
            Cost(stone, 600, gold, 300, mana, 40), Cost(stone, 300, gold, 150), 1200);
        treasury.workers = 3;
        treasury.production = new List<ResourceRate> { Rate(coin, 4f) };
        treasury.inputs = new List<ResourceRate> { Rate(gold, 0.1f) };
        treasury.upgrades = new List<BuildingUpgrade>
        {
            Upgrade("Lead into Gold", "+25% coin.", Cost(mana, 60, coin, 400), 1.25f),
            Upgrade("Self-Counting Coins", "+25% coin.", Cost(mana, 80, gold, 200), 1.25f),
        };
        var bladeHall = RankUp(db, "SpecOpsBase", "SpellbladeHall", "Spellblade Hall", age, runeblades,
            "A long moonstone hall, a great runeblade standing before its door. Trains Spellblades in rune-plate power armour.",
            Cost(stone, 600, steel, 200, mana, 40), Cost(stone, 300, steel, 100), 1500);
        Trains(bladeHall, spellblade, 4, steel, gold, food, coin);
        var academy = RankUp(db, "PlasmaRange", "ArcaneAcademy", "Arcane Academy", age, arcanism,
            "A wizards' tower over a yard of hovering target drones. Trains Arcanists, who fire spells from mana rifles.",
            Cost(stone, 500, gold, 200, mana, 50), Cost(stone, 250, mana, 25), 1150);
        Trains(academy, arcanist, 4, steel, gold, food, coin);
        var roost = RankUp(db, "DroneHangar", "GriffinRoost", "Griffin Roost", age, griffins,
            "A tall stone perch of nests. Raises Griffin Riders: flyers that throw javelins from the sky.",
            Cost(stone, 500, food, 800, mana, 50), Cost(stone, 250, food, 400), 1150);
        Trains(roost, griffinRider, 3, steel, gold, food, coin);
        var temple = RankUp(db, "NanoClinic", "TempleOfLight", "Temple of Light", age, light,
            "A white temple under a turning golden sun. Trains Lightbringers.",
            Cost(stone, 600, gold, 150, mana, 40), Cost(stone, 300, mana, 20), 1150);
        temple.trainsUnit = lightbringer;
        temple.maxUnits = 3;
        temple.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Choir", description = "+1 lightbringer.", requirements = new Requirements { cost = Cost(mana, 40, gold, 100) }, upgradeTime = 25f, extraUnits = 1 },
            new BuildingUpgrade { name = "Dawn Hymn", description = "Lightbringers heal 30% more and are 20% tougher.", requirements = new Requirements { cost = Cost(mana, 80, gold, 120) }, upgradeTime = 30f,
                trainedUnits = new UnitStatModifier { attackMultiplier = 1.3f, healthMultiplier = 1.2f } },
        };

        var manaBolt = AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/Units/Projectiles/ManaBolt.png");
        var fallingStar = PixelProjectile("FallingStar", 14, 9, c =>
        {
            var g = PixelPalette.Gold;
            c.LineRaw(new Vector2(0, 2), new Vector2(8, 4), g[1]); c.LineRaw(new Vector2(2, 3), new Vector2(9, 5), g[2]);
            c.Ellipse(new Vector2(11, 4.5f), 2.6f, 2.6f, new[] { g[2], g[3], PixelPalette.White, PixelPalette.White });
        });
        var fireball = PixelProjectile("Fireball", 12, 10, c =>
        {
            var f = PixelPalette.Fire;
            c.LineRaw(new Vector2(0, 5), new Vector2(5, 5), f[1]); c.LineRaw(new Vector2(1, 3), new Vector2(5, 4), f[0]);
            c.Ellipse(new Vector2(8, 5), 3.4f, 3.4f, f); c.Put(9, 6, PixelPalette.White);
        });
        void Crewless(BuildingDefinition t, float range, int damage, float cooldown, Sprite shot, float arc, float speed, float splash, bool smoke, float mx, float my)
        {
            t.towerRange = range; t.towerDamage = damage; t.towerCooldown = cooldown;
            t.operatorIdle = null; t.operatorShoot = new List<Sprite>();
            t.towerProjectile = shot; t.towerProjectileArc = arc; t.towerProjectileSpeed = speed;
            t.towerSplash = splash; t.towerMuzzleSmoke = smoke;
            t.towerMuzzle = new Vector2(mx / TilePx, my / TilePx);
            t.workers = 2;
        }
        var obelisk = RankUp(db, "PlasmaTurret", "ArcaneObelisk", "Arcane Obelisk", age, obelisks,
            "A rune-carved obelisk, a crystal spinning over its tip that throws bolts of mana.",
            Cost(stone, 500, mana, 70, runestone, 40), Cost(stone, 250, mana, 35), 2000);
        Crewless(obelisk, 10.5f, 48, 0.45f, manaBolt, 0f, 26f, 0.9f, false, 32f, 50f);
        obelisk.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Seeing Eye", description = "+1.5 range.", requirements = new Requirements { cost = Cost(mana, 60, stone, 200) }, upgradeTime = 25f, extraTowerRange = 1.5f },
            new BuildingUpgrade { name = "Brighter Crystal", description = "+16 damage.", requirements = new Requirements { cost = Cost(mana, 100, runestone, 40) }, upgradeTime = 25f, extraTowerDamage = 16 },
        };
        var starSpire = RankUp(db, "OrbitalUplink", "StarfallSpire", "Starfall Spire", age, starfall,
            "A needle of a spire with a star circling its tip. It pulls falling stars down onto raiders from very far away.",
            Cost(stone, 500, mana, 120, gold, 120), Cost(stone, 250, mana, 60), 1400);
        Crewless(starSpire, 20f, 360, 3.0f, fallingStar, 0f, 40f, 1.2f, false, 32f, 56f);
        starSpire.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Wider Sky", description = "+2 range.", requirements = new Requirements { cost = Cost(mana, 100, gold, 100) }, upgradeTime = 25f, extraTowerRange = 2f },
            new BuildingUpgrade { name = "Heavier Stars", description = "+110 damage.", requirements = new Requirements { cost = Cost(mana, 140, runestone, 60) }, upgradeTime = 30f, extraTowerDamage = 110 },
        };
        var fireSpire = RankUp(db, "MissileBattery", "FireballSpire", "Fireball Spire", age, fireballs,
            "A squat tower crowned with a brazier. Each volley is a fireball that bursts wide among the raiders.",
            Cost(stone, 600, mana, 100, fuel, 100), Cost(stone, 300, mana, 50), 2400);
        Crewless(fireSpire, 14.5f, 340, 3.4f, fireball, 0.25f, 16f, 2.6f, true, 32f, 38f);
        fireSpire.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Longer Throw", description = "+2 range.", requirements = new Requirements { cost = Cost(mana, 100, fuel, 100) }, upgradeTime = 25f, extraTowerRange = 2f },
            new BuildingUpgrade { name = "Dragonfire", description = "+110 damage.", requirements = new Requirements { cost = Cost(mana, 120, steel, 160) }, upgradeTime = 30f, extraTowerDamage = 110 },
        };
        var runeWall = RankUp(db, "HardLightWall", "RunewardWall", "Runeward Wall", age, obelisks,
            "Pale moonstone, warded with glowing runes.", Cost(stone, 14, mana, 2), Cost(stone, 12, mana, 2), 4700);
        var runeGate = RankUp(db, "HardLightGate", "RunewardGate", "Runeward Gate", age, obelisks,
            "A moonstone gate that knows its friends.", Cost(stone, 28, mana, 6), Cost(stone, 24, mana, 4), 6300);
        runeWall.upgrades = new List<BuildingUpgrade>();
        runeGate.upgrades = new List<BuildingUpgrade>();

        var runeforge = RankUp(db, "Nanoforge", "Runeforge", "Runeforge", age, runeforging,
            "A forge hall where runes are hammered into the steel. Steel without end.",
            Cost(stone, 500, steel, 300, mana, 60), Cost(stone, 250, mana, 30), 1350);
        runeforge.workers = 5;
        runeforge.production = new List<ResourceRate> { Rate(steel, 0.7f) };
        runeforge.inputs = new List<ResourceRate> { Rate(iron, 0.36f), Rate(mana, 0.02f) };
        runeforge.upgrades = new List<BuildingUpgrade>
        {
            Upgrade("Rune Anvils", "+30% steel.", Cost(mana, 80, runestone, 40), 1.3f),
            Upgrade("Singing Hammers", "+30% steel.", Cost(steel, 300, mana, 80), 1.3f),
        };
        var workshop = RankUp(db, "Nanoassembler", "AnimatedWorkshop", "Animated Workshop", age, animation,
            "Hammers and saws working on their own over the benches. Gears without end.",
            Cost(stone, 400, gears, 160, mana, 60), Cost(stone, 200, mana, 30), 1200);
        workshop.workers = 4;
        workshop.production = new List<ResourceRate> { Rate(gears, 0.42f) };
        workshop.inputs = new List<ResourceRate> { Rate(steel, 0.15f), Rate(mana, 0.015f) };
        workshop.upgrades = new List<BuildingUpgrade>
        {
            Upgrade("More Hands", "+30% gears.", Cost(mana, 80, gears, 100), 1.3f),
            Upgrade("Tireless Tools", "+30% gears.", Cost(steel, 200, mana, 60), 1.3f),
        };
        var colossusWorks = RankUp(db, "PlasmaTankWorks", "DrakePens", "Drake Pens", age, walkers,
            "Moonstone pens round a great barred gate, a drake's head looking out. Raises War Drakes: armoured drakes that breathe fireballs.",
            Cost(stone, 600, steel, 400, mana, 140), Cost(stone, 300, mana, 70), 1900);
        colossusWorks.trainsUnit = warwalker;
        colossusWorks.maxUnits = 2;
        colossusWorks.workers = 6;
        colossusWorks.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Second Pen", description = "+1 war drake.", requirements = new Requirements { cost = Cost(steel, 300, mana, 100) }, upgradeTime = 35f, extraUnits = 1 },
            new BuildingUpgrade { name = "Warded Barding", description = "War drakes are 25% tougher.", requirements = new Requirements { cost = Cost(mana, 120, runestone, 60) }, upgradeTime = 35f,
                trainedUnits = new UnitStatModifier { healthMultiplier = 1.25f } },
        };
        var sanctum = RankUp(db, "AndroidLab", "GolemSanctum", "Golem Sanctum", age, golemancy,
            "A ring of standing stones open to the sky, where golems are woken with a rune, an iron frame and a mana core.",
            Cost(stone, 1200, mana, 200, runestone, 100), Cost(stone, 600, mana, 100), 1400);
        sanctum.trainsUnit = golem;
        sanctum.maxUnits = 4;
        sanctum.workers = 4;
        sanctum.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Second Circle", description = "+1 golem.", requirements = new Requirements { cost = Cost(stone, 600, mana, 140) }, upgradeTime = 35f, extraUnits = 1 },
            new BuildingUpgrade { name = "Deeper Runes", description = "Golems hit 25% harder and are 20% tougher.", requirements = new Requirements { cost = Cost(mana, 160, runestone, 80) }, upgradeTime = 35f,
                trainedUnits = new UnitStatModifier { attackMultiplier = 1.25f, healthMultiplier = 1.2f } },
        };
        var dock = RankUp(db, "JetHangar", "SkyshipDock", "Skyship Dock", age, skyships,
            "A mooring tower on a paved apron. Builds Skyships: steel-hulled, mana-driven ships of the air that bomb from on high.",
            Cost(wood, 1200, steel, 300, mana, 100), Cost(wood, 600, mana, 50), 1300);
        dock.trainsUnit = skyship;
        dock.maxUnits = 2;
        dock.workers = 4;
        dock.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Second Mooring", description = "+1 skyship.", requirements = new Requirements { cost = Cost(wood, 800, mana, 100) }, upgradeTime = 35f, extraUnits = 1 },
            new BuildingUpgrade { name = "Warded Hulls", description = "Skyships are 25% tougher.", requirements = new Requirements { cost = Cost(mana, 120, steel, 120) }, upgradeTime = 35f,
                trainedUnits = new UnitStatModifier { healthMultiplier = 1.25f } },
        };
        var nexus = RankUp(db, "FusionReactor", "LeylineNexus", "Leyline Nexus", age, leylines,
            "Standing stones where three leylines meet, wired to a transformer. Power cells by the thousand, from a trickle of mana.",
            Cost(stone, 1200, mana, 150, steel, 300), Cost(stone, 600, mana, 80), 1600);
        nexus.workers = 5;
        nexus.production = new List<ResourceRate> { Rate(cells, 0.5f) };
        nexus.inputs = new List<ResourceRate> { Rate(mana, 0.03f) };
        nexus.upgrades = new List<BuildingUpgrade>
        {
            Upgrade("Fourth Line", "+30% power cells.", Cost(stone, 600, mana, 80), 1.3f),
            Upgrade("Deep Taps", "+30% power cells.", Cost(mana, 150, runestone, 60), 1.3f),
        };
        var mine = NewBuilding(db, "SiliconQuarry", "ManastoneMine", "Manastone Mine", age, crystals, new Vector2Int(3, 3),
            "An open pit glittering with blue crystal. Must be built on or next to Manastone, or right on a rich manastone vein.",
            Cost(steel, 260, stone, 400, chips, 40), 700);
        mine.requiredDeposit = manaDeposit;
        mine.canBuildOnHills = true;
        mine.workers = 5;
        mine.production = new List<ResourceRate> { Rate(manastone, 0.14f) };
        mine.upgrades = new List<BuildingUpgrade>
        {
            Upgrade("Singing Picks", "+30% manastone.", Cost(steel, 200, mana, 40), 1.3f),
            Upgrade("Crystal Dowsing", "+30% manastone.", Cost(chips, 60, gold, 120), 1.3f),
        };
        var well = NewBuilding(db, "ChipFab", "ManaWell", "Mana Well", age, crystals, new Vector2Int(3, 3),
            "A moonstone well where manastone is dissolved into glowing Mana.",
            Cost(stone, 600, steel, 200, gold, 150), 1000);
        well.workers = 5;
        well.production = new List<ResourceRate> { Rate(mana, 0.12f) };
        well.inputs = new List<ResourceRate> { Rate(manastone, 0.12f) };
        well.runsWithoutInputs = 0f;
        well.upgrades = new List<BuildingUpgrade>
        {
            Upgrade("Deeper Well", "+30% mana.", Cost(stone, 400, gold, 150), 1.3f),
            Upgrade("Crystal Jars", "+30% mana.", Cost(mana, 60, chips, 60), 1.3f),
        };

        var all = new[] { spire, treasury, bladeHall, academy, roost, temple, obelisk, starSpire, fireSpire, runeWall, runeGate, runeforge, workshop, colossusWorks, sanctum, dock, nexus, mine, well };
        foreach (var b in all) EditorUtility.SetDirty(b);
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();

        // ---------- Art (only what this age adds) ----------
        manaDeposit.sprites = PixelDeposits("Manastone", 3, Ore.Mana);
        EditorUtility.SetDirty(manaDeposit);
        foreach (var b in all)
            if (!b.IsWall) GenerateBuildingLooks(b);
        GenerateWallArt(11);
        GenerateWarriorsAndCrews(db, new HashSet<string>
            { "Spellblade", "Arcanist", "GriffinRider", "Lightbringer", "WarDrake", "RunicGolem", "Skyship", "Hexblade", "Warlock", "WyvernRider" });
        obelisk.towerProjectile = AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/Units/Projectiles/ManaBolt.png");
        EditorUtility.SetDirty(obelisk);
        SetupBosses("ArcaneAge");
        GenerateIconArt();
        SetupRichVeins();
        AssetDatabase.SaveAssets();
        Debug.Log("Arcane Age set up.");
    }
}
