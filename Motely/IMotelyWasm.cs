using Motely.Filters;
using Motely.Analysis;

namespace Motely;

/// <summary>Result of <see cref="IMotelyWasm.ValidateJaml"/>.</summary>
public sealed record JamlValidationResult(
    bool Valid,
    string? Message,
    /// <summary>Dot-path to the offending key, e.g. "must[0].joker". Null when not determinable.</summary>
    string? Path,
    /// <summary>1-based line number from the YAML parser. 0 when not available.</summary>
    int Line,
    /// <summary>1-based column number from the YAML parser. 0 when not available.</summary>
    int Column
);

/// <summary>Cheap structural summary of a JAML filter — no search required.</summary>
public sealed record JamlMetaResult(
    /// <summary>Sorted unique antes checked across all must/should/mustNot clauses.</summary>
    int[] Antes,
    /// <summary>Item type names present (e.g. "Joker", "Voucher", "Boss").</summary>
    string[] ItemTypes,
    /// <summary>Number of must clauses.</summary>
    int MustCount,
    /// <summary>Number of should clauses (scored).</summary>
    int ShouldCount,
    /// <summary>Number of mustNot clauses.</summary>
    int MustNotCount,
    string Deck,
    string Stake
);

public sealed record MotelyItemLayout(
    int ItemTypeMask,
    int StandardcardRankMask,
    int StandardcardSuitOffset,
    int StandardcardSuitMask,
    int ItemTypeCategoryOffset,
    int ItemTypeCategoryMask,
    int JokerRarityOffset,
    int JokerRarityMask,
    int ItemSealOffset,
    int ItemSealMask,
    int ItemEnhancementOffset,
    int ItemEnhancementMask,
    int ItemEditionOffset,
    int ItemEditionMask,
    int PerishableStickerOffset,
    int EternalStickerOffset,
    int RentalStickerOffset
);

public interface IMotelyWasmMetadata
{
    string GetVersion();
    /// <summary>
    /// Returns the bundled JAML JSON Schema as a JSON string for this package version.
    /// Consumers can do <c>const schema = JSON.parse(MotelyWasm.getJamlSchema())</c>
    /// and feed it directly to Monaco/Ajv/etc.
    /// </summary>
    string GetJamlSchema();
    MotelyItemLayout GetItemLayout();
    /// <summary>Structured validation — use instead of the legacy plain-string overload.</summary>
    JamlValidationResult ValidateJamlStructured(string jaml);
    /// <summary>Legacy plain-string validation ("valid" or error message). Kept for back-compat.</summary>
    string ValidateJaml(string jaml);
    string[] GetTallyLabels(string jaml);
    /// <summary>Cheap structural summary without running a search. Safe to call on every keystroke.</summary>
    JamlMetaResult GetJamlMeta(string jaml);
    /// <summary>Runs JAML against a seed list and returns compact analysis data with matched preview items highlighted. Pass a one-item array for a single seed.</summary>
    MotelyJamlyzerResult AnalyzeJamlSeeds(string jaml, string[] seeds);
}

public interface IMotelyWasmSearchApi
{
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
}

public interface IMotelyWasmJamlLibraryApi
{
    // --- JAML Library (Bootsharp.FileSystem) ---

    /// <summary>Opens a directory picker and mounts the selected folder as a JAML library. Returns the root ID, or null if the user cancelled.</summary>
    Task<string?> MountJamlLibrary();
    /// <summary>Unmounts a previously mounted JAML library.</summary>
    Task UnmountJamlLibrary(string rootId);
    /// <summary>Returns the current list of .jaml file URIs in a mounted library.</summary>
    string[] GetJamlLibraryFiles(string rootId);
    Task<string> LoadLibraryFile(string rootId, string uri);
    Task SaveLibraryFile(string rootId, string uri, string content);
    /// <summary>Reads a .jaml file from a mounted library and returns its UTF-8 content.</summary>
    Task<string> LoadJamlFile(string rootId, string uri);
    /// <summary>Writes UTF-8 content to a .jaml file in a mounted library.</summary>
    Task SaveJamlFile(string rootId, string uri, string content);
}

public interface IMotelyWasm
{
    string GetVersion();
    string GetJamlSchema();
    MotelyItemLayout GetItemLayout();
    JamlValidationResult ValidateJamlStructured(string jaml);
    string ValidateJaml(string jaml);
    string[] GetTallyLabels(string jaml);
    JamlMetaResult GetJamlMeta(string jaml);
    MotelyJamlyzerResult AnalyzeJamlSeeds(string jaml, string[] seeds);

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

    Task<string?> MountJamlLibrary();
    Task UnmountJamlLibrary(string rootId);
    string[] GetJamlLibraryFiles(string rootId);
    Task<string> LoadLibraryFile(string rootId, string uri);
    Task SaveLibraryFile(string rootId, string uri, string content);
    Task<string> LoadJamlFile(string rootId, string uri);
    Task SaveJamlFile(string rootId, string uri, string content);
}
