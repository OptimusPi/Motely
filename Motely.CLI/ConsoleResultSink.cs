using Motely.Filters;

namespace Motely.CLI;

internal sealed class ConsoleResultSink : IMotelyResultSink
{
    public void OnSeed(string seed) => Console.WriteLine(seed);

    public void OnScored(in MotelySeedScoreTally tally)
    {
        Console.WriteLine(tally.ToCsvRow());
    }

    public void Dispose() { }
}
