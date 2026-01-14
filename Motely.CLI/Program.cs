using McMaster.Extensions.CommandLineUtils;
using Motely.Analysis;
using Motely.Executors;
using Motely.Filters;
using Motely.API;
using Motely.DuckDB;

namespace Motely
{
    partial class Program
    {
        static int Main(string[] args)
        {

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
                "Generate seeds containing keyword (SFW by default, use --nsfw for NSFW)",
                CommandOptionType.SingleValue
            );
            var nsfwOption = app.Option(
                "--nsfw",
                "Switch keyword search to NSFW mode",
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
                    return ExecuteAnalyze(analyzeSeed, deckOption.Value()!, stakeOption.Value()!, outputJsonOption.HasValue());
                }

                // Handle --keyword option: generate seeds containing the keyword
                string? keywordSeedSource = null;
                if (keywordOption.HasValue())
                {
                    string keyword = keywordOption.Value()!.ToUpperInvariant();
                    bool isNsfw = nsfwOption.HasValue();
                    keywordSeedSource = GenerateKeywordSeeds(keyword, isNsfw, quietOption.HasValue());
                }

                // Build common parameters
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
                    SeedSources = keywordSeedSource ?? seedsourcesOption.Value(),
                    RandomSeeds = randomOption.HasValue() ? randomOption.ParsedValue : null,
                };

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

                // Convert percent to batch if specified
                if (startPercentOption.HasValue())
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
                            if (!JamlConfigLoader.TryLoadFromJaml(jamlPath, out csvConfig, out var error))
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
                        csvWriter.WriteLine(string.Join(",", csvColumnNames.Select(name => $"\"{name}\"")));
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
                            if (!JamlConfigLoader.TryLoadFromJaml(jamlPath, out config, out var error))
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
                        
