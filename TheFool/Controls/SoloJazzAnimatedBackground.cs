using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using System;

namespace TheFool.Controls
{
    public class SoloJazzAnimatedBackground : ContentControl
    {
        private double _time;
        private readonly DispatcherTimer _timer;
        
        public SoloJazzAnimatedBackground()
        {
            ClipToBounds = true;
            _timer = new DispatcherTimer(TimeSpan.FromSeconds(1.0/30.0), DispatcherPriority.Background, OnTick);
            _timer.Start();
        }

        private void OnTick(object? sender, EventArgs e)
        {
            _time += 1.0 / 30.0;
            
            // Create animated background brush with MUCH more visible rotation
            var rotation1 = _time * 45; // Much faster rotation
            var rotation2 = _time * -60;
            var rotation3 = _time * 30;
            
            // Use actual bright Solo Jazz colors
            var brush1 = CreateRotatingGradient(rotation1, Colors.White, Colors.DarkTurquoise);
            var brush2 = CreateRotatingGradient(rotation2, Colors.Magenta, Colors.White);
            var brush3 = CreateRotatingGradient(rotation3, Colors.Cyan, Colors.BlueViolet);
            
            // Cycle through different backgrounds faster
            var cycle = Math.Sin(_time * 1.5) * 0.5 + 0.5; // 0 to 1, faster cycling
            
            IBrush finalBrush;
            if (cycle < 0.33)
                finalBrush = brush1;
            else if (cycle < 0.66)
                finalBrush = brush2;
            else
                finalBrush = brush3;
                
            Background = finalBrush;
        }
        
        private LinearGradientBrush CreateRotatingGradient(double angleDegrees, Color color1, Color color2)
        {
            var angle = angleDegrees * Math.PI / 180.0; // Convert to radians
            
            // Calculate start and end points for the gradient
            var startX = 0.5 + 0.5 * Math.Cos(angle);
            var startY = 0.5 + 0.5 * Math.Sin(angle);
            var endX = 0.5 - 0.5 * Math.Cos(angle);
            var endY = 0.5 - 0.5 * Math.Sin(angle);
            
            return new LinearGradientBrush
            {
                StartPoint = new RelativePoint(startX, startY, RelativeUnit.Relative),
                EndPoint = new RelativePoint(endX, endY, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(255, color1.R, color1.G, color1.B), 0.0),
                    new GradientStop(Color.FromArgb(255, color2.R, color2.G, color2.B), 0.5),
                    new GradientStop(Color.FromArgb(255, color1.R, color1.G, color1.B), 1.0)
                }
            };
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            _timer.Stop();
            base.OnDetachedFromVisualTree(e);
        }
    }
}