using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Flyers, tunnellers and lighter towers:
/// - Air: the Industrial Airship (Airship Mast) and the Modern Biplane (Airfield, which the mast ranks up
///   into); Sky Pirate airships and raider biplanes. Flyers cross walls and water; only ranged attacks reach them.
/// - Underground: Sapper raiders (Medieval), Steam Moles (Industrial) and Tunnel Rats (Modern), who tunnel
///   under walls and surface to fight.
/// - Towers beside the heavy guns: precision (Marksman Tower, Railgun Tower, Sniper Nest), rapid (Gatling
///   Tower, which the Pillbox now ranks up from), and the Modern heavy Gun Emplacement after the Steam Gun Turret.
/// Safe to run again. (Tools > Age of Stickman > Setup Flyers, Tunnellers and Towers.)
/// </summary>
public static partial class PlaceholderArt
{
    [MenuItem("Tools/Age of Stickman/Setup Flyers, Tunnellers and Towers")]
    public static void SetupTowersAndAir()
    {
        var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Resources/GameDatabase.asset");
        Age AgeNamed(string n) => db.ages.Find(a => a != null && a.name == n);
        Age medieval = AgeNamed("MedievalAge"), enlightenment = AgeNamed("EnlightenmentAge"), industrial = AgeNamed("IndustrialAge"), modern = AgeNamed("ModernAge");
        ResourceType food = Res(db, "Food"), wood = Res(db, "Wood"), stone = Res(db, "Stone"), iron = Res(db, "Iron"), steel = Res(db, "Steel"),
            coin = Res(db, "Coin"), gunpowder = Res(db, "Gunpowder"), gears = Res(db, "Gears"), aether = Res(db, "Aether"), fuel = Res(db, "Fuel"), gold = Res(db, "Gold");

        // ---------- Research ----------
        Technology NewTech(string id, string name, Age age, string description, float time, List<ResourceAmount> cost, string after, params Technology[] needs)
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
        var airships = NewTech("Airships", "Airships", industrial, "A gasbag of aether-light gas and a gondola beneath. Unlocks the Airship Mast; airships fly over walls and drop bombs.",
            115f, Cost(steel, 300, gears, 80, aether, 40, wood, 600), "Railways", db.GetTechnology("AetherTheory"));
        var aviation = NewTech("Aviation", "Aviation", modern, "Wood, wire and a petrol engine: flying machines. Airship Masts rank up into the Airfield.",
            115f, Cost(steel, 300, fuel, 150, gears, 100), "MotorVehicles", db.GetTechnology("MotorVehicles"));

        // ---------- Units ----------
        UnitDefinition Mover(string id, string display, Age age, string template, int hp, int atk, float cd, float range, float speed, Movement movement, float flyHeight = 1.4f)
        {
            var u = MedievalUnit(id, display, age, template, hp, atk, cd, range, speed);
            u.movement = movement;
            u.flyHeight = flyHeight;
            EditorUtility.SetDirty(u);
            return u;
        }
        var sapper = Mover("Sapper", "Sapper", medieval, "Brigand", 420, 34, 1.1f, 0.8f, 1.0f, Movement.Underground);
        var mole = Mover("SteamMole", "Steam Mole", industrial, "SteamBrute", 1000, 70, 1.4f, 1.0f, 0.85f, Movement.Underground);
        var tunnelRat = Mover("TunnelRat", "Tunnel Rat", modern, "TrenchRaider", 650, 60, 1.0f, 0.8f, 1.1f, Movement.Underground);
        var airship = Mover("Airship", "Airship", industrial, "Grenadier", 900, 90, 3.0f, 5.0f, 0.7f, Movement.Air, 1.7f);
        airship.populationCost = 3;
        airship.upkeep = new List<ResourceRate> { Rate(aether, 0.006f), Rate(coin, 0.08f) };
        var skyPirate = Mover("SkyPirate", "Sky Pirate", industrial, "BanditRifleman", 800, 70, 3.0f, 5.0f, 0.7f, Movement.Air, 1.7f);
        var biplane = Mover("Biplane", "Biplane", modern, "MachineGunner", 600, 14, 0.4f, 5.5f, 2.2f, Movement.Air, 1.4f);
        biplane.populationCost = 2;
        biplane.upkeep = new List<ResourceRate> { Rate(fuel, 0.03f), Rate(coin, 0.07f) };
        var raiderPlane = Mover("RaiderBiplane", "Raider Biplane", modern, "Gangster", 520, 12, 0.45f, 5.0f, 2.1f, Movement.Air, 1.4f);
        void AddTo(List<UnitDefinition> list, UnitDefinition u) { if (!list.Contains(u)) list.Add(u); }
        AddTo(medieval.enemyUnits, sapper);
        AddTo(industrial.enemyUnits, mole); AddTo(industrial.enemyUnits, skyPirate); AddTo(industrial.playerUnits, airship);
        AddTo(modern.enemyUnits, tunnelRat); AddTo(modern.enemyUnits, raiderPlane); AddTo(modern.playerUnits, biplane);
        foreach (var a in new[] { medieval, industrial, modern }) EditorUtility.SetDirty(a);

        // ---------- Towers ----------
        var ball = AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/Units/Projectiles/MusketBall.png");
        var bullet = AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/Units/Projectiles/Bullet.png");
        var shell = AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/Units/Projectiles/Cannonball.png");
        var slug = PixelProjectile("RailSlug", 14, 5, c =>
        {
            var a = PixelPalette.Aether;
            c.LineRaw(new Vector2(0, 2), new Vector2(9, 2), a[1]); c.LineRaw(new Vector2(5, 2), new Vector2(11, 2), a[3]);
            c.Rect(11, 1, 13, 3, PixelPalette.Brass[2]);
        });
        void Crewless(BuildingDefinition t, float range, int damage, float cooldown, Sprite shot, float arc, float speed, float splash, bool smoke, float muzzleX, float muzzleY)
        {
            t.towerRange = range; t.towerDamage = damage; t.towerCooldown = cooldown;
            t.operatorIdle = null; t.operatorShoot = new List<Sprite>();
            t.towerProjectile = shot; t.towerProjectileArc = arc; t.towerProjectileSpeed = speed;
            t.towerSplash = splash; t.towerMuzzleSmoke = smoke;
            t.towerMuzzle = new Vector2(muzzleX / TilePx, muzzleY / TilePx);
            t.workers = 2;
        }
        var marksman = NewBuilding(db, "CannonBastion", "MarksmanTower", "Marksman Tower", enlightenment, db.GetTechnology("LightInfantry"), new Vector2Int(2, 2),
            "A slim stone tower with a loophole near the top. A marksman picks off raiders from far away, one at a time.",
            Cost(stone, 300, wood, 120, gunpowder, 30), 800);
        Crewless(marksman, 13f, 75, 2.4f, ball, 0f, 26f, 0f, true, 51f, 45.5f);
        marksman.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Rifled Barrel", description = "+2 range.", requirements = new Requirements { cost = Cost(steel, 40, wood, 100) }, upgradeTime = 25f, extraTowerRange = 2f },
            new BuildingUpgrade { name = "Steady Rest", description = "+25 damage.", requirements = new Requirements { cost = Cost(steel, 60, gunpowder, 30) }, upgradeTime = 25f, extraTowerDamage = 25 },
        };

