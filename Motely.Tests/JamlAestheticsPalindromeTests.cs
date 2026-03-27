using Motely.Filters;
using Xunit;

namespace Motely.Tests;

public class JamlAestheticsPalindromeTests
{
    /// <summary>
    /// Palindrome search walks every palindrome over <see cref="Motely.SeedDigits"/> for lengths 1..8 in a fixed order.
    /// </summary>
    [Fact]
    public void EnumerateSeeds_EverySeed_Matches_And_Count_Agrees_With_GetSeedCount()
    {
        var aesthetic = JamlAesthetic.Palindrome;
        int expected = JamlAesthetics.GetSeedCount(aesthetic);
        int n = 0;

        foreach (var seed in JamlAesthetics.EnumerateSeeds(aesthetic))
        {
            Assert.True(
                JamlAesthetics.Matches(aesthetic, seed),
                $"Enumerated seed should match: {seed}"
            );
            n++;
        }

        Assert.Equal(expected, n);
    }

    [Theory]
    [InlineData("ALEEB", false)]
    [InlineData("TACOCAT", true)]
    [InlineData("1", true)]
    [InlineData("12", false)]
    [InlineData("121", true)]
    public void Palindrome_Matches_MotelyRules(string seed, bool expected) =>
        Assert.Equal(expected, JamlAesthetics.Matches(JamlAesthetic.Palindrome, seed));

    [Fact]
    public void CollectMatches_Tacocat_Includes_Palindrome()
    {
        var list = new List<JamlAesthetic>();
        JamlAesthetics.CollectMatches("TACOCAT", list);
        Assert.Single(list);
        Assert.Equal(JamlAesthetic.Palindrome, list[0]);
    }

    [Fact]
    public void PalindromeSeedProvider_Uses_Same_Count_As_JamlAesthetics()
    {
        var provider = new MotelyPalindromeSeedProvider();
        Assert.Equal(JamlAesthetics.GetSeedCount(JamlAesthetic.Palindrome), provider.SeedCount);
    }
}
