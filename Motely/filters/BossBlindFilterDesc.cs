using System.Runtime.Intrinsics;

namespace Motely;

/// <summary>
/// Filter descriptor for finding specific boss blinds in Balatro seeds
/// </summary>
public struct BossBlindFilterDesc : IMotelySeedFilterDesc<BossBlindFilterDesc.BossBlindFilter>
{
    public MotelyBossBlind TargetBoss { get; }
    public int[] TargetAntes { get; }

    public BossBlindFilterDesc(MotelyBossBlind targetBoss, int[] targetAntes)
    {
        TargetBoss = targetBoss;
        TargetAntes = targetAntes ?? new[] { 1, 2, 3, 4, 5, 6, 7, 8 }; // Default to antes 1-8
    }

    public BossBlindFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        // Cache the boss PRNG stream
        ctx.CachePseudoHash("boss");
        
        // Boss blinds appear in EVERY ante
        foreach (var ante in TargetAntes)
        {
            // We might need to cache specific ante-based streams
            // ctx.CachePseudoHash($"boss_ante_{ante}");
        }
        
        return new BossBlindFilter(TargetBoss, TargetAntes);
    }

    public struct BossBlindFilter : IMotelySeedFilter
    {
        private readonly MotelyBossBlind _targetBoss;
        private readonly int[] _targetAntes;

        public BossBlindFilter(MotelyBossBlind targetBoss, int[] targetAntes)
        {
            _targetBoss = targetBoss;
            _targetAntes = targetAntes;
        }

        public VectorMask Filter(ref MotelyVectorSearchContext searchContext)
        {
            VectorMask mask = VectorMask.AllBitsSet;
            
            // Boss generation algorithm from Ouija:
            // 1. Bosses appear in EVERY ante (after small and big blinds)
            // 2. There are regular bosses and finisher bosses (ante % 8 == 0)
            // 3. Once a boss is used, it's locked until the pool is exhausted
            // 4. This implementation is simplified - doesn't track locked bosses
            
            foreach (var ante in _targetAntes)
            {
                
                // Create boss PRNG stream
                MotelyVectorPrngStream bossStream = searchContext.CreatePrngStream("boss");
                
                // Get random values for boss selection
                var randomValues = searchContext.GetNextRandom(ref bossStream);
                
                // Calculate which boss index this would select (0-27 for 28 total bosses)
                // We need to check if floor(randomValue * 28) == targetBoss
                var targetIndex = (int)_targetBoss;
                var lowerBound = (double)targetIndex / 28.0;
                var upperBound = (double)(targetIndex + 1) / 28.0;
                
                // Check if random value falls in the range for our target boss
                var matchesLower = Vector512.GreaterThanOrEqual(randomValues, Vector512.Create(lowerBound));
                var matchesUpper = Vector512.LessThan(randomValues, Vector512.Create(upperBound));
                
                // Both conditions must be true
                VectorMask anteMask = matchesLower & matchesUpper;
                mask &= anteMask;
                
                if (mask.IsAllFalse())
                {
                    break; // Early exit if no seeds match
                }
            }
            
            return mask;
        }
    }
}