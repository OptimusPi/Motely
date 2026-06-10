using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Motely.Filters.Jaml;

// ────────────────────────────── Loader ──────────────────────────────

public static partial class JamlConfigLoader
{
    private static readonly int[] DefaultAntes = [1, 2, 3, 4, 5, 6, 7, 8];

    public static JamlConfig FromYaml(string jaml)
    {
        if (!TryLoad(jaml, out var config, out var error))
            throw new InvalidOperationException(error ?? "Invalid JAML.");
        return config;
    }

    public static JamlConfig FromJson(string json)
    {
        if (!TryLoadFromJson(json, out var config, out var error))
            throw new InvalidOperationException(error ?? "Invalid JAML JSON.");
        return config;
    }

    public static bool TryLoadFromJson(
        string json,
        [NotNullWhen(true)] out JamlConfig? config,
        out string? error
    )
    {
        config = null;
        return TryParseRootJson(json, out var doc, out error)
            && TryLoadFromDoc(doc, out config, out error);
    }

    /// <summary>
    /// JSON counterpart of <see cref="TryParseRoot"/>: same nested and/or normalization,
    /// same strict unknown-key rejection (via <see cref="JamlJsonContext"/>).
    /// </summary>
    public static bool TryParseRootJson(
        string json,
        [NotNullWhen(true)] out JamlRootDocument? doc,
        out string? error
    )
    {
        doc = null;
        error = null;

        if (string.IsNullOrWhiteSpace(json))
        {
            error = "JSON content is required.";
            return false;
        }

        try
        {
            if (JsonNode.Parse(json) is not JsonObject root)
            {
                error = "JAML JSON document must be an object at the root.";
                return false;
            }
            NormalizeNestedLogicSyntax(root);
            doc = root.Deserialize(JamlJsonContext.Default.JamlRootDocument);
            if (doc is null)
            {
                error = "JSON deserialized to null.";
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            error = FormatLoadError(ex);
            return false;
        }
    }

    /// <summary>JSON twin of the YAML <see cref="NormalizeNestedLogicSyntax(YamlNode)"/> walk:
    /// rewrites <c>"and"/"or": { "clauses": [...], shared keys... }</c> into the flat shape,
    /// hoisting shared keys to the parent clause.</summary>
    private static void NormalizeNestedLogicSyntax(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                NormalizeNestedLogicBlock(obj, "and");
                NormalizeNestedLogicBlock(obj, "or");
                foreach (var child in obj.Select(static p => p.Value).ToArray())
                    NormalizeNestedLogicSyntax(child);
                break;
            case JsonArray array:
                foreach (var child in array.ToArray())
                    NormalizeNestedLogicSyntax(child);
                break;
        }
    }

    private static void NormalizeNestedLogicBlock(JsonObject parent, string key)
    {
        if (parent[key] is not JsonObject legacyLogicBlock)
            return;
        if (!legacyLogicBlock.TryGetPropertyValue("clauses", out var clausesNode))
            return;

        foreach (var (childKey, childValue) in legacyLogicBlock.ToArray())
        {
            if (childKey == "clauses" || parent.ContainsKey(childKey))
                continue;
            legacyLogicBlock.Remove(childKey);
            parent[childKey] = childValue;
        }

        legacyLogicBlock.Remove("clauses");
        parent[key] = clausesNode;
    }

