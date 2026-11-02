using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>A kind of place in the wild.</summary>
public enum SiteKind { Ruins, Dungeon, Lair, Shrine, Cache, LoreStone }

/// <summary>A place in the wild found on the map: placed from the map seed, saved so what you did there sticks.</summary>
[Serializable]
public class SiteState
{
    public SiteKind kind;
    [Tooltip("The map zone it sits in (one site per zone at most).")]
    public Vector2Int zoneXY;
    [Tooltip("Bottom-left tile of its footprint.")]
    public Vector2Int cell;
    [Tooltip("Which creatures, blessing, lore or look it has.")]
    public int variant;
    [Tooltip("Looted, used or read.")]
    public bool done;
    [Tooltip("Seconds until it fills up again (dungeons and lairs).")]
    public float resetIn;
    [Tooltip("Tougher each time it comes back.")]
    public int extraTier;

    public int Size => kind switch
    {
        SiteKind.Ruins or SiteKind.Dungeon or SiteKind.Lair => 3,
        SiteKind.Shrine => 2,
        _ => 1,
    };

    public Vector2 Centre => cell + Vector2.one * (Size * 0.5f);

    /// <summary>Sites with creatures to fight.</summary>
    public bool Guarded => kind == SiteKind.Ruins || kind == SiteKind.Dungeon || kind == SiteKind.Lair;
}

/// <summary>Wild sites: ruins, dungeons, lairs, shrines, caches and lore stones.</summary>
public partial class Settlement
{
    public event Action<SiteState> SiteChanged;

    /// <summary>Lair creature kinds (variant % 3).</summary>
    public const int LairWolves = 0, LairSpiders = 1, LairTroll = 2;

    /// <summary>Give explored zones their wild sites (the same zones always get the same ones).</summary>
    public void RollSites(IEnumerable<Vector2Int> zones)
    {
        if (!IsFounded || Map == null) return;
        var settlements = SettlementZones();
        foreach (var zone in zones)
        {
            if (State.sites.Exists(s => s.zoneXY == zone) || State.camps.Exists(c => c.zoneXY == zone)) continue;
            if (CampHash(zone, 301) >= Database.siteChance) continue;
            if (!Map.IsZoneRevealed(zone.x, zone.y)) continue;
            // Pick the kind: caches and lore are common, dungeons and shrines rare.
            float k = CampHash(zone, 302);
            var kind = k < 0.24f ? SiteKind.Cache : k < 0.44f ? SiteKind.LoreStone : k < 0.64f ? SiteKind.Ruins
                : k < 0.80f ? SiteKind.Lair : k < 0.90f ? SiteKind.Shrine : SiteKind.Dungeon;
            var site = new SiteState { kind = kind, zoneXY = zone, variant = (int)(CampHash(zone, 303) * 1000) };
            var rect = Map.ZoneRect(zone.x, zone.y);
            int size = site.Size;
            for (int attempt = 0; attempt < 24; attempt++)
            {
                int x = rect.x + 1 + (int)(CampHash(zone, 310 + attempt) * Mathf.Max(1, rect.width - size - 2));
                int y = rect.y + 1 + (int)(CampHash(zone, 350 + attempt) * Mathf.Max(1, rect.height - size - 2));
                var cell = new Vector2Int(x, y);
                var area = new RectInt(cell, Vector2Int.one * size);
                if (!CampSiteClear(area) || CampAt(area) != null || SiteAt(area) != null) continue;
                Vector2 centre = cell + Vector2.one * (size * 0.5f);
                // Fights keep away from home; small finds can be anywhere outside the settlement.
                float minDistance = site.Guarded ? Database.campMinDistance * 0.7f : 0f;
                bool tooClose = false;
                foreach (var s in settlements)
                    if (s.DistanceTo(centre) < Mathf.Max(minDistance, s.safeDistance * 0.6f)) tooClose = true;
                if (tooClose) break;
                site.cell = cell;
                State.sites.Add(site);
                break;
            }
        }
    }

    /// <summary>A site (not a looted cache) overlapping this area, if any.</summary>
    public SiteState SiteAt(RectInt area)
    {
        foreach (var site in State.sites)
        {
            if (site.kind == SiteKind.Cache && site.done) continue;
            if (new RectInt(site.cell, Vector2Int.one * site.Size).Overlaps(area)) return site;
        }
        return null;
    }

    /// <summary>How tough a site is, as an age index (like camps: further out is tougher, up to one age ahead).</summary>
    public int SiteTier(SiteState site) => TierAt(site.Centre, site.extraTier + (site.kind == SiteKind.Dungeon ? 1 : 0));

    private int TierAt(Vector2 centre, int extra)
    {
        float best = float.MaxValue;
        foreach (var s in SettlementZones())
            if (!s.IsOutpost) best = Mathf.Min(best, s.DistanceTo(centre));
        int byDistance = best == float.MaxValue ? 0 : Mathf.FloorToInt(Mathf.Max(0, best - Database.campMinDistance) / Mathf.Max(1f, Database.campTierDistance));
        return Mathf.Clamp(byDistance + extra, 0, Mathf.Min(Database.ages.Count - 1, State.ageIndex + 1));
    }

    private List<ResourceAmount> GiveResources(params (string id, int amount)[] amounts)
    {
        var loot = new List<ResourceAmount>();
        foreach (var (id, amount) in amounts)
        {
            var res = Database.resources.Find(r => r != null && r.id == id);
            if (res == null || amount <= 0) continue;
            loot.Add(new ResourceAmount { resource = res, amount = amount });
            AddResource(res, amount);
        }
        return loot;
    }

