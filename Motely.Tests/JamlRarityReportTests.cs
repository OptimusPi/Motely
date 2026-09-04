namespace Motely.Tests;

/// <summary>
/// Pins the notation and arithmetic laws in <see cref="JamlRarityReport"/>: magnitudes stay
/// readable past a trillion, durations past a day report days rather than the hours-within-day
/// component, the at-least-one odds survive probabilities small enough to round <c>1 - p</c> to
/// one, and — the load-bearing one — no unusable probability ever reaches the rendered block as a
/// digit. A pre-search estimate that lies is worse than no estimate, so these tests are about
/// what the report refuses to say.
/// </summary>
public sealed class JamlRarityReportTests(Xunit.Abstractions.ITestOutputHelper output)
{
    private const long FullSpace = JamlRarityReport.FullSequentialSeedSpace;

    /// <summary>
    /// Prints every shape of the block. Not an assertion — a place to read the words as a person
    /// would, because the failure mode this feature guards against is prose that is technically
    /// true and useless.
    /// </summary>
    [Fact]
    public void Sample_EveryShapeOfTheBlock()
    {
        void Show(string title, IReadOnlyList<string> lines)
        {
            output.WriteLine($"── {title} " + new string('─', Math.Max(0, 58 - title.Length)));
            foreach (string line in lines)
                output.WriteLine(line);
            output.WriteLine("");
        }

        Show("findable, fully modeled", Render(Complete(1.0 / 165_000_000), FullSweep));
        Show("hopeless", Render(Complete(1.0 / 40_200_000_000_000d), FullSweep));
        Show("partial coverage", Render(Partial(1.0 / 165_000_000), FullSweep));
        Show("nothing modeled yet", Render(Empty(), FullSweep));
        Show("impossible", Render(new JamlRarityEstimate(0.0, 3, 0, [], false), FullSweep));
        Show("cold machine, no clock", Render(Complete(1.0 / 165_000_000), FullSweep, speed: null));
        Show(
            "narrowed space",
            Render(Complete(1.0 / 165_000_000), new JamlSearchSpace(15_006_250_000, "batches 0–9,999"))
        );
        Show("collect 5", Render(Complete(1.0 / 165_000_000), FullSweep, collect: 5));
    }

    private static JamlRarityEstimate Complete(double p) => new(p, 3, 0, [], false);

    private static JamlRarityEstimate Partial(double p) => new(p, 2, 3, ["erraticRank", "pokerHand"], false);

    private static JamlRarityEstimate Empty() => new(double.NaN, 0, 2, ["erraticRank", "pokerHand"], false);

    private static IReadOnlyList<string> Render(
        JamlRarityEstimate rarity,
        JamlSearchSpace space,
        double? speed = 1.37e7,
        long collect = 0
    ) => JamlRarityReport.Render(rarity, space, 11.375, 91, collect);

    private static JamlSearchSpace FullSweep => new(FullSpace, "full sequential sweep");

    // ── Humanize ───────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0, "0")]
    [InlineData(999, "999")]
    [InlineData(1500, "1.5K")]
    [InlineData(1.65e8, "165M")]
    [InlineData(2.251875390625e12, "2.25T")]
    [InlineData(4.02e13, "40.2T")]
    public void Humanize_ReadableMagnitudes(double value, string expected)
    {
        Assert.Equal(expected, JamlRarityReport.Humanize(value));
    }

    /// <summary>
    /// The bug this replaces stopped at B, so a 1-in-40-trillion filter rendered as "40200B".
    /// Anything past a trillion must carry a T (or scientific), never a four-digit B.
    /// </summary>
    [Fact]
    public void Humanize_PastTrillion_DoesNotOverflowIntoBillions()
    {
        string rendered = JamlRarityReport.Humanize(4.02e13);
        Assert.EndsWith("T", rendered);
        Assert.DoesNotContain("B", rendered);
        Assert.StartsWith("1e+", JamlRarityReport.Humanize(1e15));
    }

    [Fact]
    public void Humanize_NonFinite_SaysUnknown()
    {
        Assert.Equal("unknown", JamlRarityReport.Humanize(double.NaN));
        Assert.Equal("unknown", JamlRarityReport.Humanize(double.PositiveInfinity));
    }

