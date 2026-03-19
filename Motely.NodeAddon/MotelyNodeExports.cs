using System.Runtime.Intrinsics;
using Microsoft.JavaScript.NodeApi;
using Motely;
using Motely.Analysis;
using Motely.Executors;
using Motely.Filters;

namespace Motely.NodeAddon;

/// <summary>
/// Node.js native addon exports. Thin wrappers around MotelyExports (shared orchestration).
/// node-api-dotnet auto-generates JS bindings for [JSExport].
/// JAMMY's index.cjs loads via require("./Motely.NodeAddon.node").MotelyNodeExports.
/// </summary>
[JSExport]
public static class MotelyNodeExports
{
    // ── Capabilities ──

    public static CapabilitiesDto GetCapabilities() => new()
    {
        Simd = MotelyExports.IsSimdEnabled(),
        Threads = true,
        AvailableThreadCount = MotelyExports.GetProcessorCount(),
        ProcessorCount = MotelyExports.GetProcessorCount(),
        Runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
        Version = MotelyExports.GetVersion(typeof(MotelyCore).Assembly),
        Timestamp = DateTime.UtcNow.ToString("o"),
    };

    // ── Seed analysis — delegates to MotelyExports ──

    public static SeedAnalysisDto AnalyzeSeed(string seed, string deck, string stake)
    {
        try { return MotelyExports.AnalyzeSeed(seed, deck, stake); }
        catch (Exception ex)
        {
            return new SeedAnalysisDto { Seed = seed, Deck = deck, Stake = stake, Error = ex.Message };
        }
    }

    // ── JAML validation — delegates to MotelyExports ──

    public static ValidateResultDto ValidateJaml(string jamlContent)
    {
        if (JamlConfigLoader.TryLoad(jamlContent, out var config, out var error))
            return new ValidateResultDto
            {
                Valid = true,
                Name = config?.Name,
                Deck = config?.Deck.ToString(),
                Stake = config?.Stake.ToString(),
            };

        return new ValidateResultDto { Valid = false, Error = error ?? "Unknown error" };
    }

    // ── Single-block search (distributed worker pool) — delegates to ProcessBlockRunner ──

    public static async Task<BlockSearchResultDto> ProcessBlockAsync(string jamlContent, int blockId)
    {
        var result = await ProcessBlockRunner.ProcessBlockAsync(jamlContent, blockId);
        if (result == null)
            return new BlockSearchResultDto { BlockId = blockId };

        return ToDto(result);
    }

    // ── Full searches — all delegate to MotelyExports.RunSearch ──

    public static BlockSearchResultDto RunSequentialRangeAsync(
        string jamlContent, int startBlock, int endBlock)
    {
        return RunViaShared(jamlContent, new MotelySearchRequest
        {
            ThreadCount = Environment.ProcessorCount,
            BatchCharCount = ProcessBlockRunner.BatchCharCount,
            StartBatch = startBlock,
            EndBatch = endBlock,
        }, startBlock);
    }

    public static BlockSearchResultDto RunListSearchAsync(
        string jamlContent, string[] seeds)
    {
        return RunViaShared(jamlContent, new MotelySearchRequest
        {
            ThreadCount = Environment.ProcessorCount,
            BatchCharCount = ProcessBlockRunner.BatchCharCount,
            Seeds = seeds,
        });
    }

    public static BlockSearchResultDto RunKeywordsSearchAsync(
        string jamlContent, string[] keywords, string? padding)
    {
        return RunViaShared(jamlContent, new MotelySearchRequest
        {
            ThreadCount = Environment.ProcessorCount,
            BatchCharCount = ProcessBlockRunner.BatchCharCount,
            Keywords = keywords,
            Padding = string.IsNullOrEmpty(padding) ? null : padding,
        });
    }

    public static BlockSearchResultDto RunRandomSearchAsync(
        string jamlContent, int count)
    {
        return RunViaShared(jamlContent, new MotelySearchRequest
        {
            ThreadCount = Environment.ProcessorCount,
            BatchCharCount = ProcessBlockRunner.BatchCharCount,
            RandomSeeds = count > 0 ? count : 1000,
        });
    }

    public static BlockSearchResultDto RunPalindromeSearchAsync(string jamlContent)
    {
        return RunViaShared(jamlContent, new MotelySearchRequest
        {
            ThreadCount = Environment.ProcessorCount,
            BatchCharCount = ProcessBlockRunner.BatchCharCount,
            Palindrome = true,
        });
    }

    // ── Private helpers ──

    private static BlockSearchResultDto RunViaShared(
        string jamlContent, MotelySearchRequest request, int blockId = 0)
    {
        // Collect at the wrapper level — bounded by JS-side chunking (500 blocks per call).
        // The orchestrator streams via callbacks; this is just the final DTO for the JS boundary.
        var seeds = new List<string>();
        int highestScore = 0;

        var (status, seedsFound, _) = MotelyExports.RunSearch(jamlContent, request,
            onResult: (seed, score) =>
            {
                seeds.Add(seed);
                if (score > highestScore) highestScore = score;
            });

        return new BlockSearchResultDto
        {
            BlockId = blockId,
            SeedsFound = seedsFound,
            HighestScore = highestScore,
            Seeds = seeds.ToArray(),
        };
    }

    private static BlockSearchResultDto ToDto(BlockSearchResult result) => new()
    {
        BlockId = result.BlockId,
        SeedsFound = result.SeedsFound,
        HighestScore = result.HighestScore,
        Seeds = result.Seeds.ToArray(),
    };
}
