using Motely.Filters;

namespace Motely.GPU;

/// <summary>
/// Translates MotelyRunConfig clauses into DungmotConfig for GPU pre-filtering.
///
/// Dungmot supports a subset of Motely filters - specifically designed for fast
/// GPU pre-filtering of seeds that Motely's CPU SIMD pipeline then fully scores.
///
/// Supported filter types:
/// - Negative edition jokers (specific joker or by rarity)
/// - Negative edition legendary jokers (from Soul cards)
/// - Negative tags
/// </summary>
public static class DungmotFilterTranslator
{
    /// <summary>
    /// Attempt to translate a MotelyRunConfig into a DungmotConfig.
    /// Returns null if no dungmot-compatible filter is found.
    /// </summary>
    public static DungmotConfig? TryTranslate(
        MotelyRunConfig config,
        DungmotOptions? options = null
    )
    {
        options ??= DungmotOptions.Default;

        // Look for dungmot-compatible clauses in Must array (required filters)
        foreach (var clause in config.Must)
        {
            var dungmotConfig = TryTranslateClause(clause, options);
            if (dungmotConfig != null)
            {
                return dungmotConfig;
            }
        }

        // If no Must clauses match, try Should (nice-to-have filters)
        foreach (var clause in config.Should)
        {
            var dungmotConfig = TryTranslateClause(clause, options);
            if (dungmotConfig != null)
            {
                return dungmotConfig;
            }
        }

        return null;
    }

    /// <summary>
    /// Translate a single clause into DungmotConfig if compatible.
    /// </summary>
    private static DungmotConfig? TryTranslateClause(
        MotelyJsonFilterClause clause,
        DungmotOptions options
    )
    {
        // Handle Generic (Raw) clauses - delegate to raw logic
        if (clause is MotelyJsonGenericFilterClause generic)
        {
            return TryTranslateRawClause(generic.Raw, options);
        }

        // Handle Typed Joker Clause
        if (clause is MotelyJsonJokerFilterClause jokerClause)
        {
            // Negative edition joker (shop)
            if (jokerClause.EditionEnum == MotelyItemEdition.Negative)
            {
                var antes = ConvertWantedAntes(jokerClause.WantedAntes);
                return new DungmotConfig
                {
                    ExecutablePath = options.ExecutablePath ?? "negative_joker_prefilter.exe",
                    FilterType = "negative-joker",
                    Joker = jokerClause.JokerType.HasValue
                        ? jokerClause.JokerType.ToString()
                        : null,
                    Edition = "negative",
                    Antes = antes.Length > 0 ? antes : [1, 2, 3, 4],
                    StartBatch = options.StartBatch,
                    EndBatch = options.EndBatch,
                    BatchChars = options.BatchChars,
                    Stream = true,
                };
            }
        }

        // Handle Typed Soul Joker Clause
        if (clause is MotelyJsonSoulJokerFilterClause soulClause)
        {
            // Soul joker (legendary from Soul card) - always negative for pre-filtering sake or explicit check?
            // Original logic checked ItemType == SoulJoker.

            var antes = ConvertWantedAntes(soulClause.WantedAntes);
            return new DungmotConfig
            {
                ExecutablePath = options.ExecutablePath ?? "negative_legendary_prefilter.exe",
                FilterType = "negative-legendary",
                Joker = soulClause.JokerType.HasValue ? soulClause.JokerType.ToString() : null,
                Antes = antes.Length > 0 ? antes : [1, 2, 3, 4],
                StartBatch = options.StartBatch,
                EndBatch = options.EndBatch,
                BatchChars = options.BatchChars,
                Stream = true,
            };
        }

        return null;
    }

    private static int[] ConvertWantedAntes(bool[] wanted)
    {
        var list = new List<int>();
        for (int i = 0; i < wanted.Length; i++)
        {
            if (wanted[i])
                list.Add(i);
        }
        return list.ToArray();
    }

