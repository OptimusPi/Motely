# Motely

Motely is a fast Balatro seed searching library. It utilizes your CPU's 512-bit registers along with SIMD to search 8 seeds at once per thread.
It performs very well compared to current GPU-based balatro seed searches (better in a lot of systems), and is the fastest general purpose CPU based searcher to my knowledge. 

Thank you so much to [@OptimusPi](https://github.com/OptimusPi/) for commissioning the development of this library. It started out as a personal project, but
would not have the capabilities it has today without his support.

## Quick Start

```bash
# Search with a JSON filter
dotnet run -c Release -- --json telescope-test --threads 16 --cutoff 2

# Use a native filter
dotnet run -c Release -- --native negativecopy --threads 16

# Analyze a specific seed
dotnet run -c Release -- --analyze ABCD1234
```

## CSV Scoring & Filter Chaining

### Native Filter with CSV Output
```bash
# NegativeCopy filter with built-in CSV scoring
dotnet run -c Release -- --native negativecopy --csvScore native --cutoff 7
# Output: Seed,Score,Showman,Blueprint,Brainstorm,Invisible,NegShowman,NegBlueprint,NegBrainstorm,NegInvisible
```

### Chained Filters with JSON Scoring
```bash
# Combine PerkeoObservatory + NegativeCopy filters, score with JSON
dotnet run -c Release -- --native PerkeoObservatory --chain NegativeCopy --score PerkeoObservatory
# Uses PerkeoObservatory.json SHOULD clauses for CSV columns
```

### Reverse Chain for Different Scoring
```bash
# Same filters, but score with NegativeCopy's built-in logic
dotnet run -c Release -- --native negativecopy --chain PerkeoObservatory --csvScore native
```

## Command Line Options

### Core Options
- `-j, --json <FILE>`: JSON config from JsonItemFilters/
- `-n, --native <FILTER>`: Built-in native filter
- `--analyze <SEED>`: Analyze specific seed
- `--chain <FILTERS>`: Chain additional filters (AND logic)
- `--score <JSON>`: Add JSON scoring to native filter
- `--csvScore native`: Enable built-in CSV scoring

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

## Performance Tips
1. Use `--batchSize 4` or higher for better vectorization
2. Soul joker filters work best in SHOULD clauses
3. Redirect stderr to hide progress: `2>/dev/null`
4. Place most restrictive filter first when chaining
