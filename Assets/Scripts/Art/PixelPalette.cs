using UnityEngine;

/// <summary>
/// The game's colour ramps (dark to light), shared by everything drawn in pixel art: a warm,
/// saturated fantasy palette lit from the top-left.
/// </summary>
public static class PixelPalette
{
    private static Color32 H(string hex) => PixelCanvas.Hex(hex);

    public static Color32 Hex(string hex) => PixelCanvas.Hex(hex);

    public static readonly Color32 Ink = H("241a24");
    public static readonly Color32 White = H("ffffff");

    public static readonly Color32[] Skin = { H("6e3b2f"), H("b06a4f"), H("dd9c76"), H("f3c39e") };
    public static readonly Color32[] Blue = { H("1d2557"), H("2c4a9c"), H("437fdc"), H("86bcff") };
    public static readonly Color32[] Red = { H("561726"), H("97262d"), H("d4453a"), H("ff8466") };
    public static readonly Color32[] Steel = { H("353d4b"), H("62707f"), H("a3b2bf"), H("eef5f8") };
    public static readonly Color32[] Bronze = { H("6b3a1c"), H("a8642a"), H("d99a44"), H("f6d07a") };
    public static readonly Color32[] Wood = { H("45261c"), H("76452b"), H("a96d3f"), H("d79d62") };
    public static readonly Color32[] Leather = { H("35201c"), H("5f3829"), H("8c5a3a"), H("b88757") };
    public static readonly Color32[] Cloth = { H("2c2430"), H("4a3d4a"), H("6d5d66"), H("948089") };
    public static readonly Color32[] Linen = { H("7a6a58"), H("b5a185"), H("dccab0"), H("f5ead6") };
    public static readonly Color32[] Gold = { H("7f421c"), H("cc8526"), H("f6c544"), H("fff2a0") };
    public static readonly Color32[] Hair = { H("24161a"), H("46281f"), H("70412b"), H("9e6239") };
    public static readonly Color32[] Fur = { H("2f1d17"), H("573626"), H("84553a"), H("b07d55") };
    public static readonly Color32[] Wolf = { H("3a3a44"), H("62626c"), H("8e8c92"), H("c2bfc0") };
    public static readonly Color32[] Bone = { H("8b7b67"), H("c4b49a"), H("e9dfc8"), H("fffaf0") };
    public static readonly Color32[] Purple = { H("2e1a3e"), H("54296e"), H("8a43a8"), H("c07de0") };
    public static readonly Color32[] Green = { H("1e3a24"), H("2f6236"), H("4f8f48"), H("86c46a") };
    public static readonly Color32[] Straw = { H("6b4420"), H("a3702f"), H("d6a347"), H("f4d579") };
    public static readonly Color32[] Horse = { H("2e1a14"), H("5a3221"), H("8a5232"), H("b9804f") };
    public static readonly Color32[] DarkHorse = { H("161214"), H("2e2426"), H("4a3c3c"), H("6e5c58") };
    public static readonly Color32[] Troll = { H("26341f"), H("42583a"), H("66844e"), H("93b06c") };
    public static readonly Color32[] Elephant = { H("38363f"), H("5d5a64"), H("858089"), H("aca6ab") };

