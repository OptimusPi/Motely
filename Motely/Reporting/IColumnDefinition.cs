using Motely.Filters;

namespace Motely.Reporting;

/// <summary>
/// Defines a column in the output CSV/DuckDB with its type, aggregation strategy, and formatters
/// </summary>
public interface IColumnDefinition
{
    /// <summary>
    /// Column name for CSV/DuckDB output
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Type of column (determines output format and behavior)
    /// </summary>
    ColumnType Type { get; }

    /// <summary>
    /// List of scorers that provide input data for this column
    /// </summary>
    IReadOnlyList<IScorer> Scorers { get; }

    /// <summary>
    /// Strategy for aggregating multiple scorer results
    /// </summary>
    IReportingStrategy? AggregationStrategy { get; }

    /// <summary>
    /// Evaluate the column for a given seed context
    /// </summary>
    /// <param name="ctx">Single search context for the seed</param>
    /// <param name="runState">Current run state</param>
    /// <returns>Formatted value for CSV output</returns>
    string? Evaluate(ref MotelySingleSearchContext ctx, ref MotelyRunState runState);
}

/// <summary>
/// Implementation of column definition with pipeline support
/// </summary>
public class ColumnDefinition : IColumnDefinition
{
    public string Name { get; }
    public ColumnType Type { get; }
    public IReadOnlyList<IScorer> Scorers { get; }
    public IReportingStrategy? AggregationStrategy { get; }

    // InlineLabel-specific properties
    public string? DefaultValue { get; set; }
    public string? Prefix { get; set; }
    public string? Suffix { get; set; }

    // AnteDisplay-specific properties
    public AnteDisplayFormat AnteFormat { get; set; } = AnteDisplayFormat.FirstFound;

    // CoalesceStrategy delimiter (if using CoalesceStrategy)
    public string? CoalesceDelimiter { get; set; }

    public ColumnDefinition(
        string name,
        ColumnType type,
        IReadOnlyList<IScorer> scorers,
        IReportingStrategy? aggregationStrategy = null
    )
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Type = type;
        Scorers = scorers ?? throw new ArgumentNullException(nameof(scorers));
        AggregationStrategy = aggregationStrategy;

        // Set default aggregation strategy based on column type if not provided
        if (AggregationStrategy == null)
        {
            AggregationStrategy = Type switch
            {
                ColumnType.ScoreTally => new SumAllStrategy(),
                ColumnType.InlineLabel => new FirstMatchStrategy(),
                ColumnType.AnteDisplay => new FirstMatchStrategy(),
                ColumnType.ItemDisplay => new FirstMatchStrategy(),
                _ => new SumAllStrategy()
            };
        }

        // Set default value for InlineLabel (user requested SPACE by default)
        if (Type == ColumnType.InlineLabel && DefaultValue == null)
        {
            DefaultValue = " ";
        }
    }

    public string? Evaluate(ref MotelySingleSearchContext ctx, ref MotelyRunState runState)
    {
        // Run all scorers
        var results = new List<object?>();
        foreach (var scorer in Scorers)
        {
            // Create a copy of runState for each scorer to avoid mutations affecting other scorers
            var scorerRunState = runState;
            var result = scorer.Evaluate(ref ctx, ref scorerRunState);
            results.Add(result);
        }

        // Aggregate results
        var aggregated = AggregationStrategy?.Aggregate(results);

        // Format based on column type
        return FormatValue(aggregated);
    }

    private string? FormatValue(object? value)
    {
        if (value == null)
        {
            return Type == ColumnType.InlineLabel ? DefaultValue : null;
        }

        return Type switch
        {
            ColumnType.ScoreTally => FormatScoreTally(value),
            ColumnType.InlineLabel => FormatInlineLabel(value),
            ColumnType.AnteDisplay => FormatAnteDisplay(value),
            ColumnType.ItemDisplay => FormatItemDisplay(value),
            _ => value.ToString()
        };
    }

    private string FormatScoreTally(object value)
    {
        // ScoreTally outputs integer
        return ConvertToInt(value)?.ToString() ?? "0";
    }

    private string FormatInlineLabel(object value)
    {
        var str = value.ToString() ?? DefaultValue ?? " ";
        
        // Apply prefix/suffix
        if (!string.IsNullOrEmpty(Prefix))
            str = Prefix + str;
        if (!string.IsNullOrEmpty(Suffix))
            str = str + Suffix;

        return str;
    }

    private string FormatAnteDisplay(object value)
    {
        // AnteDisplay can output integer or array string
        return AnteFormat switch
        {
            AnteDisplayFormat.FirstFound => ConvertToInt(value)?.ToString() ?? "",
            AnteDisplayFormat.AllList => FormatAnteList(value),
            AnteDisplayFormat.BestFound => ConvertToInt(value)?.ToString() ?? "",
            _ => value.ToString() ?? ""
        };
    }

    private string FormatAnteList(object value)
    {
        // If value is already a collection, join with pipe
        if (value is IEnumerable<object> collection)
        {
            return string.Join("|", collection.Select(v => v.ToString()));
        }

        // Otherwise, just return the value
        return value.ToString() ?? "";
    }

    private string FormatItemDisplay(object value)
    {
        // ItemDisplay outputs string
        return value.ToString() ?? "";
    }

    private static int? ConvertToInt(object? value)
    {
        return value switch
        {
            int i => i,
            long l => (int)l,
            float f => (int)Math.Round(f),
            double d => (int)Math.Round(d),
            decimal dec => (int)dec,
            string s when int.TryParse(s, out var parsed) => parsed,
            _ => null
        };
    }
}

/// <summary>
/// Formatting strategy for AnteDisplay columns
/// </summary>
public enum AnteDisplayFormat
{
    /// <summary>
    /// Returns first ante number (e.g., 2)
    /// </summary>
    FirstFound,

    /// <summary>
    /// Returns ante with highest associated score
    /// </summary>
    BestFound,

    /// <summary>
    /// Returns pipe-delimited list (e.g., "2|5|8")
    /// </summary>
    AllList
}
