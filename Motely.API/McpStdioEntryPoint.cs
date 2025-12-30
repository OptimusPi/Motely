using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Motely.API;

/// <summary>
/// Entry point for MCP server in stdio mode
/// Detects stdio mode and runs MCP server instead of HTTP server
/// </summary>
public static class McpStdioEntryPoint
{
    /// <summary>
    /// Check if we should run in stdio mode
    /// Detects: --mcp-stdio flag, MCP_MODE env var, or if stdin is not a terminal
    /// </summary>
    public static bool ShouldRunStdioMode(string[]? args = null)
    {
        // Check for explicit flag
        if (args != null && args.Contains("--mcp-stdio", StringComparer.OrdinalIgnoreCase))
            return true;

        // Check environment variable
        if (Environment.GetEnvironmentVariable("MCP_MODE") == "stdio")
            return true;

        // Check if stdin is redirected (not a terminal)
        // This happens when launched as a subprocess by Claude Desktop
        try
        {
            // If stdin is redirected (not a terminal), we're in stdio mode
            return Console.IsInputRedirected;
        }
        catch
        {
            // If we can't check, assume HTTP mode
            return false;
        }
    }

    /// <summary>
    /// Run MCP server in stdio mode
    /// </summary>
    public static async Task RunStdioModeAsync(string[]? args = null)
    {
        // Create minimal host for dependency injection
        var builder = Host.CreateApplicationBuilder(args ?? Array.Empty<string>());
        
        // Configure logging to stderr (stdout is for JSON-RPC only)
        builder.Logging.ClearProviders();
        builder.Logging.AddSimpleConsole(options =>
        {
            options.ColorBehavior = Microsoft.Extensions.Logging.Console.LoggerColorBehavior.Disabled;
            options.SingleLine = true;
        });

        // Configure services (minimal - just what MCP server needs)
        builder.Services.AddSingleton<GenieFeedbackService>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<GenieFeedbackService>>();
            return new GenieFeedbackService(logger);
        });

        builder.Services.AddScoped<McpServer>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<McpServer>>();
            var httpClient = new HttpClient();
            var config = sp.GetRequiredService<IConfiguration>();
            var feedbackService = sp.GetService<GenieFeedbackService>();
            return new McpServer(logger, httpClient, config, feedbackService);
        });

        builder.Services.AddScoped<McpProtocol.McpProtocolServer>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<McpProtocol.McpProtocolServer>>();
            var jamlGenieService = sp.GetRequiredService<McpServer>();
            var searchManager = SearchManager.Instance;
            return new McpProtocol.McpProtocolServer(logger, jamlGenieService, searchManager);
        });

        builder.Services.AddScoped<McpProtocol.McpStdioServer>();

        var host = builder.Build();

        // Get services and run stdio server
        using var scope = host.Services.CreateScope();
        var stdioServer = scope.ServiceProvider.GetRequiredService<McpProtocol.McpStdioServer>();
        
        Console.Error.WriteLine("MCP Server starting in stdio mode...");
        await stdioServer.RunAsync();
    }
}

