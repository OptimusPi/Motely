using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Motely.Filters;

namespace Motely.Executors
{
    /// <summary>
    /// Executes JSON config-based searches (--config parameter)
    /// </summary>
    public class JsonSearchExecutor
    {
        private readonly string _configPath;
        private readonly SearchParameters _params;
        private bool _cancelled = false;
        
        public JsonSearchExecutor(string configPath, SearchParameters parameters)
        {
            _configPath = configPath;
            _params = parameters;
        }
        
        public int Execute()
        {
            DebugLogger.IsEnabled = _params.EnableDebug;
            FancyConsole.IsEnabled = !_params.NoFancy;
            
            List<string>? seeds = LoadSeeds();
            
            Console.WriteLine($"🔍 Motely Ouija Search Starting");
            Console.WriteLine($"   Config: {_configPath}");
            Console.WriteLine($"   Threads: {_params.Threads}");
            Console.WriteLine($"   Batch Size: {_params.BatchSize} chars");
            string endDisplay = _params.EndBatch <= 0 ? "∞" : _params.EndBatch.ToString();
            Console.WriteLine($"   Range: {_params.StartBatch} to {endDisplay}");
            if (_params.EnableDebug)
                Console.WriteLine($"   Debug: Enabled");
            Console.WriteLine();
            
            try
            {
                var config = LoadConfig(_configPath);
                Console.WriteLine($"✅ Loaded config: {config.Must?.Count ?? 0} must, {config.Should?.Count ?? 0} should, {config.MustNot?.Count ?? 0} mustNot");
                
                if (_params.EnableDebug)
                {
                    DebugLogger.Log("\n--- Parsed Motely JSON Config ---");
                    DebugLogger.Log(config.ToJson());
                    DebugLogger.Log("--- End Config ---\n");
                }
                
                // Create search and run
                var search = CreateSearch(config, seeds);
                var searchStopwatch = Stopwatch.StartNew();
                
                // Setup cancellation
                Console.CancelKeyPress += (sender, e) =>
                {
                    e.Cancel = true;
                    _cancelled = true;
                    Console.WriteLine("\n🛑 Stopping search...");
                    MotelyJsonSeedScoreDesc.MotelyJsonSeedScoreProvider.IsCancelled = true;
                    search.Dispose();
                };
                
                // Print CSV header
                PrintResultsHeader(config);
                
                // Wait for completion
                while (search.Status != MotelySearchStatus.Completed && !_cancelled)
                {
                    System.Threading.Thread.Sleep(100);
                }
                
                searchStopwatch.Stop();
                
                // Give threads a moment to finish printing any final results
                System.Threading.Thread.Sleep(50);
                
                // Print summary
                PrintSummary(search, searchStopwatch.Elapsed, seeds);
                
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
                if (_params.EnableDebug)
                    DebugLogger.Log(ex.StackTrace ?? "No stack trace");
                return 1;
            }
        }
        
        private List<string>? LoadSeeds()
        {
            if (!string.IsNullOrEmpty(_params.SpecificSeed))
            {
                Console.WriteLine($"🔍 Searching for specific seed: {_params.SpecificSeed}");
                return new List<string> { _params.SpecificSeed };
            }
            
            if (!string.IsNullOrEmpty(_params.Wordlist))
            {
                var wordlistPath = Path.Combine("WordLists", _params.Wordlist + ".txt");
                if (!File.Exists(wordlistPath))
                    throw new FileNotFoundException($"Wordlist file not found: {wordlistPath}");
                    
                var seeds = File.ReadAllLines(wordlistPath)
                    .Select(line => line.Trim())
                    .Where(line => line.Length == 8)
                    .ToList();
                    
                Console.WriteLine($"✅ Loaded {seeds.Count} seeds from wordlist: {wordlistPath}");
                return seeds;
            }
            
            return null;
        }
        
        private MotelyJsonConfig LoadConfig(string configPath)
        {
            if (Path.IsPathRooted(configPath) && File.Exists(configPath))
            {
                DebugLogger.Log($"📁 Loading config from: {configPath}");
                if (!MotelyJsonConfig.TryLoadFromJsonFile(configPath, out var config))
                    throw new InvalidOperationException($"Config loading failed for: {configPath}");
                return config;
            }
            
            string fileName = configPath.EndsWith(".json") ? configPath : configPath + ".json";
            string jsonItemFiltersPath = Path.Combine("JsonItemFilters", fileName);
            if (File.Exists(jsonItemFiltersPath))
            {
                DebugLogger.Log($"📁 Loading config from: {jsonItemFiltersPath}");
                if (!MotelyJsonConfig.TryLoadFromJsonFile(jsonItemFiltersPath, out var config))
                    throw new InvalidOperationException($"Config loading failed for: {jsonItemFiltersPath}");
                return config;
            }
            
            throw new FileNotFoundException($"Could not find JSON config file: {configPath}");
        }
        
