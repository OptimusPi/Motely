using System.Diagnostics;
using Motely.Filters;

namespace Motely.Executors
{
    /// <summary>
    /// Executes built-in native filters (--native parameter)
    /// Handles: PerkeoObservatory, Trickeoglyph, NegativeCopy, etc.
    /// </summary>
    public class NativeFilterExecutor
    {
        private readonly string _filterName;
        private readonly string? _scoreConfig;
        private readonly JsonSearchParams _params;
        private bool _cancelled = false;
        private IEnumerable<string>? _searchSeeds = null;
        public global::Motely.DuckDB.MotelySearchDatabase? ResultsDatabase { get; set; }

        public NativeFilterExecutor(
            string filterName,
            JsonSearchParams parameters,
            string? scoreConfig = null
        )
        {
            _filterName = filterName;
            _scoreConfig = scoreConfig;
            _params = parameters;
        }

        public IMotelySearch ExecuteAsSearch()
        {
            DateTime lastProgressUpdate = DateTime.UtcNow;
            object progressLock = new object();
            
            Action<MotelyProgress>? progressCallback = (progress) =>
            {
                lock (progressLock)
                {
                    var now = DateTime.UtcNow;
                    var timeSinceLastUpdate = (now - lastProgressUpdate).TotalMilliseconds;
                    if (timeSinceLastUpdate < 2000) return;
                    lastProgressUpdate = now;

                    string timeLeftFormatted = "calculating...";
                    if (progress.TotalBatchCount > 0 && progress.CompletedBatchCount > 0)
                    {
                        if (progress.EstimatedTimeRemaining.HasValue)
                        {
                            var timeLeftSpan = progress.EstimatedTimeRemaining.Value;
                            timeLeftFormatted = timeLeftSpan.Days == 0 
                                ? $"{timeLeftSpan:hh\\:mm\\:ss}" 
                                : $"{timeLeftSpan:d\\:hh\\:mm\\:ss}";
                        }
                    }
                    double pct = progress.PercentComplete;
                    double elapsedMS = progress.ElapsedTime.TotalMilliseconds;
                    string[] spinnerFrames = ["⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧"];
                    var spinner = spinnerFrames[(int)(elapsedMS / 250) % spinnerFrames.Length];
                    double seedsPerSec = progress.SeedsPerMillisecond * 1000.0;
                    string progressLine = $"{spinner} {pct:F2}% | {timeLeftFormatted} remaining | {Math.Round(seedsPerSec)} seeds/sec";
                    Console.Write($"\r{progressLine}                    \r{progressLine}");
                }
            };

            // Return search without starting - caller will call Start(cancellationToken)
            return CreateFilterSearch(_filterName.ToLower().Trim(), _params.Quiet ? null : progressCallback);
        }

        public int Execute(CancellationToken cancellationToken = default)
        {
            var effectiveToken = cancellationToken != default ? cancellationToken : _params.CancellationToken ?? default;
            
            DebugLogger.IsEnabled = _params.EnableDebug;
            FancyConsole.IsEnabled = !_params.NoFancy;
            // Ensure tally colors respect --nofancy
            TallyColorizer.ColorEnabled = !_params.NoFancy;

            string normalizedFilterName = _filterName
                .ToLower(System.Globalization.CultureInfo.CurrentCulture)
                .Trim();

            // Progress callback - only used in silent mode or when fancy console is disabled
            // Otherwise FancyConsole handles progress display at the bottom line
            Action<MotelyProgress>? progressCallback = null;

            DateTime lastProgressUpdate = DateTime.UtcNow;
            object progressLock = new object();
            
            progressCallback = (progress) =>
            {
                lock (progressLock)
                {
                    var now = DateTime.UtcNow;
                    var timeSinceLastUpdate = (now - lastProgressUpdate).TotalMilliseconds;

                    // Throttle progress updates to every 2 seconds
                    if (timeSinceLastUpdate < 2000)
                        return;

                    lastProgressUpdate = now;

                    string timeLeftFormatted = "calculating...";
                    if (progress.TotalBatchCount > 0 && progress.CompletedBatchCount > 0)
                    {
                        if (progress.EstimatedTimeRemaining.HasValue)
                        {
                            var timeLeftSpan = progress.EstimatedTimeRemaining.Value;
                            timeLeftFormatted = timeLeftSpan.Days == 0 
                                ? $"{timeLeftSpan:hh\\:mm\\:ss}" 
                                : $"{timeLeftSpan:d\\:hh\\:mm\\:ss}";
                        }
                    }
                    double pct = progress.PercentComplete;
                    double elapsedMS = progress.ElapsedTime.TotalMilliseconds;
                    string[] spinnerFrames = ["⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧"];
                    var spinner = spinnerFrames[(int)(elapsedMS / 250) % spinnerFrames.Length];
                    string progressLine =
                        $"{spinner} {pct:F2}% | {timeLeftFormatted} remaining | {Math.Round(progress.SeedsPerMillisecond)} seeds/ms";
                    Console.Write($"\r{progressLine}                    \r{progressLine}");
                }
            };

            // Create the appropriate filter
            IMotelySearch search;
            try
            {
                search = CreateFilterSearch(normalizedFilterName, progressCallback);
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine($"❌ {ex.Message}");
                return 1;
            }

            Console.WriteLine(
                $"🔍 Running native filter: {_filterName}"
                    + (
                        !string.IsNullOrEmpty(_params.SpecificSeed)
                            ? $" on seed: {_params.SpecificSeed}"
                            : ""
                    )
                    + (!string.IsNullOrEmpty(_scoreConfig) ? $" with scoring: {_scoreConfig}" : "")
            );

            // Help identify non-determinism
            DebugLogger.Log($"Thread count: {_params.Threads}");
            DebugLogger.Log($"Batch size: {_params.BatchSize}");
            DebugLogger.Log($"Start batch: {_params.StartBatch}");
            DebugLogger.Log($"End batch: {_params.EndBatch}");

#if !BROWSER
            // Setup cancellation handler
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                _cancelled = true;
                Console.WriteLine("\n🛑 Stopping search...");
                // Use fast-path cancel to return immediately without waiting for threads
                search.Cancel();
            };
#endif

