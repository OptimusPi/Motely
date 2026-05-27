using System;
using System.Threading;
using System.Threading.Tasks;
using Motely;

namespace Motely.Wasm;

/// <summary>
/// Bootsharp-exportable search handle. Wraps core <see cref="IMotelySearch"/> without
/// exposing CancellationToken parameters to JS, avoiding Bootsharp conversion exceptions on undefined/null tokens.
/// </summary>
public sealed class WasmSearch
{
    private readonly IMotelySearch _inner;

    internal WasmSearch(IMotelySearch inner) => _inner = inner;

    public long ElapsedMs
    {
        get
        {
            try { return _inner.ElapsedMs; }
            catch (Exception ex) { Program.ReportWasmError($"[WASM ElapsedMs ex] {ex}"); throw; }
        }
    }
    public long TotalSeedsSearched
    {
        get
        {
            try { return _inner.TotalSeedsSearched; }
            catch (Exception ex) { Program.ReportWasmError($"[WASM TotalSeedsSearched ex] {ex}"); throw; }
        }
    }
    public long MatchingSeeds
    {
        get
        {
            try { return _inner.MatchingSeeds; }
            catch (Exception ex) { Program.ReportWasmError($"[WASM MatchingSeeds ex] {ex}"); throw; }
        }
    }
    public long FilteredSeeds
    {
        get
        {
            try { return _inner.FilteredSeeds; }
            catch (Exception ex) { Program.ReportWasmError($"[WASM FilteredSeeds ex] {ex}"); throw; }
        }
    }
    public bool IsCompleted
    {
        get
        {
            try { return _inner.IsCompleted; }
            catch (Exception ex) { Program.ReportWasmError($"[WASM IsCompleted ex] {ex}"); throw; }
        }
    }
    public bool IsSequentialBatchSearch
    {
        get
        {
            try { return _inner.IsSequentialBatchSearch; }
            catch (Exception ex) { Program.ReportWasmError($"[WASM IsSequentialBatchSearch ex] {ex}"); throw; }
        }
    }
    public long BatchIndex
    {
        get
        {
            try { return _inner.BatchIndex; }
            catch (Exception ex) { Program.ReportWasmError($"[WASM BatchIndex ex] {ex}"); throw; }
        }
    }
    public long CompletedBatchCount
    {
        get
        {
            try { return _inner.CompletedBatchCount; }
            catch (Exception ex) { Program.ReportWasmError($"[WASM CompletedBatchCount ex] {ex}"); throw; }
        }
    }

    public void Start()
    {
        try
        {
            _inner.Start();
        }
        catch (Exception ex)
        {
            Program.ReportWasmError($"[WASM C# EXCEPTION in WasmSearch.Start] {ex}");
            throw;
        }
    }
    public Task RunSearchAsync() => _inner.RunSearchAsync();
    public void RunSearchUntilCompletion() => _inner.RunSearchUntilCompletion();
    public void AwaitCompletion() => _inner.AwaitCompletion();
    public async Task WaitForCompletionAsync()
    {
        try
        {
            await _inner.WaitForCompletionAsync();
        }
        catch (Exception ex)
        {
            Program.ReportWasmError($"[WASM C# EXCEPTION in WasmSearch.WaitForCompletionAsync] {ex}");
            throw;
        }
    }
    public void Cancel() => _inner.Cancel();
}
