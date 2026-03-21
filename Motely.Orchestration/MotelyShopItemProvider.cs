using System.Text.Json;
using Motely;
using Motely.Analysis;

namespace Motely.Executors;

/// <summary>
/// Stateless infinite shop item provider. Each call re-runs the PRNG pipeline
/// from scratch, skips <paramref name="offset"/> items, collects <paramref name="count"/>.
/// Deterministic: same (seed, deck, stake, ante, offset) always yields the same items.
/// </summary>
public static class MotelyShopItemProvider
{
    public static string GetShopItems(
        string seed, string deck, string stake,
        int ante, int offset, int count)
    {
        if (!Enum.TryParse<MotelyDeck>(deck, true, out var deckEnum))
            throw new ArgumentException($"Unknown deck: '{deck}'", nameof(deck));
        if (!Enum.TryParse<MotelyStake>(stake, true, out var stakeEnum))
            throw new ArgumentException($"Unknown stake: '{stake}'", nameof(stake));

        if (ante < 1 || ante > 8)
            throw new ArgumentOutOfRangeException(nameof(ante), "Ante must be 1-8.");
        if (offset < 0)
            throw new ArgumentOutOfRangeException(nameof(offset), "Offset must be >= 0.");
        if (count < 1 || count > 1000)
            throw new ArgumentOutOfRangeException(nameof(count), "Count must be 1-1000.");

        var filterDesc = new ShopItemFilterDesc(ante, offset, count);

        var settings = new MotelySearchSettings<ShopItemFilterDesc.ShopItemFilter>(filterDesc)
            .WithDeck(deckEnum)
            .WithStake(stakeEnum)
            .WithListSearch([seed])
            .WithThreadCount(1);

        using var search = settings.Start();
        search.AwaitCompletion();

        return filterDesc.ResultJson ?? "[]";
    }

    private sealed class ShopItemFilterDesc(int ante, int offset, int count)
        : IMotelySeedFilterDesc<ShopItemFilterDesc.ShopItemFilter>
    {
        public string? ResultJson { get; private set; }

        public ShopItemFilter CreateFilter(ref MotelyFilterCreationContext ctx) =>
            new(this, ante, offset, count);

        public readonly struct ShopItemFilter(
            ShopItemFilterDesc desc, int ante, int offset, int count
        ) : IMotelySeedFilter
        {
            public VectorMask Filter(ref MotelyVectorSearchContext ctx)
            {
                // Copy primary constructor params to locals — lambdas can't capture struct params
                var localDesc = desc;
                int localAnte = ante;
                int localOffset = offset;
                int localCount = count;

                return ctx.SearchIndividualSeeds(
                    (ref MotelySingleSearchContext singleCtx) =>
                    {
                        var stream = singleCtx.CreateShopItemStream(localAnte);

                        // Skip offset items (deterministic — same skip always produces same sequence)
                        for (int i = 0; i < localOffset; i++)
                            singleCtx.GetNextShopItem(ref stream);

                        // Collect count items
                        var items = new ShopItemDto[localCount];
                        for (int i = 0; i < localCount; i++)
                        {
                            var item = singleCtx.GetNextShopItem(ref stream);
                            items[i] = new ShopItemDto
                            {
                                Id = item.Type.ToString(),
                                Name = FormatUtils.FormatItem(item),
                            };
                        }

                        localDesc.ResultJson = JsonSerializer.Serialize(items);
                        return false; // Not filtering — just collecting
                    }
                );
            }
        }
    }

    public sealed class ShopItemDto
    {
        public string Id { get; init; } = "";
        public string Name { get; init; } = "";
    }
}
