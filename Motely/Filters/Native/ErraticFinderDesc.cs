using System.Runtime.Intrinsics;

namespace Motely.Filters.Native;

public struct ErraticFinderDesc() : IMotelySeedFilterDesc<ErraticFinderDesc.FilterStruct>
{
    public const MotelyStandardcardSuit CardSuit = MotelyStandardcardSuit.Hearts;
    public const int RequiredCount = 28;

    public readonly FilterStruct CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        ctx.CacheErraticDeckPrngStream();

        return new FilterStruct();
    }

    public struct FilterStruct() : IMotelySeedFilter
    {
        public readonly VectorMask Filter(ref MotelyVectorSearchContext searchContext)
        {
            var stream = searchContext.CreateErraticDeckPrngStream(true);

            Vector256<int> counts = Vector256<int>.Zero;

            for (int i = 0; i < 52; i++)
            {
                var cardVector = searchContext.GetNextErraticDeckCard(ref stream);

                counts += Vector256.ConditionalSelect(
                    VectorEnum256.Equals(cardVector.StandardcardSuit, CardSuit),
                    Vector256<int>.One,
                    Vector256<int>.Zero
                );
            }

            return Vector256.GreaterThanOrEqual(counts, Vector256.Create(RequiredCount));
        }
    }
}
