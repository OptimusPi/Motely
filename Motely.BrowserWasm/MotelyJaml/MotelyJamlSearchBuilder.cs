#nullable enable
using Motely;
using Motely.BrowserWasm;
using Motely.Filters;

namespace MotelyJaml;

public interface IMotelyJamlSearchBuilder
{
    string GetVersion();
    MotelyJamlSearchBuilder LoadConfig(JamlConfig config);
    MotelyJamlSearchBuilder LoadJaml(string jaml);
    MotelyJamlSearchBuilder CompileJummy(string jummy);
    MotelyJamlSearchBuilder Configured(int batchCharCount, long startBatch, long endBatch);
    MotelyJamlSearchBuilder ConfiguredBySearchIndex(
        int batchCharCount,
        long startSeedSearchIndex,
        long stopSeedSearchIndexInclusive);
    MotelyJamlSearchBuilder Sequential(int batchCharCount, long startBatch, long endBatch);
    MotelyJamlSearchBuilder SequentialBySearchIndex(
        int batchCharCount,
        long startSeedSearchIndex,
        long stopSeedSearchIndexInclusive);
    MotelyJamlSearchBuilder Random(int randomSeedCount);
    MotelyJamlSearchBuilder Aesthetic(JamlAesthetic aesthetic);
    MotelyJamlSearchBuilder Keywords(string keywordsCsv, string paddingChars);
    MotelyJamlSearchBuilder SeedList(string[] seeds);
    IMotelySearch Run();
}

public sealed class MotelyJamlSearchBuilder : IMotelyJamlSearchBuilder
{
    private readonly ISearchEvents _events;

    private JamlConfig? _config;
    private SearchMode _mode = SearchMode.None;

    private int _batchCharCount;
    private long _startBatch;
    private long _endBatch;
    private long _startSeedSearchIndex;
    private long _stopSeedSearchIndexInclusive;
    private int _randomSeedCount;
    private JamlAesthetic _aesthetic;
    private string _keywordsCsv = "";
    private string _paddingChars = "";
    private string[] _seeds = [];

    public MotelyJamlSearchBuilder(ISearchEvents events)
    {
        _events = events;
    }

    public string GetVersion()
    {
        return VersionInfo.Version;
    }

    public MotelyJamlSearchBuilder LoadConfig(JamlConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        ClearPlan();
        JamlSearchBuilder.EnsureRunnablePlan(config);
        _config = config;
        return this;
    }

    public MotelyJamlSearchBuilder LoadJaml(string jaml)
    {
        ClearPlan();
        if (!JamlConfigLoader.TryLoad(jaml, out var config, out var error))
            throw new InvalidOperationException(error ?? "Invalid JAML.");
        JamlSearchBuilder.EnsureRunnablePlan(config);
        _config = config;
        return this;
    }

    public MotelyJamlSearchBuilder CompileJummy(string jummy)
    {
        ClearPlan();
        if (!JummyCompiler.TryCompile(jummy, out var jamlYaml, out var compileErr))
            throw new InvalidOperationException(compileErr ?? "Jummy compile failed.");
        if (!JamlConfigLoader.TryLoad(jamlYaml, out var config, out var loadErr))
            throw new InvalidOperationException(loadErr ?? "Invalid JAML after Jummy compile.");
        JamlSearchBuilder.EnsureRunnablePlan(config);
        _config = config;
        return this;
    }

    public MotelyJamlSearchBuilder Configured(int batchCharCount, long startBatch, long endBatch)
    {
        RequireConfig();
        _mode = SearchMode.Configured;
        _batchCharCount = batchCharCount;
        _startBatch = startBatch;
        _endBatch = endBatch;
        return this;
    }

    public MotelyJamlSearchBuilder ConfiguredBySearchIndex(
        int batchCharCount,
        long startSeedSearchIndex,
        long stopSeedSearchIndexInclusive)
    {
        RequireConfig();
        _mode = SearchMode.ConfiguredByIndex;
        _batchCharCount = batchCharCount;
        _startSeedSearchIndex = startSeedSearchIndex;
        _stopSeedSearchIndexInclusive = stopSeedSearchIndexInclusive;
        return this;
    }

