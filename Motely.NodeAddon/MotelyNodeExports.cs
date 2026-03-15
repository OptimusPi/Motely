using System.Text.Json;
using Microsoft.JavaScript.NodeApi;
using Motely;
using Motely.Analysis;
using Motely.Executors;
using Motely.Filters;
using BlockSearchResultDto = global::Motely.BlockSearchResultDto;
using BlockSeedResultDto = global::Motely.BlockSeedResultDto;
using CapabilitiesDto = global::Motely.CapabilitiesDto;
using ErrorDto = global::Motely.ErrorDto;
using SearchOptionsDto = global::Motely.SearchOptionsDto;
using ValidateResultDto = global::Motely.ValidateResultDto;
using VersionDto = global::Motely.VersionDto;

namespace Motely.NodeAddon;

/// <summary>
/// Node addon exports for Motely - clean direct functions matching CLI pattern.
/// No optionsJson complexity - just simple async functions.
/// </summary>
[JSExport]
public static class MotelyNodeExports
{
    private static string? _cachedVersion;
    private static string[]? _cachedFeatures;

    [JSExport]
    public static Task<string> GetVersionAsync()
    {
        var dto = new VersionDto
        {
            Version = GetCachedVersion(),
            Runtime = "node-addon",
            Features = GetFeatureList(),
        };
        return Task.FromResult(JsonSerializer.Serialize(dto, MotelyJsonContext.Default.VersionDto));
    }

    [JSExport]
    public static Task<string> GetCapabilitiesAsync()
    {
        var dto = new CapabilitiesDto
        {
            Simd = IsSimdEnabled(),
            Threads = true,
            AvailableThreadCount = Environment.ProcessorCount,
            ProcessorCount = Environment.ProcessorCount,
            Runtime = "node-addon",
            Version = GetCachedVersion(),
            Timestamp = DateTime.UtcNow.ToString("O"),
        };
        return Task.FromResult(
            JsonSerializer.Serialize(dto, MotelyJsonContext.Default.CapabilitiesDto)
        );
    }

    private static bool IsSimdEnabled() => System.Runtime.Intrinsics.Vector128.IsHardwareAccelerated;

    [JSExport]
    public static Task<string> AnalyzeSeedAsync(string seed, string deck, string stake)
    {
        var result = AnalyzeSeed(seed, deck, stake);
        return Task.FromResult(result);
    }

    private static string AnalyzeSeed(string seed, string deck, string stake)
    {
        try
        {
            if (!Enum.TryParse<MotelyDeck>(deck, true, out var deckEnum))
                return JsonSerializer.Serialize(
                    new ErrorDto { Error = $"Unknown deck: {deck}" },
                    MotelyJsonContext.Default.ErrorDto
                );

            if (!Enum.TryParse<MotelyStake>(stake, true, out var stakeEnum))
                return JsonSerializer.Serialize(
                    new ErrorDto { Error = $"Unknown stake: {stake}" },
                    MotelyJsonContext.Default.ErrorDto
                );

            var cfg = new MotelySeedAnalysisConfig(seed, deckEnum, stakeEnum);
            var analysis = MotelySeedAnalyzer.Analyze(cfg);

            if (!string.IsNullOrEmpty(analysis.Error))
                return JsonSerializer.Serialize(
                    new ErrorDto { Error = analysis.Error },
                    MotelyJsonContext.Default.ErrorDto
                );

            var dto = MapAnalysisToDto(analysis, seed, deckEnum, stakeEnum);
            return JsonSerializer.Serialize(dto, MotelyJsonContext.Default.SeedAnalysisDto);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(
                new ErrorDto { Error = ex.Message },
                MotelyJsonContext.Default.ErrorDto
            );
        }
    }

    [JSExport]
    public static Task<string> ValidateJamlAsync(string jamlContent)
    {
        var result = ValidateJaml(jamlContent);
        return Task.FromResult(result);
    }

    private static string ValidateJaml(string jamlContent)
    {
        try
        {
            if (
                !JamlConfigLoader.TryLoad(jamlContent, out var config, out var parseError)
                || config == null
            )
            {
                return JsonSerializer.Serialize(
                    new ValidateResultDto
                    {
                        Valid = false,
                        Error = parseError ?? "Failed to parse JAML",
                    },
                    MotelyJsonContext.Default.ValidateResultDto
                );
            }

            return JsonSerializer.Serialize(
                new ValidateResultDto
                {
                    Valid = true,
                    Name = config.Name,
                    Deck = config.Deck.ToString(),
                    Stake = config.Stake.ToString(),
                },
                MotelyJsonContext.Default.ValidateResultDto
            );
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(
                new ValidateResultDto { Valid = false, Error = ex.Message },
                MotelyJsonContext.Default.ValidateResultDto
            );
        }
    }

    /// <summary>Run keyword search - single keyword padded to 8 chars</summary>
    [JSExport]
    public static Task<string> RunKeywordSearchAsync(string jamlContent, string keyword, string? padding = null)
        => RunSearchAsyncCore(
            jamlContent,
            new SearchOptionsDto
            {
                Keyword = keyword,
                Padding = padding,
            },
            NodeSearchMode.Keywords
        );

