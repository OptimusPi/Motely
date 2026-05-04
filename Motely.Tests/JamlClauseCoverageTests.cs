using Motely.Analysis;
using Motely.Filters;
using Xunit;

namespace Motely.Tests;

public class JammyClauseCoverageTests
{
    private const string Seed = "JAMMY";

    private static MotelySeedAnalysis AnalyzeJammy(MotelyDeck deck = MotelyDeck.Red)
    {
        var analysis = MotelySeedAnalyzer.Analyze(new MotelySeedAnalysisConfig(Seed, deck, MotelyStake.White));
        Assert.True(string.IsNullOrEmpty(analysis.Error), $"Analyzer failed for {Seed}/{deck}: {analysis.Error}");
        return analysis;
    }

    private static string BuildMustJaml(string clauseBody, MotelyDeck deck = MotelyDeck.Red)
    {
        var lines = clauseBody.Replace("\r\n", "\n").Trim().Split('\n');
        var clauseLines = lines
            .Select((line, idx) => idx == 0 ? $"  - {line.TrimEnd()}" : $"    {line.TrimEnd()}");

        return $$"""
            name: JammyClauseCoverage
            deck: {{deck}}
            stake: White
            must:
            {{string.Join('\n', clauseLines)}}
            """;
    }

    private static string BuildMustAndMustNotJaml(string clauseBody, MotelyDeck deck = MotelyDeck.Red)
    {
        var lines = clauseBody.Replace("\r\n", "\n").Trim().Split('\n');
        var clauseLines = lines
            .Select((line, idx) => idx == 0 ? $"  - {line.TrimEnd()}" : $"    {line.TrimEnd()}")
            .ToArray();
        var block = string.Join('\n', clauseLines);

        return $$"""
            name: JammyClauseCoverageNegative
            deck: {{deck}}
            stake: White
            must:
            {{block}}
            mustNot:
            {{block}}
            """;
    }

    private static (long SeedsSearched, long MatchingSeeds) RunSingleSeedJaml(
        string jaml,
        string seed = Seed
    )
    {
        Assert.True(
            JamlConfigLoader.TryLoad(jaml, out var config, out var error),
            $"JAML parse failed: {error}\n{jaml}"
        );

        var settings = JamlSearchBuilder
            .CreateSettings(config!)
            .WithListSearch([seed], 1)
            .WithThreadCount(1)
            .WithQuietMode(true);

        using var search = settings.Start();
        search.AwaitCompletion();
        return (search.TotalSeedsSearched, search.MatchingSeeds);
    }

    private static void AssertMatchesJammy(string jaml)
    {
        var result = RunSingleSeedJaml(jaml);
        Assert.Equal(1, result.SeedsSearched);
        Assert.Equal(1, result.MatchingSeeds);
    }

    private static void AssertRejectsJammy(string jaml)
    {
        var result = RunSingleSeedJaml(jaml);
        Assert.Equal(1, result.SeedsSearched);
        Assert.Equal(0, result.MatchingSeeds);
    }

    private static MotelyJokerRarity GetJokerRarity(MotelyItem item) =>
        (MotelyJokerRarity)((int)item.Type & (int)MotelyJokerRarity.Legendary);

    private static (int Ante, int ShopIndex, MotelyItem Item)? FindShopItem(
        MotelySeedAnalysis analysis,
        Func<MotelyItem, bool> predicate
    )
    {
        foreach (var ante in analysis.Antes)
        {
            for (int i = 0; i < ante.ShopQueue.Count; i++)
            {
                var item = ante.ShopQueue[i];
                if (predicate(item))
                    return (ante.Ante, i, item);
            }
        }

        return null;
    }

    private static (int Ante, int PackIndex, MotelyItem Item)? FindPackItem(
        MotelySeedAnalysis analysis,
        Func<MotelyItem, bool> predicate
    )
    {
        foreach (var ante in analysis.Antes)
        {
            for (int packIndex = 0; packIndex < ante.Packs.Count; packIndex++)
            {
                var pack = ante.Packs[packIndex];
                foreach (var item in pack.Items)
                {
                    if (predicate(item))
                        return (ante.Ante, packIndex, item);
                }
            }
        }

        return null;
    }

    [Fact]
    public void Clause_Joker_MatchesJammy()
    {
        var analysis = AnalyzeJammy();
        var hit = FindShopItem(analysis, i => i.TypeCategory == MotelyItemTypeCategory.Joker);
        Assert.True(hit.HasValue, "Expected at least one joker in JAMMY analysis");

        var d = hit!.Value;
        var clauseBody = $$"""
            joker: {{d.Item.Type}}
            antes: [{{d.Ante}}]
            sources:
              shopItems: [{{d.ShopIndex}}]
            """;
        var jaml = BuildMustJaml(clauseBody);
        AssertMatchesJammy(jaml);
        AssertRejectsJammy(BuildMustAndMustNotJaml(clauseBody));
    }

