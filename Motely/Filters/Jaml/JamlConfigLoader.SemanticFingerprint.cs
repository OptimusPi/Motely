using System.Buffers.Binary;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace Motely.Filters;

/// <summary>
/// AOT-safe semantic fingerprint for <see cref="JamlConfig"/>: walks loaded <see cref="IJamlClause"/> trees
/// (not <see cref="JamlSearchBuilder"/> output), packs enums and <see cref="MotelyItemType"/> ints, sorts
/// order-independent multisets, excludes <see cref="IJamlClause.Label"/>. Full SHA-256 hex is appended to the base slug.
/// </summary>
/// <remarks>
/// Covered clause kinds: <see cref="AndClause"/>, <see cref="OrClause"/>, <see cref="JokerClause"/>,
/// <see cref="CommonJokerClause"/>, <see cref="UncommonJokerClause"/>, <see cref="RareJokerClause"/>,
/// <see cref="MixedJokerClause"/>, <see cref="LegendaryJokerClause"/>, <see cref="VoucherClause"/>,
/// <see cref="BossClause"/>, <see cref="TagClause"/>, <see cref="TarotCardClause"/>, <see cref="SpectralCardClause"/>,
/// <see cref="PlanetCardClause"/>, <see cref="StandardCardClause"/>, <see cref="ErraticCardClause"/>,
/// <see cref="ErraticRankClause"/>, <see cref="ErraticSuitClause"/>, roll events
/// (<see cref="LuckyMoneyClause"/> … <see cref="GrosMichelExtinctClause"/>), <see cref="StartingDrawClause"/>.
/// Human-only fields omitted: <see cref="IJamlClause.Label"/>; root <see cref="JamlConfig.Name"/> / description / aesthetics
/// do not participate (only deck, stake, defaults + clause trees).
/// </remarks>
public static partial class JamlConfigLoader
{
    internal static string AppendSemanticFingerprintToFilterId(
        string baseId,
        JamlConfig config,
        JamlDefaultsDto? docDefaults
    )
    {
        var payload = SemanticFingerprintBuilder.Build(config, docDefaults, DefaultAntes);
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(payload, hash);
        return $"{baseId}_{Convert.ToHexString(hash).ToLowerInvariant()}";
    }
}