    public static readonly Color32[] Grass = { H("1e4a2f"), H("2f7539"), H("4fa144"), H("84cc5c"), H("bde97f") };
    public static readonly Color32[] Leaf = { H("163a2a"), H("245f34"), H("3c8c3e"), H("6dbb4f"), H("a5df6e") };
    public static readonly Color32[] Pine = { H("10302a"), H("1b4a38"), H("2b6b46"), H("4a9458"), H("7dbf6e") };
    public static readonly Color32[] Dirt = { H("5c3d2a"), H("8a6040"), H("b88a5c"), H("dcb683") };
    public static readonly Color32[] Sand = { H("8a6a42"), H("c49a62"), H("e4c48a"), H("f6e2b0") };
    public static readonly Color32[] Water = { H("1b3a78"), H("2763b0"), H("3c97d6"), H("7fd0f0"), H("d4f6ff") };
    public static readonly Color32[] Stone = { H("3e3a44"), H("68626b"), H("9a929a"), H("cbc4c4"), H("efe9e4") };
    public static readonly Color32[] Rock = { H("4a4038"), H("756658"), H("a08c78"), H("c8b49c"), H("e8dac4") };
    public static readonly Color32[] Plaster = { H("8a7058"), H("c7ad8a"), H("ead8b6"), H("fbf1dc") };
    public static readonly Color32[] Mud = { H("5a3e28"), H("8c6642"), H("b58c60"), H("d8b486") };
    public static readonly Color32[] Thatch = { H("6b4420"), H("a3702f"), H("d6a347"), H("f4d579") };
    public static readonly Color32[] Roof = { H("4e1b26"), H("8a2d33"), H("c4463f"), H("ec7a5c") };
    public static readonly Color32[] Tile = { H("5a2a1e"), H("964632"), H("c8693f"), H("eb9a62") };
    public static readonly Color32[] Marble = { H("8c8a94"), H("bcbac2"), H("e2e0e6"), H("fbfaff") };
    public static readonly Color32[] Glow = { H("9a4a1a"), H("e58a2a"), H("ffcf5a"), H("fff4b0") };
    public static readonly Color32[] Fire = { H("8a1e1e"), H("e0461e"), H("ff9a2a"), H("ffe66a") };
    public static readonly Color32[] Iron = { H("2a2e36"), H("4a505c"), H("767e8a"), H("a8b0ba") };
    public static readonly Color32[] Coal = { H("141216"), H("26222a"), H("3a3440"), H("5a5260") };
    public static readonly Color32[] Berry = { H("5a0e24"), H("a01e3a"), H("e0365a"), H("ff8aa0") };
    /// <summary>The cold blue-green glow of carved runes.</summary>
    public static readonly Color32[] Rune = { H("143a44"), H("1f7a80"), H("46d6c8"), H("c4fff2") };
    /// <summary>Polished brass: steam fittings, gauges, automaton plate.</summary>
    public static readonly Color32[] Brass = { H("6a4416"), H("b07c26"), H("e2b44a"), H("fff0a8") };
    /// <summary>The violet-blue crackle of aether.</summary>
    public static readonly Color32[] Aether = { H("2a1f5c"), H("5a3fc0"), H("9f8cff"), H("e6e0ff") };
    /// <summary>Olive drab: Modern uniforms, helmets and armour.</summary>
    public static readonly Color32[] Olive = { H("2e331c"), H("4a5230"), H("6b7546"), H("95a066") };
    /// <summary>Khaki and trench-coat tan.</summary>
    public static readonly Color32[] Khaki = { H("5e5236"), H("8c7c52"), H("b8a674"), H("dccb98") };
    /// <summary>Polished chrome for Electric Age robots and fins.</summary>
    public static readonly Color32[] Chrome = { H("3a4656"), H("7a8ca0"), H("c2d2e0"), H("f2f8ff") };
    /// <summary>Neon ray-gun green.</summary>
    public static readonly Color32[] Ray = { H("0a5c2a"), H("22b04a"), H("7cff7c"), H("e0ffe0") };
    public static readonly Color32[] Flower = { H("d84a7a"), H("ffd84a"), H("f6f2ff"), H("8a6cff") };

    /// <summary>A rarity's trim colour on worn gear.</summary>
    public static Color32 RarityTrim(Rarity r) => r switch
    {
        Rarity.Uncommon => H("6ee06a"),
        Rarity.Rare => H("5aa8ff"),
        Rarity.Epic => H("c46bff"),
        Rarity.Legendary => H("ffb43a"),
        _ => default,
    };

    public static Color32[] TeamRamp(int team) => team == 1 ? Red : Blue;

    public static Color32[] Named(string name) => name switch
    {
        "Blue" => Blue, "Red" => Red, "Purple" => Purple, "Green" => Green, "Gold" => Gold, "Bronze" => Bronze,
        "Steel" => Steel, "Leather" => Leather, "Cloth" => Cloth, "Fur" => Fur, "Linen" => Linen, "Straw" => Straw,
        "Wolf" => Wolf, "Wood" => Wood, "Iron" => Iron, "Bone" => Bone, "Troll" => Troll, "Skin" => Skin, "Coal" => Coal, "Rock" => Rock, "Rune" => Rune,
        "Brass" => Brass, "Aether" => Aether, "Olive" => Olive, "Khaki" => Khaki, "Chrome" => Chrome, "Ray" => Ray,
        _ => Blue,
    };
}