            var searchStopwatch = Stopwatch.StartNew();

            // Add debug output for batch range processing
            if (_params.StartBatch > 0 || _params.EndBatch > 0)
            {
                Console.WriteLine(
                    $"   Processing batches: {_params.StartBatch} to {_params.EndBatch}"
                );
                Console.WriteLine($"   Seeds per batch: {Math.Pow(35, _params.BatchSize):N0}");
                Console.WriteLine(
                    $"   Total seeds to search: {((_params.EndBatch - _params.StartBatch + 1) * Math.Pow(35, _params.BatchSize)):N0}"
                );
            }

            search.Start(effectiveToken);

            try
            {
                // Wait for completion - will exit early if cancellation token is signaled
                search.AwaitCompletion();

                searchStopwatch.Stop();
                PrintSummary(search, searchStopwatch.Elapsed);

                return 0;
            }
            finally
            {
                // Always dispose, but avoid double-dispose if cancelled and handler already disposed
                if (!_cancelled)
                {
                    search.Dispose();
                }
            }
        }

        /// <summary>
        /// Async version of Execute that doesn't block the calling thread.
        /// Uses WaitForCompletionAsync instead of AwaitCompletion.
        /// </summary>
        public async Task<int> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            var effectiveToken = cancellationToken != default ? cancellationToken : _params.CancellationToken ?? default;
            
            DebugLogger.IsEnabled = _params.EnableDebug;
            FancyConsole.IsEnabled = !_params.NoFancy;
            // Ensure tally colors respect --nofancy
            TallyColorizer.ColorEnabled = !_params.NoFancy;

            string normalizedFilterName = _filterName
                .ToLower(System.Globalization.CultureInfo.CurrentCulture)
                .Trim();

            // Progress callback
            Action<MotelyProgress>? progressCallback = null;

            DateTime lastProgressUpdate = DateTime.UtcNow;
            object progressLock = new object();
            
            progressCallback = (progress) =>
            {
                lock (progressLock)
                {
                    var now = DateTime.UtcNow;
                    var timeSinceLastUpdate = (now - lastProgressUpdate).TotalMilliseconds;

                    // Throttle progress updates to every 2 seconds
                    if (timeSinceLastUpdate < 2000)
                        return;

                    lastProgressUpdate = now;

                    string timeLeftFormatted = "calculating...";
                    if (progress.TotalBatchCount > 0 && progress.CompletedBatchCount > 0)
                    {
                        if (progress.EstimatedTimeRemaining.HasValue)
                        {
                            var timeLeftSpan = progress.EstimatedTimeRemaining.Value;
                            timeLeftFormatted = timeLeftSpan.Days == 0 
                                ? $"{timeLeftSpan:hh\\:mm\\:ss}" 
                                : $"{timeLeftSpan:d\\:hh\\:mm\\:ss}";
                        }
                    }
                    double pct = progress.PercentComplete;
                    double elapsedMS = progress.ElapsedTime.TotalMilliseconds;
                    string[] spinnerFrames = ["⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧"];
                    var spinner = spinnerFrames[(int)(elapsedMS / 250) % spinnerFrames.Length];
                    string progressLine =
                        $"{spinner} {pct:F2}% | {timeLeftFormatted} remaining | {Math.Round(progress.SeedsPerMillisecond)} seeds/ms";
                    Console.Write($"\r{progressLine}                    \r{progressLine}");
                }
            };

