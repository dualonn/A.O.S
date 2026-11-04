using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One world's people (saved on WorldState): how far the wants of its classes have come in
/// (old saves and new worlds start at nothing and ease in), literacy, how comfortable each home
/// is, and how well each good has been supplied lately. See People.cs.
/// </summary>
[Serializable]
public class PeopleWorldState
{
    [Tooltip("False until the census first runs on this world (old saves): then wants ease in from nothing.")]
    public bool started;
    [Tooltip("0 to 1: how far needs, luxuries and their effects have eased in (full after about 15 minutes).")]
    public float ramp;
    [Tooltip("0 to 1: share of the people who are schooled (moves slowly towards what the schools can teach).")]
    public float literacy;
    [Tooltip("Classes that have lived here (bit per class), so a new class is announced once.")]
    public int seenMask;
    [Tooltip("Play time of the last census here.")]
    public float lastAt;
    public bool introduced;

    [Tooltip("How comfortable each home is (0 plain .. 3 grand), by building instance id.")]
    public List<PeopleHouse> houses = new List<PeopleHouse>();
    [Tooltip("Each good the people want: when it became available here, and how well it's been supplied lately.")]
    public List<PeopleGood> goods = new List<PeopleGood>();

    // Worked out at each census (once a second); not saved.
    [NonSerialized] public float[] classPop = new float[PeopleCatalog.ClassCount];
    [NonSerialized] public float[] needSat = new float[PeopleCatalog.ClassCount];
    [NonSerialized] public float[] luxSat = new float[PeopleCatalog.ClassCount];
    [NonSerialized] public int[] houseCount = new int[PeopleCatalog.ClassCount];
    [NonSerialized] public int[] grandCount = new int[PeopleCatalog.ClassCount];
    [NonSerialized] public float[] skilled = new float[PeopleCatalog.ClassCount];
    [NonSerialized] public float seats, avgLevel, quality, comfortAvg, taxFactor = 1f, growth = 1f;
    [NonSerialized] public int bonusHousing, topLevel;
    [NonSerialized] public string topSchool;
    [NonSerialized] public List<(float amount, string text)> reasons = new List<(float, string)>();
    [NonSerialized] public Dictionary<int, PeopleHouse> houseIndex;
}

[Serializable]
public class PeopleHouse
{
    public int id;
    [Tooltip("0 plain, 1 comfortable, 2 prosperous, 3 grand.")]
    public int level;
}

[Serializable]
public class PeopleGood
{
    public string id;
    [Tooltip("Play time when it became available here (its want eases in from then).")]
    public float since;
    [Tooltip("0 to 1: how well it's been supplied lately.")]
    public float sat;
}

/// <summary>A class of people: who lives in the homes of one age, and what they want.</summary>
public class PeopleClass
{
    public int tier;
    public string name, desc;
    public string[] needs, lux;
}

/// <summary>A good the people want, what makes it, and how much a person uses.</summary>
public struct PeopleGoodSpec
{
    public string id, name;
    public int age;
    [Tooltip("A good of an existing chain (gold, mana, chips...): people only buy what's above a reserve, and use less.")]
    public bool shared;
}

/// <summary>A workshop making one of the new goods.</summary>
public struct PeopleWorkshopSpec
{
    public string id, name, good, age, tech, desc;
    public int ageOrder, w, h, tall, workers;
    public float rate;
    public (string id, float rate)[] inputs;
}

/// <summary>A school, one per age band: they rank up into each other.</summary>
public struct PeopleSchoolSpec
{
    public string id, name, age, tech, desc, specialists;
    public int ageOrder, level, seats, size, tall, workers;
}

/// <summary>The classes, goods, workshops and schools (shared by the game and its setup).</summary>
public static class PeopleCatalog
{
    public const int ClassCount = 17;

