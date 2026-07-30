using Motely;
using Motely.Enums;
using Motely.Filters.Native;

var seeds = new[] { "1946", "1111", "1234", "1945", "1947" };
var matched = new List<string>();
using var search = new MotelySearchSettings<NegativeLegendaryJokerSimdFilterDesc.FilterStruct>(
    new NegativeLegendaryJokerSimdFilterDesc()
)
    .WithAdditionalFilter(new LegendaryJokerShopSoulFilterDesc())
    .WithDeck(MotelyDeck.Red)
    .WithStake(MotelyStake.White)
    .WithListSearch(seeds, seeds.Length)
    .WithThreadCount(1)
    .WithQuietMode(true)
    .WithSeedMatchCallback(matched.Add)
    .Start();
search.AwaitCompletion();
Console.WriteLine("matched: [" + string.Join(",", matched) + "]");

// also full 1..4000 like the test
matched.Clear();
var all = Enumerable.Range(1, 4000).Select(i => i.ToString()).ToArray();
using var search2 = new MotelySearchSettings<NegativeLegendaryJokerSimdFilterDesc.FilterStruct>(
    new NegativeLegendaryJokerSimdFilterDesc()
)
    .WithAdditionalFilter(new LegendaryJokerShopSoulFilterDesc())
    .WithDeck(MotelyDeck.Red)
    .WithStake(MotelyStake.White)
    .WithListSearch(all, all.Length)
    .WithThreadCount(1)
    .WithQuietMode(true)
    .WithSeedMatchCallback(matched.Add)
    .Start();
search2.AwaitCompletion();
Console.WriteLine("full matched: [" + string.Join(",", matched.OrderBy(s => s, StringComparer.Ordinal)) + "] count=" + matched.Count);
