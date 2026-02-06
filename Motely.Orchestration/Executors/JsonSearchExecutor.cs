using System.Text;
using Motely;
using Motely.Filters;
using Motely.Reporting;
using Motely.Repository;
using Motely.Utils;

namespace Motely.Executors
{
    /// <summary>
    /// Executes JSON-based filter searches with specialized vectorized filters
    /// </summary>
    public sealed class JsonSearchExecutor : IDisposable
    {
        private readonly string? _configPath;
        private readonly MotelyJsonConfig? _config;
        private readonly JsonSearchParams _params;
        private readonly string? _format;
        private readonly Action<MotelySeedScoreTally>? _customCallback;
        private bool _cancelled = false;
        private IMotelySearch? _runningSearch;
        private MotelyJsonConfig? _loadedConfig; // Config loaded by ExecuteAsSearch for header printing

        /// <summary>Optional result storage.</summary>
        public IResultStorage? ResultStorage { get; set; }

        /// <summary>
        /// Print CSV header row. Call AFTER your startup output but BEFORE Start().
        /// </summary>
        public void EmitResultsHeader()
        {
            if (_loadedConfig != null)
                PrintResultsHeader(_loadedConfig);
        }

        public JsonSearchExecutor(
            string configPath,
            JsonSearchParams parameters,
            Action<MotelySeedScoreTally>? customCallback = null
        )
        {
            _configPath = configPath;
            _config = null;
            _params = parameters;
            _format = Path.GetExtension(configPath)
                .EndsWith(".jaml", StringComparison.OrdinalIgnoreCase)
                ? "jaml"
                : "json";
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
            _format = null;
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
            MotelyJsonConfig.MotelyJsonFilterClause clause,
            string sectionName, // "MUST", "MUSTNOT", "SHOULD"
            int index
        )
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
                    : (
                        clause.Values != null && clause.Values.Length > 0
                            ? string.Join(", ", clause.Values)
                            : "<none>"
                    );
                throw new ArgumentException(
                    $"Config error in {sectionName}[{index}] — type: '{typeText}', value(s): '{valueText}'. {ex.Message}\nHint: Each clause needs a non-empty 'type' (e.g., 'Joker', 'TarotCard', 'PlayingCard'). If using multiple values, use 'values': [ ... ] not 'value'."
                );
            }
        }

        /// <summary>
        /// Execute the search synchronously. Blocks the calling thread until completion.
        /// Uses AwaitCompletion() which blocks on threads (not async), making it safe for console apps.
        /// For UI frameworks (Avalonia, WPF, etc.), use ExecuteAsync() instead to avoid freezing the UI thread.
        /// </summary>
        public int Execute(CancellationToken cancellationToken = default)
        {
            var effectiveToken =
                cancellationToken != default
                    ? cancellationToken
                    : _params.CancellationToken ?? default;

            DebugLogger.IsEnabled = _params.EnableDebug;
            FancyConsole.IsEnabled = !_params.NoFancy;
            TallyColorizer.ColorEnabled = !_params.NoFancy;

            if (!_params.Quiet)
            {
                Console.WriteLine($"🔍 MotelyJAML Search Starting");
                Console.WriteLine($"   Config: {_configPath}");
                Console.WriteLine($"   Threads: {_params.Threads}");

                if (_params.RandomSeeds.HasValue)
                    Console.WriteLine($"   Mode: Random ({_params.RandomSeeds} seeds)");
                else
                {
                    Console.WriteLine($"   Batch Size: {_params.BatchSize} chars");
                    string endDisplay = _params.EndBatch == 0 ? "∞" : _params.EndBatch.ToString();
                    Console.WriteLine($"   Range: {_params.StartBatch} to {endDisplay}");
                }
                if (_params.EnableDebug)
                    Console.WriteLine($"   Debug: Enabled");

                Console.WriteLine();
            }

            try
            {
                MotelyJsonConfig config = LoadConfig();
                IMotelySearch search = CreateSearch(config);
                if (search == null)
                    return 1;

                PrintResultsHeader(config);

                search.Start(effectiveToken);

                try
                {
                    // Use AwaitCompletion() - blocks on threads (not async), safe for console apps
                    // Respects cancellation token internally via Thread.Join with timeout checks
                    search.AwaitCompletion();

                    // Always print final summary, even in quiet mode
                    PrintResultsSummary(search, _cancelled);
                }
                catch (OperationCanceledException)
                {
                    _cancelled = true;
                    PrintResultsSummary(search, _cancelled);
                }
                finally
                {
                    // Always dispose, but avoid double-dispose if cancelled and handler already disposed
                    if (!_cancelled)
                    {
                        search.Dispose();
                    }
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
        /// Execute the search asynchronously without blocking the calling thread.
        /// Uses WaitForCompletionAsync instead of polling with Thread.Sleep.
        /// Required for UI frameworks (Avalonia, WPF, etc.) to avoid freezing the UI thread.
        /// </summary>
        public async Task<int> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            var effectiveToken =
                cancellationToken != default
                    ? cancellationToken
                    : _params.CancellationToken ?? default;

            DebugLogger.IsEnabled = _params.EnableDebug;
            FancyConsole.IsEnabled = !_params.NoFancy;
            TallyColorizer.ColorEnabled = !_params.NoFancy;

            if (!_params.Quiet)
            {
                Console.WriteLine($"🔍 MotelyJAML Search Starting");
                Console.WriteLine($"   Config: {_configPath}");
                Console.WriteLine($"   Threads: {_params.Threads}");

                if (_params.RandomSeeds.HasValue)
                    Console.WriteLine($"   Mode: Random ({_params.RandomSeeds} seeds)");
                else
                {
                    Console.WriteLine($"   Batch Size: {_params.BatchSize} chars");
                    string endDisplay = _params.EndBatch == 0 ? "∞" : _params.EndBatch.ToString();
                    Console.WriteLine($"   Range: {_params.StartBatch} to {endDisplay}");
                }
                if (_params.EnableDebug)
                    Console.WriteLine($"   Debug: Enabled");

                Console.WriteLine();
            }

            try
            {
                MotelyJsonConfig config = LoadConfig();
                IMotelySearch search = CreateSearch(config);
                if (search == null)
                    return 1;

                PrintResultsHeader(config);

                search.Start(effectiveToken);

                try
                {
                    // Wait for completion using async API - doesn't block the UI thread
                    await search.WaitForCompletionAsync(effectiveToken).ConfigureAwait(false);

                    // Always print final summary, even in quiet mode
                    PrintResultsSummary(search, _cancelled);
                }
                catch (OperationCanceledException)
                {
                    _cancelled = true;
                    PrintResultsSummary(search, _cancelled);
                }
                finally
                {
                    // Always dispose, but avoid double-dispose if cancelled and handler already disposed
                    if (!_cancelled)
                    {
                        search.Dispose();
                    }
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
        /// Same as Execute() but returns the search handle for orchestration.
        /// Caller should call PrintResultsHeader() after their own startup output.
        /// </summary>
        public IMotelySearch ExecuteAsSearch()
        {
            try
            {
                _loadedConfig = LoadConfig();
                _runningSearch = CreateSearch(_loadedConfig);
                if (_params.EmitResultsHeader && _loadedConfig != null)
                {
                    PrintResultsHeader(_loadedConfig);
                }
                return _runningSearch;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"❌ Search initialization failed: {ex.Message}");
                throw;
            }
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
                configPath = Path.Combine(filterDir, _configPath + extension);
            }

            if (!File.Exists(configPath))
                throw new FileNotFoundException(
                    $"Could not find {_format!.ToUpper()} config file: {configPath}"
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

        /// <summary>
        /// Create a search with the appropriate seed source.
        /// Priority: Random -> Palindrome -> SpecificSeed/SeedList -> SeedSources -> Sequential
        /// </summary>
        private IMotelySearch CreateSearch(MotelyJsonConfig config)
        {
            var runConfig = MotelyRunConfig.Factory(config);

            if (runConfig.FilterPipeline == null)
                throw new InvalidOperationException(
                    "Failed to create filter pipeline from configuration."
                );

            var searchSettings = SpecializedFilterFactory
                .CreateSearchSettings(runConfig.FilterPipeline)
                .WithThreadCount(_params.Threads)
                .WithBatchCharacterCount(_params.BatchSize)
                .WithStartBatchIndex((long)_params.StartBatch)
                .WithDeck(runConfig.Deck)
                .WithStake(runConfig.Stake)
                .WithCsvOutput(_params.Quiet)
                .WithQuietMode(_params.Quiet)
                .WithProgressCallback(progress => _params.ProgressCallback?.Invoke(progress));

            if (_params.EndBatch > 0)
                searchSettings.WithEndBatchIndex((long)_params.EndBatch);

            if (config.Should != null && config.Should.Count > 0)
            {
                Action<MotelySeedScoreTally> onResult = (tally) =>
                {
                    // If custom callback is provided, use it (handles CSV formatting with quotes)
                    // Otherwise, use PrintResultRow (handles colored output)
                    if (_customCallback != null)
                    {
                        _customCallback.Invoke(tally);
                    }
                    else
                    {
                        PrintResultRow(tally, config);
                    }
                };

                var scoreDesc = new MotelyJsonSeedScoreDesc(
                    config,
                    _params.Cutoff,
                    _params.CutoffMode,
                    onResult
                );
                searchSettings.WithSeedScoreProvider(scoreDesc);
            }

            var token = _params.CancellationToken ?? default;

            // Random search
            if (_params.RandomSeeds.HasValue)
            {
                if (!_params.Quiet)
                    Console.WriteLine($"🎲 Random Search: {_params.RandomSeeds} seeds");
                return searchSettings.WithRandomSearch(_params.RandomSeeds.Value).Start(token);
            }

            // Palindrome search
            if (_params.PalindromeSeeds)
            {
                if (!_params.Quiet)
                    Console.WriteLine($"🔄 Palindrome Search");
                return searchSettings.WithPalindromeSearch().Start(token);
            }

            // Specific seed -> convert to list
            if (!string.IsNullOrEmpty(_params.SpecificSeed))
            {
                if (!_params.Quiet)
                    Console.WriteLine($"🔍 Specific seed: {_params.SpecificSeed}");
                _params.SeedList = new[] { _params.SpecificSeed };
            }

            // Seed list search
            if (_params.SeedList != null)
            {
                if (!_params.Quiet)
                    Console.WriteLine($"📋 List Search");
                return searchSettings
                    .WithListSearch(_params.SeedList, seedCount: _params.KeywordSeedCount ?? -1)
                    .Start(token);
            }

            // File-based seed source: resolved via repository (desktop: DuckDB/file, browser: host provides impl or throws)
            if (!string.IsNullOrEmpty(_params.SeedSources))
            {
                if (!_params.Quiet)
                    Console.WriteLine($"📁 File Search: {_params.SeedSources}");
                return searchSettings
                    .WithProviderSearch(RepositoryHost.Instance.GetSource(_params.SeedSources))
                    .Start(token);
            }

            // Default: Sequential
            if (!_params.Quiet)
                Console.WriteLine($"🔄 Sequential Search: 35^{8 - _params.BatchSize} batches");
            return searchSettings.WithSequentialSearch().Start(token);
        }

        private void PrintResultsHeader(MotelyJsonConfig config)
        {
            // Informational output goes to stderr (doesn't pollute CSV/piped output)
            if (!_params.Quiet)
            {
                Console.Error.WriteLine($"# Deck: {config.Deck}, Stake: {config.Stake}");
            }

            // ONE SOURCE OF TRUTH: Use GetColumnNames()
            var columnNames = config.GetColumnNames();
            var allColumns = new List<string> { "Seed", "Score" };
            allColumns.AddRange(columnNames.Skip(2)); // Skip "seed" and "score" since we already have them capitalized
            var headerLine = string.Join(",", allColumns.Select(name => $"\"{name}\""));

            // Write header to stdout (for console mode / piping)
            Console.WriteLine(headerLine);

            // Also write header to CSV file if specified (ONE SOURCE OF TRUTH)
            _params.CsvWriter?.WriteLine(headerLine);
        }

        private void PrintResultRow(MotelySeedScoreTally result, MotelyJsonConfig config)
        {
            var tallies = result.TallyColumns;
            var columnValues = result.ColumnValues;
            bool hasStringValues = false;

            if (columnValues != null)
            {
                for (int i = 0; i < columnValues.Count && i < tallies.Count; i++)
                {
                    var strVal = columnValues[i];
                    if (strVal != null && strVal != tallies[i].ToString())
                    {
                        hasStringValues = true;
                        break;
                    }
                }
            }

            string line;
            if (hasStringValues)
            {
                // Mixed string/int columns - use the column values version
                line = TallyColorizer.FormatResultLine(result.Seed, result.Score, columnValues!);
            }
            else
            {
                // Pure integer tallies - use the optimized span version with colors!
                line = TallyColorizer.FormatResultLine(result.Seed, result.Score, tallies);
            }

            FancyConsole.WriteLine(line);
        }

        /// <summary>
        /// Generate a meaningful column name for a clause, handling And/Or groupings
        /// </summary>
        private static string GetClauseHeaderName(MotelyJsonConfig.MotelyJsonFilterClause clause)
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
            MotelyJsonConfig.MotelyJsonFilterClause clause
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

            long lastBatchIndex = search.CompletedBatchCount;

            // Batches only apply to sequential (batch) search; for provider/list search, don't show batch progress
            if (search.IsSequentialBatchSearch)
            {
                long maxBatches = (long)Math.Pow(35, 8 - _params.BatchSize);
                double precisePercent =
                    maxBatches > 0 ? (double)lastBatchIndex * 100.0 / (double)maxBatches : 0.0;
                Console.WriteLine($"   Last batch: {lastBatchIndex:N0} ({precisePercent:F4}%)");
            }
            Console.WriteLine($"   Seeds passed filter and cutoff: {search.MatchingSeeds}");
            // Note: FilteredSeeds is deprecated and always returns 0
            // MatchingSeeds represents seeds that passed all filters AND cutoff

            Console.WriteLine($"   Duration: {search.ElapsedTime:hh\\:mm\\:ss\\.fff}");
            Console.WriteLine(
                search.IsSequentialBatchSearch
                    ? $"   Total seeds: {search.TotalSeedsSearched:N0} ({search.CompletedBatchCount} batches)"
                    : $"   Total seeds: {search.TotalSeedsSearched:N0}"
            );
            double speedMs =
                search.ElapsedTime.TotalMilliseconds > 0
                    ? (double)search.TotalSeedsSearched / search.ElapsedTime.TotalMilliseconds
                    : 0;
            double speedPerSecond = speedMs * 1000.0;
            string speedFormatted = FormatSpeed(speedPerSecond);
            Console.WriteLine($"   Speed: {speedFormatted}");

            // Only show "To continue" for sequential batch search when cancelled
            if (wasCancelled && search.IsSequentialBatchSearch)
            {
                long maxBatches = (long)Math.Pow(35, 8 - _params.BatchSize);
                Console.WriteLine($"   To continue from here, use: --start {lastBatchIndex}");
            }
        }

        /// <summary>
        /// Format speed as M/s (millions per second) for readability.
        /// Examples: 2950678 → "2.95 M/s", 123456 → "123K seeds/s", 1234 → "1.23K seeds/s"
        /// </summary>
        private static string FormatSpeed(double seedsPerSecond)
        {
            if (seedsPerSecond >= 1_000_000)
            {
                return $"{seedsPerSecond / 1_000_000:F2} M/s";
            }
            else if (seedsPerSecond >= 1_000)
            {
                return $"{seedsPerSecond / 1_000:F2}K seeds/s";
            }
            else
            {
                return $"{seedsPerSecond:F0} seeds/s";
            }
        }

        public void Dispose()
        {
            // Cleanup result storage
            if (ResultStorage is IDisposable disposableStorage)
            {
                disposableStorage.Dispose();
            }
            ResultStorage = null;

            _runningSearch?.Dispose();
            _runningSearch = null;
        }
    }

    public record JsonSearchParams
    {
        public string Config { get; set; } = "standard";
        public int Threads { get; set; } = Environment.ProcessorCount;
        public int BatchSize { get; set; } = 4;
        public ulong StartBatch { get; set; }
        public ulong EndBatch { get; set; }
        public int Cutoff { get; set; }
        public bool AutoCutoff { get; set; }
        public ScoreCutoffMode CutoffMode { get; set; } = ScoreCutoffMode.None;
        public bool EnableDebug { get; set; }
        public bool NoFancy { get; set; }
        public bool Quiet { get; set; }
        public bool ForceOverwrite { get; set; } = false;
        public bool AutoSave { get; set; } = false; // If true, auto-generate DB path from config when OutputDbPath is null
        public Func<string, string, bool>? SchemaMismatchPrompt { get; set; }
        public string? SpecificSeed { get; set; }
        public string? Deck { get; set; }
        public string? Stake { get; set; }

        /// <summary>
        /// Unified seed source: can be .txt, .csv, or .db file
        /// Supports relative paths (SeedSources/file.db) or absolute paths (C:\path\file.db)
        /// Automatically detects type and handles accordingly
        /// </summary>
        public string? OutputDbPath { get; set; }
        public string? SeedSources { get; set; }
        public IEnumerable<string>? SeedList { get; set; }
        public int? RandomSeeds { get; set; }
        public bool PalindromeSeeds { get; set; }
        public int? KeywordSeedCount { get; set; } // Seed count for keyword generation (for progress)

        /// <summary>
        /// In-memory (WASM/browser) only: max number of results to keep in the top-K heap.
        /// Ignored when using database storage. Default 1000.
        /// </summary>
        public int MaxResults { get; set; } = 1000;

        /// <summary>
        /// Progress callback: receives MotelyProgress object with all progress data
        /// </summary>
        public Action<MotelyProgress>? ProgressCallback { get; set; }

        /// <summary>
        /// Result callback: each matching seed (WASM/browser uses this to push to JS via MotelyWasmOnResult).
        /// </summary>
        public Action<MotelySeedScoreTally>? ResultCallback { get; set; }

        /// <summary>
        /// Cancellation token to stop the search when CTRL+C is pressed
        /// </summary>
        public CancellationToken? CancellationToken { get; set; }

        /// <summary>
        /// Optional CSV file writer. When set, header and results are written here.
        /// ONE SOURCE OF TRUTH: The executor writes the header format.
        /// </summary>
        public TextWriter? CsvWriter { get; set; }

        /// <summary>
        /// Emit the CSV header before search starts (for CLI piping).
        /// </summary>
        public bool EmitResultsHeader { get; set; } = false;

        /// <summary>
        /// Path to export results as CSV after search completes.
        /// Export is done via DuckDB COPY command (proper formatting).
        /// Requires AutoSave or OutputDbPath to have results in DuckDB.
        /// </summary>
        public string? CsvExportPath { get; set; }
    }
}
