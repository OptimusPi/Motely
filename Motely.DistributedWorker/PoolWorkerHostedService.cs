using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Motely;
using Motely.DB.SeedSource;
using Motely.Filters;

namespace Motely.DistributedWorker;

/// <summary>Runs the pool worker loop inside the API process. Claim → search → submit.</summary>
public sealed class PoolWorkerHostedService : BackgroundService
{
    private readonly PoolWorkerOptions _options;
    private readonly ILogger<PoolWorkerHostedService>? _logger;

    public PoolWorkerHostedService(IOptions<PoolWorkerOptions> options, ILogger<PoolWorkerHostedService>? logger = null)
    {
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_options.Url))
            return;

        var workerId = string.IsNullOrWhiteSpace(_options.WorkerId)
            ? $"{Environment.MachineName}-{Environment.ProcessId}"
            : _options.WorkerId;
        var threads = Math.Clamp(_options.Threads, 1, Environment.ProcessorCount);
        var targetFilterId = string.IsNullOrWhiteSpace(_options.FilterId) ? null : _options.FilterId;
        var localDbDir = string.IsNullOrWhiteSpace(_options.LocalDbPath) ? null : _options.LocalDbPath;

        _logger?.LogInformation(
            "Pool worker starting: {PoolUrl}, WorkerId={WorkerId}, Threads={Threads}, Filter={Filter}, LocalDb={LocalDb}",
            _options.Url, workerId, threads, targetFilterId ?? "any", localDbDir ?? "disabled");

        using var pool = new PoolClient(_options.Url.TrimEnd('/'));
        long totalSeedsSearched = 0;
        long totalMatches = 0;
        int chunksCompleted = 0;

        // ── Per-filter local DuckLake sinks ─────────────────────────────
        var localSinks = new Dictionary<string, ISeedResultSink>();
        ISeedResultSink? GetOrOpenSink(string fId)
        {
            if (localDbDir == null) return null;
            if (localSinks.TryGetValue(fId, out var existing)) return existing;
            try
            {
                Directory.CreateDirectory(localDbDir);
                var dbPath = Path.Combine(localDbDir, $"{fId}.db");
                var sink = SeedResultSinkFactory.Create(dbPath, tallyCount: 0);
                localSinks[fId] = sink;
                _logger?.LogInformation("Opened local DuckLake: {Path}", dbPath);
                return sink;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Could not open local DuckLake for {FilterId}", fId);
                return null;
            }
        }

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                PoolClaimResponseDto claim;
                try
                {
                    claim = await pool.ClaimAsync(workerId, targetFilterId, stoppingToken);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Pool claim failed, retrying in 10s");
                    try { await Task.Delay(10000, stoppingToken).ConfigureAwait(false); }
                    catch (OperationCanceledException) { break; }
                    continue;
                }

                if (claim.Idle)
                {
                    try { await Task.Delay(claim.RetryAfterMs, stoppingToken).ConfigureAwait(false); }
                    catch (OperationCanceledException) { break; }
                    continue;
                }

                if (string.IsNullOrEmpty(claim.Jaml) || string.IsNullOrEmpty(claim.FilterId))
                {
                    await Task.Delay(2000, stoppingToken).ConfigureAwait(false);
                    continue;
                }

                if (!JamlConfigLoader.TryLoad(claim.Jaml, out var config, out var parseError) || config is null)
                {
                    _logger?.LogWarning("JAML parse error: {Error}", parseError);
                    await Task.Delay(2000, stoppingToken).ConfigureAwait(false);
                    continue;
                }

                var matchResults = new ConcurrentBag<SeedResultDto>();
                long seedsSearched = 0;
                long endBatchExclusive = claim.BatchIndex + Math.Max(1, claim.Remaining);

                var plan = JamlSearchBuilder.CreatePlan(config);
                var settings = plan.Settings
                    .WithDeck(config.Deck)
                    .WithStake(config.Stake)
                    .WithThreadCount(threads)
                    .WithBatchCharacterCount(claim.BatchCharCount)
                    .WithStartBatchIndex(claim.BatchIndex)
                    .WithEndBatchIndex(endBatchExclusive)
                    .WithSequentialSearch();

                settings.WithSeedMatchCallback(line =>
                {
                    int comma = line.IndexOf(',');
                    if (comma < 0) { matchResults.Add(new SeedResultDto { Seed = line }); return; }
                    string seed = line[..comma];
                    int comma2 = line.IndexOf(',', comma + 1);
                    var scoreSpan = comma2 >= 0 ? line.AsSpan(comma + 1, comma2 - comma - 1) : line.AsSpan(comma + 1);
                    matchResults.Add(new SeedResultDto { Seed = seed, Score = int.TryParse(scoreSpan, out int s) ? s : 0 });
                });

                try
                {
                    using var search = settings.Start();
                    search.Start(stoppingToken);
                    await search.WaitForCompletionAsync(stoppingToken);
                    seedsSearched = search.TotalSeedsSearched;
                }
                catch (OperationCanceledException) { break; }

                totalSeedsSearched += seedsSearched;
                totalMatches += matchResults.Count;

                var results = matchResults.ToArray();

                // ── Save to local DuckLake ───────────────────────────────
                if (results.Length > 0)
                {
                    var sink = GetOrOpenSink(claim.FilterId!);
                    if (sink != null)
                        foreach (var r in results)
                            sink.AppendScoredResult(r.Seed, r.Score, ReadOnlySpan<int>.Empty);
                }

                // ── Submit to pool ───────────────────────────────────────
                var submitBody = new SubmitResultsDto
                {
                    StartBatch = claim.BatchIndex,
                    EndBatch = endBatchExclusive,
                    Results = results,
                    SeedsSearched = seedsSearched,
                };

                try
                {
                    await pool.SubmitResultsAsync(claim.FilterId!, submitBody, stoppingToken);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Submit failed for filter {FilterId}, retrying", claim.FilterId);
                    try
                    {
                        await Task.Delay(2000, stoppingToken);
                        await pool.SubmitResultsAsync(claim.FilterId!, submitBody, stoppingToken);
                    }
                    catch (Exception ex2) { _logger?.LogError(ex2, "Submit retry failed"); }
                }

                chunksCompleted++;
            }
        }
        finally
        {
            foreach (var (fId, sink) in localSinks)
            {
                try { sink.Dispose(); }
                catch (Exception ex) { _logger?.LogWarning(ex, "DuckLake flush failed for {FilterId}", fId); }
            }
        }

        _logger?.LogInformation("Pool worker stopped. Chunks={Chunks}, Seeds={Seeds}, Matches={Matches}",
            chunksCompleted, totalSeedsSearched, totalMatches);
    }
}
