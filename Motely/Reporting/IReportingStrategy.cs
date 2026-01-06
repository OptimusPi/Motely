namespace Motely.Reporting;

/// <summary>
/// Strategy for aggregating multiple scorer results into a single column value
/// </summary>
public interface IReportingStrategy
{
    /// <summary>
    /// Aggregate results from multiple scorers into a single value
    /// </summary>
    /// <param name="results">Results from all attached scorers</param>
    /// <returns>Aggregated value (type depends on column type)</returns>
    object? Aggregate(IEnumerable<object?> results);
}

/// <summary>
/// The "Best" Logic: Returns the highest single value returned by any scorer.
/// Use Case: "Score of 5 from Scorer A vs Score of 2 from Scorer B -> Result: 5"
/// </summary>
public class MaxOfStrategy : IReportingStrategy
{
    public object? Aggregate(IEnumerable<object?> results)
    {
        var values = results
            .Where(r => r != null)
            .Select(r => ConvertToComparable(r))
            .Where(v => v.HasValue)
            .ToList();

        if (values.Count == 0)
            return null;

        return values.Max()!.Value;
    }

    private static double? ConvertToComparable(object? value)
    {
        return value switch
        {
            int i => i,
            long l => l,
            float f => f,
            double d => d,
            decimal dec => (double)dec,
            string s when double.TryParse(s, out var parsed) => parsed,
            _ => null
        };
    }
}

/// <summary>
/// Sum Logic: Sums all scorer results.
/// Use Case: "Total Value = (Value of Jokers) + (Value of Consumables)"
/// </summary>
public class SumAllStrategy : IReportingStrategy
{
    public object? Aggregate(IEnumerable<object?> results)
    {
        double sum = 0;
        bool hasValue = false;

        foreach (var result in results)
        {
            if (result == null) continue;

            var num = ConvertToNumber(result);
            if (num.HasValue)
            {
                sum += num.Value;
                hasValue = true;
            }
        }

        return hasValue ? (int)Math.Round(sum) : null;
    }

    private static double? ConvertToNumber(object? value)
    {
        return value switch
        {
            int i => i,
            long l => l,
            float f => f,
            double d => d,
            decimal dec => (double)dec,
            string s when double.TryParse(s, out var parsed) => parsed,
            _ => null
        };
    }
}

/// <summary>
/// The "Or" Logic: Returns the first non-zero/non-null result.
/// Use Case: "Did we find a Perkeo? If yes, report 'Perkeo'. If no, did we find a Blueprint? Report 'Blueprint'. Else 'None'."
/// </summary>
public class FirstMatchStrategy : IReportingStrategy
{
    public object? Aggregate(IEnumerable<object?> results)
    {
        foreach (var result in results)
        {
            if (result == null) continue;

            // For numeric values, treat 0 as null
            if (IsNumericZero(result))
                continue;

            // For strings, treat empty as null
            if (result is string str && string.IsNullOrWhiteSpace(str))
                continue;

            return result;
        }

        return null;
    }

    private static bool IsNumericZero(object value)
    {
        return value switch
        {
            int i => i == 0,
            long l => l == 0,
            float f => Math.Abs(f) < float.Epsilon,
            double d => Math.Abs(d) < double.Epsilon,
            decimal dec => dec == 0,
            _ => false
        };
    }
}

/// <summary>
/// String Joining Logic: Collects all non-null strings and joins them with a delimiter.
/// Use Case: Editions_Found: "Holographic + Polychrome"
/// </summary>
public class CoalesceStrategy : IReportingStrategy
{
    private readonly string _delimiter;

    public CoalesceStrategy(string delimiter = " + ")
    {
        _delimiter = delimiter;
    }

    public object? Aggregate(IEnumerable<object?> results)
    {
        var strings = results
            .Where(r => r != null)
            .Select(r => r is string s ? s : r?.ToString())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        if (strings.Count == 0)
            return null;

        return string.Join(_delimiter, strings);
    }
}
