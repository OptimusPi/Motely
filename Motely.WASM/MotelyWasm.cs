using System.Diagnostics;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Intrinsics;
using System.Text.Json;
using Motely;
using Motely.Analysis;
using Motely.Filters;
using Motely.Filters.MotelyJson;
using Motely.Executors;
using System.Linq;

namespace Motely.WASM;

/// <summary>
/// JavaScript-callable exports for Motely seed analysis and searching.
/// All methods are static and use JSON for data exchange with JavaScript.
/// 
/// This module uses:
/// - WebAssembly SIMD for vectorized seed filtering (8 seeds at a time via Vector512)
/// - In-memory result storage only (no DuckDB in browser; Orchestration still references Motely.DB so it gets trimmed in)
/// - [JSImport] callbacks for progress/complete (MS best practice: C# pushes to JS)
/// </summary>
public static partial class MotelyWasm
{
    /// <summary>JS callback: progress updates during search. Host must set globalThis.MotelyWasmOnProgress.</summary>
    [JSImport("globalThis.MotelyWasmOnProgress")]
    private static partial void InvokeProgress(string progressJson);

    /// <summary>JS callback: final result when search completes. Host must set globalThis.MotelyWasmOnComplete.</summary>
    [JSImport("globalThis.MotelyWasmOnComplete")]
    private static partial void InvokeComplete(string resultJson);

    /// <summary>JS callback: each matching seed as it is found. Host must set globalThis.MotelyWasmOnResult.</summary>
    [JSImport("globalThis.MotelyWasmOnResult")]
    private static partial void InvokeResult(string seed, int score, string talliesJson);
    private static CancellationTokenSource? _searchCts;
    private static volatile bool _isSearchRunning;
    private static long _searchedCount;
    private static int _foundCount;
    private static Stopwatch? _searchStopwatch;
    private static IMotelySearchContext? _currentSearchContext;
    private static int _searchThreadCount;
    private static string? _lastSearchResultJson;

