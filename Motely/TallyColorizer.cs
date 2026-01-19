using System.Runtime.InteropServices;
using System.Text;

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
        { 0, "\u001b[38;5;17m" }, // Dark blue for 0
        { 1, "\u001b[38;5;54m" }, // Purple for 1
        { 2, "\u001b[38;5;196m" }, // Red for 2
        { 3, "\u001b[38;5;208m" }, // Orange for 3
        { 4, "\u001b[38;5;226m" }, // Yellow for 4
        { 5, "\u001b[38;5;46m" }, // Green for 5
        { 6, "\u001b[38;5;51m" }, // Cyan for 6
        { 7, "\u001b[38;5;201m" }, // Magenta for 7
        { 8, "\u001b[38;5;231m" }, // White for 8+
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
        if (
            Environment.OSVersion.Platform == PlatformID.Unix
            || Environment.OSVersion.Platform == PlatformID.MacOSX
        )
            return true;

        return false;
    }

    private static bool? _colorEnabled = null;

    /// <summary>
    /// Gets or sets whether color output is enabled
    /// </summary>
    public static bool ColorEnabled
    {
        get
        {
            // If explicitly set, use that value
            if (_colorEnabled.HasValue)
                return _colorEnabled.Value;

            // Check if NO_COLOR is set (standard way to disable colors)
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR")))
                return false;

            // For Windows, be more aggressive - Windows 10+ supports ANSI colors
            // Even if TERM=dumb, modern Windows terminals (PowerShell, Windows Terminal) support colors
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                var osVersion = Environment.OSVersion.Version;
                if (osVersion.Major >= 10)
                {
                    // Windows 10+ - assume colors work (terminal will handle it)
                    // TERM=dumb is often set but doesn't mean colors don't work
                    return true;
                }
            }

            // Check environment variables for modern terminals
            var wtSession = Environment.GetEnvironmentVariable("WT_SESSION");
            var termProgram = Environment.GetEnvironmentVariable("TERM_PROGRAM");
            var term = Environment.GetEnvironmentVariable("TERM");

            if (!string.IsNullOrEmpty(wtSession)) // Windows Terminal
                return true;
            if (termProgram?.Contains("vscode", StringComparison.OrdinalIgnoreCase) == true) // VS Code
                return true;
            if (
                !string.IsNullOrEmpty(term)
                && (term.Contains("color") || term.Contains("256") || term.Contains("xterm"))
            )
                return true;

            // Unix-like systems usually support colors
            if (
                Environment.OSVersion.Platform == PlatformID.Unix
                || Environment.OSVersion.Platform == PlatformID.MacOSX
            )
                return true;

            // Default: enable colors (let terminal handle it - most modern terminals support ANSI)
            // If the terminal doesn't support colors, ANSI codes will just be ignored
            return true;
        }
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
    public static string ColorizeTallies(ReadOnlySpan<int> tallies)
    {
        if (!ColorEnabled)
        {
            // No colors - format as CSV
            if (tallies.Length == 0)
                return string.Empty;

            var sb = new StringBuilder(tallies.Length * 3);
            for (int i = 0; i < tallies.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(tallies[i]);
            }
            return sb.ToString();
        }

        // Zero-allocation path using stack-allocated span
        if (tallies.Length == 0)
            return string.Empty;

        Span<char> buffer = stackalloc char[tallies.Length * 16]; // 16 chars per value max
        var writer = new SpanWriter(buffer);
        bool first = true;

        foreach (var tally in tallies)
        {
            if (!first)
                writer.Write(',');
            first = false;

            int colorKey = Math.Max(0, Math.Min(8, tally));
            if (TallyColors.TryGetValue(colorKey, out var color))
            {
                writer.Write(color);
                writer.Write(tally);
                writer.Write(ResetColor);
            }
            else
            {
                writer.Write(tally);
            }
        }

        return writer.ToString();
    }

    public static string ColorizeTallies(IEnumerable<int> tallies)
    {
        if (!ColorEnabled)
            return string.Join(",", tallies);

        // Zero-allocation path for collections
        if (tallies is ICollection<int> collection)
        {
            if (collection.Count == 0)
                return string.Empty;

            Span<char> buffer = stackalloc char[collection.Count * 16]; // 16 chars per value max
            var writer = new SpanWriter(buffer);
            bool first = true;

            foreach (var tally in collection)
            {
                if (!first)
                    writer.Write(',');
                first = false;

                int colorKey = Math.Max(0, Math.Min(8, tally));
                if (TallyColors.TryGetValue(colorKey, out var color))
                {
                    writer.Write(color);
                    writer.Write(tally);
                    writer.Write(ResetColor);
                }
                else
                {
                    writer.Write(TallyColors[8]);
                    writer.Write(tally);
                    writer.Write(ResetColor);
                }
            }

            return writer.ToString();
        }

        // Fallback for unknown collection types (rare)
        return string.Join(",", tallies.Select(ColorizeTally));
    }

    // Minimal span-based string writer for zero-allocation formatting
    private ref struct SpanWriter
    {
        private Span<char> _buffer;
        private int _position;

        public SpanWriter(Span<char> buffer)
        {
            _buffer = buffer;
            _position = 0;
        }

        public void Write(char c)
        {
            if (_position < _buffer.Length)
                _buffer[_position++] = c;
        }

        public void Write(string s)
        {
            if (_position + s.Length <= _buffer.Length)
            {
                s.AsSpan().CopyTo(_buffer.Slice(_position));
                _position += s.Length;
            }
        }

        public void Write(int value)
        {
            if (_position < _buffer.Length)
            {
                if (value.TryFormat(_buffer.Slice(_position), out int written))
                    _position += written;
            }
        }

        public override string ToString() => _buffer.Slice(0, _position).ToString();
    }

    /// <summary>
    /// Format a complete result line with colored tallies (zero-allocation span version for .NET 10+)
    /// </summary>
    public static string FormatResultLine(string seed, int score, ReadOnlySpan<int> tallies)
    {
        if (!ColorEnabled)
        {
            // No colors - format as CSV
            var sb = new StringBuilder(seed.Length + 10 + tallies.Length * 3);
            sb.Append(seed).Append(',').Append(score).Append(',');
            for (int i = 0; i < tallies.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(tallies[i]);
            }
            return sb.ToString();
        }

        // Use ColorizeTallies for colored output
        return $"{seed},{score},{ColorizeTallies(tallies)}{ResetColor}";
    }

    /// <summary>
    /// Format a complete result line with colored tallies (legacy List version)
    /// </summary>
    public static string FormatResultLine(string seed, int score, IEnumerable<int> tallies)
    {
        if (!ColorEnabled)
            return $"{seed},{score},{string.Join(",", tallies)}";

        return $"{seed},{score},{ColorizeTallies(tallies)}{ResetColor}";
    }

    /// <summary>
    /// Format a result line with visual ASCII block bars for tallies.
    /// Shows tallies as colored Unicode block characters (░▒▓█) for easy visualization.
    /// Perfect for visualizing seed search results at a glance!
    /// </summary>
    public static string FormatResultLineWithBlocks(
        string seed,
        int score,
        IEnumerable<int> tallies,
        int maxBlockWidth = 40
    )
    {
        bool useColor = ColorEnabled;
        var blockBar = FormatTallyBlocks(tallies, maxBlockWidth, useColor);
        return $"{seed} | Score: {score} | {blockBar}";
    }

    /// <summary>
    /// Format tallies as a visual block bar using Unicode block characters.
    /// Each tally value is represented by a proportional block height.
    /// </summary>
    private static string FormatTallyBlocks(IEnumerable<int> tallies, int maxWidth, bool useColor)
    {
        var tallyList = tallies is ICollection<int> coll ? coll : tallies.ToList();
        if (tallyList.Count == 0)
            return string.Empty;

        // Find max value for normalization
        int maxValue = tallyList.Max();
        if (maxValue == 0)
            return new string('░', maxWidth); // All empty

        // Unicode block characters: ░ (light) ▒ (medium-light) ▓ (medium-dark) █ (full)
        const char blockEmpty = '░';
        const char blockLight = '▒';
        const char blockMedium = '▓';
        const char blockFull = '█';

        var result = new StringBuilder(maxWidth * tallyList.Count);
        int blockIndex = 0;

        foreach (var tally in tallyList)
        {
            if (blockIndex >= maxWidth)
                break;

            // Normalize tally to 0-4 range for block selection
            int normalized = maxValue > 0 ? (int)((double)tally / maxValue * 4) : 0;
            normalized = Math.Max(0, Math.Min(4, normalized));

            char blockChar = normalized switch
            {
                0 => blockEmpty,
                1 => blockLight,
                2 => blockMedium,
                3 => blockFull,
                4 => blockFull,
                _ => blockEmpty,
            };

            if (useColor)
            {
                // Apply color based on tally value
                int colorKey = Math.Max(0, Math.Min(8, tally));
                if (TallyColors.TryGetValue(colorKey, out var color))
                {
                    result.Append(color);
                    result.Append(blockChar);
                    result.Append(ResetColor);
                }
                else
                {
                    result.Append(TallyColors[8]);
                    result.Append(blockChar);
                    result.Append(ResetColor);
                }
            }
            else
            {
                result.Append(blockChar);
            }

            blockIndex++;
        }

        return result.ToString();
    }

    /// <summary>
    /// Format a complete result line with colored tallies (List version)
    /// </summary>
    public static string FormatResultLine(string seed, int score, List<int> tallies)
    {
        return FormatResultLine(seed, score, (IEnumerable<int>)tallies);
    }
}
