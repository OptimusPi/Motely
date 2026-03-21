namespace Motely.BrowserWasm.Interop;

/// <summary>Exported WASM API (formerly static JSExport surface). Progress/results use <see cref="IMotelyJsUi"/> events.</summary>
public interface IMotelyWasmBackend
{
    int CreateInstance();
    void DestroyInstance(int id);

    Task<string> StartJamlSearch(
        int instanceId, string jamlContent, int threadCount, int batchCharCount,
        int startBatch, int endBatch);

    Task<string> StartSeedListSearch(
        int instanceId, string jamlContent, int threadCount, int batchCharCount,
        IReadOnlyList<string> seeds);

    Task<string> StartKeywordSearch(
        int instanceId, string jamlContent, int threadCount, int batchCharCount,
        IReadOnlyList<string> keywords, string padding);

    Task<string> StartRandomSearch(
        int instanceId, string jamlContent, int threadCount, int batchCharCount, int count);

    Task<string> StartPalindromeSearch(
        int instanceId, string jamlContent, int threadCount, int batchCharCount);

    Task StopSearch(int instanceId);

    Task<string> AnalyzeSeed(int instanceId, string seed, string deck, string stake);

    Task<string> GetVersion();
    Task<bool> IsSimdEnabled();
    Task<int> GetProcessorCount();

    Task<bool> ValidateJaml(string jamlContent);
    Task<string> ValidateJamlWithError(string jamlContent);

    /// <summary>
    /// Infinite shop item stream: get <paramref name="count"/> items starting at <paramref name="offset"/>
    /// for a given seed/deck/stake/ante. Deterministic and stateless — same inputs always produce same output.
    /// </summary>
    Task<string> GetShopItems(string seed, string deck, string stake, int ante, int offset, int count);
}
