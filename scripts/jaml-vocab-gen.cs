#:project ../Motely/Motely.csproj
#:property EnableTrimAnalyzer=false
#:property EnableAotAnalyzer=false
#:property EnableSingleFileAnalyzer=false
#:property TreatWarningsAsErrors=false

// Single-file vocab generator: reflects over every IJamlClause type decorated with
// [JamlClauseAttribute] and emits its discriminator key(s) + full nested property schema as JSON.
// Source of truth is the attribute on the clause class itself (next to its real *FilterDesc),
// not a hand-maintained parallel list — run this after adding a clause type to refresh the vocab.
//
// Nested reference types (e.g. LegendaryJokerSourceConfig) are expanded recursively — not just
// named by their CLR type — and enum-typed properties list their real member names, so an editor
// can scope suggestions per clause instead of showing every property/value everywhere.

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Motely.Filters.Jaml;

var clauseTypes = typeof(IJamlClause).Assembly
    .GetTypes()
    .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IJamlClause).IsAssignableFrom(t))
    .Select(t => (Type: t, Attr: t.GetCustomAttribute<JamlClauseAttribute>()))
    .Where(x => x.Attr is not null)
    .OrderBy(x => x.Attr!.Key)
    .ToArray();

var vocab = clauseTypes
    .Select(x => new VocabEntry(x.Attr!.Key, x.Attr.Aliases, x.Type.Name, DescribeProperties(x.Type, [])))
    .ToArray();

Console.WriteLine(JsonSerializer.Serialize(vocab, VocabJsonContext.Default.VocabEntryArray));

static VocabProperty[] DescribeProperties(Type type, HashSet<Type> visited)
{
    if (!visited.Add(type))
        return []; // cycle guard — none of the real clause/source types are cyclic, but stay safe.

    return
    [
        .. type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite)
            .Select(p => DescribeProperty(p, visited)),
    ];
}

static VocabProperty DescribeProperty(PropertyInfo prop, HashSet<Type> visited)
{
    var t = prop.PropertyType;
    var underlying = Nullable.GetUnderlyingType(t) ?? t;
    var elementType = underlying.IsArray ? underlying.GetElementType()! : underlying;
    var isEnum = elementType.IsEnum;
    var isComplex = !isEnum && elementType.IsClass && elementType != typeof(string);

    return new VocabProperty(
        prop.Name,
        DescribeTypeName(t),
        isEnum ? Enum.GetNames(elementType) : null,
        isComplex ? DescribeProperties(elementType, visited) : null
    );
}

static string DescribeTypeName(Type t)
{
    var underlying = Nullable.GetUnderlyingType(t) ?? t;
    var name = underlying.IsArray
        ? DescribeTypeName(underlying.GetElementType()!) + "[]"
        : underlying.Name;
    return Nullable.GetUnderlyingType(t) is not null ? name + "?" : name;
}

record VocabEntry(string Key, string[] Aliases, string ClrType, VocabProperty[] Properties);

record VocabProperty(string Name, string Type, string[]? EnumValues, VocabProperty[]? Properties);

[JsonSourceGenerationOptions(WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(VocabEntry[]))]
partial class VocabJsonContext : JsonSerializerContext;
