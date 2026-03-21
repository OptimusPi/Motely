namespace Motely.BrowserWasm.Interop;

/// <summary>JavaScript bindings invoked from .NET (search progress/results, optional console).</summary>
public interface IMotelyJsUi
{
    void NotifySearchProgress(SearchProgressPayload payload);
    void NotifySearchResult(SearchResultPayload payload);
    void NotifyConsoleLog(string message);
    void NotifyConsoleError(string message);
}
