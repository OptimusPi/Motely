using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Text;
using Motely.BrowserWasm;

// Route Console output to browser console
JsConsole.Attach();

// Keep alive - required for WASM host to stay resident for JSExport calls
await Task.Delay(Timeout.Infinite);

[SupportedOSPlatform("browser")]
internal static partial class JsConsole
{
    [JSImport("globalThis.console.log")]
    static partial void Log([JSMarshalAs<JSType.String>] string message);

    [JSImport("globalThis.console.error")]
    static partial void Error([JSMarshalAs<JSType.String>] string message);

    internal static void Attach()
    {
        Console.SetOut(new BufferedWriter(isError: false));
        Console.SetError(new BufferedWriter(isError: true));
    }

    private sealed class BufferedWriter : TextWriter
    {
        private readonly bool _isError;
        private readonly StringBuilder _buffer = new();

        internal BufferedWriter(bool isError) => _isError = isError;

        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value)
        {
            if (value == '\n')
            {
                FlushBuffer();
            }
            else if (value != '\r')
            {
                _buffer.Append(value);
            }
        }

        public override void Write(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return;
            foreach (var c in value)
            {
                Write(c);
            }
        }

        private void FlushBuffer()
        {
            if (_buffer.Length > 0)
            {
                var msg = _buffer.ToString();
                if (_isError)
                    Error(msg);
                else
                    Log(msg);

                _buffer.Clear();
            }
        }
    }
}
