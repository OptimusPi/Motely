using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Motely.MCP;

var builder = Host.CreateApplicationBuilder(args);

// stdio transport — how Claude Desktop/Code and most local MCP clients launch a server:
// spawn the process, talk JSON-RPC over stdin/stdout. No open port, no auth needed locally.
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

// stdout is the wire protocol — nothing else may write to it, or every client breaks.
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

await builder.Build().RunAsync();