    /// <summary>Run keyword search - multiple keywords</summary>
    [JSExport]
    public static async Task<string> RunKeywordsSearchAsync(
        string jamlContent,
        string[]? keywords,
        string? padding = null
    )
    {
        if (keywords == null)
            return ErrorJson("keywords is required.");

        return await RunSearchAsyncCore(
            jamlContent,
            new SearchOptionsDto
            {
                Keywords = keywords,
                Padding = padding,
            },
            NodeSearchMode.Keywords
        );
    }

    /// <summary>Run random seed search</summary>
    [JSExport]
    public static Task<string> RunRandomSearchAsync(string jamlContent, int count)
        => RunSearchAsyncCore(
            jamlContent,
            new SearchOptionsDto { RandomSeeds = count },
            NodeSearchMode.Random
        );

    /// <summary>Run palindrome search</summary>
    [JSExport]
    public static Task<string> RunPalindromeSearchAsync(string jamlContent)
        => RunSearchAsyncCore(
            jamlContent,
            new SearchOptionsDto { Palindrome = true },
            NodeSearchMode.Palindrome
        );

    /// <summary>Run any non-sequential search mode from JSON options.</summary>
    [JSExport]
    public static async Task<string> RunSearchAsync(string jamlContent, string optionsJson)
    {
        SearchOptionsDto? opts;
        try
        {
            opts = JsonSerializer.Deserialize<SearchOptionsDto>(
                optionsJson,
                MotelyJsonContext.Default.SearchOptionsDto
            );
        }
        catch (Exception ex)
        {
            return ErrorJson("Invalid options JSON: " + ex.Message);
        }

        if (opts == null)
            return ErrorJson("Search options are required.");

        return await RunSearchAsyncCore(jamlContent, opts, null);
    }

    /// <summary>Run list search (seeds[] or specificSeed). Returns JSON BlockSearchResultDto or ErrorDto.</summary>
    [JSExport]
    public static async Task<string> RunListSearchAsync(string jamlContent, string optionsJson)
    {
        SearchOptionsDto? opts;
        try
        {
            opts = JsonSerializer.Deserialize<SearchOptionsDto>(optionsJson, MotelyJsonContext.Default.SearchOptionsDto);
        }
        catch (Exception ex)
        {
            return ErrorJson("Invalid options JSON: " + ex.Message);
        }

        if (opts == null)
            return ErrorJson("Options required for list search");

        return await RunSearchAsyncCore(jamlContent, opts, NodeSearchMode.List);
    }

    /// <summary>Run a range of blocks [startBlockId, endBlockId). Returns aggregated JSON BlockSearchResultDto.</summary>
    [JSExport]
    public static async Task<string> RunSequentialRangeAsync(string jamlContent, int startBlockId, int endBlockId)
    {
        const int maxBlocks = 35 * 35 * 35;
        if (startBlockId < 0 || endBlockId > maxBlocks || startBlockId >= endBlockId)
            return ErrorJson($"Block range must be 0..{maxBlocks}, start < end");

        try
        {
            long totalSearched = 0;
            var allSeeds = new List<BlockSeedResultDto>();
            for (int blockId = startBlockId; blockId < endBlockId; blockId++)
            {
                var result = await ProcessBlockRunner.ProcessBlockAsync(jamlContent, blockId).ConfigureAwait(false);
                if (result == null)
                    return ErrorJson("Invalid JAML or blockId out of range");
                totalSearched += result.SeedsSearched;
                foreach (var s in result.Seeds)
                    allSeeds.Add(new BlockSeedResultDto { Seed = s.Seed, Score = s.Score });
            }

            var dto = new BlockSearchResultDto
            {
                BlockId = startBlockId,
                SeedsSearched = totalSearched,
                SeedsFound = allSeeds.Count,
                Seeds = allSeeds.ToArray(),
            };
            return JsonSerializer.Serialize(dto, MotelyJsonContext.Default.BlockSearchResultDto);
        }
        catch (Exception ex)
        {
            return ErrorJson(ex.Message);
        }
    }

    /// <summary>Run one block of sequential search. Returns JSON BlockSearchResultDto or ErrorDto.</summary>
    [JSExport]
    public static async Task<string> ProcessBlockAsync(string jamlContent, int blockId)
    {
        try
        {
            var result = await ProcessBlockRunner.ProcessBlockAsync(jamlContent, blockId).ConfigureAwait(false);
            if (result == null)
                return ErrorJson("Invalid JAML or blockId out of range");
            var dto = new BlockSearchResultDto
            {
                BlockId = result.BlockId,
                SeedsSearched = result.SeedsSearched,
                SeedsFound = result.SeedsFound,
                Seeds = result.Seeds
                    .Select(s => new BlockSeedResultDto { Seed = s.Seed, Score = s.Score })
                    .ToArray(),
            };
            return JsonSerializer.Serialize(dto, MotelyJsonContext.Default.BlockSearchResultDto);
        }
        catch (Exception ex)
        {
            return ErrorJson(ex.Message);
        }
    }


