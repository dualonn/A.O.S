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
        Enlightenment = 6, Industrial = 7, Modern = 8, Electric = 9, Information = 10, Arcane = 11, Astral = 12, Stellar = 13, Interstellar = 14, Galactic = 15, Universal = 16;
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
        new Entry("The Sun's Song",
            "The old sun-hymns were never prayers. Sung into a heliolite lens at the right pitch, they make it glow; sung by a choir, they make it burn. The priests always knew. They just never had the lenses.", Stellar),
        new Entry("The Eclipse",
            "They worship the dark at the heart of the sun: the moment it goes out. They wear its burning ring for a face, and they have sworn to put out every sun that men have caught in glass.", Stellar),
        new Entry("Bottled Dawn",
            "A bottle of sunfire lights a house for a year and heats it for ten. The first merchant to sell them grew so rich he bought a moon, and then, it is said, could not find a ship to take him there.", Stellar),
        new Entry("The Fold",
            "Space is not empty, the gravimancers say, but cloth: and cloth can be folded until two far places touch. The first fold was made by accident, in a teacup. The second took a city's worth of mana and went to another star.", Interstellar),
        new Entry("Whales Between the Stars",
            "They were always there, swimming the dark between suns, too slow for anyone to notice. They sing to each other across light-years. Some of them, it turns out, are willing to carry a rider, if the rider sings back.", Interstellar),
        new Entry("What the Rift Is",
            "The Rift is not a place. It is the space between the folds, where things fall when a fold is made badly. The reavers were people once, on a world that folded itself shut. They have been trying to get out ever since.", Interstellar),
        new Entry("The Hive",
            "It does not want your gold, your land or your gods. It wants your world, all of it, to eat. It has eaten others: the seven worlds were twelve, once, the oldest star charts say.", Galactic),
        new Entry("The Gate Builders",
            "The precursors did not build ships. They built gates, from star to star, and walked. Every ancient gate you wake is a door they left open behind them, as if they meant to come back, or meant for someone to follow.", Galactic, after: 20),
        new Entry("The Ark",
            "A ship the size of a city, built to carry a people from a dying world. It never needed to. Now it carries armies, and on its bridge, carved where every captain can see it, are the words: In case we were wrong.", Galactic),
        new Entry("The Unmade",
            "At the end of everything, when the last star has gone out, there is nothing left but the Unmade. They have come back from there to here, to make it happen sooner. They do not hate you. They are only tidying up.", Universal),
        new Entry("The First Moment",
            "Primordium is stone from the first moment of everything, still warm. Hold a piece to your ear and you can hear it: not a bang, the old books were wrong, but a kind of laugh.", Universal),
        new Entry("Borrowed Hours",
            "A Chronomancer does not heal a wound. She finds the moment before it, and borrows that moment back. Every hour borrowed must be paid back one day, they say; nobody knows by whom.", Universal, after: 20),
        // ----- Wonders: given when you build one, or find one (never on lore stones) -----
        new Entry("Stone Circle",
            "Nobody remembers who raised the first stones, or how. They knew where the sun would rise on the longest day, and they set a stone there, and the people came to watch it, and kept coming, and began to count the days.", 99),
        new Entry("Great Ziggurat",
            "A mountain made by hand, step on step, so that the priests could climb closer to the gods. The gods never came down, but the stone-cutters and the scribes who counted them grew so many that the city came instead.", 99),
        new Entry("Hanging Gardens",
            "A queen from the hills, homesick in the flat country, was given a mountain of terraces and trees. Water was lifted to the top by screws turned day and night. They say the gardens smelled of rain even in the dry season.", 99),
        new Entry("Great Library",
            "Every ship that came into the harbour was searched, and every scroll on it copied. The copies went back to the ships. The originals stayed. Some say the library holds a copy of every book ever written, and one or two not yet.", 99),
        new Entry("Grand Cathedral",
            "It took four lifetimes to build. The mason who laid the first stone was buried under it, and her great-great-grandson carved the last angel on the spire. The bells can be heard three valleys away.", 99),
        new Entry("Grand Observatory",
            "Its lenses were ground for twenty years, its dome turns on bronze rails, and from it the astronomers saw at last what the old monk only drew: seven worlds, each with seas and mountains of its own.", 99),
        new Entry("Royal Academy",
            "A palace given over to argument. Philosophers, surgeons, engineers and poets, all under one roof, all arguing. The king who founded it said he wanted wisdom. He got an encyclopaedia, eleven new sciences and a revolution.", 99),
        new Entry("Great Engine",
            "The largest machine ever built, a cathedral of iron and steam. It drives every mill in the city through a forest of shafts and belts, and when it was first started up the whole city stopped work to listen to it breathe.", 99),
        new Entry("Skyscraper",
            "A hundred floors of steel and glass, so tall the top is in cloud on bad days. A city of offices in one building. From the observation deck, they say, you can see the curve of the world.", 99),
        new Entry("Tesla Tower",
            "A tower of copper and lightning that sends power through the air itself, no wires needed. Every lamp for a hundred miles glows a little brighter when it hums. Birds will not land on it.", 99),
        new Entry("Data Ark",
            "Everything your people know, stored in crystal under a mountain, copied three times and cooled by an underground river. If the world ended tomorrow, the Ark would remember it.", 99),
        new Entry("Archmage's Spire",
            "A tower that grows a floor whenever a new spell is written, and has grown past the clouds. The archmage at its top is said to be the same one who started it, centuries ago. Nobody has dared ask.", 99),
        new Entry("Space Elevator",
            "A thread from the ground to the stars, strong as starmetal and fine as hair. Climbers ride up it in a day. From the top, the homeworld is a blue coin you could hide behind your thumb.", 99),
        new Entry("Sunforge",
            "A forge built round a captured piece of the sun. It melts anything, makes starmetal from iron, and must be fed sunfire every hour or it will break loose and go home.", 99),
        new Entry("Fold Nexus",
            "A knot in space where a thousand folds meet. Step through the right door and you are on another world; step through the wrong one and you are, the engineers insist, also on another world, just not one they meant.", 99),
        new Entry("Galactic Senate",
            "A hall where every world sends someone to argue, in a thousand tongues at once. Nothing is ever decided quickly, but since it was built, nothing has been decided by war either.", 99),
        new Entry("Cradle of Creation",
            "The machine that makes new worlds. A seed of Genesis Matter goes in; a world comes out, small and raw and steaming, with an ocean and a sky. Nobody has yet worked out who it is that lives on them.", 99),
        new Entry("Thundering Falls",
            "A whole river falls off the edge of the land here, so loud you feel it in your chest before you hear it. The mist makes rainbows all day, and the fields below it never go dry.", 99),
        new Entry("The Crystal Spire",
            "A single crystal, taller than any tower, rising out of the ground like a spear. Mana gathers round it the way water gathers in a hollow, and at night it hums.", 99),
        new Entry("The Elder Tree",
            "The oldest living thing on this world. Its roots go down further than any mine, and children born in its shade, the old women say, are always strong.", 99),
        new Entry("The Fire Caldera",
            "A crater of molten rock a mile across, breathing smoke. Smiths come from far away to work iron in its heat, and none of them ever goes home with a cold forge.", 99),
        new Entry("The Sky Arch",
            "An arch of red stone so high that clouds pass through it. Nobody made it. The wind did, the geologists say, over a million years, which is somehow more frightening.", 99),
        new Entry("The Singing Dunes",
            "When the wind blows over these dunes, the sand sings: a low, endless note that carries for miles. Traders follow it across the desert like a road.", 99),
        new Entry("The Aurora Lake",
            "A lake so still it holds the sky. On cold nights the auroras come down to it, and for a while there are two skies, one above and one below, and you can stand between them.", 99),
        new Entry("The Great Rift",
            "The world is splitting here, slowly, a canyon so deep its bottom is in darkness. Every layer of rock is on show in its walls, like the pages of a book about everything that came before.", 99),
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
