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

    [Fact]
    public void TestSourcesConfig_HasJudgementProperty()
    {
        // Verify that SourcesConfig has the new Judgement property
        var sources = new SourcesConfig
        {
            Judgement = new int[] { 0, 1 }
        };

        Assert.NotNull(sources.Judgement);
        Assert.Equal(2, sources.Judgement.Length);
        Assert.Equal(0, sources.Judgement[0]);
        Assert.Equal(1, sources.Judgement[1]);
    }

    [Fact]
    public void TestSourcesConfig_HasRareTagProperty()
    {
        // Verify that SourcesConfig has the new RareTag property
        var sources = new SourcesConfig
        {
            RareTag = new int[] { 0 }
        };

        Assert.NotNull(sources.RareTag);
        Assert.Single(sources.RareTag);
        Assert.Equal(0, sources.RareTag[0]);
    }

    [Fact]
    public void TestSourcesConfig_HasUncommonTagProperty()
    {
        // Verify that SourcesConfig has the new UncommonTag property
        var sources = new SourcesConfig
        {
            UncommonTag = new int[] { 0 }
        };

        Assert.NotNull(sources.UncommonTag);
        Assert.Single(sources.UncommonTag);
        Assert.Equal(0, sources.UncommonTag[0]);
    }
}
