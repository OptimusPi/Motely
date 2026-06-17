using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Motely.Filters.Jaml;

/// <summary>
/// JAML-powered seed analyzer. One parameter (<see cref="JamlConfig"/>), two modes:
/// <list type="bullet">
/// <item><see cref="CreateScoreProvider"/> for the SIMD search path</item>
/// <item><see cref="Analyze"/> for single-seed introspection (glow / peek / scoop)</item>
/// </list>
/// Both use the same clause set — no parallel logic, no drift.
/// </summary>
public sealed class JamlSeedAnalyzer
{
    private readonly JamlConfig _config;

    public JamlSeedAnalyzer(JamlConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>Create a score provider for the vector search path.</summary>
    public IMotelySeedScoreProvider CreateScoreProvider()
    {
        return new JamlShouldScoreDesc.JamlShouldScoreProvider(
            _config.Must.ToArray(),
            _config.Should.ToArray(),
            null,
            0
        );
    }

    /// <summary>
    /// Analyze a single seed against the JAML config. Returns string-based match details
    /// (item names, locations, clause indices) — no engine enums cross the boundary.
    /// </summary>
    public JamlAnalysisResult Analyze(string seed, MotelyDeck deck, MotelyStake stake)
    {
        var desc = new JamlAnalyzerFilterDesc(_config);
        var settings = new MotelySearchSettings<JamlAnalyzerFilterDesc.JamlAnalyzerFilter>(desc)
            .WithDeck(deck)
            .WithStake(stake)
            .WithListSearch([seed])
            .WithThreadCount(1);

        using var search = settings.CreateSearch();
        search.RunSearchUntilCompletion();

        return desc.LastResult
            ?? new JamlAnalysisResult(seed, [], [], [], []);
    }
}

// ── Result types (all strings, no engine enums) ───────────────────────────────

public sealed record class JamlAnalysisResult(
    string Seed,
    IReadOnlyList<JamlMatch> MustMatches,
    IReadOnlyList<JamlMatch> ShouldMatches,
    IReadOnlyList<JamlMatch> MustNotMatches,
    IReadOnlyList<JamlAntePeek> Peek
);

public sealed record class JamlMatch(
    int ClauseIndex,
    string? ClauseLabel,
    string ItemName,
    string Source,
    int Ante,
    int Slot,
    int Score
);

public sealed record class JamlAntePeek(
    int Ante,
    string? Boss,
    string? Voucher,
    string? SmallBlindTag,
    string? BigBlindTag,
    IReadOnlyList<JamlPeekItem> ShopItems,
    IReadOnlyList<JamlPeekPack> Packs
);

public sealed record class JamlPeekItem(
    int Slot,
    string Name,
    bool IsHighlighted
);

public sealed record class JamlPeekPack(
    string Type,
    IReadOnlyList<JamlPeekItem> Cards
);

// ── Filter descriptor ───────────────────────────────────────────────────────

public sealed class JamlAnalyzerFilterDesc : IMotelySeedFilterDesc<JamlAnalyzerFilterDesc.JamlAnalyzerFilter>
{
    private readonly JamlConfig _config;
    public JamlAnalysisResult? LastResult { get; private set; }

    public JamlAnalyzerFilterDesc(JamlConfig config) => _config = config;

    public JamlAnalyzerFilter CreateFilter(ref MotelyFilterCreationContext ctx) => new(this);

    public readonly struct JamlAnalyzerFilter(JamlAnalyzerFilterDesc desc) : IMotelySeedFilter
    {
        public JamlAnalyzerFilterDesc FilterDesc { get; } = desc;

        public readonly VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            return ctx.SearchIndividualSeeds(CheckSeed);
        }

