using Microsoft.JavaScript.NodeApi;
using Motely;
using Motely.Analysis;
using Motely.Executors;
using Motely.Filters;
using BlockSearchResultDto = global::Motely.BlockSearchResultDto;
using BlockSeedResultDto = global::Motely.BlockSeedResultDto;
using CapabilitiesDto = global::Motely.CapabilitiesDto;
using SearchOptionsDto = global::Motely.SearchOptionsDto;
using ValidateResultDto = global::Motely.ValidateResultDto;
using VersionDto = global::Motely.VersionDto;

namespace Motely.NodeAddon;

/// <summary>
/// Node addon exports — typed returns, no JSON serialization.
/// node-api-dotnet Generator marshals C# types ↔ JS objects at compile time.
/// Errors throw exceptions; they propagate as JS errors automatically.
/// </summary>
[JSExport]
public static class MotelyNodeExports
{
    private static string? _cachedVersion;
    private static string[]? _cachedFeatures;

    // ── Version / Capabilities ───────────────────────────────────────────────

    [JSExport]
    public static VersionDto GetVersion() =>
        new()
        {
            Version = GetCachedVersion(),
            Runtime = "node-addon",
            Features = GetFeatureList(),
        };

    [JSExport]
    public static CapabilitiesDto GetCapabilities() =>
        new()
        {
            Simd = IsSimdEnabled(),
            Threads = true,
            AvailableThreadCount = Environment.ProcessorCount,
            ProcessorCount = Environment.ProcessorCount,
            Runtime = "node-addon",
            Version = GetCachedVersion(),
            Timestamp = DateTime.UtcNow.ToString("O"),
        };

    // ── Seed Analysis ────────────────────────────────────────────────────────

    [JSExport]
    public static SeedAnalysisDto AnalyzeSeed(string seed, string deck, string stake)
    {
        if (!Enum.TryParse<MotelyDeck>(deck, true, out var deckEnum))
            throw new ArgumentException($"Unknown deck: '{deck}'", nameof(deck));
        if (!Enum.TryParse<MotelyStake>(stake, true, out var stakeEnum))
            throw new ArgumentException($"Unknown stake: '{stake}'", nameof(stake));

        var cfg = new MotelySeedAnalysisConfig(seed, deckEnum, stakeEnum);
        var analysis = MotelySeedAnalyzer.Analyze(cfg);

        if (!string.IsNullOrEmpty(analysis.Error))
            throw new InvalidOperationException(analysis.Error);

        return MapAnalysisToDto(analysis, seed, deckEnum, stakeEnum);
    }

    // ── JAML Validation ──────────────────────────────────────────────────────

    [JSExport]
    public static ValidateResultDto ValidateJaml(string jamlContent)
    {
        if (!JamlConfigLoader.TryLoad(jamlContent, out var config, out var parseError) || config == null)
            return new ValidateResultDto { Valid = false, Error = parseError ?? "Failed to parse JAML" };

        return new ValidateResultDto
        {
            Valid = true,
            Name = config.Name,
            Deck = config.Deck.ToString(),
            Stake = config.Stake.ToString(),
        };
    }

    // ── Stream Cursors ───────────────────────────────────────────────────────

    /// <summary>
    /// Stream Lucky Money results with cursor pattern.
    /// state &lt; 0 → start fresh. state = savedDouble → resume.
    /// Returns { results: bool[], nextState: double }.
    /// </summary>
    [JSExport]
    public static LuckyMoneyStreamDto StreamLuckyMoney(
        string seed,
        string deck,
        string stake,
        double state,
        int take,
        double baseLuck = 1
    )
    {
        if (!Enum.TryParse<MotelyDeck>(deck, true, out var deckEnum))
            throw new ArgumentException($"Unknown deck: '{deck}'", nameof(deck));
        if (!Enum.TryParse<MotelyStake>(stake, true, out var stakeEnum))
            throw new ArgumentException($"Unknown stake: '{stake}'", nameof(stake));

        double? cursorState = state < 0 ? null : state;

        var (results, nextState) = MotelySeedStreamer.StreamLuckyMoney(
            seed, deckEnum, stakeEnum, cursorState, take, baseLuck);

        return new LuckyMoneyStreamDto
        {
            Results = results,
            NextState = nextState,
        };
    }

    [JSExport]
    public static LuckyMoneyStreamDto StreamLuckyMult(
        string seed, string deck, string stake,
        double state, int take, double baseLuck = 1)
    {
        ParseEnums(deck, stake, out var d, out var s);
        double? cs = state < 0 ? null : state;
        var (results, nextState) = MotelySeedStreamer.StreamLuckyMult(seed, d, s, cs, take, baseLuck);
        return new() { Results = results, NextState = nextState };
    }

