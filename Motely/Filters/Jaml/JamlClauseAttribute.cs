namespace Motely.Filters.Jaml;

/// <summary>
/// Declares the JAML discriminator key(s) (e.g. <c>"legendaryJoker"</c>) for the <see cref="IJamlClause"/>
/// this decorates. This is the source of truth for vocab/schema generation — put it directly on the
/// clause class that sits next to its real <c>*FilterDesc</c> (and that FilterDesc's co-located
/// <c>DefaultSources</c>, when present), not in a hand-maintained parallel list. A generator can find
/// every clause type by reflecting for this attribute instead of duplicating <c>JamlConfigLoader</c>'s
/// discriminator switch.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class JamlClauseAttribute(string key, params string[] aliases) : Attribute
{
    /// <summary>Primary discriminator key, e.g. <c>"legendaryJoker"</c>.</summary>
    public string Key { get; } = key;

    /// <summary>Additional accepted discriminator keys, e.g. <c>"legendaryJokers"</c>.</summary>
    public string[] Aliases { get; } = aliases;

    /// <summary>
    /// Estimated cost in crunches — one crunch ≈ one vectorized PRNG pull (2 vector divisions +
    /// a full LuaRandom reseed covering 8 seeds). Per targeted ante for ante-scoped clauses
    /// (unless <see cref="CostPerAnte"/> is false), total otherwise. Tiers measured in
    /// <c>JamlClauseCosts.md</c>; <see cref="JamlCostModel"/> reads this so JamlSearchBuilder can
    /// run cheap clauses first. 0 = unspecified (falls back to a mid-tier default).
    /// </summary>
    public int Cost { get; init; }

    /// <summary>
    /// False for ante-scoped clauses whose cost does not scale with the targeted ante count
    /// (e.g. erratic deck clauses: one flat 52-card walk no matter how many antes are named).
    /// </summary>
    public bool CostPerAnte { get; init; } = true;
}
