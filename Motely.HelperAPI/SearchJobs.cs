using System.Collections.Concurrent;
using Motely.Filters;
using Motely.Filters.Jaml;

namespace Motely.HelperAPI;

/// <summary>
/// In-process multi-search host: the "launch several JAML filters at once" layer the CLI and
/// TUI never had. Each job owns one engine search (its own thread pool via
/// <see cref="IMotelySearchSettings.WithThreadCount"/>), a live stats snapshot for polling,
/// and a capped ring of the best-looking results. The dashboard in wwwroot drives this through
/// the /api/search endpoints registered in <see cref="HelperApiHost"/>.
/// </summary>
public static class SearchJobManager
{
    private static readonly ConcurrentDictionary<string, SearchJob> Jobs = new();

    /// <summary>Directory holding the *.jaml filters. Resolved once by walking up from the
    /// app base directory (bin/Release/net10.0 → repo root) until a JamlFilters/ dir appears;
    /// MOTELY_FILTERS_DIR wins when set.</summary>
    public static string FiltersDir { get; } = ResolveFiltersDir();

    private static string ResolveFiltersDir()
    {
        string? env = Environment.GetEnvironmentVariable("MOTELY_FILTERS_DIR");
        if (!string.IsNullOrWhiteSpace(env) && Directory.Exists(env))
            return Path.GetFullPath(env);

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(dir.FullName, "JamlFilters");
            if (Directory.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }
        // Last resort: current working directory.
        return Path.GetFullPath("JamlFilters");
    }

