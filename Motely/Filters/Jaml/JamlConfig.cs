using System.Collections.Generic;

namespace Motely.Filters.Jaml;

/// <summary>
/// JAML config consumed by JamlSearchBuilder.
/// </summary>
public sealed class JamlConfig
{
    public required string Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Author { get; set; }
    public MotelyDeck Deck { get; set; } = MotelyDeck.Red;
    public MotelyStake Stake { get; set; } = MotelyStake.White;
    public List<string> Seeds { get; set; } = [];

    public List<IJamlClause> Must { get; set; } = [];
    public List<IJamlClause> Should { get; set; } = [];
    public List<IJamlClause> MustNot { get; set; } = [];
}

public static class JamlConfigExtensions
{
    public static bool HasAnyClauses(this JamlConfig config) =>
        config.Must.Count != 0 || config.Should.Count != 0 || config.MustNot.Count != 0;
}

public sealed class JokerSourceConfig
{
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

public sealed class LegendaryJokerSourceConfig
{
    public int[] ShopItems { get; set; } = [];

    /// <summary>
    /// Legacy: pack offering slots where The Soul may count from either arcana or Spectral path.
    /// Ignored for slot matching when <see cref="ArcanaPacks"/> or <see cref="SpectralPacks"/> is non-empty.
    /// </summary>
    public int[] BoosterPacks { get; set; } = [];

    /// <summary>
    /// If non-empty (or <see cref="SpectralPacks"/> non-empty), only listed slots are checked on the arcana/Tarot path.
    /// </summary>
    public int[] ArcanaPacks { get; set; } = [];

    /// <summary>Only listed slots on the Spectral pack path.</summary>
    public int[] SpectralPacks { get; set; } = [];

    public int[] SoulCard { get; set; } = [];

    /// <summary>If true, only Mega-sized booster packs (e.g. Charm Tag Mega arcana) match.</summary>
    public bool RequireMegaPack { get; set; }

    /// <summary>Largest referenced pack slot index across all booster source arrays (-1 if none).</summary>
    public int MaxReferencedBoosterSlot()
    {
        int m = -1;
        foreach (var x in BoosterPacks)
            if (x > m)
                m = x;
        foreach (var x in ArcanaPacks)
            if (x > m)
                m = x;
        foreach (var x in SpectralPacks)
            if (x > m)
                m = x;
        return m;
    }
}

public sealed class TarotCardSourceConfig
{
    public int[] ShopItems { get; set; } = [];
    public int[] BoosterPacks { get; set; } = [];
    public int[] Emperor { get; set; } = [];
    public int[] PurpleSealOrEightBall { get; set; } = [];

    /// <summary>
    /// When true, booster arcana scoring may consume the Charm-tag bonus pack (second weighted slot, no natural Arcana).
    /// </summary>
    public bool CharmTag { get; set; }
}

public sealed class SpectralCardSourceConfig
{
    public int[] ShopItems { get; set; } = [];
    public int[] BoosterPacks { get; set; } = [];
    public int[] SixthSense { get; set; } = [];
    public int[] Seance { get; set; } = [];

    /// <summary>
    /// When true, booster Spectral scoring may consume the Ethereal-tag bonus pack (second weighted slot, no natural Spectral).
    /// </summary>
    public bool EtherealTag { get; set; }
}

public sealed class PlanetSourceConfig
{
    public int[] ShopItems { get; set; } = [];
    public int[] BoosterPacks { get; set; } = [];
}

public sealed class StandardCardSourceConfig
{
    public int[] ShopItems { get; set; } = [];
    public int[] BoosterPacks { get; set; } = [];

    public int[] Certificate { get; set; } = [];
    public int[] Incantation { get; set; } = [];
    public int[] Familiar { get; set; } = [];
    public int[] Grim { get; set; } = [];
    public int[] DeckDraw { get; set; } = [];
}
