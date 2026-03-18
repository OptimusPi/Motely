using Microsoft.JavaScript.NodeApi;
using Motely;
using Motely.Analysis;
using Motely.Executors;
using Motely.Filters;
using BlockSearchResultDto = global::Motely.BlockSearchResultDto;
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

    // ── Version / Capabilities ───────────────────────────────────────────────

    [JSExport]
    public static VersionDto GetVersion() =>
        new()
        {
            Version = GetCachedVersion(),
            Runtime = "node-addon",
            Features = MotelyRuntime.GetFeatureList("node-addon", Environment.ProcessorCount),
        };

    [JSExport]
    public static CapabilitiesDto GetCapabilities() =>
        new()
        {
            Simd = MotelyRuntime.IsSimdEnabled(),
            Threads = true,
            AvailableThreadCount = Environment.ProcessorCount,
            ProcessorCount = Environment.ProcessorCount,
            Runtime = "node-addon",
            Version = GetCachedVersion(),
            Timestamp = DateTime.UtcNow.ToString("O"),
        };

    // ── Seed Analysis ────────────────────────────────────────────────────────

    [JSExport]
    public static SeedAnalysisDto AnalyzeSeed(string seed, string deck, string stake) =>
        MotelyRuntime.AnalyzeSeed(seed, deck, stake);

    // ── JAML Validation ──────────────────────────────────────────────────────

    [JSExport]
    public static ValidateResultDto ValidateJaml(string jamlContent) =>
        MotelyRuntime.ValidateJaml(jamlContent);

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
        out MotelyDeck deckEnum, out MotelyStake stakeEnum) =>
        MotelyRuntime.ParseEnums(deck, stake, out deckEnum, out stakeEnum);

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
        int maxBlocks = ProcessBlockRunner.TotalBlocks;
        if (startBlockId < 0 || endBlockId > maxBlocks || startBlockId >= endBlockId)
            throw new ArgumentOutOfRangeException(
                nameof(startBlockId),
                $"Block range must be 0..{maxBlocks} with start < end."
            );

        var allSeeds = new List<string>();
        int highestScore = 0;

        for (int blockId = startBlockId; blockId < endBlockId; blockId++)
        {
            var result = await ProcessBlockRunner
                .ProcessBlockAsync(jamlContent, blockId)
                .ConfigureAwait(false);

            if (result == null)
                throw new InvalidOperationException(
                    $"Invalid JAML or block {blockId} out of range."
                );

            allSeeds.AddRange(result.Seeds);
            if (result.HighestScore > highestScore) highestScore = result.HighestScore;
        }

        return new BlockSearchResultDto
        {
            BlockId = startBlockId,
            SeedsFound = allSeeds.Count,
            HighestScore = highestScore,
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
            SeedsFound = result.SeedsFound,
            HighestScore = result.HighestScore,
            Seeds = result.Seeds.ToArray(),
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

        var seeds = new List<string>();
        int highestScore = 0;
        var settings = plan.Settings;

        if (plan.ShouldClauseCount > 0)
        {
            settings = settings.WithScoredResultCallback(tally =>
            {
                seeds.Add(tally.Seed);
                if (tally.Score > highestScore) highestScore = tally.Score;
            });
        }
        else
        {
            settings = settings.WithSeedMatchCallback(seed => seeds.Add(seed));
        }

        using var search = settings.CreateSearch();
        await Task.Run(() => search.Start(CancellationToken.None));

        return new BlockSearchResultDto
        {
            BlockId = 0,
            SeedsFound = seeds.Count,
            HighestScore = highestScore,
            Seeds = seeds.ToArray(),
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

    private static string GetCachedVersion() =>
        _cachedVersion ??= MotelyRuntime.GetVersion(typeof(MotelyNodeExports).Assembly);
}
