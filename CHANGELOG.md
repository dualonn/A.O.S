# Age of Stickman - Changelog

## How versions work

Versions look like MAJOR.MINOR.PATCH (for example 0.9.0). The version is shown on the title screen.

- MAJOR goes up for huge milestones that change what the game is.
- MINOR goes up for each batch of new features.
- PATCH goes up for releases that only fix things.

The game is still in development, so versions are below 1.0.0; 1.0.0 will be the first public release. Newest version first.

---

## [0.9.0] - 2026-11-17

### Title screen and menus
- The game opens on a title screen over a living town: a random age each time, laid out fresh, anywhere from a hamlet of a few huts to a sprawling city, with the camera cutting between slow pans across it.
- A main menu: Continue, New Game, Load Game, Settings, Controls, Credits and Quit (no Quit in the browser).
- A pause menu (Escape in game): Resume, Save, Settings, Controls, Main menu, Save and quit.
- Settings: master, music, effects and ambience volume; UI scale; fullscreen, resolution and vsync on PC; autosave interval; hints.
- Controls lists every key and mouse control; Credits lists the sound and music attributions.
- The version is shown on the title screen and in the pause menu.

### Ready for the web
- The game now runs in the browser: it saves when the tab is hidden or closed, music loads only when needed and waits for your first click, and music and ambience are about half the size.
- Lighter graphics settings in the browser; PC keeps its full quality.

### Cleanup pass
- One consistent way of layering things on the map, so buildings, units, fire, flyers and scenery overlap correctly everywhere.
- A tidier HUD: one window at a time with an X on each, folding side panels (B and R), help on H as well as F1, shorter messages, and nothing overlapping.
- Units and buildings run faster, especially in big settlements and big armies.

### Fixes
- The top resource bar folds late-age resources neatly into a "+N" instead of squashing the bar.
- Power cables hang only from poles and pylons (no more wire strung through the air between two power plants).

---

## [0.8.0] - 2026-11-07

A big batch that deepens how your people live, trade, govern and fight.

### Saving and starting out
- New Game and multiple save slots.
- Beginner hints to guide your first settlement.

### Trade and roads
- Trade caravans carry goods between settlements.
- Roads connect your towns and speed up trade.

### Disasters
- Natural disasters can strike: be ready to rebuild.

### Governments and laws
- Choose how your realm is ruled and pass laws that change how it plays.

### Utilities and pollution
- Power and water, supplied by district, keep your buildings working.
- Industry now leaves pollution you have to deal with.

### People
- Social classes give your population more character and needs.

### Warfare
- Deeper war with sieges and more ways to attack and defend.

### The sea
- Build on and sail the sea, with ships and sea trade.

### Marching raids and the advisor
- Raids now come from camps as marching war parties you can see approaching.
- An advisor card points out what needs your attention.

### Sound
- New sounds for disasters, government, utilities, trade, sieges, the sea and marching war parties (a spotted horn, camp war drums, scattering raiders).

---

## [0.7.0] - 2026-11-03

The first full release: a living world with seasons, culture, space travel and a full soundtrack.

### Living world
- Seasons and weather, including rain and thunder.
- Random events, and great and natural wonders to discover or build.
- Rivers and the sea shape the land; wildlife roams the wilds.
- Wild peoples you can meet and make deals with (diplomacy).

### People and culture
- Culture and faith for your realm.
- Hero quests to send your champions on.
- A chronicle of your civilisation's story and a market for trading goods.

### Sound and music
- Recorded sound effects, music that follows your age, and ambience for each biome.
- Music reacts faster to battle and calms down soon after a raid.
- Special sounds for events, wonders, diplomacy, weather and every unit type.
- Gun towers and late-age guns no longer sound like bows.

### Quality of play
- Research several things at once.
- Each world keeps its own auto-repair and upgrade timing; new colonies get a free first Starport.
- Every ore looks different at a glance, and the zoomed-out map shows each ore in its own colour.
- Much faster with lots of research and with big armies.
- Raids wait for the last one to end and come less often.
- Troops go out to meet raids on their town.

---

## [0.6.0] - 2026-11-03

### Worlds and space
- Separate maps for each world, with other worlds carrying on in the background.
- A galaxy and star map, space probes and colony ships.
- Cargo routes between worlds, anomalies, alien sites, native peoples and new lands.
- Stellar, Interstellar, Galactic and Universal Ages, world traits, orbital mining, space research and the Star Gate.
- Troopships, space goals, and the Navigator and Xenologist heroes.
- Gear for every age and deeper early ages.
- Camps you build over stay gone.

### Fixes
- Smoother play at high game speeds.
- Attack troops burn camps and no longer chase every raider; troops head home when a raid targets their settlement.
- Units no longer stall on riverbanks, and pathfinding finds long ways round rivers.

---

## [0.5.0] - 2026-11-02 / 2026-11-03

### New lands and heroes
- Biomes: desert, tundra, swamp, volcanic and crystal lands.
- Enchanting: enhance, enchant, re-roll and salvage hero gear.
- Hero classes: Warrior, Ranger, Mage and Priest paths.
- Expansion goals: three rewarding objectives at a time.
- 29 new lore entries tied to ages, lands and a star-world story.

### New ages
- The Arcane Age: spellblades, griffins, golems and skyships, with a sci-fi side of rune-plate power armour and mana rifles.
- The Astral Age, with the War Drake.

---

## [0.4.0] - 2026-11-02

### Ages you grow into
- Ages now advance through your progress instead of being bought.
- Mastery research: capped, levelled research for the Classical Age.
- Rich veins: old mines in the wild where normal mines produce three times as much.

### Eight new ages
- Medieval, Renaissance (gunpowder, cannons, alchemy), Industrial (steam, rifles, automatons, aether), Enlightenment, Modern (machine guns, tanks, oil), Electric (ray guns, hover tanks, robots) and Information (commandos, drones, jets, plasma, androids).
- Flyers, tunnellers and lighter towers.
- The Steam Gun Turret turns to face its target and fires from its barrel.

---

## [0.3.0] - 2026-11-02

### Heroes and the wilds
- Heroes with gear and loot, and gear shown on the figure.
- Bosses, wild sites, underground dungeon levels and burning camps.
- An order to attack camps.
- Medics: Medicine research, the Valetudinarium and Capsarius healers, who cover the whole settlement.

### Look
- All art switched to pixel art, with unit sprites padded so weapons are never cut off.

---

## [0.2.0] - 2026-11-02

### Bigger settlements
- The Iron and Classical Ages, smooth terrain and smarter settlements.
- Mass selection of troops, production chains, automation, walls and a searchable build menu.
- Raider camps send raids and can be burnt down for loot.
- Levels are gone: the map is now endless.

---

## [0.1.0] - 2026-10-30

### First playable game
- Upgraded to Unity 6.3.
- Free mode: build a settlement, train villagers and soldiers, and fight loose, hand-to-hand battles.
- Raids, troop stances and zooming.
- A procedurally generated map, per-building upgrades and stickman units.
- The Bronze Age with animated buildings and ranking up.
