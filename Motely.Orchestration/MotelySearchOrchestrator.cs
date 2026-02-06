using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Motely.Filters;
using Motely.Reporting;
using Motely.Repository;

namespace Motely.Executors
{
    /// <summary>
    /// Static orchestrator to launch searches from various configuration formats.
    /// Host sets repository (desktop: DuckDB-backed; browser: in-memory or throws for file/DB).
    /// </summary>
    public static class MotelySearchOrchestrator
    {
        /// <summary>Set repository implementation. Must be called before using any methods.</summary>
        public static void SetRepository(IMotelyRepository repository) =>
            RepositoryHost.Set(repository);

        /// <summary>Print CSV header row for a config. Call after your startup messages but before search.Start().</summary>
        public static void PrintCsvHeader(MotelyJsonConfig config)
        {
            var columnNames = config.GetColumnNames();
            var allColumns = new List<string> { "Seed", "Score" };
            allColumns.AddRange(columnNames.Skip(2)); // Skip "seed" and "score" since we already have them capitalized
            var headerLine = string.Join(",", allColumns.Select(name => $"\"{name}\""));
            Console.WriteLine(headerLine);
            Console.Out.Flush();
        }

        /// <summary>
        /// Launch a search and return a context with full result access.
        /// This is the preferred method for UI applications like BSO.
        ///
        /// Motely owns everything: SearchId, FilterId, database operations, result queries.
        /// The consumer just calls methods on the returned context.
        /// </summary>
        /// <param name="config">The filter configuration</param>
        /// <param name="parameters">Search parameters (threads, batch size, etc.)</param>
        /// <param name="useInMemoryStorage">True for browser/WASM builds, false for desktop</param>
        /// <returns>Search context with full control and result access</returns>
        public static IMotelySearchContext LaunchWithContext(
            MotelyJsonConfig config,
            JsonSearchParams parameters,
            bool useInMemoryStorage = false
        )
        {
            var runConfig = MotelyRunConfig.Factory(config);

            // Generate IDs - Motely owns this!
            var filterId = GenerateFilterId(config);
            var searchId =
                $"{filterId}_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid().ToString("N")[..8]}";

            if (useInMemoryStorage)
            {
                // Browser/WASM: Use in-memory storage with callback (no DuckDB in NPM package)
                var context = LaunchInMemory(config, runConfig, parameters, searchId, filterId);
                return context;
            }

            // Desktop: Use database storage
            var contextDb = LaunchWithDatabase(config, runConfig, parameters, searchId, filterId);
            return contextDb;
        }

        private static MotelySearchContext LaunchWithDatabase(
            MotelyJsonConfig config,
            MotelyRunConfig runConfig,
            JsonSearchParams parameters,
            string searchId,
            string filterId
        )
        {
            // Pass moniker to repository - implementation handles path/storage (desktop: DuckDB, browser: host-provided)
            var moniker = parameters.OutputDbPath ?? filterId;
            var database = RepositoryHost.Instance.GetSink(moniker, runConfig);

            // Create executor with callback that writes to database
            var executor = new JsonSearchExecutor(
                config,
                parameters,
                result =>
                {
                    database.InsertRow(
                        result.Seed,
                        result.Score,
                        result.TallyColumns,
                        result.ColumnValues
                    );
                }
            );

            var search = executor.ExecuteAsSearch();

            return new MotelySearchContext(search, database, runConfig, searchId, filterId);
        }

        private static MotelySearchContext LaunchInMemory(
            MotelyJsonConfig config,
            MotelyRunConfig runConfig,
            JsonSearchParams parameters,
            string searchId,
            string filterId
        )
        {
            // Create executor first (needed to get search instance)
            // We'll create a placeholder callback that will be updated after context creation
            MotelySearchContext? contextRef = null;

            // Combined callback: store in context AND call external callback if provided
            Action<MotelySeedScoreTally> combinedCallback = result =>
            {
                // Store result in context's in-memory storage (contextRef is assigned before Start() is called)
                contextRef?.StoreResult(result);

                // Also call external callback if provided (e.g., for JS interop)
                parameters.ResultCallback?.Invoke(result);
            };

            var executor = new JsonSearchExecutor(config, parameters, combinedCallback);
            var search = executor.ExecuteAsSearch();

            // Create context AFTER executor (context needs search instance)
            // Note: Callback won't be invoked until Start() is called, so contextRef will be set by then
            var context = new MotelySearchContext(search, runConfig, searchId, filterId);
            contextRef = context; // Assign to closure variable

            return context;
        }

        /// <summary>
        /// Generate a consistent filter ID from config (used for both filterId and searchId prefix).
        /// Public so CLI can use the same logic.
        /// </summary>
        public static string GenerateFilterId(MotelyJsonConfig config)
        {
            var name = SanitizeForId(config.Name ?? "Unknown");
            var deck = config.Deck ?? "Red";
            var stake = config.Stake ?? "White";
            return $"{name}_{deck}_{stake}";
        }

        /// <summary>
        /// Sanitize a string for use in file/folder names.
        /// Public so CLI can use the same logic.
        /// </summary>
        public static string SanitizeForId(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "unknown";

            // Replace spaces with underscores, remove invalid chars
            var sanitized = input.Trim().Replace(" ", "");
            var invalidChars = Path.GetInvalidFileNameChars();
            foreach (var c in invalidChars)
            {
                sanitized = sanitized.Replace(c, '_');
            }
            return sanitized;
        }

        // === Legacy methods for backward compatibility (desktop only) ===

