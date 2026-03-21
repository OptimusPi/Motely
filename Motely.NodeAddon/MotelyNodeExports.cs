using System.Runtime.Intrinsics;
using Microsoft.JavaScript.NodeApi;
using Motely;
using Motely.Analysis;
using Motely.Executors;
using Motely.Filters;

namespace Motely.NodeAddon;

/// <summary>Node-API boundary. One-liners only — all logic in <see cref="MotelySearchOrchestrator"/>.</summary>
[JSExport]
public static class MotelyNodeExports
{
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

    public static SeedAnalysisDto AnalyzeSeed(string seed, string deck, string stake)
    {
        try { return MotelySeedAnalyzer.AnalyzeToDto(seed, deck, stake); }
        catch (Exception ex)
        {
            return new SeedAnalysisDto { Seed = seed, Deck = deck, Stake = stake, Error = ex.Message };
        }
    }

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

    public static Task<BlockSearchResultDto> ProcessBlockAsync(string jamlContent, int blockId) =>
        MotelySearchOrchestrator.ProcessBlockAsync(jamlContent, blockId);

    public static BlockSearchResultDto RunSequentialRangeAsync(string jamlContent, int startBlock, int endBlock) =>
        MotelySearchOrchestrator.RunSearchCollecting(jamlContent, new MotelySearchRequest
        {
            ThreadCount = Environment.ProcessorCount,
            BatchCharCount = ProcessBlockRunner.BatchCharCount,
            StartBatch = startBlock,
            EndBatch = endBlock,
        }, startBlock);

    public static BlockSearchResultDto RunListSearchAsync(string jamlContent, string[] seeds) =>
        MotelySearchOrchestrator.RunSearchCollecting(jamlContent, new MotelySearchRequest
        {
            ThreadCount = Environment.ProcessorCount,
            BatchCharCount = ProcessBlockRunner.BatchCharCount,
            Seeds = seeds,
        });

    public static BlockSearchResultDto RunKeywordsSearchAsync(string jamlContent, string[] keywords, string? padding) =>
        MotelySearchOrchestrator.RunSearchCollecting(jamlContent, new MotelySearchRequest
        {
            ThreadCount = Environment.ProcessorCount,
            BatchCharCount = ProcessBlockRunner.BatchCharCount,
            Keywords = keywords,
            Padding = string.IsNullOrEmpty(padding) ? null : padding,
        });

    public static BlockSearchResultDto RunRandomSearchAsync(string jamlContent, int count) =>
        MotelySearchOrchestrator.RunSearchCollecting(jamlContent, new MotelySearchRequest
        {
            ThreadCount = Environment.ProcessorCount,
            BatchCharCount = ProcessBlockRunner.BatchCharCount,
            RandomSeeds = count > 0 ? count : 1000,
        });

    public static BlockSearchResultDto RunPalindromeSearchAsync(string jamlContent) =>
        MotelySearchOrchestrator.RunSearchCollecting(jamlContent, new MotelySearchRequest
        {
            ThreadCount = Environment.ProcessorCount,
            BatchCharCount = ProcessBlockRunner.BatchCharCount,
            Palindrome = true,
        });
}
