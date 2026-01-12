# Cloudflare Vectorize Implementation Plan

## Overview
Add vector similarity search to enable "find similar seeds" and "filter recommendations" features.

## Use Cases

1. **Seed Similarity**: "Find seeds similar to this one"
   - Embed seed metadata (jokers, antes, scores)
   - Find seeds with similar joker combinations

2. **Filter Recommendations**: "What filters are like this?"
   - Embed filter descriptions
   - Recommend similar filters based on user history

3. **Joker Combinations**: "What jokers work well together?"
   - Embed joker synergy data
   - Find complementary jokers

4. **Natural Language Search**: "Find seeds with early economy"
   - Embed seed descriptions
   - Semantic search for natural language queries

## Implementation Steps

### Step 1: Create Vectorize Index

**File**: `Motely.API/Services/VectorizeService.cs`

```csharp
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Motely.API.Services;

public class VectorizeService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<VectorizeService> _logger;
    private readonly string _apiToken;
    private readonly string _accountId;
    private readonly string _indexName;

    public VectorizeService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<VectorizeService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        
        var vectorizeConfig = configuration.GetSection("Cloudflare:Vectorize");
        _apiToken = vectorizeConfig["ApiToken"] ?? throw new InvalidOperationException("Vectorize API token not configured");
        _accountId = vectorizeConfig["AccountId"] ?? throw new InvalidOperationException("Account ID not configured");
        _indexName = vectorizeConfig["IndexName"] ?? "balatro-seeds";
        
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiToken}");
    }

    /// <summary>
    /// Create Vectorize index (one-time setup).
    /// </summary>
    public async Task CreateIndexAsync(int dimensions = 384)
    {
        var request = new
        {
            name = _indexName,
            dimensions = dimensions,
            metric = "cosine" // or "euclidean", "dot-product"
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"https://api.cloudflare.com/client/v4/accounts/{_accountId}/vectorize/indexes",
            request);
        
        response.EnsureSuccessStatusCode();
        _logger.LogInformation("Created Vectorize index: {IndexName}", _indexName);
    }

    /// <summary>
    /// Insert seed metadata as vector.
    /// </summary>
    public async Task InsertSeedVectorAsync(string seedId, float[] embedding, Dictionary<string, object> metadata)
    {
        var request = new
        {
            id = seedId,
            values = embedding,
            metadata = metadata
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"https://api.cloudflare.com/client/v4/accounts/{_accountId}/vectorize/indexes/{_indexName}/insert",
            request);
        
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Query similar seeds.
    /// </summary>
    public async Task<List<SimilarSeed>> QuerySimilarSeedsAsync(float[] queryVector, int topK = 10)
    {
        var request = new
        {
            vector = queryVector,
            topK = topK,
            returnMetadata = true
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"https://api.cloudflare.com/client/v4/accounts/{_accountId}/vectorize/indexes/{_indexName}/query",
            request);
        
        response.EnsureSuccessStatusCode();
        
        var result = await response.Content.ReadFromJsonAsync<VectorizeQueryResponse>();
        return result?.Matches?.Select(m => new SimilarSeed
        {
            SeedId = m.Id,
            Score = m.Score,
            Metadata = m.Metadata ?? new Dictionary<string, object>()
        }).ToList() ?? new List<SimilarSeed>();
    }
}

public class SimilarSeed
{
    public string SeedId { get; set; } = "";
    public double Score { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class VectorizeQueryResponse
{
    public List<VectorMatch>? Matches { get; set; }
}

public class VectorMatch
{
    public string Id { get; set; } = "";
    public double Score { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}
```

### Step 2: Create Embedding Service

**File**: `Motely.API/Services/EmbeddingService.cs`