    public static List<FilterDto> ListFilters()
    {
        var list = new List<FilterDto>();
        if (!Directory.Exists(FiltersDir))
            return list;
        foreach (string path in Directory.EnumerateFiles(FiltersDir, "*.jaml"))
        {
            var fi = new FileInfo(path);
            list.Add(new FilterDto(Path.GetFileNameWithoutExtension(fi.Name), fi.Length));
        }
        list.Sort(static (a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return list;
    }

    public static string? ReadFilterJaml(string name)
    {
        // Same convention as the CLI: a bare name resolves under JamlFilters/ with .jaml.
        string path = Path.GetFullPath(
            Path.HasExtension(name) ? name : Path.Combine(FiltersDir, name + ".jaml"));
        // Never serve files outside the filters dir through the bare-name path.
        if (!Path.IsPathRooted(name) && !path.StartsWith(FiltersDir, StringComparison.Ordinal))
            return null;
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    public static string Start(StartSearchRequest req)
    {
        string jaml = req.Jaml ?? ReadFilterJaml(req.Filter ?? "")
            ?? throw new ArgumentException($"Filter '{req.Filter}' not found in {FiltersDir}.");

        if (!JamlConfigLoader.TryLoad(jaml, out var config, out string? error))
            throw new ArgumentException($"JAML parse failed: {error}");

        int running = Jobs.Values.Count(j => j.Status == "running") + 1;
        int threads = req.Threads is > 0
            ? req.Threads.Value
            : Math.Max(1, Environment.ProcessorCount / running);

        var job = new SearchJob(
            id: Guid.NewGuid().ToString("N")[..8],
            filter: req.Filter ?? "(custom jaml)",
            name: config!.Name,
            threads: threads,
            batchCharCount: req.BatchCharCount is >= 1 and <= 7 ? req.BatchCharCount.Value : 4,
            startBatch: req.StartBatch ?? 0,
            endBatch: req.EndBatch ?? long.MaxValue,
            stopAfter: req.StopAfter ?? 0,
            config: config);

        Jobs[job.Id] = job;
        Task.Run(job.Run);
        return job.Id;
    }

    public static List<SearchJobDto> ListJobs() =>
        [.. Jobs.Values.OrderByDescending(j => j.StartedAt).Select(j => j.Snapshot())];

    public static JobResultsDto? GetResults(string id) =>
        Jobs.TryGetValue(id, out var job) ? job.ResultsSnapshot() : null;

    public static bool Stop(string id)
    {
        if (!Jobs.TryGetValue(id, out var job))
            return false;
        job.Stop();
        return true;
    }
}

/// <summary>One running (or finished) filter search.</summary>
public sealed class SearchJob : IDisposable
{
    private const int MaxKeptResults = 500;

    private readonly JamlConfig _config;
    private readonly CancellationTokenSource _cts = new();
    private readonly object _gate = new();
    private readonly List<FoundSeedDto> _results = [];
    private IMotelySearch? _search;

    // Live stats, written by the engine's progress callback, read by Snapshot().
    private long _seedsSearched;
    private long _matchingSeeds;
    private long _elapsedMs;
    private long? _etaMs;
    private double _seedsPerMs;
    private double _percent;
    private long _totalMatches;

    public string Id { get; }
    public string Filter { get; }
    public string? Name { get; }
    public int Threads { get; }
    public int BatchCharCount { get; }
    public long StartBatch { get; }
    public long EndBatch { get; }
    public long StopAfter { get; }
    public string StartedAt { get; } = DateTime.UtcNow.ToString("O");
    public string Status { get; private set; } = "running";
    public string? Error { get; private set; }
    public string[] TallyLabels { get; private set; } = [];

    public SearchJob(
        string id, string filter, string? name, int threads, int batchCharCount,
        long startBatch, long endBatch, long stopAfter, JamlConfig config)
    {
        Id = id;
        Filter = filter;
        Name = name;
        Threads = threads;
        BatchCharCount = batchCharCount;
        StartBatch = startBatch;
        EndBatch = endBatch;
        StopAfter = stopAfter;
        _config = config;
    }

    public void Run()
    {
        try
        {
            // CreateSettings is the public entry (JamlSearchPlan.Settings is internal to the
            // engine assembly); tally columns are one per should-clause, same as CreatePlan.
            var settings = JamlSearchBuilder.CreateSettings(_config);
            TallyLabels = [.. _config.Should.Select(JamlSearchBuilder.DefaultTallyLabel)];

            settings = settings
                .WithDeck(_config.Deck)
                .WithStake(_config.Stake)
                .WithThreadCount(Threads)
                .WithBatchCharacterCount(BatchCharCount)
                .WithStartBatchIndex(StartBatch)
                .WithEndBatchIndex(EndBatch)
                .WithSequentialSearch()
                .WithQuietMode(true)
                .WithProgressCallback(p =>
                {
                    _seedsSearched = p.SeedsSearched;
                    _matchingSeeds = p.MatchingSeeds;
                    _elapsedMs = p.ElapsedMilliseconds;
                    _seedsPerMs = p.SeedsPerMillisecond;
                    _percent = p.PercentComplete;
                    _etaMs = p.EstimatedTimeRemainingMilliseconds;
                });

            if (_config.Should.Count > 0)
            {
                settings = settings.WithScoredResultCallback(r =>
                    Record(new FoundSeedDto(r.Seed, r.Score, [.. r.Tally.Select(b => (int)b)])));
            }
            else
            {
                settings = settings.WithSeedMatchCallback(seed =>
                    Record(new FoundSeedDto(seed, 0, null)));
            }

            if (StopAfter > 0)
                settings = settings.StopAfter(StopAfter);

            _search = settings.Start(_cts.Token);
            _search.WaitForCompletionAsync(_cts.Token).GetAwaiter().GetResult();

            Status = _cts.IsCancellationRequested ? "stopped" : "completed";
        }
        catch (OperationCanceledException)
        {
            Status = "stopped";
        }
        catch (Exception ex)
        {
            Status = "error";
            Error = ex.Message;
        }
    }

    private void Record(FoundSeedDto found)
    {
        Interlocked.Increment(ref _totalMatches);
        lock (_gate)
        {
            if (_results.Count < MaxKeptResults)
                _results.Add(found);
        }
    }

    public void Stop() => _cts.Cancel();

    public SearchJobDto Snapshot() => new(
        Id, Filter, Name, Status, Threads, BatchCharCount,
        _seedsSearched, _matchingSeeds, _seedsPerMs, _percent,
        _elapsedMs, _etaMs, StartedAt, Error, TallyLabels,
        Interlocked.Read(ref _totalMatches));

    public JobResultsDto ResultsSnapshot()
    {
        lock (_gate)
        {
            return new JobResultsDto(
                Id, Filter, TallyLabels, Interlocked.Read(ref _totalMatches), [.. _results]);
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _search?.Dispose();
        _cts.Dispose();
    }
}

// ── DTOs ────────────────────────────────────────────────────────────

public sealed record FilterDto(string Name, long Bytes);

public sealed record FoundSeedDto(string Seed, int Score, int[]? Tally);

public sealed record StartSearchRequest(
    string? Filter,
    string? Jaml,
    int? Threads,
    int? BatchCharCount,
    long? StartBatch,
    long? EndBatch,
    long? StopAfter);

public sealed record StartSearchResponse(string JobId);

public sealed record StopSearchResponse(bool Stopped);

public sealed record ValidateRequest(string? Jaml);

public sealed record ValidateResponse(bool Ok, List<string> Errors);

public sealed record SearchJobDto(
    string Id,
    string Filter,
    string? Name,
    string Status,
    int Threads,
    int BatchCharCount,
    long SeedsSearched,
    long MatchingSeeds,
    double SeedsPerMillisecond,
    double PercentComplete,
    long ElapsedMs,
    long? EtaMs,
    string StartedAt,
    string? Error,
    string[] TallyLabels,
    long TotalMatches);

public sealed record JobResultsDto(
    string Id,
    string Filter,
    string[] TallyLabels,
    long TotalMatches,
    FoundSeedDto[] Results);

public sealed record ErrorResponse(string Error);
