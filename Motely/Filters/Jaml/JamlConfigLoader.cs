using System.Globalization;

namespace Motely.Filters.Jaml;

public static partial class JamlConfigLoader
{
    public static bool TryLoad(string content, out JamlConfig? config, out string? error)
    {
        try
        {
            config = FromJaml(content);
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

    public static JamlConfig FromJaml(string content)
    {
        try
        {
            var root = JamlDocumentParser.ParseJaml(content);
            return ParseConfig(new NodeReader(root));
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"JAML parse error: {ex.Message}", ex);
        }
    }

    public static LegendaryJokerSourceConfig CreateLegendaryJokerSources(
        LegendaryJokerSourceConfig? userConfig
    ) => userConfig ?? new LegendaryJokerSourceConfig();

    private static JamlConfig ParseConfig(NodeReader root)
    {
        ValidateKeys(root, JamlConfig.RootKeys, "JAML root");
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
        if (root.GetString("filter") is { Length: > 0 } filterName)
        {
            if (!MotelyNativeFilterNames.TryParse(filterName, out _))
            {
                string valid = string.Join(", ", MotelyNativeFilterNames.DisplayNames);
                throw new InvalidOperationException(
                    $"Unknown native filter '{filterName}'. Valid filters: {valid}"
                );
            }
            config.Filter = filterName;
        }
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
        if (!JamlLine.TryToClause(line, out var clause, out var error))
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
        // An unspecified score is worth 1, not 0 — a should clause you bothered to write should
        // count for something. Explicit scores (including negative penalties) still win; this
        // only fills the blank. Defaulting to 0 silently made unscored should clauses contribute
        // nothing, the bug that zeroed whole filters for ~10 months.
        var score = data.GetInt("score") ?? 1;
        var label = data.GetString("label");

        // Families migrated to the sane path build themselves off their descriptor; the switch
        // below is the shrinking legacy fallback for the ones not yet moved.
        if (DescBuilders.TryGetValue(Normalize(discriminator), out var descBuild))
            return descBuild(discriminator, node, data, antes, min, max, score, label);

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
                return PopulateJokerFamily<MotelyJoker>(discriminator, node, data, antes, min, max, score, label);
            case "commonjoker":
            case "commonjokers":
                return PopulateJokerFamily<MotelyJokerCommon>(discriminator, node, data, antes, min, max, score, label);
            case "uncommonjoker":
            case "uncommonjokers":
                return PopulateJokerFamily<MotelyJokerUncommon>(discriminator, node, data, antes, min, max, score, label);
            case "rarejoker":
            case "rarejokers":
                return PopulateJokerFamily<MotelyJokerRare>(discriminator, node, data, antes, min, max, score, label);
            case "legendaryjoker":
            case "legendaryjokers":
                return PopulateJokerFamily<MotelyJoker>(discriminator, node, data, antes, min, max, score, label);
            case "voucher":
            case "vouchers":
                return PopulateAndCast<VoucherClause>(discriminator, node, data, antes, min, max, score, label);
            case "tarotcard":
            case "tarotcards":
                return PopulateAndCast<TarotCardClause>(discriminator, node, data, antes, min, max, score, label);
            case "spectralcard":
            case "spectralcards":
                return PopulateAndCast<SpectralCardClause>(discriminator, node, data, antes, min, max, score, label);
            case "planetcard":
            case "planetcards":
                return PopulateAndCast<PlanetCardClause>(discriminator, node, data, antes, min, max, score, label);
            case "standardcard":
            case "standardcards":
                return PopulateAndCast<StandardCardClause>(
                    discriminator, node, data, antes, min, max, score, label, applyDiscriminatorValue: false);
            case "boss":
            case "bosses":
                return PopulateAndCast<BossClause>(discriminator, node, data, antes, min, max, score, label);
            case "tag":
            case "tags":
            case "smallblindtag":
            case "bigblindtag":
                return PopulateAndCast<TagClause>(discriminator, node, data, antes, min, max, score, label);
            case "erraticrank":
                return PopulateAndCast<ErraticRankClause>(discriminator, node, data, antes, min, max, score, label);
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
                        // How many of the listed erratic ranks must appear (child Min=1 is
                        // per-rank: each just needs one occurrence). Honor an explicit min:.
                        Min = min,
                        Score = score,
                        Label = label,
                    },
                    max
                );
            case "erraticsuit":
            case "erraticsuits":
                return PopulateAndCast<ErraticSuitClause>(discriminator, node, data, antes, min, max, score, label);
            case "startingdraw":
                return PopulateAndCast<StartingDrawClause>(
                    discriminator, node, data, antes, min, max, score, label, applyDiscriminatorValue: false);
            case "luckymoney":
            case "luckymult":
            case "misprintmult":
            case "wheeloffortune":
            case "grosmichelextinct":
            case "cavendishextinct":
            case "spacelevelup":
            case "businesspayout":
            case "bloodstonetrigger":
            case "parkingpayout":
            case "glassdestroy":
            case "wheelstaysflipped":
                return Populate(discriminator, node, data, antes, min, max, score, label);
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

    // Allowed keys = FilterDesc.ClauseKeys (via generated JamlSchema) plus every wire
    // discriminator so the outer map may carry the disc key itself.
    private static void ValidateClauseKeys(string discriminator, IReader outer, IReader? inner)
    {
        var allowed = JamlSchema.ClauseKeysFor(discriminator);
        ValidateKeys(outer, [.. allowed, .. JamlSchema.Discriminators], "clause");
        if (inner != null)
            ValidateKeys(inner, allowed, $"'{discriminator}' block");
    }

