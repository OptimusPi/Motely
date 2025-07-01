using System;

namespace TheFool.Services;

public static class LogManager
{
    public static void Initialize(ConfigService config)
    {
        Console.WriteLine("TheFool initialized");
    }

    public static void Shutdown()
    {
        Console.WriteLine("TheFool shutdown");
    }
}
