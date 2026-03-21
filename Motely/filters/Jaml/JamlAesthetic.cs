namespace Motely.Filters;

/// <summary>
/// Seed-space constraints declared under top-level <c>aesthetics</c> in a JAML document.
/// Enumeration and classification: <see cref="JamlAesthetics"/>.
/// </summary>
public enum JamlAesthetic
{
    Palindrome,
}

public static class JamlAestheticParser
{
    /// <summary>
    /// Canonical JAML spellings (lowercase). Single source for <see cref="TryParse"/> and JSON schema (<c>definitions/JamlAesthetic</c>).
    /// </summary>
    private static readonly (string Jaml, JamlAesthetic Value)[] Known =
    [
        ("palindrome", JamlAesthetic.Palindrome),
    ];

    /// <summary>Strings for <c>enum</c> in the generated schema; order matches <see cref="Known"/>.</summary>
    public static string[] KnownJamlStringsForSchema() => [.. Known.Select(static e => e.Jaml)];

    /// <summary>Comma-separated list for load errors (e.g. “Known: palindrome, foo.”).</summary>
    public static string KnownJamlStringsDescription() => string.Join(", ", Known.Select(static e => e.Jaml));

    public static bool TryParse(string raw, out JamlAesthetic value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var key = raw.Trim().ToLowerInvariant();
        foreach (var (jaml, v) in Known)
        {
            if (key == jaml)
            {
                value = v;
                return true;
            }
        }

        return false;
    }
}
