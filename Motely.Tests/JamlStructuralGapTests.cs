using Motely.Filters;
using Xunit;

namespace Motely.Tests;

/// <summary>
/// Gaps vs <see cref="JamlClauseCoverageTests"/>: combinators, extinct events, soulCardOnly.
/// All JAML is inline (CI-safe — no disk reads). Does not use MotelyLegacyTextAnalyzer.
/// <para>
/// <c>TODOPASS</c> / <c>TODOFAIL</c> are placeholders. We only assert match counts when logic forces them
/// (contradictory must/mustNot). For everything else, replace TODOPASS with a verified seed and then
/// assert <see cref="IMotelySearch.MatchingSeeds"/>.
/// </para>
/// </summary>
public sealed class JamlStructuralGapTests
{
    /// <summary>Swap for a seed known to satisfy your filter when you want a positive match assertion.</summary>
    private const string PassSeed = "TODOPASS";

    /// <summary>Used with filters that should not match (or with impossible filters — any seed yields 0).</summary>
    private const string FailSeed = "TODOFAIL";

    private static (long SeedsSearched, long MatchingSeeds) RunListSearch(string jaml, string seed)
    {
        Assert.True(JamlConfigLoader.TryLoad(jaml, out var config, out var error), error);
        var settings = JamlSearchBuilder
            .CreateSettings(config!)
            .WithListSearch([seed], 1)
            .WithThreadCount(1)
            .WithQuietMode(true);
        using var search = settings.Start();
        search.AwaitCompletion();
        return (search.TotalSeedsSearched, search.MatchingSeeds);
    }

    [Fact]
    public void Must_and_mustNot_same_clause_is_unsatisfiable_zero_matches_TODOFAIL()
    {
        var jaml = """
            id: structural-gap-impossible
            deck: Red
            stake: White
            must:
              - voucher: Telescope
                antes: [1]
            mustNot:
              - voucher: Telescope
                antes: [1]
            """;
        var r = RunListSearch(jaml, FailSeed);
        Assert.Equal(1, r.SeedsSearched);
        Assert.Equal(0, r.MatchingSeeds);
    }

    /// <summary>
    /// Shop slot 0 is always Buffoon in <see cref="Motely.Filters.LegendarySoulMatcher"/> — soul/legendary
    /// only appears from arcana/spectralCard, so a clause targeting only ante-1-slot-0 has zero possible matches.
    /// <para>
    /// Pifreak rule (2026-04-26): never block users on inferred mistakes. The validator was demoted from
    /// throw-at-plan-time to a no-op; the search runs and returns zero, which is information enough.
    /// Future work: surface this as a structured warning via TryLoad return shape so the CLI can print it
    /// in red and the WASM bridge can hand it to JS, without library code touching <c>Console</c>.
    /// </para>
    /// </summary>
    [Fact]
    public void Soul_joker_booster_slot_zero_only_loads_and_plans_without_throwing()
    {
        var jaml = """
            id: structural-gap-buffoon-slot0
            deck: Red
            stake: White
            must:
              - legendaryJoker: Perkeo
                antes: [1]
                sources:
                  boosterPacks: [0]
            """;
        Assert.True(JamlConfigLoader.TryLoad(jaml, out var config, out var err), err);
        // Was: Assert.Throws<InvalidOperationException>(...). New design: trust the user, plan succeeds,
        // search returns zero matches at runtime. No exception expected.
        var plan = JamlSearchBuilder.CreatePlan(config!);
        Assert.NotNull(plan.Settings);
    }

    /// <summary>
    /// <c>mustNot</c> with a dead soul path is still loadable (negation is vacuously true); we do not block it.
    /// </summary>
    [Fact]
    public void Soul_joker_booster_slot_zero_only_in_mustNot_CreatePlan_succeeds()
    {
        var jaml = """
            id: structural-gap-buffoon-mustnot
            deck: Red
            stake: White
            must:
              - voucher: Telescope
                antes: [1]
            mustNot:
              - legendaryJoker: Perkeo
                antes: [1]
                sources:
                  boosterPacks: [0]
            """;
        Assert.True(JamlConfigLoader.TryLoad(jaml, out var config, out var err), err);
        var plan = JamlSearchBuilder.CreatePlan(config!);
        Assert.NotNull(plan.Settings);
    }

