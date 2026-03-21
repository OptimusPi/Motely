using System.Runtime.Intrinsics;
using System.Runtime.Versioning;
using System.Text.Json;
using Motely;
using Motely.Analysis;
using Motely.Executors;
using Motely.Filters;

namespace Motely.BrowserWasm.Interop;

[SupportedOSPlatform("browser")]
public sealed class MotelyWasmBackend : IMotelyWasmBackend
{
    private readonly IMotelyJsUi _js;
    private static int _consoleAttached;

    public MotelyWasmBackend(IMotelyJsUi js)
    {
        _js = js;
        if (Interlocked.Exchange(ref _consoleAttached, 1) == 0)
            new ConsoleForwarder(js).Attach();
    }

    public int CreateInstance() => MotelySearchSession.Create();

    public void DestroyInstance(int id) => MotelySearchSession.Destroy(id);

    public Task<string> StartJamlSearch(
        int instanceId, string jamlContent, int threadCount, int batchCharCount,
        int startBatch, int endBatch)
        => RunSearch(instanceId, jamlContent, new MotelySearchRequest
        {
            ThreadCount = threadCount > 0 ? threadCount : Environment.ProcessorCount,
            BatchCharCount = batchCharCount is >= 1 and <= 7 ? batchCharCount : 4,
            StartBatch = startBatch >= 0 ? startBatch : null,
            EndBatch = endBatch > 0 ? endBatch : null,
        });

    public Task<string> StartSeedListSearch(
        int instanceId, string jamlContent, int threadCount, int batchCharCount,
        IReadOnlyList<string> seeds)
        => RunSearch(instanceId, jamlContent, new MotelySearchRequest
        {
            ThreadCount = threadCount > 0 ? threadCount : Environment.ProcessorCount,
            BatchCharCount = batchCharCount is >= 1 and <= 7 ? batchCharCount : 4,
            Seeds = seeds.ToArray(),
        });

    public Task<string> StartKeywordSearch(
        int instanceId, string jamlContent, int threadCount, int batchCharCount,
        IReadOnlyList<string> keywords, string padding)
        => RunSearch(instanceId, jamlContent, new MotelySearchRequest
        {
            ThreadCount = threadCount > 0 ? threadCount : Environment.ProcessorCount,
            BatchCharCount = batchCharCount is >= 1 and <= 7 ? batchCharCount : 4,
            Keywords = keywords.ToArray(),
            Padding = string.IsNullOrEmpty(padding) ? null : padding,
        });

    public Task<string> StartRandomSearch(
        int instanceId, string jamlContent, int threadCount, int batchCharCount, int count)
        => RunSearch(instanceId, jamlContent, new MotelySearchRequest
        {
            ThreadCount = threadCount > 0 ? threadCount : Environment.ProcessorCount,
            BatchCharCount = batchCharCount is >= 1 and <= 7 ? batchCharCount : 4,
            RandomSeeds = count > 0 ? count : 1000,
        });

    public Task<string> StartPalindromeSearch(
        int instanceId, string jamlContent, int threadCount, int batchCharCount)
        => RunSearch(instanceId, jamlContent, new MotelySearchRequest
        {
            ThreadCount = threadCount > 0 ? threadCount : Environment.ProcessorCount,
            BatchCharCount = batchCharCount is >= 1 and <= 7 ? batchCharCount : 4,
            Palindrome = true,
        });

    public Task StopSearch(int instanceId)
    {
        MotelySearchSession.Get(instanceId).CancelSearch();
        return Task.CompletedTask;
    }

    public Task<string> AnalyzeSeed(int instanceId, string seed, string deck, string stake)
    {
        try
        {
            var dto = MotelySeedAnalyzer.AnalyzeToDto(seed, deck, stake);
            return Task.FromResult(JsonSerializer.Serialize(dto, AnalysisJsonContext.Default.SeedAnalysisDto));
        }
        catch (Exception ex)
        {
            var dto = new SeedAnalysisDto { Seed = seed, Deck = deck, Stake = stake, Error = ex.Message };
            return Task.FromResult(JsonSerializer.Serialize(dto, AnalysisJsonContext.Default.SeedAnalysisDto));
        }
    }

    public Task<string> GetVersion() =>
        Task.FromResult(MotelyBuildVersion.For(typeof(MotelyCore).Assembly));

    public Task<bool> IsSimdEnabled() =>
        Task.FromResult(Vector128.IsHardwareAccelerated);

    public Task<int> GetProcessorCount() =>
        Task.FromResult(Environment.ProcessorCount);

    public Task<bool> ValidateJaml(string jamlContent) =>
        Task.FromResult(JamlConfigLoader.TryLoad(jamlContent, out _, out _));

    public Task<string> ValidateJamlWithError(string jamlContent)
    {
        if (JamlConfigLoader.TryLoad(jamlContent, out _, out var error))
            return Task.FromResult("");
        return Task.FromResult(error ?? "Unknown validation error");
    }

    private async Task<string> RunSearch(int instanceId, string jamlContent, MotelySearchRequest request)
    {
        var session = MotelySearchSession.Get(instanceId);
        if (session.IsSearchActive)
            return "error: search already running on this instance";

        var token = session.BeginSearch();
        try
        {
            var (status, _, _) = await Task.Run(() =>
                MotelySearchOrchestrator.RunSearch(
                    jamlContent, request,
                    (a, b, c) => _js.NotifySearchProgress(new SearchProgressPayload(instanceId, a, b, c)),
                    (seed, n) => _js.NotifySearchResult(new SearchResultPayload(instanceId, seed, n)),
                    token));
            return status;
        }
        catch (OperationCanceledException) { return "cancelled"; }
        catch (Exception ex) { return $"error: {ex.Message}"; }
        finally { session.EndSearch(); }
    }
}
