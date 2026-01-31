namespace Motely.Executors
{
    /// <summary>
    /// No-op terminal output for browser/WASM or when console output is not desired.
    /// </summary>
    public sealed class NoOpTerminalOutput : ITerminalOutput
    {
        public static readonly NoOpTerminalOutput Instance = new();

        private NoOpTerminalOutput() { }

        public void Write(string? value) { }

        public void WriteLine(string? value) { }
    }
}
