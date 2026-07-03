using Motely.Filters.Jummy;

namespace Motely.Tests;

/// <summary>
/// Validate/Canonicalize are the line-level surface every head (WASM, CLI, LSP) shares:
/// Validate is TryToClause's verdict as null-or-error, Canonicalize is the lossless
/// parse→format round-trip pinned by <see cref="JummyLineTests"/>.
/// </summary>
public class JummyLineCanonicalizeTests
{
    [Fact]
    public void Validate_returnsNull_forThePinnedExample()
    {
        Assert.Null(JummyLine.Validate("Eternal Blueprint in antes 1 or 2"));
    }

    [Fact]
    public void Validate_returnsTheParsersError_forGarbage()
    {
        var error = JummyLine.Validate("Definitely Not A JUMMY Line");
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Theory]
    [InlineData("Eternal Blueprint in antes 1 or 2", "Eternal Blueprint in antes 1 or 2")]
    [InlineData("Blueprint", "Blueprint")]
    [InlineData("Showman in antes 1, 2", "Showman in antes 1 or 2")]
    public void Canonicalize_reformatsThroughTheClause(string line, string canonical)
    {
        Assert.Equal(canonical, JummyLine.Canonicalize(line));
    }

    [Fact]
    public void Canonicalize_throwsFormatException_forGarbage()
    {
        Assert.Throws<FormatException>(() => JummyLine.Canonicalize("Definitely Not A JUMMY Line"));
    }
}
