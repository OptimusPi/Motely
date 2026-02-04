using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Motely;

/// <summary>
/// Desktop implementation of FancyConsole with proper cursor positioning.
/// The bottom line stays at the bottom - content scrolls above it.
/// </summary>
public class FancyConsoleDesktop : FancyConsoleImpl
{
    private static readonly FancyConsoleDesktop _desktopInstance = new();
    private static int _savedCursorTop = -1;
    private static int _bottomLineRow = -1;
    private static bool _isTerminalSupported;

    static FancyConsoleDesktop()
    {
        // Check if we have a real terminal (not redirected)
        _isTerminalSupported =
            !Console.IsOutputRedirected
                && !Console.IsErrorRedirected
                && Environment.GetEnvironmentVariable("TERM") != null
            || RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    }

    public static new FancyConsoleDesktop Instance => _desktopInstance;

    [MethodImpl(MethodImplOptions.Synchronized)]
    public new void SetBottomLine(string? bottomLine)
    {
        _bottomLine = bottomLine;

        if (!IsEnabled || !_isTerminalSupported)
        {
            // Fallback: just track the value, print only on change
            if (bottomLine != null && bottomLine != _lastPrintedBottomLine)
            {
                Console.Error.WriteLine(bottomLine);
                _lastPrintedBottomLine = bottomLine;
            }
            return;
        }

        try
        {
            if (bottomLine != null)
            {
                WriteBottomLineWithCursor(bottomLine);
            }
            else
            {
                ClearBottomLineWithCursor();
            }
        }
        catch
        {
            // Terminal doesn't support cursor ops - fall back to simple output
            _isTerminalSupported = false;
            if (bottomLine != null && bottomLine != _lastPrintedBottomLine)
            {
                Console.Error.WriteLine(bottomLine);
                _lastPrintedBottomLine = bottomLine;
            }
        }
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    public new void WriteLine(string? message)
    {
        if (!IsEnabled)
        {
            Console.WriteLine(message ?? "null");
            return;
        }

        if (!_isTerminalSupported || _bottomLine == null)
        {
            // No cursor support or no bottom line - just write
            Console.WriteLine(message ?? "null");
            return;
        }

        try
        {
            // Clear bottom line, write message, restore bottom line
            ClearBottomLineWithCursor();
            Console.WriteLine(message ?? "null");
            WriteBottomLineWithCursor(_bottomLine);
        }
        catch
        {
            // Fallback
            _isTerminalSupported = false;
            Console.WriteLine(message ?? "null");
        }
    }

    private void WriteBottomLineWithCursor(string bottomLine)
    {
        // Save current position
        _savedCursorTop = Console.CursorTop;

        // Calculate bottom row (leave room for bottom line)
        int windowHeight = Console.WindowHeight;
        _bottomLineRow = windowHeight - 1;

        // If we're at or past the bottom, scroll up first
        if (_savedCursorTop >= _bottomLineRow)
        {
            Console.SetCursorPosition(0, _bottomLineRow);
            Console.Write(new string(' ', Console.WindowWidth - 1));
            _savedCursorTop = _bottomLineRow - 1;
        }

        // Move to bottom row
        Console.SetCursorPosition(0, _bottomLineRow);

        // Clear the line and write new content
        string truncated =
            bottomLine.Length > Console.WindowWidth - 1
                ? bottomLine.Substring(0, Console.WindowWidth - 1)
                : bottomLine;
        Console.Write(truncated.PadRight(Console.WindowWidth - 1));

        // Restore cursor position
        Console.SetCursorPosition(0, _savedCursorTop);

        _lastPrintedBottomLine = bottomLine;
    }

    private void ClearBottomLineWithCursor()
    {
        if (_bottomLineRow < 0)
            return;

        _savedCursorTop = Console.CursorTop;

        // Move to bottom and clear
        Console.SetCursorPosition(0, _bottomLineRow);
        Console.Write(new string(' ', Console.WindowWidth - 1));

        // Restore cursor
        Console.SetCursorPosition(0, _savedCursorTop);

        _bottomLineRow = -1;
        _lastPrintedBottomLine = null;
    }
}
