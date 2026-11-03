using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Troops, raiders, raider chiefs and tower crews in pixel art. Each unit is a FigureStyle
/// (its gear, team colour and mount) drawn by PixelFigure into 12 frames and baked into a prefab.
/// The style is kept on the prefab so heroes can be redrawn in the game in their own gear.
/// (Tools > Age of Stickman > Generate Unit Art.)
/// </summary>
public static partial class PlaceholderArt
{
    /// <summary>Art pixels per world unit: 32 per tile, everywhere.</summary>
    public const float PixelsPerTile = 32f;

    /// <summary>Units are scaled by the controller's unitScale in play, so their frames use a matching density.</summary>
    private static float UnitPPU => PixelsPerTile * UnitScale;

    private static float UnitScale
    {
        get
        {
            var controller = Object.FindFirstObjectByType<FreeModeController>();
            return controller != null ? controller.unitScale : 0.42f;
        }
    }

    private static FigureStyle Fig(string team, GearLook head, GearLook weapon, GearLook shield = GearLook.None, GearLook body = GearLook.None,
        MountKind mount = MountKind.None, string crest = "", string shieldColor = "", string cape = "", float scale = 1f,
        bool raider = false, bool necklace = false, bool warpaint = false, string coat = "Horse", bool barding = false) => new FigureStyle
    {
        team = team, head = head, weapon = weapon, shield = shield, body = body, mount = mount, crest = crest, shieldColor = shieldColor,
        cape = cape, scale = scale, raider = raider, necklace = necklace, warpaint = warpaint, coat = coat, barding = barding,
    };

    private const string B = "Blue", Rd = "Red";

