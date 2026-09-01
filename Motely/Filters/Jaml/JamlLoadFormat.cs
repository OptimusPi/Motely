namespace Motely.Filters.Jaml;

/// <summary>How to parse a filter document. Auto: leading <c>{</c> is JSON, else JAML.</summary>
public enum JamlLoadFormat
{
    Auto,
    Jaml,
    Json,
    Yaml,
}
