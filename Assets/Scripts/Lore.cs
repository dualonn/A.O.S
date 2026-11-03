/// <summary>
/// Fragments of the world's history, carved on lore stones and gathered in the journal. They hint
/// at what lies further out: the old kingdoms, the barrow kings, and the lights in the sky.
/// </summary>
public static class Lore
{
    public struct Entry
    {
        public string title, text;
        [UnityEngine.Tooltip("Found only from this age on (age index).")]
        public int minAge;
        [UnityEngine.Tooltip("Found only on stones in this land (-1: anywhere).")]
        public int biome;
        [UnityEngine.Tooltip("Found only after this entry was read (-1: none), so story arcs come in order.")]
        public int after;
        public Entry(string title, string text, int minAge = 0, int biome = -1, int after = -1)
        { this.title = title; this.text = text; this.minAge = minAge; this.biome = biome; this.after = after; }
    }

    // Age indexes, for readability below.
    private const int Stone = 0, Bronze = 1, Iron = 2, Classical = 3, Medieval = 4, Renaissance = 5,
        Enlightenment = 6, Industrial = 7, Modern = 8, Electric = 9, Information = 10, Arcane = 11, Astral = 12;
    private const int Desert = (int)Biome.Desert, Tundra = (int)Biome.Tundra, Swamp = (int)Biome.Swamp,
        Volcanic = (int)Biome.Volcanic, Crystal = (int)Biome.Crystal;

