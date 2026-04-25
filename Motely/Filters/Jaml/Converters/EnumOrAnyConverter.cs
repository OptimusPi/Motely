using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Motely.Filters.Converters;

/// <summary>
/// AOT-safe YAML scalar converter for <see cref="EnumOrAny{T}"/>. Reads the literal string
/// <c>any</c> (case-insensitive) as the wildcard sentinel, and any other scalar as a strict
/// case-insensitive enum parse via <see cref="Enum.Parse{TEnum}(string, bool)"/>.
/// Register one instance per closed enum type on the deserializer/serializer.
/// </summary>
public sealed class EnumOrAnyConverter<T> : IYamlTypeConverter where T : struct, Enum
{
    public bool Accepts(Type type) =>
        type == typeof(EnumOrAny<T>) || type == typeof(EnumOrAny<T>?);

    public object? ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        var scalar = parser.Consume<Scalar>();
        var value = scalar.Value;
        if (string.Equals(value, "any", StringComparison.OrdinalIgnoreCase))
            return EnumOrAny<T>.Any;
        return EnumOrAny<T>.Of(Enum.Parse<T>(value, ignoreCase: true));
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
    {
        if (value is null)
        {
            emitter.Emit(new Scalar(""));
            return;
        }
        var v = (EnumOrAny<T>)value;
        emitter.Emit(new Scalar(v.IsAny ? "any" : v.Value.ToString()));
    }
}
