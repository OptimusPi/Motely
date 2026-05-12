#:project Motely/Motely.csproj
#:property PublishAot=false

// Single-file .NET 10 schema generator. Run with:
//   dotnet run jaml-schema.cs
// Writes jaml.schema.json at the repo root.
//
// Walks the JAML deserialization POCOs (JamlRootDocument / JamlClauseDto /
// JamlSourcesDto / JamlDefaultsDto / StandardCardConfigDto) and emits JSON
// Schema 2020-12 with all enum literals inlined. Reflection is fine here —
// this tool runs once on demand and never ships to WASM/AOT.

using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Motely;
using Motely.Filters;
using YamlDotNet.Serialization;

const string OutputPath = "jaml.schema.json";

var defs = new JsonObject();
var root = new JsonObject
{
    ["$schema"] = "https://json-schema.org/draft/2020-12/schema",
    ["$id"] = "https://seedfinder.app/jaml.schema.json",
    ["title"] = "JAML — Jimbo's Ante Markup Language",
    ["description"] = "Generated from Motely's JAML POCOs. Do not hand-edit; regenerate via `dotnet run jaml-schema.cs`.",
    ["type"] = "object",
    ["additionalProperties"] = false,
};

PopulateObject(typeof(JamlRootDocument), root, defs);

// Root-level deck/stake are string in the POCO but the loader parses them as enums.
var rootProps = (JsonObject)root["properties"]!;
rootProps["deck"] = SchemaFor(typeof(MotelyDeck), defs);
rootProps["stake"] = SchemaFor(typeof(MotelyStake), defs);

root["$defs"] = defs;

File.WriteAllText(
    OutputPath,
    root.ToJsonString(new JsonSerializerOptions
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    }) + "\n"
);
Console.WriteLine($"wrote {OutputPath} ({defs.Count} type defs, {((JsonObject)root["properties"]!).Count} root keys)");

static void PopulateObject(Type type, JsonObject target, JsonObject defs)
{
    var props = new JsonObject();
    foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
    {
        if (p.GetCustomAttribute<JsonIgnoreAttribute>() is not null) continue;
        var key = p.GetCustomAttribute<YamlMemberAttribute>()?.Alias ?? Camel(p.Name);
        props[key] = SchemaFor(Unwrap(p.PropertyType), defs);
    }
    target["properties"] = props;
}

static JsonNode SchemaFor(Type type, JsonObject defs)
{
    if (type == typeof(string)) return new JsonObject { ["type"] = "string" };
    if (type == typeof(bool))   return new JsonObject { ["type"] = "boolean" };
    if (type == typeof(int) || type == typeof(long)) return new JsonObject { ["type"] = "integer" };
    if (type == typeof(double) || type == typeof(float) || type == typeof(decimal))
        return new JsonObject { ["type"] = "number" };

    if (type.IsEnum) return EnumRef(type, defs);

    // EnumOrAny<T>: `any` literal or one of the enum values.
    if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(EnumOrAny<>))
    {
        var inner = type.GetGenericArguments()[0];
        return new JsonObject
        {
            ["oneOf"] = new JsonArray(
                new JsonObject { ["const"] = "any" },
                EnumRef(inner, defs)
            ),
        };
    }

    // StandardCardValue: bare string ("Ah", "Ks") or full StandardCardConfigDto object.
    if (type == typeof(StandardCardValue))
    {
        return new JsonObject
        {
            ["oneOf"] = new JsonArray(
                new JsonObject { ["type"] = "string" },
                ObjectRef(typeof(StandardCardConfigDto), defs)
            ),
        };
    }

    // Arrays / List<T>.
    var elem = ElementType(type);
    if (elem is not null)
    {
        return new JsonObject
        {
            ["type"] = "array",
            ["items"] = SchemaFor(Unwrap(elem), defs),
        };
    }

    // Class / struct — recurse into $defs.
    return ObjectRef(type, defs);
}

static JsonNode EnumRef(Type enumType, JsonObject defs)
{
    var name = enumType.Name;
    if (!defs.ContainsKey(name))
    {
        defs[name] = null!; // placeholder to break cycles
        var values = new JsonArray();
        foreach (var n in Enum.GetNames(enumType))
            values.Add(n);
        defs[name] = new JsonObject
        {
            ["type"] = "string",
            ["enum"] = values,
        };
    }
    return new JsonObject { ["$ref"] = $"#/$defs/{name}" };
}

static JsonNode ObjectRef(Type type, JsonObject defs)
{
    var name = type.Name;
    if (!defs.ContainsKey(name))
    {
        defs[name] = null!; // placeholder to break cycles
        var obj = new JsonObject
        {
            ["type"] = "object",
            ["additionalProperties"] = false,
        };
        PopulateObject(type, obj, defs);
        defs[name] = obj;
    }
    return new JsonObject { ["$ref"] = $"#/$defs/{name}" };
}

static Type? ElementType(Type t)
{
    if (t.IsArray) return t.GetElementType();
    if (t.IsGenericType)
    {
        var def = t.GetGenericTypeDefinition();
        if (def == typeof(List<>) || def == typeof(IList<>) || def == typeof(IEnumerable<>) || def == typeof(IReadOnlyList<>))
            return t.GetGenericArguments()[0];
    }
    return null;
}

static Type Unwrap(Type t) => Nullable.GetUnderlyingType(t) ?? t;

static string Camel(string s) =>
    string.IsNullOrEmpty(s) ? s : char.ToLowerInvariant(s[0]) + s[1..];
