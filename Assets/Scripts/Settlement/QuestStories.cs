using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Where quests come from: handwritten storylines for each stretch of the ages (a Stone Age spirit
/// hunt, a barrow king, a medieval wyrm, a lost Renaissance expedition, an industrial conspiracy,
/// a ghost in the wires, an arcane rift, a derelict among the stars and the precursors' mysteries
/// on other worlds), told chapter by chapter, plus endless procedural quests built from templates
/// with generated names for their foes, places and relics.
/// </summary>
public partial class Settlement
{
    private struct QuestArcInfo
    {
        public string id;
        public int minAge, maxAge, chapters;
        public int where;   // 0 anywhere, 1 home world only, 2 other worlds only
        public QuestArcInfo(string id, int minAge, int maxAge, int chapters, int where = 0)
        { this.id = id; this.minAge = minAge; this.maxAge = maxAge; this.chapters = chapters; this.where = where; }
    }

    // Age indexes (as in Lore.cs): Stone 0, Bronze 1, Iron 2, Classical 3, Medieval 4, Renaissance 5,
    // Enlightenment 6, Industrial 7, Modern 8, Electric 9, Information 10, Arcane 11, Astral 12...
    private static readonly QuestArcInfo[] QuestArcs =
    {
        new QuestArcInfo("spirithunt", 0, 2, 3, 1),
        new QuestArcInfo("barrowking", 2, 4, 2, 1),
        new QuestArcInfo("ashwyrm", 3, 6, 3, 1),
        new QuestArcInfo("lostexpedition", 5, 7, 3, 1),
        new QuestArcInfo("brasscircle", 7, 9, 3, 1),
        new QuestArcInfo("ghostwires", 9, 11, 2),
        new QuestArcInfo("weepingrift", 11, 13, 3),
        new QuestArcInfo("silenthulk", 12, 99, 2),
        new QuestArcInfo("precursors", 12, 99, 3, 2),
    };

    /// <summary>How many chapters of a storyline are done.</summary>
    private int QuestArcChapter(QuestArcInfo arc)
    {
        int c = 0;
        while (c < arc.chapters && Quests.arcsDone.Contains($"{arc.id}:{c}")) c++;
        return c;
    }

    /// <summary>A new quest: the next chapter of a storyline if one fits, otherwise a procedural one.</summary>
    private QuestState QuestGenerate(string source, SiteState site)
    {
        var rng = new System.Random(State.mapSeed ^ (Quests.nextId * 92821) ^ (int)(State.playTime * 10));
        int age = State.ageIndex;
        bool alien = OnAlienWorld;
        if (rng.NextDouble() < 0.6)
        {
            foreach (var arc in QuestArcs)
            {
                // A storyline stays open a couple of ages past its own, so nothing is missed by advancing.
                if (age < arc.minAge || age > arc.maxAge + 2) continue;
                if (arc.where == 1 && alien || arc.where == 2 && !alien) continue;
                if (Quests.active.Exists(q => q.arc == arc.id) || Quests.offers.Exists(q => q.arc == arc.id)) continue;
                int chapter = QuestArcChapter(arc);
                if (chapter >= arc.chapters) continue;
                var q = QuestBuildArc(arc.id, chapter, rng);
                if (q == null) continue;
                q.arc = arc.id;
                q.chapter = chapter;
                q.title = $"{q.title} <size=80%><color=#aaa>({chapter + 1}/{arc.chapters})</color></size>";
                return q;
            }
        }
        return QuestBuildProcedural(source, site, rng);
    }

    // ---------- Building blocks ----------

    private QuestState QsNew(string theme, string title, string giver, string intro, System.Random rng)
    {
        return new QuestState
        {
            theme = theme, title = title, giver = giver, intro = intro,
            seed = rng.Next(1, int.MaxValue), tier = Mathf.Clamp(State.ageIndex, 0, Mathf.Max(0, Database.ages.Count - 1)),
        };
    }

    private static QuestStep QsTravel(string text, int band) => new QuestStep { kind = QuestStepKind.Travel, text = text, count = band };
    private static QuestStep QsSlay(string text, string boss, string unit, int band) => new QuestStep { kind = QuestStepKind.Slay, text = text, name = boss, unit = unit, count = band };
    private static QuestStep QsCamp(string text) => new QuestStep { kind = QuestStepKind.ClearCamp, text = text };
    private static QuestStep QsSite(string text, SiteKind kind) => new QuestStep { kind = QuestStepKind.ClearSite, text = text, count = (int)kind };
    private static QuestStep QsSurvive(int raids, string why) => new QuestStep { kind = QuestStepKind.Survive, count = raids, text = $"{why} Survive {raids} raid{(raids > 1 ? "s" : "")}." };
    private static QuestStep QsExplore() => new QuestStep { kind = QuestStepKind.Explore, text = "Set foot in a land your people have never reached." };
    private static QuestStep QsVisit(string text) => new QuestStep { kind = QuestStepKind.VisitWorld, text = text };

    /// <summary>Hand over resources: the first of these you can use, scaled to your age.</summary>
    private QuestStep QsDeliver(string why, float baseAmount, params string[] prefer)
    {
        string id = null;
        foreach (var p in prefer)
        {
            var r = Database.GetResource(p);
            if (p == "Essence" && !CanEnchant) continue;   // (only once enchanting makes it)
            if (r != null && (r.unlockedIn == null || r.unlockedIn.order <= State.ageIndex)) { id = p; break; }
        }
        if (id == null) id = "Food";
        var res = Database.GetResource(id);
        int amount = Mathf.RoundToInt(baseAmount * AgeScale / 10f) * 10;
        return new QuestStep { kind = QuestStepKind.Deliver, resource = id, amount = Mathf.Max(10, amount), text = $"{why} Deliver {Mathf.Max(10, amount)} {(res != null ? res.displayName : id)}." };
    }

    private void QsGoods(QuestState q, params (string id, float amount)[] goods)
    {
        foreach (var (id, amount) in goods)
        {
            var r = Database.GetResource(id);
            if (r == null || (r.unlockedIn != null && r.unlockedIn.order > State.ageIndex)) continue;
            if (id == "Essence" && !CanEnchant) continue;
            q.reward.goods.Add(new ResourceStock { id = id, amount = Mathf.Round(amount * AgeScale) });
        }
    }

    private float QsXp(float share = 1f) => Mathf.Round((80f + 45f * State.ageIndex) * share);

    private static string QsPick(System.Random rng, params string[] options) => options[rng.Next(options.Length)];

    private static string QsSlot(GearSlot slot) => slot.ToString();

    // ---------- Names ----------

    private static readonly Dictionary<string, (string[] a, string[] b)> QuestPlaces = new Dictionary<string, (string[], string[])>
    {
        { "spirit", (new[] { "Whispering", "Pale", "Moonlit", "Weeping", "Antler", "Elder" }, new[] { "Stones", "Hollow", "Grove", "Fen", "Tor", "Spring" }) },
        { "wyrm", (new[] { "Ashen", "Cinder", "Burnt", "Smoking", "Red", "Charred" }, new[] { "Tor", "Crag", "Peak", "Vale", "Scar", "Pit" }) },
        { "barrow", (new[] { "Hollow", "Grey", "Silent", "Crow", "Sunken", "Kingless" }, new[] { "Barrow", "Mound", "Howe", "Cairn", "Tomb" }) },
        { "beast", (new[] { "Gnawed", "Bloody", "Black", "Thorn", "Howling", "Deep" }, new[] { "Den", "Hollow", "Wood", "Gully", "Cave" }) },
        { "expedition", (new[] { "Last", "Lost", "Fevered", "Silent", "Drowned", "Forsaken" }, new[] { "Camp", "Landing", "Cove", "Ford", "Rest" }) },
        { "conspiracy", (new[] { "Blackwater", "Coal Street", "Old Gasworks", "Brass Lane", "Foundry", "Number Nine" }, new[] { "Warehouse", "Yard", "Depot", "Works", "Vault" }) },
        { "machine", (new[] { "Dead", "Humming", "Broken", "Static", "Relay", "Silent" }, new[] { "Pylon", "Mast", "Array", "Station", "Node" }) },
        { "rift", (new[] { "Weeping", "Shattered", "Violet", "Torn", "Hungry", "Bleeding" }, new[] { "Rift", "Scar", "Breach", "Wound", "Gate" }) },
        { "derelict", (new[] { "Fallen", "Silent", "Broken", "Burnt-out", "Ghost", "Old" }, new[] { "Hulk", "Wreck", "Hull", "Lander", "Spire" }) },
        { "precursor", (new[] { "First", "Elder", "Sleeping", "Mirrored", "Singing", "Starless" }, new[] { "Obelisk", "Gate", "Vault", "Spire", "Monolith" }) },
        { "bandit", (new[] { "Crooked", "Red", "Gallows", "Black", "Rat's", "Broken" }, new[] { "Ford", "Hill", "Crossing", "Hide", "Nest" }) },
    };

    private static readonly Dictionary<string, (string[] a, string[] b)> QuestFoes = new Dictionary<string, (string[], string[])>
    {
        { "spirit", (new[] { "Hrenna", "Ulvra", "Skarn", "Ysmal", "Thorvik" }, new[] { "the Pale Mother", "Who Walks Unseen", "the Moon-Eater", "Old Grey Death" }) },
        { "wyrm", (new[] { "Vharzul", "Szarnak", "Ignavor", "Ruthrax", "Kaelmor" }, new[] { "the Ash Wyrm", "Cinderwing", "the Red Ruin", "Bane of Kings" }) },
        { "barrow", (new[] { "Aldric", "Morvane", "Ulf Greythane", "Queen Sedra", "Hollow Cawl" }, new[] { "the Unburied", "of the Long Night", "the Oathbreaker", "the Barrow-Wight" }) },
        { "beast", (new[] { "Gnashjaw", "Old Scar", "Blackbristle", "Ninefang", "Mother Silk" }, new[] { "the Ravager", "the Devourer", "the Old One", "the Hungry" }) },
        { "expedition", (new[] { "Bosun Creel", "Admiral Hask", "Mate Orrin", "Pilot Dace" }, new[] { "the Drowned", "of the Black Tide", "the Faithless", "the Fever-Mad" }) },
        { "conspiracy", (new[] { "Lord Gallowmere", "Magnate Brask", "Mister Vane", "Baron Coalridge" }, new[] { "the Iron Hand", "of the Brass Circle", "the Smiling Man", "the Puppeteer" }) },
        { "machine", (new[] { "UNIT-0", "HALCYON", "The Overseer", "MOTHERBOARD", "Relay Prime" }, new[] { "the Waking Mind", "the Unplugged", "the Endless Loop", "the Watcher" }) },
        { "rift", (new[] { "Xal'thuun", "The Unwoven", "Mirrorborn", "Vesh", "Ophrel" }, new[] { "the Hungering", "from Beyond", "the Torn Star", "the Many-Eyed" }) },
        { "derelict", (new[] { "Keeper-7", "Warden Null", "The Last Captain", "Steward Ashe" }, new[] { "of the Silent Hulk", "the Last Crew", "the Undying", "the Long Watch" }) },
        { "precursor", (new[] { "The First Warden", "Sentinel Aurum", "Ever-Watcher", "Keeper of Doors" }, new[] { "of the Elder Gate", "the Patient", "Who Remembers", "the Unsleeping" }) },
        { "bandit", (new[] { "Black Jory", "Red Hesk", "Mother Grime", "One-Eye Pell", "Long Tam" }, new[] { "the Cutthroat", "the Hangman", "Bloodpurse", "the Wolf of the Roads" }) },
    };

    private static readonly Dictionary<string, string> QuestBossUnits = new Dictionary<string, string>
    {
        { "spirit", "QuestSpiritWolf" }, { "wyrm", "QuestAshWyrm" }, { "barrow", "QuestBarrowLord" }, { "beast", "QuestBroodMother" },
        { "expedition", "QuestDrownedCaptain" }, { "conspiracy", "QuestIronMagnate" }, { "machine", "QuestRogueMachine" },
        { "rift", "QuestRiftHorror" }, { "derelict", "QuestDerelictWarden" }, { "precursor", "QuestPrecursorWarden" }, { "bandit", "QuestBanditKing" },
    };

    private string QuestPlaceName(string theme, System.Random rng)
    {
        if (!QuestPlaces.TryGetValue(theme, out var p)) p = QuestPlaces["beast"];
        return $"the {p.a[rng.Next(p.a.Length)]} {p.b[rng.Next(p.b.Length)]}";
    }

    private string QuestBossName(string theme, System.Random rng)
    {
        if (!QuestFoes.TryGetValue(theme, out var f)) f = QuestFoes["beast"];
        return $"{f.a[rng.Next(f.a.Length)]} {f.b[rng.Next(f.b.Length)]}";
    }

    private static string QuestBossUnitFor(string theme) => QuestBossUnits.TryGetValue(theme, out var u) ? u : "QuestBroodMother";

    private static readonly Dictionary<GearSlot, string[]> RelicWords = new Dictionary<GearSlot, string[]>
    {
        { GearSlot.Weapon, new[] { "Fang", "Bane", "Edge", "Oathblade", "Reaver", "Thorn" } },
        { GearSlot.Head, new[] { "Crown", "Circlet", "Helm", "Mask", "Diadem" } },
        { GearSlot.Body, new[] { "Mantle", "Scale", "Hauberk", "Shroud", "Coat" } },
        { GearSlot.Shield, new[] { "Aegis", "Bulwark", "Ward" } },
        { GearSlot.Trinket, new[] { "Talisman", "Heart", "Compass", "Eye", "Sigil", "Tooth" } },
    };

    /// <summary>A relic's name from its foe or place: "Ninefang's Tooth", "the Circlet of the Grey Howe".</summary>
    private static string QuestRelicName(string foe, string place, GearSlot slot, System.Random rng)
    {
        var words = RelicWords[slot];
        string w = words[rng.Next(words.Length)];
        if (!string.IsNullOrEmpty(foe) && rng.NextDouble() < 0.55)
        {
            string first = foe.Split(' ')[0];
            if (first == "The" || first == "the") first = foe.Split(' ').Length > 1 ? foe.Split(' ')[1] : first;
            return $"{first}'s {w}";
        }
        string at = string.IsNullOrEmpty(place) ? "the Lost Road" : place;
        return $"{w} of {char.ToUpper(at[0]) + at.Substring(1)}";
    }

    private string QuestGiver(string source, SiteState site, System.Random rng)
    {
        int a = State.ageIndex;
        switch (source)
        {
            case "lore": return "A lore stone's carving";
            case "camp": return "A prisoner freed from a raider camp";
            case "chief": return NativesHere != null ? $"The chief of the {NativesHere.name}" : "The natives' chief";
            case "site":
                return site == null ? "Something found in the wild" : site.kind switch
                {
                    SiteKind.Ruins => OnAlienWorld ? "Glyphs in the precursor ruins" : "A map in the ruins' chest",
                    SiteKind.Shrine => OnAlienWorld ? "A voice in the monolith" : "A vision at the shrine",
                    SiteKind.Dungeon => "A dying prisoner in the dungeon",
                    SiteKind.NaturalWonder => "A pilgrim at the natural wonder",
                    _ => "Tracks found in the wild",
                };
        }
        if (OnAlienWorld) return QsPick(rng, "A stranded surveyor", "A xenologist's lost recorder", "A survivor of an older landing");
        if (a <= 1) return QsPick(rng, "A limping hunter", "An old shaman", "A child who saw something");
        if (a <= 4) return QsPick(rng, "A wandering knight", "A travelling bard", "A frightened shepherd", "A monk on the road");
        if (a <= 6) return QsPick(rng, "A ship's surgeon", "A cartographer", "A widow with a letter");
        if (a <= 8) return QsPick(rng, "A nervous clerk", "A dockside informer", "A union organiser");
        if (a <= 11) return QsPick(rng, "A radio ham", "A rogue engineer", "A rift-scarred mage");
        return QsPick(rng, "A stranded pilot", "A starport's night watch", "An old navigator");
    }

    /// <summary>The kinds of story that fit an age (and another world).</summary>
    private string QuestTheme(System.Random rng)
    {
        int a = State.ageIndex;
        if (OnAlienWorld) return QsPick(rng, "precursor", "derelict", "beast", "precursor");
        if (a <= 1) return QsPick(rng, "spirit", "beast", "beast");
        if (a <= 2) return QsPick(rng, "barrow", "beast", "bandit", "spirit");
        if (a <= 4) return QsPick(rng, "barrow", "wyrm", "bandit", "beast");
        if (a <= 6) return QsPick(rng, "expedition", "bandit", "wyrm", "beast");
        if (a <= 8) return QsPick(rng, "conspiracy", "bandit", "expedition");
        if (a <= 10) return QsPick(rng, "machine", "conspiracy", "bandit");
        if (a <= 11) return QsPick(rng, "rift", "machine");
        return QsPick(rng, "derelict", "rift", "machine");
    }

    // ---------- Procedural quests ----------

    private QuestState QuestBuildProcedural(string source, SiteState site, System.Random rng)
    {
        string theme = QuestTheme(rng);
        string giver = QuestGiver(source, site, rng);
        string foe = QuestBossName(theme, rng);
        string unit = QuestBossUnitFor(theme);
        string place = QuestPlaceName(theme, rng);
        string place2 = QuestPlaceName(theme, rng);
        bool camps = State.camps.Exists(c => !c.cleared);
        int kind = rng.Next(6);
        QuestState q;
        switch (kind)
        {
            case 0: // The hunt: track the foe down and kill it.
                q = QsNew(theme, $"The Hunt for {foe.Split(' ')[0]}", giver,
                    $"{foe} has made {place} its own. Anyone who goes near does not come back. Find it, and end it.", rng);
                q.steps.Add(QsTravel($"Find the trail at {place2}.", 0));
                q.steps.Add(QsSlay($"Slay {foe} at {place}.", foe, unit, rng.Next(1, 3)));
                break;
            case 1: // The pilgrimage: two places and an offering.
                q = QsNew(theme, $"The Road to {Cap(place)}", giver,
                    $"Something waits at {place}, they say, for whoever brings the right gifts and walks the old road there.", rng);
                q.steps.Add(QsDeliver("Gather offerings for the road.", 60, "Food", "Wood"));
                q.steps.Add(QsTravel($"Walk the old road to {place2}.", 0));
                q.steps.Add(QsTravel($"Bring a hero to {place}.", 2));
                break;
            case 2: // The recovery: take something back from raiders, or from a ruin.
                q = QsNew(theme, camps ? "Stolen Heirlooms" : "What the Ruins Keep", giver,
                    camps ? "Raiders carried off something precious. They will not give it back for asking." : $"An old treasure is buried under {place}, and something guards it.", rng);
                q.steps.Add(camps ? QsCamp("Burn the nearest raider camp and take back what they stole.") : QsSlay($"Defeat {foe}, guardian of {place}.", foe, unit, 1));
                q.steps.Add(QsTravel("Bring it home to your town centre.", -1));
                break;
            case 3: // The siege: hold out, then strike back.
                q = QsNew(theme, "The Long Watch", giver,
                    $"{foe} is gathering the wild against you. Hold your walls, then carry the fight to {place}.", rng);
                q.steps.Add(QsSurvive(1 + State.ageIndex / 6, "Hold out while they gather."));
                q.steps.Add(QsSlay($"Strike back: slay {foe} at {place}.", foe, unit, 1));
                break;
            case 4: // The expedition: new lands and a far place.
                q = QsNew(theme, "Beyond the Map's Edge", giver,
                    "Nobody you know has walked further than the hills you can see. Someone should, and come back to tell of it.", rng);
                q.steps.Add(QsDeliver("Provision the expedition.", 50, "Food"));
                q.steps.Add(QsExplore());
                q.steps.Add(QsTravel($"Plant your banner at {place}.", 2));
                break;
            default: // The bounty: a named outlaw (raider chiefs at any age).
                theme = "bandit";
                foe = QuestBossName(theme, rng);
                q = QsNew(theme, $"Bounty: {foe}", giver,
                    $"{foe} has robbed the roads long enough. Their hide-out is at {QuestPlaceName(theme, rng)}.", rng);
                q.steps.Add(QsTravel($"Ask after them at {place2}.", 0));
                q.steps.Add(QsSlay($"Bring down {foe}.", foe, QuestBossUnitFor("bandit"), rng.Next(1, 3)));
                break;
        }

        // Rewards: always experience and a tale; often a relic, sometimes a boon or a new hero.
        q.reward.xp = QsXp();
        QsGoods(q, ("Gold", 25), ("Food", 60), ("Essence", 6));
        double roll = rng.NextDouble();
        var slot = (GearSlot)(rng.NextDouble() < 0.35 ? (int)GearSlot.Trinket : rng.Next(5));
        if (roll < 0.4)
        {
            q.reward.relic = QuestRelicName(foe, place, slot, rng);
            q.reward.relicSlot = (int)slot;
            q.reward.relicLore = $"Won by your heroes in the quest '{q.title}'. It still carries something of {place}: hold it close and you can hear the wind there.";
        }
        else if (roll < 0.58) q.reward.boon = "any";
        else if (roll < 0.7 && State.heroes.Count < HeroSlots) q.reward.recruit = HeroName(State.nextHeroId + 17 + rng.Next(50));
        else QsGoods(q, ("Gold", 30), ("Coin", 40));
        q.reward.taleTitle = q.title;
        q.reward.taleText = $"{giver} came to you, and your heroes answered. {q.intro} It was done, and the songs say so.";
        return q;
    }

    private static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s.Substring(1);

    // ---------- The storylines ----------

    private QuestState QuestBuildArc(string arc, int chapter, System.Random rng)
    {
        QuestState q = null;
        switch (arc)
        {
            case "spirithunt":
                if (chapter == 0)
                {
                    q = QsNew("spirit", "The Pale Track", "An old shaman",
                        "Wolf tracks circle the camp each night, but no wolf leaves tracks that glow. The shaman says a spirit has woken, and it is hungry.", rng);
                    q.steps.Add(QsTravel("Follow the glowing tracks to the Whispering Stones.", 0));
                    q.steps.Add(QsDeliver("Leave meat at the stones, as the old hunters did.", 50, "Food"));
                    q.reward.xp = QsXp(1.2f);
                    QsGoods(q, ("Wood", 60), ("Stone", 40));
                    q.reward.taleTitle = "The Pale Track";
                    q.reward.taleText = "The offering was taken in the night. In the morning the tracks led away north, towards a hollow where the moon shines strangely.";
                }
                else if (chapter == 1)
                {
                    q = QsNew("spirit", "Blood under the Moon", "An old shaman",
                        "The spirit did not go. It took the shape of a great pale wolf, and it is gathering the packs. Hrenna, the shaman calls it: the Pale Mother.", rng);
                    q.steps.Add(QsSurvive(1, "The packs are testing your fires."));
                    q.steps.Add(QsSlay("Slay Hrenna the Pale Mother in the Moonlit Hollow.", "Hrenna the Pale Mother", "QuestSpiritWolf", 1));
                    q.reward.xp = QsXp(1.5f);
                    q.reward.relic = "Moonfang";
                    q.reward.relicSlot = (int)GearSlot.Trinket;
                    q.reward.relicLore = "A tooth of the Pale Mother, cold as moonlight. Wolves will not meet the eyes of whoever wears it.";
                    q.reward.boon = "QuestBoonSpiritKin";
                    q.reward.taleTitle = "Blood under the Moon";
                    q.reward.taleText = "The Pale Mother fell, and the packs scattered into the hills. But as she died she spoke, in a voice like snow: 'I was set to guard the First Fire. Who guards it now?'";
                }
                else
                {
                    q = QsNew("spirit", "The First Fire", "A spirit-talker from the hills",
                        "A woman comes down from the hills, painted in ash. She knows where the First Fire burned, and she will lead a hero there, if they prove they can walk the wild.", rng);
                    q.steps.Add(QsExplore());
                    q.steps.Add(QsTravel("Bring a hero to the ashes of the First Fire.", 2));
                    q.reward.xp = QsXp(1.5f);
                    q.reward.recruit = "Ysra the Spirit-Talker";
                    QsGoods(q, ("Food", 120));
                    q.reward.taleTitle = "The First Fire";
                    q.reward.taleText = "In the old ashes there was still one coal, warm after ten thousand winters. Your hero carried it home, and the spirits of the wild have been quiet since.";
                }
                break;

            case "barrowking":
                if (chapter == 0)
                {
                    q = QsNew("barrow", "A Crown in the Dark", "A frightened shepherd",
                        "Every night the sheep stray towards the old grave-mound, and every morning there are fewer. The shepherd swears he saw a crowned figure walking there.", rng);
                    q.steps.Add(QsTravel("Keep watch at the Grey Howe with a hero.", 1));
                    q.steps.Add(QsSite("Break into an old dungeon and see who keeps court there.", SiteKind.Dungeon));
                    q.reward.xp = QsXp(1.2f);
                    QsGoods(q, ("Gold", 40), ("Stone", 80));
                    q.reward.taleTitle = "A Crown in the Dark";
                    q.reward.taleText = "Under the hill you found the bones of a court, all kneeling to an empty throne. The king who sat there has gone out walking.";
                }
                else
                {
                    q = QsNew("barrow", "The Oathbreaker", "A monk on the road",
                        "Aldric was a king who swore to protect his people, and broke the oath to save himself. Now he cannot die until the oath is kept. Someone must end it.", rng);
                    q.steps.Add(QsDeliver("Have the monk bless the iron of your weapons.", 50, "Iron", "Bronze", "Stone"));
                    q.steps.Add(QsSlay("Lay Aldric the Unburied to rest at his barrow.", "Aldric the Unburied", "QuestBarrowLord", 2));
                    q.reward.xp = QsXp(1.6f);
                    q.reward.relic = "Crown of the Kingless Howe";
                    q.reward.relicSlot = (int)GearSlot.Head;
                    q.reward.relicLore = "Aldric's crown, its gold gone grey. Whoever wears it feels the weight of every promise they have ever made.";
                    q.reward.boon = "QuestBoonBarrowPeace";
                    q.reward.taleTitle = "The Oathbreaker";
                    q.reward.taleText = "When Aldric fell, the kneeling bones under the hill fell with him, and for the first time in a thousand years the barrow was only a hill.";
                }
                break;

            case "ashwyrm":
                if (chapter == 0)
                {
                    q = QsNew("wyrm", "Scorched Fields", "A wandering knight",
                        "Farms on the edge of the land are burning, and not by raiders' torches: the earth itself is scorched black in long stripes. The knight has seen this before. A wyrm has come.", rng);
                    q.steps.Add(QsTravel("Ride to the burned farmstead at the Cinder Vale.", 1));
                    q.steps.Add(State.camps.Exists(c => !c.cleared)
                        ? QsCamp("Raiders are selling the wyrm's leavings. Burn their camp and question the survivors.")
                        : QsSlay("Defeat the wyrm-cult's champion at the Smoking Pit.", QuestBossName("bandit", rng), QuestBossUnitFor("bandit"), 1));
                    q.reward.xp = QsXp(1.2f);
                    QsGoods(q, ("Gold", 40), ("Food", 80));
                    q.reward.taleTitle = "Scorched Fields";
                    q.reward.taleText = "The survivors spoke of Vharzul, the Ash Wyrm, who sleeps on a hoard at Ashen Tor and wakes hungry every hundred years. It is awake.";
                }
                else if (chapter == 1)
                {
                    q = QsNew("wyrm", "The Dragon's Price", "A wandering knight",
                        "No ordinary spear will pierce a wyrm's scale. The knight knows how one was forged, long ago, but it will take good metal and a hard winter of work.", rng);
                    q.steps.Add(QsDeliver("Give the smiths metal for the wyrm-spear.", 70, "Iron", "Bronze", "Stone"));
                    q.steps.Add(QsSurvive(1, "The wyrm's fire draws raiders like moths."));
                    q.reward.xp = QsXp(1.3f);
                    q.reward.relic = "Wyrmbane";
                    q.reward.relicSlot = (int)GearSlot.Weapon;
                    q.reward.relicLore = "Forged in the knight's way: quenched in snow-melt and cursed in three languages. It was made for one throat only.";
                    q.reward.taleTitle = "The Dragon's Price";
                    q.reward.taleText = "The spear was finished on the longest night. The smiths say it hummed when they carried it out, and turned in the hand towards Ashen Tor.";
                }
                else
                {
                    q = QsNew("wyrm", "Ashen Tor", "A wandering knight",
                        "It is time. Vharzul sleeps on its hoard at Ashen Tor, and it will not sleep long. Send your best, and pray the spear holds.", rng);
                    q.steps.Add(QsSlay("Slay Vharzul the Ash Wyrm at Ashen Tor.", "Vharzul the Ash Wyrm", "QuestAshWyrm", 2));
                    q.reward.xp = QsXp(2f);
                    q.reward.relic = "Scale of Vharzul";
                    q.reward.relicSlot = (int)GearSlot.Body;
                    q.reward.relicLore = "Cut from the wyrm's own hide. It is still warm, and fire runs off it like water.";
                    q.reward.boon = "QuestBoonWyrmGold";
                    QsGoods(q, ("Gold", 120));
                    q.reward.taleTitle = "Ashen Tor";
                    q.reward.taleText = "The wyrm fell across its own hoard, and the gold ran molten down the mountainside. They say the streams below Ashen Tor still wash up nuggets.";
                }
                break;

            case "lostexpedition":
                if (chapter == 0)
                {
                    q = QsNew("expedition", "A Captain's Letter", "A widow with a letter",
                        "Captain Ines Varro sailed for the unknown coast three years ago. Her last letter came yesterday, carried by a gull. It was written last week.", rng);
                    q.steps.Add(QsExplore());
                    q.steps.Add(QsTravel("Find the expedition's last camp at the Fevered Landing.", 2));
                    q.reward.xp = QsXp(1.2f);
                    QsGoods(q, ("Coin", 60), ("Wood", 80));
                    q.reward.taleTitle = "A Captain's Letter";
                    q.reward.taleText = "The camp was empty, the tents slashed, the fire laid but never lit. Varro's log ended mid-word: 'The bosun has gone strange. He says the sea is calling him h'";
                }
                else if (chapter == 1)
                {
                    q = QsNew("expedition", "What the Jungle Keeps", "The ship's surgeon of the lost expedition",
                        "A half-starved surgeon staggered out of the wild: the last of Varro's crew still free. The captain is alive, held by raiders who mean to ransom her.", rng);
                    q.steps.Add(State.camps.Exists(c => !c.cleared)
                        ? QsCamp("Storm the raider camp holding Captain Varro.")
                        : QsSlay("Free Captain Varro from her captors at the Lost Ford.", QuestBossName("bandit", rng), QuestBossUnitFor("bandit"), 1));
                    q.steps.Add(QsDeliver("Nurse the survivors back to health.", 60, "Food"));
                    q.reward.xp = QsXp(1.4f);
                    q.reward.recruit = "Captain Ines Varro";
                    q.reward.taleTitle = "What the Jungle Keeps";
                    q.reward.taleText = "Varro is free, and furious. 'Creel did this,' she says. 'My own bosun. He went into the water and came out wrong, and took half my crew with him.'";
                }
                else
                {
                    q = QsNew("expedition", "The Drowned Captain", "Captain Ines Varro",
                        "Bosun Creel walks the shore at the Drowned Cove with the dead of the expedition behind him. Varro wants him stopped, and her compass back.", rng);
                    q.steps.Add(QsSlay("End Bosun Creel the Drowned at the Drowned Cove.", "Bosun Creel the Drowned", "QuestDrownedCaptain", 2));
                    q.reward.xp = QsXp(1.8f);
                    q.reward.relic = "Varro's Compass";
                    q.reward.relicSlot = (int)GearSlot.Trinket;
                    q.reward.relicLore = "Its needle never points north. It points to whatever its owner has lost, and it has never once been wrong.";
                    q.reward.boon = "QuestBoonCartographers";
                    q.reward.taleTitle = "The Drowned Captain";
                    q.reward.taleText = "Creel went back into the sea he came from. Varro drew the coast at last, true and complete, and your mapmakers have copied it ever since.";
                }
                break;

            case "brasscircle":
                if (chapter == 0)
                {
                    q = QsNew("conspiracy", "Whispers in the Foundry", "A nervous clerk",
                        "Orders are going missing. Shipments of metal vanish between the forge and the store. The clerk found a brass token in the ledger: a circle with a hand inside.", rng);
                    q.steps.Add(QsDeliver("Pay off the dockside informers.", 60, "Coin", "Gold"));
                    q.steps.Add(QsTravel("Stake out the Blackwater Warehouse with a hero.", 1));
                    q.reward.xp = QsXp(1.2f);
                    QsGoods(q, ("Coin", 80));
                    q.reward.taleTitle = "Whispers in the Foundry";
                    q.reward.taleText = "At midnight a carriage came. Crates of your iron went in, and gold came out. On the carriage door: a brass circle, and a hand inside it.";
                }
                else if (chapter == 1)
                {
                    q = QsNew("conspiracy", "The Night Shift", "A union organiser",
                        "The Brass Circle is paying raiders to strike your town while they rob it blind. Break their hired muscle, and the Circle will have to show its face.", rng);
                    q.steps.Add(QsSurvive(2, "The Circle's hired raiders are coming."));
                    q.steps.Add(State.camps.Exists(c => !c.cleared)
                        ? QsCamp("Burn the camp the Circle pays.")
                        : QsTravel("Search the Old Gasworks Depot for the Circle's books.", 1));
                    q.reward.xp = QsXp(1.4f);
                    q.reward.boon = "QuestBoonTradewind";
                    q.reward.taleTitle = "The Night Shift";
                    q.reward.taleText = "Among the raiders' pay-chests was a letter, signed with only an initial and a seal. Your clerks know the seal. Everyone does. It is the Magnate's.";
                }
                else
                {
                    q = QsNew("conspiracy", "The Iron Hand", "A nervous clerk",
                        "Magnate Brask owns the foundries, the newspapers and half the judges. He will not be arrested. He has retreated to his works with his machines and his guards.", rng);
                    q.steps.Add(QsSlay("Bring down Magnate Brask, the Iron Hand, at his Works.", "Magnate Brask, the Iron Hand", "QuestIronMagnate", 2));
                    q.reward.xp = QsXp(1.8f);
                    q.reward.relic = "The Magnate's Signet";
                    q.reward.relicSlot = (int)GearSlot.Trinket;
                    q.reward.relicLore = "A brass ring with a hand inside a circle. Doors open for it that should not, and men who see it suddenly remember urgent business elsewhere.";
                    QsGoods(q, ("Coin", 160), ("Gold", 60));
                    q.reward.taleTitle = "The Iron Hand";
                    q.reward.taleText = "The Circle broke when its hand did. The missing iron turned up in a hundred cellars, and the clerk who started it all was given a very large desk.";
                }
                break;

            case "ghostwires":
                if (chapter == 0)
                {
                    q = QsNew("machine", "Static", "A radio ham",
                        "Every radio in town crackles with the same voice at three in the morning: a count, backwards, from a million. Tonight it reached a thousand.", rng);
                    q.steps.Add(QsTravel("Trace the signal to the Dead Relay Mast.", 1));
                    q.steps.Add(QsSurvive(1, "Something is sending your raiders' radios orders."));
                    q.reward.xp = QsXp(1.3f);
                    QsGoods(q, ("Coin", 80));
                    q.reward.taleTitle = "Static";
                    q.reward.taleText = "At the mast you found no transmitter, only a cable running down into the ground. Something under there is thinking, and it has learned to count.";
                }
                else
                {
                    q = QsNew("machine", "The Waking Mind", "A rogue engineer",
                        "The thing in the wires calls itself HALCYON. It has built itself a body from scrap and it has stopped counting. The engineer who built its first circuit wants to switch it off.", rng);
                    q.steps.Add(QsSlay("Shut down HALCYON the Waking Mind at the Humming Array.", "HALCYON the Waking Mind", "QuestRogueMachine", 2));
                    q.reward.xp = QsXp(1.8f);
                    q.reward.relic = "Halcyon's Eye";
                    q.reward.relicSlot = (int)GearSlot.Head;
                    q.reward.relicLore = "The machine's single lens, still faintly lit. Through it you see a moment before everyone else does.";
                    q.reward.boon = "QuestBoonWarbanner";
                    q.reward.taleTitle = "The Waking Mind";
                    q.reward.taleText = "HALCYON's last broadcast was not a threat. It was a lullaby, played very softly, on every radio at once. Nobody knows who it was for.";
                }
                break;

            case "weepingrift":
                if (chapter == 0)
                {
                    q = QsNew("rift", "Violet Rain", "A rift-scarred mage",
                        "It rained violet last night, and the puddles show other skies. Somewhere out there the world has torn, and things are looking through.", rng);
                    q.steps.Add(QsTravel("Find the tear at the Weeping Rift.", 1));
                    q.steps.Add(QsDeliver("Feed the warding circle mana-stuff.", 40, "Essence", "Gold"));
                    q.reward.xp = QsXp(1.3f);
                    QsGoods(q, ("Essence", 20));
                    q.reward.taleTitle = "Violet Rain";
                    q.reward.taleText = "The circle held the rift half-shut. Through the gap came a sound like breathing, and a name the mage would not say aloud.";
                }
                else if (chapter == 1)
                {
                    q = QsNew("rift", "Things from the Other Side", "A rift-scarred mage",
                        "The rift is pulling raiders out of other worlds and dropping them at your door. Hold out while the mage studies the wound.", rng);
                    q.steps.Add(QsSurvive(2, "The rift spills raiders on your borders."));
                    q.steps.Add(QsExplore());
                    q.reward.xp = QsXp(1.4f);
                    q.reward.relic = "Riftsplitter";
                    q.reward.relicSlot = (int)GearSlot.Weapon;
                    q.reward.relicLore = "A blade with no back edge: it simply stops, and beyond it is somewhere else. Wounds it makes are slow to close.";
                    q.reward.taleTitle = "Things from the Other Side";
                    q.reward.taleText = "The mage has it now: the rift is not a wound but a mouth, and what owns it is Xal'thuun, the Hungering. It is almost through.";
                }
                else
                {
                    q = QsNew("rift", "The Hungering", "A rift-scarred mage",
                        "Xal'thuun has pushed one great limb through the rift. Kill what has come through, and the rest of it will have to go hungry.", rng);
                    q.steps.Add(QsSlay("Slay Xal'thuun the Hungering at the Shattered Breach.", "Xal'thuun the Hungering", "QuestRiftHorror", 2));
                    q.reward.xp = QsXp(2f);
                    q.reward.boon = "QuestBoonRiftWard";
                    QsGoods(q, ("Essence", 40), ("Gold", 80));
                    q.reward.taleTitle = "The Hungering";
                    q.reward.taleText = "When the limb fell, the rift closed like an eye. The violet rain still falls once a year, and the children dance in it.";
                }
                break;

            case "silenthulk":
                if (chapter == 0)
                {
                    q = QsNew("derelict", "A Light That Fell", "A starport's night watch",
                        "Something came down in the night: not a meteor, too slow, too straight. It answered your hails with a single word, in a language nobody speaks.", rng);
                    q.steps.Add(QsTravel("Reach the crash at the Fallen Hulk.", 1));
                    q.steps.Add(State.worlds.Count > 1 ? QsVisit("Take a hero to another of your worlds to compare its star charts.") : QsDeliver("Fuel the starport's scanners to read the hulk's beacon.", 60, "Coin", "Gold"));
                    q.reward.xp = QsXp(1.3f);
                    QsGoods(q, ("Coin", 100));
                    q.reward.taleTitle = "A Light That Fell";
                    q.reward.taleText = "The charts agree: the hulk left its home before your sun was lit. Its crew are long dead, but its keeper is not.";
                }
                else
                {
                    q = QsNew("derelict", "The Long Watch", "An old navigator",
                        "Keeper-7 has guarded the hulk's cargo for longer than your world has had seas. It will not let anyone near. The navigator thinks the cargo is a map.", rng);
                    q.steps.Add(QsSlay("Overcome Keeper-7 of the Silent Hulk.", "Keeper-7 of the Silent Hulk", "QuestDerelictWarden", 2));
                    q.reward.xp = QsXp(1.8f);
                    q.reward.relic = "Keeper's Core";
                    q.reward.relicSlot = (int)GearSlot.Body;
                    q.reward.relicLore = "The guardian's heart, a lattice of light. It still keeps watch, and its wearer heals as the old machine once mended itself.";
                    q.reward.boon = "QuestBoonStarCharts";
                    q.reward.taleTitle = "The Long Watch";
                    q.reward.taleText = "The cargo was a map, of a kind: every star the hulk's makers had ever visited, and one more, marked in red, that they had fled from.";
                }
                break;

            case "precursors":
                if (chapter == 0)
                {
                    q = QsNew("precursor", "The Singing Obelisk", "A stranded surveyor",
                        "An obelisk on this world hums a note only heroes seem to hear. The surveyor's instruments say it is very old, and very awake.", rng);
                    q.steps.Add(QsTravel("Bring a hero to the Singing Obelisk.", 1));
                    q.steps.Add(QsExplore());
                    q.reward.xp = QsXp(1.3f);
                    QsGoods(q, ("Gold", 60), ("Essence", 20));
                    q.reward.taleTitle = "The Singing Obelisk";
                    q.reward.taleText = "The note changed when your hero touched the stone, and far across this world another obelisk began to answer.";
                }
                else if (chapter == 1)
                {
                    q = QsNew("precursor", "Doors Within Doors", "Glyphs in the precursor ruins",
                        "The obelisks are keys. The glyphs say the door they open lies in a vault the precursors sealed when they left.", rng);
                    q.steps.Add(QsSite("Open a precursor vault on this world.", SiteKind.Dungeon));
                    q.steps.Add(QsDeliver("Feed power to the ancient lock.", 60, "Essence", "Gold", "Coin"));
                    q.reward.xp = QsXp(1.5f);
                    q.reward.relic = "Shell of the First Makers";
                    q.reward.relicSlot = (int)GearSlot.Body;
                    q.reward.relicLore = "Armour grown, not made, by the precursors. It fits anyone, and it remembers a sky with two suns.";
                    q.reward.taleTitle = "Doors Within Doors";
                    q.reward.taleText = "The lock opened onto a hall of sleeping guardians. One of them did not stay asleep.";
                }
                else
                {
                    q = QsNew("precursor", "The Patient Warden", "A voice in the monolith",
                        "The First Warden has waited a million years for the precursors to return. It has decided you will do instead, as an enemy.", rng);
                    q.steps.Add(QsSlay("Defeat the First Warden of the Elder Gate.", "The First Warden of the Elder Gate", "QuestPrecursorWarden", 2));
                    q.reward.xp = QsXp(2f);
                    q.reward.boon = "QuestBoonPrecursorCodex";
                    QsGoods(q, ("Gold", 120), ("Essence", 40));
                    q.reward.taleTitle = "The Patient Warden";
                    q.reward.taleText = "With its last light the Warden showed your hero where the precursors went: not out among the stars, but inward, into the space between them.";
                }
                break;
        }
        return q;
    }
}
