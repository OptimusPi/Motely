using Motely.Lsp.Core;

namespace Motely.Tests;

public class JamlLanguageServiceTests
{
    private const string CleanDocument = """
        name: lsp-clean
        deck: Red
        stake: White
        must:
          - joker: Blueprint
            antes: [1, 2]
        """;

    // ── Diagnose ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Diagnose_CleanDocument_ReportsNothing()
    {
        Assert.Empty(JamlLanguageService.Diagnose(CleanDocument));
    }

    [Fact]
    public void Diagnose_EmptyText_ReportsNothing()
    {
        Assert.Empty(JamlLanguageService.Diagnose(""));
    }

    [Fact]
    public void Diagnose_UnknownRootKey_UnderlinesTheTypo()
    {
        var text = "name: oops\nboses:\n  - joker: Blueprint\n";
        var diagnostic = Assert.Single(JamlLanguageService.Diagnose(text));
        Assert.Contains("boses", diagnostic.Message);
        Assert.Equal(1, diagnostic.Span.StartLine);
        Assert.Equal(0, diagnostic.Span.StartColumn);
        Assert.Equal("boses".Length, diagnostic.Span.EndColumn);
        Assert.Equal(JamlDiagnosticSeverity.Error, diagnostic.Severity);
    }

    [Fact]
    public void Diagnose_UnknownClauseKey_NamesTheKey()
    {
        var text = "must:\n  - joker: Blueprint\n    boosterPakcz: [0]\n";
        var diagnostic = Assert.Single(JamlLanguageService.Diagnose(text));
        Assert.Contains("boosterPakcz", diagnostic.Message);
        Assert.Equal(2, diagnostic.Span.StartLine);
    }

    [Fact]
    public void Diagnose_SyntaxError_CarriesTheParsersSpan()
    {
        var text = "name: ok\nmust\n";
        var diagnostic = Assert.Single(JamlLanguageService.Diagnose(text));
        Assert.Equal("JAML0001", diagnostic.Code);
        Assert.Equal(1, diagnostic.Span.StartLine);
    }

    [Fact]
    public void Diagnose_UnknownKey_ThatAlsoAppearsAsAValue_UnderlinesTheKeyNotTheFirstMatch()
    {
        // "boses" is both the name value (line 0) and the unknown key (line 1). Locating the
        // squiggle by string-searching the document finds line 0; the engine's own key span
        // finds the key itself on line 1. This is exactly the drift the span-carrying loader
        // removes — the diagnostic points at what the loader rejected, not at a lookalike word.
        var text = "name: boses\nboses:\n  - joker: Blueprint\n";
        var diagnostic = Assert.Single(JamlLanguageService.Diagnose(text));
        Assert.Contains("boses", diagnostic.Message);
        Assert.Equal(1, diagnostic.Span.StartLine);
        Assert.Equal(0, diagnostic.Span.StartColumn);
        Assert.Equal("boses".Length, diagnostic.Span.EndColumn);
    }

    [Fact]
    public void Diagnose_BadBossValue_BuiltThroughItsDescriptor_IsPositioned()
    {
        // Boss builds through its descriptor now, reading its value via the span-carrying reader.
        // A value outside MotelyBossBlind is reported on the clause's line — the desc rail carries
        // a real diagnostic, not a swallowed or location-less one.
        var text = "must:\n  - boss: NotARealBoss\n";
        var diagnostic = Assert.Single(JamlLanguageService.Diagnose(text));
        Assert.Contains("NotARealBoss", diagnostic.Message);
        Assert.Equal(1, diagnostic.Span.StartLine);
    }

    // ── Hover ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Hover_OnDiscriminator_DescribesTheClause()
    {
        // Cursor on "joker" in "  - joker: Blueprint".
        var hover = JamlLanguageService.Hover(CleanDocument, 4, 5);
        Assert.NotNull(hover);
        Assert.Contains("**joker**", hover.Markdown);
        Assert.Contains("MotelyJoker", hover.Markdown);
    }

    [Fact]
    public void Hover_OnJokerName_NamesItsVocabulary()
    {
        // Cursor on "Blueprint".
        var hover = JamlLanguageService.Hover(CleanDocument, 4, 12);
        Assert.NotNull(hover);
        Assert.Contains("Blueprint", hover.Markdown);
        Assert.Contains("joker", hover.Markdown);
    }

    [Fact]
    public void Hover_OnWhitespace_SaysNothing()
    {
        Assert.Null(JamlLanguageService.Hover(CleanDocument, 3, 0));
    }

    // ── Complete ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Complete_AtRoot_OffersRootKeys()
    {
        var items = JamlLanguageService.Complete("de", 0, 2);
        Assert.Contains(items, i => i.Label == "deck");
        Assert.Contains(items, i => i.Label == "description");
        Assert.DoesNotContain(items, i => i.Label == "must" && i.Kind != "key");
    }

    [Fact]
    public void Complete_DeckValue_OffersDecks()
    {
        var items = JamlLanguageService.Complete("deck: Err", 0, 9);
        Assert.Contains(items, i => i.Label == "Erratic");
        Assert.All(items, i => Assert.Equal("value", i.Kind));
    }

    [Fact]
    public void Complete_NewListItem_OffersDiscriminators()
    {
        var text = "must:\n  - jok";
        var items = JamlLanguageService.Complete(text, 1, 7);
        Assert.Contains(items, i => i.Label is "joker" or "jokers");
        Assert.All(items, i => Assert.Equal("discriminator", i.Kind));
    }

    [Fact]
    public void Complete_JokerValue_OffersJokerNamesAndAny()
    {
        var text = "must:\n  - joker: Lu";
        var items = JamlLanguageService.Complete(text, 1, 13);
        Assert.Contains(items, i => i.Label == "LuckyCat");
        Assert.Contains(JamlLanguageService.Complete("must:\n  - joker: An", 1, 13),
            i => i.Label == "Any");
    }

    [Fact]
    public void Complete_ClauseKey_OffersTheClausesOwnKeys()
    {
        var text = "must:\n  - joker: Blueprint\n    edi";
        var items = JamlLanguageService.Complete(text, 2, 7);
        Assert.Contains(items, i => i.Label == "edition");
    }

    [Fact]
    public void Complete_InsideSourcesBlock_OffersSourceKeys()
    {
        var text = "must:\n  - joker: Blueprint\n    sources:\n      shop";
        var items = JamlLanguageService.Complete(text, 3, 10);
        Assert.Contains(items, i => i.Label.StartsWith("shop", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Complete_EditionValue_OffersEditions()
    {
        var text = "must:\n  - joker: Blueprint\n    edition: Neg";
        var items = JamlLanguageService.Complete(text, 2, 16);
        Assert.Contains(items, i => i.Label == "Negative");
    }
}