    /// <summary>Unit assets (Assets/Data/Units/{id}.asset) and how each is drawn.</summary>
    private static (string id, FigureStyle style, bool enemy)[] PixelWarriors => new[]
    {
        // Stone Age
        ("Clubman", Fig(B, GearLook.Headband, GearLook.Club), false),
        ("EnemyClubman", Fig(Rd, GearLook.Feather, GearLook.Club, raider: true, necklace: true), true),
        // Bronze Age
        ("Spearman", Fig(B, GearLook.DomeHelm, GearLook.Spear, GearLook.HideShield), false),
        ("Axeman", Fig(B, GearLook.DomeHelm, GearLook.Axe, body: GearLook.HideVest), false),
        ("WarChariot", Fig(B, GearLook.DomeHelm, GearLook.Spear, mount: MountKind.Chariot), false),
        ("BronzeRaider", Fig(Rd, GearLook.DomeHelm, GearLook.Spear, GearLook.HideShield, raider: true, necklace: true, crest: "Leather"), true),
        // Iron Age
        ("Swordsman", Fig(B, GearLook.ConeHelm, GearLook.Sword, GearLook.RoundShield), false),
        ("Archer", Fig(B, GearLook.Cap, GearLook.Bow), false),
        ("Slinger", Fig(B, GearLook.Headband, GearLook.Sling), false),
        ("IronRaider", Fig(Rd, GearLook.ConeHelm, GearLook.Sword, GearLook.RoundShield, raider: true, necklace: true, shieldColor: "Leather"), true),
        ("RaiderArcher", Fig(Rd, GearLook.Feather, GearLook.Bow, raider: true), true),
        ("HorseArcher", Fig(B, GearLook.Cap, GearLook.Bow, body: GearLook.Jerkin, mount: MountKind.Horse), false),
        // Classical Age
        ("Legionary", Fig(B, GearLook.CrestedHelm, GearLook.ShortSword, GearLook.TowerShield, GearLook.Plate, crest: "Red"), false),
        ("CompositeArcher", Fig(B, GearLook.ConeHelm, GearLook.Bow, body: GearLook.Jerkin), false),
        ("Horseman", Fig(B, GearLook.CrestedHelm, GearLook.Spear, mount: MountKind.Horse, crest: "Blue"), false),
        ("Peltast", Fig(B, GearLook.None, GearLook.Javelin, GearLook.WickerShield), false),
        ("WarElephant", Fig(B, GearLook.CrestedHelm, GearLook.Spear, mount: MountKind.Elephant, crest: "Blue", coat: "Elephant"), false),
        ("Barbarian", Fig(Rd, GearLook.None, GearLook.Axe, GearLook.OvalShield, GearLook.Bare, raider: true, necklace: true, warpaint: true, shieldColor: "Green"), true),
        ("HorseRaider", Fig(Rd, GearLook.Headband, GearLook.Sword, mount: MountKind.Horse, raider: true, coat: "DarkHorse"), true),
        // Medieval Age
        ("ManAtArms", Fig(B, GearLook.GreatHelm, GearLook.Sword, GearLook.KiteShield, GearLook.Surcoat, crest: "Gold"), false),
        ("Pikeman", Fig(B, GearLook.KettleHat, GearLook.Pike, body: GearLook.Mail), false),
        ("Crossbowman", Fig(B, GearLook.KettleHat, GearLook.Crossbow, body: GearLook.Jerkin), false),
        ("Knight", Fig(B, GearLook.GreatHelm, GearLook.Lance, GearLook.KiteShield, GearLook.Surcoat, MountKind.Horse, crest: "Gold", barding: true), false),
        ("Monk", Fig(B, GearLook.Cowl, GearLook.None, body: GearLook.Robe, crest: "Wolf"), false),
        ("RuneWarden", Fig(B, GearLook.Cowl, GearLook.RuneStaff, body: GearLook.Robe, crest: "Purple"), false),
        ("Brigand", Fig(Rd, GearLook.Cap, GearLook.Mace, GearLook.RoundShield, GearLook.Jerkin, raider: true, shieldColor: "Leather"), true),
        ("Reaver", Fig(Rd, GearLook.KettleHat, GearLook.Sword, GearLook.KiteShield, GearLook.Mail, MountKind.Horse, raider: true, coat: "DarkHorse", shieldColor: "Coal"), true),
        ("BanditCrossbow", Fig(Rd, GearLook.Hood, GearLook.Crossbow, body: GearLook.Jerkin, raider: true), true),
        // Renaissance
        ("Arquebusier", Fig(B, GearLook.Morion, GearLook.Arquebus, body: GearLook.Doublet, crest: "Gold"), false),
        ("Halberdier", Fig(B, GearLook.Morion, GearLook.Halberd, body: GearLook.Cuirass), false),
        ("Cuirassier", Fig(B, GearLook.Morion, GearLook.Pistol, body: GearLook.Cuirass, mount: MountKind.Horse, coat: "DarkHorse"), false),
        ("Physician", Fig(B, GearLook.PlagueMask, GearLook.None, body: GearLook.Robe, crest: "Coal"), false),
        ("Alchemist", Fig(B, GearLook.Cowl, GearLook.Flask, body: GearLook.Robe, crest: "Green"), false),
        ("FieldCannon", Fig(B, GearLook.FeatheredCap, GearLook.Cannon, body: GearLook.Doublet, mount: MountKind.Cannon, crest: "Gold"), false),
        ("Landsknecht", Fig(Rd, GearLook.FeatheredCap, GearLook.Zweihander, body: GearLook.Doublet, raider: true, crest: "Gold"), true),
        ("Highwayman", Fig(Rd, GearLook.FeatheredCap, GearLook.Pistol, body: GearLook.Jerkin, mount: MountKind.Horse, raider: true, coat: "DarkHorse", crest: "Coal"), true),
        ("BrigandMusketeer", Fig(Rd, GearLook.Cap, GearLook.Arquebus, body: GearLook.Jerkin, raider: true), true),
        // Age of Enlightenment
        ("Musketeer", Fig(B, GearLook.Tricorne, GearLook.Musket, body: GearLook.Greatcoat, crest: "Linen"), false),
        ("Jager", Fig(B, GearLook.Tricorne, GearLook.Rifle, body: GearLook.Greatcoat, crest: "Green"), false),
        ("Hussar", Fig(B, GearLook.Busby, GearLook.Sabre, body: GearLook.Doublet, mount: MountKind.Horse, crest: "Gold"), false),
        ("RegimentalSurgeon", Medic(Fig(B, GearLook.Tricorne, GearLook.None, body: GearLook.Greatcoat, crest: "Linen")), false),
        ("Galvanist", Fig(B, GearLook.Tricorne, GearLook.LeydenRod, body: GearLook.Robe, crest: "Coal"), false),
        ("Howitzer", Fig(B, GearLook.Tricorne, GearLook.Cannon, body: GearLook.Greatcoat, mount: MountKind.Cannon, crest: "Gold"), false),
        ("Privateer", Fig(Rd, GearLook.Tricorne, GearLook.Sabre, body: GearLook.Jerkin, raider: true, crest: "Red"), true),
        ("BrigandFusilier", Fig(Rd, GearLook.Tricorne, GearLook.Musket, body: GearLook.Jerkin, raider: true, crest: "Leather"), true),
        ("MarauderHussar", Fig(Rd, GearLook.Busby, GearLook.Sabre, body: GearLook.Jerkin, mount: MountKind.Horse, raider: true, coat: "DarkHorse", crest: "Coal"), true),
        // Industrial (steampunk)
        ("Rifleman", Fig(B, GearLook.Shako, GearLook.Rifle, body: GearLook.Greatcoat, crest: "Red"), false),
        ("Grenadier", Fig(B, GearLook.Bearskin, GearLook.Grenade, body: GearLook.Greatcoat, crest: "Red"), false),
        ("Dragoon", Fig(B, GearLook.Shako, GearLook.Rifle, body: GearLook.Greatcoat, mount: MountKind.Horse, crest: "Gold", coat: "DarkHorse"), false),
        ("FieldSurgeon", Medic(Fig(B, GearLook.TopHat, GearLook.None, body: GearLook.Greatcoat, crest: "Linen")), false),
        ("SteamCrawler", Fig(B, GearLook.Goggles, GearLook.Cannon, body: GearLook.Greatcoat, mount: MountKind.Crawler, crest: "Gold"), false),
        ("Automaton", Skin(Fig(B, GearLook.BoilerHelm, GearLook.PistonHammer, body: GearLook.BrassPlate, scale: 1.3f), "Brass"), false),
        ("Saboteur", Fig(Rd, GearLook.Goggles, GearLook.Wrench, body: GearLook.Jerkin, raider: true), true),
        // Flyers and tunnellers
        ("Sapper", Fig(Rd, GearLook.KettleHat, GearLook.Pick, body: GearLook.Jerkin, raider: true), true),
        ("SteamMole", Fig(Rd, GearLook.Goggles, GearLook.Wrench, body: GearLook.Jerkin, mount: MountKind.Drill, raider: true), true),
        ("TunnelRat", Fig(Rd, GearLook.Cap, GearLook.Shovel, body: GearLook.Fatigues, raider: true), true),
        ("Airship", Fig(B, GearLook.Goggles, GearLook.Grenade, body: GearLook.Greatcoat, mount: MountKind.Airship), false),
        ("SkyPirate", Fig(Rd, GearLook.Goggles, GearLook.Grenade, body: GearLook.Jerkin, mount: MountKind.Airship, raider: true), true),
        ("Biplane", Fig(B, GearLook.Goggles, GearLook.MachineGun, body: GearLook.Fatigues, mount: MountKind.Biplane), false),
        ("RaiderBiplane", Fig(Rd, GearLook.Goggles, GearLook.MachineGun, body: GearLook.Jerkin, mount: MountKind.Biplane, raider: true), true),
        // Electric Age (retro-futuristic)
        ("RayTrooper", Fig(B, GearLook.BubbleHelmet, GearLook.RayRifle, body: GearLook.Jumpsuit), false),
        ("BlasterGunner", Fig(B, GearLook.BubbleHelmet, GearLook.RayRifle, body: GearLook.Jumpsuit, crest: "Red"), false),
        ("HoverCar", Fig(B, GearLook.BubbleHelmet, GearLook.RayRifle, body: GearLook.Jumpsuit, mount: MountKind.HoverCar), false),
        ("RayNurse", Medic(Fig(B, GearLook.BubbleHelmet, GearLook.None, body: GearLook.Jumpsuit)), false),
        ("HoverTank", Fig(B, GearLook.BubbleHelmet, GearLook.Cannon, body: GearLook.Jumpsuit, mount: MountKind.HoverTank), false),
        ("GiantRobot", Skin(Fig(B, GearLook.RobotHead, GearLook.Claw, body: GearLook.ChromePlate, scale: 1.5f), "Chrome"), false),
        ("Rocketeer", Fig(B, GearLook.BubbleHelmet, GearLook.RayGun, body: GearLook.Jumpsuit, mount: MountKind.Jetpack), false),
        ("RayBandit", Fig(Rd, GearLook.Goggles, GearLook.RayGun, body: GearLook.Jerkin, raider: true), true),
        ("RoboRaider", Skin(Fig(Rd, GearLook.RobotHead, GearLook.Claw, body: GearLook.ChromePlate, scale: 1.2f, raider: true), "Chrome"), true),
        ("JetRaider", Fig(Rd, GearLook.Goggles, GearLook.RayGun, body: GearLook.Jerkin, mount: MountKind.Jetpack, raider: true), true),
        // Modern (dieselpunk)
        ("Infantryman", Fig(B, GearLook.SteelHelmet, GearLook.BoltRifle, body: GearLook.Fatigues), false),
        ("MachineGunner", Fig(B, GearLook.SteelHelmet, GearLook.MachineGun, body: GearLook.Fatigues), false),
        ("ArmouredCar", Fig(B, GearLook.SteelHelmet, GearLook.MachineGun, body: GearLook.Fatigues, mount: MountKind.ArmouredCar), false),
        ("Medic", Medic(Fig(B, GearLook.SteelHelmet, GearLook.None, body: GearLook.Fatigues)), false),
        ("Tank", Fig(B, GearLook.Goggles, GearLook.Cannon, body: GearLook.Fatigues, mount: MountKind.Tank), false),
        ("DieselWalker", Fig(B, GearLook.Goggles, GearLook.MachineGun, body: GearLook.Fatigues, mount: MountKind.Walker), false),
        ("Gangster", Fig(Rd, GearLook.Fedora, GearLook.Tommygun, body: GearLook.TrenchCoat, raider: true, crest: "Red"), true),
        ("TrenchRaider", Fig(Rd, GearLook.Cap, GearLook.Shovel, body: GearLook.Jerkin, raider: true), true),
        ("BanditSniper", Fig(Rd, GearLook.Fedora, GearLook.BoltRifle, body: GearLook.TrenchCoat, raider: true, crest: "Coal"), true),
        ("BanditRifleman", Fig(Rd, GearLook.Cap, GearLook.Rifle, body: GearLook.Jerkin, raider: true), true),
        ("SteamBrute", Fig(Rd, GearLook.Goggles, GearLook.PistonHammer, body: GearLook.BrassPlate, scale: 1.2f, raider: true), true),
    };

