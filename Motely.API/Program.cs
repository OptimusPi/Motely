using Motely.API;

/// <summary>Entry point when running Motely.API as a standalone web app.</summary>
public class Program
{
    /// <summary>
    /// Builds the API host and runs the HTTP server until shutdown.
    /// </summary>
    /// <param name="args">Command-line arguments (e.g. --urls http://localhost:3141).</param>
    public static async Task Main(string[] args)
    {
        await MotelyApiHost.CreateHost(args).RunAsync();
    }
}
