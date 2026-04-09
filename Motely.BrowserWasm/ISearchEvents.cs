#nullable enable

namespace Motely.BrowserWasm;

/// <summary>JS implements this (JSImport); used by <c>MotelyJamlSearchBuilder</c> for progress/results.</summary>
public interface ISearchEvents
{
    void NotifyProgress(long seedsSearched, long matchingSeeds);
    void NotifyResult(string seed, int score, int[] tallyColumns);
    void NotifyComplete(string status, long totalSeedsSearched, long matchingSeeds);
}
