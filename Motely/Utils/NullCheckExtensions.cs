using System.Collections.Generic;

namespace Motely.Utils
{
    /// <summary>
    /// Extension methods for consistent null checking patterns
    /// </summary>
    public static class NullCheckExtensions
    {
        /// <summary>
        /// Returns true if the collection is null or empty
        /// </summary>
        public static bool IsNullOrEmpty<T>(this ICollection<T>? collection) =>
            collection == null || collection.Count == 0;

        /// <summary>
        /// Returns true if the array is null or empty
        /// </summary>
        public static bool IsNullOrEmpty<T>(this T[]? array) =>
            array == null || array.Length == 0;

        /// <summary>
        /// Returns the count of the collection, or 0 if null
        /// </summary>
        public static int SafeCount<T>(this ICollection<T>? collection) =>
            collection?.Count ?? 0;

        /// <summary>
        /// Returns the length of the array, or 0 if null
        /// </summary>
        public static int SafeLength<T>(this T[]? array) =>
            array?.Length ?? 0;

        /// <summary>
        /// Returns true if the string is null or empty
        /// </summary>
        public static bool IsNullOrEmpty(this string? str) =>
            string.IsNullOrEmpty(str);

        /// <summary>
        /// Returns true if the string is null, empty, or whitespace
        /// </summary>
        public static bool IsNullOrWhiteSpace(this string? str) =>
            string.IsNullOrWhiteSpace(str);
    }
}
