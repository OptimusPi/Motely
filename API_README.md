# Motely API Server

REST API server for running Balatro seed searches and analysis remotely.

## Starting the Server

1. Launch Motely TUI
2. Select "Start API Server" from the main menu
3. Server will start on `http://localhost:3141/`

The TUI window shows:
- Server status (Running/Stopped)
- Server URL
- Request log with timestamps
- Press ESC or click "Stop Server" to shut down

**Web UI:** Visit `http://localhost:3141/` in your browser for a built-in test interface with search and analyze forms.

## API Endpoints

### Search for Seeds
```http
POST /search
Content-Type: application/json
```

Submit a filter (JSON with must/should/mustnot clauses) and get results immediately from 1 million random seeds.

**Request:**
```json
{
  "must": [
    { "type": "SoulJoker", "value": "Perkeo" },
    { "type": "Joker", "value": "Blueprint" }
  ],
  "should": [
    { "type": "Voucher", "value": "Overstock", "score": 10 }
  ],
  "deck": "Red",
  "stake": "White"
}
```

**Response:**
```json
{
  "results": [
    {
      "seed": "ABCD1234",
      "score": 150
    },
    {
      "seed": "XYZ9876",
      "score": 142
    }
  ]
}
```

Returns top 10 results sorted by score (descending). If no results found, returns empty array.

### Analyze a Seed
```http
POST /analyze
Content-Type: application/json
```

Analyze a specific seed to see shop queue, packs, bosses, tags for all antes.

**Request:**
```json
{
  "seed": "ABCD1234",
  "deck": "Red",
  "stake": "White"
}
```

**Response:**
```json
{
  "seed": "ABCD1234",
  "deck": "Red",
  "stake": "White",
  "analysis": "==ANTE 1==\nBoss: The Serpent\n..."
}
```

The analysis field contains formatted text with all seed details.

## Usage Examples

### curl

```bash
# Search for seeds
curl -X POST http://localhost:3141/search \
  -H "Content-Type: application/json" \
  -d '{
    "must": [
      {"type": "SoulJoker", "value": "Perkeo"}
    ]
  }'

# Analyze a seed
curl -X POST http://localhost:3141/analyze \
  -H "Content-Type: application/json" \
  -d '{
    "seed": "9ZXMM1M",
    "deck": "Red",
    "stake": "White"
  }'
```

### JavaScript

```javascript
// Search for seeds
const searchResponse = await fetch('http://localhost:3141/search', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    must: [
      { type: 'SoulJoker', value: 'Perkeo' },
      { type: 'Voucher', value: 'Overstock' }
    ],
    deck: 'Red',
    stake: 'White'
  })
});

const { results } = await searchResponse.json();
console.log('Top results:', results);

// Analyze a seed
const analyzeResponse = await fetch('http://localhost:3141/analyze', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    seed: '9ZXMM1M',
    deck: 'Red',
    stake: 'White'
  })
});

const analysis = await analyzeResponse.json();
console.log(analysis.analysis);
```

## Filter Format

The filter JSON format matches Motely's JSON filter files:

- **must**: Array of items that MUST appear (ANDed together)
- **should**: Array of items for scoring (ORed together)
- **mustNot**: Array of items that must NOT appear
- **deck**: Deck name (default: "Red")
- **stake**: Stake name (default: "White")

Each filter clause:
```json
{
  "type": "Joker|SoulJoker|Voucher|TarotCard|SpectralCard|PlanetCard|PlayingCard|Boss|SmallBlindTag|BigBlindTag",
  "value": "ItemName",
  "antes": [1, 2, 3],
  "score": 10
}
```

## Features

- **Synchronous**: Searches complete in seconds, results returned immediately
- **CORS Enabled**: Can be called from web browsers
- **No Job Management**: No need to poll for results, everything is synchronous
- **1M Random Seeds**: Each search tests 1 million random seeds
- **Top 10 Results**: Returns best 10 matches sorted by score

## Technical Details

- Built with `System.Net.HttpListener` (no external dependencies)
- Uses `JsonSearchExecutor` for search execution
- Uses `MotelySeedAnalyzer` for seed analysis
- Thread-safe for concurrent API requests
- Automatically redirects console output to capture results