            // Create the appropriate filter
            IMotelySearch search;
            try
            {
                search = CreateFilterSearch(normalizedFilterName, progressCallback);
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine($"❌ {ex.Message}");
                return 1;
            }

            Console.WriteLine(
                $"🔍 Running native filter: {_filterName}"
                    + (
                        !string.IsNullOrEmpty(_params.SpecificSeed)
                            ? $" on seed: {_params.SpecificSeed}"
                            : ""
                    )
                    + (!string.IsNullOrEmpty(_scoreConfig) ? $" with scoring: {_scoreConfig}" : "")
            );

            // Help identify non-determinism
            DebugLogger.Log($"Thread count: {_params.Threads}");
            DebugLogger.Log($"Batch size: {_params.BatchSize}");
            DebugLogger.Log($"Start batch: {_params.StartBatch}");
            DebugLogger.Log($"End batch: {_params.EndBatch}");

            var searchStopwatch = Stopwatch.StartNew();

            // Add debug output for batch range processing
            if (_params.StartBatch > 0 || _params.EndBatch > 0)
            {
                Console.WriteLine(
                    $"   Processing batches: {_params.StartBatch} to {_params.EndBatch}"
                );
                Console.WriteLine($"   Seeds per batch: {Math.Pow(35, _params.BatchSize):N0}");
                Console.WriteLine(
                    $"   Total seeds to search: {((_params.EndBatch - _params.StartBatch + 1) * Math.Pow(35, _params.BatchSize)):N0}"
                );
            }

            search.Start(effectiveToken);