        private readonly bool CheckSeed(ref MotelySingleSearchContext singleCtx)
        {
            var config = FilterDesc._config;
            var mustMatches = new List<JamlMatch>();
            var shouldMatches = new List<JamlMatch>();
            var mustNotMatches = new List<JamlMatch>();

            var runState = new MotelyRunState();
            JamlScoring.PrepareRunState(ref singleCtx, CombineForPrepareRunState(config), ref runState);

            // ── Must clauses ──
            for (int i = 0; i < config.Must.Count; i++)
            {
                var clause = config.Must[i];
                var scoop = new JamlScoop();
                runState.ScoopSink = scoop;
                scoop.CurrentClauseIndex = i;

                int raw = JamlScoring.CountRawOccurrences(ref singleCtx, clause, ref runState);
                if (raw >= clause.Min)
                {
                    foreach (var m in scoop.Matches)
                        mustMatches.Add(ConvertMatch(m, clause.Label, "must"));
                }
                runState.ScoopSink = null;
            }

            // ── Should clauses ──
            for (int i = 0; i < config.Should.Count; i++)
            {
                var clause = config.Should[i];
                var scoop = new JamlScoop();
                runState.ScoopSink = scoop;
                scoop.CurrentClauseIndex = i;

                int raw = JamlScoring.CountRawOccurrences(ref singleCtx, clause, ref runState);
                if (raw > 0)
                {
                    foreach (var m in scoop.Matches)
                        shouldMatches.Add(ConvertMatch(m, clause.Label, "should"));
                }
                runState.ScoopSink = null;
            }

            // ── MustNot clauses ──
            for (int i = 0; i < config.MustNot.Count; i++)
            {
                var clause = config.MustNot[i];
                var scoop = new JamlScoop();
                runState.ScoopSink = scoop;
                scoop.CurrentClauseIndex = i;

                int raw = JamlScoring.CountRawOccurrences(ref singleCtx, clause, ref runState);
                if (raw > 0)
                {
                    foreach (var m in scoop.Matches)
                        mustNotMatches.Add(ConvertMatch(m, clause.Label, "mustNot"));
                }
                runState.ScoopSink = null;
            }

            // ── Peek view: only the antes the filter cares about ──
            var peek = BuildPeek(ref singleCtx, config, runState);

            FilterDesc.LastResult = new JamlAnalysisResult(
                singleCtx.GetSeed(),
                mustMatches,
                shouldMatches,
                mustNotMatches,
                peek
            );

            return true; // Analysis is not a filter — always pass
        }

        private static JamlMatch ConvertMatch(ScoopedMatch m, string? label, string source)
        {
            string sourceStr = m.Source switch
            {
                MotelyMatchSource.Shop => "shop",
                MotelyMatchSource.BoosterPack => "pack",
                MotelyMatchSource.Tag => "tag",
                MotelyMatchSource.Voucher => "voucher",
                MotelyMatchSource.Boss => "boss",
                MotelyMatchSource.SoulJoker => "soul",
                MotelyMatchSource.TagJoker => "tag-joker",
                MotelyMatchSource.Consumable => "consumable",
                _ => "unknown",
            };

            return new JamlMatch(
                m.ClauseIndex,
                label,
                FormatUtils.FormatItem(m.Item),
                sourceStr,
                m.Ante,
                m.Slot,
                m.Score
            );
        }

        private static IJamlClause[] CombineForPrepareRunState(JamlConfig config)
        {
            if (config.Must.Count == 0 && config.Should.Count == 0)
                return config.MustNot.ToArray();
            if (config.Should.Count == 0 && config.MustNot.Count == 0)
                return config.Must.ToArray();
            if (config.Must.Count == 0 && config.MustNot.Count == 0)
                return config.Should.ToArray();

            var combined = new List<IJamlClause>();
            combined.AddRange(config.Must);
            combined.AddRange(config.Should);
            combined.AddRange(config.MustNot);
            return combined.ToArray();
        }

