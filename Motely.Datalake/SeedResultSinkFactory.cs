#if !BROWSER

namespace Motely.Datalake;

public static class SeedResultSinkFactory
{
    public static ISeedResultSink Create(string filterId, int tallyCount)
    {
        return MotelyLake.GetSink(filterId, tallyCount);
    }
}

#endif