    public static readonly PeopleClass[] Classes =
    {
        new PeopleClass { tier = 0, name = "Tribesfolk", desc = "Hunters and gatherers round the fire. They ask little: food, a roof, and later a good pot.", needs = new string[0], lux = new[] { "Pottery" } },
        new PeopleClass { tier = 1, name = "Villagers", desc = "Farmers in mud-brick houses. They need pots to store grain, and like a woven cloak.", needs = new[] { "Pottery" }, lux = new[] { "Cloth" } },
        new PeopleClass { tier = 2, name = "Peasants", desc = "Iron-tool farmers and smiths' families. Pots and cloth are a must; ale is a treat.", needs = new[] { "Pottery", "Cloth" }, lux = new[] { "Ale" } },
        new PeopleClass { tier = 3, name = "Citizens", desc = "City folk with a vote and a bath-house. They want good clothes and wine, and like gold trinkets.", needs = new[] { "Cloth", "Ale" }, lux = new[] { "Pottery", "Gold" } },
        new PeopleClass { tier = 4, name = "Burghers", desc = "Guild masters behind town walls. Fine cloth and ale, and books and jewellery to show off.", needs = new[] { "Cloth", "Ale" }, lux = new[] { "Books", "Gold" } },
        new PeopleClass { tier = 5, name = "Merchants", desc = "Bankers and traders of the new age. They read, they drink, and they crave spices and coffee from afar.", needs = new[] { "Ale", "Books" }, lux = new[] { "Coffee", "Gold" } },
        new PeopleClass { tier = 6, name = "Gentry", desc = "Polite society in its townhouses: coffee houses, libraries, silk and silver.", needs = new[] { "Books", "Coffee" }, lux = new[] { "Cloth", "Gold" } },
        new PeopleClass { tier = 7, name = "Workers", desc = "Factory hands in their terraces. Cheap clothes and a pint at the end of a shift.", needs = new[] { "Cloth", "Ale" }, lux = new[] { "Coffee" } },
        new PeopleClass { tier = 8, name = "Middle Class", desc = "Clerks and shopkeepers in flats: ready-made clothes and coffee, a shelf of books.", needs = new[] { "Cloth", "Coffee" }, lux = new[] { "Books", "Ale" } },
        new PeopleClass { tier = 9, name = "Engineers", desc = "Folk of the chrome age. Books and coffee, and a radio humming in every home.", needs = new[] { "Books", "Coffee" }, lux = new[] { "Electronics" } },
        new PeopleClass { tier = 10, name = "Technicians", desc = "Screen-lit lives in smart towers: they need their gadgets, and like the newest chips.", needs = new[] { "Electronics", "Coffee" }, lux = new[] { "Microchips" } },
        new PeopleClass { tier = 11, name = "Mages", desc = "The schooled of the arcane age. Spellbooks and mana to live by; mana crystals for luxury.", needs = new[] { "Books", "Mana" }, lux = new[] { "Manastone" } },
        new PeopleClass { tier = 12, name = "Astral Seers", desc = "Star-gazing spellwrights in their arcologies: mana and devices, with crystals and old books.", needs = new[] { "Mana", "Electronics" }, lux = new[] { "Manastone", "Books" } },
        new PeopleClass { tier = 13, name = "Augmented", desc = "People with sun-forged implants. They need devices and chips, and love holo-media.", needs = new[] { "Electronics", "Microchips" }, lux = new[] { "HoloMedia" } },
        new PeopleClass { tier = 14, name = "Synthetics", desc = "Minds part flesh, part machine. Chips and holo-media keep them whole; devices amuse them.", needs = new[] { "Microchips", "HoloMedia" }, lux = new[] { "Electronics" } },
        new PeopleClass { tier = 15, name = "Starborn", desc = "Born between the stars. Holo-media and real coffee from home, and stardust for luxury.", needs = new[] { "HoloMedia", "Coffee" }, lux = new[] { "Stardust" } },
        new PeopleClass { tier = 16, name = "Ascended", desc = "Beings of light at the end of the ages. They keep the old books and holo-dreams, and drink quintessence.", needs = new[] { "HoloMedia", "Books" }, lux = new[] { "Quintessence" } },
    };

    public static readonly PeopleGoodSpec[] Goods =
    {
        new PeopleGoodSpec { id = "Pottery", name = "Pottery", age = 1 },
        new PeopleGoodSpec { id = "Cloth", name = "Cloth", age = 1 },
        new PeopleGoodSpec { id = "Ale", name = "Ale", age = 2 },
        new PeopleGoodSpec { id = "Gold", name = "Gold", age = 3, shared = true },
        new PeopleGoodSpec { id = "Books", name = "Books", age = 4 },
        new PeopleGoodSpec { id = "Coffee", name = "Coffee", age = 5 },
        new PeopleGoodSpec { id = "Electronics", name = "Electronics", age = 9 },
        new PeopleGoodSpec { id = "Microchips", name = "Microchips", age = 10, shared = true },
        new PeopleGoodSpec { id = "Mana", name = "Mana", age = 11, shared = true },
        new PeopleGoodSpec { id = "Manastone", name = "Manastone", age = 11, shared = true },
        new PeopleGoodSpec { id = "HoloMedia", name = "Holo-media", age = 13 },
        new PeopleGoodSpec { id = "Stardust", name = "Stardust", age = 15, shared = true },
        new PeopleGoodSpec { id = "Quintessence", name = "Quintessence", age = 15, shared = true },
    };

