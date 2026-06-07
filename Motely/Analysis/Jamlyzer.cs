using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json.Serialization;
using Motely.Filters.Jaml;

namespace Motely.Analysis;

public sealed record class JamlyzerOptions(
    string Seed,
    MotelyDeck Deck,
    MotelyStake Stake,
    // Optional JAML lens: items matching its should clauses get IsHighlighted/MatchedBy (glow).
    JamlConfig? Lens = null
);

/// <summary>
/// Classic String Block Format Analysis ("The Soul" layout).
///
/// This is the LEGACY TEXT analyzer: its <see cref="ToString"/> is a flat, human-readable
/// block intended for unit-test ground-truth (Verify()) and cross-tool comparison against
/// external Balatro seed tools (miaklwalker, mathisfun_), NOT for UI rendering.
/// </summary>
public sealed record class JamlyzerSnapshot(
    string? Error,
    IReadOnlyList<AnteSnapshot> Antes,
    MotelyDeck? Deck = null,
    string? ErraticDeckComposition = null,
    string? ErraticDeckBreakdown = null,
    // The focused view: a flat list of EXACTLY what the JAML (must + should) matched on this seed,
    // scoped exactly as written — straight from the scorer. This is the "swipe card": render this,
    // not the whole board. Lean by design — packed MotelyItem + clause index + location ints; the
    // frontend decodes the item and maps the index to its own clause label. Null when no lens.
    IReadOnlyList<ScoopedMatch>? Matches = null
)
{
    public override string ToString()
    {
        if (!string.IsNullOrEmpty(Error))
        {
            return $"❌ Error analyzing seed: {Error}";
        }

        StringBuilder sb = new();

        // Add erratic deck composition at the top if available (Erratic deck only)
        if (!string.IsNullOrEmpty(ErraticDeckComposition))
        {
            sb.AppendLine($"Erratic Deck Composition: {ErraticDeckComposition}");
            if (!string.IsNullOrEmpty(ErraticDeckBreakdown))
            {
                sb.AppendLine(ErraticDeckBreakdown);
            }
            sb.AppendLine();
        }

        // Match TheSoul's format exactly
        foreach (var ante in Antes)
        {
            sb.AppendLine($"==ANTE {ante.Ante}==");

            // Add draw order for this ante (for all decks)
            if (!string.IsNullOrEmpty(ante.DrawOrder))
            {
                sb.AppendLine($"Draw: {ante.DrawOrder}");
            }

            sb.AppendLine($"Boss: {FormatUtils.FormatBoss(ante.Boss)}");
            sb.AppendLine($"Voucher: {FormatUtils.FormatVoucher(ante.Voucher)}");

            // Tags
            sb.AppendLine(
                $"Tags: {FormatUtils.FormatTag(ante.SmallBlindTag)}, {FormatUtils.FormatTag(ante.BigBlindTag)}"
            );

            // Shop Queue - match TheSoul format exactly: "Shop Queue: " on its own line, then numbered items
            sb.AppendLine("Shop Queue: ");
            foreach ((int i, SnapshotItem item) in ante.ShopQueue.Index())
            {
                sb.AppendLine($"{i + 1}) {FormatUtils.FormatItem(item.Item)}");
            }
            sb.AppendLine();

            // Packs - match Immolate format exactly: "Pack Name - Card1, Card2, Card3"
            sb.AppendLine("Packs: ");
            foreach (var pack in ante.Packs)
            {
                // Format: "Pack Name - Card1, Card2, Card3"
                var contents =
                    pack.Items.Count > 0
                        ? " - "
                            + string.Join(
                                ", ",
                                pack.Items.Select(item => FormatUtils.FormatItem(item.Item))
                            )
                        : "";
                sb.AppendLine($"{FormatUtils.FormatPackName(pack.Type)}{contents}");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }
}

public sealed record class AnteSnapshot(
    int Ante,
    MotelyBossBlind Boss,
    MotelyVoucher Voucher,
    MotelyTag SmallBlindTag,
    MotelyTag BigBlindTag,
    IReadOnlyList<SnapshotItem> ShopQueue,
    IReadOnlyList<PackSnapshot> Packs,
    string? DrawOrder = null,
    bool BossMatched = false,
    bool VoucherMatched = false,
    bool SmallBlindTagMatched = false,
    bool BigBlindTagMatched = false,
    // Optional: jokers granted by tag effects (not rendered in ToString)
    SnapshotItem? SmallBlindTagGrantedJoker = null,
    SnapshotItem? BigBlindTagGrantedJoker = null
);

public sealed record class SnapshotItem(
    [property: JsonIgnore] MotelyItem Item,
    // Snapshot model (JAMLyzer): JAML is the lens, not the filter.
    // IsHighlighted = glow; MatchedBy = why it glows (tooltip + tally index label).
    bool IsHighlighted = false,
    string? MatchedBy = null
)
{
    public string Name => FormatUtils.FormatItem(Item);
    public int Value => Item.Value;
    public MotelyItemType Type => Item.Type;
    public MotelyItemTypeCategory TypeCategory => Item.TypeCategory;
    public MotelyItemSeal Seal => Item.Seal;
    public MotelyItemEnhancement Enhancement => Item.Enhancement;
    public MotelyItemEdition Edition => Item.Edition;
    public MotelyStandardcardSuit StandardcardSuit => Item.StandardcardSuit;
    public MotelyStandardcardRank StandardcardRank => Item.StandardcardRank;
    public bool IsPerishable => Item.IsPerishable;
    public bool IsEternal => Item.IsEternal;
    public bool IsRental => Item.IsRental;
    public bool IsInvalid => Item.IsInvalid;

    public static implicit operator MotelyItem(SnapshotItem item) => item.Item;
}

public sealed record class PackSnapshot(
    MotelyBoosterPack Type,
    IReadOnlyList<SnapshotItem> Items,
    // Optional: legendary joker granted by The Soul in this pack (not rendered in ToString)
    SnapshotItem? GrantedLegendaryJoker = null
);

/// <summary>
/// Legacy text-block seed analyzer. Produces the classic "The Soul" string layout via
/// <see cref="JamlyzerSnapshot.ToString"/>, intended for unit-test ground-truth and
/// cross-tool comparison (miaklwalker, mathisfun_) — NOT for UI.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static partial class Jamlyzer
{
    /// <summary>
    /// Analyzes a seed and returns structured data
    /// </summary>
    public static JamlyzerSnapshot Analyze(JamlyzerOptions cfg)
    {
        try
        {
            JamlyzerFilterDesc filterDesc = new() { Lens = cfg.Lens };

            var searchSettings = new MotelySearchSettings<JamlyzerFilterDesc.JamlyzerFilter>(
                filterDesc
            )
                .WithDeck(cfg.Deck)
                .WithStake(cfg.Stake)
                .WithListSearch([cfg.Seed]) // Single seed analysis
                .WithThreadCount(1);

            using var search = searchSettings.CreateSearch();
            search.RunSearchUntilCompletion();

            Debug.Assert(filterDesc.LastAnalysis != null);

            // Don't write to Console here - the caller should handle output
            // Console.Write(filterDesc.LastAnalysis);

            return filterDesc.LastAnalysis;
        }
        catch (Exception ex)
        {
            return new JamlyzerSnapshot(ex.ToString(), []);
        }
    }

    /// <summary>
    /// Lens overload: analyze <paramref name="seed"/> and glow items matched by the JAML
    /// <paramref name="lens"/>'s should clauses (deck/stake are taken from the lens config).
    /// </summary>
    public static JamlyzerSnapshot Analyze(string seed, JamlConfig lens) =>
        Analyze(new JamlyzerOptions(seed, lens.Deck, lens.Stake, lens));
}
