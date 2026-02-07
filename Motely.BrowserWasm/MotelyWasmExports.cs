using System.Collections.Concurrent;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Text.Json;
using Motely;
using Motely.Analysis;
using Motely.Executors;
using Motely.Filters;

namespace Motely.BrowserWasm;

/// <summary>
/// All [JSExport] methods for the Motely WASM npm package.
/// JSON-in, JSON-out. Main-thread only (search runs on background threads, JS polls).
/// Uses MotelyAotJsonContext for AOT-safe serialization.
/// </summary>
[SupportedOSPlatform("browser")]
public static partial class MotelyWasmExports
{
    private static readonly ConcurrentDictionary<string, IMotelySearchContext> _activeSearches = new();

    // ──────────────────────────────── Version / Capabilities ────────────────────────────────

    [JSExport]
    public static string GetVersion()
    {
        var dto = new VersionDto
        {
            Version = typeof(MotelyWasmExports).Assembly.GetName().Version?.ToString() ?? "0.0.0",
            Runtime = "browser-wasm",
            Features = GetFeatureList(),
        };
        return JsonSerializer.Serialize(dto, MotelyAotJsonContext.Default.VersionDto);
    }

    [JSExport]
    public static string GetCapabilities()
    {
        var dto = new CapabilitiesDto
        {
            Simd = IsSimdEnabled(),
            Threads = IsThreadingEnabled(),
            ProcessorCount = GetProcessorCount(),
            Runtime = "browser-wasm",
            Version = typeof(MotelyWasmExports).Assembly.GetName().Version?.ToString() ?? "0.0.0",
            Timestamp = DateTime.UtcNow.ToString("O"),
        };
        return JsonSerializer.Serialize(dto, WasmJsonContext.Default.CapabilitiesDto);
    }

    [JSExport]
    public static bool IsSimdEnabled()
    {
#if NET10_0_OR_GREATER
        return System.Runtime.Intrinsics.Vector128.IsHardwareAccelerated;
#else
        return false;
#endif
    }

    [JSExport]
    public static bool IsThreadingEnabled() => Thread.CurrentThread.ManagedThreadId >= 0
        && Environment.ProcessorCount > 0; // Will be > 1 if threads are enabled

    [JSExport]
    public static int GetProcessorCount() => Environment.ProcessorCount;

    // ──────────────────────────────── Analyzer ────────────────────────────────

