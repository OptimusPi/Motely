using Motely.Filters;
using Xunit;

namespace Motely.Tests;

/// <summary>
/// Tests for JAML config parsing — verifies shorthand keys, source config mapping,
/// and strict handling of unknown YAML keys.
/// </summary>
public class JamlConfigTests
{
  [Fact]
  public void ValidJaml_ParsesSuccessfully()
  {
    var jaml = """
            name: Test
            must:
              - joker: Showman
                antes: [1,2]
                sources:
                  shopItems: [0,1]
                  boosterPacks: [0,1]
            """;

    var success = JamlConfigLoader.TryLoad(jaml, out var config, out var error);

    Assert.True(success, $"Failed to parse: {error}");
    Assert.NotNull(config);
    Assert.True(config!.Must.HasAnyClauses);
    Assert.Single(config.Must.Jokers);
  }

  [Fact]
  public void Sources_shopItems_AreMapped()
  {
    var jaml = """
            name: Test
            must:
              - joker: Showman
                antes: [1]
                sources:
                  shopItems: [0,1]
            """;

    var success = JamlConfigLoader.TryLoad(jaml, out var config, out var error);

    Assert.True(success, $"Parse should succeed (unknown keys are ignored): {error}");
    Assert.NotNull(config);
    Assert.Single(config!.Must.Jokers);
    Assert.Equal([0, 1], config.Must.Jokers[0].Sources.ShopItems);
  }

  [Fact]
  public void Sources_boosterPacks_AreMapped()
  {
    var jaml = """
            name: Test
            must:
              - joker: Showman
                antes: [1]
                sources:
                  boosterPacks: [0,1]
            """;

    var success = JamlConfigLoader.TryLoad(jaml, out var config, out var error);

    Assert.True(success, $"Parse should succeed (unknown keys are ignored): {error}");
    Assert.NotNull(config);
    Assert.Single(config!.Must.Jokers);
    Assert.Equal([0, 1], config.Must.Jokers[0].Sources.BoosterPacks);
  }

  [Fact]
  public void JokerRarityClauses_ParseIntoTypedLists()
  {
    // mixedJokers: removed in v14.0.2 — `jokers:` IS the mixed-rarity union list.
    var jaml = """
            name: TypedJokers
            must:
              - commonJoker: HalfJoker
              - uncommonJoker: Showman
              - rareJoker: Blueprint
              - jokers: [Blueprint, Showman]
              - legendaryJoker: Perkeo
                sources:
                  boosterPacks: [0,1,2,3]
            """;

    var success = JamlConfigLoader.TryLoad(jaml, out var config, out var error);

    Assert.True(success, $"Failed to parse: {error}");
    Assert.NotNull(config);
    Assert.Single(config!.Must.CommonJokers);
    Assert.Single(config.Must.UncommonJokers);
    Assert.Single(config.Must.RareJokers);
    Assert.Single(config.Must.Jokers);
    Assert.Single(config.Must.LegendaryJokers);
  }

  [Fact]
  public void Jokers_MixedLegendaryAndNonLegendary_StayInSingleGenericClause()
  {
    var jaml = """
            name: MixedGenericJokers
            must:
              - jokers: [Perkeo, Blueprint]
                antes: [1,2]
            """;

    var success = JamlConfigLoader.TryLoad(jaml, out var config, out var error);

    Assert.True(success, $"Failed to parse: {error}");
    Assert.NotNull(config);
    Assert.Single(config!.Must.Jokers);
    Assert.Equal(2, config.Must.Jokers[0].Jokers.Length);
    Assert.Empty(config.Must.LegendaryJokers);
  }

  [Fact]
  public void JokerSources_RawShopStreams_AreMapped()
  {
    var jaml = """
            name: RawStreams
            must:
              - uncommonJoker: Showman
                antes: [1]
                sources:
                  shopItems: [0,1]
                  boosterPacks: [0,1]
                  judgement: [0]
                  wraith: [0]
                  riffRaff: [0,1]
                  rareTag: [0]
                  uncommonTag: [0]
                  commonShopJokers: [0,2]
                  uncommonShopJokers: [1,3]
                  rareShopJokers: [4]
                  allShopJokers: [0,1,2,3,4]
            """;

    var success = JamlConfigLoader.TryLoad(jaml, out var config, out var error);

    Assert.True(success, $"Failed to parse: {error}");
    Assert.NotNull(config);
    var clause = config!.Must.UncommonJokers[0];
    Assert.Equal([0, 1], clause.Sources.ShopItems);
    Assert.Equal([0, 1], clause.Sources.BoosterPacks);
    Assert.Equal([0], clause.Sources.Judgement);
    Assert.Equal([0], clause.Sources.Wraith);
    Assert.Equal([0, 1], clause.Sources.RiffRaff);
    Assert.Equal([0], clause.Sources.RareTag);
    Assert.Equal([0], clause.Sources.UncommonTag);
    Assert.Equal([0, 2], clause.Sources.CommonShopJokers);
    Assert.Equal([1, 3], clause.Sources.UncommonShopJokers);
    Assert.Equal([4], clause.Sources.RareShopJokers);
    Assert.Equal([0, 1, 2, 3, 4], clause.Sources.AllShopJokers);
  }