    public MotelyJamlSearchBuilder Sequential(int batchCharCount, long startBatch, long endBatch)
    {
        RequireConfig();
        _mode = SearchMode.Sequential;
        _batchCharCount = batchCharCount;
        _startBatch = startBatch;
        _endBatch = endBatch;
        return this;
    }

    public MotelyJamlSearchBuilder SequentialBySearchIndex(
        int batchCharCount,
        long startSeedSearchIndex,
        long stopSeedSearchIndexInclusive)
    {
        RequireConfig();
        _mode = SearchMode.SequentialByIndex;
        _batchCharCount = batchCharCount;
        _startSeedSearchIndex = startSeedSearchIndex;
        _stopSeedSearchIndexInclusive = stopSeedSearchIndexInclusive;
        return this;
    }

    public MotelyJamlSearchBuilder Random(int randomSeedCount)
    {
        RequireConfig();
        _mode = SearchMode.Random;
        _randomSeedCount = randomSeedCount;
        return this;
    }

    public MotelyJamlSearchBuilder Aesthetic(JamlAesthetic aesthetic)
    {
        RequireConfig();
        if (!Enum.IsDefined(aesthetic))
            throw new ArgumentOutOfRangeException(nameof(aesthetic));
        _mode = SearchMode.Aesthetic;
        _aesthetic = aesthetic;
        return this;
    }

    public MotelyJamlSearchBuilder Keywords(string keywordsCsv, string paddingChars)
    {
        RequireConfig();
        _mode = SearchMode.Keyword;
        _keywordsCsv = keywordsCsv;
        _paddingChars = paddingChars;
        return this;
    }

    public MotelyJamlSearchBuilder SeedList(string[] seeds)
    {
        RequireConfig();
        ArgumentNullException.ThrowIfNull(seeds);
        _mode = SearchMode.SeedList;
        _seeds = seeds;
        return this;
    }

    public IMotelySearch Run()
    {
        var jaml = _config ?? throw new InvalidOperationException("Call LoadJaml or CompileJummy first.");
        if (_mode == SearchMode.None)
            throw new InvalidOperationException("Choose a search mode (Configured, Sequential, Random, …) before Run.");

        IMotelySearchSettings settings;
        switch (_mode)
        {
            case SearchMode.Configured:
                settings = BuildConfigured(jaml, _batchCharCount, _startBatch, _endBatch);
                break;
            case SearchMode.ConfiguredByIndex:
                if (jaml.Aesthetics.Count > 0)
                    throw new InvalidOperationException(
                        "JAML declares aesthetics; seed-index ranges apply only to sequential search. Use Configured or Aesthetic.");
                {
                    var (sb, ebExclusive) = SeedMath.SearchIndexRangeToBatchRange(
                        _startSeedSearchIndex,
                        _stopSeedSearchIndexInclusive,
                        _batchCharCount);
                    settings = BuildConfigured(jaml, _batchCharCount, sb, ebExclusive);
                }
                break;
            case SearchMode.Sequential:
                settings = BuildSequential(jaml, _batchCharCount, _startBatch, _endBatch);
                break;
            case SearchMode.SequentialByIndex:
                if (jaml.Aesthetics.Count > 0)
                    throw new InvalidOperationException(
                        "This JAML declares aesthetics; use Configured or Aesthetic.");
                {
                    var (sb, ebExclusive) = SeedMath.SearchIndexRangeToBatchRange(
                        _startSeedSearchIndex,
                        _stopSeedSearchIndexInclusive,
                        _batchCharCount);
                    settings = BuildSequential(jaml, _batchCharCount, sb, ebExclusive);
                }
                break;
            case SearchMode.Random:
                settings = PlanProviderSearch(jaml).WithRandomSearch(Math.Max(1, _randomSeedCount));
                break;
            case SearchMode.Aesthetic:
                settings = PlanProviderSearch(jaml).WithAestheticSearch(_aesthetic);
                break;
            case SearchMode.Keyword:
                settings = BuildKeyword(jaml, _keywordsCsv, _paddingChars);
                break;
            case SearchMode.SeedList:
                settings = BuildSeedList(jaml, _seeds);
                break;
            default:
                throw new InvalidOperationException("Invalid search mode.");
        }

        ClearPlan();
        return WireAndRun(settings);
    }

    private void ClearPlan()
    {
        _config = null;
        _mode = SearchMode.None;
    }

