using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>How rare (and how strong) an item is.</summary>
public enum Rarity { Common, Uncommon, Rare, Epic, Legendary }

/// <summary>Special traits an item can roll: stat bonuses and combat effects.</summary>
public enum Trait
{
    // Stat bonuses (share, e.g. 0.1 = +10%)
    Vigor, Might, Swiftness, Haste, Reach, Warding,
    // Effects
    LifeSteal,   // heal this share of damage dealt
    Cleave,      // hit others next to the target for this share
    Thorns,      // attackers take this share back
    Mending,     // heal nearby allies this many health per second
    Rallying,    // nearby allies move this share faster
    // Xeno-artefacts only
    Dodge,       // this share of hits miss
    Regen,       // heals itself this many health per second
    Xenobane     // this share more damage to the natives of other worlds
}

[Serializable]
public struct TraitRoll
{
    public Trait trait;
    public float value;
}

/// <summary>One item you own: rolled from a template, saved in the settlement's inventory.</summary>
[Serializable]
public class ItemInstance
{
    public int uid;
    public string template;
    public string name;
    public float power;
    public Rarity rarity;
    public List<TraitRoll> traits = new List<TraitRoll>();
    [Tooltip("Enhancement level, +0 to +5 (each +10% to the base stats).")]
    public int enhance;
    [Tooltip("One extra magic trait bound with a rare material.")]
    public bool enchanted;
    public TraitRoll enchant;
}

/// <summary>Everything an item (or a set of equipped items) does, added up.</summary>
public struct GearStats
{
    public float health, attack, speed, haste, range, armour;
    public float lifeSteal, cleave, thorns, mending, rallying;
    public float dodge, regen, xenobane;

    public void Add(GearStats o)
    {
        health += o.health; attack += o.attack; speed += o.speed; haste += o.haste; range += o.range; armour += o.armour;
        lifeSteal += o.lifeSteal; cleave += o.cleave; thorns += o.thorns; mending += o.mending; rallying += o.rallying;
        dodge += o.dodge; regen += o.regen; xenobane += o.xenobane;
    }

    public UnitStatModifier ToModifier() => new UnitStatModifier
    {
        healthMultiplier = 1f + health,
        attackMultiplier = 1f + attack,
        speedMultiplier = 1f + speed,
        cooldownMultiplier = 1f / (1f + haste),
        rangeMultiplier = 1f + range,
    };
}

/// <summary>Rolling, naming and reading items.</summary>
public static class Items
{
    public static readonly Color[] RarityColors =
    {
        new Color(0.85f, 0.85f, 0.85f), // common
        new Color(0.45f, 0.9f, 0.45f),  // uncommon
        new Color(0.4f, 0.65f, 1f),     // rare
        new Color(0.75f, 0.45f, 1f),    // epic
        new Color(1f, 0.7f, 0.25f),     // legendary
    };

    public static string RarityHex(Rarity r) => ColorUtility.ToHtmlStringRGB(RarityColors[(int)r]);

    /// <summary>
    /// Can a hero of this unit type use this gear? A hero keeps their unit's way of fighting:
    /// bows are for archers, swords, axes, clubs and spears for fighters, and an archer needs
    /// both hands for the bow. Slingers and javelin throwers keep their own weapon.
    /// </summary>
    public static bool Fits(ItemTemplate template, UnitDefinition unit, out string why)
    {
        why = null;
        var rig = unit != null && unit.prefab != null ? unit.prefab.GetComponent<SpriteUnitVisual>() : null;
        if (template == null || rig == null || !rig.HasHeroRig)
        {
            return true;
        }
        string who = unit.displayName;
        var kind = rig.weaponClass;
        if (template.slot == GearSlot.Weapon)
        {
            bool bow = template.look == GearLook.Bow;
            if (kind == WeaponClass.Bow && !bow) why = $"A {who} fights with a bow";
            else if (kind == WeaponClass.Melee && bow) why = $"A {who} fights hand to hand";
            else if (kind == WeaponClass.Thrown || kind == WeaponClass.None || kind == WeaponClass.Crossbow || kind == WeaponClass.Staff
                || kind == WeaponClass.Musket || kind == WeaponClass.Cannon)
                why = $"A {who} keeps their own weapon";
        }
        else if (template.slot == GearSlot.Shield && (kind == WeaponClass.Bow || kind == WeaponClass.Crossbow || kind == WeaponClass.Musket || kind == WeaponClass.Cannon))
        {
            why = $"A {who} needs both hands for the bow";
        }
        return why == null;
    }

    private static readonly float[] RarityStrength = { 1f, 1.2f, 1.45f, 1.8f, 2.3f };
    private static readonly int[] RarityTraits = { 0, 1, 1, 2, 3 };

