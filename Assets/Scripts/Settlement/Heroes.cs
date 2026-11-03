using System.Collections.Generic;
using UnityEngine;

/// <summary>Heroes, gear and loot: the settlement side (saved state and rules).</summary>
public partial class Settlement
{
    /// <summary>Raised when a hero levels up, falls, recovers, or changes gear.</summary>
    public event System.Action<HeroState> HeroChanged;

    /// <summary>How many heroes you can have: one, plus one per age reached.</summary>
    public int HeroSlots => 1 + State.ageIndex;

    /// <summary>Highest level a hero can reach right now.</summary>
    public int LevelCap => 5 + 5 * State.ageIndex;

    /// <summary>
    /// How far along you are, for matching loot (0 = start of the Stone Age, 1 = Bronze...).
    /// Until ages are earned gradually, this is the middle of your current age.
    /// </summary>
    public float Progress => State.ageIndex + 0.5f;

    public HeroState GetHero(int id) => State.heroes.Find(h => h.id == id);

    public ItemInstance GetItem(int uid) => uid == 0 ? null : State.inventory.Find(i => i.uid == uid);

    public ItemTemplate GetTemplate(string id) => Database.itemTemplates.Find(t => t != null && t.id == id);

    public bool IsDown(HeroState hero) => hero != null && State.playTime < hero.downUntil;

    /// <summary>A player unit type by asset name (heroes remember the type they were promoted from).</summary>
    public UnitDefinition FindUnit(string name)
    {
        foreach (var age in Database.ages)
        {
            if (age == null) continue;
            var unit = age.playerUnits.Find(u => u != null && u.name == name);
            if (unit != null) return unit;
        }
        var building = Database.buildings.Find(b => b != null && b.trainsUnit != null && b.trainsUnit.name == name);
        return building != null ? building.trainsUnit : null;
    }

    // ---------- Promotion ----------

    public List<ResourceAmount> PromoteCost()
    {
        int a = State.ageIndex + 1;
        var cost = new List<ResourceAmount>();
        var food = Database.food;
        var wood = Database.resources.Find(r => r != null && r.id == "Wood");
        if (food != null) cost.Add(new ResourceAmount { resource = food, amount = 120 * a });
        if (wood != null) cost.Add(new ResourceAmount { resource = wood, amount = 60 * a });
        return cost;
    }

    public bool CanPromote(out string why)
    {
        if (State.heroes.Count >= HeroSlots)
        {
            why = $"All {HeroSlots} hero places are taken (one more each age)";
            return false;
        }
        if (!CanAfford(PromoteCost()))
        {
            why = "Not enough resources";
            return false;
        }
        why = null;
        return true;
    }

    /// <summary>Make a troop of this type, from this building, into a named hero.</summary>
    public HeroState Promote(UnitDefinition unit, BuildingState home, out string why)
    {
        if (unit == null || home == null)
        {
            why = "Can't promote that";
            return null;
        }
        if (!CanPromote(out why))
        {
            return null;
        }
        Spend(PromoteCost());
        var hero = new HeroState
        {
            id = State.nextHeroId++,
            unitId = unit.name,
            homeBuilding = home.instanceId,
            world = State.currentWorld,
        };
        hero.name = HeroName(hero.id);
        State.heroes.Add(hero);
        if (!offWorld) GameAudio.UI("hero_recruit");
        Notify($"{hero.name} the {unit.displayName} is now a hero!");
        HeroChanged?.Invoke(hero);
        return hero;
    }

    private static readonly string[] NameStarts = { "Ar", "Bran", "Cor", "Da", "Eld", "Fen", "Gar", "Hal", "Ivo", "Jor", "Kael", "Lu", "Mor", "Nyr", "Os", "Per", "Rho", "Syl", "Tor", "Ul", "Vex", "Wyn", "Yor", "Zan" };
    private static readonly string[] NameEnds = { "ic", "a", "ok", "en", "is", "uth", "ar", "wyn", "o", "ric", "ia", "dor", "an", "et", "ius" };
    private static readonly string[] Epithets = { "the Bold", "Ironhand", "the Swift", "Stoneheart", "the Wise", "Wolfsbane", "the Red", "Oathkeeper", "the Unbroken", "Farstrider" };

    private string HeroName(int id)
    {
        var rng = new System.Random(State.mapSeed ^ (id * 7919));
        return NameStarts[rng.Next(NameStarts.Length)] + NameEnds[rng.Next(NameEnds.Length)] + " " + Epithets[rng.Next(Epithets.Length)];
    }

    // ---------- Experience ----------

    public float XpToNext(HeroState hero) => 60f * Mathf.Pow(hero.level + 1, 1.4f);

