using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Motely.Filters;

namespace Motely.Executors
{
    /// <summary>
    /// Executes built-in native filters (--motely parameter)
    /// Handles: PerkeoObservatory, Trickeoglyph, NegativeCopy, SoulTest, etc.
    /// </summary>
    public class NativeFilterExecutor
    {
        private readonly string _filterName;
        private readonly string? _chainFilters;
        private readonly string? _scoreConfig;
        private readonly SearchParameters _params;
        private bool _cancelled = false;
        
        public NativeFilterExecutor(string filterName, SearchParameters parameters, string? chainFilters = null, string? scoreConfig = null)
        {
            _filterName = filterName;
            _chainFilters = chainFilters;
            _scoreConfig = scoreConfig;
            _params = parameters;
        }
        
        public int Execute()
        {
            DebugLogger.IsEnabled = _params.EnableDebug;
            FancyConsole.IsEnabled = !_params.NoFancy;
            
            string normalizedFilterName = _filterName.ToLower().Trim();
            
            // Create progress callback
            DateTime lastProgressUpdate = DateTime.UtcNow;
            DateTime progressStartTime = DateTime.UtcNow;
            Action<long, long, long, double> progressCallback = (completed, total, seedsSearched, seedsPerMs) =>
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
                (!string.IsNullOrEmpty(_chainFilters) ? $" with chained filters: {_chainFilters}" : "") +
                (!string.IsNullOrEmpty(_scoreConfig) ? $" with scoring: {_scoreConfig}" : ""));
            
            // DEBUG: Help identify non-determinism
            Console.WriteLine($"   DEBUG: Thread count: {_params.Threads}");
            Console.WriteLine($"   DEBUG: Batch size: {_params.BatchSize}");
            Console.WriteLine($"   DEBUG: Start batch: {_params.StartBatch}");
            Console.WriteLine($"   DEBUG: End batch: {_params.EndBatch}");
                
            // Setup cancellation handler
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                _cancelled = true;
                Console.WriteLine("\n🛑 Stopping search...");
                search.Dispose();
            };
            
            var searchStopwatch = Stopwatch.StartNew();
            search.Start();
            
            // Wait for completion using polling instead of blocking
            while (search.Status != MotelySearchStatus.Completed && !_cancelled)
            {
                System.Threading.Thread.Sleep(100);
            }
            
            // Give threads a moment to finish printing any final results
            System.Threading.Thread.Sleep(50);
            
            searchStopwatch.Stop();
            PrintSummary(search, searchStopwatch.Elapsed);
            
