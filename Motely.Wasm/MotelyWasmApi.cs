using System.Reflection;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Text.Json;
using Motely.Enums;
using Motely.Filters;
using Motely.Filters.Jaml;
using Motely.Lsp.Core;

namespace Motely.Wasm;

/// <summary>
/// The browser surface of the Motely engine: parse/validate JAML, expose the engine's own
/// vocabulary, run the language service (diagnostics/hover/completion), and score known seed
/// lists — all through in-box <c>[JSExport]</c> interop. Strings in, JSON strings out; the
/// shapes live in <see cref="WasmDtos"/> and serialize through the source-generated context.
/// </summary>
[SupportedOSPlatform("browser")]
public static partial class MotelyWasmApi
{
    [JSExport]
    public static string Version() =>
        typeof(JamlConfigLoader).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "0.0.0";

    /// <summary>Validate a JAML document with the one true loader. Never throws.</summary>
    [JSExport]
    public static string ParseJaml(string text)
    {
        ParseResultDto dto = JamlConfigLoader.TryLoad(text, out var config, out var error)
            ? new(
                Ok: true,
                Error: null,
                Name: config.Name,
                Deck: config.Deck.ToString(),
                Stake: config.Stake.ToString(),
                Must: config.Must.Count,
                Should: config.Should.Count,
                MustNot: config.MustNot.Count
            )
            : new(false, error, null, null, null, 0, 0, 0);
        return JsonSerializer.Serialize(dto, WasmJson.Default.ParseResultDto);
    }

    /// <summary>The engine's enums, verbatim — the one true vocabulary, never a JS copy.</summary>
    [JSExport]
    public static string Vocabulary() =>
        JsonSerializer.Serialize(
            new VocabularyDto(
                Decks: Enum.GetNames<MotelyDeck>(),
                Stakes: Enum.GetNames<MotelyStake>(),
                Jokers: Enum.GetNames<MotelyJoker>(),
                LegendaryJokers: Enum.GetNames<MotelyJokerLegendary>(),
                Vouchers: Enum.GetNames<MotelyVoucher>(),
                TarotCards: Enum.GetNames<MotelyTarotCard>(),
                SpectralCards: Enum.GetNames<MotelySpectralCard>(),
                PlanetCards: Enum.GetNames<MotelyPlanetCard>(),
                Bosses: Enum.GetNames<MotelyBossBlind>(),
                Tags: Enum.GetNames<MotelyTag>(),
                Editions: Enum.GetNames<MotelyItemEdition>(),
                Enhancements: Enum.GetNames<MotelyItemEnhancement>(),
                Seals: Enum.GetNames<MotelyItemSeal>()
            ),
            WasmJson.Default.VocabularyDto
        );

    [JSExport]
    public static string Diagnostics(string text) =>
        JsonSerializer.Serialize(
            JamlLanguageService
                .Diagnose(text)
                .Select(d => new DiagnosticDto(
                    ToSpanDto(d.Span),
                    d.Message,
                    d.Severity.ToString(),
                    d.Code
                ))
                .ToArray(),
            WasmJson.Default.DiagnosticDtoArray
        );

    /// <summary>Hover markdown for the word at (line, character), or JSON null.</summary>
    [JSExport]
    public static string Hover(string text, int line, int character)
    {
        var hover = JamlLanguageService.Hover(text, line, character);
        return hover is null
            ? "null"
            : JsonSerializer.Serialize(
                new HoverDto(ToSpanDto(hover.Span), hover.Markdown),
                WasmJson.Default.HoverDto
            );
    }

    [JSExport]
    public static string Complete(string text, int line, int character) =>
        JsonSerializer.Serialize(
            JamlLanguageService
                .Complete(text, line, character)
                .Select(c => new CompletionDto(
                    c.Label,
                    c.Kind,
                    c.Detail,
                    c.ReplaceSpan.IsEmpty ? null : ToSpanDto(c.ReplaceSpan)
                ))
                .ToArray(),
            WasmJson.Default.CompletionDtoArray
        );

    /// <summary>
    /// List-mode search: gate the given seeds through the filter's must clauses and rank the
    /// survivors by should score, best first. Runs on the engine's single-threaded browser
    /// pump, so the page stays responsive while it grinds.
    /// </summary>
    [JSExport]
    public static async Task<string> ScoreSeeds(string jaml, string[] seeds)
    {
        if (!JamlConfigLoader.TryLoad(jaml, out var config, out var error))
            return JsonSerializer.Serialize(
                new ScoreRunDto(false, error, 0, 0, 0, 0, []),
                WasmJson.Default.ScoreRunDto
            );

        List<ScoredSeedDto> scored = [];
        var settings = JamlSearchBuilder
            .CreateSettings(config)
            .WithSeedList([.. seeds.Where(s => !string.IsNullOrWhiteSpace(s))])
            .WithThreadCount(1)
            .WithQuietMode(true)
            .WithScoredResultCallback(tally =>
                scored.Add(new ScoredSeedDto(tally.Seed, tally.Score, [.. tally.Tally.Select(b => (int)b)]))
            );

        using var search = settings.CreateSearch();
        await search.Start().WaitForCompletionAsync();

        return JsonSerializer.Serialize(
            new ScoreRunDto(
                Ok: true,
                Error: null,
                TotalSeeds: search.TotalSeedsSearched,
                MatchingSeeds: search.MatchingSeeds,
                FilteredSeeds: search.FilteredSeeds,
                ElapsedMs: search.ElapsedMs,
                Results: [.. scored.OrderByDescending(s => s.Score)]
            ),
            WasmJson.Default.ScoreRunDto
        );
    }

    private static SpanDto ToSpanDto(JamlSpan span) =>
        new(span.StartLine, span.StartColumn, span.EndLine, span.EndColumn);
}