    private void RequireConfig()
    {
        if (_config == null)
            throw new InvalidOperationException("Call LoadJaml or CompileJummy first.");
    }

    private IMotelySearchSettings BuildConfigured(JamlConfig jaml, int batchCharCount, long startBatch, long endBatch)
    {
        IMotelySearchSettings settings;
        if (jaml.Aesthetics.Count > 0)
        {
            settings = PlanProviderSearch(jaml).WithAestheticSearch(jaml.Aesthetics[0]);
        }
        else
        {
            settings = PlanSequentialSearch(jaml, batchCharCount).WithSequentialSearch();
            if (startBatch > 0) settings = settings.WithStartBatchIndex(startBatch);
            if (endBatch > 0) settings = settings.WithEndBatchIndex(endBatch);
        }

        return settings;
    }

    private static IMotelySearchSettings BuildSequential(
        JamlConfig jaml,
        int batchCharCount,
        long startBatch,
        long endBatch)
    {
        if (jaml.Aesthetics.Count > 0)
            throw new InvalidOperationException(
                "This JAML declares aesthetics; use Configured or Aesthetic.");

        var settings = PlanSequentialSearch(jaml, batchCharCount).WithSequentialSearch();
        if (startBatch > 0) settings = settings.WithStartBatchIndex(startBatch);
        if (endBatch > 0) settings = settings.WithEndBatchIndex(endBatch);
        return settings;
    }

    private static IMotelySearchSettings BuildKeyword(JamlConfig jaml, string keywordsCsv, string paddingChars)
    {
        var keywords = keywordsCsv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static k => k.Trim().ToUpperInvariant())
            .Where(static k => k.Length > 0)
            .ToArray();

        if (keywords.Length == 0)
            throw new ArgumentException("At least one keyword is required.", nameof(keywordsCsv));

        char[]? pad = string.IsNullOrEmpty(paddingChars) ? null
            : paddingChars.ToUpperInvariant()
                .Where(static c => MotelyGlobals.SeedDigits.Contains(c))
                .Distinct()
                .ToArray();

        var padded = MotelyGlobals.GeneratePaddedSeedsForKeywords(keywords, pad);
        long keywordSeedCount = MotelyGlobals.GetPaddedSeedCountForKeywordsLong(keywords, pad);
        return PlanProviderSearch(jaml)
            .WithProviderSearch(new MotelySeedListProvider(padded, keywordSeedCount));
    }

    private static IMotelySearchSettings BuildSeedList(JamlConfig jaml, string[] seeds)
    {
        var trimmed = seeds.Select(static s => s.Trim()).Where(static s => s.Length > 0).ToArray();
        if (trimmed.Length == 0)
            throw new ArgumentException("At least one non-empty seed is required.", nameof(seeds));
        return PlanProviderSearch(jaml).WithListSearch(trimmed, trimmed.Length);
    }

    private static IMotelySearchSettings PlanProviderSearch(JamlConfig jaml)
    {
        var built = JamlSearchBuilder.CreatePlan(jaml);
        return built.Settings
            .WithDeck(jaml.Deck)
            .WithStake(jaml.Stake)
            .WithThreadCount(1);
    }

    private static IMotelySearchSettings PlanSequentialSearch(JamlConfig jaml, int batchCharCount)
    {
        return PlanProviderSearch(jaml).WithBatchCharacterCount(batchCharCount);
    }

    private IMotelySearch WireAndRun(IMotelySearchSettings settings)
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
            _events.NotifyResult(t.Seed, t.Score, t.TallyColumns.ToArray()));

        IMotelySearch search;
        try
        {
            search = settings.Start();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"MotelyJamlSearchBuilder settings.Start() failed: {ex.Message}", ex);
        }
        _ = NotifyOnCompletionAsync(search, () => lastSeedsSearched, () => lastMatchingSeeds);
        return search;
    }

    private async Task NotifyOnCompletionAsync(IMotelySearch search, Func<long> getSeedsSearched, Func<long> getMatchingSeeds)
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
    }

    private enum SearchMode
    {
        None,
        Configured,
        ConfiguredByIndex,
        Sequential,
        SequentialByIndex,
        Random,
        Aesthetic,
        Keyword,
        SeedList,
    }
}
