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
            Height = Dim.Fill(),
        };

        // Add animated Balatro shader background
        var background = new BalatroShaderBackground();
        welcomeTop.Add(background);
        background.Start();

        // Welcome box - fixed size, centered
        // The box is 98 chars wide, 30 lines tall (with border and Jimbo!)
        // Calculate initial Y position - start below screen, slide up
        int boxHeight = 30;
        int boxWidth = 98;
        int targetY = (Application.Driver.Rows - boxHeight) / 2;
        int startY = Application.Driver.Rows; // Start below screen

        // Convert Welcome text to 2D array for dissolve effect
        var welcomeLines = JimboArt.Welcome.Split('\n');
        var charArray = new char[welcomeLines.Length][];
        var revealedArray = new char[welcomeLines.Length][];
        var revealPositions = new List<(int row, int col)>();

        for (int i = 0; i < welcomeLines.Length; i++)
        {
            charArray[i] = welcomeLines[i].ToCharArray();
            revealedArray[i] = new string(' ', charArray[i].Length).ToCharArray();

            // Track all non-space positions to reveal
            for (int j = 0; j < charArray[i].Length; j++)
            {
                if (charArray[i][j] != ' ')
                {
                    revealPositions.Add((i, j));
                }
            }
        }

        // Shuffle positions for random reveal
        var random = new Random();
        for (int i = revealPositions.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (revealPositions[i], revealPositions[j]) = (revealPositions[j], revealPositions[i]);
        }

        var welcomeLabel = new Label()
        {
            X = Pos.Center() - 49, // Center horizontally (98/2 = 49)
            Y = startY,
            Width = boxWidth,
            Height = boxHeight,
            Text = string.Join('\n', revealedArray.Select(arr => new string(arr))),
            TextAlignment = Alignment.Start,
        };

        welcomeTop.Add(welcomeLabel);

        // Animate slide-up AND dissolve reveal simultaneously over 1100ms
        var animationSteps = 55; // 1100ms / 20ms per frame = 55 frames
        var stepDelay = 20; // 20ms per frame
        var currentStep = 0;
        var animationToken = new System.Threading.CancellationTokenSource();
        var revealIndex = 0;
        var charsPerFrame = Math.Max(1, revealPositions.Count / animationSteps);

        var timer = new System.Timers.Timer(stepDelay);
        timer.Elapsed += (s, e) =>
        {
            if (currentStep >= animationSteps || animationToken.IsCancellationRequested)
            {
                // Ensure all characters are revealed at the end
                for (int i = 0; i < revealedArray.Length; i++)
                {
                    revealedArray[i] = (char[])charArray[i].Clone();
                }

                Application.Invoke(() =>
                {
                    welcomeLabel.Text = string.Join('\n', revealedArray.Select(arr => new string(arr)));
                    Application.Refresh();
                });

                timer.Stop();
                timer.Dispose();
                return;
            }

            currentStep++;
            var progress = (double)currentStep / animationSteps;

            // Smooth easing function (ease-out)
            var easedProgress = 1 - Math.Pow(1 - progress, 3);

            var currentY = (int)(startY - (startY - targetY) * easedProgress);

            // Reveal more characters
            for (int i = 0; i < charsPerFrame && revealIndex < revealPositions.Count; i++)
            {
                var (row, col) = revealPositions[revealIndex];
                revealedArray[row][col] = charArray[row][col];
                revealIndex++;
            }

            Application.Invoke(() =>
            {
                welcomeLabel.Y = currentY;
                welcomeLabel.Text = string.Join('\n', revealedArray.Select(arr => new string(arr)));
                Application.Refresh();
            });
        };
        timer.Start();

        // Wait for any key press
        var keyPressed = false;
        welcomeTop.KeyDown += (sender, e) =>
        {
            if (!keyPressed)
            {
                keyPressed = true;
                animationToken.Cancel();
                timer?.Stop();
                timer?.Dispose();
                background.Stop(); // Stop animation before closing
                Application.RequestStop();
                e.Handled = true;
            }
        };

        Application.Run(welcomeTop);

        // Cleanup
        animationToken?.Dispose();
        timer?.Dispose();
    }
}
