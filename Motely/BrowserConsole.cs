using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.JavaScript;

namespace Motely;

/// <summary>
/// Browser-specific console implementation that logs to browser console
/// </summary>
public class BrowserConsole : IMotelyConsole
{
    private static bool _isEnabled = true;
    private static readonly BrowserConsole _instance = new();

    public static BrowserConsole Instance => _instance;

    // IMotelyConsole implementation
    public bool IsEnabled 
    { 
        get => _isEnabled; 
        set => _isEnabled = value; 
    }

    private BrowserConsole() { } // Private constructor for singleton

    public void SetBottomLine(string? bottomLine)
    {
        // Browser doesn't have a bottom line concept, so we just log it
        if (_isEnabled && bottomLine != null)
        {
            WriteLine($"[STATUS] {bottomLine}");
        }
    }

    public void WriteLine<T>(T message)
    {
        WriteLine(message?.ToString() ?? null);
    }

    public void WriteLine(string? message)
    {
        if (!_isEnabled) return;

        try
        {
            // Use browser console logging when available
            if (OperatingSystem.IsBrowser())
            {
                Console.WriteLine($"[Motely] {message ?? "null"}");
            }
            else
            {
                // Fallback to standard console
                Console.WriteLine($"[Motely] {message ?? "null"}");
            }
        }
        catch
        {
            // Ultimate fallback - just try to write to console
            Console.WriteLine($"[Motely] {message ?? "null"}");
        }
    }
}
