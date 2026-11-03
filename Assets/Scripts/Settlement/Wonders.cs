using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Wonders. Great Wonders are buildings, one per age, each the only one of its kind in the whole
/// game (across all your worlds): while one stands, its power works like a piece of research.
/// Natural Wonders are rare places on a world's map; finding one blesses that world for good.
/// </summary>
public partial class Settlement
{
    // ---------- Great Wonders ----------

    /// <summary>A Great Wonder can only be built once, anywhere: is one already standing or going up?</summary>
    public bool WonderTaken(BuildingDefinition def, out string where)
    {
        where = null;
        if (def == null || !def.wonder) return false;
        foreach (var w in State.worlds)
            foreach (var b in w.buildings)
                if (b.definitionId == def.id)
                {
                    where = w.name;
                    return true;
                }
        return false;
    }

    private float wonderCheckAt;

    /// <summary>
    /// Keep each wonder's power in step with the wonder: it works while one stands (on any of your
    /// worlds), and fades if it falls. One change at a time, so cached research totals notice.
    /// </summary>
    private void TickWonders(float dt)
    {
        wonderCheckAt -= dt;
        if (wonderCheckAt > 0f) return;
        wonderCheckAt = 2f;
        foreach (var def in Database.buildings)
        {
            if (def == null || !def.wonder || def.wonderEffect == null) continue;
            bool stands = false;
            foreach (var w in State.worlds)
            {
                foreach (var b in w.buildings)
                    if (b.definitionId == def.id && b.IsBuilt && b.health > 0) { stands = true; break; }
                if (stands) break;
            }
            string id = def.wonderEffect.id;
            bool has = State.researched.Contains(id);
            if (stands && !has)
            {
                State.researched.Add(id);
                Notify($"<color=#ffd56a><b>Wonder complete: the {def.displayName}!</b></color> {def.wonderEffect.description}");
                if (!offWorld) GameAudio.UI("wonder_complete");
                AddLoreByTitle(def.displayName);
                RaiseChanged();
                return;
            }
            if (!stands && has)
            {
                State.researched.Remove(id);
                Notify($"<color=#f87>The {def.displayName} has fallen.</color> Its power fades until it is rebuilt.");
                RaiseChanged();
                return;
            }
        }
    }

    /// <summary>Put the lore entry with this title in the journal (wonders' own stories).</summary>
    private void AddLoreByTitle(string title)
    {
        for (int i = 0; i < Lore.Entries.Length; i++)
        {
            if (Lore.Entries[i].title != title) continue;
            if (!State.journal.Contains(i)) State.journal.Add(i);
            return;
        }
    }

    // ---------- Natural Wonders ----------

    public struct NaturalWonderInfo
    {
        public string name, effect;
        public (string resource, float multiplier)[] production;   // a null resource means everything
        public float growth;
        public Biome[] biomes;
    }

    /// <summary>The natural wonders a world can have (a site's variant is its index here).</summary>
    public static readonly NaturalWonderInfo[] NaturalWonders =
    {
        new NaturalWonderInfo { name = "Thundering Falls", effect = "+15% food and +10% wood on this world.",
            production = new[] { ("Food", 1.15f), ("Wood", 1.1f) }, growth = 1f, biomes = new[] { Biome.Temperate, Biome.Jungle, Biome.Tundra } },
        new NaturalWonderInfo { name = "The Crystal Spire", effect = "+20% mana and +15% essence on this world.",
            production = new[] { ("Mana", 1.2f), ("Essence", 1.15f) }, growth = 1f, biomes = new[] { Biome.Crystal, Biome.Fungal } },
        new NaturalWonderInfo { name = "The Elder Tree", effect = "+20% wood on this world, and its people grow 15% faster.",
            production = new[] { ("Wood", 1.2f) }, growth = 1.15f, biomes = new[] { Biome.Temperate, Biome.Jungle, Biome.Swamp, Biome.Fungal } },
        new NaturalWonderInfo { name = "The Fire Caldera", effect = "+15% iron and steel on this world.",
            production = new[] { ("Iron", 1.15f), ("Steel", 1.15f) }, growth = 1f, biomes = new[] { Biome.Volcanic, Biome.Barren } },
        new NaturalWonderInfo { name = "The Sky Arch", effect = "+15% stone and +10% gold on this world.",
            production = new[] { ("Stone", 1.15f), ("Gold", 1.1f) }, growth = 1f, biomes = new[] { Biome.Desert, Biome.Barren, Biome.Tundra } },
        new NaturalWonderInfo { name = "The Singing Dunes", effect = "+15% gold and +10% coin on this world.",
            production = new[] { ("Gold", 1.15f), ("Coin", 1.1f) }, growth = 1f, biomes = new[] { Biome.Desert } },
        new NaturalWonderInfo { name = "The Aurora Lake", effect = "+10% mana and +5% of everything on this world.",
            production = new[] { ("Mana", 1.1f), (null, 1.05f) }, growth = 1f, biomes = new[] { Biome.Tundra, Biome.Crystal, Biome.Swamp } },
        new NaturalWonderInfo { name = "The Great Rift", effect = "+20% stone and +10% iron on this world.",
            production = new[] { ("Stone", 1.2f), ("Iron", 1.1f) }, growth = 1f, biomes = new[] { Biome.Volcanic, Biome.Barren, Biome.Temperate } },
    };

