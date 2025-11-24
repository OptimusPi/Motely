using Terminal.Gui;
using System;

namespace Motely.TUI;

/// <summary>
/// Animated Balatro-style shader background using colored ASCII blocks
/// </summary>
public class BalatroShaderBackground : View
{
    private Random _random = new Random();
    private double _time = 0;
    private bool _isRunning = false;

    private static readonly ColorName[] BalatroColors = new[]
    {
        ColorName.BrightRed,      // #ff4c40 Red (SIGNATURE BALATRO BUTTON COLOR)
        ColorName.BrightBlue,     // #0093ff Blue (SIGNATURE BALATRO BUTTON COLOR)
        ColorName.Red,            // #a02721 DarkRed
        ColorName.Blue,           // #0057a1 DarkBlue
        ColorName.BrightRed,      // Emphasize red again
        ColorName.BrightBlue,     // Emphasize blue again
        ColorName.BrightMagenta,  // #7d60e0 Purple
        ColorName.Magenta,        // #292189 DarkPurple
        ColorName.BrightRed,      // More signature red
        ColorName.BrightBlue,     // More signature blue
        ColorName.DarkGray,       // #3a5055 Grey
        ColorName.Black           // #1e2b2d DarkGrey
    };

    public BalatroShaderBackground()
    {
        X = 0;
        Y = 0;
        Width = Dim.Fill();
        Height = Dim.Fill();
        CanFocus = false;
        WantContinuousButtonPressed = false;
    }

    public void Start()
    {
        if (_isRunning) return;

        _isRunning = true;

        // Slower refresh for better performance (200ms = 5 FPS)
        Application.AddTimeout(TimeSpan.FromMilliseconds(200), () =>
        {
            if (!_isRunning) return false;

            _time += 0.02; // Slower animation
            DrawShader();
            SetNeedsDisplay();

            return true; // Keep timer running
        });
    }

    public void Stop()
    {
        _isRunning = false;
    }

    private void DrawShader()
    {
        if (Driver == null) return;

        int width = Viewport.Width;
        int height = Viewport.Height;

        // Validate bounds before drawing
        if (width <= 0 || height <= 0)
            return;

        try
        {
            // Authentic Balatro-style flowing gradient
            // Simple, smooth, not too crazy
            for (int y = 0; y < height; y += 2) // Skip every other line for performance
            {
                for (int x = 0; x < width; x += 2) // Skip every other column
                {
                    // Normalized coordinates
                    double fx = (double)x / width;
                    double fy = (double)y / height;

                    // Simple flowing diagonal gradient (like Balatro's background)
                    double wave = Math.Sin((fx + fy) * 3.0 + _time);

                    // Map to color index (cycling through red and blue)
                    double value = (wave + 1.0) * 0.5; // Normalize to 0-1

                    ColorName color;
                    if (value < 0.33)
                        color = ColorName.Blue;
                    else if (value < 0.66)
                        color = ColorName.BrightRed;
                    else
                        color = ColorName.BrightBlue;

                    // Simple block characters for subtle texture
                    char ch = value > 0.6 ? '▓' : '░';

                    // Draw
                    Driver!.Move(x, y);
                    Driver.SetAttribute(new Terminal.Gui.Attribute(color, ColorName.Black));
                    Driver.AddRune(new System.Text.Rune(ch));
                }
            }
        }
        catch (InvalidOperationException)
        {
            // Terminal is being resized or in invalid state - skip this frame
        }
        catch (ArgumentOutOfRangeException)
        {
            // Bounds changed during render - skip this frame
        }
    }
}
