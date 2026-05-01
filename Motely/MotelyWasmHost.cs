using System.Text;
using Bootsharp.FileSystem;
using Motely.Analysis;
using Motely.Filters;
using YamlDotNet.Core;

namespace Motely;

/// <summary>
/// Single <c>[JSExport]</c> facade for the browser. All search modes pin
/// <c>WithThreadCount(1)</c> — browser WASM is single-threaded. Scored results stream out
/// through <see cref="IMotelyWasmEvents"/> (Bootsharp <c>[JSImport]</c>).
/// </summary>
public sealed class MotelyWasmHost : IMotelyWasm
{
    private readonly IMotelyWasmEvents _events;
    private readonly IFileMounter _fileMounter;
    private readonly Dictionary<string, (IFileSystem Fs, JamlFileWatcher Watcher)> _libraries = new();

    public MotelyWasmHost(IMotelyWasmEvents events, IFileMounter fileMounter)
    {
        _events = events;
        _fileMounter = fileMounter;
    }

    public string GetVersion() => VersionInfo.Version;

    public MotelyItemLayout GetItemLayout() =>
        new(
            MotelyGlobals.ItemTypeMask,
            MotelyGlobals.StandardcardRankMask,
            MotelyGlobals.StandardcardSuitOffset,
            MotelyGlobals.StandardcardSuitMask,
            MotelyGlobals.ItemTypeCategoryOffset,
            MotelyGlobals.ItemTypeCategoryMask,
            MotelyGlobals.JokerRarityOffset,
            MotelyGlobals.JokerRarityMask,
            MotelyGlobals.ItemSealOffset,
            MotelyGlobals.ItemSealMask,
            MotelyGlobals.ItemEnhancementOffset,
            MotelyGlobals.ItemEnhancementMask,
            MotelyGlobals.ItemEditionOffset,
            MotelyGlobals.ItemEditionMask,
            MotelyGlobals.PerishableStickerOffset,
            MotelyGlobals.EternalStickerOffset,
            MotelyGlobals.RentalStickerOffset
        );

    private static readonly Lazy<string> JamlSchemaJson = new(LoadJamlSchema);

    public string GetJamlSchema() =>
        JamlSchemaJson.Value;