    /// <summary>At most this many natural wonders on one world.</summary>
    private const int MaxNaturalWonders = 2;
    private const float NaturalWonderChance = 0.025f;

    /// <summary>
    /// Now and then an explored zone holds a natural wonder. Runs after the ordinary sites, only in
    /// zones left empty, with its own hashes, so the usual camps and sites never change.
    /// </summary>
    private void RollNaturalWonders(IEnumerable<Vector2Int> zones)
    {
        if (!IsFounded || Map == null) return;
        int have = 0;
        var used = new HashSet<int>();
        foreach (var s in State.sites)
            if (s.kind == SiteKind.NaturalWonder) { have++; used.Add(s.variant); }
        if (have >= MaxNaturalWonders) return;
        var settlements = SettlementZones();
        foreach (var zone in zones)
        {
            if (have >= MaxNaturalWonders) return;
            if (State.sites.Exists(s => s.zoneXY == zone) || State.camps.Exists(c => c.zoneXY == zone)) continue;
            if (CampHash(zone, 701) >= NaturalWonderChance) continue;
            if (!Map.IsZoneRevealed(zone.x, zone.y)) continue;
            var rect = Map.ZoneRect(zone.x, zone.y);
            var biome = Map.BiomeAt((int)rect.center.x, (int)rect.center.y);
            // One that fits the land (and isn't on this world already).
            var fits = new List<int>();
            for (int i = 0; i < NaturalWonders.Length; i++)
                if (!used.Contains(i) && System.Array.IndexOf(NaturalWonders[i].biomes, biome) >= 0) fits.Add(i);
            if (fits.Count == 0) continue;
            int variant = fits[(int)(CampHash(zone, 702) * fits.Count) % fits.Count];
            var site = new SiteState { kind = SiteKind.NaturalWonder, zoneXY = zone, variant = variant };
            int size = site.Size;
            for (int attempt = 0; attempt < 24; attempt++)
            {
                int x = rect.x + 1 + (int)(CampHash(zone, 710 + attempt) * Mathf.Max(1, rect.width - size - 2));
                int y = rect.y + 1 + (int)(CampHash(zone, 750 + attempt) * Mathf.Max(1, rect.height - size - 2));
                var cell = new Vector2Int(x, y);
                var area = new RectInt(cell, Vector2Int.one * size);
                if (!CampSiteClear(area) || CampAt(area) != null || SiteAt(area) != null) continue;
                Vector2 centre = cell + Vector2.one * (size * 0.5f);
                bool tooClose = false;
                foreach (var s in settlements)
                    if (s.DistanceTo(centre) < s.safeDistance * 0.6f) tooClose = true;
                if (tooClose) break;
                site.cell = cell;
                State.sites.Add(site);
                have++;
                used.Add(variant);
                break;
            }
        }
    }

    public static NaturalWonderInfo NaturalWonderOf(SiteState site) =>
        NaturalWonders[Mathf.Clamp(site.variant, 0, NaturalWonders.Length - 1)];

    /// <summary>Someone saw a natural wonder for the first time: its blessing is this world's for good.</summary>
    public void DiscoverNaturalWonder(SiteState site)
    {
        if (site == null || site.done || site.kind != SiteKind.NaturalWonder) return;
        var info = NaturalWonderOf(site);
        Notify($"<color=#8fe><b>A natural wonder: {info.name}!</b></color> {info.effect}");
        AddLoreByTitle(info.name);
        naturalAt = -99f;
        Finish(site, 0f);
    }

    // The bonuses of this world's discovered natural wonders, worked out again once a second.
    private float naturalAt = -99f, naturalGrowth = 1f, naturalAll = 1f;
    private WorldState naturalFor;
    private readonly Dictionary<string, float> naturalProduction = new Dictionary<string, float>();

    private void RefreshNatural()
    {
        if (naturalFor == CurrentWorld && Mathf.Abs(State.playTime - naturalAt) < 1f) return;
        naturalFor = CurrentWorld;
        naturalAt = State.playTime;
        naturalProduction.Clear();
        naturalGrowth = 1f;
        naturalAll = 1f;
        foreach (var s in State.sites)
        {
            if (s.kind != SiteKind.NaturalWonder || !s.done) continue;
            var info = NaturalWonderOf(s);
            naturalGrowth *= info.growth;
            foreach (var (res, m) in info.production)
            {
                if (res == null) { naturalAll *= m; continue; }
                naturalProduction[res] = (naturalProduction.TryGetValue(res, out var f) ? f : 1f) * m;
            }
        }
    }

    /// <summary>This world's natural wonders' effect on a resource's production.</summary>
    public float NaturalWonderFactor(ResourceType r)
    {
        if (r == null) return 1f;
        RefreshNatural();
        return naturalAll * (naturalProduction.TryGetValue(r.id, out var f) ? f : 1f);
    }

    /// <summary>This world's natural wonders' effect on how fast its people grow.</summary>
    public float NaturalWonderGrowth
    {
        get { RefreshNatural(); return naturalGrowth; }
    }
}
