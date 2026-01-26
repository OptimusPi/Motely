using DuckDB.NET.Data;
using McMaster.Extensions.CommandLineUtils;
using Motely.Analysis;
using Motely.Orchestration;
using DuckSeedStorage = global::Motely.DuckDB.DuckDBSeedStorage;
using Motely.Executors;
using Motely.Filters;
using Motely.GPU;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Text;

namespace Motely
{
    partial class Program
    {
        private static readonly CancellationTokenSource _cts = new();

        static int Main(string[] args)
        {
            // Wire up Ctrl+C to CancellationTokenSource for immediate cancellation
            // NOTE: Console.CancelKeyPress handlers are synchronous, so we must use Cancel()
            // not CancelAsync(). The cancellation token propagates to all awaiting tasks immediately.
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true; // Suppress default termination (allow graceful shutdown)
                _cts.Cancel(); // Signal cancellation token synchronously - propagates to all subscribers immediately
            };

            var app = new CommandLineApplication
            {
                Name = "Motely",
                Description = "Motely - Balatro Seed Searcher",
                OptionsComparison = StringComparison.OrdinalIgnoreCase,
            };

            app.HelpOption("-?|-h|--help");

            var noArgsProvided = args.Length == 0;

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
                "--cutoff <MODE>",
                "Min score threshold: number (0=no cutoff, 1+=manual), or specify 'auto' (AutoSmart) / 'best' (AutoBest)",
                CommandOptionType.SingleValue
            );