            return 0;
        }
        
        private IMotelySearch CreateFilterSearch(string filterName, Action<long, long, long, double> progressCallback)
        {
            var seeds = LoadSeeds();
            var filterDesc = GetFilterDescriptor(filterName);
            
            // We need to handle each type explicitly since BuildSearch is generic
            return filterDesc switch
            {
                NaNSeedFilterDesc d => BuildSearch(d, progressCallback, seeds),
                PerkeoObservatoryFilterDesc d => BuildSearch(d, progressCallback, seeds),
                PassthroughFilterDesc d => BuildSearch(d, progressCallback, seeds),
                TrickeoglyphFilterDesc d => BuildSearch(d, progressCallback, seeds),
                NegativeCopyFilterDesc d => BuildSearch(d, progressCallback, seeds),
                NegativeTagFilterDesc d => BuildSearch(d, progressCallback, seeds),
                SoulTestFilterDesc d => BuildSearch(d, progressCallback, seeds),
                _ => throw new ArgumentException($"Unknown filter type: {filterDesc.GetType()}")
            };
        }
        
        // Single method that builds the search with ALL the common settings
        private IMotelySearch BuildSearch<TFilter>(IMotelySeedFilterDesc<TFilter> filterDesc, 
            Action<long, long, long, double> progressCallback, List<string>? seeds) 
            where TFilter : struct, IMotelySeedFilter
        {
            var settings = new MotelySearchSettings<TFilter>(filterDesc)
                .WithThreadCount(_params.Threads)
                .WithBatchCharacterCount(_params.BatchSize)
                .WithSilent(_params.Silent)
                .WithProgressCallback(progressCallback);
    
            settings = ApplyChainedFilters(settings);
            settings = ApplyScoring(settings);
            settings = ApplyCsvScoring(settings, filterDesc);
            
            settings = settings.WithStartBatchIndex(_params.StartBatch-1);
            if (_params.EndBatch > 0) settings = settings.WithEndBatchIndex(_params.EndBatch);
            
            if (seeds != null && seeds.Count > 0)
                return settings.WithListSearch(seeds).Start();
            else
                return settings.WithSequentialSearch().Start();
        }
        
        
        private object GetFilterDescriptor(string filterName)
        {
            var normalizedName = filterName.ToLower().Trim();
            return normalizedName switch
            {
                "nanseed" => new NaNSeedFilterDesc(),
                "perkeoobservatory" => new PerkeoObservatoryFilterDesc(),
                "passthrough" => new PassthroughFilterDesc(),
                "trickeoglyph" => new TrickeoglyphFilterDesc(),
                "negativecopy" => new NegativeCopyFilterDesc(),
                "negativetags" => new NegativeTagFilterDesc(),
                "negativetag" => new NegativeTagFilterDesc(),
                "soultest" => new SoulTestFilterDesc(),
                _ => throw new ArgumentException($"Unknown filter: {filterName}")
            };
        }
        
        private MotelySearchSettings<T> ApplyChainedFilters<T>(MotelySearchSettings<T> settings) where T : struct, IMotelySeedFilter
        {
            if (string.IsNullOrEmpty(_chainFilters))
                return settings;
                
            var filters = _chainFilters.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            
            foreach (var filter in filters)
            {
                // Check if it's a JSON filter (contains .json or exists as json file)
                if (filter.EndsWith(".json") || IsJsonFilter(filter))
                {
                    // Load JSON filter
                    var config = LoadJsonConfig(filter);
                    var jsonFilter = new MotelyJsonFilterDesc(FilterCategory.Mixed, config.Must.ToList());
                    settings = settings.WithAdditionalFilter(jsonFilter);
                    Console.WriteLine($"   + Chained JSON filter: {filter}");
                }
                else
                {
                    // Native filter
                    var descriptor = GetFilterDescriptor(filter);
                    
                    settings = descriptor switch
                    {
                        NaNSeedFilterDesc d => settings.WithAdditionalFilter(d),
                        PerkeoObservatoryFilterDesc d => settings.WithAdditionalFilter(d),
                        TrickeoglyphFilterDesc d => settings.WithAdditionalFilter(d),
                        NegativeCopyFilterDesc d => settings.WithAdditionalFilter(d),
                        NegativeTagFilterDesc d => settings.WithAdditionalFilter(d),
                        SoulTestFilterDesc d => settings.WithAdditionalFilter(d),
                        PassthroughFilterDesc d => settings.WithAdditionalFilter(d),
                        _ => throw new ArgumentException($"Unknown chain filter type: {descriptor.GetType()}")
                    };
                    
                    Console.WriteLine($"   + Chained filter: {filter}");
                }
            }
            
            return settings;
        }
        
        private bool IsJsonFilter(string filter)
        {
            // Check if JSON file exists
            string fileName = filter.EndsWith(".json") ? filter : filter + ".json";
            string jsonItemFiltersPath = Path.Combine("JsonItemFilters", fileName);
            return File.Exists(jsonItemFiltersPath) || (Path.IsPathRooted(filter) && File.Exists(filter));
        }
        
        private MotelyJsonConfig LoadJsonConfig(string configPath)
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
            
            throw new FileNotFoundException($"Could not find JSON config file: {configPath}");
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
                if (_params.Silent)
                    return;
                    
                Console.Write("\r" + new string(' ', Console.WindowWidth - 1) + "\r");
                Console.WriteLine($"{score.Seed},{score.Score},{string.Join(",", score.TallyColumns)}");
                if (!string.IsNullOrEmpty(lastProgressLine))
                    Console.Write(lastProgressLine);
            };
            
            // Use cutoff from params if provided
            int cutoff = _params.Cutoff;
            bool autoCutoff = _params.AutoCutoff;
            
            var scoreDesc = new MotelyJsonSeedScoreDesc(config, cutoff, autoCutoff, onResultFound, ScoreOnlyMode: false);
            
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
        
        private MotelySearchSettings<TFilter> ApplyCsvScoring<TFilter>(MotelySearchSettings<TFilter> settings, IMotelySeedFilterDesc<TFilter> filterDesc) 
            where TFilter : struct, IMotelySeedFilter
        {
            // Check if --csvScore is specified
            if (string.IsNullOrEmpty(_params.CsvScore))
                return settings;
            
            // Special handling for NegativeCopyFilterDesc with CSV scoring
            if (filterDesc is NegativeCopyFilterDesc && _params.CsvScore == "native")
            {
                // Print CSV header
                Console.WriteLine("Seed,Score,Showman,Blueprint,Brainstorm,Invisible,NegShowman,NegBlueprint,NegBrainstorm,NegInvisible");
                
                // Create score handler
                Action<Filters.NegativeCopyJokersScore> onResultFound = (score) =>
                {
                    // Apply cutoff if specified
                    if (_params.Cutoff > 0 && score.Score < _params.Cutoff)
                        return;
                        
                    Console.WriteLine($"{score.Seed},{score.Score}," +
                        $"{score.ShowmanCount},{score.BlueprintCount},{score.BrainstormCount},{score.InvisibleCount}," +
                        $"{score.NegativeShowmanCount},{score.NegativeBlueprintCount},{score.NegativeBrainstormCount},{score.NegativeInvisibleCount}");
                };
                
                // Create the score descriptor
                var scoreDesc = new Filters.NegativeCopyJokersScoreDesc(_params.Cutoff, _params.AutoCutoff, onResultFound);
                return settings.WithSeedScoreProvider(scoreDesc);
            }
            
            return settings;
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
                return new List<string> { _params.SpecificSeed };
            }
            
            if (!string.IsNullOrEmpty(_params.Wordlist))
            {
                var wordlistPath = $"WordLists/{_params.Wordlist}.txt";
                if (!File.Exists(wordlistPath))
                {
                    throw new FileNotFoundException($"Wordlist file not found: {wordlistPath}");
                }
                return File.ReadAllLines(wordlistPath)
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .ToList();
            }
            
            return null;
        }
        
        private void PrintSummary(IMotelySearch search, TimeSpan duration)
        {
            Console.WriteLine(_cancelled ? "\n✅ Search stopped gracefully" : "\n✅ Search completed");
            
            // Use the actual tracked counts from the search
            var lastBatch = search.CompletedBatchCount > 0 ? _params.StartBatch + search.CompletedBatchCount : 0;
            
            Console.WriteLine($"   Last batch: {lastBatch}");
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
}