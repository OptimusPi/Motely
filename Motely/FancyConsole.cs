
using System.Runtime.CompilerServices;

namespace Motely;

public static class FancyConsole
{
    public static bool IsEnabled { get; set; } = true;

    private static string? _bottomLine;

    private static void WriteBottomLine(string bottomLine)
    {
        try
        {
            (int oldLeft, int oldTop) = Console.GetCursorPosition();
            
            // Don't try to write to bottom if window is too small
            if (Console.WindowHeight < 1) return;
            
            int bottomRow = Console.CursorTop >= Console.WindowHeight - 1 
                ? Console.WindowHeight - 1 
                : Console.WindowHeight - 1;
                
            Console.SetCursorPosition(0, bottomRow);
            // Clear the line first
            Console.Write(new string(' ', Math.Min(bottomLine.Length + 20, Console.WindowWidth - 1)));
            Console.SetCursorPosition(0, bottomRow);
            Console.Write(bottomLine);
            Console.SetCursorPosition(oldLeft, oldTop);
        }
        catch
        {
            // In test environments, console operations may fail - ignore
        }
    }

    private static void ClearBottomLine()
    {
        try
        {
            (int oldLeft, int oldTop) = Console.GetCursorPosition();
            Console.SetCursorPosition(0, Console.BufferHeight - 1);
            Console.Write(new string(' ', Console.BufferWidth));
            Console.SetCursorPosition(oldLeft, oldTop);
        }
        catch
        {
            // In test environments, console operations may fail - ignore
        }
    }


    public static void SetBottomLine(string? bottomLine)
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

    public static void WriteLine<T>(T message)
    {
        WriteLine(message?.ToString() ?? null);
    }


    [MethodImpl(MethodImplOptions.Synchronized)]
    public static void WriteLine(string? message)
    {
        if (!IsEnabled)
        {
            Console.WriteLine($"\r{message ?? "null"}");
            return;
        }

        try
        {
            (int oldLeft, int oldTop) = Console.GetCursorPosition();

            // If we're about to write to the bottom line, we need to scroll first
            if (oldTop >= Console.BufferHeight - 2)
            {
                // Clear the bottom line
                ClearBottomLine();
                
                // Move cursor up one line if we're on the bottom line
                if (oldTop == Console.BufferHeight - 1)
                {
                    Console.SetCursorPosition(0, Console.BufferHeight - 2);
                }
            }
        }
        catch
        {
            // In test environments or when console is not available, ignore fancy positioning
        }

        Console.WriteLine(message ?? "null");

        // Always restore the bottom line if we have one
        if (_bottomLine != null)
        {
            WriteBottomLine(_bottomLine);
        }
    }
}