using System.Diagnostics;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Intrinsics;
using System.Text;
using System.Text.Json;
using Motely;
using Motely.Analysis;
using Motely.Filters;
using Motely.Filters.MotelyJson;
using Motely.Executors;
using Motely.Orchestration.Browser;
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
    public static async Task<string> SearchSeeds(string jamlFilterJson, string? seedList, int threadCount)
    {
        var options = new SearchOptionsDto
        {
            ThreadCount = threadCount,
            SeedList = seedList
        };
        return await SearchSeedsCore(jamlFilterJson, options);
    }

    /// <summary>
    /// Search for seeds matching a JAML filter with advanced options (keyword, batch ranges, random, etc).
    /// optionsJson follows SearchOptionsDto (camelCase).
    /// </summary>
    [JSExport]
    public static async Task<string> SearchSeedsWithOptions(string jamlFilterJson, string optionsJson)
    {
        SearchOptionsDto? options = null;
        if (!string.IsNullOrWhiteSpace(optionsJson))
        {
            try
            {
                options = JsonSerializer.Deserialize(optionsJson, MotelyAotJsonContext.Default.SearchOptionsDto);
            }
            catch (Exception ex)
            {
                var errJson = JsonSerializer.Serialize(new ErrorDto { Error = $"Invalid options JSON: {ex.Message}" }, MotelyAotJsonContext.Default.ErrorDto);
                _lastSearchResultJson = errJson;
                PushComplete(errJson);
                return errJson;
            }
        }

        return await SearchSeedsCore(jamlFilterJson, options ?? new SearchOptionsDto());
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
        return JsonSerializer.Serialize(new VersionDto { Version = "1.0.4", Runtime = "browser-wasm", Features = new[] { "balatro", "seed-searcher", "analyze", "search", "jaml", "simd" } }, MotelyAotJsonContext.Default.VersionDto);
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

    private static async Task<string> SearchSeedsCore(string jamlFilterJson, SearchOptionsDto options)
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

            int threadCount = options.ThreadCount.GetValueOrDefault();
            threadCount = threadCount <= 0 ? Environment.ProcessorCount : Math.Clamp(threadCount, 1, Environment.ProcessorCount);
            _searchThreadCount = threadCount;

            int batchSize = options.BatchSize.GetValueOrDefault(4);
            if (batchSize < 1 || batchSize >= 8)
            {
                var errJson = JsonSerializer.Serialize(new ErrorDto { Error = $"batchSize must be between 1 and 7 (got {batchSize})" }, MotelyAotJsonContext.Default.ErrorDto);
                _lastSearchResultJson = errJson;
                PushComplete(errJson);
                return errJson;
            }

            long maxBatches = (long)Math.Pow(35, 8 - batchSize);
            long startBatch = options.StartBatch.GetValueOrDefault(0);
            long endBatch = options.EndBatch.GetValueOrDefault(0);

            if (!string.IsNullOrWhiteSpace(options.StartSeed))
            {
                var seedStr = NormalizeSeed(options.StartSeed);
                if (seedStr.Length != 8)
                {
                    var errJson = JsonSerializer.Serialize(new ErrorDto { Error = "startSeed must be 8 characters" }, MotelyAotJsonContext.Default.ErrorDto);
                    _lastSearchResultJson = errJson;
                    PushComplete(errJson);
                    return errJson;
                }
                startBatch = SeedMath.SeedToBatchIndex(seedStr, batchSize);
            }
            else if (options.StartPercent.HasValue)
            {
                var startPct = options.StartPercent.Value;
                if (startPct < 0 || startPct > 100)
                {
                    var errJson = JsonSerializer.Serialize(new ErrorDto { Error = "startPercent must be 0-100" }, MotelyAotJsonContext.Default.ErrorDto);
                    _lastSearchResultJson = errJson;
                    PushComplete(errJson);
                    return errJson;
                }
                startBatch = (long)(maxBatches * startPct / 100.0);
            }

            if (options.EndPercent.HasValue)
            {
                var endPct = options.EndPercent.Value;
                if (endPct < 0 || endPct > 100)
                {
                    var errJson = JsonSerializer.Serialize(new ErrorDto { Error = "endPercent must be 0-100" }, MotelyAotJsonContext.Default.ErrorDto);
                    _lastSearchResultJson = errJson;
                    PushComplete(errJson);
                    return errJson;
                }
                endBatch = (long)(maxBatches * endPct / 100.0);
            }

            if (endBatch > maxBatches)
            {
                var errJson = JsonSerializer.Serialize(new ErrorDto { Error = $"endBatch too large (max for batchSize {batchSize}: {maxBatches:N0})" }, MotelyAotJsonContext.Default.ErrorDto);
                _lastSearchResultJson = errJson;
                PushComplete(errJson);
                return errJson;
            }
            if (endBatch != 0 && startBatch >= endBatch)
            {
                var errJson = JsonSerializer.Serialize(new ErrorDto { Error = $"startBatch must be less than endBatch (start={startBatch}, end={endBatch})" }, MotelyAotJsonContext.Default.ErrorDto);
                _lastSearchResultJson = errJson;
                PushComplete(errJson);
                return errJson;
            }

            var (cutoffValue, cutoffMode) = ParseCutoff(options.Cutoff);

            IEnumerable<string>? seedList = null;
            int? keywordSeedCount = null;
            if (!string.IsNullOrWhiteSpace(options.Keyword))
            {
                var keyword = options.Keyword.Trim();
                seedList = GenerateKeywordSeeds(keyword, options.Padding, out var count);
                keywordSeedCount = count;
            }
            else if (!string.IsNullOrWhiteSpace(options.SeedList))
            {
                if (!TryParseSeedList(options.SeedList, out seedList, out var seedError))
                {
                    var errJson = JsonSerializer.Serialize(new ErrorDto { Error = seedError ?? "Invalid seed list" }, MotelyAotJsonContext.Default.ErrorDto);
                    _lastSearchResultJson = errJson;
                    PushComplete(errJson);
                    return errJson;
                }
            }

            var searchParams = new JsonSearchParams
            {
                Threads = threadCount,
                BatchSize = batchSize,
                StartBatch = (ulong)Math.Max(0, startBatch),
                EndBatch = (ulong)Math.Max(0, endBatch),
                Cutoff = cutoffValue,
                CutoffMode = cutoffMode,
                SpecificSeed = options.SpecificSeed,
                SeedList = seedList,
                KeywordSeedCount = keywordSeedCount,
                RandomSeeds = options.RandomSeeds,
                PalindromeSeeds = options.Palindrome == true,
                CancellationToken = _searchCts.Token,
                ResultCallback = r => PushResult(r.Seed, r.Score, r.TallyColumns)
            };

            _currentSearchContext = BrowserWASMOrchestrator.LaunchWithContext(config, searchParams);

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
                catch (OperationCanceledException) { }
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

    private static (int cutoffValue, ScoreCutoffMode cutoffMode) ParseCutoff(string? cutoff)
    {
        if (string.IsNullOrWhiteSpace(cutoff))
            return (0, ScoreCutoffMode.None);

        var cutoffStr = cutoff.Trim().ToLowerInvariant();
        if (cutoffStr == "auto" || cutoffStr == "smart")
            return (0, ScoreCutoffMode.AutoSmart);
        if (cutoffStr == "best")
            return (0, ScoreCutoffMode.AutoBest);
        if (int.TryParse(cutoffStr, out var parsed))
            return (parsed, parsed > 0 ? ScoreCutoffMode.Manual : ScoreCutoffMode.None);

        return (0, ScoreCutoffMode.None);
    }

    private static bool TryParseSeedList(string seedListRaw, out IEnumerable<string> seeds, out string? error)
    {
        var list = new List<string>();
        var tokens = seedListRaw
            .Split(new[] { ',', '\n', '\r', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var token in tokens)
        {
            var seed = NormalizeSeed(token);
            if (string.IsNullOrEmpty(seed) || seed.Length != 8)
            {
                error = $"Invalid seed '{token}'. Seeds must be 8 characters (A-Z, 1-9).";
                seeds = Array.Empty<string>();
                return false;
            }
            list.Add(seed);
        }

        error = null;
        seeds = list;
        return list.Count > 0;
    }

    private static IEnumerable<string> GenerateKeywordSeeds(string keyword, string? paddingChars, out int seedCount)
    {
        keyword = keyword.ToUpperInvariant().Replace('0', 'O');
        foreach (var c in keyword)
        {
            if (!"ABCDEFGHIJKLMNOPQRSTUVWXYZ123456789".Contains(c))
                throw new ArgumentException($"Invalid character '{c}' in keyword. Only A-Z and 1-9 allowed.");
        }

        if (keyword.Length > 8)
            throw new ArgumentException($"Keyword too long ({keyword.Length} chars). Max 8 chars allowed.");

        char[] validChars;
        if (!string.IsNullOrEmpty(paddingChars))
        {
            paddingChars = paddingChars.ToUpperInvariant().Replace('0', 'O');
            var paddingSet = new HashSet<char>();
            foreach (var c in paddingChars)
            {
                if (!"ABCDEFGHIJKLMNOPQRSTUVWXYZ123456789".Contains(c))
                    throw new ArgumentException($"Invalid padding character '{c}'. Only A-Z and 1-9 allowed.");
                paddingSet.Add(c);
            }

            if (paddingSet.Count == 0)
                throw new ArgumentException("Padding characters must contain at least one valid character (A-Z, 1-9).");

            validChars = paddingSet.ToArray();
        }
        else
        {
            validChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ123456789".ToCharArray();
        }

        int maxPad = 8 - keyword.Length;
        seedCount = (int)Math.Min(GetCountOfSeeds(keyword, maxPad, validChars.Length), int.MaxValue);
        return GenerateKeywordSeedsEnumerable(keyword, maxPad, validChars);
    }

    private static IEnumerable<string> GenerateKeywordSeedsEnumerable(string keyword, int maxPad, char[] validChars)
    {
        yield return keyword;

        if (maxPad > 0)
        {
            for (int padLen = 1; padLen <= maxPad; padLen++)
            {
                foreach (var seed in GeneratePaddedSeeds(keyword, padLen, validChars))
                {
                    yield return seed;
                }
            }
        }
    }

    private static IEnumerable<string> GeneratePaddedSeeds(string keyword, int padLen, char[] validChars)
    {
        if (padLen <= 0)
        {
            yield return keyword;
            yield break;
        }

        if (padLen == 1)
        {
            foreach (var c in validChars)
            {
                yield return c + keyword;
                yield return keyword + c;
            }
        }
        else if (padLen == 2)
        {
            foreach (var c1 in validChars)
            {
                foreach (var c2 in validChars)
                {
                    yield return $"{c1}{c2}{keyword}";
                    yield return $"{keyword}{c1}{c2}";
                    yield return $"{c1}{keyword}{c2}";
                }
            }
        }
        else if (padLen == 3)
        {
            foreach (var c1 in validChars)
            {
                foreach (var c2 in validChars)
                {
                    foreach (var c3 in validChars)
                    {
                        yield return $"{c1}{c2}{c3}{keyword}";
                        yield return $"{keyword}{c1}{c2}{c3}";
                        yield return $"{c1}{keyword}{c2}{c3}";
                        yield return $"{c1}{c2}{keyword}{c3}";
                    }
                }
            }
        }
        else
        {
            foreach (var seed in GenerateLargePaddedSeeds(keyword, padLen, validChars))
            {
                yield return seed;
            }
        }
    }

    private static IEnumerable<string> GenerateLargePaddedSeeds(string keyword, int padLen, char[] validChars)
    {
        var padding = new char[padLen];
        return GenerateLargePaddedSeedsRec(keyword, validChars, padding, 0);
    }

    private static IEnumerable<string> GenerateLargePaddedSeedsRec(string keyword, char[] validChars, char[] padding, int depth)
    {
        if (depth == padding.Length)
        {
            for (int pos = 0; pos <= padding.Length; pos++)
            {
                var builder = new StringBuilder(8);
                builder.Append(padding, 0, pos);
                builder.Append(keyword);
                builder.Append(padding, pos, padding.Length - pos);
                yield return builder.ToString();
            }
            yield break;
        }

        foreach (var c in validChars)
        {
            padding[depth] = c;
            foreach (var seed in GenerateLargePaddedSeedsRec(keyword, validChars, padding, depth + 1))
            {
                yield return seed;
            }
        }
    }

    private static long GetCountOfSeeds(string keyword, int maxPad, int validCharCount)
    {
        long total = 1;
        for (int padLen = 1; padLen <= maxPad; padLen++)
        {
            long permutations = (long)Math.Pow(validCharCount, padLen);
            long positions = padLen + 1;
            total += positions * permutations;
        }
        return total;
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
