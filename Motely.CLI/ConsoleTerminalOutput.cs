using Motely.Executors;

namespace Motely
{
    /// <summary>
    /// Desktop terminal output using System.Console.
    /// </summary>
    internal sealed class ConsoleTerminalOutput : ITerminalOutput
    {
        public void Write(string? value) => Console.Write(value);

        public void WriteLine(string? value) => Console.WriteLine(value);
    }
}
