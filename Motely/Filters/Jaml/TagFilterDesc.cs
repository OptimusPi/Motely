using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Motely.Filters.Jaml;

public enum TagPosition
{
    Any,
    SmallBlind,
    BigBlind,
}

public sealed class TagClause : JamlClause
{
    public required MotelyTag[] Tags { get; init; }
    public TagPosition Position { get; init; } = TagPosition.Any;

    public override int EstimatedCost => 3 + MaxAnte;
    public override string Describe() => $"tag {string.Join(", ", System.Array.ConvertAll(Tags, static t => t.ToString()))}";
    public override IMotelySeedFilterDesc CreateDesc() => new TagFilterDesc(this);
}

public struct TagFilterDesc(TagClause clause) : IMotelySeedFilterDesc<TagFilterDesc.TagFilter>
{
    private readonly TagClause _clause = clause;

    public TagFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        foreach (var ante in _clause.Antes)
        {
            ctx.CacheBoosterPackStream(ante);
            ctx.CacheTagStream(ante);
        }
        return new TagFilter(_clause);
    }

    public struct TagFilter(TagClause clause) : IMotelySeedFilter
    {
        private readonly TagClause _clause = clause;

        [MethodImpl(
            MethodImplOptions.AggressiveInlining
        )]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            Debug.Assert(_clause.Tags.Length > 0);
            var clause = _clause;

            Vector256<int> matchCounts = Vector256<int>.Zero;

            foreach (var ante in clause.Antes)
            {
                var tagStream = ctx.CreateTagStream(ante);
                var smallTag = ctx.GetNextTag(ref tagStream);
                var bigTag = ctx.GetNextTag(ref tagStream);

                foreach (var t in clause.Tags)
                {
                    if (clause.Position == TagPosition.SmallBlind || clause.Position == TagPosition.Any)
                    {
                        var match = VectorEnum256.Equals(smallTag, t);
                        matchCounts = Vector256.Add(
                            matchCounts,
                            Vector256.ConditionalSelect(
                                match,
                                Vector256.Create(1),
                                Vector256<int>.Zero
                            )
                        );
                    }

                    if (clause.Position == TagPosition.BigBlind || clause.Position == TagPosition.Any)
                    {
                        var match = VectorEnum256.Equals(bigTag, t);
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
