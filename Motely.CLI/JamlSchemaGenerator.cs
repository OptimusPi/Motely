using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Motely.Filters;

namespace Motely;

internal static class JamlSchemaGenerator
{
    private static readonly string[] RankValues = ["2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Jack", "Q", "Queen", "K", "King", "A", "Ace"];
    private static readonly string[] SuitValues = ["Hearts", "Diamonds", "Clubs", "Spades"];
    private static readonly string[] MetadataKeys = ["name", "author", "dateCreated", "description", "deck", "stake", "seeds", "aesthetics"];
    private static readonly string[] SectionKeys = ["defaults", "must", "should", "mustNot"];
    private static readonly string[] ClauseTypeKeys = [
        "joker", "jokers",
        "commonJoker", "commonJokers",
        "uncommonJoker", "uncommonJokers",
        "rareJoker", "rareJokers",
        "mixedJoker", "mixedJokers",
        "soulJoker",
        "legendaryJoker",
        "voucher", "vouchers",
        "tarot", "tarotCard",
        "spectral", "spectralCard",
        "planet", "planetCard",
        "boss",
        "tag",
        "smallBlindTag",
        "bigBlindTag",
        "standardCard",
        "erraticRank",
        "erraticSuit",
        "erraticCard",
        "startingDraw",
        "event",
        "eventType"
    ];
    private static readonly string[] PropertyKeys = [
        "type", "value",
        "antes", "score", "min", "max", "label",
        "edition", "stickers", "seal", "enhancement", "rank", "suit",
        "rolls", "shopItems", "boosterPacks", "minShopSlot", "maxShopSlot", "minPackSlot", "maxPackSlot",
        "sources", "and", "or"
    ];
    private static readonly string[] SourceKeys = [
        "shopItems", "boosterPacks", "minShopSlot", "maxShopSlot", "minPackSlot", "maxPackSlot",
        "tags", "requireMega", "judgement", "rareTag", "uncommonTag", "wraith", "soulCard", "riffRaff",
        "purpleSealOrEightBall", "emperor", "sixthSense", "seance", "certificate", "incantation", "familiar", "grim",
        "deckDraw", "uncommonShopJokers", "rareShopJokers", "commonShopJokers", "allShopJokers"
    ];

