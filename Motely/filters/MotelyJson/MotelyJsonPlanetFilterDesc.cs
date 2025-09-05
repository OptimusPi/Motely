using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Motely.Filters;

namespace Motely.Filters;

/// <summary>
/// Filters seeds based on planet card criteria from JSON configuration.
/// </summary>
public struct MotelyJsonPlanetFilterDesc(List<MotelyJsonConfig.MotleyJsonFilterClause> planetClauses)
    : IMotelySeedFilterDesc<MotelyJsonPlanetFilterDesc.MotelyJsonPlanetFilter>
{
    private readonly List<MotelyJsonConfig.MotleyJsonFilterClause> _planetClauses = planetClauses;

    public readonly string Name => "JSON Planet Filter";
    public readonly string Description => "Vectorized planet card filtering";

    public MotelyJsonPlanetFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        foreach (var clause in _planetClauses)
        {
            if (clause.EffectiveAntes != null)
            {
                foreach (var ante in clause.EffectiveAntes)
                {
                    ctx.CacheBoosterPackStream(ante);
                }
            }
        }
        
        return new MotelyJsonPlanetFilter(_planetClauses);
    }

    public struct MotelyJsonPlanetFilter(List<MotelyJsonConfig.MotleyJsonFilterClause> clauses) : IMotelySeedFilter
    {
        private readonly List<MotelyJsonConfig.MotleyJsonFilterClause> _clauses = clauses;

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            if (_clauses == null || _clauses.Count == 0)
                return VectorMask.AllBitsSet;
            
            var anteClauses = new Dictionary<int, List<MotelyJsonConfig.MotleyJsonFilterClause>>();
            foreach (var clause in _clauses)
            {
                foreach (var ante in clause.EffectiveAntes ?? Array.Empty<int>())
                {
                    if (!anteClauses.ContainsKey(ante))
                        anteClauses[ante] = new List<MotelyJsonConfig.MotleyJsonFilterClause>();
                    anteClauses[ante].Add(clause);
                }
            }
            
            var resultMask = VectorMask.AllBitsSet;
            
            foreach (var ante in anteClauses.Keys.OrderBy(x => x))
            {
                var clausesForThisAnte = anteClauses[ante];
                
                foreach (var clause in clausesForThisAnte)
                {
                    var clauseMask = VectorMask.NoBitsSet;
                    
                    // Check pack slots (Celestial packs for planets)
                    if (clause.Sources?.PackSlots != null && clause.Sources.PackSlots.Length > 0)
                    {
                        var packStream = ctx.CreateBoosterPackStream(ante);
                        var planetStream = ctx.CreateCelestialPackPlanetStream(ante);
                        var maxSlot = clause.Sources.PackSlots.Max();
                        
                        for (int slot = 0; slot <= maxSlot; slot++)
                        {
                            var pack = ctx.GetNextBoosterPack(ref packStream);
                            
                            if (clause.Sources.PackSlots.Contains(slot))
                            {
                                // VECTORIZED: Check if this is a Celestial pack
                                VectorMask isCelestialPack = VectorEnum256.Equals(pack.GetPackType(), MotelyBoosterPackType.Celestial);
                                
                                if (isCelestialPack.IsPartiallyTrue())
                                {
                                    VectorMask isNormalSize = VectorEnum256.Equals(pack.GetPackSize(), MotelyBoosterPackSize.Normal);
                                    
                                    if ((isCelestialPack & isNormalSize).IsPartiallyTrue())
                                    {
                                        var contents = ctx.GetNextCelestialPackContents(ref planetStream, MotelyBoosterPackSize.Normal);
                                        
                                        for (int j = 0; j < contents.Length; j++)
                                        {
                                            VectorMask typeMatches = clause.PlanetEnum.HasValue
                                                ? VectorEnum256.Equals(contents[j].Type, (MotelyItemType)clause.PlanetEnum.Value)
                                                : VectorEnum256.Equals(contents[j].TypeCategory, MotelyItemTypeCategory.PlanetCard);
                                            
                                            VectorMask editionMatches = VectorMask.AllBitsSet;
                                            if (clause.EditionEnum.HasValue)
                                                editionMatches = VectorEnum256.Equals(contents[j].Edition, clause.EditionEnum.Value);
                                            
                                            clauseMask |= (typeMatches & editionMatches & isCelestialPack & isNormalSize);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    
                    resultMask &= clauseMask;
                    if (resultMask.IsAllFalse()) return VectorMask.NoBitsSet;
                }
            }
            
            return resultMask;
        }
    }
}