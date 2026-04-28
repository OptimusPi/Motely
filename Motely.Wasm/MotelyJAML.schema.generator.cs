using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization;
using Motely;
using Motely.Filters;

namespace Motely.WasmTools;

public static partial class MotelyJamlSchemaGenerator
{
    private const string SchemaId = "https://mcp.seedfinder.app/jaml.schema.json";
    private const string SchemaTitle = "JAML — Jimbo's Ante Markup Language";
    private const string SchemaDescription =
        "JSON Schema for JAML (.jaml), Motely's Balatro seed search language. Use it for validation, completions, and editor tooling.";
    private const string JamlCriterionDef = nameof(JamlCriterion);
    private const string JamlSourcesDef = nameof(JamlSources);

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
        ["legendaryJoker"] = "LegendaryJoker",
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
        ["standardCard"] = "StandardCard",
        ["and"] = JamlCriterionDef,
        ["or"] = JamlCriterionDef,
        ["clauses"] = JamlCriterionDef,
        ["sources"] = JamlSourcesDef,
    };

    [JsonSourceGenerationOptions(
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        GenerationMode = JsonSourceGenerationMode.Metadata)]
    [JsonSerializable(typeof(JamlDocument))]
    [JsonSerializable(typeof(JamlCriterion))]
    [JsonSerializable(typeof(JamlSources))]
    [JsonSerializable(typeof(JamlDefaults))]
    internal sealed partial class SchemaContext : JsonSerializerContext
    {
    }

    public static JsonObject Generate()
    {
        var rootNode = JsonSchemaExporter.GetJsonSchemaAsNode(
            SchemaContext.Default.JamlDocument,
            ExporterOptions());

        var root = rootNode as JsonObject
            ?? throw new InvalidOperationException("JsonSchemaExporter did not return an object.");

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
            result["properties"] = RewriteCriterionArrays(props);
        }

        return result;
    }

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

    private static JsonObject RewriteCriterionArrays(JsonNode props)
    {
        var result = props.DeepClone().AsObject();
        result["must"] = CriterionArrayDef();
        result["should"] = CriterionArrayDef();
        result["mustNot"] = CriterionArrayDef();
        return result;
    }

    private static JsonObject CriterionArrayDef() =>
        new()
        {
            ["type"] = "array",
            ["items"] = RefNode(JamlCriterionDef),
        };

    private static void WriteAllText(string path, string content)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(path, content);
    }

    private static JsonObject RefNode(string defName) =>
        new() { ["$ref"] = $"#/$defs/{defName}" };

    private static JsonSchemaExporterOptions ExporterOptions() =>
        new()
        {
            TreatNullObliviousAsNonNullable = true,
            TransformSchemaNode = static (ctx, schema) =>
            {
                SimplifyNullableType(schema);

                var propName = ctx.PropertyInfo?.Name;
                if (propName is null || schema is not JsonObject obj)
                    return schema;

                if (!PropertyToRef.TryGetValue(propName, out var defName))
                    return schema;

                if (IsArrayType(obj))
                {
                    obj["items"] = RefNode(defName);
                    return obj;
                }

                return RefNode(defName);
            },
        };

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
                    nonNullCount = -1;
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
            [JamlCriterionDef] = CriterionDef(),
            ["Joker"] = EnumDef(CombineWithWildcards(Enum.GetNames<MotelyJoker>(), "any")),
            ["CommonJoker"] = EnumDef(CombineWithWildcards(Enum.GetNames<MotelyJokerCommon>(), "any")),
            ["UncommonJoker"] = EnumDef(CombineWithWildcards(Enum.GetNames<MotelyJokerUncommon>(), "any")),
            ["RareJoker"] = EnumDef(CombineWithWildcards(Enum.GetNames<MotelyJokerRare>(), "any")),
            ["LegendaryJoker"] = EnumDef(CombineWithWildcards(Enum.GetNames<MotelyJokerLegendary>(), "any")),
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
            ["Sticker"] = EnumDef(WithoutNone(Enum.GetNames<MotelyJokerSticker>())),
            ["Mode"] = EnumDef(new[] { "any", "all", "none" }),
            ["StandardCard"] = StandardCardDef(),
            [JamlSourcesDef] = SourcesDef(),
        };
    }

    private static JsonObject CriterionDef()
    {
        var node = JsonSchemaExporter.GetJsonSchemaAsNode(
            SchemaContext.Default.JamlCriterion,
            ExporterOptions());

        var result = node as JsonObject
            ?? throw new InvalidOperationException("JsonSchemaExporter did not return an object.");
        result.Remove("$schema");
        return result;
    }

    private static JsonObject SourcesDef()
    {
        var node = JsonSchemaExporter.GetJsonSchemaAsNode(
            SchemaContext.Default.JamlSources,
            ExporterOptions());

        var result = node as JsonObject
            ?? throw new InvalidOperationException("JsonSchemaExporter did not return an object.");
        result.Remove("$schema");
        return result;
    }

    private static JsonObject StandardCardDef()
    {
        var anyOf = new JsonArray();
        anyOf.Add((JsonNode)new JsonObject { ["type"] = "string" });
        anyOf.Add((JsonNode)new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["rank"] = RefNode("Rank"),
                ["suit"] = RefNode("Suit"),
                ["seal"] = RefNode("Seal"),
                ["enhancement"] = RefNode("Enhancement"),
                ["edition"] = RefNode("Edition")
            }
        });

        return new JsonObject { ["anyOf"] = anyOf };
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

    public static IReadOnlyList<string> DefaultOutputPaths(string repoRoot) => new[]
    {
        Path.Combine(repoRoot, "jaml.schema.json"),
        Path.Combine(repoRoot, "motely-wasm", "jaml.schema.json"),
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

