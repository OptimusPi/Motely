# MotelyJSON Rename Analysis

## Current State

- **Class Name**: `MotelyJsonConfig`
- **Namespace**: `Motely.Filters` (class is in `Motely/filters/MotelyJson/`)
- **References**: 987 matches across 53 files
- **Related Classes**: 28 files in `MotelyJson/` directory, all prefixed with `MotelyJson*`

## The Problem

The name `MotelyJsonConfig` is misleading because:
- It's used for **both** JSON and JAML formats
- The class comment says "MongoDB compound Operator-style JSON configuration" (also misleading - it's not MongoDB-specific)
- The `MotelyJson/` namespace suggests JSON-only, but JAML is the primary format

## Proposed Solution

### Option 1: Rename to `MotelyFilterConfig` (Recommended)
- **Class**: `MotelyJsonConfig` → `MotelyFilterConfig`
- **Namespace**: Keep `Motely.Filters` (already correct)
- **Directory**: Could rename `MotelyJson/` → `FilterConfig/` or keep as-is
- **Related Classes**: Rename `MotelyJson*` → `FilterConfig*` (e.g., `MotelyJsonConfigValidator` → `FilterConfigValidator`)

**Pros:**
- Accurate - it's a filter configuration, not JSON-specific
- Shorter, cleaner name
- Still clear what it does

**Cons:**
- Massive refactor (987 references)
- Breaking change for any external code
- All tests need updating
- Documentation needs updating

### Option 2: Keep Name, Fix Comments
- Keep `MotelyJsonConfig` name (for backward compatibility)
- Update XML comments to clarify it supports both JSON and JAML
- Update class comment to remove "MongoDB" reference

**Pros:**
- No code changes needed
- No breaking changes
- Quick fix

**Cons:**
- Name still misleading
- Doesn't solve the fundamental naming issue

## Recommendation

**Option 2 (Fix Comments)** for now, because:
1. The rename is a massive breaking change (987 references)
2. The name, while misleading, is established and works
3. Comments/documentation can clarify the actual usage
4. Can do the rename later if needed, as a separate focused effort

If we do rename, use `MotelyFilterConfig` - it's accurate and cleaner.

## Impact if We Rename

- **Files to modify**: 53 files
- **References to update**: 987 instances
- **Test files**: ~15 test files
- **Breaking changes**: Yes - any external code using the API
- **Estimated effort**: 2-4 hours of careful find/replace + testing

