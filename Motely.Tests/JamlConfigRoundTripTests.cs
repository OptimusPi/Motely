using System.Text.Json;
using Motely.Filters;

namespace Motely.Tests;

public class JamlConfigRoundTripTests
{
    // ── JSON → JAML → JSON → POCO round-trip ──

    [Fact]
    public void JsonRoundTrip_SimpleJoker()
    {
        var jsonA = """
        {
            "version": "2.0.0",
            "name": "SimpleJoker",
            "deck": "Red",
            "stake": "White",
            "must": [
                { "joker": "Blueprint" }
            ]
        }
        """;

        // JSON → POCO
        var configA = JamlConfigLoader.FromJson(jsonA);
        Assert.NotNull(configA);
        Assert.Equal("2.0.0", configA.Version);
        Assert.Equal("SimpleJoker", configA.Name);
        Assert.Equal(JamlDeck.Red, configA.Deck);
        Assert.Single(configA.Must);
        Assert.Equal(JamlClauseType.Joker, configA.Must[0].Type);
        Assert.Equal("Blueprint", configA.Must[0].Value);

        // POCO → JSON → POCO again
        var jsonB = JsonSerializer.Serialize(configA, JamlJsonContext.Default.JamlConfig);
        var configB = JamlConfigLoader.FromJson(jsonB);
        Assert.Equal(configA.Name, configB.Name);
        Assert.Equal(configA.Deck, configB.Deck);
        Assert.Equal(configA.Must[0].Type, configB.Must[0].Type);
        Assert.Equal(configA.Must[0].Value, configB.Must[0].Value);
    }

    [Fact]
    public void JamlRoundTrip_SimpleJoker()
    {
        var jamlA = """
            name: SimpleJoker
            deck: Red
            stake: White
            must:
            - joker: Blueprint
            """;

        // JAML → POCO
        var ok = JamlConfigLoader.TryLoadFromJamlString(jamlA, out var configA, out var error);
        Assert.True(ok, error);
        Assert.NotNull(configA);
        Assert.Equal("SimpleJoker", configA!.Name);
        Assert.Single(configA.Must);
        Assert.Equal(JamlClauseType.Joker, configA.Must[0].Type);
        Assert.Equal("Blueprint", configA.Must[0].Value);

        // POCO → JSON → POCO
        var json = JsonSerializer.Serialize(configA, JamlJsonContext.Default.JamlConfig);
        var configB = JamlConfigLoader.FromJson(json);
        Assert.Equal(configA.Name, configB.Name);
        Assert.Equal(configA.Must[0].Type, configB.Must[0].Type);
        Assert.Equal(configA.Must[0].Value, configB.Must[0].Value);
    }

    // ── Plural shortcuts expand to OR ──

    [Fact]
    public void PluralShortcut_ExpandsToOr()
    {
        var jaml = """
            name: PluralTest
            must:
            - jokers: [Blueprint, Brainstorm]
            """;

        var ok = JamlConfigLoader.TryLoadFromJamlString(jaml, out var config, out var error);
        Assert.True(ok, error);
        Assert.NotNull(config);

        // The plural shortcut should expand into an Or clause
        var clause = config!.Must[0];
        Assert.Equal(JamlClauseType.Or, clause.Type);
        Assert.NotNull(clause.Clauses);
        Assert.Equal(2, clause.Clauses!.Count);
        Assert.Equal(JamlClauseType.Joker, clause.Clauses[0].Type);
        Assert.Equal("Blueprint", clause.Clauses[0].Value);
        Assert.Equal(JamlClauseType.Joker, clause.Clauses[1].Type);
        Assert.Equal("Brainstorm", clause.Clauses[1].Value);
    }

    // ── Source canonicalization ──

    [Fact]
    public void SourceCanonicalization_JokerWithJudgement()
    {
        var json = """
        {
            "must": [
                {
                    "joker": "Blueprint",
                    "sources": [
                        { "judgement": [0, 1, 2], "antes": [1, 2] },
                        { "shopItems": [0, 1, 2, 3], "antes": [1, 2, 3] }
                    ]
                }
            ]
        }
        """;

        var config = JamlConfigLoader.FromJson(json);
        var clause = config.Must[0];
        Assert.Equal(JamlClauseType.Joker, clause.Type);
        Assert.NotNull(clause.SourceConfigs);
        Assert.Equal(2, clause.SourceConfigs!.Count);

        // First source: JokerSourceConfig with judgement rolls
        var src0 = Assert.IsType<JokerSourceConfig>(clause.SourceConfigs[0]);
        Assert.Equal([0, 1, 2], src0.Judgement);
        Assert.Equal([1, 2], src0.Antes);
        Assert.Empty(src0.ShopItems);

        // Second source: JokerSourceConfig with shop items
        var src1 = Assert.IsType<JokerSourceConfig>(clause.SourceConfigs[1]);
        Assert.Equal([0, 1, 2, 3], src1.ShopItems);
        Assert.Equal([1, 2, 3], src1.Antes);
        Assert.Empty(src1.Judgement);
    }

    [Fact]
    public void SourceCanonicalization_StandardCardWithDeckDraw()
    {
        var json = """
        {
            "must": [
                {
                    "standardCard": "KH",
                    "sources": [
                        { "deckDraw": [0, 1, 2, 3] }
                    ]
                }
            ]
        }
        """;

        var config = JamlConfigLoader.FromJson(json);
        var clause = config.Must[0];
        Assert.Equal(JamlClauseType.StandardCard, clause.Type);
        Assert.NotNull(clause.SourceConfigs);

        var src = Assert.IsType<StandardCardSourceConfig>(clause.SourceConfigs![0]);
        Assert.Equal([0, 1, 2, 3], src.DeckDraw);
        Assert.Empty(src.ShopItems);
        Assert.Empty(src.Certificate);
    }

