using Motely;
using Motely.DB;
using Motely.Filters;
using Motely.Reporting;
using Motely.Utils;
using System.Text;

namespace Motely.Executors
{
    /// <summary>
    /// Executes JSON-based filter searches with specialized vectorized filters
    /// </summary>
    public sealed class JsonSearchExecutor : IDisposable
    {
        /// <summary>
        /// Enum for seed source type - used to determine which search mode to use
        /// </summary>
        private enum SeedSourceType
        {
            /// <summary>Specific seed lookup or in-memory IEnumerable list</summary>
            SeedList,
            /// <summary>DuckDB file (streamed or loaded into memory)</summary>
            DuckDatabase,
            /// <summary>No seed source provided - use sequential search</summary>
            Sequential,
        }

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
            _format = Path.GetExtension(configPath).EndsWith(".jaml", StringComparison.OrdinalIgnoreCase) ? "jaml" : "json";
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

        public int Execute(bool awaitCompletion = true, CancellationToken cancellationToken = default)
        {
            var effectiveToken = cancellationToken != default ? cancellationToken : _params.CancellationToken ?? default;
            
            DebugLogger.IsEnabled = _params.EnableDebug;
            FancyConsole.IsEnabled = !_params.NoFancy;
            // Gate colored output based on --nofancy
            TallyColorizer.ColorEnabled = !_params.NoFancy;

            SeedSourceResult seedSource = LoadSeeds();

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
                IMotelySearch search = CreateSearch(config, seedSource);
                if (search == null)
                {
                    return 1;
                }
                
                PrintResultsHeader(config);

                // Setup cancellation handler ONLY when NOT in TUI mode
                // In TUI mode, the UI handles Ctrl+C via KeyDown event and calls Cancel() directly
                ConsoleCancelEventHandler? cancelHandler = null;
                if (_customCallback == null)
                {
                    try
                    {
                        cancelHandler = (sender, e) =>
                        {
                            e.Cancel = true;
                            _cancelled = true;
                            if (!_params.Quiet)
                            {
                                Console.WriteLine("\n🛑 Stopping search...");
                            }
                            search.Cancel();
                        };
                        Console.CancelKeyPress += cancelHandler;
                    }
                    catch (PlatformNotSupportedException)
                    {
                        // Console.CancelKeyPress not available on this platform (e.g., WASM)
                        cancelHandler = null;
                    }
                }

                search.Start(effectiveToken);

                if (awaitCompletion)
                {
                    try
                    {
                        // Wait for completion - will exit early if cancellation token is signaled
                        // Wait for completion - check for keys to support manual progress (P) and quit (ESC ESC)
                        DateTime? lastEscTime = null;
                        while (!_cancelled && (search.Status == MotelySearchStatus.Running || search.Status == MotelySearchStatus.Paused))
                        {
                            if (_params.CancellationToken?.IsCancellationRequested == true)
                                break;

                            // Support P for progress, double-ESC to quit
                            try
                            {
                                // Check for key input (only works when stdin isn't redirected)
                                if (!Console.IsInputRedirected && Console.KeyAvailable)
                                {
                                    var key = Console.ReadKey(true);
                                    if (key.Key == ConsoleKey.P)
                                    {
                                        Console.WriteLine("📊 Progress:");
                                        search.ForceProgressReport();
                                    }
                                    else if (key.Key == ConsoleKey.Escape)
                                    {
                                        var now = DateTime.UtcNow;
                                        if (lastEscTime.HasValue && (now - lastEscTime.Value).TotalMilliseconds < 1000)
                                        {
                                            // Double-tap ESC within 1 second = quit
                                            Console.WriteLine();
                                            Console.WriteLine("🛑 ESC ESC - Stopping search...");
                                            search.Cancel();
                                            _cancelled = true;
                                            break;
                                        }
                                        else
                                        {
                                            // First ESC - show hint
                                            lastEscTime = now;
                                            Console.WriteLine("💡 Double-tap ESC to quit (or Ctrl+C)");
                                        }
                                    }
                                }
                            }
                            catch (PlatformNotSupportedException)
                            {
                                // Console key input not available on this platform
                            }

                            // Use a small sleep to avoid pegged CPU on main thread
                            Thread.Sleep(100);
                        }

                        // Always print final summary, even in quiet mode
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
                }
                else
                {
                    // Store the search for later access/cancellation
                    _runningSearch = search;
                }

                // Cleanup cancel handler if registered
                if (cancelHandler != null)
                {
                    try { Console.CancelKeyPress -= cancelHandler; }
                    catch (PlatformNotSupportedException) { }
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
        /// Async version of Execute that doesn't block the calling thread.
        /// Uses WaitForCompletionAsync instead of polling with Thread.Sleep.
        /// </summary>
        public async Task<int> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            var effectiveToken = cancellationToken != default ? cancellationToken : _params.CancellationToken ?? default;
            
            DebugLogger.IsEnabled = _params.EnableDebug;
            FancyConsole.IsEnabled = !_params.NoFancy;
            // Gate colored output based on --nofancy
            TallyColorizer.ColorEnabled = !_params.NoFancy;

            SeedSourceResult seedSource = LoadSeeds();

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
                IMotelySearch search = CreateSearch(config, seedSource);
                if (search == null)
                {
                    return 1;
                }
                
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
                
                // Load seeds from the configured source
                SeedSourceResult source = LoadSeeds();
                
                _runningSearch = CreateSearch(_loadedConfig, source);
                
                // Return the search handle - caller will call Start(cancellationToken)
                return _runningSearch;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"❌ Search initialization failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Load seeds from the configured source and determine which search mode to use.
        /// 
        /// Priority:
        /// 1. SpecificSeed → SeedList mode (search for one seed)
        /// 2. SeedList → SeedList mode (use provided IEnumerable directly)
        /// 3. SeedSources → DuckDatabase mode (load from .db/.csv/.txt file)
        /// 4. (none) → Sequential mode (full seed space scan)
        /// </summary>
        private SeedSourceResult LoadSeeds()
        {
            if (!string.IsNullOrEmpty(_params.SpecificSeed))
            {
                if (!_params.Quiet)
                {
                    Console.WriteLine($"🔍 Searching for specific seed: {_params.SpecificSeed}");
                }
                // Create a single-seed list from SpecificSeed
                _params.SeedList = new[] { _params.SpecificSeed };
                return new SeedSourceResult(SeedSourceType.SeedList);
            }

            // Direct seed list takes priority over wordlist file
            if (_params.SeedList != null)
            {
                if (!_params.Quiet)
                {
                    // Don't enumerate IEnumerable - just note we have a list
                    Console.WriteLine($"🔍 Searching seeds from provided list");
                }
                return new SeedSourceResult(SeedSourceType.SeedList);
            }

            // Unified SeedSources parameter - handles both relative and absolute paths
            if (!string.IsNullOrEmpty(_params.SeedSources))
            {
                string? dbPath = LoadSeedSources(_params.SeedSources);
                if (dbPath != null)
                {
                    return new SeedSourceResult(SeedSourceType.DuckDatabase, dbPath);
                }
                // If LoadSeedSources returns null, it indicates the operation was cancelled by the user (e.g. denied overwrite)
                throw new OperationCanceledException("Seed source loading cancelled by user.");
            }

            // No seed source provided - use sequential search
            return new SeedSourceResult(SeedSourceType.Sequential);
        }

        /// <summary>
        /// Load seed sources from file (.db/.csv/.txt).
        /// Returns the file path directly - DataLakeSeedProvider handles reading .txt/.csv files natively.
        /// 
        /// For IEnumerable sources (--keyword, --seedlist), use SeedList directly instead - faster!
        /// DuckDB conversion is only done for caching/reuse, but .txt/.csv can be read directly.
        /// </summary>
        private string? LoadSeedSources(string seedSource)
        {
            // If the user gave an absolute path or it already exists exactly where it is, respect it!
            if (File.Exists(seedSource))
            {
                string ext = Path.GetExtension(seedSource).ToLowerInvariant();
                
                // .txt and .csv files can be read directly by DataLakeSeedProvider - no conversion needed!
                if (ext == ".txt" || ext == ".csv")
                {
                    return seedSource; // Pass directly - DataLakeSeedProvider handles it
                }
                
                if (ext == ".db")
                {
                    return seedSource;
                }
                
                throw new NotSupportedException($"Unsupported seed source extension: {ext}");
            }

            // Fallback for relative, extensionless names (legacy behavior/convenience)
            // Use unified "seeds" folder (combines SearchResults and SeedSources)
            string storageDirectory = "seeds";
            // Directory will be created by MotelySearchDatabase or file operations as needed

            // Special case: if it has an extension, try looking in SeedSources with the exact name
            if (Path.HasExtension(seedSource))
            {
                string directPathInSeedSources = Path.Combine(storageDirectory, seedSource);
                if (File.Exists(directPathInSeedSources))
                {
                    string ext = Path.GetExtension(directPathInSeedSources).ToLowerInvariant();
                    
                    // .txt and .csv files can be read directly - no conversion needed!
                    if (ext == ".txt" || ext == ".csv" || ext == ".db")
                    {
                        return directPathInSeedSources;
                    }
                    
                    throw new NotSupportedException($"Unsupported seed source extension: {ext}");
                }
            }

            // Priority: .db > .csv > .txt (for extensionless names)
            // But .txt/.csv can be read directly without conversion!
            string dbPathInternal = Path.Combine(storageDirectory, seedSource + ".db");
            string csvPathInternal = Path.Combine(storageDirectory, seedSource + ".csv");
            string txtPathInternal = Path.Combine(storageDirectory, seedSource + ".txt");

            if (File.Exists(dbPathInternal))
            {
                return dbPathInternal; 
            }

            if (File.Exists(csvPathInternal))
            {
                return csvPathInternal; // Read directly - no conversion!
            }

            if (File.Exists(txtPathInternal))
            {
                return txtPathInternal; // Read directly - no conversion!
            }

            throw new FileNotFoundException(
                $"Seed source file not found. Checked exact path '{seedSource}' and variants in '{storageDirectory}'"
            );
        }

        /// <summary>
        /// Convert CSV to DuckDB and return dbPath. ONE TRUE WAY!
        /// </summary>
        private string? ConvertCsvToDuckDB(string csvPath, string dbPath)
        {
            // Check if DB already exists - use it directly
            if (File.Exists(dbPath))
            {
                if (!_params.Quiet)
                {
                    Console.WriteLine($"✅ Using existing DuckDB: {dbPath}");
                }
                return dbPath;
            }

            // SAFETY CHECK: Warn before overwriting existing absolute path DuckDB files
            if (File.Exists(dbPath))
            {
                var existingDbInfo = new FileInfo(dbPath);
                var existingSizeMB = existingDbInfo.Length / (1024.0 * 1024.0);
                
                Console.WriteLine();
                Console.WriteLine($"⚠️⚠️⚠️ There is currently a DUCKDB file called {Path.GetFileName(dbPath)} [{existingSizeMB:F0}MB] that would be deleted.");
                Console.WriteLine($"   Are you sure you want to [Y]eet the seed sources database {Path.GetFileName(dbPath)}? [y/N]");
                
                if (_params.ForceOverwrite)
                {
                    Console.WriteLine("   ✅ Force overwrite enabled - proceeding with conversion...");
                }
                else
                {
                    Console.Write("   ");
                    var response = Console.ReadLine()?.Trim().ToLowerInvariant();
                    
                    if (response != "y" && response != "yes")
                    {
                        Console.WriteLine("   ❌ Conversion cancelled by user.");
                        return null;
                    }
                    
                    Console.WriteLine("   ✅ User confirmed - proceeding with conversion...");
                }
                
                // Delete the existing database file
                try
                {
                    File.Delete(dbPath);
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to delete existing database {dbPath}: {ex.Message}", ex);
                }
            }

            var fileInfo = new FileInfo(csvPath);
            var sizeMB = fileInfo.Length / (1024.0 * 1024.0);
            if (!_params.Quiet)
            {
                Console.WriteLine($"🔄 Converting CSV to DuckDB: {csvPath} -> {dbPath}");
                Console.WriteLine(
                    $"   File size: {sizeMB:F1} MB - this may take a minute for large files..."
                );
            }

            try
            {
                using var conn = DuckDBConnectionFactory.CreateConnection(dbPath);
                
                // Create the seeds table first
                using var createCmd = conn.CreateCommand();
                createCmd.CommandText = "CREATE TABLE seeds (seed VARCHAR PRIMARY KEY)";
                createCmd.ExecuteNonQuery();
                
                // Use DuckDB.NET Appender for maximum performance bulk loading
                using var appender = conn.CreateAppender("seeds");
                
                // Read and parse CSV file - prepare all seeds first
                var lines = File.ReadAllLines(csvPath);
                var seeds = new List<string>();
                
                foreach (var line in lines)
                {
                    // Handle comma-separated values
                    var parts = line.Split(',');
                    foreach (var part in parts)
                    {
                        var trimmedPart = part.Trim();
                        if (!string.IsNullOrEmpty(trimmedPart))
                        {
                            seeds.Add(trimmedPart);
                        }
                    }
                }
                
                // Bulk append all seeds at once for maximum performance
                foreach (var seed in seeds)
                {
                    var row = appender.CreateRow();
                    row.AppendValue(seed);
                    row.EndRow();
                }
                
                // Dispose() automatically calls Close() and flushes all data to database
                appender.Dispose();
                conn.Close();
                
                if (!_params.Quiet)
                {
                    var dbInfo = new FileInfo(dbPath);
                    var dbSizeMB = dbInfo.Length / (1024.0 * 1024.0);
                    Console.WriteLine(
                        $"✅ Converted CSV to DuckDB: {dbPath} ({dbSizeMB:F1} MB)"
                    );
                    Console.WriteLine(
                        $"   Imported seeds from CSV file"
                    );
                }

                // Keep source file - don't delete it! User may need it later.
                return dbPath;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to convert CSV to DuckDB: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Convert text file to DuckDB and return dbPath. ONE TRUE WAY!
        /// </summary>
        private string? ConvertTextToDuckDB(string textPath, string dbPath)
        {
            if (File.Exists(dbPath))
            {
                var existingDbInfo = new FileInfo(dbPath);
                var existingSizeMB = existingDbInfo.Length / (1024.0 * 1024.0);
                
                Console.WriteLine();
                Console.WriteLine($"⚠️⚠️⚠️ There is currently a DUCKDB file called {Path.GetFileName(dbPath)} [{existingSizeMB:F0}MB] that would be deleted.");
                Console.WriteLine($"   Are you sure you want to [Y]eet the seed sources database {Path.GetFileName(dbPath)}? [y/N]");
                
                if (_params.ForceOverwrite)
                {
                    Console.WriteLine("   ✅ Force overwrite enabled - proceeding with conversion...");
                }
                else
                {
                    Console.Write("   ");
                    var response = Console.ReadLine()?.Trim().ToLowerInvariant();
                    
                    if (response != "y" && response != "yes")
                    {
                        Console.WriteLine("   ❌ Conversion cancelled by user.");
                        return null;
                    }
                    
                    Console.WriteLine("   ✅ User confirmed - proceeding with conversion...");
                }
                
                // Delete the existing database file
                try
                {
                    File.Delete(dbPath);
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to delete existing database {dbPath}: {ex.Message}", ex);
                }
            }

            
            var fileInfo = new FileInfo(textPath);
            var sizeMB = fileInfo.Length / (1024.0 * 1024.0);
            Console.WriteLine($"🔄 Converting text file to DuckDB: {textPath} -> {dbPath}");
            Console.WriteLine(
                $"   File size: {sizeMB:F1} MB - this may take a minute for large files..."
            );

            try
            {
                using var conn = DuckDBConnectionFactory.CreateConnection(dbPath);
                using var cmd = conn.CreateCommand();
                
                // Create table
                cmd.CommandText = "CREATE TABLE seeds (seed VARCHAR)";
                cmd.ExecuteNonQuery();
                
                // Use Appender to stream data - no loading entire file into memory
                using var appender = conn.CreateAppender("seeds");
                
                int totalLines = 0;
                var seenSeeds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                
                // Stream lines from file
                foreach (var line in File.ReadLines(textPath))
                {
                    var trimmed = line.Trim();
                    if (!string.IsNullOrEmpty(trimmed) && seenSeeds.Add(trimmed))
                    {
                        var row = appender.CreateRow();
                        row.AppendValue(trimmed);
                        row.EndRow();
                        totalLines++;
                    }
                }
                
                appender.Close();
                conn.Close();
                
                if (!_params.Quiet)
                {
                    var dbInfo = new FileInfo(dbPath);
                    var dbSizeMB = dbInfo.Length / (1024.0 * 1024.0);
                    Console.WriteLine(
                        $"✅ Converted text file to DuckDB: {dbPath} ({dbSizeMB:F1} MB)"
                    );
                    Console.WriteLine(
                        $"   Imported {totalLines:N0} unique seeds"
                    );
                }

                return dbPath;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to convert text file to DuckDB: {ex.Message}", ex);
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
        /// Create a search with the appropriate seed source (Random, SeedList, DuckDB, or Sequential)
        /// </summary>
        /// <summary>
        /// Create a search with the appropriate seed source (Random, SeedList, DuckDB, or Sequential)
        /// </summary>
        private IMotelySearch CreateSearch(MotelyJsonConfig config, SeedSourceResult source)
        {
            // 1. Create optimized filter pipeline via MotelyRunConfig factory
            // This ensures AVX/SIMD optimizations are applied!
            var runConfig = MotelyRunConfig.Factory(config);
            
            if (runConfig.FilterPipeline == null)
            {
                // Fallback for empty/trivial filters (should rarely happen with Factory)
                 throw new InvalidOperationException("Failed to create filter pipeline from configuration.");
            }

            // 2. Create Search Settings from the pipeline
            // SpecializedFilterFactory now returns IMotelySearchSettings, supporting fluent config
            var searchSettings = SpecializedFilterFactory.CreateSearchSettings(runConfig.FilterPipeline)
                .WithThreadCount(_params.Threads)
                .WithBatchCharacterCount(_params.BatchSize)
                .WithStartBatchIndex((long)_params.StartBatch)
                .WithDeck(runConfig.Deck)
                .WithStake(runConfig.Stake)
                .WithCsvOutput(_params.Quiet)
                .WithQuietMode(_params.Quiet)
                .WithProgressCallback(progress =>
                {
                    // DO NOT call _customCallback here - that's for RESULTS only, not progress!
                    // Progress updates should NOT appear as CSV rows
                    
                    // Forward to the main progress callback if set (for API/UI stats)
                    _params.ProgressCallback?.Invoke(progress);
                });

            // Handle EndBatch (0 means infinite/max)
            if (_params.EndBatch > 0)
            {
                 searchSettings.WithEndBatchIndex((long)_params.EndBatch);
            }

            // 4. Attach Score Provider with PRINTING callback
            // override the one from RunConfig because we need to hook up the UI callback here
            if (config.Should != null && config.Should.Count > 0)
            {
                // Define the callback that prints the result to the console
                Action<MotelySeedScoreTally> onResult = (tally) => 
                {
                    PrintResultRow(tally, config);
                    _customCallback?.Invoke(tally);
                };

                // Create a new descriptor with the callback
                var scoreDesc = new MotelyJsonSeedScoreDesc(
                    config, 
                    _params.Cutoff, 
                    _params.CutoffMode, 
                    onResult
                );
                
                searchSettings.WithSeedScoreProvider(scoreDesc);
            }

            // 3. Configure Seed Source & Start Search
            // Priority: Random -> Palindrome -> SeedList -> DuckDB -> Sequential
            
            var token = _params.CancellationToken ?? default;
            
            if (_params.RandomSeeds.HasValue)
            {
                 if (!_params.Quiet) Console.WriteLine($"🎲 Random Search: {_params.RandomSeeds} seeds");
                 return searchSettings.WithRandomSearch(_params.RandomSeeds.Value).Start(token);
            }
            
            if (_params.PalindromeSeeds)
            {
                 if (!_params.Quiet) Console.WriteLine($"🔄 Palindrome Search: generating palindrome seeds lazily");
                 return searchSettings.WithPalindromeSearch().Start(token);
            }
            
            if (source.SourceType == SeedSourceType.SeedList && _params.SeedList != null)
            {
                 // Don't materialize IEnumerable - it's lazy! Seeds come from generator/enumerator in their natural order
                 // Use known count for keyword generation if available (for progress reporting)
                 if (!_params.Quiet) Console.WriteLine($"📋 List Search: seeds from provided list (lazy enumeration)");
                 return searchSettings.WithListSearch(_params.SeedList, seedCount: _params.KeywordSeedCount ?? -1).Start(token);
            }

            if (source.SourceType == SeedSourceType.DuckDatabase && !string.IsNullOrEmpty(source.DbPath))
            {
                 if (!_params.Quiet) Console.WriteLine($"🦆 DuckDB Search: {source.DbPath}");
                 return searchSettings.WithProviderSearch(new global::Motely.DB.DataLakeSeedProvider(source.DbPath)).Start(token);
            }

            // Default: Sequential Search
            if (!_params.Quiet) Console.WriteLine($"🔄 Sequential Search: 35^{8-_params.BatchSize} batches");
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
                double precisePercent = maxBatches > 0 ? (double)lastBatchIndex * 100.0 / (double)maxBatches : 0.0;
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
            double speedMs = search.ElapsedTime.TotalMilliseconds > 0 
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
            // Cleanup DuckDB Appender for Sequential Search results
            if (_resultsAppender != null)
            {
                _resultsAppender.Dispose();
                _resultsAppender = null;
                
            }
            
            // Cleanup MotelySearchDatabase ResultsDatabase
            if (ResultsDatabase != null)
            {
                ResultsDatabase.Checkpoint();
                ResultsDatabase.Dispose();
                ResultsDatabase = null;
                
            }
            
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
        /// Path to export results as CSV after search completes.
        /// Export is done via DuckDB COPY command (proper formatting).
        /// Requires AutoSave or OutputDbPath to have results in DuckDB.
        /// </summary>
        public string? CsvExportPath { get; set; }
    }
}
