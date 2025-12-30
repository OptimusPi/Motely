using System.Collections.Generic;
using System.IO;
using Motely.Filters;
using Motely.Utils;

namespace Motely.Executors
{
    /// <summary>
    /// Executes JSON-based filter searches with specialized vectorized filters
    /// </summary>
    public sealed class JsonSearchExecutor
    {
        private readonly string? _configPath;
        private readonly MotelyJsonConfig? _config;
        private readonly JsonSearchParams _params;
        private readonly string _format;
        private readonly Action<MotelySeedScoreTally>? _customCallback;
        private bool _cancelled = false;
        private IMotelySearch? _runningSearch;

        public JsonSearchExecutor(
            string configPath,
            JsonSearchParams parameters,
            string format = "json",
            Action<MotelySeedScoreTally>? customCallback = null
        )
        {
            _configPath = configPath;
            _config = null;
            _params = parameters;
            _format = format;
            _customCallback = customCallback;
        }

        public JsonSearchExecutor(
            MotelyJsonConfig config,
            JsonSearchParams parameters,
            Action<MotelySeedScoreTally>? customCallback = null
        )
        {
            _configPath = null;
            _config = config;
            _params = parameters;
            _format = "json";
            _customCallback = customCallback;
        }

        /// <summary>
        /// Cancel the currently running search
        /// </summary>
        public void Cancel()
        {
            _cancelled = true;
            _runningSearch?.Pause();
        }

        /// <summary>
        /// Initialize parsed enums for a clause with helpful error messages
        /// </summary>
        private static void InitializeClauseWithContext(
            MotelyJsonConfig.MotleyJsonFilterClause clause,
            string sectionName,  // "MUST", "MUSTNOT", "SHOULD"
            int index)
        {
            try
            {
                clause.InitializeParsedEnums();
            }
            catch (Exception ex)
            {
                var typeText = string.IsNullOrEmpty(clause.Type) ? "<missing>" : clause.Type;
                var valueText = !string.IsNullOrEmpty(clause.Value)
                    ? clause.Value
                    : (clause.Values != null && clause.Values.Length > 0
                        ? string.Join(", ", clause.Values)
                        : "<none>");
                throw new ArgumentException(
                    $"Config error in {sectionName}[{index}] — type: '{typeText}', value(s): '{valueText}'. {ex.Message}\nHint: Each clause needs a non-empty 'type' (e.g., 'Joker', 'TarotCard', 'PlayingCard'). If using multiple values, use 'values': [ ... ] not 'value'."
                );
            }
        }

        public int Execute(bool awaitCompletion = true)
        {
            DebugLogger.IsEnabled = _params.EnableDebug;
            FancyConsole.IsEnabled = !_params.NoFancy;
            // Gate colored output based on --nofancy
            TallyColorizer.ColorEnabled = !_params.NoFancy;

            var (seeds, preSorted) = LoadSeeds();

            // Suppress startup messages in quiet mode
            if (!_params.Quiet)
            {
                Console.WriteLine($"🔍 MotelyJAML Search Starting");
                Console.WriteLine($"   Config: {_configPath}");
                Console.WriteLine($"   Threads: {_params.Threads}");

                if (_params.RandomSeeds.HasValue)
                {
                    Console.WriteLine($"   Mode: Random ({_params.RandomSeeds} seeds)");
                }
                else
                {
                    Console.WriteLine($"   Batch Size: {_params.BatchSize} chars");
                    string endDisplay = _params.EndBatch == 0 ? "∞" : _params.EndBatch.ToString();
                    Console.WriteLine($"   Range: {_params.StartBatch} to {endDisplay}");
                }
                if (_params.EnableDebug)
                {
                    Console.WriteLine($"   Debug: Enabled");
                }

                Console.WriteLine();
            }

            try
            {
                MotelyJsonConfig config = LoadConfig();
                IMotelySearch search = CreateSearch(config, seeds, preSorted);
                if (search == null)
                {
                    return 1;
                }

                // Print CSV header (even for filters with no SHOULD clauses, output seed with score 0)
                PrintResultsHeader(config);

                // Setup cancellation handler ONLY when NOT in TUI mode
                // In TUI mode, the UI handles Ctrl+C via KeyDown event and calls Cancel() directly
                ConsoleCancelEventHandler? cancelHandler = null;
                if (_customCallback == null)
                {
                    cancelHandler = (sender, e) =>
                    {
                        e.Cancel = true;
                        _cancelled = true;
                        if (!_params.Quiet)
                        {
                            Console.WriteLine("\n🛑 Stopping search...");
                        }
                    };
                    Console.CancelKeyPress += cancelHandler;
                }

                search.Start();

                if (awaitCompletion)
                {
                    // Wait for completion - progress shown by MotelySearch.PrintReport() in FancyConsole bottom line
                    while (search.Status != MotelySearchStatus.Completed && !_cancelled)
                    {
                        Thread.Sleep(100);
                    }

                    // Stop the search gracefully (if cancelled)
                    if (_cancelled)
                    {
                        search.Dispose();

                        // Wait for final batch to flush before showing stats
                        // The search may have queued results that need to be written
                        Console.Out.Flush();
                        Thread.Sleep(500); // Give time for final batch flush
                        Console.Out.Flush();
                    }

                    Console.Out.Flush();
                    Thread.Sleep(100);
                    Console.Out.Flush();

                    // Suppress summary in quiet mode
                    if (!_params.Quiet)
                    {
                        PrintResultsSummary(search, _cancelled);
                    }
                }
                else
                {
                    // Store the search for later access/cancellation
                    _runningSearch = search;
                }

                // Cleanup cancel handler if registered
                if (cancelHandler != null)
                {
                    Console.CancelKeyPress -= cancelHandler;
                }

                Console.Out.Flush();
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
                if (_params.EnableDebug)
                {
                    Console.WriteLine($"[DEBUG] {ex}");
                }
                return 1;
            }
        }

        /// <summary>
        /// Load seeds from the configured source.
        /// Returns (seeds, preSorted) where preSorted=true means seeds are already sorted by length.
        /// For DbList, returns streaming IEnumerable that doesn't load everything into RAM.
        /// </summary>
        private (IEnumerable<string>? seeds, bool preSorted) LoadSeeds()
        {
            if (!string.IsNullOrEmpty(_params.SpecificSeed))
            {
                if (!_params.Quiet)
                {
                    Console.WriteLine($"🔍 Searching for specific seed: {_params.SpecificSeed}");
                }
                // Return just the specific seed
                return (new[] { _params.SpecificSeed }, false);
            }

            // Direct seed list takes priority over wordlist file
            if (_params.SeedList != null && _params.SeedList.Count > 0)
            {
                if (!_params.Quiet)
                {
                    Console.WriteLine($"🔍 Searching {_params.SeedList.Count} seeds from provided list");
                }
                return (_params.SeedList, false);
            }

            if (!string.IsNullOrEmpty(_params.Wordlist))
            {
                string wordlistPath = ResolveWordlistPath(_params.Wordlist);

                List<string> seeds =
                [
                    .. File.ReadAllLines(wordlistPath)
                        .Where(static s => !string.IsNullOrWhiteSpace(s)),
                ];
                if (!_params.Quiet)
                {
                    Console.WriteLine(
                        $"✅ Loaded {seeds.Count} seeds from wordlist: {wordlistPath}"
                    );
                }
                return (seeds, false);
            }

            // DuckDB seed list (e.g., fertilizer.db) - ZERO memory allocation!
            // DuckDBSeeds.Stream() returns IEnumerable that streams from DuckDB, already sorted by LENGTH
            if (!string.IsNullOrEmpty(_params.DbList))
            {
                string dbPath = ResolveDbPath(_params.DbList);
                
                if (!File.Exists(dbPath))
                {
                    throw new FileNotFoundException($"DuckDB file not found: {dbPath}");
                }

                if (!_params.Quiet)
                {
                    Console.WriteLine($"✅ Streaming seeds from DuckDB: {dbPath}");
                }
                // Return streaming IEnumerable - NO list materialization, already sorted by LENGTH!
                return (DuckDBSeeds.Stream(dbPath), true);
            }

            return (null, false); // Sequential search
        }

