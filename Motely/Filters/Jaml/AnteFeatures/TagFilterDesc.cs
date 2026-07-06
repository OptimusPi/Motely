using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Motely.Filters.Jaml;

public sealed class TagClause : IJamlClause, IAnteScopedClause
{
    /// <summary>This clause's own clause-level keys beyond JamlClause.SharedKeys — shared by
    /// tag/smallBlindTag/bigBlindTag, all backed by this same TagClause type.</summary>
    public static readonly string[] ClauseKeys = ["rolls"];

    public string? Label { get; set; }
    public int Min { get; set; } = 1;
    public int? Max { get; set; }
    public int Score { get; set; }
    public int[] Antes { get; set; } = [];
    public required MotelyTag[] Tags { get; set; }

    /// <summary>
    /// Tag-stream draw indices per ante: 0 = small-blind offer, 1 = big-blind offer,
    /// 2+ = further draws on the same ante stream (replay / double-tag extras).
    /// </summary>
    public required int[] Rolls { get; set; }
}

public struct TagFilterDesc(TagClause clause) : IMotelySeedFilterDesc<TagFilterDesc.TagFilter>
{
    private readonly TagClause _clause = clause;

    public TagFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        foreach (var ante in _clause.Antes)
        {
            // NOTE(audit): the tag clause only reads the tag stream below — this
            // booster-pack-stream cache looks unused/vestigial here. Left intact pending review;
            // remove if nothing downstream actually consumes a cached pack stream for tags.
            ctx.CacheBoosterPackStream(ante);
            ctx.CacheTagStream(ante);
        }
        return new TagFilter(_clause);
    }

    public struct TagFilter(TagClause clause) : IMotelySeedFilter
    {
        private readonly TagClause _clause = clause;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            Debug.Assert(_clause.Tags.Length > 0);
            var clause = _clause;
            int maxDraw = MapFeatureRolls.MaxRollIndex(clause.Rolls);
            Span<VectorEnum256<MotelyTag>> draws = stackalloc VectorEnum256<MotelyTag>[maxDraw + 1];

            Vector256<int> matchCounts = Vector256<int>.Zero;

            foreach (var ante in clause.Antes)
            {
                var tagStream = ctx.CreateTagStream(ante);
                for (int i = 0; i <= maxDraw; i++)
                    draws[i] = ctx.GetNextTag(ref tagStream);

                foreach (var drawIndex in clause.Rolls)
                {
                    var rolled = draws[drawIndex];
                    foreach (var t in clause.Tags)
                    {
                        var match = VectorEnum256.Equals(rolled, t);
                        matchCounts = Vector256.Add(
                            matchCounts,
                            Vector256.ConditionalSelect(
                                match,
                                Vector256.Create(1),
                                Vector256<int>.Zero
                            )
                        );
                    }
                }
            }

            var comparison = Vector256.GreaterThan(
                matchCounts,
                Vector256.Subtract(Vector256.Create(clause.Min), Vector256.Create(1))
            );
            return new VectorMask(MotelyVectorUtils.VectorizedComparisonToMask(comparison));
        }
    }
}
