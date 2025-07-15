# Ouija Configuration System

A clean, KISS-compliant configuration system for Balatro seed searching that bridges user-friendly JSON configs with high-performance enum-based filtering.

## Philosophy

The system follows a simple principle:
- **Users write simple strings** in JSON configs
- **System parses to enums once** during validation
- **Filters use cached enums** for maximum performance

This gives you the best of both worlds: easy-to-write configs and blazing fast searches.

## Quick Start

### Example Config

```json
{
  "maxSearchAnte": 2,
  "deck": "RedDeck",
  "stake": "WhiteStake",
  "needs": [
    {
      "type": "SoulJoker",
      "value": "Perkeo",
      "searchAntes": [1, 2],
      "score": 100
    }
  ],
  "wants": [
    {
      "type": "Joker",
      "value": "Blueprint",
      "edition": "Negative",
      "desireByAnte": 3,
      "score": 10
    }
  ]
}
```

### Running

```bash
Motely.exe -ojson perkeo_hunt.json
```

## Configuration Structure

### Core Properties

- `maxSearchAnte` - How many antes to simulate (1-8)
- `deck` - Starting deck (e.g., "RedDeck", "BlueDeck")
- `stake` - Difficulty stake (e.g., "WhiteStake", "GoldStake")
- `scoreNaturalNegatives` - Track naturally occurring negative jokers
- `scoreDesiredNegatives` - Track negative jokers from your wants list

### Needs (Required Items)

Items that MUST be found for a seed to pass:

```json
"needs": [
  {
    "type": "Joker",
    "value": "Perkeo",
    "desireByAnte": 2,
    "score": 10
  }
]
```

### Wants (Scored Items)

Optional items that add to the seed's score:

```json
"wants": [
  {
    "type": "Planet",
    "value": "Pluto",
    "searchAntes": [1, 2, 3],
    "score": 5
  }
]
```

### Desire Properties

- `type` - Item category (see Types below)
- `value` - Specific item name
- `desireByAnte` - Find by this ante (default: 8)
- `searchAntes` - Array of antes to search (overrides desireByAnte)
- `score` - Points awarded when found
- `edition` - For jokers: "Foil", "Holographic", "Polychrome", "Negative"

## Supported Types

- `Joker` - Regular jokers (Common, Uncommon, Rare)
- `SoulJoker` - Legendary jokers from Soul cards (Perkeo, Chicot, etc.)
- `Planet` - Planet cards
- `Spectral` - Spectral cards
- `Tarot` - Tarot cards
- `Tag` - Boss blind tags
- `Voucher` - Shop vouchers

## How It Works

1. **Load & Validate**: Config is loaded and all string values are parsed to enums
2. **Cache Streams**: PRNG streams for requested antes are cached
3. **Vector Filter**: Thousands of seeds are checked in parallel using SIMD
4. **Individual Scoring**: Seeds that pass all needs are scored based on wants

## Performance Tips

1. **Minimize Antes**: Lower `maxSearchAnte` for faster searches
2. **Use SearchAntes**: Be specific about which antes to check
3. **Order Matters**: Put rarer needs first to filter out more seeds early
4. **SoulJoker**: These require individual seed processing, so they're slower

## Finding Valid Values

To see all valid values for configs, add this to your code:

```csharp
OuijaEnumHelper.PrintAllValidValues();
```

Or search for specific items:

```csharp
OuijaEnumHelper.SearchValues("blue");
```

## Creating Configs Programmatically

```csharp
var config = new OuijaConfig
{
    MaxSearchAnte = 4,
    Needs = new[]
    {
        new OuijaConfig.Desire
        {
            Type = "Tag",
            Value = "NegativeTag",
            DesireByAnte = 3,
            Score = 20
        }
    }
};

OuijaConfigLoader.Save(config, "my_search.json");
```

## Common Patterns

### Perkeo Hunt
```json
{
  "type": "SoulJoker",
  "value": "Perkeo",
  "searchAntes": [1, 2]
}
```

### Negative Tag Strategy
```json
{
  "type": "Tag",
  "value": "NegativeTag",
  "desireByAnte": 3
}
```

### Multi-Ante Planet Search
```json
{
  "type": "Planet",
  "value": "Jupiter",
  "searchAntes": [1, 2, 3, 4]
}
```

## Troubleshooting

- **"Unknown joker"**: Check spelling and capitalization
- **"Missing enum"**: Make sure the value exists (use enum helper)
- **No results**: Try relaxing requirements or increasing ante range
- **Slow search**: Reduce antes or use more common items as needs

## Architecture

The system uses a clean separation:
- `OuijaConfig` - User-facing config with strings
- `OuijaConfig.Desire` - Individual item requirements
- `ValidateAndCache()` - Parses strings to enums once
- `OuijaJsonFilterDesc` - Creates high-performance filter
- Cached enums used in hot paths for speed

This keeps the code simple (KISS) while maintaining excellent performance!