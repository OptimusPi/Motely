using Terminal.Gui;

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
        exitBtn.Accept += (_, _) => Application.RequestStop(Application.Top);
        v.Add(label, exitBtn);
        exitBtn.SetFocus();
        return v;
    }

    private static BalatroShaderBackground? _shaderBackground;
    private static Window? _desktop;
    private static MainMenuWindow? _mainMenu;
    /// <summary>Window stack — open overlay windows in order they were opened.</summary>
    private static readonly List<Window> _windowStack = [];

    /// <summary>Cascade offset applied to each new window so stacked windows are distinguishable.</summary>
    private static readonly (int X, int Y)[] _cascadeOffsets =
    [
        (0, 0), (2, 1), (4, 2), (6, 3), (8, 4), (10, 5),
    ];

    /// <summary>Number of overlay windows currently open.</summary>
    public static int OpenWindowCount => _windowStack.Count;

    /// <summary>
    /// Show a window as a non-modal overlay on the desktop.
    /// Multiple windows can be open simultaneously — they stack with cascade offsets.
    /// The main menu stays in the background; focus moves to the new window.
    /// Use Alt+Tab to cycle between open windows.
    /// </summary>
    public static void ShowWindow(Window window)
    {
        if (_desktop == null)
            return;

        // Make windows fill the screen instead of being tiny modals, 
        // leaving 5 rows at the bottom for the main menu dock bar.
        window.X = 0;
        window.Y = 0;
        window.Width = Dim.Fill();
        window.Height = Dim.Fill() - 5;

        // Hide other windows so they don't draw over each other or waste CPU
        foreach (var w in _windowStack)
        {
            w.Visible = false;
        }

        _windowStack.Add(window);
        _desktop.Add(window);
        window.SetFocus();
    }

    /// <summary>
    /// Close an overlay window. Focus returns to the previous window in the stack,
    /// or to the main menu if no other windows are open.
    /// </summary>
    public static void CloseWindow(Window window)
    {
        if (_desktop == null)
            return;
        _windowStack.Remove(window);
        _desktop.Remove(window);

        // Restore focus to topmost remaining window, or main menu
        if (_windowStack.Count > 0)
        {
            var top = _windowStack[^1];
            top.Visible = true;
            top.SetFocus();
        }
        else if (_mainMenu != null)
        {
            _mainMenu.SetFocus();
        }
    }

    /// <summary>
    /// Cycle focus to the next open window (wraps around).
    /// Called by Alt+Tab global hotkey on the desktop.
    /// </summary>
    private static void CycleWindowFocus()
    {
        if (_windowStack.Count == 0)
        {
            _mainMenu?.SetFocus();
            return;
        }

        // Find which window currently has focus and move to the next
        var focused = _windowStack.FirstOrDefault(w => w.HasFocus);
        if (focused == null)
        {
            var top = _windowStack[^1];
            top.Visible = true;
            top.SetFocus();
            return;
        }

        var idx = _windowStack.IndexOf(focused);
        var next = _windowStack[(idx + 1) % _windowStack.Count];
        
        focused.Visible = false;
        next.Visible = true;
        next.SetFocus();
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
            Application.Init();
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

            // Global Alt+Tab: cycle focus between open windows
            _desktop.KeyDown += (_, e) =>
            {
                if (e.KeyCode == (KeyCode.Tab | KeyCode.AltMask))
                {
                    CycleWindowFocus();
                    e.Handled = true;
                }
            };

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

            Application.Run(_desktop);
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
            _windowStack.Clear();
            try
            {
                Application.Shutdown();
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
