using System.Runtime.CompilerServices;

namespace Motely;

/// <summary>
/// Desktop-specific console implementation with fancy bottom line support
/// </summary>
public class FancyConsoleImpl : IMotelyConsole
{
    public static bool IsEnabled { get; set; } = true;
    private static string? _bottomLine;
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