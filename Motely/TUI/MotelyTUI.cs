using Terminal.Gui;

namespace Motely.TUI;

public static class MotelyTUI
{
    public static int Run(string? configName = null, string? configFormat = null)
    {
        try
        {
            Application.Init();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to initialize Terminal.Gui: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            return 1;
        }

        try
        {
            // If config was specified, show search window directly
            if (!string.IsNullOrEmpty(configName) && !string.IsNullOrEmpty(configFormat))
            {
                var searchWindow = new SearchWindow(configName, configFormat);
                Application.Run(searchWindow);
            }
            else
            {
                // Show welcome screen with Balatro shader first!
                ShowWelcomeScreen();

                // Keep showing main menu until user explicitly exits
                while (true)
                {
                    var mainMenu = new MainMenuWindow();
                    Application.Run(mainMenu);

                    // If main menu was closed with RequestStop, we're done
                    if (!Application.Top.Running)
                        break;
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"TUI Error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            return 1;
        }
        finally
        {
            try
            {
                Application.Shutdown();
            }
            catch (Exception ex)
            {
                // Shutdown failed - log but don't crash since we're exiting anyway
                Console.Error.WriteLine($"Warning: Application.Shutdown() failed: {ex.Message}");
            }
        }
    }

    private static void ShowWelcomeScreen()
    {
        // Create a toplevel without borders
        var welcomeTop = new Toplevel()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        // Add animated Balatro shader background - FUCK YEAH!
        var background = new BalatroShaderBackground();
        welcomeTop.Add(background);
        background.Start();

        // Welcome box - fixed size, centered
        // The box is 66 chars wide, 48 lines tall (with your detailed Jimbo!)
        var welcomeLabel = new Label()
        {
            X = Pos.Center() - 33,  // Center horizontally (66/2 = 33)
            Y = Pos.Center() - 24,  // Center vertically (48/2 = 24)
            Width = 66,
            Height = 48,
            Text = JimboArt.Welcome,
            TextAlignment = Alignment.Start
        };

        welcomeTop.Add(welcomeLabel);

        // Wait for any key press
        welcomeTop.KeyDown += (sender, e) =>
        {
            background.Stop(); // Stop animation before closing
            Application.RequestStop();
            e.Handled = true;
        };

        Application.Run(welcomeTop);
    }
}
