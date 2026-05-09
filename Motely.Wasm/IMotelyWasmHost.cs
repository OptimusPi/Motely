using Motely.Analysis;
using Motely.Filters;

namespace Motely;

public interface IMotelyWasmHost
{
    string GetVersion();
    MotelyItemLayout GetItemLayout();
    string GetJamlSchema();
    string ValidateJaml(string jaml);
    JamlValidationResult ValidateJamlStructured(string jaml);
    JamlMetaResult GetJamlMeta(string jaml);
    string ExplainJamlPerformance(string jaml);
    string[] GetTallyLabels(string jaml);
    MotelyJamlyzerResult AnalyzeJamlSeeds(string jaml, string[] seeds);
    IMotelyWasmSearch StartRandomSearch(string jaml, int randomSeedCount);
    IMotelyWasmSearch StartAestheticSearch(string jaml, JamlAesthetic aesthetic);
    IMotelyWasmSearch StartSequentialSearch(string jaml, int batchCharCount, long startBatch, long endBatch);
    Task<MotelyWasmSearchBatchResult> RunSequentialSearchBatch(string jaml, int batchCharCount, long startBatch, long endBatch, int maxResults);
    IMotelyWasmSearch StartSeedListSearch(string jaml, string[] seeds);
    IMotelyWasmSearch StartKeywordSearch(string jaml, string keywordsCsv, string paddingChars);
    Task<string?> MountJamlLibrary();
    Task UnmountJamlLibrary(string rootId);
    string[] GetJamlLibraryFiles(string rootId);
    Task<string> LoadLibraryFile(string rootId, string uri);
    Task SaveLibraryFile(string rootId, string uri, string content);
}
