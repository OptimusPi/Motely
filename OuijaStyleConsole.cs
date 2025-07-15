
using System.Runtime.CompilerServices;

namespace Motely;

public static class OuijaStyleConsole
{
    [MethodImpl(MethodImplOptions.Synchronized)]
    private static void WriteBottomLine(string bottomLine)
    {
        Console.WriteLine($"${bottomLine}");
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    private static void ClearBottomLine()
    {
        Console.WriteLine("");
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    public static void SetBottomLine(string? bottomLine)
    {
        Console.WriteLine($"${bottomLine}");
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    public static void WriteLine<T>(T message)
    {
        Console.WriteLine(message?.ToString() ?? null);
    }
}