    public static readonly PeopleWorkshopSpec[] Workshops =
    {
        new PeopleWorkshopSpec { id = "PplPotter", name = "Potter's Kiln", good = "Pottery", age = "BronzeAge", ageOrder = 1, w = 2, h = 2, tall = 1, workers = 2, rate = 0.15f, tech = "Pottery",
            inputs = new[] { ("Stone", 0.05f), ("Wood", 0.04f) },
            desc = "A potter's wheel and a domed kiln: jars, bowls and lamps from clay. Villagers need pots; later folk still use them." },
        new PeopleWorkshopSpec { id = "PplWeaver", name = "Weaver's Hut", good = "Cloth", age = "BronzeAge", ageOrder = 1, w = 2, h = 2, tall = 1, workers = 2, rate = 0.15f, tech = "Agriculture",
            inputs = new[] { ("Food", 0.04f) },
            desc = "Flax and wool spun and woven on an upright loom. Cloth clothes nearly every class." },
        new PeopleWorkshopSpec { id = "PplBrewery", name = "Brewery", good = "Ale", age = "IronAge", ageOrder = 2, w = 2, h = 2, tall = 1, workers = 2, rate = 0.2f,
            inputs = new[] { ("Food", 0.1f), ("Wood", 0.02f) },
            desc = "Barley malted, mashed and brewed (and grapes trodden for wine). Ale cheers peasants, citizens and workers alike." },
        new PeopleWorkshopSpec { id = "PplScriptorium", name = "Scriptorium", good = "Books", age = "MedievalAge", ageOrder = 4, w = 2, h = 2, tall = 1, workers = 3, rate = 0.12f, tech = "Monasteries",
            inputs = new[] { ("Wood", 0.05f) },
            desc = "Copyists at their desks, and later a printing press: books for burghers, gentry, mages and the Ascended." },
        new PeopleWorkshopSpec { id = "PplPlantation", name = "Coffee Plantation", good = "Coffee", age = "RenaissanceAge", ageOrder = 5, w = 3, h = 2, tall = 0, workers = 3, rate = 0.25f,
            inputs = new (string, float)[0],
            desc = "Rows of coffee bushes, sugar cane and spice, and a drying floor. Merchants want it; later ages can't do without it." },
        new PeopleWorkshopSpec { id = "PplRadioWorks", name = "Radio Works", good = "Electronics", age = "ElectricAge", ageOrder = 9, w = 3, h = 2, tall = 1, workers = 4, rate = 0.3f, tech = "Television",
            inputs = new[] { ("Gears", 0.03f), ("PowerCells", 0.02f) },
            desc = "A chrome factory turning out radios, gramophones and, later, every household device. Needed from the Electric Age on." },
        new PeopleWorkshopSpec { id = "PplHoloStudio", name = "Holo Studio", good = "HoloMedia", age = "StellarAge", ageOrder = 13, w = 3, h = 2, tall = 1, workers = 4, rate = 0.4f,
            inputs = new[] { ("Microchips", 0.02f), ("Sunfire", 0.01f) },
            desc = "Dream-weavers recording whole worlds of light. Holo-media keeps the Augmented, Synthetics, Starborn and Ascended content." },
    };

