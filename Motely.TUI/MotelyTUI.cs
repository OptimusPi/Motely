using Motely.API;
using Terminal.Gui;
using Terminal.Gui.Views;

namespace Motely.TUI;

public static class MotelyTUI
{
    private static string CrashLogPath => global::Motely.Program.CrashLogPath;

    private static void LogCrash(string phase, Exception ex)
    {
        var msg = $"{phase}: {ex.Message}";
        Console.WriteLine(msg);
        Console.WriteLine(ex.StackTrace);
        try
        {
            File.WriteAllText(CrashLogPath, $"{DateTime.UtcNow:O} [{phase}]\n{ex}");
        }
        catch { }
    }

    private static View MakeErrorFallback(string message)
    {
        var v = new View()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            CanFocus = true,
        };
        var label = new Label()
        {
            X = 2,
            Y = 2,
            Width = Dim.Fill() - 4,
            Height = 5,
            Text = message,
        };
        var exitBtn = new CleanButton()
        {
            X = Pos.Center(),
            Y = 8,
            Text = " Exit ",
        };
        exitBtn.Accept += (_, _) => _app?.RequestStop();
        v.Add(label, exitBtn);
        exitBtn.SetFocus();
        return v;
    }

    private static BalatroShaderBackground? _shaderBackground;
    private static Window? _desktop;
    private static MainMenuWindow? _mainMenu;
    private static IApplication? _app;

    /// <summary>
    /// The v2 instance-based application context.
    /// Views should use View.App property, but this is available for static access if needed.
    /// </summary>
    public static IApplication? App => _app;

    /// <summary>
    /// Show a window as a non-modal overlay on the desktop.
    /// The main menu is hidden and the new window is focused.
    /// </summary>
    public static void ShowWindow(Window window)
    {
        if (_desktop == null)
            return;
        if (_mainMenu != null)
            _mainMenu.Visible = false;
        _desktop.Add(window);
        window.SetFocus();
    }

    /// <summary>
    /// Close an overlay window and return to the main menu.
    /// </summary>
    public static void CloseWindow(Window window)
    {
        if (_desktop == null)
            return;
        _desktop.Remove(window);
        if (_mainMenu != null)
        {
            _mainMenu.Visible = true;
            _mainMenu.SetFocus();
        }
    }

    public static int Run(string? configName = null, string? configFormat = null)
    {
        try
        {
            TuiSettings.Load();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load TUI settings: {ex.Message}");
        }

        try
        {
            _app = Application.Create();
            _app.Init();
        }
        catch (Exception ex)
        {
            LogCrash("Terminal.Gui init", ex);
            return 1;
        }

        try
        {
            // Desktop: full-screen container for shader + overlapping windows
            _desktop = new Window()
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill(),
                CanFocus = true,
            };

            try
            {
                _shaderBackground = new BalatroShaderBackground();
                _desktop.Add(_shaderBackground);
                _shaderBackground.Start();
            }
            catch (Exception ex)
            {
                LogCrash("Shader init (continuing without shader)", ex);
            }

            if (!string.IsNullOrEmpty(configName) && !string.IsNullOrEmpty(configFormat))
            {
                // Direct search mode (CLI arg) — show search window immediately
                var searchWindow = new SearchWindow(configName, configFormat);
                _desktop.Add(searchWindow);
            }
            else
            {
                try
                {
                    _mainMenu = new MainMenuWindow();
                    _desktop.Add(_mainMenu);
                    _mainMenu.SetFocus();
                }
                catch (Exception ex)
                {
                    LogCrash("Main menu init", ex);
                    _desktop.Add(MakeErrorFallback($"Main menu failed: {ex.Message}"));
                }
            }

            _app.Run(_desktop);
            return 0;
        }
        catch (Exception ex)
        {
            LogCrash("TUI run", ex);
            return 1;
        }
        finally
        {
            _shaderBackground?.Stop();
            try
            {
                (_app as IDisposable)?.Dispose();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Warning: Application dispose failed: {ex.Message}");
            }

            // Wipe terminal so shell prompt isn't drawn over TUI remnants
            try
            {
                Console.Clear();
            }
            catch { }
        }
    }

    public static BalatroShaderBackground? ShaderBackground => _shaderBackground;
}
