namespace Motely.BrowserWasm;

public sealed class MotelyBrowserApi : IMotelyBrowserApi
{
    public string GetVersion() =>
        typeof(MotelyBrowserApi).Assembly.GetName().Version?.ToString() ?? "unknown";
}
