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
        private readonly SearchParameters _params;
        private bool _cancelled = false;
        
        public NativeFilterExecutor(string filterName, SearchParameters parameters, string? chainFilters = null)
        {
            _filterName = filterName;
            _chainFilters = chainFilters;
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
                (!string.IsNullOrEmpty(_chainFilters) ? $" with chained filters: {_chainFilters}" : ""));
            
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
                .WithProgressCallback(progressCallback);
    
            settings = ApplyChainedFilters(settings);
            
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
                "perkeoobservatory" => new PerkeoObservatoryFilterDesc(),
                "passthrough" => new PassthroughFilterDesc(),
                "trickeoglyph" => new TrickeoglyphFilterDesc(),
                "negativecopy" => new NegativeCopyFilterDesc(),
                "negativetags" => new NegativeTagFilterDesc(),
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
                var descriptor = GetFilterDescriptor(filter);
                
                // Unfortunately C# doesn't let us do this dynamically without reflection
                // So we still need the switch, but at least it's cleaner
                settings = descriptor switch
                {
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
            
            return settings;
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
            var lastBatch = search.CompletedBatchCount > 0 ? _params.StartBatch + search.CompletedBatchCount - 1 : 0;
            
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