    private static string LoadJamlSchema()
    {
        var assembly = typeof(MotelyWasmHost).Assembly;
        using var stream = assembly.GetManifestResourceStream("jaml.schema.json")
            ?? throw new InvalidOperationException("Embedded JAML schema resource was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public string ValidateJaml(string jaml)
    {
        if (JamlConfigLoader.TryLoad(jaml, out var config, out var error))
        {
            try { JamlSearchBuilder.EnsureRunnablePlan(config); }
            catch (Exception ex) { return ex.Message; }
            return "valid";
        }
        return error ?? "Invalid JAML.";
    }

    public JamlValidationResult ValidateJamlStructured(string jaml)
    {
        if (JamlConfigLoader.TryLoadWithException(jaml, out var config, out var error, out var exception))
        {
            try { JamlSearchBuilder.EnsureRunnablePlan(config); }
            catch (Exception ex)
            {
                return new JamlValidationResult(false, ex.Message, null, 0, 0);
            }
            return new JamlValidationResult(true, null, null, 0, 0);
        }

        int line = 0, col = 0;
        string? path = null;
        if (exception is YamlException yamlEx)
        {
            line = (int)yamlEx.Start.Line;
            col = (int)yamlEx.Start.Column;
        }
        return new JamlValidationResult(false, error ?? "Invalid JAML.", path, line, col);
    }

    public JamlMetaResult GetJamlMeta(string jaml)
    {
        if (!JamlConfigLoader.TryLoad(jaml, out var config, out _))
            return new JamlMetaResult([], [], 0, 0, 0, "Red", "White");

        var antes = new SortedSet<int>();
        var itemTypes = new SortedSet<string>();

        void Walk(System.Collections.Generic.IEnumerable<IJamlClause> clauses)
        {
            foreach (var c in clauses)
            {
                switch (c)
                {
                    case JokerClause jc:
                        itemTypes.Add("Joker");
                        foreach (var a in jc.Antes) antes.Add(a);
                        break;
                    case CommonJokerClause cc:
                        itemTypes.Add("CommonJoker");
                        foreach (var a in cc.Antes) antes.Add(a);
                        break;
                    case UncommonJokerClause uc:
                        itemTypes.Add("UncommonJoker");
                        foreach (var a in uc.Antes) antes.Add(a);
                        break;
                    case RareJokerClause rc:
                        itemTypes.Add("RareJoker");
                        foreach (var a in rc.Antes) antes.Add(a);
                        break;
                    case LegendaryJokerClause lc:
                        itemTypes.Add("LegendaryJoker");
                        foreach (var a in lc.Antes) antes.Add(a);
                        break;
                    case VoucherClause vc:
                        itemTypes.Add("Voucher");
                        foreach (var a in vc.Antes) antes.Add(a);
                        break;
                    case BossClause bc:
                        itemTypes.Add("Boss");
                        foreach (var a in bc.Antes) antes.Add(a);
                        break;
                    case TagClause tc:
                        itemTypes.Add("Tag");
                        foreach (var a in tc.Antes) antes.Add(a);
                        break;
                    case TarotCardClause tarot:
                        itemTypes.Add("Tarot");
                        foreach (var a in tarot.Antes) antes.Add(a);
                        break;
                    case SpectralCardClause spec:
                        itemTypes.Add("Spectral");
                        foreach (var a in spec.Antes) antes.Add(a);
                        break;
                    case PlanetCardClause planet:
                        itemTypes.Add("Planet");
                        foreach (var a in planet.Antes) antes.Add(a);
                        break;
                    case ErraticRankClause erk:
                        itemTypes.Add("ErraticRank");
                        foreach (var a in erk.Antes) antes.Add(a);
                        break;
                    case ErraticSuitClause esc:
                        itemTypes.Add("ErraticSuit");
                        foreach (var a in esc.Antes) antes.Add(a);
                        break;
                    case AndClause and:
                        Walk(and.Clauses);
                        break;
                    case OrClause or:
                        Walk(or.Clauses);
                        break;
                }
            }
        }

        Walk(config.Must);
        Walk(config.Should);
        Walk(config.MustNot);

        return new JamlMetaResult(
            [.. antes],
            [.. itemTypes],
            config.Must.Count,
            config.Should.Count,
            config.MustNot.Count,
            config.Deck.ToString(),
            config.Stake.ToString()
        );
    }

    public string CompileJummy(string jummy)
    {
        if (!JummyCompiler.TryCompile(jummy, out var jaml, out var error))
            throw new InvalidOperationException(error ?? "Invalid Jummy.");
        return jaml;
    }

    public IMotelyWasmSearchContext CreateSearchContext(string seed, MotelyDeck deck, MotelyStake stake)
    {
        return new MotelyWasmSearchContext(seed, deck, stake);
    }

    public IMotelyWasmSearch StartRandomSearch(string jaml, int randomSeedCount)
    {
        return StartSearch(jaml, settings => settings.WithRandomSearch(Math.Max(1, randomSeedCount)));
    }

    public IMotelyWasmSearch StartAestheticSearch(string jaml, JamlAesthetic aesthetic)
    {
        return StartSearch(jaml, settings => settings.WithAestheticSearch(aesthetic));
    }

    public IMotelyWasmSearch StartSequentialSearch(string jaml, int batchCharCount, long startBatch, long endBatch)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(batchCharCount, 1, nameof(batchCharCount));
        return StartSearch(jaml, settings =>
        {
            var s = settings
                .WithBatchCharacterCount(batchCharCount)
                .WithSequentialSearch();
            if (startBatch > 0) s = s.WithStartBatchIndex(startBatch);
            if (endBatch > 0) s = s.WithEndBatchIndex(endBatch);
            return s;
        });
    }

    public Task<MotelyWasmSearchBatchResult> RunSequentialSearchBatch(
        string jaml,
        int batchCharCount,
        long startBatch,
        long endBatch,
        int maxResults
    )
    {
        var results = new List<MotelyWasmSearchResult>();
        using var search = StartSequentialSearch(
            jaml,
            batchCharCount,
            startBatch,
            endBatch,
            result =>
            {
                if (results.Count < Math.Max(0, maxResults))
                    results.Add(result);
            }
        );
        // NativeAOT fires all events synchronously — search is complete by here.
        var snap = search.GetSnapshot();
        var completion = new MotelyWasmSearchCompletion(
            MotelyWasmSearchState.Completed,
            snap.TotalSeedsSearched,
            snap.MatchingSeeds,
            null
        );
        return Task.FromResult(new MotelyWasmSearchBatchResult(completion, [.. results]));
    }

    public IMotelyWasmSearch StartSeedListSearch(string jaml, string[] seeds)
    {
        var trimmed = (seeds ?? Array.Empty<string>())
            .Select(static s => s.Trim())
            .Where(static s => s.Length > 0)
            .ToArray();
        if (trimmed.Length == 0)
            throw new ArgumentException("StartSeedListSearch requires at least one non-empty seed.", nameof(seeds));
        return StartSearch(jaml, settings => settings.WithListSearch(trimmed, trimmed.Length));
    }

    public IMotelyWasmSearch StartKeywordSearch(string jaml, string keywordsCsv, string paddingChars)
    {
        var normalized = (keywordsCsv ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static k => k.ToUpperInvariant())
            .Where(static k => k.Length > 0)
            .ToArray();
        if (normalized.Length == 0)
            throw new ArgumentException("StartKeywordSearch requires at least one keyword.", nameof(keywordsCsv));
        var padding = string.IsNullOrEmpty(paddingChars)
            ? null
            : paddingChars.ToUpperInvariant().Distinct().ToArray();
        var provider = new MotelyKeywordSeedProvider(normalized, padding);
        return StartSearch(jaml, settings => settings.WithProviderSearch(provider));
    }

    private IMotelyWasmSearch StartSequentialSearch(
        string jaml,
        int batchCharCount,
        long startBatch,
        long endBatch,
        Action<MotelyWasmSearchResult>? onResult
    )
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(batchCharCount, 1, nameof(batchCharCount));
        return StartSearch(jaml, settings =>
        {
            var s = settings
                .WithBatchCharacterCount(batchCharCount)
                .WithSequentialSearch();
            if (startBatch > 0) s = s.WithStartBatchIndex(startBatch);
            if (endBatch > 0) s = s.WithEndBatchIndex(endBatch);
            return s;
        }, onResult);
    }

