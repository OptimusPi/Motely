using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Motely.Generators;

[Generator(LanguageNames.CSharp)]
public sealed class JamlGrammarGenerator : IIncrementalGenerator
{
    private const string AttributeFullName = "Motely.Filters.Jaml.JamlDiscriminatorAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var entries = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax,
                transform: static (ctx, _) => ReadEntries(ctx))
            .SelectMany(static (list, _) => list);

        var collected = entries.Collect()
            .Select(static (entries, _) =>
                new EquatableArray<Entry>(entries.OrderBy(e => e.Normalized.First()).ToArray()));

        context.RegisterSourceOutput(collected, static (spc, entries) => Emit(spc, entries));
    }

    /// <summary>
    /// One clause type may carry several [JamlDiscriminator] attributes
    /// (different wires / rolls defaults). Each attribute becomes its own schema entry.
    /// </summary>
    private static ImmutableArray<Entry> ReadEntries(GeneratorSyntaxContext ctx)
    {
        if (ctx.Node is not ClassDeclarationSyntax classDecl)
            return ImmutableArray<Entry>.Empty;
        if (ctx.SemanticModel.GetDeclaredSymbol(classDecl) is not INamedTypeSymbol type)
            return ImmutableArray<Entry>.Empty;

        var attrs = type.GetAttributes()
            .Where(a => a.AttributeClass?.ToDisplayString() == AttributeFullName)
            .ToArray();
        if (attrs.Length == 0)
            return ImmutableArray<Entry>.Empty;

        var clauseTypeFull = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        // T2: keys live on the FilterDesc (IJamlClauseDesc), not mirrored on the clause type.
        var clauseKeysMember = FindClauseKeysForClause(type);
        var builder = ImmutableArray.CreateBuilder<Entry>(attrs.Length);

        foreach (var attr in attrs)
        {
            var wires = ExtractWires(attr);
            if (wires.Length == 0)
                continue;

            var normalized = wires.Select(Normalize).ToArray();

            var sourceConfigType = attr.NamedArguments
                .FirstOrDefault(a => a.Key == "SourceConfigType").Value;
            var sourceConfigFull = sourceConfigType.Kind == TypedConstantKind.Type
                ? ((INamedTypeSymbol?)sourceConfigType.Value)?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                : null;
            var sourceKeysMember = sourceConfigFull is not null
                ? FindSourceKeysMember(sourceConfigType)
                : null;

            var valueEnumType = attr.NamedArguments
                .FirstOrDefault(a => a.Key == "ValueEnum").Value;
            var valueEnumFull = valueEnumType.Kind == TypedConstantKind.Type
                ? ((INamedTypeSymbol?)valueEnumType.Value)?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                : null;

            var rollsInline = attr.NamedArguments
                .FirstOrDefault(a => a.Key == "RollsAreInlineValue").Value;
            bool rollsAreInlineValue = rollsInline.Kind == TypedConstantKind.Primitive
                && rollsInline.Value is true;

            var rollsDefault = attr.NamedArguments
                .FirstOrDefault(a => a.Key == "RollsDefault").Value;
            int[]? rollsDefaultArr = rollsDefault.Kind == TypedConstantKind.Array
                ? rollsDefault.Values.Select(v => (int)v.Value!).ToArray()
                : null;

            builder.Add(new Entry(
                wires, normalized, clauseTypeFull, clauseKeysMember,
                sourceConfigFull, sourceKeysMember,
                valueEnumFull, rollsAreInlineValue, rollsDefaultArr));
        }

        return builder.ToImmutable();
    }

    /// <summary>
    /// Resolve ClauseKeys from the FilterDesc that owns the grammar.
    /// Convention: <c>FooClause</c> → sibling <c>FooFilterDesc.ClauseKeys</c>
    /// (IJamlClauseDesc). And/Or stay on <c>LogicClause</c>.
    /// </summary>
    private static string? FindClauseKeysForClause(INamedTypeSymbol clauseType)
    {
        if (clauseType.Name is "AndClause" or "OrClause")
            return FindClauseKeysMember(clauseType);

        if (clauseType.Name.EndsWith("Clause", StringComparison.Ordinal)
            && clauseType.Name.Length > "Clause".Length)
        {
            var descName = clauseType.Name.Substring(0, clauseType.Name.Length - "Clause".Length) + "FilterDesc";
            foreach (var desc in clauseType.ContainingNamespace.GetTypeMembers(descName))
            {
                var keys = FindClauseKeysMember(desc);
                if (keys is not null)
                    return keys;
            }
        }

        // Fallback: keys declared on the clause/base (logic bags only after T2).
        return FindClauseKeysMember(clauseType);
    }

    private static string[] ExtractWires(AttributeData attr)
    {
        if (attr.ConstructorArguments.Length == 0)
            return [];
        var arg = attr.ConstructorArguments[0];
        if (arg.Kind == TypedConstantKind.Array)
            return arg.Values.Select(v => (string)v.Value!).ToArray();
        if (arg.Kind == TypedConstantKind.Primitive && arg.Value is string single)
            return [single];
        return [];
    }

    private static string? FindClauseKeysMember(INamedTypeSymbol type)
    {
        for (var t = type; t is not null; t = t.BaseType)
        {
            var field = t.GetMembers("ClauseKeys")
                .FirstOrDefault(m => m is IFieldSymbol f && f.IsStatic && f.Type is IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_String });
            if (field is not null)
                return t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + ".ClauseKeys";

            var prop = t.GetMembers("ClauseKeys")
                .FirstOrDefault(m => m is IPropertySymbol p && p.IsStatic && p.Type is IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_String });
            if (prop is not null)
                return t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + ".ClauseKeys";
        }
        return null;
    }

    private static string? FindSourceKeysMember(TypedConstant sourceConfigType)
    {
        if (sourceConfigType.Value is not INamedTypeSymbol type)
            return null;
        var field = type.GetMembers("SourceKeys")
            .FirstOrDefault(m => m is IFieldSymbol f && f.IsStatic && f.Type is IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_String });
        if (field is not null)
            return type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + ".SourceKeys";
        var prop = type.GetMembers("SourceKeys")
            .FirstOrDefault(m => m is IPropertySymbol p && p.IsStatic && p.Type is IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_String });
        if (prop is not null)
            return type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + ".SourceKeys";
        return null;
    }

    private static void Emit(SourceProductionContext spc, EquatableArray<Entry> entries)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/> Written by Motely.Generators from [JamlDiscriminator] attributes.");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("namespace Motely.Filters.Jaml;");
        sb.AppendLine();
        sb.AppendLine("public static partial class JamlSchema");
        sb.AppendLine("{");

        var all = entries.Items;
        if (all.Length == 0)
        {
            sb.AppendLine("    public static string[] ClauseKeysFor(string discriminator) => [];");
            sb.AppendLine("    public static string[]? SourceKeysFor(string discriminator) => null;");
            sb.AppendLine("    public static System.Type ClauseTypeFor(string discriminator) => throw new System.ArgumentException($\"Unknown discriminator: {discriminator}\");");
            sb.AppendLine("    public static System.Type? SourceConfigTypeFor(string discriminator) => null;");
            sb.AppendLine("    public static System.Type? ValueEnumTypeFor(string discriminator) => null;");
            sb.AppendLine("    public static bool RollsAreInlineFor(string discriminator) => false;");
            sb.AppendLine("    public static int[]? RollsDefaultFor(string discriminator) => null;");
            sb.AppendLine("    public static bool IsKnownDiscriminator(string discriminator) => false;");
            sb.AppendLine("    public static string[] Discriminators => [];");
            sb.AppendLine("    public static string[] NormalizedDiscriminators => [];");
            sb.AppendLine("}");
            spc.AddSource("JamlSchema.g.cs", sb.ToString());
            return;
        }

        sb.AppendLine("    private static string N(string d) =>");
        sb.AppendLine("        d.ToLowerInvariant().Replace(\" \", \"\").Replace(\"-\", \"\").Replace(\"_\", \"\");");
        sb.AppendLine();

        // ClauseKeysFor
        sb.AppendLine("    public static string[] ClauseKeysFor(string discriminator) => N(discriminator) switch");
        sb.AppendLine("    {");
        foreach (var e in all)
        {
            var patterns = string.Join(" or ", e.Normalized.Select(n => $"\"{n}\""));
            var member = e.ClauseKeysMember ?? "[]";
            sb.AppendLine($"        {patterns} => {member},");
        }
        sb.AppendLine("        _ => [],");
        sb.AppendLine("    };");
        sb.AppendLine();

        // SourceKeysFor
        sb.AppendLine("    public static string[]? SourceKeysFor(string discriminator) => N(discriminator) switch");
        sb.AppendLine("    {");
        foreach (var e in all)
        {
            if (e.SourceKeysMember is { } sk)
                foreach (var n in e.Normalized)
                    sb.AppendLine($"        \"{n}\" => {sk},");
        }
        sb.AppendLine("        _ => null,");
        sb.AppendLine("    };");
        sb.AppendLine();

        // ClauseTypeFor
        sb.AppendLine("    public static System.Type ClauseTypeFor(string discriminator) => N(discriminator) switch");
        sb.AppendLine("    {");
        foreach (var e in all)
            foreach (var n in e.Normalized)
                sb.AppendLine($"        \"{n}\" => typeof({e.ClauseTypeFull}),");
        sb.AppendLine("        _ => throw new System.ArgumentException($\"Unknown discriminator: {discriminator}\")");
        sb.AppendLine("    };");
        sb.AppendLine();

        // SourceConfigTypeFor
        sb.AppendLine("    public static System.Type? SourceConfigTypeFor(string discriminator) => N(discriminator) switch");
        sb.AppendLine("    {");
        foreach (var e in all.Where(e => e.SourceConfigFull is not null))
            foreach (var n in e.Normalized)
                sb.AppendLine($"        \"{n}\" => typeof({e.SourceConfigFull}),");
        sb.AppendLine("        _ => null,");
        sb.AppendLine("    };");
        sb.AppendLine();

        // ValueEnumTypeFor
        sb.AppendLine("    public static System.Type? ValueEnumTypeFor(string discriminator) => N(discriminator) switch");
        sb.AppendLine("    {");
        foreach (var e in all.Where(e => e.ValueEnumFull is not null))
            foreach (var n in e.Normalized)
                sb.AppendLine($"        \"{n}\" => typeof({e.ValueEnumFull}),");
        sb.AppendLine("        _ => null,");
        sb.AppendLine("    };");
        sb.AppendLine();

        // RollsAreInlineFor
        sb.AppendLine("    public static bool RollsAreInlineFor(string discriminator) => N(discriminator) switch");
        sb.AppendLine("    {");
        foreach (var e in all.Where(e => e.RollsAreInlineValue))
            foreach (var n in e.Normalized)
                sb.AppendLine($"        \"{n}\" => true,");
        sb.AppendLine("        _ => false,");
        sb.AppendLine("    };");
        sb.AppendLine();

        // RollsDefaultFor
        sb.AppendLine("    public static int[]? RollsDefaultFor(string discriminator) => N(discriminator) switch");
        sb.AppendLine("    {");
        foreach (var e in all.Where(e => e.RollsDefault is not null))
        {
            var arr = string.Join(", ", e.RollsDefault!);
            foreach (var n in e.Normalized)
                sb.AppendLine($"        \"{n}\" => [{arr}],");
        }
        sb.AppendLine("        _ => null,");
        sb.AppendLine("    };");
        sb.AppendLine();

        // IsKnownDiscriminator — supersedes hand JamlDiscriminatorRegistry.Entries.ContainsKey
        var allNormalized = new HashSet<string>();
        foreach (var e in all)
            foreach (var n in e.Normalized)
                allNormalized.Add(n);
        sb.AppendLine("    public static bool IsKnownDiscriminator(string discriminator) => N(discriminator) switch");
        sb.AppendLine("    {");
        if (allNormalized.Count > 0)
        {
            var patterns = string.Join(" or ", allNormalized.OrderBy(n => n, StringComparer.Ordinal).Select(n => $"\"{n}\""));
            sb.AppendLine($"        {patterns} => true,");
        }
        sb.AppendLine("        _ => false,");
        sb.AppendLine("    };");
        sb.AppendLine();

        // Discriminators array (all wire strings)
        sb.AppendLine("    public static string[] Discriminators =>");
        sb.AppendLine("    [");
        foreach (var e in all)
            foreach (var w in e.Wires)
                sb.AppendLine($"        \"{w}\",");
        sb.AppendLine("    ];");
        sb.AppendLine();

        // NormalizedDiscriminators (distinct)
        sb.AppendLine("    public static string[] NormalizedDiscriminators =>");
        sb.AppendLine("    [");
        foreach (var n in allNormalized.OrderBy(x => x, StringComparer.Ordinal))
            sb.AppendLine($"        \"{n}\",");
        sb.AppendLine("    ];");

        sb.AppendLine("}");

        spc.AddSource("JamlSchema.g.cs", sb.ToString());
    }

    private static string Normalize(string d) =>
        d.ToLowerInvariant().Replace(" ", "").Replace("-", "").Replace("_", "");

    private readonly struct Entry(
        string[] Wires,
        string[] Normalized,
        string ClauseTypeFull,
        string? ClauseKeysMember,
        string? SourceConfigFull,
        string? SourceKeysMember,
        string? ValueEnumFull,
        bool RollsAreInlineValue,
        int[]? RollsDefault
    ) : System.IEquatable<Entry>
    {
        public string[] Wires { get; } = Wires;
        public string[] Normalized { get; } = Normalized;
        public string ClauseTypeFull { get; } = ClauseTypeFull;
        public string? ClauseKeysMember { get; } = ClauseKeysMember;
        public string? SourceConfigFull { get; } = SourceConfigFull;
        public string? SourceKeysMember { get; } = SourceKeysMember;
        public string? ValueEnumFull { get; } = ValueEnumFull;
        public bool RollsAreInlineValue { get; } = RollsAreInlineValue;
        public int[]? RollsDefault { get; } = RollsDefault;

        public bool Equals(Entry other) =>
            System.Linq.Enumerable.SequenceEqual(Wires, other.Wires)
            && System.Linq.Enumerable.SequenceEqual(Normalized, other.Normalized)
            && ClauseTypeFull == other.ClauseTypeFull
            && ClauseKeysMember == other.ClauseKeysMember
            && SourceConfigFull == other.SourceConfigFull
            && SourceKeysMember == other.SourceKeysMember
            && ValueEnumFull == other.ValueEnumFull
            && RollsAreInlineValue == other.RollsAreInlineValue
            && (RollsDefault == other.RollsDefault || (RollsDefault is not null && other.RollsDefault is not null && System.Linq.Enumerable.SequenceEqual(RollsDefault, other.RollsDefault)));

        public override bool Equals(object? obj) => obj is Entry o && Equals(o);
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                foreach (var w in Wires) hash = hash * 31 + (w?.GetHashCode() ?? 0);
                foreach (var n in Normalized) hash = hash * 31 + (n?.GetHashCode() ?? 0);
                hash = hash * 31 + (ClauseTypeFull?.GetHashCode() ?? 0);
                hash = hash * 31 + (ClauseKeysMember?.GetHashCode() ?? 0);
                hash = hash * 31 + (SourceConfigFull?.GetHashCode() ?? 0);
                hash = hash * 31 + (SourceKeysMember?.GetHashCode() ?? 0);
                hash = hash * 31 + (ValueEnumFull?.GetHashCode() ?? 0);
                hash = hash * 31 + RollsAreInlineValue.GetHashCode();
                if (RollsDefault is not null)
                    foreach (var r in RollsDefault) hash = hash * 31 + r.GetHashCode();
                return hash;
            }
        }
    }
}

internal readonly struct EquatableArray<T>(T[] items) : System.IEquatable<EquatableArray<T>>
    where T : System.IEquatable<T>
{
    public T[] Items { get; } = items;

    public bool Equals(EquatableArray<T> other) => Items.AsSpan().SequenceEqual(other.Items.AsSpan());
    public override bool Equals(object? obj) => obj is EquatableArray<T> o && Equals(o);
    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            foreach (var item in Items)
                hash = hash * 31 + (item?.GetHashCode() ?? 0);
            return hash;
        }
    }
}