                        dbCallback = (result) =>
                        {
                            try
                            {
                                db?.InsertRow(result.Seed, result.Score, result.TallyColumns);
                                seedsFound++;
                                // Avoid spamming/interleaving with progress output (both write to stderr).
                                // Just print once when the first result is found.
                                if (seedsFound == 1)
                                    Console.Error.WriteLine("✅ Found first matching seed (writing to DuckDB)...");
                            }
                            catch (Exception ex)
                            {
                                // CRITICAL: Never silently swallow database errors!
                                Console.Error.WriteLine($"❌ [CRITICAL] Failed to write seed {result.Seed} to database: {ex.Message}");
                                Console.Error.WriteLine($"   This is a fatal error - stopping search to prevent data loss!");
                                throw; // Re-throw to stop the search
                            }
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
                                var values = new List<string> { $"\"{result.Seed}\"", result.Score.ToString() };
                                for (int i = 2; i < csvColumnNames.Count; i++)
                                {
                                    int tallyIndex = i - 2;
                                    int tallyValue = (tallyIndex < result.TallyColumns.Count) ? result.TallyColumns[tallyIndex] : 0;
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
                                var values = new List<string> { $"\"{result.Seed}\"", result.Score.ToString() };
                                for (int i = 2; i < csvColumnNames.Count; i++)
                                {
                                    int tallyIndex = i - 2;
                                    int tallyValue = (tallyIndex < result.TallyColumns.Count) ? result.TallyColumns[tallyIndex] : 0;
                                    values.Add(tallyValue.ToString());
                                }
                                csvWriter.WriteLine(string.Join(",", values));
                                csvWriter.Flush();
                                seedsFound++;
                                if (seedsFound == 1 && !parameters.Quiet)
                                    Console.Error.WriteLine("✅ Found first matching seed (writing to CSV)...");
                            }
                        };
                    }
                    
                    var executor = new JsonSearchExecutor(configName!, parameters, configFormat, dbCallback);
                    int exitCode = executor.Execute();
                    
                    // Flush and close CSV
                    if (csvWriter != null)
                    {
                        try
                        {
                            csvWriter.Flush();
                            csvWriter.Close();
                            Console.WriteLine($"💾 Total seeds saved to CSV: {seedsFound}");
                        }
                        catch (Exception ex)
                        {
                            Console.Error.WriteLine($"❌ [CRITICAL] CSV write failed: {ex.Message}");
                            return 1;
                        }
                    }
                    
                    // Flush and close database
                    if (db != null)
                    {
                        try
                        {
                            db.Checkpoint();
                            
                            // CRITICAL: Verify data was actually written!
                            db.VerifyDataWritten();
                            
                            var actualCount = db.GetResultCount();
                            Console.WriteLine($"💾 Total seeds saved to database: {actualCount} (verified)");
                            
                            if (seedsFound != actualCount)
                            {
                                Console.Error.WriteLine($"⚠️  WARNING: Expected {seedsFound} seeds but database contains {actualCount}!");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.Error.WriteLine($"❌ [CRITICAL] Database checkpoint/verification failed: {ex.Message}");
                            Console.Error.WriteLine($"   This may indicate data loss - check the database manually!");
                            return 1; // Return error code
                        }
                        finally
                        {
                            db.Dispose();
                        }
                    }
                    
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
            catch (Exception ex) when (ex.Message.Contains("Unrecognized") || ex.Message.Contains("option"))
            {
                // Catch any other parsing errors
                Console.Error.WriteLine($"❌ Error: {ex.Message}");
                Console.Error.WriteLine();
                app.ShowHelp();
                return 1;
            }
        }

        private static int ExecuteAnalyze(string seed, string deckName, string stakeName, bool outputJson)
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
                    startingDeck = analysis.StartingDeck?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>(),
                    twos = analysis.StartingDeck?.Split(',', StringSplitOptions.RemoveEmptyEntries).Count(c => c.StartsWith("2_")) ?? 0,
                    error = analysis.Error,
                    antes = analysis.Antes.Select(ante => new
                    {
                        ante = ante.Ante,
                        boss = FormatUtils.FormatBoss(ante.Boss),
                        voucher = FormatUtils.FormatVoucher(ante.Voucher),
                        smallBlindTag = FormatUtils.FormatTag(ante.SmallBlindTag),
                        bigBlindTag = FormatUtils.FormatTag(ante.BigBlindTag),
                        drawOrder = ante.DrawOrder,
                        shopQueue = ante.ShopQueue.Select(item => new
                        {
                            id = item.ToString(),
                            name = FormatUtils.FormatItem(item)
                        }).ToArray(),
                        packs = ante.Packs.Select(pack => new
                        {
                            type = FormatUtils.FormatPackName(pack.Type),
                            items = pack.Items.Select(item => FormatUtils.FormatItem(item)).ToArray()
                        }).ToArray()
                    }).ToArray()
                };
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(jsonOutput, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = false,
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                }));
            }
            else
            {
                Console.WriteLine($"🔍 Analyzing seed: '{seed}' with deck: {deck}, stake: {stake}");
                Console.Write(analysis);
            }
            return 0;
        }

        /// <summary>
        /// Generate seeds containing a keyword and return path to the generated seed source file.
        /// </summary>
        private static string GenerateKeywordSeeds(string keyword, bool isNsfw, bool quiet)
        {
            // Validate keyword - only valid Balatro chars
            keyword = keyword.ToUpperInvariant().Replace('0', 'O');
            foreach (var c in keyword)
            {
                if (!"ABCDEFGHIJKLMNOPQRSTUVWXYZ123456789".Contains(c))
                {
                    throw new ArgumentException($"Invalid character '{c}' in keyword. Only A-Z and 1-9 allowed.");
                }
            }
            
            if (keyword.Length > 8)
            {
                throw new ArgumentException($"Keyword too long ({keyword.Length} chars). Max 8 chars allowed.");
            }
            
            // Generate seeds containing this keyword
            string directory = "SeedSources";
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            string mode = isNsfw ? "nsfw" : "sfw";
            string fileName = $"_keyword__{keyword.ToLowerInvariant()}_{mode}.txt";
            string filePath = Path.Combine(directory, fileName);
            
            if (!quiet)
            {
                Console.WriteLine($"🔧 Generating {mode.ToUpper()} seeds containing '{keyword}'...");
            }
            
            int count = 0;
            using (var writer = new StreamWriter(filePath))
            {
                // Generate all valid padding combinations around the keyword
                int maxPad = 8 - keyword.Length;
                char[] validChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ123456789".ToCharArray();
                
                // Keyword alone
                if (CheckKeywordValidity(keyword, isNsfw))
                {
                    writer.WriteLine(keyword);
                    count++;
                }
                
                // Generate with padding
                for (int padLen = 1; padLen <= maxPad; padLen++)
                {
                    foreach (var seed in GeneratePaddedSeeds(keyword, padLen, validChars))
                    {
                        if (CheckKeywordValidity(seed, isNsfw))
                        {
                            writer.WriteLine(seed);
                            count++;
                        }
                    }
                }
            }
            
            if (!quiet)
            {
                Console.WriteLine($"   Generated {count:N0} seeds containing '{keyword}'");
            }
            
            return filePath;
        }
        
        private static bool CheckKeywordValidity(string seed, bool isNsfw)
        {
            if (isNsfw)
            {
                // For NSFW mode, we want NSFW seeds
                return NsfwSeedGenerator.ScoreSeed(seed) > 0;
            }
            else
            {
                // For SFW mode, reject NSFW seeds
                return SfwSeedGenerator.IsSfw(seed);
            }
        }
        
        private static IEnumerable<string> GeneratePaddedSeeds(string keyword, int padLen, char[] validChars)
        {
            if (padLen <= 0)
            {
                yield return keyword;
                yield break;
            }
            
            // Generate padding combinations (limit depth to avoid explosion)
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
            else if (padLen >= 4)
            {
                // For 4+ padding, only do prefix/suffix to avoid explosion
                foreach (var c1 in validChars)
                {
                    foreach (var c2 in validChars)
                    {
                        foreach (var c3 in validChars)
                        {
                            foreach (var c4 in validChars)
                            {
                                string pad = $"{c1}{c2}{c3}{c4}";
                                if (padLen == 4)
                                {
                                    yield return pad + keyword;
                                    yield return keyword + pad;
                                }
                                else
                                {
                                    // For 5+, use 4-char pad on one side only
                                    yield return pad + keyword;
                                    yield return keyword + pad;
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
