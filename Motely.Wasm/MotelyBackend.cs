using Motely.Filters;
using Motely.Filters.Jaml;

namespace Motely.Wasm;

public sealed class MotelyBackend : IMotelyBackend
{
    public event Action<MotelySearchProgress>? OnProgress;
    public event Action<string>? OnSeedMatch;
    public event Action<MotelySeedMatch>? OnScoredResult;

    private CancellationTokenSource? _cts;

    public async Task RunSearch(string jamlYaml)
    {
        CancelSearch();
        var cts = _cts = new CancellationTokenSource();

        var config = JamlConfigLoader.FromYaml(jamlYaml);
        var plan = JamlSearchBuilder.CreatePlan(config);

        IMotelySearchSettings settings = plan
            .Settings.WithDeck(config.Deck)
            .WithStake(config.Stake)
            .WithProgressCallback(p =>
                OnProgress?.Invoke(
                    new(p.PercentComplete, p.SeedsSearched, p.MatchingSeeds, p.SeedsPerMillisecond)
                )
            );

        bool hasScore = plan.ScoreTallyColumnCount > 0;
        settings = hasScore
            ? settings.WithScoredResultCallback(tally =>
                OnScoredResult?.Invoke(new(tally.Seed, tally.Score))
            )
            : settings.WithSeedMatchCallback(seed => OnSeedMatch?.Invoke(seed));

        using var search = settings.Start(cts.Token);
        try
        {
            await search.WaitForCompletionAsync(cts.Token);
        }
        catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
        {
            // Cancellation requested via CancelSearch() — not an error.
        }
    }

    public void CancelSearch() => _cts?.Cancel();
}
