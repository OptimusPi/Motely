using System;
using Motely.Filters.Native;

namespace Motely.Filters.Jaml;

public interface IJamlClause
{
    string? Label { get; }
    int Min { get; }
    int? Max { get; }
    int Score { get; }
    int EstimatedCost { get; }
    string Describe();
}

public abstract class JamlClause : IJamlClause
{
    public string? Label { get; set; }
    public int[] Antes { get; set; } = [];
    public int Min { get; set; } = 1;
    public int? Max { get; set; }
    public int Score { get; set; }

    public int MaxAnte
    {
        get
        {
            int max = 0;
            for (int i = 0; i < Antes.Length; i++)
                if (Antes[i] > max)
                    max = Antes[i];
            return max;
        }
    }

    public virtual int EstimatedCost => 10 + MaxAnte;
    public abstract string Describe();
}

public abstract class RollClause : IJamlClause
{
    public string? Label { get; set; }
    public int[] Rolls { get; set; } = [];
    public int Min { get; set; } = 1;
    public int? Max { get; set; }
    public int Score { get; set; }
    public virtual int EstimatedCost => 5;

    public abstract string Describe();
}

public static class JamlClauseExtensions
{
    public static IMotelySeedFilterDesc CreateDesc(this IJamlClause clause)
    {
        return clause switch
        {
            JokerClause c => new JokerFilterDesc(c),
            CommonJokerClause c => new CommonJokerFilterDesc(c),
            UncommonJokerClause c => new UncommonJokerFilterDesc(c),
            RareJokerClause c => new RareJokerFilterDesc(c),
            LegendaryJokerClause c => new LegendaryJokerFilterDesc(c),
            VoucherClause c => new VoucherFilterDesc(c),
            TarotCardClause c => new TarotCardFilterDesc(c),
            SpectralCardClause c => SpecialSpectralCardFilterDesc.Handles(c)
                ? new SpecialSpectralCardFilterDesc(c)
                : new SpectralCardFilterDesc(c),
            PlanetCardClause c => new PlanetCardFilterDesc(c),
            StandardCardClause c => new StandardCardFilterDesc(c),
            BossClause c => new BossFilterDesc(c),
            TagClause c => new TagFilterDesc(c),
            ErraticRankClause c => new ErraticRankFilterDesc(c),
            ErraticSuitClause c => new ErraticSuitFilterDesc(c),
            ErraticCardClause c => new ErraticCardFilterDesc(c),
            LuckyMoneyClause c => new LuckyMoneyFilterDesc(c),
            LuckyMultClause c => new LuckyMultFilterDesc(c),
            MisprintMultClause c => new MisprintMultFilterDesc(c),
            WheelOfFortuneClause c => new WheelOfFortuneFilterDesc(c),
            CavendishExtinctClause c => new CavendishExtinctFilterDesc(c),
            GrosMichelExtinctClause c => new GrosMichelExtinctFilterDesc(c),
            SpaceLevelupClause c => new SpaceLevelupFilterDesc(c),
            BusinessPayoutClause c => new BusinessPayoutFilterDesc(c),
            BloodstoneTriggerClause c => new BloodstoneTriggerFilterDesc(c),
            ParkingPayoutClause c => new ParkingPayoutFilterDesc(c),
            GlassDestroyClause c => new GlassDestroyFilterDesc(c),
            WheelStaysFlippedClause c => new WheelStaysFlippedFilterDesc(c),
            StartingDrawClause c => new StartingDrawFilterDesc(c),
            AndClause c => new AndFilterDesc(Array.ConvertAll(c.Clauses, static inner => inner.CreateDesc())),
            OrClause c => new OrFilterDesc(Array.ConvertAll(c.Clauses, static inner => inner.CreateDesc()), c.Min),
            _ => throw new ArgumentException($"Unsupported clause type {clause.GetType()}")
        };
    }
}

