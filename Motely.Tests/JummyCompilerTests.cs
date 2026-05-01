using Motely.Filters;
using Xunit;

namespace Motely.Tests;

public class JummyCompilerTests
{
    [Fact]
    public void BlueprintExample_CompilesToJaml_AndLoads()
    {
        var jummy = """
            jummy: 1
            name: Blueprint in booster packs
            description: Blueprint in ante 1 or 2 in the first and second booster packs.
            deck: Red
            stake: White
            must:
              - what:
                  rareJoker: Blueprint
                where:
                  ante: "1 or 2"
                  booster packs: "first and second"
            """;

        Assert.True(JummyCompiler.TryCompile(jummy, out var jaml, out var compileError), compileError);
        Assert.NotNull(jaml);
        Assert.DoesNotContain("what:", jaml, StringComparison.Ordinal);
        Assert.DoesNotContain("where:", jaml, StringComparison.Ordinal);
        Assert.Contains("rareJoker: Blueprint", jaml, StringComparison.Ordinal);
        Assert.Contains("antes:", jaml, StringComparison.Ordinal);
        Assert.Contains("boosterPacks:", jaml, StringComparison.Ordinal);

        Assert.True(JamlConfigLoader.TryLoad(jaml!, out var config, out var loadError), loadError);
        Assert.NotNull(config);
        Assert.Single(config!.Must.RareJokers);
        Assert.Equal(MotelyJokerRare.Blueprint, config.Must.RareJokers[0].Jokers[0]);
        Assert.Equal([1, 2], config.Must.RareJokers[0].Antes);
        Assert.Equal([0, 1], config.Must.RareJokers[0].Sources.BoosterPacks);
    }

    [Fact]
    public void TryLoadJummy_RoundTrips()
    {
        var jummy = """
            name: T
            must:
              - what:
                  joker: Showman
                where:
                  ante: [1]
                  booster packs: [0, 1]
            """;

        Assert.True(JamlConfigLoader.TryLoadJummy(jummy, out var config, out var error), error);
        Assert.NotNull(config);
        Assert.Single(config!.Must.Jokers);
        Assert.Equal([1], config.Must.Jokers[0].Antes);
        Assert.Equal([0, 1], config.Must.Jokers[0].Sources.BoosterPacks);
    }

    [Fact]
    public void RepoBlueprintJummyFile_Loads_WhenPresent()
    {
        var jummyFile = Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "JummyFilters",
                "blueprint-booster-packs.jummy"
            )
        );
        if (!File.Exists(jummyFile))
            return;

        Assert.True(JamlConfigLoader.TryLoadJummyFromFile(jummyFile, out var config, out var error), error);
        Assert.NotNull(config);
        Assert.Single(config!.Must.RareJokers);
        Assert.Equal([1, 2], config.Must.RareJokers[0].Antes);
        Assert.Equal([0, 1], config.Must.RareJokers[0].Sources.BoosterPacks);
    }

    [Fact]
    public void MumbleLine_InAnte1_UsesFourBoosterSlots()
    {
        var jummy = """
            name: Mumble
            must:
              - Eternal Blueprint in Ante 1
            """;

        Assert.True(JummyCompiler.TryCompile(jummy, out var jaml, out var compileErr), compileErr);
        Assert.DoesNotContain("\njoker:", jaml!, StringComparison.Ordinal);
        Assert.Contains("rareJoker:", jaml!, StringComparison.Ordinal);

        Assert.True(JamlConfigLoader.TryLoadJummy(jummy, out var config, out var err), err);
        Assert.NotNull(config);
        Assert.IsType<RareJokerClause>(config!.Must.OrderedClauses[0]);
        Assert.Single(config.Must.RareJokers);
        var c = config.Must.RareJokers[0];
        Assert.Equal(MotelyJokerRare.Blueprint, c.Jokers[0]);
        Assert.Equal([1], c.Antes);
        Assert.Equal([0, 1, 2, 3], c.Sources.BoosterPacks);
        Assert.Contains(MotelyJokerSticker.Eternal, c.Stickers);
    }

    [Fact]
    public void MumbleLine_ByAnte4_CumulativeAntesAndSixPackUnion()
    {
        var jummy = """
            name: Mumble
            must:
              - Perishable Egg by Ante 4
            """;

        Assert.True(JamlConfigLoader.TryLoadJummy(jummy, out var config, out var err), err);
        Assert.NotNull(config);
        Assert.Single(config!.Must.CommonJokers);
        var c = config.Must.CommonJokers[0];
        Assert.Equal(MotelyJokerCommon.Egg, c.Jokers[0]);
        Assert.Equal([1, 2, 3, 4], c.Antes);
        Assert.Equal([0, 1, 2, 3, 4, 5], c.Sources.BoosterPacks);
        Assert.Contains(MotelyJokerSticker.Perishable, c.Stickers);
    }

    [Fact]
    public void MumbleLine_LineCommentAfterPhrase_IsIgnored()
    {
        var jummy = """
            name: Mumble
            must:
              - Eternal Blueprint in Ante 2 // note
            """;

        Assert.True(JamlConfigLoader.TryLoadJummy(jummy, out var config, out var err), err);
        Assert.Single(config!.Must.RareJokers);
        Assert.Equal([2], config.Must.RareJokers[0].Antes);
        Assert.Equal([0, 1, 2, 3, 4, 5], config.Must.RareJokers[0].Sources.BoosterPacks);
    }
}
