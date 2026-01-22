using Motely.API;

public class Program
{
    public static async Task Main(string[] args)
    {
        // Run normal HTTP server
        await MotelyApiHost.CreateHost(args).RunAsync();
    }
}