    public static bool TryParseRoot(
        string jaml,
        [NotNullWhen(true)] out JamlRootDocument? doc,
        out string? error
    )
    {
        doc = null;
        error = null;
        if (string.IsNullOrWhiteSpace(jaml))
        {
            error = "JAML content is required.";
            return false;
        }
        try
        {
            var normalizedJaml = NormalizeNestedLogicSyntax(jaml);
            return TryParseRootFromYaml(normalizedJaml, out doc, out error);
        }
        catch (Exception ex)
        {
            error = FormatLoadError(ex);
            return false;
        }
    }

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
                error ??= "JAML document could not be parsed.";
                return false;
            }
            return TryLoadFromDoc(load, out config, out error);
        }
        catch (Exception ex)
        {
            config = null;
            error = FormatLoadError(ex);
            return false;
        }
    }

    private static bool TryLoadFromDoc(
        JamlRootDocument load,
        [NotNullWhen(true)] out JamlConfig? config,
        out string? error
    )
    {
        error = null;
        try
        {
            config = LoadFromDoc(load);
            return true;
        }
        catch (Exception ex)
        {
            config = null;
            error = FormatLoadError(ex);
            return false;
        }
    }

    /// <summary>The one doc→config mapping. Throws on invalid clauses.</summary>
    private static JamlConfig LoadFromDoc(JamlRootDocument load)
    {
        var defaultAntes = load.Defaults?.Antes ?? DefaultAntes;

        var deck = Enum.TryParse<MotelyDeck>(load.Deck, true, out var deckEnum)
            ? deckEnum
            : MotelyDeck.Red;
        var stake = Enum.TryParse<MotelyStake>(load.Stake, true, out var stakeEnum)
            ? stakeEnum
            : MotelyStake.White;

        var config = new JamlConfig
        {
            Id = NormalizeFilterId(load.Id, load.Name),
            Name = load.Name,
            Description = load.Description,
            Author = load.Author,
            Deck = deck,
            Stake = stake,
        };

        config.Seeds = NormalizeSeeds(load.Seeds);

        // MUST gates, SHOULD scores, MUSTNOT negates (see CLAUDE.md "JAML is the filter authoring layer").
        PopulateClauses(config.Must, load.Must, defaultAntes, load.Defaults);
        PopulateClauses(config.Should, load.Should, defaultAntes, load.Defaults);
        PopulateClauses(config.MustNot, load.MustNot, defaultAntes, load.Defaults);

        return config;
    }

    /// <summary>
    /// Like <see cref="TryLoad"/> but also surfaces the raw exception so callers can extract
    /// structured location info (line, column) from <see cref="YamlException"/>.
    /// </summary>
    public static bool TryLoadWithException(
        string jaml,
        [NotNullWhen(true)] out JamlConfig? config,
        out string? error,
        out Exception? exception
    )
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
                if (error == null)
                    error = "JAML document could not be parsed.";
                return false;
            }

            config = LoadFromDoc(load);
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
    /// (<c>antes</c>, <c>label</c>, <c>mode</c>, <c>score</c>, …) into the flat shape <see cref="JamlClauseUnion"/> expects.
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

                if (
                    sequence.Children.Count > 0
                    && sequence.Children.All(static c => c is YamlScalarNode)
                )
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
        mapping
            .Children.Keys.OfType<YamlScalarNode>()
            .Any(node => string.Equals(node.Value, key, StringComparison.OrdinalIgnoreCase));

    private static bool TryGetYamlChild(
        YamlMappingNode mapping,
        string key,
        [NotNullWhen(true)] out YamlScalarNode? keyNode,
        [NotNullWhen(true)] out YamlNode? valueNode
    )
    {
        foreach (var child in mapping.Children)
        {
            if (
                child.Key is YamlScalarNode scalarNode
                && string.Equals(scalarNode.Value, key, StringComparison.OrdinalIgnoreCase)
            )
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

        if (ex is JsonException)
        {
            // STJ's UnmappedMemberHandling.Disallow message → the same shape the YAML path emits.
            var unmappedMatch = Regex.Match(
                message,
                "The JSON property '([^']+)' could not be mapped to any .NET member of type '([^']+)'",
                RegexOptions.CultureInvariant
            );
            if (unmappedMatch.Success)
            {
                var propertyName = unmappedMatch.Groups[1].Value;
                var targetType = unmappedMatch.Groups[2].Value;
                return $"Unknown property '{propertyName}' in {DescribeYamlTarget(targetType)}.";
            }
            return message;
        }

        if (ex is YamlException yamlEx)
        {
            var mark = yamlEx.Start;
            var location =
                mark.Line > 0 && mark.Column > 0
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

    private static string DescribeYamlTarget(string targetType)
    {
        var shortName = targetType.Contains('.')
            ? targetType[(targetType.LastIndexOf('.') + 1)..]
            : targetType;

        return shortName switch
        {
            "JamlRootDocument" => "the top-level JAML document",
            "JamlClauseUnion" => "a clause",
            "JamlSources" => "a clause's sources block",
            "JamlDefaults" => "the defaults block",
            "StandardCardValue" => "a standardCard value",
            "StandardCardConfig" => "a standardCard mapping",
            _ => targetType,
        };
    }

    private static void PopulateClauses(
        List<IJamlClause> clausesOut,
        List<JamlClauseUnion>? clauses,
        int[] defaultAntes,
        JamlDefaults? defaults
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
            clausesOut.Add(clause);
        }
    }

    private static IJamlClause CreateClauseFromDto(
        JamlClauseUnion c,
        int[] defaultAntes,
        JamlDefaults? defaults,
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
                Clauses = c
                    .Clauses.Select(sub =>
                        CreateClauseFromDto(sub, antes, defaults, hasUserSpecifiedAntes)
                    )
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

        // "Any source specified" must include sources set DIRECTLY on the clause (e.g. a bare
        // `wraith:`/`judgement:`/`rareTag:` or `shopItems:`), not just a `sources:` block —
        // otherwise a clause like `joker: Blueprint` + `wraith: [0]` ("Blueprint from the Wraith
        // consumable") would ALSO get the default shop/pack scan injected and falsely match a
        // Blueprint sitting in the shop. If the author named a source, honor exactly that.
        bool hasExplicitSources =
            c.Sources != null
            || c.ShopItems != null
            || c.BoosterPacks != null
            || c.MinShopItem != null
            || c.MaxShopItem != null
            || c.Judgement != null
            || c.Wraith != null
            || c.RareTag != null
            || c.UncommonTag != null;
        NormalizeDefaultSources(ref shopItems, ref boosterPacks, itemType, hasExplicitSources);

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
                Jokers = c.Joker is { IsAny: false, Value: var jv }
                    ? [jv]
                    : c.Jokers?.ToArray() ?? [],
                Edition = edition,
                Stickers = c.Stickers ?? [],
                Sources = new JokerSourceConfig
                {
                    ShopItems = shopItems ?? [],
                    BoosterPacks = boosterPacks ?? [],
                    Judgement = c.Judgement ?? c.Sources?.Judgement ?? [],
                    Wraith = c.Wraith ?? c.Sources?.Wraith ?? [],
                    RiffRaff = c.Sources?.RiffRaff ?? [],
                    RareTag = c.RareTag ?? c.Sources?.RareTag ?? [],
                    UncommonTag = c.UncommonTag ?? c.Sources?.UncommonTag ?? [],
                    CommonShopJokers = c.Sources?.CommonShopJokers ?? [],
                    UncommonShopJokers = c.Sources?.UncommonShopJokers ?? [],
                    RareShopJokers = c.Sources?.RareShopJokers ?? [],
                    AllShopJokers = c.Sources?.AllShopJokers ?? [],
                },
                LegendarySources = CreateLegendaryJokerSources(
                    shopItems,
                    boosterPacks,
                    c.Sources?.ArcanaPacks,
                    c.Sources?.SpectralPacks,
                    c.Sources?.SoulCard,
                    c.Sources?.RequireMega ?? false,
                    hasExplicitSources
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
                Jokers = c.CommonJoker is { IsAny: false, Value: var cjv }
                    ? [cjv]
                    : c.CommonJokers?.ToArray() ?? [],
                Edition = edition,
                Stickers = c.Stickers ?? [],
                Sources = new JokerSourceConfig
                {
                    ShopItems = shopItems ?? [],
                    BoosterPacks = boosterPacks ?? [],
                    Judgement = c.Judgement ?? c.Sources?.Judgement ?? [],
                    Wraith = c.Wraith ?? c.Sources?.Wraith ?? [],
                    RiffRaff = c.Sources?.RiffRaff ?? [],
                    RareTag = c.RareTag ?? c.Sources?.RareTag ?? [],
                    UncommonTag = c.UncommonTag ?? c.Sources?.UncommonTag ?? [],
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
                Max = max,
                IsWildcard = c.UncommonJoker?.IsAny ?? false,
                Jokers = c.UncommonJoker is { IsAny: false, Value: var ujv }
                    ? [ujv]
                    : c.UncommonJokers?.ToArray() ?? [],
                Edition = edition,
                Stickers = c.Stickers ?? [],
                Sources = new JokerSourceConfig
                {
                    ShopItems = shopItems ?? [],
                    BoosterPacks = boosterPacks ?? [],
                    Judgement = c.Judgement ?? c.Sources?.Judgement ?? [],
                    Wraith = c.Wraith ?? c.Sources?.Wraith ?? [],
                    RiffRaff = c.Sources?.RiffRaff ?? [],
                    RareTag = c.RareTag ?? c.Sources?.RareTag ?? [],
                    UncommonTag = c.UncommonTag ?? c.Sources?.UncommonTag ?? [],
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
                Max = max,
                IsWildcard = c.RareJoker?.IsAny ?? false,
                Jokers = c.RareJoker is { IsAny: false, Value: var rjv }
                    ? [rjv]
                    : c.RareJokers?.ToArray() ?? [],
                Edition = edition,
                Stickers = c.Stickers ?? [],
                Sources = new JokerSourceConfig
                {
                    ShopItems = shopItems ?? [],
                    BoosterPacks = boosterPacks ?? [],
                    Judgement = c.Judgement ?? c.Sources?.Judgement ?? [],
                    Wraith = c.Wraith ?? c.Sources?.Wraith ?? [],
                    RiffRaff = c.Sources?.RiffRaff ?? [],
                    RareTag = c.RareTag ?? c.Sources?.RareTag ?? [],
                    UncommonTag = c.UncommonTag ?? c.Sources?.UncommonTag ?? [],
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
                Max = max,
                IsWildcard = c.Joker?.IsAny ?? false,
                Jokers = c.Joker is { IsAny: false, Value: var mjv }
                    ? [mjv]
                    : c.Jokers?.ToArray() ?? [],
                Edition = edition,
                Stickers = c.Stickers ?? [],
                Sources = new JokerSourceConfig
                {
                    ShopItems = shopItems ?? [],
                    BoosterPacks = boosterPacks ?? [],
                    Judgement = c.Judgement ?? c.Sources?.Judgement ?? [],
                    Wraith = c.Wraith ?? c.Sources?.Wraith ?? [],
                    RiffRaff = c.Sources?.RiffRaff ?? [],
                    RareTag = c.RareTag ?? c.Sources?.RareTag ?? [],
                    UncommonTag = c.UncommonTag ?? c.Sources?.UncommonTag ?? [],
                    CommonShopJokers = c.Sources?.CommonShopJokers ?? [],
                    UncommonShopJokers = c.Sources?.UncommonShopJokers ?? [],
                    RareShopJokers = c.Sources?.RareShopJokers ?? [],
                    AllShopJokers = c.Sources?.AllShopJokers ?? [],
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
                Jokers = c.LegendaryJoker is { IsAny: false, Value: var lgv }
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
                    hasExplicitSources
                ),
            },
            MotelyFilterItemType.Voucher => new VoucherClause
            {
                Label = label,
                Score = score,
                Antes = antes,
                Min = min,
                Max = max,
                Vouchers = c.Voucher is { } v ? [v] : c.Vouchers?.ToArray() ?? [],
                Rolls = ResolveMapRolls(
                    c.Rolls,
                    [0],
                    maxRoll: MotelyGlobals.MaxMapVoucherRollIndex,
                    "voucher"
                ),
            },
            MotelyFilterItemType.TarotCard => new TarotCardClause
            {
                Label = label,
                Score = score,
                Antes = antes,
                Min = min,
                Max = max,
                Tarots = c.TarotCard is { } t ? [t] : c.TarotCards?.ToArray() ?? [],
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
                Max = max,
                Spectrals = c.SpectralCard is { } sp ? [sp] : c.SpectralCards?.ToArray() ?? [],
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
                Max = max,
                Planets = c.PlanetCard is { } p ? [p] : [],
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
                Max = max,
                Bosses = c.Boss is { } b ? [b] : [],
                Rolls = ResolveMapRolls(
                    c.Rolls,
                    [0],
                    maxRoll: MotelyGlobals.MaxMapBossRollIndex,
                    "boss"
                ),
            },
            MotelyFilterItemType.SmallBlindTag => CreateTagClause(
                label,
                score,
                antes,
                min,
                max,
                ResolveMapRolls(
                    c.Rolls,
                    c.Tag != null || c.Tags != null ? [0, 1] : [0],
                    maxRoll: MotelyGlobals.MaxMapTagRollIndex,
                    "tag"
                ),
                c.SmallBlindTags,
                c.Tags,
                c.SmallBlindTag,
                c.Tag
            ),
            MotelyFilterItemType.BigBlindTag => CreateTagClause(
                label,
                score,
                antes,
                min,
                max,
                ResolveMapRolls(c.Rolls, [1], maxRoll: MotelyGlobals.MaxMapTagRollIndex, "tag"),
                c.BigBlindTags,
                singlePrimary: c.BigBlindTag
            ),
            MotelyFilterItemType.Standardcard => new StandardCardClause
            {
                Label = label,
                Score = score,
                Antes = antes,
                Min = min,
                Max = max,
                Rank = ParseRank(c.StandardCard?.ObjectValue?.Rank ?? c.Rank) ?? shRank,
                Suit = ParseSuit(c.StandardCard?.ObjectValue?.Suit ?? c.Suit) ?? shSuit,
                Enhancement = c.StandardCard?.ObjectValue?.Enhancement ?? c.Enhancement,
                Seal = c.StandardCard?.ObjectValue?.Seal ?? c.Seal,
                Edition = c.StandardCard?.ObjectValue?.Edition ?? edition,
                Sources = new StandardCardSourceConfig
                {
                    ShopItems = c.StandardCard?.ObjectValue?.Sources?.ShopItems ?? shopItems ?? [],
                    BoosterPacks =
                        c.StandardCard?.ObjectValue?.Sources?.BoosterPacks ?? boosterPacks ?? [],
                    Certificate =
                        c.StandardCard?.ObjectValue?.Sources?.Certificate
                        ?? c.Sources?.Certificate
                        ?? [],
                    Incantation =
                        c.StandardCard?.ObjectValue?.Sources?.Incantation
                        ?? c.Sources?.Incantation
                        ?? [],
                    Familiar =
                        c.StandardCard?.ObjectValue?.Sources?.Familiar ?? c.Sources?.Familiar ?? [],
                    Grim = c.StandardCard?.ObjectValue?.Sources?.Grim ?? c.Sources?.Grim ?? [],
                    DeckDraw =
                        c.StandardCard?.ObjectValue?.Sources?.DeckDraw ?? c.Sources?.DeckDraw ?? [],
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
                c.Sources?.Luck,
                min,
                score,
                max,
                label,
                hasUserSpecifiedAntes,
                hasExplicitSources
            ),
            _ => throw new NotSupportedException($"Unsupported clause type: {itemType}"),
        };
    }

    private static ErraticCardClause CreateErraticCardClause(
        JamlClauseUnion c,
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

    private static RollClause CreateEventClause(
        MotelyEventType? eventType,
        int[]? rolls,
        int? luck,
        int min,
        int score,
        int? max,
        string label,
        bool hasUserSpecifiedAntes,
        bool hasExplicitSources
    )
    {
        if (eventType is null)
            throw new NotSupportedException("Event clause is missing event type name.");
        // Oops! All 6s doubles every "listed probability" in Balatro, so `luck` modifies
        // any probabilistic event. MisprintMult is the lone exception: it rolls a value
        // range, not a probability, so there is nothing for luck to scale.
        bool supportsLuck = eventType is not MotelyEventType.MisprintMult;
        if (hasUserSpecifiedAntes)
            throw new NotSupportedException(
                "Event clauses do not support 'antes'. Remove 'antes' from the event clause, enclosing logic block, or defaults section."
            );
        if (hasExplicitSources && !supportsLuck)
            throw new NotSupportedException(
                "This event clause does not support 'sources'. Remove the sources block from the event clause."
            );
        if (hasExplicitSources && supportsLuck && luck is null)
            throw new NotSupportedException(
                "Event sources only support 'luck'. Remove other keys from the sources block."
            );

        int resolvedLuck = luck ?? 1;
        if (resolvedLuck < 1)
            throw new NotSupportedException("sources.luck must be a positive integer.");

        var r = (rolls == null || rolls.Length == 0) ? new int[] { 0 } : rolls;
        return eventType.Value switch
        {
            MotelyEventType.LuckyMoney => new LuckyMoneyClause
            {
                Label = label,
                Score = score,
                Min = min,
                Max = max,
                Luck = resolvedLuck,
                Rolls = r,
            },
            MotelyEventType.LuckyMult => new LuckyMultClause
            {
                Label = label,
                Score = score,
                Min = min,
                Max = max,
                Luck = resolvedLuck,
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
                Luck = resolvedLuck,
                Rolls = r,
            },
            MotelyEventType.CavendishExtinct => new CavendishExtinctClause
            {
                Label = label,
                Score = score,
                Min = min,
                Max = max,
                Luck = resolvedLuck,
                Rolls = r,
            },
            MotelyEventType.GrosMichelExtinct => new GrosMichelExtinctClause
            {
                Label = label,
                Score = score,
                Min = min,
                Max = max,
                Luck = resolvedLuck,
                Rolls = r,
            },
            MotelyEventType.SpaceLevelup => new SpaceLevelupClause
            {
                Label = label,
                Score = score,
                Min = min,
                Max = max,
                Luck = resolvedLuck,
                Rolls = r,
            },
            MotelyEventType.BusinessPayout => new BusinessPayoutClause
            {
                Label = label,
                Score = score,
                Min = min,
                Max = max,
                Luck = resolvedLuck,
                Rolls = r,
            },
            MotelyEventType.BloodstoneTrigger => new BloodstoneTriggerClause
            {
                Label = label,
                Score = score,
                Min = min,
                Max = max,
                Luck = resolvedLuck,
                Rolls = r,
            },
            MotelyEventType.ParkingPayout => new ParkingPayoutClause
            {
                Label = label,
                Score = score,
                Min = min,
                Max = max,
                Luck = resolvedLuck,
                Rolls = r,
            },
            MotelyEventType.GlassDestroy => new GlassDestroyClause
            {
                Label = label,
                Score = score,
                Min = min,
                Max = max,
                Luck = resolvedLuck,
                Rolls = r,
            },
            MotelyEventType.WheelStaysFlipped => new WheelStaysFlippedClause
            {
                Label = label,
                Score = score,
                Min = min,
                Max = max,
                Luck = resolvedLuck,
                Rolls = r,
            },
            _ => throw new NotSupportedException($"Unsupported event type: {eventType}"),
        };
    }

    private static MotelyEventType? ResolveEventType(JamlClauseUnion c) =>
        c.Event
        ?? (
            c.LuckyMoney != null ? MotelyEventType.LuckyMoney
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
            : (MotelyEventType?)null
        );

    private static int[]? ResolveEventRolls(JamlClauseUnion c) =>
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
        bool hasExplicitSources
    )
    {
        var arcana = arcanaPacks ?? [];
        var Spectral = spectralPacks ?? [];
        bool split = arcana.Length > 0 || Spectral.Length > 0;

        var resolvedBooster =
            split ? System.Array.Empty<int>()
            : boosterPacks is { } bp ? bp
            : hasExplicitSources ? System.Array.Empty<int>()
            : new[] { 0, 1, 2, 3, 4, 5 };

        return new LegendaryJokerSourceConfig
        {
            ShopItems = shopItems ?? [],
            BoosterPacks = resolvedBooster,
            ArcanaPacks = arcana,
            SpectralPacks = Spectral,
            SoulCard = soulCard ?? [],
            RequireMegaPack = requireMegaPack,
        };
    }

    private static void NormalizeDefaultSources(
        ref int[]? shopItems,
        ref int[]? boosterPacks,
        MotelyFilterItemType itemType,
        bool hasExplicitSources
    )
    {
        if (shopItems != null || boosterPacks != null)
            return;

        // Global rule: if any sources object is present on the clause, do not inject defaults.
        if (hasExplicitSources)
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

            case MotelyFilterItemType.TarotCard:
            case MotelyFilterItemType.SpectralCard:
            case MotelyFilterItemType.PlanetCard:
                // Consumables show up both in the shop's consumable slots and in their booster
                // packs (Arcana / Spectral / Celestial). Without this case a bare
                // `tarotCard: TheFool` defaulted to NO sources and matched nothing — the same
                // reach as jokers makes the obvious clause do the obvious thing.
                shopItems = [0, 1, 2, 3];
                boosterPacks = [0, 1, 2, 3, 4, 5];
                break;
        }
    }

    private static string LabelEnumOrAny<T>(EnumOrAny<T> v)
        where T : struct, Enum =>
        v.IsAny ? "Any" : FormatUtils.FormatDisplayName(v.Value.ToString());

    private static string LabelEnums<T>(IEnumerable<T> values)
        where T : struct, Enum =>
        string.Join(
            ", ",
            values.Select(static value => FormatUtils.FormatDisplayName(value.ToString()))
        );

    private static MotelyJoker ToMotelyJoker(MotelyJokerLegendary joker) =>
        (MotelyJoker)((int)MotelyJokerRarity.Legendary | (int)joker);

    private static string GenerateLabel(JamlClauseUnion c)
    {
        if (c.Joker is { } jokerValue)
            return LabelEnumOrAny(jokerValue);
        if (c.Jokers is { Count: > 0 } jj)
            return LabelEnums(jj);
        if (c.CommonJoker is { } commonJokerValue)
            return LabelEnumOrAny(commonJokerValue);
        if (c.CommonJokers is { Count: > 0 } cj)
            return LabelEnums(cj);
        if (c.UncommonJoker is { } uncommonJokerValue)
            return LabelEnumOrAny(uncommonJokerValue);
        if (c.UncommonJokers is { Count: > 0 } uj)
            return LabelEnums(uj);
        if (c.RareJoker is { } rareJokerValue)
            return LabelEnumOrAny(rareJokerValue);
        if (c.RareJokers is { Count: > 0 } rj)
            return LabelEnums(rj);
        if (c.LegendaryJoker is { } legendaryJokerValue)
            return LabelEnumOrAny(legendaryJokerValue);
        if (c.LegendaryJokers is { Count: > 0 } lj)
            return LabelEnums(lj);
        if (c.Voucher is { } voucherValue)
            return FormatUtils.FormatVoucher(voucherValue);
        if (c.Vouchers is { Count: > 0 } vv)
            return string.Join(", ", vv.Select(FormatUtils.FormatVoucher));
        if (c.TarotCard is { } tarotCardValue)
            return FormatUtils.FormatDisplayName(tarotCardValue.ToString());
        if (c.TarotCards is { Count: > 0 } tt)
            return LabelEnums(tt);
        if (c.SpectralCard is { } spectralCardValue)
            return FormatUtils.FormatDisplayName(spectralCardValue.ToString());
        if (c.SpectralCards is { Count: > 0 } ss)
            return LabelEnums(ss);
        if (c.PlanetCard is { } planetCardValue)
            return FormatUtils.FormatDisplayName(planetCardValue.ToString());
        if (c.Boss is { } bossValue)
            return FormatUtils.FormatBoss(bossValue);
        if (c.Tag is { } tagValue)
            return FormatUtils.FormatTag(tagValue);
        if (c.Tags is { Count: > 0 } tagList)
            return string.Join(", ", tagList.Select(FormatUtils.FormatTag));
        if (c.SmallBlindTag is { } smallBlindTagValue)
            return FormatUtils.FormatTag(smallBlindTagValue);
        if (c.SmallBlindTags is { Count: > 0 } smallBlindTagList)
            return string.Join(", ", smallBlindTagList.Select(FormatUtils.FormatTag));
        if (c.BigBlindTag is { } bigBlindTagValue)
            return FormatUtils.FormatTag(bigBlindTagValue);
        if (c.BigBlindTags is { Count: > 0 } bigBlindTagList)
            return string.Join(", ", bigBlindTagList.Select(FormatUtils.FormatTag));
        if (c.StandardCard != null)
            return LabelStandardCard(c);
        if (c.StandardCards is { Count: > 0 })
            return "Standard Cards";
        if (c.ErraticRank != null)
            return $"Erratic {LabelRank(c.ErraticRank)}";
        if (c.ErraticSuit != null)
            return $"Erratic {LabelSuit(c.ErraticSuit)}";
        if (c.ErraticCard != null)
            return FormatUtils.FormatDisplayName(c.ErraticCard);
        if (c.StartingDraw != null)
            return FormatUtils.FormatDisplayName(c.StartingDraw);
        if (c.Event is { } eventValue)
            return FormatUtils.FormatDisplayName(eventValue.ToString());
        if (c.LuckyMoney != null)
            return "Lucky Money";
        if (c.LuckyMult != null)
            return "Lucky Mult";
        if (c.MisprintMult != null)
            return "Misprint Mult";
        if (c.WheelOfFortune != null)
            return "Wheel of Fortune";
        if (c.CavendishExtinct != null)
            return "Cavendish Extinct";
        if (c.GrosMichelExtinct != null)
            return "Gros Michel Extinct";
        if (c.SpaceLevelup != null)
            return "Space Level Up";
        if (c.BusinessPayout != null)
            return "Business Card";
        if (c.BloodstoneTrigger != null)
            return "Bloodstone";
        if (c.ParkingPayout != null)
            return "Parking Lot";
        if (c.GlassDestroy != null)
            return "Glass Destroy";
        if (c.WheelStaysFlipped != null)
            return "Wheel Stays Flipped";
        return "Clause";
    }

    private static string LabelStandardCard(JamlClauseUnion c)
    {
        if (!string.IsNullOrWhiteSpace(c.StandardCard?.StringValue))
            return FormatUtils.FormatDisplayName(c.StandardCard.Value.StringValue);

        var parts = new List<string>();
        if (c.StandardCard?.ObjectValue?.Rank is { } rank)
            parts.Add(LabelRank(rank));
        else if (c.Rank is { } rankText)
            parts.Add(LabelRank(rankText));

        if (c.StandardCard?.ObjectValue?.Suit is { } suit)
            parts.Add(LabelSuit(suit));
        else if (c.Suit is { } suitText)
            parts.Add(LabelSuit(suitText));

        if (c.StandardCard?.ObjectValue?.Enhancement is { } enhancement)
            parts.Add(FormatUtils.FormatDisplayName(enhancement.ToString()));
        if (c.StandardCard?.ObjectValue?.Seal is { } seal)
            parts.Add(FormatUtils.FormatDisplayName(seal.ToString()));
        if (c.StandardCard?.ObjectValue?.Edition is { } edition)
            parts.Add(FormatUtils.FormatDisplayName(edition.ToString()));

        return parts.Count == 0 ? "Standard Card" : string.Join(" ", parts);
    }

    private static string LabelRank(string rank) =>
        ParseRank(rank) is { } parsed
            ? FormatUtils.FormatDisplayName(parsed.ToString())
            : FormatUtils.FormatStandardcardRank(rank);

    private static string LabelSuit(string suit) =>
        ParseSuit(suit) is { } parsed
            ? FormatUtils.FormatDisplayName(parsed.ToString())
            : FormatUtils.FormatStandardcardSuit(suit);

    // ── Resolve type from shorthand keys or explicit type field ──

    private static (MotelyFilterItemType itemType, string? value) ResolveType(JamlClauseUnion c)
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
        if (c.PlanetCard != null)
            return (MotelyFilterItemType.PlanetCard, null);
        if (c.Boss != null)
            return (MotelyFilterItemType.Boss, null);
        if (c.Tag != null)
            return (MotelyFilterItemType.SmallBlindTag, null);
        if (c.Tags != null)
            return (MotelyFilterItemType.SmallBlindTag, null);
        if (c.SmallBlindTag != null)
            return (MotelyFilterItemType.SmallBlindTag, null);
        if (c.SmallBlindTags != null)
            return (MotelyFilterItemType.SmallBlindTag, null);
        if (c.BigBlindTag != null)
            return (MotelyFilterItemType.BigBlindTag, null);
        if (c.BigBlindTags != null)
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
        if (
            c.LuckyMoney != null
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
            || c.WheelStaysFlipped != null
        )
            return (MotelyFilterItemType.Event, null);

        throw new InvalidOperationException("Clause is missing a recognized clause key or type.");
    }

    // ── Helpers ──

    private static TagClause CreateTagClause(
        string label,
        int score,
        int[] antes,
        int min,
        int? max,
        int[] rolls,
        List<MotelyTag>? primary,
        List<MotelyTag>? secondary = null,
        MotelyTag? singlePrimary = null,
        MotelyTag? singleSecondary = null
    )
    {
        var tags = CoalesceTagList(primary, secondary, singlePrimary, singleSecondary);
        if (tags.Length == 0)
        {
            throw new InvalidOperationException(
                "Tag clause must name at least one tag (e.g. tag: EtherealTag or smallBlindTags: [NegativeTag, DoubleTag]). "
                    + "An empty list is invalid. Generic tag / tags check stream draws 0 and 1 (small- or big-blind offer); "
                    + "smallBlindTag(s) pins draw 0, bigBlindTag(s) pins draw 1. Use rolls: [0] or rolls: [1] to pick a slot explicitly."
            );
        }

        return new TagClause
        {
            Label = label,
            Score = score,
            Antes = antes,
            Min = min,
            Max = max,
            Tags = tags,
            Rolls = rolls,
        };
    }

    /// <summary>Map-feature stream indices (tag / voucher / boss). Not used for shop-item or event clauses.</summary>
    private static int[] ResolveMapRolls(
        int[]? rolls,
        int[] defaults,
        int maxRoll,
        string featureName
    )
    {
        if (rolls is not { Length: > 0 })
            return defaults;

        foreach (var roll in rolls)
        {
            if (roll < 0 || roll > maxRoll)
            {
                throw new InvalidOperationException(
                    $"{featureName} clause rolls index {roll} is not supported (valid 0..{maxRoll} for current engine)."
                );
            }
        }

        return rolls;
    }

    private static MotelyTag[] CoalesceTagList(
        List<MotelyTag>? primary,
        List<MotelyTag>? secondary = null,
        params MotelyTag?[] singles
    )
    {
        if (primary is not null)
            return primary.ToArray();
        if (secondary is not null)
            return secondary.ToArray();

        var list = new List<MotelyTag>(singles.Length);
        foreach (var tag in singles)
        {
            if (tag is { } value)
                list.Add(value);
        }

        return list.ToArray();
    }

    private static string NormalizeFilterId(string? explicitId, string? name)
    {
        var source = string.IsNullOrWhiteSpace(explicitId) ? name : explicitId;
        if (string.IsNullOrWhiteSpace(source))
            return "unnamed";

        var normalized = Regex.Replace(source.Trim(), "[^A-Za-z0-9_-]+", "-");
        normalized = Regex.Replace(normalized, "-+", "-").Trim('-', '_');

        return string.IsNullOrWhiteSpace(normalized) ? "unnamed" : normalized.ToLowerInvariant();
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

            // Tolerate "SEED,SCORE" lines (as written by --save-seeds): keep only the
            // seed field before the first comma, otherwise the comma/score would survive
            // normalization and fail seed validation on reload.
            var seedField = entry.Split(',', 2)[0];
            if (string.IsNullOrWhiteSpace(seedField))
                continue;

            var seed = seedField.Trim().ToUpperInvariant().Replace('0', 'O');
            if (seen.Add(seed))
                normalized.Add(seed);
        }

        return normalized;
    }

    private static T RequireEnum<T>(string value)
        where T : struct, Enum => Enum.Parse<T>(value, ignoreCase: true);

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
            if (suit1 != null && rank1 != null)
                return (rank1, suit1);

            var rank2 = ParseRank(value.Substring(0, value.Length - 1));
            var suit2 = ParseSuit(value.Substring(value.Length - 1));
            if (suit2 != null && rank2 != null)
                return (rank2, suit2);
        }

        return (null, null);
    }
}
