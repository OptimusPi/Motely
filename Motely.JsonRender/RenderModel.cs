using Motely.Analysis;
using Motely.Enums;

namespace Motely.JsonRender;

// Interchange DTOs for the JSON document and the HTML report. They mirror the
// MotelyJamlyzer result records field-for-field so the JSON is a faithful, stable
// contract for other consumers (e.g. jaml-ui) — the only reshape is MotelyItem,
// which serializes as a readable object instead of its raw packed int.

public sealed record RenderReport(
    RenderFilter Filter,
    MotelyDeck Deck,
    MotelyStake Stake,
    int EventRolls,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<RenderSeed> Seeds
);

public sealed record RenderFilter(string Id, string? Name, string? Description, string? Author);

public sealed record RenderSeed(
    string Seed,
    int Score,
    IReadOnlyList<RenderAnte> Antes,
    MotelyJamlyzerEvents Events,
    MotelyJamlyzerStreamStates StreamStates,
    IReadOnlyList<RenderItem>? ErraticDeck
);

public sealed record RenderAnte(
    int Ante,
    MotelyBossBlind Boss,
    MotelyVoucher Voucher,
    MotelyTag SmallBlindTag,
    MotelyTag BigBlindTag,
    IReadOnlyList<RenderItem> ShopItems,
    IReadOnlyList<RenderPack> Packs,
    RenderPulls Pulls,
    RenderShopStreams ShopStreams
);

public sealed record RenderPack(MotelyBoosterPack Pack, IReadOnlyList<RenderItem> Items);

/// <summary>
/// One MotelyItem as a readable object: canonical display <paramref name="Name"/> plus every
/// facet of the packed int broken out. <paramref name="Suit"/>/<paramref name="Rank"/> are set
/// only for standard cards and stay null (omitted from JSON) for everything else.
/// </summary>
public sealed record RenderItem(
    string Name,
    MotelyItemType Type,
    MotelyItemTypeCategory TypeCategory,
    MotelyItemEdition Edition,
    MotelyItemEnhancement Enhancement,
    MotelyItemSeal Seal,
    IReadOnlyList<string> Stickers,
    string? Suit,
    string? Rank
);

public sealed record RenderPulls(
    IReadOnlyList<RenderItem> JudgementJokers,
    IReadOnlyList<RenderItem> WraithJokers,
    IReadOnlyList<RenderItem> EmperorTarots,
    IReadOnlyList<RenderItem> PurpleSealTarots,
    IReadOnlyList<RenderItem> SixthSenseSpectrals,
    IReadOnlyList<RenderItem> SeanceSpectrals,
    IReadOnlyList<RenderItem> RiffRaffJokers,
    IReadOnlyList<RenderItem> RareTagJokers,
    IReadOnlyList<RenderItem> UncommonTagJokers,
    IReadOnlyList<RenderItem> LegendaryJokers,
    IReadOnlyList<MotelyVoucher> VoucherSequence
);

public sealed record RenderShopStreams(
    IReadOnlyList<RenderItem> ShopJokers,
    IReadOnlyList<RenderItem> CommonShopJokers,
    IReadOnlyList<RenderItem> UncommonShopJokers,
    IReadOnlyList<RenderItem> RareShopJokers,
    IReadOnlyList<RenderItem> ShopTarots,
    IReadOnlyList<RenderItem> ShopPlanets,
    IReadOnlyList<RenderItem> ShopSpectrals
);
