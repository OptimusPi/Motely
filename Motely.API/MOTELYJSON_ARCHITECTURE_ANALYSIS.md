# MotelyJsonConfig Architecture Analysis

## Current State

### What It Is
- **Purpose**: Deserialization target for JAML/JSON filter files
- **Usage**: 
  1. File-based: JAML/JSON → `MotelyJsonConfig` → Filter Descriptors → Filters ✅ (works fine)
  2. Programmatic: C# code → `MotelyJsonConfig` → Filter Descriptors → Filters ❌ (verbose, error-prone)

### Current Programmatic Usage Examples

**MCP Server (McpServer.cs:1187-1219):**
```csharp
config.Should.Add(new MotelyJsonConfig.MotelyJsonFilterClause
{
    Type = "Joker",  // String - no compile-time checking!
    Value = joker,
    Score = 2,
    Antes = new[] { 1, 2, 3 },
    Label = $"{joker} (economy)"
});
```

**TUI Filter Builder (FilterBuilderWindow.cs:678-687):**
```csharp
var config = new MotelyJsonConfig
{
    Name = name,
    Description = "Created with Filter Builder TUI",
    Author = Environment.UserName,
    DateCreated = DateTime.UtcNow,
    Must = _mustItems.Select(ParseDisplayTextToClause).ToList(),
    Should = _shouldItems.Select(ParseDisplayTextToClause).ToList(),
    MustNot = _mustNotItems.Select(ParseDisplayTextToClause).ToList(),
};
// Then serialize to JAML just to save it
```

**Tests (everywhere):**
```csharp
var config = new MotelyJsonConfig
{
    Must = new List<MotelyJsonConfig.MotelyJsonFilterClause>
    {
        new MotelyJsonConfig.MotelyJsonFilterClause
        {
            Type = "Joker",
            Value = "Blueprint",
            Antes = new[] { 1, 2, 3 }
        }
    }
};
```

## Problems

1. **Verbose**: Nested object construction is tedious
2. **String-based types**: `Type = "Joker"` - no compile-time checking, typos fail at runtime
3. **Unnecessary indirection**: Building config objects just to convert to descriptors
4. **Misleading name**: "Json" when it's format-agnostic (JAML/JSON)
5. **No type safety**: Can't use enums directly, must use strings

## Proposed Solutions

### Option 1: Fluent Builder API (Recommended)

Create a type-safe builder that directly creates filter descriptors:

```csharp
// Instead of this:
var config = new MotelyJsonConfig
{
    Must = new List<MotelyJsonConfig.MotelyJsonFilterClause>
    {
        new MotelyJsonConfig.MotelyJsonFilterClause
        {
            Type = "Joker",
            Value = "Blueprint",
            Antes = new[] { 1, 2, 3 }
        }
    }
};

// Do this:
var filterDesc = FilterBuilder.Create()
    .Must()
        .Joker(MotelyJoker.Blueprint)
        .Antes(1, 2, 3)
    .Build();
```

**Pros:**
- Type-safe (enums, not strings)
- Less verbose
- Compile-time checking
- Can still serialize to JAML/JSON if needed
- Direct to filter descriptors (skip config layer)

**Cons:**
- New API to learn
- Still need config for file-based loading

### Option 2: Rename + Keep Current API

Just rename `MotelyJsonConfig` → `JamlConfig` or `MotelyFilterConfig`:

**Pros:**
- Accurate name
- No API changes
- Minimal breaking changes (can use type aliases)

**Cons:**
- Still verbose for programmatic use
- Still string-based types
- Doesn't solve the real problem

### Option 3: Hybrid Approach (Best)

1. **Rename** `MotelyJsonConfig` → `JamlConfig` (accurate name)
2. **Keep** `JamlConfig` for file-based deserialization (JAML/JSON)
3. **Add** fluent builder API for programmatic use
4. **Add** conversion: `FilterBuilder` → `JamlConfig` (for serialization)

**Example:**
```csharp
// Programmatic use (type-safe, fluent)
var builder = FilterBuilder.Create()
    .Must()
        .Joker(MotelyJoker.Blueprint).Antes(1, 2, 3)
        .Voucher(MotelyVoucher.Telescope)
    .Should()
        .Joker(MotelyJoker.Brainstorm).Score(5)
    .Build();

// Can convert to JamlConfig for serialization
var jamlConfig = builder.ToJamlConfig();
var jaml = JamlFormatter.Serialize(jamlConfig);

// Or use directly in search
var search = JsonSearchExecutor.Create(builder, params);
```

## Recommendation

**Option 3 (Hybrid)** because:
1. File-based configs work fine - keep `JamlConfig` for that
2. Programmatic use is painful - add fluent builder
3. Best of both worlds: type-safe programmatic API + flexible file format
4. Can serialize builder results to JAML/JSON when needed

## Implementation Plan

1. **Phase 1**: Rename `MotelyJsonConfig` → `JamlConfig` (with type alias for backward compat)
2. **Phase 2**: Create `FilterBuilder` fluent API
3. **Phase 3**: Add `ToJamlConfig()` conversion method
4. **Phase 4**: Migrate MCP/TUI to use builder API
5. **Phase 5**: Update tests to use builder (optional, can keep config for now)

## Do We Need JSON?

**Answer: No, but it's useful.**

- **JAML is primary**: Human-readable, YAML-based, better for editing
- **JSON is optional**: Can be useful for programmatic generation, but not required
- **Current support**: Both work, but JAML is preferred

**Recommendation**: Keep both formats supported, but document JAML as primary.

