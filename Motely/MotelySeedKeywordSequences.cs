namespace Motely;

/// <summary>
/// Lazy <see cref="IEnumerable{T}"/> sequences and keyword tables for CLI/provider seed modes.
/// Keeps <c>Program.cs</c> thin; enumerate without materializing huge lists.
/// </summary>
public static class MotelySeedKeywordSequences
{
    public static IEnumerable<string> RepeatCharKeywords(int repeatCount)
    {
        for (int c = (int)'A'; c <= (int)'Z'; c++)
            yield return new string((char)c, repeatCount);
    }

    public static IEnumerable<string> AscendingDigitLetterKeywords(int length)
    {
        const string chars = "123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        int maxStart = chars.Length - length;
        for (int i = 0; i <= maxStart; i++)
            yield return chars.Substring(i, length);
    }

    public static IEnumerable<string> DescendingDigitLetterKeywords(int length)
    {
        const string chars = "ZYXWVUTSRQPONMLKJIHGFEDCBA987654321";
        int maxStart = chars.Length - length;
        for (int i = 0; i <= maxStart; i++)
            yield return chars.Substring(i, length);
    }

    /// <summary>Mirror-friendly strings over symmetric characters (length 3–8 from CLI).</summary>
    public static IEnumerable<string> MirrorPatternKeywords(int length)
    {
        const string symmetricChars = "AHIMOTUVWXy18";

        IEnumerable<string> Generate(string current, int remaining)
        {
            if (remaining == 0)
            {
                yield return current;
                yield break;
            }

            foreach (char c in symmetricChars)
            {
                foreach (string pattern in Generate(current + c, remaining - 1))
                    yield return pattern;
            }
        }

        foreach (string pattern in Generate("", length))
            yield return pattern;
    }

    // All keywords must be ≤8 chars, using only 1-9A-Z (no zero).

    public static readonly string[] GrossKeywords =
    [
        "FART",
        "BUTT",
        "POOP",
        "PUKE",
        "BURP",
        "TOOT",
        "GUTS",
        "SLIME",
        "YUCK",
        "DUMB",
        "LAME",
        "UGLY",
        "WEAK",
        "CRUD",
        "CRAP",
        "SICK",
        "NASTY",
        "GROSS",
        "SNOT",
        "BARF",
        "STINK",
        "GRIME",
        "MUCK",
        "FILTH",
        "SLUDGE",
        "RANK",
        "ROTTEN",
        "PUTRID",
        "VILE",
        "CRUSTY",
        "MUSTY",
        "GRIMY",
        "ICKY",
        "SLIMY",
        "FUNKY",
        "REEK",
        "STENCH",
        "GUNK",
        "SCUM",
    ];

    public static readonly string[] NsfwKeywords =
    [
        "FUCK",
        "SHIT",
        "DAMN",
        "HELL",
        "CRAP",
        "ASSES",
        "ASSHAT",
        "DICK",
        "COCK",
        "PUSSY",
        "WHORE",
        "SLUT",
        "BITCH",
        "PISS",
        "CUNT",
        "TWAT",
        "ARSE",
        "BUGGER",
        "SOD",
        "TITS",
        "WANKER",
        "BASTARD",
        "FAGS",
        "FAGGOT",
        "KIKE",
        "DYKE",
        "SPIC",
        "GOOK",
        "CHINK",
        "TRANNY",
        "HOMO",
        "LESBO",
        "RETARD",
        "NIGGA",
        "NIGGER",
        "SMUT",
        "FILTH",
        "LEWD",
        "PERV",
        "KINKY",
        "NASTY",
        "RUDE",
        "CRUDE",
        "CRASS",
        "VULGAR",
    ];

    public static readonly string[] PrettyKeywords =
    [
        "BEAUTY",
        "GRACE",
        "LOVELY",
        "SERENE",
        "BLISS",
        "PEACE",
        "CALM",
        "PURE",
        "BRIGHT",
        "SHINE",
        "GLEAM",
        "GLOW",
        "DIVINE",
        "PRETTY",
        "CHARM",
        "TENDER",
        "SWEET",
        "LOVE",
        "JOY",
        "HOPE",
        "DREAM",
        "MAGIC",
        "WONDER",
        "ANGEL",
        "SILK",
        "PEARL",
        "FAIRY",
        "MUSE",
        "DUSK",
        "DAWN",
        "HAZE",
        "MIST",
        "PETAL",
        "BLOOM",
        "AURA",
        "STAR",
        "LUNA",
        "EMBER",
        "WISH",
        "JADE",
        "IRIS",
        "SAGE",
        "OPAL",
        "LYRIC",
        "VELVET",
        "CRYSTAL",
    ];

    public static readonly string[] FunnyKeywords =
    [
        "LOL",
        "HAHA",
        "HEHE",
        "LMAO",
        "ROFL",
        "YEET",
        "BRUH",
        "LULZ",
        "SILLY",
        "GOOFY",
        "WACKY",
        "ZANY",
        "NUTTY",
        "DOPEY",
        "DAFT",
        "WITTY",
        "PUNNY",
        "JOKEY",
        "QUIRKY",
        "MEME",
        "NOOB",
        "DERP",
        "YOLO",
        "DODO",
        "BOBO",
        "JOJO",
        "BOOM",
        "ZOOM",
        "WOOSH",
        "YOYO",
        "TEHE",
        "GEEKY",
        "LOOPY",
        "BONKY",
        "WONKY",
        "FUNKY",
        "NERD",
        "GEEK",
        "DORK",
        "GOOF",
        "BOOP",
        "BLEP",
        "HONK",
        "BONK",
        "YIKES",
        "SHEESH",
        "SNARK",
        "SASS",
        "SMIRK",
        "CHAOS",
    ];

    public static readonly string[] BalatroKeywords =
    [
        "JOKER",
        "CHIPS",
        "MULT",
        "HAND",
        "CARD",
        "DECK",
        "SUIT",
        "RANK",
        "WILD",
        "SCORE",
        "ROUND",
        "ANTE",
        "BLIND",
        "STAKE",
        "SPADE",
        "HEART",
        "CLUB",
        "JIMBO",
        "JOLLY",
        "SEANCE",
        "GLASS",
        "STEEL",
        "STONE",
        "BRONZE",
        "SILVER",
        "GOLD",
        "VOID",
        "TORN",
        "BLUE",
        "BUFF",
        "COPY",
        "SEAL",
        "SHOP",
        "SELL",
        "LEVEL",
        "PAYOUT",
        "EDGE",
        "BONUS",
        "RETRO",
        "LUCKY",
        "RUN",
        "WIN",
        "BEAT",
        "BOSS",
        "FLUSH",
        "FIVE",
        "HOUSE",
        "PAIR",
        "ACE",
        "KING",
        "QUEEN",
        "JACK",
        "FULL",
        "HIGH",
        "TAROT",
        "PLANET",
        "VOUCHER",
        "TAG",
        "FOIL",
        "HOLO",
        "POLY",
        "RARE",
        "UNCOMM",
        "COMMON",
        "LEGEND",
    ];
}
