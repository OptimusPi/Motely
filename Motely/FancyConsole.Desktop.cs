// Desktop-specific console implementation with full cursor positioning support
#if !BROWSER && !ANDROID && !IOS
using System.Runtime.CompilerServices;

namespace Motely;

/// <summary>
/// Desktop-specific console implementation
/// Full cursor positioning support for fancy bottom line
/// </summary>
public partial class FancyConsoleImpl : IMotelyConsole
{
    [MethodImpl(MethodImplOptions.Synchronized)]
    private void WriteBottomLine(string bottomLine)
    {
        (int oldLeft, int oldTop) = Console.GetCursorPosition();
        Console.SetCursorPosition(0, Console.BufferHeight - 1);
        Console.Write(new string(' ', Console.BufferWidth));
        Console.SetCursorPosition(0, Console.BufferHeight - 1);
        Console.Write(bottomLine);
        Console.SetCursorPosition(oldLeft, oldTop);
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    private void ClearBottomLine()
    {
        (int oldLeft, int oldTop) = Console.GetCursorPosition();
        Console.SetCursorPosition(0, Console.BufferHeight - 1);
        Console.Write(new string(' ', Console.BufferWidth));
        Console.SetCursorPosition(oldLeft, oldTop);
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    public void WriteLine(string? message)
    {
        try
        {
            (int oldLeft, int oldTop) = Console.GetCursorPosition();

            if (oldTop == Console.BufferHeight - 1)
            {
                ClearBottomLine();
            }

            Console.WriteLine(message ?? "null");

            if (oldTop == Console.BufferHeight - 1)
            {
                SetBottomLine(_bottomLine);
            }
        }
        catch (System.IO.IOException)
        {
            // No console available (e.g., running in test environment) - just write to stdout
            Console.WriteLine(message ?? "null");
        }
    }
}
#endif

