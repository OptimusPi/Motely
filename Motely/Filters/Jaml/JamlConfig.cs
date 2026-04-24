using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;

namespace Motely.Filters;

/// <summary>
/// Typed clause lists for one JAML section (must / should / mustNot).
/// Each element = one filter in the chain.
/// </summary>
public sealed class JamlClauseSet : IEnumerable<IJamlClause>
{
    public List<IJamlClause> OrderedClauses { get; } = [];
    public List<JokerClause> Jokers { get; set; } = [];
    public List<CommonJokerClause> CommonJokers { get; set; } = [];
    public List<UncommonJokerClause> UncommonJokers { get; set; } = [];
    public List<RareJokerClause> RareJokers { get; set; } = [];
    public List<MixedJokerClause> MixedJokers { get; set; } = [];
    public List<LegendaryJokerClause> LegendaryJokers { get; set; } = [];
    public List<VoucherClause> Vouchers { get; set; } = [];
    public List<TarotCardClause> TarotCards { get; set; } = [];
    public List<SpectralCardClause> SpectralCards { get; set; } = [];
    public List<PlanetCardClause> PlanetCards { get; set; } = [];
    public List<StandardCardClause> StandardCards { get; set; } = [];
    public List<BossClause> Bosses { get; set; } = [];
    public List<TagClause> Tags { get; set; } = [];
    public List<ErraticRankClause> ErraticRanks { get; set; } = [];
    public List<ErraticSuitClause> ErraticSuits { get; set; } = [];
    public List<ErraticCardClause> ErraticCards { get; set; } = [];
    public List<LuckyMoneyClause> LuckyMoney { get; set; } = [];
    public List<LuckyMultClause> LuckyMult { get; set; } = [];
    public List<MisprintMultClause> MisprintMult { get; set; } = [];
    public List<WheelOfFortuneClause> WheelOfFortune { get; set; } = [];
    public List<CavendishExtinctClause> CavendishExtinct { get; set; } = [];
    public List<GrosMichelExtinctClause> GrosMichelExtinct { get; set; } = [];
    public List<StartingDrawClause> StartingDraw { get; set; } = [];
    public List<AndClause> And { get; set; } = [];
    public List<OrClause> Or { get; set; } = [];

    /// <summary>Number of clauses in <see cref="OrderedClauses"/> (evaluation order).</summary>
    public int Count { get { return OrderedClauses.Count; } }

    public bool HasAnyClauses { get { return OrderedClauses.Count > 0; } }

    public IEnumerator<IJamlClause> GetEnumerator() { return OrderedClauses.GetEnumerator(); }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() { return GetEnumerator(); }
}

/// <summary>
/// JAML config consumed by JamlSearchBuilder.
/// </summary>
public sealed class JamlConfig
{
    public string Id { get; set; } = "";
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Author { get; set; }
    public string DateCreated { get; set; } = System.DateTime.UtcNow.ToString("O");
    public MotelyDeck Deck { get; set; } = MotelyDeck.Red;
    public MotelyStake Stake { get; set; } = MotelyStake.White;
    public List<string> Hashtags { get; set; } = [];

    public JamlClauseSet Must { get; set; } = new();
    public JamlClauseSet Should { get; set; } = new();
    public JamlClauseSet MustNot { get; set; } = new();

    public bool HasAnyClauses { get; set; }

    /// <summary>Normalized filter name used as the runtime identifier for this config.</summary>
    public string FilterId { get; set; } = "";

    /// <summary>
    /// Optional seed-space constraints from the JAML document’s <c>aesthetics</c> list.
    /// Merged into the search request during orchestration when compatible with the host search mode.
    /// </summary>
    public List<JamlAesthetic> Aesthetics { get; set; } = [];
}

/// <summary>
/// Top-level JAML document: the loader fills this from YAML; <see cref="JamlSerializer"/> and the TUI emit the same shape. Keys are camelCase (JAML convention).
/// </summary>
public sealed class JamlRootDocument
{
    [YamlMember(Alias = "id")]
    public string? Id { get; set; }

    [YamlMember(Alias = "name")]
    public string? Name { get; set; }

    [YamlMember(Alias = "author")]
    public string? Author { get; set; }

    [YamlMember(Alias = "dateCreated")]
    public string? DateCreated { get; set; }

    [YamlMember(Alias = "description")]
    public string? Description { get; set; }

    [YamlMember(Alias = "deck")]
    public string? Deck { get; set; }

    [YamlMember(Alias = "stake")]
    public string? Stake { get; set; }

    [YamlMember(Alias = "defaults")]
    public JamlDefaultsDto? Defaults { get; set; }

    [YamlMember(Alias = "must")]
    public List<JamlClauseDto>? Must { get; set; }

    [YamlMember(Alias = "should")]
    public List<JamlClauseDto>? Should { get; set; }

    [YamlMember(Alias = "mustNot")]
    public List<JamlClauseDto>? MustNot { get; set; }

    [YamlMember(Alias = "aesthetics")]
    public List<string>? Aesthetics { get; set; }

    [YamlMember(Alias = "hashtags")]
    public List<string>? Hashtags { get; set; }

    [YamlMember(Alias = "seeds")]
    public List<string>? Seeds { get; set; }
}

public sealed class JamlDefaultsDto
{
    [YamlMember(Alias = "antes")]
    public int[]? Antes { get; set; }

    [YamlMember(Alias = "boosterPacks")]
    public int[]? BoosterPacks { get; set; }

    [YamlMember(Alias = "shopItems")]
    public int[]? ShopItems { get; set; }

    [YamlMember(Alias = "score")]
    public int? Score { get; set; }
}

public sealed class JamlClauseDto
{
    // Explicit type+value (old syntax)
    [YamlMember(Alias = "type")]
    public string? Type { get; set; }

    [YamlMember(Alias = "value")]
    public string? Value { get; set; }

    // Type-as-key shorthand (new syntax)
    [YamlMember(Alias = "joker")]
    public string? Joker { get; set; }

    [YamlMember(Alias = "jokers")]
    public List<string>? Jokers { get; set; }

    [YamlMember(Alias = "commonJoker")]
    public string? CommonJoker { get; set; }

    [YamlMember(Alias = "commonJokers")]
    public List<string>? CommonJokers { get; set; }

    [YamlMember(Alias = "uncommonJoker")]
    public string? UncommonJoker { get; set; }

    [YamlMember(Alias = "uncommonJokers")]
    public List<string>? UncommonJokers { get; set; }

    [YamlMember(Alias = "rareJoker")]
    public string? RareJoker { get; set; }

    [YamlMember(Alias = "rareJokers")]
    public List<string>? RareJokers { get; set; }

    [YamlMember(Alias = "mixedJoker")]
    public string? MixedJoker { get; set; }

    [YamlMember(Alias = "mixedJokers")]
    public List<string>? MixedJokers { get; set; }

    [YamlMember(Alias = "soulJoker")]
    public string? SoulJoker { get; set; }

    [YamlMember(Alias = "legendaryJoker")]
    public string? LegendaryJoker { get; set; }

    [YamlMember(Alias = "voucher")]
    public string? Voucher { get; set; }

    [YamlMember(Alias = "vouchers")]
    public List<string>? Vouchers { get; set; }

    [YamlMember(Alias = "tarot")]
    public string? Tarot { get; set; }

    [YamlMember(Alias = "tarotCard")]
    public string? TarotCard { get; set; }

    [YamlMember(Alias = "spectral")]
    public string? Spectral { get; set; }

    [YamlMember(Alias = "spectralCard")]
    public string? SpectralCard { get; set; }

    [YamlMember(Alias = "planet")]
    public string? Planet { get; set; }

    [YamlMember(Alias = "planetCard")]
    public string? PlanetCard { get; set; }

    [YamlMember(Alias = "boss")]
    public string? Boss { get; set; }

    [YamlMember(Alias = "tag")]
    public string? Tag { get; set; }

    [YamlMember(Alias = "smallBlindTag")]
    public string? SmallBlindTag { get; set; }

    [YamlMember(Alias = "bigBlindTag")]
    public string? BigBlindTag { get; set; }

    [YamlMember(Alias = "standardCard")]
    public string? StandardCard { get; set; }

    [YamlMember(Alias = "erraticRank")]
    public string? ErraticRank { get; set; }

    [YamlMember(Alias = "erraticSuit")]
    public string? ErraticSuit { get; set; }

    [YamlMember(Alias = "erraticCard")]
    public string? ErraticCard { get; set; }

    [YamlMember(Alias = "startingDraw")]
    public string? StartingDraw { get; set; }

    [YamlMember(Alias = "event")]
    public string? Event { get; set; }

    [YamlMember(Alias = "eventType")]
    public string? EventType { get; set; }

    [YamlMember(Alias = "luckyMoney")]
    public int[]? LuckyMoney { get; set; }

    [YamlMember(Alias = "luckyMult")]
    public int[]? LuckyMult { get; set; }

    [YamlMember(Alias = "misprintMult")]
    public int[]? MisprintMult { get; set; }

    [YamlMember(Alias = "wheelOfFortune")]
    public int[]? WheelOfFortune { get; set; }

