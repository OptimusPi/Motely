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
    IMotelySingleSearchContext MotelySingleSearchContext(string seed, MotelyDeck deck, MotelyStake stake);
    MotelyBossBlind SingleGetBossForAnte(string seed, MotelyDeck deck, MotelyStake stake, int ante);
    MotelyVoucher SingleGetAnteFirstVoucher(string seed, MotelyDeck deck, MotelyStake stake, int ante);
    MotelyTag SingleGetNextTag(string seed, MotelyDeck deck, MotelyStake stake, int ante);
    MotelyItem SingleGetNextShopItem(string seed, MotelyDeck deck, MotelyStake stake, int ante);
    bool SingleGetNextLuckyMoney(string seed, MotelyDeck deck, MotelyStake stake, double baseLuck = 1);
    bool SingleGetNextLuckyMult(string seed, MotelyDeck deck, MotelyStake stake, double baseLuck = 1);
    int SingleGetNextMisprintMult(string seed, MotelyDeck deck, MotelyStake stake);
    void StartConfiguredSearch(string configId, int batchCharCount, long startBatch = 0, long endBatch = 0);
    void StartConfiguredSearchFromJaml(string jaml, int batchCharCount, long startBatch = 0, long endBatch = 0);
    void StartRandomSearch(string configId, int randomSeedCount);
    void StartRandomSearchFromJaml(string jaml, int randomSeedCount);
    void StartAestheticSearch(string configId, JamlAesthetic aesthetic);
    void StartKeywordSearch(string configId, string keywordsCsv, string paddingChars);
    void StartSeedListSearch(string configId, string[] seeds);
    void StartSeedListSearchFromJaml(string jaml, string[] seeds);
    void StopSearch();
}

public sealed class MotelyWasmHost : IMotelyWasmHost
{
    private readonly IInternalMotelyJamlSearchBuilder _builder;
    private readonly MotelySingleSearchContext _singleSearchContext;
    private readonly ISearchEvents _events;
    private readonly object _sync = new();
    private IMotelySearch? _currentSearch;
    private readonly Dictionary<string, JamlConfig> _configs = new();

    public MotelyWasmHost(
        IInternalMotelyJamlSearchBuilder builder,
        MotelySingleSearchContext singleSearchContext,
        ISearchEvents events)
    {
        _builder = builder;
        _singleSearchContext = singleSearchContext;
        _events = events;
    }

    private string StoreConfig(JamlConfig config)
    {
        var id = config.Id;
        if (string.IsNullOrEmpty(id)) id = Guid.NewGuid().ToString();
        lock (_configs) { _configs[id] = config; }
        return id;
    }

    private JamlConfig GetConfig(string id)
    {
        lock (_configs)
        {
            if (_configs.TryGetValue(id, out var config)) return config;
        }
        throw new InvalidOperationException($"Config '{id}' not found. Call LoadJaml first.");
    }

    public string GetVersion()
    {
        return _builder.GetVersion();
    }

    private static JamlConfig LoadJamlCore(string jaml)
    {
        if (!JamlConfigLoader.TryLoad(jaml, out var config, out var error))
            throw new InvalidOperationException(error ?? "Invalid JAML.");
        JamlSearchBuilder.EnsureRunnablePlan(config);
        return config;
    }

    public string LoadJaml(string jaml)
    {
        return StoreConfig(LoadJamlCore(jaml));
    }

    public string CompileJummy(string jummy)
    {
        if (!JummyCompiler.TryCompile(jummy, out var jamlYaml, out var compileErr))
            throw new InvalidOperationException(compileErr ?? "Jummy compile failed.");
        return StoreConfig(LoadJamlCore(jamlYaml));
    }

    public IMotelySingleSearchContext MotelySingleSearchContext(string seed, MotelyDeck deck, MotelyStake stake)
    {
        _singleSearchContext.OpenInternal(seed, deck, stake);
        return _singleSearchContext;
    }

    public MotelyBossBlind SingleGetBossForAnte(string seed, MotelyDeck deck, MotelyStake stake, int ante)
    {
        _singleSearchContext.OpenInternal(seed, deck, stake);
        return _singleSearchContext.GetBossForAnteInternal(ante);
    }

    public MotelyVoucher SingleGetAnteFirstVoucher(string seed, MotelyDeck deck, MotelyStake stake, int ante)
    {
        _singleSearchContext.OpenInternal(seed, deck, stake);
        return _singleSearchContext.GetAnteFirstVoucherInternal(ante);
    }

    public MotelyTag SingleGetNextTag(string seed, MotelyDeck deck, MotelyStake stake, int ante)
    {
        _singleSearchContext.OpenInternal(seed, deck, stake);
        return _singleSearchContext.GetNextTagInternal(ante);
    }

    public MotelyItem SingleGetNextShopItem(string seed, MotelyDeck deck, MotelyStake stake, int ante)
    {
        _singleSearchContext.OpenInternal(seed, deck, stake);
        return _singleSearchContext.GetNextShopItemInternal(ante);
    }