    [Fact]
    public void Clause_CommonJoker_MatchesJammy()
    {
        var analysis = AnalyzeJammy();
        var hit = FindShopItem(
            analysis,
            i => i.TypeCategory == MotelyItemTypeCategory.Joker && GetJokerRarity(i) == MotelyJokerRarity.Common
        );
        Assert.True(hit.HasValue, "Expected at least one common joker in JAMMY analysis");

        var d = hit!.Value;
        var clauseBody = $$"""
            commonJoker: {{d.Item.Type}}
            antes: [{{d.Ante}}]
            sources:
              shopItems: [{{d.ShopIndex}}]
            """;
        var jaml = BuildMustJaml(clauseBody);
        AssertMatchesJammy(jaml);
        AssertRejectsJammy(BuildMustAndMustNotJaml(clauseBody));
    }

    [Fact]
    public void Clause_UncommonJoker_MatchesJammy()
    {
        var analysis = AnalyzeJammy();
        var hit = FindShopItem(
            analysis,
            i => i.TypeCategory == MotelyItemTypeCategory.Joker && GetJokerRarity(i) == MotelyJokerRarity.Uncommon
        );
        Assert.True(hit.HasValue, "Expected at least one uncommon joker in JAMMY analysis");

        var d = hit!.Value;
        var clauseBody = $$"""
            uncommonJoker: {{d.Item.Type}}
            antes: [{{d.Ante}}]
            sources:
              shopItems: [{{d.ShopIndex}}]
            """;
        var jaml = BuildMustJaml(clauseBody);
        AssertMatchesJammy(jaml);
        AssertRejectsJammy(BuildMustAndMustNotJaml(clauseBody));
    }

    [Fact]
    public void Clause_RareJoker_MatchesJammy()
    {
        MotelyJokerRare? matching = null;
        foreach (var joker in Enum.GetValues<MotelyJokerRare>())
        {
            var jaml = BuildMustJaml($"rareJoker: {joker}");
            if (RunSingleSeedJaml(jaml).MatchingSeeds == 1)
            {
                matching = joker;
                break;
            }
        }

        Assert.True(matching.HasValue, $"Expected at least one rareJoker clause to match seed {Seed}.");
        AssertRejectsJammy(BuildMustAndMustNotJaml($"rareJoker: {matching!.Value}"));
    }

    [Fact]
    public void Clause_Joker_MatchesJammy_MixedRarity()
    {
        // v14.0.2: `joker:` IS the mixed-rarity union (replaces removed `joker:`).
        var analysis = AnalyzeJammy();
        var hit = FindShopItem(analysis, i => i.TypeCategory == MotelyItemTypeCategory.Joker);
        Assert.True(hit.HasValue, "Expected at least one joker in JAMMY analysis");

        var d = hit!.Value;
        var clauseBody = $$"""
            joker: {{d.Item.Type}}
            antes: [{{d.Ante}}]
            sources:
              shopItems: [{{d.ShopIndex}}]
            """;
        var jaml = BuildMustJaml(clauseBody);
        AssertMatchesJammy(jaml);
        AssertRejectsJammy(BuildMustAndMustNotJaml(clauseBody));
    }

    [Fact]
    public void Clause_LegendaryJoker_ExecutesForJammy()
    {
        var legendary = Enum
            .GetValues<MotelyJoker>()
            .First(j => GetJokerRarity(new MotelyItem(j)) == MotelyJokerRarity.Legendary);

        var clauseBody = $"legendaryJoker: {legendary}";
        var jaml = BuildMustJaml(clauseBody);
        var result = RunSingleSeedJaml(jaml);
        Assert.Equal(1, result.SeedsSearched);
        AssertRejectsJammy(BuildMustAndMustNotJaml(clauseBody));
    }

    [Fact]
    public void Clause_Voucher_MatchesJammy()
    {
        var analysis = AnalyzeJammy();
        var ante = analysis.Antes[0];

        var clauseBody = $$"""
            voucher: {{ante.Voucher}}
            antes: [{{ante.Ante}}]
            """;
        var jaml = BuildMustJaml(clauseBody);
        AssertMatchesJammy(jaml);
        AssertRejectsJammy(BuildMustAndMustNotJaml(clauseBody));
    }

