namespace Motely.Executors
{
    /// <summary>
    /// Platform-agnostic cancel key (e.g. Ctrl+C) registration. Desktop: Console.CancelKeyPress; browser: no-op.
    /// </summary>
    public interface ICancelKeyHandler
    {
        void Register(Action onCancel);
    }
}
