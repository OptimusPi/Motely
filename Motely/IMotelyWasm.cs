namespace Motely;

public interface IMotelyWasm
{
    string GetVersion();
    string ValidateJaml(string jaml);
    string CompileJummy(string jummy);
    IMotelyWasmSearchContext CreateSearchContext(string seed, MotelyDeck deck, MotelyStake stake);
    void StartRandomSearch(string jaml, int randomSeedCount);
    void StartSequentialSearch(string jaml, int batchCharCount,
        long startBatch, long endBatch);
    void StartSeedListSearch(string jaml, string[] seeds);
    void StartKeywordSearch(string jaml, string keywordsCsv,
        string paddingChars);
    void StopSearch();
}