file static class SemanticFingerprintBuilder
{
    /// <summary>Increment when the canonical binary format changes.</summary>
    private const int FingerprintFormatVersion = 1;

    private enum ClauseKind : byte
    {
        And = 1,
        Or = 2,
        Joker = 3,
        CommonJoker = 4,
        UncommonJoker = 5,
        RareJoker = 6,
        MixedJoker = 7,
        LegendaryJoker = 8,
        Voucher = 9,
        Boss = 10,
        Tag = 11,
        TarotCard = 12,
        SpectralCard = 13,
        PlanetCard = 14,
        StandardCard = 15,
        ErraticCard = 16,
        ErraticRank = 17,
        ErraticSuit = 18,
        LuckyMoney = 19,
        LuckyMult = 20,
        MisprintMult = 21,
        WheelOfFortune = 22,
        CavendishExtinct = 23,
        GrosMichelExtinct = 24,
        StartingDraw = 25,
    }

    public static byte[] Build(JamlConfig config, JamlDefaultsDto? docDefaults, int[] defaultAntes)
    {
        var b = new List<byte>(4096);
        W32(b, FingerprintFormatVersion);
        W32(b, (int)config.Deck);
        W32(b, (int)config.Stake);

        var antes = docDefaults?.Antes ?? defaultAntes;
        WriteSortedInt32s(b, antes);
        WriteSortedInt32s(b, docDefaults?.BoosterPacks);
        WriteSortedInt32s(b, docDefaults?.ShopItems);
        if (docDefaults?.Score is { } sc)
        {
            b.Add(1);
            W32(b, sc);
        }
        else
            b.Add(0);

        WriteSortedLengthPrefixedBlocks(b, FingerprintSet(config.Must));
        WriteSortedLengthPrefixedBlocks(b, FingerprintSet(config.Should));
        WriteSortedLengthPrefixedBlocks(b, FingerprintSet(config.MustNot));

        return b.ToArray();
    }

    private static List<byte[]> FingerprintSet(JamlClauseSet set)
    {
        var list = new List<byte[]>(set.Count);
        foreach (var c in set.OrderedClauses)
            list.Add(FingerprintClause(c));
        return list;
    }

    private static void WriteSortedLengthPrefixedBlocks(List<byte> b, List<byte[]> blocks)
    {
        blocks.Sort(ByteSequenceComparer.Instance);
        W32(b, blocks.Count);
        for (int i = 0; i < blocks.Count; i++)
        {
            W32(b, blocks[i].Length);
            b.AddRange(blocks[i]);
        }
    }

    private static byte[] FingerprintClause(IJamlClause clause)
    {
        var b = new List<byte>(256);
        switch (clause)
        {
            case AndClause a:
                b.Add((byte)ClauseKind.And);
                W32(b, a.Score);
                WriteSortedLengthPrefixedBlocks(b, FingerprintClauses(a.Clauses));
                return b.ToArray();
            case OrClause o:
                b.Add((byte)ClauseKind.Or);
                W32(b, o.Score);
                W32(b, o.Min);
                WriteSortedLengthPrefixedBlocks(b, FingerprintClauses(o.Clauses));
                return b.ToArray();
            case JokerClause j:
                b.Add((byte)ClauseKind.Joker);
                W32(b, j.Score);
                b.Add(j.IsWildcard ? (byte)1 : (byte)0);
                WriteOptionalEnum32(b, j.WildcardRarity);
                WriteOptionalEnum32(b, j.Edition);
                WriteSortedEnumInts(b, j.Stickers);
                WriteSortedItemTypesFromJokers(b, j.Jokers);
                WriteJokerSourceConfig(b, j.Sources);
                WriteSortedInt32s(b, j.Antes);
                W32(b, j.Min);
                return b.ToArray();
            case CommonJokerClause cj:
                b.Add((byte)ClauseKind.CommonJoker);
                W32(b, cj.Score);
                b.Add(cj.IsWildcard ? (byte)1 : (byte)0);
                WriteOptionalEnum32(b, cj.Edition);
                WriteSortedEnumInts(b, cj.Stickers);
                WriteSortedItemTypesFromEnums(b, cj.Jokers);
                WriteJokerSourceConfig(b, cj.Sources);
                WriteSortedInt32s(b, cj.Antes);
                W32(b, cj.Min);
                return b.ToArray();
            case UncommonJokerClause uj:
                b.Add((byte)ClauseKind.UncommonJoker);
                W32(b, uj.Score);
                b.Add(uj.IsWildcard ? (byte)1 : (byte)0);
                WriteOptionalEnum32(b, uj.Edition);
                WriteSortedEnumInts(b, uj.Stickers);
                WriteSortedItemTypesFromEnums(b, uj.Jokers);
                WriteJokerSourceConfig(b, uj.Sources);
                WriteSortedInt32s(b, uj.Antes);
                W32(b, uj.Min);
                return b.ToArray();
            case RareJokerClause rj:
                b.Add((byte)ClauseKind.RareJoker);
                W32(b, rj.Score);
                b.Add(rj.IsWildcard ? (byte)1 : (byte)0);
                WriteOptionalEnum32(b, rj.Edition);
                WriteSortedEnumInts(b, rj.Stickers);
                WriteSortedItemTypesFromEnums(b, rj.Jokers);
                WriteJokerSourceConfig(b, rj.Sources);
                WriteSortedInt32s(b, rj.Antes);
                W32(b, rj.Min);
                return b.ToArray();
            case MixedJokerClause mj:
                b.Add((byte)ClauseKind.MixedJoker);
                W32(b, mj.Score);
                b.Add(mj.IsWildcard ? (byte)1 : (byte)0);
                WriteOptionalEnum32(b, mj.WildcardRarity);
                WriteOptionalEnum32(b, mj.Edition);
                WriteSortedEnumInts(b, mj.Stickers);
                WriteSortedItemTypesFromJokers(b, mj.Jokers);
                WriteJokerSourceConfig(b, mj.Sources);
                WriteSortedInt32s(b, mj.Antes);
                W32(b, mj.Min);
                return b.ToArray();
            case LegendaryJokerClause lj:
                b.Add((byte)ClauseKind.LegendaryJoker);
                W32(b, lj.Score);
                b.Add(lj.IsWildcard ? (byte)1 : (byte)0);
                WriteOptionalEnum32(b, lj.Edition);
                WriteSortedItemTypesFromJokers(b, lj.Jokers);
                b.Add(lj.SoulCardOnly ? (byte)1 : (byte)0);
                W32(b, lj.SoulEditionRolls);
                WriteSoulJokerSourceConfig(b, lj.Sources);
                WriteSortedInt32s(b, lj.Antes);
                W32(b, lj.Min);
                return b.ToArray();
            case VoucherClause v:
                b.Add((byte)ClauseKind.Voucher);
                W32(b, v.Score);
                WriteSortedEnumInts(b, v.Vouchers);
                WriteSortedInt32s(b, v.Antes);
                W32(b, v.Min);
                return b.ToArray();
            case BossClause bc:
                b.Add((byte)ClauseKind.Boss);
                W32(b, bc.Score);
                WriteSortedEnumInts(b, bc.Bosses);
                WriteSortedInt32s(b, bc.Antes);
                W32(b, bc.Min);
                return b.ToArray();
            case TagClause tg:
                b.Add((byte)ClauseKind.Tag);
                W32(b, tg.Score);
                W32(b, (int)tg.Position);
                WriteSortedEnumInts(b, tg.Tags);
                WriteSortedInt32s(b, tg.Antes);
                W32(b, tg.Min);
                return b.ToArray();
            case TarotCardClause tc:
                b.Add((byte)ClauseKind.TarotCard);
                W32(b, tc.Score);
                WriteSortedItemTypesFromEnums(b, tc.Tarots);
                WriteTarotCardSourceConfig(b, tc.Sources);
                WriteSortedInt32s(b, tc.Antes);
                W32(b, tc.Min);
                return b.ToArray();
            case SpectralCardClause sc:
                b.Add((byte)ClauseKind.SpectralCard);
                W32(b, sc.Score);
                WriteSortedItemTypesFromEnums(b, sc.Spectrals);
                WriteSpectralCardSourceConfig(b, sc.Sources);
                WriteSortedInt32s(b, sc.Antes);
                W32(b, sc.Min);
                return b.ToArray();
            case PlanetCardClause pc:
                b.Add((byte)ClauseKind.PlanetCard);
                W32(b, pc.Score);
                WriteSortedItemTypesFromEnums(b, pc.Planets);
                WritePlanetSourceConfig(b, pc.Sources);
                WriteSortedInt32s(b, pc.Antes);
                W32(b, pc.Min);
                return b.ToArray();
            case StandardCardClause std:
                b.Add((byte)ClauseKind.StandardCard);
                W32(b, std.Score);
                WriteOptionalEnum32(b, std.Rank);
                WriteOptionalEnum32(b, std.Suit);
                WriteOptionalEnum32(b, std.Enhancement);
                WriteOptionalEnum32(b, std.Seal);
                WriteOptionalEnum32(b, std.Edition);
                WriteStandardCardSourceConfig(b, std.Sources);
                WriteSortedInt32s(b, std.Antes);
                W32(b, std.Min);
                return b.ToArray();
            case ErraticCardClause ec:
                b.Add((byte)ClauseKind.ErraticCard);
                W32(b, ec.Score);
                WriteOptionalEnum32(b, ec.Rank);
                WriteOptionalEnum32(b, ec.Suit);
                WriteSortedInt32s(b, ec.Antes);
                W32(b, ec.Min);
                return b.ToArray();
            case ErraticRankClause er:
                b.Add((byte)ClauseKind.ErraticRank);
                W32(b, er.Score);
                W32(b, (int)er.Rank);
                WriteSortedInt32s(b, er.Antes);
                W32(b, er.Min);
                return b.ToArray();
            case ErraticSuitClause es:
                b.Add((byte)ClauseKind.ErraticSuit);
                W32(b, es.Score);
                W32(b, (int)es.Suit);
                WriteSortedInt32s(b, es.Antes);
                W32(b, es.Min);
                return b.ToArray();
            case LuckyMoneyClause lm:
                b.Add((byte)ClauseKind.LuckyMoney);
                W32(b, lm.Score);
                WriteSortedInt32s(b, lm.Rolls);
                W32(b, lm.Min);
                return b.ToArray();
            case LuckyMultClause lmu:
                b.Add((byte)ClauseKind.LuckyMult);
                W32(b, lmu.Score);
                WriteSortedInt32s(b, lmu.Rolls);
                W32(b, lmu.Min);
                return b.ToArray();
            case MisprintMultClause mm:
                b.Add((byte)ClauseKind.MisprintMult);
                W32(b, mm.Score);
                WriteSortedInt32s(b, mm.Rolls);
                W32(b, mm.Min);
                if (mm.Value is { } mv)
                {
                    b.Add(1);
                    W32(b, mv);
                }
                else
                    b.Add(0);
                return b.ToArray();
            case WheelOfFortuneClause wf:
                b.Add((byte)ClauseKind.WheelOfFortune);
                W32(b, wf.Score);
                WriteSortedInt32s(b, wf.Rolls);
                W32(b, wf.Min);
                return b.ToArray();
            case CavendishExtinctClause ce:
                b.Add((byte)ClauseKind.CavendishExtinct);
                W32(b, ce.Score);
                WriteSortedInt32s(b, ce.Rolls);
                W32(b, ce.Min);
                return b.ToArray();
            case GrosMichelExtinctClause gm:
                b.Add((byte)ClauseKind.GrosMichelExtinct);
                W32(b, gm.Score);
                WriteSortedInt32s(b, gm.Rolls);
                W32(b, gm.Min);
                return b.ToArray();
            case StartingDrawClause sd:
                b.Add((byte)ClauseKind.StartingDraw);
                W32(b, sd.Score);
                WriteOptionalEnum32(b, sd.Rank);
                WriteOptionalEnum32(b, sd.Suit);
                WriteSortedInt32s(b, sd.Antes);
                W32(b, sd.Min);
                return b.ToArray();
            default:
                throw new InvalidOperationException(
                    $"Unhandled IJamlClause type for semantic fingerprint: {clause.GetType().Name}"
                );
        }
    }

    private static List<byte[]> FingerprintClauses(IJamlClause[] clauses)
    {
        var list = new List<byte[]>(clauses.Length);
        for (int i = 0; i < clauses.Length; i++)
            list.Add(FingerprintClause(clauses[i]));
        return list;
    }

    private static void WriteSortedItemTypesFromJokers(List<byte> b, MotelyJoker[] jokers)
    {
        var ints = new int[jokers.Length];
        for (int i = 0; i < jokers.Length; i++)
            ints[i] = JokerToItemTypeInt(jokers[i]);
        Array.Sort(ints);
        W32(b, ints.Length);
        for (int i = 0; i < ints.Length; i++)
            W32(b, ints[i]);
    }

    private static void WriteSortedItemTypesFromEnums<T>(List<byte> b, T[] items)
        where T : struct, Enum
    {
        var ints = new int[items.Length];
        for (int i = 0; i < items.Length; i++)
            ints[i] = EnumNameToMotelyItemTypeInt(items[i]);
        Array.Sort(ints);
        W32(b, ints.Length);
        for (int i = 0; i < ints.Length; i++)
            W32(b, ints[i]);
    }

    private static void WriteSortedEnumInts<T>(List<byte> b, T[] items)
        where T : struct, Enum
    {
        var ints = new int[items.Length];
        for (int i = 0; i < items.Length; i++)
            ints[i] = Convert.ToInt32(items[i]);
        Array.Sort(ints);
        W32(b, ints.Length);
        for (int i = 0; i < ints.Length; i++)
            W32(b, ints[i]);
    }

    private static int JokerToItemTypeInt(MotelyJoker j) =>
        EnumNameToMotelyItemTypeInt(j);

    private static int EnumNameToMotelyItemTypeInt<T>(T e)
        where T : struct, Enum
    {
        var name = e.ToString();
        if (!Enum.TryParse(name, ignoreCase: true, out MotelyItemType t))
            throw new InvalidOperationException($"MotelyItemType not found for '{typeof(T).Name}.{name}'");
        return (int)t;
    }

    private static void WriteOptionalEnum32<T>(List<byte> b, T? nullable)
        where T : struct, Enum
    {
        if (nullable is { } v)
        {
            b.Add(1);
            W32(b, Convert.ToInt32(v));
        }
        else
            b.Add(0);
    }

    private static void WriteSortedInt32s(List<byte> b, int[]? arr)
    {
        if (arr is not { Length: > 0 })
        {
            W32(b, 0);
            return;
        }

        var copy = new int[arr.Length];
        Array.Copy(arr, copy, arr.Length);
        Array.Sort(copy);
        W32(b, copy.Length);
        for (int i = 0; i < copy.Length; i++)
            W32(b, copy[i]);
    }

    private static void WriteJokerSourceConfig(List<byte> b, JokerSourceConfig s)
    {
        WriteSortedInt32s(b, s.AllShopJokers);
        WriteSortedInt32s(b, s.BoosterPacks);
        WriteSortedInt32s(b, s.CommonShopJokers);
        WriteSortedInt32s(b, s.Judgement);
        WriteSortedInt32s(b, s.RareTag);
        WriteSortedInt32s(b, s.RiffRaff);
        WriteSortedInt32s(b, s.ShopItems);
        WriteSortedInt32s(b, s.UncommonShopJokers);
        WriteSortedInt32s(b, s.UncommonTag);
        WriteSortedInt32s(b, s.Wraith);
    }

    private static void WriteSoulJokerSourceConfig(List<byte> b, SoulJokerSourceConfig s)
    {
        var n = s.NormalizeSoulJokerBoostersIfEmpty();
        WriteSortedInt32s(b, n.ArcanaBoosterPacks);
        WriteSortedInt32s(b, n.BoosterPacks);
        WriteSortedInt32s(b, n.ShopItems);
        WriteSortedInt32s(b, n.SoulCard);
        WriteSortedInt32s(b, n.SpectralBoosterPacks);
        b.Add(n.RequireMegaPack ? (byte)1 : (byte)0);
    }

    private static void WriteTarotCardSourceConfig(List<byte> b, TarotCardSourceConfig s)
    {
        WriteSortedInt32s(b, s.ShopItems);
        WriteSortedInt32s(b, s.BoosterPacks);
        WriteSortedInt32s(b, s.Emperor);
        WriteSortedInt32s(b, s.PurpleSealOrEightBall);
        b.Add(s.CharmTag ? (byte)1 : (byte)0);
    }

    private static void WriteSpectralCardSourceConfig(List<byte> b, SpectralCardSourceConfig s)
    {
        WriteSortedInt32s(b, s.ShopItems);
        WriteSortedInt32s(b, s.BoosterPacks);
        WriteSortedInt32s(b, s.SixthSense);
        WriteSortedInt32s(b, s.Seance);
        b.Add(s.EtherealTag ? (byte)1 : (byte)0);
    }

    private static void WritePlanetSourceConfig(List<byte> b, PlanetSourceConfig s)
    {
        WriteSortedInt32s(b, s.ShopItems);
        WriteSortedInt32s(b, s.BoosterPacks);
    }

    private static void WriteStandardCardSourceConfig(List<byte> b, StandardCardSourceConfig s)
    {
        WriteSortedInt32s(b, s.ShopItems);
        WriteSortedInt32s(b, s.BoosterPacks);
        WriteSortedInt32s(b, s.Certificate);
        WriteSortedInt32s(b, s.Incantation);
        WriteSortedInt32s(b, s.Familiar);
        WriteSortedInt32s(b, s.Grim);
        WriteSortedInt32s(b, s.DeckDraw);
    }

    private static void W32(List<byte> b, int v)
    {
        var s = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(s, v);
        b.AddRange(s);
    }

    private sealed class ByteSequenceComparer : IComparer<byte[]>
    {
        public static readonly ByteSequenceComparer Instance = new();

        public int Compare(byte[]? x, byte[]? y)
        {
            if (ReferenceEquals(x, y))
                return 0;
            if (x is null)
                return -1;
            if (y is null)
                return 1;
            int len = Math.Min(x.Length, y.Length);
            for (int i = 0; i < len; i++)
            {
                int c = x[i].CompareTo(y[i]);
                if (c != 0)
                    return c;
            }

            return x.Length.CompareTo(y.Length);
        }
    }
}