    public static readonly Entry[] Entries =
    {
        new Entry("The First Fire",
            "Before the first fire there was only the long dark and the hungry things in it. The one who struck the spark is not named here: only that they kept it burning, and that others came to sit by it."),
        new Entry("Of the Barrow Kings",
            "They were kings once, and would not stop being kings when they died. They had themselves buried with their swords and their guards, and they still keep court beneath the hills, waiting for someone to bow."),
        new Entry("The Wolf Who Counted",
            "The grey wolves remember every hunter who wronged them. Leave them the first share of a kill, the old hunters say, or they will come for the second share themselves."),
        new Entry("The Sundered Road",
            "A road of fitted stone once ran from sea to sea. Where it broke, the land broke with it. You can still find its stones in the grass, too straight and too square for any river to have made."),
        new Entry("Shrines of the Quiet Ones",
            "No one built the shrines, the priests insist; they were found. Kneel at one with a true heart and the Quiet Ones lend a little of their strength to all your people, for as long as the stone stands."),
        new Entry("The Bronze Bargain",
            "Copper is soft and tin is brittle, but married in the fire they make bronze. The first smiths said it was a bargain struck with the mountain itself, and paid it back in songs."),
        new Entry("Why Raiders Burn",
            "The raiders were farmers once, before the long winter took their fields. Now they take what they need, and burn what they cannot carry, so that no one else will have what they lost."),
        new Entry("The Webbed Wood",
            "In the deep woods the spiders grow as large as hounds and weave between the trees like sails. Their silk is stronger than rope. Their patience is longer than a lifetime."),
        new Entry("The Troll Under the Ford",
            "Pay the troll his toll and cross in peace. Refuse, and he will remember your face for a hundred years. Trolls do not forget, and they do not forgive, and they do not die easily."),
        new Entry("Iron from the Sky",
            "The first iron did not come from the ground. It fell, burning, out of the night, and the smith who found it made a blade that never dulled. They say more of it still waits, far out where the stars come down."),
        new Entry("The Lights Above",
            "Some nights a few of the stars do not twinkle. They move, slowly, as if someone were carrying lanterns across the sky. The old tales say those are other worlds, and that one day we will walk on them."),
        new Entry("The Age That Will Come",
            "Stone, then bronze, then iron, then marble and law. The seers carved one more mark below these, a shape like a wheel of cogs breathing steam, and would not say what it meant."),
        new Entry("A Soldier's Grave",
            "Here lies one who held the wall when the others ran. Their name is worn away. Their shield is still here, set into the stone, so that the wall would remember them."),
        new Entry("The Caravan Road",
            "The merchants of the south crossed the wild with silk and salt. Not all of them arrived. If you find their carts in the grass, take what they carried: they would rather it was used than lost."),

        // ----- The star-fallen: a thread from the sky-iron to the worlds beyond (read in order) -----
        new Entry("The Star Map",
            "A monk spent forty winters charting the stars that move. His map shows seven of them, each with a name in no tongue anyone speaks, joined by a ring drawn in silver ink.", Medieval, after: 10),
        new Entry("The Astronomer's Letter",
            "\"With the new lenses I have seen them plainly: not stars, but worlds, with seas and mountains of their own. One is red as rust. One is wrapped in ice. One glitters, as though it were cut from a single gem.\"", Renaissance, after: 14),
        new Entry("The Brass Orrery",
            "The philosophers built the monk's map in brass: seven worlds turning about the sun on silver arms. When it was wound for the first time the aether in the room grew cold, and every compass in the city pointed up.", Enlightenment, after: 15),
        new Entry("What the Sky-Iron Is",
            "Under the steam hammer, the old star-iron would not flatten. Under the microscope it was not iron at all, but a lattice, too regular to be grown. Someone made it. Someone, somewhere out there, threw it down to us.", Industrial, after: 16),
        new Entry("The Wireless Whisper",
            "The great receiving towers pick up a voice beneath the static, always at the same hour. It counts, slowly, from one to seven, and then it says a word that sounds very much like \"come\".", Electric, after: 17),
        new Entry("The Quiet Satellites",
            "Our machines in the sky found older machines already there: dark, patient, circling since before the first fire. They woke when we spoke to them, and turned, all at once, to face the seventh world.", Information, after: 18),
        new Entry("The Gate at the Edge of Night",
            "The calculations are finished. The worlds are not far, only folded away, and the old shrines were never shrines at all, but doors with the keys taken out. The old magic is not the opposite of science, the seers say: it is the part we had not measured yet.", Information, after: 19),

        // ----- The lands -----
        new Entry("The Glass Sea",
            "Where lightning strikes the dunes it leaves glass behind: twisted, hollow and green. Desert folk read the shapes like letters, and say the sky writes in them to those patient enough to collect a whole sentence.", Stone, Desert),
        new Entry("The Sleepers in the Sand",
            "A whole city lies under the dunes, its towers reaching up like drowned fingers. The wind uncovers a roof now and then. Inside, the beds are made, the tables laid, and no one has ever been found.", Bronze, Desert),
        new Entry("The Long Winter's Mouth",
            "This is where the long winter began, the northern clans say: a crack in the world, far out on the ice, that breathes cold. The raiders' grandparents farmed here once. The furrows are still under the snow.", Stone, Tundra),
        new Entry("Bones in the Ice",
            "Beasts lie frozen in the glacier, tusked and shaggy and taller than houses. One of them still has a spear in its side, and the spear is made of something that is not stone, bone or bronze.", Iron, Tundra),
        new Entry("The Drowned Village",
            "When the river changed its mind, the village went under in a single night. On still evenings you can hear its bell ringing beneath the reeds, calling people to a market that will never open.", Stone, Swamp),
        new Entry("Lights in the Mire",
            "Follow the little lights in the swamp and you will be lost by morning. Follow them for three nights and they will lead you to a hoard. No one has ever come back to say what happens on the fourth.", Bronze, Swamp),
        new Entry("The Mountain's Heart",
            "The fire mountains are not mountains, the ash-folk say, but the scabs over a wound in the world. Sometimes the wound reopens, and what bleeds out is the same red as the sky-iron's glow.", Stone, Volcanic),
        new Entry("The Ash Smiths",
            "In the burning lands the smiths need no forge. They work at the lip of the lava, with tongs as long as spears, and the blades they make there will cut through a lesser sword like reeds.", Iron, Volcanic),
        new Entry("The Singing Stones",
            "The crystals hum when the wind blows through them, each a different note. Strike the right three in the right order, the shepherds say, and the hills will sing back.", Stone, Crystal),
        new Entry("Light Without Sun",
            "Deep in the crystal caves there is light, though no sun reaches. The crystals drink the day and give it back all night. Runesmiths grind them into their inks, and the runes never fade.", Medieval, Crystal),

        // ----- The ages -----
        new Entry("The Barrow King's Crown",
            "The crown taken from the barrow was cold to wear and heavy beyond its gold. The one who wore it dreamt every night of a court beneath the hills, bowing, and woke each morning a little more willing to be bowed to.", Iron, after: 1),
        new Entry("The Last Barrow",
            "The greatest barrow was never opened. The legions built a temple over it instead, and the priests still pour wine into a crack in the floor once a year, so that the king below stays content.", Classical, after: 31),
        new Entry("The Knights' Oath",
            "Protect the weak, keep faith, fear nothing but dishonour. Most knights broke the oath before their beards came in. The few who kept it are the ones the songs remember, and the shrines too, it seems.", Medieval),
        new Entry("Runes That Remember",
            "A rune is a word that will not let itself be forgotten. Cut it into steel and the steel remembers it too, and becomes a little more like the word: keener, harder, or hungrier.", Medieval),
        new Entry("The Painted Ceiling",
            "The painter lay on his back for four years to paint the heavens on the chapel's ceiling. When it was unveiled, the astronomers noticed that the stars were right, every one, including seven that no one else could see.", Renaissance),
        new Entry("Powder That Thunders",
            "Charcoal, sulphur and saltpetre: three dull things that together make thunder. The alchemists were looking for gold. What they found changed walls, kings and war more than gold ever did.", Renaissance),
        new Entry("The Weight of Air",
            "The philosophers weighed the air, then the light, then the space between the stars. They found something there, thin and bright and restless, and named it aether, and it has not been still since.", Enlightenment),
        new Entry("The Clockwork Servant",
            "The first automaton could only pour tea. The second could only march. The tenth could do both, and the watchmaker who built it left a note: \"It wound itself last night. I did not show it how.\"", Industrial),
        new Entry("Smoke Over the Valley",
            "The valley that grew wheat now grows chimneys. The river runs black and the town runs all night. People say it was better before, and then go to work in the mill, because the mill pays.", Industrial),
        new Entry("Engines of War",
            "Trenches, wire, gas and engines. Every invention meant to end the war made it longer. When it finally stopped, people built monuments, and then, quietly, better engines.", Modern),
        new Entry("The Lightning Tamed",
            "The old gods threw lightning. Now it runs along wires to light our streets, cook our food and drive our machines. Some of the old priests say the gods are not pleased. Most people no longer listen.", Electric),
        new Entry("The Thinking Machines",
            "We taught the machines to count, then to remember, then to learn. The newest ones dream, the engineers say, when they think no one is looking. They dream of seven worlds, and of a door.", Information),
        new Entry("Spells That Compile",
            "The first spell run on a machine took four days and burned out the machine. The second took an hour. Now a child can cast with a staff what took a circle of wizards a lifetime, and the old wizards are not sure how they feel about it.", Arcane),
        new Entry("The Iron That Fell",
            "Starmetal does not rust, does not bend, and does not forget where it came from. Hold a piece of it up at night and it pulls, very gently, towards one star.", Astral),
        new Entry("The Void Between",
            "Between the worlds there is a dark that is not empty. Things live there that were raiders once, of some other world, and went too far. Now they come down to ours, burning magenta, hungry for the light.", Astral),
        new Entry("The First Ship",
            "It went up on a column of blue fire, circled the world twice and came down in the sea. The crew climbed out laughing. They had seen the seven worlds plainly, they said, and one of them had lights on it that answered theirs.", Astral, after: 20),
    };

    /// <summary>The entry a lore stone shows: unread ones that fit your age and the stone's land first, story arcs in order.</summary>
    public static int Pick(int variant, int ageIndex, int biome, System.Collections.Generic.ICollection<int> read)
    {
        int best = -1, bestScore = int.MinValue;
        for (int i = 0; i < Entries.Length; i++)
        {
            int id = (variant + i) % Entries.Length;
            var e = Entries[id];
            if (read.Contains(id) || e.minAge > ageIndex) continue;
            if (e.biome >= 0 && e.biome != biome) continue;
            if (e.after >= 0 && !read.Contains(e.after)) continue;
            // Prefer the land's own lore, then the current age's, then story arcs.
            int score = (e.biome >= 0 ? 3 : 0) + (e.minAge == ageIndex ? 2 : 0) + (e.after >= 0 ? 1 : 0);
            if (score > bestScore) { best = id; bestScore = score; }
        }
        return best;
    }
}
