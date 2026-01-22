using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Motely.MCP;
using GenieFeedbackService = global::Motely.API.GenieFeedbackService;

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

        builder.Services.AddSingleton(global::Motely.API.SearchManager.Instance);
        builder.Services.AddHttpClient();
        builder.Services.AddScoped<McpServer>();
        builder.Services.AddScoped<McpProtocol.McpProtocolServer>();

        var app = builder.Build();

        global::Motely.API.MotelyPaths.Initialize(app.Environment, app.Configuration);

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

}
