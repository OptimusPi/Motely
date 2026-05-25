namespace Motely.Filters.Jaml;

public sealed class JokerSourceConfig
{
    public int[] ShopItems { get; init; } = [];
    public int[] BoosterPacks { get; init; } = [];
    public int[] CommonShopJokers { get; init; } = [];
    public int[] UncommonShopJokers { get; init; } = [];
    public int[] RareShopJokers { get; init; } = [];
    public int[] AllShopJokers { get; init; } = [];
    public int[] Judgement { get; init; } = [];
    public int[] Wraith { get; init; } = [];
    public int[] RiffRaff { get; init; } = [];
    public int[] RareTag { get; init; } = [];
    public int[] UncommonTag { get; init; } = [];
}

public sealed class LegendaryJokerSourceConfig
{
    public int[] BoosterPacks { get; init; } = [];
    public int[] ArcanaPacks { get; init; } = [];
    public int[] SpectralPacks { get; init; } = [];
    public int[] SoulCard { get; init; } = [];
    public bool RequireMegaPack { get; init; }

    public int MaxReferencedBoosterSlot()
    {
        int max = -1;
        for (int i = 0; i < BoosterPacks.Length; i++)
            if (BoosterPacks[i] > max) max = BoosterPacks[i];
        for (int i = 0; i < ArcanaPacks.Length; i++)
            if (ArcanaPacks[i] > max) max = ArcanaPacks[i];
        for (int i = 0; i < SpectralPacks.Length; i++)
            if (SpectralPacks[i] > max) max = SpectralPacks[i];
        return max;
    }
}

public sealed class PlanetSourceConfig
{
    public int[] ShopItems { get; init; } = [];
    public int[] BoosterPacks { get; init; } = [];
}

public sealed class TarotCardSourceConfig
{
    public int[] ShopItems { get; init; } = [];
    public int[] BoosterPacks { get; init; } = [];
    public int[] Emperor { get; init; } = [];
    public int[] PurpleSealOrEightBall { get; init; } = [];
    public bool CharmTag { get; init; }
}

public sealed class SpectralCardSourceConfig
{
    public int[] ShopItems { get; init; } = [];
    public int[] BoosterPacks { get; init; } = [];
    public int[] SixthSense { get; init; } = [];
    public int[] Seance { get; init; } = [];
    public bool EtherealTag { get; init; }
}

public sealed class StandardCardSourceConfig
{
    public int[] ShopItems { get; init; } = [];
    public int[] BoosterPacks { get; init; } = [];
}
