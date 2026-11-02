using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using static Painter;

/// <summary>
/// Gear templates (what can drop) and their icons. Items are rolled from these in play, each
/// with its own power, rarity and traits, so a couple of dozen templates give endless gear
/// (Tools > Age of Stickman > Setup Gear Data).
/// </summary>
public static partial class PlaceholderArt
{
    private struct GearSpec
    {
        public string id, name;
        public GearSlot slot;
        public GearLook look;
        public float minPower, health, attack, speed, haste, range, armour;
    }

    private static readonly GearSpec[] GearSpecs =
    {
        // Head
        new GearSpec { id = "LeatherCap", name = "Leather Cap", slot = GearSlot.Head, look = GearLook.Cap, minPower = 0, health = 0.05f, armour = 4 },
        new GearSpec { id = "WolfskinHood", name = "Wolfskin Hood", slot = GearSlot.Head, look = GearLook.Hood, minPower = 0, health = 0.04f, speed = 0.03f },
        new GearSpec { id = "BronzeHelm", name = "Bronze Helm", slot = GearSlot.Head, look = GearLook.DomeHelm, minPower = 1, health = 0.06f, armour = 7 },
        new GearSpec { id = "IronHelm", name = "Iron Helm", slot = GearSlot.Head, look = GearLook.ConeHelm, minPower = 2, health = 0.07f, armour = 9 },
        new GearSpec { id = "CrestedGalea", name = "Crested Galea", slot = GearSlot.Head, look = GearLook.CrestedHelm, minPower = 3, health = 0.08f, armour = 10, attack = 0.02f },
        // Body
        new GearSpec { id = "HideVest", name = "Hide Vest", slot = GearSlot.Body, look = GearLook.HideVest, minPower = 0, health = 0.1f, armour = 3 },
        new GearSpec { id = "PaddedJerkin", name = "Padded Jerkin", slot = GearSlot.Body, look = GearLook.Jerkin, minPower = 1, health = 0.12f, armour = 6 },
        new GearSpec { id = "BronzeScale", name = "Bronze Scale", slot = GearSlot.Body, look = GearLook.Scale, minPower = 1, health = 0.1f, armour = 10, speed = -0.02f },
        new GearSpec { id = "IronMail", name = "Iron Mail", slot = GearSlot.Body, look = GearLook.Mail, minPower = 2, health = 0.13f, armour = 13, speed = -0.02f },
        new GearSpec { id = "LoricaPlate", name = "Lorica Plate", slot = GearSlot.Body, look = GearLook.Plate, minPower = 3, health = 0.15f, armour = 16, speed = -0.03f },
        // Weapon
        new GearSpec { id = "KnottedClub", name = "Knotted Club", slot = GearSlot.Weapon, look = GearLook.Club, minPower = 0, attack = 0.12f },
        new GearSpec { id = "FlintSpear", name = "Flint Spear", slot = GearSlot.Weapon, look = GearLook.Spear, minPower = 0, attack = 0.09f, range = 0.08f },
        new GearSpec { id = "BronzeSword", name = "Bronze Sword", slot = GearSlot.Weapon, look = GearLook.Sword, minPower = 1, attack = 0.12f, haste = 0.04f },
        new GearSpec { id = "BronzeAxe", name = "Bronze Axe", slot = GearSlot.Weapon, look = GearLook.Axe, minPower = 1, attack = 0.16f },
        new GearSpec { id = "HuntingBow", name = "Hunting Bow", slot = GearSlot.Weapon, look = GearLook.Bow, minPower = 1, attack = 0.08f, range = 0.12f },
        new GearSpec { id = "IronBlade", name = "Iron Blade", slot = GearSlot.Weapon, look = GearLook.Sword, minPower = 2, attack = 0.15f, haste = 0.05f },
        new GearSpec { id = "WarBow", name = "War Bow", slot = GearSlot.Weapon, look = GearLook.Bow, minPower = 2, attack = 0.11f, range = 0.15f },
        new GearSpec { id = "Gladius", name = "Gladius", slot = GearSlot.Weapon, look = GearLook.ShortSword, minPower = 3, attack = 0.14f, haste = 0.09f },
        // Shield
        new GearSpec { id = "HideShield", name = "Hide Shield", slot = GearSlot.Shield, look = GearLook.HideShield, minPower = 0, armour = 6, health = 0.03f },
        new GearSpec { id = "WickerShield", name = "Wicker Shield", slot = GearSlot.Shield, look = GearLook.WickerShield, minPower = 1, armour = 9, health = 0.03f },
        new GearSpec { id = "RoundShield", name = "Round Shield", slot = GearSlot.Shield, look = GearLook.RoundShield, minPower = 2, armour = 12, health = 0.04f },
        new GearSpec { id = "Scutum", name = "Scutum", slot = GearSlot.Shield, look = GearLook.TowerShield, minPower = 3, armour = 16, health = 0.05f, speed = -0.02f },
        // Trinket
        new GearSpec { id = "BoneCharm", name = "Bone Charm", slot = GearSlot.Trinket, look = GearLook.Charm, minPower = 0, health = 0.04f },
        new GearSpec { id = "WarHorn", name = "War Horn", slot = GearSlot.Trinket, look = GearLook.Horn, minPower = 1, speed = 0.04f },
        new GearSpec { id = "AmberAmulet", name = "Amber Amulet", slot = GearSlot.Trinket, look = GearLook.Amulet, minPower = 1, health = 0.05f, attack = 0.03f },
        new GearSpec { id = "SilverRing", name = "Silver Ring", slot = GearSlot.Trinket, look = GearLook.Ring, minPower = 2, attack = 0.05f, haste = 0.03f },
        new GearSpec { id = "LaurelWreath", name = "Laurel Wreath", slot = GearSlot.Trinket, look = GearLook.Wreath, minPower = 3, health = 0.05f, attack = 0.04f, speed = 0.03f },
    };