    public void GainXp(HeroState hero, float xp)
    {
        if (hero == null || hero.level + 1 >= LevelCap)
        {
            return;
        }
        hero.xp += xp;
        while (hero.level + 1 < LevelCap && hero.xp >= XpToNext(hero))
        {
            hero.xp -= XpToNext(hero);
            hero.level++;
            if (!offWorld) GameAudio.UI("level_up");
            Notify($"{hero.name} reached level {hero.level + 1}!");
            HeroChanged?.Invoke(hero);
        }
    }

    /// <summary>A hero fell: back on their feet at home after a while.</summary>
    public void HeroFell(HeroState hero)
    {
        if (hero == null) return;
        hero.downUntil = State.playTime + Database.heroRecoverySeconds;
        Notify($"{hero.name} has fallen, and will recover at home in {Database.heroRecoverySeconds:0}s.");
        if (!offWorld) GameAudio.UI("hero_fallen");
        HeroChanged?.Invoke(hero);
    }

    // ---------- Gear ----------

    /// <summary>The hero wearing this item, if any.</summary>
    public HeroState WhoWears(int uid) => uid == 0 ? null : State.heroes.Find(h => h.equipped.Contains(uid));

    public void Equip(HeroState hero, ItemInstance item)
    {
        var template = GetTemplate(item?.template);
        if (hero == null || template == null || !Items.Fits(template, FindUnit(hero.unitId), out _))
        {
            return;
        }
        // Take it off whoever had it.
        var other = WhoWears(item.uid);
        if (other != null)
        {
            other.equipped[other.equipped.IndexOf(item.uid)] = 0;
            HeroChanged?.Invoke(other);
        }
        while (hero.equipped.Count < 5) hero.equipped.Add(0);
        hero.equipped[(int)template.slot] = item.uid;
        GameAudio.UI("equip");
        HeroChanged?.Invoke(hero);
    }

    public void Unequip(HeroState hero, GearSlot slot)
    {
        if (hero == null) return;
        while (hero.equipped.Count < 5) hero.equipped.Add(0);
        hero.equipped[(int)slot] = 0;
        GameAudio.UI("equip", 0.6f);
        HeroChanged?.Invoke(hero);
    }

    /// <summary>What a hero has on, slot by slot, for drawing it on them (None = nothing there).</summary>
    public void WornLooks(HeroState hero, GearLook[] looks, Rarity[] rarities)
    {
        for (int i = 0; i < looks.Length; i++)
        {
            looks[i] = GearLook.None;
            rarities[i] = Rarity.Common;
            var item = hero != null && i < hero.equipped.Count ? GetItem(hero.equipped[i]) : null;
            var template = item != null ? GetTemplate(item.template) : null;
            if (template != null)
            {
                looks[i] = template.look;
                rarities[i] = item.rarity;
            }
        }
    }

    /// <summary>A hero's bonuses: every equipped item, plus +5% health and attack per level.</summary>
    public GearStats HeroStats(HeroState hero)
    {
        var total = new GearStats();
        if (hero == null) return total;
        foreach (int uid in hero.equipped)
        {
            var item = GetItem(uid);
            if (item != null) total.Add(Items.StatsOf(item, GetTemplate(item.template)));
        }
        total.health += 0.05f * hero.level;
        total.attack += 0.05f * hero.level;
        // The hero's path: a passive bonus that grows with its level.
        switch (hero.heroClass)
        {
            case HeroClass.Warrior: total.health += 0.15f + 0.02f * hero.level; total.armour += 10f + 2f * hero.level; break;
            case HeroClass.Ranger: total.range += 0.12f; total.haste += 0.1f + 0.01f * hero.level; break;
            case HeroClass.Mage: total.attack += 0.12f + 0.015f * hero.level; break;
            case HeroClass.Priest: total.mending += 1f + 0.3f * hero.level; break;
            case HeroClass.Navigator: total.speed += 0.12f + 0.01f * hero.level; total.haste += 0.05f; break;
            case HeroClass.Xenologist: total.xenobane += 0.25f + 0.02f * hero.level; total.health += 0.05f; break;
        }
        total.Add(CompanionStats(hero));   // a tamed companion (Wildlife.cs)
        return total;
    }

    /// <summary>The level (shown as level 2) at which a hero can choose its path.</summary>
    public const int ClassLevel = 1;

    /// <summary>Can this hero take this path? Warriors fight hand to hand, rangers shoot; anyone can be a mage or a priest.</summary>
    public bool CanChooseClass(HeroState hero, HeroClass cls, out string why)
    {
        why = null;
        if (hero == null || cls == HeroClass.None) { why = "No hero"; return false; }
        if (hero.heroClass != HeroClass.None) { why = $"Already a {hero.heroClass}"; return false; }
        if (hero.level < ClassLevel) { why = $"Reach level {ClassLevel + 1} first"; return false; }
        if (!ClassUnlocked(cls)) { why = cls == HeroClass.Navigator ? "Research Navigation first" : "Research Xenology first"; return false; }
        var unit = FindUnit(hero.unitId);
        bool ranged = unit != null && unit.projectile != null;
        if (cls == HeroClass.Warrior && ranged) { why = "Warriors fight hand to hand"; return false; }
        if (cls == HeroClass.Ranger && !ranged) { why = "Rangers fight from a distance"; return false; }
        return true;
    }

