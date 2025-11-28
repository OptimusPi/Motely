namespace Motely.TUI;

/// <summary>
/// Quirky quips and JAML JOTD (Joke Of The Day) for the main menu.
/// Themes: JAML, Motely, SIMD, CPU, Speed, Search, Rare Seeds, Legendary, Balatro, Joker, Jimbo, pifreak
/// </summary>
public static class MotelyQuips
{
    private static readonly Random _random = new();

    /// <summary>
    /// JAML JOTD - Jokey acronym expansions for "JAML"
    /// Displayed as subtitle under the JAML logo
    /// </summary>
    public static readonly string[] JamlJotd =
    [
        // The real one
        "Joker Ante Markup Language",
        // Jimbo themed
        "Jimbo's Ante Markup Language",
        "Jimbo And Motely? Lovely!",
        "Jimbo Approves My Luck",
        "Jimbo's Amazing Multiplier Locator",
        // Poker/card puns
        "Jolly Aces Make Legendaries",
        "Jackpot Ante Modifier Language",
        "Just Another Multiplier Legend",
        "Jokers Always Mean Luck",
        "Just Ace Me Lucky",
        // Programming jokes
        "Java? Assembly?? Machine Language?!",
        "Just Another Markup Language",
        "JSON's Awesome Markup Lovechild",
        "YAML Already Made Lemonade",
        // Balatro specific
        "Jolly Ante Manipulation Lab",
        "Just Another Motely Launcher",
        "Jokers Acquired, Multipliers Loaded",
        "Just Ante My Life",
        // Silly/absurd
        "Jumbled Assorted Microprocessor Luggage",
        "Jolly And Merry Love",
        "Jimbos Absolutely Magnificent Laughter",
        "Just Add More Legendaries",
        "Jokes And Memes, Literally",
        // Search themed
        "Just Another Million Loops",
        "Join And Match Legendaries",
        "Juggling All Matching Loots",
        // Speed themed
        "Juiced And Massively Loaded",
        "Jetting At Maximum Lightspeed",
        // Dad jokes
        "Jokes Are My Legacy",
        "Jesting About My Luck",
        "Joyful And Maybe Lucky",
    ];

