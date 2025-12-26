# Shared State for Multiple JAML Instances

## Current State
- **SignalR**: Real-time updates for search progress/results (already working)
- **In-Memory**: Search state stored in `SearchManager` (single instance only)

## Options for Multi-Instance Shared State

### Option 1: Cloudflare KV (Recommended for Cloudflare Pages)
**Pros:**
- Built into Cloudflare (if using Pages/Workers)
- Simple key-value store
- Low latency
- Free tier: 100k reads/day, 1k writes/day

**Implementation:**
```javascript
// In Cloudflare Worker
const searchStatus = await env.SEARCH_STATUS_KV.get(searchId);
await env.SEARCH_STATUS_KV.put(searchId, JSON.stringify(status));
```

**Backend (ASP.NET):**
- Use Cloudflare API to read/write KV
- Or proxy through Cloudflare Worker

### Option 2: Redis (Recommended for Self-Hosted)
**Pros:**
- Fast, in-memory
- Pub/Sub for real-time updates
- Works with existing SignalR
- Can share state across multiple API instances

**Implementation:**
```csharp
// Add StackExchange.Redis
services.AddStackExchangeRedisCache(options => {
    options.Configuration = "localhost:6379";
});

// Store search status
await redis.StringSetAsync($"search:{searchId}", JsonSerializer.Serialize(status));
```

### Option 3: Database (SQLite/PostgreSQL)
**Pros:**
- Persistent
- Already have SQLite (`fertilizer.db`)
- Simple queries

**Implementation:**
- Add `SearchStatus` table
- Update on progress
- Query on page load

## Recommendation
- **For Cloudflare**: Use KV for search status
- **For Self-Hosted**: Use Redis for shared state + SignalR for real-time updates
- **For Single Instance**: Current in-memory is fine

## What to Share
1. **Search Status**: `{ searchId, status, progress, seedsPerSecond, seedsSearched, seedsFound }`
2. **Active Searches**: List of running search IDs
3. **Results**: Cache recent results (optional, SignalR handles real-time)

