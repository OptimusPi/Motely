using System;
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
    private DateTime _startTime = DateTime.Now;
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
            Interval = TimeSpan.FromMilliseconds(16) // ~60fps
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
        // Dark background
        context.FillRectangle(new SolidColorBrush(Color.FromRgb(22, 35, 37)), Bounds);
        
        var elapsed = (DateTime.Now - _startTime).TotalSeconds;
        var width = Bounds.Width;
        var height = Bounds.Height;
        var centerX = width / 2;
        var centerY = height / 2;
        
        // Create "paint blobs" using bezier curves
        var blobCount = 12;
        for (int i = 0; i < blobCount; i++)
        {
            var t = elapsed * MoveSpeed * 0.1 + i * (Math.PI * 2 / blobCount);
            
            // Create flowing paint paths
            var pathFigure = new PathFigure();
            
            // Starting point with circular motion
            var startRadius = Math.Min(width, height) * 0.3;
            var startAngle = t + i * 0.5;
            var startX = centerX + Math.Cos(startAngle) * startRadius;
            var startY = centerY + Math.Sin(startAngle) * startRadius;
            pathFigure.StartPoint = new Point(startX, startY);
            
            // Create flowing bezier segments
            for (int j = 0; j < 4; j++)
            {
                var angle = startAngle + j * Math.PI / 2;
                var radius = startRadius * (1 + Math.Sin(t * 2 + j) * 0.3);
                
                // Control points for smooth curves
                var cp1X = centerX + Math.Cos(angle - 0.5) * radius * 1.5;
                var cp1Y = centerY + Math.Sin(angle - 0.5) * radius * 1.5;
                var cp2X = centerX + Math.Cos(angle + 0.5) * radius * 1.5;
                var cp2Y = centerY + Math.Sin(angle + 0.5) * radius * 1.5;
                
                // End point
                var endAngle = angle + Math.PI / 2;
                var endX = centerX + Math.Cos(endAngle) * radius;
                var endY = centerY + Math.Sin(endAngle) * radius;
                
                // Add flowing distortion
                cp1X += Math.Sin(t * 3 + i + j) * 30;
                cp1Y += Math.Cos(t * 3 + i + j) * 30;
                cp2X += Math.Sin(t * 2.5 + i + j) * 40;
                cp2Y += Math.Cos(t * 2.5 + i + j) * 40;
                
                pathFigure.Segments.Add(new BezierSegment
                {
                    Point1 = new Point(cp1X, cp1Y),
                    Point2 = new Point(cp2X, cp2Y),
                    Point3 = new Point(endX, endY)
                });
            }
            
            pathFigure.IsClosed = true;
            
            var pathGeometry = new PathGeometry();
            pathGeometry.Figures.Add(pathFigure);
            
            // Choose color based on position
            Color blobColor;
            var colorChoice = (i + Math.Sin(t)) % 3;
            if (colorChoice < 1)
                blobColor = Color.FromArgb(150, 222, 68, 59);  // Red
            else if (colorChoice < 2)
                blobColor = Color.FromArgb(150, 0, 107, 180); // Blue
            else
                blobColor = Color.FromArgb(150, 100, 100, 100); // Gray
            
            var brush = new SolidColorBrush(blobColor);
            
            // Draw the blob
            context.DrawGeometry(brush, null, pathGeometry);
        }
        
        // Add some smaller, faster moving circles for more motion
        for (int i = 0; i < 20; i++)
        {
            var t = elapsed * MoveSpeed * 0.5 + i;
            var x = centerX + Math.Sin(t * 0.7 + i) * width * 0.4;
            var y = centerY + Math.Cos(t * 0.5 + i) * height * 0.4;
            var size = 10 + Math.Sin(t * 2) * 5;
            
            var alpha = (byte)(50 + Math.Sin(t) * 30);
            var color = Color.FromArgb(alpha, 255, 255, 255);
            
            context.DrawEllipse(new SolidColorBrush(color), null, new Point(x, y), size, size);
        }
    }
}
