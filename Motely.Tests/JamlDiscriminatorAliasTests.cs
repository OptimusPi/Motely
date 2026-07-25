using Motely.Filters.Jaml;
using Xunit;

namespace Motely.Tests;

/// <summary>
/// Every wire discriminator generated into <see cref="JamlSchema"/> must be recognised by the
/// loader. Prevents a second hand list from drifting away from the attribute-driven schema.
/// </summary>
public sealed class JamlDiscriminatorAliasTests
{
    public static TheoryData<string> AllDiscriminators()
    {
        var data = new TheoryData<string>();
        foreach (var key in JamlSchema.Discriminators)
            data.Add(key);
        return data;
    }

    [Theory]
    [MemberData(nameof(AllDiscriminators))]
    public void EverySchemaDiscriminator_IsRecognisedByTheLoader(string discriminator)
    {
        // A clause whose only key is the discriminator. Some kinds will still reject this
        // minimal shape for a missing value — that's fine; the one error that must never
        // appear is the loader failing to recognise the discriminator at all.
        var jaml = $"must:\n  - {discriminator}: 1\n";
        JamlConfigLoader.TryLoad(jaml, out _, out var error);

        Assert.False(
            error?.Contains("no recognised discriminator", StringComparison.OrdinalIgnoreCase) ?? false,
            $"'{discriminator}' is in JamlSchema but the loader does not recognise it: {error}");
    }

    [Theory]
    [InlineData("tags", "[NegativeTag, CharmTag]")]
    [InlineData("tarotCards", "[TheFool]")]
    [InlineData("spectralCards", "[Ankh]")]
    [InlineData("planetCards", "[Pluto]")]
    public void PluralItemAliases_ParseToTheSameClauseAsTheSingular(string discriminator, string value)
    {
        var loaded = JamlConfigLoader.TryLoad($"must:\n  - {discriminator}: {value}\n", out var config, out var error);

        Assert.True(loaded, $"'{discriminator}' failed to load: {error}");
        Assert.NotNull(config);
        Assert.Single(config.Must);
    }

    [Theory]
    [InlineData("tag", new[] { 0, 1 })]
    [InlineData("tags", new[] { 0, 1 })]
    [InlineData("smallBlindTag", new[] { 0 })]
    [InlineData("bigBlindTag", new[] { 1 })]
    public void TagWire_DefaultRolls_MatchBlindSemantics(string discriminator, int[] expectedRolls)
    {
        Assert.Equal(expectedRolls, JamlSchema.RollsDefaultFor(discriminator));

        var loaded = JamlConfigLoader.TryLoad(
            $"must:\n  - {discriminator}: NegativeTag\n",
            out var config,
            out var error);

        Assert.True(loaded, $"'{discriminator}' failed to load: {error}");
        var tag = Assert.IsType<TagClause>(Assert.Single(config!.Must));
        Assert.Equal(expectedRolls, tag.Rolls);
    }

    /// <summary>
    /// T1 lock: every schema wire is known, and non-logic wires resolve to an IJamlClause type
    /// the loader can construct. And/Or are logic bags (not IJamlClauseDesc families).
    /// </summary>
    [Fact]
    public void EverySchemaWire_IsKnown_AndNonLogicResolvesToClauseType()
    {
        Assert.NotEmpty(JamlSchema.Discriminators);
        foreach (var d in JamlSchema.Discriminators)
        {
            Assert.True(
                JamlSchema.IsKnownDiscriminator(d),
                $"'{d}' is listed in Discriminators but IsKnownDiscriminator returned false.");

            if (string.Equals(d, "and", StringComparison.OrdinalIgnoreCase)
                || string.Equals(d, "or", StringComparison.OrdinalIgnoreCase))
                continue;

            var clauseType = JamlSchema.ClauseTypeFor(d);
            Assert.True(
                typeof(IJamlClause).IsAssignableFrom(clauseType),
                $"'{d}' → {clauseType.Name} is not IJamlClause.");
        }
    }

