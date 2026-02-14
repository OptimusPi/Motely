using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Motely.Filters;

namespace Motely.Executors
{
    /// <summary>
    /// Minimal orchestrator to launch searches.
    /// </summary>
    public static class MotelySearchOrchestrator
    {
        /// <summary>
        /// Launch a search and return a context.
        /// </summary>
        public static IMotelySearchContext LaunchWithContext(
            MotelyJsonConfig config,
            JsonSearchParams parameters,
            bool useInMemoryStorage = false
        )
        {
            var searchId = GenerateShortSearchId(config);
            var filterId = GenerateFilterId(config);

            // Create executor with callback
            var executor = new JsonSearchExecutor(config, parameters, parameters.ResultCallback);
            var search = executor.ExecuteAsSearch();
            return new MotelySearchContext(search, searchId, filterId);
        }

        /// <summary>
        /// Generate a SHORT search ID for browser/WASM.
        /// </summary>
        public static string GenerateShortSearchId(MotelyJsonConfig config)
        {
            var name = SanitizeForId(config.Name ?? "Unknown", maxLength: 12);
            var deck = config.Deck ?? "Red";
            var stake = config.Stake ?? "White";
            var timestamp = DateTime.UtcNow.ToString("HHmmss");
            var random = Guid.NewGuid().ToString("N")[..4];
            return $"{name}_{deck}_{stake}_{timestamp}_{random}";
        }

        /// <summary>
        /// Generate a consistent filter ID from config.
        /// </summary>
        public static string GenerateFilterId(MotelyJsonConfig config)
        {
            var name = SanitizeForId(config.Name ?? "Unknown", maxLength: 30);
            var deck = config.Deck ?? "Red";
            var stake = config.Stake ?? "White";
            return $"{name}_{deck}_{stake}";
        }

        /// <summary>
        /// Sanitize a string for use in file/folder names.
        /// </summary>
        public static string SanitizeForId(string input, int maxLength = 50)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "unknown";

            var firstPart = input.Split(new[] { ",", " - ", "–", "—", ";", ". " }, StringSplitOptions.None)[0];
            var sanitized = firstPart.Trim().Replace(" ", "");
            var invalidChars = Path.GetInvalidFileNameChars();
            foreach (var c in invalidChars)
            {
                sanitized = sanitized.Replace(c, '_');
            }
            
            if (sanitized.Length > maxLength)
                sanitized = sanitized[..maxLength];
                
            return sanitized;
        }
    }
}
