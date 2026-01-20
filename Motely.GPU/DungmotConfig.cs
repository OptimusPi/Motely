using System.Text;

namespace Motely.GPU;

/// <summary>
/// Configuration for dungmot GPU prefilter execution.
/// Maps to dungmot CLI arguments.
/// </summary>
public sealed class DungmotConfig
{
    /// <summary>
    /// Path to dungmot executable. Defaults to "dungmot.exe" (expects in PATH or CWD).
    /// </summary>
    public string ExecutablePath { get; set; } = "dungmot.exe";

    /// <summary>
    /// Filter type to run. Maps to dungmot --filter-type argument.
    /// Values: "negative-joker", "negative-legendary", "negative-tag", "negative-rare", "negative-uncommon"
    /// </summary>
    public required string FilterType { get; set; }

    /// <summary>
    /// Joker name (when FilterType is joker-related).
    /// </summary>
    public string? Joker { get; set; }

    /// <summary>
    /// Edition filter (e.g., "negative", "polychrome", "holographic", "foil").
    /// </summary>
    public string? Edition { get; set; }

    /// <summary>
    /// Antes to search (e.g., [1, 2, 3, 4]).
    /// </summary>
    public int[] Antes { get; set; } = [1, 2, 3, 4];

    /// <summary>
    /// Starting batch index for search range.
    /// </summary>
    public long StartBatch { get; set; } = 0;

    /// <summary>
    /// Ending batch index for search range. 0 = no limit.
    /// </summary>
    public long EndBatch { get; set; } = 0;

    /// <summary>
    /// Batch character size (affects granularity).
    /// </summary>
    public int BatchChars { get; set; } = 3;

    /// <summary>
    /// Enable streaming mode (seeds to stdout, progress to stderr).
    /// </summary>
    public bool Stream { get; set; } = true;

    /// <summary>
    /// Optional output database path (for fertilizer mode).
    /// </summary>
    public string? OutputDb { get; set; }

    /// <summary>
    /// Convert config to dungmot CLI argument string.
    /// </summary>
    public string ToArgumentString()
    {
        var sb = new StringBuilder();

        if (Stream)
            sb.Append("--stream ");

        sb.Append($"--filter-type {FilterType} ");

        if (!string.IsNullOrEmpty(Joker))
            sb.Append($"--joker {Joker} ");

        if (!string.IsNullOrEmpty(Edition))
            sb.Append($"--edition {Edition} ");

        if (Antes.Length > 0)
            sb.Append($"--antes {string.Join(",", Antes)} ");

        if (StartBatch > 0)
            sb.Append($"--start-batch {StartBatch} ");

        if (EndBatch > 0)
            sb.Append($"--end-batch {EndBatch} ");

        sb.Append($"--batch-chars {BatchChars} ");

        if (!string.IsNullOrEmpty(OutputDb))
            sb.Append($"--output-db \"{OutputDb}\" ");

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Create a debug-friendly string representation.
    /// </summary>
    public override string ToString()
    {
        return $"[dungmot] {FilterType}: joker={Joker ?? "N/A"}, edition={Edition ?? "any"}, antes=[{string.Join(",", Antes)}]";
    }
}
