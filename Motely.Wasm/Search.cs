using Bootsharp;
using Motely;
using Motely.Filters;
using Motely.Filters.Jaml;
using Motely.Filters.Native;

/// <summary>Search host. Finds on <see cref="OnScored"/>. Jimmolate is the live context.</summary>
public static partial class Search
{
    [Export]
    public static event Action<MotelyProgress>? OnProgress;

    [Export]
    public static event Action<MotelySeedScore>? OnScored;

    /// <summary>
    /// JS predicate. Gets the live <see cref="MotelySingleSearchContext"/> (specialization rail),
    /// not a seed string. Return score; 0 drops. Same contract as
    /// <see cref="MotelyIndividualSeedSearcher"/> / JimmolateFilterTests.
    /// </summary>
    [Import]
    public static partial int Jimmolate(MotelySingleSearchContext ctx);

    [Export]
    public static async Task ScoreList(string jaml, string[] seeds)
    {
        var config = JamlConfigLoader.FromJaml(jaml);
        using var search = JamlSearchBuilder
            .CreateSettings(config)
            .WithSeedList([.. seeds.Where(static s => !string.IsNullOrWhiteSpace(s))])
            .WithThreadCount(1)
            .WithQuietMode(true)
            .WithProgressCallback(static p => OnProgress?.Invoke(p))
            .WithScoredResultCallback(static t =>
                OnScored?.Invoke(new MotelySeedScore(t.Seed, t.Score, t.TallyValuesSpan.ToArray()))
            )
            .Start();
        await search.WaitForCompletionAsync();
    }

    /// <summary>
    /// Passthrough + Jimmolate. Predicate is <see cref="Jimmolate"/> — live context, in-engine.
    /// </summary>
    [Export]
    public static async Task JimmolateList(string[] seeds)
    {
        using var search = new MotelySearchSettings<PassthroughFilterDesc.PassthroughFilter>(
            new PassthroughFilterDesc()
        )
            .WithSeedList([.. seeds.Where(static s => !string.IsNullOrWhiteSpace(s))])
            .WithThreadCount(1)
            .WithQuietMode(true)
            .WithJimmolate(static ctx => Jimmolate(ctx))
            .WithProgressCallback(static p => OnProgress?.Invoke(p))
            .WithSeedMatchCallback(static s =>
                OnScored?.Invoke(new MotelySeedScore(s, 1, []))
            )
            .Start();
        await search.WaitForCompletionAsync();
    }
}