    public bool ChooseClass(HeroState hero, HeroClass cls, out string why)
    {
        if (!CanChooseClass(hero, cls, out why)) return false;
        hero.heroClass = cls;
        Notify($"{hero.name} takes the path of the {cls}.");
        GameAudio.UI("enchant");
        RaiseChanged();
        return true;
    }

    public static string ClassDescription(HeroClass cls) => cls switch
    {
        HeroClass.Warrior => "Tougher and better armoured. <b>Whirlwind</b>: every few seconds, strikes everything around it.",
        HeroClass.Ranger => "Longer reach and quicker shots. <b>Volley</b>: every few seconds, fires at three more enemies at once.",
        HeroClass.Mage => "Hits harder. <b>Firestorm</b>: every few seconds, fire bursts among the enemy it's fighting.",
        HeroClass.Priest => "Heals allies around it all the time. <b>Blessing</b>: every few seconds, heals everyone nearby a fifth of their health.",
        HeroClass.Navigator => "Quicker on its feet and with its strikes. On its world, ships launched there fly faster and reach further. <b>Warp Strike</b>: folds space to appear beside a distant enemy and hit it hard.",
        HeroClass.Xenologist => "Hits the natives of other worlds much harder. On its world, alien ruins, vaults, lairs and anomalies give more. <b>Beguile</b>: turns a native fighter to your side for a while.",
        _ => "",
    };

    /// <summary>The star paths need their research: Navigation for Navigators, Xenology for Xenologists.</summary>
    public bool ClassUnlocked(HeroClass cls) => cls switch
    {
        HeroClass.Navigator => State.researched.Contains("Navigation"),
        HeroClass.Xenologist => State.researched.Contains("Xenology"),
        _ => true,
    };

    /// <summary>The best level among heroes of a path on the world you're on and on their feet (-1: none).</summary>
    private int BestHereLevel(HeroClass cls)
    {
        int best = -1;
        foreach (var h in State.heroes)
            if (h.heroClass == cls && HeroHere(h) && !IsDown(h)) best = Mathf.Max(best, h.level);
        return best;
    }

    /// <summary>A Navigator here: ships go faster (a share) and further (light-years).</summary>
    private float NavigatorSpeed { get { int l = BestHereLevel(HeroClass.Navigator); return l < 0 ? 1f : 1.2f + 0.02f * l; } }
    private float NavigatorReach { get { int l = BestHereLevel(HeroClass.Navigator); return l < 0 ? 0f : 4f + 0.4f * l; } }

    // ---------- Loot ----------

    /// <summary>
    /// A chance of a xeno-artefact from something found on another world (a Xenologist on this
    /// world makes it likelier).
    /// </summary>
    private void XenoFind(float chance, string from, Rarity minRarity = Rarity.Uncommon)
    {
        if (!OnAlienWorld) return;
        if (UnityEngine.Random.value < chance * XenoRewardFactor) GainLoot(0.3f, 0.45f, from, minRarity, true);
    }

    /// <summary>How much more alien finds give: a Xenologist on this world knows what to look for.</summary>
    public float XenoRewardFactor => 1f + XenologistBonus();

    private float XenologistBonus() { int l = BestHereLevel(HeroClass.Xenologist); return l < 0 ? 0f : 0.3f + 0.02f * l; }

    /// <summary>
    /// Roll a piece of gear into the inventory. Its strength centres on your progress (plus
    /// 'sourceBonus' for tough sources) with a slim chance of something far beyond.
    /// </summary>
    public ItemInstance GainLoot(float sourceBonus, float luck, string from, Rarity minRarity = Rarity.Common, bool xeno = false)
    {
        var rng = new System.Random(State.mapSeed ^ (State.nextItemId * 104729) ^ (int)(State.playTime * 10));
        var item = Items.Roll(Database.itemTemplates, Progress, sourceBonus, luck, State.nextItemId, rng, minRarity, xeno)
            ?? (xeno ? Items.Roll(Database.itemTemplates, Progress, sourceBonus, luck, State.nextItemId, rng, minRarity) : null);   // (no artefacts set up yet)
        if (item == null)
        {
            return null;
        }
        State.nextItemId++;
        State.inventory.Add(item);
        string ahead = item.power >= State.ageIndex + 1.5f ? " <color=#fd6>(far beyond your age!)</color>" : "";
        if (!offWorld) GameAudio.UI("drop_" + item.rarity.ToString().ToLowerInvariant());
        Notify($"Found <color=#{Items.RarityHex(item.rarity)}>{item.name}</color> {from}.{ahead}");
        return item;
    }
}
