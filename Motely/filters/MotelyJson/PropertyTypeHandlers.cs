using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Motely.Filters.MotelyJson
{
    /// <summary>
    /// Handles property type conversion for dynamic property setting
    /// </summary>
    internal interface IPropertyTypeHandler
    {
        bool CanHandle(Type propertyType);
        void SetValue(PropertyInfo property, object target, object? value);
    }

    internal static class PropertyTypeHandlerRegistry
    {
        private static readonly Dictionary<Type, IPropertyTypeHandler> _handlers = new()
        {
            { typeof(string), new StringHandler() },
            { typeof(int[]), new IntArrayHandler() },
            { typeof(string[]), new StringArrayHandler() },
            { typeof(List<string>), new StringListHandler() },
            { typeof(int), new IntHandler() },
            { typeof(int?), new NullableIntHandler() },
            { typeof(List<MotelyJsonConfig.MotelyJsonFilterClause>), new ClausesListHandler() },
            { typeof(SourcesConfig), new SourcesConfigHandler() },
        };

        public static IPropertyTypeHandler? GetHandler(Type propertyType)
        {
            // Direct type match
            if (_handlers.TryGetValue(propertyType, out var handler))
                return handler;

            // Check for List<T> where T is string
            if (
                propertyType.IsGenericType
                && propertyType.GetGenericTypeDefinition() == typeof(List<>)
                && propertyType.GetGenericArguments()[0] == typeof(string)
            )
            {
                return _handlers[typeof(List<string>)];
            }

            return null;
        }
    }

    internal class StringHandler : IPropertyTypeHandler
    {
        public bool CanHandle(Type propertyType) => propertyType == typeof(string);

        public void SetValue(PropertyInfo property, object target, object? value) =>
            property.SetValue(target, value?.ToString());
    }

    internal class IntArrayHandler : IPropertyTypeHandler
    {
        public bool CanHandle(Type propertyType) => propertyType == typeof(int[]);

        public void SetValue(PropertyInfo property, object target, object? value)
        {
            int[]? intArray = null;
            if (value is object[] array)
                intArray = array.Cast<int>().ToArray();
            else if (value is System.Collections.IList list)
                intArray = list.Cast<object>().Select(o => Convert.ToInt32(o)).ToArray();
            if (intArray != null)
                property.SetValue(target, intArray);
        }
    }

    internal class StringArrayHandler : IPropertyTypeHandler
    {
        public bool CanHandle(Type propertyType) => propertyType == typeof(string[]);

        public void SetValue(PropertyInfo property, object target, object? value)
        {
            string[]? stringArray = null;
            if (value is object[] array)
                stringArray = array.Select(o => o?.ToString() ?? "").ToArray();
            else if (value is System.Collections.IList list)
                stringArray = list.Cast<object>().Select(o => o?.ToString() ?? "").ToArray();
            if (stringArray != null)
                property.SetValue(target, stringArray);
        }
    }

    internal class StringListHandler : IPropertyTypeHandler
    {
        public bool CanHandle(Type propertyType) =>
            propertyType == typeof(List<string>)
            || (
                propertyType.IsGenericType
                && propertyType.GetGenericTypeDefinition() == typeof(List<>)
                && propertyType.GetGenericArguments()[0] == typeof(string)
            );

        public void SetValue(PropertyInfo property, object target, object? value)
        {
            List<string>? stringList = null;
            if (value is object[] array)
                stringList = array.Select(o => o?.ToString() ?? "").ToList();
            else if (value is System.Collections.IList list)
                stringList = list.Cast<object>().Select(o => o?.ToString() ?? "").ToList();
            if (stringList != null)
                property.SetValue(target, stringList);
        }
    }

    internal class IntHandler : IPropertyTypeHandler
    {
        public bool CanHandle(Type propertyType) => propertyType == typeof(int);

        public void SetValue(PropertyInfo property, object target, object? value)
        {
            if (int.TryParse(value?.ToString(), out var intValue))
                property.SetValue(target, intValue);
        }
    }

    internal class NullableIntHandler : IPropertyTypeHandler
    {
        public bool CanHandle(Type propertyType) => propertyType == typeof(int?);

        public void SetValue(PropertyInfo property, object target, object? value)
        {
            if (value == null)
                property.SetValue(target, null);
            else if (int.TryParse(value.ToString(), out var intValue))
                property.SetValue(target, intValue);
        }
    }

    internal class ClausesListHandler : IPropertyTypeHandler
    {
        public bool CanHandle(Type propertyType) =>
            propertyType == typeof(List<MotelyJsonConfig.MotelyJsonFilterClause>);

        public void SetValue(PropertyInfo property, object target, object? value)
        {
            if (value is List<MotelyJsonConfig.MotelyJsonFilterClause> clausesList)
                property.SetValue(target, clausesList);
            else if (value is System.Collections.IList list)
            {
                var convertedList = new List<MotelyJsonConfig.MotelyJsonFilterClause>();
                foreach (var item in list)
                {
                    if (item is MotelyJsonConfig.MotelyJsonFilterClause filterClause)
                        convertedList.Add(filterClause);
                }
                property.SetValue(target, convertedList);
            }
        }
    }

    internal class SourcesConfigHandler : IPropertyTypeHandler
    {
        public bool CanHandle(Type propertyType) => propertyType == typeof(SourcesConfig);

        public void SetValue(PropertyInfo property, object target, object? value)
        {
            if (value is SourcesConfig sourcesConfig)
                property.SetValue(target, sourcesConfig);
        }
    }
}
