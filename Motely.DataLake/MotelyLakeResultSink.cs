using Motely.Filters;

namespace Motely.DataLake;

public sealed class MotelyLakeResultSink : IMotelyResultSink
{
    private readonly MotelyLakeSeedSink inner;

    public MotelyLakeResultSink(string seedsRoot, string filterId, IReadOnlyList<string> tallyLabels)
    {
        inner = new MotelyLakeSeedSink(seedsRoot, filterId, tallyLabels);
    }

    public void OnSeed(string seed) { }

    public void OnScored(in MotelySeedScoreTally tally) => inner.Append(in tally);

    public void Dispose() => inner.Dispose();
}
