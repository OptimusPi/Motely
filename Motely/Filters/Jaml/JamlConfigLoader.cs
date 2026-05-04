using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Motely.Filters;

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
                Id = NormalizeFilterId(load.Id, load.Name),
                Name = load.Name,
                Description = load.Description,
                Author = load.Author,
                Deck = deck,
                Stake = stake,
            };

            config.Hashtags = NormalizeHashtags(load.Hashtags);
            config.Seeds = NormalizeSeeds(load.Seeds);

            // MUST → required filters
            PopulateClauses(config.Must, load.Must, defaultAntes, load.Defaults);

            // SHOULD → scoring clauses
            PopulateClauses(config.Should, load.Should, defaultAntes, load.Defaults);

            // MUSTNOT → negation filters
            PopulateClauses(config.MustNot, load.MustNot, defaultAntes, load.Defaults);

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
                Id = NormalizeFilterId(load.Id, load.Name),
                Deck = deck,
                Stake = stake,
            };
            config.Seeds = NormalizeSeeds(load.Seeds);

            PopulateClauses(config.Must, load.Must, defaultAntes, load.Defaults);
            PopulateClauses(config.Should, load.Should, defaultAntes, load.Defaults);
            PopulateClauses(config.MustNot, load.MustNot, defaultAntes, load.Defaults);

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
            case SpaceLevelupClause c:
                set.SpaceLevelup.Add(c);
                break;
            case BusinessPayoutClause c:
                set.BusinessPayout.Add(c);
                break;
            case BloodstoneTriggerClause c:
                set.BloodstoneTrigger.Add(c);
                break;
            case ParkingPayoutClause c:
                set.ParkingPayout.Add(c);
                break;
            case GlassDestroyClause c:
                set.GlassDestroy.Add(c);
                break;
            case WheelStaysFlippedClause c:
                set.WheelStaysFlipped.Add(c);
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
        int? max = c.Max;
        int score = c.Score ?? 1;
        var label = c.Label ?? GenerateLabel(c);

        if (c.And != null)
        {
            var children = c.And ?? c.Clauses ?? [];

            return new AndClause
            {
                Label = label,
                Score = score,
                Max = max,
                Clauses = children
                    .Select(sub => CreateClauseFromDto(sub, antes, defaults, hasUserSpecifiedAntes))
                    .ToArray(),
            };
        }

        if (c.Or != null)
        {
            var children = c.Or ?? c.Clauses ?? [];

            return new OrClause
            {
                Label = label,
                Score = score,
                Min = min,
                Max = max,
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
                Max = max,
                Clauses = c.Clauses
                    .Select(sub => CreateClauseFromDto(sub, antes, defaults, hasUserSpecifiedAntes))
                    .ToArray(),
            };
        }

        var (itemType, value) = ResolveType(c);
        var edition = c.Edition;

        var shopItems = c.Sources?.ShopItems ?? c.ShopItems ?? defaults?.ShopItems;
        var boosterPacks = c.Sources?.BoosterPacks ?? c.BoosterPacks ?? defaults?.BoosterPacks;

        var minShop = c.Sources?.MinShopItem ?? c.MinShopItem;
        var maxShop = c.Sources?.MaxShopItem ?? c.MaxShopItem;

        if (shopItems == null && minShop != null && maxShop != null)
            shopItems = Enumerable
                .Range(minShop.Value, maxShop.Value - minShop.Value + 1)
                .ToArray();

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
                Max = max,
                IsWildcard = c.Joker?.IsAny ?? false,
                Jokers =
                    c.Joker is { IsAny: false, Value: var jv }
                        ? [jv]
                        : c.Jokers?.ToArray() ?? [],
                Edition = edition,
                Stickers = c.Stickers ?? [],
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
                    EarlyAntesMaxPack = c.Sources?.EarlyAntesMaxPack ?? MotelyGlobals.DefaultEarlyAntesMaxPack,
                },
                LegendarySources = CreateLegendaryJokerSources(
                    shopItems,
                    boosterPacks,
                    c.Sources?.ArcanaPacks,
                    c.Sources?.SpectralPacks,
                    c.Sources?.SoulCard,
                    c.Sources?.RequireMega ?? false,
                    c.Sources?.EarlyAntesMaxPack ?? MotelyGlobals.DefaultEarlyAntesMaxPack
                ),
            },
            MotelyFilterItemType.CommonJoker => new CommonJokerClause
            {
                Label = label,
                Score = score,
                Antes = antes,
                Min = min,
                Max = max,
                IsWildcard = c.CommonJoker?.IsAny ?? false,
                Jokers =
                    c.CommonJoker is { IsAny: false, Value: var cjv }
                        ? [cjv]
                        : c.CommonJokers?.ToArray() ?? [],
                Edition = edition,
                Stickers = c.Stickers ?? [],
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
                    EarlyAntesMaxPack = c.Sources?.EarlyAntesMaxPack ?? MotelyGlobals.DefaultEarlyAntesMaxPack,
                },
            },
            MotelyFilterItemType.UncommonJoker => new UncommonJokerClause
            {
                Label = label,
                Score = score,
                Antes = antes,
                Min = min,
                Max = max,
                IsWildcard = c.UncommonJoker?.IsAny ?? false,
                Jokers =
                    c.UncommonJoker is { IsAny: false, Value: var ujv }
                        ? [ujv]
                        : c.UncommonJokers?.ToArray() ?? [],
                Edition = edition,
                Stickers = c.Stickers ?? [],
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
                    EarlyAntesMaxPack = c.Sources?.EarlyAntesMaxPack ?? MotelyGlobals.DefaultEarlyAntesMaxPack,
                },
            },
            MotelyFilterItemType.RareJoker => new RareJokerClause
            {
                Label = label,
                Score = score,
                Antes = antes,
                Min = min,
                Max = max,
                IsWildcard = c.RareJoker?.IsAny ?? false,
                Jokers =
                    c.RareJoker is { IsAny: false, Value: var rjv }
                        ? [rjv]
                        : c.RareJokers?.ToArray() ?? [],
                Edition = edition,
                Stickers = c.Stickers ?? [],
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
                    EarlyAntesMaxPack = c.Sources?.EarlyAntesMaxPack ?? MotelyGlobals.DefaultEarlyAntesMaxPack,
                },
            },
            MotelyFilterItemType.MixedJoker => new MixedJokerClause
            {
                Label = label,
                Score = score,
                Antes = antes,
                Min = min,
                Max = max,
                IsWildcard = c.Joker?.IsAny ?? false,
                Jokers =
                    c.Joker is { IsAny: false, Value: var mjv }
                        ? [mjv]
                        : c.Jokers?.ToArray() ?? [],
                Edition = edition,
                Stickers = c.Stickers ?? [],
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
                    EarlyAntesMaxPack = c.Sources?.EarlyAntesMaxPack ?? MotelyGlobals.DefaultEarlyAntesMaxPack,
                },
            },
            MotelyFilterItemType.LegendaryJoker => new LegendaryJokerClause
            {
                Label = label,
                Score = score,
                Antes = antes,
                Min = min,
                Max = max,
                IsWildcard = c.LegendaryJoker?.IsAny ?? false,
                Jokers =
                    c.LegendaryJoker is { IsAny: false, Value: var lgv }
                        ? [ToMotelyJoker(lgv)]
                        : c.LegendaryJokers?.Select(ToMotelyJoker).ToArray() ?? [],
                Edition = edition,
                SoulCardOnly = c.SoulCardOnly ?? false,
                SoulEditionRolls = c.SoulEditionRolls ?? 0,
                Sources = CreateLegendaryJokerSources(
                    shopItems,
                    boosterPacks,
                    c.Sources?.ArcanaPacks,
                    c.Sources?.SpectralPacks,
                    c.Sources?.SoulCard,
                    c.Sources?.RequireMega ?? false,
                    c.Sources?.EarlyAntesMaxPack ?? MotelyGlobals.DefaultEarlyAntesMaxPack
                ),
            },
            MotelyFilterItemType.Voucher => new VoucherClause
            {
                Label = label,
                Score = score,
                Antes = antes,
                Min = min,
                Max = max,
                Vouchers =
                    c.Voucher is { } v
                        ? [v]
                        : c.Vouchers?.ToArray() ?? [],
            },
            MotelyFilterItemType.TarotCard => new TarotCardClause
            {
                Label = label,
                Score = score,
                Antes = antes,
                Min = min,
                Max = max,
                Tarots =
                    c.TarotCard is { } t
                        ? [t]
                        : c.TarotCards?.ToArray() ?? [],
                Sources = new TarotCardSourceConfig
                {
                    ShopItems = shopItems ?? [],
                    BoosterPacks = boosterPacks ?? [],
                    Emperor = c.Sources?.Emperor ?? [],
                    PurpleSealOrEightBall = c.Sources?.PurpleSealOrEightBall ?? [],
                    CharmTag = c.Sources?.CharmTag ?? false,
                    EarlyAntesMaxPack = c.Sources?.EarlyAntesMaxPack ?? MotelyGlobals.DefaultEarlyAntesMaxPack,
                },
            },
            MotelyFilterItemType.SpectralCard => new SpectralCardClause
            {
                Label = label,
                Score = score,
                Antes = antes,
                Min = min,
                Max = max,
                Spectrals =
                    c.SpectralCard is { } sp
                        ? [sp]
                        : c.SpectralCards?.ToArray() ?? [],
                Sources = new SpectralCardSourceConfig
                {
                    ShopItems = shopItems ?? [],
                    BoosterPacks = boosterPacks ?? [],
                    SixthSense = c.Sources?.SixthSense ?? [],
                    Seance = c.Sources?.Seance ?? [],
                    EtherealTag = c.Sources?.EtherealTag ?? false,
                    EarlyAntesMaxPack = c.Sources?.EarlyAntesMaxPack ?? MotelyGlobals.DefaultEarlyAntesMaxPack,
                },
            },
            MotelyFilterItemType.PlanetCard => new PlanetCardClause
            {
                Label = label,
                Score = score,
                Antes = antes,
                Min = min,
                Max = max,
                Planets = (c.Planet ?? c.PlanetCard) is { } p ? [p] : [],
                Sources = new PlanetSourceConfig
                {
                    ShopItems = shopItems ?? [],
                    BoosterPacks = boosterPacks ?? [],
                    EarlyAntesMaxPack = c.Sources?.EarlyAntesMaxPack ?? MotelyGlobals.DefaultEarlyAntesMaxPack,
                },
            },
            MotelyFilterItemType.Boss => new BossClause
            {
                Label = label,
                Score = score,
                Antes = antes,
                Min = min,
                Max = max,
                Bosses = c.Boss is { } b ? [b] : [],
            },
            MotelyFilterItemType.SmallBlindTag => new TagClause
            {
                Label = label,
                Score = score,
                Antes = antes,
                Min = min,
                Max = max,
                Tags = (c.Tag ?? c.SmallBlindTag) is { } sbt ? [sbt] : [],
                Position = c.Tag != null
                    ? TagPosition.Any
                    : TagPosition.SmallBlind,
            },
            MotelyFilterItemType.BigBlindTag => new TagClause
            {
                Label = label,
                Score = score,
                Antes = antes,
                Min = min,
                Max = max,
                Tags = c.BigBlindTag is { } bbt ? [bbt] : [],
                Position = TagPosition.BigBlind,
            },
            MotelyFilterItemType.Standardcard => new StandardCardClause
            {
                Label = label,
                Score = score,
                Antes = antes,
                Min = min,
                Max = max,
                Cards = ResolveStandardCardTargets(c, shRank, shSuit, edition),
                Sources = new StandardCardSourceConfig
                {
                    ShopItems = c.StandardCard?.ObjectValue?.Sources?.ShopItems ?? shopItems ?? [],
                    BoosterPacks = c.StandardCard?.ObjectValue?.Sources?.BoosterPacks ?? boosterPacks ?? [],
                    Certificate = c.StandardCard?.ObjectValue?.Sources?.Certificate ?? c.Sources?.Certificate ?? [],
                    Incantation = c.StandardCard?.ObjectValue?.Sources?.Incantation ?? c.Sources?.Incantation ?? [],
                    Familiar = c.StandardCard?.ObjectValue?.Sources?.Familiar ?? c.Sources?.Familiar ?? [],
                    Grim = c.StandardCard?.ObjectValue?.Sources?.Grim ?? c.Sources?.Grim ?? [],
                    DeckDraw = c.StandardCard?.ObjectValue?.Sources?.DeckDraw ?? c.Sources?.DeckDraw ?? [],
                },
            },
            MotelyFilterItemType.ErraticRank => new ErraticRankClause
            {
                Label = label,
                Score = score,
                Antes = antes,
                Min = min,
                Max = max,
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
                Max = max,
                Suit =
                    ParseSuit(c.Suit ?? value)
                    ?? throw new NotSupportedException("ErraticSuit clause requires a suit value."),
            },
            MotelyFilterItemType.ErraticCard => CreateErraticCardClause(
                c,
                value,
                antes,
                min,
                score,
                max
            ),
            MotelyFilterItemType.StartingDraw => new StartingDrawClause
            {
                Label = label,
                Score = score,
                Antes = antes,
                Min = min,
                Max = max,
                Rank = ParseRank(c.Rank) ?? shRank,
                Suit = ParseSuit(c.Suit) ?? shSuit,
            },
            MotelyFilterItemType.Event => CreateEventClause(
                ResolveEventType(c),
                ResolveEventRolls(c),
                min,
                score,
                max,
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
        int score,
        int? max
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
                Max = max,
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
        throw new NotSupportedException("ErraticCard clause requires both Rank and Suit.");
    }

    private static StandardCardTarget[] ResolveStandardCardTargets(
        JamlClauseDto c,
        MotelyStandardcardRank? shorthandRank,
        MotelyStandardcardSuit? shorthandSuit,
        MotelyItemEdition? clauseEdition
    )
    {
        if (c.StandardCards is { Count: > 0 } cards)
        {
            return cards.Select(card =>
            {
                var (rank, suit) = ParseCardShorthand(card.StringValue ?? string.Empty);
                return new StandardCardTarget
                {
                    Rank = ParseRank(card.ObjectValue?.Rank) ?? rank,
                    Suit = ParseSuit(card.ObjectValue?.Suit) ?? suit,
                    Enhancement = card.ObjectValue?.Enhancement,
                    Seal = card.ObjectValue?.Seal,
                    Edition = card.ObjectValue?.Edition ?? clauseEdition,
                };
            }).ToArray();
        }

        return
        [
            new StandardCardTarget
            {
                Rank = ParseRank(c.StandardCard?.ObjectValue?.Rank ?? c.Rank) ?? shorthandRank,
                Suit = ParseSuit(c.StandardCard?.ObjectValue?.Suit ?? c.Suit) ?? shorthandSuit,
                Enhancement = c.StandardCard?.ObjectValue?.Enhancement ?? c.Enhancement,
                Seal = c.StandardCard?.ObjectValue?.Seal ?? c.Seal,
                Edition = c.StandardCard?.ObjectValue?.Edition ?? clauseEdition,
            },
        ];
    }

    private static IRollClause CreateEventClause(
        MotelyEventType? eventType,
        int[]? rolls,
        int min,
        int score,
        int? max,
        string label,
        bool hasUserSpecifiedAntes
    )
    {
        if (eventType is null)
            throw new NotSupportedException("Event clause is missing event type name.");
        if (hasUserSpecifiedAntes)
            throw new NotSupportedException(
                "Event clauses do not support 'antes'. Remove 'antes' from the event clause, enclosing logic block, or defaults section."
            );

        var r = (rolls == null || rolls.Length == 0) ? new int[] { 0 } : rolls;
        return eventType.Value switch
        {
            MotelyEventType.LuckyMoney => new LuckyMoneyClause
            {
                Label = label,
                Score = score,
                Min = min,
                Max = max,
                Rolls = r,
            },
            MotelyEventType.LuckyMult => new LuckyMultClause
            {
                Label = label,
                Score = score,
                Min = min,
                Max = max,
                Rolls = r,
            },
            MotelyEventType.MisprintMult => new MisprintMultClause
            {
                Label = label,
                Score = score,
                Min = min,
                Max = max,
                Rolls = r,
            },
            MotelyEventType.WheelOfFortune => new WheelOfFortuneClause
            {
                Label = label,
                Score = score,
                Min = min,
                Max = max,
                Rolls = r,
            },
            MotelyEventType.CavendishExtinct => new CavendishExtinctClause
            {
                Label = label,
                Score = score,
                Min = min,
                Max = max,
                Rolls = r,
            },
            MotelyEventType.GrosMichelExtinct => new GrosMichelExtinctClause
            {
                Label = label,
                Score = score,
                Min = min,
                Max = max,
                Rolls = r,
            },
            MotelyEventType.SpaceLevelup => new SpaceLevelupClause
            {
                Label = label,
                Score = score,
                Min = min,
                Max = max,
                Rolls = r,
            },
            MotelyEventType.BusinessPayout => new BusinessPayoutClause
            {
                Label = label,
                Score = score,
                Min = min,
                Max = max,
                Rolls = r,
            },
            MotelyEventType.BloodstoneTrigger => new BloodstoneTriggerClause
            {
                Label = label,
                Score = score,
                Min = min,
                Max = max,
                Rolls = r,
            },
            MotelyEventType.ParkingPayout => new ParkingPayoutClause
            {
                Label = label,
                Score = score,
                Min = min,
                Max = max,
                Rolls = r,
            },
            MotelyEventType.GlassDestroy => new GlassDestroyClause
            {
                Label = label,
                Score = score,
                Min = min,
                Max = max,
                Rolls = r,
            },
            MotelyEventType.WheelStaysFlipped => new WheelStaysFlippedClause
            {
                Label = label,
                Score = score,
                Min = min,
                Max = max,
                Rolls = r,
            },
            _ => throw new NotSupportedException($"Unsupported event type: {eventType}"),
        };
    }

    private static MotelyEventType? ResolveEventType(JamlClauseDto c) =>
        c.Event
        ?? (c.LuckyMoney != null ? MotelyEventType.LuckyMoney
            : c.LuckyMult != null ? MotelyEventType.LuckyMult
            : c.MisprintMult != null ? MotelyEventType.MisprintMult
            : c.WheelOfFortune != null ? MotelyEventType.WheelOfFortune
            : c.CavendishExtinct != null ? MotelyEventType.CavendishExtinct
            : c.GrosMichelExtinct != null ? MotelyEventType.GrosMichelExtinct
            : c.SpaceLevelup != null ? MotelyEventType.SpaceLevelup
            : c.BusinessPayout != null ? MotelyEventType.BusinessPayout
            : c.BloodstoneTrigger != null ? MotelyEventType.BloodstoneTrigger
            : c.ParkingPayout != null ? MotelyEventType.ParkingPayout
            : c.GlassDestroy != null ? MotelyEventType.GlassDestroy
            : c.WheelStaysFlipped != null ? MotelyEventType.WheelStaysFlipped
            : (MotelyEventType?)null);

    private static int[]? ResolveEventRolls(JamlClauseDto c) =>
        c.Rolls
        ?? c.LuckyMoney
        ?? c.LuckyMult
        ?? c.MisprintMult
        ?? c.WheelOfFortune
        ?? c.CavendishExtinct
        ?? c.GrosMichelExtinct
        ?? c.SpaceLevelup
        ?? c.BusinessPayout
        ?? c.BloodstoneTrigger
        ?? c.ParkingPayout
        ?? c.GlassDestroy
        ?? c.WheelStaysFlipped;

    private static LegendaryJokerSourceConfig CreateLegendaryJokerSources(
        int[]? shopItems,
        int[]? boosterPacks,
        int[]? arcanaPacks,
        int[]? spectralPacks,
        int[]? soulCard,
        bool requireMegaPack,
        int earlyAntesMaxPack
    )
    {
        var arcana = arcanaPacks ?? [];
        var spectral = spectralPacks ?? [];
        bool split = arcana.Length > 0 || spectral.Length > 0;

        // If the user specified neither a plain boosterPacks list nor split arcana/spectral lists,
        // default to the full per-ante PRNG slot range (0..5). Scoring clamps ante 1 using
        // EarlyAntesMaxPack (default 3 = normal gameplay, raise to 5 for Hieroglyph scans).
        var resolvedBooster = split
            ? System.Array.Empty<int>()
            : (boosterPacks is { Length: > 0 } bp ? bp : new[] { 0, 1, 2, 3, 4, 5 });

        return new LegendaryJokerSourceConfig
        {
            ShopItems = shopItems ?? [],
            BoosterPacks = resolvedBooster,
            EarlyAntesMaxPack = earlyAntesMaxPack,
            ArcanaPacks = arcana,
            SpectralPacks = spectral,
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
                shopItems = [0, 1, 2, 3];
                boosterPacks = [0, 1, 2, 3, 4, 5];
                break;

            case MotelyFilterItemType.LegendaryJoker:
                boosterPacks = [0, 1, 2, 3, 4, 5];
                break;

            case MotelyFilterItemType.Standardcard:
                // Standard cards (ranked + suited playing cards) come out of standard booster
                // packs in the shop pack stream — same default range as joker types so a bare
                // `standardCard: { rank: King }` matches every shop pack slot at the targeted
                // antes instead of zero.
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

    private static string LabelEnumOrAny<T>(EnumOrAny<T> v) where T : struct, Enum =>
        v.IsAny ? "any" : v.Value.ToString();

    private static MotelyJoker ToMotelyJoker(MotelyJokerLegendary joker) =>
        (MotelyJoker)((int)MotelyJokerRarity.Legendary | (int)joker);

    private static string GenerateLabel(JamlClauseDto c)
    {
        if (c.Joker is { } jokerValue) return LabelEnumOrAny(jokerValue);
        if (c.Jokers is { Count: > 0 } jj) return string.Join(", ", jj);
        if (c.CommonJoker is { } commonJokerValue) return LabelEnumOrAny(commonJokerValue);
        if (c.CommonJokers is { Count: > 0 } cj) return string.Join(", ", cj);
        if (c.UncommonJoker is { } uncommonJokerValue) return LabelEnumOrAny(uncommonJokerValue);
        if (c.UncommonJokers is { Count: > 0 } uj) return string.Join(", ", uj);
        if (c.RareJoker is { } rareJokerValue) return LabelEnumOrAny(rareJokerValue);
        if (c.RareJokers is { Count: > 0 } rj) return string.Join(", ", rj);
        if (c.LegendaryJoker is { } legendaryJokerValue) return LabelEnumOrAny(legendaryJokerValue);
        if (c.LegendaryJokers is { Count: > 0 } lj) return string.Join(", ", lj);
        if (c.Voucher is { } voucherValue) return voucherValue.ToString();
        if (c.Vouchers is { Count: > 0 } vv) return string.Join(", ", vv);
        if (c.TarotCard is { } tarotCardValue) return tarotCardValue.ToString();
        if (c.TarotCards is { Count: > 0 } tt) return string.Join(", ", tt);
        if (c.SpectralCard is { } spectralCardValue) return spectralCardValue.ToString();
        if (c.SpectralCards is { Count: > 0 } ss) return string.Join(", ", ss);
        if (c.Planet is { } planetValue) return planetValue.ToString();
        if (c.PlanetCard is { } planetCardValue) return planetCardValue.ToString();
        if (c.Boss is { } bossValue) return bossValue.ToString();
        if (c.Tag is { } tagValue) return tagValue.ToString();
        if (c.SmallBlindTag is { } smallBlindTagValue) return smallBlindTagValue.ToString();
        if (c.BigBlindTag is { } bigBlindTagValue) return bigBlindTagValue.ToString();
        if (c.StandardCard != null) return c.StandardCard.Value.StringValue ?? string.Empty;
        if (c.StandardCards is { Count: > 0 }) return "standardCards";
        if (c.ErraticRank != null) return c.ErraticRank;
        if (c.ErraticSuit != null) return c.ErraticSuit;
        if (c.ErraticCard != null) return c.ErraticCard;
        if (c.StartingDraw != null) return c.StartingDraw;
        if (c.Event is { } eventValue) return eventValue.ToString();
        if (c.LuckyMoney != null) return "luckyMoney";
        if (c.LuckyMult != null) return "luckyMult";
        if (c.MisprintMult != null) return "misprintMult";
        if (c.WheelOfFortune != null) return "wheelOfFortune";
        if (c.CavendishExtinct != null) return "cavendishExtinct";
        if (c.GrosMichelExtinct != null) return "grosMichelExtinct";
        if (c.SpaceLevelup != null) return "spaceLevelup";
        if (c.BusinessPayout != null) return "businessPayout";
        if (c.BloodstoneTrigger != null) return "bloodstoneTrigger";
        if (c.ParkingPayout != null) return "parkingPayout";
        if (c.GlassDestroy != null) return "glassDestroy";
        if (c.WheelStaysFlipped != null) return "wheelStaysFlipped";
        return "clause";
    }

    // ── Resolve type from shorthand keys or explicit type field ──

    private static (MotelyFilterItemType itemType, string? value) ResolveType(JamlClauseDto c)
    {
        // Shorthand keys (type-as-key) — check each one
        if (c.Joker != null)
            return (MotelyFilterItemType.Joker, null);
        if (c.Jokers != null)
            return (MotelyFilterItemType.Joker, null); // plural
        if (c.CommonJoker != null)
            return (MotelyFilterItemType.CommonJoker, null);
        if (c.CommonJokers != null)
            return (MotelyFilterItemType.CommonJoker, null);
        if (c.UncommonJoker != null)
            return (MotelyFilterItemType.UncommonJoker, null);
        if (c.UncommonJokers != null)
            return (MotelyFilterItemType.UncommonJoker, null);
        if (c.RareJoker != null)
            return (MotelyFilterItemType.RareJoker, null);
        if (c.RareJokers != null)
            return (MotelyFilterItemType.RareJoker, null);
        if (c.LegendaryJoker != null)
            return (MotelyFilterItemType.LegendaryJoker, null);
        if (c.LegendaryJokers != null)
            return (MotelyFilterItemType.LegendaryJoker, null);
        if (c.Voucher != null)
            return (MotelyFilterItemType.Voucher, null);
        if (c.Vouchers != null)
            return (MotelyFilterItemType.Voucher, null);
        if (c.TarotCard != null)
            return (MotelyFilterItemType.TarotCard, null);
        if (c.TarotCards != null)
            return (MotelyFilterItemType.TarotCard, null);
        if (c.SpectralCard != null)
            return (MotelyFilterItemType.SpectralCard, null);
        if (c.SpectralCards != null)
            return (MotelyFilterItemType.SpectralCard, null);
        if (c.Planet != null)
            return (MotelyFilterItemType.PlanetCard, null);
        if (c.PlanetCard != null)
            return (MotelyFilterItemType.PlanetCard, null);
        if (c.Boss != null)
            return (MotelyFilterItemType.Boss, null);
        if (c.Tag != null)
            return (MotelyFilterItemType.SmallBlindTag, null);
        if (c.SmallBlindTag != null)
            return (MotelyFilterItemType.SmallBlindTag, null);
        if (c.BigBlindTag != null)
            return (MotelyFilterItemType.BigBlindTag, null);
        if (c.StandardCard != null)
            return (MotelyFilterItemType.Standardcard, c.StandardCard.Value.StringValue);
        if (c.StandardCards != null)
            return (MotelyFilterItemType.Standardcard, null);
        if (c.ErraticRank != null)
            return (MotelyFilterItemType.ErraticRank, c.ErraticRank);
        if (c.ErraticSuit != null)
            return (MotelyFilterItemType.ErraticSuit, c.ErraticSuit);
        if (c.ErraticCard != null)
            return (MotelyFilterItemType.ErraticCard, c.ErraticCard);
        if (c.StartingDraw != null)
            return (MotelyFilterItemType.StartingDraw, c.StartingDraw);
        if (c.Event != null)
            return (MotelyFilterItemType.Event, null);
        if (c.LuckyMoney != null
            || c.LuckyMult != null
            || c.MisprintMult != null
            || c.WheelOfFortune != null
            || c.CavendishExtinct != null
            || c.GrosMichelExtinct != null
            || c.SpaceLevelup != null
            || c.BusinessPayout != null
            || c.BloodstoneTrigger != null
            || c.ParkingPayout != null
            || c.GlassDestroy != null
            || c.WheelStaysFlipped != null)
            return (MotelyFilterItemType.Event, null);


        throw new InvalidOperationException("Clause is missing a recognized clause key or type.");
    }

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

    private static List<string> NormalizeSeeds(List<string>? seeds)
    {
        if (seeds is not { Count: > 0 })
            return [];

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var normalized = new List<string>();

        foreach (var entry in seeds)
        {
            if (string.IsNullOrWhiteSpace(entry))
                continue;

            var seed = entry.Trim().ToUpperInvariant().Replace('0', 'O');
            if (seen.Add(seed))
                normalized.Add(seed);
        }

        return normalized;
    }

    private static T RequireEnum<T>(string value)
        where T : struct, Enum =>
        Enum.Parse<T>(value, ignoreCase: true);

    private static T? ParseEnum<T>(string? value)
        where T : struct, Enum =>
        value != null && Enum.TryParse<T>(value, ignoreCase: true, out var result) ? result : null;

    private static MotelyStandardcardRank? ParseRank(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return null;
        return value.ToUpperInvariant() switch
        {
            "2" => MotelyStandardcardRank.Two,
            "3" => MotelyStandardcardRank.Three,
            "4" => MotelyStandardcardRank.Four,
            "5" => MotelyStandardcardRank.Five,
            "6" => MotelyStandardcardRank.Six,
            "7" => MotelyStandardcardRank.Seven,
            "8" => MotelyStandardcardRank.Eight,
            "9" => MotelyStandardcardRank.Nine,
            "10" or "T" => MotelyStandardcardRank.Ten,
            "J" => MotelyStandardcardRank.Jack,
            "Q" => MotelyStandardcardRank.Queen,
            "K" => MotelyStandardcardRank.King,
            "A" => MotelyStandardcardRank.Ace,
            _ => ParseEnum<MotelyStandardcardRank>(value),
        };
    }

    private static MotelyStandardcardSuit? ParseSuit(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return null;
        return value.ToUpperInvariant() switch
        {
            "C" or "CLUBS" => MotelyStandardcardSuit.Clubs,
            "D" or "DIAMONDS" => MotelyStandardcardSuit.Diamonds,
            "H" or "HEARTS" => MotelyStandardcardSuit.Hearts,
            "S" or "SPADES" => MotelyStandardcardSuit.Spades,
            _ => ParseEnum<MotelyStandardcardSuit>(value),
        };
    }

    private static (MotelyStandardcardRank? rank, MotelyStandardcardSuit? suit) ParseCardShorthand(
        string value
    )
    {
        if (string.IsNullOrEmpty(value))
            return (null, null);
        if (Enum.TryParse<MotelyStandardCard>(value, true, out var card))
        {
            return (card.GetRank(), card.GetSuit());
        }

        // SWAP FALLBACK for old shorthands like "C2", "SK", "10H", etc.
        if (value.Length >= 2)
        {
            var suit1 = ParseSuit(value.Substring(0, 1));
            var rank1 = ParseRank(value.Substring(1));
            if (suit1 != null && rank1 != null) return (rank1, suit1);

            var rank2 = ParseRank(value.Substring(0, value.Length - 1));
            var suit2 = ParseSuit(value.Substring(value.Length - 1));
            if (suit2 != null && rank2 != null) return (rank2, suit2);
        }

        return (null, null);
    }
}
