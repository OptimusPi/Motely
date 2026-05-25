namespace Motely.Filters.Jaml;

public interface IJamlClause
{
    string? Label { get; }
    int Min { get; }
    int? Max { get; }
    int Score { get; }
    int EstimatedCost { get; }
    string Describe();
    IMotelySeedFilterDesc CreateDesc();
}

public abstract class JamlClause : IJamlClause
{
    public string? Label { get; init; }
    public int[] Antes { get; init; } = [];
    public int Min { get; init; } = 1;
    public int? Max { get; init; }
    public int Score { get; init; }

    public int MaxAnte
    {
        get
        {
            int max = 0;
            for (int i = 0; i < Antes.Length; i++)
                if (Antes[i] > max) max = Antes[i];
            return max;
        }
    }

    public virtual int EstimatedCost => 10 + MaxAnte;
    public abstract string Describe();
    public abstract IMotelySeedFilterDesc CreateDesc();
}

public abstract class RollClause : IRollClause
{
    public string? Label { get; init; }
    public int[] Rolls { get; init; } = [];
    public int Min { get; init; } = 1;
    public int? Max { get; init; }
    public int Score { get; init; }
    public virtual int EstimatedCost => 5;

    public abstract string Describe();
    public abstract IMotelySeedFilterDesc CreateDesc();
}