  [Fact]
  public void LegendaryJoker_ParsesPerkeo()
  {
    var jaml = """
            name: Test
            must:
              - legendaryJoker: Perkeo
                antes: [1,2,3]
                sources:
                  boosterPacks: [0,1,2,3]
            """;

    var success = JamlConfigLoader.TryLoad(jaml, out var config, out var error);

    Assert.True(success, $"Failed to parse: {error}");
    Assert.NotNull(config);
    Assert.Single(config!.Must.LegendaryJokers);
  }

  [Fact]
  public void LegendaryJokers_Plural_ParsesMultipleValues()
  {
    var jaml = """
            name: Test
            must:
              - legendaryJokers: [Perkeo, Canio]
                antes: [1,2,3]
                sources:
                  boosterPacks: [0,1,2,3]
            """;

    var success = JamlConfigLoader.TryLoad(jaml, out var config, out var error);

    Assert.True(success, $"Failed to parse: {error}");
    Assert.NotNull(config);
    Assert.Single(config!.Must.LegendaryJokers);
    Assert.Equal(2, config.Must.LegendaryJokers[0].Jokers.Length);
  }

  [Fact]
  public void CanonicalPluralCardKeys_ParseSuccessfully()
  {
    var jaml = """
            name: CardPlurals
            must:
              - tarotCards: [TheFool, TheEmperor]
              - spectralCards: [Familiar, Aura]
              - standardCards: [HA, C2]
            """;

    var success = JamlConfigLoader.TryLoad(jaml, out var config, out var error);

    Assert.True(success, $"Failed to parse: {error}");
    Assert.NotNull(config);
    Assert.Single(config!.Must.TarotCards);
    Assert.Equal(2, config.Must.TarotCards[0].Tarots.Length);
    Assert.Single(config.Must.SpectralCards);
    Assert.Equal(2, config.Must.SpectralCards[0].Spectrals.Length);
    Assert.Single(config.Must.StandardCards);
    Assert.Equal(2, config.Must.StandardCards[0].Cards.Length);
  }

  [Fact]
  public void MustAndShould_BothParse()
  {
    var jaml = """
            name: Showman
            deck: Anaglyph
            stake: White
            must:
              - joker: Showman
                antes: [1,2]
                sources:
                  shopItems: [0,1]
                  boosterPacks: [0,1]
            should:
              - joker: Showman
                antes: [1,2]
                sources:
                  shopItems: [0,1]
                  boosterPacks: [0,1]
                score: 100
                max: 2
              - joker: OopsAll6s
                antes: [1,2,3]
                score: 1
            """;

    var success = JamlConfigLoader.TryLoad(jaml, out var config, out var error);

    Assert.True(success, $"Failed to parse: {error}");
    Assert.NotNull(config);
    Assert.True(config!.Must.HasAnyClauses);
    Assert.Equal(2, config.Should.Jokers.Count);
    Assert.Equal(2, config.Should.Jokers[0].Max);
  }

  [Fact]
  public void Max_CapsScoringCountWithoutChangingRawTallySemantics()
  {
    var clause = new JokerClause
    {
      Label = "Showman",
      Score = 100,
      Min = 1,
      Max = 2,
    };

    var rawTally = 4;
    var weighted = JamlScoring.CapScoreCountForTesting(rawTally, clause);

    Assert.Equal(4, rawTally);
    Assert.Equal(2, weighted);
    Assert.Equal(200, weighted * clause.Score);
  }

  [Fact]
  public void UnknownClauseKey_IsRejected()
  {
    var jaml = """
            name: Test
            must:
              - joker: Showman
                totallyFakeKey: 42
            """;

    var success = JamlConfigLoader.TryLoad(jaml, out var config, out var error);

    Assert.False(success);
    Assert.Null(config);
    Assert.NotNull(error);
    Assert.Contains("totallyFakeKey", error);
  }

