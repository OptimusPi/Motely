using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Motely.Filters.Jaml;
using Motely.Enums;

namespace Motely.Filters;

public sealed class JsonEnumOrAnyConverter<T> : JsonConverter<EnumOrAny<T>>
    where T : struct, Enum
{
    public override EnumOrAny<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var s = reader.GetString();
            if (string.Equals(s, "any", StringComparison.OrdinalIgnoreCase))
                return EnumOrAny<T>.Any;
            if (Enum.TryParse<T>(s, ignoreCase: true, out var val))
                return EnumOrAny<T>.Of(val);
        }
        else if (reader.TokenType == JsonTokenType.Number)
        {
            if (reader.TryGetInt32(out var i))
            {
                var val = (T)(object)i;
                return EnumOrAny<T>.Of(val);
            }
        }
        throw new JsonException($"Cannot parse value as EnumOrAny<{typeof(T).Name}>.");
    }

    public override void Write(Utf8JsonWriter writer, EnumOrAny<T> value, JsonSerializerOptions options)
    {
        if (value.IsAny)
            writer.WriteStringValue("any");
        else
            writer.WriteStringValue(value.Value.ToString());
    }
}

public sealed class JsonStandardCardValueConverter : JsonConverter<StandardCardValue>
{
    public override StandardCardValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
            return new StandardCardValue { StringValue = reader.GetString() };
        if (reader.TokenType == JsonTokenType.StartObject)
        {
            var config = JsonSerializer.Deserialize<StandardCardConfig>(ref reader, JamlJsonContext.Default.StandardCardConfig);
            return new StandardCardValue { ObjectValue = config };
        }
        throw new JsonException("standardCard must be a string or an object.");
    }

    public override void Write(Utf8JsonWriter writer, StandardCardValue value, JsonSerializerOptions options)
    {
        if (value.StringValue != null)
            writer.WriteStringValue(value.StringValue);
        else if (value.ObjectValue != null)
            JsonSerializer.Serialize(writer, value.ObjectValue, JamlJsonContext.Default.StandardCardConfig);
        else
            writer.WriteNullValue();
    }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    Converters = [
        typeof(JsonStandardCardValueConverter),
        typeof(JsonEnumOrAnyConverter<MotelyJoker>),
        typeof(JsonEnumOrAnyConverter<MotelyJokerCommon>),
        typeof(JsonEnumOrAnyConverter<MotelyJokerUncommon>),
        typeof(JsonEnumOrAnyConverter<MotelyJokerRare>),
        typeof(JsonEnumOrAnyConverter<MotelyJokerLegendary>)
    ]
)]
[JsonSerializable(typeof(JamlRootDocument))]
[JsonSerializable(typeof(JamlDefaults))]
[JsonSerializable(typeof(JamlClauseUnion))]
[JsonSerializable(typeof(JamlSources))]
[JsonSerializable(typeof(StandardCardValue))]
[JsonSerializable(typeof(StandardCardConfig))]
[JsonSerializable(typeof(EnumOrAny<MotelyJoker>))]
[JsonSerializable(typeof(EnumOrAny<MotelyJokerCommon>))]
[JsonSerializable(typeof(EnumOrAny<MotelyJokerUncommon>))]
[JsonSerializable(typeof(EnumOrAny<MotelyJokerRare>))]
[JsonSerializable(typeof(EnumOrAny<MotelyJokerLegendary>))]
public partial class JamlJsonContext : JsonSerializerContext { }
