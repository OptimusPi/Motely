namespace Motely.Filters.Jaml;

/// <summary>Common scalar contract shared by all flat clause types.</summary>
public interface IJamlClause
{
    string? Label { get; set; }
    int Min { get; set; }
    int? Max { get; set; }
    int Score { get; set; }
}
