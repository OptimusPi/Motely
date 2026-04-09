using Motely.Filters;
using Xunit;

namespace Motely.Tests;

public class JamlSemanticFingerprintTests
{
    [Fact]
    public void FilterId_must_clause_order_is_invariant()
    {
        var a = """
            id: stable
            must:
              - joker: Showman
                antes: [1]
                sources:
                  shopItems: [0]
              - joker: Joker
                antes: [1]
                sources:
                  shopItems: [0]
            """;
        var b = """
            id: stable
            must:
              - joker: Joker
                antes: [1]
                sources:
                  shopItems: [0]
              - joker: Showman
                antes: [1]
                sources:
                  shopItems: [0]
            """;

        Assert.True(JamlConfigLoader.TryLoad(a, out var ca, out var ea), ea);
        Assert.True(JamlConfigLoader.TryLoad(b, out var cb, out var eb), eb);
        Assert.Equal(ca!.FilterId, cb!.FilterId);
    }

    [Fact]
    public void FilterId_ignores_label_and_description()
    {
        var a = """
            id: meta
            description: One
            must:
              - joker: Showman
                label: L1
                antes: [1]
                sources:
                  shopItems: [0]
            """;
        var b = """
            id: meta
            description: Two
            must:
              - joker: Showman
                label: L2
                antes: [1]
                sources:
                  shopItems: [0]
            """;

        Assert.True(JamlConfigLoader.TryLoad(a, out var ca, out var ea), ea);
        Assert.True(JamlConfigLoader.TryLoad(b, out var cb, out var eb), eb);
        Assert.Equal(ca!.FilterId, cb!.FilterId);
    }

    [Fact]
    public void FilterId_includes_clause_min_for_should()
    {
        var a = """
            id: same
            should:
              - joker: Showman
                min: 1
                antes: [1]
                sources:
                  shopItems: [0]
            """;
        var b = """
            id: same
            should:
              - joker: Showman
                min: 2
                antes: [1]
                sources:
                  shopItems: [0]
            """;

        Assert.True(JamlConfigLoader.TryLoad(a, out var ca, out var ea), ea);
        Assert.True(JamlConfigLoader.TryLoad(b, out var cb, out var eb), eb);
        Assert.Equal(1, ca!.Should.Jokers[0].Min);
        Assert.Equal(2, cb!.Should.Jokers[0].Min);
        Assert.NotEqual(ca.FilterId, cb.FilterId);
    }

    [Fact]
    public void FilterId_suffix_is_full_sha256_hex()
    {
        var jaml = """
            id: hexlen
            must:
              - joker: Joker
                antes: [1]
                sources:
                  shopItems: [0]
            """;

        Assert.True(JamlConfigLoader.TryLoad(jaml, out var c, out var e), e);
        var id = c!.FilterId;
        var idx = id.LastIndexOf('_');
        Assert.True(idx > 0);
        var suffix = id[(idx + 1)..];
        Assert.Equal(64, suffix.Length);
        Assert.Matches("^[0-9a-f]{64}$", suffix);
    }
}
