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

    [JSInvokable]
    public static string? ValidateJaml(string jamlContent)
    {
        if (!JamlConfigLoader.TryLoad(jamlContent, out _, out var error))
            return error ?? "Invalid JAML.";
        return null;
    }

    [JSInvokable]
    public static string RunSearch(
        string jamlContent,
        int threadCount,
        int batchCharCount,
        int startBatch,
        int endBatch)
    {
        var request = new MotelySearchRequest
        {
            ThreadCount = threadCount,
            BatchCharCount = batchCharCount,
            StartBatch = startBatch,
            EndBatch = endBatch
        };

        var (status, seedsFound, highestScore) = MotelySearchOrchestrator.RunSearch(
            jamlContent, request,
            onProgress: (searched, found, elapsed) => OnProgress(searched, found, elapsed),
            onResult: (seed, score) => OnResult(seed, score)
        );

        return $"{status}|{seedsFound}|{highestScore}";
    }

    [JSInvokable]
    public static string RunKeywordSearch(
        string jamlContent,
        int threadCount,
        string keyword,
        string? padding = null)
    {
        var request = new MotelySearchRequest
        {
            ThreadCount = threadCount,
            BatchCharCount = 1,
            Keywords = [keyword],
            Padding = padding
        };

        var (status, seedsFound, highestScore) = MotelySearchOrchestrator.RunSearch(
            jamlContent, request,
            onProgress: (searched, found, elapsed) => OnProgress(searched, found, elapsed),
            onResult: (seed, score) => OnResult(seed, score)
        );

        return $"{status}|{seedsFound}|{highestScore}";
    }

    [JSInvokable]
    public static string RunSeedListSearch(
        string jamlContent,
        int threadCount,
        string[] seeds)
    {
        var request = new MotelySearchRequest
        {
            ThreadCount = threadCount,
            BatchCharCount = 1,
            Seeds = seeds
        };

        var (status, seedsFound, highestScore) = MotelySearchOrchestrator.RunSearch(
            jamlContent, request,
            onProgress: (searched, found, elapsed) => OnProgress(searched, found, elapsed),
            onResult: (seed, score) => OnResult(seed, score)
        );

        return $"{status}|{seedsFound}|{highestScore}";
    }

    [JSInvokable]
    public static string RunRandomSearch(
        string jamlContent,
        int threadCount,
        int count)
    {
        var request = new MotelySearchRequest
        {
            ThreadCount = threadCount,
            BatchCharCount = 1,
            RandomSeeds = count
        };

        var (status, seedsFound, highestScore) = MotelySearchOrchestrator.RunSearch(
            jamlContent, request,
            onProgress: (searched, found, elapsed) => OnProgress(searched, found, elapsed),
            onResult: (seed, score) => OnResult(seed, score)
        );

        return $"{status}|{seedsFound}|{highestScore}";
    }

    [JSInvokable]
    public static string RunPalindromeSearch(
        string jamlContent,
        int threadCount)
    {
        var request = new MotelySearchRequest
        {
            ThreadCount = threadCount,
            BatchCharCount = 1,
            Palindrome = true
        };

        var (status, seedsFound, highestScore) = MotelySearchOrchestrator.RunSearch(
            jamlContent, request,
            onProgress: (searched, found, elapsed) => OnProgress(searched, found, elapsed),
            onResult: (seed, score) => OnResult(seed, score)
        );

        return $"{status}|{seedsFound}|{highestScore}";
    }
}
#endif
