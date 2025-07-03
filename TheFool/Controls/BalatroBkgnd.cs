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
        AvaloniaProperty.Register<BalatroBkgnd, double>(nameof(SpinSpeed), 1.0);
    
    public static readonly StyledProperty<double> MoveSpeedProperty =
        AvaloniaProperty.Register<BalatroBkgnd, double>(nameof(MoveSpeed), 3.0);
    
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
            Interval = TimeSpan.FromMilliseconds(15)
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
        
        // ULTRA-FAST simplified Balatro-style background
        const double PIXEL_SIZE = 20.0; // Much larger pixels for speed
        const double ANIMATION_SPEED = 0.5;
        
        // Balatro colors
        var COLOR_1 = Color.FromRgb(222, 68, 59);   // Red
        var COLOR_2 = Color.FromRgb(0, 107, 180);   // Blue  
        var COLOR_3 = Color.FromRgb(22, 35, 37);    // Dark background
        
        var pixelStep = (int)PIXEL_SIZE;
        var centerX = width * 0.5;
        var centerY = height * 0.5;
        var time = elapsed * ANIMATION_SPEED;
        
        // Pre-calculate common values
        var sinTime = Math.Sin(time);
        var cosTime = Math.Cos(time);
        
        // Render large pixelated blocks
        for (int x = 0; x < width; x += pixelStep)
        {
            for (int y = 0; y < height; y += pixelStep)
            {
                // Simple distance-based pattern
                var dx = (x - centerX) / width;
                var dy = (y - centerY) / height;
                var dist = Math.Sqrt(dx * dx + dy * dy);
                
                // Simple rotating pattern
                var angle = Math.Atan2(dy, dx) + time * 0.3;
                var wave = Math.Sin(dist * 8.0 + time * 2.0) * 0.5 + 0.5;
                var spiral = Math.Sin(angle * 3.0 + dist * 10.0) * 0.5 + 0.5;
                
                // Simple color mixing based on wave patterns
                var colorMix = (wave + spiral) * 0.5;
                
                Color pixelColor;
                if (colorMix < 0.33)
                {
                    // Blend COLOR_3 and COLOR_1
                    var t = colorMix * 3.0;
                    pixelColor = Color.FromRgb(
                        (byte)(COLOR_3.R + (COLOR_1.R - COLOR_3.R) * t),
                        (byte)(COLOR_3.G + (COLOR_1.G - COLOR_3.G) * t),
                        (byte)(COLOR_3.B + (COLOR_1.B - COLOR_3.B) * t));
                }
                else if (colorMix < 0.66)
                {
                    // Blend COLOR_1 and COLOR_2
                    var t = (colorMix - 0.33) * 3.0;
                    pixelColor = Color.FromRgb(
                        (byte)(COLOR_1.R + (COLOR_2.R - COLOR_1.R) * t),
                        (byte)(COLOR_1.G + (COLOR_2.G - COLOR_1.G) * t),
                        (byte)(COLOR_1.B + (COLOR_2.B - COLOR_1.B) * t));
                }
                else
                {
                    // Blend COLOR_2 and COLOR_3
                    var t = (colorMix - 0.66) * 3.0;
                    pixelColor = Color.FromRgb(
                        (byte)(COLOR_2.R + (COLOR_3.R - COLOR_2.R) * t),
                        (byte)(COLOR_2.G + (COLOR_3.G - COLOR_2.G) * t),
                        (byte)(COLOR_2.B + (COLOR_3.B - COLOR_2.B) * t));
                }
                
                var brush = new SolidColorBrush(pixelColor);
                var pixelRect = new Rect(x, y, Math.Min(pixelStep, width - x), Math.Min(pixelStep, height - y));
                context.FillRectangle(brush, pixelRect);
            }
        }
    }
}