    [YamlMember(Alias = "cavendishExtinct")]
    public int[]? CavendishExtinct { get; set; }

    [YamlMember(Alias = "grosMichelExtinct")]
    public int[]? GrosMichelExtinct { get; set; }

    // Common clause properties
    [YamlMember(Alias = "antes")]
    public int[]? Antes { get; set; }

    [YamlMember(Alias = "score")]
    public int? Score { get; set; }

    [YamlMember(Alias = "min")]
    public int? Min { get; set; }

    [YamlMember(Alias = "max")]
    public int? Max { get; set; }

    [YamlMember(Alias = "label")]
    public string? Label { get; set; }

    [YamlMember(Alias = "edition")]
    public string? Edition { get; set; }

    [YamlMember(Alias = "stickers")]
    public string[]? Stickers { get; set; }

    [YamlMember(Alias = "seal")]
    public string? Seal { get; set; }

    [YamlMember(Alias = "enhancement")]
    public string? Enhancement { get; set; }

    [YamlMember(Alias = "rank")]
    public string? Rank { get; set; }

    [YamlMember(Alias = "suit")]
    public string? Suit { get; set; }

    [YamlMember(Alias = "rolls")]
    public int[]? Rolls { get; set; }

    /// <summary>
    /// Extra soul-stream edition reads per ante for the legendary edition vector prefilter (see
    /// <see cref="LegendaryJokerClause.SoulEditionRolls"/>).
    /// </summary>
    [YamlMember(Alias = "soulEditionRolls")]
    public int? SoulEditionRolls { get; set; }

    /// <summary>
    /// Match The Soul tarot/spectral card in packs only (no legendary joker roll). See
    /// <see cref="LegendaryJokerClause.SoulCardOnly"/>.
    /// </summary>
    [YamlMember(Alias = "soulCardOnly")]
    public bool? SoulCardOnly { get; set; }

    // Compound clauses (YAML keys are lowercase; matches jaml.schema / hand-written JAML)
    [YamlMember(Alias = "and")]
    public List<JamlClauseDto>? And { get; set; }

    [YamlMember(Alias = "or")]
    public List<JamlClauseDto>? Or { get; set; }

    [YamlMember(Alias = "clauses")]
    public List<JamlClauseDto>? Clauses { get; set; }

    [YamlMember(Alias = "mode")]
    public string? Mode { get; set; }

    // Flat source shortcuts (top-level on clause)
    [YamlMember(Alias = "shopItems")]
    public int[]? ShopItems { get; set; }

    [YamlMember(Alias = "boosterPacks")]
    public int[]? BoosterPacks { get; set; }

    [YamlMember(Alias = "minShopSlot")]
    public int? MinShopSlot { get; set; }

    [YamlMember(Alias = "maxShopSlot")]
    public int? MaxShopSlot { get; set; }

    [YamlMember(Alias = "minPackSlot")]
    public int? MinPackSlot { get; set; }

    [YamlMember(Alias = "maxPackSlot")]
    public int? MaxPackSlot { get; set; }

    // Nested sources object
    [YamlMember(Alias = "sources")]
    public JamlSourcesDto? Sources { get; set; }
}

public sealed class JamlSourcesDto
{
    [YamlMember(Alias = "shopItems")]
    public int[]? ShopItems { get; set; }

    [YamlMember(Alias = "boosterPacks")]
    public int[]? BoosterPacks { get; set; }

    [YamlMember(Alias = "minShopSlot")]
    public int? MinShopSlot { get; set; }

    [YamlMember(Alias = "maxShopSlot")]
    public int? MaxShopSlot { get; set; }

    [YamlMember(Alias = "minPackSlot")]
    public int? MinPackSlot { get; set; }

    [YamlMember(Alias = "maxPackSlot")]
    public int? MaxPackSlot { get; set; }

    [YamlMember(Alias = "tags")]
    public bool Tags { get; set; }

    [YamlMember(Alias = "requireMega")]
    public bool RequireMega { get; set; }

    /// <summary>
    /// Booster scoring: apply Charm-tag rules (bonus Arcana pack on second weighted offer when none rolled Arcana).
    /// </summary>
    [YamlMember(Alias = "charmTag")]
    public bool CharmTag { get; set; }

    /// <summary>
    /// Booster scoring: apply Ethereal-tag rules (bonus Spectral pack when none rolled Spectral).
    /// </summary>
    [YamlMember(Alias = "etherealTag")]
    public bool EtherealTag { get; set; }

    [YamlMember(Alias = "judgement")]
    public int[]? Judgement { get; set; }

    [YamlMember(Alias = "rareTag")]
    public int[]? RareTag { get; set; }

    [YamlMember(Alias = "uncommonTag")]
    public int[]? UncommonTag { get; set; }

    [YamlMember(Alias = "wraith")]
    public int[]? Wraith { get; set; }

    [YamlMember(Alias = "soulCard")]
    public int[]? SoulCard { get; set; }

    /// <summary>
    /// Legendary / soul joker: shop pack slots where The Soul may appear in an <b>arcana</b> pack (tarot stream).
    /// If either this or spectralBoosterPacks is non-empty, matching uses split rules (see SoulJokerSourceConfig).
    /// </summary>
    [YamlMember(Alias = "arcanaBoosterPacks")]
    public int[]? ArcanaBoosterPacks { get; set; }

    /// <summary>
    /// Legendary / soul joker: shop pack slots where The Soul may appear in a <b>spectral</b> pack (spectral stream).
    /// </summary>
    [YamlMember(Alias = "spectralBoosterPacks")]
    public int[]? SpectralBoosterPacks { get; set; }

    [YamlMember(Alias = "riffRaff")]
    public int[]? RiffRaff { get; set; }

    [YamlMember(Alias = "purpleSealOrEightBall")]
    public int[]? PurpleSealOrEightBall { get; set; }

    [YamlMember(Alias = "emperor")]
    public int[]? Emperor { get; set; }

    [YamlMember(Alias = "sixthSense")]
    public int[]? SixthSense { get; set; }

    [YamlMember(Alias = "seance")]
    public int[]? Seance { get; set; }

    [YamlMember(Alias = "certificate")]
    public int[]? Certificate { get; set; }

    [YamlMember(Alias = "incantation")]
    public int[]? Incantation { get; set; }

    [YamlMember(Alias = "familiar")]
    public int[]? Familiar { get; set; }

    [YamlMember(Alias = "grim")]
    public int[]? Grim { get; set; }

    [YamlMember(Alias = "deckDraw")]
    public int[]? DeckDraw { get; set; }

    [YamlMember(Alias = "uncommonShopJokers")]
    public int[]? UncommonShopJokers { get; set; }

    [YamlMember(Alias = "rareShopJokers")]
    public int[]? RareShopJokers { get; set; }

    [YamlMember(Alias = "commonShopJokers")]
    public int[]? CommonShopJokers { get; set; }

    [YamlMember(Alias = "allShopJokers")]
    public int[]? AllShopJokers { get; set; }
}

// ────────────────────────────── Loader ──────────────────────────────

public static partial class JamlConfigLoader
{
    private static readonly int[] DefaultAntes = [1, 2, 3, 4, 5, 6, 7, 8];

    public static bool TryLoad(
        string jaml,
        [NotNullWhen(true)] out JamlConfig? config,
        out string? error
    )
    {
        config = null;
        error = null;

        if (string.IsNullOrWhiteSpace(jaml))
        {
            error = "JAML content is required.";
            return false;
        }

        try
        {
            var normalizedJaml = NormalizeNestedLogicSyntax(jaml);
            if (!TryParseRootFromYaml(normalizedJaml, out var load, out error) || load is null)
            {
                config = null;
                if (error == null)
                    error = "JAML document could not be parsed.";
                return false;
            }

            var defaultAntes = load.Defaults?.Antes ?? DefaultAntes;

            var deck = Enum.TryParse<MotelyDeck>(load.Deck, true, out var deckEnum)
                ? deckEnum
                : MotelyDeck.Red;
            var stake = Enum.TryParse<MotelyStake>(load.Stake, true, out var stakeEnum)
                ? stakeEnum
                : MotelyStake.White;

            config = new JamlConfig
            {
                Name = load.Name,
                Description = load.Description,
                Author = load.Author,
                DateCreated = load.DateCreated ?? System.DateTime.UtcNow.ToString("O"),
                Deck = deck,
                Stake = stake,
            };

            config.Hashtags = NormalizeHashtags(load.Hashtags);

            if (load.Aesthetics is { Count: > 0 })
            {
                foreach (var entry in load.Aesthetics)
                {
                    if (string.IsNullOrWhiteSpace(entry))
                        continue;
                    if (!JamlAestheticParser.TryParse(entry, out var aesthetic))
                    {
                        error = $"Unknown aesthetics value '{entry.Trim()}'. Known: {JamlAestheticParser.KnownJamlStringsDescription()}.";
                        config = null;
                        return false;
                    }

                    if (!config.Aesthetics.Contains(aesthetic))
                        config.Aesthetics.Add(aesthetic);
                }
            }

            // MUST → required filters
            PopulateClauses(config.Must, load.Must, defaultAntes, load.Defaults);

            // SHOULD → scoring clauses
            PopulateClauses(config.Should, load.Should, defaultAntes, load.Defaults);

            // MUSTNOT → negation filters
            PopulateClauses(config.MustNot, load.MustNot, defaultAntes, load.Defaults);

            // Semantic fingerprint must run after clauses are populated (not on an empty config).
            var baseFilterId = NormalizeFilterId(load.Id, load.Name);
            config.Id = AppendSemanticFingerprintToFilterId(baseFilterId, config, load.Defaults);
            config.FilterId = config.Id;

            // Set once — config is immutable after load
            config.HasAnyClauses = config.Must.HasAnyClauses || config.Should.HasAnyClauses || config.MustNot.HasAnyClauses;

            return true;
        }
        catch (Exception ex)
        {
            config = null;
            error = FormatLoadError(ex);
            return false;
        }
    }