    /// <summary>
    /// How much stronger stats get per power level. Gentle on purpose: everything else (troops,
    /// raiders) already grows each age, so gear stays a bonus rather than taking over.
    /// </summary>
    private static float PowerScale(float power) => 0.8f + 0.2f * Mathf.Max(0f, power);

    // ---------- Loot rolls: mostly your level, now and then a lucky jackpot ----------

    /// <summary>
    /// The power level of a drop. Centred on your progress (plus the source's toughness), with a
    /// gentle spread either side and a long, thin tail upwards: you usually get gear for your age,
    /// and once in a while something well beyond it. How far beyond is rarer the further it is.
    /// </summary>
    public static float RollPower(float progress, float sourceBonus, System.Random rng)
    {
        float centre = progress + sourceBonus;
        // Gentle spread (sum of two uniforms ~ a soft bell).
        float spread = ((float)rng.NextDouble() + (float)rng.NextDouble() - 1f) * 0.6f;
        float power = centre + spread;
        // Lucky tail: about 1 in 10 drops gets a jump, and big jumps are rarer still.
        if (rng.NextDouble() < 0.1)
        {
            power += -Mathf.Log(1f - (float)rng.NextDouble() * 0.999f) * 0.7f;
        }
        return Mathf.Max(0f, power);
    }

    /// <summary>Rarity: mostly common, sometimes better; 'luck' (0..1) shifts the odds upwards.</summary>
    public static Rarity RollRarity(float luck, System.Random rng)
    {
        float r = (float)rng.NextDouble() * (1f - luck * 0.6f);
        return r < 0.008f ? Rarity.Legendary
            : r < 0.04f ? Rarity.Epic
            : r < 0.13f ? Rarity.Rare
            : r < 0.38f ? Rarity.Uncommon
            : Rarity.Common;
    }

    /// <summary>Roll a whole item: power, a template that fits it, rarity, traits and a name.</summary>
    public static ItemInstance Roll(IList<ItemTemplate> templates, float progress, float sourceBonus, float luck, int uid, System.Random rng, Rarity minRarity = Rarity.Common, bool xeno = false)
    {
        float power = RollPower(progress, sourceBonus, rng);
        // Templates that exist at this power, favouring ones made for around this level.
        var fits = new List<ItemTemplate>();
        var weights = new List<float>();
        float total = 0f;
        foreach (var t in templates)
        {
            if (t == null || t.xeno != xeno || t.minPower > power + 0.25f) continue;   // xeno-artefacts only drop from alien finds
            if (t.tradeOnly && templates.Count > 1) continue;   // native goods: only when bought by name
            float behind = Mathf.Max(0, power - t.minPower - 1f);
            float w = 1f / ((1f + 2f * behind) * (1f + 2f * behind)); // gear from long-past ages fades out fast
            fits.Add(t);
            weights.Add(w);
            total += w;
        }
        if (fits.Count == 0)
        {
            return null;
        }
        float pick = (float)rng.NextDouble() * total;
        var template = fits[fits.Count - 1];
        for (int i = 0; i < fits.Count; i++)
        {
            pick -= weights[i];
            if (pick <= 0) { template = fits[i]; break; }
        }

        var item = new ItemInstance
        {
            uid = uid,
            template = template.id,
            power = Mathf.Round(power * 100f) / 100f,
            rarity = (Rarity)Mathf.Max((int)minRarity, (int)RollRarity(luck, rng)),
        };
        int traitCount = RarityTraits[(int)item.rarity];
        var pool = TraitsFor(template);
        for (int i = 0; i < traitCount && pool.Count > 0; i++)
        {
            var trait = pool[rng.Next(pool.Count)];
            pool.Remove(trait);
            item.traits.Add(new TraitRoll { trait = trait, value = TraitValue(trait, item.power, (float)rng.NextDouble()) });
        }
        item.name = NameFor(item, template, rng);
        return item;
    }

    /// <summary>The traits an item can roll: its slot's, plus the alien ones on a xeno-artefact.</summary>
    private static List<Trait> TraitsFor(ItemTemplate template)
    {
        var pool = TraitsFor(template.slot);
        if (template.xeno) pool.AddRange(new[] { Trait.Dodge, Trait.Regen, Trait.Xenobane });
        return pool;
    }

    private static List<Trait> TraitsFor(GearSlot slot)
    {
        switch (slot)
        {
            case GearSlot.Weapon: return new List<Trait> { Trait.Might, Trait.Haste, Trait.Reach, Trait.LifeSteal, Trait.Cleave };
            case GearSlot.Shield: return new List<Trait> { Trait.Warding, Trait.Vigor, Trait.Thorns };
            case GearSlot.Head: return new List<Trait> { Trait.Vigor, Trait.Warding, Trait.Haste, Trait.Rallying };
            case GearSlot.Body: return new List<Trait> { Trait.Vigor, Trait.Warding, Trait.Thorns, Trait.Swiftness };
            default: return new List<Trait> { Trait.Might, Trait.Swiftness, Trait.LifeSteal, Trait.Mending, Trait.Rallying, Trait.Vigor };
        }
    }

