using System.Collections.Concurrent;
using Motely;
using Motely.DistributedWorker;
using Motely.Filters;

/// <summary>
/// Motely Distributed Worker — AOT native Linux executable.
///
/// Connects to the seed-finder pool and claims one block at a time
/// from whatever filter currently needs help most.
///
/// Usage:
///   MotelyWorker --pool https://www.seedfinder.app --pool-token <POOL_TOKEN>
///
/// Options:
///   --threads N       Thread count (default: all cores)
///   --worker-id id    Worker identifier (default: hostname-pid)
/// </summary>
class Program
{
    static async Task<int> Main(string[] args)
    {
        // ── Parse args ──────────────────────────────────────────────────
        string? url = null, poolToken = null, workerId = null;
        int threads = Environment.ProcessorCount;

        for (int i = 0; i < args.Length - 1; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--pool": url = args[++i]; break;
                case "--pool-token": poolToken = args[++i]; break;
                case "--threads": threads = int.Parse(args[++i]); break;
                case "--worker-id": workerId = args[++i]; break;
            }
        }

        if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(poolToken))
        {
            Console.Error.WriteLine("Usage:");
            Console.Error.WriteLine("  MotelyWorker --pool <base-url> --pool-token <POOL_TOKEN>");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Options:");
            Console.Error.WriteLine("  --threads <N>      Thread count (default: all cores)");
            Console.Error.WriteLine("  --worker-id <id>   Worker identifier (optional)");
            return 1;
        }

        workerId ??= $"{Environment.MachineName}-{Environment.ProcessId}";

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        return await RunPoolMode(url, poolToken, workerId, threads, cts);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  POOL MODE — claim one block at a time from the help-wanted queue
    // ═══════════════════════════════════════════════════════════════════
    static async Task<int> RunPoolMode(string poolUrl, string poolToken, string workerId, int threads, CancellationTokenSource cts)
    {
        using var pool = new PoolClient(poolUrl, poolToken);

        Console.Error.WriteLine($"[MotelyWorker] → {poolUrl}");
        Console.Error.WriteLine($"[MotelyWorker] Worker: {workerId} | Threads: {threads}");
        Console.Error.WriteLine("[MotelyWorker] Waiting for work...");
        Console.Error.WriteLine();

        long totalSeedsSearched = 0;
        long totalMatches = 0;
        int chunksCompleted = 0;
        var startTime = DateTime.UtcNow;

        while (!cts.Token.IsCancellationRequested)
        {
            // ── CLAIM from pool ──────────────────────────────────────
            PoolClaimResponseDto claim;
            try
            {
                claim = await pool.ClaimAsync(workerId, cts.Token);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[MotelyWorker] Pool claim failed: {ex.Message}. Retrying in 5s...");
                await Task.Delay(5000, cts.Token).ConfigureAwait(false);
                continue;
            }

            if (claim.Idle)
            {
                Console.Error.Write("\r[MotelyWorker] Idle — no work available. Waiting...          ");
                await Task.Delay(claim.RetryAfterMs, cts.Token).ConfigureAwait(false);
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
            if (Enum.TryParse<MotelyDeck>(claim.Deck, true, out var deck)) config.Deck = deck;
            if (Enum.TryParse<MotelyStake>(claim.Stake, true, out var stake)) config.Stake = stake;

            // ── SEARCH ───────────────────────────────────────────────
            var matchResults = new ConcurrentBag<SeedResultDto>();
            long seedsSearched = 0;

            var plan = JamlSearchBuilder.CreatePlan(config);
            var settings = plan.Settings
                .WithDeck(config.Deck)
                .WithStake(config.Stake)
                .WithThreadCount(threads)
                .WithBatchCharacterCount(claim.BatchCharCount)
                .WithStartBatchIndex(claim.BatchIndex)
                .WithEndBatchIndex(claim.BatchIndex + 1)
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

            // ── SUBMIT ───────────────────────────────────────────────
            var submitBody = new SubmitResultsDto
            {
                StartBatch = claim.BatchIndex,
                EndBatch = claim.BatchIndex + 1,
                Results = matchResults.ToArray(),
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
                    Console.Error.WriteLine("[MotelyWorker] Submit retry failed. Results lost for this chunk.");
                }
            }

            chunksCompleted++;

            var elapsed = DateTime.UtcNow - startTime;
            double speed = elapsed.TotalSeconds > 0 ? totalSeedsSearched / elapsed.TotalSeconds : 0;
            Console.Error.Write(
                $"\r[MotelyWorker] Filter:{claim.FilterId} | Block:{claim.BatchIndex}/{claim.Remaining} remaining | Seeds: {totalSeedsSearched:N0} | Matches: {totalMatches} | {speed:N0} seeds/s  "
            );
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
