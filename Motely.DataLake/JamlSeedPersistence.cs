using System.Collections.Generic;
using Motely.Filters;

namespace Motely.DataLake;

/// <summary>
/// Persist a search's finds: cutoff, then lake, then <c>seeds:</c> save-back.
/// One gate. Auto/fixed/off decide what is kept — UI and disk see the same rows.
/// Every find is scored (must-only is score 0).
/// </summary>
public sealed class JamlSeedPersistence : System.IDisposable
{
    private readonly MotelyScoreCutoff _cutoff;
    private readonly SeedLakeSink _lake;
    private readonly MotelyTopSeedSink.Collector _scoredCollector;

    /// <summary>
    /// Worker-thread hook after a seed passes the cutoff and is persisted. Live UI (console, TUI row).
    /// </summary>
    public System.Action<MotelyScoredSeedResult>? OnScoredAccepted { get; set; }

    /// <param name="lakeRoot">
    /// The seed-lake root (<c>--results-path</c> / <c>MOTELY_DATALAKE_PATH</c> / default "Seeds").
    /// </param>
    /// <param name="filterId">The JAML filter id; names its lake file.</param>
    /// <param name="cutoff">Emit gate for lake, UI, and save-back. Default: off.</param>
    /// <param name="saveLimit">
    /// Max seeds saved back into the JAML <c>seeds:</c> block. Defaults to unbounded.
    /// </param>
    /// <param name="tallyLabels">The filter's tally column names, recorded in the lake so its rows stay readable.</param>
    public JamlSeedPersistence(
        string? lakeRoot,
        string filterId,
        MotelyScoreCutoff? cutoff = null,
        int saveLimit = int.MaxValue,
        IReadOnlyList<string>? tallyLabels = null
    )
    {
        _cutoff = cutoff ?? MotelyScoreCutoff.Off();
        _lake = new SeedLakeSink(lakeRoot, filterId, tallyLabels);
        _scoredCollector = new MotelyTopSeedSink.Collector(saveLimit);
    }

    /// <summary>The cutoff gate this instance applies (for front-ends that also gate their UI).</summary>
    public MotelyScoreCutoff Cutoff => _cutoff;

    /// <summary>
    /// Scored-result callback: cutoff, then lake + save-back + <see cref="OnScoredAccepted"/>.
    /// </summary>
    public bool OnScored(in MotelyScoredSeedResult tally)
    {
        if (!_cutoff.ShouldEmit(tally.Score))
            return false;

        _lake.OnScored(in tally);
        _scoredCollector.Consider(tally.Seed, tally.Score);
        OnScoredAccepted?.Invoke(tally);
        return true;
    }

    /// <summary>The seeds that would be written back, best-first (a snapshot; safe to call anytime).</summary>
    public IReadOnlyList<string> SeedsToSave() => _scoredCollector.GetSeeds();

    /// <summary>
    /// Merge the collected seeds into the JAML <c>seeds:</c> block on disk. Safe to call on
    /// completion, on stop, or both — the block is idempotent (seeds are de-duped). A no-op when
    /// nothing was found. Returns false with <paramref name="error"/> on write/validation failure.
    /// </summary>
    public bool SaveBack(string jamlPath, out string? error) =>
        MotelyJamlFile.TrySaveSeeds(jamlPath, SeedsToSave(), out error);

    /// <summary>Push buffered finds to the lake. Search batch boundary.</summary>
    public void Flush() => _lake.Flush();

    public void Dispose() => _lake.Dispose();
}