```csharp
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Motely.API.Services;

/// <summary>
/// Generates embeddings for seeds and filters using Cloudflare Workers AI.
/// </summary>
public class EmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<EmbeddingService> _logger;
    private readonly string _workerUrl;

    public EmbeddingService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<EmbeddingService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        
        var aiConfig = configuration.GetSection("Cloudflare:WorkersAI");
        _workerUrl = aiConfig["EmbeddingWorkerUrl"] ?? throw new InvalidOperationException("Embedding Worker URL not configured");
    }

    /// <summary>
    /// Generate embedding for seed metadata.
    /// </summary>
    public async Task<float[]> EmbedSeedAsync(string seed, List<string> jokers, List<int> antes, int score)
    {
        var text = $"Seed: {seed}, Jokers: {string.Join(", ", jokers)}, Antes: {string.Join(", ", antes)}, Score: {score}";
        return await EmbedTextAsync(text);
    }

    /// <summary>
    /// Generate embedding for filter description.
    /// </summary>
    public async Task<float[]> EmbedFilterAsync(string filterName, string description, List<string> mustJokers, List<string> shouldJokers)
    {
        var text = $"Filter: {filterName}, Description: {description}, Must: {string.Join(", ", mustJokers)}, Should: {string.Join(", ", shouldJokers)}";
        return await EmbedTextAsync(text);
    }

    private async Task<float[]> EmbedTextAsync(string text)
    {
        var request = new { text };
        var response = await _httpClient.PostAsJsonAsync($"{_workerUrl}/embed", request);
        response.EnsureSuccessStatusCode();
        
        var result = await response.Content.ReadFromJsonAsync<EmbeddingResponse>();
        return result?.Embedding ?? Array.Empty<float>();
    }
}

public class EmbeddingResponse
{
    public float[] Embedding { get; set; } = Array.Empty<float>();
}
```

### Step 3: Create Cloudflare Worker for Embeddings

**File**: `Motely.API/cloudflare-worker-embeddings/src/index.ts`

```typescript
export interface Env {
  AI: Ai;
}

export default {
  async fetch(request: Request, env: Env): Promise<Response> {
    if (request.method !== 'POST') {
      return new Response('Method not allowed', { status: 405 });
    }

    const { text } = await request.json();
    
    // Use Workers AI to generate embedding
    const embedding = await env.AI.run('@cf/baai/bge-base-en-v1.5', {
      text: [text]
    });

    return new Response(JSON.stringify({ embedding }), {
      headers: { 'Content-Type': 'application/json' }
    });
  }
};
```

### Step 4: Update appsettings.json

```json
{
  "Cloudflare": {
    "Vectorize": {
      "ApiToken": "YOUR_API_TOKEN",
      "AccountId": "YOUR_ACCOUNT_ID",
      "IndexName": "balatro-seeds",
      "Enabled": true
    },
    "WorkersAI": {
      "EmbeddingWorkerUrl": "https://your-embeddings-worker.your-subdomain.workers.dev"
    }
  }
}
```

### Step 5: Integration with SearchManager

```csharp
// After search completes, embed and store results
var embedding = await _embeddingService.EmbedSeedAsync(
    seed, 
    jokers, 
    antes, 
    score
);

await _vectorizeService.InsertSeedVectorAsync(
    seed,
    embedding,
    new Dictionary<string, object>
    {
        { "seed", seed },
        { "jokers", jokers },
        { "antes", antes },
        { "score", score }
    }
);
```

## Use Cases Implementation

### 1. Find Similar Seeds

```csharp
// In API endpoint
[HttpGet("seeds/{seedId}/similar")]
public async Task<IActionResult> GetSimilarSeeds(string seedId, int topK = 10)
{
    // Get seed metadata
    var seed = await GetSeedAsync(seedId);
    
    // Generate embedding
    var embedding = await _embeddingService.EmbedSeedAsync(
        seed.Seed, seed.Jokers, seed.Antes, seed.Score
    );
    
    // Query similar seeds
    var similar = await _vectorizeService.QuerySimilarSeedsAsync(embedding, topK);
    
    return Ok(similar);
}
```

### 2. Filter Recommendations

```csharp
// In JamlGenie
var filterEmbedding = await _embeddingService.EmbedFilterAsync(
    filterName, description, mustJokers, shouldJokers
);

var similarFilters = await _vectorizeService.QuerySimilarSeedsAsync(
    filterEmbedding, topK: 5
);
```

## Cost Estimate

- **Free Tier**: 5M vector operations/month
- **Paid**: Pay-as-you-go pricing
- **Example**: 1,000 seeds/day = 30K/month = **FREE**

## References

- [Cloudflare Vectorize Docs](https://developers.cloudflare.com/vectorize/)
- [Workers AI Embeddings](https://developers.cloudflare.com/workers-ai/models/embeddings/)
