using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Text.Json;
using Motely;
using Motely.Analysis;
using Motely.Executors;

namespace Motely.BrowserWasm;

/// <summary>
/// Browser WASM [JSExport] surface. Thin wrappers over MotelyExports.
/// JSON only for AnalyzeSeed (genuinely nested structure).
/// </summary>
[SupportedOSPlatform("browser")]
public static partial class MotelyWasmExports
{
    [JSExport]
    public static string GetVersion() => MotelyExports.GetVersion(typeof(MotelyWasmExports).Assembly);

    [JSExport]
    public static bool IsSimdEnabled() => MotelyExports.IsSimdEnabled();

    [JSExport]
    public static int GetProcessorCount() => MotelyExports.GetProcessorCount();

    [JSExport]
    public static bool ValidateJaml(string jamlContent) => MotelyExports.ValidateJaml(jamlContent);

    [JSExport]
    public static string ValidateJamlWithError(string jamlContent) => MotelyExports.ValidateJamlWithError(jamlContent);

    [JSExport]
    public static string AnalyzeSeed(string seed, string deck, string stake)
    {
        try
        {
            var dto = MotelyExports.AnalyzeSeed(seed, deck, stake);
            return JsonSerializer.Serialize(dto, AnalysisJsonContext.Default.SeedAnalysisDto);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(
                new ErrorDto { Error = ex.Message },
                MotelyJsonContext.Default.ErrorDto);
        }
    }

    // ── Search ───────────────────────────────────────────────────────────

    [JSExport]
    public static Task<string> StartJamlSearch(
        string jamlContent,
        int threadCount,
        int batchCharCount,
        [JSMarshalAs<JSType.Function<JSType.Number, JSType.Number, JSType.Number>>]
        Action<long, long, long> onProgress,
        [JSMarshalAs<JSType.Function<JSType.String, JSType.Number>>]
        Action<string, int> onResult)
    {
        try
        {
            threadCount = Math.Clamp(threadCount, 1, Environment.ProcessorCount);
            batchCharCount = Math.Clamp(batchCharCount, 1, 7);

            var request = new MotelySearchRequest
            {
                ThreadCount = threadCount,
                BatchCharCount = batchCharCount,
            };

            var (status, _, _) = MotelyExports.RunSearch(jamlContent, request, onProgress, onResult);
            return Task.FromResult(status);
        }
        catch (Exception ex)
        {
            return Task.FromResult($"error: {ex.Message}");
        }
    }

    [JSExport]
    public static void StopSearch()
    {
        // TODO: wire up cancellation through MotelyExports
    }
}
