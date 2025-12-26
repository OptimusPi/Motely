// iOS-specific console implementation
#if IOS
using System.Runtime.CompilerServices;

namespace Motely;

/// <summary>
/// iOS-specific console implementation
/// iOS may support some console operations, but we use simple output for compatibility
/// </summary>
public partial class FancyConsoleImpl : IMotelyConsole
{
    [MethodImpl(MethodImplOptions.Synchronized)]
    private void WriteBottomLine(string bottomLine)
    {
        // iOS platform - simple console output
        Console.WriteLine($"[STATUS] {bottomLine}");
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    private void ClearBottomLine()
    {
        // iOS platform - no fancy console operations
        // Nothing to clear
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    public void WriteLine(string? message)
    {
        try
        {
            // iOS platform - simple console output
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

