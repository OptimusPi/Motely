# Cloudflare Queues Implementation Plan

## Overview
Migrate `SearchQueueService` from DuckDB-based queue to Cloudflare Queues for persistent, scalable job processing.

## Current Implementation
- **Location**: `Motely.API/Services/SearchQueueService.cs`
- **Storage**: DuckDB database (`searchqueue.db`)
- **Operations**: Enqueue, Dequeue, Update status, Mark completed

## Cloudflare Queues Benefits
1. **Persistent**: Survives server restarts
2. **Scalable**: Handle millions of operations
3. **Retry Logic**: Built-in retry for failed jobs
4. **Cost-Effective**: Free tier (1M ops/month), then $0.40/1M
5. **Global**: Low latency worldwide

## Implementation Steps

### Step 1: Create Cloudflare Worker Queue Consumer

**File**: `Motely.API/cloudflare-worker-queue/src/index.ts`

```typescript
export interface Env {
  SEARCH_QUEUE: Queue;
  API_BASE_URL: string; // Your Motely.API URL
}

export default {
  async fetch(request: Request, env: Env): Promise<Response> {
    // Enqueue search job
    if (request.method === 'POST') {
      const body = await request.json();
      await env.SEARCH_QUEUE.send({
        searchId: body.searchId,
        jamlFilter: body.jamlFilter,
        seedSource: body.seedSource,
        threadCount: body.threadCount || 1,
        isBurst: body.isBurst || false,
        dateCreated: new Date().toISOString()
      });
      return new Response(JSON.stringify({ success: true }), {
        headers: { 'Content-Type': 'application/json' }
      });
    }
    return new Response('Method not allowed', { status: 405 });
  },

  async queue(batch: MessageBatch<SearchJob>, env: Env): Promise<void> {
    // Process search jobs
    for (const message of batch.messages) {
      try {
        const job = message.body;
        
        // Call Motely.API to execute search
        const response = await fetch(`${env.API_BASE_URL}/api/search/execute`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(job)
        });
        
        if (!response.ok) {
          throw new Error(`Search execution failed: ${response.statusText}`);
        }
        
        message.ack(); // Mark as processed
      } catch (error) {
        console.error(`Error processing search ${message.body.searchId}:`, error);
        message.retry(); // Retry on failure
      }
    }
  }
};

interface SearchJob {
  searchId: string;
  jamlFilter: string;
  seedSource?: string;
  threadCount: number;
  isBurst: boolean;
  dateCreated: string;
}
```

### Step 2: Create Queue Service Interface

**File**: `Motely.API/Services/ICloudflareQueueService.cs`

```csharp
namespace Motely.API.Services;

public interface ICloudflareQueueService
{
    Task EnqueueSearchAsync(string searchId, string jamlFilter, int threadCount = 1, bool isBurst = false);
    Task<SearchQueueEntry?> DequeueNextAsync();
    Task MarkCompletedAsync(string searchId, long seedsSearched, int resultsFound);
    Task MarkFailedAsync(string searchId, string error);
}
```

### Step 3: Implement Cloudflare Queue Service

**File**: `Motely.API/Services/CloudflareQueueService.cs`

```csharp
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Motely.API.Services;

public class CloudflareQueueService : ICloudflareQueueService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CloudflareQueueService> _logger;
    private readonly string _queueUrl;

    public CloudflareQueueService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<CloudflareQueueService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        
        var queueConfig = configuration.GetSection("Cloudflare:Queue");
        _queueUrl = queueConfig["WorkerUrl"] ?? throw new InvalidOperationException("Queue Worker URL not configured");
    }

    public async Task EnqueueSearchAsync(string searchId, string jamlFilter, int threadCount = 1, bool isBurst = false)
    {
        var job = new
        {
            searchId,
            jamlFilter,
            threadCount,
            isBurst
        };

        var response = await _httpClient.PostAsJsonAsync(_queueUrl, job);
        response.EnsureSuccessStatusCode();
        
        _logger.LogInformation("Enqueued search {SearchId} to Cloudflare Queue", searchId);
    }

    // Other methods...
}
```

### Step 4: Update appsettings.json

```json
{
  "Cloudflare": {
    "Queue": {
      "WorkerUrl": "https://your-queue-worker.your-subdomain.workers.dev",
      "Enabled": true
    }
  }
}
```

### Step 5: Migration Strategy

1. **Dual Mode**: Support both DuckDB queue (legacy) and Cloudflare Queue (new)
2. **Feature Flag**: Use `appsettings.json` to enable/disable Cloudflare Queue
3. **Gradual Migration**: Start with new searches using Cloudflare Queue
4. **Backward Compatibility**: Keep DuckDB queue for existing searches

### Step 6: Update SearchManager

```csharp
// In SearchManager.StartSearchAsync
if (useCloudflareQueue)
{
    await _cloudflareQueueService.EnqueueSearchAsync(searchId, filterJaml, threadCount, isBurst);
}
else
{
    _queueService.Enqueue(searchId, filterJaml, threadCount, isBurst);
}
```

## Testing

1. **Local Testing**: Use Wrangler dev server
2. **Integration Testing**: Test with real Cloudflare Queue
3. **Load Testing**: Test with 100+ concurrent searches
4. **Retry Testing**: Test retry logic for failed searches

## Deployment

1. Deploy Worker to Cloudflare
2. Create Queue binding in Worker
3. Update appsettings.json with Worker URL
4. Enable feature flag
5. Monitor queue metrics

## Cost Estimate

- **Free Tier**: 1M operations/month
- **Paid**: $0.40 per 1M operations
- **Example**: 10,000 searches/day = 300K/month = **FREE**

## References

- [Cloudflare Queues Docs](https://developers.cloudflare.com/queues/)
- [Workers Queue API](https://developers.cloudflare.com/queues/platform/configuration/)