        private static IReadOnlyList<JamlAntePeek> BuildPeek(
            ref MotelySingleSearchContext ctx,
            JamlConfig config,
            MotelyRunState runState
        )
        {
            var antes = new List<JamlAntePeek>();

            // Collect all unique antes mentioned by any clause
            var allAntes = new HashSet<int>();
            foreach (var c in config.Must) allAntes.UnionWith(c.Antes);
            foreach (var c in config.Should) allAntes.UnionWith(c.Antes);
            foreach (var c in config.MustNot) allAntes.UnionWith(c.Antes);

            if (allAntes.Count == 0)
                allAntes.Add(1); // Default to ante 1 if no antes specified

            var sortedAntes = allAntes.OrderBy(a => a).ToArray();
            var maxAnte = sortedAntes.Length > 0 ? sortedAntes[^1] : 1;

            // Pre-cache bosses if needed
            if (maxAnte > 0 && runState.CachedBosses == null)
            {
                runState.CachedBosses = new MotelyBossBlind[maxAnte + 1];
                var bossStream = ctx.CreateBossStream();
                var bossState = new MotelyRunState();
                for (int a = 1; a <= maxAnte; a++)
                    runState.CachedBosses[a] = ctx.GetBossForAnte(ref bossStream, a, ref bossState);
            }

            // Pre-activate vouchers for all relevant antes
            int maxVoucherAnte = maxAnte < 8 ? maxAnte + 1 : maxAnte;
            for (int a = 1; a <= maxVoucherAnte; a++)
            {
                var voucher = ctx.GetAnteFirstVoucher(a, runState);
                runState.ActivateVoucher(voucher);
                if (voucher is MotelyVoucher.Hieroglyph or MotelyVoucher.Petroglyph)
                {
                    runState.ActivateExtendedPackAnte(a - 1);
                    var voucherStream = ctx.CreateVoucherStream(a);
                    var bonusVoucher = ctx.GetNextVoucher(ref voucherStream, runState);
                    runState.ActivateVoucher(bonusVoucher);
                }
            }

            foreach (int ante in sortedAntes)
            {
                var boss = runState.CachedBosses != null && ante < runState.CachedBosses.Length
                    ? runState.CachedBosses[ante]
                    : MotelyBossBlind.None;

                var voucher = ctx.GetAnteFirstVoucher(ante, runState);

                var tagStream = ctx.CreateTagStream(ante);
                var smallTag = ctx.GetNextTag(ref tagStream);
                var bigTag = ctx.GetNextTag(ref tagStream);

                var shopItems = new List<JamlPeekItem>();
                var packs = new List<JamlPeekPack>();

                // Materialize shop (first 10 slots)
                var shopStream = ctx.CreateShopItemStream(ante, runState);
                for (int slot = 0; slot < 10; slot++)
                {
                    var item = ctx.GetNextShopItem(ref shopStream);
                    shopItems.Add(new JamlPeekItem(
                        slot,
                        FormatUtils.FormatItem(item),
                        false // TODO: check if highlighted by any clause
                    ));
                }

                // Materialize packs (first 2 packs for ante 1, 3 for others)
                int maxPacks = ante == 1 ? 2 : 3;
                var packStream = ctx.CreateBoosterPackStream(ante);
                var state = new PeekPackState();
                state.ArcanaStream = ctx.CreateArcanaPackTarotStream(ante);
                state.CelestialStream = ctx.CreateCelestialPackPlanetStream(ante);
                state.SpectralStream = ctx.CreateSpectralPackSpectralStream(ante);
                state.BuffoonStream = ctx.CreateBuffoonPackJokerStream(ante);
                state.StandardStream = ctx.CreateStandardPackCardStream(ante);
                for (int p = 0; p < maxPacks; p++)
                {
                    var pack = ctx.GetNextBoosterPack(ref packStream);
                    var packContents = GetPackContents(ref ctx, ante, pack, ref state);
                    var cards = packContents
                        .AsArray()
                        .Select((item, idx) => new JamlPeekItem(idx, FormatUtils.FormatItem(item), false))
                        .ToArray();

                    packs.Add(new JamlPeekPack(pack.ToString(), cards));
                }

                antes.Add(new JamlAntePeek(
                    ante,
                    boss.ToString(),
                    voucher.ToString(),
                    smallTag.ToString(),
                    bigTag.ToString(),
                    shopItems,
                    packs
                ));
            }

            return antes;
        }

        private static MotelySingleItemSet GetPackContents(
            ref MotelySingleSearchContext ctx,
            int ante,
            MotelyBoosterPack pack,
            ref PeekPackState state
        )
        {
            var packType = pack.GetPackType();
            var packSize = pack.GetPackSize();

            return packType switch
            {
                MotelyBoosterPackType.Arcana =>
                    ctx.GetNextArcanaPackContents(ref state.ArcanaStream, packSize),
                MotelyBoosterPackType.Celestial =>
                    ctx.GetNextCelestialPackContents(ref state.CelestialStream, packSize),
                MotelyBoosterPackType.Spectral =>
                    ctx.GetNextSpectralPackContents(ref state.SpectralStream, packSize),
                MotelyBoosterPackType.Buffoon =>
                    ctx.GetNextBuffoonPackContents(ref state.BuffoonStream, packSize),
                MotelyBoosterPackType.Standard =>
                    ctx.GetNextStandardPackContents(ref state.StandardStream, packSize),
                _ => throw new InvalidOperationException($"Unknown pack type: {packType}")
            };
        }

        private ref struct PeekPackState
        {
            public MotelySingleTarotStream ArcanaStream;
            public MotelySinglePlanetStream CelestialStream;
            public MotelySingleSpectralStream SpectralStream;
            public MotelySingleJokerStream BuffoonStream;
            public MotelySingleStandardCardStream StandardStream;
        }
    }
}
