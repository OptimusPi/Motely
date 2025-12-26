// Browser-specific console implementation
#if BROWSER
using System.Runtime.CompilerServices;

namespace Motely;

/// <summary>
/// Browser-specific console implementation
/// Browser doesn't support cursor positioning, so we use simple console output
/// </summary>
public partial class FancyConsoleImpl : IMotelyConsole
{
    [MethodImpl(MethodImplOptions.Synchronized)]
    private void WriteBottomLine(string bottomLine)
    {
        // Browser platform - no fancy console operations, just log
        Console.WriteLine($"[STATUS] {bottomLine}");
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    private void ClearBottomLine()
    {
        // Browser platform - no fancy console operations
        // Nothing to clear in browser console
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    public void WriteLine(string? message)
    {
        try
        {
            // Browser platform - simple console output
            Console.WriteLine(message ?? "null");
        }
        catch (System.IO.IOException)
        {
            // No console available - just write to stdout
            Console.WriteLine(message ?? "null");
        }
    }
}
#endif

