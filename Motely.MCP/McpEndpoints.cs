using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Motely.MCP.McpProtocol;
using McpPromptRequest = global::Motely.API.Models.McpPromptRequest;

namespace Motely.MCP;

public static class McpEndpoints
{
    /// <summary>
    /// Register MCP endpoints - called by Motely.API via reflection to avoid circular dependency
    /// </summary>
    public static IEndpointRouteBuilder MapMcpEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
            "/mcp/generate",
            async (McpPromptRequest request, McpServer mcpServer) =>
                await GenerateJaml(request, mcpServer)
        );
        endpoints.MapPost(
            "/mcp/prompt",
            async (McpPromptRequest request, McpServer mcpServer) =>
                await ProcessPrompt(request, mcpServer)
        );
        endpoints.MapPost(
            "/mcp/protocol",
            async (HttpRequest request, McpProtocolServer mcpProtocolServer) =>
                await HandleMcpProtocol(request, mcpProtocolServer)
        );
        return endpoints;
    }

    public static async Task<IResult> ProcessPrompt(McpPromptRequest request, McpServer mcpServer)
    {
        try
        {
            if (request?.Prompt == null)
                return Results.BadRequest(new { error = "Missing prompt" });

            var response = await mcpServer.ProcessPromptAsync(request.Prompt);

            return Results.Ok(
                new
                {
                    success = response.Success,
                    jamlFilter = response.JamlFilter,
                    reasoning = response.Reasoning,
                    error = response.Success ? null : response.Message,
                    searchId = response.SearchId,
                    results = response.Results,
                    columns = response.Columns,
                    message = response.Message,
                    searchUrl = response.SearchUrl,
                }
            );
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    public static async Task<IResult> GenerateJaml(McpPromptRequest request, McpServer mcpServer)
    {
        try
        {
            if (request?.Prompt == null)
                return Results.BadRequest(new { error = "Missing prompt" });

            var (jaml, reasoning, error) = await mcpServer.GenerateJamlOnlyAsync(request.Prompt);

            return Results.Ok(
                new
                {
                    success = string.IsNullOrEmpty(error),
                    jaml = jaml,
                    reasoning = reasoning,
                    error = error,
                }
            );
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    public static async Task<IResult> HandleMcpProtocol(
        HttpRequest request,
        McpProtocolServer mcpProtocolServer
    )
    {
        try
        {
            using var reader = new StreamReader(request.Body);
            var body = await reader.ReadToEndAsync();

            if (string.IsNullOrWhiteSpace(body))
            {
                return Results.BadRequest(new { error = "Request body is required" });
            }

            var jsonRpcRequest = JsonSerializer.Deserialize<JsonRpcRequest>(
                body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );

            if (jsonRpcRequest == null)
            {
                return Results.BadRequest(new { error = "Invalid JSON-RPC request" });
            }

            var response = await mcpProtocolServer.HandleRequestAsync(jsonRpcRequest);

            return Results.Json(response);
        }
        catch (JsonException ex)
        {
            return Results.BadRequest(new { error = $"Invalid JSON: {ex.Message}" });
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }
}
