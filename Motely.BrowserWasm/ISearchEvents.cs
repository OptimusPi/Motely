#nullable enable

namespace Motely.BrowserWasm;

public interface ISearchEvents
{
    void NotifyProgress(long seedsSearched, long matchingSeeds);
    void NotifyResult(string seed, int score, int[] tallyColumns);
    void NotifyComplete(string status, long totalSeedsSearched, long matchingSeeds);
}
