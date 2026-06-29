#!/usr/bin/dotnet run
#:project ./Motely/Motely.csproj

// Single-file .NET 10 app. Run:  dotnet run find-claude22.cs
// Jimmolate finds CLAUDE22 among decoys by DERIVING its ante-1 voucher (Paint Brush),
// not by reading its name. The predicate runs native, in-engine, per surviving seed.

using Motely;
using Motely.Enums;
using Motely.Filters.Native;

string[] seeds = ["DECOY111", "CLAUDE22", "DECOY222"];
var matched = new List<string>();

var settings = new MotelySearchSettings<PassthroughFilterDesc.PassthroughFilter>(
        new PassthroughFilterDesc())
    .WithDeck(MotelyDeck.Erratic)
    .WithStake(MotelyStake.White)
    .WithListSearch(seeds, seeds.Length)
    .WithThreadCount(1)
    .WithQuietMode(true)
    // The Immolate predicate: keep the seed iff its ante-1 first voucher is Paint Brush.
    .WithJimmolate((ref MotelySingleSearchContext ctx) =>
        ctx.GetAnteFirstVoucher(1) == MotelyVoucher.PaintBrush)
    .WithSeedMatchCallback(matched.Add);

using var search = settings.Start();
search.AwaitCompletion();

Console.WriteLine($"matched: {string.Join(", ", matched)}");
Console.WriteLine(matched is ["CLAUDE22"] ? "DING — found CLAUDE22 by derivation." : "no match");
