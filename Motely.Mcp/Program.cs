using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Motely.Mcp — a stdio MCP server that puts the real Motely engine one tool-call away.
// The client model writes the JAML itself; this bridge runs it on the real SIMD engine
// and hands back real seeds. stdout is reserved for the MCP protocol; logs go to stderr.

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services.AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly();

await builder.Build().RunAsync();
