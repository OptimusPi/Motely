using System;
using Avalonia;
using Avalonia.ReactiveUI;
using TheFool.Services;

namespace TheFool;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            Console.WriteLine("Starting TheFool application...");
            
            
            
            // Set up global error handling
            ErrorHandler.SetupGlobalHandlers();
            
            Console.WriteLine("Building Avalonia app...");
            var app = BuildAvaloniaApp();
            
            Console.WriteLine("Starting desktop lifetime...");
            app.StartWithClassicDesktopLifetime(args);
            
            Console.WriteLine("Application ended normally.");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to start application: {ex}");
            Console.Error.WriteLine($"Stack trace: {ex.StackTrace}");
            Console.ReadLine(); // Keep console open to see error
            throw;
        }
    }



    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();
}
