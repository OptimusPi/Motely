using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using SkiaSharp;

namespace Motely.TheFool.Controls;

/// <summary>
/// Balatro-style animated background - Paint Flow Version!
/// </summary>
public class BalatroBkgnd : Control
{
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private DispatcherTimer? _animationTimer;
    
    #region Properties
    
    public static readonly StyledProperty<double> SpinSpeedProperty =
        AvaloniaProperty.Register<BalatroBkgnd, double>(nameof(SpinSpeed), 2.0);
    
    public static readonly StyledProperty<double> MoveSpeedProperty =
        AvaloniaProperty.Register<BalatroBkgnd, double>(nameof(MoveSpeed), 7.0);
    
    public double SpinSpeed
    {
        get => GetValue(SpinSpeedProperty);
        set => SetValue(SpinSpeedProperty, value);
    }
    
    public double MoveSpeed
    {
        get => GetValue(MoveSpeedProperty);
        set => SetValue(MoveSpeedProperty, value);
    }
    
    #endregion
    
    static BalatroBkgnd()
    {
        ClipToBoundsProperty.OverrideDefaultValue<BalatroBkgnd>(true);
        AffectsRender<BalatroBkgnd>(SpinSpeedProperty, MoveSpeedProperty);
    }
    
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        

        _animationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(33)
        };
        _animationTimer.Tick += (_, _) => InvalidateVisual();
        _animationTimer.Start();
    }
    
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _animationTimer?.Stop();
        _animationTimer = null;
    }
    
    public override void Render(DrawingContext context)
    {
        var elapsed = _stopwatch.Elapsed.TotalSeconds;
        var width = (int)Bounds.Width;
        var height = (int)Bounds.Height;
        
        if (width <= 0 || height <= 0) return;
        
        // Optimized CPU-based Balatro background rendering
        const double SPIN_ROTATION = -2.0;
        const double SPIN_SPEED = 5.0;
        const double CONTRAST = 3.5;
        const double SPIN_AMOUNT = 0.2;
        const double PIXEL_FILTER = 45.0; // Lower for better performance
        const double SPIN_EASE = 1.0;
        
        // Balatro colors
        var COLOR_1 = Color.FromRgb(222, 68, 59);   // Red
        var COLOR_2 = Color.FromRgb(0, 107, 180);   // Blue  
        var COLOR_3 = Color.FromRgb(22, 35, 37);    // Dark background
        
        var screenLength = Math.Sqrt(width * width + height * height);
        var pixelSize = screenLength / PIXEL_FILTER;
        var pixelStep = Math.Max(6, (int)Math.Ceiling(pixelSize)); // Larger pixels for better performance
        
        // Render pixelated blocks directly
        for (int x = 0; x < width; x += pixelStep)
        {
            for (int y = 0; y < height; y += pixelStep)
            {
                // Convert to UV coordinates (normalized to [-0.5, 0.5])
                var uvX = (x - width * 0.5) / screenLength;
                var uvY = (y - height * 0.5) / screenLength;
                
                var uvLen = Math.Sqrt(uvX * uvX + uvY * uvY);
                
                // Spin calculation
                var speed = (SPIN_ROTATION * SPIN_EASE * 0.2) + elapsed * 0.1;
                var newPixelAngle = Math.Atan2(uvY, uvX) + speed - SPIN_EASE * 20.0 * (SPIN_AMOUNT * uvLen + (1.0 - SPIN_AMOUNT));
                
                uvX = uvLen * Math.Cos(newPixelAngle);
                uvY = uvLen * Math.Sin(newPixelAngle);
                
                // Scale and animate
                uvX *= 25.0; // Reduced from 30.0 to make the pattern larger (zoom in effect)
                uvY *= 25.0;
                var animSpeed = elapsed * SPIN_SPEED;
                
                var uv2X = uvX;
                var uv2Y = uvY;
                
                // Ultra-simplified paint flow (2 iterations for maximum performance)
                for (int i = 0; i < 2; i++)
                {
                    var sinMax = Math.Sin(Math.Max(uvX, uvY));
                    uv2X += sinMax * 0.5 + uvX * 0.05;
                    uv2Y += sinMax * 0.5 + uvY * 0.05;
                    
                    var newUvX = uvX + 0.3 * Math.Cos(5.1 + 0.35 * uv2Y + animSpeed * 0.13);
                    var newUvY = uvY + 0.3 * Math.Sin(uv2X - 0.11 * animSpeed);
                    
                    var cosUv = Math.Cos(uvX + uvY) * 0.5;
                    var sinUv = Math.Sin(uvX * 0.7 - uvY) * 0.5;
                    
                    uvX = newUvX - cosUv + sinUv;
                    uvY = newUvY - cosUv + sinUv;
                }
                
                
                // Color calculation
                var contrastMod = (0.25 * CONTRAST + 0.5 * SPIN_AMOUNT + 1.2);
                var paintRes = Math.Min(2.0, Math.Max(0.0, Math.Sqrt(uvX * uvX + uvY * uvY) * 0.035 * contrastMod));
                
                var c1p = Math.Max(0.0, 1.0 - contrastMod * Math.Abs(1.0 - paintRes));
                var c2p = Math.Max(0.0, 1.0 - contrastMod * Math.Abs(paintRes));
                var c3p = 1.0 - Math.Min(1.0, c1p + c2p);
                
                // Final color mixing
                var baseWeight = 0.3 / CONTRAST;
                var mainWeight = 1.0 - baseWeight;
                
                var finalR = (byte)Math.Min(255, Math.Max(0, 
                    baseWeight * COLOR_1.R + mainWeight * (COLOR_1.R * c1p + COLOR_2.R * c2p + COLOR_3.R * c3p)));
                var finalG = (byte)Math.Min(255, Math.Max(0, 
                    baseWeight * COLOR_1.G + mainWeight * (COLOR_1.G * c1p + COLOR_2.G * c2p + COLOR_3.G * c3p)));
                var finalB = (byte)Math.Min(255, Math.Max(0, 
                    baseWeight * COLOR_1.B + mainWeight * (COLOR_1.B * c1p + COLOR_2.B * c2p + COLOR_3.B * c3p)));
                
                var pixelColor = Color.FromRgb(finalR, finalG, finalB);
                var brush = new SolidColorBrush(pixelColor);
                
                // Draw overlapping pixel block to eliminate grid lines
                var pixelRect = new Rect(x, y, Math.Min(pixelStep + 1, width - x), Math.Min(pixelStep + 1, height - y));
                context.FillRectangle(brush, pixelRect);
            }
        }
    }
}
