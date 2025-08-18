using System.Collections.Generic;
using System.Linq;
using Motely.Analysis;

namespace Motely;

/// <summary>
/// Backwards-compat adapter that mimics the old SeedAnalyzerCapture API using MotelySeedAnalyzer.
/// </summary>
public static class SeedAnalyzerCapture
{
    public sealed record class ShopItem(int Slot, MotelyItem Item, string FormattedName);
    public sealed record class PackContent(MotelyBoosterPackType PackType, List<string> Contents);
    public sealed record class AnteData(
        int Ante,
        MotelyVoucher Voucher,
        MotelyBossBlind Boss,
        List<ShopItem> ShopQueue,
        List<PackContent> Packs,
        List<MotelyTag> Tags
    );

    public static List<AnteData> CaptureAnalysis(string seed, MotelyDeck deck, MotelyStake stake)
    {
        var analysis = MotelySeedAnalyzer.Analyze(new MotelySeedAnalysisConfig(seed, deck, stake));
        var list = new List<AnteData>(analysis.Antes.Count);
        foreach (var ante in analysis.Antes)
        {
            var shop = ante.ShopQueue.Select((item, idx) => new ShopItem(idx + 1, item, FormatUtils.FormatItem(item))).ToList();
            var packs = ante.Packs.Select(p => new PackContent(p.Type.GetPackType(), p.Items.Select(FormatUtils.FormatItem).ToList())).ToList();
            var tags = new List<MotelyTag> { ante.SmallBlindTag, ante.BigBlindTag };
            list.Add(new AnteData(ante.Ante, ante.Voucher, ante.Boss, shop, packs, tags));
        }
        return list;
    }
}
