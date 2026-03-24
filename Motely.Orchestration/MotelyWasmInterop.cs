#if BROWSER
using Bootsharp;
using Motely.Filters;

namespace Motely.Executors;

public static partial class MotelyWasm
{
    [JSEvent]
    public static partial void OnProgress(long searched, long found, long elapsedMs);

    [JSEvent]
    public static partial void OnResult(string seed, int score);

    [JSEvent]
    public static partial void OnComplete(string status, int seedsFound, int highestScore);

    [JSInvokable]
    public static string? ValidateJaml(string jamlContent)
    {
        if (!JamlConfigLoader.TryLoad(jamlContent, out _, out var error))
            return error ?? "Invalid JAML.";
        return null;
    }

    private static string Run(MotelySearchRequest request, string jamlContent)
    {
        var (status, seedsFound, highestScore) = MotelySearchOrchestrator.RunSearch(
            jamlContent, request,
            onProgress: (searched, found, elapsed) => OnProgress(searched, found, elapsed),
            onResult: (seed, score) => OnResult(seed, score)
        );
        OnComplete(status, seedsFound, highestScore);
        return $"{status}|{seedsFound}|{highestScore}";
    }

    [JSInvokable]
    public static string RunSearch(
        string jamlContent, int threadCount, int batchCharCount,
        int startBatch, int endBatch)
        => Run(new MotelySearchRequest
        {
            ThreadCount = threadCount,
            BatchCharCount = batchCharCount,
            StartBatch = startBatch,
            EndBatch = endBatch
        }, jamlContent);

    [JSInvokable]
    public static string RunKeywordSearch(
        string jamlContent, int threadCount, string keyword,
        string? padding = null)
        => Run(new MotelySearchRequest
        {
            ThreadCount = threadCount,
            BatchCharCount = 1,
            Keywords = [keyword],
            Padding = padding
        }, jamlContent);

    [JSInvokable]
    public static string RunSeedListSearch(
        string jamlContent, int threadCount, string[] seeds)
        => Run(new MotelySearchRequest
        {
            ThreadCount = threadCount,
            BatchCharCount = 1,
            Seeds = seeds
        }, jamlContent);

    [JSInvokable]
    public static string RunRandomSearch(
        string jamlContent, int threadCount, int count)
        => Run(new MotelySearchRequest
        {
            ThreadCount = threadCount,
            BatchCharCount = 1,
            RandomSeeds = count
        }, jamlContent);

    [JSInvokable]
    public static string RunPalindromeSearch(
        string jamlContent, int threadCount)
        => Run(new MotelySearchRequest
        {
            ThreadCount = threadCount,
            BatchCharCount = 1,
            Palindrome = true
        }, jamlContent);
}
#endif