        private IMotelySearch CreateSearch(MotelyJsonConfig config, List<string>? seeds)
        {
            Console.WriteLine("CreateSearch...");
            string lastProgressLine = "";
            Action<MotelySeedScoreTally> onResultFound = (score) =>
            {
                // Skip output in silent mode
                if (_params.Silent)
                {
                    return;
                }
                
                Console.Write("\r" + new string(' ', Console.WindowWidth - 1) + "\r");
                Console.WriteLine($"{score.Seed},{score.Score},{string.Join(",", score.TallyColumns)}");
                if (!string.IsNullOrEmpty(lastProgressLine))
                    Console.Write(lastProgressLine);
            };
            
            var scoreDesc = new MotelyJsonSeedScoreDesc(config, _params.Cutoff, _params.AutoCutoff, onResultFound, _params.ScoreOnly);
            
            if (_params.AutoCutoff)
                Console.WriteLine($"✅ Loaded config with auto-cutoff (starting at {_params.Cutoff})");
            else
                Console.WriteLine($"✅ Loaded config with cutoff: {_params.Cutoff}");
                
            var searchSettings = Program.CreateSliceChainedSearch(config, _params.Threads, _params.BatchSize, _params.ScoreOnly);
            
            // Add scoring if needed
            bool hasValidShouldClauses = config.Should != null && 
                config.Should.Count > 0 && 
                config.Should.Any(c => !string.IsNullOrEmpty(c.Type) || !string.IsNullOrEmpty(c.Value));
                
            if (hasValidShouldClauses || _params.ScoreOnly)
                searchSettings = searchSettings.WithSeedScoreProvider(scoreDesc);
                
            // Apply deck and stake
            if (!string.IsNullOrEmpty(config.Deck) && Enum.TryParse<MotelyDeck>(config.Deck, true, out var deck))
                searchSettings = searchSettings.WithDeck(deck);
            if (!string.IsNullOrEmpty(config.Stake) && Enum.TryParse<MotelyStake>(config.Stake, true, out var stake))
                searchSettings = searchSettings.WithStake(stake);
                
            // Set batch range
            searchSettings = searchSettings.WithStartBatchIndex(_params.StartBatch);
            if (_params.EndBatch > 0)
                searchSettings = searchSettings.WithEndBatchIndex(_params.EndBatch);
                
            // Progress callback
                DateTime progressStartTime = DateTime.UtcNow;
            DateTime lastProgressUpdate = DateTime.UtcNow;
            searchSettings = searchSettings.WithProgressCallback((completed, total, seedsSearched, seedsPerMs) =>
            {
                if (_params.Silent) return; // Skip progress display in silent mode
                
                var now = DateTime.UtcNow;
                var timeSinceLastUpdate = (now - lastProgressUpdate).TotalMilliseconds;
                if (timeSinceLastUpdate < 100) return;
                lastProgressUpdate = now;
                
                var elapsedMS = (now - progressStartTime).TotalMilliseconds;
                string timeLeftFormatted = "calculating...";
                if (total > 0 && completed > 0)
                {
                    double portionFinished = (double)completed / total;
                    double timeLeft = elapsedMS / portionFinished - elapsedMS;
                    TimeSpan timeLeftSpan = TimeSpan.FromMilliseconds(Math.Min(timeLeft, TimeSpan.MaxValue.TotalMilliseconds));
                    if (timeLeftSpan.Days == 0) timeLeftFormatted = $"{timeLeftSpan:hh\\:mm\\:ss}";
                    else timeLeftFormatted = $"{timeLeftSpan:d\\:hh\\:mm\\:ss}";
                }
                double pct = total > 0 ? Math.Clamp(((double)completed / total) * 100, 0, 100) : 0;
                string[] spinnerFrames = ["⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧"];
                var spinner = spinnerFrames[(int)(elapsedMS / 250) % spinnerFrames.Length];
                string progressLine = $"{spinner} {pct:F2}% | {timeLeftFormatted} remaining | {Math.Round(seedsPerMs)} seeds/ms";
                lastProgressLine = $"\r{progressLine}";
                Console.Write($"\r{progressLine}                    \r{progressLine}");
            });
            
            // Start search
            if (seeds != null && seeds.Count > 0)
                return searchSettings.WithListSearch(seeds).Start();
            else
                return searchSettings.WithSequentialSearch().Start();
        }
        
        private void PrintResultsHeader(MotelyJsonConfig config)
        {
            Console.WriteLine($"# Deck: {config.Deck}, Stake: {config.Stake}");
            var header = "Seed,TotalScore";
            
            if (config.Should != null)
            {
                foreach (var should in config.Should)
                {
                    var col = should.Label ?? should.Value ?? should.Type;
                    header += $",{col}";
                }
            }
            Console.WriteLine(header);
        }
        
        private void PrintSummary(IMotelySearch search, TimeSpan duration, List<string>? seeds)
        {
            Console.WriteLine(_cancelled ? "\n✅ Search stopped gracefully" : "\n✅ Search completed");
            
            // Use the actual tracked counts from the search
            long lastBatchIndex = search.CompletedBatchCount > 0 ? _params.StartBatch + search.CompletedBatchCount : 0;
            
            Console.WriteLine($"   Last batch Index: {lastBatchIndex}");
            Console.WriteLine($"   Seeds searched: {search.TotalSeedsSearched:N0}");
            Console.WriteLine($"   Seeds matched: {search.MatchingSeeds:N0}");
            
            if (duration.TotalMilliseconds >= 1)
            {
                var speed = (double)search.TotalSeedsSearched / duration.TotalMilliseconds;
                Console.WriteLine($"   Duration: {duration:hh\\:mm\\:ss\\.fff}");
                Console.WriteLine($"   Speed: {speed:N0} seeds/ms");
            }
        }
    }
    
    /// <summary>
    /// Common search parameters
    /// </summary>
    public class SearchParameters
    {
        public int Threads { get; set; } = Environment.ProcessorCount;
        public int BatchSize { get; set; } = 1;
        public long StartBatch { get; set; } = 0;
        public long EndBatch { get; set; } = 0;
        public int Cutoff { get; set; } = 0;
        public bool AutoCutoff { get; set; } = false;
        public bool EnableDebug { get; set; } = false;
        public bool NoFancy { get; set; } = false;
        public bool ScoreOnly { get; set; } = false;
        public bool Silent { get; set; } = false;
        public string? SpecificSeed { get; set; }
        public string? Wordlist { get; set; }
        public string? Keyword { get; set; }
    }
}