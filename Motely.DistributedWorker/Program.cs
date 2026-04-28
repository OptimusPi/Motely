using System.Collections.Concurrent;
using Motely;
using Motely.Datalake;
using Motely.DistributedWorker;
using Motely.Filters;

/// <summary>
/// Motely Distributed Worker — AOT native Linux executable.
///
/// Connects to the seed-finder pool and claims one block (35^5 seeds) at a time.
/// <see cref="Motely.DB.SeedSource.SeedResultSinkDirectory"/> writes all filters into one shared DuckLake root (partitioned by filter_id).
///
/// Usage:
///   MotelyWorker --pool https://www.seedfinder.app
///
/// Options:
///   --threads N           Motely search thread count for each claimed block (SIMD workers inside one block)
///   --worker-id id        Worker identifier (default: hostname-pid)
///   --filter filterId     Only claim blocks for this filter (optional; omit for any active filter)
///   --local-db ./dir      Shared DuckLake directory (default: Seeds/ducklake)
///                         Set to "-" to disable local saving.
/// </summary>
class Program
{
    static async Task<int> Main(string[] args)
    {
        // ── Parse args ──────────────────────────────────────────────────
        string? url = null, workerId = null, filterId = null, localDbDir = "Seeds/ducklake";
        int threads = Environment.ProcessorCount;

        for (int i = 0; i < args.Length - 1; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--pool": url = args[++i]; break;
                case "--threads": threads = int.Parse(args[++i]); break;
                case "--worker-id":
                case "--workerid": // common typo / convenience
                    workerId = args[++i]; break;
                case "--filter": filterId = args[++i]; break;
                case "--local-db": localDbDir = args[++i]; break;
            }
        }

        if (string.IsNullOrEmpty(url))
        {
            Console.Error.WriteLine("Usage:");
            Console.Error.WriteLine("  MotelyWorker --pool <helper-url>");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Options:");
            Console.Error.WriteLine("  --threads <N>        Search threads per claimed block (default: all cores)");
            Console.Error.WriteLine("  --worker-id <id>     Worker identifier (optional)");
            Console.Error.WriteLine("  --filter <filterId>  Only claim blocks for this filter (optional)");
            Console.Error.WriteLine("  --local-db <dir>     Shared DuckLake root (default: Seeds/ducklake)");
            Console.Error.WriteLine("                       Use '-' to disable local saving");
            return 1;
        }

        workerId ??= $"{Environment.MachineName}-{Environment.ProcessId}";
        if (localDbDir == "-") localDbDir = null;

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        return await RunPoolMode(url, workerId, threads, filterId, localDbDir, cts);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  POOL MODE — claim one block at a time from the help-wanted queue
    // ═══════════════════════════════════════════════════════════════════
    static async Task<int> RunPoolMode(
        string poolUrl, string workerId, int threads,
        string? targetFilterId, string? localDbDir,
        CancellationTokenSource cts)
    {
        using var pool = new PoolClient(poolUrl);

        Console.Error.WriteLine($"[MotelyWorker] → {poolUrl}");
        Console.Error.WriteLine($"[MotelyWorker] Worker: {workerId} | Threads: {threads}");
        if (targetFilterId != null)
            Console.Error.WriteLine($"[MotelyWorker] Targeting filter: {targetFilterId}");
        if (localDbDir != null)
            Console.Error.WriteLine($"[MotelyWorker] Local DuckLake root: {Path.GetFullPath(localDbDir)} (filter_id partitions)");
        Console.Error.WriteLine("[MotelyWorker] Waiting for work...");
        Console.Error.WriteLine();

        long totalSeedsSearched = 0;
        long totalMatches = 0;
        int chunksCompleted = 0;
        var startTime = DateTime.UtcNow;

        SeedResultSinkDirectory? localSinks = null;
        if (localDbDir != null)
        {
            try
            {
                localSinks = new SeedResultSinkDirectory(localDbDir, tallyCount: 0);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[MotelyWorker] Warning: could not open local DuckLake sink directory: {ex.Message}");
            }
        }

        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                // ── CLAIM from pool ──────────────────────────────────────
                PoolClaimResponseDto claim;
                try
                {
                    claim = await pool.ClaimAsync(workerId, targetFilterId, cts.Token);
                    if (!claim.Idle)
                        Console.WriteLine(
                            $"[MotelyWorker] Claim: filter={claim.FilterId} | block={claim.BatchIndex} | batchCharCount={claim.BatchCharCount}"
                        );
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[MotelyWorker] Pool claim failed: {ex.Message}. Retrying in 60s...");
                    try { await Task.Delay(60000, cts.Token).ConfigureAwait(false); }
                    catch (OperationCanceledException) { break; }
                    continue;
                }

                if (claim.Idle)
                {
                    Console.Error.Write("\r[MotelyWorker] Idle — no work available. Waiting...          ");
                    try { await Task.Delay(claim.RetryAfterMs, cts.Token).ConfigureAwait(false); }
                    catch (OperationCanceledException) { break; }
                    continue;
                }

                if (string.IsNullOrEmpty(claim.Jaml) || string.IsNullOrEmpty(claim.FilterId))
                {
                    Console.Error.WriteLine("[MotelyWorker] Invalid pool claim response. Retrying...");
                    await Task.Delay(2000, cts.Token).ConfigureAwait(false);
                    continue;
                }

                // ── PARSE JAML ───────────────────────────────────────────
                if (!JamlConfigLoader.TryLoad(claim.Jaml, out var config, out var parseError) || config is null)
                {
                    Console.Error.WriteLine($"[MotelyWorker] JAML parse error: {parseError}");
                    await Task.Delay(2000, cts.Token).ConfigureAwait(false);
                    continue;
                }

                // ── SEARCH ───────────────────────────────────────────────
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
                    Console.WriteLine($"[MotelyWorker] Searching filter: {claim.FilterId}");
                    using var search = settings.Start();
                    search.Start(cts.Token);
                    await search.WaitForCompletionAsync(cts.Token);
                    seedsSearched = search.TotalSeedsSearched;
                }
                catch (OperationCanceledException)
                {
                    Console.Error.WriteLine("\n[MotelyWorker] Search cancelled.");
                    break;
                }

