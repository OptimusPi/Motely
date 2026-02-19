using Motely.Filters;

namespace Motely.Executors;

public enum ScoreCutoffMode
{
    None = 0,
    Manual = 1,
    AutoBest = 2,
    AutoSmart = 3,
}

public sealed class JsonSearchParams
{
    public int Threads { get; set; } = 1;
    public int BatchSize { get; set; } = 4;
    public ulong StartBatch { get; set; }
    public ulong EndBatch { get; set; }
    public string? SpecificSeed { get; set; }
    public int RandomSeeds { get; set; }
    public bool PalindromeSeeds { get; set; }
    public string? Deck { get; set; }
    public string? Stake { get; set; }
    public int Cutoff { get; set; }
    public ScoreCutoffMode CutoffMode { get; set; }
    public bool Quiet { get; set; }
    public bool NoFancy { get; set; }
    public string? OutputDbPath { get; set; }
    public CancellationToken CancellationToken { get; set; }
    public Action<MotelySeedScoreTally>? ResultCallback { get; set; }
}

public interface IMotelySearchContext : IDisposable
{
    string SearchId { get; }
    string FilterId { get; }
    bool IsCompleted { get; }
    bool IsSequentialBatchSearch { get; }
    long BatchIndex { get; }
    long CompletedBatchCount { get; }
    TimeSpan ElapsedTime { get; }
    long TotalSeedsSearched { get; }
    long MatchingSeeds { get; }
    long FilteredSeeds { get; }
    int ResultCount { get; }
    IReadOnlyList<string> ColumnNames { get; }
    List<MotelySearchResultRow> GetResults(int offset, int limit);
    List<MotelySearchResultRow> GetTopResults(int limit = 1000);
    void Start(CancellationToken cancellationToken = default);
    void AwaitCompletion();
    Task WaitForCompletionAsync(CancellationToken cancellationToken = default);
    void Cancel();
    void ForceProgressReport();
}

public sealed class MotelySearchResultRow
{
    public string Seed { get; set; } = string.Empty;
    public int Score { get; set; }
    public List<string>? Tallies { get; set; }
}
