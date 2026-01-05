# Technical Debt Inventory

Generated: 2026-01-04

## 🔴 High Priority

### 1. **JamlTypeAsKeyConverter Property Type Handling**
**Location**: `Motely/filters/MotelyJson/JamlTypeAsKeyConverter.cs:214-282`
**Issue**: Massive if-else chain for property type handling (string, int[], string[], List<string>, int, int?, List<MotelyJsonFilterClause>, SourcesConfig)
**Impact**: Hard to maintain, easy to miss new types, error-prone
**Solution**: Refactor to use strategy pattern or dictionary-based type handlers
**Effort**: Medium (2-3 hours)

### 2. **JamlFormatter Post-Processor Complexity**
**Location**: `Motely/filters/MotelyJson/JamlFormatter.cs:101-214`
**Issue**: Complex regex-based line-by-line parsing for array inlining and type-as-key conversion
**Impact**: Fragile, hard to debug, potential edge cases with indentation/formatting
**Solution**: Consider using YAML AST manipulation or more robust parsing library
**Effort**: High (4-6 hours)

### 3. **Antes Inheritance Logic Duplication**
**Location**: `Motely/filters/MotelyJson/MotelyCompositeFilterDesc.cs`
**Issue**: Similar logic repeated in `CreateAndFilter`, `CreateOrFilter`, `CreateSingleAndFilter` for antes propagation
**Impact**: Maintenance burden, bugs can be fixed in one but not the other
**Solution**: Extract common antes propagation logic to shared method
**Effort**: Medium (2-3 hours)

### 4. **Reflection-Based Property Setting**
**Location**: `Motely/filters/MotelyJson/JamlTypeAsKeyConverter.cs:213-283`
**Issue**: Heavy use of reflection (`FindPropertyWithAlias`, `SetValue`) for dynamic property assignment
**Impact**: Performance overhead, runtime errors instead of compile-time safety
**Solution**: Consider code generation or strongly-typed property mapping
**Effort**: High (6-8 hours)

## 🟡 Medium Priority

### 5. **Commented-Out Debug Code**
**Location**: Multiple files (MotelyJsonSeedScoreDesc.cs, MotelyJsonScoring.cs, MotelySearch.cs)
**Issue**: Many `// DISABLED FOR PERFORMANCE` and `#if DEBUG` blocks with commented code
**Impact**: Code clutter, confusion about what's active
**Solution**: Remove or use proper logging framework with log levels
**Effort**: Low (1-2 hours)

### 6. **Null Check Inconsistencies**
**Location**: Throughout codebase
**Issue**: Mix of `== null`, `?.Count ?? 0`, `?.Length ?? 0`, `!= null && .Count > 0` patterns
**Impact**: Inconsistent code style, potential null reference bugs
**Solution**: Standardize null-checking patterns, consider nullable reference types
**Effort**: Medium (3-4 hours)

### 7. **Error Message Quality**
**Location**: `Motely/JamlConfigLoader.cs:90-99`, `Motely/ConfigFormatConverter.cs`
**Issue**: Some error messages lack context or actionable hints
**Impact**: Poor developer experience when debugging JAML parsing issues
**Solution**: Enhance error messages with line numbers, context, suggestions
**Effort**: Low (2-3 hours)

### 8. **Test File Cleanup**
**Location**: `Motely.Tests/`
**Issue**: Debug test files created and deleted (StickerDebugTest.cs, StickerSimpleTest.cs) - useful patterns might be lost
**Impact**: Lost test coverage, potential regression
**Solution**: Review and preserve useful test patterns in permanent test files
**Effort**: Low (1 hour)

### 9. **String Comparison Inconsistencies**
**Location**: Throughout codebase
**Issue**: Mix of `StringComparison.OrdinalIgnoreCase`, `ToLowerInvariant()`, direct `==` comparisons
**Impact**: Potential case-sensitivity bugs, performance overhead
**Solution**: Standardize on `StringComparison.OrdinalIgnoreCase` for case-insensitive comparisons
**Effort**: Low (2 hours)

### 10. **Magic Numbers and Hardcoded Values**
**Location**: Multiple files
**Issue**: Hardcoded values like `itemValue.Length < 50`, `itemValue.Length < 30` in post-processor
**Impact**: Unclear intent, hard to maintain
**Solution**: Extract to named constants with documentation
**Effort**: Low (1 hour)

## 🟢 Low Priority

