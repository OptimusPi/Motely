using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using static Motely.MotelyVectorUtils;

namespace Motely.Filters.Jaml;

[JamlDiscriminator("joker", "jokers",
    ValueEnum = typeof(MotelyJoker), SourceConfigType = typeof(JokerSourceConfig))]
public sealed class JokerClause : IJamlClause, IAnteScopedClause
{
    public string? Label { get; set; }
    public int Min { get; set; } = 1;
    public int? Max { get; set; }
    public int Score { get; set; }
    public int[] Antes { get; set; } = [];
    public MotelyJoker[] Jokers { get; set; } = [];
    public bool IsWildcard { get; set; }
    public MotelyItemEdition? Edition { get; set; }
    public MotelyJokerSticker[] Stickers { get; set; } = [];
    public JokerSourceConfig? Sources { get; set; }

    /// <summary>Legendary-path sources for any Legendary names in this mixed clause. Null = apply
    /// <see cref="LegendaryJokerFilterDesc.DefaultSources"/> (same convention as <see cref="Sources"/>).</summary>
    public LegendaryJokerSourceConfig? LegendarySources { get; set; }
}

public struct JokerFilterDesc(JokerClause clause)
    : IMotelySeedFilterDesc<JokerFilterDesc.JokerFilter>,
      IJamlClauseDesc<JokerClause>
{
    private readonly JokerClause _clause = clause;

    /// <inheritdoc/>
    public static string[] Discriminators => ["joker", "jokers"];

    /// <inheritdoc/>
    public static string[] ClauseKeys =>
        ["min", "max", "score", "label", "ante", "antes", "sources", "edition", "stickers"];

    /// <inheritdoc/>
    public static bool Set(JokerClause clause, string key, IJamlValueReader value)
    {
        switch (key.ToLowerInvariant())
        {
            case "edition":
                if (!value.TryEnum<MotelyItemEdition>(out var edition)) return false;
                clause.Edition = edition;
                return true;
            case "stickers":
                if (!value.TryEnumArray<MotelyJokerSticker>(out var stickers)) return false;
                clause.Stickers = stickers;
                return true;
            default:
                return false;
        }
    }

    /// <inheritdoc/>
    public static bool SetDiscriminatorValue(JokerClause clause, IJamlValueReader value)
    {
        if (value.IsAny)
        {
            clause.IsWildcard = true;
            return true;
        }
        if (!value.TryEnumArray<MotelyJoker>(out var jokers))
            return false;
        clause.Jokers = jokers;
        return true;
    }

    /// <summary>Defaults when a clause specifies no <c>sources:</c> block — a normal shop run
    /// (8 shop slots) plus the 6 booster packs. Specialty/legendary sources stay off by default.
    /// Applied only when <c>Sources</c> is null; any explicit block overrides wholesale.</summary>
    internal static readonly JokerSourceConfig DefaultSources = new()
    {
        ShopItems = [0, 1, 2, 3, 4, 5, 6, 7],
        BoosterPacks = [0, 1, 2, 3, 4, 5],
    };

    public JokerFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        foreach (var ante in _clause.Antes)
        {
            ctx.CacheShopStream(ante);
            ctx.CacheBoosterPackStream(ante);
        }

        // Pre-calculate target item types to avoid bitwise logic in the hot loop
        var targetTypes = new MotelyItemType[_clause.Jokers.Length];
        for (int i = 0; i < _clause.Jokers.Length; i++)
        {
            if (Enum.TryParse(_clause.Jokers[i].ToString(), out MotelyItemType type))
            {
                targetTypes[i] = type;
            }
            else
            {
                throw new InvalidOperationException(
                    $"Joker {_clause.Jokers[i]} not found in MotelyItemType"
                );
            }
        }

        // null sources → default shop+packs; resolved here (and in JamlScoring) since the loader
        // no longer normalizes. Specialty/legendary sources stay off by default.
        var sources = _clause.Sources ?? DefaultSources;
        var shopIndices = sources.ShopItems;
        var boosterIndices = sources.BoosterPacks;

        Debug.Assert(
            shopIndices.Length > 0 || boosterIndices.Length > 0,
            "Joker clause should have non-empty default sources."
        );

        int maxShopItem = 0;
        foreach (var idx in shopIndices)
            if (idx > maxShopItem)
                maxShopItem = idx;

        int maxBoosterPack = 0;
        foreach (var idx in boosterIndices)
            if (idx > maxBoosterPack)
                maxBoosterPack = idx;

        return new JokerFilter(
            _clause,
            targetTypes,
            [.. shopIndices],
            [.. boosterIndices],
            maxShopItem,
            maxBoosterPack
        );
    }

    public struct JokerFilter(
        JokerClause clause,
        MotelyItemType[] targetTypes,
        int[] shopIndices,
        int[] boosterIndices,
        int maxShopItem,
        int maxBoosterPack
    ) : IMotelySeedFilter
    {
        private readonly JokerClause _clause = clause;
        private readonly MotelyItemType[] _targetTypes = targetTypes;
        private readonly int[] _shopIndices = shopIndices;
        private readonly int[] _boosterIndices = boosterIndices;
        private readonly int _maxShopItem = maxShopItem;
        private readonly int _maxBoosterPack = maxBoosterPack;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            Debug.Assert(_clause.IsWildcard || _clause.Jokers.Length > 0);
            int needed = _clause.Min;
            Debug.Assert(needed > 0, "JokerClause.Min must be > 0 — loader bug.");

            if (UsesLegendaryPath(_clause))
            {
                var clause = _clause;
                return ctx.SearchIndividualSeeds(
                    (MotelySingleSearchContext singleCtx) =>
                        JamlScoring.CountJokerClauseOccurrencesForFilter(ref singleCtx, clause)
                        >= needed
                            ? 1
                            : 0
                );
            }

            Vector256<int> matchCounts = Vector256<int>.Zero;

            var shopIndices = _shopIndices;
            var boosterIndices = _boosterIndices;

            foreach (var ante in _clause.Antes)
            {
                // ── Shop items SIMD ──
                if (shopIndices.Length > 0)
                {
                    var shopStream = ctx.CreateShopItemStream(ante);

                    for (int slot = 0; slot <= _maxShopItem; slot++)
                    {
                        var shopItem = ctx.GetNextShopItem(ref shopStream);
                        bool isTarget = false;
                        for (int i = 0; i < shopIndices.Length; i++)
                        {
                            if (shopIndices[i] == slot)
                            {
                                isTarget = true;
                                break;
                            }
                        }

                        if (!isTarget)
                            continue;

                        VectorMask jokerMatch = MatchJokers(shopItem);
                        if (jokerMatch.IsPartiallyTrue())
                        {
                            matchCounts = Vector256.Add(
                                matchCounts,
                                Vector256.ConditionalSelect(
                                    VectorMaskToConditionalSelectMask(jokerMatch),
                                    Vector256.Create(1),
                                    Vector256<int>.Zero
                                )
                            );
                        }
                    }
                }

                // ── Buffoon packs SIMD ──
                if (boosterIndices.Length > 0)
                {
                    var packStream = ctx.CreateBoosterPackStream(ante);
                    var jokerStream = ctx.CreateBuffoonPackJokerStream(ante);

                    for (int p = 0; p <= _maxBoosterPack; p++)
                    {
                        var pack = ctx.GetNextBoosterPack(ref packStream);
                        bool isTarget = false;
                        for (int i = 0; i < boosterIndices.Length; i++)
                        {
                            if (boosterIndices[i] == p)
                            {
                                isTarget = true;
                                break;
                            }
                        }

                        VectorMask isBuffoon = VectorEnum256.Equals(
                            pack.GetPackType(),
                            MotelyBoosterPackType.Buffoon
                        );

                        if (isBuffoon.IsPartiallyTrue())
                        {
                            VectorMask isNormalSize = VectorEnum256.Equals(
                                pack.GetPackSize(),
                                MotelyBoosterPackSize.Normal
                            );
                            VectorMask isJumboSize = VectorEnum256.Equals(
                                pack.GetPackSize(),
                                MotelyBoosterPackSize.Jumbo
                            );
                            VectorMask isMegaSize = VectorEnum256.Equals(
                                pack.GetPackSize(),
                                MotelyBoosterPackSize.Mega
                            );

                            if ((isBuffoon & isNormalSize).IsPartiallyTrue())
                            {
                                var contents = ctx.GetNextBuffoonPackContents(
                                    ref jokerStream,
                                    MotelyBoosterPackSize.Normal
                                );
                                MatchBuffoonContents(contents, isTarget, ref matchCounts);
                            }

                            if ((isBuffoon & isJumboSize).IsPartiallyTrue())
                            {
                                var contents = ctx.GetNextBuffoonPackContents(
                                    ref jokerStream,
                                    MotelyBoosterPackSize.Jumbo
                                );
                                MatchBuffoonContents(contents, isTarget, ref matchCounts);
                            }

                            if ((isBuffoon & isMegaSize).IsPartiallyTrue())
                            {
                                var contents = ctx.GetNextBuffoonPackContents(
                                    ref jokerStream,
                                    MotelyBoosterPackSize.Mega
                                );
                                MatchBuffoonContents(contents, isTarget, ref matchCounts);
                            }
                        }
                    }
                }
            }

            Vector256<int> minVec = Vector256.Create(_clause.Min);
            Vector256<int> comparison = Vector256.GreaterThan(
                matchCounts,
                Vector256.Subtract(minVec, Vector256.Create(1))
            );
            return new VectorMask(MotelyVectorUtils.VectorizedComparisonToMask(comparison));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private readonly VectorMask MatchJokers(in MotelyItemVector item)
        {
            VectorMask jokerMatch;
            if (_clause.IsWildcard)
            {
                jokerMatch = VectorEnum256.Equals(item.TypeCategory, MotelyItemTypeCategory.Joker);
            }
            else
            {
                jokerMatch = VectorMask.NoBitsSet;
                for (int t = 0; t < _targetTypes.Length; t++)
                    jokerMatch |= VectorEnum256.Equals(item.Type, _targetTypes[t]);
            }

            if (_clause.Edition.HasValue)
                jokerMatch &= VectorEnum256.Equals(item.Edition, _clause.Edition.Value);

            if (_clause.Stickers.Length > 0)
            {
                VectorMask stickerMatch = VectorMask.NoBitsSet;
                for (int s = 0; s < _clause.Stickers.Length; s++)
                {
                    switch (_clause.Stickers[s])
                    {
                        case MotelyJokerSticker.Eternal:
                            stickerMatch |= item.IsEternal;
                            break;
                        case MotelyJokerSticker.Perishable:
                            stickerMatch |= item.IsPerishable;
                            break;
                        case MotelyJokerSticker.Rental:
                            stickerMatch |= item.IsRental;
                            break;
                    }
                }
                jokerMatch &= stickerMatch;
            }

            return jokerMatch;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private readonly void MatchBuffoonContents(
            in MotelyVectorItemSet contents,
            bool isTarget,
            ref Vector256<int> matchCounts
        )
        {
            if (!isTarget)
                return;

            for (int i = 0; i < contents.Length; i++)
            {
                VectorMask match = MatchJokers(contents[i]);
                matchCounts = Vector256.Add(
                    matchCounts,
                    Vector256.ConditionalSelect(
                        VectorMaskToConditionalSelectMask(match),
                        Vector256.Create(1),
                        Vector256<int>.Zero
                    )
                );
            }
        }

        private static bool UsesLegendaryPath(JokerClause clause)
        {
            if (clause.IsWildcard)
                return true;

            for (int i = 0; i < clause.Jokers.Length; i++)
            {
                if (
                    ((MotelyJokerRarity)((int)clause.Jokers[i] & MotelyGlobals.JokerRarityMask))
                    == MotelyJokerRarity.Legendary
                )
                    return true;
            }

            return false;
        }
    }
}

