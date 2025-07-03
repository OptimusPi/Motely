using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using TheFool.Services;
using TheFool.ViewModels;
using TheFool.Views;

namespace TheFool;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        try
        {
            Console.WriteLine("Initializing services...");
            
            // Set up services
            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();

            Console.WriteLine("Getting config service...");
            var configService = _serviceProvider.GetRequiredService<ConfigService>();
            
            Console.WriteLine("Initializing LogManager...");
            LogManager.Initialize(configService);
            


            // Line below is needed to remove Avalonia data validation.
            // Without this line you will get duplicate validations from both Avalonia and CT
            BindingPlugins.DataValidators.RemoveAt(0);

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                Console.WriteLine("Creating MainWindow...");
                var mainViewModel = _serviceProvider.GetRequiredService<MainWindowViewModel>();
                
                desktop.MainWindow = new MainWindow
                {
                    DataContext = mainViewModel
                };
                
                // Handle app exit
                desktop.ShutdownRequested += OnShutdownRequested;
                
                Console.WriteLine("MainWindow created successfully with DataContext");
                

            }

            base.OnFrameworkInitializationCompleted();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error during initialization: {ex}");
            Console.Error.WriteLine($"Stack trace: {ex.StackTrace}");
            Console.ReadLine(); // Keep console open
            throw;
        }
    }

    private void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        LogManager.Shutdown();
        _serviceProvider?.Dispose();
    }
    
    private void ConfigureServices(IServiceCollection services)
    {
        // Register services with appropriate lifetimes
        services.AddSingleton<ConfigService>();
        services.AddSingleton<UserConfigService>();
        services.AddSingleton<DatabaseService>();
        services.AddSingleton<IOuijaService, OuijaService>();
        
        // Register ViewModels
        services.AddTransient<MainWindowViewModel>();
        
        Console.WriteLine("Services configured successfully");
    }
}
