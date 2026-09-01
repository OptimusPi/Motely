using Motely.Filters.Jaml;
using Xunit;

namespace Motely.Tests;

/// <summary>
/// JAML has two block styles and honors both: '|' keeps the line breaks an author typed,
/// '>' folds them into spaces. YAML's chomping indicators ('|-', '|+', '>-', '>+') tune how many
/// trailing newlines survive, which JAML has no way to express — so it refuses them by name
/// rather than reading '|+' and quietly doing what '|' does.
/// </summary>
public sealed class JamlBlockScalarTests
{
    private static string Doc(string indicator) =>
        $"name: probe\ndescription: {indicator}\n  line one\n  line two\nstake: White\n";

    [Fact]
    public void Literal_KeepsLineBreaks()
    {
        Assert.True(JamlConfigLoader.TryLoad(Doc("|"), out var config, out var error), error);
        Assert.Equal("line one\nline two", config!.Description);
    }

    [Fact]
    public void Folded_JoinsLinesWithSpaces()
    {
        Assert.True(JamlConfigLoader.TryLoad(Doc(">"), out var config, out var error), error);
        Assert.Equal("line one line two", config!.Description);
    }

    [Theory]
    [InlineData("|-")]
    [InlineData("|+")]
    [InlineData(">-")]
    [InlineData(">+")]
    public void ChompingIndicators_AreRefusedByName(string indicator)
    {
        Assert.False(JamlConfigLoader.TryLoad(Doc(indicator), out _, out var error));
        Assert.Contains(indicator, error);
        Assert.Contains("block style", error);
    }
}
