using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.JavaScript.NodeApi;
using Motely;
using Motely.Analysis;
using Motely.Filters;
using CapabilitiesDto = global::Motely.CapabilitiesDto;
using ErrorDto = global::Motely.ErrorDto;
using SearchHitDto = global::Motely.SearchHitDto;
using SearchOptionsDto = global::Motely.SearchOptionsDto;
using SearchStatusDto = global::Motely.SearchStatusDto;
using ValidateResultDto = global::Motely.ValidateResultDto;
using VersionDto = global::Motely.VersionDto;

namespace Motely.NodeAddon;

/// <summary>
/// Node addon exports for Motely (node-api-dotnet). Same API surface as WASM: version, capabilities,
/// analyzeSeed, validateJaml, startJamlSearch (options JSON), GetSearchStatus, StopSearch, DisposeSearch.
/// Search runs on a thread-pool thread so the JS thread stays responsive. JS can poll GetSearchStatus for progress and results.
/// </summary>
[JSExport]
public static class MotelyNodeExports
{
    private static IMotelySearch? _currentSearch;
    private static CancellationTokenSource? _currentCts;
    private static string? _currentFilterId;
    private static readonly object SearchLock = new();
    private static readonly ConcurrentQueue<(string Seed, int Score)> ResultQueue = new();
    private static readonly List<(string Seed, int Score)> DrainedResults = new();

    private static string? _cachedVersion;
    private static string[]? _cachedFeatures;

    [JSExport]
    public static Task<string> GetVersionAsync()
    {
        var dto = new VersionDto
        {
            Version = GetCachedVersion(),
            Runtime = "node-addon",
            Features = GetFeatureList(),
        };
        return Task.FromResult(JsonSerializer.Serialize(dto, MotelyJsonContext.Default.VersionDto));
    }

    [JSExport]
    public static Task<string> GetCapabilitiesAsync()
    {
        var dto = new CapabilitiesDto
        {
            Simd = IsSimdEnabled(),
            Threads = true,
            AvailableThreadCount = Environment.ProcessorCount,
            ProcessorCount = Environment.ProcessorCount,
            Runtime = "node-addon",
            Version = GetCachedVersion(),
            Timestamp = DateTime.UtcNow.ToString("O"),
        };
        return Task.FromResult(
            JsonSerializer.Serialize(dto, MotelyJsonContext.Default.CapabilitiesDto)
        );
    }

    private static bool IsSimdEnabled() => System.Runtime.Intrinsics.Vector128.IsHardwareAccelerated;

    [JSExport]
    public static Task<string> AnalyzeSeedAsync(string seed, string deck, string stake)
    {
        var result = AnalyzeSeed(seed, deck, stake);
        return Task.FromResult(result);
    }

    private static string AnalyzeSeed(string seed, string deck, string stake)
    {
        try
        {
            if (!Enum.TryParse<MotelyDeck>(deck, true, out var deckEnum))
                return JsonSerializer.Serialize(
                    new ErrorDto { Error = $"Unknown deck: {deck}" },
                    MotelyJsonContext.Default.ErrorDto
                );

            if (!Enum.TryParse<MotelyStake>(stake, true, out var stakeEnum))
                return JsonSerializer.Serialize(
                    new ErrorDto { Error = $"Unknown stake: {stake}" },
                    MotelyJsonContext.Default.ErrorDto
                );

            var cfg = new MotelySeedAnalysisConfig(seed, deckEnum, stakeEnum);
            var analysis = MotelySeedAnalyzer.Analyze(cfg);

            if (!string.IsNullOrEmpty(analysis.Error))
                return JsonSerializer.Serialize(
                    new ErrorDto { Error = analysis.Error },
                    MotelyJsonContext.Default.ErrorDto
                );

            var dto = MapAnalysisToDto(analysis, seed, deckEnum, stakeEnum);
            return JsonSerializer.Serialize(dto, MotelyJsonContext.Default.SeedAnalysisDto);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(
                new ErrorDto { Error = ex.Message },
                MotelyJsonContext.Default.ErrorDto
            );
        }
    }

    [JSExport]
    public static Task<string> ValidateJamlAsync(string jamlContent)
    {
        var result = ValidateJaml(jamlContent);
        return Task.FromResult(result);
    }