    private static FigureStyle Medic(FigureStyle s) { s.medic = true; return s; }
    private static FigureStyle Skin(FigureStyle s, string skin) { s.skin = skin; return s; }

    /// <summary>Tower crews (drawn from the waist up; they stand behind a parapet).</summary>
    private static (string building, string name, FigureStyle style)[] PixelCrews => new[]
    {
        ("Watchtower", "TowerArcher", Fig(B, GearLook.Cap, GearLook.Bow)),
        ("StoneTower", "TowerArcherIron", Fig(B, GearLook.ConeHelm, GearLook.Bow)),
        ("BallistaTower", "BallistaCrew", Fig(B, GearLook.CrestedHelm, GearLook.Ballista, crest: "Blue")),
        ("CastleTower", "TowerCrossbowman", Fig(B, GearLook.KettleHat, GearLook.Crossbow, body: GearLook.Mail)),
        ("CannonBastion", "BastionGunner", Fig(B, GearLook.Morion, GearLook.Cannon, body: GearLook.Cuirass)),
    };

    [MenuItem("Tools/Age of Stickman/Generate Unit Art")]
    public static void GenerateUnitArt()
    {
        GenerateWarriorsAndCrews(AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Resources/GameDatabase.asset"));
        SetupBosses();
        AssetDatabase.SaveAssets();
        Debug.Log("Unit art generated.");
    }

    /// <summary>
    /// Generate units' frames and prefabs, and crewed towers' operators and projectiles. 'only' limits it
    /// to those unit and tower ids (an age's setup redraws just what it adds); null redraws everything.
    /// </summary>
    private static void GenerateWarriorsAndCrews(GameDatabase database, ICollection<string> only = null)
    {
        var arrow = PixelProjectile("Arrow", 12, 5, DrawArrow);
        var bolt = PixelProjectile("Bolt", 16, 6, DrawBolt);
        var stone = PixelProjectile("SlingStone", 5, 5, DrawStone);
        var javelin = PixelProjectile("Javelin", 18, 5, DrawJavelin);
        var runeBolt = PixelProjectile("RuneBolt", 12, 7, DrawRuneBolt);
        var ball = PixelProjectile("MusketBall", 4, 4, c => { c.Rect(1, 1, 2, 2, PixelPalette.Iron[2]); c.Put(1, 2, PixelPalette.Steel[3]); });
        var cannonball = PixelProjectile("Cannonball", 8, 8, c => { c.Ellipse(new Vector2(4, 4), 3, 3, PixelPalette.Iron); c.Put(3, 5, PixelPalette.Steel[3]); });
        var flask = PixelProjectile("FireFlask", 8, 9, c =>
        {
            c.Ellipse(new Vector2(4, 3.5f), 3, 3, new[] { PixelPalette.Green[0], PixelPalette.Green[2], PixelPalette.Green[3], PixelPalette.White });
            c.Rect(3, 6, 4, 8, PixelPalette.Linen[3]); c.Put(4, 8, PixelPalette.Fire[2]);
        });
        var grenade = PixelProjectile("Grenade", 7, 8, c =>
        {
            c.Ellipse(new Vector2(3.5f, 3.5f), 3, 3, PixelPalette.Iron); c.Put(2, 5, PixelPalette.Steel[3]);
            c.Put(5, 6, PixelPalette.Linen[2]); c.Put(6, 7, PixelPalette.Fire[3]);
        });
        var spark = PixelProjectile("GalvanicSpark", 10, 7, c =>
        {
            var w = PixelPalette.Water;
            c.LineRaw(new Vector2(1, 2), new Vector2(3, 4), w[3]); c.LineRaw(new Vector2(3, 4), new Vector2(5, 2), w[4]);
            c.LineRaw(new Vector2(5, 2), new Vector2(7, 4), w[4]);
            c.Ellipse(new Vector2(8, 3), 1.6f, 1.6f, new[] { w[2], w[3], w[4], PixelPalette.White });
        });
        var rayBolt = PixelProjectile("RayBolt", 10, 4, c =>
        {
            var r = PixelPalette.Ray;
            c.LineRaw(new Vector2(0, 1), new Vector2(5, 1), r[1]); c.LineRaw(new Vector2(3, 2), new Vector2(8, 2), r[2]); c.Put(9, 2, r[3]); c.Put(8, 1, r[3]);
        });
        var bullet = PixelProjectile("Bullet", 5, 3, c => { c.LineRaw(new Vector2(0, 1), new Vector2(3, 1), PixelPalette.Brass[2]); c.Put(4, 1, PixelPalette.Glow[3]); });
        var aetherBolt = PixelProjectile("AetherBolt", 12, 7, c =>
        {
            var a = PixelPalette.Aether;
            c.LineRaw(new Vector2(1, 3), new Vector2(4, 4), a[1]); c.LineRaw(new Vector2(4, 4), new Vector2(6, 2), a[2]);
            c.LineRaw(new Vector2(6, 2), new Vector2(8, 3), a[2]);
            c.Ellipse(new Vector2(9, 3), 2.2f, 2.2f, a); c.Put(9, 4, a[3]);
        });
        foreach (var (id, style, enemy) in PixelWarriors)
        {
            if (only != null && !only.Contains(id)) continue;
            var def = AssetDatabase.LoadAssetAtPath<UnitDefinition>($"Assets/Data/Units/{id}.asset");
            if (def == null) continue;
            string name = id == "EnemyClubman" ? "Raider" : id;   // keep existing prefab names
            def.prefab = BuildPixelUnit(name, style, enemy, def);
            def.icon = AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/Units/{name}/{name}_Idle.png");
            def.projectile = style.weapon == GearLook.Bow ? arrow : style.weapon == GearLook.Sling ? stone
                : style.weapon == GearLook.Javelin ? javelin : style.weapon == GearLook.Crossbow ? bolt
                : style.weapon == GearLook.RuneStaff ? runeBolt
                : style.weapon == GearLook.Arquebus || style.weapon == GearLook.Pistol || style.weapon == GearLook.Rifle || style.weapon == GearLook.Musket ? ball
                : style.weapon == GearLook.LeydenRod ? spark
                : style.weapon == GearLook.RayGun || style.weapon == GearLook.RayRifle ? rayBolt
                : style.weapon == GearLook.BoltRifle ? ball : style.weapon == GearLook.MachineGun || style.weapon == GearLook.Tommygun ? bullet
                : style.weapon == GearLook.Grenade ? grenade : style.weapon == GearLook.AetherRod ? aetherBolt
                : style.weapon == GearLook.Flask ? flask : style.weapon == GearLook.Cannon ? cannonball : null;
            SetFlight(def, style.weapon);
            EditorUtility.SetDirty(def);
        }
        if (database == null) return;
        foreach (var (buildingId, name, style) in PixelCrews)
        {
            if (only != null && !only.Contains(buildingId)) continue;
            var tower = database.GetBuilding(buildingId);
            if (tower == null) continue;
            string dir = $"Units/{name}";
            ResetDir(dir);
            Sprite Frame(string frame, int f)
            {
                var c = PixelFigure.Draw(style, f);
                PixelFigure.CanvasFor(style, out _, out _, out _, out int gy);
                c.ClearBelow(gy + CrewCutRows);   // just below the belt: the parapet hides the rest
                return SavePixel(c, $"{dir}/{name}_{frame}.png", UnitPPU, PixelFigure.PivotFor(style));
            }
            tower.operatorIdle = Frame("Idle", 0);
            tower.operatorShoot = new List<Sprite> { Frame("Shoot0", 5), Frame("Shoot1", 6), Frame("Shoot2", 7), Frame("Shoot3", 8) };
            bool bolts = style.weapon == GearLook.Ballista || style.weapon == GearLook.Crossbow;
            bool gun = style.weapon == GearLook.Cannon;
            tower.towerProjectile = gun ? cannonball : bolts ? bolt : arrow;
            tower.towerProjectileArc = gun ? 0.06f : bolts ? 0f : Projectile.BowArc;   // bolts fly straight, arrows are lobbed
            tower.towerProjectileSpeed = gun ? 16f : bolts ? 15f : 11f;
            tower.towerSplash = gun ? 1.6f : 0f;
            tower.towerMuzzleSmoke = gun;
            tower.operatorReleaseFrame = 2;
            EditorUtility.SetDirty(tower);
        }
    }

    /// <summary>How each kind of shot flies: bolts straight and fast, arrows in an arc, javelins lobbed high, stones low.</summary>
    private static void SetFlight(UnitDefinition def, GearLook weapon)
    {
        def.splashRadius = 0f;
        def.splashFire = false;
        def.muzzleSmoke = false;
        switch (weapon)
        {
            case GearLook.Crossbow: def.projectileArc = 0f; def.projectileSpeed = 15f; break;
            case GearLook.RuneStaff: def.projectileArc = 0f; def.projectileSpeed = 12f; break;
            case GearLook.Javelin: def.projectileArc = 0.22f; break;
            case GearLook.Arquebus: case GearLook.Pistol: def.projectileArc = 0f; def.projectileSpeed = 22f; def.muzzleSmoke = true; break;
            case GearLook.Rifle: def.projectileArc = 0f; def.projectileSpeed = 26f; def.muzzleSmoke = true; break;
            case GearLook.Musket: def.projectileArc = 0f; def.projectileSpeed = 24f; def.muzzleSmoke = true; break;
            case GearLook.BoltRifle: def.projectileArc = 0f; def.projectileSpeed = 28f; def.muzzleSmoke = true; break;
            case GearLook.RayGun: case GearLook.RayRifle: def.projectileArc = 0f; def.projectileSpeed = 30f; break;
            case GearLook.MachineGun: case GearLook.Tommygun: def.projectileArc = 0f; def.projectileSpeed = 30f; break;
            case GearLook.LeydenRod: def.projectileArc = 0f; def.projectileSpeed = 16f; def.splashRadius = 0.7f; break;
            case GearLook.Grenade: def.projectileArc = 0.4f; def.projectileSpeed = 8f; def.splashRadius = 1.5f; break;
            case GearLook.AetherRod: def.projectileArc = 0f; def.projectileSpeed = 18f; def.splashRadius = 0.8f; break;
            case GearLook.Flask: def.projectileArc = 0.35f; def.projectileSpeed = 8f; def.splashRadius = 1.4f; def.splashFire = true; break;
            case GearLook.Cannon: def.projectileArc = 0.06f; def.projectileSpeed = 16f; def.splashRadius = 1.8f; def.muzzleSmoke = true; break;
            case GearLook.Sling: def.projectileArc = 0.08f; break;
            default: def.projectileArc = Projectile.BowArc; break;
        }
    }

    /// <summary>Rows above the feet where tower crews are cut (BuildingView.CrewCutAbovePivot must match).</summary>
    private const int CrewCutRows = 8;

    /// <summary>Draw a unit's 12 frames and build its prefab. Returns the prefab.</summary>
    private static GameObject BuildPixelUnit(string name, FigureStyle style, bool isEnemy, UnitDefinition definition, bool boss = false)
    {
        string dir = $"Units/{name}";
        ResetDir(dir);
        float ppu = UnitPPU;
        var pivot = PixelFigure.PivotFor(style);
        string[] names = { "Idle", "Walk0", "Walk1", "Walk2", "Walk3", "Attack0", "Attack1", "Attack2", "Attack3", "Death0", "Death1", "Death2" };
        var frames = new Sprite[PixelFigure.FrameCount];
        PixelCanvas idleCanvas = null;
        for (int f = 0; f < frames.Length; f++)
        {
            var c = PixelFigure.Draw(style, f);
            if (f == 0) idleCanvas = c;
            frames[f] = SavePixel(c, $"{dir}/{name}_{names[f]}.png", ppu, pivot);
        }

        // Size things from the idle frame: the top of the figure, its width.
        PixelFigure.CanvasFor(style, out int w, out int h, out int cx, out int gy);
        int top = gy, left = w, right = 0;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (idleCanvas.Get(x, y).a > 200)
                {
                    top = Mathf.Max(top, y);
                    left = Mathf.Min(left, x);
                    right = Mathf.Max(right, x);
                }
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
        visual.figure = style;
        visual.pixelsPerUnit = ppu;
        visual.weaponClass = style.Class;
        visual.barHeight = height + 3f / ppu;
        visual.barWidth = Mathf.Clamp(width * 0.8f, 12f / ppu, 40f / ppu);
        visual.alwaysShowBar = boss;
        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = Mathf.Max(width, height) * 0.32f;
        col.offset = new Vector2((left + right) * 0.5f / ppu - (cx + 0.5f) / ppu, height * 0.45f);
        go.AddComponent<MouseTouching>();
        Unit unit = isEnemy ? go.AddComponent<Enemy>() : (Unit)go.AddComponent<Troop>();
        unit.definition = definition;
        unit.personalSpace = (style.mount == MountKind.Elephant ? 0.95f : style.mount != MountKind.None ? 0.6f : 0.38f) * style.scale;
        if (boss) go.AddComponent<Boss>();

        string prefabPath = $"Assets/Prefabs/Units/{name}Unit.prefab";
        Directory.CreateDirectory("Assets/Prefabs/Units");
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        Object.DestroyImmediate(go);
        return prefab;
    }

    private static void ResetDir(string dir)
    {
        string full = Path.Combine(Root, dir);
        Directory.CreateDirectory(full);
        foreach (var old in Directory.GetFiles(full, "*.png")) AssetDatabase.DeleteAsset(old.Replace('\\', '/'));
    }

    /// <summary>Save a pixel canvas as a crisp sprite (point filtered, uncompressed).</summary>
    private static Sprite SavePixel(PixelCanvas canvas, string relativePath, float ppu, Vector2 pivot, bool tile = false)
    {
        string path = $"{Root}/{relativePath}";
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var tex = canvas.ToTexture();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = ppu;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = tile ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = pivot;
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteExtrude = 0;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // ---------- Projectiles (pointing right, centred) ----------

    private static Sprite PixelProjectile(string name, int w, int h, System.Action<PixelCanvas> draw)
    {
        var c = new PixelCanvas(w, h);
        draw(c);
        c.Outline(PixelPalette.Ink, 0.5f);
        return SavePixel(c, $"Units/Projectiles/{name}.png", PixelsPerTile, new Vector2(0.5f, 0.5f));
    }

    private static void DrawArrow(PixelCanvas c)
    {
        var P = PixelPalette.Wood;
        c.LineRaw(new Vector2(2, 2), new Vector2(9, 2), P[3]);
        c.Put(9, 2, PixelPalette.Steel[3]); c.Put(10, 2, PixelPalette.Steel[2]);
        c.Put(1, 1, PixelPalette.White); c.Put(1, 3, PixelPalette.White); c.Put(2, 1, PixelPalette.Linen[3]); c.Put(2, 3, PixelPalette.Linen[3]);
    }

    private static void DrawBolt(PixelCanvas c)
    {
        c.LineRaw(new Vector2(2, 2), new Vector2(12, 2), PixelPalette.Wood[2], 2);
        c.Rect(12, 1, 13, 4, PixelPalette.Steel[2]); c.Put(14, 2, PixelPalette.Steel[3]); c.Put(14, 3, PixelPalette.Steel[3]);
        c.Put(1, 1, PixelPalette.Linen[3]); c.Put(1, 4, PixelPalette.Linen[3]);
    }

    private static void DrawStone(PixelCanvas c)
    {
        c.Rect(1, 1, 3, 3, PixelPalette.Stone[2]); c.Put(1, 3, PixelPalette.Stone[4]); c.Put(3, 1, PixelPalette.Stone[1]);
    }

    private static void DrawRuneBolt(PixelCanvas c)
    {
        // a streak of rune-light with a bright head
        var r = PixelPalette.Rune;
        c.LineRaw(new Vector2(1, 3), new Vector2(7, 3), r[1]);
        c.LineRaw(new Vector2(4, 3), new Vector2(8, 3), r[2]);
        c.Ellipse(new Vector2(9, 3), 2.2f, 2.2f, r);
        c.Put(9, 4, r[3]); c.Put(2, 2, r[1]); c.Put(3, 4, r[1]);
    }

    private static void DrawJavelin(PixelCanvas c)
    {
        c.LineRaw(new Vector2(1, 2), new Vector2(13, 2), PixelPalette.Wood[3]);
        c.Put(6, 2, PixelPalette.Blue[2]);
        c.Rect(14, 1, 14, 3, PixelPalette.Steel[2]); c.Put(15, 2, PixelPalette.Steel[3]); c.Put(16, 2, PixelPalette.Steel[3]);
    }

    // ---------- Raider chiefs ----------

    private struct BossSpec
    {
        public string id, name, age;
        public FigureStyle style;
        public int health, attack;
        public float speed, cooldown, range, detection;
        public BossAbility ability;
        public float abilityCooldown, abilityRadius, abilityPower;
        public string summons;
        public int summonCount;
    }

    private static BossSpec[] BossSpecs => new[]
    {
        new BossSpec
        {
            id = "BossCaveBear", name = "Ugruk the Cave-Bear", age = "StoneAge",
            style = Fig(Rd, GearLook.BearPelt, GearLook.Club, body: GearLook.Bare, cape: "Red", scale: 1.8f, raider: true, necklace: true, warpaint: true),
            health = 900, attack = 24, speed = 0.75f, cooldown = 1.3f, range = 0.8f, detection = 3.5f,
            ability = BossAbility.Slam, abilityCooldown = 7f, abilityRadius = 2.0f, abilityPower = 1.2f,
        },
        new BossSpec
        {
            id = "BossChariotLord", name = "Sargon the Chariot Lord", age = "BronzeAge",
            style = Fig(Rd, GearLook.Crown, GearLook.Spear, mount: MountKind.Chariot, cape: "Red", scale: 1.3f, raider: true, coat: "DarkHorse"),
            health = 1400, attack = 30, speed = 1.3f, cooldown = 1.1f, range = 0.95f, detection = 4.5f,
            ability = BossAbility.WarCry, abilityCooldown = 12f, abilityPower = 1.3f,
        },
        new BossSpec
        {
            id = "BossIronKing", name = "Brenn the Iron King", age = "IronAge",
            style = Fig(Rd, GearLook.Crown, GearLook.Axe, GearLook.OvalShield, GearLook.Mail, cape: "Purple", shieldColor: "Iron", scale: 1.7f, raider: true, necklace: true),
            health = 2000, attack = 40, speed = 0.85f, cooldown = 1.1f, range = 0.8f, detection = 3.5f,
            ability = BossAbility.Summon, abilityCooldown = 15f, summons = "IronRaider", summonCount = 2,
        },
        new BossSpec
        {
            id = "BossChampion", name = "Crixus the Champion", age = "ClassicalAge",
            style = Fig(Rd, GearLook.PlumedHelm, GearLook.Sword, GearLook.RoundShield, GearLook.Plate, crest: "Purple", shieldColor: "Purple", cape: "Purple", scale: 1.7f, raider: true),
            health = 2700, attack = 52, speed = 1.0f, cooldown = 0.9f, range = 0.8f, detection = 3.5f,
            ability = BossAbility.Slam, abilityCooldown = 6f, abilityRadius = 2.3f, abilityPower = 1.3f,
        },
        new BossSpec
        {
            id = "BossBlackKnight", name = "The Black Knight", age = "MedievalAge",
            style = Fig(Rd, GearLook.GreatHelm, GearLook.Lance, GearLook.KiteShield, GearLook.Surcoat, MountKind.Horse, crest: "Coal", shieldColor: "Coal",
                cape: "Coal", scale: 1.35f, raider: true, coat: "DarkHorse", barding: true),
            health = 3400, attack = 60, speed = 1.4f, cooldown = 1.0f, range = 1.1f, detection = 4.5f,
            ability = BossAbility.WarCry, abilityCooldown = 11f, abilityPower = 1.35f,
        },
        new BossSpec
        {
            id = "BossWarlord", name = "Morcar, Warlord of the Marches", age = "MedievalAge",
            style = Fig(Rd, GearLook.Crown, GearLook.Mace, GearLook.KiteShield, GearLook.Surcoat, crest: "Purple", shieldColor: "Purple", cape: "Purple", scale: 1.7f, raider: true),
            health = 4200, attack = 64, speed = 0.9f, cooldown = 1.0f, range = 0.85f, detection = 3.5f,
            ability = BossAbility.Summon, abilityCooldown = 14f, summons = "Brigand", summonCount = 3,
        },
        new BossSpec
        {
            id = "BossCondottiere", name = "Sforza the Condottiere", age = "RenaissanceAge",
            style = Fig(Rd, GearLook.Morion, GearLook.Zweihander, body: GearLook.Cuirass, crest: "Gold", cape: "Purple", scale: 1.7f, raider: true),
            health = 5200, attack = 78, speed = 0.95f, cooldown = 1.1f, range = 0.9f, detection = 3.5f,
            ability = BossAbility.Summon, abilityCooldown = 14f, summons = "Landsknecht", summonCount = 3,
        },
        new BossSpec
        {
            id = "BossMadAlchemist", name = "Paracel the Mad Alchemist", age = "RenaissanceAge",
            style = Fig(Rd, GearLook.PlagueMask, GearLook.Flask, body: GearLook.Robe, crest: "Green", cape: "Coal", scale: 1.6f, raider: true),
            health = 4200, attack = 60, speed = 0.85f, cooldown = 1.6f, range = 4.5f, detection = 5f,
            ability = BossAbility.Slam, abilityCooldown = 8f, abilityRadius = 2.4f, abilityPower = 1.2f,
        },
        new BossSpec
        {
            id = "BossPirateKing", name = "Captain Blackwater, King of the Privateers", age = "EnlightenmentAge",
            style = Fig(Rd, GearLook.Tricorne, GearLook.Sabre, body: GearLook.Greatcoat, crest: "Gold", cape: "Red", scale: 1.7f, raider: true),
            health = 6000, attack = 86, speed = 1.0f, cooldown = 1.0f, range = 0.9f, detection = 3.5f,
            ability = BossAbility.Summon, abilityCooldown = 13f, summons = "Privateer", summonCount = 3,
        },
        new BossSpec
        {
            id = "BossGalvanisedGiant", name = "The Galvanised Giant", age = "EnlightenmentAge",
            style = Skin(Fig(Rd, GearLook.Bare, GearLook.Club, body: GearLook.Bare, cape: "Coal", scale: 2.0f, raider: true), "Troll"),
            health = 5600, attack = 80, speed = 0.75f, cooldown = 1.4f, range = 1.0f, detection = 3.5f,
            ability = BossAbility.Slam, abilityCooldown = 6.5f, abilityRadius = 2.6f, abilityPower = 1.3f,
        },
        new BossSpec
        {
            id = "BossGanglandBoss", name = "Big Al, the Gangland Boss", age = "ModernAge",
            style = Fig(Rd, GearLook.Fedora, GearLook.Tommygun, body: GearLook.TrenchCoat, crest: "Red", cape: "Coal", scale: 1.6f, raider: true),
            health = 7200, attack = 30, speed = 0.9f, cooldown = 0.5f, range = 4.5f, detection = 5f,
            ability = BossAbility.Summon, abilityCooldown = 13f, summons = "Gangster", summonCount = 3,
        },
        new BossSpec
        {
            id = "BossLandship", name = "Baron von Panzer in his Landship", age = "ModernAge",
            style = Fig(Rd, GearLook.Goggles, GearLook.Cannon, body: GearLook.Fatigues, mount: MountKind.Tank, scale: 1.3f, raider: true),
            health = 8400, attack = 160, speed = 0.6f, cooldown = 3.5f, range = 8f, detection = 8.5f,
            ability = BossAbility.WarCry, abilityCooldown = 12f, abilityPower = 1.35f,
        },
        new BossSpec
        {
            id = "BossDoctorZed", name = "Doctor Zed and his Death Ray", age = "ElectricAge",
            style = Fig(Rd, GearLook.BubbleHelmet, GearLook.RayRifle, body: GearLook.Jumpsuit, crest: "Purple", cape: "Purple", scale: 1.6f, raider: true),
            health = 7400, attack = 110, speed = 0.9f, cooldown = 1.4f, range = 7f, detection = 7.5f,
            ability = BossAbility.Summon, abilityCooldown = 13f, summons = "RoboRaider", summonCount = 2,
        },
        new BossSpec
        {
            id = "BossMegaRobot", name = "The Mega-Robot", age = "ElectricAge",
            style = Skin(Fig(Rd, GearLook.RobotHead, GearLook.Claw, body: GearLook.ChromePlate, scale: 2.2f, raider: true), "Chrome"),
            health = 9800, attack = 130, speed = 0.7f, cooldown = 1.3f, range = 1.1f, detection = 3.5f,
            ability = BossAbility.Slam, abilityCooldown = 6f, abilityRadius = 2.8f, abilityPower = 1.4f,
        },
        new BossSpec
        {
            id = "BossIronBaron", name = "Baron Ferrous in his Iron Suit", age = "IndustrialAge",
            style = Fig(Rd, GearLook.BoilerHelm, GearLook.PistonHammer, body: GearLook.BrassPlate, cape: "Coal", scale: 1.8f, raider: true),
            health = 6800, attack = 96, speed = 0.8f, cooldown = 1.3f, range = 0.95f, detection = 3.5f,
            ability = BossAbility.Slam, abilityCooldown = 6.5f, abilityRadius = 2.6f, abilityPower = 1.35f,
        },
        new BossSpec
        {
            id = "BossMadEngineer", name = "Professor Cogsworth, Mad Engineer", age = "IndustrialAge",
            style = Fig(Rd, GearLook.TopHat, GearLook.AetherRod, body: GearLook.Greatcoat, crest: "Aether", cape: "Purple", scale: 1.6f, raider: true),
            health = 5200, attack = 72, speed = 0.85f, cooldown = 1.5f, range = 5f, detection = 5.5f,
            ability = BossAbility.Summon, abilityCooldown = 13f, summons = "Saboteur", summonCount = 3,
        },
    };

    [MenuItem("Tools/Age of Stickman/Setup Bosses")]
    public static void SetupBosses() => SetupBosses(null);

    /// <summary>Set up raider chiefs; 'onlyAge' limits it to that age's chiefs.</summary>
    private static void SetupBosses(string onlyAge)
    {
        foreach (var spec in BossSpecs)
        {
            if (onlyAge != null && spec.age != onlyAge) continue;
            string path = $"Assets/Data/Units/{spec.id}.asset";
            var def = AssetDatabase.LoadAssetAtPath<UnitDefinition>(path);
            if (def == null)
            {
                def = ScriptableObject.CreateInstance<UnitDefinition>();
                AssetDatabase.CreateAsset(def, path);
            }
            var age = AssetDatabase.LoadAssetAtPath<Age>($"Assets/Data/Ages/{spec.age}.asset");
            var raider = age != null && age.enemyUnits.Count > 0 ? age.enemyUnits[0] : null;
            def.displayName = spec.name;
            def.age = age;
            def.troopClass = raider != null ? raider.troopClass : null;
            def.maxHealth = spec.health;
            def.attackPower = spec.attack;
            def.moveSpeed = spec.speed;
            def.attackCooldown = spec.cooldown;
            def.attackRange = spec.range;
            def.detectionRange = spec.detection;
            def.hitDelay = 0.3f;
            def.isBoss = true;
            def.bossAbility = spec.ability;
            def.abilityCooldown = spec.abilityCooldown;
            def.abilityRadius = spec.abilityRadius > 0 ? spec.abilityRadius : 2.2f;
            def.abilityPower = spec.abilityPower > 0 ? spec.abilityPower : 1.3f;
            def.summons = spec.summons != null ? AssetDatabase.LoadAssetAtPath<UnitDefinition>($"Assets/Data/Units/{spec.summons}.asset") : null;
            def.summonCount = spec.summonCount;
            // A chief with a ranged weapon shoots like the troops who carry it.
            string shot = spec.style.weapon switch
            {
                GearLook.Bow => "Arrow", GearLook.Crossbow => "Bolt", GearLook.Flask => "FireFlask",
                GearLook.Arquebus or GearLook.Pistol or GearLook.Rifle or GearLook.Musket => "MusketBall", GearLook.RuneStaff => "RuneBolt",
                GearLook.LeydenRod => "GalvanicSpark", GearLook.BoltRifle => "MusketBall",
                GearLook.MachineGun or GearLook.Tommygun => "Bullet", GearLook.Cannon => "Cannonball",
                GearLook.RayGun or GearLook.RayRifle => "RayBolt",
                GearLook.Grenade => "Grenade", GearLook.AetherRod => "AetherBolt", _ => null,
            };
            def.projectile = shot != null ? AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/Units/Projectiles/{shot}.png") : null;
            SetFlight(def, spec.style.weapon);
            def.prefab = BuildPixelUnit(spec.id, spec.style, true, def, boss: true);
            def.icon = AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/Units/{spec.id}/{spec.id}_Idle.png");
            EditorUtility.SetDirty(def);
            if (age != null)
            {
                age.bosses.RemoveAll(b => b == null);
                if (!age.bosses.Contains(def)) age.bosses.Add(def);
                EditorUtility.SetDirty(age);
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Bosses set up.");
    }
}
