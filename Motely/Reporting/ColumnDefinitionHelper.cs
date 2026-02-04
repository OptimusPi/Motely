using Motely.Filters;

namespace Motely.Reporting;

/// <summary>
/// Helper class to create column definitions from config
/// </summary>
public static class ColumnDefinitionHelper
{
    /// <summary>
    /// Create column definitions from a config's Should clauses
    /// Each Should clause becomes a ScoreTally column
    /// </summary>
    public static List<IColumnDefinition> CreateFromShouldClauses(MotelyJsonConfig config)
    {
        var columns = new List<IColumnDefinition>();

        if (config.Should == null || config.Should.Count == 0)
            return columns;

        // Track used names to ensure uniqueness (case-insensitive for SQL compatibility)
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var clause in config.Should)
        {
            var baseName = GetClauseColumnName(clause);

            // Ensure unique column name by adding suffix if duplicate
            var columnName = baseName;
            int suffix = 2;
            while (usedNames.Contains(columnName))
            {
                columnName = $"{baseName}_{suffix++}";
            }
            usedNames.Add(columnName);

            // Check if this is a ValueFunction column
            var mode = clause.Mode?.ToLowerInvariant();
            if (mode == "value" || mode == "function" || (!string.IsNullOrEmpty(clause.Function)))
            {
                // ValueFunction column
                var scorer = new ValueFunctionScorer(clause);
                var column = new ColumnDefinition(
                    name: columnName,
                    type: ColumnType.ValueFunction,
                    scorers: new[] { scorer },
                    aggregationStrategy: new SumAllStrategy() // Not used for ValueFunction, but required
                );
                columns.Add(column);
            }
            else
            {
                // Regular ScoreTally column
                var scorer = new FilterClauseScorer(clause);
                var column = new ColumnDefinition(
                    name: columnName,
                    type: ColumnType.ScoreTally,
                    scorers: new[] { scorer },
                    aggregationStrategy: new SumAllStrategy()
                );
                columns.Add(column);
            }
        }

        return columns;
    }

    /// <summary>
    /// Generate a human-readable column name for a filter clause
    /// (Matches logic from MotelyJsonConfig.GetClauseColumnName)
    /// </summary>
    private static string GetClauseColumnName(MotelyJsonConfig.MotelyJsonFilterClause clause)
    {
        // Use label if provided (highest priority - keep original formatting!)
        if (!string.IsNullOrEmpty(clause.Label))
            return clause.Label;

        // Handle OR/AND clauses with compact notation
        if (
            (clause.Type?.ToLower() == "or" || clause.Type?.ToLower() == "and")
            && clause.Clauses != null
            && clause.Clauses.Count > 0
        )
        {
            var clauseType = clause.Type.ToUpper();
            var count = clause.Clauses.Count;
            var anteSuffix = "";
            if (clause.Antes != null && clause.Antes.Length > 0 && clause.Antes.Length < 8)
            {
                var minAnte = clause.Antes.Min();
                var maxAnte = clause.Antes.Max();
                anteSuffix = minAnte == maxAnte ? $" A{minAnte}" : $" A{minAnte}-{maxAnte}";
            }

            return $"{count} {clauseType}{anteSuffix}";
        }

        // Build name from value/type
        string name;
        if (!string.IsNullOrEmpty(clause.Value))
        {
            // Special handling for wildcards (Any)
            if (clause.Value.Equals("Any", StringComparison.OrdinalIgnoreCase))
            {
                name = $"Any_{clause.Type}";
            }
            else
            {
                name = clause.Value;
            }
        }
        else if (clause.Values != null && clause.Values.Length > 0)
        {
            // Multi-value case: Use first value + count indicator
            if (clause.Values.Length == 1)
            {
                name = clause.Values[0];
            }
            else
            {
                // Multiple values: create descriptive name
                name = $"{clause.Values[0]}_Plus{clause.Values.Length - 1}More";
            }
        }
        else
        {
            // Fallback to type
            name = clause.Type ?? "Unknown";
        }

        // Add edition prefix if specified
        if (!string.IsNullOrEmpty(clause.Edition))
            name = clause.Edition + " " + name;

        // Add ante suffix if specified (human-readable range format)
        if (clause.Antes != null && clause.Antes.Length > 0 && clause.Antes.Length < 8)
        {
            var minAnte = clause.Antes.Min();
            var maxAnte = clause.Antes.Max();
            name += minAnte == maxAnte ? $" A{minAnte}" : $" A{minAnte}-{maxAnte}";
        }

        return name;
    }
}
