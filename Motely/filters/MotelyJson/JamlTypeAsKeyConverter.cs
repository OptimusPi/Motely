using System.Linq;
using System.Reflection;
using Motely.Filters;
using Motely.Filters.MotelyJson;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Motely.Filters.MotelyJson
{
    /// <summary>
    /// Converts "joker: Blueprint" to "type: Joker, value: Blueprint"
    /// </summary>
    public class JamlTypeAsKeyNodeDeserializer : INodeDeserializer
    {
        private static readonly Dictionary<string, string> TypeMappings = new(
            StringComparer.OrdinalIgnoreCase
        )
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
            ["erraticcard"] = "ErraticCard",
            ["event"] = "Event",
            ["and"] = "And",
            ["or"] = "Or",
        };

        private static PropertyInfo? FindPropertyWithAlias(Type type, string alias)
        {
            return type.GetProperty(
                alias,
                BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance
            );
        }

        public bool Deserialize(
            IParser reader,
            Type expectedType,
            Func<IParser, Type, object?> objectFactory,
            out object? value,
            ObjectDeserializer rootDeserializer
        )
        {
            value = null;

            // Check if this is a type we should handle
            var expectedTypeName = expectedType.Name;
            var isMotelyJsonConfigClause = expectedTypeName.Contains("MotelyJsonFilterClause");
            var isMotelyJsonFilterClause = expectedType == typeof(MotelyJsonFilterClause) || 
                                          expectedType.IsSubclassOf(typeof(MotelyJsonFilterClause));

            if (!isMotelyJsonConfigClause && !isMotelyJsonFilterClause)
            {
                return false;
            }

            // Get the current node
            if (!reader.TryConsume<MappingStart>(out var mappingStart))
            {
                return false;
            }

            // Use case-insensitive dictionary for entries to handle any casing
            var entries = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            while (!reader.TryConsume<MappingEnd>(out _))
            {
                if (!reader.TryConsume<Scalar>(out var keyScalar))
                {
                    return false;
                }

                var key = keyScalar.Value;

                // Handle YAML Merge Key (<<) - just pass it through to let MergingParser handle it
                if (key == "<<")
                {
                    var mergedValue = objectFactory(reader, typeof(object));
                    // We don't need to do anything with it here, MergingParser has already swallowed the anchor events
                    // and presented the merged properties as new events.
                    // But if we are in a custom deserializer, we might need to be careful.
                    // Actually, MergingParser sits BEFORE the Deserializer, so we shouldn't even see "<<"
                    // unless something is wrong.
                    continue;
                }

                if (TypeMappings.TryGetValue(key, out var mappedType))
                {
                    var nextEvent = reader.Current;

                    if (nextEvent is Scalar)
                    {
                        if (!reader.TryConsume<Scalar>(out var valueScalar))
                        {
                            return false;
                        }

                        DebugLogger.Log(
                            $"[CONVERTER] Type-as-key: {key} -> {mappedType}, value: {valueScalar.Value}"
                        );
                        entries["type"] = mappedType;
                        entries["value"] = valueScalar.Value;
                    }
                    else if (nextEvent is MappingStart || nextEvent is SequenceStart)
                    {
                        if (mappedType == "And" || mappedType == "Or" && nextEvent is SequenceStart)
                        {
                            var complexValue = objectFactory(
                                reader,
                                typeof(List<MotelyJsonConfig.MotelyJsonFilterClause>)
                            );
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
                        return false;
                    }
                }
                else
                {
                    if (string.Equals(key, "clauses", StringComparison.OrdinalIgnoreCase))
                    {
                        var clausesValue = objectFactory(
                            reader,
                            typeof(List<MotelyJsonConfig.MotelyJsonFilterClause>)
                        );
                        entries[key] = clausesValue!;
                    }
                    else if (string.Equals(key, "sources", StringComparison.OrdinalIgnoreCase))
                    {
                        if (reader.Current is SequenceStart)
                        {
                            var sourcesList = objectFactory(
                                reader,
                                typeof(List<SourcesConfig>)
                            ) as List<SourcesConfig>;
                            if (sourcesList != null && sourcesList.Count > 0)
                            {
                                var merged = new SourcesConfig();
                                foreach (var s in sourcesList)
                                {
                                    MergeSources(merged, s);
                                }
                                entries[key] = merged;
                            }
                        }
                        else
                        {
                            var sourcesValue = objectFactory(reader, typeof(SourcesConfig));
                            entries[key] = sourcesValue!;
                        }
                    }
                    else
                    {
                        // Validate property existence
                        var prop = FindPropertyWithAlias(expectedType, key);
                        if (prop == null)
                        {
                            // Check if it's a known source property that should be in sources:
                            var sourceProp = FindPropertyWithAlias(typeof(SourcesConfig), key);
                            if (sourceProp != null)
                            {
                                throw new YamlException(keyScalar.Start, keyScalar.End, 
                                    $"Property '{key}' is not valid at this level. " +
                                    $"Did you mean to put it inside a 'sources:' block?");
                            }
                            
                            // Normal strict failure
                            throw new YamlException(keyScalar.Start, keyScalar.End, 
                                $"Unknown property '{key}' in filter clause.");
                        }

                        // Defer type coercion for properties that might use range syntax (int[])
                        Type targetType = (prop.PropertyType == typeof(int[])) ? typeof(object) : prop.PropertyType;
                        var nodeValue = objectFactory(reader, targetType);
                        entries[key] = nodeValue!;
                    }
                }
            }

            if (!entries.TryGetValue("type", out var typeValue) || typeValue == null)
            {
                // For non-clause types (like SourcesConfig), create and populate normally
                var obj = Activator.CreateInstance(expectedType);
                if (obj != null)
                {
                    foreach (var entry in entries)
                    {
                        var prop = FindPropertyWithAlias(expectedType, entry.Key);
                        if (prop != null && prop.CanWrite)
                        {
                            SetPropertyValue(prop, obj, entry.Value);
                        }
                    }
                    value = obj;
                    return true;
                }
                return false;
            }

            var typeStr = typeValue.ToString();

            // Handle And/Or logical operators
            if (
                !string.IsNullOrEmpty(typeStr)
                && (
                    typeStr.Equals("And", StringComparison.OrdinalIgnoreCase)
                    || typeStr.Equals("Or", StringComparison.OrdinalIgnoreCase)
                )
            )
            {
                var andOrClause = new MotelyJsonConfig.MotelyJsonFilterClause
                {
                    Type = typeStr.ToLowerInvariant(),
                };

                var clausesProperty = andOrClause
                    .GetType()
                    .GetProperty(
                        "clauses",
                        BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance
                    );
                if (clausesProperty != null && clausesProperty.CanWrite)
                {
                    if (entries.TryGetValue("clauses", out var clausesValue))
                    {
                        if (
                            clausesValue
                            is List<MotelyJsonConfig.MotelyJsonFilterClause> clausesList
                        )
                        {
                            clausesProperty.SetValue(andOrClause, clausesList);
                        }
                        else if (clausesValue is System.Collections.IList list)
                        {
                            var convertedList = new List<MotelyJsonConfig.MotelyJsonFilterClause>();
                            foreach (var item in list)
                            {
                                if (item is MotelyJsonConfig.MotelyJsonFilterClause filterClause)
                                {
                                    convertedList.Add(filterClause);
                                }
                            }
                            clausesProperty.SetValue(andOrClause, convertedList);
                        }
                    }
                    else if (entries.TryGetValue("value", out var complexValue))
                    {
                        if (complexValue is System.Collections.IList list)
                        {
                            DebugLogger.Log($"  Converting list with {list.Count} items");
                            var convertedList = new List<MotelyJsonConfig.MotelyJsonFilterClause>();
                            foreach (var item in list)
                            {
                                DebugLogger.Log(
                                    $"    Item type: {item?.GetType().Name}, value: {item}"
                                );
                                if (item is MotelyJsonConfig.MotelyJsonFilterClause filterClause)
                                {
                                    convertedList.Add(filterClause);
                                    DebugLogger.Log(
                                        $"    Added filter clause with Type='{filterClause.Type}'"
                                    );
                                }
                            }
                            DebugLogger.Log(
                                $"  Final converted list has {convertedList.Count} items"
                            );
                            clausesProperty.SetValue(andOrClause, convertedList);
                        }
                    }
                }

                // Set other properties
                foreach (var entry in entries)
                {
                    if (
                        entry.Key.Equals("type", StringComparison.OrdinalIgnoreCase)
                        || entry.Key.Equals("value", StringComparison.OrdinalIgnoreCase)
                        || entry.Key.Equals("clauses", StringComparison.OrdinalIgnoreCase)
                    )
                        continue;

                    var property = FindPropertyWithAlias(andOrClause.GetType(), entry.Key);
                    if (property != null && property.CanWrite)
                    {
                        SetPropertyValue(property, andOrClause, entry.Value);
                    }
                }

                value = andOrClause;
                return true;
            }

            // Create the appropriate filter clause from the processed entries
            if (isMotelyJsonConfigClause)
            {
                // Type is required - extract it first, default to empty string if missing (will fail validation later)
                var typeValue2 = entries.TryGetValue("type", out var tv)
                    ? tv?.ToString() ?? ""
                    : "";
                var configClause = new MotelyJsonConfig.MotelyJsonFilterClause
                {
                    Type = typeValue2,
                };

                foreach (var entry in entries)
                {
                    var property = FindPropertyWithAlias(configClause.GetType(), entry.Key);
                    if (property != null && property.CanWrite)
                    {
                        SetPropertyValue(property, configClause, entry.Value);
                    }
                }

                value = configClause;
                return true;
            }
            else
            {
                // Create the abstract MotelyJsonFilterClause concrete implementation
                MotelyJsonFilterClause filterClause;

                if (entries.TryGetValue("type", out var clauseTypeValue))
                {
                    var innerTypeStr = clauseTypeValue.ToString()?.ToLowerInvariant();
                    filterClause = innerTypeStr switch
                    {
                        "joker" => new MotelyJsonJokerFilterClause(),
                        "souljoker" => new MotelyJsonSoulJokerFilterClause(),
                        "voucher" => new MotelyJsonVoucherFilterClause(),
                        "tarotcard" => new MotelyJsonTarotFilterClause(),
                        "spectralcard" => new MotelyJsonSpectralFilterClause(),
                        "planetcard" => new MotelyJsonPlanetFilterClause(),
                        "event" => new MotelyJsonJokerFilterClause(),
                        _ => throw new ArgumentException($"Unknown type: {innerTypeStr}"),
                    };
                }
                else
                {
                    throw new ArgumentException($"Missing type in filter clause");
                }

                foreach (var entry in entries)
                {
                    var property = FindPropertyWithAlias(filterClause.GetType(), entry.Key);
                    if (property != null && property.CanWrite)
                    {
                        SetPropertyValue(property, filterClause, entry.Value);
                    }
                }

                value = filterClause;
                return true;
            }
        }

        private static void SetPropertyValue(
            PropertyInfo property,
            object target,
            object? entryValue
        )
        {
            if (property.PropertyType == typeof(string))
            {
                property.SetValue(target, entryValue?.ToString());
            }
            else if (property.PropertyType == typeof(int[]))
            {
                int[]? intArray = null;

                // Handle Range Syntax: "1..3" or ["1..3", 5]
                if (entryValue is string rangeStr && rangeStr.Contains(".."))
                {
                    intArray = ParseRange(rangeStr);
                }
                else if (entryValue is object[] array)
                {
                    var resultList = new List<int>();
                    foreach (var item in array)
                    {
                        var s = item?.ToString();
                        if (s != null && s.Contains(".."))
                        {
                            resultList.AddRange(ParseRange(s));
                        }
                        else
                        {
                            resultList.Add(Convert.ToInt32(item));
                        }
                    }
                    intArray = resultList.ToArray();
                }
                else if (entryValue is System.Collections.IList list)
                {
                    var resultList = new List<int>();
                    foreach (var item in list)
                    {
                        var s = item?.ToString();
                        if (s != null && s.Contains(".."))
                        {
                            resultList.AddRange(ParseRange(s));
                        }
                        else
                        {
                            resultList.Add(Convert.ToInt32(item));
                        }
                    }
                    intArray = resultList.ToArray();
                }

                if (intArray != null)
                {
                    property.SetValue(target, intArray);
                }
            }
            else if (property.PropertyType == typeof(string[]))
            {
                string[]? stringArray = null;
                if (entryValue is object[] array)
                {
                    // Zero-allocation: direct array allocation
                    stringArray = new string[array.Length];
                    for (int i = 0; i < array.Length; i++)
                        stringArray[i] = array[i]?.ToString() ?? "";
                }
                else if (entryValue is System.Collections.IList list)
                {
                    stringArray = new string[list.Count];
                    for (int i = 0; i < list.Count; i++)
                        stringArray[i] = list[i]?.ToString() ?? "";
                }

                if (stringArray != null)
                {
                    property.SetValue(target, stringArray);
                }
            }
            else if (property.PropertyType == typeof(int))
            {
                if (int.TryParse(entryValue?.ToString(), out var intValue))
                {
                    property.SetValue(target, intValue);
                }
            }
            else if (property.PropertyType == typeof(int?))
            {
                if (entryValue == null)
                {
                    property.SetValue(target, null);
                }
                else if (int.TryParse(entryValue.ToString(), out var intValue))
                {
                    property.SetValue(target, intValue);
                }
            }
            else if (property.PropertyType == typeof(List<MotelyJsonConfig.MotelyJsonFilterClause>))
            {
                if (entryValue is List<MotelyJsonConfig.MotelyJsonFilterClause> clausesList)
                {
                    property.SetValue(target, clausesList);
                }
                else if (entryValue is System.Collections.IList list)
                {
                    var convertedList = new List<MotelyJsonConfig.MotelyJsonFilterClause>();
                    foreach (var item in list)
                    {
                        if (item is MotelyJsonConfig.MotelyJsonFilterClause filterClause)
                        {
                            convertedList.Add(filterClause);
                        }
                    }
                    property.SetValue(target, convertedList);
                }
            }
            else if (property.PropertyType == typeof(SourcesConfig))
            {
                if (entryValue is SourcesConfig sourcesConfig)
                {
                    property.SetValue(target, sourcesConfig);
                }
            }
        }

        private static void MergeSources(SourcesConfig target, SourcesConfig source)
        {
            if (source.ShopSlots != null) target.ShopSlots = source.ShopSlots;
            if (source.PackSlots != null) target.PackSlots = source.PackSlots;
            if (source.MinShopSlot.HasValue) target.MinShopSlot = source.MinShopSlot;
            if (source.MaxShopSlot.HasValue) target.MaxShopSlot = source.MaxShopSlot;
            if (source.MinPackSlot.HasValue) target.MinPackSlot = source.MinPackSlot;
            if (source.MaxPackSlot.HasValue) target.MaxPackSlot = source.MaxPackSlot;
            if (source.Tags.HasValue) target.Tags = source.Tags;
            if (source.RequireMega.HasValue) target.RequireMega = source.RequireMega;
            if (source.Judgement != null) target.Judgement = source.Judgement;
            if (source.Wraith != null) target.Wraith = source.Wraith;
            if (source.RareTag != null) target.RareTag = source.RareTag;
            if (source.UncommonTag != null) target.UncommonTag = source.UncommonTag;
            if (source.RiffRaff != null) target.RiffRaff = source.RiffRaff;
            if (source.PurpleSealOrEightBall != null) target.PurpleSealOrEightBall = source.PurpleSealOrEightBall;
            if (source.Emperor != null) target.Emperor = source.Emperor;
            if (source.SixthSense != null) target.SixthSense = source.SixthSense;
            if (source.Seance != null) target.Seance = source.Seance;
            if (source.UncommonShopJokers != null) target.UncommonShopJokers = source.UncommonShopJokers;
            if (source.RareShopJokers != null) target.RareShopJokers = source.RareShopJokers;
            if (source.CommonShopJokers != null) target.CommonShopJokers = source.CommonShopJokers;
        }

        private static int[] ParseRange(string range)
        {
            var parts = range.Split("..");
            if (
                parts.Length == 2
                && int.TryParse(parts[0], out int start)
                && int.TryParse(parts[1], out int end)
            )
            {
                if (start <= end)
                {
                    var result = new int[end - start + 1];
                    for (int i = 0; i < result.Length; i++)
                        result[i] = start + i;
                    return result;
                }
                else
                {
                    var result = new int[start - end + 1];
                    for (int i = 0; i < result.Length; i++)
                        result[i] = start - i;
                    return result;
                }
            }
            return Array.Empty<int>();
        }
    }
}