    private static void ValidateKeys(IReader reader, IEnumerable<string> allowed, string scope)
    {
        foreach (var key in reader.Keys)
        {
            if (!allowed.Any(a => string.Equals(a, key, StringComparison.OrdinalIgnoreCase)))
                // The parser tracked where this key sits; hand that span to the diagnostic
                // rather than making an editor rediscover the typo by string-searching.
                throw new JamlSemanticException($"Unknown {scope} key: '{key}'.", reader.KeySpan(key));
        }
    }

    // Luck/vouchers live under `with: { luck, vouchers }`. Bare `luck:`/`vouchers:` and
    // `sources: {luck}` are unknown keys and die in ValidateClauseKeys before this runs.
    private static JamlWith ParseWith(IReader data)
    {
        var with = data.GetObject("with");
        if (with == null)
            return new JamlWith();

        ValidateKeys(with, JamlClause.WithBlockKeys, "with");
        var result = new JamlWith();
        if (with.GetString("luck") is { } luckText)
            result.Luck = ParseLuck(luckText);
        else if (with.GetInt("luck") is { } luckInt)
            result.Luck = ParseLuck(luckInt);
        if (with.GetStringArray("vouchers") is { } vouchers)
            result.Vouchers = vouchers.Select(ParseEnum<MotelyVoucher>).ToArray();
        return result;
    }

    private static TClause WithMax<TClause>(TClause clause, int? max)
        where TClause : IJamlClause
    {
        clause.Max = max;
        return clause;
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

    // Generated JamlSchema is the one wire list — a hand copy here is how plurals once
    // silently stopped parsing.
    private static bool IsDiscriminator(string key) =>
        JamlSchema.IsKnownDiscriminator(key);

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
        JamlSpan KeySpan(string key);
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

        // The key belongs to whichever reader actually wrote it down; prefer the primary's span
        // and fall back only when the primary never saw the key (empty span).
        public JamlSpan KeySpan(string key) =>
            primary.KeySpan(key) is { IsEmpty: false } span ? span : fallback.KeySpan(key);

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

    // Backed by JAML's own tree (JMap/JSeq/JScalar from JamlDocumentParser).
    private sealed class NodeReader : IReader
    {
        private readonly JMap _map;

        public NodeReader(JMap map) => _map = map;

        public IReadOnlyList<string> Keys => _map.Keys;

        public JamlSpan KeySpan(string key) => _map.KeySpan(key);

        public string? GetString(string key) => Scalar(_map.Get(key));

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
            var value = _map.Get(key);
            if (value is null)
                return null;
            if (value is JSeq sequence)
                return sequence.Items
                    .SelectMany(item => ParseIntOrRange(Scalar(item) ?? "", key))
                    .ToArray();
            if (Scalar(value) is { } scalar)
                return ParseIntOrRange(scalar, key).ToArray();
            return null;
        }

        // "1-39" expands to every int 1..39 inclusive, so a clause doesn't need shopItems: [0, 1,
        // 2, ..., 999] spelled out by hand — one range token, or bare "N-M", stands in for the
        // whole list. Plain "N" still parses as a single value, same as before.
        private static readonly System.Text.RegularExpressions.Regex RangePattern =
            new(@"^(\d+)\s*-\s*(\d+)$", System.Text.RegularExpressions.RegexOptions.Compiled);

        private static IEnumerable<int> ParseIntOrRange(string token, string key)
        {
            if (RangePattern.Match(token) is { Success: true } rangeMatch)
            {
                int start = int.Parse(rangeMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                int end = int.Parse(rangeMatch.Groups[2].Value, CultureInfo.InvariantCulture);
                if (end < start)
                    throw new InvalidOperationException(
                        $"'{key}': range '{token}' has end < start."
                    );
                return Enumerable.Range(start, end - start + 1);
            }

            if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var single))
                return [single];

            throw new InvalidOperationException($"'{key}': '{token}' is not a valid integer or range (e.g. '1-39').");
        }

        public string[]? GetStringArray(string key)
        {
            var value = _map.Get(key);
            if (value is null)
                return null;
            if (value is JSeq sequence)
                return sequence.Items.Select(item => Scalar(item) ?? "").ToArray();
            if (Scalar(value) is { } scalar)
                return [scalar];
            return null;
        }

        public IReader? GetObject(string key) =>
            _map.Get(key) is JMap map ? new NodeReader(map) : null;

        public IReadOnlyList<NodeReader>? GetObjectList(string key)
        {
            var value = _map.Get(key);
            if (value is null)
                return null;
            if (value is JSeq sequence)
                return sequence.Items
                    .OfType<JMap>()
                    .Select(static item => new NodeReader(item))
                    .ToArray();
            return null;
        }

        // A clause-list entry is either a mapping (structured clause) or a scalar (a single-line
        // JAML clause). Anything else fails loudly — the loader never silently drops a list entry.
        public IReadOnlyList<ClauseSource>? GetClauseList(string key)
        {
            if (_map.Get(key) is not JSeq sequence)
                return null;
            var items = new List<ClauseSource>();
            foreach (var element in sequence.Items)
            {
                switch (element)
                {
                    case JMap map:
                        items.Add(new ClauseSource(new NodeReader(map), null));
                        break;
                    case JScalar { Value: { } raw }:
                        items.Add(new ClauseSource(null, raw));
                        break;
                    default:
                        throw new InvalidOperationException(
                            $"Clause list '{key}' has an entry that is neither a clause mapping nor a one-line clause."
                        );
                }
            }
            return items;
        }

        private static string? Scalar(JNode? element) =>
            element switch
            {
                JScalar value => value.Value,
                _ => null,
            };
    }
}
