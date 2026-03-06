using System;
using System.IO;
using Motely.Filters;

namespace Motely.Executors
{
    /// <summary>
    /// Minimal orchestrator to launch searches.
    /// </summary>
    public static class MotelySearchOrchestrator
    {
        public static IMotelySearchContext LaunchWithContext(
            JamlConfig config,
            JsonSearchParams parameters
        )
        {
            var searchId = GenerateShortSearchId(config);
            var filterId = GenerateFilterId(config);
            var search = BuildSearchFromJaml(config, parameters);
            return new MotelySearchContext(search, searchId, filterId);
        }

        private static IMotelySearch BuildSearchFromJaml(
            JamlConfig config,
            JsonSearchParams parameters
        )
        {
            var settings = JamlSearchBuilder
                .CreateSettings(config)
                .WithDeck(config.Deck)
                .WithStake(config.Stake)
                .WithThreadCount(Math.Max(1, parameters.Threads))
                .WithBatchCharacterCount(Math.Clamp(parameters.BatchCharCount, 1, 7));
            // TODO: when IMotelySearchSettings has WithResultCallback(parameters.ResultCallback), wire it so UI Results collection fills

            if (parameters.StartBatch > 0)
                settings.WithStartBatchIndex(
                    (long)Math.Min(parameters.StartBatch, (ulong)long.MaxValue)
                );

            if (parameters.EndBatch > 0)
                settings.WithEndBatchIndex(
                    (long)Math.Min(parameters.EndBatch, (ulong)long.MaxValue)
                );

            if (!string.IsNullOrWhiteSpace(parameters.SpecificSeed))
                settings.WithListSearch(new[] { parameters.SpecificSeed.ToUpperInvariant() });
            else if (parameters.RandomSeeds > 0)
                settings.WithRandomSearch(parameters.RandomSeeds);
            else if (parameters.PalindromeSeeds)
                settings.WithPalindromeSearch();
            else
                settings.WithSequentialSearch();

            return settings.Start();
        }

        public static string GenerateShortSearchId(JamlConfig config)
        {
            return Guid.NewGuid().ToString("N");
        }

        public static string GenerateFilterId(JamlConfig config)
        {
            var name = SanitizeForId(config.Name ?? "Unknown", maxLength: 30);
            var deck = config.Deck.ToString();
            var stake = config.Stake.ToString();
            return $"{name}_{deck}_{stake}";
        }

        private readonly struct PassAllFilterDesc
            : IMotelySeedFilterDesc<PassAllFilterDesc.PassAllFilter>
        {
            public PassAllFilter CreateFilter(ref MotelyFilterCreationContext ctx) => new();

            public readonly struct PassAllFilter : IMotelySeedFilter
            {
                public VectorMask Filter(ref MotelyVectorSearchContext searchContext) =>
                    VectorMask.AllBitsSet;
            }
        }

        /// <summary>
        /// Sanitize a string for use in file/folder names.
        /// </summary>
        public static string SanitizeForId(string input, int maxLength = 50)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "unknown";

            var firstPart = input.Split(
                new[] { ",", " - ", "–", "—", ";", ". " },
                StringSplitOptions.None
            )[0];
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
