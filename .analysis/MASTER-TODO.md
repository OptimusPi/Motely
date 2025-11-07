# Motely SIMD Hotpath Audit - Master TODO

**Audit Date**: 2025-11-06
**Auditor**: Claude (Sonnet 4.5)
**Scope**: Complete scan of Motely codebase for LINQ violations in SIMD hotpaths

---

## Summary Statistics

| Category | Count |
|----------|-------|
| **Total Issues** | 11 |
| **Critical Severity** | 3 |
| **High Severity** | 4 |
| **Medium Severity** | 3 |
| **Low Severity** | 1 |

---

## Critical Severity Issues (Immediate Action Required)

### [ ] Issue 002: LINQ .Contains() in MotelyJsonJokerFilterDesc Hotpath
**File**: `Motely\filters\MotelyJson\MotelyJsonJokerFilterDesc.cs`
**Lines**: 182, 501
**Problem**: `.Any()` and `.Contains()` in Filter() method with AggressiveOptimization
**Impact**: Lambda allocations + O(n) enumeration in SIMD hotpath
**Fix**: Replace with manual loops or HashSet
📄 [View Details](issue-002-linq-contains-joker-filter.md)

### [ ] Issue 004: LINQ .Contains() in MotelyJsonTagFilterDesc Hotpath
**File**: `Motely\filters\MotelyJson\MotelyJsonTagFilterDesc.cs`
**Line**: 101
**Problem**: `.Contains()` on int[] array in Filter() with AggressiveOptimization
**Impact**: O(n) linear search in THE hottest code path (tag filtering)
**Fix**: Use bool[] array for O(1) lookup
📄 [View Details](issue-004-linq-contains-tag-filter.md)

### [ ] Issue 008: LINQ .Min() in MotelyJsonScoring AND Clause Hotpath
**File**: `Motely\filters\MotelyJson\MotelyJsonScoring.cs`
**Line**: 1343
**Problem**: `.Min()` on HashSet in CountOccurrences (called millions of times)
**Impact**: Enumerator allocation + prevents inlining
**Fix**: Track min/max during HashSet construction
📄 [View Details](issue-008-linq-min-in-scoring-hotpath.md)

---

## High Severity Issues

### [ ] Issue 003: LINQ .Any() in MotelyJsonSoulJokerFilterDesc Individual Verification
**File**: `Motely\filters\MotelyJson\MotelyJsonSoulJokerFilterDesc.cs`
**Line**: 214
**Problem**: `.Any(x => x)` on bool array in individual seed verification lambda
**Impact**: Lambda allocation for every individual seed check
**Fix**: Use existing BoolArrayHasTrue() helper
📄 [View Details](issue-003-linq-any-soul-joker-filter.md)

### [ ] Issue 006: LINQ .ToArray() in MotelyJsonSoulJokerFilterDesc Individual Verification
**File**: `Motely\filters\MotelyJson\MotelyJsonSoulJokerFilterDesc.cs`
**Line**: 302
**Problem**: `.Select().Where().ToArray()` chain in ConvertToGeneric helper
**Impact**: 3 allocations per individual seed verification
**Fix**: Manual loop or eliminate conversion
📄 [View Details](issue-006-linq-tolist-toarray-soul-joker-desc.md)

### [ ] Issue 007: LINQ .ToArray() in MotelyJsonTarotCardFilterDesc Individual Verification
**File**: `Motely\filters\MotelyJson\MotelyJsonTarotCardFilterDesc.cs`
**Lines**: 703-708
**Problem**: List.ToArray() allocations in ConvertToGeneric
**Impact**: Multiple allocations per individual seed verification
**Fix**: Cache converted result or pre-allocate arrays
📄 [View Details](issue-007-linq-where-toarray-tarot-filter.md)

### [ ] Issue 010: Array Search Optimization Opportunity in MotelyJsonScoring
**File**: `Motely\filters\MotelyJson\MotelyJsonScoring.cs`
**Line**: 1374
**Problem**: O(n) ArrayContains search instead of O(1) bool array lookup
**Impact**: Nested loop performance degradation
**Fix**: Use bool[] WantedAntes instead of int[] EffectiveAntes
**Note**: NOT a LINQ violation - code is already manually optimized!
📄 [View Details](issue-010-linq-contains-clause-filter-check.md)

