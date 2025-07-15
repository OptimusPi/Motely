using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Motely
{
    public static class MotelyEnumUtil
    {
        // Tolerant string-to-enum mapping for any enum type
        public static bool TryParseEnum<TEnum>(string input, out TEnum result) where TEnum : struct, Enum
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                result = default;
                return false;
            }
            // Remove spaces and underscores, PascalCase the result
            string normalized = Regex.Replace(input, "[ _]", "");
            normalized = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(normalized.ToLowerInvariant());
            foreach (TEnum value in Enum.GetValues(typeof(TEnum)))
            {
                if (string.Equals(EnumToCanonicalString(value), normalized, StringComparison.OrdinalIgnoreCase))
                {
                    result = value;
                    return true;
                }
            }
            result = default;
            return false;
        }

        // Always output PascalCase canonical name
        public static string EnumToCanonicalString<TEnum>(TEnum value) where TEnum : struct, Enum
        {
            return value.ToString();
        }

        // List all valid values for an enum
        public static IEnumerable<string> GetAllCanonicalNames<TEnum>() where TEnum : struct, Enum
        {
            return Enum.GetValues(typeof(TEnum)).Cast<TEnum>().Select(EnumToCanonicalString);
        }
    }
}
