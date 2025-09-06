using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Motely.Filters;

namespace Motely.Filters;

/// <summary>
/// Filters seeds based on spectral card criteria from JSON configuration.
/// </summary>
public struct MotelyJsonSpectralCardFilterDesc(List<MotelyJsonConfig.MotleyJsonFilterClause> spectralClauses)
    : IMotelySeedFilterDesc<MotelyJsonSpectralCardFilterDesc.MotelyJsonSpectralCardFilter>
{
    private readonly List<MotelyJsonConfig.MotleyJsonFilterClause> _spectralClauses = spectralClauses;

    public readonly string Name => "JSON Spectral Card Filter";
    public readonly string Description => "Spectral card filtering from Spectral packs with pack slot support";

    public MotelyJsonSpectralCardFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        foreach (var clause in _spectralClauses)
        {
            if (clause.EffectiveAntes != null)
            {
                foreach (var ante in clause.EffectiveAntes)
                {
                    ctx.CacheBoosterPackStream(ante);
                }
            }
        }
        
        return new MotelyJsonSpectralCardFilter(_spectralClauses);
    }

    public struct MotelyJsonSpectralCardFilter(List<MotelyJsonConfig.MotleyJsonFilterClause> clauses) : IMotelySeedFilter
    {
        private readonly List<MotelyJsonConfig.MotleyJsonFilterClause> _clauses = clauses;

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            if (_clauses == null || _clauses.Count == 0)
                return VectorMask.AllBitsSet;
            
            var clausesForLambda = _clauses;
            return ctx.SearchIndividualSeeds(VectorMask.AllBitsSet, (ref MotelySingleSearchContext singleCtx) =>
            {
#if DEBUG
                Console.WriteLine($"[SpectralFilter] Checking seed");
#endif
                // Check all spectral clauses
                foreach (var clause in clausesForLambda)
                {
                    bool clauseSatisfied = false;
#if DEBUG
                    Console.WriteLine($"[SpectralFilter] Clause: {clause.SpectralEnum?.ToString() ?? "NULL"}");
#endif
                    
                    foreach (var ante in clause.EffectiveAntes ?? Array.Empty<int>())
                    {
#if DEBUG
                        Console.WriteLine($"[SpectralFilter] Checking ante {ante}");
#endif
                        var packSlots = clause.Sources?.PackSlots ?? new[] { 0, 1, 2, 3, 4 };
                        
                        // DYNAMIC: Set generatedFirstPack based on wanted pack slots
                        bool needsInitialBuffoon = ante == 1 && Array.Exists(packSlots, slot => slot == 0);
                        var packStream = singleCtx.CreateBoosterPackStream(ante, !needsInitialBuffoon, false);
                        
                        // Check ALL possible packs to find spectral packs
                        for (int packIndex = 0; packIndex < 10; packIndex++)
                        {
                            var pack = singleCtx.GetNextBoosterPack(ref packStream);
#if DEBUG
                            Console.WriteLine($"[SpectralFilter] Pack {packIndex}: {pack.GetPackType()} {pack.GetPackSize()}");
#endif
                            
                            // Check if this pack slot is wanted
                            if (packSlots.Length > 0 && !Array.Exists(packSlots, slot => slot == packIndex))
                            {
#if DEBUG
                                Console.WriteLine($"[SpectralFilter] Pack {packIndex} not in wanted slots [{string.Join(",", packSlots)}]");
#endif
                                continue;
                            }
                            
                            if (pack.GetPackType() == MotelyBoosterPackType.Spectral)
                            {
#if DEBUG
                                Console.WriteLine($"[SpectralFilter] Found Spectral pack!");
#endif
                                // Check pack size requirements - Mega means "pick 2" instead of 1
                                if (clause.Sources?.RequireMega == true && pack.GetPackSize() != MotelyBoosterPackSize.Mega)
                                {
#if DEBUG
                                    Console.WriteLine($"[SpectralFilter] Pack size {pack.GetPackSize()} doesn't match RequireMega");
#endif
                                    continue;
                                }
                                
                                var spectralStream = singleCtx.CreateSpectralPackSpectralStream(ante, true);
                                var contents = singleCtx.GetNextSpectralPackContents(ref spectralStream, pack.GetPackSize());
#if DEBUG
                                Console.WriteLine($"[SpectralFilter] Pack contents: {string.Join(", ", contents.AsArray().Select(x => x.Type.ToString()))}");
#endif
                                
                                for (int cardIndex = 0; cardIndex < contents.Length; cardIndex++)
                                {
                                    var card = contents[cardIndex];
                                    
                                    bool typeMatches = clause.SpectralEnum.HasValue
                                        ? card.Type == (MotelyItemType)clause.SpectralEnum.Value
                                        : card.TypeCategory == MotelyItemTypeCategory.SpectralCard;
                                    
                                    bool editionMatches = !clause.EditionEnum.HasValue || card.Edition == clause.EditionEnum.Value;
                                    
#if DEBUG
                                    Console.WriteLine($"[SpectralFilter] Card {cardIndex}: {card.Type}, type={typeMatches}, edition={editionMatches}");
#endif
                                    
                                    if (typeMatches && editionMatches)
                                    {
#if DEBUG
                                        Console.WriteLine($"[SpectralFilter] FOUND MATCH!");
#endif
                                        clauseSatisfied = true;
                                        goto NextClause; // Found this clause, move to next
                                    }
                                }
                            }
                        }
                    }
                    
                    NextClause:
                    if (!clauseSatisfied) 
                    {
#if DEBUG
                        Console.WriteLine($"[SpectralFilter] Clause FAILED");
#endif
                        return false; // This clause failed
                    }
                }
                
#if DEBUG
                Console.WriteLine($"[SpectralFilter] All clauses PASSED");
#endif
                return true; // All clauses satisfied
            });
        }
    }
}