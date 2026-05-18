namespace Motely.Filters.Jaml;

/// <summary>
/// Soul-joker structural validation is currently a no-op — pifreak rule: trust users, don't block
/// on inferred mistakes, don't write to console from library code. If a user genuinely targets the
/// only-arcana/Spectral-slot-zero-at-ante-1 dead case, the search returns zero matches and that's
/// information enough.
/// <para>
/// Future work tracked separately: surface structured warnings via TryLoad return shape so the CLI
/// can print them in red, the WASM bridge can hand them to JS, mobile can route to logging — and
/// none of that requires the library to touch <c>Console</c> directly.
/// </para>
/// </summary>
internal static class JamlLegendaryJokerStructuralValidation
{
    internal static void ValidateLegendaryJokerClauseOrThrow(LegendaryJokerClause clause)
    {
        // Intentionally empty. See class doc.
    }
}
