using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using YamlDotNet.RepresentationModel;

namespace Motely.Filters.Jaml;

/// <summary>
/// Parses JAML YAML/JSON text into <see cref="JamlConfig"/>.
/// No defaults are injected — FilterDesc implementations own their own fallback behaviour.
/// </summary>
public static class JamlConfigLoader
{
    // ── Allow-lists — DERIVED from JamlVocab, the single source of truth ───────
    // Do NOT hand-maintain these. JamlVocab feeds BOTH the engine parser (here)
    // and the generated editor tooling (Motely.Schema → generated.ts /
    // jaml.schema.json / jaml.tmLanguage.json). Sourcing them from one place is
    // what makes the editor and the engine physically incapable of disagreeing
    // about which JAML is legal.

    private static HashSet<string> VocabSet(IEnumerable<string> keys) =>
        new(keys, StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> KnownRootKeys = VocabSet(JamlVocab.RootKeys);

    private static readonly HashSet<string> AllDiscriminators = VocabSet(JamlVocab.Discriminators);

    // Logic blocks (and/or) own a `clauses` list on top of the shared modifiers.
    private static readonly HashSet<string> LogicBlockKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "clauses", "ante", "antes", "min", "max", "score", "label",
    };

    // Every non-discriminator clause-level key, unioned across all discriminators.
    private static readonly HashSet<string> KnownClauseLevelKeys =
        VocabSet(JamlVocab.DiscriminatorClauseKeys.Values.SelectMany(keys => keys));

    // ── Per-discriminator source allow-lists (one canonical entry per shape) ───
    private static readonly HashSet<string> JokerSourceKeys     = VocabSet(JamlVocab.DiscriminatorSourceKeys["joker"]);
    private static readonly HashSet<string> LegendarySourceKeys = VocabSet(JamlVocab.DiscriminatorSourceKeys["legendaryJoker"]);
    private static readonly HashSet<string> TarotSourceKeys     = VocabSet(JamlVocab.DiscriminatorSourceKeys["tarotCard"]);
    private static readonly HashSet<string> SpectralSourceKeys  = VocabSet(JamlVocab.DiscriminatorSourceKeys["spectralCard"]);
    private static readonly HashSet<string> PlanetSourceKeys    = VocabSet(JamlVocab.DiscriminatorSourceKeys["planetCard"]);
    private static readonly HashSet<string> StandardSourceKeys  = VocabSet(JamlVocab.DiscriminatorSourceKeys["standardCard"]);
    private static readonly HashSet<string> EventSourceKeys     = VocabSet(JamlVocab.DiscriminatorSourceKeys["luckyMoney"]);

    // ── Public API ────────────────────────────────────────────────────────────

