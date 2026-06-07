using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Motely.Filters.Jaml;

public static class JamlScoring
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void PrepareRunState(
        ref MotelySingleSearchContext ctx,
        IJamlClause[] clauses,
        ref MotelyRunState runState
    )
    {
        Debug.Assert(
            clauses.Length > 0,
            "PrepareRunState requires a non-empty should-clause array (CreatePlan / search wiring bug)."
        );

        int maxAnte = 0;
        int maxBossAnte = 0;

        for (int i = 0; i < clauses.Length; i++)
        {
            int clauseMaxAnte = GetMaxAnte(clauses[i]);
            if (clauseMaxAnte > maxAnte)
                maxAnte = clauseMaxAnte;

            if (clauses[i] is BossClause && clauseMaxAnte > maxBossAnte)
                maxBossAnte = clauseMaxAnte;
        }

        int maxVoucherAnte = maxAnte < 8 ? maxAnte + 1 : maxAnte;
        for (int ante = 1; ante <= maxVoucherAnte; ante++)
        {
            var voucher = ctx.GetAnteFirstVoucher(ante, runState);
            runState.ActivateVoucher(voucher);

            if (voucher is MotelyVoucher.Hieroglyph or MotelyVoucher.Petroglyph)
            {
                runState.ActivateExtendedPackAnte(ante - 1);
                var voucherStream = ctx.CreateVoucherStream(ante);
                var bonusVoucher = ctx.GetNextVoucher(ref voucherStream, runState);
                runState.ActivateVoucher(bonusVoucher);
            }
        }

        if (maxBossAnte > 0)
        {
            runState.CachedBosses = new MotelyBossBlind[maxBossAnte + 1];
            var bossStream = ctx.CreateBossStream();
            var bossState = new MotelyRunState();
            for (int ante = 1; ante <= maxBossAnte; ante++)
                runState.CachedBosses[ante] = ctx.GetBossForAnte(
                    ref bossStream,
                    ante,
                    ref bossState
                );
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CountOccurrences(
        ref MotelySingleSearchContext ctx,
        IJamlClause clause,
        ref MotelyRunState runState
    )
    {
        var count = CountOccurrencesUncapped(ref ctx, clause, ref runState);
        return CapScoreCount(count, clause);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CountOccurrencesUncapped(
        ref MotelySingleSearchContext ctx,
        IJamlClause clause,
        ref MotelyRunState runState
    )
    {
        return clause switch
        {
            JokerClause c => CountJokerOccurrences(ref ctx, c, ref runState),
            CommonJokerClause c => CountCommonJokerOccurrences(ref ctx, c, ref runState),
            UncommonJokerClause c => CountUncommonJokerOccurrences(ref ctx, c, ref runState),
            RareJokerClause c => CountRareJokerOccurrences(ref ctx, c, ref runState),
            MixedJokerClause c => CountMixedJokerOccurrences(ref ctx, c, ref runState),
            LegendaryJokerClause c => CountLegendaryJokerOccurrences(ref ctx, c, ref runState),
            VoucherClause c => CountVoucherOccurrences(ref ctx, c, ref runState),
            TarotCardClause c => CountTarotCardOccurrences(ref ctx, c, ref runState),
            SpectralCardClause c => CountSpectralCardOccurrences(ref ctx, c, ref runState),
            PlanetCardClause c => CountPlanetCardOccurrences(ref ctx, c, ref runState),
            BossClause c => CountBossOccurrences(c, ref runState),
            TagClause c => CountTagOccurrences(ref ctx, c),
            StandardCardClause c => CountStandardCardOccurrences(ref ctx, c, ref runState),
            ErraticRankClause c => CountErraticRankOccurrences(ref ctx, c),
            ErraticSuitClause c => CountErraticSuitOccurrences(ref ctx, c),
            ErraticCardClause c => CountErraticCardOccurrences(ref ctx, c),
            LuckyMoneyClause c => CountLuckyMoneyOccurrences(ref ctx, c, ref runState),
            LuckyMultClause c => CountLuckyMultOccurrences(ref ctx, c, ref runState),
            MisprintMultClause c => CountMisprintMultOccurrences(ref ctx, c),
            WheelOfFortuneClause c => CountWheelOfFortuneOccurrences(ref ctx, c),
            CavendishExtinctClause c => CountCavendishExtinctOccurrences(ref ctx, c),
            GrosMichelExtinctClause c => CountGrosMichelExtinctOccurrences(ref ctx, c),
            SpaceLevelupClause c => CountSpaceLevelupOccurrences(ref ctx, c),
            BusinessPayoutClause c => CountBusinessPayoutOccurrences(ref ctx, c),
            BloodstoneTriggerClause c => CountBloodstoneTriggerOccurrences(ref ctx, c),
            ParkingPayoutClause c => CountParkingPayoutOccurrences(ref ctx, c),
            GlassDestroyClause c => CountGlassDestroyOccurrences(ref ctx, c),
            WheelStaysFlippedClause c => CountWheelStaysFlippedOccurrences(ref ctx, c),
            StartingDrawClause c => CountStartingDrawOccurrences(ref ctx, c),
            AndClause c => CountAndOccurrences(ref ctx, c, ref runState),
            OrClause c => CountOrOccurrences(ref ctx, c, ref runState),
            _ => UnhandledClauseForScoring(clause),
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int CapScoreCountForTesting(int count, IJamlClause clause) =>
        CapScoreCount(count, clause);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CapScoreCount(int count, IJamlClause clause)
    {
        var max = clause.Max;
        return max is > 0 && count > max.Value ? max.Value : count;
    }

    private static int UnhandledClauseForScoring(IJamlClause clause)
    {
        Debug.Assert(
            false,
            $"JamlScoring.CountOccurrences: unhandled clause type {clause.GetType().Name} (extend switch or exclude from should-clauses)."
        );
        return 0;
    }

    private static int CountAndOccurrences(
        ref MotelySingleSearchContext ctx,
        AndClause clause,
        ref MotelyRunState runState
    )
    {
        Debug.Assert(
            clause.Clauses.Length > 0,
            "AndClause should not be empty after JAML load (validator / loader bug)."
        );

        int total = 0;
        for (int i = 0; i < clause.Clauses.Length; i++)
        {
            int count = CountOccurrences(ref ctx, clause.Clauses[i], ref runState);
            if (count <= 0)
                return 0;
            int w = clause.Clauses[i].Score;
            if (w == 0)
                w = 1;
            total += count * w;
        }

        return clause.Score != 0 ? total : 1;
    }

    private static int CountOrOccurrences(
        ref MotelySingleSearchContext ctx,
        OrClause clause,
        ref MotelyRunState runState
    )
    {
        Debug.Assert(
            clause.Clauses.Length > 0,
            "OrClause should not be empty after JAML load (validator / loader bug)."
        );
        Debug.Assert(
            clause.Min >= 1,
            "OrClause.Min must be >= 1 after JAML load (validator / loader bug)."
        );

        int matched = 0;
        int total = 0;

        for (int i = 0; i < clause.Clauses.Length; i++)
        {
            int count = CountOccurrences(ref ctx, clause.Clauses[i], ref runState);
            if (count > 0)
            {
                matched++;
                int w = clause.Clauses[i].Score;
                if (w == 0)
                    w = 1;
                total += count * w;
            }
        }

        if (matched < clause.Min)
            return 0;

        return clause.Score != 0 ? total : matched;
    }

    /// <summary>
    /// Raw occurrence counts per should-clause column (CSV tally columns). For composite clauses this sums
    /// child raw counts without multiplying by per-clause <see cref="IJamlClause.Score"/>; weighted totals are
    /// computed separately by <see cref="CountOccurrences"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    /// <summary>
    /// Reports a positive match to the analyzer's scoop sink (if one is attached) and returns the
    /// match count unchanged, so a <c>count += Match*(...)</c> site becomes a one-line
    /// <c>count += Scooped(...)</c>. No-op (one null-check) on the hot search path.
    /// </summary>
    private static int Scooped(
        ref MotelyRunState runState,
        int match,
        MotelyMatchSource source,
        int ante,
        int slot,
        int cardIndex,
        MotelyItem item
    )
    {
        if (match > 0)
            runState.ScoopSink?.Record(source, ante, slot, cardIndex, item, match);
        return match;
    }

    public static int CountRawOccurrences(
        ref MotelySingleSearchContext ctx,
        IJamlClause clause,
        ref MotelyRunState runState
    )
    {
        return clause switch
        {
            AndClause c => CountRawAndOccurrences(ref ctx, c, ref runState),
            OrClause c => CountRawOrOccurrences(ref ctx, c, ref runState),
            _ => CountOccurrencesUncapped(ref ctx, clause, ref runState),
        };
    }

    private static int CountRawAndOccurrences(
        ref MotelySingleSearchContext ctx,
        AndClause clause,
        ref MotelyRunState runState
    )
    {
        Debug.Assert(
            clause.Clauses.Length > 0,
            "AndClause should not be empty after JAML load (validator / loader bug)."
        );

        int total = 0;
        for (int i = 0; i < clause.Clauses.Length; i++)
        {
            int count = CountRawOccurrences(ref ctx, clause.Clauses[i], ref runState);
            if (count <= 0)
                return 0;
            total += count;
        }

        return clause.Score != 0 ? total : 1;
    }

    private static int CountRawOrOccurrences(
        ref MotelySingleSearchContext ctx,
        OrClause clause,
        ref MotelyRunState runState
    )
    {
        Debug.Assert(
            clause.Clauses.Length > 0,
            "OrClause should not be empty after JAML load (validator / loader bug)."
        );
        Debug.Assert(
            clause.Min >= 1,
            "OrClause.Min must be >= 1 after JAML load (validator / loader bug)."
        );

        int matched = 0;
        int total = 0;

        for (int i = 0; i < clause.Clauses.Length; i++)
        {
            int count = CountRawOccurrences(ref ctx, clause.Clauses[i], ref runState);
            if (count > 0)
            {
                matched++;
                total += count;
            }
        }

        if (matched < clause.Min)
            return 0;

        return clause.Score != 0 ? total : matched;
    }

    private static int CountBossOccurrences(BossClause clause, ref MotelyRunState runState)
    {
        Debug.Assert(
            runState.CachedBosses != null,
            "Boss scoring requires PrepareRunState to populate CachedBosses (loader / run-state bug)."
        );
        Debug.Assert(
            clause.Bosses.Length > 0,
            "BossClause.Bosses must be non-empty after JAML load (validator / loader bug)."
        );
        Debug.Assert(
            clause.Antes.Length > 0,
            "BossClause.Antes must be non-empty after JAML load (validator / loader bug)."
        );

        int count = 0;
        foreach (int ante in clause.Antes)
        {
            Debug.Assert(
                ante >= 1 && ante < runState.CachedBosses!.Length,
                $"BossClause ante {ante} is out of range for CachedBosses (validator / loader bug)."
            );
            for (int i = 0; i < clause.Bosses.Length; i++)
                if (clause.Bosses[i] == runState.CachedBosses[ante])
                    count++;
        }
        return count;
    }

    private static int CountStandardCardOccurrences(
        ref MotelySingleSearchContext ctx,
        StandardCardClause clause,
        ref MotelyRunState runState
    )
    {
        int count = 0;
        int maxShop = ArrayMax(clause.Sources.ShopItems);
        int userMaxPack = ArrayMax(clause.Sources.BoosterPacks);

        foreach (int ante in clause.Antes)
        {
            int maxPack = ClampBoosterPackSlotForAnte(ante, userMaxPack, ref runState);
            if (clause.Sources.ShopItems.Length > 0)
            {
                var shopStream = ctx.CreateShopItemStream(ante);
                for (int slot = 0; slot <= maxShop; slot++)
                {
                    var item = ctx.GetNextShopItem(ref shopStream);
                    if (ArrayContains(clause.Sources.ShopItems, slot))
                        count += Scooped(
                            ref runState,
                            MatchStandardCard(item, clause),
                            MotelyMatchSource.Shop,
                            ante,
                            slot,
                            -1,
                            item
                        );
                }
            }

            if (clause.Sources.BoosterPacks.Length > 0)
            {
                var packStream = ctx.CreateBoosterPackStream(ante);
                var cardStream = ctx.CreateStandardPackCardStream(ante);
                for (int packIndex = 0; packIndex <= maxPack; packIndex++)
                {
                    var pack = ctx.GetNextBoosterPack(ref packStream);
                    if (pack.GetPackType() != MotelyBoosterPackType.Standard)
                        continue;
                    var contents = ctx.GetNextStandardPackContents(
                        ref cardStream,
                        pack.GetPackSize()
                    );
                    if (!ArrayContains(clause.Sources.BoosterPacks, packIndex))
                        continue;
                    for (int i = 0; i < contents.Length; i++)
                        count += Scooped(
                            ref runState,
                            MatchStandardCard(contents[i], clause),
                            MotelyMatchSource.BoosterPack,
                            ante,
                            packIndex,
                            i,
                            contents[i]
                        );
                }
            }
        }

        return count;
    }

    private static int CountTarotCardOccurrences(
        ref MotelySingleSearchContext ctx,
        TarotCardClause clause,
        ref MotelyRunState runState
    )
    {
        int count = 0;
        int maxShop = ArrayMax(clause.Sources.ShopItems);
        int userMaxPack = ArrayMax(clause.Sources.BoosterPacks);
        int maxEmperor = ArrayMax(clause.Sources.Emperor);
        int maxSeal = ArrayMax(clause.Sources.PurpleSealOrEightBall);

        foreach (int ante in clause.Antes)
        {
            int maxPack = ClampBoosterPackSlotForAnte(ante, userMaxPack, ref runState);
            if (clause.Sources.ShopItems.Length > 0)
            {
                var shopStream = ctx.CreateShopItemStream(ante);
                for (int slot = 0; slot <= maxShop; slot++)
                {
                    var item = ctx.GetNextShopItem(ref shopStream);
                    if (ArrayContains(clause.Sources.ShopItems, slot))
                        count += Scooped(
                            ref runState,
                            MatchTarot(item, clause),
                            MotelyMatchSource.Shop,
                            ante,
                            slot,
                            -1,
                            item
                        );
                }
            }

            if (clause.Sources.BoosterPacks.Length > 0)
            {
                bool charmWant = clause.Sources.CharmTag;

                var packStream = ctx.CreateBoosterPackStream(ante);
                var tarotStream = ctx.CreateArcanaPackTarotStream(ante);
                bool hadNaturalArcanaPack = false;
                int weightedShopDrawNumber = 0;

                for (int packIndex = 0; ; packIndex++)
                {
                    bool needForClause = packIndex <= maxPack;
                    bool needForCharmClosure = charmWant && weightedShopDrawNumber < 2;
                    if (!needForClause && !needForCharmClosure)
                        break;

                    var pack = ctx.GetNextBoosterPack(ref packStream);
                    var packType = pack.GetPackType();
                    if (packType == MotelyBoosterPackType.Buffoon)
                        continue;

                    weightedShopDrawNumber++;

                    if (packType == MotelyBoosterPackType.Arcana)
                    {
                        hadNaturalArcanaPack = true;
                        var contents = ctx.GetNextArcanaPackContents(
                            ref tarotStream,
                            pack.GetPackSize()
                        );
                        if (!ArrayContains(clause.Sources.BoosterPacks, packIndex))
                            continue;
                        for (int i = 0; i < contents.Length; i++)
                            count += Scooped(
                                ref runState,
                                MatchTarot(contents[i], clause),
                                MotelyMatchSource.BoosterPack,
                                ante,
                                packIndex,
                                i,
                                contents[i]
                            );
                        continue;
                    }

                    // Charm: extra Arcana on the second real shop pack (after Buffoon) only if the two
                    // weighted rolls had no Arcana — uses pack stream order, not ante-scaled indices.
                    if (charmWant && !hadNaturalArcanaPack && weightedShopDrawNumber == 2)
                    {
                        var contents = ctx.GetNextArcanaPackContents(
                            ref tarotStream,
                            pack.GetPackSize()
                        );
                        if (!ArrayContains(clause.Sources.BoosterPacks, packIndex))
                            continue;
                        for (int i = 0; i < contents.Length; i++)
                            count += Scooped(
                                ref runState,
                                MatchTarot(contents[i], clause),
                                MotelyMatchSource.BoosterPack,
                                ante,
                                packIndex,
                                i,
                                contents[i]
                            );
                    }
                }
            }

            if (clause.Sources.Emperor.Length > 0)
            {
                var emperorStream = ctx.CreateEmperorTarotStream(ante);
                for (int roll = 0; roll <= maxEmperor; roll++)
                {
                    var (t1, t2) = ctx.GetNextEmperorTarots(ref emperorStream);
                    if (!ArrayContains(clause.Sources.Emperor, roll))
                        continue;
                    count += MatchTarot(t1, clause);
                    count += MatchTarot(t2, clause);
                }
            }

            if (clause.Sources.PurpleSealOrEightBall.Length > 0)
            {
                var sealStream = ctx.CreatePurpleSealTarotStream(ante);
                for (int roll = 0; roll <= maxSeal; roll++)
                {
                    var item = ctx.GetNextTarot(ref sealStream);
                    if (ArrayContains(clause.Sources.PurpleSealOrEightBall, roll))
                        count += MatchTarot(item, clause);
                }
            }
        }

        return count;
    }

    private static int CountSpectralCardOccurrences(
        ref MotelySingleSearchContext ctx,
        SpectralCardClause clause,
        ref MotelyRunState runState
    )
    {
        int count = 0;
        int maxShop = ArrayMax(clause.Sources.ShopItems);
        int userMaxPack = ArrayMax(clause.Sources.BoosterPacks);
        int maxSixthSense = ArrayMax(clause.Sources.SixthSense);
        int maxSeance = ArrayMax(clause.Sources.Seance);

        foreach (int ante in clause.Antes)
        {
            int maxPack = ClampBoosterPackSlotForAnte(ante, userMaxPack, ref runState);
            if (clause.Sources.ShopItems.Length > 0)
            {
                var shopStream = ctx.CreateShopItemStream(ante);
                for (int slot = 0; slot <= maxShop; slot++)
                {
                    var item = ctx.GetNextShopItem(ref shopStream);
                    if (ArrayContains(clause.Sources.ShopItems, slot))
                        count += Scooped(
                            ref runState,
                            MatchSpectral(item, clause),
                            MotelyMatchSource.Shop,
                            ante,
                            slot,
                            -1,
                            item
                        );
                }
            }

            if (clause.Sources.BoosterPacks.Length > 0)
            {
                bool etherealWant = clause.Sources.EtherealTag;

                var packStream = ctx.CreateBoosterPackStream(ante);
                var spectralStream = ctx.CreateSpectralPackSpectralStream(ante);
                bool hadNaturalSpectralPack = false;
                int weightedShopDrawNumber = 0;

                for (int packIndex = 0; ; packIndex++)
                {
                    bool needForClause = packIndex <= maxPack;
                    bool needForEtherealClosure = etherealWant && weightedShopDrawNumber < 2;
                    if (!needForClause && !needForEtherealClosure)
                        break;

                    var pack = ctx.GetNextBoosterPack(ref packStream);
                    var packType = pack.GetPackType();
                    if (packType == MotelyBoosterPackType.Buffoon)
                        continue;

                    weightedShopDrawNumber++;

                    if (packType == MotelyBoosterPackType.Spectral)
                    {
                        hadNaturalSpectralPack = true;
                        var contents = ctx.GetNextSpectralPackContents(
                            ref spectralStream,
                            pack.GetPackSize()
                        );
                        if (!ArrayContains(clause.Sources.BoosterPacks, packIndex))
                            continue;
                        for (int i = 0; i < contents.Length; i++)
                            count += Scooped(
                                ref runState,
                                MatchSpectral(contents[i], clause),
                                MotelyMatchSource.BoosterPack,
                                ante,
                                packIndex,
                                i,
                                contents[i]
                            );
                        continue;
                    }

                    if (etherealWant && !hadNaturalSpectralPack && weightedShopDrawNumber == 2)
                    {
                        var contents = ctx.GetNextSpectralPackContents(
                            ref spectralStream,
                            pack.GetPackSize()
                        );
                        if (!ArrayContains(clause.Sources.BoosterPacks, packIndex))
                            continue;
                        for (int i = 0; i < contents.Length; i++)
                            count += Scooped(
                                ref runState,
                                MatchSpectral(contents[i], clause),
                                MotelyMatchSource.BoosterPack,
                                ante,
                                packIndex,
                                i,
                                contents[i]
                            );
                    }
                }
            }

            if (clause.Sources.SixthSense.Length > 0)
            {
                var sixthSenseStream = ctx.CreateSixthSenseSpectralStream(ante);
                for (int roll = 0; roll <= maxSixthSense; roll++)
                {
                    var item = ctx.GetNextSpectral(ref sixthSenseStream);
                    if (ArrayContains(clause.Sources.SixthSense, roll))
                        count += MatchSpectral(item, clause);
                }
            }

            if (clause.Sources.Seance.Length > 0)
            {
                var seanceStream = ctx.CreateSeanceSpectralStream(ante);
                for (int roll = 0; roll <= maxSeance; roll++)
                {
                    var item = ctx.GetNextSpectral(ref seanceStream);
                    if (ArrayContains(clause.Sources.Seance, roll))
                        count += MatchSpectral(item, clause);
                }
            }
        }

        // The two "special" spectral cards spawn from a dedicated soul/black-hole roll in pack types
        // the loop above never reads: TheSoul also appears in Arcana packs (tarot stream), BlackHole
        // also in Celestial packs (planet stream). The spectral-pack walk above already counts both
        // when they land in a SPECTRAL pack; this adds the missing Arcana/Celestial sources so
        // `spectralCard: TheSoul` / `spectralCard: BlackHole` see everywhere the card can actually
        // spawn — the same blind spot that made the spectralCard path wrong for these two.
        if (clause.Sources.BoosterPacks.Length > 0)
        {
            if (SpectralClauseTargets(clause, MotelySpectralCard.TheSoul))
                count += CountTheSoulInArcanaPacks(ref ctx, clause, ref runState);
            if (SpectralClauseTargets(clause, MotelySpectralCard.BlackHole))
                count += CountBlackHoleInCelestialPacks(ref ctx, clause, ref runState);
        }

        return count;
    }

    /// <summary>Whether a spectral clause names <paramref name="card"/> as a target.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool SpectralClauseTargets(SpectralCardClause clause, MotelySpectralCard card)
    {
        for (int i = 0; i < clause.Spectrals.Length; i++)
            if (clause.Spectrals[i] == card)
                return true;
        return false;
    }

    /// <summary>
    /// True when the clause names TheSoul and/or BlackHole — the "special" spectrals that need the
    /// Arcana/Celestial pack sources. Used to route <c>spectralCard:</c> to <see cref="SpecialSpectralCardFilterDesc"/>.
    /// </summary>
    public static bool TargetsSpecialSpectral(SpectralCardClause clause) =>
        SpectralClauseTargets(clause, MotelySpectralCard.TheSoul)
        || SpectralClauseTargets(clause, MotelySpectralCard.BlackHole);

    /// <summary>Scalar spectral count for the SIMD filter's per-seed confirmation pass (mirrors the should-scoring count).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int CountSpectralCardOccurrencesForFilter(
        ref MotelySingleSearchContext ctx,
        SpectralCardClause clause
    )
    {
        var runState = CreatePermissivePackRunState(clause.Antes);
        return CountSpectralCardOccurrences(ref ctx, clause, ref runState);
    }

    /// <summary>
    /// TheSoul appearing in Arcana packs (tarot stream soul roll). Same booster-pack indexing as the
    /// spectral-pack walk in <see cref="CountSpectralCardOccurrences"/> so source slot numbers line up;
    /// reads each Arcana pack's contents to keep the tarot stream aligned, counts only targeted slots.
    /// </summary>
    private static int CountTheSoulInArcanaPacks(
        ref MotelySingleSearchContext ctx,
        SpectralCardClause clause,
        ref MotelyRunState runState
    )
    {
        int count = 0;
        int userMaxPack = ArrayMax(clause.Sources.BoosterPacks);

        foreach (int ante in clause.Antes)
        {
            int maxPack = ClampBoosterPackSlotForAnte(ante, userMaxPack, ref runState);

            var packStream = ctx.CreateBoosterPackStream(ante);
            var tarotStream = ctx.CreateArcanaPackTarotStream(ante);
            for (int packIndex = 0; packIndex <= maxPack; packIndex++)
            {
                var pack = ctx.GetNextBoosterPack(ref packStream);
                if (pack.GetPackType() != MotelyBoosterPackType.Arcana)
                    continue;
                var contents = ctx.GetNextArcanaPackContents(ref tarotStream, pack.GetPackSize());
                if (!ArrayContains(clause.Sources.BoosterPacks, packIndex))
                    continue;
                for (int i = 0; i < contents.Length; i++)
                    count += MatchSpectral(contents[i], clause);
            }
        }

        return count;
    }

    /// <summary>
    /// BlackHole appearing in Celestial packs (planet stream black-hole roll). Mirror of
    /// <see cref="CountPlanetCardOccurrences"/>'s celestial walk; counts only the BlackHole item.
    /// </summary>
    private static int CountBlackHoleInCelestialPacks(
        ref MotelySingleSearchContext ctx,
        SpectralCardClause clause,
        ref MotelyRunState runState
    )
    {
        int count = 0;
        int userMaxPack = ArrayMax(clause.Sources.BoosterPacks);

        foreach (int ante in clause.Antes)
        {
            int maxPack = ClampBoosterPackSlotForAnte(ante, userMaxPack, ref runState);

            var packStream = ctx.CreateBoosterPackStream(ante);
            var planetStream = ctx.CreateCelestialPackPlanetStream(ante);
            for (int packIndex = 0; packIndex <= maxPack; packIndex++)
            {
                var pack = ctx.GetNextBoosterPack(ref packStream);
                if (pack.GetPackType() != MotelyBoosterPackType.Celestial)
                    continue;
                var contents = ctx.GetNextCelestialPackContents(
                    ref planetStream,
                    pack.GetPackSize()
                );
                if (!ArrayContains(clause.Sources.BoosterPacks, packIndex))
                    continue;
                for (int i = 0; i < contents.Length; i++)
                    count += MatchSpectral(contents[i], clause);
            }
        }

        return count;
    }

    private static int CountPlanetCardOccurrences(
        ref MotelySingleSearchContext ctx,
        PlanetCardClause clause,
        ref MotelyRunState runState
    )
    {
        int count = 0;
        int maxShop = ArrayMax(clause.Sources.ShopItems);
        int userMaxPack = ArrayMax(clause.Sources.BoosterPacks);

        foreach (int ante in clause.Antes)
        {
            int maxPack = ClampBoosterPackSlotForAnte(ante, userMaxPack, ref runState);
            if (clause.Sources.ShopItems.Length > 0)
            {
                var shopStream = ctx.CreateShopItemStream(ante);
                for (int slot = 0; slot <= maxShop; slot++)
                {
                    var item = ctx.GetNextShopItem(ref shopStream);
                    if (ArrayContains(clause.Sources.ShopItems, slot))
                        count += Scooped(
                            ref runState,
                            MatchPlanet(item, clause),
                            MotelyMatchSource.Shop,
                            ante,
                            slot,
                            -1,
                            item
                        );
                }
            }

            if (clause.Sources.BoosterPacks.Length > 0)
            {
                var packStream = ctx.CreateBoosterPackStream(ante);
                var planetStream = ctx.CreateCelestialPackPlanetStream(ante);
                for (int packIndex = 0; packIndex <= maxPack; packIndex++)
                {
                    var pack = ctx.GetNextBoosterPack(ref packStream);
                    if (pack.GetPackType() != MotelyBoosterPackType.Celestial)
                        continue;
                    var contents = ctx.GetNextCelestialPackContents(
                        ref planetStream,
                        pack.GetPackSize()
                    );
                    if (!ArrayContains(clause.Sources.BoosterPacks, packIndex))
                        continue;
                    for (int i = 0; i < contents.Length; i++)
                        count += Scooped(
                            ref runState,
                            MatchPlanet(contents[i], clause),
                            MotelyMatchSource.BoosterPack,
                            ante,
                            packIndex,
                            i,
                            contents[i]
                        );
                }
            }
        }

        return count;
    }

    private static int CountVoucherOccurrences(
        ref MotelySingleSearchContext ctx,
        VoucherClause clause,
        ref MotelyRunState runState
    )
    {
        // Start from a fresh state — PrepareRunState already activated vouchers into runState,
        // which would cause GetAnteFirstVoucher to skip them and return wrong results.
        var localState = new MotelyRunState();
        int count = 0;
        int maxAnte = GetMaxAnte(clause);
        int maxVoucherRoll = MapFeatureRolls.MaxRollIndex(clause.Rolls);
        Span<MotelyVoucher> streamDraws = stackalloc MotelyVoucher[maxVoucherRoll + 1];

        for (int ante = 1; ante <= maxAnte; ante++)
        {
            var voucher = ctx.GetAnteFirstVoucher(ante, localState);
            if (ArrayContains(clause.Antes, ante))
            {
                int maxRoll = maxVoucherRoll;
                if (maxRoll >= 1)
                {
                    var voucherStream = ctx.CreateVoucherStream(ante);
                    for (int i = 1; i <= maxRoll; i++)
                        streamDraws[i] = ctx.GetNextVoucher(ref voucherStream, localState);
                }

                foreach (var roll in clause.Rolls)
                {
                    var rolled = roll == 0 ? voucher : streamDraws[roll];
                    for (int i = 0; i < clause.Vouchers.Length; i++)
                    {
                        if (rolled == clause.Vouchers[i])
                            count++;
                    }
                }
            }

            localState.ActivateVoucher(voucher);
        }

        return count;
    }

    private static int CountTagOccurrences(ref MotelySingleSearchContext ctx, TagClause clause)
    {
        int count = 0;
        int maxDraw = MapFeatureRolls.MaxRollIndex(clause.Rolls);
        Span<MotelyTag> draws = stackalloc MotelyTag[maxDraw + 1];

        foreach (int ante in clause.Antes)
        {
            var tagStream = ctx.CreateTagStream(ante);
            for (int i = 0; i <= maxDraw; i++)
                draws[i] = ctx.GetNextTag(ref tagStream);

            foreach (var drawIndex in clause.Rolls)
            {
                var rolled = draws[drawIndex];
                for (int i = 0; i < clause.Tags.Length; i++)
                {
                    if (rolled == clause.Tags[i])
                        count++;
                }
            }
        }
        return count;
    }

    private static int CountStartingDrawOccurrences(
        ref MotelySingleSearchContext ctx,
        StartingDrawClause clause
    )
    {
        int count = 0;
        foreach (int ante in clause.Antes)
        {
            MotelyItem[] deck = new MotelyItem[MotelyEnum<MotelyStandardCard>.ValueCount];
            for (int i = 0; i < deck.Length; i++)
                deck[i] = new(MotelyEnum<MotelyStandardCard>.Values[i]);

            ctx.Shuffle("nr1", deck);
            int handSize = Math.Min(8, deck.Length);
            for (int i = 0; i < handSize; i++)
            {
                var card = deck[deck.Length - handSize + i];
                bool matchRank =
                    !clause.Rank.HasValue || card.StandardcardRank == clause.Rank.Value;
                bool matchSuit =
                    !clause.Suit.HasValue || card.StandardcardSuit == clause.Suit.Value;
                if (matchRank && matchSuit)
                    count++;
            }
        }
        return count;
    }

    private static int CountErraticRankOccurrences(
        ref MotelySingleSearchContext ctx,
        ErraticRankClause clause
    )
    {
        int count = 0;
        var stream = ctx.CreateErraticDeckPrngStream();
        for (int i = 0; i < 52; i++)
            if (ctx.GetNextErraticDeckCard(ref stream).StandardcardRank == clause.Rank)
                count++;
        return count;
    }

    private static int CountErraticSuitOccurrences(
        ref MotelySingleSearchContext ctx,
        ErraticSuitClause clause
    )
    {
        int count = 0;
        var stream = ctx.CreateErraticDeckPrngStream();
        for (int i = 0; i < 52; i++)
            if (ctx.GetNextErraticDeckCard(ref stream).StandardcardSuit == clause.Suit)
                count++;
        return count;
    }

    private static int CountErraticCardOccurrences(
        ref MotelySingleSearchContext ctx,
        ErraticCardClause clause
    )
    {
        int count = 0;
        var stream = ctx.CreateErraticDeckPrngStream();
        for (int i = 0; i < 52; i++)
        {
            var card = ctx.GetNextErraticDeckCard(ref stream);
            if (
                (!clause.Rank.HasValue || card.StandardcardRank == clause.Rank.Value)
                && (!clause.Suit.HasValue || card.StandardcardSuit == clause.Suit.Value)
            )
                count++;
        }
        return count;
    }

    private static int CountLuckyMoneyOccurrences(
        ref MotelySingleSearchContext ctx,
        LuckyMoneyClause clause,
        ref MotelyRunState runState
    )
    {
        int count = 0;
        var stream = ctx.CreateLuckyCardMoneyStream(isCached: false);
        var min = clause.Min;
        var max = clause.Max;
        runState.Luck = clause.Luck;
        double luck = runState.Luck;
        for (int i = 0; i < clause.Rolls.Length; i++)
        {
            var rollIndex = clause.Rolls[i];
            for (int j = 0; j < rollIndex; j++)
                ctx.GetNextLuckyMoney(ref stream, luck);
            if (ctx.GetNextLuckyMoney(ref stream, luck))
            {
                count++;
                if (max is null && count >= min)
                    return count;
            }

            int rollsRemaining = clause.Rolls.Length - 1 - i;
            if (count + rollsRemaining < min)
                return 0;
        }
        return count;
    }

    private static int CountLuckyMultOccurrences(
        ref MotelySingleSearchContext ctx,
        LuckyMultClause clause,
        ref MotelyRunState runState
    )
    {
        int count = 0;
        var stream = ctx.CreateLuckyCardMultStream(isCached: false);
        var min = clause.Min;
        var max = clause.Max;
        runState.Luck = clause.Luck;
        double luck = runState.Luck;
        for (int i = 0; i < clause.Rolls.Length; i++)
        {
            var rollIndex = clause.Rolls[i];
            for (int j = 0; j < rollIndex; j++)
                ctx.GetNextLuckyMult(ref stream, luck);
            if (ctx.GetNextLuckyMult(ref stream, luck))
            {
                count++;
                if (max is null && count >= min)
                    return count;
            }

            int rollsRemaining = clause.Rolls.Length - 1 - i;
            if (count + rollsRemaining < min)
                return 0;
        }
        return count;
    }

    private static int CountMisprintMultOccurrences(
        ref MotelySingleSearchContext ctx,
        MisprintMultClause clause
    )
    {
        int count = 0;
        var stream = ctx.CreateMisprintPrngStream();
        var min = clause.Min;
        var max = clause.Max;
        for (int i = 0; i < clause.Rolls.Length; i++)
        {
            var rollIndex = clause.Rolls[i];
            for (int j = 0; j < rollIndex; j++)
                ctx.GetNextMisprintMult(ref stream);
            if (ctx.GetNextMisprintMult(ref stream) >= 0)
            {
                count++;
                if (max is null && count >= min)
                    return count;
            }

            int rollsRemaining = clause.Rolls.Length - 1 - i;
            if (count + rollsRemaining < min)
                return 0;
        }
        return count;
    }

    private static int CountWheelOfFortuneOccurrences(
        ref MotelySingleSearchContext ctx,
        WheelOfFortuneClause clause
    )
    {
        int count = 0;
        var stream = ctx.CreateWheelOfFortuneStream();
        var min = clause.Min;
        var max = clause.Max;
        double luck = clause.Luck;
        for (int i = 0; i < clause.Rolls.Length; i++)
        {
            var rollIndex = clause.Rolls[i];
            for (int j = 0; j < rollIndex; j++)
                ctx.GetNextWheelOfFortune(ref stream, luck);
            if (ctx.GetNextWheelOfFortune(ref stream, luck) != MotelyItemEdition.None)
            {
                count++;
                if (max is null && count >= min)
                    return count;
            }

            int rollsRemaining = clause.Rolls.Length - 1 - i;
            if (count + rollsRemaining < min)
                return 0;
        }
        return count;
    }

    private static int CountCavendishExtinctOccurrences(
        ref MotelySingleSearchContext ctx,
        CavendishExtinctClause clause
    )
    {
        int count = 0;
        var stream = ctx.CreateCavendishPrngStream(false);
        var min = clause.Min;
        var max = clause.Max;
        double luck = clause.Luck;
        for (int i = 0; i < clause.Rolls.Length; i++)
        {
            var rollIndex = clause.Rolls[i];
            for (int j = 0; j < rollIndex; j++)
                ctx.GetNextCavendishExtinct(ref stream, luck);
            if (ctx.GetNextCavendishExtinct(ref stream, luck))
            {
                count++;
                if (max is null && count >= min)
                    return count;
            }

            int rollsRemaining = clause.Rolls.Length - 1 - i;
            if (count + rollsRemaining < min)
                return 0;
        }
        return count;
    }

    private static int CountGrosMichelExtinctOccurrences(
        ref MotelySingleSearchContext ctx,
        GrosMichelExtinctClause clause
    )
    {
        int count = 0;
        var stream = ctx.CreateGrosMichelPrngStream(false);
        var min = clause.Min;
        var max = clause.Max;
        double luck = clause.Luck;
        for (int i = 0; i < clause.Rolls.Length; i++)
        {
            var rollIndex = clause.Rolls[i];
            for (int j = 0; j < rollIndex; j++)
                ctx.GetNextGrosMichelExtinct(ref stream, luck);
            if (ctx.GetNextGrosMichelExtinct(ref stream, luck))
            {
                count++;
                if (max is null && count >= min)
                    return count;
            }

            int rollsRemaining = clause.Rolls.Length - 1 - i;
            if (count + rollsRemaining < min)
                return 0;
        }
        return count;
    }

    private static int CountSpaceLevelupOccurrences(
        ref MotelySingleSearchContext ctx,
        SpaceLevelupClause clause
    )
    {
        int count = 0;
        var stream = ctx.CreateSpacePrngStream();
        var min = clause.Min;
        var max = clause.Max;
        double luck = clause.Luck;
        for (int i = 0; i < clause.Rolls.Length; i++)
        {
            var rollIndex = clause.Rolls[i];
            for (int j = 0; j < rollIndex; j++)
                ctx.GetNextSpaceLevelup(ref stream, luck);
            if (ctx.GetNextSpaceLevelup(ref stream, luck))
            {
                count++;
                if (max is null && count >= min)
                    return count;
            }

            int rollsRemaining = clause.Rolls.Length - 1 - i;
            if (count + rollsRemaining < min)
                return 0;
        }
        return count;
    }

    private static int CountBusinessPayoutOccurrences(
        ref MotelySingleSearchContext ctx,
        BusinessPayoutClause clause
    )
    {
        int count = 0;
        var stream = ctx.CreateBusinessPrngStream();
        var min = clause.Min;
        var max = clause.Max;
        double luck = clause.Luck;
        for (int i = 0; i < clause.Rolls.Length; i++)
        {
            var rollIndex = clause.Rolls[i];
            for (int j = 0; j < rollIndex; j++)
                ctx.GetNextBusinessPayout(ref stream, luck);
            if (ctx.GetNextBusinessPayout(ref stream, luck))
            {
                count++;
                if (max is null && count >= min)
                    return count;
            }

            int rollsRemaining = clause.Rolls.Length - 1 - i;
            if (count + rollsRemaining < min)
                return 0;
        }
        return count;
    }

    private static int CountBloodstoneTriggerOccurrences(
        ref MotelySingleSearchContext ctx,
        BloodstoneTriggerClause clause
    )
    {
        int count = 0;
        var stream = ctx.CreateBloodstonePrngStream();
        var min = clause.Min;
        var max = clause.Max;
        double luck = clause.Luck;
        foreach (var rollIndex in clause.Rolls)
        {
            for (int i = 0; i < rollIndex; i++)
                ctx.GetNextBloodstoneTrigger(ref stream, luck);
            if (ctx.GetNextBloodstoneTrigger(ref stream, luck))
            {
                count++;
                if (max is null && count >= min)
                    return count;
            }
        }
        return count;
    }

    private static int CountParkingPayoutOccurrences(
        ref MotelySingleSearchContext ctx,
        ParkingPayoutClause clause
    )
    {
        int count = 0;
        var stream = ctx.CreateParkingPrngStream();
        var min = clause.Min;
        var max = clause.Max;
        double luck = clause.Luck;
        foreach (var rollIndex in clause.Rolls)
        {
            for (int i = 0; i < rollIndex; i++)
                ctx.GetNextParkingPayout(ref stream, luck);
            if (ctx.GetNextParkingPayout(ref stream, luck))
            {
                count++;
                if (max is null && count >= min)
                    return count;
            }
        }
        return count;
    }

    private static int CountGlassDestroyOccurrences(
        ref MotelySingleSearchContext ctx,
        GlassDestroyClause clause
    )
    {
        int count = 0;
        var stream = ctx.CreateGlassPrngStream();
        var min = clause.Min;
        var max = clause.Max;
        double luck = clause.Luck;
        foreach (var rollIndex in clause.Rolls)
        {
            for (int i = 0; i < rollIndex; i++)
                ctx.GetNextGlassDestroy(ref stream, luck);
            if (ctx.GetNextGlassDestroy(ref stream, luck))
            {
                count++;
                if (max is null && count >= min)
                    return count;
            }
        }
        return count;
    }

    private static int CountWheelStaysFlippedOccurrences(
        ref MotelySingleSearchContext ctx,
        WheelStaysFlippedClause clause
    )
    {
        int count = 0;
        var stream = ctx.CreateTheWheelPrngStream();
        var min = clause.Min;
        var max = clause.Max;
        double luck = clause.Luck;
        foreach (var rollIndex in clause.Rolls)
        {
            for (int i = 0; i < rollIndex; i++)
                ctx.GetNextWheelStaysFlipped(ref stream, luck);
            if (ctx.GetNextWheelStaysFlipped(ref stream, luck))
            {
                count++;
                if (max is null && count >= min)
                    return count;
            }
        }
        return count;
    }

    private static int CountLegendaryJokerOccurrences(
        ref MotelySingleSearchContext ctx,
        LegendaryJokerClause clause,
        ref MotelyRunState runState
    )
    {
        int count = 0;
        var sources = clause.Sources;
        int userMaxPack = sources.MaxReferencedBoosterSlot();

        foreach (int ante in clause.Antes)
        {
            int maxPack = ClampBoosterPackSlotForAnte(ante, userMaxPack, ref runState);
            count += LegendarySoulMatcher.CountAnte(ref ctx, ante, clause, maxPack);
        }

        return count;
    }

    private static int CountJokerOccurrences(
        ref MotelySingleSearchContext ctx,
        JokerClause clause,
        ref MotelyRunState runState
    )
    {
        return CountJokerClauseOccurrences(ref ctx, clause, ref runState);
    }

    private static int CountCommonJokerOccurrences(
        ref MotelySingleSearchContext ctx,
        CommonJokerClause clause,
        ref MotelyRunState runState
    )
    {
        if (clause.IsWildcard)
            return CountJokerOccurrencesWildcard(
                ref ctx,
                clause.Antes,
                clause.Sources,
                MotelyJokerRarity.Common,
                clause.Edition,
                clause.Stickers,
                ref runState
            );
        return CountJokerOccurrencesGeneric(
            ref ctx,
            clause.Antes,
            clause.Sources,
            clause.Jokers,
            clause.Edition,
            clause.Stickers,
            ref runState
        );
    }

    private static int CountUncommonJokerOccurrences(
        ref MotelySingleSearchContext ctx,
        UncommonJokerClause clause,
        ref MotelyRunState runState
    )
    {
        if (clause.IsWildcard)
            return CountJokerOccurrencesWildcard(
                ref ctx,
                clause.Antes,
                clause.Sources,
                MotelyJokerRarity.Uncommon,
                clause.Edition,
                clause.Stickers,
                ref runState
            );
        return CountJokerOccurrencesGeneric(
            ref ctx,
            clause.Antes,
            clause.Sources,
            clause.Jokers,
            clause.Edition,
            clause.Stickers,
            ref runState
        );
    }

    private static int CountRareJokerOccurrences(
        ref MotelySingleSearchContext ctx,
        RareJokerClause clause,
        ref MotelyRunState runState
    )
    {
        if (clause.IsWildcard)
            return CountJokerOccurrencesWildcard(
                ref ctx,
                clause.Antes,
                clause.Sources,
                MotelyJokerRarity.Rare,
                clause.Edition,
                clause.Stickers,
                ref runState
            );
        return CountJokerOccurrencesGeneric(
            ref ctx,
            clause.Antes,
            clause.Sources,
            clause.Jokers,
            clause.Edition,
            clause.Stickers,
            ref runState
        );
    }

    private static int CountMixedJokerOccurrences(
        ref MotelySingleSearchContext ctx,
        MixedJokerClause clause,
        ref MotelyRunState runState
    )
    {
        if (clause.IsWildcard)
            return CountJokerOccurrencesWildcard(
                ref ctx,
                clause.Antes,
                clause.Sources,
                wildcardRarity: null,
                clause.Edition,
                clause.Stickers,
                ref runState
            );
        return CountJokerOccurrencesGeneric(
            ref ctx,
            clause.Antes,
            clause.Sources,
            clause.Jokers,
            clause.Edition,
            clause.Stickers,
            ref runState
        );
    }

    internal static int CountJokerClauseOccurrencesForFilter(
        ref MotelySingleSearchContext ctx,
        JokerClause clause
    )
    {
        var runState = CreatePermissivePackRunState(clause.Antes);
        return CountJokerClauseOccurrences(ref ctx, clause, ref runState);
    }

    private static int CountJokerClauseOccurrences(
        ref MotelySingleSearchContext ctx,
        JokerClause clause,
        ref MotelyRunState runState
    )
    {
        if (clause.IsWildcard)
        {
            int normalWildcard = CountJokerOccurrencesWildcard(
                ref ctx,
                clause.Antes,
                clause.Sources,
                wildcardRarity: null,
                clause.Edition,
                clause.Stickers,
                ref runState
            );

            var legendaryWildcard = new LegendaryJokerClause
            {
                Label = clause.Label,
                Score = clause.Score,
                Jokers = [],
                IsWildcard = true,
                Edition = clause.Edition,
                Sources = clause.LegendarySources,
                Antes = clause.Antes,
                Min = clause.Min,
                Max = clause.Max,
            };

            return normalWildcard
                + CountLegendaryJokerOccurrences(ref ctx, legendaryWildcard, ref runState);
        }

        var nonLegendary = clause
            .Jokers.Where(static j =>
                ((MotelyJokerRarity)((int)j & MotelyGlobals.JokerRarityMask))
                != MotelyJokerRarity.Legendary
            )
            .ToArray();

        var legendary = clause
            .Jokers.Where(static j =>
                ((MotelyJokerRarity)((int)j & MotelyGlobals.JokerRarityMask))
                == MotelyJokerRarity.Legendary
            )
            .ToArray();

        int count = 0;

        if (nonLegendary.Length > 0)
        {
            count += CountJokerOccurrencesGeneric(
                ref ctx,
                clause.Antes,
                clause.Sources,
                nonLegendary,
                clause.Edition,
                clause.Stickers,
                ref runState
            );
        }

        if (legendary.Length > 0)
        {
            var legendaryClause = new LegendaryJokerClause
            {
                Label = clause.Label,
                Score = clause.Score,
                Jokers = legendary,
                IsWildcard = false,
                Edition = clause.Edition,
                Sources = clause.LegendarySources,
                Antes = clause.Antes,
                Min = clause.Min,
                Max = clause.Max,
            };

            count += CountLegendaryJokerOccurrences(ref ctx, legendaryClause, ref runState);
        }

        return count;
    }

    private static int CountJokerOccurrencesGeneric<TJoker>(
        ref MotelySingleSearchContext ctx,
        int[] antes,
        JokerSourceConfig sources,
        TJoker[] jokers,
        MotelyItemEdition? edition,
        MotelyJokerSticker[] stickers,
        ref MotelyRunState runState
    )
        where TJoker : struct, Enum
    {
        int count = 0;
        var shopItems = sources.ShopItems;
        var boosterPacks = sources.BoosterPacks;

        int maxShop = ArrayMax(shopItems);
        int userMaxPack = ArrayMax(boosterPacks);
        var targetTypes = new MotelyItemType[jokers.Length];
        for (int i = 0; i < jokers.Length; i++)
            targetTypes[i] = Enum.Parse<MotelyItemType>(jokers[i].ToString(), true);

        foreach (int ante in antes)
        {
            int maxPack = ClampBoosterPackSlotForAnte(ante, userMaxPack, ref runState);

            if (shopItems.Length > 0)
            {
                var shopStream = ctx.CreateShopItemStream(ante);
                for (int slot = 0; slot <= maxShop; slot++)
                {
                    var item = ctx.GetNextShopItem(ref shopStream);
                    if (!ArrayContains(shopItems, slot))
                        continue;
                    count += Scooped(
                        ref runState,
                        MatchJoker(item, targetTypes, edition, stickers),
                        MotelyMatchSource.Shop,
                        ante,
                        slot,
                        -1,
                        item
                    );
                }
            }

            if (boosterPacks.Length > 0)
            {
                var packStream = ctx.CreateBoosterPackStream(ante);
                var jokerStream = ctx.CreateBuffoonPackJokerStream(ante);
                for (int packIndex = 0; packIndex <= maxPack; packIndex++)
                {
                    var pack = ctx.GetNextBoosterPack(ref packStream);
                    if (pack.GetPackType() != MotelyBoosterPackType.Buffoon)
                        continue;
                    var contents = ctx.GetNextBuffoonPackContents(
                        ref jokerStream,
                        pack.GetPackSize()
                    );
                    if (!ArrayContains(boosterPacks, packIndex))
                        continue;
                    for (int i = 0; i < contents.Length; i++)
                    {
                        count += Scooped(
                            ref runState,
                            MatchJoker(contents[i], targetTypes, edition, stickers),
                            MotelyMatchSource.BoosterPack,
                            ante,
                            packIndex,
                            i,
                            contents[i]
                        );
                    }
                }
            }

            count += CountSpecialtyJokerSources(
                ref ctx,
                ante,
                sources,
                targetTypes,
                edition,
                stickers,
                ref runState
            );
        }

        return count;
    }

    private static int CountSpecialtyJokerSources(
        ref MotelySingleSearchContext ctx,
        int ante,
        JokerSourceConfig sources,
        MotelyItemType[] targetTypes,
        MotelyItemEdition? edition,
        MotelyJokerSticker[] stickers,
        ref MotelyRunState runState
    )
    {
        int count = 0;

        if (sources.Judgement.Length > 0)
        {
            int max = ArrayMax(sources.Judgement);
            var stream = ctx.CreateJudgementJokerStream(ante);
            for (int roll = 0; roll <= max; roll++)
            {
                var item = ctx.GetNextJoker(ref stream);
                if (ArrayContains(sources.Judgement, roll))
                {
                    int matches = MatchJoker(item, targetTypes, edition, stickers);
                    count += matches;
                }
            }
        }

        if (sources.Wraith.Length > 0)
        {
            int max = ArrayMax(sources.Wraith);
            var stream = ctx.CreateWraithJokerStream(ante);
            for (int roll = 0; roll <= max; roll++)
            {
                var item = ctx.GetNextJoker(ref stream);
                if (ArrayContains(sources.Wraith, roll))
                {
                    int matches = MatchJoker(item, targetTypes, edition, stickers);
                    count += matches;
                }
            }
        }

        if (sources.RiffRaff.Length > 0)
        {
            int max = ArrayMax(sources.RiffRaff);
            var stream = ctx.CreateRiffRaffJokerStream(ante);
            for (int roll = 0; roll <= max; roll++)
            {
                var item = ctx.GetNextJoker(ref stream);
                if (ArrayContains(sources.RiffRaff, roll))
                {
                    int matches = MatchJoker(item, targetTypes, edition, stickers);
                    count += matches;
                }
            }
        }

        if (sources.RareTag.Length > 0)
        {
            int max = ArrayMax(sources.RareTag);
            var stream = ctx.CreateRareTagJokerStream(ante);
            for (int roll = 0; roll <= max; roll++)
            {
                var item = ctx.GetNextJoker(ref stream);
                if (ArrayContains(sources.RareTag, roll))
                {
                    int matches = MatchJoker(item, targetTypes, edition, stickers);
                    count += matches;
                }
            }
        }

        if (sources.UncommonTag.Length > 0)
        {
            int max = ArrayMax(sources.UncommonTag);
            var stream = ctx.CreateUncommonTagJokerStream(ante);
            for (int roll = 0; roll <= max; roll++)
            {
                var item = ctx.GetNextJoker(ref stream);
                if (ArrayContains(sources.UncommonTag, roll))
                {
                    int matches = MatchJoker(item, targetTypes, edition, stickers);
                    count += matches;
                }
            }
        }

        return count;
    }

    private static int CountJokerOccurrencesWildcard(
        ref MotelySingleSearchContext ctx,
        int[] antes,
        JokerSourceConfig sources,
        MotelyJokerRarity? wildcardRarity,
        MotelyItemEdition? edition,
        MotelyJokerSticker[] stickers,
        ref MotelyRunState runState
    )
    {
        int count = 0;
        var shopItems = sources.ShopItems;
        var boosterPacks = sources.BoosterPacks;

        int maxShop = ArrayMax(shopItems);
        int userMaxPack = ArrayMax(boosterPacks);

        foreach (int ante in antes)
        {
            int maxPack = ClampBoosterPackSlotForAnte(ante, userMaxPack, ref runState);

            if (shopItems.Length > 0)
            {
                var shopStream = ctx.CreateShopItemStream(ante);
                for (int slot = 0; slot <= maxShop; slot++)
                {
                    var item = ctx.GetNextShopItem(ref shopStream);
                    if (!ArrayContains(shopItems, slot))
                        continue;
                    count += Scooped(
                        ref runState,
                        MatchJokerWildcard(item, wildcardRarity, edition, stickers),
                        MotelyMatchSource.Shop,
                        ante,
                        slot,
                        -1,
                        item
                    );
                }
            }

            if (boosterPacks.Length > 0)
            {
                var packStream = ctx.CreateBoosterPackStream(ante);
                var jokerStream = ctx.CreateBuffoonPackJokerStream(ante);
                for (int packIndex = 0; packIndex <= maxPack; packIndex++)
                {
                    var pack = ctx.GetNextBoosterPack(ref packStream);
                    if (pack.GetPackType() != MotelyBoosterPackType.Buffoon)
                        continue;
                    var contents = ctx.GetNextBuffoonPackContents(
                        ref jokerStream,
                        pack.GetPackSize()
                    );
                    if (!ArrayContains(boosterPacks, packIndex))
                        continue;
                    for (int i = 0; i < contents.Length; i++)
                    {
                        count += Scooped(
                            ref runState,
                            MatchJokerWildcard(contents[i], wildcardRarity, edition, stickers),
                            MotelyMatchSource.BoosterPack,
                            ante,
                            packIndex,
                            i,
                            contents[i]
                        );
                    }
                }
            }

            count += CountSpecialtyJokerSourcesWildcard(
                ref ctx,
                ante,
                sources,
                wildcardRarity,
                edition,
                stickers,
                ref runState
            );
        }

        return count;
    }

    private static int CountSpecialtyJokerSourcesWildcard(
        ref MotelySingleSearchContext ctx,
        int ante,
        JokerSourceConfig sources,
        MotelyJokerRarity? wildcardRarity,
        MotelyItemEdition? edition,
        MotelyJokerSticker[] stickers,
        ref MotelyRunState runState
    )
    {
        int count = 0;

        if (sources.Judgement.Length > 0)
        {
            int max = ArrayMax(sources.Judgement);
            var stream = ctx.CreateJudgementJokerStream(ante);
            for (int roll = 0; roll <= max; roll++)
            {
                var item = ctx.GetNextJoker(ref stream);
                if (ArrayContains(sources.Judgement, roll))
                {
                    int matches = MatchJokerWildcard(item, wildcardRarity, edition, stickers);
                    count += matches;
                }
            }
        }

        if (sources.Wraith.Length > 0)
        {
            int max = ArrayMax(sources.Wraith);
            var stream = ctx.CreateWraithJokerStream(ante);
            for (int roll = 0; roll <= max; roll++)
            {
                var item = ctx.GetNextJoker(ref stream);
                if (ArrayContains(sources.Wraith, roll))
                {
                    int matches = MatchJokerWildcard(item, wildcardRarity, edition, stickers);
                    count += matches;
                }
            }
        }

        if (sources.RiffRaff.Length > 0)
        {
            int max = ArrayMax(sources.RiffRaff);
            var stream = ctx.CreateRiffRaffJokerStream(ante);
            for (int roll = 0; roll <= max; roll++)
            {
                var item = ctx.GetNextJoker(ref stream);
                if (ArrayContains(sources.RiffRaff, roll))
                {
                    int matches = MatchJokerWildcard(item, wildcardRarity, edition, stickers);
                    count += matches;
                }
            }
        }

        if (sources.RareTag.Length > 0)
        {
            int max = ArrayMax(sources.RareTag);
            var stream = ctx.CreateRareTagJokerStream(ante);
            for (int roll = 0; roll <= max; roll++)
            {
                var item = ctx.GetNextJoker(ref stream);
                if (ArrayContains(sources.RareTag, roll))
                {
                    int matches = MatchJokerWildcard(item, wildcardRarity, edition, stickers);
                    count += matches;
                }
            }
        }

        if (sources.UncommonTag.Length > 0)
        {
            int max = ArrayMax(sources.UncommonTag);
            var stream = ctx.CreateUncommonTagJokerStream(ante);
            for (int roll = 0; roll <= max; roll++)
            {
                var item = ctx.GetNextJoker(ref stream);
                if (ArrayContains(sources.UncommonTag, roll))
                {
                    int matches = MatchJokerWildcard(item, wildcardRarity, edition, stickers);
                    count += matches;
                }
            }
        }

        return count;
    }

    private static int MatchJoker(
        MotelyItem item,
        MotelyItemType[] targetTypes,
        MotelyItemEdition? edition,
        MotelyJokerSticker[] stickers
    )
    {
        if (item.TypeCategory != MotelyItemTypeCategory.Joker)
            return 0;
        if (edition.HasValue && item.Edition != edition.Value)
            return 0;
        for (int i = 0; i < stickers.Length; i++)
        {
            bool hasSticker = stickers[i] switch
            {
                MotelyJokerSticker.Eternal => item.IsEternal,
                MotelyJokerSticker.Perishable => item.IsPerishable,
                MotelyJokerSticker.Rental => item.IsRental,
                _ => true,
            };
            if (!hasSticker)
                return 0;
        }

        int matches = 0;
        for (int i = 0; i < targetTypes.Length; i++)
            if (item.Type == targetTypes[i])
                matches++;
        return matches;
    }

    private static int MatchJokerWildcard(
        MotelyItem item,
        MotelyJokerRarity? wildcardRarity,
        MotelyItemEdition? edition,
        MotelyJokerSticker[] stickers
    )
    {
        if (item.TypeCategory != MotelyItemTypeCategory.Joker)
            return 0;
        if (
            wildcardRarity.HasValue
            && (MotelyJokerRarity)(item.Value & MotelyGlobals.JokerRarityMask)
                != wildcardRarity.Value
        )
            return 0;
        if (edition.HasValue && item.Edition != edition.Value)
            return 0;
        for (int i = 0; i < stickers.Length; i++)
        {
            bool hasSticker = stickers[i] switch
            {
                MotelyJokerSticker.Eternal => item.IsEternal,
                MotelyJokerSticker.Perishable => item.IsPerishable,
                MotelyJokerSticker.Rental => item.IsRental,
                _ => true,
            };
            if (!hasSticker)
                return 0;
        }
        return 1;
    }

    private static int MatchStandardCard(MotelyItem item, StandardCardClause clause)
    {
        if (item.TypeCategory != MotelyItemTypeCategory.Standardcard)
            return 0;
        if (clause.Rank.HasValue && item.StandardcardRank != clause.Rank.Value)
            return 0;
        if (clause.Suit.HasValue && item.StandardcardSuit != clause.Suit.Value)
            return 0;
        if (clause.Enhancement.HasValue && item.Enhancement != clause.Enhancement.Value)
            return 0;
        if (clause.Seal.HasValue && item.Seal != clause.Seal.Value)
            return 0;
        if (clause.Edition.HasValue && item.Edition != clause.Edition.Value)
            return 0;
        return 1;
    }

    private static int MatchTarot(MotelyItem item, TarotCardClause clause)
    {
        for (int i = 0; i < clause.Tarots.Length; i++)
            if (
                item.Type
                == (MotelyItemType)((int)MotelyItemTypeCategory.TarotCard | (int)clause.Tarots[i])
            )
                return 1;
        return 0;
    }

    private static int MatchSpectral(MotelyItem item, SpectralCardClause clause)
    {
        for (int i = 0; i < clause.Spectrals.Length; i++)
        {
            var Spectral = clause.Spectrals[i];
            if (
                item.Type
                == (MotelyItemType)((int)MotelyItemTypeCategory.SpectralCard | (int)Spectral)
            )
                return 1;
            if (Spectral == MotelySpectralCard.TheSoul && item.Type == MotelyItemType.TheSoul)
                return 1;
            if (Spectral == MotelySpectralCard.BlackHole && item.Type == MotelyItemType.BlackHole)
                return 1;
        }
        return 0;
    }

    private static int MatchPlanet(MotelyItem item, PlanetCardClause clause)
    {
        for (int i = 0; i < clause.Planets.Length; i++)
            if (
                item.Type
                == (MotelyItemType)((int)MotelyItemTypeCategory.PlanetCard | (int)clause.Planets[i])
            )
                return 1;
        return 0;
    }

    private static int GetMaxAnte(IJamlClause clause)
    {
        return clause switch
        {
            JokerClause c => ArrayMax(c.Antes),
            CommonJokerClause c => ArrayMax(c.Antes),
            UncommonJokerClause c => ArrayMax(c.Antes),
            RareJokerClause c => ArrayMax(c.Antes),
            MixedJokerClause c => ArrayMax(c.Antes),
            LegendaryJokerClause c => ArrayMax(c.Antes),
            VoucherClause c => ArrayMax(c.Antes),
            TarotCardClause c => ArrayMax(c.Antes),
            SpectralCardClause c => ArrayMax(c.Antes),
            PlanetCardClause c => ArrayMax(c.Antes),
            BossClause c => ArrayMax(c.Antes),
            TagClause c => ArrayMax(c.Antes),
            StandardCardClause c => ArrayMax(c.Antes),
            ErraticRankClause c => ArrayMax(c.Antes),
            ErraticSuitClause c => ArrayMax(c.Antes),
            ErraticCardClause c => ArrayMax(c.Antes),
            StartingDrawClause c => ArrayMax(c.Antes),
            AndClause c => MaxNestedAnte(c.Clauses),
            OrClause c => MaxNestedAnte(c.Clauses),
            _ => 0,
        };
    }

    private static int MaxNestedAnte(IJamlClause[] clauses)
    {
        int max = 0;
        for (int i = 0; i < clauses.Length; i++)
        {
            int nestedMax = GetMaxAnte(clauses[i]);
            if (nestedMax > max)
                max = nestedMax;
        }
        return max;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static MotelyRunState CreatePermissivePackRunState(int[] antes)
    {
        var runState = new MotelyRunState();
        for (int i = 0; i < antes.Length; i++)
            runState.ActivateExtendedPackAnte(antes[i]);
        return runState;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ClampBoosterPackSlotForAnte(
        int ante,
        int requestedMaxPack,
        ref MotelyRunState runState
    )
    {
        int anteMaxPack =
            ante == 1 && !runState.IsExtendedPackAnteActive(ante)
                ? MotelyGlobals.EarlyAnteMaxPackSlot
                : MotelyGlobals.LateAntesMaxPackSlot;
        return requestedMaxPack < anteMaxPack ? requestedMaxPack : anteMaxPack;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ArrayMax(int[] array)
    {
        if (array.Length == 0)
            return 0;
        int max = array[0];
        for (int i = 1; i < array.Length; i++)
            if (array[i] > max)
                max = array[i];
        return max;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ArrayContains(int[] array, int value)
    {
        for (int i = 0; i < array.Length; i++)
            if (array[i] == value)
                return true;
        return false;
    }
}
