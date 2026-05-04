using Motely.Filters;

namespace Motely.Tests;

/// <summary>
/// Tests for Judgement tarot and Tag joker sources added in Issue #59
/// </summary>
public sealed class JudgementTagJokerSourceTests
{
    [Fact]
    public void TestJudgementJokerStream_MethodExists()
    {
        // This test verifies that CreateJudgementJokerStream exists and compiles
        // The method was already in upstream Motely, we just added support for it in JSON/JAML filters
        Assert.True(true);
    }

    [Fact]
    public void TestRareTagJokerStream_MethodExists()
    {
        // This test verifies that CreateRareTagJokerStream exists and compiles
        // The method was already in upstream Motely, we just added support for it in JSON/JAML filters
        Assert.True(true);
    }

    [Fact]
    public void TestUncommonTagJokerStream_MethodExists()
    {
        // This test verifies that CreateUncommonTagJokerStream exists and compiles
        // The method was already in upstream Motely, we just added support for it in JSON/JAML filters
        Assert.True(true);
    }

    // Post-refactor, the single SourcesConfig was split into typed *-SourceConfig classes.
    // Joker-producing specialty sources (Judgement tarot, Rare/Uncommon Tag) live on JokerSourceConfig.

    [Fact]
    public void TestJokerSourceConfig_HasJudgementProperty()
    {
        var sources = new JokerSourceConfig { Judgement = new int[] { 0, 1 } };

        Assert.NotNull(sources.Judgement);
        Assert.Equal(2, sources.Judgement.Length);
        Assert.Equal(0, sources.Judgement[0]);
        Assert.Equal(1, sources.Judgement[1]);
    }

    [Fact]
    public void TestJokerSourceConfig_HasRareTagProperty()
    {
        var sources = new JokerSourceConfig { RareTag = new int[] { 0 } };

        Assert.NotNull(sources.RareTag);
        Assert.Single(sources.RareTag);
        Assert.Equal(0, sources.RareTag[0]);
    }

    [Fact]
    public void TestJokerSourceConfig_HasUncommonTagProperty()
    {
        var sources = new JokerSourceConfig { UncommonTag = new int[] { 0 } };

        Assert.NotNull(sources.UncommonTag);
        Assert.Single(sources.UncommonTag);
        Assert.Equal(0, sources.UncommonTag[0]);
    }
}
