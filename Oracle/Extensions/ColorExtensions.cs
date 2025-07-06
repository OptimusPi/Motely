using System;
using SkiaSharp;

namespace Motely.Oracle.Extensions;

public static class ColorExtensions
{
    /// <summary>
    /// Converts an SKColor to a float array suitable for shader uniforms.
    /// Returns normalized RGBA values (0-1 range).
    /// </summary>
    public static float[] ToShaderArray(this SKColor color)
    {
        return new[]
        {
            color.Red / 255f,
            color.Green / 255f,
            color.Blue / 255f,
            color.Alpha / 255f
        };
    }
    
    /// <summary>
    /// Converts an SKColor to HSL color space for more natural color blending.
    /// Returns as a float array suitable for shader uniforms.
    /// </summary>
    public static float[] ToHsl(this SKColor color)
    {
        float r = color.Red / 255f;
        float g = color.Green / 255f;
        float b = color.Blue / 255f;
        float a = color.Alpha / 255f;
        
        float max = Math.Max(r, Math.Max(g, b));
        float min = Math.Min(r, Math.Min(g, b));
        float h, s, l = (max + min) / 2f;
        
        if (max == min)
        {
            h = s = 0; // achromatic
        }
        else
        {
            float d = max - min;
            s = l > 0.5f ? d / (2f - max - min) : d / (max + min);
            
            if (max == r)
                h = (g - b) / d + (g < b ? 6f : 0f);
            else if (max == g)
                h = (b - r) / d + 2f;
            else
                h = (r - g) / d + 4f;
            
            h /= 6f;
        }
        
        // Return as RGBA for shader compatibility
        return new[] { r, g, b, a };
    }
    
    /// <summary>
    /// Blends two colors using linear interpolation.
    /// </summary>
    public static SKColor Lerp(this SKColor from, SKColor to, float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        
        return new SKColor(
            (byte)(from.Red + (to.Red - from.Red) * amount),
            (byte)(from.Green + (to.Green - from.Green) * amount),
            (byte)(from.Blue + (to.Blue - from.Blue) * amount),
            (byte)(from.Alpha + (to.Alpha - from.Alpha) * amount)
        );
    }
}
