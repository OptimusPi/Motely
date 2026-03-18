using Motely.Analysis;
using Motely.Filters;

namespace Motely;

/// <summary>
/// Shared business logic for all export targets (BrowserWasm, NodeAddon).
/// Export files should be thin wrappers that call these methods.
/// </summary>
public static class MotelyRuntime
{
    // ── Version ──────────────────────────────────────────────────────────────

    public static string GetVersion(System.Reflection.Assembly assembly) =>
        MotelyBuildVersion.For(assembly);

    // ── Capabilities ─────────────────────────────────────────────────────────

    public static bool IsSimdEnabled() =>
        System.Runtime.Intrinsics.Vector128.IsHardwareAccelerated;

    public static string[] GetFeatureList(string runtime, int threadCount)
    {
        var features = new List<string> { "analyzer", "jaml-search", "jaml-validate" };

        if (runtime == "node-addon")
            features.Add("lucky-money-stream");

        if (IsSimdEnabled())
            features.Add("simd");

        features.Add($"{threadCount} threads");
        return features.ToArray();
    }

    // ── Enum Parsing ─────────────────────────────────────────────────────────

    public static void ParseEnums(string deck, string stake,
        out MotelyDeck deckEnum, out MotelyStake stakeEnum)
    {
        if (!Enum.TryParse<MotelyDeck>(deck, true, out deckEnum))
            throw new ArgumentException($"Unknown deck: '{deck}'", nameof(deck));
        if (!Enum.TryParse<MotelyStake>(stake, true, out stakeEnum))
            throw new ArgumentException($"Unknown stake: '{stake}'", nameof(stake));
    }

    // ── Seed Analysis ────────────────────────────────────────────────────────

    /// <summary>
    /// Analyze a seed. Returns the DTO. Throws on invalid input or analysis error.
    /// </summary>
    public static SeedAnalysisDto AnalyzeSeed(string seed, string deck, string stake)
    {
        ParseEnums(deck, stake, out var deckEnum, out var stakeEnum);

        var cfg = new MotelySeedAnalysisConfig(seed, deckEnum, stakeEnum);
        var analysis = MotelySeedAnalyzer.Analyze(cfg);

        if (!string.IsNullOrEmpty(analysis.Error))
            throw new InvalidOperationException(analysis.Error);

        return MapAnalysisToDto(analysis, seed, deckEnum, stakeEnum);
    }

    // ── JAML Validation ──────────────────────────────────────────────────────

    /// <summary>
    /// Validate a JAML filter string. Returns a ValidateResultDto (never throws).
    /// </summary>
    public static ValidateResultDto ValidateJaml(string jamlContent)
    {
        try
        {
            if (!JamlConfigLoader.TryLoad(jamlContent, out var config, out var parseError)
                || config == null)
            {
                return new ValidateResultDto
                {
                    Valid = false,
                    Error = parseError ?? "Failed to parse JAML",
                };
            }

            return new ValidateResultDto
            {
                Valid = true,
                Name = config.Name,
                Deck = config.Deck.ToString(),
                Stake = config.Stake.ToString(),
            };
        }
        catch (Exception ex)
        {
            return new ValidateResultDto { Valid = false, Error = ex.Message };
        }
    }

    // ── Analysis DTO Mapping ─────────────────────────────────────────────────

    public static SeedAnalysisDto MapAnalysisToDto(
        MotelySeedAnalysis analysis,
        string seed,
        MotelyDeck deck,
        MotelyStake stake)
    {
        return new SeedAnalysisDto
        {
            Seed = seed,
            Deck = deck.ToString(),
            Stake = stake.ToString(),
            Error = analysis.Error,
            ErraticDeckComposition =
                analysis.ErraticDeckComposition?.Split(
                    ',',
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
                ) ?? [],
            Antes = analysis
                .Antes.Select(a => new AnteAnalysisDto
                {
                    Ante = a.Ante,
                    Boss = a.Boss.ToString(),
                    Voucher = a.Voucher.ToString(),
                    SmallBlindTag = a.SmallBlindTag.ToString(),
                    BigBlindTag = a.BigBlindTag.ToString(),
                    DrawOrder = a.DrawOrder ?? "",
                    ShopQueue = a
                        .ShopQueue.Select(item => new ShopItemDto
                        {
                            Id = item.Type.ToString(),
                            Name = item.ToString(),
                        })
                        .ToArray(),
                    Packs = a
                        .Packs.Select(p => new PackDto
                        {
                            Type = p.Type.ToString(),
                            Items = p.Items.Select(i => i.ToString()).ToArray(),
                        })
                        .ToArray(),
                })
                .ToArray(),
        };
    }
}
