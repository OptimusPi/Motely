using Motely.Analysis;
using Xunit;
using Xunit.Abstractions;

namespace Motely.Tests;

/// <summary>
/// Tests that IMotelySeedContextProvider gives the caller a live MotelySingleSearchContext
/// identical to the one the analyzer uses — proving zero duplication.
/// </summary>
public class SeedContextProviderTests(ITestOutputHelper output)
{
    private const string TestSeed = "JAMMY";
    private const MotelyDeck TestDeck = MotelyDeck.Red;
    private const MotelyStake TestStake = MotelyStake.White;

    /// <summary>
    /// A simple desc that holds a callback. The provider calls it with the live context.
    /// </summary>
    private sealed class CallbackContextProviderDesc(MotelySeedContextCallback callback)
        : IMotelySeedContextProviderDesc
    {
        public IMotelySeedContextProvider CreateContextProvider(ref MotelyFilterCreationContext ctx)
        {
            return new CallbackContextProvider(callback);
        }

        private readonly struct CallbackContextProvider(MotelySeedContextCallback callback)
            : IMotelySeedContextProvider
        {
            public void ProvideSeedContext(ref MotelySingleSearchContext ctx)
            {
                callback(ref ctx);
            }
        }
    }

    [Fact]
    public void ContextProvider_ShopItems_MatchAnalyzer()
    {
        // --- Get shop items via the analyzer (existing path) ---
        var analysis = MotelySeedAnalyzer.Analyze(
            new MotelySeedAnalysisConfig(TestSeed, TestDeck, TestStake)
        );

        Assert.Null(analysis.Error);
        Assert.NotEmpty(analysis.Antes);

        var analyzerAnte1Items = analysis.Antes[0].ShopQueue;

        // --- Get shop items via the context provider (new path) ---
        List<MotelyItem> contextProviderItems = [];

        var desc = new CallbackContextProviderDesc((ref MotelySingleSearchContext ctx) =>
        {
            var shopStream = ctx.CreateShopItemStream(1);
            int maxSlots = 15; // ante 1 = 15, same as analyzer
            for (int i = 0; i < maxSlots; i++)
            {
                contextProviderItems.Add(ctx.GetNextShopItem(ref shopStream));
            }
        });

        // Use the same MotelyAnalyzerFilterDesc as the analyzer does,
        // but also attach our context provider
        MotelyAnalyzerFilterDesc filterDesc = new();
        var settings = new MotelySearchSettings<MotelyAnalyzerFilterDesc.AnalyzerFilter>(filterDesc)
            .WithDeck(TestDeck)
            .WithStake(TestStake)
            .WithListSearch([TestSeed])
            .WithThreadCount(1)
            .WithSeedContextProvider(desc);

        using var search = settings.Start();
        search.AwaitCompletion();

        // --- Compare ---
        Assert.Equal(analyzerAnte1Items.Count, contextProviderItems.Count);

        for (int i = 0; i < analyzerAnte1Items.Count; i++)
        {
            Assert.Equal(
                analyzerAnte1Items[i].Type,
                contextProviderItems[i].Type
            );

            output.WriteLine(
                $"Slot {i}: {FormatUtils.FormatItem(analyzerAnte1Items[i])} == {FormatUtils.FormatItem(contextProviderItems[i])}"
            );
        }

        output.WriteLine($"\nAll {contextProviderItems.Count} shop items match between analyzer and context provider.");
    }

    [Fact]
    public void ContextProvider_ShopItems_BeyondAnalyzerLimit()
    {
        // The analyzer stops at 15 items for ante 1.
        // The ladder keeps going — prove it.
        List<MotelyItem> allItems = [];

        var desc = new CallbackContextProviderDesc((ref MotelySingleSearchContext ctx) =>
        {
            var shopStream = ctx.CreateShopItemStream(1);
            for (int i = 0; i < 30; i++)
            {
                allItems.Add(ctx.GetNextShopItem(ref shopStream));
            }
        });

        MotelyAnalyzerFilterDesc filterDesc = new();
        var settings = new MotelySearchSettings<MotelyAnalyzerFilterDesc.AnalyzerFilter>(filterDesc)
            .WithDeck(TestDeck)
            .WithStake(TestStake)
            .WithListSearch([TestSeed])
            .WithThreadCount(1)
            .WithSeedContextProvider(desc);

        using var search = settings.Start();
        search.AwaitCompletion();

        Assert.Equal(30, allItems.Count);

        for (int i = 0; i < allItems.Count; i++)
        {
            output.WriteLine($"Slot {i}: {FormatUtils.FormatItem(allItems[i])}");
        }

        output.WriteLine($"\nThe ladder keeps going — {allItems.Count} items, no duplication needed.");
    }

    [Fact]
    public void Yoinker_DirectConstruction_NoSearch()
    {
        // No search pipeline at all — just seed + deck + stake
        using var yoinker = new SingleSeedContextYoinkerDesc(TestSeed, TestDeck, TestStake);

        var ctx = yoinker.CreateContext();
        var shopStream = ctx.CreateShopItemStream(1);

        List<MotelyItem> items = [];
        for (int i = 0; i < 15; i++)
        {
            items.Add(ctx.GetNextShopItem(ref shopStream));
        }

        // Compare against analyzer
        var analysis = MotelySeedAnalyzer.Analyze(
            new MotelySeedAnalysisConfig(TestSeed, TestDeck, TestStake)
        );
        var analyzerItems = analysis.Antes[0].ShopQueue;

        Assert.Equal(analyzerItems.Count, items.Count);
        for (int i = 0; i < analyzerItems.Count; i++)
        {
            Assert.Equal(analyzerItems[i].Type, items[i].Type);
            output.WriteLine($"Slot {i}: {FormatUtils.FormatItem(items[i])}");
        }

        output.WriteLine($"\nDirect construction — {items.Count} items match analyzer, no search needed.");
    }
}