        public static IMotelySearch LaunchJaml(
            string jamlPath,
            JsonSearchParams parameters,
            Action<MotelySeedScoreTally>? resultCallback = null
        )
        {
            if (!File.Exists(jamlPath))
            {
                // Try looking in JamlFilters
                string localPath = Path.Combine("JamlFilters", jamlPath);
                if (!localPath.EndsWith(".jaml", StringComparison.OrdinalIgnoreCase))
                    localPath += ".jaml";

                if (File.Exists(localPath))
                    jamlPath = localPath;
                else
                    throw new FileNotFoundException($"JAML config file not found: {jamlPath}");
            }

            if (
                !JamlConfigLoader.TryLoadFromJaml(jamlPath, out var config, out var error)
                || config == null
            )
            {
                throw new InvalidOperationException($"Error loading JAML config: {error}");
            }

            return Launch(config, parameters, resultCallback);
        }

        public static IMotelySearch LaunchJson(
            string jsonPath,
            JsonSearchParams parameters,
            Action<MotelySeedScoreTally>? resultCallback = null
        )
        {
            if (!File.Exists(jsonPath))
            {
                // Try looking in JsonFilters
                string localPath = Path.Combine("JsonFilters", jsonPath);
                if (!localPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    localPath += ".json";

                if (File.Exists(localPath))
                    jsonPath = localPath;
                else
                    throw new FileNotFoundException($"JSON config file not found: {jsonPath}");
            }

            if (!MotelyJsonConfig.TryLoadFromJsonFile(jsonPath, out var config) || config == null)
            {
                throw new InvalidOperationException($"Error loading JSON config: {jsonPath}");
            }

            return Launch(config, parameters, resultCallback);
        }

        public static IMotelySearch LaunchNative(
            string filterName,
            JsonSearchParams parameters,
            string? scoreConfig = null,
            ITerminalOutput? terminal = null,
            ICancelKeyHandler? cancelKeyHandler = null
        )
        {
            MotelyRunConfig? runConfig = null;
            if (!string.IsNullOrEmpty(scoreConfig))
            {
                // Try to load score config to get schema
                try
                {
                    var config = LoadScoringConfigSync(scoreConfig);
                    runConfig = MotelyRunConfig.Factory(config);
                }
                catch
                { /* Ignore, fallback to basic schema */
                }
            }

            // Fallback to basic schema (seed, score) if no scoring config
            runConfig ??= new MotelyRunConfig
            {
                Name = filterName,
                Columns = new List<ColumnDefinition>(),
                Deck = MotelyDeck.Red, // Defaults
                Stake = MotelyStake.White,
                Must = new List<MotelyJsonFilterClause>(),
                MustNot = new List<MotelyJsonFilterClause>(),
                Should = new List<MotelyJsonFilterClause>(),
                MaxVoucherAnte = 8,
                MaxBossAnte = 8,
            };

            var executor = new NativeFilterExecutor(
                filterName,
                parameters,
                scoreConfig,
                terminal,
                cancelKeyHandler
            );

            if (!string.IsNullOrEmpty(parameters.OutputDbPath))
            {
                executor.ResultsDatabase = RepositoryHost.Instance.GetSink(
                    parameters.OutputDbPath,
                    runConfig
                );
            }

            return executor.ExecuteAsSearch();
        }

        public static IMotelySearch Launch(
            MotelyJsonConfig config,
            JsonSearchParams parameters,
            Action<MotelySeedScoreTally>? resultCallback = null
        )
        {
            // Convert to run config
            var runConfig = MotelyRunConfig.Factory(config);

            // Auto-generate database path from config if OutputDbPath is null but user wants to save
            // Orchestrator handles everything - generates filterId from config and creates path
            if (string.IsNullOrEmpty(parameters.OutputDbPath) && parameters.AutoSave)
            {
                var filterId = GenerateFilterId(config);
                // Use unified "seeds" folder (combines SearchResults and SeedSources)
                var seedsDir = Path.Combine(Directory.GetCurrentDirectory(), "seeds");
                parameters.OutputDbPath = Path.Combine(seedsDir, $"{filterId}.db");
            }

            var executor = new JsonSearchExecutor(config, parameters, resultCallback);

            if (!string.IsNullOrEmpty(parameters.OutputDbPath))
            {
                executor.ResultStorage = RepositoryHost.Instance.GetSink(
                    parameters.OutputDbPath,
                    runConfig
                );
            }

            return executor.ExecuteAsSearch();
        }

        // Helper to load scoring config synchronously (copy of logic from NativeFilterExecutor)
        private static MotelyJsonConfig LoadScoringConfigSync(string configPath)
        {
            if (Path.IsPathRooted(configPath) && File.Exists(configPath))
            {
                if (configPath.EndsWith(".jaml", StringComparison.OrdinalIgnoreCase))
                {
                    if (
                        JamlConfigLoader.TryLoadFromJaml(
                            configPath,
                            out var jamlConfig,
                            out var error
                        )
                    )
                        return jamlConfig!;
                    throw new Exception(error);
                }
                if (MotelyJsonConfig.TryLoadFromJsonFile(configPath, out var jsonConfig))
                    return jsonConfig;
                throw new Exception("JSON load failed");
            }

            // Local directory check logic would go here, but for now we'll keep it simple
            // and expect rooted or obvious paths when calling from orchestrator.
            // In a real refactor, this should move to a central ConfigLoader.
            throw new FileNotFoundException($"Scoring config not found: {configPath}");
        }
    }
}