    /// <summary>
    /// Like <see cref="TryLoad"/> but also surfaces the raw exception so callers can extract
    /// structured location info (line, column) from <see cref="YamlException"/>.
    /// </summary>
    public static bool TryLoadWithException(
        string jaml,
        [NotNullWhen(true)] out JamlConfig? config,
        out string? error,
        out Exception? exception)
    {
        exception = null;
        config = null;
        error = null;

        if (string.IsNullOrWhiteSpace(jaml))
        {
            error = "JAML content is required.";
            return false;
        }

        try
        {
            var normalizedJaml = NormalizeNestedLogicSyntax(jaml);
            if (!TryParseRootFromYaml(normalizedJaml, out var load, out error) || load is null)
            {
                if (error == null) error = "JAML document could not be parsed.";
                return false;
            }

            var defaultAntes = load.Defaults?.Antes ?? DefaultAntes;
            var deck = Enum.TryParse<MotelyDeck>(load.Deck, true, out var deckEnum) ? deckEnum : MotelyDeck.Red;
            var stake = Enum.TryParse<MotelyStake>(load.Stake, true, out var stakeEnum) ? stakeEnum : MotelyStake.White;

            config = new JamlConfig
            {
                Id = load.Id ?? load.Name ?? string.Empty,
                FilterId = load.Id ?? load.Name ?? string.Empty,
                Deck = deck,
                Stake = stake,
            };

            PopulateClauses(config.Must, load.Must, defaultAntes, load.Defaults);
            PopulateClauses(config.Should, load.Should, defaultAntes, load.Defaults);
            PopulateClauses(config.MustNot, load.MustNot, defaultAntes, load.Defaults);

            var baseFilterId = NormalizeFilterId(load.Id, load.Name);
            config.Id = AppendSemanticFingerprintToFilterId(baseFilterId, config, load.Defaults);
            config.FilterId = config.Id;
            config.HasAnyClauses = config.Must.HasAnyClauses || config.Should.HasAnyClauses || config.MustNot.HasAnyClauses;

            return true;
        }
        catch (Exception ex)
        {
            exception = ex;
            config = null;
            error = FormatLoadError(ex);
            return false;
        }
    }

    /// <summary>
    /// Rewrites nested <c>and:</c> / <c>or:</c> blocks that use <c>clauses:</c> plus shared keys
    /// (<c>antes</c>, <c>label</c>, <c>mode</c>, <c>score</c>, …) into the flat shape <see cref="JamlClauseDto"/> expects.
    /// Hoisted keys become siblings of <c>and</c>/<c>or</c> so <see cref="CreateClauseFromDto"/> passes shared <c>antes</c> into each child.
    /// </summary>
    private static string NormalizeNestedLogicSyntax(string jaml)
    {
        var yaml = new YamlStream();
        using (var reader = new StringReader(jaml))
            yaml.Load(reader);

        foreach (var document in yaml.Documents)
        {
            NormalizeNestedLogicSyntax(document.RootNode);
            ApplyPrimitiveSequenceFlowStyle(document.RootNode);
        }

        using var writer = new StringWriter();
        yaml.Save(writer, assignAnchors: false);
        return writer.ToString();
    }

    /// <summary>
    /// Emits scalar arrays in flow style (<c>[1, 2, 3]</c>) while leaving clause/object arrays block-style.
    /// </summary>
    private static void ApplyPrimitiveSequenceFlowStyle(YamlNode node)
    {
        switch (node)
        {
            case YamlMappingNode mapping:
                foreach (var child in mapping.Children.Values)
                    ApplyPrimitiveSequenceFlowStyle(child);
                break;
            case YamlSequenceNode sequence:
                foreach (var child in sequence.Children)
                    ApplyPrimitiveSequenceFlowStyle(child);

                if (sequence.Children.Count > 0 && sequence.Children.All(static c => c is YamlScalarNode))
                    sequence.Style = YamlDotNet.Core.Events.SequenceStyle.Flow;
                break;
        }
    }

    private static void NormalizeNestedLogicSyntax(YamlNode node)
    {
        switch (node)
        {
            case YamlMappingNode mapping:
                NormalizeNestedLogicBlock(mapping, "and");
                NormalizeNestedLogicBlock(mapping, "or");

                foreach (var child in mapping.Children.Values.ToArray())
                    NormalizeNestedLogicSyntax(child);
                break;

            case YamlSequenceNode sequence:
                foreach (var child in sequence.Children)
                    NormalizeNestedLogicSyntax(child);
                break;
        }
    }

