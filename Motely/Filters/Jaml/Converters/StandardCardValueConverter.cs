using System;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Motely.Filters.Converters;

/// <summary>
/// AOT/trim-safe YAML converter for <see cref="StandardCardValue"/>. Discriminates between the
/// scalar shorthand form (e.g. <c>standardCard: KingOfHearts</c>) and the object form
/// (<c>standardCard: { rank: King, enhancement: Steel, seal: Red }</c>).
///
/// <para>The earlier implementation delegated the object form to
/// <c>rootDeserializer(typeof(StandardCardConfig))</c>, which under YamlDotNet's
/// source-generated static context fails for strict-typed enum properties
/// (<see cref="MotelyItemSeal"/>, <see cref="MotelyItemEnhancement"/>,
/// <see cref="MotelyItemEdition"/>). The recursive dispatch path didn't surface a
/// generated reader for those enums, throwing "Exception during deserialization" on
/// any object-form <c>standardCard</c> clause (e.g. <c>JamlFilters/sixtid.jaml</c>).</para>
///
/// <para>This rewrite walks events manually for the strict-typed enum properties,
/// matching the pattern already used by <see cref="EnumOrAnyConverter{T}"/>.
/// Strict types stay strict — the converter just stops asking source-gen to do the
/// thing source-gen can't reliably do behind a discriminated-union wrapper. Nested
/// <c>sources:</c> still goes through <c>rootDeserializer</c> because
/// <see cref="JamlSources"/> contains only primitive/array properties — no
/// strict-typed enums — so the recursive path is safe there.</para>
/// </summary>
public sealed class StandardCardValueConverter : IYamlTypeConverter
{
    public bool Accepts(Type type) =>
        type == typeof(StandardCardValue) || type == typeof(StandardCardValue?);

    public object? ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        if (parser.TryConsume<Scalar>(out var scalar))
            return new StandardCardValue { StringValue = scalar.Value };

        var dto = new StandardCardConfig();
        parser.Consume<MappingStart>();
        while (!parser.TryConsume<MappingEnd>(out _))
        {
            var key = parser.Consume<Scalar>().Value;
            switch (key)
            {
                case "rank":
                    dto.Rank = parser.Consume<Scalar>().Value;
                    break;
                case "suit":
                    dto.Suit = parser.Consume<Scalar>().Value;
                    break;
                case "seal":
                {
                    var v = parser.Consume<Scalar>().Value;
                    if (Enum.TryParse<MotelyItemSeal>(v, ignoreCase: true, out var parsed))
                        dto.Seal = parsed;
                    break;
                }
                case "enhancement":
                {
                    var v = parser.Consume<Scalar>().Value;
                    if (Enum.TryParse<MotelyItemEnhancement>(v, ignoreCase: true, out var parsed))
                        dto.Enhancement = parsed;
                    break;
                }
                case "edition":
                {
                    var v = parser.Consume<Scalar>().Value;
                    if (Enum.TryParse<MotelyItemEdition>(v, ignoreCase: true, out var parsed))
                        dto.Edition = parsed;
                    break;
                }
                case "sources":
                    dto.Sources = (JamlSources?)rootDeserializer(typeof(JamlSources));
                    break;
                default:
                    SkipNode(parser);
                    break;
            }
        }
        return new StandardCardValue { ObjectValue = dto };
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Skip the current value node (scalar, mapping, or sequence) including all nested events.
    /// Hand-rolled to avoid taking a dependency on a specific YamlDotNet extension API surface
    /// that has shifted between releases.
    /// </summary>
    private static void SkipNode(IParser parser)
    {
        var depth = 0;
        do
        {
            if (parser.Current is MappingStart or SequenceStart) depth++;
            else if (parser.Current is MappingEnd or SequenceEnd) depth--;
            parser.MoveNext();
        } while (depth > 0);
    }
}