    [JSExport]
    public static IntStreamDto StreamMisprint(
        string seed, string deck, string stake,
        double state, int take)
    {
        ParseEnums(deck, stake, out var d, out var s);
        double? cs = state < 0 ? null : state;
        var (results, nextState) = MotelySeedStreamer.StreamMisprint(seed, d, s, cs, take);
        return new() { Results = results, NextState = nextState };
    }

    [JSExport]
    public static LuckyMoneyStreamDto StreamCavendish(
        string seed, string deck, string stake,
        double state, int take, double baseLuck = 1)
    {
        ParseEnums(deck, stake, out var d, out var s);
        double? cs = state < 0 ? null : state;
        var (results, nextState) = MotelySeedStreamer.StreamCavendish(seed, d, s, cs, take, baseLuck);
        return new() { Results = results, NextState = nextState };
    }

    [JSExport]
    public static LuckyMoneyStreamDto StreamGrosMichel(
        string seed, string deck, string stake,
        double state, int take, double baseLuck = 1)
    {
        ParseEnums(deck, stake, out var d, out var s);
        double? cs = state < 0 ? null : state;
        var (results, nextState) = MotelySeedStreamer.StreamGrosMichel(seed, d, s, cs, take, baseLuck);
        return new() { Results = results, NextState = nextState };
    }

    [JSExport]
    public static StringStreamDto StreamErraticDeck(
        string seed, string deck, string stake,
        double state, int take)
    {
        ParseEnums(deck, stake, out var d, out var s);
        double? cs = state < 0 ? null : state;
        var (results, nextState) = MotelySeedStreamer.StreamErraticDeck(seed, d, s, cs, take);
        return new() { Results = results, NextState = nextState };
    }

    [JSExport]
    public static StringStreamDto StreamWheelOfFortune(
        string seed, string deck, string stake,
        double state, int take, double baseLuck = 1)
    {
        ParseEnums(deck, stake, out var d, out var s);
        double? cs = state < 0 ? null : state;
        var (results, nextState) = MotelySeedStreamer.StreamWheelOfFortune(seed, d, s, cs, take, baseLuck);
        return new() { Results = results, NextState = nextState };
    }

    [JSExport]
    public static StringStreamDto StreamTags(
        string seed, string deck, string stake,
        int ante, double state, int take)
    {
        ParseEnums(deck, stake, out var d, out var s);
        double? cs = state < 0 ? null : state;
        var (results, nextState) = MotelySeedStreamer.StreamTags(seed, d, s, ante, cs, take);
        return new() { Results = results, NextState = nextState };
    }

    [JSExport]
    public static PackStreamDto StreamBoosterPacks(
        string seed, string deck, string stake,
        int ante, double state, bool generatedFirstPack, int take)
    {
        ParseEnums(deck, stake, out var d, out var s);
        double? cs = state < 0 ? null : state;
        var (results, nextState, nextGen) = MotelySeedStreamer.StreamBoosterPacks(
            seed, d, s, ante, cs, generatedFirstPack, take);
        return new() { Results = results, NextState = nextState, GeneratedFirstPack = nextGen };
    }

    [JSExport]
    public static StringStreamDto StreamVouchers(
        string seed, string deck, string stake,
        int ante, int voucherBitfield, double state, int take)
    {
        ParseEnums(deck, stake, out var d, out var s);
        double? cs = state < 0 ? null : state;
        var (results, nextState) = MotelySeedStreamer.StreamVouchers(seed, d, s, ante, voucherBitfield, cs, take);
        return new() { Results = results, NextState = nextState };
    }

    [JSExport]
    public static ItemStreamDto StreamTarot(
        string seed, string deck, string stake,
        int ante, string source, double[] state, int take)
    {
        ParseEnums(deck, stake, out var d, out var s);
        double[]? cs = state.Length == 0 ? null : state;
        var (results, nextState) = MotelySeedStreamer.StreamTarot(seed, d, s, ante, source, cs, take);
        return new() { Results = results, NextState = nextState };
    }

    [JSExport]
    public static ItemStreamDto StreamPlanet(
        string seed, string deck, string stake,
        int ante, string source, double[] state, int take)
    {
        ParseEnums(deck, stake, out var d, out var s);
        double[]? cs = state.Length == 0 ? null : state;
        var (results, nextState) = MotelySeedStreamer.StreamPlanet(seed, d, s, ante, source, cs, take);
        return new() { Results = results, NextState = nextState };
    }

    [JSExport]
    public static ItemStreamDto StreamSpectral(
        string seed, string deck, string stake,
        int ante, string source, double[] state, int take)
    {
        ParseEnums(deck, stake, out var d, out var s);
        double[]? cs = state.Length == 0 ? null : state;
        var (results, nextState) = MotelySeedStreamer.StreamSpectral(seed, d, s, ante, source, cs, take);
        return new() { Results = results, NextState = nextState };
    }

