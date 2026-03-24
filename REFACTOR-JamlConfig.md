# JamlConfig.cs Refactor Plan

## Problem
`JamlConfig.cs` is 1608 lines of DTO hell. Every new clause type requires changes in 4 places:
1. Add property to `JamlClauseDto` (~40 nullable `string?` / `List<string>?` properties)
2. Add `List<XClause>` to `JamlClauseSet` (23 typed lists)
3. Add parser branch in `JamlConfigLoader` (if/else chain)
4. Add enum to `JamlSchemaGenerator.cs`

## Current structure
- `JamlClauseSet` — 23 separate `List<T>` properties, one per clause type
- `JamlDto` — YamlDotNet deserialization target for the root document
- `JamlClauseDto` — ~40 properties, one per JAML key (joker, commonJoker, voucher, etc.)
- `JamlConfigLoader` (rest of file) — massive parser mapping DTOs → typed clause objects

## Proposed refactor

### 1. Generic clause DTO
Replace 40 nullable properties with:
```csharp
public sealed class JamlClauseDto
{
    public string? Type { get; set; }      // explicit "type" key
    public string? Value { get; set; }     // explicit "value" key
    public int? Score { get; set; }
    public int? Min { get; set; }
    public int? Max { get; set; }
    public string? Label { get; set; }
    public int[]? Antes { get; set; }
    public string? Edition { get; set; }
    public List<string>? Stickers { get; set; }
    // ... shared properties only

    // All type-specific values go through a Dictionary
    [YamlMember(Alias = "")]  // catch-all
    public Dictionary<string, object>? Extra { get; set; }
}
```

Or better: use YamlDotNet's `YamlMappingNode` directly and parse with a registry.

### 2. Clause registry
```csharp
static readonly Dictionary<string, Func<JamlClauseDto, IJamlClause>> ClauseFactory = new()
{
    ["joker"] = dto => new JokerClause { ... },
    ["commonJoker"] = dto => new CommonJokerClause { ... },
    // etc — the schema generator already has these keys
};
```

### 3. Flatten JamlClauseSet
Replace 23 typed lists with one:
```csharp
public sealed class JamlClauseSet : IEnumerable<IJamlClause>
{
    public List<IJamlClause> Clauses { get; } = [];
}
```

The typed access (`set.Jokers`, `set.Vouchers`) can be extension methods or LINQ if needed.

### 4. Schema generator already does it right
`JamlSchemaGenerator.cs` reads enums directly from C# types (`EnumNames<MotelyJoker>()` etc). The parser should use the same enum validation instead of duplicating.

## What NOT to change
- `IJamlClause` interface and the typed clause classes (JokerClause, VoucherClause, etc.) — these are used by the SIMD filter engine
- `JamlSchemaGenerator.cs` — already clean
- The `.jaml` file format itself

## Estimated impact
- `JamlConfig.cs`: ~1600 lines → ~400 lines
- Adding a new clause type: 4 files → 1 file (register in factory + add clause class)
- Zero changes to the filter engine or schema generator
