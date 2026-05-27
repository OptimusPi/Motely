using Motely.Filters;
using Xunit;

namespace Motely.Tests;

/// <summary>
/// Proves the Jimmolate bridge: SIMD/base narrowing produces survivor batches, then
/// <see cref="JimmolateFilterDesc"/> runs procedural logic only on those lanes via
/// <see cref="MotelyVectorSearchContext.SearchIndividualSeeds(MotelyIndividualSeedSearcher)"/>.
/// </summary>
public sealed class JimmolateFilterDescTests
{
    /// <summary>Native SIMD-style mask: lane passes iff first seed character matches.</summary>
    private readonly struct FirstCharEqualsFilterDesc(char requiredFirst)
        : IMotelySeedFilterDesc<FirstCharEqualsFilterDesc.FirstCharMaskFilter>
    {
        public FirstCharMaskFilter CreateFilter(ref MotelyFilterCreationContext ctx) =>
            new(requiredFirst);

        public readonly struct FirstCharMaskFilter(char requiredFirst) : IMotelySeedFilter
        {
            public readonly VectorMask Filter(ref MotelyVectorSearchContext ctx)
            {
                uint bits = 0;
                for (int lane = 0; lane < MotelyGlobals.MaxVectorWidth; lane++)
                {
                    if (!ctx.IsLaneValid(lane))
                        continue;

                    string seed = ctx.GetSeed(lane);
                    if (seed.Length > 0 && seed[0] == requiredFirst)
                        bits |= 1u << lane;
                }

                return new VectorMask(bits);
            }
        }
    }

    [Fact]
    public void JimmolateRunsOnlyOnBaseSurvivors_AndMatchesControlFilter()
    {
        // One full vector batch: base filter keeps seeds whose first char is 'M'.
        // Jimmolate keeps those whose second char is 'A'. Predicate must run once per base survivor, not per lane.
        string[] seeds =
        [
            "MAAAAAAA",
            "MBBBBBBB",
            "XCCCCCCC",
            "MADDDDDD",
            "XEEEEEEE",
            "MAFFFFFF",
            "XGGGGGGG",
            "MAHHHHHH",
        ];

        var baseSurvivors = new HashSet<string>(
            seeds.Where(static s => s.Length > 0 && s[0] == 'M'),
            StringComparer.Ordinal
        );

        var expectedMatches = new HashSet<string>(
            seeds.Where(static s => s.Length >= 2 && s[0] == 'M' && s[1] == 'A'),
            StringComparer.Ordinal
        );

        var jimmolateVisited = new List<string>();

        var settings = new MotelySearchSettings<FirstCharEqualsFilterDesc.FirstCharMaskFilter>(
            new FirstCharEqualsFilterDesc('M')
        )
            .WithAdditionalFilter(
                new JimmolateFilterDesc(
                    (ref MotelySingleSearchContext ctx) =>
                    {
                        string seed = ctx.GetSeed();
                        jimmolateVisited.Add(seed);
                        return seed.Length >= 2 && seed[1] == 'A';
                    }
                )
            )
            .WithListSearch(seeds, seeds.Length)
            .WithThreadCount(1)
            .WithQuietMode(true);

        var matches = new List<string>();
        settings.SeedMatchCallback = matches.Add;

        using var search = settings.Start();
        search.AwaitCompletion();

        Assert.Equal(seeds.Length, search.TotalSeedsSearched);
        Assert.Equal(expectedMatches.Count, search.MatchingSeeds);

        Assert.Equal(baseSurvivors.Count, jimmolateVisited.Count);
        Assert.Equal(baseSurvivors, jimmolateVisited.ToHashSet(StringComparer.Ordinal));

        Assert.Equal(expectedMatches, matches.ToHashSet(StringComparer.Ordinal));
    }
}
