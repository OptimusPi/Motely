namespace Motely.Executors
{
    /// <summary>
    /// No-op cancel key handler for browser/WASM where Console.CancelKeyPress is not available.
    /// </summary>
    public sealed class NoOpCancelKeyHandler : ICancelKeyHandler
    {
        public static readonly NoOpCancelKeyHandler Instance = new();

        private NoOpCancelKeyHandler() { }

        public void Register(Action onCancel) { }
    }
}
