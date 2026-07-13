#:project Motely/Motely.csproj
#:property PublishAot=false
// This is a dev-time codegen script run via `dotnet run`, never published/AOT-compiled —
// GetTypes()/JsonSerializer.Serialize() are fine here; disabling the AOT analyzer that
// file-based apps enable by default so those calls don't error as build failures.
//
// Motely.Schema — emits jaml-lang/src/generated.ts, jaml-lsp/syntaxes/jaml.tmLanguage.json,
// and jaml-lsp/schemas/jaml.schema.json. jaml-codemirror consumes jaml-lang's generated.ts
// too (via validate/getCompletions), so it never needs its own generation step.
//
// Run:  dotnet run Motely.Schema.cs                 (from repo root)
//       dotnet run Motely.Schema.cs -- --dry-run     (show paths only)
//
// A C# 14 file-based app (no .csproj of its own).
// All data comes directly from the real clause/source types via JamlDiscriminatorRegistry
// (Motely/Filters/Jaml/JamlDiscriminatorRegistry.cs) — reflection over ClauseKeys/SourceKeys
// fields that live on the types themselves, plus Enum.GetNames<T>() for the item vocab.
// Zero hand-copied grammar tables.

using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Motely.Filters.Jaml;

// ── Locate repo root ──────────────────────────────────────────────────────────
// File-based apps build into a temp cache dir (AppContext.BaseDirectory is useless here) —
// [CallerFilePath] gets the compiler to bake in this file's real source path instead. This
// script lives directly at the repo root, so its own directory IS the repo root.

string ThisFilePath([System.Runtime.CompilerServices.CallerFilePath] string path = "") => path;
var repoRoot = Path.GetDirectoryName(ThisFilePath())!;
if (!File.Exists(Path.Combine(repoRoot, "Motely.slnx")))
    throw new InvalidOperationException($"Could not locate repo root from '{repoRoot}'.");

bool dryRun = args.Contains("--dry-run");

// ── Data — straight from JamlDiscriminatorRegistry + real types ─────────────

var entries = JamlDiscriminatorRegistry.Entries;
var discriminators = entries.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToArray();

var discClauseKeys = discriminators.ToDictionary(
    d => d,
    d => JamlDiscriminatorRegistry.ClauseKeysFor(d),
    StringComparer.OrdinalIgnoreCase
);
var discSourceKeys = discriminators
    .Where(d => entries[d].SourceConfigType != null)
    .ToDictionary(d => d, d => JamlDiscriminatorRegistry.SourceKeysFor(d), StringComparer.OrdinalIgnoreCase);
var discValueEnum = discriminators.ToDictionary(
    d => d,
    d => entries[d].ValueEnumType?.Name,
    StringComparer.OrdinalIgnoreCase
);

// Every enum type referenced by any discriminator's value, reflected by name via Enum.GetNames.
var enumTypeNames = discriminators
    .Select(d => entries[d].ValueEnumType)
    .Where(t => t != null)
    .Select(t => t!.Name)
    .Distinct()
    .OrderBy(n => n)
    .ToArray();

string[] EnumMembers(string enumTypeName)
{
    var type = entries.Values.Select(e => e.ValueEnumType).FirstOrDefault(t => t?.Name == enumTypeName);
    if (type is null)
        throw new InvalidOperationException($"No discriminator references enum type '{enumTypeName}'.");
    var method = typeof(Enum).GetMethod(nameof(Enum.GetNames), [typeof(Type)])!;
    return (string[])method.Invoke(null, [type])!;
}

var enumMap = enumTypeNames.ToDictionary(n => n, EnumMembers);

// Root keys straight from JamlConfig.RootKeys — no separate hand-copied list.
var rootKeys = JamlConfig.RootKeys;

// Clause-level keys whose value is constrained to an enum (edition/enhancement/seal/suit/rank/
// stickers) — this one small table isn't derivable from ClauseKeys alone (ClauseKeys only says
// a key exists, not what enum backs its value), so it stays hand-written, but it's small,
// stable, and doesn't touch the grammar-drift-prone stuff (which discriminators/sources exist).
var clauseKeyValueEnum = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
{
    ["edition"] = "MotelyItemEdition",
    ["enhancement"] = "MotelyItemEnhancement",
    ["seal"] = "MotelyItemSeal",
    ["suit"] = "MotelyStandardcardSuit",
    ["rank"] = "MotelyStandardcardRank",
    ["stickers"] = "MotelyJokerSticker",
};
// Root keys whose value is constrained to an enum. Emitted as RootValueEnums and, like the
// clause-level table, their enums must be present in enumMap for validation to have members.
var rootValueEnums = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
{
    ["deck"] = "MotelyDeck",
    ["stake"] = "MotelyStake",
};

