#nullable enable
using Motely;
using Motely.Filters;
using MotelyJaml;

namespace Motely.BrowserWasm;

public interface IMotelyWasmHost
{
    string GetVersion();
    string LoadJaml(string jaml);
    string ValidateJaml(string jaml);
    MotelyDeck GetConfigDeck(string configId);
    MotelyStake GetConfigStake(string configId);
    void StartConfiguredSearch(string configId, int batchCharCount, long startBatch = 0, long endBatch = 0);
    void StartRandomSearch(string configId, int randomSeedCount);
    void StartAestheticSearch(string configId, JamlAesthetic aesthetic);
    void StartKeywordSearch(string configId, string keywordsCsv, string paddingChars);
    void StartSeedListSearch(string configId, string[] seeds);
    void StopSearch();
}

public sealed class MotelyWasmHost : IMotelyWasmHost
{
    private readonly ISearchEvents _events;
    private readonly Dictionary<string, JamlConfig> _configs = new();

    public MotelyWasmHost(ISearchEvents events)
    {
        _events = events;
    }

    public string GetVersion() => VersionInfo.Version;

    public string LoadJaml(string jaml)
    {
        var config = LoadJamlCore(jaml);
        var id = Guid.NewGuid().ToString();
        _configs[id] = config;
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

    public void StartConfiguredSearch(string configId, int batchCharCount, long startBatch = 0, long endBatch = 0)
    {
        RunSearch(MotelyJamlSearchHelper.BuildConfigured(GetConfigInternal(configId), batchCharCount, startBatch, endBatch));
    }

    public void StartRandomSearch(string configId, int randomSeedCount)
    {
        RunSearch(MotelyJamlSearchHelper.BuildRandom(GetConfigInternal(configId), randomSeedCount));
    }

    public void StartAestheticSearch(string configId, JamlAesthetic aesthetic)
    {
        RunSearch(MotelyJamlSearchHelper.BuildAesthetic(GetConfigInternal(configId), aesthetic));
    }

    public void StartKeywordSearch(string configId, string keywordsCsv, string paddingChars)
    {
        RunSearch(MotelyJamlSearchHelper.BuildKeyword(GetConfigInternal(configId), keywordsCsv, paddingChars));
    }

    public void StartSeedListSearch(string configId, string[] seeds)
    {
        RunSearch(MotelyJamlSearchHelper.BuildSeedList(GetConfigInternal(configId), seeds));
    }

    public void StopSearch() { }

    private void RunSearch(IMotelySearchSettings settings)
    {
        settings = settings
            .WithProgressCallback(p =>
                _events.NotifyProgress(p.SeedsSearched, p.MatchingSeeds))
            .WithScoredResultCallback(t =>
                _events.NotifyResult(t.Seed, t.Score, t.TallyColumns.ToArray()));
        try
        {
            using var search = settings.Start();
            _events.NotifyComplete("completed", search.TotalSeedsSearched, search.MatchingSeeds);
        }
        catch (Exception ex)
        {
            _events.NotifyComplete($"error: {ex.Message}", 0, 0);
        }
    }

    private JamlConfig LoadJamlCore(string jaml)
    {
        if (!JamlConfigLoader.TryLoad(jaml, out var config, out var error))
            throw new InvalidOperationException(error ?? "Invalid JAML.");
        JamlSearchBuilder.EnsureRunnablePlan(config);
        return config;
    }

    private JamlConfig GetConfigInternal(string id)
    {
        if (_configs.TryGetValue(id, out var config))
            return config;

        throw new InvalidOperationException($"Config '{id}' not found.");
    }
}
