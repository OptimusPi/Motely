using System.Collections.Concurrent;
using System.Runtime.InteropServices.JavaScript;
using Motely;
using Motely.Analysis;
using Motely.Executors;
using Motely.Filters;
using Motely.Reporting;
using Motely.Utils;

namespace Motely.WASM;

public record CapabilitiesDto(
    bool simd,
    bool threads,
    int processorCount,
    string runtime,
    string version,
    string timestamp,
    string? note
);

public record VersionDto(string version, string runtime, string framework);

public record SearchStatusDto(
    string searchId,
    string status,
    bool isRunning,
    int progressPercent,
    long totalSeedsSearched,
    long matchingSeeds,
    int resultCount,
    SearchResultDto[] results,
    string? error
);

public record SearchResultDto(string seed, int score, int[]? tallies);

/// <summary>
/// Main WASM API entry point for Motely in the browser.
/// Exports search, analysis, and capability detection methods.
/// </summary>
public static partial class MotelyWasm
{
    /// <summary>
    /// Get runtime capabilities including SIMD, threads, and environment info.
    /// Marshalled to JS as plain object.
    /// </summary>
    [JSExport]
    [return: JSMarshalAs<JSType.Any>]
    public static object GetCapabilities()
    {
        return new CapabilitiesDto(
            simd: IsSimdEnabled(),
            threads: IsThreadingEnabled(),
            processorCount: Environment.ProcessorCount,
            runtime: "WASM",
            version: "1.0.8",
            timestamp: DateTime.UtcNow.ToString("O"),
            note: "Thread count defaults to Environment.ProcessorCount"
        );
    }

    /// <summary>
    /// Check if SIMD is enabled at runtime
    /// Checks WASM PackedSimd support
    /// </summary>
    [JSExport]
    public static bool IsSimdEnabled()
    {
        // Check WASM-specific SIMD support
        // This is set at build time via WasmSIMD property
        return System.Runtime.Intrinsics.Wasm.PackedSimd.IsSupported;
    }

    /// <summary>
    /// Check if threading is enabled
    /// Uses GetMinThreads (read-only) instead of SetMinThreads to avoid side effects
    /// </summary>
    [JSExport]
    public static bool IsThreadingEnabled()
    {
        try
        {
            // Read-only check: GetMinThreads works if threading is available
            // In WASM without threads, this will throw or return false
            ThreadPool.GetMinThreads(out int workerThreads, out int completionPortThreads);
            // If we got here, threading is available
            return true;
        }
        catch
        {
            // Threading not available or not enabled
            return false;
        }
    }

    /// <summary>
    /// Get processor/core count (useful for thread pool sizing)
    /// </summary>
    [JSExport]
    public static int GetProcessorCount()
    {
        return Environment.ProcessorCount;
    }

    /// <summary>
    /// Simple version check. Marshalled to JS as plain object.
    /// </summary>
    [JSExport]
    [return: JSMarshalAs<JSType.Any>]
    public static object GetVersion()
    {
        return new VersionDto(version: "1.0.8", runtime: "WASM", framework: "net10.0-browser");
    }

    /// <summary>
    /// Analyze a single seed and return detailed results.
    /// Arguments match legacy index.js signature; ante/shop/config currently unused.
    /// Marshalled to JS as plain object.
    /// </summary>
    [JSExport]
    [return: JSMarshalAs<JSType.Any>]
    public static object AnalyzeSeed(
        string seed,
        string deck,
        string stake,
        int ante,
        int shop,
        string config
    )
    {
        SeedAnalysisDto dto;

        if (!Enum.TryParse<MotelyDeck>(deck, true, out var deckEnum))
        {
            dto = new SeedAnalysisDto { Error = $"Invalid deck: {deck}" };
            return dto;
        }

        if (!Enum.TryParse<MotelyStake>(stake, true, out var stakeEnum))
        {
            dto = new SeedAnalysisDto { Error = $"Invalid stake: {stake}" };
            return dto;
        }

        var analysis = MotelySeedAnalyzer.Analyze(
            new MotelySeedAnalysisConfig(seed, deckEnum, stakeEnum)
        );

        var erraticComposition =
            analysis.ErraticDeckComposition?.Split(',', StringSplitOptions.RemoveEmptyEntries)
            ?? Array.Empty<string>();

        dto = new SeedAnalysisDto
        {
            Seed = seed,
            Deck = deckEnum.ToString(),
            Stake = stakeEnum.ToString(),
            ErraticDeckComposition = erraticComposition,
            Twos = erraticComposition.Count(c => c.StartsWith("2_")),
            Error = analysis.Error,
            Antes = analysis
                .Antes.Select(a => new AnteAnalysisDto
                {
                    Ante = a.Ante,
                    Boss = FormatUtils.FormatBoss(a.Boss),
                    Voucher = FormatUtils.FormatVoucher(a.Voucher),
                    SmallBlindTag = FormatUtils.FormatTag(a.SmallBlindTag),
                    BigBlindTag = FormatUtils.FormatTag(a.BigBlindTag),
                    DrawOrder = a.DrawOrder ?? string.Empty,
                    ShopQueue = a
                        .ShopQueue.Select(item => new ShopItemDto
                        {
                            Id = item.ToString(),
                            Name = FormatUtils.FormatItem(item),
                        })
                        .ToArray(),
                    Packs = a
                        .Packs.Select(pack => new PackDto
                        {
                            Type = FormatUtils.FormatPackName(pack.Type),
                            Items = pack
                                .Items.Select(item => FormatUtils.FormatItem(item))
                                .ToArray(),
                        })
                        .ToArray(),
                })
                .ToArray(),
        };

        return dto;
    }