// These enums aren't always referenced as a discriminator's own value (e.g. MotelyItemEdition
// backs a clause-level "edition:" key, not a discriminator), so make sure they're in enumMap too.
foreach (var enumName in clauseKeyValueEnum.Values.Concat(rootValueEnums.Values).Distinct())
{
    if (enumMap.ContainsKey(enumName)) continue;
    var type = typeof(JamlConfig).Assembly.GetTypes().FirstOrDefault(t => t.Name == enumName && t.IsEnum)
        ?? throw new InvalidOperationException($"Could not find enum type '{enumName}' in the Motely assembly.");
    var method = typeof(Enum).GetMethod(nameof(Enum.GetNames), [typeof(Type)])!;
    enumMap[enumName] = (string[])method.Invoke(null, [type])!;
}

// Union of all clause-level keys across every discriminator, for the TextMate grammar.
var allClauseLevelKeys = discClauseKeys.Values
    .SelectMany(v => v)
    .Concat(["with", "luck", "vouchers", "sources", "ante", "antes", "min", "max", "score", "label"])
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .OrderBy(k => k)
    .ToArray();
var allSourceKeys = discSourceKeys.Values.SelectMany(v => v).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

Console.WriteLine($"Loaded {enumMap.Count} enums, {discriminators.Length} discriminators");

// ── Output paths ──────────────────────────────────────────────────────────────

var generatedTsPath = Path.Combine(repoRoot, "jaml-lang", "src", "generated.ts");
var tmGrammarPath = Path.Combine(repoRoot, "jaml-lsp", "syntaxes", "jaml.tmLanguage.json");
var jsonSchemaPath = Path.Combine(repoRoot, "jaml-lsp", "schemas", "jaml.schema.json");

if (dryRun)
{
    Console.WriteLine($"[dry-run] {generatedTsPath}");
    Console.WriteLine($"[dry-run] {tmGrammarPath}");
    Console.WriteLine($"[dry-run] {jsonSchemaPath}");
    return 0;
}

Directory.CreateDirectory(Path.GetDirectoryName(generatedTsPath)!);
Directory.CreateDirectory(Path.GetDirectoryName(tmGrammarPath)!);
Directory.CreateDirectory(Path.GetDirectoryName(jsonSchemaPath)!);

// ── generated.ts ──────────────────────────────────────────────────────────────

var ts = new StringBuilder();
ts.AppendLine("// AUTO-GENERATED by Motely.Schema.cs — DO NOT EDIT.");
ts.AppendLine("// Source of truth: the real clause/source-config C# types, via");
ts.AppendLine("// Motely/Filters/Jaml/JamlDiscriminatorRegistry.cs + enum reflection.");
ts.AppendLine();

ts.AppendLine("export const Enums: Record<string, readonly string[]> = {");
foreach (var (name, members) in enumMap.OrderBy(kv => kv.Key))
    ts.AppendLine($"    {name}: [{string.Join(", ", members.Select(m => $"\"{m}\""))}],");
ts.AppendLine("};");
ts.AppendLine();

ts.AppendLine($"export const Discriminators: readonly string[] = [{string.Join(", ", discriminators.Select(k => $"\"{k}\""))}];");
ts.AppendLine($"export const RootKeys: readonly string[] = [{string.Join(", ", rootKeys.OrderBy(k => k).Select(k => $"\"{k}\""))}];");
// No AllClauseLevelKeys export: a clause key is only valid relative to a discriminator
// (DiscriminatorClauseKeys). The flat union exists solely for the TextMate grammar below,
// where highlighting cannot be context-sensitive.
ts.AppendLine();

ts.AppendLine($"export const RootValueEnums: Record<string, string> = {{ {string.Join(", ", rootValueEnums.Select(kv => $"{kv.Key}: \"{kv.Value}\""))} }};");
ts.AppendLine();

ts.AppendLine("export const DiscriminatorValueEnum: Record<string, string> = {");
foreach (var (disc, enumName) in discValueEnum.Where(kv => kv.Value != null))
    ts.AppendLine($"    \"{disc}\": \"{enumName}\",");
ts.AppendLine("};");
ts.AppendLine();

ts.AppendLine("export const ClauseKeyValueEnum: Record<string, string> = {");
foreach (var (key, enumName) in clauseKeyValueEnum.OrderBy(kv => kv.Key))
    ts.AppendLine($"    \"{key}\": \"{enumName}\",");
ts.AppendLine("};");
ts.AppendLine();

ts.AppendLine("export const DiscriminatorClauseKeys: Record<string, readonly string[]> = {");
foreach (var (disc, keys) in discClauseKeys)
    ts.AppendLine($"    \"{disc}\": [{string.Join(", ", keys.Select(k => $"\"{k}\""))}],");
ts.AppendLine("};");
ts.AppendLine();

ts.AppendLine("export const DiscriminatorSourceKeys: Record<string, readonly string[]> = {");
foreach (var (disc, keys) in discSourceKeys)
    ts.AppendLine($"    \"{disc}\": [{string.Join(", ", keys.Select(k => $"\"{k}\""))}],");
ts.AppendLine("};");

File.WriteAllText(generatedTsPath, ts.ToString(), Encoding.UTF8);
Console.WriteLine($"wrote {Path.GetRelativePath(repoRoot, generatedTsPath)}");

// ── TextMate grammar ──────────────────────────────────────────────────────────

