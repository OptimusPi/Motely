namespace Motely;

/// <summary>
/// One entry in <see cref="MotelyGlossary.Entries"/>: a term used across the CLI, MCP, and
/// WASM/npm surfaces that doesn't self-explain from its name alone.
/// </summary>
public sealed record MotelyGlossaryTerm(string Term, string? Acronym, string Definition);

/// <summary>
/// Single glossary for JAML and JAMLyzer. CLI <c>--glossary</c>, the MCP glossary tool, and
/// the WASM/npm export all render from this list — one write, every surface stays in lockstep.
/// </summary>
public static class MotelyGlossary
{
    public static readonly IReadOnlyList<MotelyGlossaryTerm> Entries =
    [
        new(
            "JAML",
            "Jimbo's Ante Markup Language",
            "Motely's filter config language. JAML text parses to "
                + "the same typed JamlConfig. A JAML file declares 'must' (hard requirements) and "
                + "'should' (scored, optional) clauses against a seed's antes. A clause can also be "
                + "written as one human-readable line, e.g. 'Eternal Blueprint in antes 1 or 2' — "
                + "that line parses to and formats back from the clause losslessly, so it's a "
                + "plain-English shorthand for editing a single 'must'/'should' entry by hand."
        ),
        new(
            "JAMLyzer",
            null,
            "The per-seed analyzer: walks every ante's boss, voucher, tags, shop, and packs — plus "
                + "every relevant PRNG stream — for a seed, and (when the JAML has 'should' clauses) "
                + "attaches a score. It answers \"what does this seed actually contain?\". Unrelated "
                + "to the CLI's older --analyze flag, which runs a separate legacy text-block analyzer."
        ),
    ];

    /// <summary>Plain-text rendering of every term, in <see cref="Entries"/> order.</summary>
    public static string Render()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var entry in Entries)
        {
            sb.Append(entry.Term);
            if (entry.Acronym is not null)
                sb.Append(" (").Append(entry.Acronym).Append(')');
            sb.Append(" — ").Append(entry.Definition).AppendLine();
        }
        return sb.ToString().TrimEnd();
    }

    /// <summary>Case-insensitive lookup of a single term's entry, or null if unknown.</summary>
    public static MotelyGlossaryTerm? TryGet(string term) =>
        Entries.FirstOrDefault(e => string.Equals(e.Term, term, StringComparison.OrdinalIgnoreCase));
}
