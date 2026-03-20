using System.Runtime.Intrinsics;
using Microsoft.JavaScript.NodeApi;
using Motely;
using Motely.Analysis;
using Motely.Executors;
using Motely.Filters;

namespace Motely.NodeAddon;

[JSExport]
public static class MotelyNodeExports
{
    // ── Capabilities ──

    public static CapabilitiesDto GetCapabilities() => new()
    {
        Simd = Vector128.IsHardwareAccelerated,
        Threads = true,
        AvailableThreadCount = Environment.ProcessorCount,
        ProcessorCount = Environment.ProcessorCount,
        Runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
        Version = MotelyBuildVersion.For(typeof(MotelyCore).Assembly),
        Timestamp = DateTime.UtcNow.ToString("o"),
    };

    // ── Seed analysis ──

    public static SeedAnalysisDto AnalyzeSeed(string seed, string deck, string stake)
    {
        try { return MotelySeedAnalyzer.AnalyzeToDto(seed, deck, stake); }
        catch (Exception ex)
        {
            return new SeedAnalysisDto { Seed = seed, Deck = deck, Stake = stake, Error = ex.Message };
        }
    }

    // ── JAML validation ──

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

    // ── Single-block search (distributed worker pool) ──

    public static async Task<BlockSearchResultDto> ProcessBlockAsync(string jamlContent, int blockId)
    {
        var result = await ProcessBlockRunner.ProcessBlockAsync(jamlContent, blockId);
        if (result == null)
            return new BlockSearchResultDto { BlockId = blockId };

        return ToDto(result);
    }

    // ── Full searches — all delegate to MotelySearchOrchestrator.RunSearch ──

    public static BlockSearchResultDto RunSequentialRangeAsync(
        string jamlContent, int startBlock, int endBlock)
        => RunViaOrchestrator(jamlContent, new MotelySearchRequest
        {
            ThreadCount = Environment.ProcessorCount,
            BatchCharCount = ProcessBlockRunner.BatchCharCount,
            StartBatch = startBlock,
            EndBatch = endBlock,
        }, startBlock);

    public static BlockSearchResultDto RunListSearchAsync(
        string jamlContent, string[] seeds)
        => RunViaOrchestrator(jamlContent, new MotelySearchRequest
        {
            ThreadCount = Environment.ProcessorCount,
            BatchCharCount = ProcessBlockRunner.BatchCharCount,
            Seeds = seeds,
        });

    public static BlockSearchResultDto RunKeywordsSearchAsync(
        string jamlContent, string[] keywords, string? padding)
        => RunViaOrchestrator(jamlContent, new MotelySearchRequest
        {
            ThreadCount = Environment.ProcessorCount,
            BatchCharCount = ProcessBlockRunner.BatchCharCount,
            Keywords = keywords,
            Padding = string.IsNullOrEmpty(padding) ? null : padding,
        });

    public static BlockSearchResultDto RunRandomSearchAsync(
        string jamlContent, int count)
        => RunViaOrchestrator(jamlContent, new MotelySearchRequest
        {
            ThreadCount = Environment.ProcessorCount,
            BatchCharCount = ProcessBlockRunner.BatchCharCount,
            RandomSeeds = count > 0 ? count : 1000,
        });

    public static BlockSearchResultDto RunPalindromeSearchAsync(string jamlContent)
        => RunViaOrchestrator(jamlContent, new MotelySearchRequest
        {
            ThreadCount = Environment.ProcessorCount,
            BatchCharCount = ProcessBlockRunner.BatchCharCount,
            Palindrome = true,
        });

    // ── Private helpers ──

    private static BlockSearchResultDto RunViaOrchestrator(
        string jamlContent, MotelySearchRequest request, int blockId = 0)
    {
        var seeds = new List<string>();
        int highestScore = 0;

        var (status, seedsFound, _) = MotelySearchOrchestrator.RunSearch(jamlContent, request,
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
