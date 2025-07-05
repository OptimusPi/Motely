using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Oracle.Models;
using Oracle.Services;

namespace Oracle.Controls;

public class PerformanceChart : UserControl
{
    private readonly PerformanceMonitor _monitor;
    private readonly DispatcherTimer _updateTimer;
    private readonly Queue<PerformanceDataPoint> _dataPoints = new(300); // 5 minutes at 1Hz

    public static readonly StyledProperty<IBrush> LineColorProperty =
        AvaloniaProperty.Register<PerformanceChart, IBrush>(nameof(LineColor), Brushes.Blue);

    public IBrush LineColor
    {
        get => GetValue(LineColorProperty);
        set => SetValue(LineColorProperty, value);
    }

    public PerformanceChart()
    {
        _monitor = new PerformanceMonitor();
        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _updateTimer.Tick += UpdateChart;
        
        Background = Brushes.Transparent;
        ClipToBounds = true;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _updateTimer.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _updateTimer.Stop();
    }

    private void UpdateChart(object? sender, EventArgs e)
    {
        var metrics = _monitor.GetCurrentMetrics();
        
        _dataPoints.Enqueue(new PerformanceDataPoint
        {
            Timestamp = DateTime.Now,
            CpuUsage = metrics.CpuUsage,
            MemoryUsage = metrics.MemoryUsage,
            GpuUsage = metrics.GpuUsage
        });

        while (_dataPoints.Count > 300)
            _dataPoints.Dequeue();

        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        
        if (_dataPoints.Count < 2) return;

        var bounds = Bounds;
        var points = _dataPoints.ToArray();
        
        // Draw background grid
        var gridPen = new Pen(Brushes.Gray, 0.5, new DashStyle(new double[] { 2, 2 }, 0));
        
        // Horizontal grid lines (every 25%)
        for (int i = 1; i < 4; i++)
        {
            var y = bounds.Height * i / 4;
            context.DrawLine(gridPen, new Point(0, y), new Point(bounds.Width, y));
        }
        
        // Vertical grid lines (every minute)
        for (int i = 1; i < 5; i++)
        {
            var x = bounds.Width * i / 5;
            context.DrawLine(gridPen, new Point(x, 0), new Point(x, bounds.Height));
        }

        // Simple line chart rendering for CPU usage
        var path = new StreamGeometry();
        using (var ctx = path.Open())
        {
            var xScale = bounds.Width / 300.0;
            var yScale = bounds.Height / 100.0;
            
            ctx.BeginFigure(new Point(0, bounds.Height - points[0].CpuUsage * yScale), false);
            
            for (int i = 1; i < points.Length; i++)
            {
                ctx.LineTo(new Point(i * xScale, bounds.Height - points[i].CpuUsage * yScale));
            }
        }

        context.DrawGeometry(null, new Pen(LineColor, 2), path);
        
        // Draw labels
        var typeface = new Typeface("Arial");
        var textBrush = Brushes.Black;
        
        var cpuText = new FormattedText("CPU %", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 12, textBrush);
        context.DrawText(cpuText, new Point(5, 5));
        
        if (points.Length > 0)
        {
            var lastValue = points[^1].CpuUsage;
            var valueText = new FormattedText($"{lastValue:F1}%", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 12, textBrush);
            context.DrawText(valueText, new Point(bounds.Width - 50, 5));
        }
    }
}
