using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Motely.MCP.McpProtocol;
using GenieFeedbackService = global::Motely.API.GenieFeedbackService;
using SearchManager = global::Motely.API.SearchManager;

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

        builder.Logging.ClearProviders();
        builder.Logging.AddSimpleConsole(options =>
        {
            options.ColorBehavior = Microsoft
                .Extensions
                .Logging
                .Console
                .LoggerColorBehavior
                .Disabled;
            options.SingleLine = true;
        });

        builder.Services.AddSingleton(SearchManager.Instance);
        builder.Services.AddSingleton<GenieFeedbackService>();
        builder.Services.AddHttpClient();
        builder.Services.AddScoped<McpServer>();
        builder.Services.AddScoped<McpProtocolServer>();

        builder.Services.AddScoped<McpStdioServer>();

        var host = builder.Build();

        using var scope = host.Services.CreateScope();
        var stdioServer = scope.ServiceProvider.GetRequiredService<McpStdioServer>();

        Console.Error.WriteLine("MCP Server starting in stdio mode...");
        await stdioServer.RunAsync();
    }
}
