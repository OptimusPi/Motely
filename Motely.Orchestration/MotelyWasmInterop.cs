#if BROWSER
using Bootsharp;
using Motely.Analysis;
using Motely.Filters;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;

namespace Motely.Executors;

public static partial class MotelyWasm
{
    private sealed class BrowserSingleSearchSession(string seed, MotelyDeck deck, MotelyStake stake) : IDisposable
    {
        private readonly MotelySeedRouterDesc _router = new(seed, deck, stake);
        private MotelySingleShopItemStream _shopStream;
        private bool _hasShopStream;

        public void BeginShopStream(int ante)
        {
            var ctx = _router.CreateContext();
            _shopStream = ctx.CreateShopItemStream(ante);
            _hasShopStream = true;
        }

        public string GetNextShopItemJson()
        {
            if (!_hasShopStream)
                throw new InvalidOperationException("beginShopStream must be called before getNextShopItem.");

            var ctx = _router.CreateContext();
            var item = ctx.GetNextShopItem(ref _shopStream);
            return SerializeShopItem(item);
        }

        public void Dispose() => _router.Dispose();
    }

    private static readonly ConcurrentDictionary<int, BrowserSingleSearchSession> BrowserSingleSearchSessions = new();
    private static int _nextBrowserSingleSearchSessionId;
    private static int _nextCompatibilityInstanceId;
    private static readonly string BrowserWasmVersion =
        typeof(MotelyWasm).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(MotelyWasm).Assembly.GetName().Version?.ToString()
        ?? "unknown";

    [JSEvent]
    public static partial void OnProgress(long searched, long found, long elapsedMs);

    [JSEvent]
    public static partial void OnResult(string seed, int score);

    [JSEvent]
    public static partial void OnComplete(string status, int seedsFound, int highestScore);

    [JSInvokable]
    public static string? ValidateJaml(string jamlContent)
    {
        if (!JamlConfigLoader.TryLoad(jamlContent, out _, out var error))
            return error ?? "Invalid JAML.";
        return null;
    }

    private static string Run(MotelySearchRequest request, string jamlContent)
    {
        var (status, seedsFound, highestScore) = MotelySearchOrchestrator.RunSearch(
            jamlContent, request,
            onProgress: (searched, found, elapsed) => OnProgress(searched, found, elapsed),
            onResult: (seed, score) => OnResult(seed, score)
        );
        OnComplete(status, seedsFound, highestScore);
        return $"{status}|{seedsFound}|{highestScore}";
    }

    [JSInvokable]
    public static string RunSearch(
        string jamlContent, int threadCount, int batchCharCount,
        int startBatch, int endBatch)
        => Run(new MotelySearchRequest
        {
            ThreadCount = threadCount,
            BatchCharCount = batchCharCount,
            StartBatch = startBatch,
            EndBatch = endBatch
        }, jamlContent);

    [JSInvokable]
    public static string RunKeywordSearch(
        string jamlContent, int threadCount, string[] keywords,
        string? padding = null)
        => Run(new MotelySearchRequest
        {
            ThreadCount = threadCount,
            BatchCharCount = 1,
            Keywords = keywords,
            Padding = padding
        }, jamlContent);

    [JSInvokable]
    public static string RunSeedListSearch(
        string jamlContent, int threadCount, string[] seeds)
        => Run(new MotelySearchRequest
        {
            ThreadCount = threadCount,
            BatchCharCount = 1,
            Seeds = seeds
        }, jamlContent);

    [JSInvokable]
    public static string RunRandomSearch(
        string jamlContent, int threadCount, int count)
        => Run(new MotelySearchRequest
        {
            ThreadCount = threadCount,
            BatchCharCount = 1,
            RandomSeeds = count
        }, jamlContent);

    [JSInvokable]
    public static string RunPalindromeSearch(
        string jamlContent, int threadCount)
        => Run(new MotelySearchRequest
        {
            ThreadCount = threadCount,
            BatchCharCount = 1,
            Palindrome = true
        }, jamlContent);

    [JSInvokable]
    public static int CreateInstance()
        => Interlocked.Increment(ref _nextCompatibilityInstanceId);

    [JSInvokable]
    public static void DestroyInstance(int instanceId)
    {
    }

    [JSInvokable]
    public static string AnalyzeSeed(int instanceId, string seed, string deck, string stake)
    {
        var dto = MotelySeedAnalyzer.AnalyzeToDto(seed, deck, stake);
        return JsonSerializer.Serialize(dto, AnalysisJsonContext.Default.SeedAnalysisDto);
    }

    [JSInvokable]
    public static int CreateSingleSearchContext(string seed, string deck, string stake)
    {
        var (deckEnum, stakeEnum) = ParseDeckAndStake(deck, stake);
        var sessionId = Interlocked.Increment(ref _nextBrowserSingleSearchSessionId);
        var session = new BrowserSingleSearchSession(seed, deckEnum, stakeEnum);

        if (!BrowserSingleSearchSessions.TryAdd(sessionId, session))
        {
            session.Dispose();
            throw new InvalidOperationException($"Failed to register single-search context {sessionId}.");
        }

        return sessionId;
    }

    [JSInvokable]
    public static void DisposeSingleSearchContext(int sessionId)
    {
        if (BrowserSingleSearchSessions.TryRemove(sessionId, out var session))
        {
            session.Dispose();
        }
    }

    [JSInvokable]
    public static void BeginShopStream(int sessionId, int ante)
        => GetSingleSearchSession(sessionId).BeginShopStream(ante);

    [JSInvokable]
    public static string GetNextShopItemJson(int sessionId)
        => GetSingleSearchSession(sessionId).GetNextShopItemJson();

    [JSInvokable]
    public static string GetVersion()
        => BrowserWasmVersion;

    private static BrowserSingleSearchSession GetSingleSearchSession(int sessionId)
    {
        if (BrowserSingleSearchSessions.TryGetValue(sessionId, out var session))
            return session;

        throw new ArgumentOutOfRangeException(nameof(sessionId), $"Unknown single-search context: {sessionId}");
    }

    private static (MotelyDeck Deck, MotelyStake Stake) ParseDeckAndStake(string deck, string stake)
    {
        if (!Enum.TryParse<MotelyDeck>(deck, true, out var deckEnum))
            throw new ArgumentException($"Unknown deck: '{deck}'", nameof(deck));
        if (!Enum.TryParse<MotelyStake>(stake, true, out var stakeEnum))
            throw new ArgumentException($"Unknown stake: '{stake}'", nameof(stake));

        return (deckEnum, stakeEnum);
    }

    private static string SerializeShopItem(MotelyItem item)
    {
        var dto = new ShopItemDto
        {
            Id = item.Type.ToString(),
            Name = FormatUtils.FormatItem(item),
            Value = item.Value,
        };
        return JsonSerializer.Serialize(dto, AnalysisJsonContext.Default.ShopItemDto);
    }
}
#endif