    private IMotelyWasmSearch StartSearch(
        string jaml,
        Func<IMotelySearchSettings, IMotelySearchSettings> configureMode,
        Action<MotelyWasmSearchResult>? onResult = null
    )
    {
        var config = ParseJaml(jaml);
        var plan = JamlSearchBuilder.CreatePlan(config);
        var settings = plan.Settings
            .WithDeck(config.Deck)
            .WithStake(config.Stake)
            .WithThreadCount(1);

        settings = configureMode(settings);

        var events = _events;
        // Auto-cutoff: only fire NotifyResult when score >= running max ("DING (new hi score)").
        // Browser interop crossing per match is the bottleneck — without this, a wide-net JAML
        // pierces JS↔WASM for every passing seed and tanks throughput by orders of magnitude.
        // Mirrors Motely.CLI's `--cutoff auto` behavior, but defaulted on (browser is single-threaded
        // and has no opt-in CLI flag). Caller's onResult arg still gets every match for sinks that need it.
        int currentHigh = int.MinValue;
        settings = settings
            .WithProgressCallback(progress =>
                events.NotifyProgress(progress.SeedsSearched, progress.MatchingSeeds))
            .WithScoredResultCallback(tally =>
            {
                var tallyColumns = tally.TallyColumns.ToArray();
                onResult?.Invoke(new(tally.Seed, tally.Score, tallyColumns));
                if (tally.Score < currentHigh) return;
                currentHigh = tally.Score;
                events.NotifyResult(tally.Seed, tally.Score, tallyColumns);
            });

        var search = settings.Start();
        return new MotelyWasmSearch(search);
    }

