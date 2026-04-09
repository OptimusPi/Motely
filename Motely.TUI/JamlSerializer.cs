using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.EventEmitters;
using YamlDotNet.Serialization.NamingConventions;

namespace Motely.TUI;

/// <summary>
/// Builds the canonical YamlDotNet serializer for JAML output.
/// Primitive arrays (int[], string[], etc.) are emitted as inline flow sequences: [1,2,3]
/// Object/clause arrays remain block style.
/// </summary>
internal static class JamlSerializer
{
    public static ISerializer Build() =>
        new SerializerBuilder()
            .WithNamingConvention(NullNamingConvention.Instance)
            .DisableAliases()
            .ConfigureDefaultValuesHandling(
                DefaultValuesHandling.OmitNull
                    | DefaultValuesHandling.OmitEmptyCollections
                    | DefaultValuesHandling.OmitDefaults
            )
            .WithEventEmitter(next => new PrimitiveSequenceFlowEmitter(next))
            .Build();
}

/// <summary>
/// Switches sequences of primitive scalar types to YAML flow style: [1,2,3]
/// Leaves object/mapping sequences in the default block style.
/// </summary>
file sealed class PrimitiveSequenceFlowEmitter(IEventEmitter next) : ChainedEventEmitter(next)
{
    public override void Emit(SequenceStartEventInfo eventInfo, IEmitter emitter)
    {
        if (IsPrimitiveSequence(eventInfo.Source.Type))
            eventInfo.Style = SequenceStyle.Flow;
        nextEmitter.Emit(eventInfo, emitter);
    }

    private static bool IsPrimitiveSequence(Type type)
    {
        var elem = type.IsArray
            ? type.GetElementType()
            : type.IsGenericType ? type.GetGenericArguments().FirstOrDefault() : null;

        return elem is not null && (
            elem == typeof(int) || elem == typeof(long) ||
            elem == typeof(double) || elem == typeof(float) ||
            elem == typeof(bool) || elem == typeof(string));
    }
}
