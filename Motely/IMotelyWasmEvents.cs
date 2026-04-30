namespace Motely;

public interface IMotelyWasmEvents
{
    void NotifyProgress(long seedsSearched, long matchingSeeds);
    void NotifyResult(string seed, int score, int[] tallyColumns);
    void NotifyComplete(string status, long totalSeedsSearched,
        long matchingSeeds);
    void NotifyJamlLibraryChanged(string rootId, string[] fileUris);
}
