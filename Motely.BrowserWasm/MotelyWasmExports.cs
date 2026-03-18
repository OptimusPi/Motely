using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Intrinsics;
using System.Runtime.Versioning;
using System.Text.Json;
using Motely;
using Motely.Analysis;
using Motely.Executors;
using Motely.Filters;

namespace Motely.BrowserWasm;

/// <summary>
/// [JSExport] surface for the Motely WASM npm package.
/// Plain synchronous calls — JS calls in, C# runs, returns.
/// Same as how Avalonia calls Motely from C#.
/// JSON only for AnalyzeSeed (genuinely nested).
/// </summary>
[SupportedOSPlatform("browser")]
public static partial class MotelyWasmExports
{
    // ── Search state ─────────────────────────────────────────────────────

    private static IMotelySearch? _currentSearch;
    private static CancellationTokenSource? _currentCts;
    private static readonly object _searchLock = new();

    private static string? _cachedVersion;

    // ── Simple getters ───────────────────────────────────────────────────

    [JSExport]
    public static string GetVersion() =>
        _cachedVersion ??= MotelyBuildVersion.For(typeof(MotelyWasmExports).Assembly);

    [JSExport]
    public static bool IsSimdEnabled() => Vector128.IsHardwareAccelerated;

    [JSExport]
    public static int GetProcessorCount() => Environment.ProcessorCount;

    [JSExport]
    public static bool ValidateJaml(string jamlContent) =>
        JamlConfigLoader.TryLoad(jamlContent, out _, out _);

    [JSExport]
    public static string ValidateJamlWithError(string jamlContent)
    {
        if (JamlConfigLoader.TryLoad(jamlContent, out _, out var error))
            return "";
        return error ?? "Unknown validation error";
    }

    // ── Search (mirrors CLI) ─────────────────────────────────────────────

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
            if (!JamlConfigLoader.TryLoad(jamlContent, out var config, out var parseError) || config == null)
                return Task.FromResult($"error: {parseError ?? "Failed to parse JAML"}");

            if (!config.HasAnyClauses)
                return Task.FromResult("error: no clauses in JAML");

            threadCount = Math.Clamp(threadCount, 1, Environment.ProcessorCount);
            batchCharCount = Math.Clamp(batchCharCount, 1, 7);

            var request = new MotelySearchRequest
            {
                ThreadCount = threadCount,
                BatchCharCount = batchCharCount,
            };

            var (plan, _, prepareError) = MotelySearchOrchestrator.PrepareSearch(config, request);
            if (prepareError != null || plan == null)
                return Task.FromResult($"error: {prepareError ?? "Search could not be prepared"}");

            StopSearch();

            var settings = plan.Settings;

            settings = settings.WithProgressCallback(prog =>
                onProgress(prog.SeedsSearched, prog.MatchingSeeds, (long)prog.ElapsedTime.TotalMilliseconds));

            if (plan.ShouldClauseCount > 0)
                settings = settings.WithScoredResultCallback(tally => onResult(tally.Seed, tally.Score));
            else
                settings = settings.WithSeedMatchCallback(seed => onResult(seed, 0));

            var cts = new CancellationTokenSource();
            bool cancelled;
            using var search = settings.CreateSearch();
            lock (_searchLock) { _currentSearch = search; _currentCts = cts; }
            try
            {
                search.Start(cts.Token);
                cancelled = cts.Token.IsCancellationRequested;
            }
            catch (OperationCanceledException) { cancelled = true; }
            finally
            {
                lock (_searchLock) { _currentSearch = null; _currentCts = null; }
            }

            return Task.FromResult(cancelled ? "cancelled" : "ok");
        }
        catch (Exception ex)
        {
            return Task.FromResult($"error: {ex.Message}");
        }
    }

    [JSExport]
    public static void StopSearch()
    {
        lock (_searchLock) { _currentCts?.Cancel(); }
    }

    // ── Analyze (JSON — genuinely nested) ────────────────────────────────

    [JSExport]
    public static string AnalyzeSeed(string seed, string deck, string stake)
    {
        try
        {
            if (!Enum.TryParse<MotelyDeck>(deck, true, out var deckEnum))
                return ErrorJson($"Unknown deck: {deck}");
            if (!Enum.TryParse<MotelyStake>(stake, true, out var stakeEnum))
                return ErrorJson($"Unknown stake: {stake}");

            var analysis = MotelySeedAnalyzer.Analyze(
                new MotelySeedAnalysisConfig(seed, deckEnum, stakeEnum));

            var dto = new SeedAnalysisDto
            {
                Seed = seed,
                Deck = deck,
                Stake = stake,
                Error = analysis.Error,
                ErraticDeckComposition = analysis.ErraticDeckComposition
                    ?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    ?? [],
                Antes = analysis.Antes.Select(a => new AnteAnalysisDto
                {
                    Ante = a.Ante,
                    Boss = FormatUtils.FormatBoss(a.Boss),
                    Voucher = FormatUtils.FormatVoucher(a.Voucher),
                    SmallBlindTag = FormatUtils.FormatTag(a.SmallBlindTag),
                    BigBlindTag = FormatUtils.FormatTag(a.BigBlindTag),
                    DrawOrder = a.DrawOrder ?? "",
                    ShopQueue = a.ShopQueue
                        .Select(item => new ShopItemDto { Id = item.Type.ToString(), Name = FormatUtils.FormatItem(item) })
                        .ToArray(),
                    Packs = a.Packs
                        .Select(p => new PackDto
                        {
                            Type = FormatUtils.FormatPackName(p.Type),
                            Items = p.Items.Select(FormatUtils.FormatItem).ToArray(),
                        })
                        .ToArray(),
                }).ToArray(),
            };

            return JsonSerializer.Serialize(dto, AnalysisJsonContext.Default.SeedAnalysisDto);
        }
        catch (Exception ex)
        {
            return ErrorJson(ex.Message);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static string ErrorJson(string message) =>
        JsonSerializer.Serialize(
            new ErrorDto { Error = message },
            MotelyJsonContext.Default.ErrorDto);
}
