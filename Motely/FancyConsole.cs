using System.Runtime.CompilerServices;

namespace Motely;

/// <summary>
/// Console implementation with fancy bottom line support
/// Platform-specific implementations:
/// - Desktop: FancyConsole.Desktop.cs (full cursor positioning support)
/// - Browser: FancyConsole.Browser.cs (simple console output)
/// - Android: FancyConsole.Android.cs (simple console output)
/// - iOS: FancyConsole.iOS.cs (simple console output)
/// </summary>
public partial class FancyConsoleImpl : IMotelyConsole
{
    public static bool IsEnabled { get; set; } = true;
    protected static string? _bottomLine;
    protected static string? _lastPrintedBottomLine;
    private static readonly FancyConsoleImpl _instance = new();

    public static FancyConsoleImpl Instance => _instance;

    // IMotelyConsole implementation
    bool IMotelyConsole.IsEnabled
    {
        get => IsEnabled;
        set => IsEnabled = value;
    }

    protected FancyConsoleImpl() { } // Protected constructor for inheritance

    [MethodImpl(MethodImplOptions.Synchronized)]
    public void SetBottomLine(string? bottomLine)
    {
        _bottomLine = bottomLine;

        if (!IsEnabled)
            return;

        if (_bottomLine != null)
        {
            WriteBottomLine(_bottomLine);
        }
        else
        {
            ClearBottomLine();
        }
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    public void WriteLine<T>(T message)
    {
        WriteLine(message?.ToString() ?? null);
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    public void WriteLine(string? message)
    {
        if (!IsEnabled)
        {
            Console.WriteLine(message ?? "null");
            return;
        }

        // Simple fallback: just write message, don't re-print bottom line
        // (no cursor positioning = re-printing is useless and spammy)
        // Platform-specific implementations with cursor support override this
        Console.WriteLine(message ?? "null");
    }

    // WriteBottomLine and ClearBottomLine are implemented in platform-specific partial files
    protected virtual void WriteBottomLine(string bottomLine)
    {
        // Default: only print if the value changed (no cursor positioning in fallback)
        if (bottomLine != _lastPrintedBottomLine)
        {
            Console.WriteLine(bottomLine);
            _lastPrintedBottomLine = bottomLine;
        }
    }

    protected virtual void ClearBottomLine()
    {
        // Default: no-op (platform-specific can override for cursor positioning)
        // Reset tracking so next different value will print
        _lastPrintedBottomLine = null;
    }
}

/// <summary>
/// Static facade for console output - routes to Desktop impl when available
/// </summary>
public static class FancyConsole
{
    /// <summary>
    /// Global lock for all console output to prevent interleaved writes from multiple threads
    /// </summary>
    public static readonly object ConsoleLock = new();

    // Use base implementation - cursor positioning is too unreliable across terminals
    private static readonly FancyConsoleImpl _impl = FancyConsoleImpl.Instance;

    public static bool IsEnabled
    {
        get => FancyConsoleImpl.IsEnabled;
        set => FancyConsoleImpl.IsEnabled = value;
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    public static void SetBottomLine(string? bottomLine)
    {
        lock (ConsoleLock)
        {
            _impl.SetBottomLine(bottomLine);
        }
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    public static void WriteLine<T>(T message)
    {
        if (!IsEnabled)
        {
            Console.WriteLine(message?.ToString() ?? "null");
            return;
        }

        lock (ConsoleLock)
        {
            _impl.WriteLine(message?.ToString());
        }
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    public static void WriteLine(string? message)
    {
        if (!IsEnabled)
        {
            Console.WriteLine(message ?? "null");
            return;
        }

        lock (ConsoleLock)
        {
            _impl.WriteLine(message);
        }
    }
}
