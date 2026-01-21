using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Motely.API.Hubs;

namespace Motely.API;

/// <summary>
/// Broadcasts search updates to clients via SignalR
/// </summary>
public class SearchBroadcaster : ISearchBroadcaster
{
    private readonly IHubContext<SearchHub> _hubContext;
    private readonly ILogger<SearchBroadcaster> _logger;

    public SearchBroadcaster(IHubContext<SearchHub> hubContext, ILogger<SearchBroadcaster> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>
    /// Broadcasts a message to all connected clients
    /// </summary>
    public async Task BroadcastAsync(string message)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("SearchUpdate", message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting message to all clients");
        }
    }

    /// <summary>
    /// Broadcasts a message to clients subscribed to a specific search
    /// </summary>
    public async Task BroadcastToSearchAsync(string searchId, string json)
    {
        try
        {
            var groupName = $"search_{searchId}";

            // Parse JSON to determine event type and send as object (not string)
            try
            {
                var jsonDoc = JsonDocument.Parse(json);
                var rootElement = jsonDoc.RootElement;

                if (rootElement.TryGetProperty("type", out var typeElement))
                {
                    var type = typeElement.GetString();

                    // Route based on message type - send parsed object, not JSON string
                    switch (type)
                    {
                        case "result":
                            // Extract result data for frontend - send the nested result object
                            if (rootElement.TryGetProperty("result", out var resultElement))
                            {
                                // Deserialize the result object directly
                                var resultObj = JsonSerializer.Deserialize<object>(
                                    resultElement.GetRawText()
                                );
                                await _hubContext
                                    .Clients.Group(groupName)
                                    .SendAsync("Result", resultObj);
                            }
                            else
                            {
                                // Fallback: send whole message as object
                                var fullObj = JsonSerializer.Deserialize<object>(json);
                                await _hubContext
                                    .Clients.Group(groupName)
                                    .SendAsync("Result", fullObj);
                            }
                            break;
                        case "progress":
                            // Map progress fields to frontend expectations
                            // Frontend expects: { processed: number, ... }
                            // Backend sends: { seedsSearched: number, ... }
                            var progressDict = new Dictionary<string, object>();
                            if (rootElement.TryGetProperty("seedsSearched", out var seedsSearched))
                                progressDict["processed"] = seedsSearched.GetInt64();
                            if (
                                rootElement.TryGetProperty("seedsPerSecond", out var seedsPerSecond)
                            )
                                progressDict["speed"] = seedsPerSecond.GetDouble();
                            if (rootElement.TryGetProperty("seedsFound", out var seedsFound))
                                progressDict["found"] = seedsFound.GetInt32();
                            if (rootElement.TryGetProperty("currentBatch", out var currentBatch))
                                progressDict["currentBatch"] = currentBatch.GetInt64();
                            if (rootElement.TryGetProperty("totalBatches", out var totalBatches))
                                progressDict["totalBatches"] = totalBatches.GetInt64();
                            if (rootElement.TryGetProperty("searchId", out var searchIdProp))
                                progressDict["searchId"] = searchIdProp.GetString() ?? "";

                            await _hubContext
                                .Clients.Group(groupName)
                                .SendAsync("Progress", progressDict);
                            break;
                        case "search_completed":
                        case "search_failed":
                        case "search_halted":
                            var updateObj = JsonSerializer.Deserialize<object>(json);
                            await _hubContext
                                .Clients.Group(groupName)
                                .SendAsync("SearchUpdate", updateObj);
                            break;
                        default:
                            // Fallback to generic SearchUpdate
                            var defaultObj = JsonSerializer.Deserialize<object>(json);
                            await _hubContext
                                .Clients.Group(groupName)
                                .SendAsync("SearchUpdate", defaultObj);
                            break;
                    }
                }
                else
                {
                    // No type field, send as generic SearchUpdate
                    var genericObj = JsonSerializer.Deserialize<object>(json);
                    await _hubContext
                        .Clients.Group(groupName)
                        .SendAsync("SearchUpdate", genericObj);
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to parse JSON for search {SearchId}, sending as string",
                    searchId
                );
                // Not valid JSON or can't parse, send as string
                await _hubContext.Clients.Group(groupName).SendAsync("SearchUpdate", json);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting to search {SearchId}", searchId);
        }
    }

    /// <summary>
    /// Broadcasts an object to clients subscribed to a specific search (serializes automatically)
    /// </summary>
    public async Task BroadcastToSearchAsync(string searchId, object message)
    {
        try
        {
            var json = JsonSerializer.Serialize(
                message,
                new JsonSerializerOptions(JsonSerializerDefaults.Web)
            );
            await BroadcastToSearchAsync(searchId, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error serializing and broadcasting to search {SearchId}",
                searchId
            );
        }
    }
}
