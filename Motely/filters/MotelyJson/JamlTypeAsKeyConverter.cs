using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using Motely.Filters.MotelyJson;
using System.Linq;
using System.Reflection;

namespace Motely.Filters.MotelyJson
{
    /// <summary>
    /// Converts "joker: Blueprint" to "type: Joker, value: Blueprint"
    /// </summary>
    public class JamlTypeAsKeyNodeDeserializer : INodeDeserializer
    {
        private static readonly Dictionary<string, string> TypeMappings = new(StringComparer.OrdinalIgnoreCase)
        {
            ["joker"] = "Joker",
            ["jokers"] = "Joker",
            ["souljoker"] = "SoulJoker",
            ["souljokers"] = "SoulJoker",
            ["voucher"] = "Voucher",
            ["vouchers"] = "Voucher",
            ["tarot"] = "TarotCard",
            ["tarotcard"] = "TarotCard",
            ["tarotcards"] = "TarotCard",
            ["planet"] = "PlanetCard",
            ["planetcard"] = "PlanetCard",
            ["planetcards"] = "PlanetCard",
            ["spectral"] = "SpectralCard",
            ["spectralcard"] = "SpectralCard",
            ["spectralcards"] = "SpectralCard",
            ["standardcard"] = "StandardCard",
            ["standardcards"] = "StandardCard",
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
            var expectedTypeName = expectedType.FullName ?? "";
            var isMotelyJsonConfigClause = expectedTypeName.Contains("MotleyJsonFilterClause") && expectedType != typeof(MotelyJsonConfig);
            var isMotelyJsonFilterClause = expectedType == typeof(MotelyJsonFilterClause);
            
            if (!isMotelyJsonConfigClause && !isMotelyJsonFilterClause)
            {
                value = null;
                return false;
            }

            if (!reader.TryConsume<MappingStart>(out var mappingStart))
            {
                value = null;
                return false;
            }

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
                    var nextEvent = reader.Current;
                    
                    if (nextEvent is Scalar)
                    {
                        if (!reader.TryConsume<Scalar>(out var valueScalar))
                        {
                            value = null;
                            return false;
                        }
                        DebugLogger.Log($"[CONVERTER] Type-as-key: {key} -> {mappedType}, value: {valueScalar.Value}");
                        entries["type"] = mappedType;
                        entries["value"] = valueScalar.Value;
                    }
                    else if (nextEvent is SequenceStart)
                    {
                        var arrayValue = objectFactory(reader, typeof(object));
                        entries["type"] = mappedType;
                        entries["values"] = arrayValue!;
                    }
                    else if (nextEvent is MappingStart)
                    {
                        if (mappedType == "And" || mappedType == "Or")
                        {
                            var complexValue = objectFactory(reader, typeof(List<MotelyJsonConfig.MotleyJsonFilterClause>));
                            entries["type"] = mappedType;
                            entries["value"] = complexValue!;
                        }
                        else
                        {
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
                    if (string.Equals(key, "clauses", StringComparison.OrdinalIgnoreCase))
                    {
                        var clausesValue = objectFactory(reader, typeof(List<MotelyJsonConfig.MotleyJsonFilterClause>));
                        entries[key] = clausesValue!;
                    }
                    else
                    {
                        var nodeValue = objectFactory(reader, typeof(object));
                        entries[key] = nodeValue!;
                    }
                }
            }
            
            if (entries == null || !entries.TryGetValue("type", out var typeValue))
            {
                value = null;
                return false;
            }
            if (typeValue == null)
            {
                value = null;
                return false;
            }
            var typeStr = typeValue.ToString();

            if (isMotelyJsonConfigClause)
            {
                var clause = new MotelyJsonConfig.MotleyJsonFilterClause();
                
                // SET TYPE FIRST - this is critical!
                clause.Type = typeStr ?? "";
                
                if (typeStr != null && (typeStr.Equals("And", StringComparison.OrdinalIgnoreCase) || typeStr.Equals("Or", StringComparison.OrdinalIgnoreCase)))
                {
                    clause.Type = typeStr.ToLowerInvariant();
                    var clausesProperty = clause.GetType().GetProperty("clauses", System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (clausesProperty != null && clausesProperty.CanWrite)
                    {
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
                                        convertedList.Add(filterClause);
                                }
                                clausesProperty.SetValue(clause, convertedList);
                            }
                        }
                        else if (entries.TryGetValue("value", out var complexValue) && complexValue is System.Collections.IList list)
                        {
                            var convertedList = new List<MotelyJsonConfig.MotleyJsonFilterClause>();
                            foreach (var item in list)
                            {
                                if (item is MotelyJsonConfig.MotleyJsonFilterClause filterClause)
                                    convertedList.Add(filterClause);
                            }
                            clausesProperty.SetValue(clause, convertedList);
                        }
                    }
                }
                
