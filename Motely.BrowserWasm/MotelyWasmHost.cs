#nullable enable
using Motely;
using Motely.Analysis;
using Motely.Filters;
using MotelyJaml;

namespace Motely.BrowserWasm;

public interface IMotelyWasmHost
{
    string GetVersion();
    string LoadJaml(string jaml);
    string CompileJummy(string jummy);
    MotelyDeck GetConfigDeck(string configId);
    MotelyStake GetConfigStake(string configId);
    
    // Flattened context methods
    string OpenSingleSearchContext(string seed, MotelyDeck deck, MotelyStake stake);
    string ContextGetSeed(string ctxId);
    MotelyBossBlind ContextGetBossForAnte(string ctxId, int ante);
    MotelyVoucher ContextGetAnteFirstVoucher(string ctxId, int ante);
    MotelyTag ContextGetNextTag(string ctxId, int ante);
    MotelyItem ContextGetNextShopItem(string ctxId, int ante);
    bool ContextGetNextLuckyMoney(string ctxId, double baseLuck = 1);
    bool ContextGetNextLuckyMult(string ctxId, double baseLuck = 1);
    int ContextGetNextMisprintMult(string ctxId);
    void ContextClose(string ctxId);

    void StartConfiguredSearch(string configId, int batchCharCount, long startBatch = 0, long endBatch = 0);
    void StartConfiguredSearchFromJaml(string jaml, int batchCharCount, long startBatch = 0, long endBatch = 0);
    void StartRandomSearch(string configId, int randomSeedCount);
    void StartRandomSearchFromJaml(string jaml, int randomSeedCount);
    void StartAestheticSearch(string configId, JamlAesthetic aesthetic);
}

internal static class InternalSearchRunner
{
    private static IMotelySearch? _currentSearch;
    private static readonly object _sync = new();

    public static void StartSearch(
        MotelyJamlSearchBuilder builder, 
        JamlConfig config, 
        SearchMode mode,
        int randomSeedCount,
        int batchCharCount,
        long startBatch,
        long endBatch,
        JamlAesthetic aesthetic,
        string keywordsCsv,
        string paddingChars,
        string[] seeds,
        ISearchEvents events)
    {
        IMotelySearchSettings settings = mode switch
        {
            SearchMode.Random => builder.LoadConfig(config).Random(randomSeedCount).BuildSettings(),
            SearchMode.Configured => builder.LoadConfig(config).Configured(batchCharCount, startBatch, endBatch).BuildSettings(),
            SearchMode.Aesthetic => builder.LoadConfig(config).Aesthetic(aesthetic).BuildSettings(),
            SearchMode.Keyword => builder.LoadConfig(config).Keywords(keywordsCsv, paddingChars).BuildSettings(),
            SearchMode.SeedList => builder.LoadConfig(config).SeedList(seeds).BuildSettings(),
            _ => throw new NotSupportedException()
        };

        long lastSeedsSearched = 0;
        long lastMatchingSeeds = 0;

        settings.WithProgressCallback(p => {
            lastSeedsSearched = p.SeedsSearched;
            lastMatchingSeeds = p.MatchingSeeds;
            // events.NotifyProgress(p.SeedsSearched, p.MatchingSeeds);
        });

        var search = settings.Start();
        lock (_sync) { _currentSearch?.Cancel(); _currentSearch = search; }
        
        // _ = NotifyOnCompletionAsync(search, events, () => lastSeedsSearched, () => lastMatchingSeeds);
    }

    public static void StopSearch() { lock (_sync) { _currentSearch?.Cancel(); } }

    private static async Task NotifyOnCompletionAsync(IMotelySearch search, ISearchEvents events, Func<long> getSeeds, Func<long> getMatches)
    {
        try { await search.WaitForCompletionAsync(); /* events.NotifyComplete("completed", getSeeds(), getMatches()); */ }
        catch (OperationCanceledException) { /* events.NotifyComplete("cancelled", getSeeds(), getMatches()); */ }
        catch (Exception ex) { /* events.NotifyComplete($"error: {ex.Message}", getSeeds(), getMatches()); */ }
        finally { lock (_sync) { if (ReferenceEquals(_currentSearch, search)) _currentSearch = null; } search.Dispose(); }
    }

    public enum SearchMode { Random, Configured, Aesthetic, Keyword, SeedList }
}

public sealed class MotelyWasmHost : IMotelyWasmHost
{
    private readonly MotelyJamlSearchBuilder _builder;
    private readonly ISearchEvents _events;
    private readonly Dictionary<string, JamlConfig> _configs = new();
    private readonly Dictionary<string, IMotelySingleSearchContextImpl> _contexts = new();

    public MotelyWasmHost(MotelyJamlSearchBuilder builder, ISearchEvents events)
    {
        _builder = builder;
        _events = events;
    }

    public string GetVersion() => _builder.GetVersion();

    public string LoadJaml(string jaml) => LoadJamlInternal(jaml);

    private string LoadJamlInternal(string jaml)
    {
        if (!JamlConfigLoader.TryLoad(jaml, out var config, out var error))
            throw new InvalidOperationException(error ?? "Invalid JAML.");
        JamlSearchBuilder.EnsureRunnablePlan(config);
        var id = config.Id;
        if (string.IsNullOrEmpty(id)) id = Guid.NewGuid().ToString();
        lock (_configs) { _configs[id] = config; }
        return id;
    }

