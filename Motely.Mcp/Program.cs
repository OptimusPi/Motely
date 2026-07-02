using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Motely.Mcp — a stdio MCP server that validates JAML/JUMMY filters.
// The real seed search happens client-side in the MCP App UI using motely-wasm;
// this server never runs the SIMD engine. stdout is reserved for the MCP protocol;
// logs go to stderr.

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