    [MenuItem("Tools/Age of Stickman/Setup Gear Data")]
    public static void SetupGear()
    {
        var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Resources/GameDatabase.asset");
        Directory.CreateDirectory("Assets/Data/Items");
        Directory.CreateDirectory(Path.Combine(Root, "Icons/Gear"));
        db.itemTemplates = new List<ItemTemplate>();
        foreach (var spec in GearSpecs)
        {
            string path = $"Assets/Data/Items/{spec.id}.asset";
            var t = AssetDatabase.LoadAssetAtPath<ItemTemplate>(path);
            if (t == null)
            {
                t = ScriptableObject.CreateInstance<ItemTemplate>();
                AssetDatabase.CreateAsset(t, path);
            }
            t.id = spec.id;
            t.displayName = spec.name;
            t.slot = spec.slot;
            t.look = spec.look;
            t.minPower = spec.minPower;
            t.health = spec.health;
            t.attack = spec.attack;
            t.speed = spec.speed;
            t.haste = spec.haste;
            t.range = spec.range;
            t.armour = spec.armour;
            var look = spec.look;
            t.icon = Icon($"Icons/Gear/{spec.id}.png", p => DrawGearIcon(p, look, spec.minPower));
            EditorUtility.SetDirty(t);
            db.itemTemplates.Add(t);
        }
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        Debug.Log($"Gear set up: {db.itemTemplates.Count} templates.");
    }

    /// <summary>Metal by age: hide and bone, bronze, iron, then polished steel.</summary>
    private static Color GearMetal(float power) =>
        power < 1 ? StoneLight : power < 2 ? Bronze : power < 3 ? SteelGrey : SteelLight;

