using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Motely.Filters.Jaml;

[JamlDiscriminator("boss", "bosses", ValueEnum = typeof(MotelyBossBlind))]
public sealed class BossClause : IJamlClause, IAnteScopedClause
{
    /// <summary>Clause keys mirror BossFilterDesc's — the desc owns the grammar, the clause
    /// carries the field so key validation and schema generation can read it. No Rolls yet
    /// (see below).</summary>
    public static readonly string[] ClauseKeys = BossFilterDesc.ClauseKeys;

    public string? Label { get; set; }
    public int Min { get; set; } = 1;
    public int? Max { get; set; }
    public int Score { get; set; }
    public int[] Antes { get; set; } = [];
    public MotelyBossBlind[] Bosses { get; set; } = [];
    // No Rolls — for now. Boss re-rolls ARE a real source, but the re-roll read isn't
    // implemented in MotelySearchContext.Boss.cs yet (state-heavy, same blocker as joker
    // re-rolls). Antes select the WHERE; re-add Rolls here when that source lands.
}

public readonly struct BossFilterDesc(BossClause clause)
    : IMotelySeedFilterDesc<BossFilterDesc.BossFilter>,
      IJamlClauseDesc<BossClause>
{
    private readonly BossClause _clause = clause;

    /// <inheritdoc/>
    public static string[] Discriminators => ["boss", "bosses"];

    /// <inheritdoc/>
    public static string[] ClauseKeys => ["min", "max", "score", "label", "ante", "antes"];

    /// <summary>Boss clauses carry no keys beyond the common set, so nothing is claimed here.</summary>
    public static bool Set(BossClause clause, string key, IJamlValueReader value) => false;

    /// <inheritdoc/>
    public static bool SetDiscriminatorValue(BossClause clause, IJamlValueReader value)
    {
        if (!value.TryEnumArray<MotelyBossBlind>(out var bosses))
            return false;
        clause.Bosses = bosses;
        return true;
    }

    public BossFilter CreateFilter(ref MotelyFilterCreationContext ctx) =>
        new BossFilter(_clause);

    public struct BossFilter(BossClause clause) : IMotelySeedFilter
    {
        private readonly BossClause _clause = clause;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            Debug.Assert(_clause.Bosses.Length > 0);

            // Single match core: same PrepareRunState + CountBossOccurrences as should-scoring.
            var clause = _clause;
            return ctx.SearchIndividualSeeds(
                (MotelySingleSearchContext singleCtx) =>
                    JamlScoring.ClauseMeetsMinForFilter(ref singleCtx, clause) ? 1 : 0
            );
        }
    }
}
