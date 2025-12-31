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
        // Check if this is a type we should handle
        var expectedTypeName = expectedType.FullName ?? "";
        var isMotleyJsonFilterClause = expectedTypeName.Contains("MotleyJsonFilterClause") && expectedType != typeof(MotelyJsonConfig);
        var isMotelyJsonFilterClause = expectedType == typeof(MotelyJsonFilterClause);
        
        if (!isMotleyJsonFilterClause && !isMotelyJsonFilterClause)
        {
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
        
        while (!reader.TryConsume<MappingEnd>(out _))
        {
            if (!reader.TryConsume<Scalar>(out var keyScalar))
            {
                value = null;
                return false;
            }

            var key = keyScalar.Value;
            
            if (TypeMappings.TryGetValue(key, out var mappedType))
            {
                // Check what the next event is to handle both scalar and complex values
                var nextEvent = reader.Current;
                
                if (nextEvent is Scalar)
                {
                    // This is a type-as-key entry with a scalar value, convert it
                    if (!reader.TryConsume<Scalar>(out var valueScalar))
                    {
                        value = null;
                        return false;
                    }

                    entries["type"] = mappedType;
                    entries["value"] = valueScalar.Value;
                }
                else if (nextEvent is MappingStart || nextEvent is SequenceStart)
                {
                    // This is a type-as-key entry with a complex structure
                    // For and/or, we need to deserialize sequences of MotleyJsonFilterClause items
                    if (mappedType == "And" || mappedType == "Or" && nextEvent is SequenceStart)
                    {
                        // Use objectFactory to deserialize as a list of MotleyJsonFilterClause
                        var complexValue = objectFactory(reader, typeof(List<MotelyJsonConfig.MotleyJsonFilterClause>));
                        entries["type"] = mappedType;
                        entries["value"] = complexValue!;
                    }
                    else
                    {
                        // Use objectFactory to deserialize the complex value
                        var complexValue = objectFactory(reader, typeof(object));
                        entries["type"] = mappedType;
                        entries["value"] = complexValue!;
                    }
                }
                else
                {
                    value = null;
                    return false;
                }
            }
            else
            {
                // Regular entry, just copy it using objectFactory
                var nodeValue = objectFactory(reader, typeof(object));
                entries[key] = nodeValue!;
            }
        }

        // Check if this is an event type - handle it by creating a MotleyJsonFilterClause with event properties
        if (entries.TryGetValue("type", out var typeValue))
        {
            var typeStr = typeValue.ToString();
            
            if (typeStr == "Event")
            {
                // Create a MotleyJsonFilterClause (the nested class) and set event properties
                var clause = new MotelyJsonConfig.MotleyJsonFilterClause();
                
                // Set the type to event
                clause.Type = "event";
                
                // Parse the event value and set it as the value
                if (entries.TryGetValue("value", out var eventValue))
                {
                    clause.Value = eventValue.ToString();
                }
                
                // Set other properties from entries
                foreach (var entry in entries)
                {
                    if (entry.Key.Equals("type", StringComparison.OrdinalIgnoreCase) || 
                        entry.Key.Equals("value", StringComparison.OrdinalIgnoreCase))
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
                }
                
                value = clause;
                return true;
            }
            else if (typeStr == "And" || typeStr == "Or")
            {
                // Create a MotleyJsonFilterClause (the nested class) and set logical operator properties
                var clause = new MotelyJsonConfig.MotleyJsonFilterClause();
                
                // Set the type to and/or
                clause.Type = typeStr.ToLowerInvariant();
                
                // For and/or entries, the complex value should be stored in the 'clauses' property
                if (entries.TryGetValue("value", out var complexValue))
                {
                    var clausesProperty = clause.GetType().GetProperty("clauses", System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (clausesProperty != null && clausesProperty.CanWrite)
                    {
                        // Convert the List<object> to List<MotleyJsonFilterClause>
                        if (complexValue is System.Collections.IList list)
                        {
                            Console.WriteLine($"  Converting list with {list.Count} items");
                            var convertedList = new List<MotelyJsonConfig.MotleyJsonFilterClause>();
                            foreach (var item in list)
                            {
                                Console.WriteLine($"    Item type: {item?.GetType().Name}, value: {item}");
                                if (item is MotelyJsonConfig.MotleyJsonFilterClause filterClause)
                                {
                                    convertedList.Add(filterClause);
                                    Console.WriteLine($"    Added filter clause with Type='{filterClause.Type}'");
                                }
                                else
                                {
                                    Console.WriteLine($"    WARNING: Item is not a MotleyJsonFilterClause");
                                }
                            }
                            Console.WriteLine($"  Final converted list has {convertedList.Count} items");
                            clausesProperty.SetValue(clause, convertedList);
                        }
                    }
                }
                
                // Set other properties from entries
                foreach (var entry in entries)
                {
                    if (entry.Key.Equals("type", StringComparison.OrdinalIgnoreCase) || 
                        entry.Key.Equals("value", StringComparison.OrdinalIgnoreCase))
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
                }
                
                value = clause;
                return true;
            }
        }

        // Create the appropriate filter clause from the processed entries
        if (isMotleyJsonFilterClause)
        {
            // Create a MotleyJsonFilterClause (the nested class in MotelyJsonConfig)
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
            
            value = clause;
            return true;
        }
        else
        {
            // Create the abstract MotelyJsonFilterClause concrete implementation
            MotelyJsonFilterClause clause;
            
            // Determine which concrete type to create based on the mapped type
            if (entries.TryGetValue("type", out var clauseTypeValue))
            {
                var typeStr = clauseTypeValue.ToString()?.ToLowerInvariant();
                clause = typeStr switch
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
            else
            {
                // No type found, use default
                clause = new MotelyJsonJokerFilterClause();
            }
            
            foreach (var entry in entries)
            {
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
            }

            value = clause;
            return true;
        }
    }
}