    [JSExport]
    public static ItemStreamDto StreamStandardCards(
        string seed, string deck, string stake,
        int ante, int flags, double[] state, int take)
    {
        ParseEnums(deck, stake, out var d, out var s);
        double[]? cs = state.Length == 0 ? null : state;
        var (results, nextState) = MotelySeedStreamer.StreamStandardCards(seed, d, s, ante, flags, cs, take);
        return new() { Results = results, NextState = nextState };
    }

    [JSExport]
    public static ItemStreamDto StreamJokers(
        string seed, string deck, string stake,
        int ante, string source, int flags, double[] state, int take)
    {
        ParseEnums(deck, stake, out var d, out var s);
        double[]? cs = state.Length == 0 ? null : state;
        var (results, nextState) = MotelySeedStreamer.StreamJokers(seed, d, s, ante, source, flags, cs, take);
        return new() { Results = results, NextState = nextState };
    }

    private static void ParseEnums(string deck, string stake,
        out MotelyDeck deckEnum, out MotelyStake stakeEnum)
    {
        if (!Enum.TryParse<MotelyDeck>(deck, true, out deckEnum))
            throw new ArgumentException($"Unknown deck: '{deck}'", nameof(deck));
        if (!Enum.TryParse<MotelyStake>(stake, true, out stakeEnum))
            throw new ArgumentException($"Unknown stake: '{stake}'", nameof(stake));
    }

    // ── Searches ─────────────────────────────────────────────────────────────

    /// <summary>Single keyword padded to 8 chars.</summary>
    [JSExport]
    public static Task<BlockSearchResultDto> RunKeywordSearchAsync(
        string jamlContent,
        string keyword,
        string? padding = null
    ) =>
        RunSearchAsyncCore(
            jamlContent,
            new SearchOptionsDto { Keyword = keyword, Padding = padding },
            NodeSearchMode.Keywords
        );

    /// <summary>Multiple keywords — each padded to 8 chars, results unioned.</summary>
    [JSExport]
    public static async Task<BlockSearchResultDto> RunKeywordsSearchAsync(
        string jamlContent,
        string[]? keywords,
        string? padding = null
    )
    {
        if (keywords == null || keywords.Length == 0)
            throw new ArgumentException("At least one keyword is required.", nameof(keywords));

        return await RunSearchAsyncCore(
            jamlContent,
            new SearchOptionsDto { Keywords = keywords, Padding = padding },
            NodeSearchMode.Keywords
        );
    }

    /// <summary>Random seed search.</summary>
    [JSExport]
    public static Task<BlockSearchResultDto> RunRandomSearchAsync(string jamlContent, int count) =>
        RunSearchAsyncCore(
            jamlContent,
            new SearchOptionsDto { RandomSeeds = count },
            NodeSearchMode.Random
        );

    /// <summary>Palindrome seed search.</summary>
    [JSExport]
    public static Task<BlockSearchResultDto> RunPalindromeSearchAsync(string jamlContent) =>
        RunSearchAsyncCore(
            jamlContent,
            new SearchOptionsDto { Palindrome = true },
            NodeSearchMode.Palindrome
        );

    /// <summary>Search a specific list of seeds.</summary>
    [JSExport]
    public static async Task<BlockSearchResultDto> RunListSearchAsync(
        string jamlContent,
        string[] seeds
    )
    {
        if (seeds == null || seeds.Length == 0)
            throw new ArgumentException("At least one seed is required.", nameof(seeds));

        return await RunSearchAsyncCore(
            jamlContent,
            new SearchOptionsDto { Seeds = seeds },
            NodeSearchMode.List
        );
    }

    /// <summary>
    /// Sequential range search [startBlockId, endBlockId).
    /// Returns the aggregated results across all blocks in the range.
    /// </summary>
    [JSExport]
    public static async Task<BlockSearchResultDto> RunSequentialRangeAsync(
        string jamlContent,
        int startBlockId,
        int endBlockId
    )
    {
        const int maxBlocks = 35 * 35 * 35;
        if (startBlockId < 0 || endBlockId > maxBlocks || startBlockId >= endBlockId)
            throw new ArgumentOutOfRangeException(
                nameof(startBlockId),
                $"Block range must be 0..{maxBlocks} with start < end."
            );

        long totalSearched = 0;
        var allSeeds = new List<BlockSeedResultDto>();

        for (int blockId = startBlockId; blockId < endBlockId; blockId++)
        {
            var result = await ProcessBlockRunner
                .ProcessBlockAsync(jamlContent, blockId)
                .ConfigureAwait(false);

            if (result == null)
                throw new InvalidOperationException(
                    $"Invalid JAML or block {blockId} out of range."
                );

            totalSearched += result.SeedsSearched;
            foreach (var s in result.Seeds)
                allSeeds.Add(new BlockSeedResultDto { Seed = s.Seed, Score = s.Score });
        }

        return new BlockSearchResultDto
        {
            BlockId = startBlockId,
            SeedsSearched = totalSearched,
            SeedsFound = allSeeds.Count,
            Seeds = allSeeds.ToArray(),
        };
    }

