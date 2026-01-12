# MotelyRunConfig Migration Map

## Current Architecture (BEFORE)

```
JAML Text
    │
    ▼
┌─────────────────────────────────────────────────────────────────┐
│  MotelyJsonConfig (DTO + Runtime - MIXED CONCERNS!)             │
│  ├── string? Name, Author, Description                          │
│  ├── string? Deck, Stake                                        │
│  ├── List<MotelyJsonFilterClause>? Must/Should/MustNot          │
│  │       ├── string Type  ← STRING! Parsed at runtime           │
│  │       ├── string? Value ← STRING! Nullable!                  │
│  │       ├── int[]? Antes ← Nullable!                           │
│  │       ├── MotelyJoker? JokerEnum ← Nullable!                 │
│  │       └── ... 50+ nullable fields                            │
│  └── Pre-computed: MustVouchers[], MaxVoucherAnte, etc.         │
└─────────────────────────────────────────────────────────────────┘
    │
    ▼ (Direct usage - null checks EVERYWHERE)
┌─────────────────────────────────────────────────────────────────┐
│  Filter/Scoring Code                                            │
│  ├── MotelyJsonSeedScoreDesc (config.Must?.Count > 0)           │
│  ├── MotelyJsonScoring (clause.Type?.ToLower() == "joker")      │
│  ├── MotelyCompositeFilterDesc                                  │
│  └── ... 71 files with null checks                              │
└─────────────────────────────────────────────────────────────────┘
```

## Target Architecture (AFTER)

```
JAML Text
    │
    ▼
┌─────────────────────────────────────────────────────────────────┐
│  MotelyJamlDto (PARSING ONLY - nullable is OK here)             │
│  ├── Deserialize from YAML                                      │
│  ├── PostProcess() - validate & parse enums                     │
│  └── ToRunConfig() - convert to typed runtime config            │
└─────────────────────────────────────────────────────────────────┘
    │
    ▼ ToRunConfig()
┌─────────────────────────────────────────────────────────────────┐
│  MotelyRunConfig (RUNTIME - NO NULLABLES!)                      │
│  ├── string Name, Author, Description (never null, "" default)  │
│  ├── string Deck, Stake (never null)                            │
│  ├── MotelyRunClause[] Must/Should/MustNot (never null, [])     │
│  │       ├── MotelyFilterItemType ItemType ← ENUM! Not string!  │
│  │       ├── int[] Antes ← Never null!                          │
│  │       ├── MotelyJoker Joker ← Value type, check ItemType     │
│  │       ├── MotelyRunSources Sources ← Never null!             │
│  │       └── ... all fields non-nullable                        │
│  └── Pre-computed: MustVouchers[], MaxVoucherAnte, etc.         │
└─────────────────────────────────────────────────────────────────┘
    │
    ▼ (No null checks needed - use Debug.Assert for invariants)
┌─────────────────────────────────────────────────────────────────┐
│  Filter/Scoring Code                                            │
│  ├── MotelyJsonSeedScoreDesc → MotelyRunSeedScoreDesc           │
│  ├── MotelyJsonScoring → MotelyRunScoring                       │
│  ├── switch (clause.ItemType) { case Joker: ... }               │
│  └── No null checks! Just Debug.Assert for invariants           │
└─────────────────────────────────────────────────────────────────┘
```

## Files to Migrate (Priority Order)

### Phase 1: Core Filter/Scoring (HIGH IMPACT)
| File | References | Notes |
|------|------------|-------|
| `MotelyJsonSeedScoreDesc.cs` | 2 | Main scoring entry point |
| `MotelyJsonScoring.cs` | 20 | Scoring logic |
| `MotelyCompositeFilterDesc.cs` | 16 | Filter composition |
| `MotelyJsonFilterClauseTypes.cs` | 18 | Clause type handling |
| `JsonSearchExecutor.cs` | 22 | Search execution |

### Phase 2: API/Entry Points
| File | References | Notes |
|------|------------|-------|
| `McpServer.cs` | 13 | MCP API |
| `SearchService.cs` | 3 | API search service |
| `SearchRequest.cs` | 2 | API models |
| `JamlConfigLoader.cs` | 6 | JAML loading |

### Phase 3: Supporting Code
| File | References | Notes |
|------|------------|-------|
| `MotelyJsonConfigValidator.cs` | 6 | Validation |
| `JamlFormatter.cs` | 7 | Formatting |
| `ConfigFormatConverter.cs` | 7 | Format conversion |
| `FilterCategoryMapper.cs` | 5 | Category mapping |

### Phase 4: Tests (40+ files)
| Pattern | Count | Notes |
|---------|-------|-------|
| `*Tests.cs` | 40+ | Unit tests |

## Migration Strategy

### Option A: Big Bang (NOT RECOMMENDED)
- Change everything at once
- High risk, hard to debug
- All tests break simultaneously

### Option B: Gradual Migration (RECOMMENDED)
1. Keep `MotelyJsonConfig` as-is (rename to `MotelyJamlDto` later)
2. Add `MotelyRunConfig` (DONE ✅)
3. Add `ToRunConfig()` conversion (DONE ✅)
4. Update ONE consumer at a time:
   - `MotelyJsonSeedScoreDesc` → accept both, prefer `MotelyRunConfig`
   - Run tests, fix issues
   - Repeat for next file
5. Once all consumers use `MotelyRunConfig`, delete old code

### Option C: Adapter Pattern
1. Create `IMotelyConfig` interface
2. Both `MotelyJsonConfig` and `MotelyRunConfig` implement it
3. Migrate consumers to use interface
4. Swap implementation

## Current Status

✅ **DONE:**
- `MotelyRunConfig.cs` created
- `MotelyRunClause` with typed enums
- `MotelyRunSources` with typed fields
- `ToRunConfig()` conversion method
- `Type` made `required` on `MotelyJsonFilterClause`
- Fail-fast validation in `ProcessClause()`

⏳ **TODO:**
- Migrate `MotelyJsonSeedScoreDesc` to use `MotelyRunConfig`
- Migrate `MotelyJsonScoring` to use `MotelyRunClause`
- Update tests
- Rename `MotelyJsonConfig` → `MotelyJamlDto`
- Delete JSON-specific code (only JAML used now)

## Quick Reference: Type Mappings

| Old (Nullable) | New (Non-Nullable) |
|----------------|-------------------|
| `string? Type` | `MotelyFilterItemType ItemType` |
| `string? Value` | Check `ItemType`, use typed field |
| `MotelyJoker? JokerEnum` | `MotelyJoker Joker` (check `ItemType == Joker`) |
| `int[]? Antes` | `int[] Antes` (never null, may be empty) |
| `SourcesConfig? Sources` | `MotelyRunSources Sources` (never null) |
| `List<Clause>? Clauses` | `MotelyRunClause[] NestedClauses` |

## Code Example: Before vs After

### BEFORE (null checks everywhere):
```csharp
if (clause.Type?.ToLower() == "joker" && clause.JokerEnum.HasValue)
{
    var joker = clause.JokerEnum.Value;
    var antes = clause.Antes ?? Array.Empty<int>();
    // ...
}
```

### AFTER (typed, no null checks):
```csharp
Debug.Assert(clause.ItemType == MotelyFilterItemType.Joker);
var joker = clause.Joker;  // Always valid when ItemType == Joker
var antes = clause.Antes;  // Never null
// ...
```