var allEnumMembers = enumMap.Values.SelectMany(v => v).Distinct().OrderByDescending(m => m.Length).ToList();
var allNonDiscKeys = rootKeys.Concat(allClauseLevelKeys).Concat(allSourceKeys)
    .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

string Alt(IEnumerable<string> words) =>
    $@"\b(?:{string.Join("|", words.Distinct().OrderByDescending(w => w.Length).Select(Regex.Escape))})(?=\s*:)";

string AltBare(IEnumerable<string> words) =>
    $@"\b(?:{string.Join("|", words.Distinct().OrderByDescending(w => w.Length).Select(Regex.Escape))})\b";

var grammar = new
{
    name = "JAML",
    scopeName = "source.jaml",
    fileTypes = new[] { "jaml" },
    patterns = new object[]
    {
        new { match = "#.*$", name = "comment.line.number-sign.jaml" },
        new { match = Alt(discriminators), name = "entity.name.tag.discriminator.jaml" },
        new { match = Alt(allNonDiscKeys), name = "keyword.other.key.jaml" },
        new { match = AltBare(allEnumMembers.Append("Any")), name = "support.constant.enum.jaml" },
        new { match = @"\b(?:true|false)\b", name = "constant.language.boolean.jaml" },
        new { match = @"\b\d+\b", name = "constant.numeric.jaml" },
        new { match = "\"[^\"]*\"", name = "string.quoted.double.jaml" },
        new { match = "'[^']*'", name = "string.quoted.single.jaml" },
    }
};

File.WriteAllText(tmGrammarPath,
    JsonSerializer.Serialize(grammar, new JsonSerializerOptions { WriteIndented = true }),
    Encoding.UTF8);
Console.WriteLine($"wrote {Path.GetRelativePath(repoRoot, tmGrammarPath)}");

// ── JSON Schema ───────────────────────────────────────────────────────────────

object PropSchema(string key)
{
    if (clauseKeyValueEnum.TryGetValue(key, out var enumName))
    {
        var members = enumMap.GetValueOrDefault(enumName, []);
        return key == "stickers"
            ? new { type = "array", items = new { type = "string", @enum = members } }
            : (object)new { type = "string", @enum = members };
    }

    return key switch
    {
        "ante" => new { type = "integer", minimum = 0, maximum = 39 },
        "antes" => new { type = "array", items = new { type = "integer", minimum = 0, maximum = 39 } },
        "min" or "max" or "score" or "soulEditionRolls" or "value" => new { type = "integer" },
        "shopItems" or "boosterPacks" or "rolls" => new { type = "array", items = new { type = "integer" } },
        "soulCardOnly" or "charmTag" or "etherealTag" or "requireMega" or "requireMegaPack" => new { type = "boolean" },
        "sources" or "with" => new { type = "object" },
        _ => new { type = "string" }
    };
}

object DiscValueSchema(string disc)
{
    if (discValueEnum.TryGetValue(disc, out var en) && en != null && enumMap.TryGetValue(en, out var members))
        return new { type = "string", @enum = (object)members.Concat(["Any"]).ToArray() };
    return new { type = new[] { "string", "array" } };
}

var clauseAnyOf = discriminators.Select(disc =>
{
    var props = new Dictionary<string, object> { [disc] = DiscValueSchema(disc) };
    foreach (var k in discClauseKeys.GetValueOrDefault(disc, [])) props[k] = PropSchema(k);
    return (object)new { required = new[] { disc }, properties = props, additionalProperties = false };
}).ToList();

var rootProps = new Dictionary<string, object>
{
    ["id"] = new { type = "string" },
    ["name"] = new { type = "string" },
    ["description"] = new { type = "string" },
    ["author"] = new { type = "string" },
    ["dateCreated"] = new { type = "string", format = "date-time" },
    ["seeds"] = new { type = "array", items = new { type = "string" } },
    ["deck"] = new { type = "string", @enum = enumMap.GetValueOrDefault("MotelyDeck", []) },
    ["stake"] = new { type = "string", @enum = enumMap.GetValueOrDefault("MotelyStake", []) },
    ["must"] = new { type = "array", items = new { anyOf = clauseAnyOf } },
    ["should"] = new { type = "array", items = new { anyOf = clauseAnyOf } },
    ["mustNot"] = new { type = "array", items = new { anyOf = clauseAnyOf } },
};

var missingRoot = rootKeys.Where(k => !rootProps.ContainsKey(k)).ToArray();
if (missingRoot.Length > 0)
    throw new InvalidOperationException(
        $"JamlConfig.RootKeys missing from JSON-schema root properties: {string.Join(", ", missingRoot)}. " +
        "Add them in Motely.Schema.cs.");

var jsonSchema = new
{
    title = "JAML Filter",
    description = "Jimbo's Ante Markup Language — Balatro seed filter",
    type = "object",
    additionalProperties = false,
    properties = rootProps,
};

File.WriteAllText(jsonSchemaPath,
    JsonSerializer.Serialize(jsonSchema, new JsonSerializerOptions { WriteIndented = true }),
    Encoding.UTF8);
Console.WriteLine($"wrote {Path.GetRelativePath(repoRoot, jsonSchemaPath)}");

return 0;
