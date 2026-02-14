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
        Console.SetOut(new Writer(isError: false));
        Console.SetError(new Writer(isError: true));
    }

    private sealed class Writer : TextWriter
    {
        private readonly bool _isError;

        internal Writer(bool isError) => _isError = isError;

        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(string? value)
        {
            if (string.IsNullOrEmpty(value)) return;
            if (_isError) Error(value);
            else Log(value);
        }

        public override void WriteLine(string? value)
        {
            if (value is null) return;
            if (_isError) Error(value);
            else Log(value);
        }
    }
}
