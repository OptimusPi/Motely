namespace Motely.Tests;

public class JamlAestheticsPalindromeTests
{
    [Fact]
    public void PalindromeSeedProvider_Uses_Same_Count_As_JamlAesthetics()
    {
        var provider = new MotelyPalindromeSeedProvider();
        Assert.Equal(JamlAesthetics.GetSeedCount(JamlAesthetic.Palindrome), provider.SeedCount);
    }

    [Fact]
    public void AestheticSeedProvider_Uses_Same_Count_As_JamlAesthetics()
    {
        var provider = new MotelyAestheticSeedProvider(JamlAesthetic.Gross);
        Assert.Equal(JamlAesthetics.GetSeedCount(JamlAesthetic.Gross), provider.SeedCount);
    }
}