    private static string ValidateJaml(string jamlContent)
    {
        try
        {
            if (
                !JamlConfigLoader.TryLoad(jamlContent, out var config, out var parseError)
                || config == null
            )
            {
                return JsonSerializer.Serialize(
                    new ValidateResultDto
                    {
                        Valid = false,
                        Error = parseError ?? "Failed to parse JAML",
                    },
                    MotelyJsonContext.Default.ValidateResultDto
                );
            }

            return JsonSerializer.Serialize(
                new ValidateResultDto
                {
                    Valid = true,
                    Name = config.Name,
                    Deck = config.Deck.ToString(),
                    Stake = config.Stake.ToString(),
                },
                MotelyJsonContext.Default.ValidateResultDto
            );
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(
                new ValidateResultDto { Valid = false, Error = ex.Message },
                MotelyJsonContext.Default.ValidateResultDto
            );
        }
    }

    [JSExport]
    public static async Task<string> StartJamlSearch(string jamlContent, string optionsJson)
    {
        try
        {
            if (
                !JamlConfigLoader.TryLoad(jamlContent, out var config, out var parseError)
                || config == null
            )
                return ErrorJson(parseError ?? "Failed to parse JAML filter");

            SearchOptionsDto? options = null;
            if (!string.IsNullOrEmpty(optionsJson) && optionsJson != "{}")
            {
                options = JsonSerializer.Deserialize(
                    optionsJson,
                    MotelyJsonContext.Default.SearchOptionsDto
                );
            }

            if (options == null)
                return ErrorJson("Search options are required. Provide threadCount and batchCharCount.");

            if (!options.ThreadCount.HasValue || options.ThreadCount.Value < 1)
                return ErrorJson("Invalid options.threadCount. Provide an integer >= 1.");

            if (
                !options.BatchCharCount.HasValue
                || options.BatchCharCount.Value < 1
                || options.BatchCharCount.Value > 7
            )
                return ErrorJson("Invalid options.batchCharCount. Provide an integer in range 1..7.");

            if (options.ThreadCount.Value > Environment.ProcessorCount)
            {
                return ErrorJson(
                    $"Invalid options.threadCount. Requested {options.ThreadCount.Value}, but runtime reports {Environment.ProcessorCount} available processor(s)."
                );
            }

            StopSearch();
            DrainedResults.Clear();
            while (ResultQueue.TryDequeue(out _)) { }

            var settings = JamlSearchBuilder
                .CreateSettings(config)
                .WithDeck(config.Deck)
                .WithStake(config.Stake)
                .WithThreadCount(options.ThreadCount.Value)
                .WithBatchCharacterCount(options.BatchCharCount.Value)
                .WithSeedMatchCallback(seed =>
                {
                    ResultQueue.Enqueue((seed, 0));
                })
                .WithProgressCallback(_ => { });

            if (options.StartBatch.HasValue)
                settings = settings.WithStartBatchIndex(options.StartBatch.Value);
            if (options.EndBatch.HasValue)
                settings = settings.WithEndBatchIndex(options.EndBatch.Value);

            if (options.SpecificSeed != null)
                settings = settings.WithListSearch([options.SpecificSeed], 1);
            else if (options.Seeds is { Length: > 0 })
                settings = settings.WithListSearch(options.Seeds);
            else if (!string.IsNullOrEmpty(options.Keyword))
            {
                string kw = options.Keyword.ToUpperInvariant();
                int padLen = MotelyCore.MaxSeedLength - kw.Length;
                if (padLen < 0)
                    return ErrorJson($"Keyword '{kw}' is too long (max {MotelyCore.MaxSeedLength} chars).");
                settings = settings.WithListSearch(MotelyCore.GeneratePaddedSeeds(kw, padLen));
            }
            else if (options.RandomSeeds.HasValue)
                settings = settings.WithRandomSearch(options.RandomSeeds.Value);
            else if (options.Palindrome == true)
                settings = settings.WithPalindromeSearch();
            else
                settings = settings.WithSequentialSearch();

            var cts = new CancellationTokenSource();
            var search = settings.CreateSearch();
            var filterId = MotelyRuntimeIds.GenerateFilterId(config);

            lock (SearchLock)
            {
                _currentSearch = search;
                _currentCts = cts;
                _currentFilterId = filterId;
            }

            try
            {
                await Task.Run(() => search.Start(cts.Token));
            }
            finally
            {
                lock (SearchLock)
                {
                    _currentSearch = null;
                    _currentCts = null;
                    _currentFilterId = null;
                }
            }

            DrainResultQueue();
            return BuildStatusJson(search);
        }
        catch (Exception ex)
        {
            return ErrorJson(ex.Message);
        }
    }