    public string CompileJummy(string jummy)
    {
        if (!JummyCompiler.TryCompile(jummy, out var jamlYaml, out var compileErr))
            throw new InvalidOperationException(compileErr ?? "Jummy compile failed.");
        return LoadJamlInternal(jamlYaml);
    }

    public MotelyDeck GetConfigDeck(string configId) => GetConfigInternal(configId).Deck;
    public MotelyStake GetConfigStake(string configId) => GetConfigInternal(configId).Stake;

    private JamlConfig GetConfigInternal(string id)
    {
        lock (_configs) { if (_configs.TryGetValue(id, out var config)) return config; }
        throw new InvalidOperationException($"Config '{id}' not found.");
    }

    public string OpenSingleSearchContext(string seed, MotelyDeck deck, MotelyStake stake)
    {
        var id = Guid.NewGuid().ToString();
        var router = new MotelySeedRouterDesc(seed, deck, stake);
        var impl = new MotelySingleSearchContextImpl(router);
        lock (_contexts) { _contexts[id] = impl; }
        return id;
    }

    public string ContextGetSeed(string ctxId) => GetCtxInternal(ctxId).GetSeed();
    public MotelyBossBlind ContextGetBossForAnte(string ctxId, int ante) => GetCtxInternal(ctxId).GetBossForAnte(ante);
    public MotelyVoucher ContextGetAnteFirstVoucher(string ctxId, int ante) => GetCtxInternal(ctxId).GetAnteFirstVoucher(ante);
    public MotelyTag ContextGetNextTag(string ctxId, int ante) => GetCtxInternal(ctxId).GetNextTag(ante);
    public MotelyItem ContextGetNextShopItem(string ctxId, int ante) => GetCtxInternal(ctxId).GetNextShopItem(ante);
    public bool ContextGetNextLuckyMoney(string ctxId, double baseLuck) => GetCtxInternal(ctxId).GetNextLuckyMoney(baseLuck);
    public bool ContextGetNextLuckyMult(string ctxId, double baseLuck) => GetCtxInternal(ctxId).GetNextLuckyMult(baseLuck);
    public int ContextGetNextMisprintMult(string ctxId) => GetCtxInternal(ctxId).GetNextMisprintMult();
    public void ContextClose(string ctxId) { lock (_contexts) { _contexts.Remove(ctxId); } }

    private IMotelySingleSearchContextImpl GetCtxInternal(string id)
    {
        lock (_contexts) { if (_contexts.TryGetValue(id, out var ctx)) return ctx; }
        throw new InvalidOperationException($"Context '{id}' not found.");
    }

    public void StartRandomSearch(string configId, int randomSeedCount)
    {
        InternalSearchRunner.StartSearch(_builder, GetConfigInternal(configId), InternalSearchRunner.SearchMode.Random, randomSeedCount, 0, 0, 0, default, "", "", [], _events);
    }

    public void StartRandomSearchFromJaml(string jaml, int randomSeedCount)
    {
        InternalSearchRunner.StartSearch(_builder, LoadJamlCoreInternal(jaml), InternalSearchRunner.SearchMode.Random, randomSeedCount, 0, 0, 0, default, "", "", [], _events);
    }

    private JamlConfig LoadJamlCoreInternal(string jaml)
    {
        if (!JamlConfigLoader.TryLoad(jaml, out var config, out var error))
            throw new InvalidOperationException(error ?? "Invalid JAML.");
        JamlSearchBuilder.EnsureRunnablePlan(config);
        return config;
    }

    public void StopSearch() => InternalSearchRunner.StopSearch();

    public void StartConfiguredSearch(string configId, int batchCharCount, long startBatch = 0, long endBatch = 0) 
        => InternalSearchRunner.StartSearch(_builder, GetConfigInternal(configId), InternalSearchRunner.SearchMode.Configured, 0, batchCharCount, startBatch, endBatch, default, "", "", [], _events);
    
    public void StartConfiguredSearchFromJaml(string jaml, int batchCharCount, long startBatch = 0, long endBatch = 0)
        => InternalSearchRunner.StartSearch(_builder, LoadJamlCoreInternal(jaml), InternalSearchRunner.SearchMode.Configured, 0, batchCharCount, startBatch, endBatch, default, "", "", [], _events);

    public void StartAestheticSearch(string configId, JamlAesthetic aesthetic) 
        => InternalSearchRunner.StartSearch(_builder, GetConfigInternal(configId), InternalSearchRunner.SearchMode.Aesthetic, 0, 0, 0, 0, aesthetic, "", "", [], _events);

    public void StartKeywordSearch(string configId, string keywordsCsv, string paddingChars)
        => InternalSearchRunner.StartSearch(_builder, GetConfigInternal(configId), InternalSearchRunner.SearchMode.Keyword, 0, 0, 0, 0, default, keywordsCsv, paddingChars, [], _events);

    public void StartSeedListSearch(string configId, string[] seeds)
        => InternalSearchRunner.StartSearch(_builder, GetConfigInternal(configId), InternalSearchRunner.SearchMode.SeedList, 0, 0, 0, 0, default, "", "", seeds, _events);

    public void StartSeedListSearchFromJaml(string jaml, string[] seeds)
        => InternalSearchRunner.StartSearch(_builder, LoadJamlCoreInternal(jaml), InternalSearchRunner.SearchMode.SeedList, 0, 0, 0, 0, default, "", "", seeds, _events);
}
