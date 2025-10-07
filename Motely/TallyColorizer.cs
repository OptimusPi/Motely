using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace Motely;

/// <summary>
/// Helper class to colorize tally values for terminal output
/// </summary>
public static class TallyColorizer
{
    // Windows API for enabling ANSI colors
    private const int STD_OUTPUT_HANDLE = -11;
    private const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll")]
    private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

    [DllImport("kernel32.dll")]
    private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

    static TallyColorizer()
    {
        // Try to enable ANSI support on Windows
        try
        {
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                var handle = GetStdHandle(STD_OUTPUT_HANDLE);
                if (handle != IntPtr.Zero && handle != new IntPtr(-1))
                {
                    if (GetConsoleMode(handle, out uint mode))
                    {
                        mode |= ENABLE_VIRTUAL_TERMINAL_PROCESSING;
                        SetConsoleMode(handle, mode);
                    }
                }
            }
        }
        catch
        {
            // Ignore failures - colors just won't work
        }
    }
    // ANSI color codes for different tally values
    private static readonly Dictionary<int, string> TallyColors = new()
    {
        { 0, "\u001b[38;5;17m" },   // Dark blue for 0
        { 1, "\u001b[38;5;54m" },   // Purple for 1
        { 2, "\u001b[38;5;196m" },  // Red for 2
        { 3, "\u001b[38;5;208m" },  // Orange for 3
        { 4, "\u001b[38;5;226m" },  // Yellow for 4
        { 5, "\u001b[38;5;46m" },   // Green for 5
        { 6, "\u001b[38;5;51m" },   // Cyan for 6
        { 7, "\u001b[38;5;201m" },  // Magenta for 7
        { 8, "\u001b[38;5;231m" },  // White for 8+
    };

    private const string ResetColor = "\u001b[0m";

    /// <summary>
    /// Check if the terminal supports ANSI colors
    /// </summary>
    public static bool IsColorSupported()
    {
        // Check if NO_COLOR env var is set (standard way to disable colors)
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR")))
            return false;

        // Check if running in Windows Terminal, VS Code, or other modern terminals
        var term = Environment.GetEnvironmentVariable("TERM");
        var termProgram = Environment.GetEnvironmentVariable("TERM_PROGRAM");
        var wtSession = Environment.GetEnvironmentVariable("WT_SESSION");

        // Windows Terminal
        if (!string.IsNullOrEmpty(wtSession))
            return true;

        // VS Code integrated terminal
        if (termProgram?.Contains("vscode", StringComparison.OrdinalIgnoreCase) == true)
            return true;

        // Check for color support in TERM variable
        if (!string.IsNullOrEmpty(term) && (term.Contains("color") || term.Contains("256")))
            return true;

        // On Windows, check if virtual terminal processing is available
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            try
            {
                // Modern Windows 10/11 supports ANSI by default
                var osVersion = Environment.OSVersion.Version;
                if (osVersion.Major >= 10)
                    return true;
            }
            catch { }
        }

        // Unix-like systems usually support colors
        if (Environment.OSVersion.Platform == PlatformID.Unix ||
            Environment.OSVersion.Platform == PlatformID.MacOSX)
            return true;

        return false;
    }

    private static bool? _colorEnabled = null;

    /// <summary>
    /// Gets or sets whether color output is enabled
    /// </summary>
    public static bool ColorEnabled
    {
        get => _colorEnabled ?? IsColorSupported();
        set => _colorEnabled = value;
    }

    /// <summary>
    /// Colorize a single tally value
    /// </summary>
    public static string ColorizeTally(int value)
    {
        if (!ColorEnabled)
            return value.ToString();

        // Clamp value to 0-8 range for color selection
        int colorKey = Math.Max(0, Math.Min(8, value));

        if (TallyColors.TryGetValue(colorKey, out var color))
        {
            return $"{color}{value}{ResetColor}";
        }

        // Fallback for values > 8
        return $"{TallyColors[8]}{value}{ResetColor}";
    }

    /// <summary>
    /// Colorize a list of tally values for CSV output
    /// </summary>
    public static string ColorizeTallies(IEnumerable<int> tallies)
    {
        if (!ColorEnabled)
            return string.Join(",", tallies);

        return string.Join(",", tallies.Select(ColorizeTally));
    }

    /// <summary>
    /// Format a complete result line with colored tallies
    /// </summary>
    public static string FormatResultLine(string seed, int score, IEnumerable<int> tallies)
    {
        if (!ColorEnabled)
            return $"{seed},{score},{string.Join(",", tallies)}";

        return $"{seed},{score},{ColorizeTallies(tallies)}";
    }

    /// <summary>
    /// Format a complete result line with colored tallies (List version)
    /// </summary>
    public static string FormatResultLine(string seed, int score, List<int> tallies)
    {
        return FormatResultLine(seed, score, (IEnumerable<int>)tallies);
    }
}