    public static bool TryLoad(string content, out JamlConfig? config, out string? error)
    {
        try
        {
            config = ParseFromYaml(content);
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
            return ParseFromYaml(content);
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
            config = ParseFromJson(content);
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
            return ParseFromJson(content);
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

    /// <summary>
    /// Returns a <see cref="LegendaryJokerSourceConfig"/> from the caller-supplied config, used by
    /// NegativeSoulJokerFilters and similar helpers that need a fully-resolved source config.
    /// </summary>
    public static LegendaryJokerSourceConfig CreateLegendaryJokerSources(
        LegendaryJokerSourceConfig? userConfig
    ) => userConfig ?? new LegendaryJokerSourceConfig();

    // ── YAML parsing ──────────────────────────────────────────────────────────

    private static JamlConfig ParseFromYaml(string content)
    {
        var stream = new YamlStream();
        try
        {
            stream.Load(new StringReader(content));
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"YAML parse error: {ex.Message}", ex);
        }

        if (stream.Documents.Count == 0)
            throw new InvalidOperationException("YAML document is empty.");

        if (stream.Documents[0].RootNode is not YamlMappingNode root)
            throw new InvalidOperationException("JAML root must be a mapping.");

        return ParseConfig(new YamlPropsReader(root));
    }

    // ── JSON parsing ──────────────────────────────────────────────────────────

    private static JamlConfig ParseFromJson(string content)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(content);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"JSON parse error: {ex.Message}", ex);
        }

        if (doc.RootElement.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("JAML root must be a JSON object.");

        return ParseConfig(new JsonPropsReader(doc.RootElement));
    }

    // ── Shared config builder ─────────────────────────────────────────────────

    private static JamlConfig ParseConfig(PropsReader root)
    {
        foreach (var key in root.GetKeys())
            if (!KnownRootKeys.Contains(key))
                throw new InvalidOperationException($"Unknown JAML root key: '{key}'.");

        var name = root.GetScalar("name");
        var id = root.GetScalar("id") ?? SlugifyName(name ?? "unnamed");

        var config = new JamlConfig { Id = id, Name = name };
        config.Description = root.GetScalar("description");
        config.Author = root.GetScalar("author");

        var deckStr = root.GetScalar("deck");
        if (deckStr != null)
            config.Deck = ParseEnumWithFallback<MotelyDeck>(deckStr);

        var stakeStr = root.GetScalar("stake");
        if (stakeStr != null)
            config.Stake = ParseEnumWithFallback<MotelyStake>(stakeStr);

        var seeds = root.GetStringArray("seeds");
        if (seeds != null)
            config.Seeds.AddRange(seeds);

        config.Must.AddRange(ParseClauseList(root, "must"));
        config.Should.AddRange(ParseClauseList(root, "should"));
        config.MustNot.AddRange(ParseClauseList(root, "mustNot"));

        return config;
    }

    private static List<JamlClauseBase> ParseClauseList(PropsReader root, string key)
    {
        var readers = root.GetObjectList(key);
        if (readers == null)
            return [];

        var result = new List<JamlClauseBase>();
        foreach (var reader in readers)
            result.AddRange(ParseClause(reader));
        return result;
    }

    // ── Clause dispatch ───────────────────────────────────────────────────────

    private static IEnumerable<JamlClauseBase> ParseClause(PropsReader props)
    {
        string? discriminator = null;
        foreach (var k in props.GetKeys())
        {
            if (AllDiscriminators.Contains(k))
            {
                discriminator = k;
                break;
            }
        }

        if (discriminator == null)
            throw new InvalidOperationException(
                $"Clause has no recognised discriminator key. Keys: {string.Join(", ", props.GetKeys())}."
            );

        // Reject unknown keys
        foreach (var k in props.GetKeys())
        {
            if (!AllDiscriminators.Contains(k) && !KnownClauseLevelKeys.Contains(k))
                throw new InvalidOperationException($"Unknown clause key '{k}'.");
        }

        var antes = props.GetIntArray("antes")
            ?? (props.GetInt("ante") is int singleAnte ? new[] { singleAnte } : null)
            ?? [];
        int min = props.GetInt("min") ?? 1;
        int? max = props.GetInt("max");
        int score = props.GetInt("score") ?? 0;
        string? label = props.GetScalar("label");

        switch (discriminator.ToLowerInvariant())
        {
            // ── Logic clauses ──────────────────────────────────────────────
            case "and":
            {
                var (children, _) = ParseLogicChildren(props, "and", antes);
                var clause = new AndClause { Clauses = children, Min = min, Score = score, Label = label };
                if (max.HasValue) clause.Max = max;
                yield return clause;
                yield break;
            }
            case "or":
            {
                var (children, _) = ParseLogicChildren(props, "or", antes);
                var clause = new OrClause { Clauses = children, Min = min, Score = score, Label = label };
                if (max.HasValue) clause.Max = max;
                yield return clause;
                yield break;
            }

            // ── Joker variants ─────────────────────────────────────────────
            case "joker":
            case "jokers":
            {
                var valScalar = props.GetScalar(discriminator);
                var valArr = props.GetStringArray(discriminator);
                var jokers = ParseJokerList<MotelyJoker>(valScalar, valArr, out bool isWild);
                var sources = new JokerSourceConfig();
                var siShorthand = props.GetIntArray("shopItems");
                if (siShorthand != null) sources.ShopItems = siShorthand;
                var srcBlock = props.GetObject("sources");
                if (srcBlock != null)
                    FillJokerSources(sources, srcBlock, JokerSourceKeys);
                var clause = new JokerClause
                {
                    Jokers = jokers,
                    IsWildcard = isWild,
                    Antes = antes,
                    Min = min,
                    Score = score,
                    Label = label,
                    Sources = sources,
                };
                if (max.HasValue) clause.Max = max;
                clause.Edition = ParseOptionalEnum<MotelyItemEdition>(props.GetScalar("edition"));
                clause.Stickers = ParseStickers(props.GetStringArray("stickers"));
                yield return clause;
                yield break;
            }
            case "commonjoker":
            case "commonjokers":
            {
                var valScalar = props.GetScalar(discriminator);
                var valArr = props.GetStringArray(discriminator);
                var jokers = ParseJokerList<MotelyJokerCommon>(valScalar, valArr, out bool isWild);
                var sources = new JokerSourceConfig();
                var siShorthand = props.GetIntArray("shopItems");
                if (siShorthand != null) sources.ShopItems = siShorthand;
                var srcBlock = props.GetObject("sources");
                if (srcBlock != null)
                    FillJokerSources(sources, srcBlock, JokerSourceKeys);
                var clause = new CommonJokerClause
                {
                    Jokers = jokers,
                    IsWildcard = isWild,
                    Antes = antes,
                    Min = min,
                    Score = score,
                    Label = label,
                    Sources = sources,
                };
                if (max.HasValue) clause.Max = max;
                clause.Edition = ParseOptionalEnum<MotelyItemEdition>(props.GetScalar("edition"));
                clause.Stickers = ParseStickers(props.GetStringArray("stickers"));
                yield return clause;
                yield break;
            }
            case "uncommonjoker":
            case "uncommonjokers":
            {
                var valScalar = props.GetScalar(discriminator);
                var valArr = props.GetStringArray(discriminator);
                var jokers = ParseJokerList<MotelyJokerUncommon>(valScalar, valArr, out bool isWild);
                var sources = new JokerSourceConfig();
                var siShorthand = props.GetIntArray("shopItems");
                if (siShorthand != null) sources.ShopItems = siShorthand;
                var srcBlock = props.GetObject("sources");
                if (srcBlock != null)
                    FillJokerSources(sources, srcBlock, JokerSourceKeys);
                var clause = new UncommonJokerClause
                {
                    Jokers = jokers,
                    IsWildcard = isWild,
                    Antes = antes,
                    Min = min,
                    Score = score,
                    Label = label,
                    Sources = sources,
                };
                if (max.HasValue) clause.Max = max;
                clause.Edition = ParseOptionalEnum<MotelyItemEdition>(props.GetScalar("edition"));
                clause.Stickers = ParseStickers(props.GetStringArray("stickers"));
                yield return clause;
                yield break;
            }
            case "rarejoker":
            case "rarejokers":
            {
                var valScalar = props.GetScalar(discriminator);
                var valArr = props.GetStringArray(discriminator);
                var jokers = ParseJokerList<MotelyJokerRare>(valScalar, valArr, out bool isWild);
                var sources = new JokerSourceConfig();
                var siShorthand = props.GetIntArray("shopItems");
                if (siShorthand != null) sources.ShopItems = siShorthand;
                var srcBlock = props.GetObject("sources");
                if (srcBlock != null)
                    FillJokerSources(sources, srcBlock, JokerSourceKeys);
                var clause = new RareJokerClause
                {
                    Jokers = jokers,
                    IsWildcard = isWild,
                    Antes = antes,
                    Min = min,
                    Score = score,
                    Label = label,
                    Sources = sources,
                };
                if (max.HasValue) clause.Max = max;
                clause.Edition = ParseOptionalEnum<MotelyItemEdition>(props.GetScalar("edition"));
                clause.Stickers = ParseStickers(props.GetStringArray("stickers"));
                yield return clause;
                yield break;
            }
            case "legendaryjoker":
            case "legendaryjokers":
            {
                var valScalar = props.GetScalar(discriminator);
                var valArr = props.GetStringArray(discriminator);
                bool isWild = string.Equals(valScalar, "Any", StringComparison.OrdinalIgnoreCase);
                MotelyJoker[]? jokers = isWild ? null :
                    valArr != null && valArr.Length > 1
                        ? valArr.Select(v => ParseEnumWithFallback<MotelyJoker>(v)).ToArray()
                        : valScalar != null ? [ParseEnumWithFallback<MotelyJoker>(valScalar)] : [];
                var src = new LegendaryJokerSourceConfig();
                var srcBlock = props.GetObject("sources");
                if (srcBlock != null)
                    FillLegendarySources(src, srcBlock);
                var clause = new LegendaryJokerClause
                {
                    Jokers = jokers ?? [],
                    IsWildcard = isWild,
                    Antes = antes,
                    Min = min,
                    Score = score,
                    Label = label,
                    Sources = src,
                };
                if (max.HasValue) clause.Max = max;
                clause.Edition = ParseOptionalEnum<MotelyItemEdition>(props.GetScalar("edition"));
                if (props.GetBool("soulCardOnly") is bool sco) clause.SoulCardOnly = sco;
                if (props.GetInt("soulEditionRolls") is int ser) clause.SoulEditionRolls = ser;
                yield return clause;
                yield break;
            }

            // ── Voucher ────────────────────────────────────────────────────
            case "voucher":
            {
                var val = props.GetScalar(discriminator)
                    ?? throw new InvalidOperationException("voucher clause requires a value.");
                var vouchers = new[] { ParseEnumWithFallback<MotelyVoucher>(val) };
                var clause = new VoucherClause
                {
                    Vouchers = vouchers,
                    Rolls = [0],
                    Antes = antes,
                    Min = min,
                    Score = score,
                    Label = label,
                };
                if (max.HasValue) clause.Max = max;
                yield return clause;
                yield break;
            }

            // ── Tarot ──────────────────────────────────────────────────────
            case "tarotcard":
            {
                var val = props.GetScalar(discriminator)
                    ?? throw new InvalidOperationException("tarotCard clause requires a value.");
                var src = new TarotCardSourceConfig();
                var shorthand = props.GetIntArray("shopItems");
                if (shorthand != null) src.ShopItems = shorthand;
                var srcBlock = props.GetObject("sources");
                if (srcBlock != null)
                    FillTarotSources(src, srcBlock);
                var clause = new TarotCardClause
                {
                    Tarots = [ParseEnumWithFallback<MotelyTarotCard>(val)],
                    Antes = antes,
                    Min = min,
                    Score = score,
                    Label = label,
                    Sources = src,
                };
                if (max.HasValue) clause.Max = max;
                yield return clause;
                yield break;
            }

            // ── Spectral ───────────────────────────────────────────────────
            case "spectralcard":
            {
                var val = props.GetScalar(discriminator)
                    ?? throw new InvalidOperationException("spectralCard clause requires a value.");
                var src = new SpectralCardSourceConfig();
                var shorthand = props.GetIntArray("shopItems");
                if (shorthand != null) src.ShopItems = shorthand;
                var srcBlock = props.GetObject("sources");
                if (srcBlock != null)
                    FillSpectralSources(src, srcBlock);
                var clause = new SpectralCardClause
                {
                    Spectrals = [ParseEnumWithFallback<MotelySpectralCard>(val)],
                    Antes = antes,
                    Min = min,
                    Score = score,
                    Label = label,
                    Sources = src,
                };
                if (max.HasValue) clause.Max = max;
                yield return clause;
                yield break;
            }

            // ── Planet ─────────────────────────────────────────────────────
            case "planetcard":
            {
                var val = props.GetScalar(discriminator)
                    ?? throw new InvalidOperationException("planetCard clause requires a value.");
                var src = new PlanetSourceConfig();
                var shorthand = props.GetIntArray("shopItems");
                if (shorthand != null) src.ShopItems = shorthand;
                var srcBlock = props.GetObject("sources");
                if (srcBlock != null)
                    FillPlanetSources(src, srcBlock);
                var clause = new PlanetCardClause
                {
                    Planets = [ParseEnumWithFallback<MotelyPlanetCard>(val)],
                    Antes = antes,
                    Min = min,
                    Score = score,
                    Label = label,
                    Sources = src,
                };
                if (max.HasValue) clause.Max = max;
                yield return clause;
                yield break;
            }

            // ── StandardCard ───────────────────────────────────────────────
            case "standardcard":
            {
                var src = new StandardCardSourceConfig();
                var siShorthand = props.GetIntArray("shopItems");
                if (siShorthand != null) src.ShopItems = siShorthand;
                var srcBlock = props.GetObject("sources");
                if (srcBlock != null)
                    FillStandardSources(src, srcBlock);
                var clause = new StandardCardClause
                {
                    Antes = antes,
                    Min = min,
                    Score = score,
                    Label = label,
                    Sources = src,
                };
                if (max.HasValue) clause.Max = max;
                var rankStr = props.GetScalar("rank");
                if (rankStr != null) clause.Rank = ParseRank(rankStr);
                var suitStr = props.GetScalar("suit");
                if (suitStr != null) clause.Suit = ParseEnumWithFallback<MotelyStandardcardSuit>(suitStr);
                var enhStr = props.GetScalar("enhancement");
                if (enhStr != null) clause.Enhancement = ParseEnumWithFallback<MotelyItemEnhancement>(enhStr);
                var sealStr = props.GetScalar("seal");
                if (sealStr != null) clause.Seal = ParseEnumWithFallback<MotelyItemSeal>(sealStr);
                clause.Edition = ParseOptionalEnum<MotelyItemEdition>(props.GetScalar("edition"));
                yield return clause;
                yield break;
            }

            // ── Boss ───────────────────────────────────────────────────────
            case "boss":
            {
                var val = props.GetScalar(discriminator)
                    ?? throw new InvalidOperationException("boss clause requires a value.");
                var clause = new BossClause
                {
                    Bosses = [ParseEnumWithFallback<MotelyBossBlind>(val)],
                    Antes = antes,
                    Min = min,
                    Score = score,
                    Label = label,
                };
                if (max.HasValue) clause.Max = max;
                yield return clause;
                yield break;
            }

            // ── Tags ───────────────────────────────────────────────────────
            case "tag":
            {
                var val = props.GetScalar(discriminator)
                    ?? throw new InvalidOperationException("tag clause requires a value.");
                var clause = new TagClause
                {
                    Tags = [ParseEnumWithFallback<MotelyTag>(val)],
                    Rolls = [0, 1],
                    Antes = antes,
                    Min = min,
                    Score = score,
                    Label = label,
                };
                if (max.HasValue) clause.Max = max;
                yield return clause;
                yield break;
            }
            case "smallblindtag":
            {
                var val = props.GetScalar(discriminator)
                    ?? throw new InvalidOperationException("smallBlindTag clause requires a value.");
                var clause = new TagClause
                {
                    Tags = [ParseEnumWithFallback<MotelyTag>(val)],
                    Rolls = [0],
                    Antes = antes,
                    Min = min,
                    Score = score,
                    Label = label,
                };
                if (max.HasValue) clause.Max = max;
                yield return clause;
                yield break;
            }
            case "bigblindtag":
            {
                var val = props.GetScalar(discriminator)
                    ?? throw new InvalidOperationException("bigBlindTag clause requires a value.");
                var clause = new TagClause
                {
                    Tags = [ParseEnumWithFallback<MotelyTag>(val)],
                    Rolls = [1],
                    Antes = antes,
                    Min = min,
                    Score = score,
                    Label = label,
                };
                if (max.HasValue) clause.Max = max;
                yield return clause;
                yield break;
            }

            // ── Erratic ────────────────────────────────────────────────────
            case "erraticrank":
            {
                var val = props.GetScalar(discriminator)
                    ?? throw new InvalidOperationException("erraticRank clause requires a value.");
                var clause = new ErraticRankClause
                {
                    Rank = ParseRank(val),
                    Antes = antes,
                    Min = min,
                    Score = score,
                    Label = label,
                };
                if (max.HasValue) clause.Max = max;
                yield return clause;
                yield break;
            }
            case "erraticranks":
            {
                var vals = props.GetStringArray(discriminator)
                    ?? throw new InvalidOperationException("erraticRanks clause requires an array value.");
                var children = vals
                    .Select(v => (JamlClauseBase)new ErraticRankClause
                    {
                        Rank = ParseRank(v),
                        Antes = antes,
                        Min = min,
                        Score = 0,
                        Label = label,
                    })
                    .ToArray();
                var orClause = new OrClause
                {
                    Clauses = children,
                    Min = 1,
                    Score = score,
                    Label = label,
                };
                if (max.HasValue) orClause.Max = max;
                yield return orClause;
                yield break;
            }
            case "erraticsuit":
            {
                var val = props.GetScalar(discriminator)
                    ?? throw new InvalidOperationException("erraticSuit clause requires a value.");
                var clause = new ErraticSuitClause
                {
                    Suit = ParseEnumWithFallback<MotelyStandardcardSuit>(val),
                    Antes = antes,
                    Min = min,
                    Score = score,
                    Label = label,
                };
                if (max.HasValue) clause.Max = max;
                yield return clause;
                yield break;
            }

            // ── StartingDraw ───────────────────────────────────────────────
            case "startingdraw":
            {
                var clause = new StartingDrawClause
                {
                    Antes = antes,
                    Min = min,
                    Score = score,
                    Label = label,
                };
                if (max.HasValue) clause.Max = max;
                var rankStr = props.GetScalar("rank");
                if (rankStr != null) clause.Rank = ParseRank(rankStr);
                var suitStr = props.GetScalar("suit");
                if (suitStr != null) clause.Suit = ParseEnumWithFallback<MotelyStandardcardSuit>(suitStr);
                yield return clause;
                yield break;
            }

            // ── Event / Roll clauses ───────────────────────────────────────
            case "luckymoney":
            {
                var rolls = props.GetIntArray(discriminator) ?? [];
                var c = new LuckyMoneyClause { Rolls = rolls, Min = min, Score = score, Label = label };
                if (max.HasValue) c.Max = max;
                var src = props.GetObject("sources");
                if (src != null) FillEventSource(c, src);
                yield return c;
                yield break;
            }
            case "luckymult":
            {
                var rolls = props.GetIntArray(discriminator) ?? [];
                var c = new LuckyMultClause { Rolls = rolls, Min = min, Score = score, Label = label };
                if (max.HasValue) c.Max = max;
                var src = props.GetObject("sources");
                if (src != null) FillEventSource(c, src);
                yield return c;
                yield break;
            }
            case "misprintmult":
            {
                var rolls = props.GetIntArray(discriminator) ?? [];
                var c = new MisprintMultClause { Rolls = rolls, Min = min, Score = score, Label = label };
                if (max.HasValue) c.Max = max;
                var valInt = props.GetInt("value");
                if (valInt.HasValue) c.Value = valInt;
                var src = props.GetObject("sources");
                if (src != null) FillEventSource(c, src);
                yield return c;
                yield break;
            }
            case "wheeloffortune":
            {
                var rolls = props.GetIntArray(discriminator) ?? [];
                var c = new WheelOfFortuneClause { Rolls = rolls, Min = min, Score = score, Label = label };
                if (max.HasValue) c.Max = max;
                var src = props.GetObject("sources");
                if (src != null) FillEventSource(c, src);
                yield return c;
                yield break;
            }
            case "grosmichelextinct":
            {
                var rolls = props.GetIntArray(discriminator) ?? [];
                var c = new GrosMichelExtinctClause { Rolls = rolls, Min = min, Score = score, Label = label };
                if (max.HasValue) c.Max = max;
                var src = props.GetObject("sources");
                if (src != null) FillEventSource(c, src);
                yield return c;
                yield break;
            }
            case "cavendishextinct":
            {
                var rolls = props.GetIntArray(discriminator) ?? [];
                var c = new CavendishExtinctClause { Rolls = rolls, Min = min, Score = score, Label = label };
                if (max.HasValue) c.Max = max;
                var src = props.GetObject("sources");
                if (src != null) FillEventSource(c, src);
                yield return c;
                yield break;
            }
            case "spacelevelup":
            {
                var rolls = props.GetIntArray(discriminator) ?? [];
                var c = new SpaceLevelupClause { Rolls = rolls, Min = min, Score = score, Label = label };
                if (max.HasValue) c.Max = max;
                var src = props.GetObject("sources");
                if (src != null) FillEventSource(c, src);
                yield return c;
                yield break;
            }
            case "businesspayout":
            {
                var rolls = props.GetIntArray(discriminator) ?? [];
                var c = new BusinessPayoutClause { Rolls = rolls, Min = min, Score = score, Label = label };
                if (max.HasValue) c.Max = max;
                var src = props.GetObject("sources");
                if (src != null) FillEventSource(c, src);
                yield return c;
                yield break;
            }
            case "bloodstonetrigger":
            {
                var rolls = props.GetIntArray(discriminator) ?? [];
                var c = new BloodstoneTriggerClause { Rolls = rolls, Min = min, Score = score, Label = label };
                if (max.HasValue) c.Max = max;
                var src = props.GetObject("sources");
                if (src != null) FillEventSource(c, src);
                yield return c;
                yield break;
            }
            case "parkingpayout":
            {
                var rolls = props.GetIntArray(discriminator) ?? [];
                var c = new ParkingPayoutClause { Rolls = rolls, Min = min, Score = score, Label = label };
                if (max.HasValue) c.Max = max;
                var src = props.GetObject("sources");
                if (src != null) FillEventSource(c, src);
                yield return c;
                yield break;
            }
            case "glassdestroy":
            {
                var rolls = props.GetIntArray(discriminator) ?? [];
                var c = new GlassDestroyClause { Rolls = rolls, Min = min, Score = score, Label = label };
                if (max.HasValue) c.Max = max;
                var src = props.GetObject("sources");
                if (src != null) FillEventSource(c, src);
                yield return c;
                yield break;
            }
            case "wheelstaysflipped":
            {
                var rolls = props.GetIntArray(discriminator) ?? [];
                var c = new WheelStaysFlippedClause { Rolls = rolls, Min = min, Score = score, Label = label };
                if (max.HasValue) c.Max = max;
                yield return c;
                yield break;
            }

            default:
                throw new InvalidOperationException($"Unhandled discriminator '{discriminator}'.");
        }
    }

    // ── Logic clause children helper ──────────────────────────────────────────

    private static (JamlClauseBase[] children, int[] childAntes) ParseLogicChildren(
        PropsReader props,
        string key,
        int[] outerAntes
    )
    {
        // Sequence form:  and:\n  - joker: X\n  - joker: Y\nantes: [1,2]
        var inlineList = props.GetObjectList(key);
        if (inlineList != null)
        {
            var children = inlineList.SelectMany(ParseClause).ToArray();
            HoistAntes(children, outerAntes);
            return (children, outerAntes);
        }

        // Mapping form:  and:\n    clauses:\n      - ...\n    antes: [1,2]
        var block = props.GetObject(key)
            ?? throw new InvalidOperationException($"'{key}' value must be a sequence or mapping.");

        foreach (var k in block.GetKeys())
            if (!LogicBlockKeys.Contains(k))
                throw new InvalidOperationException($"Unknown key '{k}' inside '{key}' block.");

        var clauseReaders = block.GetObjectList("clauses") ?? [];
        var innerAntes = block.GetIntArray("antes") ?? outerAntes;
        var ch = clauseReaders.SelectMany(ParseClause).ToArray();
        HoistAntes(ch, innerAntes);
        return (ch, innerAntes);
    }

    private static void HoistAntes(JamlClauseBase[] children, int[] antes)
    {
        if (antes.Length == 0)
            return;
        foreach (var child in children)
        {
            if (child is JamlClause jc && jc.Antes.Length == 0)
                jc.Antes = antes;
            else if (child is LogicClause lc)
                HoistAntes(lc.Clauses, antes);
        }
    }

    // ── Source fillers ────────────────────────────────────────────────────────

    private static void FillJokerSources(
        JokerSourceConfig src,
        PropsReader block,
        HashSet<string> allowedKeys
    )
    {
        foreach (var k in block.GetKeys())
            if (!allowedKeys.Contains(k))
                throw new InvalidOperationException($"Unknown joker source key '{k}'.");

        src.ShopItems = block.GetIntArray("shopItems") ?? src.ShopItems;
        src.BoosterPacks = block.GetIntArray("boosterPacks") ?? src.BoosterPacks;
        src.Judgement = block.GetIntArray("judgement") ?? src.Judgement;
        src.Wraith = block.GetIntArray("wraith") ?? src.Wraith;
        src.RiffRaff = block.GetIntArray("riffRaff") ?? src.RiffRaff;
        src.RareTag = block.GetIntArray("rareTag") ?? src.RareTag;
        src.UncommonTag = block.GetIntArray("uncommonTag") ?? src.UncommonTag;
        src.CommonShopJokers = block.GetIntArray("commonShopJokers") ?? src.CommonShopJokers;
        src.UncommonShopJokers = block.GetIntArray("uncommonShopJokers") ?? src.UncommonShopJokers;
        src.RareShopJokers = block.GetIntArray("rareShopJokers") ?? src.RareShopJokers;
        src.AllShopJokers = block.GetIntArray("allShopJokers") ?? src.AllShopJokers;
    }

    private static void FillLegendarySources(LegendaryJokerSourceConfig src, PropsReader block)
    {
        foreach (var k in block.GetKeys())
            if (!LegendarySourceKeys.Contains(k))
                throw new InvalidOperationException($"Unknown legendaryJoker source key '{k}'.");

        src.ShopItems = block.GetIntArray("shopItems") ?? src.ShopItems;
        src.BoosterPacks = block.GetIntArray("boosterPacks") ?? src.BoosterPacks;
        src.ArcanaPacks = block.GetIntArray("arcanaPacks") ?? src.ArcanaPacks;
        src.SpectralPacks = block.GetIntArray("spectralPacks") ?? src.SpectralPacks;
        src.SoulCard = block.GetIntArray("soulCard") ?? src.SoulCard;
        if (block.GetBool("requireMega") is bool rm)
            src.RequireMegaPack = rm;
    }

    private static void FillTarotSources(TarotCardSourceConfig src, PropsReader block)
    {
        foreach (var k in block.GetKeys())
            if (!TarotSourceKeys.Contains(k))
                throw new InvalidOperationException($"Unknown tarotCard source key '{k}'.");

        src.ShopItems = block.GetIntArray("shopItems") ?? src.ShopItems;
        src.BoosterPacks = block.GetIntArray("boosterPacks") ?? src.BoosterPacks;
        src.Emperor = block.GetIntArray("emperor") ?? src.Emperor;
        src.PurpleSealOrEightBall = block.GetIntArray("purpleSealOrEightBall") ?? src.PurpleSealOrEightBall;
        if (block.GetBool("charmTag") is bool ct)
            src.CharmTag = ct;
    }

    private static void FillSpectralSources(SpectralCardSourceConfig src, PropsReader block)
    {
        foreach (var k in block.GetKeys())
            if (!SpectralSourceKeys.Contains(k))
                throw new InvalidOperationException($"Unknown spectralCard source key '{k}'.");

        src.ShopItems = block.GetIntArray("shopItems") ?? src.ShopItems;
        src.BoosterPacks = block.GetIntArray("boosterPacks") ?? src.BoosterPacks;
        src.SixthSense = block.GetIntArray("sixthSense") ?? src.SixthSense;
        src.Seance = block.GetIntArray("seance") ?? src.Seance;
        if (block.GetBool("etherealTag") is bool et)
            src.EtherealTag = et;
    }

    private static void FillPlanetSources(PlanetSourceConfig src, PropsReader block)
    {
        foreach (var k in block.GetKeys())
            if (!PlanetSourceKeys.Contains(k))
                throw new InvalidOperationException($"Unknown planetCard source key '{k}'.");

        src.ShopItems = block.GetIntArray("shopItems") ?? src.ShopItems;
        src.BoosterPacks = block.GetIntArray("boosterPacks") ?? src.BoosterPacks;
    }

    private static void FillStandardSources(StandardCardSourceConfig src, PropsReader block)
    {
        foreach (var k in block.GetKeys())
            if (!StandardSourceKeys.Contains(k))
                throw new InvalidOperationException($"Unknown standardCard source key '{k}'.");

        src.ShopItems = block.GetIntArray("shopItems") ?? src.ShopItems;
        src.BoosterPacks = block.GetIntArray("boosterPacks") ?? src.BoosterPacks;
        src.Certificate = block.GetIntArray("certificate") ?? src.Certificate;
        src.Incantation = block.GetIntArray("incantation") ?? src.Incantation;
        src.Familiar = block.GetIntArray("familiar") ?? src.Familiar;
        src.Grim = block.GetIntArray("grim") ?? src.Grim;
        src.DeckDraw = block.GetIntArray("deckDraw") ?? src.DeckDraw;
    }

    private static void FillEventSource(RollClause clause, PropsReader block)
    {
        foreach (var k in block.GetKeys())
            if (!EventSourceKeys.Contains(k))
                throw new InvalidOperationException($"Unknown event source key '{k}'.");

        if (block.GetInt("luck") is int luck)
            clause.Luck = luck;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static TJoker[] ParseJokerList<TJoker>(
        string? scalar,
        string[]? arr,
        out bool isWildcard
    )
        where TJoker : struct, Enum
    {
        isWildcard = false;
        if (scalar != null)
        {
            if (string.Equals(scalar, "Any", StringComparison.OrdinalIgnoreCase))
            {
                isWildcard = true;
                return [];
            }
            return [ParseEnumWithFallback<TJoker>(scalar)];
        }
        if (arr != null)
            return arr.Select(v => ParseEnumWithFallback<TJoker>(v)).ToArray();
        return [];
    }

    private static MotelyJokerSticker[] ParseStickers(string[]? stickers)
    {
        if (stickers == null)
            return [];
        return stickers.Select(s => ParseEnumWithFallback<MotelyJokerSticker>(s)).ToArray();
    }

    private static T? ParseOptionalEnum<T>(string? value) where T : struct, Enum
    {
        if (value == null)
            return null;
        return ParseEnumWithFallback<T>(value);
    }

    private static MotelyStandardcardRank ParseRank(string value)
    {
        // Numeric pip support: "2"→Two, "3"→Three ... "10"→Ten
        if (int.TryParse(value, out int pip))
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
        // Short names: J/Q/K/A
        return value.ToUpperInvariant() switch
        {
            "J" => MotelyStandardcardRank.Jack,
            "Q" => MotelyStandardcardRank.Queen,
            "K" => MotelyStandardcardRank.King,
            "A" => MotelyStandardcardRank.Ace,
            _ => ParseEnumWithFallback<MotelyStandardcardRank>(value),
        };
    }

    /// <summary>
    /// Tries direct enum parse (case-insensitive), then strips spaces and retries
    /// ("Walkie Talkie" → "WalkieTalkie").
    /// </summary>
    private static T ParseEnumWithFallback<T>(string value) where T : struct, Enum
    {
        if (Enum.TryParse<T>(value, ignoreCase: true, out var result))
            return result;

        var stripped = value.Replace(" ", "");
        if (stripped != value && Enum.TryParse<T>(stripped, ignoreCase: true, out result))
            return result;

        throw new InvalidOperationException(
            $"Cannot parse '{value}' as {typeof(T).Name}. Known values: {string.Join(", ", Enum.GetNames<T>())}."
        );
    }

    private static string SlugifyName(string name) =>
        name.ToLowerInvariant().Replace(' ', '-').Replace("_", "-");
}

