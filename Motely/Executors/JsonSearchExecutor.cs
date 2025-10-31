using Motely.Filters;
using Motely.Utils;

namespace Motely.Executors
{
    /// <summary>
    /// Executes JSON-based filter searches with specialized vectorized filters
    /// </summary>
    public sealed class JsonSearchExecutor(string configPath, JsonSearchParams parameters)
    {
        private readonly string _configPath = configPath;
        private readonly JsonSearchParams _params = parameters;
        private bool _cancelled = false;

        public int Execute()
        {
            DebugLogger.IsEnabled = _params.EnableDebug;
            FancyConsole.IsEnabled = !_params.NoFancy;
            // Gate colored output based on --nofancy
            TallyColorizer.ColorEnabled = !_params.NoFancy;

            List<string>? seeds = LoadSeeds();

            // Suppress startup messages in quiet mode
            if (!_params.Quiet)
            {
                Console.WriteLine($"🔍 Motely Ouija Search Starting");
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
                IMotelySearch search = CreateSearch(config, seeds);
                if (search == null)
                {
                    return 1;
                }

                // Print CSV header if we have SHOULD clauses for scoring
                if (config.Should?.Count > 0)
                {
                    PrintResultsHeader(config);
                }

                // Setup cancellation handler
                Console.CancelKeyPress += (sender, e) =>
                {
                    e.Cancel = true;
                    _cancelled = true;
                    if (!_params.Quiet)
                    {
                        Console.WriteLine("\n🛑 Stopping search...");
                    }
                    // Don't dispose here - let it finish gracefully
                };

                search.Start();

                // Wait for completion - progress shown by MotelySearch.PrintReport() in FancyConsole bottom line
                while (search.Status != MotelySearchStatus.Completed && !_cancelled)
                {
                    Thread.Sleep(100);
                }

                // Stop the search gracefully (if cancelled)
                if (_cancelled)
                {
                    search.Dispose();

                    // CRITICAL: Wait for final batch to flush before showing stats
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

        private List<string>? LoadSeeds()
        {
            if (!string.IsNullOrEmpty(_params.SpecificSeed))
            {
                if (!_params.Quiet)
                {
                    Console.WriteLine($"🔍 Searching for specific seed: {_params.SpecificSeed}");
                }
                // Return just the specific seed - let the system handle partial batches
                var seeds = new List<string>() { _params.SpecificSeed };
                return seeds;
            }

            if (!string.IsNullOrEmpty(_params.Wordlist))
            {
                string wordlistPath = Path.Combine("wordlists", _params.Wordlist + ".txt");
                if (!File.Exists(wordlistPath))
                {
                    throw new FileNotFoundException($"Wordlist not found: {wordlistPath}");
                }

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
                return seeds;
            }

            return null; // Sequential search
        }

        private MotelyJsonConfig LoadConfig()
        {
            string configPath;

            // If the caller passed a rooted or path-containing config, use it directly
            // (but append .json only when no extension is present). Otherwise treat
            // the value as a filter name under JsonItemFilters and append .json.
            bool hasDirectory = !string.IsNullOrEmpty(Path.GetDirectoryName(_configPath));
            if (Path.IsPathRooted(_configPath) || hasDirectory)
            {
                configPath = _configPath;
                if (string.IsNullOrEmpty(Path.GetExtension(configPath)))
                    configPath = configPath + ".json";
            }
            else
            {
                configPath = Path.Combine("JsonItemFilters", _configPath + ".json");
            }

            if (!File.Exists(configPath))
                throw new FileNotFoundException($"Could not find JSON config file: {configPath}");

            if (
                !MotelyJsonConfig.TryLoadFromJsonFile(
                    configPath,
                    out MotelyJsonConfig? config,
                    out string? error
                )
            )
                throw new Exception($"Failed to load config from {configPath}: {error}");

            return config;
        }

        private IMotelySearch CreateSearch(MotelyJsonConfig config, List<string>? seeds)
        {
            if (!_params.Quiet)
            {
                Console.WriteLine("CreateSearch...");
            }

            // CRITICAL: Validate and normalize config ONCE at load time!
            // This removes ALL ambiguity from the hot path!
            MotelyJsonConfigValidator.ValidateConfig(config);

            // Create scoring config (SHOULD clauses + voucher Must clauses for activation)
            var voucherMustClauses =
                config.Must?.Where(c => c.ItemTypeEnum == MotelyFilterItemType.Voucher).ToList()
                ?? [];
            MotelyJsonConfig scoringConfig = new()
            {
                Name = config.Name,
                Must = voucherMustClauses, // Include voucher Must clauses for MaxVoucherAnte calculation
                Should = config.Should, // ONLY the SHOULD clauses for scoring
                MustNot = [], // Empty - filters handle this
            };

            // Initialize parsed enums for scoring config clauses with helpful errors
            // NOTE: Don't re-initialize SHOULD clauses if they're the same objects as MUST clauses!
            for (int i = 0; i < voucherMustClauses.Count; i++)
            {
                var clause = voucherMustClauses[i];
                try
                {
                    clause.InitializeParsedEnums();
                }
                catch (Exception ex)
                {
                    var typeText = string.IsNullOrEmpty(clause.Type) ? "<missing>" : clause.Type;
                    var valueText = !string.IsNullOrEmpty(clause.Value)
                        ? clause.Value
                        : (
                            clause.Values != null && clause.Values.Length > 0
                                ? string.Join(", ", clause.Values)
                                : "<none>"
                        );
                    throw new ArgumentException(
                        $"Config error in MUST[{i}] — type: '{typeText}', value(s): '{valueText}'. {ex.Message}\nHint: Each clause needs a non-empty 'type' (e.g., 'Joker', 'TarotCard', 'PlayingCard'). If using multiple values, use 'values': [ ... ] not 'value'."
                    );
                }
            }

            // Only initialize SHOULD clauses if they haven't been initialized already
            if (config.Should != null)
            {
                for (int i = 0; i < config.Should.Count; i++)
                {
                    var shouldClause = config.Should[i];
                    // Check if this clause was already initialized as part of MUST clauses
                    bool alreadyInitialized = config.Must?.Contains(shouldClause) == true;
                    if (!alreadyInitialized)
                    {
                        try
                        {
                            shouldClause.InitializeParsedEnums();
                        }
                        catch (Exception ex)
                        {
                            var typeText = string.IsNullOrEmpty(shouldClause.Type)
                                ? "<missing>"
                                : shouldClause.Type;
                            var valueText = !string.IsNullOrEmpty(shouldClause.Value)
                                ? shouldClause.Value
                                : (
                                    shouldClause.Values != null && shouldClause.Values.Length > 0
                                        ? string.Join(", ", shouldClause.Values)
                                        : "<none>"
                                );
                            throw new ArgumentException(
                                $"Config error in SHOULD[{i}] — type: '{typeText}', value(s): '{valueText}'. {ex.Message}\nHint: 'type' must be one of supported types (e.g., 'Joker', 'Voucher', 'TarotCard')."
                            );
                        }
                    }
                }
            }

            // Process the scoring config to calculate MaxVoucherAnte
            scoringConfig.PostProcess();

            // Create callback for CSV output - always output when scoring
            Action<MotelySeedScoreTally> scoreCallback = (MotelySeedScoreTally result) =>
            {
                // Just print the seed - it will naturally push the progress line down
                FancyConsole.WriteLine(
                    TallyColorizer.FormatResultLine(result.Seed, result.Score, result.TallyColumns)
                );
            };

            MotelyJsonSeedScoreDesc scoreDesc = new(
                scoringConfig,
                _params.Cutoff,
                _params.AutoCutoff,
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

            // Use specialized filter system
            List<MotelyJsonConfig.MotleyJsonFilterClause> mustClauses = config.Must?.ToList() ?? [];

            // Initialize parsed enums for all MUST clauses with helpful errors
            for (int i = 0; i < mustClauses.Count; i++)
            {
                var clause = mustClauses[i];
                try
                {
                    clause.InitializeParsedEnums();
                }
                catch (Exception ex)
                {
                    var typeText = string.IsNullOrEmpty(clause.Type) ? "<missing>" : clause.Type;
                    var valueText = !string.IsNullOrEmpty(clause.Value)
                        ? clause.Value
                        : (
                            clause.Values != null && clause.Values.Length > 0
                                ? string.Join(", ", clause.Values)
                                : "<none>"
                        );
                    throw new ArgumentException(
                        $"Config error in MUST[{i}] — type: '{typeText}', value(s): '{valueText}'. {ex.Message}\nSuggestion: Add 'type' and 'value' (or 'values'): {{ \"type\": \"Joker\", \"value\": \"Perkeo\" }}"
                    );
                }
            }

            Dictionary<
                FilterCategory,
                List<MotelyJsonConfig.MotleyJsonFilterClause>
            > clausesByCategory = FilterCategoryMapper.GroupClausesByCategory(mustClauses);

            // If no MUST clauses, check if we have mustNot clauses to use as a composite filter
            if (clausesByCategory.Count == 0)
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
                        var clause = config.MustNot[i];
                        try
                        {
                            clause.InitializeParsedEnums();
                        }
                        catch (Exception ex)
                        {
                            var typeText = string.IsNullOrEmpty(clause.Type)
                                ? "<missing>"
                                : clause.Type;
                            var valueText = !string.IsNullOrEmpty(clause.Value)
                                ? clause.Value
                                : (
                                    clause.Values != null && clause.Values.Length > 0
                                        ? string.Join(", ", clause.Values)
                                        : "<none>"
                                );
                            throw new ArgumentException(
                                $"Config error in MUSTNOT[{i}] — type: '{typeText}', value(s): '{valueText}'. {ex.Message}"
                            );
                        }
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

                    if (seeds != null && seeds.Count > 0)
                        return (IMotelySearch)compositeSettings.WithListSearch(seeds).Start();
                    else
                        return (IMotelySearch)compositeSettings.WithSequentialSearch().Start();
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

                if (seeds != null && seeds.Count > 0)
                    return passthroughSettings.WithListSearch(seeds).Start();
                else
                    return passthroughSettings.WithSequentialSearch().Start();
            }

            // Boss filter now works with proper ante-loop structure

            // BYPASS BROKEN CHAINING: Use composite filter for multiple categories
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
                        var clause = config.MustNot[i];
                        try
                        {
                            clause.InitializeParsedEnums();
                        }
                        catch (Exception ex)
                        {
                            var typeText = string.IsNullOrEmpty(clause.Type)
                                ? "<missing>"
                                : clause.Type;
                            var valueText = !string.IsNullOrEmpty(clause.Value)
                                ? clause.Value
                                : (
                                    clause.Values != null && clause.Values.Length > 0
                                        ? string.Join(", ", clause.Values)
                                        : "<none>"
                                );
                            throw new ArgumentException(
                                $"Config error in MUSTNOT[{i}] — type: '{typeText}', value(s): '{valueText}'. {ex.Message}"
                            );
                        }
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

                bool compositeNeedsScoring = (config.Should?.Count > 0);
                if (compositeNeedsScoring)
                {
                    compositeSettings = compositeSettings.WithSeedScoreProvider(scoreDesc);
                    compositeSettings = compositeSettings.WithCsvOutput(true);
                }

                // Apply quiet mode
                if (_params.Quiet)
                {
                    compositeSettings = compositeSettings.WithQuietMode(true);
                }

                // Start search with composite filter (no chaining needed!)
                if (_params.RandomSeeds.HasValue)
                    return (IMotelySearch)
                        compositeSettings.WithRandomSearch(_params.RandomSeeds.Value).Start();
                else if (seeds != null && seeds.Count > 0)
                    return (IMotelySearch)compositeSettings.WithListSearch(seeds).Start();
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
                    var clause = config.MustNot[i];
                    try
                    {
                        clause.InitializeParsedEnums();
                    }
                    catch (Exception ex)
                    {
                        var typeText = string.IsNullOrEmpty(clause.Type)
                            ? "<missing>"
                            : clause.Type;
                        var valueText = !string.IsNullOrEmpty(clause.Value)
                            ? clause.Value
                            : (
                                clause.Values != null && clause.Values.Length > 0
                                    ? string.Join(", ", clause.Values)
                                    : "<none>"
                            );
                        throw new ArgumentException(
                            $"Config error in MUSTNOT[{i}] — type: '{typeText}', value(s): '{valueText}'. {ex.Message}"
                        );
                    }
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

                bool singleCategoryNeedsScoring = (config.Should?.Count > 0);
                if (singleCategoryNeedsScoring)
                {
                    compositeSettings = compositeSettings.WithSeedScoreProvider(scoreDesc);
                    compositeSettings = compositeSettings.WithCsvOutput(true);
                }

                if (_params.Quiet)
                    compositeSettings = compositeSettings.WithQuietMode(true);

                // Start search with composite filter
                if (_params.RandomSeeds.HasValue)
                    return (IMotelySearch)
                        compositeSettings.WithRandomSearch(_params.RandomSeeds.Value).Start();
                else if (seeds != null && seeds.Count > 0)
                    return (IMotelySearch)compositeSettings.WithListSearch(seeds).Start();
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

            // Add scoring when SHOULD clauses exist
            bool needsScoring = (config.Should?.Count > 0);
            if (needsScoring)
            {
                searchSettings = searchSettings.WithSeedScoreProvider(scoreDesc);
                // Always enable CSV output when scoring
                searchSettings = searchSettings.WithCsvOutput(true);
            }

            // Apply quiet mode
            if (_params.Quiet)
            {
                searchSettings = searchSettings.WithQuietMode(true);
            }

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
            else if (seeds != null && seeds.Count > 0)
            {
                // Use provided seed list
                return (IMotelySearch)searchSettings.WithListSearch(seeds).Start();
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
            string header = "Seed,TotalScore";
            if (config.Should != null)
            {
                foreach (MotelyJsonConfig.MotleyJsonFilterClause should in config.Should)
                {
                    string name = GetClauseHeaderName(should);
                    // Quote all column names for Excel compatibility
                    header += $",\"{name}\"";
                }
            }
            Console.WriteLine(header);
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

            TimeSpan elapsed = search.ElapsedTime;
            if (elapsed.TotalMilliseconds > 100)
            {
                Console.WriteLine($"   Duration: {elapsed:hh\\:mm\\:ss\\.fff}");
                Console.WriteLine(
                    $"   Total seeds: {search.TotalSeedsSearched:N0} ({search.CompletedBatchCount} batches)"
                );
                double speed = (double)search.TotalSeedsSearched / elapsed.TotalMilliseconds;
                Console.WriteLine($"   Speed: {speed:N0} seeds/ms");
            }
            Console.WriteLine(new string('═', 60));

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
        public int? RandomSeeds { get; set; }
    }
}