    /// <summary>Run a single block of sequential search.</summary>
    [JSExport]
    public static async Task<BlockSearchResultDto> ProcessBlockAsync(
        string jamlContent,
        int blockId
    )
    {
        var result = await ProcessBlockRunner
            .ProcessBlockAsync(jamlContent, blockId)
            .ConfigureAwait(false);

        if (result == null)
            throw new InvalidOperationException("Invalid JAML or blockId out of range.");

        return new BlockSearchResultDto
        {
            BlockId = result.BlockId,
            SeedsSearched = result.SeedsSearched,
            SeedsFound = result.SeedsFound,
            Seeds = result
                .Seeds.Select(s => new BlockSeedResultDto { Seed = s.Seed, Score = s.Score })
                .ToArray(),
        };
    }

    // ── Internal ─────────────────────────────────────────────────────────────

    private static async Task<BlockSearchResultDto> RunSearchAsyncCore(
        string jamlContent,
        SearchOptionsDto rawOptions,
        NodeSearchMode? expectedMode
    )
    {
        if (!JamlConfigLoader.TryLoad(jamlContent, out var config, out var error) || config == null)
            throw new InvalidOperationException(error ?? "Invalid JAML.");

        var (request, requestError) = MotelySearchRequestFactory.FromOptions(
            rawOptions,
            rawOptions.ThreadCount ?? Environment.ProcessorCount,
            rawOptions.BatchCharCount ?? 4
        );
        if (requestError != null || request == null)
            throw new InvalidOperationException(requestError ?? "Search request could not be created.");

        if (expectedMode.HasValue)
            ValidateExpectedMode(expectedMode.Value, request);

        var (plan, _, prepareError) = MotelySearchOrchestrator.PrepareSearch(config, request);
        if (prepareError != null || plan == null)
            throw new InvalidOperationException(prepareError ?? "Search could not be prepared.");

        var results = new List<BlockSeedResultDto>();
        var settings = plan.Settings;

        if (plan.ShouldClauseCount > 0)
        {
            settings = settings.WithScoredResultCallback(tally =>
                results.Add(new BlockSeedResultDto { Seed = tally.Seed, Score = tally.Score })
            );
        }
        else
        {
            settings = settings.WithSeedMatchCallback(seed =>
                results.Add(new BlockSeedResultDto { Seed = seed, Score = 0 })
            );
        }

        using var search = settings.CreateSearch();
        await Task.Run(() => search.Start(CancellationToken.None));

        return new BlockSearchResultDto
        {
            BlockId = 0,
            SeedsSearched = search.TotalSeedsSearched,
            SeedsFound = results.Count,
            Seeds = results.ToArray(),
        };
    }

    private static void ValidateExpectedMode(NodeSearchMode mode, MotelySearchRequest request)
    {
        var error = mode switch
        {
            NodeSearchMode.List when request.Seeds is not { Length: > 0 } =>
                "List search requires at least one seed.",
            NodeSearchMode.Keywords when request.Keywords is not { Length: > 0 } =>
                "Keyword search requires at least one keyword.",
            NodeSearchMode.Random when !request.RandomSeeds.HasValue =>
                "Random search requires a count.",
            NodeSearchMode.Palindrome when !request.Palindrome =>
                "Palindrome search requires palindrome=true.",
            _ => null,
        };

        if (error != null)
            throw new ArgumentException(error);
    }

    private enum NodeSearchMode
    {
        List,
        Keywords,
        Random,
        Palindrome,
    }

    private static bool IsSimdEnabled() => System.Runtime.Intrinsics.Vector128.IsHardwareAccelerated;

    private static string GetCachedVersion() =>
        _cachedVersion ??= MotelyBuildVersion.For(typeof(MotelyNodeExports).Assembly);

    private static string[] GetFeatureList()
    {
        if (_cachedFeatures is not null)
            return _cachedFeatures;

        var features = new List<string> { "analyzer", "jaml-search", "jaml-validate", "lucky-money-stream" };
        if (IsSimdEnabled())
            features.Add("simd");
        features.Add("threads");

        return _cachedFeatures = features.ToArray();
    }

    private static SeedAnalysisDto MapAnalysisToDto(
        MotelySeedAnalysis analysis,
        string seed,
        MotelyDeck deck,
        MotelyStake stake
    ) =>
        new()
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
