using YamlDotNet.RepresentationModel;

namespace Motely.Filters.Jaml;

/// <summary>
/// Base POCO for every JAML <c>must</c>/<c>should</c>/<c>mustNot</c> entry. Holds the union of
/// fields any concrete clause might need (Antes/Min/Max/Score/Rolls/Label); concrete clauses
/// add their own typed properties (Bosses, Tarots, Sources, …) and implement <see cref="Describe"/>
/// + <see cref="CreateDesc"/>. No interface tree, no visitor — clauses ARE the data, polymorphism
/// is plain pattern matching in <see cref="JamlScoring"/>.
/// </summary>
public abstract class JamlClause
{
    public string? Label { get; init; }
    public int[] Antes { get; init; } = [];
    public int Min { get; init; } = 1;
    public int? Max { get; init; }
    public int Score { get; init; }
    public int[] Rolls { get; init; } = [];

    public int MaxAnte
    {
        get
        {
            int max = 0;
            for (int i = 0; i < Antes.Length; i++)
                if (Antes[i] > max) max = Antes[i];
            return max;
        }
    }

    public virtual int EstimatedCost => 10 + MaxAnte;
    public abstract string Describe();
    public abstract IMotelySeedFilterDesc CreateDesc();
}

/// <summary>YAML-mapping read helpers. Tight, allocation-aware, zero reflection.</summary>
public static class JamlYamlExtensions
{
    public static string? StringValue(this YamlMappingNode m, string key) =>
        m.Children.TryGetValue(new YamlScalarNode(key), out var v) && v is YamlScalarNode s ? s.Value : null;

    public static int? IntValue(this YamlMappingNode m, string key) =>
        m.StringValue(key) is { } s && int.TryParse(s, out var n) ? n : null;

    public static bool BoolValue(this YamlMappingNode m, string key, bool fallback = false) =>
        m.StringValue(key) is { } s && bool.TryParse(s, out var b) ? b : fallback;

    public static int[] IntArray(this YamlMappingNode m, string key)
    {
        if (!m.Children.TryGetValue(new YamlScalarNode(key), out var v) || v is not YamlSequenceNode seq)
            return [];
        var result = new int[seq.Children.Count];
        for (int i = 0; i < seq.Children.Count; i++)
            result[i] = seq.Children[i] is YamlScalarNode s && int.TryParse(s.Value, out var n) ? n : 0;
        return result;
    }

    public static T[] EnumArray<T>(this YamlMappingNode m, string singularKey, string pluralKey)
        where T : struct, Enum
    {
        if (m.Children.TryGetValue(new YamlScalarNode(pluralKey), out var pluralV) && pluralV is YamlSequenceNode seq)
        {
            var result = new T[seq.Children.Count];
            for (int i = 0; i < seq.Children.Count; i++)
                result[i] = seq.Children[i] is YamlScalarNode s && Enum.TryParse<T>(s.Value, ignoreCase: true, out var e) ? e : default;
            return result;
        }
        if (m.Children.TryGetValue(new YamlScalarNode(singularKey), out var singV) && singV is YamlScalarNode ss
            && Enum.TryParse<T>(ss.Value, ignoreCase: true, out var ee))
            return [ee];
        return [];
    }

    public static T? EnumValue<T>(this YamlMappingNode m, string key) where T : struct, Enum =>
        m.StringValue(key) is { } s && Enum.TryParse<T>(s, ignoreCase: true, out var e) ? e : null;
}