    // ── Duration ───────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0.4, "400 ms")]
    [InlineData(11538, "3h 12m")]
    public void Duration_ReadableUnits(double seconds, string expected)
    {
        Assert.Equal(expected, JamlRarityReport.Duration(seconds));
    }

    /// <summary>
    /// The regression this exists for: <c>TimeSpan.ToString(@"hh\:mm\:ss")</c> renders the
    /// hours-<em>within-day</em> component, so 33 days 23 hours silently printed as "23:00:00" —
    /// a wrong number that looks entirely plausible and understates by a factor of 34.
    /// </summary>
    [Fact]
    public void Duration_PastADay_ReportsDaysNotHoursWithinDay()
    {
        string rendered = JamlRarityReport.Duration(2_934_000);
        Assert.Equal("33d 23h", rendered);
        Assert.NotEqual("23:00:00", rendered);
    }

    [Fact]
    public void Duration_NonFinite_SaysEffectivelyNever()
    {
        Assert.Equal("effectively never", JamlRarityReport.Duration(double.PositiveInfinity));
        Assert.Equal("effectively never", JamlRarityReport.Duration(double.NaN));
        Assert.Equal("effectively never", JamlRarityReport.Duration(-1));
    }

    // ── odds arithmetic ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void AtLeastOne_MatchesThePoissonTail()
    {
        // lambda = 2.2519e12 * 1e-13 = 0.22519 -> 1 - e^-lambda = 0.20164
        Assert.Equal(0.20164, JamlRarityReport.AtLeastOneProbability(1e-13, FullSpace), 4);
    }

    /// <summary>
    /// The cancellation trap. For p below about 1e-16, <c>1 - p</c> is exactly 1.0 and
    /// <c>Math.Log(1 - p)</c> returns 0 — so the naive form reports a rare-but-possible filter as
    /// flatly impossible. That is the case this line is printed for, so it must not be the case it
    /// gets wrong.
    /// </summary>
    [Fact]
    public void AtLeastOne_SurvivesProbabilitiesThatRoundOneMinusPToOne()
    {
        double odds = JamlRarityReport.AtLeastOneProbability(1e-20, FullSpace);
        Assert.True(odds > 0, "a rare filter must not be reported as impossible");
        Assert.Equal(FullSpace * 1e-20, odds, 12);
    }

    [Fact]
    public void AtLeastOne_SaturatesRatherThanRounding()
    {
        Assert.Equal(1.0, JamlRarityReport.AtLeastOneProbability(6.06e-9, FullSpace));
    }

    [Fact]
    public void AtLeastOne_UnusableInputs_AreNaN()
    {
        Assert.True(double.IsNaN(JamlRarityReport.AtLeastOneProbability(double.NaN, FullSpace)));
        Assert.True(double.IsNaN(JamlRarityReport.AtLeastOneProbability(0, FullSpace)));
        Assert.True(double.IsNaN(JamlRarityReport.AtLeastOneProbability(1e-9, 0)));
    }

    /// <summary>Rarer must always mean slower and less likely — catches sign and inversion errors.</summary>
    [Fact]
    public void RarerIsAlwaysSlowerAndLessLikely()
    {
        // Below the lambda >= 40 saturation point, or the odds are all 1.0 and prove nothing.
        double[] ps = [1e-12, 1e-13, 1e-14, 1e-15];
        for (int i = 1; i < ps.Length; i++)
        {
            Assert.True(
                JamlRarityReport.SecondsToFirstMatch(ps[i], 1e7)
                    > JamlRarityReport.SecondsToFirstMatch(ps[i - 1], 1e7)
            );
            Assert.True(
                JamlRarityReport.AtLeastOneProbability(ps[i], FullSpace)
                    < JamlRarityReport.AtLeastOneProbability(ps[i - 1], FullSpace)
            );
        }
    }