    [Fact]
    public void SourceCanonicalization_SoulJoker_NoShopItems()
    {
        var json = """
        {
            "must": [
                {
                    "soulJoker": "Perkeo",
                    "sources": [
                        { "boosterPacks": [0, 1, 2], "soulCard": [0] }
                    ]
                }
            ]
        }
        """;

        var config = JamlConfigLoader.FromJson(json);
        var clause = config.Must[0];
        var src = Assert.IsType<SoulJokerSourceConfig>(clause.SourceConfigs![0]);
        Assert.Empty(src.ShopItems); // legendary jokers never in shop
        Assert.Equal([0, 1, 2], src.BoosterPacks);
        Assert.Equal([0], src.SoulCard);
    }

    [Fact]
    public void SourceCanonicalization_SpectralCard()
    {
        var json = """
        {
            "must": [
                {
                    "spectral": "Wraith",
                    "sources": [
                        { "boosterPacks": [0, 1], "sixthSense": [0, 1, 2], "seance": [0] }
                    ]
                }
            ]
        }
        """;

        var config = JamlConfigLoader.FromJson(json);
        var clause = config.Must[0];
        var src = Assert.IsType<SpectralCardSourceConfig>(clause.SourceConfigs![0]);
        Assert.Equal([0, 1], src.BoosterPacks);
        Assert.Equal([0, 1, 2], src.SixthSense);
        Assert.Equal([0], src.Seance);
    }

    // ── Legacy flat shopItems/boosterPacks merge into sources ──

    [Fact]
    public void LegacyFlatProps_MergedIntoSources()
    {
        var json = """
        {
            "must": [
                {
                    "joker": "Blueprint",
                    "shopItems": [0, 1],
                    "boosterPacks": [0, 1, 2]
                }
            ]
        }
        """;

        var config = JamlConfigLoader.FromJson(json);
        var clause = config.Must[0];
        Assert.NotNull(clause.SourceConfigs);
        Assert.Single(clause.SourceConfigs!);

        var src = Assert.IsType<JokerSourceConfig>(clause.SourceConfigs[0]);
        Assert.Equal([0, 1], src.ShopItems);
        Assert.Equal([0, 1, 2], src.BoosterPacks);
    }

    // ── No sources = no SourceConfigs ──

    [Fact]
    public void NoSources_NullSourceConfigs()
    {
        var json = """
        {
            "must": [
                { "joker": "Blueprint" }
            ]
        }
        """;

        var config = JamlConfigLoader.FromJson(json);
        Assert.Null(config.Must[0].SourceConfigs);
    }

    // ── Type-as-key aliases ──

    [Fact]
    public void TypeAsKey_TarotAlias()
    {
        var json = """
        {
            "must": [
                { "tarot": "Judgement" }
            ]
        }
        """;

        var config = JamlConfigLoader.FromJson(json);
        Assert.Equal(JamlClauseType.TarotCard, config.Must[0].Type);
        Assert.Equal("Judgement", config.Must[0].Value);
    }

    [Fact]
    public void TypeAsKey_SpectralAlias()
    {
        var json = """
        {
            "must": [
                { "spectral": "Wraith" }
            ]
        }
        """;

        var config = JamlConfigLoader.FromJson(json);
        Assert.Equal(JamlClauseType.SpectralCard, config.Must[0].Type);
        Assert.Equal("Wraith", config.Must[0].Value);
    }

    // ── And/Or nested clauses ──

    [Fact]
    public void AndClause_NestedCriteria()
    {
        var json = """
        {
            "must": [
                {
                    "and": [
                        { "joker": "Blueprint", "antes": [2] },
                        { "smallBlindTag": "NegativeTag", "antes": [2] }
                    ],
                    "score": 100
                }
            ]
        }
        """;

        var config = JamlConfigLoader.FromJson(json);
        var clause = config.Must[0];
        Assert.Equal(JamlClauseType.And, clause.Type);
        Assert.Equal(100, clause.Score);
        Assert.NotNull(clause.Clauses);
        Assert.Equal(2, clause.Clauses!.Count);
        Assert.Equal(JamlClauseType.Joker, clause.Clauses[0].Type);
        Assert.Equal(JamlClauseType.SmallBlindTag, clause.Clauses[1].Type);
    }

    // ── Version defaulting ──

    [Fact]
    public void VersionDefaults_To200()
    {
        var json = """
        {
            "must": [
                { "joker": "Blueprint" }
            ]
        }
        """;

        var config = JamlConfigLoader.FromJson(json);
        Assert.Equal("2.0.0", config.Version);
    }

    // ── SourceConfig init properties guarantee no nulls ──

    [Fact]
    public void SourceConfig_NoNulls()
    {
        var src = new JokerSourceConfig();
        Assert.NotNull(src.Antes);
        Assert.NotNull(src.ShopItems);
        Assert.NotNull(src.BoosterPacks);
        Assert.NotNull(src.Judgement);
        Assert.NotNull(src.RiffRaff);
        Assert.NotNull(src.Wraith);
        Assert.Empty(src.Antes);
        Assert.Empty(src.ShopItems);

        var std = new StandardCardSourceConfig();
        Assert.NotNull(std.Certificate);
        Assert.NotNull(std.Incantation);
        Assert.NotNull(std.Familiar);
        Assert.NotNull(std.Grim);
        Assert.NotNull(std.DeckDraw);
    }
}
