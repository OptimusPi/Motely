using System;
using Terminal.Gui;

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
        ColorName.BrightRed, // #ff4c40 Red (SIGNATURE BALATRO BUTTON COLOR)
        ColorName.BrightBlue, // #0093ff Blue (SIGNATURE BALATRO BUTTON COLOR)
        ColorName.Red, // #a02721 DarkRed
        ColorName.Blue, // #0057a1 DarkBlue
        ColorName.BrightRed, // Emphasize red again
        ColorName.BrightBlue, // Emphasize blue again
        ColorName.BrightMagenta, // #7d60e0 Purple
        ColorName.Magenta, // #292189 DarkPurple
        ColorName.BrightRed, // More signature red
        ColorName.BrightBlue, // More signature blue
        ColorName.DarkGray, // #3a5055 Grey
        ColorName.Black, // #1e2b2d DarkGrey
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
        if (_isRunning)
            return;

        _isRunning = true;

        // Render at 5 FPS (200ms) with pixel skipping for performance
        Application.AddTimeout(
            TimeSpan.FromMilliseconds(200),
            () =>
            {
                if (!_isRunning)
                    return false;

                _time += 0.02;
                DrawShader();
                SetNeedsDisplay();

                return true;
            }
        );
    }

    public void Stop()
    {
        _isRunning = false;
    }

    private void DrawShader()
    {
        if (Driver == null)
            return;

        int width = Viewport.Width;
        int height = Viewport.Height;

        // Validate bounds before drawing
        if (width <= 0 || height <= 0)
            return;

        try
        {
            // SICK Balatro-style flowing gradient with smooth color blending
            // Full gradient characters for smooth transitions
            char[] gradient = { ' ', '░', '▒', '▓', '█' };

            // Skip every 3rd pixel for performance (~89% reduction in operations)
            for (int y = 0; y < height; y += 3)
            {
                for (int x = 0; x < width; x += 3)
                {
                    // Normalized coordinates
                    double fx = (double)x / width;
                    double fy = (double)y / height;

                    // Multiple waves for depth and interest
                    double wave1 = Math.Sin((fx + fy) * 3.0 + _time);
                    double wave2 = Math.Sin((fx - fy) * 2.0 + _time * 0.7);
                    double combined = (wave1 + wave2 * 0.5) / 1.5;

                    // Normalize to 0-1
                    double value = (combined + 1.0) * 0.5;

                    // DRAMATIC color blending - way more visible!
                    ColorName color;
                    if (value < 0.2)
                        color = ColorName.Black; // Deep dark
                    else if (value < 0.35)
                        color = ColorName.Blue; // Blue
                    else if (value < 0.5)
                        color = ColorName.BrightBlue; // Bright blue
                    else if (value < 0.65)
                        color = ColorName.Magenta; // Purple transition
                    else if (value < 0.8)
                        color = ColorName.BrightRed; // Signature red
                    else
                        color = ColorName.BrightMagenta; // Hot magenta peaks

                    // Map value to gradient character (0-1 -> 0-4 index)
                    int charIndex = Math.Min((int)(value * gradient.Length), gradient.Length - 1);
                    char ch = gradient[charIndex];

                    // Draw with smooth gradient
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
