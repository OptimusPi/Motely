using System.Diagnostics;
using System.Text;

namespace Motely.Analysis;

public sealed record class MotelySeedAnalysisConfig(
    string Seed,
    MotelyDeck Deck,
    MotelyStake Stake
);

/// <summary>
/// Contains all analysis data for a seed
/// </summary>
public sealed record class MotelySeedAnalysis(
    string? Error,
    IReadOnlyList<MotelyAnteAnalysis> Antes,
    MotelyDeck? Deck = null,
    string? ErraticDeckComposition = null,
    string? ErraticDeckBreakdown = null
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
            foreach ((int i, MotelyItem item) in ante.ShopQueue.Index())
            {
                sb.AppendLine($"{i + 1}) {FormatUtils.FormatItem(item)}");
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
                                pack.Items.Select(item => FormatUtils.FormatItem(item))
                            )
                        : "";
                sb.AppendLine($"{FormatUtils.FormatPackName(pack.Type)}{contents}");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }
}

public sealed record class MotelyAnteAnalysis(
    int Ante,
    MotelyBossBlind Boss,
    MotelyVoucher Voucher,
    MotelyTag SmallBlindTag,
    MotelyTag BigBlindTag,
    IReadOnlyList<MotelyItem> ShopQueue,
    IReadOnlyList<MotelyBoosterPackAnalysis> Packs,
    string? DrawOrder = null
);

public sealed record class MotelyBoosterPackAnalysis(
    MotelyBoosterPack Type,
    IReadOnlyList<MotelyItem> Items
);

/// <summary>
/// Consolidated seed analyzer that captures seed data and provides various output formats
/// </summary>
public static partial class MotelySeedAnalyzer
{
    /// <summary>
    /// Analyzes a seed and returns structured data
    /// </summary>
    public static MotelySeedAnalysis Analyze(MotelySeedAnalysisConfig cfg)
    {
        try
        {
            MotelyAnalyzerFilterDesc filterDesc = new();

            var searchSettings = new MotelySearchSettings<MotelyAnalyzerFilterDesc.AnalyzerFilter>(
                filterDesc
            )
                .WithDeck(cfg.Deck)
                .WithStake(cfg.Stake)
                .WithListSearch([cfg.Seed]) // Single seed analysis
                .WithThreadCount(1);

            using var search = searchSettings.Start();
            search.Start().Wait();

            Debug.Assert(filterDesc.LastAnalysis != null);

            // Don't write to Console here - the caller should handle output
            // Console.Write(filterDesc.LastAnalysis);

            return filterDesc.LastAnalysis;
        }
        catch (Exception ex)
        {
            return new MotelySeedAnalysis(ex.ToString(), []);
        }
    }
}