    public static readonly string[] Quips =
    [
        // SIMD & CPU humor
        "SIMD: Because one calculation at a time is for peasants!",
        "My vectors are wider than your screen.",
        "256 bits of parallel processing fury!",
        "AVX-512? More like AVX-AMAZING!",
        "Turning your CPU into a seed-finding supercomputer.",
        "One core to find them all... just kidding, we use ALL cores!",
        "Your CPU called. It's finally being used properly.",
        "Embarrassingly parallel? More like gloriously parallel!",
        "SIMD: Single Instruction, Multiple Dreams.",
        "Vectorized seed searching goes brrrrr...",

        // Speed references
        "Faster than a Straight Flush on turn one!",
        "Speed is my middle name. Literally. Mo-Speed-ly.",
        "Ludicrous speed? That's our slow mode.",
        "We search seeds at 88 miles per hour!",
        "Gotta go fast! ...to find that perfect seed.",
        "Loading... just kidding, we're already done!",
        "Searching a million seeds before breakfast.",
        "My other car is a billion seeds per second.",
        "Time is money. Seeds are priceless.",
        "Blink and you'll miss a thousand seeds checked.",

        // Joker & Jimbo references
        "Jimbo believes in you!",
        "Even Jimbo is impressed by these speeds.",
        "Finding Jokers faster than you can say 'Flush Five'!",
        "Jimbo's favorite seed searcher!",
        "What do you call a fast Joker? A speed bump!",
        "Jokers wild? Our search is WILDER!",
        "Jimbo says: 'Nice seed, friend!'",
        "Every Joker deserves a legendary seed.",
        "Jimbo approved, pifreak certified!",
        "Even The Soul can't escape our search!",

        // Balatro game references
        "May your ante be low and your multipliers high!",
        "In a world of seeds, be a Legendary.",
        "Flush with excitement!",
        "Full House? Try Full AWESOME!",
        "Raise the stakes, find the seeds!",
        "Every seed is a new adventure!",
        "The Big Blind fears our search power.",
        "Boss Blind? More like Boss FIND!",
        "Ante up for legendary discoveries!",
        "Your perfect run starts with the perfect seed.",

        // Rare & Legendary seeds
        "Hunting Legendaries since boot-up.",
        "Rare seeds are our specialty!",
        "Common? Never heard of her.",
        "Finding needles in haystacks since 2024.",
        "One in a million? Those are rookie numbers!",
        "Legendary seeds deserve legendary searches.",
        "Rarity is just a filter option!",
        "The rarer the better, the faster we find it!",
        "Your white whale seed awaits...",
        "Legendaries hate this one weird trick!",

        // Search & Discovery
        "Seek and ye shall find... FAST!",
        "To search, perchance to dream...",
        "A journey of a million seeds begins with a single click.",
        "Finding dreams, one seed at a time.",
        "Search smarter, not harder!",
        "Discovery is our middle name.",
        "What are you waiting for? Seeds won't find themselves!",
        "The search never stops (until you press ESC).",
        "Every search is a step toward greatness.",
        "Fortune favors the bold searcher!",

        // Pifreak & Motely references
        "pifreak loves you!",
        "Made with chaos and coffee.",
        "Motely: Where seeds come to be found.",
        "pifreak was here. And there. And everywhere.",
        "Powered by caffeine and parallel processing.",
        "Built different. Searches different.",
        "From pifreak, with love and SIMD.",
        "Motely: The seed searcher with style!",
        "Crafted by pifreak, enjoyed by all!",
        "A pifreak production. Handle with care.",

        // Puns & wordplay
        "We're not joking around! (Okay, maybe a little.)",
        "Seed you later, alligator!",
        "Having a SIMD-ly wonderful time!",
        "Don't worry, be seedy!",
        "Keep calm and search on.",
        "Ante-cipation is killing me!",
        "This search is un-SEED-ably fast!",
        "You've got to be CHIP-ping me!",
        "That's a-MAZE-ing seed-ing speed!",
        "Stop and smell the vectors.",

        // Motivational & quirky
        "Believe in the heart of the seeds!",
        "Today's gonna be a good seed!",
        "Dreams don't work unless you search.",
        "The best time to plant a seed was yesterday. The best time to search is NOW.",
        "Be the seed you wish to find in the world.",
        "Stars can't shine without darkness. Seeds can't shine without Motely!",
        "Life is short. Search fast!",
        "Why walk when you can SIMD?",
        "Greatness awaits in seed form.",
        "You miss 100% of the seeds you don't search.",

        // Silly & absurd
        "This message will self-destruct... or not. We're just a searcher.",
        "Warning: May contain traces of awesome.",
        "No seeds were harmed in this search.",
        "Please keep hands and feet inside the search at all times.",
        "Side effects may include: extreme satisfaction.",
        "Objects in search are faster than they appear.",
        "Insert witty quote here. Oh wait, we did!",
        "I'm not saying we're fast, but... we're really fast.",
        "Plot twist: You are the legendary seed!",
        "Breaking news: Local searcher finds seeds. More at 11.",
    ];

    /// <summary>
    /// Gets a random quip from the collection.
    /// </summary>
    public static string GetRandomQuip() => Quips[_random.Next(Quips.Length)];

    /// <summary>
    /// Gets a specific quip by index (wraps around if out of bounds).
    /// </summary>
    public static string GetQuip(int index) => Quips[Math.Abs(index) % Quips.Length];

    /// <summary>
    /// Gets a random JAML JOTD (Joke Of The Day) acronym expansion.
    /// </summary>
    public static string GetRandomJamlJotd() => JamlJotd[_random.Next(JamlJotd.Length)];
}
