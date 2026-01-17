using Motely.API;

public class Program
{
    public static async Task Main(string[] args)
    {
        // Run normal HTTP server
        // MCP stdio mode is handled by Motely.MCP project
        MotelyApiHost.CreateHost(args).Run();
    }
}