    public string[] GetTallyLabels(string jaml) =>
        JamlSearchBuilder.CreatePlan(ParseJaml(jaml)).TallyLabels;

    public MotelyJamlyzerResult AnalyzeJamlSeeds(string jaml, string[] seeds) =>
        MotelyJamlyzer.AnalyzeSeeds(new(jaml, seeds));

    private static JamlConfig ParseJaml(string jaml)
    {
        if (!JamlConfigLoader.TryLoad(jaml, out var config, out var error))
            throw new InvalidOperationException(error ?? "Invalid JAML.");
        JamlSearchBuilder.EnsureRunnablePlan(config);
        return config;
    }

    // --- JAML Library ---

    public async Task<string?> MountJamlLibrary()
    {
        var rootId = await _fileMounter.PickRoot(new PickOptions
        {
            Mode = PermissionMode.ReadWrite
        });
        if (rootId is null) return null;

        var watcher = new JamlFileWatcher(_events, rootId);
        var fs = await _fileMounter.Mount(rootId, watcher, new MountOptions
        {
            Mode = PermissionMode.ReadWrite
        });
        _libraries[rootId] = (fs, watcher);
        return rootId;
    }

    public async Task UnmountJamlLibrary(string rootId)
    {
        if (_libraries.Remove(rootId))
            await _fileMounter.Unmount(rootId);
    }

    public string[] GetJamlLibraryFiles(string rootId)
    {
        return _libraries.TryGetValue(rootId, out var lib) ? lib.Watcher.FileUris : [];
    }

    public async Task<string> LoadJamlFile(string rootId, string uri)
    {
        if (!_libraries.TryGetValue(rootId, out var lib))
            throw new InvalidOperationException($"No mounted library with root '{rootId}'.");
        var bytes = await lib.Fs.ReadFile(uri);
        return Encoding.UTF8.GetString(bytes);
    }

    public async Task SaveJamlFile(string rootId, string uri, string content)
    {
        if (!_libraries.TryGetValue(rootId, out var lib))
            throw new InvalidOperationException($"No mounted library with root '{rootId}'.");
        await lib.Fs.WriteFile(uri, Encoding.UTF8.GetBytes(content));
    }

    private sealed class JamlFileWatcher(IMotelyWasmEvents events, string rootId) : IFileWatcher
    {
        private readonly Dictionary<string, bool> _files = new();

        public string[] FileUris => [.. _files.Keys.Order()];

        public Task HandleFileChanges(IReadOnlyList<Change> changes)
        {
            foreach (var c in changes)
            {
                if (!c.File || !IsJamlFile(c.Entry.Uri)) continue;

                if (c.Added || c.Modified)
                    _files[c.Entry.Uri] = true;
                else if (c.Removed)
                    _files.Remove(c.Entry.Uri);
                else if (c.Moved)
                {
                    if (c.FromUri is not null) _files.Remove(c.FromUri);
                    if (IsJamlFile(c.Entry.Uri)) _files[c.Entry.Uri] = true;
                }
            }
            events.NotifyJamlLibraryChanged(rootId, FileUris);
            return Task.CompletedTask;
        }

        private static bool IsJamlFile(string uri) =>
            uri.EndsWith(".jaml", StringComparison.OrdinalIgnoreCase) ||
            uri.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) ||
            uri.EndsWith(".yml", StringComparison.OrdinalIgnoreCase);
    }
}
