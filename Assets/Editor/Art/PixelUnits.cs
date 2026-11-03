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
        // Natives of other worlds
        ("Sandfolk_Skirmisher", Skin(Fig(Rd, GearLook.Hood, GearLook.Javelin, body: GearLook.Jerkin, raider: true), "Sandskin"), true),
        ("Sandfolk_Archer", Skin(Fig(Rd, GearLook.Headband, GearLook.Bow, body: GearLook.Jerkin, raider: true), "Sandskin"), true),
        ("Frostkin_Brute", Skin(Fig(Rd, GearLook.BearPelt, GearLook.Club, body: GearLook.Bare, scale: 1.3f, raider: true), "Frostskin"), true),
        ("Frostkin_Hurler", Skin(Fig(Rd, GearLook.Hood, GearLook.Javelin, body: GearLook.HideVest, raider: true), "Frostskin"), true),
        ("Bogfolk_Lurker", Skin(Fig(Rd, GearLook.None, GearLook.Spear, body: GearLook.Bare, raider: true), "Bogskin"), true),
        ("Bogfolk_Witch", Skin(Fig(Rd, GearLook.Hood, GearLook.RuneStaff, body: GearLook.Robe, crest: "Green", raider: true), "Bogskin"), true),
        ("Ashborn_Brute", Skin(Fig(Rd, GearLook.None, GearLook.Axe, body: GearLook.Bare, scale: 1.2f, raider: true, warpaint: true), "Ashskin"), true),
        ("Ashborn_Thrower", Skin(Fig(Rd, GearLook.Hood, GearLook.Flask, body: GearLook.Jerkin, raider: true), "Ashskin"), true),
        ("Shardkin_Warden", Skin(Fig(Rd, GearLook.GolemHead, GearLook.Sword, body: GearLook.GolemBody, scale: 1.2f, raider: true), "Shardskin"), true),
        ("Shardkin_Slinger", Skin(Fig(Rd, GearLook.None, GearLook.Sling, body: GearLook.Bare, raider: true), "Shardskin"), true),
        ("Tidefolk_Raider", Skin(Fig(Rd, GearLook.None, GearLook.Spear, GearLook.OvalShield, body: GearLook.Bare, raider: true, necklace: true), "Reefskin"), true),
        ("Tidefolk_Archer", Skin(Fig(Rd, GearLook.Headband, GearLook.Bow, body: GearLook.Bare, raider: true), "Reefskin"), true),
        ("Wildfolk_Warden", Skin(Fig(Rd, GearLook.Hood, GearLook.Sword, body: GearLook.Jerkin, raider: true), "Barkskin"), true),
        ("Wildfolk_Hunter", Skin(Fig(Rd, GearLook.Feather, GearLook.Bow, body: GearLook.HideVest, raider: true), "Barkskin"), true),
        // Astral Age (magic and sci-fi fused)
        ("Starknight", Fig(B, GearLook.StarHelm, GearLook.StarBlade, body: GearLook.StarPlate, cape: "Astral"), false),
        ("Technomancer", Fig(B, GearLook.Cowl, GearLook.BeamStaff, body: GearLook.Spellweave, crest: "Astral"), false),
        ("Skylancer", Fig(B, GearLook.StarHelm, GearLook.ManaRifle, body: GearLook.StarPlate, mount: MountKind.Skybike), false),
        ("Starmender", Medic(Fig(B, GearLook.StarHelm, GearLook.None, body: GearLook.StarPlate)), false),
        ("NovaThrone", Fig(B, GearLook.Cowl, GearLook.Cannon, body: GearLook.Spellweave, crest: "Astral", mount: MountKind.HoverThrone, scale: 1.35f), false),
        ("AstralConstruct", Skin(Fig(B, GearLook.SynthHead, GearLook.StarBlade, body: GearLook.StarPlate, scale: 1.55f), "Synth"), false),
        ("StarFrigate", Fig(B, GearLook.StarHelm, GearLook.Cannon, body: GearLook.StarPlate, mount: MountKind.Starship), false),
        ("Voidreaver", Fig(Rd, GearLook.VoidHelm, GearLook.StarBlade, body: GearLook.VoidPlate, raider: true), true),
        ("VoidCaster", Fig(Rd, GearLook.VoidHelm, GearLook.BeamStaff, body: GearLook.Robe, crest: "Void", raider: true), true),
        ("VoidSkiff", Fig(Rd, GearLook.VoidHelm, GearLook.ManaRifle, body: GearLook.VoidPlate, mount: MountKind.Skybike, raider: true), true),
        // Arcane Age (fantasy)
        ("Spellblade", Fig(B, GearLook.RuneVisor, GearLook.Runeblade, body: GearLook.RunePlate, crest: "Arcane"), false),
        ("Arcanist", Fig(B, GearLook.WizardHat, GearLook.ManaRifle, body: GearLook.Spellweave, crest: "Arcane"), false),
        ("GriffinRider", Fig(B, GearLook.RuneVisor, GearLook.Javelin, body: GearLook.RunePlate, mount: MountKind.Griffin, coat: "Griffin"), false),
        ("Lightbringer", Medic(Fig(B, GearLook.WizardHat, GearLook.None, body: GearLook.Spellweave, crest: "Linen")), false),
        ("WarDrake", Fig(B, GearLook.RuneVisor, GearLook.Runeblade, body: GearLook.RunePlate, mount: MountKind.Drake, crest: "Arcane", scale: 1.15f), false),
        ("RunicGolem", Skin(Fig(B, GearLook.GolemHead, GearLook.None, body: GearLook.GolemBody, scale: 1.5f), "Rock"), false),
        ("Skyship", Fig(B, GearLook.RuneVisor, GearLook.Grenade, body: GearLook.RunePlate, mount: MountKind.Airship, crest: "Arcane"), false),
        ("Hexblade", Fig(Rd, GearLook.Goggles, GearLook.Runeblade, body: GearLook.TacVest, raider: true), true),
        ("Warlock", Fig(Rd, GearLook.Cowl, GearLook.ManaStaff, body: GearLook.Robe, crest: "Purple", raider: true), true),
        ("WyvernRider", Fig(Rd, GearLook.Goggles, GearLook.Javelin, body: GearLook.Jerkin, mount: MountKind.Griffin, coat: "Wyvern", raider: true), true),
        // Information Age (cyber)
        ("Commando", Fig(B, GearLook.TacHelmet, GearLook.AssaultRifle, body: GearLook.TacVest), false),
        ("PlasmaGunner", Fig(B, GearLook.TacHelmet, GearLook.PlasmaRifle, body: GearLook.TacVest), false),
        ("Drone", Fig(B, GearLook.None, GearLook.MachineGun, mount: MountKind.Drone), false),
        ("Nanomedic", Medic(Fig(B, GearLook.TacHelmet, GearLook.None, body: GearLook.TacVest)), false),
        ("PlasmaTank", Fig(B, GearLook.TacHelmet, GearLook.Cannon, body: GearLook.TacVest, mount: MountKind.HoverTank), false),
        ("Android", Skin(Fig(B, GearLook.SynthHead, GearLook.EnergyBlade, body: GearLook.SynthBody, scale: 1.2f), "Synth"), false),
        ("JetFighter", Fig(B, GearLook.None, GearLook.PlasmaRifle, mount: MountKind.Jet), false),
        ("CyberThug", Fig(Rd, GearLook.Cap, GearLook.EnergyBlade, body: GearLook.Jerkin, raider: true), true),
        ("Hacker", Fig(Rd, GearLook.Goggles, GearLook.AssaultRifle, body: GearLook.Jerkin, raider: true), true),
        ("RogueDrone", Fig(Rd, GearLook.None, GearLook.MachineGun, mount: MountKind.Drone, raider: true), true),
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
    private static FigureStyle Lich(FigureStyle s) { s.skin = "Bone"; s.undead = true; return s; }

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
        var plasmaBolt = PixelProjectile("PlasmaBolt", 10, 5, c =>
        {
            var n = PixelPalette.Neon;
            c.LineRaw(new Vector2(0, 2), new Vector2(5, 2), n[1]); c.Ellipse(new Vector2(7, 2), 2, 2, n); c.Put(7, 3, n[3]);
        });
        var manaBolt = PixelProjectile("ManaBolt", 11, 7, c =>
        {
            var m = PixelPalette.Mana;
            c.LineRaw(new Vector2(0, 3), new Vector2(5, 3), m[1]); c.LineRaw(new Vector2(2, 4), new Vector2(6, 4), m[2]);
            c.Ellipse(new Vector2(8, 3.5f), 2.4f, 2.4f, m); c.Put(8, 4, PixelPalette.White);
        });
        var starBolt = PixelProjectile("StarBolt", 12, 7, c =>
        {
            var g = PixelPalette.Gold;
            c.LineRaw(new Vector2(0, 3), new Vector2(6, 3), PixelPalette.Arcane[2]); c.LineRaw(new Vector2(2, 4), new Vector2(7, 4), g[2]);
            c.Ellipse(new Vector2(9, 3.5f), 2.4f, 2.4f, new[] { g[1], g[2], g[3], PixelPalette.White });
            c.Put(9, 6, g[3]); c.Put(9, 1, g[3]); c.Put(11, 4, PixelPalette.White);
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
                : style.weapon == GearLook.PlasmaRifle ? plasmaBolt : style.weapon == GearLook.AssaultRifle ? bullet
                : style.weapon == GearLook.BoltRifle ? ball : style.weapon == GearLook.MachineGun || style.weapon == GearLook.Tommygun ? bullet
                : style.weapon == GearLook.Grenade ? grenade : style.weapon == GearLook.AetherRod ? aetherBolt
                : style.weapon == GearLook.Flask ? flask : style.weapon == GearLook.Cannon ? cannonball
                : style.weapon == GearLook.ManaStaff || style.weapon == GearLook.ManaRifle ? manaBolt
                : style.weapon == GearLook.BeamStaff ? starBolt : null;
            SetFlight(def, style.weapon);
            if (style.mount == MountKind.Starship) StarshipGun(def, starBolt);
            if (style.mount == MountKind.HoverThrone) NovaGun(def);
            if (style.mount == MountKind.Drake) DrakeFire(def);
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
            case GearLook.AssaultRifle: def.projectileArc = 0f; def.projectileSpeed = 32f; break;
            case GearLook.PlasmaRifle: def.projectileArc = 0f; def.projectileSpeed = 30f; def.splashRadius = 0.5f; break;
            case GearLook.MachineGun: case GearLook.Tommygun: def.projectileArc = 0f; def.projectileSpeed = 30f; break;
            case GearLook.LeydenRod: def.projectileArc = 0f; def.projectileSpeed = 16f; def.splashRadius = 0.7f; break;
            case GearLook.Grenade: def.projectileArc = 0.4f; def.projectileSpeed = 8f; def.splashRadius = 1.5f; break;
            case GearLook.AetherRod: def.projectileArc = 0f; def.projectileSpeed = 18f; def.splashRadius = 0.8f; break;
            case GearLook.ManaStaff: def.projectileArc = 0f; def.projectileSpeed = 20f; def.splashRadius = 0.9f; break;
            case GearLook.ManaRifle: def.projectileArc = 0f; def.projectileSpeed = 28f; def.splashRadius = 0.7f; break;
            case GearLook.BeamStaff: def.projectileArc = 0f; def.projectileSpeed = 26f; def.splashRadius = 0.8f; break;
            case GearLook.Flask: def.projectileArc = 0.35f; def.projectileSpeed = 8f; def.splashRadius = 1.4f; def.splashFire = true; break;
            case GearLook.Cannon: def.projectileArc = 0.06f; def.projectileSpeed = 16f; def.splashRadius = 1.8f; def.muzzleSmoke = true; break;
            case GearLook.Sling: def.projectileArc = 0.08f; break;
            default: def.projectileArc = Projectile.BowArc; break;
        }
    }

    /// <summary>A starship's nose cannon fires bolts of starlight, straight and fast, with no smoke.</summary>
    private static void StarshipGun(UnitDefinition def, Sprite starBolt)
    {
        def.projectile = starBolt;
        def.projectileArc = 0f; def.projectileSpeed = 26f; def.splashRadius = 1.2f; def.muzzleSmoke = false;
    }

    /// <summary>A war drake breathes fireballs that burst and set the ground alight.</summary>
    private static void DrakeFire(UnitDefinition def)
    {
        var fire = AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/Units/Projectiles/Fireball.png");
        if (fire != null) def.projectile = fire;
        def.projectileArc = 0.1f; def.projectileSpeed = 13f; def.splashRadius = 1.7f; def.splashFire = true; def.muzzleSmoke = false;
    }

    /// <summary>A hover throne's cannon lobs novas that burst wide.</summary>
    private static void NovaGun(UnitDefinition def)
    {
        var nova = AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/Units/Projectiles/Nova.png");
        if (nova != null) def.projectile = nova;
        def.projectileArc = 0.15f; def.projectileSpeed = 14f; def.splashRadius = 2.0f; def.muzzleSmoke = false;
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
        /// <summary>A native chief of another world: kept on the natives list, not its age's chiefs.</summary>
        public bool native;
    }

    private static BossSpec[] BossSpecs => new[]
    {
        // Chiefs of the native peoples of other worlds (they lead their camps and raids there, never at home).
        new BossSpec
        {
            id = "Sandfolk_Chief", name = "The Dune Warlord", age = "AstralAge", native = true,
            style = Skin(Fig(Rd, GearLook.Crown, GearLook.Axe, body: GearLook.Scale, cape: "Gold", scale: 1.7f, raider: true), "Sandskin"),
            health = 12000, attack = 160, speed = 0.9f, cooldown = 1.3f, range = 1.1f, detection = 4f,
            ability = BossAbility.WarCry, abilityCooldown = 11f, abilityPower = 1.35f,
        },
        new BossSpec
        {
            id = "Frostkin_Chief", name = "The Frost Matriarch", age = "AstralAge", native = true,
            style = Skin(Fig(Rd, GearLook.Crown, GearLook.Spear, body: GearLook.HideVest, cape: "Wolf", scale: 1.8f, raider: true), "Frostskin"),
            health = 13000, attack = 150, speed = 0.85f, cooldown = 1.3f, range = 1.3f, detection = 4f,
            ability = BossAbility.Slam, abilityCooldown = 7f, abilityRadius = 2.8f, abilityPower = 1.4f,
        },
        new BossSpec
        {
            id = "Bogfolk_Chief", name = "The Mire King", age = "AstralAge", native = true,
            style = Skin(Fig(Rd, GearLook.Crown, GearLook.Mace, body: GearLook.Bare, scale: 1.9f, raider: true, necklace: true), "Bogskin"),
            health = 14000, attack = 150, speed = 0.75f, cooldown = 1.4f, range = 1.1f, detection = 4f,
            ability = BossAbility.Summon, abilityCooldown = 12f, summons = "Bogfolk_Lurker", summonCount = 3,
        },
        new BossSpec
        {
            id = "Ashborn_Chief", name = "The Cinder Lord", age = "AstralAge", native = true,
            style = Skin(Fig(Rd, GearLook.GreatHelm, GearLook.Zweihander, body: GearLook.Plate, cape: "Coal", scale: 1.8f, raider: true), "Ashskin"),
            health = 14500, attack = 175, speed = 0.8f, cooldown = 1.4f, range = 1.2f, detection = 4f,
            ability = BossAbility.Slam, abilityCooldown = 6.5f, abilityRadius = 3f, abilityPower = 1.5f,
        },
        new BossSpec
        {
            id = "Shardkin_Chief", name = "The Prism Colossus", age = "AstralAge", native = true,
            style = Skin(Fig(Rd, GearLook.GolemHead, GearLook.None, body: GearLook.GolemBody, scale: 2.3f, raider: true), "Shardskin"),
            health = 16000, attack = 170, speed = 0.6f, cooldown = 1.4f, range = 1.2f, detection = 3.5f,
            ability = BossAbility.Slam, abilityCooldown = 6f, abilityRadius = 3f, abilityPower = 1.5f,
        },
        new BossSpec
        {
            id = "Tidefolk_Chief", name = "The Tide Caller", age = "AstralAge", native = true,
            style = Skin(Fig(Rd, GearLook.Crown, GearLook.ManaStaff, body: GearLook.Robe, crest: "Mana", cape: "Mana", scale: 1.7f, raider: true), "Reefskin"),
            health = 11000, attack = 140, speed = 0.85f, cooldown = 1.6f, range = 7f, detection = 7.5f,
            ability = BossAbility.Summon, abilityCooldown = 12f, summons = "Tidefolk_Raider", summonCount = 3,
        },
        new BossSpec
        {
            id = "Wildfolk_Chief", name = "The Green Knight", age = "AstralAge", native = true,
            style = Skin(Fig(Rd, GearLook.GreatHelm, GearLook.Sword, GearLook.KiteShield, body: GearLook.Plate, crest: "Green", cape: "Green", scale: 1.7f, raider: true), "Barkskin"),
            health = 13000, attack = 155, speed = 0.9f, cooldown = 1.2f, range = 1.1f, detection = 4f,
            ability = BossAbility.WarCry, abilityCooldown = 11f, abilityPower = 1.4f,
        },
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
            id = "BossVoidHerald", name = "The Void Herald", age = "AstralAge",
            style = Fig(Rd, GearLook.VoidHelm, GearLook.BeamStaff, body: GearLook.VoidPlate, cape: "Void", scale: 1.8f, raider: true),
            health = 15500, attack = 175, speed = 0.85f, cooldown = 1.5f, range = 7.5f, detection = 8f,
            ability = BossAbility.Summon, abilityCooldown = 12f, summons = "Voidreaver", summonCount = 3,
        },
        new BossSpec
        {
            id = "BossVoidTyrant", name = "The Void Tyrant on his Dark Throne", age = "AstralAge",
            style = Fig(Rd, GearLook.VoidHelm, GearLook.Cannon, body: GearLook.VoidPlate, cape: "Void", mount: MountKind.HoverThrone, scale: 1.5f, raider: true),
            health = 17500, attack = 230, speed = 0.65f, cooldown = 3.0f, range = 9f, detection = 9.5f,
            ability = BossAbility.WarCry, abilityCooldown = 11f, abilityPower = 1.4f,
        },
        new BossSpec
        {
            id = "BossLichArchmage", name = "The Lich Archmage", age = "ArcaneAge",
            style = Lich(Fig(Rd, GearLook.WizardHat, GearLook.ManaStaff, body: GearLook.Spellweave, crest: "Purple", cape: "Purple", scale: 1.7f, raider: true)),
            health = 11500, attack = 150, speed = 0.8f, cooldown = 1.6f, range = 7.5f, detection = 8f,
            ability = BossAbility.Summon, abilityCooldown = 12f, summons = "Hexblade", summonCount = 3,
        },
        new BossSpec
        {
            id = "BossStoneTitan", name = "The Stone Titan", age = "ArcaneAge",
            style = Skin(Fig(Rd, GearLook.GolemHead, GearLook.None, body: GearLook.GolemBody, scale: 2.4f, raider: true), "Rock"),
            health = 14000, attack = 170, speed = 0.6f, cooldown = 1.4f, range = 1.2f, detection = 3.5f,
            ability = BossAbility.Slam, abilityCooldown = 6f, abilityRadius = 3f, abilityPower = 1.5f,
        },
        new BossSpec
        {
            id = "BossRogueAI", name = "The Rogue AI", age = "InformationAge",
            style = Skin(Fig(Rd, GearLook.SynthHead, GearLook.PlasmaRifle, body: GearLook.SynthBody, scale: 2.0f, raider: true), "Synth"),
            health = 10500, attack = 40, speed = 0.8f, cooldown = 0.5f, range = 7f, detection = 7.5f,
            ability = BossAbility.Summon, abilityCooldown = 12f, summons = "RogueDrone", summonCount = 3,
        },
        new BossSpec
        {
            id = "BossMechWarlord", name = "The Warlord in his Battle Mech", age = "InformationAge",
            style = Fig(Rd, GearLook.Goggles, GearLook.MachineGun, body: GearLook.TacVest, mount: MountKind.Walker, scale: 1.5f, raider: true),
            health = 11500, attack = 34, speed = 0.8f, cooldown = 0.4f, range = 6f, detection = 6.5f,
            ability = BossAbility.WarCry, abilityCooldown = 11f, abilityPower = 1.4f,
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
        var natives = onlyAge == "Natives";
        var nativeDb = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Resources/GameDatabase.asset");
        foreach (var spec in BossSpecs)
        {
            if (natives ? !spec.native : onlyAge != null && (spec.age != onlyAge || spec.native)) continue;
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
                GearLook.RayGun or GearLook.RayRifle => "RayBolt", GearLook.PlasmaRifle => "PlasmaBolt", GearLook.AssaultRifle => "Bullet",
                GearLook.Grenade => "Grenade", GearLook.AetherRod => "AetherBolt", GearLook.ManaStaff or GearLook.ManaRifle => "ManaBolt", GearLook.BeamStaff => "StarBolt", _ => null,
            };
            def.projectile = shot != null ? AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/Units/Projectiles/{shot}.png") : null;
            SetFlight(def, spec.style.weapon);
            if (spec.style.mount == MountKind.HoverThrone) NovaGun(def);
            def.prefab = BuildPixelUnit(spec.id, spec.style, true, def, boss: true);
            def.icon = AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/Units/{spec.id}/{spec.id}_Idle.png");
            EditorUtility.SetDirty(def);
            if (spec.native)
            {
                // Native chiefs only lead their own people, on their own worlds.
                if (age != null && age.bosses.Remove(def)) EditorUtility.SetDirty(age);
                nativeDb.natives.RemoveAll(n => n == null);
                if (!nativeDb.natives.Contains(def)) nativeDb.natives.Add(def);
                EditorUtility.SetDirty(nativeDb);
            }
            else if (age != null)
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