    /// <summary>
    /// Analyze a single seed. Returns JSON SeedAnalysisDto.
    /// </summary>
    [JSExport]
    public static string AnalyzeSeed(string seed, string deck, string stake)
    {
        try
        {
            if (!Enum.TryParse<MotelyDeck>(deck, ignoreCase: true, out var deckEnum))
                return JsonSerializer.Serialize(new ErrorDto { Error = $"Unknown deck: {deck}" },
                    MotelyAotJsonContext.Default.ErrorDto);

            if (!Enum.TryParse<MotelyStake>(stake, ignoreCase: true, out var stakeEnum))
                return JsonSerializer.Serialize(new ErrorDto { Error = $"Unknown stake: {stake}" },
                    MotelyAotJsonContext.Default.ErrorDto);

            var cfg = new MotelySeedAnalysisConfig(seed, deckEnum, stakeEnum);
            var analysis = MotelySeedAnalyzer.Analyze(cfg);

            if (!string.IsNullOrEmpty(analysis.Error))
                return JsonSerializer.Serialize(new ErrorDto { Error = analysis.Error },
                    MotelyAotJsonContext.Default.ErrorDto);

            var dto = MapAnalysisToDto(analysis, seed, deck, stake);
            return JsonSerializer.Serialize(dto, MotelyAotJsonContext.Default.SeedAnalysisDto);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new ErrorDto { Error = ex.Message },
                MotelyAotJsonContext.Default.ErrorDto);
        }
    }

    // ──────────────────────────────── JAML Validation ────────────────────────────────

    /// <summary>
    /// Validate a JAML string. Returns JSON ValidateResultDto.
    /// </summary>
    [JSExport]
    public static string ValidateJaml(string jamlContent)
    {
        try
        {
            var config = ConfigFormatConverter.LoadFromJamlString(jamlContent);
            if (config == null)
            {
                return JsonSerializer.Serialize(
                    new ValidateResultDto { Valid = false, Error = "Failed to parse JAML" },
                    MotelyAotJsonContext.Default.ValidateResultDto);
            }

            return JsonSerializer.Serialize(
                new ValidateResultDto
                {
                    Valid = true,
                    Name = config.Name,
                    Deck = config.Deck,
                    Stake = config.Stake,
                },
                MotelyAotJsonContext.Default.ValidateResultDto);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(
                new ValidateResultDto { Valid = false, Error = ex.Message },
                MotelyAotJsonContext.Default.ValidateResultDto);
        }
    }

    // ──────────────────────────────── Search (async via polling) ────────────────────────────────

    /// <summary>
    /// Start a JAML search. Returns searchId string (not JSON).
    /// JS polls GetSearchStatus(searchId) for progress and results.
    /// </summary>
    [JSExport]
    public static string StartJamlSearch(string jamlContent, string optionsJson)
    {
        try
        {
            var config = ConfigFormatConverter.LoadFromJamlString(jamlContent);
            if (config == null)
                return ErrorJson("Failed to parse JAML filter");

            // Parse optional search options
            SearchOptionsDto? options = null;
            if (!string.IsNullOrEmpty(optionsJson) && optionsJson != "{}")
            {
                options = JsonSerializer.Deserialize(optionsJson,
                    MotelyAotJsonContext.Default.SearchOptionsDto);
            }

            var parameters = new JsonSearchParams
            {
                Threads = options?.ThreadCount ?? Math.Max(1, Environment.ProcessorCount - 1),
                BatchSize = options?.BatchSize ?? 4,
                Cutoff = options?.Cutoff != null ? int.Parse(options.Cutoff) : 0,
                Quiet = true, // No console output in browser
                NoFancy = true,
            };

            if (options?.StartBatch.HasValue == true)
                parameters.StartBatch = (ulong)options.StartBatch.Value;
            if (options?.EndBatch.HasValue == true)
                parameters.EndBatch = (ulong)options.EndBatch.Value;
            if (options?.SpecificSeed != null)
                parameters.SpecificSeed = options.SpecificSeed;
            if (options?.RandomSeeds.HasValue == true)
                parameters.RandomSeeds = options.RandomSeeds.Value;
            if (options?.Palindrome == true)
                parameters.PalindromeSeeds = true;

            // Override deck/stake from options if provided
            if (!string.IsNullOrEmpty(config.Deck))
                parameters.Deck = config.Deck;
            if (!string.IsNullOrEmpty(config.Stake))
                parameters.Stake = config.Stake;

            // Launch with in-memory storage (no DuckDB in browser)
            var context = MotelySearchOrchestrator.LaunchWithContext(
                config, parameters, useInMemoryStorage: true);

            var searchId = context.SearchId;
            _activeSearches[searchId] = context;

            // Start search on background thread (JSExport is main-thread only)
            var thread = new Thread(() =>
            {
                try
                {
                    context.Start();
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Search {searchId} failed: {ex.Message}");
                }
            })
            {
                IsBackground = true,
                Name = $"MotelySearch-{searchId[..Math.Min(16, searchId.Length)]}",
            };
            thread.Start();

            return searchId;
        }
        catch (Exception ex)
        {
            return ErrorJson(ex.Message);
        }
    }

    /// <summary>
    /// Get status + top results for an active search. Returns JSON ProgressDto.
    /// </summary>
    [JSExport]
    public static string GetSearchStatus(string searchId, int resultLimit)
    {
        if (!_activeSearches.TryGetValue(searchId, out var context))
            return ErrorJson($"Unknown search: {searchId}");

        try
        {
            var limit = resultLimit > 0 ? resultLimit : 50;
            var results = context.GetTopResults(limit);

            var dto = new SearchStatusDto
            {
                SearchId = searchId,
                Status = context.Status.ToString(),
                IsRunning = context.Status == MotelySearchStatus.Running,
                TotalSeedsSearched = context.TotalSeedsSearched,
                MatchingSeeds = context.MatchingSeeds,
                ResultCount = context.ResultCount,
                ElapsedMs = (long)context.ElapsedTime.TotalMilliseconds,
                Results = results.Select(r => new SearchHitDto
                {
                    Seed = r.Seed,
                    Score = r.Score,
                    Tallies = r.Tallies?.ToArray(),
                }).ToArray(),
            };

            return JsonSerializer.Serialize(dto, WasmJsonContext.Default.SearchStatusDto);
        }
        catch (Exception ex)
        {
            return ErrorJson(ex.Message);
        }
    }

    /// <summary>
    /// Stop an active search.
    /// </summary>
    [JSExport]
    public static void StopSearch(string searchId)
    {
        if (_activeSearches.TryGetValue(searchId, out var context))
        {
            context.Cancel();
        }
    }

    /// <summary>
    /// Dispose and remove a completed/stopped search from memory.
    /// </summary>
    [JSExport]
    public static void DisposeSearch(string searchId)
    {
        if (_activeSearches.TryRemove(searchId, out var context))
        {
            context.Dispose();
        }
    }

    // ──────────────────────────────── Helpers ────────────────────────────────

    private static string ErrorJson(string message) =>
        JsonSerializer.Serialize(new ErrorDto { Error = message },
            MotelyAotJsonContext.Default.ErrorDto);

    private static string[] GetFeatureList()
    {
        var features = new List<string> { "analyzer", "jaml-search", "jaml-validate" };
        if (IsSimdEnabled()) features.Add("simd");
        if (IsThreadingEnabled()) features.Add("threads");
        return features.ToArray();
    }

    private static SeedAnalysisDto MapAnalysisToDto(
        MotelySeedAnalysis analysis, string seed, string deck, string stake)
    {
        return new SeedAnalysisDto
        {
            Seed = seed,
            Deck = deck,
            Stake = stake,
            Error = analysis.Error,
            ErraticDeckComposition = analysis.ErraticDeckComposition?.Split(',',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [],
            Antes = analysis.Antes.Select(a => new AnteAnalysisDto
            {
                Ante = a.Ante,
                Boss = a.Boss.ToString(),
                Voucher = a.Voucher.ToString(),
                SmallBlindTag = a.SmallBlindTag.ToString(),
                BigBlindTag = a.BigBlindTag.ToString(),
                DrawOrder = a.DrawOrder ?? "",
                ShopQueue = a.ShopQueue.Select(item => new ShopItemDto
                {
                    Id = item.Type.ToString(),
                    Name = item.ToString(),
                }).ToArray(),
                Packs = a.Packs.Select(p => new PackDto
                {
                    Type = p.Type.ToString(),
                    Items = p.Items.Select(i => i.ToString()).ToArray(),
                }).ToArray(),
            }).ToArray(),
        };
    }
}
