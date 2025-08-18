
using System.Runtime.CompilerServices;

namespace Motely;

public static class FancyConsole
{
    public static bool IsEnabled { get; set; } = true;
    public static bool SimpleProgressMode { get; set; } = false; // fallback for Windows + quiet

    private static string? _bottomLine;

    [MethodImpl(MethodImplOptions.Synchronized)]
    private static void WriteBottomLine(string bottomLine)
    {
        (int oldLeft, int oldTop) = Console.GetCursorPosition();
        Console.SetCursorPosition(0, Console.BufferHeight - 1);
        Console.Write(new string(' ', Console.BufferWidth));
        Console.SetCursorPosition(0, Console.BufferHeight - 1);
        Console.Write(bottomLine);
        Console.SetCursorPosition(oldLeft, oldTop);
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    private static void ClearBottomLine()
    {
        (int oldLeft, int oldTop) = Console.GetCursorPosition();
        Console.SetCursorPosition(0, Console.BufferHeight - 1);
        Console.Write(new string(' ', Console.BufferWidth));
        Console.SetCursorPosition(oldLeft, oldTop);
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    public static void SetBottomLine(string? bottomLine)
    {
        if (SimpleProgressMode || !IsEnabled)
        {
            // In simple mode we don't attempt cursor reposition hacks
            return;
        }
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
    public static void WriteLine<T>(T message)
    {
        WriteLine(message?.ToString() ?? null);
    }


    [MethodImpl(MethodImplOptions.Synchronized)]
    public static void WriteLine(string? message)
    {
        if (!IsEnabled)
        {
            Console.WriteLine(message ?? "null");
            return;
        }
        if (SimpleProgressMode)
        {
            // Overwrite same line using CR only (no ANSI dependency)
            var text = message ?? "null";
            if (text.Length > Console.BufferWidth - 1)
                text = text.Substring(0, Console.BufferWidth - 1);
            Console.Write("\r" + text.PadRight(Console.BufferWidth - 1));
            return;
        }
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
}