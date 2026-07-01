namespace Motely.Wasm;

/// <summary>
/// Interop surface exported to JavaScript. Kept small and decoupled from internal engine
/// types (see <see cref="MotelySearchProgress"/>/<see cref="MotelySeedMatch"/>) so the interop
/// boundary doesn't leak Motely's internal representations to the frontend.
/// </summary>
public interface IMotelyBackend
{
    event Action<MotelySearchProgress>? OnProgress;
    event Action<string>? OnSeedMatch;
    event Action<MotelySeedMatch>? OnScoredResult;

    /// <summary>Parses <paramref name="jamlYaml"/> and runs it to completion (or cancellation).</summary>
    Task RunSearch(string jamlYaml);

    /// <summary>Cancels the in-progress search started by <see cref="RunSearch"/>, if any.</summary>
    void CancelSearch();
}

public readonly record struct MotelySearchProgress(
    double PercentComplete,
    long SeedsSearched,
    long MatchingSeeds,
    double SeedsPerMillisecond
);

public readonly record struct MotelySeedMatch(string Seed, int Score);
