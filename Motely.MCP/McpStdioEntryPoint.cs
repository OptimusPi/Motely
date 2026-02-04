using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Motely.MCP.McpProtocol;

namespace Motely.MCP;

public static class McpStdioEntryPoint
{
    public static bool ShouldRunStdioMode(string[]? args = null)
    {
        if (args != null && args.Contains("--mcp-stdio", StringComparer.OrdinalIgnoreCase))
            return true;

        if (Environment.GetEnvironmentVariable("MCP_MODE") == "stdio")
            return true;

        try
        {
            return Console.IsInputRedirected;
        }
        catch
        {
            return false;
        }
    }

    public static async Task RunStdioModeAsync(string[]? args = null)
    {
        var builder = Host.CreateApplicationBuilder(args ?? Array.Empty<string>());

        // Disable all logging to stdout - MCP uses stdout for JSON-RPC
        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.Logging.AddConsole(options =>
        {
            options.LogToStandardErrorThreshold = LogLevel.Trace; // All logs go to stderr
        });

        builder.Services.AddScoped<McpProtocolServer>();
        builder.Services.AddScoped<McpStdioServer>();

        var host = builder.Build();

        using var scope = host.Services.CreateScope();
        var stdioServer = scope.ServiceProvider.GetRequiredService<McpStdioServer>();

        Console.Error.WriteLine("MCP Server starting in stdio mode...");
        await stdioServer.RunAsync();
    }
}
