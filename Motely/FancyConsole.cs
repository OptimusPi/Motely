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
    private static readonly FancyConsoleImpl _instance = new();

    public static FancyConsoleImpl Instance => _instance;

    // IMotelyConsole implementation
    bool IMotelyConsole.IsEnabled 
    { 
        get => IsEnabled; 
        set => IsEnabled = value; 
    }

    private FancyConsoleImpl() { } // Private constructor for singleton

    [MethodImpl(MethodImplOptions.Synchronized)]
    public void SetBottomLine(string? bottomLine)
    {
        _bottomLine = bottomLine;

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

    // WriteBottomLine, ClearBottomLine, and WriteLine(string) are implemented in platform-specific partial files
}

/// <summary>
/// Static facade for backward compatibility
/// </summary>
public static class FancyConsole
{
    public static bool IsEnabled { get; set; } = true;

    [MethodImpl(MethodImplOptions.Synchronized)]
    public static void SetBottomLine(string? bottomLine)
    {
        FancyConsoleImpl.Instance.SetBottomLine(bottomLine);
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    public static void WriteLine<T>(T message)
    {
        FancyConsoleImpl.Instance.WriteLine(message);
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    public static void WriteLine(string? message)
    {
        FancyConsoleImpl.Instance.WriteLine(message);
    }
}