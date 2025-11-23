# MotelyJSON

The fastest Balatro seed searcher, now with JSON filter support.

Based on [@tacodiva](https://github.com/tacodiva)'s incredible [Motely](https://github.com/tacodiva/Motely) - a blazing-fast SIMD-powered seed searcher. This fork extends it with JSON configuration support for easy filter creation.

## Quick Start

```bash
# Search with a JSON filter
dotnet run -c Release -- --json PerkeoObservatory --threads 16 --cutoff 2

# Use a native filter
dotnet run -c Release -- --native PerkeoObservatory --threads 16

# Analyze a specific seed
dotnet run -c Release -- --analyze ALEEB
```

## Command Line Options

### Core Options
- `--json <filename without .json extension>`: JSON config from JsonItemFilters/
- `--native <filter name without .cs extension>`: Built-in native filter
- `--analyze <SEED>`: Analyze specific seed

### Performance Options
- `--threads <N>`: Thread count (default: CPU cores)
- `--batchSize <1-8>`: Vectorization batch size
- `--startBatch/--endBatch`: Search range control

### Filter Options
- `--cutoff <N|auto>`: Minimum score threshold
- `--deck <DECK>`: Deck selection
- `--stake <STAKE>`: Stake level

## JSON Filter Format

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
    "antes": [1, 2, 3, 4]
  }]
}
```

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