                // Set value property if present
                if (entries.TryGetValue("value", out var valueEntry) && valueEntry != null)
                {
                    clause.Value = valueEntry.ToString();
                }
                if (entries.TryGetValue("values", out var valuesEntry) && valuesEntry != null)
                {
                    if (valuesEntry is string[] strArray)
                    {
                        clause.Values = strArray;
                    }
                    else if (valuesEntry is System.Collections.IList list)
                    {
                        clause.Values = list.Cast<object>().Select(o => o?.ToString() ?? "").ToArray();
                    }
                }
                
                foreach (var entry in entries)
                {
                    if (entry.Key.Equals("type", StringComparison.OrdinalIgnoreCase) || entry.Key.Equals("value", StringComparison.OrdinalIgnoreCase) || entry.Key.Equals("values", StringComparison.OrdinalIgnoreCase) || entry.Key.Equals("clauses", StringComparison.OrdinalIgnoreCase))
                        continue;
                    var property = FindPropertyWithAlias(clause.GetType(), entry.Key);
                    if (property != null && property.CanWrite)
                    {
                        if (property.PropertyType == typeof(string))
                            property.SetValue(clause, entry.Value?.ToString());
                        else if (property.PropertyType == typeof(int[]))
                        {
                            int[]? intArray = null;
                            if (entry.Value is object[] array)
                                intArray = array.Cast<int>().ToArray();
                            else if (entry.Value is System.Collections.IList list)
                                intArray = list.Cast<object>().Select(o => Convert.ToInt32(o)).ToArray();
                            if (intArray != null)
                                property.SetValue(clause, intArray);
                        }
                        else if (property.PropertyType == typeof(string[]))
                        {
                            string[]? stringArray = null;
                            if (entry.Value is object[] array)
                                stringArray = array.Select(o => o?.ToString() ?? "").ToArray();
                            else if (entry.Value is System.Collections.IList list)
                                stringArray = list.Cast<object>().Select(o => o?.ToString() ?? "").ToArray();
                            if (stringArray != null)
                                property.SetValue(clause, stringArray);
                        }
                        else if (property.PropertyType == typeof(int))
                        {
                            if (int.TryParse(entry.Value?.ToString(), out var intValue))
                                property.SetValue(clause, intValue);
                        }
                        else if (property.PropertyType == typeof(int?))
                        {
                            if (entry.Value == null)
                                property.SetValue(clause, null);
                            else if (int.TryParse(entry.Value.ToString(), out var intValue))
                                property.SetValue(clause, intValue);
                        }
                        else if (property.PropertyType == typeof(List<MotelyJsonConfig.MotleyJsonFilterClause>))
                        {
                            if (entry.Value is List<MotelyJsonConfig.MotleyJsonFilterClause> clausesList)
                                property.SetValue(clause, clausesList);
                            else if (entry.Value is System.Collections.IList list)
                            {
                                var convertedList = new List<MotelyJsonConfig.MotleyJsonFilterClause>();
                                foreach (var item in list)
                                {
                                    if (item is MotelyJsonConfig.MotleyJsonFilterClause filterClause)
                                        convertedList.Add(filterClause);
                                }
                                property.SetValue(clause, convertedList);
                            }
                        }
                    }
                }
                
                value = clause;
                return true;
            }
            
            value = null;
            return false;
        }
        
        private static System.Reflection.PropertyInfo? FindPropertyWithAlias(Type type, string name)
        {
            var allProperties = type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            foreach (var prop in allProperties)
            {
                if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                    return prop;
            }
            foreach (var prop in allProperties)
            {
                var yamlMember = System.Attribute.GetCustomAttribute(prop, typeof(YamlMemberAttribute)) as YamlMemberAttribute;
                if (yamlMember != null && !string.IsNullOrEmpty(yamlMember.Alias) && string.Equals(yamlMember.Alias, name, StringComparison.OrdinalIgnoreCase))
                    return prop;
            }
            return null;
        }
    }
}
