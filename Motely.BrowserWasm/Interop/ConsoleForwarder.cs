using System.Text;

namespace Motely.BrowserWasm.Interop;

/// <summary>Forwards <see cref="Console"/> to JavaScript via <see cref="IMotelyJsUi"/> notify methods.</summary>
public sealed class ConsoleForwarder(IMotelyJsUi js)
{
    public void Attach()
    {
        Console.SetOut(new UiWriter(js, isError: false));
        Console.SetError(new UiWriter(js, isError: true));
    }

    private sealed class UiWriter(IMotelyJsUi js, bool isError) : TextWriter
    {
        private readonly StringBuilder _buffer = new();

        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value)
        {
            if (value == '\n')
                FlushBuffer();
            else if (value != '\r')
                _buffer.Append(value);
        }

        public override void Write(string? value)
        {
            if (string.IsNullOrEmpty(value)) return;
            foreach (var c in value)
                Write(c);
        }

        private void FlushBuffer()
        {
            if (_buffer.Length == 0) return;
            var msg = _buffer.ToString();
            _buffer.Clear();
            if (isError) js.NotifyConsoleError(msg);
            else js.NotifyConsoleLog(msg);
        }
    }
}
