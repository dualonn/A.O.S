using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The troops stationed on the world you're on (brought by troopship): keeps them on the field
/// by the town centre, and puts troops and heroes aboard troopships to other worlds.
/// </summary>
public class Garrison
{
    private readonly List<Troop> live = new List<Troop>();
    private float nextSpawn;

    /// <summary>Spawn stationed troops that aren't on the field yet (a few a second).</summary>
    public void Keep(FreeModeController c)
    {
        live.RemoveAll(t => t == null || !t.IsAlive);
        var s = c.Settlement;
        if (s == null || !s.IsFounded || Time.time < nextSpawn) return;
        foreach (var g in s.CurrentWorld.garrison)
        {
            int have = 0;
            foreach (var t in live) if (t.GarrisonUnit == g.unitId) have++;
            if (have >= g.count) continue;
            var unit = s.FindUnit(g.unitId);
            if (unit == null || unit.prefab == null) continue;
            Vector3 centre = c.TownCenterPosition, spot = centre;
            for (int i = 0; i < 20; i++)
            {
                Vector2 o = Random.insideUnitCircle * (2.5f + i * 0.3f);
                var p = centre + new Vector3(o.x, o.y - 2f, 0);
                if (s.Map != null && s.Map.IsWalkable(p)) { spot = p; break; }
            }
            var troop = c.SpawnUnit(unit.prefab, spot).GetComponent<Troop>();
            if (troop == null) continue;
            troop.GarrisonUnit = g.unitId;
            troop.SetRallyPoint(spot);
            troop.SetStance(TroopStance.Defend, c.TownZoneFor(spot), c.SightRange);
            troop.SetDefendArea(c.SettlementFor(spot), s.Database.defendAlertRange);
            live.Add(troop);
            nextSpawn = Time.time + 0.15f;
            return;
        }
        nextSpawn = Time.time + 0.5f;
    }

    /// <summary>Troops here that can board: not heroes, not down a dungeon.</summary>
    public static List<Troop> Boardable(IEnumerable<Troop> from = null)
    {
        var list = new List<Troop>();
        if (from != null)
        {
            foreach (var t in from) if (t != null && t.IsAlive && t.Hero == null && t.Dungeon == null) list.Add(t);
            return list;
        }
        foreach (var u in Unit.Living)
            if (u is Troop t && t.IsAlive && t.Hero == null && t.Dungeon == null) list.Add(t);
        // The strongest go first.
        list.Sort((a, b) => Settlement.UnitPower(b.definition).CompareTo(Settlement.UnitPower(a.definition)));
        return list;
    }

    /// <summary>Put up to 'count' troops aboard a troopship to world 'to' (the selected ones, if given).</summary>
    public bool SendTroops(FreeModeController c, int to, int count, IEnumerable<Troop> pick = null)
    {
        var s = c.Settlement;
        var list = Boardable(pick);
        list.RemoveAll(t => t.definition == null || t.Moves == Movement.Water || s.FindUnit(t.definition.name) == null);
        count = Mathf.Min(count, list.Count, s.GarrisonRoom(to));
        if (!s.CanSendTroops(to, count, null, out string why)) { c.ShowMessage(why); return false; }
        var stocks = new List<GarrisonStock>();
        for (int i = 0; i < count; i++)
        {
            var t = list[i];
            string id = t.definition != null ? t.definition.name : null;
            if (id == null) continue;
            var stock = stocks.Find(g => g.unitId == id);
            if (stock == null) stocks.Add(new GarrisonStock { unitId = id, count = 1 });
            else stock.count++;
            if (t.GarrisonUnit != null)
            {
                s.GarrisonLost(t.GarrisonUnit);   // it leaves this world's garrison
                t.GarrisonUnit = null;
            }
            live.Remove(t);
            Object.Destroy(t.gameObject);         // (its barracks trains a replacement)
        }
        c.SetUnitSelection(null);
        return s.LaunchTroopship(to, stocks, null);
    }

    /// <summary>The hero who'd board next here: the selected one, or the highest level on its feet.</summary>
    public static HeroState HeroToSend(FreeModeController c)
    {
        var s = c.Settlement;
        foreach (var t in c.SelectedUnits)
            if (t != null && t.Hero != null && s.HeroHere(t.Hero) && !s.IsDown(t.Hero) && t.Dungeon == null) return t.Hero;
        HeroState best = null;
        foreach (var h in s.State.heroes)
            if (s.HeroHere(h) && !s.IsDown(h) && (best == null || h.level > best.level)) best = h;
        return best;
    }

    public bool SendHero(FreeModeController c, int to, HeroState hero)
    {
        if (hero == null) return false;
        return c.Settlement.LaunchTroopship(to, new List<GarrisonStock>(), hero);   // its troop leaves the field on its own
    }
}