### 11. **Documentation Gaps**
**Location**: `Motely/filters/MotelyJson/MotelyCompositeFilterDesc.cs`
**Issue**: Complex antes inheritance behavior not fully documented
**Impact**: Hard for new developers to understand
**Solution**: Add comprehensive XML docs explaining antes propagation
**Effort**: Low (1-2 hours)

### 12. **Type Safety with Object Casting**
**Location**: `JamlTypeAsKeyConverter.cs:221-247`
**Issue**: Heavy use of `object[]`, `System.Collections.IList`, `Cast<object>()` with runtime type checks
**Impact**: Runtime errors instead of compile-time safety
**Solution**: Use generics or discriminated unions where possible
**Effort**: Medium (3-4 hours)

### 13. **Regex Pattern Duplication**
**Location**: `JamlFormatter.cs`, `JamlConfigLoader.cs`
**Issue**: Similar regex patterns for type-as-key matching in multiple places
**Impact**: Maintenance burden, potential inconsistencies
**Solution**: Extract to shared constants or utility class
**Effort**: Low (1 hour)

### 14. **Performance: LINQ in Hot Paths**
**Location**: `MotelyJsonFilterClauseTypes.cs:402-414`
**Issue**: `OrderByDescending` with complex lambda in scoring path
**Impact**: Potential performance impact on large clause lists
**Solution**: Profile and optimize if needed, consider pre-sorting
**Effort**: Low (investigation first)

### 15. **Code Organization: Large Files**
**Location**: `MotelyJsonConfig.cs` (1387 lines), `MotelyCompositeFilterDesc.cs` (443 lines), `JamlTypeAsKeyConverter.cs` (305 lines)
**Issue**: Very large files with multiple responsibilities
**Impact**: Hard to navigate, merge conflicts
**Solution**: Consider splitting into smaller, focused files (e.g., separate SourcesConfig, ProcessClause logic)
**Effort**: Medium (4-6 hours)

### 16. **Inconsistent Naming Conventions**
**Location**: Throughout codebase
**Issue**: Mix of `MotelyJsonConfig` vs `MotleyJsonFilterClause` (typo in "Motley")
**Impact**: Confusion, potential bugs
**Solution**: Standardize naming (consider fixing typo if not breaking)
**Effort**: Low (1 hour, but breaking change risk)

### 17. **Missing Unit Tests for Edge Cases**
**Location**: Format conversion, antes inheritance
**Issue**: Some edge cases not covered (empty arrays, null values, nested clauses)
**Impact**: Potential bugs in edge cases
**Solution**: Add comprehensive edge case tests
**Effort**: Medium (3-4 hours)

### 18. **YAML Anchor Support Limitations**
**Location**: `JamlConfigLoader.cs`
**Issue**: YAML merge keys (`<<:`) not supported, limiting anchor usefulness
**Impact**: Users can't use full YAML anchor features
**Solution**: Implement merge key support in custom deserializer
**Effort**: High (6-8 hours)

### 19. **Error Recovery in JAML Parsing**
**Location**: `JamlConfigLoader.cs`
**Issue**: Single parse error fails entire file load
**Impact**: Poor UX when JAML has minor issues
**Solution**: Consider partial parsing or better error recovery
**Effort**: Medium (4-5 hours)

### 20. **Code Duplication: Array Type Handling**
**Location**: `JamlTypeAsKeyConverter.cs:218-250`
**Issue**: Similar logic for `int[]`, `string[]`, `List<string>` conversion
**Impact**: Maintenance burden
**Solution**: Extract to generic helper method
**Effort**: Low (1-2 hours)

## 📊 Summary

- **High Priority**: 4 items (estimated 14-20 hours)
- **Medium Priority**: 6 items (estimated 12-18 hours)
- **Low Priority**: 10 items (estimated 18-30 hours)
- **Total Estimated Effort**: 44-68 hours

## 🎯 Recommended Sprint Plan

**Sprint 1 (High Priority)**
1. Extract antes inheritance logic (#3)
2. Refactor JamlTypeAsKeyConverter property handling (#1)
3. Improve error messages (#7)

**Sprint 2 (Medium Priority)**
1. Clean up debug code (#5)
2. Standardize null checks (#6)
3. Add edge case tests (#17)

**Sprint 3 (Polish)**
1. Extract magic numbers (#10)
2. Standardize string comparisons (#9)
3. Improve documentation (#11)
