using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Medics: Medicine (Classical research) unlocks the Valetudinarium, a hospital that trains
/// Capsarii. They don't fight: they follow your troops and bandage the most badly hurt.
/// There is no natural healing, so they are how an army stays on its feet.
/// (Tools > Age of Stickman > Setup Medics.)
/// </summary>
public static partial class PlaceholderArt
{
    [MenuItem("Tools/Age of Stickman/Setup Medics")]
    public static void SetupMedics()
    {
        var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Resources/GameDatabase.asset");
        var classical = AssetDatabase.LoadAssetAtPath<Age>("Assets/Data/Ages/ClassicalAge.asset");
        ResourceType food = db.GetResource("Food"), wood = db.GetResource("Wood"), stone = db.GetResource("Stone"),
            gold = db.GetResource("Gold"), coin = db.GetResource("Coin");
        string after = db.technologies.Exists(t => t != null && t.id == "Taxation") ? "Taxation" : "Engineering";

        var medicine = Tech(db, "Medicine", "Medicine", classical,
            "Physicians, herbs and clean bandages. Unlocks the Valetudinarium, which trains medics who heal your troops in the field.",
            45f, Cost(food, 200, gold, 60), after);
        medicine.unitModifiers = new List<UnitStatModifier>();
        EditorUtility.SetDirty(medicine);

        // The medic
        var legionary = AssetDatabase.LoadAssetAtPath<UnitDefinition>("Assets/Data/Units/Legionary.asset");
        const string unitPath = "Assets/Data/Units/Capsarius.asset";
        var medic = AssetDatabase.LoadAssetAtPath<UnitDefinition>(unitPath);
        if (medic == null)
        {
            medic = ScriptableObject.CreateInstance<UnitDefinition>();
            AssetDatabase.CreateAsset(medic, unitPath);
        }
        medic.displayName = "Capsarius";
        medic.age = classical;
        medic.troopClass = legionary != null ? legionary.troopClass : null;
        medic.populationCost = 1;
        medic.upkeep = legionary != null ? new List<ResourceRate>(legionary.upkeep) : new List<ResourceRate>();
        medic.maxHealth = 160;
        medic.attackPower = 14;   // healed per go
        medic.attackCooldown = 1.2f;
        medic.attackRange = 0.8f;
        medic.moveSpeed = 1.0f;
        medic.detectionRange = 3f;
        medic.hitDelay = 0.4f;
        medic.healer = true;
        medic.healRange = 8f;
        medic.projectile = null;
        var style = Fig("Blue", GearLook.Cap, GearLook.None, body: GearLook.Jerkin);
        style.medic = true;
        medic.prefab = BuildPixelUnit("Capsarius", style, false, medic);
        medic.icon = AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/Units/Capsarius/Capsarius_Idle.png");
        EditorUtility.SetDirty(medic);
        if (classical != null && !classical.playerUnits.Contains(medic))
        {
            classical.playerUnits.Add(medic);
            EditorUtility.SetDirty(classical);
        }

        // The hospital
        var hospital = Building(db, "Valetudinarium", "Valetudinarium", classical, "Stables", new Vector2Int(3, 2), new Vector2Int(3, 3),
            "A hospital. Trains Capsarii: medics who follow your troops and heal the wounded (there's no other healing in the field).",
            Cost(wood, 200, stone, 250, gold, 40), 35f, 450, 2, medicine);
        hospital.trainsUnit = medic;
        hospital.maxUnits = 3;
        hospital.production = new List<ResourceRate>();
        hospital.upgrades = new List<BuildingUpgrade>
        {
            new BuildingUpgrade
            {
                name = "Herb Garden", description = "Fresh herbs and more hands: +1 medic.",
                requirements = new Requirements { cost = Cost(food, 150, wood, 100) }, upgradeTime = 25f, extraUnits = 1,
            },
            new BuildingUpgrade
            {
                name = "Surgeons' Tools", description = "Iron probes and clean linen: medics heal 30% more and are 20% tougher.",
                requirements = new Requirements { cost = Cost(gold, 60, coin, 40) }, upgradeTime = 30f,
                trainedUnits = new UnitStatModifier { attackMultiplier = 1.3f, healthMultiplier = 1.2f },
            },
        };
        GenerateBuildingLooks(hospital);
        EditorUtility.SetDirty(hospital);
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        Debug.Log("Medics set up.");
    }
}
