using System.Collections.Generic;
using Motely.Reporting;
using Motely.Utils;

namespace Motely.Filters;

/// <summary>
/// A "cooked" version of MotelyJsonConfig that is ready for runtime execution.
/// All strings are parsed into enums, all defaults are applied, and columns are defined.
/// </summary>
public sealed record MotelyRunConfig
{
    public required string Name { get; init; }
    public required MotelyDeck Deck { get; init; }
    public required MotelyStake Stake { get; init; }

    // Core search logic
    // Now using typed descriptors for high-performance execution
    public IMotelySeedFilterDesc? FilterPipeline { get; init; }
    public IMotelySeedScoreDesc? ScoreProviderDesc { get; init; }

    // Metadata for UI/Display (still stores the typed clauses for inspection)
    public required IReadOnlyList<MotelyJsonFilterClause> Must { get; init; }
    public required IReadOnlyList<MotelyJsonFilterClause> MustNot { get; init; }
    public required IReadOnlyList<MotelyJsonFilterClause> Should { get; init; }

    // Reporting & Database schema
    public required IReadOnlyList<IColumnDefinition> Columns { get; init; }

    // Validation limits
    public required int MaxVoucherAnte { get; init; }
    public required int MaxBossAnte { get; init; }

    /// <summary>
    /// Factory: Converts a raw MotelyJsonConfig into a clean MotelyRunConfig
    /// </summary>
    public static MotelyRunConfig Factory(MotelyJsonConfig config)
    {
        // 1. Initialize and validate the raw config
        config.PostProcess(); // Initialize and validate

        // 2. Resolve Deck/Stake
        if (!Enum.TryParse<MotelyDeck>(config.Deck, true, out var deck))
            deck = MotelyDeck.Red;

        if (!Enum.TryParse<MotelyStake>(config.Stake, true, out var stake))
            stake = MotelyStake.White;

        // 3. Derive Columns
        var columns = ColumnDefinitionHelper.CreateFromShouldClauses(config);

        // 4. Build "Cooked" Filters and Score Provider
        IMotelySeedFilterDesc? pipeline = null;
        IMotelySeedScoreDesc? scoreDesc = null;

        try
        {
            // Build optimized filter pipeline
            if (config.Must != null && config.Must.Count > 0)
            {
                // Create the entire pipeline of filters (primary + chained)
                // This invokes the AVX2/AVX512 specialized implementations
                // Returns IMotelySearchSettings (type-safe interface)
                var pipelineSettings = SpecializedFilterFactory.CreateJsonFilterPipeline(
                    config.Must,
                    Environment.ProcessorCount,
                    1024 // Default batch size
                );

                // Extract the filter description from the settings using the interface property
                pipeline = pipelineSettings.BaseFilterDescBase;
            }

            // Build Score Provider
            if (config.Should != null && config.Should.Count > 0)
            {
                // Default setup: no cutoff, console reporting (to be overridden by executors)
                scoreDesc = new MotelyJsonSeedScoreDesc(
                    config,
                    0,
                    ScoreCutoffMode.None,
                    (_) => { }
                );
            }
        }
        catch (Exception ex)
        {
            // Log but don't crash - allow fallback to slow path if pipeline creation fails
            Console.Error.WriteLine(
                $"[RunConfig] Warning: Failed to optimize filter pipeline: {ex.Message}"
            );
        }

        // 5. Create the run config
        return new MotelyRunConfig
        {
            Name = config.Name ?? "Unnamed Search",
            Deck = deck,
            Stake = stake,
            FilterPipeline = pipeline,
            ScoreProviderDesc = scoreDesc,
            Must = MotelyJsonFilterClause.ConvertAll(config.Must),
            MustNot = MotelyJsonFilterClause.ConvertAll(config.MustNot),
            Should = MotelyJsonFilterClause.ConvertAll(config.Should),
            Columns = columns.AsReadOnly(),
            MaxVoucherAnte = 8,
            MaxBossAnte = 8,
            StartSeed = config.StartSeed,
        };
    }

    public string? StartSeed { get; init; }
}