  [Fact]
  public void UnknownNestedSourcesKey_IsRejected()
  {
    var jaml = """
            name: Test
            must:
              - joker: ScaryFace
                antes: [1]
                sources:
                  boosterPakcz: [0, 1]
            """;

    var success = JamlConfigLoader.TryLoad(jaml, out var config, out var error);

    Assert.False(success);
    Assert.Null(config);
    Assert.NotNull(error);
    Assert.Contains("on line", error);
    Assert.Contains("col", error);
    Assert.Contains("boosterPakcz", error);
    Assert.Contains("sources block", error);
  }

  [Fact]
  public void UnknownTopLevelKey_IsRejected()
  {
    var jaml = """
            name: Test
            madeUpTopLevel: 123
            must:
              - joker: Showman
            """;

    var success = JamlConfigLoader.TryLoad(jaml, out var config, out var error);

    Assert.False(success);
    Assert.Null(config);
    Assert.NotNull(error);
    Assert.Contains("on line", error);
    Assert.Contains("col", error);
    Assert.Contains("madeUpTopLevel", error);
    Assert.Contains("top-level JAML document", error);
  }

  [Fact]
  public void UnknownEventProperty_IsRejectedAtLoadTime()
  {
    var jaml = """
            name: EventTypo
            must:
              - event: LuckyMoney
                rolls: [0, 1]
                mint: 4
            """;

    var success = JamlConfigLoader.TryLoad(jaml, out var config, out var error);

    Assert.False(success);
    Assert.Null(config);
    Assert.NotNull(error);
    Assert.Contains("on line", error);
    Assert.Contains("col", error);
    Assert.Contains("mint", error);
    Assert.Contains("a clause", error);
  }

  [Fact]
  public void DeckAndStake_Parse()
  {
    var jaml = """
            name: DeckTest
            deck: Anaglyph
            stake: Gold
            must:
              - joker: Showman
                antes: [1]
            """;

    var success = JamlConfigLoader.TryLoad(jaml, out var config, out var error);

    Assert.True(success, $"Failed to parse: {error}");
    Assert.NotNull(config);
    Assert.Equal(MotelyDeck.Anaglyph, config!.Deck);
    Assert.Equal(MotelyStake.Gold, config.Stake);
  }

  [Fact]
  public void Metadata_Fields_ArePreserved()
  {
    var jaml = """
            name: MetaTest
            author: Cascade
            description: Metadata round-trip
            must:
              - joker: Showman
            """;

    var success = JamlConfigLoader.TryLoad(jaml, out var config, out var error);

    Assert.True(success, $"Failed to parse: {error}");
    Assert.NotNull(config);
    Assert.Equal("MetaTest", config!.Name);
    Assert.Equal("Cascade", config.Author);
    Assert.Equal("Metadata round-trip", config.Description);
  }

  [Fact]
  public void MustClauseLabels_AreLoadedInOrder()
  {
    var jaml = """
            name: LabelTest
            must:
              - label: First must
                joker: Showman
                antes: [1]
              - label: Second must
                bigBlindTag: CharmTag
                antes: [2]
            """;

    var success = JamlConfigLoader.TryLoad(jaml, out var config, out var error);

    Assert.True(success, $"Failed to parse: {error}");
    Assert.NotNull(config);

    var mustClauses = config!.Must.OrderedClauses;
    Assert.Equal(2, mustClauses.Count);
    Assert.Equal("First must", mustClauses[0].Label);
    Assert.Equal("Second must", mustClauses[1].Label);

    var plan = JamlSearchBuilder.CreatePlan(config);
    // Must-only configs report 0 tally columns: must-clause tallies gate execution
    // but are intentionally excluded from the CSV output (shouldOnly semantics).
    Assert.Equal(0, plan.ScoreTallyColumnCount);
  }

  [Fact]
  public void NestedLogicalAndClause_ParsesSuccessfully()
  {
    var jaml = """
            name: NestedAnd
            should:
              - label: Ante Pair
                mode: sum
                score: 100
                and:
                  - smallBlindTag: NegativeTag
                    antes: [2]
                  - bigBlindTag: CharmTag
                    antes: [2]
            """;

    var success = JamlConfigLoader.TryLoad(jaml, out var config, out var error);

    Assert.True(success, $"Failed to parse: {error}");
    Assert.NotNull(config);
    var clause = Assert.Single(config!.Should.OrderedClauses);
    var andClause = Assert.IsType<AndClause>(clause);
    Assert.Equal("Ante Pair", andClause.Label);
    Assert.Equal(2, andClause.Clauses.Length);
  }

