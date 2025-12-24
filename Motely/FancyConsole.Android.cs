// Android-specific console implementation
#if ANDROID
using System.Runtime.CompilerServices;

namespace Motely;

/// <summary>
/// Android-specific console implementation
/// Android may support some console operations, but we use simple output for compatibility
/// </summary>
public partial class FancyConsoleImpl : IMotelyConsole
{
    [MethodImpl(MethodImplOptions.Synchronized)]
    private void WriteBottomLine(string bottomLine)
    {
        // Android platform - simple console output
        Console.WriteLine($"[STATUS] {bottomLine}");
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    private void ClearBottomLine()
    {
        // Android platform - no fancy console operations
        // Nothing to clear
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    public void WriteLine(string? message)
    {
        try
        {
            // Android platform - simple console output
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

