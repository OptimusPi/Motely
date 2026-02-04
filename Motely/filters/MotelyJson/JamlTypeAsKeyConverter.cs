using System.Linq;
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

        #region AOT-Compatible Static Property Mapping

        /// <summary>
        /// Static property info for AOT compatibility - no reflection needed at runtime
        /// </summary>
        private record struct PropertyAccessor(Type PropertyType, Action<object, object?> Setter);

        /// <summary>
        /// Static property mappings for MotelyJsonFilterClause (case-insensitive keys)
        /// </summary>
        private static readonly Dictionary<string, PropertyAccessor> ClausePropertyMap = new(
            StringComparer.OrdinalIgnoreCase
        )
        {
            ["type"] = new(
                typeof(string),
                (obj, val) =>
                    ((MotelyJsonConfig.MotelyJsonFilterClause)obj).Type = val?.ToString() ?? ""
            ),
            ["value"] = new(
                typeof(string),
                (obj, val) => ((MotelyJsonConfig.MotelyJsonFilterClause)obj).Value = val?.ToString()
            ),
            ["values"] = new(
                typeof(string[]),
                (obj, val) =>
                    ((MotelyJsonConfig.MotelyJsonFilterClause)obj).Values = ConvertToStringArray(
                        val
                    )
            ),
            ["label"] = new(
                typeof(string),
                (obj, val) => ((MotelyJsonConfig.MotelyJsonFilterClause)obj).Label = val?.ToString()
            ),
            ["antes"] = new(
                typeof(int[]),
                (obj, val) =>
                    ((MotelyJsonConfig.MotelyJsonFilterClause)obj).Antes = ConvertToIntArray(val)
            ),
            ["clauses"] = new(
                typeof(List<MotelyJsonConfig.MotelyJsonFilterClause>),
                (obj, val) =>
                    ((MotelyJsonConfig.MotelyJsonFilterClause)obj).Clauses = ConvertToClauseList(
                        val
                    )
            ),
            ["score"] = new(
                typeof(int),
                (obj, val) =>
                    ((MotelyJsonConfig.MotelyJsonFilterClause)obj).Score = ConvertToInt(val, 1)
            ),
            ["mode"] = new(
                typeof(string),
                (obj, val) => ((MotelyJsonConfig.MotelyJsonFilterClause)obj).Mode = val?.ToString()
            ),
            ["function"] = new(
                typeof(string),
                (obj, val) =>
                    ((MotelyJsonConfig.MotelyJsonFilterClause)obj).Function = val?.ToString()
            ),
            ["cards"] = new(
                typeof(int[]),
                (obj, val) =>
                    ((MotelyJsonConfig.MotelyJsonFilterClause)obj).Cards = ConvertToIntArray(val)
            ),
            ["min"] = new(
                typeof(int?),
                (obj, val) =>
                    ((MotelyJsonConfig.MotelyJsonFilterClause)obj).Min = ConvertToNullableInt(val)
            ),
            ["filterOrder"] = new(
                typeof(int?),
                (obj, val) =>
                    ((MotelyJsonConfig.MotelyJsonFilterClause)obj).FilterOrder =
                        ConvertToNullableInt(val)
            ),
            ["edition"] = new(
                typeof(string),
                (obj, val) =>
                    ((MotelyJsonConfig.MotelyJsonFilterClause)obj).Edition = val?.ToString()
            ),
            ["stickers"] = new(
                typeof(string[]),
                (obj, val) =>
                    ((MotelyJsonConfig.MotelyJsonFilterClause)obj).Stickers = ConvertToStringArray(
                        val
                    )
            ),
            ["suit"] = new(
                typeof(string),
                (obj, val) => ((MotelyJsonConfig.MotelyJsonFilterClause)obj).Suit = val?.ToString()
            ),
            ["rank"] = new(
                typeof(string),
                (obj, val) => ((MotelyJsonConfig.MotelyJsonFilterClause)obj).Rank = val?.ToString()
            ),
            ["seal"] = new(
                typeof(string),
                (obj, val) => ((MotelyJsonConfig.MotelyJsonFilterClause)obj).Seal = val?.ToString()
            ),
            ["enhancement"] = new(
                typeof(string),
                (obj, val) =>
                    ((MotelyJsonConfig.MotelyJsonFilterClause)obj).Enhancement = val?.ToString()
            ),
            ["sources"] = new(
                typeof(SourcesConfig),
                (obj, val) =>
                    ((MotelyJsonConfig.MotelyJsonFilterClause)obj).Sources = val as SourcesConfig
            ),
            ["packSlots"] = new(
                typeof(int[]),
                (obj, val) =>
                    ((MotelyJsonConfig.MotelyJsonFilterClause)obj).PackSlots = ConvertToIntArray(
                        val
                    )
            ),
            ["shopSlots"] = new(
                typeof(int[]),
                (obj, val) =>
                    ((MotelyJsonConfig.MotelyJsonFilterClause)obj).ShopSlots = ConvertToIntArray(
                        val
                    )
            ),
            ["requireMega"] = new(
                typeof(bool?),
                (obj, val) =>
                    ((MotelyJsonConfig.MotelyJsonFilterClause)obj).RequireMega =
                        ConvertToNullableBool(val)
            ),
            ["tags"] = new(
                typeof(bool?),
                (obj, val) =>
                    ((MotelyJsonConfig.MotelyJsonFilterClause)obj).Tags = ConvertToNullableBool(val)
            ),
            ["eventType"] = new(
                typeof(string),
                (obj, val) =>
                    ((MotelyJsonConfig.MotelyJsonFilterClause)obj).EventType = val?.ToString()
            ),
            ["rolls"] = new(
                typeof(int[]),
                (obj, val) =>
                    ((MotelyJsonConfig.MotelyJsonFilterClause)obj).Rolls = ConvertToIntArray(val)
            ),
        };

        /// <summary>
        /// Static property mappings for SourcesConfig (case-insensitive keys)
        /// </summary>
        private static readonly Dictionary<string, PropertyAccessor> SourcesPropertyMap = new(
            StringComparer.OrdinalIgnoreCase
        )
        {
            ["shopSlots"] = new(
                typeof(int[]),
                (obj, val) => ((SourcesConfig)obj).ShopSlots = ConvertToIntArray(val)
            ),
            ["packSlots"] = new(
                typeof(int[]),
                (obj, val) => ((SourcesConfig)obj).PackSlots = ConvertToIntArray(val)
            ),
            ["minShopSlot"] = new(
                typeof(int?),
                (obj, val) => ((SourcesConfig)obj).MinShopSlot = ConvertToNullableInt(val)
            ),
            ["maxShopSlot"] = new(
                typeof(int?),
                (obj, val) => ((SourcesConfig)obj).MaxShopSlot = ConvertToNullableInt(val)
            ),
            ["minPackSlot"] = new(
                typeof(int?),
                (obj, val) => ((SourcesConfig)obj).MinPackSlot = ConvertToNullableInt(val)
            ),
            ["maxPackSlot"] = new(
                typeof(int?),
                (obj, val) => ((SourcesConfig)obj).MaxPackSlot = ConvertToNullableInt(val)
            ),
            ["tags"] = new(
                typeof(bool?),
                (obj, val) => ((SourcesConfig)obj).Tags = ConvertToNullableBool(val)
            ),
            ["requireMega"] = new(
                typeof(bool?),
                (obj, val) => ((SourcesConfig)obj).RequireMega = ConvertToNullableBool(val)
            ),
            ["judgement"] = new(
                typeof(int[]),
                (obj, val) => ((SourcesConfig)obj).Judgement = ConvertToIntArray(val)
            ),
            ["wraith"] = new(
                typeof(int[]),
                (obj, val) => ((SourcesConfig)obj).Wraith = ConvertToIntArray(val)
            ),
            ["rareTag"] = new(
                typeof(int[]),
                (obj, val) => ((SourcesConfig)obj).RareTag = ConvertToIntArray(val)
            ),
            ["uncommonTag"] = new(
                typeof(int[]),
                (obj, val) => ((SourcesConfig)obj).UncommonTag = ConvertToIntArray(val)
            ),
            ["riffRaff"] = new(
                typeof(int[]),
                (obj, val) => ((SourcesConfig)obj).RiffRaff = ConvertToIntArray(val)
            ),
            ["purpleSealOrEightBall"] = new(
                typeof(int[]),
                (obj, val) => ((SourcesConfig)obj).PurpleSealOrEightBall = ConvertToIntArray(val)
            ),
            ["emperor"] = new(
                typeof(int[]),
                (obj, val) => ((SourcesConfig)obj).Emperor = ConvertToIntArray(val)
            ),
            ["sixthSense"] = new(
                typeof(int[]),
                (obj, val) => ((SourcesConfig)obj).SixthSense = ConvertToIntArray(val)
            ),
            ["seance"] = new(
                typeof(int[]),
                (obj, val) => ((SourcesConfig)obj).Seance = ConvertToIntArray(val)
            ),
            ["uncommonShopJokers"] = new(
                typeof(int[]),
                (obj, val) => ((SourcesConfig)obj).UncommonShopJokers = ConvertToIntArray(val)
            ),
            ["rareShopJokers"] = new(
                typeof(int[]),
                (obj, val) => ((SourcesConfig)obj).RareShopJokers = ConvertToIntArray(val)
            ),
            ["commonShopJokers"] = new(
                typeof(int[]),
                (obj, val) => ((SourcesConfig)obj).CommonShopJokers = ConvertToIntArray(val)
            ),
        };

        /// <summary>
        /// Check if a property exists for a given type (AOT-safe replacement for FindPropertyWithAlias)
        /// </summary>
        private static bool TryGetPropertyAccessor(
            Type type,
            string propertyName,
            out PropertyAccessor accessor
        )
        {
            if (
                type == typeof(MotelyJsonConfig.MotelyJsonFilterClause)
                || type.Name.Contains("MotelyJsonFilterClause")
            )
            {
                return ClausePropertyMap.TryGetValue(propertyName, out accessor);
            }
            if (type == typeof(SourcesConfig))
            {
                return SourcesPropertyMap.TryGetValue(propertyName, out accessor);
            }
            accessor = default;
            return false;
        }

        /// <summary>
        /// Check if a property exists (for validation purposes)
        /// </summary>
        private static bool HasProperty(Type type, string propertyName)
        {
            return TryGetPropertyAccessor(type, propertyName, out _);
        }

        /// <summary>
        /// Get property type for a given type and property name
        /// </summary>
        private static Type? GetPropertyType(Type type, string propertyName)
        {
            if (TryGetPropertyAccessor(type, propertyName, out var accessor))
                return accessor.PropertyType;
            return null;
        }

        /// <summary>
        /// Set property value using static accessor (AOT-safe)
        /// </summary>
        private static void SetPropertyStatic(
            Type type,
            object target,
            string propertyName,
            object? value
        )
        {
            if (TryGetPropertyAccessor(type, propertyName, out var accessor))
            {
                accessor.Setter(target, value);
            }
        }

        #region Type Conversion Helpers

        private static int[]? ConvertToIntArray(object? value)
        {
            if (value == null)
                return null;
            if (value is int[] arr)
                return arr;

            // Handle range syntax and lists
            if (value is string rangeStr && rangeStr.Contains(".."))
                return ParseRange(rangeStr);

            if (value is object[] objArr)
            {
                var result = new List<int>();
                foreach (var item in objArr)
                {
                    var s = item?.ToString();
                    if (s != null && s.Contains(".."))
                        result.AddRange(ParseRange(s));
                    else if (int.TryParse(s, out var i))
                        result.Add(i);
                }
                return result.ToArray();
            }

            if (value is System.Collections.IList list)
            {
                var result = new List<int>();
                foreach (var item in list)
                {
                    var s = item?.ToString();
                    if (s != null && s.Contains(".."))
                        result.AddRange(ParseRange(s));
                    else if (int.TryParse(s, out var i))
                        result.Add(i);
                }
                return result.ToArray();
            }

            return null;
        }

        private static string[]? ConvertToStringArray(object? value)
        {
            if (value == null)
                return null;
            if (value is string[] arr)
                return arr;

            if (value is object[] objArr)
            {
                var result = new string[objArr.Length];
                for (int i = 0; i < objArr.Length; i++)
                    result[i] = objArr[i]?.ToString() ?? "";
                return result;
            }

            if (value is System.Collections.IList list)
            {
                var result = new string[list.Count];
                for (int i = 0; i < list.Count; i++)
                    result[i] = list[i]?.ToString() ?? "";
                return result;
            }

            return null;
        }

        private static List<string>? ConvertToStringList(object? value)
        {
            var arr = ConvertToStringArray(value);
            return arr?.ToList();
        }

        private static List<MotelyJsonConfig.MotelyJsonFilterClause>? ConvertToClauseList(
            object? value
        )
        {
            if (value == null)
                return null;
            if (value is List<MotelyJsonConfig.MotelyJsonFilterClause> list)
                return list;

            if (value is System.Collections.IList ilist)
            {
                var result = new List<MotelyJsonConfig.MotelyJsonFilterClause>();
                foreach (var item in ilist)
                {
                    if (item is MotelyJsonConfig.MotelyJsonFilterClause clause)
                        result.Add(clause);
                }
                return result;
            }

            return null;
        }

        private static int ConvertToInt(object? value, int defaultValue = 0)
        {
            if (value == null)
                return defaultValue;
            if (value is int i)
                return i;
            if (int.TryParse(value.ToString(), out var parsed))
                return parsed;
            return defaultValue;
        }

        private static int? ConvertToNullableInt(object? value)
        {
            if (value == null)
                return null;
            if (value is int i)
                return i;
            if (int.TryParse(value.ToString(), out var parsed))
                return parsed;
            return null;
        }

        private static bool? ConvertToNullableBool(object? value)
        {
            if (value == null)
                return null;
            if (value is bool b)
                return b;
            if (bool.TryParse(value.ToString(), out var parsed))
                return parsed;
            return null;
        }

        #endregion

        #endregion

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
            var isMotelyJsonFilterClause =
                expectedType == typeof(MotelyJsonFilterClause)
                || expectedType.IsSubclassOf(typeof(MotelyJsonFilterClause));

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
                    // Use MotelyJsonConfig instead of object for AOT compatibility
                    var mergedValue = objectFactory(
                        reader,
                        typeof(MotelyJsonConfig.MotelyJsonFilterClause)
                    );
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
                            // AOT-compatible: Deserialize to array instead of List<T>
                            var complexValue = objectFactory(
                                reader,
                                typeof(MotelyJsonConfig.MotelyJsonFilterClause[])
                            );
                            // Convert array to List for compatibility
                            if (
                                complexValue is MotelyJsonConfig.MotelyJsonFilterClause[] arrayValue
                            )
                            {
                                entries["type"] = mappedType;
                                entries["value"] =
                                    new List<MotelyJsonConfig.MotelyJsonFilterClause>(arrayValue);
                            }
                            else
                            {
                                entries["type"] = mappedType;
                                entries["value"] = complexValue!;
                            }
                        }
                        else
                        {
                            // Use MotelyJsonConfig.MotelyJsonFilterClause instead of object for AOT compatibility
                            var complexValue = objectFactory(
                                reader,
                                typeof(MotelyJsonConfig.MotelyJsonFilterClause)
                            );
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
                        // AOT-compatible: Deserialize to array instead of List<T>
                        var clausesValue = objectFactory(
                            reader,
                            typeof(MotelyJsonConfig.MotelyJsonFilterClause[])
                        );
                        // Convert array to List for compatibility
                        if (clausesValue is MotelyJsonConfig.MotelyJsonFilterClause[] arrayValue)
                        {
                            clausesValue = new List<MotelyJsonConfig.MotelyJsonFilterClause>(
                                arrayValue
                            );
                        }
                        entries[key] = clausesValue!;
                    }
                    else if (string.Equals(key, "sources", StringComparison.OrdinalIgnoreCase))
                    {
                        if (reader.Current is SequenceStart)
                        {
                            var sourcesList =
                                objectFactory(reader, typeof(List<SourcesConfig>))
                                as List<SourcesConfig>;
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
                        // Validate property existence (AOT-safe)
                        if (!HasProperty(expectedType, key))
                        {
                            // Check if it's a known source property that should be in sources:
                            if (HasProperty(typeof(SourcesConfig), key))
                            {
                                throw new YamlException(
                                    keyScalar.Start,
                                    keyScalar.End,
                                    $"Property '{key}' is not valid at this level. "
                                        + $"Did you mean to put it inside a 'sources:' block?"
                                );
                            }

                            // Normal strict failure
                            throw new YamlException(
                                keyScalar.Start,
                                keyScalar.End,
                                $"Unknown property '{key}' in filter clause."
                            );
                        }

                        // Defer type coercion for properties that might use range syntax (int[])
                        var propType = GetPropertyType(expectedType, key);
                        // Use specific types instead of object for AOT compatibility
                        // For int[] properties, use int[] directly - the static generator can handle arrays if element type is registered
                        Type targetType =
                            propType ?? typeof(MotelyJsonConfig.MotelyJsonFilterClause);
                        // int[] is already the target type, no conversion needed
                        // The static generator can handle int[] because int is a primitive type
                        var nodeValue = objectFactory(reader, targetType);
                        entries[key] = nodeValue!;
                    }
                }
            }

            if (!entries.TryGetValue("type", out var typeValue) || typeValue == null)
            {
                // For non-clause types (like SourcesConfig), create and populate normally (AOT-safe)
                object? obj =
                    expectedType == typeof(SourcesConfig) ? new SourcesConfig()
                    : expectedType == typeof(MotelyJsonConfig.MotelyJsonFilterClause)
                        ? new MotelyJsonConfig.MotelyJsonFilterClause { Type = "" }
                    : null;
                if (obj != null)
                {
                    foreach (var entry in entries)
                    {
                        if (HasProperty(expectedType, entry.Key))
                        {
                            SetPropertyStatic(expectedType, obj, entry.Key, entry.Value);
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

                // Set clauses directly (AOT-safe - no reflection)
                if (entries.TryGetValue("clauses", out var clausesValue))
                {
                    andOrClause.Clauses = ConvertToClauseList(clausesValue);
                }
                else if (entries.TryGetValue("value", out var complexValue))
                {
                    andOrClause.Clauses = ConvertToClauseList(complexValue);
                }

                // Set other properties (AOT-safe)
                foreach (var entry in entries)
                {
                    if (
                        entry.Key.Equals("type", StringComparison.OrdinalIgnoreCase)
                        || entry.Key.Equals("value", StringComparison.OrdinalIgnoreCase)
                        || entry.Key.Equals("clauses", StringComparison.OrdinalIgnoreCase)
                    )
                        continue;

                    if (HasProperty(typeof(MotelyJsonConfig.MotelyJsonFilterClause), entry.Key))
                    {
                        SetPropertyStatic(
                            typeof(MotelyJsonConfig.MotelyJsonFilterClause),
                            andOrClause,
                            entry.Key,
                            entry.Value
                        );
                    }
                }

                value = andOrClause;
                return true;
            }

            // Create filter clause and set all properties (AOT-safe)
            var typeValue2 = entries.TryGetValue("type", out var tv) ? tv?.ToString() ?? "" : "";
            var configClause = new MotelyJsonConfig.MotelyJsonFilterClause { Type = typeValue2 };

            foreach (var entry in entries)
            {
                if (HasProperty(typeof(MotelyJsonConfig.MotelyJsonFilterClause), entry.Key))
                {
                    SetPropertyStatic(
                        typeof(MotelyJsonConfig.MotelyJsonFilterClause),
                        configClause,
                        entry.Key,
                        entry.Value
                    );
                }
            }

            value = configClause;
            return true;
        }

        private static void MergeSources(SourcesConfig target, SourcesConfig source)
        {
            if (source.ShopSlots != null)
                target.ShopSlots = source.ShopSlots;
            if (source.PackSlots != null)
                target.PackSlots = source.PackSlots;
            if (source.MinShopSlot.HasValue)
                target.MinShopSlot = source.MinShopSlot;
            if (source.MaxShopSlot.HasValue)
                target.MaxShopSlot = source.MaxShopSlot;
            if (source.MinPackSlot.HasValue)
                target.MinPackSlot = source.MinPackSlot;
            if (source.MaxPackSlot.HasValue)
                target.MaxPackSlot = source.MaxPackSlot;
            if (source.Tags.HasValue)
                target.Tags = source.Tags;
            if (source.RequireMega.HasValue)
                target.RequireMega = source.RequireMega;
            if (source.Judgement != null)
                target.Judgement = source.Judgement;
            if (source.Wraith != null)
                target.Wraith = source.Wraith;
            if (source.RareTag != null)
                target.RareTag = source.RareTag;
            if (source.UncommonTag != null)
                target.UncommonTag = source.UncommonTag;
            if (source.RiffRaff != null)
                target.RiffRaff = source.RiffRaff;
            if (source.PurpleSealOrEightBall != null)
                target.PurpleSealOrEightBall = source.PurpleSealOrEightBall;
            if (source.Emperor != null)
                target.Emperor = source.Emperor;
            if (source.SixthSense != null)
                target.SixthSense = source.SixthSense;
            if (source.Seance != null)
                target.Seance = source.Seance;
            if (source.UncommonShopJokers != null)
                target.UncommonShopJokers = source.UncommonShopJokers;
            if (source.RareShopJokers != null)
                target.RareShopJokers = source.RareShopJokers;
            if (source.CommonShopJokers != null)
                target.CommonShopJokers = source.CommonShopJokers;
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
