using System.Linq;

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
        if (_config.Must.Count == 0 && _config.Should.Count == 0)
            throw new InvalidOperationException(
                "JamlSeedAnalyzer.CreateScoreProvider requires at least one must or should clause. " +
                "Configs with only mustNot clauses cannot produce a score provider.");

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
            ?? new JamlAnalysisResult(seed, [], [], [], [], null);
    }
}

// ── Result types (all strings, no engine enums) ───────────────────────────────

public sealed record class JamlAnalysisResult(
    string Seed,
    IReadOnlyList<JamlMatch> MustMatches,
    IReadOnlyList<JamlMatch> ShouldMatches,
    IReadOnlyList<JamlMatch> MustNotMatches,
    IReadOnlyList<JamlAntePeek> Peek,
    IReadOnlyList<string>? ErraticDeckComposition
);

public sealed record class JamlMatch(
    int ClauseIndex,
    string? ClauseLabel,
    string Group,
    string ItemName,
    string Source,
    int Ante,
    int Slot,
    int Count,
    int Points
);

public sealed record class JamlAntePeek(
    int Ante,
    string? Boss,
    string? Voucher,
    string? SmallBlindTag,
    string? BigBlindTag,
    string? SmallTagJoker,
    string? BigTagJoker,
    IReadOnlyList<JamlPeekItem> ShopItems,
    IReadOnlyList<JamlPeekPack> Packs,
    IReadOnlyList<JamlPeekEvent> Events,
    IReadOnlyList<string> DrawOrder
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

public sealed record class JamlPeekEvent(
    string Type,
    int Roll,
    bool Triggered,
    string? Value
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

            // ── Erratic deck composition (seed-level, not per-ante) ──
            IReadOnlyList<string>? erraticDeck = null;
            if (singleCtx.Deck == MotelyDeck.Erratic)
            {
                var erraticCards = new List<string>(52);
                var erraticStream = singleCtx.CreateErraticDeckPrngStream();
                for (int i = 0; i < 52; i++)
                {
                    var card = singleCtx.GetNextErraticDeckCard(ref erraticStream);
                    erraticCards.Add(FormatCardString(card.StandardcardRank, card.StandardcardSuit));
                }
                erraticDeck = erraticCards;
            }

            FilterDesc.LastResult = new JamlAnalysisResult(
                singleCtx.GetSeed(),
                mustMatches,
                shouldMatches,
                mustNotMatches,
                peek,
                erraticDeck
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

            // Event streams are seed-global; compute once and attach to every ante peek
            var events = MaterializeAllEvents(ref ctx, config);

            // Draw order is the deck composition (seed-global for Erratic, static for standard decks)
            var drawOrder = BuildDrawOrder(ref ctx);

            foreach (int ante in sortedAntes)
            {
                var boss = runState.CachedBosses != null && ante < runState.CachedBosses.Length
                    ? runState.CachedBosses[ante]
                    : MotelyBossBlind.None;

                var voucher = ctx.GetAnteFirstVoucher(ante, runState);

                var tagStream = ctx.CreateTagStream(ante);
                var smallTag = ctx.GetNextTag(ref tagStream);
                var bigTag = ctx.GetNextTag(ref tagStream);

                var smallTagJoker = GetTagJoker(ref ctx, ante, smallTag);
                var bigTagJoker = GetTagJoker(ref ctx, ante, bigTag);

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
                        false // TODO: cross-reference against ScoopedMatches to determine highlight
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
                    smallTagJoker,
                    bigTagJoker,
                    shopItems,
                    packs,
                    events,
                    drawOrder
                ));
            }

            return antes;
        }

        private static string? GetTagJoker(ref MotelySingleSearchContext ctx, int ante, MotelyTag tag)
        {
            if (IsRareTag(tag))
            {
                var stream = ctx.CreateRareTagJokerStream(ante);
                return FormatUtils.FormatItem(ctx.GetNextJoker(ref stream));
            }
            if (IsUncommonTag(tag))
            {
                var stream = ctx.CreateUncommonTagJokerStream(ante);
                return FormatUtils.FormatItem(ctx.GetNextJoker(ref stream));
            }
            return null;
        }

        private static bool IsRareTag(MotelyTag tag) => tag == MotelyTag.RareTag;
        private static bool IsUncommonTag(MotelyTag tag) => tag == MotelyTag.UncommonTag;

        private static string FormatCardString(MotelyStandardcardRank rank, MotelyStandardcardSuit suit)
        {
            string rankStr = rank switch
            {
                MotelyStandardcardRank.Two => "2",
                MotelyStandardcardRank.Three => "3",
                MotelyStandardcardRank.Four => "4",
                MotelyStandardcardRank.Five => "5",
                MotelyStandardcardRank.Six => "6",
                MotelyStandardcardRank.Seven => "7",
                MotelyStandardcardRank.Eight => "8",
                MotelyStandardcardRank.Nine => "9",
                MotelyStandardcardRank.Ten => "10",
                MotelyStandardcardRank.Jack => "J",
                MotelyStandardcardRank.Queen => "Q",
                MotelyStandardcardRank.King => "K",
                MotelyStandardcardRank.Ace => "A",
                _ => rank.ToString(),
            };

            string suitStr = suit switch
            {
                MotelyStandardcardSuit.Clubs => "C",
                MotelyStandardcardSuit.Diamonds => "D",
                MotelyStandardcardSuit.Hearts => "H",
                MotelyStandardcardSuit.Spades => "S",
                _ => suit.ToString(),
            };

            return $"{rankStr}_{suitStr}";
        }

        private static IReadOnlyList<JamlPeekEvent> MaterializeAllEvents(
            ref MotelySingleSearchContext ctx,
            JamlConfig config
        )
        {
            var events = new List<JamlPeekEvent>();

            foreach (var clause in config.Must.OfType<RollClause>())
                events.AddRange(MaterializeEvents(ref ctx, clause));
            foreach (var clause in config.Should.OfType<RollClause>())
                events.AddRange(MaterializeEvents(ref ctx, clause));
            foreach (var clause in config.MustNot.OfType<RollClause>())
                events.AddRange(MaterializeEvents(ref ctx, clause));

            return events;
        }

        private static IReadOnlyList<JamlPeekEvent> MaterializeEvents(
            ref MotelySingleSearchContext ctx,
            RollClause clause
        )
        {
            var results = new List<JamlPeekEvent>();
            double luck = clause.Luck;

            switch (clause)
            {
                case LuckyMoneyClause:
                {
                    var stream = ctx.CreateLuckyCardMoneyStream(isCached: false);
                    foreach (var roll in clause.Rolls)
                    {
                        for (int i = 0; i < roll; i++)
                            ctx.GetNextLuckyMoney(ref stream, luck);
                        bool triggered = ctx.GetNextLuckyMoney(ref stream, luck);
                        results.Add(new JamlPeekEvent("luckyMoney", roll, triggered, null));
                    }
                    break;
                }
                case LuckyMultClause:
                {
                    var stream = ctx.CreateLuckyCardMultStream(isCached: false);
                    foreach (var roll in clause.Rolls)
                    {
                        for (int i = 0; i < roll; i++)
                            ctx.GetNextLuckyMult(ref stream, luck);
                        bool triggered = ctx.GetNextLuckyMult(ref stream, luck);
                        results.Add(new JamlPeekEvent("luckyMult", roll, triggered, null));
                    }
                    break;
                }
                case MisprintMultClause:
                {
                    var stream = ctx.CreateMisprintPrngStream();
                    foreach (var roll in clause.Rolls)
                    {
                        for (int i = 0; i < roll; i++)
                            ctx.GetNextMisprintMult(ref stream);
                        int value = ctx.GetNextMisprintMult(ref stream);
                        results.Add(new JamlPeekEvent("misprintMult", roll, true, value.ToString()));
                    }
                    break;
                }
                case WheelOfFortuneClause:
                {
                    var stream = ctx.CreateWheelOfFortuneStream();
                    foreach (var roll in clause.Rolls)
                    {
                        for (int i = 0; i < roll; i++)
                            ctx.GetNextWheelOfFortune(ref stream, luck);
                        var edition = ctx.GetNextWheelOfFortune(ref stream, luck);
                        bool triggered = edition != MotelyItemEdition.None;
                        results.Add(new JamlPeekEvent("wheelOfFortune", roll, triggered, edition.ToString()));
                    }
                    break;
                }
                case CavendishExtinctClause:
                {
                    var stream = ctx.CreateCavendishPrngStream(false);
                    foreach (var roll in clause.Rolls)
                    {
                        for (int i = 0; i < roll; i++)
                            ctx.GetNextCavendishExtinct(ref stream, luck);
                        bool triggered = ctx.GetNextCavendishExtinct(ref stream, luck);
                        results.Add(new JamlPeekEvent("cavendishExtinct", roll, triggered, null));
                    }
                    break;
                }
                case GrosMichelExtinctClause:
                {
                    var stream = ctx.CreateGrosMichelPrngStream(false);
                    foreach (var roll in clause.Rolls)
                    {
                        for (int i = 0; i < roll; i++)
                            ctx.GetNextGrosMichelExtinct(ref stream, luck);
                        bool triggered = ctx.GetNextGrosMichelExtinct(ref stream, luck);
                        results.Add(new JamlPeekEvent("grosMichelExtinct", roll, triggered, null));
                    }
                    break;
                }
                case SpaceLevelupClause:
                {
                    var stream = ctx.CreateSpacePrngStream();
                    foreach (var roll in clause.Rolls)
                    {
                        for (int i = 0; i < roll; i++)
                            ctx.GetNextSpaceLevelup(ref stream, luck);
                        bool triggered = ctx.GetNextSpaceLevelup(ref stream, luck);
                        results.Add(new JamlPeekEvent("spaceLevelup", roll, triggered, null));
                    }
                    break;
                }
                case BusinessPayoutClause:
                {
                    var stream = ctx.CreateBusinessPrngStream();
                    foreach (var roll in clause.Rolls)
                    {
                        for (int i = 0; i < roll; i++)
                            ctx.GetNextBusinessPayout(ref stream, luck);
                        bool triggered = ctx.GetNextBusinessPayout(ref stream, luck);
                        results.Add(new JamlPeekEvent("businessPayout", roll, triggered, null));
                    }
                    break;
                }
                case BloodstoneTriggerClause:
                {
                    var stream = ctx.CreateBloodstonePrngStream();
                    foreach (var roll in clause.Rolls)
                    {
                        for (int i = 0; i < roll; i++)
                            ctx.GetNextBloodstoneTrigger(ref stream, luck);
                        bool triggered = ctx.GetNextBloodstoneTrigger(ref stream, luck);
                        results.Add(new JamlPeekEvent("bloodstoneTrigger", roll, triggered, null));
                    }
                    break;
                }
                case ParkingPayoutClause:
                {
                    var stream = ctx.CreateParkingPrngStream();
                    foreach (var roll in clause.Rolls)
                    {
                        for (int i = 0; i < roll; i++)
                            ctx.GetNextParkingPayout(ref stream, luck);
                        bool triggered = ctx.GetNextParkingPayout(ref stream, luck);
                        results.Add(new JamlPeekEvent("parkingPayout", roll, triggered, null));
                    }
                    break;
                }
                case GlassDestroyClause:
                {
                    var stream = ctx.CreateGlassPrngStream();
                    foreach (var roll in clause.Rolls)
                    {
                        for (int i = 0; i < roll; i++)
                            ctx.GetNextGlassDestroy(ref stream, luck);
                        bool triggered = ctx.GetNextGlassDestroy(ref stream, luck);
                        results.Add(new JamlPeekEvent("glassDestroy", roll, triggered, null));
                    }
                    break;
                }
                case WheelStaysFlippedClause:
                {
                    var stream = ctx.CreateTheWheelPrngStream();
                    foreach (var roll in clause.Rolls)
                    {
                        for (int i = 0; i < roll; i++)
                            ctx.GetNextWheelStaysFlipped(ref stream, luck);
                        bool triggered = ctx.GetNextWheelStaysFlipped(ref stream, luck);
                        results.Add(new JamlPeekEvent("wheelStaysFlipped", roll, triggered, null));
                    }
                    break;
                }
            }

            return results;
        }

        private static IReadOnlyList<string> BuildDrawOrder(ref MotelySingleSearchContext ctx)
        {
            if (ctx.Deck == MotelyDeck.Erratic)
            {
                var cards = new List<string>(52);
                var stream = ctx.CreateErraticDeckPrngStream();
                for (int i = 0; i < 52; i++)
                {
                    var card = ctx.GetNextErraticDeckCard(ref stream);
                    cards.Add(FormatCardString(card.StandardcardRank, card.StandardcardSuit));
                }
                return cards;
            }

            // Standard deck composition — same for all standard decks
            var standard = new List<string>(52);
            foreach (var card in MotelyEnum<MotelyStandardCard>.Values)
            {
                standard.Add(FormatCardString(card.GetRank(), card.GetSuit()));
            }
            return standard;
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
