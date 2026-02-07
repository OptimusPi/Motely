using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Text;
using Motely.BrowserWasm;
using Motely.Executors;
using Motely.Repository;

// Route Console output to browser console
JsConsoleWriter.Attach();

// Register browser repository (no DuckDB, no filesystem)
MotelySearchOrchestrator.SetRepository(BrowserRepository.Instance);

// Keep alive - required for WASM host to stay resident for JSExport calls
await Task.Delay(Timeout.Infinite);

[SupportedOSPlatform("browser")]
internal static partial class JsConsole
{
    [JSImport("globalThis.console.log")]
    internal static partial void Log([JSMarshalAs<JSType.String>] string message);

    [JSImport("globalThis.console.error")]
    internal static partial void Error([JSMarshalAs<JSType.String>] string message);
}

internal sealed class JsConsoleWriter : TextWriter
{
    private readonly bool _isError;

    internal JsConsoleWriter(bool isError)
    {
        _isError = isError;
    }

    public override Encoding Encoding => Encoding.UTF8;

    public static void Attach()
    {
        Console.SetOut(new JsConsoleWriter(isError: false));
        Console.SetError(new JsConsoleWriter(isError: true));
    }

    public override void Write(string? value)
    {
        if (string.IsNullOrEmpty(value)) return;
        if (_isError) JsConsole.Error(value);
        else JsConsole.Log(value);
    }

    public override void WriteLine(string? value)
    {
        if (value is null) return;
        if (_isError) JsConsole.Error(value);
        else JsConsole.Log(value);
    }
}
