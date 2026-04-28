namespace Motely.Datalake;

public interface ISeedResultSink : IDisposable
{
    void AppendScoredResult(string seed, int score, ReadOnlySpan<int> tallies);
    string OutputPath { get; }
}
