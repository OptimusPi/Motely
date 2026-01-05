# Tech Debt Sprint Plan - Parallel Execution

**Goal**: Complete all tech debt items efficiently using parallel task execution

## 🚀 Execution Strategy

**Phase 1: Quick Wins (Parallel - 2-3 hours total)**
These can all be done simultaneously by different agents:

### Task 1.1: Extract Magic Numbers
**File**: `Motely/filters/MotelyJson/JamlFormatter.cs`
**Lines**: 180, 184
**Action**: 
- Create constants: `MAX_INLINE_STRING_LENGTH = 50`, `MAX_SIMPLE_VALUE_LENGTH = 30`
- Replace hardcoded values
**Time**: 15 minutes
**Agent**: Any

### Task 1.2: Extract Regex Patterns
**Files**: `JamlFormatter.cs`, `JamlConfigLoader.cs`
**Action**: 
- Create `JamlPatterns` static class with regex constants
- Extract: type-as-key pattern, array property pattern, inline array pattern
**Time**: 20 minutes
**Agent**: Any

### Task 1.3: Standardize String Comparisons
**Files**: All files with string comparisons
**Action**: 
- Find all `ToLowerInvariant()` + `==` patterns
- Replace with `StringComparison.OrdinalIgnoreCase`
- Use grep to find: `\.ToLowerInvariant\(\)|\.Equals\(.*true\)|==.*\.ToLower`
**Time**: 30 minutes
**Agent**: Any

### Task 1.4: Remove Commented Debug Code
**Files**: `MotelyJsonSeedScoreDesc.cs`, `MotelyJsonScoring.cs`, `MotelySearch.cs`
**Action**: 
- Search for `// DISABLED FOR PERFORMANCE`
- Search for `#if DEBUG` blocks with commented code
- Delete or convert to proper logging
**Time**: 45 minutes
**Agent**: Any

### Task 1.5: Standardize Null Check Patterns
**Files**: Throughout codebase
**Action**: 
- Create extension methods: `IsNullOrEmpty()`, `SafeCount()`, `SafeLength()`
- Replace `?.Count ?? 0` with `SafeCount()`
- Replace `!= null && .Count > 0` with `!IsNullOrEmpty()`
**Time**: 1 hour
**Agent**: Any

### Task 1.6: Fix Naming Typo Documentation
**Files**: All files
**Action**: 
- Document that `MotleyJsonFilterClause` (typo) vs `MotelyJsonConfig` is intentional or needs fixing
- Add XML comment explaining the discrepancy
**Time**: 10 minutes
**Agent**: Any

---

## 🔧 Phase 2: Refactoring (Parallel - 4-5 hours total)

### Task 2.1: Extract Array Type Conversion Helper
**File**: `JamlTypeAsKeyConverter.cs:218-250`
**Action**: 
- Create `ConvertToArray<T>()` generic helper
- Handles `int[]`, `string[]`, `List<string>` conversion
- Replace 3 similar blocks with calls to helper
**Time**: 45 minutes
**Agent**: Any

### Task 2.2: Extract Antes Propagation Logic
**File**: `MotelyCompositeFilterDesc.cs`
**Action**: 
- Create `PropagateAntesToChildren()` method
- Extract common logic from `CreateAndFilter`, `CreateOrFilter`, `CreateSingleAndFilter`
- All 3 methods call shared helper
**Time**: 1 hour
**Agent**: Any

### Task 2.3: Create Property Type Handler Registry
**File**: `JamlTypeAsKeyConverter.cs`
**Action**: 
- Create `IPropertyTypeHandler` interface
- Create handlers: `StringHandler`, `IntArrayHandler`, `StringArrayHandler`, `StringListHandler`, `IntHandler`, `NullableIntHandler`, `ClausesListHandler`, `SourcesConfigHandler`
- Replace if-else chain with dictionary lookup
**Time**: 2 hours
**Agent**: Any (can split handlers across agents)

### Task 2.4: Improve Error Messages
**File**: `JamlConfigLoader.cs:90-99`
**Action**: 
- Add line number to error messages
- Add context (what was being parsed)
- Add suggestions (common mistakes)
**Time**: 30 minutes
**Agent**: Any

### Task 2.5: Add Edge Case Tests
**File**: `Motely.Tests/FormatConversionTests.cs`
**Action**: 
- Test empty arrays
- Test null values
- Test deeply nested clauses
- Test antes inheritance edge cases
**Time**: 1 hour
**Agent**: Any

---

## 🏗️ Phase 3: Architecture (Sequential - 6-8 hours total)

### Task 3.1: Split MotelyJsonConfig.cs
**Dependencies**: None
**Action**: 
- Extract `SourcesConfig` to separate file
- Extract `ProcessClause` logic to `ClauseProcessor.cs`
- Extract `PostProcess` logic to `ConfigPostProcessor.cs`
- Keep only config definition in main file
**Time**: 2 hours
**Agent**: Any

