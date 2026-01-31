using Motely;
using Motely.Filters;
using YamlDotNet.Serialization;

namespace MotelyJaml;

/// <summary>
/// AOT-compatible JAML serialization context for Motely filter types.
/// The YamlDotNet.Analyzers.StaticGenerator will generate static serialization code for these types.
/// Uses a separate namespace to avoid conflicts with Motely project name.
/// 
/// IMPORTANT: 
/// - Only register concrete classes, not generic collections (generator doesn't support them).
/// - The generator infers List&lt;T&gt; and T[] from property types when T is registered.
/// - All enum types used as property types MUST be registered.
/// - Value types (int, string, bool, DateTime) are handled automatically, but arrays need element type registered.
/// - Specialized filter clause types (MotelyJsonJokerFilterClause, etc.) are created
///   from MotelyJsonConfig.MotelyJsonFilterClause during processing and have init-only
///   properties that the static generator cannot handle - DO NOT register them.
/// </summary>
[YamlStaticContext]
// Core configuration classes
[YamlSerializable(typeof(MotelyJsonConfig))]
[YamlSerializable(typeof(MotelyJsonConfig.MotelyJsonFilterClause))]
[YamlSerializable(typeof(MotelyFilterDefaults))]
[YamlSerializable(typeof(SourcesConfig))]

// Enum types used in MotelyJsonConfig and MotelyJsonFilterClause
[YamlSerializable(typeof(MotelyFilterItemType))]
[YamlSerializable(typeof(MotelyJsonConfigWildcards))]
[YamlSerializable(typeof(MotelyScoreAggregationMode))]
[YamlSerializable(typeof(MotelyJoker))]
[YamlSerializable(typeof(MotelyVoucher))]
[YamlSerializable(typeof(MotelyTarotCard))]
[YamlSerializable(typeof(MotelyPlanetCard))]
[YamlSerializable(typeof(MotelySpectralCard))]
[YamlSerializable(typeof(MotelyTag))]
[YamlSerializable(typeof(MotelyTagType))]
[YamlSerializable(typeof(MotelyBossBlind))]
[YamlSerializable(typeof(MotelyEventType))]
[YamlSerializable(typeof(MotelyItemEdition))]
[YamlSerializable(typeof(MotelyJokerSticker))]
[YamlSerializable(typeof(MotelyPlayingCardSuit))]
[YamlSerializable(typeof(MotelyPlayingCardRank))]
[YamlSerializable(typeof(MotelyItemSeal))]
[YamlSerializable(typeof(MotelyItemEnhancement))]

// Additional enum types that may be referenced indirectly
[YamlSerializable(typeof(ScoreCutoffMode))]
[YamlSerializable(typeof(FilterCategory))]

// Value types - primitives are handled automatically, but arrays need element type registered
// int, string, bool, DateTime are handled automatically
// int[] and string[] are inferred from int and string registrations
// List<int> is inferred from int (primitive type) - no need to register generic types
public partial class MotelyJamlStaticContext : StaticContext
{
}
