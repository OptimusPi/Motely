using Motely.Filters;
using Motely.Analysis;

namespace Motely;

/// <summary>Result of <see cref="MotelyWasmHost.ValidateJaml"/>.</summary>
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