### Task 3.2: Refactor JamlFormatter Post-Processor
**Dependencies**: Task 1.2 (regex extraction)
**Action**: 
- Break `PostProcess()` into smaller methods:
  - `ProcessTypeAsKeyConversion()`
  - `ProcessArrayInlining()`
  - `ProcessInlineArrayFormatting()`
- Add unit tests for each method
**Time**: 2 hours
**Agent**: Any

### Task 3.3: Optimize Reflection Usage
**Dependencies**: Task 2.3 (property handler registry)
**Action**: 
- Cache `PropertyInfo` lookups in dictionary
- Pre-compute property mappings at startup
- Reduce runtime reflection calls
**Time**: 1.5 hours
**Agent**: Any

### Task 3.4: Add Comprehensive Documentation
**Files**: `MotelyCompositeFilterDesc.cs`, `JamlTypeAsKeyConverter.cs`
**Action**: 
- Document antes inheritance behavior
- Document property type handling
- Add examples to XML docs
**Time**: 1 hour
**Agent**: Any

---

## 📋 Task Assignment Matrix

### Agent 1 (Quick Wins Specialist)
- Task 1.1: Extract Magic Numbers (15 min)
- Task 1.2: Extract Regex Patterns (20 min)
- Task 1.3: Standardize String Comparisons (30 min)
- Task 1.4: Remove Debug Code (45 min)
- **Total**: ~2 hours

### Agent 2 (Refactoring Specialist)
- Task 1.5: Standardize Null Checks (1 hour)
- Task 1.6: Fix Naming Documentation (10 min)
- Task 2.1: Extract Array Conversion (45 min)
- Task 2.2: Extract Antes Logic (1 hour)
- **Total**: ~3 hours

### Agent 3 (Type System Specialist)
- Task 2.3: Property Handler Registry (2 hours)
- Task 2.4: Improve Error Messages (30 min)
- Task 3.3: Optimize Reflection (1.5 hours)
- **Total**: ~4 hours

### Agent 4 (Testing & Documentation)
- Task 2.5: Add Edge Case Tests (1 hour)
- Task 3.4: Add Documentation (1 hour)
- Task 3.1: Split Large Files (2 hours)
- **Total**: ~4 hours

### Agent 5 (Formatter Specialist)
- Task 3.2: Refactor Post-Processor (2 hours)
- Review and test all changes
- **Total**: ~2 hours

---

## ⚡ Parallel Execution Plan

**Hour 1-2**: All Phase 1 tasks run in parallel
- Agent 1: Tasks 1.1, 1.2, 1.3, 1.4
- Agent 2: Tasks 1.5, 1.6
- **Result**: Phase 1 complete

**Hour 2-4**: Phase 2 tasks run in parallel
- Agent 2: Tasks 2.1, 2.2
- Agent 3: Task 2.3 (can split handlers)
- Agent 4: Tasks 2.4, 2.5
- **Result**: Phase 2 complete

**Hour 4-6**: Phase 3 tasks run in parallel
- Agent 4: Task 3.1, 3.4
- Agent 5: Task 3.2
- Agent 3: Task 3.3
- **Result**: Phase 3 complete

**Total Wall Clock Time**: ~6 hours (not 44-68!)

---

## ✅ Acceptance Criteria Per Task

### Quick Wins
- [ ] All magic numbers extracted to named constants
- [ ] All regex patterns in shared class
- [ ] All string comparisons use `StringComparison.OrdinalIgnoreCase`
- [ ] No commented debug code remains
- [ ] Null checks use consistent patterns
- [ ] Naming discrepancy documented

### Refactoring
- [ ] Array conversion uses shared helper
- [ ] Antes logic extracted to single method
- [ ] Property handlers use registry pattern
- [ ] Error messages include line numbers
- [ ] Edge cases have test coverage

### Architecture
- [ ] Large files split into focused modules
- [ ] Post-processor broken into testable methods
- [ ] Reflection calls cached/optimized
- [ ] Complex behaviors documented

---

## 🎯 Success Metrics

- **Code Quality**: Reduced cyclomatic complexity
- **Maintainability**: Smaller, focused files
- **Performance**: Fewer reflection calls
- **Test Coverage**: Edge cases covered
- **Documentation**: Complex behaviors explained

---

## 🚨 Risk Mitigation

1. **Breaking Changes**: Each task includes tests
2. **Merge Conflicts**: Tasks are file-specific
3. **Regression**: Run full test suite after each phase
4. **Time Overruns**: Prioritize Phase 1 & 2 first

---

## 📝 Notes

- Each agent should run tests before committing
- Use feature branches per task
- Merge in phases (Phase 1 → Phase 2 → Phase 3)
- Review PRs quickly to unblock next phase
