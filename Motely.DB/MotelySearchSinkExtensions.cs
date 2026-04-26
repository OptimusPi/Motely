using Motely.DB.SeedSource;

namespace Motely.DB;

public static class MotelySearchSinkExtensions
{
    public static IMotelySearchSettings WithSeedSink(this IMotelySearchSettings settings, ISeedResultSink sink) =>
        settings.WithScoredResultCallback(t => sink.AppendScoredResult(t.Seed, t.Score, t.TallyValuesSpan));
}
