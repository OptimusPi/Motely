#nullable enable
using Motely;
using Motely.Analysis;
using Motely.Filters;
using MotelyJaml;

namespace Motely.BrowserWasm;

public interface IMotelyWasmHost
{
    string GetVersion();
    JamlConfig LoadJaml(string jaml);
    JamlConfig CompileJummy(string jummy);
    IMotelySingleSearchContextImpl MotelySingleSearchContext(string seed, MotelyDeck deck, MotelyStake stake);
    MotelyBossBlind SingleGetBossForAnte(string seed, MotelyDeck deck, MotelyStake stake, int ante);
    MotelyVoucher SingleGetAnteFirstVoucher(string seed, MotelyDeck deck, MotelyStake stake, int ante);
    MotelyTag SingleGetNextTag(string seed, MotelyDeck deck, MotelyStake stake, int ante);
    MotelyItem SingleGetNextShopItem(string seed, MotelyDeck deck, MotelyStake stake, int ante);
    bool SingleGetNextLuckyMoney(string seed, MotelyDeck deck, MotelyStake stake, double baseLuck = 1);
    bool SingleGetNextLuckyMult(string seed, MotelyDeck deck, MotelyStake stake, double baseLuck = 1);
    int SingleGetNextMisprintMult(string seed, MotelyDeck deck, MotelyStake stake);
    IMotelySearch StartConfiguredSearch(JamlConfig config, int batchCharCount, long startBatch = 0, long endBatch = 0);
    IMotelySearch StartConfiguredSearchBySearchIndex(
        JamlConfig config,
        int batchCharCount,
        long startSeedSearchIndex,
        long stopSeedSearchIndexInclusive);
    IMotelySearch StartSequentialSearch(JamlConfig config, int batchCharCount, long startBatch = 0, long endBatch = 0);
    IMotelySearch StartSequentialSearchBySearchIndex(
        JamlConfig config,
        int batchCharCount,
        long startSeedSearchIndex,
        long stopSeedSearchIndexInclusive);
    IMotelySearch StartRandomSearch(JamlConfig config, int randomSeedCount);
    IMotelySearch StartRandomSearchFromJaml(string jaml, int randomSeedCount);
    IMotelySearch StartAestheticSearch(JamlConfig config, JamlAesthetic aesthetic);
    IMotelySearch StartKeywordSearch(JamlConfig config, string keywordsCsv, string paddingChars);
    IMotelySearch StartSeedListSearch(JamlConfig config, string[] seeds);
    void StopSearch();
}

public sealed class MotelyWasmHost : IMotelyWasmHost
{
    private readonly IMotelyJamlSearchBuilder _builder;
    private readonly IMotelySingleSearchContext _singleSearchContext;
    private readonly object _sync = new();
    private IMotelySearch? _currentSearch;

    public MotelyWasmHost(
        IMotelyJamlSearchBuilder builder,
        IMotelySingleSearchContext singleSearchContext)
    {
        _builder = builder;
        _singleSearchContext = singleSearchContext;
    }

    public string GetVersion()
    {
        return _builder.GetVersion();
    }

    public JamlConfig LoadJaml(string jaml)
    {
        if (!JamlConfigLoader.TryLoad(jaml, out var config, out var error))
            throw new InvalidOperationException(error ?? "Invalid JAML.");
        JamlSearchBuilder.EnsureRunnablePlan(config);
        return config;
    }

    public JamlConfig CompileJummy(string jummy)
    {
        if (!JummyCompiler.TryCompile(jummy, out var jamlYaml, out var compileErr))
            throw new InvalidOperationException(compileErr ?? "Jummy compile failed.");
        if (!JamlConfigLoader.TryLoad(jamlYaml, out var config, out var loadErr))
            throw new InvalidOperationException(loadErr ?? "Invalid JAML after Jummy compile.");
        JamlSearchBuilder.EnsureRunnablePlan(config);
        return config;
    }

    public IMotelySingleSearchContextImpl MotelySingleSearchContext(string seed, MotelyDeck deck, MotelyStake stake)
    {
        return _singleSearchContext.Open(seed, deck, stake);
    }

