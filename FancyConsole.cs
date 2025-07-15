
using System.Runtime.CompilerServices;

namespace Motely;

public static class FancyConsole
{
    private static volatile bool _isEnabled = true;

    public static bool IsEnabled
    {
        get => _isEnabled;
        set => _isEnabled = value;
    }
    private static string? _bottomLine;

    [MethodImpl(MethodImplOptions.Synchronized)]
    private static void WriteBottomLine(string bottomLine)
    {
        if (!IsEnabled)
            return;
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
        if (!IsEnabled)
            return;
        (int oldLeft, int oldTop) = Console.GetCursorPosition();
        Console.SetCursorPosition(0, Console.BufferHeight - 1);
        Console.Write(new string(' ', Console.BufferWidth));
        Console.SetCursorPosition(oldLeft, oldTop);
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    public static void SetBottomLine(string? bottomLine)
    {
        if (!IsEnabled)
            return;
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
        if (!IsEnabled)
            return;
        WriteLine(message?.ToString() ?? null);
    }


    [MethodImpl(MethodImplOptions.Synchronized)]
    public static void WriteLine(string? message)
    {
        if (!IsEnabled)
            return;
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