using System;
using System.Collections.Generic;
using Motely.Reporting;

namespace Motely.Filters;

/// <summary>
/// Seed score descriptor that operates on <see cref="MotelyRunConfig"/> (typed, non-nullable).
/// This is a thin wrapper around the existing MotelyJson* scoring logic while migration is in progress.
/// Once all scoring utilities are ported to MotelyRunClause we will delete the old json-based classes.
/// </summary>
public struct MotelyRunSeedScoreDesc(
    MotelyRunConfig Config,
    int Cutoff,
    ScoreCutoffMode Mode,
    Action<MotelySeedScoreTally> OnResultFound
) : IMotelySeedScoreDesc<MotelyRunSeedScoreDesc.MotelyRunSeedScoreProvider>
{
    private readonly Action<MotelySeedScoreTally> _onResultFound = OnResultFound;

    public MotelyRunSeedScoreProvider CreateScoreProvider(ref MotelyFilterCreationContext ctx)
    {
        // For now, delegate to the old provider by converting back to MotelyJsonConfig.
        // TODO: Replace with native MotelyRun scoring implementation (Phase2).
        var jsonDto = Config.ToJson(); // round-trip for compatibility
        var jsonConfig = System.Text.Json.JsonSerializer.Deserialize<MotelyJsonConfig>(jsonDto);
        jsonConfig!.PostProcess();

        var legacyDesc = new MotelyJsonSeedScoreDesc(jsonConfig, Cutoff, Mode, _onResultFound);
        return new MotelyRunSeedScoreProvider(legacyDesc.CreateScoreProvider(ref ctx));
    }

    /// <summary>
    /// Adapter provider that simply forwards to the legacy provider until scoring is migrated.
    /// </summary>
    public readonly struct MotelyRunSeedScoreProvider(IMotelySeedScoreProvider Legacy)
        : IMotelySeedScoreProvider
    {
        public VectorMask Score(
            ref MotelyVectorSearchContext searchContext,
            MotelySeedScoreTally[] buffer,
            VectorMask baseFilterMask = default,
            int scoreThreshold = 0
        ) => Legacy.Score(ref searchContext, buffer, baseFilterMask, scoreThreshold);
    }
}
