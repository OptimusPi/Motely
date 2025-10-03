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
            
            // Verify each passing seed individually to avoid SIMD bugs
            var clauses = _clauses; // Copy to local for lambda capture
            var minAnte = _minAnte; // Copy to local for lambda capture
            var maxAnte = _maxAnte; // Copy to local for lambda capture
            return ctx.SearchIndividualSeeds(resultMask, (ref MotelySingleSearchContext singleCtx) =>
            {
                var singleVoucherState = new MotelyRunState();

                // Track which clauses are satisfied
                bool[] clausesSatisfied = new bool[clauses.Length];

                // Build voucher state from minAnte and check clauses (ante 0 exists with Hieroglyph/Petroglyph)
                for (int ante = minAnte; ante <= maxAnte && ante < clauses[0].WantedAntes.Length; ante++)
                {
                    var voucher = singleCtx.GetAnteFirstVoucher(ante, singleVoucherState);
                    DebugLogger.Log($"[VOUCHER VERIFY] Ante {ante}: Got voucher {voucher}");
                    singleVoucherState.ActivateVoucher(voucher);

                    // Check each clause for this ante
                    for (int i = 0; i < clauses.Length; i++)
                    {
                        if (clausesSatisfied[i]) continue; // Already satisfied

                        var clause = clauses[i];

                        if (ante < clause.WantedAntes.Length && clause.WantedAntes[ante])
                        {
                            DebugLogger.Log($"[VOUCHER VERIFY] Checking ante {ante} for clause {i} (want {clause.VoucherType})");
                            bool voucherMatches = false;
                            if (clause.VoucherTypes != null && clause.VoucherTypes.Count > 1)
                            {
                                // Multi-value check
                                foreach (var voucherType in clause.VoucherTypes)
                                {
                                    if (voucher == voucherType)
                                    {
                                        voucherMatches = true;
                                        break;
                                    }
                                }
                            }
                            else
                            {
                                // Single value check
                                voucherMatches = voucher == clause.VoucherType;
                            }

                            DebugLogger.Log($"[VOUCHER VERIFY] Match result: {voucherMatches}");
                            if (voucherMatches)
                            {
                                clausesSatisfied[i] = true;
                            }
                        }
                    }
                }

                // Check if all clauses satisfied
                for (int i = 0; i < clauses.Length; i++)
                {
                    DebugLogger.Log($"[VOUCHER VERIFY] Final check: Clause {i} satisfied = {clausesSatisfied[i]}");
                    if (!clausesSatisfied[i])
                    {
                        DebugLogger.Log($"[VOUCHER VERIFY] REJECTED - Clause {i} not satisfied");
                        return false;
                    }
                }

                DebugLogger.Log($"[VOUCHER VERIFY] ACCEPTED - All clauses satisfied!");
                return true; // All clauses satisfied
            });
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HasAntes(bool[] antes)
        {
            for (int i = 0; i < antes.Length; i++)
                if (antes[i]) return true;
            return false;
        }
    }
}