        var railgun = RankUp(db, "MarksmanTower", "RailgunTower", "Railgun Tower", industrial, db.GetTechnology("AetherTheory"),
            "A brick base carrying a long aether railgun: it hurls a slug across the map, through anything in its way.",
            Cost(stone, 350, steel, 160, aether, 40, gears, 40), Cost(steel, 100, aether, 25), 1000);
        Crewless(railgun, 15f, 190, 3.6f, slug, 0f, 40f, 0f, false, 63f, 33.5f);
        railgun.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Longer Rails", description = "+2 range.", requirements = new Requirements { cost = Cost(steel, 100, gears, 30) }, upgradeTime = 25f, extraTowerRange = 2f },
            new BuildingUpgrade { name = "Aether Capacitors", description = "+60 damage.", requirements = new Requirements { cost = Cost(aether, 60, gears, 40) }, upgradeTime = 30f, extraTowerDamage = 60 },
        };

        var sniper = RankUp(db, "RailgunTower", "SniperNest", "Sniper Nest", modern, db.GetTechnology("BoltAction"),
            "A tall concrete tower with sandbags on top. Its sniper's scoped rifle reaches further than anything else on the field.",
            Cost(stone, 400, steel, 160, gunpowder, 60), Cost(stone, 200, steel, 80), 1100);
        Crewless(sniper, 16.5f, 230, 3.2f, ball, 0f, 34f, 0f, true, 61f, 50.5f);
        sniper.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Telescopic Sight", description = "+2 range.", requirements = new Requirements { cost = Cost(steel, 80, gold, 40) }, upgradeTime = 25f, extraTowerRange = 2f },
            new BuildingUpgrade { name = "Heavy Rounds", description = "+70 damage.", requirements = new Requirements { cost = Cost(steel, 120, gunpowder, 60) }, upgradeTime = 30f, extraTowerDamage = 70 },
        };

        var gatling = NewBuilding(db, "CannonBastion", "GatlingTower", "Gatling Tower", industrial, db.GetTechnology("Rifling"), new Vector2Int(2, 2),
            "A squat iron-clad tower with a crank-turned gatling gun. Short reach, but a hail of bullets.",
            Cost(stone, 320, steel, 120, gears, 30), 1100);
        Crewless(gatling, 8.5f, 22, 0.5f, bullet, 0f, 30f, 0f, false, 59f, 23.5f);
        gatling.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "More Barrels", description = "+8 damage.", requirements = new Requirements { cost = Cost(steel, 80, gears, 30) }, upgradeTime = 25f, extraTowerDamage = 8 },
            new BuildingUpgrade { name = "Riveted Plate", description = "+500 health.", requirements = new Requirements { cost = Cost(steel, 120) }, upgradeTime = 30f, extraHealth = 500 },
        };
        // The pillbox's machine gun follows on from the gatling, not from the heavy turret.
        JoinLine(db, "GatlingTower", "Pillbox", "ReinforcedConcrete", Cost(stone, 250, steel, 70));

        var emplacement = RankUp(db, "SteamGunTurret", "GunEmplacement", "Gun Emplacement", modern, db.GetTechnology("ReinforcedConcrete"),
            "A big concrete emplacement with a heavy gun under a curved shield. Its shells burst wide among the raiders.",
            Cost(stone, 600, steel, 220, gunpowder, 120), Cost(stone, 300, steel, 110), 1900);
        Crewless(emplacement, 13f, 220, 3.6f, shell, 0.05f, 20f, 2.0f, true, 64f, 20f);
        emplacement.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Range Tables", description = "+2 range.", requirements = new Requirements { cost = Cost(gears, 60, steel, 60) }, upgradeTime = 25f, extraTowerRange = 2f },
            new BuildingUpgrade { name = "High-Explosive Shells", description = "+70 damage.", requirements = new Requirements { cost = Cost(gunpowder, 120, steel, 80) }, upgradeTime = 30f, extraTowerDamage = 70 },
            new BuildingUpgrade { name = "Overhead Cover", description = "+900 health.", requirements = new Requirements { cost = Cost(stone, 500) }, upgradeTime = 30f, extraHealth = 900 },
        };

        // ---------- Buildings for flyers ----------
        var mast = NewBuilding(db, "CannonFoundry", "AirshipMast", "Airship Mast", industrial, airships, new Vector2Int(3, 3),
            "A lattice mooring mast and a hangar. Builds Airships, which float over walls and drop bombs.",
            Cost(steel, 260, wood, 400, gears, 60, aether, 30), 700);
        mast.trainsUnit = airship;
        mast.maxUnits = 2;
        mast.workers = 4;
        mast.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Second Mooring", description = "+1 airship.", requirements = new Requirements { cost = Cost(steel, 140, aether, 40) }, upgradeTime = 35f, extraUnits = 1 },
            new BuildingUpgrade { name = "Bigger Bombs", description = "Airships hit 25% harder.", requirements = new Requirements { cost = Cost(gunpowder, 120, gears, 40) }, upgradeTime = 35f,
                trainedUnits = new UnitStatModifier { attackMultiplier = 1.25f } },
        };
        var airfield = RankUp(db, "AirshipMast", "Airfield", "Airfield", modern, aviation,
            "A grass strip, a hangar and a windsock. Builds Biplanes: fast fighters with a machine gun.",
            Cost(steel, 300, fuel, 100, gears, 80), Cost(steel, 150, fuel, 50), 900);
        airfield.trainsUnit = biplane;
        airfield.maxUnits = 3;
        airfield.workers = 4;
        airfield.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade { name = "Second Hangar", description = "+1 biplane.", requirements = new Requirements { cost = Cost(steel, 160, fuel, 60) }, upgradeTime = 35f, extraUnits = 1 },
            new BuildingUpgrade { name = "Synchronised Guns", description = "Biplanes hit 25% harder.", requirements = new Requirements { cost = Cost(gears, 80, steel, 80) }, upgradeTime = 35f,
                trainedUnits = new UnitStatModifier { attackMultiplier = 1.25f } },
        };

        var all = new[] { marksman, railgun, sniper, gatling, emplacement, mast, airfield };
        foreach (var b in all) EditorUtility.SetDirty(b);
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        foreach (var b in all) GenerateBuildingLooks(b);
        GenerateWarriorsAndCrews(db, new HashSet<string> { "Sapper", "SteamMole", "TunnelRat", "Airship", "SkyPirate", "Biplane", "RaiderBiplane" });
        AssetDatabase.SaveAssets();
        Debug.Log("Flyers, tunnellers and towers set up.");
    }
}
