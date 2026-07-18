namespace Motely.Tests;

// MotelyGlossary is the single source of truth rendered by the CLI --glossary flag, the MCP
// glossary tool, and the WASM/npm export — these tests pin its content/shape so that contract
// can't silently drift or go empty.
public class MotelyGlossaryTests
{
    [Theory]
    [InlineData("JAML")]
    [InlineData("JAMLyzer")]
    public void TryGet_KnownTerm_ReturnsNonEmptyDefinition(string term)
    {
        var entry = MotelyGlossary.TryGet(term);

        Assert.NotNull(entry);
        Assert.Equal(term, entry!.Term);
        Assert.False(string.IsNullOrWhiteSpace(entry.Definition));
    }

    [Fact]
    public void TryGet_IsCaseInsensitive()
    {
        Assert.NotNull(MotelyGlossary.TryGet("jaml"));
        Assert.NotNull(MotelyGlossary.TryGet("JAMLYZER"));
    }

    [Fact]
    public void TryGet_UnknownTerm_ReturnsNull()
    {
        Assert.Null(MotelyGlossary.TryGet("NotARealTerm"));
    }

    [Fact]
    public void Entries_HasNoDuplicateTerms()
    {
        var terms = MotelyGlossary.Entries.Select(e => e.Term.ToLowerInvariant());
        Assert.Equal(terms.Distinct().Count(), MotelyGlossary.Entries.Count);
    }

    [Fact]
    public void JamlEntry_ExpandsTheAcronym()
    {
        var jaml = MotelyGlossary.TryGet("JAML");

        Assert.Equal("Jimbo's Ante Markup Language", jaml!.Acronym);
    }

    [Fact]
    public void Render_ContainsEveryTermAndItsDefinition()
    {
        var rendered = MotelyGlossary.Render();

        foreach (var entry in MotelyGlossary.Entries)
        {
            Assert.Contains(entry.Term, rendered);
            Assert.Contains(entry.Definition, rendered);
        }
    }

    [Fact]
    public void Render_IsStableAcrossCalls()
    {
        Assert.Equal(MotelyGlossary.Render(), MotelyGlossary.Render());
    }

    // JAMLyzer's definition is the one place a reader could confuse it with the CLI's older
    // --analyze flag (a separate legacy text-block analyzer) — pin that the disambiguation
    // actually ships in the definition text, not just in a comment nobody sees.
    [Fact]
    public void JamlyzerEntry_DisambiguatesFromLegacyAnalyzeFlag()
    {
        var jamlyzer = MotelyGlossary.TryGet("JAMLyzer");

        Assert.Contains("--analyze", jamlyzer!.Definition);
    }
}