    public static readonly PeopleSchoolSpec[] Schools =
    {
        new PeopleSchoolSpec { id = "PplTabletHouse", name = "House of Tablets", age = "BronzeAge", ageOrder = 1, level = 1, seats = 15, size = 2, tall = 1, workers = 2, tech = "Writing", specialists = "Scribes",
            desc = "Scribes teach the young their signs on clay tablets. Schools people: research goes faster and schooled people work better." },
        new PeopleSchoolSpec { id = "PplAcademy", name = "Academy", age = "ClassicalAge", ageOrder = 3, level = 2, seats = 30, size = 2, tall = 1, workers = 3, tech = "Philosophy", specialists = "Scholars",
            desc = "A colonnaded grove where philosophers argue with their pupils. Schools people: research and skilled work." },
        new PeopleSchoolSpec { id = "PplUniversity", name = "University", age = "MedievalAge", ageOrder = 4, level = 3, seats = 50, size = 3, tall = 1, workers = 4, specialists = "Masters",
            desc = "Cloisters, a library and gowned masters teaching law, medicine and the stars. Schools people: research and skilled work." },
        new PeopleSchoolSpec { id = "PplRoyalAcademy", name = "Royal Academy", age = "EnlightenmentAge", ageOrder = 6, level = 4, seats = 75, size = 3, tall = 1, workers = 5, tech = "Encyclopedie", specialists = "Savants",
            desc = "A domed hall of natural philosophers, its observatory and great library. Schools people: research and skilled work." },
        new PeopleSchoolSpec { id = "PplPolytechnic", name = "Polytechnic", age = "IndustrialAge", ageOrder = 7, level = 5, seats = 100, size = 3, tall = 1, workers = 6, specialists = "Engineers",
            desc = "Red-brick lecture halls and workshops where engines are taught. Schools people: research and skilled work." },
        new PeopleSchoolSpec { id = "PplRadiumInstitute", name = "Radium Institute", age = "ElectricAge", ageOrder = 9, level = 6, seats = 130, size = 3, tall = 2, workers = 6, specialists = "Scientists",
            desc = "A gleaming tower of labs crowned with a Tesla spire. Schools people: research and skilled work." },
        new PeopleSchoolSpec { id = "PplResearchInstitute", name = "Research Institute", age = "InformationAge", ageOrder = 10, level = 7, seats = 160, size = 3, tall = 1, workers = 6, tech = "Internet", specialists = "Researchers",
            desc = "Glass labs, a data centre and a campus lawn. Schools people: research and skilled work." },
        new PeopleSchoolSpec { id = "PplArcaneCollege", name = "Arcane College", age = "ArcaneAge", ageOrder = 11, level = 8, seats = 200, size = 3, tall = 2, workers = 6, tech = "Arcanism", specialists = "Archmages",
            desc = "Spired towers where apprentices learn to weave mana into code. Schools people: research and skilled work." },
        new PeopleSchoolSpec { id = "PplNeuralAcademy", name = "Neural Academy", age = "StellarAge", ageOrder = 13, level = 9, seats = 240, size = 3, tall = 1, workers = 6, specialists = "Neural Savants",
            desc = "Students learn in linked dreams under a dome of sunlight. Schools people: research and skilled work." },
        new PeopleSchoolSpec { id = "PplOmniversity", name = "Omniversity", age = "GalacticAge", ageOrder = 15, level = 10, seats = 300, size = 3, tall = 2, workers = 6, tech = "StarMinds", specialists = "Ascended Minds",
            desc = "A floating ring of halls where every world's knowledge is taught. Schools people: research and skilled work." },
    };

    /// <summary>The class living in homes of an age (one per age, Stone Age to Universal Age).</summary>
    public static int TierOfAge(int ageOrder) => Mathf.Clamp(ageOrder, 0, ClassCount - 1);

    public static int GoodIndex(string id)
    {
        for (int i = 0; i < Goods.Length; i++) if (Goods[i].id == id) return i;
        return -1;
    }

    public static bool IsSchool(string id, out PeopleSchoolSpec spec)
    {
        foreach (var s in Schools) if (s.id == id) { spec = s; return true; }
        spec = default;
        return false;
    }

    public static bool IsWorkshop(string id, out PeopleWorkshopSpec spec)
    {
        foreach (var s in Workshops) if (s.id == id) { spec = s; return true; }
        spec = default;
        return false;
    }

    /// <summary>The school level expected by an age (the newest school at or before it; 0 before schools).</summary>
    public static int ExpectedLevel(int ageOrder)
    {
        int level = 0;
        foreach (var s in Schools) if (s.ageOrder <= ageOrder) level = Mathf.Max(level, s.level);
        return level;
    }

    /// <summary>What schooled people are called, by the best school they've been to.</summary>
    public static string SpecialistName(int level)
    {
        string name = "Scribes";
        foreach (var s in Schools) if (s.level <= level) name = s.specialists;
        return name;
    }

    /// <summary>The comfort names for homes.</summary>
    public static readonly string[] ComfortNames = { "Plain", "Comfortable", "Prosperous", "Grand" };
}
