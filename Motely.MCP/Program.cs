using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Motely.MCP;

namespace Motely.MCP;

public class Program
{
    public static async Task Main(string[] args)
    {
        if (McpStdioEntryPoint.ShouldRunStdioMode(args))
        {
            await McpStdioEntryPoint.RunStdioModeAsync(args);
            return;
        }

        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        builder.Services.AddCors(options =>
        {
            options.AddPolicy(
                "AllowAll",
                policy =>
                {
                    policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
                }
            );
        });

        builder.Logging.ClearProviders();
        builder.Logging.AddConsole(options =>
        {
            options.FormatterName = "Simple";
        });

        builder.Services.AddScoped<McpServer>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<McpServer>>();
            var httpClient = new HttpClient();
            var config = sp.GetRequiredService<IConfiguration>();
            return new McpServer(logger, httpClient, config);
        });

        builder.Services.AddScoped<McpProtocol.McpProtocolServer>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<McpProtocol.McpProtocolServer>>();
            var mcpServer = sp.GetRequiredService<McpServer>();
            var searchManager = global::Motely.API.SearchManager.Instance;
            return new McpProtocol.McpProtocolServer(logger, mcpServer, searchManager);
        });

        var app = builder.Build();

        var motelyRoot = FindMotelyRoot();
        if (!string.IsNullOrEmpty(motelyRoot))
        {
            global::Motely.API.MotelyPaths.Initialize(app.Environment, app.Configuration);
        }

        app.UseCors("AllowAll");
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "Motely MCP v1");
            c.RoutePrefix = "swagger";
        });

        app.MapGet("/health", () => new { status = "healthy", timestamp = DateTime.UtcNow });
        app.MapPost("/mcp/prompt", McpEndpoints.ProcessPrompt);
        app.MapPost("/mcp/generate", McpEndpoints.GenerateJaml);
        app.MapPost("/mcp", McpEndpoints.HandleMcpProtocol);

        app.Run();
    }

    private static string? FindMotelyRoot()
    {
        var currentDir = Directory.GetCurrentDirectory();
        var dir = new DirectoryInfo(currentDir);

        while (dir != null)
        {
            var jamlFiltersPath = Path.Combine(dir.FullName, "JamlFilters");
            if (Directory.Exists(jamlFiltersPath))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }

        return currentDir;
    }
}