    public MotelyBossBlind SingleGetBossForAnte(string seed, MotelyDeck deck, MotelyStake stake, int ante)
    {
        return _singleSearchContext.Open(seed, deck, stake).GetBossForAnte(ante);
    }

    public MotelyVoucher SingleGetAnteFirstVoucher(string seed, MotelyDeck deck, MotelyStake stake, int ante)
    {
        return _singleSearchContext.Open(seed, deck, stake).GetAnteFirstVoucher(ante);
    }

    public MotelyTag SingleGetNextTag(string seed, MotelyDeck deck, MotelyStake stake, int ante)
    {
        return _singleSearchContext.Open(seed, deck, stake).GetNextTag(ante);
    }

    public MotelyItem SingleGetNextShopItem(string seed, MotelyDeck deck, MotelyStake stake, int ante)
    {
        return _singleSearchContext.Open(seed, deck, stake).GetNextShopItem(ante);
    }

    public bool SingleGetNextLuckyMoney(string seed, MotelyDeck deck, MotelyStake stake, double baseLuck = 1)
    {
        return _singleSearchContext.Open(seed, deck, stake).GetNextLuckyMoney(baseLuck);
    }

    public bool SingleGetNextLuckyMult(string seed, MotelyDeck deck, MotelyStake stake, double baseLuck = 1)
    {
        return _singleSearchContext.Open(seed, deck, stake).GetNextLuckyMult(baseLuck);
    }

    public int SingleGetNextMisprintMult(string seed, MotelyDeck deck, MotelyStake stake)
    {
        return _singleSearchContext.Open(seed, deck, stake).GetNextMisprintMult();
    }

    public IMotelySearch StartConfiguredSearch(JamlConfig config, int batchCharCount, long startBatch = 0, long endBatch = 0)
    {
        return StartSearch(_builder.LoadConfig(config).Configured(batchCharCount, startBatch, endBatch).Run());
    }

    public IMotelySearch StartConfiguredSearchBySearchIndex(
        JamlConfig config,
        int batchCharCount,
        long startSeedSearchIndex,
        long stopSeedSearchIndexInclusive)
    {
        return StartSearch(
            _builder.LoadConfig(config)
                .ConfiguredBySearchIndex(batchCharCount, startSeedSearchIndex, stopSeedSearchIndexInclusive)
                .Run());
    }

    public IMotelySearch StartSequentialSearch(JamlConfig config, int batchCharCount, long startBatch = 0, long endBatch = 0)
    {
        return StartSearch(_builder.LoadConfig(config).Sequential(batchCharCount, startBatch, endBatch).Run());
    }

    public IMotelySearch StartSequentialSearchBySearchIndex(
        JamlConfig config,
        int batchCharCount,
        long startSeedSearchIndex,
        long stopSeedSearchIndexInclusive)
    {
        return StartSearch(
            _builder.LoadConfig(config)
                .SequentialBySearchIndex(batchCharCount, startSeedSearchIndex, stopSeedSearchIndexInclusive)
                .Run());
    }

    public IMotelySearch StartRandomSearch(JamlConfig config, int randomSeedCount)
    {
        return StartSearch(_builder.LoadConfig(config).Random(randomSeedCount).Run());
    }

    public IMotelySearch StartAestheticSearch(JamlConfig config, JamlAesthetic aesthetic)
    {
        return StartSearch(_builder.LoadConfig(config).Aesthetic(aesthetic).Run());
    }

    public IMotelySearch StartKeywordSearch(JamlConfig config, string keywordsCsv, string paddingChars)
    {
        return StartSearch(_builder.LoadConfig(config).Keywords(keywordsCsv, paddingChars).Run());
    }

    public IMotelySearch StartSeedListSearch(JamlConfig config, string[] seeds)
    {
        return StartSearch(_builder.LoadConfig(config).SeedList(seeds).Run());
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

    private IMotelySearch StartSearch(IMotelySearch search)
    {
        IMotelySearch? previous;
        lock (_sync)
        {
            previous = _currentSearch;
            _currentSearch = search;
        }

        previous?.Cancel();
        _ = ReleaseWhenCompleteAsync(search);
        return search;
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