    // ── the block ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Complete_ReadsAsAnEstimate()
    {
        var lines = Render(Complete(1.0 / 165_000_000), FullSweep);
        Assert.Contains(lines, l => l.Contains("Rare:") && l.Contains("~1 in 165M") && l.Contains("3/3"));
        Assert.DoesNotContain(lines, l => l.Contains("rarer than"));
        Assert.DoesNotContain(lines, l => l.Contains("Odds:") && l.Contains("at most"));
        Assert.DoesNotContain(lines, l => l.Contains("Find:") && l.Contains("at least "));
    }


    [Fact]
    public void Empty_PrintsNoDerivedNumbersAtAll()
    {
        var lines = Render(Empty(), FullSweep);
        Assert.Contains(lines, l => l.Contains("Rare:") && l.Contains("unknown"));
        Assert.DoesNotContain(lines, l => l.Contains("Odds:"));
        Assert.DoesNotContain(lines, l => l.Contains("Find:"));
        Assert.DoesNotContain(lines, l => l.Contains("Note:"));
    }

    /// <summary>Provably impossible is its own answer — not "unknown", and not "1 in 1e308".</summary>
    [Fact]
    public void Impossible_IsDistinctFromUnknown()
    {
        var lines = Render(new JamlRarityEstimate(0.0, 3, 0, [], false), FullSweep);
        Assert.Contains(lines, l => l.Contains("Rare:") && l.Contains("impossible"));
        Assert.DoesNotContain(lines, l => l.Contains("Odds:"));
    }



    [Fact]
    public void ViableFilter_DoesNotWarn()
    {
        var lines = Render(Complete(1.0 / 165_000_000), FullSweep);
        Assert.DoesNotContain(lines, l => l.Contains("Note:"));
    }



    // ── the sanitation matrix ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// The hostile cross-product. A NaN share arriving alongside a non-zero clause count is a bug
    /// in the model, and the report must absorb it as "unknown" rather than rendering "1 in NaN".
    /// No rendered line may ever carry a non-finite token or a negative quantity, under any input.
    /// </summary>
    [Fact]
    public void NoInput_EverRendersANonFiniteOrNegativeNumber()
    {
        double[] shares = [double.NaN, double.PositiveInfinity, double.NegativeInfinity, 0, -1, 2, double.Epsilon, 1.0];
        long[] spaces = [-1, 0, 1, FullSpace, long.MaxValue];
        double?[] speeds = [null, 0, double.NaN, double.PositiveInfinity, 1.37e7];
        JamlRarityEstimate[] shapes =
        [
            new(0, 3, 0, [], false),
            new(0, 2, 3, ["erraticRank"], false),
            new(0, 0, 2, ["erraticRank"], false),
        ];

        foreach (double share in shares)
        foreach (long spaceCount in spaces)
        foreach (double? speed in speeds)
        foreach (var shape in shapes)
        {
            var estimate = shape with { PassShare = share };
            var lines = JamlRarityReport.Render(
                estimate,
                new JamlSearchSpace(spaceCount, "test space"),
                11.375,
                91,
                0
            );

            foreach (string line in lines)
            {
                Assert.DoesNotContain("NaN", line);
                Assert.DoesNotContain("Infinity", line);
                Assert.DoesNotContain("∞", line);
                Assert.DoesNotContain("E+", line);
                Assert.DoesNotMatch(@"-\d", line);
            }

            // Nothing derived from an unusable share may appear, whatever the other inputs are.
            if (!JamlRarityReport.IsUsableProbability(share) || estimate.IsEmpty)
            {
                Assert.DoesNotContain(lines, l => l.Contains("Odds:"));
                Assert.DoesNotContain(lines, l => l.Contains("Find:"));
                Assert.DoesNotContain(lines, l => l.Contains("Note:"));
            }
        }
    }

    /// <summary>Pins house style: two-space indent, value at column 9, same as PrintSummary.</summary>
    [Fact]
    public void EveryLabelledLine_AlignsToColumnNine()
    {
        var lines = Render(Complete(1.0 / 165_000_000), FullSweep);
        foreach (string line in lines)
        {
            Assert.StartsWith("  ", line);
            Assert.Matches(@"^  \S{1,6} +\S", line);
            Assert.Equal(' ', line[8]);
            Assert.NotEqual(' ', line[9]);
        }
    }
}
