using Motely.Filters;

namespace Motely;

public interface IMotelyWasm
{
    string GetVersion();
    string ValidateJaml(string jaml);
    string CompileJummy(string jummy);
    IMotelyWasmSearchContext CreateSearchContext(string seed, MotelyDeck deck, MotelyStake stake);
    IMotelyWasmSearch StartRandomSearch(string jaml, int randomSeedCount);
    IMotelyWasmSearch StartAestheticSearch(string jaml, JamlAesthetic aesthetic);
    IMotelyWasmSearch StartSequentialSearch(string jaml, int batchCharCount,
        long startBatch, long endBatch);
    Task<MotelyWasmSearchBatchResult> RunSequentialSearchBatch(string jaml, int batchCharCount,
        long startBatch, long endBatch, int maxResults);
    IMotelyWasmSearch StartSeedListSearch(string jaml, string[] seeds);
    IMotelyWasmSearch StartKeywordSearch(string jaml, string keywordsCsv,
        string paddingChars);
    string[] GetTallyLabels(string jaml);
}
