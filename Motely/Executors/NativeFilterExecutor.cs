using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Motely.Filters;
using Motely.Utils;

namespace Motely.Executors
{
    /// <summary>
    /// Executes built-in native filters (--motely parameter)
    /// Handles: PerkeoObservatory, Trickeoglyph, NegativeCopy, SoulTest, etc.
    /// </summary>
    public class NativeFilterExecutor
    {
        private readonly string _filterName;
        private readonly string? _scoreConfig;
        private readonly JsonSearchParams _params;
        private bool _cancelled = false;
        private List<string>? _searchSeeds = null;

        public NativeFilterExecutor(string filterName, JsonSearchParams parameters, string? scoreConfig = null)
        {
            _filterName = filterName;
            _scoreConfig = scoreConfig;
            _params = parameters;
        }
        
        public int Execute()
        {
            DebugLogger.IsEnabled = _params.EnableDebug;
            FancyConsole.IsEnabled = !_params.NoFancy;
            
            string normalizedFilterName = _filterName.ToLower(System.Globalization.CultureInfo.CurrentCulture).Trim();
            
            // Progress callback - only used in silent mode or when fancy console is disabled
            // Otherwise FancyConsole handles progress display at the bottom line
            Action<long, long, long, double>? progressCallback = null;


            DateTime lastProgressUpdate = DateTime.UtcNow;
            DateTime progressStartTime = DateTime.UtcNow;
            progressCallback = (completed, total, seedsSearched, seedsPerMs) =>
            {
                var now = DateTime.UtcNow;
                var timeSinceLastUpdate = (now - lastProgressUpdate).TotalMilliseconds;

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
                Console.Write($"\r{progressLine}                    \r{progressLine}");
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
            
            Console.WriteLine($"🔍 Running native filter: {_filterName}" +
                (!string.IsNullOrEmpty(_params.SpecificSeed) ? $" on seed: {_params.SpecificSeed}" : "") +
                (!string.IsNullOrEmpty(_scoreConfig) ? $" with scoring: {_scoreConfig}" : ""));
            
            // DEBUG: Help identify non-determinism
            DebugLogger.Log($"Thread count: {_params.Threads}");
            DebugLogger.Log($"Batch size: {_params.BatchSize}");
            DebugLogger.Log($"Start batch: {_params.StartBatch}");
            DebugLogger.Log($"End batch: {_params.EndBatch}");
                
            // Setup cancellation handler
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                _cancelled = true;
                Console.WriteLine("\n🛑 Stopping search...");
                // Don't dispose here - let it finish gracefully
            };

            var searchStopwatch = Stopwatch.StartNew();

            // Add debug output for batch range processing
            if (_params.StartBatch > 0 || _params.EndBatch > 0)
            {
                Console.WriteLine($"   Processing batches: {_params.StartBatch} to {_params.EndBatch}");
                Console.WriteLine($"   Seeds per batch: {Math.Pow(35, _params.BatchSize):N0}");
                Console.WriteLine($"   Total seeds to search: {((_params.EndBatch - _params.StartBatch + 1) * Math.Pow(35, _params.BatchSize)):N0}");
            }

            search.Start();

            // Wait for completion using polling instead of blocking
            while (search.Status != MotelySearchStatus.Completed && !_cancelled)
            {
                System.Threading.Thread.Sleep(314);
            }

            // Stop the search gracefully (if cancelled)
            if (_cancelled)
            {
                search.Dispose();
            }

            // Give threads a moment to finish printing any final results
            System.Threading.Thread.Sleep(314);

            searchStopwatch.Stop();
            PrintSummary(search, searchStopwatch.Elapsed);
            
            return 0;
        }
        
        private IMotelySearch CreateFilterSearch(string filterName, Action<long, long, long, double>? progressCallback)
        {
            var seeds = LoadSeeds();
            var filterDesc = GetFilterDescriptor(filterName);
            
            // We need to handle each type explicitly since BuildSearch is generic
            return filterDesc switch
            {
                NaNSeedFilterDesc d => BuildSearch(d, progressCallback, seeds),
                PerkeoObservatoryFilterDesc d => BuildSearch(d, progressCallback, seeds),
                ObservatoryDesc d => BuildSearch(d, progressCallback, seeds),
                PassthroughFilterDesc d => BuildSearch(d, progressCallback, seeds),
                TrickeoglyphFilterDesc d => BuildSearch(d, progressCallback, seeds),
                NegativeCopyFilterDesc d => BuildSearch(d, progressCallback, seeds),
                NegativeTagFilterDesc d => BuildSearch(d, progressCallback, seeds),
                SoulTestFilterDesc d => BuildSearch(d, progressCallback, seeds),
                FilledSoulFilterDesc d => BuildSearch(d, progressCallback, seeds),
                _ => throw new ArgumentException($"Unknown filter type: {filterDesc.GetType()}")
            };
        }
        
        // Single method that builds the search with ALL the common settings
        private IMotelySearch BuildSearch<TFilter>(IMotelySeedFilterDesc<TFilter> filterDesc,
            Action<long, long, long, double>? progressCallback, List<string>? seeds) 
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
            
