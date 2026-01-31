namespace Motely.Executors
{
    /// <summary>
    /// Platform-agnostic terminal output. Desktop: Console; browser/WASM: no-op or logger.
    /// </summary>
    public interface ITerminalOutput
    {
        void Write(string? value);
        void WriteLine(string? value);
    }
}
