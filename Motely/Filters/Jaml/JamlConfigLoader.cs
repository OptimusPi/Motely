using System.Globalization;
using System.Text.Json;
using Motely.Filters;
using Motely.Filters.Jummy;
using SharpYaml.Model;

namespace Motely.Filters.Jaml;

public static class JamlConfigLoader
{
    private static readonly string[] RootKeys =
    [
        "id",
        "name",
        "description",
        "author",
        "dateCreated",
        "deck",
        "stake",
        "seeds",
        "must",
        "should",
        "mustNot",
    ];

    private static readonly string[] SharedClauseKeys =
    [
        "ante",
        "antes",
        "min",
        "max",
        "score",
        "label",
        "sources",
        "with",
        "luck",
        "vouchers",
    ];

    private static readonly string[] JokerClauseKeys =
    [
        "joker",
        "jokers",
        "commonJoker",
        "commonJokers",
        "uncommonJoker",
        "uncommonJokers",
        "rareJoker",
        "rareJokers",
        "edition",
        "stickers",
        "shopItems",
        "boosterPacks",
    ];

    private static readonly string[] LegendaryClauseKeys =
    [
        "legendaryJoker",
        "legendaryJokers",
        "edition",
        "soulCardOnly",
        "soulEditionRolls",
        "boosterPacks",
    ];

    private static readonly string[] StandardCardKeys =
    [
        "standardCard",
        "rank",
        "suit",
        "enhancement",
        "seal",
        "edition",
        "shopItems",
        "boosterPacks",
    ];

    private static readonly string[] StartingDrawKeys = ["startingDraw", "rank", "suit"];

    private static readonly string[] LogicKeys = ["and", "or", "clauses"];

    private static readonly string[] EventKeys =
    [
        "luckyMoney",
        "luckyMult",
        "misprintMult",
        "wheelOfFortune",
        "grosMichelExtinct",
        "cavendishExtinct",
        "spaceLevelup",
        "businessPayout",
        "bloodstoneTrigger",
        "parkingPayout",
        "glassDestroy",
        "wheelStaysFlipped",
        "rolls",
        "mult",
        "value",
    ];

    private static readonly string[] JokerSourceKeys =
    [
        "shopItems",
        "boosterPacks",
        "judgement",
        "emperor",
        "wraith",
        "riffRaff",
        "rareTag",
        "uncommonTag",
        "commonShopJokers",
        "uncommonShopJokers",
        "rareShopJokers",
        "allShopJokers",
    ];

    private static readonly string[] LegendarySourceKeys =
    [
        "shopItems",
        "boosterPacks",
        "arcanaPacks",
        "spectralPacks",
        "soulCard",
        "requireMega",
        "requireMegaPack",
    ];

    private static readonly string[] TarotSourceKeys =
    [
        "shopItems",
        "boosterPacks",
        "emperor",
        "purpleSealOrEightBall",
        "charmTag",
    ];

    private static readonly string[] SpectralSourceKeys =
    [
        "shopItems",
        "boosterPacks",
        "sixthSense",
        "seance",
        "etherealTag",
        "requireMega",
        "requireMegaPack",
    ];

    private static readonly string[] PlanetSourceKeys = ["shopItems", "boosterPacks"];

    private static readonly string[] StandardSourceKeys =
    [
        "shopItems",
        "boosterPacks",
        "certificate",
        "incantation",
        "familiar",
        "grim",
        "deckDraw",
    ];

    private static readonly string[] EventSourceKeys = ["luck"];

    private static readonly string[] WithKeys = ["luck", "vouchers"];

    public static bool TryLoad(string content, out JamlConfig? config, out string? error)
    {
        try
        {
            config = FromYaml(content);
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            config = null;
            error = ex.Message;
            return false;
        }
    }