    // Search management - store active searches
    private static readonly ConcurrentDictionary<string, IMotelySearchContext> _activeSearches =
        new();

    /// <summary>
    /// Start a JAML search. Returns search ID for polling results.
    /// In-memory JAML search (no file output). Parses JAML, launches orchestrator with useInMemoryStorage: true.
    /// </summary>
    [JSExport]
    public static string StartJamlSearch(
        string jamlContent,
        string deck = "Red",
        string stake = "White",
        int threads = 0,
        int batchSize = 4,
        int startBatch = 0,
        int endBatch = 0,
        int cutoff = 0
    )
    {
        if (
            !JamlConfigLoader.TryLoadFromJamlString(jamlContent, out var config, out var error)
            || config == null
        )
            throw new InvalidOperationException($"Failed to parse JAML: {error}");

        if (!string.IsNullOrEmpty(deck))
            config.Deck = deck;
        if (!string.IsNullOrEmpty(stake))
            config.Stake = stake;

        var parameters = new JsonSearchParams
        {
            Threads = threads > 0 ? threads : Environment.ProcessorCount,
            BatchSize = batchSize,
            StartBatch = (ulong)Math.Max(0, startBatch),
            EndBatch = (ulong)Math.Max(0, endBatch),
            Cutoff = cutoff,
            CutoffMode = cutoff > 0 ? ScoreCutoffMode.Manual : ScoreCutoffMode.None,
            Quiet = true,
        };

        var context = MotelySearchOrchestrator.LaunchWithContext(
            config,
            parameters,
            useInMemoryStorage: true
        );
        context.Start();
        _activeSearches[context.SearchId] = context;
        return context.SearchId;
    }

    /// <summary>
    /// Get search status and results. Marshalled to JS as plain object.
    /// </summary>
    [JSExport]
    [return: JSMarshalAs<JSType.Any>]
    public static object GetSearchStatus(string searchId, int limit = 100)
    {
        SearchStatusDto dto;

        if (!_activeSearches.TryGetValue(searchId, out var context))
        {
            dto = new SearchStatusDto(
                searchId: searchId,
                status: "not_found",
                isRunning: false,
                progressPercent: 0,
                totalSeedsSearched: 0,
                matchingSeeds: 0,
                resultCount: 0,
                results: Array.Empty<SearchResultDto>(),
                error: "Search not found"
            );
            return dto;
        }

        var results = context.GetTopResults(limit);
        var progressPercent = context.IsSequentialBatchSearch
            ? (int)((double)context.CompletedBatchCount / Math.Pow(35, 8 - 4) * 100) // Approximate
            : 0;

        dto = new SearchStatusDto(
            searchId: context.SearchId,
            status: context.Status.ToString().ToLowerInvariant(),
            isRunning: context.Status == MotelySearchStatus.Running,
            progressPercent: progressPercent,
            totalSeedsSearched: context.TotalSeedsSearched,
            matchingSeeds: context.MatchingSeeds,
            resultCount: context.ResultCount,
            results: results
                .Select(r => new SearchResultDto(
                    seed: r.Seed,
                    score: r.Score,
                    tallies: r.Tallies?.ToArray()
                ))
                .ToArray(),
            error: null
        );

        return dto;
    }

    /// <summary>
    /// Stop a search
    /// </summary>
    [JSExport]
    public static void StopSearch(string searchId)
    {
        if (_activeSearches.TryGetValue(searchId, out var context))
        {
            context.Cancel();
            context.Dispose();
            _activeSearches.TryRemove(searchId, out _);
        }
    }
}
