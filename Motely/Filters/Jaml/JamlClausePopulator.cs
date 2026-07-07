using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Motely.Filters.Jaml;

// The generic reflection-driven replacement for the ~30 hand-written Build*/Parse*Sources
// functions. Every discriminator's ClauseKeys/SourceKeys field (baked directly onto the
// clause/source-config type, the single source of truth also read by JamlDiscriminatorRegistry
// and Motely.Schema) drives which properties get populated and how — no per-discriminator
// hand-typed object initializer. Kept as a `partial` piece of JamlConfigLoader so it can reuse
// the loader's private IReader/NodeReader/ParseWith/ParseEnum/ParseRank/etc. machinery.
public static partial class JamlConfigLoader
{
    // Keys every populated clause handles itself before the generic per-property loop runs.
    private static readonly HashSet<string> PopulatorCommonKeys =
        new(StringComparer.OrdinalIgnoreCase) { "min", "max", "score", "label", "ante", "antes", "rolls" };

    private static IJamlClause Populate(
        string discriminator,
        JamlDiscriminatorEntry entry,
        NodeReader node,
        IReader data,
        int[] antes,
        int min,
        int? max,
        int score,
        string? label
    )
    {
        var clause = (IJamlClause)Activator.CreateInstance(entry.ClauseType)!;
        clause.Label = label;
        clause.Min = min;
        clause.Max = max;
        clause.Score = score;

        if (clause is IAnteScopedClause anteScoped)
            anteScoped.Antes = antes;

        if (clause is IRollScopedClause rollScoped)
        {
            rollScoped.Rolls = entry.RollsAreInlineValue
                ? node.GetIntArray(discriminator) ?? []
                : data.GetIntArray("rolls") ?? entry.RollsDefault ?? [];
        }

        var clauseKeys = JamlDiscriminatorRegistry.StaticStringArrayField(entry.ClauseType, "ClauseKeys");

        if (clauseKeys.Contains("with", StringComparer.OrdinalIgnoreCase))
        {
            var withProp =
                entry.ClauseType.GetProperty("With")
                ?? throw new InvalidOperationException(
                    $"'{entry.ClauseType.Name}' declares 'with' in ClauseKeys but has no With property."
                );
            withProp.SetValue(clause, ParseWith(data));
        }

        if (clauseKeys.Contains("sources", StringComparer.OrdinalIgnoreCase) && entry.SourceConfigType is { } sourceType)
        {
            var sourcesProp =
                entry.ClauseType.GetProperty("Sources")
                ?? throw new InvalidOperationException(
                    $"'{entry.ClauseType.Name}' declares 'sources' in ClauseKeys but has no Sources property."
                );
            sourcesProp.SetValue(clause, PopulateSourceConfig(sourceType, data));
        }

        var extraKeys = clauseKeys.Where(k =>
            !PopulatorCommonKeys.Contains(k)
            && !string.Equals(k, "with", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(k, "sources", StringComparison.OrdinalIgnoreCase)
        );

        foreach (var group in extraKeys.GroupBy(k => entry.ClauseType.GetProperty(ToPascalCase(ResolveWireKeyAlias(k)))))
        {
            if (group.Key is null)
                throw new InvalidOperationException(
                    $"Populator: '{entry.ClauseType.Name}' has no property matching key(s) '{string.Join("/", group)}'."
                );
            SetGenericProperty(clause, group.Key, data, group.ToArray());
        }

        return clause;
    }

    // Replaces ParseJokerSources/ParseLegendarySources/ParseTarotSources/ParseSpectralSources/
    // ParsePlanetSources/ParseStandardSources: one reflection-driven reader over a source-config
    // type's own SourceKeys, honoring the two real bool aliases (requireMega/requireMegaPack).
    private static object? PopulateSourceConfig(
        [DynamicallyAccessedMembers(JamlDiscriminatorRegistry.ClauseReflectionShape)] Type sourceType,
        IReader data
    )
    {
        var block = data.GetObject("sources");
        if (block is null)
            return null;

        var sourceKeys = JamlDiscriminatorRegistry.StaticStringArrayField(sourceType, "SourceKeys");
        ValidateKeys(block, sourceKeys, $"{sourceType.Name} source");

        var instance = Activator.CreateInstance(sourceType)!;
        foreach (var group in sourceKeys.GroupBy(k => sourceType.GetProperty(ToPascalCase(ResolveWireKeyAlias(k)))))
        {
            if (group.Key is null)
                throw new InvalidOperationException(
                    $"Populator: '{sourceType.Name}' has no property matching key(s) '{string.Join("/", group)}'."
                );
            SetGenericProperty(instance, group.Key, block, group.ToArray());
        }
        return instance;
    }

    // aliasKeys: every wire key that resolves to this one property (order matters — first
    // present value wins, matching the old `data.GetX("a") ?? data.GetX("b") ?? default` chains).
    private static void SetGenericProperty(object target, PropertyInfo prop, IReader data, IReadOnlyList<string> aliasKeys)
    {
        var type = prop.PropertyType;
        var underlying = Nullable.GetUnderlyingType(type);

        if (type == typeof(int))
        {
            int? val = null;
            foreach (var k in aliasKeys)
                val ??= data.GetInt(k);
            prop.SetValue(target, val ?? 0);
        }
        else if (type == typeof(bool))
        {
            bool? val = null;
            foreach (var k in aliasKeys)
                val ??= data.GetBool(k);
            prop.SetValue(target, val ?? false);
        }
        else if (type == typeof(int[]))
        {
            int[]? val = null;
            foreach (var k in aliasKeys)
                val ??= data.GetIntArray(k);
            prop.SetValue(target, val ?? []);
        }
        else if (type == typeof(string))
        {
            string? val = null;
            foreach (var k in aliasKeys)
                val ??= data.GetString(k);
            prop.SetValue(target, val);
        }
        else if (underlying == typeof(MotelyStandardcardRank))
        {
            string? val = null;
            foreach (var k in aliasKeys)
                val ??= data.GetString(k);
            prop.SetValue(target, val is null ? null : ParseRank(val));
        }
        else if (underlying is { IsEnum: true } underlyingEnum)
        {
            string? val = null;
            foreach (var k in aliasKeys)
                val ??= data.GetString(k);
            prop.SetValue(target, val is null ? null : ParseEnumDynamic(underlyingEnum, val));
        }
        else if (type.IsArray && type.GetElementType() is { IsEnum: true } elemType)
        {
            string[]? values = null;
            foreach (var k in aliasKeys)
                values ??= data.GetStringArray(k);
            values ??= [];
            var arr = Array.CreateInstance(elemType, values.Length);
            for (int i = 0; i < values.Length; i++)
                arr.SetValue(ParseEnumDynamic(elemType, values[i]), i);
            prop.SetValue(target, arr);
        }
        else
        {
            throw new InvalidOperationException(
                $"Populator: unsupported property type '{type}' on {prop.DeclaringType?.Name}.{prop.Name}."
            );
        }
    }

    private static object ParseEnumDynamic(Type enumType, string value)
    {
        if (Enum.TryParse(enumType, value, ignoreCase: true, out var parsed))
            return parsed!;

        var normalized = value
            .Replace(" ", "", StringComparison.Ordinal)
            .Replace("-", "", StringComparison.Ordinal)
            .Replace("_", "", StringComparison.Ordinal);
        if (Enum.TryParse(enumType, normalized, ignoreCase: true, out parsed))
            return parsed!;

        throw new InvalidOperationException(
            $"Cannot parse '{value}' as {enumType.Name}. Known values: {string.Join(", ", Enum.GetNames(enumType))}."
        );
    }

    // The deliberate aliases in the whole grammar — two wire keys binding to one property —
    // kept as small special cases rather than inventing an attribute for a couple of rules:
    // requireMega/requireMegaPack -> RequireMegaPack (LegendaryJokerSourceConfig,
    // SpectralCardSourceConfig); mult/value -> Mult (MisprintMultClause).
    private static string ResolveWireKeyAlias(string key) =>
        string.Equals(key, "requireMega", StringComparison.OrdinalIgnoreCase) ? "requireMegaPack"
        : string.Equals(key, "value", StringComparison.OrdinalIgnoreCase) ? "mult"
        : key;

    private static string ToPascalCase(string camelCaseKey) =>
        char.ToUpperInvariant(camelCaseKey[0]) + camelCaseKey[1..];

    // The five joker-family clause types (Joker/CommonJoker/UncommonJoker/RareJoker/LegendaryJoker)
    // share the same Jokers/IsWildcard property shape but different backing enum — that's the one
    // piece the plan calls out as staying with its family rather than going fully generic, since
    // it's already small and already correct.
    private static IJamlClause PopulateJokerFamily<TEnum>(
        string discriminator,
        NodeReader node,
        IReader data,
        int[] antes,
        int min,
        int? max,
        int score,
        string? label
    )
        where TEnum : struct, Enum
    {
        var entry = JamlDiscriminatorRegistry.Entries[discriminator];
        var clause = Populate(discriminator, entry, node, data, antes, min, max, score, label);
        var jokers = ParseJokerArray<TEnum>(node, discriminator, out var any);
        entry.ClauseType.GetProperty("Jokers")!.SetValue(clause, jokers);
        entry.ClauseType.GetProperty("IsWildcard")!.SetValue(clause, any);
        return clause;
    }

    private static TClause PopulateAndCast<TClause>(
        string discriminator,
        NodeReader node,
        IReader data,
        int[] antes,
        int min,
        int? max,
        int score,
        string? label
    )
        where TClause : IJamlClause =>
        (TClause)Populate(discriminator, JamlDiscriminatorRegistry.Entries[discriminator], node, data, antes, min, max, score, label);
}
