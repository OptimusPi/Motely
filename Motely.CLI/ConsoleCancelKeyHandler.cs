using Motely.Executors;

namespace Motely
{
    /// <summary>
    /// Desktop cancel key (Ctrl+C) handler using Console.CancelKeyPress.
    /// </summary>
    internal sealed class ConsoleCancelKeyHandler : ICancelKeyHandler
    {
        public void Register(Action onCancel)
        {
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                onCancel();
            };
        }
    }
}
