using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using Motely.Filters.MotelyJson;
using System.Linq;
using System.Reflection;

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
        ["standardcard"] = "StandardCard",
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
        var isMotelyJsonConfigClause = expectedTypeName.Contains("MotleyJsonFilterClause") && expectedType != typeof(MotelyJsonConfig);
        var isMotelyJsonFilterClause = expectedType == typeof(MotelyJsonFilterClause);
        
        if (!isMotelyJsonConfigClause && !isMotelyJsonFilterClause)
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

        // Use case-insensitive dictionary for entries to handle any casing
        var entries = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        
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

                    DebugLogger.Log($"[CONVERTER] Type-as-key: {key} -> {mappedType}, value: {valueScalar.Value}");
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
                // Regular entry - check if it's the "clauses" property for And/Or clauses
                if (string.Equals(key, "clauses", StringComparison.OrdinalIgnoreCase))
                {
                    // Deserialize as List<MotelyJsonConfig.MotleyJsonFilterClause>
                    var clausesValue = objectFactory(reader, typeof(List<MotelyJsonConfig.MotleyJsonFilterClause>));
                    entries[key] = clausesValue!;
                }
                else
                {
                    // Regular entry, just copy it using objectFactory
                    var nodeValue = objectFactory(reader, typeof(object));
                    entries[key] = nodeValue!;
                }
            }
        }
        if (entries.TryGetValue("type", out var typeValue))
        {
            var typeStr = typeValue.ToString();

            if (typeStr.Equals("And", StringComparison.OrdinalIgnoreCase) || typeStr.Equals("Or", StringComparison.OrdinalIgnoreCase))
            {
                // Create a MotleyJsonFilterClause (the nested class) and set logical operator properties
                var clause = new MotelyJsonConfig.MotleyJsonFilterClause();
                
                // Set the type to and/or
                clause.Type = typeStr.ToLowerInvariant();
                
                // For and/or entries, nested clauses can be in 'value' (shorthand) or 'clauses' (standard format)
                var clausesProperty = clause.GetType().GetProperty("clauses", System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (clausesProperty != null && clausesProperty.CanWrite)
                {
                    // Check for standard format: clauses property
                    if (entries.TryGetValue("clauses", out var clausesValue))
                    {
                        if (clausesValue is List<MotelyJsonConfig.MotleyJsonFilterClause> clausesList)
                        {
                            clausesProperty.SetValue(clause, clausesList);
                        }
                        else if (clausesValue is System.Collections.IList list)
                        {
                            var convertedList = new List<MotelyJsonConfig.MotleyJsonFilterClause>();
                            foreach (var item in list)
                            {
                                if (item is MotelyJsonConfig.MotleyJsonFilterClause filterClause)
                                {
                                    convertedList.Add(filterClause);
                                }
                            }
                            clausesProperty.SetValue(clause, convertedList);
                        }
                    }
                    // Check for shorthand format: value property contains the clauses
                    else if (entries.TryGetValue("value", out var complexValue))
                    {
                        // Convert the List<object> to List<MotleyJsonFilterClause>
                        if (complexValue is System.Collections.IList list)
                        {
                            DebugLogger.Log($"  Converting list with {list.Count} items");
                            var convertedList = new List<MotelyJsonConfig.MotleyJsonFilterClause>();
                            foreach (var item in list)
                            {
                                DebugLogger.Log($"    Item type: {item?.GetType().Name}, value: {item}");
                                if (item is MotelyJsonConfig.MotleyJsonFilterClause filterClause)
                                {
                                    convertedList.Add(filterClause);
                                    DebugLogger.Log($"    Added filter clause with Type='{filterClause.Type}'");
                                }
                                else
                                {
                                    DebugLogger.Log($"    WARNING: Item is not a MotleyJsonFilterClause");
                                }
                            }
                            DebugLogger.Log($"  Final converted list has {convertedList.Count} items");
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
                        
                    var property = FindPropertyWithAlias(clause.GetType(), entry.Key);
                    if (property != null && property.CanWrite)
                    {
                        if (property.PropertyType == typeof(string))
                        {
                            property.SetValue(clause, entry.Value?.ToString());
                        }
                    else if (property.PropertyType == typeof(int[]))
                    {
                        int[]? intArray = null;
                        if (entry.Value is object[] array)
                        {
                            intArray = array.Cast<int>().ToArray();
                        }
                        else if (entry.Value is System.Collections.IList list)
                        {
                            intArray = list.Cast<object>().Select(o => Convert.ToInt32(o)).ToArray();
                        }
                        
                        if (intArray != null)
                        {
                            property.SetValue(clause, intArray);
                        }
                    }
                    else if (property.PropertyType == typeof(int))
                    {
                        if (int.TryParse(entry.Value?.ToString(), out var intValue))
                        {
                            property.SetValue(clause, intValue);
                        }
                    }
                    else if (property.PropertyType == typeof(int?))
                    {
                        // Handle nullable int
                        if (entry.Value == null)
                        {
                            property.SetValue(clause, null);
                        }
                        else if (int.TryParse(entry.Value.ToString(), out var intValue))
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
        if (isMotelyJsonConfigClause)
        {
            // Create a MotleyJsonFilterClause (the nested class in MotelyJsonConfig)
            var clause = new MotelyJsonConfig.MotleyJsonFilterClause();
            
            // Set all properties from entries
            foreach (var entry in entries)
            {
                // Try to find property by name (case-insensitive) or YamlMember alias
                var property = FindPropertyWithAlias(clause.GetType(), entry.Key);
                if (property != null && property.CanWrite)
                {
                    if (property.PropertyType == typeof(string))
                    {
                        property.SetValue(clause, entry.Value?.ToString());
                    }
                    else if (property.PropertyType == typeof(int[]))
                    {
                        int[]? intArray = null;
                        if (entry.Value is object[] array)
                        {
                            intArray = array.Cast<int>().ToArray();
                        }
                        else if (entry.Value is System.Collections.IList list)
                        {
                            intArray = list.Cast<object>().Select(o => Convert.ToInt32(o)).ToArray();
                        }
                        
                        if (intArray != null)
                        {
                            property.SetValue(clause, intArray);
                        }
                    }
                    else if (property.PropertyType == typeof(string[]))
                    {
                        string[]? stringArray = null;
                        if (entry.Value is object[] array)
                        {
                            stringArray = array.Select(o => o?.ToString() ?? "").ToArray();
                        }
                        else if (entry.Value is System.Collections.IList list)
                        {
                            stringArray = list.Cast<object>().Select(o => o?.ToString() ?? "").ToArray();
                        }
                        
                        if (stringArray != null)
                        {
                            property.SetValue(clause, stringArray);
                        }
                    }
                    else if (property.PropertyType == typeof(int))
                    {
                        if (int.TryParse(entry.Value?.ToString(), out var intValue))
                        {
                            property.SetValue(clause, intValue);
                        }
                    }
                    else if (property.PropertyType == typeof(int?))
                    {
                        // Handle nullable int
                        if (entry.Value == null)
                        {
                            property.SetValue(clause, null);
                        }
                        else if (int.TryParse(entry.Value.ToString(), out var intValue))
                        {
                            property.SetValue(clause, intValue);
                        }
                    }
                    else if (property.PropertyType == typeof(List<MotelyJsonConfig.MotleyJsonFilterClause>))
                    {
                        // Handle nested clauses for And/Or clauses
                        if (entry.Value is List<MotelyJsonConfig.MotleyJsonFilterClause> clausesList)
                        {
                            // Already the correct type, just set it
                            property.SetValue(clause, clausesList);
                        }
                        else if (entry.Value is System.Collections.IList list)
                        {
                            // Convert from generic list to typed list
                            var convertedList = new List<MotelyJsonConfig.MotleyJsonFilterClause>();
                            foreach (var item in list)
                            {
                                if (item is MotelyJsonConfig.MotleyJsonFilterClause filterClause)
                                {
                                    convertedList.Add(filterClause);
                                }
                            }
                            property.SetValue(clause, convertedList);
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
                    "event" => new MotelyJsonJokerFilterClause(),
                    _ => throw new ArgumentException($"Unknown type: {typeStr}")
                };
            }
            else
            {
                throw new ArgumentException($"Unknown type: {clauseTypeValue}");
            }
            
            foreach (var entry in entries)
            {
                var property = FindPropertyWithAlias(clause.GetType(), entry.Key);
                if (property != null && property.CanWrite)
                {
                    if (property.PropertyType == typeof(string))
                    {
                        property.SetValue(clause, entry.Value?.ToString());
                    }
                    else if (property.PropertyType == typeof(int[]))
                    {
                        int[]? intArray = null;
                        if (entry.Value is object[] array)
                        {
                            intArray = array.Cast<int>().ToArray();
                        }
                        else if (entry.Value is System.Collections.IList list)
                        {
                            intArray = list.Cast<object>().Select(o => Convert.ToInt32(o)).ToArray();
                        }
                        
                        if (intArray != null)
                        {
                            property.SetValue(clause, intArray);
                        }
                    }
                    else if (property.PropertyType == typeof(int))
                    {
                        if (int.TryParse(entry.Value?.ToString(), out var intValue))
                        {
                            property.SetValue(clause, intValue);
                        }
                    }
                    else if (property.PropertyType == typeof(int?))
                    {
                        // Handle nullable int
                        if (entry.Value == null)
                        {
                            property.SetValue(clause, null);
                        }
                        else if (int.TryParse(entry.Value.ToString(), out var intValue))
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
    
    /// <summary>
    /// Find a property by name (case-insensitive) or YamlMember alias
    /// </summary>
    private static System.Reflection.PropertyInfo? FindPropertyWithAlias(Type type, string name)
    {
        // Get all properties once
        var allProperties = type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        
        // First check property names case-insensitively
        foreach (var prop in allProperties)
        {
            if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                return prop;
        }
        
        // Then check all properties for YamlMember aliases (case-insensitive)
        foreach (var prop in allProperties)
        {
            var yamlMember = System.Attribute.GetCustomAttribute(prop, typeof(YamlMemberAttribute)) as YamlMemberAttribute;
            if (yamlMember != null && !string.IsNullOrEmpty(yamlMember.Alias))
            {
                if (string.Equals(yamlMember.Alias, name, StringComparison.OrdinalIgnoreCase))
                    return prop;
            }
        }
        
        return null;
    }
}