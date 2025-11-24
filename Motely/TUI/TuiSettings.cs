using System;

namespace Motely.TUI;

/// <summary>
/// Runtime settings for TUI searches and API server
/// </summary>
public static class TuiSettings
{
    // Thread settings
    public static int ThreadCount { get; set; } = Environment.ProcessorCount;

    // Batch settings
    public static int BatchCharacterCount { get; set; } = 2;

    // API Server settings
    public static string ApiServerHost { get; set; } = "localhost";
    public static int ApiServerPort { get; set; } = 3141;

    /// <summary>
    /// Reset all settings to defaults
    /// </summary>
    public static void ResetToDefaults()
    {
        ThreadCount = Environment.ProcessorCount;
        BatchCharacterCount = 2;
        ApiServerHost = "localhost";
        ApiServerPort = 3141;
    }
}
