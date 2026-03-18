using System.Runtime.Intrinsics;
using Motely.Analysis;
using Motely.Filters;

namespace Motely.Executors;

/// <summary>
/// Shared export logic. Both BrowserWasm and NodeAddon call these.
/// The export files are thin wrappers that add the platform-specific [JSExport] attribute.
/// </summary>
public static class MotelyExports
{
    private static string? _cachedVersion;

    public static string GetVersion(System.Reflection.Assembly assembly) =>
        _cachedVersion ??= MotelyBuildVersion.For(assembly);

    public static bool IsSimdEnabled() => Vector128.IsHardwareAccelerated;

    public static int GetProcessorCount() => Environment.ProcessorCount;

    public static SeedAnalysisDto AnalyzeSeed(string seed, string deck, string stake) =>
        MotelySeedAnalyzer.AnalyzeToDto(seed, deck, stake);

    public static bool ValidateJaml(string jamlContent) =>
        JamlConfigLoader.TryLoad(jamlContent, out _, out _);

    public static string ValidateJamlWithError(string jamlContent)
    {
        if (JamlConfigLoader.TryLoad(jamlContent, out _, out var error))
            return "";
        return error ?? "Unknown validation error";
    }

    public static (string Status, List<string> Seeds, int HighestScore) RunSearch(
        string jamlContent, MotelySearchRequest request,
        Action<long, long, long>? onProgress = null,
        Action<string, int>? onResult = null)
    {
        if (!JamlConfigLoader.TryLoad(jamlContent, out var config, out var error) || config == null)
            throw new InvalidOperationException(error ?? "Invalid JAML.");

        var (plan, _, prepareError) = MotelySearchOrchestrator.PrepareSearch(config, request);
        if (prepareError != null || plan == null)
            throw new InvalidOperationException(prepareError ?? "Search could not be prepared.");

        var seeds = new List<string>();
        int highestScore = 0;
        var settings = plan.Settings;

        if (onProgress != null)
        {
            settings = settings.WithProgressCallback(prog =>
                onProgress(prog.SeedsSearched, prog.MatchingSeeds, (long)prog.ElapsedTime.TotalMilliseconds));
        }

        if (plan.ShouldClauseCount > 0)
        {
            settings = settings.WithScoredResultCallback(tally =>
            {
                seeds.Add(tally.Seed);
                if (tally.Score > highestScore) highestScore = tally.Score;
                onResult?.Invoke(tally.Seed, tally.Score);
            });
        }
        else
        {
            settings = settings.WithSeedMatchCallback(seed =>
            {
                seeds.Add(seed);
                onResult?.Invoke(seed, 0);
            });
        }

        using var search = settings.CreateSearch();
        search.Start(CancellationToken.None);

        return (search.IsCompleted ? "ok" : "cancelled", seeds, highestScore);
    }
}
