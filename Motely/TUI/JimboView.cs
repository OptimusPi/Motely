using System.Reflection;
using System.Text;
using SixLabors.ImageSharp.PixelFormats;
using SLImage = SixLabors.ImageSharp.Image;
using Color = Terminal.Gui.Drawing.Color;

namespace Motely.TUI;

/// <summary>
/// Renders the Jimbo sprite as colored pixel blocks in the terminal.
/// Uses half-block characters for 2x vertical resolution.
/// Loads from embedded Jimbo.png with proper alpha channel support.
/// </summary>
public class JimboView : View
{
    private readonly Color[,] _pixels;
    private readonly int _pixelWidth;
    private readonly int _pixelHeight;

    public JimboView()
    {
        (_pixels, _pixelWidth, _pixelHeight) = LoadFromPng();

        // Each character cell shows 2 vertical pixels using half-block
        Width = _pixelWidth;
        Height = (_pixelHeight + 1) / 2;
        CanFocus = false;
    }

    private static (Color[,] pixels, int width, int height) LoadFromPng()
    {
        // Load from embedded resource
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = "Motely.Jimbo.png";

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            // Fallback: return a small placeholder
            var placeholder = new Color[8, 8];
            for (int x = 0; x < 8; x++)
                for (int y = 0; y < 8; y++)
                    placeholder[x, y] = new Color(255, 0, 255); // Magenta = missing
            return (placeholder, 8, 8);
        }

        using var image = SLImage.Load<Rgba32>(stream);
        int width = image.Width;
        int height = image.Height;
        var pixels = new Color[width, height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var pixel = image[x, y];
                pixels[x, y] = new Color(pixel.R, pixel.G, pixel.B, pixel.A);
            }
        }

        return (pixels, width, height);
    }

    protected override bool OnDrawingContent()
    {
        var viewport = Viewport;
        var transparent = new Color(0, 0, 0, 0);
        var shader = MotelyTUI.ShaderBackground;
        var screenRect = ViewportToScreen(viewport);

        for (int charY = 0; charY < viewport.Height && charY * 2 < _pixelHeight; charY++)
        {
            for (int charX = 0; charX < viewport.Width && charX < _pixelWidth; charX++)
            {
                int topY = charY * 2;
                int bottomY = topY + 1;

                var topColor = _pixels[charX, topY];
                var bottomColor = bottomY < _pixelHeight ? _pixels[charX, bottomY] : transparent;

                bool topTransparent = topColor.A == 0;
                bool bottomTransparent = bottomColor.A == 0;

                // Get shader background color at this screen position
                var bgColor = shader?.GetColorAt(screenRect.X + charX, screenRect.Y + charY)
                    ?? Color.Black;

                if (topTransparent && bottomTransparent)
                {
                    // Both transparent - draw background shader color
                    SetAttribute(new Attribute(bgColor, bgColor));
                    AddRune(charX, charY, (Rune)'█');
                    continue;
                }

                if (topTransparent)
                {
                    // Only bottom pixel visible - lower half block, shader on top
                    SetAttribute(new Attribute(bottomColor, bgColor));
                    AddRune(charX, charY, (Rune)'▄');
                }
                else if (bottomTransparent)
                {
                    // Only top pixel visible - upper half block, shader on bottom
                    SetAttribute(new Attribute(topColor, bgColor));
                    AddRune(charX, charY, (Rune)'▀');
                }
                else
                {
                    // Both pixels visible - upper half block with fg=top, bg=bottom
                    SetAttribute(new Attribute(topColor, bottomColor));
                    AddRune(charX, charY, (Rune)'▀');
                }
            }
        }
        return true;
    }
}
