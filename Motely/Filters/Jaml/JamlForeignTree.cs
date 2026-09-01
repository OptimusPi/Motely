using System.Collections;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using VYaml.Serialization;

namespace Motely.Filters.Jaml;

/// <summary>
/// JSON / YAML → the existing <see cref="JMap"/> tree. One ParseConfig after that.
/// JAML terse lines stay on <see cref="JamlDocumentParser"/>.
/// </summary>
internal static class JamlForeignTree
{
    public static JMap ParseJson(string text)
    {
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(text);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"JSON parse error: {ex.Message}", ex);
        }

        return AsMap(FromJson(node), "JSON");
    }

    public static JMap ParseYaml(string text)
    {
        object? root;
        try
        {
            root = YamlSerializer.Deserialize<dynamic>(Encoding.UTF8.GetBytes(text));
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"YAML parse error: {ex.Message}", ex);
        }

        return AsMap(FromObject(root), "YAML");
    }

    private static JMap AsMap(JNode node, string kind) =>
        node as JMap ?? throw new InvalidOperationException($"{kind} root must be a mapping.");

    private static JNode FromJson(JsonNode? node) =>
        node switch
        {
            null => new JScalar(""),
            JsonObject obj => MapFrom(obj.Select(kv => (kv.Key, FromJson(kv.Value)))),
            JsonArray arr => SeqFrom(arr.Select(FromJson)),
            JsonValue value => FromJsonValue(value),
            _ => new JScalar(node.ToJsonString()),
        };

    private static JScalar FromJsonValue(JsonValue value) =>
        value.GetValueKind() switch
        {
            JsonValueKind.True => JScalar.Of(true),
            JsonValueKind.False => JScalar.Of(false),
            JsonValueKind.Number when value.TryGetValue<int>(out var i) => JScalar.Of(i),
            JsonValueKind.Number => new JScalar(value.ToJsonString(), JScalarKind.Bare),
            JsonValueKind.String => new JScalar(value.GetValue<string>() ?? "", JScalarKind.Quoted),
            _ => new JScalar(""),
        };

    private static JNode FromObject(object? value) =>
        value switch
        {
            null => new JScalar(""),
            JNode node => node,
            string s => new JScalar(s),
            bool b => JScalar.Of(b),
            IDictionary dict => MapFrom(Pairs(dict)),
            IEnumerable list and not string => SeqFrom(list.Cast<object?>().Select(FromObject)),
            IFormattable n => new JScalar(
                n.ToString(null, CultureInfo.InvariantCulture) ?? "",
                JScalarKind.Bare
            ),
            _ => new JScalar(value.ToString() ?? ""),
        };

    private static IEnumerable<(string Key, JNode Value)> Pairs(IDictionary dict)
    {
        foreach (var key in dict.Keys)
        {
            object lookup = key ?? "";
            yield return (key?.ToString() ?? "", FromObject(dict[lookup]));
        }
    }

    private static JMap MapFrom(IEnumerable<(string Key, JNode Value)> pairs)
    {
        var map = new JMap();
        foreach (var (key, val) in pairs)
            map.Set(key, val, default);
        return map;
    }

    private static JSeq SeqFrom(IEnumerable<JNode> items)
    {
        var seq = new JSeq();
        seq.Items.AddRange(items);
        return seq;
    }
}