    [Fact]
    public void Clause_Tarot_MatchesJammy()
    {
        var analysis = AnalyzeJammy();
        var hit = FindShopItem(analysis, i => i.TypeCategory == MotelyItemTypeCategory.TarotCard);
        Assert.True(hit.HasValue, "Expected at least one tarot card in JAMMY shop queue");

        var d = hit!.Value;
        var clauseBody = $$"""
            tarotCard: {{d.Item.Type}}
            antes: [{{d.Ante}}]
            sources:
              shopItems: [{{d.ShopIndex}}]
            """;
        var jaml = BuildMustJaml(clauseBody);
        AssertMatchesJammy(jaml);
        AssertRejectsJammy(BuildMustAndMustNotJaml(clauseBody));
    }

    [Fact]
    public void Clause_Spectral_MatchesJammy()
    {
        var analysis = AnalyzeJammy();
        var shopHit = FindShopItem(analysis, i => i.TypeCategory == MotelyItemTypeCategory.SpectralCard);
        if (shopHit.HasValue)
        {
            var d = shopHit.Value;
            var clauseBody = $$"""
                spectralCard: {{d.Item.Type}}
                antes: [{{d.Ante}}]
                sources:
                  shopItems: [{{d.ShopIndex}}]
                """;
            var jaml = BuildMustJaml(clauseBody);
            AssertMatchesJammy(jaml);
            AssertRejectsJammy(BuildMustAndMustNotJaml(clauseBody));
            return;
        }

        var packHit = FindPackItem(analysis, i => i.TypeCategory == MotelyItemTypeCategory.SpectralCard);
        Assert.True(packHit.HasValue, "Expected at least one spectral card in JAMMY packs");
        var p = packHit!.Value;
        var packClauseBody = $$"""
            spectralCard: {{p.Item.Type}}
            antes: [{{p.Ante}}]
            sources:
              boosterPacks: [{{p.PackIndex}}]
            """;
        var packJaml = BuildMustJaml(packClauseBody);
        AssertMatchesJammy(packJaml);
        AssertRejectsJammy(BuildMustAndMustNotJaml(packClauseBody));
    }

    [Fact]
    public void Clause_Planet_MatchesJammy()
    {
        var analysis = AnalyzeJammy();
        var hit = FindShopItem(analysis, i => i.TypeCategory == MotelyItemTypeCategory.PlanetCard);
        Assert.True(hit.HasValue, "Expected at least one planet card in JAMMY shop queue");

        var d = hit!.Value;
        var clauseBody = $$"""
            planet: {{d.Item.Type}}
            antes: [{{d.Ante}}]
            sources:
              shopItems: [{{d.ShopIndex}}]
            """;
        var jaml = BuildMustJaml(clauseBody);
        AssertMatchesJammy(jaml);
        AssertRejectsJammy(BuildMustAndMustNotJaml(clauseBody));
    }

    [Fact]
    public void Clause_Boss_MatchesJammy()
    {
        var analysis = AnalyzeJammy();
        var ante = analysis.Antes[0];

        var clauseBody = $$"""
            boss: {{ante.Boss}}
            antes: [{{ante.Ante}}]
            """;
        var jaml = BuildMustJaml(clauseBody);
        AssertMatchesJammy(jaml);
        AssertRejectsJammy(BuildMustAndMustNotJaml(clauseBody));
    }

    [Fact]
    public void Clause_TagAndBlindTags_MatchJammy()
    {
        var analysis = AnalyzeJammy();
        var ante = analysis.Antes[0];

        var anyTagBody = $$"""
            tag: {{ante.SmallBlindTag}}
            antes: [{{ante.Ante}}]
            """;
        var anyTagJaml = BuildMustJaml(anyTagBody);
        AssertMatchesJammy(anyTagJaml);
        AssertRejectsJammy(BuildMustAndMustNotJaml(anyTagBody));

        var smallTagBody = $$"""
            smallBlindTag: {{ante.SmallBlindTag}}
            antes: [{{ante.Ante}}]
            """;
        var smallTagJaml = BuildMustJaml(smallTagBody);
        AssertMatchesJammy(smallTagJaml);
        AssertRejectsJammy(BuildMustAndMustNotJaml(smallTagBody));

        var bigTagBody = $$"""
            bigBlindTag: {{ante.BigBlindTag}}
            antes: [{{ante.Ante}}]
            """;
        var bigTagJaml = BuildMustJaml(bigTagBody);
        AssertMatchesJammy(bigTagJaml);
        AssertRejectsJammy(BuildMustAndMustNotJaml(bigTagBody));
    }

    [Fact]
    public void Clause_StandardCard_MatchesJammy()
    {
        var analysis = AnalyzeJammy();
        var hit = FindPackItem(analysis, i => i.TypeCategory == MotelyItemTypeCategory.Standardcard);
        Assert.True(hit.HasValue, "Expected at least one standard card in JAMMY booster packs");

        var d = hit!.Value;
        var clauseBody = $$"""
            standardCard: {{d.Item.Type}}
            antes: [{{d.Ante}}]
            sources:
              boosterPacks: [{{d.PackIndex}}]
            """;
        var jaml = BuildMustJaml(clauseBody);
        AssertMatchesJammy(jaml);
        AssertRejectsJammy(BuildMustAndMustNotJaml(clauseBody));
    }

