using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Motely;
using Motely.API.Services;
using Motely.API;

public class Program
{
    public static async Task Main(string[] args)
    {
        // Check if we should run in MCP stdio mode (for Cursor, Claude Desktop, etc.)
        if (McpStdioEntryPoint.ShouldRunStdioMode(args))
        {
            await McpStdioEntryPoint.RunStdioModeAsync(args);
            return;
        }

        // Otherwise, run normal HTTP server
        MotelyApiHost.CreateHost(args).Run();
    }
}
