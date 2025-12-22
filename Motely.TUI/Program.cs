using Motely.TUI;

namespace Motely.TUI;

class Program
{
    static async Task<int> Main(string[] args)
    {
        // Parse command line arguments
        bool hostApi = false;
        string host = "localhost";
        int port = 5123;
        bool showHelp = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--host-api":
                    hostApi = true;
                    break;
                case "--host":
                    if (i + 1 < args.Length)
                    {
                        host = args[++i];
                    }
                    break;
                case "--port":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out int parsedPort))
                    {
                        port = parsedPort;
                    }
                    break;
                case "-h":
                case "--help":
                    showHelp = true;
                    break;
            }
        }

        if (showHelp)
        {
            Console.WriteLine("Motely TUI - Balatro Seed Search Tool");
            Console.WriteLine();
            Console.WriteLine("Usage: Motely.TUI [options]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --host-api      Run API server only (no TUI interface)");
            Console.WriteLine("  --host <host>   API server host (default: localhost)");
            Console.WriteLine("  --port <port>   API server port (default: 5123)");
            Console.WriteLine("  -h, --help      Show this help message");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  Motely.TUI                    # Run TUI interface");
            Console.WriteLine("  Motely.TUI --host-api         # Run API server on localhost:5123");
            Console.WriteLine("  Motely.TUI --host-api --host 0.0.0.0 --port 8080");
            return 0;
        }

        if (hostApi)
        {
            return await MotelyTUI.RunApiOnly(host, port);
        }

        return MotelyTUI.Run();
    }
}
