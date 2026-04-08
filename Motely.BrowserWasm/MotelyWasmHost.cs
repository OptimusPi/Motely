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
    MotelySingleSearchContextImpl MotelySingleSearchContext(string seed, MotelyDeck deck, MotelyStake stake);
    IMotelySearchSession StartConfiguredSearch(JamlConfig config, int batchCharCount, long startBatch = 0, long endBatch = 0);
    IMotelySearchSession StartConfiguredSearchBySearchIndex(
        JamlConfig config,
        int batchCharCount,
        long startSeedSearchIndex,
        long stopSeedSearchIndexInclusive);
    IMotelySearchSession StartSequentialSearch(JamlConfig config, int batchCharCount, long startBatch = 0, long endBatch = 0);
    IMotelySearchSession StartSequentialSearchBySearchIndex(
        JamlConfig config,
        int batchCharCount,
        long startSeedSearchIndex,
        long stopSeedSearchIndexInclusive);
    IMotelySearchSession StartRandomSearch(JamlConfig config, int randomSeedCount);
    IMotelySearchSession StartAestheticSearch(JamlConfig config, JamlAesthetic aesthetic);
    IMotelySearchSession StartKeywordSearch(JamlConfig config, string keywordsCsv, string paddingChars);
    IMotelySearchSession StartSeedListSearch(JamlConfig config, string[] seeds);
    void StopSearch();
}

public sealed class MotelyWasmHost : IMotelyWasmHost
{
    private readonly IMotelyJamlSearchBuilder _builder;
    private readonly IMotelySingleSearchContext _singleSearchContext;
    private readonly object _sync = new();
    private IMotelySearchSession? _currentSearch;

    public MotelyWasmHost(
        IMotelyJamlSearchBuilder builder,
        IMotelySingleSearchContext singleSearchContext)
    {
        _builder = builder;
        _singleSearchContext = singleSearchContext;
    }

    public string GetVersion() => _builder.GetVersion();

    public JamlConfig LoadJaml(string jaml)
    {
        if (!JamlConfigLoader.TryLoad(jaml, out var config, out var error))
            throw new InvalidOperationException(error ?? "Invalid JAML.");
        return config;
    }

    public JamlConfig CompileJummy(string jummy)
    {
        if (!JummyCompiler.TryCompile(jummy, out var jamlYaml, out var compileErr))
            throw new InvalidOperationException(compileErr ?? "Jummy compile failed.");
        if (!JamlConfigLoader.TryLoad(jamlYaml, out var config, out var loadErr))
            throw new InvalidOperationException(loadErr ?? "Invalid JAML after Jummy compile.");
        return config;
    }

    public MotelySingleSearchContextImpl MotelySingleSearchContext(string seed, MotelyDeck deck, MotelyStake stake) =>
        _singleSearchContext.Open(seed, deck, stake);

    public IMotelySearchSession StartConfiguredSearch(JamlConfig config, int batchCharCount, long startBatch = 0, long endBatch = 0) =>
        StartSearch(_builder.LoadConfig(config).Configured(batchCharCount, startBatch, endBatch).Run());

    public IMotelySearchSession StartConfiguredSearchBySearchIndex(
        JamlConfig config,
        int batchCharCount,
        long startSeedSearchIndex,
        long stopSeedSearchIndexInclusive) =>
        StartSearch(
            _builder.LoadConfig(config)
                .ConfiguredBySearchIndex(batchCharCount, startSeedSearchIndex, stopSeedSearchIndexInclusive)
                .Run());

    public IMotelySearchSession StartSequentialSearch(JamlConfig config, int batchCharCount, long startBatch = 0, long endBatch = 0) =>
        StartSearch(_builder.LoadConfig(config).Sequential(batchCharCount, startBatch, endBatch).Run());

    public IMotelySearchSession StartSequentialSearchBySearchIndex(
        JamlConfig config,
        int batchCharCount,
        long startSeedSearchIndex,
        long stopSeedSearchIndexInclusive) =>
        StartSearch(
            _builder.LoadConfig(config)
                .SequentialBySearchIndex(batchCharCount, startSeedSearchIndex, stopSeedSearchIndexInclusive)
                .Run());

    public IMotelySearchSession StartRandomSearch(JamlConfig config, int randomSeedCount) =>
        StartSearch(_builder.LoadConfig(config).Random(randomSeedCount).Run());

    public IMotelySearchSession StartAestheticSearch(JamlConfig config, JamlAesthetic aesthetic) =>
        StartSearch(_builder.LoadConfig(config).Aesthetic(aesthetic).Run());

    public IMotelySearchSession StartKeywordSearch(JamlConfig config, string keywordsCsv, string paddingChars) =>
        StartSearch(_builder.LoadConfig(config).Keywords(keywordsCsv, paddingChars).Run());

    public IMotelySearchSession StartSeedListSearch(JamlConfig config, string[] seeds) =>
        StartSearch(_builder.LoadConfig(config).SeedList(seeds).Run());

    public void StopSearch()
    {
        IMotelySearchSession? search;
        lock (_sync)
        {
            search = _currentSearch;
        }

        search?.Cancel();
    }

    private IMotelySearchSession StartSearch(IMotelySearchSession search)
    {
        IMotelySearchSession? previous;
        lock (_sync)
        {
            previous = _currentSearch;
            _currentSearch = search;
        }

        previous?.Cancel();
        _ = ReleaseWhenCompleteAsync(search);
        return search;
    }

    private async Task ReleaseWhenCompleteAsync(IMotelySearchSession search)
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
