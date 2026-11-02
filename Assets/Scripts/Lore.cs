/// <summary>
/// Fragments of the world's history, carved on lore stones and gathered in the journal. They hint
/// at what lies further out: the old kingdoms, the barrow kings, and the lights in the sky.
/// </summary>
public static class Lore
{
    public struct Entry
    {
        public string title, text;
        public Entry(string title, string text) { this.title = title; this.text = text; }
    }

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
    };
}
