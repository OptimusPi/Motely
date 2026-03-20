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
    /// Analyzes a seed and returns structured data.
    /// Uses MotelySeedContext directly — no search pipeline.
    /// </summary>
    public static MotelySeedAnalysis Analyze(MotelySeedAnalysisConfig cfg)
    {
        try
        {
            MotelySeedContext ctx = new(cfg.Seed, cfg.Deck, cfg.Stake);
            return AnalyzeSeedDirect(ref ctx);
        }
        catch (Exception ex)
        {
            return new MotelySeedAnalysis(ex.ToString(), []);
        }
    }

    private ref struct AnteAnalysisState
    {
        public MotelySingleTarotStream ArcanaStream;
        public readonly bool HasArcanaStream => !ArcanaStream.IsNull;
        public MotelySinglePlanetStream CelestialStream;
        public readonly bool HasCelestialStream => !CelestialStream.IsNull;
        public MotelySingleSpectralStream SpectralStream;
        public readonly bool HasSpectralStream => !SpectralStream.IsNull;
        public MotelySingleStandardCardStream StandardStream;
        public readonly bool HasStandardStream => !StandardStream.IsInvalid;
        public MotelySingleJokerStream BuffoonStream;
        public readonly bool HasBuffoonStream => !BuffoonStream.IsNull;
    }

    private static MotelySeedAnalysis AnalyzeSeedDirect(ref MotelySeedContext ctx)
    {
        MotelyRunState voucherState = new();
        MotelySingleBossStream bossStream = ctx.CreateBossStream();

        // Get starting deck composition for Erratic deck
        var deckStream = ctx.CreateErraticDeckPrngStream(isCached: false);
        var deckCards = new List<string>();

        for (int i = 0; i < 52; i++)
        {
            var card = ctx.GetNextErraticDeckCard(ref deckStream);
            deckCards.Add(FormatCardString(card.PlayingCardRank, card.PlayingCardSuit));
        }

        string startingDeck = string.Join(",", deckCards);

        List<MotelyAnteAnalysis> antes = [];

        for (int ante = 1; ante <= 8; ante++)
        {
            AnteAnalysisState state = new()
            {
                ArcanaStream = default,
                CelestialStream = default,
                SpectralStream = default,
                StandardStream = MotelySingleStandardCardStream.Invalid,
                BuffoonStream = default,
            };

            // Boss
            MotelyBossBlind boss = ctx.GetBossForAnte(ref bossStream, ante, ref voucherState);

            // Voucher
            MotelyVoucher voucher = ctx.GetAnteFirstVoucher(ante, voucherState);
            voucherState.ActivateVoucher(voucher);

            // Tags
            MotelySingleTagStream tagStream = ctx.CreateTagStream(ante);
            MotelyTag smallTag = ctx.GetNextTag(ref tagStream);
            MotelyTag bigTag = ctx.GetNextTag(ref tagStream);

            // Shop Queue
            MotelySingleShopItemStream shopStream = ctx.CreateShopItemStream(ante);
            int maxSlots = ante == 1 ? 15 : 50;
            MotelyItem[] shopItems = new MotelyItem[maxSlots];

            for (int i = 0; i < maxSlots; i++)
            {
                shopItems[i] = ctx.GetNextShopItem(ref shopStream);
            }

            // Packs
            var packStream = ctx.CreateBoosterPackStream(ante);
            int maxPacks = ante == 1 ? 4 : 6;
            MotelyBoosterPackAnalysis[] packs = new MotelyBoosterPackAnalysis[maxPacks];

            for (int i = 0; i < maxPacks; i++)
            {
                MotelyBoosterPack pack = ctx.GetNextBoosterPack(ref packStream);
                MotelySingleItemSet packContent = GetPackContents(
                    ref ctx,
                    ante,
                    pack,
                    ref state
                );

                packs[i] = new(pack, packContent.AsArray());
            }

            antes.Add(new(ante, boss, voucher, smallTag, bigTag, shopItems, packs, null));
        }

        string? deckComposition = ctx.Deck == MotelyDeck.Erratic ? startingDeck : null;
        string? deckBreakdown =
            ctx.Deck == MotelyDeck.Erratic ? GetErraticDeckBreakdown(deckCards) : null;

        return new(null, antes, ctx.Deck, deckComposition, deckBreakdown);
    }

    private static MotelySingleItemSet GetPackContents(
        ref MotelySeedContext ctx,
        int ante,
        MotelyBoosterPack pack,
        ref AnteAnalysisState state
    )
    {
        var packType = pack.GetPackType();
        var packSize = pack.GetPackSize();

        switch (packType)
        {
            case MotelyBoosterPackType.Arcana:
                if (!state.HasArcanaStream)
                    state.ArcanaStream = ctx.CreateArcanaPackTarotStream(ante);
                return ctx.GetNextArcanaPackContents(ref state.ArcanaStream, packSize);

            case MotelyBoosterPackType.Celestial:
                if (!state.HasCelestialStream)
                    state.CelestialStream = ctx.CreateCelestialPackPlanetStream(ante);
                return ctx.GetNextCelestialPackContents(ref state.CelestialStream, packSize);

            case MotelyBoosterPackType.Spectral:
                if (!state.HasSpectralStream)
                    state.SpectralStream = ctx.CreateSpectralPackSpectralStream(ante);
                return ctx.GetNextSpectralPackContents(ref state.SpectralStream, packSize);

            case MotelyBoosterPackType.Buffoon:
                if (!state.HasBuffoonStream)
                    state.BuffoonStream = ctx.CreateBuffoonPackJokerStream(ante);
                return ctx.GetNextBuffoonPackContents(ref state.BuffoonStream, packSize);

            case MotelyBoosterPackType.Standard:
                if (!state.HasStandardStream)
                    state.StandardStream = ctx.CreateStandardPackCardStream(ante);
                return ctx.GetNextStandardPackContents(ref state.StandardStream, packSize);

            default:
                throw new System.ComponentModel.InvalidEnumArgumentException();
        }
    }

    /// <summary>
    /// Analyze a seed and return a formatted DTO ready for JS consumption.
    /// Parses deck/stake strings, runs the analyzer, formats enums to display strings.
    /// </summary>
    public static SeedAnalysisDto AnalyzeToDto(string seed, string deck, string stake)
    {
        if (!Enum.TryParse<MotelyDeck>(deck, true, out var deckEnum))
            throw new ArgumentException($"Unknown deck: '{deck}'", nameof(deck));
        if (!Enum.TryParse<MotelyStake>(stake, true, out var stakeEnum))
            throw new ArgumentException($"Unknown stake: '{stake}'", nameof(stake));

        var analysis = Analyze(new MotelySeedAnalysisConfig(seed, deckEnum, stakeEnum));

        return new SeedAnalysisDto
        {
            Seed = seed,
            Deck = deck,
            Stake = stake,
            Error = analysis.Error,
            ErraticDeckComposition = analysis.ErraticDeckComposition
                ?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                ?? [],
            Antes = analysis.Antes.Select(a => new AnteAnalysisDto
            {
                Ante = a.Ante,
                Boss = FormatUtils.FormatBoss(a.Boss),
                Voucher = FormatUtils.FormatVoucher(a.Voucher),
                SmallBlindTag = FormatUtils.FormatTag(a.SmallBlindTag),
                BigBlindTag = FormatUtils.FormatTag(a.BigBlindTag),
                DrawOrder = a.DrawOrder ?? "",
                ShopQueue = a.ShopQueue
                    .Select(item => new ShopItemDto { Id = item.Type.ToString(), Name = FormatUtils.FormatItem(item) })
                    .ToArray(),
                Packs = a.Packs
                    .Select(p => new PackDto
                    {
                        Type = FormatUtils.FormatPackName(p.Type),
                        Items = p.Items.Select(FormatUtils.FormatItem).ToArray(),
                    })
                    .ToArray(),
            }).ToArray(),
        };
    }

    private static string FormatCardString(
        MotelyPlayingCardRank rank,
        MotelyPlayingCardSuit suit
    )
    {
        var rankStr = rank switch
        {
            MotelyPlayingCardRank.Two => "2",
            MotelyPlayingCardRank.Three => "3",
            MotelyPlayingCardRank.Four => "4",
            MotelyPlayingCardRank.Five => "5",
            MotelyPlayingCardRank.Six => "6",
            MotelyPlayingCardRank.Seven => "7",
            MotelyPlayingCardRank.Eight => "8",
            MotelyPlayingCardRank.Nine => "9",
            MotelyPlayingCardRank.Ten => "10",
            MotelyPlayingCardRank.Jack => "J",
            MotelyPlayingCardRank.Queen => "Q",
            MotelyPlayingCardRank.King => "K",
            MotelyPlayingCardRank.Ace => "A",
            _ => rank.ToString(),
        };
        var suitStr = suit switch
        {
            MotelyPlayingCardSuit.Clubs => "C",
            MotelyPlayingCardSuit.Diamonds => "D",
            MotelyPlayingCardSuit.Hearts => "H",
            MotelyPlayingCardSuit.Spades => "S",
            _ => suit.ToString(),
        };
        return $"{rankStr}_{suitStr}";
    }

    private static string GetErraticDeckBreakdown(List<string> deckCards)
    {
        var rankCounts = new Dictionary<string, int>();
        var suitCounts = new Dictionary<char, int>
        {
            ['C'] = 0,
            ['D'] = 0,
            ['H'] = 0,
            ['S'] = 0,
        };

        foreach (var card in deckCards)
        {
            var parts = card.Split('_');
            if (parts.Length == 2)
            {
                var rank = parts[0];
                var suit = parts[1][0];

                rankCounts[rank] = rankCounts.GetValueOrDefault(rank, 0) + 1;
                if (suitCounts.ContainsKey(suit))
                    suitCounts[suit]++;
            }
        }

        int maxRankCount = rankCounts.Values.Max();
        int maxSuitCount = suitCounts.Values.Max();

        var sb = new StringBuilder();

        string[] rankOrder = ["2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K", "A"];
        sb.AppendLine("Ranks:");
        foreach (var rank in rankOrder)
        {
            int count = rankCounts.GetValueOrDefault(rank, 0);
            string marker = count == maxRankCount && count > 0 ? "*" : "";
            sb.AppendLine($"  {rank, 2}: {count}{marker}");
        }

        sb.AppendLine("Suits:");
        var suitSymbols = new Dictionary<char, string>
        {
            ['C'] = "♣",
            ['D'] = "♦",
            ['H'] = "♥",
            ['S'] = "♠",
        };
        foreach (var (suit, symbol) in suitSymbols)
        {
            int count = suitCounts[suit];
            string marker = count == maxSuitCount && count > 0 ? "*" : "";
            sb.AppendLine($"  {symbol}: {count}{marker}");
        }

        return sb.ToString().TrimEnd();
    }
}
