using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Warfare, the settlement side: regiments (the experience of each training building, which
/// outlives its troops), drill upgrades, commanders and their traits, doctrines, battle reports,
/// and raids fought out of sight on other worlds. The battlefield side is WarField.
/// </summary>
public partial class Settlement
{
    public WarSave War
    {
        get
        {
            if (State.war == null) State.war = new WarSave();
            return State.war;
        }
    }

    public bool WarDoc(string id) => State != null && State.researched.Contains(id);

    // ---------- Regiments ----------

    public WarRegiment Regiment(int building, bool create)
    {
        var list = War.regiments;
        for (int i = 0; i < list.Count; i++) if (list[i].building == building) return list[i];
        if (!create) return null;
        var r = new WarRegiment { building = building, world = State.currentWorld };
        list.Add(r);
        return r;
    }

    public static int RegimentRank(WarRegiment r) => r == null ? 0 : WarCatalog.RankOf(r.xp, WarCatalog.RegimentXp);

    /// <summary>The rank new recruits of this building join at: the regiment's, capped by its drill (Regular with none, up to Elite).</summary>
    public int StartRank(int building)
    {
        var r = Regiment(building, false);
        return r == null ? 0 : Mathf.Min(RegimentRank(r), 1 + r.drill, 3);
    }

    /// <summary>How fast troops learn: drill, and the Muster Rolls and General Staff doctrines.</summary>
    public float WarXpFactor(int building)
    {
        var r = building != 0 ? Regiment(building, false) : null;
        float f = 1f + 0.1f * (r != null ? r.drill : 0);
        if (WarDoc(WarCatalog.DocMuster)) f *= 1.1f;
        if (WarDoc(WarCatalog.DocGeneralStaff)) f *= 1.1f;
        return f;
    }

    /// <summary>A regiment learns from its troops' fights (xp already scaled).</summary>
    public void WarRegimentGain(int building, float xp, int kills)
    {
        if (building == 0) return;
        var r = Regiment(building, true);
        int before = RegimentRank(r);
        r.xp += xp;
        r.kills += kills;
        int after = RegimentRank(r);
        if (after > before && after > r.bestRank)
        {
            r.bestRank = after;
            string name = RegimentName(building);
            int start = Mathf.Min(after, 1 + r.drill, 3);
            Notify($"The {name} regiment is now {WarCatalog.RankText(after)}." +
                   (start > 0 ? $" New recruits join as {WarCatalog.RankName(start)}s." : ""));
            if (after >= 3) XChronicle(XChronicleKind.War, $"The {name} regiment earned the rank of {WarCatalog.RankName(after)}.");
            if (!offWorld) GameAudio.UI("level_up", 0.6f);
        }
    }

    public string RegimentName(int building)
    {
        var b = State.buildings.Find(x => x.instanceId == building);
        var def = b != null ? Database.GetBuilding(b.definitionId) : null;
        return def != null ? def.displayName : "old";
    }

    /// <summary>Drop the records of regiments whose building is gone (this world only).</summary>
    public void WarCleanupRegiments()
    {
        War.regiments.RemoveAll(r => r.world == State.currentWorld && !State.buildings.Exists(b => b.instanceId == r.building));
    }

    /// <summary>For the defence sum when raids are fought out by numbers: a seasoned regiment counts for more.</summary>
    public UnitStatModifier WarRegimentModifier(BuildingState b)
    {
        int rank = b != null ? Mathf.Max(StartRank(b.instanceId), RegimentRank(Regiment(b.instanceId, false)) - 1) : 0;
        if (rank <= 0) return WarIdentityMod;
        return new UnitStatModifier { healthMultiplier = WarCatalog.RankHealth[rank], attackMultiplier = WarCatalog.RankAttack[rank] };
    }

    private static readonly UnitStatModifier WarIdentityMod = new UnitStatModifier();

    // ---------- Drill (a capped upgrade line on every training building) ----------

    public List<ResourceAmount> DrillCost(BuildingState b)
    {
        var def = b != null ? Database.GetBuilding(b.definitionId) : null;
        var r = b != null ? Regiment(b.instanceId, false) : null;
        int level = r != null ? r.drill : 0;
        int age = def != null && def.age != null ? def.age.order : State.ageIndex;
        float scale = (level + 1) * (1f + 0.55f * age);
        var cost = new List<ResourceAmount>();
        if (Database.food != null) cost.Add(new ResourceAmount { resource = Database.food, amount = Mathf.RoundToInt(120 * scale / 10f) * 10 });
        var second = age >= 3 && Database.coin != null ? Database.coin : Database.GetResource("Wood");
        if (second != null) cost.Add(new ResourceAmount { resource = second, amount = Mathf.RoundToInt((second == Database.coin ? 40 : 90) * scale / 10f) * 10 });
        return cost;
    }

    public bool CanDrill(BuildingState b, out string why)
    {
        why = null;
        var def = b != null ? Database.GetBuilding(b.definitionId) : null;
        if (def == null || def.trainsUnit == null) { why = "Trains no troops"; return false; }
        if (!b.IsBuilt) { why = "Still being built"; return false; }
        var r = Regiment(b.instanceId, false);
        if (r != null && r.drill >= WarCatalog.MaxDrill) { why = "Fully drilled"; return false; }
        if (!CanAfford(DrillCost(b))) { why = "Not enough resources"; return false; }
        return true;
    }

    public bool Drill(BuildingState b)
    {
        if (!CanDrill(b, out string why)) { Notify(why); return false; }
        Spend(DrillCost(b));
        var r = Regiment(b.instanceId, true);
        r.drill++;
        Notify($"{RegimentName(b.instanceId)}: {WarCatalog.DrillNames[r.drill - 1]} set up. Troops learn faster, hold firmer, and recruits can join at a higher rank.");
        GameAudio.UI("confirm");
        RaiseChanged();
        return true;
    }

    /// <summary>The regiment's lines in the building panel.</summary>
    public void WarSelectionInfo(StringBuilder sb, BuildingState state, BuildingDefinition def)
    {
        if (def == null || def.trainsUnit == null || !state.IsBuilt) return;
        var r = Regiment(state.instanceId, false);
        if (r == null)
        {
            sb.Append("<size=85%><color=#aaa>Regiment: untried (no battles yet)</color></size>\n");
            return;
        }
        int rank = RegimentRank(r);
        string next = rank < WarCatalog.MaxRank ? $" {r.xp:0}/{WarCatalog.RegimentXp[rank + 1]:0} xp" : "";
        sb.Append($"Regiment: {WarCatalog.RankText(rank)}<size=80%><color=#aaa>{next} · {r.kills} kills · {r.battles} battles</color></size>\n");
        int start = StartRank(state.instanceId);
        sb.Append($"<size=85%><color=#aaa>Recruits join as {WarCatalog.RankName(start)}" +
                  (r.drill > 0 ? $" · Drill {r.drill}/{WarCatalog.MaxDrill}" : "") + "</color></size>\n");
    }

    // ---------- Commanders ----------

    public WarHeroRecord WarHero(int hero, bool create)
    {
        var list = War.heroes;
        for (int i = 0; i < list.Count; i++) if (list[i].hero == hero) return list[i];
        if (!create) return null;
        var r = new WarHeroRecord { hero = hero };
        list.Add(r);
        return r;
    }

    /// <summary>The commander of the army on the world being ticked (on this world, not necessarily on its feet).</summary>
    public HeroState CommanderHere()
    {
        foreach (var r in War.heroes)
        {
            if (!r.commanding) continue;
            var h = GetHero(r.hero);
            if (h != null && HeroHere(h)) return h;
        }
        return null;
    }

    public bool IsCommander(HeroState h)
    {
        var r = h != null ? WarHero(h.id, false) : null;
        return r != null && r.commanding;
    }

    /// <summary>Make this hero the commander of the army on its world (one per world).</summary>
    public void AppointCommander(HeroState h)
    {
        if (h == null) return;
        foreach (var r in War.heroes)
        {
            if (!r.commanding || r.hero == h.id) continue;
            var other = GetHero(r.hero);
            if (other == null || other.world == h.world) r.commanding = false;
        }
        WarHero(h.id, true).commanding = true;
        Notify($"{h.name} now commands the army. Troops near them fight harder and hold firmer.");
        GameAudio.UI("hero_recruit", 0.7f);
        RaiseChanged();
    }

    public void RelieveCommander(HeroState h)
    {
        var r = h != null ? WarHero(h.id, false) : null;
        if (r == null || !r.commanding) return;
        r.commanding = false;
        Notify($"{h.name} is relieved of command.");
        RaiseChanged();
    }

    public int WarTraitSlots => WarCatalog.TraitSlots(State.ageIndex) + (WarDoc(WarCatalog.DocStaff) ? 1 : 0);

    /// <summary>Count something towards the commander of this world's traits.</summary>
    public void WarCount(WarCounter c, int n = 1)
    {
        var h = CommanderHere();
        if (h == null) return;
        WarHero(h.id, true).Add(c, n);
    }

    /// <summary>Earn any command traits now due (newest age's first is fine: in list order, within the age).</summary>
    public void WarCheckTraits(HeroState h)
    {
        if (h == null) return;
        var rec = WarHero(h.id, true);
        int slots = WarTraitSlots;
        foreach (var t in WarCatalog.Traits)
        {
            if (rec.traits.Count >= slots) break;
            if (t.minAge > State.ageIndex || rec.traits.Contains(t.id) || rec.Get(t.counter) < t.threshold) continue;
            rec.traits.Add(t.id);
            Notify($"{h.name} earned the command trait <color=#fd6>{t.name}</color>: {t.text}");
            XChronicle(XChronicleKind.Hero, $"{h.name} became known as a {t.name}.");
            if (!offWorld) GameAudio.UI("enchant");
            HeroChanged?.Invoke(h);
        }
    }

    /// <summary>The next trait a commander is working towards, for the tooltip.</summary>
    public string WarNextTraits(HeroState h, int max = 3)
    {
        var rec = h != null ? WarHero(h.id, false) : null;
        var sb = new StringBuilder();
        int shown = 0;
        foreach (var t in WarCatalog.Traits)
        {
            if (shown >= max) break;
            if (rec != null && rec.traits.Contains(t.id)) continue;
            if (t.minAge > State.ageIndex) continue;
            int have = rec != null ? rec.Get(t.counter) : 0;
            sb.Append($"\n<color=#aaa>{t.name}: {have}/{t.threshold} {WarCatalog.CounterName(t.counter)}</color>");
            shown++;
        }
        return sb.ToString();
    }

    // ---------- Battle reports ----------

    public void WarFileReport(WarReport r)
    {
        r.at = State.playTime;
        r.world = CurrentWorld.name;
        var list = War.reports;
        list.Add(r);
        if (list.Count > 12) list.RemoveRange(0, list.Count - 12);
        War.battles++;
        if (r.victory) War.victories++;
        if (r.simulated) return;   // (the background raid already tells you)
        Notify($"<color=#fc8>Battle report</color> - {r.Line()}");
        XChronicle(XChronicleKind.War, r.Line());
    }

    // ---------- Raids fought out of sight ----------

    /// <summary>A raid settled by the numbers (another world, or left mid-raid): regiments and the commander there still learn from it.</summary>
    public void WarAutoResolved(Wave wave, float severity)
    {
        if (wave == null || wave.units.Count == 0) return;
        float xp = 8f * Mathf.Sqrt(wave.units.Count) * (severity <= 0f ? 1f : 0.5f);
        foreach (var b in State.buildings)
        {
            if (!b.IsBuilt) continue;
            var def = Database.GetBuilding(b.definitionId);
            if (def == null || def.trainsUnit == null) continue;
            var r = Regiment(b.instanceId, true);
            r.world = State.currentWorld;
            r.battles++;
            WarRegimentGain(b.instanceId, xp * WarXpFactor(b.instanceId), 0);
        }
        var h = CommanderHere();
        if (h != null)
        {
            var rec = WarHero(h.id, true);
            rec.Add(WarCounter.Battles, 1);
            if (severity <= 0f) { rec.Add(WarCounter.Victories, 1); rec.Add(WarCounter.Defences, 1); }
            if (OnAlienWorld) rec.Add(WarCounter.AlienBattles, 1);
            WarCheckTraits(h);
        }
        WarFileReport(new WarReport
        {
            raiders = wave.units.Count, victory = severity <= 0f, simulated = true,
            lost = Mathf.RoundToInt(severity * wave.units.Count * 0.5f),
            boss = wave.boss != null ? wave.boss.displayName : null,
        });
    }

    /// <summary>A live raid is over: the commander's tallies, traits and a little hero experience.</summary>
    public void WarBattleOver(bool victory, bool inTown, bool hard)
    {
        var h = CommanderHere();
        if (h == null) return;
        var rec = WarHero(h.id, true);
        rec.Add(WarCounter.Battles, 1);
        if (victory)
        {
            rec.Add(WarCounter.Victories, 1);
            if (inTown) rec.Add(WarCounter.Defences, 1);
            if (hard) rec.Add(WarCounter.HardWins, 1);
            if (!IsDown(h)) GainXp(h, 20f + 6f * State.ageIndex);
        }
        if (OnAlienWorld) rec.Add(WarCounter.AlienBattles, 1);
        WarCheckTraits(h);
    }
}
