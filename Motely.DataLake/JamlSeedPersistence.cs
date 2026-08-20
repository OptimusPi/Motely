using System.Collections.Generic;
using Motely.Filters;

namespace Motely.DataLake;

/// <summary>
/// One place that guarantees a JAML search never loses its finds. It bundles the three things every
/// front-end previously wired up by hand (and the TUI mostly forgot to):
///
///   1. the <see cref="MotelyScoreCutoff"/> gate (auto / fixed / off), applied before anything is kept;
///   2. immediate durability — every surviving seed is written to the per-filter seed lake as it is
///      found, so a crash or a stop mid-sweep never discards results already on screen;
///   3. save-back — on completion or stop, the top seeds are merged into the JAML <c>seeds:</c>
///      block on disk (bounded top-N by score for scored filters; every match for match-only filters).
///
/// Callers register <see cref="OnScored"/> / <see cref="OnSeed"/> as the search's result callbacks
/// (behind the cutoff gate, which <see cref="OnScored"/> applies for them), then call
/// <see cref="SaveBack"/> when the run ends and <see cref="Dispose"/> to flush the lake. Thread-safe:
/// result callbacks fire on every engine worker thread with no serialization.
/// </summary>
public sealed class JamlSeedPersistence : System.IDisposable
{
    private readonly MotelyScoreCutoff _cutoff;
    private readonly SeedLakeSink _lake;
    private readonly MotelyTopSeedSink.Collector? _scoredCollector;
    private readonly List<string>? _matchSeeds;
    private readonly HashSet<string>? _matchSeen;
    private readonly object _matchGate = new();

    /// <summary>
    /// A raw hook invoked for every seed that passes the cutoff, on the worker thread, after it has
    /// been persisted. Front-ends use this to update their live UI (console line, TUI table row).
    /// </summary>
    public System.Action<MotelyScoredSeedResult>? OnScoredAccepted { get; set; }

    /// <summary>As <see cref="OnScoredAccepted"/> but for match-only (unscored) filters.</summary>
    public System.Action<string>? OnSeedAccepted { get; set; }

    /// <param name="lakeRoot">
    /// The seed-lake root (<c>--results-path</c> / <c>MOTELY_DATALAKE_PATH</c> / default "Seeds").
    /// </param>
    /// <param name="filterId">The JAML filter id; names its lake file.</param>
    /// <param name="hasStructuredScores">
    /// True when the filter has <c>should:</c> clauses (scored). False routes through the
    /// match-only path (top-N is "every match" and the cutoff never applies).
    /// </param>
    /// <param name="cutoff">The emit gate. Defaults to <see cref="MotelyScoreCutoff.Off"/>.</param>
    /// <param name="saveLimit">
    /// Max seeds saved back into the JAML <c>seeds:</c> block for scored filters. Defaults to
    /// unbounded (<see cref="int.MaxValue"/>), matching the CLI.
    /// </param>
    public JamlSeedPersistence(
        string? lakeRoot,
        string filterId,
        bool hasStructuredScores,
        MotelyScoreCutoff? cutoff = null,
        int saveLimit = int.MaxValue
    )
    {
        _cutoff = cutoff ?? MotelyScoreCutoff.Off();
        _lake = new SeedLakeSink(lakeRoot, filterId);

        if (hasStructuredScores)
        {
            _scoredCollector = new MotelyTopSeedSink.Collector(saveLimit);
        }
        else
        {
            _matchSeeds = new List<string>();
            _matchSeen = new HashSet<string>(System.StringComparer.Ordinal);
        }
    }

    /// <summary>The cutoff gate this instance applies (for front-ends that also gate their UI).</summary>
    public MotelyScoreCutoff Cutoff => _cutoff;

    /// <summary>
    /// Scored-result callback: apply the cutoff, and if the seed passes, persist it to the lake,
    /// remember it for save-back, and notify <see cref="OnScoredAccepted"/>. Returns whether the
    /// seed was accepted so a caller can short-circuit its own UI work.
    /// </summary>
    public bool OnScored(in MotelyScoredSeedResult tally)
    {
        if (!_cutoff.ShouldEmit(tally.Score))
            return false;

        _lake.OnScored(in tally);
        _scoredCollector?.Consider(tally.Seed, tally.Score);
        OnScoredAccepted?.Invoke(tally);
        return true;
    }

    /// <summary>
    /// Match-only callback: persist every match to the lake and remember it for save-back. No
    /// cutoff (match-only filters have no score). Duplicate seeds are de-duped for save-back but
    /// still forwarded to the lake (which dedupes on its primary key).
    /// </summary>
    public void OnSeed(string seed)
    {
        if (string.IsNullOrEmpty(seed))
            return;

        // The engine serializes nothing; a bare List/HashSet loses finds under concurrent Add.
        lock (_matchGate)
        {
            _lake.OnSeed(seed);
            if (_matchSeeds is not null && _matchSeen is not null && _matchSeen.Add(seed))
                _matchSeeds.Add(seed);
        }

        OnSeedAccepted?.Invoke(seed);
    }

    /// <summary>The seeds that would be written back, best-first (a snapshot; safe to call anytime).</summary>
    public IReadOnlyList<string> SeedsToSave()
    {
        if (_scoredCollector is not null)
            return _scoredCollector.GetSeeds();

        lock (_matchGate)
            return _matchSeeds is null ? System.Array.Empty<string>() : _matchSeeds.ToArray();
    }

    /// <summary>
    /// Merge the collected seeds into the JAML <c>seeds:</c> block on disk. Safe to call on
    /// completion, on stop, or both — the block is idempotent (seeds are de-duped). A no-op when
    /// nothing was found. Returns false with <paramref name="error"/> on write/validation failure.
    /// </summary>
    public bool SaveBack(string jamlPath, out string? error) =>
        MotelyJamlFile.TrySaveSeeds(jamlPath, SeedsToSave(), out error);

    public void Dispose() => _lake.Dispose();
}