    /// <summary>
    /// Analyze a specific seed and return JSON with all ante data.
    /// Uses SIMD-optimized analysis. Async so JS can await (browser WASM disallows sync C# calls from JS).
    /// </summary>
    [JSExport]
    public static async Task<string> AnalyzeSeed(string seed, string deck, string stake, int minAnte, int maxAnte, string optionsJson)
    {
        try
        {
            seed = NormalizeSeed(seed);
            if (string.IsNullOrEmpty(seed))
                return JsonSerializer.Serialize(new ErrorDto { Error = "Invalid seed format" }, MotelyAotJsonContext.Default.ErrorDto);
            if (!Enum.TryParse<MotelyDeck>(deck, true, out var deckEnum))
                return JsonSerializer.Serialize(new ErrorDto { Error = $"Invalid deck: {deck}" }, MotelyAotJsonContext.Default.ErrorDto);
            if (!Enum.TryParse<MotelyStake>(stake, true, out var stakeEnum))
                return JsonSerializer.Serialize(new ErrorDto { Error = $"Invalid stake: {stake}" }, MotelyAotJsonContext.Default.ErrorDto);

            minAnte = Math.Clamp(minAnte, 1, 8);
            maxAnte = Math.Clamp(maxAnte, minAnte, 8);

            var config = new MotelySeedAnalysisConfig(seed, deckEnum, stakeEnum);
            var analysis = MotelySeedAnalyzer.Analyze(config);
            var result = ConvertToDto(seed, deck, stake, analysis, minAnte, maxAnte);
            return JsonSerializer.Serialize(result, MotelyAotJsonContext.Default.SeedAnalysisDto);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new ErrorDto { Error = ex.Message }, MotelyAotJsonContext.Default.ErrorDto);
        }
    }

    /// <summary>
    /// Search for seeds matching a JAML filter using SIMD vectorized filtering.
    /// Pushes progress via globalThis.MotelyWasmOnProgress and final result via globalThis.MotelyWasmOnComplete (MS callback pattern).
    /// Async so JS can await (browser WASM disallows sync C# calls from JS).
    /// </summary>
    [JSExport]
    public static async Task<string> SearchSeeds(string jamlFilterJson, string? seedList, int threadCount, int maxResults = 1000)
    {
        if (_isSearchRunning)
        {
            var busyJson = JsonSerializer.Serialize(new ErrorDto { Error = "Search already in progress" }, MotelyAotJsonContext.Default.ErrorDto);
            _lastSearchResultJson = busyJson;
            PushComplete(busyJson);
            return busyJson;
        }

        try
        {
            _isSearchRunning = true;
            _searchedCount = 0;
            _foundCount = 0;
            _searchCts = new CancellationTokenSource();
            _searchStopwatch = Stopwatch.StartNew();

            if (!JamlConfigLoader.TryLoadFromJamlString(jamlFilterJson, out var config, out var error))
            {
                var errJson = JsonSerializer.Serialize(new ErrorDto { Error = $"Invalid JAML filter: {error}" }, MotelyAotJsonContext.Default.ErrorDto);
                _lastSearchResultJson = errJson;
                PushComplete(errJson);
                return errJson;
            }
            if (config == null)
            {
                var errJson = JsonSerializer.Serialize(new ErrorDto { Error = "Failed to parse JAML config" }, MotelyAotJsonContext.Default.ErrorDto);
                _lastSearchResultJson = errJson;
                PushComplete(errJson);
                return errJson;
            }

            threadCount = threadCount <= 0 ? Environment.ProcessorCount : Math.Clamp(threadCount, 1, Environment.ProcessorCount);
            _searchThreadCount = threadCount;

            var searchParams = new JsonSearchParams
            {
                Threads = threadCount,
                BatchSize = 4,
                MaxResults = maxResults > 0 ? Math.Min(maxResults, 100_000) : 1000,
                SeedList = !string.IsNullOrEmpty(seedList)
                    ? seedList.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    : null,
                CancellationToken = _searchCts.Token,
                ResultCallback = r => PushResult(r.Seed, r.Score, r.TallyColumns)
            };

            _currentSearchContext = MotelySearchOrchestrator.LaunchWithContext(config, searchParams, useInMemoryStorage: true);

            // Progress loop: push updates to JS (MS best practice: C# calls into JS)
            _ = Task.Run(async () =>
            {
                try
                {
                    while (_isSearchRunning && !_searchCts!.Token.IsCancellationRequested)
                    {
                        await Task.Delay(200, _searchCts.Token).ConfigureAwait(false);
                        if (!_isSearchRunning) break;
                        PushProgress(GetProgressJsonSync());
                    }
                }
                catch (OperationCanceledException) { /* Expected on cancellation */ }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[WASM] Progress loop error: {ex.Message}");
                }
            }, _searchCts.Token);

            try
            {
                await Task.Run(() =>
                {
                    _currentSearchContext.Start(_searchCts!.Token);
                    _currentSearchContext.AwaitCompletion();
                }, _searchCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _currentSearchContext?.Cancel();
                _searchStopwatch?.Stop();
                var cancelJson = JsonSerializer.Serialize(new SearchResponseDto { Cancelled = true, Results = Array.Empty<SearchHitDto>(), TotalSearched = _currentSearchContext?.TotalSeedsSearched ?? 0 }, MotelyAotJsonContext.Default.SearchResponseDto);
                _lastSearchResultJson = cancelJson;
                PushComplete(cancelJson);
                return cancelJson;
            }

            _searchStopwatch?.Stop();
            _searchedCount = _currentSearchContext.TotalSeedsSearched;
            _foundCount = (int)_currentSearchContext.MatchingSeeds;
            // KISS: results were pushed via ResultCallback -> MotelyWasmOnResult. Complete sends summary only.
            var json = JsonSerializer.Serialize(new SearchResponseDto { Results = Array.Empty<SearchHitDto>(), TotalSearched = _searchedCount, FoundCount = _foundCount, Cancelled = false }, MotelyAotJsonContext.Default.SearchResponseDto);
            _lastSearchResultJson = json;
            PushComplete(json);
            return json;
        }
        catch (OperationCanceledException)
        {
            var cancelJson = JsonSerializer.Serialize(new SearchResponseDto { Cancelled = true, Results = Array.Empty<SearchHitDto>(), TotalSearched = _searchedCount }, MotelyAotJsonContext.Default.SearchResponseDto);
            _lastSearchResultJson = cancelJson;
            PushComplete(cancelJson);
            return cancelJson;
        }
        catch (Exception ex)
        {
            var errJson = JsonSerializer.Serialize(new ErrorDto { Error = ex.Message }, MotelyAotJsonContext.Default.ErrorDto);
            _lastSearchResultJson = errJson;
            PushComplete(errJson);
            return errJson;
        }
        finally
        {
            _isSearchRunning = false;
            _currentSearchContext?.Dispose();
            _currentSearchContext = null;
            _searchCts?.Dispose();
            _searchCts = null;
            _searchStopwatch = null;
        }
    }

    private static void PushProgress(string progressJson)
    {
        try { InvokeProgress(progressJson); } catch { /* Host may not set globalThis.MotelyWasmOnProgress */ }
    }

    private static void PushResult(string seed, int score, List<int>? tallies)
    {
        try
        {
            // Comma-separated to avoid trim warning (no List<int> in MotelyAotJsonContext)
            var talliesStr = tallies != null && tallies.Count > 0 ? string.Join(",", tallies) : "";
            InvokeResult(seed, score, talliesStr);
        }
        catch { /* Host may not set globalThis.MotelyWasmOnResult */ }
    }

    private static void PushComplete(string resultJson)
    {
        try { InvokeComplete(resultJson); } catch { /* Host may not set globalThis.MotelyWasmOnComplete */ }
    }

    /// <summary>
    /// Cancel an in-progress search.
    /// </summary>
    [JSExport]
    public static Task CancelSearch()
    {
        _searchCts?.Cancel();
        _currentSearchContext?.Cancel();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Check if a search is currently running.
    /// </summary>
    [JSExport]
    public static async Task<bool> IsSearchRunning() => _isSearchRunning;

    /// <summary>
    /// Get the last search result JSON (set when a search completes). UI polls this after IsSearchRunning becomes false.
    /// </summary>
    [JSExport]
    public static Task<string> GetLastSearchResult()
    {
        var json = _lastSearchResultJson ?? "null";
        _lastSearchResultJson = null;
        return Task.FromResult(json);
    }

    /// <summary>
    /// Get current search progress (for polling; prefer callbacks via globalThis.MotelyWasmOnProgress).
    /// </summary>
    [JSExport]
    public static Task<string> GetSearchProgress()
    {
        var json = GetProgressJsonSync();
        return Task.FromResult(json);
    }

    private static string GetProgressJsonSync()
    {
        var elapsed = _searchStopwatch?.Elapsed.TotalSeconds ?? 0;
        var searched = _currentSearchContext?.TotalSeedsSearched ?? _searchedCount;
        var found = _currentSearchContext?.ResultCount ?? _foundCount;
        var seedsPerSecond = elapsed > 0 ? searched / elapsed : 0;
        var status = _currentSearchContext?.Status.ToString() ?? "Idle";
        return JsonSerializer.Serialize(new ProgressDto { SearchedCount = searched, FoundCount = found, Status = status, PercentComplete = 0, SeedsPerSecond = seedsPerSecond, ThreadCount = _searchThreadCount }, MotelyAotJsonContext.Default.ProgressDto);
    }

    /// <summary>
    /// Get version information.
    /// </summary>
    [JSExport]
    public static async Task<string> GetVersion()
    {
        return JsonSerializer.Serialize(new VersionDto { Version = "1.0.0", Runtime = "browser-wasm", Features = new[] { "analyze", "search", "jaml", "simd", "duckdb" } }, MotelyAotJsonContext.Default.VersionDto);
    }

    /// <summary>
    /// Validate a JAML filter without searching.
    /// </summary>
    [JSExport]
    public static async Task<string> ValidateJaml(string jamlString)
    {
        try
        {
            if (!JamlConfigLoader.TryLoadFromJamlString(jamlString, out var config, out var error))
                return JsonSerializer.Serialize(new ValidateResultDto { Valid = false, Error = error ?? "Unknown parse error" }, MotelyAotJsonContext.Default.ValidateResultDto);
            return JsonSerializer.Serialize(new ValidateResultDto { Valid = true, Name = config?.Name, Deck = config?.Deck, Stake = config?.Stake }, MotelyAotJsonContext.Default.ValidateResultDto);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new ValidateResultDto { Valid = false, Error = ex.Message }, MotelyAotJsonContext.Default.ValidateResultDto);
        }
    }

    /// <summary>
    /// Get processor count for thread hints.
    /// </summary>
    [JSExport]
    public static async Task<int> GetProcessorCount() => Environment.ProcessorCount;

    #region Private Implementation

    private static string NormalizeSeed(string seed)
    {
        if (string.IsNullOrWhiteSpace(seed))
            return string.Empty;

        seed = seed.Trim().ToUpperInvariant().Replace('0', 'O');

        if (seed.Length > 8 || seed.Length == 0)
            return string.Empty;

        foreach (var c in seed)
        {
            if (!((c >= 'A' && c <= 'Z') || (c >= '1' && c <= '9')))
                return string.Empty;
        }

        return seed;
    }

    private static IEnumerable<string> GenerateSequentialSeeds(int count)
    {
        const string chars = "123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        var indices = new int[8];

        for (int i = 0; i < count; i++)
        {
            var seedChars = new char[8];
            for (int j = 0; j < 8; j++)
                seedChars[j] = chars[indices[j]];

            yield return new string(seedChars);

            // Increment (base-35 counter)
            for (int j = 7; j >= 0; j--)
            {
                indices[j]++;
                if (indices[j] < chars.Length)
                    break;
                indices[j] = 0;
            }
        }
    }

    private static SeedAnalysisDto ConvertToDto(
        string seed, string deck, string stake, 
        MotelySeedAnalysis analysis,
        int minAnte, int maxAnte)
    {
        var erraticComposition = analysis.ErraticDeckComposition?.Split(
            ',',
            StringSplitOptions.RemoveEmptyEntries
        ) ?? Array.Empty<string>();

        var result = new SeedAnalysisDto
        {
            Seed = seed,
            Deck = deck,
            Stake = stake,
            ErraticDeckComposition = erraticComposition,
            Twos = erraticComposition.Count(c => c.StartsWith("2_")),
            Error = analysis.Error,
            Antes = analysis.Antes?
                .Where(a => a.Ante >= minAnte && a.Ante <= maxAnte)
                .Select(ante => new AnteAnalysisDto
                {
                    Ante = ante.Ante,
                    Boss = FormatUtils.FormatBoss(ante.Boss),
                    Voucher = FormatUtils.FormatVoucher(ante.Voucher),
                    SmallBlindTag = FormatUtils.FormatTag(ante.SmallBlindTag),
                    BigBlindTag = FormatUtils.FormatTag(ante.BigBlindTag),
                    DrawOrder = ante.DrawOrder ?? string.Empty,
                    ShopQueue = ante.ShopQueue.Select(item => new ShopItemDto
                    {
                        Id = item.ToString(),
                        Name = FormatUtils.FormatItem(item)
                    }).ToArray(),
                    Packs = ante.Packs.Select(pack => new PackDto
                    {
                        Type = FormatUtils.FormatPackName(pack.Type),
                        Items = pack.Items.Select(item => FormatUtils.FormatItem(item)).ToArray()
                    }).ToArray()
                }).ToArray() ?? Array.Empty<AnteAnalysisDto>()
        };

        return result;
    }

    private record SearchResult
    {
        public string Seed { get; init; } = string.Empty;
        public int Score { get; init; }
        public int[]? Tallies { get; init; }
    }

    #endregion
}