// ── Rarity-specific joker clauses ──

[JamlDiscriminator("commonJoker", "commonJokers",
    ValueEnum = typeof(MotelyJokerCommon), SourceConfigType = typeof(JokerSourceConfig))]
public sealed class CommonJokerClause : IJamlClause, IAnteScopedClause
{
    public string? Label { get; set; }
    public int Min { get; set; } = 1;
    public int? Max { get; set; }
    public int Score { get; set; }
    public int[] Antes { get; set; } = [];
    public MotelyJokerCommon[] Jokers { get; set; } = [];
    public bool IsWildcard { get; set; }
    public MotelyItemEdition? Edition { get; set; }
    public MotelyJokerSticker[] Stickers { get; set; } = [];
    public JokerSourceConfig? Sources { get; set; }
}

[JamlDiscriminator("uncommonJoker", "uncommonJokers",
    ValueEnum = typeof(MotelyJokerUncommon), SourceConfigType = typeof(JokerSourceConfig))]
public sealed class UncommonJokerClause : IJamlClause, IAnteScopedClause
{
    public string? Label { get; set; }
    public int Min { get; set; } = 1;
    public int? Max { get; set; }
    public int Score { get; set; }
    public int[] Antes { get; set; } = [];
    public MotelyJokerUncommon[] Jokers { get; set; } = [];
    public bool IsWildcard { get; set; }
    public MotelyItemEdition? Edition { get; set; }
    public MotelyJokerSticker[] Stickers { get; set; } = [];
    public JokerSourceConfig? Sources { get; set; }
}