// ── PropsReader abstraction ────────────────────────────────────────────────────

internal abstract class PropsReader
{
    public abstract string? GetScalar(string key);
    public abstract int? GetInt(string key);
    public abstract bool? GetBool(string key);
    public abstract int[]? GetIntArray(string key);
    public abstract string[]? GetStringArray(string key);
    public abstract PropsReader? GetObject(string key);
    public abstract List<PropsReader>? GetObjectList(string key);
    public abstract IEnumerable<string> GetKeys();
    public abstract bool HasKey(string key);
}

// ── YAML implementation ────────────────────────────────────────────────────────

internal sealed class YamlPropsReader : PropsReader
{
    private readonly Dictionary<string, YamlNode> _map;

    public YamlPropsReader(YamlMappingNode node)
    {
        _map = new Dictionary<string, YamlNode>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in node.Children)
        {
            var key = ((YamlScalarNode)kv.Key).Value ?? "";
            _map[key] = kv.Value;
        }
    }

    public override IEnumerable<string> GetKeys() => _map.Keys;

    public override bool HasKey(string key) => _map.ContainsKey(key);

    public override string? GetScalar(string key)
    {
        if (!_map.TryGetValue(key, out var node)) return null;
        return (node as YamlScalarNode)?.Value;
    }

    public override int? GetInt(string key)
    {
        var s = GetScalar(key);
        return s != null && int.TryParse(s, out var v) ? v : null;
    }

    public override bool? GetBool(string key)
    {
        var s = GetScalar(key);
        if (s == null) return null;
        if (s.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
        if (s.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;
        return null;
    }

    public override int[]? GetIntArray(string key)
    {
        if (!_map.TryGetValue(key, out var node)) return null;
        if (node is YamlSequenceNode seq)
            return seq.Children
                .OfType<YamlScalarNode>()
                .Select(s => int.TryParse(s.Value, out var v) ? v
                    : throw new InvalidOperationException($"Expected integer in '{key}' array, got '{s.Value}'."))
                .ToArray();
        return null;
    }

    public override string[]? GetStringArray(string key)
    {
        if (!_map.TryGetValue(key, out var node)) return null;
        if (node is YamlSequenceNode seq)
            return seq.Children
                .OfType<YamlScalarNode>()
                .Select(s => s.Value ?? "")
                .ToArray();
        if (node is YamlScalarNode scalar)
            return [scalar.Value ?? ""];
        return null;
    }

    public override PropsReader? GetObject(string key)
    {
        if (!_map.TryGetValue(key, out var node)) return null;
        return node is YamlMappingNode map ? new YamlPropsReader(map) : null;
    }

    public override List<PropsReader>? GetObjectList(string key)
    {
        if (!_map.TryGetValue(key, out var node)) return null;
        if (node is not YamlSequenceNode seq) return null;
        var list = new List<PropsReader>();
        foreach (var item in seq.Children)
        {
            if (item is YamlMappingNode mapItem)
                list.Add(new YamlPropsReader(mapItem));
        }
        return list;
    }
}

// ── JSON implementation ────────────────────────────────────────────────────────

internal sealed class JsonPropsReader : PropsReader
{
    private readonly JsonElement _el;

    public JsonPropsReader(JsonElement el) => _el = el;

    public override IEnumerable<string> GetKeys() =>
        _el.ValueKind == JsonValueKind.Object
            ? _el.EnumerateObject().Select(p => p.Name)
            : [];

    public override bool HasKey(string key) =>
        _el.ValueKind == JsonValueKind.Object && _el.TryGetProperty(key, out _);

    public override string? GetScalar(string key)
    {
        if (!_el.TryGetProperty(key, out var prop)) return null;
        return prop.ValueKind == JsonValueKind.String ? prop.GetString() : null;
    }

    public override int? GetInt(string key)
    {
        if (!_el.TryGetProperty(key, out var prop)) return null;
        if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var v)) return v;
        if (prop.ValueKind == JsonValueKind.String &&
            int.TryParse(prop.GetString(), out v)) return v;
        return null;
    }

    public override bool? GetBool(string key)
    {
        if (!_el.TryGetProperty(key, out var prop)) return null;
        if (prop.ValueKind == JsonValueKind.True) return true;
        if (prop.ValueKind == JsonValueKind.False) return false;
        return null;
    }

    public override int[]? GetIntArray(string key)
    {
        if (!_el.TryGetProperty(key, out var prop)) return null;
        if (prop.ValueKind == JsonValueKind.Array)
            return prop.EnumerateArray()
                .Select(e => e.ValueKind == JsonValueKind.Number && e.TryGetInt32(out var v) ? v
                    : throw new InvalidOperationException($"Expected integer in '{key}' array, got '{e}'."))
                .ToArray();
        return null;
    }

    public override string[]? GetStringArray(string key)
    {
        if (!_el.TryGetProperty(key, out var prop)) return null;
        if (prop.ValueKind == JsonValueKind.Array)
            return prop.EnumerateArray()
                .Select(e => e.ValueKind == JsonValueKind.String ? e.GetString() ?? "" : "")
                .ToArray();
        if (prop.ValueKind == JsonValueKind.String)
            return [prop.GetString() ?? ""];
        return null;
    }

    public override PropsReader? GetObject(string key)
    {
        if (!_el.TryGetProperty(key, out var prop)) return null;
        return prop.ValueKind == JsonValueKind.Object ? new JsonPropsReader(prop) : null;
    }

    public override List<PropsReader>? GetObjectList(string key)
    {
        if (!_el.TryGetProperty(key, out var prop)) return null;
        if (prop.ValueKind == JsonValueKind.Array)
        {
            var list = new List<PropsReader>();
            foreach (var item in prop.EnumerateArray())
                if (item.ValueKind == JsonValueKind.Object)
                    list.Add(new JsonPropsReader(item));
            return list;
        }
        return null;
    }
}
