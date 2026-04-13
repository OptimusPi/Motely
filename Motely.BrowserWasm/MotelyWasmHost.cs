#nullable enable
using System.Text.Json;
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
    string ValidateJaml(string jaml);
    MotelyDeck GetConfigDeck(string configId);
    MotelyStake GetConfigStake(string configId);
    string AnalyzeSeed(string seed, MotelyDeck deck, MotelyStake stake);

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
    void StartConfiguredSearchBySearchIndex(string configId, int batchCharCount, long startSeedSearchIndex, long stopSeedSearchIndexInclusive);
    void StartSequentialSearch(string configId, int batchCharCount, long startBatch = 0, long endBatch = 0);
    void StartSequentialSearchBySearchIndex(string configId, int batchCharCount, long startSeedSearchIndex, long stopSeedSearchIndexInclusive);
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
    private readonly MotelyJamlSearchBuilder _builder;
    private readonly ISearchEvents _events;
    private readonly Dictionary<string, JamlConfig> _configs = new();
    private readonly Dictionary<string, IMotelySingleSearchContextImpl> _contexts = new();
    private readonly object _sync = new();
    private IMotelySearch? _currentSearch;

    public MotelyWasmHost(MotelyJamlSearchBuilder builder, ISearchEvents events)
    {
        _builder = builder;
        _events = events;
    }

    public string GetVersion()
    {
        return _builder.GetVersion();
    }

    public string LoadJaml(string jaml)
    {
        var config = LoadJamlCore(jaml);
        var id = Guid.NewGuid().ToString();
        lock (_configs) _configs[id] = config;
        return id;
    }

    public string CompileJummy(string jummy)
    {
        var config = CompileJummyCore(jummy);
        var id = Guid.NewGuid().ToString();
        lock (_configs) _configs[id] = config;
        return id;
    }

    public string ValidateJaml(string jaml)
    {
        return JamlConfigLoader.TryLoad(jaml, out _, out var error) ? "valid" : error ?? "Invalid JAML.";
    }

    public MotelyDeck GetConfigDeck(string configId)
    {
        return GetConfigInternal(configId).Deck;
    }

    public MotelyStake GetConfigStake(string configId)
    {
        return GetConfigInternal(configId).Stake;
    }

    public string AnalyzeSeed(string seed, MotelyDeck deck, MotelyStake stake)
    {
        var analysis = MotelySeedAnalyzer.Analyze(new(seed, deck, stake));
        return SerializeAnalysis(seed, analysis);
    }

    public string OpenSingleSearchContext(string seed, MotelyDeck deck, MotelyStake stake)
    {
        var id = Guid.NewGuid().ToString();
        var router = new MotelySeedRouterDesc(seed, deck, stake);
        var impl = new MotelySingleSearchContextImpl(router);
        lock (_contexts) _contexts[id] = impl;
        return id;
    }

    public string ContextGetSeed(string ctxId)
    {
        return GetContextInternal(ctxId).GetSeed();
    }

    public MotelyBossBlind ContextGetBossForAnte(string ctxId, int ante)
    {
        return GetContextInternal(ctxId).GetBossForAnte(ante);
    }

    public MotelyVoucher ContextGetAnteFirstVoucher(string ctxId, int ante)
    {
        return GetContextInternal(ctxId).GetAnteFirstVoucher(ante);
    }

    public MotelyTag ContextGetNextTag(string ctxId, int ante)
    {
        return GetContextInternal(ctxId).GetNextTag(ante);
    }

    public MotelyItem ContextGetNextShopItem(string ctxId, int ante)
    {
        return GetContextInternal(ctxId).GetNextShopItem(ante);
    }

    public bool ContextGetNextLuckyMoney(string ctxId, double baseLuck = 1)
    {
        return GetContextInternal(ctxId).GetNextLuckyMoney(baseLuck);
    }

    public bool ContextGetNextLuckyMult(string ctxId, double baseLuck = 1)
    {
        return GetContextInternal(ctxId).GetNextLuckyMult(baseLuck);
    }

    public int ContextGetNextMisprintMult(string ctxId)
    {
        return GetContextInternal(ctxId).GetNextMisprintMult();
    }

    public void ContextClose(string ctxId)
    {
        lock (_contexts) _contexts.Remove(ctxId);
    }

    public void StartConfiguredSearch(string configId, int batchCharCount, long startBatch = 0, long endBatch = 0)
    {
        QueueSearch(CreatePlanBuilder().LoadConfig(GetConfigInternal(configId)).Configured(batchCharCount, startBatch, endBatch));
    }

    public void StartConfiguredSearchFromJaml(string jaml, int batchCharCount, long startBatch = 0, long endBatch = 0)
    {
        QueueSearch(CreatePlanBuilder().LoadConfig(LoadJamlCore(jaml)).Configured(batchCharCount, startBatch, endBatch));
    }

    public void StartConfiguredSearchBySearchIndex(string configId, int batchCharCount, long startSeedSearchIndex, long stopSeedSearchIndexInclusive)
    {
        QueueSearch(CreatePlanBuilder().LoadConfig(GetConfigInternal(configId)).ConfiguredBySearchIndex(batchCharCount, startSeedSearchIndex, stopSeedSearchIndexInclusive));
    }

    public void StartSequentialSearch(string configId, int batchCharCount, long startBatch = 0, long endBatch = 0)
    {
        QueueSearch(CreatePlanBuilder().LoadConfig(GetConfigInternal(configId)).Sequential(batchCharCount, startBatch, endBatch));
    }

    public void StartSequentialSearchBySearchIndex(string configId, int batchCharCount, long startSeedSearchIndex, long stopSeedSearchIndexInclusive)
    {
        QueueSearch(CreatePlanBuilder().LoadConfig(GetConfigInternal(configId)).SequentialBySearchIndex(batchCharCount, startSeedSearchIndex, stopSeedSearchIndexInclusive));
    }

    public void StartRandomSearch(string configId, int randomSeedCount)
    {
        QueueSearch(CreatePlanBuilder().LoadConfig(GetConfigInternal(configId)).Random(randomSeedCount));
    }

    public void StartRandomSearchFromJaml(string jaml, int randomSeedCount)
    {
        QueueSearch(CreatePlanBuilder().LoadConfig(LoadJamlCore(jaml)).Random(randomSeedCount));
    }

    public void StartAestheticSearch(string configId, JamlAesthetic aesthetic)
    {
        QueueSearch(CreatePlanBuilder().LoadConfig(GetConfigInternal(configId)).Aesthetic(aesthetic));
    }

    public void StartKeywordSearch(string configId, string keywordsCsv, string paddingChars)
    {
        QueueSearch(CreatePlanBuilder().LoadConfig(GetConfigInternal(configId)).Keywords(keywordsCsv, paddingChars));
    }

    public void StartSeedListSearch(string configId, string[] seeds)
    {
        QueueSearch(CreatePlanBuilder().LoadConfig(GetConfigInternal(configId)).SeedList(seeds));
    }

    public void StartSeedListSearchFromJaml(string jaml, string[] seeds)
    {
        QueueSearch(CreatePlanBuilder().LoadConfig(LoadJamlCore(jaml)).SeedList(seeds));
    }

    public void StopSearch()
    {
        lock (_sync) _currentSearch?.Cancel();
    }

    private MotelyJamlSearchBuilder CreatePlanBuilder() => new(_events);

    private void QueueSearch(IMotelyJamlSearchBuilder plan)
    {
        _ = RunAfterYieldAsync(plan);
    }

    private async Task RunAfterYieldAsync(IMotelyJamlSearchBuilder plan)
    {
        try
        {
            await Task.Yield();
            StartSearch(plan.Run());
        }
        catch (Exception ex)
        {
            _events.NotifyComplete($"error: {ex.Message}", 0, 0);
        }
    }

    private void StartSearch(IMotelySearch search)
    {
        lock (_sync)
        {
            _currentSearch?.Cancel();
            _currentSearch = search;
        }

        _ = ReleaseWhenCompleteAsync(search);
    }

    private async Task ReleaseWhenCompleteAsync(IMotelySearch search)
    {
        try
        {
            await search.WaitForCompletionAsync();
        }
        catch
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

    private JamlConfig LoadJamlCore(string jaml)
    {
        if (!JamlConfigLoader.TryLoad(jaml, out var config, out var error))
            throw new InvalidOperationException(error ?? "Invalid JAML.");
        JamlSearchBuilder.EnsureRunnablePlan(config);
        return config;
    }

    private JamlConfig CompileJummyCore(string jummy)
    {
        if (!JummyCompiler.TryCompile(jummy, out var jamlYaml, out var compileErr))
            throw new InvalidOperationException(compileErr ?? "Jummy compile failed.");
        return LoadJamlCore(jamlYaml);
    }

    private JamlConfig GetConfigInternal(string id)
    {
        lock (_configs)
        {
            if (_configs.TryGetValue(id, out var config))
                return config;
        }

        throw new InvalidOperationException($"Config '{id}' not found.");
    }

    private IMotelySingleSearchContextImpl GetContextInternal(string id)
    {
        lock (_contexts)
        {
            if (_contexts.TryGetValue(id, out var context))
                return context;
        }

        throw new InvalidOperationException($"Context '{id}' not found.");
    }

    private static string SerializeAnalysis(string seed, MotelyLegacyTextAnalyzer analysis)
    {
        if (!string.IsNullOrEmpty(analysis.Error))
            return JsonSerializer.Serialize(
                new BrowserSeedAnalysisErrorDto { Error = analysis.Error },
                BrowserAnalysisJsonContext.Default.BrowserSeedAnalysisErrorDto);

        var dto = new BrowserSeedAnalysisDto
        {
            Seed = seed,
            Deck = analysis.Deck?.ToString(),
            ErraticDeckComposition = analysis.ErraticDeckComposition,
            Antes = analysis.Antes.Select(static ante => new BrowserAnteAnalysisDto
            {
                Ante = ante.Ante,
                Boss = ante.Boss.ToString(),
                Voucher = ante.Voucher.ToString(),
                SmallBlindTag = ante.SmallBlindTag.ToString(),
                BigBlindTag = ante.BigBlindTag.ToString(),
                DrawOrder = ante.DrawOrder,
                ShopQueue = ante.ShopQueue.Select(FormatUtils.FormatItem).ToArray(),
                Packs = ante.Packs.Select(static pack => new BrowserPackAnalysisDto
                {
                    Type = FormatUtils.FormatPackName(pack.Type),
                    Items = pack.Items.Select(FormatUtils.FormatItem).ToArray()
                }).ToArray()
            }).ToArray()
        };

        return JsonSerializer.Serialize(dto, BrowserAnalysisJsonContext.Default.BrowserSeedAnalysisDto);
    }
}