    /// <summary>
    /// T2 lock: schema ClauseKeysFor dispatches to the FilterDesc, not a clause-type mirror.
    /// </summary>
    [Theory]
    [InlineData("joker", typeof(JokerFilterDesc))]
    [InlineData("commonJoker", typeof(CommonJokerFilterDesc))]
    [InlineData("tag", typeof(TagFilterDesc))]
    [InlineData("luckyMoney", typeof(LuckyMoneyFilterDesc))]
    [InlineData("voucher", typeof(VoucherFilterDesc))]
    public void SchemaClauseKeys_MatchDescNotClauseMirror(string discriminator, Type descType)
    {
        var descKeys = (string[])descType
            .GetProperty("ClauseKeys", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)!
            .GetValue(null)!;
        Assert.Equal(descKeys, JamlSchema.ClauseKeysFor(discriminator));

        // Wire clause types no longer carry a public static ClauseKeys field (the old product).
        var clauseType = JamlSchema.ClauseTypeFor(discriminator);
        Assert.Null(
            clauseType.GetField(
                "ClauseKeys",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static));
    }

    [Fact]
    public void LogicWires_StillReadLogicClauseKeys()
    {
        Assert.Equal(Motely.Filters.LogicClause.ClauseKeys, JamlSchema.ClauseKeysFor("and"));
        Assert.Equal(Motely.Filters.LogicClause.ClauseKeys, JamlSchema.ClauseKeysFor("or"));
    }

    /// <summary>
    /// T5 lock: schema SourceKeysFor / SourceConfigTypeFor point at source shapes colocated
    /// with the desc family (not a phone book on the JamlConfig bag).
    /// </summary>
    [Theory]
    [InlineData("joker", typeof(JokerSourceConfig))]
    [InlineData("commonJoker", typeof(JokerSourceConfig))]
    [InlineData("legendaryJoker", typeof(LegendaryJokerSourceConfig))]
    [InlineData("tarotCard", typeof(TarotCardSourceConfig))]
    [InlineData("spectralCard", typeof(SpectralCardSourceConfig))]
    [InlineData("planetCard", typeof(PlanetSourceConfig))]
    [InlineData("standardCard", typeof(StandardCardSourceConfig))]
    public void SchemaSourceKeys_MatchColocatedSourceConfig(string discriminator, Type sourceConfigType)
    {
        Assert.Equal(sourceConfigType, JamlSchema.SourceConfigTypeFor(discriminator));

        var keys = (string[])sourceConfigType
            .GetField("SourceKeys", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)!
            .GetValue(null)!;
        Assert.Equal(keys, JamlSchema.SourceKeysFor(discriminator));
    }

    // Property type on the shape IS the vocabulary — generated KeyValueEnumTypeFor, no hand table.

    [Theory]
    [InlineData("deck", typeof(MotelyDeck))]
    [InlineData("stake", typeof(MotelyStake))]
    [InlineData("edition", typeof(MotelyItemEdition))]
    [InlineData("seal", typeof(MotelyItemSeal))]
    [InlineData("enhancement", typeof(MotelyItemEnhancement))]
    [InlineData("rank", typeof(MotelyStandardcardRank))]
    [InlineData("suit", typeof(MotelyStandardcardSuit))]
    [InlineData("stickers", typeof(MotelyJokerSticker))]
    [InlineData("luck", typeof(MotelyLuck))]
    public void SchemaKeyValueEnum_ComesFromPropertyType(string key, Type expectedEnum)
    {
        Assert.Equal(expectedEnum, JamlSchema.KeyValueEnumTypeFor(key));
        Assert.Equal(expectedEnum, JamlSchema.EnumTypeForKind(key));
    }

    [Theory]
    [InlineData("tarot", typeof(MotelyTarotCard))]
    [InlineData("spectral", typeof(MotelySpectralCard))]
    [InlineData("planet", typeof(MotelyPlanetCard))]
    public void SchemaShortWire_IsRealDiscriminatorAlias(string wire, Type expectedEnum)
    {
        Assert.True(JamlSchema.IsKnownDiscriminator(wire));
        Assert.Equal(expectedEnum, JamlSchema.ValueEnumTypeFor(wire));
        Assert.Equal(expectedEnum, JamlSchema.EnumTypeForKind(wire));
    }

    [Fact]
    public void SchemaListItems_EditionNamesMatchEngineEnum()
    {
        Assert.Equal(Enum.GetNames<MotelyItemEdition>(), JamlSchema.ListItems("edition"));
        Assert.Contains("LuckyCat", JamlSchema.ListItems("joker", "luckyc"));
    }
}
