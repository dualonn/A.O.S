/// <summary>
/// Every world sprite's place in the draw order, in one table. Everything is on the Default sorting
/// layer; bands are sorting orders, far apart so nothing ties by accident. Inside a band, sprites
/// that stand on the ground (the World and Air bands) sort by z: lower on screen = nearer = in front
/// (see <see cref="Depth"/>). Screen-space UI canvases are separate and not listed here.
/// </summary>
public static class Sorting
{
    // ---------- Below everything ----------
    /// <summary>A dungeon's painted level, under the map itself.</summary>
    public const int DungeonFloor = -110;

    // ---------- Terrain (bottom to top) ----------
    /// <summary>Unexplored land colour.</summary>
    public const int Fog = -100;
    /// <summary>Ground tiles (grass, biomes).</summary>
    public const int Terrain = -90;
    /// <summary>Hill edge layers: Hills + tier, tiers 0..7 (stay under Water).</summary>
    public const int Hills = -88;
    public const int Water = -80;
    /// <summary>Region overview and strategy pages for the zoomed-out maps (the world still draws above).</summary>
    public const int Overview = -70;
    /// <summary>Placement grid lines.</summary>
    public const int GridLines = -65;
    /// <summary>Flat marks on the ground: flood water, mud, lava, craters, cracks.</summary>
    public const int GroundFx = -60;
    /// <summary>Flat decals under figures: camp grounds, fish shoals.</summary>
    public const int GroundDecal = -50;
    /// <summary>A selected building's outline.</summary>
    public const int Selection = -49;
    /// <summary>Selection rings, hero glows, ring pulses: under the body.</summary>
    public const int Rings = -48;

    // ---------- Things standing on the ground (Y-sorted by z) ----------
    /// <summary>Units, villagers, buildings, deposits, ships, caravans, decor, flames, pickups, sea creatures.</summary>
    public const int World = 0;
    /// <summary>Flyers and raised pennants: over everything standing on the ground.</summary>
    public const int Air = 10;

    // ---------- Above the world ----------
    public const int Projectile = 20;
    /// <summary>Puffs, splashes, water arcs.</summary>
    public const int Fx = 30;
    /// <summary>Health bars: back at Bars, fill at BarFill.</summary>
    public const int Bars = 40;
    public const int BarFill = 41;
    /// <summary>Name tags and building labels.</summary>
    public const int Labels = 50;
    /// <summary>Floating trade text.</summary>
    public const int Popup = 51;
    /// <summary>The ring where a move order was given.</summary>
    public const int OrderMarker = 52;
    public const int BoxFill = 53;
    public const int BoxOutline = 54;
    /// <summary>Darkness over dungeon rooms not reached yet.</summary>
    public const int DungeonDark = 55;

    // ---------- Overlays ----------
    public const int UtlCover = 60;
    public const int UtlWire = 61;
    public const int UtlLink = 62;
    public const int UtlHaze = 63;
    public const int RaidLine = 64;
    /// <summary>Settlement / raid thumbnails on the zoomed-out maps.</summary>
    public const int MapMarker = 65;
    public const int WonderGlow = 66;
    public const int WonderIcon = 67;
    public const int QuestMapIcon = 68;
    public const int RaidMarker = 69;
    public const int RaidLabel = 70;
    public const int WonderLabel = 71;

    // ---------- Building placement ----------
    public const int PlaceCatchment = 75;
    public const int PlaceGhost = 76;
    public const int PlaceGhostSprite = 77;
    public const int PlaceInfo = 78;

    // ---------- Sky ----------
    /// <summary>Disaster smoke.</summary>
    public const int Smoke = 80;
    public const int WeatherTint = 81;
    public const int WeatherBand = 82;
    public const int WeatherParticle = 83;
    public const int WeatherBolt = 84;
    public const int WeatherFlash = 85;
    /// <summary>Meteor trails (head at Sky + 1).</summary>
    public const int Sky = 86;

    // ---------- Depth within a band ----------
    /// <summary>A hair nearer than the parent: draws just in front of it at the same depth.</summary>
    public const float Front = -0.001f;
    public const float Front2 = -0.002f;
    public const float Front4 = -0.004f;
    /// <summary>A hair further than the parent: just behind it.</summary>
    public const float Behind2 = 0.002f;

    /// <summary>Things lower on screen get a nearer z, so they draw in front.</summary>
    public static float Depth(float footY) => footY * 0.01f;
}
