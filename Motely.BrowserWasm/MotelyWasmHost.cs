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
    IMotelySingleSearchContext MotelySingleSearchContext(string seed, MotelyDeck deck, MotelyStake stake);
    MotelyBossBlind SingleGetBossForAnte(string seed, MotelyDeck deck, MotelyStake stake, int ante);
    MotelyVoucher SingleGetAnteFirstVoucher(string seed, MotelyDeck deck, MotelyStake stake, int ante);
    MotelyTag SingleGetNextTag(string seed, MotelyDeck deck, MotelyStake stake, int ante);
    MotelyItem SingleGetNextShopItem(string seed, MotelyDeck deck, MotelyStake stake, int ante);
    bool SingleGetNextLuckyMoney(string seed, MotelyDeck deck, MotelyStake stake, double baseLuck = 1);
    bool SingleGetNextLuckyMult(string seed, MotelyDeck deck, MotelyStake stake, double baseLuck = 1);
    int SingleGetNextMisprintMult(string seed, MotelyDeck deck, MotelyStake stake);
    void StartConfiguredSearch(JamlConfig config, int batchCharCount, long startBatch = 0, long endBatch = 0);
    void StartConfiguredSearchFromJaml(string jaml, int batchCharCount, long startBatch = 0, long endBatch = 0);
    void StartConfiguredSearchBySearchIndex(
        JamlConfig config,
        int batchCharCount,
        long startSeedSearchIndex,
        long stopSeedSearchIndexInclusive);
    void StartSequentialSearch(JamlConfig config, int batchCharCount, long startBatch = 0, long endBatch = 0);
    void StartSequentialSearchBySearchIndex(
        JamlConfig config,
        int batchCharCount,
        long startSeedSearchIndex,
        long stopSeedSearchIndexInclusive);
    void StartRandomSearch(JamlConfig config, int randomSeedCount);
    void StartRandomSearchFromJaml(string jaml, int randomSeedCount);
    void StartAestheticSearch(JamlConfig config, JamlAesthetic aesthetic);
    void StartKeywordSearch(JamlConfig config, string keywordsCsv, string paddingChars);
    void StartSeedListSearch(JamlConfig config, string[] seeds);
    void StartSeedListSearchFromJaml(string jaml, string[] seeds);
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

    // [JSExport] methods must never call other [JSExport] methods on `this` —
    // Bootsharp surfaces them as UnmanagedCallersOnly thunks and managed dispatch crashes.
    private static JamlConfig LoadJamlCore(string jaml)
    {
        if (!JamlConfigLoader.TryLoad(jaml, out var config, out var error))
            throw new InvalidOperationException(error ?? "Invalid JAML.");
        JamlSearchBuilder.EnsureRunnablePlan(config);
        return config;
    }

    public JamlConfig LoadJaml(string jaml)
    {
        return LoadJamlCore(jaml);
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

    public IMotelySingleSearchContext MotelySingleSearchContext(string seed, MotelyDeck deck, MotelyStake stake)
    {
        _singleSearchContext.Open(seed, deck, stake);
        return _singleSearchContext;
    }

    public MotelyBossBlind SingleGetBossForAnte(string seed, MotelyDeck deck, MotelyStake stake, int ante)
    {
        _singleSearchContext.Open(seed, deck, stake);
        return System.Text.Json.JsonSerializer.Deserialize<MotelyBossBlind>(_singleSearchContext.GetBossForAnte(ante));
    }

    public MotelyVoucher SingleGetAnteFirstVoucher(string seed, MotelyDeck deck, MotelyStake stake, int ante)
    {
        _singleSearchContext.Open(seed, deck, stake);
        return System.Text.Json.JsonSerializer.Deserialize<MotelyVoucher>(_singleSearchContext.GetAnteFirstVoucher(ante));
    }

    public MotelyTag SingleGetNextTag(string seed, MotelyDeck deck, MotelyStake stake, int ante)
    {
        _singleSearchContext.Open(seed, deck, stake);
        return System.Text.Json.JsonSerializer.Deserialize<MotelyTag>(_singleSearchContext.GetNextTag(ante));
    }

    public MotelyItem SingleGetNextShopItem(string seed, MotelyDeck deck, MotelyStake stake, int ante)
    {
        _singleSearchContext.Open(seed, deck, stake);
        return System.Text.Json.JsonSerializer.Deserialize<MotelyItem>(_singleSearchContext.GetNextShopItem(ante));
    }

    public bool SingleGetNextLuckyMoney(string seed, MotelyDeck deck, MotelyStake stake, double baseLuck = 1)
    {
        _singleSearchContext.Open(seed, deck, stake);
        return _singleSearchContext.PseudoHash("lucky_money", false) > 0.5; // Simplified for now
    }

    public bool SingleGetNextLuckyMult(string seed, MotelyDeck deck, MotelyStake stake, double baseLuck = 1)
    {
        _singleSearchContext.Open(seed, deck, stake);
        return _singleSearchContext.PseudoHash("lucky_mult", false) > 0.5; // Simplified for now
    }

    public int SingleGetNextMisprintMult(string seed, MotelyDeck deck, MotelyStake stake)
    {
        _singleSearchContext.Open(seed, deck, stake);
        return (int)(_singleSearchContext.PseudoHash("misprint", false) * 100); // Simplified for now
    }

    public void StartConfiguredSearch(JamlConfig config, int batchCharCount, long startBatch = 0, long endBatch = 0)
    {
        StartSearch(_builder.LoadConfig(config).Configured(batchCharCount, startBatch, endBatch).Run());
    }

    public void StartConfiguredSearchFromJaml(string jaml, int batchCharCount, long startBatch = 0, long endBatch = 0)
    {
        var config = LoadJamlCore(jaml);
        StartSearch(_builder.LoadConfig(config).Configured(batchCharCount, startBatch, endBatch).Run());
    }

    public void StartConfiguredSearchBySearchIndex(
        JamlConfig config,
        int batchCharCount,
        long startSeedSearchIndex,
        long stopSeedSearchIndexInclusive)
    {
        StartSearch(
            _builder.LoadConfig(config)
                .ConfiguredBySearchIndex(batchCharCount, startSeedSearchIndex, stopSeedSearchIndexInclusive)
                .Run());
    }

    public void StartSequentialSearch(JamlConfig config, int batchCharCount, long startBatch = 0, long endBatch = 0)
    {
        StartSearch(_builder.LoadConfig(config).Sequential(batchCharCount, startBatch, endBatch).Run());
    }

    public void StartSequentialSearchBySearchIndex(
        JamlConfig config,
        int batchCharCount,
        long startSeedSearchIndex,
        long stopSeedSearchIndexInclusive)
    {
        StartSearch(
            _builder.LoadConfig(config)
                .SequentialBySearchIndex(batchCharCount, startSeedSearchIndex, stopSeedSearchIndexInclusive)
                .Run());
    }

    public void StartRandomSearch(JamlConfig config, int randomSeedCount)
    {
        StartSearch(_builder.LoadConfig(config).Random(randomSeedCount).Run());
    }

    public void StartRandomSearchFromJaml(string jaml, int randomSeedCount)
    {
        var config = LoadJamlCore(jaml);
        StartSearch(_builder.LoadConfig(config).Random(randomSeedCount).Run());
    }

    public void StartAestheticSearch(JamlConfig config, JamlAesthetic aesthetic)
    {
        StartSearch(_builder.LoadConfig(config).Aesthetic(aesthetic).Run());
    }

    public void StartKeywordSearch(JamlConfig config, string keywordsCsv, string paddingChars)
    {
        StartSearch(_builder.LoadConfig(config).Keywords(keywordsCsv, paddingChars).Run());
    }

    public void StartSeedListSearch(JamlConfig config, string[] seeds)
    {
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