    private static void DrawGearIcon(Painter p, GearLook look, float power)
    {
        Color metal = GearMetal(power);
        switch (look)
        {
            case GearLook.Cap:
                p.OutlinedPolygon(new[] { V(0.18f, 0.4f), V(0.82f, 0.4f), V(0.75f, 0.68f), V(0.5f, 0.8f), V(0.25f, 0.68f) }, Hide);
                p.Line(V(0.2f, 0.45f), V(0.8f, 0.45f), 0.04f, WoodDark);
                break;
            case GearLook.Hood:
                p.OutlinedPolygon(new[] { V(0.2f, 0.2f), V(0.8f, 0.2f), V(0.78f, 0.6f), V(0.5f, 0.88f), V(0.22f, 0.6f) }, new Color(0.55f, 0.52f, 0.5f));
                p.OutlinedEllipse(V(0.5f, 0.45f), V(0.18f, 0.2f), Dark);
                p.OutlinedPolygon(new[] { V(0.3f, 0.75f), V(0.36f, 0.95f), V(0.42f, 0.8f) }, new Color(0.55f, 0.52f, 0.5f));
                p.OutlinedPolygon(new[] { V(0.58f, 0.8f), V(0.64f, 0.95f), V(0.7f, 0.75f) }, new Color(0.55f, 0.52f, 0.5f));
                break;
            case GearLook.DomeHelm:
            case GearLook.ConeHelm:
            case GearLook.CrestedHelm:
                if (look == GearLook.CrestedHelm)
                {
                    p.OutlinedPolygon(new[] { V(0.3f, 0.78f), V(0.5f, 0.97f), V(0.75f, 0.85f), V(0.6f, 0.72f) }, Flag);
                }
                var top = look == GearLook.ConeHelm ? V(0.5f, 0.92f) : V(0.5f, 0.82f);
                p.OutlinedPolygon(new[] { V(0.2f, 0.3f), V(0.8f, 0.3f), V(0.8f, 0.55f), top + V(0.18f, -0.08f), top, top + V(-0.18f, -0.08f), V(0.2f, 0.55f) }, metal);
                p.OutlinedBox(0.42f, 0.18f, 0.58f, 0.45f, 0.02f, metal); // nose guard
                p.Line(V(0.22f, 0.5f), V(0.78f, 0.5f), 0.03f, Color.Lerp(metal, Color.white, 0.4f));
                break;
            case GearLook.HideVest:
            case GearLook.Jerkin:
            case GearLook.Scale:
            case GearLook.Mail:
            case GearLook.Plate:
                Color body = look == GearLook.HideVest ? Hide : look == GearLook.Jerkin ? new Color(0.72f, 0.62f, 0.45f) : metal;
                p.OutlinedPolygon(new[] { V(0.2f, 0.12f), V(0.8f, 0.12f), V(0.82f, 0.72f), V(0.66f, 0.85f), V(0.5f, 0.75f), V(0.34f, 0.85f), V(0.18f, 0.72f) }, body);
                if (look == GearLook.Scale || look == GearLook.Mail)
                {
                    for (float y = 0.22f; y < 0.7f; y += 0.1f)
                        for (float x = 0.27f; x < 0.75f; x += 0.1f)
                            p.Circle(V(x + (y * 10 % 2 < 1 ? 0 : 0.05f), y), 0.03f, Color.Lerp(body, Color.black, 0.25f));
                }
                if (look == GearLook.Plate || look == GearLook.Jerkin)
                {
                    for (float y = 0.25f; y < 0.7f; y += 0.12f) p.InkLine(V(0.22f, y), V(0.78f, y), 0.6f);
                }
                break;
            case GearLook.Club:
                p.OutlinedLine(V(0.25f, 0.15f), V(0.62f, 0.75f), 0.08f, Wood);
                p.OutlinedCircle(V(0.68f, 0.8f), 0.14f, WoodLight);
                break;
            case GearLook.Spear:
                p.OutlinedLine(V(0.18f, 0.12f), V(0.75f, 0.78f), 0.05f, WoodLight);
                p.OutlinedPolygon(new[] { V(0.7f, 0.72f), V(0.92f, 0.94f), V(0.78f, 0.66f) }, power < 1 ? StoneDark : metal);
                break;
            case GearLook.Sword:
            case GearLook.ShortSword:
                float len = look == GearLook.ShortSword ? 0.55f : 0.68f;
                p.OutlinedPolygon(new[] { V(0.42f, 0.28f), V(0.58f, 0.28f), V(0.55f, 0.28f + len), V(0.5f, 0.34f + len), V(0.45f, 0.28f + len) }, metal);
                p.OutlinedBox(0.3f, 0.22f, 0.7f, 0.29f, 0.02f, Bronze);
                p.OutlinedBox(0.46f, 0.06f, 0.54f, 0.22f, 0.02f, WoodDark);
                break;
            case GearLook.Axe:
                p.OutlinedLine(V(0.3f, 0.1f), V(0.55f, 0.88f), 0.06f, WoodLight);
                p.OutlinedPolygon(new[] { V(0.5f, 0.6f), V(0.82f, 0.5f), V(0.86f, 0.82f), V(0.56f, 0.8f) }, metal);
                break;
            case GearLook.Bow:
                p.Draw(q => Mathf.Abs(SdCircle(q, V(0.25f, 0.5f), 0.45f)) - 0.035f, new Rect(0.25f, 0.02f, 0.5f, 0.96f), p.Ink);
                p.Draw(q => Mathf.Max(Mathf.Abs(SdCircle(q, V(0.25f, 0.5f), 0.45f)) - 0.02f, 0.26f - q.x), new Rect(0.25f, 0.02f, 0.5f, 0.96f), Wood);
                p.Line(V(0.42f, 0.1f), V(0.42f, 0.9f), 0.015f, new Color(0.9f, 0.88f, 0.8f));
                break;
            case GearLook.HideShield:
            case GearLook.WickerShield:
            case GearLook.RoundShield:
                Color face = look == GearLook.HideShield ? Hide : look == GearLook.WickerShield ? Straw : Color.Lerp(Wood, Flag, 0.4f);
                p.OutlinedCircle(V(0.5f, 0.5f), 0.4f, face);
                if (look == GearLook.WickerShield) for (float y = 0.25f; y < 0.8f; y += 0.1f) p.Line(V(0.18f, y), V(0.82f, y), 0.02f, StrawDark);
                p.OutlinedCircle(V(0.5f, 0.5f), 0.1f, look == GearLook.HideShield ? WoodDark : metal);
                break;
            case GearLook.TowerShield:
                p.OutlinedBox(0.25f, 0.1f, 0.75f, 0.9f, 0.08f, Flag);
                p.Line(V(0.5f, 0.14f), V(0.5f, 0.86f), 0.03f, Gold);
                p.OutlinedCircle(V(0.5f, 0.5f), 0.08f, metal);
                break;
            case GearLook.Charm:
                p.Line(V(0.3f, 0.85f), V(0.5f, 0.55f), 0.02f, Wood);
                p.Line(V(0.7f, 0.85f), V(0.5f, 0.55f), 0.02f, Wood);
                p.OutlinedPolygon(new[] { V(0.42f, 0.55f), V(0.58f, 0.55f), V(0.55f, 0.15f), V(0.5f, 0.1f), V(0.45f, 0.15f) }, new Color(0.93f, 0.9f, 0.8f));
                break;
            case GearLook.Horn:
                p.OutlinedPolygon(new[] { V(0.15f, 0.62f), V(0.25f, 0.75f), V(0.6f, 0.55f), V(0.85f, 0.25f), V(0.72f, 0.2f), V(0.5f, 0.42f) }, new Color(0.9f, 0.85f, 0.7f));
                p.OutlinedEllipse(V(0.8f, 0.22f), V(0.07f, 0.1f), Dark);
                p.Line(V(0.3f, 0.66f), V(0.42f, 0.58f), 0.03f, Bronze);
                break;
            case GearLook.Amulet:
            case GearLook.Ring:
                if (look == GearLook.Amulet)
                {
                    p.Draw(q => Mathf.Abs(SdCircle(q, V(0.5f, 0.7f), 0.28f)) - 0.015f, new Rect(0.2f, 0.4f, 0.6f, 0.6f), metal);
                    p.OutlinedCircle(V(0.5f, 0.38f), 0.16f, new Color(0.95f, 0.6f, 0.15f));
                    p.Circle(V(0.45f, 0.43f), 0.05f, new Color(1f, 0.85f, 0.5f));
                }
                else
                {
                    p.Draw(q => Mathf.Abs(SdCircle(q, V(0.5f, 0.42f), 0.25f)) - 0.06f, new Rect(0.15f, 0.1f, 0.7f, 0.7f), p.Ink);
                    p.Draw(q => Mathf.Abs(SdCircle(q, V(0.5f, 0.42f), 0.25f)) - 0.04f, new Rect(0.15f, 0.1f, 0.7f, 0.7f), StoneLight);
                    p.OutlinedCircle(V(0.5f, 0.68f), 0.1f, new Color(0.4f, 0.65f, 1f));
                }
                break;
            case GearLook.Wreath:
                // A ring of leaves, open at the top, tied with a ribbon at the bottom.
                for (int i = 0; i < 16; i++)
                {
                    float a = Mathf.Lerp(-55f, 235f, i / 15f) * Mathf.Deg2Rad - Mathf.PI * 0.5f;
                    var c = V(0.5f, 0.5f) + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.3f;
                    float tilt = a + Mathf.PI * 0.5f;
                    var along = new Vector2(Mathf.Cos(tilt), Mathf.Sin(tilt)) * 0.07f;
                    var side = new Vector2(-along.y, along.x) * 0.5f;
                    p.OutlinedPolygon(new[] { c - along, c + side, c + along, c - side }, i % 2 == 0 ? Leaf : LeafLight);
                }
                p.OutlinedCircle(V(0.5f, 0.2f), 0.06f, Flag);
                break;
        }
    }
}
