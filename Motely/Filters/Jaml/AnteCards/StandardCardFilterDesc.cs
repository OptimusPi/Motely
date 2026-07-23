using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Motely.Filters.Jaml;

[JamlDiscriminator("standardCard", "standardCards",
    SourceConfigType = typeof(StandardCardSourceConfig))]
public sealed class StandardCardClause : IJamlClause, IAnteScopedClause
{
    public string? Label { get; set; }
    public int Min { get; set; } = 1;
    public int? Max { get; set; }
    public int Score { get; set; }
    public int[] Antes { get; set; } = [];
    public MotelyStandardcardRank? Rank { get; set; }
    public MotelyStandardcardSuit? Suit { get; set; }
    public MotelyItemEnhancement? Enhancement { get; set; }
    public MotelyItemSeal? Seal { get; set; }
    public MotelyItemEdition? Edition { get; set; }

    // null = no `sources:` block → StandardCardFilterDesc.DefaultSources. Explicit block used verbatim.
    public StandardCardSourceConfig? Sources { get; set; }
}

public struct StandardCardFilterDesc(StandardCardClause clause)
    : IMotelySeedFilterDesc<StandardCardFilterDesc.StandardCardFilter>,
      IJamlClauseDesc<StandardCardClause>
{
    private readonly StandardCardClause _clause = clause;

    /// <inheritdoc/>
    public static string[] Discriminators => ["standardCard", "standardCards"];

    /// <inheritdoc/>
    public static string[] ClauseKeys => ["min", "max", "score", "label", "ante", "antes", "sources", "rank", "suit", "enhancement", "seal", "edition"];

    /// <inheritdoc/>
    public static bool Set(StandardCardClause clause, string key, IJamlValueReader value)
    {
        switch (key.ToLowerInvariant())
        {
            case "rank":
                if (!value.TryEnum<MotelyStandardcardRank>(out var rank)) return false;
                clause.Rank = rank;
                return true;
            case "suit":
                if (!value.TryEnum<MotelyStandardcardSuit>(out var suit)) return false;
                clause.Suit = suit;
                return true;
            case "enhancement":
                if (!value.TryEnum<MotelyItemEnhancement>(out var enh)) return false;
                clause.Enhancement = enh;
                return true;
            case "seal":
                if (!value.TryEnum<MotelyItemSeal>(out var seal)) return false;
                clause.Seal = seal;
                return true;
            case "edition":
                if (!value.TryEnum<MotelyItemEdition>(out var edition)) return false;
                clause.Edition = edition;
                return true;
            default:
                return false;
        }
    }

    /// <summary>Defaults when a clause specifies no <c>sources:</c> block — a normal shop run
    /// (8 shop slots) plus the 6 booster packs. Deferred specialty sources stay off by default.
    /// Applied only when <c>Sources</c> is null; any explicit block overrides wholesale.</summary>
    internal static readonly StandardCardSourceConfig DefaultSources = new()
    {
        ShopItems = [0, 1, 2, 3, 4, 5, 6, 7],
        BoosterPacks = [0, 1, 2, 3, 4, 5],
    };

    public StandardCardFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        var sources = _clause.Sources ?? DefaultSources;

        foreach (var ante in _clause.Antes)
        {
            ctx.CacheShopStream(ante);
            ctx.CacheBoosterPackStream(ante);
        }

        int maxShopItem = 0;
        for (int i = 0; i < sources.ShopItems.Length; i++)
        {
            if (sources.ShopItems[i] > maxShopItem)
                maxShopItem = sources.ShopItems[i];
        }

        int maxBoosterPack = 0;
        for (int i = 0; i < sources.BoosterPacks.Length; i++)
        {
            if (sources.BoosterPacks[i] > maxBoosterPack)
                maxBoosterPack = sources.BoosterPacks[i];
        }

        return new StandardCardFilter(_clause, maxShopItem, maxBoosterPack);
    }

    public struct StandardCardFilter(StandardCardClause clause, int maxShopItem, int maxBoosterPack)
        : IMotelySeedFilter
    {
        private readonly StandardCardClause _clause = clause;
        private readonly int _maxShopItem = maxShopItem;
        private readonly int _maxBoosterPack = maxBoosterPack;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            var clause = _clause;
            int maxShopItem = _maxShopItem;
            int maxBoosterPack = _maxBoosterPack;

            return ctx.SearchIndividualSeeds(
                (MotelySingleSearchContext singleCtx) =>
                {
                    int needed = clause.Min;
                    Debug.Assert(needed > 0, "StandardCardClause.Min must be > 0 — loader bug.");

                    int count = 0;
                    var sources = clause.Sources ?? DefaultSources;
                    var shopItems = sources.ShopItems;
                    var boosterPacks = sources.BoosterPacks;

                    foreach (var ante in clause.Antes)
                    {
                        // ── Shop items ──
                        if (shopItems.Length > 0)
                        {
                            var shopStream = singleCtx.CreateShopItemStream(ante);

                            for (int slot = 0; slot <= maxShopItem; slot++)
                            {
                                var item = singleCtx.GetNextShopItem(ref shopStream);
                                bool isTarget = false;
                                for (int i = 0; i < shopItems.Length; i++)
                                {
                                    if (shopItems[i] == slot)
                                    {
                                        isTarget = true;
                                        break;
                                    }
                                }

                                if (
                                    isTarget
                                    && item.TypeCategory == MotelyItemTypeCategory.Standardcard
                                    && MatchesStandardCard(item, clause)
                                )
                                {
                                    count++;
                                }
                            }
                        }

                        // ── Standard packs ──
                        if (boosterPacks.Length > 0)
                        {
                            var packStream = singleCtx.CreateBoosterPackStream(ante);
                            var cardStream = singleCtx.CreateStandardPackCardStream(ante);

                            // SIMD prefilter over-permissive by design; scoring re-verifies per-ante.
                            for (int p = 0; p <= maxBoosterPack; p++)
                            {
                                var pack = singleCtx.GetNextBoosterPack(ref packStream);
                                bool isTarget = false;
                                for (int i = 0; i < boosterPacks.Length; i++)
                                {
                                    if (boosterPacks[i] == p)
                                    {
                                        isTarget = true;
                                        break;
                                    }
                                }

                                if (
                                    isTarget
                                    && pack.GetPackType() == MotelyBoosterPackType.Standard
                                )
                                {
                                    var contents = singleCtx.GetNextStandardPackContents(
                                        ref cardStream,
                                        pack.GetPackSize()
                                    );
                                    for (int i = 0; i < contents.Length; i++)
                                    {
                                        if (MatchesStandardCard(contents[i], clause))
                                            count++;
                                    }
                                }
                                else if (pack.GetPackType() == MotelyBoosterPackType.Standard)
                                {
                                    singleCtx.GetNextStandardPackContents(
                                        ref cardStream,
                                        pack.GetPackSize()
                                    );
                                }
                            }
                        }

                        if (count >= needed)
                            break;
                    }

                    return (count >= needed) ? 1 : 0;
                }
            );
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool MatchesStandardCard(MotelyItem item, StandardCardClause clause)
        {
            if (clause.Rank.HasValue && item.StandardcardRank != clause.Rank.Value)
                return false;
            if (clause.Suit.HasValue && item.StandardcardSuit != clause.Suit.Value)
                return false;
            if (clause.Enhancement.HasValue && item.Enhancement != clause.Enhancement.Value)
                return false;
            if (clause.Seal.HasValue && item.Seal != clause.Seal.Value)
                return false;
            if (clause.Edition.HasValue && item.Edition != clause.Edition.Value)
                return false;
            return true;
        }
    }
}

/// <summary>
/// <c>sources:</c> block for <c>standardCard:</c>. Colocated with <see cref="StandardCardFilterDesc"/> (T5).
/// </summary>
public sealed record StandardCardSourceConfig
{
    public static readonly string[] SourceKeys =
        ["shopItems", "boosterPacks", "certificate", "incantation", "familiar", "grim", "deckDraw"];

    public int[] ShopItems { get; set; } = [];
    public int[] BoosterPacks { get; set; } = [];

    public int[] Certificate { get; set; } = [];
    public int[] Incantation { get; set; } = [];
    public int[] Familiar { get; set; } = [];
    public int[] Grim { get; set; } = [];
    public int[] DeckDraw { get; set; } = [];
}