    private static void NormalizeNestedLogicBlock(YamlMappingNode mapping, string key)
    {
        if (!TryGetYamlChild(mapping, key, out var keyNode, out var valueNode))
            return;

        if (valueNode is not YamlMappingNode legacyLogicBlock)
            return;

        if (!TryGetYamlChild(legacyLogicBlock, "clauses", out _, out var clausesNode))
            return;

        foreach (var child in legacyLogicBlock.Children)
        {
            if (child.Key is not YamlScalarNode childKey || childKey.Value == null)
                continue;

            if (string.Equals(childKey.Value, "clauses", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!ContainsYamlKey(mapping, childKey.Value))
                mapping.Add(new YamlScalarNode(childKey.Value), child.Value);
        }

        mapping.Children[keyNode] = clausesNode;
    }

    private static bool ContainsYamlKey(YamlMappingNode mapping, string key) =>
        mapping.Children.Keys.OfType<YamlScalarNode>().Any(node =>
            string.Equals(node.Value, key, StringComparison.OrdinalIgnoreCase)
        );

    private static bool TryGetYamlChild(
        YamlMappingNode mapping,
        string key,
        [NotNullWhen(true)] out YamlScalarNode? keyNode,
        [NotNullWhen(true)] out YamlNode? valueNode
    )
    {
        foreach (var child in mapping.Children)
        {
            if (child.Key is YamlScalarNode scalarNode
                && string.Equals(scalarNode.Value, key, StringComparison.OrdinalIgnoreCase))
            {
                keyNode = scalarNode;
                valueNode = child.Value;
                return true;
            }
        }

        keyNode = null;
        valueNode = null;
        return false;
    }

    private static string FormatLoadError(Exception ex)
    {
        var message = ex.Message;

        if (ex is YamlException yamlEx)
        {
            var mark = yamlEx.Start;
            var location = mark.Line > 0 && mark.Column > 0
                ? $"on line {mark.Line}, col {mark.Column}: "
                : string.Empty;

            var unknownPropertyMatch = Regex.Match(
                message,
                "Property '([^']+)' not found on type '([^']+)'",
                RegexOptions.CultureInvariant
            );

            if (unknownPropertyMatch.Success)
            {
                var propertyName = unknownPropertyMatch.Groups[1].Value;
                var targetType = unknownPropertyMatch.Groups[2].Value;
                return $"{location}Unknown property '{propertyName}' in {DescribeYamlTarget(targetType)}.";
            }

            return $"{location}{message}";
        }

        return message;
    }

    private static string DescribeYamlTarget(string targetType) =>
        targetType switch
        {
            "Motely.Filters.JamlRootDocument" => "the top-level JAML document",
            "Motely.Filters.JamlClauseDto" => "a clause",
            "Motely.Filters.JamlSourcesDto" => "a clause's sources block",
            "Motely.Filters.JamlDefaultsDto" => "the defaults block",
            _ => $"{targetType}",
        };

    public static bool TryLoadFromFile(
        string path,
        [NotNullWhen(true)] out JamlConfig? config,
        out string? error
    )
    {
        config = null;
        var resolved = ResolveJamlPath(path);
        if (resolved == null)
        {
            error = $"File not found: {path}";
            return false;
        }
        return TryLoad(File.ReadAllText(resolved), out config, out error);
    }

    private static string? ResolveJamlPath(string path)
    {
        // Exact path
        if (File.Exists(path))
            return path;
        // Add .jaml extension
        var withExt = Path.ChangeExtension(path, ".jaml");
        if (File.Exists(withExt))
            return withExt;
        // Check JamlFilters/ subdirectory
        var inFilters = Path.Combine("JamlFilters", path);
        if (File.Exists(inFilters))
            return inFilters;
        var inFiltersExt = Path.Combine("JamlFilters", withExt);
        if (File.Exists(inFiltersExt))
            return inFiltersExt;
        return null;
    }

    // ── Clause list population — adds directly to typed lists or logic clauses ──

    private static void PopulateClauses(
        JamlClauseSet set,
        List<JamlClauseDto>? clauses,
        int[] defaultAntes,
        JamlDefaultsDto? defaults
    )
    {
        if (clauses == null || clauses.Count == 0)
            return;
        bool inheritedAntesSpecifiedByUser = defaults?.Antes != null;
        foreach (var c in clauses)
        {
            var clause = CreateClauseFromDto(
                c,
                defaultAntes,
                defaults,
                inheritedAntesSpecifiedByUser
            );
            AddClauseToSet(set, clause);
        }
    }

    private static void AddClauseToSet(JamlClauseSet set, IJamlClause clause)
    {
        set.OrderedClauses.Add(clause);

        switch (clause)
        {
            case JokerClause c:
                set.Jokers.Add(c);
                break;
            case CommonJokerClause c:
                set.CommonJokers.Add(c);
                break;
            case UncommonJokerClause c:
                set.UncommonJokers.Add(c);
                break;
            case RareJokerClause c:
                set.RareJokers.Add(c);
                break;
            case MixedJokerClause c:
                set.MixedJokers.Add(c);
                break;
            case LegendaryJokerClause c:
                set.LegendaryJokers.Add(c);
                break;
            case VoucherClause c:
                set.Vouchers.Add(c);
                break;
            case TarotCardClause c:
                set.TarotCards.Add(c);
                break;
            case SpectralCardClause c:
                set.SpectralCards.Add(c);
                break;
            case PlanetCardClause c:
                set.PlanetCards.Add(c);
                break;
            case BossClause c:
                set.Bosses.Add(c);
                break;
            case TagClause c:
                set.Tags.Add(c);
                break;
            case StandardCardClause c:
                set.StandardCards.Add(c);
                break;
            case ErraticRankClause c:
                set.ErraticRanks.Add(c);
                break;
            case ErraticSuitClause c:
                set.ErraticSuits.Add(c);
                break;
            case ErraticCardClause c:
                set.ErraticCards.Add(c);
                break;
            case LuckyMoneyClause c:
                set.LuckyMoney.Add(c);
                break;
            case LuckyMultClause c:
                set.LuckyMult.Add(c);
                break;
            case MisprintMultClause c:
                set.MisprintMult.Add(c);
                break;
            case WheelOfFortuneClause c:
                set.WheelOfFortune.Add(c);
                break;
            case CavendishExtinctClause c:
                set.CavendishExtinct.Add(c);
                break;
            case GrosMichelExtinctClause c:
                set.GrosMichelExtinct.Add(c);
                break;
            case StartingDrawClause c:
                set.StartingDraw.Add(c);
                break;
            case AndClause c:
                set.And.Add(c);
                break;
            case OrClause c:
                set.Or.Add(c);
                break;
            default:
                throw new NotSupportedException(
                    $"Unsupported clause type: {clause.GetType().Name}"
                );
        }
    }

    private static IJamlClause CreateClauseFromDto(
        JamlClauseDto c,
        int[] defaultAntes,
        JamlDefaultsDto? defaults,
        bool inheritedAntesSpecifiedByUser
    )
    {
        bool clauseHasExplicitAntes = c.Antes != null;
        var antes = c.Antes ?? defaultAntes;
        bool hasUserSpecifiedAntes = clauseHasExplicitAntes || inheritedAntesSpecifiedByUser;
        int min = c.Min ?? 1;
        int score = c.Score ?? 1;
        var label = c.Label ?? GenerateLabel(c);

        bool explicitAnd = string.Equals(c.Type, "And", StringComparison.OrdinalIgnoreCase);
        bool explicitOr = string.Equals(c.Type, "Or", StringComparison.OrdinalIgnoreCase);

        if (c.And != null || explicitAnd)
        {
            var children = c.And ?? c.Clauses ?? [];

            return new AndClause
            {
                Label = label,
                Score = score,
                Clauses = children
                    .Select(sub => CreateClauseFromDto(sub, antes, defaults, hasUserSpecifiedAntes))
                    .ToArray(),
            };
        }

        if (c.Or != null || explicitOr)
        {
            var children = c.Or ?? c.Clauses ?? [];

            return new OrClause
            {
                Label = label,
                Score = score,
                Min = min,
                Clauses = children
                    .Select(sub => CreateClauseFromDto(sub, antes, defaults, hasUserSpecifiedAntes))
                    .ToArray(),
            };
        }

        if (c.Clauses != null)
        {
            return new AndClause
            {
                Label = label,
                Score = score,
                Clauses = c.Clauses
                    .Select(sub => CreateClauseFromDto(sub, antes, defaults, hasUserSpecifiedAntes))
                    .ToArray(),
            };
        }

        var (itemType, value) = ResolveType(c);
        var edition = ParseEnum<MotelyItemEdition>(c.Edition);

        var shopItems = c.Sources?.ShopItems ?? c.ShopItems ?? defaults?.ShopItems;
        var boosterPacks = c.Sources?.BoosterPacks ?? c.BoosterPacks ?? defaults?.BoosterPacks;

        // Support top-level range generators (e.g. minShopSlot: 0)
        var minShop = c.Sources?.MinShopSlot ?? c.MinShopSlot;
        var maxShop = c.Sources?.MaxShopSlot ?? c.MaxShopSlot;
        var minPack = c.Sources?.MinPackSlot ?? c.MinPackSlot;
        var maxPack = c.Sources?.MaxPackSlot ?? c.MaxPackSlot;

        if (shopItems == null && minShop != null && maxShop != null)
            shopItems = Enumerable
                .Range(minShop.Value, maxShop.Value - minShop.Value + 1)
                .ToArray();

        if (boosterPacks == null && minPack != null && maxPack != null)
            boosterPacks = Enumerable
                .Range(minPack.Value, maxPack.Value - minPack.Value + 1)
                .ToArray();

        // Soul joker: arcana/spectral pack lists alone imply split matching — do not inject default [0..5] boosterPacks.
        if (
            itemType == MotelyFilterItemType.SoulJoker
            && boosterPacks == null
            && (
                (c.Sources?.ArcanaBoosterPacks?.Length ?? 0) > 0
                || (c.Sources?.SpectralBoosterPacks?.Length ?? 0) > 0
            )
        )
            boosterPacks = [];

        NormalizeDefaultSources(ref shopItems, ref boosterPacks, itemType, c.Sources);

        var (shRank, shSuit) = ParseCardShorthand(value ?? "");

        return itemType switch
        {
            MotelyFilterItemType.Joker => new JokerClause
            {
                Label = label,
                Score = score,
                Antes = antes,
                Min = min,
                IsWildcard = IsAnyWildcard(value ?? c.Jokers?.FirstOrDefault()),
                WildcardRarity = ParseWildcardRarity(value ?? c.Jokers?.FirstOrDefault()),
                Jokers =
                    IsAnyWildcard(value ?? c.Jokers?.FirstOrDefault())
                        ? []
                        : value != null
                            ? [RequireEnum<MotelyJoker>(value)]
                            : c.Jokers?.Select(j => RequireEnum<MotelyJoker>(j)).ToArray() ?? [],
                Edition = edition,
                Stickers =
                    c.Stickers?.Select(s => RequireEnum<MotelyJokerSticker>(s)).ToArray()
                    ?? [],
                Sources = new JokerSourceConfig
                {
                    ShopItems = shopItems ?? [],
                    BoosterPacks = boosterPacks ?? [],
                    Judgement = c.Sources?.Judgement ?? [],
                    Wraith = c.Sources?.Wraith ?? [],
                    RiffRaff = c.Sources?.RiffRaff ?? [],
                    RareTag = c.Sources?.RareTag ?? [],
                    UncommonTag = c.Sources?.UncommonTag ?? [],
                    CommonShopJokers = c.Sources?.CommonShopJokers ?? [],
                    UncommonShopJokers = c.Sources?.UncommonShopJokers ?? [],
                    RareShopJokers = c.Sources?.RareShopJokers ?? [],
                    AllShopJokers = c.Sources?.AllShopJokers ?? [],
                },
            },
            MotelyFilterItemType.CommonJoker => new CommonJokerClause
            {
                Label = label,
                Score = score,
                Antes = antes,
                Min = min,
                IsWildcard = IsAnyWildcard(value ?? (c.CommonJokers ?? c.Jokers)?.FirstOrDefault()),
                Jokers =
                    IsAnyWildcard(value ?? (c.CommonJokers ?? c.Jokers)?.FirstOrDefault())
                        ? []
                        : value != null
                            ? [RequireEnum<MotelyJokerCommon>(value)]
                            : (c.CommonJokers ?? c.Jokers)?.Select(j => RequireEnum<MotelyJokerCommon>(j)).ToArray() ?? [],
                Edition = edition,
                Stickers =
                    c.Stickers?.Select(s => RequireEnum<MotelyJokerSticker>(s)).ToArray()
                    ?? [],
                Sources = new JokerSourceConfig
                {
                    ShopItems = shopItems ?? [],
                    BoosterPacks = boosterPacks ?? [],
                    Judgement = c.Sources?.Judgement ?? [],
                    Wraith = c.Sources?.Wraith ?? [],
                    RiffRaff = c.Sources?.RiffRaff ?? [],
                    RareTag = c.Sources?.RareTag ?? [],
                    UncommonTag = c.Sources?.UncommonTag ?? [],
                    CommonShopJokers = c.Sources?.CommonShopJokers ?? [],
                    UncommonShopJokers = c.Sources?.UncommonShopJokers ?? [],
                    RareShopJokers = c.Sources?.RareShopJokers ?? [],
                    AllShopJokers = c.Sources?.AllShopJokers ?? [],
                },
            },
            MotelyFilterItemType.UncommonJoker => new UncommonJokerClause
            {
                Label = label,
                Score = score,
                Antes = antes,
                Min = min,
                IsWildcard = IsAnyWildcard(value ?? (c.UncommonJokers ?? c.Jokers)?.FirstOrDefault()),
                Jokers =
                    IsAnyWildcard(value ?? (c.UncommonJokers ?? c.Jokers)?.FirstOrDefault())
                        ? []
                        : value != null
                            ? [RequireEnum<MotelyJokerUncommon>(value)]
                            : (c.UncommonJokers ?? c.Jokers)?.Select(j => RequireEnum<MotelyJokerUncommon>(j)).ToArray() ?? [],
                Edition = edition,
                Stickers =
                    c.Stickers?.Select(s => RequireEnum<MotelyJokerSticker>(s)).ToArray()
                    ?? [],
                Sources = new JokerSourceConfig
                {
                    ShopItems = shopItems ?? [],
                    BoosterPacks = boosterPacks ?? [],
                    Judgement = c.Sources?.Judgement ?? [],
                    Wraith = c.Sources?.Wraith ?? [],
                    RiffRaff = c.Sources?.RiffRaff ?? [],
                    RareTag = c.Sources?.RareTag ?? [],
                    UncommonTag = c.Sources?.UncommonTag ?? [],
                    CommonShopJokers = c.Sources?.CommonShopJokers ?? [],
                    UncommonShopJokers = c.Sources?.UncommonShopJokers ?? [],
                    RareShopJokers = c.Sources?.RareShopJokers ?? [],
                    AllShopJokers = c.Sources?.AllShopJokers ?? [],
                },
            },
            MotelyFilterItemType.RareJoker => new RareJokerClause
            {
                Label = label,
                Score = score,
                Antes = antes,
                Min = min,
                IsWildcard = IsAnyWildcard(value ?? (c.RareJokers ?? c.Jokers)?.FirstOrDefault()),
                Jokers =
                    IsAnyWildcard(value ?? (c.RareJokers ?? c.Jokers)?.FirstOrDefault())
                        ? []
                        : value != null
                            ? [RequireEnum<MotelyJokerRare>(value)]
                            : (c.RareJokers ?? c.Jokers)?.Select(j => RequireEnum<MotelyJokerRare>(j)).ToArray() ?? [],
                Edition = edition,
                Stickers =
                    c.Stickers?.Select(s => RequireEnum<MotelyJokerSticker>(s)).ToArray()
                    ?? [],
                Sources = new JokerSourceConfig
                {
                    ShopItems = shopItems ?? [],
                    BoosterPacks = boosterPacks ?? [],
                    Judgement = c.Sources?.Judgement ?? [],
                    Wraith = c.Sources?.Wraith ?? [],
                    RiffRaff = c.Sources?.RiffRaff ?? [],
                    RareTag = c.Sources?.RareTag ?? [],
                    UncommonTag = c.Sources?.UncommonTag ?? [],
                    CommonShopJokers = c.Sources?.CommonShopJokers ?? [],
                    UncommonShopJokers = c.Sources?.UncommonShopJokers ?? [],
                    RareShopJokers = c.Sources?.RareShopJokers ?? [],
                    AllShopJokers = c.Sources?.AllShopJokers ?? [],
                },
            },
            MotelyFilterItemType.MixedJoker => new MixedJokerClause
            {
                Label = label,
                Score = score,
                Antes = antes,
                Min = min,
                IsWildcard = IsAnyWildcard(value ?? (c.MixedJokers ?? c.Jokers)?.FirstOrDefault()),
                WildcardRarity = ParseWildcardRarity(value ?? (c.MixedJokers ?? c.Jokers)?.FirstOrDefault()),
                Jokers =
                    IsAnyWildcard(value ?? (c.MixedJokers ?? c.Jokers)?.FirstOrDefault())
                        ? []
                        : value != null
                            ? [RequireEnum<MotelyJoker>(value)]
                            : (c.MixedJokers ?? c.Jokers)?.Select(j => RequireEnum<MotelyJoker>(j)).ToArray() ?? [],
                Edition = edition,
                Stickers =
                    c.Stickers?.Select(s => RequireEnum<MotelyJokerSticker>(s)).ToArray()
                    ?? [],
                Sources = new JokerSourceConfig
                {
                    ShopItems = shopItems ?? [],
                    BoosterPacks = boosterPacks ?? [],
                    Judgement = c.Sources?.Judgement ?? [],
                    Wraith = c.Sources?.Wraith ?? [],
                    RiffRaff = c.Sources?.RiffRaff ?? [],
                    RareTag = c.Sources?.RareTag ?? [],
                    UncommonTag = c.Sources?.UncommonTag ?? [],
                    CommonShopJokers = c.Sources?.CommonShopJokers ?? [],
                    UncommonShopJokers = c.Sources?.UncommonShopJokers ?? [],
                    RareShopJokers = c.Sources?.RareShopJokers ?? [],
                    AllShopJokers = c.Sources?.AllShopJokers ?? [],
                },
            },
            MotelyFilterItemType.SoulJoker => new LegendaryJokerClause
            {
                Label = label,
                Score = score,
                Antes = antes,
                Min = min,
                IsWildcard = IsAnyWildcard(value ?? c.Jokers?.FirstOrDefault()),
                Jokers =
                    IsAnyWildcard(value ?? c.Jokers?.FirstOrDefault())
                        ? []
                        : value != null
                            ? [RequireEnum<MotelyJoker>(value)]
                            : c.Jokers?.Select(j => RequireEnum<MotelyJoker>(j)).ToArray() ?? [],
                Edition = edition,
                SoulCardOnly = c.SoulCardOnly ?? false,
                SoulEditionRolls = c.SoulEditionRolls ?? 0,
                Sources = CreateSoulJokerSources(
                    shopItems,
                    boosterPacks,
                    c.Sources?.ArcanaBoosterPacks,
                    c.Sources?.SpectralBoosterPacks,
                    c.Sources?.SoulCard,
                    c.Sources?.RequireMega ?? false
                ),
            },
            MotelyFilterItemType.Voucher => new VoucherClause
            {
                Label = label,
                Score = score,
                Antes = antes,
                Min = min,
                Vouchers =
                    value != null
                        ? [RequireEnum<MotelyVoucher>(value)]
                        : c.Vouchers?.Select(v => RequireEnum<MotelyVoucher>(v)).ToArray()
                            ?? [],
            },
            MotelyFilterItemType.TarotCard => new TarotCardClause
            {
                Label = label,
                Score = score,
                Antes = antes,
                Min = min,
                Tarots = value != null ? [RequireEnum<MotelyTarotCard>(value)] : [],
                Sources = new TarotCardSourceConfig
                {
                    ShopItems = shopItems ?? [],
                    BoosterPacks = boosterPacks ?? [],
                    Emperor = c.Sources?.Emperor ?? [],
                    PurpleSealOrEightBall = c.Sources?.PurpleSealOrEightBall ?? [],
                    CharmTag = c.Sources?.CharmTag ?? false,
                },
            },
            MotelyFilterItemType.SpectralCard => new SpectralCardClause
            {
                Label = label,
                Score = score,
                Antes = antes,
                Min = min,
                Spectrals = value != null ? [RequireEnum<MotelySpectralCard>(value)] : [],
                Sources = new SpectralCardSourceConfig
                {
                    ShopItems = shopItems ?? [],
                    BoosterPacks = boosterPacks ?? [],
                    SixthSense = c.Sources?.SixthSense ?? [],
                    Seance = c.Sources?.Seance ?? [],
                    EtherealTag = c.Sources?.EtherealTag ?? false,
                },
            },
            MotelyFilterItemType.PlanetCard => new PlanetCardClause
            {
                Label = label,
                Score = score,
                Antes = antes,
                Min = min,
                Planets = value != null ? [RequireEnum<MotelyPlanetCard>(value)] : [],
                Sources = new PlanetSourceConfig
                {
                    ShopItems = shopItems ?? [],
                    BoosterPacks = boosterPacks ?? [],
                },
            },
            MotelyFilterItemType.Boss => new BossClause
            {
                Label = label,
                Score = score,
                Antes = antes,
                Min = min,
                Bosses = value != null ? [RequireEnum<MotelyBossBlind>(value)] : [],
            },
            MotelyFilterItemType.SmallBlindTag => new TagClause
            {
                Label = label,
                Score = score,
                Antes = antes,
                Min = min,
                Tags = value != null ? [RequireEnum<MotelyTag>(value)] : [],
                Position = c.Tag != null || string.Equals(c.Type, "Tag", StringComparison.OrdinalIgnoreCase)
                    ? TagPosition.Any
                    : TagPosition.SmallBlind,
            },
            MotelyFilterItemType.BigBlindTag => new TagClause
            {
                Label = label,
                Score = score,
                Antes = antes,
                Min = min,
                Tags = value != null ? [RequireEnum<MotelyTag>(value)] : [],
                Position = TagPosition.BigBlind,
            },
            MotelyFilterItemType.PlayingCard => new StandardCardClause
            {
                Label = label,
                Score = score,
                Antes = antes,
                Min = min,
                Rank = ParseRank(c.Rank) ?? shRank,
                Suit = ParseSuit(c.Suit) ?? shSuit,
                Enhancement = ParseEnum<MotelyItemEnhancement>(c.Enhancement),
                Seal = ParseEnum<MotelyItemSeal>(c.Seal),
                Edition = edition,
                Sources = new StandardCardSourceConfig
                {
                    ShopItems = shopItems ?? [],
                    BoosterPacks = boosterPacks ?? [],
                    Certificate = c.Sources?.Certificate ?? [],
                    Incantation = c.Sources?.Incantation ?? [],
                    Familiar = c.Sources?.Familiar ?? [],
                    Grim = c.Sources?.Grim ?? [],
                    DeckDraw = c.Sources?.DeckDraw ?? [],
                },
            },
            MotelyFilterItemType.ErraticRank => new ErraticRankClause
            {
                Label = label,
                Score = score,
                Antes = antes,
                Min = min,
                Rank =
                    ParseRank(c.Rank ?? value)
                    ?? throw new NotSupportedException("ErraticRank clause requires a rank value."),
            },
            MotelyFilterItemType.ErraticSuit => new ErraticSuitClause
            {
                Label = label,
                Score = score,
                Antes = antes,
                Min = min,
                Suit =
                    ParseSuit(c.Suit ?? value)
                    ?? throw new NotSupportedException("ErraticSuit clause requires a suit value."),
            },
            MotelyFilterItemType.ErraticCard => CreateErraticCardClause(
                c,
                value,
                antes,
                min,
                score
            ),
            MotelyFilterItemType.StartingDraw => new StartingDrawClause
            {
                Label = label,
                Score = score,
                Antes = antes,
                Min = min,
                Rank = ParseRank(c.Rank) ?? shRank,
                Suit = ParseSuit(c.Suit) ?? shSuit,
            },
            MotelyFilterItemType.Event => CreateEventClause(
                c.Event ?? value,
                ResolveEventRolls(c),
                min,
                score,
                label,
                hasUserSpecifiedAntes
            ),
            _ => throw new NotSupportedException($"Unsupported clause type: {itemType}"),
        };
    }

    private static ErraticCardClause CreateErraticCardClause(
        JamlClauseDto c,
        string? value,
        int[] antes,
        int min,
        int score
    )
    {
        var (shRank, shSuit) = ParseCardShorthand(value ?? "");
        var rank = ParseRank(c.Rank) ?? shRank;
        var suit = ParseSuit(c.Suit) ?? shSuit;

        if (rank != null && suit != null)
        {
            return new ErraticCardClause
            {
                Score = score,
                Antes = antes,
                Min = min,
                Rank = rank.Value,
                Suit = suit.Value,
            };
        }
        // If not both, we can't create ErraticCardClause. But ResolveType logic mapped rank-only to ErraticRank, etc.
        // If we got here with itemType == ErraticCard, we expect both OR explicit 'ErraticCard' type tag.
        // But if user provided Rank OR Suit only, ResolveType mapped to ErraticRank/Suit.
        // So this method ONLY handles the true ErraticCard case (both present).
        // Wait, ResolveType maps c.ErraticCard != null -> ErraticCard.
        // What if c.ErraticCard (key) is used but only rank provided?
        // We should throw or handle.
        // I will throw if incomplete, because if it was mapped here, user intended ErraticCard.

        if (c.Type == "ErraticCard" && (rank == null || suit == null))
            throw new NotSupportedException("ErraticCard clause requires both Rank and Suit.");

        throw new NotSupportedException("ErraticCard clause requires rank and suit.");
    }

    private static IRollClause CreateEventClause(
        string? eventName,
        int[]? rolls,
        int min,
        int score,
        string label,
        bool hasUserSpecifiedAntes
    )
    {
        if (string.IsNullOrEmpty(eventName))
            throw new NotSupportedException("Event clause is missing event type name.");
        if (hasUserSpecifiedAntes)
            throw new NotSupportedException(
                "Event clauses do not support 'antes'. Remove 'antes' from the event clause, enclosing logic block, or defaults section."
            );

        var r = (rolls == null || rolls.Length == 0) ? new int[] { 0 } : rolls;
        return Enum.Parse<MotelyEventType>(eventName, true) switch
        {
            MotelyEventType.LuckyMoney => new LuckyMoneyClause
            {
                Label = label,
                Score = score,
                Min = min,
                Rolls = r,
            },
            MotelyEventType.LuckyMult => new LuckyMultClause
            {
                Label = label,
                Score = score,
                Min = min,
                Rolls = r,
            },
            MotelyEventType.MisprintMult => new MisprintMultClause
            {
                Label = label,
                Score = score,
                Min = min,
                Rolls = r,
            },
            MotelyEventType.WheelOfFortune => new WheelOfFortuneClause
            {
                Label = label,
                Score = score,
                Min = min,
                Rolls = r,
            },
            MotelyEventType.CavendishExtinct => new CavendishExtinctClause
            {
                Label = label,
                Score = score,
                Min = min,
                Rolls = r,
            },
            MotelyEventType.GrosMichelExtinct => new GrosMichelExtinctClause
            {
                Label = label,
                Score = score,
                Min = min,
                Rolls = r,
            },
            _ => throw new NotSupportedException($"Unsupported event type: {eventName}"),
        };
    }

    private static int[]? ResolveEventRolls(JamlClauseDto c) =>
        c.Rolls
        ?? c.LuckyMoney
        ?? c.LuckyMult
        ?? c.MisprintMult
        ?? c.WheelOfFortune
        ?? c.CavendishExtinct
        ?? c.GrosMichelExtinct;

    private static SoulJokerSourceConfig CreateSoulJokerSources(
        int[]? shopItems,
        int[]? boosterPacks,
        int[]? arcanaBoosterPacks,
        int[]? spectralBoosterPacks,
        int[]? soulCard,
        bool requireMegaPack
    )
    {
        var arcana = arcanaBoosterPacks ?? [];
        var spectral = spectralBoosterPacks ?? [];
        bool split = arcana.Length > 0 || spectral.Length > 0;
        return new SoulJokerSourceConfig
        {
            ShopItems = shopItems ?? [],
            BoosterPacks = split ? [] : (boosterPacks ?? []),
            ArcanaBoosterPacks = arcana,
            SpectralBoosterPacks = spectral,
            SoulCard = soulCard ?? [],
            RequireMegaPack = requireMegaPack,
        };
    }

    private static void NormalizeDefaultSources(
        ref int[]? shopItems,
        ref int[]? boosterPacks,
        MotelyFilterItemType itemType,
        JamlSourcesDto? sources
    )
    {
        if (shopItems != null || boosterPacks != null)
            return;

        if (HasSpecialtySources(sources))
            return;

        switch (itemType)
        {
            case MotelyFilterItemType.Joker:
            case MotelyFilterItemType.CommonJoker:
            case MotelyFilterItemType.UncommonJoker:
            case MotelyFilterItemType.RareJoker:
            case MotelyFilterItemType.MixedJoker:
                shopItems = [0, 1, 2, 3];
                boosterPacks = [0, 1, 2, 3, 4, 5];
                break;

            case MotelyFilterItemType.SoulJoker:
                boosterPacks = [0, 1, 2, 3, 4, 5];
                break;
        }
    }

    private static bool HasSpecialtySources(JamlSourcesDto? sources)
    {
        if (sources == null) return false;
        return (sources.Judgement?.Length ?? 0) > 0
            || (sources.Wraith?.Length ?? 0) > 0
            || (sources.RiffRaff?.Length ?? 0) > 0
            || (sources.RareTag?.Length ?? 0) > 0
            || (sources.UncommonTag?.Length ?? 0) > 0
            || (sources.CommonShopJokers?.Length ?? 0) > 0
            || (sources.UncommonShopJokers?.Length ?? 0) > 0
            || (sources.RareShopJokers?.Length ?? 0) > 0
            || (sources.AllShopJokers?.Length ?? 0) > 0;
    }

    private static string GenerateLabel(JamlClauseDto c)
    {
        if (c.Joker != null) return c.Joker;
        if (c.Jokers is { Count: > 0 } jj) return string.Join(", ", jj);
        if (c.CommonJoker != null) return c.CommonJoker;
        if (c.CommonJokers is { Count: > 0 } cj) return string.Join(", ", cj);
        if (c.UncommonJoker != null) return c.UncommonJoker;
        if (c.UncommonJokers is { Count: > 0 } uj) return string.Join(", ", uj);
        if (c.RareJoker != null) return c.RareJoker;
        if (c.RareJokers is { Count: > 0 } rj) return string.Join(", ", rj);
        if (c.MixedJoker != null) return c.MixedJoker;
        if (c.MixedJokers is { Count: > 0 } mj) return string.Join(", ", mj);
        if (c.SoulJoker != null) return c.SoulJoker;
        if (c.LegendaryJoker != null) return c.LegendaryJoker;
        if (c.Voucher != null) return c.Voucher;
        if (c.Vouchers is { Count: > 0 } vv) return string.Join(", ", vv);
        if (c.Tarot != null) return c.Tarot;
        if (c.TarotCard != null) return c.TarotCard;
        if (c.Spectral != null) return c.Spectral;
        if (c.SpectralCard != null) return c.SpectralCard;
        if (c.Planet != null) return c.Planet;
        if (c.PlanetCard != null) return c.PlanetCard;
        if (c.Boss != null) return c.Boss;
        if (c.Tag != null) return c.Tag;
        if (c.SmallBlindTag != null) return c.SmallBlindTag;
        if (c.BigBlindTag != null) return c.BigBlindTag;
        if (c.StandardCard != null) return c.StandardCard;
        if (c.ErraticRank != null) return c.ErraticRank;
        if (c.ErraticSuit != null) return c.ErraticSuit;
        if (c.ErraticCard != null) return c.ErraticCard;
        if (c.StartingDraw != null) return c.StartingDraw;
        if (c.Event != null) return c.Event;
        if (c.LuckyMoney != null) return "luckyMoney";
        if (c.LuckyMult != null) return "luckyMult";
        if (c.MisprintMult != null) return "misprintMult";
        if (c.WheelOfFortune != null) return "wheelOfFortune";
        if (c.CavendishExtinct != null) return "cavendishExtinct";
        if (c.GrosMichelExtinct != null) return "grosMichelExtinct";
        if (c.Type != null) return c.Value ?? c.Type;
        return "clause";
    }

    // ── Resolve type from shorthand keys or explicit type field ──

    private static (MotelyFilterItemType itemType, string? value) ResolveType(JamlClauseDto c)
    {
        // Shorthand keys (type-as-key) — check each one
        if (c.Joker != null)
            return (MotelyFilterItemType.Joker, c.Joker);
        if (c.Jokers != null)
            return (MotelyFilterItemType.Joker, null); // plural
        if (c.CommonJoker != null)
            return (MotelyFilterItemType.CommonJoker, c.CommonJoker);
        if (c.CommonJokers != null)
            return (MotelyFilterItemType.CommonJoker, null);
        if (c.UncommonJoker != null)
            return (MotelyFilterItemType.UncommonJoker, c.UncommonJoker);
        if (c.UncommonJokers != null)
            return (MotelyFilterItemType.UncommonJoker, null);
        if (c.RareJoker != null)
            return (MotelyFilterItemType.RareJoker, c.RareJoker);
        if (c.RareJokers != null)
            return (MotelyFilterItemType.RareJoker, null);
        if (c.MixedJoker != null)
            return (MotelyFilterItemType.MixedJoker, c.MixedJoker);
        if (c.MixedJokers != null)
            return (MotelyFilterItemType.MixedJoker, null);
        if (c.SoulJoker != null)
            return (MotelyFilterItemType.SoulJoker, c.SoulJoker);
        if (c.LegendaryJoker != null)
            return (MotelyFilterItemType.SoulJoker, c.LegendaryJoker);
        if (c.Voucher != null)
            return (MotelyFilterItemType.Voucher, c.Voucher);
        if (c.Vouchers != null)
            return (MotelyFilterItemType.Voucher, null);
        if (c.Tarot != null)
            return (MotelyFilterItemType.TarotCard, c.Tarot);
        if (c.TarotCard != null)
            return (MotelyFilterItemType.TarotCard, c.TarotCard);
        if (c.Spectral != null)
            return (MotelyFilterItemType.SpectralCard, c.Spectral);
        if (c.SpectralCard != null)
            return (MotelyFilterItemType.SpectralCard, c.SpectralCard);
        if (c.Planet != null)
            return (MotelyFilterItemType.PlanetCard, c.Planet);
        if (c.PlanetCard != null)
            return (MotelyFilterItemType.PlanetCard, c.PlanetCard);
        if (c.Boss != null)
            return (MotelyFilterItemType.Boss, c.Boss);
        if (c.Tag != null)
            return (MotelyFilterItemType.SmallBlindTag, c.Tag);
        if (c.SmallBlindTag != null)
            return (MotelyFilterItemType.SmallBlindTag, c.SmallBlindTag);
        if (c.BigBlindTag != null)
            return (MotelyFilterItemType.BigBlindTag, c.BigBlindTag);
        if (c.StandardCard != null)
            return (MotelyFilterItemType.PlayingCard, c.StandardCard);
        if (c.ErraticRank != null)
            return (MotelyFilterItemType.ErraticRank, c.ErraticRank);
        if (c.ErraticSuit != null)
            return (MotelyFilterItemType.ErraticSuit, c.ErraticSuit);
        if (c.ErraticCard != null)
            return (MotelyFilterItemType.ErraticCard, c.ErraticCard);
        if (c.StartingDraw != null)
            return (MotelyFilterItemType.StartingDraw, c.StartingDraw);
        if (c.Event != null)
            return (MotelyFilterItemType.Event, c.Event);
        if (c.LuckyMoney != null)
            return (MotelyFilterItemType.Event, nameof(MotelyEventType.LuckyMoney));
        if (c.LuckyMult != null)
            return (MotelyFilterItemType.Event, nameof(MotelyEventType.LuckyMult));
        if (c.MisprintMult != null)
            return (MotelyFilterItemType.Event, nameof(MotelyEventType.MisprintMult));
        if (c.WheelOfFortune != null)
            return (MotelyFilterItemType.Event, nameof(MotelyEventType.WheelOfFortune));
        if (c.CavendishExtinct != null)
            return (MotelyFilterItemType.Event, nameof(MotelyEventType.CavendishExtinct));
        if (c.GrosMichelExtinct != null)
            return (MotelyFilterItemType.Event, nameof(MotelyEventType.GrosMichelExtinct));

        // Explicit type+value
        if (c.Type != null)
        {
            var itemType = ParseItemType(c.Type);
            return (itemType, c.Value ?? c.EventType);
        }

        throw new InvalidOperationException("Clause is missing a recognized clause key or type.");
    }

    private static MotelyFilterItemType ParseItemType(string type) =>
        type switch
        {
            "Joker" => MotelyFilterItemType.Joker,
            "CommonJoker" => MotelyFilterItemType.CommonJoker,
            "UncommonJoker" => MotelyFilterItemType.UncommonJoker,
            "RareJoker" => MotelyFilterItemType.RareJoker,
            "MixedJoker" => MotelyFilterItemType.MixedJoker,
            "SoulJoker" => MotelyFilterItemType.SoulJoker,
            "Voucher" => MotelyFilterItemType.Voucher,
            "TarotCard" => MotelyFilterItemType.TarotCard,
            "Planet" or "PlanetCard" => MotelyFilterItemType.PlanetCard,
            "Spectral" or "SpectralCard" => MotelyFilterItemType.SpectralCard,
            "Boss" or "BossBlind" => MotelyFilterItemType.Boss,
            "Tag" => MotelyFilterItemType.SmallBlindTag,
            "SmallBlindTag" => MotelyFilterItemType.SmallBlindTag,
            "BigBlindTag" => MotelyFilterItemType.BigBlindTag,
            "StandardCard" => MotelyFilterItemType.PlayingCard,
            "Event"
            or "LuckyMoney"
            or "LuckyMult"
            or "MisprintMult"
            or "WheelOfFortune"
            or "CavendishExtinct"
            or "GrosMichelExtinct" => MotelyFilterItemType.Event,
            "ErraticRank" => MotelyFilterItemType.ErraticRank,
            "ErraticSuit" => MotelyFilterItemType.ErraticSuit,
            "ErraticCard" => MotelyFilterItemType.ErraticCard,
            "StartingDraw" => MotelyFilterItemType.StartingDraw,
            _ => throw new NotSupportedException($"Unknown clause type: {type}"),
        };

    // ── Helpers ──

    private static string NormalizeFilterId(string? explicitId, string? name)
    {
        var source = string.IsNullOrWhiteSpace(explicitId) ? name : explicitId;
        if (string.IsNullOrWhiteSpace(source))
            return "unnamed";

        var normalized = Regex.Replace(source.Trim(), "[^A-Za-z0-9_-]+", "-");
        normalized = Regex.Replace(normalized, "-+", "-").Trim('-', '_');

        return string.IsNullOrWhiteSpace(normalized) ? "unnamed" : normalized.ToLowerInvariant();
    }

    private static List<string> NormalizeHashtags(List<string>? hashtags)
    {
        if (hashtags is not { Count: > 0 })
            return [];

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var normalized = new List<string>();

        foreach (var entry in hashtags)
        {
            if (string.IsNullOrWhiteSpace(entry))
                continue;

            var tag = entry.Trim();
            if (tag.StartsWith('#'))
                tag = tag[1..];

            if (string.IsNullOrWhiteSpace(tag))
                continue;

            tag = tag.ToLowerInvariant();
            if (seen.Add(tag))
                normalized.Add(tag);
        }

        return normalized;
    }

    private static string NormalizeItemName(string name) =>
        name.Replace(" ", "").Replace("'", "").Replace(".", "");

    private static T RequireEnum<T>(string value)
        where T : struct, Enum =>
        Enum.Parse<T>(NormalizeItemName(value), true);

    private static T? ParseEnum<T>(string? value)
        where T : struct, Enum =>
        value != null && Enum.TryParse<T>(NormalizeItemName(value), true, out var result) ? result : null;

    private static MotelyPlayingCardRank? ParseRank(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return null;
        return value.ToUpperInvariant() switch
        {
            "2" => MotelyPlayingCardRank.Two,
            "3" => MotelyPlayingCardRank.Three,
            "4" => MotelyPlayingCardRank.Four,
            "5" => MotelyPlayingCardRank.Five,
            "6" => MotelyPlayingCardRank.Six,
            "7" => MotelyPlayingCardRank.Seven,
            "8" => MotelyPlayingCardRank.Eight,
            "9" => MotelyPlayingCardRank.Nine,
            "10" or "T" => MotelyPlayingCardRank.Ten,
            "J" => MotelyPlayingCardRank.Jack,
            "Q" => MotelyPlayingCardRank.Queen,
            "K" => MotelyPlayingCardRank.King,
            "A" => MotelyPlayingCardRank.Ace,
            _ => ParseEnum<MotelyPlayingCardRank>(value),
        };
    }

    private static MotelyPlayingCardSuit? ParseSuit(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return null;
        return value.ToUpperInvariant() switch
        {
            "C" or "CLUBS" => MotelyPlayingCardSuit.Clubs,
            "D" or "DIAMONDS" => MotelyPlayingCardSuit.Diamonds,
            "H" or "HEARTS" => MotelyPlayingCardSuit.Hearts,
            "S" or "SPADES" => MotelyPlayingCardSuit.Spades,
            _ => ParseEnum<MotelyPlayingCardSuit>(value),
        };
    }

    private static (MotelyPlayingCardRank? rank, MotelyPlayingCardSuit? suit) ParseCardShorthand(
        string value
    )
    {
        if (string.IsNullOrEmpty(value))
            return (null, null);
        if (Enum.TryParse<MotelyPlayingCard>(value, true, out var card))
        {
            return (card.GetRank(), card.GetSuit());
        }
        return (null, null);
    }

    private static bool IsAnyWildcard(string? v) =>
        v != null && (
            string.Equals(v, "any", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(v, "anycommon", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(v, "anyuncommon", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(v, "anyrare", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(v, "anylegendary", StringComparison.OrdinalIgnoreCase)
        );

    private static MotelyJokerRarity? ParseWildcardRarity(string? v)
    {
        if (string.Equals(v, "anycommon", StringComparison.OrdinalIgnoreCase)) return MotelyJokerRarity.Common;
        if (string.Equals(v, "anyuncommon", StringComparison.OrdinalIgnoreCase)) return MotelyJokerRarity.Uncommon;
        if (string.Equals(v, "anyrare", StringComparison.OrdinalIgnoreCase)) return MotelyJokerRarity.Rare;
        if (string.Equals(v, "anylegendary", StringComparison.OrdinalIgnoreCase)) return MotelyJokerRarity.Legendary;
        return null;
    }
}

public sealed class JokerSource
{
    public JokerSourceType Source { get; set; }
    public int[] Indices { get; set; } = [];
}

public enum JokerSourceType
{
    Shop,
    BoosterPack,
    Judgement,
    Wraith,
    RiffRaff,
    RareTag,
    UncommonTag,
}

public sealed class JokerSourceConfig
{
    /// <summary>Assembled shop slots via the full shop item stream (any item type).</summary>
    public int[] ShopItems { get; set; } = [];
    public int[] BoosterPacks { get; set; } = [];
    public int[] Judgement { get; set; } = [];
    public int[] Wraith { get; set; } = [];
    public int[] RiffRaff { get; set; } = [];
    public int[] RareTag { get; set; } = [];
    public int[] UncommonTag { get; set; } = [];
    /// <summary>0..n rolls on the common shop joker PRNG only (fast path).</summary>
    public int[] CommonShopJokers { get; set; } = [];
    /// <summary>0..n rolls on the uncommon shop joker PRNG only (fast path; not the same indices as <see cref="ShopItems"/> when slots mix types).</summary>
    public int[] UncommonShopJokers { get; set; } = [];
    /// <summary>0..n rolls on the rare shop joker PRNG only (fast path).</summary>
    public int[] RareShopJokers { get; set; } = [];
    /// <summary>0..n rolls on the all-rarity shop joker stream (fast path).</summary>
    public int[] AllShopJokers { get; set; } = [];
}

public sealed class SoulJokerSourceConfig
{
    public int[] ShopItems { get; set; } = [];

    /// <summary>
    /// Legacy: pack offering slots where The Soul may count from either arcana or spectral path.
    /// Ignored for slot matching when <see cref="ArcanaBoosterPacks"/> or <see cref="SpectralBoosterPacks"/> is non-empty.
    /// </summary>
    public int[] BoosterPacks { get; set; } = [];

    /// <summary>
    /// If non-empty (or <see cref="SpectralBoosterPacks"/> non-empty), only listed slots are checked on the arcana/tarot path.
    /// </summary>
    public int[] ArcanaBoosterPacks { get; set; } = [];

    /// <summary>Only listed slots on the spectral pack path.</summary>
    public int[] SpectralBoosterPacks { get; set; } = [];

    public int[] SoulCard { get; set; } = [];

    /// <summary>If true, only Mega-sized booster packs (e.g. Charm Tag Mega arcana) match.</summary>
    public bool RequireMegaPack { get; set; }

    /// <summary>Largest referenced pack slot index across all booster source arrays (-1 if none).</summary>
    public int MaxReferencedBoosterSlot()
    {
        int m = -1;
        foreach (var x in BoosterPacks)
            if (x > m)
                m = x;
        foreach (var x in ArcanaBoosterPacks)
            if (x > m)
                m = x;
        foreach (var x in SpectralBoosterPacks)
            if (x > m)
                m = x;
        return m;
    }

    /// <summary>
    /// When no booster slot lists are set, soul/legendary matching uses slots 0..5 (same as JAML <c>defaults:</c>).
    /// </summary>
    public SoulJokerSourceConfig NormalizeSoulJokerBoostersIfEmpty()
    {
        if (BoosterPacks.Length > 0 || ArcanaBoosterPacks.Length > 0 || SpectralBoosterPacks.Length > 0)
            return this;

        return new SoulJokerSourceConfig
        {
            ShopItems = ShopItems,
            BoosterPacks = [0, 1, 2, 3, 4, 5],
            ArcanaBoosterPacks = [],
            SpectralBoosterPacks = [],
            SoulCard = SoulCard,
            RequireMegaPack = RequireMegaPack,
        };
    }
}

public sealed class TarotCardSourceConfig
{
    public int[] ShopItems { get; set; } = [];
    public int[] BoosterPacks { get; set; } = [];
    public int[] Emperor { get; set; } = [];
    public int[] PurpleSealOrEightBall { get; set; } = [];

    /// <summary>
    /// When true, booster arcana scoring may consume the Charm-tag bonus pack (second weighted slot, no natural Arcana).
    /// </summary>
    public bool CharmTag { get; set; }
}

public sealed class SpectralCardSourceConfig
{
    public int[] ShopItems { get; set; } = [];
    public int[] BoosterPacks { get; set; } = [];
    public int[] SixthSense { get; set; } = [];
    public int[] Seance { get; set; } = [];

    /// <summary>
    /// When true, booster spectral scoring may consume the Ethereal-tag bonus pack (second weighted slot, no natural Spectral).
    /// </summary>
    public bool EtherealTag { get; set; }
}

public sealed class PlanetSourceConfig
{
    public int[] ShopItems { get; set; } = [];
    public int[] BoosterPacks { get; set; } = [];
}

public sealed class StandardCardSourceConfig
{
    public int[] ShopItems { get; set; } = [];
    public int[] BoosterPacks { get; set; } = [];
    public int[] Certificate { get; set; } = [];
    public int[] Incantation { get; set; } = [];
    public int[] Familiar { get; set; } = [];
    public int[] Grim { get; set; } = [];
    public int[] DeckDraw { get; set; } = [];
}
