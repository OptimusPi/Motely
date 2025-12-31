using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using Motely.Filters.MotelyJson;
using System.Linq;

namespace Motely.Filters.MotelyJson;

/// <summary>
/// Converts "joker: Blueprint" to "type: Joker, value: Blueprint"
/// </summary>
public class JamlTypeAsKeyNodeDeserializer : INodeDeserializer
{
    private static readonly Dictionary<string, string> TypeMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["joker"] = "Joker",
        ["souljoker"] = "SoulJoker", 
        ["voucher"] = "Voucher",
        ["tarot"] = "TarotCard",
        ["tarotcard"] = "TarotCard",
        ["planet"] = "PlanetCard",
        ["planetcard"] = "PlanetCard",
        ["spectral"] = "SpectralCard",
        ["spectralcard"] = "SpectralCard",
        ["playingcard"] = "PlayingCard",
        ["standardcard"] = "PlayingCard",
        ["boss"] = "Boss",
        ["tag"] = "Tag",
        ["smallblindtag"] = "SmallBlindTag",
        ["bigblindtag"] = "BigBlindTag",
        ["erraticrank"] = "ErraticRank",
        ["erraticsuit"] = "ErraticSuit",
        ["event"] = "Event",
        ["and"] = "And",
        ["or"] = "Or"
    };

    public bool Deserialize(IParser reader, Type expectedType, Func<IParser, Type, object?> objectFactory, out object? value, ObjectDeserializer rootDeserializer)
    {
        // Debug logging to see what types are being processed
        // Console.WriteLine($"JamlTypeAsKeyNodeDeserializer called for type: {expectedType.FullName}");
        
        // Check if this is a type we should handle before consuming any parser events
        var expectedTypeName = expectedType.FullName ?? "";
        var isMotleyJsonFilterClause = expectedTypeName.Contains("MotleyJsonFilterClause");
        var isMotelyJsonFilterClause = expectedType == typeof(MotelyJsonFilterClause);
        
        if (!isMotleyJsonFilterClause && !isMotelyJsonFilterClause)
        {
            // Console.WriteLine($"  Not a target type, returning false");
            value = null;
            return false;
        }
        
        // Get the current node
        if (!reader.TryConsume<MappingStart>(out var mappingStart))
        {
            value = null;
            return false;
        }

        var entries = new Dictionary<string, object>();
        var hasTypeAsKeyEntry = false;
        
        // Peek ahead to see if this mapping contains type-as-key entries
        var parserState = reader.Current as YamlDotNet.Core.Events.Scalar;
        while (!reader.TryConsume<MappingEnd>(out _))
        {
            if (!reader.TryConsume<Scalar>(out var keyScalar))
            {
                // Reset parser state and let other deserializers handle this
                value = null;
                return false;
            }

            var key = keyScalar.Value;
            
            if (TypeMappings.TryGetValue(key, out var mappedType))
            {
                hasTypeAsKeyEntry = true;
                // This is a type-as-key entry, convert it
                if (!reader.TryConsume<Scalar>(out var valueScalar))
                {
                    value = null;
                    return false;
                }

                entries["type"] = mappedType;
                entries["value"] = valueScalar.Value;
            }
            else
            {
                // Regular entry, just copy it
                var nodeValue = objectFactory(reader, typeof(object));
                entries[key] = nodeValue!;
            }
        }

        // If we didn't find any type-as-key entries, let other deserializers handle this
        if (!hasTypeAsKeyEntry)
        {
            // Console.WriteLine($"  No type-as-key entries found, returning false");
            value = null;
            return false;
        }

        // Console.WriteLine($"  Found type-as-key entries, processing for type: {expectedType.FullName}");
        
        if (isMotleyJsonFilterClause)
        {
            // Create a MotleyJsonFilterClause with the type and value properties set
            var clause = new MotelyJsonConfig.MotleyJsonFilterClause();
            
            // Set all properties from entries
            foreach (var entry in entries)
            {
                var property = clause.GetType().GetProperty(entry.Key, System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (property != null && property.CanWrite)
                {
                    if (property.PropertyType == typeof(string))
                    {
                        property.SetValue(clause, entry.Value?.ToString());
                    }
                    else if (property.PropertyType == typeof(string[]))
                    {
                        if (entry.Value is object[] array)
                        {
                            property.SetValue(clause, array.Select(x => x?.ToString()).ToArray());
                        }
                    }
                    else if (property.PropertyType == typeof(int[]))
                    {
                        if (entry.Value is object[] array)
                        {
                            property.SetValue(clause, array.Cast<int>().ToArray());
                        }
                    }
                    else if (property.PropertyType == typeof(int))
                    {
                        if (int.TryParse(entry.Value?.ToString(), out var intValue))
                        {
                            property.SetValue(clause, intValue);
                        }
                    }
                }
            }
            
            // Console.WriteLine($"  Created MotleyJsonFilterClause with type: {entries["type"]}, value: {entries["value"]}");
            value = clause;
        }
        else if (isMotelyJsonFilterClause)
        {
            // For the abstract MotelyJsonFilterClause, create concrete implementation
            var clause = CreateConcreteClause(entries["type"].ToString()!);
            
            // Set all properties from entries
            foreach (var entry in entries)
            {
                if (entry.Key.Equals("type", StringComparison.OrdinalIgnoreCase))
                    continue;
                    
                var property = clause.GetType().GetProperty(entry.Key, System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (property != null && property.CanWrite)
                {
                    if (property.PropertyType == typeof(string))
                    {
                        property.SetValue(clause, entry.Value?.ToString());
                    }
                    else if (property.PropertyType == typeof(int[]))
                    {
                        if (entry.Value is object[] array)
                        {
                            property.SetValue(clause, array.Cast<int>().ToArray());
                        }
                    }
                    else if (property.PropertyType == typeof(int))
                    {
                        if (int.TryParse(entry.Value?.ToString(), out var intValue))
                        {
                            property.SetValue(clause, intValue);
                        }
                    }
                }
                else if (entry.Key.Equals("value", StringComparison.OrdinalIgnoreCase))
                {
                    // Handle the special "value" field - map it to the appropriate property
                    MapValueToClauseProperty(clause, entry.Value);
                }
            }

            // Console.WriteLine($"  Created concrete {clause.GetType().Name} with type: {entries["type"]}, value: {entries["value"]}");
            value = clause;
        }
        else
        {
            // Console.WriteLine($"  Unknown type, returning false");
            value = null;
            return false;
        }
        
        return true;
    }

    private static MotelyJsonFilterClause CreateConcreteClause(string type)
    {
        return type.ToLowerInvariant() switch
        {
            "joker" => new MotelyJsonJokerFilterClause(),
            "souljoker" => new MotelyJsonSoulJokerFilterClause(),
            "voucher" => new MotelyJsonVoucherFilterClause(),
            "tarotcard" => new MotelyJsonTarotFilterClause(),
            "spectralcard" => new MotelyJsonSpectralFilterClause(),
            "planetcard" => new MotelyJsonPlanetFilterClause(),
            _ => new MotelyJsonJokerFilterClause() // Default fallback
        };
    }

    private static void MapValueToClauseProperty(MotelyJsonFilterClause clause, object? value)
    {
        if (value == null) return;
        
        var valueStr = value.ToString()!;
        
        // Map based on clause type
        switch (clause)
        {
            case MotelyJsonJokerFilterClause jokerClause:
                // Try to parse as enum value
                if (System.Enum.TryParse<MotelyJoker>(valueStr, true, out var jokerType))
                {
                    jokerClause.GetType().GetProperty("JokerType")?.SetValue(jokerClause, jokerType);
                }
                break;
                
            case MotelyJsonSoulJokerFilterClause soulJokerClause:
                if (System.Enum.TryParse<MotelyJoker>(valueStr, true, out var soulJokerType))
                {
                    soulJokerClause.GetType().GetProperty("JokerType")?.SetValue(soulJokerClause, soulJokerType);
                }
                break;
                
            case MotelyJsonVoucherFilterClause voucherClause:
                if (System.Enum.TryParse<MotelyVoucher>(valueStr, true, out var voucherType))
                {
                    voucherClause.GetType().GetProperty("VoucherType")?.SetValue(voucherClause, voucherType);
                }
                break;
                
            case MotelyJsonTarotFilterClause tarotClause:
                if (System.Enum.TryParse<MotelyTarotCard>(valueStr, true, out var tarotType))
                {
                    tarotClause.GetType().GetProperty("TarotType")?.SetValue(tarotClause, tarotType);
                }
                break;
                
            case MotelyJsonSpectralFilterClause spectralClause:
                if (System.Enum.TryParse<MotelySpectralCard>(valueStr, true, out var spectralType))
                {
                    spectralClause.GetType().GetProperty("SpectralType")?.SetValue(spectralClause, spectralType);
                }
                break;
                
            case MotelyJsonPlanetFilterClause planetClause:
                if (System.Enum.TryParse<MotelyPlanetCard>(valueStr, true, out var planetType))
                {
                    planetClause.GetType().GetProperty("PlanetType")?.SetValue(planetClause, planetType);
                }
                break;
        }
    }
}