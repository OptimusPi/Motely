namespace Motely.BrowserWasm;

public interface IMotelyWasmBackend
{
    // Single-seed: shop items as packed ints (JS unpacks with bitmask constants)
    IReadOnlyList<int> GetShopItems(string seed, string deck, string stake, int ante, int offset, int count);

    // Classic analyzer: returns formatted text block (MotelySeedAnalysis.ToString())
    string AnalyzeSeed(string seed, string deck, string stake);

    // JAML validation
    bool ValidateJaml(string jamlContent);
    string ValidateJamlWithError(string jamlContent);

    // Bulk search
    void StartJamlSearch(string jamlContent, int threadCount);
    void StopSearch();

    // Capabilities
    string GetVersion();
    bool IsSimdEnabled();
    int GetProcessorCount();
}
