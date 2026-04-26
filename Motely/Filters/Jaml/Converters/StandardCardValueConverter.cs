using System;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Motely.Filters.Converters;

public sealed class StandardCardValueConverter : IYamlTypeConverter
{
    public bool Accepts(Type type) => 
        type == typeof(StandardCardValue) || type == typeof(StandardCardValue?);

    public object? ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        try 
        {
            if (parser.TryConsume<Scalar>(out var scalar))
            {
                return new StandardCardValue { StringValue = scalar.Value };
            }

            var dto = (StandardCardConfigDto?)rootDeserializer(typeof(StandardCardConfigDto));
            return new StandardCardValue { ObjectValue = dto };
        }
        catch (Exception ex)
        {
            Console.WriteLine("CONVERTER EXCEPTION: " + ex);
            throw;
        }
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
    {
        throw new NotImplementedException();
    }
}
