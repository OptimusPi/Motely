using System;
using System.Runtime.Intrinsics;

namespace Motely;

public struct SoulTestFilterDesc() : IMotelySeedFilterDesc<SoulTestFilterDesc.SoulTestFilter>
{
    public SoulTestFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        // Cache the streams we need
        ctx.CacheBoosterPackStream(1);
        ctx.CacheBoosterPackStream(2);
        ctx.CacheSoulJokerStream(1);
        ctx.CacheSoulJokerStream(2);
        ctx.CacheArcanaPackTarotStream(1, true);
        ctx.CacheArcanaPackTarotStream(2, true);

        return new SoulTestFilter();
    }

    public struct SoulTestFilter() : IMotelySeedFilter
    {
        public readonly VectorMask Filter(ref MotelyVectorSearchContext searchContext)
        {
            // We'll check seeds individually to debug Soul generation
            return searchContext.SearchIndividualSeeds((ref MotelySingleSearchContext ctx) =>
            {
                var seed = ctx.GetSeed();
                
                // Only check our test seed
                if (seed != "92QP6311")
                    return false;

                Console.WriteLine($"\n[DEBUG] Checking seed: {seed}");
                
                // Check ante 1 pack 1 contents
                Console.WriteLine($"[DEBUG] Checking ante 1 pack 1 contents...");
                var boosterStream = ctx.CreateBoosterPackStream(1, false); // Don't use cache
                var pack = ctx.GetNextBoosterPack(ref boosterStream);
                
                Console.WriteLine($"  Pack 1: Type={pack.GetPackType()}, Size={pack.GetPackSize()}");
                
                if (pack.GetPackType() == MotelyBoosterPackType.Buffoon)
                {
                    // Get buffoon pack contents
                    var packContents = ctx.GetNextBuffoonPackContents(1, pack.GetPackCardCount());
                    Console.WriteLine($"  Buffoon pack contains {packContents.Length} jokers:");
                    
                    for (int i = 0; i < packContents.Length; i++)
                    {
                        var item = packContents.GetItem(i);
                        Console.WriteLine($"    Slot {i + 1}: {item.Type}, Edition: {item.Edition}");
                        
                        if (item.Type == MotelyItemType.Triboulet && item.Edition == MotelyItemEdition.Negative)
                        {
                            Console.WriteLine($"    [FOUND] Negative Triboulet in pack slot {i + 1}!");
                        }
                    }
                }
                
                // Check all antes for packs
                for (int ante = 1; ante <= 4; ante++)
                {
                    Console.WriteLine($"[DEBUG] Checking ante {ante} packs...");
                    var anteBoosterStream = ctx.CreateBoosterPackStream(ante, false); // Don't use cache
                    var tarotStreamInit = false;
                    var spectralStreamInit = false;
                    var tarotStream = default(MotelySingleTarotStream);
                    var spectralStream = default(MotelySingleSpectralStream);
                    
                    for (int packNum = 0; packNum < 3; packNum++)
                    {
                        var antePack = ctx.GetNextBoosterPack(ref anteBoosterStream);
                        Console.WriteLine($"  Pack {packNum + 1}: Type={antePack.GetPackType()}, Size={antePack.GetPackSize()}");
                        
                        if (antePack.GetPackType() == MotelyBoosterPackType.Arcana)
                        {
                            if (!tarotStreamInit)
                            {
                                tarotStreamInit = true;
                                tarotStream = ctx.CreateArcanaPackTarotStream(ante, true);
                            }
                            
                            var hasSoul = ctx.GetNextArcanaPackHasTheSoul(ref tarotStream, antePack.GetPackSize());
                            Console.WriteLine($"    Checking for Soul... {(hasSoul ? "FOUND!" : "not found")}");
                            
                            if (hasSoul)
                            {
                                var soulStream = ctx.CreateSoulJokerStream(ante);
                                var joker = ctx.GetNextJoker(ref soulStream);
                                
                                Console.WriteLine($"  Soul joker: {joker.Type}, Edition: {joker.Edition}");
                            }
                        }
                        else if (pack.GetPackType() == MotelyBoosterPackType.Spectral)
                        {
                            if (!spectralStreamInit)
                            {
                                spectralStreamInit = true;
                                spectralStream = ctx.CreateSpectralPackSpectralStream(ante, true);
                            }
                            
                            if (ctx.GetNextSpectralPackHasTheSoul(ref spectralStream, pack.GetPackSize()))
                            {
                                Console.WriteLine($"  [SOUL FOUND] in Spectral pack {packNum + 1}!");
                                
                                var soulStream = ctx.CreateSoulJokerStream(ante);
                                var joker = ctx.GetNextJoker(ref soulStream);
                                
                                Console.WriteLine($"  Soul joker: {joker.Type}, Edition: {joker.Edition}");
                            }
                        }
                    }
                }
                
                return false;
            });
        }
    }
}