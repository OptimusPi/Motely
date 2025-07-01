using System.Collections.Generic;

namespace TheFool.Services;

public static class BalatroDataService
{
    public static readonly Dictionary<string, List<string>> Jokers = new()
    {
        ["Common"] = new()
        {
            "Banner", "Mystic Summit", "8 Ball", "Misprint", "Dusk", "Raised Fist",
            "Chaos the Clown", "Fibonacci", "Steel Joker", "Scary Face", "Abstract Joker",
            "Delayed Gratification", "Hack", "Pareidolia", "Gros Michel", "Even Steven",
            "Odd Todd", "Scholar", "Business Card", "Supernova", "Ride the Bus", "Space Joker",
            "Egg", "Burglar", "Blackboard", "Runner", "Ice Cream", "DNA", "Splash",
            "Blue Joker", "Sixth Sense", "Constellation", "Hiker", "Faceless Joker",
            "Green Joker", "Superposition", "To Do List", "Cavendish", "Card Sharp",
            "Red Card", "Madness", "Square Joker", "Seance", "Riff-raff", "Vampire",
            "Shortcut", "Hologram", "Vagabond", "Baron", "Cloud 9", "Rocket", "Obelisk",
            "Midas Mask", "Luchador", "Photograph", "Gift Card", "Turtle Bean", "Erosion",
            "Reserved Parking", "Mail-In Rebate", "To the Moon", "Hallucination", "Fortune Teller",
            "Juggler", "Drunkard", "Stone Joker", "Golden Joker", "Lucky Cat", "Baseball Card",
            "Bull", "Diet Cola", "Trading Card", "Flash Card", "Popcorn", "Spare Trousers",
            "Ancient Joker", "Ramen", "Walkie Talkie", "Selzer", "Castle", "Smiley Face",
            "Campfire", "Golden Ticket", "Mr. Bones", "Acrobat", "Sock and Buskin",
            "Swashbuckler", "Troubadour", "Certificate", "Smeared Joker", "Throwback",
            "Hanging Chad", "Rough Gem", "Bloodstone", "Arrowhead", "Onyx Agate", "Glass Joker",
            "Showman", "Flower Pot", "Blueprint", "Wee Joker", "Merry Andy", "Oops! All 6s",
            "The Idol", "Seeing Double", "Matador", "Hit the Road", "The Duo", "The Trio",
            "The Family", "The Order", "The Tribe", "Stuntman", "Invisible Joker", "Brainstorm",
            "Satellite", "Shoot the Moon", "Driver's License", "Cartographer", "Astronomer",
            "Burnt Joker", "Bootstraps", "Canio", "Triboulet", "Yorick", "Chicot",
            "Perkeo"
        },
        ["Uncommon"] = new()
        {
            "Joker", "Greedy Joker", "Lusty Joker", "Wrathful Joker", "Gluttonous Joker",
            "Jolly Joker", "Zany Joker", "Mad Joker", "Crazy Joker", "Droll Joker",
            "Sly Joker", "Wily Joker", "Clever Joker", "Devious Joker", "Crafty Joker",
            "Half Joker", "Joker Stencil", "Four Fingers", "Mime", "Credit Card", "Ceremonial Dagger",
            "Banner", "Mystic Summit", "Loyalty Card", "8 Ball", "Dusk", "Fibonacci"
        },
        ["Rare"] = new()
        {
            "Vampire", "Hologram", "Baron", "Cloud 9", "Rocket", "Obelisk", "Midas Mask",
            "Luchador", "Photograph", "Gift Card", "Turtle Bean", "Erosion", "Reserved Parking",
            "Mail-In Rebate", "To the Moon", "Hallucination", "Fortune Teller"
        },
        ["Legendary"] = new()
        {
            "Triboulet", "Yorick", "Chicot", "Perkeo", "Canio"
        }
    };

    public static readonly List<string> Editions = new()
    {
        "None", "Foil", "Holographic", "Polychrome", "Negative"
    };

    public static readonly List<string> Tarots = new()
    {
        "The Fool", "The Magician", "The High Priestess", "The Empress", "The Emperor",
        "The Hierophant", "The Lovers", "The Chariot", "Justice", "The Hermit",
        "Wheel of Fortune", "Strength", "The Hanged Man", "Death", "Temperance",
        "The Devil", "The Tower", "The Star", "The Moon", "The Sun", "Judgement", "The World"
    };

    public static readonly List<string> Spectrals = new()
    {
        "Familiar", "Grim", "Incantation", "Talisman", "Aura", "Wraith", "Sigil",
        "Ouija", "Ectoplasm", "Immolate", "Ankh", "Deja Vu", "Hex", "Trance", "Medium",
        "Cryptid", "The Soul", "Black Hole"
    };

    public static readonly List<string> Vouchers = new()
    {
        "Overstock", "Clearance Sale", "Hone", "Reroll Surplus", "Crystal Ball",
        "Telescope", "Grabber", "Wasteful", "Tarot Merchant", "Planet Merchant",
        "Seed Money", "Blank", "Magic Trick", "Hieroglyph", "Directors Cut",
        "Paint Brush", "Retcon", "Palette"
    };

    public static readonly List<string> Tags = new()
    {
        "Uncommon Tag", "Rare Tag", "Negative Tag", "Foil Tag", "Holographic Tag",
        "Polychrome Tag", "Investment Tag", "Voucher Tag", "Boss Tag", "Standard Tag",
        "Charm Tag", "Meteor Tag", "Buffoon Tag", "Handy Tag", "Garbage Tag",
        "Ethereal Tag", "Coupon Tag", "Double Tag", "Juggle Tag", "D6 Tag",
        "Top-up Tag", "Speed Tag", "Orbital Tag", "Economy Tag"
    };

    public static readonly List<string> Ranks = new()
    {
        "2", "3", "4", "5", "6", "7", "8", "9", "10", "Jack", "Queen", "King", "Ace"
    };

    public static readonly List<string> Suits = new()
    {
        "Spades", "Hearts", "Diamonds", "Clubs"
    };
}