            if (seeds != null && seeds.Count > 0)
                return settings.WithListSearch(seeds).Start();
            else
                return settings.WithSequentialSearch().Start();
        }
        
        
        private object GetFilterDescriptor(string filterName)
        {
            var normalizedName = filterName.ToLower(System.Globalization.CultureInfo.CurrentCulture).Trim();
            DebugLogger.Log($"Loading filter descriptor for: {normalizedName}");
            return normalizedName switch
            {
                "nanseed" => new NaNSeedFilterDesc(),
                "perkeoobservatory" => new PerkeoObservatoryFilterDesc(),
                "observatory" => new ObservatoryDesc(),
                "passthrough" => new PassthroughFilterDesc(),
                "trickeoglyph" => new TrickeoglyphFilterDesc(),
                "negativecopy" => new NegativeCopyFilterDesc(),
                "negativetags" => new NegativeTagFilterDesc(),
                "negativetag" => new NegativeTagFilterDesc(),
                "soultest" => new SoulTestFilterDesc(),
                "filledsoul" => new FilledSoulFilterDesc(),
                _ => throw new ArgumentException($"Unknown filter: {filterName}")
            };
        }
        
        private bool IsJsonFilter(string filter)
        {
            // Check if JSON file exists
            string fileName = filter.EndsWith(".json") ? filter : filter + ".json";
            string jsonItemFiltersPath = Path.Combine("JsonItemFilters", fileName);
            return File.Exists(jsonItemFiltersPath) || (Path.IsPathRooted(filter) && File.Exists(filter));
        }
    
        
        private MotelySearchSettings<T> ApplyScoring<T>(MotelySearchSettings<T> settings) where T : struct, IMotelySeedFilter
        {
            if (string.IsNullOrEmpty(_scoreConfig))
                return settings;
                
            // Load the JSON config for scoring
            var config = LoadScoringConfig(_scoreConfig);
            
            // Print CSV header
            PrintResultsHeader(config);
            
            // Create scoring provider with callbacks
            string lastProgressLine = "";
            Action<MotelySeedScoreTally> onResultFound = (score) =>
            {
                try
                {
                    Console.Write($"\r{new string(' ', Math.Max(80, Console.WindowWidth - 1))}\r");
                }
                catch
                {
                    // Fallback if Console.WindowWidth fails
                    Console.Write("\r" + new string(' ', 120) + "\r");
                }
                Console.WriteLine($"{score.Seed},{score.Score},{string.Join(",", score.TallyColumns)}");
                if (!string.IsNullOrEmpty(lastProgressLine))
                    Console.Write(lastProgressLine);
            };
            
            // Use cutoff from params if provided
            int cutoff = _params.Cutoff;
            bool autoCutoff = _params.AutoCutoff;

            var scoreDesc = new MotelyJsonSeedScoreDesc(config, cutoff, autoCutoff, onResultFound);
            
            return settings.WithSeedScoreProvider(scoreDesc);
        }
        
        private MotelyJsonConfig LoadScoringConfig(string configPath)
        {
            if (Path.IsPathRooted(configPath) && File.Exists(configPath))
            {
                if (!MotelyJsonConfig.TryLoadFromJsonFile(configPath, out var config))
                    throw new InvalidOperationException($"Config loading failed for: {configPath}");
                return config;
            }
            
            string fileName = configPath.EndsWith(".json") ? configPath : configPath + ".json";
            string jsonItemFiltersPath = Path.Combine("JsonItemFilters", fileName);
            if (File.Exists(jsonItemFiltersPath))
            {
                if (!MotelyJsonConfig.TryLoadFromJsonFile(jsonItemFiltersPath, out var config))
                    throw new InvalidOperationException($"Config loading failed for: {jsonItemFiltersPath}");
                return config;
            }
            
            throw new FileNotFoundException($"Could not find JSON scoring config file: {configPath}");
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
        
        private List<string>? LoadSeeds()
        {
            if (!string.IsNullOrEmpty(_params.SpecificSeed))
            {
                _searchSeeds = new List<string> { _params.SpecificSeed };
                return _searchSeeds;
            }

            if (!string.IsNullOrEmpty(_params.Wordlist))
            {
                var wordlistPath = $"WordLists/{_params.Wordlist}.txt";
                if (!File.Exists(wordlistPath))
                {
                    throw new FileNotFoundException($"Wordlist file not found: {wordlistPath}");
                }
                _searchSeeds = File.ReadAllLines(wordlistPath)
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .ToList();
                return _searchSeeds;
            }

            return null;
        }

        
        private void PrintSummary(IMotelySearch search, TimeSpan duration)
        {
            Console.WriteLine(_cancelled ? "\n✅ Search stopped gracefully" : "\n✅ Search completed");

            // Calculate actual seeds searched
            ulong totalSeedsSearched;

            if (_searchSeeds != null)
            {
                // Wordlist or specific seed mode - use actual seed count
                totalSeedsSearched = (ulong)_searchSeeds.Count;
            }
            else
            {
                // Sequential batch mode - calculate from batches
                ulong seedsPerBatch = (ulong)Math.Pow(35, _params.BatchSize);
                totalSeedsSearched = (ulong)search.CompletedBatchCount * seedsPerBatch;
            }

            // Calculate the actual last batch processed
            var lastBatch = _params.StartBatch > 0
                ? (long)_params.StartBatch + search.CompletedBatchCount - 1
                : search.CompletedBatchCount;

            Console.WriteLine($"   Batches completed: {search.CompletedBatchCount}");
            Console.WriteLine($"   Last batch: {lastBatch}");
            Console.WriteLine($"   Seeds searched: {totalSeedsSearched:N0}");
            Console.WriteLine($"   Seeds passed filter: {search.FilteredSeeds}");
            Console.WriteLine($"   Seeds passed cutoff: {search.MatchingSeeds}");

            if (duration.TotalMilliseconds >= 1)
            {
                var speed = (double)totalSeedsSearched / duration.TotalMilliseconds;
                Console.WriteLine($"   Duration: {duration:hh\\:mm\\:ss\\.fff}");
                Console.WriteLine($"   Speed: {speed:N0} seeds/ms");
            }
        }
    }
}