using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Motely.API.McpProtocol;

/// <summary>
/// MCP Server stdio transport handler
/// Reads JSON-RPC requests from stdin, writes responses to stdout
/// Used when running in stdio mode (e.g., for Claude Desktop command-based setup)
/// </summary>
public class McpStdioServer
{
    private readonly ILogger<McpStdioServer> _logger;
    private readonly McpProtocolServer _mcpServer;

    public McpStdioServer(ILogger<McpStdioServer> logger, McpProtocolServer mcpServer)
    {
        _logger = logger;
        _mcpServer = mcpServer;
    }

    /// <summary>
    /// Run MCP server in stdio mode
    /// Reads line-delimited JSON-RPC requests from stdin
    /// Writes JSON-RPC responses to stdout
    /// Logs to stderr
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("MCP Server starting in stdio mode");
        _logger.LogInformation("Reading JSON-RPC requests from stdin, writing responses to stdout");

        try
        {
            using var stdin = Console.OpenStandardInput();
            using var stdout = Console.OpenStandardOutput();
            using var reader = new StreamReader(stdin, Encoding.UTF8, leaveOpen: true);
            using var writer = new StreamWriter(stdout, Encoding.UTF8, leaveOpen: true)
            {
                AutoFlush = true
            };

            string? line;
            while (!cancellationToken.IsCancellationRequested && (line = await reader.ReadLineAsync()) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                try
                {
                    // Parse JSON-RPC request
                    var request = JsonSerializer.Deserialize<JsonRpcRequest>(
                        line,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                    );

                    if (request == null)
                    {
                        _logger.LogWarning("Failed to parse JSON-RPC request: {Line}", line);
                        var errorResponse = JsonRpcResponse.Error(null, -32700, "Parse error");
                        await writer.WriteLineAsync(JsonSerializer.Serialize(errorResponse));
                        continue;
                    }

                    // Handle request
                    var response = await _mcpServer.HandleRequestAsync(request);

                    // Write response as JSON (one line, preserve property names as-is for MCP protocol)
                    var responseJson = JsonSerializer.Serialize(response);
                    await writer.WriteLineAsync(responseJson);
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "JSON parse error: {Line}", line);
                    var errorResponse = JsonRpcResponse.Error(null, -32700, $"Parse error: {ex.Message}");
                    await writer.WriteLineAsync(JsonSerializer.Serialize(errorResponse));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing request: {Line}", line);
                    var errorResponse = JsonRpcResponse.Error(null, -32603, $"Internal error: {ex.Message}");
                    await writer.WriteLineAsync(JsonSerializer.Serialize(errorResponse));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in stdio mode");
            throw;
        }
    }
}