    public static void GenerateAndWriteAll(string repoRoot)
    {
        var version = ReadMotelyVersion(repoRoot);
        var schema = Generate(version);
        var json = schema.ToJsonString(new JsonSerializerOptions { WriteIndented = true });

        foreach (var path in GetJsonOutputPaths(repoRoot))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, json + Environment.NewLine);
        }
    }

    public static string ReadMotelyVersion(string repoRoot)
    {
        var propsPath = Path.Combine(repoRoot, "Directory.Packages.props");
        var doc = XDocument.Load(propsPath);
        var version = doc.Root?
            .Elements("PropertyGroup")
            .Elements("MotelyVersion")
            .Select(x => x.Value.Trim())
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(version))
            throw new InvalidOperationException($"MotelyVersion not found in {propsPath}");

        return version;
    }

    public static string FindRepoRoot(string startDirectory)
    {
        var dir = new DirectoryInfo(startDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Motely.sln")) && File.Exists(Path.Combine(dir.FullName, "Directory.Packages.props")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate repo root containing Motely.sln and Directory.Packages.props.");
    }

    private static IEnumerable<string> GetJsonOutputPaths(string repoRoot)
    {
        yield return Path.Combine(repoRoot, "jaml.schema.json");
        yield return Path.Combine(repoRoot, "public", "jaml.schema.json");
        yield return Path.Combine(repoRoot, "Motely.npm-staging", "motely-wasm", "jaml.schema.json");
    }

    private static JsonObject Generate(string version)
    {
        var root = new JsonObject
        {
            ["$schema"] = "http://json-schema.org/draft-07/schema#",
            ["$id"] = "https://seedfinder.app/jaml.schema.json",
            ["version"] = version,
            ["title"] = "JAML - Jimbo's Ante Markup Language",
            ["description"] = "Schema for Balatro seed filter configuration files (.jaml)",
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["name"] = StringProperty("Display name of the filter"),
                ["description"] = StringProperty("Description of what this filter searches for"),
                ["author"] = StringProperty("Creator of the filter"),
                ["dateCreated"] = new JsonObject
                {
                    ["type"] = "string",
                    ["format"] = "date-time",
                    ["description"] = "ISO 8601 timestamp when filter was created"
                },
                ["deck"] = EnumStringProperty(EnumNames<MotelyDeck>(), "Balatro deck to search with", "Red"),
                ["stake"] = EnumStringProperty(EnumNames<MotelyStake>(), "Balatro stake level", "White"),
                ["seeds"] = StringArrayProperty("Known seed examples associated with this filter."),
                ["aesthetics"] = new JsonObject
                {
                    ["type"] = "array",
                    ["description"] =
                        "Optional seed-space constraints from JamlAesthetic (see definitions/JamlAesthetic). Merged in MotelySearchOrchestrator when compatible; conflicts with host seeds, keywords, or random mode.",
                    ["items"] = new JsonObject { ["$ref"] = "#/definitions/JamlAesthetic" },
                },
                ["defaults"] = BuildDefaultsProperty(),
                ["must"] = ClauseArray("Required clauses. All listed clauses must match."),
                ["should"] = ClauseArray("Scored clauses. Matching clauses add score but do not gate the seed by themselves."),
                ["mustNot"] = ClauseArray("Rejected clauses. If any listed clause matches, the seed is rejected.")
            },
            ["definitions"] = new JsonObject
            {
                ["clause"] = BuildClauseDefinition(),
                ["JamlAesthetic"] = new JsonObject
                {
                    ["title"] = "JamlAesthetic",
                    ["description"] =
                        "Named constraint on which seeds participate in search. Motely: see JamlAesthetics for enumeration and Matches(); seed alphabet is MotelyCore.SeedDigits, max length MotelyCore.MaxSeedLength.",
                    ["type"] = "string",
                    ["enum"] = ToJsonArray(JamlAestheticParser.KnownJamlStringsForSchema()),
                },
            },
            ["additionalProperties"] = false
        };

        return root;
    }

    private static JsonObject BuildDefaultsProperty()
    {
        return new JsonObject
        {
            ["type"] = "object",
            ["description"] = "Default values applied to clauses when a clause does not specify its own values.",
            ["properties"] = new JsonObject
            {
                ["antes"] = IntegerArrayProperty(0, 39, "Default antes to check if a clause does not specify antes.", [1, 2, 3, 4, 5, 6, 7, 8]),
                ["boosterPacks"] = IntegerArrayProperty(0, 5, "Default pack offering slots to inspect.", [0, 1, 2, 3, 4, 5]),
                ["shopItems"] = IntegerArrayProperty(0, 5, "Default shop item slots to inspect.", [0, 1, 2, 3, 4, 5]),
                ["score"] = IntegerProperty("Default score for should clauses.", 0, 1)
            },
            ["additionalProperties"] = false
        };
    }

    private static JsonObject BuildClauseDefinition()
    {
        var properties = new JsonObject
        {
            ["type"] = EnumStringProperty([
                "Joker",
                "CommonJoker",
                "UncommonJoker",
                "RareJoker",
                "MixedJoker",
                "SoulJoker",
                "Voucher",
                "TarotCard",
                "Planet",
                "PlanetCard",
                "Spectral",
                "SpectralCard",
                "Tag",
                "SmallBlindTag",
                "BigBlindTag",
                "Boss",
                "BossBlind",
                "StandardCard",
                "Event",
                "LuckyMoney",
                "LuckyMult",
                "MisprintMult",
                "WheelOfFortune",
                "CavendishExtinct",
                "GrosMichelExtinct",
                "ErraticRank",
                "ErraticSuit",
                "ErraticCard",
                "StartingDraw",
                "And",
                "Or"
            ], "Explicit clause type when using the type/value form."),
            ["value"] = StringProperty("Primary clause value when using the explicit type/value form."),
            ["eventType"] = EnumStringProperty(EnumNames<MotelyEventType>(), "Event clause value for the explicit type/eventType form."),
            ["joker"] = EnumStringProperty(EnumNames<MotelyJoker>(), "Joker clause."),
            ["jokers"] = StringArrayProperty("Joker clause matching any listed joker names."),
            ["commonJoker"] = EnumStringProperty(EnumNames<MotelyJokerCommon>(), "Common Joker clause."),
            ["commonJokers"] = StringArrayProperty("Common Joker clause matching any listed common joker names."),
            ["uncommonJoker"] = EnumStringProperty(EnumNames<MotelyJokerUncommon>(), "Uncommon Joker clause."),
            ["uncommonJokers"] = StringArrayProperty("Uncommon Joker clause matching any listed uncommon joker names."),
            ["rareJoker"] = EnumStringProperty(EnumNames<MotelyJokerRare>(), "Rare Joker clause."),
            ["rareJokers"] = StringArrayProperty("Rare Joker clause matching any listed rare joker names."),
            ["mixedJoker"] = EnumStringProperty(EnumNames<MotelyJoker>(), "Mixed-rarity Joker clause."),
            ["mixedJokers"] = StringArrayProperty("Mixed-rarity Joker clause matching any listed joker names."),
            ["soulJoker"] = EnumStringProperty(EnumNames<MotelyJokerLegendary>(), "Soul Joker clause."),
            ["legendaryJoker"] = EnumStringProperty(EnumNames<MotelyJokerLegendary>(), "Legendary Joker clause."),
            ["voucher"] = EnumStringProperty(EnumNames<MotelyVoucher>(), "Voucher clause."),
            ["vouchers"] = StringArrayProperty("Voucher clause matching any listed voucher names."),
            ["tarot"] = EnumStringProperty(EnumNames<MotelyTarotCard>(), "Tarot card clause."),
            ["tarotCard"] = EnumStringProperty(EnumNames<MotelyTarotCard>(), "Tarot card clause."),
            ["spectral"] = EnumStringProperty(EnumNames<MotelySpectralCard>(), "Spectral card clause."),
            ["spectralCard"] = EnumStringProperty(EnumNames<MotelySpectralCard>(), "Spectral card clause."),
            ["planet"] = EnumStringProperty(EnumNames<MotelyPlanetCard>(), "Planet card clause."),
            ["planetCard"] = EnumStringProperty(EnumNames<MotelyPlanetCard>(), "Planet card clause."),
            ["boss"] = EnumStringProperty(EnumNames<MotelyBossBlind>(), "Boss clause."),
            ["tag"] = EnumStringProperty(EnumNames<MotelyTag>(), "Tag clause matching either blind position."),
            ["smallBlindTag"] = EnumStringProperty(EnumNames<MotelyTag>(), "Small blind tag clause."),
            ["bigBlindTag"] = EnumStringProperty(EnumNames<MotelyTag>(), "Big blind tag clause."),
            ["standardCard"] = EnumStringProperty(EnumNames<MotelyPlayingCard>(), "Playing card clause."),
            ["erraticRank"] = EnumStringProperty(RankValues, "Erratic deck rank clause."),
            ["erraticSuit"] = EnumStringProperty(SuitValues, "Erratic deck suit clause."),
            ["erraticCard"] = EnumStringProperty(EnumNames<MotelyPlayingCard>(), "Erratic deck card clause."),
            ["startingDraw"] = EnumStringProperty(EnumNames<MotelyPlayingCard>(), "Starting draw clause."),
            ["event"] = EnumStringProperty(EnumNames<MotelyEventType>(), "Event clause."),
            ["antes"] = IntegerArrayProperty(0, 39, "Which antes to search in."),
            ["score"] = IntegerProperty("Score contribution for should clauses.", 0, 1),
            ["min"] = IntegerProperty("Minimum matches required for this clause.", 0),
            ["max"] = IntegerProperty("Maximum matches allowed for this clause.", 0),
            ["label"] = StringProperty("Custom label for result output."),
            ["edition"] = EnumStringProperty(EnumNames<MotelyItemEdition>().Where(x => x != "None"), "Required edition for matching items."),
            ["stickers"] = new JsonObject
            {
                ["type"] = "array",
                ["items"] = new JsonObject
                {
                    ["type"] = "string",
                    ["enum"] = ToJsonArray(EnumNames<MotelyJokerSticker>().Where(x => x != "None"))
                },
                ["description"] = "Required joker stickers."
            },
            ["seal"] = EnumStringProperty(EnumNames<MotelyItemSeal>().Where(x => x != "None"), "Required seal for playing cards."),
            ["enhancement"] = EnumStringProperty(EnumNames<MotelyItemEnhancement>().Where(x => x != "None"), "Required enhancement for playing cards."),
            ["rank"] = EnumStringProperty(RankValues, "Required rank for playing-card-based clauses."),
            ["suit"] = EnumStringProperty(SuitValues, "Required suit for playing-card-based clauses."),
            ["rolls"] = IntegerArrayProperty(0, null, "Event occurrence indices to check."),
            ["shopItems"] = IntegerArrayProperty(0, 1023, "Clause-level source mapping for shop item slots."),
            ["boosterPacks"] = IntegerArrayProperty(0, 5, "Clause-level source mapping for pack offering slots."),
            ["minShopSlot"] = IntegerProperty("Minimum shop slot index for range generation.", 0, null, 1023),
            ["maxShopSlot"] = IntegerProperty("Maximum shop slot index for range generation.", 0, null, 1023),
            ["minPackSlot"] = IntegerProperty("Minimum pack slot index for range generation.", 0, null, 5),
            ["maxPackSlot"] = IntegerProperty("Maximum pack slot index for range generation.", 0, null, 5),
            ["sources"] = BuildSourcesProperty(),
            ["and"] = ClauseArray("Nested AND clause list."),
            ["or"] = ClauseArray("Nested OR clause list.")
        };

        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["allOf"] = new JsonArray(BuildEventAntesRestriction()),
            ["additionalProperties"] = false,
            ["minProperties"] = 1
        };
    }

    private static JsonObject BuildEventAntesRestriction()
    {
        return new JsonObject
        {
            ["if"] = new JsonObject
            {
                ["anyOf"] = new JsonArray(
                    new JsonObject
                    {
                        ["required"] = ToJsonArray(["event"])
                    },
                    new JsonObject
                    {
                        ["required"] = ToJsonArray(["eventType"])
                    },
                    new JsonObject
                    {
                        ["properties"] = new JsonObject
                        {
                            ["type"] = new JsonObject
                            {
                                ["enum"] = ToJsonArray([
                                    "Event",
                                    "LuckyMoney",
                                    "LuckyMult",
                                    "MisprintMult",
                                    "WheelOfFortune",
                                    "CavendishExtinct",
                                    "GrosMichelExtinct"
                                ])
                            }
                        },
                        ["required"] = ToJsonArray(["type"])
                    }
                )
            },
            ["then"] = new JsonObject
            {
                ["not"] = new JsonObject
                {
                    ["required"] = ToJsonArray(["antes"])
                }
            }
        };
    }

    private static JsonObject BuildSourcesProperty()
    {
        return new JsonObject
        {
            ["type"] = "object",
            ["description"] = "Source mapping that describes where the clause may be satisfied.",
            ["properties"] = new JsonObject
            {
                ["shopItems"] = IntegerArrayProperty(0, 1023, "Shop item slot indices."),
                ["boosterPacks"] = IntegerArrayProperty(0, 5, "Pack offering slot indices."),
                ["minShopSlot"] = IntegerProperty("Minimum shop slot index for range generation.", 0, null, 1023),
                ["maxShopSlot"] = IntegerProperty("Maximum shop slot index for range generation.", 0, null, 1023),
                ["minPackSlot"] = IntegerProperty("Minimum pack slot index for range generation.", 0, null, 5),
                ["maxPackSlot"] = IntegerProperty("Maximum pack slot index for range generation.", 0, null, 5),
                ["tags"] = BooleanProperty("Allow tag-based sources."),
                ["requireMega"] = BooleanProperty("Require Mega pack sources where applicable."),
                ["judgement"] = IntegerArrayProperty(0, null, "Judgement roll indices."),
                ["rareTag"] = IntegerArrayProperty(0, null, "Rare Tag roll indices."),
                ["uncommonTag"] = IntegerArrayProperty(0, null, "Uncommon Tag roll indices."),
                ["wraith"] = IntegerArrayProperty(0, null, "Wraith roll indices."),
                ["soulCard"] = IntegerArrayProperty(0, null, "Soul card roll indices."),
                ["riffRaff"] = IntegerArrayProperty(0, null, "Riff-Raff roll indices."),
                ["purpleSealOrEightBall"] = IntegerArrayProperty(0, null, "Purple Seal or 8 Ball tarot roll indices."),
                ["emperor"] = IntegerArrayProperty(0, null, "Emperor roll indices."),
                ["sixthSense"] = IntegerArrayProperty(0, null, "Sixth Sense roll indices."),
                ["seance"] = IntegerArrayProperty(0, null, "Seance roll indices."),
                ["certificate"] = IntegerArrayProperty(0, null, "Certificate roll indices."),
                ["incantation"] = IntegerArrayProperty(0, null, "Incantation roll indices."),
                ["familiar"] = IntegerArrayProperty(0, null, "Familiar roll indices."),
                ["grim"] = IntegerArrayProperty(0, null, "Grim roll indices."),
                ["deckDraw"] = IntegerArrayProperty(0, null, "Deck draw positions."),
                ["uncommonShopJokers"] = IntegerArrayProperty(0, null, "Raw uncommon shop joker stream positions."),
                ["rareShopJokers"] = IntegerArrayProperty(0, null, "Raw rare shop joker stream positions."),
                ["commonShopJokers"] = IntegerArrayProperty(0, null, "Raw common shop joker stream positions."),
                ["allShopJokers"] = IntegerArrayProperty(0, null, "Raw shop joker stream positions across all rarities.")
            },
            ["additionalProperties"] = false
        };
    }

    private static JsonObject ClauseArray(string description)
    {
        return new JsonObject
        {
            ["type"] = "array",
            ["description"] = description,
            ["items"] = new JsonObject
            {
                ["$ref"] = "#/definitions/clause"
            }
        };
    }

    private static JsonObject StringProperty(string description)
    {
        return new JsonObject
        {
            ["type"] = "string",
            ["description"] = description
        };
    }

    private static JsonObject BooleanProperty(string description)
    {
        return new JsonObject
        {
            ["type"] = "boolean",
            ["description"] = description
        };
    }

    private static JsonObject StringArrayProperty(string description)
    {
        return new JsonObject
        {
            ["type"] = "array",
            ["items"] = new JsonObject
            {
                ["type"] = "string"
            },
            ["description"] = description
        };
    }

    private static JsonObject IntegerProperty(string description, int minimum, int? defaultValue = null, int? maximum = null)
    {
        var obj = new JsonObject
        {
            ["type"] = "integer",
            ["minimum"] = minimum,
            ["description"] = description
        };

        if (maximum.HasValue)
            obj["maximum"] = maximum.Value;
        if (defaultValue.HasValue)
            obj["default"] = defaultValue.Value;

        return obj;
    }

    private static JsonObject IntegerArrayProperty(int minimum, int? maximum, string description, IEnumerable<int>? defaultValues = null)
    {
        var items = new JsonObject
        {
            ["type"] = "integer",
            ["minimum"] = minimum
        };

        if (maximum.HasValue)
            items["maximum"] = maximum.Value;

        var obj = new JsonObject
        {
            ["type"] = "array",
            ["items"] = items,
            ["description"] = description
        };

        if (defaultValues != null)
            obj["default"] = new JsonArray(defaultValues.Select(v => (JsonNode?)JsonValue.Create(v)).ToArray());

        return obj;
    }

    private static JsonObject EnumStringProperty(IEnumerable<string> values, string description, string? defaultValue = null)
    {
        var obj = new JsonObject
        {
            ["type"] = "string",
            ["enum"] = ToJsonArray(values),
            ["description"] = description
        };

        if (defaultValue != null)
            obj["default"] = defaultValue;

        return obj;
    }

    private static JsonArray ToJsonArray(IEnumerable<string> values)
    {
        return new JsonArray(values.Select(v => (JsonNode?)JsonValue.Create(v)).ToArray());
    }

    private static string[] EnumNames<T>() where T : struct, Enum
    {
        return Enum.GetNames<T>();
    }
}
