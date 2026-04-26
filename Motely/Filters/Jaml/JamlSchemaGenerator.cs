using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization;

namespace Motely.Filters;

/// <summary>
/// Generates <c>jaml.schema.json</c> from the Motely JAML DTO graph
/// (<see cref="JamlRootDocument"/>, <see cref="JamlClauseDto"/>, <see cref="JamlSourcesDto"/>,
/// <see cref="JamlDefaultsDto"/>) using <see cref="JsonSchemaExporter"/>.
///
/// AOT-safe: uses a source-generated <see cref="JsonSerializerContext"/> and a
/// <c>TransformSchemaNode</c> callback keyed off <c>JsonPropertyInfo.Name</c>, so no
/// runtime reflection over attributes is required.
/// </summary>
public static partial class JamlSchemaGenerator
{
    private const string SchemaId = "https://mcp.seedfinder.app/jaml.schema.json";
    private const string SchemaTitle = "JAML \u2014 Jimbo's Ante Markup Language";
    private const string SchemaDescription =
        "Schema for Balatro seed filter files (.jaml). Generated from Motely C# DTOs via System.Text.Json.Schema.JsonSchemaExporter.";

    // Property name (camelCase, as emitted by JsonSchemaExporter under CamelCase policy)
    // -> named enum $def. Applied to scalar string properties and to the `items` of
    // string-array properties.
    private static readonly Dictionary<string, string> PropertyToRef = new(StringComparer.Ordinal)
    {
        ["joker"] = "Joker",
        ["jokers"] = "Joker",
        ["commonJoker"] = "CommonJoker",
        ["commonJokers"] = "CommonJoker",
        ["uncommonJoker"] = "UncommonJoker",
        ["uncommonJokers"] = "UncommonJoker",
        ["rareJoker"] = "RareJoker",
        ["rareJokers"] = "RareJoker",
        ["mixedJoker"] = "Joker",
        ["mixedJokers"] = "Joker",
        ["soulJoker"] = "Joker",
        ["legendaryJoker"] = "Joker",
        ["voucher"] = "Voucher",
        ["vouchers"] = "Voucher",
        ["tarot"] = "Tarot",
        ["tarotCard"] = "Tarot",
        ["spectral"] = "Spectral",
        ["spectralCard"] = "Spectral",
        ["planet"] = "Planet",
        ["planetCard"] = "Planet",
        ["boss"] = "Boss",
        ["tag"] = "Tag",
        ["smallBlindTag"] = "Tag",
        ["bigBlindTag"] = "Tag",
        ["deck"] = "Deck",
        ["stake"] = "Stake",
        ["edition"] = "Edition",
        ["stickers"] = "Sticker",
        ["seal"] = "Seal",
        ["enhancement"] = "Enhancement",
        ["rank"] = "Rank",
        ["erraticRank"] = "Rank",
        ["suit"] = "Suit",
        ["erraticSuit"] = "Suit",
        ["mode"] = "Mode",
        ["aesthetics"] = "Aesthetic",
        ["event"] = "Event",
    };

    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(JamlRootDocument))]
    [JsonSerializable(typeof(JamlClauseDto))]
    [JsonSerializable(typeof(JamlSourcesDto))]
    [JsonSerializable(typeof(JamlDefaultsDto))]
    internal sealed partial class SchemaContext : JsonSerializerContext
    {
    }

    /// <summary>Builds the JAML JSON Schema as an in-memory <see cref="JsonObject"/>.</summary>
    public static JsonObject Generate()
    {
        var options = new JsonSchemaExporterOptions
        {
            TreatNullObliviousAsNonNullable = true,
            TransformSchemaNode = static (ctx, schema) =>
            {
                // First, collapse any ["X", "null"] type arrays the exporter emits for nullable
                // reference / value types down to plain "X" for JAML (null is always permitted by YAML).
                SimplifyNullableType(schema);

                var propName = ctx.PropertyInfo?.Name;
                if (propName is null || schema is not JsonObject obj)
                    return schema;

                if (!PropertyToRef.TryGetValue(propName, out var defName))
                    return schema;

                // Array property -> keep array shape, swap items for a $ref.
                if (IsArrayType(obj))
                {
                    obj["items"] = RefNode(defName);
                    return obj;
                }

                // Scalar string property -> replace the whole schema with a $ref.
                return RefNode(defName);
            },
        };

        var rootNode = JsonSchemaExporter.GetJsonSchemaAsNode(
            SchemaContext.Default.JamlRootDocument,
            options);

        var root = rootNode as JsonObject
            ?? throw new InvalidOperationException("JsonSchemaExporter did not return an object.");

        // Strip any top-level metadata the exporter injects ($schema/type/etc.) so we can emit
        // our own canonical ordering.
        root.Remove("$schema");
        root.Remove("type");

        var result = new JsonObject
        {
            ["$schema"] = "https://json-schema.org/draft/2020-12/schema",
            ["$id"] = SchemaId,
            ["title"] = SchemaTitle,
            ["description"] = SchemaDescription,
            ["type"] = "object",
            ["$defs"] = BuildDefs(),
        };

        if (root["properties"] is JsonNode props)
        {
            root.Remove("properties");
            result["properties"] = props;
        }

        return result;
    }

    /// <summary>Writes the schema to <paramref name="outputPath"/> (and optional extra copies).</summary>
    public static void WriteToFile(string outputPath, params string[] extraCopies)
    {
        var schema = Generate();
        var json = schema.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });

        WriteAllText(outputPath, json);
        foreach (var extra in extraCopies)
            WriteAllText(extra, json);
    }

    private static void WriteAllText(string path, string content)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(path, content);
    }

    private static JsonObject RefNode(string defName) =>
        new() { ["$ref"] = $"#/$defs/{defName}" };

    private static bool IsArrayType(JsonObject obj)
    {
        var typeNode = obj["type"];
        if (typeNode is JsonValue v && v.TryGetValue<string>(out var s))
            return s == "array";
        if (typeNode is JsonArray arr)
        {
            foreach (var item in arr)
            {
                if (item is JsonValue iv && iv.TryGetValue<string>(out var t) && t == "array")
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// When <see cref="JsonSchemaExporter"/> emits <c>"type": ["X", "null"]</c> for nullable
    /// members, rewrite it in-place to plain <c>"type": "X"</c> so the resulting schema stays
    /// idiomatic for hand-authored JAML.
    /// </summary>
    private static void SimplifyNullableType(JsonNode? node)
    {
        if (node is not JsonObject obj) return;

        if (obj["type"] is JsonArray typeArr)
        {
            string? kept = null;
            int nonNullCount = 0;
            foreach (var item in typeArr)
            {
                if (item is JsonValue jv && jv.TryGetValue<string>(out var ts))
                {
                    if (ts == "null") continue;
                    kept = ts;
                    nonNullCount++;
                }
                else
                {
                    nonNullCount = -1; // bail
                    break;
                }
            }
            if (nonNullCount == 1 && kept is not null)
                obj["type"] = kept;
        }
    }

    private static JsonObject BuildDefs()
    {
        return new JsonObject
        {
            ["Joker"] = EnumDef(CombineWithWildcards(Enum.GetNames<MotelyJoker>(), "any")),
            ["CommonJoker"] = EnumDef(CombineWithWildcards(Enum.GetNames<MotelyJokerCommon>(), "any")),
            ["UncommonJoker"] = EnumDef(CombineWithWildcards(Enum.GetNames<MotelyJokerUncommon>(), "any")),
            ["RareJoker"] = EnumDef(CombineWithWildcards(Enum.GetNames<MotelyJokerRare>(), "any")),
            ["Voucher"] = EnumDef(Enum.GetNames<MotelyVoucher>()),
            ["Tarot"] = EnumDef(Enum.GetNames<MotelyTarotCard>()),
            ["Planet"] = EnumDef(Enum.GetNames<MotelyPlanetCard>()),
            ["Spectral"] = EnumDef(Enum.GetNames<MotelySpectralCard>()),
            ["Tag"] = EnumDef(Enum.GetNames<MotelyTag>()),
            ["Boss"] = EnumDef(Enum.GetNames<MotelyBossBlind>()),
            ["Deck"] = EnumDef(Enum.GetNames<MotelyDeck>()),
            ["Stake"] = EnumDef(Enum.GetNames<MotelyStake>()),
            ["Edition"] = EnumDef(Enum.GetNames<MotelyItemEdition>()),
            ["Seal"] = EnumDef(WithoutNone(Enum.GetNames<MotelyItemSeal>())),
            ["Enhancement"] = EnumDef(WithoutNone(Enum.GetNames<MotelyItemEnhancement>())),
            ["Rank"] = EnumDef(Enum.GetNames<MotelyStandardcardRank>()),
            ["Suit"] = EnumDef(Enum.GetNames<MotelyStandardcardSuit>()),
            ["Aesthetic"] = EnumDef(JamlAestheticParser.KnownJamlStringsForSchema()),
            ["Sticker"] = EnumDef(WithoutNone(Enum.GetNames<MotelyJokerSticker>())),
            ["Mode"] = EnumDef(new[] { "any", "all", "none" }),
            ["Event"] = EnumDef(Enum.GetNames<MotelyEventType>()),
        };
    }

    private static JsonObject EnumDef(IReadOnlyList<string> values)
    {
        var arr = new JsonArray();
        foreach (var v in values)
            arr.Add((JsonNode)JsonValue.Create(v));
        return new JsonObject
        {
            ["type"] = "string",
            ["enum"] = arr,
        };
    }

    private static string[] CombineWithWildcards(string[] baseNames, params string[] wildcards)
    {
        var result = new string[baseNames.Length + wildcards.Length];
        Array.Copy(baseNames, 0, result, 0, baseNames.Length);
        Array.Copy(wildcards, 0, result, baseNames.Length, wildcards.Length);
        return result;
    }

    private static string[] WithoutNone(string[] names)
    {
        var list = new List<string>(names.Length);
        foreach (var n in names)
        {
            if (!string.Equals(n, "None", StringComparison.Ordinal))
                list.Add(n);
        }
        return list.ToArray();
    }

    // ── CLI entry points ─────────────────────────────────────────────────────

    /// <summary>
    /// Default output paths (relative to repo root) when invoked via
    /// <c>dotnet run --project Motely.CLI -- --write-jaml-schema</c>.
    /// </summary>
    public static IReadOnlyList<string> DefaultOutputPaths(string repoRoot) => new[]
    {
        Path.Combine(repoRoot, "jaml.schema.json"),
        Path.Combine(repoRoot, "tools", "jaml-language", "jaml-schema", "schemas", "jaml.schema.json"),
        Path.Combine(repoRoot, "tools", "jaml-language", "vscode-extension", "schemas", "jaml.schema.json"),
    };

    public static int WriteDefault(string? repoRootOverride = null, TextWriter? log = null)
    {
        var repoRoot = repoRootOverride ?? FindRepoRoot(Environment.CurrentDirectory)
            ?? Environment.CurrentDirectory;
        var paths = DefaultOutputPaths(repoRoot);
        var schema = Generate();
        var json = schema.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });

        foreach (var p in paths)
        {
            WriteAllText(p, json);
            (log ?? Console.Out).WriteLine($"wrote {p}");
        }
        return 0;
    }

    private static string? FindRepoRoot(string start)
    {
        var dir = new DirectoryInfo(start);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Motely.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }
}
