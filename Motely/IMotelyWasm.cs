using Motely.Filters;

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

public interface IMotelyWasm
{
    string GetVersion();
    /// <summary>
    /// Returns the JAML JSON Schema as a JSON string. Generated at runtime from the
    /// same typed DTO graph the parser consumes — guaranteed in lockstep with this
    /// version's parsing rules. Replaces the separately-versioned `jaml-schema` npm
    /// package: consumers can do <c>const schema = JSON.parse(MotelyWasm.getJamlSchema())</c>
    /// and feed it directly to Monaco/Ajv/etc.
    /// </summary>
    string GetJamlSchema();
    MotelyItemLayout GetItemLayout();
    /// <summary>Structured validation — use instead of the legacy plain-string overload.</summary>
    JamlValidationResult ValidateJamlStructured(string jaml);
    /// <summary>Legacy plain-string validation ("valid" or error message). Kept for back-compat.</summary>
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
    /// <summary>Cheap structural summary without running a search. Safe to call on every keystroke.</summary>
    JamlMetaResult GetJamlMeta(string jaml);
}