                totalSeedsSearched += seedsSearched;
                totalMatches += matchResults.Count;

                var results = matchResults.ToArray();

                // ── SAVE TO LOCAL DUCKLAKE ────────────────────────────────
                if (results.Length > 0)
                {
                    try
                    {
                        var sink = localSinks?.GetOrOpen(claim.FilterId!);
                        if (sink != null)
                        {
                            foreach (var r in results)
                                sink.AppendScoredResult(r.Seed, r.Score, ReadOnlySpan<int>.Empty);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"[MotelyWorker] Warning: could not write local DuckLake for {claim.FilterId}: {ex.Message}");
                    }
                }

                // ── SUBMIT TO POOL ───────────────────────────────────────
                var submitBody = new SubmitResultsDto
                {
                    StartBatch = claim.BatchIndex,
                    EndBatch = endBatchExclusive,
                    Results = results,
                    SeedsSearched = seedsSearched,
                };

                try
                {
                    await pool.SubmitResultsAsync(claim.FilterId!, submitBody, cts.Token);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"\n[MotelyWorker] Submit failed: {ex.Message}. Retrying...");
                    try
                    {
                        await Task.Delay(2000, cts.Token);
                        await pool.SubmitResultsAsync(claim.FilterId!, submitBody, cts.Token);
                    }
                    catch
                    {
                        Console.Error.WriteLine("[MotelyWorker] Submit retry failed. Results are saved locally but not submitted to pool.");
                    }
                }

                chunksCompleted++;

                var elapsed = DateTime.UtcNow - startTime;
                double speed = elapsed.TotalSeconds > 0 ? totalSeedsSearched / elapsed.TotalSeconds : 0;
                Console.Error.Write(
                    $"\r[MotelyWorker] Filter:{claim.FilterId} | Block:{claim.BatchIndex} | Seeds: {totalSeedsSearched:N0} | Matches: {totalMatches} | {speed:N0} seeds/s  "
                );
            }
        }
        finally
        {
            try
            {
                localSinks?.Dispose();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"\n[MotelyWorker] Warning: DuckLake flush failed: {ex.Message}");
            }
        }

        PrintSummary(workerId, chunksCompleted, totalSeedsSearched, totalMatches, startTime);
        return cts.Token.IsCancellationRequested ? 1 : 0;
    }

    static void PrintSummary(string workerId, int chunksCompleted, long totalSeedsSearched, long totalMatches, DateTime startTime)
    {
        var totalElapsed = DateTime.UtcNow - startTime;
        double finalSpeed = totalElapsed.TotalSeconds > 0 ? totalSeedsSearched / totalElapsed.TotalSeconds : 0;

        Console.Error.WriteLine();
        Console.Error.WriteLine();
        Console.Error.WriteLine("═══════════════════════════════════════════");
        Console.Error.WriteLine($"  Worker:   {workerId}");
        Console.Error.WriteLine($"  Chunks:   {chunksCompleted}");
        Console.Error.WriteLine($"  Seeds:    {totalSeedsSearched:N0}");
        Console.Error.WriteLine($"  Matches:  {totalMatches}");
        Console.Error.WriteLine($"  Time:     {totalElapsed:hh\\:mm\\:ss}");
        Console.Error.WriteLine($"  Speed:    {finalSpeed:N0} seeds/sec");
        Console.Error.WriteLine("═══════════════════════════════════════════");
    }
}