  [Fact]
  public void NestedLogicalAndClause_WithClausesBlock_SharedAntesApplyToAllChildren()
  {
    var jaml = """
            name: LegacyClausesAntes
            must:
              - label: Shared
                and:
                  antes: [2, 3, 4]
                  clauses:
                    - smallBlindTag: NegativeTag
                    - uncommonJoker: OopsAll6s
                      edition: Negative
                      sources:
                        shopItems: [0, 1, 2]
            """;

    var success = JamlConfigLoader.TryLoad(jaml, out var config, out var error);

    Assert.True(success, $"Failed to parse: {error}");
    Assert.NotNull(config);
    var clause = Assert.Single(config!.Must.OrderedClauses);
    var andClause = Assert.IsType<AndClause>(clause);
    Assert.Equal(2, andClause.Clauses.Length);
    var tag = Assert.IsType<TagClause>(andClause.Clauses[0]);
    Assert.Equal(new[] { 2, 3, 4 }, tag.Antes);
    var joker = Assert.IsType<UncommonJokerClause>(andClause.Clauses[1]);
    Assert.Equal(new[] { 2, 3, 4 }, joker.Antes);
  }

  [Fact]
  public void EventClause_WithoutAntes_ParsesSuccessfully()
  {
    var jaml = """
            name: EventOk
            must:
              - event: LuckyMoney
                rolls: [0, 1]
            """;

    var success = JamlConfigLoader.TryLoad(jaml, out var config, out var error);

    Assert.True(success, $"Failed to parse: {error}");
    Assert.NotNull(config);
    var clause = Assert.Single(config!.Must.LuckyMoney);
    Assert.Equal([0, 1], clause.Rolls);
  }

  [Fact]
  public void EventClause_WithDirectEventKey_ParsesSuccessfully()
  {
    var jaml = """
            name: EventDirectKey
            must:
              - luckyMoney: [0, 1, 2, 3, 4, 5, 6, 7, 8]
            """;

    var success = JamlConfigLoader.TryLoad(jaml, out var config, out var error);

    Assert.True(success, $"Failed to parse: {error}");
    Assert.NotNull(config);
    var clause = Assert.Single(config!.Must.LuckyMoney);
    Assert.Equal([0, 1, 2, 3, 4, 5, 6, 7, 8], clause.Rolls);
  }

  [Fact]
  public void EventClause_WithMin_ParsesSuccessfully()
  {
    var jaml = """
            name: EventMin
            must:
              - event: LuckyMoney
                rolls: [0, 1, 2, 3, 4, 5, 6, 7, 8]
                min: 8
            """;

    var success = JamlConfigLoader.TryLoad(jaml, out var config, out var error);

    Assert.True(success, $"Failed to parse: {error}");
    Assert.NotNull(config);
    var clause = Assert.Single(config!.Must.LuckyMoney);
    Assert.Equal([0, 1, 2, 3, 4, 5, 6, 7, 8], clause.Rolls);
    Assert.Equal(8, clause.Min);
  }

  [Fact]
  public void EventClause_WithAntes_IsRejected()
  {
    var jaml = """
            name: EventBad
            must:
              - event: LuckyMoney
                antes: [1]
                rolls: [0]
            """;

    var success = JamlConfigLoader.TryLoad(jaml, out var config, out var error);

    Assert.False(success);
    Assert.Null(config);
    Assert.NotNull(error);
    Assert.Contains("Event clauses do not support 'antes'", error);
  }

  [Fact]
  public void EventClause_WithDefaultAntes_IsRejected()
  {
    var jaml = """
            name: EventBadDefaults
            defaults:
              antes: [2]
            must:
              - event: LuckyMoney
                rolls: [0]
            """;

    var success = JamlConfigLoader.TryLoad(jaml, out var config, out var error);

    Assert.False(success);
    Assert.Null(config);
    Assert.NotNull(error);
    Assert.Contains("Event clauses do not support 'antes'", error);
  }

  [Fact]
  public void EventClause_WithInheritedLogicAntes_IsRejected()
  {
    var jaml = """
            name: EventBadInherited
            must:
              - and:
                  antes: [3]
                  clauses:
                    - event: LuckyMoney
                      rolls: [0]
            """;

    var success = JamlConfigLoader.TryLoad(jaml, out var config, out var error);

    Assert.False(success);
    Assert.Null(config);
    Assert.NotNull(error);
    Assert.Contains("Event clauses do not support 'antes'", error);
  }
}