            // Output options
            var saveOption = app.Option(
                "--save",
                "Save results to SearchResults/ (DuckDB)",
                CommandOptionType.NoValue
            );

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
                if (noArgsProvided)
                {
                    app.ShowHelp();
                    return 0;
                }

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
                    Deck = deckOption.Value(),
                    Stake = stakeOption.Value(),
                    SeedList = null, // Will be set by keyword handling below
                    RandomSeeds = randomOption.HasValue() ? randomOption.ParsedValue : null,
                    CancellationToken = _cts.Token,
                };

                // Smart progress reporting: batch 1, then 0.01%-0.1%, then 1% increments
                if (!parameters.Quiet)
                {
                    parameters.ProgressCallback = CreateSmartProgressCallback();
                }

                static Action<MotelyProgress> CreateSmartProgressCallback()
                {
                    var lastReportedPercent = -1.0;
                    var batchOneReported = false;
                    var lastReportTime = DateTime.MinValue;
                    var lockObj = new object();

                    return (progress) =>
                    {
                        var now = DateTime.UtcNow;
                        
                        // Benign race condition on throttling is acceptable for console output
                        if ((now - lastReportTime).TotalSeconds < 2.0 && batchOneReported)
                            return;

                        if (progress.TotalBatchCount <= 0) return;

                        double progressPercent = progress.PercentComplete;
                        if (progressPercent > 100.0) progressPercent = 100.0;

                        // Determine reporting threshold
                        double threshold = progressPercent < 0.1 ? 0.001 : progressPercent < 1.0 ? 0.01 : 0.1;
                        double nextThreshold = (Math.Floor(lastReportedPercent / threshold) + 1) * threshold;

                        // Only report if crossed threshold or enough time elapsed
                        if (progressPercent >= nextThreshold || (now - lastReportTime).TotalSeconds > 10.0)
                        {
                            var timeStr = progress.ElapsedTime.TotalSeconds < 60
                                ? $"{progress.ElapsedTime.TotalSeconds:F1}s"
                                : $"{progress.ElapsedTime.TotalMinutes:F1}m";

                            Console.WriteLine($"   ◸ {progressPercent:F4}% complete ({progress.CompletedBatchCount:N0}/{progress.TotalBatchCount:N0} batches) - {timeStr} elapsed");
                            lastReportedPercent = progressPercent;
                            lastReportTime = now;
                            if (!batchOneReported && progress.CompletedBatchCount > 0)
                                batchOneReported = true;
                        }
                    };
                }

                // Handle --keyword: Use IEnumerable directly for fast keyword generation
                if (keywordOption.HasValue())
                {
                    string keyword = keywordOption.Value()!.ToUpperInvariant();

                    if (!parameters.Quiet)
                        Console.WriteLine($"🔧 Generating seeds for keyword '{keyword}'...");

                    string? paddingChars = paddingOption.HasValue() ? paddingOption.Value() : null;

                    // Generate seeds as IEnumerable (lazy, no allocation)
                    var keywordSeedList = GenerateKeywordSeeds(
                        keyword,
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

                // Convert percent to batch if specified (keyword/startSeed take priority)
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

                // Determine output paths
                string? dbPath = outputDbOption.Value();
                string? csvPath = outputCsvOption.Value();
                string? configName = jamlOption.HasValue() ? jamlOption.Value() : (jsonOption.Value() ?? "standard");

                // Check which mode to run
                var nativeFilter = nativeOption.Value();

                if (saveOption.HasValue())
                {
                    Directory.CreateDirectory("SearchResults");
                    var filterId = System.Text.RegularExpressions.Regex.Replace(
                        configName ?? "search",
                        "[^a-zA-Z0-9_-]",
                        "_"
                    ).ToLowerInvariant();
                    dbPath = $"SearchResults/{filterId}.db";
                }

                parameters.OutputDbPath = dbPath;

                // Setup CSV output if requested
                StreamWriter? csvWriter = null;
                if (!string.IsNullOrEmpty(csvPath))
                {
                    if (!parameters.Quiet)
                        Console.WriteLine($"💾 Writing results to CSV: {csvPath}");
                    
                    csvWriter = new StreamWriter(csvPath, false, Encoding.UTF8);
                    // Header will be printed by the executor inside the orchestrator
                    // But we need to handle the writing of rows if we want it in CSV.
                    // The Orchestrator's Launch method takes a resultCallback.
                }

                int exitCode = 0;
                try
                {
                    IMotelySearch search;
                    Action<MotelySeedScoreTally> resultCallback = (result) =>
                    {
                        if (csvWriter != null)
                        {
                            var values = new List<string> { $"\"{result.Seed}\"", result.Score.ToString() };
                            values.AddRange(result.TallyColumns.Select(t => t.ToString()));
                            csvWriter.WriteLine(string.Join(",", values));
                        }
                    };

                    if (!string.IsNullOrEmpty(nativeFilter))
                    {
                        search = MotelySearchOrchestrator.LaunchNative(nativeFilter, parameters, scoreOption.Value());
                    }
                    else
                    {
                        if (jamlOption.HasValue())
                        {
                            search = MotelySearchOrchestrator.LaunchJaml(jamlOption.Value()!, parameters, resultCallback);
                        }
                        else
                        {
                            search = MotelySearchOrchestrator.LaunchJson(jsonOption.Value() ?? "standard", parameters, resultCallback);
                        }
                    }

                    search.Start();
                    while (search.Status == MotelySearchStatus.Running || search.Status == MotelySearchStatus.Paused)
                    {
                        if (parameters.CancellationToken?.IsCancellationRequested == true)
                            break;
                        Thread.Sleep(100);
                    }
                    search.Dispose();
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"❌ Search failed: {ex.Message}");
                    if (parameters.EnableDebug)
                        Console.Error.WriteLine(ex.StackTrace);
                    exitCode = 1;
                }
                finally
                {
                    if (csvWriter != null)
                    {
                        csvWriter.Flush();
                        csvWriter.Close();
                    }
                }

                return exitCode;
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
                string paddingInfo = paddingChars != null ? $" (padding: {paddingChars})" : "";
                Console.WriteLine(
                    $"🔧 Generating seeds containing '{keyword}'{paddingInfo}..."
                );
            }

            int maxPad = 8 - keyword.Length;

            // Generate all combinations - yield directly, no materialization!
            var seeds = GenerateKeywordSeedsEnumerable(keyword, maxPad, validChars);
            var count = GetCountOfSeeds(keyword, maxPad, validChars.Length);
            if (!quiet)
            {
                Console.WriteLine(
                    $"🔧 Generated {count:N0} seeds containing '{keyword}'"
                );
            }

            return seeds;
        }

        private static IEnumerable<string> GenerateKeywordSeedsEnumerable(
            string keyword,
            int maxPad,
            char[] validChars)
        {
            
            yield return keyword;

            // Generate with padding - yield directly (NO SFW filtering during generation)
            for (int padLen = 1; padLen <= maxPad; padLen++)
            {
                foreach (var seed in GeneratePaddedSeeds(keyword, padLen, validChars))
                {
                    yield return seed;
                }
            }

        }

        private static IEnumerable<string> GeneratePaddedSeeds(
            string keyword,
            int padLen,
            char[] validChars
        )
        {
            if (validChars == null) throw new ArgumentNullException(nameof(validChars));

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
        private static long GetCountOfSeeds(string keyword, int maxPad, int validCharCount)
        {
            long total = 1; // The keyword itself

            // Formula: sum( (padLen + 1) * N^padLen ) for padLen 1 to maxPad
            // where N is validCharCount
            
            for (int padLen = 1; padLen <= maxPad; padLen++)
            {
                long permutations = (long)Math.Pow(validCharCount, padLen);
                long positions = padLen + 1;
                total += positions * permutations;
            }

            return total;
        }

        /// <summary>
        /// Save generated seeds to DuckDB file using Motely.DB optimized storage
        /// </summary>
        private static void SaveSeedsToDuckDB(IEnumerable<string> seeds, string dbPath, bool quiet, bool isRegenerating = false)
        {
            if (!quiet)
                Console.WriteLine($"💾 Saving seeds to {dbPath}...");
            
            // Only delete existing file if we're explicitly regenerating
            if (isRegenerating && File.Exists(dbPath))
                File.Delete(dbPath);
            
            using var storage = new DuckSeedStorage(dbPath);
            long count = storage.BulkInsertSeeds(seeds);
            
            if (!quiet)
                Console.WriteLine($"✅ Saved {count:N0} seeds to {dbPath}");
        }

        /// <summary>
        /// Insert a batch of seeds into DuckDB
        /// </summary>

    }
}
