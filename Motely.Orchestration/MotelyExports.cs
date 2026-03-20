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

    /// <summary>
    /// Streaming search — pure source→sink via callbacks. No seed accumulation.
    /// The caller decides what to do with each seed (fire to JS, write to DuckDB, etc).
    /// </summary>
    public static (string Status, int SeedsFound, int HighestScore) RunSearch(
        string jamlContent, MotelySearchRequest request,
        Action<long, long, long>? onProgress = null,
        Action<string, int>? onResult = null,
        CancellationToken cancellationToken = default)
    {
        if (!JamlConfigLoader.TryLoad(jamlContent, out var config, out var error) || config == null)
            throw new InvalidOperationException(error ?? "Invalid JAML.");

        var (plan, _, prepareError) = MotelySearchOrchestrator.PrepareSearch(config, request);
        if (prepareError != null || plan == null)
            throw new InvalidOperationException(prepareError ?? "Search could not be prepared.");

        int seedsFound = 0;
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
                Interlocked.Increment(ref seedsFound);
                if (tally.Score > highestScore) highestScore = tally.Score;
                onResult?.Invoke(tally.Seed, tally.Score);
            });
        }
        else
        {
            settings = settings.WithSeedMatchCallback(seed =>
            {
                Interlocked.Increment(ref seedsFound);
                onResult?.Invoke(seed, 0);
            });
        }

        using var search = settings.CreateSearch();
        search.Start(cancellationToken);

        return (cancellationToken.IsCancellationRequested ? "cancelled" :
                search.IsCompleted ? "ok" : "cancelled",
                seedsFound, highestScore);
    }
}
