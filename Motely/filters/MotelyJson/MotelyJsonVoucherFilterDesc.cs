using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Motely.Filters;

namespace Motely.Filters;

/// <summary>
/// Filters seeds based on voucher criteria from JSON configuration.
/// </summary>
public struct MotelyJsonVoucherFilterDesc(MotelyJsonVoucherFilterCriteria criteria)
    : IMotelySeedFilterDesc<MotelyJsonVoucherFilterDesc.MotelyJsonVoucherFilter>
{
    private readonly MotelyJsonVoucherFilterCriteria _criteria = criteria;

    public MotelyJsonVoucherFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        // Cache only the antes we actually need
        foreach (var clause in _criteria.Clauses)
        {
            DebugLogger.Log($"[VOUCHER] Clause: VoucherType={clause.VoucherType}, VoucherTypes={clause.VoucherTypes?.Count ?? 0}");

            // Extract antes from array (ante is 1-based, but we're iterating 0-based array)
            for (int anteIndex = 0; anteIndex < 40; anteIndex++)
            {
                if (clause.WantedAntes[anteIndex])
                {
                    DebugLogger.Log($"[VOUCHER] Caching ante {anteIndex} for voucher {clause.VoucherType}");
                    ctx.CacheAnteFirstVoucher(anteIndex); // Antes are 0-based
                }
            }
        }

        return new MotelyJsonVoucherFilter(_criteria.Clauses, _criteria.MinAnte, _criteria.MaxAnte);
    }

    public struct MotelyJsonVoucherFilter : IMotelySeedFilter
    {
        private readonly MotelyJsonVoucherFilterClause[] _clauses;
        private readonly int _minAnte;
        private readonly int _maxAnte;

        public MotelyJsonVoucherFilter(List<MotelyJsonVoucherFilterClause> clauses, int minAnte, int maxAnte)
        {
            _clauses = clauses.ToArray();
            _minAnte = minAnte;
            _maxAnte = maxAnte;

            DebugLogger.Log($"[VOUCHER FILTER] Created filter with minAnte={minAnte}, maxAnte={maxAnte}, {clauses.Count} clauses");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            if (_clauses == null || _clauses.Length == 0)
                return VectorMask.AllBitsSet;
            
            // Stack-allocated clause masks - no heap allocation!
            Span<VectorMask> clauseMasks = stackalloc VectorMask[_clauses.Length];
            for (int i = 0; i < clauseMasks.Length; i++)
                clauseMasks[i] = VectorMask.NoBitsSet;
            
            var voucherState = new MotelyVectorRunState();

            // Check vouchers from minAnte to maxAnte (ante 0 exists with Hieroglyph/Petroglyph)
            for (int ante = _minAnte; ante <= _maxAnte && ante < _clauses[0].WantedAntes.Length; ante++)
            {
                var vouchers = ctx.GetAnteFirstVoucher(ante, voucherState);
                DebugLogger.Log($"[VOUCHER VECTORIZED] Ante {ante}: Checking vouchers, lane 0 has {vouchers[0]}");

                // Check all clauses for this ante
                for (int i = 0; i < _clauses.Length; i++)
                {
                    // Check if this ante is wanted
                    if (ante < _clauses[i].WantedAntes.Length && _clauses[i].WantedAntes[ante])
                    {
                        DebugLogger.Log($"[VOUCHER VECTORIZED] Ante {ante}: Clause {i} wants {_clauses[i].VoucherType}");
                        VectorMask matches = VectorMask.NoBitsSet;

                        if (_clauses[i].VoucherTypes?.Count > 1)
                        {
                            // Multi-value: OR logic - match any voucher in the list
                            foreach (var voucherType in _clauses[i].VoucherTypes!)
                            {
                                matches |= VectorEnum256.Equals(vouchers, voucherType);
                            }
                        }
                        else
                        {
                            // Single value
                            matches = VectorEnum256.Equals(vouchers, _clauses[i].VoucherType);
                        }

                        DebugLogger.LogMask(matches, $"Ante {ante} clause {i} matches");
                        clauseMasks[i] |= matches;
                        DebugLogger.LogMask(clauseMasks[i], $"Clause {i} accumulated mask after ante {ante}");
                    }
                }

                voucherState.ActivateVoucher(vouchers);
            }
            
            // All voucher clauses must be satisfied
            // CRITICAL FIX: If any clause found nothing (NoBitsSet), the entire filter fails!
            var resultMask = VectorMask.AllBitsSet;
            for (int i = 0; i < clauseMasks.Length; i++)
            {
                // FIX: If this clause found nothing across all antes, fail immediately
                if (clauseMasks[i].IsAllFalse())
                {
                    return VectorMask.NoBitsSet;
                }
                
                resultMask &= clauseMasks[i];
                if (resultMask.IsAllFalse()) return VectorMask.NoBitsSet;
            }
            
            if (resultMask.IsAllFalse())
                return VectorMask.NoBitsSet;
            
            // USE THE SHARED FUNCTION - same logic as scoring!
            var clauses = _clauses;
            return ctx.SearchIndividualSeeds(resultMask, (ref MotelySingleSearchContext singleCtx) =>
            {
                var voucherState = new MotelyRunState();

                // Check all clauses using the SAME shared function used in scoring
                foreach (var clause in clauses)
                {
                    var genericClause = ConvertToGeneric(clause);
                    int totalCount = MotelyJsonScoring.CountVoucherOccurrences(ref singleCtx, genericClause, ref voucherState);

                    // Check Min threshold (default to 1 if not specified)
                    int minThreshold = clause.Min ?? 1;
                    if (totalCount < minThreshold)
                        return false;
                }

                return true;
            });
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HasAntes(bool[] antes)
        {
            for (int i = 0; i < antes.Length; i++)
                if (antes[i]) return true;
            return false;
        }

        private static MotelyJsonConfig.MotleyJsonFilterClause ConvertToGeneric(MotelyJsonVoucherFilterClause clause)
        {
            var effectiveAntes = new List<int>();
            for (int i = 0; i < clause.WantedAntes.Length; i++)
            {
                if (clause.WantedAntes[i])
                    effectiveAntes.Add(i);
            }

            return new MotelyJsonConfig.MotleyJsonFilterClause
            {
                VoucherEnum = clause.VoucherType,
                VoucherEnums = clause.VoucherTypes,
                EffectiveAntes = effectiveAntes.ToArray()
            };
        }
    }
}