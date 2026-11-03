using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The peoples native to other worlds: two kinds of fighter and a chief each, in their own
/// skins. They hold the camps and lead raids on their own kind of world, never at home.
/// Built to meet colonists of the Astral Age and later. (Tools > Age of Stickman > Setup Natives.)
/// </summary>
public static partial class PlaceholderArt
{
    [MenuItem("Tools/Age of Stickman/Setup Natives")]
    public static void SetupNatives()
    {
        var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Resources/GameDatabase.asset");
        var astral = db.ages.Find(a => a != null && a.name == "AstralAge");
        if (astral == null) { Debug.LogError("Run Setup Astral Age first."); return; }
        // (id, name, template, health, attack, cooldown, range, speed)
        var specs = new (string id, string name, string template, int hp, int atk, float cd, float range, float speed)[]
        {
            ("Sandfolk_Skirmisher", "Dune Skirmisher", "Voidreaver", 900, 95, 1.2f, 4.5f, 1.25f),
            ("Sandfolk_Archer", "Sand Archer", "VoidCaster", 560, 90, 1.5f, 7.5f, 1.1f),
            ("Frostkin_Brute", "Frost Brute", "Voidreaver", 1500, 120, 1.3f, 1.0f, 0.95f),
            ("Frostkin_Hurler", "Ice Hurler", "VoidCaster", 650, 90, 1.5f, 5f, 1.0f),
            ("Bogfolk_Lurker", "Bog Lurker", "Voidreaver", 1000, 100, 1.0f, 1.2f, 1.15f),
            ("Bogfolk_Witch", "Mire Witch", "VoidCaster", 520, 105, 1.7f, 7f, 1.0f),
            ("Ashborn_Brute", "Ash Brute", "Voidreaver", 1250, 125, 1.1f, 1.0f, 1.05f),
            ("Ashborn_Thrower", "Ember Thrower", "VoidCaster", 600, 110, 1.8f, 5.5f, 1.0f),
            ("Shardkin_Warden", "Shard Warden", "Voidreaver", 1600, 105, 1.1f, 1.0f, 0.9f),
            ("Shardkin_Slinger", "Shard Slinger", "VoidCaster", 540, 95, 1.3f, 6f, 1.1f),
            ("Tidefolk_Raider", "Reef Raider", "Voidreaver", 1050, 100, 1.0f, 1.3f, 1.15f),
            ("Tidefolk_Archer", "Coral Archer", "VoidCaster", 560, 92, 1.5f, 7.5f, 1.1f),
            ("Wildfolk_Warden", "Thorn Warden", "Voidreaver", 1100, 105, 1.0f, 1.0f, 1.15f),
            ("Wildfolk_Hunter", "Wild Hunter", "VoidCaster", 560, 95, 1.4f, 8f, 1.2f),
        };
        var ids = new HashSet<string>();
        db.natives.RemoveAll(n => n == null);
        foreach (var s in specs)
        {
            var u = MedievalUnit(s.id, s.name, astral, s.template, s.hp, s.atk, s.cd, s.range, s.speed);
            u.movement = Movement.Ground; u.flyHeight = 0f;
            u.isNative = true;
            EditorUtility.SetDirty(u);
            if (!db.natives.Contains(u)) db.natives.Add(u);
            ids.Add(s.id);
        }
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        GenerateWarriorsAndCrews(db, ids);
        SetupBosses("Natives");
        AssetDatabase.SaveAssets();
        Debug.Log("Natives set up.");
    }
}