    public bool SingleGetNextLuckyMoney(string seed, MotelyDeck deck, MotelyStake stake, double baseLuck = 1)
    {
        _singleSearchContext.OpenInternal(seed, deck, stake);
        return _singleSearchContext.GetNextLuckyMoneyInternal(baseLuck);
    }

    public bool SingleGetNextLuckyMult(string seed, MotelyDeck deck, MotelyStake stake, double baseLuck = 1)
    {
        _singleSearchContext.OpenInternal(seed, deck, stake);
        return _singleSearchContext.GetNextLuckyMultInternal(baseLuck);
    }

    public int SingleGetNextMisprintMult(string seed, MotelyDeck deck, MotelyStake stake)
    {
        _singleSearchContext.OpenInternal(seed, deck, stake);
        return _singleSearchContext.GetNextMisprintMultInternal();
    }

    public void StartConfiguredSearch(string configId, int batchCharCount, long startBatch = 0, long endBatch = 0)
    {
        var config = GetConfig(configId);
        StartSearch(_builder.LoadConfig(config).Configured(batchCharCount, startBatch, endBatch).Run());
    }

    public void StartConfiguredSearchFromJaml(string jaml, int batchCharCount, long startBatch = 0, long endBatch = 0)
    {
        var config = LoadJamlCore(jaml);
        StartSearch(_builder.LoadConfig(config).Configured(batchCharCount, startBatch, endBatch).Run());
    }

    public void StartRandomSearch(string configId, int randomSeedCount)
    {
        var config = GetConfig(configId);
        StartSearch(_builder.LoadConfig(config).Random(randomSeedCount).Run());
    }

    public void StartRandomSearchFromJaml(string jaml, int randomSeedCount)
    {
        var config = LoadJamlCore(jaml);
        var built = JamlSearchBuilder.CreatePlan(config);
        var settings = built.Settings
            .WithDeck(config.Deck)
            .WithStake(config.Stake)
            .WithThreadCount(1)
            .WithRandomSearch(randomSeedCount);
        
        StartSearch(WireAndRunCore(settings));
    }

    private IMotelySearch WireAndRunCore(IMotelySearchSettings settings)
    {
        long lastSeedsSearched = 0;
        long lastMatchingSeeds = 0;

        settings = settings.WithProgressCallback(p =>
        {
            lastSeedsSearched = p.SeedsSearched;
            lastMatchingSeeds = p.MatchingSeeds;
            _events.NotifyProgress(p.SeedsSearched, p.MatchingSeeds);
        });

        settings = settings.WithScoredResultCallback(t =>
        {
            _events.NotifyResult(t.Seed, t.Score, t.TallyColumns.ToArray());
        });

        var search = settings.Start();
        _ = NotifyOnCompletionCoreAsync(search, () => lastSeedsSearched, () => lastMatchingSeeds);
        return search;
    }

    private async Task NotifyOnCompletionCoreAsync(IMotelySearch search, Func<long> getSeedsSearched, Func<long> getMatchingSeeds)
    {
        try
        {
            await search.WaitForCompletionAsync();
            _events.NotifyComplete("completed", getSeedsSearched(), getMatchingSeeds());
        }
        catch (OperationCanceledException)
        {
            _events.NotifyComplete("cancelled", getSeedsSearched(), getMatchingSeeds());
        }
        catch (Exception ex)
        {
            _events.NotifyComplete($"error: {ex.Message}", getSeedsSearched(), getMatchingSeeds());
        }
        finally
        {
            lock (_sync)
            {
                if (ReferenceEquals(_currentSearch, search))
                    _currentSearch = null;
            }
            search.Dispose();
        }
    }

    public void StartAestheticSearch(string configId, JamlAesthetic aesthetic)
    {
        var config = GetConfig(configId);
        StartSearch(_builder.LoadConfig(config).Aesthetic(aesthetic).Run());
    }

    public void StartKeywordSearch(string configId, string keywordsCsv, string paddingChars)
    {
        var config = GetConfig(configId);
        StartSearch(_builder.LoadConfig(config).Keywords(keywordsCsv, paddingChars).Run());
    }

    public void StartSeedListSearch(string configId, string[] seeds)
    {
        var config = GetConfig(configId);
        StartSearch(_builder.LoadConfig(config).SeedList(seeds).Run());
    }

    public void StartSeedListSearchFromJaml(string jaml, string[] seeds)
    {
        var config = LoadJamlCore(jaml);
        StartSearch(_builder.LoadConfig(config).SeedList(seeds).Run());
    }

    public void StopSearch()
    {
        IMotelySearch? search;
        lock (_sync)
        {
            search = _currentSearch;
        }

        search?.Cancel();
    }

    private void StartSearch(IMotelySearch search)
    {
        IMotelySearch? previous;
        lock (_sync)
        {
            previous = _currentSearch;
            _currentSearch = search;
        }

        previous?.Cancel();
        _ = ReleaseWhenCompleteAsync(search);
    }

    private async Task ReleaseWhenCompleteAsync(IMotelySearch search)
    {
        try
        {
            await search.WaitForCompletionAsync();
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            lock (_sync)
            {
                if (ReferenceEquals(_currentSearch, search))
                    _currentSearch = null;
            }

            search.Dispose();
        }
    }
}