            try
            {
                // Wait for completion using async API - doesn't block the UI thread
                await search.WaitForCompletionAsync(effectiveToken).ConfigureAwait(false);

                searchStopwatch.Stop();
                PrintSummary(search, searchStopwatch.Elapsed);

                return 0;
            }
            catch (OperationCanceledException)
            {
                _cancelled = true;
                searchStopwatch.Stop();
                PrintSummary(search, searchStopwatch.Elapsed);
                return 0;
            }
            finally
            {
                // Always dispose, but avoid double-dispose if cancelled and handler already disposed
                if (!_cancelled)
                {
                    search.Dispose();
                }
            }
        }

        private IMotelySearch CreateFilterSearch(
            string filterName,
            Action<MotelyProgress>? progressCallback
        )
        {
            var seeds = LoadSeeds();
            var filterDesc = GetFilterDescriptor(filterName);

            return filterDesc switch
            {
                NaNSeedFilterDesc d => BuildSearch(d, progressCallback, seeds),
                PerkeoObservatoryFilterDesc d => BuildSearch(d, progressCallback, seeds),
                ObservatoryDesc d => BuildSearch(d, progressCallback, seeds),
                PassthroughFilterDesc d => BuildSearch(d, progressCallback, seeds),
                TrickeoglyphFilterDesc d => BuildSearch(d, progressCallback, seeds),
                NegativeCopyFilterDesc d => BuildSearch(d, progressCallback, seeds),
                NegativeTagFilterDesc d => BuildSearch(d, progressCallback, seeds),
                FilledSoulFilterDesc d => BuildSearch(d, progressCallback, seeds),
                ErraticFinderDesc d => BuildSearch(d, progressCallback, seeds),
                _ => throw new ArgumentException($"Unknown filter type: {filterDesc.GetType()}"),
            };
        }

        // Single method that builds the search with ALL the common settings
        private IMotelySearch BuildSearch<TFilter>(
            IMotelySeedFilterDesc<TFilter> filterDesc,
            Action<MotelyProgress>? progressCallback,
            IEnumerable<string>? seeds
        )
            where TFilter : struct, IMotelySeedFilter
        {
            var settings = new MotelySearchSettings<TFilter>(filterDesc)
                .WithThreadCount(_params.Threads)
                .WithBatchCharacterCount(_params.BatchSize);

            if (progressCallback != null)
            {
                settings = settings.WithProgressCallback(progressCallback);
            }

            settings = ApplyScoring(settings);

            // Set batch boundaries
            settings = settings.WithStartBatchIndex((long)_params.StartBatch);

            // Set end batch boundary (if user specified --endBatch, add 1 to make it inclusive)
            if (_params.EndBatch > 0)
            {
                settings = settings.WithEndBatchIndex((long)_params.EndBatch + 1);
            }
            // If endBatch=0, don't set end boundary (infinite search until Ctrl+C)

            if (_params.RandomSeeds.HasValue)
                return settings.WithRandomSearch(_params.RandomSeeds.Value).Start();
            else if (seeds != null && seeds.Any())
                return settings.WithListSearch(seeds).Start();
            else
                return settings.WithSequentialSearch().Start();
        }

        private object GetFilterDescriptor(string filterName)
        {
            var normalizedName = filterName
                .ToLower(System.Globalization.CultureInfo.CurrentCulture)
                .Trim();
            DebugLogger.Log($"Loading filter descriptor for: {normalizedName}");
            return normalizedName switch
            {
                "nanseed" => new NaNSeedFilterDesc(),
                "perkeoobservatory" => new PerkeoObservatoryFilterDesc(),
                "erraticfinder" => new ErraticFinderDesc(),
                "observatory" => new ObservatoryDesc(),
                "passthrough" => new PassthroughFilterDesc(), // for testing chaining but im leaving it cuz im lazy and cuz it might be useful some day?
                "trickeoglyph" => new TrickeoglyphFilterDesc(),
                "negativecopy" => new NegativeCopyFilterDesc(),
                "negativetag" => new NegativeTagFilterDesc(),
                "filledsoul" => new FilledSoulFilterDesc(),
                _ => throw new ArgumentException($"Unknown filter: {filterName}"),
            };
        }

        private MotelySearchSettings<T> ApplyScoring<T>(MotelySearchSettings<T> settings)
            where T : struct, IMotelySeedFilter
        {
            if (string.IsNullOrEmpty(_scoreConfig))
                return settings;

            // Load the JSON config for scoring
            var config = LoadScoringConfig(_scoreConfig);

            // Print CSV header
            PrintResultsHeader(config);

            // Track printed seeds to avoid duplicate console output
            var printedSeeds = new HashSet<string>();
            var printLock = new object();

            // Create scoring provider with callbacks
            Action<MotelySeedScoreTally> onResultFound = (score) =>
            {
                // Deduplicate console output - same seed can be found in multiple batches/threads
                lock (printLock)
                {
                    if (printedSeeds.Contains(score.Seed))
                        return; // Already printed this seed

                    printedSeeds.Add(score.Seed);
                }

                // Use original tally column format (CSV-style with colored numbers)
                Console.WriteLine(
                    TallyColorizer.FormatResultLine(score.Seed, score.Score, score.TallyValuesSpan)
                );

                if (ResultsDatabase != null)
                {
                    ResultsDatabase.InsertRow(score.Seed, score.Score, score.TallyColumns, score.ColumnValues);
                }
            };

            // Use cutoff from params if provided
            int cutoff = _params.Cutoff;
            bool autoCutoff = _params.AutoCutoff;

            var scoreDesc = new MotelyJsonSeedScoreDesc(
                config,
                cutoff,
                autoCutoff ? ScoreCutoffMode.AutoSmart : ScoreCutoffMode.Manual,
                onResultFound
            );

            return settings.WithSeedScoreProvider(scoreDesc).WithCsvOutput(true);
        }

        private MotelyJsonConfig LoadScoringConfig(string configPath)
        {
            // If rooted path, load based on extension
            if (Path.IsPathRooted(configPath) && File.Exists(configPath))
            {
                if (configPath.EndsWith(".jaml", StringComparison.OrdinalIgnoreCase))
                {
                    if (
                        !JamlConfigLoader.TryLoadFromJaml(
                            configPath,
                            out var jamlConfig,
                            out var error
                        )
                    )
                        throw new InvalidOperationException($"JAML loading failed: {error}");
                    return jamlConfig!;
                }
                if (!MotelyJsonConfig.TryLoadFromJsonFile(configPath, out var jsonConfig))
                    throw new InvalidOperationException($"JSON loading failed: {configPath}");
                return jsonConfig;
            }

            // Try .jaml first (prefer JAML over JSON) - case-insensitive search
            string jamlFileName = configPath.EndsWith(".jaml", StringComparison.OrdinalIgnoreCase)
                ? configPath
                : configPath + ".jaml";
            string jamlDir = "JamlFilters";

            // Case-insensitive file search
            if (Directory.Exists(jamlDir))
            {
                var matchingJamlFiles = Directory
                    .GetFiles(jamlDir, "*.jaml", SearchOption.TopDirectoryOnly)
                    .Where(f =>
                        Path.GetFileName(f).Equals(jamlFileName, StringComparison.OrdinalIgnoreCase)
                    )
                    .ToList();

                if (matchingJamlFiles.Count > 0)
                {
                    string jamlPath = matchingJamlFiles[0];
                    if (
                        !JamlConfigLoader.TryLoadFromJaml(
                            jamlPath,
                            out var jamlConfig,
                            out var error
                        )
                    )
                        throw new InvalidOperationException($"JAML loading failed: {error}");
                    return jamlConfig!;
                }
            }

            // Fall back to .json - case-insensitive search
            string jsonFileName = configPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                ? configPath
                : configPath + ".json";
            string jsonDir = "JsonFilters";

            if (Directory.Exists(jsonDir))
            {
                var matchingJsonFiles = Directory
                    .GetFiles(jsonDir, "*.json", SearchOption.TopDirectoryOnly)
                    .Where(f =>
                        Path.GetFileName(f).Equals(jsonFileName, StringComparison.OrdinalIgnoreCase)
                    )
                    .ToList();

                if (matchingJsonFiles.Count > 0)
                {
                    string jsonPath = matchingJsonFiles[0];
                    if (!MotelyJsonConfig.TryLoadFromJsonFile(jsonPath, out var jsonConfig))
                        throw new InvalidOperationException($"JSON loading failed: {jsonPath}");
                    return jsonConfig;
                }
            }

            throw new FileNotFoundException(
                $"Scoring config not found in {jamlDir} or {jsonDir} (searched for: {jamlFileName} or {jsonFileName})"
            );
        }

        private void PrintResultsHeader(MotelyJsonConfig config)
        {
            Console.WriteLine($"# Deck: {config.Deck}, Stake: {config.Stake}");

            // ONE SOURCE OF TRUTH: Use GetColumnNames()
            var columnNames = config.GetColumnNames();
            var quotedColumns = columnNames.Select(name => $"\"{name}\"");
            Console.WriteLine(string.Join(",", quotedColumns));
        }

        private IEnumerable<string>? LoadSeeds()
        {
            if (!string.IsNullOrEmpty(_params.SpecificSeed))
            {
                _searchSeeds = new List<string> { _params.SpecificSeed };
                return _searchSeeds;
            }

            // Check for keyword-generated seeds (from --keyword)
            if (_params.SeedList != null)
            {
                _searchSeeds = _params.SeedList;
                return _searchSeeds;
            }

            if (!string.IsNullOrEmpty(_params.SeedSources))
            {
                var seedSourcePath = $"SeedSources/{_params.SeedSources}.txt";
                if (!File.Exists(seedSourcePath))
                {
                    throw new FileNotFoundException(
                        $"Seed source file not found: {seedSourcePath}"
                    );
                }
                _searchSeeds = File.ReadLines(seedSourcePath)
                    .Where(line => !string.IsNullOrWhiteSpace(line));
                return _searchSeeds;
            }

            return null;
        }

        private void PrintSummary(IMotelySearch search, TimeSpan duration)
        {
            Console.WriteLine(
                _cancelled ? "\n✅ Search stopped gracefully" : "\n✅ Search completed"
            );

            // Calculate actual seeds searched - always use TotalSeedsSearched for accuracy
            ulong totalSeedsSearched = (ulong)search.TotalSeedsSearched;

            // Calculate the actual last batch processed
            var lastBatch = search.CompletedBatchCount;

            Console.WriteLine($"   Batches completed: {search.CompletedBatchCount}");
            Console.WriteLine($"   Last batch: {lastBatch}");
            Console.WriteLine($"   Seeds searched: {totalSeedsSearched:N0}");
            Console.WriteLine($"   Seeds passed filter and cutoff: {search.MatchingSeeds}");
            // Note: FilteredSeeds is deprecated and always returns 0
            // MatchingSeeds represents seeds that passed all filters AND cutoff

            if (duration.TotalMilliseconds >= 1)
            {
                var speed = duration.TotalMilliseconds > 0 
                    ? (double)totalSeedsSearched / duration.TotalMilliseconds 
                    : 0;
                Console.WriteLine($"   Duration: {duration:hh\\:mm\\:ss\\.fff}");
                Console.WriteLine($"   Speed: {speed:F2} seeds/millisecond");
            }
        }
    }
}
