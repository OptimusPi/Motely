using System;
using System.Diagnostics;

namespace Oracle.Services;

public static class LogManager
{
    private static bool _verboseLogging = true; // Enable verbose logging by default for debugging
    
    public static void Initialize(ConfigService config)
    {
        Console.WriteLine("Oracle initialized");
        Console.WriteLine($"Verbose logging: {_verboseLogging}");
        
        if (_verboseLogging)
        {
            Console.WriteLine("Debug logging enabled - all shader and rendering errors will be displayed");
        }
    }

    public static void Shutdown()
    {
        Console.WriteLine("Oracle shutdown");
    }
    
    public static void LogDebug(string message)
    {
        if (_verboseLogging)
        {
            Console.WriteLine($"[DEBUG] {DateTime.Now:HH:mm:ss.fff} {message}");
            Debug.WriteLine($"[DEBUG] {DateTime.Now:HH:mm:ss.fff} {message}");
        }
    }
    
    public static void LogError(string message, Exception? ex = null)
    {
        Console.WriteLine($"[ERROR] {DateTime.Now:HH:mm:ss.fff} {message}");
        Debug.WriteLine($"[ERROR] {DateTime.Now:HH:mm:ss.fff} {message}");
        
        if (ex != null)
        {
            Console.WriteLine($"[ERROR] Exception: {ex.Message}");
            Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
            Debug.WriteLine($"[ERROR] Exception: {ex.Message}");
            Debug.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
        }
    }
    
    public static void LogInfo(string message)
    {
        Console.WriteLine($"[INFO] {DateTime.Now:HH:mm:ss.fff} {message}");
        Debug.WriteLine($"[INFO] {DateTime.Now:HH:mm:ss.fff} {message}");
    }
    
    public static void SetVerboseLogging(bool enabled)
    {
        _verboseLogging = enabled;
        Console.WriteLine($"Verbose logging {(enabled ? "enabled" : "disabled")}");
    }
}
