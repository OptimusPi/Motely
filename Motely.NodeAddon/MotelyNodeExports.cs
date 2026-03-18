using Microsoft.JavaScript.NodeApi;
using Motely;
using Motely.Analysis;
using Motely.Executors;

namespace Motely.NodeAddon;

/// <summary>
/// Node AOT [JSExport] surface. Thin wrappers over MotelyExports.
/// node-api-dotnet marshals C# types to JS objects at compile time. No JSON.
/// </summary>
[JSExport]
public static class MotelyNodeExports
{
    [JSExport]
    public static string GetVersion() => MotelyExports.GetVersion(typeof(MotelyNodeExports).Assembly);

    [JSExport]
    public static bool IsSimdEnabled() => MotelyExports.IsSimdEnabled();

    [JSExport]
    public static int GetProcessorCount() => MotelyExports.GetProcessorCount();

    [JSExport]
    public static SeedAnalysisDto AnalyzeSeed(string seed, string deck, string stake) =>
        MotelyExports.AnalyzeSeed(seed, deck, stake);

    [JSExport]
    public static bool ValidateJaml(string jamlContent) => MotelyExports.ValidateJaml(jamlContent);

    [JSExport]
    public static string ValidateJamlWithError(string jamlContent) => MotelyExports.ValidateJamlWithError(jamlContent);

    // ── Search ───────────────────────────────────────────────────────────

    [JSExport]
    public static Task<BlockSearchResultDto> RunKeywordSearchAsync(
        string jamlContent, string keyword, string? padding = null)
    {
        return Task.Run(() =>
        {
            var (_, seeds, highestScore) = MotelyExports.RunSearch(jamlContent, new MotelySearchRequest
            {
                ThreadCount = Environment.ProcessorCount,
                BatchCharCount = 4,
                Keywords = [keyword.Trim().ToUpperInvariant()],
                Padding = padding?.Trim().ToUpperInvariant(),
            });
            return new BlockSearchResultDto { BlockId = 0, SeedsFound = seeds.Count, HighestScore = highestScore, Seeds = seeds.ToArray() };
        });
    }

    [JSExport]
    public static Task<BlockSearchResultDto> RunKeywordsSearchAsync(
        string jamlContent, string[] keywords, string? padding = null)
    {
        if (keywords == null || keywords.Length == 0)
            throw new ArgumentException("At least one keyword is required.", nameof(keywords));

        return Task.Run(() =>
        {
            var (_, seeds, highestScore) = MotelyExports.RunSearch(jamlContent, new MotelySearchRequest
            {
                ThreadCount = Environment.ProcessorCount,
                BatchCharCount = 4,
                Keywords = keywords.Select(k => k.Trim().ToUpperInvariant()).ToArray(),
                Padding = padding?.Trim().ToUpperInvariant(),
            });
            return new BlockSearchResultDto { BlockId = 0, SeedsFound = seeds.Count, HighestScore = highestScore, Seeds = seeds.ToArray() };
        });
    }

    [JSExport]
    public static Task<BlockSearchResultDto> RunRandomSearchAsync(string jamlContent, int count)
    {
        return Task.Run(() =>
        {
            var (_, seeds, highestScore) = MotelyExports.RunSearch(jamlContent, new MotelySearchRequest
            {
                ThreadCount = Environment.ProcessorCount,
                BatchCharCount = 4,
                RandomSeeds = count,
            });
            return new BlockSearchResultDto { BlockId = 0, SeedsFound = seeds.Count, HighestScore = highestScore, Seeds = seeds.ToArray() };
        });
    }

    [JSExport]
    public static Task<BlockSearchResultDto> RunPalindromeSearchAsync(string jamlContent)
    {
        return Task.Run(() =>
        {
            var (_, seeds, highestScore) = MotelyExports.RunSearch(jamlContent, new MotelySearchRequest
            {
                ThreadCount = Environment.ProcessorCount,
                BatchCharCount = 4,
                Palindrome = true,
            });
            return new BlockSearchResultDto { BlockId = 0, SeedsFound = seeds.Count, HighestScore = highestScore, Seeds = seeds.ToArray() };
        });
    }

    [JSExport]
    public static Task<BlockSearchResultDto> RunListSearchAsync(string jamlContent, string[] seeds)
    {
        if (seeds == null || seeds.Length == 0)
            throw new ArgumentException("At least one seed is required.", nameof(seeds));

        return Task.Run(() =>
        {
            var (_, foundSeeds, highestScore) = MotelyExports.RunSearch(jamlContent, new MotelySearchRequest
            {
                ThreadCount = Environment.ProcessorCount,
                BatchCharCount = 4,
                Seeds = seeds.Select(s => s.Trim().ToUpperInvariant()).ToArray(),
            });
            return new BlockSearchResultDto { BlockId = 0, SeedsFound = foundSeeds.Count, HighestScore = highestScore, Seeds = foundSeeds.ToArray() };
        });
    }

    [JSExport]
    public static async Task<BlockSearchResultDto> RunSequentialRangeAsync(
        string jamlContent, int startBlockId, int endBlockId)
    {
        int maxBlocks = ProcessBlockRunner.TotalBlocks;
        if (startBlockId < 0 || endBlockId > maxBlocks || startBlockId >= endBlockId)
            throw new ArgumentOutOfRangeException(
                nameof(startBlockId),
                $"Block range must be 0..{maxBlocks} with start < end.");

        var allSeeds = new List<string>();
        int highestScore = 0;

        for (int blockId = startBlockId; blockId < endBlockId; blockId++)
        {
            var result = await ProcessBlockRunner
                .ProcessBlockAsync(jamlContent, blockId)
                .ConfigureAwait(false);

            if (result == null)
                throw new InvalidOperationException($"Invalid JAML or block {blockId} out of range.");

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

    [JSExport]
    public static async Task<BlockSearchResultDto> ProcessBlockAsync(string jamlContent, int blockId)
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
}
