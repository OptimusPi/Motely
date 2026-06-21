namespace Motely.Filters.Jaml;

// ─────────────────────────────────────────────────────────────────────────────
// JAML clause spine. Plain abstract classes, no interface.
//   JamlClauseBase — the common surface JamlConfig + the search builder consume.
//   JamlClause     — ante-targeted filters (cards, jokers, bosses, tags, vouchers…).
//   RollClause     — per-roll event/trigger filters (lucky money, wheel, extinct…).
//   LogicClause    — and/or composition, lives in Motely/Filters/LogicClause.cs.
// Minimal by design: only what the live engine actually reads off a clause.
// No Describe(): it is self-describing. Nothing reads it.
// ─────────────────────────────────────────────────────────────────────────────

public abstract class JamlClauseBase
{
    public string? Label { get; set; }
    public int Min { get; set; } = 1;
    public int? Max { get; set; }
    public int Score { get; set; }
}

/// <summary>Ante-targeted clause: matches something across a set of antes.</summary>
public abstract class JamlClause : JamlClauseBase
{
    public int[] Antes { get; set; } = [];

    /// <summary>Highest ante this clause targets (0 if none).</summary>
    public int MaxAnte
    {
        get
        {
            int max = 0;
            for (int i = 0; i < Antes.Length; i++)
                if (Antes[i] > max)
                    max = Antes[i];
            return max;
        }
    }
}

/// <summary>Per-roll event/trigger clause: matches across a set of roll indices.</summary>
public abstract class RollClause : JamlClauseBase
{
    public int[] Rolls { get; set; } = [];
    public int Luck { get; set; } = 1;
}
