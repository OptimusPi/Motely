using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using Motely;
using Motely.Filters;

namespace Motely.WasmTools;

public static partial class MotelyJamlSchemaGenerator
{
    private const string SchemaId = "https://www.seedfinder.app/jaml.schema.json";
    private const string SchemaTitle = "JAML — Jimbo's Ante Markup Language";
    private const string SchemaDescription =
        "JSON Schema for JAML (.jaml), Motely's Balatro seed search language. Use it for validation, completions, and editor tooling.";
    private const string JamlClauseDef = nameof(JamlClauseDto);
    private const string JamlSourcesDef = nameof(JamlSourcesDto);

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
        ["legendaryJokers"] = "LegendaryJoker",
        ["voucher"] = "Voucher",
        ["vouchers"] = "Voucher",
        ["tarotCard"] = "Tarot",
        ["tarotCards"] = "Tarot",
        ["spectralCard"] = "Spectral",
        ["spectralCards"] = "Spectral",
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
        ["standardCards"] = "StandardCard",
        ["and"] = JamlClauseDef,
        ["or"] = JamlClauseDef,
        ["clauses"] = JamlClauseDef,
        ["sources"] = JamlSourcesDef,
    };

    [JsonSourceGenerationOptions(
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        GenerationMode = JsonSourceGenerationMode.Metadata)]
    [JsonSerializable(typeof(JamlRootDocument))]
    [JsonSerializable(typeof(JamlClauseDto))]
    [JsonSerializable(typeof(JamlSourcesDto))]
    [JsonSerializable(typeof(JamlDefaultsDto))]
    internal sealed partial class SchemaContext : JsonSerializerContext
    {
    }

    public static JsonObject Generate()
    {
        var rootNode = JsonSchemaExporter.GetJsonSchemaAsNode(
            SchemaContext.Default.JamlRootDocument,
            ExporterOptions());

        var root = rootNode as JsonObject
            ?? throw new InvalidOperationException("JsonSchemaExporter did not return an object.");

        root.Remove("$schema");
        root.Remove("type");

        var result = new JsonObject
        {
            ["$schema"] = "https://json-schema.org/draft/2020-12/schema",
            ["$id"] = SchemaId,
            ["version"] = GetSchemaVersion(),
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

    private static string GetSchemaVersion()
    {
        var repoRoot = FindRepoRoot(Environment.CurrentDirectory);
        if (repoRoot is not null)
        {
            var propsPath = Path.Combine(repoRoot, "Directory.Packages.props");
            if (File.Exists(propsPath))
            {
                var props = XDocument.Load(propsPath);
                var version = props.Root?
                    .Element("PropertyGroup")?
                    .Element("MotelyVersion")?
                    .Value;

                if (!string.IsNullOrWhiteSpace(version))
                    return version.Trim();
            }
        }

        return typeof(MotelyJamlSchemaGenerator).Assembly.GetName().Version?.ToString()
            ?? "0.0.0";
    }

    public static void WriteToFile(string outputPath, params string[] extraCopies)
    {
        var schema = Generate();
        var json = schema.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        }).ReplaceLineEndings("\n");

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
            ["items"] = RefNode(JamlClauseDef),
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
            [JamlClauseDef] = CriterionDef(),
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
            SchemaContext.Default.JamlClauseDto,
            ExporterOptions());

        var result = node as JsonObject
            ?? throw new InvalidOperationException("JsonSchemaExporter did not return an object.");
        result.Remove("$schema");
        return result;
    }

    private static JsonObject SourcesDef()
    {
        var node = JsonSchemaExporter.GetJsonSchemaAsNode(
            SchemaContext.Default.JamlSourcesDto,
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
        Path.Combine(repoRoot, "packages", "jaml-language-core", "schema", "jaml.schema.json"),
        Path.Combine(repoRoot, "packages", "jaml-language-support", "schema", "jaml.schema.json"),
    };

    public static IReadOnlyList<string> DefaultItemFormatTypeOutputPaths(string repoRoot) => new[]
    {
        Path.Combine(repoRoot, "motely-wasm", "motely-item-formats.d.ts"),
        Path.Combine(repoRoot, "packages", "jaml-language-core", "motely-item-formats.d.ts"),
        Path.Combine(repoRoot, "packages", "jaml-language-support", "schema", "motely-item-formats.d.ts"),
    };

    public static IReadOnlyList<string> DefaultItemFormatModuleOutputPaths(string repoRoot) => new[]
    {
        Path.Combine(repoRoot, "motely-wasm", "motely-item-formats.mjs"),
        Path.Combine(repoRoot, "packages", "jaml-language-core", "motely-item-formats.mjs"),
        Path.Combine(repoRoot, "packages", "jaml-language-support", "schema", "motely-item-formats.mjs"),
    };

    public static string GenerateItemFormatTypescript()
    {
        var sb = new StringBuilder();
        sb.AppendLine("export interface MotelyItemFormatEntry {");
        sb.AppendLine("  readonly value: number;");
        sb.AppendLine("  readonly enumName: string;");
        sb.AppendLine("  readonly displayName: string;");
        sb.AppendLine("  readonly category: string;");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("export declare const MOTELY_ITEM_FORMATS_BY_VALUE: {");

        foreach (var itemType in Enum.GetValues<MotelyItemType>().OrderBy(static itemType => (int)itemType))
        {
            var item = new MotelyItem(itemType);
            var value = (int)item.Value;
            var enumName = itemType.ToString();
            var displayName = FormatUtils.FormatItem(item);
            var category = item.TypeCategory.ToString();
            sb.AppendLine($"  readonly {value}: {{ readonly value: {value}; readonly enumName: {JsonString(enumName)}; readonly displayName: {JsonString(displayName)}; readonly category: {JsonString(category)}; }};");
        }

        sb.AppendLine("};");
        sb.AppendLine();
        sb.AppendLine("export declare const MOTELY_ITEM_FORMATS_BY_ENUM_NAME: {");

        foreach (var itemType in Enum.GetValues<MotelyItemType>().OrderBy(static itemType => itemType.ToString(), StringComparer.Ordinal))
        {
            var item = new MotelyItem(itemType);
            var value = (int)item.Value;
            var enumName = itemType.ToString();
            var displayName = FormatUtils.FormatItem(item);
            var category = item.TypeCategory.ToString();
            sb.AppendLine($"  readonly {enumName}: {{ readonly value: {value}; readonly enumName: {JsonString(enumName)}; readonly displayName: {JsonString(displayName)}; readonly category: {JsonString(category)}; }};");
        }

        sb.AppendLine("};");
        sb.AppendLine();
        sb.AppendLine("export type MotelyItemEnumName = keyof typeof MOTELY_ITEM_FORMATS_BY_ENUM_NAME;");
        sb.AppendLine("export type MotelyItemPackedValue = keyof typeof MOTELY_ITEM_FORMATS_BY_VALUE;");
        return sb.ToString().ReplaceLineEndings("\n");
    }

    public static string GenerateItemFormatModule()
    {
        var entries = Enum.GetValues<MotelyItemType>()
            .OrderBy(static itemType => (int)itemType)
            .Select(static itemType =>
            {
                var item = new MotelyItem(itemType);
                return new
                {
                    value = (int)item.Value,
                    enumName = itemType.ToString(),
                    displayName = FormatUtils.FormatItem(item),
                    category = item.TypeCategory.ToString(),
                };
            })
            .ToArray();

        var sb = new StringBuilder();
        sb.AppendLine("export const MOTELY_ITEM_FORMATS_BY_VALUE = Object.freeze({");
        foreach (var entry in entries)
            sb.AppendLine($"  {entry.value}: Object.freeze({{ value: {entry.value}, enumName: {JsonString(entry.enumName)}, displayName: {JsonString(entry.displayName)}, category: {JsonString(entry.category)} }}),");
        sb.AppendLine("});");
        sb.AppendLine();
        sb.AppendLine("export const MOTELY_ITEM_FORMATS_BY_ENUM_NAME = Object.freeze({");
        foreach (var entry in entries.OrderBy(static entry => entry.enumName, StringComparer.Ordinal))
            sb.AppendLine($"  {entry.enumName}: MOTELY_ITEM_FORMATS_BY_VALUE[{entry.value}],");
        sb.AppendLine("});");
        return sb.ToString().ReplaceLineEndings("\n");
    }

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
        }).ReplaceLineEndings("\n");

        foreach (var p in paths)
        {
            WriteAllText(p, json);
            (log ?? Console.Out).WriteLine($"wrote {p}");
        }

        var itemFormatTypescript = GenerateItemFormatTypescript();
        foreach (var p in DefaultItemFormatTypeOutputPaths(repoRoot))
        {
            WriteAllText(p, itemFormatTypescript);
            (log ?? Console.Out).WriteLine($"wrote {p}");
        }

        var itemFormatModule = GenerateItemFormatModule();
        foreach (var p in DefaultItemFormatModuleOutputPaths(repoRoot))
        {
            WriteAllText(p, itemFormatModule);
            (log ?? Console.Out).WriteLine($"wrote {p}");
        }
        return 0;
    }

    private static string JsonString(string value) => JsonSerializer.Serialize(value);

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