    [Fact]
    public void Clause_StartingDraw_HasAtLeastOneMatchingCardForJammy()
    {
        MotelyStandardCard? matchingCard = null;

        foreach (var card in Enum.GetValues<MotelyStandardCard>())
        {
            var jaml = BuildMustJaml($"startingDraw: {card}");
            var result = RunSingleSeedJaml(jaml);
            if (result.MatchingSeeds == 1)
            {
                matchingCard = card;
                break;
            }
        }

        Assert.True(
            matchingCard.HasValue,
            "Expected at least one startingDraw card to match JAMMY."
        );
        AssertRejectsJammy(BuildMustAndMustNotJaml($"startingDraw: {matchingCard!.Value}"));
    }

    [Fact]
    public void Clause_ErraticRankSuitAndCard_HaveMatchesForJammyErraticDeck()
    {
        MotelyStandardcardRank? matchingRank = null;
        foreach (var rank in Enum.GetValues<MotelyStandardcardRank>())
        {
            var jaml = BuildMustJaml($"erraticRank: {rank}", MotelyDeck.Erratic);
            if (RunSingleSeedJaml(jaml).MatchingSeeds == 1)
            {
                matchingRank = rank;
                break;
            }
        }
        Assert.True(matchingRank.HasValue, "Expected at least one erraticRank to match JAMMY/Erratic.");
        AssertRejectsJammy(
            BuildMustAndMustNotJaml($"erraticRank: {matchingRank!.Value}", MotelyDeck.Erratic)
        );

        MotelyStandardcardSuit? matchingSuit = null;
        foreach (var suit in Enum.GetValues<MotelyStandardcardSuit>())
        {
            var jaml = BuildMustJaml($"erraticSuit: {suit}", MotelyDeck.Erratic);
            if (RunSingleSeedJaml(jaml).MatchingSeeds == 1)
            {
                matchingSuit = suit;
                break;
            }
        }
        Assert.True(matchingSuit.HasValue, "Expected at least one erraticSuit to match JAMMY/Erratic.");
        AssertRejectsJammy(
            BuildMustAndMustNotJaml($"erraticSuit: {matchingSuit!.Value}", MotelyDeck.Erratic)
        );

        MotelyStandardCard? matchingCard = null;
        foreach (var card in Enum.GetValues<MotelyStandardCard>())
        {
            var jaml = BuildMustJaml($"erraticCard: {card}", MotelyDeck.Erratic);
            if (RunSingleSeedJaml(jaml).MatchingSeeds == 1)
            {
                matchingCard = card;
                break;
            }
        }
        Assert.True(matchingCard.HasValue, "Expected at least one erraticCard to match JAMMY/Erratic.");
        AssertRejectsJammy(
            BuildMustAndMustNotJaml($"erraticCard: {matchingCard!.Value}", MotelyDeck.Erratic)
        );
    }

    [Fact]
    public void Clause_EventTypes_ExecuteAndCoreEventsFindAMatchingRoll()
    {
        foreach (var eventType in Enum.GetValues<MotelyEventType>())
        {
            // Baseline: clause parses and executes (logic path reached).
            var baselineBody = $$"""
                event: {{eventType}}
                rolls: [0]
                """;
            var baselineJaml = BuildMustJaml(baselineBody);
            var baseline = RunSingleSeedJaml(baselineJaml);
            Assert.Equal(1, baseline.SeedsSearched);
            AssertRejectsJammy(BuildMustAndMustNotJaml(baselineBody));
        }

        // Stronger positive checks for the common events.
        foreach (
            var eventType in new[]
            {
                MotelyEventType.LuckyMoney,
                MotelyEventType.LuckyMult,
                MotelyEventType.MisprintMult,
                MotelyEventType.WheelOfFortune,
            }
        )
        {
            int? matchRoll = null;
            for (int roll = 0; roll < 128; roll++)
            {
                var jaml = BuildMustJaml(
                    $$"""
                    event: {{eventType}}
                    rolls: [{{roll}}]
                    """
                );
                if (RunSingleSeedJaml(jaml).MatchingSeeds == 1)
                {
                    matchRoll = roll;
                    break;
                }
            }

            Assert.True(
                matchRoll.HasValue,
                $"Expected at least one roll index to match event {eventType} for seed {Seed}."
            );
            AssertRejectsJammy(
                BuildMustAndMustNotJaml(
                    $$"""
                    event: {{eventType}}
                    rolls: [{{matchRoll!.Value}}]
                    """
                )
            );
        }
    }
}
