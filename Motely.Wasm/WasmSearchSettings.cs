using Motely;
using Motely.Filters;
using Motely.Filters.Native;

namespace Motely.Wasm;

/// <summary>
/// Bootsharp-exportable search builder. Wraps core <see cref="IMotelySearchSettings"/> without
/// exposing ref-struct filters, <c>Action&lt;&gt;</c> chain methods, or generic settings types to JS.
/// </summary>
/// <remarks>
/// No primary constructor — Bootsharp serializes public ctor params and would crawl
/// <see cref="IMotelySearchSettings"/> (and every filter interface) into JS imports.
/// </remarks>
public sealed class WasmSearchSettings
{
    private IMotelySearchSettings _inner;

    internal WasmSearchSettings(IMotelySearchSettings inner) => _inner = inner;

    public WasmSearchSettings WithThreadCount(int threadCount) =>
        new(_inner.WithThreadCount(threadCount));

    public WasmSearchSettings WithBatchCharacterCount(int batchCharacterCount) =>
        new(_inner.WithBatchCharacterCount(batchCharacterCount));

    public WasmSearchSettings WithStartBatchIndex(long startBatchIndex) =>
        new(_inner.WithStartBatchIndex(startBatchIndex));

    public WasmSearchSettings WithEndBatchIndex(long endBatchIndex) =>
        new(_inner.WithEndBatchIndex(endBatchIndex));

    public WasmSearchSettings WithListSearch(string[] seeds, int seedCount = -1) =>
        new(_inner.WithListSearch(seeds, seedCount));

    public WasmSearchSettings WithRandomSearch(int count) => new(_inner.WithRandomSearch(count));

    public WasmSearchSettings WithAestheticSearch(JamlAesthetic aesthetic) =>
        new(_inner.WithAestheticSearch(aesthetic));

    public WasmSearchSettings WithSequentialSearch() => new(_inner.WithSequentialSearch());

    public WasmSearchSettings WithDeck(MotelyDeck deck) => new(_inner.WithDeck(deck));

    public WasmSearchSettings WithStake(MotelyStake stake) => new(_inner.WithStake(stake));

    public WasmSearchSettings WithProgressReportIntervalMs(long intervalMs) =>
        new(_inner.WithProgressReportIntervalMs(intervalMs));

    public WasmSearchSettings WithCsvOutput(bool csvOutput) => new(_inner.WithCsvOutput(csvOutput));

    public WasmSearchSettings WithQuietMode(bool quietMode) => new(_inner.WithQuietMode(quietMode));

    public WasmSearchSettings WithAutoScoreCutoff(bool enabled = true) =>
        new(_inner.WithAutoScoreCutoff(enabled));

    /// <summary>
    /// Attaches <see cref="JimmolateFilterDesc"/> using the JS <see cref="Program.JimmolateProbe"/> import
    /// (assign before <c>bootsharp.boot()</c>). Same engine path as
    /// <c>WithAdditionalFilter(new JimmolateFilterDesc(...))</c> in C#.
    /// </summary>
    public WasmSearchSettings WithJimmolate() =>
        new(_inner.WithAdditionalFilter(new JimmolateFilterDesc(Program.RunJimmolateImport)));

    public IMotelySearch CreateSearch() => _inner.CreateSearch();

    public IMotelySearch Start(CancellationToken cancellationToken = default) =>
        _inner.Start(cancellationToken);
}