[JamlDiscriminator("rareJoker", "rareJokers",
    ValueEnum = typeof(MotelyJokerRare), SourceConfigType = typeof(JokerSourceConfig))]
public sealed class RareJokerClause : IJamlClause, IAnteScopedClause
{
    public string? Label { get; set; }
    public int Min { get; set; } = 1;
    public int? Max { get; set; }
    public int Score { get; set; }
    public int[] Antes { get; set; } = [];
    public MotelyJokerRare[] Jokers { get; set; } = [];
    public bool IsWildcard { get; set; }
    public MotelyItemEdition? Edition { get; set; }
    public MotelyJokerSticker[] Stickers { get; set; } = [];
    public JokerSourceConfig? Sources { get; set; }
}

/// <summary>
/// <c>sources:</c> block for joker / common / uncommon / rare clauses. Lives with the joker
/// desc family (T5) — not on the dumb <see cref="JamlConfig"/> bag.
/// </summary>
public sealed record JokerSourceConfig
{
    /// <summary>
    /// This class's settable properties, camelCased — the single list JamlConfigLoader
    /// ValidateKeys and Motely.Schema both read. <c>emperor</c> lives on
    /// <see cref="TarotCardSourceConfig"/>, not here.
    /// </summary>
    public static readonly string[] SourceKeys =
    [
        "shopItems", "boosterPacks", "judgement", "wraith", "riffRaff", "rareTag", "uncommonTag",
        "commonShopJokers", "uncommonShopJokers", "rareShopJokers", "allShopJokers",
    ];

    /// <summary>Assembled shop slots via the full shop item stream (any item type).</summary>
    public int[] ShopItems { get; set; } = [];
    public int[] BoosterPacks { get; set; } = [];

    /// <summary>Ante-1 pack-slot cap. Default 3 (normal gameplay). Raise to 5 for Hieroglyph scans.</summary>
    public int[] Judgement { get; set; } = [];
    public int[] Wraith { get; set; } = [];
    public int[] RiffRaff { get; set; } = [];
    public int[] RareTag { get; set; } = [];
    public int[] UncommonTag { get; set; } = [];

    /// <summary>0..n rolls on the common shop joker PRNG only (fast path).</summary>
    public int[] CommonShopJokers { get; set; } = [];

    /// <summary>0..n rolls on the uncommon shop joker PRNG only (fast path; not the same indices as <see cref="ShopItems"/> when slots mix types).</summary>
    public int[] UncommonShopJokers { get; set; } = [];

    /// <summary>0..n rolls on the rare shop joker PRNG only (fast path).</summary>
    public int[] RareShopJokers { get; set; } = [];

    /// <summary>0..n rolls on the all-rarity shop joker stream (fast path).</summary>
    public int[] AllShopJokers { get; set; } = [];
}
