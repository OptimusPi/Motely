using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Motely.Filters;

public enum TagPosition { Any, SmallBlind, BigBlind }

public sealed class TagClause : IJamlClause
{
    public string Label { get; init; } = "";
    public int Score { get; init; }
    public required MotelyTag[] Tags { get; init; }
    public TagPosition Position { get; init; } = TagPosition.Any;
    public int[] Antes { get; init; } = [];
    public int Min { get; init; } = 1;
}

public struct TagFilterDesc(TagClause clause)
    : IMotelySeedFilterDesc<TagFilterDesc.TagFilter>
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

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            Debug.Assert(_clause.Tags.Length > 0);
            var clause = _clause;

            VectorMask result = VectorMask.NoBitsSet;

            foreach (var ante in clause.Antes)
            {
                var tagStream = ctx.CreateTagStream(ante);
                var smallTag = ctx.GetNextTag(ref tagStream);
                var bigTag = ctx.GetNextTag(ref tagStream);

                foreach (var t in clause.Tags)
                    result |= MatchTag(smallTag, bigTag, t, clause.Position);

                if (result.IsAllTrue()) return result;
            }

            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static VectorMask MatchTag(
            VectorEnum256<MotelyTag> smallTag,
            VectorEnum256<MotelyTag> bigTag,
            MotelyTag target,
            TagPosition position)
        {
            return position switch
            {
                TagPosition.SmallBlind => VectorEnum256.Equals(smallTag, target),
                TagPosition.BigBlind => VectorEnum256.Equals(bigTag, target),
                _ => VectorEnum256.Equals(smallTag, target) | VectorEnum256.Equals(bigTag, target),
            };
        }
    }
}