    private System.Random SiteRng(SiteState site) => new System.Random(State.mapSeed ^ site.cell.x * 31 ^ site.cell.y * 17 ^ (int)(State.playTime * 10));

    private void Finish(SiteState site, float resetSeconds)
    {
        site.done = true;
        site.resetIn = resetSeconds;
        SiteChanged?.Invoke(site);
        Changed?.Invoke();
    }

    /// <summary>The ruins' guardians are gone and someone opened the old chest.</summary>
    public void OpenRuins(SiteState site)
    {
        if (site.done) return;
        int t = SiteTier(site);
        var loot = GiveResources(("Stone", 80 * (t + 1)), ("Gold", t >= 2 ? 25 * t : 0), ("Bronze", t >= 1 ? 30 : 0));
        Notify($"You opened the chest in the ruins: {Describe(loot)}.");
        GainLoot(0.2f + 0.1f * t - 0.1f * State.ageIndex, 0.25f, "in the ruins");
        Finish(site, 0f);
    }

    /// <summary>A dungeon's last guardian fell: the great chest is yours. It fills up again in an hour.</summary>
    public void ClearDungeon(SiteState site)
    {
        if (site.done) return;
        int t = SiteTier(site);
        var loot = GiveResources(("Gold", 40 + 30 * t), ("Iron", t >= 2 ? 40 : 0), ("Food", 150));
        Notify($"The dungeon is cleared! Its treasure: {Describe(loot)}.");
        GainLoot(0.5f, 0.6f, "in the dungeon's hoard", Rarity.Rare);
        GainLoot(0.3f, 0.4f, "in the dungeon's hoard");
        Finish(site, Database.dungeonResetSeconds);
    }

    /// <summary>A lair's creatures are dead: hides, meat and whatever they hoarded.</summary>
    public void ClearLair(SiteState site)
    {
        if (site.done) return;
        int t = SiteTier(site);
        var loot = GiveResources(("Food", 120 * (t + 1)));
        Notify($"The lair is cleared: {Describe(loot)}.");
        var rng = SiteRng(site);
        if (rng.NextDouble() < 0.4 + 0.08 * t) GainLoot(0.15f * t, 0.2f, "in the lair");
        Finish(site, Database.lairResetSeconds);
    }

    /// <summary>A hero touched a shrine: a blessing for the whole realm (or a gift, once you have it).</summary>
    public void UseShrine(SiteState site, HeroState hero)
    {
        if (site.done || hero == null) return;
        var blessings = Database.blessings;
        Technology blessing = blessings.Count > 0 ? blessings[site.variant % blessings.Count] : null;
        if (blessing != null && !State.researched.Contains(blessing.id))
        {
            State.researched.Add(blessing.id);
            Notify($"{hero.name} knelt at the shrine. <color=#9df>{blessing.displayName}</color>: {blessing.description}");
        }
        else
        {
            Notify($"{hero.name} knelt at the shrine and found an offering left there.");
            GainLoot(0.3f, 0.5f, "at the shrine", Rarity.Uncommon);
        }
        GainXp(hero, 40f);
        Finish(site, 0f);
    }

    /// <summary>Someone stumbled on a cache or a lost caravan.</summary>
    public void TakeCache(SiteState site)
    {
        if (site.done) return;
        int t = SiteTier(site);
        var rng = SiteRng(site);
        bool caravan = site.variant % 3 == 0;
        var loot = caravan
            ? GiveResources(("Food", 80 + 40 * t), ("Wood", 80 + 40 * t), ("Coin", t >= 3 ? 30 : 0))
            : GiveResources(("Stone", 50 + 30 * t), ("Bronze", t >= 1 ? 20 : 0), ("Gold", t >= 2 ? 15 : 0));
        Notify(caravan ? $"You found a lost caravan: {Describe(loot)}." : $"You found a buried cache: {Describe(loot)}.");
        if (rng.NextDouble() < 0.25) GainLoot(0f, 0.1f, caravan ? "among the caravan's goods" : "in the cache");
        Finish(site, 0f);
    }

    /// <summary>Someone read a lore stone: a new entry in the journal.</summary>
    public void ReadLore(SiteState site)
    {
        if (site.done) return;
        int count = Lore.Entries.Length;
        int pick = -1;
        for (int i = 0; i < count; i++)
        {
            int id = (site.variant + i) % count;
            if (!State.journal.Contains(id)) { pick = id; break; }
        }
        if (pick < 0) pick = site.variant % count;
        if (!State.journal.Contains(pick)) State.journal.Add(pick);
        Notify($"Your people read a lore stone: <color=#fd6>{Lore.Entries[pick].title}</color>. (See the Journal.)");
        Finish(site, 0f);
    }

    private void TickSites(float dt)
    {
        foreach (var site in State.sites)
        {
            if (!site.done || site.resetIn <= 0) continue;
            site.resetIn -= dt;
            if (site.resetIn <= 0)
            {
                site.done = false;
                site.extraTier++;
                SiteChanged?.Invoke(site);
                Changed?.Invoke();
            }
        }
    }

    /// <summary>Shrine blessings you have (they count as research).</summary>
    public List<Technology> Blessings()
    {
        var list = new List<Technology>();
        foreach (var b in Database.blessings)
            if (b != null && State.researched.Contains(b.id)) list.Add(b);
        return list;
    }
}
