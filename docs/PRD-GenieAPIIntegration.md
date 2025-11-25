# Genie API Integration - Motely Search Backend

## Overview

The Motely API provides **instant seed searching** for balatrogenie.app. No async job tracking, no polling - just send filter JSON, get top 10 results back immediately.

## API Endpoints

**Base URL:** `http://localhost:3141/`

### POST /search - Search Seeds

Searches **1 million random seeds** and returns top 10 results immediately (completes in seconds).

**Request Body:**
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
    { "seed": "ABCD1234", "score": 150 },
    { "seed": "XYZ9876", "score": 142 }
  ]
}
```

**If no results:** `{ "results": [] }`

### POST /analyze - Analyze Single Seed

Get full ante-by-ante breakdown for a specific seed.

**Request Body:**
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

## Integration Pattern

### Simple Search Flow

```javascript
async function searchSeeds(filterJson) {
  const response = await fetch('http://localhost:3141/search', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(filterJson)
  });

  const data = await response.json();

  if (data.results && data.results.length > 0) {
    // Show results to user
    displayResults(data.results);
  } else {
    // No results found - let user search again!
    showNoResultsMessage();
    // Keep search button enabled - they can click again for another 1M seeds
  }
}
```

### No Results = Search Again!

**IMPORTANT:** If no results are found from 1M seeds, the user should be able to **immediately search again** to try another 1M random seeds. Each search tests different random seeds, so multiple searches increase chances of finding matches.

**UI Pattern:**
1. User submits filter
2. Show loading spinner: "🔍 Searching 1,000,000 random seeds..."
3. **If results found:** Display top 10 seeds with scores
4. **If no results:** Show message: "No matches in this batch. Try searching again!" with button enabled
5. User clicks search again → new 1M random seeds tested

## Valid Item Types

```
Joker           - Regular jokers (Blueprint, Mime, Brainstorm, etc)
SoulJoker       - Soul jokers (Perkeo, Canio, Triboulet, Yorick, Chicot, Jimbo)
Voucher         - Vouchers (Overstock, Hone, Clearance Sale, etc)
TarotCard       - Tarot cards
SpectralCard    - Spectral cards
PlanetCard      - Planet cards
PlayingCard     - Playing cards
Boss            - Boss blinds
SmallBlindTag   - Small blind tags
BigBlindTag     - Big blind tags
```

## Filter Structure

```json
{
  "must": [
    // Items that MUST appear (AND logic)
    { "type": "SoulJoker", "value": "Perkeo" }
  ],
  "should": [
    // Items for scoring (OR logic, optional)
    { "type": "Voucher", "value": "Overstock", "score": 10 }
  ],
  "mustnot": [
    // Items that must NOT appear (exclusions)
    { "type": "Boss", "value": "The Serpent" }
  ],
  "deck": "Red",     // Optional, default: Red
  "stake": "White"   // Optional, default: White
}
```

### Filter Clause Options

```json
{
  "type": "ItemType",
  "value": "ItemName",
  "antes": [1, 2, 3],  // Optional: restrict to specific antes
  "score": 10          // Optional: for should clauses (higher = better)
}
```

## Example: Genie Text-to-Filter Integration

### User Input:
```
"Find me Perkeo and Blueprint with Overstock voucher on Red Deck"
```

### Genie Converts To:
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

### Genie Calls API:
```javascript
const response = await fetch('http://localhost:3141/search', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify(generatedFilter)
});

const { results } = await response.json();
```

### Display Results:
```javascript
if (results.length > 0) {
  results.forEach((result, i) => {
    console.log(`#${i+1}: ${result.seed} (Score: ${result.score})`);
  });
} else {
  console.log("No matches! Click 'Search Again' to try another 1M seeds.");
}
```

## Key Points for Genie Integration

1. **Synchronous** - No job IDs, no status polling. Send request, get results immediately.

2. **Random Seeds** - Each search tests 1M random seeds, not sequential. Multiple searches = more coverage.

3. **No Results Is Normal** - If filter is too restrictive, empty results are expected. Solution: search again or relax filter.

4. **CORS Enabled** - Can call directly from browser (balatrogenie.app).

5. **Type Correctness** - Use `SoulJoker` for Perkeo/Canio/etc, NOT `Joker`.

6. **Search Button** - Keep enabled after "no results" so user can immediately try again with different random seeds.

7. **Fast** - Searches complete in seconds due to vectorized SIMD processing.

## Error Handling

```javascript
try {
  const response = await fetch('http://localhost:3141/search', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(filter)
  });

  const data = await response.json();

  if (!response.ok) {
    // API error (400, 500, etc)
    showError(`Search failed: ${data.error}`);
    return;
  }

  if (data.results.length === 0) {
    // No results - normal, let user search again
    showNoResults("No matches in this batch. Search again for more!");
  } else {
    // Success - show results
    displayResults(data.results);
  }
} catch (error) {
  // Network error
  showError(`Connection failed: ${error.message}`);
}
```

## Production Deployment Notes

**Current:** API runs on `localhost:3141`

**For Production:**
- Deploy Motely API on a server (cloud VM, VPS, etc)
- Update balatrogenie.app to call production URL
- Or: Run API locally and use ngrok/cloudflare tunnel for public access

**CORS:** Already enabled for all origins (`*`) - works from any domain.

## Testing

**Test Search:**
```bash
curl -X POST http://localhost:3141/search \
  -H "Content-Type: application/json" \
  -d '{"must":[{"type":"SoulJoker","value":"Perkeo"}]}'
```

**Test Analyze:**
```bash
curl -X POST http://localhost:3141/analyze \
  -H "Content-Type: application/json" \
  -d '{"seed":"9ZXMM1M","deck":"Red","stake":"White"}'
```

**Test Web UI:**
Visit `http://localhost:3141/` in browser for built-in test interface.

## Summary

**Dead simple integration:**
1. Convert user's natural language to filter JSON (Genie does this)
2. POST to `/search` with filter
3. Get results immediately
4. If no results, let user search again (different 1M seeds each time)
5. Display top 10 seeds sorted by score

**No complexity:** No async, no jobs, no polling, no databases. Just simple HTTP POST and JSON response.
