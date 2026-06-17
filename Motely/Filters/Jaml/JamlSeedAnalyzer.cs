using System.Linq;
using System.Text;

namespace Motely.Filters.Jaml;

/// <summary>
/// JAML-powered seed analyzer. One parameter (<see cref="JamlConfig"/>), two modes:
/// <list type="bullet">
/// <item><see cref="CreateScoreProvider"/> for the SIMD search path</item>
/// <item><see cref="Analyze(string, MotelyDeck, MotelyStake)"/> for single-seed introspection (glow / peek / scoop)</item>
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

    /// <summary>
    /// Convenience for the CLI <c>--analyze --jaml</c> path: analyze <paramref name="seed"/> against
    /// <paramref name="config"/> (using its deck/stake) and render a human-readable text block.
    /// </summary>
    public static string Analyze(string seed, JamlConfig config)
    {
        var result = new JamlSeedAnalyzer(config).Analyze(seed, config.Deck, config.Stake);
        return Render(result);
    }

    // ── Text rendering (the * marks a clause-matched item — the glow, in plain text) ──

    public static string Render(JamlAnalysisResult r)
    {
        var sb = new StringBuilder();

        RenderMatchGroup(sb, "MUST", r.MustMatches);
        RenderMatchGroup(sb, "SHOULD", r.ShouldMatches);
        RenderMatchGroup(sb, "MUST NOT (violations)", r.MustNotMatches);

        if (r.Peek.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("PEEK (clause-targeted antes — * = matched by a clause):");
            foreach (var ante in r.Peek)
            {
                sb.Append("  Ante ").Append(ante.Ante);
                if (ante.Boss is { Length: > 0 })
                    sb.Append("  boss=").Append(Glow(ante.Boss, ante.BossHighlighted));
                if (ante.Voucher is { Length: > 0 })
                    sb.Append("  voucher=").Append(Glow(ante.Voucher, ante.VoucherHighlighted));
                if (ante.SmallBlindTag is { Length: > 0 } || ante.BigBlindTag is { Length: > 0 })
                    sb.Append("  tags=[")
                        .Append(Glow(ante.SmallBlindTag, ante.SmallBlindTagHighlighted))
                        .Append(", ")
                        .Append(Glow(ante.BigBlindTag, ante.BigBlindTagHighlighted))
                        .Append(']');
                sb.AppendLine();

                if (ante.ShopItems.Count > 0)
                {
                    sb.Append("    shop: ");
                    sb.AppendLine(string.Join("  ", ante.ShopItems.Select(FormatPeekItem)));
                }

                foreach (var pack in ante.Packs)
                {
                    sb.Append("    ").Append(pack.Type).Append(": ");
                    sb.AppendLine(
                        pack.Cards.Count > 0
                            ? string.Join(", ", pack.Cards.Select(FormatPeekItem))
                            : "(empty)"
                    );
                }
            }
        }

        if (r.MustMatches.Count == 0 && r.ShouldMatches.Count == 0 && r.MustNotMatches.Count == 0)
            sb.AppendLine("(no clauses matched this seed)");

        return sb.ToString();
    }

    private static void RenderMatchGroup(StringBuilder sb, string title, IReadOnlyList<JamlMatch> matches)
    {
        if (matches.Count == 0)
            return;
        sb.Append(title).AppendLine(":");
        foreach (var m in matches)
        {
            sb.Append("  [✓] ");
            sb.Append(m.ClauseLabel is { Length: > 0 } ? m.ClauseLabel : $"clause #{m.ClauseIndex}");
            sb.Append(" — ").Append(m.ItemName);
            // Ante <= 0 marks a non-board, count-only match (events / erratic / starting draw):
            // show the count, not a board location.
            if (m.Ante > 0)
            {
                sb.Append(" @ ante ").Append(m.Ante).Append(' ').Append(m.Source);
                if (m.Slot >= 0)
                    sb.Append(" slot ").Append(m.Slot);
                if (m.Score != 0)
                    sb.Append("  (+").Append(m.Score).Append(')');
            }
            else if (m.Score != 0)
            {
                sb.Append("  (×").Append(m.Score).Append(')');
            }
            sb.AppendLine();
        }
    }

    private static string FormatPeekItem(JamlPeekItem item) =>
        item.IsHighlighted ? $"*{item.Name}*" : item.Name;

    private static string Glow(string? text, bool highlighted) =>
        highlighted ? $"*{text}*" : (text ?? "");
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
    IReadOnlyList<JamlPeekPack> Packs,
    // Field-level glow: a clause matched this ante's boss / first voucher / blind tags.
    bool BossHighlighted = false,
    bool VoucherHighlighted = false,
    bool SmallBlindTagHighlighted = false,
    bool BigBlindTagHighlighted = false
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
            // Every raw match across all categories — used to light up the peek view.
            var allScooped = new List<ScoopedMatch>();

            var runState = new MotelyRunState();
            JamlScoring.PrepareRunState(ref singleCtx, CombineForPrepareRunState(config), ref runState);

            CollectMatches(ref singleCtx, config.Must, ref runState, mustMatches, allScooped, "must", requireMin: true);
            CollectMatches(ref singleCtx, config.Should, ref runState, shouldMatches, allScooped, "should", requireMin: false);
            CollectMatches(ref singleCtx, config.MustNot, ref runState, mustNotMatches, allScooped, "mustNot", requireMin: false);

            // ── Peek view: only the antes the filter cares about, with matched slots lit ──
            var peek = BuildPeek(ref singleCtx, config, runState, allScooped);

            FilterDesc.LastResult = new JamlAnalysisResult(
                singleCtx.GetSeed(),
                mustMatches,
                shouldMatches,
                mustNotMatches,
                peek
            );

            return true; // Analysis is not a filter — always pass
        }

        private static void CollectMatches(
            ref MotelySingleSearchContext ctx,
            List<IJamlClause> clauses,
            ref MotelyRunState runState,
            List<JamlMatch> into,
            List<ScoopedMatch> allScooped,
            string source,
            bool requireMin
        )
        {
            for (int i = 0; i < clauses.Count; i++)
            {
                var clause = clauses[i];
                var scoop = new JamlScoop { CurrentClauseIndex = i };
                runState.ScoopSink = scoop;

                int raw = JamlScoring.CountRawOccurrences(ref ctx, clause, ref runState);
                if (raw > 0 && (!requireMin || raw >= clause.Min))
                {
                    foreach (var m in scoop.Matches)
                    {
                        allScooped.Add(m);
                        into.Add(ConvertMatch(m, clause.Label, source));
                    }

                    // The clause passed (per the real scorer's raw count) but recorded no board
                    // cell — event/roll counters, erratic deck, starting draw. Surface a count-only
                    // entry so the analyzer never reports "no match" for a clause the search accepts.
                    // The match decision is the scorer's, not re-derived here.
                    if (scoop.Matches.Count == 0)
                        into.Add(new JamlMatch(i, clause.Label, clause.Describe(), "count", 0, -1, raw));
                }
                runState.ScoopSink = null;
            }
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

            // Format at the boundary: vouchers/bosses/tags arrive as raw enum codes from the engine;
            // turn them into display text here, not in the scorer. int.MinValue → format the MotelyItem.
            string itemName = m.Code != int.MinValue
                ? m.Source switch
                {
                    MotelyMatchSource.Voucher => ((MotelyVoucher)m.Code).ToString(),
                    MotelyMatchSource.Boss => ((MotelyBossBlind)m.Code).ToString(),
                    MotelyMatchSource.Tag => ((MotelyTag)m.Code).ToString(),
                    _ => "The Soul",
                }
                : FormatUtils.FormatItem(m.Item);

            return new JamlMatch(
                m.ClauseIndex,
                label,
                itemName,
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

        /// <summary>
        /// Antes a clause targets, regardless of clause family: <see cref="JamlClause.Antes"/>,
        /// <see cref="RollClause.Rolls"/> (event ante indices), and recursively the children of a
        /// <see cref="LogicClause"/> (and / or).
        /// </summary>
        private static void CollectAntes(IJamlClause clause, HashSet<int> into)
        {
            switch (clause)
            {
                case JamlClause jc:
                    into.UnionWith(jc.Antes);
                    break;
                case RollClause rc:
                    into.UnionWith(rc.Rolls);
                    break;
                case LogicClause lc:
                    foreach (var inner in lc.Clauses)
                        CollectAntes(inner, into);
                    break;
            }
        }

        private static IReadOnlyList<JamlAntePeek> BuildPeek(
            ref MotelySingleSearchContext ctx,
            JamlConfig config,
            MotelyRunState runState,
            List<ScoopedMatch> scooped
        )
        {
            var antes = new List<JamlAntePeek>();

            // Glow lookups built from the real scoop: shop matches key on (ante, slot); pack
            // matches key on (ante, packIndex, cardIndex). These are exactly the coordinates
            // JamlScoring.Scooped(...) records, so the peek lights up precisely what matched.
            var shopHits = new HashSet<(int Ante, int Slot)>();
            var packHits = new HashSet<(int Ante, int Pack, int Card)>();
            // Field-level glow: bosses, the ante's first voucher (roll 0 = the one we display),
            // and the small/big blind tags (tag draw 0 / 1).
            var bossHits = new HashSet<int>();
            var voucherFieldHits = new HashSet<int>();
            var smallTagHits = new HashSet<int>();
            var bigTagHits = new HashSet<int>();
            foreach (var m in scooped)
            {
                switch (m.Source)
                {
                    case MotelyMatchSource.Shop:
                        shopHits.Add((m.Ante, m.Slot));
                        break;
                    case MotelyMatchSource.BoosterPack:
                        packHits.Add((m.Ante, m.Slot, m.CardIndex));
                        break;
                    case MotelyMatchSource.Boss:
                        bossHits.Add(m.Ante);
                        break;
                    case MotelyMatchSource.Voucher when m.Slot == 0:
                        voucherFieldHits.Add(m.Ante);
                        break;
                    case MotelyMatchSource.Tag when m.Slot == 0:
                        smallTagHits.Add(m.Ante);
                        break;
                    case MotelyMatchSource.Tag when m.Slot == 1:
                        bigTagHits.Add(m.Ante);
                        break;
                }
            }

            // Collect all unique antes mentioned by any clause
            var allAntes = new HashSet<int>();
            foreach (var c in config.Must) CollectAntes(c, allAntes);
            foreach (var c in config.Should) CollectAntes(c, allAntes);
            foreach (var c in config.MustNot) CollectAntes(c, allAntes);

            if (allAntes.Count == 0)
                allAntes.Add(1); // Default to ante 1 if no antes specified

            var sortedAntes = allAntes.Where(a => a >= 1).OrderBy(a => a).ToArray();
            if (sortedAntes.Length == 0)
                return antes;
            var maxAnte = sortedAntes[^1];

            // Pre-cache bosses if needed
            if (runState.CachedBosses == null)
            {
                runState.CachedBosses = new MotelyBossBlind[maxAnte + 1];
                var bossStream = ctx.CreateBossStream();
                var bossState = new MotelyRunState();
                for (int a = 1; a <= maxAnte; a++)
                    runState.CachedBosses[a] = ctx.GetBossForAnte(ref bossStream, a, ref bossState);
            }

            // The displayed first voucher per ante must be reconstructed with a FRESH state that
            // activates vouchers ante-by-ante in gameplay order — exactly like CountVoucherOccurrences.
            // Reusing the scoring runState (already voucher-activated by PrepareRunState) would make
            // GetAnteFirstVoucher skip activated vouchers and report the wrong one.
            var firstVouchers = new MotelyVoucher[maxAnte + 1];
            {
                var voucherState = new MotelyRunState();
                for (int a = 1; a <= maxAnte; a++)
                {
                    var v = ctx.GetAnteFirstVoucher(a, voucherState);
                    firstVouchers[a] = v;
                    voucherState.ActivateVoucher(v);
                }
            }

            foreach (int ante in sortedAntes)
            {
                string? boss = runState.CachedBosses != null && ante < runState.CachedBosses.Length
                    ? runState.CachedBosses[ante].ToString()
                    : null;

                var voucher = firstVouchers[ante];

                var tagStream = ctx.CreateTagStream(ante);
                var smallTag = ctx.GetNextTag(ref tagStream);
                var bigTag = ctx.GetNextTag(ref tagStream);

                var shopItems = new List<JamlPeekItem>();
                var packs = new List<JamlPeekPack>();

                // Materialize shop (first 10 slots). Default run state — the scorer's shop matchers
                // all use CreateShopItemStream(ante) (default state), so this keeps slot glow aligned.
                var shopStream = ctx.CreateShopItemStream(ante);
                for (int slot = 0; slot < 10; slot++)
                {
                    var item = ctx.GetNextShopItem(ref shopStream);
                    shopItems.Add(new JamlPeekItem(
                        slot,
                        FormatUtils.FormatItem(item),
                        shopHits.Contains((ante, slot))
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
                    int packIndex = p;
                    var cards = packContents
                        .AsArray()
                        .Select((item, idx) => new JamlPeekItem(
                            idx,
                            FormatUtils.FormatItem(item),
                            packHits.Contains((ante, packIndex, idx))
                        ))
                        .ToArray();

                    packs.Add(new JamlPeekPack(pack.ToString(), cards));
                }

                antes.Add(new JamlAntePeek(
                    ante,
                    boss,
                    voucher.ToString(),
                    smallTag.ToString(),
                    bigTag.ToString(),
                    shopItems,
                    packs,
                    BossHighlighted: bossHits.Contains(ante),
                    VoucherHighlighted: voucherFieldHits.Contains(ante),
                    SmallBlindTagHighlighted: smallTagHits.Contains(ante),
                    BigBlindTagHighlighted: bigTagHits.Contains(ante)
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