    [Fact]
    public void Must_and_mustNot_same_clause_is_unsatisfiable_zero_matches_TODOPASS()
    {
        var jaml = """
            id: structural-gap-impossible-b
            deck: Red
            stake: White
            must:
              - voucher: Telescope
                antes: [1]
            mustNot:
              - voucher: Telescope
                antes: [1]
            """;
        var r = RunListSearch(jaml, PassSeed);
        Assert.Equal(1, r.SeedsSearched);
        Assert.Equal(0, r.MatchingSeeds);
    }

    [Fact]
    public void And_combinator_loads_and_executes_list_search_TODOPASS()
    {
        var jaml = """
            id: structural-gap-and
            deck: Red
            stake: White
            must:
              - and:
                - voucher: Telescope
                  antes: [1]
                - voucher: Telescope
                  antes: [1]
            """;
        var r = RunListSearch(jaml, PassSeed);
        Assert.Equal(1, r.SeedsSearched);
    }

    [Fact]
    public void Or_combinator_loads_and_executes_list_search_TODOPASS()
    {
        var jaml = """
            id: structural-gap-or
            deck: Red
            stake: White
            must:
              - or:
                - voucher: Telescope
                  antes: [1]
                - voucher: Telescope
                  antes: [1]
            """;
        var r = RunListSearch(jaml, PassSeed);
        Assert.Equal(1, r.SeedsSearched);
    }

    [Fact]
    public void Event_CavendishExtinct_loads_and_executes_list_search_TODOPASS()
    {
        var jaml = """
            id: structural-gap-cavendish
            deck: Red
            stake: White
            must:
              - event: CavendishExtinct
                rolls: [0]
            """;
        var r = RunListSearch(jaml, PassSeed);
        Assert.Equal(1, r.SeedsSearched);
    }

    [Fact]
    public void Event_GrosMichelExtinct_loads_and_executes_list_search_TODOPASS()
    {
        var jaml = """
            id: structural-gap-gros
            deck: Red
            stake: White
            must:
              - event: GrosMichelExtinct
                rolls: [0]
            """;
        var r = RunListSearch(jaml, PassSeed);
        Assert.Equal(1, r.SeedsSearched);
    }

    [Fact]
    public void LegendaryJoker_soulCardOnly_loads_and_executes_list_search_TODOPASS()
    {
        var jaml = """
            id: structural-gap-soulcardonly
            deck: Red
            stake: White
            must:
              - legendaryJoker: Perkeo
                soulCardOnly: true
                antes: [1]
                sources:
                  arcanaPacks: [1]
            """;
        var r = RunListSearch(jaml, PassSeed);
        Assert.Equal(1, r.SeedsSearched);
    }

    [Fact]
    public void CreatePlan_succeeds_for_gap_clauses_without_starting_search()
    {
        string[] jams =
        [
            """
                id: plan-and
                deck: Red
                stake: White
                must:
                  - and:
                    - boss: TheHook
                      antes: [1]
                    - boss: TheHook
                      antes: [1]
                """,
            """
                id: plan-or
                deck: Red
                stake: White
                must:
                  - or:
                    - event: LuckyMoney
                      rolls: [0]
                    - event: LuckyMult
                      rolls: [0]
                """,
        ];

        foreach (var jaml in jams)
        {
            Assert.True(JamlConfigLoader.TryLoad(jaml, out var config, out var err), err);
            var plan = JamlSearchBuilder.CreatePlan(config!);
            Assert.True(plan.ScoreTallyColumnCount >= 0);
            _ = plan.Settings;
        }
    }
}