    [JSExport]
    public static Task<string> GetSearchStatus()
    {
        IMotelySearch? search;
        lock (SearchLock)
        {
            search = _currentSearch;
        }

        if (search == null)
            return Task.FromResult(ErrorJson("No active search"));

        DrainResultQueue();
        return Task.FromResult(BuildStatusJson(search));
    }

    [JSExport]
    public static void StopSearch()
    {
        lock (SearchLock)
        {
            _currentCts?.Cancel();
        }
    }

    [JSExport]
    public static async Task DisposeSearch()
    {
        IMotelySearch? search;
        CancellationTokenSource? cts;
        lock (SearchLock)
        {
            search = _currentSearch;
            cts = _currentCts;
            _currentSearch = null;
            _currentCts = null;
        }

        if (search != null)
        {
            cts?.Cancel();
            await search.WaitForCompletionAsync();
            search.Dispose();
        }
    }

    private static void DrainResultQueue()
    {
        while (ResultQueue.TryDequeue(out var r))
        {
            DrainedResults.Add(r);
        }
    }

    private static string ErrorJson(string message) =>
        JsonSerializer.Serialize(
            new ErrorDto { Error = message },
            MotelyJsonContext.Default.ErrorDto
        );

    private static string BuildStatusJson(IMotelySearch search)
    {
        var results = DrainedResults
            .Select(r => new SearchHitDto { Seed = r.Seed, Score = r.Score, Tallies = [] })
            .ToArray();
        var dto = new SearchStatusDto
        {
            FilterId = _currentFilterId ?? string.Empty,
            Status = search.IsCompleted ? "Completed" : "Running",
            IsRunning = !search.IsCompleted,
            TotalSeedsSearched = search.TotalSeedsSearched,
            MatchingSeeds = search.MatchingSeeds,
            ResultCount = results.Length,
            ElapsedMs = (long)search.ElapsedTime.TotalMilliseconds,
            Results = results,
        };

        return JsonSerializer.Serialize(dto, MotelyJsonContext.Default.SearchStatusDto);
    }

    private static string GetCachedVersion() =>
        _cachedVersion ??= MotelyBuildVersion.For(typeof(MotelyNodeExports).Assembly);

    private static string[] GetFeatureList()
    {
        if (_cachedFeatures is not null)
            return _cachedFeatures;
        var features = new List<string> { "analyzer", "jaml-search", "jaml-validate" };
        if (IsSimdEnabled())
            features.Add("simd");
        features.Add("threads");
        _cachedFeatures = features.ToArray();
        return _cachedFeatures;
    }

    private static SeedAnalysisDto MapAnalysisToDto(
        MotelySeedAnalysis analysis,
        string seed,
        MotelyDeck deck,
        MotelyStake stake
    )
    {
        return new SeedAnalysisDto
        {
            Seed = seed,
            Deck = deck.ToString(),
            Stake = stake.ToString(),
            Error = analysis.Error,
            ErraticDeckComposition =
                analysis.ErraticDeckComposition?.Split(
                    ',',
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
                ) ?? [],
            Antes = analysis
                .Antes.Select(a => new AnteAnalysisDto
                {
                    Ante = a.Ante,
                    Boss = a.Boss.ToString(),
                    Voucher = a.Voucher.ToString(),
                    SmallBlindTag = a.SmallBlindTag.ToString(),
                    BigBlindTag = a.BigBlindTag.ToString(),
                    DrawOrder = a.DrawOrder ?? "",
                    ShopQueue = a
                        .ShopQueue.Select(item => new ShopItemDto
                        {
                            Id = item.Type.ToString(),
                            Name = item.ToString(),
                        })
                        .ToArray(),
                    Packs = a
                        .Packs.Select(p => new PackDto
                        {
                            Type = p.Type.ToString(),
                            Items = p.Items.Select(i => i.ToString()).ToArray(),
                        })
                        .ToArray(),
                })
                .ToArray(),
        };
    }
}
