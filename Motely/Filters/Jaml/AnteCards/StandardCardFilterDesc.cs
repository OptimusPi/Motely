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

    // null = no sources: in JAML → filter DefaultSources at CreateFilter/score (not parse).
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

    /// <summary>
    /// Filter-layer default when Sources is null. Shop only; packs/specialty need explicit sources:.
    /// </summary>
    internal static readonly StandardCardSourceConfig DefaultSources = new()
    {
        ShopItems = [0, 1, 2, 3, 4, 5, 6, 7],
    };

    public StandardCardFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        foreach (var ante in _clause.Antes)
        {
            ctx.CacheShopStream(ante);
            ctx.CacheBoosterPackStream(ante);
        }

        return new StandardCardFilter(_clause);
    }

    public struct StandardCardFilter(StandardCardClause clause) : IMotelySeedFilter
    {
        private readonly StandardCardClause _clause = clause;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            // Single match core: same PrepareRunState + count as should-scoring.
            var clause = _clause;
            return ctx.SearchIndividualSeeds(
                (MotelySingleSearchContext singleCtx) =>
                    JamlScoring.ClauseMeetsMinForFilter(ref singleCtx, clause) ? 1 : 0
            );
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
