namespace Motely.Filters;

/// <summary>
/// JAML files are YAML. This helper normalizes Motely-specific sugar (e.g. nested <c>and</c>/<c>or</c> + <c>clauses</c>)
/// and returns canonical YAML text — useful for tools that only accept a <c>.yaml</c> path or for diffing.
/// </summary>
public static class JamlToYamlConverter
{
    /// <summary>
    /// Applies the same normalizations as the JAML loader and returns canonical YAML (UTF-16 string).
    /// </summary>
    public static string Convert(string jaml)
    {
        if (string.IsNullOrWhiteSpace(jaml))
            throw new ArgumentException("JAML content is required.", nameof(jaml));

        return JamlConfigLoader.NormalizeToCanonicalYaml(jaml);
    }
}
