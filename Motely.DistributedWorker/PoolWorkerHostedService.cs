using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Motely;
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
        if (string.IsNullOrWhiteSpace(_options.Url) || string.IsNullOrWhiteSpace(_options.Token))
            return;

        var workerId = string.IsNullOrWhiteSpace(_options.WorkerId)
            ? $"{Environment.MachineName}-{Environment.ProcessId}"
            : _options.WorkerId;
        var threads = Math.Clamp(_options.Threads, 1, Environment.ProcessorCount);

        _logger?.LogInformation("Pool worker starting: {PoolUrl}, WorkerId={WorkerId}, Threads={Threads}", _options.Url, workerId, threads);

        using var pool = new PoolClient(_options.Url.TrimEnd('/'), _options.Token);
        long totalSeedsSearched = 0;
        long totalMatches = 0;
        int chunksCompleted = 0;
        var startTime = DateTime.UtcNow;

        while (!stoppingToken.IsCancellationRequested)
        {
            PoolClaimResponseDto claim;
            try
            {
                claim = await pool.ClaimAsync(workerId, stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Pool claim failed, retrying in 5s");
                await Task.Delay(5000, stoppingToken).ConfigureAwait(false);
                continue;
            }

            if (claim.Idle)
            {
                await Task.Delay(claim.RetryAfterMs, stoppingToken).ConfigureAwait(false);
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
            if (Enum.TryParse<MotelyDeck>(claim.Deck, true, out var deck)) config.Deck = deck;
            if (Enum.TryParse<MotelyStake>(claim.Stake, true, out var stake)) config.Stake = stake;

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
                search.Start(stoppingToken);
                await search.WaitForCompletionAsync(stoppingToken);
                seedsSearched = search.TotalSeedsSearched;
            }
            catch (OperationCanceledException) { break; }

            totalSeedsSearched += seedsSearched;
            totalMatches += matchResults.Count;

            var submitBody = new SubmitResultsDto
            {
                StartBatch = claim.BatchIndex,
                EndBatch = claim.BatchIndex + 1,
                Results = matchResults.ToArray(),
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

        _logger?.LogInformation("Pool worker stopped. Chunks={Chunks}, Seeds={Seeds}, Matches={Matches}",
            chunksCompleted, totalSeedsSearched, totalMatches);
    }
}