    private static float TraitValue(Trait trait, float power, float roll)
    {
        float scale = PowerScale(power) * (0.75f + roll * 0.5f);
        switch (trait)
        {
            case Trait.Vigor: return 0.08f * scale;
            case Trait.Might: return 0.07f * scale;
            case Trait.Swiftness: return 0.05f * scale;
            case Trait.Haste: return 0.06f * scale;
            case Trait.Reach: return 0.08f * scale;
            case Trait.Warding: return 8f * scale;
            case Trait.LifeSteal: return 0.06f * scale;
            case Trait.Cleave: return 0.25f * scale;
            case Trait.Thorns: return 0.12f * scale;
            case Trait.Mending: return 1.5f * scale;
            case Trait.Rallying: return 0.06f * scale;
            case Trait.Dodge: return 0.04f * scale;
            case Trait.Regen: return 1.2f * scale;
            case Trait.Xenobane: return 0.12f * scale;
            default: return 0f;
        }
    }

    // ---------- Names ----------

    private static readonly string[][] RarityWords =
    {
        new string[0],
        new[] { "Fine", "Sturdy", "Keen", "Trusty" },
        new[] { "Masterwork", "Runed", "Gleaming", "Storied" },
        new[] { "Heroic", "Storm-forged", "Ancestral", "Moonlit" },
        new[] { "Mythic", "Sun-blessed", "Worldsplitter's", "Undying" },
    };

    private static string TraitSuffix(Trait trait)
    {
        switch (trait)
        {
            case Trait.Vigor: return "of the Bear";
            case Trait.Might: return "of the Ox";
            case Trait.Swiftness: return "of the Hare";
            case Trait.Haste: return "of the Falcon";
            case Trait.Reach: return "of the Heron";
            case Trait.Warding: return "of the Tortoise";
            case Trait.LifeSteal: return "of the Leech";
            case Trait.Cleave: return "of Sweeping";
            case Trait.Thorns: return "of Brambles";
            case Trait.Mending: return "of the Healer";
            case Trait.Rallying: return "of the War-horn";
            case Trait.Dodge: return "of the Phantom";
            case Trait.Regen: return "of the Undying Star";
            case Trait.Xenobane: return "of the Far Hunter";
            default: return "";
        }
    }

    private static string NameFor(ItemInstance item, ItemTemplate template, System.Random rng)
    {
        var words = RarityWords[(int)item.rarity];
        string prefix = words.Length > 0 ? words[rng.Next(words.Length)] + " " : "";
        string suffix = item.traits.Count > 0 ? " " + TraitSuffix(item.traits[0].trait) : "";
        return prefix + template.displayName + suffix;
    }

    // ---------- Enchanting ----------

    /// <summary>The name to show: "+3 Runed Sword of the Ox ✦" for an enhanced, enchanted one.</summary>
    public static string DisplayName(ItemInstance item) =>
        item == null ? "" : (item.enhance > 0 ? $"+{item.enhance} " : "") + item.name + (item.enchanted ? " <color=#c9f>*</color>" : "");

    private static readonly Trait[] EnchantPool =
        { Trait.LifeSteal, Trait.Cleave, Trait.Thorns, Trait.Mending, Trait.Rallying, Trait.Might, Trait.Vigor, Trait.Haste, Trait.Swiftness, Trait.Warding };

    /// <summary>A magic trait for an enchantment: a little stronger than a rolled one.</summary>
    public static TraitRoll RollEnchantment(ItemInstance item, System.Random rng)
    {
        var trait = EnchantPool[rng.Next(EnchantPool.Length)];
        return new TraitRoll { trait = trait, value = TraitValue(trait, item.power, (float)rng.NextDouble()) * 1.25f };
    }

    public static string EnchantmentName(Trait trait) => trait switch
    {
        Trait.LifeSteal => "Bloodthirst", Trait.Cleave => "Sweeping Edge", Trait.Thorns => "Spines", Trait.Mending => "Healing Light",
        Trait.Rallying => "War Song", Trait.Might => "Strength", Trait.Vigor => "Endurance", Trait.Haste => "Quickness",
        Trait.Swiftness => "Fleetness", Trait.Warding => "Warding", _ => trait.ToString(),
    };