    /// <summary>
    /// Translate a single raw clause into DungmotConfig.
    /// </summary>
    private static DungmotConfig? TryTranslateRawClause(
        MotelyJsonConfig.MotelyJsonFilterClause clause,
        DungmotOptions options
    )
    {
        // Negative edition joker (shop)
        if (
            clause.ItemTypeEnum == MotelyFilterItemType.Joker
            && clause.EditionEnum == MotelyItemEdition.Negative
        )
        {
            return new DungmotConfig
            {
                ExecutablePath = options.ExecutablePath ?? "negative_joker_prefilter.exe",
                FilterType = "negative-joker",
                Joker = clause.JokerEnum.HasValue ? clause.JokerEnum.ToString() : null,
                Edition = "negative",
                Antes =
                    clause.Antes != null && clause.Antes.Length > 0 ? clause.Antes : [1, 2, 3, 4],
                StartBatch = options.StartBatch,
                EndBatch = options.EndBatch,
                BatchChars = options.BatchChars,
                Stream = true,
            };
        }

        // Soul joker (legendary from Soul card) - always negative
        if (clause.ItemTypeEnum == MotelyFilterItemType.SoulJoker)
        {
            return new DungmotConfig
            {
                ExecutablePath = options.ExecutablePath ?? "negative_legendary_prefilter.exe",
                FilterType = "negative-legendary",
                Joker = clause.JokerEnum.HasValue ? clause.JokerEnum.ToString() : null,
                Antes =
                    clause.Antes != null && clause.Antes.Length > 0 ? clause.Antes : [1, 2, 3, 4],
                StartBatch = options.StartBatch,
                EndBatch = options.EndBatch,
                BatchChars = options.BatchChars,
                Stream = true,
            };
        }

        // Negative tag
        if (
            clause.ItemTypeEnum == MotelyFilterItemType.SmallBlindTag
            && clause.TagEnum == MotelyTag.NegativeTag
        )
        {
            return new DungmotConfig
            {
                ExecutablePath = options.ExecutablePath ?? "negative_tag_skipper.exe",
                FilterType = "negative-tag",
                Antes =
                    clause.Antes != null && clause.Antes.Length > 0 ? clause.Antes : [1, 2, 3, 4],
                StartBatch = options.StartBatch,
                EndBatch = options.EndBatch,
                BatchChars = options.BatchChars,
                Stream = true,
            };
        }

        return null;
    }

    /// <summary>
    /// Get a human-readable description of what dungmot will filter.
    /// </summary>
    public static string DescribeFilter(DungmotConfig config)
    {
        var jokerPart = string.IsNullOrEmpty(config.Joker) ? "any" : config.Joker;
        var editionPart = string.IsNullOrEmpty(config.Edition) ? "" : $" {config.Edition}";
        var antesPart =
            config.Antes.Length > 0 ? $" in antes [{string.Join(",", config.Antes)}]" : "";

        return config.FilterType switch
        {
            "negative-joker" => $"Negative edition {jokerPart} joker{antesPart}",
            "negative-legendary" => $"Negative legendary joker ({jokerPart}){antesPart}",
            "negative-tag" => $"Negative tag{antesPart}",
            "negative-rare" => $"Negative rare joker{antesPart}",
            "negative-uncommon" => $"Negative uncommon joker{antesPart}",
            _ => $"{config.FilterType}{editionPart}{antesPart}",
        };
    }
}

/// <summary>
/// Options for dungmot execution (batch ranges, paths, etc.)
/// </summary>
public class DungmotOptions
{
    /// <summary>
    /// Path to dungmot executable. If null, uses filter-specific default.
    /// </summary>
    public string? ExecutablePath { get; set; }

    /// <summary>
    /// Starting batch index.
    /// </summary>
    public long StartBatch { get; set; } = 0;

    /// <summary>
    /// Ending batch index. 0 = no limit.
    /// </summary>
    public long EndBatch { get; set; } = 0;

    /// <summary>
    /// Batch character size (affects granularity).
    /// </summary>
    public int BatchChars { get; set; } = 4;

    /// <summary>
    /// Default options instance.
    /// </summary>
    public static DungmotOptions Default { get; } = new();
}
