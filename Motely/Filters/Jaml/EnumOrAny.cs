namespace Motely.Filters.Jaml;

/// <summary>
/// Wildcard-or-specific value union for clause discriminators that accept the JAML literal
/// scalar <c>any</c> (case-insensitive) in addition to a typed enum member name. The clause
/// type's rarity is encoded by the clause key (<c>joker:</c>, <c>commonJoker:</c>, etc.), not
/// by the value — so this type only carries the wildcard flag and a specific enum value.
/// </summary>
/// <remarks>
/// Parsed by <see cref="Converters.EnumOrAnyConverter{T}"/>. Schema emitted as a <c>oneOf</c>
/// over <c>{ "const": "any" }</c> and the typed enum's <c>$ref</c> by the file-based generator
/// at the repo root (<c>dotnet run jaml-schema.cs</c>).
/// </remarks>
public readonly record struct EnumOrAny<T>(bool IsAny, T Value)
    where T : struct, Enum
{
    public static EnumOrAny<T> Any => new(true, default);

    public static EnumOrAny<T> Of(T value) => new(false, value);
}
