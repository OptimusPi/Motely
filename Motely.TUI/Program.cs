using Motely.TUI;

namespace Motely
{
    partial class Program
    {
        static int Main(string[] args)
        {
            // If no args provided, launch TUI
            if (args.Length == 0)
            {
                return MotelyTUI.Run();
            }
            
            // If args provided, show message that CLI is separate
            Console.WriteLine("CLI functionality has been moved to Motely.CLI");
            Console.WriteLine("Run: dotnet run --project Motely.CLI -- [args]");
            return 1;
        }
    }
}