        private static string ResolveWordlistPath(string wordlistInput)
        {
            string pathWithExtension = Path.HasExtension(wordlistInput)
                ? wordlistInput
                : wordlistInput + ".txt";

            if (Path.IsPathRooted(pathWithExtension))
            {
                if (File.Exists(pathWithExtension))
                {
                    return pathWithExtension;
                }
                throw new FileNotFoundException($"Wordlist not found: {pathWithExtension}");
            }

            foreach (var directory in EnumerateDirectoriesUpwards(Directory.GetCurrentDirectory()))
            {
                foreach (var folder in new[] { "WordLists", "wordlists" })
                {
                    var candidate = Path.Combine(directory, folder, pathWithExtension);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }

            var relativeCandidate = Path.Combine(Directory.GetCurrentDirectory(), pathWithExtension);
            if (File.Exists(relativeCandidate))
            {
                return relativeCandidate;
            }

            throw new FileNotFoundException($"Wordlist not found: {pathWithExtension}");
        }

        private static string ResolveDbPath(string dbInput)
        {
            string pathWithExtension = Path.HasExtension(dbInput)
                ? dbInput
                : dbInput + ".db";

            if (Path.IsPathRooted(pathWithExtension))
            {
                if (File.Exists(pathWithExtension))
                {
                    return pathWithExtension;
                }
                throw new FileNotFoundException($"Database file not found: {pathWithExtension}");
            }

            foreach (var directory in EnumerateDirectoriesUpwards(Directory.GetCurrentDirectory()))
            {
                foreach (var folder in new[] { "WordLists", "wordlists" })
                {
                    var candidate = Path.Combine(directory, folder, pathWithExtension);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }

            var relativeCandidate = Path.Combine(Directory.GetCurrentDirectory(), pathWithExtension);
            if (File.Exists(relativeCandidate))
            {
                return relativeCandidate;
            }

            throw new FileNotFoundException($"Database file not found: {pathWithExtension}");
        }

        private static string ResolveConfigPath(string configName, string filterDir, string extension)
        {
            string pathWithExtension = Path.HasExtension(configName)
                ? configName
                : configName + extension;

            if (Path.IsPathRooted(pathWithExtension))
            {
                if (File.Exists(pathWithExtension))
                {
                    return pathWithExtension;
                }
                throw new FileNotFoundException($"Config file not found: {pathWithExtension}");
            }

            foreach (var directory in EnumerateDirectoriesUpwards(Directory.GetCurrentDirectory()))
            {
                var candidate = Path.Combine(directory, filterDir, pathWithExtension);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            var relativeCandidate = Path.Combine(Directory.GetCurrentDirectory(), filterDir, pathWithExtension);
            if (File.Exists(relativeCandidate))
            {
                return relativeCandidate;
            }

            throw new FileNotFoundException($"Config file not found: {pathWithExtension}");
        }

        private static IEnumerable<string> EnumerateDirectoriesUpwards(string startDirectory)
        {
            var current = startDirectory;
            while (!string.IsNullOrEmpty(current))
            {
                yield return current;

                var parent = Directory.GetParent(current);
                if (parent == null)
                {
                    break;
                }
                current = parent.FullName;
            }
        }

        private MotelyJsonConfig LoadConfig()
        {
            if (_config != null)
                return _config;

            if (string.IsNullOrEmpty(_configPath))
                throw new InvalidOperationException("No config path or config object provided");

            string configPath;
            bool isJamlFormat = _format == "jaml";
            string extension = isJamlFormat ? ".jaml" : ".json";
            string filterDir = isJamlFormat ? "JamlFilters" : "JsonFilters";

            bool hasDirectory = !string.IsNullOrEmpty(Path.GetDirectoryName(_configPath));
            if (Path.IsPathRooted(_configPath) || hasDirectory)
            {
                configPath = _configPath;
                if (string.IsNullOrEmpty(Path.GetExtension(configPath)))
                    configPath = configPath + extension;
            }
            else
            {
                configPath = ResolveConfigPath(_configPath, filterDir, extension);
            }

            if (!File.Exists(configPath))
                throw new FileNotFoundException(
                    $"Could not find {_format.ToUpper()} config file: {configPath}"
                );

            MotelyJsonConfig? config;
            string? error;
            bool success;

            if (isJamlFormat)
            {
                success = JamlConfigLoader.TryLoadFromJaml(configPath, out config, out error);
            }
            else
            {
                success = MotelyJsonConfig.TryLoadFromJsonFile(configPath, out config, out error);
            }

            if (!success || config == null)
                throw new Exception($"Failed to load config from {configPath}: {error}");

            return config;
        }

        private IMotelySearch CreateSearch(MotelyJsonConfig config, IEnumerable<string>? seeds, bool preSorted = false)
        {
            if (!_params.Quiet)
            {
                Console.WriteLine("CreateSearch...");
            }

            // Scoring: ALL Must clauses (verify strictly) + Should clauses (score)
            MotelyJsonConfig scoringConfig = new()
            {
                Name = config.Name,
                Must = config.Must, // ALL Must clauses - verify without vector approximation
                Should = config.Should, // Should clauses for scoring
                MustNot = [], // Empty - filters handle this
            };

            // PostProcess to calculate MaxVoucherAnte and other metrics
            scoringConfig.PostProcess();

            // Create callback for CSV output - use custom callback if provided, otherwise console output
            Action<MotelySeedScoreTally> scoreCallback = _customCallback ?? ((MotelySeedScoreTally result) =>
            {
                // Just print the seed - it will naturally push the progress line down
                FancyConsole.WriteLine(
                    TallyColorizer.FormatResultLine(result.Seed, result.Score, result.TallyColumns)
                );
            });

            MotelyJsonSeedScoreDesc scoreDesc = new(
                scoringConfig,
                _params.Cutoff,
                _params.AutoCutoff ? ScoreCutoffMode.AutoSmart : ScoreCutoffMode.Manual,
                scoreCallback
            );

            if (!_params.Quiet)
            {
                if (_params.AutoCutoff)
                {
                    Console.WriteLine(
                        $"✅ Loaded config with auto-cutoff (starting at {_params.Cutoff})"
                    );
                }
                else
                {
                    Console.WriteLine($"✅ Loaded config with cutoff: {_params.Cutoff}");
                }
            }

            // Use specialized filter system - DON'T GROUP! Chain each clause separately!
            // config.PostProcess() already initialized all clauses!
            List<MotelyJsonConfig.MotleyJsonFilterClause> mustClauses = config.Must?.ToList() ?? [];

            // If no MUST clauses, check if we have mustNot clauses to use as a composite filter
            if (mustClauses.Count == 0)
            {
                if (config.MustNot != null && config.MustNot.Count > 0)
                {
                    // We have ONLY mustNot clauses - create a composite filter with inverted clauses
                    if (!_params.Quiet)
                    {
                        Console.WriteLine(
                            $"[COMPOSITE] Creating composite filter with {config.MustNot.Count} inverted mustNot clauses"
                        );
                    }

                    // Initialize parsed enums for MustNot clauses
                    for (int i = 0; i < config.MustNot.Count; i++)
                    {
                        InitializeClauseWithContext(config.MustNot[i], "MUSTNOT", i);
                    }

                    // Mark all mustNot clauses as inverted
                    var allRequiredClauses = new List<MotelyJsonConfig.MotleyJsonFilterClause>();
                    foreach (var clause in config.MustNot)
                    {
                        clause.IsInverted = true;
                        allRequiredClauses.Add(clause);
                    }

                    // Create composite filter with inverted clauses
                    var compositeFilter = new MotelyCompositeFilterDesc(allRequiredClauses);
                    var compositeSettings =
                        new MotelySearchSettings<MotelyCompositeFilterDesc.MotelyCompositeFilter>(
                            compositeFilter
                        );

                    if (
                        !string.IsNullOrEmpty(config.Deck)
                        && Enum.TryParse(config.Deck, true, out MotelyDeck compositeDeck)
                    )
                        compositeSettings = compositeSettings.WithDeck(compositeDeck);
                    if (
                        !string.IsNullOrEmpty(config.Stake)
                        && Enum.TryParse(config.Stake, true, out MotelyStake compositeStake)
                    )
                        compositeSettings = compositeSettings.WithStake(compositeStake);

                    compositeSettings = compositeSettings.WithThreadCount(_params.Threads);
                    compositeSettings = compositeSettings.WithBatchCharacterCount(
                        _params.BatchSize
                    );
                    compositeSettings = compositeSettings.WithStartBatchIndex(
                        (long)_params.StartBatch
                    );
                    if (_params.EndBatch > 0)
                        compositeSettings = compositeSettings.WithEndBatchIndex(
                            (long)_params.EndBatch + 1
                        );

                    compositeSettings = compositeSettings.WithSeedScoreProvider(scoreDesc);
                    compositeSettings = compositeSettings.WithCsvOutput(true);

                    if (_params.Quiet)
                        compositeSettings = compositeSettings.WithQuietMode(true);
                    if (_params.ProgressCallback != null)
                        compositeSettings = compositeSettings.WithProgressCallback(_params.ProgressCallback);

                    // Configure search mode
                    if (_params.RandomSeeds.HasValue)
                        compositeSettings = compositeSettings.WithRandomSearch(_params.RandomSeeds.Value);
                    else if (seeds != null)
                        compositeSettings = compositeSettings.WithListSearch(seeds, preSorted);
                    else
                        compositeSettings = compositeSettings.WithSequentialSearch();

                    // Start search
                    return (IMotelySearch)compositeSettings.Start();
                }

                // No MUST or MUSTNOT clauses - use passthrough filter (accept all seeds, score via SHOULD)
                if (!_params.Quiet)
                {
                    Console.WriteLine(
                        $"[PASSTHROUGH] No MUST/MUSTNOT clauses - accepting all seeds for scoring"
                    );
                }
                var passthroughFilter = new PassthroughFilterDesc();
                var passthroughSettings =
                    new MotelySearchSettings<PassthroughFilterDesc.PassthroughFilter>(
                        passthroughFilter
                    );

                if (
                    !string.IsNullOrEmpty(config.Deck)
                    && Enum.TryParse(config.Deck, true, out MotelyDeck passthroughDeck)
                )
                    passthroughSettings = passthroughSettings.WithDeck(passthroughDeck);
                if (
                    !string.IsNullOrEmpty(config.Stake)
                    && Enum.TryParse(config.Stake, true, out MotelyStake passthroughStake)
                )
                    passthroughSettings = passthroughSettings.WithStake(passthroughStake);

                passthroughSettings = passthroughSettings.WithThreadCount(_params.Threads);
                passthroughSettings = passthroughSettings.WithBatchCharacterCount(
                    _params.BatchSize
                );
                passthroughSettings = passthroughSettings.WithStartBatchIndex(
                    (long)_params.StartBatch
                );
                if (_params.EndBatch > 0)
                    passthroughSettings = passthroughSettings.WithEndBatchIndex(
                        (long)_params.EndBatch + 1
                    );

                passthroughSettings = passthroughSettings.WithSeedScoreProvider(scoreDesc);
                passthroughSettings = passthroughSettings.WithCsvOutput(true);

                if (_params.Quiet)
                    passthroughSettings = passthroughSettings.WithQuietMode(true);
                if (_params.ProgressCallback != null)
                    passthroughSettings = passthroughSettings.WithProgressCallback(_params.ProgressCallback);

                if (seeds != null)
                    return passthroughSettings.WithListSearch(seeds, preSorted).Start();
                else
                    return passthroughSettings.WithSequentialSearch().Start();
            }

            // Chain each Must clause as a separate filter (vector -> vector -> vector -> ...)
            // First clause becomes primary filter
            var firstClause = mustClauses[0];

            if (!_params.Quiet)
            {
                Console.WriteLine($"[CHAINING] Primary filter: {firstClause.ItemTypeEnum}");
            }

            // Create primary filter from SINGLE clause
            var primaryFilter = CreateSingleClauseFilterDesc(firstClause);
            dynamic searchSettings = CreateSearchSettings(primaryFilter, firstClause.ItemTypeEnum);

            // Chain remaining clauses with WithAdditionalFilter
            for (int i = 1; i < mustClauses.Count; i++)
            {
                var clause = mustClauses[i];
                var additionalFilter = CreateSingleClauseFilterDesc(clause);
                searchSettings = searchSettings.WithAdditionalFilter(additionalFilter);

                if (!_params.Quiet)
                {
                    Console.WriteLine($"   + Chained filter {i}: {clause.ItemTypeEnum}");
                }
            }

            // Chain mustNot clauses as inverted filters
            if (config.MustNot != null && config.MustNot.Count > 0)
            {
                foreach (var clause in config.MustNot)
                {
                    clause.IsInverted = true;
                    var invertedFilter = CreateSingleClauseFilterDesc(clause);
                    searchSettings = searchSettings.WithAdditionalFilter(invertedFilter);
                }
            }

            // Apply all settings
            if (!string.IsNullOrEmpty(config.Deck) && Enum.TryParse(config.Deck, true, out MotelyDeck deck))
                searchSettings = searchSettings.WithDeck(deck);
            if (!string.IsNullOrEmpty(config.Stake) && Enum.TryParse(config.Stake, true, out MotelyStake stake))
                searchSettings = searchSettings.WithStake(stake);

            searchSettings = searchSettings.WithThreadCount(_params.Threads);
            searchSettings = searchSettings.WithBatchCharacterCount(_params.BatchSize);
            searchSettings = searchSettings.WithStartBatchIndex((long)_params.StartBatch);
            if (_params.EndBatch > 0)
                searchSettings = searchSettings.WithEndBatchIndex((long)_params.EndBatch);

            searchSettings = searchSettings.WithSeedScoreProvider(scoreDesc);
            searchSettings = searchSettings.WithCsvOutput(true);

            if (_params.Quiet)
                searchSettings = searchSettings.WithQuietMode(true);
            if (_params.ProgressCallback != null)
                searchSettings = searchSettings.WithProgressCallback(_params.ProgressCallback);

            // Configure search mode
            if (_params.RandomSeeds.HasValue)
                searchSettings = searchSettings.WithRandomSearch(_params.RandomSeeds.Value);
            else if (seeds != null)
                searchSettings = searchSettings.WithListSearch(seeds, preSorted);
            else
                searchSettings = searchSettings.WithSequentialSearch();

            // Start search
            return (IMotelySearch)searchSettings.Start();
        }

        // Helper: Create filter descriptor for a SINGLE clause
        private static IMotelySeedFilterDesc CreateSingleClauseFilterDesc(MotelyJsonConfig.MotleyJsonFilterClause clause)
        {
            var singleClauseList = new List<MotelyJsonConfig.MotleyJsonFilterClause> { clause };

            return clause.ItemTypeEnum switch
            {
                MotelyFilterItemType.Joker => new MotelyJsonJokerFilterDesc(
                    MotelyJsonJokerFilterClause.CreateCriteria(
                        MotelyJsonJokerFilterClause.ConvertClauses(singleClauseList)
                    )
                ),
                MotelyFilterItemType.SoulJoker => new MotelyJsonSoulJokerFilterDesc(
                    MotelyJsonSoulJokerFilterClause.CreateCriteria(
                        MotelyJsonSoulJokerFilterClause.ConvertClauses(singleClauseList)
                    )
                ),
                MotelyFilterItemType.Voucher => new MotelyJsonVoucherFilterDesc(
                    MotelyJsonVoucherFilterClause.CreateCriteria(
                        MotelyJsonVoucherFilterClause.ConvertClauses(singleClauseList)
                    )
                ),
                MotelyFilterItemType.TarotCard => new MotelyJsonTarotCardFilterDesc(
                    MotelyJsonTarotFilterClause.CreateCriteria(
                        MotelyJsonTarotFilterClause.ConvertClauses(singleClauseList)
                    )
                ),
                MotelyFilterItemType.PlanetCard => new MotelyJsonPlanetFilterDesc(
                    MotelyJsonPlanetFilterClause.CreateCriteria(
                        MotelyJsonPlanetFilterClause.ConvertClauses(singleClauseList)
                    )
                ),
                MotelyFilterItemType.SpectralCard => new MotelyJsonSpectralCardFilterDesc(
                    MotelyJsonSpectralFilterClause.CreateCriteria(
                        MotelyJsonSpectralFilterClause.ConvertClauses(singleClauseList)
                    )
                ),
                MotelyFilterItemType.PlayingCard => new MotelyJsonPlayingCardFilterDesc(
                    MotelyJsonFilterClauseExtensions.CreatePlayingCardCriteria(singleClauseList)
                ),
                MotelyFilterItemType.Boss => new MotelyJsonBossFilterDesc(
                    MotelyJsonFilterClauseExtensions.CreateBossCriteria(singleClauseList)
                ),
                MotelyFilterItemType.SmallBlindTag or MotelyFilterItemType.BigBlindTag => new MotelyJsonTagFilterDesc(
                    MotelyJsonFilterClauseExtensions.CreateTagCriteria(singleClauseList)
                ),
                MotelyFilterItemType.Event => new MotelyJsonEventFilterDesc(
                    MotelyJsonFilterClauseExtensions.CreateEventCriteria(singleClauseList)
                ),
                MotelyFilterItemType.ErraticRank => new MotelyJsonErraticRankFilterDesc(
                    clause.RankEnum!.Value,
                    clause.Min ?? 1
                ),
                MotelyFilterItemType.ErraticSuit => new MotelyJsonErraticSuitFilterDesc(
                    clause.SuitEnum!.Value,
                    clause.Min ?? 1
                ),
                MotelyFilterItemType.And or MotelyFilterItemType.Or => new MotelyCompositeFilterDesc(singleClauseList),
                _ => throw new ArgumentException($"Unsupported filter type: {clause.ItemTypeEnum}")
            };
        }

        // Helper: Create search settings for a filter (handles all filter types)
        private static dynamic CreateSearchSettings(IMotelySeedFilterDesc filterDesc, MotelyFilterItemType itemType)
        {
            return itemType switch
            {
                MotelyFilterItemType.Joker => new MotelySearchSettings<MotelyJsonJokerFilterDesc.MotelyJsonJokerFilter>(
                    (MotelyJsonJokerFilterDesc)filterDesc
                ),
                MotelyFilterItemType.SoulJoker => new MotelySearchSettings<MotelyJsonSoulJokerFilterDesc.MotelyJsonSoulJokerFilter>(
                    (MotelyJsonSoulJokerFilterDesc)filterDesc
                ),
                MotelyFilterItemType.Voucher => new MotelySearchSettings<MotelyJsonVoucherFilterDesc.MotelyJsonVoucherFilter>(
                    (MotelyJsonVoucherFilterDesc)filterDesc
                ),
                MotelyFilterItemType.TarotCard => new MotelySearchSettings<MotelyJsonTarotCardFilterDesc.MotelyJsonTarotCardFilter>(
                    (MotelyJsonTarotCardFilterDesc)filterDesc
                ),
                MotelyFilterItemType.PlanetCard => new MotelySearchSettings<MotelyJsonPlanetFilterDesc.MotelyJsonPlanetFilter>(
                    (MotelyJsonPlanetFilterDesc)filterDesc
                ),
                MotelyFilterItemType.SpectralCard => new MotelySearchSettings<MotelyJsonSpectralCardFilterDesc.MotelyJsonSpectralCardFilter>(
                    (MotelyJsonSpectralCardFilterDesc)filterDesc
                ),
                MotelyFilterItemType.PlayingCard => new MotelySearchSettings<MotelyJsonPlayingCardFilterDesc.MotelyJsonPlayingCardFilter>(
                    (MotelyJsonPlayingCardFilterDesc)filterDesc
                ),
                MotelyFilterItemType.Boss => new MotelySearchSettings<MotelyJsonBossFilterDesc.MotelyJsonBossFilter>(
                    (MotelyJsonBossFilterDesc)filterDesc
                ),
                MotelyFilterItemType.SmallBlindTag or MotelyFilterItemType.BigBlindTag => new MotelySearchSettings<MotelyJsonTagFilterDesc.MotelyJsonTagFilter>(
                    (MotelyJsonTagFilterDesc)filterDesc
                ),
                MotelyFilterItemType.Event => new MotelySearchSettings<MotelyJsonEventFilterDesc.MotelyJsonEventFilter>(
                    (MotelyJsonEventFilterDesc)filterDesc
                ),
                MotelyFilterItemType.ErraticRank => new MotelySearchSettings<MotelyJsonErraticRankFilterDesc.MotelyJsonErraticRankFilter>(
                    (MotelyJsonErraticRankFilterDesc)filterDesc
                ),
                MotelyFilterItemType.ErraticSuit => new MotelySearchSettings<MotelyJsonErraticSuitFilterDesc.MotelyJsonErraticSuitFilter>(
                    (MotelyJsonErraticSuitFilterDesc)filterDesc
                ),
                MotelyFilterItemType.And or MotelyFilterItemType.Or => new MotelySearchSettings<MotelyCompositeFilterDesc.MotelyCompositeFilter>(
                    (MotelyCompositeFilterDesc)filterDesc
                ),
                _ => throw new ArgumentException($"Unsupported search settings type: {itemType}")
            };
        }

        // Keep old category-based code for now in case we need to revert
        private IMotelySearch CreateSearchOLD_GROUPED(MotelyJsonConfig config, IEnumerable<string>? seeds, bool preSorted, MotelyJsonSeedScoreDesc scoreDesc, List<MotelyJsonConfig.MotleyJsonFilterClause> mustClauses)
        {
            Dictionary<FilterCategory, List<MotelyJsonConfig.MotleyJsonFilterClause>> clausesByCategory = FilterCategoryMapper.GroupClausesByCategory(mustClauses);
            List<FilterCategory> categories = [.. clausesByCategory.Keys];

            if (categories.Count > 1)
            {
                // Multiple categories - use composite filter to avoid broken chaining
                if (!_params.Quiet)
                {
                    Console.WriteLine(
                        $"[COMPOSITE] Creating composite filter with {categories.Count} filter types"
                    );
                }

                // CRITICAL REFACTOR: Merge mustNot clauses into must clauses BEFORE creating composite
                // Mark mustNot clauses with IsInverted=true so they're handled in one pass
                var allRequiredClauses = new List<MotelyJsonConfig.MotleyJsonFilterClause>(
                    mustClauses
                );

                if (config.MustNot != null && config.MustNot.Count > 0)
                {
                    // Initialize parsed enums for MustNot clauses
                    for (int i = 0; i < config.MustNot.Count; i++)
                    {
                        InitializeClauseWithContext(config.MustNot[i], "MUSTNOT", i);
                    }

                    if (!_params.Quiet)
                    {
                        Console.WriteLine(
                            $"   + Including MustNot: {config.MustNot.Count} inverted clauses (exclusion)"
                        );
                    }

                    // Mark mustNot clauses as inverted and add to the composite
                    foreach (var clause in config.MustNot)
                    {
                        clause.IsInverted = true;
                        allRequiredClauses.Add(clause);
                    }
                }

                // Create ONE composite filter with both must and mustNot clauses
                var compositeFilter = new MotelyCompositeFilterDesc(allRequiredClauses);
                var compositeSettings =
                    new MotelySearchSettings<MotelyCompositeFilterDesc.MotelyCompositeFilter>(
                        compositeFilter
                    );

                // Apply all the same settings
                if (
                    !string.IsNullOrEmpty(config.Deck)
                    && Enum.TryParse(config.Deck, true, out MotelyDeck compositeDeck)
                )
                    compositeSettings = compositeSettings.WithDeck(compositeDeck);
                if (
                    !string.IsNullOrEmpty(config.Stake)
                    && Enum.TryParse(config.Stake, true, out MotelyStake compositeStake)
                )
                    compositeSettings = compositeSettings.WithStake(compositeStake);

                compositeSettings = compositeSettings.WithThreadCount(_params.Threads);
                compositeSettings = compositeSettings.WithBatchCharacterCount(_params.BatchSize);
                compositeSettings = compositeSettings.WithStartBatchIndex((long)_params.StartBatch);
                if (_params.EndBatch > 0)
                    compositeSettings = compositeSettings.WithEndBatchIndex((long)_params.EndBatch);

                // Always enable CSV output and scoring (score will be 0 if no SHOULD clauses)
                compositeSettings = compositeSettings.WithSeedScoreProvider(scoreDesc);
                compositeSettings = compositeSettings.WithCsvOutput(true);

                // Apply quiet mode
                if (_params.Quiet)
                {
                    compositeSettings = compositeSettings.WithQuietMode(true);
                }
                if (_params.ProgressCallback != null)
                    compositeSettings = compositeSettings.WithProgressCallback(_params.ProgressCallback);

                // Start search with composite filter (no chaining needed!)
                if (_params.RandomSeeds.HasValue)
                    return (IMotelySearch)
                        compositeSettings.WithRandomSearch(_params.RandomSeeds.Value).Start();
                else if (seeds != null)
                    return (IMotelySearch)compositeSettings.WithListSearch(seeds, preSorted).Start();
                else
                    return (IMotelySearch)compositeSettings.WithSequentialSearch().Start();
            }

            // Single category - but check if we have mustNot clauses to merge
            FilterCategory primaryCategory = categories[0];
            List<MotelyJsonConfig.MotleyJsonFilterClause> primaryClauses = clausesByCategory[
                primaryCategory
            ];

            // If we have mustNot clauses, use composite filter to handle both must and mustNot in one pass
            bool hasMustNot = config.MustNot != null && config.MustNot.Count > 0;
            if (hasMustNot)
            {
                if (!_params.Quiet)
                {
                    Console.WriteLine(
                        $"[COMPOSITE] Single category with mustNot - using composite filter"
                    );
                    Console.WriteLine(
                        $"   Must: {primaryClauses.Count} clauses ({primaryCategory})"
                    );
                }

                // Initialize and mark mustNot clauses as inverted
                var allRequiredClauses = new List<MotelyJsonConfig.MotleyJsonFilterClause>(
                    primaryClauses
                );

                for (int i = 0; i < config.MustNot!.Count; i++)
                {
                    InitializeClauseWithContext(config.MustNot[i], "MUSTNOT", i);
                }

                if (!_params.Quiet)
                {
                    Console.WriteLine(
                        $"   MustNot: {config.MustNot.Count} inverted clauses (exclusion)"
                    );
                }

                // Mark mustNot clauses as inverted and add to composite
                foreach (var clause in config.MustNot)
                {
                    clause.IsInverted = true;
                    allRequiredClauses.Add(clause);
                }

                // Use composite filter for both must and mustNot
                var compositeFilter = new MotelyCompositeFilterDesc(allRequiredClauses);
                var compositeSettings =
                    new MotelySearchSettings<MotelyCompositeFilterDesc.MotelyCompositeFilter>(
                        compositeFilter
                    );

                // Apply all settings
                if (
                    !string.IsNullOrEmpty(config.Deck)
                    && Enum.TryParse(config.Deck, true, out MotelyDeck compositeDeck)
                )
                    compositeSettings = compositeSettings.WithDeck(compositeDeck);
                if (
                    !string.IsNullOrEmpty(config.Stake)
                    && Enum.TryParse(config.Stake, true, out MotelyStake compositeStake)
                )
                    compositeSettings = compositeSettings.WithStake(compositeStake);

                compositeSettings = compositeSettings.WithThreadCount(_params.Threads);
                compositeSettings = compositeSettings.WithBatchCharacterCount(_params.BatchSize);
                compositeSettings = compositeSettings.WithStartBatchIndex((long)_params.StartBatch);
                if (_params.EndBatch > 0)
                    compositeSettings = compositeSettings.WithEndBatchIndex((long)_params.EndBatch);

                // Always enable CSV output and scoring (score will be 0 if no SHOULD clauses)
                compositeSettings = compositeSettings.WithSeedScoreProvider(scoreDesc);
                compositeSettings = compositeSettings.WithCsvOutput(true);

                if (_params.Quiet)
                    compositeSettings = compositeSettings.WithQuietMode(true);
                if (_params.ProgressCallback != null)
                    compositeSettings = compositeSettings.WithProgressCallback(_params.ProgressCallback);

                // Start search with composite filter
                if (_params.RandomSeeds.HasValue)
                    return (IMotelySearch)
                        compositeSettings.WithRandomSearch(_params.RandomSeeds.Value).Start();
                else if (seeds != null)
                    return (IMotelySearch)compositeSettings.WithListSearch(seeds, preSorted).Start();
                else
                    return (IMotelySearch)compositeSettings.WithSequentialSearch().Start();
            }

            // Single category with no mustNot - use specialized filter directly
            if (!_params.Quiet)
            {
                Console.WriteLine(
                    $"[FILTER SETUP] Base filter: {primaryCategory} with {primaryClauses.Count} clauses"
                );
            }

            IMotelySeedFilterDesc filterDesc = primaryCategory switch
            {
                FilterCategory.SoulJoker => new MotelyJsonSoulJokerFilterDesc(
                    MotelyJsonSoulJokerFilterClause.CreateCriteria(
                        MotelyJsonSoulJokerFilterClause.ConvertClauses(primaryClauses)
                    )
                ),
                FilterCategory.SoulJokerEditionOnly => new MotelyJsonSoulJokerEditionOnlyFilterDesc(
                    MotelyJsonSoulJokerFilterClause.CreateCriteria(
                        MotelyJsonSoulJokerFilterClause.ConvertClauses(primaryClauses)
                    )
                ),
                FilterCategory.Joker => new MotelyJsonJokerFilterDesc(
                    MotelyJsonJokerFilterClause.CreateCriteria(
                        MotelyJsonJokerFilterClause.ConvertClauses(primaryClauses)
                    )
                ),
                FilterCategory.Voucher => new MotelyJsonVoucherFilterDesc(
                    MotelyJsonVoucherFilterClause.CreateCriteria(
                        MotelyJsonVoucherFilterClause.ConvertClauses(primaryClauses)
                    )
                ),
                FilterCategory.PlanetCard => new MotelyJsonPlanetFilterDesc(
                    MotelyJsonPlanetFilterClause.CreateCriteria(
                        MotelyJsonPlanetFilterClause.ConvertClauses(primaryClauses)
                    )
                ),
                FilterCategory.TarotCard => new MotelyJsonTarotCardFilterDesc(
                    MotelyJsonTarotFilterClause.CreateCriteria(
                        MotelyJsonTarotFilterClause.ConvertClauses(primaryClauses)
                    )
                ),
                FilterCategory.SpectralCard => new MotelyJsonSpectralCardFilterDesc(
                    MotelyJsonSpectralFilterClause.CreateCriteria(
                        MotelyJsonSpectralFilterClause.ConvertClauses(primaryClauses)
                    )
                ),
                FilterCategory.PlayingCard => new MotelyJsonPlayingCardFilterDesc(
                    MotelyJsonFilterClauseExtensions.CreatePlayingCardCriteria(primaryClauses)
                ),
                FilterCategory.Boss => new MotelyJsonBossFilterDesc(
                    MotelyJsonFilterClauseExtensions.CreateBossCriteria(primaryClauses)
                ),
                FilterCategory.Tag => new MotelyJsonTagFilterDesc(
                    MotelyJsonFilterClauseExtensions.CreateTagCriteria(primaryClauses)
                ),
                FilterCategory.Event => new MotelyJsonEventFilterDesc(
                    MotelyJsonFilterClauseExtensions.CreateEventCriteria(primaryClauses)
                ),
                FilterCategory.ErraticRank => new MotelyJsonErraticRankFilterDesc(
                    primaryClauses[0].RankEnum!.Value,
                    primaryClauses[0].Min ?? 1
                ),
                FilterCategory.ErraticSuit => new MotelyJsonErraticSuitFilterDesc(
                    primaryClauses[0].SuitEnum!.Value,
                    primaryClauses[0].Min ?? 1
                ),
                FilterCategory.ErraticRankAndSuit => new MotelyJsonErraticRankAndSuitFilterDesc(
                    MotelyJsonFilterClauseExtensions.CreateErraticRankAndSuitCriteria(primaryClauses)
                ),
                FilterCategory.And or FilterCategory.Or => new MotelyCompositeFilterDesc(
                    primaryClauses
                ),
                _ => throw new ArgumentException(
                    $"Specialized filter not implemented: {primaryCategory}"
                ),
            };

            // Single category with no mustNot - create specialized filter settings
            dynamic searchSettings = primaryCategory switch
            {
                FilterCategory.SoulJoker =>
                    new MotelySearchSettings<MotelyJsonSoulJokerFilterDesc.MotelyJsonSoulJokerFilter>(
                        (MotelyJsonSoulJokerFilterDesc)filterDesc
                    ),
                FilterCategory.SoulJokerEditionOnly =>
                    new MotelySearchSettings<MotelyJsonSoulJokerEditionOnlyFilterDesc.MotelyJsonSoulJokerEditionOnlyFilter>(
                        (MotelyJsonSoulJokerEditionOnlyFilterDesc)filterDesc
                    ),
                FilterCategory.Joker =>
                    new MotelySearchSettings<MotelyJsonJokerFilterDesc.MotelyJsonJokerFilter>(
                        (MotelyJsonJokerFilterDesc)filterDesc
                    ),
                FilterCategory.Voucher =>
                    new MotelySearchSettings<MotelyJsonVoucherFilterDesc.MotelyJsonVoucherFilter>(
                        (MotelyJsonVoucherFilterDesc)filterDesc
                    ),
                FilterCategory.PlanetCard =>
                    new MotelySearchSettings<MotelyJsonPlanetFilterDesc.MotelyJsonPlanetFilter>(
                        (MotelyJsonPlanetFilterDesc)filterDesc
                    ),
                FilterCategory.TarotCard =>
                    new MotelySearchSettings<MotelyJsonTarotCardFilterDesc.MotelyJsonTarotCardFilter>(
                        (MotelyJsonTarotCardFilterDesc)filterDesc
                    ),
                FilterCategory.SpectralCard =>
                    new MotelySearchSettings<MotelyJsonSpectralCardFilterDesc.MotelyJsonSpectralCardFilter>(
                        (MotelyJsonSpectralCardFilterDesc)filterDesc
                    ),
                FilterCategory.PlayingCard =>
                    new MotelySearchSettings<MotelyJsonPlayingCardFilterDesc.MotelyJsonPlayingCardFilter>(
                        (MotelyJsonPlayingCardFilterDesc)filterDesc
                    ),
                FilterCategory.Boss =>
                    new MotelySearchSettings<MotelyJsonBossFilterDesc.MotelyJsonBossFilter>(
                        (MotelyJsonBossFilterDesc)filterDesc
                    ),
                FilterCategory.Tag =>
                    new MotelySearchSettings<MotelyJsonTagFilterDesc.MotelyJsonTagFilter>(
                        (MotelyJsonTagFilterDesc)filterDesc
                    ),
                FilterCategory.Event =>
                    new MotelySearchSettings<MotelyJsonEventFilterDesc.MotelyJsonEventFilter>(
                        (MotelyJsonEventFilterDesc)filterDesc
                    ),
                FilterCategory.ErraticRank =>
                    new MotelySearchSettings<MotelyJsonErraticRankFilterDesc.MotelyJsonErraticRankFilter>(
                        (MotelyJsonErraticRankFilterDesc)filterDesc
                    ),
                FilterCategory.ErraticSuit =>
                    new MotelySearchSettings<MotelyJsonErraticSuitFilterDesc.MotelyJsonErraticSuitFilter>(
                        (MotelyJsonErraticSuitFilterDesc)filterDesc
                    ),
                FilterCategory.ErraticRankAndSuit =>
                    new MotelySearchSettings<MotelyJsonErraticRankAndSuitFilterDesc.MotelyJsonErraticRankAndSuitFilter>(
                        (MotelyJsonErraticRankAndSuitFilterDesc)filterDesc
                    ),
                FilterCategory.And or FilterCategory.Or =>
                    new MotelySearchSettings<MotelyCompositeFilterDesc.MotelyCompositeFilter>(
                        (MotelyCompositeFilterDesc)filterDesc
                    ),
                _ => throw new ArgumentException(
                    $"Search settings not implemented: {primaryCategory}"
                ),
            };

            if (!_params.Quiet)
            {
                Console.WriteLine(
                    $"   + Base {primaryCategory} filter: {primaryClauses.Count} clauses"
                );
            }

            // Chain additional filters
            for (int i = 1; i < categories.Count; i++)
            {
                FilterCategory category = categories[i];
                List<MotelyJsonConfig.MotleyJsonFilterClause> clauses = clausesByCategory[category];
                IMotelySeedFilterDesc additionalFilter = category switch
                {
                    FilterCategory.SoulJoker => new MotelyJsonSoulJokerFilterDesc(
                        MotelyJsonSoulJokerFilterClause.CreateCriteria(
                            MotelyJsonSoulJokerFilterClause.ConvertClauses(clauses)
                        )
                    ),
                    FilterCategory.SoulJokerEditionOnly =>
                        new MotelyJsonSoulJokerEditionOnlyFilterDesc(
                            MotelyJsonSoulJokerFilterClause.CreateCriteria(
                                MotelyJsonSoulJokerFilterClause.ConvertClauses(clauses)
                            )
                        ),
                    FilterCategory.Joker => new MotelyJsonJokerFilterDesc(
                        MotelyJsonJokerFilterClause.CreateCriteria(
                            MotelyJsonJokerFilterClause.ConvertClauses(clauses)
                        )
                    ),
                    FilterCategory.Voucher => new MotelyJsonVoucherFilterDesc(
                        MotelyJsonVoucherFilterClause.CreateCriteria(
                            MotelyJsonVoucherFilterClause.ConvertClauses(clauses)
                        )
                    ),
                    FilterCategory.PlanetCard => new MotelyJsonPlanetFilterDesc(
                        MotelyJsonPlanetFilterClause.CreateCriteria(
                            MotelyJsonPlanetFilterClause.ConvertClauses(clauses)
                        )
                    ),
                    FilterCategory.TarotCard => new MotelyJsonTarotCardFilterDesc(
                        MotelyJsonTarotFilterClause.CreateCriteria(
                            MotelyJsonTarotFilterClause.ConvertClauses(clauses)
                        )
                    ),
                    FilterCategory.SpectralCard => new MotelyJsonSpectralCardFilterDesc(
                        MotelyJsonSpectralFilterClause.CreateCriteria(
                            MotelyJsonSpectralFilterClause.ConvertClauses(clauses)
                        )
                    ),
                    FilterCategory.PlayingCard => new MotelyJsonPlayingCardFilterDesc(
                        MotelyJsonFilterClauseExtensions.CreatePlayingCardCriteria(clauses)
                    ),
                    FilterCategory.Boss => new MotelyJsonBossFilterDesc(
                        MotelyJsonFilterClauseExtensions.CreateBossCriteria(clauses)
                    ),
                    FilterCategory.Tag => new MotelyJsonTagFilterDesc(
                        MotelyJsonFilterClauseExtensions.CreateTagCriteria(clauses)
                    ),
                    FilterCategory.Event => new MotelyJsonEventFilterDesc(
                        MotelyJsonFilterClauseExtensions.CreateEventCriteria(clauses)
                    ),
                    FilterCategory.ErraticRank => new MotelyJsonErraticRankFilterDesc(
                        clauses[0].RankEnum!.Value,
                        clauses[0].Min ?? 1
                    ),
                    FilterCategory.ErraticSuit => new MotelyJsonErraticSuitFilterDesc(
                        clauses[0].SuitEnum!.Value,
                        clauses[0].Min ?? 1
                    ),
                    FilterCategory.ErraticRankAndSuit => new MotelyJsonErraticRankAndSuitFilterDesc(
                        MotelyJsonFilterClauseExtensions.CreateErraticRankAndSuitCriteria(clauses)
                    ),
                    FilterCategory.And or FilterCategory.Or => new MotelyCompositeFilterDesc(
                        clauses
                    ),
                    _ => throw new ArgumentException(
                        $"Additional filter not implemented: {category}"
                    ),
                };
                searchSettings = searchSettings.WithAdditionalFilter(additionalFilter);
                if (!_params.Quiet)
                {
                    Console.WriteLine($"   + Chained {category} filter: {clauses.Count} clauses");
                }
            }

            // Always enable CSV output and scoring (score will be 0 if no SHOULD clauses)
            searchSettings = searchSettings.WithSeedScoreProvider(scoreDesc);
            searchSettings = searchSettings.WithCsvOutput(true);

            // Apply quiet mode
            if (_params.Quiet)
            {
                searchSettings = searchSettings.WithQuietMode(true);
            }
            if (_params.ProgressCallback != null)
                searchSettings = searchSettings.WithProgressCallback(_params.ProgressCallback);

            // Apply deck and stake
            if (
                !string.IsNullOrEmpty(config.Deck)
                && Enum.TryParse(config.Deck, true, out MotelyDeck deck)
            )
            {
                searchSettings = searchSettings.WithDeck(deck);
            }

            if (
                !string.IsNullOrEmpty(config.Stake)
                && Enum.TryParse(config.Stake, true, out MotelyStake stake)
            )
            {
                searchSettings = searchSettings.WithStake(stake);
            }

            // Set batch configuration
            searchSettings = searchSettings.WithThreadCount(_params.Threads);
            searchSettings = searchSettings.WithBatchCharacterCount(_params.BatchSize);
            searchSettings = searchSettings.WithStartBatchIndex((long)_params.StartBatch);
            if (_params.EndBatch > 0)
            {
                searchSettings = searchSettings.WithEndBatchIndex((long)_params.EndBatch);
            }

            // Start search
            if (_params.RandomSeeds.HasValue)
            {
                // Use random seed provider for testing
                return (IMotelySearch)
                    searchSettings.WithRandomSearch(_params.RandomSeeds.Value).Start();
            }
            else if (seeds != null)
            {
                // Use provided seed list (streaming IEnumerable for DuckDB)
                return (IMotelySearch)searchSettings.WithListSearch(seeds, preSorted).Start();
            }
            else
            {
                // Use sequential search
                return (IMotelySearch)searchSettings.WithSequentialSearch().Start();
            }
        }

        private void PrintResultsHeader(MotelyJsonConfig config)
        {
            if (!_params.Quiet)
            {
                Console.WriteLine($"# Deck: {config.Deck}, Stake: {config.Stake}");
            }

            // ONE SOURCE OF TRUTH: Use GetColumnNames()
            var columnNames = config.GetColumnNames();
            var quotedColumns = columnNames.Select(name => $"\"{name}\"");
            Console.WriteLine(string.Join(",", quotedColumns));
        }

        /// <summary>
        /// Generate a meaningful column name for a clause, handling And/Or groupings
        /// </summary>
        private static string GetClauseHeaderName(MotelyJsonConfig.MotleyJsonFilterClause clause)
        {
            // Handle And/Or clauses with nested clauses
            if (
                clause.ItemTypeEnum == MotelyFilterItemType.And
                || clause.ItemTypeEnum == MotelyFilterItemType.Or
            )
            {
                if (clause.Clauses == null || clause.Clauses.Count == 0)
                    return clause.ItemTypeEnum == MotelyFilterItemType.And
                        ? "And(empty)"
                        : "Or(empty)";

                // Create a compound name from nested clauses (recursively)
                var nestedNames = clause
                    .Clauses.Select(c => GetClauseBaseNameInternal(c))
                    .ToArray();
                string baseName =
                    clause.ItemTypeEnum == MotelyFilterItemType.And
                        ? $"And({string.Join("+", nestedNames)})"
                        : $"Or({string.Join("+", nestedNames)})";

                // Extract ante from first nested clause that has it (they should all match for And/Or groups)
                var anteClause = clause.Clauses.FirstOrDefault(c =>
                    c.Antes != null && c.Antes.Length > 0
                );
                if (anteClause != null && anteClause.Antes != null)
                {
                    var suffix = FormatAnteSuffix(anteClause.Antes);
                    if (!string.IsNullOrEmpty(suffix))
                        baseName += suffix;
                }

                return baseName;
            }

            // Standard clause - get base name and add ante suffix
            string name = GetClauseBaseNameInternal(clause);

            // Add ante suffix if specific antes are specified (not default all antes)
            if (clause.Antes != null && clause.Antes.Length > 0)
            {
                var suffix = FormatAnteSuffix(clause.Antes);
                if (!string.IsNullOrEmpty(suffix))
                    name += suffix;
            }

            return name;
        }

        // Format ante suffix as @A<min>-<max> when contiguous, otherwise @A<list>
        private static string FormatAnteSuffix(int[] antes)
        {
            if (antes == null || antes.Length == 0)
                return string.Empty;
            // Hide suffix if it's all 8 antes (default behavior)
            if (antes.Length >= 8)
                return string.Empty;

            if (antes.Length == 1)
                return $"@A{antes[0]}";

            // Sort and check contiguity
            var sorted = (int[])antes.Clone();
            Array.Sort(sorted);
            int min = sorted[0];
            int max = sorted[sorted.Length - 1];
            bool contiguous = (max - min + 1) == sorted.Length;
            if (contiguous)
                return $"@A{min}-{max}";

            return $"@A{string.Join("_", sorted)}";
        }

        private static string GetClauseBaseNameInternal(
            MotelyJsonConfig.MotleyJsonFilterClause clause
        )
        {
            if (!string.IsNullOrEmpty(clause.Label))
                return clause.Label;

            if (!string.IsNullOrEmpty(clause.Value))
                return clause.Value;

            if (clause.Values != null && clause.Values.Length > 0)
            {
                if (clause.Values.Length > 1)
                    return string.Join("+", clause.Values);
                else
                    return clause.Values[0];
            }

            return clause.Type;
        }

        private void PrintResultsSummary(IMotelySearch search, bool wasCancelled)
        {
            Console.Out.Flush();
            Console.WriteLine("\n" + new string('═', 60));
            Console.WriteLine(wasCancelled ? "🛑 SEARCH STOPPED" : "✅ SEARCH COMPLETED");
            Console.WriteLine(new string('═', 60));

            long lastBatchIndex =
                search.CompletedBatchCount > 0
                    ? (long)_params.StartBatch + search.CompletedBatchCount
                    : 0;

            // Calculate percentage of total search space
            long maxBatches = (long)Math.Pow(35, 8 - _params.BatchSize);
            int percentComplete = (int)(lastBatchIndex * 100 / maxBatches);

            Console.WriteLine($"   Last batch: {lastBatchIndex:N0} ({percentComplete}%)");
            Console.WriteLine($"   Seeds passed filter: {search.FilteredSeeds}");
            Console.WriteLine($"   Seeds passed cutoff: {search.MatchingSeeds}");

            
                Console.WriteLine($"   Duration: {search.ElapsedTime:hh\\:mm\\:ss\\.fff}");
                Console.WriteLine(
                    $"   Total seeds: {search.TotalSeedsSearched:N0} ({search.CompletedBatchCount} batches)"
                );
                double speed = (double)search.TotalSeedsSearched / search.ElapsedTime.TotalMilliseconds;
                Console.WriteLine($"   Speed: {speed:N0} seeds/ms");
            

            // Only show "To continue" message if search was cancelled (interrupted)
            if (wasCancelled)
            {
                Console.WriteLine(
                    $"💡 To continue: --startBatch {lastBatchIndex} or --startPercent {percentComplete}"
                );
                Console.WriteLine(new string('═', 60));
            }
        }
    }

    public record JsonSearchParams
    {
        public string Config { get; set; } = "standard";
        public int Threads { get; set; } = Environment.ProcessorCount;
        public int BatchSize { get; set; } = 1;
        public ulong StartBatch { get; set; }
        public ulong EndBatch { get; set; }
        public int Cutoff { get; set; }
        public bool AutoCutoff { get; set; }
        public bool EnableDebug { get; set; }
        public bool NoFancy { get; set; }
        public bool Quiet { get; set; }
        public string? SpecificSeed { get; set; }
        public string? Wordlist { get; set; }
        /// <summary>
        /// DuckDB file path containing seeds table (e.g., fertilizer.db)
        /// Use this for large seed collections - more efficient than SeedList
        /// </summary>
        public string? DbList { get; set; }
        public List<string>? SeedList { get; set; }
        public int? RandomSeeds { get; set; }
        /// <summary>
        /// Progress callback: (completedBatches, totalBatches, seedsSearched, seedsPerMs)
        /// </summary>
        public Action<long, long, long, double>? ProgressCallback { get; set; }
    }
}
