#if NODE
using Microsoft.JavaScript.NodeApi;
using Motely.Filters;

namespace Motely.Executors;

[JSExport]
public static partial class MotelyNode
{
    public static string? ValidateJaml(string jamlContent)
    {
        if (!JamlConfigLoader.TryLoad(jamlContent, out _, out var error))
            return error ?? "Invalid JAML.";
        return null;
    }

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
            onResult: (seed, score) => Console.WriteLine($"SEED:{seed}|{score}")
        );

        return $"{status}|{seedsFound}|{highestScore}";
    }

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
            onResult: (seed, score) => Console.WriteLine($"SEED:{seed}|{score}")
        );

        return $"{status}|{seedsFound}|{highestScore}";
    }

    public static string RunKeywordSearch(
        string jamlContent,
        int threadCount,
        string[] keywords,
        string? padding = null)
    {
        var request = new MotelySearchRequest
        {
            ThreadCount = threadCount,
            BatchCharCount = 1,
            Keywords = keywords,
            Padding = padding
        };

        var (status, seedsFound, highestScore) = MotelySearchOrchestrator.RunSearch(
            jamlContent, request,
            onResult: (seed, score) => Console.WriteLine($"SEED:{seed}|{score}")
        );

        return $"{status}|{seedsFound}|{highestScore}";
    }
}
#endif
