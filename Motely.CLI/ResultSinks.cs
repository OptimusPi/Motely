using Motely.DataLake;
using Motely.Filters;

namespace Motely.CLI;

internal interface IMotelyResultSink : IDisposable
{
    void OnSeed(string seed);
    void OnScored(in MotelySeedScoreTally tally);
}

internal sealed class CompositeResultSink : IMotelyResultSink
{
    private readonly IMotelyResultSink[] _sinks;

    public CompositeResultSink(IEnumerable<IMotelyResultSink> sinks)
    {
        _sinks = sinks.ToArray();
    }

    public void OnSeed(string seed)
    {
        for (int i = 0; i < _sinks.Length; i++)
            _sinks[i].OnSeed(seed);
    }

    public void OnScored(in MotelySeedScoreTally tally)
    {
        for (int i = 0; i < _sinks.Length; i++)
            _sinks[i].OnScored(in tally);
    }

    public void Dispose()
    {
        for (int i = _sinks.Length - 1; i >= 0; i--)
            _sinks[i].Dispose();
    }
}

internal sealed class ConsoleResultSink : IMotelyResultSink
{
    public void OnSeed(string seed) => Console.WriteLine(seed);

    public void OnScored(in MotelySeedScoreTally tally)
    {
        var tallies = string.Join(",", tally.TallyValuesSpan.ToArray());
        Console.WriteLine($"{tally.Seed},{tally.Score},{tallies}");
    }

    public void Dispose() { }
}

internal sealed class MotelyLakeResultSink : IMotelyResultSink
{
    private readonly MotelyLakeSeedSink _inner;

    public MotelyLakeResultSink(string seedsRoot, string filterId, IReadOnlyList<string> tallyLabels)
    {
        _inner = new MotelyLakeSeedSink(seedsRoot, filterId, tallyLabels);
    }

    public void OnSeed(string seed) { }

    public void OnScored(in MotelySeedScoreTally tally) => _inner.Append(in tally);

    public void Dispose() => _inner.Dispose();
}