    public static JamlConfig FromYaml(string content)
    {
        try
        {
            var stream = YamlStream.Load(new StringReader(content));

            if (stream.Count == 0)
                throw new InvalidOperationException("YAML document is empty.");

            if (stream[0].Contents is not YamlMapping root)
                throw new InvalidOperationException("JAML root must be a mapping.");

            return ParseConfig(new NodeReader(root));
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"YAML parse error: {ex.Message}", ex);
        }
    }

    public static bool TryLoadFromJson(string content, out JamlConfig? config, out string? error)
    {
        try
        {
            config = FromJson(content);
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            config = null;
            error = ex.Message;
            return false;
        }
    }

    public static JamlConfig FromJson(string content)
    {
        try
        {
            return FromYaml(content);
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"JSON parse error: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"JSON parse error: {ex.Message}", ex);
        }
    }

    public static LegendaryJokerSourceConfig CreateLegendaryJokerSources(
        LegendaryJokerSourceConfig? userConfig
    ) => userConfig ?? new LegendaryJokerSourceConfig();

    private static JamlConfig ParseConfig(NodeReader root)
    {
        ValidateKeys(root, RootKeys, "JAML root");
        var name = root.GetString("name");
        var config = new JamlConfig
        {
            Id = root.GetString("id") ?? Slugify(name ?? "unnamed"),
            Name = name,
            Description = root.GetString("description"),
            Author = root.GetString("author"),
        };

        if (root.GetString("deck") is { } deck)
            config.Deck = ParseEnum<MotelyDeck>(deck);
        if (root.GetString("stake") is { } stake)
            config.Stake = ParseEnum<MotelyStake>(stake);

        config.Seeds.AddRange(root.GetStringArray("seeds") ?? []);
        config.Must.AddRange(ParseClauseList(root, "must"));
        config.Should.AddRange(ParseClauseList(root, "should"));
        config.MustNot.AddRange(ParseClauseList(root, "mustNot"));
        return config;
    }

    private static IEnumerable<IJamlClause> ParseClauseList(NodeReader root, string key)
    {
        foreach (var item in root.GetClauseList(key) ?? [])
            yield return ParseClauseSource(item);
    }

    // A clause in a list is either a structured mapping (joker: …) or a single-line JAML clause
    // ("Eternal Blueprint in antes 1 or 2"), turned into a real clause through the engine's own
    // line converter off MotelyItem identity — no second grammar.
    private static IJamlClause ParseClauseSource(ClauseSource source) =>
        source.Line is { } line ? ParseLineClause(line) : ParseClause(source.Mapping!);

    private static IJamlClause ParseLineClause(string line)
    {
        if (!JummyLine.TryToClause(line, out var clause, out var error))
            throw new InvalidOperationException($"Invalid JAML line '{line}': {error}");
        return clause!;
    }

    private static IJamlClause ParseClause(NodeReader node)
    {
        var discriminator =
            FindDiscriminator(node)
            ?? throw new InvalidOperationException(
                $"Clause has no recognised discriminator key. Keys: {string.Join(", ", node.Keys)}."
            );

        var value = node.GetObject(discriminator);
        ValidateClauseKeys(discriminator, node, value);
        IReader data = value is null ? node : new OverlayReader(value, node);
        var antes = data.GetIntArray("antes") ?? data.GetIntArray("ante") ?? [];
        var min = data.GetInt("min") ?? 1;
        var max = data.GetInt("max");
        var score = data.GetInt("score") ?? 0;
        var label = data.GetString("label");

        switch (Normalize(discriminator))
        {
            case "and":
                return ParseLogic(
                    new AndClause(),
                    data,
                    discriminator,
                    antes,
                    min,
                    max,
                    score,
                    label
                );
            case "or":
                return ParseLogic(
                    new OrClause(),
                    data,
                    discriminator,
                    antes,
                    min,
                    max,
                    score,
                    label
                );
            case "joker":
            case "jokers":
                return BuildJoker(node, discriminator, data, antes, min, max, score, label);
            case "commonjoker":
            case "commonjokers":
                return BuildCommonJoker(node, discriminator, data, antes, min, max, score, label);
            case "uncommonjoker":
            case "uncommonjokers":
                return BuildUncommonJoker(node, discriminator, data, antes, min, max, score, label);
            case "rarejoker":
            case "rarejokers":
                return BuildRareJoker(node, discriminator, data, antes, min, max, score, label);
            case "legendaryjoker":
            case "legendaryjokers":
                return BuildLegendaryJoker(
                    node,
                    discriminator,
                    data,
                    antes,
                    min,
                    max,
                    score,
                    label
                );
            case "voucher":
                return WithMax(
                    new VoucherClause
                    {
                        Vouchers = ParseEnumArray<MotelyVoucher>(node, discriminator),
                        Rolls = data.GetIntArray("rolls") ?? [0],
                        Antes = antes,
                        Min = min,
                        Score = score,
                        Label = label,
                    },
                    max
                );
            case "tarotcard":
                return WithMax(
                    new TarotCardClause
                    {
                        Tarots = ParseEnumArray<MotelyTarotCard>(node, discriminator),
                        Sources = ParseTarotSources(data),
                        Antes = antes,
                        Min = min,
                        Score = score,
                        Label = label,
                    },
                    max
                );
            case "spectralcard":
                return WithMax(
                    new SpectralCardClause
                    {
                        Spectrals = ParseEnumArray<MotelySpectralCard>(node, discriminator),
                        Sources = ParseSpectralSources(data),
                        Antes = antes,
                        Min = min,
                        Score = score,
                        Label = label,
                    },
                    max
                );
            case "planetcard":
                return WithMax(
                    new PlanetCardClause
                    {
                        Planets = ParseEnumArray<MotelyPlanetCard>(node, discriminator),
                        Sources = ParsePlanetSources(data),
                        Antes = antes,
                        Min = min,
                        Score = score,
                        Label = label,
                    },
                    max
                );
            case "standardcard":
                return BuildStandardCard(data, antes, min, max, score, label);
            case "boss":
                return WithMax(
                    new BossClause
                    {
                        Bosses = ParseEnumArray<MotelyBossBlind>(node, discriminator),
                        Antes = antes,
                        Min = min,
                        Score = score,
                        Label = label,
                    },
                    max
                );
            case "tag":
                return WithMax(
                    new TagClause
                    {
                        Tags = ParseEnumArray<MotelyTag>(node, discriminator),
                        Rolls = data.GetIntArray("rolls") ?? [0, 1],
                        Antes = antes,
                        Min = min,
                        Score = score,
                        Label = label,
                    },
                    max
                );
            case "smallblindtag":
                return WithMax(
                    new TagClause
                    {
                        Tags = ParseEnumArray<MotelyTag>(node, discriminator),
                        Rolls = data.GetIntArray("rolls") ?? [0],
                        Antes = antes,
                        Min = min,
                        Score = score,
                        Label = label,
                    },
                    max
                );
            case "bigblindtag":
                return WithMax(
                    new TagClause
                    {
                        Tags = ParseEnumArray<MotelyTag>(node, discriminator),
                        Rolls = data.GetIntArray("rolls") ?? [1],
                        Antes = antes,
                        Min = min,
                        Score = score,
                        Label = label,
                    },
                    max
                );
            case "erraticrank":
                return WithMax(
                    new ErraticRankClause
                    {
                        Rank = ParseRank(
                            ScalarValue(node, discriminator) ?? throw MissingValue(discriminator)
                        ),
                        Antes = antes,
                        Min = min,
                        Score = score,
                        Label = label,
                    },
                    max
                );
            case "erraticranks":
                return WithMax(
                    new OrClause
                    {
                        Clauses = ParseStringArray(node, discriminator)
                            .Select(v =>
                                (IJamlClause)
                                    new ErraticRankClause
                                    {
                                        Rank = ParseRank(v),
                                        Antes = antes,
                                        Min = 1,
                                    }
                            )
                            .ToArray(),
                        Min = 1,
                        Score = score,
                        Label = label,
                    },
                    max
                );
            case "erraticsuit":
                return WithMax(
                    new ErraticSuitClause
                    {
                        Suit = ParseEnum<MotelyStandardcardSuit>(
                            ScalarValue(node, discriminator) ?? throw MissingValue(discriminator)
                        ),
                        Antes = antes,
                        Min = min,
                        Score = score,
                        Label = label,
                    },
                    max
                );
            case "startingdraw":
                return BuildStartingDraw(data, antes, min, max, score, label);
            case "luckymoney":
                return WithMax(
                    new LuckyMoneyClause
                    {
                        Rolls = node.GetIntArray(discriminator) ?? [],
                        With = ParseWith(data),
                        Min = min,
                        Score = score,
                        Label = label,
                    },
                    max
                );
            case "luckymult":
                return WithMax(
                    new LuckyMultClause
                    {
                        Rolls = node.GetIntArray(discriminator) ?? [],
                        With = ParseWith(data),
                        Min = min,
                        Score = score,
                        Label = label,
                    },
                    max
                );
            case "misprintmult":
                return WithMax(
                    new MisprintMultClause
                    {
                        Rolls = node.GetIntArray(discriminator) ?? [],
                        Mult = data.GetInt("mult") ?? data.GetInt("value") ?? 0,
                        Min = min,
                        Score = score,
                        Label = label,
                    },
                    max
                );
            case "wheeloffortune":
                return WithMax(
                    new WheelOfFortuneClause
                    {
                        Rolls = node.GetIntArray(discriminator) ?? [],
                        With = ParseWith(data),
                        Min = min,
                        Score = score,
                        Label = label,
                    },
                    max
                );
            case "grosmichelextinct":
                return WithMax(
                    new GrosMichelExtinctClause
                    {
                        Rolls = node.GetIntArray(discriminator) ?? [],
                        With = ParseWith(data),
                        Min = min,
                        Score = score,
                        Label = label,
                    },
                    max
                );
            case "cavendishextinct":
                return WithMax(
                    new CavendishExtinctClause
                    {
                        Rolls = node.GetIntArray(discriminator) ?? [],
                        With = ParseWith(data),
                        Min = min,
                        Score = score,
                        Label = label,
                    },
                    max
                );
            case "spacelevelup":
                return WithMax(
                    new SpaceLevelupClause
                    {
                        Rolls = node.GetIntArray(discriminator) ?? [],
                        With = ParseWith(data),
                        Min = min,
                        Score = score,
                        Label = label,
                    },
                    max
                );
            case "businesspayout":
                return WithMax(
                    new BusinessPayoutClause
                    {
                        Rolls = node.GetIntArray(discriminator) ?? [],
                        Min = min,
                        Score = score,
                        Label = label,
                    },
                    max
                );
            case "bloodstonetrigger":
                return WithMax(
                    new BloodstoneTriggerClause
                    {
                        Rolls = node.GetIntArray(discriminator) ?? [],
                        Min = min,
                        Score = score,
                        Label = label,
                    },
                    max
                );
            case "parkingpayout":
                return WithMax(
                    new ParkingPayoutClause
                    {
                        Rolls = node.GetIntArray(discriminator) ?? [],
                        Min = min,
                        Score = score,
                        Label = label,
                    },
                    max
                );
            case "glassdestroy":
                return WithMax(
                    new GlassDestroyClause
                    {
                        Rolls = node.GetIntArray(discriminator) ?? [],
                        With = ParseWith(data),
                        Min = min,
                        Score = score,
                        Label = label,
                    },
                    max
                );
            case "wheelstaysflipped":
                return WithMax(
                    new WheelStaysFlippedClause
                    {
                        Rolls = node.GetIntArray(discriminator) ?? [],
                        With = ParseWith(data),
                        Min = min,
                        Score = score,
                        Label = label,
                    },
                    max
                );
            default:
                throw new InvalidOperationException(
                    $"Unhandled JAML discriminator '{discriminator}'."
                );
        }
    }

    private static IJamlClause ParseLogic(
        LogicClause clause,
        IReader data,
        string discriminator,
        int[] antes,
        int min,
        int? max,
        int score,
        string? label
    )
    {
        var sources = data.GetClauseList("clauses") ?? data.GetClauseList(discriminator) ?? [];
        var children = sources.Select(ParseClauseSource).ToArray();
        HoistAntes(children, antes);
        clause.Clauses = children;
        clause.Min = min;
        clause.Max = max;
        clause.Score = score;
        clause.Label = label;
        return clause;
    }

    private static JokerClause BuildJoker(
        NodeReader node,
        string discriminator,
        IReader data,
        int[] antes,
        int min,
        int? max,
        int score,
        string? label
    )
    {
        var jokers = ParseJokerArray<MotelyJoker>(node, discriminator, out var any);
        return WithMax(
            new JokerClause
            {
                Jokers = jokers,
                IsWildcard = any,
                Edition = ParseOptionalEnum<MotelyItemEdition>(data.GetString("edition")),
                Stickers = ParseEnumArray<MotelyJokerSticker>(data, "stickers", allowMissing: true),
                Sources = ParseJokerSources(data),
                Antes = antes,
                Min = min,
                Score = score,
                Label = label,
            },
            max
        );
    }

    private static CommonJokerClause BuildCommonJoker(
        NodeReader node,
        string discriminator,
        IReader data,
        int[] antes,
        int min,
        int? max,
        int score,
        string? label
    )
    {
        var jokers = ParseJokerArray<MotelyJokerCommon>(node, discriminator, out var any);
        return WithMax(
            new CommonJokerClause
            {
                Jokers = jokers,
                IsWildcard = any,
                Edition = ParseOptionalEnum<MotelyItemEdition>(data.GetString("edition")),
                Stickers = ParseEnumArray<MotelyJokerSticker>(data, "stickers", allowMissing: true),
                Sources = ParseJokerSources(data),
                Antes = antes,
                Min = min,
                Score = score,
                Label = label,
            },
            max
        );
    }

    private static UncommonJokerClause BuildUncommonJoker(
        NodeReader node,
        string discriminator,
        IReader data,
        int[] antes,
        int min,
        int? max,
        int score,
        string? label
    )
    {
        var jokers = ParseJokerArray<MotelyJokerUncommon>(node, discriminator, out var any);
        return WithMax(
            new UncommonJokerClause
            {
                Jokers = jokers,
                IsWildcard = any,
                Edition = ParseOptionalEnum<MotelyItemEdition>(data.GetString("edition")),
                Stickers = ParseEnumArray<MotelyJokerSticker>(data, "stickers", allowMissing: true),
                Sources = ParseJokerSources(data),
                Antes = antes,
                Min = min,
                Score = score,
                Label = label,
            },
            max
        );
    }

    private static RareJokerClause BuildRareJoker(
        NodeReader node,
        string discriminator,
        IReader data,
        int[] antes,
        int min,
        int? max,
        int score,
        string? label
    )
    {
        var jokers = ParseJokerArray<MotelyJokerRare>(node, discriminator, out var any);
        return WithMax(
            new RareJokerClause
            {
                Jokers = jokers,
                IsWildcard = any,
                Edition = ParseOptionalEnum<MotelyItemEdition>(data.GetString("edition")),
                Stickers = ParseEnumArray<MotelyJokerSticker>(data, "stickers", allowMissing: true),
                Sources = ParseJokerSources(data),
                Antes = antes,
                Min = min,
                Score = score,
                Label = label,
            },
            max
        );
    }

    private static LegendaryJokerClause BuildLegendaryJoker(
        NodeReader node,
        string discriminator,
        IReader data,
        int[] antes,
        int min,
        int? max,
        int score,
        string? label
    )
    {
        var jokers = ParseJokerArray<MotelyJoker>(node, discriminator, out var any);
        return WithMax(
            new LegendaryJokerClause
            {
                Jokers = jokers,
                IsWildcard = any,
                Edition = ParseOptionalEnum<MotelyItemEdition>(data.GetString("edition")),
                Sources = ParseLegendarySources(data) ?? new LegendaryJokerSourceConfig(),
                SoulCardOnly = data.GetBool("soulCardOnly") ?? false,
                SoulEditionRolls = data.GetInt("soulEditionRolls") ?? 0,
                Antes = antes,
                Min = min,
                Score = score,
                Label = label,
            },
            max
        );
    }

    private static StandardCardClause BuildStandardCard(
        IReader data,
        int[] antes,
        int min,
        int? max,
        int score,
        string? label
    ) =>
        WithMax(
            new StandardCardClause
            {
                Rank = ParseOptionalRank(data.GetString("rank")),
                Suit = ParseOptionalEnum<MotelyStandardcardSuit>(data.GetString("suit")),
                Enhancement = ParseOptionalEnum<MotelyItemEnhancement>(
                    data.GetString("enhancement")
                ),
                Seal = ParseOptionalEnum<MotelyItemSeal>(data.GetString("seal")),
                Edition = ParseOptionalEnum<MotelyItemEdition>(data.GetString("edition")),
                Sources = ParseStandardSources(data),
                Antes = antes,
                Min = min,
                Score = score,
                Label = label,
            },
            max
        );

    private static StartingDrawClause BuildStartingDraw(
        IReader data,
        int[] antes,
        int min,
        int? max,
        int score,
        string? label
    ) =>
        WithMax(
            new StartingDrawClause
            {
                Rank = ParseOptionalRank(data.GetString("rank")),
                Suit = ParseOptionalEnum<MotelyStandardcardSuit>(data.GetString("suit")),
                Antes = antes,
                Min = min,
                Score = score,
                Label = label,
            },
            max
        );

    private static void HoistAntes(IJamlClause[] clauses, int[] antes)
    {
        if (antes.Length == 0)
            return;
        foreach (var clause in clauses)
        {
            if (clause is IAnteScopedClause { Antes.Length: 0 } anteScoped)
                anteScoped.Antes = antes;
            else if (clause is LogicClause logic)
                HoistAntes(logic.Clauses, antes);
        }
    }

    private static void ValidateClauseKeys(string discriminator, IReader outer, IReader? inner)
    {
        var allowed = ClauseKeys(discriminator);
        ValidateKeys(outer, [.. SharedClauseKeys, .. allowed, .. AllDiscriminatorKeys()], "clause");
        if (inner != null)
            ValidateKeys(inner, [.. SharedClauseKeys, .. allowed], $"'{discriminator}' block");
    }

    private static string[] ClauseKeys(string discriminator) =>
        Normalize(discriminator) switch
        {
            "and" or "or" => LogicKeys,
            "joker"
            or "jokers"
            or "commonjoker"
            or "commonjokers"
            or "uncommonjoker"
            or "uncommonjokers"
            or "rarejoker"
            or "rarejokers" => JokerClauseKeys,
            "legendaryjoker" or "legendaryjokers" => LegendaryClauseKeys,
            "voucher" => ["voucher", "rolls"],
            "tarotcard" => ["tarotCard", "shopItems", "boosterPacks"],
            "spectralcard" => ["spectralCard", "shopItems", "boosterPacks"],
            "planetcard" => ["planetCard", "shopItems", "boosterPacks"],
            "standardcard" => StandardCardKeys,
            "boss" => ["boss"],
            "tag" => ["tag", "rolls"],
            "smallblindtag" => ["smallBlindTag", "rolls"],
            "bigblindtag" => ["bigBlindTag", "rolls"],
            "erraticrank" => ["erraticRank"],
            "erraticranks" => ["erraticRanks"],
            "erraticsuit" => ["erraticSuit"],
            "startingdraw" => StartingDrawKeys,
            _ => EventKeys,
        };

    private static string[] AllDiscriminatorKeys() =>
        [
            "and",
            "or",
            "joker",
            "jokers",
            "commonJoker",
            "commonJokers",
            "uncommonJoker",
            "uncommonJokers",
            "rareJoker",
            "rareJokers",
            "legendaryJoker",
            "legendaryJokers",
            "voucher",
            "tarotCard",
            "spectralCard",
            "planetCard",
            "standardCard",
            "boss",
            "tag",
            "smallBlindTag",
            "bigBlindTag",
            "erraticRank",
            "erraticRanks",
            "erraticSuit",
            "startingDraw",
            "luckyMoney",
            "luckyMult",
            "misprintMult",
            "wheelOfFortune",
            "grosMichelExtinct",
            "cavendishExtinct",
            "spaceLevelup",
            "businessPayout",
            "bloodstoneTrigger",
            "parkingPayout",
            "glassDestroy",
            "wheelStaysFlipped",
        ];

    private static void ValidateKeys(IReader reader, string[] allowed, string scope)
    {
        foreach (var key in reader.Keys)
        {
            if (!allowed.Any(a => string.Equals(a, key, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"Unknown {scope} key: '{key}'.");
        }
    }

    private static JamlWith ParseWith(IReader data)
    {
        var with = data.GetObject("with");
        var sources = data.GetObject("sources");
        if (with != null)
            ValidateKeys(with, WithKeys, "with");
        if (sources != null)
            ValidateKeys(sources, EventSourceKeys, "event source");
        var luckText =
            with?.GetString("luck") ?? sources?.GetString("luck") ?? data.GetString("luck");
        var luckInt = with?.GetInt("luck") ?? sources?.GetInt("luck") ?? data.GetInt("luck");
        var result = new JamlWith();
        if (luckText != null)
            result.Luck = ParseLuck(luckText);
        else if (luckInt.HasValue)
            result.Luck = ParseLuck(luckInt.Value);
        var vouchers = with?.GetStringArray("vouchers") ?? data.GetStringArray("vouchers");
        if (vouchers != null)
            result.Vouchers = vouchers.Select(ParseEnum<MotelyVoucher>).ToArray();
        return result;
    }

    private static JokerSourceConfig? ParseJokerSources(IReader data)
    {
        var block = data.GetObject("sources");
        if (block != null)
            ValidateKeys(block, JokerSourceKeys, "joker source");
        if (
            block is null
            && data.GetIntArray("shopItems") is null
            && data.GetIntArray("boosterPacks") is null
        )
            return null;
        return new JokerSourceConfig
        {
            ShopItems = block?.GetIntArray("shopItems") ?? data.GetIntArray("shopItems") ?? [],
            BoosterPacks =
                block?.GetIntArray("boosterPacks") ?? data.GetIntArray("boosterPacks") ?? [],
            Judgement = block?.GetIntArray("judgement") ?? [],
            Wraith = block?.GetIntArray("wraith") ?? [],
            RiffRaff = block?.GetIntArray("riffRaff") ?? [],
            RareTag = block?.GetIntArray("rareTag") ?? [],
            UncommonTag = block?.GetIntArray("uncommonTag") ?? [],
            CommonShopJokers = block?.GetIntArray("commonShopJokers") ?? [],
            UncommonShopJokers = block?.GetIntArray("uncommonShopJokers") ?? [],
            RareShopJokers = block?.GetIntArray("rareShopJokers") ?? [],
            AllShopJokers = block?.GetIntArray("allShopJokers") ?? [],
        };
    }

    private static LegendaryJokerSourceConfig? ParseLegendarySources(IReader data)
    {
        var block = data.GetObject("sources");
        if (block is null && data.GetIntArray("boosterPacks") is null)
            return null;
        if (block != null)
            ValidateKeys(block, LegendarySourceKeys, "legendaryJoker source");
        return new LegendaryJokerSourceConfig
        {
            ShopItems = block?.GetIntArray("shopItems") ?? [],
            BoosterPacks =
                block?.GetIntArray("boosterPacks") ?? data.GetIntArray("boosterPacks") ?? [],
            ArcanaPacks = block?.GetIntArray("arcanaPacks") ?? [],
            SpectralPacks = block?.GetIntArray("spectralPacks") ?? [],
            SoulCard = block?.GetIntArray("soulCard") ?? [],
            RequireMegaPack =
                block?.GetBool("requireMega") ?? block?.GetBool("requireMegaPack") ?? false,
        };
    }

    private static TarotCardSourceConfig? ParseTarotSources(IReader data)
    {
        var block = data.GetObject("sources");
        if (block != null)
            ValidateKeys(block, TarotSourceKeys, "tarotCard source");
        if (
            block is null
            && data.GetIntArray("shopItems") is null
            && data.GetIntArray("boosterPacks") is null
        )
            return null;
        return new TarotCardSourceConfig
        {
            ShopItems = block?.GetIntArray("shopItems") ?? data.GetIntArray("shopItems") ?? [],
            BoosterPacks =
                block?.GetIntArray("boosterPacks") ?? data.GetIntArray("boosterPacks") ?? [],
            Emperor = block?.GetIntArray("emperor") ?? [],
            PurpleSealOrEightBall = block?.GetIntArray("purpleSealOrEightBall") ?? [],
            CharmTag = block?.GetBool("charmTag") ?? false,
        };
    }

    private static SpectralCardSourceConfig? ParseSpectralSources(IReader data)
    {
        var block = data.GetObject("sources");
        if (block != null)
            ValidateKeys(block, SpectralSourceKeys, "spectralCard source");
        if (
            block is null
            && data.GetIntArray("shopItems") is null
            && data.GetIntArray("boosterPacks") is null
        )
            return null;
        return new SpectralCardSourceConfig
        {
            ShopItems = block?.GetIntArray("shopItems") ?? data.GetIntArray("shopItems") ?? [],
            BoosterPacks =
                block?.GetIntArray("boosterPacks") ?? data.GetIntArray("boosterPacks") ?? [],
            SixthSense = block?.GetIntArray("sixthSense") ?? [],
            Seance = block?.GetIntArray("seance") ?? [],
            RequireMegaPack =
                block?.GetBool("requireMega") ?? block?.GetBool("requireMegaPack") ?? false,
            EtherealTag = block?.GetBool("etherealTag") ?? false,
        };
    }

    private static PlanetSourceConfig? ParsePlanetSources(IReader data)
    {
        var block = data.GetObject("sources");
        if (block != null)
            ValidateKeys(block, PlanetSourceKeys, "planetCard source");
        if (
            block is null
            && data.GetIntArray("shopItems") is null
            && data.GetIntArray("boosterPacks") is null
        )
            return null;
        return new PlanetSourceConfig
        {
            ShopItems = block?.GetIntArray("shopItems") ?? data.GetIntArray("shopItems") ?? [],
            BoosterPacks =
                block?.GetIntArray("boosterPacks") ?? data.GetIntArray("boosterPacks") ?? [],
        };
    }

    private static StandardCardSourceConfig? ParseStandardSources(IReader data)
    {
        var block = data.GetObject("sources");
        if (block != null)
            ValidateKeys(block, StandardSourceKeys, "standardCard source");
        if (
            block is null
            && data.GetIntArray("shopItems") is null
            && data.GetIntArray("boosterPacks") is null
        )
            return null;
        return new StandardCardSourceConfig
        {
            ShopItems = block?.GetIntArray("shopItems") ?? data.GetIntArray("shopItems") ?? [],
            BoosterPacks =
                block?.GetIntArray("boosterPacks") ?? data.GetIntArray("boosterPacks") ?? [],
            Certificate = block?.GetIntArray("certificate") ?? [],
            Incantation = block?.GetIntArray("incantation") ?? [],
            Familiar = block?.GetIntArray("familiar") ?? [],
            Grim = block?.GetIntArray("grim") ?? [],
            DeckDraw = block?.GetIntArray("deckDraw") ?? [],
        };
    }

    private static TClause WithMax<TClause>(TClause clause, int? max)
        where TClause : IJamlClause
    {
        clause.Max = max;
        return clause;
    }

    private static TEnum[] ParseJokerArray<TEnum>(NodeReader node, string key, out bool any)
        where TEnum : struct, Enum
    {
        var values = ParseStringArray(node, key);
        any = values.Length == 1 && IsAny(values[0]);
        return any ? [] : values.Select(ParseEnum<TEnum>).ToArray();
    }

    private static TEnum[] ParseEnumArray<TEnum>(NodeReader node, string key)
        where TEnum : struct, Enum =>
        ParseStringArray(node, key).Select(ParseEnum<TEnum>).ToArray();

    private static TEnum[] ParseEnumArray<TEnum>(IReader node, string key, bool allowMissing)
        where TEnum : struct, Enum
    {
        var values = node.GetStringArray(key);
        if (values is null)
            return allowMissing ? [] : throw MissingValue(key);
        return values.Select(ParseEnum<TEnum>).ToArray();
    }

    private static string[] ParseStringArray(NodeReader node, string key) =>
        node.GetStringArray(key) ?? throw MissingValue(key);

    private static string? ScalarValue(NodeReader node, string key) => node.GetString(key);

    private static Exception MissingValue(string key) =>
        new InvalidOperationException($"'{key}' clause requires a value.");

    private static string? FindDiscriminator(IReader node)
    {
        foreach (var key in node.Keys)
        {
            if (IsDiscriminator(key))
                return key;
        }
        return null;
    }

    private static bool IsDiscriminator(string key) =>
        Normalize(key) switch
        {
            "and"
            or "or"
            or "joker"
            or "jokers"
            or "commonjoker"
            or "commonjokers"
            or "uncommonjoker"
            or "uncommonjokers"
            or "rarejoker"
            or "rarejokers"
            or "legendaryjoker"
            or "legendaryjokers"
            or "voucher"
            or "tarotcard"
            or "spectralcard"
            or "planetcard"
            or "standardcard"
            or "boss"
            or "tag"
            or "smallblindtag"
            or "bigblindtag"
            or "erraticrank"
            or "erraticranks"
            or "erraticsuit"
            or "startingdraw"
            or "luckymoney"
            or "luckymult"
            or "misprintmult"
            or "wheeloffortune"
            or "grosmichelextinct"
            or "cavendishextinct"
            or "spacelevelup"
            or "businesspayout"
            or "bloodstonetrigger"
            or "parkingpayout"
            or "glassdestroy"
            or "wheelstaysflipped" => true,
            _ => false,
        };

    private static MotelyStandardcardRank? ParseOptionalRank(string? value) =>
        value is null ? null : ParseRank(value);

    private static MotelyStandardcardRank ParseRank(string value)
    {
        if (int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var pip))
        {
            return pip switch
            {
                2 => MotelyStandardcardRank.Two,
                3 => MotelyStandardcardRank.Three,
                4 => MotelyStandardcardRank.Four,
                5 => MotelyStandardcardRank.Five,
                6 => MotelyStandardcardRank.Six,
                7 => MotelyStandardcardRank.Seven,
                8 => MotelyStandardcardRank.Eight,
                9 => MotelyStandardcardRank.Nine,
                10 => MotelyStandardcardRank.Ten,
                _ => throw new InvalidOperationException($"Unsupported rank pip value: {pip}."),
            };
        }

        return value.ToUpperInvariant() switch
        {
            "J" => MotelyStandardcardRank.Jack,
            "Q" => MotelyStandardcardRank.Queen,
            "K" => MotelyStandardcardRank.King,
            "A" => MotelyStandardcardRank.Ace,
            _ => ParseEnum<MotelyStandardcardRank>(value),
        };
    }

    private static T? ParseOptionalEnum<T>(string? value)
        where T : struct, Enum => value is null ? null : ParseEnum<T>(value);

    private static T ParseEnum<T>(string value)
        where T : struct, Enum
    {
        if (Enum.TryParse<T>(value, ignoreCase: true, out var parsed))
            return parsed;

        var normalized = value
            .Replace(" ", "", StringComparison.Ordinal)
            .Replace("-", "", StringComparison.Ordinal)
            .Replace("_", "", StringComparison.Ordinal);
        if (Enum.TryParse<T>(normalized, ignoreCase: true, out parsed))
            return parsed;

        throw new InvalidOperationException(
            $"Cannot parse '{value}' as {typeof(T).Name}. Known values: {string.Join(", ", Enum.GetNames<T>())}."
        );
    }

    private static MotelyLuck ParseLuck(string value)
    {
        if (
            int.TryParse(
                value.TrimStart('x', 'X'),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var numeric
            )
        )
            return ParseLuck(numeric);
        return ParseEnum<MotelyLuck>(value);
    }

    private static MotelyLuck ParseLuck(int value) =>
        value switch
        {
            1 => MotelyLuck.X1,
            2 => MotelyLuck.X2,
            4 => MotelyLuck.X4,
            5 => MotelyLuck.X5,
            8 => MotelyLuck.X8,
            16 => MotelyLuck.X16,
            32 => MotelyLuck.X32,
            64 => MotelyLuck.X64,
            _ => throw new InvalidOperationException($"Unsupported luck multiplier: {value}."),
        };

    private static bool IsAny(string value) =>
        string.Equals(value, "any", StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string value) =>
        value
            .Replace(" ", "", StringComparison.Ordinal)
            .Replace("-", "", StringComparison.Ordinal)
            .Replace("_", "", StringComparison.Ordinal)
            .ToLowerInvariant();

    private static string Slugify(string name) => Normalize(name);

    // One entry in a clause list: a structured mapping, or a single-line JAML clause.
    private readonly record struct ClauseSource(NodeReader? Mapping, string? Line);

    private interface IReader
    {
        IReadOnlyList<string> Keys { get; }
        string? GetString(string key);
        int? GetInt(string key);
        bool? GetBool(string key);
        int[]? GetIntArray(string key);
        string[]? GetStringArray(string key);
        IReader? GetObject(string key);
        IReadOnlyList<NodeReader>? GetObjectList(string key);
        IReadOnlyList<ClauseSource>? GetClauseList(string key);
    }

    private sealed class OverlayReader(IReader primary, IReader fallback) : IReader
    {
        public IReadOnlyList<string> Keys =>
            primary.Keys.Concat(fallback.Keys).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        public string? GetString(string key) => primary.GetString(key) ?? fallback.GetString(key);

        public int? GetInt(string key) => primary.GetInt(key) ?? fallback.GetInt(key);

        public bool? GetBool(string key) => primary.GetBool(key) ?? fallback.GetBool(key);

        public int[]? GetIntArray(string key) =>
            primary.GetIntArray(key) ?? fallback.GetIntArray(key);

        public string[]? GetStringArray(string key) =>
            primary.GetStringArray(key) ?? fallback.GetStringArray(key);

        public IReader? GetObject(string key) => primary.GetObject(key) ?? fallback.GetObject(key);

        public IReadOnlyList<NodeReader>? GetObjectList(string key) =>
            primary.GetObjectList(key) ?? fallback.GetObjectList(key);

        public IReadOnlyList<ClauseSource>? GetClauseList(string key) =>
            primary.GetClauseList(key) ?? fallback.GetClauseList(key);
    }

    private sealed class NodeReader : IReader
    {
        private readonly Dictionary<string, YamlElement> _items;

        public NodeReader(YamlMapping mapping)
        {
            _items = new Dictionary<string, YamlElement>(StringComparer.OrdinalIgnoreCase);
            foreach (var key in mapping.Keys)
            {
                if (Scalar(key) is { } name)
                    _items[name] = mapping[key]!;
            }
        }

        public IReadOnlyList<string> Keys => _items.Keys.ToArray();

        public string? GetString(string key) =>
            _items.TryGetValue(key, out var value) ? Scalar(value) : null;

        public int? GetInt(string key) =>
            int.TryParse(
                GetString(key),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var value
            )
                ? value
                : null;

        public bool? GetBool(string key) =>
            bool.TryParse(GetString(key), out var value) ? value : null;

        public int[]? GetIntArray(string key)
        {
            if (!_items.TryGetValue(key, out var value))
                return null;
            if (value is YamlSequence sequence)
                return sequence
                    .Select(item => int.Parse(Scalar(item) ?? "", CultureInfo.InvariantCulture))
                    .ToArray();
            if (
                Scalar(value) is { } scalar
                && int.TryParse(
                    scalar,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var single
                )
            )
                return [single];
            return null;
        }

        public string[]? GetStringArray(string key)
        {
            if (!_items.TryGetValue(key, out var value))
                return null;
            if (value is YamlSequence sequence)
                return sequence.Select(item => Scalar(item) ?? "").ToArray();
            if (Scalar(value) is { } scalar)
                return [scalar];
            return null;
        }

        public IReader? GetObject(string key) =>
            _items.TryGetValue(key, out var value) && value is YamlMapping mapping
                ? new NodeReader(mapping)
                : null;

        public IReadOnlyList<NodeReader>? GetObjectList(string key)
        {
            if (!_items.TryGetValue(key, out var value))
                return null;
            if (value is YamlSequence sequence)
                return sequence
                    .OfType<YamlMapping>()
                    .Select(static item => new NodeReader(item))
                    .ToArray();
            return null;
        }

        // A clause-list entry is either a mapping (structured clause) or a scalar (a single-line
        // JAML clause). Anything else fails loudly — the loader never silently drops a list entry.
        public IReadOnlyList<ClauseSource>? GetClauseList(string key)
        {
            if (!_items.TryGetValue(key, out var value) || value is not YamlSequence sequence)
                return null;
            var items = new List<ClauseSource>();
            foreach (var element in sequence)
            {
                switch (element)
                {
                    case YamlMapping mapping:
                        items.Add(new ClauseSource(new NodeReader(mapping), null));
                        break;
                    case YamlValue { Value: { } raw }:
                        items.Add(new ClauseSource(null, raw.ToString()));
                        break;
                    default:
                        throw new InvalidOperationException(
                            $"Clause list '{key}' has an entry that is neither a clause mapping nor a JUMMY line."
                        );
                }
            }
            return items;
        }

        private static string? Scalar(YamlElement element) =>
            element switch
            {
                YamlValue value => value.Value?.ToString(),
                _ => null,
            };
    }
}