    /// <summary>New traits for an item, as many as its rarity gives.</summary>
    public static void RerollTraits(ItemInstance item, ItemTemplate template, System.Random rng)
    {
        if (template == null) return;
        int count = item.traits.Count;
        item.traits.Clear();
        var pool = TraitsFor(template);
        for (int i = 0; i < count && pool.Count > 0; i++)
        {
            var trait = pool[rng.Next(pool.Count)];
            pool.Remove(trait);
            item.traits.Add(new TraitRoll { trait = trait, value = TraitValue(trait, item.power, (float)rng.NextDouble()) });
        }
        item.name = NameFor(item, template, rng);
    }

    // ---------- Reading items ----------

    /// <summary>What an item does, from its template, power, rarity and traits.</summary>
    public static GearStats StatsOf(ItemInstance item, ItemTemplate template)
    {
        var s = new GearStats();
        if (item == null || template == null)
        {
            return s;
        }
        float k = PowerScale(item.power) * RarityStrength[(int)item.rarity] * (1f + 0.1f * item.enhance);
        s.health = template.health * k;
        s.attack = template.attack * k;
        s.speed = template.speed * k;
        s.haste = template.haste * k;
        s.range = template.range * k;
        s.armour = template.armour * k;
        s.dodge = template.dodge * k;
        s.regen = template.regen * k;
        s.xenobane = template.xenobane * k;
        var all = new List<TraitRoll>(item.traits);
        if (item.enchanted) all.Add(item.enchant);
        foreach (var t in all)
        {
            switch (t.trait)
            {
                case Trait.Vigor: s.health += t.value; break;
                case Trait.Might: s.attack += t.value; break;
                case Trait.Swiftness: s.speed += t.value; break;
                case Trait.Haste: s.haste += t.value; break;
                case Trait.Reach: s.range += t.value; break;
                case Trait.Warding: s.armour += t.value; break;
                case Trait.LifeSteal: s.lifeSteal += t.value; break;
                case Trait.Cleave: s.cleave += t.value; break;
                case Trait.Thorns: s.thorns += t.value; break;
                case Trait.Mending: s.mending += t.value; break;
                case Trait.Rallying: s.rallying += t.value; break;
                case Trait.Dodge: s.dodge += t.value; break;
                case Trait.Regen: s.regen += t.value; break;
                case Trait.Xenobane: s.xenobane += t.value; break;
            }
        }
        return s;
    }

    /// <summary>Plain-English lines for a set of stats ("+12% health", "Heals allies 2/s").</summary>
    public static string Describe(GearStats s)
    {
        var sb = new StringBuilder();
        void Pct(float v, string what) { if (v > 0.001f) sb.Append($"+{v:P0} {what}\n"); }
        Pct(s.health, "health");
        Pct(s.attack, "attack");
        Pct(s.speed, "speed");
        Pct(s.haste, "attack speed");
        Pct(s.range, "range");
        if (s.armour > 0.5f) sb.Append($"{s.armour:0} armour ({s.armour / (s.armour + 100f):P0} less damage)\n");
        if (s.lifeSteal > 0.001f) sb.Append($"<color=#f99>Heals {s.lifeSteal:P0} of damage dealt</color>\n");
        if (s.cleave > 0.001f) sb.Append($"<color=#fc8>Cleaves: {s.cleave:P0} damage to enemies beside the target</color>\n");
        if (s.thorns > 0.001f) sb.Append($"<color=#9d9>Thorns: attackers take {s.thorns:P0} back</color>\n");
        if (s.mending > 0.01f) sb.Append($"<color=#8f8>Heals nearby allies {s.mending:0.#} health/s</color>\n");
        if (s.rallying > 0.001f) sb.Append($"<color=#8cf>Nearby allies move {s.rallying:P0} faster</color>\n");
        if (s.dodge > 0.001f) sb.Append($"<color=#9ef>Dodges {Mathf.Min(s.dodge, MaxDodge):P0} of hits</color>\n");
        if (s.regen > 0.01f) sb.Append($"<color=#8f8>Regenerates {s.regen:0.#} health/s</color>\n");
        if (s.xenobane > 0.001f) sb.Append($"<color=#fc8>+{s.xenobane:P0} damage to the natives of other worlds</color>\n");
        return sb.ToString().TrimEnd();
    }

    /// <summary>Roughly how strong an item is overall (for sorting and comparing).</summary>
    public static float Score(GearStats s) =>
        s.health + s.attack * 1.2f + s.speed + s.haste + s.range * 0.6f + s.armour / 80f
        + s.lifeSteal * 2f + s.cleave + s.thorns + s.mending * 0.05f + s.rallying
        + s.dodge * 2f + s.regen * 0.05f + s.xenobane * 0.5f;

    /// <summary>Dodging never goes past this, however much gear stacks up.</summary>
    public const float MaxDodge = 0.4f;
}