    private static string ErrorJson(string message) =>
        JsonSerializer.Serialize(
            new ErrorDto { Error = message },
            MotelyJsonContext.Default.ErrorDto
        );

    private static async Task<string> RunSearchAsyncCore(
        string jamlContent,
        SearchOptionsDto rawOptions,
        NodeSearchMode? expectedMode
    )
    {
        if (!JamlConfigLoader.TryLoad(jamlContent, out var config, out var error))
            return ErrorJson(error ?? "Invalid JAML");

        var (request, requestError) = MotelySearchRequestFactory.FromOptions(
            rawOptions,
            rawOptions.ThreadCount ?? Environment.ProcessorCount,
            rawOptions.BatchCharCount ?? 4
        );
        if (requestError != null || request == null)
            return ErrorJson(requestError ?? "Search request could not be created.");

        if (expectedMode.HasValue)
        {
            var expectedModeError = ValidateExpectedMode(expectedMode.Value, request);
            if (expectedModeError != null)
                return ErrorJson(expectedModeError);
        }

        var (plan, _, prepareError) = MotelySearchOrchestrator.PrepareSearch(config, request);
        if (prepareError != null || plan == null)
            return ErrorJson(prepareError ?? "Search could not be prepared.");

        var results = new List<BlockSeedResultDto>();
        var settings = plan.Settings;
        if (plan.ShouldClauseCount > 0)
        {
            settings = settings.WithScoredResultCallback(tally =>
                results.Add(
                    new BlockSeedResultDto
                    {
                        Seed = tally.Seed,
                        Score = tally.Score,
                    }
                )
            );
        }
        else
        {
            settings = settings.WithSeedMatchCallback(seed =>
                results.Add(
                    new BlockSeedResultDto
                    {
                        Seed = seed,
                        Score = 0,
                    }
                )
            );
        }

        using var search = settings.CreateSearch();
        await Task.Run(() => search.Start(CancellationToken.None));

        return JsonSerializer.Serialize(
            new BlockSearchResultDto
            {
                BlockId = 0,
                SeedsSearched = search.TotalSeedsSearched,
                SeedsFound = results.Count,
                Seeds = results.ToArray(),
            },
            MotelyJsonContext.Default.BlockSearchResultDto
        );
    }

    private static string? ValidateExpectedMode(
        NodeSearchMode expectedMode,
        MotelySearchRequest request
    )
    {
        return expectedMode switch
        {
            NodeSearchMode.List when request.Seeds is not { Length: > 0 } =>
                "List search requires at least one seed.",
            NodeSearchMode.Keywords when request.Keywords is not { Length: > 0 } =>
                "Keyword search requires at least one keyword.",
            NodeSearchMode.Random when !request.RandomSeeds.HasValue =>
                "Random search requires randomSeeds.",
            NodeSearchMode.Palindrome when !request.Palindrome =>
                "Palindrome search requires palindrome=true.",
            _ => null,
        };
    }

    private enum NodeSearchMode
    {
        List,
        Keywords,
        Random,
        Palindrome,
    }


    private static string GetCachedVersion() =>
        _cachedVersion ??= MotelyBuildVersion.For(typeof(MotelyNodeExports).Assembly);

    private static string[] GetFeatureList()
    {
        if (_cachedFeatures is not null)
            return _cachedFeatures;
        var features = new List<string> { "analyzer", "jaml-search", "jaml-validate" };
        if (IsSimdEnabled())
            features.Add("simd");
        features.Add("threads");
        _cachedFeatures = features.ToArray();
        return _cachedFeatures;
    }

    private static SeedAnalysisDto MapAnalysisToDto(
        MotelySeedAnalysis analysis,
        string seed,
        MotelyDeck deck,
        MotelyStake stake
    )
    {
        return new SeedAnalysisDto
        {
            Seed = seed,
            Deck = deck.ToString(),
            Stake = stake.ToString(),
            Error = analysis.Error,
            ErraticDeckComposition =
                analysis.ErraticDeckComposition?.Split(
                    ',',
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
                ) ?? [],
            Antes = analysis
                .Antes.Select(a => new AnteAnalysisDto
                {
                    Ante = a.Ante,
                    Boss = a.Boss.ToString(),
                    Voucher = a.Voucher.ToString(),
                    SmallBlindTag = a.SmallBlindTag.ToString(),
                    BigBlindTag = a.BigBlindTag.ToString(),
                    DrawOrder = a.DrawOrder ?? "",
                    ShopQueue = a
                        .ShopQueue.Select(item => new ShopItemDto
                        {
                            Id = item.Type.ToString(),
                            Name = item.ToString(),
                        })
                        .ToArray(),
                    Packs = a
                        .Packs.Select(p => new PackDto
                        {
                            Type = p.Type.ToString(),
                            Items = p.Items.Select(i => i.ToString()).ToArray(),
                        })
                        .ToArray(),
                })
                .ToArray(),
        };
    }
}
