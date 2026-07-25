using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Motely.Analysis;
using Motely.Enums;
using Motely.Filters.Jaml;

namespace Motely.JsonRender;

/// <summary>
/// Emits the same Jamlyzer results as the rich JSON/HTML reports, but in jaml-ui's
/// native dialect — the camelCase, numeric-enum shape of motely-wasm's generated
/// TypeScript types, so <c>JamlyzerView</c> can consume the file without any mapping.
/// <para>
/// The engine records are serialized as-is: <see cref="MotelyItem"/>'s public
/// properties are exactly the packed-int facets the wasm contract expects
/// (value, type, typeCategory, seal, enhancement, edition, standardcardSuit,
/// standardcardRank, isPerishable, isEternal, isRental), and with no string-enum
/// converter every enum lands as its numeric value. Nulls drop out, so
/// <c>erraticDeck</c> only appears on Erratic-deck runs, matching the optional
/// field in the contract.
/// </para>
/// </summary>
public static class JamlUiJsonRenderer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers =
            {
                static typeInfo =>
                {
                    // MotelyItem.IsInvalid is a derived convenience getter that the
                    // motely-wasm contract doesn't declare — keep the JSON field set
                    // an exact match for JamlyzerView's types.
                    if (typeInfo.Type != typeof(MotelyItem))
                        return;
                    var extra = typeInfo.Properties.FirstOrDefault(p => p.Name == "isInvalid");
                    if (extra is not null)
                        typeInfo.Properties.Remove(extra);
                },
            },
        },
    };

    /// <summary>jaml-ui report header: which filter produced these seeds, at what deck/stake.</summary>
    private sealed record JamlUiFilter(string Id, string? Name);

    private sealed record JamlUiReport(
        JamlUiFilter Filter,
        MotelyDeck Deck,
        MotelyStake Stake,
        int EventRolls,
        IReadOnlyList<MotelyJamlyzerSeedResult> Seeds
    );

    public static void Write(
        JamlConfig config,
        IReadOnlyList<MotelyJamlyzerSeedResult> results,
        int eventRolls,
        string path
    )
    {
        var report = new JamlUiReport(
            new JamlUiFilter(config.Id, config.Name),
            config.Deck,
            config.Stake,
            eventRolls,
            results
        );
        JsonRenderDocument.EnsureParentDir(path);
        File.WriteAllText(path, JsonSerializer.Serialize(report, Options) + "\n");
    }
}
