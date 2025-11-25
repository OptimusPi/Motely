# Motely

The fastest Balatro seed searcher with JSON, YAML support and an interactive TUI.

Based on [@tacodiva](https://github.com/tacodiva)'s incredible [Motely](https://github.com/tacodiva/Motely) - a blazing-fast SIMD-powered seed searcher. This fork extends it with multiple configuration formats (JSON, YAML, TOML) and a Terminal User Interface for easy filter creation.

## Quick Start

### Launch the TUI (Terminal User Interface)
```bash
# Launch interactive TUI (default when no arguments provided)
dotnet run -c Release

# Or explicitly specify TUI mode
dotnet run -c Release -- --tui
```

The TUI provides an interactive menu for:
- Building custom filters visually
- Quick search with predefined filters
- Loading config files
- Starting the API server (coming soon)

### Command Line Usage
```bash
# Search with a JSON filter
dotnet run -c Release -- --json PerkeoObservatory --threads 16 --cutoff 2

# Search with a YAML filter
dotnet run -c Release -- --yaml MyFilter --threads 16 --cutoff 2

# Search with a TOML filter
dotnet run -c Release -- --toml CustomSearch --threads 16 --cutoff 2

# Use a native filter
dotnet run -c Release -- --native PerkeoObservatory --threads 16

# Analyze a specific seed
dotnet run -c Release -- --analyze ALEEB
```

## Command Line Options

### Core Options
- `--tui`: Launch Terminal User Interface (default if no args provided)
- `--json <filename>`: JSON config from JsonItemFilters/ (without .json extension)
- `--yaml <filename>`: YAML config from YamlItemFilters/ (without .yaml extension)
- `--toml <filename>`: TOML config from TomlItemFilters/ (without .toml extension)
- `--native <filter name>`: Built-in native filter (without .cs extension)
- `--analyze <SEED>`: Analyze specific seed

### Performance Options
- `--threads <N>`: Thread count (default: CPU cores)
- `--batchSize <1-8>`: Vectorization batch size
- `--startBatch/--endBatch`: Search range control

### Filter Options
- `--cutoff <N|auto>`: Minimum score threshold
- `--deck <DECK>`: Deck selection
- `--stake <STAKE>`: Stake level

## Filter Formats

### JSON Filter Format

Create in `JsonItemFilters/`:
```json
{
  "name": "Example",
  "must": [{
    "type": "Voucher",
    "value": "Telescope",
    "antes": [1, 2, 3]
  }],
  "should": [{
    "type": "Joker",
    "value": "Blueprint",
    "antes": [1, 2, 3, 4],
    "score": 100
  }]
}
```

### YAML Filter Format

Create in `YamlItemFilters/`:
```yaml
name: Example
description: Example filter using YAML
author: YourName
dateCreated: 2025-01-01T00:00:00Z

must:
  - type: Voucher
    value: Telescope
    antes: [1, 2, 3]

should:
  - type: Joker
    value: Blueprint
    antes: [1, 2, 3, 4]
    score: 100
```

### TOML Filter Format

Create in `TomlItemFilters/`:
```toml
name = "Example"
description = "Example filter using TOML"
author = "YourName"
dateCreated = 2025-01-01T00:00:00Z

[[must]]
type = "Voucher"
value = "Telescope"
antes = [1, 2, 3]

[[should]]
type = "Joker"
value = "Blueprint"
antes = [1, 2, 3, 4]
score = 100
```

All three formats support the same filter logic - choose whichever you prefer!

## Native Filters
- `negativecopy`: Showman + copy jokers with negatives
- `PerkeoObservatory`: Telescope/Observatory + soul jokers
- `trickeoglyph`: Cartomancer + Hieroglyph
- `soultest`: Soul joker testing

## Tweak the Batch Size 
1. For the most responsive option, Use `--batchSize 1` to batch by one character count (35^1 = 35 seeds) 
2. Use `--batchSize 2` to batch by two character count (35^2 = 1225 seeds)
3. Use `--batchSize 3` to batch by three character count (35^3 = 42875 seeds)
4. Use `--batchSize 4` to batch by four character count (35^4 = 1500625 seeds)

Above this is senseless and not recommended.
Use a higher batch size for less responsive CLI updates but faster searching!
I like to use --batchSize 2 or maybe 3 usually for a good balance, but I would use --batchSize 4 for overnight searches.