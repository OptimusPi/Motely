using DuckDB.NET.Data;
using McMaster.Extensions.CommandLineUtils;
using Motely.Analysis;
using Motely.API;
using Motely.DuckDB;
using Motely.Executors;
using Motely.Filters;
using Motely.GPU;
using System.Text;

namespace Motely
{
    partial class Program
    {
        private static readonly CancellationTokenSource _cts = new();

        static async Task<int> Main(string[] args)
        {
            // Wire up Ctrl+C to CancellationTokenSource for proper async/await cancellation
            Console.CancelKeyPress += async (sender, e) =>
            {
                e.Cancel = true; // Suppress default termination (allow graceful shutdown)
                await _cts.CancelAsync(); // Signal cancellation token to all subscribers asynchronously
            };

            var app = new CommandLineApplication
            {
                Name = "Motely",
                Description = "Motely - Balatro Seed Searcher",
                OptionsComparison = StringComparison.OrdinalIgnoreCase,
            };

            app.HelpOption("-?|-h|--help");

            // Core options
            var jsonOption = app.Option<string>(
                "-j|--json <JSON>",
                "JSON config file (JsonFilters/)",
                CommandOptionType.SingleValue
            );
            var jamlOption = app.Option<string>(
                "--jaml <JAML>",
                "JAML config file (JamlFilters/) - Joker Ante Markup Language",
                CommandOptionType.SingleValue
            );
            var analyzeOption = app.Option<string>(
                "--analyze <SEED>",
                "Analyze a specific seed",
                CommandOptionType.SingleValue
            );
            var outputJsonOption = app.Option(
                "--output-json",
                "Output analysis as JSON (for --analyze mode)",
                CommandOptionType.NoValue
            );
            var nativeOption = app.Option<string>(
                "-n|--native <FILTER>",
                "Run built-in native filter",
                CommandOptionType.SingleValue
            );
            var convertOption = app.Option(
                "--convert",
                "Convert all JSON filters to JAML format",
                CommandOptionType.NoValue
            );
            var scoreOption = app.Option<string>(
                "--score <JSON>",
                "Add JSON scoring to native filter",
                CommandOptionType.SingleValue
            );
            var csvScoreOption = app.Option<string>(
                "--csvScore <TYPE>",
                "Enable CSV scoring output (native for built-in)",
                CommandOptionType.SingleValue
            );
            var timeOption = app.Option<int>(
                "--time <SECONDS>",
                "Progress report interval in seconds (default: 1200)",
                CommandOptionType.SingleValue
            );

            // Search parameters
            var threadsOption = app.Option<int>(
                "--threads <COUNT>",
                "Number of threads",
                CommandOptionType.SingleValue
            );
            var batchSizeOption = app.Option<int>(
                "--batchSize <CHARS>",
                "Batch size",
                CommandOptionType.SingleValue
            );
            var startBatchOption = app.Option<long>(
                "--startBatch <INDEX>",
                "Starting batch",
                CommandOptionType.SingleValue
            );
            var endBatchOption = app.Option<long>(
                "--endBatch <INDEX>",
                "Ending batch",
                CommandOptionType.SingleValue
            );
            var startPercentOption = app.Option<double>(
                "--startPercent <PCT>",
                "Starting percent (0-100)",
                CommandOptionType.SingleValue
            );
            var startSeedOption = app.Option<string>(
                "--startSeed <SEED>",
                "Starting seed (e.g., TACO1111) - converts to batch number",
                CommandOptionType.SingleValue
            );
            var endPercentOption = app.Option<double>(
                "--endPercent <PCT>",
                "Ending percent (0-100)",
                CommandOptionType.SingleValue
            );

            // Input options
            var seedOption = app.Option<string>(
                "--seed <SEED>",
                "Specific seed",
                CommandOptionType.SingleValue
            );
            var seedsourcesOption = app.Option<string>(
                "--seedsource <SS>",
                "Seed source file (txt, csv, or db)",
                CommandOptionType.SingleValue
            );
            var keywordOption = app.Option<string>(
                "--keyword <KEYWORD>",
                "Generate seeds containing keyword (all seeds by default, use --sfw to filter NSFW)",
                CommandOptionType.SingleValue
            );
            var sfwOption = app.Option(
                "--sfw",
                "Filter out NSFW seeds (only return SFW seeds)",
                CommandOptionType.NoValue
            );
            var paddingOption = app.Option<string>(
                "--padding <CHARS>",
                "Restrict padding characters (e.g., --padding OU or --padding 1). Only works with --keyword.",
                CommandOptionType.SingleValue
            );
            var regenerateKeywordDbOption = app.Option(
                "--regenerate-keyword-db",
                "Force regeneration of keyword DB even if it exists",
                CommandOptionType.NoValue
            );
            var randomOption = app.Option<int>(
                "--random <COUNT>",
                "Test with random seeds",
                CommandOptionType.SingleValue
            );

            // Game options
            var deckOption = app.Option<string>(
                "--deck <DECK>",
                "Deck to use",
                CommandOptionType.SingleValue
            );
            var stakeOption = app.Option<string>(
                "--stake <STAKE>",
                "Stake to use",
                CommandOptionType.SingleValue
            );

            // JSON specific
            var cutoffOption = app.Option<string>(
                "--cutoff <SCORE>",
                "Min score threshold",
                CommandOptionType.SingleValue
            );

            // Output options
            var outputDbOption = app.Option<string>(
                "--output-db <PATH>",
                "Write results to DuckDB database file (instead of CSV to console)",
                CommandOptionType.SingleValue
            );
            var outputCsvOption = app.Option<string>(
                "--output-csv <PATH>",
                "Write results to CSV file (instead of CSV to console)",
                CommandOptionType.SingleValue
            );
            var debugOption = app.Option(
                "--debug",
                "Enable debug output",
                CommandOptionType.NoValue
            );
            
            // GPU acceleration options
            var dungmotOption = app.Option(
                "--dungmot",
                "Use GPU-accelerated dungmot as seed pre-filter",
                CommandOptionType.NoValue
            );
            var dungmotPathOption = app.Option<string>(
                "--dungmot-path <PATH>",
                "Path to dungmot executable (default: auto-detect based on filter type)",
                CommandOptionType.SingleValue
            );
            var noFancyOption = app.Option(
                "--nofancy",
                "Suppress fancy output",
                CommandOptionType.NoValue
            );
            var quietOption = app.Option(
                "--quiet",
                "Suppress all progress output (CSV only)",
                CommandOptionType.NoValue
            );

            // Set defaults for performance options
            threadsOption.DefaultValue = Environment.ProcessorCount;
            batchSizeOption.DefaultValue = 2;
            startBatchOption.DefaultValue = 0;
            endBatchOption.DefaultValue = 0;
            cutoffOption.DefaultValue = "0";
            deckOption.DefaultValue = "Red";
            stakeOption.DefaultValue = "White";
            timeOption.DefaultValue = 2; // 2 seconds

            app.OnExecute(() =>
            {
                // Analyze mode takes priority
                var analyzeSeed = analyzeOption.Value();
                if (!string.IsNullOrEmpty(analyzeSeed))
                {
                    return ExecuteAnalyze(
                        analyzeSeed,
                        deckOption.Value()!,
                        stakeOption.Value()!,
                        outputJsonOption.HasValue()
                    );
                }

                // Build common parameters first
                var parameters = new JsonSearchParams
                {
                    Threads = threadsOption.ParsedValue,
                    BatchSize = batchSizeOption.ParsedValue,
                    StartBatch = (ulong)startBatchOption.ParsedValue,
                    EndBatch = (ulong)endBatchOption.ParsedValue,
                    EnableDebug = debugOption.HasValue(),
                    NoFancy = noFancyOption.HasValue(),
                    Quiet = quietOption.HasValue(),
                    SpecificSeed = seedOption.Value(),
                    SeedSources = seedsourcesOption.Value(),
                    SeedList = null, // Will be set by keyword handling below
                    RandomSeeds = randomOption.HasValue() ? randomOption.ParsedValue : null,
                    CancellationToken = _cts.Token,
                };

                // Smart progress reporting: batch 1, then 0.01%-0.1%, then 1% increments
                if (!parameters.Quiet)
                {
                    parameters.ProgressCallback = CreateSmartProgressCallback();
                }

                static Action<long, long, long, double> CreateSmartProgressCallback()
                {
                    var lastReportedPercent = -1.0;
                    var batchOneReported = false;
                    var lockObj = new object();

                    return (batchIndex, completedBatches, totalBatches, elapsedSeconds) =>
                    {
                        lock (lockObj) // Only report from one thread at a time
                        {
                            // Report after batch 1
                            if (!batchOneReported && completedBatches >= 1)
                            {
                                Console.WriteLine($"   ✓ Batch 1 complete");
                                batchOneReported = true;
                                lastReportedPercent = 0;
                            }

                            if (totalBatches <= 0) return;

                            double progressPercent = (completedBatches * 100.0) / totalBatches;

                            // Determine reporting threshold based on progress
                            double threshold;
                            if (progressPercent < 0.1)
                                threshold = 0.01; // Report every 0.01% from 0-0.1%
                            else if (progressPercent < 1.0)
                                threshold = 0.1; // Report every 0.1% from 0.1%-1%
                            else
                                threshold = 1.0; // Report every 1% from 1% onwards

                            // Check if we've crossed a reporting threshold
                            double nextThreshold = (Math.Floor(lastReportedPercent / threshold) + 1) * threshold;

                            if (progressPercent >= nextThreshold)
                            {
                                var timeStr = elapsedSeconds < 60 
                                    ? $"{elapsedSeconds:F1}s"
                                    : $"{elapsedSeconds / 60:F1}m";
                                
                                Console.WriteLine($"   ◸ {progressPercent:F2}% complete ({completedBatches:N0}/{totalBatches:N0} batches) - {timeStr} elapsed");
                                lastReportedPercent = progressPercent;
                            }
                        }
                    };
                }

                // Handle --keyword: Use IEnumerable directly for fast keyword generation
                if (keywordOption.HasValue())
                {
                    string keyword = keywordOption.Value()!.ToUpperInvariant();
                    
                    if (!parameters.Quiet)
                        Console.WriteLine($"🔧 Generating seeds for keyword '{keyword}'...");
                    
                    bool sfwOnly = sfwOption.HasValue();
                    string? paddingChars = paddingOption.HasValue() ? paddingOption.Value() : null;
                    
                    // Generate seeds as IEnumerable (lazy, no allocation)
                    var keywordSeedList = GenerateKeywordSeeds(
                        keyword,
                        sfwOnly,
                        paddingChars,
                        parameters.Quiet
                    );
                    
                    // FAST PATH: Use IEnumerable directly (no DuckDB overhead)
                    // This skips all file I/O and locks - perfect for in-memory searching
                    parameters.SeedList = keywordSeedList;
                    parameters.SeedSources = null; // Don't use DuckDB for keywords
                    
                    if (!parameters.Quiet)
                        Console.WriteLine($"✅ Seeds ready for search (streaming mode)");
                }

                // Validate batch size
                if (parameters.BatchSize < 1 || parameters.BatchSize >= 8)
                {
                    Console.WriteLine(
                        $"❌ Error: batchSize must be between 1 and 7 (got {parameters.BatchSize})"
                    );
                    Console.WriteLine(
                        $"   batchSize represents the number of seed digits to process in parallel."
                    );
                    Console.WriteLine(
                        $"   Valid range: 1-7 (batchSize=8 creates a single 2.25 trillion seed batch)"
                    );
                    Console.WriteLine($"   Recommended: 2-4 for optimal performance");
                    return 1;
                }

                // Calculate max batches for this batch size
                long maxBatches = (long)Math.Pow(35, 8 - parameters.BatchSize);

                // Convert startSeed to batch if specified
                if (startSeedOption.HasValue())
                {
                    string startSeed = startSeedOption.Value()!.ToUpperInvariant();
                    long batchNum = ConvertSeedToBatch(startSeed, parameters.BatchSize);
                    parameters.StartBatch = (ulong)batchNum;
                    if (!parameters.Quiet)
                    {
                        Console.WriteLine(
                            $"📍 Starting at seed '{startSeed}' = batch {parameters.StartBatch:N0}"
                        );
                    }
                }
                // Convert percent to batch if specified (keyword/startSeed take priority)
                else if (startPercentOption.HasValue())
                {
                    double startPct = startPercentOption.ParsedValue;
                    if (startPct < 0 || startPct > 100)
                    {
                        Console.WriteLine($"❌ Error: startPercent must be 0-100 (got {startPct})");
                        return 1;
                    }
                    parameters.StartBatch = (ulong)(maxBatches * startPct / 100.0);
                    if (!parameters.Quiet)
                    {
                        Console.WriteLine(
                            $"📍 Starting at {startPct}% = batch {parameters.StartBatch:N0}"
                        );
                    }
                }

                if (endPercentOption.HasValue())
                {
                    double endPct = endPercentOption.ParsedValue;
                    if (endPct < 0 || endPct > 100)
                    {
                        Console.WriteLine($"❌ Error: endPercent must be 0-100 (got {endPct})");
                        return 1;
                    }
                    parameters.EndBatch = (ulong)(maxBatches * endPct / 100.0);
                    if (!parameters.Quiet)
                    {
                        if (endPct == 0)
                            Console.WriteLine($"📍 Ending at ∞ (no limit)");
                        else
                            Console.WriteLine(
                                $"📍 Ending at {endPct}% = batch {parameters.EndBatch:N0}"
                            );
                    }
                }
                else if (parameters.EndBatch == 0 && startPercentOption.HasValue())
                {
                    // User specified startPercent but no end - show infinity
                    if (!parameters.Quiet)
                    {
                        Console.WriteLine($"📍 Ending at ∞ (no limit)");
                    }
                }

                // Validate batch ranges
                if ((long)parameters.EndBatch > maxBatches)
                {
                    Console.WriteLine(
                        $"❌ endBatch too large: {parameters.EndBatch} (max for batchSize {parameters.BatchSize}: {maxBatches:N0})"
                    );
                    return 1;
                }
                if (parameters.StartBatch >= parameters.EndBatch && parameters.EndBatch != 0)
                {
                    Console.WriteLine(
                        $"❌ startBatch ({parameters.StartBatch}) must be less than endBatch ({parameters.EndBatch})"
                    );
                    return 1;
                }

                // Check which mode to run
                var nativeFilter = nativeOption.Value();
                if (!string.IsNullOrEmpty(nativeFilter))
                {
                    // Native filter mode
                    var scoreConfig = scoreOption.Value();

                    // Parse cutoff for native filters with scoring or CSV scoring
                    if (!string.IsNullOrEmpty(scoreConfig))
                    {
                        var cutoffStr = cutoffOption.Value() ?? "0";
                        parameters.AutoCutoff = cutoffStr.ToLowerInvariant() == "auto";
                        parameters.Cutoff = parameters.AutoCutoff
                            ? 1
                            : (int.TryParse(cutoffStr, out var c) ? c : 0);
                    }

                    var executor = new NativeFilterExecutor(nativeFilter, parameters, scoreConfig);
                    return executor.Execute();
                }
                else
                {
                    // Config file mode (JSON/JAML)
                    var cutoffStr = cutoffOption.Value() ?? "0";
                    bool autoCutoff = cutoffStr.ToLowerInvariant() == "auto";
                    parameters.Cutoff = autoCutoff
                        ? 0
                        : (int.TryParse(cutoffStr, out var c) ? c : 0);
                    parameters.AutoCutoff = autoCutoff;

                    // Determine which config format
                    string? configName = null;
                    string? configFormat = null;

                    if (jamlOption.HasValue())
                    {
                        configName = jamlOption.Value();
                        configFormat = "jaml";
                    }
                    else
                    {
                        configName = jsonOption.Value() ?? "standard";
                        configFormat = "json";
                    }

                    // Setup output (DuckDB and/or CSV)
                    Action<MotelySeedScoreTally>? dbCallback = null;
                    MotelySearchDatabase? db = null;
                    StreamWriter? csvWriter = null;
                    List<string>? csvColumnNames = null;
                    long seedsFound = 0;
                    
                    // Queue-based DB writer to avoid concurrent write conflicts
                    var dbWriteQueue = new System.Collections.Concurrent.BlockingCollection<MotelySeedScoreTally>();
                    Task? dbWriterTask = null;
                    CancellationTokenSource? dbWriterCts = null;

                    // Setup CSV output if requested
                    if (outputCsvOption.HasValue())
                    {
                        string csvPath = outputCsvOption.Value()!;
                        if (string.IsNullOrWhiteSpace(csvPath))
                        {
                            Console.WriteLine("❌ Error: --output-csv requires a CSV file path");
                            return 1;
                        }

                        // Load config to get column names for CSV header
                        MotelyJsonConfig? csvConfig = null;
                        if (configFormat == "jaml")
                        {
                            string jamlPath = Path.Combine("JamlFilters", configName! + ".jaml");
                            if (!File.Exists(jamlPath))
                            {
                                jamlPath = configName!; // Try as absolute path
                            }
                            if (
                                !JamlConfigLoader.TryLoadFromJaml(
                                    jamlPath,
                                    out csvConfig,
                                    out var error
                                )
                            )
                            {
                                Console.WriteLine($"❌ Error loading JAML config: {error}");
                                return 1;
                            }
                        }
                        else
                        {
                            string jsonPath = Path.Combine("JsonFilters", configName! + ".json");
                            if (!File.Exists(jsonPath))
                            {
                                jsonPath = configName!; // Try as absolute path
                            }
                            if (!MotelyJsonConfig.TryLoadFromJsonFile(jsonPath, out csvConfig))
                            {
                                Console.WriteLine($"❌ Error loading JSON config: {jsonPath}");
                                return 1;
                            }
                        }

                        if (csvConfig == null)
                        {
                            Console.WriteLine("❌ Error: Failed to load config for CSV output");
                            return 1;
                        }

                        csvColumnNames = csvConfig.GetColumnNames();
                        csvWriter = new StreamWriter(csvPath, append: false);

                        // Write CSV header
                        csvWriter.WriteLine(
                            string.Join(",", csvColumnNames.Select(name => $"\"{name}\""))
                        );
                        csvWriter.Flush();

                        if (!parameters.Quiet)
                        {
                            Console.WriteLine($"💾 Writing results to CSV: {csvPath}");
                        }
                    }

                    if (outputDbOption.HasValue())
                    {
                        string dbPath = outputDbOption.Value()!;
                        if (string.IsNullOrWhiteSpace(dbPath))
                        {
                            Console.WriteLine("❌ Error: --output-db requires a database path");
                            return 1;
                        }

                        // Load config to get column names upfront
                        MotelyJsonConfig? config = null;
                        if (configFormat == "jaml")
                        {
                            string jamlPath = Path.Combine("JamlFilters", configName! + ".jaml");
                            if (!File.Exists(jamlPath))
                            {
                                jamlPath = configName!; // Try as absolute path
                            }
                            if (
                                !JamlConfigLoader.TryLoadFromJaml(
                                    jamlPath,
                                    out config,
                                    out var error
                                )
                            )
                            {
                                Console.WriteLine($"❌ Error loading JAML config: {error}");
                                return 1;
                            }
                        }
                        else
                        {
                            string jsonPath = Path.Combine("JsonFilters", configName! + ".json");
                            if (!File.Exists(jsonPath))
                            {
                                jsonPath = configName!; // Try as absolute path
                            }
                            if (!MotelyJsonConfig.TryLoadFromJsonFile(jsonPath, out config))
                            {
                                Console.WriteLine($"❌ Error loading JSON config: {jsonPath}");
                                return 1;
                            }
                        }

                        if (config == null)
                        {
                            Console.WriteLine("❌ Error: Failed to load config");
                            return 1;
                        }

                        // Get column names from config
                        var columnNames = config.GetColumnNames();
                        db = new MotelySearchDatabase(dbPath, columnNames);

                        if (!parameters.Quiet)
                        {
                            Console.WriteLine($"💾 Writing results to: {dbPath}");
                        }

                        // Start dedicated DB writer thread
                        dbWriterCts = new CancellationTokenSource();
                        dbWriterTask = Task.Run(() =>
                        {
                            foreach (var result in dbWriteQueue.GetConsumingEnumerable(dbWriterCts.Token))
                            {
                                try
                                {
                                    db?.InsertRow(result.Seed, result.Score, result.TallyColumns);
                                    seedsFound++;
                                    // Only print once when first result is found
                                    if (seedsFound == 1)
                                        Console.Error.WriteLine(
                                            "✅ Found first matching seed (writing to DuckDB)..."
                                        );
                                }
                                catch (Exception ex)
                                {
                                    // CRITICAL: Stop on DB errors to prevent data corruption
                                    Console.Error.WriteLine(
                                        $"❌ [CRITICAL] Failed to write seed {result.Seed} to database: {ex.Message}"
                                    );
                                    Console.Error.WriteLine(
                                        $"   This is a fatal error - stopping search to prevent data loss!"
                                    );
                                    // Signal cancellation to stop the search
                                    _cts.Cancel();
                                    throw;
                                }
                            }
                        });

                        // Callback pushes to queue instead of writing directly
                        dbCallback = (result) =>
                        {
                            dbWriteQueue.Add(result);
                        };
                    }

                    // Combine callbacks if both CSV and DB are requested
                    if (csvWriter != null && dbCallback != null)
                    {
                        var originalDbCallback = dbCallback;
                        dbCallback = (result) =>
                        {
                            // Write to CSV
                            if (csvColumnNames != null && csvColumnNames.Count > 0)
                            {
                                var values = new List<string>
                                {
                                    $"\"{result.Seed}\"",
                                    result.Score.ToString(),
                                };
                                for (int i = 2; i < csvColumnNames.Count; i++)
                                {
                                    int tallyIndex = i - 2;
                                    int tallyValue =
                                        (tallyIndex < result.TallyColumns.Count)
                                            ? result.TallyColumns[tallyIndex]
                                            : 0;
                                    values.Add(tallyValue.ToString());
                                }
                                csvWriter.WriteLine(string.Join(",", values));
                                csvWriter.Flush();
                            }
                            // Write to DB
                            originalDbCallback(result);
                        };
                    }
                    else if (csvWriter != null)
                    {
                        // CSV only
                        dbCallback = (result) =>
                        {
                            if (csvColumnNames != null && csvColumnNames.Count > 0)
                            {
                                var values = new List<string>
                                {
                                    $"\"{result.Seed}\"",
                                    result.Score.ToString(),
                                };
                                for (int i = 2; i < csvColumnNames.Count; i++)
                                {
                                    int tallyIndex = i - 2;
                                    int tallyValue =
                                        (tallyIndex < result.TallyColumns.Count)
                                            ? result.TallyColumns[tallyIndex]
                                            : 0;
                                    values.Add(tallyValue.ToString());
                                }
                                csvWriter.WriteLine(string.Join(",", values));
                                csvWriter.Flush();
                                seedsFound++;
                                if (seedsFound == 1 && !parameters.Quiet)
                                    Console.Error.WriteLine(
                                        "✅ Found first matching seed (writing to CSV)..."
                                    );
                            }
                        };
                    }

                    // Handle dungmot GPU acceleration if requested
                    DungmotSeedProvider? dungmotProvider = null;
                    if (dungmotOption.HasValue() && configFormat == "jaml")
                    {
                        // Load JAML to get MotelyRunConfig for translation
                        string jamlPath = Path.Combine("JamlFilters", configName! + ".jaml");
                        if (!File.Exists(jamlPath))
                            jamlPath = configName!;

                        if (JamlConfigLoader.TryLoadFromJaml(jamlPath, out var jamlConfig, out var jamlError) && jamlConfig != null)
                        {
                            var runConfig = jamlConfig.ToRunConfig();
                            var dungmotOptions = new DungmotOptions
                            {
                                ExecutablePath = dungmotPathOption.HasValue() ? dungmotPathOption.Value() : null,
                                StartBatch = (long)parameters.StartBatch,
                                EndBatch = (long)parameters.EndBatch,
                                BatchChars = parameters.BatchSize
                            };

                            var dungmotConfig = DungmotFilterTranslator.TryTranslate(runConfig, dungmotOptions);
                            if (dungmotConfig != null)
                            {
                                if (!parameters.Quiet)
                                {
                                    Console.WriteLine($"🚀 GPU Mode: {DungmotFilterTranslator.DescribeFilter(dungmotConfig)}");
                                    Console.WriteLine($"   Executable: {dungmotConfig.ExecutablePath}");
                                    Console.WriteLine($"   Args: {dungmotConfig.ToArgumentString()}");
                                }

                                try
                                {
                                    dungmotProvider = new DungmotSeedProvider(dungmotConfig);
                                    // NOTE: For now, dungmot streams seeds but we don't yet pipe them to the search
                                    // Future: parameters.SeedProvider = dungmotProvider or similar
                                    Console.WriteLine("⚠️  GPU streaming mode not yet fully integrated with search pipeline.");
                                    Console.WriteLine("   Seeds will be generated but not fed to scoring (coming soon!)");
                                }
                                catch (Exception ex)
                                {
                                    Console.Error.WriteLine($"❌ Failed to start dungmot: {ex.Message}");
                                    Console.Error.WriteLine("   Falling back to CPU-only search...");
                                    dungmotProvider = null;
                                }
                            }
                            else
                            {
                                if (!parameters.Quiet)
                                {
                                    Console.WriteLine("⚠️  No dungmot-compatible filter found in JAML.");
                                    Console.WriteLine("   Dungmot supports: negative jokers, soul jokers, negative tags.");
                                    Console.WriteLine("   Falling back to CPU-only search...");
                                }
                            }
                        }
                        else
                        {
                            Console.Error.WriteLine($"❌ Failed to load JAML for dungmot translation: {jamlError}");
                        }
                    }
                    else if (dungmotOption.HasValue() && configFormat != "jaml")
                    {
                        Console.Error.WriteLine("⚠️  --dungmot currently only supports JAML configs (not JSON)");
                    }

                    var executor = new JsonSearchExecutor(
                        configName!,
                        parameters,
                        configFormat,
                        dbCallback
                    );
                    int exitCode = 0;
                    try
                    {
                        exitCode = executor.Execute();
                    }
                    finally
                    {
                        // CRITICAL: Drain queue and wait for DB writer to complete
                        if (dbWriteQueue != null && dbWriterTask != null)
                        {
                            try
                            {
                                // Signal no more items will be added
                                dbWriteQueue.CompleteAdding();
                                
                                // Wait for writer task to process remaining items (with timeout)
                                if (!dbWriterTask.Wait(TimeSpan.FromSeconds(30)))
                                {
                                    Console.Error.WriteLine("⚠️  DB writer task did not complete within 30 seconds");
                                    dbWriterCts?.Cancel();
                                    dbWriterTask?.Wait(TimeSpan.FromSeconds(5));
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.Error.WriteLine($"❌ Error draining DB queue: {ex.Message}");
                            }
                        }
                        
                        // CRITICAL: Always flush DuckDB on exit (normal or cancelled) to prevent WAL files
                        if (db != null)
                        {
                            try
                            {
                                db.Checkpoint();
                                
                                // Create indexes after search completes (deferred to avoid write conflicts)
                                db.CreateIndexes();
                                
                                if (!parameters.Quiet)
                                {
                                    var actualCount = db.GetResultCount();
                                    Console.WriteLine(
                                        $"💾 Total seeds saved to database: {actualCount} (verified)"
                                    );
                                    if (seedsFound != actualCount)
                                    {
                                        Console.Error.WriteLine(
                                            $"⚠️  WARNING: Expected {seedsFound} seeds but database contains {actualCount}!"
                                        );
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.Error.WriteLine($"❌ [CRITICAL] DuckDB checkpoint failed: {ex.Message}");
                            }
                        }

                        // Flush and close CSV
                        if (csvWriter != null)
                        {
                            try
                            {
                                csvWriter.Flush();
                                csvWriter.Close();
                                if (!parameters.Quiet)
                                {
                                    Console.WriteLine($"💾 Total seeds saved to CSV: {seedsFound}");
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.Error.WriteLine($"❌ [CRITICAL] CSV write failed: {ex.Message}");
                            }
                        }

                        // Clean up dungmot provider
                        dungmotProvider?.Dispose();
                    }
                    
                    // Dispose database connection
                    db?.Dispose();

                    return exitCode;
                }
            });

            try
            {
                return app.Execute(args);
            }
            catch (McMaster.Extensions.CommandLineUtils.UnrecognizedCommandParsingException ex)
            {
                // Clear the annoying "Specify --help" message by writing directly to stderr
                Console.Error.WriteLine($"❌ Error: {ex.Message}");
                Console.Error.WriteLine();
                app.ShowHelp();
                return 1;
            }
            catch (Exception ex)
                when (ex.Message.Contains("Unrecognized") || ex.Message.Contains("option"))
            {
                // Catch any other parsing errors
                Console.Error.WriteLine($"❌ Error: {ex.Message}");
                Console.Error.WriteLine();
                app.ShowHelp();
                return 1;
            }
        }

        private static int ExecuteAnalyze(
            string seed,
            string deckName,
            string stakeName,
            bool outputJson
        )
        {
            if (!Enum.TryParse<MotelyDeck>(deckName, true, out var deck))
            {
                Console.WriteLine($"❌ Invalid deck: {deckName}");
                return 1;
            }

            if (!Enum.TryParse<MotelyStake>(stakeName, true, out var stake))
            {
                Console.WriteLine($"❌ Invalid stake: {stakeName}");
                return 1;
            }

            var analysis = MotelySeedAnalyzer.Analyze(
                new MotelySeedAnalysisConfig(seed, deck, stake)
            );

            if (outputJson)
            {
                // Output as JSON for script consumption
                var jsonOutput = new
                {
                    seed = seed,
                    deck = deck.ToString(),
                    stake = stake.ToString(),
                    startingDeck = analysis.StartingDeck?.Split(
                        ',',
                        StringSplitOptions.RemoveEmptyEntries
                    ) ?? Array.Empty<string>(),
                    twos = analysis
                        .StartingDeck?.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Count(c => c.StartsWith("2_")) ?? 0,
                    error = analysis.Error,
                    antes = analysis
                        .Antes.Select(ante => new
                        {
                            ante = ante.Ante,
                            boss = FormatUtils.FormatBoss(ante.Boss),
                            voucher = FormatUtils.FormatVoucher(ante.Voucher),
                            smallBlindTag = FormatUtils.FormatTag(ante.SmallBlindTag),
                            bigBlindTag = FormatUtils.FormatTag(ante.BigBlindTag),
                            drawOrder = ante.DrawOrder,
                            shopQueue = ante
                                .ShopQueue.Select(item => new
                                {
                                    id = item.ToString(),
                                    name = FormatUtils.FormatItem(item),
                                })
                                .ToArray(),
                            packs = ante
                                .Packs.Select(pack => new
                                {
                                    type = FormatUtils.FormatPackName(pack.Type),
                                    items = pack
                                        .Items.Select(item => FormatUtils.FormatItem(item))
                                        .ToArray(),
                                })
                                .ToArray(),
                        })
                        .ToArray(),
                };
                Console.WriteLine(
                    System.Text.Json.JsonSerializer.Serialize(
                        jsonOutput,
                        new System.Text.Json.JsonSerializerOptions
                        {
                            WriteIndented = false,
                            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                        }
                    )
                );
            }
            else
            {
                Console.WriteLine($"🔍 Analyzing seed: '{seed}' with deck: {deck}, stake: {stake}");
                Console.Write(analysis);
            }
            return 0;
        }

        /// <summary>
        /// Generate seeds containing a keyword and return as IEnumerable (lazy evaluation).
        /// </summary>
        private static IEnumerable<string> GenerateKeywordSeeds(
            string keyword,
            bool sfwOnly,
            string? paddingChars,
            bool quiet
        )
        {
            // Validate keyword - only valid Balatro chars
            keyword = keyword.ToUpperInvariant().Replace('0', 'O');
            foreach (var c in keyword)
            {
                if (!"ABCDEFGHIJKLMNOPQRSTUVWXYZ123456789".Contains(c))
                {
                    throw new ArgumentException(
                        $"Invalid character '{c}' in keyword. Only A-Z and 1-9 allowed."
                    );
                }
            }

            if (keyword.Length > 8)
            {
                throw new ArgumentException(
                    $"Keyword too long ({keyword.Length} chars). Max 8 chars allowed."
                );
            }

            // Parse padding characters
            char[] validChars;
            if (!string.IsNullOrEmpty(paddingChars))
            {
                paddingChars = paddingChars.ToUpperInvariant().Replace('0', 'O');
                var paddingSet = new HashSet<char>();
                foreach (var c in paddingChars)
                {
                    if (!"ABCDEFGHIJKLMNOPQRSTUVWXYZ123456789".Contains(c))
                    {
                        throw new ArgumentException(
                            $"Invalid padding character '{c}'. Only A-Z and 1-9 allowed."
                        );
                    }
                    paddingSet.Add(c);
                }
                validChars = paddingSet.ToArray();
            }
            else
            {
                // Default: use all valid chars
                validChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ123456789".ToCharArray();
            }

            if (!quiet)
            {
                string filterMode = sfwOnly ? "SFW-only" : "all";
                string paddingInfo = paddingChars != null ? $" (padding: {paddingChars})" : "";
                Console.WriteLine(
                    $"🔧 Generating {filterMode} seeds containing '{keyword}'{paddingInfo}..."
                );
            }

            int maxPad = 8 - keyword.Length;

            // Generate all combinations - yield directly, no materialization!
            return GenerateKeywordSeedsEnumerable(keyword, maxPad, validChars, sfwOnly, quiet);
        }

        private static IEnumerable<string> GenerateKeywordSeedsEnumerable(
            string keyword,
            int maxPad,
            char[] validChars,
            bool sfwOnly,
            bool quiet
        )
        {
            int count = 0;
            yield return keyword;
            count++;

            // Generate with padding - yield directly (NO SFW filtering during generation)
            for (int padLen = 1; padLen <= maxPad; padLen++)
            {
                foreach (var seed in GeneratePaddedSeeds(keyword, padLen, validChars))
                {
                    yield return seed;
                    count++;
                }
            }

            if (!quiet)
            {
                Console.WriteLine($"   Generated {count:N0} seeds containing '{keyword}'");
            }
        }

        private static IEnumerable<string> GeneratePaddedSeeds(
            string keyword,
            int padLen,
            char[] validChars
        )
        {
            if (padLen <= 0)
            {
                yield return keyword;
                yield break;
            }

            if (padLen == 1)
            {
                foreach (var c in validChars)
                {
                    yield return c + keyword;
                    yield return keyword + c;
                }
            }
            else if (padLen == 2)
            {
                foreach (var c1 in validChars)
                {
                    foreach (var c2 in validChars)
                    {
                        yield return $"{c1}{c2}{keyword}";
                        yield return $"{keyword}{c1}{c2}";
                        yield return $"{c1}{keyword}{c2}";
                    }
                }
            }
            else if (padLen == 3)
            {
                foreach (var c1 in validChars)
                {
                    foreach (var c2 in validChars)
                    {
                        foreach (var c3 in validChars)
                        {
                            yield return $"{c1}{c2}{c3}{keyword}";
                            yield return $"{keyword}{c1}{c2}{c3}";
                            yield return $"{c1}{keyword}{c2}{c3}";
                            yield return $"{c1}{c2}{keyword}{c3}";
                        }
                    }
                }
            }
            else
            {
                // For padLen > 3, generate all positions
                foreach (var seed in GenerateLargePaddedSeeds(keyword, padLen, validChars))
                {
                    yield return seed;
                }
            }
        }

        private static IEnumerable<string> GenerateLargePaddedSeeds(string keyword, int padLen, char[] validChars)
        {
            var padding = new char[padLen];
            return GenerateLargePaddedSeedsRec(keyword, validChars, padding, 0);
        }

        private static IEnumerable<string> GenerateLargePaddedSeedsRec(string keyword, char[] validChars, char[] padding, int depth)
        {
            if (depth == padding.Length)
            {
                // Generate all positions for keyword within padding
                for (int pos = 0; pos <= padding.Length; pos++)
                {
                    var builder = new System.Text.StringBuilder(8);
                    builder.Append(padding, 0, pos);
                    builder.Append(keyword);
                    builder.Append(padding, pos, padding.Length - pos);
                    yield return builder.ToString();
                }
                yield break;
            }

            foreach (var c in validChars)
            {
                padding[depth] = c;
                foreach (var seed in GenerateLargePaddedSeedsRec(keyword, validChars, padding, depth + 1))
                {
                    yield return seed;
                }
            }
        }

        private static IEnumerable<string> GenerateAllCombinations(char[] validChars, int length)
        {
            if (length == 0)
            {
                yield return "";
                yield break;
            }

            var buffer = new char[length];
            foreach (var combo in GenerateCombinationsRecursive(validChars, buffer, 0))
            {
                yield return combo;
            }
        }

        private static IEnumerable<string> GenerateCombinationsRecursive(
            char[] validChars,
            char[] buffer,
            int index
        )
        {
            if (index == buffer.Length)
            {
                yield return new string(buffer);
                yield break;
            }

            foreach (var c in validChars)
            {
                buffer[index] = c;
                // Continue building - only yield when we reach the end (base case above)
                foreach (var result in GenerateCombinationsRecursive(validChars, buffer, index + 1))
                {
                    yield return result;
                }
            }
        }

        /// <summary>
        /// Convert a seed string to a batch number for sequential search.
        /// Batches are organized by the first (8 - BatchSize) characters.
        /// </summary>
        private static long ConvertSeedToBatch(string seed, int batchSize)
        {
            // Pad seed to max length if needed
            seed = seed.PadRight(8, '1');

            // Batches are organized by the characters that are NOT varying.
            // Motely iterates using the LEFT characters (indices 0 to batchSize-1)
            // as the varying parts within a batch.
            // The FIXED characters for a batch are the RIGHT characters (indices batchSize to 7).

            int fixedLength = 8 - batchSize;
            if (fixedLength <= 0)
                return 0;

            // Get the fixed part (the suffix)
            string suffix = seed.Substring(batchSize);

            // Convert to batch index:
            // digit at index batchSize is 35^0
            // digit at index batchSize+1 is 35^1
            // ...
            // digit at index 7 is 35^(fixedLength-1)

            long batchNum = 0;
            long multiplier = 1;

            for (int i = 0; i < suffix.Length; i++)
            {
                char c = suffix[i];
                int digitIndex = Array.IndexOf(Motely.SeedDigits, c);
                if (digitIndex < 0)
                {
                    throw new ArgumentException(
                        $"Invalid seed character '{c}' in '{seed}'. Valid chars: 1-9, A-Z"
                    );
                }

                batchNum += digitIndex * multiplier;
                multiplier *= 35;
            }

            return batchNum;
        }

        /// <summary>
        /// Save generated seeds to DuckDB file
        /// </summary>
        private static void SaveSeedsToDuckDB(IEnumerable<string> seeds, string dbPath, bool quiet, bool isRegenerating = false)
        {
            if (!quiet)
                Console.WriteLine($"💾 Saving seeds to {dbPath}...");
            
            // Only delete existing file if we're explicitly regenerating
            if (isRegenerating && File.Exists(dbPath))
                File.Delete(dbPath);
            
            using var conn = DuckDBConnectionFactory.CreateConnection(dbPath);
            using var cmd = conn.CreateCommand();
            
            // Create table
            cmd.CommandText = @"
                CREATE TABLE seeds (
                    id BIGINT,
                    seed VARCHAR
                );
                CREATE INDEX idx_seeds_id ON seeds(id);
            ";
            cmd.ExecuteNonQuery();
            
            // Insert seeds in batches (DuckDB is fast!)
            const int batchSize = 100000;
            var batch = new List<string>(batchSize);
            long id = 0;
            
            foreach (var seed in seeds)
            {
                batch.Add(seed);
                if (batch.Count >= batchSize)
                {
                    InsertBatch(cmd, batch, ref id);
                    batch.Clear();
                }
            }
            
            // Insert remaining
            if (batch.Count > 0)
                InsertBatch(cmd, batch, ref id);
            
            if (!quiet)
                Console.WriteLine($"✅ Saved {id:N0} seeds to {dbPath}");
        }

        /// <summary>
        /// Insert a batch of seeds into DuckDB
        /// </summary>
        private static void InsertBatch(DuckDBCommand cmd, List<string> batch, ref long startId)
        {
            // Build INSERT statement with VALUES clause
            var values = new StringBuilder();
            for (int i = 0; i < batch.Count; i++)
            {
                if (i > 0) values.Append(',');
                var seed = batch[i].Replace("'", "''"); // SQL escape
                values.Append($"({startId + i}, '{seed}')");
            }
            
            cmd.CommandText = $"INSERT INTO seeds (id, seed) VALUES {values}";
            cmd.ExecuteNonQuery();
            
            startId += batch.Count;
        }
    }
}