---

## Medium Severity Issues

### [ ] Issue 005: LINQ in MotelyJsonFilterClauseTypes Initialization
**File**: `Motely\filters\MotelyJson\MotelyJsonFilterClauseTypes.cs`
**Lines**: 148-149, 209-212, 336-339, 475-478, 569-573, 696-700, 833-836
**Problem**: Multiple LINQ operations in FromJsonClause and ConvertClauses
**Impact**: Multiple allocations during filter initialization
**Fix**: Replace with manual loops
📄 [View Details](issue-005-linq-where-count-joker-clause-types.md)

### [ ] Issue 009: String.IsNullOrEmpty() in MotelyJsonScoring Hotpath
**File**: `Motely\filters\MotelyJson\MotelyJsonScoring.cs`
**Lines**: 1492, 1542
**Problem**: String operations in scoring hotpath for Mode checking
**Impact**: Unnecessary string comparisons in hotpath
**Fix**: Pre-compute bool flag or use enum instead of string
📄 [View Details](issue-009-string-isnullorempty-in-scoring.md)

### [ ] Issue 001: LINQ .All() in MotelyCompositeFilterDesc Initialization
**File**: `Motely\filters\MotelyJson\MotelyCompositeFilterDesc.cs`
**Line**: 31
**Problem**: `.All()` lambda in CreateFilter (not hotpath but frequent)
**Impact**: Allocation during filter setup
**Fix**: Simple manual loop
📄 [View Details](issue-001-linq-any-composite-filter.md)

---

## Low Severity Issues

### [ ] Issue 011: LINQ in MotelyCompositeFilterDesc Filter Creation
**File**: `Motely\filters\MotelyJson\MotelyCompositeFilterDesc.cs`
**Line**: 31
**Problem**: LINQ in initialization-only code
**Impact**: Minimal - only called during setup
**Fix**: Replace with manual loop for consistency
📄 [View Details](issue-011-linq-where-select-composite-andor.md)

---

## Recommendations by Priority

### Immediate (Critical - Must Fix)
1. **Issue 004** - Tag filter Contains() - This is THE hottest path
2. **Issue 002** - Joker filter Contains() and Any() - Second hottest path
3. **Issue 008** - Scoring Min() - Impacts both filtering and scoring

### High Priority (Should Fix Soon)
4. **Issue 003** - Soul joker Any() - Individual verification hotpath
5. **Issue 006** - Soul joker ToArray() chain - High GC pressure
6. **Issue 007** - Tarot ToArray() - Individual verification allocations
7. **Issue 010** - Array search optimization - Easy win for nested loops

### Medium Priority (Fix When Possible)
8. **Issue 005** - Initialization LINQ chains - Startup performance
9. **Issue 009** - String operations - Use enum or pre-compute
10. **Issue 001** - Composite filter All() - Minor initialization overhead

### Low Priority (Nice to Have)
11. **Issue 011** - Duplicate of Issue 001 (already listed)

---

## Testing Strategy

After fixing each issue:
1. **Unit Tests**: Verify behavior unchanged
2. **Performance Tests**: Benchmark seeds/second improvement
3. **Memory Tests**: Measure GC allocation reduction
4. **Regression Tests**: Run full test suite

---

## Performance Improvement Estimates

| Issue | Expected Speedup | GC Reduction |
|-------|------------------|--------------|
| Issue 004 (Tag Filter) | 15-25% | Low (no allocs) |
| Issue 002 (Joker Filter) | 10-20% | High |
| Issue 008 (Scoring Min) | 5-15% | Medium |
| Issues 003,006,007 | 10-15% combined | Very High |
| All Issues Combined | **30-50%** | **50-70%** |

---

## Notes

- All file paths are relative to `X:\BalatroSeedOracle\external\Motely\Motely\`
- Issues are numbered sequentially for tracking
- Severity based on: location (hotpath vs initialization), frequency (calls/sec), impact (throughput)
- **Critical** = In SIMD hotpath with AggressiveOptimization
- **High** = In individual verification (millions of calls)
- **Medium** = In initialization or less frequent paths
- **Low** = Initialization-only, minimal impact

---

**Generated by**: Claude Code (Sonnet 4.5)
**For**: PIFREAK / optimuspi/motely
**Purpose**: Complete SIMD hotpath performance audit
