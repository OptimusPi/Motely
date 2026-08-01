using System.Runtime.CompilerServices;

namespace Motely.Filters.Jaml;

/// <summary>
/// Category-any law: null or empty discriminator list means "any of this category".
/// Named list means only those members. No wire token <c>Any</c>.
/// </summary>
internal static class JamlDisc
{
    /// <summary>True when the disc list is absent or empty — match the whole category.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsCategoryAny<T>(T[]? items) => items is not { Length: > 0 };

    /// <summary>Never null — loader/host may leave the property unset.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] OrEmpty<T>(T[]? items) => items ?? [];
}
