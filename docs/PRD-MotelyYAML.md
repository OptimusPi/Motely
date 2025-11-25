# MotelyYAML - Product Requirements Document

## Overview
Support YAML format for filter configurations. YAML is the most human-friendly config format, widely used in DevOps, and great for complex nested structures.

## Why YAML?
```yaml
# Find Perkeo with Observatory
name: Perkeo Observatory

must:
  - type: SoulJoker
    value: Perkeo
    antes: [1, 2, 3]

  - type: Voucher
    value: Observatory
    antes: [1, 2]
```

**YAML advantages:**
- ✅ Most readable (minimal syntax noise)
- ✅ Native comments
- ✅ Multi-line strings
- ✅ Anchors/aliases for DRY configs
- ✅ Industry standard (Kubernetes, Docker Compose, GitHub Actions)

**YAML gotchas:**
- ❌ Indentation-sensitive (use spaces, not tabs!)
- ❌ Can be ambiguous (quotes needed sometimes)
- ❌ Slower to parse than JSON/TOML

## Target Outcome
Users can write configs in YAML and run:
```bash
dotnet run -c CLI -- --yaml PerkeoObservatory --threads 16
```

## Implementation Tasks

### Phase 1: YAML Parser Integration (Non-Breaking)
- [ ] **Task 1.1**: Research YAML libraries
  - Primary: `YamlDotNet` (https://github.com/aaubry/YamlDotNet)
  - Alternative: `SharpYaml` (https://github.com/xoofx/SharpYaml)
- [ ] **Task 1.2**: Add YamlDotNet NuGet package to Motely project
- [ ] **Task 1.3**: Create `YamlItemFilters/` directory
- [ ] **Task 1.4**: Test YamlDotNet deserialization with sample YAML

### Phase 2: YAML Config Loader (Non-Breaking)
- [ ] **Task 2.1**: Create `MotelyYamlConfig.cs` class (mirrors MotelyJsonConfig)
- [ ] **Task 2.2**: Implement `TryLoadFromYamlFile(string path, out config, out error)`
- [ ] **Task 2.3**: Add YAML → MotelyJsonConfig converter
- [ ] **Task 2.4**: Handle YAML-specific types (anchors, aliases)
- [ ] **Task 2.5**: Write unit tests for YAML parsing

### Phase 3: CLI Integration (Non-Breaking)
- [ ] **Task 3.1**: Add `--yaml <filename>` CLI parameter
- [ ] **Task 3.2**: Update config loader to check `YamlItemFilters/` directory
- [ ] **Task 3.3**: Add YAML validation with line number errors
- [ ] **Task 3.4**: Update `--help` documentation

### Phase 4: YAML-Specific Features (Non-Breaking)
- [ ] **Task 4.1**: Support YAML anchors for reusable config blocks
- [ ] **Task 4.2**: Support multi-line descriptions/comments
- [ ] **Task 4.3**: Create YAML templates for common patterns
- [ ] **Task 4.4**: JSON → YAML converter utility

### Phase 5: Migration & Examples (Non-Breaking)
- [ ] **Task 5.1**: Convert 5 popular JSON configs to YAML
- [ ] **Task 5.2**: Create advanced YAML examples (using anchors)
- [ ] **Task 5.3**: Update README with YAML quickstart
- [ ] **Task 5.4**: YAML schema for IDE autocompletion

### Phase 6: Testing & Polish (Non-Breaking)
- [ ] **Task 6.1**: Integration tests for all filter types in YAML
- [ ] **Task 6.2**: Performance testing (YAML vs JSON vs TOML)
- [ ] **Task 6.3**: Error message improvements
- [ ] **Task 6.4**: Documentation & migration guide

## YAML Example Configs

### Simple Config
```yaml
# YamlItemFilters/simple-blueprint.yaml
name: Blueprint Ante 1-3

must:
  - type: Joker
    value: Blueprint
    antes: [1, 2, 3]
```

### Using YAML Anchors (DRY)
```yaml
# YamlItemFilters/advanced-combo.yaml
name: Reusable Ante Config

# Define reusable blocks
common_antes: &early_game [1, 2, 3]
common_shop: &shop_slots [0, 1, 2, 3, 4]

must:
  - type: Joker
    value: Blueprint
    antes: *early_game  # Reuse anchor
    sources:
      shop_slots: *shop_slots

  - type: Joker
    value: Brainstorm
    antes: *early_game
    sources:
      shop_slots: *shop_slots
```

### Complex Nested Structure
```yaml
# YamlItemFilters/soul-joker-combo.yaml
name: Advanced Soul Joker Search
description: |
  This config searches for Perkeo soul joker
  with specific edition requirements across
  multiple antes. Multi-line description!

must:
  - type: SoulJoker
    value: Perkeo
    antes: [1, 2]
    edition: Negative
    sources:
      shop_slots: [0, 1, 2]

  - type: Voucher
    value: Observatory
    antes: [1, 2]

should:
  - type: Joker
    value: Blueprint
    antes: [2, 3]

search_params:
  deck: Red
  stake: White
  threads: 16
```

### Template with Comments
```yaml
# YamlItemFilters/template.yaml
# Complete YAML config template
# Copy and customize!

name: Your Search Name

# Required conditions (ALL must match)
must:
  - type: Joker  # Joker | SoulJoker | Voucher | PlanetCard | TarotCard | SpectralCard | Boss | Tag
    value: Blueprint  # See docs for valid values
    antes: [1, 2, 3]  # Which antes to check (1-8)
    edition: Any  # Any | Negative | Polychrome | Holographic | Foil
    sources:
      shop_slots: [0, 1, 2]  # Which shop slots (0-7)
      pack_slots: [0, 1, 2]  # Which pack slots (0-5)

# Optional conditions (at least one should match)
should:
  - type: Joker
    value: Brainstorm
    antes: [2, 3]

# Forbidden conditions (none must match)
must_not:
  - type: Boss
    value: TheWheel
    antes: [1]

# Search parameters
search_params:
  deck: Red  # Red | Blue | Yellow | Green | etc
  stake: White  # White | Red | Green | etc
  threads: 16
  max_results: 100
```

## Library Comparison

### YamlDotNet (Recommended)
```bash
dotnet add package YamlDotNet
```
**Pros:**
- Most mature .NET YAML library
- Full YAML 1.2 support
- Excellent anchor/alias support
- Good performance
- Active development

**Cons:**
- Larger package size
- More complex API

### SharpYaml (Alternative)
```bash
dotnet add package SharpYaml
```
**Pros:**
- Lighter weight
- Simpler API
- Good for basic YAML

**Cons:**
- Less feature-complete
- Slower development

## Success Metrics
- Parse any valid YAML config → MotelyJsonConfig in < 15ms
- Support all YAML 1.2 features (anchors, aliases, multi-line)
- Zero indentation errors with good error messages
- Users can write configs 50% faster than JSON

## Backward Compatibility
- ✅ JSON configs still work
- ✅ TOML configs still work
- ✅ YAML is purely additive
- ✅ All formats can coexist

## Format Recommendation Matrix

| Format | Best For | Difficulty | Features |
|--------|----------|------------|----------|
| **JSON** | Programmatic generation | Easy | Fast parsing |
| **TOML** | Hand-editing configs | Medium | Comments, readable |
| **YAML** | Complex nested configs | Hard | Anchors, multi-line |

**Recommendation:**
- Beginners → JSON (familiar, tools)
- Power users → TOML (readable, fast)
- Advanced users → YAML (DRY, flexible)

---

**YAML for the win! 🎯**
