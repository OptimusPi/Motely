namespace Motely.Filters;

/// <summary>
/// Platform-agnostic interface for JAML formatting and parsing.
/// Desktop uses YamlDotNet (reflection-based), Browser uses AOT-compatible alternative.
/// </summary>
public interface IJamlFormatter
{
    /// <summary>
    /// Format a MotelyJsonConfig to clean JAML string
    /// </summary>
    string Format(MotelyJsonConfig config);

    /// <summary>
    /// Format raw JAML string (parse then re-serialize with formatting)
    /// </summary>
    string Format(string jamlContent);

    /// <summary>
    /// Parse JAML string to MotelyJsonConfig
    /// </summary>
    MotelyJsonConfig Parse(string jamlContent);
}
