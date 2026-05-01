#if !BROWSER

using System.Collections.Concurrent;

namespace Motely.Datalake;

public sealed class SeedResultSinkDirectory : IDisposable
{
    private readonly string _directory;
    private readonly int _tallyCount;
    private readonly ConcurrentDictionary<string, ISeedResultSink> _sinks = new();

    public SeedResultSinkDirectory(string directory, int tallyCount)
    {
        _directory = directory;
        _tallyCount = tallyCount;
        Directory.CreateDirectory(directory);
    }

    public ISeedResultSink GetOrOpen(string filterId)
    {
        return _sinks.GetOrAdd(filterId, id => MotelyLake.GetSink(id, _tallyCount));
    }

    public void Dispose()
    {
        foreach (var sink in _sinks.Values)
            sink.Dispose();
        _sinks.Clear();
    }
}

#endif
