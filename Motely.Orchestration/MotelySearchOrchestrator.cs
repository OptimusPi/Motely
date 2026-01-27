using System;
using System.Collections.Generic;
using System.IO;
using Motely.Filters;
using Motely.Reporting;

namespace Motely.Executors
{
    /// <summary>
    /// Static orchestrator to launch searches from various configuration formats.
    /// Provides one-liner entry points for CLI, TUI, API, and BSO.
    /// </summary>
    public static class MotelySearchOrchestrator
    {
        public static IMotelySearch LaunchJaml(string jamlPath, JsonSearchParams parameters, Action<MotelySeedScoreTally>? resultCallback = null)
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

            if (!JamlConfigLoader.TryLoadFromJaml(jamlPath, out var config, out var error) || config == null)
            {
                throw new InvalidOperationException($"Error loading JAML config: {error}");
            }

            return Launch(config, parameters, resultCallback);
        }

        public static IMotelySearch LaunchJson(string jsonPath, JsonSearchParams parameters, Action<MotelySeedScoreTally>? resultCallback = null)
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

        public static IMotelySearch LaunchNative(string filterName, JsonSearchParams parameters, string? scoreConfig = null)
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
                catch { /* Ignore, fallback to basic schema */ }
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
                MaxBossAnte = 8
            };

            var executor = new NativeFilterExecutor(filterName, parameters, scoreConfig);
            
            if (!string.IsNullOrEmpty(parameters.OutputDbPath))
            {
                executor.ResultsDatabase = OrchestrateDatabase(parameters.OutputDbPath, runConfig, parameters);
            }

            return executor.ExecuteAsSearch();
        }

        public static IMotelySearch Launch(MotelyJsonConfig config, JsonSearchParams parameters, Action<MotelySeedScoreTally>? resultCallback = null)
        {
            // Convert to run config
            var runConfig = MotelyRunConfig.Factory(config);
            var executor = new JsonSearchExecutor(config, parameters, resultCallback);
            
            if (!string.IsNullOrEmpty(parameters.OutputDbPath))
            {
                executor.ResultsDatabase = OrchestrateDatabase(parameters.OutputDbPath, runConfig, parameters);
            }
            
            return executor.ExecuteAsSearch();
        }

        private static global::Motely.DuckDB.MotelySearchDatabase OrchestrateDatabase(string dbPath, MotelyRunConfig runConfig, JsonSearchParams parameters)
        {
            bool exists = File.Exists(dbPath);
            bool compatible = exists && global::Motely.DuckDB.MotelySearchDatabase.IsSchemaCompatible(dbPath, runConfig, out _);

            if (exists && !compatible)
            {
                bool shouldOverwrite = parameters.ForceOverwrite;
                if (!shouldOverwrite && parameters.SchemaMismatchPrompt != null)
                {
                    shouldOverwrite = parameters.SchemaMismatchPrompt(dbPath, "Database schema mismatch. Existing database has different columns or types than current search config.");
                }

                if (shouldOverwrite)
                {
                    try { File.Delete(dbPath); } catch { /* Ignore delete errors, let DB open fail if needed */ }
                }
                else
                {
                    throw new InvalidOperationException($"Cannot use existing database '{dbPath}' due to schema mismatch. Use --force to overwrite.");
                }
            }

            return new global::Motely.DuckDB.MotelySearchDatabase(dbPath, runConfig);
        }

        // Helper to load scoring config synchronously (copy of logic from NativeFilterExecutor)
        private static MotelyJsonConfig LoadScoringConfigSync(string configPath)
        {
            if (Path.IsPathRooted(configPath) && File.Exists(configPath))
            {
                if (configPath.EndsWith(".jaml", StringComparison.OrdinalIgnoreCase))
                {
                    if (JamlConfigLoader.TryLoadFromJaml(configPath, out var jamlConfig, out var error))
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
