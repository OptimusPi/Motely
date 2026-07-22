using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Motely.Generators;

/// <summary>
/// Turns every <c>[JamlDiscriminator]</c>-annotated clause type into <c>JamlSchema.g.cs</c>:
/// switch-based lookups from discriminator to clause keys, source keys, clause/source/enum
/// types, and rolls behavior. The FilterDescs are the grammar; this generator is only the
/// index over them — add a clause, rebuild, and the schema already knows it.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class JamlSchemaGenerator : IIncrementalGenerator
{
    private const string AttributeFullName = "Motely.Filters.Jaml.JamlDiscriminatorAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // ForAttributeWithMetadataName only visits nodes that actually carry the attribute,
        // so edits elsewhere in the compilation never re-run the transform.
        var entries = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                AttributeFullName,
                predicate: static (_, _) => true,
                transform: static (ctx, _) => ReadEntry(ctx))
            .Where(static e => e is not null)
            .Select(static (e, _) => e!);

        var ordered = entries.Collect()
            .Select(static (all, _) => new EquatableArray<SchemaEntry>(
                [.. all.OrderBy(e => e.Normalized.Items[0], StringComparer.Ordinal)]));

        context.RegisterSourceOutput(ordered, static (spc, all) =>
            spc.AddSource("JamlSchema.g.cs", Emit(all.Items)));
    }

    // ── Reading one annotated clause type ───────────────────────────────────────────────

    private static SchemaEntry? ReadEntry(GeneratorAttributeSyntaxContext ctx)
    {
        if (ctx.TargetSymbol is not INamedTypeSymbol type)
            return null;
        var attr = ctx.Attributes[0];

        var wires = ReadWires(attr);
        if (wires.Length == 0)
            return null;

        var sourceConfig = NamedType(attr, "SourceConfigType");
        return new SchemaEntry(
            new EquatableArray<string>(wires),
            new EquatableArray<string>([.. wires.Select(Normalize)]),
            Display(type),
            StaticStringArrayMember(type, "ClauseKeys", walkBaseTypes: true),
            sourceConfig is null ? null : Display(sourceConfig),
            sourceConfig is null ? null : StaticStringArrayMember(sourceConfig, "SourceKeys", walkBaseTypes: false),
            NamedType(attr, "ValueEnum") is { } ve ? Display(ve) : null,
            NamedBool(attr, "RollsAreInlineValue"),
            NamedIntArray(attr, "RollsDefault"));
    }

    private static string[] ReadWires(AttributeData attr)
    {
        if (attr.ConstructorArguments.Length == 0)
            return [];
        var arg = attr.ConstructorArguments[0];
        return arg.Kind switch
        {
            TypedConstantKind.Array => [.. arg.Values.Select(v => (string)v.Value!)],
            TypedConstantKind.Primitive when arg.Value is string single => [single],
            _ => [],
        };
    }

    private static INamedTypeSymbol? NamedType(AttributeData attr, string name) =>
        attr.NamedArguments.FirstOrDefault(a => a.Key == name).Value is
            { Kind: TypedConstantKind.Type, Value: INamedTypeSymbol symbol } ? symbol : null;

    private static bool NamedBool(AttributeData attr, string name) =>
        attr.NamedArguments.FirstOrDefault(a => a.Key == name).Value is
            { Kind: TypedConstantKind.Primitive, Value: true };

    private static EquatableArray<int>? NamedIntArray(AttributeData attr, string name)
    {
        var value = attr.NamedArguments.FirstOrDefault(a => a.Key == name).Value;
        if (value.Kind != TypedConstantKind.Array || value.Values.IsDefault)
            return null;
        return new EquatableArray<int>([.. value.Values.Select(v => (int)v.Value!)]);
    }

    /// <summary>A static <c>string[]</c> field or property named <paramref name="name"/>,
    /// rendered as a fully-qualified member reference the emitted switch can cite.</summary>
    private static string? StaticStringArrayMember(INamedTypeSymbol type, string name, bool walkBaseTypes)
    {
        for (var t = (INamedTypeSymbol?)type; t is not null; t = walkBaseTypes ? t.BaseType : null)
        {
            var found = t.GetMembers(name).Any(m => m switch
            {
                IFieldSymbol { IsStatic: true, Type: IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_String } } => true,
                IPropertySymbol { IsStatic: true, Type: IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_String } } => true,
                _ => false,
            });
            if (found)
                return $"{Display(t)}.{name}";
        }
        return null;
    }

    private static string Display(INamedTypeSymbol type) =>
        type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    /// <summary>Mirror of the engine's discriminator normalization: JAML accepts
    /// <c>Booster Packs</c>, <c>booster-packs</c>, and <c>boosterPacks</c> as one word.</summary>
    private static string Normalize(string discriminator) =>
        discriminator.ToLowerInvariant().Replace(" ", "").Replace("-", "").Replace("_", "");

    // ── Emitting JamlSchema.g.cs ────────────────────────────────────────────────────────

    private static string Emit(SchemaEntry[] entries)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/> Written by Motely.Generators from [JamlDiscriminator] attributes.");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("namespace Motely.Filters.Jaml;");
        sb.AppendLine();
        sb.AppendLine("public static partial class JamlSchema");
        sb.AppendLine("{");
        sb.AppendLine("    private static string N(string d) =>");
        sb.AppendLine("        d.ToLowerInvariant().Replace(\" \", \"\").Replace(\"-\", \"\").Replace(\"_\", \"\");");

        Switch(sb, "public static string[] ClauseKeysFor(string discriminator)", "[]",
            entries, e => e.ClauseKeysMember ?? "[]");
        Switch(sb, "public static string[]? SourceKeysFor(string discriminator)", "null",
            entries, e => e.SourceKeysMember);
        Switch(sb, "public static global::System.Type ClauseTypeFor(string discriminator)",
            "throw new global::System.ArgumentException($\"Unknown discriminator: {discriminator}\")",
            entries, e => $"typeof({e.ClauseType})");
        Switch(sb, "public static global::System.Type? SourceConfigTypeFor(string discriminator)", "null",
            entries, e => e.SourceConfigType is { } t ? $"typeof({t})" : null);
        Switch(sb, "public static global::System.Type? ValueEnumTypeFor(string discriminator)", "null",
            entries, e => e.ValueEnum is { } t ? $"typeof({t})" : null);
        Switch(sb, "public static bool RollsAreInlineFor(string discriminator)", "false",
            entries, e => e.RollsAreInline ? "true" : null);
        Switch(sb, "public static int[]? RollsDefaultFor(string discriminator)", "null",
            entries, e => e.RollsDefault is { } rolls ? $"[{string.Join(", ", rolls.Items)}]" : null);

        sb.AppendLine();
        sb.AppendLine("    public static string[] Discriminators =>");
        sb.AppendLine("    [");
        foreach (var entry in entries)
            foreach (var wire in entry.Wires.Items)
                sb.AppendLine($"        \"{wire}\",");
        sb.AppendLine("    ];");

        sb.AppendLine();
        sb.AppendLine("    public static string[] NormalizedDiscriminators =>");
        sb.AppendLine("    [");
        var seen = new HashSet<string>();
        foreach (var entry in entries)
            foreach (var normalized in entry.Normalized.Items)
                if (seen.Add(normalized))
                    sb.AppendLine($"        \"{normalized}\",");
        sb.AppendLine("    ];");

        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>One switch-based lookup: entries whose <paramref name="arm"/> is null fall
    /// through to <paramref name="fallback"/>, so absent facts cost nothing per clause.</summary>
    private static void Switch(
        StringBuilder sb, string signature, string fallback,
        SchemaEntry[] entries, Func<SchemaEntry, string?> arm)
    {
        sb.AppendLine();
        sb.AppendLine($"    {signature} => N(discriminator) switch");
        sb.AppendLine("    {");
        foreach (var entry in entries)
        {
            if (arm(entry) is not { } result)
                continue;
            var patterns = string.Join(" or ", entry.Normalized.Items.Select(n => $"\"{n}\""));
            sb.AppendLine($"        {patterns} => {result},");
        }
        sb.AppendLine($"        _ => {fallback},");
        sb.AppendLine("    };");
    }
}

/// <summary>Everything the schema needs to know about one annotated clause type. A record so
/// the incremental pipeline caches by value; arrays ride in <see cref="EquatableArray{T}"/>
/// for the same reason.</summary>
internal sealed record SchemaEntry(
    EquatableArray<string> Wires,
    EquatableArray<string> Normalized,
    string ClauseType,
    string? ClauseKeysMember,
    string? SourceConfigType,
    string? SourceKeysMember,
    string? ValueEnum,
    bool RollsAreInline,
    EquatableArray<int>? RollsDefault);

/// <summary>An array with structural equality, so records holding one stay cacheable
/// across incremental runs.</summary>
internal readonly struct EquatableArray<T>(T[] items) : IEquatable<EquatableArray<T>>
    where T : IEquatable<T>
{
    public T[] Items { get; } = items;

    public bool Equals(EquatableArray<T> other) => Items.AsSpan().SequenceEqual(other.Items.AsSpan());
    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        var hash = 17;
        foreach (var item in Items)
            hash = unchecked(hash * 31 + (item?.GetHashCode() ?? 0));
        return hash;
    }
}
