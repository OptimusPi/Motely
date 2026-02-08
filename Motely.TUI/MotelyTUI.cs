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
        try { File.WriteAllText(CrashLogPath, $"{DateTime.UtcNow:O} [{phase}]\n{ex}"); } catch { }
    }

    private static View MakeErrorFallback(string message)
    {
        var v = new View()
        {
            X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill(),
            CanFocus = true,
        };
        var label = new Label() { X = 2, Y = 2, Width = Dim.Fill() - 4, Height = 5, Text = message };
        var exitBtn = new CleanButton() { X = Pos.Center(), Y = 8, Text = " Exit " };
        exitBtn.Accept += (_, _) => _app?.RequestStop();
        v.Add(label, exitBtn);
        exitBtn.SetFocus();
        return v;
    }

    private static BalatroShaderBackground? _shaderBackground;
    private static Window? _mainTop;
    private static IApplication? _app;

    /// <summary>
    /// The v2 instance-based application context.
    /// Views should use View.App property, but this is available for static access if needed.
    /// </summary>
    public static IApplication? App => _app;

    public static int Run(string? configName = null, string? configFormat = null)
    {
        try { TuiSettings.Load(); }
        catch (Exception ex) { Console.WriteLine($"Failed to load TUI settings: {ex.Message}"); }

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
            _mainTop = new Window() { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill() };

            try
            {
                _shaderBackground = new BalatroShaderBackground();
                _mainTop.Add(_shaderBackground);
                _shaderBackground.Start();
            }
            catch (Exception ex) { LogCrash("Shader init (continuing without shader)", ex); }

            View mainContent;
            if (!string.IsNullOrEmpty(configName) && !string.IsNullOrEmpty(configFormat))
            {
                mainContent = new SearchWindow(configName, configFormat);
                mainContent.SetScheme(BalatroTheme.Window);
            }
            else
            {
                try { mainContent = new MainMenuWindow(); }
                catch (Exception ex)
                {
                    LogCrash("Main menu init", ex);
                    mainContent = MakeErrorFallback($"Main menu failed: {ex.Message}");
                }
                mainContent.SetFocus();
            }

            _mainTop.Add(mainContent);
            _app.Run(_mainTop);
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
        }
    }

    public static BalatroShaderBackground? ShaderBackground => _shaderBackground;
}
