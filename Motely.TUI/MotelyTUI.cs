using Terminal.Gui;
using Motely.API;

namespace Motely.TUI;

public static class MotelyTUI
{
    private static BalatroShaderBackground? _shaderBackground;
    private static Toplevel? _mainTop;
    private static IApplication? _app;

    /// <summary>
    /// The v2 instance-based application context.
    /// Views should use View.App property, but this is available for static access if needed.
    /// </summary>
    public static IApplication? App => _app;

    public static int Run(string? configName = null, string? configFormat = null)
    {
        // Load persisted settings from tui.json
        TuiSettings.Load();

        try
        {
            // v2 instance-based approach
            _app = Application.Create();
            _app.Init();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to initialize Terminal.Gui: {ex.Message}");
            return 1;
        }

        try
        {
            _mainTop = new Toplevel()
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill(),
            };

            _shaderBackground = new BalatroShaderBackground();
            _mainTop.Add(_shaderBackground);
            _shaderBackground.Start();

            if (!string.IsNullOrEmpty(configName) && !string.IsNullOrEmpty(configFormat))
            {
                var searchWindow = new SearchWindow(configName, configFormat);
                searchWindow.SetScheme(BalatroTheme.Window);
                _mainTop.Add(searchWindow);
            }
            else
            {
                // Go straight to main menu (no ColorScheme - let shader show through)
                var mainMenu = new MainMenuWindow();
                _mainTop.Add(mainMenu);
                mainMenu.SetFocus();
            }

            _app.Run(_mainTop);
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"TUI Error: {ex.Message}");
            return 1;
        }
        finally
        {
            _shaderBackground?.Stop();
            try
            {
                _app?.Shutdown();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Warning: Application.Shutdown() failed: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Run only the API server without TUI interface
    /// </summary>
    public static async Task<int> RunApiOnly(string host = "localhost", int port = 5123)
    {
        try
        {
            Console.WriteLine($"Starting Motely API server on http://{host}:{port}");
            
            var args = new[] { "--urls", $"http://{host}:{port}" };
            var app = MotelyApiFactory.CreateApi(args);
            
            Console.WriteLine("API server started successfully. Press Ctrl+C to stop.");
            await app.RunAsync();
            
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"API Error: {ex.Message}");
            return 1;
        }
    }

    public static BalatroShaderBackground? ShaderBackground => _shaderBackground;
}
