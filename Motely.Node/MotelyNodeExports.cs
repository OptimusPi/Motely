using Microsoft.JavaScript.NodeApi;
using Motely.Executors;
using Motely.Filters;

namespace Motely.NodeInterop;

/// <summary>
/// Node.js exports — thin bridge to the real orchestrator.
/// [JSExport] on the CLASS. Generator handles marshalling + .d.ts.
/// Returns real types, not JSON strings.
/// </summary>
[JSExport]
public static class MotelyNodeExports
{
    public static string? ValidateJaml(string jamlContent) =>
        JamlConfigLoader.TryLoad(jamlContent, out _, out var error) ? null : error;

    public static string? ValidateRequest(MotelySearchRequest request) =>
        MotelySearchOrchestrator.ValidateRequest(request);

    public static (string Status, int SeedsFound, int HighestScore) RunSearch(
        string jamlContent, MotelySearchRequest request,
        Action<long, long, long>? onProgress = null,
        Action<string, int>? onResult = null,
        CancellationToken cancellationToken = default) =>
        MotelySearchOrchestrator.RunSearch(jamlContent, request, onProgress, onResult, cancellationToken);
}
