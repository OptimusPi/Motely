#nullable enable
using Motely;
using Motely.Analysis;
using Motely.Filters;
using MotelyJaml;
using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;

namespace Motely.BrowserWasm;

public static partial class MotelyBrowserApi
{
    private static MotelyJamlSearchBuilder _builder = null!;
    private static IMotelySearch? _currentSearch;
    private static readonly object _sync = new();

    // Config store: loadJaml/compileJummy return an ID, search uses it.
    private static readonly Dictionary<string, JamlConfig> _configs = new();
    private static int _nextConfigId;

    public static void Initialize(MotelyJamlSearchBuilder builder)
    {
        _builder = builder;
    }

    // ── Version ─────────────────────────────────────────────────────────────
    [JSExport] public static string GetVersion() => _builder.GetVersion();

    // ── JAML / Jummy parsing ────────────────────────────────────────────────

    private static string StoreConfig(JamlConfig config)
    {
        var id = $"cfg_{_nextConfigId++}";
        _configs[id] = config;
        return id;
    }

    private static JamlConfig GetConfig(string configId)
    {
        if (!_configs.TryGetValue(configId, out var cfg))
            throw new InvalidOperationException($"Unknown config ID: {configId}");
        return cfg;
    }

    [JSExport]
    public static string LoadJaml(string jaml)
    {
        if (!JamlConfigLoader.TryLoad(jaml, out var config, out var error))
            throw new InvalidOperationException(error ?? "Invalid JAML.");
        return StoreConfig(config!);
    }

    [JSExport]
    public static string CompileJummy(string jummy)
    {
        if (!JummyCompiler.TryCompile(jummy, out var jamlYaml, out var compileErr))
            throw new InvalidOperationException(compileErr ?? "Jummy compile failed.");
        if (!JamlConfigLoader.TryLoad(jamlYaml, out var config, out var loadErr))
            throw new InvalidOperationException(loadErr ?? "Invalid JAML after Jummy compile.");
        return StoreConfig(config!);
    }

    [JSExport]
    public static string ValidateJaml(string jaml)
    {
        if (JamlConfigLoader.TryLoad(jaml, out _, out var error))
            return "valid";
        return error ?? "Invalid JAML.";
    }

    [JSExport]
    public static string GetConfigDeck(string configId) => GetConfig(configId).Deck.ToString();

    [JSExport]
    public static string GetConfigStake(string configId) => GetConfig(configId).Stake.ToString();

    // ── Search ──────────────────────────────────────────────────────────────

    [JSExport]
    public static void StartRandomSearch(string configId, int randomSeedCount)
    {
        var config = GetConfig(configId);
        JamlSearchBuilder.EnsureRunnablePlan(config);
        var settings = _builder.LoadConfig(config).Random(randomSeedCount).BuildSettings();
        StartSearchInternal(settings);
    }

    [JSExport]
    public static void StopSearch() { lock (_sync) { _currentSearch?.Cancel(); } }

    // ── Seed analysis ───────────────────────────────────────────────────────

    [JSExport]
    public static string AnalyzeSeed(string seed, string deckName, string stakeName)
    {
        var deck = Enum.Parse<MotelyDeck>(deckName, ignoreCase: true);
        var stake = Enum.Parse<MotelyStake>(stakeName, ignoreCase: true);
        var analysis = MotelySeedAnalyzer.Analyze(new(seed, deck, stake));
        return SerializeAnalysis(analysis);
    }

    private static string SerializeAnalysis(MotelyLegacyTextAnalyzer analysis)
    {
        if (!string.IsNullOrEmpty(analysis.Error))
            return JsonSerializer.Serialize(new { error = analysis.Error });

        var antes = analysis.Antes.Select(a => new
        {
            ante = a.Ante,
            boss = a.Boss.ToString(),
            voucher = a.Voucher.ToString(),
            smallBlindTag = a.SmallBlindTag.ToString(),
            bigBlindTag = a.BigBlindTag.ToString(),
            drawOrder = a.DrawOrder,
            shopQueue = a.ShopQueue.Select(FormatUtils.FormatItem).ToArray(),
            packs = a.Packs.Select(p => new
            {
                type = FormatUtils.FormatPackName(p.Type),
                items = p.Items.Select(FormatUtils.FormatItem).ToArray()
            }).ToArray()
        }).ToArray();

        return JsonSerializer.Serialize(new
        {
            seed,
            deck = analysis.Deck?.ToString(),
            erraticDeckComposition = analysis.ErraticDeckComposition,
            antes
        });
    }

    private static void StartSearchInternal(IMotelySearchSettings settings)
    {
        long lastSeedsSearched = 0;
        long lastMatchingSeeds = 0;

        settings.WithProgressCallback(p => {
            lastSeedsSearched = p.SeedsSearched;
            lastMatchingSeeds = p.MatchingSeeds;
            OnProgress(p.SeedsSearched, p.MatchingSeeds);
        });

        settings.WithScoredResultCallback(t => {
            OnResult(t.Seed, t.Score, t.TallyColumns.ToArray());
        });

        var search = settings.Start();
        lock (_sync) { _currentSearch?.Cancel(); _currentSearch = search; }
        
        _ = NotifyOnCompletionAsync(search, () => lastSeedsSearched, () => lastMatchingSeeds);
    }

    private static async Task NotifyOnCompletionAsync(IMotelySearch search, Func<long> getSeeds, Func<long> getMatches)
    {
        try 
        { 
            await search.WaitForCompletionAsync(); 
            OnComplete("completed", getSeeds(), getMatches()); 
        }
        catch (OperationCanceledException) 
        { 
            OnComplete("cancelled", getSeeds(), getMatches()); 
        }
        catch (Exception ex) 
        { 
            OnComplete($"error: {ex.Message}", getSeeds(), getMatches()); 
        }
        finally 
        { 
            lock (_sync) { if (ReferenceEquals(_currentSearch, search)) _currentSearch = null; } 
            search.Dispose(); 
        }
    }

    [JSImport("SearchEvents.onProgress", "Bootsharp")]
    private static partial void OnProgress([JSMarshalAs<JSType.BigInt>] long seedsSearched, [JSMarshalAs<JSType.BigInt>] long matchingSeeds);

    [JSImport("SearchEvents.onResult", "Bootsharp")]
    private static partial void OnResult(string seed, int score, int[] tally);

    [JSImport("SearchEvents.onComplete", "Bootsharp")]
    private static partial void OnComplete(string status, [JSMarshalAs<JSType.BigInt>] long totalSeedsSearched, [JSMarshalAs<JSType.BigInt>] long matchingSeeds);
}
